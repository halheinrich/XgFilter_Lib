namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Shared helper for assembling the 26-element on-roll-relative board
/// array used throughout classifier and filter tests. Indices follow
/// the canonical layout: <c>0</c> opponent's bar, <c>1..24</c> points,
/// <c>25</c> on-roll player's bar; positive values are the on-roll
/// player's checkers, negative are the opponent's.
/// </summary>
internal static class BoardBuilder
{
    /// <summary>
    /// Returns a 26-element board with the given <paramref name="points"/>
    /// applied; all other indices are zero.
    /// </summary>
    public static int[] Build(params (int index, int count)[] points)
    {
        var board = new int[26];
        foreach (var (index, count) in points)
            board[index] = count;
        return board;
    }
}
