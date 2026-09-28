using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// The ranking in force through <see cref="FilteredDecisionIterator"/>
/// (SPEC-scoring §2a): the caller states it at construction, and every entry
/// point judges each decision under it — each row built for it, each record
/// filtered through its view for it. The file is synthesized inline
/// (<see cref="XgFileBuilder"/>, then real XG bytes or the library's JSON), so
/// these tests read nothing from <c>TestData</c> and gate.
/// </summary>
public class FilteredDecisionIteratorRankingTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    // -----------------------------------------------------------------------
    //  The file — one game of a 7-point match, one analysed checker play: the
    //  opening 3-1 by P1, with three candidates at two depths.
    //
    //    8/5 6/5       4-ply  +0.25   depth first's best
    //    13/10 6/5     2-ply  +0.50   equity's best; not scored under depth first
    //    24/23 13/10   4-ply   0.00   erred by 0.50 (equity) or 0.25 (depth first)
    // -----------------------------------------------------------------------

    private static readonly Play DeepBest = [new(8, 5), new(6, 5)];
    private static readonly Play ShallowHigh = [new(13, 10), new(6, 5)];
    private static readonly Play DeepWorst = [new(24, 23), new(13, 10)];

    private static XgFile OpeningPlayed(Play played)
    {
        var builder = XgFileBuilder.ForMatch(7, "P1", "P2");
        builder.AddGame().Play(
            XgPlayer.Player1, new DiceRoll(3, 1), played,
            [
                new XgPlayCandidate(DeepBest, equity: 0.25, ply: 4),
                new XgPlayCandidate(ShallowHigh, equity: 0.50, ply: 2),
                new XgPlayCandidate(DeepWorst, equity: 0.00, ply: 4),
            ]);
        return builder.Build();
    }

    // -----------------------------------------------------------------------
    //  Every public entry point, run over one file
    // -----------------------------------------------------------------------

    public static TheoryData<string> EntryPoints() =>
    [
        nameof(FilteredDecisionIterator.IterateXgDirectory),
        nameof(FilteredDecisionIterator.IterateJsonDirectory),
        nameof(FilteredDecisionIterator.IterateXgStreams),
        nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams),
        nameof(FilteredDecisionIterator.IterateXgStreamDiagrams),
    ];

    /// <summary>
    /// The identifiers of the decisions <paramref name="entryPoint"/> yields
    /// for <paramref name="file"/> through an iterator built with
    /// <paramref name="filters"/> under <paramref name="ranking"/> — the file
    /// staged as the entry point reads it: a directory holding its XG bytes or
    /// its JSON, or a named stream of its bytes.
    /// </summary>
    private static List<DecisionId> Run(
        string entryPoint, XgFile file, DecisionFilterSet filters, PlayRanking ranking)
    {
        var iterator = new FilteredDecisionIterator(filters, ranking, NullLogger);
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "match.xg"), XgFileWriter.ToBytes(file));
            File.WriteAllText(Path.Combine(dir, "match.json"), XgFileReader.ToJson(file));
            XgFileStream[] streams = [new("match.xg", new MemoryStream(XgFileWriter.ToBytes(file)))];

            return entryPoint switch
            {
                nameof(FilteredDecisionIterator.IterateXgDirectory) =>
                    iterator.IterateXgDirectory(dir).Select(r => r.Id).ToList(),
                nameof(FilteredDecisionIterator.IterateJsonDirectory) =>
                    iterator.IterateJsonDirectory(dir).Select(r => r.Id).ToList(),
                nameof(FilteredDecisionIterator.IterateXgStreams) =>
                    iterator.IterateXgStreams(streams).Select(r => r.Id).ToList(),
                nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams) =>
                    iterator.IterateXgDirectoryDiagrams(dir).Select(d => d.Id).ToList(),
                nameof(FilteredDecisionIterator.IterateXgStreamDiagrams) =>
                    iterator.IterateXgStreamDiagrams(streams).Select(d => d.Id).ToList(),
                _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, null),
            };
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static DecisionFilterSet ErredBetween(double? min, double? max) =>
        new DecisionFilterSet().Add(new ErrorRangeFilter(min, max));

    // -----------------------------------------------------------------------
    //  "Erred by more than x" under the caller's ranking
    // -----------------------------------------------------------------------

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void UnderDepthFirst_AMoveTheRankingDoesNotScore_NeverPassesAnErrorRange(string entryPoint)
    {
        // The played 2-ply move beats depth first's 4-ply best on equity: not
        // scored there, so no error range admits it — under equity the same
        // move is the best, scored 0, and passes. The file, the filter and the
        // entry point are the same; only the ranking the caller states differs.
        var file = OpeningPlayed(ShallowHigh);

        Run(entryPoint, file, ErredBetween(0.0, null), PlayRanking.DepthFirst)
            .Should().BeEmpty("a move the ranking does not score has no error");
        Run(entryPoint, file, ErredBetween(0.0, null), PlayRanking.Equity)
            .Should().ContainSingle("under equity the same move is scored, with error 0");
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void AScoredMovesError_IsMeasuredUnderTheCallersRanking(string entryPoint)
    {
        // 24/23 13/10 erred by 0.25 against depth first's best and by 0.50
        // against equity's: each exact range admits it under its own ranking
        // only.
        var file = OpeningPlayed(DeepWorst);

        Run(entryPoint, file, ErredBetween(0.25, 0.25), PlayRanking.DepthFirst).Should().ContainSingle();
        Run(entryPoint, file, ErredBetween(0.25, 0.25), PlayRanking.Equity).Should().BeEmpty();
        Run(entryPoint, file, ErredBetween(0.50, 0.50), PlayRanking.Equity).Should().ContainSingle();
        Run(entryPoint, file, ErredBetween(0.50, 0.50), PlayRanking.DepthFirst).Should().BeEmpty();
    }

    // -----------------------------------------------------------------------
    //  Rows are built for the caller's ranking
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(PlayRanking.Equity, PlayerResultKind.Scored)]
    [InlineData(PlayRanking.DepthFirst, PlayerResultKind.NotScored)]
    public void Rows_AreBuiltForTheCallersRanking(PlayRanking ranking, PlayerResultKind result)
    {
        var iterator = new FilteredDecisionIterator(new DecisionFilterSet(), ranking, NullLogger);
        XgFileStream[] streams =
            [new("match.xg", new MemoryStream(XgFileWriter.ToBytes(OpeningPlayed(ShallowHigh))))];

        var row = iterator.IterateXgStreams(streams).Should().ContainSingle().Which;

        row.Ranking.Should().Be(ranking);
        row.Result.Should().Be(result);
    }

    // -----------------------------------------------------------------------
    //  Construction
    // -----------------------------------------------------------------------

    [Fact]
    public void Constructor_UndefinedRanking_Throws()
    {
        var act = () => new FilteredDecisionIterator(new DecisionFilterSet(), (PlayRanking)99, NullLogger);

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("ranking");
    }
}
