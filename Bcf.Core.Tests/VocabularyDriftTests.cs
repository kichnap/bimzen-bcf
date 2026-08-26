using System;
using System.IO;
using System.Linq;
using Bcf.Core.Resources;
using Bcf.Core.Vocabulary;
using Bcf.Vocabulary.Generator;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// The main defence against the vocabulary and the code drifting apart: the
    /// test generates the constants afresh from bcf-extensions.json and compares
    /// them with the committed file. An edit to the vocabulary without a
    /// regeneration fails the build instead of surfacing as an odd value later.
    /// </summary>
    public class VocabularyDriftTests
    {
        private static string RepositoryRoot => RepositoryPaths.FindRoot(AppContext.BaseDirectory);

        [Fact]
        public void GeneratedConstants_MatchVocabularyFile()
        {
            string json = File.ReadAllText(RepositoryPaths.VocabularyFile(RepositoryRoot));
            string expected = VocabularyCodeGenerator.Generate(json);
            string actual = File.ReadAllText(RepositoryPaths.GeneratedFile(RepositoryRoot));

            Assert.Equal(
                RepositoryPaths.NormalizeNewLines(expected),
                RepositoryPaths.NormalizeNewLines(actual));
        }

        [Fact]
        public void EmbeddedVocabulary_MatchesFileOnDisk()
        {
            // The resource in the assembly and the file in the repository are one and the
            // same vocabulary. They can differ only if somebody broke EmbeddedResource.
            string onDisk = File.ReadAllText(RepositoryPaths.VocabularyFile(RepositoryRoot));

            Assert.Equal(
                RepositoryPaths.NormalizeNewLines(onDisk),
                RepositoryPaths.NormalizeNewLines(EmbeddedResources.ReadVocabularyJson()));
        }

        [Theory]
        [InlineData("topicTypes")]
        [InlineData("topicStatuses")]
        [InlineData("priorities")]
        [InlineData("topicLabels")]
        [InlineData("stages")]
        public void EveryValueFromJson_HasConstant(string section)
        {
            var root = JObject.Parse(EmbeddedResources.ReadVocabularyJson());
            string[] fromJson = ((JArray)root[section]).Select(i => (string)i["value"]).ToArray();

            string[] fromCode = Section(section);

            Assert.Equal(fromJson, fromCode);
        }

        [Fact]
        public void NavisworksMapping_MatchesJson()
        {
            var root = JObject.Parse(EmbeddedResources.ReadVocabularyJson());
            var mapping = (JObject)root["navisworksStatusMapping"];

            foreach (var property in mapping.Properties().Where(p => !p.Name.StartsWith("$", StringComparison.Ordinal)))
            {
                Assert.Equal((string)property.Value, BcfVocabulary.NavisworksStatusToBcf[property.Name]);
            }
        }

        [Fact]
        public void ApprovedMapsToClosed_ByAgreedDefault()
        {
            // A contentious point settled by the client: Approved in Clash Detective means
            // "the fix has been verified and accepted". The alternative, Rejected ("the
            // intersection was accepted as tolerable"), is set in the export settings.
            Assert.Equal(BcfVocabulary.TopicStatuses.Closed, BcfVocabulary.NavisworksStatusToBcf["Approved"]);
        }

        private static string[] Section(string section)
        {
            switch (section)
            {
                case "topicTypes": return BcfVocabulary.TopicTypes.All.ToArray();
                case "topicStatuses": return BcfVocabulary.TopicStatuses.All.ToArray();
                case "priorities": return BcfVocabulary.Priorities.All.ToArray();
                case "topicLabels": return BcfVocabulary.TopicLabels.All.ToArray();
                case "stages": return BcfVocabulary.Stages.All.ToArray();
                default: throw new ArgumentOutOfRangeException(nameof(section));
            }
        }
    }
}
