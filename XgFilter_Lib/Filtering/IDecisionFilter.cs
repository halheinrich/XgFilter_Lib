using BgDataTypes_Lib;

namespace XgFilter_Lib.Filtering;

/// <summary>
/// Row-level filter contract. <see cref="Matches"/> is the inclusion test;
/// <see cref="ShouldAdvanceGame"/> and <see cref="ShouldAdvanceMatch"/> are
/// optional mid-stream early-exit hints evaluated from a just-yielded row.
/// For pre-stream skip decisions based on match or game headers (before any
/// rows are yielded), implement <see cref="IMatchFilter"/> as well.
/// </summary>
public interface IDecisionFilter
{
    /// <summary>Inclusion test for a single row.</summary>
    bool Matches(IDecisionFilterData data);

    /// <summary>
    /// Mid-stream hint: after <paramref name="data"/> has matched, return true
    /// if no further row in the current game can match this filter. Caller will
    /// cut to the next game boundary. Row-input, post-yield — distinct from
    /// <see cref="IMatchFilter.ShouldSkipGame"/>, which is header-input and
    /// pre-stream. Defaults to false; a filter must override to opt in.
    /// </summary>
    virtual bool ShouldAdvanceGame(IDecisionFilterData data) => false;

    /// <summary>
    /// Mid-stream hint: after <paramref name="data"/> has matched, return true
    /// if no further row in the current match can match this filter. Caller
    /// will cut to the next file. Row-input, post-yield — distinct from
    /// <see cref="IMatchFilter.ShouldSkipMatch"/>, which is header-input and
    /// pre-stream. Defaults to false; a filter must override to opt in.
    /// </summary>
    virtual bool ShouldAdvanceMatch(IDecisionFilterData data) => false;
}
