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
}
