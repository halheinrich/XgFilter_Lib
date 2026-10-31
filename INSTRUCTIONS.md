# XgFilter_Lib

> Collaboration contract: [`../AGENTS.md`](../AGENTS.md)
> Umbrella status & dependency graph: [`../INSTRUCTIONS.md`](../INSTRUCTIONS.md)
> Mission & principles: [`../VISION.md`](../VISION.md)
> Open work: the [tracker](https://github.com/halheinrich/backgammon/issues), label `XgFilter_Lib`.

## What it is for

The ecosystem's one implementation of filters on backgammon decisions. A
consumer says what it wants in a `FilterConfig`, a plain document it can bind
to a UI, save and POST; the library judges that document, materializes it as a
`DecisionFilterSet`, and evaluates the set over decisions, streamed from XG
files and JSON documents or already held as records. Beside the filters it
owns what a filter is written in: the score-token grammar (`MatchScoreToken`),
the board-pattern grammar (`BoardPattern`) and the saved-filters document
(`NamedFilterCollection`).

A source flows one way through it: `FilteredDecisionIterator` reads it through
ConvertXgToJson_Lib's `XgSourceReader`, walks its decisions with the producer,
and yields those the set passes under the ranking the caller stated. The
library chooses no ranking, persists nothing (a consumer loads and writes the
bytes), derives no fact a record states, and words no message: a verdict is
typed, and the consumer says it in its own voice.

## Which surface for which task

Each public type's XML doc, built on every build, is its contract and its
signatures; this table says where to start.

| To… | Use |
|---|---|
| state a filter, from a UI or a stored document | `FilterConfig` |
| ask before applying it, and mark what is wrong | `GetInvalidFields()`; `MatchScoreToken.GetFault` per token; `BoardPattern.Parse`'s exception for why |
| show which facets are set | `GetActiveFacets()`, labelled through `EnumLabel.ToLabel` |
| tell "changed" from "back where it started" | `FilterConfig.Equals` (see "Value equality") |
| filter a directory of files | `FilteredDecisionIterator`: `IterateXgDirectory`, `IterateXgDirectoryDiagrams`, `IterateJsonDirectory` |
| filter bytes held in memory | `IterateXgStreams`, `IterateXgStreamDiagrams`, over `XgFileStream` entries |
| filter a file already decoded | `IterateFile`, `IterateFileDiagrams` |
| filter records already held | `set.Matches(record.ViewFor(ranking))` |
| learn what a walk could not read | pass a fresh `SourceReport` |
| read the error the facets judged, for an order by error | `DecisionFilterSet.ErrorRead` |
| save filters by name | `NamedFilterCollection` |
| write a position predicate | `BoardPattern`; the token table is under "Patterns" |

The usual walk, from a filter the user filled in to the decisions it passes:

```csharp
var config = new FilterConfig
{
    Players = { "Alice" },
    MatchScores = { "3a5a", "5a3a" },      // on-roll anchored: both orientations
    ErrorMin = 0.05,
    PositionPattern = "[20,2,] [13-18,,-2]",
};
if (config.GetInvalidFields().Count > 0)
    return;                                // mark those fields; Build would throw

var filters = config.Build();
var iterator = new FilteredDecisionIterator(filters, PlayRanking.Equity, logger);
var report = new SourceReport();
foreach (BgDecisionData decision in iterator.IterateXgDirectoryDiagrams(folder, report))
    Show(decision, filters.ErrorRead(decision.ViewFor(PlayRanking.Equity)));

if (report.AllRejected)
    Tell(report.Rejected);                 // every file refused, each with its reason
```

## Stack

C# / .NET 10 / Class Library / xUnit.

## Solution

`D:\Users\Hal\Documents\Visual Studio 2026\Projects\backgammon\XgFilter_Lib\XgFilter_Lib.slnx`

From this directory:

```bash
dotnet build XgFilter_Lib.slnx
```

```bash
dotnet test XgFilter_Lib.slnx
```

Without the fixtures only one developer's machine holds:

```bash
dotnet test XgFilter_Lib.slnx --filter "Category!=RequiresFixtureFiles"
```

**What the tests read.** `TestData` is the umbrella's, shared at
`backgammon\TestData` and gitignored ([AGENTS § Tests](../AGENTS.md#tests)
owns its rules). `TestPaths` reaches it in place, five levels above the test
output directory; nothing copies it into `bin/` (halheinrich/backgammon#447),
so moving it or changing the output depth breaks every test that reads it. The
suite took about 30 seconds against the local corpus on 2026-10-30. Three
kinds of test read differently:

* Gating tests synthesize their sources through the producer's builder and
  writers (`SyntheticSources`) and need nothing. This is most of the suite. A
  test that needs only some readable source does this; one that modifies input
  stages its own files under `Path.GetTempPath()`.
* `TestData/FixtureFiles` tests carry the `RequiresFixtureFiles` trait and
  fail on an empty folder, each asserting it read something:
  `BoardPatternCorpusOracleTests`, two methods of
  `FilteredDecisionIteratorTests`, one of `FilteredDecisionIteratorFileTests`.
* `TestData/xg` tests sweep whatever it holds, carry no trait, and pass
  vacuously when it is empty: the three methods of
  `FilteredDecisionIteratorTests` on race and contact, which check invariants.

## Repo

https://github.com/halheinrich/XgFilter_Lib — branch `main`.

## Depends on

* **BgDataTypes_Lib** — `IDecisionFilterData` (the substrate filters operate
  on: a `DecisionRow`, or a `BgDecisionData`'s view built for a
  `PlayRanking`), the two decision kinds, `BoardPosition`, `PlayerResult`, the
  session kinds with their terms and standings, the header contracts
  `IMatchInfo` / `IGameInfo`, `DiceRoll`, the depth taxonomy, and the
  named-document machinery (`IJsonDocument`, `NamedCollection`). Tests build
  every record through its test-support project
  (`BgDataTypes_Lib.TestSupport`'s `TestRecords`).
* **ConvertXgToJson_Lib** — `XgSourceReader` (the one read of one source, with
  its outcome pair `XgSourceDecoded` / `XgSourceFailed` and the
  `XgSourceFormat` the caller states), `XgDecisionIterator` and its
  `XgIteratorCallbacks` / `XgIteratorOptions`, `OpeningBook`, `XgFileReader`
  (XG-format discovery) and the opaque `Models.XgFile`. Tests additionally use
  `XgFileBuilder` / `XgGameBuilder`, the producer's one public path to an
  in-memory `XgFile`, and `XgFileWriter` to synthesize fixtures; the XG record
  model behind them is internal to the producer and cannot be hand-built.

## Layout

Two projects of its own, governed by repo-root `Directory.Build.props` (TFM,
nullable, `TreatWarningsAsErrors`, XML doc generation) and
`Directory.Packages.props` (Central Package Management — no inline `Version=`
anywhere). What is repo-wide policy versus a per-project decision is settled
by the boundary comment in `Directory.Build.props` itself. `XgFilter_Lib.slnx`
also carries the `BgDataTypes_Lib`, `BgDataTypes_Lib.TestSupport` and
`ConvertXgToJson_Lib` project files, so the dependency chain builds from this
solution rather than from packages.

**`XgFilter_Lib/`** — the library. Four areas plus the top level:

- **`Enums/`** — the vocabulary consumers name. The filterable categories
  (`ContactType`, `PositionType`, `PlayType`), `DecisionTypeOption`, and two
  reflection vocabularies over `FilterConfig`: `FilterFacet` (complete, one
  member per add/skip gate) and `FilterField` (deliberately partial, one
  member per individually-blameable field). `EnumLabel` reads the
  `[Description]` labels, keeping display text owned by the library that
  defines the enum rather than by a UI layer.
- **`Filtering/`** — the intent surface and the machinery behind it.
  `FilterConfig` is the serializable DTO a consumer fills; its `Build()`
  materializes a `DecisionFilterSet` from the ten `internal` filter classes,
  which implement the `IDecisionFilter` / `IMatchFilter` seams.
  `NamedFilterCollection` is the versioned saved-filters document, with its
  closed converter. `MatchScoreToken` is the one public *grammar* here,
  standing to `MatchScoreFilter` as `BoardPattern` stands to
  `PositionPatternFilter`.
- **`Classification/`** — the `internal` predicates the category filters
  dispatch to: `IPositionClassifier` / `IPlayTypeClassifier` plus one
  hand-written implementation per enum member. Board-reading only, never
  XGID-parsing.
- **`Patterns/`** — the declarative counterpart to those named classifiers:
  `BoardPattern` over its two `IPatternConstraint` kinds, `CheckerRange` over
  `CheckerLocation` and `CheckerSpanRange` over `CheckerSpan`, plus the
  converter that carries the bracket-list text form onto the wire.
- **Top level** — `FilteredDecisionIterator`, the integration point;
  `XgFileStream`, the named stream its directory-free entries take;
  `SourceReport` with its `SourceRejection` entries, the caller-supplied
  record of what one walk attempted and rejected; and `XgFilterJsonContext`,
  the source-generated metadata of the wire surface.

**`XgFilter_Lib.Tests/`** — xUnit, mirroring the library's folders, plus
`XgFilterJsonContextTests` and `PublicCollectionsTests` at its root. Reaches
the library's `internal` surface via `InternalsVisibleTo`.
`Patterns/BoardPatternCompatibilityTests` writes the pre-span grammar out as
an independent oracle; `Patterns/BoardPatternOracleTests` and
`Integration/BoardPatternCorpusOracleTests` are the classifier oracles (see
"Classification"). Two folders have no counterpart in the library: `Helpers/`
(`BoardBuilder`, `CubeHalves`, the shared `DecisionFilterAsserts` over one
record's view and row under one ranking, `RankingSplit` for a record whose
best play differs by ranking, `SyntheticSources`, and the `FakeGameInfo` /
`FakeMatchInfo` headers; the records and the test loggers come from
`BgDataTypes_Lib.TestSupport`) and `Integration/` (end-to-end iterator runs,
synthesized or over the corpus).

## Architecture

### Substrate

All row-level filters operate on `IDecisionFilterData`, defined in
`BgDataTypes_Lib`: a `DecisionRow` (CSV-shaped) is one, and a `BgDecisionData`
(diagram-shaped) gives one through `ViewFor(ranking)`. A single filter
instance applies to either — no parallel hierarchies, no conversion at the
filter boundary. Every fact is read where the producer states it — the
decision's `Kind`, its `Session` (a `MoneySession` or a `MatchSession`,
matched exhaustively), `Board` and the after-boards as `BoardPosition`s, the
player's result — and nothing the producer derives is derived again here. A
fact that does not apply is `null` on the view, and each filter's doc says
whether such a decision passes.

### The ranking in force

Which play is best — and so a checker play's error, the best play's depth and
its after-board — is a ranking's (SPEC-scoring §2a). A view or a row is built
for one ranking and states it (`IDecisionFilterData.Ranking`), so:

* **A filter holds no ranking.** It reads the ranking of the data it is
  handed: "erred by more than x" under a ranking is the error facet's filter
  (`PlayerErrorFilter`) over views, or rows, built for that ranking.
* **`FilterConfig` holds no ranking.** A saved filter is the user's selection;
  the ranking is the application's one setting (§2a), so the persisted form
  does not carry it and did not change with it.
* **The caller states it where records are filtered.** Where this library
  builds views and rows itself — `FilteredDecisionIterator`, its walks and its
  filtering operation alike — the ranking is a required constructor argument
  beside the filters, and the only one: the producer's opening book, the other
  producer configuration a caller supplies, is taken as the book alone, so no
  caller can state a second ranking. A caller filtering records it holds
  builds the views itself. An application without a ranking setting passes
  `PlayRanking.Equity`, the default; the argument is required rather than
  defaulted, as the producer's `ViewFor` and `DecisionRow.From` are, so an
  application with the setting cannot silently fall back to the default.

### Enums

`ContactType`, `DecisionTypeOption`, `PlayType` and `PositionType` cross a
wire, and each carries BgDataTypes_Lib's
`StrictJsonStringEnumConverter<TEnum>` by type-level attribute. That
converter's doc owns the rule: tokens are the declared names, numeric ordinals
are refused, name matching on read is case-insensitive. This library's own
copy was retired (halheinrich/backgammon#424, 2026-10-19: common code moves to
one shared owner).

* `PositionType` — structural board patterns, **not** mutually exclusive: a
  position may satisfy several at once, and the filter takes the union.
* `ContactType` — these two **partition** every position. Contact-vs-race was
  extracted out of `PositionType` to make it an orthogonal axis at the type
  level, so it composes with a structural selection by AND across the set,
  never by OR within one filter.
* `PlayType` — one member per `IPlayTypeClassifier` implementation; it grows
  as play-shape classifiers land.
* `FilterFacet` — the facet vocabulary of `FilterConfig`, one member per
  add/skip gate, declared in `Build()`'s add order. `Players` and `ErrorRange`
  are two facets materializing as one filter, added at `Players`' place. The
  depth facet's three per-mode pairs are one facet. The UI-shelved
  `PositionTypes` / `PlayTypes` stay in the vocabulary because an old saved
  config can carry them; each member retires with its facet. Labels match the
  FilterPanel's visible section headings, so an "N hidden filters active"
  signal names the sections the user finds on expanding.
* `FilterField` — the companion vocabulary and the currency of
  `GetInvalidFields()`. Deliberately **partial**: a member exists iff some
  `FieldRules` row can name it, so a facet with no rule to state is absent
  rather than permanently valid. Read it as the blameable set, never as an
  inventory of the config's members. Fields rather than facets because a
  consumer marks inputs: a range facet contributes one member per bound, so a
  misordered pair can blame both ends.
* `MatchScoreTokenFault` — two faults, not one per parse rule, because the
  only distinction that changes what a consumer *says* is retired-vocabulary
  versus wrong-shape: a retired token gets its replacements named, everything
  else gets retyped.
* **Labels.** Every member of every enum here carries a UI-facing
  `[Description]`, read through `EnumLabel.ToLabel`, except `FilterField` and
  `MatchScoreTokenFault`: their members name a consumer's own input and a
  verdict the consumer words itself (the reasoning is on each enum). `ToLabel`
  throws on an undeclared value and on a member without a label, so a missed
  annotation surfaces loudly rather than degrading to the raw identifier. It
  reflects on every call, with no cache, which matters only on a hot UI render
  path.

### Filtering

The ten concrete filter classes and `IMatchFilter` are `internal`:
`FilterConfig.Build()` is the intent surface, so consumers state *what* to
filter and the library materializes the set. Only `IDecisionFilter` stays
public, as the `DecisionFilterSet.Add` seam.

* `IDecisionFilter` — `Matches`, and the two advance hints,
  `ShouldAdvanceGame` and `ShouldAdvanceMatch`, which default to `false`.
  **The hint contract**, stated on the interface (halheinrich/backgammon#373,
  Hal's ruling of 2026-10-08): an advance hint receives any just-yielded
  decision, whether or not it matches, and a true vote guarantees that no
  later decision in the game or match can pass that filter. It says nothing
  about the decision voted on: `MoveNumberFilter`'s vote comes from the first
  decision past the maximum, which it rejects. What the contract buys is
  **Early exit only saves work** (under Iteration).
* `IMatchFilter` — the skip hints, `ShouldSkipMatch` and `ShouldSkipGame`,
  with the same guarantee from a header: a true vote means no decision in the
  match or game can pass the filter; a false one promises nothing.
* `DecisionFilterSet` — filters ANDed in order. `IsEmpty` is the SSOT for "no
  filters active": consumers consult it rather than re-inspecting whatever
  config produced the set. One member's skip or advance vote is sound for the
  set, since the set is an AND. Duplicates of a concrete filter type compose
  with AND (two `MoveNumberFilter`s intersect their ranges), except the filter
  for the name and error facets: a set holds at most one, and `Add` refuses a
  second, since it states which error the set reads. `ErrorRead(data)` is that
  reading: the pair filter's qualifying error or, in a set without one, the
  doubler's error. `Add` mutates the set and returns it, and `Build()`
  discards the return, so an `Add` changed to return a new set would silently
  leave `Build` with an empty one.

#### FilterConfig

The DTO a consumer fills directly: serializable, default-constructible and
mutable, with no string-list parsing on the consumer side.

**Empty means inactive.** `Build()` adds a filter only for an active facet. An
empty list, a null-or-blank `PositionPattern`, `DecisionType = Both` and a
range with neither bound set each mean "no filter of this kind", never "reject
everything". `Players` and the error bounds add one `PlayerErrorFilter` when
either is active. The depth facet spans three member pairs (see **Depth facet
semantics**).

**One table for activity.** The per-facet gates live in a single private
`FacetRules` table, one rule per filter in add order, each owning its facets'
gates and one filter factory. `Build()` and `GetActiveFacets()` both iterate
it, so materialization and the activity query share one predicate per facet
and cannot drift. `GetActiveFacets()` judges **presence, not validity**:
garbage is still an active filter to surface, and the query never throws. It
is the surface behind the FilterPanel's "N hidden filters active" signal: the
panel consults the config's own verdict rather than judging activity from its
edit buffers, the same consult-the-result ruling as
`DecisionFilterSet.IsEmpty`.

**One table for validity.** The private `FieldRules` table feeds
`GetInvalidFields()`, the fields whose values `Build()` would throw on. Each
row only *routes* to the facet that owns the semantic, and the table is sparse
by design: the facets a user fills in by hand have rows (the match-score
tokens, halheinrich/backgammon#121; the error bounds,
halheinrich/backgammon#39 and halheinrich/backgammon#374; the move-number
bounds, halheinrich/backgammon#119; the position-pattern text,
halheinrich/backgammon#269), and a checkbox list has no rule to state. So an
empty result does not promise that `Build()` succeeds: an undefined enum value
in a checkbox list still throws there. An undefined `DecisionType` goes
further: `GetInvalidFields()` does not name it and `Build()` constructs its
filter, which throws on its first `Matches`. Only a cast can produce one,
since the wire refuses it.

A range facet contributes **two** rows for one rule pair, because a consumer
marks inputs rather than facets. Each row composes its own bound's
admissibility with the pair's ordering rule, and the composition keeps blame
honest: a bound wrong on its own value is named alone, while a misordered pair
of admissible bounds blames both ends and leaves the user to choose. A bound
at fault for its own value drags the pair out of order as a side effect, and
that must not red the field the user got right.

Validity is deliberately **not** a gate on assignment. Setters accept
anything, so a saved document written before a rule existed still loads and
the offending value can be shown back to the user rather than lost to a failed
restore. It is orthogonal to activity: a retired score token reports its facet
active while being named here.

**The verdict carries no message.** The lib rules on the input and the
consumer says so in its own voice. Where a consumer needs more than "this
field is wrong" — which entry, and whether it is retired vocabulary rather
than a typo — it asks `MatchScoreToken.GetFault` per token and reads
`RetiredMoneyReplacements`; facts and values cross the API, never prose. Both
surfaces read the same rule, so they cannot disagree.

**The position pattern is stored as text** (halheinrich/backgammon#269, option
(b)). `PositionPattern` is the bracket list the user wrote, a `string?`, and
the grammar is asked only when the text is judged (`GetInvalidFields`, through
`BoardPattern.TryParse`) or built (`Build`, through `BoardPattern.Parse`,
whose exceptions surface unchanged). That is the shape every other hand-filled
facet has, and it extends the posture above to patterns: a stored pattern a
newer grammar rule refuses (halheinrich/backgammon#268's `[0,1,]`) loads with
every other member intact, is reported invalid and active at apply, and is
written back exactly. The alternative considered, a value type beside
`BoardPattern` carrying the text and a derived parse, was rejected as a second
mechanism for a posture the table already provides. Two guarantees hold by
construction: a `BoardPattern` is never built from text the grammar refuses,
and a config holding such text cannot become an executable filter that drops
the constraint (`Build` throws before any filter is added). The text is
**never canonicalized**: the spelling is the user's and the pattern is the
meaning, the `DMP` rule for score tokens.

**Canonical JSON.** The trio (`ToJson`, `FromJson`, `TryFromJson`) is
BgDataTypes_Lib's `IJsonDocument<TSelf>`, whose contract states the restore
policy, over the source-generated metadata, which carries **no converters and
no policy at all**: every member that needs one carries it on its own type. So
an enum value rides as its declared name (`DecisionType`'s `"CubeOnly"`), an
enum collection as an array of those names (`["InnerBoard631", ...]`),
`DiceRolls` as a token array (`["31","66"]`), the toggles as plain booleans,
and `PositionPattern` as the text itself, no grammar consulted on the wire.

Putting the strictness on the **types** is the point: a `FilterConfig` crosses
wires this library does not own, and only a type attribute reaches those
(halheinrich/backgammon#37, where those enums had been crossing as bare
ordinals). It matters because a saved filter is durable and `AnalysisLevel`'s
declaration order is contractual and interleaved: the 2026-08-28 `Ply3Red`
insertion renumbered the ladder, so an ordinal read back today would name a
different level than when it was written (halheinrich/backgammon#164).

A member the reader does not know is ignored, so a retired facet resets to its
inactive default rather than failing the document. Old saved configs lost
their depth selection this way, accepted, with no migration shim
(`FromJson_MissingDepthMembers_RestoreToInactiveDefaults`).

**Value equality.** A config's identity is its content, so two configs
describing the same filtering are equal whoever built them. This is the
surface behind the FilterPanel's Apply gate (halheinrich/backgammon#49): the
panel compares its built config with the last-committed one rather than
re-materializing or serializing anything; a `ToJson()` comparison was
explicitly rejected. `Equals(FilterConfig)` states the per-member rules. The
list facets compare as multisets, since selection order is no part of a
config's meaning, and `PositionPattern` compares by meaning where there is
one: two texts that parse compare as `BoardPattern`s, two that do not compare
ordinally, and one of each never match. Two edges err toward *reporting* a
difference: duplicate entries make `{A,A} ≠ {A}` though both build the same
filter, and comparison is of intent, not of the materialized set. A third goes
the other way: two equal configs may serialize different pattern text.

#### NamedFilterCollection

The versioned **saved-filters document**: an immutable collection of named
`FilterConfig` entries, the pick list a consumer offers when the user saves
and reloads configurations, persisted as `xg-filters.json`. The library does
no I/O: consumers load bytes, restore, apply the withers (`With`, `Without`)
and write back.

It is a `NamedCollection<FilterConfig, NamedFilterCollection>` specialization
and owns only its identity. Everything structural is BgDataTypes_Lib's and
documented there: the name rule, the canonical order, the snapshot contract,
the strict-envelope/tolerant-payload split, the `IJsonDocument<TSelf>` trio
and reference equality. What lives here is the private constructor, `Create`,
`CanonicalTypeInfo` pointing at `XgFilterJsonContext`, and the two wire names
on the closed converter (halheinrich/backgammon#190 leg (B)).

The payload is what makes the document worth having. `FilterConfig` is
deliberately mutable (UI state binds to it), and its own trio is both the
base's snapshot mechanism and its entry-body seam, so its tolerance of absent
members is what keeps a retired facet from bricking a user's saved collection.
Because a stored config is restored rather than re-entered, this is also where
`GetInvalidFields()` earns its posture: an entry holding pattern text the
grammar now refuses loads with its text intact beside every other entry, and
reports invalid at apply. What the envelope still refuses is corruption (a
pattern member of the wrong JSON type, an unknown enum name), never evolution.

#### The filters

| Filter | Reads | Fails on | Early exit |
|---|---|---|---|
| `PlayerErrorFilter` | the selected halves' names and results | see below | `ShouldSkipMatch`, while names are listed |
| `DecisionTypeFilter` | `Kind` | | none |
| `MatchScoreFilter` | `Session` | | `ShouldSkipMatch`, `ShouldSkipGame`, `ShouldAdvanceMatch` |
| `MoveNumberFilter` | `MoveNumber`, `IsStandardStart` | a standalone position; a non-standard start | `ShouldSkipGame`, `ShouldAdvanceGame` |
| `ContactTypeFilter` | `Board` | | none |
| `PositionTypeFilter` | `Board` | | none |
| `PositionPatternFilter` | `Board` | | none |
| `PlayTypeFilter` | `Board`, `AfterBestBoard`, `AfterPlayerBoard` | a cube decision; a play off the candidate list | none |
| `AnalysisDepthFilter` | `AnalysisMode`, `AnalysisLevel` | `Unknown` mode, whenever active | none |
| `DiceRollFilter` | `Dice` | a cube decision | none |

The category and dice filters are OR within their lists, with an empty list
admitting nothing; `Build()` keeps that state out of the set by skipping the
add. `ContactTypeFilter`, `PositionTypeFilter` and `PlayTypeFilter` dispatch
through a private registry, the single source of truth for the
enum-to-classifier correspondence, and reject an undefined value at
construction, not at first dispatch. `PositionPatternFilter` is the
data-driven counterpart to `PositionTypeFilter`: an arbitrary sparse
constraint set, so a caller can express a structural shape without a dedicated
`PositionType`. Depth and dice are `Matches`-only because neither is knowable
from a header nor monotonic within a game, so there is no sound early exit.

**Depth facet semantics.** User-facing selection is three per-mode pairs, a
toggle plus its own level list (`IncludeEvaluations` + `EvaluationLevels`,
`IncludeRollouts` + `RolloutLevels`, `IncludeBookRollouts` +
`BookRolloutLevels`), raw intent the config stores verbatim. `Build()` is the
**single source of truth** for deriving the filter's clause union:

* **Facet inactive** iff all three toggles are off. A level list whose toggle
  is off is **inert**: it neither activates the facet nor constrains anything,
  and is never validated.
* Otherwise **one clause per enabled toggle**, carrying that mode's level list
  verbatim (empty = *any level*). A row passes iff any clause admits it, so a
  level selection qualifies **only its own mode**. A further toggle, or a
  further level on a list that holds some, only widens the match; the first
  level checked on an empty list narrows it from any level to that one.
* Canonical example (the beta report's, inexpressible under the old shared
  level set): Rollouts on with no levels + Evaluations at XG Roller++ passes
  rollout rows at *any* inner level and evaluation rows only at Roller++.
* A book hit with an `Unknown` level (`AnalysisMode.BookRollout` says when)
  passes only via the Book-rollouts toggle with no book-rollout level checked,
  or with `Unknown` itself listed. `Unknown` mode is never selectable: no
  clause can name it.

**`PlayerErrorFilter`** is the name facet and the error facet, **one filter
for the pair** (SPEC-filtering §8, halheinrich/backgammon#440, which owns the
rule; the type's remarks state it for this code). A cube record holds two
decisions, the doubler's and, where a double was offered, the taker's; a
checker play holds the player's one. Two filters ANDed cannot say "some listed
player's half has an error in range", so one filter, built from the listed
names and the bounds, says which half each facet reads:

* With no names, the doubler's half, which for a checker play is the player's.
  With names, each half whose maker is listed, compared case-insensitively. A
  record with no recorded name selects nothing.
* A selected half **qualifies** with no bounds set, or with an error
  (`PlayerResult.TryGetError`) within `[min, max]`. `Matches` is true when
  some half qualifies, and `QualifyingError`, the number
  `DecisionFilterSet.ErrorRead` hands the by-error order, is the largest error
  among the qualifying halves.

So the name facet admits a listed taker's take or pass and never the
opponent's no-double, and an error range judges the listed player's half, not
the unlisted doubler's. A further name on a list that holds some cannot remove
a decision (a take error of 0.2 beside a doubling error of 0, under 0.1,
passes for the taker alone and for both players); the first name can, since
with no bounds an empty list admits every player's decision and one name
excludes the others. Where one listed half qualifies and the other does not,
the reading is the qualifying half's, even if the other's error is larger. A
double offered and never answered passes the name facet for the listed taker
and never satisfies a range through that half. Every other facet still reads a
cube record in the doubler's frame, whichever half admitted it.
`ShouldSkipMatch` applies **only while names are listed**: every half's maker
is one of the header's two players.

The bound rule lives here, enforced by the constructor: a bound must be a
finite number of zero or greater, a null end stays open, and `min ≤ max`
(halheinrich/backgammon#39, halheinrich/backgammon#374). That is a policy
about bounds, not a claim that every error is non-negative;
`IsBoundFiniteNonNegative` says why.

**`MatchScoreFilter`.** Score tokens are **on-roll anchored**: `MaNa` means
the player on roll needs M and the opponent N, so including both orientations
admits a score regardless of who is on roll. Money and match are the session's
kind (halheinrich/backgammon#273): every gate reads the kind the producer
states through its exhaustive `Match`, so no match fact is read off money, no
money fact off a match, and no stand-in (a length of 0, away scores of 0) is
consulted.

Each gate is the exact projection of `Matches` onto the information it has,
and the type's comments derive each one:

* `ShouldSkipMatch` reads the header's terms: money by the Jacoby rule, and a
  match's length against tuples no game of that length can carry, in either
  orientation.
* `ShouldSkipGame` reads a standing anchored to the file's players, so it
  admits a tuple in **either orientation** and leaves the verdict to
  `Matches`. A game header's standing states no Jacoby rule, so a money game
  is admissible iff either money token is listed.
* `ShouldAdvanceMatch` votes only when no tuple matches the current game in
  either orientation (the producer's cut is immediate, and would drop the
  game's remaining rows) and none is reachable in a later game. Wired beside
  the filter's own `ShouldSkipGame`, as the iterator wires them, it never
  votes true: a decision's session is built from its game's standing
  (ConvertXgToJson_Lib `MatchContext.SessionFor`), so a game it could cut from
  was skipped at its header. It is sound standing alone, which is all the
  contract asks: inert, not wrong (halheinrich/backgammon#396).
* `ShouldAdvanceGame` stays at the default on purpose: a matching decision can
  be followed by mirror-orientation decisions in the same game, so there is no
  sound game-level cut.

The two money tokens are separate targets (halheinrich/backgammon#121).
Wanting money under either rule means **listing both**, exactly as admitting a
score regardless of who is on roll means listing both orientations. Every
money session states its rule (`MoneyTerms.IsJacoby`), so there is no unknown
rule to place.

**`MatchScoreToken`** is **the score-token grammar**: it states once, for the
whole library, what a `MatchScores` entry may say. `MatchScoreFilter` parses
through it, the field-rule table judges through it, and a consumer asks it
directly. Spellings are exported constants, so a consumer rendering one in
help text or a placeholder reads the constant rather than repeating a literal.
`GetFault` and the internal `ParseScore` route through one private `Inspect`,
so the answer a consumer asks for and the answer `Build()` enforces cannot
drift. `DMP` is an **alias of `1a1a`** (halheinrich/backgammon#259), resolved
inside `Inspect`, so nothing past the parse can tell the two apart and a
stored `DMP` round-trips unchanged: the spelling is the user's, the tuple is
the meaning.

**The `money` retirement** (halheinrich/backgammon#121). `money` is no longer
a valid token: it is named by `GetInvalidFields()`, rejected by `Build()`, and
carries the `Retired` fault so a consumer can tell it from a typo and offer
`RetiredMoneyReplacements`. The two alternatives were both rejected as *silent
semantic changes*: reinterpreting it as "either rule" would quietly widen a
saved filter's meaning, and letting it match nothing would quietly narrow it.
Either way the user gets a result they cannot explain from what they typed. It
survives as a constant because a filter saved before the split may hold it,
and it is reported rather than reinterpreted.

**`MoveNumberFilter`** gates on `IsStandardStart` because a non-standard start
(custom problem positions, Bg960) has no canonical numbering. A standalone
position (an `.xgp` decision) belongs to no game, so its move number and
standard start are `null`, never a stamped value (halheinrich/backgammon#124).
`ShouldAdvanceGame` votes on the first decision past the maximum, since move
numbers never decrease within a game. Bounds are constrained, not merely
stored (halheinrich/backgammon#119): a lower bound below 1 only restates the
open end, an upper bound below 1 admits nothing, and `min > max` is empty by
construction. None is a filter a user could mean, so each is a construction
error rather than a filter that silently matches nothing.

### Serialization

`XgFilterJsonContext` is the source-generated `JsonSerializerContext` for this
library's whole wire surface: compile-time `System.Text.Json` metadata instead
of runtime reflection (halheinrich/backgammon#129 leg 4). The invariant:
**source generation changes the mechanism, never the bytes.**
`XgFilterJsonContextTests` pins byte identity between the reflection resolver,
this context alone, and both orders of a chained consumer. The context's own
remarks give the full account; the decisions a reader needs:

* **The wire surface.** Two documents own the trio, `FilterConfig` and
  `NamedFilterCollection`. Five more types are wire units by the other mark, a
  bundled type-level `[JsonConverter]` defining their own token:
  `BoardPattern` and the four enums. All seven are declared roots, and the
  suite derives that list from the assembly rather than trusting it. The
  `IJsonDocument<TSelf>` trim rule binds it here: an implementer resolves its
  metadata from a source-generated context, never from a reflection-bound
  `JsonSerializer` overload.
* **Public, deliberately.** XgFilter_Razor's `SavedFiltersStore`, and BgQuiz
  behind it, round-trips through the trio and owns no `JsonSerializerOptions`,
  so that consumer alone would have admitted an `internal` context.
  ExtractFromXgToCsv would not: its `ProcessRequest` carries a `public
  FilterConfig Filters`, the client POSTs it with `PostAsJsonAsync` and the
  server binds it `[FromBody]` under a bare `AddControllers()`, so
  `FilterConfig` is named to two serializers this library does not own. Public
  is what lets a consumer in that position chain the context. None does today:
  a search of every member's `.cs` and `.razor` on 2026-10-30 found no chain,
  so those two serializers run on reflection and the type attributes.
* **Every bundled converter must be public.** Not for this context, since the
  generator runs in this assembly, but for a consumer's, whose generator emits
  `new TheConverter()` into its own assembly. With an internal converter it
  reports SYSLIB1220 then SYSLIB1030 and drops the type, recorded on the leg's
  commit (`08aec48`, 2026-08-31, net10.0 / SDK 10.0.400) and not re-run since.
  Both are warnings, so a consumer's build need not fail, which is why
  `EveryBundledConverter_IsPubliclyConstructible` holds the rule here.
* **Chains nothing, and shadows `BgDataTypes_Lib` on purpose.** A
  `FilterConfig` embeds three `IList<AnalysisLevel>` facets and an
  `IList<DiceRoll>`, so the generator's walk emits its own copy of their
  metadata here. This context resolves the entire closure alone
  (`ThisReposContextAlone_ResolvesTheFullClosure`), so the shipped entry
  points need no resolver chain. The cost is that a consumer chaining
  most-derived-first gets *this* copy;
  `TheContext_ShadowsBgDataTypes_ButIdentically` pins the two copies
  byte-identical rather than assuming it.
* **`GenerationMode = Metadata`,** per the arc's binding rule: a default-mode
  fast-path handler binds nested resolution to the declaring context's private
  options and bypasses the resolver chain. BgDataTypes_Lib's chained-consumer
  test pair owns that rule.
* **Trim posture.** The library csproj sets `IsTrimmable` and
  `EnableTrimAnalyzer`, so with repo-wide `TreatWarningsAsErrors` a
  reflection-serialization regression is a **build error here** rather than a
  publish-time warning downstream. The commit that set them records a mutation
  check, not re-run since, in which two of eight mutations failed the build
  with IL2026 (`08aec48`).

### Classification

Everything in `Classification/` is `internal`: consumers pass enum values to
the filters, which dispatch here. `IPositionClassifier` takes the board in the
frame the producer states on `IDecisionFilterData.Board`;
`IPlayTypeClassifier` takes the view's `Board`, `AfterBestBoard` and
`AfterPlayerBoard`, the after-boards in the next mover's frame (reached
through `BoardPosition.Flipped`, never a rule of this library's), and is only
asked about a play whose after-boards both exist.

| Classifier | Backs | True when |
|---|---|---|
| `RaceClassifier` | `ContactType.Race` | the two sides' checkers have passed each other |
| `ContactClassifier` | `ContactType.Contact` | not a race |
| `InnerBoard631Classifier` | `PositionType.InnerBoard631` | in contact, points 6, 3, 1 made and 5, 4, 2 not |
| `InnerBoard54321Classifier` | `PositionType.InnerBoard54321` | in contact, points 5 to 1 made and 6 not |
| `VsTwoPlusUpClassifier` | `PositionType.VsTwoPlusUp` | the opponent has two or more on the bar (`board[0] <= -2`) |
| `Holding1386Vs20Classifier` | `PositionType.Holding1386Vs20` | the full predicate is on its XML doc |
| `Make20PtClassifier` | `PlayType.Make20Pt` | the decision-maker's 20-point is not already made and exactly one of the two plays makes it |

Race and Contact partition positions: that is a property of the pair, *not* a
contract on `IPositionClassifier`, and the `PositionType` classifiers overlap
freely. `Make20PtClassifier` reads the decision-maker's 20-point as index 20
in the prior board and, in the flipped after-boards, as index 5 with their
checkers negative.

The classifiers are the oracle for their pattern equivalents.
`BoardPatternOracleTests` holds the bracket list of each `PositionType` and
asserts agreement on a sample of boards; `BoardPatternCorpusOracleTests`
repeats it over `TestData/FixtureFiles` and requires each named filter to
admit at least one decision there, so it cannot pass vacuously. A pattern
cannot express the race guard, so the two inner-board patterns agree only on
contact boards, or composed with `ContactType.Contact`.

### Patterns

The data-driven position matching behind `PositionPatternFilter` and
`FilterConfig.PositionPattern`. Where the named `PositionType` classifiers are
hand-written predicates, a `BoardPattern` is a *declarative* predicate a
caller or the FilterPanel can author at runtime. **Parsing lives in this
library**, not the UI.

**The text form** is the bracket list: whitespace-separated `[head,min,max]`
tokens, an empty bound meaning unbounded. The head is a board index in the
frame of `IDecisionFilterData.Board` (0 the opponent's bar, 1–24 the points,
25 the on-roll player's bar), a named borne-off location, or a span `a-b` of
board indices. One sign rule runs through the grammar: positive counts are the
on-roll player's, negative the opponent's.

| Token | Means |
|---|---|
| `[5,2,]` | the on-roll player has two or more on the 5-point |
| `[5,,-2]` | the opponent has two or more there |
| `[6,0,3]` | up to three of the player's on the 6-point, and none of the opponent's |
| `[0,,-2]` | the opponent has two or more on the bar |
| `[off,1,]` | the on-roll player has one or more off |
| `[opp-off,,-2]` | the opponent has two or more off |
| `[7-12,3,]` | the on-roll player has three or more across 7 to 12 |
| `[13-18,,-2]` | the opponent has two or more across 13 to 18 |
| `[7-12,0,0]` | the span holds neither side's checkers |
| `[7-12,,0]` | the span holds none of the on-roll player's; the opponent's are ignored |
| `[7-12,0,]` | the span holds none of the opponent's; the player's are ignored |

Names parse case-insensitively and render lower-case. `Parse` throws and
`TryParse` absorbs (the doc on `Parse` says which exception means what);
`ToBracketList` writes the form, and the two round-trip. An empty pattern
matches every board, and blank text parses to it.

* **The types.** `CheckerLocation` is the single source of truth for location
  vocabulary: which places exist, the grammar's names, each place's value
  interval and how its value is read. `CheckerSpan` is a contiguous run of at
  least two board indices: a single index is a `CheckerLocation`, so each
  constraint has one spelling. `IPatternConstraint` is **closed**, its members
  internal, so its only kinds are `CheckerRange` (a signed count at one
  location) and `CheckerSpanRange` (one side's total across a span); a
  consumer needing the detail pattern-matches on those. `BoardPattern` is the
  immutable, validated set of them, ANDed.
* **The intervals have one owner.** The internal `CheckerSides` owns each
  side's value interval and which board entries are whose. A location's
  interval is the hull of the sides that can sit there, so a bound outside it
  (`[0,1,]`, `[off,-2,]`) is a construction error rather than a constraint
  that silently never matches. Nothing here spells `0` / `±N` as a side's
  limit, and nothing restates N, which is `BoardPosition.CheckersPerSide`.
* **A value is never invalid once it exists.** Each type validates at
  construction, and each default is a valid value: `default(CheckerLocation)`
  is `Board(0)`, and `CheckerSpan` stores its last index as a distance so that
  its default is the span `0-1`, making `default(CheckerSpanRange)` the
  unconstraining `[0-1,,]`.
* **One cross-element invariant.** No two constraints on the same place, keyed
  on `Place` value-equality, so the check spans numeric locations, named
  locations and spans. Overlap is not duplication: `[7,1,] [7-12,3,]
  [5-8,,-2]` is three conditions.
* **A span's bounds apply to every side whose interval holds them all**, which
  is what gives the zero forms their meaning, and the other side's checkers
  are ignored, never netted. A positive and a negative bound together are
  refused. A bar contributes nothing to the side that cannot sit on it, so
  either bar may sit in a span with either sign. The constrained sides are
  derived from the bounds when read, not stored.
* **Equality** is over the constraint set, in any order; the hash is
  order-independent and each element compares by its own record-struct
  equality, so there is no second encoding of "same constraint". The text form
  preserves construction order.
* **Serialization.** The type names `BoardPatternJsonConverter` by attribute,
  the only wiring point: it writes the bracket-list string and reads it
  through `Parse`, so deserialization stays on the validated path and a bad
  pattern fails as a `JsonException`. It is `public` because a consumer's
  source generator must construct it (see "Serialization"); nothing registers
  it on any options, and `BoardPatternWireSafetyTests` round-trips a bare
  pattern through options that hold no converter. A `FilterConfig` stores its
  pattern as text, so the converter serves only a bare `BoardPattern`. No
  in-tree consumer names one to a serializer (searched, every member's `.cs`
  and `.razor`, 2026-10-30); the type stays a declared wire unit.

### Iteration

`FilteredDecisionIterator` is the integration point. Its configuration is the
constructor's: the filter set, the ranking (see **The ranking in force**), a
logger and an optional opening book. The source or sources are the per-call
argument. Two output shapes are exposed: `DecisionRow`, CSV-flat and built for
the ranking, and `BgDecisionData`, the full candidate list and after-boards,
depending on no ranking and filtered through its view for it. The JSON
directory walk offers rows only; a caller wanting JSON documents as records
reads them as `.json` stream entries.

**The filtering operation** (halheinrich/backgammon#373, Hal's ruling of
2026-10-08: "a new producer-owned filtering operation over an already-parsed
source owns the callback wiring and filter evaluation; the existing directory
and stream entry points use it"). `IterateFile(XgFile file, string
sourceName)` and `IterateFileDiagrams` own everything filtering a parsed
source takes: the producer call under the iterator's options, the filter set's
four early-exit hints wired once, and `DecisionFilterSet.Matches` on each row
or each record's view. A caller holding a decoded file writes none of it.
Arguments are checked at the call; the rest is lazy, and a failure while
iterating propagates. `sourceName` is every decision's identity, as in the
walks: the file name with its extension.

* *Where it lives, and why.* On `FilteredDecisionIterator`, because it needs
  exactly the iterator's configuration, and the iterator is already the one
  object that states it. A separate type would have to take the same four
  arguments, or take an iterator; either way the configuration would be stated
  twice or the type would be the iterator by another name.
* *The name and the shape.* The siblings' pattern, `Iterate` + the source +
  `Diagrams` for the record shape, with the source here a parsed `XgFile`. It
  takes `(XgFile, string)` rather than the converter's `XgSourceDecoded`
  because a decoded outcome's `SourceName` is the path as read, where the
  producer needs a file name, and because a file parsed some other way is
  filtered the same.
* *The opening book* is an optional constructor argument, the book alone,
  never producer options: `XgIteratorOptions` carries a ranking too, and a
  caller able to pass one could state a second ranking that disagrees with the
  iterator's. Optional rather than required because "no book" is a real
  configuration, not a fallback that masks a missing setting:
  ExtractFromXgToCsv's `LocalFolderProcessor` passes a book, and BgQuiz passes
  none.
* *Two strategies, one rule set.* The operation is the streaming strategy,
  which ExtractFromXgToCsv uses: it filters as it iterates and exits early
  where the filters allow. A consumer that retains unfiltered decisions and
  refilters them when the filter changes, as BgQuiz does (`ProblemLoader`
  loads with an empty set, `Selection` applies `Matches` to each view), runs
  the same rules through a different execution. The two agree because early
  exit never changes what passes.

**The walks.** Every directory and stream entry funnels through one generic
private core, `IterateSources<T>`: read each source, then filter each decoded
one through the operation above. So the read, the skip and log, and early exit
are identical across every shape and every source. Directory walks discover
through the producer's `XgFileReader.EnumerateXgFormatFiles`, or the `.json`
pattern, and pass `Path.GetFileName`; stream walks take a caller-supplied
`IEnumerable<XgFileStream>` for callers that hold the bytes rather than a
server path, such as a Blazor WASM client parsing browser-picked files that
never leave the browser. Both are WASM-compatible by dependency, every library
involved being plain `net10.0`; the directory methods simply rely on
`System.IO.Directory`, which a browser consumer does not call.

**The read is the converter's** (`XgSourceReader`, Hal's ruling of 2026-10-08
on halheinrich/backgammon#373): the one read of one source, under one failure
policy, returning `XgSourceDecoded` or `XgSourceFailed`. The walk keeps no
read and no catch of its own. A decoded source is iterated; a failed one is
recorded and logged, and the walk continues with the next.

**The stream entries' format is the name's extension, mapped once**
(`FormatOf`, at the stream-entry boundary). A name the converter's
`XgFileReader.IsXgFormatFile` recognizes is read as XG's binary file, and
`.json` as the library's JSON document, both case-insensitively, so one stream
walk may mix them. The converter infers nothing from a name (its ruling); this
boundary is where the name becomes a stated format, and nowhere else. The
XG-format extensions are the converter's; the JSON one is stated once here,
`JsonExtension`.

**A usage error is not a failed source.** A malformed file or stream content
is a failed outcome: logged, skipped, recorded. A misused *stream entry*
throws `ArgumentException` in two places, each owned once: `RequireValid`,
when the walk reaches the entry, for what the iterator owns (a null or blank
name, an extension naming no format it reads, a null `Data`), and
`XgSourceReader.ReadStream`, when the attempt starts, for what the converter
owns (a stream it cannot decode in place). Neither is ever recorded as a
skipped file. `sourceName` **must carry its extension**: the producer's
`DecisionId` stamping derives the `.xg`/`.xgp`/`.json` discrimination from it
and throws on an extension-less name.

**Logging.** A skipped source logs `LogWarning(failed.Exception, "Skipping
{File}", sourceName)`, so the exception is captured on the entry, not
stringified. The constructor's logger is also forwarded into the producer, so
its per-decision warnings, notably an illegal-play skip, surface through this
same pipeline rather than being swallowed there.

**Rejected sources, as data** (halheinrich/backgammon#368). Every walk entry
takes an optional `SourceReport`, which a caller reads during or after the
enumeration: how many sources were attempted, how many read, which were
rejected and why, whether the walk reached the end, and the one conclusion the
report draws itself, `AllRejected`, true only for a *completed* walk that
attempted at least one source and read none. So an empty selection, a
selection nobody finished reading, and readable files that yielded no matching
decision are each told apart from one whose every file was refused. A caller
that passes none gets the same behaviour. The warning stays as a trace of the
same event and carries the same exception, so the report is the one source of
the fact.

* *Where it lives, and why.* A caller-supplied state object, the shape the
  producer's `XgIteratorState` already has: the iterator is its only writer
  and everyone else reads. State on the iterator was rejected because one
  iterator may serve many enumerations
  (`OneIteratorAcrossEnumerations_EachReportStandsAlone`), and a completed
  report must stay unchanged beside the parse it describes (BgQuiz's
  `LoadedProblems` holds one). A returned result type was rejected because
  every consumer `foreach`es an `IEnumerable<T>`, and an enumerable that can
  be walked twice has no one report to return. The one-walk rule is
  mechanical, not advisory: a report is claimed by the first `MoveNext` of the
  enumeration it was handed to and refuses every later claim, so nothing can
  clear, append to or replace a record once made.
* *What is recorded, and when.* Each count is recorded as its event happens,
  and none is derived from another (halheinrich/backgammon#373, Hal's ruling
  of 2026-10-08): an attempt as the read starts; a readable source when the
  read decodes, before any decision is filtered, so a source whose decisions
  all fail still counts; a rejection on the failed outcome; completion after
  the last source. In a completed walk the attempted sources are exactly the
  readable and the rejected ones. An attempt that ends in neither outcome, a
  cancellation or a stream the read refused as a usage error, is attempted but
  neither readable nor rejected, and the walk is incomplete.
* *What is a rejection.* Exactly the converter's failed outcome. A source that
  read is readable whatever the filters make of its decisions. A cancellation,
  a usage error, a failure enumerating the caller's sources, and a producer,
  filter or consumer failure all propagate, record no rejection, and leave the
  report incomplete; its counts then describe the sources reached and claim
  nothing about the rest.

**Early exit.** At construction the iterator builds one `XgIteratorCallbacks`
record threading the filter set's four predicates to the producer, the one
place they are wired:

```
SkipMatchAt    ← DecisionFilterSet.ShouldSkipMatch     (IMatchInfo)
SkipGameAt     ← DecisionFilterSet.ShouldSkipGame      (IGameInfo)
StopGameAfter  ← DecisionFilterSet.ShouldAdvanceGame   (IDecisionFilterData)
StopMatchAfter ← DecisionFilterSet.ShouldAdvanceMatch  (IDecisionFilterData)
```

The producer evaluates each at its declared boundary and short-circuits its
own iteration, and it sees each decision through a view for the same ranking
the iterator filters under. The consumer has no imperative skip on the
producer's surface: the producer owns iteration control, and the consumer owns
the predicates, supplied once.

**Early exit only saves work** (halheinrich/backgammon#373, Hal's ruling of
2026-10-08). For any filter set whose filters honour the hint contract, and
any source, the filtering operation yields exactly the decisions
`DecisionFilterSet.Matches` accepts from what the producer yields for that
source with no callbacks, in the same order. Each vote guarantees no later
decision in its scope passes its filter, and the set is an AND, so a cut drops
only decisions the set would reject. That is what lets a consumer refilter
retained decisions with `Matches` and agree with one that streams. It holds
for `.xgp` sources too because the producer runs its stop callbacks only on
decisions it yields to its caller, after the single-decision selection
(halheinrich/backgammon#392). `EarlyExitEquivalenceTests` enforces the
guarantee; its doc comment says what it compares and over which sources.

## Pitfalls

### Filters and facets

* **Read the board from `data.Board`, never the XGID.** It is already a
  `BoardPosition` in the player on roll's frame; parsing the XGID would
  re-derive it and risk perspective bugs.
* **A filter never chooses a ranking.** It reads the one the view or row was
  built for. Building a view inside a filter, or giving `FilterConfig` a
  ranking, would let one run judge "best" and "error" two ways (see **The
  ranking in force**).
* **The name and error facets are one filter; never split them.** Two filters
  ANDed either drop a listed taker's error or agree with "some listed half in
  range" only by a coincidence a reader must prove.
  `DecisionFilterSet.ErrorRead` reads from the same rule, so the facets and
  the by-error order cannot disagree.
* **An active error range drops results with no error.** A decision with no
  move recorded, a move the view's ranking does not score, and a double never
  answered have no error (`PlayerResult.TryGetError` is false) and fail an
  active range through that half. Reading such a result as 0 silently changes
  what "erred by more than x" means under depth first.
* **A missing after-board is never substituted.** `AfterPlayerBoard` is `null`
  exactly when the player's play is off the candidate list
  (halheinrich/backgammon#15), and both after-boards are `null` on a cube
  decision. Reading another board in its place classifies a play that was not
  made; `OffListPlay_NoPlayerAfterBoard_ReturnsFalse` is the pin.
* **Facet activation gates live only in `FacetRules`.** An ad-hoc `if` in
  `Build()`, a parallel rule in the query or a rule in a consumer re-creates
  the two-encodings hazard the table exists to kill. A new facet means a new
  `FilterFacet` member plus a new table row.
* **A new facet must be added to equality too.** `Equals` / `GetHashCode`
  enumerate the members by hand; there is no table to single-source them on.
  The guard is in the tests: `MemberMutators_CoverEveryPublicMember` fails
  until the new member has a mutator, and `Equals_IsSensitiveToEveryMember`
  then fails until `Equals` accounts for it. Neither should be "fixed" by
  relaxing the expectation.
* **A validity rule has one home.** It belongs on the filter or grammar that
  owns the semantic (`PlayerErrorFilter.IsBoundFiniteNonNegative`,
  `MoveNumberFilter.IsBoundAtLeastOne`, `MatchScoreToken.GetFault`); the
  `FieldRules` row only routes to it. An inline predicate lets
  `GetInvalidFields()` and `Build()` drift, and the swept agreement tests in
  `FilterConfigTests` exist to catch that. The gates rely on `ParseScore`'s
  invariants in the same way, so a parse shortcut lets an impossible target
  through unchecked.
* **Depth levels qualify only their own mode.** Rollout-family lists select on
  the rollout's **inner** level, a different quantity from an evaluation's
  level. `AnalysisMode`'s remarks say which levels each mode carries: as that
  library states it, Roller-family levels appear on cube rollouts and not on
  checker rollouts, with no sample recorded here and no enforcement in the
  producer. So copying one mode's levels onto another's clause, or offering
  them as one list, recreates the match-nothing defect the clause union was
  introduced to fix.
* **The depth clause union is derived in `Build()`, not the panel.**
  XgFilter_Razor binds the six raw inputs and lets `Build()` derive the
  clauses. Pre-computing clauses in the UI duplicates the SSOT and will
  silently drift, losing the inactive rule or the inert-levels rule.
* **The depth facet drops `Unknown`-mode rows whenever it is active.** They
  pass only when the facet is inactive and the filter absent from the set: the
  same drop-don't-pass posture an active error range applies to a result with
  no error. Separately, checking any concrete book-rollout level excludes an
  unenriched book hit's `Unknown` level: intended, not a bug.
* **Match-score tokens are on-roll anchored; game headers are
  player-anchored.** `4a5a` and `5a4a` are different targets, but
  `MatchStanding.Away1/Away2` are anchored to the file's players, and both
  players roll within a game. A game-level or coarser gate must admit a tuple
  in **either orientation** and leave the verdict to `Matches`: comparing the
  header orientation alone silently eats every decision whose on-roll player
  is the file's player 2 (the shipped `4a5a` / `Move1_0001.xgp` bug, pinned in
  `MatchScoreFilterIntegrationTests`). The same trap applies mid-stream:
  `ShouldAdvanceMatch`'s cut is immediate, so it must treat the *current*
  game's score as still matchable.
* **A hint is judged on every decision yielded, and must be sound for each.**
  Don't gate the advance hints on `Matches`: the vote that ends a game is
  often cast on a decision the filter rejects, and gating it loses the cut.
  Never vote true where a later decision could still pass: the cut drops it
  silently, and the operation then disagrees with `Matches`. A filter that can
  exit early must override the default `false`, and missing an override is
  equally silent, so a new hint gets a case in `EarlyExitEquivalenceTests`.

### Equality and collections

* **`==` is *not* value comparison on `FilterConfig` or `BoardPattern`.** Both
  implement `IEquatable<T>` and neither declares `==`, so `a == b` on two
  equal-by-content instances is `false`. Call `Equals`; `HashSet`, `Contains`
  and `Distinct` reach it through `IEquatable<T>`. A `FilterConfig` is also
  mutable, so it must not be mutated while it is a key in a hash-based
  collection.
* **Don't compare patterns by their text.** `ToBracketList` preserves
  construction order, so two equal patterns can render different lists.
  `a.ToBracketList() == b.ToBracketList()` is stricter than `Equals` and
  reports a spurious difference for a permuted pattern.
* **No public member hands out a live mutable collection or array** (the
  collection rider, Hal 2026-09-26 on halheinrich/backgammon#273). A list
  behind `IReadOnlyList<T>` that is really the type's own `List<T>` or array
  can be cast back and written, silently changing the object that handed it
  out. Hand out an `ImmutableArray<T>`, boxed once, as
  `BoardPattern.Constraints` does; `PublicCollectionsTests` holds each site
  and sweeps the exported surface for public members declared as arrays.
  `FilterConfig`'s `IList<T>` members are mutable by design (the DTO a UI
  binds to), and `GetActiveFacets` / `GetInvalidFields` build a fresh set per
  call.

### Patterns

* **Borne-off counts are the position's, never stored or re-derived here.**
  The `[off,…]` / `[opp-off,…]` locations read
  `BoardPosition.OnRollBorneOffCount` / `OpponentBorneOffCount`
  (halheinrich/backgammon#295); `CheckerLocation.ValueOn` only reads and signs
  them. Don't plumb off-count data into `IDecisionFilterData`, grow the board
  past its 26 slots, or count checkers here to support them.
* **A wrong-signed bound on a one-sided place is an error, not an empty
  range.** Since halheinrich/backgammon#268, `[0,…]` (the opponent's bar)
  refuses a positive bound and `[25,…]` (the on-roll player's bar) a negative
  one, like `[off,-2,]` and `[opp-off,,2]`. A consumer must not "helpfully"
  flip signs before handing text to `Parse`, and the rule lives in
  `CheckerLocation.Sides`: don't re-encode it in a parser or a UI. A
  **stored** pattern holding one loads as an invalid field, and `Build`
  throws; the `…StoredBarRuleToken…` tests pin it. Spans are exempt.
* **Never drop refused pattern text on the way into a config.** A consumer
  that pre-judges the text and stores `null` when the grammar refuses it turns
  an invalid filter into an inactive one, the silent drop
  halheinrich/backgammon#269 ruled out. Store the text as written and let
  `GetInvalidFields` name it. For the same reason, don't canonicalize it on
  set or on write: the round trip must return it exactly.
* **A span counts one side; a point sees both.** `[6-7,0,3]` admits an
  opponent's blot on the 6-point, while `[6,0,3]`, bounding the signed count
  on one point, rejects it. "Fixing" either to match the other breaks a pinned
  contract: the span's by the opposing-checkers invariance and P/O sweeps in
  `CheckerSpanRangeTests`, the point's by the legacy-oracle sweep in
  `BoardPatternCompatibilityTests`.

### Serialization

* **Don't register a converter on this library's seam.** An options-level
  converter outranks a type attribute (BgDataTypes_Lib's Pitfalls entry, and
  `OptionsLevelRegistration_OutranksTypeAttribute_SoConsumersMustTightenToo`,
  which pins it), so a registration here would govern every read and mask the
  removal of any attribute. The same precedence cuts both ways: a consumer
  that registers a loose converter on its own options lowers the floor for
  itself.
* **`BoardPattern`'s JSON converter must stay declared on the type.** The
  immutable type has no settable property for its constructor parameter, so
  the default reflection serializer cannot reconstruct it, and no options
  object anywhere registers the converter. A `FilterConfig` no longer depends
  on it, so removing the attribute would not show up there:
  `BoardPattern_PlainOptions_DoesNotThrowAndRoundTrips` is the pin.
* **`NamedFilterCollection`'s parts are a closed pattern upstream.**
  BgDataTypes_Lib's Pitfalls entry on the pattern says what breaks if a part
  is skipped; do not re-derive it here. Two consequences are this repo's own.
  The name rule, the canonical sort, the snapshot round-trip and the envelope
  are not reimplementable here: a local copy would be a second definition of
  what the base single-sources. And **the two wire names are a file
  contract**, fixed by every `xg-filters.json` on a user's disk and pinned
  byte-for-byte by `NamedFilterCollectionSerializationTests`: changing either
  is a file migration. The base's `Get` / `TryGet` are the only get pair, by
  ruling (2026-09-09, halheinrich/backgammon#190).

### Iteration

* **A stream is read during enumeration, not at the call.** The stream entries
  are `yield`-based, so the caller must keep each `XgFileStream.Data` open,
  unread and at position 0 until enumeration reaches it, and it must be
  seekable, since XG's decoder seeks to the payload its header locates. One
  that is not is refused as a usage error, which ends the walk; it is never a
  skipped file. The boring-safe pattern is to buffer the bytes up front and
  hand each walk a fresh stream over them, as the tests do and as BgQuiz does
  through `PickedFile.OpenRead`. The iterator does not dispose streams.
* **`FileName`'s extension is how the stream is decoded.** A name must not be
  "fixed up" (say, to `.xg`) to get an entry past the check.
* **A `SourceReport` serves one walk; make a new one per enumeration.**
  Passing it to a second call, or walking the same enumerable twice, throws
  `InvalidOperationException`. The claim is lazy, so the failure comes at the
  second walk's first step, not at the call. A consumer that retains a
  completed report must not hand that instance to the next walk.
* **Only a failed read is a rejection, and the walk catches nothing.**
  `AllRejected`'s meaning depends on `Rejected` holding exactly the sources
  whose read failed. Catching a cancellation turns a cancelled run into a
  completed one, and recording a usage error, a source-enumeration failure or
  a filter failure as a rejection turns a bug into a "file could not be read"
  notice. The report's incomplete state is what tells a consumer such a walk
  never finished. Don't simplify `ReadableCount` to attempted minus rejected:
  an attempt that ends in a cancellation or a usage error is neither.
