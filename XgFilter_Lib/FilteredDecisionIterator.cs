using System.Text.Json;
using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib;

/// <summary>
/// Iterates over .xg or .json files in a directory and yields the
/// decision records that pass the supplied filters. Two output shapes
/// are supported: <see cref="DecisionRow"/> (CSV-flat) and
/// <see cref="BgDecisionData"/> (diagram-shaped, with after-boards and
/// per-candidate plays). Both share the same filter-evaluation and
/// early-exit pipeline via <see cref="XgIteratorState"/>.
/// </summary>
public static class FilteredDecisionIterator
{
    /// <summary>
    /// Iterates all .xg files in <paramref name="xgDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public static IEnumerable<DecisionRow> IterateXgDirectory(
        string xgDir,
        DecisionFilterSet filters) =>
        IterateDirectory(xgDir, "*.xg", XgFileReader.ReadFile,
            XgDecisionIterator.Iterate, filters);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>,
    /// shaped as <see cref="DecisionRow"/>.
    /// </summary>
    public static IEnumerable<DecisionRow> IterateJsonDirectory(
        string jsonDir,
        DecisionFilterSet filters) =>
        IterateDirectory(jsonDir, "*.json", XgFileReader.ReadJson,
            XgDecisionIterator.Iterate, filters);

    /// <summary>
    /// Iterates all .xg files in <paramref name="xgDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>,
    /// shaped as <see cref="BgDecisionData"/> — the diagram form, with
    /// the full <c>Plays</c> list and after-boards. Filter semantics are
    /// identical to <see cref="IterateXgDirectory"/>.
    /// </summary>
    public static IEnumerable<BgDecisionData> IterateXgDirectoryDiagrams(
        string xgDir,
        DecisionFilterSet filters) =>
        IterateDirectory(xgDir, "*.xg", XgFileReader.ReadFile,
            XgDecisionIterator.IterateDiagramRequests, filters);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>,
    /// shaped as <see cref="BgDecisionData"/>. Filter semantics are
    /// identical to <see cref="IterateJsonDirectory"/>.
    /// </summary>
    public static IEnumerable<BgDecisionData> IterateJsonDirectoryDiagrams(
        string jsonDir,
        DecisionFilterSet filters) =>
        IterateDirectory(jsonDir, "*.json", XgFileReader.ReadJson,
            XgDecisionIterator.IterateDiagramRequests, filters);

    private static IEnumerable<T> IterateDirectory<T>(
        string dir,
        string searchPattern,
        Func<string, XgFile> reader,
        Func<XgFile, string?, XgIteratorState?, IEnumerable<T>> source,
        DecisionFilterSet filters)
        where T : IDecisionFilterData
    {
        var state = new XgIteratorState();

        foreach (var path in Directory.EnumerateFiles(dir, searchPattern))
        {
            XgFile file;
            try { file = reader(path); }
            catch (Exception ex) when (
                ex is IOException ||
                ex is UnauthorizedAccessException ||
                ex is JsonException)
            {
                continue;
            }

            state.AdvanceNextMatch = false;
            state.AdvanceNextGame = false;
            state.MatchInfo = XgDecisionIterator.ExtractMatchInfo(file);
            state.GameInfo = null;

            if (filters.ShouldSkipMatch(state.MatchInfo))
                continue;

            string matchId = Path.GetFileNameWithoutExtension(path);

            foreach (var item in source(file, matchId, state))
            {
                // GameInfo is freshly populated by the source iterator at each
                // GameHeaderRecord. Check it before deciding whether to skip
                // the game.
                if (state.GameInfo != null && filters.ShouldSkipGame(state.GameInfo))
                {
                    state.AdvanceNextGame = true;
                    state.GameInfo = null;
                    continue;
                }

                if (!filters.Matches(item)) continue;

                state.AdvanceNextGame = filters.ShouldAdvanceGame(item);
                state.AdvanceNextMatch = filters.ShouldAdvanceMatch(item);

                yield return item;
            }
        }
    }
}
