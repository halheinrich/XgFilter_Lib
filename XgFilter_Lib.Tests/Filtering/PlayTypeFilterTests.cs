using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;
using static XgFilter_Lib.Tests.Helpers.DecisionFilterAsserts;

namespace XgFilter_Lib.Tests.Filtering;

public class PlayTypeFilterTests
{
    // -----------------------------------------------------------------------
    //  Helpers — priorBoard is decision-maker's POV (20-point at index 20,
    //  checkers positive); afterBoards are flipped to opponent's POV
    //  (decision-maker's 20-point at index 5, checkers negative).
    // -----------------------------------------------------------------------

    private static int[] Prior(int at20)
    {
        var b = new int[26];
        b[20] = at20;
        return b;
    }

    private static int[] After(int decisionMakerCount, int opponentCount = 0)
    {
        var b = new int[26];
        b[5] = opponentCount - decisionMakerCount;
        return b;
    }

    private static RowShape Shape(int[] prior, int[] afterBest, int[] afterPlayer, bool isCube = false) =>
        new RowShape(
            IsCube: isCube,
            Board: prior,
            AfterBestBoard: afterBest,
            AfterPlayerBoard: afterPlayer);

    // -----------------------------------------------------------------------
    //  Empty type set
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyTypes_CheckerRow_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([]);
        AssertMatchesBoth(filter, Shape(Prior(0), After(2), After(0)), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Cube rows are always excluded
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeRow_SelectedType_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, Shape(Prior(0), After(2), After(0), isCube: true), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Make20Pt behavioural coverage
    // -----------------------------------------------------------------------

    [Fact]
    public void Make20Pt_BestMakes_PlayerDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, Shape(Prior(0), After(2), After(0)), expected: true);
    }

    [Fact]
    public void Make20Pt_PlayerMakes_BestDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, Shape(Prior(0), After(0), After(2)), expected: true);
    }

    [Fact]
    public void Make20Pt_BothMake_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, Shape(Prior(0), After(2), After(2)), expected: false);
    }

    [Fact]
    public void Make20Pt_AlreadyMade_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatchesBoth(filter, Shape(Prior(3), After(3), After(2)), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown enum value — contract: throw
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownPlayType_CheckerRow_Throws()
    {
        var filter = new PlayTypeFilter([(PlayType)999]);
        var row = Shape(Prior(0), After(2), After(0)).ToDecisionRow();

        var act = () => filter.Matches(row);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
