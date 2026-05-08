using XgFilter_Lib.Classification;

namespace XgFilter_Lib.Tests.Classification;

public class VsTwoPlusUpClassifierTests
{
    private static readonly VsTwoPlusUpClassifier _sut = new();

    private static int[] MakeBoard(params (int index, int count)[] points)
    {
        var board = new int[26];
        foreach (var (index, count) in points)
            board[index] = count;
        return board;
    }

    [Fact]
    public void Matches_OpponentHasTwoOnBar_ReturnsTrue()
    {
        var board = MakeBoard(
            (0, -2),
            (24, 2), (13, 5), (8, 3), (6, 5),
            (12, -5), (17, -3), (19, -3));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void Matches_OpponentHasThreeOnBar_ReturnsTrue()
    {
        var board = MakeBoard(
            (0, -3),
            (24, 2), (13, 5), (8, 3), (6, 5),
            (12, -5), (17, -3), (19, -2));
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void NoMatch_OpponentHasOneOnBar_ReturnsFalse()
    {
        var board = MakeBoard(
            (0, -1),
            (24, 2), (13, 5), (8, 3), (6, 5),
            (12, -5), (17, -3), (19, -4));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_NoOneOnBar_ReturnsFalse()
    {
        var board = MakeBoard(
            (24, 2), (13, 5), (8, 3), (6, 5),
            (1, -2), (12, -5), (17, -3), (19, -5));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OnlyPlayerOnBar_ReturnsFalse()
    {
        // board[0] is opponent's bar; board[25] is player's bar.
        // Player on the bar must not trip the classifier.
        var board = MakeBoard(
            (25, 3),
            (13, 5), (8, 3), (6, 4),
            (1, -2), (12, -5), (17, -3), (19, -5));
        _sut.Matches(board).Should().BeFalse();
    }

    [Fact]
    public void Matches_BothSidesOnBar_ReturnsTrue()
    {
        // Classifier is purely about the opponent's bar count; player on
        // the bar simultaneously is irrelevant.
        var board = MakeBoard(
            (0, -2), (25, 1),
            (13, 5), (8, 3), (6, 4),
            (12, -5), (17, -3), (19, -3));
        _sut.Matches(board).Should().BeTrue();
    }
}
