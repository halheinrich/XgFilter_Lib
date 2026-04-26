using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Single source of truth for a filter-test input. Projects to both
/// <see cref="DecisionRow"/> (CSV-shaped) and <see cref="BgDecisionData"/>
/// (diagram-shaped) via <see cref="ToDecisionRow"/> / <see cref="ToBgDecisionData"/>,
/// so every filter assertion can exercise both substrates without the two
/// projections ever drifting.
///
/// Only fields common to both substrates are exposed. Substrate-specific
/// tests (e.g. <see cref="BgDecisionData"/>'s cube-error fallback) cannot
/// express themselves through <see cref="RowShape"/> and belong in their
/// owning substrate's own test suite.
/// </summary>
internal sealed record RowShape(
    string Player = "Player1",
    bool IsCube = false,
    double? Error = 0.0,
    int OnRollNeeds = 3,
    int OpponentNeeds = 5,
    bool IsCrawford = false,
    int MatchLength = 7,
    int MoveNumber = 1,
    bool IsStandardStart = true,
    int[]? Board = null,
    int[]? AfterBestBoard = null,
    int[]? AfterPlayerBoard = null)
{
    public DecisionRow ToDecisionRow() => IsCube
        ? DecisionRowBuilder.BuildCube(
            player: Player,
            error: Error ?? 0.0,
            onRollNeeds: OnRollNeeds,
            opponentNeeds: OpponentNeeds,
            isCrawford: IsCrawford,
            matchLength: MatchLength,
            moveNumber: MoveNumber,
            isStandardStart: IsStandardStart)
        : DecisionRowBuilder.Build(
            player: Player,
            error: Error ?? 0.0,
            onRollNeeds: OnRollNeeds,
            opponentNeeds: OpponentNeeds,
            isCrawford: IsCrawford,
            matchLength: MatchLength,
            moveNumber: MoveNumber,
            isStandardStart: IsStandardStart,
            board: Board,
            afterBestBoard: AfterBestBoard,
            afterPlayerBoard: AfterPlayerBoard);

    public BgDecisionData ToBgDecisionData() => BgDecisionDataBuilder.Build(
        player: Player,
        isCube: IsCube,
        onRollNeeds: OnRollNeeds,
        opponentNeeds: OpponentNeeds,
        isCrawford: IsCrawford,
        matchLength: MatchLength,
        moveNumber: MoveNumber,
        isStandardStart: IsStandardStart,
        userPlayError: Error,
        board: Board,
        afterBestBoard: AfterBestBoard,
        afterPlayerBoard: AfterPlayerBoard);
}
