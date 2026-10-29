using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;
using XgFilter_Lib.Tests.Helpers;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// Pins the contract that <see cref="FilteredDecisionIterator"/> preserves
/// every yielded record's <see cref="DecisionId"/> bit-for-bit (record
/// equality) from the producer through to the consumer. The filter pipeline
/// is a row-level inclusion / exclusion stage — it never constructs or
/// transforms records — so Ids must survive both the pass-all case and the
/// drop case for every public delegate entry point.
///
/// <para>
/// Each public entry point of <see cref="FilteredDecisionIterator"/> is pinned
/// independently rather than coupling to the internal shared-helper
/// structure: tests cover the row shape (<c>IterateXgDirectory</c>) and the
/// diagram shape (<c>IterateXgDirectoryDiagrams</c>), cross-multiplied
/// against the two Id carriers (<c>.xg</c> → <see cref="XgDecisionId"/>,
/// <c>.xgp</c> → <see cref="XgpDecisionId"/>), plus a drop variant per
/// shape with an explicit non-vacuousness gate.
/// </para>
///
/// <para>
/// Every source is synthesized (<see cref="SyntheticSources"/>), written to a
/// per-test temp directory and read back through XG's reader on both sides,
/// so these tests read nothing from <c>TestData</c> and gate.
/// </para>
/// </summary>
public class FilteredDecisionIteratorIdTests
{
    /// <summary>A match of checker plays, and a position read as a match: its cube decision and its play.</summary>
    private static readonly (string Name, XgFile File)[] XgSources =
    [
        ("match.xg", SyntheticSources.TwoGameMatch()),
        ("cube-and-play.xg", SyntheticSources.Position()),
    ];

    /// <summary>Two positions, each yielding its play alone.</summary>
    private static readonly (string Name, XgFile File)[] XgpSources =
    [
        ("first.xgp", SyntheticSources.Position()),
        ("second.xgp", SyntheticSources.Position()),
    ];

    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet filters) =>
        new FilteredDecisionIterator(filters, PlayRanking.Equity, NullLogger);

    // -----------------------------------------------------------------------
    //  Pass-all passthrough — IDs survive unchanged on every delegate entry
    //  point, for both Id carrier types.
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectory_PassthroughOnXgFixtures_IdsSurvive()
    {
        // Stage a temp dir containing only .xg sources so the consumer's
        // EnumerateXgFormatFiles walk and the raw-producer walk run over the
        // same path set, exercising the XgDecisionId carrier exclusively.
        var tempDir = Stage(XgSources);
        try
        {
            var producerIds = RawIterate(tempDir,
                    (file, sf, state, callbacks) => XgDecisionIterator.Iterate(file, sf, state, callbacks))
                .Select(r => r.Id).ToList();

            var consumerIds = NewIterator(new DecisionFilterSet())
                .IterateXgDirectory(tempDir).Select(r => r.Id).ToList();

            producerIds.Should().NotBeEmpty(
                "the .xg sources must yield at least one row to make this test meaningful");
            consumerIds.Should().AllBeOfType<XgDecisionId>(
                ".xg files must stamp XgDecisionId, not the .xgp shape");
            consumerIds.Should().Equal(producerIds,
                "pass-all filter must preserve every Id, in order, as a record");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_PassthroughOnXgFixtures_IdsSurvive()
    {
        var tempDir = Stage(XgSources);
        try
        {
            var producerIds = RawIterate(tempDir,
                    (file, sf, state, callbacks) => XgDecisionIterator.IterateDiagramRequests(file, sf, state, callbacks))
                .Select(d => d.Id).ToList();

            var consumerIds = NewIterator(new DecisionFilterSet())
                .IterateXgDirectoryDiagrams(tempDir).Select(d => d.Id).ToList();

            producerIds.Should().NotBeEmpty();
            consumerIds.Should().AllBeOfType<XgDecisionId>();
            consumerIds.Should().Equal(producerIds,
                "diagram-shape pass-all filter must preserve every Id, in order");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void IterateXgDirectory_PassthroughOnXgpFixtures_IdsSurvive()
    {
        var tempDir = Stage(XgpSources);
        try
        {
            var producerIds = RawIterate(tempDir,
                    (file, sf, state, callbacks) => XgDecisionIterator.Iterate(file, sf, state, callbacks))
                .Select(r => r.Id).ToList();

            var consumerIds = NewIterator(new DecisionFilterSet())
                .IterateXgDirectory(tempDir).Select(r => r.Id).ToList();

            producerIds.Should().NotBeEmpty();
            consumerIds.Should().AllBeOfType<XgpDecisionId>(
                ".xgp files must stamp XgpDecisionId, not the .xg shape");
            consumerIds.Should().Equal(producerIds,
                "pass-all filter must preserve every XgpDecisionId, in order");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_PassthroughOnXgpFixtures_IdsSurvive()
    {
        var tempDir = Stage(XgpSources);
        try
        {
            var producerIds = RawIterate(tempDir,
                    (file, sf, state, callbacks) => XgDecisionIterator.IterateDiagramRequests(file, sf, state, callbacks))
                .Select(d => d.Id).ToList();

            var consumerIds = NewIterator(new DecisionFilterSet())
                .IterateXgDirectoryDiagrams(tempDir).Select(d => d.Id).ToList();

            producerIds.Should().NotBeEmpty();
            consumerIds.Should().AllBeOfType<XgpDecisionId>();
            consumerIds.Should().Equal(producerIds,
                "diagram-shape pass-all filter must preserve every XgpDecisionId, in order");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    //  Drop semantics — surviving rows' Ids match the producer's subset,
    //  with an explicit non-vacuousness gate (kept > 0 AND dropped > 0).
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectory_FilterDropsCubes_SurvivingIdsMatchProducerSubset()
    {
        // DecisionTypeFilter(CheckerPlaysOnly) drops cube rows. The drop
        // contract is "surviving rows are intact and ordered as the producer
        // would have ordered them"; pin via Id-equality on the post-filter
        // list against the producer's hand-filtered checker-play subset.
        // Non-vacuousness is asserted explicitly on the raw sources so a
        // change that eliminates cube rows surfaces loudly rather than
        // turning this test into a tautology.
        var tempDir = Stage([.. XgSources, .. XgpSources]);
        try
        {
            var producerAll = RawIterate(tempDir,
                (file, sf, state, callbacks) => XgDecisionIterator.Iterate(file, sf, state, callbacks)).ToList();
            var producerKept = producerAll.Where(r => r.Kind == DecisionKind.CheckerPlay).ToList();
            var producerDropped = producerAll.Where(r => r.Kind == DecisionKind.Cube).ToList();

            producerKept.Should().NotBeEmpty(
                "non-vacuousness gate: the sources must yield at least one checker-play row");
            producerDropped.Should().NotBeEmpty(
                "non-vacuousness gate: the sources must yield at least one cube row, " +
                "otherwise CheckerPlaysOnly is a no-op and this test pins nothing");

            var consumerKept = NewIterator(
                new DecisionFilterSet().Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly)))
                .IterateXgDirectory(tempDir).ToList();

            consumerKept.Select(r => r.Id).Should().Equal(producerKept.Select(r => r.Id),
                "surviving Ids must equal the producer's checker-play subset, in order");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_FilterDropsCubes_SurvivingIdsMatchProducerSubset()
    {
        // Mirror of the DecisionRow drop test on the diagram-shape delegate
        // entry point. Pinning each public entry point independently — even
        // though they share an internal helper today, the public contract
        // is per-entry-point.
        var tempDir = Stage([.. XgSources, .. XgpSources]);
        try
        {
            var producerAll = RawIterate(tempDir,
                (file, sf, state, callbacks) => XgDecisionIterator.IterateDiagramRequests(file, sf, state, callbacks)).ToList();
            var producerKept = producerAll.Where(d => d.Kind == DecisionKind.CheckerPlay).ToList();
            var producerDropped = producerAll.Where(d => d.Kind == DecisionKind.Cube).ToList();

            producerKept.Should().NotBeEmpty(
                "non-vacuousness gate: the sources must yield at least one checker-play diagram");
            producerDropped.Should().NotBeEmpty(
                "non-vacuousness gate: the sources must yield at least one cube diagram");

            var consumerKept = NewIterator(
                new DecisionFilterSet().Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly)))
                .IterateXgDirectoryDiagrams(tempDir).ToList();

            consumerKept.Select(d => d.Id).Should().Equal(producerKept.Select(d => d.Id),
                "surviving Ids must equal the producer's checker-play subset, in order");
        }
        finally { Directory.Delete(tempDir, recursive: true); }
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Writes <paramref name="sources"/> as XG's binary container into a
    /// fresh temp directory and returns the path, so the consumer's
    /// <c>EnumerateXgFormatFiles</c> walk and the raw-producer walk align
    /// on the same path set.
    /// </summary>
    private static string Stage((string Name, XgFile File)[] sources)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        foreach (var (name, file) in sources)
            File.WriteAllBytes(Path.Combine(tempDir, name), SyntheticSources.XgBytes(file));
        return tempDir;
    }

    /// <summary>
    /// Walks <paramref name="dir"/> the same way <see cref="FilteredDecisionIterator"/>
    /// does (all <c>*.xg</c> then all <c>*.xgp</c>) and yields the raw
    /// producer output via <paramref name="source"/>. Mirrors the consumer's
    /// path enumeration so the two passes line up element-for-element.
    /// </summary>
    private static IEnumerable<T> RawIterate<T>(
        string dir,
        Func<XgFile, string?, XgIteratorState?, XgIteratorCallbacks?, IEnumerable<T>> source)
        where T : notnull
    {
        var paths = Directory.EnumerateFiles(dir, "*.xg")
            .Concat(Directory.EnumerateFiles(dir, "*.xgp"));
        foreach (var path in paths)
        {
            var file = XgFileReader.ReadFile(path);
            foreach (var item in source(file, Path.GetFileName(path), null, null))
                yield return item;
        }
    }
}
