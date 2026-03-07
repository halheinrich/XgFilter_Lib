using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class DecisionFilterSetTests
{
    [Fact]
    public void Apply_WithNoFilters_ReturnsAllRows()
    {
        var set = new DecisionFilterSet();
        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob"),
        };

        set.Apply(rows).Should().HaveCount(2);
    }

    [Fact]
    public void Apply_WithSingleFilter_ReturnsMatchingRows()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob"),
        };

        var result = set.Apply(rows).ToList();

        result.Should().HaveCount(1);
        result[0].Player.Should().Be("Alice");
    }

    [Fact]
    public void Apply_WithMultipleFilters_AndSemantics()
    {
        // Only rows where Player = Alice AND Error >= 0.05 should pass
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new ErrorRangeFilter(min: 0.05, max: null));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice", error: 0.10),  // pass
            DecisionRowBuilder.Build(player: "Alice", error: 0.02),  // fail error
            DecisionRowBuilder.Build(player: "Bob",   error: 0.10),  // fail player
        };

        var result = set.Apply(rows).ToList();

        result.Should().HaveCount(1);
        result[0].Player.Should().Be("Alice");
        result[0].Error.Should().Be(0.10);
    }

    [Fact]
    public void Apply_WhenNoRowsMatch_ReturnsEmpty()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Charlie"]));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice"),
            DecisionRowBuilder.Build(player: "Bob"),
        };

        set.Apply(rows).Should().BeEmpty();
    }

    [Fact]
    public void Apply_WithDecisionTypeAndPlayerFilters_CombinesCorrectly()
    {
        var set = new DecisionFilterSet()
            .Add(new PlayerFilter(["Alice"]))
            .Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly));

        var rows = new[]
        {
            DecisionRowBuilder.Build(player: "Alice", roll: 31),   // pass
            DecisionRowBuilder.BuildCube(player: "Alice"),          // fail type
            DecisionRowBuilder.Build(player: "Bob", roll: 31),     // fail player
        };

        var result = set.Apply(rows).ToList();

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
