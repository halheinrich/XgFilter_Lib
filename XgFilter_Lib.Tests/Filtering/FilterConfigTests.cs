using System.Text.Json;
using BgDataTypes_Lib;
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

    // -----------------------------------------------------------------------
    //  Depth facet — the two-axis semantics matrix. Build owns the derivation
    //  of the effective mode set from raw intent (checked levels + the two
    //  toggles); these tests pin every rule of that derivation through the
    //  observable behaviour of the built set.
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_DepthFacetInactive_PassesEveryRow()
    {
        // No level checked AND neither toggle on → facet inactive → filter not
        // added, so even an Unknown-mode row (legacy data) passes.
        var set = new FilterConfig().Build();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Unknown, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_LevelsOnly_DefaultsToEvaluationMode()
    {
        // Levels checked, neither toggle on → mode set defaults to {Evaluation}.
        // A 4-ply evaluation passes; a 4-ply rollout is the same level but a
        // mode the facet did not select.
        var set = new FilterConfig
        {
            AnalysisLevels = { AnalysisLevel.Ply4 },
        }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_RolloutsToggleOnly_AnyLevelRollout_Passes()
    {
        // Rollouts on, no level checked → modes {Rollout}, level axis "any".
        var set = new FilterConfig { IncludeRollouts = true }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.XgRoller).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_Canonical_FourPlyChecked_RolloutsOn_OnlyFourPlyRolloutsPass()
    {
        // The user's own canonical example: 4-ply checked + Rollouts on → only
        // 4-ply rollouts pass; plain 4-ply evaluations do not.
        var set = new FilterConfig
        {
            AnalysisLevels = { AnalysisLevel.Ply4 },
            IncludeRollouts = true,
        }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_BothToggles_AdmitBothRolloutModes()
    {
        // Rollouts AND Book rollouts on → modes {Rollout, BookRollout}.
        var set = new FilterConfig
        {
            IncludeRollouts = true,
            IncludeBookRollouts = true,
        }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_BookRolloutsToggle_NoLevel_AdmitsUnenrichedBookHit()
    {
        // An unenriched / V1 / eval-baseline book hit carries BookRollout mode
        // and an Unknown level. With Book rollouts on and no level checked, the
        // "any level" axis lets it through.
        var set = new FilterConfig { IncludeBookRollouts = true }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_BookRolloutsToggle_LevelChecked_DropsUnknownLevelBookHit()
    {
        // Once a concrete level is checked, an Unknown-level book hit no longer
        // matches the level axis — only book hits enriched to that level pass.
        var set = new FilterConfig
        {
            IncludeBookRollouts = true,
            AnalysisLevels = { AnalysisLevel.Ply4 },
        }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeFalse();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_DepthFacetActive_DropsUnknownModeRow()
    {
        // No selection produces mode Unknown, so any active facet drops legacy
        // Unknown-mode rows.
        var set = new FilterConfig { IncludeRollouts = true }.Build();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Unknown, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeFalse();
    }

    // -----------------------------------------------------------------------
    //  Dice facet
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_DiceRollsNonEmpty_AddsDiceRollFilter()
    {
        var set = new FilterConfig
        {
            DiceRolls = { new DiceRoll(3, 1) },
        }.Build();

        set.Matches(new RowShape(Roll: 31).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(Roll: 52).ToDecisionRow()).Should().BeFalse();
        // Cube rows carry no roll and never pass an active dice facet.
        set.Matches(new RowShape(IsCube: true).ToDecisionRow()).Should().BeFalse();
    }

    [Fact]
    public void Build_DiceRollsEmpty_SkipsDiceRollFilter()
    {
        // Empty = facet inactive; the filter is not added, so every row passes
        // (an added empty-set DiceRollFilter would instead reject everything).
        var set = new FilterConfig().Build();
        set.Matches(new RowShape(Roll: 52).ToDecisionRow()).Should().BeTrue();
        set.Matches(new RowShape(IsCube: true).ToDecisionRow()).Should().BeTrue();
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

    [Fact]
    public void Build_UnknownAnalysisLevel_Throws()
    {
        var cfg = new FilterConfig { AnalysisLevels = { (AnalysisLevel)999 } };
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
            AnalysisLevels = { AnalysisLevel.Ply3, AnalysisLevel.XgRoller },
            IncludeRollouts = true,
            IncludeBookRollouts = true,
            DiceRolls = { new DiceRoll(3, 1), new DiceRoll(6, 6) },
            PositionPattern = BoardPattern.Parse("[6,,0] [5,2,] [0,,-1]"),
        };

        var restored = FilterConfig.FromJson(original.ToJson());

        restored.Should().BeEquivalentTo(original);
    }

    [Fact]
    public void ToJson_DiceRolls_SerializeAsTwoDigitTokenArray()
    {
        // DiceRoll carries its own type-level [JsonConverter] (owned by
        // BgDataTypes_Lib), so the list rides the wire as a string-token array —
        // ["31","66"] — even though FilterConfig registers no converter for it.
        var json = new FilterConfig
        {
            DiceRolls = { new DiceRoll(3, 1), new DiceRoll(6, 6) },
        }.ToJson();

        json.Should().Contain("[\"31\",\"66\"]",
            "DiceRoll values must serialize as their canonical two-digit tokens, not objects or ordinals");
    }

    [Fact]
    public void FromJson_DiceRollTokenArray_RestoresRolls()
    {
        // The inverse: a token array reads back through the type's converter,
        // canonicalized (the low-first "13" token yields the same value as "31").
        var restored = FilterConfig.FromJson("{\"DiceRolls\":[\"13\",\"66\"]}");

        restored.DiceRolls.Should().BeEquivalentTo(
            new[] { new DiceRoll(3, 1), new DiceRoll(6, 6) });
    }

    [Fact]
    public void FromJson_MissingDiceRolls_RestoresToInactiveFacet()
    {
        // An old saved config written before the dice facet existed omits the
        // field entirely; it must materialize as an empty list (facet inactive),
        // not null — the same additive-field tolerance the other list members get.
        var restored = FilterConfig.FromJson("{\"Players\":[\"Alice\"]}");

        restored.DiceRolls.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ToJson_AnalysisLevels_SerializeAsDeclarationNames()
    {
        // AnalysisLevel carries its own type-level JsonStringEnumConverter (it is
        // owned by BgDataTypes_Lib), so it rides the wire as its declaration name
        // even though FilterConfig does not own the enum.
        var json = new FilterConfig
        {
            AnalysisLevels = { AnalysisLevel.XgRoller },
        }.ToJson();

        json.Should().Contain("\"XgRoller\"",
            "AnalysisLevel values must serialize as declaration names, not ordinals");
    }

    [Fact]
    public void FromJson_MissingDepthMembers_RestoreToInactiveDefaults()
    {
        // Legacy JSON written before the depth facet existed (or under the
        // retired AnalysisDepthClasses field) omits the new members entirely;
        // they must materialize as an empty level list and both toggles off —
        // an inactive facet — not null. This is the beta-acceptable reset of
        // old saved configs.
        var restored = FilterConfig.FromJson(
            "{\"Players\":[\"Alice\"],\"AnalysisDepthClasses\":[\"Ply3\"]}");

        restored.AnalysisLevels.Should().NotBeNull().And.BeEmpty();
        restored.IncludeRollouts.Should().BeFalse();
        restored.IncludeBookRollouts.Should().BeFalse();
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
        restored.AnalysisLevels.Should().BeEmpty();
        restored.IncludeRollouts.Should().BeFalse();
        restored.IncludeBookRollouts.Should().BeFalse();
        restored.DiceRolls.Should().BeEmpty();
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
        // The absent-key case: a storage entry that was never written hands the
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
