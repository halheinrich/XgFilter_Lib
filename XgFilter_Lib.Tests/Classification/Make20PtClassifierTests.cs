using XgFilter_Lib.Classification;

namespace XgFilter_Lib.Tests.Classification;

public class Make20PtClassifierTests
{
    private static readonly Make20PtClassifier _sut = new();

    // priorBoard: decision-maker on roll. Their 20-point is index 20;
    // positive counts = decision-maker's checkers, negative = opponent's.
    private static int[] Prior(int at20)
    {
        var b = new int[26];
        b[20] = at20;
        return b;
    }

    // After-board: opponent on roll after the turn-flip. The decision-
    // maker's 20-point is now index 5; their checkers are negative,
    // opponent's are positive. `decisionMakerCount` is stored as its
    // negation; `opponentCount` is stored positively.
    private static int[] After(int decisionMakerCount, int opponentCount = 0)
    {
        var b = new int[26];
        b[5] = opponentCount - decisionMakerCount;
        return b;
    }

    // -----------------------------------------------------------------------
    //  Positive — XOR true, 20-point not already made
    // -----------------------------------------------------------------------

    [Fact]
    public void BestMakesFromEmpty_PlayerLeavesEmpty_ReturnsTrue()
    {
        _sut.Matches(
                Prior(0),
                After(decisionMakerCount: 2),
                After(decisionMakerCount: 0))
            .Should().BeTrue();
    }

    [Fact]
    public void PlayerMakesFromEmpty_BestLeavesEmpty_ReturnsTrue()
    {
        _sut.Matches(
                Prior(0),
                After(decisionMakerCount: 0),
                After(decisionMakerCount: 2))
            .Should().BeTrue();
    }

    [Fact]
    public void BestStacksFriendlyBlot_PlayerLeavesBlot_ReturnsTrue()
    {
        _sut.Matches(
                Prior(1),
                After(decisionMakerCount: 2),
                After(decisionMakerCount: 1))
            .Should().BeTrue();
    }

    [Fact]
    public void BestStacksFriendlyBlot_PlayerMovesBlotAway_ReturnsTrue()
    {
        _sut.Matches(
                Prior(1),
                After(decisionMakerCount: 2),
                After(decisionMakerCount: 0))
            .Should().BeTrue();
    }

    [Fact]
    public void BestHitsOpponentBlotAndMakes_PlayerIgnores_ReturnsTrue()
    {
        // Prior: opp blot at decision-maker's 20-point.
        // Best: decision-maker hits and makes (opp checker goes to bar,
        //       not on the 20-point in the after-board).
        // Player: ignores — the opp blot is still there in the post-play
        //       board, recorded as +1 at index 5 from the new POV.
        _sut.Matches(
                Prior(-1),
                After(decisionMakerCount: 2),
                After(decisionMakerCount: 0, opponentCount: 1))
            .Should().BeTrue();
    }

    [Fact]
    public void SingleFriendlyCheckerDoesNotCountAsMade_ReturnsTrue()
    {
        // Best leaves a blot (1 friendly); player makes (2 friendly).
        // Guards against a bug that treats a blot as "made".
        _sut.Matches(
                Prior(0),
                After(decisionMakerCount: 1),
                After(decisionMakerCount: 2))
            .Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Negative — XOR false, or 20-point already made before play
    // -----------------------------------------------------------------------

    [Fact]
    public void NeitherMakes_ReturnsFalse()
    {
        _sut.Matches(
                Prior(0),
                After(decisionMakerCount: 0),
                After(decisionMakerCount: 0))
            .Should().BeFalse();
    }

    [Fact]
    public void BothMake_ReturnsFalse()
    {
        _sut.Matches(
                Prior(0),
                After(decisionMakerCount: 2),
                After(decisionMakerCount: 2))
            .Should().BeFalse();
    }

    [Fact]
    public void AlreadyMade_BothHold_ReturnsFalse()
    {
        _sut.Matches(
                Prior(3),
                After(decisionMakerCount: 3),
                After(decisionMakerCount: 2))
            .Should().BeFalse();
    }

    [Fact]
    public void AlreadyMade_OnePlayBreaks_ReturnsFalse()
    {
        // Before guard short-circuits regardless of post-play state:
        // breaking a point is not a "make" event.
        _sut.Matches(
                Prior(2),
                After(decisionMakerCount: 1),
                After(decisionMakerCount: 2))
            .Should().BeFalse();
    }
}
