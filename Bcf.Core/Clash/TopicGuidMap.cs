using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// The store of topic identifiers already issued.
    ///
    /// A coordinator exports the same set of clashes once a week, and the
    /// topics must not duplicate. The stable key
    /// (<see cref="Conversion.StableTopicKey"/>) settles that for the first
    /// export; this store settles it for every later one, including the case
    /// where a server issued an identifier of its own that differs from the
    /// deterministic one.
    ///
    /// Хранилище уже выданных идентификаторов замечаний.
    ///
    /// Координатор выгружает один и тот же набор коллизий раз в неделю,
    /// и замечания не должны дублироваться. Устойчивый ключ
    /// (<see cref="Conversion.StableTopicKey"/>) решает это для первой
    /// выгрузки, а хранилище — для всех последующих, в том числе когда сервер
    /// выдал свой идентификатор, отличный от детерминированного.
    /// </summary>
    public interface ITopicGuidStore
    {
        /// <summary>
        /// The identifier issued for this key earlier.
        /// Идентификатор, выданный этому ключу раньше.
        /// </summary>
        /// <param name="key">The stable topic key.</param>
        /// <param name="guid">The identifier that was found.</param>
        bool TryGet(string key, out Guid guid);

        /// <summary>
        /// Remembers the identifier for this key.
        /// Запоминает идентификатор за этим ключом.
        /// </summary>
        /// <param name="key">The stable topic key.</param>
        /// <param name="guid">The identifier to keep.</param>
        void Remember(string key, Guid guid);
    }

    /// <summary>
    /// A "stable key to topic identifier" map kept as JSON.
    ///
    /// The format is deliberately flat and readable: the file lives next to the
    /// model, people will look at it, carry it between machines and put it
    /// under version control.
    ///
    /// Карта «устойчивый ключ → идентификатор замечания», хранимая в JSON.
    ///
    /// Формат намеренно плоский и читаемый: файл лежит рядом с моделью, люди
    /// будут в него смотреть, переносить между машинами и класть в систему
    /// контроля версий.
    /// </summary>
    public sealed class TopicGuidMap : ITopicGuidStore
    {
        /// <summary>
        /// The file extension of the map.
        /// Расширение файла карты.
        /// </summary>
        public const string FileExtension = ".bcfmap.json";

        private readonly Dictionary<string, Guid> _topics = new Dictionary<string, Guid>(StringComparer.Ordinal);

        /// <summary>
        /// How many identifiers have been issued.
        /// Сколько идентификаторов уже выдано.
        /// </summary>
        public int Count
        {
            get { return _topics.Count; }
        }

        /// <summary>
        /// Whether new entries appeared since the map was loaded.
        /// Появились ли новые записи с момента загрузки.
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Looks up the identifier issued for this key earlier.
        /// Ищет идентификатор, выданный этому ключу раньше.
        /// </summary>
        /// <param name="key">The stable topic key.</param>
        /// <param name="guid">The identifier that was found.</param>
        public bool TryGet(string key, out Guid guid)
        {
            if (string.IsNullOrEmpty(key))
            {
                guid = Guid.Empty;
                return false;
            }

            return _topics.TryGetValue(key, out guid);
        }

        /// <summary>
        /// Remembers the identifier for this key.
        /// Запоминает идентификатор за этим ключом.
        /// </summary>
        /// <param name="key">The stable topic key.</param>
        /// <param name="guid">The identifier to keep.</param>
        public void Remember(string key, Guid guid)
        {
            if (string.IsNullOrEmpty(key)) return;

            Guid existing;
            if (_topics.TryGetValue(key, out existing) && existing == guid) return;

            _topics[key] = guid;
            IsDirty = true;
        }

        /// <summary>
        /// Reads the map from a stream.
        /// Читает карту из потока.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <exception cref="InvalidDataException">
        /// The file is damaged. The caller must tell the user rather than
        /// quietly start from an empty map: a fresh map means a duplicate of
        /// every topic at the receiving end.
        ///
        /// Файл повреждён. Вызывающий обязан сказать об этом пользователю,
        /// а не молча начать с пустой карты: пустая карта означает дубль
        /// каждого замечания у приёмника.
        /// </exception>
        public static TopicGuidMap Read(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var map = new TopicGuidMap();

            TopicGuidMapFile file;

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(TopicGuidMapFile));
                file = (TopicGuidMapFile)serializer.ReadObject(stream);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException("The identifier map does not parse: " + ex.Message, ex);
            }

            if (file?.Topics == null) return map;

            foreach (TopicGuidEntry entry in file.Topics)
            {
                Guid guid;
                if (entry == null || string.IsNullOrEmpty(entry.Key) || !Guid.TryParse(entry.Guid, out guid)) continue;

                map._topics[entry.Key] = guid;
            }

            return map;
        }

        /// <summary>
        /// Writes the map into a stream. UTF-8 without a BOM, entries sorted —
        /// the file is friendly to a version diff.
        ///
        /// Пишет карту в поток. UTF-8 без BOM, записи отсортированы — файл
        /// дружелюбен к сравнению версий.
        /// </summary>
        /// <param name="stream">The stream to write into.</param>
        public void Write(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            var file = new TopicGuidMapFile
            {
                Version = 1,
                Generator = "BIMzen NaVi",
                Topics = new List<TopicGuidEntry>(_topics.Count)
            };

            var keys = new List<string>(_topics.Keys);
            keys.Sort(StringComparer.Ordinal);

            foreach (string key in keys)
            {
                file.Topics.Add(new TopicGuidEntry
                {
                    Key = key,
                    Guid = Conversion.StableTopicKey.FormatGuid(_topics[key])
                });
            }

            var serializer = new DataContractJsonSerializer(typeof(TopicGuidMapFile));

            using (var writer = JsonReaderWriterFactory.CreateJsonWriter(stream, new UTF8Encoding(false), false, true, "  "))
            {
                serializer.WriteObject(writer, file);
                writer.Flush();
            }

            IsDirty = false;
        }

        /// <summary>
        /// Reads the map from a file. A missing file is not an error: that is
        /// what the first export of this document looks like.
        ///
        /// Читает карту из файла. Отсутствующий файл — не ошибка: так выглядит
        /// первая выгрузка этого документа.
        /// </summary>
        public static TopicGuidMap ReadFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return new TopicGuidMap();

            using (FileStream stream = File.OpenRead(path))
            {
                return Read(stream);
            }
        }

        /// <summary>
        /// Writes the map to a file, creating the folder when needed.
        /// Пишет карту в файл, создавая папку при необходимости.
        /// </summary>
        /// <param name="path">Where to write.</param>
        public void WriteFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("The map path is empty.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            // Written through a temporary file: an interrupted write must not
            // leave a damaged map — losing it duplicates every topic
            string temporary = path + ".tmp";

            using (FileStream stream = File.Create(temporary))
            {
                Write(stream);
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        [DataContract]
        private sealed class TopicGuidMapFile
        {
            [DataMember(Name = "version", Order = 0)]
            public int Version { get; set; }

            [DataMember(Name = "generator", Order = 1)]
            public string Generator { get; set; }

            [DataMember(Name = "topics", Order = 2)]
            public List<TopicGuidEntry> Topics { get; set; }
        }

        [DataContract]
        private sealed class TopicGuidEntry
        {
            [DataMember(Name = "key", Order = 0)]
            public string Key { get; set; }

            [DataMember(Name = "guid", Order = 1)]
            public string Guid { get; set; }
        }
    }

    /// <summary>
    /// A map that lives in memory only. It is used when there is nowhere to
    /// keep the identifiers — a one-off export from an unsaved document, for
    /// instance.
    ///
    /// Карта, живущая только в памяти. Нужна, когда хранить идентификаторы
    /// негде — например, при разовой выгрузке из несохранённого документа.
    /// </summary>
    public sealed class InMemoryTopicGuidStore : ITopicGuidStore
    {
        private readonly Dictionary<string, Guid> _topics = new Dictionary<string, Guid>(StringComparer.Ordinal);

        /// <summary>
        /// Looks up the identifier issued for this key earlier.
        /// Ищет идентификатор, выданный этому ключу раньше.
        /// </summary>
        /// <param name="key">The stable topic key.</param>
        /// <param name="guid">The identifier that was found.</param>
        public bool TryGet(string key, out Guid guid)
        {
            return _topics.TryGetValue(key ?? string.Empty, out guid);
        }

        /// <summary>
        /// Remembers the identifier for this key.
        /// Запоминает идентификатор за этим ключом.
        /// </summary>
        /// <param name="key">The stable topic key.</param>
        /// <param name="guid">The identifier to keep.</param>
        public void Remember(string key, Guid guid)
        {
            if (!string.IsNullOrEmpty(key)) _topics[key] = guid;
        }
    }
}
