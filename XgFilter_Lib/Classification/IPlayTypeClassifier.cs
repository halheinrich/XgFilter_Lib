namespace XgFilter_Lib.Classification;

/// <summary>
/// Classifies a backgammon play from three board arrays, each normalized
/// to the player who is on roll at that moment:
///   - priorBoard       — decision-maker on roll (before any play).
///   - afterBestBoard   — opponent on roll (turn has flipped after the best play).
///   - afterPlayerBoard — opponent on roll (turn has flipped after the player's actual play).
///
/// Board index convention (each board):
///   board[0]    = opponent's bar (never positive)
///   board[1-24] = points 1-24 from on-roll player's perspective
///   board[25]   = on-roll player's bar (never negative)
///   Positive values = on-roll player's checkers; negative = opponent's.
///
/// Perspective consequence: what was the decision-maker's point X in
/// priorBoard is point (25 - X) in the after-boards, and the decision-
/// maker's checkers there are stored as negative values.
/// </summary>
public interface IPlayTypeClassifier
{
    bool Matches(
        IReadOnlyList<int> priorBoard,
        IReadOnlyList<int> afterBestBoard,
        IReadOnlyList<int> afterPlayerBoard);
}
