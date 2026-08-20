using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

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
    [InlineData(double.MaxValue)]
    [InlineData(double.PositiveInfinity)]
    public void IsBoundNonNegative_AdmissibleBound_ReturnsTrue(double? bound) =>
        ErrorRangeFilter.IsBoundNonNegative(bound).Should().BeTrue();

    [Theory]
    [InlineData(-0.0000001)]
    [InlineData(-1.0)]
    [InlineData(double.MinValue)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]  // unordered, so it admits nothing — the very failure the rule catches
    public void IsBoundNonNegative_InadmissibleBound_ReturnsFalse(double? bound) =>
        ErrorRangeFilter.IsBoundNonNegative(bound).Should().BeFalse();

    [Theory]
    [InlineData(null, null)]      // both absent
    [InlineData(0.05, null)]      // one-sided
    [InlineData(null, 0.05)]      // one-sided
    [InlineData(0.05, 0.20)]
    [InlineData(0.05, 0.05)]      // equal: the inclusive bounds make this an exact-value filter
    public void AreBoundsOrdered_OrderedPair_ReturnsTrue(double? min, double? max) =>
        ErrorRangeFilter.AreBoundsOrdered(min, max).Should().BeTrue();

    [Theory]
    [InlineData(0.20, 0.05)]
    [InlineData(0.0500001, 0.05)]
    public void AreBoundsOrdered_MinExceedsMax_ReturnsFalse(double? min, double? max) =>
        ErrorRangeFilter.AreBoundsOrdered(min, max).Should().BeFalse();

    [Fact]
    public void Constructor_NegativeMin_Throws()
    {
        var act = () => new ErrorRangeFilter(min: -0.05, max: 0.20);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void Constructor_NegativeMax_Throws()
    {
        // A negative upper bound is the sharper case: filter error is a
        // magnitude, so this admits nothing at all rather than merely being
        // redundant.
        var act = () => new ErrorRangeFilter(min: null, max: -0.05);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("max");
    }

    [Fact]
    public void Constructor_NaNBound_Throws()
    {
        var minIsNaN = () => new ErrorRangeFilter(min: double.NaN);
        var maxIsNaN = () => new ErrorRangeFilter(max: double.NaN);

        minIsNaN.Should().Throw<ArgumentOutOfRangeException>()
                .Which.ParamName.Should().Be("min");
        maxIsNaN.Should().Throw<ArgumentOutOfRangeException>()
                .Which.ParamName.Should().Be("max");
    }

    [Fact]
    public void Constructor_MinExceedsMax_Throws()
    {
        // Both bounds are individually admissible; the pair is not.
        var act = () => new ErrorRangeFilter(min: 0.20, max: 0.05);

        act.Should().Throw<ArgumentException>().WithMessage("*empty error range*");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, null)]
    [InlineData(null, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.05, 0.05)]
    [InlineData(0.0, double.PositiveInfinity)]
    public void Constructor_AdmissibleBounds_DoesNotThrow(double? min, double? max)
    {
        var act = () => new ErrorRangeFilter(min, max);

        act.Should().NotThrow();
    }

    [Fact]
    public void Matches_ZeroOnlyRange_AdmitsExactlyZeroError()
    {
        // The reason zero is admissible rather than treated as "no bound":
        // [0, 0] is the errorless-decision filter, and it is not empty.
        var filter = new ErrorRangeFilter(min: 0.0, max: 0.0);

        AssertMatchesBoth(filter, new RowShape(Error: 0.0), expected: true);
        AssertMatchesBoth(filter, new RowShape(Error: 0.01), expected: false);
    }

    [Fact]
    public void Matches_EqualBounds_AdmitsExactlyThatError()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.05);

        AssertMatchesBoth(filter, new RowShape(Error: 0.05), expected: true);
        AssertMatchesBoth(filter, new RowShape(Error: 0.04), expected: false);
        AssertMatchesBoth(filter, new RowShape(Error: 0.06), expected: false);
    }
}
