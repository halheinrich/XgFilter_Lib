using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Patterns;

/// <summary>
/// The span constraint <c>[a-b,min,max]</c> (halheinrich/backgammon#268): one
/// side's total checkers across a run of board indices, the side named by the
/// sign of the bounds, the other side's checkers ignored.
/// </summary>
public class CheckerSpanRangeTests
{
    private static bool Satisfies(CheckerSpanRange range, int[] board) =>
        ((IPatternConstraint)range).IsSatisfiedBy(board);

    // -----------------------------------------------------------------------
    //  Construction — one side per span, each side's bounds in its interval
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(3, null)]      // player side
    [InlineData(0, 15)]        // player side, full interval
    [InlineData(null, -2)]     // opponent side
    [InlineData(-15, 0)]       // opponent side, full interval
    [InlineData(0, 0)]         // both sides: empty
    [InlineData(null, 0)]      // player has none
    [InlineData(0, null)]      // opponent has none
    [InlineData(null, null)]   // no constraint
    [InlineData(15, 15)]
    [InlineData(-15, -15)]
    public void Ctor_OneSidedOrZeroBounds_Construct(int? min, int? max)
    {
        var act = () => new CheckerSpanRange(7, 12, min, max);
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-2, 3)]
    [InlineData(-1, 1)]
    [InlineData(-15, 15)]
    public void Ctor_OppositeSignedBounds_Throws(int min, int max)
    {
        // A span constrains one side; one positive and one negative bound name
        // both, and are refused rather than read as a net count.
        var act = () => new CheckerSpanRange(7, 12, min, max);

        act.Should().Throw<ArgumentException>()
            .Which.Should().NotBeOfType<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(16, null)]
    [InlineData(null, 16)]
    [InlineData(-16, null)]
    [InlineData(null, -16)]
    public void Ctor_BoundBeyondFifteen_Throws(int? min, int? max)
    {
        var act = () => new CheckerSpanRange(7, 12, min, max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(5, 3)]
    [InlineData(-2, -5)]
    public void Ctor_MinGreaterThanMax_Throws(int min, int max)
    {
        var act = () => new CheckerSpanRange(7, 12, min, max);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(12, 7)]
    [InlineData(7, 7)]
    public void Ctor_InvalidSpan_Throws(int first, int last)
    {
        var act = () => new CheckerSpanRange(first, last, 1, null);
        act.Should().Throw<ArgumentException>();
    }

    // CheckerSides is internal, so the expectation travels as its underlying int.
    [Theory]
    [InlineData(3, null, (int)CheckerSides.Player)]
    [InlineData(0, 3, (int)CheckerSides.Player)]
    [InlineData(null, -2, (int)CheckerSides.Opponent)]
    [InlineData(-3, 0, (int)CheckerSides.Opponent)]
    [InlineData(0, 0, (int)CheckerSides.Both)]
    [InlineData(null, 0, (int)CheckerSides.Both)]
    [InlineData(0, null, (int)CheckerSides.Both)]
    [InlineData(null, null, (int)CheckerSides.Both)]
    public void ConstrainedSides_FollowTheSignOfTheBounds(int? min, int? max, int expected)
    {
        new CheckerSpanRange(7, 12, min, max).ConstrainedSides.Should().Be((CheckerSides)expected);
    }

    [Fact]
    public void Default_IsTheValidUnconstrainingSpan()
    {
        var range = default(CheckerSpanRange);

        range.Should().Be(new CheckerSpanRange(0, 1, null, null));
        Satisfies(range, BoardBuilder.Build((0, -3), (1, 4))).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Totals — per side, opposing checkers ignored
    // -----------------------------------------------------------------------

    [Fact]
    public void PlayerSide_TotalsTheSpan_IgnoringTheOpponent()
    {
        // [7-12,3,]: P over 7..12 is 1 + 2 = 3; the opponent's 5 there are ignored.
        var range = new CheckerSpanRange(7, 12, 3, null);

        Satisfies(range, BoardBuilder.Build((7, 1), (9, 2), (10, -5))).Should().BeTrue();
        Satisfies(range, BoardBuilder.Build((7, 1), (9, 1), (10, -5))).Should().BeFalse();
        // Checkers just outside the span do not count.
        Satisfies(range, BoardBuilder.Build((6, 5), (13, 5), (9, 2))).Should().BeFalse();
    }

    [Fact]
    public void OpponentSide_TotalsTheSpan_IgnoringThePlayer()
    {
        // [13-18,,-2]: the opponent has two or more across 13..18.
        var range = new CheckerSpanRange(13, 18, null, -2);

        Satisfies(range, BoardBuilder.Build((13, -1), (18, -1), (15, 6))).Should().BeTrue();
        Satisfies(range, BoardBuilder.Build((13, -1), (15, 6))).Should().BeFalse();
        Satisfies(range, BoardBuilder.Build((12, -4), (19, -4))).Should().BeFalse();
    }

    [Fact]
    public void ZeroBoundForms_EachReadsItsOwnSides()
    {
        var bothSides = BoardBuilder.Build((8, 2), (9, -1));
        var playerOnly = BoardBuilder.Build((8, 2));
        var opponentOnly = BoardBuilder.Build((9, -1));
        var empty = BoardBuilder.Build((6, 5), (13, -5));

        // [7-12,0,0]: empty of both sides.
        var emptyOfBoth = new CheckerSpanRange(7, 12, 0, 0);
        Satisfies(emptyOfBoth, empty).Should().BeTrue();
        Satisfies(emptyOfBoth, playerOnly).Should().BeFalse();
        Satisfies(emptyOfBoth, opponentOnly).Should().BeFalse();

        // [7-12,,0]: the on-roll player has none; the opponent is ignored.
        var noPlayer = new CheckerSpanRange(7, 12, null, 0);
        Satisfies(noPlayer, opponentOnly).Should().BeTrue();
        Satisfies(noPlayer, empty).Should().BeTrue();
        Satisfies(noPlayer, bothSides).Should().BeFalse();

        // [7-12,0,]: the opponent has none; the on-roll player is ignored.
        var noOpponent = new CheckerSpanRange(7, 12, 0, null);
        Satisfies(noOpponent, playerOnly).Should().BeTrue();
        Satisfies(noOpponent, empty).Should().BeTrue();
        Satisfies(noOpponent, bothSides).Should().BeFalse();

        // [7-12,,]: no constraint.
        var unconstrained = new CheckerSpanRange(7, 12, null, null);
        Satisfies(unconstrained, bothSides).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Bars — a span may include either bar with either sign
    // -----------------------------------------------------------------------

    [Fact]
    public void SpanOverPlayersBar_OpponentSide_CountsTheOpponentOnly()
    {
        // Hal's example: [24-25,-2,-2] — exactly two of the opponent's checkers
        // across 24 and 25. The player's bar holds only the player, whose
        // checkers there are ignored.
        var range = new CheckerSpanRange(24, 25, -2, -2);

        Satisfies(range, BoardBuilder.Build((24, -2), (25, 3))).Should().BeTrue();
        Satisfies(range, BoardBuilder.Build((24, -2))).Should().BeTrue();
        Satisfies(range, BoardBuilder.Build((24, -1), (25, 3))).Should().BeFalse();
        Satisfies(range, BoardBuilder.Build((24, -3))).Should().BeFalse();
    }

    [Fact]
    public void SpanOverPlayersBar_PlayerSide_CountsTheBar()
    {
        var range = new CheckerSpanRange(24, 25, 2, null);

        Satisfies(range, BoardBuilder.Build((24, 1), (25, 1))).Should().BeTrue();
        Satisfies(range, BoardBuilder.Build((25, 2))).Should().BeTrue();
        Satisfies(range, BoardBuilder.Build((25, 1))).Should().BeFalse();
    }

    [Fact]
    public void SpanOverOpponentsBar_BothSigns()
    {
        // Opponent side counts its bar...
        var opponent = new CheckerSpanRange(0, 1, null, -3);
        Satisfies(opponent, BoardBuilder.Build((0, -2), (1, -1))).Should().BeTrue();
        Satisfies(opponent, BoardBuilder.Build((0, -2))).Should().BeFalse();

        // ...and the player side is untouched by it.
        var player = new CheckerSpanRange(0, 2, 1, null);
        Satisfies(player, BoardBuilder.Build((1, 2), (0, -5))).Should().BeTrue();
        Satisfies(player, BoardBuilder.Build((0, -3))).Should().BeFalse();
    }

    [Fact]
    public void WholeBoardSpan_CountsEveryIndex()
    {
        // [0-25,15,15]: all fifteen of the player's checkers are on the board,
        // bar included; [0-25,,-15] likewise for the opponent.
        var board = BoardBuilder.Build((25, 1), (6, 5), (13, 9), (0, -2), (19, -13));

        Satisfies(new CheckerSpanRange(0, 25, 15, 15), board).Should().BeTrue();
        Satisfies(new CheckerSpanRange(0, 25, -15, -15), board).Should().BeTrue();
        Satisfies(new CheckerSpanRange(1, 24, 15, 15), board).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  The definition of P and O, swept — both sides inside the span
    // -----------------------------------------------------------------------

    private static readonly (int First, int Last)[] _spans =
        [(0, 25), (0, 1), (24, 25), (1, 6), (7, 12), (13, 18), (5, 8), (19, 25)];

    private static readonly int?[] _bounds =
        [null, -6, -5, -4, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6];

    /// <summary>
    /// Boards with both sides scattered over every index — so every span of the
    /// sweep holds checkers of both signs on most boards.
    /// </summary>
    private static IEnumerable<int[]> MixedBoards()
    {
        var rng = new Random(20260923);
        for (int n = 0; n < 400; n++)
        {
            var board = new int[26];
            for (int i = 0; i < 26; i++)
                board[i] = rng.Next(-3, 4);
            yield return board;
        }
    }

    /// <summary>
    /// The brief's rule 4 written out case by case, independently of the
    /// library's side-interval formulation.
    /// </summary>
    private static bool ByDefinition(int[] board, int first, int last, int? min, int? max)
    {
        int p = 0, o = 0;
        for (int i = first; i <= last; i++)
        {
            if (board[i] > 0) p += board[i];
            else o -= board[i];
        }

        bool Within(int value) => (min ?? int.MinValue) <= value && value <= (max ?? int.MaxValue);

        if (min > 0 || max > 0) return Within(p);
        if (min < 0 || max < 0) return Within(-o);
        return (min, max) switch
        {
            (0, 0) => p == 0 && o == 0,
            (null, 0) => p == 0,
            (0, null) => o == 0,
            _ => true,
        };
    }

    private static IEnumerable<(int? Min, int? Max)> ValidBoundPairs() =>
        from min in _bounds
        from max in _bounds
        where !(min < 0 && max > 0) && !(min > 0 && max < 0)
        where !(min is { } l && max is { } h && l > h)
        select (min, max);

    [Fact]
    public void Totals_AgreeWithTheDefinitionOfPAndO()
    {
        var boards = MixedBoards().ToArray();
        int bothSidesPresent = 0;

        foreach (var (first, last) in _spans)
            foreach (var (min, max) in ValidBoundPairs())
            {
                var range = new CheckerSpanRange(first, last, min, max);
                foreach (var board in boards)
                {
                    Satisfies(range, board).Should().Be(
                        ByDefinition(board, first, last, min, max),
                        "[{0}-{1},{2},{3}] on board [{4}]", first, last, min, max, string.Join(",", board));
                }
            }

        foreach (var board in boards)
            if (board[7..13].Any(v => v > 0) && board[7..13].Any(v => v < 0))
                bothSidesPresent++;
        bothSidesPresent.Should().BeGreaterThan(300, "the sweep must exercise spans holding both sides");
    }

    [Fact]
    public void AddingOpposingCheckers_NeverChangesASignedSpansVerdict()
    {
        // The pin against a net count: for a span constraining one side, piling
        // the other side's checkers into the span (onto indices that side can
        // hold) leaves the verdict alone.
        var rng = new Random(268);
        var boards = MixedBoards().ToArray();

        foreach (var (first, last) in _spans)
            foreach (var (min, max) in ValidBoundPairs())
            {
                bool player = min > 0 || max > 0;
                bool opponent = min < 0 || max < 0;
                if (!player && !opponent)
                    continue;

                var range = new CheckerSpanRange(first, last, min, max);
                foreach (var board in boards)
                {
                    var added = (int[])board.Clone();
                    for (int i = first; i <= last; i++)
                    {
                        if (player && i != 25 && added[i] <= 0)
                            added[i] -= rng.Next(1, 4);
                        if (opponent && i != 0 && added[i] >= 0)
                            added[i] += rng.Next(1, 4);
                    }

                    Satisfies(range, added).Should().Be(
                        Satisfies(range, board),
                        "[{0}-{1},{2},{3}] must ignore the opposing side", first, last, min, max);
                }
            }
    }

    // -----------------------------------------------------------------------
    //  Equality and rendering
    // -----------------------------------------------------------------------

    [Fact]
    public void Equality_IsStructural()
    {
        new CheckerSpanRange(7, 12, 3, null).Should().Be(new CheckerSpanRange(new CheckerSpan(7, 12), 3, null));
        new CheckerSpanRange(7, 12, 3, null).Should().NotBe(new CheckerSpanRange(7, 12, 3, 5));
        new CheckerSpanRange(7, 12, 3, null).Should().NotBe(new CheckerSpanRange(7, 11, 3, null));
    }

    [Theory]
    [InlineData(7, 12, 3, null, "[7-12,3,]")]
    [InlineData(13, 18, null, -2, "[13-18,,-2]")]
    [InlineData(24, 25, -2, -2, "[24-25,-2,-2]")]
    [InlineData(0, 25, 0, 0, "[0-25,0,0]")]
    [InlineData(1, 6, null, null, "[1-6,,]")]
    public void ToString_RendersBracketToken(int first, int last, int? min, int? max, string expected)
    {
        new CheckerSpanRange(first, last, min, max).ToString().Should().Be(expected);
    }
}
