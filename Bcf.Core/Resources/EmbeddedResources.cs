using System;
using System.IO;
using System.Reflection;

namespace Bcf.Core.Resources
{
    /// <summary>
    /// Доступ к встроенным ресурсам библиотеки: справочнику значений BCF
    /// и XSD-схемам buildingSMART.
    /// </summary>
    public static class EmbeddedResources
    {
        /// <summary>Имя ресурса с каноническим справочником значений.</summary>
        public const string VocabularyResourceName = "Bcf.Core.Resources.bcf-extensions.json";

        /// <summary>Префикс имён ресурсов со схемами BCF 3.0.</summary>
        public const string Bcf30SchemaPrefix = "Bcf.Core.Schemas.Bcf30.";

        /// <summary>Префикс имён ресурсов со схемами BCF 2.1.</summary>
        public const string Bcf21SchemaPrefix = "Bcf.Core.Schemas.Bcf21.";

        /// <summary>
        /// Открывает встроенный ресурс по имени.
        /// </summary>
        /// <exception cref="InvalidOperationException">Ресурс не найден — сборка собрана неверно.</exception>
        public static Stream Open(string resourceName)
        {
            if (resourceName == null) throw new ArgumentNullException(nameof(resourceName));

            Stream stream = typeof(EmbeddedResources).GetTypeInfo().Assembly
                .GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "Встроенный ресурс '" + resourceName + "' не найден в сборке Bcf.Core.");
            }

            return stream;
        }

        /// <summary>
        /// Читает встроенный ресурс как текст в UTF-8.
        /// </summary>
        public static string ReadAllText(string resourceName)
        {
            using (Stream stream = Open(resourceName))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false), true))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>Канонический справочник значений BCF в исходном виде (JSON).</summary>
        public static string ReadVocabularyJson()
        {
            return ReadAllText(VocabularyResourceName);
        }

        /// <summary>Все имена встроенных ресурсов сборки.</summary>
        public static string[] GetNames()
        {
            return typeof(EmbeddedResources).GetTypeInfo().Assembly.GetManifestResourceNames();
        }
    }
}
