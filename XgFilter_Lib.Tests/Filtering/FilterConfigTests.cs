using System.Reflection;
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
    //  Depth facet — the clause-union semantics matrix. Build owns the
    //  derivation of the clause union from raw intent (three per-mode toggles,
    //  each with its own level list); these tests pin every rule of that
    //  derivation through the observable behaviour of the built set.
    // -----------------------------------------------------------------------

    [Fact]
    public void Build_DepthFacetInactive_PassesEveryRow()
    {
        // All three toggles off → facet inactive → filter not added, so even
        // an Unknown-mode row (legacy data) passes.
        var set = new FilterConfig().Build();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Unknown, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_LevelsWithoutTheirToggle_AreInert()
    {
        // A level list qualifies only its own mode toggle; with every toggle
        // off the facet stays inactive no matter which lists are populated —
        // the filter is not added and every row passes.
        var set = new FilterConfig
        {
            EvaluationLevels = { AnalysisLevel.Ply4 },
            RolloutLevels = { AnalysisLevel.Ply3 },
            BookRolloutLevels = { AnalysisLevel.XgRoller },
        }.Build();

        set.IsEmpty.Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply1).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Unknown, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_EvaluationsToggleWithLevels_OnlyThoseEvaluationsPass()
    {
        // Evaluations on with 4-ply checked → one clause: Evaluation at Ply4.
        // A 4-ply rollout is the same level but a mode the facet did not select.
        var set = new FilterConfig
        {
            IncludeEvaluations = true,
            EvaluationLevels = { AnalysisLevel.Ply4 },
        }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply3).ToDecisionRow())
            .Should().BeFalse();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_RolloutsToggleOnly_AnyLevelRollout_Passes()
    {
        // Rollouts on, no rollout level checked → one clause: Rollout at any
        // inner level.
        var set = new FilterConfig { IncludeRollouts = true }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.XgRoller).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_RolloutLevels_ConstrainOnlyRolloutRows()
    {
        // Rollout levels are the INNER level of the rollout's games; they bind
        // the Rollout clause and nothing else. An unconstrained Evaluation
        // clause alongside stays "any level".
        var set = new FilterConfig
        {
            IncludeRollouts = true,
            RolloutLevels = { AnalysisLevel.Ply3 },
            IncludeEvaluations = true,
        }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply3).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeFalse();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply1).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_BothRolloutToggles_AdmitBothRolloutModes()
    {
        // Rollouts AND Book rollouts on → two clauses, each any-level.
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
        // and an Unknown level. With Book rollouts on and no book-rollout
        // level checked, the clause's "any level" axis lets it through.
        var set = new FilterConfig { IncludeBookRollouts = true }.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeTrue();
    }

    [Fact]
    public void Build_BookRolloutLevelChecked_DropsUnknownLevelBookHit()
    {
        // Once a concrete book-rollout level is checked, an Unknown-level book
        // hit no longer matches that clause — only book hits enriched to the
        // checked level pass.
        var set = new FilterConfig
        {
            IncludeBookRollouts = true,
            BookRolloutLevels = { AnalysisLevel.Ply4 },
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

    [Fact]
    public void Build_AcceptanceCase_AnyRolloutOrRollerPlusPlusEvaluation()
    {
        // The beta report's inexpressible selection, verbatim: Rollouts on
        // with no levels + Evaluations at XG Roller++. Rollout rows pass at
        // ANY inner level (checker rollouts never carry Roller-family inner
        // levels, so the old shared level set matched no rollouts at all);
        // Roller++ evaluation rows pass; other evaluations do not.
        var config = new FilterConfig
        {
            IncludeRollouts = true,
            IncludeEvaluations = true,
            EvaluationLevels = { AnalysisLevel.XgRollerPlusPlus },
        };
        var set = config.Build();

        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply3).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply4).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.XgRoller).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.XgRollerPlusPlus).ToDecisionRow())
            .Should().BeTrue();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.XgRoller).ToDecisionRow())
            .Should().BeFalse();
        set.Matches(new RowShape(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown).ToDecisionRow())
            .Should().BeFalse();
    }

    [Fact]
    public void Build_AcceptanceCase_AddingSelections_StrictlyGrowsTheMatchedSet()
    {
        // Union semantics: every further toggle or level can only admit MORE
        // rows. Pinned on a mixed sample containing a row each addition newly
        // admits, starting from the acceptance-case config.
        var sample = new RowShape[]
        {
            new(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.Ply3),
            new(AnalysisMode: AnalysisMode.Rollout, AnalysisLevel: AnalysisLevel.XgRoller),
            new(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.XgRollerPlusPlus),
            new(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.XgRoller),
            new(AnalysisMode: AnalysisMode.Evaluation, AnalysisLevel: AnalysisLevel.Ply3),
            new(AnalysisMode: AnalysisMode.BookRollout, AnalysisLevel: AnalysisLevel.Unknown),
            new(AnalysisMode: AnalysisMode.Unknown, AnalysisLevel: AnalysisLevel.Unknown),
        };

        static int Matched(FilterConfig config, IEnumerable<RowShape> rows)
        {
            var set = config.Build();
            return rows.Count(r => set.Matches(r.ToDecisionRow()));
        }

        var config = new FilterConfig
        {
            IncludeRollouts = true,
            IncludeEvaluations = true,
            EvaluationLevels = { AnalysisLevel.XgRollerPlusPlus },
        };
        var baseline = Matched(config, sample);

        // A further level on an existing clause admits the XgRoller evaluation.
        config.EvaluationLevels.Add(AnalysisLevel.XgRoller);
        var withExtraLevel = Matched(config, sample);
        withExtraLevel.Should().BeGreaterThan(baseline);

        // A further toggle admits the book hit.
        config.IncludeBookRollouts = true;
        Matched(config, sample).Should().BeGreaterThan(withExtraLevel);
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
    public void Build_UnknownAnalysisLevel_WithItsToggleOn_Throws()
    {
        var cfg = new FilterConfig
        {
            IncludeEvaluations = true,
            EvaluationLevels = { (AnalysisLevel)999 },
        };
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_UnknownAnalysisLevel_WithoutItsToggle_DoesNotThrow()
    {
        // An inert level list contributes no clause and is never validated —
        // consistent with it constraining nothing. Another active toggle
        // exercises the facet's factory to prove the inert list stays unread.
        var cfg = new FilterConfig
        {
            IncludeRollouts = true,
            EvaluationLevels = { (AnalysisLevel)999 },
        };
        var act = () => cfg.Build();
        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.05, null)]
    [InlineData(null, -0.05)]
    [InlineData(-0.05, -0.01)]
    [InlineData(double.NaN, null)]
    [InlineData(null, double.NaN)]
    public void Build_InadmissibleErrorBound_Throws(double? min, double? max)
    {
        // Filter error is a magnitude, so Build refuses rather than
        // materializing a bound that is either a no-op or admits nothing.
        var cfg = new FilterConfig { ErrorMin = min, ErrorMax = max };
        var act = () => cfg.Build();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_ErrorMinExceedsMax_Throws()
    {
        var cfg = new FilterConfig { ErrorMin = 0.20, ErrorMax = 0.05 };
        var act = () => cfg.Build();

        act.Should().Throw<ArgumentException>().WithMessage("*empty error range*");
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, null)]
    [InlineData(null, 0.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(0.05, 0.05)]
    [InlineData(0.05, 0.20)]
    public void Build_AdmissibleErrorBounds_DoesNotThrow(double? min, double? max)
    {
        var cfg = new FilterConfig { ErrorMin = min, ErrorMax = max };
        var act = () => cfg.Build();

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  GetActiveFacets — facet-activity mirror of Build's add/skip gates.
    //  Both consume the same private rule table, so these tests pin the
    //  activation vocabulary the FilterPanel's "N hidden filters active"
    //  signal consults; the Build matrix above pins the materialization.
    // -----------------------------------------------------------------------

    [Fact]
    public void GetActiveFacets_DefaultConfig_ReturnsEmptySet()
    {
        var facets = new FilterConfig().GetActiveFacets();

        facets.Should().BeEmpty();
        facets.Count.Should().Be(0);
    }

    [Fact]
    public void GetActiveFacets_EachListFacet_ReportsExactlyThatFacet()
    {
        new FilterConfig { Players = { "Alice" } }
            .GetActiveFacets().Should().Equal(FilterFacet.Players);
        new FilterConfig { MatchScores = { "3a5a" } }
            .GetActiveFacets().Should().Equal(FilterFacet.MatchScores);
        new FilterConfig { ContactTypes = { ContactType.Race } }
            .GetActiveFacets().Should().Equal(FilterFacet.ContactTypes);
        new FilterConfig { PositionTypes = { PositionType.InnerBoard631 } }
            .GetActiveFacets().Should().Equal(FilterFacet.PositionTypes);
        new FilterConfig { PlayTypes = { PlayType.Make20Pt } }
            .GetActiveFacets().Should().Equal(FilterFacet.PlayTypes);
        new FilterConfig { DiceRolls = { new DiceRoll(3, 1) } }
            .GetActiveFacets().Should().Equal(FilterFacet.DiceRolls);
    }

    [Fact]
    public void GetActiveFacets_DecisionTypeNonBoth_ReportsDecisionType()
    {
        new FilterConfig { DecisionType = DecisionTypeOption.CheckerPlaysOnly }
            .GetActiveFacets().Should().Equal(FilterFacet.DecisionType);
        new FilterConfig { DecisionType = DecisionTypeOption.CubeOnly }
            .GetActiveFacets().Should().Equal(FilterFacet.DecisionType);
    }

    [Fact]
    public void GetActiveFacets_DecisionTypeBoth_IsInactive()
    {
        // Both is Build's no-op default; the facet must mirror the skipped add.
        new FilterConfig { DecisionType = DecisionTypeOption.Both }
            .GetActiveFacets().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveFacets_EitherErrorBoundAlone_ReportsErrorRange()
    {
        new FilterConfig { ErrorMin = 0.05 }
            .GetActiveFacets().Should().Equal(FilterFacet.ErrorRange);
        new FilterConfig { ErrorMax = 0.50 }
            .GetActiveFacets().Should().Equal(FilterFacet.ErrorRange);
    }

    [Fact]
    public void GetActiveFacets_EitherMoveNumberBoundAlone_ReportsMoveNumberRange()
    {
        new FilterConfig { MoveNumberMin = 1 }
            .GetActiveFacets().Should().Equal(FilterFacet.MoveNumberRange);
        new FilterConfig { MoveNumberMax = 20 }
            .GetActiveFacets().Should().Equal(FilterFacet.MoveNumberRange);
    }

    [Fact]
    public void GetActiveFacets_DepthArms_EachToggleAloneActivatesTheOneFacet()
    {
        // The three mode toggles are ONE facet: each toggle alone, and a
        // combined arm, all report exactly AnalysisDepth — matching the Build
        // derivation (facet active iff any toggle on).
        new FilterConfig { IncludeEvaluations = true }
            .GetActiveFacets().Should().Equal(FilterFacet.AnalysisDepth);
        new FilterConfig { IncludeRollouts = true }
            .GetActiveFacets().Should().Equal(FilterFacet.AnalysisDepth);
        new FilterConfig { IncludeBookRollouts = true }
            .GetActiveFacets().Should().Equal(FilterFacet.AnalysisDepth);
        new FilterConfig { IncludeEvaluations = true, EvaluationLevels = { AnalysisLevel.Ply4 }, IncludeRollouts = true }
            .GetActiveFacets().Should().Equal(FilterFacet.AnalysisDepth);
    }

    [Fact]
    public void GetActiveFacets_DepthLevelsWithoutTheirToggle_AreInactive()
    {
        // Inert level lists mirror Build's skipped add: no toggle, no facet.
        new FilterConfig
        {
            EvaluationLevels = { AnalysisLevel.Ply4 },
            RolloutLevels = { AnalysisLevel.Ply3 },
            BookRolloutLevels = { AnalysisLevel.XgRoller },
        }.GetActiveFacets().Should().BeEmpty();
    }

    [Fact]
    public void GetActiveFacets_PositionPattern_EmptyInactive_NonEmptyActive()
    {
        // Null and the empty pattern are both the inactive state — an empty
        // pattern matches every board, and Build skips the add for both.
        new FilterConfig { PositionPattern = null }
            .GetActiveFacets().Should().BeEmpty();
        new FilterConfig { PositionPattern = BoardPattern.Empty }
            .GetActiveFacets().Should().BeEmpty();
        new FilterConfig { PositionPattern = BoardPattern.Parse("[0,,-2]") }
            .GetActiveFacets().Should().Equal(FilterFacet.PositionPattern);
    }

    [Fact]
    public void GetActiveFacets_MultiFacetConfig_EnumeratesDistinctInDeclarationOrder()
    {
        // Set semantics plus the documented ordering guarantee: declaration
        // order == Build's add order, regardless of assignment order here.
        var facets = new FilterConfig
        {
            PositionPattern = BoardPattern.Parse("[0,,-2]"),
            DiceRolls = { new DiceRoll(6, 6) },
            IncludeBookRollouts = true,
            ErrorMin = 0.05,
            Players = { "Alice" },
        }.GetActiveFacets();

        facets.Should().Equal(
            FilterFacet.Players,
            FilterFacet.ErrorRange,
            FilterFacet.AnalysisDepth,
            FilterFacet.DiceRolls,
            FilterFacet.PositionPattern);
    }

    [Fact]
    public void GetActiveFacets_AgreesWithBuildEmptiness()
    {
        // The cross-surface consistency contract: no facets active iff the
        // built set is empty (DecisionFilterSet.IsEmpty is the SSOT consumers
        // consult for "no filters active").
        var configs = new[]
        {
            new FilterConfig(),
            new FilterConfig { PositionPattern = BoardPattern.Empty },
            new FilterConfig { DecisionType = DecisionTypeOption.Both },
            new FilterConfig { Players = { "Alice" } },
            new FilterConfig { EvaluationLevels = { AnalysisLevel.Ply4 } },
            new FilterConfig { IncludeEvaluations = true },
            new FilterConfig { IncludeRollouts = true },
            new FilterConfig { IncludeBookRollouts = true },
            new FilterConfig { ErrorMin = 0.05, DiceRolls = { new DiceRoll(3, 1) } },
        };

        foreach (var config in configs)
        {
            (config.GetActiveFacets().Count == 0).Should().Be(
                config.Build().IsEmpty,
                "GetActiveFacets and Build must agree on whether any facet is active");
        }
    }

    [Fact]
    public void GetActiveFacets_MalformedContent_StillReportsFacetActiveWithoutThrowing()
    {
        // Activity is presence, not validity: a garbage score token and an
        // undefined enum value both count as active facets (they are filters
        // the user has set, however broken); Build stays the point that throws.
        new FilterConfig { MatchScores = { "garbage" } }
            .GetActiveFacets().Should().Equal(FilterFacet.MatchScores);
        new FilterConfig { DecisionType = (DecisionTypeOption)999 }
            .GetActiveFacets().Should().Equal(FilterFacet.DecisionType);
    }

    // -----------------------------------------------------------------------
    //  GetInvalidFields — the validity counterpart to GetActiveFacets, and the
    //  query a panel gates its commit action on. The rules live on the filters
    //  (ErrorRangeFilterTests pins them); these tests pin the reporting: which
    //  field gets blamed, that it never throws, and that it agrees with Build.
    // -----------------------------------------------------------------------

    [Fact]
    public void GetInvalidFields_DefaultConfig_ReturnsEmptySet()
    {
        var fields = new FilterConfig().GetInvalidFields();

        fields.Should().BeEmpty();
        fields.Contains(FilterField.ErrorMin).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, null)]   // both absent: the rule constrains values, never presence
    [InlineData(0.0, null)]    // one-sided, at the admissible edge
    [InlineData(null, 0.0)]
    [InlineData(0.0, 0.0)]     // the exact-zero-error filter
    [InlineData(0.05, 0.05)]   // equal bounds: inclusive, so an exact-value filter
    [InlineData(0.05, 0.20)]
    [InlineData(0.05, null)]
    [InlineData(null, 0.20)]
    public void GetInvalidFields_AdmissibleErrorBounds_ReturnsEmptySet(double? min, double? max)
    {
        new FilterConfig { ErrorMin = min, ErrorMax = max }
            .GetInvalidFields().Should().BeEmpty();
    }

    [Theory]
    [InlineData(-0.05, null)]
    [InlineData(-0.05, 0.20)]
    [InlineData(double.NaN, null)]
    [InlineData(double.NaN, 0.20)]
    [InlineData(double.NegativeInfinity, null)]
    public void GetInvalidFields_InadmissibleMinAlone_BlamesOnlyErrorMin(double? min, double? max)
    {
        new FilterConfig { ErrorMin = min, ErrorMax = max }
            .GetInvalidFields().Should().Equal(FilterField.ErrorMin);
    }

    [Theory]
    [InlineData(null, -0.05)]
    [InlineData(0.0, -0.05)]     // the min is also "above" the max, but only because the max is bad
    [InlineData(0.20, -0.05)]
    [InlineData(null, double.NaN)]
    [InlineData(0.05, double.NaN)]
    [InlineData(null, double.NegativeInfinity)]
    public void GetInvalidFields_InadmissibleMaxAlone_BlamesOnlyErrorMax(double? min, double? max)
    {
        // A valid min alongside an invalid max must not be marked: a consumer
        // reds the boxes this names, and reding a field the user got right is
        // its own bug. An inadmissible max drags the pair out of order as a
        // side effect, and that consequence must not leak into the blame.
        new FilterConfig { ErrorMin = min, ErrorMax = max }
            .GetInvalidFields().Should().Equal(FilterField.ErrorMax);
    }

    [Fact]
    public void GetInvalidFields_BothBoundsNegative_BlamesBoth()
    {
        new FilterConfig { ErrorMin = -0.20, ErrorMax = -0.05 }
            .GetInvalidFields().Should().Equal(FilterField.ErrorMin, FilterField.ErrorMax);
    }

    [Fact]
    public void GetInvalidFields_MinExceedsMax_BlamesBothOnlyWhenBothAreAdmissible()
    {
        // Neither bound is wrong on its own — the pair is — so both are named
        // and the user chooses which end to move.
        new FilterConfig { ErrorMin = 0.20, ErrorMax = 0.05 }
            .GetInvalidFields().Should().Equal(FilterField.ErrorMin, FilterField.ErrorMax);
    }

    [Fact]
    public void GetInvalidFields_EnumeratesInFilterFieldDeclarationOrder()
    {
        new FilterConfig
        {
            MatchScores = { "garbage" },
            ErrorMin = 0.20,
            ErrorMax = 0.05,
            MoveNumberMin = 10,
            MoveNumberMax = 3,
        }
            .GetInvalidFields().Should().ContainInOrder(
                FilterField.MatchScores,
                FilterField.ErrorMin,
                FilterField.ErrorMax,
                FilterField.MoveNumberMin,
                FilterField.MoveNumberMax);
    }

    [Fact]
    public void GetInvalidFields_InvalidBounds_StillReportTheFacetActive()
    {
        // The two queries answer different questions and must not be conflated:
        // an invalid bound is still a filter the user set, so it stays active
        // (and countable in the panel's hidden-filters signal) while being
        // named here.
        var cfg = new FilterConfig { ErrorMin = -0.05 };

        cfg.GetActiveFacets().Should().Equal(FilterFacet.ErrorRange);
        cfg.GetInvalidFields().Should().Equal(FilterField.ErrorMin);
    }

    [Fact]
    public void GetInvalidFields_ContentInvalidElsewhere_StaysEmpty()
    {
        // The documented converse: an empty result is not a promise that Build
        // succeeds. Facets with no rule row still carry content Build validates
        // — a checkbox list's undefined enum value is the remaining case, now
        // that match scores have joined the table
        // (halheinrich/backgammon#121).
        var cfg = new FilterConfig { ContactTypes = { (ContactType)999 } };

        cfg.GetInvalidFields().Should().BeEmpty();
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void GetInvalidFields_NeverThrows_OnWhollyCorruptConfig()
    {
        var cfg = new FilterConfig
        {
            ErrorMin = double.NaN,
            ErrorMax = -1.0,
            MoveNumberMin = 0,
            MoveNumberMax = int.MinValue,
            MatchScores = { "garbage" },
            DecisionType = (DecisionTypeOption)999,
            ContactTypes = { (ContactType)999 },
        };
        var act = () => cfg.GetInvalidFields();

        act.Should().NotThrow();
    }

    [Fact]
    public void GetInvalidFields_AgreesWithBuild_OnEveryErrorBoundCombination()
    {
        // The two surfaces consult the same predicates on ErrorRangeFilter, so
        // "named here" and "rejected by Build" must coincide for this facet.
        // Swept rather than enumerated so a future bound rule cannot quietly
        // hold on one surface only.
        double?[] candidates =
        [
            null, 0.0, 0.05, 0.20, -0.05, double.NaN,
            double.PositiveInfinity, double.NegativeInfinity,
        ];

        foreach (var min in candidates)
        {
            foreach (var max in candidates)
            {
                var cfg = new FilterConfig { ErrorMin = min, ErrorMax = max };
                var named = cfg.GetInvalidFields().Count > 0;
                var rejected = Record.Exception(() => cfg.Build()) is not null;

                rejected.Should().Be(
                    named,
                    "GetInvalidFields and Build must agree for ErrorMin={0}, ErrorMax={1}",
                    min,
                    max);
            }
        }
    }

    [Fact]
    public void FromJson_LegacyNegativeBounds_LoadAndAreReportedRatherThanRejected()
    {
        // The reason validity is a query and not a gate on assignment: a
        // document written before the rule existed must still round-trip, so
        // the consumer can show the offending value back to the user instead of
        // losing it to a failed restore.
        var restored = FilterConfig.FromJson("""{"ErrorMin":-0.5,"ErrorMax":-0.1}""");

        restored.ErrorMin.Should().Be(-0.5);
        restored.ErrorMax.Should().Be(-0.1);
        restored.GetInvalidFields()
                .Should().Equal(FilterField.ErrorMin, FilterField.ErrorMax);
    }

    [Fact]
    public void TryFromJson_LegacyNegativeBounds_SucceedsRatherThanFallingBackToDefault()
    {
        var ok = FilterConfig.TryFromJson("""{"ErrorMin":-0.5}""", out var restored);

        ok.Should().BeTrue("a rule stated after the document was written must not corrupt the restore");
        restored.ErrorMin.Should().Be(-0.5);
        restored.GetInvalidFields().Should().Equal(FilterField.ErrorMin);
    }

    [Fact]
    public void Setters_InadmissibleBounds_DoNotThrow()
    {
        // Stated explicitly because it is a contract, not an omission: the
        // setters are wire-facing and must accept whatever a stored document or
        // a half-typed field holds.
        var act = () =>
        {
            var cfg = new FilterConfig();
            cfg.ErrorMin = -1.0;
            cfg.ErrorMax = double.NaN;
            cfg.MoveNumberMin = 0;
            cfg.MoveNumberMax = -3;
            return cfg;
        };

        act.Should().NotThrow();
    }

    // -----------------------------------------------------------------------
    //  Canonical JSON round-trip — lib-owned wire format the panel persists
    // -----------------------------------------------------------------------

    [Fact]
    public void ToJson_PopulatedConfig_RoundTripsValueEqualThroughFromJson()
    {
        // Every field, including the enum-typed ones, must survive a
        // ToJson -> FromJson round-trip unchanged. Asserted twice over: the
        // type's own value equality, and a structural comparison that is
        // independent of it (so a bug in Equals cannot mask a wire regression).
        var original = new FilterConfig
        {
            Players = { "Alice", "Bob" },
            DecisionType = DecisionTypeOption.CheckerPlaysOnly,
            MatchScores = { "3a5a", "moneyJ" },
            ErrorMin = 0.05,
            ErrorMax = 0.50,
            MoveNumberMin = 1,
            MoveNumberMax = 20,
            ContactTypes = { ContactType.Race },
            PositionTypes = { PositionType.InnerBoard631 },
            PlayTypes = { PlayType.Make20Pt },
            IncludeEvaluations = true,
            EvaluationLevels = { AnalysisLevel.Ply3, AnalysisLevel.XgRoller },
            IncludeRollouts = true,
            RolloutLevels = { AnalysisLevel.Ply4 },
            IncludeBookRollouts = true,
            BookRolloutLevels = { AnalysisLevel.XgRollerPlus },
            DiceRolls = { new DiceRoll(3, 1), new DiceRoll(6, 6) },
            PositionPattern = BoardPattern.Parse("[6,,0] [5,2,] [0,,-1]"),
        };

        var restored = FilterConfig.FromJson(original.ToJson());

        restored.Should().Be(original);
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
    public void ToJson_DepthLevels_SerializeAsDeclarationNames()
    {
        // AnalysisLevel carries its own type-level JsonStringEnumConverter (it is
        // owned by BgDataTypes_Lib), so it rides the wire as its declaration name
        // even though FilterConfig does not own the enum.
        var json = new FilterConfig
        {
            EvaluationLevels = { AnalysisLevel.XgRoller },
        }.ToJson();

        json.Should().Contain("\"XgRoller\"",
            "AnalysisLevel values must serialize as declaration names, not ordinals");
    }

    [Fact]
    public void FromJson_MissingDepthMembers_RestoreToInactiveDefaults()
    {
        // Legacy JSON written before the per-mode pairs existed — under the
        // retired flat AnalysisDepthClasses field or the retired shared
        // AnalysisLevels list — carries none of the current members; the
        // unrecognized fields are ignored and the pairs materialize as empty
        // level lists with every toggle off (an inactive facet), not null.
        // This is the accepted reset of old saved configs (Contact/Race
        // precedent).
        var restored = FilterConfig.FromJson(
            "{\"Players\":[\"Alice\"],\"AnalysisDepthClasses\":[\"Ply3\"],\"AnalysisLevels\":[\"Ply4\"]}");

        restored.IncludeEvaluations.Should().BeFalse();
        restored.EvaluationLevels.Should().NotBeNull().And.BeEmpty();
        restored.IncludeRollouts.Should().BeFalse();
        restored.RolloutLevels.Should().NotBeNull().And.BeEmpty();
        restored.IncludeBookRollouts.Should().BeFalse();
        restored.BookRolloutLevels.Should().NotBeNull().And.BeEmpty();
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
        restored.IncludeEvaluations.Should().BeFalse();
        restored.EvaluationLevels.Should().BeEmpty();
        restored.IncludeRollouts.Should().BeFalse();
        restored.RolloutLevels.Should().BeEmpty();
        restored.IncludeBookRollouts.Should().BeFalse();
        restored.BookRolloutLevels.Should().BeEmpty();
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

    // -----------------------------------------------------------------------
    //  Value equality — a config's identity is its content. This is the
    //  lib-side surface behind the FilterPanel's "nothing has changed, so
    //  Apply stays disabled" gate: the panel compares its built config with
    //  the last-committed one rather than re-materializing or serializing.
    // -----------------------------------------------------------------------

    /// <summary>
    /// A config with every member set away from its default — the baseline the
    /// member-sensitivity matrix perturbs one member at a time.
    /// </summary>
    private static FilterConfig Populated() => new()
    {
        Players = { "Alice", "Bob" },
        DecisionType = DecisionTypeOption.CheckerPlaysOnly,
        MatchScores = { "3a5a", "moneyJ" },
        ErrorMin = 0.05,
        ErrorMax = 0.50,
        MoveNumberMin = 1,
        MoveNumberMax = 20,
        ContactTypes = { ContactType.Race },
        PositionTypes = { PositionType.InnerBoard631 },
        PlayTypes = { PlayType.Make20Pt },
        IncludeEvaluations = true,
        EvaluationLevels = { AnalysisLevel.Ply3, AnalysisLevel.XgRoller },
        IncludeRollouts = true,
        RolloutLevels = { AnalysisLevel.Ply4 },
        IncludeBookRollouts = true,
        BookRolloutLevels = { AnalysisLevel.XgRollerPlus },
        DiceRolls = { new DiceRoll(3, 1), new DiceRoll(6, 6) },
        PositionPattern = BoardPattern.Parse("[6,,0] [5,2,] [0,,-1]"),
    };

    /// <summary>
    /// One mutation per public member of <see cref="FilterConfig"/>, each
    /// changing that member of <see cref="Populated"/> and nothing else.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, Action<FilterConfig>> MemberMutators =
        new Dictionary<string, Action<FilterConfig>>
        {
            [nameof(FilterConfig.Players)] = c => c.Players.Add("Carol"),
            [nameof(FilterConfig.DecisionType)] = c => c.DecisionType = DecisionTypeOption.CubeOnly,
            [nameof(FilterConfig.MatchScores)] = c => c.MatchScores.Remove("moneyJ"),
            [nameof(FilterConfig.ErrorMin)] = c => c.ErrorMin = 0.10,
            [nameof(FilterConfig.ErrorMax)] = c => c.ErrorMax = null,
            [nameof(FilterConfig.MoveNumberMin)] = c => c.MoveNumberMin = 2,
            [nameof(FilterConfig.MoveNumberMax)] = c => c.MoveNumberMax = null,
            [nameof(FilterConfig.ContactTypes)] = c => c.ContactTypes.Add(ContactType.Contact),
            [nameof(FilterConfig.PositionTypes)] = c => c.PositionTypes.Add(PositionType.VsTwoPlusUp),
            [nameof(FilterConfig.PlayTypes)] = c => c.PlayTypes.Clear(),
            [nameof(FilterConfig.IncludeEvaluations)] = c => c.IncludeEvaluations = false,
            [nameof(FilterConfig.EvaluationLevels)] = c => c.EvaluationLevels.Add(AnalysisLevel.Ply2),
            [nameof(FilterConfig.IncludeRollouts)] = c => c.IncludeRollouts = false,
            [nameof(FilterConfig.RolloutLevels)] = c => c.RolloutLevels.Add(AnalysisLevel.Ply2),
            [nameof(FilterConfig.IncludeBookRollouts)] = c => c.IncludeBookRollouts = false,
            [nameof(FilterConfig.BookRolloutLevels)] = c => c.BookRolloutLevels.Clear(),
            [nameof(FilterConfig.DiceRolls)] = c => c.DiceRolls.Add(new DiceRoll(5, 2)),
            [nameof(FilterConfig.PositionPattern)] = c => c.PositionPattern = BoardPattern.Parse("[6,,0]"),
        };

    /// <summary>
    /// The nine list facets, each as a pair of populating actions that add the
    /// same entries in opposite orders.
    /// </summary>
    private static readonly IReadOnlyDictionary<
        string, (Action<FilterConfig> Forward, Action<FilterConfig> Reversed)> ListFacetPermutations =
        new Dictionary<string, (Action<FilterConfig>, Action<FilterConfig>)>
        {
            [nameof(FilterConfig.Players)] =
                (c => { c.Players.Add("Alice"); c.Players.Add("Bob"); },
                 c => { c.Players.Add("Bob"); c.Players.Add("Alice"); }),
            [nameof(FilterConfig.MatchScores)] =
                (c => { c.MatchScores.Add("3a5a"); c.MatchScores.Add("moneyJ"); },
                 c => { c.MatchScores.Add("moneyJ"); c.MatchScores.Add("3a5a"); }),
            [nameof(FilterConfig.ContactTypes)] =
                (c => { c.ContactTypes.Add(ContactType.Contact); c.ContactTypes.Add(ContactType.Race); },
                 c => { c.ContactTypes.Add(ContactType.Race); c.ContactTypes.Add(ContactType.Contact); }),
            [nameof(FilterConfig.PositionTypes)] =
                (c => { c.PositionTypes.Add(PositionType.InnerBoard631); c.PositionTypes.Add(PositionType.VsTwoPlusUp); },
                 c => { c.PositionTypes.Add(PositionType.VsTwoPlusUp); c.PositionTypes.Add(PositionType.InnerBoard631); }),
            // PlayType has a single member today, so its "permutation" is the
            // degenerate one-entry case — still worth listing, so the facet is
            // covered the day a second member arrives.
            [nameof(FilterConfig.PlayTypes)] =
                (c => c.PlayTypes.Add(PlayType.Make20Pt),
                 c => c.PlayTypes.Add(PlayType.Make20Pt)),
            [nameof(FilterConfig.EvaluationLevels)] =
                (c => { c.EvaluationLevels.Add(AnalysisLevel.Ply3); c.EvaluationLevels.Add(AnalysisLevel.XgRoller); },
                 c => { c.EvaluationLevels.Add(AnalysisLevel.XgRoller); c.EvaluationLevels.Add(AnalysisLevel.Ply3); }),
            [nameof(FilterConfig.RolloutLevels)] =
                (c => { c.RolloutLevels.Add(AnalysisLevel.Ply2); c.RolloutLevels.Add(AnalysisLevel.Ply4); },
                 c => { c.RolloutLevels.Add(AnalysisLevel.Ply4); c.RolloutLevels.Add(AnalysisLevel.Ply2); }),
            [nameof(FilterConfig.BookRolloutLevels)] =
                (c => { c.BookRolloutLevels.Add(AnalysisLevel.Unknown); c.BookRolloutLevels.Add(AnalysisLevel.XgRollerPlus); },
                 c => { c.BookRolloutLevels.Add(AnalysisLevel.XgRollerPlus); c.BookRolloutLevels.Add(AnalysisLevel.Unknown); }),
            [nameof(FilterConfig.DiceRolls)] =
                (c => { c.DiceRolls.Add(new DiceRoll(3, 1)); c.DiceRolls.Add(new DiceRoll(6, 6)); },
                 c => { c.DiceRolls.Add(new DiceRoll(6, 6)); c.DiceRolls.Add(new DiceRoll(3, 1)); }),
        };

    [Fact]
    public void Equals_TwoDefaultConfigs_AreEqual()
    {
        var a = new FilterConfig();
        var b = new FilterConfig();

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_IsReflexiveAndSymmetric()
    {
        var config = Populated();
        var twin = Populated();

        config.Equals(config).Should().BeTrue();
        config.Should().Be(twin);
        twin.Should().Be(config);
        config.GetHashCode().Should().Be(twin.GetHashCode());
    }

    [Fact]
    public void Equals_NullOrOtherType_IsFalse()
    {
        // A fresh receiver per assertion: comparing a reference to null teaches
        // the compiler's flow analysis that it might be null, which would flag
        // every later use of a shared local.
        Populated().Equals((FilterConfig?)null).Should().BeFalse();
        Populated().Equals((object?)null).Should().BeFalse();
        Populated().Equals(Populated().ToJson()).Should().BeFalse();
    }

    [Fact]
    public void Equals_PopulatedVsDefault_IsFalse()
    {
        Populated().Should().NotBe(new FilterConfig());
    }

    [Fact]
    public void Equals_IsSensitiveToEveryMember()
    {
        // The member-sensitivity matrix: changing any one member, and nothing
        // else, must break equality. This is what catches a facet that was
        // added to the config but forgotten in Equals — when a future facet
        // makes this fail, the fix is to extend Equals/GetHashCode, not the
        // expectation.
        foreach (var (member, mutate) in MemberMutators)
        {
            var baseline = Populated();
            var altered = Populated();
            mutate(altered);

            altered.Should().NotBe(baseline,
                "changing {0} alone must make the config compare unequal", member);
        }
    }

    [Fact]
    public void MemberMutators_CoverEveryPublicMember()
    {
        // The guard that keeps the matrix above honest: a new member on
        // FilterConfig fails here until it is given a mutator, and the mutator
        // then fails Equals_IsSensitiveToEveryMember until equality learns
        // about the member. Neither test alone closes that gap.
        var members = typeof(FilterConfig)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name);

        MemberMutators.Keys.Should().BeEquivalentTo(members);
    }

    [Fact]
    public void Equals_ListFacets_AreOrderInsensitive_AndHashAgrees()
    {
        // Selection order is not part of a config's meaning, so a re-ordered
        // selection is not a change. The hash must agree, or a permuted-but-
        // equal config would go missing from a hash-based lookup.
        foreach (var (facet, (forward, reversed)) in ListFacetPermutations)
        {
            var a = new FilterConfig();
            var b = new FilterConfig();
            forward(a);
            reversed(b);

            a.Should().Be(b, "{0} must compare order-insensitively", facet);
            a.GetHashCode().Should().Be(b.GetHashCode(),
                "{0}'s hash must agree with its order-insensitive equality", facet);
        }
    }

    [Fact]
    public void Equals_AllListFacetsPermutedTogether_IsStillEqual()
    {
        var forward = new FilterConfig();
        var reversed = new FilterConfig();

        foreach (var (_, (addForward, addReversed)) in ListFacetPermutations)
        {
            addForward(forward);
            addReversed(reversed);
        }

        forward.Should().Be(reversed);
        forward.GetHashCode().Should().Be(reversed.GetHashCode());
    }

    [Fact]
    public void Equals_DuplicateEntries_CompareAsMultiset()
    {
        // Documented, accepted edge: the list facets compare as multisets, so a
        // doubled entry differs from a single one even though Build() produces
        // the same filter from both. Duplicates are unreachable through the
        // panel's checkbox UI, and the error direction — reporting a difference
        // that materializes identically — is the safe one for a dirty-state
        // gate. Pinned here so a change to it is a deliberate one.
        var doubled = new FilterConfig { Players = { "Alice", "Alice" } };
        var single = new FilterConfig { Players = { "Alice" } };

        doubled.Should().NotBe(single);
    }

    [Fact]
    public void Equals_NullListMember_MatchesEmptyAndDoesNotThrow()
    {
        // An explicit JSON null lands as a null list member. Equality treats it
        // as the empty list it means and stays total — Equals must never throw.
        var withNullList = FilterConfig.FromJson("""{"Players":null}""");

        withNullList.Should().Be(new FilterConfig());
        withNullList.GetHashCode().Should().Be(new FilterConfig().GetHashCode());
    }

    [Fact]
    public void Equals_PositionPattern_DelegatesToBoardPatternEquality()
    {
        var a = new FilterConfig { PositionPattern = BoardPattern.Parse("[6,,0] [5,2,]") };
        var b = new FilterConfig { PositionPattern = BoardPattern.Parse("[5,2,] [6,,0]") };

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact]
    public void Equals_PositionPatternNullVsEmpty_AreDistinct()
    {
        // Null and the empty pattern are both inactive facets, but they remain
        // distinct values everywhere else on this type; equality follows suit
        // rather than inventing a third rule.
        var none = new FilterConfig { PositionPattern = null };
        var empty = new FilterConfig { PositionPattern = BoardPattern.Empty };

        none.Should().NotBe(empty);
    }

    [Fact]
    public void Equals_ConfigsBuiltDifferentWays_AreEqual()
    {
        // The consumer's actual question: a config restored from storage and
        // one rebuilt from panel state describe the same filtering, so the
        // Apply gate must see them as equal.
        var restored = FilterConfig.FromJson(Populated().ToJson());

        restored.Should().Be(Populated());
        restored.GetHashCode().Should().Be(Populated().GetHashCode());
    }

    // -----------------------------------------------------------------------
    //  GetInvalidFields — the match-score field rule (halheinrich/backgammon#121,
    //  the shape #39 booked for #23: one FilterField member, one FieldRules
    //  row delegating to the facet's own grammar). The grammar's rules are
    //  pinned in MatchScoreTokenTests; these pin the reporting.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("3a5a")]
    [InlineData("1a5aC")]
    [InlineData("moneyJ")]
    [InlineData("moneyNJ")]
    public void GetInvalidFields_AcceptedScoreTokens_ReturnEmptySet(string token)
    {
        new FilterConfig { MatchScores = { token } }
            .GetInvalidFields().Should().BeEmpty();
    }

    [Theory]
    [InlineData("garbage")]
    [InlineData("0a5a")]
    [InlineData("3a5aC")]
    [InlineData("money")]
    public void GetInvalidFields_FaultedScoreToken_BlamesOnlyMatchScores(string token)
    {
        new FilterConfig { MatchScores = { token } }
            .GetInvalidFields().Should().Equal(FilterField.MatchScores);
    }

    [Fact]
    public void GetInvalidFields_OneFaultedTokenAmongValidOnes_StillBlamesTheField()
    {
        // The field says "some entry here is wrong" — one bad token in a list
        // of good ones is enough, exactly as Build treats it.
        new FilterConfig { MatchScores = { "3a5a", "money", "moneyNJ" } }
            .GetInvalidFields().Should().Equal(FilterField.MatchScores);
    }

    [Fact]
    public void GetInvalidFields_RetiredScoreToken_StillReportsTheFacetActive()
    {
        // Activity is presence, validity is content: a retired token is still
        // a filter the user set, so it stays countable in the panel's
        // hidden-filters signal while being named here.
        var cfg = new FilterConfig { MatchScores = { "money" } };

        cfg.GetActiveFacets().Should().Equal(FilterFacet.MatchScores);
        cfg.GetInvalidFields().Should().Equal(FilterField.MatchScores);
    }

    [Fact]
    public void GetInvalidFields_NullScoreToken_IsReportedRatherThanThrowing()
    {
        // An explicit JSON null reaches the list as a null entry; the query
        // must judge it, not throw on it.
        var restored = FilterConfig.FromJson("""{"MatchScores":["3a5a",null]}""");

        restored.GetInvalidFields().Should().Equal(FilterField.MatchScores);
        var act = () => restored.Build();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetInvalidFields_AgreesWithBuild_OnEveryScoreTokenCandidate()
    {
        // The two surfaces route through MatchScoreToken.GetFault, so "named
        // here" and "rejected by Build" must coincide for every token. Swept
        // rather than enumerated so a future grammar rule cannot quietly hold
        // on one surface only.
        string[] candidates =
        [
            "3a5a", "1a5aC", "1a1a", "1a2aC", "4A5A", " 4a5a ",
            "moneyJ", "moneyNJ", "MONEYNJ", " moneyj ",
            "money", "MONEY", " money ",
            "garbage", "", "   ", "0a5a", "5a0a", "3a5aC", "1a1aC",
            "4 a 5a", "3a5a5a", "moneyX", "9999999999a5a",
        ];

        foreach (string token in candidates)
        {
            var cfg = new FilterConfig { MatchScores = { token } };
            bool named = cfg.GetInvalidFields().Contains(FilterField.MatchScores);
            bool rejected = Record.Exception(() => cfg.Build()) is not null;

            rejected.Should().Be(
                named,
                "GetInvalidFields and Build must agree for MatchScores token '{0}'",
                token);
        }
    }

    // -----------------------------------------------------------------------
    //  The retired money token, end to end through the config surface
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData("money")]
    [InlineData("MONEY")]
    [InlineData(" money ")]
    public void Build_RetiredMoneyToken_Throws(string retired)
    {
        var cfg = new FilterConfig { MatchScores = { retired } };
        var act = () => cfg.Build();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RetiredMoneyToken_CarriesTheRetiredFaultAndItsReplacements()
    {
        // The typed fact a consumer reads to word its own explanation: the
        // fault distinguishes retired vocabulary from a typo, and the
        // replacements travel as values. No sentence crosses the API, so
        // nothing here pins one.
        var cfg = new FilterConfig { MatchScores = { "3a5a", "money" } };

        cfg.GetInvalidFields().Should().Equal(FilterField.MatchScores);

        var faults = cfg.MatchScores
            .Select(t => (Token: t, Fault: MatchScoreToken.GetFault(t)))
            .Where(x => x.Fault != MatchScoreTokenFault.None)
            .ToList();

        faults.Should().ContainSingle();
        faults[0].Token.Should().Be(MatchScoreToken.RetiredMoney);
        faults[0].Fault.Should().Be(MatchScoreTokenFault.Retired);
        MatchScoreToken.RetiredMoneyReplacements.Should().Equal(
            MatchScoreToken.MoneyWithJacoby, MatchScoreToken.MoneyWithoutJacoby);
    }

    [Fact]
    public void RetiredMoneyToken_IsDistinguishableFromAMalformedToken()
    {
        // The distinction the fault vocabulary exists to carry: both are
        // named by the same field, and only the grammar tells them apart.
        MatchScoreToken.GetFault("money").Should().Be(MatchScoreTokenFault.Retired);
        MatchScoreToken.GetFault("garbage").Should().Be(MatchScoreTokenFault.Malformed);

        new FilterConfig { MatchScores = { "money" } }
            .GetInvalidFields().Should().Equal(FilterField.MatchScores);
        new FilterConfig { MatchScores = { "garbage" } }
            .GetInvalidFields().Should().Equal(FilterField.MatchScores);
    }

    // -----------------------------------------------------------------------
    //  The saved-filter path. A document written before the split carries the
    //  retired token; it must LOAD intact (validity is a query, not a gate on
    //  assignment — the #39 posture, so the offending value can be shown back
    //  to the user) and then surface the same typed verdict at apply. Never a
    //  silent drop, never a silent no-match.
    // -----------------------------------------------------------------------

    [Fact]
    public void FromJson_RetiredMoneyToken_LoadsIntactAndIsReportedRatherThanRejected()
    {
        var restored = FilterConfig.FromJson("""{"MatchScores":["3a5a","money"]}""");

        restored.MatchScores.Should().Equal("3a5a", "money");
        restored.GetInvalidFields().Should().Equal(FilterField.MatchScores);
        MatchScoreToken.GetFault(restored.MatchScores[1])
            .Should().Be(MatchScoreTokenFault.Retired);
    }

    [Fact]
    public void TryFromJson_RetiredMoneyToken_SucceedsRatherThanFallingBackToDefault()
    {
        // The tolerant restore must not treat a token retired after the
        // document was written as corruption: losing the whole config would
        // hide the very thing the user has to fix.
        var ok = FilterConfig.TryFromJson("""{"MatchScores":["money"]}""", out var restored);

        ok.Should().BeTrue();
        restored.MatchScores.Should().Equal("money");
        restored.GetInvalidFields().Should().Equal(FilterField.MatchScores);
    }

    [Fact]
    public void SavedDocument_RetiredMoneyToken_SurvivesRoundTripAndIsInvalidAtApply()
    {
        // The full saved-filter path: a NamedFilterCollection entry snapshots
        // its config through ToJson/FromJson, so this exercises the storage
        // round-trip the consumer actually uses. The token survives it (the
        // document is not silently rewritten), the retrieved config names the
        // field, the grammar names the fault, and Build refuses.
        var saved = NamedFilterCollection.Empty
            .With("Money sessions", new FilterConfig { MatchScores = { "money" } });

        var reloaded = NamedFilterCollection.FromJson(saved.ToJson());
        var config = reloaded.GetConfig("Money sessions");

        config.MatchScores.Should().Equal("money");
        config.GetActiveFacets().Should().Equal(FilterFacet.MatchScores);
        config.GetInvalidFields().Should().Equal(FilterField.MatchScores);
        MatchScoreToken.GetFault(config.MatchScores[0])
            .Should().Be(MatchScoreTokenFault.Retired);

        var act = () => config.Build();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SavedDocument_RetiredMoneyToken_NeverSilentlyMatchesNothing()
    {
        // The failure mode the loud verdict replaces: before the retirement
        // had a rule row, a stored "money" would pass a consumer's validity
        // gate and then either throw unguarded at Build or — had the token
        // simply stopped matching — silently return an empty result set. Both
        // surfaces must refuse it instead, and no DecisionFilterSet may exist
        // that was built from it.
        var config = NamedFilterCollection.Empty
            .With("Legacy", new FilterConfig { MatchScores = { "money" } })
            .GetConfig("Legacy");

        config.GetInvalidFields().Should().NotBeEmpty(
            "a consumer gating on GetInvalidFields must refuse to apply this");

        var act = () => config.Build();
        act.Should().Throw<ArgumentException>(
            "and a consumer that builds regardless must fail loud, never return an empty set");
    }

    // -----------------------------------------------------------------------
    //  GetInvalidFields — the move-number bound rules
    //  (halheinrich/backgammon#119, the same shape the error bounds already
    //  had: two FilterField members, two FieldRules rows delegating to the
    //  facet's own filter). The rules themselves are pinned in
    //  MoveNumberFilterTests; these pin the reporting, and the Build path that
    //  reaches them.
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(null, null)]   // both absent: the rule constrains values, never presence
    [InlineData(1, null)]      // one-sided, at the admissible edge
    [InlineData(null, 1)]
    [InlineData(1, 1)]         // the opening-decision filter
    [InlineData(5, 5)]         // equal bounds: inclusive, so a single-move filter
    [InlineData(3, 10)]
    [InlineData(3, null)]
    [InlineData(null, 10)]
    public void GetInvalidFields_AdmissibleMoveNumberBounds_ReturnsEmptySet(int? min, int? max)
    {
        new FilterConfig { MoveNumberMin = min, MoveNumberMax = max }
            .GetInvalidFields().Should().BeEmpty();
    }

    [Theory]
    [InlineData(0, null)]      // the floor is one, so zero is a bound that names no decision
    [InlineData(0, 10)]
    [InlineData(-5, 10)]
    [InlineData(int.MinValue, null)]
    public void GetInvalidFields_InadmissibleMoveNumberMinAlone_BlamesOnlyMoveNumberMin(
        int? min, int? max)
    {
        new FilterConfig { MoveNumberMin = min, MoveNumberMax = max }
            .GetInvalidFields().Should().Equal(FilterField.MoveNumberMin);
    }

    [Theory]
    [InlineData(null, 0)]
    [InlineData(1, 0)]         // the min is also "above" the max, but only because the max is bad
    [InlineData(10, 0)]
    [InlineData(null, -5)]
    public void GetInvalidFields_InadmissibleMoveNumberMaxAlone_BlamesOnlyMoveNumberMax(
        int? min, int? max)
    {
        // Blame honesty, the same way the error facet states it: an
        // inadmissible max drags the pair out of order as a side effect, and
        // that consequence must not red the field the user got right.
        new FilterConfig { MoveNumberMin = min, MoveNumberMax = max }
            .GetInvalidFields().Should().Equal(FilterField.MoveNumberMax);
    }

    [Fact]
    public void GetInvalidFields_BothMoveNumberBoundsSubFloor_BlamesBoth()
    {
        new FilterConfig { MoveNumberMin = -5, MoveNumberMax = 0 }
            .GetInvalidFields().Should().Equal(
                FilterField.MoveNumberMin, FilterField.MoveNumberMax);
    }

    [Fact]
    public void GetInvalidFields_MoveNumberMinExceedsMax_BlamesBothOnlyWhenBothAreAdmissible()
    {
        // The case halheinrich/backgammon#119 exists for: neither bound is
        // wrong on its own, the pair is, so both are named and the user chooses
        // which end to move.
        new FilterConfig { MoveNumberMin = 10, MoveNumberMax = 3 }
            .GetInvalidFields().Should().Equal(
                FilterField.MoveNumberMin, FilterField.MoveNumberMax);
    }

    [Fact]
    public void GetInvalidFields_InvalidMoveNumberBounds_StillReportTheFacetActive()
    {
        // Activity is presence, validity is content: a broken bound is still a
        // filter the user set, so it stays countable in the panel's
        // hidden-filters signal while being named here.
        var cfg = new FilterConfig { MoveNumberMin = 10, MoveNumberMax = 3 };

        cfg.GetActiveFacets().Should().Equal(FilterFacet.MoveNumberRange);
        cfg.GetInvalidFields().Should().Equal(
            FilterField.MoveNumberMin, FilterField.MoveNumberMax);
    }

    [Fact]
    public void GetInvalidFields_MoveNumberAndErrorFacets_AreJudgedIndependently()
    {
        // The two range facets share a shape, not a rule: a valid error range
        // must not be dragged into the move-number facet's verdict, and the
        // floors genuinely differ — zero is admissible for an error magnitude,
        // never for a 1-based move ordinal.
        new FilterConfig { ErrorMin = 0.0, ErrorMax = 0.0, MoveNumberMin = 0 }
            .GetInvalidFields().Should().Equal(FilterField.MoveNumberMin);
    }

    [Fact]
    public void GetInvalidFields_AgreesWithBuild_OnEveryMoveNumberBoundCombination()
    {
        // The two surfaces consult the same predicates on MoveNumberFilter, so
        // "named here" and "rejected by Build" must coincide for this facet.
        // Swept rather than enumerated so a future bound rule cannot quietly
        // hold on one surface only.
        int?[] candidates = [null, 0, 1, 2, 10, -5, int.MinValue, int.MaxValue];

        foreach (var min in candidates)
        {
            foreach (var max in candidates)
            {
                var cfg = new FilterConfig { MoveNumberMin = min, MoveNumberMax = max };
                var named = cfg.GetInvalidFields().Count > 0;
                var rejected = Record.Exception(() => cfg.Build()) is not null;

                rejected.Should().Be(
                    named,
                    "GetInvalidFields and Build must agree for MoveNumberMin={0}, MoveNumberMax={1}",
                    min,
                    max);
            }
        }
    }

    [Fact]
    public void FromJson_LegacyMoveNumberBounds_LoadAndAreReportedRatherThanRejected()
    {
        // The reason validity is a query and not a gate on assignment: a
        // document written before the rule existed must still round-trip, so
        // the consumer can show the offending value back to the user instead of
        // losing it to a failed restore.
        var restored = FilterConfig.FromJson("""{"MoveNumberMin":0,"MoveNumberMax":-3}""");

        restored.MoveNumberMin.Should().Be(0);
        restored.MoveNumberMax.Should().Be(-3);
        restored.GetInvalidFields()
                .Should().Equal(FilterField.MoveNumberMin, FilterField.MoveNumberMax);
    }

    [Fact]
    public void TryFromJson_LegacyMisorderedMoveNumberBounds_SucceedsRatherThanFallingBackToDefault()
    {
        var ok = FilterConfig.TryFromJson("""{"MoveNumberMin":10,"MoveNumberMax":3}""", out var restored);

        ok.Should().BeTrue("a rule stated after the document was written must not corrupt the restore");
        restored.MoveNumberMin.Should().Be(10);
        restored.MoveNumberMax.Should().Be(3);
        restored.GetInvalidFields()
                .Should().Equal(FilterField.MoveNumberMin, FilterField.MoveNumberMax);
    }

    // -----------------------------------------------------------------------
    //  The saved-filter path for the move-number bounds. A config stored before
    //  the rule existed carries a misordered or sub-floor pair; it must LOAD
    //  intact and then surface the same typed verdict at apply. Never a silent
    //  drop, never a silent no-match — which is exactly what a stored
    //  min > max used to be (halheinrich/backgammon#119).
    // -----------------------------------------------------------------------

    [Fact]
    public void SavedDocument_MisorderedMoveNumberBounds_SurviveRoundTripAndAreInvalidAtApply()
    {
        // The full saved-filter path: a NamedFilterCollection entry snapshots
        // its config through ToJson/FromJson, so this exercises the storage
        // round-trip the consumer actually uses. The bounds survive it (the
        // document is not silently rewritten), the retrieved config names both
        // fields, and Build refuses.
        var saved = NamedFilterCollection.Empty
            .With("Late game", new FilterConfig { MoveNumberMin = 10, MoveNumberMax = 3 });

        var reloaded = NamedFilterCollection.FromJson(saved.ToJson());
        var config = reloaded.GetConfig("Late game");

        config.MoveNumberMin.Should().Be(10);
        config.MoveNumberMax.Should().Be(3);
        config.GetActiveFacets().Should().Equal(FilterFacet.MoveNumberRange);
        config.GetInvalidFields().Should().Equal(
            FilterField.MoveNumberMin, FilterField.MoveNumberMax);

        var act = () => config.Build();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SavedDocument_SubFloorMoveNumberBound_SurvivesRoundTripAndIsInvalidAtApply()
    {
        // The other half of the gate, and a different exception type: a bound
        // wrong on its own value, not a pair out of order.
        var saved = NamedFilterCollection.Empty
            .With("From zero", new FilterConfig { MoveNumberMin = 0, MoveNumberMax = 10 });

        var config = NamedFilterCollection.FromJson(saved.ToJson()).GetConfig("From zero");

        config.MoveNumberMin.Should().Be(0);
        config.GetInvalidFields().Should().Equal(FilterField.MoveNumberMin);

        var act = () => config.Build();
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void SavedDocument_MisorderedMoveNumberBounds_NeverSilentlyMatchNothing()
    {
        // The failure mode the loud verdict replaces: before the bounds had
        // rule rows, a stored min > max passed a consumer's validity gate,
        // built a filter without complaint, and then matched nothing at all —
        // indistinguishable from a session with no mistakes in it. Both
        // surfaces must refuse it instead, and no DecisionFilterSet may exist
        // that was built from it.
        var config = NamedFilterCollection.Empty
            .With("Legacy", new FilterConfig { MoveNumberMin = 10, MoveNumberMax = 3 })
            .GetConfig("Legacy");

        config.GetInvalidFields().Should().NotBeEmpty(
            "a consumer gating on GetInvalidFields must refuse to apply this");

        var act = () => config.Build();
        act.Should().Throw<ArgumentException>(
            "and a consumer that builds regardless must fail loud, never return an empty set");
    }
}
