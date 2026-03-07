using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class ErrorRangeFilterTests
{
    [Fact]
    public void Matches_WhenErrorWithinRange_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        var row = DecisionRowBuilder.Build(error: 0.10);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenErrorBelowMin_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        var row = DecisionRowBuilder.Build(error: 0.03);

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenErrorAboveMax_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        var row = DecisionRowBuilder.Build(error: 0.25);

        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Matches_WhenErrorAtMinBoundary_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        var row = DecisionRowBuilder.Build(error: 0.05);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenErrorAtMaxBoundary_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        var row = DecisionRowBuilder.Build(error: 0.20);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsZeroError()
    {
        var filter = new ErrorRangeFilter(min: null, max: 0.20);
        var row = DecisionRowBuilder.Build(error: 0.0);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsLargeError()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: null);
        var row = DecisionRowBuilder.Build(error: 1.0);

        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Matches_WhenNoBoundsSet_AlwaysReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: null, max: null);
        var row = DecisionRowBuilder.Build(error: 0.50);

        filter.Matches(row).Should().BeTrue();
    }
}
