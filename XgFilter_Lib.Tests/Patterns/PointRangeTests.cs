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

    [Fact]
    public void Ctor_IndexForm_EqualsSlotForm()
    {
        // The index ctor is sugar for the board-slot form of the slot ctor.
        new PointRange(6, 2, null).Should().Be(
            new PointRange(PatternSlot.Board(6), 2, null));
    }

    // -----------------------------------------------------------------------
    //  Borne-off slots — per-slot signed bound intervals, fail-loud
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0, 15)]     // full interval
    [InlineData(0, 0)]      // nobody off, exactly
    [InlineData(15, 15)]    // everybody off, exactly
    [InlineData(2, null)]   // at least two off
    [InlineData(null, null)]
    public void Ctor_PlayerOff_BoundsWithinZeroToFifteen_Construct(int? min, int? max)
    {
        var act = () => new PointRange(PatternSlot.PlayerOff, min, max);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-1, null)]   // wrong sign: player's off count is never negative
    [InlineData(null, -2)]
    [InlineData(16, null)]   // beyond the checker ceiling
    [InlineData(null, 16)]
    public void Ctor_PlayerOff_BoundOutsideInterval_Throws(int? min, int? max)
    {
        var act = () => new PointRange(PatternSlot.PlayerOff, min, max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(-15, 0)]     // full interval
    [InlineData(0, 0)]       // nobody off, exactly
    [InlineData(-15, -15)]   // everybody off, exactly
    [InlineData(null, -2)]   // at least two off (signed convention)
    [InlineData(null, null)]
    public void Ctor_OpponentOff_BoundsWithinMinusFifteenToZero_Construct(int? min, int? max)
    {
        var act = () => new PointRange(PatternSlot.OpponentOff, min, max);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1, null)]    // wrong sign: opponent's off count is never positive
    [InlineData(null, 2)]
    [InlineData(-16, null)]  // beyond the checker ceiling
    [InlineData(null, -16)]
    public void Ctor_OpponentOff_BoundOutsideInterval_Throws(int? min, int? max)
    {
        var act = () => new PointRange(PatternSlot.OpponentOff, min, max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Ctor_OffSlot_MinGreaterThanMax_Throws()
    {
        var playerAct = () => new PointRange(PatternSlot.PlayerOff, 3, 1);
        playerAct.Should().Throw<ArgumentException>();

        var opponentAct = () => new PointRange(PatternSlot.OpponentOff, -1, -3);
        opponentAct.Should().Throw<ArgumentException>();
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

    [Fact]
    public void ToString_OffSlots_RenderNamedTokens()
    {
        new PointRange(PatternSlot.PlayerOff, 1, null).ToString().Should().Be("[off,1,]");
        new PointRange(PatternSlot.PlayerOff, 0, 0).ToString().Should().Be("[off,0,0]");
        new PointRange(PatternSlot.OpponentOff, null, -2).ToString().Should().Be("[opp-off,,-2]");
        new PointRange(PatternSlot.OpponentOff, -15, -15).ToString().Should().Be("[opp-off,-15,-15]");
    }
}
