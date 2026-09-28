using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class MatchScoreFilterTests
{
    // -----------------------------------------------------------------------
    //  Records — each session built as a producer builds one
    //  (TestRecords.MatchSession / MoneySession, through Session.Create), so
    //  every standing here is one a real record can hold.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A checker play in a match of <paramref name="length"/> points, standing
    /// from the player on roll's side: the player on roll needs
    /// <paramref name="onRollNeeds"/>, the opponent
    /// <paramref name="opponentNeeds"/>.
    /// </summary>
    private static CheckerPlayDecision AtScore(
        int onRollNeeds, int opponentNeeds, bool isCrawford = false, int length = 7) =>
        TestRecords.CheckerPlay(position: TestRecords.Position(
            session: TestRecords.MatchSession(length, onRollNeeds, opponentNeeds, isCrawford)));

    /// <summary>A checker play in a money session under <paramref name="isJacoby"/>.</summary>
    private static CheckerPlayDecision Money(bool isJacoby, int onRollScore = 0, int opponentScore = 0) =>
        TestRecords.CheckerPlay(position: TestRecords.Position(
            session: TestRecords.MoneySession(
                isJacoby: isJacoby, onRollScore: onRollScore, opponentScore: opponentScore)));

    /// <summary>A cube decision in a money session under <paramref name="isJacoby"/>.</summary>
    private static CubeDecision MoneyCube(bool isJacoby) =>
        TestRecords.Cube(position: TestRecords.Position(
            session: TestRecords.MoneySession(isJacoby: isJacoby)));

    // -----------------------------------------------------------------------
    //  Matches
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenScoreInList_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC", "moneyJ"]);
        AssertMatchesBoth(filter, AtScore(1, 5, isCrawford: true), expected: true);
    }

    [Fact]
    public void Matches_WhenScoreNotInList_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertMatchesBoth(filter, AtScore(2, 4), expected: false);
    }

    [Fact]
    public void Matches_MoneyNotInList_ReturnsFalse()
    {
        // A match-score-only filter admits no money session, whatever its rule.
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertMatchesBoth(filter, Money(isJacoby: true), expected: false);
        AssertMatchesBoth(filter, Money(isJacoby: false), expected: false);
    }

    [Fact]
    public void Matches_WhenListIsEmpty_ReturnsFalse()
    {
        var filter = new MatchScoreFilter([]);
        AssertMatchesBoth(filter, AtScore(3, 5), expected: false);
    }

    [Fact]
    public void Matches_NonCrawfordScore()
    {
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertMatchesBoth(filter, AtScore(3, 5), expected: true);
    }

    [Fact]
    public void Matches_CrawfordMismatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertMatchesBoth(filter, AtScore(1, 5, isCrawford: false), expected: false);
    }

    [Fact]
    public void Matches_IsOnRollAnchored_MirrorOrientationDoesNotMatch()
    {
        // MaNa is on-roll anchored: 4a5a means the player on roll needs 4
        // and the opponent needs 5. 4a5a and 5a4a are distinct targets.
        var filter = new MatchScoreFilter(["4a5a"]);
        AssertMatchesBoth(filter, AtScore(4, 5), expected: true);
        AssertMatchesBoth(filter, AtScore(5, 4), expected: false);
    }

    [Fact]
    public void Matches_MatchLength_IsNotPartOfTheTarget()
    {
        // A score token names the standing, not the terms: 3a5a is 3a5a in a
        // 5-point match and in a 25-point one alike.
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertMatchesBoth(filter, AtScore(3, 5, length: 5), expected: true);
        AssertMatchesBoth(filter, AtScore(3, 5, length: 25), expected: true);
    }

    [Fact]
    public void Matches_MoneySessionsScores_AreNeverReadAsAwayScores()
    {
        // Money versus match is the session's kind (halheinrich/backgammon#273):
        // a money session at 3-5 won points is not a 3a5a match standing, and
        // a filter reading a stand-in off it — its scores as away scores —
        // would admit it here.
        var filter = new MatchScoreFilter(["3a5a", "5a3a"]);
        AssertMatchesBoth(filter, Money(isJacoby: true, onRollScore: 3, opponentScore: 5), expected: false);
        AssertMatchesBoth(filter, Money(isJacoby: false, onRollScore: 3, opponentScore: 5), expected: false);
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipMatch — IMatchInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_MoneySession_FilterHasNoMoney_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Money(isJacoby: true)).Should().BeTrue();
        filter.ShouldSkipMatch(FakeMatchInfo.Money(isJacoby: false)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MoneySession_FilterIncludesItsRule_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["moneyJ"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Money(isJacoby: true)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_FilterIsMoneyOnly_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["moneyJ", "moneyNJ"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_TargetsExceedMatchLength_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["7a7a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(5)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_AtLeastOneTargetReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_SwappedOrientationBoundsIdentically_ReturnsFalse()
    {
        // The length bound is orientation-free: 5a3a fits a 5-point match
        // exactly as 3a5a does.
        var filter = new MatchScoreFilter(["5a3a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(5)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_PostCrawfordTargetAtMatchLength_ReturnsTrue()
    {
        // (1, m, false) exists only after a Crawford game (1, k, true) where
        // the trailer won at least one point, so m < k <= L. 1a5a can never
        // occur in a 5-point match; the naive "both sides <= L" bound
        // over-admitted it.
        var filter = new MatchScoreFilter(["1a5a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(5)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_PostCrawfordTargetBelowMatchLength_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a4a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(5)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_CrawfordTargetAtMatchLength_ReturnsFalse()
    {
        // Crawford (1, L, true) is reachable: the leader hits 1-away while
        // the trailer still needs the full match length. Only the
        // non-Crawford 1-away family is capped at L - 1.
        var filter = new MatchScoreFilter(["1a5aC"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(5)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_OnePointMatch_1a1a_ReturnsFalse()
    {
        // The one exception to the "max <= L - 1" rule for 1-away targets:
        // a 1-point match's only game is (1, 1, false) with no Crawford game
        // before it. 1a1a is never Crawford — a match session refuses a
        // Crawford game with both sides 1-away.
        var filter = new MatchScoreFilter(["1a1a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(1)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_TwoPointMatch_1a2a_ReturnsTrue()
    {
        // In a 2-point match the post-Crawford family is only (1,1):
        // (1, 2, false) would need a Crawford game at (1, k) with k > 2 > L.
        var filter = new MatchScoreFilter(["1a2a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(2)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_TwoPointMatch_1a1a_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a1a"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(2)).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_MoneyGame_FilterHasNoMoney_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);

        filter.ShouldSkipGame(FakeGameInfo.Money()).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_MoneyGame_FilterIncludesMoney_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["moneyJ"]);

        filter.ShouldSkipGame(FakeGameInfo.Money()).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_ScoreMatchesTarget_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(3, 5)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_ScoreMissesTarget_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["3a5a"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(2, 4)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_CrawfordMismatch_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(1, 5, isCrawford: false)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_CrawfordMatch_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(1, 5, isCrawford: true)).Should().BeFalse();
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

        filter.ShouldSkipGame(FakeGameInfo.Match(5, 4)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_SwappedOrientationCrawford_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(5, 1, isCrawford: true)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_SwappedOrientationCrawfordFlagMismatch_ReturnsTrue()
    {
        // Orientation is projected loosely (either order) but the Crawford
        // flag stays exact — it is game-level information the header knows.
        var filter = new MatchScoreFilter(["1a5aC"]);

        filter.ShouldSkipGame(FakeGameInfo.Match(5, 1, isCrawford: false)).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  IDecisionFilter: ShouldAdvanceMatch — mid-stream, exercised on both
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceMatch_MoneySession_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["moneyJ"]);
        AssertShouldAdvanceMatchBoth(filter, Money(isJacoby: true), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_FutureTupleReachable_ReturnsFalse()
    {
        // Current (5,5). Tuple (3,5) reachable if on-roll side wins the next game.
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(5, 5), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_NoTupleReachable_ReturnsTrue()
    {
        // Current (2,2). Tuple (5,5) unreachable — both axes exceed current.
        var filter = new MatchScoreFilter(["5a5a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(2, 2), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_SwappedPerspectiveReachable_ReturnsFalse()
    {
        // Current (5,3). Tuple (2,4) fits only with swap: 4 <= 5, 2 <= 3, sum 6 < 8.
        var filter = new MatchScoreFilter(["2a4a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(5, 3), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CurrentGameStillMatches_ReturnsFalse()
    {
        // The producer cuts the file immediately on a true vote — including
        // the rest of the CURRENT game, whose later decisions carry this
        // exact score. Advancing here would drop them.
        var filter = new MatchScoreFilter(["3a5a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(3, 5), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CurrentGameMirrorOrientation_ReturnsFalse()
    {
        // Current decision is (5,4), but the same game's later decisions
        // include the mirror (4,5) whenever the other player is on roll — the
        // 4a5a orientation bug in its mid-.xg form.
        var filter = new MatchScoreFilter(["4a5a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(5, 4), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CrawfordTupleReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a3aC"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(3, 5), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_CrawfordTupleOutOfRange_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a7aC"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(3, 5), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_CrawfordTupleMatchesCurrentGame_ReturnsFalse()
    {
        // The current game IS the Crawford game the tuple names; its
        // remaining decisions can still match.
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 5, isCrawford: true), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_MirrorCrawfordTuple_ReturnsFalse()
    {
        // Mirror orientation of the current Crawford game: the leader's
        // decisions score (1,5,C), the trailer's (5,1,C).
        var filter = new MatchScoreFilter(["5a1aC"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 5, isCrawford: true), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_InCrawford_PostCrawfordTupleReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a3a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 5, isCrawford: true), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_CrawfordRequired_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["1a5aC"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 5), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_SmallerReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a2a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 5), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PostCrawford_NonPostCrawfordTuple_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["2a3a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 5), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_MultipleTuples_AnyReachable_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["3a5a", "10a10a", "1a2a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(3, 5), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_MoneyFilterWithMatchSession_ReturnsTrue()
    {
        var filter = new MatchScoreFilter(["moneyJ", "moneyNJ"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(3, 5), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_PostCrawfordTupleAtBound_ReturnsTrue()
    {
        // The reachability over-admission fix: from (2,5) the post-Crawford
        // family is (1, m, false) with m < max(2,5) = 5 — reaching (1,5)
        // would need a Crawford game at (1, k > 5), impossible. The old
        // generic fits/strict-sum path admitted it (1 <= 2, 5 <= 5, 6 < 7).
        var filter = new MatchScoreFilter(["1a5a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(2, 5), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_PostCrawfordTupleBelowBound_ReturnsFalse()
    {
        // (1,4,false) from (2,5): Crawford at (1,5), trailer wins one point.
        var filter = new MatchScoreFilter(["1a4a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(2, 5), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_PreCrawford_OneAwayOneAwayTuple_ReturnsFalse()
    {
        // (1,1,false) is reachable from any pre-Crawford state: Crawford at
        // (1,2), then the trailer wins a point.
        var filter = new MatchScoreFilter(["1a1a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(2, 2), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_AtOneAwayOneAway_TupleIsCurrentGame_ReturnsFalse()
    {
        var filter = new MatchScoreFilter(["1a1a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 1), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_AtOneAwayOneAway_NothingReachable_ReturnsTrue()
    {
        // From (1,1) the match ends with the current game; no future game
        // exists and the current game's score is not the target.
        var filter = new MatchScoreFilter(["1a2a"]);
        AssertShouldAdvanceMatchBoth(filter, AtScore(1, 1), expected: true);
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
        // 0 would otherwise parse into a dead tuple that could never match a
        // decision — exactly the silent "filter does nothing" failure the
        // fail-loud philosophy exists to prevent. This routes through the ≥ 1
        // semantic message, distinct from the format rejection above (a
        // negative sign never reaches here — \d+ makes it a format error).
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
        // it admits exactly the (OnRoll 4, Opp 5) standing and rejects the
        // mirror.
        var filter = new MatchScoreFilter([variant]);
        AssertMatchesBoth(filter, AtScore(4, 5), expected: true);
        AssertMatchesBoth(filter, AtScore(5, 4), expected: false);
    }

    [Theory]
    [InlineData("1a5Ac")]    // mixed-case separator + lowercase Crawford
    [InlineData("1A5aC")]    // mixed-case separator + uppercase Crawford
    public void ParseScore_MixedCaseCrawfordVariants_MatchSameAsCanonical(string variant)
    {
        // Equivalent in effect to the canonical "1a5aC": admits the Crawford
        // (OnRoll 1, Opp 5) standing, rejects the same score with the
        // Crawford flag off.
        var filter = new MatchScoreFilter([variant]);
        AssertMatchesBoth(filter, AtScore(1, 5, isCrawford: true), expected: true);
        AssertMatchesBoth(filter, AtScore(1, 5, isCrawford: false), expected: false);
    }

    // -----------------------------------------------------------------------
    //  The money tokens: moneyJ / moneyNJ (halheinrich/backgammon#121)
    //
    //  The sessions a score filter must tell apart — money under the Jacoby
    //  rule, money without it, and a match (with its Crawford variant) —
    //  swept against every money-token selection. Every money session states
    //  its rule (MoneyTerms.IsJacoby is a required bool), so there is no
    //  unknown-rule session to place: the type makes it unrepresentable.
    // -----------------------------------------------------------------------

    public static TheoryData<string[], bool, bool> MoneyTokenMatrix() => new()
    {
        // moneyJ alone: the Jacoby session only.
        { ["moneyJ"],            true,  true  },
        { ["moneyJ"],            false, false },

        // moneyNJ alone: the no-Jacoby session only.
        { ["moneyNJ"],           true,  false },
        { ["moneyNJ"],           false, true  },

        // Both listed — "money under either rule", which is what the old bare
        // token used to mean.
        { ["moneyJ", "moneyNJ"], true,  true  },
        { ["moneyJ", "moneyNJ"], false, true  },
    };

    [Theory]
    [MemberData(nameof(MoneyTokenMatrix))]
    public void Matches_MoneySessions_AdmittedByTheirRulesTokenOnly(
        string[] tokens, bool isJacoby, bool expected)
    {
        var filter = new MatchScoreFilter(tokens);
        AssertMatchesBoth(filter, Money(isJacoby), expected);
    }

    [Theory]
    [MemberData(nameof(MoneyTokenMatrix))]
    public void ShouldSkipMatch_MoneySession_JudgedByItsRule_ExactlyAsMatches(
        string[] tokens, bool isJacoby, bool admitted)
    {
        // The match header states the terms — the Jacoby rule among them — so
        // the match gate is the exact projection of Matches onto them: it
        // skips a money session exactly when no decision of it could pass.
        var filter = new MatchScoreFilter(tokens);

        filter.ShouldSkipMatch(FakeMatchInfo.Money(isJacoby)).Should().Be(!admitted);
    }

    [Theory]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void Matches_MoneyToken_NeverAdmitsAMatchSession(string token)
    {
        // Match scores are untouched by the money tokens, and vice versa:
        // neither token admits a match standing, Crawford or not.
        var filter = new MatchScoreFilter([token]);
        AssertMatchesBoth(filter, AtScore(3, 5), expected: false);
        AssertMatchesBoth(filter, AtScore(1, 5, isCrawford: true), expected: false);
    }

    [Theory]
    [InlineData("moneyJ", true)]
    [InlineData("moneyNJ", false)]
    public void Matches_MoneyToken_IsIndifferentToCubeAndPlayDecisions(string token, bool rule)
    {
        // The score facet reads only the session; the decision-type axis is
        // DecisionTypeFilter's, composed by AND at the set. A money session's
        // verdict is therefore the same for a cube decision and a checker
        // play — the per-facet independence, restated for the money tokens so
        // a future money-only special case cannot quietly break it.
        var filter = new MatchScoreFilter([token]);

        AssertMatchesBoth(filter, Money(rule), expected: true);
        AssertMatchesBoth(filter, MoneyCube(rule), expected: true);
    }

    // -----------------------------------------------------------------------
    //  Header gates: a game header's standing carries no rule, so a money
    //  game is admissible iff EITHER money token is listed — exact for the
    //  information that header carries; the match gate and Matches judge
    //  the rule.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void ShouldSkipGame_MoneyGame_EitherMoneyToken_ReturnsFalse(string token)
    {
        var filter = new MatchScoreFilter([token]);

        filter.ShouldSkipGame(FakeGameInfo.Money()).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_MatchSession_SingleMoneyToken_ReturnsTrue()
    {
        // A money-token-only filter carries no tuples, so no match session can
        // satisfy it.
        var filter = new MatchScoreFilter(["moneyNJ"]);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeTrue();
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
        // and no money session of any rule can ride through on it.
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
    [InlineData("DMP")]
    [InlineData(" dmp ")]
    [InlineData("DMPC")]
    [InlineData("DMP1")]
    public void Constructor_ThrowsExactlyWhenGetFaultFaults(string token)
    {
        var act = () => new MatchScoreFilter([token]);

        if (MatchScoreToken.GetFault(token) == MatchScoreTokenFault.None)
            act.Should().NotThrow();
        else
            act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    //  The DMP alias (halheinrich/backgammon#259). Pinned as an equivalence
    //  with 1a1a rather than as its own expectations: whatever 1a1a admits or
    //  rejects, at every gate, DMP must too — so a future change to 1a1a's
    //  behaviour carries the alias with it instead of splitting the two.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Whether a match standing at these away scores exists: each at least 1
    /// and at most the length, and in the Crawford game exactly one side
    /// 1-away — the match session's own rules, which a sweep must respect to
    /// build a record at all.
    /// </summary>
    private static bool IsStanding(int length, int onRoll, int opponent, bool isCrawford) =>
        onRoll <= length && opponent <= length
        && (!isCrawford || (Math.Min(onRoll, opponent) == 1 && Math.Max(onRoll, opponent) >= 2));

    [Fact]
    public void DoubleMatchPoint_FiltersExactlyAs1a1a()
    {
        var alias = new MatchScoreFilter([MatchScoreToken.DoubleMatchPoint]);
        var score = new MatchScoreFilter(["1a1a"]);

        // Decisions: every small standing in both orientations, Crawford or
        // not, at match lengths the gates treat differently, plus money under
        // each rule.
        var decisions = new List<BgDecisionData>();
        foreach (int length in new[] { 1, 2, 5 })
            for (int onRoll = 1; onRoll <= length; onRoll++)
                for (int opp = 1; opp <= length; opp++)
                    foreach (bool crawford in new[] { false, true })
                        if (IsStanding(length, onRoll, opp, crawford))
                            decisions.Add(AtScore(onRoll, opp, crawford, length));
        decisions.Add(Money(isJacoby: true));
        decisions.Add(Money(isJacoby: false));

        foreach (var decision in decisions)
        {
            var row = DecisionRow.From(decision, PlayRanking.Equity);
            AssertMatchesBoth(alias, decision, expected: score.Matches(row));
            AssertShouldAdvanceMatchBoth(alias, decision, expected: score.ShouldAdvanceMatch(row));
        }

        // Header gates.
        var matches = new[] { FakeMatchInfo.Money(isJacoby: true), FakeMatchInfo.Money(isJacoby: false) }
            .Concat(new[] { 1, 2, 5 }.Select(FakeMatchInfo.Match));
        foreach (var match in matches)
            alias.ShouldSkipMatch(match).Should().Be(score.ShouldSkipMatch(match), "match header {0}", match);

        var games = new List<FakeGameInfo> { FakeGameInfo.Money() };
        for (int away1 = 1; away1 <= 3; away1++)
            for (int away2 = 1; away2 <= 3; away2++)
                foreach (bool crawford in new[] { false, true })
                    if (IsStanding(3, away1, away2, crawford))
                        games.Add(FakeGameInfo.Match(away1, away2, crawford));
        foreach (var game in games)
            alias.ShouldSkipGame(game).Should().Be(score.ShouldSkipGame(game), "game header {0}", game);

        // Not vacuous: the sweep contains the decision both admit.
        AssertMatchesBoth(alias, AtScore(1, 1, length: 5), expected: true);
    }
}
