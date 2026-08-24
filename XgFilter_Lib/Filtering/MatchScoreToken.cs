using System.Text.RegularExpressions;
using XgFilter_Lib.Enums;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The score-token grammar: one statement, for the whole library, of what a
/// <see cref="FilterConfig.MatchScores"/> entry may say and how it is spelled.
/// <see cref="MatchScoreFilter"/> parses through it, the
/// <see cref="FilterConfig.GetInvalidFields"/> rule table judges through it,
/// and a consumer asks it directly to mark the token the user got wrong — so
/// the answer each surface gives is the same answer asked once.
///
/// <para>
/// <b>The vocabulary.</b> A token is either a match score in the <c>NaNa</c>
/// form with an optional trailing <c>C</c> Crawford flag (<c>3a5a</c>,
/// <c>1a5aC</c>), or one of the two money tokens
/// <see cref="MoneyWithJacoby"/> / <see cref="MoneyWithoutJacoby"/>. The
/// money tokens carry the Jacoby rule as a suffix exactly the way a match
/// score carries Crawford as one — a game-rule qualifier inside the grammar,
/// not a separate filter facet — and they are the read-back of the spelling
/// <see cref="BgDataTypes_Lib.DecisionRow.MatchScore"/> writes. Wanting money
/// sessions under either rule means listing both tokens, the same way
/// admitting a score regardless of who is on roll means listing both
/// orientations.
/// </para>
///
/// <para>
/// <b>Spellings live here.</b> The three token spellings are exported
/// constants rather than literals repeated at each surface, so a consumer
/// rendering them (help text, a placeholder, an explanation of a rejected
/// token) and the parser accepting them cannot drift. The verdicts are
/// likewise typed and wordless: <see cref="GetFault"/> returns a
/// <see cref="MatchScoreTokenFault"/>, never a sentence — the lib rules on
/// the token, the consumer words the ruling.
/// </para>
///
/// <para>
/// <b>Case and whitespace.</b> The whole grammar is case-insensitive and
/// trims incidental surrounding whitespace before judging anything: the
/// <c>a</c> away-separators, the <c>C</c> Crawford suffix, and the money
/// tokens (including their <c>J</c> / <c>NJ</c> suffixes) all match in any
/// casing, so <c>MONEYNJ</c>, <c>moneynj</c>, and <c>moneyNJ</c> are one
/// token. Embedded whitespace and any extra or repeated separator are
/// rejected — trimming tolerates hand-built configs and CLI arguments, it
/// does not loosen the grammar.
/// </para>
/// </summary>
public static partial class MatchScoreToken
{
    /// <summary>
    /// The money-session token for sessions played <b>with</b> the Jacoby
    /// rule in force: matches a money record whose
    /// <see cref="BgDataTypes_Lib.IDecisionFilterData.IsJacoby"/> is
    /// <see langword="true"/>, and nothing else. Exported so a consumer
    /// renders the spelling this grammar accepts rather than its own literal.
    /// </summary>
    public const string MoneyWithJacoby = "moneyJ";

    /// <summary>
    /// The money-session token for sessions played <b>without</b> the Jacoby
    /// rule: matches a money record whose
    /// <see cref="BgDataTypes_Lib.IDecisionFilterData.IsJacoby"/> is
    /// <see langword="false"/>, and nothing else. Exported for the same
    /// reason as <see cref="MoneyWithJacoby"/>.
    /// </summary>
    public const string MoneyWithoutJacoby = "moneyNJ";

    /// <summary>
    /// The retired bare money token. It once meant "any money session"; the
    /// grammar now distinguishes the Jacoby rule, so it means nothing here
    /// and is rejected with <see cref="MatchScoreTokenFault.Retired"/> rather
    /// than silently reinterpreted as one of the two rule-bearing tokens or
    /// silently matching nothing.
    /// <para>
    /// It survives as a constant because it is still recognized — that is
    /// what makes the rejection specific — and because
    /// <see cref="BgDataTypes_Lib.DecisionRow.MatchScore"/> still
    /// <em>writes</em> it, for the one case that has no rule to state: a
    /// money row whose Jacoby fact was never stamped. Written out it is an
    /// honest "unknown"; read back in as a filter target it is retired
    /// vocabulary, and the asymmetry is deliberate.
    /// </para>
    /// </summary>
    public const string RetiredMoney = "money";

    /// <summary>
    /// What a consumer should offer in place of <see cref="RetiredMoney"/>:
    /// the two rule-bearing money tokens, in the order a reader meets them
    /// (with the rule, then without). This is the data behind
    /// <see cref="MatchScoreTokenFault.Retired"/> — the replacements travel
    /// as values, so the consumer composes the sentence and the lib owns the
    /// spellings.
    /// <para>
    /// Listing <em>both</em> is what the old token used to mean, so a
    /// consumer offering a one-click repair should offer both entries, not a
    /// choice between them.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> RetiredMoneyReplacements { get; } =
        [MoneyWithJacoby, MoneyWithoutJacoby];

    /// <summary>
    /// Judges one token, wordlessly — the single statement of match-score
    /// token validity, and the rule behind both
    /// <see cref="FilterConfig.GetInvalidFields"/> (which reports) and
    /// <see cref="MatchScoreFilter"/>'s constructor (which throws). A
    /// consumer calls it per token to mark exactly the entry the user got
    /// wrong, and reads the returned <see cref="MatchScoreTokenFault"/> to
    /// decide what to say.
    /// </summary>
    /// <param name="token">
    /// The candidate token, or null. Null is
    /// <see cref="MatchScoreTokenFault.Malformed"/> rather than an exception:
    /// an explicit JSON <c>null</c> can reach a deserialized
    /// <see cref="FilterConfig.MatchScores"/> list, and judging a value is
    /// not using it.
    /// </param>
    /// <returns>
    /// <see cref="MatchScoreTokenFault.None"/> when the token is one this
    /// grammar accepts; otherwise the fault that rejects it.
    /// </returns>
    public static MatchScoreTokenFault GetFault(string? token)
    {
        if (token is null)
            return MatchScoreTokenFault.Malformed;

        string trimmed = token.Trim();

        if (IsMoneyWithJacobyToken(trimmed) || IsMoneyWithoutJacobyToken(trimmed))
            return MatchScoreTokenFault.None;

        if (IsRetiredMoneyToken(trimmed))
            return MatchScoreTokenFault.Retired;

        return Inspect(trimmed, out _, out _, out _) == ScoreShape.WellFormed
            ? MatchScoreTokenFault.None
            : MatchScoreTokenFault.Malformed;
    }

    /// <summary>
    /// Whether <paramref name="token"/> is <see cref="MoneyWithJacoby"/>,
    /// under the grammar's case and whitespace rules.
    /// </summary>
    internal static bool IsMoneyWithJacobyToken(string? token) =>
        HasSpelling(token, MoneyWithJacoby);

    /// <summary>
    /// Whether <paramref name="token"/> is <see cref="MoneyWithoutJacoby"/>,
    /// under the grammar's case and whitespace rules.
    /// </summary>
    internal static bool IsMoneyWithoutJacobyToken(string? token) =>
        HasSpelling(token, MoneyWithoutJacoby);

    /// <summary>
    /// Whether <paramref name="token"/> is the retired
    /// <see cref="RetiredMoney"/> token, under the grammar's case and
    /// whitespace rules.
    /// </summary>
    private static bool IsRetiredMoneyToken(string? token) =>
        HasSpelling(token, RetiredMoney);

    /// <summary>
    /// The grammar's one comparison rule for a fixed-spelling token: trim the
    /// ends, then compare case-insensitively by ordinal.
    /// </summary>
    private static bool HasSpelling(string? token, string spelling) =>
        token is not null
        && token.AsSpan().Trim().Equals(spelling, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Parses a match-score token into the tuple
    /// <see cref="MatchScoreFilter"/> targets, throwing on anything
    /// <see cref="GetFault"/> would fault. The two money tokens are the
    /// caller's to recognize first (they are not scores and carry no away
    /// counts); everything else — including the retired
    /// <see cref="RetiredMoney"/> — arrives here.
    /// <para>
    /// Fail-loud by design, and the reason <see cref="FilterConfig.Build"/>
    /// rejects an invalid configuration rather than materializing a filter
    /// nobody could have meant: a silently dropped token leaves the user with
    /// a filter that quietly ignores their typo. The messages carry the
    /// offending token so a consumer that lets the exception surface can
    /// still locate it; a consumer that wants to ask first asks
    /// <see cref="GetFault"/>, and the two cannot disagree because both route
    /// through <see cref="Inspect"/>.
    /// </para>
    /// </summary>
    /// <param name="token">The candidate token.</param>
    /// <returns>The parsed target tuple.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="token"/> is null, malformed, spells a score no game
    /// can carry, or is the retired <see cref="RetiredMoney"/> token.
    /// </exception>
    internal static (int Away1, int Away2, bool IsCrawford) ParseScore(string? token)
    {
        if (token is null)
            throw new ArgumentException(
                "Invalid match score: a null token is not a score.", nameof(token));

        string trimmed = token.Trim();

        if (IsRetiredMoneyToken(trimmed))
            throw new ArgumentException(
                $"Invalid match score: '{token}'. The '{RetiredMoney}' token is retired — " +
                $"a money session is now filtered by its Jacoby rule. Use '{MoneyWithJacoby}' " +
                $"(Jacoby in force), '{MoneyWithoutJacoby}' (Jacoby off), or list both " +
                "for either.",
                nameof(token));

        return Inspect(trimmed, out int a1, out int a2, out bool isCrawford) switch
        {
            ScoreShape.WellFormed => (a1, a2, isCrawford),

            ScoreShape.NonPositiveAway => throw new ArgumentException(
                $"Invalid match score: '{token}'. Away scores must be at least 1 — " +
                "a player needing 0 or fewer points has already won the match.",
                nameof(token)),

            ScoreShape.ImpossibleCrawford => throw new ArgumentException(
                $"Invalid match score: '{token}'. A Crawford game has exactly one side " +
                "1-away and the other 2-away or more; a (1,1) game is always post-Crawford.",
                nameof(token)),

            _ => throw new ArgumentException(
                $"Invalid match score: '{token}'. Expected format like '3a5a', '1a5aC', " +
                $"'{MoneyWithJacoby}', or '{MoneyWithoutJacoby}'.",
                nameof(token)),
        };
    }

    /// <summary>
    /// Why a score token is or is not acceptable. Private: the distinction
    /// between the three rejections is what lets <see cref="ParseScore"/>
    /// word three exceptions, and every one of them is
    /// <see cref="MatchScoreTokenFault.Malformed"/> to a consumer — whose
    /// remedy is the same in all three cases.
    /// </summary>
    private enum ScoreShape
    {
        WellFormed,
        BadFormat,
        NonPositiveAway,
        ImpossibleCrawford,
    }

    /// <summary>
    /// The score-token rule, stated once: match the anchored grammar, then
    /// check the two things a well-formed token can still get wrong. Both
    /// <see cref="GetFault"/> and <see cref="ParseScore"/> route here, so the
    /// answer a consumer asks for and the answer
    /// <see cref="FilterConfig.Build"/> enforces are the same answer.
    /// <para>
    /// Validated beyond the characters because a dead tuple is the silent
    /// "filter does nothing" failure fail-loud exists to prevent: an away
    /// count below 1 belongs to a player who has already won, and a Crawford
    /// token needs exactly one side 1-away with the other 2-away or more
    /// (<c>3a5aC</c> has no 1-away side; <c>1a1aC</c> is always
    /// post-Crawford).
    /// </para>
    /// </summary>
    /// <param name="trimmed">The token, already trimmed.</param>
    /// <param name="away1">The on-roll side's away count when well-formed.</param>
    /// <param name="away2">The opponent's away count when well-formed.</param>
    /// <param name="isCrawford">The Crawford flag when well-formed.</param>
    /// <returns>The token's shape.</returns>
    private static ScoreShape Inspect(
        string trimmed, out int away1, out int away2, out bool isCrawford)
    {
        away1 = away2 = 0;
        isCrawford = false;

        var match = ScoreTokenRegex().Match(trimmed);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, out away1)
            || !int.TryParse(match.Groups[2].Value, out away2))
            return ScoreShape.BadFormat;

        isCrawford = match.Groups[3].Success;

        if (away1 < 1 || away2 < 1)
            return ScoreShape.NonPositiveAway;

        if (isCrawford && (Math.Min(away1, away2) != 1 || Math.Max(away1, away2) < 2))
            return ScoreShape.ImpossibleCrawford;

        return ScoreShape.WellFormed;
    }

    /// <summary>
    /// The case-insensitive score-token grammar: an away count, the <c>a</c>
    /// separator, a second away count and its <c>a</c>, and an optional
    /// trailing <c>C</c> Crawford flag. Anchored, so it rejects any leading,
    /// trailing, embedded, or repeated slop a permissive split would swallow.
    /// </summary>
    [GeneratedRegex(@"^(\d+)[aA](\d+)[aA]([cC])?$")]
    private static partial Regex ScoreTokenRegex();
}
