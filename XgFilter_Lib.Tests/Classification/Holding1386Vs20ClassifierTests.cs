using BgDataTypes_Lib;
using XgFilter_Lib.Classification;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Classification;

public class Holding1386Vs20ClassifierTests
{
    private static readonly Holding1386Vs20Classifier _sut = new();

    /// <summary>
    /// Canonical 20-point holding game from the on-roll player's perspective
    /// (15 vs 15): player has made the 6/8/13 points, the opponent holds the
    /// 20-point anchor (board[5]) and the player's 12 point, points 7/9/10/11
    /// are empty, nothing on-roll sits above the 13, and the opponent has no
    /// checkers in the player's home or on the bar. Negative tests restate
    /// this position with exactly one clause falsified (<see cref="HoldingWith"/>).
    /// </summary>
    private static readonly (int Index, int Count)[] Canonical =
    [
        (13, 4), (8, 3), (6, 4), (4, 2), (2, 2),          // on-roll player (15)
        (5, -2), (12, -3), (19, -4), (21, -4), (23, -2),  // opponent (-15)
    ];

    private static BoardPosition CanonicalHolding() => BoardBuilder.Build(Canonical);

    /// <summary>
    /// The canonical holding with each of <paramref name="changes"/> setting
    /// one slot's count. A change that adds checkers takes them from a slot
    /// the predicate does not read (the player's 2-point, the opponent's 23),
    /// so the result is a position — never more than fifteen a side.
    /// </summary>
    private static BoardPosition HoldingWith(params (int Index, int Count)[] changes)
    {
        var counts = Canonical.ToDictionary(p => p.Index, p => p.Count);
        foreach (var (index, count) in changes)
            counts[index] = count;
        return BoardBuilder.Build([.. counts.Select(p => (p.Key, p.Value))]);
    }

    [Fact]
    public void Matches_CanonicalHolding_ReturnsTrue()
    {
        _sut.Matches(CanonicalHolding()).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Each clause falsified individually — one negative per predicate term
    // -----------------------------------------------------------------------

    [Fact]
    public void NoMatch_OpponentNotAnchoredOn20_ReturnsFalse()
    {
        // board[5] == -1 → opponent has not anchored the 20 point.
        _sut.Matches(HoldingWith((5, -1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerMissing6Point_ReturnsFalse()
    {
        _sut.Matches(HoldingWith((6, 1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerMissing8Point_ReturnsFalse()
    {
        _sut.Matches(HoldingWith((8, 1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_PlayerMissing13Point_ReturnsFalse()
    {
        _sut.Matches(HoldingWith((13, 1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OpponentMissing12Point_ReturnsFalse()
    {
        // board[12] == -1 → opponent no longer holds the player's 12.
        _sut.Matches(HoldingWith((12, -1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_RequiredEmptyPointOccupied_ReturnsFalse()
    {
        // Any of 7/9/10/11 occupied breaks the structure; exercise the 11.
        _sut.Matches(HoldingWith((11, 2), (2, 0))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OpponentCheckerInPlayerHome_ReturnsFalse()
    {
        // An opponent checker on indices 0-4 (here the 3 point) is disallowed.
        _sut.Matches(HoldingWith((3, -2), (23, 0))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OpponentOnBar_ReturnsFalse()
    {
        // board[0] < 0 → opponent has a checker on the bar.
        _sut.Matches(HoldingWith((0, -1), (23, -1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OnRollCheckerAbove13_ReturnsFalse()
    {
        // A player checker anywhere in 14-25 (here the 20) breaks the wall.
        _sut.Matches(HoldingWith((20, 2), (2, 0))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_OnRollCheckerOnBar_ReturnsFalse()
    {
        // board[25] > 0 → on-roll player has a checker on the bar.
        _sut.Matches(HoldingWith((25, 1), (2, 1))).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Boundaries — each "made"/"anchor" threshold at exactly the cutoff
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_AllThresholdsExactlyAtCutoff_ReturnsTrue()
    {
        // board[5] == -2 and board[12] == -2 (anchors exactly two); board[6/8/13]
        // == 2 (held exactly two). All inclusive bounds → still a match.
        var board = BoardBuilder.Build(
            (13, 2), (8, 2), (6, 2), (4, 2), (2, 7),          // on-roll player (15)
            (5, -2), (12, -2), (19, -4), (21, -4), (23, -3)); // opponent (-15)
        _sut.Matches(board).Should().BeTrue();
    }

    [Fact]
    public void NoMatch_6PointHasOnlyOne_ReturnsFalse()
    {
        // One short of the made-point cutoff.
        _sut.Matches(HoldingWith((6, 1))).Should().BeFalse();
    }

    [Fact]
    public void NoMatch_AnchorHasOnlyOne_ReturnsFalse()
    {
        // board[5] == -1 → opponent's 20 anchor is one short.
        _sut.Matches(HoldingWith((5, -1))).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Reported XGID regression: -a--BBC-C--BcB---d-bbc--A-
    // -----------------------------------------------------------------------

    [Fact]
    public void NoMatch_ReportedXgidIsNotAHoldingGame_ReturnsFalse()
    {
        // The XGID -a--BBC-C--BcB---d-bbc--A- decodes (on-roll POV) to a board
        // whose -2 anchor sits at board[20], not board[5]; board[5] is +2, the
        // 11 point is occupied, and an on-roll checker sits on the 24. It is
        // NOT a 20-point holding game per the spec and must not match — this
        // guards against the prior board[20] perspective bug.
        var board = BoardBuilder.Build(
            (4, 2), (5, 2), (6, 3), (8, 3), (11, 2), (13, 2), (24, 1),
            (1, -1), (12, -3), (17, -4), (19, -2), (20, -2), (21, -3));
        _sut.Matches(board).Should().BeFalse();
    }
}
