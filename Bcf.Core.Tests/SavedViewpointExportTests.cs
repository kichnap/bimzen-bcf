using System;
using System.Collections.Generic;
using System.IO;
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
    /// Сохранённые виды как второй источник замечаний: то, что не описывается
    /// логикой коллизий — прибор повёрнут не той стороной, труба посреди
    /// помещения.
    /// </summary>
    public class SavedViewpointExportTests
    {
        [Fact]
        public void Viewpoints_BecomeTopics()
        {
            BcfExportSettings settings = Settings();
            var viewpoints = new FakeViewpointSource(View("v-1", "Прибор развёрнут"), View("v-2", "Труба в помещении"));

            BcfReadResult read = ReadBack(settings, viewpoints);

            Assert.Equal(2, read.Topics.Count);
            Assert.Contains(read.Topics, t => t.Title == "Прибор развёрнут");
            Assert.Contains(read.Topics, t => t.Title == "Труба в помещении");
        }

        [Fact]
        public void Viewpoints_AreCountedSeparately()
        {
            BcfExportResult result = Export(Settings(), new FakeViewpointSource(View("v-1", "Замечание")));

            Assert.Equal(1, result.TopicsCreated);
            Assert.Equal(1, result.ViewpointTopicsCreated);
            Assert.Equal(0, result.ClashesProcessed);
        }

        [Fact]
        public void Disabled_SkipsViewpointsEntirely()
        {
            BcfExportSettings settings = Settings();
            settings.IncludeSavedViewpoints = false;

            var viewpoints = new FakeViewpointSource(View("v-1", "Замечание"));

            BcfExportResult result = Export(settings, viewpoints);

            Assert.Equal(0, result.TopicsCreated);
            Assert.False(viewpoints.WasAsked);
        }

        [Fact]
        public void Selection_LimitsToChosenViewpoints()
        {
            BcfExportSettings settings = Settings();
            settings.SelectedViewpointIds = new List<string> { "v-2" };

            BcfReadResult read = ReadBack(settings, new FakeViewpointSource(View("v-1", "Первый"), View("v-2", "Второй")));

            Assert.Equal("Второй", read.Topics.Single().Title);
        }

        [Fact]
        public void EmptySelection_MeansAllViewpoints()
        {
            BcfExportSettings settings = Settings();
            settings.SelectedViewpointIds = new List<string>();

            BcfReadResult read = ReadBack(settings, new FakeViewpointSource(View("v-1", "Первый"), View("v-2", "Второй")));

            Assert.Equal(2, read.Topics.Count);
        }

        [Fact]
        public void TopicGuid_SurvivesRenameAndMove()
        {
            BcfExportSettings settings = Settings();

            SavedViewpointInfo before = View("v-1", "Прибор развёрнут");
            SavedViewpointInfo after = View("v-1", "Прибор развёрнут — уточнено");
            after.FolderPath = "Этаж 3 / ОВ";

            Guid first = ReadBack(settings, new FakeViewpointSource(before)).Topics.Single().Guid;
            Guid second = ReadBack(settings, new FakeViewpointSource(after)).Topics.Single().Guid;

            Assert.Equal(first, second);
        }

        [Fact]
        public void DifferentViewpoints_GetDifferentTopics()
        {
            BcfExportSettings settings = Settings();

            Guid first = ReadBack(settings, new FakeViewpointSource(View("v-1", "Замечание"))).Topics.Single().Guid;
            Guid second = ReadBack(settings, new FakeViewpointSource(View("v-2", "Замечание"))).Topics.Single().Guid;

            Assert.NotEqual(first, second);
        }

        [Fact]
        public void TopicType_ComesFromSettings()
        {
            BcfExportSettings settings = Settings();
            settings.SavedViewpointTopicType = BcfVocabulary.TopicTypes.Issue;

            BcfTopic topic = ReadBack(settings, new FakeViewpointSource(View("v-1", "Замечание"))).Topics.Single();

            Assert.Equal(BcfVocabulary.TopicTypes.Issue, topic.TopicType);
        }

        [Fact]
        public void AutoLabel_IsNotAppliedToManualNote()
        {
            BcfExportSettings settings = Settings();
            settings.Labels = new List<string> { BcfVocabulary.TopicLabels.Auto, BcfVocabulary.TopicLabels.Modeling };

            BcfTopic topic = ReadBack(settings, new FakeViewpointSource(View("v-1", "Замечание"))).Topics.Single();

            Assert.DoesNotContain(BcfVocabulary.TopicLabels.Auto, topic.Labels);
            Assert.Contains(BcfVocabulary.TopicLabels.Modeling, topic.Labels);
        }

        [Fact]
        public void Comments_AreCarriedOver()
        {
            BcfExportSettings settings = Settings();

            SavedViewpointInfo viewpoint = View("v-1", "Замечание");
            viewpoint.Comments.Add(new ClashCommentInfo
            {
                Author = "hvac@example.com",
                Text = "Развернуть прибор по проекту",
                Date = new DateTimeOffset(2026, 3, 1, 10, 0, 0, TimeSpan.Zero)
            });

            BcfTopic topic = ReadBack(settings, new FakeViewpointSource(viewpoint)).Topics.Single();

            Assert.Contains(topic.Comments, c => c.Text == "Развернуть прибор по проекту");
        }

        [Fact]
        public void Snapshot_IsWrittenWithTheTopic()
        {
            BcfExportSettings settings = Settings();
            settings.IncludeSnapshots = true;

            BcfTopic topic = ReadBack(settings, new FakeViewpointSource(View("v-1", "Замечание"))).Topics.Single();
            BcfViewpoint viewpoint = topic.Viewpoints.Single();

            // Читатель отдаёт имя файла снимка, а не байты: сам PNG лежит
            // в архиве рядом с markup
            Assert.NotNull(viewpoint.Camera);
            Assert.False(string.IsNullOrWhiteSpace(viewpoint.SnapshotFileName));
        }

        [Fact]
        public void Viewpoint_ShowsWholeModelByDefault()
        {
            BcfExportSettings settings = Settings();

            BcfViewpoint viewpoint = ReadBack(settings, new FakeViewpointSource(View("v-1", "Замечание")))
                .Topics.Single().Viewpoints.Single();

            Assert.NotNull(viewpoint.Visibility);
            Assert.True(viewpoint.Visibility.DefaultVisibility);
        }

        [Fact]
        public void BrokenViewpoint_DoesNotStopExport()
        {
            var viewpoints = new FakeViewpointSource(View("v-1", "Плохой"), View("v-2", "Хороший"))
            {
                FailFor = "v-1"
            };

            BcfExportResult result = Export(Settings(), viewpoints);

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.TopicsCreated);
            Assert.Contains(result.Warnings, w => w.Contains("Плохой"));
        }

        [Fact]
        public void UnreadableSource_WarnsInsteadOfFailing()
        {
            var viewpoints = new FakeViewpointSource { FailListing = true };

            BcfExportResult result = Export(Settings(), viewpoints);

            Assert.True(result.Succeeded);
            Assert.Equal(0, result.TopicsCreated);
            Assert.Contains(result.Warnings, w => w.Contains("The saved views were not read"));
        }

        [Fact]
        public void Progress_CountsViewpointsInTheTotal()
        {
            BcfExportSettings settings = Settings();
            var seen = new List<int[]>();

            var progress = new DelegateProgress(state => seen.Add(new[] { state.TotalClashes, state.ProcessedClashes }));

            using (var buffer = new MemoryStream())
            {
                new BcfClashExporter(new EmptyClashSource(), null, new FakeViewpointSource(View("v-1", "Первый"), View("v-2", "Второй")))
                    .Export(buffer, settings, progress);
            }

            // Индикатор не должен упираться в 100 % и стоять там,
            // пока снимаются самые долгие кадры — виды
            Assert.NotEmpty(seen);
            Assert.All(seen, state => Assert.Equal(2, state[0]));
            Assert.Equal(2, seen.Last()[1]);
        }

        [Fact]
        public void NoViewpointSource_ExportsClashesAsBefore()
        {
            BcfExportSettings settings = Settings();

            using (var buffer = new MemoryStream())
            {
                BcfExportResult result = new BcfClashExporter(new EmptyClashSource())
                    .Export(buffer, settings);

                Assert.True(result.Succeeded);
                Assert.Equal(0, result.ViewpointTopicsCreated);
            }
        }

        private static BcfExportSettings Settings()
        {
            return new BcfExportSettings
            {
                Author = "coordinator@example.com",
                ProjectName = "Тестовый проект",
                IncludedClashStatuses = new List<string> { "New", "Active" },
                IncludeSavedViewpoints = true,
                IncludeSnapshots = false,
                ExportTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)
            };
        }

        private static SavedViewpointInfo View(string id, string name)
        {
            return new SavedViewpointInfo { Id = id, Name = name };
        }

        private static BcfExportResult Export(BcfExportSettings settings, ISavedViewpointSource viewpoints)
        {
            using (var buffer = new MemoryStream())
            {
                return new BcfClashExporter(new EmptyClashSource(), null, viewpoints).Export(buffer, settings);
            }
        }

        private static BcfReadResult ReadBack(BcfExportSettings settings, ISavedViewpointSource viewpoints)
        {
            using (var buffer = new MemoryStream())
            {
                BcfExportResult result = new BcfClashExporter(new EmptyClashSource(), null, viewpoints)
                    .Export(buffer, settings);

                Assert.True(result.Succeeded, result.Error?.ToString());

                using (var reading = new MemoryStream(buffer.ToArray()))
                {
                    return BcfArchiveReader.Read(reading);
                }
            }
        }

        /// <summary>Документ без единой проверки: выгружаются только виды.</summary>
        private sealed class EmptyClashSource : IClashSource
        {
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
                return new List<ClashTestInfo>();
            }

            public IEnumerable<ClashItem> EnumerateClashes(ClashTestInfo test, CancellationToken cancellationToken)
            {
                return new List<ClashItem>();
            }

            public ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken)
            {
                return null;
            }
        }

        private sealed class FakeViewpointSource : ISavedViewpointSource
        {
            private readonly List<SavedViewpointInfo> _viewpoints;

            public FakeViewpointSource(params SavedViewpointInfo[] viewpoints)
            {
                _viewpoints = viewpoints.ToList();
            }

            /// <summary>Идентификатор вида, на котором источник «сломается».</summary>
            public string FailFor { get; set; }

            /// <summary>Ломать ли само чтение списка видов.</summary>
            public bool FailListing { get; set; }

            /// <summary>Спрашивали ли у источника список видов вообще.</summary>
            public bool WasAsked { get; private set; }

            public IReadOnlyList<SavedViewpointInfo> GetSavedViewpoints()
            {
                WasAsked = true;

                if (FailListing) throw new InvalidOperationException("дерево видов недоступно");

                return _viewpoints;
            }

            public ClashViewpointData CreateViewpoint(
                SavedViewpointInfo viewpoint, SnapshotRequest snapshot, CancellationToken cancellationToken)
            {
                if (FailFor != null && viewpoint.Id == FailFor)
                {
                    throw new InvalidOperationException("вид недоступен");
                }

                var data = new ClashViewpointData
                {
                    Camera = CameraConverter.ToPerspective(
                        new Vector3(1, 2, 3),
                        new Rotation(0, 0, 1, 0),
                        Math.PI / 4,
                        4.0 / 3.0,
                        LengthUnit.Meters)
                };

                if (snapshot.Enabled) data.Snapshot = TestData.FakePng();

                return data;
            }
        }

        private sealed class DelegateProgress : IProgress<BcfExportProgress>
        {
            private readonly Action<BcfExportProgress> _handler;

            public DelegateProgress(Action<BcfExportProgress> handler)
            {
                _handler = handler;
            }

            public void Report(BcfExportProgress value)
            {
                _handler(value);
            }
        }
    }
}
