using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using XgFilter_Lib.Enums;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib.Tests.Integration;

/// <summary>
/// Pins the contract that <see cref="FilteredDecisionIterator"/> preserves
/// every yielded record's <see cref="DecisionId"/> bit-for-bit (record
/// equality) from the producer through to the consumer. The filter pipeline
/// is a row-level inclusion / exclusion stage — it never constructs or
/// transforms records — so Ids must survive both the pass-all case and the
/// drop case for every public delegate slot.
///
/// <para>
/// Each public slot of <see cref="FilteredDecisionIterator"/> is pinned
/// independently rather than coupling to the internal shared-helper
/// structure: tests cover the row shape (<c>IterateXgDirectory</c>) and the
/// diagram shape (<c>IterateXgDirectoryDiagrams</c>), cross-multiplied
/// against the two Id carriers (<c>.xg</c> → <see cref="XgDecisionId"/>,
/// <c>.xgp</c> → <see cref="XgpDecisionId"/>), plus a drop variant per
/// shape with an explicit non-vacuousness gate.
/// </para>
/// </summary>
public class FilteredDecisionIteratorIdTests
{
    private static readonly string FixtureDir = Path.Combine(
        AppContext.BaseDirectory, "TestData", "FixtureFiles");

    private static readonly ILogger<FilteredDecisionIterator> NullLogger =
        NullLogger<FilteredDecisionIterator>.Instance;

    private static FilteredDecisionIterator NewIterator(DecisionFilterSet filters) =>
        new FilteredDecisionIterator(filters, NullLogger);

    // -----------------------------------------------------------------------
    //  Pass-all passthrough — IDs survive unchanged on every delegate slot,
    //  for both Id carrier types.
    // -----------------------------------------------------------------------

    [Fact]
    public void IterateXgDirectory_PassthroughOnXgFixtures_IdsSurvive()
    {
        // Stage a temp dir containing only the .xg fixtures so the consumer's
        // EnumerateXgFormatFiles walk and the raw-producer walk run over the
        // same path set, exercising the XgDecisionId carrier exclusively.
        var tempDir = StageFixturesByExtension("*.xg");
        try
        {
            var producerIds = RawIterate(tempDir,
                    (file, sf, state, callbacks) => XgDecisionIterator.Iterate(file, sf, state, callbacks))
                .Select(r => r.Id).ToList();

            var consumerIds = NewIterator(new DecisionFilterSet())
                .IterateXgDirectory(tempDir).Select(r => r.Id).ToList();

            producerIds.Should().NotBeEmpty(
                "the .xg fixture corpus must yield at least one row to make this test meaningful");
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
        var tempDir = StageFixturesByExtension("*.xg");
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
        var tempDir = StageFixturesByExtension("*.xgp");
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
        var tempDir = StageFixturesByExtension("*.xgp");
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
        // Non-vacuousness is asserted explicitly on the raw corpus so a
        // future fixture rotation that eliminates cube rows surfaces loudly
        // rather than turning this test into a tautology.
        var producerAll = RawIterate(FixtureDir,
            (file, sf, state, callbacks) => XgDecisionIterator.Iterate(file, sf, state, callbacks)).ToList();
        var producerKept = producerAll.Where(r => !r.IsCube).ToList();
        var producerDropped = producerAll.Where(r => r.IsCube).ToList();

        producerKept.Should().NotBeEmpty(
            "non-vacuousness gate: the corpus must yield at least one checker-play row");
        producerDropped.Should().NotBeEmpty(
            "non-vacuousness gate: the corpus must yield at least one cube row, " +
            "otherwise CheckerPlaysOnly is a no-op and this test pins nothing");

        var consumerKept = NewIterator(
            new DecisionFilterSet().Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly)))
            .IterateXgDirectory(FixtureDir).ToList();

        consumerKept.Select(r => r.Id).Should().Equal(producerKept.Select(r => r.Id),
            "surviving Ids must equal the producer's checker-play subset, in order");
    }

    [Fact]
    public void IterateXgDirectoryDiagrams_FilterDropsCubes_SurvivingIdsMatchProducerSubset()
    {
        // Mirror of the DecisionRow drop test on the diagram-shape delegate
        // slot. Pinning each public slot independently — even though they
        // share an internal helper today, the public contract is per-slot.
        var producerAll = RawIterate(FixtureDir,
            (file, sf, state, callbacks) => XgDecisionIterator.IterateDiagramRequests(file, sf, state, callbacks)).ToList();
        var producerKept = producerAll.Where(d => !d.Decision.IsCube).ToList();
        var producerDropped = producerAll.Where(d => d.Decision.IsCube).ToList();

        producerKept.Should().NotBeEmpty(
            "non-vacuousness gate: the corpus must yield at least one checker-play diagram");
        producerDropped.Should().NotBeEmpty(
            "non-vacuousness gate: the corpus must yield at least one cube diagram");

        var consumerKept = NewIterator(
            new DecisionFilterSet().Add(new DecisionTypeFilter(DecisionTypeOption.CheckerPlaysOnly)))
            .IterateXgDirectoryDiagrams(FixtureDir).ToList();

        consumerKept.Select(d => d.Id).Should().Equal(producerKept.Select(d => d.Id),
            "surviving Ids must equal the producer's checker-play subset, in order");
    }

    // -----------------------------------------------------------------------
    //  Helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Stages every fixture matching <paramref name="pattern"/> from
    /// <see cref="FixtureDir"/> into a fresh temp directory and returns the
    /// path. Used to isolate a single file extension so the consumer's
    /// <c>EnumerateXgFormatFiles</c> walk and the raw-producer walk align
    /// on the same path set.
    /// </summary>
    private static string StageFixturesByExtension(string pattern)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        foreach (var src in Directory.GetFiles(FixtureDir, pattern))
            File.Copy(src, Path.Combine(tempDir, Path.GetFileName(src)));
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
