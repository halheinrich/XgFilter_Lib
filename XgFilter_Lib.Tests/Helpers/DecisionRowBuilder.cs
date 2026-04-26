using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

internal static class DecisionRowBuilder
{
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
        double equity = 0.0,
        int[]? board = null,
        int[]? afterBestBoard = null,
        int[]? afterPlayerBoard = null)
    {
        return new DecisionRow
        {
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
            Equity = equity,
            Board = board ?? [],
            AfterBestBoard = afterBestBoard ?? [],
            AfterPlayerBoard = afterPlayerBoard ?? [],
        };
    }

    public static DecisionRow BuildCube(
        string player = "Player1",
        double error = 0.0,
        int onRollNeeds = 3,
        int opponentNeeds = 5,
        bool isCrawford = false,
        int matchLength = 7,
        int moveNumber = 1,
        bool isStandardStart = true)
    {
        return Build(player: player, error: error, onRollNeeds: onRollNeeds,
                     opponentNeeds: opponentNeeds, isCrawford: isCrawford,
                     matchLength: matchLength, moveNumber: moveNumber,
                     isStandardStart: isStandardStart, roll: 0);
    }
}