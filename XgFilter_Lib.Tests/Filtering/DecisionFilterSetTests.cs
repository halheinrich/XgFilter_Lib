using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class DecisionFilterSetTests
{
    [Fact]
    public void Matches_WithNoFilters_PassesAllRows()
    {
        var set = new DecisionFilterSet();
        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob"),
        };

        rows.Where(set.Matches).Should().HaveCount(2);
    }

    [Fact]
    public void Matches_WithSingleFilter_PassesMatchingRows()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob"),
        };

        var result = rows.Where(set.Matches).ToList();

        result.Should().HaveCount(1);
        result[0].Player.Should().Be("Alice");
    }

    [Fact]
    public void Matches_WithMultipleFilters_AndSemantics()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new ErrorRangeFilter(min: 0.05, max: null));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice", error: 0.10),
            DecisionRowBuilder.Build(player: "Alice", error: 0.02),
            DecisionRowBuilder.Build(player: "Bob",   error: 0.10),
        };

        var result = rows.Where(set.Matches).ToList();

        result.Should().HaveCount(1);
        result[0].Player.Should().Be("Alice");
        result[0].Error.Should().Be(0.10);
    }

    [Fact]
    public void Matches_WhenNoRowsMatch_ReturnsEmpty()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Charlie"]));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob"),
        };

        rows.Where(set.Matches).Should().BeEmpty();
    }

    [Fact]
    public void Matches_WithDecisionTypeAndPlayerFilters_CombinesCorrectly()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice", roll: 31),
            DecisionRowBuilder.BuildCube(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob", roll: 31),
        };

        var result = rows.Where(set.Matches).ToList();

        result.Should().HaveCount(1);
        result[0].IsCube.Should().BeFalse();
    }

    [Fact]
    public void Add_IsFluentAndReturnsSameInstance()
    {
        var set = new DecisionFilterSet();
        var returned = set.Add(new PlayerFilter(["Alice"]));

        returned.Should().BeSameAs(set);
    }
}