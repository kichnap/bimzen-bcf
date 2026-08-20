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
            // Строго на выход: значение вне справочника — исключение на этапе
            // сборки топика, а не молчаливая запись в файл.
            var ex = Assert.Throws<BcfVocabularyException>(() => BcfVocabulary.EnsureTopicStatus("Открыто"));

            Assert.Equal("TopicStatus", ex.Field);
            Assert.Equal("Открыто", ex.Value);
        }

        [Fact]
        public void IsKnown_UnknownValue_ReturnsFalse_WithoutThrowing()
        {
            // Терпимо на вход: файл из BIMcollab или Revizto законно содержит
            // свои статусы, отвергать его нельзя — только пометить значение чужим.
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
            // Сравнение с регистром и пробелами: "In Progress" не равно ничему из этого
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
            // Не переопределённые статусы продолжают идти по дефолтам
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
            // Resolved не конечный: подтверждает координатор, а не исполнитель
            Assert.True(BcfVocabulary.IsTransitionAllowed(BcfVocabulary.TopicStatuses.Resolved, BcfVocabulary.TopicStatuses.Reopened));
            Assert.False(BcfVocabulary.IsTransitionAllowed(BcfVocabulary.TopicStatuses.New, BcfVocabulary.TopicStatuses.Resolved));
            Assert.False(BcfVocabulary.IsTransitionAllowed("Open", BcfVocabulary.TopicStatuses.Closed));
        }

        [Fact]
        public void RussianLabel_FallsBackToExternalValue()
        {
            Assert.Equal("В работе", BcfVocabulary.GetRussianLabel(BcfVocabulary.TopicStatuses.LabelsRu, BcfVocabulary.TopicStatuses.InProgress));
            // Чужое значение показывается как есть — с пометкой «внешний статус» в UI
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
            // SITE (раздел ГП) и Site (выявлено на площадке) — разные метки,
            // и различает их только регистр. Сравнение обязано быть строгим.
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
            // Пропущенные значения попадут в итоговый отчёт экспорта,
            // а не исчезнут молча: сопоставление исполнителей неполное
            Assert.Equal(new[] { "Иванов (ОВ)" }, skipped);
        }
    }
}
