using System;
using System.Linq;
using Bcf.Core.Resources;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// Checks that the resources really made it into the assembly. It breaks if
    /// somebody renames the schema folder or switches EmbeddedResource off in the
    /// .csproj — that is, at build time and not at run time on a user's machine.
    /// </summary>
    public class EmbeddedResourcesTests
    {
        [Fact]
        public void Vocabulary_IsEmbedded_AndParses()
        {
            string json = EmbeddedResources.ReadVocabularyJson();

            var root = JObject.Parse(json);

            Assert.Equal("3.0", (string)root["bcfTargetVersion"]);
            Assert.Equal("2.1", (string)root["bcfFallbackVersion"]);
            Assert.NotEmpty((JArray)root["topicStatuses"]);
        }

        [Theory]
        [InlineData("markup.xsd")]
        [InlineData("visinfo.xsd")]
        [InlineData("extensions.xsd")]
        [InlineData("project.xsd")]
        [InlineData("version.xsd")]
        [InlineData("shared-types.xsd")]
        [InlineData("documents.xsd")]
        public void Bcf30Schema_IsEmbedded(string fileName)
        {
            Assert.Contains(EmbeddedResources.Bcf30SchemaPrefix + fileName, EmbeddedResources.GetNames());
        }

        [Theory]
        [InlineData("markup.xsd")]
        [InlineData("visinfo.xsd")]
        [InlineData("project.xsd")]
        [InlineData("version.xsd")]
        public void Bcf21Schema_IsEmbedded(string fileName)
        {
            Assert.Contains(EmbeddedResources.Bcf21SchemaPrefix + fileName, EmbeddedResources.GetNames());
        }

        [Fact]
        public void Bcf21_HasNoExtensionsSchema()
        {
            // In 2.1 the vocabularies are declared by an extensions.xsd file INSIDE every
            // archive, and we generate it ourselves. The reference for comparison lies in
            // schemas/2.1/extensions.reference.xsd and must not be a resource.
            Assert.DoesNotContain(
                EmbeddedResources.GetNames(),
                name => name.StartsWith(EmbeddedResources.Bcf21SchemaPrefix, StringComparison.Ordinal)
                        && name.EndsWith("extensions.xsd", StringComparison.Ordinal));
        }
    }
}
