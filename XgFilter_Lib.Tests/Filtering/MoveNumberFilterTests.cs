using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class MoveNumberFilterTests
{
    // -----------------------------------------------------------------------
    //  Matches — bounded range, both substrates via RowShape
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenInRangeAndStandardStart_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 5, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenBelowMin_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 2, IsStandardStart: true),
            expected: false);
    }

    [Fact]
    public void Matches_WhenAboveMax_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 11, IsStandardStart: true),
            expected: false);
    }

    [Fact]
    public void Matches_WhenAtMinBoundary_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 3, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenAtMaxBoundary_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 10, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenInRangeButNonStandardStart_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 5, IsStandardStart: false),
            expected: false);
    }

    [Fact]
    public void Matches_WhenNoBoundsSet_AcceptsAnyStandardStart()
    {
        var filter = new MoveNumberFilter(min: null, max: null);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 42, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsLowMoveNumber()
    {
        var filter = new MoveNumberFilter(min: null, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 1, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsHighMoveNumber()
    {
        var filter = new MoveNumberFilter(min: 3, max: null);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 99, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_RejectsNonStandardStart()
    {
        var filter = new MoveNumberFilter(min: null, max: 10);
        AssertMatchesBoth(
            filter,
            new RowShape(MoveNumber: 1, IsStandardStart: false),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame — IGameInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_StandardStart_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var game = new FakeGameInfo { IsStandardStart = true };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_NonStandardStart_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var game = new FakeGameInfo { IsStandardStart = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_AlwaysReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var match = new FakeMatchInfo { Player1 = "A", Player2 = "B", MatchLength = 7 };

        filter.ShouldSkipMatch(match).Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  IDecisionFilter: ShouldAdvanceGame — mid-stream, exercised on both
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldAdvanceGame_AtMax_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        AssertShouldAdvanceGameBoth(
            filter,
            new RowShape(MoveNumber: 5, IsStandardStart: true),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceGame_OnePastMax_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        AssertShouldAdvanceGameBoth(
            filter,
            new RowShape(MoveNumber: 6, IsStandardStart: true),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceGame_NullMax_AlwaysReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: null);
        AssertShouldAdvanceGameBoth(
            filter,
            new RowShape(MoveNumber: 9999, IsStandardStart: true),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  The bound rule, stated once here and asked twice — the constructor
    //  enforces it and FilterConfig.GetInvalidFields reports it. These tests
    //  pin the predicates directly; FilterConfigTests pins the reporting and
    //  the Build path that reaches this constructor.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]           // absent: the rule constrains values, never presence
    [InlineData(1)]              // the first move of a game — the admissible edge
    [InlineData(2)]
    [InlineData(int.MaxValue)]
    public void IsBoundAtLeastOne_AdmissibleBound_ReturnsTrue(int? bound) =>
        MoveNumberFilter.IsBoundAtLeastOne(bound).Should().BeTrue();

    [Theory]
    [InlineData(0)]              // one below the floor: names no decision
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void IsBoundAtLeastOne_InadmissibleBound_ReturnsFalse(int? bound) =>
        MoveNumberFilter.IsBoundAtLeastOne(bound).Should().BeFalse();

    [Theory]
    [InlineData(null, null)]     // both absent
    [InlineData(3, null)]        // one-sided
    [InlineData(null, 3)]        // one-sided
    [InlineData(3, 10)]
    [InlineData(5, 5)]           // equal: the inclusive bounds make this a single-move filter
    public void AreBoundsOrdered_OrderedPair_ReturnsTrue(int? min, int? max) =>
        MoveNumberFilter.AreBoundsOrdered(min, max).Should().BeTrue();

    [Theory]
    [InlineData(10, 3)]
    [InlineData(6, 5)]
    public void AreBoundsOrdered_MinExceedsMax_ReturnsFalse(int? min, int? max) =>
        MoveNumberFilter.AreBoundsOrdered(min, max).Should().BeFalse();

    [Fact]
    public void Constructor_SubFloorMin_Throws()
    {
        // Zero is the interesting case, not merely the negative one: it is what
        // a spinner lands on first, and as a lower bound it restates the open
        // end instead of filtering.
        var act = () => new MoveNumberFilter(min: 0, max: 10);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void Constructor_SubFloorMax_Throws()
    {
        // The sharper case: move numbers start at one, so an upper bound below
        // one admits nothing at all rather than merely being redundant.
        var act = () => new MoveNumberFilter(min: null, max: 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("max");
    }

    [Fact]
    public void Constructor_NegativeBound_Throws()
    {
        var minIsNegative = () => new MoveNumberFilter(min: -1);
        var maxIsNegative = () => new MoveNumberFilter(max: -1);

        minIsNegative.Should().Throw<ArgumentOutOfRangeException>()
                     .Which.ParamName.Should().Be("min");
        maxIsNegative.Should().Throw<ArgumentOutOfRangeException>()
                     .Which.ParamName.Should().Be("max");
    }

    [Fact]
    public void Constructor_MinExceedsMax_Throws()
    {
        // Both bounds are individually admissible; the pair is not. Before this
        // rule the range was accepted and silently matched nothing
        // (halheinrich/backgammon#119).
        var act = () => new MoveNumberFilter(min: 10, max: 3);

        act.Should().Throw<ArgumentException>().WithMessage("*empty move-number range*");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(1, null)]
    [InlineData(null, 1)]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(1, int.MaxValue)]
    public void Constructor_AdmissibleBounds_DoesNotThrow(int? min, int? max)
    {
        var act = () => new MoveNumberFilter(min, max);

        act.Should().NotThrow();
    }

    [Fact]
    public void Matches_FirstMoveOnlyRange_AdmitsExactlyMoveOne()
    {
        // The reason one is admissible rather than treated as "no bound":
        // [1, 1] is the opening-decision filter, and it is not empty.
        var filter = new MoveNumberFilter(min: 1, max: 1);

        AssertMatchesBoth(
            filter, new RowShape(MoveNumber: 1, IsStandardStart: true), expected: true);
        AssertMatchesBoth(
            filter, new RowShape(MoveNumber: 2, IsStandardStart: true), expected: false);
    }
}
