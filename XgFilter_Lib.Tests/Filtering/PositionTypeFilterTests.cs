using ConvertXgToJson_Lib.Models;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PositionTypeFilterTests
{
    // -----------------------------------------------------------------------
    //  Helper
    // -----------------------------------------------------------------------

    private static DecisionRow MakeRow(params (int index, int count)[] points)
    {
        var board = new int[26];
        foreach (var (index, count) in points)
            board[index] = count;
        return DecisionRowBuilder.Build(board: board);
    }

    // -----------------------------------------------------------------------
    //  Race filter
    // -----------------------------------------------------------------------

    [Fact]
    public void RaceFilter_RacePosition_Passes()
    {
        var filter = new PositionTypeFilter([PositionType.Race]);
        var row = MakeRow((3, 2), (2, 3), (22, -2), (23, -3));
        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void RaceFilter_ContactPosition_DoesNotPass()
    {
        var filter = new PositionTypeFilter([PositionType.Race]);
        // Starting position
        var row = MakeRow((24, 2), (13, 5), (8, 3), (6, 5),
                          (1, -2), (12, -5), (17, -3), (19, -5));
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Contact filter
    // -----------------------------------------------------------------------

    [Fact]
    public void ContactFilter_ContactPosition_Passes()
    {
        var filter = new PositionTypeFilter([PositionType.Contact]);
        var row = MakeRow((24, 2), (13, 5), (8, 3), (6, 5),
                          (1, -2), (12, -5), (17, -3), (19, -5));
        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void ContactFilter_RacePosition_DoesNotPass()
    {
        var filter = new PositionTypeFilter([PositionType.Contact]);
        var row = MakeRow((3, 2), (2, 3), (22, -2), (23, -3));
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Multiple types in filter
    // -----------------------------------------------------------------------

    [Fact]
    public void MultiTypeFilter_RaceAndContact_PassesBoth()
    {
        var filter = new PositionTypeFilter([PositionType.Race, PositionType.Contact]);

        var raceRow = MakeRow((3, 2), (2, 3), (22, -2), (23, -3));
        var contactRow = MakeRow((24, 2), (13, 5), (8, 3), (6, 5),
                                 (1, -2), (12, -5), (17, -3), (19, -5));

        filter.Matches(raceRow).Should().BeTrue();
        filter.Matches(contactRow).Should().BeTrue();
    }

    [Fact]
    public void EmptyTypeList_NothingPasses()
    {
        var filter = new PositionTypeFilter([]);
        var row = MakeRow((3, 2), (2, 3), (22, -2), (23, -3));
        filter.Matches(row).Should().BeFalse();
    }
}