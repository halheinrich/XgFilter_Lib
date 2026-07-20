using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Parser-free <see cref="IGameInfo"/> test double, the game-scope companion of
/// <see cref="FakeMatchInfo"/>. The header-level filter gates
/// (<see cref="Filtering.IMatchFilter.ShouldSkipGame"/>) consume the
/// abstraction, not any producer's concrete game-info type, so these tests
/// build their input from this fake rather than a parser type.
/// Money sessions: <see cref="Away1"/> = 0, <see cref="Away2"/> = 0,
/// <see cref="IsCrawfordGame"/> = false.
/// </summary>
internal sealed record FakeGameInfo : IGameInfo
{
    /// <inheritdoc/>
    public bool IsStandardStart { get; init; }

    /// <inheritdoc/>
    public int Away1 { get; init; }

    /// <inheritdoc/>
    public int Away2 { get; init; }

    /// <inheritdoc/>
    public bool IsCrawfordGame { get; init; }
}
