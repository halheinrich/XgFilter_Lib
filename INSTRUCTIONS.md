# XgFilter_Lib — Project Instructions

Part of the Backgammon tools ecosystem: https://github.com/halheinrich/backgammon
**After committing here, return to the Backgammon Umbrella project to update hashes and instructions doc.**

## Repo

https://github.com/halheinrich/XgFilter_Lib
**Branch:** main
**Current commit:** `f134d90`

## Stack

C# / .NET 10 / Class Library / Visual Studio 2026 / Windows

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgFilter_Lib\XgFilter_Lib.slnx`

## Depends on

* ConvertXgToJson_Lib — provides DecisionRow, XgIteratorState, XgMatchInfo, XgGameInfo, XgDecisionIterator, XgFileReader

## Repo directory tree

```
XgFilter_Lib/
  XgFilter_Lib/
    Classification/
      ContactClassifier.cs
      InnerBoard54321Classifier.cs
      InnerBoard631Classifier.cs
      IPositionClassifier.cs
      RaceClassifier.cs
    Enums/
      PlayType.cs
      PositionType.cs
    Filtering/
      DecisionFilterSet.cs
      DecisionTypeFilter.cs
      ErrorRangeFilter.cs
      IDecisionFilter.cs
      IMatchFilter.cs
      MatchScoreFilter.cs
      PlayerFilter.cs
      PlayTypeFilter.cs
      PositionTypeFilter.cs
    Projection/
      ColumnSelector.cs
    FilteredDecisionIterator.cs
    XgFilter_Lib.csproj
  XgFilter_Lib.Tests/
    Classification/
      InnerBoard631ClassifierTests.cs
    Helpers/
      DecisionRowBuilder.cs
    FilteredDecisionIteratorTests.cs
    GlobalUsings.cs
    RaceClassifierTests.cs
    TestPaths.cs
    XgFilter_Lib.Tests.csproj
  XgFilter_Lib.slnx
  INSTRUCTIONS.md
```

## Key files

* XgFilter_Lib.csproj: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/XgFilter_Lib.csproj
* Tests.csproj: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/XgFilter_Lib.Tests.csproj
* GlobalUsings.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/GlobalUsings.cs
* Enums/PositionType.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Enums/PositionType.cs
* Enums/PlayType.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Enums/PlayType.cs
* Filtering/IDecisionFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/IDecisionFilter.cs
* Filtering/IMatchFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/IMatchFilter.cs
* Filtering/DecisionFilterSet.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/DecisionFilterSet.cs
* Filtering/PlayerFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/PlayerFilter.cs
* Filtering/DecisionTypeFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/DecisionTypeFilter.cs
* Filtering/MatchScoreFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/MatchScoreFilter.cs
* Filtering/ErrorRangeFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/ErrorRangeFilter.cs
* Filtering/PositionTypeFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/PositionTypeFilter.cs
* Filtering/PlayTypeFilter.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Filtering/PlayTypeFilter.cs
* Classification/IPositionClassifier.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Classification/IPositionClassifier.cs
* Classification/RaceClassifier.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Classification/RaceClassifier.cs
* Classification/ContactClassifier.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Classification/ContactClassifier.cs
* Classification/InnerBoard631Classifier.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Classification/InnerBoard631Classifier.cs
* Classification/InnerBoard54321Classifier.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Classification/InnerBoard54321Classifier.cs
* Projection/ColumnSelector.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/Projection/ColumnSelector.cs
* FilteredDecisionIterator.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib/FilteredDecisionIterator.cs
* Tests/Helpers/DecisionRowBuilder.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/Helpers/DecisionRowBuilder.cs
* Tests/Classification/RaceClassifierTests.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/Classification/RaceClassifierTests.cs
* Tests/FilteredDecisionIteratorTests.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/FilteredDecisionIteratorTests.cs
* Tests/Classification/InnerBoard631ClassifierTests.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/Classification/InnerBoard631ClassifierTests.cs
* Tests/TestPaths.cs: https://raw.githack.com/halheinrich/XgFilter_Lib/f134d90/XgFilter_Lib.Tests/TestPaths.cs

## Dependency files

### ConvertXgToJson_Lib
Files needed from this dependency (fetch URLs from umbrella INSTRUCTIONS.md):
* Models/DecisionRow.cs
* XgDecisionIterator.cs
* XgMatchInfo.cs
* XgGameInfo.cs
* XgFileReader.cs
* BackgammonConstants.cs

## Architecture

### Enums

* `PositionType` — Contact, Race, Priming, Blitz, HoldingGame
* `PlayType` — Hit, MakePoint, HitAndMakePoint, SlotAndGo, RunningPlay

### Filtering

* `IDecisionFilter` — `bool Matches(DecisionRow row)`; default methods `ShouldAdvanceGame`, `ShouldAdvanceMatch` (both default false)
* `IMatchFilter` — `bool ShouldSkipMatch(XgMatchInfo match)`; `bool ShouldSkipGame(XgGameInfo game)`
* `DecisionFilterSet` — ordered list of IDecisionFilter, AND semantics; fluent `Add()`; `Matches()`; `Apply()`; `ShouldSkipMatch()`; `ShouldSkipGame()`; `ShouldAdvanceGame()`; `ShouldAdvanceMatch()`
* `PlayerFilter` — implements IDecisionFilter + IMatchFilter; include list of player name strings; ShouldSkipMatch skips if neither player is in list
* `DecisionTypeFilter` — checker play, cube, or both; uses `DecisionRow.IsCube`
* `MatchScoreFilter` — implements IDecisionFilter + IMatchFilter; include list of score strings e.g. `"3a5a"`, `"1a1aC"`, `"money"`; ShouldSkipMatch detects money/match mismatch and impossible away scores; ShouldSkipGame skips if game score doesn't match any target tuple; stores targets as `(Away1, Away2, IsCrawford)` tuples internally; money = `(0, 0, false)`
* `ErrorRangeFilter` — min/max double
* `PositionTypeFilter` — include list of PositionType; uses `row.Board` via classifiers — never parses Xgid
* `PlayTypeFilter` — stub

### Classification

* `IPositionClassifier` — `bool Matches(int[] board)`
* `RaceClassifier` — true when no contact between checker blocks
* `ContactClassifier` — Contact = !Race (exhaustive and mutually exclusive for now)
* `InnerBoard631Classifier` — classifies inner board 6-3-1 structure
* `InnerBoard54321Classifier` — classifies inner board 5-4-3-2-1 structure
* Priming, Blitz, HoldingGame classifiers — deferred

### Projection

* `ColumnSelector` — explicit column registry, no reflection; ordered list drives CSV header and row serialization
* Board is never exposed in CSV output

### Early-exit optimization

* `FilteredDecisionIterator` iterates .xg files directly, creates and owns `XgIteratorState`
* Per file: calls `XgDecisionIterator.ExtractMatchInfo(file)`, evaluates `ShouldSkipMatch` — skips entire file if true
* Per game: `XgDecisionIterator.Iterate()` populates `state.GameInfo` from `GameHeaderRecord`; evaluates `ShouldSkipGame` and sets `AdvanceNextGame` if true
* Per row: evaluates `ShouldAdvanceGame` / `ShouldAdvanceMatch` — sets flags for future filters (currently all return false)

### Board layout (DecisionRow.Board)

* `int[]` 26 elements
* `board[0]` = opponent bar (never positive)
* `board[1–24]` = points 1–24 from player on roll's perspective
* `board[25]` = player bar (never negative)
* Positive = player on roll; negative = opponent
* Board is never exposed in CSV output

## Current status

✅ Complete — all filters, classifiers, ColumnSelector, FilteredDecisionIterator with early-exit optimization, full test suite passing

## Deferred

* Priming, Blitz, HoldingGame classifiers
* PlayTypeFilter implementation
* ShouldAdvanceGame / ShouldAdvanceMatch implementations (MoveNumberFilter will be first consumer)
* ColumnSelector wired into ExtractFromXgToCsv UI (column projection)

## Key decisions

* Contact = !Race (exhaustive and mutually exclusive for now)
* PositionTypeFilter uses row.Board via classifiers — never parses Xgid
* Board not exposed in CSV/ColumnSelector
* ColumnSelector uses explicit registry — no reflection
* IMatchFilter is a separate interface — filters implement it alongside IDecisionFilter where applicable
* FilteredDecisionIterator owns XgIteratorState and iterates files directly via XgDecisionIterator.Iterate()
* MatchScoreFilter stores targets as (Away1, Away2, IsCrawford) tuples internally; money = (0, 0, false)
* TestData lives at shared `backgammon\TestData`; referenced via `..\..\TestData` with `Link` in Tests.csproj