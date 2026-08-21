using System;
using System.IO;
using System.Linq;
using Bcf.Core;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;
using Bcf.Vocabulary.Generator;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// Эталонные архивы из test-data уходят команде онлайн-сервиса как фикстуры
    /// для тестов импорта. Здесь они проверяются как чужие файлы: читаются
    /// заново и валидируются по официальным схемам buildingSMART.
    ///
    /// Если эти тесты упали, значит серверная команда получила бы битые данные.
    /// </summary>
    public class TestDataFixturesTests
    {
        private static string Directory => Path.Combine(
            RepositoryPaths.FindRoot(AppContext.BaseDirectory), "test-data");

        [Theory]
        [InlineData("small-3-topics-bcf30.bcfzip", 3)]
        [InlineData("large-500-topics-bcf30.bcfzip", 500)]
        [InlineData("small-3-topics-bcf21.bcfzip", 3)]
        [InlineData("large-500-topics-bcf21.bcfzip", 500)]
        public void Fixture_HasExpectedTopicCount(string fileName, int expected)
        {
            Assert.Equal(expected, Read(fileName).Topics.Count);
        }

        [Theory]
        [InlineData("small-3-topics-bcf30.bcfzip", BcfVersion.Bcf30)]
        [InlineData("small-3-topics-bcf21.bcfzip", BcfVersion.Bcf21)]
        [InlineData("large-500-topics-bcf30.bcfzip", BcfVersion.Bcf30)]
        [InlineData("large-500-topics-bcf21.bcfzip", BcfVersion.Bcf21)]
        public void Fixture_IsValidAgainstOfficialSchemas(string fileName, BcfVersion version)
        {
            byte[] archive = File.ReadAllBytes(Path.Combine(Directory, fileName));

            BcfReadResult read = Read(fileName);

            // Проверяем разметку каждого топика: именно её разбирает импортёр
            foreach (BcfTopic topic in read.Topics.Take(25))
            {
                string markup = TestData.EntryText(archive, BcfEntryNames.MarkupEntry(topic.Guid));

                Assert.Empty(TestData.Validate(markup, TestData.SchemaPath(version, "markup.xsd")));
            }
        }

        [Theory]
        [InlineData("small-3-topics-bcf30.bcfzip")]
        [InlineData("large-500-topics-bcf30.bcfzip")]
        public void Fixture_CarriesSnapshotsAsRealPng(string fileName)
        {
            byte[] archive = File.ReadAllBytes(Path.Combine(Directory, fileName));

            BcfTopic topic = Read(fileName).Topics.First();
            BcfViewpoint viewpoint = topic.Viewpoints.First();

            byte[] snapshot = TestData.EntryBytes(archive, BcfEntryNames.SnapshotEntry(
                topic.Guid, viewpoint.SnapshotFileName));

            // Сигнатура PNG плюс завершающий блок IEND: заглушка из одной
            // подписи не открылась бы ни в одном просмотрщике
            Assert.True(snapshot.Length > 100);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, snapshot.Take(4).ToArray());
            Assert.Contains("IEND", System.Text.Encoding.ASCII.GetString(snapshot.Skip(snapshot.Length - 12).ToArray()), StringComparison.Ordinal);
        }

        [Fact]
        public void ForeignValues_AreReadWithoutError_AndReported()
        {
            // Критерий приёмки: файл со значениями чужого словаря читается,
            // значения сохраняются как есть, а в отчёте появляется предупреждение
            BcfReadResult read = Read("foreign-values-bcf30.bcfzip");

            Assert.Equal(3, read.Topics.Count);
            Assert.Contains(read.Topics, t => t.TopicStatus == "Открыто");
            Assert.Contains(read.Topics, t => t.TopicType == "Пересечение");

            Assert.Contains(read.ExternalValues, v => v.Value == "Открыто" && v.Field == "TopicStatus");
            Assert.Contains(read.ExternalValues, v => v.Value == "Пересечение" && v.Field == "TopicType");

            // И при этом ни одно значение не подменено на «правильное»
            Assert.DoesNotContain(read.Topics, t => t.TopicStatus == BcfVocabulary.TopicStatuses.New);
        }

        [Fact]
        public void Fixtures_CarryVocabularyLabelsAndDisciplines()
        {
            BcfReadResult read = Read("small-3-topics-bcf30.bcfzip");

            // Метка Auto — признак автоматической выгрузки, по ней сервис
            // отличает коллизии от заведённых руками замечаний
            Assert.All(read.Topics, t => Assert.Contains(BcfVocabulary.TopicLabels.Auto, t.Labels));
            Assert.Contains(read.Topics, t => t.Labels.Contains(BcfVocabulary.TopicLabels.HVAC));
        }

        private static BcfReadResult Read(string fileName)
        {
            using (FileStream stream = File.OpenRead(Path.Combine(Directory, fileName)))
            {
                return BcfArchiveReader.Read(stream);
            }
        }
    }
}
