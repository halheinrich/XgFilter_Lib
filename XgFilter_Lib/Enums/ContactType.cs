using System.ComponentModel;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Enums;

/// <summary>
/// Whether a position still carries contact or has raced. Unlike the
/// structural patterns in <see cref="PositionType"/>, these two members form a
/// partition — every position is exactly one of <see cref="Contact"/> or
/// <see cref="Race"/>, never both and never neither. This makes contact-vs-race
/// an axis orthogonal to <see cref="PositionType"/>, so the two facets compose
/// via AND (e.g. Contact ∧ Holding13-8-6-vs-20) rather than collapsing into
/// OR-within-a-single-facet. Determined from the on-roll-relative board array
/// alone; no XGID parsing is involved. Each member carries a UI-facing label via
/// <see cref="DescriptionAttribute"/>; read it with
/// <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/>.
/// </summary>
[JsonConverter(typeof(StrictJsonStringEnumConverter<ContactType>))]
public enum ContactType
{
    /// <summary>At least one player still has checkers on or behind opposing checkers.</summary>
    [Description("Contact")]
    Contact,

    /// <summary>The two checker blocks have separated — no further hits are possible.</summary>
    [Description("Race")]
    Race,
}
