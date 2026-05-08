using BgDataTypes_Lib;
using ConvertXgToJson_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.MatchScore"/> matches any entry in the include list.
/// Examples: "5a5a", "3a1aC", "money".
/// Comparison is case-insensitive.
/// </summary>
public sealed class MatchScoreFilter : IDecisionFilter, IMatchFilter
{
    private readonly List<(int Away1, int Away2, bool IsCrawford)> _tuples;
    private readonly bool _includesMoney;

    public MatchScoreFilter(IEnumerable<string> scores)
    {
        var list = scores.ToList();
        _includesMoney = list.Any(s => s.Equals("money", StringComparison.OrdinalIgnoreCase));
        _tuples = list
            .Where(s => !s.Equals("money", StringComparison.OrdinalIgnoreCase))
            .Select(ParseScore)
            .ToList();
    }

    public bool Matches(IDecisionFilterData data)
    {
        if (data.MatchLength == 0)
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
    /// - all target away scores exceed the match length (e.g. 7a7a in a 5-point match)
    /// </summary>
    public bool ShouldSkipMatch(XgMatchInfo match)
    {
        bool isMoney = match.MatchLength == 0;

        if (isMoney && !_includesMoney) return true;
        if (!isMoney && _tuples.Count == 0) return true;

        if (!isMoney)
        {
            bool anyPossible = _tuples.Any(t =>
                t.Away1 <= match.MatchLength &&
                t.Away2 <= match.MatchLength);
            if (!anyPossible) return true;
        }

        return false;
    }

    /// <summary>
    /// Skip the game if its score doesn't match any target tuple.
    /// Score is fixed for the entire game so this skips all decisions in it.
    /// </summary>
    public bool ShouldSkipGame(XgGameInfo game)
    {
        bool isMoney = game.Away1 == 0 && game.Away2 == 0 && !game.IsCrawfordGame;

        if (isMoney) return !_includesMoney;

        return !_tuples.Any(t =>
            t.Away1 == game.Away1 &&
            t.Away2 == game.Away2 &&
            t.IsCrawford == game.IsCrawfordGame);
    }

    /// <summary>
    /// Mid-stream: return true when no future game in this match can reach
    /// any target tuple, so the rest of the file can be skipped. Money rows
    /// always return false (no "match" concept). Within a match, reachability
    /// exploits the fact that away-scores decrease monotonically game-to-game
    /// and Crawford happens at most once per match.
    /// </summary>
    public bool ShouldAdvanceMatch(IDecisionFilterData data)
    {
        if (data.MatchLength == 0) return false;
        return !_tuples.Any(t => IsReachable(t, data));
    }

    /// <summary>
    /// True if <paramref name="t"/> can match some future game reachable from
    /// <paramref name="current"/>. Excludes the current game itself (strict
    /// monotone decrease on at least one axis).
    /// </summary>
    private static bool IsReachable(
        (int Away1, int Away2, bool IsCrawford) t,
        IDecisionFilterData current)
    {
        if (t.Away1 < 1 || t.Away2 < 1) return false;

        int ca = current.OnRollNeeds;
        int cb = current.OpponentNeeds;
        if (ca < 1 || cb < 1) return false;

        int minT = Math.Min(t.Away1, t.Away2);
        int maxT = Math.Max(t.Away1, t.Away2);
        int maxC = Math.Max(ca, cb);
        int minC = Math.Min(ca, cb);

        // Current is in Crawford, or past it (one side at 1-away, non-Crawford flag).
        // No further Crawford possible; only continuing post-Crawford (1, m, false)
        // with m strictly below the non-1 side's current count.
        if (current.IsCrawford || minC == 1)
        {
            if (t.IsCrawford) return false;
            return minT == 1 && maxT < maxC;
        }

        // Pre-Crawford: ca > 1, cb > 1, non-Crawford.
        if (t.IsCrawford)
        {
            // Crawford is reached as (1, m, true) when whichever side drops to
            // 1-away first; m is the other side's count at that moment, so
            // m is bounded by the current count of whichever side stays.
            // Reachable iff one side of the tuple is 1 and the other is in
            // [2, max(ca, cb)].
            return minT == 1 && maxT >= 2 && maxT <= maxC;
        }

        // Non-Crawford tuple. Future state {p1, p2} is reachable iff
        // {t.Away1, t.Away2} fits as multiset under (ca, cb) with strict
        // decrement on at least one axis. Either player may be on-roll
        // in the future game, so check both orderings.
        bool fits1 = t.Away1 <= ca && t.Away2 <= cb;
        bool fits2 = t.Away2 <= ca && t.Away1 <= cb;
        bool strictSum = t.Away1 + t.Away2 < ca + cb;
        return (fits1 || fits2) && strictSum;
    }

    /// <summary>
    /// Parses a score token like "3a5a" or "1a5aC" into its tuple form.
    /// Throws <see cref="ArgumentException"/> on any malformed input —
    /// silent drop would let UI typos slip through as "filter does
    /// nothing," which is the kind of failure that bites at runtime
    /// without a clear cause.
    /// </summary>
    private static (int Away1, int Away2, bool IsCrawford) ParseScore(string s)
    {
        bool isCrawford = s.EndsWith("C", StringComparison.OrdinalIgnoreCase);
        var clean = isCrawford ? s[..^1] : s;
        var parts = clean.Split('a', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out int a1)
            && int.TryParse(parts[1], out int a2))
            return (a1, a2, isCrawford);
        throw new ArgumentException(
            $"Invalid match score: '{s}'. Expected format like '3a5a', '1a5aC', or 'money'.",
            nameof(s));
    }
}