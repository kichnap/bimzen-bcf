using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using Bcf.Core;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;
using Bcf.Vocabulary.Generator;

namespace Bcf.Core.Tests
{
    /// <summary>Data and schema checks shared by the serialization tests.</summary>
    internal static class TestData
    {
        public static string RepositoryRoot
        {
            get { return RepositoryPaths.FindRoot(AppContext.BaseDirectory); }
        }

        public static string SchemaPath(BcfVersion version, string fileName)
        {
            return Path.Combine(RepositoryRoot, "schemas", version.ToVersionId(), fileName);
        }

        public static BcfWriteOptions Options(BcfVersion version)
        {
            return new BcfWriteOptions
            {
                Version = version,
                Author = "coordinator@example.com",
                Project = new BcfProject
                {
                    ProjectId = "8a1c2d3e-4f56-4789-8abc-def012345678",
                    Name = "Тестовый проект"
                }
            };
        }

        /// <summary>A topic with everything a host really writes out of Clash Detective.</summary>
        public static BcfTopic Topic(int number = 1)
        {
            Guid topicGuid = StableTopicKey.ToTopicGuid(
                StableTopicKey.ForClash("ОВ vs КР", new[] { "2SugUv4EX5LAhcVpDp2dUH", "3woirKUVTF1wlWXy6aBfQJ" + number }));

            var topic = new BcfTopic
            {
                Guid = topicGuid,
                TopicType = BcfVocabulary.TopicTypes.Clash,
                TopicStatus = BcfVocabulary.TopicStatuses.New,
                Title = "Коллизия " + number + " — ОВ vs КР",
                Priority = BcfVocabulary.Priorities.Normal,
                Stage = BcfVocabulary.Stages.Default,
                CreationDate = new DateTimeOffset(2026, 8, 18, 10, 30, 0, TimeSpan.FromHours(3)),
                CreationAuthor = "coordinator@example.com",
                AssignedTo = "hvac@example.com",
                Description = "Жёсткая коллизия, глубина проникновения 0.125 м, уровень «Этаж 3»"
            };

            topic.Labels.Add(BcfVocabulary.TopicLabels.Auto);
            topic.Labels.Add(BcfVocabulary.TopicLabels.HVAC);

            topic.Files.Add(new BcfFile
            {
                Filename = "ОВ_модель.nwc",
                Date = new DateTimeOffset(2026, 8, 17, 9, 0, 0, TimeSpan.FromHours(3)),
                IsExternal = true
            });

            topic.Comments.Add(new BcfComment
            {
                Guid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
                Date = new DateTimeOffset(2026, 8, 18, 11, 0, 0, TimeSpan.FromHours(3)),
                Author = "coordinator@example.com",
                Text = "Требуется перетрассировка воздуховода"
            });

            topic.Viewpoints.Add(Viewpoint());

            return topic;
        }

        public static BcfViewpoint Viewpoint()
        {
            var viewpoint = new BcfViewpoint
            {
                Guid = Guid.Parse("12345678-90ab-cdef-1234-567890abcdef"),
                Camera = CameraConverter.ToPerspective(
                    new Vector3(10, 20, 30),
                    Rotation.FromAxisAngle(new Vector3(1, 0, 0), Math.PI / 2),
                    Math.PI / 3,
                    16.0 / 9.0,
                    LengthUnit.Feet),
                Index = 0,
                Snapshot = FakePng()
            };

            viewpoint.Selection.Add(new BcfComponent("2SugUv4EX5LAhcVpDp2dUH")
            {
                OriginatingSystem = "Navisworks",
                AuthoringToolId = "123456"
            });
            viewpoint.Selection.Add(new BcfComponent("3woirKUVTF1wlWXy6aBfQJ"));

            var visibility = new BcfVisibility { DefaultVisibility = true };
            visibility.Exceptions.Add(new BcfComponent("1$1j4xEDn78A9oA4mCPCkL"));
            viewpoint.Visibility = visibility;

            viewpoint.ClippingPlanes.Add(new BcfClippingPlane
            {
                Location = new Vector3(0, 0, 3.5),
                Direction = new Vector3(0, 0, -1)
            });

            return viewpoint;
        }

        /// <summary>The eight header bytes of a PNG: the content of a snapshot does not matter here.</summary>
        public static byte[] FakePng()
        {
            return new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        }

        /// <summary>Writes an archive into memory.</summary>
        public static byte[] WriteArchive(BcfVersion version, params BcfTopic[] topics)
        {
            BcfWriteReport ignored;
            return WriteArchive(Options(version), out ignored, topics);
        }

        public static byte[] WriteArchive(BcfWriteOptions options, out BcfWriteReport report, params BcfTopic[] topics)
        {
            using (var buffer = new MemoryStream())
            {
                using (BcfArchiveWriter writer = BcfArchiveWriter.Create(buffer, options))
                {
                    foreach (BcfTopic topic in topics)
                    {
                        writer.WriteTopic(topic);
                    }

                    writer.Complete();
                    report = writer.Report;
                }

                return buffer.ToArray();
            }
        }

        public static IReadOnlyList<string> EntryNames(byte[] archive)
        {
            using (var buffer = new MemoryStream(archive))
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Read))
            {
                return zip.Entries.Select(e => e.FullName).ToList();
            }
        }

        public static byte[] EntryBytes(byte[] archive, string entryName)
        {
            using (var buffer = new MemoryStream(archive))
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Read))
            {
                ZipArchiveEntry entry = zip.Entries.FirstOrDefault(
                    e => string.Equals(e.FullName, entryName, StringComparison.OrdinalIgnoreCase));

                if (entry == null)
                {
                    throw new InvalidOperationException(
                        "В архиве нет записи '" + entryName + "'. Есть: " + string.Join(", ", zip.Entries.Select(e => e.FullName)));
                }

                using (Stream stream = entry.Open())
                using (var target = new MemoryStream())
                {
                    stream.CopyTo(target);
                    return target.ToArray();
                }
            }
        }

        public static string EntryText(byte[] archive, string entryName)
        {
            return new UTF8Encoding(false).GetString(EntryBytes(archive, entryName));
        }

        /// <summary>Unpacks an archive into a temporary folder — for checking against schemas with redefine.</summary>
        public static string Extract(byte[] archive)
        {
            string directory = Path.Combine(Path.GetTempPath(), "bcf-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            using (var buffer = new MemoryStream(archive))
            using (var zip = new ZipArchive(buffer, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    string path = Path.Combine(directory, entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));

                    using (Stream source = entry.Open())
                    using (FileStream target = File.Create(path))
                    {
                        source.CopyTo(target);
                    }
                }
            }

            return directory;
        }

        /// <summary>
        /// Validates XML against a schema. A resolver is required: the schemas pull
        /// each other in through include and redefine, and by default .NET does not
        /// resolve external references.
        /// </summary>
        public static IReadOnlyList<string> Validate(string xml, string schemaPath)
        {
            var errors = new List<string>();

            var schemas = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
            schemas.ValidationEventHandler += (s, e) => errors.Add(e.Message);
            schemas.Add(null, schemaPath);

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemas,
                XmlResolver = new XmlUrlResolver()
            };
            settings.ValidationEventHandler += (s, e) => errors.Add(e.Message);

            using (var reader = XmlReader.Create(new StringReader(xml), settings))
            {
                while (reader.Read()) { }
            }

            return errors;
        }
    }
}
