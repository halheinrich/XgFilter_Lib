using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Projection;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// Integration test that reads real .xg files and filters by player name.
/// Requires .xg files in TestData\xg relative to the test output directory.
/// </summary>
public class FilteredDecisionIteratorTests
{
    private static readonly string XgDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "xg");
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "FixtureFiles");

    [Fact]
    public void TestDataDirectory_Exists_AndContainsXgFiles()
    {
        Directory.Exists(XgDir).Should().BeTrue(
            because: $"TestData\\xg directory should be copied to output: {XgDir}");

        Directory.GetFiles(XgDir, "*.xg")
            .Should().NotBeEmpty(
            because: "TestData\\xg should contain at least one .xg file");
    }

    [Fact]
    public void IterateXgDirectory_FilterByPlayer_ReturnsOnlyMatchingRows()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(FixtureDir, filters).ToList();

        rows.Should().NotBeEmpty("expected at least one decision by halheinrich in the test files");
        rows.Should().OnlyContain(r => r.Player.Equals("halheinrich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IterateXgDirectory_FilterByPlayer_CustomColumns()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(FixtureDir, filters).ToList();

        var selector = new ColumnSelector([
            Column.Player, Column.SourceFile, Column.Game,
            Column.MoveNumber, Column.Roll, Column.Error, Column.Equity]);
        selector.Header.Should().Be("Player,SourceFile,Game,MoveNumber,Roll,Error,Equity");

        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(r => selector.Serialize(r).Split(',').Length == 7);
    }

    [Fact]
    public void IterateXgDirectory_FilterByRace_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new PositionTypeFilter([PositionType.Race]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_FilterByContact_ListsDecisions()
    {
        var filters = new DecisionFilterSet()
            .Add(new PositionTypeFilter([PositionType.Contact]));

        var rows = FilteredDecisionIterator.IterateXgDirectory(XgDir, filters).ToList();

        rows.Should().OnlyContain(r => r.Board.Count == 26);
    }

    [Fact]
    public void IterateXgDirectory_RaceAndContactAreComplementary()
    {
        var allFilters = new DecisionFilterSet();
        var raceFilters = new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Race]));
        var contactFilters = new DecisionFilterSet().Add(new PositionTypeFilter([PositionType.Contact]));

        var all = FilteredDecisionIterator.IterateXgDirectory(XgDir, allFilters).ToList();
        var race = FilteredDecisionIterator.IterateXgDirectory(XgDir, raceFilters).ToList();
        var contact = FilteredDecisionIterator.IterateXgDirectory(XgDir, contactFilters).ToList();

        (race.Count + contact.Count).Should().Be(all.Count,
            "Race and Contact should be mutually exclusive and exhaustive");
    }

    [Fact]
    public void IterateXgDirectory_WhenNoFilesPresent_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            var filters = new DecisionFilterSet()
                .Add(new PlayerFilter(["halheinrich"]));

            var rows = FilteredDecisionIterator.IterateXgDirectory(emptyDir, filters).ToList();

            rows.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }

    // -----------------------------------------------------------------------
    //  Diagram-shape variants
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectoryDiagrams_FilterByPlayer_ReturnsOnlyMatchingDiagrams()
    {
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var diagrams = FilteredDecisionIterator
            .IterateXgDirectoryDiagrams(FixtureDir, filters).ToList();

        diagrams.Should().NotBeEmpty(
            "expected at least one decision by halheinrich in the test files");
        diagrams.Should().OnlyContain(d =>
            d.Descriptive.OnRollName.Equals("halheinrich", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_AtLeastOneDecisionHasPlays()
    {
        // The diagram form's whole purpose is the per-candidate Plays list.
        // No filter — take everything in the fixture corpus and assert that
        // some checker-play decision yields a non-empty Plays list. Cube
        // decisions may have empty Plays by contract; we just need one
        // non-cube decision to populate.
        var diagrams = FilteredDecisionIterator
            .IterateXgDirectoryDiagrams(FixtureDir, new DecisionFilterSet()).ToList();

        diagrams.Should().NotBeEmpty();
        diagrams.Should().Contain(d => d.Decision.Plays.Count > 0,
            "diagram form must populate Plays for checker-play decisions");
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_RowVariant_AndDiagramVariant_AgreeOnPassingDecisions()
    {
        // Same fixture corpus, same filter set, both shapes implement
        // IDecisionFilterData identically — the pass/fail outcome must be
        // identical decision-for-decision. Compare on (SourceFile, MoveNumber,
        // IsCube) which both shapes carry.
        var filters = new DecisionFilterSet()
            .Add(new PlayerFilter(["halheinrich"]));

        var rowKeys = FilteredDecisionIterator
            .IterateXgDirectory(FixtureDir, filters)
            .Select(r => (r.SourceFile, r.MoveNumber, r.IsCube))
            .ToList();

        var diagramKeys = FilteredDecisionIterator
            .IterateXgDirectoryDiagrams(FixtureDir, filters)
            .Select(d => (d.Descriptive.SourceFile, d.Descriptive.MoveNumber, d.Decision.IsCube))
            .ToList();

        diagramKeys.Should().Equal(rowKeys,
            "row and diagram variants must yield the same decisions in the same order for the same filter set");
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_WhenNoFilesPresent_ReturnsEmpty()
    {
        var emptyDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(emptyDir);

        try
        {
            var diagrams = FilteredDecisionIterator
                .IterateXgDirectoryDiagrams(emptyDir, new DecisionFilterSet()).ToList();

            diagrams.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(emptyDir);
        }
    }
}
