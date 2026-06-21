using System.Text.Json;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Patterns;
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
    public void Build_ContactTypesNonEmpty_AddsContactTypeFilter()
    {
        var raceBoard = new int[26];
        raceBoard[3] = 2; raceBoard[2] = 3;
        raceBoard[22] = -2; raceBoard[23] = -3;

        var set = new FilterConfig
        {
            ContactTypes = { ContactType.Race },
        }.Build();

        set.Matches(new RowShape(Board: raceBoard).ToDecisionRow()).Should().BeTrue();
    }

    [Fact]
    public void Build_PositionTypesNonEmpty_AddsPositionTypeFilter()
    {
        // Holding 13-8-6 vs 20: player holds 13/8/6 with nothing above the 13;
        // opponent anchors on the player's 5 point (its own 20) and the 12.
        var holdingBoard = new int[26];
        holdingBoard[13] = 5; holdingBoard[8] = 3; holdingBoard[6] = 4; holdingBoard[4] = 2; holdingBoard[1] = 1;
        holdingBoard[5] = -2; holdingBoard[12] = -3; holdingBoard[19] = -4; holdingBoard[21] = -4; holdingBoard[23] = -2;

        var set = new FilterConfig
        {
            PositionTypes = { PositionType.Holding1386Vs20 },
        }.Build();

        set.Matches(new RowShape(Board: holdingBoard).ToDecisionRow()).Should().BeTrue();
    }

    [Fact]
    public void Build_ContactTypeAndPositionType_ComposeWithAnd()
    {
        // The whole point of the two-axis split: a row must satisfy BOTH the
        // contact-type facet AND the position-type facet. A holding position
        // is Contact AND Holding → passes; the plain starting position is
        // Contact but NOT Holding → rejected by the AND.
        var holdingBoard = new int[26];
        holdingBoard[13] = 5; holdingBoard[8] = 3; holdingBoard[6] = 4; holdingBoard[4] = 2; holdingBoard[1] = 1;
        holdingBoard[5] = -2; holdingBoard[12] = -3; holdingBoard[19] = -4; holdingBoard[21] = -4; holdingBoard[23] = -2;

        var startingBoard = new int[26];
        startingBoard[24] = 2; startingBoard[13] = 5; startingBoard[8] = 3; startingBoard[6] = 5;
        startingBoard[1] = -2; startingBoard[12] = -5; startingBoard[17] = -3; startingBoard[19] = -5;

        var set = new FilterConfig
        {
            ContactTypes = { ContactType.Contact },
            PositionTypes = { PositionType.Holding1386Vs20 },
        }.Build();

        set.Matches(new RowShape(Board: holdingBoard).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(Board: startingBoard).ToDecisionRow()).Should().BeFalse();
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

    [Fact]
    public void Build_PositionPatternSet_AddsPositionPatternFilter()
    {
        // [0,,-2]: opponent two-or-more on the bar.
        var vsTwoPlusUp = new int[26];
        vsTwoPlusUp[0] = -2;

        var set = new FilterConfig
        {
            PositionPattern = BoardPattern.Parse("[0,,-2]"),
        }.Build();

        set.Matches(new RowShape(Board: vsTwoPlusUp).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(Board: new int[26]).ToDecisionRow()).Should().BeFalse();
    }

    [Fact]
    public void Build_PositionPatternNull_SkipsPositionPatternFilter()
    {
        var set = new FilterConfig().Build();
        set.Matches(new RowShape(Board: new int[26]).ToDecisionRow()).Should().BeTrue();
    }

    [Fact]
    public void Build_PositionPatternEmpty_SkipsPositionPatternFilter()
    {
        // An empty pattern matches every board, so adding the filter would be a
        // no-op AND step on every row; Build skips it like the empty lists.
        var set = new FilterConfig { PositionPattern = BoardPattern.Empty }.Build();
        set.Matches(new RowShape(Board: new int[26]).ToDecisionRow()).Should().BeTrue();
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
    public void Build_UnknownContactType_Throws()
    {
        var cfg = new FilterConfig { ContactTypes = { (ContactType)999 } };
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentOutOfRangeException>();
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
    //  Canonical JSON round-trip — lib-owned wire format the panel persists
    // -----------------------------------------------------------------------

    [Fact]
    public void ToJson_PopulatedConfig_RoundTripsValueEqualThroughFromJson()
    {
        // Every field, including the enum-typed ones, must survive a
        // ToJson -> FromJson round-trip unchanged. Structural comparison
        // (BeEquivalentTo on the whole object) stands in for value equality,
        // which FilterConfig deliberately does not implement.
        var original = new FilterConfig
        {
            Players = { "Alice", "Bob" },
            DecisionType = DecisionTypeOption.CheckerPlaysOnly,
            MatchScores = { "3a5a", "money" },
            ErrorMin = 0.05,
            ErrorMax = 0.50,
            MoveNumberMin = 1,
            MoveNumberMax = 20,
            ContactTypes = { ContactType.Race },
            PositionTypes = { PositionType.InnerBoard631 },
            PlayTypes = { PlayType.Make20Pt },
            PositionPattern = BoardPattern.Parse("[6,,0] [5,2,] [0,,-1]"),
        };

        var restored = FilterConfig.FromJson(original.ToJson());

        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ToJson_PositionPattern_SerializesAsBracketListString()
    {
        // The pattern rides the wire as its human-readable bracket list, not as
        // a nested object — the BoardPatternJsonConverter is what pins this.
        var json = new FilterConfig
        {
            PositionPattern = BoardPattern.Parse("[6,,0] [5,2,]"),
        }.ToJson();

        json.Should().Contain("\"[6,,0] [5,2,]\"");
    }

    [Fact]
    public void RoundTrip_PositionPattern_ReparsesToEquivalentRanges()
    {
        var original = new FilterConfig
        {
            PositionPattern = BoardPattern.Parse("[6,,0] [5,2,] [0,,-1]"),
        };

        var restored = FilterConfig.FromJson(original.ToJson());

        restored.PositionPattern.Should().NotBeNull();
        restored.PositionPattern!.Ranges.Should().BeEquivalentTo(original.PositionPattern!.Ranges);
    }

    [Fact]
    public void FromJson_InvalidPositionPattern_Throws()
    {
        // A corrupt bracket list must fail the deserialize, not silently drop to
        // an empty pattern — the converter routes through BoardPattern.Parse.
        var act = () => FilterConfig.FromJson("{\"PositionPattern\":\"[99,,0]\"}");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void FromJson_NullPositionPattern_RestoresNull()
    {
        var restored = FilterConfig.FromJson("{\"PositionPattern\":null}");
        restored.PositionPattern.Should().BeNull();
    }

    [Fact]
    public void ToJson_EnumMembers_SerializeAsDeclarationNames()
    {
        // Pin the wire-format contract Razor relies on: enum values serialize
        // as their declaration names (PositionType.InnerBoard631 ->
        // "InnerBoard631"), not ordinals. These enum types carry no type-level
        // [JsonConverter], so this is guaranteed only by the canonical options.
        var json = new FilterConfig
        {
            DecisionType = DecisionTypeOption.CheckerPlaysOnly,
            ContactTypes = { ContactType.Race },
            PositionTypes = { PositionType.InnerBoard631 },
            PlayTypes = { PlayType.Make20Pt },
        }.ToJson();

        json.Should().Contain("\"InnerBoard631\"",
            "enum values must serialize as declaration names so the wire format survives enum reordering");
        json.Should().Contain("\"Race\"",
            "ContactType values must serialize as declaration names, not ordinals");
        json.Should().Contain("\"CheckerPlaysOnly\"");
        json.Should().Contain("\"Make20Pt\"");
    }

    [Fact]
    public void ToJson_DefaultConfig_RoundTripsToEquivalentDefaults()
    {
        // The defaults must survive the round-trip: empty lists stay empty and
        // DecisionType stays Both, so a persisted default-config blob rebuilds
        // a set that still matches every row.
        var original = new FilterConfig();

        var restored = FilterConfig.FromJson(original.ToJson());

        restored.Should().BeEquivalentTo(original);
        restored.DecisionType.Should().Be(DecisionTypeOption.Both);
        restored.Players.Should().BeEmpty();
        restored.MatchScores.Should().BeEmpty();
        restored.ContactTypes.Should().BeEmpty();
        restored.PositionTypes.Should().BeEmpty();
        restored.PlayTypes.Should().BeEmpty();
    }

    [Fact]
    public void FromJson_EmptyObject_RebuildsDefaultConfig()
    {
        // A consumer that omits every field (or trims an empty blob to "{}")
        // must still get a usable default config, not nulls.
        var restored = FilterConfig.FromJson("{}");

        restored.Should().BeEquivalentTo(new FilterConfig());
    }

    [Fact]
    public void FromJson_NullToken_Throws()
    {
        var act = () => FilterConfig.FromJson("null");
        act.Should().Throw<ArgumentException>();
    }

    // -----------------------------------------------------------------------
    //  TryFromJson — tolerant restore: absent / corrupt input -> default
    // -----------------------------------------------------------------------

    [Fact]
    public void TryFromJson_ValidJson_ReturnsTrueAndRestoresConfig()
    {
        var original = new FilterConfig
        {
            Players = { "Alice" },
            DecisionType = DecisionTypeOption.CubeOnly,
            ContactTypes = { ContactType.Race },
        };

        var ok = FilterConfig.TryFromJson(original.ToJson(), out var restored);

        ok.Should().BeTrue();
        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void TryFromJson_NullString_ReturnsFalseAndDefaultConfig()
    {
        // The absent-key case: a storage slot that was never written hands the
        // consumer a null reference, not the string "null".
        var ok = FilterConfig.TryFromJson(null, out var restored);

        ok.Should().BeFalse();
        restored.Should().BeEquivalentTo(new FilterConfig());
    }

    [Fact]
    public void TryFromJson_NullToken_ReturnsFalseAndDefaultConfig()
    {
        var ok = FilterConfig.TryFromJson("null", out var restored);

        ok.Should().BeFalse();
        restored.Should().BeEquivalentTo(new FilterConfig());
    }

    [Fact]
    public void TryFromJson_MalformedJson_ReturnsFalseAndDefaultConfig()
    {
        var ok = FilterConfig.TryFromJson("not json {", out var restored);

        ok.Should().BeFalse();
        restored.Should().BeEquivalentTo(new FilterConfig());
    }
}
