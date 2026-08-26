using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using Bcf.Core.Model;
using Bcf.Core.Resources;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// The BCF 2.1 serializer — for receiving tools where 3.0 is not supported
    /// everywhere. Not a stub: it is complete, but the format can hold less, and
    /// whatever does not fit goes into the report instead of vanishing quietly.
    ///
    /// The markup is built along different lines here: comments and viewpoints
    /// sit next to Topic rather than inside it, labels and references are
    /// written as repeated elements without a wrapper, and the vocabularies are
    /// declared by an extensions.xsd schema that has to travel in the archive
    /// together with markup.xsd — it redefines the types from exactly there.
    ///
    /// Сериализатор BCF 2.1 — для приёмников, где 3.0 поддержан не везде.
    /// Не заглушка: реализован полностью, но у формата меньше возможностей,
    /// и всё, что не помещается, попадает в отчёт, а не исчезает молча.
    ///
    /// Разметка устроена принципиально иначе: комментарии и точки зрения лежат
    /// рядом с Topic, а не внутри него, метки и ссылки пишутся повторяющимися
    /// элементами без обёрток, а справочники объявляются схемой extensions.xsd,
    /// которую надо положить в архив вместе с markup.xsd — типы она
    /// переопределяет именно оттуда.
    /// </summary>
    internal sealed class Bcf21ArchiveWriter : BcfArchiveWriter
    {
        public Bcf21ArchiveWriter(Stream destination, BcfWriteOptions options)
            : base(destination, options)
        {
        }

        public override BcfVersion Version
        {
            get { return BcfVersion.Bcf21; }
        }

        protected override void WriteVersionFile(XmlWriter writer)
        {
            writer.WriteStartElement("Version");
            writer.WriteAttributeString("VersionId", BcfVersion.Bcf21.ToVersionId());
            writer.WriteElementString("DetailedVersion", BcfVersion.Bcf21.ToVersionId());
            writer.WriteEndElement();
        }

        protected override void WriteProjectFile(XmlWriter writer)
        {
            BcfProject project = Options.Project ?? new BcfProject();

            writer.WriteStartElement("ProjectExtension");

            if (!string.IsNullOrWhiteSpace(project.ProjectId))
            {
                writer.WriteStartElement("Project");
                writer.WriteAttributeString("ProjectId", project.ProjectId);
                WriteOptionalElement(writer, "Name", project.Name);
                writer.WriteEndElement();
            }

            // The schema demands this element: it is what ties the archive to
            // the vocabulary declaration
            writer.WriteElementString("ExtensionSchema", ExtensionsWriter.Bcf21FileName);

            writer.WriteEndElement();
        }

        protected override void WriteExtensions(IReadOnlyList<string> users, BcfExtraVocabulary extra)
        {
            using (Stream stream = CreateEntry(ExtensionsWriter.Bcf21FileName, CompressionLevel.Optimal))
            {
                ExtensionsWriter.Write21(stream, users, extra);
            }

            // extensions.xsd redefines the types of markup.xsd, so that schema
            // has to lie in the archive beside it. The buildingSMART reference
            // archive MaximumInformation is built the same way.
            WriteResourceEntry(
                ExtensionsWriter.Bcf21RedefinedSchema,
                EmbeddedResources.Bcf21SchemaPrefix + ExtensionsWriter.Bcf21RedefinedSchema);
        }

        protected override void WriteMarkup(XmlWriter writer, BcfTopic topic)
        {
            writer.WriteStartElement("Markup");

            WriteHeader(writer, topic);
            WriteTopicElement(writer, topic);
            WriteComments(writer, topic);
            WriteViewpointReferences(writer, topic);

            writer.WriteEndElement();
        }

        private static void WriteHeader(XmlWriter writer, BcfTopic topic)
        {
            if (topic.Files.Count == 0) return;

            writer.WriteStartElement("Header");

            // In 2.1 File sits directly in Header, with no Files wrapper
            foreach (BcfFile file in topic.Files)
            {
                writer.WriteStartElement("File");

                if (!string.IsNullOrWhiteSpace(file.IfcProject)) writer.WriteAttributeString("IfcProject", file.IfcProject);
                if (!string.IsNullOrWhiteSpace(file.IfcSpatialStructureElement)) writer.WriteAttributeString("IfcSpatialStructureElement", file.IfcSpatialStructureElement);
                writer.WriteAttributeString("isExternal", file.IsExternal ? "true" : "false");

                WriteOptionalElement(writer, "Filename", file.Filename);
                WriteOptionalDate(writer, "Date", file.Date);
                WriteOptionalElement(writer, "Reference", file.Reference);

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private void WriteTopicElement(XmlWriter writer, BcfTopic topic)
        {
            writer.WriteStartElement("Topic");

            writer.WriteAttributeString("Guid", FormatGuid(topic.Guid));
            writer.WriteAttributeString("TopicType", topic.TopicType);
            writer.WriteAttributeString("TopicStatus", topic.TopicStatus);

            if (!string.IsNullOrWhiteSpace(topic.ServerAssignedId))
            {
                Report.Drop("ServerAssignedId", "BCF 2.1 has no such attribute");
            }

            foreach (string link in topic.ReferenceLinks)
            {
                WriteOptionalElement(writer, "ReferenceLink", link);
            }

            writer.WriteElementString("Title", topic.Title);
            WriteOptionalElement(writer, "Priority", topic.Priority);

            if (topic.Index.HasValue) writer.WriteElementString("Index", BcfNumber.Format(topic.Index.Value));

            // Labels in 2.1 are repeated Labels elements, with no wrapper
            foreach (string label in topic.Labels)
            {
                WriteOptionalElement(writer, "Labels", label);
            }

            writer.WriteElementString("CreationDate", BcfNumber.Format(topic.CreationDate));
            writer.WriteElementString("CreationAuthor", topic.CreationAuthor);
            WriteOptionalDate(writer, "ModifiedDate", topic.ModifiedDate);
            WriteOptionalElement(writer, "ModifiedAuthor", topic.ModifiedAuthor);
            WriteOptionalDate(writer, "DueDate", topic.DueDate);
            WriteOptionalElement(writer, "AssignedTo", topic.AssignedTo);
            WriteOptionalElement(writer, "Stage", topic.Stage);
            WriteOptionalElement(writer, "Description", topic.Description);

            foreach (Guid related in topic.RelatedTopics)
            {
                writer.WriteStartElement("RelatedTopic");
                writer.WriteAttributeString("Guid", FormatGuid(related));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        private void WriteComments(XmlWriter writer, BcfTopic topic)
        {
            foreach (BcfComment comment in topic.Comments)
            {
                if (string.IsNullOrWhiteSpace(comment.Text))
                {
                    // In 2.1 the comment text is mandatory; in 3.0 it is not.
                    // An empty comment would make the archive invalid.
                    Report.Warn("Comments without text were skipped: the BCF 2.1 schema demands non-empty text.");
                    continue;
                }

                writer.WriteStartElement("Comment");
                writer.WriteAttributeString("Guid", FormatGuid(comment.Guid));

                writer.WriteElementString("Date", BcfNumber.Format(comment.Date));
                writer.WriteElementString("Author", comment.Author);
                writer.WriteElementString("Comment", comment.Text);

                if (comment.ViewpointGuid.HasValue)
                {
                    writer.WriteStartElement("Viewpoint");
                    writer.WriteAttributeString("Guid", FormatGuid(comment.ViewpointGuid.Value));
                    writer.WriteEndElement();
                }

                WriteOptionalDate(writer, "ModifiedDate", comment.ModifiedDate);
                WriteOptionalElement(writer, "ModifiedAuthor", comment.ModifiedAuthor);

                writer.WriteEndElement();
            }
        }

        private static void WriteViewpointReferences(XmlWriter writer, BcfTopic topic)
        {
            foreach (BcfViewpoint viewpoint in topic.Viewpoints)
            {
                // The element is called Viewpoints yet describes a single
                // viewpoint: that is how the 2.1 schema has it, and it stays
                writer.WriteStartElement("Viewpoints");
                writer.WriteAttributeString("Guid", FormatGuid(viewpoint.Guid));

                writer.WriteElementString("Viewpoint", BcfEntryNames.ViewpointFileName(viewpoint.Guid));

                if (viewpoint.Snapshot != null && viewpoint.Snapshot.Length > 0)
                {
                    writer.WriteElementString("Snapshot", BcfEntryNames.Sanitize(viewpoint.SnapshotFileName));
                }

                if (viewpoint.Index.HasValue) writer.WriteElementString("Index", BcfNumber.Format(viewpoint.Index.Value));

                writer.WriteEndElement();
            }
        }

        protected override void WriteVisualizationInfo(XmlWriter writer, BcfViewpoint viewpoint)
        {
            writer.WriteStartElement("VisualizationInfo");
            writer.WriteAttributeString("Guid", FormatGuid(viewpoint.Guid));

            WriteComponents(writer, viewpoint);
            WriteCamera(writer, viewpoint);
            WriteClippingPlanes(writer, viewpoint);

            writer.WriteEndElement();
        }

        private static void WriteComponents(XmlWriter writer, BcfViewpoint viewpoint)
        {
            bool hasSelection = viewpoint.Selection.Count > 0;
            BcfVisibility visibility = viewpoint.Visibility;

            if (!hasSelection && visibility == null) return;

            writer.WriteStartElement("Components");

            // In 2.1 the hints live at the Components level and come first
            if (visibility != null && visibility.Hints != null)
            {
                Bcf30ArchiveWriter.WriteViewSetupHints(writer, visibility.Hints);
            }

            if (hasSelection)
            {
                writer.WriteStartElement("Selection");
                foreach (BcfComponent component in viewpoint.Selection)
                {
                    WriteComponent(writer, component);
                }
                writer.WriteEndElement();
            }

            // In 2.1 Visibility is mandatory inside Components: without one we
            // put in the default visibility, or the file is invalid
            writer.WriteStartElement("Visibility");
            writer.WriteAttributeString("DefaultVisibility", visibility != null && visibility.DefaultVisibility ? "true" : "false");

            if (visibility != null && visibility.Exceptions.Count > 0)
            {
                writer.WriteStartElement("Exceptions");
                foreach (BcfComponent component in visibility.Exceptions)
                {
                    WriteComponent(writer, component);
                }
                writer.WriteEndElement();
            }

            writer.WriteEndElement();

            writer.WriteEndElement();
        }

        private void WriteCamera(XmlWriter writer, BcfViewpoint viewpoint)
        {
            if (viewpoint.Camera == null) return;

            var perspective = viewpoint.Camera as BcfPerspectiveCamera;

            if (perspective != null)
            {
                writer.WriteStartElement("PerspectiveCamera");
                WriteVector(writer, "CameraViewPoint", perspective.ViewPoint);
                WriteVector(writer, "CameraDirection", perspective.Direction);
                WriteVector(writer, "CameraUpVector", perspective.UpVector);

                bool clamped;
                double fov = Conversion.CameraConverter.ClampFieldOfView(
                    perspective.FieldOfViewDegrees, BcfVersion.Bcf21, out clamped);

                if (clamped)
                {
                    Report.Warn(
                        "The field of view was clamped to the [45; 60] interval: the BCF 2.1 schema allows nothing else. " +
                        "In 2.1 the view will differ from the original; in 3.0 it will not.");
                }

                writer.WriteElementString("FieldOfView", BcfNumber.Format(fov));
                writer.WriteEndElement();
            }
            else
            {
                var orthogonal = (BcfOrthogonalCamera)viewpoint.Camera;

                writer.WriteStartElement("OrthogonalCamera");
                WriteVector(writer, "CameraViewPoint", orthogonal.ViewPoint);
                WriteVector(writer, "CameraDirection", orthogonal.Direction);
                WriteVector(writer, "CameraUpVector", orthogonal.UpVector);
                writer.WriteElementString("ViewToWorldScale", BcfNumber.Format(orthogonal.ViewToWorldScale));
                writer.WriteEndElement();
            }

            Report.Drop("AspectRatio", "cameras in BCF 2.1 have no such field");
        }
    }
}
