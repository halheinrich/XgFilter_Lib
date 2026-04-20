namespace XgFilter_Lib.Enums;

/// <summary>
/// Classifies the type of checker play, derived from the prior board and
/// the boards after the best and actual plays. Each member pairs with an
/// <see cref="Classification.IPlayTypeClassifier"/> implementation.
/// </summary>
public enum PlayType
{
    Make20Pt,
}
