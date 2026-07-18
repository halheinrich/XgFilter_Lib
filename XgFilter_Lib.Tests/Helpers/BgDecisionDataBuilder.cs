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
        AnalysisMode mode = AnalysisMode.Unknown,
        AnalysisLevel level = AnalysisLevel.Unknown,
        int roll = 31,
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
            // BgDecisionData.AnalysisMode / AnalysisLevel derive from the cube
            // analysis for cube rows and from the best-play candidate for checker
            // rows (see BgDecisionData). Stamp both surfaces so the derived pair
            // equals (mode, level) on either row kind; the unused surface is
            // ignored by the getters.
            Decision = new DecisionData
            {
                IsCube = isCube,
                // Two producer-stamped faces, rolled order; BgDecisionData.Dice
                // canonicalizes them. A cube row's Dice getter returns null
                // regardless, so this is ignored there. Default 31 mirrors
                // DecisionRowBuilder's default roll, keeping the two substrates'
                // Dice in step for the shared RowShape default.
                Dice = [roll / 10, roll % 10],
                UserPlayError = userPlayError,
                UserDoubleError = userDoubleError,
                UserTakeError = userTakeError,
                CubeAnalysisMode = mode,
                CubeAnalysisLevel = level,
                Plays = [new PlayCandidate { AnalysisMode = mode, AnalysisLevel = level }],
                BestPlayIndex = 0,
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
