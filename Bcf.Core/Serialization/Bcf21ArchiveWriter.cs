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
    /// Сериализатор BCF 2.1 — для приёмников, где 3.0 поддержан не везде.
    /// Не заглушка: реализован полностью, но у формата меньше возможностей,
    /// и всё, что не помещается, попадает в отчёт, а не исчезает молча.
    ///
    /// Устройство markup здесь принципиально другое: комментарии и точки зрения
    /// лежат не внутри Topic, а рядом с ним, метки и ссылки пишутся
    /// повторяющимися элементами без обёрток, а справочники объявляются
    /// схемой extensions.xsd, которую надо положить в архив вместе с markup.xsd —
    /// она переопределяет типы именно оттуда.
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

            // Элемент обязателен по схеме: он и связывает архив с объявлением справочников
            writer.WriteElementString("ExtensionSchema", ExtensionsWriter.Bcf21FileName);

            writer.WriteEndElement();
        }

        protected override void WriteExtensions(IReadOnlyList<string> users)
        {
            using (Stream stream = CreateEntry(ExtensionsWriter.Bcf21FileName, CompressionLevel.Optimal))
            {
                ExtensionsWriter.Write21(stream, users);
            }

            // extensions.xsd переопределяет типы markup.xsd через redefine,
            // поэтому сама схема обязана лежать в архиве рядом. Так же устроен
            // эталонный архив MaximumInformation у buildingSMART.
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

            // В 2.1 File лежит прямо в Header, без обёртки Files
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
                Report.Drop("ServerAssignedId", "в BCF 2.1 такого атрибута нет");
            }

            foreach (string link in topic.ReferenceLinks)
            {
                WriteOptionalElement(writer, "ReferenceLink", link);
            }

            writer.WriteElementString("Title", topic.Title);
            WriteOptionalElement(writer, "Priority", topic.Priority);

            if (topic.Index.HasValue) writer.WriteElementString("Index", BcfNumber.Format(topic.Index.Value));

            // Метки в 2.1 — повторяющиеся элементы Labels, без обёртки
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
                    // В 2.1 текст комментария обязателен, в 3.0 — нет.
                    // Пустой комментарий сделал бы архив невалидным.
                    Report.Warn("Комментарии без текста пропущены: схема BCF 2.1 требует непустой текст.");
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
                // Элемент называется Viewpoints, но описывает одну точку зрения:
                // так в схеме 2.1, менять нельзя
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

            // В 2.1 подсказки лежат на уровне Components и идут первыми
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

            // Visibility в 2.1 обязателен внутри Components: если его нет,
            // подставляем видимость по умолчанию, иначе файл невалиден
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
                        "Угол обзора подрезан до интервала [45; 60]: схема BCF 2.1 других значений не допускает. " +
                        "В 2.1 вид будет отличаться от исходного, в 3.0 — нет.");
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

            Report.Drop("AspectRatio", "в BCF 2.1 у камер нет такого поля");
        }
    }
}
