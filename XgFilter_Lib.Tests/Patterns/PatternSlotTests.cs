using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Patterns;

public class PatternSlotTests
{
    // -----------------------------------------------------------------------
    //  Factories — the only construction paths, each validated
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Board_IndexAtBoundary_Constructs(int index)
    {
        var slot = PatternSlot.Board(index);
        slot.Kind.Should().Be(PatternSlotKind.Board);
        slot.BoardIndex.Should().Be(index);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(26)]
    public void Board_IndexOutOfRange_Throws(int index)
    {
        var act = () => PatternSlot.Board(index);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OffSlots_CarryTheirKind_AndNoBoardIndex()
    {
        PatternSlot.PlayerOff.Kind.Should().Be(PatternSlotKind.PlayerOff);
        PatternSlot.PlayerOff.BoardIndex.Should().BeNull();

        PatternSlot.OpponentOff.Kind.Should().Be(PatternSlotKind.OpponentOff);
        PatternSlot.OpponentOff.BoardIndex.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    //  Value-equality — what BoardPattern's duplicate-slot key relies on
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        PatternSlot.Board(3).Should().Be(PatternSlot.Board(3));
        PatternSlot.Board(3).Should().NotBe(PatternSlot.Board(4));
        PatternSlot.PlayerOff.Should().Be(PatternSlot.PlayerOff);
        PatternSlot.OpponentOff.Should().Be(PatternSlot.OpponentOff);
        PatternSlot.PlayerOff.Should().NotBe(PatternSlot.OpponentOff);
        PatternSlot.Board(0).Should().NotBe(PatternSlot.PlayerOff);
    }

    [Fact]
    public void Default_IsBoardIndexZero()
    {
        // Mirrors default(PointRange) before the slot model: a valid slot at
        // the opponent's bar, never an invalid state.
        default(PatternSlot).Should().Be(PatternSlot.Board(0));
    }

    // -----------------------------------------------------------------------
    //  ToString — the canonical bracket-grammar token head
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_RendersCanonicalTokenHead()
    {
        PatternSlot.Board(6).ToString().Should().Be("6");
        PatternSlot.Board(0).ToString().Should().Be("0");
        PatternSlot.Board(25).ToString().Should().Be("25");
        PatternSlot.PlayerOff.ToString().Should().Be("off");
        PatternSlot.OpponentOff.ToString().Should().Be("opp-off");
    }
}
