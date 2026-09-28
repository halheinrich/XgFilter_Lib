using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PositionPatternFilterTests
{
    /// <summary>A checker play made on <paramref name="board"/>, the player on roll's position.</summary>
    private static CheckerPlayDecision OnBoard(BoardPosition board) =>
        TestRecords.CheckerPlay(position: TestRecords.Position(mop: board));

    // Contact fixture with two opponent checkers on the bar.
    private static readonly BoardPosition _vsTwoPlusUpPosition = BoardBuilder.Build(
        (0, -2),
        (24, 2), (13, 5), (8, 3), (6, 5),
        (12, -5), (17, -3), (19, -3));

    private static readonly BoardPosition _startingPosition = BoardBuilder.Build(
        (24, 2), (13, 5), (8, 3), (6, 5),
        (1, -2), (12, -5), (17, -3), (19, -5));

    [Fact]
    public void Filter_DispatchesPatternMatch_OnBothSubstrates()
    {
        // [0,,-2] passes the two-on-bar board and rejects the starting board.
        var filter = new PositionPatternFilter(BoardPattern.Parse("[0,,-2]"));

        AssertMatchesBoth(filter, OnBoard(_vsTwoPlusUpPosition), expected: true);
        AssertMatchesBoth(filter, OnBoard(_startingPosition), expected: false);
    }

    [Fact]
    public void Filter_EmptyPattern_PassesEveryBoard()
    {
        var filter = new PositionPatternFilter(BoardPattern.Empty);

        AssertMatchesBoth(filter, OnBoard(_startingPosition), expected: true);
        AssertMatchesBoth(filter, OnBoard(_vsTwoPlusUpPosition), expected: true);
    }

    [Fact]
    public void Ctor_NullPattern_Throws()
    {
        var act = () => new PositionPatternFilter(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
