using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Запись архива BCF.
    ///
    /// Топики пишутся по одному, потоком: держать пять тысяч замечаний
    /// со снимками в памяти нельзя. Справочники и проект дописываются в конце,
    /// когда известен полный список встреченных исполнителей.
    ///
    /// Версии 2.1 и 3.0 реализованы отдельными наследниками, а не флагом внутри
    /// одного кода: у них по-разному устроен markup (комментарии и точки зрения
    /// в 2.1 лежат вне топика), по-разному объявляются справочники и различаются
    /// ограничения схем.
    /// </summary>
    public abstract class BcfArchiveWriter : IDisposable
    {
        private readonly ZipArchive _archive;
        private readonly List<string> _users = new List<string>();
        private readonly BcfExtraVocabulary _extraVocabulary = new BcfExtraVocabulary();
        private readonly HashSet<string> _entryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _declaredLabels = new HashSet<string>(StringComparer.Ordinal);
        private bool _completed;

        /// <summary>
        /// Opens an archive for writing over the destination stream.
        /// Открывает архив для записи поверх потока назначения.
        /// </summary>
        /// <param name="destination">Where the archive is written; the caller owns the stream.</param>
        /// <param name="options">What to write and how.</param>
        protected BcfArchiveWriter(Stream destination, BcfWriteOptions options)
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));

            Options = options ?? throw new ArgumentNullException(nameof(options));
            Report = new BcfWriteReport();

            _archive = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true);

            if (!string.IsNullOrWhiteSpace(options.Author))
            {
                _users.Add(options.Author);
            }
        }

        /// <summary>Настройки записи.</summary>
        protected BcfWriteOptions Options { get; }

        /// <summary>Итог записи: счётчики и предупреждения.</summary>
        public BcfWriteReport Report { get; }

        /// <summary>Версия формата, которую пишет этот сериализатор.</summary>
        public abstract BcfVersion Version { get; }

        /// <summary>Создаёт сериализатор нужной версии.</summary>
        public static BcfArchiveWriter Create(Stream destination, BcfWriteOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            switch (options.Version)
            {
                case BcfVersion.Bcf30: return new Bcf30ArchiveWriter(destination, options);
                case BcfVersion.Bcf21: return new Bcf21ArchiveWriter(destination, options);

                case BcfVersion.Bcf20:
                    // Reading 2.0 is a courtesy to old archives; writing it would
                    // mean shipping a third serializer for a version nobody asks
                    // to receive any more.
                    throw new NotSupportedException(
                        "BCF 2.0 archives can be read but not written. Choose 2.1 or 3.0.");
                default:
                    throw new ArgumentOutOfRangeException(nameof(options), options.Version, "Неизвестная версия BCF.");
            }
        }

        /// <summary>
        /// Пишет один топик: markup, файлы точек зрения и снимки.
        /// </summary>
        public void WriteTopic(BcfTopic topic)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));
            if (_completed) throw new InvalidOperationException("Архив уже закрыт вызовом Complete().");

            Validate(topic);
            CollectUsers(topic);

            WriteXmlEntry(BcfEntryNames.MarkupEntry(topic.Guid), writer => WriteMarkup(writer, topic));

            foreach (BcfViewpoint viewpoint in topic.Viewpoints)
            {
                WriteXmlEntry(
                    BcfEntryNames.ViewpointEntry(topic.Guid, viewpoint.Guid),
                    writer => WriteVisualizationInfo(writer, viewpoint));

                Report.ViewpointsWritten++;

                if (Options.IncludeSnapshots && viewpoint.Snapshot != null && viewpoint.Snapshot.Length > 0)
                {
                    WriteBinaryEntry(
                        BcfEntryNames.SnapshotEntry(topic.Guid, viewpoint.SnapshotFileName),
                        viewpoint.Snapshot);

                    Report.SnapshotsWritten++;
                }
            }

            Report.TopicsWritten++;
        }

        /// <summary>
        /// Переносит запись чужого архива как есть.
        ///
        /// При обновлении существующего файла замечания, которых выгрузка
        /// не касалась, копируются побайтово, а не пересобираются из модели:
        /// в них могут лежать вложения, ссылки на документы и точки зрения,
        /// которых модель не хранит, и пересборка молча их потеряла бы.
        /// </summary>
        /// <returns>false, если имя записи не годится для архива BCF.</returns>
        public bool CopyEntry(string entryName, Stream content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            if (_completed) throw new InvalidOperationException("Архив уже закрыт вызовом Complete().");

            try
            {
                BcfEntryNames.Validate(entryName);
            }
            catch (ArgumentException ex)
            {
                Report.Warn("Запись '" + entryName + "' не перенесена: " + ex.Message);
                return false;
            }

            if (!_entryNames.Add(entryName))
            {
                Report.Warn("Запись '" + entryName + "' встретилась дважды, перенесена первая.");
                return false;
            }

            // PNG и JPEG уже сжаты — второй проход только тратит время
            CompressionLevel level = IsAlreadyCompressed(entryName)
                ? CompressionLevel.Fastest
                : CompressionLevel.Optimal;

            using (Stream target = CreateEntry(entryName, level))
            {
                content.CopyTo(target);
            }

            Report.EntriesCopied++;

            return true;
        }

        /// <summary>
        /// Объявляет метку, которой нет в справочнике.
        ///
        /// Нужно для меток-идентификаторов — имён групп коллизий: строгая
        /// проверка их бы не пропустила, а файл обязан объявлять всё, что
        /// в нём есть. Объявление узкое и намеренное: сюда попадает только
        /// то, что вызывающий назвал явно, а не любая строка из топика.
        /// </summary>
        public void DeclareLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return;

            _declaredLabels.Add(label.Trim());
            _extraVocabulary.AddTopicLabel(label);
        }

        /// <summary>
        /// Учитывает замечание, перенесённое из чужого архива: его участники
        /// и его значения справочника должны попасть в extensions, иначе
        /// файл объявляет меньше, чем содержит.
        /// </summary>
        public void RegisterCopiedTopic(BcfTopic topic)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            CollectUsers(topic);
            CollectExtraVocabulary(topic);
        }

        /// <summary>
        /// Дописывает bcf.version, project.bcfp и справочники. После вызова
        /// топики добавлять нельзя.
        /// </summary>
        public void Complete()
        {
            if (_completed) return;

            WriteXmlEntry(BcfEntryNames.Version, WriteVersionFile);
            WriteXmlEntry(BcfEntryNames.Project, WriteProjectFile);
            WriteExtensions(NormalizedUsers(), _extraVocabulary);

            _completed = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the archive. Complete() is deliberately not called here.
        /// Освобождает архив. Complete() здесь намеренно не вызывается.
        /// </summary>
        /// <param name="disposing">True when called from Dispose rather than from a finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!disposing) return;

            // Complete() намеренно не вызывается сам: незавершённый архив
            // должен остаться незавершённым, а не притвориться целым.
            _archive.Dispose();
        }

        /// <summary>Пишет markup.bcf топика.</summary>
        protected abstract void WriteMarkup(XmlWriter writer, BcfTopic topic);

        /// <summary>Пишет файл точки зрения.</summary>
        protected abstract void WriteVisualizationInfo(XmlWriter writer, BcfViewpoint viewpoint);

        /// <summary>Пишет bcf.version.</summary>
        protected abstract void WriteVersionFile(XmlWriter writer);

        /// <summary>Пишет project.bcfp.</summary>
        protected abstract void WriteProjectFile(XmlWriter writer);

        /// <summary>
        /// Writes the vocabulary declaration: extensions.xml in 3.0,
        /// extensions.xsd in 2.1.
        ///
        /// Пишет объявление справочников: extensions.xml в 3.0,
        /// extensions.xsd в 2.1.
        /// </summary>
        /// <param name="users">The people met while writing, already normalised.</param>
        /// <param name="extra">Values that arrived with the topics carried over.</param>
        protected abstract void WriteExtensions(IReadOnlyList<string> users, BcfExtraVocabulary extra);

        /// <summary>Создаёт запись архива и отдаёт поток для записи.</summary>
        protected Stream CreateEntry(string entryName, CompressionLevel compressionLevel)
        {
            BcfEntryNames.Validate(entryName);

            _entryNames.Add(entryName);

            ZipArchiveEntry entry = _archive.CreateEntry(entryName, compressionLevel);

            // Метка времени записи по умолчанию — текущий момент. Заданная явно
            // делает архив воспроизводимым: два прогона с одними данными дают
            // одинаковые байты, и эталонные файлы не «шумят» в истории
            if (Options.EntryTimestamp.HasValue) entry.LastWriteTime = Options.EntryTimestamp.Value;

            return entry.Open();
        }

        /// <summary>Пишет XML-запись: UTF-8 без BOM, с отступами.</summary>
        protected void WriteXmlEntry(string entryName, Action<XmlWriter> body)
        {
            using (Stream stream = CreateEntry(entryName, CompressionLevel.Optimal))
            using (XmlWriter writer = CreateXmlWriter(stream))
            {
                writer.WriteStartDocument();
                body(writer);
                writer.WriteEndDocument();
            }
        }

        /// <summary>Пишет двоичную запись — снимок.</summary>
        protected void WriteBinaryEntry(string entryName, byte[] content)
        {
            // PNG уже сжат: тратить время на повторное сжатие незачем,
            // а на пяти тысячах снимков это заметная часть экспорта.
            using (Stream stream = CreateEntry(entryName, CompressionLevel.Fastest))
            {
                stream.Write(content, 0, content.Length);
            }
        }

        /// <summary>Пишет запись из встроенного ресурса — например, markup.xsd для 2.1.</summary>
        protected void WriteResourceEntry(string entryName, string resourceName)
        {
            using (Stream source = Resources.EmbeddedResources.Open(resourceName))
            using (Stream target = CreateEntry(entryName, CompressionLevel.Optimal))
            {
                source.CopyTo(target);
            }
        }

        /// <summary>GUID в том виде, в каком его требует схема: нижний регистр, формат D.</summary>
        protected static string FormatGuid(Guid guid)
        {
            return guid.ToString("D", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        /// <summary>Пишет элемент, если значение непустое.</summary>
        protected static void WriteOptionalElement(XmlWriter writer, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            writer.WriteElementString(name, value);
        }

        /// <summary>Пишет дату, если она задана.</summary>
        protected static void WriteOptionalDate(XmlWriter writer, string name, DateTimeOffset? value)
        {
            if (!value.HasValue) return;

            writer.WriteElementString(name, BcfNumber.Format(value.Value));
        }

        /// <summary>Пишет точку или направление: X, Y, Z в инвариантном формате.</summary>
        protected static void WriteVector(XmlWriter writer, string elementName, Vector3 value)
        {
            writer.WriteStartElement(elementName);
            writer.WriteElementString("X", BcfNumber.Format(value.X));
            writer.WriteElementString("Y", BcfNumber.Format(value.Y));
            writer.WriteElementString("Z", BcfNumber.Format(value.Z));
            writer.WriteEndElement();
        }

        /// <summary>Пишет элемент Component.</summary>
        protected static void WriteComponent(XmlWriter writer, BcfComponent component)
        {
            writer.WriteStartElement("Component");

            if (!string.IsNullOrWhiteSpace(component.IfcGuid))
            {
                writer.WriteAttributeString("IfcGuid", component.IfcGuid);
            }

            WriteOptionalElement(writer, "OriginatingSystem", component.OriginatingSystem);
            WriteOptionalElement(writer, "AuthoringToolId", component.AuthoringToolId);

            writer.WriteEndElement();
        }

        /// <summary>Пишет секущие плоскости, если они есть.</summary>
        protected static void WriteClippingPlanes(XmlWriter writer, BcfViewpoint viewpoint)
        {
            if (viewpoint.ClippingPlanes.Count == 0) return;

            writer.WriteStartElement("ClippingPlanes");

            foreach (BcfClippingPlane plane in viewpoint.ClippingPlanes)
            {
                writer.WriteStartElement("ClippingPlane");
                WriteVector(writer, "Location", plane.Location);
                WriteVector(writer, "Direction", plane.Direction);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private static XmlWriter CreateXmlWriter(Stream stream)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                // UTF-8 без BOM: на BOM ломаются чужие парсеры, .NET его переживает
                Encoding = new UTF8Encoding(false),
                CloseOutput = false
            };

            return XmlWriter.Create(stream, settings);
        }

        /// <summary>
        /// Строгая проверка перед записью: значение вне справочника — исключение
        /// на этапе сборки топика, а не молчаливая запись в файл.
        /// </summary>
        private void Validate(BcfTopic topic)
        {
            if (topic.Guid == Guid.Empty)
            {
                throw new InvalidOperationException("У топика пустой Guid.");
            }

            if (string.IsNullOrWhiteSpace(topic.Title))
            {
                throw new InvalidOperationException("У топика " + FormatGuid(topic.Guid) + " пустой заголовок, схема требует непустой.");
            }

            if (string.IsNullOrWhiteSpace(topic.CreationAuthor))
            {
                throw new InvalidOperationException("У топика " + FormatGuid(topic.Guid) + " не задан автор.");
            }

            if (!Options.ValidateVocabulary) return;

            BcfVocabulary.EnsureTopicType(topic.TopicType);
            BcfVocabulary.EnsureTopicStatus(topic.TopicStatus);

            if (!string.IsNullOrWhiteSpace(topic.Priority)) BcfVocabulary.EnsurePriority(topic.Priority);
            if (!string.IsNullOrWhiteSpace(topic.Stage)) BcfVocabulary.EnsureStage(topic.Stage);

            foreach (string label in topic.Labels)
            {
                if (_declaredLabels.Contains(label)) continue;

                BcfVocabulary.EnsureTopicLabel(label);
            }
        }

        private static bool IsAlreadyCompressed(string entryName)
        {
            return entryName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                   || entryName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                   || entryName.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase);
        }

        private void CollectExtraVocabulary(BcfTopic topic)
        {
            _extraVocabulary.AddTopicType(topic.TopicType);
            _extraVocabulary.AddTopicStatus(topic.TopicStatus);
            _extraVocabulary.AddPriority(topic.Priority);
            _extraVocabulary.AddStage(topic.Stage);

            foreach (string label in topic.Labels)
            {
                _extraVocabulary.AddTopicLabel(label);
            }
        }

        private void CollectUsers(BcfTopic topic)
        {
            AddUser(topic.CreationAuthor);
            AddUser(topic.ModifiedAuthor);
            AddUser(topic.AssignedTo);

            foreach (BcfComment comment in topic.Comments)
            {
                AddUser(comment.Author);
                AddUser(comment.ModifiedAuthor);
            }
        }

        private void AddUser(string user)
        {
            if (!string.IsNullOrWhiteSpace(user))
            {
                _users.Add(user);
            }
        }

        private IReadOnlyList<string> NormalizedUsers()
        {
            IReadOnlyList<string> skipped;
            IReadOnlyList<string> users = BcfUsers.Normalize(_users, out skipped);

            if (skipped.Count > 0)
            {
                Report.Warn(
                    "В список Users не попали значения, не похожие на email (" + skipped.Count + "): " +
                    string.Join(", ", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(skipped, 10))) +
                    (skipped.Count > 10 ? " и другие" : string.Empty) +
                    ". В самих замечаниях поле AssignedTo сохранено как есть.");
            }

            return users;
        }
    }
}
