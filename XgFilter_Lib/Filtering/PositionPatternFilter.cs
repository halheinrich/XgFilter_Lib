using BgDataTypes_Lib;
using XgFilter_Lib.Patterns;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows whose board satisfies a <see cref="BoardPattern"/> — a general
/// per-point checker-range predicate. This is the pattern-based counterpart to
/// the named <see cref="PositionTypeFilter"/>: where that filter dispatches to
/// hand-written classifiers, this one evaluates an arbitrary sparse constraint
/// set, so a caller can express a structural shape without a dedicated
/// <c>PositionType</c>. An empty pattern matches every board.
/// </summary>
public sealed class PositionPatternFilter : IDecisionFilter
{
    private readonly BoardPattern _pattern;

    /// <summary>
    /// Creates a filter passing rows whose board matches <paramref name="pattern"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="pattern"/> is null.</exception>
    public PositionPatternFilter(BoardPattern pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _pattern = pattern;
    }

    /// <inheritdoc/>
    public bool Matches(IDecisionFilterData data) => _pattern.Matches(data.Board);
}
