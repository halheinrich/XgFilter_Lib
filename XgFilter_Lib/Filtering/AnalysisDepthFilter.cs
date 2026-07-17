using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Passes rows whose <see cref="IDecisionFilterData.AnalysisDepthClass"/> is any
/// of the selected depth classes (OR semantics within the facet). Unlike the
/// board-reading facets (<see cref="ContactTypeFilter"/>,
/// <see cref="PositionTypeFilter"/>, <see cref="PlayTypeFilter"/>), depth is a
/// scalar the producer already stamped on each decision — the cube analysis for
/// cube rows, the best-by-equity candidate for checker rows — so this filter is
/// a direct enum-membership test with no classifier dispatch and no board reads.
///
/// <para>
/// Rows carrying <see cref="AnalysisDepthClass.Unknown"/> — legacy archives, or
/// anything the producer could not classify — are excluded unless
/// <see cref="AnalysisDepthClass.Unknown"/> is itself selected. This is the same
/// drop-don't-pass convention <see cref="ErrorRangeFilter"/> applies to a null
/// <c>FilterError</c>, with <see cref="AnalysisDepthClass.Unknown"/>'s
/// selectability as the deliberate opt-in for consumers that want the
/// unclassified tail. An empty include-set admits nothing (empty OR), matching
/// <see cref="ContactTypeFilter"/> / <see cref="PlayTypeFilter"/>.
/// </para>
///
/// <para>
/// Deliberately implements only <see cref="IDecisionFilter.Matches"/> — no
/// <see cref="IMatchFilter"/> and no <c>ShouldAdvance*</c> overrides. Depth is
/// not knowable from a match or game header (it is a per-decision property), and
/// it is not monotonic within a game (a single game can mix book, N-ply, and
/// rollout decisions), so there is no sound early-exit to offer.
/// </para>
///
/// <para>
/// Undefined <see cref="AnalysisDepthClass"/> values are rejected at
/// construction rather than on first dispatch.
/// </para>
/// </summary>
internal sealed class AnalysisDepthFilter : IDecisionFilter
{
    private readonly HashSet<AnalysisDepthClass> _classes;

    /// <summary>
    /// Creates a filter passing rows whose
    /// <see cref="IDecisionFilterData.AnalysisDepthClass"/> is any of the
    /// selected <paramref name="classes"/>. Throws
    /// <see cref="ArgumentOutOfRangeException"/> if <paramref name="classes"/>
    /// contains an undefined enum value.
    /// </summary>
    public AnalysisDepthFilter(IEnumerable<AnalysisDepthClass> classes)
    {
        _classes = new HashSet<AnalysisDepthClass>(classes);
        foreach (var depthClass in _classes)
            if (!Enum.IsDefined(depthClass))
                throw new ArgumentOutOfRangeException(
                    nameof(classes), depthClass, "Unknown AnalysisDepthClass");
    }

    /// <summary>
    /// Returns <c>true</c> iff the row's
    /// <see cref="IDecisionFilterData.AnalysisDepthClass"/> is in the selected
    /// set. Rows carrying <see cref="AnalysisDepthClass.Unknown"/> pass only
    /// when <see cref="AnalysisDepthClass.Unknown"/> was explicitly selected.
    /// </summary>
    public bool Matches(IDecisionFilterData data) =>
        _classes.Contains(data.AnalysisDepthClass);
}
