using BgDataTypes_Lib;

namespace XgFilter_Lib.Classification;

/// <summary>
/// Classifies a backgammon play from three positions, each in the frame the
/// producer states on the member it comes from:
///   - priorBoard       — <see cref="IDecisionFilterData.Board"/>, the
///     decision-maker on roll.
///   - afterBestBoard   — <see cref="IDecisionFilterData.AfterBestBoard"/>,
///     the position the best play under the view's ranking reaches, in the
///     next mover's frame.
///   - afterPlayerBoard — <see cref="IDecisionFilterData.AfterPlayerBoard"/>,
///     the position the player's own play reaches, in the same frame.
///
/// So a point of the decision-maker's in priorBoard is read in the after-boards
/// through the producer's one flip rule (<see cref="BoardPosition.Flipped"/>),
/// never a rule of this library's.
///
/// A classifier is only asked about a play whose after-boards both exist: the
/// caller never substitutes one board for another, so a player's play off the
/// candidate list (no after-board) is decided before a classifier is consulted.
/// </summary>
internal interface IPlayTypeClassifier
{
    bool Matches(
        BoardPosition priorBoard,
        BoardPosition afterBestBoard,
        BoardPosition afterPlayerBoard);
}
