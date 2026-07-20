using System.Text.RegularExpressions;
using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.MatchScore"/> matches any entry in the include list.
/// Examples: "5a5a", "3a1aC", "money".
/// Comparison is case-insensitive: the <c>a</c> away-separators, the
/// trailing <c>C</c> Crawford suffix, and the <c>money</c> token all match
/// in either case (see <see cref="ParseScore"/> for the score-token grammar).
///
/// <para>
/// Score tokens are <b>on-roll anchored</b>: <c>MaNa</c> means the player on
/// roll needs M points and the opponent needs N, so <c>"4a5a"</c> and
/// <c>"5a4a"</c> are distinct targets — include both orientations to admit a
/// score regardless of who is on roll. Only <see cref="Matches"/> sees on-roll
/// information; the header-level gates project the tuples exactly onto their
/// coarser inputs (a game header is player1/player2-anchored and both players
/// roll within a game, so <see cref="ShouldSkipGame"/> admits either
/// orientation and leaves the per-decision verdict to <see cref="Matches"/>).
/// </para>
/// </summary>
internal sealed partial class MatchScoreFilter : IDecisionFilter, IMatchFilter
{
    private readonly List<(int Away1, int Away2, bool IsCrawford)> _tuples;
    private readonly bool _includesMoney;

    /// <summary>
    /// Creates a filter passing rows whose match score appears in
    /// <paramref name="scores"/>. Tokens are like <c>"3a5a"</c>,
    /// <c>"1a5aC"</c>, or <c>"money"</c>; comparison is case-insensitive.
    /// <c>MaNa</c> is on-roll anchored — M is what the player on roll needs,
    /// N what the opponent needs — so <c>"4a5a"</c> and <c>"5a4a"</c> are
    /// distinct entries. Throws <see cref="ArgumentException"/> on any
    /// malformed token, on away scores below 1, and on Crawford tokens no
    /// real game can carry (see <see cref="ParseScore"/>).
    /// </summary>
    public MatchScoreFilter(IEnumerable<string> scores)
    {
        var list = scores.ToList();
        _includesMoney = list.Any(s => s.Equals("money", StringComparison.OrdinalIgnoreCase));
        _tuples = list
            .Where(s => !s.Equals("money", StringComparison.OrdinalIgnoreCase))
            .Select(ParseScore)
            .ToList();
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        if (data.IsMoneyGame)
            return _includesMoney;

        return _tuples.Any(t =>
            t.Away1 == data.OnRollNeeds &&
            t.Away2 == data.OpponentNeeds &&
            t.IsCrawford == data.IsCrawford);
    }

    /// <summary>
    /// Skip the match if:
    /// - money session but filter contains no money target, or
    /// - match session but filter contains only money, or
    /// - no target tuple is a score any game of a match this length can
    ///   carry (see <see cref="CanOccurAtLength"/>).
    /// Match headers carry no orientation, and the length bound is
    /// orientation-free, so this projection is exact for either orientation.
    /// </summary>
    public bool ShouldSkipMatch(IMatchInfo match)
    {
        bool isMoney = match.IsMoneyGame;

        if (isMoney && !_includesMoney) return true;
        if (!isMoney && _tuples.Count == 0) return true;

        if (!isMoney)
        {
            bool anyPossible = _tuples.Any(t => CanOccurAtLength(t, match.MatchLength));
            if (!anyPossible) return true;
        }

        return false;
    }

    /// <summary>
    /// True when <paramref name="t"/> is a score some game of a match of
    /// <paramref name="matchLength"/> points can carry, in either orientation.
    /// </summary>
    private static bool CanOccurAtLength(
        (int Away1, int Away2, bool IsCrawford) t, int matchLength)
    {
        int minT = Math.Min(t.Away1, t.Away2);
        int maxT = Math.Max(t.Away1, t.Away2);

        // Crawford (1, k, true): k is the trailer's count when the leader
        // first reaches 1-away, so 2 <= k <= L. The constructor already
        // guarantees the tuple's shape (minT == 1, maxT >= 2).
        if (t.IsCrawford)
            return maxT <= matchLength;

        // Post-Crawford (1, m, false) exists only after a Crawford game
        // (1, k, true) where the trailer won at least one point, so
        // m < k <= L. The one exception is 1a1a in a 1-point match: the
        // 1-pointer's only game is (1, 1, false) with no Crawford game
        // before it (1a1a is never Crawford — the substrate rule settled
        // in BgGame_Lib: a (1,1) game is cubeless).
        if (minT == 1)
            return maxT == 1 || maxT <= matchLength - 1;

        return maxT <= matchLength;
    }

    /// <summary>
    /// Skip the game when no target tuple can match any of its decisions.
    /// Game headers are player1/player2-anchored while target tuples are
    /// on-roll anchored, and both players roll within a game — a game at
    /// (Away1, Away2) yields decisions scored (Away1, Away2) <i>and</i>
    /// (Away2, Away1). The exact projection onto game-level information is
    /// therefore: skip iff no tuple matches in either orientation, Crawford
    /// flag exact. <see cref="Matches"/> remains the per-decision arbiter
    /// of orientation.
    /// </summary>
    public bool ShouldSkipGame(IGameInfo game)
    {
        bool isMoney = game.Away1 == 0 && game.Away2 == 0 && !game.IsCrawfordGame;

        if (isMoney) return !_includesMoney;

        return !_tuples.Any(t =>
            MatchesGameScore(t, game.Away1, game.Away2, game.IsCrawfordGame));
    }

    /// <summary>
    /// True when <paramref name="t"/> equals the game score
    /// {<paramref name="away1"/>, <paramref name="away2"/>} in either
    /// orientation with an exactly matching Crawford flag — i.e. some
    /// decision of a game at that score can satisfy <see cref="Matches"/>.
    /// </summary>
    private static bool MatchesGameScore(
        (int Away1, int Away2, bool IsCrawford) t,
        int away1, int away2, bool isCrawford) =>
        t.IsCrawford == isCrawford &&
        ((t.Away1 == away1 && t.Away2 == away2) ||
         (t.Away1 == away2 && t.Away2 == away1));

    /// <summary>
    /// Mid-stream: return true when no remaining row in this match can match
    /// any target tuple, so the rest of the file can be skipped. "Remaining"
    /// includes the rest of the <i>current</i> game — the producer cuts the
    /// file immediately on a true vote — whose later decisions carry the
    /// current score in either orientation (both players roll). Strictly
    /// future games are covered by <see cref="IsReachable"/>, which exploits
    /// the monotonic decrease of away-scores game-to-game and the
    /// once-per-match Crawford rule. Money rows always return false (no
    /// "match" concept).
    /// </summary>
    public bool ShouldAdvanceMatch(IDecisionFilterData data)
    {
        if (data.IsMoneyGame) return false;
        return !_tuples.Any(t =>
            MatchesGameScore(t, data.OnRollNeeds, data.OpponentNeeds, data.IsCrawford) ||
            IsReachable(t, data));
    }

    /// <summary>
    /// True if <paramref name="t"/> can match some strictly-future game
    /// reachable from <paramref name="current"/>. The current game itself is
    /// <see cref="ShouldAdvanceMatch"/>'s separate <see cref="MatchesGameScore"/>
    /// check. Tuples are constructor-validated (both sides &gt;= 1; Crawford
    /// implies exactly one side == 1), so tuple validity is not re-checked here.
    /// </summary>
    private static bool IsReachable(
        (int Away1, int Away2, bool IsCrawford) t,
        IDecisionFilterData current)
    {
        int ca = current.OnRollNeeds;
        int cb = current.OpponentNeeds;
        if (ca < 1 || cb < 1) return false;

        int minT = Math.Min(t.Away1, t.Away2);
        int maxT = Math.Max(t.Away1, t.Away2);
        int maxC = Math.Max(ca, cb);
        int minC = Math.Min(ca, cb);

        // Current game is Crawford, or past it (one side at 1-away,
        // non-Crawford flag). No further Crawford game is possible; the only
        // future games are post-Crawford (1, m, false) with m strictly below
        // the non-1 side's current count (the leader stays at 1-away — any
        // game they win ends the match).
        if (current.IsCrawford || minC == 1)
            return !t.IsCrawford && minT == 1 && maxT < maxC;

        // Pre-Crawford from here: ca >= 2, cb >= 2, non-Crawford.

        // Crawford is reached as (1, k, true) when whichever side drops to
        // 1-away first; k is the staying side's count at that moment, so k
        // is bounded by the current count of whichever side stays:
        // 2 <= k <= max(ca, cb). The constructor guarantees the tuple's
        // shape (minT == 1, maxT >= 2), so only the upper bound is tested.
        if (t.IsCrawford)
            return maxT <= maxC;

        // Post-Crawford (1, m, false) exists only after a Crawford game
        // (1, k, true) with m < k, and k <= max(ca, cb) by the bound above —
        // so m stays strictly below max(ca, cb). Conversely every m in
        // [1, max(ca, cb) - 1] is reachable, (1, 1) included.
        if (minT == 1)
            return maxT < maxC;

        // Pre-Crawford tuple (both sides >= 2). A future game {p1, p2} is
        // reachable iff {t.Away1, t.Away2} fits as a multiset under (ca, cb)
        // with a strictly smaller sum — every game transfers at least one
        // point, so the same multiset never recurs, and a fitting target
        // with both sides >= 2 is reached by single-point wins that never
        // trigger Crawford. Either player may be on roll in the future game,
        // so check both orderings.
        bool fits1 = t.Away1 <= ca && t.Away2 <= cb;
        bool fits2 = t.Away2 <= ca && t.Away1 <= cb;
        return (fits1 || fits2) && t.Away1 + t.Away2 < ca + cb;
    }

    /// <summary>
    /// Parses a score token like "3a5a" or "1a5aC" into its tuple form and
    /// validates it against scores a real match game can carry: both sides
    /// at least 1-away, and a Crawford token with exactly one side 1-away
    /// and the other 2-away or more ("1a1aC" is rejected — a (1,1) game is
    /// always post-Crawford; "3a5aC" is rejected — Crawford requires a side
    /// at 1-away).
    ///
    /// <para>
    /// The accepted format is the case-insensitive grammar
    /// <c>^(\d+)[aA](\d+)[aA]([cC])?$</c> — an away count, the <c>a</c>
    /// separator, a second away count and its <c>a</c>, and an optional
    /// trailing <c>C</c> Crawford flag. The token is trimmed first so
    /// incidental surrounding whitespace from non-UI callers (CLI args,
    /// hand-built configs) is tolerated; embedded whitespace and any extra
    /// or repeated separators are rejected. A sign, decimal point, or any
    /// non-digit in an away field is a format error.
    /// </para>
    ///
    /// <para>
    /// Throws <see cref="ArgumentException"/> on any malformed or impossible
    /// input — silent drop would let UI typos slip through as "filter does
    /// nothing," which is the kind of failure that bites at runtime without
    /// a clear cause.
    /// </para>
    /// </summary>
    private static (int Away1, int Away2, bool IsCrawford) ParseScore(string s)
    {
        var match = ScoreTokenRegex().Match(s.Trim());
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out int a1)
            || !int.TryParse(match.Groups[2].Value, out int a2))
            throw new ArgumentException(
                $"Invalid match score: '{s}'. Expected format like '3a5a', '1a5aC', or 'money'.",
                nameof(s));

        bool isCrawford = match.Groups[3].Success;

        if (a1 < 1 || a2 < 1)
            throw new ArgumentException(
                $"Invalid match score: '{s}'. Away scores must be at least 1 — " +
                "a player needing 0 or fewer points has already won the match.",
                nameof(s));

        if (isCrawford && (Math.Min(a1, a2) != 1 || Math.Max(a1, a2) < 2))
            throw new ArgumentException(
                $"Invalid match score: '{s}'. A Crawford game has exactly one side " +
                "1-away and the other 2-away or more; a (1,1) game is always post-Crawford.",
                nameof(s));

        return (a1, a2, isCrawford);
    }

    /// <summary>
    /// The case-insensitive score-token grammar: an away count, the <c>a</c>
    /// separator, a second away count and its <c>a</c>, and an optional
    /// trailing <c>C</c> Crawford flag. Anchored, so it rejects any leading,
    /// trailing, embedded, or repeated slop the previous
    /// <c>Split('a', RemoveEmptyEntries)</c> silently swallowed.
    /// </summary>
    [GeneratedRegex(@"^(\d+)[aA](\d+)[aA]([cC])?$")]
    private static partial Regex ScoreTokenRegex();
}
