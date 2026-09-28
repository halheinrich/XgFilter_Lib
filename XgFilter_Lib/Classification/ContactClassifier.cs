using BgDataTypes_Lib;

namespace XgFilter_Lib.Classification;

/// <summary>
/// Returns true when the position is NOT a pure race — at least one player
/// still has checkers on or behind opposing checkers.
/// </summary>
internal sealed class ContactClassifier : IPositionClassifier
{
    private static readonly RaceClassifier _race = new();

    public bool Matches(BoardPosition board) => !_race.Matches(board);
}
