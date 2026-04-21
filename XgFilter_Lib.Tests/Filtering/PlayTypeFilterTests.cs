using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
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

    /// <summary>A classifier that always returns the fixed result.</summary>
    private sealed class FixedClassifier(bool result) : IPlayTypeClassifier
    {
        public bool Matches(
            IReadOnlyList<int> priorBoard,
            IReadOnlyList<int> afterBestBoard,
            IReadOnlyList<int> afterPlayerBoard) => result;
    }

    private static DecisionRow CheckerRow(int[] prior, int[] afterBest, int[] afterPlayer) =>
        DecisionRowBuilder.Build(
            board: prior,
            afterBestBoard: afterBest,
            afterPlayerBoard: afterPlayer);

    // -----------------------------------------------------------------------
    //  Empty classifier collection
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyClassifiers_CheckerRow_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([]);
        var row = CheckerRow(Prior(0), After(2), After(0));
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Cube rows are always excluded
    // -----------------------------------------------------------------------

    [Fact]
    public void CubeRow_AlwaysMatchingClassifier_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([new FixedClassifier(true)]);
        var row = DecisionRowBuilder.BuildCube();
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Single classifier
    // -----------------------------------------------------------------------

    [Fact]
    public void SingleClassifier_Matches_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([new FixedClassifier(true)]);
        var row = CheckerRow(Prior(0), After(2), After(0));
        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void SingleClassifier_DoesNotMatch_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([new FixedClassifier(false)]);
        var row = CheckerRow(Prior(0), After(2), After(0));
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Multiple classifiers — OR semantics
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleClassifiers_OneMatches_ReturnsTrue()
    {
        var filter = new PlayTypeFilter(
            [new FixedClassifier(false), new FixedClassifier(true)]);
        var row = CheckerRow(Prior(0), After(2), After(0));
        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void MultipleClassifiers_NoneMatch_ReturnsFalse()
    {
        var filter = new PlayTypeFilter(
            [new FixedClassifier(false), new FixedClassifier(false)]);
        var row = CheckerRow(Prior(0), After(2), After(0));
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Integration with real Make20PtClassifier via DecisionRow
    // -----------------------------------------------------------------------

    [Fact]
    public void Make20Pt_BestMakes_PlayerDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([new Make20PtClassifier()]);
        var row = CheckerRow(Prior(0), After(2), After(0));
        filter.Matches(row).Should().BeTrue();
    }

    [Fact]
    public void Make20Pt_BothMake_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([new Make20PtClassifier()]);
        var row = CheckerRow(Prior(0), After(2), After(2));
        filter.Matches(row).Should().BeFalse();
    }

    [Fact]
    public void Make20Pt_AlreadyMade_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([new Make20PtClassifier()]);
        var row = CheckerRow(Prior(3), After(3), After(2));
        filter.Matches(row).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Integration with real Make20PtClassifier via BgDecisionData
    // -----------------------------------------------------------------------

    [Fact]
    public void Make20Pt_BgDecisionData_BestMakes_PlayerDoesNot_ReturnsTrue()
    {
        var filter = new PlayTypeFilter([new Make20PtClassifier()]);
        var data = BgDecisionDataBuilder.Build(
            board: Prior(0),
            afterBestBoard: After(2),
            afterPlayerBoard: After(0));
        filter.Matches(data).Should().BeTrue();
    }

    [Fact]
    public void Make20Pt_BgDecisionData_Cube_ReturnsFalse()
    {
        var filter = new PlayTypeFilter([new Make20PtClassifier()]);
        var data = BgDecisionDataBuilder.Build(isCube: true);
        filter.Matches(data).Should().BeFalse();
    }
}
