namespace XgFilter_Lib.Classification;

/// <summary>
/// Returns true when the position is NOT a pure race — at least one player
/// still has checkers on or behind opposing checkers.
/// </summary>
public sealed class ContactClassifier : IPositionClassifier
{
    private static readonly RaceClassifier _race = new();

    public bool Matches(IReadOnlyList<int> board) => !_race.Matches(board);
}