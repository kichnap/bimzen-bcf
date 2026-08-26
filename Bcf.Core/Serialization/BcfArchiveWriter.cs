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
    /// Writing a BCF archive.
    ///
    /// Topics are written one at a time, as a stream: five thousand topics with
    /// their snapshots cannot be held in memory. The vocabularies and the
    /// project are appended at the end, once the full list of assignees met
    /// along the way is known.
    ///
    /// Versions 2.1 and 3.0 are separate subclasses rather than a flag inside
    /// one body of code: their markup is built differently (in 2.1 comments and
    /// viewpoints sit outside the topic), they declare vocabularies differently,
    /// and their schema limits differ.
    ///
    /// Запись архива BCF.
    ///
    /// Замечания пишутся по одному, потоком: держать пять тысяч замечаний
    /// со снимками в памяти нельзя. Справочники и проект дописываются в конце,
    /// когда известен полный список встреченных исполнителей.
    ///
    /// Версии 2.1 и 3.0 сделаны отдельными наследниками, а не флагом внутри
    /// одного кода: у них по-разному устроена разметка (в 2.1 комментарии
    /// и точки зрения лежат вне замечания), по-разному объявляются справочники
    /// и различаются ограничения схем.
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

        /// <summary>
        /// What to write and how.
        /// Что писать и как.
        /// </summary>
        protected BcfWriteOptions Options { get; }

        /// <summary>
        /// The outcome of writing: the counters and the warnings.
        /// Итог записи: счётчики и предупреждения.
        /// </summary>
        public BcfWriteReport Report { get; }

        /// <summary>
        /// The format version this serializer writes.
        /// Версия формата, которую пишет этот сериализатор.
        /// </summary>
        public abstract BcfVersion Version { get; }

        /// <summary>
        /// Creates the serializer for the version asked for.
        /// Создаёт сериализатор нужной версии.
        /// </summary>
        /// <param name="destination">Where the archive is written; the caller owns the stream.</param>
        /// <param name="options">What to write and how.</param>
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
                    throw new ArgumentOutOfRangeException(nameof(options), options.Version, "Unknown BCF version.");
            }
        }

        /// <summary>
        /// Writes one topic: the markup, the viewpoint files and the snapshots.
        /// Пишет одно замечание: разметку, файлы точек зрения и снимки.
        /// </summary>
        /// <param name="topic">The topic to write.</param>
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
        /// Carries over an entry of someone else's archive exactly as it is.
        ///
        /// When an existing file is updated, the topics the export did not touch
        /// are copied byte for byte rather than rebuilt from the model: they may
        /// hold attachments, document references and viewpoints the model does
        /// not keep, and rebuilding would quietly lose them.
        ///
        /// Переносит запись чужого архива ровно как есть.
        ///
        /// При обновлении существующего файла замечания, которых выгрузка
        /// не касалась, копируются побайтово, а не пересобираются из модели:
        /// в них могут лежать вложения, ссылки на документы и точки зрения,
        /// которых модель не хранит, и пересборка молча их потеряла бы.
        /// </summary>
        /// <param name="entryName">The name of the entry in the archive.</param>
        /// <param name="content">The bytes of the entry.</param>
        /// <returns>False when the entry name will not do for a BCF archive.</returns>
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
                Report.Warn("The entry '" + entryName + "' was not carried over: " + ex.Message);
                return false;
            }

            if (!_entryNames.Add(entryName))
            {
                Report.Warn("The entry '" + entryName + "' was met twice; the first one was carried over.");
                return false;
            }

            // PNG and JPEG are compressed already — a second pass only costs time
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
        /// Declares a label the vocabulary does not hold.
        ///
        /// This exists for labels that act as identifiers — the names of clash
        /// groups: the strict check would not let them through, yet the file has
        /// to declare everything it contains. The declaration is narrow on
        /// purpose: only what the caller named explicitly gets in, not every
        /// string found in a topic.
        ///
        /// Объявляет метку, которой нет в справочнике.
        ///
        /// Нужно для меток-идентификаторов — имён групп коллизий: строгая
        /// проверка их бы не пропустила, а файл обязан объявлять всё, что в нём
        /// есть. Объявление узкое намеренно: сюда попадает только то, что
        /// вызывающий назвал явно, а не любая строка из замечания.
        /// </summary>
        /// <param name="label">The label to declare.</param>
        public void DeclareLabel(string label)
        {
            if (string.IsNullOrWhiteSpace(label)) return;

            _declaredLabels.Add(label.Trim());
            _extraVocabulary.AddTopicLabel(label);
        }

        /// <summary>
        /// Takes account of a topic carried over from someone else's archive:
        /// its people and its vocabulary values have to reach the extensions, or
        /// the file declares less than it holds.
        ///
        /// Учитывает замечание, перенесённое из чужого архива: его участники
        /// и его значения справочника должны попасть в extensions, иначе файл
        /// объявляет меньше, чем содержит.
        /// </summary>
        /// <param name="topic">The topic that was carried over.</param>
        public void RegisterCopiedTopic(BcfTopic topic)
        {
            if (topic == null) throw new ArgumentNullException(nameof(topic));

            CollectUsers(topic);
            CollectExtraVocabulary(topic);
        }

        /// <summary>
        /// Appends bcf.version, project.bcfp and the vocabularies. After this
        /// call no more topics can be added.
        ///
        /// Дописывает bcf.version, project.bcfp и справочники. После вызова
        /// добавлять замечания нельзя.
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

            // Complete() is deliberately not called on our own: an unfinished
            // archive must stay unfinished rather than pretend to be whole.
            _archive.Dispose();
        }

        /// <summary>
        /// Writes the markup.bcf of a topic.
        /// Пишет markup.bcf замечания.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="topic">The topic to write.</param>
        protected abstract void WriteMarkup(XmlWriter writer, BcfTopic topic);

        /// <summary>
        /// Writes a viewpoint file.
        /// Пишет файл точки зрения.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="viewpoint">The viewpoint to write.</param>
        protected abstract void WriteVisualizationInfo(XmlWriter writer, BcfViewpoint viewpoint);

        /// <summary>
        /// Writes bcf.version.
        /// Пишет bcf.version.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        protected abstract void WriteVersionFile(XmlWriter writer);

        /// <summary>
        /// Writes project.bcfp.
        /// Пишет project.bcfp.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
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

        /// <summary>
        /// Creates an archive entry and hands back a stream to write into.
        /// Создаёт запись архива и отдаёт поток для записи.
        /// </summary>
        /// <param name="entryName">The name of the entry.</param>
        /// <param name="compressionLevel">How hard to compress it.</param>
        protected Stream CreateEntry(string entryName, CompressionLevel compressionLevel)
        {
            BcfEntryNames.Validate(entryName);

            _entryNames.Add(entryName);

            ZipArchiveEntry entry = _archive.CreateEntry(entryName, compressionLevel);

            // By default an entry is stamped with the current moment. Setting
            // the stamp explicitly makes the archive reproducible: two runs over
            // the same data give the same bytes, and reference files stop
            // rustling in the history
            if (Options.EntryTimestamp.HasValue) entry.LastWriteTime = Options.EntryTimestamp.Value;

            return entry.Open();
        }

        /// <summary>
        /// Writes an XML entry: UTF-8 without a BOM, indented.
        /// Пишет XML-запись: UTF-8 без BOM, с отступами.
        /// </summary>
        /// <param name="entryName">The name of the entry.</param>
        /// <param name="body">What to write into it.</param>
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

        /// <summary>
        /// Writes a binary entry — a snapshot.
        /// Пишет двоичную запись — снимок.
        /// </summary>
        /// <param name="entryName">The name of the entry.</param>
        /// <param name="content">The bytes to write.</param>
        protected void WriteBinaryEntry(string entryName, byte[] content)
        {
            // PNG is compressed already: spending time on compressing it again
            // buys nothing, and across five thousand snapshots it is a visible
            // part of the export.
            using (Stream stream = CreateEntry(entryName, CompressionLevel.Fastest))
            {
                stream.Write(content, 0, content.Length);
            }
        }

        /// <summary>
        /// Writes an entry out of an embedded resource — markup.xsd for 2.1, for
        /// instance.
        ///
        /// Пишет запись из встроенного ресурса — например, markup.xsd для 2.1.
        /// </summary>
        /// <param name="entryName">The name of the entry.</param>
        /// <param name="resourceName">The name of the embedded resource.</param>
        protected void WriteResourceEntry(string entryName, string resourceName)
        {
            using (Stream source = Resources.EmbeddedResources.Open(resourceName))
            using (Stream target = CreateEntry(entryName, CompressionLevel.Optimal))
            {
                source.CopyTo(target);
            }
        }

        /// <summary>
        /// An identifier in the shape the schema demands: lower case, the D
        /// format.
        ///
        /// Идентификатор в том виде, какого требует схема: нижний регистр,
        /// формат D.
        /// </summary>
        /// <param name="guid">The identifier to format.</param>
        protected static string FormatGuid(Guid guid)
        {
            return guid.ToString("D", System.Globalization.CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        /// <summary>
        /// Writes an element when the value is not empty.
        /// Пишет элемент, если значение непустое.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="name">The element name.</param>
        /// <param name="value">The value to write.</param>
        protected static void WriteOptionalElement(XmlWriter writer, string name, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            writer.WriteElementString(name, value);
        }

        /// <summary>
        /// Writes a date when there is one.
        /// Пишет дату, если она задана.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="name">The element name.</param>
        /// <param name="value">The date to write.</param>
        protected static void WriteOptionalDate(XmlWriter writer, string name, DateTimeOffset? value)
        {
            if (!value.HasValue) return;

            writer.WriteElementString(name, BcfNumber.Format(value.Value));
        }

        /// <summary>
        /// Writes a point or a direction: X, Y, Z in the invariant format.
        /// Пишет точку или направление: X, Y, Z в инвариантном формате.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="elementName">The element name.</param>
        /// <param name="value">The vector to write.</param>
        protected static void WriteVector(XmlWriter writer, string elementName, Vector3 value)
        {
            writer.WriteStartElement(elementName);
            writer.WriteElementString("X", BcfNumber.Format(value.X));
            writer.WriteElementString("Y", BcfNumber.Format(value.Y));
            writer.WriteElementString("Z", BcfNumber.Format(value.Z));
            writer.WriteEndElement();
        }

        /// <summary>
        /// Writes a Component element.
        /// Пишет элемент Component.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="component">The component to write.</param>
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

        /// <summary>
        /// Writes the clipping planes, when the viewpoint has any.
        /// Пишет секущие плоскости, если они есть.
        /// </summary>
        /// <param name="writer">The writer of the entry.</param>
        /// <param name="viewpoint">The viewpoint to take them from.</param>
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
                // UTF-8 without a BOM: other parsers break on a BOM, .NET survives it
                Encoding = new UTF8Encoding(false),
                CloseOutput = false
            };

            return XmlWriter.Create(stream, settings);
        }

        /// <summary>
        /// The strict check before writing: a value outside the vocabulary
        /// raises an exception while the topic is being built rather than
        /// travelling quietly into the file.
        ///
        /// Строгая проверка перед записью: значение вне справочника даёт
        /// исключение на этапе сборки замечания, а не молчаливую запись
        /// в файл.
        /// </summary>
        private void Validate(BcfTopic topic)
        {
            if (topic.Guid == Guid.Empty)
            {
                throw new InvalidOperationException("The topic has an empty identifier.");
            }

            if (string.IsNullOrWhiteSpace(topic.Title))
            {
                throw new InvalidOperationException("The topic " + FormatGuid(topic.Guid) + " has an empty title, and the schema demands a non-empty one.");
            }

            if (string.IsNullOrWhiteSpace(topic.CreationAuthor))
            {
                throw new InvalidOperationException("The topic " + FormatGuid(topic.Guid) + " has no author.");
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
                    "Values that do not look like an email address did not reach the Users list (" + skipped.Count + "): " +
                    string.Join(", ", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Take(skipped, 10))) +
                    (skipped.Count > 10 ? " and others" : string.Empty) +
                    ". Inside the topics themselves AssignedTo is kept as it is.");
            }

            return users;
        }
    }
}
