using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using Bcf.Core.Model;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// The BCF 3.0 serializer — the main export format.
    ///
    /// The differences from 2.1 that renaming files cannot paper over:
    /// comments and viewpoints live inside Topic, the vocabularies moved out
    /// into extensions.xml, cameras gained a mandatory AspectRatio, and
    /// ViewSetupHints moved inside Visibility.
    ///
    /// Сериализатор BCF 3.0 — основной формат выгрузки.
    ///
    /// Отличия от 2.1, которые не обойти переименованием файлов: комментарии
    /// и точки зрения лежат внутри Topic, справочники вынесены в extensions.xml,
    /// у камер появился обязательный AspectRatio, а ViewSetupHints переехал
    /// внутрь Visibility.
    /// </summary>
    internal sealed class Bcf30ArchiveWriter : BcfArchiveWriter
    {
        public Bcf30ArchiveWriter(Stream destination, BcfWriteOptions options)
            : base(destination, options)
        {
        }

        public override BcfVersion Version
        {
            get { return BcfVersion.Bcf30; }
        }

        protected override void WriteVersionFile(XmlWriter writer)
        {
            writer.WriteStartElement("Version");
            writer.WriteAttributeString("VersionId", BcfVersion.Bcf30.ToVersionId());
            writer.WriteEndElement();
        }

        protected override void WriteProjectFile(XmlWriter writer)
        {
            BcfProject project = Options.Project ?? new BcfProject();

            writer.WriteStartElement("ProjectInfo");
            writer.WriteStartElement("Project");
            writer.WriteAttributeString("ProjectId", EnsureProjectId(project.ProjectId));
            WriteOptionalElement(writer, "Name", project.Name);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        protected override void WriteExtensions(IReadOnlyList<string> users, BcfExtraVocabulary extra)
        {
            using (Stream stream = CreateEntry(ExtensionsWriter.Bcf30FileName, CompressionLevel.Optimal))
            {
                ExtensionsWriter.Write30(stream, users, extra);
            }
        }

        protected override void WriteMarkup(XmlWriter writer, BcfTopic topic)
        {
            writer.WriteStartElement("Markup");

            WriteHeader(writer, topic);
            WriteTopicElement(writer, topic);

            writer.WriteEndElement();
        }

        private static void WriteHeader(XmlWriter writer, BcfTopic topic)
        {
            if (topic.Files.Count == 0) return;

            writer.WriteStartElement("Header");
            writer.WriteStartElement("Files");

            foreach (BcfFile file in topic.Files)
            {
                writer.WriteStartElement("File");

                if (!string.IsNullOrWhiteSpace(file.IfcProject)) writer.WriteAttributeString("IfcProject", file.IfcProject);
                if (!string.IsNullOrWhiteSpace(file.IfcSpatialStructureElement)) writer.WriteAttributeString("IfcSpatialStructureElement", file.IfcSpatialStructureElement);
                writer.WriteAttributeString("IsExternal", file.IsExternal ? "true" : "false");

                WriteOptionalElement(writer, "Filename", file.Filename);
                WriteOptionalDate(writer, "Date", file.Date);
                WriteOptionalElement(writer, "Reference", file.Reference);

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        private static void WriteTopicElement(XmlWriter writer, BcfTopic topic)
        {
            writer.WriteStartElement("Topic");

            writer.WriteAttributeString("Guid", FormatGuid(topic.Guid));
            if (!string.IsNullOrWhiteSpace(topic.ServerAssignedId)) writer.WriteAttributeString("ServerAssignedId", topic.ServerAssignedId);
            writer.WriteAttributeString("TopicType", topic.TopicType);
            writer.WriteAttributeString("TopicStatus", topic.TopicStatus);

            // The element order is fixed by the xs:sequence of markup.xsd.
            // Reordering makes the file invalid, and the eye does not catch it.
            if (topic.ReferenceLinks.Count > 0)
            {
                writer.WriteStartElement("ReferenceLinks");
                foreach (string link in topic.ReferenceLinks)
                {
                    WriteOptionalElement(writer, "ReferenceLink", link);
                }
                writer.WriteEndElement();
            }

            writer.WriteElementString("Title", topic.Title);
            WriteOptionalElement(writer, "Priority", topic.Priority);

            if (topic.Index.HasValue) writer.WriteElementString("Index", BcfNumber.Format(topic.Index.Value));

            if (topic.Labels.Count > 0)
            {
                writer.WriteStartElement("Labels");
                foreach (string label in topic.Labels)
                {
                    WriteOptionalElement(writer, "Label", label);
                }
                writer.WriteEndElement();
            }

            writer.WriteElementString("CreationDate", BcfNumber.Format(topic.CreationDate));
            writer.WriteElementString("CreationAuthor", topic.CreationAuthor);
            WriteOptionalDate(writer, "ModifiedDate", topic.ModifiedDate);
            WriteOptionalElement(writer, "ModifiedAuthor", topic.ModifiedAuthor);
            WriteOptionalDate(writer, "DueDate", topic.DueDate);
            WriteOptionalElement(writer, "AssignedTo", topic.AssignedTo);
            WriteOptionalElement(writer, "Stage", topic.Stage);
            WriteOptionalElement(writer, "Description", topic.Description);

            if (topic.RelatedTopics.Count > 0)
            {
                writer.WriteStartElement("RelatedTopics");
                foreach (Guid related in topic.RelatedTopics)
                {
                    writer.WriteStartElement("RelatedTopic");
                    writer.WriteAttributeString("Guid", FormatGuid(related));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }

            WriteComments(writer, topic);
            WriteViewpointReferences(writer, topic);

            writer.WriteEndElement();
        }

        private static void WriteComments(XmlWriter writer, BcfTopic topic)
        {
            if (topic.Comments.Count == 0) return;

            writer.WriteStartElement("Comments");

            foreach (BcfComment comment in topic.Comments)
            {
                writer.WriteStartElement("Comment");
                writer.WriteAttributeString("Guid", FormatGuid(comment.Guid));

                writer.WriteElementString("Date", BcfNumber.Format(comment.Date));
                writer.WriteElementString("Author", comment.Author);
                WriteOptionalElement(writer, "Comment", comment.Text);

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

            writer.WriteEndElement();
        }

        private static void WriteViewpointReferences(XmlWriter writer, BcfTopic topic)
        {
            if (topic.Viewpoints.Count == 0) return;

            writer.WriteStartElement("Viewpoints");

            foreach (BcfViewpoint viewpoint in topic.Viewpoints)
            {
                writer.WriteStartElement("ViewPoint");
                writer.WriteAttributeString("Guid", FormatGuid(viewpoint.Guid));

                writer.WriteElementString("Viewpoint", BcfEntryNames.ViewpointFileName(viewpoint.Guid));

                if (viewpoint.Snapshot != null && viewpoint.Snapshot.Length > 0)
                {
                    writer.WriteElementString("Snapshot", BcfEntryNames.Sanitize(viewpoint.SnapshotFileName));
                }

                if (viewpoint.Index.HasValue) writer.WriteElementString("Index", BcfNumber.Format(viewpoint.Index.Value));

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
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
            bool hasVisibility = viewpoint.Visibility != null;

            if (!hasSelection && !hasVisibility) return;

            writer.WriteStartElement("Components");

            if (hasSelection)
            {
                writer.WriteStartElement("Selection");
                foreach (BcfComponent component in viewpoint.Selection)
                {
                    WriteComponent(writer, component);
                }
                writer.WriteEndElement();
            }

            if (hasVisibility)
            {
                BcfVisibility visibility = viewpoint.Visibility;

                writer.WriteStartElement("Visibility");
                writer.WriteAttributeString("DefaultVisibility", visibility.DefaultVisibility ? "true" : "false");

                // In 3.0 the hints live inside Visibility; in 2.1, at the Components level
                if (visibility.Hints != null)
                {
                    WriteViewSetupHints(writer, visibility.Hints);
                }

                if (visibility.Exceptions.Count > 0)
                {
                    writer.WriteStartElement("Exceptions");
                    foreach (BcfComponent component in visibility.Exceptions)
                    {
                        WriteComponent(writer, component);
                    }
                    writer.WriteEndElement();
                }

                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        internal static void WriteViewSetupHints(XmlWriter writer, BcfViewSetupHints hints)
        {
            writer.WriteStartElement("ViewSetupHints");
            writer.WriteAttributeString("SpacesVisible", hints.SpacesVisible ? "true" : "false");
            writer.WriteAttributeString("SpaceBoundariesVisible", hints.SpaceBoundariesVisible ? "true" : "false");
            writer.WriteAttributeString("OpeningsVisible", hints.OpeningsVisible ? "true" : "false");
            writer.WriteEndElement();
        }

        private void WriteCamera(XmlWriter writer, BcfViewpoint viewpoint)
        {
            if (viewpoint.Camera == null)
            {
                // The 3.0 schema demands a camera: the choice between
                // OrthogonalCamera and PerspectiveCamera carries no minOccurs="0".
                throw new InvalidOperationException(
                    "The viewpoint " + FormatGuid(viewpoint.Guid) + " has no camera, and the BCF 3.0 schema demands one.");
            }

            var perspective = viewpoint.Camera as BcfPerspectiveCamera;

            if (perspective != null)
            {
                writer.WriteStartElement("PerspectiveCamera");
                WriteVector(writer, "CameraViewPoint", perspective.ViewPoint);
                WriteVector(writer, "CameraDirection", perspective.Direction);
                WriteVector(writer, "CameraUpVector", perspective.UpVector);

                bool clamped;
                double fov = Conversion.CameraConverter.ClampFieldOfView(
                    perspective.FieldOfViewDegrees, BcfVersion.Bcf30, out clamped);

                if (clamped)
                {
                    Report.Warn("The field of view fell outside the (0; 180) interval the 3.0 schema allows and was clamped.");
                }

                writer.WriteElementString("FieldOfView", BcfNumber.Format(fov));
                writer.WriteElementString("AspectRatio", BcfNumber.Format(perspective.AspectRatio));
                writer.WriteEndElement();
                return;
            }

            var orthogonal = (BcfOrthogonalCamera)viewpoint.Camera;

            writer.WriteStartElement("OrthogonalCamera");
            WriteVector(writer, "CameraViewPoint", orthogonal.ViewPoint);
            WriteVector(writer, "CameraDirection", orthogonal.Direction);
            WriteVector(writer, "CameraUpVector", orthogonal.UpVector);
            writer.WriteElementString("ViewToWorldScale", BcfNumber.Format(orthogonal.ViewToWorldScale));
            writer.WriteElementString("AspectRatio", BcfNumber.Format(orthogonal.AspectRatio));
            writer.WriteEndElement();
        }

        private static string EnsureProjectId(string projectId)
        {
            // The schema demands a non-empty ProjectId. An empty identifier
            // means a forgotten setting, and failing here beats handing out an
            // invalid archive.
            if (string.IsNullOrWhiteSpace(projectId))
            {
                throw new InvalidOperationException("The project identifier is not set (BcfWriteOptions.Project.ProjectId).");
            }

            return projectId;
        }
    }
}
