using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Parser-free <see cref="IMatchInfo"/> test double. The header-level filter
/// gates (<see cref="Filtering.IMatchFilter.ShouldSkipMatch"/>) consume the
/// abstraction, not any producer's concrete match-info type, so these tests
/// build their input from this fake — the decoupling the contract-layer arc
/// exists to buy. Money versus match is the <see cref="Terms"/>' kind, as on
/// the contract; the factories state each kind's terms.
/// </summary>
internal sealed record FakeMatchInfo : IMatchInfo
{
    /// <inheritdoc/>
    public string Player1 { get; init; } = "Player 1";

    /// <inheritdoc/>
    public string Player2 { get; init; } = "Player 2";

    /// <inheritdoc/>
    public required SessionTerms Terms { get; init; }

    /// <summary>A match header for a match to <paramref name="length"/> points.</summary>
    public static FakeMatchInfo Match(int length) =>
        new() { Terms = new MatchTerms { Length = length } };

    /// <summary>
    /// A money-session header under <paramref name="isJacoby"/>, with XG's
    /// other money defaults: no beaver rule, a cube limit of 1024.
    /// </summary>
    public static FakeMatchInfo Money(bool isJacoby) =>
        new() { Terms = new MoneyTerms { IsJacoby = isJacoby, IsBeaver = false, CubeLimit = 1024 } };
}
