using System.ComponentModel;
using System.Text.Json.Serialization;

namespace XgFilter_Lib.Enums;

/// <summary>
/// Structural board patterns a position can carry. These are not mutually
/// exclusive — a single position may satisfy several at once (e.g.
/// InnerBoard631 and VsTwoPlusUp). Whether a position has contact or has
/// raced is a separate, orthogonal axis; see <see cref="ContactType"/>. All
/// are determined from the on-roll-relative board array alone; no XGID
/// parsing is involved. Each member carries a UI-facing label via
/// <see cref="DescriptionAttribute"/>; read it with
/// <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/>.
/// </summary>
[JsonConverter(typeof(StrictJsonStringEnumConverter<PositionType>))]
public enum PositionType
{
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
