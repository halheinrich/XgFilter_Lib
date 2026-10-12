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
/// are the sources under iteration and, optionally, a
/// <see cref="SourceReport"/>. Every source is read through the converter's
/// one read of one source, <see cref="XgSourceReader"/>, in the format the
/// entry point states — <see cref="XgSourceFormat.Xg"/> for the XG-format
/// walks, <see cref="XgSourceFormat.Json"/> for the JSON one — and its
/// failure policy is the walk's: a source whose read fails (corruption, I/O,
/// deserialization, anything else) is skipped with a warning logged via the
/// injected <see cref="ILogger"/>, and iteration continues with the next
/// source rather than aborting the whole run. A cancellation is never a
/// failed source: it propagates and ends the walk. A caller that wants the
/// failures as data rather than as log lines — which sources were rejected
/// and why, how many were attempted, whether the walk finished — passes a
/// fresh <see cref="SourceReport"/> to the entry point and reads it during
/// or after the enumeration (halheinrich/backgammon#368); the warning stays
/// as a trace of the same event, carrying the same exception.
/// </para>
///
/// <para>
/// The same logger is forwarded into the producer, so per-decision warnings
/// it raises — notably an illegal-play skip — surface through this pipeline
/// alongside the file-level skip warnings above.
/// </para>
///
/// <para>
/// <b>The ranking in force is the caller's</b> (SPEC-scoring §2a). Which play
/// is best, and so a checker play's error, depth and best after-board, is a
/// ranking's; the caller states it at construction, beside the filters, and
/// every decision is judged under it: each row is built for it
/// (<see cref="DecisionRow.From"/>, through the producer's
/// <see cref="XgIteratorOptions.Ranking"/>), and each record is filtered
/// through its view for it (<see cref="BgDecisionData.ViewFor"/>) — the record
/// itself does not depend on the ranking. The filters hold none of their own:
/// they read the ranking the view or row was built for. An application
/// without a ranking setting passes <see cref="PlayRanking.Equity"/>, the
/// default.
/// </para>
/// </summary>
public sealed class FilteredDecisionIterator
{
    private readonly DecisionFilterSet _filters;
    private readonly PlayRanking _ranking;
    private readonly XgIteratorOptions _options;
    private readonly ILogger<FilteredDecisionIterator> _logger;

    /// <summary>
    /// Creates an iterator that applies <paramref name="filters"/> under
    /// <paramref name="ranking"/> on every walk and logs file-skip events to
    /// <paramref name="logger"/>.
    /// </summary>
    /// <param name="filters">The filters every decision must pass.</param>
    /// <param name="ranking">
    /// The ranking in force: every row is built for it and every record is
    /// filtered through its view for it. An application without a ranking
    /// setting passes <see cref="PlayRanking.Equity"/>, the default.
    /// </param>
    /// <param name="logger">Where file-skip events, and the producer's per-decision warnings, are logged.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filters"/> or <paramref name="logger"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ranking"/> is not a defined ranking.
    /// </exception>
    public FilteredDecisionIterator(
        DecisionFilterSet filters,
        PlayRanking ranking,
        ILogger<FilteredDecisionIterator> logger)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (!Enum.IsDefined(ranking))
            throw new ArgumentOutOfRangeException(nameof(ranking), ranking, "Not a defined play ranking.");
        ArgumentNullException.ThrowIfNull(logger);
        _filters = filters;
        _ranking = ranking;
        _options = new XgIteratorOptions(Ranking: ranking);
        _logger = logger;
    }

    /// <summary>
    /// Iterates every XG-format file in <paramref name="xgDir"/> — both
    /// <c>*.xg</c> (match files) and <c>*.xgp</c> (position files) — and
    /// returns the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/> built for the configured ranking.
    /// </summary>
    /// <param name="xgDir">The directory to walk.</param>
    /// <param name="report">
    /// Where this walk records the sources it attempted and rejected, or null
    /// to record nothing. A fresh report per enumeration; see
    /// <see cref="SourceReport"/> for the one-walk contract.
    /// </param>
    public IEnumerable<DecisionRow> IterateXgDirectory(string xgDir, SourceReport? report = null) =>
        IterateFiles(XgFileReader.EnumerateXgFormatFiles(xgDir), XgSourceFormat.Xg, Rows, AsView, report);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/> built for the configured ranking.
    /// </summary>
    /// <param name="jsonDir">The directory to walk.</param>
    /// <param name="report">
    /// Where this walk records the sources it attempted and rejected, or null
    /// to record nothing. A fresh report per enumeration; see
    /// <see cref="SourceReport"/> for the one-walk contract.
    /// </param>
    public IEnumerable<DecisionRow> IterateJsonDirectory(string jsonDir, SourceReport? report = null) =>
        IterateFiles(Directory.EnumerateFiles(jsonDir, "*.json"), XgSourceFormat.Json, Rows, AsView, report);

    /// <summary>
    /// Iterates every XG-format file in <paramref name="xgDir"/> — both
    /// <c>*.xg</c> (match files) and <c>*.xgp</c> (position files) — and
    /// returns the subset of decisions that match the configured filters,
    /// shaped as <see cref="BgDecisionData"/> — the diagram form, with
    /// the full candidate list and after-boards. Filter semantics are
    /// identical to <see cref="IterateXgDirectory"/>: each record is filtered
    /// through its view for the configured ranking, and the record itself,
    /// which depends on no ranking, is yielded.
    /// </summary>
    /// <param name="xgDir">The directory to walk.</param>
    /// <param name="report">
    /// Where this walk records the sources it attempted and rejected, or null
    /// to record nothing. A fresh report per enumeration; see
    /// <see cref="SourceReport"/> for the one-walk contract.
    /// </param>
    public IEnumerable<BgDecisionData> IterateXgDirectoryDiagrams(string xgDir, SourceReport? report = null) =>
        IterateFiles(XgFileReader.EnumerateXgFormatFiles(xgDir), XgSourceFormat.Xg, Records, ViewOf, report);

    /// <summary>
    /// Iterates a caller-supplied list of XG-format files presented as named
    /// streams — the directory-free counterpart to
    /// <see cref="IterateXgDirectory"/> for callers that hold the bytes rather
    /// than a server path (e.g. a WASM client parsing browser-picked files).
    /// Each <see cref="XgFileStream.Data"/> stream is read through
    /// <see cref="XgSourceReader.ReadStream"/>; filter semantics, malformed-file
    /// skip+log behaviour, and the early-exit pipeline are identical to the
    /// directory overload. Yields <see cref="DecisionRow"/> built for the
    /// configured ranking.
    /// </summary>
    /// <remarks>
    /// See <see cref="XgFileStream"/> for the stream ownership, precondition
    /// and laziness contract. <see cref="XgFileStream.FileName"/> must be non-blank
    /// and carry its extension, and <see cref="XgFileStream.Data"/> must be
    /// readable, seekable and at position 0; the iterator throws
    /// <see cref="ArgumentException"/> when it reaches an entry that violates
    /// either. A stream that fails to parse (corruption, truncation, anything
    /// else) is skipped and logged — and recorded on <paramref name="report"/>
    /// when one is passed; only a misused <i>entry</i> is a usage error.
    /// </remarks>
    /// <param name="files">The named streams to walk, lazily.</param>
    /// <param name="report">
    /// Where this walk records the sources it attempted and rejected, or null
    /// to record nothing. A fresh report per enumeration; see
    /// <see cref="SourceReport"/> for the one-walk contract.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public IEnumerable<DecisionRow> IterateXgStreams(
        IEnumerable<XgFileStream> files, SourceReport? report = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        return IterateSources(ToSources(files), Rows, AsView, report);
    }

    /// <summary>
    /// Diagram-shaped counterpart to <see cref="IterateXgStreams"/>: parses the
    /// same caller-supplied named streams but yields <see cref="BgDecisionData"/>
    /// (the full candidate list and after-boards). Filter semantics are
    /// identical to <see cref="IterateXgStreams"/>: each record is filtered
    /// through its view for the configured ranking.
    /// </summary>
    /// <param name="files">The named streams to walk, lazily.</param>
    /// <param name="report">
    /// Where this walk records the sources it attempted and rejected, or null
    /// to record nothing. A fresh report per enumeration; see
    /// <see cref="SourceReport"/> for the one-walk contract.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="files"/> is null.</exception>
    public IEnumerable<BgDecisionData> IterateXgStreamDiagrams(
        IEnumerable<XgFileStream> files, SourceReport? report = null)
    {
        ArgumentNullException.ThrowIfNull(files);
        return IterateSources(ToSources(files), Records, ViewOf, report);
    }

    /// <summary>
    /// The producer's row surface for one file, under this iterator's options
    /// (the ranking every row is built for) and logger — the one place either
    /// is handed to the producer for rows.
    /// </summary>
    private IEnumerable<DecisionRow> Rows(
        XgFile file, string sourceFile, XgIteratorState? state, XgIteratorCallbacks? callbacks) =>
        XgDecisionIterator.Iterate(file, sourceFile, state, callbacks, _options, _logger);

    /// <summary>
    /// The producer's record surface for one file, under the same options and
    /// logger as <see cref="Rows"/>: its post-yield callbacks see each record
    /// through its view for the same ranking this iterator filters under.
    /// </summary>
    private IEnumerable<BgDecisionData> Records(
        XgFile file, string sourceFile, XgIteratorState? state, XgIteratorCallbacks? callbacks) =>
        XgDecisionIterator.IterateDiagramRequests(file, sourceFile, state, callbacks, _options, _logger);

    /// <summary>A row is its own view, built for the configured ranking.</summary>
    private static IDecisionFilterData AsView(DecisionRow row) => row;

    /// <summary>A record's view for the configured ranking.</summary>
    private IDecisionFilterData ViewOf(BgDecisionData record) => record.ViewFor(_ranking);

    /// <summary>
    /// Projects caller-supplied <see cref="XgFileStream"/> entries onto the
    /// <c>(sourceName, deferred-read)</c> pairs that <see cref="IterateSources{T}"/>
    /// consumes. The entry's own validation runs lazily as each entry is
    /// reached during enumeration — a usage error (null/blank/extension-less
    /// name, null stream) throws <see cref="ArgumentException"/> before the
    /// source is attempted, rather than being recorded as a rejection. The read
    /// itself is deferred into the returned thunk, so <see cref="IterateSources{T}"/>
    /// attempts it in walk order and handles its outcome exactly as for a
    /// directory file. The stream's own state — readable, seekable, at
    /// position 0 — is the converter's precondition, validated by
    /// <see cref="XgSourceReader.ReadStream"/> when the read is attempted.
    /// </summary>
    private static IEnumerable<(string sourceName, Func<XgSourceRead> read)> ToSources(
        IEnumerable<XgFileStream> files) =>
        files.Select(file =>
        {
            // The name must carry the extension — see RequireValid and the
            // IterateSources contract comment. Validated here, eagerly per entry,
            // so a bad name fails fast instead of reaching the producer.
            Stream data = RequireValid(file);
            return (file.FileName,
                (Func<XgSourceRead>)(() => XgSourceReader.ReadStream(file.FileName, data, XgSourceFormat.Xg)));
        });

    /// <summary>
    /// Enforces the <see cref="XgFileStream"/> usage contract this iterator
    /// owns for one entry — a name carrying its extension, and a stream to
    /// read — and returns its stream. Validation lives at the API boundary
    /// rather than in the record-struct constructor because
    /// <c>default(XgFileStream)</c> would bypass any primary-constructor guard,
    /// and "the name must carry an extension" is the iterator's contract, not
    /// an intrinsic property of a filename+stream pair. Whether the stream can
    /// be decoded in place is the converter's rule, and stays there.
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
                $"XgFileStream.Data for '{file.FileName}' must be a stream holding the file " +
                "(readable, seekable, at position 0), not null.",
                nameof(file));
    }

    /// <summary>
    /// Maps a directory walk onto the shared <see cref="IterateSources{T}"/>
    /// core: each path becomes a <c>(sourceName, deferred-read)</c> pair, where
    /// <c>sourceName = Path.GetFileName(path)</c> (extension preserved — see the
    /// contract comment in <see cref="IterateSources{T}"/>) and the read, in the
    /// <paramref name="format"/> the walk states, is deferred so the core
    /// attempts it in walk order.
    /// </summary>
    private IEnumerable<T> IterateFiles<T>(
        IEnumerable<string> paths,
        XgSourceFormat format,
        Func<XgFile, string, XgIteratorState?, XgIteratorCallbacks?, IEnumerable<T>> source,
        Func<T, IDecisionFilterData> view,
        SourceReport? report) =>
        IterateSources(
            paths.Select(path => (Path.GetFileName(path),
                (Func<XgSourceRead>)(() => XgSourceReader.ReadFile(path, format)))),
            source,
            view,
            report);

    /// <summary>
    /// The single-sourced read / filter / early-exit pipeline, shared by every
    /// directory and stream entry point. Iterates
    /// <c>(sourceName, deferred-read)</c> pairs: each read is the converter's
    /// (<see cref="XgSourceReader"/>), whose outcome decides what follows — a
    /// decoded source is iterated, its validated <paramref name="sources"/>
    /// name handed to the producer as the decision's <c>SourceFile</c>, and
    /// every produced item gated by <see cref="DecisionFilterSet.Matches"/> on
    /// its <paramref name="view"/> for the configured ranking (a row as itself,
    /// a record through <see cref="BgDecisionData.ViewFor"/>); a failed source
    /// is recorded and logged, and the walk goes on with the next. The walk
    /// catches nothing: the read's failure policy is the converter's, so a
    /// cancellation, and a usage error the read refuses, propagate.
    ///
    /// <para>
    /// Contract: <c>sourceName</c> is passed straight through to the producer
    /// and must carry its extension — the producer's <c>DecisionId</c> stamping
    /// derives the .xg/.xgp/.json discrimination from
    /// <c>Path.GetExtension(sourceFile)</c>, and an extension-less name throws
    /// at stamp time. Both callers honour this: the directory mapper keeps the
    /// extension via <c>Path.GetFileName</c>, and the stream mapper validates it
    /// up front in <see cref="RequireValid"/>.
    /// </para>
    ///
    /// <para>
    /// The report, when one is passed, is written at the points the pipeline
    /// has: claimed when this walk starts (the enumeration's first
    /// <c>MoveNext</c>, so a report reused across walks fails on the second
    /// walk's first step); one attempt per source as its read starts; one
    /// readable source per decoded outcome, before its decisions are
    /// filtered; one rejection per failed outcome, before the skip is logged
    /// and carrying the outcome's own exception, so the log is a trace of the
    /// record and never a second source of it; and completion after the last
    /// source. Nothing else is recorded: a usage error thrown by the
    /// <paramref name="sources"/> projection or by the read, a cancellation,
    /// a failure in the producer, a filter or the consumer all leave the
    /// walk, and the report, incomplete — an attempt ended that way is
    /// neither readable nor rejected.
    /// </para>
    /// </summary>
    private IEnumerable<T> IterateSources<T>(
        IEnumerable<(string sourceName, Func<XgSourceRead> read)> sources,
        Func<XgFile, string, XgIteratorState?, XgIteratorCallbacks?, IEnumerable<T>> source,
        Func<T, IDecisionFilterData> view,
        SourceReport? report)
    {
        report?.Claim();

        var callbacks = new XgIteratorCallbacks(
            SkipMatchAt:    _filters.ShouldSkipMatch,
            SkipGameAt:     _filters.ShouldSkipGame,
            StopGameAfter:  _filters.ShouldAdvanceGame,
            StopMatchAfter: _filters.ShouldAdvanceMatch);

        foreach (var (sourceName, read) in sources)
        {
            report?.Attempted();
            var decisions = read().Match(
                decoded =>
                {
                    // Readable from the moment it decodes, before any of its
                    // decisions is filtered, so a source none of whose
                    // decisions match still counts.
                    report?.Decoded();
                    return source(decoded.File, sourceName, null, callbacks);
                },
                failed =>
                {
                    // The record before the trace: a logger that throws still
                    // propagates and still leaves the walk incomplete, but the
                    // source that failed to read is already on the report.
                    report?.Reject(sourceName, failed);
                    _logger.LogWarning(failed.Exception, "Skipping {File}", sourceName);
                    return [];
                });

            foreach (var item in decisions)
            {
                if (!_filters.Matches(view(item))) continue;
                yield return item;
            }
        }

        report?.Complete();
    }
}
