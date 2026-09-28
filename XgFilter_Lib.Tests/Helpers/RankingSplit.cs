using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// A checker play whose best play — and so the player's result, the best
/// play's depth and its after-board — differs between the two rankings
/// (SPEC-scoring §2a), built through <see cref="TestRecords"/>: the opening
/// 3-1 from the standard start, with three candidates valid from it.
/// <list type="table">
///   <listheader><term>Index</term><description>Play, depth, equity — the result of playing it</description></listheader>
///   <item><term><see cref="DeepBest"/></term><description>8/5 6/5, 4-ply, +0.25 — Equity: scored 0.25; DepthFirst: best, scored 0</description></item>
///   <item><term><see cref="ShallowHigh"/></term><description>13/10 6/5, 2-ply, +0.50 — Equity: best, scored 0; DepthFirst: not scored (shallower yet higher)</description></item>
///   <item><term><see cref="DeepWorst"/></term><description>24/23 13/10, 4-ply, 0.00 — Equity: scored 0.50; DepthFirst: scored 0.25</description></item>
/// </list>
/// The equities are binary fractions, so each error is exact.
/// </summary>
internal static class RankingSplit
{
    /// <summary>8/5 6/5 at 4-ply, +0.25: depth first's best.</summary>
    public const int DeepBest = 0;

    /// <summary>13/10 6/5 at 2-ply, +0.50: equity's best, not scored under depth first.</summary>
    public const int ShallowHigh = 1;

    /// <summary>24/23 13/10 at 4-ply, 0.00: scored under both, with different errors.</summary>
    public const int DeepWorst = 2;

    /// <summary>
    /// The decision with the player's move stated as the candidate at
    /// <paramref name="userPlayIndex"/>; <see langword="null"/> states none,
    /// with <paramref name="unlistedPlayError"/> the analyser's error for a
    /// move off the list, or neither when no move is recorded.
    /// </summary>
    public static CheckerPlayDecision Played(int? userPlayIndex, double? unlistedPlayError = null) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(
            plays:
            [
                TestRecords.Candidate(
                    play: [new(8, 5), new(6, 5)],
                    analysisLevel: AnalysisLevel.Ply4, equity: 0.25),
                TestRecords.Candidate(
                    play: [new(13, 10), new(6, 5)],
                    analysisLevel: AnalysisLevel.Ply2, equity: 0.50),
                TestRecords.Candidate(
                    play: [new(24, 23), new(13, 10)],
                    analysisLevel: AnalysisLevel.Ply4, equity: 0.00),
            ],
            userPlayIndex: userPlayIndex,
            unlistedPlayError: unlistedPlayError));
}
