using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.MatchScore"/> matches any entry in the include list.
/// Examples: "5a5a", "3a1aC", "money".
/// Comparison is case-insensitive.
/// </summary>
public sealed class MatchScoreFilter : IDecisionFilter, IMatchFilter
{
    private readonly HashSet<string> _scores;
    private readonly List<(int Away1, int Away2, bool IsCrawford)> _tuples;
    private readonly bool _includesMoney;

    public MatchScoreFilter(IEnumerable<string> scores)
    {
        var list = scores.ToList();
        _scores = new HashSet<string>(list, StringComparer.OrdinalIgnoreCase);
        _includesMoney = _scores.Contains("money");
        _tuples = list
            .Where(s => !s.Equals("money", StringComparison.OrdinalIgnoreCase))
            .Select(ParseScore)
            .Where(t => t.HasValue)
            .Select(t => t!.Value)
            .ToList();
    }

    public bool Matches(DecisionRow row) =>
        _scores.Contains(row.MatchScore);

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

    private static (int Away1, int Away2, bool IsCrawford)? ParseScore(string s)
    {
        bool isCrawford = s.EndsWith("C", StringComparison.OrdinalIgnoreCase);
        var clean = isCrawford ? s[..^1] : s;
        var parts = clean.Split('a', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out int a1)
            && int.TryParse(parts[1], out int a2))
            return (a1, a2, isCrawford);
        return null;
    }
}