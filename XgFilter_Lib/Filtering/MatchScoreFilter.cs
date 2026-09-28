using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes decisions whose session as it stands at the decision
/// (<see cref="IDecisionFilterData.Session"/>) is a target in the include
/// list. Examples: "5a5a", "3a1aC", "moneyJ", "moneyNJ". The token grammar —
/// spellings, casing, and what makes a token valid — lives once on
/// <see cref="MatchScoreToken"/>; this filter parses through it and states no
/// rule of its own.
///
/// <para>
/// <b>Money and match are read from the session's kind</b>
/// (halheinrich/backgammon#273): a match's away scores and Crawford flag from
/// a <see cref="MatchSession"/>, a money session's Jacoby rule from a
/// <see cref="MoneySession"/>'s terms, each through the kind's exhaustive
/// <c>Match</c>. No match fact is read off a money session, no money rule off
/// a match, and no stand-in — a length of 0, away scores of 0 — is consulted
/// anywhere. The header gates read the header's terms and standing the same
/// way (<see cref="IMatchInfo.Terms"/>, <see cref="IGameInfo.Standing"/>).
/// </para>
///
/// <para>
/// Score tokens are <b>on-roll anchored</b>: <c>MaNa</c> means the player on
/// roll needs M points and the opponent needs N, so <c>"4a5a"</c> and
/// <c>"5a4a"</c> are distinct targets — include both orientations to admit a
/// score regardless of who is on roll. Only <see cref="Matches"/> sees on-roll
/// information; the header-level gates project the tuples exactly onto their
/// coarser inputs (a game header's standing is player1/player2-anchored and
/// both players roll within a game, so <see cref="ShouldSkipGame"/> admits
/// either orientation and leaves the per-decision verdict to
/// <see cref="Matches"/>).
/// </para>
///
/// <para>
/// <b>Money sessions and the Jacoby rule.</b> The two money tokens are
/// separate targets, each admitting money sessions under one rule:
/// <see cref="MatchScoreToken.MoneyWithJacoby"/> admits a money session whose
/// terms state the Jacoby rule (<see cref="MoneyTerms.IsJacoby"/>),
/// <see cref="MatchScoreToken.MoneyWithoutJacoby"/> one whose terms do not,
/// and listing both admits money under either rule. Every money session
/// states its rule, so there is no unknown rule to place. Match scores are
/// untouched by the money tokens, and the money tokens by any match score.
/// </para>
///
/// <para>
/// A match header states the session's terms, the Jacoby rule among them, so
/// <see cref="ShouldSkipMatch"/> judges a money session by its rule exactly as
/// <see cref="Matches"/> does — every decision's session carries its header's
/// terms. A game header states only the standing, which carries no rule, so
/// at game scope a money game is admissible iff <em>either</em> money token is
/// listed (see <see cref="IncludesAnyMoneyToken"/>): still the exact
/// projection onto the information a game header carries, the same shape as
/// the orientation projection above.
/// </para>
/// </summary>
internal sealed class MatchScoreFilter : IDecisionFilter, IMatchFilter
{
    private readonly List<(int Away1, int Away2, bool IsCrawford)> _tuples = [];
    private readonly bool _includesMoneyWithJacoby;
    private readonly bool _includesMoneyWithoutJacoby;

    /// <summary>
    /// Creates a filter passing decisions whose session is a target in
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
    /// Jacoby rule (a game header's standing states none) is entitled to ask.
    /// </summary>
    private bool IncludesAnyMoneyToken =>
        _includesMoneyWithJacoby || _includesMoneyWithoutJacoby;

    /// <summary>
    /// Whether a money session on <paramref name="terms"/> is a target: the
    /// token for its Jacoby rule is listed. The one statement of the money
    /// verdict, which <see cref="Matches"/> and <see cref="ShouldSkipMatch"/>
    /// both ask, so the two cannot drift apart on it.
    /// </summary>
    private bool AdmitsMoney(MoneyTerms terms) =>
        terms.IsJacoby ? _includesMoneyWithJacoby : _includesMoneyWithoutJacoby;

    /// <summary>
    /// Whether a match, as it stands from the player on roll's side, is a
    /// target tuple: both away scores in their orientation and the Crawford
    /// flag exact.
    /// </summary>
    private bool AdmitsMatch(MatchSession match) =>
        _tuples.Any(t =>
            t.Away1 == match.OnRollNeeds &&
            t.Away2 == match.OpponentNeeds &&
            t.IsCrawford == match.IsCrawford);

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data) =>
        data.Session.Match(money => AdmitsMoney(money.Terms), AdmitsMatch);

    /// <summary>
    /// Skip the match if:
    /// - money terms whose Jacoby rule's token is not listed, or
    /// - match terms, but no target tuple is a score any game of a match this
    ///   length can carry (see <see cref="CanOccurAtLength"/>) — which
    ///   includes a filter listing only money tokens.
    /// A match header states the terms but no orientation, and the length
    /// bound is orientation-free, so this projection is exact for either
    /// orientation.
    /// </summary>
    public bool ShouldSkipMatch(IMatchInfo match) => match.Terms.Match(
        money => !AdmitsMoney(money),
        terms => !_tuples.Any(t => CanOccurAtLength(t, terms.Length)));

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
    /// Skip the game when no target can match any of its decisions. A match
    /// standing is player1/player2-anchored while target tuples are on-roll
    /// anchored, and both players roll within a game — a game at
    /// (Away1, Away2) yields decisions scored (Away1, Away2) <i>and</i>
    /// (Away2, Away1). The exact projection onto game-level information is
    /// therefore: skip iff no tuple matches in either orientation, Crawford
    /// flag exact. <see cref="Matches"/> remains the per-decision arbiter
    /// of orientation. A money game is admissible iff either money token is
    /// listed — a money standing carries no Jacoby rule, so the rule verdict
    /// is <see cref="ShouldSkipMatch"/>'s and <see cref="Matches"/>'s.
    /// </summary>
    public bool ShouldSkipGame(IGameInfo game) => game.Standing.Match(
        _ => !IncludesAnyMoneyToken,
        aways => !_tuples.Any(t => MatchesGameScore(t, aways.Away1, aways.Away2, aways.IsCrawford)));

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
    /// Mid-stream: return true when no remaining decision in this match can
    /// match any target tuple, so the rest of the file can be skipped.
    /// "Remaining" includes the rest of the <i>current</i> game — the producer
    /// cuts the file immediately on a true vote — whose later decisions carry
    /// the current score in either orientation (both players roll). Strictly
    /// future games are covered by <see cref="IsReachable"/>, which exploits
    /// the monotonic decrease of away-scores game-to-game and the
    /// once-per-match Crawford rule. A money session always returns false:
    /// it has no away scores to run down, and its rule is the same in every
    /// game, which <see cref="ShouldSkipMatch"/> has already judged.
    /// </summary>
    public bool ShouldAdvanceMatch(IDecisionFilterData data) => data.Session.Match(
        static _ => false,
        match => !_tuples.Any(t =>
            MatchesGameScore(t, match.OnRollNeeds, match.OpponentNeeds, match.IsCrawford) ||
            IsReachable(t, match)));

    /// <summary>
    /// True if <paramref name="t"/> can match some strictly-future game
    /// reachable from <paramref name="current"/>. The current game itself is
    /// <see cref="ShouldAdvanceMatch"/>'s separate <see cref="MatchesGameScore"/>
    /// check. Tuples are constructor-validated (both sides &gt;= 1; Crawford
    /// implies exactly one side == 1), and a <see cref="MatchSession"/> holds
    /// its own away scores to the same floor, so neither is re-checked here.
    /// </summary>
    private static bool IsReachable(
        (int Away1, int Away2, bool IsCrawford) t,
        MatchSession current)
    {
        int ca = current.OnRollNeeds;
        int cb = current.OpponentNeeds;

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
