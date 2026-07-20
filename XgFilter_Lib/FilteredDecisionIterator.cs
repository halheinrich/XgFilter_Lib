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
///
/// <para>
/// The same logger is forwarded into the producer, so per-decision warnings
/// it raises — notably an illegal-play skip — surface through this pipeline
/// alongside the file-level skip warnings above.
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
        IterateFiles(XgFileReader.EnumerateXgFormatFiles(xgDir), XgFileReader.ReadFile,
            (file, sourceFile, state, callbacks) =>
                XgDecisionIterator.Iterate(file, sourceFile, state, callbacks, logger: _logger));

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public IEnumerable<DecisionRow> IterateJsonDirectory(string jsonDir) =>
        IterateFiles(Directory.EnumerateFiles(jsonDir, "*.json"),
            XgFileReader.ReadJson,
            (file, sourceFile, state, callbacks) =>
                XgDecisionIterator.Iterate(file, sourceFile, state, callbacks, logger: _logger));

    /// <summary>
    /// Iterates every XG-format file in <paramref name="xgDir"/> — both
    /// <c>*.xg</c> (match files) and <c>*.xgp</c> (position files) — and
    /// returns the subset of decisions that match the configured filters,
    /// shaped as <see cref="BgDecisionData"/> — the diagram form, with
    /// the full <c>Plays</c> list and after-boards. Filter semantics are
    /// identical to <see cref="IterateXgDirectory"/>.
    /// </summary>
    public IEnumerable<BgDecisionData> IterateXgDirectoryDiagrams(string xgDir) =>
        IterateFiles(XgFileReader.EnumerateXgFormatFiles(xgDir), XgFileReader.ReadFile,
            (file, sourceFile, state, callbacks) =>
                XgDecisionIterator.IterateDiagramRequests(file, sourceFile, state, callbacks, logger: _logger));

    /// <summary>
    /// Iterates a caller-supplied list of XG-format files presented as named
    /// streams — the directory-free counterpart to
    /// <see cref="IterateXgDirectory"/> for callers that hold the bytes rather
    /// than a server path (e.g. a WASM client parsing browser-picked files).
    /// Each <see cref="XgFileStream.Data"/> stream is parsed via
    /// <see cref="XgFileReader.ReadStream"/>; filter semantics, malformed-file
    /// skip+log behaviour, and the early-exit pipeline are identical to the
    /// directory overload. Yields <see cref="DecisionRow"/>.
    /// </summary>
    /// <remarks>
    /// See <see cref="XgFileStream"/> for the stream ownership, single-forward-read,
    /// and laziness contract. <see cref="XgFileStream.FileName"/> must be non-blank
    /// and carry its extension; the iterator throws <see cref="ArgumentException"/>
    /// when it reaches an entry that violates this. A stream that fails to parse
    /// (corruption, truncation, anything else) is skipped and logged — only a
    /// malformed <i>name</i> is treated as a usage error.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public IEnumerable<DecisionRow> IterateXgStreams(IEnumerable<XgFileStream> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return IterateSources(ToSources(files),
            (file, sourceFile, state, callbacks) =>
                XgDecisionIterator.Iterate(file, sourceFile, state, callbacks, logger: _logger));
    }

    /// <summary>
    /// Diagram-shaped counterpart to <see cref="IterateXgStreams"/>: parses the
    /// same caller-supplied named streams but yields <see cref="BgDecisionData"/>
    /// (the full <c>Plays</c> list and after-boards). Filter semantics are
    /// identical to <see cref="IterateXgStreams"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public IEnumerable<BgDecisionData> IterateXgStreamDiagrams(IEnumerable<XgFileStream> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        return IterateSources(ToSources(files),
            (file, sourceFile, state, callbacks) =>
                XgDecisionIterator.IterateDiagramRequests(file, sourceFile, state, callbacks, logger: _logger));
    }

    /// <summary>
    /// Projects caller-supplied <see cref="XgFileStream"/> entries onto the
    /// <c>(sourceFile, deferred-read)</c> pairs that <see cref="IterateSources{T}"/>
    /// consumes. Validation runs lazily as each entry is reached during
    /// enumeration — a usage error (null/blank/extension-less name, null stream)
    /// throws <see cref="ArgumentException"/> rather than being swallowed as a
    /// skipped file. The read itself is deferred into the returned thunk so a
    /// parse failure surfaces inside <see cref="IterateSources{T}"/>'s try/catch
    /// and is skipped+logged, exactly as for directory files.
    /// </summary>
    private static IEnumerable<(string sourceFile, Func<XgFile> read)> ToSources(
        IEnumerable<XgFileStream> files) =>
        files.Select(file =>
        {
            // sourceFile must carry the extension — see RequireValid and the
            // IterateSources contract comment. Validated here, eagerly per entry,
            // so a bad name fails fast instead of reaching the producer.
            Stream data = RequireValid(file);
            return (file.FileName, (Func<XgFile>)(() => XgFileReader.ReadStream(data)));
        });

    /// <summary>
    /// Enforces the <see cref="XgFileStream"/> usage contract for one entry and
    /// returns its stream. Validation lives at the API boundary rather than in
    /// the record-struct constructor because <c>default(XgFileStream)</c> would
    /// bypass any primary-constructor guard, and "the name must carry an
    /// extension" is the iterator's contract, not an intrinsic property of a
    /// filename+stream pair.
    /// </summary>
    private static Stream RequireValid(XgFileStream file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            throw new ArgumentException(
                "XgFileStream.FileName must be a non-blank file name carrying its " +
                "extension (e.g. \"match.xg\"); the source name discriminates " +
                ".xg/.xgp/.json downstream.", nameof(file));

        if (string.IsNullOrEmpty(Path.GetExtension(file.FileName)))
            throw new ArgumentException(
                $"XgFileStream.FileName '{file.FileName}' must include an extension " +
                "(.xg, .xgp, or .json); the source name discriminates the format " +
                "downstream and an extension-less name fails at DecisionId stamping.",
                nameof(file));

        return file.Data
            ?? throw new ArgumentException(
                $"XgFileStream.Data for '{file.FileName}' must be a non-null, open, " +
                "forward-readable stream positioned at the start of the file.",
                nameof(file));
    }

    /// <summary>
    /// Maps a directory walk onto the shared <see cref="IterateSources{T}"/>
    /// core: each path becomes a <c>(sourceFile, deferred-read)</c> pair, where
    /// <c>sourceFile = Path.GetFileName(path)</c> (extension preserved — see the
    /// contract comment in <see cref="IterateSources{T}"/>) and the read is
    /// deferred so a failed read is skipped+logged rather than aborting the walk.
    /// </summary>
    private IEnumerable<T> IterateFiles<T>(
        IEnumerable<string> paths,
        Func<string, XgFile> reader,
        Func<XgFile, string, XgIteratorState?, XgIteratorCallbacks?, IEnumerable<T>> source)
        where T : IDecisionFilterData =>
        IterateSources(
            paths.Select(path => (Path.GetFileName(path), (Func<XgFile>)(() => reader(path)))),
            source);

    /// <summary>
    /// The single-sourced filter / early-exit / skip-and-continue pipeline,
    /// shared by every directory and stream entry point. Iterates
    /// <c>(sourceFile, deferred-read)</c> pairs: the read is invoked inside the
    /// try/catch so a malformed file is logged and skipped (iteration continues
    /// with the next entry), the validated <paramref name="sources"/> name is
    /// handed to the producer as the decision's <c>SourceFile</c>, and every
    /// produced item is gated by <see cref="DecisionFilterSet.Matches"/>.
    ///
    /// <para>
    /// Contract: <c>sourceFile</c> is passed straight through to the producer
    /// and must carry its extension — the producer's <c>DecisionId</c> stamping
    /// derives the .xg/.xgp/.json discrimination from
    /// <c>Path.GetExtension(sourceFile)</c>, and an extension-less name throws
    /// at stamp time. Both callers honour this: the directory mapper keeps the
    /// extension via <c>Path.GetFileName</c>, and the stream mapper validates it
    /// up front in <see cref="RequireValid"/>.
    /// </para>
    /// </summary>
    private IEnumerable<T> IterateSources<T>(
        IEnumerable<(string sourceFile, Func<XgFile> read)> sources,
        Func<XgFile, string, XgIteratorState?, XgIteratorCallbacks?, IEnumerable<T>> source)
        where T : IDecisionFilterData
    {
        var callbacks = new XgIteratorCallbacks(
            SkipMatchAt:    _filters.ShouldSkipMatch,
            SkipGameAt:     _filters.ShouldSkipGame,
            StopGameAfter:  _filters.ShouldAdvanceGame,
            StopMatchAfter: _filters.ShouldAdvanceMatch);

        foreach (var (sourceFile, read) in sources)
        {
            XgFile file;
            try
            {
                file = read();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Skipping {File}", sourceFile);
                continue;
            }

            foreach (var item in source(file, sourceFile, null, callbacks))
            {
                if (!_filters.Matches(item)) continue;
                yield return item;
            }
        }
    }
}
