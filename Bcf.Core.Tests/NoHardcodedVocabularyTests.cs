using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bcf.Core.Vocabulary;
using Bcf.Vocabulary.Generator;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// The acceptance criterion: no vocabulary strings in the code — neither
    /// "In Progress" nor "Clash". Otherwise an edit to the vocabulary drifts away
    /// from the behaviour quietly, in one forgotten place.
    ///
    /// The test walks the sources of Bcf.Core and, when the library is wired into
    /// a host as a submodule, the sources of that host too.
    /// </summary>
    public class NoHardcodedVocabularyTests
    {
        /// <summary>
        /// The values that are Clash Detective statuses at the same time.
        /// In host code they are legitimate: those are ClashResultStatus names, and
        /// they have nothing to do with the BCF vocabulary.
        /// </summary>
        private static readonly HashSet<string> NavisworksStatusNames =
            new HashSet<string>(BcfVocabulary.NavisworksStatusToBcf.Keys, StringComparer.Ordinal);

        [Fact]
        public void SourceFiles_ContainNoVocabularyLiterals()
        {
            string[] values = BcfVocabulary.TopicTypes.All
                .Concat(BcfVocabulary.TopicStatuses.All)
                .Concat(BcfVocabulary.Priorities.All)
                .Concat(BcfVocabulary.TopicLabels.All)
                .Concat(BcfVocabulary.Stages.All)
                .Distinct(StringComparer.Ordinal)
                .Where(v => !NavisworksStatusNames.Contains(v))
                .ToArray();

            var offenders = new List<string>();

            foreach (string file in SourceFiles())
            {
                string[] lines;

                try
                {
                    lines = File.ReadAllLines(file);
                }
                catch (IOException)
                {
                    // The test walks a live working directory: a file may have gone or become
                    // locked while the build runs. That is not a finding but a race — such a
                    // file is skipped rather than failing the run
                    continue;
                }

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();

                    // Comments and XML documentation are text for a person
                    if (trimmed.StartsWith("//", StringComparison.Ordinal) ||
                        trimmed.StartsWith("*", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    foreach (string value in values)
                    {
                        if (line.IndexOf("\"" + value + "\"", StringComparison.Ordinal) < 0) continue;

                        offenders.Add(file + ":" + (i + 1) + " -> \"" + value + "\"");
                    }
                }
            }

            Assert.True(offenders.Count == 0,
                "Значения справочника должны браться из BcfVocabulary, а не писаться строкой:" +
                Environment.NewLine + string.Join(Environment.NewLine, offenders));
        }

        private static IEnumerable<string> SourceFiles()
        {
            string root = RepositoryPaths.FindRoot(AppContext.BaseDirectory);

            foreach (string file in Sources(Path.Combine(root, "Bcf.Core")))
            {
                yield return file;
            }

            // A submodule lies inside the consuming repository: its code is checked too
            string consumerRoot = Directory.GetParent(root)?.FullName;
            if (consumerRoot == null) yield break;

            string pluginDirectory = Path.Combine(consumerRoot, "BIMzen");
            if (!File.Exists(Path.Combine(pluginDirectory, "BIMzen.csproj"))) yield break;

            foreach (string file in Sources(pluginDirectory))
            {
                yield return file;
            }
        }

        private static IEnumerable<string> Sources(string directory)
        {
            if (!Directory.Exists(directory)) return Enumerable.Empty<string>();

            string[] files;

            try
            {
                files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            }
            catch (IOException)
            {
                return Enumerable.Empty<string>();
            }

            return files
                .Where(f => !f.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                .Where(f => !IsBuildOutput(f));
        }

        private static bool IsBuildOutput(string path)
        {
            string separator = Path.DirectorySeparatorChar.ToString();
            return path.IndexOf(separator + "obj" + separator, StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf(separator + "bin" + separator, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
