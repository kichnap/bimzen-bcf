using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Bcf.Core;
using Bcf.Core.Clash;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;
using Xunit;

namespace Bcf.Core.Tests
{
    public class ClashExporterTests
    {
        [Fact]
        public void GroupMode_MakesOneTopicPerGroup()
        {
            var source = new FakeClashSource(
                Clash("Этаж 3", "New"),
                Clash("Этаж 3", "New"),
                Clash("Этаж 4", "Active"));

            BcfExportResult result = Export(source, Settings());

            Assert.True(result.Succeeded);
            Assert.Equal(2, result.TopicsCreated);
            Assert.Equal(3, result.ClashesProcessed);
        }

        [Fact]
        public void ClashMode_MakesOneTopicPerClash()
        {
            var source = new FakeClashSource(
                Clash("Этаж 3", "New"),
                Clash("Этаж 3", "New"),
                Clash("Этаж 4", "Active"));

            BcfExportSettings settings = Settings();
            settings.Grouping = ClashGroupingMode.ClashPerTopic;

            BcfExportResult result = Export(source, settings);

            Assert.Equal(3, result.TopicsCreated);
        }

        [Fact]
        public void StatusFilter_SkipsWhatWasNotAsked()
        {
            var source = new FakeClashSource(
                Clash("Этаж 3", "New"),
                Clash("Этаж 3", "Approved"),
                Clash("Этаж 4", "Resolved"));

            BcfExportResult result = Export(source, Settings());

            Assert.Equal(1, result.ClashesProcessed);
            Assert.Equal(2, result.ClashesSkippedByStatus);
        }

        [Fact]
        public void ApprovedMapsToClosed_ByDefault_AndToRejectedWhenOverridden()
        {
            var source = new FakeClashSource(Clash("Этаж 3", "Approved"));

            BcfExportSettings settings = Settings();
            settings.IncludedClashStatuses = new List<string> { "Approved" };

            Assert.Equal(BcfVocabulary.TopicStatuses.Closed, StatusOfSingleTopic(source, settings));

            settings.StatusMapping["Approved"] = BcfVocabulary.TopicStatuses.Rejected;

            Assert.Equal(BcfVocabulary.TopicStatuses.Rejected, StatusOfSingleTopic(source, settings));
        }

        [Fact]
        public void GroupStatus_IsTheMostOpenOne()
        {
            // Группа закрыта только тогда, когда закрыта целиком: иначе
            // незакрытая часть работы пропадёт из виду координатора
            var source = new FakeClashSource(
                Clash("Этаж 3", "Approved"),
                Clash("Этаж 3", "New"));

            BcfExportSettings settings = Settings();
            settings.IncludedClashStatuses = new List<string> { "New", "Approved" };

            Assert.Equal(BcfVocabulary.TopicStatuses.New, StatusOfSingleTopic(source, settings));
        }

        [Fact]
        public void RepeatedExport_KeepsTopicGuids()
        {
            // Повторная выгрузка того же набора не должна плодить дубли:
            // идентификаторы коллизий Navisworks пересоздаются при Reset теста,
            // поэтому топик привязан к устойчивому ключу, а не к ним
            var source = new FakeClashSource(Clash("Этаж 3", "New"), Clash("Этаж 4", "New"));

            IReadOnlyList<Guid> first = TopicGuids(source, Settings());
            IReadOnlyList<Guid> second = TopicGuids(source, Settings());

            Assert.Equal(first, second);
            Assert.Equal(2, first.Count);
        }

        [Fact]
        public void ElementsWithoutIdentifier_AreCounted()
        {
            ClashItem clash = Clash("Этаж 3", "New");
            clash.Elements[1].IfcGuid = null;
            clash.Elements[1].Origin = ElementIdOrigin.None;

            BcfExportResult result = Export(new FakeClashSource(clash), Settings());

            Assert.Equal(1, result.ElementsWithoutGuid);
        }

        [Fact]
        public void Cancellation_StopsWithoutException()
        {
            var source = new FakeClashSource(Enumerable.Range(0, 50).Select(i => Clash("Этаж " + i, "New")).ToArray());

            using (var cts = new CancellationTokenSource())
            {
                var progress = new Progress<BcfExportProgress>(p =>
                {
                    if (p.ProcessedClashes >= 25) cts.Cancel();
                });

                BcfExportResult result;
                using (var buffer = new MemoryStream())
                {
                    result = new BcfClashExporter(source).Export(buffer, Settings(), progress, cts.Token);
                }

                Assert.True(result.Cancelled);
                Assert.False(result.Succeeded);
                Assert.Null(result.Error);
            }
        }

        [Fact]
        public void Progress_IsReported()
        {
            var source = new FakeClashSource(Enumerable.Range(0, 60).Select(i => Clash("Этаж " + i, "New")).ToArray());

            var seen = new List<double>();

            // Штатный Progress<T> доставляет вызовы через контекст синхронизации,
            // то есть в другом потоке и когда придётся: список успевал остаться
            // пустым к моменту проверки. Экспорт синхронный — приёмник тоже
            var progress = new ImmediateProgress(p => seen.Add(p.Fraction));

            Export(source, Settings(), progress);

            Assert.NotEmpty(seen);
            Assert.All(seen, f => Assert.InRange(f, 0.0, 1.0));
        }

        [Fact]
        public void DisciplineLabel_OnlyWhenRuleMatches()
        {
            var source = new FakeClashSource(Clash("Этаж 3", "New"));

            BcfExportSettings settings = Settings();
            BcfTopic withoutRules = SingleTopic(source, settings);
            Assert.Equal(new[] { BcfVocabulary.TopicLabels.Auto }, withoutRules.Labels);

            settings.DisciplineLabelRules.Add(new DisciplineLabelRule("ОВ", BcfVocabulary.TopicLabels.HVAC));
            BcfTopic withRule = SingleTopic(source, settings);

            Assert.Contains(BcfVocabulary.TopicLabels.HVAC, withRule.Labels);
        }

        [Fact]
        public void Description_CarriesDistanceAndCoordinatesInInvariantForm()
        {
            BcfTopic topic = SingleTopic(new FakeClashSource(Clash("Этаж 3", "New")), Settings());

            Assert.Contains("Расстояние: 0.125 м", topic.Description, StringComparison.Ordinal);
            Assert.Contains("X=1.5", topic.Description, StringComparison.Ordinal);
            Assert.Contains("Уровень: Этаж 3", topic.Description, StringComparison.Ordinal);
        }

        [Fact]
        public void Snapshots_RespectTheLimit()
        {
            var source = new FakeClashSource(
                Clash("Этаж 1", "New"), Clash("Этаж 2", "New"), Clash("Этаж 3", "New"));

            BcfExportSettings settings = Settings();
            settings.MaxSnapshots = 2;

            BcfExportResult result = Export(source, settings);

            Assert.Equal(3, result.TopicsCreated);
            Assert.Equal(2, result.SnapshotsCaptured);
        }

        [Fact]
        public void ExportedArchive_IsValidAndReadable()
        {
            var source = new FakeClashSource(Clash("Этаж 3", "New"), Clash("Этаж 4", "Active"));

            byte[] archive;
            using (var buffer = new MemoryStream())
            {
                BcfExportResult result = new BcfClashExporter(source).Export(buffer, Settings());
                Assert.True(result.Succeeded, result.Error?.ToString());
                archive = buffer.ToArray();
            }

            BcfReadResult read;
            using (var buffer = new MemoryStream(archive))
            {
                read = BcfArchiveReader.Read(buffer);
            }

            Assert.Equal(2, read.Topics.Count);
            Assert.Empty(read.ExternalValues);

            foreach (BcfTopic topic in read.Topics)
            {
                string markup = TestData.EntryText(archive, BcfEntryNames.MarkupEntry(topic.Guid));
                Assert.Empty(TestData.Validate(markup, TestData.SchemaPath(BcfVersion.Bcf30, "markup.xsd")));
            }
        }

        [Fact]
        public void SourceFailureOnOneClash_DoesNotStopExport()
        {
            var source = new FakeClashSource(Clash("Этаж 3", "New"), Clash("Этаж 4", "New"))
            {
                FailViewpointFor = "Этаж 3"
            };

            BcfExportResult result = Export(source, Settings());

            // Топик всё равно создаётся — без точки зрения, но с данными
            Assert.Equal(2, result.TopicsCreated);
            Assert.Contains(result.Warnings, w => w.Contains("не получена"));
        }

        [Fact]
        public void Bcf21Export_CarriesWriterWarningsIntoResult()
        {
            var source = new FakeClashSource(Clash("Этаж 3", "New"));

            BcfExportSettings settings = Settings();
            settings.Version = BcfVersion.Bcf21;

            BcfExportResult result = Export(source, settings);

            Assert.NotNull(result.WriteReport);
            Assert.Contains("AspectRatio", result.WriteReport.DroppedFields);
            Assert.Contains(result.Warnings, w => w.Contains("AspectRatio"));
        }

        [Fact]
        public void PreviouslyIssuedGuid_WinsOverDeterministicOne()
        {
            // Так выглядит топик, идентификатор которому выдал сервер:
            // повторная выгрузка обязана попасть в него, а не создать рядом второй
            var source = new FakeClashSource(Clash("Этаж 3", "New"));

            var map = new TopicGuidMap();
            Guid serverGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
            map.Remember(StableTopicKey.ForGroup("ОВ vs КР", "Этаж 3"), serverGuid);

            BcfReadResult read = ReadBack(source, Settings(), map);

            Assert.Equal(serverGuid, read.Topics.Single().Guid);
        }

        [Fact]
        public void SecondExport_ReusesGuidsFromMap()
        {
            var source = new FakeClashSource(Clash("Этаж 3", "New"), Clash("Этаж 4", "New"));
            var map = new TopicGuidMap();

            BcfExportResult first = Export(source, Settings(), map: map);
            BcfExportResult second = Export(source, Settings(), map: map);

            Assert.Equal(0, first.TopicsReused);
            Assert.Equal(2, second.TopicsReused);
            Assert.Equal(2, map.Count);
        }

        [Fact]
        public void MapSurvivesRestart_AndKeepsTopicsStable()
        {
            // Между выгрузками Navisworks закрывают: карта уходит на диск
            // и возвращается оттуда, идентификаторы не должны поменяться
            var source = new FakeClashSource(Clash("Этаж 3", "New"));
            var map = new TopicGuidMap();

            IReadOnlyList<Guid> before = ReadBack(source, Settings(), map).Topics.Select(t => t.Guid).ToList();

            TopicGuidMap restored;
            using (var buffer = new MemoryStream())
            {
                map.Write(buffer);
                using (var reading = new MemoryStream(buffer.ToArray()))
                {
                    restored = TopicGuidMap.Read(reading);
                }
            }

            IReadOnlyList<Guid> after = ReadBack(source, Settings(), restored).Topics.Select(t => t.Guid).ToList();

            Assert.Equal(before, after);
        }

        [Fact]
        public void SnapshotSettings_ReachTheSource()
        {
            // Режим съёмки и всё, что к нему прилагается, задаёт пользователь
            // в диалоге; источник обязан получить их без изменений
            var source = new FakeClashSource(Clash("Этаж 3", "New"));

            BcfExportSettings settings = Settings();
            settings.SnapshotMode = SnapshotMode.Native;
            settings.SnapshotIsolation = SnapshotIsolation.HideOthers;
            settings.SnapshotBoxMarginMeters = 4.5;
            settings.SnapshotTimeBudgetSeconds = 12;
            settings.SnapshotIncludeOverlay = false;

            Export(source, settings);

            Assert.Equal(SnapshotMode.Native, source.LastRequest.Mode);
            Assert.Equal(SnapshotIsolation.HideOthers, source.LastRequest.Isolation);
            Assert.Equal(4.5, source.LastRequest.BoxMarginMeters);
            Assert.Equal(12, source.LastRequest.TimeBudgetSeconds);
            Assert.False(source.LastRequest.IncludeOverlay);
        }

        [Fact]
        public void EmptySnapshots_AreCountedSeparately()
        {
            // «Снято 51» при 51 пустом кадре — это не отчёт, а дезинформация:
            // ровно так выглядела первая выгрузка на реальной модели
            var source = new FakeClashSource(Clash("Этаж 3", "New"), Clash("Этаж 4", "New"))
            {
                ReportEmptySnapshots = true
            };

            BcfExportResult result = Export(source, Settings());

            Assert.Equal(2, result.SnapshotsCaptured);
            Assert.Equal(2, result.SnapshotsEmpty);
        }

        private static BcfExportSettings Settings()
        {
            return new BcfExportSettings
            {
                Author = "coordinator@example.com",
                ProjectName = "Тестовый проект",
                IncludedClashStatuses = new List<string> { "New", "Active" }
            };
        }

        private static BcfExportResult Export(
            IClashSource source,
            BcfExportSettings settings,
            IProgress<BcfExportProgress> progress = null,
            ITopicGuidStore map = null)
        {
            using (var buffer = new MemoryStream())
            {
                return new BcfClashExporter(source, map).Export(buffer, settings, progress);
            }
        }

        private static IReadOnlyList<Guid> TopicGuids(IClashSource source, BcfExportSettings settings)
        {
            return ReadBack(source, settings).Topics.Select(t => t.Guid).OrderBy(g => g).ToList();
        }

        private static BcfTopic SingleTopic(IClashSource source, BcfExportSettings settings)
        {
            return ReadBack(source, settings).Topics.Single();
        }

        private static string StatusOfSingleTopic(IClashSource source, BcfExportSettings settings)
        {
            return SingleTopic(source, settings).TopicStatus;
        }

        private static BcfReadResult ReadBack(IClashSource source, BcfExportSettings settings, ITopicGuidStore map = null)
        {
            using (var buffer = new MemoryStream())
            {
                BcfExportResult result = new BcfClashExporter(source, map).Export(buffer, settings);
                Assert.True(result.Succeeded, result.Error?.ToString());

                using (var reading = new MemoryStream(buffer.ToArray()))
                {
                    return BcfArchiveReader.Read(reading);
                }
            }
        }

        private static ClashItem Clash(string group, string status)
        {
            var clash = new ClashItem
            {
                TestId = "test-1",
                TestName = "ОВ vs КР",
                GroupName = group,
                LevelName = group,
                DisplayName = "Столкновение " + Guid.NewGuid().ToString("N").Substring(0, 6),
                Status = status,
                DistanceMeters = 0.125,
                CenterMeters = new Vector3(1.5, 2.5, 3.5),
                AssignedTo = "hvac@example.com"
            };

            clash.Elements.Add(new ClashElementInfo
            {
                IfcGuid = "2SugUv4EX5LAhcVpDp2dUH",
                ElementId = "123456",
                ModelFileName = "ОВ.nwc",
                Path = "Модель > Этаж 3 > Воздуховод",
                Origin = ElementIdOrigin.RevitUniqueId
            });

            clash.Elements.Add(new ClashElementInfo
            {
                IfcGuid = "3woirKUVTF1wlWXy6aBfQJ",
                ElementId = "654321",
                ModelFileName = "КР.nwc",
                Path = "Модель > Этаж 3 > Балка",
                Origin = ElementIdOrigin.RevitUniqueId
            });

            clash.Comments.Add(new ClashCommentInfo
            {
                Author = "coordinator@example.com",
                Text = "Проверить трассировку",
                Date = new DateTimeOffset(2026, 8, 18, 9, 0, 0, TimeSpan.FromHours(3))
            });

            return clash;
        }

        /// <summary>
        /// Источник-заглушка. Ровно то, ради чего порт узкий: экспорт целиком
        /// проверяется без Navisworks.
        /// </summary>
        /// <summary>Приёмник прогресса, выполняющий обработчик сразу и в том же потоке.</summary>
        private sealed class ImmediateProgress : IProgress<BcfExportProgress>
        {
            private readonly Action<BcfExportProgress> _handler;

            public ImmediateProgress(Action<BcfExportProgress> handler)
            {
                _handler = handler;
            }

            public void Report(BcfExportProgress value)
            {
                _handler(value);
            }
        }

        private sealed class FakeClashSource : IClashSource
        {
            private readonly List<ClashItem> _clashes;

            public FakeClashSource(params ClashItem[] clashes)
            {
                _clashes = clashes.ToList();
            }

            /// <summary>Имя группы, на которой источник «сломается».</summary>
            public string FailViewpointFor { get; set; }

            /// <summary>Запрос, с которым источник просили снять последний кадр.</summary>
            public SnapshotRequest LastRequest { get; private set; }

            /// <summary>Отдавать ли снимки как пустые кадры.</summary>
            public bool ReportEmptySnapshots { get; set; }

            public ClashDocumentInfo GetDocument()
            {
                var document = new ClashDocumentInfo
                {
                    Title = "Тестовый документ",
                    FilePath = @"C:\проекты\тест.nwf",
                    Units = LengthUnit.Meters
                };

                document.Models.Add(new ClashModelInfo { FileName = "ОВ.nwc" });
                document.Models.Add(new ClashModelInfo { FileName = "КР.nwc" });

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
                foreach (ClashItem clash in _clashes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    yield return clash;
                }
            }

            public ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken)
            {
                if (FailViewpointFor != null && clash.GroupName == FailViewpointFor)
                {
                    throw new InvalidOperationException("вид недоступен");
                }

                LastRequest = snapshot;

                var data = new ClashViewpointData
                {
                    Camera = CameraConverter.ToPerspective(
                        new Vector3(10, 10, 10), Rotation.Identity, Math.PI / 4, 4.0 / 3.0, LengthUnit.Meters),
                    Snapshot = snapshot.Enabled ? TestData.FakePng() : null,
                    SnapshotIsEmpty = snapshot.Enabled && ReportEmptySnapshots
                };

                return data;
            }
        }
    }
}
