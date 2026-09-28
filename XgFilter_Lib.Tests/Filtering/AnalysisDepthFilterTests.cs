using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;
using Clause = XgFilter_Lib.Filtering.AnalysisDepthFilter.Clause;

namespace XgFilter_Lib.Tests.Filtering;

/// <summary>
/// Mechanics of the clause-union depth filter in isolation — a row passes iff
/// any per-mode clause admits it (mode equality AND membership in that
/// clause's own level set, empty = any level). The facet-level derivation of
/// the clause union from user intent (one clause per enabled toggle, carrying
/// its own level list; the inactive rule) is <see cref="FilterConfig.Build"/>'s
/// job and is pinned in <c>FilterConfigTests</c>.
/// </summary>
public class AnalysisDepthFilterTests
{
    /// <summary>
    /// A checker play whose one candidate — so its best under either ranking —
    /// was analysed in <paramref name="mode"/> at <paramref name="level"/>.
    /// </summary>
    private static CheckerPlayDecision Analysed(AnalysisMode mode, AnalysisLevel level) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(
            plays: [TestRecords.Candidate(analysisMode: mode, analysisLevel: level)]));

    /// <summary>A cube decision whose cube analysis was in <paramref name="mode"/> at <paramref name="level"/>.</summary>
    private static CubeDecision CubeAnalysed(AnalysisMode mode, AnalysisLevel level) =>
        TestRecords.Cube(decision: TestRecords.CubeData(analysisMode: mode, analysisLevel: level));

    // -----------------------------------------------------------------------
    //  Single clause — mode equality AND per-clause level membership.
    // -----------------------------------------------------------------------

    [Fact]
    public void ModeAndLevelBothMatch_Passes()
    {
        var filter = new AnalysisDepthFilter(
            [new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply3])]);
        AssertMatchesBoth(
            filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply3),
            expected: true);
    }

    [Fact]
    public void ModeMismatch_DoesNotPass()
    {
        // Level matches but mode does not — a clause admits only its own mode.
        var filter = new AnalysisDepthFilter(
            [new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply3])]);
        AssertMatchesBoth(
            filter,
            Analysed(AnalysisMode.Rollout, AnalysisLevel.Ply3),
            expected: false);
    }

    [Fact]
    public void LevelMismatch_DoesNotPass()
    {
        // Mode matches but level does not.
        var filter = new AnalysisDepthFilter(
            [new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply3])]);
        AssertMatchesBoth(
            filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply4),
            expected: false);
    }

    [Fact]
    public void CubeDecision_AdmittedByClause_Passes()
    {
        // A cube decision carries the (mode, level) pair too — its cube analysis.
        var filter = new AnalysisDepthFilter(
            [new Clause(AnalysisMode.Rollout, [AnalysisLevel.XgRoller])]);
        AssertMatchesBoth(
            filter,
            CubeAnalysed(AnalysisMode.Rollout, AnalysisLevel.XgRoller),
            expected: true);
    }

    [Fact]
    public void MultipleLevelsWithinClause_AnyMatchingLevel_Passes()
    {
        var filter = new AnalysisDepthFilter(
            [new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply3, AnalysisLevel.Ply4])]);

        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply3),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply4),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply5),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Union across clauses — a row passes iff ANY clause admits it, and a
    //  level selection qualifies only its own clause.
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleClauses_AnyAdmittingClause_Passes()
    {
        var filter = new AnalysisDepthFilter(
        [
            new Clause(AnalysisMode.Rollout, [AnalysisLevel.Ply3]),
            new Clause(AnalysisMode.BookRollout, [AnalysisLevel.XgRoller]),
        ]);

        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Rollout, AnalysisLevel.Ply3),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.BookRollout, AnalysisLevel.XgRoller),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply3),
            expected: false);
    }

    [Fact]
    public void LevelsQualifyOnlyTheirOwnClause()
    {
        // The defect the union shape exists to fix: an unconstrained rollout
        // clause alongside a level-constrained evaluation clause. The
        // evaluation levels must not leak onto rollout rows (whose level is
        // the rollout's INNER level), and vice versa.
        var filter = new AnalysisDepthFilter(
        [
            new Clause(AnalysisMode.Rollout, []),
            new Clause(AnalysisMode.Evaluation, [AnalysisLevel.XgRollerPlusPlus]),
        ]);

        // Rollout rows pass at any inner level — Ply3 is not in the
        // evaluation clause's set, and that set must not constrain them.
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Rollout, AnalysisLevel.Ply3),
            expected: true);
        // Evaluation rows pass only at the level checked FOR evaluations.
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.XgRollerPlusPlus),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply3),
            expected: false);
    }

    [Fact]
    public void DuplicateModeClauses_UnionTheirLevels()
    {
        // Build never produces two clauses on one mode, but the filter's union
        // semantics make the shape well-defined rather than an error: the
        // clauses simply OR.
        var filter = new AnalysisDepthFilter(
        [
            new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply3]),
            new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply4]),
        ]);

        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply3),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply4),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Ply5),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Empty level set — the clause's level axis is unconstrained (any level).
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyLevels_AnyLevelOfClauseMode_Passes()
    {
        // A mode-only clause: every level of its mode passes, including the
        // Unknown level — this is the path an unenriched book hit takes.
        var filter = new AnalysisDepthFilter([new Clause(AnalysisMode.BookRollout, [])]);

        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.BookRollout, AnalysisLevel.Unknown),
            expected: true);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.BookRollout, AnalysisLevel.XgRoller),
            expected: true);
    }

    [Fact]
    public void EmptyLevels_StillGatesOnMode()
    {
        // "Any level" does not mean "any mode" — the clause's mode still applies.
        var filter = new AnalysisDepthFilter([new Clause(AnalysisMode.BookRollout, [])]);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Evaluation, AnalysisLevel.Unknown),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  Unknown mode — never admitted by an active filter, because no clause
    //  can name mode Unknown (the Clause constructor rejects it).
    // -----------------------------------------------------------------------

    [Fact]
    public void UnknownModeRow_DoesNotPassActiveFilter()
    {
        // Legacy / unstamped rows carry Unknown mode; an active depth facet
        // drops them (they pass only when the facet is inactive and the filter
        // is absent — see FilterConfigTests).
        var filter = new AnalysisDepthFilter([new Clause(AnalysisMode.Evaluation, [])]);
        AssertMatchesBoth(filter,
            Analysed(AnalysisMode.Unknown, AnalysisLevel.Unknown),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  The ranking in force: a checker play's depth is its best play's under
    //  the ranking the view was built for (SPEC-scoring §2a).
    // -----------------------------------------------------------------------

    [Fact]
    public void CheckerPlay_DepthIsTheRankingsBestPlays()
    {
        // Equity's best is the 2-ply play; depth first's is a 4-ply one. A
        // clause on either level admits the decision under exactly the
        // ranking whose best was analysed at it.
        var record = RankingSplit.Played(RankingSplit.DeepBest);
        var fourPly = new AnalysisDepthFilter([new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply4])]);
        var twoPly = new AnalysisDepthFilter([new Clause(AnalysisMode.Evaluation, [AnalysisLevel.Ply2])]);

        AssertMatchesBoth(fourPly, record, expected: true, PlayRanking.DepthFirst);
        AssertMatchesBoth(fourPly, record, expected: false, PlayRanking.Equity);
        AssertMatchesBoth(twoPly, record, expected: true, PlayRanking.Equity);
        AssertMatchesBoth(twoPly, record, expected: false, PlayRanking.DepthFirst);
    }

    // -----------------------------------------------------------------------
    //  Construction guards.
    // -----------------------------------------------------------------------

    [Fact]
    public void ConstructsForDefinedValues()
    {
        var act = () => new AnalysisDepthFilter(
            [new Clause(AnalysisMode.Rollout, [AnalysisLevel.Ply3])]);
        act.Should().NotThrow();
    }

    [Fact]
    public void EmptyLevels_IsLegal()
    {
        // A clause's level axis is optional; only the clause collection must
        // be non-empty.
        var act = () => new AnalysisDepthFilter([new Clause(AnalysisMode.Rollout, [])]);
        act.Should().NotThrow();
    }

    [Fact]
    public void EmptyClauses_Throws()
    {
        var act = () => new AnalysisDepthFilter([]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NullClause_Throws()
    {
        var act = () => new AnalysisDepthFilter([null!]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UndefinedMode_ClauseConstructor_Throws()
    {
        var act = () => new Clause((AnalysisMode)999, [AnalysisLevel.Ply3]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void UnknownMode_ClauseConstructor_Throws()
    {
        // Unknown is a defined member but not a selectable one — no UI
        // selection produces it, so a clause naming it is a usage error.
        var act = () => new Clause(AnalysisMode.Unknown, [AnalysisLevel.Ply3]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UndefinedLevel_ClauseConstructor_Throws()
    {
        var act = () => new Clause(AnalysisMode.Rollout, [(AnalysisLevel)999]);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
