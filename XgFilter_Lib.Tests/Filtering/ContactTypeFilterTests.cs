using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class ContactTypeFilterTests
{
    // Starting-position fixture — clear contact position
    private static readonly int[] _startingPosition = BoardBuilder.Build(
        (24, 2), (13, 5), (8, 3), (6, 5),
        (1, -2), (12, -5), (17, -3), (19, -5));

    // Race fixture — all player checkers past all opponent checkers
    private static readonly int[] _racePosition = BoardBuilder.Build(
        (3, 2), (2, 3), (22, -2), (23, -3));

    // -----------------------------------------------------------------------
    //  Race filter
    // -----------------------------------------------------------------------

    [Fact]
    public void RaceFilter_RacePosition_Passes()
    {
        var filter = new ContactTypeFilter([ContactType.Race]);
        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: true);
    }

    [Fact]
    public void RaceFilter_ContactPosition_DoesNotPass()
    {
        var filter = new ContactTypeFilter([ContactType.Race]);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Contact filter
    // -----------------------------------------------------------------------

    [Fact]
    public void ContactFilter_ContactPosition_Passes()
    {
        var filter = new ContactTypeFilter([ContactType.Contact]);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: true);
    }

    [Fact]
    public void ContactFilter_RacePosition_DoesNotPass()
    {
        var filter = new ContactTypeFilter([ContactType.Contact]);
        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Multiple types in filter — union (OR) semantics within the facet.
    //  Contact and Race partition every position, so the pair admits both.
    // -----------------------------------------------------------------------

    [Fact]
    public void MultiTypeFilter_RaceAndContact_PassesBoth()
    {
        var filter = new ContactTypeFilter([ContactType.Race, ContactType.Contact]);

        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: true);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: true);
    }

    [Fact]
    public void EmptyTypeList_NothingPasses()
    {
        var filter = new ContactTypeFilter([]);
        AssertMatchesBoth(filter, new RowShape(Board: _racePosition), expected: false);
        AssertMatchesBoth(filter, new RowShape(Board: _startingPosition), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown enum value — fails fast at construction
    // -----------------------------------------------------------------------

    [Fact]
    public void ContactTypeFilter_ConstructsForDefinedValue()
    {
        // Defined enum value passes the Enum.IsDefined construction guard.
        var act = () => new ContactTypeFilter([ContactType.Contact]);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnknownContactType_Constructor_Throws()
    {
        var act = () => new ContactTypeFilter([(ContactType)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownContactType_MixedWithValid_Constructor_Throws()
    {
        var act = () => new ContactTypeFilter([ContactType.Contact, (ContactType)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
