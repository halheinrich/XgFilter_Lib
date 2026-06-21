namespace XgFilter_Lib.Classification;

/// <summary>
/// Returns true when the on-roll player holds a "13-8-6 vs 20" holding
/// structure (the 20-point holding game) from the on-roll player's
/// perspective. The full predicate over the 26-element on-roll-relative
/// board is:
/// <list type="bullet">
///   <item><description>
///     <c>board[6]</c>, <c>board[8]</c>, <c>board[13]</c> &#8805; 2 — the
///     player has made the 6, 8, and 13 (midpoint) points.
///   </description></item>
///   <item><description>
///     <c>board[5]</c> &#8804; -2 — the opponent holds the 20-point anchor.
///     The 20 is the opponent's <em>own</em> 20 point, which from the
///     on-roll player's perspective is the player's 5 point (board index 5);
///     opponent checkers are stored negative.
///   </description></item>
///   <item><description>
///     <c>board[12]</c> &#8804; -2 — the opponent holds the player's 12 point.
///   </description></item>
///   <item><description>
///     <c>board[7]</c>, <c>board[9]</c>, <c>board[10]</c>, <c>board[11]</c>
///     == 0 — these points are empty.
///   </description></item>
///   <item><description>
///     <c>board[0..4]</c> &#8805; 0 — no opponent checkers in the player's
///     home (indices 1-4) or on the opponent's bar (index 0).
///   </description></item>
///   <item><description>
///     <c>board[14..25]</c> &#8804; 0 — no on-roll checkers above the 13,
///     including the player's bar (index 25): the player's structure is
///     entirely on or below the midpoint.
///   </description></item>
/// </list>
/// Board index convention: <c>board[0]</c> = opponent's bar (negative),
/// <c>board[1..24]</c> = points (positive = on-roll player, negative =
/// opponent), <c>board[25]</c> = player's bar (positive). No race guard is
/// needed: an opponent anchor on the 20 makes the position contact by
/// definition.
/// </summary>
internal sealed class Holding1386Vs20Classifier : IPositionClassifier
{
    // board[0]=opp bar (neg), board[1..24] points (pos=player, neg=opp), board[25]=player bar (pos)
    public bool Matches(IReadOnlyList<int> board)
    {
        // Ordered by selectivity: lead with the rarest signal — the opponent's
        // 20-point anchor — so the common non-holding board is rejected in a
        // single comparison before the made-point and range checks run.
        if (board[5] > -2) return false;    // opponent's 20-point anchor
        if (board[12] > -2) return false;   // opponent holds the player's 12

        if (board[6] < 2 || board[8] < 2 || board[13] < 2) return false;  // player holds the 6/8/13
        if (board[7] != 0 || board[9] != 0 || board[10] != 0 || board[11] != 0) return false;  // empty points

        // No opponent checkers on the bar or in the player's home (0-4).
        for (int i = 0; i <= 4; i++)
            if (board[i] < 0) return false;

        // No on-roll checkers above the 13 — including the player's bar (14-25).
        for (int i = 14; i <= 25; i++)
            if (board[i] > 0) return false;

        return true;
    }
}
