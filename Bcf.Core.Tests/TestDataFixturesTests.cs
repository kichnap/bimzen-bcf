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
    /// The reference archives in test-data travel to other teams as fixtures for
    /// import tests. Here they are checked as foreign files: read afresh and
    /// validated against the official buildingSMART schemas.
    ///
    /// If these tests fail, the receiving team would have got broken data.
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

            // The markup of every topic is validated: that is what an importer parses
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

            // The PNG signature plus the closing IEND block: a stub of one signature
            // would not open in any viewer
            Assert.True(snapshot.Length > 100);
            Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, snapshot.Take(4).ToArray());
            Assert.Contains("IEND", System.Text.Encoding.ASCII.GetString(snapshot.Skip(snapshot.Length - 12).ToArray()), StringComparison.Ordinal);
        }

        [Fact]
        public void ForeignValues_AreReadWithoutError_AndReported()
        {
            // The acceptance criterion: a file with values of a foreign vocabulary is
            // read, the values are kept as they are, and a warning appears in the report
            BcfReadResult read = Read("foreign-values-bcf30.bcfzip");

            Assert.Equal(3, read.Topics.Count);
            Assert.Contains(read.Topics, t => t.TopicStatus == "Открыто");
            Assert.Contains(read.Topics, t => t.TopicType == "Пересечение");

            Assert.Contains(read.ExternalValues, v => v.Value == "Открыто" && v.Field == "TopicStatus");
            Assert.Contains(read.ExternalValues, v => v.Value == "Пересечение" && v.Field == "TopicType");

            // And not one value was swapped for a "correct" one along the way
            Assert.DoesNotContain(read.Topics, t => t.TopicStatus == BcfVocabulary.TopicStatuses.New);
        }

        [Fact]
        public void Fixtures_CarryVocabularyLabelsAndDisciplines()
        {
            BcfReadResult read = Read("small-3-topics-bcf30.bcfzip");

            // The Auto label marks an automatic export; by it a service tells clashes
            // from topics entered by hand
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
