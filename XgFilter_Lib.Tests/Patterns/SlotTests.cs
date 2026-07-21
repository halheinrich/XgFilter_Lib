using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Patterns;

public class SlotTests
{
    // -----------------------------------------------------------------------
    //  Factories — the only construction paths, each validated
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Board_IndexAtBoundary_Constructs(int index)
    {
        var slot = Slot.Board(index);
        slot.Kind.Should().Be(SlotKind.Board);
        slot.BoardIndex.Should().Be(index);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(26)]
    public void Board_IndexOutOfRange_Throws(int index)
    {
        var act = () => Slot.Board(index);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OffSlots_CarryTheirKind_AndNoBoardIndex()
    {
        Slot.PlayerOff.Kind.Should().Be(SlotKind.PlayerOff);
        Slot.PlayerOff.BoardIndex.Should().BeNull();

        Slot.OpponentOff.Kind.Should().Be(SlotKind.OpponentOff);
        Slot.OpponentOff.BoardIndex.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    //  Value-equality — what BoardPattern's duplicate-slot key relies on
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        Slot.Board(3).Should().Be(Slot.Board(3));
        Slot.Board(3).Should().NotBe(Slot.Board(4));
        Slot.PlayerOff.Should().Be(Slot.PlayerOff);
        Slot.OpponentOff.Should().Be(Slot.OpponentOff);
        Slot.PlayerOff.Should().NotBe(Slot.OpponentOff);
        Slot.Board(0).Should().NotBe(Slot.PlayerOff);
    }

    [Fact]
    public void Default_IsBoardIndexZero()
    {
        // Mirrors default(SlotRange) before the slot model: a valid slot at
        // the opponent's bar, never an invalid state.
        default(Slot).Should().Be(Slot.Board(0));
    }

    // -----------------------------------------------------------------------
    //  ToString — the canonical bracket-grammar token head
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_RendersCanonicalTokenHead()
    {
        Slot.Board(6).ToString().Should().Be("6");
        Slot.Board(0).ToString().Should().Be("0");
        Slot.Board(25).ToString().Should().Be("25");
        Slot.PlayerOff.ToString().Should().Be("off");
        Slot.OpponentOff.ToString().Should().Be("opp-off");
    }
}
