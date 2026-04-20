using XgFilter_Lib.Classification;

namespace XgFilter_Lib.Tests.Classification;

public class ContactClassifierTests
{
    private static readonly ContactClassifier _sut = new();

    private static int[] MakeBoard(params (int index, int count)[] points)
    {
        var board = new int[26];
        foreach (var (index, count) in points)
            board[index] = count;
        return board;
    }

    [Fact]
    public void Contact_StartingPosition_ReturnsTrue()
    {
        var board = MakeBoard(
            (24, 2), (13, 5), (8, 3), (6, 5),
            (1, -2), (12, -5), (17, -3), (19, -5));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Contact_PlayerAndOpponentIntermixed_ReturnsTrue()
    {
        var board = MakeBoard((15, 2), (14, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Contact_PlayerOnBar_ReturnsTrue()
    {
        var board = MakeBoard((25, 1), (6, 14), (19, -15));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Contact_OpponentOnBar_ReturnsTrue()
    {
        var board = MakeBoard((6, 15), (0, -1), (19, -14));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Race_BothPlayersBearingOff_ReturnsFalse()
    {
        var board = MakeBoard((3, 2), (2, 3), (22, -2), (23, -3));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Race_PlayerLastOnSix_OpponentFirstOnSeven_ReturnsFalse()
    {
        var board = MakeBoard((6, 5), (7, -5));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Race_AdjacentNoOverlap_ReturnsFalse()
    {
        var board = MakeBoard((10, 1), (11, -1));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void ContactAndRace_AreComplementary()
    {
        var race = new RaceClassifier();
        // Sample of mixed positions
        var boards = new[]
        {
            MakeBoard((24, 2), (13, 5), (8, 3), (6, 5),
                      (1, -2), (12, -5), (17, -3), (19, -5)),
            MakeBoard((3, 2), (2, 3), (22, -2), (23, -3)),
            MakeBoard((15, 2), (14, -2)),
            MakeBoard((10, 1), (11, -1)),
            MakeBoard((8, 1), (7, -1)),
        };

        foreach (var board in boards)
            _sut.Matches(board).Should().Be(!race.Matches(board));
    }
}
