using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

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

    /// <summary>The opening 3-1 by <paramref name="player"/>, who erred by exactly <paramref name="error"/>.</summary>
    private static CheckerPlayDecision Erred(string player, double error) =>
        TestRecords.CheckerPlay(
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
        var set = new DecisionFilterSet().Add(new PlayerFilter(["Alice"]));

        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Alice")), expected: true);
        AssertSetMatchesBoth(set, TestRecords.CheckerPlay(descriptive: By("Bob")), expected: false);
    }

    [Fact]
    public void Matches_MultipleFilters_AndSemantics_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new ErrorRangeFilter(min: 0.05, max: null));

        AssertSetMatchesBoth(set, Erred("Alice", 0.10), expected: true);
        AssertSetMatchesBoth(set, Erred("Alice", 0.02), expected: false);
        AssertSetMatchesBoth(set, Erred("Bob",   0.10), expected: false);
    }

    [Fact]
    public void Matches_DecisionTypeAndPlayer_ComposeCorrectly_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
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
            .Add(new PlayerFilter(["Alice"]))
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
        set.Add(new PlayerFilter(["Alice"])).Should().BeSameAs(set);
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
        var set = new DecisionFilterSet().Add(new PlayerFilter(["Alice"]));
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
        var set = new DecisionFilterSet().Add(new PlayerFilter(["Alice"]));

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
