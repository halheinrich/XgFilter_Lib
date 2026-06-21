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

    // Holding fixture — 20-point holding game: player holds 13/8/6, opponent
    // anchors on the 20 (board[5]) and the player's 12; nothing on-roll above
    // the 13. See Holding1386Vs20Classifier for the full predicate.
    private static readonly int[] _holding1386Vs20Position = BoardBuilder.Build(
        (13, 5), (8, 3), (6, 4), (4, 2), (1, 1),          // on-roll player (15)
        (5, -2), (12, -3), (19, -4), (21, -4), (23, -2)); // opponent (-15)

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
    //  Holding1386Vs20 filter
    // -----------------------------------------------------------------------

    [Fact]
    public void Holding1386Vs20Filter_HoldingPosition_Passes()
    {
        var filter = new PositionTypeFilter([PositionType.Holding1386Vs20]);
        AssertMatchesBoth(filter, new RowShape(Board: _holding1386Vs20Position), expected: true);
    }

    [Fact]
    public void Holding1386Vs20Filter_StartingPosition_DoesNotPass()
    {
        var filter = new PositionTypeFilter([PositionType.Holding1386Vs20]);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: false);
    }

    [Fact]
    public void Holding1386Vs20ComposesWithContact_BothMatch()
    {
        // A holding position is also Contact — the union semantics let a row
        // pass under either label, and the overlap is by design.
        var filter = new PositionTypeFilter([PositionType.Contact]);
        AssertMatchesBoth(filter, new RowShape(Board: _holding1386Vs20Position), expected: true);

        filter = new PositionTypeFilter([PositionType.Holding1386Vs20]);
        AssertMatchesBoth(filter, new RowShape(Board: _holding1386Vs20Position), expected: true);
    }

    [Fact]
    public void Holding1386Vs20Filter_ConstructsForDefinedValue()
    {
        // Defined enum value passes the Enum.IsDefined construction guard.
        var act = () => new PositionTypeFilter([PositionType.Holding1386Vs20]);
        act.Should().NotThrow();
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
