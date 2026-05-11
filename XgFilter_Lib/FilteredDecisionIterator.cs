using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib;

/// <summary>
/// Iterates over .xg or .json files in a directory and yields the
/// decision records that pass the configured <see cref="DecisionFilterSet"/>.
/// Two output shapes are supported: <see cref="DecisionRow"/> (CSV-flat) and
/// <see cref="BgDecisionData"/> (diagram-shaped, with after-boards and
/// per-candidate plays). Both share the same filter-evaluation and
/// early-exit pipeline: the filter set's four skip / advance predicates
/// are wired into a single <see cref="XgIteratorCallbacks"/> instance and
/// handed to the producer, which short-circuits its own iteration at the
/// match, game, and per-row boundaries.
///
/// <para>
/// Filters and a logger are configured at construction; per-call parameters
/// are limited to the directory under iteration. Files that fail to read
/// (corruption, I/O, deserialization, anything else) are skipped with a
/// warning logged via the injected <see cref="ILogger"/>; iteration
/// continues with the next file rather than aborting the whole run.
/// </para>
/// </summary>
public sealed class FilteredDecisionIterator
{
    private readonly DecisionFilterSet _filters;
    private readonly ILogger<FilteredDecisionIterator> _logger;

    /// <summary>
    /// Creates an iterator that applies <paramref name="filters"/> on every
    /// directory walk and logs file-skip events to <paramref name="logger"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filters"/> or <paramref name="logger"/> is null.
    /// </exception>
    public FilteredDecisionIterator(
        DecisionFilterSet filters,
        ILogger<FilteredDecisionIterator> logger)
    {
        ArgumentNullException.ThrowIfNull(filters);
        ArgumentNullException.ThrowIfNull(logger);
        _filters = filters;
        _logger = logger;
    }

    /// <summary>
    /// Iterates every XG-format file in <paramref name="xgDir"/> — both
    /// <c>*.xg</c> (match files) and <c>*.xgp</c> (position files) — and
    /// returns the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public IEnumerable<DecisionRow> IterateXgDirectory(string xgDir) =>
        IterateFiles(EnumerateXgFormatFiles(xgDir), XgFileReader.ReadFile,
            XgDecisionIterator.Iterate);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public IEnumerable<DecisionRow> IterateJsonDirectory(string jsonDir) =>
        IterateFiles(Directory.EnumerateFiles(jsonDir, "*.json"),
            XgFileReader.ReadJson, XgDecisionIterator.Iterate);

    /// <summary>
    /// Iterates every XG-format file in <paramref name="xgDir"/> — both
    /// <c>*.xg</c> (match files) and <c>*.xgp</c> (position files) — and
    /// returns the subset of decisions that match the configured filters,
    /// shaped as <see cref="BgDecisionData"/> — the diagram form, with
    /// the full <c>Plays</c> list and after-boards. Filter semantics are
    /// identical to <see cref="IterateXgDirectory"/>.
    /// </summary>
    public IEnumerable<BgDecisionData> IterateXgDirectoryDiagrams(string xgDir) =>
        IterateFiles(EnumerateXgFormatFiles(xgDir), XgFileReader.ReadFile,
            XgDecisionIterator.IterateDiagramRequests);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="BgDecisionData"/>. Filter semantics are
    /// identical to <see cref="IterateJsonDirectory"/>.
    /// </summary>
    public IEnumerable<BgDecisionData> IterateJsonDirectoryDiagrams(string jsonDir) =>
        IterateFiles(Directory.EnumerateFiles(jsonDir, "*.json"),
            XgFileReader.ReadJson, XgDecisionIterator.IterateDiagramRequests);

    /// <summary>
    /// Enumerates every XG-format file in a directory: both <c>*.xg</c>
    /// (match files) and <c>*.xgp</c> (position files). Mirrors the
    /// equivalent private helper in
    /// <c>ConvertXgToJson_Lib.XgDecisionIterator.EnumerateXgFormatFiles</c>;
    /// the duplication is provisional — the long-term fix is to expose
    /// the parser-side helper as a shared API and consume it here.
    /// </summary>
    private static IEnumerable<string> EnumerateXgFormatFiles(string dir) =>
        Directory.EnumerateFiles(dir, "*.xg")
            .Concat(Directory.EnumerateFiles(dir, "*.xgp"));

    private IEnumerable<T> IterateFiles<T>(
        IEnumerable<string> paths,
        Func<string, XgFile> reader,
        Func<XgFile, string?, XgIteratorState?, XgIteratorCallbacks?, IEnumerable<T>> source)
        where T : IDecisionFilterData
    {
        var callbacks = new XgIteratorCallbacks(
            SkipMatchAt:    _filters.ShouldSkipMatch,
            SkipGameAt:     _filters.ShouldSkipGame,
            StopGameAfter:  _filters.ShouldAdvanceGame,
            StopMatchAfter: _filters.ShouldAdvanceMatch);

        foreach (var path in paths)
        {
            XgFile file;
            try
            {
                file = reader(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping {File}", path);
                continue;
            }

            string sourceFile = Path.GetFileNameWithoutExtension(path);
            foreach (var item in source(file, sourceFile, null, callbacks))
            {
                if (!_filters.Matches(item)) continue;
                yield return item;
            }
        }
    }
}
