using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class MatchScoreFilterTests
{
    [Fact]
    public void Matches_WhenScoreInList_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC", "money"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: true);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenScoreNotInList_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 2, opponentNeeds: 4, isCrawford: false);

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_MoneyGame()
    {
        var filter = new MatchScoreFilter(["money"]);
        var row = DecisionRowBuilder.Build(matchLength: 0, onRollNeeds: 0, opponentNeeds: 0, isCrawford: false);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_MoneyNotInList_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var row = DecisionRowBuilder.Build(matchLength: 0, onRollNeeds: 0, opponentNeeds: 0, isCrawford: false);

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenListIsEmpty_ReturnsFalse()
    {
        var filter = new MatchScoreFilter([]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_NonCrawfordScore()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_CrawfordMismatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: false);

        filter.Matches(row).Should().BeFalse();
    }
}