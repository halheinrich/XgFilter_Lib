using BgDataTypes_Lib;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Parser-free <see cref="IGameInfo"/> test double, the game-scope companion of
/// <see cref="FakeMatchInfo"/>. The header-level filter gates
/// (<see cref="Filtering.IMatchFilter.ShouldSkipGame"/>) consume the
/// abstraction, not any producer's concrete game-info type, so these tests
/// build their input from this fake rather than a parser type. Money versus
/// match is the <see cref="Standing"/>'s kind, as on the contract; the
/// factories state each kind's standing, player 1's first.
/// </summary>
internal sealed record FakeGameInfo : IGameInfo
{
    /// <inheritdoc/>
    public bool IsStandardStart { get; init; } = true;

    /// <inheritdoc/>
    public required GameStanding Standing { get; init; }

    /// <summary>A match game at player 1 needing <paramref name="away1"/> and player 2 <paramref name="away2"/>.</summary>
    public static FakeGameInfo Match(int away1, int away2, bool isCrawford = false) =>
        new() { Standing = new MatchStanding { Away1 = away1, Away2 = away2, IsCrawford = isCrawford } };

    /// <summary>A money game at the session's start, 0-0.</summary>
    public static FakeGameInfo Money() =>
        new() { Standing = new MoneyStanding { Score1 = 0, Score2 = 0 } };
}
