namespace XgFilter_Lib.Classification;

/// <summary>
/// Detects whether exactly one of the two candidate plays (best / user)
/// makes the on-roll player's 20-point (the golden anchor), given that
/// the 20-point is not already made before the play.
///
/// A point is "made" when it holds 2 or more friendly checkers
/// (board[20] &gt;= 2). The XOR over {bestMakes, userMakes} isolates
/// decisions where the 20-point-making choice is the differentiator
/// between best and user — high-signal training material.
/// </summary>
public sealed class Make20PtClassifier : IPlayTypeClassifier
{
    public bool Matches(
        IReadOnlyList<int> priorBoard,
        IReadOnlyList<int> afterBestBoard,
        IReadOnlyList<int> afterUserBoard)
    {
        if (priorBoard[20] >= 2) return false;
        bool bestMakes = afterBestBoard[20] >= 2;
        bool userMakes = afterUserBoard[20] >= 2;
        return bestMakes ^ userMakes;
    }
}
