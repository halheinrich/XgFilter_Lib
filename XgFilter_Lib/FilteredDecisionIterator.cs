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
/// early-exit pipeline via <see cref="XgIteratorState"/>.
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
    /// Iterates all .xg files in <paramref name="xgDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public IEnumerable<DecisionRow> IterateXgDirectory(string xgDir) =>
        IterateDirectory(xgDir, "*.xg", XgFileReader.ReadFile,
            XgDecisionIterator.Iterate);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public IEnumerable<DecisionRow> IterateJsonDirectory(string jsonDir) =>
        IterateDirectory(jsonDir, "*.json", XgFileReader.ReadJson,
            XgDecisionIterator.Iterate);

    /// <summary>
    /// Iterates all .xg files in <paramref name="xgDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="BgDecisionData"/> — the diagram form, with
    /// the full <c>Plays</c> list and after-boards. Filter semantics are
    /// identical to <see cref="IterateXgDirectory"/>.
    /// </summary>
    public IEnumerable<BgDecisionData> IterateXgDirectoryDiagrams(string xgDir) =>
        IterateDirectory(xgDir, "*.xg", XgFileReader.ReadFile,
            XgDecisionIterator.IterateDiagramRequests);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match the configured filters,
    /// shaped as <see cref="BgDecisionData"/>. Filter semantics are
    /// identical to <see cref="IterateJsonDirectory"/>.
    /// </summary>
    public IEnumerable<BgDecisionData> IterateJsonDirectoryDiagrams(string jsonDir) =>
        IterateDirectory(jsonDir, "*.json", XgFileReader.ReadJson,
            XgDecisionIterator.IterateDiagramRequests);

    private IEnumerable<T> IterateDirectory<T>(
        string dir,
        string searchPattern,
        Func<string, XgFile> reader,
        Func<XgFile, string?, XgIteratorState?, IEnumerable<T>> source)
        where T : IDecisionFilterData
    {
        var state = new XgIteratorState();

        foreach (var path in Directory.EnumerateFiles(dir, searchPattern))
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

            state.AdvanceNextMatch = false;
            state.AdvanceNextGame = false;
            state.MatchInfo = XgDecisionIterator.ExtractMatchInfo(file);
            state.GameInfo = null;

            if (_filters.ShouldSkipMatch(state.MatchInfo))
                continue;

            string matchId = Path.GetFileNameWithoutExtension(path);

            foreach (var item in source(file, matchId, state))
            {
                // GameInfo is freshly populated by the source iterator at each
                // GameHeaderRecord. Check it before deciding whether to skip
                // the game.
                if (state.GameInfo != null && _filters.ShouldSkipGame(state.GameInfo))
                {
                    state.AdvanceNextGame = true;
                    state.GameInfo = null;
                    continue;
                }

                if (!_filters.Matches(item)) continue;

                state.AdvanceNextGame = _filters.ShouldAdvanceGame(item);
                state.AdvanceNextMatch = _filters.ShouldAdvanceMatch(item);

                yield return item;
            }
        }
    }
}
