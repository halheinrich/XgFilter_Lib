using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using Microsoft.Extensions.Logging;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib;

/// <summary>
/// Yields the decisions that pass the configured <see cref="DecisionFilterSet"/>
/// under the configured ranking: from one already-parsed source
/// (<see cref="IterateFile"/>, <see cref="IterateFileDiagrams"/> — the
/// filtering operation), or from a walk over many sources, a directory of
/// .xg/.xgp or .json files or a list of named streams, which reads each
/// source and filters each decoded one through that same operation.
/// Two output shapes are supported: <see cref="DecisionRow"/> (CSV-flat) and
/// <see cref="BgDecisionData"/> (diagram-shaped, with after-boards and
/// per-candidate plays). Both share the same filter-evaluation and
/// early-exit pipeline: the filter set's four skip / advance predicates
/// are wired into a single <see cref="XgIteratorCallbacks"/> instance and
/// handed to the producer, which short-circuits its own iteration at the
/// match, game, and per-row boundaries.
///
/// <para>
/// Filters, the ranking, a logger and, optionally, the producer's opening
/// book are configured at construction; per-call parameters are the source
/// or sources under iteration and, for a walk, optionally a
/// <see cref="SourceReport"/>. Every source a walk takes is read through the converter's
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
/// default. The ranking is stated here once: the opening book a caller
/// supplies is the book alone, never producer options that could carry a
/// second ranking.
/// </para>
/// </summary>
public sealed class FilteredDecisionIterator
{
    /// <summary>
    /// The file extension of the library's JSON document — this library's one
    /// statement of it, read by the JSON directory walk's file pattern and by
    /// the stream entries' format mapping (<see cref="FormatOf"/>). The
    /// XG-format extensions are the converter's
    /// (<see cref="XgFileReader.XgFormatExtensions"/>).
    /// </summary>
    private const string JsonExtension = ".json";

    private readonly DecisionFilterSet _filters;

    /// <summary>
    /// The producer's options for every call: the ranking in force, the one
    /// statement of it here, and the caller's opening book, if any.
    /// </summary>
    private readonly XgIteratorOptions _options;

    /// <summary>
    /// The filter set's four early-exit predicates, wired once to the
    /// producer's boundaries — the only place they are wired, so no caller
    /// writes them.
    /// </summary>
    private readonly XgIteratorCallbacks _callbacks;

    private readonly ILogger<FilteredDecisionIterator> _logger;

    /// <summary>
    /// Creates an iterator that applies <paramref name="filters"/> under
    /// <paramref name="ranking"/> on every walk and filtering call, enriches
    /// book-analysed decisions from <paramref name="openingBook"/> when one is
    /// supplied, and logs file-skip events to <paramref name="logger"/>.
    /// </summary>
    /// <param name="filters">The filters every decision must pass.</param>
    /// <param name="ranking">
    /// The ranking in force: every row is built for it and every record is
    /// filtered through its view for it. An application without a ranking
    /// setting passes <see cref="PlayRanking.Equity"/>, the default.
    /// </param>
    /// <param name="logger">Where file-skip events, and the producer's per-decision warnings, are logged.</param>
    /// <param name="openingBook">
    /// The producer's loaded opening book (<see cref="XgIteratorOptions.OpeningBook"/>),
    /// which enriches each book-analysed candidate with the rollout behind it,
    /// on every walk and filtering call; or null, the default, for none — a
    /// book hit then states the bare book facts. Locating and loading the
    /// book is the application's concern.
    /// </param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="filters"/> or <paramref name="logger"/> is null.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="ranking"/> is not a defined ranking.
    /// </exception>
    public FilteredDecisionIterator(
        DecisionFilterSet filters,
        PlayRanking ranking,
        ILogger<FilteredDecisionIterator> logger,
        OpeningBook? openingBook = null)
    {
        ArgumentNullException.ThrowIfNull(filters);
        if (!Enum.IsDefined(ranking))
            throw new ArgumentOutOfRangeException(nameof(ranking), ranking, "Not a defined play ranking.");
        ArgumentNullException.ThrowIfNull(logger);
        _filters = filters;
        _options = new XgIteratorOptions(openingBook, ranking);
        _callbacks = new XgIteratorCallbacks(
            SkipMatchAt:    filters.ShouldSkipMatch,
            SkipGameAt:     filters.ShouldSkipGame,
            StopGameAfter:  filters.ShouldAdvanceGame,
            StopMatchAfter: filters.ShouldAdvanceMatch);
        _logger = logger;
    }

    /// <summary>
    /// The filtering operation over one already-parsed source, as
    /// <see cref="DecisionRow"/>s built for the configured ranking: the
    /// decisions of <paramref name="file"/> that pass the configured filters,
    /// in the producer's order. This operation owns what filtering a source
    /// takes — the producer call under the configured ranking and opening
    /// book, the filter set's early-exit hints wired to the producer's
    /// boundaries, and the evaluation of <see cref="DecisionFilterSet.Matches"/>
    /// on each row — so a caller holding a decoded file (from
    /// <see cref="XgSourceReader"/>, say) writes none of it. Every walk filters
    /// each source it decodes through this operation.
    /// </summary>
    /// <remarks>
    /// Lazy, like the walks: arguments are checked at the call, and the file
    /// is iterated as the result is enumerated. A failure while iterating —
    /// the producer refusing the file's contents, a filter throwing — is not
    /// caught: it propagates to the enumerating caller.
    /// </remarks>
    /// <param name="file">The parsed source.</param>
    /// <param name="sourceName">
    /// The source's file name with its extension (e.g. <c>"match.xg"</c>):
    /// handed to the producer as every decision's <c>SourceFile</c>, it is the
    /// decisions' identity, and its extension decides it — the producer stamps
    /// <c>.xg</c> and <c>.json</c> sources one way and <c>.xgp</c> positions
    /// another (one decision each), and refuses any other extension when it
    /// stamps the first decision.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="sourceName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty or white space.</exception>
    public IEnumerable<DecisionRow> IterateFile(XgFile file, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        return Passing(
            XgDecisionIterator.Iterate(file, sourceName, state: null, _callbacks, _options, _logger),
            static row => row);
    }

    /// <summary>
    /// The filtering operation over one already-parsed source, as
    /// <see cref="BgDecisionData"/> records — the diagram form, with the full
    /// candidate list and after-boards. Identical to <see cref="IterateFile"/>
    /// in everything but the shape: each record is filtered through its view
    /// for the configured ranking (<see cref="BgDecisionData.ViewFor"/>), and
    /// the record itself, which depends on no ranking, is yielded.
    /// </summary>
    /// <remarks>Lazy, and uncaught while iterating, as <see cref="IterateFile"/>.</remarks>
    /// <param name="file">The parsed source.</param>
    /// <param name="sourceName">The source's file name with its extension; see <see cref="IterateFile"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="file"/> or <paramref name="sourceName"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="sourceName"/> is empty or white space.</exception>
    public IEnumerable<BgDecisionData> IterateFileDiagrams(XgFile file, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        return Passing(
            XgDecisionIterator.IterateDiagramRequests(file, sourceName, state: null, _callbacks, _options, _logger),
            record => record.ViewFor(_options.Ranking));
    }

    /// <summary>
    /// The filter evaluation, stated once for both shapes: the items the
    /// producer yields whose <paramref name="view"/> for the configured
    /// ranking passes every filter.
    /// </summary>
    private IEnumerable<T> Passing<T>(IEnumerable<T> produced, Func<T, IDecisionFilterData> view) =>
        produced.Where(item => _filters.Matches(view(item)));

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
        IterateFiles(XgFileReader.EnumerateXgFormatFiles(xgDir), XgSourceFormat.Xg, IterateFile, report);

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
        IterateFiles(Directory.EnumerateFiles(jsonDir, "*" + JsonExtension), XgSourceFormat.Json, IterateFile, report);

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
        IterateFiles(XgFileReader.EnumerateXgFormatFiles(xgDir), XgSourceFormat.Xg, IterateFileDiagrams, report);

    /// <summary>
    /// Iterates a caller-supplied list of sources presented as named streams —
    /// the directory-free counterpart to <see cref="IterateXgDirectory"/> and
    /// <see cref="IterateJsonDirectory"/> for callers that hold the bytes
    /// rather than a server path (e.g. a WASM client parsing browser-picked
    /// files). Each <see cref="XgFileStream.Data"/> stream is read through
    /// <see cref="XgSourceReader.ReadStream"/> in the format its name's
    /// extension states — XG's binary file for <c>.xg</c> and <c>.xgp</c>,
    /// the library's JSON document for <c>.json</c>, case-insensitively — so
    /// one walk may mix them; filter semantics, malformed-file skip+log
    /// behaviour, and the early-exit pipeline are identical to the directory
    /// overloads. Yields <see cref="DecisionRow"/> built for the configured
    /// ranking.
    /// </summary>
    /// <remarks>
    /// See <see cref="XgFileStream"/> for the stream ownership, precondition
    /// and laziness contract. <see cref="XgFileStream.FileName"/> must be non-blank
    /// and carry one of those extensions, and <see cref="XgFileStream.Data"/> must be
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
        return IterateSources(ToSources(files), IterateFile, report);
    }

    /// <summary>
    /// Diagram-shaped counterpart to <see cref="IterateXgStreams"/>: reads the
    /// same caller-supplied named streams, through the same mapping of each
    /// name's extension to the format it is read in, but yields
    /// <see cref="BgDecisionData"/> (the full candidate list and
    /// after-boards). Filter semantics are identical to
    /// <see cref="IterateXgStreams"/>: each record is filtered through its
    /// view for the configured ranking.
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
        return IterateSources(ToSources(files), IterateFileDiagrams, report);
    }

    /// <summary>
    /// Projects caller-supplied <see cref="XgFileStream"/> entries onto the
    /// <c>(sourceName, deferred-read)</c> pairs that <see cref="IterateSources{T}"/>
    /// consumes. The entry's own validation runs lazily as each entry is
    /// reached during enumeration — a usage error (a null or blank name, an
    /// extension that names no format the iterator reads, a null stream)
    /// throws <see cref="ArgumentException"/> before the source is attempted,
    /// rather than being recorded as a rejection. The read itself is deferred
    /// into the returned thunk, in the format the name's extension states
    /// (<see cref="FormatOf"/>), so <see cref="IterateSources{T}"/> attempts
    /// it in walk order and handles its outcome exactly as for a directory
    /// file. The stream's own state — readable, seekable, at position 0 — is
    /// the converter's precondition, validated by
    /// <see cref="XgSourceReader.ReadStream"/> when the read is attempted.
    /// Both stream output shapes project through here, so they share the one
    /// mapping, and the entry's name, unchanged, is the decision's identity.
    /// </summary>
    private static IEnumerable<(string sourceName, Func<XgSourceRead> read)> ToSources(
        IEnumerable<XgFileStream> files) =>
        files.Select(file =>
        {
            // The name must carry an extension stating the format — see
            // RequireValid and the IterateSources contract comment. Validated
            // here, eagerly per entry, so a bad name fails fast instead of
            // reaching the read or the producer.
            var (data, format) = RequireValid(file);
            return (file.FileName,
                (Func<XgSourceRead>)(() => XgSourceReader.ReadStream(file.FileName, data, format)));
        });

    /// <summary>
    /// Enforces the <see cref="XgFileStream"/> usage contract this iterator
    /// owns for one entry — a name whose extension states a format the
    /// iterator reads, and a stream to read — and returns the stream with that
    /// format. Validation lives at the API boundary rather than in the
    /// record-struct constructor because <c>default(XgFileStream)</c> would
    /// bypass any primary-constructor guard, and "the name states the format"
    /// is the iterator's contract, not an intrinsic property of a
    /// filename+stream pair. Whether the stream can be decoded in place is the
    /// converter's rule, and stays there.
    /// </summary>
    private static (Stream Data, XgSourceFormat Format) RequireValid(XgFileStream file)
    {
        if (string.IsNullOrWhiteSpace(file.FileName))
            throw new ArgumentException(
                "XgFileStream.FileName must be a non-blank file name carrying its " +
                "extension (e.g. \"match.xg\"); the source name discriminates " +
                ".xg/.xgp/.json downstream.", nameof(file));

        var format = FormatOf(file.FileName)
            ?? throw new ArgumentException(
                $"XgFileStream.FileName '{file.FileName}' must carry the extension of a format " +
                $"the iterator reads ({string.Join(", ", ReadableExtensions)}); the extension " +
                "states how the stream is decoded and which decision identity it stamps.",
                nameof(file));

        var data = file.Data
            ?? throw new ArgumentException(
                $"XgFileStream.Data for '{file.FileName}' must be a stream holding the file " +
                "(readable, seekable, at position 0), not null.",
                nameof(file));

        return (data, format);
    }

    /// <summary>
    /// The one mapping from a stream entry's name to the format it is read in:
    /// <see cref="XgSourceFormat.Xg"/> for a name the converter recognizes as
    /// XG-format (<see cref="XgFileReader.IsXgFormatFile"/> — <c>.xg</c> and
    /// <c>.xgp</c>), <see cref="XgSourceFormat.Json"/> for
    /// <see cref="JsonExtension"/>, both case-insensitively; null for any
    /// other extension, or none. The converter infers nothing from a name;
    /// this boundary states the format, from the name, once.
    /// </summary>
    private static XgSourceFormat? FormatOf(string fileName) =>
        XgFileReader.IsXgFormatFile(fileName) ? XgSourceFormat.Xg
        : string.Equals(Path.GetExtension(fileName), JsonExtension, StringComparison.OrdinalIgnoreCase) ? XgSourceFormat.Json
        : null;

    /// <summary>The extensions <see cref="FormatOf"/> maps, as a usage error names them.</summary>
    private static IEnumerable<string> ReadableExtensions => XgFileReader.XgFormatExtensions.Append(JsonExtension);

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
        Func<XgFile, string, IEnumerable<T>> filter,
        SourceReport? report) =>
        IterateSources(
            paths.Select(path => (Path.GetFileName(path),
                (Func<XgSourceRead>)(() => XgSourceReader.ReadFile(path, format)))),
            filter,
            report);

    /// <summary>
    /// The single-sourced walk, shared by every directory and stream entry
    /// point: read each source, then filter each decoded one. Iterates
    /// <c>(sourceName, deferred-read)</c> pairs: each read is the converter's
    /// (<see cref="XgSourceReader"/>), whose outcome decides what follows — a
    /// decoded source goes through the filtering operation,
    /// <paramref name="filter"/> (<see cref="IterateFile"/> or
    /// <see cref="IterateFileDiagrams"/>, so a walk filters exactly as a
    /// caller holding the parsed file does), under its validated
    /// <paramref name="sources"/> name; a failed source is recorded and
    /// logged, and the walk goes on with the next. The walk catches nothing:
    /// the read's failure policy is the converter's, so a cancellation, and a
    /// usage error the read refuses, propagate.
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
        Func<XgFile, string, IEnumerable<T>> filter,
        SourceReport? report)
    {
        report?.Claim();

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
                    return filter(decoded.File, sourceName);
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
                yield return item;
        }

        report?.Complete();
    }
}
