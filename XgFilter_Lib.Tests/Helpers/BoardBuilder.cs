using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Shared helper for assembling a sparse <see cref="BoardPosition"/> in the
/// player on roll's frame, used throughout classifier and filter tests:
/// <c>0</c> the opponent's bar, <c>1..24</c> the points, <c>25</c> the on-roll
/// player's bar; positive counts are the on-roll player's checkers, negative
/// the opponent's. The position's own constructor judges the counts, so a
/// board no position can hold is refused here as everywhere.
/// </summary>
internal static class BoardBuilder
{
    /// <summary>
    /// Returns the position with the given <paramref name="points"/> applied;
    /// every other slot is empty.
    /// </summary>
    public static BoardPosition Build(params (int index, int count)[] points)
    {
        Span<int> counts = stackalloc int[26];
        foreach (var (index, count) in points)
            counts[index] = count;
        return new BoardPosition(counts);
    }
}
