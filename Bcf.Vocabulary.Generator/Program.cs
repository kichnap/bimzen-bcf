using System;
using System.IO;
using System.Text;

namespace Bcf.Vocabulary.Generator
{
    /// <summary>
    /// The generator of the vocabulary constants.
    ///
    ///   dotnet run --project Bcf.Vocabulary.Generator            rewrite BcfVocabulary.g.cs
    ///   dotnet run --project Bcf.Vocabulary.Generator -- --check check that the file is current
    ///
    /// The --check mode is doubled by the VocabularyDriftTests test: the
    /// generator is convenient for a person, the test makes sure nobody forgets
    /// to run it.
    ///
    /// Генератор констант справочника.
    ///
    ///   dotnet run --project Bcf.Vocabulary.Generator            перезаписать BcfVocabulary.g.cs
    ///   dotnet run --project Bcf.Vocabulary.Generator -- --check проверить, что файл актуален
    ///
    /// Режим --check продублирован тестом VocabularyDriftTests: генератор удобен
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
                        Console.Error.WriteLine("There is no " + RepositoryPaths.OutputRelativePath +
                                                " file. Run the generator without --check.");
                        return 1;
                    }

                    string existing = File.ReadAllText(generatedFile);
                    if (RepositoryPaths.NormalizeNewLines(existing) != RepositoryPaths.NormalizeNewLines(generated))
                    {
                        Console.Error.WriteLine(RepositoryPaths.OutputRelativePath +
                                                " has drifted from the vocabulary. Run the generator without --check and commit the result.");
                        return 1;
                    }

                    Console.WriteLine("The vocabulary and the constants agree.");
                    return 0;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(generatedFile));
                // UTF-8 without a BOM: the file is read by .NET and by the diff
                // tools in CI alike
                File.WriteAllText(generatedFile, generated, new UTF8Encoding(false));
                Console.WriteLine("Written: " + generatedFile);
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
