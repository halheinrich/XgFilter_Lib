using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Patterns;

public class CheckerSpanTests
{
    // -----------------------------------------------------------------------
    //  Construction — two or more board indices, a < b, bars included
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, 25)]    // the whole board, both bars included
    [InlineData(0, 1)]     // the opponent's bar and the 1-point
    [InlineData(24, 25)]   // the 24-point and the on-roll player's bar
    [InlineData(7, 12)]
    public void Ctor_AscendingIndicesInRange_Constructs(int first, int last)
    {
        var span = new CheckerSpan(first, last);

        span.First.Should().Be(first);
        span.Last.Should().Be(last);
    }

    [Theory]
    [InlineData(12, 7)]   // a > b
    [InlineData(7, 7)]    // a == b: a single index is a location, not a span
    [InlineData(0, 0)]
    [InlineData(25, 25)]
    public void Ctor_StartNotBelowEnd_Throws(int first, int last)
    {
        var act = () => new CheckerSpan(first, last);

        act.Should().Throw<ArgumentException>()
            .Which.Should().NotBeOfType<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(5, 26)]
    [InlineData(-1, 26)]
    public void Ctor_IndexOutOfRange_Throws(int first, int last)
    {
        var act = () => new CheckerSpan(first, last);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_IsTheValidSpanZeroToOne()
    {
        // The stored-extent encoding exists so a default instance is still a
        // valid span, never the invalid 0-0.
        default(CheckerSpan).Should().Be(new CheckerSpan(0, 1));
        default(CheckerSpan).First.Should().Be(0);
        default(CheckerSpan).Last.Should().Be(1);
    }

    // -----------------------------------------------------------------------
    //  Equality and rendering
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        new CheckerSpan(7, 12).Should().Be(new CheckerSpan(7, 12));
        new CheckerSpan(7, 12).GetHashCode().Should().Be(new CheckerSpan(7, 12).GetHashCode());
        new CheckerSpan(7, 12).Should().NotBe(new CheckerSpan(7, 11));
        new CheckerSpan(7, 12).Should().NotBe(new CheckerSpan(6, 12));
    }

    [Theory]
    [InlineData(7, 12, "7-12")]
    [InlineData(0, 25, "0-25")]
    [InlineData(24, 25, "24-25")]
    public void ToString_RendersTokenHead(int first, int last, string expected)
    {
        new CheckerSpan(first, last).ToString().Should().Be(expected);
    }
}
