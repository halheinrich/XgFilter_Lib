using XgFilter_Lib.Enums;
using XgFilter_Lib.Projection;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Projection;

public class ColumnSelectorTests
{
    // -----------------------------------------------------------------------
    //  Default constructor — all columns in declaration order
    // -----------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_SelectedColumns_MatchesAllColumns()
    {
        var selector = new ColumnSelector();

        selector.SelectedColumns.Should().Equal(ColumnSelector.AllColumns);
    }

    [Fact]
    public void DefaultConstructor_HeaderContainsEveryColumnLabel()
    {
        var selector = new ColumnSelector();
        var header = selector.Header;

        foreach (var column in ColumnSelector.AllColumns)
            header.Should().Contain(column.ToLabel());
    }

    [Fact]
    public void DefaultConstructor_SerializeContainsAllValues()
    {
        var selector = new ColumnSelector();
        var row = DecisionRowBuilder.Build(player: "Alice", error: 0.05, roll: 31);

        var line = selector.Serialize(row);

        line.Should().Contain("Alice");
        line.Should().Contain("0.05");
        line.Should().Contain("31");
    }

    // -----------------------------------------------------------------------
    //  Explicit column selection
    // -----------------------------------------------------------------------

    [Fact]
    public void ExplicitColumns_HeaderMatchesSelection()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);

        selector.Header.Should().Be("Player,Error");
    }

    [Fact]
    public void ExplicitColumns_SerializeOnlyIncludesSelectedColumns()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);
        var row = DecisionRowBuilder.Build(player: "Alice", error: 0.05, roll: 31);

        var line = selector.Serialize(row);

        line.Should().Contain("Alice");
        line.Should().Contain("0.05");
        line.Should().NotContain("31"); // Roll not selected
    }

    [Fact]
    public void ExplicitColumns_OrderIsPreserved()
    {
        var selector = new ColumnSelector([Column.Error, Column.Player]);

        selector.Header.Should().Be("Error,Player");
    }

    [Fact]
    public void ExplicitColumns_SingleColumn_HeaderAndSerializeWork()
    {
        var selector = new ColumnSelector([Column.Player]);
        var row = DecisionRowBuilder.Build(player: "Bob");

        selector.Header.Should().Be("Player");
        selector.Serialize(row).Should().Be("Bob");
    }

    [Fact]
    public void ExplicitColumns_Empty_HeaderAndSerializeAreEmpty()
    {
        var selector = new ColumnSelector([]);
        var row = DecisionRowBuilder.Build(player: "Alice");

        selector.Header.Should().BeEmpty();
        selector.Serialize(row).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    //  Undefined enum value — defensive throw at serialization time
    // -----------------------------------------------------------------------

    [Fact]
    public void Serialize_UndefinedColumnValue_Throws()
    {
        var selector = new ColumnSelector([(Column)999]);
        var row = DecisionRowBuilder.Build();

        var act = () => selector.Serialize(row);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -----------------------------------------------------------------------
    //  BuildCsv
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildCsv_EmptyRows_ReturnsHeaderOnly()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);

        var csv = selector.BuildCsv([]);

        csv.Should().StartWith("Player,Error");
        csv.Trim().Should().Be("Player,Error");
    }

    [Fact]
    public void BuildCsv_MultipleRows_IncludesHeaderAndAllRows()
    {
        var selector = new ColumnSelector([Column.Player, Column.Error]);
        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice", error: 0.05),
            DecisionRowBuilder.Build(player: "Bob",   error: 0.10),
        };

        var csv = selector.BuildCsv(rows);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                       .Select(l => l.TrimEnd('\r'))
                       .ToArray();

        lines.Should().HaveCount(3); // header + 2 rows
        lines[0].Should().Be("Player,Error");
        lines[1].Should().Contain("Alice");
        lines[2].Should().Contain("Bob");
    }

    [Fact]
    public void BuildCsv_DefaultSelector_HeaderIsAllColumns()
    {
        var selector = new ColumnSelector();
        var csv = selector.BuildCsv([]);
        var firstLine = csv.Split('\n')[0].TrimEnd('\r');

        firstLine.Should().Be("Xgid,Error,MatchScore,MatchLength,Player,SourceFile,Game,MoveNum,Roll,AnalysisDepth,Equity");
    }
}
