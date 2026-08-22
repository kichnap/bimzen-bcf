using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Bcf.Core.Model;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Архив, который выгрузка обновляет, а не создаёт заново.
    ///
    /// Замечания, которых выгрузка не касалась, переносятся побайтово:
    /// в файле, побывавшем у приёмника, лежат статусы, комментарии, вложения
    /// и ссылки на документы, которых наша модель не хранит. Пересобрать
    /// такое замечание из модели значит молча его обеднить, а пользователь
    /// узнает об этом через неделю и не от нас.
    /// </summary>
    public sealed class ExistingBcfArchive : IDisposable
    {
        private readonly ZipArchive _archive;
        private readonly Dictionary<Guid, List<ZipArchiveEntry>> _topicEntries = new Dictionary<Guid, List<ZipArchiveEntry>>();
        private readonly List<ZipArchiveEntry> _otherEntries = new List<ZipArchiveEntry>();
        private readonly Dictionary<Guid, BcfTopic> _topics = new Dictionary<Guid, BcfTopic>();
        private readonly HashSet<Guid> _handled = new HashSet<Guid>();

        private ExistingBcfArchive(ZipArchive archive, BcfReadResult read)
        {
            _archive = archive;

            Version = read.Version;
            Warnings = read.Warnings;

            foreach (BcfTopic topic in read.Topics)
            {
                if (topic.Guid == Guid.Empty) continue;

                // Дубль GUID в чужом архиве возможен; выигрывает первый,
                // как и при чтении
                if (!_topics.ContainsKey(topic.Guid)) _topics.Add(topic.Guid, topic);
            }

            IndexEntries();
        }

        /// <summary>Версия, объявленная в архиве.</summary>
        public BcfVersion Version { get; }

        /// <summary>Предупреждения чтения.</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>Сколько замечаний было в файле до обновления.</summary>
        public int TopicCount
        {
            get { return _topics.Count; }
        }

        /// <summary>Открывает архив для обновления. Поток остаётся за вызывающим.</summary>
        public static ExistingBcfArchive Open(Stream archiveStream)
        {
            if (archiveStream == null) throw new ArgumentNullException(nameof(archiveStream));

            var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true);

            try
            {
                return new ExistingBcfArchive(archive, BcfArchiveReader.Read(archive));
            }
            catch
            {
                archive.Dispose();
                throw;
            }
        }

        /// <summary>Замечание с таким идентификатором или null.</summary>
        public BcfTopic Find(Guid topicGuid)
        {
            BcfTopic topic;

            return _topics.TryGetValue(topicGuid, out topic) ? topic : null;
        }

        /// <summary>
        /// Помечает замечание как обработанное: выгрузка написала его сама,
        /// и переносить старую версию не нужно.
        /// </summary>
        public void MarkHandled(Guid topicGuid)
        {
            _handled.Add(topicGuid);
        }

        /// <summary>
        /// Переносит замечание в новый архив как есть.
        /// </summary>
        /// <returns>false, если папки замечания в архиве не оказалось.</returns>
        public bool CopyTopic(Guid topicGuid, BcfArchiveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            List<ZipArchiveEntry> entries;
            if (!_topicEntries.TryGetValue(topicGuid, out entries) || entries.Count == 0) return false;

            _handled.Add(topicGuid);

            foreach (ZipArchiveEntry entry in entries)
            {
                CopyEntry(entry, writer);
            }

            BcfTopic topic = Find(topicGuid);
            if (topic != null) writer.RegisterCopiedTopic(topic);

            return true;
        }

        /// <summary>
        /// Переносит все замечания, которых выгрузка не касалась. Коллизия
        /// разобрана и в проверку больше не попадает — замечание о ней должно
        /// остаться в файле, а не исчезнуть.
        /// </summary>
        /// <returns>Сколько замечаний перенесено.</returns>
        public int CopyRemainingTopics(BcfArchiveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int copied = 0;

            foreach (KeyValuePair<Guid, List<ZipArchiveEntry>> pair in _topicEntries)
            {
                if (_handled.Contains(pair.Key)) continue;

                if (CopyTopic(pair.Key, writer)) copied++;
            }

            return copied;
        }

        /// <summary>
        /// Переносит записи вне папок замечаний — папку documents 3.0 и всё,
        /// что положил туда приёмник. Файлы, которые выгрузка пишет сама
        /// (bcf.version, project.bcfp, справочники), пропускаются.
        /// </summary>
        public int CopyExtraEntries(BcfArchiveWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));

            int copied = 0;

            foreach (ZipArchiveEntry entry in _otherEntries)
            {
                if (CopyEntry(entry, writer)) copied++;
            }

            return copied;
        }

        public void Dispose()
        {
            _archive.Dispose();
        }

        private bool CopyEntry(ZipArchiveEntry entry, BcfArchiveWriter writer)
        {
            using (Stream content = entry.Open())
            {
                return writer.CopyEntry(entry.FullName, content);
            }
        }

        private void IndexEntries()
        {
            foreach (ZipArchiveEntry entry in _archive.Entries)
            {
                // Записи-папки: длина ноль и имя на слэше
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal)) continue;

                Guid topicGuid = TopicOf(entry.FullName);

                if (topicGuid != Guid.Empty)
                {
                    List<ZipArchiveEntry> entries;
                    if (!_topicEntries.TryGetValue(topicGuid, out entries))
                    {
                        entries = new List<ZipArchiveEntry>();
                        _topicEntries.Add(topicGuid, entries);
                    }

                    entries.Add(entry);
                    continue;
                }

                if (IsOurs(entry.FullName)) continue;

                _otherEntries.Add(entry);
            }
        }

        /// <summary>Замечание, которому принадлежит запись; пусто — запись не из папки замечания.</summary>
        private static Guid TopicOf(string entryName)
        {
            int slash = entryName.IndexOf('/');
            if (slash <= 0) return Guid.Empty;

            Guid guid;

            return Guid.TryParseExact(entryName.Substring(0, slash), "D", out guid) ? guid : Guid.Empty;
        }

        /// <summary>Файлы, которые пишет сама выгрузка: переносить их нельзя, будут дубли.</summary>
        private static bool IsOurs(string entryName)
        {
            return Same(entryName, BcfEntryNames.Version)
                   || Same(entryName, BcfEntryNames.Project)
                   || Same(entryName, "extensions.xml")
                   || Same(entryName, "extensions.xsd")
                   || Same(entryName, "markup.xsd")
                   || Same(entryName, "visinfo.xsd")
                   || Same(entryName, "project.xsd")
                   || Same(entryName, "version.xsd");
        }

        private static bool Same(string entryName, string name)
        {
            return string.Equals(entryName, name, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Существующий архив: версия {0}, замечаний {1}",
                Version.ToVersionId(), _topics.Count);
        }
    }
}
