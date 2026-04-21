using ConvertXgToJson_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;
using static XgFilter_Lib.Tests.Helpers.DecisionFilterAsserts;

namespace XgFilter_Lib.Tests.Filtering;

public class MatchScoreFilterTests
{
    // -----------------------------------------------------------------------
    //  Matches
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenScoreInList_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC", "money"]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenScoreNotInList_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 2, OpponentNeeds: 4, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void Matches_MoneyGame()
    {
        var filter = new MatchScoreFilter(["money"]);
        AssertMatchesBoth(
            filter,
            new RowShape(MatchLength: 0, OnRollNeeds: 0, OpponentNeeds: 0, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void Matches_MoneyNotInList_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertMatchesBoth(
            filter,
            new RowShape(MatchLength: 0, OnRollNeeds: 0, OpponentNeeds: 0, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void Matches_WhenListIsEmpty_ReturnsFalse()
    {
        var filter = new MatchScoreFilter([]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void Matches_NonCrawfordScore()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void Matches_CrawfordMismatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipMatch — XgMatchInfo input, no substrate axis
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

    // -----------------------------------------------------------------------
    //  IDecisionFilter: ShouldAdvanceMatch — mid-stream, exercised on both
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceMatch_MoneyRow_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["money"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(MatchLength: 0, OnRollNeeds: 0, OpponentNeeds: 0, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_FutureTupleReachable_ReturnsFalse()
    {
        // Current (5,5). Tuple (3,5) reachable if on-roll side wins the next game.
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 5, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_NoTupleReachable_ReturnsTrue()
    {
        // Current (2,2). Tuple (5,5) unreachable — both axes exceed current.
        var filter = new MatchScoreFilter(["5a5a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 2, OpponentNeeds: 2, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_SwappedPerspectiveReachable_ReturnsFalse()
    {
        // Current (5,3). Tuple (2,4) fits only with swap: 4 <= 5, 2 <= 3, sum 6 < 8.
        var filter = new MatchScoreFilter(["2a4a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 5, OpponentNeeds: 3, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CurrentTupleOnly_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CrawfordTupleReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a3aC"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CrawfordTupleOutOfRange_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a7aC"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_CrawfordTupleOnly_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_PostCrawfordTupleReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a3a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_CrawfordRequired_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_SmallerReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a2a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_NonPostCrawfordTuple_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["2a3a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_MultipleTuples_AnyReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a", "10a10a", "1a2a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_MoneyFilterWithMatchRow_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["money"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }
}
