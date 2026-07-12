using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
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
        public bool ShouldSkipMatch(XgMatchInfo match) => skipMatch;
        public bool ShouldSkipGame(XgGameInfo game) => skipGame;
    }

    // -----------------------------------------------------------------------
    //  Matches — empty set, single filter, AND aggregation; both substrates
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_EmptySet_PassesEveryRow_BothSubstrates()
    {
        var set = new DecisionFilterSet();

        AssertSetMatchesBoth(set, new RowShape(Player: "Alice"), expected: true);
        AssertSetMatchesBoth(set, new RowShape(Player: "Bob"), expected: true);
    }

    [Fact]
    public void Matches_SingleFilter_PassesMatchingRows_BothSubstrates()
    {
        var set = new DecisionFilterSet().Add(new PlayerFilter(["Alice"]));

        AssertSetMatchesBoth(set, new RowShape(Player: "Alice"), expected: true);
        AssertSetMatchesBoth(set, new RowShape(Player: "Bob"), expected: false);
    }

    [Fact]
    public void Matches_MultipleFilters_AndSemantics_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new ErrorRangeFilter(min: 0.05, max: null));

        AssertSetMatchesBoth(set, new RowShape(Player: "Alice", Error: 0.10), expected: true);
        AssertSetMatchesBoth(set, new RowShape(Player: "Alice", Error: 0.02), expected: false);
        AssertSetMatchesBoth(set, new RowShape(Player: "Bob",   Error: 0.10), expected: false);
    }

    [Fact]
    public void Matches_DecisionTypeAndPlayer_ComposeCorrectly_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly));

        AssertSetMatchesBoth(set, new RowShape(Player: "Alice", IsCube: false), expected: true);
        AssertSetMatchesBoth(set, new RowShape(Player: "Alice", IsCube: true),  expected: false);
        AssertSetMatchesBoth(set, new RowShape(Player: "Bob",   IsCube: false), expected: false);
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
        new DecisionFilterSet().ShouldSkipMatch(new XgMatchInfo()).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_NoIMatchFilters_ReturnsFalse()
    {
        // A filter that implements IDecisionFilter but not IMatchFilter
        // must not influence the skip decision.
        var set = new DecisionFilterSet().Add(new AlwaysMatchFilter());
        set.ShouldSkipMatch(new XgMatchInfo()).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipMatch_AnyVoteSkips_ReturnsTrue()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipMatch: false))
            .Add(new SkipVoteFilter(skipMatch: true));

        set.ShouldSkipMatch(new XgMatchInfo()).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_NoneVoteSkip_ReturnsFalse()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipMatch: false))
            .Add(new SkipVoteFilter(skipMatch: false));

        set.ShouldSkipMatch(new XgMatchInfo()).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_AnyVoteSkips_ReturnsTrue()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipGame: false))
            .Add(new SkipVoteFilter(skipGame: true));

        set.ShouldSkipGame(new XgGameInfo()).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipGame_NoneVoteSkip_ReturnsFalse()
    {
        var set = new DecisionFilterSet()
            .Add(new SkipVoteFilter(skipGame: false))
            .Add(new SkipVoteFilter(skipGame: false));

        set.ShouldSkipGame(new XgGameInfo()).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  ShouldAdvanceMatch / ShouldAdvanceGame aggregation — row-input,
    //  exercised on both substrates. OR semantics across filters.
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceMatch_EmptySet_ReturnsFalse_BothSubstrates()
    {
        AssertSetShouldAdvanceMatchBoth(new DecisionFilterSet(), new RowShape(), expected: false);
    }

    [Fact]
    public void ShouldAdvanceMatch_AnyVoteAdvances_ReturnsTrue_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceMatch: false))
            .Add(new AdvanceVoteFilter(advanceMatch: true));

        AssertSetShouldAdvanceMatchBoth(set, new RowShape(), expected: true);
    }

    [Fact]
    public void ShouldAdvanceMatch_NoneVoteAdvance_ReturnsFalse_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceMatch: false))
            .Add(new AdvanceVoteFilter(advanceMatch: false));

        AssertSetShouldAdvanceMatchBoth(set, new RowShape(), expected: false);
    }

    [Fact]
    public void ShouldAdvanceGame_AnyVoteAdvances_ReturnsTrue_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceGame: false))
            .Add(new AdvanceVoteFilter(advanceGame: true));

        AssertSetShouldAdvanceGameBoth(set, new RowShape(), expected: true);
    }

    [Fact]
    public void ShouldAdvanceGame_NoneVoteAdvance_ReturnsFalse_BothSubstrates()
    {
        var set = new DecisionFilterSet()
            .Add(new AdvanceVoteFilter(advanceGame: false))
            .Add(new AdvanceVoteFilter(advanceGame: false));

        AssertSetShouldAdvanceGameBoth(set, new RowShape(), expected: false);
    }
}
