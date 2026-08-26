using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Bcf.Core.Clash;
using Bcf.Core.Conversion;
using Bcf.Core.Geometry;

namespace Bcf.TestData.Generator
{
    /// <summary>
    /// The source of clashes for the reference archives.
    ///
    /// The data is synthetic yet shaped exactly like what comes out of Clash
    /// Detective: non-ASCII names of tests and levels, pairs of elements with
    /// IFC identifiers, comments, distances.
    ///
    /// Источник коллизий для эталонных архивов.
    ///
    /// Данные синтетические, но по форме такие же, как приходят из Clash
    /// Detective: русские имена проверок и уровней, пары элементов
    /// с идентификаторами IFC, комментарии, расстояния.
    /// </summary>
    internal sealed class SyntheticClashSource : IClashSource
    {
        private static readonly string[] Tests =
        {
            "ОВ_vs_КР",
            "ВК_vs_АР",
            "ЭОМ_vs_ОВ"
        };

        private static readonly string[] Disciplines = { "ОВ", "ВК", "ЭОМ", "АР", "КР" };

        private readonly int _topicCount;
        private readonly DateTimeOffset _moment;
        private readonly byte[] _snapshot;

        /// <param name="topicCount">How many topics should come out: one group per topic.</param>
        /// <param name="moment">The fixed time — reference files have to be reproducible.</param>
        /// <param name="snapshot">The snapshot; null gives an archive with no images.</param>
        public SyntheticClashSource(int topicCount, DateTimeOffset moment, byte[] snapshot)
        {
            _topicCount = topicCount;
            _moment = moment;
            _snapshot = snapshot;
        }

        public ClashDocumentInfo GetDocument()
        {
            var document = new ClashDocumentInfo
            {
                Title = "ЖК Северный — координация",
                FilePath = @"C:\Проекты\ЖК Северный\Координация\Сводная.nwf",
                Units = LengthUnit.Meters
            };

            document.Models.Add(new ClashModelInfo { FileName = "АР.nwc", Date = _moment.AddDays(-3) });
            document.Models.Add(new ClashModelInfo { FileName = "КР.nwc", Date = _moment.AddDays(-2) });
            document.Models.Add(new ClashModelInfo { FileName = "ОВ.nwc", Date = _moment.AddDays(-1) });

            return document;
        }

        public IReadOnlyList<ClashTestInfo> GetTests()
        {
            var tests = new List<ClashTestInfo>();

            for (int i = 0; i < Tests.Length; i++)
            {
                tests.Add(new ClashTestInfo
                {
                    Id = "test-" + (i + 1).ToString(CultureInfo.InvariantCulture),
                    Name = Tests[i],
                    Index = i,
                    ClashCount = ClashesInTest(i)
                });
            }

            return tests;
        }

        public IEnumerable<ClashItem> EnumerateClashes(ClashTestInfo test, CancellationToken cancellationToken)
        {
            int count = ClashesInTest(test.Index);

            for (int i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return CreateClash(test, i);
            }
        }

        public ClashViewpointData CreateViewpoint(ClashItem clash, SnapshotRequest snapshot, CancellationToken cancellationToken)
        {
            // A stable hash of our own rather than String.GetHashCode: in .NET
            // that one is randomised per process, and the reference archives
            // would stop matching byte for byte from run to run
            int index = (int)(StableHash(clash.DisplayName) % 360);
            double angle = index * Math.PI / 180.0;

            var data = new ClashViewpointData
            {
                Camera = CameraConverter.ToPerspective(
                    new Vector3(12 + index % 7, -18 - index % 5, 6),
                    Rotation.FromAxisAngle(new Vector3(1, 0, 0), Math.PI / 2 + angle / 12),
                    Math.PI / 4,
                    4.0 / 3.0,
                    LengthUnit.Meters),
                Snapshot = snapshot.Enabled ? _snapshot : null
            };

            data.ClippingPlanes.Add(new Bcf.Core.Model.BcfClippingPlane
            {
                Location = new Vector3(0, 0, 3.2),
                Direction = new Vector3(0, 0, -1)
            });

            return data;
        }

        /// <summary>
        /// The FNV-1a hash: the same in any process and on any platform.
        /// Хеш FNV-1a: одинаковый в любом процессе и на любой платформе.
        /// </summary>
        private static uint StableHash(string value)
        {
            uint hash = 2166136261;

            foreach (char c in value ?? string.Empty)
            {
                hash ^= c;
                hash *= 16777619;
            }

            return hash;
        }

        /// <summary>
        /// The topics are spread across the tests roughly evenly.
        /// Замечания распределены по проверкам примерно поровну.
        /// </summary>
        private int ClashesInTest(int testIndex)
        {
            int perTest = _topicCount / Tests.Length;
            int remainder = _topicCount % Tests.Length;

            return perTest + (testIndex < remainder ? 1 : 0);
        }

        private ClashItem CreateClash(ClashTestInfo test, int index)
        {
            int global = test.Index * 1000 + index;
            int level = 1 + index % 12;

            var clash = new ClashItem
            {
                TestId = test.Id,
                TestName = test.Name,
                // One group makes one topic: the group names differ within a test
                GroupName = "Этаж " + level.ToString(CultureInfo.InvariantCulture) +
                            " — зона " + (char)('А' + index % 4),
                DisplayName = "Столкновение " + (index + 1).ToString(CultureInfo.InvariantCulture),
                Status = index % 5 == 0 ? "Active" : "New",
                DistanceMeters = Math.Round(0.02 + (index % 40) * 0.01, 3),
                CenterMeters = new Vector3(
                    Math.Round(10 + index * 0.37, 3),
                    Math.Round(-4 - index * 0.21, 3),
                    Math.Round(3.2 * level, 3)),
                LevelName = "Этаж " + level.ToString(CultureInfo.InvariantCulture),
                GridLocation = ((char)('A' + index % 8)) + "-" + (1 + index % 12).ToString(CultureInfo.InvariantCulture),
                CreatedDate = _moment.AddHours(-index),
                AssignedTo = index % 3 == 0 ? "hvac@example.com" : null
            };

            clash.Elements.Add(Element(global, 1, Disciplines[index % Disciplines.Length]));
            clash.Elements.Add(Element(global, 2, Disciplines[(index + 2) % Disciplines.Length]));

            if (index % 4 == 0)
            {
                clash.Comments.Add(new ClashCommentInfo
                {
                    Author = "coordinator@example.com",
                    Text = "Проверить трассировку на пересечении с балкой.",
                    Date = _moment.AddHours(-index - 1)
                });
            }

            return clash;
        }

        private static ClashElementInfo Element(int global, int side, string discipline)
        {
            // The identifier is derived from a Revit UniqueId the same way as in
            // a real export: a reference file has to look like a live one
            string uniqueId = string.Format(
                CultureInfo.InvariantCulture,
                "1a2b3c4d-5e6f-4a7b-8c9d-{0:D12}-{1:x8}",
                global,
                (uint)(global * 10 + side));

            return new ClashElementInfo
            {
                IfcGuid = IfcGuidConverter.RevitUniqueIdToIfcGuid(uniqueId),
                ElementId = (global * 10 + side).ToString(CultureInfo.InvariantCulture),
                ModelFileName = discipline + ".nwc",
                Path = "Сводная > " + discipline + ".nwc > Этаж " + (1 + global % 12).ToString(CultureInfo.InvariantCulture) +
                       " > Элемент " + (global * 10 + side).ToString(CultureInfo.InvariantCulture),
                Origin = ElementIdOrigin.RevitUniqueId
            };
        }
    }
}
