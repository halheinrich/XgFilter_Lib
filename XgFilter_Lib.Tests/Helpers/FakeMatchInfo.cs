using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Parser-free <see cref="IMatchInfo"/> test double. The header-level filter
/// gates (<see cref="Filtering.IMatchFilter.ShouldSkipMatch"/>) consume the
/// abstraction, not any producer's concrete match-info type, so these tests
/// build their input from this fake — the decoupling the contract-layer arc
/// exists to buy. <see cref="IMatchInfo.IsMoneyGame"/> is inherited from the
/// contract's default member (derived from <see cref="MatchLength"/>), never
/// restated here.
/// </summary>
internal sealed record FakeMatchInfo : IMatchInfo
{
    /// <inheritdoc/>
    public string Player1 { get; init; } = string.Empty;

    /// <inheritdoc/>
    public string Player2 { get; init; } = string.Empty;

    /// <inheritdoc/>
    public int MatchLength { get; init; }
}
