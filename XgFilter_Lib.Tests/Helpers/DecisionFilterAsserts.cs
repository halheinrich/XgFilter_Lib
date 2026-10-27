using BgDataTypes_Lib;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Assertion helpers for <see cref="IDecisionFilter"/> that exercise both
/// <see cref="IDecisionFilterData"/> substrates of one record under one
/// ranking in one call: the record's view (<see cref="BgDecisionData.ViewFor"/>)
/// and its row (<see cref="DecisionRow.From"/>). Every row-level filter
/// assertion goes through one of these — it catches a filter that reads a
/// member the two substrates answer differently, which a single-substrate
/// assertion would miss.
/// <para>
/// The ranking defaults to <see cref="PlayRanking.Equity"/>, the one an
/// application without the setting uses; a test whose subject is the ranking
/// states it.
/// </para>
/// </summary>
internal static class DecisionFilterAsserts
{
    /// <summary>
    /// Asserts <see cref="IDecisionFilter.Matches"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>. Fails with a
    /// substrate-identifying message on disagreement.
    /// </summary>
    public static void AssertMatchesBoth(
        IDecisionFilter filter, BgDecisionData record, bool expected, PlayRanking ranking = PlayRanking.Equity)
    {
        filter.Matches(record.ViewFor(ranking))
              .Should().Be(expected, "the record's view under {0}", ranking);
        filter.Matches(DecisionRow.From(record, ranking))
              .Should().Be(expected, "the record's row under {0}", ranking);
    }

    /// <summary>
    /// Asserts <see cref="IDecisionFilter.ShouldAdvanceMatch"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>.
    /// </summary>
    public static void AssertShouldAdvanceMatchBoth(
        IDecisionFilter filter, BgDecisionData record, bool expected, PlayRanking ranking = PlayRanking.Equity)
    {
        filter.ShouldAdvanceMatch(record.ViewFor(ranking))
              .Should().Be(expected, "the record's view under {0}", ranking);
        filter.ShouldAdvanceMatch(DecisionRow.From(record, ranking))
              .Should().Be(expected, "the record's row under {0}", ranking);
    }

    /// <summary>
    /// Asserts <see cref="IDecisionFilter.ShouldAdvanceGame"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>.
    /// </summary>
    public static void AssertShouldAdvanceGameBoth(
        IDecisionFilter filter, BgDecisionData record, bool expected, PlayRanking ranking = PlayRanking.Equity)
    {
        filter.ShouldAdvanceGame(record.ViewFor(ranking))
              .Should().Be(expected, "the record's view under {0}", ranking);
        filter.ShouldAdvanceGame(DecisionRow.From(record, ranking))
              .Should().Be(expected, "the record's row under {0}", ranking);
    }

    /// <summary>
    /// Asserts <see cref="PlayerErrorFilter.QualifyingError"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>: null as
    /// null, a number to within rounding, since a cube's errors are derived
    /// from its equities.
    /// </summary>
    public static void AssertQualifyingErrorBoth(
        PlayerErrorFilter filter, BgDecisionData record, double? expected, PlayRanking ranking = PlayRanking.Equity)
    {
        AssertError(filter.QualifyingError(record.ViewFor(ranking)), expected, "the record's view under {0}", ranking);
        AssertError(filter.QualifyingError(DecisionRow.From(record, ranking)), expected, "the record's row under {0}", ranking);
    }

    private static void AssertError(double? actual, double? expected, string because, PlayRanking ranking)
    {
        if (expected is null)
            actual.Should().BeNull(because, ranking);
        else
            actual.Should().BeApproximately(expected.Value, ErrorPrecision, because, ranking);
    }

    /// <summary>Rounding slack for an error derived from equities: far below any error a test distinguishes.</summary>
    private const double ErrorPrecision = 1e-12;

    // -----------------------------------------------------------------------
    //  Set-level mirrors of the row-level asserts above.
    //  Catch DecisionFilterSet aggregation regressions that would otherwise
    //  hide behind single-substrate test coverage.
    // -----------------------------------------------------------------------

    /// <summary>
    /// Asserts <see cref="DecisionFilterSet.Matches"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>.
    /// </summary>
    public static void AssertSetMatchesBoth(
        DecisionFilterSet set, BgDecisionData record, bool expected, PlayRanking ranking = PlayRanking.Equity)
    {
        set.Matches(record.ViewFor(ranking))
           .Should().Be(expected, "the record's view under {0}", ranking);
        set.Matches(DecisionRow.From(record, ranking))
           .Should().Be(expected, "the record's row under {0}", ranking);
    }

    /// <summary>
    /// Asserts <see cref="DecisionFilterSet.ErrorRead"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>, as
    /// <see cref="AssertQualifyingErrorBoth"/> compares.
    /// </summary>
    public static void AssertSetErrorReadBoth(
        DecisionFilterSet set, BgDecisionData record, double? expected, PlayRanking ranking = PlayRanking.Equity)
    {
        AssertError(set.ErrorRead(record.ViewFor(ranking)), expected, "the record's view under {0}", ranking);
        AssertError(set.ErrorRead(DecisionRow.From(record, ranking)), expected, "the record's row under {0}", ranking);
    }

    /// <summary>
    /// Asserts <see cref="DecisionFilterSet.ShouldAdvanceMatch"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>.
    /// </summary>
    public static void AssertSetShouldAdvanceMatchBoth(
        DecisionFilterSet set, BgDecisionData record, bool expected, PlayRanking ranking = PlayRanking.Equity)
    {
        set.ShouldAdvanceMatch(record.ViewFor(ranking))
           .Should().Be(expected, "the record's view under {0}", ranking);
        set.ShouldAdvanceMatch(DecisionRow.From(record, ranking))
           .Should().Be(expected, "the record's row under {0}", ranking);
    }

    /// <summary>
    /// Asserts <see cref="DecisionFilterSet.ShouldAdvanceGame"/> returns
    /// <paramref name="expected"/> on both substrates of
    /// <paramref name="record"/> under <paramref name="ranking"/>.
    /// </summary>
    public static void AssertSetShouldAdvanceGameBoth(
        DecisionFilterSet set, BgDecisionData record, bool expected, PlayRanking ranking = PlayRanking.Equity)
    {
        set.ShouldAdvanceGame(record.ViewFor(ranking))
           .Should().Be(expected, "the record's view under {0}", ranking);
        set.ShouldAdvanceGame(DecisionRow.From(record, ranking))
           .Should().Be(expected, "the record's row under {0}", ranking);
    }
}
