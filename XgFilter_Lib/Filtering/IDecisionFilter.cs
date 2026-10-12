using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Row-level filter contract. <see cref="Matches"/> is the inclusion test;
/// <see cref="ShouldAdvanceGame"/> and <see cref="ShouldAdvanceMatch"/> are
/// optional mid-stream early-exit hints evaluated from a just-yielded row.
/// For pre-stream skip decisions based on match or game headers (before any
/// rows are yielded), implement <see cref="IMatchFilter"/> as well.
///
/// <para>
/// <b>The hint contract.</b> An advance hint receives any just-yielded
/// decision, whether or not it matches — this filter or the set it belongs
/// to; the hints are never gated on <see cref="Matches"/>. A true vote
/// guarantees that no later decision in the relevant game
/// (<see cref="ShouldAdvanceGame"/>) or match (<see cref="ShouldAdvanceMatch"/>)
/// can pass this filter. It says nothing about the decision voted on, which
/// may pass or fail: a decision this filter rejects can be the one that
/// proves nothing after it can pass (a move past a move-number maximum).
/// </para>
///
/// <para>
/// <b>What the contract buys.</b> In a set the filters are ANDed, so one
/// filter's guarantee covers the set, and cutting where any filter votes
/// skips only decisions the set would reject. Early exit therefore only
/// saves work: the filtering operation
/// (<see cref="FilteredDecisionIterator.IterateFile"/>,
/// <see cref="FilteredDecisionIterator.IterateFileDiagrams"/>) yields exactly
/// the decisions <see cref="DecisionFilterSet.Matches"/> accepts from what the
/// producer yields for the source with no hints at all, in the same order —
/// so a consumer that refilters retained decisions with
/// <see cref="DecisionFilterSet.Matches"/> agrees with one that streams. A
/// hint that votes true where a later decision could still pass breaks that
/// silently, by dropping it.
/// </para>
/// </summary>
public interface IDecisionFilter
{
    /// <summary>Inclusion test for a single row.</summary>
    bool Matches(IDecisionFilterData data);

    /// <summary>
    /// Mid-stream hint, given any decision just yielded, matching or not:
    /// return true only if no later decision in the current game can pass
    /// this filter (see the hint contract on the type). The producer then cuts
    /// to the next game boundary. Row-input, post-yield — distinct from
    /// <see cref="IMatchFilter.ShouldSkipGame"/>, which is header-input and
    /// pre-stream. Defaults to false; a filter must override to opt in.
    /// </summary>
    virtual bool ShouldAdvanceGame(IDecisionFilterData data) => false;

    /// <summary>
    /// Mid-stream hint, given any decision just yielded, matching or not:
    /// return true only if no later decision in the current match can pass
    /// this filter (see the hint contract on the type). The producer then cuts
    /// to the next file. Row-input, post-yield — distinct from
    /// <see cref="IMatchFilter.ShouldSkipMatch"/>, which is header-input and
    /// pre-stream. Defaults to false; a filter must override to opt in.
    /// </summary>
    virtual bool ShouldAdvanceMatch(IDecisionFilterData data) => false;
}
