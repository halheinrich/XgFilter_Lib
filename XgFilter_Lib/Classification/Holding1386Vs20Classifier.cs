namespace XgFilter_Lib.Classification;

/// <summary>
/// Returns true when the on-roll player holds a "13-8-6 vs 20" holding
/// structure: the player has made the 13, 8, and 6 points (≥ 2 checkers
/// each) while the opponent anchors on the player's 20 point (≥ 2 of the
/// opponent's checkers there). Board index convention:
/// <c>board[0]</c> = opponent's bar (negative), <c>board[1..24]</c> = points
/// (positive = on-roll player, negative = opponent), <c>board[25]</c> =
/// player's bar (positive). No race guard is needed: an opponent anchor on
/// the 20 makes the position contact by definition.
/// </summary>
internal sealed class Holding1386Vs20Classifier : IPositionClassifier
{
    // board[0]=opp bar (neg), board[1..24] points (pos=player, neg=opp), board[25]=player bar (pos)
    public bool Matches(IReadOnlyList<int> board) =>
        board[6] >= 2 && board[8] >= 2 && board[13] >= 2 && board[20] <= -2;
}
