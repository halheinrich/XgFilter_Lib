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
        string match = "TestMatch",
        int game = 1,
        int moveNum = 1,
        int roll = 31,
        string depth = "3-ply",
        double equity = 0.0,
        int[]? board = null)
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
            Match = match,
            Game = game,
            MoveNum = moveNum,
            Roll = roll,
            AnalysisDepth = depth,
            Equity = equity,
            Board = board ?? [],
        };
    }

    public static DecisionRow BuildCube(
        string player = "Player1",
        double error = 0.0,
        int onRollNeeds = 3,
        int opponentNeeds = 5,
        bool isCrawford = false,
        int matchLength = 7)
    {
        return Build(player: player, error: error, onRollNeeds: onRollNeeds,
                     opponentNeeds: opponentNeeds, isCrawford: isCrawford,
                     matchLength: matchLength, roll: 0);
    }
}