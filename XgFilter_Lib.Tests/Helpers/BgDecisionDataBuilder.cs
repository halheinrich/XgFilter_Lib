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
        double? userPlayError = 0.0,
        double? userDoubleError = null,
        double? userTakeError = null,
        int[]? board = null)
    {
        return new BgDecisionData
        {
            Descriptive = new DescriptiveData
            {
                OnRollName = player,
                MatchLength = matchLength,
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
        };
    }
}