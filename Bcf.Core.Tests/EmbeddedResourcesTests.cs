using System;
using System.Linq;
using Bcf.Core.Resources;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// Проверяет, что ресурсы действительно попали в сборку. Ломается, если
    /// кто-то переименует папку схем или отключит EmbeddedResource в .csproj —
    /// то есть на сборке, а не в рантайме у пользователя.
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
            // В 2.1 справочники объявляются файлом extensions.xsd ВНУТРИ каждого
            // архива, и генерируем его мы сами. Эталон для сверки лежит в
            // schemas/2.1/extensions.reference.xsd и ресурсом быть не должен.
            Assert.DoesNotContain(
                EmbeddedResources.GetNames(),
                name => name.StartsWith(EmbeddedResources.Bcf21SchemaPrefix, StringComparison.Ordinal)
                        && name.EndsWith("extensions.xsd", StringComparison.Ordinal));
        }
    }
}
