using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Enums;

/// <summary>
/// The vocabulary of individually-blameable <see cref="FilterConfig"/> members
/// — one member per settable field a validity rule can name, and the currency
/// of <see cref="FilterConfig.GetInvalidFields"/>.
///
/// <para>
/// This is a deliberately partial vocabulary, and grows with the rules: a
/// member exists here iff some rule in <see cref="FilterConfig"/>'s field-rule
/// table can name it. Most facets have no rule — a checkbox list cannot be
/// filled in wrongly — so their members are absent rather than permanently
/// valid. Do not read this enum as an inventory of the configuration's
/// members; <see cref="FilterFacet"/> is the complete facet vocabulary, and
/// each member below documents the facet it belongs to.
/// </para>
///
/// <para>
/// Unlike <see cref="FilterFacet"/>, members carry no
/// <see cref="System.ComponentModel.DescriptionAttribute"/> label. A facet
/// label serves a lib-driven signal (the "N hidden filters active" count names
/// sections the lib, not the consumer, chose to enumerate); a field label is
/// simply the caption a consumer already put beside its own input, so
/// duplicating it here would create a second source for text the lib never
/// renders.
/// </para>
/// </summary>
public enum FilterField
{
    /// <summary>
    /// The match-score token list (<see cref="FilterConfig.MatchScores"/>) of
    /// the <see cref="FilterFacet.MatchScores"/> facet — named when any entry
    /// is a token <see cref="MatchScoreToken.GetFault"/> faults.
    /// <para>
    /// The one field of this vocabulary whose facet holds a <em>list</em>, so
    /// naming it says "some entry here is wrong" rather than "this box is
    /// wrong". A consumer that marks the individual entry — and that needs to
    /// tell a retired token from a malformed one, which this set cannot carry
    /// — asks <see cref="MatchScoreToken.GetFault"/> per token; the two
    /// answers cannot disagree, since this field's rule is that query swept
    /// over the list.
    /// </para>
    /// </summary>
    MatchScores,

    /// <summary>
    /// The lower error bound (<see cref="FilterConfig.ErrorMin"/>) of the
    /// <see cref="FilterFacet.ErrorRange"/> facet.
    /// </summary>
    ErrorMin,

    /// <summary>
    /// The upper error bound (<see cref="FilterConfig.ErrorMax"/>) of the
    /// <see cref="FilterFacet.ErrorRange"/> facet.
    /// </summary>
    ErrorMax,

    /// <summary>
    /// The lower move-number bound (<see cref="FilterConfig.MoveNumberMin"/>)
    /// of the <see cref="FilterFacet.MoveNumberRange"/> facet.
    /// </summary>
    MoveNumberMin,

    /// <summary>
    /// The upper move-number bound (<see cref="FilterConfig.MoveNumberMax"/>)
    /// of the <see cref="FilterFacet.MoveNumberRange"/> facet.
    /// </summary>
    MoveNumberMax,
}
