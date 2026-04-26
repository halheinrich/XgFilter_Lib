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
    Column.cs
    DecisionTypeOption.cs
    EnumLabel.cs
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
    MoveNumberFilter.cs
    PositionTypeFilter.cs
    PlayTypeFilter.cs
  Classification/
    IPositionClassifier.cs
    IPlayTypeClassifier.cs
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
    BgDecisionDataBuilder.cs
    DecisionFilterAsserts.cs
    DecisionFilterAssertsTests.cs
    DecisionRowBuilder.cs
    RowShape.cs
    RowShapeTests.cs
  Enums/
    EnumLabelTests.cs
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
    MoveNumberFilterTests.cs
    PositionTypeFilterTests.cs
    PlayTypeFilterTests.cs
    DecisionFilterSetTests.cs
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
* `PlayType` — Make20Pt. Each value pairs with an
  `IPlayTypeClassifier` implementation; the single-value shape
  reflects that only `Make20Pt` has a classifier today. The enum
  grows as each new play-shape classifier lands alongside its
  matching value.
* `DecisionTypeOption` — which decision types `DecisionTypeFilter`
  admits. Members: CheckerPlaysOnly, CubeOnly, Both.
* `Column` — CSV columns `ColumnSelector` can project. One member
  per column; declaration order is the default output order. Each
  label is the column's CSV-header text.
* Every member of every enum in this namespace carries a UI-facing
  `[Description]` label. Consumers read it via
  `EnumLabel.ToLabel<TEnum>(value)`. Display text is owned by this
  library, not the UI layer. The helper throws `ArgumentException`
  on undeclared values and on declared members without a
  `[Description]` — missed annotations surface loudly rather than
  degrading to the raw identifier.

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
  target. `ShouldAdvanceMatch` overrides the default to cut the rest of
  the file when no target tuple is reachable from the current state —
  exploits monotonic away-score decrease and the once-per-match Crawford
  rule, branching on (in-Crawford, post-Crawford, pre-Crawford). Crawford
  flag is matched strictly on reachability: a Crawford tuple is
  unreachable from any state where Crawford has already occurred.
* `ErrorRangeFilter` — `double?` min / max on `FilterError`. Returns `false`
  when `FilterError` is `null`, i.e. unanalysed rows are excluded, not
  passed through.
* `MoveNumberFilter` — implements both interfaces. `int?` min / max on
  `MoveNumber`, gated by `IsStandardStart`. Non-standard-start games
  (custom problem positions, Bg960, etc.) have no canonical move
  numbering, so `ShouldSkipGame` drops them wholesale via
  `XgGameInfo.IsStandardStart` before any rows are yielded; `Matches`
  rejects any decision whose `IsStandardStart` is false as a safety
  net. First filter in the codebase to override
  `ShouldAdvanceGame` — once a decision past `max` is seen, no later
  decision in the same game can match, since move numbers increase
  monotonically per game.
* `PositionTypeFilter` — include list of `PositionType`. Reads
  `data.Board` and delegates to `IPositionClassifier` instances. Never
  parses the XGID.
* `PlayTypeFilter` — include list of `PlayType`. Reads `data.Board`,
  `data.AfterBestBoard`, and `data.AfterPlayerBoard` and dispatches
  each selected type to its matching `IPlayTypeClassifier` via an
  internal exhaustive switch that throws on unknown values. OR
  semantics: a row passes when any selected type matches. Cube rows
  always fail — no play was made, so no play-type applies, and the
  after-boards are empty on cube rows by contract. Empty type set →
  always false (empty OR). Shape mirrors `PositionTypeFilter`; the
  enum→classifier correspondence is owned by the filter, not the
  caller.

### Classification

Everything in `Classification/` is `internal`. Consumers never touch a
classifier directly — they pass `PlayType` / `PositionType` enum values
to the filters, which dispatch internally. These types are documented
here because they're substantive internal machinery, not because they're
on the public surface; they do not appear in the Public API block.
`InternalsVisibleTo("XgFilter_Lib.Tests")` makes them reachable from
the test project.

* `IPositionClassifier` — `bool Matches(IReadOnlyList<int> board)`. Board
  is the 26-element on-roll-relative layout from `ConvertXgToJson_Lib`.
* `RaceClassifier` — true when no contact exists between the two checker
  blocks.
* `ContactClassifier` — `!RaceClassifier`. These two partition positions
  today, but that's a local property of the Race/Contact pair, not a
  framework contract — see the multi-membership pitfall below.
* `InnerBoard631Classifier`, `InnerBoard54321Classifier` — inner-board
  shape classifiers.
* `IPlayTypeClassifier` —
  `bool Matches(IReadOnlyList<int> priorBoard,
  IReadOnlyList<int> afterBestBoard,
  IReadOnlyList<int> afterPlayerBoard)`. Three 26-element boards, each
  from the on-roll player's perspective at that moment: priorBoard has
  the decision-maker on roll; the two after-boards have the opponent on
  roll (the turn has flipped). Consequence: what was the decision-
  maker's point X in priorBoard is point `(25 - X)` in the after-boards,
  with their checkers stored negatively. Implementations classify one
  `PlayType` each.
* `Make20PtClassifier` — first `IPlayTypeClassifier` implementation.
  True when the decision-maker's 20-point is not already made
  (`priorBoard[20] < 2`) and exactly one of the two plays makes it —
  under the flipped after-POV the decision-maker's 20-point is index 5
  and their checkers are negative, so "makes" is `afterBoard[5] <= -2`:
  `afterBestBoard[5] <= -2` XOR `afterPlayerBoard[5] <= -2`.

### Projection

* `ColumnSelector` — enum-driven column selection. Constructor takes
  `IEnumerable<Column>`; header text comes from each member's
  `[Description]` label. Typed to `DecisionRow` because the projection
  target is CSV; `Board` is deliberately not exposed as a column. The
  internal `GetValue` switch is exhaustive, throwing
  `ArgumentOutOfRangeException` on undefined `Column` values.

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
   filter set. Any filter that overrides the virtual defaults (today:
   `MatchScoreFilter.ShouldAdvanceMatch`) can vote to cut mid-stream
   after the just-yielded row. `state.AdvanceNextGame` /
   `state.AdvanceNextMatch` are set accordingly before the yield.

## Public API

```csharp
namespace XgFilter_Lib.Enums;

public enum PlayType           { Make20Pt }
public enum PositionType       { Contact, Race, InnerBoard631, InnerBoard54321 }
public enum DecisionTypeOption { CheckerPlaysOnly, CubeOnly, Both }
public enum Column
{
    Xgid, Error, MatchScore, MatchLength, Player, SourceFile,
    Game, MoveNumber, Roll, AnalysisDepth, Equity,
}

public static class EnumLabel
{
    public static string ToLabel<TEnum>(this TEnum value) where TEnum : struct, Enum;
}
```

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

public sealed class PlayerFilter       : IDecisionFilter, IMatchFilter { /* ... */ }
public sealed class DecisionTypeFilter : IDecisionFilter               { /* ... */ }
public sealed class MatchScoreFilter   : IDecisionFilter, IMatchFilter { /* ... */ }
public sealed class ErrorRangeFilter   : IDecisionFilter               { /* ... */ }
public sealed class MoveNumberFilter   : IDecisionFilter, IMatchFilter { /* ... */ }
public sealed class PositionTypeFilter : IDecisionFilter               { /* ... */ }
public sealed class PlayTypeFilter     : IDecisionFilter               { /* ... */ }
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
    public static readonly IReadOnlyList<Column> AllColumns;

    public ColumnSelector();
    public ColumnSelector(IEnumerable<Column> columns);

    public IReadOnlyList<Column> SelectedColumns { get; }
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

None. Cross-cutting items (new classifiers, downstream UI wiring,
filter early-exit extensions) live in the umbrella `INSTRUCTIONS.md`
"Next up" / "Deferred" sections.
