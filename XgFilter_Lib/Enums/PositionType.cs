using System.ComponentModel;

namespace XgFilter_Lib.Enums;

/// <summary>
/// Board-derived classifications a position can carry. Categories are not
/// mutually exclusive — a single position may satisfy several (e.g. Contact
/// and InnerBoard631). All are determined from the on-roll-relative board
/// array alone; no XGID parsing is involved. Each member carries a UI-facing
/// label via <see cref="DescriptionAttribute"/>; read it with
/// <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/>.
/// </summary>
public enum PositionType
{
    [Description("Contact")]
    Contact,

    [Description("Race")]
    Race,

    [Description("Inner-board 6-3-1")]
    InnerBoard631,

    [Description("Inner-board 5-4-3-2-1")]
    InnerBoard54321,

    [Description("Vs 2+ on bar")]
    VsTwoPlusUp,
}
