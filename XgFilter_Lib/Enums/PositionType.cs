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
    /// <summary>At least one player still has checkers on or behind opposing checkers.</summary>
    [Description("Contact")]
    Contact,

    /// <summary>The two checker blocks have separated — no further hits are possible.</summary>
    [Description("Race")]
    Race,

    /// <summary>Inner-board 6-3-1 structure: points 6, 3, and 1 made; points 5, 4, and 2 broken.</summary>
    [Description("Inner-board 6-3-1")]
    InnerBoard631,

    /// <summary>Full 5-4-3-2-1 inner board: points 5 through 1 made, point 6 broken.</summary>
    [Description("Inner-board 5-4-3-2-1")]
    InnerBoard54321,

    /// <summary>Opponent has two or more checkers on the bar.</summary>
    [Description("Vs 2+ on bar")]
    VsTwoPlusUp,

    /// <summary>Player holds the 13, 8, and 6 points; opponent anchors on the 20.</summary>
    [Description("Holding 13-8-6 vs 20")]
    Holding1386Vs20,
}
