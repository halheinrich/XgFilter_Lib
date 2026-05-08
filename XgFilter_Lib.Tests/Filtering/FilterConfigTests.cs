using System.Text.Json;
using System.Text.Json.Serialization;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Filtering;

public class FilterConfigTests
{
    // -----------------------------------------------------------------------
    //  Default config — empty set, matches everything
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_DefaultConfig_ProducesSetThatPassesEveryRow()
    {
        var set = new FilterConfig().Build();

        set.Matches(new RowShape().ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(IsCube: true).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(Player: "anyone").ToDecisionRow()).Should().BeTrue();
    }

    [Fact]
    public void Build_DefaultDecisionTypeBoth_DoesNotAddFilter()
    {
        // Both is a no-op in the resulting filter; we don't want to add it
        // and pay an unnecessary AND step on every row. Verifying via the
        // observable: a cube row passes the default-built set, which it
        // would also under explicit Both — but skipping the add keeps the
        // set's filter list lean.
        var set = new FilterConfig { DecisionType = DecisionTypeOption.Both }.Build();

        set.Matches(new RowShape(IsCube: true).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(IsCube: false).ToDecisionRow()).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Per-filter add/skip behaviour
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_PlayersNonEmpty_AddsPlayerFilter()
    {
        var set = new FilterConfig { Players = { "Alice" } }.Build();

        set.Matches(new RowShape(Player: "Alice").ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(Player: "Bob").ToDecisionRow()).Should().BeFalse();
    }

    [Fact]
    public void Build_PlayersEmpty_SkipsPlayerFilter()
    {
        var set = new FilterConfig().Build();
        set.Matches(new RowShape(Player: "anyone").ToDecisionRow()).Should().BeTrue();
    }

    [Fact]
    public void Build_DecisionTypeCheckerOnly_AddsFilter()
    {
        var set = new FilterConfig
        {
            DecisionType = DecisionTypeOption.CheckerPlaysOnly,
        }.Build();

        set.Matches(new RowShape(IsCube: false).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(IsCube: true).ToDecisionRow()).Should().BeFalse();
    }

    [Fact]
    public void Build_MatchScoresNonEmpty_AddsMatchScoreFilter()
    {
        var set = new FilterConfig { MatchScores = { "3a5a" } }.Build();

        set.Matches(new RowShape(OnRollNeeds: 3, OpponentNeeds: 5, IsCrawford: false).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(OnRollNeeds: 2, OpponentNeeds: 4, IsCrawford: false).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_ErrorBoundsSet_AddsErrorRangeFilter()
    {
        var set = new FilterConfig { ErrorMin = 0.05 }.Build();

        set.Matches(new RowShape(Error: 0.10).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(Error: 0.01).ToDecisionRow()).Should().BeFalse();
    }

    [Fact]
    public void Build_ErrorBoundsBothNull_SkipsErrorRangeFilter()
    {
        // Skipping matters because the filter would otherwise reject rows
        // with null FilterError as a safety. The default config must not
        // silently drop unanalysed rows.
        var set = new FilterConfig().Build();
        var unanalysedDiagram = new RowShape(Error: null).ToBgDecisionData();
        set.Matches(unanalysedDiagram).Should().BeTrue();
    }

    [Fact]
    public void Build_MoveNumberBoundsSet_AddsMoveNumberFilter()
    {
        var set = new FilterConfig { MoveNumberMax = 5 }.Build();

        set.Matches(new RowShape(MoveNumber: 3).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(MoveNumber: 6).ToDecisionRow()).Should().BeFalse();
    }

    [Fact]
    public void Build_PositionTypesNonEmpty_AddsPositionTypeFilter()
    {
        var raceBoard = new int[26];
        raceBoard[3] = 2; raceBoard[2] = 3;
        raceBoard[22] = -2; raceBoard[23] = -3;

        var set = new FilterConfig
        {
            PositionTypes = { PositionType.Race },
        }.Build();

        set.Matches(new RowShape(Board: raceBoard).ToDecisionRow()).Should().BeTrue();
    }

    [Fact]
    public void Build_PlayTypesNonEmpty_AddsPlayTypeFilter()
    {
        // Make20Pt: prior board has decision-maker's 20-point empty,
        // best play makes it (afterBest[5] = -2), player play does not.
        var prior = new int[26];
        var afterBest = new int[26];
        afterBest[5] = -2;
        var afterPlayer = new int[26];

        var set = new FilterConfig
        {
            PlayTypes = { PlayType.Make20Pt },
        }.Build();

        set.Matches(new RowShape(
            Board: prior,
            AfterBestBoard: afterBest,
            AfterPlayerBoard: afterPlayer).ToDecisionRow())
            .Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    //  Throw propagation — invalid input fails fast at Build, not silently
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_MalformedMatchScore_Throws()
    {
        var cfg = new FilterConfig { MatchScores = { "garbage" } };
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentException>().WithMessage("*garbage*");
    }

    [Fact]
    public void Build_UnknownPositionType_Throws()
    {
        var cfg = new FilterConfig { PositionTypes = { (PositionType)999 } };
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_UnknownPlayType_Throws()
    {
        var cfg = new FilterConfig { PlayTypes = { (PlayType)999 } };
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    // -----------------------------------------------------------------------
    //  JSON round-trip — wire-format guarantee for cross-process consumers
    // -----------------------------------------------------------------------

    [Fact]
    public void JsonRoundTrip_PreservesEnumValuesAsStrings()
    {
        // Pin the wire-format contract Razor relies on: enum values
        // serialize as their declaration names (PositionType.InnerBoard631
        // -> "InnerBoard631"), and round-trip back to the same enum.
        var options = new JsonSerializerOptions
        {
            Converters = { new JsonStringEnumConverter() },
        };

        var original = new FilterConfig
        {
            Players = { "Alice", "Bob" },
            DecisionType = DecisionTypeOption.CheckerPlaysOnly,
            MatchScores = { "3a5a", "money" },
            ErrorMin = 0.05,
            ErrorMax = 0.50,
            MoveNumberMin = 1,
            MoveNumberMax = 20,
            PositionTypes = { PositionType.Race, PositionType.InnerBoard631 },
            PlayTypes = { PlayType.Make20Pt },
        };

        var json = JsonSerializer.Serialize(original, options);
        json.Should().Contain("\"InnerBoard631\"",
            "enum values must serialize as declaration names so the existing wire format is preserved");
        json.Should().Contain("\"CheckerPlaysOnly\"");
        json.Should().Contain("\"Make20Pt\"");

        var restored = JsonSerializer.Deserialize<FilterConfig>(json, options)!;

        restored.Players.Should().BeEquivalentTo(original.Players);
        restored.DecisionType.Should().Be(original.DecisionType);
        restored.MatchScores.Should().BeEquivalentTo(original.MatchScores);
        restored.ErrorMin.Should().Be(original.ErrorMin);
        restored.ErrorMax.Should().Be(original.ErrorMax);
        restored.MoveNumberMin.Should().Be(original.MoveNumberMin);
        restored.MoveNumberMax.Should().Be(original.MoveNumberMax);
        restored.PositionTypes.Should().BeEquivalentTo(original.PositionTypes);
        restored.PlayTypes.Should().BeEquivalentTo(original.PlayTypes);
    }
}
