namespace XgFilter_Lib.Classification;

/// <summary>
/// Classifies a backgammon play from three board arrays (prior to play,
/// after the best play, after the user's play), each normalized to the
/// player on roll.
/// board[0]    = opponent's bar (never positive)
/// board[1-24] = points 1-24 from player on roll's perspective
/// board[25]   = player on roll's bar (never negative)
/// Positive values = player on roll's checkers; negative = opponent's.
/// </summary>
public interface IPlayTypeClassifier
{
    bool Matches(
        IReadOnlyList<int> priorBoard,
        IReadOnlyList<int> afterBestBoard,
        IReadOnlyList<int> afterUserBoard);
}
