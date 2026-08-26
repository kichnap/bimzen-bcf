using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace Bcf.Core.Clash
{
    /// <summary>
    /// Хранилище выданных идентификаторов замечаний.
    ///
    /// Клиент выгружает один и тот же набор коллизий раз в неделю, и топики
    /// не должны дублироваться. Устойчивый ключ (<see cref="Conversion.StableTopicKey"/>)
    /// решает это для первой выгрузки, а хранилище — для всех последующих,
    /// в том числе когда идентификатор пришёл с сервера и отличается
    /// от детерминированного.
    /// </summary>
    public interface ITopicGuidStore
    {
        /// <summary>Ранее выданный идентификатор для ключа.</summary>
        bool TryGet(string key, out Guid guid);

        /// <summary>Запоминает идентификатор за ключом.</summary>
        void Remember(string key, Guid guid);
    }

    /// <summary>
    /// Карта «устойчивый ключ -> Guid замечания», хранимая в JSON.
    ///
    /// Формат намеренно плоский и читаемый: файл лежит рядом с .nwf, его будут
    /// видеть люди, переносить между машинами и класть в систему контроля версий.
    /// </summary>
    public sealed class TopicGuidMap : ITopicGuidStore
    {
        /// <summary>Расширение файла карты.</summary>
        public const string FileExtension = ".bcfmap.json";

        private readonly Dictionary<string, Guid> _topics = new Dictionary<string, Guid>(StringComparer.Ordinal);

        /// <summary>Сколько идентификаторов уже выдано.</summary>
        public int Count
        {
            get { return _topics.Count; }
        }

        /// <summary>Появились ли новые записи с момента загрузки.</summary>
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

        /// <summary>Читает карту из потока.</summary>
        /// <exception cref="InvalidDataException">Файл повреждён — вызывающий обязан сообщить об этом пользователю, а не молча начать с чистой карты: иначе на сервере появятся дубли всех топиков.</exception>
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
                throw new InvalidDataException("Файл карты идентификаторов не разбирается: " + ex.Message, ex);
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

        /// <summary>Пишет карту в поток. UTF-8 без BOM, записи отсортированы — файл дружелюбен к сравнению версий.</summary>
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

        /// <summary>Пишет карту в файл, создавая папку при необходимости.</summary>
        public void WriteFile(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("Пустой путь к карте.", nameof(path));

            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory)) Directory.CreateDirectory(directory);

            // Пишем через временный файл: прерванная запись не должна оставить
            // повреждённую карту — потерять её значит продублировать все топики
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
    /// Карта, живущая только в памяти. Используется, когда сохранять
    /// идентификаторы некуда — например, в разовой выгрузке из несохранённого
    /// документа.
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
