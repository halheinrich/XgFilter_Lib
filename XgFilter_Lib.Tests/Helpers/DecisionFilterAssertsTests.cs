using BgDataTypes_Lib;
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

    private sealed class ShouldAdvanceMatchFilter(bool result) : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => true;
        public bool ShouldAdvanceMatch(IDecisionFilterData data) => result;
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — agreement paths
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_BothAgreeTrue_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), new RowShape(), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertMatchesBoth_BothAgreeFalse_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysFalseFilter(), new RowShape(), expected: false);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — expected mismatch fails
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_BothAgreeButExpectationWrong_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), new RowShape(), expected: false);
        act.Should().Throw<Exception>();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — substrate disagreement fails
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_SubstratesDisagree_ExpectedTrue_Throws()
    {
        // DecisionRow returns true, BgDecisionData returns false.
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new OnlyDecisionRowFilter(), new RowShape(), expected: true);
        act.Should().Throw<Exception>();
    }

    [Fact]
    public void AssertMatchesBoth_SubstratesDisagree_ExpectedFalse_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new OnlyDecisionRowFilter(), new RowShape(), expected: false);
        act.Should().Throw<Exception>();
    }

    // -----------------------------------------------------------------------
    //  AssertMatchesBoth — cube and empty-afterboard paths resolve cleanly
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertMatchesBoth_CubeShape_WorksWithoutAfterBoards()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), new RowShape(IsCube: true), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertMatchesBoth_CheckerShape_WithEmptyAfterBoards_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertMatchesBoth(
            new AlwaysTrueFilter(), new RowShape(IsCube: false), expected: true);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  AssertShouldAdvanceMatchBoth
    // -----------------------------------------------------------------------

    [Fact]
    public void AssertShouldAdvanceMatchBoth_BothAgreeTrue_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceMatchBoth(
            new ShouldAdvanceMatchFilter(true), new RowShape(), expected: true);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertShouldAdvanceMatchBoth_BothAgreeFalse_DoesNotThrow()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceMatchBoth(
            new ShouldAdvanceMatchFilter(false), new RowShape(), expected: false);
        act.Should().NotThrow();
    }

    [Fact]
    public void AssertShouldAdvanceMatchBoth_ExpectationWrong_Throws()
    {
        var act = () => DecisionFilterAsserts.AssertShouldAdvanceMatchBoth(
            new ShouldAdvanceMatchFilter(true), new RowShape(), expected: false);
        act.Should().Throw<Exception>();
    }
}
