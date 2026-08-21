using System;
using System.IO;
using System.Text;
using Bcf.Core.Clash;
using Bcf.Core.Conversion;
using Xunit;

namespace Bcf.Core.Tests
{
    public class TopicGuidMapTests
    {
        private const string Key = "1d5b52aa10366bb8b87145df767892e758035a149656b19a74f4e7c3341094b6";

        [Fact]
        public void Map_SurvivesRoundTrip()
        {
            Guid guid = Guid.NewGuid();

            var map = new TopicGuidMap();
            map.Remember(Key, guid);

            TopicGuidMap restored = ReadBack(map);

            Guid restoredGuid;
            Assert.True(restored.TryGet(Key, out restoredGuid));
            Assert.Equal(guid, restoredGuid);
        }

        [Fact]
        public void Map_IsDirtyOnlyAfterRealChanges()
        {
            var map = new TopicGuidMap();
            Assert.False(map.IsDirty);

            Guid guid = Guid.NewGuid();
            map.Remember(Key, guid);
            Assert.True(map.IsDirty);

            using (var buffer = new MemoryStream())
            {
                map.Write(buffer);
            }

            Assert.False(map.IsDirty);

            // Тот же ключ с тем же значением — не изменение
            map.Remember(Key, guid);
            Assert.False(map.IsDirty);

            map.Remember(Key, Guid.NewGuid());
            Assert.True(map.IsDirty);
        }

        [Fact]
        public void Map_WritesGuidsInLowerCase()
        {
            var map = new TopicGuidMap();
            map.Remember(Key, Guid.NewGuid());

            string json = Text(map);

            Assert.DoesNotContain("GUID\":\"", json, StringComparison.Ordinal);
            Assert.Matches(@"""guid"":\s*""[a-f0-9-]{36}""", json);
        }

        [Fact]
        public void Map_KeepsEntriesSorted()
        {
            // Файл лежит рядом с .nwf и попадает в систему контроля версий:
            // произвольный порядок давал бы шум в каждом сравнении
            var map = new TopicGuidMap();
            map.Remember("cccc", Guid.NewGuid());
            map.Remember("aaaa", Guid.NewGuid());
            map.Remember("bbbb", Guid.NewGuid());

            string json = Text(map);

            Assert.True(json.IndexOf("aaaa", StringComparison.Ordinal) < json.IndexOf("bbbb", StringComparison.Ordinal));
            Assert.True(json.IndexOf("bbbb", StringComparison.Ordinal) < json.IndexOf("cccc", StringComparison.Ordinal));
        }

        [Fact]
        public void MissingFile_IsNotAnError()
        {
            // Так выглядит первая выгрузка документа
            TopicGuidMap map = TopicGuidMap.ReadFile(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

            Assert.Equal(0, map.Count);
        }

        [Fact]
        public void BrokenFile_Throws_AndDoesNotPretendToBeEmpty()
        {
            // Молча начать с чистой карты значит продублировать на сервере
            // все топики — пользователь обязан узнать об этом
            using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes("{ это не json")))
            {
                Assert.Throws<InvalidDataException>(() => TopicGuidMap.Read(buffer));
            }
        }

        [Fact]
        public void UnknownEntries_AreSkipped_NotFatal()
        {
            const string json = "{\"version\":1,\"topics\":[" +
                                "{\"key\":\"good\",\"guid\":\"614ce774-2268-8a69-a7c8-71b52ea587f4\"}," +
                                "{\"key\":\"broken\",\"guid\":\"не guid\"}," +
                                "{\"key\":\"\",\"guid\":\"614ce774-2268-8a69-a7c8-71b52ea587f4\"}]}";

            TopicGuidMap map;
            using (var buffer = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                map = TopicGuidMap.Read(buffer);
            }

            Assert.Equal(1, map.Count);

            Guid guid;
            Assert.True(map.TryGet("good", out guid));
        }

        [Fact]
        public void File_IsWrittenAtomically()
        {
            string directory = Path.Combine(Path.GetTempPath(), "bcf-map-" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(directory, "модель" + TopicGuidMap.FileExtension);

            try
            {
                var map = new TopicGuidMap();
                map.Remember(Key, StableTopicKey.ToTopicGuid(Key));
                map.WriteFile(path);

                Assert.True(File.Exists(path));
                // Временный файл не остаётся рядом
                Assert.Empty(Directory.GetFiles(directory, "*.tmp"));

                TopicGuidMap restored = TopicGuidMap.ReadFile(path);
                Assert.Equal(1, restored.Count);

                // Повторная запись поверх существующего файла не падает
                map.Remember("second", Guid.NewGuid());
                map.WriteFile(path);

                Assert.Equal(2, TopicGuidMap.ReadFile(path).Count);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void EmptyKey_IsIgnored()
        {
            var map = new TopicGuidMap();
            map.Remember(null, Guid.NewGuid());
            map.Remember(string.Empty, Guid.NewGuid());

            Assert.Equal(0, map.Count);

            Guid guid;
            Assert.False(map.TryGet(null, out guid));
        }

        private static TopicGuidMap ReadBack(TopicGuidMap map)
        {
            using (var buffer = new MemoryStream())
            {
                map.Write(buffer);

                using (var reading = new MemoryStream(buffer.ToArray()))
                {
                    return TopicGuidMap.Read(reading);
                }
            }
        }

        private static string Text(TopicGuidMap map)
        {
            using (var buffer = new MemoryStream())
            {
                map.Write(buffer);
                return new UTF8Encoding(false).GetString(buffer.ToArray());
            }
        }
    }
}
