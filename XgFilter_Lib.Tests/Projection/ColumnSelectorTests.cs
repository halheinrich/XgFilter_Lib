using XgFilter_Lib.Projection;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Projection;

public class ColumnSelectorTests
{
    // -----------------------------------------------------------------------
    //  Default constructor — all columns
    // -----------------------------------------------------------------------

    [Fact]
    public void DefaultConstructor_HeaderContainsAllColumns()
    {
        var selector = new ColumnSelector();
        var header = selector.Header;

        header.Should().Contain("Xgid");
        header.Should().Contain("Error");
        header.Should().Contain("MatchScore");
        header.Should().Contain("MatchLength");
        header.Should().Contain("Player");
        header.Should().Contain("SourceFile");
        header.Should().Contain("Game");
        header.Should().Contain("MoveNum");
        header.Should().Contain("Roll");
        header.Should().Contain("AnalysisDepth");
        header.Should().Contain("Equity");
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
        var selector = new ColumnSelector(["Player", "Error"]);

        selector.Header.Should().Be("Player,Error");
    }

    [Fact]
    public void ExplicitColumns_SerializeOnlyIncludesSelectedColumns()
    {
        var selector = new ColumnSelector(["Player", "Error"]);
        var row = DecisionRowBuilder.Build(player: "Alice", error: 0.05, roll: 31);

        var line = selector.Serialize(row);

        line.Should().Contain("Alice");
        line.Should().Contain("0.05");
        line.Should().NotContain("31"); // Roll not selected
    }

    [Fact]
    public void ExplicitColumns_OrderIsPreserved()
    {
        var selector = new ColumnSelector(["Error", "Player"]);

        selector.Header.Should().Be("Error,Player");
    }

    [Fact]
    public void ExplicitColumns_UnknownColumnThrows()
    {
        var act = () => new ColumnSelector(["Player", "NonExistentColumn"]);

        act.Should().Throw<ArgumentException>()
           .WithMessage("*NonExistentColumn*");
    }

    [Fact]
    public void SingleColumn_HeaderAndSerializeWork()
    {
        var selector = new ColumnSelector(["Player"]);
        var row = DecisionRowBuilder.Build(player: "Bob");

        selector.Header.Should().Be("Player");
        selector.Serialize(row).Should().Be("Bob");
    }

    // -----------------------------------------------------------------------
    //  BuildCsv
    // -----------------------------------------------------------------------

    [Fact]
    public void BuildCsv_EmptyRows_ReturnsHeaderOnly()
    {
        var selector = new ColumnSelector(["Player", "Error"]);

        var csv = selector.BuildCsv([]);

        csv.Should().StartWith("Player,Error");
        csv.Trim().Should().Be("Player,Error");
    }

    [Fact]
    public void BuildCsv_MultipleRows_IncludesHeaderAndAllRows()
    {
        var selector = new ColumnSelector(["Player", "Error"]);
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