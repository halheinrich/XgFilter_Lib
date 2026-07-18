using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The depth facet, expressed over the two-axis analysis taxonomy
/// (<see cref="AnalysisMode"/> × <see cref="AnalysisLevel"/>) that replaced the
/// retired flat depth class. A row passes iff its
/// <see cref="IDecisionFilterData.AnalysisMode"/> is one of the selected
/// modes <em>and</em> its
/// <see cref="IDecisionFilterData.AnalysisLevel"/> satisfies the level axis —
/// membership in the selected levels, or any level when the level set is
/// empty. Depth is a scalar pair the producer
/// already stamped on each decision (the cube analysis for cube rows, the
/// best-by-equity candidate for checker rows), so this is a direct
/// two-axis membership test with no classifier dispatch and no board reads.
///
/// <para>
/// The two axes are not symmetric, and that asymmetry is deliberate — it
/// mirrors the facet's selection semantics, whose derivation lives in
/// <see cref="FilterConfig.Build"/> (the single source of truth the UI must
/// not re-encode):
/// <list type="bullet">
///   <item><description>
///     The <b>mode</b> set is always non-empty. An active facet defaults to
///     <see cref="AnalysisMode.Evaluation"/> when neither rollout toggle is on;
///     <see cref="FilterConfig.Build"/> supplies that default, so this filter
///     rejects an empty mode set at construction rather than silently admitting
///     nothing. It never receives <see cref="AnalysisMode.Unknown"/> — no
///     selection produces it, so <c>Unknown</c>-mode rows (legacy / unstamped
///     data) pass only when the whole facet is inactive and this filter is
///     absent from the set.
///   </description></item>
///   <item><description>
///     The <b>level</b> set may be empty, meaning "any level". This is how a
///     book hit whose inner level the producer could not recover
///     (<see cref="AnalysisMode.BookRollout"/> + <see cref="AnalysisLevel.Unknown"/>)
///     is admitted: select Book rollouts with no level checked, and the
///     unconstrained level axis lets the <c>Unknown</c> level through.
///   </description></item>
/// </list>
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
/// Undefined <see cref="AnalysisMode"/> / <see cref="AnalysisLevel"/> values are
/// rejected at construction rather than on first dispatch.
/// </para>
/// </summary>
internal sealed class AnalysisDepthFilter : IDecisionFilter
{
    private readonly HashSet<AnalysisMode> _modes;
    private readonly HashSet<AnalysisLevel> _levels;

    /// <summary>
    /// Creates a filter passing rows whose
    /// <see cref="IDecisionFilterData.AnalysisMode"/> is any of
    /// <paramref name="modes"/> and whose
    /// <see cref="IDecisionFilterData.AnalysisLevel"/> is any of
    /// <paramref name="levels"/> (or any level when <paramref name="levels"/> is
    /// empty).
    /// </summary>
    /// <param name="modes">
    /// The admitted analysis modes. Must be non-empty — an active depth facet
    /// always resolves to at least one mode (see the type remarks);
    /// <see cref="FilterConfig.Build"/> guarantees this.
    /// </param>
    /// <param name="levels">
    /// The admitted evaluation levels, or an empty sequence to leave the level
    /// axis unconstrained.
    /// </param>
    /// <exception cref="ArgumentException"><paramref name="modes"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="modes"/> or <paramref name="levels"/> contains an
    /// undefined enum value.
    /// </exception>
    public AnalysisDepthFilter(
        IEnumerable<AnalysisMode> modes,
        IEnumerable<AnalysisLevel> levels)
    {
        _modes = new HashSet<AnalysisMode>(modes);
        _levels = new HashSet<AnalysisLevel>(levels);

        if (_modes.Count == 0)
            throw new ArgumentException(
                "At least one AnalysisMode is required; an active depth facet " +
                "resolves to Evaluation when neither rollout toggle is on.",
                nameof(modes));

        foreach (var mode in _modes)
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(
                    nameof(modes), mode, "Unknown AnalysisMode");

        foreach (var level in _levels)
            if (!Enum.IsDefined(level))
                throw new ArgumentOutOfRangeException(
                    nameof(levels), level, "Unknown AnalysisLevel");
    }

    /// <summary>
    /// Returns <c>true</c> iff the row's
    /// <see cref="IDecisionFilterData.AnalysisMode"/> is in the selected mode set
    /// and its <see cref="IDecisionFilterData.AnalysisLevel"/> is in the selected
    /// level set — the latter always satisfied when no levels were selected.
    /// </summary>
    public bool Matches(IDecisionFilterData data) =>
        _modes.Contains(data.AnalysisMode)
        && (_levels.Count == 0 || _levels.Contains(data.AnalysisLevel));
}
