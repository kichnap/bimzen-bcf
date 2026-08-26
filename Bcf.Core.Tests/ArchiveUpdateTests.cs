using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using Bcf.Core.Clash;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// Updating an existing archive.
    ///
    /// A file that has been through a receiving tool is not our draft: it holds
    /// foreign statuses, comments and attachments. A repeat export has to spare them.
    /// </summary>
    public class ArchiveUpdateTests
    {
        [Fact]
        public void Overwrite_IgnoresTheExistingFile()
        {
            byte[] existing = Existing(Topic("старое замечание"));
            BcfExportSettings settings = Settings(BcfUpdateMode.Overwrite);

            BcfReadResult read = Update(settings, existing, Clash("Этаж 3"));

            Assert.Single(read.Topics);
            Assert.DoesNotContain(read.Topics, t => t.Title == "старое замечание");
        }

        [Fact]
        public void AppendNew_KeepsWhatWasThere()
        {
            byte[] existing = Existing(Topic("старое замечание"));
            BcfExportSettings settings = Settings(BcfUpdateMode.AppendNew);

            BcfReadResult read = Update(settings, existing, Clash("Этаж 3"));

            Assert.Equal(2, read.Topics.Count);
            Assert.Contains(read.Topics, t => t.Title == "старое замечание");
        }

        [Fact]
        public void AppendNew_DoesNotTouchOurOwnTopic()
        {
            BcfExportSettings settings = Settings(BcfUpdateMode.AppendNew);
            ClashItem clash = Clash("Этаж 3");

            // The first export creates the file, the second goes over it
            byte[] first = Export(Settings(BcfUpdateMode.Overwrite), clash);

            BcfTopic before = Read(first).Topics.Single();
            byte[] withComment = WithReceiverComment(first, before.Guid, "закрыто на совещании");

            BcfExportResult result = Run(settings, withComment, out BcfReadResult read, clash);

            Assert.Equal(1, result.TopicsKept);
            Assert.Equal(0, result.TopicsUpdated);
            Assert.Contains(read.Topics.Single().Comments, c => c.Text == "закрыто на совещании");
        }

        [Fact]
        public void UpdateAndAppend_RewritesOurTopicAndKeepsReceiverComments()
        {
            ClashItem clash = Clash("Этаж 3");
            byte[] first = Export(Settings(BcfUpdateMode.Overwrite), clash);

            BcfTopic before = Read(first).Topics.Single();
            byte[] withComment = WithReceiverComment(first, before.Guid, "разобрать с ОВ");

            BcfExportResult result = Run(Settings(BcfUpdateMode.UpdateAndAppend), withComment, out BcfReadResult read, clash);

            BcfTopic after = read.Topics.Single();

            Assert.Equal(1, result.TopicsUpdated);
            Assert.Equal(0, result.TopicsKept);
            Assert.Contains(after.Comments, c => c.Text == "разобрать с ОВ");
            Assert.NotNull(after.ModifiedDate);
        }

        [Fact]
        public void UpdateAndAppend_KeepsReceiverStatusByDefault()
        {
            ClashItem clash = Clash("Этаж 3");
            byte[] first = Export(Settings(BcfUpdateMode.Overwrite), clash);

            BcfTopic before = Read(first).Topics.Single();
            byte[] closed = WithStatus(first, before.Guid, BcfVocabulary.TopicStatuses.Closed);

            Run(Settings(BcfUpdateMode.UpdateAndAppend), closed, out BcfReadResult read, clash);

            Assert.Equal(BcfVocabulary.TopicStatuses.Closed, read.Topics.Single().TopicStatus);
        }

        [Fact]
        public void UpdateAndAppend_TakesStatusFromNavisworksWhenAsked()
        {
            ClashItem clash = Clash("Этаж 3");
            byte[] first = Export(Settings(BcfUpdateMode.Overwrite), clash);

            BcfTopic before = Read(first).Topics.Single();
            byte[] closed = WithStatus(first, before.Guid, BcfVocabulary.TopicStatuses.Closed);

            BcfExportSettings settings = Settings(BcfUpdateMode.UpdateAndAppend);
            settings.KeepReceiverChanges = false;

            Run(settings, closed, out BcfReadResult read, clash);

            Assert.Equal(before.TopicStatus, read.Topics.Single().TopicStatus);
        }

        [Fact]
        public void UpdateAndAppend_DoesNotRewriteTopicsWithDataWeDoNotKeep()
        {
            ClashItem clash = Clash("Этаж 3");
            byte[] first = Export(Settings(BcfUpdateMode.Overwrite), clash);

            BcfTopic before = Read(first).Topics.Single();
            byte[] withDocument = WithDocumentReference(first, before.Guid);

            BcfExportResult result = Run(Settings(BcfUpdateMode.UpdateAndAppend), withDocument, out BcfReadResult read, clash);

            Assert.Equal(1, result.TopicsKept);
            Assert.Equal(0, result.TopicsUpdated);
            Assert.Contains(result.Warnings, w => w.Contains("DocumentReferences"));
        }

        [Fact]
        public void ForeignEntries_SurviveTheUpdate()
        {
            byte[] existing = WithExtraEntry(Existing(Topic("старое замечание")), "documents/spec.txt", "чужой файл");

            Run(Settings(BcfUpdateMode.AppendNew), existing, out BcfReadResult _, Clash("Этаж 3"));

            byte[] updated = LastArchive;

            Assert.Equal("чужой файл", ReadEntry(updated, "documents/spec.txt"));
        }

        [Fact]
        public void ForeignStatus_IsDeclaredInExtensions()
        {
            byte[] existing = Existing(Topic("замечание из BIMcollab", status: "На согласовании"));

            Run(Settings(BcfUpdateMode.AppendNew), existing, out BcfReadResult _, Clash("Этаж 3"));

            string extensions = ReadEntry(LastArchive, "extensions.xml");

            // The file we write has to declare everything it holds
            Assert.Contains("На согласовании", extensions);
        }

        [Fact]
        public void VersionMismatch_StopsBeforeWritingAnything()
        {
            BcfExportSettings settings = Settings(BcfUpdateMode.AppendNew);
            settings.Version = BcfVersion.Bcf21;

            byte[] existing = Existing(Topic("старое замечание"));

            BcfExportResult result = Run(settings, existing, out BcfReadResult _, Clash("Этаж 3"));

            Assert.False(result.Succeeded);
            Assert.Contains("2.1", result.Error.Message);
        }

        [Fact]
        public void TopicsNotInTheExportStayInTheFile()
        {
            // The clash is resolved and no longer appears in the test: the topic
            // about it must stay in the file rather than vanish
            byte[] existing = Existing(Topic("разобранная коллизия"));

            BcfExportResult result = Run(Settings(BcfUpdateMode.AppendNew), existing, out BcfReadResult read, Clash("Этаж 4"));

            Assert.Equal(1, result.TopicsKept);
            Assert.Contains(read.Topics, t => t.Title == "разобранная коллизия");
        }

        [Fact]
        public void UpdatedTopic_KeepsItsCreationDateAndServerId()
        {
            ClashItem clash = Clash("Этаж 3");

            BcfExportSettings first = Settings(BcfUpdateMode.Overwrite);
            first.ExportTime = new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

            byte[] created = Export(first, clash);
            BcfTopic before = Read(created).Topics.Single();

            BcfExportSettings second = Settings(BcfUpdateMode.UpdateAndAppend);
            second.ExportTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

            Run(second, created, out BcfReadResult read, clash);

            Assert.Equal(before.CreationDate, read.Topics.Single().CreationDate);
        }

        // --- helpers ----------------------------------------------------------

        /// <summary>The bytes of the last archive written — for entry-level checks.</summary>
        private byte[] LastArchive { get; set; }

        private static BcfExportSettings Settings(BcfUpdateMode mode)
        {
            return new BcfExportSettings
            {
                Author = "coordinator@example.com",
                ProjectName = "Тестовый проект",
                IncludedClashStatuses = new List<string> { "New", "Active" },
                IncludeSnapshots = false,
                UpdateMode = mode,
                ExportTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)
            };
        }

        private BcfReadResult Update(BcfExportSettings settings, byte[] existing, params ClashItem[] clashes)
        {
            BcfReadResult read;
            Run(settings, existing, out read, clashes);

            return read;
        }

        private BcfExportResult Run(
            BcfExportSettings settings, byte[] existing, out BcfReadResult read, params ClashItem[] clashes)
        {
            using (var destination = new MemoryStream())
            using (var source = new MemoryStream(existing))
            {
                BcfExportResult result = new BcfClashExporter(new FakeSource(clashes))
                    .Export(destination, source, settings);

                LastArchive = destination.ToArray();

                read = result.Succeeded ? Read(LastArchive) : new BcfReadResult();

                return result;
            }
        }

        private static byte[] Export(BcfExportSettings settings, params ClashItem[] clashes)
        {
            using (var destination = new MemoryStream())
            {
                BcfExportResult result = new BcfClashExporter(new FakeSource(clashes)).Export(destination, settings);
                Assert.True(result.Succeeded, result.Error?.ToString());

                return destination.ToArray();
            }
        }

        private static BcfReadResult Read(byte[] archive)
        {
            using (var stream = new MemoryStream(archive))
            {
                return BcfArchiveReader.Read(stream);
            }
        }

        /// <summary>An archive with one foreign topic, the way a receiving tool would hand it over.</summary>
        private static byte[] Existing(BcfTopic topic)
        {
            using (var buffer = new MemoryStream())
            {
                using (BcfArchiveWriter writer = BcfArchiveWriter.Create(buffer, new BcfWriteOptions
                {
                    Version = BcfVersion.Bcf30,
                    Author = "receiver@example.com",
                    Project = new BcfProject { Name = "Тестовый проект", ProjectId = Guid.NewGuid().ToString("D") },

                    // Foreign statuses are an everyday thing: the standard does not fix the vocabularies
                    ValidateVocabulary = false
                }))
                {
                    writer.WriteTopic(topic);
                    writer.Complete();
                }

                return buffer.ToArray();
            }
        }

        private static BcfTopic Topic(string title, string status = null)
        {
            return new BcfTopic
            {
                Guid = Guid.NewGuid(),
                Title = title,
                TopicType = BcfVocabulary.TopicTypes.Clash,
                TopicStatus = status ?? BcfVocabulary.TopicStatuses.Default,
                CreationAuthor = "receiver@example.com",
                CreationDate = new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero)
            };
        }

        private static ClashItem Clash(string group)
        {
            var clash = new ClashItem
            {
                TestId = "test-1",
                TestName = "ОВ vs КР",
                GroupName = group,
                LevelName = group,
                DisplayName = "Столкновение 1",
                Status = "New",
                DistanceMeters = 0.125,
                CenterMeters = new Vector3(1.5, 2.5, 3.5)
            };

            clash.Elements.Add(new ClashElementInfo
            {
                IfcGuid = "2SugUv4EX5LAhcVpDp2dUH",
                ElementId = "123456",
                ModelFileName = "ОВ.nwc",
                Origin = ElementIdOrigin.RevitUniqueId
            });

            return clash;
        }

        /// <summary>A comment left in a receiving tool.</summary>
        private static byte[] WithReceiverComment(byte[] archive, Guid topicGuid, string text)
        {
            return Rewrite(archive, topicGuid, markup =>
            {
                string comment =
                    "<Comments><Comment Guid=\"" + Guid.NewGuid().ToString("D") + "\">" +
                    "<Date>2026-02-20T10:00:00Z</Date>" +
                    "<Author>receiver@example.com</Author>" +
                    "<Comment>" + text + "</Comment>" +
                    "</Comment></Comments>";

                return markup.Replace("</Topic>", comment + "</Topic>");
            });
        }

        private static byte[] WithStatus(byte[] archive, Guid topicGuid, string status)
        {
            return Rewrite(archive, topicGuid, markup =>
            {
                int start = markup.IndexOf("TopicStatus=\"", StringComparison.Ordinal) + "TopicStatus=\"".Length;
                int end = markup.IndexOf('"', start);

                return markup.Substring(0, start) + status + markup.Substring(end);
            });
        }

        /// <summary>A document reference — the kind of thing our model does not keep.</summary>
        private static byte[] WithDocumentReference(byte[] archive, Guid topicGuid)
        {
            return Rewrite(archive, topicGuid, markup => markup.Replace(
                "</Topic>",
                "<DocumentReferences><DocumentReference Guid=\"" + Guid.NewGuid().ToString("D") + "\">" +
                "<Url>https://example.com/spec.pdf</Url></DocumentReference></DocumentReferences></Topic>"));
        }

        private static byte[] Rewrite(byte[] archive, Guid topicGuid, Func<string, string> edit)
        {
            string entryName = BcfEntryNames.MarkupEntry(topicGuid);

            var copy = new MemoryStream();
            copy.Write(archive, 0, archive.Length);
            copy.Position = 0;

            using (var zip = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
            {
                ZipArchiveEntry entry = zip.GetEntry(entryName);
                Assert.NotNull(entry);

                string markup;
                using (var reader = new StreamReader(entry.Open()))
                {
                    markup = reader.ReadToEnd();
                }

                string updated = edit(markup);

                entry.Delete();
                ZipArchiveEntry replacement = zip.CreateEntry(entryName);

                using (var writer = new StreamWriter(replacement.Open()))
                {
                    writer.Write(updated);
                }
            }

            return copy.ToArray();
        }

        private static byte[] WithExtraEntry(byte[] archive, string entryName, string content)
        {
            var copy = new MemoryStream();
            copy.Write(archive, 0, archive.Length);
            copy.Position = 0;

            using (var zip = new ZipArchive(copy, ZipArchiveMode.Update, leaveOpen: true))
            using (var writer = new StreamWriter(zip.CreateEntry(entryName).Open()))
            {
                writer.Write(content);
            }

            return copy.ToArray();
        }

        private static string ReadEntry(byte[] archive, string entryName)
        {
            using (var stream = new MemoryStream(archive))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                ZipArchiveEntry entry = zip.GetEntry(entryName);
                Assert.NotNull(entry);

                using (var reader = new StreamReader(entry.Open()))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private sealed class FakeSource : IClashSource
        {
            private readonly List<ClashItem> _clashes;

            public FakeSource(params ClashItem[] clashes)
            {
                _clashes = clashes.ToList();
            }

            public ClashDocumentInfo GetDocument()
            {
                var document = new ClashDocumentInfo
                {
                    Title = "Тестовый документ",
                    FilePath = @"C:\проекты\тест.nwf",
                    Units = LengthUnit.Meters
                };

                document.Models.Add(new ClashModelInfo { FileName = "ОВ.nwc" });

                return document;
            }

            public IReadOnlyList<ClashTestInfo> GetTests()
            {
                return new[]
                {
                    new ClashTestInfo { Id = "test-1", Name = "ОВ vs КР", Index = 0, ClashCount = _clashes.Count }
                };
            }

            public IEnumerable<ClashItem> EnumerateClashes(ClashTestInfo test, CancellationToken cancellationToken)
            {
                return _clashes;
            }

            public ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken)
            {
                return new ClashViewpointData
                {
                    Camera = CameraConverter.ToPerspective(
                        new Vector3(1, 2, 3),
                        new Rotation(0, 0, 1, 0),
                        Math.PI / 4,
                        4.0 / 3.0,
                        LengthUnit.Meters)
                };
            }
        }
    }
}
