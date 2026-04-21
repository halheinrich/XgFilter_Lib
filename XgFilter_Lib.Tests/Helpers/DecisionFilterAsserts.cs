using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Helpers;

/// <summary>
/// Assertion helpers for <see cref="IDecisionFilter"/> that exercise both
/// <c>IDecisionFilterData</c> substrates (<c>DecisionRow</c> and
/// <c>BgDecisionData</c>) in one call. Every row-level filter assertion
/// should go through one of these — catches substrate-specific regressions
/// that a single-substrate assertion would miss.
/// </summary>
internal static class DecisionFilterAsserts
{
    /// <summary>
    /// Asserts <see cref="IDecisionFilter.Matches"/> returns
    /// <paramref name="expected"/> on both substrates produced from
    /// <paramref name="shape"/>. Fails with a substrate-identifying message
    /// on disagreement.
    /// </summary>
    public static void AssertMatchesBoth(IDecisionFilter filter, RowShape shape, bool expected)
    {
        filter.Matches(shape.ToDecisionRow())
              .Should().Be(expected, "DecisionRow substrate");
        filter.Matches(shape.ToBgDecisionData())
              .Should().Be(expected, "BgDecisionData substrate");
    }

    /// <summary>
    /// Asserts <see cref="IDecisionFilter.ShouldAdvanceMatch"/> returns
    /// <paramref name="expected"/> on both substrates produced from
    /// <paramref name="shape"/>.
    /// </summary>
    public static void AssertShouldAdvanceMatchBoth(IDecisionFilter filter, RowShape shape, bool expected)
    {
        filter.ShouldAdvanceMatch(shape.ToDecisionRow())
              .Should().Be(expected, "DecisionRow substrate");
        filter.ShouldAdvanceMatch(shape.ToBgDecisionData())
              .Should().Be(expected, "BgDecisionData substrate");
    }
}
