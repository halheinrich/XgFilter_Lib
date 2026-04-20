# XgFilter_Lib

> Session conventions: [`../CLAUDE.md`](../CLAUDE.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)

## Stack

C# / .NET 10 / Class Library / xUnit. Filtering, classification, and column
projection for backgammon decision records produced by `ConvertXgToJson_Lib`.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgFilter_Lib\XgFilter_Lib.slnx`

## Repo

https://github.com/halheinrich/XgFilter_Lib — branch `main`.

## Depends on

* **BgDataTypes_Lib** — `IDecisionFilterData` (the substrate filters operate
  on), `DecisionRow`, `BgDecisionData`, `PositionData`, `DecisionData`,
  `DescriptiveData`.
* **ConvertXgToJson_Lib** — `XgDecisionIterator`, `XgIteratorState`,
  `XgMatchInfo`, `XgGameInfo`, `XgFileReader`, `XgFile`. Used by
  `FilteredDecisionIterator` to walk `.xg` files and drive early-exit.

## Directory tree

```
XgFilter_Lib.slnx
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
    Make20PtClassifier.cs
  Projection/
    ColumnSelector.cs
XgFilter_Lib.Tests/
  XgFilter_Lib.Tests.csproj
  GlobalUsings.cs
  Helpers/
    DecisionRowBuilder.cs
    BgDecisionDataBuilder.cs
  Classification/
    RaceClassifierTests.cs
    ContactClassifierTests.cs
    InnerBoard631ClassifierTests.cs
    InnerBoard54321ClassifierTests.cs
    Make20PtClassifierTests.cs
  Filtering/
    PlayerFilterTests.cs
    DecisionTypeFilterTests.cs
    ErrorRangeFilterTests.cs
    MatchScoreFilterTests.cs
    PositionTypeFilterTests.cs
    DecisionFilterSetTests.cs
    BgDecisionDataFilterTests.cs
  Projection/
    ColumnSelectorTests.cs
  Integration/
    FilteredDecisionIteratorTests.cs
```

## Architecture

### Substrate

All row-level filters operate on `IDecisionFilterData`, defined in
`BgDataTypes_Lib` and implemented by both `DecisionRow` (CSV-shaped) and
`BgDecisionData` (diagram-shaped). A single filter instance applies to either
type — no parallel hierarchies, no conversion at the filter boundary.

### Enums

* `PositionType` — board-derived classifications a position can carry.
  Members: Contact, Race, InnerBoard631, InnerBoard54321. Categories
  are **not** mutually exclusive: a single position may satisfy
  several (e.g. Contact + InnerBoard631). The unifying property is
  that each is determinable from the on-roll-relative board array
  alone — no XGID parsing.
* `PlayType` — Hit, MakePoint, Make20Pt, HitAndMakePoint, SlotAndGo,
  RunningPlay. Per-type classifiers operate on three 26-element
  on-roll-POV boards: before play, after best play, after user play.
  Today only `Make20Pt` has a classifier implementation; the others
  remain enum-only pending their classifiers.

### Filtering

* `IDecisionFilter` — `bool Matches(IDecisionFilterData data)`. Two virtual
  defaults for game/match-level early-exit hints:
  `ShouldAdvanceGame(data)` and `ShouldAdvanceMatch(data)`, both returning
  `false` unless a filter opts in.
* `IMatchFilter` — optional extension for filters that can skip an entire
  match or game before any rows are yielded:
  `ShouldSkipMatch(XgMatchInfo)` and `ShouldSkipGame(XgGameInfo)`. Filters
  implement both interfaces where applicable.
* `DecisionFilterSet` — ordered list of `IDecisionFilter` combined with AND
  semantics. Fluent `Add()`. `Matches()` passes when every filter passes (or
  the set is empty). `ShouldSkipMatch` / `ShouldSkipGame` delegate to any
  filter in the set that also implements `IMatchFilter`.
* `PlayerFilter` — implements both interfaces. `Matches` admits rows where
  the on-roll player is in the include list; `ShouldSkipMatch` drops the
  whole file when neither player is in the list.
* `DecisionTypeFilter` — checker play, cube, or both. Dispatches on
  `data.IsCube`.
* `MatchScoreFilter` — implements both interfaces. Targets are stored as
  `(OnRollNeeds, OpponentNeeds, IsCrawford)` tuples; money is `(0, 0, false)`.
  Parses strings like `"3a5a"`, `"1a5aC"`, `"money"`. `ShouldSkipMatch`
  detects money-vs-match mismatches and impossible away scores;
  `ShouldSkipGame` drops games whose post-header score cannot reach any
  target.
* `ErrorRangeFilter` — `double?` min / max on `FilterError`. Returns `false`
  when `FilterError` is `null`, i.e. unanalysed rows are excluded, not
  passed through.
* `PositionTypeFilter` — include list of `PositionType`. Reads
  `data.Board` and delegates to `IPositionClassifier` instances. Never
  parses the XGID.
* `PlayTypeFilter` — stub; shape in place, `ClassifyPlay` always returns
  `RunningPlay`. Per-type detection exists for `Make20Pt` (see
  `Make20PtClassifier`) but cannot be invoked here until
  `IDecisionFilterData` grows the three boards the classifiers require.

### Classification

* `IPositionClassifier` — `bool Matches(IReadOnlyList<int> board)`. Board
  is the 26-element on-roll-relative layout from `ConvertXgToJson_Lib`.
* `RaceClassifier` — true when no contact exists between the two checker
  blocks.
* `ContactClassifier` — `!RaceClassifier`. These two partition positions
  today, but that's a local property of the Race/Contact pair, not a
  framework contract — see the multi-membership pitfall below.
* `InnerBoard631Classifier`, `InnerBoard54321Classifier` — inner-board
  shape classifiers.
* `Make20PtClassifier` — first play-shape classifier. Signature
  `Matches(before, afterBest, afterUser)` over three 26-element
  on-roll-POV boards. True when the 20-point is not already made
  (`before[20] < 2`) and exactly one of the two plays makes it
  (`afterBest[20] >= 2` XOR `afterUser[20] >= 2`). No `IPlayClassifier`
  interface yet — reintroduce when a second play-shape classifier
  arrives.

### Projection

* `ColumnSelector` — explicit column registry, no reflection. Typed to
  `DecisionRow` because the projection target is CSV; `Board` is
  deliberately not exposed as a column.

### FilteredDecisionIterator

The top-level integration point. Owns an `XgIteratorState` and iterates
`.xg` or `.json` files in a directory, yielding only `DecisionRow` records
that pass the supplied `DecisionFilterSet`. The two public methods
(`IterateXgDirectory`, `IterateJsonDirectory`) share a private helper
that differs only in file glob and reader delegate.

Early-exit pipeline, applied in this order:

1. **Per file** — `XgDecisionIterator.ExtractMatchInfo` → `ShouldSkipMatch`.
   Skip the entire file if true.
2. **Per game** — `ShouldSkipGame` sets `state.AdvanceNextGame` so the
   underlying iterator jumps straight to the next game.
3. **Per row** — `ShouldAdvanceGame` / `ShouldAdvanceMatch` flags on the
   filter set (all `false` today, reserved for future filters that can
   decide to cut from mid-game).

## Public API

```csharp
namespace XgFilter_Lib.Filtering;

public interface IDecisionFilter
{
    bool Matches(IDecisionFilterData data);
    virtual bool ShouldAdvanceGame (IDecisionFilterData data) => false;
    virtual bool ShouldAdvanceMatch(IDecisionFilterData data) => false;
}

public interface IMatchFilter
{
    bool ShouldSkipMatch(XgMatchInfo match);
    bool ShouldSkipGame (XgGameInfo  game);
}

public sealed class DecisionFilterSet
{
    public DecisionFilterSet Add(IDecisionFilter filter);
    public bool Matches          (IDecisionFilterData data);
    public bool ShouldSkipMatch  (XgMatchInfo match);
    public bool ShouldSkipGame   (XgGameInfo  game);
    public bool ShouldAdvanceGame (IDecisionFilterData data);
    public bool ShouldAdvanceMatch(IDecisionFilterData data);
}

public sealed class PlayerFilter      : IDecisionFilter, IMatchFilter { /* ... */ }
public sealed class DecisionTypeFilter : IDecisionFilter              { /* ... */ }
public sealed class MatchScoreFilter   : IDecisionFilter, IMatchFilter { /* ... */ }
public sealed class ErrorRangeFilter   : IDecisionFilter              { /* ... */ }
public sealed class PositionTypeFilter : IDecisionFilter              { /* ... */ }
public sealed class PlayTypeFilter     : IDecisionFilter              { /* ... */ }
```

```csharp
namespace XgFilter_Lib.Classification;

public interface IPositionClassifier
{
    bool Matches(IReadOnlyList<int> board);
}

public sealed class RaceClassifier            : IPositionClassifier { /* ... */ }
public sealed class ContactClassifier         : IPositionClassifier { /* ... */ }
public sealed class InnerBoard631Classifier   : IPositionClassifier { /* ... */ }
public sealed class InnerBoard54321Classifier : IPositionClassifier { /* ... */ }
```

```csharp
namespace XgFilter_Lib;

public static class FilteredDecisionIterator
{
    public static IEnumerable<DecisionRow> IterateXgDirectory(
        string xgDir, DecisionFilterSet filters);

    public static IEnumerable<DecisionRow> IterateJsonDirectory(
        string jsonDir, DecisionFilterSet filters);
}
```

```csharp
namespace XgFilter_Lib.Projection;

public sealed class ColumnSelector
{
    public static readonly IReadOnlyList<string> AllColumns;

    public ColumnSelector();
    public ColumnSelector(IEnumerable<string> columns);

    public IReadOnlyList<string> SelectedColumns { get; }
    public string Header { get; }
    public string Serialize(DecisionRow row);
    public string BuildCsv (IEnumerable<DecisionRow> rows);
}
```

## Pitfalls

* **`PositionTypeFilter` reads `data.Board`, never the XGID.** The board
  array is already in on-roll-relative form; parsing the XGID would
  re-derive it and risk perspective bugs. Classifiers must keep taking
  `IReadOnlyList<int>`.
* **`ErrorRangeFilter` drops unanalysed rows.** When `FilterError` is
  `null` the filter returns `false` — unanalysed `.xgp` positions are
  excluded, not admitted as "zero error". Changing that silently regresses
  CSV exports.
* **`MatchScoreFilter` has coupled constraints.** Money matches only if
  `matchLength == 0`; a non-zero `IsCrawford` target requires exactly one
  side at 1-away and the other at `> 1`. `ShouldSkipMatch` enforces both;
  adding parse shortcuts that bypass these checks will let impossible
  targets through.
* **`IDecisionFilter.ShouldAdvanceGame` / `ShouldAdvanceMatch` default to
  `false`.** A filter that *can* early-exit mid-game must override them.
  Missing an override is silent — rows just stop being skipped.
* **`PositionType` is multi-membership by design.** A position can satisfy
  several categories at once (e.g. Contact + InnerBoard631).
  `PositionTypeFilter` takes the union: a row passes when *any* selected
  type matches. Race and Contact happen to be mutually exclusive and
  exhaustive today, but that's a property of those two classifiers, not a
  contract at the filter level — future categories introduced alongside
  Contact will overlap with it and do not need a carve-out.
* **Shared `TestData` at `backgammon\TestData`.** Referenced via
  `..\..\TestData` with `Link` in the Tests csproj. Moving TestData or
  changing csproj output depth breaks every file-touching test.

## Subproject-internal next steps

None. Cross-cutting items (new classifiers, PlayTypeFilter implementation,
downstream UI wiring, filter early-exit extensions) live in the umbrella
`INSTRUCTIONS.md` "Next up" / "Deferred" sections.
