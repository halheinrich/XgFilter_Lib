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
    [Description("Checker plays only")]
    CheckerPlaysOnly,

    [Description("Cube decisions only")]
    CubeOnly,

    [Description("Both checker and cube")]
    Both,
}
