using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;
using static XgFilter_Lib.Tests.Helpers.CubeHalves;

namespace XgFilter_Lib.Tests.Filtering;

/// <summary>
/// The name facet and the error facet as one filter for the pair
/// (SPEC-filtering §8, halheinrich/backgammon#440): which halves of a decision
/// the listed names select, which of those an error range qualifies, the
/// error read off the qualifying halves, and the match skip. The facts the
/// two filters it replaced pinned — the name facet's comparison, the error
/// facet's ranges and bound rule, the ranking each range is judged under —
/// are pinned here on the doubler's half, where they still hold. The cube
/// records are <see cref="CubeHalves"/>': Alice doubles, Bob takes.
/// </summary>
public class PlayerErrorFilterTests
{
    // -----------------------------------------------------------------------
    //  Records
    // -----------------------------------------------------------------------

    /// <summary>A checker play made by <paramref name="player"/>, the player on roll.</summary>
    private static CheckerPlayDecision PlayedBy(string? player) =>
        TestRecords.CheckerPlay(descriptive: TestRecords.Descriptive(onRollName: player));

    /// <summary>
    /// The opening 3-1 with the player's play scored at exactly
    /// <paramref name="error"/> under either ranking: two candidates at the
    /// same depth, the best at equity 0 and the played one at
    /// <c>-error</c>, so the derived error, <c>0 - (-error)</c>, is exact.
    /// </summary>
    private static CheckerPlayDecision ErredBy(double error, string? player = "Alice") =>
        TestRecords.CheckerPlay(
            decision: TestRecords.CheckerPlayData(
                plays:
                [
                    TestRecords.Candidate(play: [new(8, 5), new(6, 5)], equity: 0.0),
                    TestRecords.Candidate(play: [new(13, 10), new(6, 5)], equity: -error),
                ],
                userPlayIndex: 1),
            descriptive: TestRecords.Descriptive(onRollName: player));

    /// <summary>The opening 3-1 with the player's play off the candidate list, the analyser's error stated.</summary>
    private static CheckerPlayDecision OffList(double analysersError) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(
            userPlayIndex: null, unlistedPlayError: analysersError));

    /// <summary>The opening 3-1 with no move recorded: no candidate is the player's and no error is stated.</summary>
    private static CheckerPlayDecision NothingRecorded() =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(userPlayIndex: null));

    private static PlayerErrorFilter Names(params string[] players) => new(players);

    // -----------------------------------------------------------------------
    //  The name facet on a checker play: the player on roll's one decision
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenPlayerInList_ReturnsTrue()
    {
        AssertMatchesBoth(Names("Alice", "Bob"), PlayedBy("Alice"), expected: true);
    }

    [Fact]
    public void Matches_WhenPlayerNotInList_ReturnsFalse()
    {
        AssertMatchesBoth(Names("Alice", "Bob"), PlayedBy("Charlie"), expected: false);
    }

    [Fact]
    public void Matches_IsCaseInsensitive()
    {
        AssertMatchesBoth(Names("alice"), PlayedBy("ALICE"), expected: true);
        AssertMatchesBoth(Names("bob"), TestRecords.Cube(
            descriptive: TestRecords.Descriptive(onRollName: "Alice", opponentName: "BOB")), expected: true);
    }

    [Fact]
    public void Matches_WhenListHasSingleEntry_MatchesOnlyThatPlayer()
    {
        var filter = Names("Alice");
        AssertMatchesBoth(filter, PlayedBy("Alice"), expected: true);
        AssertMatchesBoth(filter, PlayedBy("Bob"), expected: false);
    }

    [Fact]
    public void Matches_NoRecordedName_NeverPasses()
    {
        // A source that recorded no name states none (null, the one spelling
        // of "none recorded"): there is no name to match, so listed names
        // select no half of the decision — a checker play's, or either of a
        // doubled cube's — rather than guessing it into the list.
        var filter = Names("Alice", "Bob");
        var unnamed = TestRecords.Descriptive(onRollName: null, opponentName: null);

        AssertMatchesBoth(filter, PlayedBy(null), expected: false);
        AssertMatchesBoth(filter, TestRecords.Cube(descriptive: unnamed), expected: false);
    }

    [Fact]
    public void Matches_NoNamesNoBounds_PassesEveryDecision()
    {
        // Was the name filter's "an empty list rejects every row". An empty list
        // is now the name facet inactive: the doubler's half is selected, and
        // with no bounds it qualifies whatever its result — no error
        // included. Build never made either filter from an empty input, so no
        // configuration changes; this is the rule with no facet active, which
        // DecisionFilterSet.ErrorRead falls back to.
        foreach (var filter in new[] { Names(), PlayerErrorFilter.Default })
        {
            AssertMatchesBoth(filter, PlayedBy("Alice"), expected: true);
            AssertMatchesBoth(filter, PlayedBy(null), expected: true);
            AssertMatchesBoth(filter, NothingRecorded(), expected: true);
            AssertMatchesBoth(filter, NotDoubled(), expected: true);
            AssertMatchesBoth(filter, DoubledAndUnanswered(), expected: true);
        }
    }

    // -----------------------------------------------------------------------
    //  The name facet on a cube decision: the doubler's half and, where a
    //  double was offered, the taker's
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_TheTakerAlone_PassesWhereADoubleWasOffered()
    {
        // Bob made the take or pass: his name admits the decision, however
        // his response is recorded — stated, or the analyser's error.
        var bob = Names("Bob");

        AssertMatchesBoth(bob, TestRecords.Cube(), expected: true);   // double/take
        AssertMatchesBoth(bob, DoubledAndPassed(doublerError: 0.0, takerError: 0.2), expected: true);
        AssertMatchesBoth(bob, DoubledWithTheAnalysersTakeError(0.2), expected: true);
    }

    [Fact]
    public void Matches_TheOpponentOfANoDouble_Fails()
    {
        // No double was offered, so the non-roller made no decision: listing
        // only Bob admits nothing, while Alice's no-double is hers.
        AssertMatchesBoth(Names("Bob"), NotDoubled(), expected: false);
        AssertMatchesBoth(Names("Alice"), NotDoubled(), expected: true);
    }

    [Fact]
    public void Matches_ADoubleNeverAnswered_PassesTheNameFacetForTheTaker()
    {
        // A double answered by resigning is a decision the taker was offered
        // (Hal, 2026-10-26; SPEC-filtering §8): his half is stated, not
        // recorded, and his name admits it.
        var record = DoubledAndUnanswered();
        record.ViewFor(PlayRanking.Equity).TakerResult.Should().Be(PlayerResult.NotRecorded);

        AssertMatchesBoth(Names("Bob"), record, expected: true);
    }

    [Fact]
    public void Matches_ADoubleNeverAnswered_FailsAnyErrorRangeForTheTakerAlone()
    {
        // No error is invented for the unanswered half, so no range — not the
        // widest, not the exact-zero one — admits it through the taker. The
        // record still qualifies through the doubler's half where that is
        // listed: Alice's double is recorded, with an error of 0.
        var record = DoubledAndUnanswered();

        foreach (var (min, max) in new (double?, double?)[] { (0.0, null), (null, double.MaxValue), (0.0, 0.0) })
        {
            AssertMatchesBoth(new PlayerErrorFilter(["Bob"], min, max), record, expected: false);
            AssertMatchesBoth(new PlayerErrorFilter(["Alice", "Bob"], min, max), record, expected: true);
        }
    }

    // -----------------------------------------------------------------------
    //  The error facet with no names: the doubler's half alone
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenErrorWithinRange_ReturnsTrue()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.05, max: 0.20), ErredBy(0.10), expected: true);
    }

    [Fact]
    public void Matches_WhenErrorBelowMin_ReturnsFalse()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.05, max: 0.20), ErredBy(0.03), expected: false);
    }

    [Fact]
    public void Matches_WhenErrorAboveMax_ReturnsFalse()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.05, max: 0.20), ErredBy(0.25), expected: false);
    }

    [Fact]
    public void Matches_WhenErrorAtMinBoundary_ReturnsTrue()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.05, max: 0.20), ErredBy(0.05), expected: true);
    }

    [Fact]
    public void Matches_WhenErrorAtMaxBoundary_ReturnsTrue()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.05, max: 0.20), ErredBy(0.20), expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsZeroError()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: null, max: 0.20), ErredBy(0.0), expected: true);
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsLargeError()
    {
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.05, max: null), ErredBy(1.0), expected: true);
    }

    [Fact]
    public void Matches_UnstatedMove_IsJudgedByTheAnalysersError()
    {
        // A play off the candidate list has no candidate to score, but the
        // record states the analyser's error for it: that error is the one
        // judged, under either ranking.
        var filter = new PlayerErrorFilter([], min: 0.05, max: 0.20);

        foreach (var ranking in Enum.GetValues<PlayRanking>())
        {
            AssertMatchesBoth(filter, OffList(0.10), expected: true, ranking);
            AssertMatchesBoth(filter, OffList(0.30), expected: false, ranking);
        }
    }

    [Fact]
    public void Matches_NothingRecorded_NeverPassesARange()
    {
        // No move recorded, no error: dropped even by the widest range, under
        // either ranking — never read as an error of 0.
        var filter = new PlayerErrorFilter([], min: 0.0, max: null);

        foreach (var ranking in Enum.GetValues<PlayRanking>())
            AssertMatchesBoth(filter, NothingRecorded(), expected: false, ranking);
    }

    [Fact]
    public void Matches_CubeDecision_IsJudgedByTheStatedActionsError()
    {
        // No double on a double/take: the doubler's error, 0.122, is the
        // player's result.
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.1, max: 0.2), NotDoubled(), expected: true);
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.2), NotDoubled(), expected: false);
    }

    [Fact]
    public void Matches_NoNamesWithBounds_JudgesTheDoublerOnly()
    {
        // With no name facet the half read is the doubler's, as before
        // (SPEC-filtering §8): the taker's 0.2 is not judged.
        var record = DoubledAndPassed(doublerError: 0.0, takerError: 0.2);

        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.1), record, expected: false);
        AssertMatchesBoth(new PlayerErrorFilter([], min: 0.0, max: 0.0), record, expected: true);
    }

    // -----------------------------------------------------------------------
    //  The ranking in force (SPEC-scoring §2a): the error is the player's
    //  under the ranking the view or row was built for.
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_UnderDepthFirst_AMoveTheRankingDoesNotScore_NeverPassesARange()
    {
        // The shallow play whose equity beats depth first's best is not
        // scored there: it has no error, so no range admits it — not even
        // the widest. Under equity the same move is scored (error 0) and
        // passes; only the ranking differs. With no bounds at all the
        // decision is admitted under either ranking: no range is judged.
        var played = RankingSplit.Played(RankingSplit.ShallowHigh);

        foreach (var filter in new[]
        {
            new PlayerErrorFilter([], min: 0.0),
            new PlayerErrorFilter([], max: 1.0),
        })
        {
            AssertMatchesBoth(filter, played, expected: false, PlayRanking.DepthFirst);
            AssertMatchesBoth(filter, played, expected: true, PlayRanking.Equity);
        }

        AssertMatchesBoth(PlayerErrorFilter.Default, played, expected: true, PlayRanking.DepthFirst);
    }

    [Fact]
    public void Matches_UnderDepthFirst_AScoredMovesErrorIsTheOneMeasured()
    {
        // 24/23 13/10 is scored under both rankings, against different best
        // plays: 0.25 under depth first, 0.50 under equity. Each range admits
        // the move under exactly the ranking whose error it bounds.
        var played = RankingSplit.Played(RankingSplit.DeepWorst);
        var depthFirstsError = new PlayerErrorFilter([], min: 0.25, max: 0.25);
        var equitysError = new PlayerErrorFilter([], min: 0.50, max: 0.50);

        AssertMatchesBoth(depthFirstsError, played, expected: true, PlayRanking.DepthFirst);
        AssertMatchesBoth(depthFirstsError, played, expected: false, PlayRanking.Equity);
        AssertMatchesBoth(equitysError, played, expected: true, PlayRanking.Equity);
        AssertMatchesBoth(equitysError, played, expected: false, PlayRanking.DepthFirst);
    }

    [Fact]
    public void Matches_ErredByMoreThanX_UnderEachRanking()
    {
        // "Erred by more than 0.3": depth first's best, 8/5 6/5, erred by
        // 0.25 under equity; 24/23 13/10 by 0.50 under equity and 0.25 under
        // depth first. So under equity one of the three passes, under depth
        // first none does — the not-scored shallow play included.
        var filter = new PlayerErrorFilter([], min: 0.3);

        AssertMatchesBoth(filter, RankingSplit.Played(RankingSplit.DeepWorst), expected: true, PlayRanking.Equity);
        AssertMatchesBoth(filter, RankingSplit.Played(RankingSplit.DeepBest), expected: false, PlayRanking.Equity);
        AssertMatchesBoth(filter, RankingSplit.Played(RankingSplit.ShallowHigh), expected: false, PlayRanking.Equity);

        foreach (int played in new[] { RankingSplit.DeepBest, RankingSplit.ShallowHigh, RankingSplit.DeepWorst })
            AssertMatchesBoth(filter, RankingSplit.Played(played), expected: false, PlayRanking.DepthFirst);
    }

    [Fact]
    public void Matches_ZeroOnlyRange_AdmitsExactlyZeroError()
    {
        // The reason zero is admissible rather than treated as "no bound":
        // [0, 0] is the errorless-decision filter, and it is not empty.
        var filter = new PlayerErrorFilter([], min: 0.0, max: 0.0);

        AssertMatchesBoth(filter, ErredBy(0.0), expected: true);
        AssertMatchesBoth(filter, ErredBy(0.01), expected: false);
    }

    [Fact]
    public void Matches_EqualBounds_AdmitsExactlyThatError()
    {
        var filter = new PlayerErrorFilter([], min: 0.05, max: 0.05);

        AssertMatchesBoth(filter, ErredBy(0.05), expected: true);
        AssertMatchesBoth(filter, ErredBy(0.04), expected: false);
        AssertMatchesBoth(filter, ErredBy(0.06), expected: false);
    }

    // -----------------------------------------------------------------------
    //  The pair: some listed half within the range
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_ChatGptsCounterexample_TheListedTakersErrorQualifies()
    {
        // A take error of 0.200 and the opponent's doubling error of 0, under
        // a 0.100 threshold (#440, 6401821443): listing the taker alone
        // passes, and so does listing both players — adding a name never
        // removes a decision. Listing the doubler alone does not: his half
        // has no error in range. Stated (a pass) and as the analyser's error.
        foreach (var record in new BgDecisionData[]
        {
            DoubledAndPassed(doublerError: 0.0, takerError: 0.2),
            DoubledWithTheAnalysersTakeError(0.2),
        })
        {
            AssertMatchesBoth(new PlayerErrorFilter(["Bob"], min: 0.1), record, expected: true);
            AssertMatchesBoth(new PlayerErrorFilter(["Alice", "Bob"], min: 0.1), record, expected: true);
            AssertMatchesBoth(new PlayerErrorFilter(["Alice"], min: 0.1), record, expected: false);
        }
    }

    [Fact]
    public void Matches_TheListedTakersErrorIsJudged_NotTheUnlistedDoublers()
    {
        // Alice's double cost 0.3, within the range; Bob's pass 0.05, outside
        // it. Only Bob is listed, so only his half is judged.
        var record = DoubledAndPassed(doublerError: 0.3, takerError: 0.05);

        AssertMatchesBoth(new PlayerErrorFilter(["Bob"], min: 0.1), record, expected: false);
        AssertMatchesBoth(new PlayerErrorFilter(["Alice"], min: 0.1), record, expected: true);
    }

    [Fact]
    public void QualifyingError_BothListedHalvesQualify_IsTheLarger()
    {
        // Whichever half is larger, both qualifying: the by-error order reads
        // the larger of the two (SPEC-filtering §8).
        var takerLarger = DoubledAndPassed(doublerError: 0.15, takerError: 0.2);
        var doublerLarger = DoubledAndPassed(doublerError: 0.3, takerError: 0.2);

        foreach (var filter in new[]
        {
            new PlayerErrorFilter(["Alice", "Bob"], min: 0.1),
            new PlayerErrorFilter(["Alice", "Bob"]),
        })
        {
            AssertMatchesBoth(filter, takerLarger, expected: true);
            AssertQualifyingErrorBoth(filter, takerLarger, expected: 0.2);
            AssertMatchesBoth(filter, doublerLarger, expected: true);
            AssertQualifyingErrorBoth(filter, doublerLarger, expected: 0.3);
        }
    }

    [Fact]
    public void QualifyingError_OneHalfQualifies_IsItsError_EvenWhereTheOtherListedHalfIsLarger()
    {
        // Bound [0, 0.1], the listed taker at 0.2 and the listed doubler at
        // 0.05: the decision passes on the doubler's half, and the reading is
        // 0.05 — the half the facets passed, not merely a listed one.
        var filter = new PlayerErrorFilter(["Alice", "Bob"], min: 0.0, max: 0.1);
        var record = DoubledAndPassed(doublerError: 0.05, takerError: 0.2);

        AssertMatchesBoth(filter, record, expected: true);
        AssertQualifyingErrorBoth(filter, record, expected: 0.05);
    }

    [Fact]
    public void QualifyingError_TheListedTakerAlone_IsTheTakersError()
    {
        var record = DoubledAndPassed(doublerError: 0.05, takerError: 0.2);

        AssertQualifyingErrorBoth(Names("Bob"), record, expected: 0.2);
        AssertQualifyingErrorBoth(new PlayerErrorFilter(["Bob"], min: 0.1), record, expected: 0.2);
    }

    [Fact]
    public void QualifyingError_NoHalfQualifies_IsNull()
    {
        // Never an error from an unselected half or an out-of-range one: the
        // unlisted doubler's 0.6 is in range but not selected, and the listed
        // taker's 0.2 is selected but out of range.
        var record = DoubledAndPassed(doublerError: 0.6, takerError: 0.2);

        foreach (var filter in new[]
        {
            new PlayerErrorFilter(["Bob"], min: 0.5),
            new PlayerErrorFilter(["Alice", "Bob"], min: 0.7),
            Names("Carol"),
        })
        {
            AssertMatchesBoth(filter, record, expected: false);
            AssertQualifyingErrorBoth(filter, record, expected: null);
        }
    }

    [Fact]
    public void QualifyingError_AQualifyingHalfWithNoError_ReadsNone()
    {
        // With no bounds the unanswered half qualifies under the taker's
        // name, with no error to read; listed beside the doubler, the
        // doubler's recorded 0 is the reading.
        var record = DoubledAndUnanswered();

        AssertMatchesBoth(Names("Bob"), record, expected: true);
        AssertQualifyingErrorBoth(Names("Bob"), record, expected: null);
        AssertQualifyingErrorBoth(Names("Alice", "Bob"), record, expected: 0.0);
    }

    [Fact]
    public void QualifyingError_TheDefaultRule_IsTheDoublersError()
    {
        // No facet active: the doubler's error, which for a checker play is
        // the player's, or null where it has none.
        var filter = PlayerErrorFilter.Default;

        AssertQualifyingErrorBoth(filter, DoubledAndPassed(doublerError: 0.05, takerError: 0.2), expected: 0.05);
        AssertQualifyingErrorBoth(filter, NotDoubled(), expected: 0.122);
        AssertQualifyingErrorBoth(filter, ErredBy(0.1), expected: 0.1);
        AssertQualifyingErrorBoth(filter, NothingRecorded(), expected: null);
        AssertQualifyingErrorBoth(filter, RankingSplit.Played(RankingSplit.ShallowHigh), expected: null, PlayRanking.DepthFirst);
    }

    // -----------------------------------------------------------------------
    //  A checker play is unchanged under every case: the old composition —
    //  the player listed AND the error in range — is the oracle.
    // -----------------------------------------------------------------------

    public static TheoryData<string[], double?, double?> Cases()
    {
        var data = new TheoryData<string[], double?, double?>();
        string[][] names = [[], ["Alice"], ["Bob"], ["alice", "Bob"]];
        (double?, double?)[] bounds = [(null, null), (0.05, 0.20), (0.3, null), (null, 0.0), (0.0, 0.0)];
        foreach (var list in names)
            foreach (var (min, max) in bounds)
                data.Add(list, min, max);
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ACheckerPlay_IsJudgedAsTheTwoFiltersJudgedIt(string[] players, double? min, double? max)
    {
        var filter = new PlayerErrorFilter(players, min, max);
        CheckerPlayDecision[] plays =
        [
            ErredBy(0.1, "Alice"), ErredBy(0.0, "Bob"), ErredBy(0.5, "ALICE"), ErredBy(0.1, null),
            OffList(0.1), NothingRecorded(), RankingSplit.Played(RankingSplit.ShallowHigh),
        ];

        foreach (var ranking in Enum.GetValues<PlayRanking>())
        {
            foreach (var play in plays)
            {
                var view = play.ViewFor(ranking);
                bool named = players.Length == 0
                    || (view.Player is { } p && players.Contains(p, StringComparer.OrdinalIgnoreCase));
                bool hasError = view.PlayerResult.TryGetError(out double error);
                bool inRange = (min is null && max is null)
                    || (hasError && (min is null || error >= min) && (max is null || error <= max));
                bool expected = named && inRange;

                AssertMatchesBoth(filter, play, expected, ranking);
                AssertQualifyingErrorBoth(filter, play, expected && hasError ? error : null, ranking);
            }
        }
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipMatch — IMatchInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_WhenNeitherPlayerInList_ReturnsTrue()
    {
        var match = FakeMatchInfo.Match(7) with { Player1 = "Bob", Player2 = "Charlie" };

        Names("Alice").ShouldSkipMatch(match).Should().BeTrue();
        new PlayerErrorFilter(["Alice"], min: 0.1).ShouldSkipMatch(match).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_WhenPlayer1InList_ReturnsFalse()
    {
        var match = FakeMatchInfo.Match(7) with { Player1 = "Alice", Player2 = "Bob" };

        Names("Alice").ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_WhenPlayer2InList_ReturnsFalse()
    {
        // Player 2 makes decisions on roll and, where player 1 doubles, as the
        // taker: either way one of the header's two.
        var match = FakeMatchInfo.Match(7) with { Player1 = "Bob", Player2 = "Alice" };

        Names("Alice").ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_IsCaseInsensitive()
    {
        var match = FakeMatchInfo.Money(isJacoby: true) with { Player1 = "ALICE", Player2 = "Bob" };

        Names("alice").ShouldSkipMatch(match).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_WithNoNamesListed_NeverSkips()
    {
        // Bounds alone select the doubler's half of every decision in every
        // match, so no header rules a match out: "neither header name listed"
        // over an empty list would be every match.
        var match = FakeMatchInfo.Match(7) with { Player1 = "Bob", Player2 = "Charlie" };

        new PlayerErrorFilter([], min: 0.1).ShouldSkipMatch(match).Should().BeFalse();
        new PlayerErrorFilter([], max: 0.0).ShouldSkipMatch(match).Should().BeFalse();
        PlayerErrorFilter.Default.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_AlwaysReturnsFalse()
    {
        foreach (var filter in new[] { Names("Alice"), new PlayerErrorFilter([], min: 0.1), PlayerErrorFilter.Default })
        {
            filter.ShouldSkipGame(FakeGameInfo.Match(3, 5)).Should().BeFalse();
            filter.ShouldSkipGame(FakeGameInfo.Money()).Should().BeFalse();
        }
    }

    // -----------------------------------------------------------------------
    //  The bound rule, stated once here and asked twice — the constructor
    //  enforces it and FilterConfig.GetInvalidFields reports it. These tests
    //  pin the predicates directly; FilterConfigTests pins the reporting and
    //  the Build path that reaches this constructor.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]   // absent: the rule constrains values, never presence
    [InlineData(0.0)]    // an exact-zero error filter is meaningful
    [InlineData(0.05)]
    [InlineData(3.5)]               // no cap (halheinrich/backgammon#374): above 3 is loose, not wrong
    [InlineData(1e300)]             // no cap: a huge finite bound is still a number
    [InlineData(double.MaxValue)]   // the largest finite bound
    public void IsBoundFiniteNonNegative_AdmissibleBound_ReturnsTrue(double? bound) =>
        PlayerErrorFilter.IsBoundFiniteNonNegative(bound).Should().BeTrue();

    [Theory]
    [InlineData(-0.0000001)]
    [InlineData(-1.0)]
    [InlineData(double.MinValue)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]  // unordered, so it admits nothing — the very failure the rule catches
    [InlineData(double.PositiveInfinity)]  // not finite (halheinrich/backgammon#374): "1e999" parses to it
    public void IsBoundFiniteNonNegative_InadmissibleBound_ReturnsFalse(double? bound) =>
        PlayerErrorFilter.IsBoundFiniteNonNegative(bound).Should().BeFalse();

    [Theory]
    [InlineData(null, null)]      // both absent
    [InlineData(0.05, null)]      // one-sided
    [InlineData(null, 0.05)]      // one-sided
    [InlineData(0.05, 0.20)]
    [InlineData(0.05, 0.05)]      // equal: the inclusive bounds make this an exact-value filter
    public void AreBoundsOrdered_OrderedPair_ReturnsTrue(double? min, double? max) =>
        PlayerErrorFilter.AreBoundsOrdered(min, max).Should().BeTrue();

    [Theory]
    [InlineData(0.20, 0.05)]
    [InlineData(0.0500001, 0.05)]
    public void AreBoundsOrdered_MinExceedsMax_ReturnsFalse(double? min, double? max) =>
        PlayerErrorFilter.AreBoundsOrdered(min, max).Should().BeFalse();

    [Fact]
    public void Constructor_NegativeMin_Throws()
    {
        var act = () => new PlayerErrorFilter([], min: -0.05, max: 0.20);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void Constructor_NegativeMax_Throws()
    {
        // A negative upper bound is the sharper case: a scored error is a
        // magnitude, so this admits nothing at all rather than merely being
        // redundant.
        var act = () => new PlayerErrorFilter([], min: null, max: -0.05);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("max");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_NonFiniteBound_Throws(double bound)
    {
        var minIsNonFinite = () => new PlayerErrorFilter([], min: bound);
        var maxIsNonFinite = () => new PlayerErrorFilter([], max: bound);

        minIsNonFinite.Should().Throw<ArgumentOutOfRangeException>()
                      .Which.ParamName.Should().Be("min");
        maxIsNonFinite.Should().Throw<ArgumentOutOfRangeException>()
                      .Which.ParamName.Should().Be("max");
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 0.20, "min")]  // a bound fault, not a misordered pair
    [InlineData(0.0, double.PositiveInfinity, "max")]   // not a spelling of the open end: null is that
    [InlineData(0.05, double.NaN, "max")]
    public void Constructor_NonFiniteBoundBesideFiniteOne_BlamesTheNonFiniteBound(
        double? min, double? max, string expectedParam)
    {
        var act = () => new PlayerErrorFilter([], min, max);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithMessage("Bound must be a finite number of zero or greater*")
           .Which.ParamName.Should().Be(expectedParam);
    }

    [Fact]
    public void Constructor_MinExceedsMax_Throws()
    {
        // Both bounds are individually admissible; the pair is not.
        var act = () => new PlayerErrorFilter([], min: 0.20, max: 0.05);

        act.Should().Throw<ArgumentException>().WithMessage("*empty error range*");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, null)]
    [InlineData(null, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.05, 0.05)]
    [InlineData(0.0, double.MaxValue)]          // the widest closed range
    [InlineData(double.MaxValue, double.MaxValue)]
    [InlineData(3.5, null)]                     // no cap (halheinrich/backgammon#374)
    [InlineData(0.05, 1e300)]                   // no cap
    public void Constructor_AdmissibleBounds_DoesNotThrow(double? min, double? max)
    {
        var act = () => new PlayerErrorFilter(["Alice"], min, max);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_NullPlayers_Throws()
    {
        var act = () => new PlayerErrorFilter(null!);

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("players");
    }
}
