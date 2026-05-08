namespace XgFilter_Lib.Classification;

/// <summary>
/// Returns true when the opponent has two or more checkers on the bar — i.e.
/// the on-roll player is playing "vs 2+ up." Board index convention:
/// <c>board[0]</c> holds the opponent's bar with their checkers stored as
/// negatives, so the threshold is <c>board[0] &lt;= -2</c>. No race guard
/// needed: any position with checkers on the bar is contact by definition.
/// </summary>
internal sealed class VsTwoPlusUpClassifier : IPositionClassifier
{
    public bool Matches(IReadOnlyList<int> board) => board[0] <= -2;
}
