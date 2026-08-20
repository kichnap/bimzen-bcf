using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Формирует объявление справочников для архива — из констант, а не из
    /// готового файла: единственный источник правды это bcf-extensions.json,
    /// и любой предзаготовленный extensions.xml разошёлся бы с ним первым же
    /// изменением справочника.
    ///
    /// В 3.0 это extensions.xml в корне архива. В 2.1 механизм другой:
    /// extensions.xsd, переопределяющий простые типы markup.xsd через redefine —
    /// поэтому markup.xsd обязан лежать в архиве рядом, иначе схема не разрешится.
    /// </summary>
    public static class ExtensionsWriter
    {
        /// <summary>Имя файла справочников в архиве BCF 3.0.</summary>
        public const string Bcf30FileName = "extensions.xml";

        /// <summary>
        /// Имя файла справочников в архиве BCF 2.1. Именно extensions.xsd,
        /// во множественном числе — так он называется в тест-кейсах buildingSMART
        /// и так его ищут сторонние приёмники.
        /// </summary>
        public const string Bcf21FileName = "extensions.xsd";

        /// <summary>Схема, которую переопределяет extensions.xsd в 2.1.</summary>
        public const string Bcf21RedefinedSchema = "markup.xsd";

        private const string XmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema";

        /// <summary>
        /// Префикс пространства имён XSD. Именно префикс, а не пространство
        /// по умолчанию: значения атрибутов base и type — это QName, и при
        /// xmlns="...XMLSchema" неквалифицированное имя TopicType уехало бы
        /// в пространство самой схемы. Тогда redefine перестаёт ссылаться
        /// на переопределяемый тип, и валидатор говорит
        /// "the base type has to be self-referenced".
        /// </summary>
        private const string XmlSchemaPrefix = "xs";

        /// <summary>
        /// Пишет extensions.xml (BCF 3.0) в поток. UTF-8 без BOM: Node-парсеры
        /// на BOM спотыкаются чаще, чем .NET.
        /// </summary>
        /// <param name="stream">Поток архива.</param>
        /// <param name="users">Автор выгрузки и встреченные исполнители, уже пропущенные через <see cref="BcfUsers"/>.</param>
        public static void Write30(Stream stream, IEnumerable<string> users)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (XmlWriter writer = CreateWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Extensions");

                // Порядок элементов задан схемой extensions.xsd (xs:sequence):
                // TopicTypes, TopicStatuses, Priorities, TopicLabels, Users,
                // SnippetTypes, Stages. Перестановка делает файл невалидным —
                // в эталоне bcf-vocabularies/extensions.xml Stages стоит выше Users,
                // и по схеме такой файл не проходит.
                WriteValues(writer, "TopicTypes", "TopicType", BcfVocabulary.TopicTypes.All);
                WriteValues(writer, "TopicStatuses", "TopicStatus", BcfVocabulary.TopicStatuses.All);
                WriteValues(writer, "Priorities", "Priority", BcfVocabulary.Priorities.All);
                WriteValues(writer, "TopicLabels", "TopicLabel", BcfVocabulary.TopicLabels.All);
                WriteValues(writer, "Users", "User", Materialize(users));
                WriteValues(writer, "Stages", "Stage", BcfVocabulary.Stages.All);

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        /// <summary>Пишет extensions.xsd (BCF 2.1) в поток. UTF-8 без BOM.</summary>
        public static void Write21(Stream stream, IEnumerable<string> users)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            using (XmlWriter writer = CreateWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(XmlSchemaPrefix, "schema", XmlSchemaNamespace);

                writer.WriteStartElement(XmlSchemaPrefix, "redefine", XmlSchemaNamespace);
                writer.WriteAttributeString("schemaLocation", Bcf21RedefinedSchema);

                // Переопределяются только простые типы, объявленные в markup.xsd 2.1.
                // SnippetType туда не входит — там это обычный строковый атрибут,
                // и redefine на него схему сломает.
                WriteEnumeration(writer, "TopicType", BcfVocabulary.TopicTypes.All);
                WriteEnumeration(writer, "TopicStatus", BcfVocabulary.TopicStatuses.All);
                WriteEnumeration(writer, "TopicLabel", BcfVocabulary.TopicLabels.All);
                WriteEnumeration(writer, "Priority", BcfVocabulary.Priorities.All);
                WriteEnumeration(writer, "UserIdType", Materialize(users));
                WriteEnumeration(writer, "Stage", BcfVocabulary.Stages.All);

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        /// <summary>extensions.xml (BCF 3.0) как текст — для тестов и диагностики.</summary>
        public static string ToXml30(IEnumerable<string> users)
        {
            return ToText(s => Write30(s, users));
        }

        /// <summary>extensions.xsd (BCF 2.1) как текст — для тестов и диагностики.</summary>
        public static string ToXsd21(IEnumerable<string> users)
        {
            return ToText(s => Write21(s, users));
        }

        private static XmlWriter CreateWriter(Stream stream)
        {
            var settings = new XmlWriterSettings
            {
                Indent = true,
                IndentChars = "  ",
                // Кодировка задаётся здесь, а не при декодировании: XmlWriter
                // пишет её же в объявление документа. UTF8Encoding(false) — без BOM.
                Encoding = new UTF8Encoding(false),
                CloseOutput = false
            };

            return XmlWriter.Create(stream, settings);
        }

        private static string ToText(Action<Stream> write)
        {
            using (var buffer = new MemoryStream())
            {
                write(buffer);
                return new UTF8Encoding(false).GetString(buffer.ToArray());
            }
        }

        private static void WriteValues(XmlWriter writer, string containerName, string itemName, IReadOnlyList<string> values)
        {
            writer.WriteStartElement(containerName);

            for (int i = 0; i < values.Count; i++)
            {
                writer.WriteElementString(itemName, values[i]);
            }

            writer.WriteEndElement();
        }

        private static void WriteEnumeration(XmlWriter writer, string typeName, IReadOnlyList<string> values)
        {
            writer.WriteStartElement(XmlSchemaPrefix, "simpleType", XmlSchemaNamespace);
            writer.WriteAttributeString("name", typeName);

            writer.WriteStartElement(XmlSchemaPrefix, "restriction", XmlSchemaNamespace);
            writer.WriteAttributeString("base", typeName);

            for (int i = 0; i < values.Count; i++)
            {
                writer.WriteStartElement(XmlSchemaPrefix, "enumeration", XmlSchemaNamespace);
                writer.WriteAttributeString("value", values[i]);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static IReadOnlyList<string> Materialize(IEnumerable<string> values)
        {
            if (values == null) return new string[0];

            var list = values as IReadOnlyList<string>;
            return list ?? new List<string>(values);
        }
    }
}
