using System;
using System.Globalization;
using System.Text;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Имена записей в архиве.
    ///
    /// Правила продиктованы приёмником: импортёр на Node спотыкается на
    /// не-ASCII именах, обратных слэшах и ведущем «./» гораздо чаще, чем .NET.
    /// Папка топика называется ровно его GUID в нижнем регистре.
    /// </summary>
    public static class BcfEntryNames
    {
        public const string Version = "bcf.version";
        public const string Project = "project.bcfp";
        public const string Markup = "markup.bcf";

        public const string ViewpointExtension = ".bcfv";

        /// <summary>Папка топика.</summary>
        public static string TopicFolder(Guid topicGuid)
        {
            return topicGuid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        /// <summary>Путь к markup.bcf топика.</summary>
        public static string MarkupEntry(Guid topicGuid)
        {
            return TopicFolder(topicGuid) + "/" + Markup;
        }

        /// <summary>Путь к файлу точки зрения.</summary>
        public static string ViewpointEntry(Guid topicGuid, Guid viewpointGuid)
        {
            return TopicFolder(topicGuid) + "/" + ViewpointFileName(viewpointGuid);
        }

        /// <summary>Имя файла точки зрения внутри папки топика.</summary>
        public static string ViewpointFileName(Guid viewpointGuid)
        {
            return viewpointGuid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant() + ViewpointExtension;
        }

        /// <summary>Путь к снимку точки зрения.</summary>
        public static string SnapshotEntry(Guid topicGuid, string snapshotFileName)
        {
            return TopicFolder(topicGuid) + "/" + Sanitize(snapshotFileName);
        }

        /// <summary>
        /// Приводит имя файла к безопасному виду: только ASCII-буквы, цифры,
        /// точка, дефис и подчёркивание. Кириллица в имени снимка приехала бы
        /// из имени теста и сломала бы разбор архива на стороне сервиса.
        /// </summary>
        public static string Sanitize(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return "snapshot.png";

            var sb = new StringBuilder(fileName.Length);

            foreach (char c in fileName)
            {
                bool allowed = (c >= 'a' && c <= 'z')
                               || (c >= 'A' && c <= 'Z')
                               || (c >= '0' && c <= '9')
                               || c == '.' || c == '-' || c == '_';

                sb.Append(allowed ? c : '_');
            }

            string result = sb.ToString().TrimStart('.');

            return result.Length == 0 ? "snapshot.png" : result;
        }

        /// <summary>
        /// Проверка имени записи перед добавлением в архив.
        /// </summary>
        public static void Validate(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("Пустое имя записи архива.", nameof(entryName));
            }

            if (entryName.IndexOf('\\') >= 0)
            {
                throw new ArgumentException(
                    "Разделитель в именах записей — прямой слэш: '" + entryName + "'.", nameof(entryName));
            }

            if (entryName.StartsWith("./", StringComparison.Ordinal) || entryName.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Имя записи не должно начинаться с './' или '/': '" + entryName + "'.", nameof(entryName));
            }

            if (entryName.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException(
                    "Имя записи не должно содержать '..': '" + entryName + "'.", nameof(entryName));
            }

            foreach (char c in entryName)
            {
                if (c > 127)
                {
                    throw new ArgumentException(
                        "Имя записи должно быть в ASCII: '" + entryName + "'.", nameof(entryName));
                }
            }
        }
    }
}
