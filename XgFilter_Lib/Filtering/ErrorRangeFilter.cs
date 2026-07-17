using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="IDecisionFilterData.FilterError"/> falls within
/// [min, max] (inclusive). Either bound may be omitted (null) to leave that end open.
/// Rows with a null <c>FilterError</c> (unanalysed positions) do not pass.
/// </summary>
internal sealed class ErrorRangeFilter : IDecisionFilter
{
    private readonly double? _min;
    private readonly double? _max;

    /// <summary>
    /// Creates a filter passing rows whose <see cref="IDecisionFilterData.FilterError"/>
    /// is in <c>[min, max]</c> inclusive. Either bound may be null to leave that end open.
    /// </summary>
    public ErrorRangeFilter(double? min = null, double? max = null)
    {
        _min = min;
        _max = max;
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data)
    {
        if (data.FilterError is not double error) return false;
        return (_min is null || error >= _min.Value) &&
               (_max is null || error <= _max.Value);
    }
}
