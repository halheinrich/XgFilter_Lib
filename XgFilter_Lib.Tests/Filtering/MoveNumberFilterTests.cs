using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class MoveNumberFilterTests
{
    /// <summary>
    /// A checker play at move <paramref name="moveNumber"/> of game 1 of a
    /// match, in a game that did or did not start from the standard position.
    /// </summary>
    private static CheckerPlayDecision AtMove(int moveNumber, bool isStandardStart = true) =>
        TestRecords.CheckerPlay(
            id: new XgDecisionId("match.xg", Game: 1, MoveNumber: moveNumber, IsCube: false),
            descriptive: TestRecords.Descriptive(isStandardStart: isStandardStart));

    /// <summary>
    /// A checker play in a standalone position (an <c>.xgp</c> file): it
    /// belongs to no game, so its move number and standard start are "not
    /// applicable" — both null.
    /// </summary>
    private static CheckerPlayDecision Standalone() =>
        TestRecords.CheckerPlay(id: new XgpDecisionId("position.xgp"));

    // -----------------------------------------------------------------------
    //  Matches — bounded range, both substrates of one record
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenInRangeAndStandardStart_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(5),
            expected: true);
    }

    [Fact]
    public void Matches_WhenBelowMin_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(2),
            expected: false);
    }

    [Fact]
    public void Matches_WhenAboveMax_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(11),
            expected: false);
    }

    [Fact]
    public void Matches_WhenAtMinBoundary_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(3),
            expected: true);
    }

    [Fact]
    public void Matches_WhenAtMaxBoundary_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(10),
            expected: true);
    }

    [Fact]
    public void Matches_WhenInRangeButNonStandardStart_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(5, isStandardStart: false),
            expected: false);
    }

    [Fact]
    public void Matches_WhenNoBoundsSet_AcceptsAnyStandardStart()
    {
        var filter = new MoveNumberFilter(min: null, max: null);
        AssertMatchesBoth(
            filter,
            AtMove(42),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsLowMoveNumber()
    {
        var filter = new MoveNumberFilter(min: null, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(1),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsHighMoveNumber()
    {
        var filter = new MoveNumberFilter(min: 3, max: null);
        AssertMatchesBoth(
            filter,
            AtMove(99),
            expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_RejectsNonStandardStart()
    {
        var filter = new MoveNumberFilter(min: null, max: 10);
        AssertMatchesBoth(
            filter,
            AtMove(1, isStandardStart: false),
            expected: false);
    }

    // -----------------------------------------------------------------------
    //  A standalone position (halheinrich/backgammon#124): no game, no move
    //  number, no standard start — "not applicable", never a stamped value.
    //  The filter decides explicitly: such a position never passes.
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_StandalonePosition_NeverPasses()
    {
        // Whatever the bounds — even [1, 1], which a stamped move 1 would
        // satisfy — a position that belongs to no game has no move to bound.
        foreach (var filter in new[]
        {
            new MoveNumberFilter(min: 1, max: 1),
            new MoveNumberFilter(min: 1, max: null),
            new MoveNumberFilter(min: null, max: int.MaxValue),
        })
        {
            AssertMatchesBoth(filter, Standalone(), expected: false);
        }
    }

    [Fact]
    public void ShouldAdvanceGame_StandalonePosition_ReturnsFalse()
    {
        // No move number, so never "past max": a standalone position belongs to
        // no game and has nothing to advance past.
        AssertShouldAdvanceGameBoth(new MoveNumberFilter(min: 1, max: 1), Standalone(), expected: false);
    }

    // -----------------------------------------------------------------------
    //  IMatchFilter: ShouldSkipGame — IGameInfo input, no substrate axis
    // -----------------------------------------------------------------------

    [Fact]
    public void ShouldSkipGame_StandardStart_ReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var game = FakeGameInfo.Match(3, 5) with { IsStandardStart = true };

        filter.ShouldSkipGame(game).Should().BeFalse();
    }

    [Fact]
    public void ShouldSkipGame_NonStandardStart_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        var game = FakeGameInfo.Money() with { IsStandardStart = false };

        filter.ShouldSkipGame(game).Should().BeTrue();
    }

    [Fact]
    public void ShouldSkipMatch_AlwaysReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);

        filter.ShouldSkipMatch(FakeMatchInfo.Match(7)).Should().BeFalse();
        filter.ShouldSkipMatch(FakeMatchInfo.Money(isJacoby: true)).Should().BeFalse();
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
            AtMove(5),
            expected: false);
    }

    [Fact]
    public void ShouldAdvanceGame_OnePastMax_ReturnsTrue()
    {
        var filter = new MoveNumberFilter(min: 1, max: 5);
        AssertShouldAdvanceGameBoth(
            filter,
            AtMove(6),
            expected: true);
    }

    [Fact]
    public void ShouldAdvanceGame_NullMax_AlwaysReturnsFalse()
    {
        var filter = new MoveNumberFilter(min: 3, max: null);
        AssertShouldAdvanceGameBoth(
            filter,
            AtMove(9999),
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

        AssertMatchesBoth(filter, AtMove(1), expected: true);
        AssertMatchesBoth(filter, AtMove(2), expected: false);
    }
}
