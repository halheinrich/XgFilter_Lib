using BgDataTypes_Lib;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;
using XgFilter_Lib.Tests.Patterns;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// The real-corpus counterpart to <see cref="BoardPatternOracleTests"/>. Where
/// that class proves <c>pattern ≡ classifier</c> on synthetic boards, this one
/// runs both the named <see cref="PositionTypeFilter"/> and its
/// <see cref="BoardPattern"/> equivalent over the actual .xg/.xgp fixture
/// corpus and asserts they admit the identical decisions. Together they justify
/// the eventual deletion of the named types: the patterns reproduce them on
/// hand-made <em>and</em> real positions.
///
/// <para>
/// Every test reads the local <c>TestData\FixtureFiles</c> corpus in place
/// (<see cref="TestPaths"/>), which is gitignored and lives only on the
/// developer's machine, so the class carries <c>RequiresFixtureFiles</c>.
/// Every run asserts that it read at least one source, so neither layer can
/// pass on an empty folder. Two layers:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Layer 1 — file-level equivalence.</b> Fixture-agnostic: the named
///     filter and its pattern equivalent must yield the same decision-key
///     sequence, whatever the corpus holds.
///   </description></item>
///   <item><description>
///     <b>Layer 2 — non-zero discrimination.</b> Each named filter must admit
///     at least one decision. It is a forcing function — a zero count means
///     the oracle is <em>vacuous</em> for that filter (Layer 1 would pass on
///     an empty match set), so a fixture exercising that pattern should be
///     appended to the local corpus (append-only).
///   </description></item>
/// </list>
/// </summary>
[Trait("Category", "RequiresFixtureFiles")]
public class BoardPatternCorpusOracleTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    /// <summary>Each decision's identity across both filter runs: its identifier.</summary>
    private static List<DecisionId> Run(DecisionFilterSet filters)
    {
        var report = new SourceReport();
        var ids = new FilteredDecisionIterator(filters, PlayRanking.Equity, NullLogger)
            .IterateXgDirectory(TestPaths.FixtureFilesDir, report)
            .Select(r => r.Id)
            .ToList();

        report.ReadableCount.Should().BePositive(
            "the oracle compares decisions read from the corpus; an empty corpus compares nothing");
        return ids;
    }

    private static DecisionFilterSet Named(PositionType type) =>
        new DecisionFilterSet().Add(new PositionTypeFilter([type]));

    private static DecisionFilterSet Pattern(string bracketList) =>
        new DecisionFilterSet().Add(new PositionPatternFilter(BoardPattern.Parse(bracketList)));

    // -----------------------------------------------------------------------
    //  Layer 1 — file-level equivalence (fixture-agnostic)
    // -----------------------------------------------------------------------

    [Fact]
    public void VsTwoPlusUp_NamedFilter_AndPattern_AdmitIdenticalDecisions()
    {
        Run(Pattern(BoardPatternOracleTests.VsTwoPlusUpPattern))
            .Should().Equal(Run(Named(PositionType.VsTwoPlusUp)));
    }

    [Fact]
    public void Holding1386Vs20_NamedFilter_AndPattern_AdmitIdenticalDecisions()
    {
        Run(Pattern(BoardPatternOracleTests.Holding1386Vs20Template))
            .Should().Equal(Run(Named(PositionType.Holding1386Vs20)));
    }

    [Fact]
    public void InnerBoard631_NamedFilter_AndPatternWithContact_AdmitIdenticalDecisions()
    {
        // The pattern carries the structural predicate only; the classifier also
        // requires !race. Compose the pattern with Contact (AND across the set)
        // to recover the full equivalence on real positions.
        var patternWithContact = new DecisionFilterSet()
            .Add(new PositionPatternFilter(BoardPattern.Parse(BoardPatternOracleTests.InnerBoard631Pattern)))
            .Add(new ContactTypeFilter([ContactType.Contact]));

        Run(patternWithContact).Should().Equal(Run(Named(PositionType.InnerBoard631)));
    }

    [Fact]
    public void InnerBoard54321_NamedFilter_AndPatternWithContact_AdmitIdenticalDecisions()
    {
        var patternWithContact = new DecisionFilterSet()
            .Add(new PositionPatternFilter(BoardPattern.Parse(BoardPatternOracleTests.InnerBoard54321Pattern)))
            .Add(new ContactTypeFilter([ContactType.Contact]));

        Run(patternWithContact).Should().Equal(Run(Named(PositionType.InnerBoard54321)));
    }

    // -----------------------------------------------------------------------
    //  Layer 2 — non-zero discrimination. A zero here is the forcing function
    //  firing: the oracle is vacuous for that filter, so append a fixture
    //  that exercises it.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(PositionType.VsTwoPlusUp)]
    [InlineData(PositionType.Holding1386Vs20)]
    [InlineData(PositionType.InnerBoard631)]
    [InlineData(PositionType.InnerBoard54321)]
    public void NamedFilter_AdmitsAtLeastOneDecisionInCorpus(PositionType type)
    {
        Run(Named(type)).Should().NotBeEmpty(
            "the {0} oracle is only meaningful if the local corpus contains a position that exercises it; " +
            "a zero means a fixture hitting this pattern should be appended (append-only, local)", type);
    }
}
