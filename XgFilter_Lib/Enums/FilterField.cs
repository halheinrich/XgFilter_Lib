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
    /// The lower error bound (<see cref="FilterConfig.ErrorMin"/>) of the
    /// <see cref="FilterFacet.ErrorRange"/> facet.
    /// </summary>
    ErrorMin,

    /// <summary>
    /// The upper error bound (<see cref="FilterConfig.ErrorMax"/>) of the
    /// <see cref="FilterFacet.ErrorRange"/> facet.
    /// </summary>
    ErrorMax,
}
