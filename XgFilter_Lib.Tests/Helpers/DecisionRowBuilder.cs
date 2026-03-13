using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Tests.Helpers;

internal static class DecisionRowBuilder
{
    public static DecisionRow Build(
        string xgid = "XGID=-b----E-C---eE---c-e----B-:0:0:1:00:0:0:0:0:10",
        double error = 0.0,
        string matchScore = "0a0aC",
        int matchLength = 5,
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
            MatchScore = matchScore,
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
        string matchScore = "0a0aC",
        int matchLength = 5)
    {
        return Build(player: player, error: error, matchScore: matchScore,
                     matchLength: matchLength, roll: 0);
    }
}