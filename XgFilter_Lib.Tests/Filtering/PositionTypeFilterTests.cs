using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PositionTypeFilterTests
{
    // Starting-position fixture — clear contact position
    private static readonly int[] _startingPosition = BoardBuilder.Build(
        (24, 2), (13, 5), (8, 3), (6, 5),
        (1, -2), (12, -5), (17, -3), (19, -5));

    // Race fixture — all player checkers past all opponent checkers
    private static readonly int[] _racePosition = BoardBuilder.Build(
        (3, 2), (2, 3), (22, -2), (23, -3));

    // Contact fixture with two opponent checkers on the bar
    private static readonly int[] _vsTwoPlusUpPosition = BoardBuilder.Build(
        (0, -2),
        (24, 2), (13, 5), (8, 3), (6, 5),
        (12, -5), (17, -3), (19, -3));

    // -----------------------------------------------------------------------
    //  Race filter
    // -----------------------------------------------------------------------

    [Fact]
    public void RaceFilter_RacePosition_Passes()
    {
        var filter = new PositionTypeFilter([PositionType.Race]);
        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: true);
    }

    [Fact]
    public void RaceFilter_ContactPosition_DoesNotPass()
    {
        var filter = new PositionTypeFilter([PositionType.Race]);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Contact filter
    // -----------------------------------------------------------------------

    [Fact]
    public void ContactFilter_ContactPosition_Passes()
    {
        var filter = new PositionTypeFilter([PositionType.Contact]);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: true);
    }

    [Fact]
    public void ContactFilter_RacePosition_DoesNotPass()
    {
        var filter = new PositionTypeFilter([PositionType.Contact]);
        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  VsTwoPlusUp filter
    // -----------------------------------------------------------------------

    [Fact]
    public void VsTwoPlusUpFilter_OpponentTwoOnBar_Passes()
    {
        var filter = new PositionTypeFilter([PositionType.VsTwoPlusUp]);
        AssertMatchesBoth(filter, new RowShape(Board: _vsTwoPlusUpPosition), expected: true);
    }

    [Fact]
    public void VsTwoPlusUpFilter_StartingPosition_DoesNotPass()
    {
        var filter = new PositionTypeFilter([PositionType.VsTwoPlusUp]);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Multiple types in filter
    // -----------------------------------------------------------------------

    [Fact]
    public void MultiTypeFilter_RaceAndContact_PassesBoth()
    {
        var filter = new PositionTypeFilter([PositionType.Race, PositionType.Contact]);

        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: true);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: true);
    }

    [Fact]
    public void MultiTypeFilter_VsTwoPlusUpComposesWithContact_BothMatch()
    {
        // A VsTwoPlusUp position is also Contact — the union semantics let
        // a row pass under either label, and overlap is by design.
        var filter = new PositionTypeFilter([PositionType.Contact]);
        AssertMatchesBoth(filter, new RowShape(Board: _vsTwoPlusUpPosition), expected: true);

        filter = new PositionTypeFilter([PositionType.VsTwoPlusUp]);
        AssertMatchesBoth(filter, new RowShape(Board: _vsTwoPlusUpPosition), expected: true);
    }

    [Fact]
    public void EmptyTypeList_NothingPasses()
    {
        var filter = new PositionTypeFilter([]);
        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown enum value — fails fast at construction
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownPositionType_Constructor_Throws()
    {
        var act = () => new PositionTypeFilter([(PositionType)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownPositionType_MixedWithValid_Constructor_Throws()
    {
        var act = () => new PositionTypeFilter([PositionType.Race, (PositionType)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
