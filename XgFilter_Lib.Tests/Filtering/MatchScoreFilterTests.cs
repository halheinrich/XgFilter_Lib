using ConvertXgToJson_Lib;
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

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipMatch
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_MoneySession_FilterHasNoMoney_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 0 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MoneySession_FilterIncludesMoney_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["money"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 0 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_FilterIsMoneyOnly_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["money"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_TargetsExceedMatchLength_ReturnsTrue()
    {
        // 5-point match but filter only asks for 7-away scores
        var filter = new MatchScoreFilter(["7a7a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_AtLeastOneTargetReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_MoneyGame_FilterHasNoMoney_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var game = new XgGameInfo { Away1 = 0, Away2 = 0, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_MoneyGame_FilterIncludesMoney_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["money"]);
        var game = new XgGameInfo { Away1 = 0, Away2 = 0, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_ScoreMatchesTarget_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var game = new XgGameInfo { Away1 = 3, Away2 = 5, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_ScoreMissesTarget_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var game = new XgGameInfo { Away1 = 2, Away2 = 4, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_CrawfordMismatch_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new XgGameInfo { Away1 = 1, Away2 = 5, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_CrawfordMatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new XgGameInfo { Away1 = 1, Away2 = 5, IsCrawfordGame = true };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }
}