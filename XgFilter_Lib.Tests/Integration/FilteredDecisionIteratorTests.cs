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
}
