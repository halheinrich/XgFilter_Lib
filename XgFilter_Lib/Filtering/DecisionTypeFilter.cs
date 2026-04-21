using System.ComponentModel;
using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Specifies which decision types to include. Each member carries a
/// UI-facing label via <see cref="DescriptionAttribute"/>; read it with
/// <see cref="Enums.EnumLabel.ToLabel{TEnum}(TEnum)"/>.
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

/// <summary>
/// Passes rows based on whether they are cube decisions or checker plays,
/// as determined by <see cref="DecisionRow.IsCube"/>.
/// </summary>
public sealed class DecisionTypeFilter : IDecisionFilter
{
    private readonly DecisionTypeOption _option;

    public DecisionTypeFilter(DecisionTypeOption option)
    {
        _option = option;
    }

    public bool Matches(IDecisionFilterData data) => _option switch
    {
        DecisionTypeOption.CheckerPlaysOnly => !data.IsCube,
        DecisionTypeOption.CubeOnly         => data.IsCube,
        DecisionTypeOption.Both             =>  true,
        _ => throw new ArgumentOutOfRangeException(
            nameof(_option), _option, "Unknown DecisionTypeOption"),
    };
}
