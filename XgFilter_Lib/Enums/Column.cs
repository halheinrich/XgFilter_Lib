using System.ComponentModel;

namespace XgFilter_Lib.Enums;

/// <summary>
/// CSV columns available for projection via
/// <see cref="Projection.ColumnSelector"/>. Each member carries the
/// column's CSV-header text as a <see cref="DescriptionAttribute"/>
/// label; <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/> reads it. The
/// declaration order is the default output order.
/// </summary>
public enum Column
{
    [Description("Xgid")]
    Xgid,

    [Description("Error")]
    Error,

    [Description("MatchScore")]
    MatchScore,

    [Description("MatchLength")]
    MatchLength,

    [Description("Player")]
    Player,

    [Description("SourceFile")]
    SourceFile,

    [Description("Game")]
    Game,

    [Description("MoveNumber")]
    MoveNumber,

    [Description("Roll")]
    Roll,

    [Description("AnalysisDepth")]
    AnalysisDepth,

    [Description("Equity")]
    Equity,
}
