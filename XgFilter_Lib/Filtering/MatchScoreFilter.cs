using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.MatchScore"/> matches any entry in
/// the include list. Examples: "5a5a", "3a1aC", "moneyJ", "moneyNJ". The token
/// grammar — spellings, casing, and what makes a token valid — lives once on
/// <see cref="MatchScoreToken"/>; this filter parses through it and states no
/// rule of its own.
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
///
/// <para>
/// <b>Money sessions and the Jacoby rule.</b> The two money tokens are
/// separate targets, each admitting money records under one rule:
/// <see cref="MatchScoreToken.MoneyWithJacoby"/> admits
/// <c>IsMoneyGame &amp;&amp; IsJacoby == true</c>,
/// <see cref="MatchScoreToken.MoneyWithoutJacoby"/> admits
/// <c>IsMoneyGame &amp;&amp; IsJacoby == false</c>, and listing both admits
/// money under either rule. A money record whose Jacoby fact is unknown
/// (<see cref="IDecisionFilterData.IsJacoby"/> null) matches <b>neither</b> —
/// an unknown rule is never guessed into a side. Match scores are untouched by
/// the money tokens, and the money tokens are untouched by any match score.
/// </para>
///
/// <para>
/// The header-level gates cannot see the Jacoby fact —
/// <see cref="IMatchInfo"/> and <see cref="IGameInfo"/> carry no such member,
/// by their stated "members are added on demand" minimalism — so at header
/// scope a money session is admissible iff <em>either</em> money token is
/// listed (see <see cref="IncludesAnyMoneyToken"/>). That is still the exact
/// projection onto the information those headers carry: a header cannot
/// distinguish the two rules, both rules occur under it, and
/// <see cref="Matches"/> remains the per-decision arbiter — the same shape as
/// the orientation projection above.
/// </para>
/// </summary>
internal sealed class MatchScoreFilter : IDecisionFilter, IMatchFilter
{
    private readonly List<(int Away1, int Away2, bool IsCrawford)> _tuples = [];
    private readonly bool _includesMoneyWithJacoby;
    private readonly bool _includesMoneyWithoutJacoby;

    /// <summary>
    /// Creates a filter passing rows whose match score appears in
    /// <paramref name="scores"/>. Tokens are like <c>"3a5a"</c>,
    /// <c>"1a5aC"</c>, <c>"moneyJ"</c>, or <c>"moneyNJ"</c>; the grammar
    /// (including its case and whitespace rules) is
    /// <see cref="MatchScoreToken"/>'s. <c>MaNa</c> is on-roll anchored — M is
    /// what the player on roll needs, N what the opponent needs — so
    /// <c>"4a5a"</c> and <c>"5a4a"</c> are distinct entries.
    /// </summary>
    /// <param name="scores">The include list of score tokens.</param>
    /// <exception cref="ArgumentException">
    /// Any entry is a token <see cref="MatchScoreToken.GetFault"/> would
    /// fault — malformed, an impossible score, or the retired
    /// <see cref="MatchScoreToken.RetiredMoney"/> token. Rejecting rather than
    /// dropping is what makes <see cref="FilterConfig.Build"/> the point that
    /// refuses a configuration nobody could have meant; a consumer that wants
    /// to ask before building asks
    /// <see cref="FilterConfig.GetInvalidFields"/>.
    /// </exception>
    public MatchScoreFilter(IEnumerable<string> scores)
    {
        foreach (string token in scores)
        {
            // Dispatch order matches MatchScoreToken.GetFault's: the money
            // tokens are recognized first (they are not scores), and
            // everything else — the retired bare money token included — goes
            // to ParseScore, which is the throw.
            if (MatchScoreToken.IsMoneyWithJacobyToken(token))
                _includesMoneyWithJacoby = true;
            else if (MatchScoreToken.IsMoneyWithoutJacobyToken(token))
                _includesMoneyWithoutJacoby = true;
            else
                _tuples.Add(MatchScoreToken.ParseScore(token));
        }
    }

    /// <summary>
    /// Whether either money token is listed — what a gate that cannot see the
    /// Jacoby fact is entitled to ask. Stated once here so the two header
    /// gates cannot drift apart on it.
    /// </summary>
    private bool IncludesAnyMoneyToken =>
        _includesMoneyWithJacoby || _includesMoneyWithoutJacoby;

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        // The ruled conjunctions, spelled as IDecisionFilterData.IsJacoby
        // states them. `== true` / `== false` are load-bearing against the
        // tri-state: the near-miss spellings `!= false` / `!= true` would
        // admit an unknown-rule money record into one side, and an unknown
        // rule is never guessed.
        if (data.IsMoneyGame)
            return (_includesMoneyWithJacoby && data.IsJacoby == true)
                || (_includesMoneyWithoutJacoby && data.IsJacoby == false);

        return _tuples.Any(t =>
            t.Away1 == data.OnRollNeeds &&
            t.Away2 == data.OpponentNeeds &&
            t.IsCrawford == data.IsCrawford);
    }

    /// <summary>
    /// Skip the match if:
    /// - money session but filter lists neither money token, or
    /// - match session but filter lists only money tokens, or
    /// - no target tuple is a score any game of a match this length can
    ///   carry (see <see cref="CanOccurAtLength"/>).
    /// Match headers carry neither orientation nor the Jacoby fact, and the
    /// length bound is orientation-free, so this projection is exact for
    /// either orientation and either rule.
    /// </summary>
    public bool ShouldSkipMatch(IMatchInfo match)
    {
        bool isMoney = match.IsMoneyGame;

        if (isMoney && !IncludesAnyMoneyToken) return true;
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
    /// of orientation. A money game is admissible iff either money token is
    /// listed — the header carries no Jacoby fact, so the rule verdict is
    /// likewise <see cref="Matches"/>'s.
    /// </summary>
    public bool ShouldSkipGame(IGameInfo game)
    {
        bool isMoney = game.Away1 == 0 && game.Away2 == 0 && !game.IsCrawfordGame;

        if (isMoney) return !IncludesAnyMoneyToken;

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
}
