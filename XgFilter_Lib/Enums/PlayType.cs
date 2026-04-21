using System.ComponentModel;

namespace XgFilter_Lib.Enums;

/// <summary>
/// Classifies the type of checker play, derived from the prior board and
/// the boards after the best and actual plays. Each member pairs with an
/// <see cref="Classification.IPlayTypeClassifier"/> implementation and
/// carries a UI-facing label via <see cref="DescriptionAttribute"/>;
/// read it with <see cref="EnumLabel.ToLabel{TEnum}(TEnum)"/>.
/// </summary>
public enum PlayType
{
    [Description("Make 20-point?")]
    Make20Pt,
}
