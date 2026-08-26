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
    /// Группировка коллизий: что переживает выгрузку, а что теряется.
    ///
    /// Проверки написаны по разбору живой выгрузки на 1391 замечание —
    /// плоский список компонентов группы не сохраняет разбиение на пары,
    /// а имена несгруппированных коллизий Navisworks раздаёт заново.
    /// </summary>
    public class ClashGroupingTests
    {
        [Fact]
        public void GroupTopic_KeepsPairsInViewpoints()
        {
            // Три коллизии в группе, общий элемент в двух из них: именно тот
            // случай, когда плоский список компонентов пары теряет
            var source = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-1", "лоток-2"),
                Clash("Ось А-13", "стена-2", "лоток-3"));

            BcfTopic topic = ReadBack(Settings(), source).Topics.Single();

            // Обзорная точка зрения плюс по одной на коллизию
            Assert.Equal(4, topic.Viewpoints.Count);

            IReadOnlyList<BcfViewpoint> pairs = topic.Viewpoints.Where(v => v.Index > 0).ToList();

            Assert.Equal(3, pairs.Count);
            Assert.All(pairs, v => Assert.Equal(2, v.Selection.Count));

            BcfViewpoint overview = topic.Viewpoints.Single(v => v.Index == 0);

            // В обзорной — все пять элементов без повторов: именно так пары
            // и теряются, если других точек зрения нет
            Assert.Equal(5, overview.Selection.Count);
        }

        [Fact]
        public void PairViewpoints_HaveNoSnapshots()
        {
            BcfExportSettings settings = Settings();
            settings.IncludeSnapshots = true;

            var source = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-1", "лоток-2"));

            BcfExportResult result = Export(settings, source);

            // Секунда на кадр против пары килобайт XML: снимок снимается
            // только для обзорной точки зрения
            Assert.Equal(1, result.SnapshotsCaptured);
        }

        [Fact]
        public void PairViewpoints_CanBeTurnedOff()
        {
            BcfExportSettings settings = Settings();
            settings.ViewpointPerClash = false;

            var source = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-1", "лоток-2"));

            BcfTopic topic = ReadBack(settings, source).Topics.Single();

            Assert.Single(topic.Viewpoints);
        }

        [Fact]
        public void PairViewpointGuids_SurviveRepeatedExport()
        {
            var source = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-1", "лоток-2"));

            IEnumerable<Guid> first = ReadBack(Settings(), source).Topics.Single().Viewpoints.Select(v => v.Guid);
            IEnumerable<Guid> second = ReadBack(Settings(), source).Topics.Single().Viewpoints.Select(v => v.Guid);

            Assert.Equal(first.OrderBy(g => g), second.OrderBy(g => g));
        }

        [Fact]
        public void SingleClashTopic_IsKeyedByElements_NotByClashName()
        {
            // Navisworks раздаёт имена «Столкновение123» заново при пересоздании
            // проверки. Замечание, опознаваемое по имени, при этом теряет себя
            ClashItem before = Clash(null, "стена-1", "лоток-1");
            before.DisplayName = "Столкновение7";

            ClashItem after = Clash(null, "стена-1", "лоток-1");
            after.DisplayName = "Столкновение42";

            Guid first = ReadBack(Settings(), new FakeSource(before)).Topics.Single().Guid;
            Guid second = ReadBack(Settings(), new FakeSource(after)).Topics.Single().Guid;

            Assert.Equal(first, second);
        }

        [Fact]
        public void GroupTopic_IsStillKeyedByGroupName()
        {
            // У настоящей группы имя даёт человек, и оно устойчивее состава:
            // приход и уход коллизий не должен плодить замечания
            var first = new FakeSource(Clash("Ось А-13", "стена-1", "лоток-1"));
            var second = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-2", "лоток-2"));

            Guid before = ReadBack(Settings(), first).Topics.Single().Guid;
            Guid after = ReadBack(Settings(), second).Topics.Single().Guid;

            Assert.Equal(before, after);
        }

        [Fact]
        public void LegacyKey_CarriesTheIdentifierOver()
        {
            // У пользователя, выгружавшего прежней версией, идентификатор выдан
            // на старый ключ. Смена правила счёта не должна обернуться дублями
            ClashItem clash = Clash(null, "стена-1", "лоток-1");
            clash.DisplayName = "Столкновение7";

            var map = new TopicGuidMap();
            var legacy = Guid.Parse("11111111-2222-3333-4444-555555555555");

            map.Remember(StableTopicKey.ForGroup("ОВ vs КР", "Столкновение7"), legacy);

            BcfExportResult result;
            BcfReadResult read = ReadBack(Settings(), new FakeSource(clash), map, out result);

            Assert.Equal(legacy, read.Topics.Single().Guid);
            Assert.Equal(1, result.TopicsReused);
        }

        [Fact]
        public void SamePairTwice_GivesTwoTopics()
        {
            // Труба пересекает стену дважды: пара элементов одна, а замечания
            // должны быть разными — иначе второе затрёт первое в архиве
            ClashItem first = Clash(null, "стена-1", "труба-1");
            ClashItem second = Clash(null, "стена-1", "труба-1");

            BcfReadResult read = ReadBack(Settings(), new FakeSource(first, second));

            Assert.Equal(2, read.Topics.Count);
            Assert.Equal(2, read.Topics.Select(t => t.Guid).Distinct().Count());
        }

        [Fact]
        public void RepeatedPairKeys_AreStableBetweenExports()
        {
            ClashItem first = Clash(null, "стена-1", "труба-1");
            ClashItem second = Clash(null, "стена-1", "труба-1");

            IEnumerable<Guid> before = ReadBack(Settings(), new FakeSource(first, second)).Topics.Select(t => t.Guid);
            IEnumerable<Guid> after = ReadBack(Settings(), new FakeSource(first, second)).Topics.Select(t => t.Guid);

            Assert.Equal(before.OrderBy(g => g), after.OrderBy(g => g));
        }

        [Fact]
        public void ElementIdSources_AreCounted()
        {
            // Потребитель сверяет свои данные с нашими по числовому
            // идентификатору, а у составного элемента номер геометрии
            // и номер элемента разные. Счётчик показывает, который в файле
            ClashItem clash = Clash(null, "стена-1", "труба-1");
            clash.Elements[0].ElementIdSource = "LcRevitId/LcOaNat64AttributeValue@1";
            clash.Elements[1].ElementIdSource = "LcRevitId/LcOaNat64AttributeValue@0";

            BcfExportResult result = Export(Settings(), new FakeSource(clash));

            Assert.Equal(1, result.ElementIdSources["LcRevitId/LcOaNat64AttributeValue@1"]);
            Assert.Equal(1, result.ElementIdSources["LcRevitId/LcOaNat64AttributeValue@0"]);
        }

        [Fact]
        public void ElementsWithoutIdSource_AreCountedSeparately()
        {
            ClashItem clash = Clash(null, "стена-1", "труба-1");

            BcfExportResult result = Export(Settings(), new FakeSource(clash));

            Assert.Equal(2, result.ElementIdSources["not found"]);
        }

        [Fact]
        public void ElementIdOrigins_AreCounted()
        {
            // Прочитанный из свойства IFC идентификатор совпадёт с исходной
            // моделью IFC всегда, вычисленный из UniqueId — почти всегда,
            // внутренний Navisworks — никогда. Тому, кто сверяет выгрузку
            // с IFC, нужно знать пропорцию
            ClashItem clash = Clash(null, "стена-1", "труба-1");
            clash.Elements[0].Origin = ElementIdOrigin.IfcProperty;
            clash.Elements[1].Origin = ElementIdOrigin.InstanceGuid;

            BcfExportResult result = Export(Settings(), new FakeSource(clash));

            Assert.Equal(1, result.ElementIdOrigins["IfcProperty"]);
            Assert.Equal(1, result.ElementIdOrigins["InstanceGuid"]);
        }

        [Fact]
        public void GroupName_ReachesTheDescription()
        {
            BcfExportSettings settings = Settings();
            settings.Grouping = ClashGroupingMode.ClashPerTopic;

            var source = new FakeSource(Clash("Ось А-13", "стена-1", "лоток-1"));

            BcfTopic topic = ReadBack(settings, source).Topics.Single();

            // Поштучные замечания без этого теряют принадлежность к группе совсем
            Assert.Contains("Группа: Ось А-13", topic.Description);
        }

        [Fact]
        public void ClashPerTopic_LinksTopicsOfOneGroup()
        {
            BcfExportSettings settings = Settings();
            settings.Grouping = ClashGroupingMode.ClashPerTopic;

            var source = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-2", "лоток-2"),
                Clash("Ось Б-2", "стена-3", "лоток-3"));

            IList<BcfTopic> topics = ReadBack(settings, source).Topics;

            Assert.Equal(3, topics.Count);

            // Звезда: второе замечание группы ссылается на первое, одиночка — ни на кого
            Assert.Equal(1, topics.Count(t => t.RelatedTopics.Count == 1));
            Assert.Equal(2, topics.Count(t => t.RelatedTopics.Count == 0));
        }

        [Fact]
        public void GroupLinks_CanBeTurnedOff()
        {
            BcfExportSettings settings = Settings();
            settings.Grouping = ClashGroupingMode.ClashPerTopic;
            settings.LinkGroupTopics = false;

            var source = new FakeSource(
                Clash("Ось А-13", "стена-1", "лоток-1"),
                Clash("Ось А-13", "стена-2", "лоток-2"));

            Assert.All(ReadBack(settings, source).Topics, t => Assert.Empty(t.RelatedTopics));
        }

        [Fact]
        public void GroupNameAsLabel_IsOffByDefault()
        {
            var source = new FakeSource(Clash("Ось А-13", "стена-1", "лоток-1"));

            BcfTopic topic = ReadBack(Settings(), source).Topics.Single();

            Assert.DoesNotContain("Ось А-13", topic.Labels);
        }

        [Fact]
        public void GroupNameAsLabel_IsDeclaredInExtensions()
        {
            BcfExportSettings settings = Settings();
            settings.GroupNameAsLabel = true;

            var source = new FakeSource(Clash("Ось А-13", "стена-1", "лоток-1"));

            byte[] archive = ExportBytes(settings, source);
            BcfTopic topic = Read(archive).Topics.Single();

            Assert.Contains("Ось А-13", topic.Labels);

            // Файл обязан объявлять всё, что в нём есть, — иначе строгий
            // приёмник вправе спросить, откуда взялась эта метка
            Assert.Contains("Ось А-13", Entry(archive, "extensions.xml"));
        }

        // --- вспомогательное -------------------------------------------------

        private static BcfExportSettings Settings()
        {
            return new BcfExportSettings
            {
                Author = "coordinator@example.com",
                ProjectName = "Тестовый проект",
                IncludedClashStatuses = new List<string> { "New", "Active" },
                IncludeSnapshots = false,
                ExportTime = new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.Zero)
            };
        }

        private static ClashItem Clash(string group, params string[] elementIds)
        {
            var clash = new ClashItem
            {
                TestId = "test-1",
                TestName = "ОВ vs КР",
                GroupName = group,
                LevelName = "-01 АР1",
                GridLocation = "А-13 : -01 АР1",
                DisplayName = "Столкновение" + Guid.NewGuid().ToString("N").Substring(0, 4),
                Status = "New",
                DistanceMeters = 0.01,
                CenterMeters = new Vector3(1, 2, 3)
            };

            foreach (string id in elementIds)
            {
                clash.Elements.Add(new ClashElementInfo
                {
                    IfcGuid = IfcGuidConverter.ToIfcGuid(Deterministic(id)),
                    ElementId = id,
                    ModelFileName = "ОВ.nwc",
                    Path = "Модель > " + id,
                    Origin = ElementIdOrigin.RevitUniqueId
                });
            }

            return clash;
        }

        /// <summary>Один и тот же элемент между выгрузками — один и тот же идентификатор.</summary>
        private static Guid Deterministic(string id)
        {
            var bytes = new byte[16];
            byte[] source = System.Text.Encoding.UTF8.GetBytes(id);

            for (int i = 0; i < source.Length && i < 16; i++) bytes[i] = source[i];

            return new Guid(bytes);
        }

        private static BcfExportResult Export(BcfExportSettings settings, FakeSource source)
        {
            using (var buffer = new MemoryStream())
            {
                return new BcfClashExporter(source).Export(buffer, settings);
            }
        }

        private static byte[] ExportBytes(BcfExportSettings settings, FakeSource source)
        {
            using (var buffer = new MemoryStream())
            {
                BcfExportResult result = new BcfClashExporter(source).Export(buffer, settings);
                Assert.True(result.Succeeded, result.Error?.ToString());

                return buffer.ToArray();
            }
        }

        private static BcfReadResult ReadBack(BcfExportSettings settings, FakeSource source = null)
        {
            BcfExportResult ignored;

            return ReadBack(settings, source ?? new FakeSource(Clash("Ось А-13", "стена-1", "лоток-1")), null, out ignored);
        }

        private static BcfReadResult ReadBack(
            BcfExportSettings settings, FakeSource source, ITopicGuidStore map, out BcfExportResult result)
        {
            using (var buffer = new MemoryStream())
            {
                result = new BcfClashExporter(source, map).Export(buffer, settings);
                Assert.True(result.Succeeded, result.Error?.ToString());

                return Read(buffer.ToArray());
            }
        }

        private static BcfReadResult Read(byte[] archive)
        {
            using (var stream = new MemoryStream(archive))
            {
                return BcfArchiveReader.Read(stream);
            }
        }

        private static string Entry(byte[] archive, string entryName)
        {
            using (var stream = new MemoryStream(archive))
            using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read))
            using (var reader = new StreamReader(zip.GetEntry(entryName).Open()))
            {
                return reader.ReadToEnd();
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
    }
}
