# XgFilter_Lib — Project Instructions

Part of the Backgammon tools ecosystem: https://github.com/halheinrich/backgammon

## Repo

https://github.com/halheinrich/XgFilter_Lib
**Branch:** main

## Stack

C# / .NET 10 / Class Library / Visual Studio 2026 / Windows

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgFilter_Lib\XgFilter_Lib.slnx`

## Depends on

* **BgDataTypes_Lib** — IDecisionFilterData, DecisionRow, BgDecisionData, PositionData, DecisionData, DescriptiveData
* **ConvertXgToJson_Lib** — XgDecisionIterator, XgIteratorState, XgMatchInfo, XgGameInfo, XgFileReader, XgFile

## Dependency files

### BgDataTypes_Lib
* BgDataTypes_Lib/IDecisionFilterData.cs
* BgDataTypes_Lib/DecisionRow.cs
* BgDataTypes_Lib/BgDecisionData.cs
* BgDataTypes_Lib/PositionData.cs
* BgDataTypes_Lib/DecisionData.cs

### ConvertXgToJson_Lib
* ConvertXgToJson_Lib/XgDecisionIterator.cs
* ConvertXgToJson_Lib/XgIteratorState.cs
* ConvertXgToJson_Lib/XgMatchInfo.cs
* ConvertXgToJson_Lib/XgGameInfo.cs

## Directory tree

```
XgFilter_Lib/
  XgFilter_Lib/
    XgFilter_Lib.csproj
    FilteredDecisionIterator.cs
    Enums/
      PlayType.cs
      PositionType.cs
    Filtering/
      IDecisionFilter.cs
      IMatchFilter.cs
      DecisionFilterSet.cs
      PlayerFilter.cs
      DecisionTypeFilter.cs
      MatchScoreFilter.cs
      ErrorRangeFilter.cs
      PositionTypeFilter.cs
      PlayTypeFilter.cs
    Classification/
      IPositionClassifier.cs
      RaceClassifier.cs
      ContactClassifier.cs
      InnerBoard631Classifier.cs
      InnerBoard54321Classifier.cs
    Projection/
      ColumnSelector.cs
  XgFilter_Lib.Tests/
    XgFilter_Lib.Tests.csproj
    GlobalUsings.cs
    TestPaths.cs
    Helpers/
      DecisionRowBuilder.cs
      BgDecisionDataBuilder.cs
    Classification/
      RaceClassifierTests.cs
    Filtering/
      BgDecisionDataFilterTests.cs
      DecisionFilterSetTests.cs
      MatchScoreFilterTests.cs
    Integration/
      FilteredDecisionIteratorTests.cs
```

## Architecture

### Enums

* `PositionType` — Contact, Race, Priming, Blitz, HoldingGame
* `PlayType` — Hit, MakePoint, HitAndMakePoint, SlotAndGo, RunningPlay

### Filtering

* `IDecisionFilter` — `bool Matches(IDecisionFilterData data)`; default methods `ShouldAdvanceGame`, `ShouldAdvanceMatch` (both default false)
* `IMatchFilter` — `bool ShouldSkipMatch(XgMatchInfo match)`; `bool ShouldSkipGame(XgGameInfo game)`
* `DecisionFilterSet` — ordered list of IDecisionFilter, AND semantics; fluent `Add()`; `Matches()`; `ShouldSkipMatch()`; `ShouldSkipGame()`; `ShouldAdvanceGame()`; `ShouldAdvanceMatch()`
* `PlayerFilter` — IDecisionFilter + IMatchFilter; ShouldSkipMatch skips if neither player is in list
* `DecisionTypeFilter` — checker play, cube, or both; uses `IsCube`
* `MatchScoreFilter` — IDecisionFilter + IMatchFilter; tuple comparison of (OnRollNeeds, OpponentNeeds, IsCrawford); parses strings like `"3a5a"`, `"1a5aC"`, `"money"`; ShouldSkipMatch detects money/match mismatch and impossible away scores; ShouldSkipGame skips if game score doesn't match
* `ErrorRangeFilter` — min/max double; returns false when FilterError is null
* `PositionTypeFilter` — include list of PositionType; uses `data.Board` via classifiers
* `PlayTypeFilter` — stub

### Classification

* `IPositionClassifier` — `bool Matches(IReadOnlyList<int> board)`
* `RaceClassifier` — true when no contact between checker blocks
* `ContactClassifier` — !Race (exhaustive and mutually exclusive for now)
* `InnerBoard631Classifier` — classifies inner board 6-3-1 structure
* `InnerBoard54321Classifier` — classifies inner board 5-4-3-2-1 structure

### Projection

* `ColumnSelector` — explicit column registry, no reflection; typed to DecisionRow (CSV-specific)

### Early-exit optimization

* `FilteredDecisionIterator` owns `XgIteratorState`, iterates .xg files directly
* Per file: `ExtractMatchInfo` → `ShouldSkipMatch` → skip entire file if true
* Per game: `ShouldSkipGame` → sets `AdvanceNextGame` if true
* Per row: `ShouldAdvanceGame` / `ShouldAdvanceMatch` flags (currently all return false)

## Current status

✅ Complete — all filters operate on IDecisionFilterData; full test suite passing

## Deferred

* Priming, Blitz, HoldingGame classifiers
* PlayTypeFilter implementation
* ShouldAdvanceGame / ShouldAdvanceMatch implementations
* ColumnSelector wired into ExtractFromXgToCsv UI

## Key decisions

* Filters operate on `IDecisionFilterData` — both DecisionRow and BgDecisionData supported
* `IPositionClassifier.Matches` accepts `IReadOnlyList<int>`
* `MatchScoreFilter` uses structured tuple comparison, not string matching
* `ErrorRangeFilter` returns false when `FilterError` is null
* Contact = !Race (exhaustive and mutually exclusive)
* PositionTypeFilter uses `data.Board` via classifiers — never parses Xgid
* Board not exposed in CSV/ColumnSelector
* ColumnSelector uses explicit registry — no reflection
* IMatchFilter separate from IDecisionFilter — filters implement both where applicable
* MatchScoreFilter stores targets as (Away1, Away2, IsCrawford) tuples; money = (0, 0, false)
* TestData at shared `backgammon\TestData`; referenced via `..\..\TestData` with `Link` in Tests.csproj
* Crawford constraint: isCrawford only true if one away == 1 and other > 1
* Away score constraint: needs may only be zero if matchLength == 0 (money)