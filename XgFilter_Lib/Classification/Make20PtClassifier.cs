using BgDataTypes_Lib;

namespace XgFilter_Lib.Classification;

/// <summary>
/// Detects whether exactly one of the two plays — the best under the view's
/// ranking, and the player's — makes the decision-maker's 20-point (the
/// golden anchor), given that the 20-point is not already made before the
/// play.
///
/// A point is "made" when the decision-maker holds 2 or more checkers on
/// it. In priorBoard the decision-maker is on roll, so their 20-point is
/// index 20 and their checkers are positive: priorBoard[20] &gt;= 2. The
/// after-boards are in the next mover's frame, reached through the
/// producer's flip (<see cref="BoardPosition.Flipped"/>), so the
/// decision-maker's 20-point is index 5 there and their checkers are
/// negative: afterBoard[5] &lt;= -2. The XOR over {bestMakes, playerMakes} isolates decisions
/// where the 20-point-making choice differentiates best and player —
/// high-signal training material.
/// </summary>
internal sealed class Make20PtClassifier : IPlayTypeClassifier
{
    public bool Matches(
        BoardPosition priorBoard,
        BoardPosition afterBestBoard,
        BoardPosition afterPlayerBoard)
    {
        if (priorBoard[20] >= 2) return false;
        bool bestMakes = afterBestBoard[5] <= -2;
        bool playerMakes = afterPlayerBoard[5] <= -2;
        return bestMakes ^ playerMakes;
    }
}
