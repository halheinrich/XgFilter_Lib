using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Patterns;

public class PointRangeTests
{
    // -----------------------------------------------------------------------
    //  Contains — inclusive, signed, unbounded sides
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(2, 5, 2, true)]   // lower boundary inclusive
    [InlineData(2, 5, 5, true)]   // upper boundary inclusive
    [InlineData(2, 5, 3, true)]   // interior
    [InlineData(2, 5, 1, false)]  // below
    [InlineData(2, 5, 6, false)]  // above
    public void Contains_BothBounds_IsInclusive(int min, int max, int value, bool expected)
    {
        new PointRange(6, min, max).Contains(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(-3, -2, true)]    // at the opponent-side bound
    [InlineData(-3, -5, false)]   // beyond it (more negative)
    [InlineData(-3, 0, true)]     // unbounded above admits zero/positive
    public void Contains_OpponentSide_RespectsSignedBound(int min, int value, bool expected)
    {
        // Min only; Max unbounded.
        new PointRange(0, min, null).Contains(value).Should().Be(expected);
    }

    [Fact]
    public void Contains_NullMin_LowerSideUnbounded()
    {
        var range = new PointRange(0, null, -1);
        range.Contains(-15).Should().BeTrue();
        range.Contains(-1).Should().BeTrue();
        range.Contains(0).Should().BeFalse();
    }

    [Fact]
    public void Contains_NullMax_UpperSideUnbounded()
    {
        var range = new PointRange(6, 2, null);
        range.Contains(15).Should().BeTrue();
        range.Contains(2).Should().BeTrue();
        range.Contains(1).Should().BeFalse();
    }

    [Fact]
    public void Contains_BothNull_MatchesEveryValue()
    {
        var range = new PointRange(13, null, null);
        range.Contains(int.MinValue).Should().BeTrue();
        range.Contains(0).Should().BeTrue();
        range.Contains(int.MaxValue).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Construction validation — never invalid once constructed
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(-1)]
    [InlineData(26)]
    public void Ctor_IndexOutOfRange_Throws(int index)
    {
        var act = () => new PointRange(index, null, null);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Ctor_IndexAtBoundary_Constructs(int index)
    {
        var act = () => new PointRange(index, null, null);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(16, null)]
    [InlineData(-16, null)]
    [InlineData(null, 16)]
    [InlineData(null, -16)]
    public void Ctor_BoundMagnitudeOverFifteen_Throws(int? min, int? max)
    {
        var act = () => new PointRange(6, min, max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(15)]
    [InlineData(-15)]
    public void Ctor_BoundMagnitudeAtFifteen_Constructs(int bound)
    {
        var act = () => new PointRange(6, bound, bound);
        act.Should().NotThrow();
    }

    [Fact]
    public void Ctor_MinGreaterThanMax_Throws()
    {
        var act = () => new PointRange(6, 3, 2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Ctor_MinEqualsMax_Constructs()
    {
        // A single-value range (e.g. "exactly empty", [7,0,0]) is valid.
        var act = () => new PointRange(7, 0, 0);
        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  Value-equality and bracket-token rendering
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        new PointRange(6, 2, null).Should().Be(new PointRange(6, 2, null));
        new PointRange(6, 2, null).Should().NotBe(new PointRange(6, 2, 5));
    }

    [Theory]
    [InlineData(6, 2, null, "[6,2,]")]
    [InlineData(6, null, 0, "[6,,0]")]
    [InlineData(0, null, -1, "[0,,-1]")]
    [InlineData(7, 0, 0, "[7,0,0]")]
    [InlineData(13, null, null, "[13,,]")]
    public void ToString_RendersBracketToken(int index, int? min, int? max, string expected)
    {
        new PointRange(index, min, max).ToString().Should().Be(expected);
    }
}
