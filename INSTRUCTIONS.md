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
  via callback registration on the producer. Tests additionally use
  `XgFileBuilder` / `XgGameBuilder` — the producer's one public path to an
  in-memory `XgFile` — to synthesize integration fixtures; the XG record
  model behind it is internal to the producer and cannot be hand-built.

## Layout

Two projects of its own, governed by repo-root `Directory.Build.props` (TFM,
nullable, `TreatWarningsAsErrors`, XML doc generation) and
`Directory.Packages.props` (Central Package Management — no inline `Version=`
anywhere). What is repo-wide policy versus a per-project decision is settled by
the boundary comment in `Directory.Build.props` itself; `LangVersion`,
`InternalsVisibleTo`, and the test project's doc-file opt-out are csproj-local
under that rule. `XgFilter_Lib.slnx` additionally carries the
`BgDataTypes_Lib` and `ConvertXgToJson_Lib` project files, so the dependency
chain builds from this solution rather than from packages.

**`XgFilter_Lib/`** — the library. Five areas plus two top-level types:

- **`Enums/`** — the vocabulary consumers name. The filterable categories
  (`ContactType`, `PositionType`, `PlayType`), `DecisionTypeOption`, the
  projectable `Column` set, and two reflection vocabularies over
  `FilterConfig`: `FilterFacet` (complete — one member per add/skip gate) and
  `FilterField` (deliberately partial — one member per individually-blameable
  field). `EnumLabel` reads the `[Description]` labels, keeping display text
  owned by the library that defines the enum rather than by a UI layer.
  `StrictJsonStringEnumConverter<T>` is bundled by type-level attribute onto
  the four enums that cross a wire, so they serialize as declaration names and
  reject numeric ordinals under *any* consumer's serializer.
- **`Filtering/`** — the intent surface and the machinery behind it.
  `FilterConfig` is the serializable DTO a consumer fills; its `Build()`
  materializes a `DecisionFilterSet` from the eleven `internal` filter classes,
  which implement the `IDecisionFilter` / `IMatchFilter` seams.
  `NamedFilterCollection`, with its type-bundled `internal` converter, is the
  versioned saved-filters document a consumer persists. `MatchScoreToken` is
  the one public *grammar* here — the score-token vocabulary and its wordless
  verdict — standing to `MatchScoreFilter` as `BoardPattern` stands to
  `PositionPatternFilter`.
- **`Classification/`** — the `internal` predicates the category filters
  dispatch to: `IPositionClassifier` / `IPlayTypeClassifier` plus one
  hand-written implementation per enum member. Board-reading only, never
  XGID-parsing.
- **`Patterns/`** — the declarative counterpart to those named classifiers:
  `BoardPattern` over `CheckerRange` over `CheckerLocation`, plus the converter
  that carries the bracket-list text form onto the wire.
- **`Projection/`** — `ColumnSelector`, the `Column`-driven CSV projection.
- **Top level** — `FilteredDecisionIterator`, the integration point that walks
  XG and JSON sources through a filter set, and `XgFileStream`, the named
  stream its directory-free entries take.

**`XgFilter_Lib.Tests/`** — xUnit, mirroring the library's folders with one
test class per type, plus two folders the library has no counterpart for:
`Helpers/` (the `BgDecisionDataBuilder` / `DecisionRowBuilder` fixture
builders, `BoardBuilder`, the shared `DecisionFilterAsserts`, and the
`FakeGameInfo` / `FakeMatchInfo` headers) and `Integration/` (end-to-end
iterator runs and the corpus oracles). Reaches the library's `internal`
surface via `InternalsVisibleTo` and the umbrella corpus at `..\..\TestData`
via a `Link` item — see the TestData pitfall.

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
* `FilterFacet` — the facet vocabulary of `FilterConfig`: one member
  per add/skip gate `Build()` recognizes (Players, DecisionType,
  MatchScores, ErrorRange, MoveNumberRange, ContactTypes,
  PositionTypes, PlayTypes, AnalysisDepth, DiceRolls,
  PositionPattern), declared in `Build()`'s add order. The depth
  facet's three per-mode toggle+levels pairs are ONE facet. The UI-shelved
  `PositionTypes` / `PlayTypes` stay in the vocabulary because they
  remain `Build()`-reachable (old saved configs can carry them); each
  member retires together with its facet. Labels match the
  FilterPanel's visible section headings, so an "N hidden filters
  active" signal names the sections the user finds on expanding.
* `FilterField` — the companion vocabulary to `FilterFacet`, and the
  currency of `FilterConfig.GetInvalidFields()`: one member per
  individually-blameable *field* a validity rule can name (MatchScores,
  ErrorMin, ErrorMax, MoveNumberMin, MoveNumberMax), declared in that order.
  Deliberately **partial**, and it grows with the rules — a member exists iff
  some `FieldRules` row can name it, so a facet with no rule to state (a
  checkbox list, whose only failure mode is an undefined enum value no UI can
  produce) is absent rather than permanently valid. Read it as the blameable
  set, never as an inventory of the config's members; `FilterFacet` is the
  complete vocabulary. Fields rather than facets because a consumer marks
  inputs: a range facet contributes one member per bound, so a misordered pair
  can blame both ends.
* `MatchScoreTokenFault` — the typed verdict `MatchScoreToken.GetFault`
  returns on one `MatchScores` entry: `None`, `Malformed`, `Retired`. Two
  faults, not one per parse rule, because the only distinction that changes
  what a consumer *says* is retired-vocabulary versus wrong-shape: a retired
  token gets its replacements named, everything else gets retyped. Carries no
  text — see **The score-token grammar** below.
* Every member of every enum in this namespace carries a UI-facing
  `[Description]` label — `FilterField` and `MatchScoreTokenFault` excepted,
  deliberately. `FilterField`'s members name a consumer's own input, whose
  caption this library never renders, and `MatchScoreTokenFault`'s name a
  verdict the consumer words in its own voice (the reasoning is on each
  enum). Consumers read it via
  `EnumLabel.ToLabel<TEnum>(value)`. Display text is owned by this
  library, not the UI layer. The helper throws `ArgumentException`
  on undeclared values and on declared members without a
  `[Description]` — missed annotations surface loudly rather than
  degrading to the raw identifier.

### Filtering

The eleven concrete filter classes (`PlayerFilter` … `DiceRollFilter`)
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
  `PlayTypes` / `DiceRolls`, and a null-or-empty `PositionPattern`, each
  mean "no filter of this kind is active," not "reject everything";
  `Build()` skips the corresponding `Add()` in that case. The depth facet is the
  one exception to the single-member pattern — three per-mode pairs
  (`IncludeEvaluations`+`EvaluationLevels`, `IncludeRollouts`+`RolloutLevels`,
  `IncludeBookRollouts`+`BookRolloutLevels`); it is inactive (skipped) iff
  all three toggles are off (see **Depth facet semantics** below).
  Likewise `DecisionType = Both` is a no-op and is skipped. Range filters
  (`ErrorRange`, `MoveNumber`) are added if either bound is set.
  Canonical JSON is single-sourced on the type via `ToJson()` /
  `FromJson(string)` / `TryFromJson(string?, out FilterConfig)`, over a
  cached `JsonSerializerOptions` that **registers no converters at all** — every
  member that needs one carries it on its own type. `DecisionType` /
  `ContactTypes` / `PositionTypes` / `PlayTypes` carry
  `StrictJsonStringEnumConverter<T>` (this repo's, in `Enums/`); the depth level
  lists hold `AnalysisLevel`, which carries `BgDataTypes_Lib`'s equivalent; and
  `DiceRoll` / `BoardPattern` carry their own. So every enum member round-trips
  as an `["InnerBoard631", ...]` name-array and a numeric ordinal is rejected
  (halheinrich/backgammon#164), `DiceRolls` rides as a `["31","66"]` token
  array, and the three mode toggles are plain booleans.

  Putting the strictness on the **types** rather than on these options is the
  point, not an accident: a `FilterConfig` crosses wires this library does not
  own — ExtractFromXgToCsv POSTs one to its local server under ASP.NET Core's
  stock options — and only a type attribute reaches those
  (halheinrich/backgammon#37, where those enums had been crossing as bare
  ordinals). It matters because a saved filter is durable and `AnalysisLevel`'s
  declaration order is contractual and interleaved: the 2026-08-28 `Ply3Red`
  insertion renumbered the ladder, so an ordinal read back today would name a
  different level than when it was written. The blanket options-level
  registration that used to sit here became exactly redundant once the
  attributes landed and was removed (halheinrich/backgammon#16) — while it
  stood it *outranked* the attributes and masked the removal of any of them.
  Note the same precedence cuts both ways: a consumer that registers a loose
  converter on its own options can still lower the floor. `TryFromJson` restores a fresh default
  config on a null argument, the literal `null` token, or malformed JSON;
  separately, retired field names (`AnalysisDepthClasses`, the shared
  `AnalysisLevels` list) are simply ignored on read, so old saved configs
  reset to an inactive depth facet — accepted, the Contact/Race precedent.

  Internally the per-facet add/skip gates live in a single private
  `FacetRules` table — `(FilterFacet, activation predicate, filter
  factory)` triples in add order — that both `Build()` and
  `GetActiveFacets()` iterate, so materialization and the activity
  query share one predicate per facet and cannot drift.
  `GetActiveFacets()` returns the facets `Build()` would materialize
  as an `IReadOnlySet<FilterFacet>` (backed by a `SortedSet`, so it
  enumerates in declaration = add order); it judges **presence, not
  validity** — a malformed `MatchScores` token or an undefined enum
  value still reports its facet active, `Build()` stays the point
  that throws, and `GetActiveFacets()` itself never throws. This is
  the lib-side surface behind the FilterPanel's "N hidden filters
  active" signal: the panel consults the config's own activation
  verdicts rather than judging facet activity from its edit buffers
  (the same consult-the-result ruling as `DecisionFilterSet.IsEmpty`).

  **Validity** is the other axis, and the mirror of the above: a private
  `FieldRules` table of `(FilterField, violation predicate)` pairs, each
  predicate **delegating to the facet that owns the semantic**, feeds
  `GetInvalidFields()` — an `IReadOnlySet<FilterField>` naming the fields
  whose values `Build()` would throw on. The table is sparse by design; the
  facets a user fills in by hand are the ones with rows — the match-score
  tokens (via `MatchScoreToken.GetFault`, halheinrich/backgammon#121, the "one
  enum member, one row" join halheinrich/backgammon#39 booked for this facet),
  the error bounds (via `ErrorRangeFilter.IsBoundNonNegative` /
  `.AreBoundsOrdered`, halheinrich/backgammon#39), and the move-number bounds
  (via `MoveNumberFilter.IsBoundAtLeastOne` / `.AreBoundsOrdered`,
  halheinrich/backgammon#119). A checkbox list has no rule to state, so it
  contributes none.

  A range facet contributes **two** rows for one rule pair, because a consumer
  marks inputs rather than facets. Each row composes its own bound's
  admissibility with the pair's ordering rule, and the composition is what
  keeps blame honest: a bound wrong on its own value is named alone, while a
  misordered pair — both bounds individually admissible, still out of order —
  blames both ends and leaves the user to choose which to move. A bound
  already at fault for its own value drags the pair out of order as a side
  effect, and that consequence must not red the field the user got right. The
  two range facets differ only in the floor each bound must clear, and each
  floor is stated by the filter that owns it: zero for an error magnitude, one
  for a 1-based move ordinal. Like `GetActiveFacets()` it never
  throws and is computed fresh from mutable state, and it is deliberately
  **not** a gate on assignment: setters accept anything, so a saved document
  written before a rule existed still loads and the offending value can be
  shown back to the user rather than lost to a failed restore. Validity is
  orthogonal to activity — a retired score token still reports its facet
  active while being named here.

  The verdict carries **no message**, for either facet. The lib rules on the
  input and the consumer says so in its own voice (the same division as
  `BoardPattern.TryParse`). Where a consumer needs more than "this field is
  wrong" — which entry, and whether it is retired vocabulary rather than a
  typo — it asks `MatchScoreToken.GetFault` per token and reads the typed
  fault plus `RetiredMoneyReplacements`; facts and values cross the API,
  never prose. Both surfaces read the same rule, so they cannot disagree.

  **Value equality** (`IEquatable<FilterConfig>`, `Equals` +
  `GetHashCode`): a config's identity is its content, so two configs
  describing the same filtering are equal whoever built them. Structural
  over every member — the nine list facets, the four range bounds,
  `DecisionType`, the three depth toggles, and `PositionPattern`. The
  list facets compare **order-insensitively** (as multisets, tallied by
  occurrence; the hash aggregates with XOR so a permuted selection hashes
  alike), strings compare ordinally, and `PositionPattern` delegates to
  `BoardPattern`'s own equality — `null` and `Empty` staying distinct, as
  everywhere else on the type. A null list member counts as empty, so
  equality is total and never throws on a config an explicit JSON `null`
  produced. Two accepted edges, both erring toward *reporting* a
  difference: duplicate entries make `{A,A} ≠ {A}` though both build the
  same filter (unreachable through a checkbox UI), and comparison is of
  intent, not of the materialized `DecisionFilterSet`. This is the
  lib-side surface behind the FilterPanel's Apply gate
  (halheinrich/backgammon#49): the panel compares its built config with
  the last-committed one rather than re-materializing or serializing
  anything — a `ToJson()` comparison was explicitly rejected. No `==` /
  `!=` operators; see the pitfalls.

* `NamedFilterCollection` — the versioned **saved-filters document**: an
  immutable collection of named `FilterConfig` entries, the pick list a
  consumer offers when the user saves and reloads configurations. The library
  does no I/O — consumers load bytes, deserialize, apply the withers
  (`With(name, config)` adds or replaces, `Without(name)` removes and is
  idempotent; each returns a new collection and leaves the receiver
  untouched), and serialize back.

  * **One comparer is the whole name rule.** `OrdinalIgnoreCase` is the single
    definition of "same name" for duplicate rejection, `Contains` /
    `GetConfig` / `TryGetConfig` lookup, replace-on-`With`, `Without`, and the
    canonical sort — which the rule makes total, since case-variant duplicates
    cannot coexist. Display case is preserved as typed, and a replacing `With`
    stores the new spelling (last write wins for name and config alike). Names
    are validated, never coerced: blank or untrimmed names are rejected, in
    memory and on the wire.
  * **One canonical order.** Entries sort by name — in `Names` and on the wire
    — so a given collection always serializes to the same content regardless
    of the add/remove sequence that built it. Reads accept entries in any
    order and re-canonicalize: order is presentation, not semantics, so a
    hand-reordered file is not corruption (the duplicate check still gates).
  * **Snapshot contract.** `FilterConfig` is deliberately mutable (UI state
    binds to it), so this document stores each config's *serialized value*,
    never the caller's instance: `With` snapshots on the way in through the
    canonical `FilterConfig.ToJson` / `FromJson` round-trip — which also
    normalizes, so the stored value is exactly what the wire will carry — and
    every retrieval hands out a fresh snapshot. Mutating a config after saving
    it, or mutating one retrieved from the document, never affects the
    document; saving an edit back is an explicit `With`.
  * **Strict envelope, tolerant payload.** JSON via the type-bundled
    `internal` converter (type-level `[JsonConverter]` — consumers register
    nothing) at `CurrentSchemaVersion`. The envelope — version, structure,
    names — is fail-loud, with a version bump as its only evolution mechanism;
    entry config bodies delegate to `FilterConfig`'s own deserialization,
    which ignores unknown members, so a retired config facet never bricks a
    user's saved collection. The persistence trio matches `FilterConfig`'s:
    `ToJson` / `FromJson` / `TryFromJson`, the last absorbing the absent,
    null-token, and malformed cases into `Empty`.
  * A plain class, not a record: it wraps a collection, where record equality
    would silently be reference equality. Instances compare by reference.

  Because a stored config is restored rather than re-entered, this is also
  where `GetInvalidFields()` earns its posture: a document written before a
  rule existed loads intact and reports invalid at apply, instead of failing
  the restore and losing the value the user has to fix.

* **Depth facet semantics.** User-facing selection state is three per-mode
  pairs — a toggle plus its own level list (`IncludeEvaluations` +
  `EvaluationLevels`, `IncludeRollouts` + `RolloutLevels`,
  `IncludeBookRollouts` + `BookRolloutLevels`) — raw intent the config stores
  verbatim. `Build()` is the **single source of truth** for deriving the
  filter's clause union from that intent, and the derivation is:
  * **Facet inactive** (whole facet passes everything, filter not added) iff
    all three toggles are off. A level list whose toggle is off is **inert**:
    it neither activates the facet nor constrains anything, and is never
    validated.
  * Otherwise the filter gets **one clause per enabled toggle**, carrying that
    mode's level list verbatim (empty = *any level*). A row passes iff any
    clause admits it: `clause.Mode == AnalysisMode && (clause levels empty ||
    AnalysisLevel ∈ clause levels)` — a union of per-mode conjunctions, so a
    level selection qualifies **only its own mode**.
  * Canonical example (the beta report's, inexpressible under the old shared
    level set): Rollouts on with no levels + Evaluations at XG Roller++ →
    rollout rows pass at *any* inner level, and evaluation rows pass only at
    Roller++. Adding any further toggle or level strictly grows the matched
    set. A book hit with an `Unknown` level (unenriched, V1, or eval-baseline
    book entry) passes only via the Book-rollouts toggle with no
    book-rollout level checked (or `Unknown` explicitly in that list).
  * `Unknown` mode (legacy/unstamped rows) is never selectable — no clause
    can name it — so those rows pass only when the facet is inactive.
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
  tuples; the two money tokens are tracked separately as a bool each,
  not as `(0, 0, false)` tuples, and bypass score parsing. Everything
  else goes through `MatchScoreToken.ParseScore` — the grammar and its
  fail-loud behaviour live there, not here (see **The score-token
  grammar** below). Downstream gates rely on the resulting constructor
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

  **Money sessions and the Jacoby rule** (halheinrich/backgammon#121).
  The two money tokens are separate targets, each admitting money
  records under one rule — `moneyJ` admits
  `IsMoneyGame && IsJacoby == true`, `moneyNJ` admits
  `IsMoneyGame && IsJacoby == false`. Wanting money under either rule
  means **listing both**, exactly as admitting a score regardless of
  who is on roll means listing both orientations. A money record whose
  Jacoby fact is unknown (`IsJacoby` `null`) matches **neither** — an
  unknown rule is never guessed into a side (the illegal state is
  upstream's to prevent, halheinrich/backgammon#142; the filter simply
  never admits it). The `== true` / `== false` spellings are
  load-bearing against the tri-state: the near-misses `!= false` /
  `!= true` each admit the unknown record into one side, which is what
  the unknown-side pins in `MatchScoreFilterTests` exist to catch.
  Match scores are untouched by the money tokens and vice versa.

  The header gates cannot see the fact — `IMatchInfo` / `IGameInfo`
  carry no Jacoby member, by their "members are added on demand"
  minimalism — so at header scope a money session is admissible iff
  **either** money token is listed. That is still the exact projection
  onto the information those headers carry (a header cannot distinguish
  the two rules, both occur under it, `Matches` stays the arbiter) —
  the same shape as the orientation projection above.
* `MatchScoreToken` — **the score-token grammar**, and the one public
  type in `Filtering/` besides `FilterConfig`, `DecisionFilterSet`,
  `NamedFilterCollection`, and `IDecisionFilter`. It states once, for
  the whole library, what a `MatchScores` entry may say: a `NaNa[C]`
  match score, or one of the money tokens. `MatchScoreFilter` parses
  through it, `FilterConfig`'s field-rule table judges through it, and
  a consumer asks it directly.

  * **Vocabulary as exported constants** — `MoneyWithJacoby`
    (`"moneyJ"`), `MoneyWithoutJacoby` (`"moneyNJ"`), `RetiredMoney`
    (`"money"`), plus `RetiredMoneyReplacements` (both rule-bearing
    tokens, in order). Spellings live here once; a consumer rendering
    them in help text, a placeholder, or an explanation reads the
    constants rather than repeating literals, the same export
    discipline as `FilterHelp.StorageSectionAnchorId`.
  * **Case and whitespace** — the whole grammar is case-insensitive and
    trims incidental *surrounding* whitespace before judging anything:
    the `a` separators, the `C` Crawford suffix, and the money tokens
    with their `J` / `NJ` suffixes alike, so `MONEYNJ`, `moneynj`, and
    `moneyNJ` are one token. Embedded whitespace and repeated
    separators are still rejected. (This regularised one inconsistency:
    the old bare `money` check compared *untrimmed* while score tokens
    trimmed.)
  * **`GetFault(token)`** — the single statement of token validity,
    returning a `MatchScoreTokenFault` and **never a sentence**. Null
    is `Malformed` rather than an exception, so
    `GetInvalidFields` keeps its never-throws contract on a config an
    explicit JSON `null` produced.
  * **`ParseScore`** (internal) fail-louds with `ArgumentException`
    (offending token in the message) on malformed tokens, on impossible
    scores — away scores below 1 (`0a5a`), Crawford tokens without
    exactly one side 1-away and the other ≥ 2 (`3a5aC`, `1a1aC`, a
    (1,1) game being always post-Crawford) — and on the retired token.
    Both it and `GetFault` route through one private `Inspect`, so the
    answer a consumer asks for and the answer `Build()` enforces cannot
    drift.
  * **The `money` retirement** (halheinrich/backgammon#121). `money` is
    no longer a valid token: a pattern containing it is **invalid**,
    named by `GetInvalidFields()` and rejected by `Build()`, carrying
    the `Retired` fault so a consumer can distinguish it from a typo
    and offer `RetiredMoneyReplacements`. The two alternatives were
    both rejected as *silent semantic changes*: reinterpreting it as
    "either rule" would quietly widen a saved filter's meaning, and
    letting it match nothing would quietly narrow it — either way the
    user gets a result they cannot explain from what they typed. The
    verdict is loud instead, and messageless: the lib rules, the
    consumer words it (the #39 posture, extended with a typed fault and
    replacement data rather than prose). The token survives as a
    constant because `DecisionRow.MatchScore` still *writes* it, for
    the one money row that has no rule to state; written out it is an
    honest "unknown", read back in as a target it is retired. That
    asymmetry is deliberate.
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

  Bounds are **constrained, not merely stored** (halheinrich/backgammon#119).
  `IsBoundAtLeastOne` and `AreBoundsOrdered` state the rule once for the whole
  library and the constructor enforces it: move numbers are 1-based, so a
  lower bound below 1 only restates the open end while an upper bound below 1
  admits nothing at all, and `min > max` is empty by construction. None is a
  filter a user could mean, so each is a construction error — `Build()` throws
  where it once handed back a filter that silently matched nothing. Same
  posture as `ErrorRangeFilter`'s magnitude bounds, differing only in the
  floor.
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
  per-location constraint in it. Where `PositionTypeFilter` dispatches to
  hand-written classifiers, this evaluates an arbitrary sparse
  `[location,min,max]` constraint set — board indices and the derived
  borne-off locations alike — so a caller can express a structural shape
  without a dedicated `PositionType`. An empty pattern matches every
  board.
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
* `AnalysisDepthFilter` — the depth facet as a **union of per-mode clauses**
  over the two-axis analysis taxonomy (`AnalysisMode` × `AnalysisLevel`) that
  replaced the retired flat `AnalysisDepthClass`. Unlike the board-reading
  facets, depth is a scalar pair the producer already stamped on each decision
  (`IDecisionFilterData.AnalysisMode` / `AnalysisLevel` — the cube analysis
  for cube rows, the best-by-equity candidate for checker rows), so this is
  a direct membership test: no classifier dispatch, no board reads.
  Constructed with a non-empty set of `Clause`s (a nested validated record:
  one `AnalysisMode` plus that mode's own level set); a row passes iff any
  clause admits it — mode equality AND (clause levels empty || level ∈ clause
  levels). Per-clause levels are the point: a level selection qualifies only
  its own mode, so "(any rollout) OR (evaluation at Roller++)" is one filter,
  and rollout rows are never constrained by evaluation levels (rollout rows'
  levels are *inner* levels — checker rollouts never carry Roller-family
  inner levels, which is why the old shared level set matched no rollouts).
  An empty clause collection is rejected at construction (an inactive facet
  is expressed by omitting the filter, never by an empty union); within a
  clause the level set may be empty, meaning "any level" — how an unenriched
  book hit (`BookRollout` + `Unknown` level) is admitted. `Clause` rejects
  mode `Unknown` (no selection produces it), so legacy/unstamped
  `Unknown`-mode rows pass only when the whole facet is inactive and this
  filter is absent from the set — the same drop-don't-pass posture the old
  designs had. Clauses on the same mode are legal and simply union.
  Deliberately implements only `Matches` — no `IMatchFilter` and no
  `ShouldAdvance*` overrides: depth is not knowable from a match/game header
  and is not monotonic within a game (a single game mixes book, N-ply, and
  rollout decisions), so there is no sound early-exit. Undefined
  `AnalysisMode` / `AnalysisLevel` values are rejected at construction.
* `DiceRollFilter` — include list of `DiceRoll`. Like the depth facet, the
  roll is a scalar the producer already stamped on each decision
  (`IDecisionFilterData.Dice`), so this is a direct set-membership test — no
  classifier dispatch, no board reads. OR semantics: a row passes iff its
  `Dice` value-equals a selected roll, over `DiceRoll`'s record-struct value
  equality. `DiceRoll` is canonical-unordered by construction, so 3-1 ≡ 1-3
  and the filter does **no** normalization of its own; the include-set and the
  row's roll may each be spelled in either dice order. Cube rows always fail —
  `Dice` is null (no roll exists before the cube is offered), the same
  drop-don't-pass posture `PlayTypeFilter`/`ErrorRangeFilter` apply to their
  null cases. Empty set → always false (empty OR), which `FilterConfig.Build`
  keeps out of the set by skipping the add. Unlike the enum facets there is no
  unknown-value guard: `DiceRoll` is validated by construction, so an ill-formed
  roll cannot reach the set. Deliberately `Matches`-only — no `IMatchFilter`,
  no `ShouldAdvance*`: dice are not knowable from a match/game header and are
  not monotonic within a game, so there is no sound early-exit (the
  `AnalysisDepthFilter` reasoning).

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

* `CheckerLocation` — a `readonly record struct`, the discriminated
  place a constraint addresses, and the **single source of truth for
  location vocabulary**: a `Kind` (`CheckerLocationKind.Board` /
  `PlayerOff` / `OpponentOff`), the named tokens the grammar uses
  (`off` / `opp-off`), each location's signed value interval (board
  `[-15, 15]`, player-off `[0, 15]`, opponent-off `[-15, 0]`), and how a
  location's value is read or derived from a board (`ValueOn`,
  internal). Constructed only via `Board(int index)` (validated 0–25,
  `ArgumentOutOfRangeException`) and the `PlayerOff` / `OpponentOff`
  statics, so never invalid; `BoardIndex` is `null` on the off locations
  (no throwing property). `default(CheckerLocation)` is `Board(0)`.
  Carries the domain constants `MaxBoardIndex` (25) and `MaxCheckers`
  (15) — location-domain facts, so they live on the location type.
  Value-equality is what `BoardPattern` keys duplicate detection — and
  its own equality — on, so a numeric and a named location never
  conflate.
  `ToString` renders the canonical (lower-case) token head; name parsing
  is case-insensitive.
* `CheckerRange` — a `readonly record struct`: an inclusive signed-count
  constraint on one `CheckerLocation`, the element of a pattern.
  `Min` / `Max` are inclusive bounds on the on-roll-relative checker
  count at the location (negative = opponent; `null` = that side
  unbounded). Validated at construction — each bound must lie in the
  **location's own value interval**, so a wrong-signed borne-off bound
  (e.g. `[off,-2,]`) is an `ArgumentOutOfRangeException`, not a
  constraint that silently never matches; `ArgumentException` on
  `Min > Max`. The `(int index, min, max)` ctor remains as sugar for the
  board-location case. `Contains` tests one signed count; internal
  `IsSatisfiedBy(board)` pairs it with `CheckerLocation.ValueOn`;
  `ToString` renders the `[location,min,max]` token (unbounded side →
  empty field). A struct with value-equality by design — which is what
  `BoardPattern`'s own equality delegates to, element by element, and
  what its duplicate-location check keys on.
* `BoardPattern` — an immutable, validated bag of `CheckerRange`
  constraints over the on-roll-relative board (`[0]` opponent bar,
  `[1..24]` points, `[25]` on-roll bar; positive = on-roll player). A
  location named by no range is unconstrained; the empty pattern
  (`Empty`, `IsEmpty`) matches every board (vacuous truth). The one
  cross-element invariant the constructor enforces is **no two ranges on
  the same location** (`ArgumentException`, keyed on `CheckerLocation`
  value-equality, so the check spans numeric and named locations alike);
  each element is already self-valid. `Matches(board)` ANDs every
  constraint; borne-off values are derived per element (see the
  derivation pitfall), and board indexing never exceeds the real 26
  elements.
  * **Text form** — the bracket list: whitespace-separated
    `[location,min,max]` tokens, each field comma-separated with an
    empty bound field meaning "unbounded". The location head is a board
    index or a named borne-off location — `[off,min,max]` (on-roll
    player, bounds `[0, 15]`) / `[opp-off,min,max]` (opponent, bounds
    `[-15, 0]`, negative per the grammar-wide sign rule:
    `[opp-off,,-2]` = "opponent has ≥ 2 off", reading exactly like
    `[5,,-2]`), e.g. `"[6,,0] [5,2,] [off,1,] [opp-off,,-2]"`. Names
    parse case-insensitively and render canonically lower-case. This is
    the form the FilterPanel exposes; **parsing lives in this library**,
    not the UI. `Parse` / `TryParse` read it (throwing vs.
    return-value-on-failure), `ToBracketList` / `ToString` write it, and
    the two round-trip. `Parse` surfaces `FormatException` (malformed
    token, unknown location name), `ArgumentOutOfRangeException` (index
    / bound, including wrong-signed off bounds), and `ArgumentException`
    (`Min > Max`, duplicate location); `TryParse` absorbs all of those
    into `false`.
  * **Equality** — value-based over the constraint set, via
    `IEquatable<BoardPattern>` (`Equals` + a consistent `GetHashCode`).
    Two patterns are equal when they carry the same `CheckerRange`
    constraints **in any order**: the constructor already treats order as
    insignificant, and the no-duplicate-location invariant makes the
    constraints a set, so the comparison is an exact set comparison and
    the hash an order-independent (XOR) aggregate. Element comparison
    delegates to `CheckerRange`'s record-struct value equality — no
    second encoding of "same constraint" anywhere. Patterns parsed from
    the same bracket list are therefore always equal; the converse holds
    only up to token order, because `ToBracketList` preserves
    construction order and two equal patterns may render permuted lists.
    No `==` / `!=` operators: it is a reference type, and by the
    prevailing convention its operators keep reference semantics — call
    `Equals`.
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
`_logger` as the named `logger:` argument (the producer's signature grew an
optional `XgIteratorOptions? options` leg between `callbacks` and `logger`,
which this consumer leaves defaulted — it supplies no opening book), so
per-decision warnings the producer raises — notably an illegal-play skip —
surface through this same pipeline alongside the file-level skip warnings
above, rather than being swallowed inside the producer.

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
public enum FilterFacet          // declaration order == FilterConfig.Build()'s add order
{
    Players, DecisionType, MatchScores, ErrorRange, MoveNumberRange,
    ContactTypes, PositionTypes, PlayTypes, AnalysisDepth, DiceRolls,
    PositionPattern,
}
public enum FilterField          // deliberately partial: one member per rule that can name it
{
    MatchScores, ErrorMin, ErrorMax, MoveNumberMin, MoveNumberMax,
}
public enum MatchScoreTokenFault { None, Malformed, Retired }

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

/// The score-token grammar and vocabulary: spellings once, verdicts typed.
public static class MatchScoreToken
{
    public const string MoneyWithJacoby    = "moneyJ";
    public const string MoneyWithoutJacoby = "moneyNJ";
    public const string RetiredMoney       = "money";   // retired as a target

    public static IReadOnlyList<string> RetiredMoneyReplacements { get; }

    public static MatchScoreTokenFault GetFault(string? token);   // never throws
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

public sealed class FilterConfig : IEquatable<FilterConfig>
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
    public bool                      IncludeEvaluations   { get; set; }
    public IList<AnalysisLevel>      EvaluationLevels     { get; set; }
    public bool                      IncludeRollouts      { get; set; }
    public IList<AnalysisLevel>      RolloutLevels        { get; set; }
    public bool                      IncludeBookRollouts  { get; set; }
    public IList<AnalysisLevel>      BookRolloutLevels    { get; set; }
    public IList<DiceRoll>           DiceRolls            { get; set; }
    public BoardPattern?             PositionPattern      { get; set; }

    public DecisionFilterSet Build();
    public IReadOnlySet<FilterFacet> GetActiveFacets();   // presence
    public IReadOnlySet<FilterField> GetInvalidFields();  // validity; no message

    public string ToJson();
    public static FilterConfig FromJson(string json);
    public static bool TryFromJson(string? json, out FilterConfig config);

    public bool Equals(FilterConfig? other);   // value equality over every
    public override bool Equals(object? obj);  // member; list facets compare
    public override int GetHashCode();         // as multisets. No == / !=.
}

/// The versioned saved-filters document: immutable, name-keyed
/// (OrdinalIgnoreCase), canonically sorted by name, storing each config as a
/// serialized snapshot rather than the caller's instance. Wire format via a
/// type-bundled internal converter — consumers register nothing. Reference
/// equality; the library does no I/O.
public sealed class NamedFilterCollection
{
    public const int CurrentSchemaVersion = 1;

    public static NamedFilterCollection Empty { get; }

    public int                   Count { get; }
    public IReadOnlyList<string> Names { get; }   // canonical order

    public bool         Contains    (string name);
    public FilterConfig GetConfig   (string name);   // KeyNotFoundException when absent
    public bool         TryGetConfig(string name, out FilterConfig? config);

    public NamedFilterCollection With   (string name, FilterConfig config);  // add or replace
    public NamedFilterCollection Without(string name);                       // idempotent

    public string ToJson();
    public static NamedFilterCollection FromJson(string json);
    public static bool TryFromJson(string? json, out NamedFilterCollection collection);
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

public enum CheckerLocationKind { Board, PlayerOff, OpponentOff }

public readonly record struct CheckerLocation
{
    public const int MaxBoardIndex = 25;   // on-roll player's bar
    public const int MaxCheckers   = 15;   // per-side checker ceiling

    public static CheckerLocation PlayerOff   { get; }   // "off";     values [0, 15]
    public static CheckerLocation OpponentOff { get; }   // "opp-off"; values [-15, 0]
    public static CheckerLocation Board(int index);      // validated 0–25

    public CheckerLocationKind Kind       { get; }
    public int?                BoardIndex { get; }       // null for the off locations

    public override string ToString();   // "6" | "off" | "opp-off" (canonical lower-case)
}

public readonly record struct CheckerRange
{
    public CheckerLocation Location { get; }
    public int?            Min      { get; }   // inclusive; null = unbounded
    public int?            Max      { get; }   // inclusive; null = unbounded

    public CheckerRange(int index, int? min, int? max);   // board-location sugar
    public CheckerRange(CheckerLocation location, int? min, int? max);  // validates
                                                        // bounds per location
    public bool   Contains(int value);
    public override string ToString();   // "[location,min,max]"
}

[JsonConverter(typeof(BoardPatternJsonConverter))]
public sealed class BoardPattern : IEquatable<BoardPattern>
{
    public static BoardPattern Empty { get; }

    public BoardPattern(IEnumerable<CheckerRange> ranges);   // rejects duplicate indices

    public IReadOnlyList<CheckerRange> Ranges { get; }
    public bool IsEmpty { get; }
    public bool Matches(IReadOnlyList<int> board);

    public static BoardPattern Parse(string text);
    public static bool TryParse(string? text, out BoardPattern? pattern);
    public string ToBracketList();
    public override string ToString();   // == ToBracketList()

    public bool Equals(BoardPattern? other);   // value equality over the
    public override bool Equals(object? obj);  // constraint set, order-
    public override int GetHashCode();         // insensitive. No == / !=.
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
* **Facet activation gates live only in `FilterConfig`'s `FacetRules`
  table.** `Build()` and `GetActiveFacets()` both iterate the one private
  table of `(facet, predicate, factory)` triples; that shared predicate is
  what makes the activity query drift-proof. Adding a facet's add/skip
  check as an ad-hoc `if` in `Build()` (or a parallel rule in the query,
  or in a consumer) re-creates exactly the two-encodings hazard the table
  exists to kill — a new facet means a new `FilterFacet` member plus a new
  table row, nothing else.
* **Depth levels qualify only their own mode.** Each level list binds
  exclusively to its paired toggle's clause: `EvaluationLevels` never
  constrains rollout rows, `RolloutLevels` never constrains evaluations, and
  a level list whose toggle is off is **inert** (no activation, no
  constraint, no validation). Rollout-family level lists select on the
  rollout's **inner** level — and checker rollouts never carry Roller-family
  inner levels, so offering Roller-family choices as "rollout levels" in a UI
  (or copying one mode's levels onto another's clause) recreates the
  match-nothing defect the clause union was introduced to fix.
* **The depth facet's clause union is derived in `Build()`, not the panel.**
  `FilterConfig` stores raw user intent (three per-mode toggle+levels pairs);
  the mapping to `AnalysisDepthFilter` clauses — one clause per enabled
  toggle carrying its own level list, plus the "all toggles off = inactive"
  rule — lives **only** in `FilterConfig` (the depth facet's private
  `FacetRules` entry and its filter factory, both reached solely through
  `Build()`). It is the single source of truth. The XgFilter_Razor panel
  must bind the six raw inputs and let `Build()` derive the clauses;
  re-encoding the derivation in the UI (e.g. pre-computing clauses or an
  `AnalysisMode` list and stuffing it into the config) duplicates the SSOT
  and will silently drift — for instance losing the inactive rule or the
  inert-levels rule.
* **The depth facet drops `Unknown`-mode rows whenever it is active.**
  No selection produces mode `Unknown` (`Clause` rejects it at
  construction), so any active depth facet excludes legacy/unstamped rows
  (`AnalysisMode.Unknown`) — they pass only when the facet is inactive and
  `AnalysisDepthFilter` is absent from the set. This is the same
  drop-don't-pass posture `ErrorRangeFilter` applies to a null
  `FilterError`. Separately, a clause's level axis is unconstrained when its
  list is empty (any level, including `Unknown`), so an unenriched book hit
  rides through on the Book-rollouts toggle alone; checking any concrete
  book-rollout level then excludes those `Unknown`-level hits — intended,
  not a bug.
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
* **`MatchScoreFilter` has coupled constraints.** A money token matches
  only if `matchLength == 0`; a Crawford target requires exactly one
  side at 1-away and the other at `≥ 2`; away scores below 1 are
  impossible. `MatchScoreToken.ParseScore` enforces all of these
  fail-loud, and the gates rely on those invariants — adding parse
  shortcuts that bypass validation will let impossible targets through
  unchecked.
* **The Jacoby fact is tri-state, and `!= false` / `!= true` are the
  trap.** `IDecisionFilterData.IsJacoby` is `bool?`: `true` / `false` on
  a money record, `null` when the rule was never stamped (and `null` on
  a match record, where the question does not arise). `moneyJ` /
  `moneyNJ` must be spelled `IsMoneyGame && IsJacoby == true` /
  `== false`. The near-miss spellings each silently admit the
  unknown-rule record into one side — a wrong answer that no
  known-rule test would catch, which is why the unknown-side pins are
  called out by name in `MatchScoreFilterTests`.
* **Adding a validity rule means adding it in one place, not two.** A
  facet's rule belongs on the facet that owns the semantic
  (`ErrorRangeFilter.IsBoundNonNegative`,
  `MoveNumberFilter.IsBoundAtLeastOne`, `MatchScoreToken.GetFault`);
  `FilterConfig`'s `FieldRules` row only *routes* to it. Writing the
  predicate inline in the table would let `GetInvalidFields()` and
  `Build()` drift, and the swept agreement tests in `FilterConfigTests`
  exist to catch exactly that.
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
* **Borne-off counts are derived, never stored.** The `[off,…]` /
  `[opp-off,…]` locations compute a side's off count as 15 minus its
  on-board sum **with bars included** (`board[0]` / `board[25]` count as
  on-board), the opponent's value signed negative. Don't plumb off-count
  data into `IDecisionFilterData` or grow the board past 26 elements to
  support them — the derivation in `CheckerLocation.ValueOn` is the SSOT.
  A malformed board carrying more than 15 checkers a side yields an
  out-of-interval derived value that simply fails any off constraint
  (garbage in, no throw — same posture as `Matches` on any other absurd
  board). Related: wrong-signed off bounds (`[off,-2,]`, `[opp-off,,2]`)
  are construction/parse **errors**, not empty ranges — a consumer must not
  "helpfully" flip signs before handing text to `Parse`; the sign rule is
  validated here, and `TryParse` already absorbs the rejection.
* **`==` is *not* value comparison on `FilterConfig` or `BoardPattern`.**
  Both implement `IEquatable<T>` with value semantics, but neither
  declares `==` / `!=`, so the operators keep reference semantics by the
  prevailing convention for reference types — `a == b` on two equal-by-
  content configs is `false`. Call `Equals`. (Hash-based and LINQ
  collections reach the right one through `IEquatable<T>`, so
  `HashSet`/`Contains`/`Distinct` behave by value.)
* **`FilterConfig` is mutable *and* value-equal.** That combination is
  fine for its purpose — comparing a panel's built config with the
  last-committed one — but an instance must not be mutated while it is
  in use as a key in a hash-based collection, since its hash changes
  underneath the collection. Nothing does that today; keep it that way.
* **A new facet must be added to equality too.** `Equals` /
  `GetHashCode` enumerate the members by hand — unlike the facet
  activation gates, there is no rule table to single-source them on.
  The guard is in the tests: `MemberMutators_CoverEveryPublicMember`
  reflects over `FilterConfig`'s public members and fails until the new
  one has a mutator, and `Equals_IsSensitiveToEveryMember` then fails
  until `Equals` accounts for it. Both must stay green, and neither
  should be "fixed" by relaxing the expectation.
* **`BoardPattern` equality ignores token order; `ToBracketList` does
  not.** Equality is over the constraint *set*, but the text form
  preserves construction order — so two equal patterns can render
  different bracket lists. Comparing patterns by their text
  (`a.ToBracketList() == b.ToBracketList()`) is therefore *stricter*
  than `Equals` and will report a spurious difference for a permuted
  pattern. Use `Equals`.
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
* **`DecisionFilterSet` immutability.** `Add()` currently mutates and returns
  `this`; an immutable variant (returns a new set) would eliminate the class of
  caller-mutation bugs paid down in BgQuiz_Blazor's filter-pipeline
  encapsulation arc. Touches `FilterConfig.Build()` (currently relies on Add
  returns being discardable — would silently produce empty sets under naive
  immutability) and all tests; non-trivial. Worth its own encapsulation-pass
  session.
* **`EnumLabel` reflection caching.** Per-call reflection has no cache today;
  relevant only at hot UI render paths. Move when perf work begins.
