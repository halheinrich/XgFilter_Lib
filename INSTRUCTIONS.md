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
Directory.Build.props
Directory.Packages.props
XgFilter_Lib.slnx
XgFilter_Lib/
  XgFilter_Lib.csproj
  FilteredDecisionIterator.cs
  XgFileStream.cs
  Enums/
    Column.cs
    ContactType.cs
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
    ContactTypeFilter.cs
    PositionTypeFilter.cs
    PositionPatternFilter.cs
    PlayTypeFilter.cs
    AnalysisDepthFilter.cs
  Classification/
    IPositionClassifier.cs
    IPlayTypeClassifier.cs
    RaceClassifier.cs
    ContactClassifier.cs
    InnerBoard631Classifier.cs
    InnerBoard54321Classifier.cs
    VsTwoPlusUpClassifier.cs
    Holding1386Vs20Classifier.cs
    Make20PtClassifier.cs
  Patterns/
    BoardPattern.cs
    BoardPatternJsonConverter.cs
    PointRange.cs
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
    Holding1386Vs20ClassifierTests.cs
    Make20PtClassifierTests.cs
  Filtering/
    PlayerFilterTests.cs
    DecisionTypeFilterTests.cs
    ErrorRangeFilterTests.cs
    MatchScoreFilterTests.cs
    MoveNumberFilterTests.cs
    ContactTypeFilterTests.cs
    PositionTypeFilterTests.cs
    PositionPatternFilterTests.cs
    PlayTypeFilterTests.cs
    AnalysisDepthFilterTests.cs
    DecisionFilterSetTests.cs
    FilterConfigTests.cs
  Patterns/
    BoardPatternTests.cs
    BoardPatternOracleTests.cs
    BoardPatternWireSafetyTests.cs
    PointRangeTests.cs
  Projection/
    ColumnSelectorTests.cs
  Integration/
    FilteredDecisionIteratorTests.cs
    FilteredDecisionIteratorIdTests.cs
    MatchScoreFilterIntegrationTests.cs
    BoardPatternCorpusOracleTests.cs
```

`Directory.Packages.props` at the repo root opts the solution into Central
Package Management: package versions are pinned there and the two csprojs carry
versionless `<PackageReference>`s. `Directory.Build.props` carries the
repo-wide build policy (target framework, nullable, warnings-as-errors,
doc-file generation); per-project decisions stay in each csproj — see the
boundary-rule comment in the file.

## Architecture

### Substrate

All row-level filters operate on `IDecisionFilterData`, defined in
`BgDataTypes_Lib` and implemented by both `DecisionRow` (CSV-shaped) and
`BgDecisionData` (diagram-shaped). A single filter instance applies to either
type — no parallel hierarchies, no conversion at the filter boundary.

### Enums

* `PositionType` — structural board patterns a position can carry.
  Members: InnerBoard631, InnerBoard54321, VsTwoPlusUp,
  Holding1386Vs20. **Not** mutually exclusive: a single position may
  satisfy several at once (e.g. InnerBoard631 + VsTwoPlusUp). Each is
  determinable from the on-roll-relative board array alone — no XGID
  parsing.
* `ContactType` — Contact, Race. Whether a position still carries
  contact or has raced. These two **partition** every position: it is
  exactly one, never both and never neither. Contact-vs-race is an axis
  **orthogonal** to `PositionType`, so the two facets compose via AND
  (e.g. Contact ∧ InnerBoard631) rather than collapsing into
  OR-within-a-single-facet. Also board-derived — no XGID parsing.
  Contact/Race were extracted out of `PositionType` into this dedicated
  enum precisely to make that orthogonality explicit at the type level.
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

The ten concrete filter classes (`PlayerFilter` … `AnalysisDepthFilter`)
and `IMatchFilter` are `internal`. `FilterConfig.Build()` is the intent
surface — consumers state *what* to filter and the library materializes
the `DecisionFilterSet`; only `IDecisionFilter` stays public, as the
`DecisionFilterSet.Add` seam. Like `Classification/`, the internal
filters are documented here as substantive machinery, not public
surface, and are reachable from the test project via
`InternalsVisibleTo("XgFilter_Lib.Tests")`.

* `IDecisionFilter` — `bool Matches(IDecisionFilterData data)`. Two virtual
  defaults for game/match-level early-exit hints:
  `ShouldAdvanceGame(data)` and `ShouldAdvanceMatch(data)`, both returning
  `false` unless a filter opts in.
* `IMatchFilter` — optional extension for filters that can skip an entire
  match or game before any rows are yielded:
  `ShouldSkipMatch(XgMatchInfo)` and `ShouldSkipGame(XgGameInfo)`. Filters
  implement both interfaces where applicable.
* `DecisionFilterSet` — ordered list of `IDecisionFilter` combined with AND
  semantics. Fluent `Add()`. `IsEmpty` is the SSOT for "no filters
  active" — consumers consult it rather than re-inspecting whatever
  config produced the set. `Matches()` passes when every filter passes (or
  the set is empty). `ShouldSkipMatch` / `ShouldSkipGame` delegate to any
  filter in the set that also implements `IMatchFilter`. Duplicates of the
  same concrete filter type are allowed and compose with AND (e.g. two
  `ErrorRangeFilter`s intersect their ranges).
* `FilterConfig` — serializable, default-constructible, mutable DTO that
  bundles every filter's input. Owned by this lib; consumers fill it
  directly (no string-list parsing on the consumer side) and call
  `Build()` to materialize a `DecisionFilterSet`. Empty-list semantics:
  an empty `Players` / `MatchScores` / `ContactTypes` / `PositionTypes` /
  `PlayTypes` / `AnalysisDepthClasses`, and a null-or-empty
  `PositionPattern`, each mean "no
  filter of this kind is active," not "reject everything"; `Build()`
  skips the corresponding `Add()` in that case. Likewise
  `DecisionType = Both` is a no-op and is skipped. Range filters
  (`ErrorRange`, `MoveNumber`) are added if either bound is set.
  Canonical JSON is single-sourced on the type via `ToJson()` /
  `FromJson(string)` / `TryFromJson(string?, out FilterConfig)`, over a
  cached `JsonSerializerOptions` that registers `JsonStringEnumConverter`
  — so the enum-list members round-trip as `["InnerBoard631", ...]`
  name-arrays rather than ordinals (`ContactType` / `PositionType` /
  `PlayType` / `DecisionType` carry no type-level `[JsonConverter]`).
  `AnalysisDepthClasses` and `PositionPattern` are the self-describing
  exceptions that need no converter registered here: `AnalysisDepthClass`
  (owned by `BgDataTypes_Lib`) carries its own type-level
  `JsonStringEnumConverter`, and `BoardPattern` carries its own (see
  **Patterns** below), so both serialize as their string form under these
  options and under any others. `TryFromJson` restores a fresh default
  config on a null argument, the literal `null` token, or malformed JSON.
* `PlayerFilter` — implements both interfaces. `Matches` admits rows where
  the on-roll player is in the include list; `ShouldSkipMatch` drops the
  whole file when neither player is in the list.
* `DecisionTypeFilter` — checker play, cube, or both. Dispatches on
  `data.IsCube`.
* `MatchScoreFilter` — implements both interfaces. Score tokens are
  **on-roll anchored**: `MaNa` means the player on roll needs M points
  and the opponent needs N, so `"4a5a"` and `"5a4a"` are distinct
  targets (include both orientations to admit a score regardless of who
  is on roll). Targets are stored as `(Away1, Away2, IsCrawford)`
  tuples; the `"money"` token is tracked separately as a
  `_includesMoney` bool, not as a `(0, 0, false)` tuple, and bypasses
  score parsing. Only `NaNa[C]`-form tokens go through `ParseScore`,
  which fail-louds with `ArgumentException` (offending token in the
  message) on malformed tokens **and** on impossible scores: away
  scores below 1 (`0a5a`), and Crawford tokens without exactly one side
  1-away and the other ≥ 2 (`3a5aC`, `1a1aC` — a (1,1) game is always
  post-Crawford). Downstream gates rely on these constructor
  invariants instead of re-validating. Each gate is the exact
  projection of `Matches` onto its information granularity:
  `ShouldSkipMatch` detects money-vs-match mismatches and tuples no
  game of an L-point match can carry — orientation-free `max ≤ L`,
  tightened to `max ≤ L − 1` for non-Crawford 1-away tuples (a
  post-Crawford `(1, m)` needs a preceding Crawford `(1, k)` with
  `m < k ≤ L`), with `1a1a` exempt (valid at every L, including the
  1-point match whose only game is `(1, 1, false)`).
  `ShouldSkipGame` compares tuples against the player1/player2-anchored
  game header in **either orientation** (both players roll within a
  game), Crawford flag exact; `Matches` stays the per-decision arbiter
  of orientation. `ShouldAdvanceMatch` cuts the rest of the file only
  when no tuple matches the *current* game (either orientation — the
  producer's cut is immediate and would drop the game's remaining rows)
  **and** no tuple is reachable in any strictly-future game —
  reachability exploits monotonic away-score decrease and the
  once-per-match Crawford rule, branching on (in/post-Crawford,
  pre-Crawford). A Crawford tuple is unreachable once Crawford has
  occurred, and a post-Crawford `(1, m)` requires `m < max(current)`.
  `ShouldAdvanceGame` deliberately stays at the interface default
  (false): a matching decision can be followed by mirror-orientation
  decisions in the same game, so there is no sound game-level cut.
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
* `ContactTypeFilter` — include list of `ContactType`. Same
  read-`data.Board` + private-registry dispatch pattern as
  `PositionTypeFilter`, over the `Contact` / `Race` classifiers. OR
  semantics within the list; because Contact and Race partition every
  position, selecting both is equivalent to no filter and selecting
  neither (an empty set) admits nothing. Contact-vs-race is a separate
  axis from the structural `PositionTypeFilter` and composes with it via
  AND across the set. Unknown enum values are rejected at construction.
* `PositionTypeFilter` — include list of `PositionType`. Reads
  `data.Board` and delegates to `IPositionClassifier` instances via a
  private static `PositionType` → `IPositionClassifier` dictionary
  registry — single source of truth for the enum→classifier
  correspondence. Never parses the XGID. Unknown enum values are
  rejected at construction, not at first dispatch (`Enum.IsDefined`
  guard, `ArgumentOutOfRangeException`). Orthogonal to `ContactTypeFilter`;
  the two compose via AND across the set.
* `PositionPatternFilter` — the general, data-driven counterpart to the
  named `PositionTypeFilter`. Holds a single immutable `BoardPattern`
  (see **Patterns**) and passes rows whose `data.Board` satisfies every
  per-point constraint in it. Where `PositionTypeFilter` dispatches to
  hand-written classifiers, this evaluates an arbitrary sparse
  `[index,min,max]` constraint set, so a caller can express a structural
  shape without a dedicated `PositionType`. An empty pattern matches
  every board.
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
* `AnalysisDepthFilter` — include list of `AnalysisDepthClass`. Unlike the
  board-reading facets, depth is a scalar the producer already stamped on
  each decision (`IDecisionFilterData.AnalysisDepthClass` — the cube
  analysis for cube rows, the best-by-equity candidate for checker rows),
  so this is a direct enum-membership test: no classifier dispatch, no
  board reads. OR semantics; empty set → always false (empty OR). Rows
  carrying `AnalysisDepthClass.Unknown` (legacy archives, or anything the
  producer could not classify) are excluded unless `Unknown` is itself
  selected — the same drop-don't-pass convention as `ErrorRangeFilter`'s
  null `FilterError`, with `Unknown`'s selectability as the deliberate
  opt-in. Deliberately implements only `Matches` — no `IMatchFilter` and
  no `ShouldAdvance*` overrides: depth is not knowable from a match/game
  header and is not monotonic within a game (a single game mixes book,
  N-ply, and rollout decisions), so there is no sound early-exit. Unknown
  enum values are rejected at construction.

### Classification

Everything in `Classification/` is `internal`. Consumers never touch a
classifier directly — they pass `ContactType` / `PositionType` /
`PlayType` enum values to the filters, which dispatch internally. These types are documented
here because they're substantive internal machinery, not because they're
on the public surface; they do not appear in the Public API block.
`InternalsVisibleTo("XgFilter_Lib.Tests")` makes them reachable from
the test project.

* `IPositionClassifier` — `bool Matches(IReadOnlyList<int> board)`. Board
  is the 26-element on-roll-relative layout from `ConvertXgToJson_Lib`.
* `RaceClassifier` — true when no contact exists between the two checker
  blocks. Backs `ContactType.Race`.
* `ContactClassifier` — `!RaceClassifier`; backs `ContactType.Contact`.
  These two partition positions — the invariant the `ContactType` enum
  makes explicit — and `ContactTypeFilter` relies on it. Note this is a
  property of the Race/Contact pair specifically, *not* a framework-wide
  contract on `IPositionClassifier`; the `PositionType` classifiers below
  overlap freely (see the multi-membership pitfall).
* `InnerBoard631Classifier`, `InnerBoard54321Classifier` — inner-board
  shape classifiers.
* `VsTwoPlusUpClassifier` — true when the opponent has ≥ 2 checkers on
  the bar (`board[0] <= -2`). No race guard needed; any checker on the
  bar implies contact.
* `Holding1386Vs20Classifier` — true when the on-roll player holds the
  13-, 8-, and 6-points (each `>= 2`) while the opponent anchors on the
  20 (the player's 5-point under the on-roll POV, `board[5] <= -2`) and
  the player's structure is otherwise on or below the midpoint. The full
  predicate — made points, opponent's 12-anchor, the empty 7/9/10/11
  points, and the no-checkers-above-13 / no-opponent-in-home ranges — is
  on the classifier's XML doc; it is ordered by selectivity (rarest
  signal first) so a non-holding board is rejected in one comparison.
  No race guard needed; the opponent anchor implies contact. Backs
  `PositionType.Holding1386Vs20`.
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

### Patterns

The data-driven position-matching machinery behind `PositionPatternFilter`
and `FilterConfig.PositionPattern`. Where the named `PositionType`
classifiers are hand-written predicates, a `BoardPattern` is a
*declarative* predicate a caller (or the FilterPanel UI) can author at
runtime, including via a compact text form. This is the public,
reintroduction-ready alternative to the named `PositionType` machinery.

* `PointRange` — a `readonly record struct`: an inclusive signed-count
  constraint on one board-array index. `Index` is 0–25 (bars included),
  `Min` / `Max` are inclusive bounds on the on-roll-relative checker
  count there (negative = opponent's checkers; `null` = that side
  unbounded). Validated at construction — `Index` in `[0, MaxIndex]`
  (25), each bound's magnitude ≤ `MaxCheckers` (15), and `Min <= Max` —
  so it is never invalid once it exists; `ArgumentOutOfRangeException` on
  index/bound violations, `ArgumentException` on `Min > Max`. `Contains`
  tests one signed count; `ToString` renders the `[index,min,max]` token
  (unbounded side → empty field). It is a struct with value-equality by
  design — the small immutable element, unlike the pattern that wraps it.
* `BoardPattern` — an immutable, validated bag of `PointRange`
  constraints over the on-roll-relative board (`[0]` opponent bar,
  `[1..24]` points, `[25]` on-roll bar; positive = on-roll player). An
  index named by no range is unconstrained; the empty pattern (`Empty`,
  `IsEmpty`) matches every board (vacuous truth). The one cross-element
  invariant the constructor enforces is **no two ranges on the same
  index** (`ArgumentException`); each element is already self-valid.
  `Matches(board)` ANDs every constraint.
  * **Text form** — the bracket list: whitespace-separated
    `[index,min,max]` tokens, each field comma-separated with an empty
    field meaning "unbounded", e.g. `"[6,,0] [5,2,] [0,,-1]"`. This is
    the form the FilterPanel exposes; **parsing lives in this library**,
    not the UI. `Parse` / `TryParse` read it (throwing vs.
    return-value-on-failure), `ToBracketList` / `ToString` write it, and
    the two round-trip. `Parse` surfaces `FormatException` (malformed
    token), `ArgumentOutOfRangeException` (index/bound), and
    `ArgumentException` (`Min > Max`, duplicate index); `TryParse`
    absorbs all of those into `false`.
  * **Equality** — deliberately **not** value-equality: the backing store
    is a reference-typed `IReadOnlyList`, so structural equality would be
    a footgun (the same reason `FilterConfig` declined it). Compare
    structurally (FluentAssertions `BeEquivalentTo`) or via
    `ToBracketList`.
  * **Serialization** — the type carries `[JsonConverter(typeof(
    BoardPatternJsonConverter))]` on itself, so it round-trips as its
    bracket-list string under *any* `JsonSerializerOptions`; a consumer
    need not remember to register the converter. This is why
    `FilterConfig`'s canonical options list omits it.
* `BoardPatternJsonConverter` — the `JsonConverter<BoardPattern>` the
  type declares. Writes the bracket-list string; reads it back through
  `BoardPattern.Parse`, so deserialization stays on the validated path
  and a malformed/out-of-range pattern in the JSON fails fast as a
  `JsonException` rather than materializing an invalid object. A JSON
  `null` reads as `null`. `internal`: the type-level attribute on
  `BoardPattern` is the only wiring point, and System.Text.Json
  instantiates the attribute-named converter via reflection regardless
  of accessibility — verified on the real wire path by
  `BoardPatternWireSafetyTests`, so the converter needs no public
  presence.

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
`IterateXgDirectoryDiagrams`. The JSON-directory source offers only the
row shape — its diagram variant (`IterateJsonDirectoryDiagrams`) was
deleted as dead code (zero consumers umbrella-wide).

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

The constructor-injected logger is also **forwarded into the producer**:
every `XgDecisionIterator.Iterate` / `IterateDiagramRequests` call passes
`_logger` as its final argument, so per-decision warnings the producer
raises — notably an illegal-play skip — surface through this same
pipeline alongside the file-level skip warnings above, rather than being
swallowed inside the producer.

XG-format file discovery (`*.xg` then `*.xgp`) is delegated to the
producer's public `XgFileReader.EnumerateXgFormatFiles`, the single
source of truth for the rule. The former private duplicate here has
been deleted.

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
public enum ContactType        { Contact, Race }
public enum PositionType       { InnerBoard631, InnerBoard54321, VsTwoPlusUp, Holding1386Vs20 }
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

public sealed class DecisionFilterSet
{
    public DecisionFilterSet Add(IDecisionFilter filter);
    public bool IsEmpty { get; }
    public bool Matches          (IDecisionFilterData data);
    public bool ShouldSkipMatch  (XgMatchInfo match);
    public bool ShouldSkipGame   (XgGameInfo  game);
    public bool ShouldAdvanceGame (IDecisionFilterData data);
    public bool ShouldAdvanceMatch(IDecisionFilterData data);
}

public sealed class FilterConfig
{
    public IList<string>             Players              { get; set; }
    public DecisionTypeOption        DecisionType         { get; set; }
    public IList<string>             MatchScores          { get; set; }
    public double?                   ErrorMin             { get; set; }
    public double?                   ErrorMax             { get; set; }
    public int?                      MoveNumberMin        { get; set; }
    public int?                      MoveNumberMax        { get; set; }
    public IList<ContactType>        ContactTypes         { get; set; }
    public IList<PositionType>       PositionTypes        { get; set; }
    public IList<PlayType>           PlayTypes            { get; set; }
    public IList<AnalysisDepthClass> AnalysisDepthClasses { get; set; }
    public BoardPattern?             PositionPattern      { get; set; }

    public DecisionFilterSet Build();

    public string ToJson();
    public static FilterConfig FromJson(string json);
    public static bool TryFromJson(string? json, out FilterConfig config);
}
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

```csharp
namespace XgFilter_Lib.Patterns;

public readonly record struct PointRange
{
    public const int MaxCheckers = 15;   // ±15 checkers-per-side ceiling
    public const int MaxIndex    = 25;   // on-roll player's bar

    public int  Index { get; }
    public int? Min   { get; }           // inclusive; null = unbounded
    public int? Max   { get; }           // inclusive; null = unbounded

    public PointRange(int index, int? min, int? max);   // validates on construction
    public bool   Contains(int value);
    public override string ToString();   // "[index,min,max]"
}

[JsonConverter(typeof(BoardPatternJsonConverter))]
public sealed class BoardPattern
{
    public static BoardPattern Empty { get; }

    public BoardPattern(IEnumerable<PointRange> ranges);   // rejects duplicate indices

    public IReadOnlyList<PointRange> Ranges { get; }
    public bool IsEmpty { get; }
    public bool Matches(IReadOnlyList<int> board);

    public static BoardPattern Parse(string text);
    public static bool TryParse(string? text, out BoardPattern? pattern);
    public string ToBracketList();
    public override string ToString();   // == ToBracketList()
    // Note: reference equality by design — compare structurally or via ToBracketList().
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
* **`AnalysisDepthFilter` drops `Unknown`-depth rows unless `Unknown` is
  selected.** Rows carrying `AnalysisDepthClass.Unknown` — legacy archives,
  or anything the producer could not classify — are excluded by any
  include-set that does not list `Unknown` explicitly. This is the same
  drop-don't-pass convention `ErrorRangeFilter` applies to a null
  `FilterError`; `Unknown`'s selectability is the deliberate opt-in for
  callers who want the unclassified tail. A filter selecting only real
  depths (`Ply3`, `Rollout`, …) therefore silently omits pre-classification
  data — intended, not a bug.
* **`MatchScoreFilter` tokens are on-roll anchored; game headers are
  player-anchored.** `MaNa` means the *player on roll* needs M — `4a5a`
  and `5a4a` are different targets — but `XgGameInfo.Away1/Away2` are
  anchored to the file's player 1/player 2, and both players roll within
  a game. Any game-level (or coarser) gate must therefore admit a tuple
  in **either orientation** and leave the orientation verdict to
  `Matches`; comparing the header orientation only silently eats every
  decision whose on-roll player is the file's player 2 (the shipped
  4a5a/`Move1_0001.xgp` bug). The same trap applies mid-stream:
  `ShouldAdvanceMatch`'s file-cut fires immediately, so it must treat
  the *current* game's score (either orientation) as still-matchable,
  not just future games.
* **`MatchScoreFilter` has coupled constraints.** Money matches only if
  `matchLength == 0`; a Crawford target requires exactly one side at
  1-away and the other at `≥ 2`; away scores below 1 are impossible.
  The constructor (`ParseScore`) enforces all of these fail-loud, and
  the gates rely on those invariants — adding parse shortcuts that
  bypass validation will let impossible targets through unchecked.
* **`IDecisionFilter.ShouldAdvanceGame` / `ShouldAdvanceMatch` default to
  `false`.** A filter that *can* early-exit mid-game must override them.
  Missing an override is silent — rows just stop being skipped.
* **`PositionType` is multi-membership by design.** A position can satisfy
  several structural categories at once (e.g. InnerBoard631 +
  VsTwoPlusUp). `PositionTypeFilter` takes the union: a row passes when
  *any* selected type matches. Contact-vs-race is a *separate* axis,
  extracted into the `ContactType` enum, where the two members **are**
  mutually exclusive and exhaustive — but that partition is a property of
  that enum, not a filter-level contract on `PositionType`. Composing a
  contact requirement with a structural one is AND across the set (a
  `ContactTypeFilter` alongside a `PositionTypeFilter`), never OR within
  one filter.
* **`BoardPattern` has no value-equality.** Its backing store is a
  reference-typed list, so `==` / `Equals` are reference comparisons — two
  structurally identical patterns compare unequal. Compare via
  `ToBracketList()` or a structural assertion (FluentAssertions
  `BeEquivalentTo`), never `==`. This is deliberate; giving it a
  synthesized structural equality over a mutable-shaped member is the
  footgun `FilterConfig` also declined.
* **`BoardPattern`'s JSON converter must stay declared on the type.** The
  `[JsonConverter(typeof(BoardPatternJsonConverter))]` attribute on
  `BoardPattern` is what makes it round-trip under *any*
  `JsonSerializerOptions` — the immutable type has no settable property
  for its constructor parameter, so the default reflection serializer
  cannot reconstruct it. Remove the attribute and `FilterConfig`'s
  `PositionPattern` silently stops deserializing correctly, because
  `FilterConfig.CanonicalOptions` intentionally does **not** register the
  converter (it relies on the type-level attribute).
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
