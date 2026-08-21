using System;
using System.IO;
using System.Text;

namespace Bcf.Vocabulary.Generator
{
    /// <summary>
    /// Генератор констант справочника.
    ///
    ///   dotnet run --project Bcf.Vocabulary.Generator            перезаписать BcfVocabulary.g.cs
    ///   dotnet run --project Bcf.Vocabulary.Generator -- --check проверить, что файл актуален
    ///
    /// Режим --check дублируется тестом VocabularyDriftTests: генератор удобен
    /// человеку, тест не даёт забыть его запустить.
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            try
            {
                bool checkOnly = Array.Exists(args, a =>
                    string.Equals(a, "--check", StringComparison.OrdinalIgnoreCase));

                string root = RepositoryPaths.FindRoot(AppContext.BaseDirectory);
                string vocabularyFile = RepositoryPaths.VocabularyFile(root);
                string generatedFile = RepositoryPaths.GeneratedFile(root);

                string generated = VocabularyCodeGenerator.Generate(File.ReadAllText(vocabularyFile));

                if (checkOnly)
                {
                    if (!File.Exists(generatedFile))
                    {
                        Console.Error.WriteLine("Нет файла " + RepositoryPaths.OutputRelativePath +
                                                ". Запустите генератор без --check.");
                        return 1;
                    }

                    string existing = File.ReadAllText(generatedFile);
                    if (RepositoryPaths.NormalizeNewLines(existing) != RepositoryPaths.NormalizeNewLines(generated))
                    {
                        Console.Error.WriteLine(RepositoryPaths.OutputRelativePath +
                                                " разошёлся со справочником. Запустите генератор без --check и закоммитьте результат.");
                        return 1;
                    }

                    Console.WriteLine("Справочник и константы совпадают.");
                    return 0;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(generatedFile));
                // UTF-8 без BOM: файл читают и .NET, и утилиты сравнения в CI
                File.WriteAllText(generatedFile, generated, new UTF8Encoding(false));
                Console.WriteLine("Записано: " + generatedFile);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
