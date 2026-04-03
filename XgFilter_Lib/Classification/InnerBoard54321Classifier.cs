using XgFilter_Lib.Classification;

/// <summary>
/// Returns true when the player on roll holds an inner-board 6-3-1 structure:
///   - Points 6, 3, and 1 each have ≥ 2 checkers (player on roll)
///   - Points 5, 4, and 2 each have fewer than 2 checkers (player on roll)
///   - Position must be contact (not a race)
/// Board index convention: board[1]–board[24] = points 1–24 from player on roll's perspective.
/// Positive values = player on roll's checkers.
/// </summary>
public sealed class InnerBoard54321Classifier : IPositionClassifier
{
    private static readonly RaceClassifier _race = new();

    public bool Matches(int[] board)
    {
        if (_race.Matches(board)) return false;

        return board[6] < 2
            && board[5] >= 2
            && board[4] >= 2
            && board[3] >= 2
            && board[2] >= 2
            && board[1] >= 2;
    }
}