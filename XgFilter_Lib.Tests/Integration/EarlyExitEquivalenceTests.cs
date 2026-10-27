using System.Text.Json;
using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// Early exit never changes the result (halheinrich/backgammon#373, Hal's
/// ruling of 2026-10-08). For a filter set whose filters honour the hint
/// contract on <see cref="IDecisionFilter"/>, and any source, the filtering
/// operation — <see cref="FilteredDecisionIterator.IterateFile"/> and
/// <see cref="FilteredDecisionIterator.IterateFileDiagrams"/>, with the
/// set's skip and advance hints wired to the producer — yields exactly the
/// decisions <see cref="DecisionFilterSet.Matches"/> accepts from what the
/// producer yields for that source with no callbacks, in the same order.
/// That is what lets a consumer refilter retained, unfiltered decisions with
/// <c>Matches</c> and agree with one that streams.
///
/// <para>
/// Each case runs both shapes — rows compared by their whole wire form,
/// records by identity — over synthesized sources (<see cref="SyntheticSources"/>),
/// <c>.xg</c> matches and an <c>.xgp</c> position alike, so the suite gates.
/// Each case also shows its hint operated: every filter is wrapped in an
/// <see cref="Observed"/> that counts the decisions the operation evaluated
/// and the votes each hint cast, so a case cannot pass because nothing was
/// cut.
/// </para>
/// </summary>
public class EarlyExitEquivalenceTests
{
    private const PlayRanking Ranking = PlayRanking.Equity;

    /// <summary>A source as a walk would hand it to the filtering operation.</summary>
    private sealed record Source(string Name, XgFile File);

    /// <summary>
    /// A row's value: its JSON wire form under the producer's registered
    /// metadata, which carries every stored fact — Error and Equity under the
    /// ranking in force included — and so tells rows apart that share an id.
    /// </summary>
    private static string WireForm(DecisionRow row) =>
        JsonSerializer.Serialize(row, BgDataTypesJsonContext.Default.DecisionRow);

    /// <summary>
    /// The decisions the operation yields for <paramref name="source"/> under
    /// <paramref name="filters"/>, against those <c>Matches</c> accepts from
    /// the producer's no-callback output — rows as wire forms, records as ids
    /// — and the count of decisions the producer yields with no callbacks.
    /// </summary>
    private static (List<string> Actual, List<string> Expected, int Produced) Run(
        Observation filters, Source source, bool diagrams)
    {
        var iterator = new FilteredDecisionIterator(filters.Set, Ranking, NullLogger<FilteredDecisionIterator>.Instance);
        var unobserved = filters.Unobserved();
        var options = new XgIteratorOptions(Ranking: Ranking);

        if (diagrams)
        {
            var produced = XgDecisionIterator.IterateDiagramRequests(source.File, source.Name, options: options).ToList();
            var actual = iterator.IterateFileDiagrams(source.File, source.Name).Select(d => d.Id.ToString()).ToList();
            var expected = produced.Where(d => unobserved.Matches(d.ViewFor(Ranking)))
                .Select(d => d.Id.ToString()).ToList();
            return (actual, expected, produced.Count);
        }
        else
        {
            var produced = XgDecisionIterator.Iterate(source.File, source.Name, options: options).ToList();
            var actual = iterator.IterateFile(source.File, source.Name).Select(WireForm).ToList();
            var expected = produced.Where(r => unobserved.Matches(r)).Select(WireForm).ToList();
            return (actual, expected, produced.Count);
        }
    }

    /// <summary>
    /// A filter set whose every filter is wrapped in an <see cref="Observed"/>,
    /// with the observers in the set's order.
    /// </summary>
    private sealed record Observation(DecisionFilterSet Set, IReadOnlyList<Observed> Filters)
    {
        /// <summary>
        /// The same filters without their observers, so judging the expected
        /// side leaves the observers' counts to the operation alone.
        /// </summary>
        public DecisionFilterSet Unobserved()
        {
            var set = new DecisionFilterSet();
            foreach (var observed in Filters)
                set.Add(observed.Inner);
            return set;
        }
    }

    private static Observation Observe(params IDecisionFilter[] filters)
    {
        var observers = filters.Select(f => new Observed(f)).ToArray();
        var set = new DecisionFilterSet();
        foreach (var observer in observers)
            set.Add(observer);
        return new Observation(set, observers);
    }

    public static TheoryData<bool> Shapes() => [false, true];

    /// <summary>
    /// Asserts the equivalence for every source of a case, and returns the
    /// totals across them: decisions evaluated by the operation (the first
    /// observer sees every one — <c>Matches</c> short-circuits after it),
    /// decisions the producer yields with no callbacks, and decisions kept.
    /// </summary>
    private static (int Evaluated, int Produced, int Kept) AssertEquivalent(
        Observation filters, bool diagrams, params Source[] sources)
    {
        int produced = 0, kept = 0;
        foreach (var source in sources)
        {
            var (actual, expected, count) = Run(filters, source, diagrams);
            actual.Should().Equal(expected,
                $"early exit must not change what {source.Name} yields ({(diagrams ? "records" : "rows")})");
            produced += count;
            kept += expected.Count;
        }
        return (filters.Filters[0].Evaluated, produced, kept);
    }

    // -----------------------------------------------------------------------
    //  Match skip — the filter for the name and error facets
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void MatchSkip_ThePairsFilter_UnderNames(bool diagrams)
    {
        // Neither Carol nor Dave is listed, nor the position's P1 or P2: each
        // of those matches is skipped at its header. In Alice-Bob, Alice's
        // first decision is rejected and Bob's next one accepted: a rejection
        // never ends the game or the match.
        var filters = Observe(new PlayerErrorFilter(["Bob"]));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("alice-bob.xg", SyntheticSources.TwoGameMatch("Alice", "Bob")),
            new Source("carol-dave.xg", SyntheticSources.TwoGameMatch("Carol", "Dave")),
            new Source("position.xgp", SyntheticSources.Position()));

        var player = filters.Filters[0];
        player.MatchSkips.Should().BeGreaterThan(0, "the match-skip hint operated");
        kept.Should().BeGreaterThan(0, "Bob's decisions survive Alice's rejected ones");
        evaluated.Should().BeLessThan(produced, "the skipped matches were never iterated");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void MatchSkip_ThePairsFilter_BoundsAlone_SkipNoMatch(bool diagrams)
    {
        // With no names listed the doubler's half of every decision is
        // judged, so no header rules a match out: "neither header name
        // listed" over an empty list would skip every match, Carol-Dave's and
        // the position's included. Every synthesized decision has an error
        // (an analysed play's 0, the position's no-double 0.15), so the widest
        // range keeps them all.
        var filters = Observe(new PlayerErrorFilter([], min: 0.0));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("alice-bob.xg", SyntheticSources.TwoGameMatch("Alice", "Bob")),
            new Source("carol-dave.xg", SyntheticSources.TwoGameMatch("Carol", "Dave")),
            new Source("position.xg", SyntheticSources.Position()));

        filters.Filters[0].MatchSkips.Should().Be(0, "bounds alone never vote to skip a match");
        evaluated.Should().Be(produced, "every match was iterated");
        kept.Should().Be(produced);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void TheTakersCube_AdmittedThroughTheTaker_TheScoreFacetReadsTheDoublersSide(bool diagrams)
    {
        // Alice, 5-away to Bob's 4-away, doubles and Bob takes: Bob's take
        // admits the cube, and the score facet reads it from Alice's side,
        // 5a4a. Alice's plays are 5a4a but not Bob's decisions; Bob's are his,
        // but 4a5a. So the cube is the one decision kept, and Carol-Dave is
        // skipped at its header.
        var builder = XgFileBuilder.ForMatch(5, "Alice", "Bob");
        builder.AddGame(score1: 0, score2: 1)
            .CubeDecision(XgPlayer.Player1, SyntheticSources.Equities,
                doublerAction: CubeAction.Double, takerAction: CubeAction.Take)
            .WithMoves(4);
        var file = builder.Build();
        var filters = Observe(new PlayerErrorFilter(["Bob"]), new MatchScoreFilter(["5a4a"]));

        var (_, _, kept) = AssertEquivalent(filters, diagrams,
            new Source("alice-bob.xg", file),
            new Source("carol-dave.xg", SyntheticSources.TwoGameMatch("Carol", "Dave")));

        kept.Should().Be(1, "the cube Bob took, alone");
        filters.Filters[0].MatchSkips.Should().BeGreaterThan(0, "Carol-Dave was skipped at its header");
        new FilteredDecisionIterator(filters.Unobserved(), Ranking, NullLogger<FilteredDecisionIterator>.Instance)
            .IterateFileDiagrams(file, "alice-bob.xg")
            .Should().ContainSingle().Which.Kind.Should().Be(DecisionKind.Cube);
    }

    // -----------------------------------------------------------------------
    //  Game skip — the match-score and move-number filters
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void GameSkip_TheMatchScoreFilter(bool diagrams)
    {
        // Game 1 stands 5-away/5-away, matching neither target, and is skipped
        // at its header; game 2 stands 4-away/5-away, and both players'
        // decisions in it pass, one orientation each.
        var filters = Observe(new MatchScoreFilter(["4a5a", "5a4a"]));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("match.xg", SyntheticSources.TwoGameMatch()),
            new Source("position.xgp", SyntheticSources.Position()));

        filters.Filters[0].GameSkips.Should().BeGreaterThan(0, "the game-skip hint operated");
        kept.Should().BeGreaterThan(0);
        evaluated.Should().BeLessThan(produced);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void GameSkip_TheMoveNumberFilter_OnANonStandardStart(bool diagrams)
    {
        var builder = XgFileBuilder.ForMatch(5, "P1", "P2");
        builder.AddGame().WithMoves(4);
        builder.AddGame(score1: 1, score2: 0, initialPosition: SyntheticSources.NonStandardStart).WithMoves(4);
        var filters = Observe(new MoveNumberFilter(max: SyntheticSources.MaxMoves));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("match.xg", builder.Build()),
            new Source("position.xgp", SyntheticSources.Position()));

        filters.Filters[0].GameSkips.Should().BeGreaterThan(0, "the non-standard game was skipped at its header");
        kept.Should().BeGreaterThan(0, "the standard game's decisions survive");
        evaluated.Should().BeLessThan(produced);
    }

    // -----------------------------------------------------------------------
    //  Game advance — the move-number filter: a valid cutoff operates
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void GameAdvance_AValidCutoffOperates_OnADecisionThatFailsMatches(bool diagrams)
    {
        // At most move 2: move 3 of game 1 fails Matches and votes to advance
        // the game anyway, which the hint contract allows (no later decision
        // in the game can pass); moves 4 to 6 are never evaluated. Game 2's
        // moves 1 and 2 survive.
        var file = SyntheticSources.TwoGameMatch(moves: SyntheticSources.MaxMoves);
        var filters = Observe(new MoveNumberFilter(max: 2));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("match.xg", file),
            new Source("position.xgp", SyntheticSources.Position()));

        var moves = filters.Filters[0];
        moves.GameAdvancesOnRejected.Should().BeGreaterThan(0,
            "the advance vote came from a decision the filter itself rejects");
        moves.EvaluatedMoves.Where(m => m is not null).Should().Equal([1, 2, 3, 1, 2, 3],
            "each game is cut after the first decision past the maximum");
        kept.Should().BeGreaterThan(0, "an eligible decision in the next game survives the cut");
        evaluated.Should().BeLessThan(produced);
    }

    // -----------------------------------------------------------------------
    //  No invalid cutoff loses a match — the match-score and player filters
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void NoInvalidCutoff_ARejectedOrientation_NeverEndsTheGameOrTheMatch(bool diagrams)
    {
        // The game stands player 1 5-away, player 2 4-away: player 1's
        // decisions are 5a4a, rejected; player 2's, after each of them in the
        // same game, are 4a5a, accepted. The match-advance hint is asked on
        // every rejected decision and must not cut.
        var builder = XgFileBuilder.ForMatch(5, "P1", "P2");
        builder.AddGame(score1: 0, score2: 1).WithMoves(4);
        var filters = Observe(new MatchScoreFilter(["4a5a"]));

        var (_, _, kept) = AssertEquivalent(filters, diagrams,
            new Source("match.xg", builder.Build()),
            new Source("position.xgp", SyntheticSources.Position()));

        var score = filters.Filters[0];
        score.MatchAdvanceAsked.Should().BeGreaterThan(0, "the hint was consulted on the rejected decisions");
        score.MatchAdvances.Should().Be(0);
        kept.Should().Be(2, "both of player 2's decisions survive; the position's player 1 stands 5a4a");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void NoInvalidCutoff_APlayerRejection_NeverEndsTheGameOrTheMatch(bool diagrams)
    {
        // A player filter combined with a hinting one: player 1's decisions
        // are rejected, player 2's accepted in the same game; the move-number
        // cutoff ends each game where it should and nowhere earlier.
        var filters = Observe(new PlayerErrorFilter(["P2"]), new MoveNumberFilter(max: 4));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("match.xg", SyntheticSources.TwoGameMatch(moves: SyntheticSources.MaxMoves)),
            new Source("position.xgp", SyntheticSources.Position()));

        kept.Should().Be(4, "player 2's moves 2 and 4 in each game");
        filters.Filters[1].GameAdvances.Should().BeGreaterThan(0);
        evaluated.Should().BeLessThan(produced);
    }

    // -----------------------------------------------------------------------
    //  Match advance
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void MatchAdvance_AContractHonouringVote_CutsTheRestOfTheMatch(bool diagrams)
    {
        // None of the built-in filters casts a true match-advance vote on a
        // decision the operation yields (MatchScoreFilter's would fall only in
        // a game its own game skip has removed), so a filter through the
        // public seam drives this hint: decisions at the match's opening
        // score only, and a vote to advance the match once either side has
        // fewer than 5 to go — sound, since away scores never rise.
        var builder = XgFileBuilder.ForMatch(5, "P1", "P2");
        builder.AddGame().WithMoves(4);
        builder.AddGame(score1: 1, score2: 0).WithMoves(4);
        builder.AddGame(score1: 1, score2: 1).WithMoves(4);
        var filters = Observe(new OpeningScoreOnly());

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("match.xg", builder.Build()),
            new Source("position.xgp", SyntheticSources.Position()));

        filters.Filters[0].MatchAdvances.Should().BeGreaterThan(0, "the match-advance hint operated");
        kept.Should().Be(4, "game 1's decisions");
        evaluated.Should().BeLessThan(produced, "game 2's later decisions and game 3 were never iterated");
    }

    // -----------------------------------------------------------------------
    //  Every hint at once
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryBuiltInHintAtOnce(bool diagrams)
    {
        // Neither Carol nor Dave is listed (match skip); in Alice-Bob game 1
        // stands 5a5a and game 2 4a5a for Alice (admitted) and 5a4a for Bob
        // (rejected by score and player alike), game 3 starts off the
        // standard position at 4-away/4-away (skipped by both game hints), and
        // every standard game is cut after its first decision past move 3.
        var standard = XgFileBuilder.ForMatch(5, "Alice", "Bob");
        standard.AddGame().WithMoves(SyntheticSources.MaxMoves);
        standard.AddGame(score1: 1, score2: 0).WithMoves(SyntheticSources.MaxMoves);
        standard.AddGame(score1: 1, score2: 1, initialPosition: SyntheticSources.NonStandardStart).WithMoves(4);
        var filters = Observe(
            new PlayerErrorFilter(["Alice", "P1"]),
            new MatchScoreFilter(["5a5a", "4a5a"]),
            new MoveNumberFilter(max: 3));

        var (evaluated, produced, kept) = AssertEquivalent(filters, diagrams,
            new Source("alice-bob.xg", standard.Build()),
            new Source("carol-dave.xg", SyntheticSources.TwoGameMatch("Carol", "Dave")),
            new Source("position.xgp", SyntheticSources.Position()));

        kept.Should().BeGreaterThan(0);
        evaluated.Should().BeLessThan(produced);
    }

    // -----------------------------------------------------------------------
    //  The .xgp case (halheinrich/backgammon#392)
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(Shapes))]
    public void AnXgpsSelectedDecision_IsNeverReplacedByOneAStopVoteSaw(bool diagrams)
    {
        // A position holding a cube decision before a checker play, under a
        // filter that admits cubes and votes to advance the match on a
        // standalone position's cube — sound, since the only later decision is
        // the play, which it rejects. Read as an .xgp, the producer yields the
        // play alone, so nothing passes; a vote cast on the cube before the
        // selection once made the walk yield the cube instead. Read as an .xg,
        // both decisions are yielded and the cube passes.
        var position = SyntheticSources.Position();
        var filters = Observe(new CubesOfStandalonePositions());

        var (_, _, keptAsXgp) = AssertEquivalent(filters, diagrams, new Source("position.xgp", position));
        var (_, _, keptAsXg) = AssertEquivalent(filters, diagrams, new Source("position.xg", position));

        keptAsXgp.Should().Be(0, "an .xgp yields its play, which this filter rejects");
        keptAsXg.Should().Be(1, "an .xg yields the cube too, which this filter admits");
    }

    // -----------------------------------------------------------------------
    //  Filters through the public seam, each honouring the hint contract
    // -----------------------------------------------------------------------

    /// <summary>Decisions at a 5-point match's opening score, 5-away/5-away.</summary>
    private sealed class OpeningScoreOnly : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) =>
            data.Session.Match(_ => false, m => m.OnRollNeeds == 5 && m.OpponentNeeds == 5);

        public bool ShouldAdvanceMatch(IDecisionFilterData data) =>
            data.Session.Match(_ => false, m => m.OnRollNeeds < 5 || m.OpponentNeeds < 5);
    }

    /// <summary>Cube decisions; votes to advance the match on a standalone position's cube.</summary>
    private sealed class CubesOfStandalonePositions : IDecisionFilter
    {
        public bool Matches(IDecisionFilterData data) => data.Kind == DecisionKind.Cube;

        public bool ShouldAdvanceMatch(IDecisionFilterData data) =>
            data.Kind == DecisionKind.Cube && data.MoveNumber is null;
    }

    /// <summary>
    /// Wraps one filter, delegating every member unchanged, and records what
    /// the filtering operation asked of it: each decision it evaluated (with
    /// its move number), and each skip and advance vote it cast.
    /// </summary>
    private sealed class Observed(IDecisionFilter inner) : IDecisionFilter, IMatchFilter
    {
        public IDecisionFilter Inner { get; } = inner;
        public int Evaluated => EvaluatedMoves.Count;
        public List<int?> EvaluatedMoves { get; } = [];
        public int MatchSkips { get; private set; }
        public int GameSkips { get; private set; }
        public int GameAdvances { get; private set; }
        public int GameAdvancesOnRejected { get; private set; }
        public int MatchAdvanceAsked { get; private set; }
        public int MatchAdvances { get; private set; }

        public bool Matches(IDecisionFilterData data)
        {
            EvaluatedMoves.Add(data.MoveNumber);
            return Inner.Matches(data);
        }

        public bool ShouldAdvanceGame(IDecisionFilterData data)
        {
            bool vote = Inner.ShouldAdvanceGame(data);
            if (vote)
            {
                GameAdvances++;
                if (!Inner.Matches(data))
                    GameAdvancesOnRejected++;
            }
            return vote;
        }

        public bool ShouldAdvanceMatch(IDecisionFilterData data)
        {
            MatchAdvanceAsked++;
            bool vote = Inner.ShouldAdvanceMatch(data);
            if (vote)
                MatchAdvances++;
            return vote;
        }

        public bool ShouldSkipMatch(IMatchInfo match)
        {
            bool vote = Inner is IMatchFilter f && f.ShouldSkipMatch(match);
            if (vote)
                MatchSkips++;
            return vote;
        }

        public bool ShouldSkipGame(IGameInfo game)
        {
            bool vote = Inner is IMatchFilter f && f.ShouldSkipGame(game);
            if (vote)
                GameSkips++;
            return vote;
        }
    }
}
