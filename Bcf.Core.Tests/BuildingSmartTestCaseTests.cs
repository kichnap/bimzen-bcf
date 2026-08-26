using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Bcf.Core.Model;
using Bcf.Core.Serialization;
using Bcf.Vocabulary.Generator;
using Xunit;

namespace Bcf.Core.Tests
{
    /// <summary>
    /// Reading the official buildingSMART test cases.
    ///
    /// Every other fixture in this repository is written by our own exporter
    /// and read back by our own reader: a mistake shared by both halves would
    /// pass unnoticed. These archives were produced by other tools years
    /// before this library existed, and they are the only check of whether we
    /// read the format the way its authors meant it.
    ///
    /// Reading the official buildingSMART reference archives.
    ///
    /// Every other fixture in this repository is written by our exporter and read
    /// by our reader: an error shared by both halves would go unnoticed. These
    /// archives were made by other tools years before this library existed, and
    /// they are the only check of whether we understand the format the way its
    /// authors do.
    /// </summary>
    public class BuildingSmartTestCaseTests
    {
        /// <summary>
        /// What each archive must yield. The numbers were read out of the files
        /// themselves, not out of our output: an expectation copied from what
        /// the code already does checks nothing.
        ///
        /// What every archive has to yield. The numbers are taken from the files
        /// themselves and not from our output: an expectation copied from what the
        /// code already does checks nothing.
        /// </summary>
        public static IEnumerable<object[]> Cases()
        {
            // file, version, topics, viewpoints, selected components
            yield return new object[] { @"v2.1\MinimumInformation.bcf", BcfVersion.Bcf21, 1, 0, 0 };
            yield return new object[] { @"v2.1\MaximumInformation.bcf", BcfVersion.Bcf21, 2, 3, 15 };
            yield return new object[] { @"v2.1\ExternalBIMSnippet.bcf", BcfVersion.Bcf21, 1, 0, 0 };
            yield return new object[] { @"v2.1\InternalBIMSnippet.bcf", BcfVersion.Bcf21, 1, 0, 0 };
            yield return new object[] { @"v2.1\RelatedTopics.bcf", BcfVersion.Bcf21, 2, 0, 0 };

            yield return new object[] { @"v2.0\MinimumInformation.bcfzip", BcfVersion.Bcf20, 1, 0, 0 };
            yield return new object[] { @"v2.0\SelectedComponent.bcfzip", BcfVersion.Bcf20, 1, 1, 1 };
            yield return new object[] { @"v2.0\SingleInvisibleWall.bcfzip", BcfVersion.Bcf20, 1, 1, 0 };
            yield return new object[] { @"v2.0\Clippingplane.bcfzip", BcfVersion.Bcf20, 1, 1, 0 };
            yield return new object[] { @"v2.0\ComponentColoring.bcfzip", BcfVersion.Bcf20, 1, 1, 0 };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void OfficialArchive_IsReadAsItsAuthorsMeantIt(
            string relativePath, BcfVersion version, int topics, int viewpoints, int selected)
        {
            BcfReadResult read = Read(relativePath);

            Assert.Equal(version, read.Version);
            Assert.Equal(topics, read.Topics.Count);
            Assert.Equal(viewpoints, read.Topics.Sum(t => t.Viewpoints.Count));
            Assert.Equal(selected, read.Topics.Sum(t => t.Viewpoints.Sum(v => v.Selection.Count)));

            // A topic without an identifier or a title is a topic no receiving
            // tool can show
            Assert.All(read.Topics, topic =>
            {
                Assert.NotEqual(Guid.Empty, topic.Guid);
                Assert.False(string.IsNullOrWhiteSpace(topic.Title));
            });
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void OfficialArchive_IsReadWithoutComplaints(
            string relativePath, BcfVersion version, int topics, int viewpoints, int selected)
        {
            _ = version;
            _ = topics;
            _ = viewpoints;
            _ = selected;

            // A warning here means we met something we do not understand.
            // On files written to the specification there must be nothing
            // of the kind
            Assert.Empty(Read(relativePath).Warnings);
        }

        [Fact]
        public void ForeignVocabulary_IsKeptRatherThanRejected()
        {
            // The reference archive carries statuses and types of its own —
            // Open, Structural, Construction Start. None of them is in our
            // vocabulary, and all of them are legitimate
            BcfReadResult read = Read(@"v2.1\MaximumInformation.bcf");

            Assert.NotEmpty(read.ExternalValues);
            Assert.Contains(read.ExternalValues, v => v.Field == "TopicStatus" && v.Value == "Open");
            Assert.Contains(read.Topics, t => t.TopicStatus == "Open");
        }

        [Fact]
        public void Camera_IsReadFromAnArchiveWeDidNotWrite()
        {
            BcfViewpoint viewpoint = Read(@"v2.0\Clippingplane.bcfzip").Topics.Single().Viewpoints.Single();

            Assert.NotNull(viewpoint.Camera);
            Assert.NotEmpty(viewpoint.ClippingPlanes);

            // A direction vector that is not a unit vector means the reader
            // misplaced the components
            Assert.Equal(1.0, viewpoint.Camera.Direction.Length, 3);
            Assert.Equal(1.0, viewpoint.Camera.UpVector.Length, 3);
        }

        [Fact]
        public void LegacyVisibility_SurvivesTheFlatComponentList()
        {
            // BCF 2.0 kept visibility as an attribute of a flat component list.
            // Without support for that layout the archive reads as topics with
            // no components at all — whole and meaningless
            BcfViewpoint viewpoint = Read(@"v2.0\SingleInvisibleWall.bcfzip")
                .Topics.Single().Viewpoints.Single();

            Assert.NotNull(viewpoint.Visibility);
            Assert.True(viewpoint.Visibility.DefaultVisibility);

            // One wall is hidden: in 2.0 that is an attribute of the component,
            // not a section of its own
            BcfComponent hidden = Assert.Single(viewpoint.Visibility.Exceptions);
            Assert.Equal("1E8YkwPMfB$h99jtn_uAjI", hidden.IfcGuid);
            Assert.Empty(viewpoint.Selection);
        }

        private static BcfReadResult Read(string relativePath)
        {
            string root = RepositoryPaths.FindRoot(AppContext.BaseDirectory);
            string path = Path.Combine(root, "test-data", "buildingsmart", relativePath);

            Assert.True(File.Exists(path), "Fixture not found: " + path);

            using (FileStream stream = File.OpenRead(path))
            {
                return BcfArchiveReader.Read(stream);
            }
        }
    }
}
