using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Test-only builder for <see cref="BgDecisionData"/>. Every constructed
/// instance carries a fixed placeholder <see cref="BgDecisionData.Id"/> of
/// <c>new XgpDecisionId("test.xgp")</c> — the required-Id contract has to be
/// satisfied, but the existing filter-test suites do not assert on Id, so a
/// shared placeholder is sufficient. Callers that need to assert on Id (or
/// on Id-derived behaviour) should construct <see cref="BgDecisionData"/>
/// explicitly rather than use this builder, since multiple builder-constructed
/// instances are otherwise indistinguishable by Id.
/// </summary>
internal static class BgDecisionDataBuilder
{
    /// <summary>
    /// Builds a <see cref="BgDecisionData"/> populated from the supplied
    /// arguments. The <see cref="BgDecisionData.Id"/> field is set to the
    /// shared placeholder described on <see cref="BgDecisionDataBuilder"/>;
    /// it is not parameterised.
    /// </summary>
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
            Id = new XgpDecisionId("test.xgp"),
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
