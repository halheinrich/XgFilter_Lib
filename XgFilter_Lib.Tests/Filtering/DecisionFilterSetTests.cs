using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Helpers;
using static XgFilter_Lib.Tests.Helpers.CubeHalves;

namespace XgFilter_Lib.Tests.Filtering;

public class DecisionFilterSetTests
{
    // -----------------------------------------------------------------------
    //  Test doubles for ShouldSkipMatch / ShouldSkipGame / ShouldAdvance*
    //  aggregation. Per the audit the aggregation paths weren't covered;
    //  these doubles let us assert OR semantics over an injected vote.
    // -----------------------------------------------------------------------

    private sealed class AlwaysMatchFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
    }

    private sealed class AdvanceVoteFilter(bool advanceMatch = false, bool advanceGame = false) : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldAdvanceMatch(IDecisionFilterData data) => advanceMatch;
        public bool ShouldAdvanceGame(IDecisionFilterData data) => advanceGame;
    }

    private sealed class SkipVoteFilter(bool skipMatch = false, bool skipGame = false)
        : IDecisionFilter, IMatchFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldSkipMatch(IMatchInfo match) => skipMatch;
        public bool ShouldSkipGame(IGameInfo game) => skipGame;
    }

    // -----------------------------------------------------------------------
    //  Records
    // -----------------------------------------------------------------------

    private static DescriptiveData By(string player) => TestRecords.Descriptive(onRollName: player);

    /// <summary>
    /// The opening 3-1 by <paramref name="player"/>, who erred by exactly
    /// <paramref name="error"/>, at <paramref name="moveNumber"/> of game 1.
    /// </summary>
    private static CheckerPlayDecision Erred(string player, double error, int moveNumber = 1) =>
        TestRecords.CheckerPlay(
            id: new XgDecisionId("match.xg", Game: 1, MoveNumber: moveNumber, IsCube: false),
            decision: TestRecords.CheckerPlayData(
                plays:
                [
                    TestRecords.Candidate(play: [new(8, 5), new(6, 5)], equity: 0.0),
                    TestRecords.Candidate(play: [new(13, 10), new(6, 5)], equity: -error),
                ],
                userPlayIndex: 1),
            descriptive: By(player));

    /// <summary>The opening 3-1 by <paramref name="player"/>, its one candidate analysed in <paramref name="mode"/> at <paramref name="level"/>.</summary>
    private static CheckerPlayDecision Analysed(string player, AnalysisMode mode, AnalysisLevel level) =>
        TestRecords.CheckerPlay(
            decision: TestRecords.CheckerPlayData(
                plays: [TestRecords.Candidate(analysisMode: mode, analysisLevel: level)]),
            descriptive: By(player));

    // -----------------------------------------------------------------------
    //  Matches — empty set, single filter, AND aggregation; both substrates
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_EmptySet_PassesEveryRow_BothSubstrates()
    {
        var set = new DecisionFilterSet();

        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Alice")), expected: true);
        AssertSetMatchesBoth(set, TestRecords.Cube(descriptive: By("Bob")), expected: true);
    }

    [Fact]
    public void Matches_SingleFilter_PassesMatchingRows_BothSubstrates()
    {
        var set = new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice"]));

        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Alice")), expected: true);
        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Bob")), expected: false);
    }

    [Fact]
    public void Matches_MultipleFilters_AndSemantics_BothSubstrates()
    {
        // The name and error facets are one filter now (a checker play reads
        // as both ANDed); the AND is across filters: the pair's and the move
        // number's.
        var set = new DecisionFilterSet()
            .Add(new PlayerErrorFilter(["Alice"], min: 0.05))
            .Add(new MoveNumberFilter(max: 1));

        AssertSetMatchesBoth(set, Erred("Alice", 0.10), expected: true);
        AssertSetMatchesBoth(set, Erred("Alice", 0.02), expected: false);
        AssertSetMatchesBoth(set, Erred("Bob",   0.10), expected: false);
        AssertSetMatchesBoth(set, Erred("Alice", 0.10, moveNumber: 2), expected: false);
    }

    [Fact]
    public void Matches_DecisionTypeAndPlayer_ComposeCorrectly_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerErrorFilter(["Alice"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly));

        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Alice")), expected: true);
        AssertSetMatchesBoth(set, TestRecords.Cube(descriptive: By("Alice")),        expected: false);
        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Bob")),   expected: false);
    }

    [Fact]
    public void Matches_AnalysisDepthAndPlayer_ComposeWithAnd_BothSubstrates()
    {
        // Depth is an independent facet from player identity; a row must clear
        // BOTH the depth clause-union AND the player filter. A 3-ply row by
        // Alice passes; the same depth by Bob, and a rollout by Alice, are each
        // rejected by the AND.
        var set = new DecisionFilterSet()
            .Add(new PlayerErrorFilter(["Alice"]))
            .Add(new AnalysisDepthFilter(
                [new AnalysisDepthFilter.Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply3])]));

        AssertSetMatchesBoth(set,
            Analysed("Alice", AnalysisMode.Evaluation, AnalysisLevel.Ply3), expected: true);
        AssertSetMatchesBoth(set,
            Analysed("Bob", AnalysisMode.Evaluation, AnalysisLevel.Ply3), expected: false);
        AssertSetMatchesBoth(set,
            Analysed("Alice", AnalysisMode.Rollout, AnalysisLevel.Ply3), expected: false);
    }

    [Fact]
    public void Add_IsFluentAndReturnsSameInstance()
    {
        var set = new DecisionFilterSet();
        set.Add(new PlayerErrorFilter(["Alice"])).Should().BeSameAs(set);
    }

    // -----------------------------------------------------------------------
    //  The filter for the name and error facets: at most one per set
    // -----------------------------------------------------------------------

    [Fact]
    public void Add_ASecondFilterForThePair_IsRefused_AndLeavesTheSetAsItWas()
    {
        var set = new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice"]));

        var act = () => set.Add(new PlayerErrorFilter([], min: 0.1));

        act.Should().Throw<ArgumentException>().Which.ParamName.Should().Be("filter");
        AssertSetMatchesBoth(set, Erred("Alice", 0.0), expected: true);
        AssertSetErrorReadBoth(set, Erred("Alice", 0.05), expected: 0.05);
    }

    [Fact]
    public void Add_OtherFilters_BesideThePairs_AreAccepted()
    {
        var act = () => new DecisionFilterSet()
            .Add(new MoveNumberFilter(max: 3))
            .Add(new PlayerErrorFilter(["Alice"]))
            .Add(new MoveNumberFilter(min: 1));

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  ErrorRead — the error the set read on a decision; both substrates
    // -----------------------------------------------------------------------

    [Fact]
    public void ErrorRead_EmptySet_IsTheDoublersError()
    {
        var set = new DecisionFilterSet();

        AssertSetErrorReadBoth(set, DoubledAndPassed(doublerError: 0.05, takerError: 0.2), expected: 0.05);
        AssertSetErrorReadBoth(set, NotDoubled(), expected: 0.122);
        AssertSetErrorReadBoth(set, Erred("Alice", 0.1), expected: 0.1);
        AssertSetErrorReadBoth(set, TestRecords.CheckerPlay(
            decision: TestRecords.CheckerPlayData(userPlayIndex: null)), expected: null);
    }

    [Fact]
    public void ErrorRead_SetWithoutThePairsFilter_IsTheDoublersError()
    {
        // Another facet does not change the half read, on a record the set
        // passes or one it rejects.
        var set = new DecisionFilterSet().Add(new DecisionTypeFilter(DecisionTypeOption.CubeOnly));

        AssertSetErrorReadBoth(set, DoubledAndPassed(doublerError: 0.05, takerError: 0.2), expected: 0.05);
        AssertSetErrorReadBoth(set, Erred("Alice", 0.1), expected: 0.1);
    }

    [Fact]
    public void ErrorRead_IsThePairsQualifyingError()
    {
        var record = DoubledAndPassed(doublerError: 0.05, takerError: 0.2);

        AssertSetErrorReadBoth(new DecisionFilterSet().Add(new PlayerErrorFilter(["Bob"])), record, expected: 0.2);
        AssertSetErrorReadBoth(
            new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice", "Bob"])), record, expected: 0.2);
        AssertSetErrorReadBoth(
            new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice", "Bob"], min: 0.0, max: 0.1)),
            record, expected: 0.05);
    }

    [Fact]
    public void ErrorRead_OnARecordAnotherFacetRejects_IsStillTheQualifyingError()
    {
        // Bob's pass qualifies, at 0.2; the decision-type facet rejects the
        // cube. ErrorRead is defined for every record, and is not gated on
        // Matches: the pair's reading stands.
        var set = new DecisionFilterSet()
            .Add(new PlayerErrorFilter(["Bob"], min: 0.1))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly));
        var record = DoubledAndPassed(doublerError: 0.0, takerError: 0.2);

        AssertSetMatchesBoth(set, record, expected: false);
        AssertSetErrorReadBoth(set, record, expected: 0.2);
    }

    [Fact]
    public void ErrorRead_WhereNoSelectedHalfQualifies_IsNull()
    {
        // Never an error from an unselected half (the unlisted doubler's 0.6,
        // in range) or an out-of-range one (the listed taker's 0.2).
        var record = DoubledAndPassed(doublerError: 0.6, takerError: 0.2);

        foreach (var set in new[]
        {
            new DecisionFilterSet().Add(new PlayerErrorFilter(["Bob"], min: 0.5)),
            new DecisionFilterSet().Add(new PlayerErrorFilter(["Carol"])),
        })
        {
            AssertSetMatchesBoth(set, record, expected: false);
            AssertSetErrorReadBoth(set, record, expected: null);
        }
    }

    [Fact]
    public void ErrorRead_NullData_Throws()
    {
        var act = () => new DecisionFilterSet().ErrorRead(null!);

        act.Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("data");
    }

    // -----------------------------------------------------------------------
    //  A record admitted through the taker: every other facet still reads it
    //  from the player on roll's side (SPEC-filtering §8)
    // -----------------------------------------------------------------------

    [Fact]
    public void ARecordAdmittedThroughTheTaker_ThePositionPatternAndMatchScoreFacetsEvaluateIdentically()
    {
        // Alice, on roll 3-away to Bob's 5-away, holds two checkers on her
        // 6-point; Bob holds three on Alice's 22-point, his 3-point. Admitted
        // through Bob's pass, each other facet passes or fails the decision
        // exactly as it does alone and as it does admitted through Alice: in
        // the doubler's frame, "3a5a" and Alice's 6-point.
        var position = TestRecords.Position(
            mop: BoardBuilder.Build((6, 2), (5, 1), (22, -3)),
            session: TestRecords.MatchSession(onRollNeeds: 3, opponentNeeds: 5));
        var record = DoubledAndPassed(doublerError: 0.0, takerError: 0.2, position: position);

        IDecisionFilter[] facets =
        [
            new MatchScoreFilter(["3a5a"]),
            new MatchScoreFilter(["5a3a"]),
            new PositionPatternFilter(BoardPattern.Parse("[6,2,2]")),
            new PositionPatternFilter(BoardPattern.Parse("[3,3,3]")),
        ];
        bool[] alone = [true, false, true, false];

        for (int i = 0; i < facets.Length; i++)
        {
            AssertMatchesBoth(facets[i], record, alone[i]);
            AssertSetMatchesBoth(
                new DecisionFilterSet().Add(new PlayerErrorFilter(["Bob"], min: 0.1)).Add(facets[i]),
                record, alone[i]);
            AssertSetMatchesBoth(
                new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice"])).Add(facets[i]),
                record, alone[i]);
        }
    }

    // -----------------------------------------------------------------------
    //  IsEmpty — the SSOT for "no filters active". True on a fresh set and on
    //  a set Build() produces from a default config; false once any filter is
    //  present. Cases 3-4 confirm IsEmpty faithfully reflects Build()'s output
    //  (the activation rule itself is FilterConfigTests' concern).
    // -----------------------------------------------------------------------

    [Fact]
    public void IsEmpty_FreshSet_ReturnsTrue()
    {
        new DecisionFilterSet().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_AfterAdd_ReturnsFalse()
    {
        var set = new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice"]));
        set.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void IsEmpty_DefaultConfigBuild_ReturnsTrue()
    {
        new FilterConfig().Build().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void IsEmpty_AnyFieldActiveBuild_ReturnsFalse()
    {
        var config = new FilterConfig { Players = { "Alice" } };
        config.Build().IsEmpty.Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  ShouldSkipMatch / ShouldSkipGame aggregation — header-input,
    //  no substrate axis. OR semantics: any IMatchFilter in the set voting
    //  to skip carries.
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_EmptySet_ReturnsFalse()
    {
        new DecisionFilterSet().ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_NoIMatchFilters_ReturnsFalse()
    {
        // A filter that implements IDecisionFilter but not IMatchFilter
        // must not influence the skip decision.
        var set = new DecisionFilterSet().Add(new AlwaysMatchFilter());
        set.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_AnyVoteSkips_ReturnsTrue()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipMatch: false))
            .Add(new SkipVoteFilter(skipMatch: true));

        set.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_NoneVoteSkip_ReturnsFalse()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipMatch: false))
            .Add(new SkipVoteFilter(skipMatch: false));

        set.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_AnyVoteSkips_ReturnsTrue()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipGame: false))
            .Add(new SkipVoteFilter(skipGame: true));

        set.ShouldSkipGame(FakeGameInfo.Match(3, 5)).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_NoneVoteSkip_ReturnsFalse()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipGame: false))
            .Add(new SkipVoteFilter(skipGame: false));

        set.ShouldSkipGame(FakeGameInfo.Match(3, 5)).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  ShouldAdvanceMatch / ShouldAdvanceGame aggregation — row-input,
    //  exercised on both substrates. OR semantics across filters.
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceMatch_EmptySet_ReturnsFalse_BothSubstrates()
    {
        AssertSetShouldAdvanceMatchBoth(new DecisionFilterSet(), TestRecords.CheckerPlay(), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_AnyVoteAdvances_ReturnsTrue_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceMatch: false))
            .Add(new AdvanceVoteFilter(advanceMatch: true));

        AssertSetShouldAdvanceMatchBoth(set, TestRecords.CheckerPlay(), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_NoneVoteAdvance_ReturnsFalse_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceMatch: false))
            .Add(new AdvanceVoteFilter(advanceMatch: false));

        AssertSetShouldAdvanceMatchBoth(set, TestRecords.CheckerPlay(), expected: false);
    }

    [Fact]
    public void ShouldAdvanceGame_AnyVoteAdvances_ReturnsTrue_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceGame: false))
            .Add(new AdvanceVoteFilter(advanceGame: true));

        AssertSetShouldAdvanceGameBoth(set, TestRecords.CheckerPlay(), expected: true);
    }

    [Fact]
    public void ShouldAdvanceGame_NoneVoteAdvance_ReturnsFalse_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceGame: false))
            .Add(new AdvanceVoteFilter(advanceGame: false));

        AssertSetShouldAdvanceGameBoth(set, TestRecords.CheckerPlay(), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Decoupling proof — the encapsulation this contract-layer arc exists to
    //  buy. The header-skip surface consumes only BgDataTypes_Lib abstractions,
    //  so a real filter set decides skips from a plain IMatchInfo / IGameInfo
    //  with no parser (ConvertXgToJson_Lib) type anywhere in reach. Inputs are
    //  statically typed as the interfaces to make that explicit; money versus
    //  match is read from the header's own terms and standing.
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipMatch_DecidesFromPlainIMatchInfo_NoParserType()
    {
        var set = new DecisionFilterSet().Add(new PlayerErrorFilter(["Alice"]));

        IMatchInfo present = FakeMatchInfo.Match(7) with { Player1 = "Alice", Player2 = "Bob" };
        IMatchInfo absent = FakeMatchInfo.Match(7) with { Player1 = "Bob", Player2 = "Charlie" };

        set.ShouldSkipMatch(present).Should().BeFalse();
        set.ShouldSkipMatch(absent).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_DecidesFromPlainIGameInfo_NoParserType()
    {
        var set = new DecisionFilterSet().Add(new MoveNumberFilter(min: 1, max: 5));

        IGameInfo standard = FakeGameInfo.Match(3, 5) with { IsStandardStart = true };
        IGameInfo custom = FakeGameInfo.Match(3, 5) with { IsStandardStart = false };

        set.ShouldSkipGame(standard).Should().BeFalse();
        set.ShouldSkipGame(custom).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_MoneyVerdict_ReadsTheTermsOfAPlainIMatchInfo()
    {
        // A moneyJ-only filter skips a match session and a money session
        // without the Jacoby rule, and admits one with it: the verdict turns
        // on IMatchInfo.Terms' kind and, for money, its rule.
        var set = new DecisionFilterSet().Add(new MatchScoreFilter(["moneyJ"]));

        IMatchInfo jacoby = FakeMatchInfo.Money(isJacoby: true);
        IMatchInfo noJacoby = FakeMatchInfo.Money(isJacoby: false);
        IMatchInfo match = FakeMatchInfo.Match(7);

        set.ShouldSkipMatch(jacoby).Should().BeFalse();
        set.ShouldSkipMatch(noJacoby).Should().BeTrue();
        set.ShouldSkipMatch(match).Should().BeTrue();
    }
}
