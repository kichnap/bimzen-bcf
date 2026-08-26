using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Bcf.Core.Clash;
using Bcf.Vocabulary.Generator;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// The settings schema is an external contract: another project builds a job
    /// file from it without reading our code. A contract that has drifted from the
    /// code is worse than a missing one: it looks trustworthy.
    ///
    /// So the schema is checked against the settings class on every build.
    /// </summary>
    public class SettingsSchemaDriftTests
    {
        private const string SchemaRelativePath = "schemas/api/bcf-export-settings.schema.json";

        [Fact]
        public void Schema_DescribesEverySetting()
        {
            JObject properties = SchemaProperties();

            IEnumerable<string> missing = SettingNames().Where(name => properties[name] == null);

            Assert.Empty(missing);
        }

        [Fact]
        public void Schema_DescribesNothingExtra()
        {
            var names = new HashSet<string>(SettingNames(), StringComparer.Ordinal);

            IEnumerable<string> extra = SchemaProperties()
                .Properties()
                .Select(p => p.Name)
                .Where(name => !names.Contains(name));

            Assert.Empty(extra);
        }

        [Theory]

        [InlineData("Grouping", typeof(ClashGroupingMode))]
        [InlineData("SnapshotMode", typeof(SnapshotMode))]
        [InlineData("SnapshotIsolation", typeof(SnapshotIsolation))]
        [InlineData("UpdateMode", typeof(BcfUpdateMode))]
        public void Schema_ListsEveryEnumValue(string property, Type enumType)
        {
            JToken values = SchemaProperties()[property]["enum"];

            Assert.NotNull(values);

            var declared = new HashSet<string>(values.Select(v => (string)v), StringComparer.Ordinal);

            foreach (string name in Enum.GetNames(enumType))
            {
                Assert.Contains(name, declared);
            }

            Assert.Equal(Enum.GetNames(enumType).Length, declared.Count);
        }

        [Fact]
        public void Schema_ListsOnlyTheVersionsThatCanBeWritten()
        {
            // BCF 2.0 is in the enum because archives in it are read, but it
            // must not appear among the export settings: offering a version
            // the writer refuses would be a lie told by the contract itself
            var declared = new HashSet<string>(
                SchemaProperties()["Version"]["enum"].Select(v => (string)v), StringComparer.Ordinal);

            Assert.Equal(new[] { "Bcf21", "Bcf30" }, declared.OrderBy(v => v, StringComparer.Ordinal));
        }

        [Fact]
        public void Schema_KeepsVocabularyOutOfItself()
        {
            // The single source of vocabulary values is bcf-extensions.json. Duplicating
            // them in the schema would set up a second one, which would start drifting
            // from the first quietly
            string text = File.ReadAllText(SchemaFile());

            Assert.DoesNotContain("\"Hard clash\"", text);
            Assert.DoesNotContain("\"In Progress\"", text);
        }

        private static IEnumerable<string> SettingNames()
        {
            return typeof(BcfExportSettings)
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead)
                .Select(p => p.Name);
        }

        private static JObject SchemaProperties()
        {
            var schema = JObject.Parse(File.ReadAllText(SchemaFile()));

            return (JObject)schema["properties"];
        }

        private static string SchemaFile()
        {
            string root = RepositoryPaths.FindRoot(AppContext.BaseDirectory);

            return Path.Combine(root, SchemaRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
