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
/// Two layers, with different corpus dependencies:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <b>Layer 1 — file-level equivalence.</b> Fixture-agnostic: the named
///     filter and its pattern equivalent must yield the same decision-key
///     sequence. Vacuously passes on an empty corpus, so it is safe to run
///     anywhere (CI included) even though the corpus itself is gitignored.
///   </description></item>
///   <item><description>
///     <b>Layer 2 — non-zero discrimination.</b> Corpus-requiring: each named
///     filter must admit at least one decision. This needs the local
///     <c>TestData\FixtureFiles</c> corpus, which is gitignored (like the
///     existing <c>halheinrich</c> player tests) and lives only on the
///     developer's machine. It is a forcing function — a zero count means the
///     oracle is <em>vacuous</em> for that filter (Layer 1 would pass on an
///     empty match set), so a fixture exercising that pattern should be
///     appended to the local corpus (append-only).
///   </description></item>
/// </list>
/// </summary>
public class BoardPatternCorpusOracleTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "FixtureFiles");

    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    /// <summary>A decision's identity across both filter runs.</summary>
    private readonly record struct DecisionKey(string? SourceFile, int MoveNumber, bool IsCube);

    private static List<DecisionKey> Run(DecisionFilterSet filters) =>
        new FilteredDecisionIterator(filters, NullLogger)
            .IterateXgDirectory(FixtureDir)
            .Select(r => new DecisionKey(r.SourceFile, r.MoveNumber, r.IsCube))
            .ToList();

    private static DecisionFilterSet Named(PositionType type) =>
        new DecisionFilterSet().Add(new PositionTypeFilter([type]));

    private static DecisionFilterSet Pattern(string bracketList) =>
        new DecisionFilterSet().Add(new PositionPatternFilter(BoardPattern.Parse(bracketList)));

    // -----------------------------------------------------------------------
    //  Layer 1 — file-level equivalence (fixture-agnostic; vacuous on empty)
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
    //  Layer 2 — non-zero discrimination (requires the local FixtureFiles
    //  corpus; gitignored). A zero here is the forcing function firing: the
    //  oracle is vacuous for that filter, so append a fixture that exercises it.
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
