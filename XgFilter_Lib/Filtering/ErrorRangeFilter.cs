using ConvertXgToJson_Lib.Models;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows where <see cref="DecisionRow.Error"/> falls within [min, max] (inclusive).
/// Either bound may be omitted (null) to leave that end open.
/// </summary>
public sealed class ErrorRangeFilter : IDecisionFilter
{
    private readonly double? _min;
    private readonly double? _max;

    public ErrorRangeFilter(double? min = null, double? max = null)
    {
        _min = min;
        _max = max;
    }

    public bool Matches(DecisionRow row) =>
        (_min is null || row.Error >= _min.Value) &&
        (_max is null || row.Error <= _max.Value);
}
