using BgDataTypes_Lib;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class PlayTypeFilterTests
{
    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    // priorBoard: decision-maker on roll. Their 20-point is index 20,
    // checkers positive.
    private static int[] Prior(int at20)
    {
        var b = new int[26];
        b[20] = at20;
        return b;
    }

    // after-board: opponent on roll. Decision-maker's 20-point is index 5,
    // their checkers are negative; opponent's are positive.
    private static int[] After(int decisionMakerCount, int opponentCount = 0)
    {
        var b = new int[26];
        b[5] = opponentCount - decisionMakerCount;
        return b;
    }

    /// <summary>
    /// Exercises both <see cref="IDecisionFilterData"/> substrates — the
    /// <see cref="DecisionRow"/> (CSV-shaped) and <see cref="BgDecisionData"/>
    /// (diagram-shaped) paths — with a single assertion call. Every filter
    /// case that doesn't care about substrate-specific fields should go
    /// through this helper.
    /// </summary>
    private static void AssertMatches(
        PlayTypeFilter filter,
        int[] prior, int[] afterBest, int[] afterPlayer,
        bool isCube, bool expected)
    {
        var row = isCube
            ? DecisionRowBuilder.BuildCube()
            : DecisionRowBuilder.Build(
                board: prior, afterBestBoard: afterBest, afterPlayerBoard: afterPlayer);
        var data = BgDecisionDataBuilder.Build(
            isCube: isCube,
            board: prior, afterBestBoard: afterBest, afterPlayerBoard: afterPlayer);

        filter.Matches(row ).Should().Be(expected, "DecisionRow substrate");
        filter.Matches(data).Should().Be(expected, "BgDecisionData substrate");
    }

    // -----------------------------------------------------------------------
    //  Empty type set
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyTypes_CheckerRow_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([]);
        AssertMatches(filter, Prior(0), After(2), After(0), isCube: false, expected: false);
    }

    // -----------------------------------------------------------------------
    //  Cube rows are always excluded
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeRow_SelectedType_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatches(filter, Prior(0), After(2), After(0), isCube: true, expected: false);
    }

    // -----------------------------------------------------------------------
    //  Make20Pt behavioural coverage
    // -----------------------------------------------------------------------

    [Fact]
    public void Make20Pt_BestMakes_PlayerDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatches(filter, Prior(0), After(2), After(0), isCube: false, expected: true);
    }

    [Fact]
    public void Make20Pt_PlayerMakes_BestDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatches(filter, Prior(0), After(0), After(2), isCube: false, expected: true);
    }

    [Fact]
    public void Make20Pt_BothMake_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatches(filter, Prior(0), After(2), After(2), isCube: false, expected: false);
    }

    [Fact]
    public void Make20Pt_AlreadyMade_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([PlayType.Make20Pt]);
        AssertMatches(filter, Prior(3), After(3), After(2), isCube: false, expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown enum value — contract: throw
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownPlayType_CheckerRow_Throws()
    {
        var filter = new PlayTypeFilter([(PlayType)999]);
        var row = DecisionRowBuilder.Build(
            board: Prior(0), afterBestBoard: After(2), afterPlayerBoard: After(0));

        var act = () => filter.Matches(row);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
