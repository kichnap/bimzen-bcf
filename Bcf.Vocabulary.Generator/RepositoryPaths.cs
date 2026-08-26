using System;
using System.IO;

namespace Bcf.Vocabulary.Generator
{
    /// <summary>
    /// Finding the root of the bimzen-bcf repository, walking up from the
    /// current directory. Both the generator and the tests need it: both work
    /// with files of the repository yet run from different bin subdirectories.
    ///
    /// Поиск корня репозитория bimzen-bcf вверх от текущего каталога. Нужен
    /// и генератору, и тестам: оба работают с файлами репозитория, а запускаются
    /// из разных подкаталогов bin.
    /// </summary>
    public static class RepositoryPaths
    {
        /// <summary>
        /// The path to the vocabulary, relative to the repository root.
        /// Путь к справочнику относительно корня репозитория.
        /// </summary>
        public const string VocabularyRelativePath = "bcf-vocabularies/bcf-extensions.json";

        /// <summary>
        /// The path to the generated constants file, relative to the root.
        /// Путь к генерируемому файлу констант относительно корня.
        /// </summary>
        public const string OutputRelativePath = "Bcf.Core/Vocabulary/BcfVocabulary.g.cs";

        /// <summary>
        /// Climbs up from <paramref name="startDirectory"/> until it finds the
        /// vocabulary — that file is what marks the repository root.
        ///
        /// Поднимается вверх от <paramref name="startDirectory"/>, пока не найдёт
        /// справочник — он и помечает корень репозитория.
        /// </summary>
        /// <param name="startDirectory">Where the climb begins.</param>
        public static string FindRoot(string startDirectory)
        {
            var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());

            while (directory != null)
            {
                string marker = Path.Combine(directory.FullName,
                    VocabularyRelativePath.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(marker)) return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "The bimzen-bcf repository root was not found: the search went up from '" + startDirectory +
                "' and never met " + VocabularyRelativePath + ".");
        }

        public static string VocabularyFile(string repositoryRoot)
        {
            return Path.Combine(repositoryRoot,
                VocabularyRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string GeneratedFile(string repositoryRoot)
        {
            return Path.Combine(repositoryRoot,
                OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Comparing sources without regard to line endings: from git a file
        /// may arrive with CRLF or with LF, and the difference means nothing
        /// here.
        ///
        /// Сравнение исходников без оглядки на переводы строк: из git файл может
        /// приехать и с CRLF, и с LF, а различие тут ничего не значит.
        /// </summary>
        /// <param name="text">The text to normalise.</param>
        public static string NormalizeNewLines(string text)
        {
            return text == null ? null : text.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
