using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Vocabulary;

namespace Bcf.Core.Serialization
{
    /// <summary>
    /// Чтение архива BCF — терпимое, в отличие от записи.
    ///
    /// Файл мог быть создан BIMcollab, Revizto или Solibri, у которых свои
    /// словари статусов и типов, и это законно: стандарт объявляет механизм
    /// справочников, но не фиксирует значения. Незнакомое значение сохраняется
    /// как есть и попадает в отчёт; топик из-за него не отбрасывается.
    /// Отвергать такой файл — самый быстрый способ прослыть инструментом,
    /// который «не понимает openBIM».
    ///
    /// Разбор не различает версии жёстко: комментарии и точки зрения ищутся
    /// и внутри Topic (3.0), и рядом с ним (2.1). Реальные файлы бывают
    /// на полшага не по схеме, и упасть на этом — не лучшая услуга пользователю.
    /// </summary>
    public static class BcfArchiveReader
    {
        public static BcfReadResult Read(Stream archiveStream)
        {
            if (archiveStream == null) throw new ArgumentNullException(nameof(archiveStream));

            var result = new BcfReadResult();

            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read, leaveOpen: true))
            {
                ReadVersion(archive, result);
                ReadProject(archive, result);

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (!IsMarkup(entry.FullName)) continue;

                    try
                    {
                        BcfTopic topic = ReadMarkup(entry, archive, result);
                        if (topic != null) result.Topics.Add(topic);
                    }
                    catch (Exception ex)
                    {
                        // Один битый топик не должен обрушить чтение всего архива
                        result.Warn("Не удалось прочитать '" + entry.FullName + "': " + ex.Message);
                    }
                }
            }

            return result;
        }

        private static bool IsMarkup(string entryName)
        {
            return entryName.EndsWith(BcfEntryNames.Markup, StringComparison.OrdinalIgnoreCase);
        }

        private static void ReadVersion(ZipArchive archive, BcfReadResult result)
        {
            ZipArchiveEntry entry = Find(archive, BcfEntryNames.Version);

            if (entry == null)
            {
                result.Warn("В архиве нет файла bcf.version, версия принята за 3.0.");
                return;
            }

            try
            {
                XDocument document = Load(entry);
                string versionId = (string)document.Root.Attribute("VersionId");

                result.Version = BcfVersionExtensions.Parse(versionId);
            }
            catch (Exception ex)
            {
                result.Warn("Версию архива определить не удалось (" + ex.Message + "), принята за 3.0.");
            }
        }

        private static void ReadProject(ZipArchive archive, BcfReadResult result)
        {
            ZipArchiveEntry entry = Find(archive, BcfEntryNames.Project);
            if (entry == null) return;

            try
            {
                XDocument document = Load(entry);

                // 3.0: ProjectInfo/Project, 2.1: ProjectExtension/Project
                XElement project = document.Root.Element("Project");
                if (project == null) return;

                result.Project = new BcfProject
                {
                    ProjectId = (string)project.Attribute("ProjectId"),
                    Name = (string)project.Element("Name")
                };
            }
            catch (Exception ex)
            {
                result.Warn("Файл project.bcfp не прочитан: " + ex.Message);
            }
        }

        private static BcfTopic ReadMarkup(ZipArchiveEntry entry, ZipArchive archive, BcfReadResult result)
        {
            XDocument document = Load(entry);
            XElement markup = document.Root;
            XElement topicElement = markup.Element("Topic");

            if (topicElement == null)
            {
                result.Warn("В '" + entry.FullName + "' нет элемента Topic, файл пропущен.");
                return null;
            }

            var topic = new BcfTopic
            {
                Guid = ParseGuid((string)topicElement.Attribute("Guid")),
                ServerAssignedId = (string)topicElement.Attribute("ServerAssignedId"),
                TopicType = (string)topicElement.Attribute("TopicType"),
                TopicStatus = (string)topicElement.Attribute("TopicStatus"),
                Title = (string)topicElement.Element("Title"),
                Priority = (string)topicElement.Element("Priority"),
                CreationAuthor = (string)topicElement.Element("CreationAuthor"),
                ModifiedAuthor = (string)topicElement.Element("ModifiedAuthor"),
                AssignedTo = (string)topicElement.Element("AssignedTo"),
                Stage = (string)topicElement.Element("Stage"),
                Description = (string)topicElement.Element("Description")
            };

            topic.CreationDate = ParseDate(topicElement.Element("CreationDate"), result) ?? default(DateTimeOffset);
            topic.ModifiedDate = ParseDate(topicElement.Element("ModifiedDate"), result);
            topic.DueDate = ParseDate(topicElement.Element("DueDate"), result);

            int index;
            if (int.TryParse((string)topicElement.Element("Index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
            {
                topic.Index = index;
            }

            // Метки: в 3.0 обёрнуты в Labels/Label, в 2.1 — повторяющиеся Labels
            XElement labelsWrapper = topicElement.Element("Labels");
            if (labelsWrapper != null && labelsWrapper.Elements("Label").Any())
            {
                foreach (XElement label in labelsWrapper.Elements("Label"))
                {
                    AddLabel(topic, (string)label);
                }
            }
            else
            {
                foreach (XElement label in topicElement.Elements("Labels"))
                {
                    AddLabel(topic, (string)label);
                }
            }

            foreach (XElement link in Collect(topicElement, "ReferenceLinks", "ReferenceLink"))
            {
                if (!string.IsNullOrWhiteSpace((string)link)) topic.ReferenceLinks.Add(((string)link).Trim());
            }

            foreach (XElement related in Collect(topicElement, "RelatedTopics", "RelatedTopic"))
            {
                Guid relatedGuid = ParseGuid((string)related.Attribute("Guid"));
                if (relatedGuid != Guid.Empty) topic.RelatedTopics.Add(relatedGuid);
            }

            ReadComments(topic, markup, topicElement, result);
            ReadViewpoints(topic, markup, topicElement, archive, entry, result);
            CheckVocabulary(topic, result);

            return topic;
        }

        private static void AddLabel(BcfTopic topic, string value)
        {
            if (!string.IsNullOrWhiteSpace(value)) topic.Labels.Add(value.Trim());
        }

        private static void ReadComments(BcfTopic topic, XElement markup, XElement topicElement, BcfReadResult result)
        {
            // 3.0: Topic/Comments/Comment. 2.1: Markup/Comment
            IEnumerable<XElement> comments = Collect(topicElement, "Comments", "Comment")
                .Concat(markup.Elements("Comment"));

            foreach (XElement element in comments)
            {
                var comment = new BcfComment
                {
                    Guid = ParseGuid((string)element.Attribute("Guid")),
                    Author = (string)element.Element("Author"),
                    Text = (string)element.Element("Comment"),
                    ModifiedAuthor = (string)element.Element("ModifiedAuthor")
                };

                comment.Date = ParseDate(element.Element("Date"), result) ?? default(DateTimeOffset);
                comment.ModifiedDate = ParseDate(element.Element("ModifiedDate"), result);

                XElement viewpointRef = element.Element("Viewpoint");
                if (viewpointRef != null)
                {
                    Guid viewpointGuid = ParseGuid((string)viewpointRef.Attribute("Guid"));
                    if (viewpointGuid != Guid.Empty) comment.ViewpointGuid = viewpointGuid;
                }

                topic.Comments.Add(comment);
            }
        }

        private static void ReadViewpoints(
            BcfTopic topic,
            XElement markup,
            XElement topicElement,
            ZipArchive archive,
            ZipArchiveEntry markupEntry,
            BcfReadResult result)
        {
            // 3.0: Topic/Viewpoints/ViewPoint. 2.1: Markup/Viewpoints
            IEnumerable<XElement> references = Collect(topicElement, "Viewpoints", "ViewPoint")
                .Concat(markup.Elements("Viewpoints"));

            string folder = FolderOf(markupEntry.FullName);

            foreach (XElement reference in references)
            {
                var viewpoint = new BcfViewpoint
                {
                    Guid = ParseGuid((string)reference.Attribute("Guid"))
                };

                string snapshot = (string)reference.Element("Snapshot");
                if (!string.IsNullOrWhiteSpace(snapshot)) viewpoint.SnapshotFileName = snapshot.Trim();

                int index;
                if (int.TryParse((string)reference.Element("Index"), NumberStyles.Integer, CultureInfo.InvariantCulture, out index))
                {
                    viewpoint.Index = index;
                }

                string fileName = (string)reference.Element("Viewpoint");
                ZipArchiveEntry viewpointEntry = string.IsNullOrWhiteSpace(fileName)
                    ? null
                    : Find(archive, folder + fileName.Trim());

                if (viewpointEntry != null)
                {
                    try
                    {
                        ReadVisualizationInfo(viewpoint, Load(viewpointEntry));
                    }
                    catch (Exception ex)
                    {
                        result.Warn("Точка зрения '" + viewpointEntry.FullName + "' прочитана частично: " + ex.Message);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(fileName))
                {
                    result.Warn("В архиве нет файла точки зрения '" + folder + fileName.Trim() + "'.");
                }

                topic.Viewpoints.Add(viewpoint);
            }
        }

        private static void ReadVisualizationInfo(BcfViewpoint viewpoint, XDocument document)
        {
            XElement root = document.Root;

            XElement perspective = root.Element("PerspectiveCamera");
            if (perspective != null)
            {
                viewpoint.Camera = new BcfPerspectiveCamera
                {
                    ViewPoint = ReadVector(perspective.Element("CameraViewPoint")),
                    Direction = ReadVector(perspective.Element("CameraDirection")),
                    UpVector = ReadVector(perspective.Element("CameraUpVector")),
                    FieldOfViewDegrees = ReadDouble(perspective.Element("FieldOfView")),
                    AspectRatio = ReadDouble(perspective.Element("AspectRatio"), 1.0)
                };
            }

            XElement orthogonal = root.Element("OrthogonalCamera");
            if (orthogonal != null)
            {
                viewpoint.Camera = new BcfOrthogonalCamera
                {
                    ViewPoint = ReadVector(orthogonal.Element("CameraViewPoint")),
                    Direction = ReadVector(orthogonal.Element("CameraDirection")),
                    UpVector = ReadVector(orthogonal.Element("CameraUpVector")),
                    ViewToWorldScale = ReadDouble(orthogonal.Element("ViewToWorldScale")),
                    AspectRatio = ReadDouble(orthogonal.Element("AspectRatio"), 1.0)
                };
            }

            XElement components = root.Element("Components");
            if (components != null)
            {
                foreach (XElement component in Collect(components, "Selection", "Component"))
                {
                    viewpoint.Selection.Add(ReadComponent(component));
                }

                XElement visibility = components.Element("Visibility");
                if (visibility != null)
                {
                    var model = new BcfVisibility
                    {
                        DefaultVisibility = ReadBool(visibility.Attribute("DefaultVisibility"))
                    };

                    foreach (XElement component in Collect(visibility, "Exceptions", "Component"))
                    {
                        model.Exceptions.Add(ReadComponent(component));
                    }

                    // Подсказки в 3.0 внутри Visibility, в 2.1 — на уровне Components
                    XElement hints = visibility.Element("ViewSetupHints") ?? components.Element("ViewSetupHints");
                    if (hints != null)
                    {
                        model.Hints = new BcfViewSetupHints
                        {
                            SpacesVisible = ReadBool(hints.Attribute("SpacesVisible")),
                            SpaceBoundariesVisible = ReadBool(hints.Attribute("SpaceBoundariesVisible")),
                            OpeningsVisible = ReadBool(hints.Attribute("OpeningsVisible"))
                        };
                    }

                    viewpoint.Visibility = model;
                }
            }

            foreach (XElement plane in Collect(root, "ClippingPlanes", "ClippingPlane"))
            {
                viewpoint.ClippingPlanes.Add(new BcfClippingPlane
                {
                    Location = ReadVector(plane.Element("Location")),
                    Direction = ReadVector(plane.Element("Direction"))
                });
            }
        }

        private static BcfComponent ReadComponent(XElement element)
        {
            return new BcfComponent
            {
                IfcGuid = (string)element.Attribute("IfcGuid"),
                OriginatingSystem = (string)element.Element("OriginatingSystem"),
                AuthoringToolId = (string)element.Element("AuthoringToolId")
            };
        }

        /// <summary>
        /// Отмечает значения вне справочника. Именно отмечает: топик остаётся
        /// как есть, значение не подменяется и не отбрасывается.
        /// </summary>
        private static void CheckVocabulary(BcfTopic topic, BcfReadResult result)
        {
            Check("TopicStatus", topic.TopicStatus, BcfVocabulary.IsKnownTopicStatus(topic.TopicStatus), topic, result);
            Check("TopicType", topic.TopicType, BcfVocabulary.IsKnownTopicType(topic.TopicType), topic, result);

            if (!string.IsNullOrWhiteSpace(topic.Priority))
            {
                Check("Priority", topic.Priority, BcfVocabulary.IsKnownPriority(topic.Priority), topic, result);
            }

            if (!string.IsNullOrWhiteSpace(topic.Stage))
            {
                Check("Stage", topic.Stage, BcfVocabulary.IsKnownStage(topic.Stage), topic, result);
            }

            foreach (string label in topic.Labels)
            {
                Check("Label", label, BcfVocabulary.IsKnownTopicLabel(label), topic, result);
            }
        }

        private static void Check(string field, string value, bool known, BcfTopic topic, BcfReadResult result)
        {
            if (known || string.IsNullOrWhiteSpace(value)) return;

            topic.ExternalValues[field] = value;
            result.AddExternalValue(field, value, topic.Guid);
        }

        private static IEnumerable<XElement> Collect(XElement parent, string wrapperName, string itemName)
        {
            XElement wrapper = parent.Element(wrapperName);

            return wrapper == null
                ? Enumerable.Empty<XElement>()
                : wrapper.Elements(itemName);
        }

        private static ZipArchiveEntry Find(ZipArchive archive, string entryName)
        {
            return archive.Entries.FirstOrDefault(
                e => string.Equals(e.FullName, entryName, StringComparison.OrdinalIgnoreCase));
        }

        private static string FolderOf(string entryName)
        {
            int slash = entryName.LastIndexOf('/');
            return slash < 0 ? string.Empty : entryName.Substring(0, slash + 1);
        }

        private static XDocument Load(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static Guid ParseGuid(string value)
        {
            Guid guid;
            return Guid.TryParse(value, out guid) ? guid : Guid.Empty;
        }

        private static DateTimeOffset? ParseDate(XElement element, BcfReadResult result)
        {
            if (element == null) return null;

            try
            {
                return BcfNumber.ParseDate(element.Value);
            }
            catch (Exception)
            {
                result.Warn("Дата '" + element.Value + "' в поле " + element.Name.LocalName + " не разобрана.");
                return null;
            }
        }

        private static Vector3 ReadVector(XElement element)
        {
            if (element == null) return Vector3.Zero;

            return new Vector3(
                ReadDouble(element.Element("X")),
                ReadDouble(element.Element("Y")),
                ReadDouble(element.Element("Z")));
        }

        private static double ReadDouble(XElement element, double fallback = 0.0)
        {
            if (element == null) return fallback;

            double value;
            return double.TryParse(element.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private static bool ReadBool(XAttribute attribute)
        {
            if (attribute == null) return false;

            bool value;
            return bool.TryParse(attribute.Value, out value) && value;
        }
    }
}
