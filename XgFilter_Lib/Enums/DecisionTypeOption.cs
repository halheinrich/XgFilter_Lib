using System.ComponentModel;

namespace XgFilter_Lib.Enums;

/// <summary>
/// Specifies which decision types a <see cref="Filtering.DecisionTypeFilter"/>
/// should admit. Each member carries a UI-facing label via
/// <see cref="DescriptionAttribute"/>; read it with
/// <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/>.
/// </summary>
public enum DecisionTypeOption
{
    /// <summary>Admit only checker-play decisions; reject cube decisions.</summary>
    [Description("Checker plays only")]
    CheckerPlaysOnly,

    /// <summary>Admit only cube decisions; reject checker plays.</summary>
    [Description("Cube decisions only")]
    CubeOnly,

    /// <summary>Admit every decision regardless of type.</summary>
    [Description("Both checker and cube")]
    Both,
}
