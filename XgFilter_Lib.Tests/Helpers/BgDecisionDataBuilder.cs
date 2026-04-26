using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

internal static class BgDecisionDataBuilder
{
    public static BgDecisionData Build(
        string player = "Player1",
        bool isCube = false,
        int onRollNeeds = 3,
        int opponentNeeds = 5,
        bool isCrawford = false,
        int matchLength = 7,
        int moveNumber = 1,
        bool isStandardStart = true,
        double? userPlayError = 0.0,
        double? userDoubleError = null,
        double? userTakeError = null,
        int[]? board = null,
        int[]? afterBestBoard = null,
        int[]? afterPlayerBoard = null)
    {
        return new BgDecisionData
        {
            Descriptive = new DescriptiveData
            {
                OnRollName = player,
                MatchLength = matchLength,
                MoveNumber = moveNumber,
                IsStandardStart = isStandardStart,
            },
            Decision = new DecisionData
            {
                IsCube = isCube,
                UserPlayError = userPlayError,
                UserDoubleError = userDoubleError,
                UserTakeError = userTakeError,
            },
            Position = new PositionData
            {
                OnRollNeeds = onRollNeeds,
                OpponentNeeds = opponentNeeds,
                IsCrawford = isCrawford,
                Mop = board ?? new int[26],
            },
            Outcome = new PlayOutcomeData
            {
                AfterBestBoard = afterBestBoard ?? [],
                AfterPlayerBoard = afterPlayerBoard ?? [],
            },
        };
    }
}