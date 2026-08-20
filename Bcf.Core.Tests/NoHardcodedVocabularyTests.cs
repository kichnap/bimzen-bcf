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
    /// Критерий приёмки: строк из справочника в коде быть не должно — ни
    /// "In Progress", ни "Clash". Иначе правка справочника молча разъедется
    /// с поведением в одном забытом месте.
    ///
    /// Тест проходит по исходникам Bcf.Core и, если библиотека подключена
    /// сабмодулем в плагин, по исходникам плагина тоже.
    /// </summary>
    public class NoHardcodedVocabularyTests
    {
        /// <summary>
        /// Значения, которые одновременно являются статусами Clash Detective.
        /// В коде плагина они законны: это имена ClashResultStatus, к справочнику
        /// BCF отношения не имеющие.
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
                string[] lines = File.ReadAllLines(file);

                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    string trimmed = line.TrimStart();

                    // Комментарии и XML-документация — текст для человека
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

            // Сабмодуль лежит внутри репозитория-потребителя: проверяем и его код
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

            return Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories)
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
