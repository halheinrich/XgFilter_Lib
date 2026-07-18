using BgDataTypes_Lib;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

/// <summary>
/// Mechanics of the two-axis depth filter in isolation — mode axis AND level
/// axis, over the pre-derived (modes, levels) it receives. The facet-level
/// derivation of those sets from user intent (which toggles map to which modes,
/// the Evaluation default, the inactive rule) is <see cref="FilterConfig.Build"/>'s
/// job and is pinned in <c>FilterConfigTests</c>.
/// </summary>
public class AnalysisDepthFilterTests
{
    // -----------------------------------------------------------------------
    //  Two-axis membership — a row passes iff mode ∈ modes AND level ∈ levels.
    // -----------------------------------------------------------------------

    [Fact]
    public void ModeAndLevelBothMatch_Passes()
    {
        var filter = new AnalysisDepthFilter([AnalysisMode.Evaluation], [AnalysisLevel.Ply3]);
        AssertMatchesBoth(
            filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply3),
            expected: true);
    }

    [Fact]
    public void ModeMismatch_DoesNotPass()
    {
        // Level matches but mode does not — the AND across axes rejects it.
        var filter = new AnalysisDepthFilter([AnalysisMode.Evaluation], [AnalysisLevel.Ply3]);
        AssertMatchesBoth(
            filter,
            new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply3),
            expected: false);
    }

    [Fact]
    public void LevelMismatch_DoesNotPass()
    {
        // Mode matches but level does not.
        var filter = new AnalysisDepthFilter([AnalysisMode.Evaluation], [AnalysisLevel.Ply3]);
        AssertMatchesBoth(
            filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4),
            expected: false);
    }

    [Fact]
    public void CubeRow_MatchingBothAxes_Passes()
    {
        // Cube rows carry the (mode, level) pair too — the cube analysis.
        var filter = new AnalysisDepthFilter([AnalysisMode.Rollout], [AnalysisLevel.XgRoller]);
        AssertMatchesBoth(
            filter,
            new RowShape(IsCube: true, AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.XgRoller),
            expected: true);
    }

    // -----------------------------------------------------------------------
    //  OR within each axis — multiple modes / multiple levels.
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleModes_AnyMatchingMode_Passes()
    {
        var filter = new AnalysisDepthFilter(
            [AnalysisMode.Rollout, AnalysisMode.BookRollout],
            [AnalysisLevel.Ply3]);

        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply3),
            expected: true);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Ply3),
            expected: true);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply3),
            expected: false);
    }

    [Fact]
    public void MultipleLevels_AnyMatchingLevel_Passes()
    {
        var filter = new AnalysisDepthFilter(
            [AnalysisMode.Evaluation],
            [AnalysisLevel.Ply3, AnalysisLevel.Ply4]);

        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply3),
            expected: true);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4),
            expected: true);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply5),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Empty level set — the level axis is unconstrained (any level).
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyLevels_AnyLevelOfMatchingMode_Passes()
    {
        // A mode-only filter: every level of a selected mode passes, including
        // the Unknown level — this is the path an unenriched book hit takes.
        var filter = new AnalysisDepthFilter([AnalysisMode.BookRollout], []);

        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown),
            expected: true);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.XgRoller),
            expected: true);
    }

    [Fact]
    public void EmptyLevels_StillGatesOnMode()
    {
        // "Any level" does not mean "any mode" — the mode axis still applies.
        var filter = new AnalysisDepthFilter([AnalysisMode.BookRollout], []);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Unknown),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Canonical example (the user's own): 4-ply checked + Rollouts on.
    //  Derived modes = {Rollout}, levels = {Ply4}. Only 4-ply rollouts pass.
    // -----------------------------------------------------------------------

    [Fact]
    public void Canonical_FourPlyRollouts_OnlyFourPlyRolloutsPass()
    {
        var filter = new AnalysisDepthFilter([AnalysisMode.Rollout], [AnalysisLevel.Ply4]);

        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4),
            expected: true);
        // A plain 4-ply evaluation is the same level but a different mode.
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4),
            expected: false);
        // A rollout at a different level.
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply3),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown mode — never admitted by an active filter, because no selection
    //  produces mode Unknown (FilterConfig.Build never supplies it).
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownModeRow_DoesNotPassActiveFilter()
    {
        // Legacy / unstamped rows carry Unknown mode; an active depth facet
        // drops them (they pass only when the facet is inactive and the filter
        // is absent — see FilterConfigTests).
        var filter = new AnalysisDepthFilter([AnalysisMode.Evaluation], []);
        AssertMatchesBoth(filter,
            new RowShape(AnalysisMode: AnalysisMode.Unknown, AnalysisLevel: AnalysisLevel.Unknown),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Construction guards.
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstructsForDefinedValues()
    {
        var act = () => new AnalysisDepthFilter([AnalysisMode.Rollout], [AnalysisLevel.Ply3]);
        act.Should().NotThrow();
    }

    [Fact]
    public void EmptyLevels_IsLegal()
    {
        // The level axis is optional; only the mode set must be non-empty.
        var act = () => new AnalysisDepthFilter([AnalysisMode.Rollout], []);
        act.Should().NotThrow();
    }

    [Fact]
    public void EmptyModes_Throws()
    {
        var act = () => new AnalysisDepthFilter([], [AnalysisLevel.Ply3]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UnknownMode_Constructor_Throws()
    {
        var act = () => new AnalysisDepthFilter([(AnalysisMode)999], [AnalysisLevel.Ply3]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownLevel_Constructor_Throws()
    {
        var act = () => new AnalysisDepthFilter([AnalysisMode.Rollout], [(AnalysisLevel)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
