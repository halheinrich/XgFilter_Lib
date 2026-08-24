using XgFilter_Lib.Enums;
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
        var filter = new MatchScoreFilter(["1a5aC", "moneyJ"]);
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
    public void Matches_MoneyNotInList_ReturnsFalse()
    {
        // A match-score-only filter admits no money record, whatever its rule.
        var filter = new MatchScoreFilter(["3a5a"]);
        foreach (bool? rule in new bool?[] { true, false, null })
        {
            AssertMatchesBoth(filter, MoneyRow(rule), expected: false);
        }
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
    //  IMatchFilter: ShouldSkipMatch — IMatchInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_MoneySession_FilterHasNoMoney_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 0 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MoneySession_FilterIncludesMoney_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["moneyJ"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 0 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_FilterIsMoneyOnly_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["moneyJ", "moneyNJ"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_TargetsExceedMatchLength_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["7a7a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_AtLeastOneTargetReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_SwappedOrientationBoundsIdentically_ReturnsFalse()
    {
        // The length bound is orientation-free: 5a3a fits a 5-point match
        // exactly as 3a5a does.
        var filter = new MatchScoreFilter(["5a3a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

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
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_PostCrawfordTargetBelowMatchLength_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a4a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_CrawfordTargetAtMatchLength_ReturnsFalse()
    {
        // Crawford (1, L, true) is reachable: the leader hits 1-away while
        // the trailer still needs the full match length. Only the
        // non-Crawford 1-away family is capped at L - 1.
        var filter = new MatchScoreFilter(["1a5aC"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 5 };

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
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 1 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_TwoPointMatch_1a2a_ReturnsTrue()
    {
        // In a 2-point match the post-Crawford family is only (1,1):
        // (1, 2, false) would need a Crawford game at (1, k) with k > 2 > L.
        var filter = new MatchScoreFilter(["1a2a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 2 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_TwoPointMatch_1a1a_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a1a"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 2 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_MoneyGame_FilterHasNoMoney_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var game = new FakeGameInfo { Away1 = 0, Away2 = 0, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_MoneyGame_FilterIncludesMoney_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["moneyJ"]);
        var game = new FakeGameInfo { Away1 = 0, Away2 = 0, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_ScoreMatchesTarget_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var game = new FakeGameInfo { Away1 = 3, Away2 = 5, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_ScoreMissesTarget_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        var game = new FakeGameInfo { Away1 = 2, Away2 = 4, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_CrawfordMismatch_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new FakeGameInfo { Away1 = 1, Away2 = 5, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_CrawfordMatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new FakeGameInfo { Away1 = 1, Away2 = 5, IsCrawfordGame = true };

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
        var game = new FakeGameInfo { Away1 = 5, Away2 = 4, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_SwappedOrientationCrawford_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new FakeGameInfo { Away1 = 5, Away2 = 1, IsCrawfordGame = true };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_SwappedOrientationCrawfordFlagMismatch_ReturnsTrue()
    {
        // Orientation is projected loosely (either order) but the Crawford
        // flag stays exact — it is game-level information the header knows.
        var filter = new MatchScoreFilter(["1a5aC"]);
        var game = new FakeGameInfo { Away1 = 5, Away2 = 1, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  IDecisionFilter: ShouldAdvanceMatch — mid-stream, exercised on both
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceMatch_MoneyRow_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["moneyJ"]);
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
        var filter = new MatchScoreFilter(["moneyJ", "moneyNJ"]);
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
        var act = () => new MatchScoreFilter(["3a5a", "garbage", "moneyJ"]);
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
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    [InlineData("MONEYJ")]    // the money tokens follow the grammar's casing rule
    [InlineData("moneynj")]
    [InlineData("MoNeYnJ")]
    [InlineData(" moneyNJ ")] // ...and its trimming rule
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

    // -----------------------------------------------------------------------
    //  The money tokens: moneyJ / moneyNJ (halheinrich/backgammon#121)
    //
    //  The record classes a score filter must tell apart — a moneyJ record, a
    //  moneyNJ record, an unknown-rule money record, and a match record (with
    //  its Crawford variant) — swept against every money-token selection. The
    //  table below is the whole contract in one place: moneyJ admits
    //  IsMoneyGame && IsJacoby == true, moneyNJ admits IsMoneyGame &&
    //  IsJacoby == false, an unknown rule is admitted by neither, and no
    //  money token ever admits a match record.
    // -----------------------------------------------------------------------

    /// <summary>A money row carrying <paramref name="isJacoby"/> as its rule.</summary>
    private static RowShape MoneyRow(bool? isJacoby) => new(
        MatchLength: 0, OnRollNeeds: 0, OpponentNeeds: 0,
        IsCrawford: false, IsJacoby: isJacoby);

    public static TheoryData<string[], bool?, bool> MoneyTokenMatrix() => new()
    {
        // moneyJ alone: the Jacoby record only.
        { ["moneyJ"],            true,  true  },
        { ["moneyJ"],            false, false },
        { ["moneyJ"],            null,  false },

        // moneyNJ alone: the no-Jacoby record only.
        { ["moneyNJ"],           true,  false },
        { ["moneyNJ"],           false, true  },
        { ["moneyNJ"],           null,  false },

        // Both listed — "money under either rule", which is what the old bare
        // token used to mean. Still not the unknown-rule record.
        { ["moneyJ", "moneyNJ"], true,  true  },
        { ["moneyJ", "moneyNJ"], false, true  },
        { ["moneyJ", "moneyNJ"], null,  false },
    };

    [Theory]
    [MemberData(nameof(MoneyTokenMatrix))]
    public void Matches_MoneyRecords_AdmittedByRuleBearingTokenOnly(
        string[] tokens, bool? isJacoby, bool expected)
    {
        var filter = new MatchScoreFilter(tokens);
        AssertMatchesBoth(filter, MoneyRow(isJacoby), expected);
    }

    [Theory]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void Matches_MoneyToken_NeverAdmitsAMatchRecord(string token)
    {
        // Match scores are untouched by the money tokens, and vice versa:
        // neither token admits a match row at any score, Crawford or not.
        var filter = new MatchScoreFilter([token]);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false),
            expected: false);
        AssertMatchesBoth(
            filter,
            new RowShape(OnRollNeeds: 1, OpponentNeeds: 5, IsCrawford: true),
            expected: false);
    }

    [Theory]
    [InlineData("moneyJ", true)]
    [InlineData("moneyNJ", false)]
    public void Matches_MoneyToken_IsIndifferentToCubeAndPlayRows(string token, bool rule)
    {
        // The score facet reads only the score axis; the decision-type axis is
        // DecisionTypeFilter's, composed by AND at the set. A money record's
        // verdict is therefore the same for a cube decision and a checker
        // play — the existing per-facet independence, restated for the new
        // tokens so a future money-only special case cannot quietly break it.
        var filter = new MatchScoreFilter([token]);

        AssertMatchesBoth(filter, MoneyRow(rule) with { IsCube = false }, expected: true);
        AssertMatchesBoth(filter, MoneyRow(rule) with { IsCube = true }, expected: true);
    }

    // -----------------------------------------------------------------------
    //  The near-miss pins. IDecisionFilterData.IsJacoby names `!= false` and
    //  `!= true` as the spellings that silently admit an unknown-rule record
    //  into one side. These are the tests that fail if Matches is ever written
    //  that way — and nothing else in the matrix above would.
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_UnknownRuleMoneyRecord_AdmittedByNeitherToken()
    {
        // `_includesMoneyWithJacoby && IsJacoby != false` would pass this
        // record; the `== true` spelling is what rejects it.
        AssertMatchesBoth(new MatchScoreFilter(["moneyJ"]), MoneyRow(null), expected: false);

        // ...and the mirror: `!= true` would pass it here.
        AssertMatchesBoth(new MatchScoreFilter(["moneyNJ"]), MoneyRow(null), expected: false);

        // Not even both tokens together admit it. An unknown rule is never
        // guessed into a side (halheinrich/backgammon#142 — the illegal state
        // is upstream's to prevent; the filter simply never admits it).
        AssertMatchesBoth(
            new MatchScoreFilter(["moneyJ", "moneyNJ"]), MoneyRow(null), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Header gates: IMatchInfo / IGameInfo carry no Jacoby fact, so a money
    //  header is admissible iff EITHER money token is listed. Exact for the
    //  information those headers carry; Matches stays the rule arbiter.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void ShouldSkipMatch_MoneySession_EitherMoneyToken_ReturnsFalse(string token)
    {
        var filter = new MatchScoreFilter([token]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 0 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    [Theory]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void ShouldSkipGame_MoneyGame_EitherMoneyToken_ReturnsFalse(string token)
    {
        var filter = new MatchScoreFilter([token]);
        var game = new FakeGameInfo { Away1 = 0, Away2 = 0, IsCrawfordGame = false };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_SingleMoneyToken_ReturnsTrue()
    {
        // A money-token-only filter carries no tuples, so no match session can
        // satisfy it — unchanged by the token split.
        var filter = new MatchScoreFilter(["moneyNJ"]);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  The retirement of the bare money token
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("money")]
    [InlineData("MONEY")]
    [InlineData("Money")]
    public void Constructor_RetiredMoneyToken_Throws(string retired)
    {
        // Never a silent no-match and never a silent reinterpretation as one
        // of the two rule-bearing tokens: the retired spelling is rejected the
        // way any other unusable token is, so no filter is ever built from it
        // and no money row of any rule can ride through on it.
        var act = () => new MatchScoreFilter([retired]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_RetiredMoneyToken_SurroundingWhitespaceStillThrows()
    {
        // The grammar trims before judging, so a stored token that picked up
        // whitespace is still recognized as the retired one — it does not slip
        // out to the format rejection and lose its specific verdict.
        var act = () => new MatchScoreFilter([" money "]);
        act.Should().Throw<ArgumentException>();

        MatchScoreToken.GetFault(" money ").Should().Be(MatchScoreTokenFault.Retired);
    }

    // -----------------------------------------------------------------------
    //  Grammar / filter agreement: the constructor throws on exactly the
    //  tokens MatchScoreToken.GetFault faults, so GetInvalidFields and Build
    //  cannot disagree about any token.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("3a5a")]
    [InlineData("1a5aC")]
    [InlineData("1a1a")]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    [InlineData(" MONEYNJ ")]
    [InlineData("money")]
    [InlineData("MONEY")]
    [InlineData("garbage")]
    [InlineData("")]
    [InlineData("0a5a")]
    [InlineData("3a5aC")]
    [InlineData("1a1aC")]
    [InlineData("4 a 5a")]
    public void Constructor_ThrowsExactlyWhenGetFaultFaults(string token)
    {
        var act = () => new MatchScoreFilter([token]);

        if (MatchScoreToken.GetFault(token) == MatchScoreTokenFault.None)
            act.Should().NotThrow();
        else
            act.Should().Throw<ArgumentException>();
    }
}
