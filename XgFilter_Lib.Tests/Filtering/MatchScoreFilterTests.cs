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

    // -----------------------------------------------------------------------
    //  IDecisionFilter: ShouldAdvanceMatch — mid-stream early-exit
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceMatch_MoneyRow_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["money"]);
        var row = DecisionRowBuilder.Build(matchLength: 0, onRollNeeds: 0, opponentNeeds: 0, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_FutureTupleReachable_ReturnsFalse()
    {
        // Current (5,5). Tuple (3,5) reachable if on-roll side wins the next game.
        var filter = new MatchScoreFilter(["3a5a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 5, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_NoTupleReachable_ReturnsTrue()
    {
        // Current (2,2). Tuple (5,5) unreachable — both axes exceed current.
        var filter = new MatchScoreFilter(["5a5a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 2, opponentNeeds: 2, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_SwappedPerspectiveReachable_ReturnsFalse()
    {
        // Current (5,3). Tuple (2,4) fits only with swap: 4 <= 5, 2 <= 3, sum 6 < 8.
        var filter = new MatchScoreFilter(["2a4a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 5, opponentNeeds: 3, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CurrentTupleOnly_ReturnsTrue()
    {
        // Current (3,5) matches the only tuple. No future game can revisit it
        // (strict monotone decrease), so nothing further is reachable.
        var filter = new MatchScoreFilter(["3a5a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CrawfordTupleReachable_ReturnsFalse()
    {
        // Current (3,5) pre-Crawford; Crawford (1,3,C) is reachable since
        // 3 <= max(3,5) when a side drops to 1-away.
        var filter = new MatchScoreFilter(["1a3aC"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CrawfordTupleOutOfRange_ReturnsTrue()
    {
        // Current (3,5) pre-Crawford; Crawford (1,7,C) unreachable — the
        // non-1 side of the Crawford state can't exceed max(ca,cb)=5.
        var filter = new MatchScoreFilter(["1a7aC"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_CrawfordTupleOnly_ReturnsTrue()
    {
        // Currently in Crawford (1,5,true); Crawford is unique per match so
        // no other Crawford state is reachable.
        var filter = new MatchScoreFilter(["1a5aC"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: true);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_PostCrawfordTupleReachable_ReturnsFalse()
    {
        // From Crawford (1,5,true) the continuing post-Crawford state (1,3,false)
        // is reachable when the trailer wins Crawford by 2.
        var filter = new MatchScoreFilter(["1a3a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: true);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_CrawfordRequired_ReturnsTrue()
    {
        // Current (1,5,false) is post-Crawford — Crawford already happened,
        // so a Crawford tuple is unreachable.
        var filter = new MatchScoreFilter(["1a5aC"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_SmallerReachable_ReturnsFalse()
    {
        // Post-Crawford (1,5,false); continuing state (1,2,false) still reachable.
        var filter = new MatchScoreFilter(["1a2a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_NonPostCrawfordTuple_ReturnsTrue()
    {
        // Post-Crawford (1,5,false); can't return to a pre-Crawford state.
        var filter = new MatchScoreFilter(["2a3a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 1, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }

    [Fact]
    public void ShouldAdvanceMatch_MultipleTuples_AnyReachable_ReturnsFalse()
    {
        // Current (3,5) pre-Crawford. (10,10) unreachable, (1,2) reachable — one
        // reachable tuple is enough to keep iterating.
        var filter = new MatchScoreFilter(["3a5a", "10a10a", "1a2a"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeFalse();
    }

    [Fact]
    public void ShouldAdvanceMatch_MoneyFilterWithMatchRow_ReturnsTrue()
    {
        // Match-play row (3,5) with a money-only filter: no match tuple can
        // ever match (ShouldSkipMatch already returns true at file level, but
        // defensive reachability still reports unreachable).
        var filter = new MatchScoreFilter(["money"]);
        var row = DecisionRowBuilder.Build(onRollNeeds: 3, opponentNeeds: 5, isCrawford: false);

        filter.ShouldAdvanceMatch(row).Should().BeTrue();
    }
}