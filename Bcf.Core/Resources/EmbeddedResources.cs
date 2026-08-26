using System;
using System.IO;
using System.Reflection;

namespace Bcf.Core.Resources
{
    /// <summary>
    /// Access to the resources embedded in the library: the BCF vocabulary and
    /// the buildingSMART XSD schemas.
    ///
    /// Доступ к ресурсам, встроенным в библиотеку: справочнику значений BCF
    /// и XSD-схемам buildingSMART.
    /// </summary>
    public static class EmbeddedResources
    {
        /// <summary>
        /// The resource name of the canonical vocabulary.
        /// Имя ресурса с каноническим справочником значений.
        /// </summary>
        public const string VocabularyResourceName = "Bcf.Core.Resources.bcf-extensions.json";

        /// <summary>
        /// The name prefix of the BCF 3.0 schema resources.
        /// Префикс имён ресурсов со схемами BCF 3.0.
        /// </summary>
        public const string Bcf30SchemaPrefix = "Bcf.Core.Schemas.Bcf30.";

        /// <summary>
        /// The name prefix of the BCF 2.1 schema resources.
        /// Префикс имён ресурсов со схемами BCF 2.1.
        /// </summary>
        public const string Bcf21SchemaPrefix = "Bcf.Core.Schemas.Bcf21.";

        /// <summary>
        /// Opens an embedded resource by name.
        /// Открывает встроенный ресурс по имени.
        /// </summary>
        /// <param name="resourceName">The logical name of the resource.</param>
        /// <exception cref="InvalidOperationException">
        /// The resource is missing, which means the assembly was built wrong.
        /// </exception>
        public static Stream Open(string resourceName)
        {
            if (resourceName == null) throw new ArgumentNullException(nameof(resourceName));

            Stream stream = typeof(EmbeddedResources).GetTypeInfo().Assembly
                .GetManifestResourceStream(resourceName);

            if (stream == null)
            {
                throw new InvalidOperationException(
                    "Embedded resource '" + resourceName + "' is not present in the Bcf.Core assembly.");
            }

            return stream;
        }

        /// <summary>
        /// Reads an embedded resource as UTF-8 text.
        /// Читает встроенный ресурс как текст в UTF-8.
        /// </summary>
        /// <param name="resourceName">The logical name of the resource.</param>
        public static string ReadAllText(string resourceName)
        {
            using (Stream stream = Open(resourceName))
            using (var reader = new StreamReader(stream, new System.Text.UTF8Encoding(false), true))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// The canonical BCF vocabulary in its original form, as JSON.
        /// Канонический справочник значений BCF в исходном виде, как JSON.
        /// </summary>
        public static string ReadVocabularyJson()
        {
            return ReadAllText(VocabularyResourceName);
        }

        /// <summary>
        /// The names of every resource embedded in the assembly.
        /// Имена всех ресурсов, встроенных в сборку.
        /// </summary>
        public static string[] GetNames()
        {
            return typeof(EmbeddedResources).GetTypeInfo().Assembly.GetManifestResourceNames();
        }
    }
}
