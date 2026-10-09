using BgDataTypes_Lib;
using BgDataTypes_Lib.TestSupport;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class ErrorRangeFilterTests
{
    /// <summary>
    /// The opening 3-1 with the player's play scored at exactly
    /// <paramref name="error"/> under either ranking: two candidates at the
    /// same depth, the best at equity 0 and the played one at
    /// <c>-error</c>, so the derived error, <c>0 - (-error)</c>, is exact.
    /// </summary>
    private static CheckerPlayDecision ErredBy(double error) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(
            plays:
            [
                TestRecords.Candidate(play: [new(8, 5), new(6, 5)], equity: 0.0),
                TestRecords.Candidate(play: [new(13, 10), new(6, 5)], equity: -error),
            ],
            userPlayIndex: 1));

    /// <summary>The opening 3-1 with the player's play off the candidate list, the analyser's error stated.</summary>
    private static CheckerPlayDecision OffList(double analysersError) =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(
            userPlayIndex: null, unlistedPlayError: analysersError));

    /// <summary>The opening 3-1 with no move recorded: no candidate is the player's and no error is stated.</summary>
    private static CheckerPlayDecision NothingRecorded() =>
        TestRecords.CheckerPlay(decision: TestRecords.CheckerPlayData(userPlayIndex: null));

    // -----------------------------------------------------------------------
    //  Bounded range — exercised against both substrates of one record
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_WhenErrorWithinRange_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, ErredBy(0.10), expected: true);
    }

    [Fact]
    public void Matches_WhenErrorBelowMin_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, ErredBy(0.03), expected: false);
    }

    [Fact]
    public void Matches_WhenErrorAboveMax_ReturnsFalse()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, ErredBy(0.25), expected: false);
    }

    [Fact]
    public void Matches_WhenErrorAtMinBoundary_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, ErredBy(0.05), expected: true);
    }

    [Fact]
    public void Matches_WhenErrorAtMaxBoundary_ReturnsTrue()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);
        AssertMatchesBoth(filter, ErredBy(0.20), expected: true);
    }

    [Fact]
    public void Matches_WhenNoMinSet_AcceptsZeroError()
    {
        var filter = new ErrorRangeFilter(min: null, max: 0.20);
        AssertMatchesBoth(filter, ErredBy(0.0), expected: true);
    }

    [Fact]
    public void Matches_WhenNoMaxSet_AcceptsLargeError()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: null);
        AssertMatchesBoth(filter, ErredBy(1.0), expected: true);
    }

    [Fact]
    public void Matches_WhenNoBoundsSet_PassesEveryResultWithAnError()
    {
        var filter = new ErrorRangeFilter(min: null, max: null);
        AssertMatchesBoth(filter, ErredBy(0.50), expected: true);
        AssertMatchesBoth(filter, OffList(0.50), expected: true);
    }

    // -----------------------------------------------------------------------
    //  Which results carry an error (IDecisionFilterData.PlayerResult)
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_UnstatedMove_IsJudgedByTheAnalysersError()
    {
        // A play off the candidate list has no candidate to score, but the
        // record states the analyser's error for it: that error is the one
        // judged, under either ranking.
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.20);

        foreach (var ranking in Enum.GetValues<PlayRanking>())
        {
            AssertMatchesBoth(filter, OffList(0.10), expected: true, ranking);
            AssertMatchesBoth(filter, OffList(0.30), expected: false, ranking);
        }
    }

    [Fact]
    public void Matches_NothingRecorded_NeverPasses()
    {
        // No move recorded, no error: dropped even by the widest range, under
        // either ranking — never read as an error of 0.
        var filter = new ErrorRangeFilter(min: 0.0, max: null);

        foreach (var ranking in Enum.GetValues<PlayRanking>())
            AssertMatchesBoth(filter, NothingRecorded(), expected: false, ranking);
    }

    [Fact]
    public void Matches_CubeDecision_IsJudgedByTheStatedActionsError()
    {
        // No double on a double/take (no double +0.512, double/take +0.634):
        // the doubler's error, 0.122, is the player's result.
        var noDouble = TestRecords.Cube(decision: TestRecords.CubeData(
            userDoublerAction: CubeAction.NoDouble, userTakerAction: null));

        AssertMatchesBoth(new ErrorRangeFilter(min: 0.1, max: 0.2), noDouble, expected: true);
        AssertMatchesBoth(new ErrorRangeFilter(min: 0.2), noDouble, expected: false);
    }

    // -----------------------------------------------------------------------
    //  The ranking in force (SPEC-scoring §2a): the error is the player's
    //  under the ranking the view or row was built for.
    // -----------------------------------------------------------------------

    [Fact]
    public void Matches_UnderDepthFirst_AMoveTheRankingDoesNotScore_NeverPasses()
    {
        // The shallow play whose equity beats depth first's best is not
        // scored there: it has no error, so no range admits it — not even
        // the widest. Under equity the same move is scored (error 0) and
        // passes; only the ranking differs.
        var played = RankingSplit.Played(RankingSplit.ShallowHigh);

        foreach (var filter in new[]
        {
            new ErrorRangeFilter(min: 0.0),
            new ErrorRangeFilter(max: 1.0),
            new ErrorRangeFilter(min: null, max: null),
        })
        {
            AssertMatchesBoth(filter, played, expected: false, PlayRanking.DepthFirst);
            AssertMatchesBoth(filter, played, expected: true, PlayRanking.Equity);
        }
    }

    [Fact]
    public void Matches_UnderDepthFirst_AScoredMovesErrorIsTheOneMeasured()
    {
        // 24/23 13/10 is scored under both rankings, against different best
        // plays: 0.25 under depth first, 0.50 under equity. Each range admits
        // the move under exactly the ranking whose error it bounds.
        var played = RankingSplit.Played(RankingSplit.DeepWorst);
        var depthFirstsError = new ErrorRangeFilter(min: 0.25, max: 0.25);
        var equitysError = new ErrorRangeFilter(min: 0.50, max: 0.50);

        AssertMatchesBoth(depthFirstsError, played, expected: true, PlayRanking.DepthFirst);
        AssertMatchesBoth(depthFirstsError, played, expected: false, PlayRanking.Equity);
        AssertMatchesBoth(equitysError, played, expected: true, PlayRanking.Equity);
        AssertMatchesBoth(equitysError, played, expected: false, PlayRanking.DepthFirst);
    }

    [Fact]
    public void Matches_ErredByMoreThanX_UnderEachRanking()
    {
        // "Erred by more than 0.3": depth first's best, 8/5 6/5, erred by
        // 0.25 under equity; 24/23 13/10 by 0.50 under equity and 0.25 under
        // depth first. So under equity one of the three passes, under depth
        // first none does — the not-scored shallow play included.
        var filter = new ErrorRangeFilter(min: 0.3);

        AssertMatchesBoth(filter, RankingSplit.Played(RankingSplit.DeepWorst), expected: true, PlayRanking.Equity);
        AssertMatchesBoth(filter, RankingSplit.Played(RankingSplit.DeepBest), expected: false, PlayRanking.Equity);
        AssertMatchesBoth(filter, RankingSplit.Played(RankingSplit.ShallowHigh), expected: false, PlayRanking.Equity);

        foreach (int played in new[] { RankingSplit.DeepBest, RankingSplit.ShallowHigh, RankingSplit.DeepWorst })
            AssertMatchesBoth(filter, RankingSplit.Played(played), expected: false, PlayRanking.DepthFirst);
    }

    // -----------------------------------------------------------------------
    //  The bound rule, stated once here and asked twice — the constructor
    //  enforces it and FilterConfig.GetInvalidFields reports it. These tests
    //  pin the predicates directly; FilterConfigTests pins the reporting and
    //  the Build path that reaches this constructor.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null)]   // absent: the rule constrains values, never presence
    [InlineData(0.0)]    // an exact-zero error filter is meaningful
    [InlineData(0.05)]
    [InlineData(3.5)]               // no cap (halheinrich/backgammon#374): above 3 is loose, not wrong
    [InlineData(1e300)]             // no cap: a huge finite bound is still a number
    [InlineData(double.MaxValue)]   // the largest finite bound
    public void IsBoundFiniteNonNegative_AdmissibleBound_ReturnsTrue(double? bound) =>
        ErrorRangeFilter.IsBoundFiniteNonNegative(bound).Should().BeTrue();

    [Theory]
    [InlineData(-0.0000001)]
    [InlineData(-1.0)]
    [InlineData(double.MinValue)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.NaN)]  // unordered, so it admits nothing — the very failure the rule catches
    [InlineData(double.PositiveInfinity)]  // not finite (halheinrich/backgammon#374): "1e999" parses to it
    public void IsBoundFiniteNonNegative_InadmissibleBound_ReturnsFalse(double? bound) =>
        ErrorRangeFilter.IsBoundFiniteNonNegative(bound).Should().BeFalse();

    [Theory]
    [InlineData(null, null)]      // both absent
    [InlineData(0.05, null)]      // one-sided
    [InlineData(null, 0.05)]      // one-sided
    [InlineData(0.05, 0.20)]
    [InlineData(0.05, 0.05)]      // equal: the inclusive bounds make this an exact-value filter
    public void AreBoundsOrdered_OrderedPair_ReturnsTrue(double? min, double? max) =>
        ErrorRangeFilter.AreBoundsOrdered(min, max).Should().BeTrue();

    [Theory]
    [InlineData(0.20, 0.05)]
    [InlineData(0.0500001, 0.05)]
    public void AreBoundsOrdered_MinExceedsMax_ReturnsFalse(double? min, double? max) =>
        ErrorRangeFilter.AreBoundsOrdered(min, max).Should().BeFalse();

    [Fact]
    public void Constructor_NegativeMin_Throws()
    {
        var act = () => new ErrorRangeFilter(min: -0.05, max: 0.20);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("min");
    }

    [Fact]
    public void Constructor_NegativeMax_Throws()
    {
        // A negative upper bound is the sharper case: a scored error is a
        // magnitude, so this admits nothing at all rather than merely being
        // redundant.
        var act = () => new ErrorRangeFilter(min: null, max: -0.05);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .Which.ParamName.Should().Be("max");
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Constructor_NonFiniteBound_Throws(double bound)
    {
        var minIsNonFinite = () => new ErrorRangeFilter(min: bound);
        var maxIsNonFinite = () => new ErrorRangeFilter(max: bound);

        minIsNonFinite.Should().Throw<ArgumentOutOfRangeException>()
                      .Which.ParamName.Should().Be("min");
        maxIsNonFinite.Should().Throw<ArgumentOutOfRangeException>()
                      .Which.ParamName.Should().Be("max");
    }

    [Theory]
    [InlineData(double.PositiveInfinity, 0.20, "min")]  // a bound fault, not a misordered pair
    [InlineData(0.0, double.PositiveInfinity, "max")]   // not a spelling of the open end: null is that
    [InlineData(0.05, double.NaN, "max")]
    public void Constructor_NonFiniteBoundBesideFiniteOne_BlamesTheNonFiniteBound(
        double? min, double? max, string expectedParam)
    {
        var act = () => new ErrorRangeFilter(min, max);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithMessage("Bound must be a finite number of zero or greater*")
           .Which.ParamName.Should().Be(expectedParam);
    }

    [Fact]
    public void Constructor_MinExceedsMax_Throws()
    {
        // Both bounds are individually admissible; the pair is not.
        var act = () => new ErrorRangeFilter(min: 0.20, max: 0.05);

        act.Should().Throw<ArgumentException>().WithMessage("*empty error range*");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, null)]
    [InlineData(null, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.05, 0.05)]
    [InlineData(0.0, double.MaxValue)]          // the widest closed range
    [InlineData(double.MaxValue, double.MaxValue)]
    [InlineData(3.5, null)]                     // no cap (halheinrich/backgammon#374)
    [InlineData(0.05, 1e300)]                   // no cap
    public void Constructor_AdmissibleBounds_DoesNotThrow(double? min, double? max)
    {
        var act = () => new ErrorRangeFilter(min, max);

        act.Should().NotThrow();
    }

    [Fact]
    public void Matches_ZeroOnlyRange_AdmitsExactlyZeroError()
    {
        // The reason zero is admissible rather than treated as "no bound":
        // [0, 0] is the errorless-decision filter, and it is not empty.
        var filter = new ErrorRangeFilter(min: 0.0, max: 0.0);

        AssertMatchesBoth(filter, ErredBy(0.0), expected: true);
        AssertMatchesBoth(filter, ErredBy(0.01), expected: false);
    }

    [Fact]
    public void Matches_EqualBounds_AdmitsExactlyThatError()
    {
        var filter = new ErrorRangeFilter(min: 0.05, max: 0.05);

        AssertMatchesBoth(filter, ErredBy(0.05), expected: true);
        AssertMatchesBoth(filter, ErredBy(0.04), expected: false);
        AssertMatchesBoth(filter, ErredBy(0.06), expected: false);
    }
}
