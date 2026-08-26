using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Bcf.Core.Resources;
using Bcf.Core.Vocabulary;
using Bcf.Vocabulary.Generator;
using Xunit;

namespace Bcf.Core.Tests
{
    public class ExtensionsWriterTests
    {
        private static readonly string[] Users = { "coordinator@example.com", "hvac@example.com" };

        private static string RepositoryRoot => RepositoryPaths.FindRoot(AppContext.BaseDirectory);

        [Fact]
        public void Xml30_IsValidAgainstOfficialSchema()
        {
            string xml = ExtensionsWriter.ToXml30(Users);

            // extensions.xsd pulls in shared-types.xsd, and by default .NET does not
            // resolve external schema references — without a resolver the
            // NonEmptyOrBlankString type ends up undeclared.
            var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            string schemaDirectory = Path.Combine(RepositoryRoot, "schemas", "3.0");
            schemas.Add(null, Path.Combine(schemaDirectory, "extensions.xsd"));

            var errors = new List<string>();
            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas
            };
            settings.ValidationEventHandler += (s, e) => errors.Add(e.Message);

            using (var reader = XmlReader.Create(new StringReader(xml), settings))
            {
                while (reader.Read()) { }
            }

            Assert.Empty(errors);
        }

        [Fact]
        public void Xml30_HasSameValueSetAsReferenceFile()
        {
            // The acceptance criterion: the set of values in the exported archive matches
            // the reference bcf-vocabularies/extensions.xml. The order and the layout may
            // differ — in the reference Stages comes before Users while the schema demands
            // the opposite, so it is the sets that are compared.
            var generated = XDocument.Parse(ExtensionsWriter.ToXml30(Users));
            var reference = XDocument.Load(Path.Combine(RepositoryRoot, "bcf-vocabularies", "extensions.xml"));

            foreach (string container in new[] { "TopicTypes", "TopicStatuses", "Priorities", "TopicLabels", "Stages" })
            {
                Assert.Equal(Values(reference, container), Values(generated, container));
            }
        }

        [Fact]
        public void Xml30_DeclaresUtf8_AndHasNoBom()
        {
            using (var buffer = new MemoryStream())
            {
                ExtensionsWriter.Write30(buffer, Users);
                byte[] bytes = buffer.ToArray();

                // A BOM (EF BB BF) breaks parsers outside .NET more often than .NET itself
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

                string text = new UTF8Encoding(false).GetString(bytes);
                Assert.Contains("encoding=\"utf-8\"", text, StringComparison.OrdinalIgnoreCase);
            }
        }

        [Fact]
        public void Xml30_ContainsOnlyPassedUsers()
        {
            var document = XDocument.Parse(ExtensionsWriter.ToXml30(Users));

            Assert.Equal(Users, Values(document, "Users"));
        }

        [Fact]
        public void Xsd21_IsCompilableSchema_WithMarkupAlongside()
        {
            // extensions.xsd in 2.1 redefines the types of markup.xsd, so markup.xsd has
            // to lie beside it — in the test as well as in the archive.
            string directory = Path.Combine(Path.GetTempPath(), "bcf-ext-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                File.Copy(
                    Path.Combine(RepositoryRoot, "schemas", "2.1", ExtensionsWriter.Bcf21RedefinedSchema),
                    Path.Combine(directory, ExtensionsWriter.Bcf21RedefinedSchema));

                string extensionsPath = Path.Combine(directory, ExtensionsWriter.Bcf21FileName);
                using (var file = File.Create(extensionsPath))
                {
                    ExtensionsWriter.Write21(file, Users);
                }

                var errors = new List<string>();
                var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
                schemas.ValidationEventHandler += (s, e) => errors.Add(e.Message);
                schemas.Add(null, extensionsPath);
                schemas.Compile();

                Assert.True(errors.Count == 0, string.Join(" | ", errors));
            }
            finally
            {
                Directory.Delete(directory, true);
            }
        }

        [Fact]
        public void Xsd21_EnumeratesEveryVocabularyValue()
        {
            XDocument document = XDocument.Parse(ExtensionsWriter.ToXsd21(Users));
            XNamespace xs = "http://www.w3.org/2001/XMLSchema";

            Assert.Equal(BcfVocabulary.TopicStatuses.All, Enumerations(document, xs, "TopicStatus"));
            Assert.Equal(BcfVocabulary.TopicTypes.All, Enumerations(document, xs, "TopicType"));
            Assert.Equal(BcfVocabulary.Priorities.All, Enumerations(document, xs, "Priority"));
            Assert.Equal(BcfVocabulary.TopicLabels.All, Enumerations(document, xs, "TopicLabel"));
            Assert.Equal(BcfVocabulary.Stages.All, Enumerations(document, xs, "Stage"));
            Assert.Equal(Users, Enumerations(document, xs, "UserIdType"));
        }

        [Fact]
        public void Xsd21_DoesNotRedefineSnippetType()
        {
            // markup.xsd 2.1 has no simple type SnippetType — a redefine of it makes the
            // schema invalid, even though it does turn up in some foreign files.
            Assert.DoesNotContain("SnippetType", ExtensionsWriter.ToXsd21(Users), StringComparison.Ordinal);
        }

        [Fact]
        public void BothVersions_ContainNoCyrillic()
        {
            // The Russian labels live in the interface only: in a file at a third-party
            // receiving tool the string is shown to the user literally, and the encodings
            // of foreign parsers are a source of pain all of their own.
            AssertAscii(ExtensionsWriter.ToXml30(Users));
            AssertAscii(ExtensionsWriter.ToXsd21(Users));
        }

        private static void AssertAscii(string text)
        {
            foreach (char c in text)
            {
                Assert.True(c < 128, "Не-ASCII символ в выходном файле: " + c);
            }
        }

        private static string[] Values(XDocument document, string container)
        {
            XElement element = document.Root.Element(container);
            return element == null
                ? new string[0]
                : element.Elements().Select(e => e.Value).ToArray();
        }

        private static string[] Enumerations(XDocument document, XNamespace xs, string typeName)
        {
            return document.Descendants(xs + "simpleType")
                .Where(t => (string)t.Attribute("name") == typeName)
                .Descendants(xs + "enumeration")
                .Select(e => (string)e.Attribute("value"))
                .ToArray();
        }
    }
}
