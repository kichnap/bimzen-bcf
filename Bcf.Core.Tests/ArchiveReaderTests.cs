using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Bcf.Core;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Core.Vocabulary;
using Xunit;

namespace Bcf.Core.Tests
{
    public class ArchiveReaderTests
    {
        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void WrittenArchive_ReadsBack(BcfVersion version)
        {
            BcfTopic written = TestData.Topic();
            byte[] archive = TestData.WriteArchive(version, written);

            BcfReadResult result = Read(archive);

            Assert.Equal(version, result.Version);
            Assert.Empty(result.Warnings);
            BcfTopic topic = Assert.Single(result.Topics);

            Assert.Equal(written.Guid, topic.Guid);
            Assert.Equal(written.Title, topic.Title);
            Assert.Equal(written.TopicStatus, topic.TopicStatus);
            Assert.Equal(written.TopicType, topic.TopicType);
            Assert.Equal(written.Priority, topic.Priority);
            Assert.Equal(written.Stage, topic.Stage);
            Assert.Equal(written.AssignedTo, topic.AssignedTo);
            Assert.Equal(written.Description, topic.Description);
            Assert.Equal(written.CreationDate, topic.CreationDate);
            Assert.Equal(written.Labels, topic.Labels);
            Assert.Single(topic.Comments);
            Assert.Equal(written.Comments[0].Text, topic.Comments[0].Text);
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void Camera_SurvivesRoundTrip(BcfVersion version)
        {
            BcfTopic written = TestData.Topic();
            var expected = (BcfPerspectiveCamera)written.Viewpoints[0].Camera;

            BcfReadResult result = Read(TestData.WriteArchive(version, written));

            BcfViewpoint viewpoint = Assert.Single(result.Topics[0].Viewpoints);
            var camera = Assert.IsType<BcfPerspectiveCamera>(viewpoint.Camera);

            Assert.Equal(expected.ViewPoint.X, camera.ViewPoint.X, 9);
            Assert.Equal(expected.ViewPoint.Y, camera.ViewPoint.Y, 9);
            Assert.Equal(expected.ViewPoint.Z, camera.ViewPoint.Z, 9);
            Assert.Equal(expected.Direction.Y, camera.Direction.Y, 9);
            Assert.Equal(expected.UpVector.Z, camera.UpVector.Z, 9);
            Assert.Equal(expected.FieldOfViewDegrees, camera.FieldOfViewDegrees, 9);
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void ComponentsAndClippingPlanes_SurviveRoundTrip(BcfVersion version)
        {
            BcfReadResult result = Read(TestData.WriteArchive(version, TestData.Topic()));

            BcfViewpoint viewpoint = result.Topics[0].Viewpoints[0];

            Assert.Equal(2, viewpoint.Selection.Count);
            Assert.Equal("2SugUv4EX5LAhcVpDp2dUH", viewpoint.Selection[0].IfcGuid);
            Assert.Equal("Navisworks", viewpoint.Selection[0].OriginatingSystem);

            Assert.NotNull(viewpoint.Visibility);
            Assert.True(viewpoint.Visibility.DefaultVisibility);
            Assert.Single(viewpoint.Visibility.Exceptions);

            BcfClippingPlane plane = Assert.Single(viewpoint.ClippingPlanes);
            Assert.Equal(3.5, plane.Location.Z, 9);
            Assert.Equal(-1.0, plane.Direction.Z, 9);
        }

        [Theory]
        [InlineData(BcfVersion.Bcf30)]
        [InlineData(BcfVersion.Bcf21)]
        public void Coloring_SurvivesRoundTrip(BcfVersion version)
        {
            BcfReadResult result = Read(TestData.WriteArchive(version, TestData.Topic()));

            BcfViewpoint viewpoint = result.Topics[0].Viewpoints[0];

            Assert.Equal(2, viewpoint.Coloring.Count);

            BcfColoring red = viewpoint.Coloring[0];
            Assert.Equal("FF0000", red.ToHex());
            Assert.Equal(255, red.Alpha);
            Assert.Equal("2SugUv4EX5LAhcVpDp2dUH", Assert.Single(red.Components).IfcGuid);

            BcfColoring blue = viewpoint.Coloring[1];
            Assert.Equal("7F0080FF", blue.ToHex());
            Assert.Equal(0x7F, blue.Alpha);
            Assert.Equal(2, blue.Components.Count);
            Assert.Equal("654321", blue.Components[1].AuthoringToolId);
        }

        [Fact]
        public void Coloring_IsReadFromBothLayouts()
        {
            // 3.0 wraps the components of a colour in Components, 2.1 puts them
            // straight into Color. A file half a step off its schema mixes the
            // two, and the reader looks in both places either way
            BcfReadResult result = Read(ArchiveWithColoring(@"
    <Coloring>
      <Color Color=""ff00ff"">
        <Components><Component IfcGuid=""2SugUv4EX5LAhcVpDp2dUH"" /></Components>
      </Color>
      <Color Color=""#80FFFF00"">
        <Component IfcGuid=""3woirKUVTF1wlWXy6aBfQJ"" />
      </Color>
    </Coloring>"));

            BcfViewpoint viewpoint = result.Topics.Single().Viewpoints.Single();

            Assert.Equal(2, viewpoint.Coloring.Count);
            Assert.Equal("FF00FF", viewpoint.Coloring[0].ToHex());
            Assert.Equal("2SugUv4EX5LAhcVpDp2dUH", viewpoint.Coloring[0].Components.Single().IfcGuid);

            Assert.Equal(0x80, viewpoint.Coloring[1].Alpha);
            Assert.Equal(0xFF, viewpoint.Coloring[1].Red);
            Assert.Equal(0xFF, viewpoint.Coloring[1].Green);
            Assert.Equal(0x00, viewpoint.Coloring[1].Blue);
            Assert.Equal("3woirKUVTF1wlWXy6aBfQJ", viewpoint.Coloring[1].Components.Single().IfcGuid);
        }

        [Fact]
        public void UnreadableColor_IsWarning_AndEmptyColorIsDropped()
        {
            BcfReadResult result = Read(ArchiveWithColoring(@"
    <Coloring>
      <Color Color=""crimson"">
        <Components><Component IfcGuid=""2SugUv4EX5LAhcVpDp2dUH"" /></Components>
      </Color>
      <Color Color=""00FF00"" />
      <Color Color=""0000FF"">
        <Components><Component IfcGuid=""3woirKUVTF1wlWXy6aBfQJ"" /></Components>
      </Color>
    </Coloring>"));

            BcfViewpoint viewpoint = result.Topics.Single().Viewpoints.Single();

            Assert.Equal("0000FF", Assert.Single(viewpoint.Coloring).ToHex());
            Assert.Contains(result.Warnings, w => w.Contains("crimson"));
        }

        [Theory]
        [InlineData("FF0000", 255, 255, 0, 0)]
        [InlineData("80FF0000", 128, 255, 0, 0)]
        [InlineData("#00ff7f", 255, 0, 255, 127)]
        [InlineData(" 0a0B0c ", 255, 10, 11, 12)]
        public void ColorHex_IsParsedAsArgb(string value, int alpha, int red, int green, int blue)
        {
            BcfColoring coloring;

            Assert.True(BcfColoring.TryParseHex(value, out coloring));
            Assert.Equal(alpha, coloring.Alpha);
            Assert.Equal(red, coloring.Red);
            Assert.Equal(green, coloring.Green);
            Assert.Equal(blue, coloring.Blue);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("FFF")]
        [InlineData("FF00000")]
        [InlineData("GG0000")]
        [InlineData("+F0000")]
        public void ColorHex_RejectsWhatIsNotAColor(string value)
        {
            BcfColoring coloring;

            Assert.False(BcfColoring.TryParseHex(value, out coloring));
            Assert.Null(coloring);
        }

        [Fact]
        public void Project_IsReadBack()
        {
            BcfReadResult result = Read(TestData.WriteArchive(BcfVersion.Bcf30, TestData.Topic()));

            Assert.NotNull(result.Project);
            Assert.Equal("8a1c2d3e-4f56-4789-8abc-def012345678", result.Project.ProjectId);
            Assert.Equal("Тестовый проект", result.Project.Name);
        }

        [Fact]
        public void ForeignStatus_IsKeptAndReported()
        {
            // A file from BIMcollab or Revizto legitimately arrives with a vocabulary
            // of its own: it must not be rejected, and the value must be kept and shown.
            byte[] archive = ForeignArchive();

            BcfReadResult result = Read(archive);

            BcfTopic topic = Assert.Single(result.Topics);
            Assert.Equal("Open", topic.TopicStatus);
            Assert.Equal("Open", topic.ExternalValues["TopicStatus"]);
            Assert.Equal("Information", topic.ExternalValues["TopicType"]);

            BcfExternalValue status = Assert.Single(result.ExternalValues, v => v.Field == "TopicStatus");
            Assert.Equal("Open", status.Value);
            Assert.Equal(1, status.Count);
            Assert.Equal(topic.Guid, status.FirstTopic);
        }

        [Fact]
        public void ForeignLabelsAndPriority_AreReported()
        {
            BcfReadResult result = Read(ForeignArchive());

            Assert.Contains(result.ExternalValues, v => v.Field == "Priority" && v.Value == "Medium");
            Assert.Contains(result.ExternalValues, v => v.Field == "Label" && v.Value == "MEP");
        }

        [Fact]
        public void KnownValues_AreNotReportedAsForeign()
        {
            BcfReadResult result = Read(TestData.WriteArchive(BcfVersion.Bcf30, TestData.Topic()));

            Assert.Empty(result.ExternalValues);
        }

        [Fact]
        public void MissingViewpointFile_IsWarning_NotFailure()
        {
            byte[] archive = ArchiveWithoutViewpointFile();

            BcfReadResult result = Read(archive);

            Assert.Single(result.Topics);
            Assert.Contains(result.Warnings, w => w.Contains("точки зрения"));
        }

        [Fact]
        public void BrokenMarkup_DoesNotStopOtherTopics()
        {
            byte[] archive = ArchiveWithBrokenTopic();

            BcfReadResult result = Read(archive);

            // One broken file must not bring down the read of the whole archive
            Assert.Single(result.Topics);
            Assert.Contains(result.Warnings, w => w.Contains("Не удалось прочитать"));
        }

        [Fact]
        public void MissingVersionFile_DefaultsToThirtyWithWarning()
        {
            byte[] archive = ArchiveWithoutVersionFile();

            BcfReadResult result = Read(archive);

            Assert.Equal(BcfVersion.Bcf30, result.Version);
            Assert.Contains(result.Warnings, w => w.Contains("bcf.version"));
        }

        private static BcfReadResult Read(byte[] archive)
        {
            using (var buffer = new MemoryStream(archive))
            {
                return BcfArchiveReader.Read(buffer);
            }
        }

        /// <summary>
        /// An archive "from someone else's tool": the BIMcollab vocabulary instead
        /// of ours. It is assembled by hand, because our serializer will not let such
        /// a file out — the validation on the way out is strict.
        /// </summary>
        private static byte[] ForeignArchive()
        {
            const string markup = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Markup>
  <Topic Guid=""3c9c2f6a-1111-4b2e-9a55-abcdefabcdef"" TopicType=""Information"" TopicStatus=""Open"">
    <Title>Замечание из стороннего инструмента</Title>
    <Priority>Medium</Priority>
    <Labels><Label>MEP</Label></Labels>
    <CreationDate>2026-08-18T10:30:00+03:00</CreationDate>
    <CreationAuthor>architect@example.com</CreationAuthor>
  </Topic>
</Markup>";

            return BuildArchive(new[]
            {
                Tuple.Create("bcf.version", @"<?xml version=""1.0"" encoding=""utf-8""?><Version VersionId=""3.0"" />"),
                Tuple.Create("3c9c2f6a-1111-4b2e-9a55-abcdefabcdef/markup.bcf", markup)
            });
        }

        private static byte[] ArchiveWithoutViewpointFile()
        {
            const string markup = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Markup>
  <Topic Guid=""3c9c2f6a-2222-4b2e-9a55-abcdefabcdef"" TopicType=""Clash"" TopicStatus=""New"">
    <Title>Без файла точки зрения</Title>
    <CreationDate>2026-08-18T10:30:00+03:00</CreationDate>
    <CreationAuthor>coordinator@example.com</CreationAuthor>
    <Viewpoints>
      <ViewPoint Guid=""3c9c2f6a-3333-4b2e-9a55-abcdefabcdef"">
        <Viewpoint>missing.bcfv</Viewpoint>
      </ViewPoint>
    </Viewpoints>
  </Topic>
</Markup>";

            return BuildArchive(new[]
            {
                Tuple.Create("bcf.version", @"<?xml version=""1.0"" encoding=""utf-8""?><Version VersionId=""3.0"" />"),
                Tuple.Create("3c9c2f6a-2222-4b2e-9a55-abcdefabcdef/markup.bcf", markup)
            });
        }

        private static byte[] ArchiveWithBrokenTopic()
        {
            const string good = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Markup>
  <Topic Guid=""3c9c2f6a-4444-4b2e-9a55-abcdefabcdef"" TopicType=""Clash"" TopicStatus=""New"">
    <Title>Целый топик</Title>
    <CreationDate>2026-08-18T10:30:00+03:00</CreationDate>
    <CreationAuthor>coordinator@example.com</CreationAuthor>
  </Topic>
</Markup>";

            return BuildArchive(new[]
            {
                Tuple.Create("bcf.version", @"<?xml version=""1.0"" encoding=""utf-8""?><Version VersionId=""3.0"" />"),
                Tuple.Create("3c9c2f6a-4444-4b2e-9a55-abcdefabcdef/markup.bcf", good),
                Tuple.Create("3c9c2f6a-5555-4b2e-9a55-abcdefabcdef/markup.bcf", "<Markup><Topic Guid=")
            });
        }

        private static byte[] ArchiveWithoutVersionFile()
        {
            const string markup = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Markup>
  <Topic Guid=""3c9c2f6a-6666-4b2e-9a55-abcdefabcdef"" TopicType=""Clash"" TopicStatus=""New"">
    <Title>Без bcf.version</Title>
    <CreationDate>2026-08-18T10:30:00+03:00</CreationDate>
    <CreationAuthor>coordinator@example.com</CreationAuthor>
  </Topic>
</Markup>";

            return BuildArchive(new[]
            {
                Tuple.Create("3c9c2f6a-6666-4b2e-9a55-abcdefabcdef/markup.bcf", markup)
            });
        }

        /// <summary>A topic with one viewpoint whose Components carry the given Coloring element.</summary>
        private static byte[] ArchiveWithColoring(string coloring)
        {
            const string markup = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Markup>
  <Topic Guid=""3c9c2f6a-7777-4b2e-9a55-abcdefabcdef"" TopicType=""Clash"" TopicStatus=""New"">
    <Title>Раскраска</Title>
    <CreationDate>2026-08-18T10:30:00+03:00</CreationDate>
    <CreationAuthor>coordinator@example.com</CreationAuthor>
    <Viewpoints>
      <ViewPoint Guid=""3c9c2f6a-8888-4b2e-9a55-abcdefabcdef"">
        <Viewpoint>viewpoint.bcfv</Viewpoint>
      </ViewPoint>
    </Viewpoints>
  </Topic>
</Markup>";

            string visualization = @"<?xml version=""1.0"" encoding=""utf-8""?>
<VisualizationInfo Guid=""3c9c2f6a-8888-4b2e-9a55-abcdefabcdef"">
  <Components>
    <Visibility DefaultVisibility=""true"" />" + coloring + @"
  </Components>
</VisualizationInfo>";

            return BuildArchive(new[]
            {
                Tuple.Create("bcf.version", @"<?xml version=""1.0"" encoding=""utf-8""?><Version VersionId=""3.0"" />"),
                Tuple.Create("3c9c2f6a-7777-4b2e-9a55-abcdefabcdef/markup.bcf", markup),
                Tuple.Create("3c9c2f6a-7777-4b2e-9a55-abcdefabcdef/viewpoint.bcfv", visualization)
            });
        }

        private static byte[] BuildArchive(Tuple<string, string>[] entries)
        {
            using (var buffer = new MemoryStream())
            {
                using (var zip = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
                {
                    foreach (Tuple<string, string> entry in entries)
                    {
                        using (Stream stream = zip.CreateEntry(entry.Item1).Open())
                        {
                            byte[] bytes = new UTF8Encoding(false).GetBytes(entry.Item2);
                            stream.Write(bytes, 0, bytes.Length);
                        }
                    }
                }

                return buffer.ToArray();
            }
        }
    }
}
