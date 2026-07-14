using ConvertXgToJson_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

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

    [Fact]
    public void Matches_IsOnRollAnchored_MirrorOrientationDoesNotMatch()
    {
        // MaNa is on-roll anchored: 4a5a means the player on roll needs 4
        // and the opponent needs 5. 4a5a and 5a4a are distinct targets.
        var filter = new MatchScoreFilter(["4a5a"]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 4, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 5, OpponentNeeds: 4, IsCrawford: false),
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

    [Fact]
    public void ShouldSkipMatch_SwappedOrientationBoundsIdentically_ReturnsFalse()
    {
        // The length bound is orientation-free: 5a3a fits a 5-point match
        // exactly as 3a5a does.
        var filter = new MatchScoreFilter(["5a3a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_PostCrawfordTargetAtMatchLength_ReturnsTrue()
    {
        // (1, m, false) exists only after a Crawford game (1, k, true) where
        // the trailer won at least one point, so m < k <= L. 1a5a can never
        // occur in a 5-point match; the naive "both sides <= L" bound
        // over-admitted it.
        var filter = new MatchScoreFilter(["1a5a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_PostCrawfordTargetBelowMatchLength_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a4a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_CrawfordTargetAtMatchLength_ReturnsFalse()
    {
        // Crawford (1, L, true) is reachable: the leader hits 1-away while
        // the trailer still needs the full match length. Only the
        // non-Crawford 1-away family is capped at L - 1.
        var filter = new MatchScoreFilter(["1a5aC"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_OnePointMatch_1a1a_ReturnsFalse()
    {
        // The one exception to the "max <= L - 1" rule for 1-away targets:
        // a 1-point match's only game is (1, 1, false) with no Crawford game
        // before it. 1a1a is never Crawford — the substrate rule settled in
        // BgGame_Lib: a (1,1) game is cubeless. Empirical note: whether XG
        // stamps CrawfordApplies on a 1-point match's game header is unpinned
        // (no 1-point fixture in the corpus at time of writing); if it does,
        // those rows carry IsCrawford=true and match no valid token, so this
        // header-level admit stays harmless.
        var filter = new MatchScoreFilter(["1a1a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 1 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_TwoPointMatch_1a2a_ReturnsTrue()
    {
        // In a 2-point match the post-Crawford family is only (1,1):
        // (1, 2, false) would need a Crawford game at (1, k) with k > 2 > L.
        var filter = new MatchScoreFilter(["1a2a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 2 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_TwoPointMatch_1a1a_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a1a"]);
        var match = new XgMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 2 };

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

    [Fact]
    public void ShouldSkipGame_SwappedOrientation_ReturnsFalse()
    {
        // The shipped 4a5a bug: game headers are player1/player2-anchored,
        // tuples are on-roll anchored, and both players roll within a game —
        // a game at (Away1=5, Away2=4) yields decisions scored (5,4) AND
        // (4,5). The game gate must admit either orientation; Matches stays
        // the per-decision arbiter.
        var filter = new MatchScoreFilter(["4a5a"]);
        var game = new XgGameInfo { Away1 = 5, Away2 = 4, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_SwappedOrientationCrawford_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new XgGameInfo { Away1 = 5, Away2 = 1, IsCrawfordGame = true };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_SwappedOrientationCrawfordFlagMismatch_ReturnsTrue()
    {
        // Orientation is projected loosely (either order) but the Crawford
        // flag stays exact — it is game-level information the header knows.
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new XgGameInfo { Away1 = 5, Away2 = 1, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
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
    public void ShouldAdvanceMatch_PreCrawford_CurrentGameStillMatches_ReturnsFalse()
    {
        // The producer cuts the file immediately on a true vote — including
        // the rest of the CURRENT game, whose later decisions carry this
        // exact score. Advancing here would drop them. (Previously expected
        // true: the old reachability was future-games-only and forgot the
        // current game's remaining rows.)
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CurrentGameMirrorOrientation_ReturnsFalse()
    {
        // Current row is (5,4), but the same game's later decisions include
        // the mirror (4,5) whenever the other player is on roll — the 4a5a
        // orientation bug in its mid-.xg form.
        var filter = new MatchScoreFilter(["4a5a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 5, OpponentNeeds: 4, IsCrawford: false),
            expected: false);
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
    public void ShouldAdvanceMatch_InCrawford_CrawfordTupleMatchesCurrentGame_ReturnsFalse()
    {
        // The current game IS the Crawford game the tuple names; its
        // remaining decisions can still match. (Previously expected true —
        // same future-games-only oversight as the pre-Crawford case.)
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_MirrorCrawfordTuple_ReturnsFalse()
    {
        // Mirror orientation of the current Crawford game: the leader's
        // decisions score (1,5,C), the trailer's (5,1,C).
        var filter = new MatchScoreFilter(["5a1aC"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: false);
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

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_PostCrawfordTupleAtBound_ReturnsTrue()
    {
        // The reachability over-admission fix: from (2,5) the post-Crawford
        // family is (1, m, false) with m < max(2,5) = 5 — reaching (1,5)
        // would need a Crawford game at (1, k > 5), impossible. The old
        // generic fits/strict-sum path admitted it (1 <= 2, 5 <= 5, 6 < 7).
        var filter = new MatchScoreFilter(["1a5a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 2, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_PostCrawfordTupleBelowBound_ReturnsFalse()
    {
        // (1,4,false) from (2,5): Crawford at (1,5), trailer wins one point.
        var filter = new MatchScoreFilter(["1a4a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 2, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_OneAwayOneAwayTuple_ReturnsFalse()
    {
        // (1,1,false) is reachable from any pre-Crawford state: Crawford at
        // (1,2), then the trailer wins a point.
        var filter = new MatchScoreFilter(["1a1a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 2, OpponentNeeds: 2, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_AtOneAwayOneAway_TupleIsCurrentGame_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a1a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 1, IsCrawford: false),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_AtOneAwayOneAway_NothingReachable_ReturnsTrue()
    {
        // From (1,1) the match ends with the current game; no future game
        // exists and the current game's score is not the target.
        var filter = new MatchScoreFilter(["1a2a"]);
        AssertShouldAdvanceMatchBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 1, IsCrawford: false),
            expected: true);
    }

    // -----------------------------------------------------------------------
    //  Constructor — invalid score tokens fail fast
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("3a5x")]      // non-numeric tail
    [InlineData("garbage")]   // no 'a' separator
    [InlineData("5a")]        // missing second number
    [InlineData("a5a")]       // missing first number
    [InlineData("")]          // empty (also "" after trim)
    [InlineData("-1a5a")]     // a sign is a format error, not a ≥ 1 semantic one
    [InlineData("4aa5a")]     // doubled separator — old Split swallowed the empty
    [InlineData("a4a5a")]     // leading separator
    [InlineData("4a5aa")]     // doubled trailing away-marker
    [InlineData("4Aa5a")]     // separator then a second, numberless separator
    [InlineData("4 a 5a")]    // embedded whitespace (trim only strips the ends)
    public void Constructor_InvalidScoreString_Throws(string bad)
    {
        var act = () => new MatchScoreFilter([bad]);
        act.Should().Throw<ArgumentException>()
            .WithMessage($"*{bad}*",
                "the offending input must appear in the message so the consumer can locate the typo");
    }

    [Fact]
    public void Constructor_MixedValidAndInvalid_Throws()
    {
        // One bad entry contaminates the whole list — silent drop of the
        // invalid one would leave the consumer with a filter that quietly
        // ignores their typo instead of telling them about it.
        var act = () => new MatchScoreFilter(["3a5a", "garbage", "money"]);
        act.Should().Throw<ArgumentException>().WithMessage("*garbage*");
    }

    [Theory]
    [InlineData("0a5a")]     // a 0-away side has already won the match
    [InlineData("5a0a")]
    [InlineData("0a0a")]
    public void Constructor_NonPositiveAwayScore_Throws(string bad)
    {
        // A well-formed token (grammar accepts the digits) whose away score is
        // 0 previously parsed into a dead tuple that could never match a row —
        // exactly the silent "filter does nothing" failure the fail-loud
        // philosophy exists to prevent. This routes through the ≥ 1 semantic
        // message, distinct from the format rejection above (a negative sign
        // never reaches here — \d+ makes it a format error).
        var act = () => new MatchScoreFilter([bad]);
        act.Should().Throw<ArgumentException>().WithMessage($"*{bad}*");
    }

    [Theory]
    [InlineData("3a5aC")]    // Crawford requires a side at 1-away
    [InlineData("5a3aC")]
    [InlineData("1a1aC")]    // a (1,1) game is always post-Crawford
    public void Constructor_ImpossibleCrawfordScore_Throws(string bad)
    {
        var act = () => new MatchScoreFilter([bad]);
        act.Should().Throw<ArgumentException>().WithMessage($"*{bad}*");
    }

    [Theory]
    [InlineData("1a2aC")]    // Crawford, leader on roll
    [InlineData("2a1aC")]    // Crawford, trailer on roll
    [InlineData("1a1a")]     // post-Crawford tie, and a 1-point match's only game
    [InlineData("1a5ac")]    // lowercase Crawford suffix
    [InlineData("4A5A")]     // uppercase away-separators (case-insensitive grammar)
    [InlineData("1a5Ac")]    // mixed-case separator + lowercase Crawford suffix
    [InlineData("1A5aC")]    // mixed-case separator + uppercase Crawford suffix
    [InlineData(" 4a5a ")]   // surrounding whitespace is trimmed before parsing
    [InlineData("money")]
    [InlineData("MONEY")]
    public void Constructor_ValidTokens_DoNotThrow(string good)
    {
        var act = () => new MatchScoreFilter([good]);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData("4A5A")]     // uppercase separators
    [InlineData(" 4a5a ")]   // surrounding whitespace
    public void ParseScore_CaseAndWhitespaceVariants_MatchSameAsCanonical(string variant)
    {
        // The grammar is case-insensitive and trims incidental whitespace, so
        // each variant is equivalent in effect to its canonical "4a5a" form —
        // it admits exactly the (OnRoll 4, Opp 5) row and rejects the mirror.
        var filter = new MatchScoreFilter([variant]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 4, OpponentNeeds: 5, IsCrawford: false),
            expected: true);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 5, OpponentNeeds: 4, IsCrawford: false),
            expected: false);
    }

    [Theory]
    [InlineData("1a5Ac")]    // mixed-case separator + lowercase Crawford
    [InlineData("1A5aC")]    // mixed-case separator + uppercase Crawford
    public void ParseScore_MixedCaseCrawfordVariants_MatchSameAsCanonical(string variant)
    {
        // Equivalent in effect to the canonical "1a5aC": admits the Crawford
        // (OnRoll 1, Opp 5) row, rejects the same score with the Crawford flag
        // off.
        var filter = new MatchScoreFilter([variant]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: true);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
    }
}
