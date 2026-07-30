using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// The depth facet, expressed as a <em>union of per-mode clauses</em> over the
/// two-axis analysis taxonomy (<see cref="AnalysisMode"/> ×
/// <see cref="AnalysisLevel"/>) that replaced the retired flat depth class.
/// Each <see cref="Clause"/> admits one mode, qualified by its own level set;
/// a row passes iff <em>any</em> clause admits it. Depth is a scalar pair the
/// producer already stamped on each decision (the cube analysis for cube rows,
/// the best-by-equity candidate for checker rows), so this is a direct
/// membership test with no classifier dispatch and no board reads.
///
/// <para>
/// The union-of-clauses shape exists because a level selection qualifies only
/// its own mode. For <see cref="AnalysisMode.Evaluation"/> the row's level is
/// the level of the evaluation itself; for the rollout-family modes it is the
/// <em>inner</em> level of the rollout's games — and checker rollouts never
/// carry Roller-family inner levels. A single level set shared across all
/// selected modes (the previous conjunction design) therefore both made
/// "(any rollout) OR (evaluation at XG Roller++)" inexpressible and let a
/// level selection wrongly constrain rollout rows by inner level, matching
/// nothing. Per-clause levels dissolve both defects: each mode is constrained
/// only by the levels selected <em>for it</em>.
/// </para>
///
/// <para>
/// The clause union is derived in <see cref="FilterConfig.Build"/> (the single
/// source of truth the UI must not re-encode): one clause per enabled mode
/// toggle, carrying that mode's own level list. The clause set is always
/// non-empty — an inactive facet is expressed by omitting the filter from the
/// set, never by an empty union — so this filter rejects an empty clause
/// collection at construction rather than silently admitting nothing. Within a
/// clause the level set may be empty, meaning "any level"; that unconstrained
/// axis is how a book hit whose inner level the producer could not recover
/// (<see cref="AnalysisMode.BookRollout"/> + <see cref="AnalysisLevel.Unknown"/>)
/// is admitted. No clause ever names <see cref="AnalysisMode.Unknown"/> — no
/// UI selection produces it, so <c>Unknown</c>-mode rows (legacy / unstamped
/// data) pass only when the whole facet is inactive and this filter is absent
/// from the set.
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
    /// <summary>
    /// One admitted selection of the depth facet: rows of <see cref="Mode"/>
    /// whose <see cref="IDecisionFilterData.AnalysisLevel"/> is in the
    /// clause's level set — any level when the set is empty. Immutable and
    /// validated at construction, so a clause is never invalid.
    /// </summary>
    /// <remarks>
    /// Equality is not meaningful on this type (the level set is a reference
    /// member, so the synthesized record equality degrades to reference
    /// comparison there) — the same no-value-equality posture as
    /// <see cref="Patterns.BoardPattern"/>. Compare structurally if a test
    /// ever needs to.
    /// </remarks>
    internal sealed record Clause
    {
        private readonly HashSet<AnalysisLevel> _levels;

        /// <summary>The analysis mode this clause admits.</summary>
        public AnalysisMode Mode { get; }

        /// <summary>
        /// Creates a clause admitting rows of <paramref name="mode"/> whose
        /// level is any of <paramref name="levels"/>, or any level when
        /// <paramref name="levels"/> is empty.
        /// </summary>
        /// <param name="mode">
        /// The admitted analysis mode. Never
        /// <see cref="AnalysisMode.Unknown"/> — no UI selection produces it
        /// (see the filter's type remarks).
        /// </param>
        /// <param name="levels">
        /// The admitted levels for this mode, or an empty sequence to leave
        /// the level axis unconstrained.
        /// </param>
        /// <exception cref="ArgumentException">
        /// <paramref name="mode"/> is <see cref="AnalysisMode.Unknown"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// <paramref name="mode"/> or <paramref name="levels"/> contains an
        /// undefined enum value.
        /// </exception>
        public Clause(AnalysisMode mode, IEnumerable<AnalysisLevel> levels)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(
                    nameof(mode), mode, "Unknown AnalysisMode");

            if (mode == AnalysisMode.Unknown)
                throw new ArgumentException(
                    "AnalysisMode.Unknown is not admissible in a clause; no " +
                    "selection produces it, and Unknown-mode rows pass only " +
                    "when the depth facet is inactive.",
                    nameof(mode));

            _levels = new HashSet<AnalysisLevel>(levels);

            foreach (var level in _levels)
                if (!Enum.IsDefined(level))
                    throw new ArgumentOutOfRangeException(
                        nameof(levels), level, "Unknown AnalysisLevel");

            Mode = mode;
        }

        /// <summary>
        /// Returns <c>true</c> iff the row's
        /// <see cref="IDecisionFilterData.AnalysisMode"/> equals
        /// <see cref="Mode"/> and its
        /// <see cref="IDecisionFilterData.AnalysisLevel"/> is in the clause's
        /// level set — the latter always satisfied when the set is empty.
        /// </summary>
        public bool Admits(IDecisionFilterData data) =>
            Mode == data.AnalysisMode
            && (_levels.Count == 0 || _levels.Contains(data.AnalysisLevel));
    }

    private readonly Clause[] _clauses;

    /// <summary>
    /// Creates a filter passing rows admitted by any of
    /// <paramref name="clauses"/>.
    /// </summary>
    /// <param name="clauses">
    /// The per-mode clauses to union. Must be non-empty — an inactive depth
    /// facet is expressed by omitting the filter from the set (see the type
    /// remarks); <see cref="FilterConfig.Build"/> guarantees this. Clauses on
    /// the same mode are legal and simply union their level sets.
    /// </param>
    /// <exception cref="ArgumentException">
    /// <paramref name="clauses"/> is empty or contains a null clause.
    /// </exception>
    public AnalysisDepthFilter(IEnumerable<Clause> clauses)
    {
        _clauses = clauses.ToArray();

        if (_clauses.Length == 0)
            throw new ArgumentException(
                "At least one clause is required; an inactive depth facet is " +
                "expressed by omitting the filter, not by an empty union.",
                nameof(clauses));

        if (Array.IndexOf(_clauses, null) >= 0)
            throw new ArgumentException(
                "Clauses must not contain null.", nameof(clauses));
    }

    /// <summary>
    /// Returns <c>true</c> iff any clause admits the row — mode equality plus
    /// membership in that clause's level set (always satisfied when the
    /// clause selected no levels).
    /// </summary>
    public bool Matches(IDecisionFilterData data)
    {
        foreach (var clause in _clauses)
            if (clause.Admits(data))
                return true;

        return false;
    }
}
