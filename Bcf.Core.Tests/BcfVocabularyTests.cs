using System.Collections.Generic;
using Bcf.Core;
using Bcf.Core.Vocabulary;
using Xunit;

namespace Bcf.Core.Tests
{
    public class BcfVocabularyTests
    {
        [Fact]
        public void Ensure_UnknownValue_Throws()
        {
            // Strict on the way out: a value outside the vocabulary raises an exception
            // while the topic is being built rather than travelling quietly into a file.
            var ex = Assert.Throws<BcfVocabularyException>(() => BcfVocabulary.EnsureTopicStatus("Открыто"));

            Assert.Equal("TopicStatus", ex.Field);
            Assert.Equal("Открыто", ex.Value);
        }

        [Fact]
        public void IsKnown_UnknownValue_ReturnsFalse_WithoutThrowing()
        {
            // Lenient on the way in: a file from BIMcollab or Revizto legitimately holds
            // statuses of its own; it must not be rejected, only the value marked foreign.
            Assert.False(BcfVocabulary.IsKnownTopicStatus("Open"));
            Assert.True(BcfVocabulary.IsKnownTopicStatus(BcfVocabulary.TopicStatuses.New));
        }

        [Theory]
        [InlineData("in progress")]
        [InlineData("INPROGRESS")]
        [InlineData("InProgress")]
        [InlineData(" In Progress")]
        public void Comparison_IsStrict(string value)
        {
            // Case and spaces count: "In Progress" equals none of these
            Assert.False(BcfVocabulary.IsKnownTopicStatus(value));
        }

        [Fact]
        public void MapNavisworksStatus_UsesDefaults()
        {
            Assert.Equal(BcfVocabulary.TopicStatuses.Assigned, BcfVocabulary.MapNavisworksStatus("Active"));
            Assert.Equal(BcfVocabulary.TopicStatuses.Closed, BcfVocabulary.MapNavisworksStatus("Approved"));
        }

        [Fact]
        public void MapNavisworksStatus_OverrideWins()
        {
            var overrides = new Dictionary<string, string>
            {
                { "Approved", BcfVocabulary.TopicStatuses.Rejected }
            };

            Assert.Equal(BcfVocabulary.TopicStatuses.Rejected, BcfVocabulary.MapNavisworksStatus("Approved", overrides));
            // The statuses that were not overridden keep following the defaults
            Assert.Equal(BcfVocabulary.TopicStatuses.Assigned, BcfVocabulary.MapNavisworksStatus("Active", overrides));
        }

        [Fact]
        public void MapNavisworksStatus_OverrideOutsideVocabulary_Throws()
        {
            var overrides = new Dictionary<string, string> { { "Approved", "Согласовано" } };

            Assert.Throws<BcfVocabularyException>(() => BcfVocabulary.MapNavisworksStatus("Approved", overrides));
        }

        [Fact]
        public void MapNavisworksStatus_UnknownStatus_ReturnsNull()
        {
            Assert.Null(BcfVocabulary.MapNavisworksStatus("Somebody else's status"));
        }

        [Fact]
        public void Transitions_FollowLifecycleModel()
        {
            Assert.True(BcfVocabulary.IsTransitionAllowed(BcfVocabulary.TopicStatuses.New, BcfVocabulary.TopicStatuses.Assigned));
            // Resolved is not terminal: a coordinator confirms it, not the assignee
            Assert.True(BcfVocabulary.IsTransitionAllowed(BcfVocabulary.TopicStatuses.Resolved, BcfVocabulary.TopicStatuses.Reopened));
            Assert.False(BcfVocabulary.IsTransitionAllowed(BcfVocabulary.TopicStatuses.New, BcfVocabulary.TopicStatuses.Resolved));
            Assert.False(BcfVocabulary.IsTransitionAllowed("Open", BcfVocabulary.TopicStatuses.Closed));
        }

        [Fact]
        public void RussianLabel_FallsBackToExternalValue()
        {
            Assert.Equal("В работе", BcfVocabulary.GetRussianLabel(BcfVocabulary.TopicStatuses.LabelsRu, BcfVocabulary.TopicStatuses.InProgress));
            // A foreign value is shown as it is — marked as an external status in the interface
            Assert.Equal("Open", BcfVocabulary.GetRussianLabel(BcfVocabulary.TopicStatuses.LabelsRu, "Open"));
        }

        [Fact]
        public void Defaults_MatchAgreedExportSettings()
        {
            Assert.Equal("Clash", BcfVocabulary.TopicTypes.Default);
            Assert.Equal("Normal", BcfVocabulary.Priorities.Default);
            Assert.Equal("Design", BcfVocabulary.Stages.Default);
        }

        [Fact]
        public void DisciplineAndSourceLabels_AreDistinctValues()
        {
            // SITE (the site-plan section) and Site (found on site) are different labels,
            // and only the case tells them apart. The comparison has to be strict.
            Assert.Equal("discipline", BcfVocabulary.TopicLabels.Groups["SITE"]);
            Assert.Equal("source", BcfVocabulary.TopicLabels.Groups["Site"]);
        }

        [Theory]
        [InlineData("coordinator@example.com", true)]
        [InlineData("Coordinator@Example.COM", true)]
        [InlineData("Иванов", false)]
        [InlineData("ОВ / Петров", false)]
        [InlineData("no-at-sign", false)]
        [InlineData("@example.com", false)]
        [InlineData("user@localhost", false)]
        public void UserLooksLikeEmail(string value, bool expected)
        {
            Assert.Equal(expected, BcfUsers.LooksLikeEmail(value));
        }

        [Fact]
        public void Users_AreLowercased_Deduplicated_AndNonEmailsReported()
        {
            IReadOnlyList<string> skipped;
            IReadOnlyList<string> users = BcfUsers.Normalize(
                new[] { "Coordinator@Example.com", "coordinator@example.com", "Иванов (ОВ)", "  ", null, "hvac@example.com" },
                out skipped);

            Assert.Equal(new[] { "coordinator@example.com", "hvac@example.com" }, users);
            // The skipped values reach the final export report rather than vanishing
            // quietly: the mapping of assignees is incomplete
            Assert.Equal(new[] { "Иванов (ОВ)" }, skipped);
        }
    }
}
