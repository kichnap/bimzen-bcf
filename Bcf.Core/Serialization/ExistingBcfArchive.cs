using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using Bcf.Core.Model;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// An archive the export updates rather than creates anew.
    ///
    /// Topics the export did not touch are carried over byte for byte: a file
    /// that has been through a receiving tool holds statuses, comments,
    /// attachments and document references this model does not keep. Rebuilding
    /// such a topic from the model would quietly impoverish it, and the user
    /// would learn about it a week later and not from us.
    ///
    /// Архив, который выгрузка обновляет, а не создаёт заново.
    ///
    /// Замечания, которых выгрузка не касалась, переносятся побайтово: в файле,
    /// побывавшем у приёмника, лежат статусы, комментарии, вложения и ссылки
    /// на документы, которых эта модель не хранит. Пересобрать такое замечание
    /// из модели значит молча его обеднить, а пользователь узнает об этом через
    /// неделю и не от нас.
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

                // A duplicate identifier is possible in someone else's
                // archive; the first one wins, as it does when reading
                if (!_topics.ContainsKey(topic.Guid)) _topics.Add(topic.Guid, topic);
            }

            IndexEntries();
        }

        /// <summary>
        /// The version the archive declares.
        /// Версия, объявленная в архиве.
        /// </summary>
        public BcfVersion Version { get; }

        /// <summary>
        /// What reading the archive had to complain about.
        /// На что пришлось пожаловаться при чтении архива.
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// How many topics the file held before the update.
        /// Сколько замечаний было в файле до обновления.
        /// </summary>
        public int TopicCount
        {
            get { return _topics.Count; }
        }

        /// <summary>
        /// Opens an archive for updating. The stream stays with the caller.
        /// Открывает архив для обновления. Поток остаётся за вызывающим.
        /// </summary>
        /// <param name="archiveStream">The archive to read.</param>
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

        /// <summary>
        /// The topic with this identifier, or null.
        /// Замечание с таким идентификатором или null.
        /// </summary>
        /// <param name="topicGuid">The identifier to look for.</param>
        public BcfTopic Find(Guid topicGuid)
        {
            BcfTopic topic;

            return _topics.TryGetValue(topicGuid, out topic) ? topic : null;
        }

        /// <summary>
        /// Marks a topic as handled: the export wrote it itself, so the old
        /// version must not be carried over.
        ///
        /// Помечает замечание обработанным: выгрузка написала его сама,
        /// и переносить старую версию не нужно.
        /// </summary>
        /// <param name="topicGuid">The identifier of the topic.</param>
        public void MarkHandled(Guid topicGuid)
        {
            _handled.Add(topicGuid);
        }

        /// <summary>
        /// Carries a topic into the new archive exactly as it is.
        /// Переносит замечание в новый архив ровно как есть.
        /// </summary>
        /// <param name="topicGuid">The identifier of the topic.</param>
        /// <param name="writer">The writer of the new archive.</param>
        /// <returns>False when the archive holds no folder for that topic.</returns>
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
        /// Carries over every topic the export did not touch. A clash has been
        /// resolved and no longer appears in the test — the topic about it must
        /// stay in the file rather than vanish.
        ///
        /// Переносит все замечания, которых выгрузка не касалась. Коллизия
        /// разобрана и в проверку больше не попадает — замечание о ней должно
        /// остаться в файле, а не исчезнуть.
        /// </summary>
        /// <param name="writer">The writer of the new archive.</param>
        /// <returns>How many topics were carried over.</returns>
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
        /// Carries over the entries outside topic folders — the 3.0 documents
        /// folder and whatever a receiving tool put there. The files the export
        /// writes itself (bcf.version, project.bcfp, the vocabulary
        /// declaration) are skipped.
        ///
        /// Переносит записи вне папок замечаний — папку documents 3.0 и всё,
        /// что положил туда приёмник. Файлы, которые выгрузка пишет сама
        /// (bcf.version, project.bcfp, объявление справочников), пропускаются.
        /// </summary>
        /// <param name="writer">The writer of the new archive.</param>
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

        /// <inheritdoc />
        public void Dispose()
        {
            _archive.Dispose();
        }

        private static bool CopyEntry(ZipArchiveEntry entry, BcfArchiveWriter writer)
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
                // Directory entries: zero length and a name ending in a slash
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

        /// <summary>
        /// The topic an entry belongs to; empty when the entry is not inside a
        /// topic folder.
        ///
        /// Замечание, которому принадлежит запись; пусто, если запись лежит
        /// не в папке замечания.
        /// </summary>
        private static Guid TopicOf(string entryName)
        {
            int slash = entryName.IndexOf('/');
            if (slash <= 0) return Guid.Empty;

            Guid guid;

            return Guid.TryParseExact(entryName.Substring(0, slash), "D", out guid) ? guid : Guid.Empty;
        }

        /// <summary>
        /// The files the export writes itself: carrying them over would produce
        /// duplicates.
        ///
        /// Файлы, которые пишет сама выгрузка: перенеся их, мы получили бы
        /// дубли.
        /// </summary>
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

        /// <inheritdoc />
        public override string ToString()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "Existing archive: version {0}, topics {1}",
                Version.ToVersionId(), _topics.Count);
        }
    }
}
