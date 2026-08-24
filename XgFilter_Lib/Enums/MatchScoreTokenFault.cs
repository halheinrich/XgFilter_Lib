using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Enums;

/// <summary>
/// The typed verdict on one match-score token — what
/// <see cref="MatchScoreToken.GetFault"/> answers, and the fact a consumer
/// reads to decide what to say about a rejected token.
///
/// <para>
/// The vocabulary is deliberately two faults, not one per parse rule: the
/// distinction that changes what a consumer tells the user is
/// <em>retired vocabulary</em> versus <em>wrong shape</em>. A retired token
/// was spelled correctly for an earlier grammar, so the answer names its
/// replacements (<see cref="MatchScoreToken.RetiredMoneyReplacements"/>);
/// every other rejection is answered by retyping the token, whether the
/// characters were wrong or the score they spell is one no real game can
/// carry.
/// </para>
///
/// <para>
/// No member carries text. The lib rules on the token and the consumer says
/// so in its own voice — the same division of labour
/// <see cref="FilterConfig.GetInvalidFields"/> and
/// <see cref="Patterns.BoardPattern.TryParse"/> already keep. What crosses
/// the API is the fault, the offending token (the caller's own input), and
/// the replacement spellings as values.
/// </para>
/// </summary>
public enum MatchScoreTokenFault
{
    /// <summary>
    /// The token is a valid entry of <see cref="FilterConfig.MatchScores"/>:
    /// a well-formed, possible match score
    /// (<c>3a5a</c>, <c>1a5aC</c>), or one of the money tokens
    /// (<see cref="MatchScoreToken.MoneyWithJacoby"/>,
    /// <see cref="MatchScoreToken.MoneyWithoutJacoby"/>).
    /// </summary>
    None,

    /// <summary>
    /// The token is not a score this grammar accepts — either not of the
    /// <c>NaNa[C]</c> form at all, or of that form but spelling a score no
    /// game can carry (an away count below 1, a Crawford token without
    /// exactly one side 1-away and the other 2-away or more). Both are
    /// answered by retyping the token, which is why they share a member.
    /// </summary>
    Malformed,

    /// <summary>
    /// The token is retired vocabulary: it named something real under an
    /// earlier grammar and no longer does. Today the only retired token is
    /// <see cref="MatchScoreToken.RetiredMoney"/>, whose replacements are
    /// <see cref="MatchScoreToken.RetiredMoneyReplacements"/> — the
    /// distinction this member exists to let a consumer draw, so a filter
    /// written before the money tokens split can be explained rather than
    /// merely refused.
    /// </summary>
    Retired,
}
