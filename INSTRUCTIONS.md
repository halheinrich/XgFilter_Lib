# XgFilter_Lib

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
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
* **ConvertXgToJson_Lib** — `XgDecisionIterator`, `XgIteratorCallbacks`,
  `XgMatchInfo`, `XgGameInfo`, `XgFileReader`, `Models.XgFile`. Used by
  `FilteredDecisionIterator` to walk `.xg` files and drive early-exit
  via callback registration on the producer.

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
    FilterConfig.cs
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
    VsTwoPlusUpClassifier.cs
    Make20PtClassifier.cs
  Projection/
    ColumnSelector.cs
XgFilter_Lib.Tests/
  XgFilter_Lib.Tests.csproj
  GlobalUsings.cs
  Helpers/
    BgDecisionDataBuilder.cs
    BoardBuilder.cs
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
    VsTwoPlusUpClassifierTests.cs
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
    FilterConfigTests.cs
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
  Members: Contact, Race, InnerBoard631, InnerBoard54321, VsTwoPlusUp.
  Categories are **not** mutually exclusive: a single position may
  satisfy several (e.g. Contact + InnerBoard631, or Contact +
  VsTwoPlusUp). The unifying property is that each is determinable
  from the on-roll-relative board array alone — no XGID parsing.
* `PlayType` — Make20Pt. The enum has one member per
  `IPlayTypeClassifier` implementation, and grows as new
  play-shape classifiers land alongside their matching values.
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
  filter in the set that also implements `IMatchFilter`. Duplicates of the
  same concrete filter type are allowed and compose with AND (e.g. two
  `ErrorRangeFilter`s intersect their ranges).
* `FilterConfig` — serializable, default-constructible, mutable DTO that
  bundles every filter's input. Owned by this lib; consumers fill it
  directly (no string-list parsing on the consumer side) and call
  `Build()` to materialize a `DecisionFilterSet`. Empty-list semantics:
  an empty `Players` / `MatchScores` / `PositionTypes` / `PlayTypes`
  means "no filter of this kind is active," not "reject everything";
  `Build()` skips the corresponding `Add()` in that case. Likewise
  `DecisionType = Both` is a no-op and is skipped. Range filters
  (`ErrorRange`, `MoveNumber`) are added if either bound is set.
  JSON-round-trippable with `JsonStringEnumConverter` for enum
  properties; the wire format is `["InnerBoard631", ...]`-style
  string-array per enum list.
* `PlayerFilter` — implements both interfaces. `Matches` admits rows where
  the on-roll player is in the include list; `ShouldSkipMatch` drops the
  whole file when neither player is in the list.
* `DecisionTypeFilter` — checker play, cube, or both. Dispatches on
  `data.IsCube`.
* `MatchScoreFilter` — implements both interfaces. Targets are stored as
  `(Away1, Away2, IsCrawford)` tuples; the `"money"` token is tracked
  separately as a `_includesMoney` bool, not as a `(0, 0, false)` tuple.
  Accepts strings like `"3a5a"`, `"1a5aC"`, `"money"`: the `"money"`
  token is recognized as a special token (sets `_includesMoney`) and
  bypasses score parsing. Only `NaNa[C]`-form tokens go through
  `ParseScore` and are validated. Malformed `NaNa[C]` tokens throw
  `ArgumentException` at construction (the offending token appears in
  the message). `ShouldSkipMatch`
  detects money-vs-match mismatches and impossible away scores;
  `ShouldSkipGame` drops games whose post-header score cannot reach any
  target. `ShouldAdvanceMatch` overrides the default to cut the rest of
  the file when no target tuple is reachable from the current state —
  exploits monotonic away-score decrease and the once-per-match Crawford
  rule, branching on (in-Crawford, post-Crawford, pre-Crawford). Crawford
  flag is matched strictly on reachability: a Crawford tuple is
  unreachable from any state where Crawford has already occurred.
* `ErrorRangeFilter` — `double?` min / max on `FilterError`. Returns `false`
  when `FilterError` is `null`, i.e. unanalyzed rows are excluded, not
  passed through.
* `MoveNumberFilter` — implements both interfaces. `int?` min / max on
  `MoveNumber`, gated by `IsStandardStart`. Non-standard-start games
  (custom problem positions, Bg960, etc.) have no canonical move
  numbering, so `ShouldSkipGame` drops them wholesale via
  `XgGameInfo.IsStandardStart` before any rows are yielded; `Matches`
  rejects any row whose `IsStandardStart` is false as a safety net.
  Overrides `ShouldAdvanceGame`: once a row past `max` is seen, no
  later row in the same game can match, since move numbers increase
  monotonically per game.
* `PositionTypeFilter` — include list of `PositionType`. Reads
  `data.Board` and delegates to `IPositionClassifier` instances via a
  private static `PositionType` → `IPositionClassifier` dictionary
  registry — single source of truth for the enum→classifier
  correspondence. Never parses the XGID. Unknown enum values are
  rejected at construction, not at first dispatch (`Enum.IsDefined`
  guard, `ArgumentOutOfRangeException`).
* `PlayTypeFilter` — include list of `PlayType`. Reads `data.Board`,
  `data.AfterBestBoard`, and `data.AfterPlayerBoard` and dispatches
  each selected type to its matching `IPlayTypeClassifier` via the
  same private-registry pattern as `PositionTypeFilter`. OR
  semantics: a row passes when any selected type matches. Cube rows
  always fail — no play was made, so no play-type applies, and the
  after-boards are empty on cube rows by contract. Checker rows whose
  after-boards are empty also fail — the producer emits empty
  `AfterBestBoard` / `AfterPlayerBoard` as a sentinel for "no analyzed
  after-state available" (e.g. the player's move was not in XG's
  analyzed candidate set). Empty type set → always false (empty OR).
  The enum→classifier correspondence is owned by the filter, not the
  caller. Unknown enum values are rejected at construction.

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
* `ContactClassifier` — `!RaceClassifier`. These two partition positions,
  but that's a local property of the Race/Contact pair, not a framework
  contract — see the multi-membership pitfall below.
* `InnerBoard631Classifier`, `InnerBoard54321Classifier` — inner-board
  shape classifiers.
* `VsTwoPlusUpClassifier` — true when the opponent has ≥ 2 checkers on
  the bar (`board[0] <= -2`). No race guard needed; any checker on the
  bar implies contact.
* `IPlayTypeClassifier` —
  `bool Matches(IReadOnlyList<int> priorBoard,
  IReadOnlyList<int> afterBestBoard,
  IReadOnlyList<int> afterPlayerBoard)`. Three 26-element boards, each
  from the on-roll player's perspective at that moment: priorBoard has
  the decision-maker on roll; the two after-boards have the opponent on
  roll (the turn has flipped). Consequence: what was the
  decision-maker's point X in priorBoard is point `(25 - X)` in the
  after-boards, with their checkers stored negatively. Implementations
  classify one `PlayType` each.
* `Make20PtClassifier` — `IPlayTypeClassifier` implementation. True
  when the decision-maker's 20-point is not already made
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

### Iteration

The top-level integration point. A sealed instance class constructed
with `(DecisionFilterSet, ILogger<FilteredDecisionIterator>)` — both
required, null-guarded. Filters are configuration; the directory is
the per-call argument. Walks XG-format files (`*.xg` match files plus
`*.xgp` position files) or `*.json` files, yielding only the rows
that pass the configured filter set.

Two output shapes are exposed: `DecisionRow` (CSV-flat) via
`IterateXgDirectory` / `IterateJsonDirectory`, and `BgDecisionData`
(diagram-shaped — full `Plays` list, after-boards) via
`IterateXgDirectoryDiagrams` / `IterateJsonDirectoryDiagrams`.

Two **input** sources are exposed, both shapes for each. The directory
methods above walk a filesystem path. The stream methods —
`IterateXgStreams` (`DecisionRow`) and `IterateXgStreamDiagrams`
(`BgDecisionData`) — take a caller-supplied `IEnumerable<XgFileStream>`
instead, parsing each via `XgFileReader.ReadStream`. This is the
directory-free entry for callers that hold the bytes rather than a
server path — e.g. a Blazor WASM client parsing browser-picked files
that never leave the browser. The directory and stream paths are
WASM-compatible by dependency (both `XgFilter_Lib` and its deps
`BgDataTypes_Lib` / `ConvertXgToJson_Lib` are plain `net10.0`); the
directory methods simply rely on `System.IO.Directory`, which compiles
under `browser-wasm` but is never called by a remote consumer.

All entry points funnel through a single generic private core,
`IterateSources<T>`, which iterates `(string sourceFile, Func<XgFile> read)`
pairs — so the filter-evaluation, malformed-file skip+log, and early-exit
pipeline is guaranteed identical across every shape *and* every source.
Only the terminal yield and the per-source mapping differ:

* directory paths map `p → (Path.GetFileName(p), () => ReadFile(p))` via
  the thin `IterateFiles<T>` adapter (extension preserved by
  `Path.GetFileName`);
* streams map `XgFileStream → (FileName, () => ReadStream(Data))` via
  `ToSources`, after `RequireValid` enforces the name contract.

The output shape is selected by passing `XgDecisionIterator.Iterate` or
`XgDecisionIterator.IterateDiagramRequests` as the source delegate; the
`where T : IDecisionFilterData` constraint binds the filter calls
identically for either shape.

The read is always deferred into the thunk and invoked inside
`IterateSources`'s try/catch, so a malformed *file/stream content* is
logged and skipped (iteration continues). A malformed *stream name*
(null/blank/extension-less name, null `Data`) is a different category —
a usage error — and `RequireValid` throws `ArgumentException` from the
`ToSources` projection, *outside* the try/catch, so it is never
swallowed as a skipped file. `sourceFile` is passed straight through to
the producer and **must carry its extension** — the producer's
`DecisionId` stamping derives the `.xg`/`.xgp`/`.json` discrimination
from `Path.GetExtension(sourceFile)` and throws on an extension-less
name. The directory mapper preserves it; the stream mapper validates it.

Files that fail to read are skipped and logged via
`ILogger.LogWarning(ex, "Skipping {File}", path)` — the original
exception (type, stack, inner) is captured on the log entry, not
stringified. Iteration continues with the next file rather than
aborting the run.

A private static `EnumerateXgFormatFiles` helper concatenates
`*.xg` and `*.xgp` enumerations, mirroring the equivalent private
helper inside `ConvertXgToJson_Lib.XgDecisionIterator`.

Early-exit pipeline. At the start of each directory walk the iterator
constructs a single `XgIteratorCallbacks` record threading the filter
set's four skip / advance predicates to the producer:

```
SkipMatchAt    ← DecisionFilterSet.ShouldSkipMatch     (XgMatchInfo)
SkipGameAt     ← DecisionFilterSet.ShouldSkipGame      (XgGameInfo)
StopGameAfter  ← DecisionFilterSet.ShouldAdvanceGame   (IDecisionFilterData)
StopMatchAfter ← DecisionFilterSet.ShouldAdvanceMatch  (IDecisionFilterData)
```

The producer evaluates each predicate at its declared boundary (match
header, game header, post-yield) and short-circuits its own iteration
when the predicate returns `true`. The consumer's loop is reduced to
a filter-and-yield: every item produced by `source(...)` is gated by
`DecisionFilterSet.Matches` and yielded if it passes. No iterator
state is observed; `XgIteratorState` is passed as `null`. The
match-skip decision flows through `SkipMatchAt`, which the producer
wires up internally.

The architectural ruling is that the consumer has no direct
`SkipMatch` / `SkipGame` mutator on the producer's surface: all skip
semantics are declarative, supplied once as the four pre-registered
callbacks. The producer owns iteration control; the consumer owns
the predicates; `XgIteratorState` remains a read-only observer for
callers that want per-row context (this consumer does not).

## Public API

```csharp
namespace XgFilter_Lib.Enums;

public enum PlayType           { Make20Pt }
public enum PositionType       { Contact, Race, InnerBoard631, InnerBoard54321, VsTwoPlusUp }
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

public sealed class FilterConfig
{
    public IList<string>         Players       { get; set; }
    public DecisionTypeOption    DecisionType  { get; set; }
    public IList<string>         MatchScores   { get; set; }
    public double?               ErrorMin      { get; set; }
    public double?               ErrorMax      { get; set; }
    public int?                  MoveNumberMin { get; set; }
    public int?                  MoveNumberMax { get; set; }
    public IList<PositionType>   PositionTypes { get; set; }
    public IList<PlayType>       PlayTypes     { get; set; }

    public DecisionFilterSet Build();
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

/// A named XG-format source supplied as a stream rather than a path.
/// FileName must carry its extension (.xg/.xgp/.json); Data is read once,
/// forward, and is owned/disposed by the caller. See the stream-ownership
/// pitfall below.
public readonly record struct XgFileStream(string FileName, Stream Data);

public sealed class FilteredDecisionIterator
{
    public FilteredDecisionIterator(
        DecisionFilterSet filters,
        ILogger<FilteredDecisionIterator> logger);

    // Directory sources
    public IEnumerable<DecisionRow>      IterateXgDirectory          (string xgDir);
    public IEnumerable<DecisionRow>      IterateJsonDirectory        (string jsonDir);
    public IEnumerable<BgDecisionData>   IterateXgDirectoryDiagrams  (string xgDir);
    public IEnumerable<BgDecisionData>   IterateJsonDirectoryDiagrams(string jsonDir);

    // Stream / file-list sources (directory-free; WASM-friendly)
    public IEnumerable<DecisionRow>      IterateXgStreams      (IEnumerable<XgFileStream> files);
    public IEnumerable<BgDecisionData>   IterateXgStreamDiagrams(IEnumerable<XgFileStream> files);
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
* **`ErrorRangeFilter` drops unanalyzed rows.** When `FilterError` is
  `null` the filter returns `false` — unanalyzed `.xgp` positions are
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
  exhaustive, but that's a property of those two classifiers, not a
  contract at the filter level — future categories introduced alongside
  Contact will overlap with it and do not need a carve-out.
* **Shared `TestData` at `backgammon\TestData`.** Referenced via
  `..\..\TestData` with `Link` in the Tests csproj. Moving TestData or
  changing csproj output depth breaks every file-touching test.
* **`XgFileStream` ownership + lazy-read hazard.** The stream entries
  (`IterateXgStreams` / `IterateXgStreamDiagrams`) are `yield`-based, so
  each `XgFileStream.Data` is read *during enumeration*, not at the call
  site. The caller owns the stream and must keep it open, unread, and
  positioned at the start until enumeration reaches it — a stream disposed
  before its deferred read throws (and is then swallowed as a skipped
  file). The boring-safe pattern, used by the tests, is to buffer the
  bytes up front (`new MemoryStream(File.ReadAllBytes(path))`) so the
  stream cannot be pulled out from under the read. The iterator does not
  dispose streams. Separately, `FileName` **must** carry its extension:
  `RequireValid` throws `ArgumentException` on a null/blank/extension-less
  name (a usage error, surfaced loudly) — distinct from a malformed
  *content* stream, which is skip+logged like any unreadable file.

## Subproject-internal next steps

* Test builders (`BgDecisionDataBuilder`, `DecisionRowBuilder`) currently stamp
  every constructed instance with the same fixed placeholder
  `XgpDecisionId("test.xgp")`. Acceptable today because no filter test asserts
  on `Id`, but encapsulation-suboptimal: identical-Id instances become a
  silent collision risk the moment Id assertions enter the test surface.
  Promote to deriving `Id` from existing builder state (the row builder's
  `sourceFile` parameter; add a matching parameter to the diagram builder)
  when an Id-asserting test lands. Documented inline on both builders'
  XML doc.
