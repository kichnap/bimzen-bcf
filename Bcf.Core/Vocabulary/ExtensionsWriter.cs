using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Bcf.Core.Vocabulary
{
    /// <summary>
    /// Builds the vocabulary declaration for an archive out of constants
    /// rather than out of a ready-made file: the single source of truth is
    /// bcf-extensions.json, and any pre-baked extensions.xml would drift away
    /// from it on the first change to the vocabulary.
    ///
    /// In 3.0 this is extensions.xml at the root of the archive. In 2.1 the
    /// mechanism differs: an extensions.xsd that redefines the simple types of
    /// markup.xsd — which is why markup.xsd has to lie in the archive beside it,
    /// or the schema will not resolve.
    ///
    /// Формирует объявление справочников для архива из констант, а не
    /// из готового файла: единственный источник правды — bcf-extensions.json,
    /// и любой заранее заготовленный extensions.xml разошёлся бы с ним первым же
    /// изменением справочника.
    ///
    /// В 3.0 это extensions.xml в корне архива. В 2.1 механизм другой:
    /// extensions.xsd, переопределяющий простые типы markup.xsd, — поэтому
    /// markup.xsd обязан лежать в архиве рядом, иначе схема не разрешится.
    /// </summary>
    public static class ExtensionsWriter
    {
        /// <summary>
        /// The name of the vocabulary file in a BCF 3.0 archive.
        /// Имя файла справочников в архиве BCF 3.0.
        /// </summary>
        public const string Bcf30FileName = "extensions.xml";

        /// <summary>
        /// The name of the vocabulary file in a BCF 2.1 archive. Exactly
        /// extensions.xsd, in the plural — that is what the buildingSMART test
        /// cases call it and what receiving tools look for.
        ///
        /// Имя файла справочников в архиве BCF 2.1. Именно extensions.xsd,
        /// во множественном числе — так он называется в тест-кейсах
        /// buildingSMART и так его ищут сторонние приёмники.
        /// </summary>
        public const string Bcf21FileName = "extensions.xsd";

        /// <summary>
        /// The schema that extensions.xsd redefines in 2.1.
        /// Схема, которую переопределяет extensions.xsd в 2.1.
        /// </summary>
        public const string Bcf21RedefinedSchema = "markup.xsd";

        private const string XmlSchemaNamespace = "http://www.w3.org/2001/XMLSchema";

        /// <summary>
        /// The prefix of the XSD namespace. A prefix and not a default
        /// namespace on purpose: the values of the base and type attributes are
        /// QNames, and under xmlns="...XMLSchema" the unqualified name TopicType
        /// would land in the namespace of the schema itself. The redefine then
        /// stops pointing at the type it redefines and the validator says
        /// "the base type has to be self-referenced".
        ///
        /// Префикс пространства имён XSD. Именно префикс, а не пространство
        /// по умолчанию: значения атрибутов base и type — это QName, и при
        /// xmlns="...XMLSchema" неквалифицированное имя TopicType уехало бы
        /// в пространство самой схемы. Тогда redefine перестаёт ссылаться
        /// на переопределяемый тип, и валидатор говорит
        /// "the base type has to be self-referenced".
        /// </summary>
        private const string XmlSchemaPrefix = "xs";

        /// <summary>
        /// Writes extensions.xml (BCF 3.0) to a stream. UTF-8 without a BOM:
        /// parsers outside .NET stumble over a BOM far more often.
        ///
        /// Пишет extensions.xml (BCF 3.0) в поток. UTF-8 без BOM: чужие парсеры
        /// на BOM спотыкаются чаще, чем .NET.
        /// </summary>
        /// <param name="stream">The archive entry stream.</param>
        /// <param name="users">Export author and the assignees encountered, already filtered through <see cref="BcfUsers"/>.</param>
        public static void Write30(Stream stream, IEnumerable<string> users)
        {
            Write30(stream, users, null);
        }

        /// <summary>
        /// Writes extensions.xml (BCF 3.0) to a stream. UTF-8 without a BOM.
        ///
        /// Пишет extensions.xml (BCF 3.0) в поток. UTF-8 без BOM.
        /// </summary>
        /// <param name="stream">The archive entry stream.</param>
        /// <param name="users">Export author and the assignees encountered, already filtered through <see cref="BcfUsers"/>.</param>
        /// <param name="extra">
        /// Foreign values that entered the archive while updating an existing
        /// file. A file we write must declare everything it contains.
        ///
        /// Чужие значения, попавшие в архив при обновлении существующего файла.
        /// Файл, который мы пишем, обязан объявлять всё, что в нём есть.
        /// </param>
        public static void Write30(Stream stream, IEnumerable<string> users, BcfExtraVocabulary extra)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            extra = extra ?? new BcfExtraVocabulary();

            using (XmlWriter writer = CreateWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement("Extensions");

                // The element order is fixed by the extensions.xsd schema
                // (xs:sequence): TopicTypes, TopicStatuses, Priorities,
                // TopicLabels, Users, SnippetTypes, Stages. Reordering makes the
                // file invalid — in the reference bcf-vocabularies/extensions.xml
                // Stages sits above Users, and such a file fails the schema.
                WriteValues(writer, "TopicTypes", "TopicType", BcfExtraVocabulary.Combine(BcfVocabulary.TopicTypes.All, extra.TopicTypes));
                WriteValues(writer, "TopicStatuses", "TopicStatus", BcfExtraVocabulary.Combine(BcfVocabulary.TopicStatuses.All, extra.TopicStatuses));
                WriteValues(writer, "Priorities", "Priority", BcfExtraVocabulary.Combine(BcfVocabulary.Priorities.All, extra.Priorities));
                WriteValues(writer, "TopicLabels", "TopicLabel", BcfExtraVocabulary.Combine(BcfVocabulary.TopicLabels.All, extra.TopicLabels));
                WriteValues(writer, "Users", "User", Materialize(users));
                WriteValues(writer, "Stages", "Stage", BcfExtraVocabulary.Combine(BcfVocabulary.Stages.All, extra.Stages));

                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        /// <summary>
        /// Writes extensions.xsd (BCF 2.1) to a stream. UTF-8 without a BOM.
        ///
        /// Пишет extensions.xsd (BCF 2.1) в поток. UTF-8 без BOM.
        /// </summary>
        /// <param name="stream">The archive entry stream.</param>
        /// <param name="users">Export author and the assignees encountered.</param>
        public static void Write21(Stream stream, IEnumerable<string> users)
        {
            Write21(stream, users, null);
        }

        /// <summary>
        /// Writes extensions.xsd (BCF 2.1) to a stream. UTF-8 without a BOM.
        ///
        /// Пишет extensions.xsd (BCF 2.1) в поток. UTF-8 без BOM.
        /// </summary>
        /// <param name="stream">The archive entry stream.</param>
        /// <param name="users">Export author and the assignees encountered.</param>
        /// <param name="extra">Foreign values — see <see cref="Write30(Stream, IEnumerable{string}, BcfExtraVocabulary)"/>.</param>
        public static void Write21(Stream stream, IEnumerable<string> users, BcfExtraVocabulary extra)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));

            extra = extra ?? new BcfExtraVocabulary();

            using (XmlWriter writer = CreateWriter(stream))
            {
                writer.WriteStartDocument();
                writer.WriteStartElement(XmlSchemaPrefix, "schema", XmlSchemaNamespace);

                writer.WriteStartElement(XmlSchemaPrefix, "redefine", XmlSchemaNamespace);
                writer.WriteAttributeString("schemaLocation", Bcf21RedefinedSchema);

                // Only the simple types declared in markup.xsd 2.1 are
                // redefined. SnippetType is not among them — there it is an
                // ordinary string attribute, and a redefine breaks the schema.
                WriteEnumeration(writer, "TopicType", BcfExtraVocabulary.Combine(BcfVocabulary.TopicTypes.All, extra.TopicTypes));
                WriteEnumeration(writer, "TopicStatus", BcfExtraVocabulary.Combine(BcfVocabulary.TopicStatuses.All, extra.TopicStatuses));
                WriteEnumeration(writer, "TopicLabel", BcfExtraVocabulary.Combine(BcfVocabulary.TopicLabels.All, extra.TopicLabels));
                WriteEnumeration(writer, "Priority", BcfExtraVocabulary.Combine(BcfVocabulary.Priorities.All, extra.Priorities));
                WriteEnumeration(writer, "UserIdType", Materialize(users));
                WriteEnumeration(writer, "Stage", BcfExtraVocabulary.Combine(BcfVocabulary.Stages.All, extra.Stages));

                writer.WriteEndElement();
                writer.WriteEndElement();
                writer.WriteEndDocument();
            }
        }

        /// <summary>
        /// extensions.xml (BCF 3.0) as text — for tests and diagnostics.
        /// extensions.xml (BCF 3.0) как текст — для тестов и диагностики.
        /// </summary>
        /// <param name="users">Export author and the assignees encountered.</param>
        public static string ToXml30(IEnumerable<string> users)
        {
            return ToText(s => Write30(s, users));
        }

        /// <summary>
        /// extensions.xsd (BCF 2.1) as text — for tests and diagnostics.
        /// extensions.xsd (BCF 2.1) как текст — для тестов и диагностики.
        /// </summary>
        /// <param name="users">Export author and the assignees encountered.</param>
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
                // The encoding is set here rather than at decoding time:
                // XmlWriter puts this very value into the document declaration.
                // UTF8Encoding(false) means no BOM.
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
            if (values == null) return Array.Empty<string>();

            var list = values as IReadOnlyList<string>;
            return list ?? new List<string>(values);
        }
    }
}
