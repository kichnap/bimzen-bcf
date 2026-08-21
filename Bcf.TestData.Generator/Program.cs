using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Bcf.Core;
using Bcf.Core.Clash;
using Bcf.Core.Vocabulary;
using Bcf.Vocabulary.Generator;

namespace Bcf.TestData.Generator
{
    /// <summary>
    /// Генератор эталонных архивов для папки test-data.
    ///
    ///   dotnet run --project Bcf.TestData.Generator
    ///
    /// Файлы уходят команде онлайн-сервиса как фикстуры для тестов импорта,
    /// поэтому важны две вещи: они собираются настоящим экспортёром (а не
    /// написаны руками) и воспроизводимы побайтово — время выгрузки и метки
    /// времени записей архива зафиксированы.
    /// </summary>
    public static class Program
    {
        /// <summary>Фиксированный момент выгрузки — иначе каждый прогон менял бы все файлы.</summary>
        private static readonly DateTimeOffset Moment =
            new DateTimeOffset(2026, 8, 20, 12, 0, 0, TimeSpan.FromHours(3));

        public static int Main()
        {
            try
            {
                string root = RepositoryPaths.FindRoot(AppContext.BaseDirectory);
                string directory = Path.Combine(root, "test-data");
                Directory.CreateDirectory(directory);

                byte[] snapshot = PngWriter.Create(320, 240, 0x2F, 0x6F, 0xB2);

                // Маленький набор — в режиме «группа = замечание», как выгружают
                // обычно. Большой — «каждая коллизия отдельно»: только так
                // получается ровно 500 замечаний, и заодно это худший случай
                // по размеру, ради которого фикстура и нужна
                Write(directory, "small-3-topics-bcf30.bcfzip", 3, BcfVersion.Bcf30, snapshot,
                    ClashGroupingMode.GroupPerTopic, maxSnapshots: 0);
                Write(directory, "small-3-topics-bcf21.bcfzip", 3, BcfVersion.Bcf21, snapshot,
                    ClashGroupingMode.GroupPerTopic, maxSnapshots: 0);

                // Снимки только у первых пятидесяти: пятьсот картинок раздули бы
                // файл в репозитории до мегабайтов, а импортёру важно увидеть
                // и замечания со снимком, и замечания без него
                Write(directory, "large-500-topics-bcf30.bcfzip", 500, BcfVersion.Bcf30, snapshot,
                    ClashGroupingMode.ClashPerTopic, maxSnapshots: 50);
                Write(directory, "large-500-topics-bcf21.bcfzip", 500, BcfVersion.Bcf21, snapshot,
                    ClashGroupingMode.ClashPerTopic, maxSnapshots: 50);

                WriteForeignValues(directory);

                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void Write(
            string directory,
            string fileName,
            int topics,
            BcfVersion version,
            byte[] snapshot,
            ClashGroupingMode grouping,
            int maxSnapshots)
        {
            string path = Path.Combine(directory, fileName);

            var settings = new BcfExportSettings
            {
                Version = version,
                Author = "coordinator@example.com",
                ProjectName = "ЖК Северный",
                Grouping = grouping,
                IncludedClashStatuses = new[] { "New", "Active" },
                MaxSnapshots = maxSnapshots,
                ExportTime = Moment
            };

            settings.DisciplineLabelRules.Add(new DisciplineLabelRule("ОВ", BcfVocabulary.TopicLabels.HVAC));
            settings.DisciplineLabelRules.Add(new DisciplineLabelRule("ВК", BcfVocabulary.TopicLabels.PLUMB));
            settings.DisciplineLabelRules.Add(new DisciplineLabelRule("ЭОМ", BcfVocabulary.TopicLabels.ELEC));

            var source = new SyntheticClashSource(topics, Moment, snapshot);

            BcfExportResult result;

            using (var buffer = new MemoryStream())
            {
                result = new BcfClashExporter(source).Export(buffer, settings);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException("Не удалось собрать " + fileName + ": " + result.Error);
                }

                File.WriteAllBytes(path, buffer.ToArray());
            }

            Console.WriteLine(
                fileName + ": замечаний " + result.TopicsCreated +
                ", снимков " + result.SnapshotsCaptured +
                ", размер " + new FileInfo(path).Length / 1024 + " КБ");
        }

        /// <summary>
        /// Архив со значениями вне справочника — как его прислал бы BIMcollab
        /// или Revizto со своими словарями. Такой файл законен: стандарт словари
        /// не фиксирует. Наш экспортёр его создать не может (на выход валидация
        /// строгая), поэтому он собирается подменой значений в готовом архиве.
        /// </summary>
        private static void WriteForeignValues(string directory)
        {
            string source = Path.Combine(directory, "small-3-topics-bcf30.bcfzip");
            string target = Path.Combine(directory, "foreign-values-bcf30.bcfzip");

            byte[] original = File.ReadAllBytes(source);

            using (var output = new MemoryStream())
            {
                using (var reading = new MemoryStream(original))
                using (var input = new ZipArchive(reading, ZipArchiveMode.Read))
                using (var writing = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (ZipArchiveEntry entry in input.Entries)
                    {
                        ZipArchiveEntry copy = writing.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                        copy.LastWriteTime = Moment;

                        using (Stream from = entry.Open())
                        using (Stream to = copy.Open())
                        {
                            if (!entry.FullName.EndsWith("markup.bcf", StringComparison.Ordinal))
                            {
                                from.CopyTo(to);
                                continue;
                            }

                            string markup = new StreamReader(from, new UTF8Encoding(false)).ReadToEnd();

                            markup = markup
                                .Replace("TopicStatus=\"" + BcfVocabulary.TopicStatuses.New + "\"", "TopicStatus=\"Открыто\"")
                                .Replace("TopicStatus=\"" + BcfVocabulary.TopicStatuses.Assigned + "\"", "TopicStatus=\"Открыто\"")
                                .Replace("TopicType=\"" + BcfVocabulary.TopicTypes.Default + "\"", "TopicType=\"Пересечение\"");

                            byte[] bytes = new UTF8Encoding(false).GetBytes(markup);
                            to.Write(bytes, 0, bytes.Length);
                        }
                    }
                }

                File.WriteAllBytes(target, output.ToArray());
            }

            Console.WriteLine("foreign-values-bcf30.bcfzip: статусы и типы подменены на значения вне справочника");
        }
    }
}
