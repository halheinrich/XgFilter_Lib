using System.Text.Json;
using BgDataTypes_Lib;
using ConvertXgToJson_Lib;
using ConvertXgToJson_Lib.Models;
using XgFilter_Lib.Filtering;

namespace XgFilter_Lib;

/// <summary>
/// Iterates over .xg or .json files in a directory and yields only the
/// <see cref="DecisionRow"/> records that pass the supplied filters.
/// Applies match- and game-level early-exit optimizations via
/// <see cref="XgIteratorState"/>.
/// </summary>
public static class FilteredDecisionIterator
{
    /// <summary>
    /// Iterates all .xg files in <paramref name="xgDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>.
    /// </summary>
    public static IEnumerable<DecisionRow> IterateXgDirectory(
        string xgDir,
        DecisionFilterSet filters) =>
        IterateDirectory(xgDir, "*.xg", XgFileReader.ReadFile, filters);

    /// <summary>
    /// Iterates all .json files in <paramref name="jsonDir"/> and returns
    /// the subset of decisions that match <paramref name="filters"/>.
    /// </summary>
    public static IEnumerable<DecisionRow> IterateJsonDirectory(
        string jsonDir,
        DecisionFilterSet filters) =>
        IterateDirectory(jsonDir, "*.json", XgFileReader.ReadJson, filters);

    private static IEnumerable<DecisionRow> IterateDirectory(
        string dir,
        string searchPattern,
        Func<string, XgFile> reader,
        DecisionFilterSet filters)
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

            foreach (var row in XgDecisionIterator.Iterate(file, matchId, state))
            {
                // GameInfo is freshly populated by Iterate() at each GameHeaderRecord.
                // Check it before deciding whether to skip the game.
                if (state.GameInfo != null && filters.ShouldSkipGame(state.GameInfo))
                {
                    state.AdvanceNextGame = true;
                    state.GameInfo = null;
                    continue;
                }

                if (!filters.Matches(row)) continue;

                state.AdvanceNextGame = filters.ShouldAdvanceGame(row);
                state.AdvanceNextMatch = filters.ShouldAdvanceMatch(row);

                yield return row;
            }
        }
    }
}
