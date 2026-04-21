namespace XgFilter_Lib.Classification;

/// <summary>
/// Classifies a backgammon position from the board array normalized to the player on roll.
/// board[0]    = opponent's bar (never positive)
/// board[1-24] = points 1-24 from player on roll's perspective
/// board[25]   = player on roll's bar (never negative)
/// Positive values = player on roll's checkers; negative = opponent's.
/// </summary>
internal interface IPositionClassifier
{
    bool Matches(IReadOnlyList<int> board);
}