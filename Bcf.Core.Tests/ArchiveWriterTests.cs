using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Bcf.Core;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;
using Xunit;

namespace Bcf.Core.Tests
{
    public class ArchiveWriterTests
    {
        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void Markup_IsValidAgainstOfficialSchema(BcfVersion version)
        {
            BcfTopic topic = TestData.Topic();
            byte[] archive = TestData.WriteArchive(version, topic);

            string markup = TestData.EntryText(archive, BcfEntryNames.MarkupEntry(topic.Guid));

            Assert.Empty(TestData.Validate(markup, TestData.SchemaPath(version, "markup.xsd")));
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void Viewpoint_IsValidAgainstOfficialSchema(BcfVersion version)
        {
            BcfTopic topic = TestData.Topic();
            byte[] archive = TestData.WriteArchive(version, topic);

            string viewpoint = TestData.EntryText(
                archive, BcfEntryNames.ViewpointEntry(topic.Guid, topic.Viewpoints[0].Guid));

            Assert.Empty(TestData.Validate(viewpoint, TestData.SchemaPath(version, "visinfo.xsd")));
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void ProjectAndVersionFiles_AreValid(BcfVersion version)
        {
            byte[] archive = TestData.WriteArchive(version, TestData.Topic());

            Assert.Empty(TestData.Validate(
                TestData.EntryText(archive, BcfEntryNames.Project),
                TestData.SchemaPath(version, "project.xsd")));

            Assert.Empty(TestData.Validate(
                TestData.EntryText(archive, BcfEntryNames.Version),
                TestData.SchemaPath(version, "version.xsd")));
        }

        [Fact]
        public void Bcf21Markup_IsValidAgainstGeneratedExtensionSchema()
        {
            // Самая честная проверка для 2.1: markup проверяется не голой схемой,
            // а сгенерированной extensions.xsd из этого же архива — то есть
            // заодно проверяется, что значения справочника ей соответствуют.
            BcfTopic topic = TestData.Topic();
            byte[] archive = TestData.WriteArchive(BcfVersion.Bcf21, topic);

            string directory = TestData.Extract(archive);

            try
            {
                string markup = System.IO.File.ReadAllText(
                    System.IO.Path.Combine(directory, BcfEntryNames.TopicFolder(topic.Guid), BcfEntryNames.Markup));

                IReadOnlyList<string> errors = TestData.Validate(
                    markup, System.IO.Path.Combine(directory, ExtensionsWriter.Bcf21FileName));

                Assert.Empty(errors);
            }
            finally
            {
                System.IO.Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Bcf30Archive_HasExpectedEntries()
        {
            BcfTopic topic = TestData.Topic();
            IReadOnlyList<string> entries = TestData.EntryNames(TestData.WriteArchive(BcfVersion.Bcf30, topic));

            Assert.Contains(BcfEntryNames.Version, entries);
            Assert.Contains(BcfEntryNames.Project, entries);
            Assert.Contains(ExtensionsWriter.Bcf30FileName, entries);
            Assert.Contains(BcfEntryNames.MarkupEntry(topic.Guid), entries);
            Assert.Contains(BcfEntryNames.ViewpointEntry(topic.Guid, topic.Viewpoints[0].Guid), entries);
            Assert.Contains(BcfEntryNames.SnapshotEntry(topic.Guid, "snapshot.png"), entries);

            // В 3.0 схема справочников не нужна: значения объявлены в extensions.xml
            Assert.DoesNotContain(ExtensionsWriter.Bcf21RedefinedSchema, entries);
        }

        [Fact]
        public void Bcf21Archive_CarriesMarkupSchemaForRedefine()
        {
            IReadOnlyList<string> entries = TestData.EntryNames(TestData.WriteArchive(BcfVersion.Bcf21, TestData.Topic()));

            Assert.Contains(ExtensionsWriter.Bcf21FileName, entries);
            // extensions.xsd переопределяет типы markup.xsd — без самой схемы
            // рядом объявление справочников не разрешится
            Assert.Contains(ExtensionsWriter.Bcf21RedefinedSchema, entries);
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void EntryNames_AreAsciiAndUseForwardSlashes(BcfVersion version)
        {
            // Заголовок топика и имя модели в тесте нарочно кириллические:
            // в имена записей это просочиться не должно
            foreach (string name in TestData.EntryNames(TestData.WriteArchive(version, TestData.Topic())))
            {
                Assert.All(name, c => Assert.True(c < 128, "Не-ASCII в имени записи: " + name));
                Assert.DoesNotContain("\\", name, StringComparison.Ordinal);
                Assert.False(name.StartsWith("./", StringComparison.Ordinal));
                Assert.False(name.StartsWith("/", StringComparison.Ordinal));
            }
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void Archive_HasNoStrayEntries(BcfVersion version)
        {
            BcfTopic topic = TestData.Topic();
            IReadOnlyList<string> entries = TestData.EntryNames(TestData.WriteArchive(version, topic));

            var expected = new HashSet<string>(StringComparer.Ordinal)
            {
                BcfEntryNames.Version,
                BcfEntryNames.Project,
                BcfEntryNames.MarkupEntry(topic.Guid),
                BcfEntryNames.ViewpointEntry(topic.Guid, topic.Viewpoints[0].Guid),
                BcfEntryNames.SnapshotEntry(topic.Guid, "snapshot.png"),
                version == BcfVersion.Bcf30 ? ExtensionsWriter.Bcf30FileName : ExtensionsWriter.Bcf21FileName
            };

            if (version == BcfVersion.Bcf21) expected.Add(ExtensionsWriter.Bcf21RedefinedSchema);

            Assert.Equal(expected.OrderBy(e => e, StringComparer.Ordinal), entries.OrderBy(e => e, StringComparer.Ordinal));
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void XmlEntries_HaveNoByteOrderMark(BcfVersion version)
        {
            BcfTopic topic = TestData.Topic();
            byte[] archive = TestData.WriteArchive(version, topic);

            foreach (string name in TestData.EntryNames(archive))
            {
                if (name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                byte[] bytes = TestData.EntryBytes(archive, name);

                Assert.False(
                    bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF,
                    "BOM в записи " + name);
            }
        }

        [Fact]
        public void Guids_AreLowercase()
        {
            var topic = TestData.Topic();
            byte[] archive = TestData.WriteArchive(BcfVersion.Bcf30, topic);
            string markup = TestData.EntryText(archive, BcfEntryNames.MarkupEntry(topic.Guid));

            // Схема 3.0 проверяет GUID шаблоном [a-f0-9], заглавные не пройдут
            Assert.Contains(topic.Guid.ToString("D").ToLowerInvariant(), markup, StringComparison.Ordinal);
            Assert.DoesNotContain(topic.Guid.ToString("D").ToUpperInvariant(), markup, StringComparison.Ordinal);
        }

        [Fact]
        public void Numbers_StayInvariant_OnRussianLocale()
        {
            CultureInfo previous = CultureInfo.CurrentCulture;

            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");

                BcfTopic topic = TestData.Topic();
                byte[] archive = TestData.WriteArchive(BcfVersion.Bcf30, topic);
                string viewpoint = TestData.EntryText(
                    archive, BcfEntryNames.ViewpointEntry(topic.Guid, topic.Viewpoints[0].Guid));

                // 10 футов = 3.048 м. С запятой это значение развалит парсер на Node
                Assert.Contains("<X>3.048</X>", viewpoint, StringComparison.Ordinal);
                Assert.Empty(TestData.Validate(viewpoint, TestData.SchemaPath(BcfVersion.Bcf30, "visinfo.xsd")));
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = previous;
            }
        }

        [Fact]
        public void Bcf21_ReportsWhatItDrops()
        {
            BcfTopic topic = TestData.Topic();
            topic.ServerAssignedId = "BCF-42";
            topic.Comments.Add(new BcfComment
            {
                Guid = Guid.NewGuid(),
                Date = topic.CreationDate,
                Author = "coordinator@example.com",
                Text = null
            });

            BcfWriteReport report;
            TestData.WriteArchive(TestData.Options(BcfVersion.Bcf21), out report, topic);

            Assert.Contains("ServerAssignedId", report.DroppedFields);
            Assert.Contains("AspectRatio", report.DroppedFields);
            Assert.Contains(report.Warnings, w => w.Contains("without text"));
        }

        [Fact]
        public void Bcf21_ClampsFieldOfViewAndSaysSo()
        {
            BcfTopic topic = TestData.Topic();
            ((BcfPerspectiveCamera)topic.Viewpoints[0].Camera).FieldOfViewDegrees = 90;

            BcfWriteReport report;
            byte[] archive = TestData.WriteArchive(TestData.Options(BcfVersion.Bcf21), out report, topic);

            string viewpoint = TestData.EntryText(
                archive, BcfEntryNames.ViewpointEntry(topic.Guid, topic.Viewpoints[0].Guid));

            Assert.Contains("<FieldOfView>60</FieldOfView>", viewpoint, StringComparison.Ordinal);
            Assert.Contains(report.Warnings, w => w.Contains("clamped"));
            Assert.Empty(TestData.Validate(viewpoint, TestData.SchemaPath(BcfVersion.Bcf21, "visinfo.xsd")));
        }

        [Fact]
        public void Bcf30_KeepsWideFieldOfView()
        {
            BcfTopic topic = TestData.Topic();
            ((BcfPerspectiveCamera)topic.Viewpoints[0].Camera).FieldOfViewDegrees = 90;

            byte[] archive = TestData.WriteArchive(BcfVersion.Bcf30, topic);
            string viewpoint = TestData.EntryText(
                archive, BcfEntryNames.ViewpointEntry(topic.Guid, topic.Viewpoints[0].Guid));

            Assert.Contains("<FieldOfView>90</FieldOfView>", viewpoint, StringComparison.Ordinal);
        }

        [Fact]
        public void ValueOutsideVocabulary_ThrowsBeforeWriting()
        {
            BcfTopic topic = TestData.Topic();
            topic.TopicStatus = "Открыто";

            Assert.Throws<BcfVocabularyException>(() => TestData.WriteArchive(BcfVersion.Bcf30, topic));
        }

        [Fact]
        public void TopicWithoutTitle_Throws()
        {
            BcfTopic topic = TestData.Topic();
            topic.Title = "  ";

            Assert.Throws<InvalidOperationException>(() => TestData.WriteArchive(BcfVersion.Bcf30, topic));
        }

        [Fact]
        public void Bcf30_ViewpointWithoutCamera_Throws()
        {
            // В 3.0 камера обязательна: выбор между двумя типами объявлен
            // без minOccurs="0"
            BcfTopic topic = TestData.Topic();
            topic.Viewpoints[0].Camera = null;

            Assert.Throws<InvalidOperationException>(() => TestData.WriteArchive(BcfVersion.Bcf30, topic));
        }

        [Fact]
        public void Extensions_ContainAuthorAndAssignees()
        {
            byte[] archive = TestData.WriteArchive(BcfVersion.Bcf30, TestData.Topic());
            string extensions = TestData.EntryText(archive, ExtensionsWriter.Bcf30FileName);

            Assert.Contains("<User>coordinator@example.com</User>", extensions, StringComparison.Ordinal);
            Assert.Contains("<User>hvac@example.com</User>", extensions, StringComparison.Ordinal);
        }

        [Fact]
        public void NonEmailAssignee_StaysInTopic_ButNotInUsers()
        {
            BcfTopic topic = TestData.Topic();
            topic.AssignedTo = "Иванов (ОВ)";

            BcfWriteReport report;
            byte[] archive = TestData.WriteArchive(TestData.Options(BcfVersion.Bcf30), out report, topic);

            string markup = TestData.EntryText(archive, BcfEntryNames.MarkupEntry(topic.Guid));
            string extensions = TestData.EntryText(archive, ExtensionsWriter.Bcf30FileName);

            // Значение не теряется — оно остаётся в самом замечании
            Assert.Contains("<AssignedTo>Иванов (ОВ)</AssignedTo>", markup, StringComparison.Ordinal);
            // но в объявленный список идентификаторов не попадает
            Assert.DoesNotContain("Иванов", extensions, StringComparison.Ordinal);
            Assert.Contains(report.Warnings, w => w.Contains("не похожие на email"));
        }

        [Fact]
        public void ManyTopics_AreWrittenAsStream()
        {
            // Пятьсот замечаний — как в эталонном наборе для серверной команды.
            // Проверяем, что каждое доехало и архив не собирается в памяти целиком.
            var topics = Enumerable.Range(1, 500).Select(TestData.Topic).ToArray();

            BcfWriteReport report;
            byte[] archive = TestData.WriteArchive(TestData.Options(BcfVersion.Bcf30), out report, topics);

            Assert.Equal(500, report.TopicsWritten);
            Assert.Equal(500, report.ViewpointsWritten);
            Assert.Equal(500, report.SnapshotsWritten);

            // bcf.version, project.bcfp, extensions.xml + по три записи на топик
            Assert.Equal(3 + 500 * 3, TestData.EntryNames(archive).Count);
        }

        [Fact]
        public void SnapshotsCanBeTurnedOff()
        {
            BcfWriteOptions options = TestData.Options(BcfVersion.Bcf30);
            options.IncludeSnapshots = false;

            BcfWriteReport report;
            byte[] archive = TestData.WriteArchive(options, out report, TestData.Topic());

            Assert.Equal(0, report.SnapshotsWritten);
            Assert.DoesNotContain(TestData.EntryNames(archive), n => n.EndsWith(".png", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void Bcf20_IsReadOnly_AndTheWriterSaysSo()
        {
            // Old archives are read as a courtesy; writing 2.0 would mean
            // a third serializer for a version nobody asks to receive
            var buffer = new MemoryStream();

            NotSupportedException error = Assert.Throws<NotSupportedException>(
                () => BcfArchiveWriter.Create(buffer, new BcfWriteOptions { Version = BcfVersion.Bcf20 }));

            Assert.Contains("2.0", error.Message);
        }
    }
}
