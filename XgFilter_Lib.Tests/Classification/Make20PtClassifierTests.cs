using XgFilter_Lib.Classification;

namespace XgFilter_Lib.Tests.Classification;

public class Make20PtClassifierTests
{
    private static readonly Make20PtClassifier _sut = new();

    // The classifier reads only board[20]. Other indices are left at 0 —
    // semantics isolate to that single point.
    private static int[] BoardAt20(int count)
    {
        var b = new int[26];
        b[20] = count;
        return b;
    }

    // -----------------------------------------------------------------------
    //  Positive — XOR true, 20-point not already made
    // -----------------------------------------------------------------------

    [Fact]
    public void BestMakesFromEmpty_UserLeavesEmpty_ReturnsTrue()
    {
        _sut.Matches(BoardAt20(0), BoardAt20(2), BoardAt20(0))
            .Should().BeTrue();
    }

    [Fact]
    public void UserMakesFromEmpty_BestLeavesEmpty_ReturnsTrue()
    {
        _sut.Matches(BoardAt20(0), BoardAt20(0), BoardAt20(2))
            .Should().BeTrue();
    }

    [Fact]
    public void BestStacksFriendlyBlot_UserLeavesBlot_ReturnsTrue()
    {
        _sut.Matches(BoardAt20(1), BoardAt20(2), BoardAt20(1))
            .Should().BeTrue();
    }

    [Fact]
    public void BestStacksFriendlyBlot_UserMovesBlotAway_ReturnsTrue()
    {
        _sut.Matches(BoardAt20(1), BoardAt20(2), BoardAt20(0))
            .Should().BeTrue();
    }

    [Fact]
    public void BestHitsOpponentBlotAndMakes_UserIgnores_ReturnsTrue()
    {
        _sut.Matches(BoardAt20(-1), BoardAt20(2), BoardAt20(-1))
            .Should().BeTrue();
    }

    [Fact]
    public void SingleFriendlyCheckerDoesNotCountAsMade_ReturnsTrue()
    {
        // best leaves a blot (1); user makes (2). Only user makes → XOR true.
        // Guards against a bug that treats blot-as-made.
        _sut.Matches(BoardAt20(0), BoardAt20(1), BoardAt20(2))
            .Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Negative — XOR false, or 20-point already made before play
    // -----------------------------------------------------------------------

    [Fact]
    public void NeitherMakes_ReturnsFalse()
    {
        _sut.Matches(BoardAt20(0), BoardAt20(0), BoardAt20(0))
            .Should().BeFalse();
    }

    [Fact]
    public void BothMake_ReturnsFalse()
    {
        _sut.Matches(BoardAt20(0), BoardAt20(2), BoardAt20(2))
            .Should().BeFalse();
    }

    [Fact]
    public void AlreadyMade_BothHold_ReturnsFalse()
    {
        _sut.Matches(BoardAt20(3), BoardAt20(3), BoardAt20(2))
            .Should().BeFalse();
    }

    [Fact]
    public void AlreadyMade_OnePlayBreaks_ReturnsFalse()
    {
        // Before guard short-circuits regardless of post-play state:
        // breaking a point is not a "make" event.
        _sut.Matches(BoardAt20(2), BoardAt20(1), BoardAt20(2))
            .Should().BeFalse();
    }
}
