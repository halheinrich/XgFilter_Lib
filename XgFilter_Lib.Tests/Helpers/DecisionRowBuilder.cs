using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Test-only builder for <see cref="DecisionRow"/>. Every constructed
/// instance carries a fixed placeholder <see cref="DecisionRow.Id"/> of
/// <c>new XgpDecisionId("test.xgp")</c> — the required-Id contract has to be
/// satisfied, but the existing filter-test suites do not assert on Id, so a
/// shared placeholder is sufficient. Callers that need to assert on Id (or
/// on Id-derived behaviour) should construct <see cref="DecisionRow"/>
/// explicitly rather than use this builder, since multiple builder-constructed
/// instances are otherwise indistinguishable by Id.
/// </summary>
internal static class DecisionRowBuilder
{
    /// <summary>
    /// Builds a <see cref="DecisionRow"/> populated from the supplied
    /// arguments. The <see cref="DecisionRow.Id"/> field is set to the
    /// shared placeholder described on <see cref="DecisionRowBuilder"/>;
    /// it is not parameterised.
    /// </summary>
    public static DecisionRow Build(
        string xgid = "XGID=-b----E-C---eE---c-e----B-:0:0:1:00:0:0:0:0:10",
        double error = 0.0,
        int onRollNeeds = 3,
        int opponentNeeds = 5,
        bool isCrawford = false,
        int matchLength = 7,
        string player = "Player1",
        string sourceFile = "TestMatch.xg",
        int game = 1,
        int moveNumber = 1,
        bool isStandardStart = true,
        int roll = 31,
        string depth = "3-ply",
        AnalysisDepthClass depthClass = AnalysisDepthClass.Unknown,
        double equity = 0.0,
        int[]? board = null,
        int[]? afterBestBoard = null,
        int[]? afterPlayerBoard = null)
    {
        return new DecisionRow
        {
            Id = new XgpDecisionId("test.xgp"),
            Xgid = xgid,
            Error = error,
            OnRollNeeds = onRollNeeds,
            OpponentNeeds = opponentNeeds,
            IsCrawford = isCrawford,
            MatchLength = matchLength,
            Player = player,
            SourceFile = sourceFile,
            Game = game,
            MoveNumber = moveNumber,
            IsStandardStart = isStandardStart,
            Roll = roll,
            AnalysisDepth = depth,
            AnalysisDepthClass = depthClass,
            Equity = equity,
            Board = board ?? [],
            AfterBestBoard = afterBestBoard ?? [],
            AfterPlayerBoard = afterPlayerBoard ?? [],
        };
    }

    /// <summary>
    /// Builds a cube-decision <see cref="DecisionRow"/> by delegating to
    /// <see cref="Build"/> with <c>roll = 0</c>. Inherits the shared
    /// placeholder <see cref="DecisionRow.Id"/> — see
    /// <see cref="DecisionRowBuilder"/>.
    /// </summary>
    public static DecisionRow BuildCube(
        string player = "Player1",
        double error = 0.0,
        int onRollNeeds = 3,
        int opponentNeeds = 5,
        bool isCrawford = false,
        int matchLength = 7,
        int moveNumber = 1,
        bool isStandardStart = true,
        AnalysisDepthClass depthClass = AnalysisDepthClass.Unknown)
    {
        return Build(player: player, error: error, onRollNeeds: onRollNeeds,
                     opponentNeeds: opponentNeeds, isCrawford: isCrawford,
                     matchLength: matchLength, moveNumber: moveNumber,
                     isStandardStart: isStandardStart, roll: 0,
                     depthClass: depthClass);
    }
}
