using System.Text.Json;
using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// The filtering operation over one already-parsed source,
/// <see cref="FilteredDecisionIterator.IterateFile"/> and
/// <see cref="FilteredDecisionIterator.IterateFileDiagrams"/>: what it yields,
/// that every walk filters each decoded source through it, how it checks its
/// arguments, and that the caller's opening book reaches the producer
/// through it. All but the opening-book test synthesize their sources
/// (<see cref="SyntheticSources"/>) and gate; that one needs the real book,
/// which no public surface can synthesize, and is local-only.
/// </summary>
public class FilteredDecisionIteratorFileTests
{
    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet filters) =>
        new(filters, PlayRanking.Equity, NullLogger);

    /// <summary>
    /// A row's value: its JSON wire form under the producer's registered
    /// metadata, which carries every stored fact — Error and Equity under the
    /// ranking in force included — and so tells rows apart that share an id.
    /// </summary>
    private static string WireForm(DecisionRow row) =>
        JsonSerializer.Serialize(row, BgDataTypesJsonContext.Default.DecisionRow);

    // -----------------------------------------------------------------------
    //  What the operation yields
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateFile_YieldsTheRowsOfTheSourceThatPassTheFilters_InTheProducersOrder()
    {
        var file = SyntheticSources.TwoGameMatch();
        var filters = new DecisionFilterSet().Add(new PlayerErrorFilter(["P2"]));

        var rows = NewIterator(filters).IterateFile(file, "match.xg").ToList();

        var expected = XgDecisionIterator.Iterate(file, "match.xg")
            .Where(r => filters.Matches(r))
            .Select(WireForm)
            .ToList();
        expected.Should().NotBeEmpty();
        rows.Select(WireForm).Should().Equal(expected);
        rows.Should().OnlyContain(r => r.Player == "P2");
    }

    [Fact]
    public void IterateFileDiagrams_YieldsTheRecordsOfTheSourceThatPassTheFilters_InTheProducersOrder()
    {
        var file = SyntheticSources.TwoGameMatch();
        var filters = new DecisionFilterSet().Add(new PlayerErrorFilter(["P2"]));

        var records = NewIterator(filters).IterateFileDiagrams(file, "match.xg").ToList();

        var expected = XgDecisionIterator.IterateDiagramRequests(file, "match.xg")
            .Where(d => filters.Matches(d.ViewFor(PlayRanking.Equity)))
            .Select(d => d.Id)
            .ToList();
        expected.Should().NotBeEmpty();
        records.Select(d => d.Id).Should().Equal(expected);
        records.Should().OnlyContain(d => d.Player == "P2");
    }

    [Fact]
    public void ErrorBoundsAlone_SkipNoMatch_ThroughTheProducersMatchSkip()
    {
        // A configuration with an error range and no names: the producer's
        // match skip (SkipMatchAt) is asked about a match neither of whose
        // players anyone listed, and must not skip it — the doubler's half of
        // every decision is judged. Every synthesized play is the analysed
        // played move, an error of 0, so the widest range keeps them all.
        var file = SyntheticSources.TwoGameMatch("Carol", "Dave");
        var filters = new FilterConfig { ErrorMin = 0.0 }.Build();

        var rows = NewIterator(filters).IterateFile(file, "match.xg").Select(WireForm).ToList();
        var records = NewIterator(filters).IterateFileDiagrams(file, "match.xg").Select(d => d.Id).ToList();

        rows.Should().NotBeEmpty()
            .And.Equal(XgDecisionIterator.Iterate(file, "match.xg").Select(WireForm));
        records.Should().Equal(SyntheticSources.ProducerIds(file, "match.xg"));
    }

    // -----------------------------------------------------------------------
    //  Every walk is "read each source, then filter each decoded one"
    // -----------------------------------------------------------------------

    public static TheoryData<string> Walks() =>
    [
        nameof(FilteredDecisionIterator.IterateXgDirectory),
        nameof(FilteredDecisionIterator.IterateJsonDirectory),
        nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams),
        nameof(FilteredDecisionIterator.IterateXgStreams),
        nameof(FilteredDecisionIterator.IterateXgStreamDiagrams),
    ];

    [Theory]
    [MemberData(nameof(Walks))]
    public void EveryWalk_YieldsWhatTheFilteringOperationYieldsForEachSource(string walk)
    {
        // A filter set that skips games, cuts games short and rejects rows,
        // over one source: whatever the walk yields for it is what the
        // filtering operation yields for the parsed file under the same name.
        var file = SyntheticSources.TwoGameMatch(moves: 6);
        var filters = new DecisionFilterSet()
            .Add(new PlayerErrorFilter(["P1"]))
            .Add(new MoveNumberFilter(max: 3));
        var iterator = NewIterator(filters);
        bool json = walk == nameof(FilteredDecisionIterator.IterateJsonDirectory);
        string name = json ? "match.json" : "match.xg";
        bool diagrams = walk.EndsWith("Diagrams", StringComparison.Ordinal);

        var expected = diagrams
            ? iterator.IterateFileDiagrams(file, name).Select(d => d.Id).ToList()
            : iterator.IterateFile(file, name).Select(r => r.Id).ToList();

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(dir);
        try
        {
            if (json)
                File.WriteAllBytes(Path.Combine(dir, name), SyntheticSources.JsonBytes(file));
            else
                File.WriteAllBytes(Path.Combine(dir, name), SyntheticSources.XgBytes(file));
            XgFileStream[] entries = [SyntheticSources.XgEntry(name, file)];

            var actual = walk switch
            {
                nameof(FilteredDecisionIterator.IterateXgDirectory) => iterator.IterateXgDirectory(dir).Select(r => r.Id),
                nameof(FilteredDecisionIterator.IterateJsonDirectory) => iterator.IterateJsonDirectory(dir).Select(r => r.Id),
                nameof(FilteredDecisionIterator.IterateXgDirectoryDiagrams) => iterator.IterateXgDirectoryDiagrams(dir).Select(d => d.Id),
                nameof(FilteredDecisionIterator.IterateXgStreams) => iterator.IterateXgStreams(entries).Select(r => r.Id),
                nameof(FilteredDecisionIterator.IterateXgStreamDiagrams) => iterator.IterateXgStreamDiagrams(entries).Select(d => d.Id),
                _ => throw new ArgumentOutOfRangeException(nameof(walk), walk, null),
            };

            expected.Should().NotBeEmpty();
            actual.ToList().Should().Equal(expected);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    // -----------------------------------------------------------------------
    //  Arguments are checked at the call
    // -----------------------------------------------------------------------

    public static TheoryData<string> Shapes() =>
    [
        nameof(FilteredDecisionIterator.IterateFile),
        nameof(FilteredDecisionIterator.IterateFileDiagrams),
    ];

    private static Func<object> Call(string shape, XgFile file, string sourceName)
    {
        var iterator = NewIterator(new DecisionFilterSet());
        return shape switch
        {
            nameof(FilteredDecisionIterator.IterateFile) => () => iterator.IterateFile(file, sourceName),
            nameof(FilteredDecisionIterator.IterateFileDiagrams) => () => iterator.IterateFileDiagrams(file, sourceName),
            _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, null),
        };
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ANullFile_IsRefusedAtTheCall(string shape)
    {
        Call(shape, null!, "match.xg").Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("file");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ANullOrBlankSourceName_IsRefusedAtTheCall(string shape)
    {
        var file = SyntheticSources.TwoGameMatch();

        Call(shape, file, null!).Should().Throw<ArgumentNullException>().Which.ParamName.Should().Be("sourceName");
        Call(shape, file, "  ").Should().Throw<ArgumentException>().Which.ParamName.Should().Be("sourceName");
    }

    // -----------------------------------------------------------------------
    //  The opening book reaches the producer through the operation
    // -----------------------------------------------------------------------

    private static string Fixture(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "FixtureFiles", name);
        if (!File.Exists(path))
            throw new Xunit.Sdk.XunitException(
                $"Expected fixture not present: {path}. OpeningBookV2.ob is copied from the eXtreme " +
                "Gammon 2 install directory into TestData/FixtureFiles/.");
        return path;
    }

    /// <summary>
    /// <c>ajhhBG0407.xg</c> game 9 move 1 is an opening play whose candidates
    /// XG stamped as book hits, the best of them backed by a 4-ply rollout in
    /// the real book (ConvertXgToJson_Lib's fixture (a)). Without the book the
    /// producer states only the bare book facts; with it, through either shape
    /// of the filtering operation, the hits carry their rollouts. No public
    /// surface can synthesize a book or a book-stamped candidate, so this pin
    /// reads the real ones and is local-only.
    /// </summary>
    [Fact]
    [Trait("Category", "RequiresFixtureFiles")]
    public void TheCallersOpeningBook_ReachesTheProducer_ThroughTheFilteringOperation()
    {
        var book = OpeningBook.Load(Fixture("OpeningBookV2.ob"));
        var file = XgFileReader.ReadFile(Fixture("ajhhBG0407.xg"));
        const string name = "ajhhBG0407.xg";
        var withBook = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, NullLogger, book);
        var withoutBook = new FilteredDecisionIterator(new DecisionFilterSet(), PlayRanking.Equity, NullLogger);

        static CheckerPlayDecision Record(IEnumerable<BgDecisionData> records) =>
            records.OfType<CheckerPlayDecision>().Single(d => d.Game == 9 && d.MoveNumber == 1);
        static DecisionRow Row(IEnumerable<DecisionRow> rows) =>
            rows.Single(r => r.Kind == DecisionKind.CheckerPlay && r.Game == 9 && r.MoveNumber == 1);

        var bareHits = Record(withoutBook.IterateFileDiagrams(file, name)).Decision.Plays
            .Where(p => p.AnalysisMode == AnalysisMode.BookRollout).ToList();
        var enrichedHits = Record(withBook.IterateFileDiagrams(file, name)).Decision.Plays
            .Where(p => p.AnalysisMode == AnalysisMode.BookRollout).ToList();

        bareHits.Should().NotBeEmpty().And.OnlyContain(p => p.RolloutTrials == null && p.AnalysisLevel == AnalysisLevel.Unknown);
        enrichedHits.Should().HaveSameCount(bareHits).And.Contain(p => p.RolloutTrials != null,
            "the book's rollouts reach the records the operation yields");

        var bareRow = Row(withoutBook.IterateFile(file, name));
        var enrichedRow = Row(withBook.IterateFile(file, name));
        bareRow.AnalysisMode.Should().Be(AnalysisMode.BookRollout);
        bareRow.AnalysisLevel.Should().Be(AnalysisLevel.Unknown);
        enrichedRow.AnalysisMode.Should().Be(AnalysisMode.BookRollout);
        enrichedRow.AnalysisLevel.Should().Be(AnalysisLevel.Ply4,
            "the best play's book entry is a 4-ply rollout, and the row is built with the book");
    }
}
