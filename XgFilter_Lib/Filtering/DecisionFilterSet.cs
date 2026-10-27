using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// An ordered list of <see cref="IDecisionFilter"/> instances combined with AND semantics.
/// A row must satisfy every filter to pass. The set's skip and advance votes
/// are any member's, and each is sound for the whole set because its members
/// honour the hint contract on <see cref="IDecisionFilter"/>: early exit
/// driven by them never changes what <see cref="Matches"/> accepts.
/// </summary>
public sealed class DecisionFilterSet
{
    private readonly List<IDecisionFilter> _filters = [];

    /// <summary>
    /// The set's one filter for the name and error facets, or null when it
    /// holds none: the half <see cref="ErrorRead"/> reads is that filter's.
    /// </summary>
    private PlayerErrorFilter? _playerError;

    /// <summary>
    /// Adds <paramref name="filter"/> to the set and returns this set for
    /// fluent chaining. Duplicates of the same concrete filter type are
    /// allowed and compose with AND: a row passes only if every entry's
    /// <see cref="IDecisionFilter.Matches"/> returns true (two
    /// <see cref="MoveNumberFilter"/> instances intersect their ranges). The
    /// one exception is the filter for the name and error facets
    /// (<see cref="PlayerErrorFilter"/>): it states which half of a decision
    /// both facets read and which error <see cref="ErrorRead"/> answers, so a
    /// set holds at most one, and a second is refused rather than ANDed —
    /// two would leave the reading with two answers.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="filter"/> is the filter for the name and error facets
    /// and the set already holds one.
    /// </exception>
    public DecisionFilterSet Add(IDecisionFilter filter)
    {
        if (filter is PlayerErrorFilter playerError)
        {
            if (_playerError is not null)
                throw new ArgumentException(
                    "The set already holds the filter for the name and error facets; it holds at most one.",
                    nameof(filter));
            _playerError = playerError;
        }

        _filters.Add(filter);
        return this;
    }

    /// <summary>
    /// True when no filters have been added, in which case <see cref="Matches"/>
    /// admits every row (and the <c>ShouldSkip*</c>/<c>ShouldAdvance*</c> votes
    /// are all false). This is the single source of truth for "no filters
    /// active": consumers must consult it rather than re-inspecting
    /// <see cref="FilterConfig"/> fields, since <see cref="FilterConfig.Build"/>
    /// owns the rule for which fields activate a filter. Mirrors
    /// <see cref="Patterns.BoardPattern.IsEmpty"/>.
    /// </summary>
    public bool IsEmpty => _filters.Count == 0;

    /// <summary>
    /// Returns true if the row passes all filters (or if the set is empty),
    /// judged under the ranking <paramref name="data"/> was built for
    /// (<see cref="IDecisionFilterData.Ranking"/>): a <see cref="DecisionRow"/>
    /// states its own, and a record is filtered through its view for the
    /// ranking in force (<see cref="BgDecisionData.ViewFor"/>).
    /// </summary>
    public bool Matches(IDecisionFilterData data) => _filters.All(f => f.Matches(data));

    /// <summary>
    /// The error this set reads on <paramref name="data"/>, judged under the
    /// ranking it was built for as <see cref="Matches"/> judges: the largest
    /// error among the halves of the decision that qualify under the set's
    /// name and error facets — the listed players' halves within the error
    /// range (SPEC-filtering §8) — or <see langword="null"/> where none has
    /// one. With neither facet active it is the doubler's error, which for a
    /// checker play is the player's. Defined for every decision; callers read
    /// it for the decisions the set passes, as the by-error order does.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
    public double? ErrorRead(IDecisionFilterData data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return (_playerError ?? PlayerErrorFilter.Default).QualifyingError(data);
    }

    /// <summary>
    /// Pre-stream: true if any <see cref="IMatchFilter"/> in the set votes to
    /// skip the match from header metadata alone. One vote suffices: it
    /// guarantees no decision in the match passes that filter, so none passes
    /// the set (the hint contract on <see cref="IDecisionFilter"/>).
    /// </summary>
    public bool ShouldSkipMatch(IMatchInfo match) =>
        _filters.OfType<IMatchFilter>().Any(f => f.ShouldSkipMatch(match));

    /// <summary>
    /// Pre-stream: true if any <see cref="IMatchFilter"/> in the set votes to
    /// skip the game from header metadata alone. One vote suffices, as for
    /// <see cref="ShouldSkipMatch"/>.
    /// </summary>
    public bool ShouldSkipGame(IGameInfo game) =>
        _filters.OfType<IMatchFilter>().Any(f => f.ShouldSkipGame(game));

    /// <summary>
    /// Mid-stream: true if any filter, given the decision just yielded —
    /// matching the set or not; the vote is never gated on
    /// <see cref="Matches"/> — votes to cut the rest of the current game. One
    /// vote suffices: it guarantees no later decision in the game passes that
    /// filter, so none passes the set (the hint contract on
    /// <see cref="IDecisionFilter"/>).
    /// </summary>
    public bool ShouldAdvanceGame(IDecisionFilterData data) => _filters.Any(f => f.ShouldAdvanceGame(data));

    /// <summary>
    /// Mid-stream: true if any filter, given the decision just yielded —
    /// matching the set or not — votes to cut the rest of the current match.
    /// One vote suffices, as for <see cref="ShouldAdvanceGame"/>.
    /// </summary>
    public bool ShouldAdvanceMatch(IDecisionFilterData data) => _filters.Any(f => f.ShouldAdvanceMatch(data));
}
