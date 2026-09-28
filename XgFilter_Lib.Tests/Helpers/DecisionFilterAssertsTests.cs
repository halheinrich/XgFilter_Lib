using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Helpers;

public class DecisionFilterAssertsTests
{
    // -----------------------------------------------------------------------
    //  Test doubles
    // -----------------------------------------------------------------------

    private sealed class AlwaysTrueFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
    }

    private sealed class AlwaysFalseFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => false;
    }

    /// <summary>Matches only <see cref="DecisionRow"/> — simulates a substrate-specific bug.</summary>
    private sealed class OnlyDecisionRowFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => data is DecisionRow;
    }

    /// <summary>Matches only a view built for depth first — simulates a filter that reads the wrong ranking.</summary>
    private sealed class OnlyDepthFirstFilter : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => data.Ranking == PlayRanking.DepthFirst;
    }

    private sealed class ShouldAdvanceMatchFilter(bool result) : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldAdvanceMatch(IDecisionFilterData data) => result;
    }

    private sealed class ShouldAdvanceGameFilter(bool result) : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldAdvanceGame(IDecisionFilterData data) => result;
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — agreement paths
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_BothAgreeTrue_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), TestRecords.CheckerPlay(), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertMatchesBoth_BothAgreeFalse_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysFalseFilter(), TestRecords.CheckerPlay(), expected: false);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — expected mismatch fails
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_BothAgreeButExpectationWrong_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), TestRecords.CheckerPlay(), expected: false);
        act.Should().Throw<Exception>();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — substrate disagreement fails
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_SubstratesDisagree_ExpectedTrue_Throws()
    {
        // The row returns true, the view false.
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new OnlyDecisionRowFilter(), TestRecords.CheckerPlay(), expected: true);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void AssertMatchesBoth_SubstratesDisagree_ExpectedFalse_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new OnlyDecisionRowFilter(), TestRecords.CheckerPlay(), expected: false);
        act.Should().Throw<Exception>();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — both substrates are built for the stated ranking
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(PlayRanking.Equity, false)]
    [InlineData(PlayRanking.DepthFirst, true)]
    public void AssertMatchesBoth_BuildsBothSubstratesForTheStatedRanking(PlayRanking ranking, bool expected)
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new OnlyDepthFirstFilter(), TestRecords.CheckerPlay(), expected, ranking);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertMatchesBoth_DefaultsToTheEquityRanking()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new OnlyDepthFirstFilter(), TestRecords.CheckerPlay(), expected: false);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — cube and off-list paths resolve cleanly
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_CubeDecision_WorksWithoutAfterBoards()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), TestRecords.Cube(), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertMatchesBoth_OffListPlay_WithNoPlayerAfterBoard_DoesNotThrow()
    {
        var offList = TestRecords.CheckerPlay(
            decision: TestRecords.CheckerPlayData(userPlayIndex: null, unlistedPlayError: 0.1));
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), offList, expected: true);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  AssertShouldAdvanceMatchBoth
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertShouldAdvanceMatchBoth_BothAgreeTrue_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceMatchBoth(
            new ShouldAdvanceMatchFilter(true), TestRecords.CheckerPlay(), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertShouldAdvanceMatchBoth_BothAgreeFalse_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceMatchBoth(
            new ShouldAdvanceMatchFilter(false), TestRecords.CheckerPlay(), expected: false);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertShouldAdvanceMatchBoth_ExpectationWrong_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceMatchBoth(
            new ShouldAdvanceMatchFilter(true), TestRecords.CheckerPlay(), expected: false);
        act.Should().Throw<Exception>();
    }

    // -----------------------------------------------------------------------
    //  AssertShouldAdvanceGameBoth
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertShouldAdvanceGameBoth_BothAgreeTrue_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceGameBoth(
            new ShouldAdvanceGameFilter(true), TestRecords.CheckerPlay(), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertShouldAdvanceGameBoth_BothAgreeFalse_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceGameBoth(
            new ShouldAdvanceGameFilter(false), TestRecords.CheckerPlay(), expected: false);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertShouldAdvanceGameBoth_ExpectationWrong_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceGameBoth(
            new ShouldAdvanceGameFilter(true), TestRecords.CheckerPlay(), expected: false);
        act.Should().Throw<Exception>();
    }
}
