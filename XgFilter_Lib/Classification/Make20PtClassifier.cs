namespace XgFilter_Lib.Classification;

/// <summary>
/// Detects whether exactly one of the two candidate plays (best / player)
/// makes the decision-maker's 20-point (the golden anchor), given that
/// the 20-point is not already made before the play.
///
/// A point is "made" when the decision-maker holds 2 or more checkers on
/// it. In priorBoard the decision-maker is on roll, so their 20-point is
/// index 20 and their checkers are positive: priorBoard[20] &gt;= 2. In
/// the after-boards the turn has flipped — the decision-maker's 20-point
/// is index 5 and their checkers are stored negatively: afterBoard[5]
/// &lt;= -2. The XOR over {bestMakes, playerMakes} isolates decisions
/// where the 20-point-making choice differentiates best and player —
/// high-signal training material.
/// </summary>
public sealed class Make20PtClassifier : IPlayTypeClassifier
{
    public bool Matches(
        IReadOnlyList<int> priorBoard,
        IReadOnlyList<int> afterBestBoard,
        IReadOnlyList<int> afterPlayerBoard)
    {
        if (priorBoard[20] >= 2) return false;
        bool bestMakes = afterBestBoard[5] <= -2;
        bool playerMakes = afterPlayerBoard[5] <= -2;
        return bestMakes ^ playerMakes;
    }
}
