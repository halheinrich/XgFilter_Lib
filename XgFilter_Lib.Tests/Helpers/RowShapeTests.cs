using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

public class RowShapeTests
{
    // -----------------------------------------------------------------------
    //  Default-shape projections — both substrates agree on common fields
    // -----------------------------------------------------------------------

    [Fact]
    public void ToDecisionRow_Defaults_ProducesCheckerPlayWithMatchingFields()
    {
        var row = new RowShape().ToDecisionRow();

        row.Player.Should().Be("Player1");
        row.IsCube.Should().BeFalse();
        row.OnRollNeeds.Should().Be(3);
        row.OpponentNeeds.Should().Be(5);
        row.IsCrawford.Should().BeFalse();
        row.MatchLength.Should().Be(7);
        row.FilterError.Should().Be(0.0);
    }

    [Fact]
    public void ToBgDecisionData_Defaults_ProducesCheckerPlayWithMatchingFields()
    {
        var data = new RowShape().ToBgDecisionData();

        data.Player.Should().Be("Player1");
        data.IsCube.Should().BeFalse();
        data.OnRollNeeds.Should().Be(3);
        data.OpponentNeeds.Should().Be(5);
        data.IsCrawford.Should().BeFalse();
        data.MatchLength.Should().Be(7);
        data.FilterError.Should().Be(0.0);
    }

    // -----------------------------------------------------------------------
    //  Cube projections
    // -----------------------------------------------------------------------

    [Fact]
    public void Cube_DecisionRow_IsMarkedCube()
    {
        new RowShape(IsCube: true).ToDecisionRow().IsCube.Should().BeTrue();
    }

    [Fact]
    public void Cube_BgDecisionData_IsMarkedCube()
    {
        new RowShape(IsCube: true).ToBgDecisionData().IsCube.Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Null error — not expressible on DecisionRow (substrate asymmetry)
    // -----------------------------------------------------------------------

    [Fact]
    public void NullError_BgDecisionData_ProducesNullFilterError()
    {
        new RowShape(Error: null).ToBgDecisionData().FilterError.Should().BeNull();
    }

    // -----------------------------------------------------------------------
    //  Board projection — both substrates preserve the array
    // -----------------------------------------------------------------------

    [Fact]
    public void Board_BothSubstrates_PreserveArray()
    {
        var board = new int[26];
        board[6] = 5;
        board[20] = 2;
        var shape = new RowShape(Board: board);

        shape.ToDecisionRow().Board.Should().Equal(board);
        shape.ToBgDecisionData().Board.Should().Equal(board);
    }

    [Fact]
    public void AfterBoards_BothSubstrates_PreserveArrays()
    {
        var best = new int[26];
        best[5] = -2;
        var player = new int[26];
        player[5] = 0;
        var shape = new RowShape(AfterBestBoard: best, AfterPlayerBoard: player);

        shape.ToDecisionRow().AfterBestBoard.Should().Equal(best);
        shape.ToDecisionRow().AfterPlayerBoard.Should().Equal(player);
        shape.ToBgDecisionData().AfterBestBoard.Should().Equal(best);
        shape.ToBgDecisionData().AfterPlayerBoard.Should().Equal(player);
    }

    [Fact]
    public void NullAfterBoards_BothSubstrates_ProjectAsEmpty()
    {
        var shape = new RowShape();

        shape.ToDecisionRow().AfterBestBoard.Should().BeEmpty();
        shape.ToDecisionRow().AfterPlayerBoard.Should().BeEmpty();
        shape.ToBgDecisionData().AfterBestBoard.Should().BeEmpty();
        shape.ToBgDecisionData().AfterPlayerBoard.Should().BeEmpty();
    }
}
