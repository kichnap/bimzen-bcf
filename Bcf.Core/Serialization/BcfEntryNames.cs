using System;
using System.Globalization;
using System.Text;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// The names of the entries inside an archive.
    ///
    /// The rules are dictated by the receiving side: importers outside .NET
    /// stumble over non-ASCII names, backslashes and a leading "./" far more
    /// often. A topic folder is named exactly after its identifier, in lower
    /// case.
    ///
    /// Имена записей внутри архива.
    ///
    /// Правила продиктованы принимающей стороной: импортёры вне .NET
    /// спотыкаются на не-ASCII именах, обратных слэшах и ведущем «./» куда
    /// чаще. Папка замечания называется ровно его идентификатором в нижнем
    /// регистре.
    /// </summary>
    public static class BcfEntryNames
    {
        /// <summary>
        /// The name of the version file at the archive root.
        /// Имя файла версии в корне архива.
        /// </summary>
        public const string Version = "bcf.version";
        /// <summary>
        /// The name of the project file at the archive root.
        /// Имя файла проекта в корне архива.
        /// </summary>
        public const string Project = "project.bcfp";
        /// <summary>
        /// The name of the markup file inside a topic folder.
        /// Имя файла разметки внутри папки замечания.
        /// </summary>
        public const string Markup = "markup.bcf";

        /// <summary>
        /// The extension of a viewpoint file.
        /// Расширение файла точки зрения.
        /// </summary>
        public const string ViewpointExtension = ".bcfv";

        /// <summary>
        /// The folder of a topic.
        /// Папка замечания.
        /// </summary>
        /// <param name="topicGuid">The topic identifier.</param>
        public static string TopicFolder(Guid topicGuid)
        {
            return topicGuid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant();
        }

        /// <summary>
        /// The path to the markup.bcf of a topic.
        /// Путь к markup.bcf замечания.
        /// </summary>
        /// <param name="topicGuid">The topic identifier.</param>
        public static string MarkupEntry(Guid topicGuid)
        {
            return TopicFolder(topicGuid) + "/" + Markup;
        }

        /// <summary>
        /// The path to a viewpoint file.
        /// Путь к файлу точки зрения.
        /// </summary>
        /// <param name="topicGuid">The topic identifier.</param>
        /// <param name="viewpointGuid">The viewpoint identifier.</param>
        public static string ViewpointEntry(Guid topicGuid, Guid viewpointGuid)
        {
            return TopicFolder(topicGuid) + "/" + ViewpointFileName(viewpointGuid);
        }

        /// <summary>
        /// The name of a viewpoint file inside the topic folder.
        /// Имя файла точки зрения внутри папки замечания.
        /// </summary>
        /// <param name="viewpointGuid">The viewpoint identifier.</param>
        public static string ViewpointFileName(Guid viewpointGuid)
        {
            return viewpointGuid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant() + ViewpointExtension;
        }

        /// <summary>
        /// The path to the snapshot of a viewpoint.
        /// Путь к снимку точки зрения.
        /// </summary>
        /// <param name="topicGuid">The topic identifier.</param>
        /// <param name="snapshotFileName">The file name of the snapshot.</param>
        public static string SnapshotEntry(Guid topicGuid, string snapshotFileName)
        {
            return TopicFolder(topicGuid) + "/" + Sanitize(snapshotFileName);
        }

        /// <summary>
        /// Reduces a file name to a safe shape: ASCII letters, digits, a dot,
        /// a hyphen and an underscore. Non-ASCII text in a snapshot name would
        /// have arrived from the name of a clash test and would break the
        /// reading of the archive on a service.
        ///
        /// Приводит имя файла к безопасному виду: только ASCII-буквы, цифры,
        /// точка, дефис и подчёркивание. Не-ASCII текст в имени снимка приехал
        /// бы из имени проверки и сломал бы разбор архива на сервисе.
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
        /// Checks an entry name before it is added to the archive.
        /// Проверяет имя записи перед добавлением в архив.
        /// </summary>
        public static void Validate(string entryName)
        {
            if (string.IsNullOrWhiteSpace(entryName))
            {
                throw new ArgumentException("The archive entry name is empty.", nameof(entryName));
            }

            if (entryName.IndexOf('\\') >= 0)
            {
                throw new ArgumentException(
                    "Entry names are separated by a forward slash: '" + entryName + "'.", nameof(entryName));
            }

            if (entryName.StartsWith("./", StringComparison.Ordinal) || entryName.StartsWith("/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "An entry name must not start with './' or '/': '" + entryName + "'.", nameof(entryName));
            }

            if (entryName.IndexOf("..", StringComparison.Ordinal) >= 0)
            {
                throw new ArgumentException(
                    "An entry name must not contain '..': '" + entryName + "'.", nameof(entryName));
            }

            foreach (char c in entryName)
            {
                if (c > 127)
                {
                    throw new ArgumentException(
                        "An entry name must be ASCII: '" + entryName + "'.", nameof(entryName));
                }
            }
        }
    }
}
