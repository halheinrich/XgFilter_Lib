using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Tests.Patterns;

public class CheckerLocationTests
{
    // -----------------------------------------------------------------------
    //  Factories — the only construction paths, each validated
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(25)]
    public void Board_IndexAtBoundary_Constructs(int index)
    {
        var location = CheckerLocation.Board(index);
        location.Kind.Should().Be(CheckerLocationKind.Board);
        location.BoardIndex.Should().Be(index);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(26)]
    public void Board_IndexOutOfRange_Throws(int index)
    {
        var act = () => CheckerLocation.Board(index);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void OffLocations_CarryTheirKind_AndNoBoardIndex()
    {
        CheckerLocation.PlayerOff.Kind.Should().Be(CheckerLocationKind.PlayerOff);
        CheckerLocation.PlayerOff.BoardIndex.Should().BeNull();

        CheckerLocation.OpponentOff.Kind.Should().Be(CheckerLocationKind.OpponentOff);
        CheckerLocation.OpponentOff.BoardIndex.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    //  Value-equality — what BoardPattern's duplicate-location key relies on
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        CheckerLocation.Board(3).Should().Be(CheckerLocation.Board(3));
        CheckerLocation.Board(3).Should().NotBe(CheckerLocation.Board(4));
        CheckerLocation.PlayerOff.Should().Be(CheckerLocation.PlayerOff);
        CheckerLocation.OpponentOff.Should().Be(CheckerLocation.OpponentOff);
        CheckerLocation.PlayerOff.Should().NotBe(CheckerLocation.OpponentOff);
        CheckerLocation.Board(0).Should().NotBe(CheckerLocation.PlayerOff);
    }

    [Fact]
    public void Default_IsBoardIndexZero()
    {
        // Mirrors default(CheckerRange) before the location model: a valid
        // location at the opponent's bar, never an invalid state.
        default(CheckerLocation).Should().Be(CheckerLocation.Board(0));
    }

    // -----------------------------------------------------------------------
    //  ToString — the canonical bracket-grammar token head
    // -----------------------------------------------------------------------

    [Fact]
    public void ToString_RendersCanonicalTokenHead()
    {
        CheckerLocation.Board(6).ToString().Should().Be("6");
        CheckerLocation.Board(0).ToString().Should().Be("0");
        CheckerLocation.Board(25).ToString().Should().Be("25");
        CheckerLocation.PlayerOff.ToString().Should().Be("off");
        CheckerLocation.OpponentOff.ToString().Should().Be("opp-off");
    }
}
