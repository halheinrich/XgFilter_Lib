using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;
using static XgFilter_Lib.Tests.Helpers.DecisionFilterAsserts;

namespace XgFilter_Lib.Tests.Filtering;

public class ErrorRangeFilterTests
{
    // -----------------------------------------------------------------------
    //  Bounded range — exercised against both substrates via RowShape
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenErrorWithinRange_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, new RowShape(Error: 0.10), expected: true);
    }

    [Fact]
    public void Matches_WhenErrorBelowMin_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, new RowShape(Error: 0.03), expected: false);
    }

    [Fact]
    public void Matches_WhenErrorAboveMax_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, new RowShape(Error: 0.25), expected: false);
    }

    [Fact]
    public void Matches_WhenErrorAtMinBoundary_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, new RowShape(Error: 0.05), expected: true);
    }

    [Fact]
    public void Matches_WhenErrorAtMaxBoundary_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, new RowShape(Error: 0.20), expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsZeroError()
    {
        var filter = new ErrorRangeFilter(min: null, max: 0.20);
        AssertMatchesBoth(filter, new RowShape(Error: 0.0), expected: true);
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsLargeError()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: null);
        AssertMatchesBoth(filter, new RowShape(Error: 1.0), expected: true);
    }

    [Fact]
    public void Matches_WhenNoBoundsSet_AlwaysReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: null, max: null);
        AssertMatchesBoth(filter, new RowShape(Error: 0.50), expected: true);
    }

    // -----------------------------------------------------------------------
    //  Null FilterError — reachable only via BgDecisionData; DecisionRow's
    //  FilterError is non-nullable by construction. Filter behaviour is
    //  "reject", asserted against the substrate that can produce null.
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenFilterErrorIsNull_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.01);
        var data = new RowShape(Error: null).ToBgDecisionData();

        filter.Matches(data).Should().BeFalse();
    }
}
