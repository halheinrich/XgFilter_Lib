using BgDataTypes_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class AnalysisDepthFilterTests
{
    // -----------------------------------------------------------------------
    //  Single class — membership test on both substrates and both row kinds.
    //  Depth is a scalar the producer stamped; the filter reads it directly.
    // -----------------------------------------------------------------------

    [Fact]
    public void SingleClass_MatchingCheckerRow_Passes()
    {
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Ply3]);
        AssertMatchesBoth(
            filter,
            new RowShape(AnalysisDepthClass: AnalysisDepthClass.Ply3),
            expected: true);
    }

    [Fact]
    public void SingleClass_NonMatchingCheckerRow_DoesNotPass()
    {
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Ply3]);
        AssertMatchesBoth(
            filter,
            new RowShape(AnalysisDepthClass: AnalysisDepthClass.Ply1),
            expected: false);
    }

    [Fact]
    public void SingleClass_MatchingCubeRow_Passes()
    {
        // Cube rows carry a depth just like checker rows (the cube analysis).
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Rollout]);
        AssertMatchesBoth(
            filter,
            new RowShape(IsCube: true, AnalysisDepthClass: AnalysisDepthClass.Rollout),
            expected: true);
    }

    [Fact]
    public void SingleClass_NonMatchingCubeRow_DoesNotPass()
    {
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Rollout]);
        AssertMatchesBoth(
            filter,
            new RowShape(IsCube: true, AnalysisDepthClass: AnalysisDepthClass.XgRoller),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Multiple classes in filter — union (OR) semantics within the facet.
    // -----------------------------------------------------------------------

    [Fact]
    public void MultiClass_AnySelectedClass_Passes()
    {
        var filter = new AnalysisDepthFilter(
            [AnalysisDepthClass.Ply3, AnalysisDepthClass.RolloutPly3]);

        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Ply3), expected: true);
        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.RolloutPly3), expected: true);
    }

    [Fact]
    public void MultiClass_UnselectedClass_DoesNotPass()
    {
        var filter = new AnalysisDepthFilter(
            [AnalysisDepthClass.Ply3, AnalysisDepthClass.RolloutPly3]);

        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Ply4), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Empty include-set — admits nothing (empty OR), same as the other facets.
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyClassList_NothingPasses()
    {
        var filter = new AnalysisDepthFilter([]);
        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Ply3), expected: false);
        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Rollout), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown — drop-don't-pass unless explicitly selected.
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownRow_NotSelected_DoesNotPass()
    {
        // Legacy / unclassified rows carry Unknown; a filter selecting real
        // depths must exclude them (the same convention ErrorRangeFilter uses
        // for a null FilterError).
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Ply3]);
        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Unknown), expected: false);
    }

    [Fact]
    public void UnknownRow_UnknownSelected_Passes()
    {
        // Unknown's selectability is the deliberate opt-in for consumers that
        // want the unclassified tail.
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Unknown]);
        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Unknown), expected: true);
    }

    [Fact]
    public void UnknownSelected_ClassifiedRow_DoesNotPass()
    {
        // Selecting Unknown must not sweep in classified rows.
        var filter = new AnalysisDepthFilter([AnalysisDepthClass.Unknown]);
        AssertMatchesBoth(filter, new RowShape(AnalysisDepthClass: AnalysisDepthClass.Ply3), expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown enum value — fails fast at construction
    // -----------------------------------------------------------------------

    [Fact]
    public void AnalysisDepthFilter_ConstructsForDefinedValue()
    {
        var act = () => new AnalysisDepthFilter([AnalysisDepthClass.Ply3]);
        act.Should().NotThrow();
    }

    [Fact]
    public void UnknownAnalysisDepthClass_Constructor_Throws()
    {
        var act = () => new AnalysisDepthFilter([(AnalysisDepthClass)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownAnalysisDepthClass_MixedWithValid_Constructor_Throws()
    {
        var act = () => new AnalysisDepthFilter([AnalysisDepthClass.Ply3, (AnalysisDepthClass)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
