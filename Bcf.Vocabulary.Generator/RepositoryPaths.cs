using System;
using System.IO;

namespace Bcf.Vocabulary.Generator
{
    /// <summary>
    /// Поиск корня репозитория bimzen-bcf от текущего каталога вверх.
    /// Нужен и генератору, и тестам: оба работают с файлами репозитория,
    /// а запускаются из разных подкаталогов bin.
    /// </summary>
    public static class RepositoryPaths
    {
        /// <summary>
        /// Поднимается от <paramref name="startDirectory"/> вверх, пока не найдёт
        /// справочник — он и помечает корень репозитория.
        /// </summary>
        public static string FindRoot(string startDirectory)
        {
            var directory = new DirectoryInfo(startDirectory ?? Directory.GetCurrentDirectory());

            while (directory != null)
            {
                string marker = Path.Combine(directory.FullName,
                    VocabularyCodeGenerator.VocabularyRelativePath.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(marker)) return directory.FullName;

                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Не найден корень репозитория bimzen-bcf: поиск шёл вверх от '" + startDirectory +
                "' и не встретил " + VocabularyCodeGenerator.VocabularyRelativePath + ".");
        }

        public static string VocabularyFile(string repositoryRoot)
        {
            return Path.Combine(repositoryRoot,
                VocabularyCodeGenerator.VocabularyRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static string GeneratedFile(string repositoryRoot)
        {
            return Path.Combine(repositoryRoot,
                VocabularyCodeGenerator.OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// Сравнение исходников без оглядки на переводы строк: в git файл может
        /// приехать и с CRLF, и с LF, а различие тут ничего не значит.
        /// </summary>
        public static string NormalizeNewLines(string text)
        {
            return text == null ? null : text.Replace("\r\n", "\n").Replace("\r", "\n");
        }
    }
}
