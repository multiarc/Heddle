# Heddle generator review series — findings register

**What this is.** A durable, per-finding record of what the review cycles over `src/Heddle.Generator`
(and the engine code those cycles reached into) found, fixed, deferred or retracted. It is assembled
from two sources that disagree in places:

- `docs/generator_plan/phase-8-docs-sweep.md` — the per-cycle prose record, written after each cycle.
  Believed over a commit message wherever the two conflict: several commit messages were later
  measured wrong and corrected there, and a future reader may hit the uncorrected message first.
- the commit messages of the review series, `cd3a665 .. f990a9c` on this branch.

Test names and their doc comments (`src/Heddle.Tests`, `src/Heddle.Generator.Tests`,
`src/Heddle.Generator.IntegrationTests`, `src/Heddle.LanguageServices.Tests`) and
`src/TestCorpus/CorpusIntent.cs` were read to fill the **pinned by** field.

**How to use it.** Read it at the *start* of a review cycle, in this order:

1. **Defect classes**, near the end. Four cycles in a row found one member of a class, fixed it, and
   the next cycle found another member. That section says which classes were enumerated exhaustively
   and which were not. Start in a class with unenumerated members.
2. **Known-open register**, last. Do not re-report these; each is deferred with a stated reason.
3. The individual entries, to decide whether a new finding is a duplicate. Every entry carries a
   **regression check** — the shortest thing you can run to confirm it is still fixed. That field is
   the point of the document: nobody has re-run the repro of a finding fixed ten cycles ago.

> This register is documentation. It may cite code, tests and commits. **Code and tests must never
> cite this register** — no finding ids in comments or test names. The repo already carries the rule
> that code may not cite documents (F-072 is the cycle that swept it); this document does not create
> an exception to it.

**Reading the fields.**

- **severity** uses the series' own scale: `1` silent wrong output · `2` generated code breaks the
  consumer's build · `3` an error on one tier where the other renders · `4` silent loss of the
  precompiled tier for a working template · `5` leaks · `6` tests that cannot fail. Some findings are
  documentation or process defects and sit off that scale; those say so instead of borrowing a number.
- **found** names the cycle. Cycles 1–3 and 8–19 have prose sections in the record. **Cycles 4–7 have
  no prose section at all** and are reconstructed from commit messages alone, which is why entries
  there often say "not recorded" for role and repro detail.
- **regression check** assumes the repo root. `-f net8.0` keeps runs short; note the declared `net6.0`
  container does not start on the development box (F-081) and a run that silently skips it still
  exits 0.
- "not recorded" means the source does not say it. It is never a reconstruction.

**Cycle → commit map** — the fastest route to a finding's primary evidence.

| Cycle | Prose section in the record | Landing commit(s) |
| --- | --- | --- |
| 1 | Independent review (2026-07-27) | `cd3a665`, `7fd1999`, `66c5a60` (record only) |
| 2 | Second independent review (2026-07-27) | `01922e5`, `e818ac1`, `7997562`, `d2beb20`, `570cf84`, `ecaca87`, `50832db` |
| 3 | Third independent review (2026-07-27) | `2576bc2` |
| 4 | *(none — commit message only)* | `1b64d10` |
| 5 | *(none)* | `25b2554` |
| 6 | *(none)* | `0f9ba46`, `cb4c426` (adversary) |
| 6→7 interval | *(none)* | `13c7172`, `8a30aed`, `8010e2a`, `be7b4c1`, `1f3ef70`, `6b2b876`, `49d17e9`, `4ad3283`, `66b86df` |
| 7 | *(none)* | `e46d0b6`, `d034dda` (verifier) |
| closing round | Closing round (2026-07-29) | `0db6222` |
| 8 | Eighth review cycle | `726031b`; `f872b79` closes the three it left open |
| 9 | Ninth review cycle | `31e618d` |
| 10 | Tenth review cycle | `4680907` |
| 11 | Eleventh review cycle | `4f299ce` |
| 12 | Twelfth review cycle | `6d013eb` |
| 13 | Thirteenth review cycle | `27dd357` |
| 14 | Fourteenth review cycle | `e14e862` |
| 15 | Fifteenth review cycle | `c1b20dd` |
| 16 | Sixteenth review cycle | `2664141` |
| 17 | Seventeenth review cycle | `669eef8` |
| 18 | Eighteenth review cycle | `2d3fab6` |
| 19 | Nineteenth review cycle | `f990a9c` |

**168 findings catalogued:** 147 FIXED, 11 KNOWN-OPEN, 5 SUPERSEDED, 5 NOT-A-DEFECT.

## Contents, by status

### FIXED (147)

| id | severity | title | note |
| --- | --- | --- | --- |
| F-001 | 1 | Unsynchronised publication of a shared lookup table under a legal concurrent host call |  |
| F-002 | 3 | An assembly is marked scanned before the work it gates succeeds |  |
| F-003 | 3 | `GetTypes()` called unguarded on host assemblies |  |
| F-004 | 3 | `Assembly.Location` used as the test for "the host loaded this" |  |
| F-005 | 3 | Load order decided what resolved |  |
| F-006 | off-scale | A published diagnostic row lost its description to unescaped table pipes |  |
| F-007 | off-scale | A shipped sample called a method on a property that defaults to null |  |
| F-008 | off-scale | A document rewritten after a module initializer landed still asserted none exists |  |
| F-009 | off-scale | A diagnostic was documented by two of its three causes |  |
| F-010 | 6 | A corpus row promised a byte assertion that nothing makes |  |
| F-011 | 6 | A gate's "claimed id" predicate was unanchored to the rows it claims to read |  |
| F-012 | 6 | A gate matched diagnostic ids by substring |  |
| F-013 | 6 | The link gate skipped the majority of the links it claimed to check |  |
| F-014 | 6 | The public-API gate could not see a documented call with arguments |  |
| F-015 | off-scale | A checkable completeness claim stated over an incomplete list |  |
| F-016 | 6 | Test names claiming more than the test checks |  |
| F-017 | off-scale | A green report read stale bytes because the capture path is project-relative |  |
| F-018 | off-scale | A work item reported met on both halves of its own done-when, and was not |  |
| F-019 | 3 | The C#-tier metadata half of the single-file fix was never fixed |  |
| F-020 | 3 | A count-based change gate cannot tell "nothing happened" from "one left and one arrived" |  |
| F-021 | 6 | "The engine loads nothing" was pinned for scanning only | (with a stated residual — see notes) |
| F-022 | 2 | A hex formatter dropped the leading zero of every byte below 0x10 |  |
| F-023 | 3 | A cache stored the value and dropped the diagnostics |  |
| F-024 | 2 | The generated tier refused arithmetic the engine renders |  |
| F-025 | 3 | An import cycle killed the process |  |
| F-026 | 3 | The parse walk had no depth bound | (bound now a fixed count of 250) |
| F-028 | off-scale | A generator diagnostic reported at `Location.None` |  |
| F-029 | 1 | An equality comparer whose hash disagreed with its equality |  |
| F-030 | 6 | The cached-failure path had no test at all |  |
| F-031 | 6 | A byte-parity gate over a hand-maintained subset |  |
| F-032 | 3 | A depth bound placed after the early return it was meant to protect |  |
| F-033 | 1 | Replaying a cached diagnostic replayed its position |  |
| F-034 | 3 | A failure cache with no invalidation outlived the registration that would heal it |  |
| F-035 | 2 | The constant fold lost the operand type by widening every integer to `long` |  |
| F-036 | 6 | A test class that could not fail because every case degraded anyway |  |
| F-037 | 3 | Import depth measured per document, and cycle identity taken from the raw spelling |  |
| F-038 | 3 | A six-character template threw out of the compile |  |
| F-039 | off-scale | A reference built twice on every cache miss |  |
| F-040 | 2 | The fold read the wrong field when converting an `int` operand to `uint` |  |
| F-041 | 6 | The fold's test class had no case that could see the defect |  |
| F-042 | 3 | A fixed depth bound set above the stack it has to fit inside | (superseded by the value change in F-051) |
| F-043 | 3 | A per-parse budget stored on a reusable public settings object |  |
| F-044 | 6 | The import-cycle normalisation had no real pin |  |
| F-045 | off-scale | The language reference contradicted itself about nesting limits |  |
| F-046 | 6 | The depth limit was pinned by nothing over a range of 125–1499 |  |
| F-047 | 2 | Unary minus on a `uint` left undecided |  |
| F-048 | 2 | A refusal scoped to a whole operand kind rather than the illegal pairs |  |
| F-049 | 4 | A fold rule that modelled text the emitter never writes | (rule deleted) |
| F-050 | 6 | An assertion set to exactly what the fixture produces with the guard deleted |  |
| F-051 | 3 | A bound measured in Debug and shipped in Release |  |
| F-052 | 6 | A pin that does not redden when the property breaks — it takes the host down | (test deleted, with a note saying what would verify it) |
| F-053 | 6 | A guard added to fix a defect that could never fire | (deleted) |
| F-054 | off-scale | Comments describing rules that had already been deleted or moved |  |
| F-055 | off-scale | "Member paths cost nothing" — a comment that hid an exponential | (the comment; the defect it hid is F-056) |
| F-056 | 1 | A null-safe hop spelled its receiver twice, in every tier |  |
| F-057 | 3 | Import fan-out was unbounded |  |
| F-058 | 6 | Both tests for the fan-out bound read the constant they were checking |  |
| F-059 | 2 | "No opinion" treated as "no problem" for five operators and the conditional |  |
| F-060 | 3 | A last-resort handler with no id and a message built from an empty name |  |
| F-061 | 3 | An unreadable `@<<` import threw out of the parse |  |
| F-062 | 3 | A guard written for the disk reader's exception set, over a public seam |  |
| F-063 | 3 | Two components disagreed about what makes two imports the same document |  |
| F-064 | 5 | A cache entry holding a `Type` from a collectible context, evicted only on failure |  |
| F-065 | 3 | Per-parse import state on a shared, public settings object |  |
| F-066 | off-scale | A diagnostic documented for one of its two producers |  |
| F-067 | 6 | The fix for an unfalsifiable assertion was itself unfalsifiable |  |
| F-068 | off-scale | Code comments citing documents |  |
| F-069 | 2 | `?.` emitted onto a type that has no nullable form |  |
| F-070 | 3 | Thread-static state broke re-entrancy through a public callback |  |
| F-071 | 4/3 | A fan-out bound that charges a shared document once per path that reaches it |  |
| F-072 | 1 | `?.` short-circuits the rest of the chain; the engine defaults one hop and keeps walking |  |
| F-073 | 2 | A ref-struct hop behind a reference hop did not compile |  |
| F-075 | 6 | A diagnostic's message unpinned at both of its unrelated producers |  |
| F-076 | 6 | A coverage column that let an entry exempt itself |  |
| F-077 | 6 | A differential harness whose answer depended on test scheduling |  |
| F-078 | 6 | Arithmetic rows for types no template literal can produce | (deleted) |
| F-082 | 1 | A ref-struct hop off a reference receiver read that receiver twice |  |
| F-083 | 2 | A path ending *on* a ref struct emitted `CS0030` |  |
| F-084 | 1 | Generated arithmetic inherited the consumer's overflow-checking setting |  |
| F-085 | 3 | A failed render permanently poisoned a compiled template |  |
| F-086 | 5 | A cache whose key can never repeat |  |
| F-087 | 3 | An assembly that lost a name collision was retired for good |  |
| F-088 | 3 | A cached *success* was never invalidated |  |
| F-089 | 6 | A branch whose gate is statically decided | (deleted) |
| F-090 | 6 | Five properties claimed by a commit message and pinned by nothing | (three of five; two deferred, then closed in cycle 8's follow-up) |
| F-091 | 3 | An internal member on a referenced model type errored where the engine renders |  |
| F-092 | 2 | Internal *types* are imported from metadata regardless of accessibility |  |
| F-094 | 6 | A harness call that suppressed the property it should have tested |  |
| F-095 | 1 | The overflow-context fix covered one of two expression paths |  |
| F-096 | 2 | A visibility check that only detects one of the two reference kinds |  |
| F-097 | 2 | An accessibility gate scoped to one of four positions that spell a type |  |
| F-098 | 2 | A ref-struct check that guarded hops but not the model |  |
| F-099 | 3 | A probe that reverted to the original error on an ambiguous name |  |
| F-100 | 6 | Six properties that nothing pinned, two of them one day old | (four); two are unpinnable and recorded as such |
| F-102 | 1 | A memo keyed on a display string |  |
| F-103 | 6 | A guard whose only test the same commit ate |  |
| F-104 | 2 | `[Obsolete(…, error: true)]` on a model type or member breaks the consumer's build |  |
| F-105 | 2 | A static class as `@model()` |  |
| F-106 | 3 | A slot-value type check the engine performs and the emitter did not |  |
| F-107 | 6 | Two invalidation calls inside the branch where the case they handle cannot occur | (removed) |
| F-108 | 6 | Test suites that wrote 58 probe assemblies and deleted none |  |
| F-110 | 2 | Type nameability answered by enumerating kinds instead of asking one question |  |
| F-111 | 2 | An `@model` text that resolves to no symbol was emitted verbatim | then re-opened and fixed again in cycle 13 (F-127) |
| F-112 | 4 | A degrade is a cost, and a blanket refusal charges it to working templates |  |
| F-113 | 1 | A body cache keyed without the value that decides the body |  |
| F-116 | 6 | Literal slot values skipped, and a boxing arm with no test |  |
| F-117 | 3 | A rule mirrored from an assumption about the engine rather than from the engine |  |
| F-118 | 6 | Two tests that passed for the wrong reason |  |
| F-119 | 2 | "Complete" was not complete: the verdicts recursed into elements but not type arguments |  |
| F-120 | 3 | A grammar prelude the runtime never had |  |
| F-121 | 3 | The null literal has a type on one tier and not the other |  |
| F-122 | off-scale | An author-facing warning raised for a fault no author can fix |  |
| F-123 | 2 | A dotted `@model` spelling whose last segment happens to name a real type |  |
| F-124 | 1 | `:: dynamic` on a definition does not mean "untyped body" | (for the call forms in the probed matrix; completed by F-130) |
| F-125 | 2 | A walk deleted as a duplicate, that nothing else performed | (restored) |
| F-126 | 6 | A completeness suite that pinned the answer but not the arm |  |
| F-127 | 2 | The gate reported and the emission did not listen |  |
| F-129 | 6 | A boundary rule pinned by nothing, and a per-keystroke full-closure walk |  |
| F-130 | 1 | "The emitter cannot say" read as "the engine has no type" |  |
| F-131 | 1 | An `@list` body nested in a definition lost the definition's prop layout |  |
| F-132 | 6 | A table row asserting a degrade and an empty error list |  |
| F-133 | 6 | Two verdict arms no compilation can reach |  |
| F-134 | 6 | A gate's global-namespace stop, claimed covered and covered by nothing |  |
| F-135 | 3 | `@out(this)` inside an `@list` body inside a slot definition was an unchecked precompile |  |
| F-136 | 2 | Prop-first resolution missing from the expression writer |  |
| F-137 | 3 | Prop-first resolution missing from the routine that types a computed call-site value |  |
| F-138 | 1 | `@list` over something that is not a list |  |
| F-139 | 6 | Nine table rows that were one assertion wearing nine hats |  |
| F-141 | 1 | Caller content compiled under the callee's model, with no layout and no slot mode |  |
| F-142 | 1 | A prop argument checked against the model member while emitted as the prop |  |
| F-143 | 4 | Every native expression refused on the dynamic tier before the layout was consulted |  |
| F-144 | 1 | What a chain hands the next link is text, not the producer's value |  |
| F-146 | 6 | A verdict row that could not fail on the arm it named |  |
| F-148 | 1 | A kind funnel cannot say what a name says |  |
| F-149 | 1 | `[DataType]` is a rule, not a list of extensions |  |
| F-150 | 3 | Caller content under a `:: dynamic` callee was still untyped |  |
| F-151 | 6 | A sweep category that could not hold the defect the cycle found | (instrument added) |
| F-153 | 6 | A failed layout left truncated in the cache |  |
| F-155 | 4 | Reflection's assignability claimed, not reproduced |  |
| F-156 | 1 | `:: object` is the engine's predicate; `dynamic` is a spelling |  |
| F-157 | 2 | An `[Obsolete(…, error: true)]` exported function breaks the consumer's build |  |
| F-159 | 6 | A third sweep category: newly precompiling | (instrument added) |
| F-160 | 6 | Two more tests that could not fail |  |
| F-161 | 1 | A region body given its host's props but not its host's model |  |
| F-162 | 1 | `Nullable<TEnum>` reached `System.Enum` through a boxing correction |  |
| F-163 | 4 | Array covariance over element types the CLR reduces to one |  |
| F-164 | 1 | A declared `:: T` was never checked against what the call site passed |  |

### KNOWN-OPEN — do not re-report (11)

| id | severity | title | note |
| --- | --- | --- | --- |
| F-027 | 3 | A run of several thousand prefix operators exhausts ANTLR's own lookahead |  |
| F-079 | 3 | `Path.Combine` rejects characters on .NET Framework that .NET Core accepts |  |
| F-080 | 3 | On `netstandard2.0` an assembly with no file yields no metadata reference | (by design; there is no API to fix it with) |
| F-081 | 6 | A declared target framework runs zero tests and the run still exits 0 |  |
| F-101 | 2 | A hop whose property *type* is internal still emits a name the consumer cannot compile |  |
| F-140 | 6 | Two verdict rows that cannot be honestly pinned | (recorded rather than pretended) |
| F-147 | 4 | A native expression reading the element's own member inside an `@list` body degrades |  |
| F-154 | 4 | Three caller-content shapes degrade under a `:: dynamic` callee |  |
| F-166 | 3 | A `bool`/`bool?` bitwise operand pair has no diagnostic of its own | (partially closed) |
| F-167 | 3 | `floor(3)` / `ceil(3)` / `round`-on-`int` are compile errors |  |
| F-168 | off-scale | A shipped sample still uses removed MSBuild item metadata |  |

### SUPERSEDED — the fix or guard was later replaced, removed or reversed (5)

| id | severity | title | note |
| --- | --- | --- | --- |
| F-074 | 3 | A cache drop that ran before the removal it was draining | (the epoch this added was removed in the next cycle as redundant — F-093) |
| F-093 | 6 | The epoch was a second counter for something one counter already knew | (removed) |
| F-115 | — | An unmeasured micro-optimisation, reverted | (reverted) |
| F-145 | 6 | A type-name gate added to a layout on suspicion, with no demonstrated red | (removed in cycle 18 — F-158) |
| F-158 | 4 | The extension prop-layout guard, reversed | (removed) — closes F-145 and F-153 |

### NOT-A-DEFECT — measured and dismissed; do not re-report (5)

| id | severity | title | note |
| --- | --- | --- | --- |
| F-109 | — | `ThreadLocal` per definition, reported as an unbounded slot table |  |
| F-114 | — | A reported repro that does not compile | (as reported) — the underlying defect is real and is covered by F-110 |
| F-128 | — | A root reference as a slot value, reported as a latent hole |  |
| F-152 | — | Two constructs examined rather than assumed | (both), with one cleanup |
| F-165 | — | An unreachable guard arm, kept and labelled | (documented decision) |


---

## Findings

### F-001 — Unsynchronised publication of a shared lookup table under a legal concurrent host call

- **status:** FIXED
- **severity:** 1 (a phantom diagnostic on a valid template — worse than a crash, per the record)
- **found:** cycle 1, adversary; reproduced 3/3 runs
- **symptom:** a compile racing `Register` failed with `Couldn't resolve type <System.DateTime>` on a
  template that had just compiled, and a valid `@if`/`@else` drew `HED3003` "branch terminal with no
  matching opener".
- **root cause:** `ReflectionHelper.Reconfigure` assigned each name map a fresh empty dictionary and
  then filled it while unlocked readers looked them up; `TemplateFactory` mutated its registry in
  place with no lock (`RegisteredNames`' lock was decoration, being the only one). Making `Register`
  the mandatory, repeatable host call turned a latent race into a reachable one.
- **fixed by:** `cd3a665` — maps built as locals and published as one immutable object in a single
  `Volatile.Write`, one snapshot per resolve; the factory registry made copy-on-write behind a
  registration lock.
- **pinned by:** `RegistrationConcurrencyTests.CompilingWhileRegisteringNeverFailsToResolveAKnownType`,
  `.CompilingABranchSetWhileRegisteringNeverReportsAMissingOpener`,
  `.ReadingRegisteredNamesWhileRegisteringNeverThrows`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~RegistrationConcurrencyTests`.
  To confirm the pin still bites, revert `ReflectionHelper` to publish-then-fill: the type-map
  scenario reddens within 28 compiles with the reported message.
- **notes:** **the two halves are not equally pinned.** The commit states the type-map half is
  demonstrated load-bearing, and that the factory-registry half "did not reproduce under a pre-fix
  mutation in 600 compiles, so its test is a guard, not a demonstration". Treat the registry half as
  unpinned.

### F-002 — An assembly is marked scanned before the work it gates succeeds

- **status:** FIXED
- **severity:** 3 (a host that caught a registration failure and retried got a silent no-op)
- **found:** cycle 1, adversary
- **symptom:** `RegisterExportedExtensions` marked an assembly scanned *before* registering it, so a
  retry after a caught failure did nothing at all.
- **root cause:** ordering inside `RegisterExportedExtensions`; the mark now follows success.
- **fixed by:** `cd3a665`.
- **pinned by:** not recorded. No test in the series is named for it.
- **regression check:** none available. Read the mark/register ordering in
  `RegisterExportedExtensions` and confirm the mark is after the successful registration.
- **notes:** fixed WITHOUT a test.

### F-003 — `GetTypes()` called unguarded on host assemblies

- **status:** FIXED
- **severity:** 3 (threw out of the host's startup call)
- **found:** cycle 1, adversary
- **symptom:** `LoadExtensions` called `GetTypes()` unguarded, so an `[ExportExtensions]` in its
  parameterless form threw `ReflectionTypeLoadException` out of the host's startup call because of
  one unresolvable type reference.
- **root cause:** the same call in `ReflectionHelper` already skipped what cannot load; this one did
  not.
- **fixed by:** `cd3a665` — skips what cannot load.
- **pinned by:** not recorded.
- **regression check:** none available; verify by reading the guard around `GetTypes()` in
  `LoadExtensions`.
- **notes:** fixed WITHOUT a test.

### F-004 — `Assembly.Location` used as the test for "the host loaded this"

- **status:** FIXED
- **severity:** 3 (in a single-file or WASM publish the engine resolved nothing at all)
- **found:** cycle 1, adversary; reproduced against a real `PublishSingleFile` host
- **symptom:** `Location` is empty for the *entire application* in a single-file or WASM publish, so
  the engine observed no assemblies; `@model System.DateTime` failed to resolve and
  `Register(yourAssembly)` — the documented migration step — did not repair it. The SDK had been
  emitting `IL3000` on that line throughout.
- **root cause:** the observation filter keyed on `Assembly.Location` instead of on what it meant to
  exclude.
- **fixed by:** `7fd1999` — the filter now excludes dynamic assemblies, collectible and custom load
  contexts, and the engine's own emitted expression assemblies (tracked explicitly, since the ALC
  check does not exist on `netstandard2.0`).
- **pinned by:** `CSharpTierMetadataTests.AnAssemblyWithNoFileBehindItStillYieldsMetadata`,
  `.NoObservedAssemblyIsDroppedFromTheReferenceSet` — added a cycle later, by `01922e5`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~CSharpTierMetadataTests`.
  Mutation: delete the `Location` guard's replacement and confirm a red.
- **notes:** **cycle 2's mutation testing found this fix entirely unpinned** — deleting the `Location`
  guard passed 1,832 tests. The pin above arrived with the fix for its unfixed half (F-019). The
  `netstandard2.0` half of the metadata story is still open — see F-080.

### F-005 — Load order decided what resolved

- **status:** FIXED
- **severity:** 3 (the same host and template resolved or failed by accident of scheduling)
- **found:** cycle 1, verifier and adversary
- **symptom:** an assembly loaded after the first resolution was permanently invisible; whether a
  type resolved depended on whether an unrelated earlier compile had happened. This falsified, in the
  same words, four documents the docs sweep had just landed.
- **root cause:** observation never rebuilt the type maps.
- **fixed by:** `7fd1999` — maps carry the stamp of the assembly set they were built from and rebuild
  when it moves; a count gate and reference-identity fast path keep it off the hot path (the naive
  version cost 27s → 49s on the suite; back to 26s).
- **pinned by:** `AssemblyRegistrationTests.AnAssemblyLoadedAfterTheFirstResolutionStillResolves`
  (reddens within 28 compiles against the pre-fix shape).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AssemblyRegistrationTests`.
- **notes:** the count gate added here *introduced* F-020. The claim "load order does not decide what
  resolves" was still false after this commit, by a different route.

### F-006 — A published diagnostic row lost its description to unescaped table pipes

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** cycle 1
- **symptom:** the `HED1005` row's entire description vanished from the rendered page.
- **root cause:** unescaped `|` inside a markdown table cell.
- **fixed by:** `7fd1999`.
- **pinned by:** not recorded — the documentation gates check id presence, not row rendering.
- **regression check:** render `docs/native-expressions.md` (or grep the `HED1005` row) and confirm
  the description survives the table cell.
- **notes:** the class — a gate that is green because it never reaches the rendered text — recurs; see
  F-011 to F-014.

### F-007 — A shipped sample called a method on a property that defaults to null

- **status:** FIXED
- **severity:** off-scale (documentation/sample)
- **found:** cycle 1
- **symptom:** the new startup-order sample called `RegisterFrom` on `TemplateOptions.Functions`,
  which defaults to `null`, so the sample threw.
- **root cause:** documentation written against an API shape that was never checked by running it.
- **fixed by:** `7fd1999`.
- **pinned by:** not recorded.
- **regression check:** none automated; the sample code in the documentation is not compiled by any
  gate. Read it.

### F-008 — A document rewritten after a module initializer landed still asserted none exists

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** cycle 1
- **symptom:** the document was rewritten one commit after a module initializer was added and
  re-asserted that none exists.
- **root cause:** prose currency; no gate reaches this class of claim.
- **fixed by:** `7fd1999`.
- **pinned by:** not recorded (the documentation-currency rule landed as a convention, not a gate).
- **regression check:** none automated.

### F-009 — A diagnostic was documented by two of its three causes

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** cycle 1
- **symptom:** `HED7104`'s published description named two of the three things that raise it.
- **fixed by:** `7fd1999`.
- **pinned by:** not recorded — the diagnostics gate checks that an id is *named* in its owning
  document, never that the description is complete. Sibling: F-066.
- **regression check:** none automated. Compare the causes in the code that raises `HED7104` with the
  published description.

### F-010 — A corpus row promised a byte assertion that nothing makes

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier
- **symptom:** a row in `src/TestCorpus/CorpusIntent.cs` stated an intent no harness enforced.
- **fixed by:** `7fd1999` (row reworded).
- **pinned by:** the completeness gates over the intent table
  (`CorpusRenderParityTests.EveryDeclaredStandaloneEntryIsInTheCorpus`,
  `.EveryDeclaredPrecompilingEntryIsInTheCorpus`) — but see F-076: those gates arrived later, and the
  render column was itself unmeasured until the closing round.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CorpusRenderParityTests`.

### F-011 — A gate's "claimed id" predicate was unanchored to the rows it claims to read

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier (predicted by the sweep plan, then confirmed)
- **symptom:** the generator-side `ClaimedIds` helper regexed `` `HED\d{4}` `` over the whole registry
  file, so a mere cross-reference mention anywhere in that document satisfied "claimed" — leaving the
  registry cross-reference hole open. The runtime-side copy was correctly section- and row-anchored.
- **fixed by:** `7fd1999`.
- **pinned by:** `PipelineDiagnosticsTests` (the generator-side gate) — see its
  registry-parsing helper.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~PipelineDiagnosticsTests`;
  mutation: add a bare `` `HED7099` `` mention outside the registry table and confirm it does not
  satisfy the gate.

### F-012 — A gate matched diagnostic ids by substring

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier
- **symptom:** `DiagnosticIdTests` matched ids by substring, so a longer id containing a shorter one
  satisfied the shorter one's row.
- **fixed by:** `7fd1999`.
- **pinned by:** `DiagnosticIdTests` itself, now id-anchored.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DiagnosticIdTests`.
- **notes:** the same class as F-011, in the other gate. The record also corrects that gate's own doc
  comment, which claimed a block filter the code does not have — it gates all 85 constants.

### F-013 — The link gate skipped the majority of the links it claimed to check

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier
- **symptom:** `DocumentationLinkTests` skipped every same-directory link — 435 of 803 — and checked
  only the first number of a line range, which is precisely the defect that prompted the gate.
- **fixed by:** `7fd1999`.
- **pinned by:** `DocumentationLinkTests`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DocumentationLinkTests`;
  mutation: introduce a broken same-directory link and a broken range end, and confirm both redden.

### F-014 — The public-API gate could not see a documented call with arguments

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier
- **symptom:** `PublicApiDocMentionTests`' extraction missed any documented call written with
  arguments, so the whole window's new API was invisible to it.
- **fixed by:** `7fd1999`.
- **pinned by:** `PublicApiDocMentionTests`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PublicApiDocMentionTests`;
  mutation: document a call with arguments against a member that does not exist and confirm a red.

### F-015 — A checkable completeness claim stated over an incomplete list

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** cycle 1, verifier
- **symptom:** `editor-support.md` named 6 of the 11 excluded options while asserting "every compile
  option that affects analysis has a key here".
- **fixed by:** `7fd1999`.
- **pinned by:** `WorkspaceOptionParityTests` (the doc-facing legs added by the sweep).
- **regression check:** `dotnet test src/Heddle.LanguageServices.Tests -f net8.0 --filter FullyQualifiedName~WorkspaceOptionParityTests`.

### F-016 — Test names claiming more than the test checks

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier
- **symptom:** two test names asserted a property the body did not check.
- **fixed by:** `7fd1999` — renamed to what they check, with the behavioural pin named alongside.
- **pinned by:** n/a (this is a naming defect).
- **regression check:** none automated. This class recurs constantly — see F-036, F-041, F-046,
  F-050, F-058, F-067, F-118, F-126, F-132, F-160.

### F-017 — A green report read stale bytes because the capture path is project-relative

- **status:** FIXED
- **severity:** off-scale (verification process) — but it made a *false* green report
- **found:** cycle 1, verifier
- **symptom:** an earlier commit reported "all suites and all ten sample goldens are green"; the
  branch was in fact red in CI. `samples/codegen-t4-successor`'s generated-source golden still held
  stripped citations.
- **root cause:** the capture path resolves against the sample's own directory, so a repo-relative
  `--capture samples/x/out` writes to `samples/x/samples/x/out` while the comparison reads
  `samples/x/out`. The check that reported green was comparing stale bytes.
- **fixed by:** `7fd1999`/`ecaca87` — golden re-ratified; the correct invocation is `--capture out`
  after deleting the directory.
- **pinned by:** nothing. This is an invocation trap, not a code path.
- **regression check:** when re-ratifying a sample golden, run the capture from inside the sample
  directory with `--capture out`, delete the output directory first, and confirm `compare-golden`
  reads the bytes you just wrote.
- **notes:** the single most repeatable way to produce a false green in this repo. Any future cycle
  reporting "all ten sample goldens clean" should state the invocation it used.

### F-018 — A work item reported met on both halves of its own done-when, and was not

- **status:** FIXED
- **severity:** off-scale (process)
- **found:** cycle 1, verifier
- **symptom:** the implementation record claimed a success criterion met while three of the cycle's
  serious findings were live; one work item was reported met on both halves of its done-when and was
  not.
- **fixed by:** `66c5a60` (record corrected), the underlying halves by `7fd1999`.
- **pinned by:** n/a.
- **regression check:** none. Recorded because the record's own "success criteria met" line is not
  evidence.


### F-019 — The C#-tier metadata half of the single-file fix was never fixed

- **status:** FIXED
- **severity:** 3 (every C#-tier expression failed where the dynamic tier rendered)
- **found:** cycle 2; measured on a real `PublishSingleFile` host
- **symptom:** 23 observed assemblies, **0** metadata references, every C#-tier template failing with
  `Predefined type 'System.Object' is not defined`. The language service hit the same gate from the
  other side, because it byte-loads model assemblies: a document whose model resolved fine for
  `@model` drew 11 errors the moment an expression used the C# tier.
- **root cause:** `RoslynReferenceProvider` still gated on `Assembly.Location` after F-004 fixed
  `AssemblyHelper`. The rule written alongside F-004 covered "type resolution **and C#-tier
  metadata**"; only the first half was implemented.
- **fixed by:** `01922e5` — reads the loaded metadata image when there is no file to read.
- **pinned by:** `CSharpTierMetadataTests.AnAssemblyWithNoFileBehindItStillYieldsMetadata`,
  `.NoObservedAssemblyIsDroppedFromTheReferenceSet`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~CSharpTierMetadataTests`.
- **notes:** the archetype of "a record says closed, one half was never done". On `netstandard2.0`
  this is still broken by design — `TryGetRawMetadata` does not exist there; see the platform
  known-open entry (F-080).

### F-020 — A count-based change gate cannot tell "nothing happened" from "one left and one arrived"

- **status:** FIXED
- **severity:** 3 (sticky: the type stayed unresolvable until an unrelated later load repaired it)
- **found:** cycle 2; demonstrated 3/3
- **symptom:** an unload followed by a load restored the count, so observation concluded nothing had
  happened and the newly loaded assembly stayed invisible — the exact shape of a collectible context
  reload, which the language service performs on every model reload.
- **root cause:** `_observedCount` counted every loaded assembly including the excluded collectibles.
  **The optimisation added by F-005's fix created it.**
- **fixed by:** `e818ac1` — an order-sensitive identity digest of the loaded set, which cannot cancel
  out; allocation-free, so the per-resolve cost that motivated the count gate is unchanged.
  Generation is also published before the stamp, so a reader between the two writes cannot conclude
  both that nothing loaded and that its maps are current.
- **pinned by:** `AssemblyRegistrationTests.AnAssemblyLoadedAfterACollectibleUnloadStillResolves`
  (unloads in its own frame, because a debug build roots every local until the method returns);
  `CSharpTierMetadataTests.NoAssemblyIsDroppedFromTheReferenceSet`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AssemblyRegistrationTests`;
  mutation: revert the digest to a count and confirm the test reddens with `Couldn't resolve type`.

### F-021 — "The engine loads nothing" was pinned for scanning only

- **status:** FIXED (with a stated residual — see notes)
- **severity:** 6
- **found:** cycle 2, by mutation
- **symptom:** restoring the deleted transitive load walk **lazily** passed all 1,832 tests. What was
  pinned was "no static constructor" and "no `DependencyModel`" — the mechanism of yesterday's
  defect, not the property claimed.
- **fixed by:** `7997562` — the pin states the load half as behaviour: an assembly referenced by a
  loaded assembly, but not itself loaded, must still be unloadable after the engine has observed,
  compiled and rendered.
- **pinned by:** `AssemblyRegistrationTests.ObservingAndRenderingLoadsNoReferencedAssembly`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~ObservingAndRenderingLoadsNoReferencedAssembly`;
  mutation: reintroduce a load on the observation pass and confirm a red.
- **notes:** **the pin cannot catch a one-shot startup walk**, which has already run before the probe
  can be built — verified, not assumed, and written in the test. Catching that shape needs a child
  process, which no suite has. Commit `519a30f`'s message claims this property reddens; it reddens
  the *scan*, not the *load*, and the message did not distinguish them. Believe the record.

### F-022 — A hex formatter dropped the leading zero of every byte below 0x10

- **status:** FIXED
- **severity:** 2 (the consumer's compiler rejected keys nobody wrote)
- **found:** cycle 2
- **symptom:** `ToHexString` used `"X"` instead of `"X2"`, corrupting every public key emitted into
  `InternalsVisibleTo` — 30 characters lost from a 320-character key — and the rejections surfaced in
  the host's error list.
- **fixed by:** `01922e5`.
- **pinned by:** `CSharpTierMetadataTests.EveryByteFormatsToTwoHexDigits`,
  `.TheEnginesOwnPublicKeyFormatsToItsFullLength`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~CSharpTierMetadataTests`.
- **notes:** before the fix, **returning a constant `"DEADBEEF"` as every public key passed all 1,832
  tests despite 100 invocations**. Compounded with F-023: the cache routed the second compile down
  the path where the corrupt keys surfaced.

### F-023 — A cache stored the value and dropped the diagnostics

- **status:** FIXED
- **severity:** 3 (the same template reported 1 error then 5, in one process)
- **found:** cycle 2
- **symptom:** the first caller to compile a broken expression received the errors; every later one
  received none, fell through to a different path, and got a different list.
- **root cause:** the preparse cache entry held the value only.
- **fixed by:** `01922e5` — diagnostics are part of the cached entry and replay to every caller.
- **pinned by:** `CSharpTierMetadataTests.TheSameFailingExpressionReportsTheSameDiagnosticsEveryTime`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~TheSameFailingExpressionReportsTheSameDiagnostics`.
- **notes:** **the replay has no demonstrated red** — once the public keys were fixed both paths
  produced identical text and positions, so removing the replay leaves the suite green. Stated in the
  test (`CSharpTierMetadataTests`, the doc comment beginning "This is a guard, not a red-verified
  pin"). The fix also *introduced* F-033 and F-034.

### F-024 — The generated tier refused arithmetic the engine renders

- **status:** FIXED
- **severity:** 2 (raw `CS0020`/`CS0220` against the `.heddle` file, manifest still claiming success)
- **found:** cycle 2
- **symptom:** `@(2147483647+1)` renders `-2147483648` on the engine and was a build error
  precompiled; `@(1/0)` throws `DivideByZeroException` on the engine and is refused by C# at compile
  time. Adding the generator to a working project turned rendering templates into build errors.
- **root cause:** the emitter wrote arithmetic operators through verbatim; the two tiers disagree
  about *when* an arithmetic fault is found.
- **fixed by:** `570cf84` — a constant fold that leaves an expression C# would refuse unwritten, so it
  degrades to the dynamic tier. Timid by construction: anything it cannot evaluate with certainty is
  treated as not constant.
- **pinned by:** `ConstantArithmeticDifferentialTests.ConstantOverflowDegradesInsteadOfBreakingTheBuild`,
  `.ConstantDivisionByZeroCompilesAndFaultsLikeTheEngine`, `.OrdinaryArithmeticStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`.
- **notes:** the fold went on to be wrong four more times — F-035, F-040, F-047, F-048, F-049, F-059.
  Its narrowness is pinned deliberately, because a fix that quietly moved arithmetic off the fast tier
  would be its own regression.

### F-025 — An import cycle killed the process

- **status:** FIXED
- **severity:** 3 (exit 134, ~27,500 frames, on a template typo)
- **found:** cycle 2
- **symptom:** an `@<<` import parsed the imported document in place with no visited set, so a
  document that imported its way back to one already being parsed recursed until the stack ran out. A
  `StackOverflowException` cannot be caught, so the process died. Self-import did the same.
- **fixed by:** `d2beb20` — imports carry the chain being parsed; one that reaches a document already
  on it reports `HED4006` and skips the repeated import.
- **pinned by:** `ImportCycleTests.ATwoDocumentImportCycleIsAnErrorNotAStackOverflow`,
  `.ADocumentImportingItselfIsAnErrorNotAStackOverflow`, `.TheCycleErrorNamesTheDocumentsInvolved`,
  `.TheSameImportOnTwoSeparateBranchesIsNotACycle` (the near neighbour that keeps the guard from being
  tightened into breaking diamond imports).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ImportCycleTests`.
- **notes:** the cycle *key* was wrong twice afterwards — F-037 and F-063.

### F-026 — The parse walk had no depth bound

- **status:** FIXED (bound now a fixed count of 250)
- **severity:** 3 (a single template file took the process down: exit 134)
- **found:** cycle 2
- **symptom:** Heddle's expression and chain builders recurse once per operator or level; deep input
  exhausted the stack, and a `StackOverflowException` cannot be caught. Flat operator runs,
  parenthesis nesting, indexer chains, prefix runs, nested conditionals and `??` chains all reached it.
- **fixed by:** four commits, and the sequence is the finding:
  - `ecaca87` — builders fail catchably, parse boundary reports `HED4007`. Half the defect: the
    parser's own recursion still terminated the process.
  - `50832db` — **reversal of instrument**: the first attempt probed remaining stack with
    `EnsureSufficientExecutionStack`, whose answer depends on thread stack size and build
    configuration, so the same template passed on one host and died on another and no fixed-depth test
    was portable. Replaced by a fixed depth count of 1000, enforced in two places (an ANTLR parse
    listener for the parser's own recursion; an iterative tree-depth check for left-associative runs,
    which ANTLR parses with a loop and so never recurses on).
  - `1b64d10` — 1000 was unreachable on a 1 MB stack (the Windows and thread-pool default); set to 300.
  - `0f9ba46` — 300 was measured in Debug; a Release build dies at 284 while the guard fired at 293.
    Set to 250, below the Release figure. See F-051.
- **pinned by:** `DeepNestingTests.TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`,
  `.APrefixRunPastTheLimitIsReportedOnASmallStack`, `.AFlatRunPastTheLimitIsReported`;
  `DeepNestingGeneratorTests.ADeeplyNestedTemplateFailsTheBuildInsteadOfTheCompiler`,
  `.AnOrdinarilyDeepTemplateStillPrecompiles`, `.ADeepTemplateThatAlsoHasASyntaxErrorIsStillBounded`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DeepNestingTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DeepNestingGeneratorTests`.
  Mutation: change the limit constant and confirm the value assertion reddens.
- **notes:** the generator gets the bound by source-linking the parser, asserted at the generator's own
  entry point — it matters more there, because an unbounded build-time parse takes down the compiler
  or the IDE rather than failing a build. Three separate can't-fail tests attached to this one
  behaviour: F-046, F-050, F-052. Residual: F-027.

### F-027 — A run of several thousand prefix operators exhausts ANTLR's own lookahead

- **status:** KNOWN-OPEN
- **severity:** 3 (process death, not a catchable error)
- **found:** cycle 2, restated in cycles 5 and 6
- **symptom:** 3000 prefix operators survive, 5000 does not. The overflow is inside
  `ParserATNSimulator` with the rule depth still in single figures, at 1 MB and at 4 MB, in Debug and
  Release — so **no fixed count can see it** and neither a listener nor the grammar can reach it.
- **root cause:** the ATN simulator's own recursion, upstream (antlr/antlr4#744).
- **fixed by:** — (documented in the language reference rather than left to be found).
- **pinned by:** nothing, and nothing can pin it here.
- **regression check:** none. The published numbers are marked indicative rather than contractual
  because the threshold moves with stack size.
- **notes:** **this was recorded, then deleted as "disproved", then re-established.** Cycle 2 recorded
  it; a later commit replaced it with a claim that the crash is the parser's own descent and the bound
  is therefore reached first; `0f9ba46` measured both mechanisms and found both real. A related
  correction: a sentence in the record claiming prefix runs report `HED4007` "at 3000, 20000 and
  100000" was false when written, and the residual note two sentences later was right.

### F-028 — A generator diagnostic reported at `Location.None`

- **status:** FIXED
- **severity:** off-scale (usability of a build error)
- **found:** cycle 2
- **symptom:** `HED7020` reported an emitter fault at `Location.None` although the template's text was
  in scope three lines away, so the IDE error list had nothing to navigate to and the path lived only
  in the message.
- **fixed by:** `570cf84`.
- **pinned by:** not recorded at the time — **cycle 3 found this fix entirely unpinned** (it reverts
  green). Whether a later commit pinned it is not recorded.
- **regression check:** none identified. Candidate for the next cycle: assert the location of the
  `HED7020` a generator fault produces.

### F-029 — An equality comparer whose hash disagreed with its equality

- **status:** FIXED
- **severity:** 1 (the assembly cache held one assembly twice, making every type in it ambiguous)
- **found:** cycle 2
- **symptom:** `AssemblyNameEqualityComparer` compared names case-insensitively while hashing them
  case-sensitively, so two names that compare equal could hash apart. Separately, its
  public-key-token read could throw out of `GetHashCode` — inside a dictionary lookup no caller
  guards — for a token shorter than eight bytes.
- **fixed by:** `570cf84` (both halves).
- **pinned by:** `AssemblyNameEqualityTests.NamesDifferingOnlyInCaseAreOneEntry`,
  `.AShortPublicKeyTokenDoesNotThrowOutOfTheHash` — added later, by `66b86df`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AssemblyNameEqualityTests`;
  both revert red.
- **notes:** **cycle 3 found this fix entirely unpinned** — it reverted green for two cycles.

### F-030 — The cached-failure path had no test at all

- **status:** FIXED
- **severity:** 6
- **found:** cycle 2, by the mutation-testing agent (three of four checked defects were entirely
  unpinned)
- **symptom:** no test exercised the path that serves a cached compile failure.
- **fixed by:** `2576bc2` and later `f872b79`/`31e618d` (the generation rules).
- **pinned by:** `CSharpTierMetadataTests.AFailureCachedBeforeRegistrationDoesNotSurviveIt`;
  `PreparseCacheGenerationTests.*`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PreparseCacheGenerationTests`.

### F-031 — A byte-parity gate over a hand-maintained subset

- **status:** FIXED
- **severity:** 6
- **found:** cycle 2 (recorded as "not fixed"), closed in the 6→7 interval and the closing round
- **symptom:** `CorpusRenderParityTests` was ten hand-listed template names against thirty-two
  eligible, with nothing to notice the other twenty-two.
- **fixed by:** `66b86df` — the set comes from the intent table, and a declared entry missing from the
  corpus is a red build; `0db6222` — everything not declared `ResolveOnly` is rendered and compared,
  and everything declared `ResolveOnly` is rendered too, to prove it cannot be (see F-076).
- **pinned by:** `CorpusRenderParityTests.EveryDeclaredStandaloneEntryIsInTheCorpus`,
  `.EveryDeclaredPrecompilingEntryIsInTheCorpus`, `.AnEntryDeclaredResolveOnlyGenuinelyDoesNotRender`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CorpusRenderParityTests`;
  add a template to `src/TestCorpus` without an intent row and confirm the build reddens.

### F-032 — A depth bound placed after the early return it was meant to protect

- **status:** FIXED
- **severity:** 3 (process death, exit 134, 52,309 `RuleContext.GetText()` frames)
- **found:** cycle 3, both reviewers independently
- **symptom:** a deep left-associative run plus one stray `@(` still killed the process.
- **root cause:** `EnsureTreeWithinLimit` sat *after* the early `return tree.GetText()` that fires
  when a parse reported errors — and `GetText()` recurses over the whole tree.
- **fixed by:** `2576bc2`.
- **pinned by:** `DeepNestingGeneratorTests.ADeepTemplateThatAlsoHasASyntaxErrorIsStillBounded`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~ADeepTemplateThatAlsoHasASyntaxErrorIsStillBounded`.
- **notes:** it mattered most exactly where the bound was sold hardest — an editor's document has a
  syntax error most of the time, and the generator is the compiler.

### F-033 — Replaying a cached diagnostic replayed its position

- **status:** FIXED
- **severity:** 1 (a one-line document was told its error was on line four)
- **found:** cycle 3
- **symptom:** the first caller's coordinates were stamped onto every later caller. Positions had been
  correct before the cache fix (F-023) introduced this.
- **fixed by:** `2576bc2` — cached diagnostics carry messages only and are re-stamped by each caller.
- **pinned by:** `CSharpTierMetadataTests.ARepeatedFailingExpressionIsReportedAtEachCallersOwnPosition`
  — **a guard, not a red-verified pin**, and the test says so: replaying a fixed position leaves the
  suite green because two documents sharing an expression do not reliably share a cache entry here.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~ARepeatedFailingExpressionIsReportedAtEachCallersOwnPosition`.
  Be aware this passes against a build that re-stamps wrongly.
- **notes:** weak pin. Candidate for the next cycle.

### F-034 — A failure cache with no invalidation outlived the registration that would heal it

- **status:** FIXED
- **severity:** 3 (a fault that used to heal on the next compile became load-order-decided forever)
- **found:** cycle 3
- **symptom:** a failure cached before a host registered an assembly survived the registration for the
  life of the process.
- **root cause:** nothing invalidated the cache when the assembly set changed — introduced by F-023's
  fix, one file over from the observation gate that exists to prevent exactly this property.
- **fixed by:** `2576bc2` — a cached failure is retired when the observed generation moves.
- **pinned by:** `CSharpTierMetadataTests.AFailureCachedBeforeRegistrationDoesNotSurviveIt`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~AFailureCachedBeforeRegistrationDoesNotSurviveIt`.
- **notes:** sibling F-088 (a cached *success* was never invalidated either, found five cycles later).

### F-035 — The constant fold lost the operand type by widening every integer to `long`

- **status:** FIXED
- **severity:** 2 and 4 (both directions)
- **found:** cycle 3
- **symptom:** five classes still broke the host build (`(2147483647+0)+(1+0)`, `+2147483647+1`,
  `uint` overflow, `decimal` overflow), and legal C# was refused, taking whole templates off the
  precompiled tier.
- **fixed by:** `2576bc2` — rewritten to track the type C# evaluates in, including the constant
  `int`→`uint` conversion and the `-2147483648` literal rule (the latter deleted again in cycle 5, see
  F-049).
- **pinned by:** `ConstantArithmeticDifferentialTests` (`.ConstantDivisionByZeroDegradesInsteadOfBreakingTheBuild`,
  `.LegalArithmeticStillPrecompiles` and the mixed-width rows added by `1b64d10`).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`;
  deleting the fold reddens 17 of 32 rows.

### F-036 — A test class that could not fail because every case degraded anyway

- **status:** FIXED
- **severity:** 6
- **found:** cycle 3
- **symptom:** every fold case used `@model(){{dynamic}}`, under which constant-only expressions
  degrade anyway — **deleting the entire fold left all 876 generator tests green**.
- **fixed by:** `2576bc2` — typed models make a degrade attributable; deleting the fold now reddens 17
  of 32.
- **pinned by:** the same suite, retyped.
- **regression check:** delete the fold's entry point and confirm ~17 `ConstantArithmeticDifferentialTests`
  rows redden.
- **notes:** the first appearance of the rule cycle 12 states in general (F-118): **a test that only
  asserts a degrade cannot distinguish a rule from a blanket refusal.**

### F-037 — Import depth measured per document, and cycle identity taken from the raw spelling

- **status:** FIXED
- **severity:** 3 (stack exhaustion; and 863,109 diagnostics at build time)
- **found:** cycle 3
- **symptom:** a chain of 4000 distinct files — no cycle anywhere — exhausted the stack. And
  `./a.heddle` and `d/../a.heddle` read as different documents, so a cycle walked past the guard and
  eight spellings produced 863,109 diagnostics.
- **fixed by:** `2576bc2`.
- **pinned by:** `ImportCycleTests.AnUnboundedImportChainIsReportedInsteadOfKillingTheProcess`,
  `.ACycleIsCaughtHoweverTheImportPathIsSpelled`, and (cycle 4)
  `.ACycleWhoseSpellingsNeverRepeatIsStillCaught`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ImportCycleTests`.
- **notes:** the normalisation was still wrong for the generator's own reader — F-063. And the
  original cycle-normalisation test could not fail, F-044.

### F-038 — A six-character template threw out of the compile

- **status:** FIXED
- **severity:** 3 (an uncatchable-shaped failure from a malformed template)
- **found:** cycle 3, by a test written for something else
- **symptom:** `@(1)}}` underflowed the lexer's mode stack and the `InvalidOperationException`
  escaped the compile. Pre-existing, unrelated to depth.
- **fixed by:** `2576bc2`.
- **pinned by:** `DeepNestingTests.UnbalancedClosersAreReportedRatherThanThrown`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~UnbalancedClosersAreReportedRatherThanThrown`.

### F-039 — A reference built twice on every cache miss

- **status:** FIXED
- **severity:** off-scale (waste, not a divergence)
- **found:** cycle 3, by review — **not by any test**
- **symptom:** `RoslynReferenceProvider` built each reference twice on a cache miss. Pre-existing.
- **fixed by:** `2576bc2`, as part of anchoring the assembly.
- **pinned by:** not recorded.
- **regression check:** none identified.

### F-040 — The fold read the wrong field when converting an `int` operand to `uint`

- **status:** FIXED
- **severity:** 2 and 4 (2,081 build breaks and 1,636 needless degrades, by differential fuzzing
  against the real compiler)
- **found:** cycle 4, both reviewers independently, from one line
- **symptom:** `(4294967295u + 1)` folded as `(4294967295 + 0)`, was emitted, and failed the host's
  build with a raw `CS0220` — verbatim the symptom the fold exists to prevent. `(3000000000 / 2)`
  folded as division by zero and was refused, taking the whole template off the precompiled tier.
- **root cause:** an `Int`-kind `Numeric` keeps its value in the signed field; the `uint` conversion
  read the unsigned one, so every `int` operand promoted to `uint` arrived as zero. Complementing a
  `uint` was also computed in `ulong` and narrowed under check (so every `~x` on a `uint` looked like
  an overflow), and negating a floating constant returned it unchanged.
- **fixed by:** `1b64d10`.
- **pinned by:** eleven mixed-width rows in `ConstantArithmeticDifferentialTests`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`.

### F-041 — The fold's test class had no case that could see the defect

- **status:** FIXED
- **severity:** 6
- **found:** cycle 4
- **symptom:** no case paired an unsuffixed `uint`-range literal with a small `int`, and the one
  `uint` case in the suite passed *because of* the bug.
- **fixed by:** `1b64d10` — eleven mixed-width cases added.
- **regression check:** as F-040.
- **notes:** second instance of the same class as F-036, in the same suite, one cycle later.

### F-042 — A fixed depth bound set above the stack it has to fit inside

- **status:** FIXED (superseded by the value change in F-051)
- **severity:** 3 (the crash always arrived before the bound)
- **found:** cycle 4
- **symptom:** at a bound of 1000, a 1 MB thread — the Windows default, and what the thread pool hands
  out — overflows on the parser-recursive shapes at around 350, so a generator hosted in MSBuild died
  on a template a few hundred characters long.
- **root cause:** a fixed count is only host-independent if it sits below *every* host's limit.
- **fixed by:** `1b64d10` — 300, measured against 300/500/800 last-safe depths at 1/2/4 MB.
- **pinned by:** see F-026 and F-046.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`.
- **notes:** still wrong — the 300 was a Debug measurement (F-051).

### F-043 — A per-parse budget stored on a reusable public settings object

- **status:** FIXED
- **severity:** 3 (a host reusing one settings object stopped reporting import cycles from the
  thirty-third parse, while still skipping them)
- **found:** cycle 4
- **root cause:** the cycle-report budget lived on `ParserSettings` and was never reset.
- **fixed by:** `1b64d10` — reset per top-level parse.
- **pinned by:** `ImportCycleTests.ReusingOneSettingsObjectKeepsReportingCycles`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~ReusingOneSettingsObjectKeepsReportingCycles`.
- **notes:** the same object held the import *stack* too — F-065, found three commits later. Same
  class, different field.

### F-044 — The import-cycle normalisation had no real pin

- **status:** FIXED
- **severity:** 6
- **found:** cycle 4
- **symptom:** the existing test's documents each imported their own spelling, so the raw key repeated
  and the cycle fired without the normalisation.
- **fixed by:** `1b64d10` — a cycle whose spellings never repeat; reverting the normalisation reddens.
- **pinned by:** `ImportCycleTests.ACycleWhoseSpellingsNeverRepeatIsStillCaught`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~ACycleWhoseSpellingsNeverRepeatIsStillCaught`.

### F-045 — The language reference contradicted itself about nesting limits

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** cycle 4
- **symptom:** one section still promised imports could nest "to any depth"; the 64-level import bound
  was stated nowhere; and the block-nesting figure was the internal rule count (1000), not the ~330
  nested blocks actually allowed.
- **fixed by:** `1b64d10`.
- **pinned by:** not recorded.
- **regression check:** none automated.

### F-046 — The depth limit was pinned by nothing over a range of 125–1499

- **status:** FIXED
- **severity:** 6
- **found:** cycle 5, two reviewers converging
- **symptom:** the suite tolerated any limit value from 125 to 1499, so the previous cycle's headline
  change (1000 → 300) could have been skipped entirely and 2,766 tests would still have passed. The
  test constant mirroring the production value was left at 1000, under a comment saying it exists as a
  copy precisely so a change cannot pass unnoticed — **a copy only notices if something compares it.**
- **fixed by:** `25b2554` — the value is asserted directly, and the property it was chosen for is
  stated as a test.
- **pinned by:** `DeepNestingTests.TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`,
  `.APrefixRunPastTheLimitIsReportedOnASmallStack`.
- **regression check:** change the limit constant by one and confirm a red.

### F-047 — Unary minus on a `uint` left undecided

- **status:** FIXED
- **severity:** 2
- **found:** cycle 5, by differential fuzzing against the real compiler
- **symptom:** `(-1u)/0` reached the host build as `CS0020`. Unary minus on a `uint` yields `long` in
  C#, so it is decidable; the fold left it undecided, and undecided means "emit as written".
- **fixed by:** `25b2554`.
- **pinned by:** `ConstantArithmeticDifferentialTests` rows for the shape.
- **regression check:** as F-040.

### F-048 — A refusal scoped to a whole operand kind rather than the illegal pairs

- **status:** FIXED
- **severity:** 2 (68 cases were emitted and rejected by the host's compiler)
- **found:** cycle 5
- **symptom:** refusing to decide whenever a `ulong` met any signed operand was too broad: C# converts
  a non-negative integer constant to `ulong` implicitly, which is how `ulong + 'a'` is legal — so a
  `char` meeting a `ulong` was emitted and rejected, 68 cases.
- **fixed by:** `25b2554`.
- **pinned by:** `ConstantArithmeticDifferentialTests.AUnsignedLongMeetingACharDegradesInsteadOfBreakingTheBuild`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~AUnsignedLongMeetingAChar`.
- **notes:** the *replacement* narrow guard turned out to be dead code — F-053.

### F-049 — A fold rule that modelled text the emitter never writes

- **status:** FIXED (rule deleted)
- **severity:** 4
- **found:** cycle 5
- **symptom:** the literal-minimum rule modelled C#'s reading of `-2147483648` as one `int` constant,
  but the emitter writes `2147483648U`, which C# types as `long` — so it described text that is never
  emitted and needlessly degraded suffixed and parenthesised forms.
- **fixed by:** `25b2554` — deleted rather than repaired.
- **pinned by:** the case moved into the legal set of `ConstantArithmeticDifferentialTests`.
- **regression check:** as F-040.
- **notes:** **a test had already enshrined the wrong answer**, asserting a degrade for an expression
  the emitted code compiles and the engine renders as `-2147483649`. A test can pin a defect.

### F-050 — An assertion set to exactly what the fixture produces with the guard deleted

- **status:** FIXED
- **severity:** 6
- **found:** cycle 5
- **symptom:** the cycle-report assertion was set at 64, which is exactly what that fixture produces
  with the budget deleted, so it could not fail.
- **fixed by:** `25b2554` — compares against the budget.
- **pinned by:** `ImportCycleTests.CycleReportingIsBoundedHoweverManySpellingsReachIt`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~CycleReportingIsBounded`.
- **notes:** **the fix was itself unfalsifiable** and had to be fixed again — F-067. Comparing against
  the constant means the test agrees with any value the constant is changed to.

### F-051 — A bound measured in Debug and shipped in Release

- **status:** FIXED
- **severity:** 3
- **found:** cycle 6
- **symptom:** a Release build — what a source generator actually runs as — dies at 284 on a 1 MB
  thread where Debug survives to 503. The guard fired at 293, so for the parser-recursive shapes it
  could never fire first: every input deep enough to trip the bound killed the process before reaching
  it.
- **fixed by:** `0f9ba46` — limit 250, below the Release figure; both numbers recorded in the comment.
- **pinned by:** `DeepNestingTests.TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`.
- **regression check:** as F-046. Re-measure in **Release** if the bound is ever revisited.

### F-052 — A pin that does not redden when the property breaks — it takes the host down

- **status:** FIXED (test deleted, with a note saying what would verify it)
- **severity:** 6
- **found:** cycle 6
- **symptom:** the test written to pin the depth property aborted the net8.0 Release run after 1,143
  of 1,865 tests — breaking CI in a configuration nobody had run.
- **fixed by:** `0f9ba46` — deleted; the limit's value stays asserted, which is the part a test can
  hold. Verifying the crash property needs a child process comparing exit codes, which no suite does.
- **pinned by:** n/a — deliberately.
- **regression check:** none. Do not re-add a test that reaches the crash in-process.

### F-053 — A guard added to fix a defect that could never fire

- **status:** FIXED (deleted)
- **severity:** 6
- **found:** cycle 6, adversary
- **symptom:** the `ulong`/negative-operand guard added in cycle 5 is dead code: replacing its
  condition with `if (false)` produced a byte-identical fingerprint of `CompilerWouldReject` over
  **2,587,410** folded expressions. The checked cast in `Convert` already overflows for exactly that
  set.
- **fixed by:** `cb4c426` — guard and helper deleted, the explanation moved to the cast where the
  behaviour actually lives.
- **regression check:** re-run the fingerprint sweep if a similar guard is proposed: replace its
  condition with `if (false)` and compare fold verdicts over the fuzz corpus.
- **notes:** it was the removal of the *old broad* condition (F-048) that fixed the 68 cases; the
  replacement never fired. Reversal worth remembering: three lines of justification are not evidence.

### F-054 — Comments describing rules that had already been deleted or moved

- **status:** FIXED
- **severity:** off-scale (documentation in code)
- **found:** cycle 6, adversary
- **symptom:** `Unify`'s summary still described the rule deleted a commit earlier, and `Wider`'s
  summary had been orphaned onto an unrelated helper by an insertion between a doc comment and its
  method.
- **fixed by:** `cb4c426`.
- **regression check:** none automated.

### F-055 — "Member paths cost nothing" — a comment that hid an exponential

- **status:** FIXED (the comment; the defect it hid is F-056)
- **severity:** off-scale for the comment; the hidden defect is severity 1
- **found:** cycle 6, adversary
- **symptom:** a `ParseDepthGuard` comment claimed member paths are free. They are free to *parse* and
  not free to *compile*: a null-safe hop duplicates its receiver in the expression tree, so an n-hop
  chain performs 2^n − 1 property reads at render where the generated tier performs n, and a 17-hop
  dynamic path fails to compile at all.
- **fixed by:** `cb4c426` (comment), `13c7172` (behaviour — F-056).
- **regression check:** see F-056.

### F-056 — A null-safe hop spelled its receiver twice, in every tier

- **status:** FIXED
- **severity:** 1 (the two tiers disagreed, exponentially, about how many times a template reads a
  model — observable through any getter that logs, materialises lazily or queries)
- **found:** cycle 6, adversary (reported, not fixed there)
- **symptom:** LINQ expression trees are trees, not DAGs, so hop k held two copies of everything below
  it: an n-hop path compiled to 3·2^(n+1)−5 nodes and called 2^(n+1)−1 getters. Fourteen hops took
  859 ms to compile, sixteen took 7.6 s, seventeen produced IL the runtime refused with
  `InvalidProgramException`. The emitter had the same doubling in its non-nullable-value hop, bounded
  at one duplication: it charged 2n+1 reads where the engine charges n+1.
- **fixed by:** `13c7172` — each receiver bound to a local, in the member tier, the dynamic tier and
  the native-expression tier's array and indexer accesses; the emitter switched to `x?.M ?? default(T)`.
- **pinned by:** `NullSafeHopEvaluationTests.TheMemberTierReadsEachHopExactlyOnce`,
  `.TheNativeExpressionTierReadsEachHopExactlyOnce`, `.TheDynamicTierReadsEachHopExactlyOnce`,
  `.AnIndexedHopEvaluatesItsReceiverOnce`, `.ADeepPathStillCompiles`,
  `.TheAccessorTreeGrowsLinearlyWithPathLength`;
  `MemberHopEvaluationCountTests.BothTiersReadTheModelTheSameNumberOfTimes`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~NullSafeHopEvaluationTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~MemberHopEvaluationCountTests`.
- **notes:** the emitter's new spelling introduced F-069 (`?.` on a type with no nullable form), whose
  fix introduced the pair F-072/F-073 and then F-082. **Four commits in this chain each introduced the
  next defect.**

### F-057 — Import fan-out was unbounded

- **status:** FIXED
- **severity:** 3 (a build that never finishes)
- **found:** 6→7 interval; not recorded which reviewer
- **symptom:** the cycle guard only knows what is currently on the import stack, so a document already
  parsed and popped is re-read. A graph where each file imports the next one twice is acyclic and
  twenty levels deep — well inside the depth ceiling of sixty-four — and expanded to 2,097,151 parses
  with no diagnostic at all; at the ceiling, upwards of 10^19.
- **fixed by:** `8a30aed` — total expansions per top-level parse bounded (1024 initially), overflow
  described once as `HED4008`. Repeats cannot simply be collapsed: each expansion contributes the
  imported document's output chains at its own position.
- **pinned by:** `ImportCycleTests.AnAcyclicImportFanOutIsBoundedAndReported`,
  `.TheFanOutOverflowIsDescribedOnce`, `.TheFanOutBudgetIsRestoredForEachTopLevelParse`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~FanOut`.
- **notes:** the bound was wrong twice — unpinned (F-058) and then charged per path rather than per
  document (F-071, raised to 16384).

### F-058 — Both tests for the fan-out bound read the constant they were checking

- **status:** FIXED
- **severity:** 6
- **found:** cycle 7
- **symptom:** both tests read `MaxImportExpansions` itself, so they agreed with any value it was
  raised to; raising it to 65536 left them green. **The same commit that added them had diagnosed and
  fixed exactly this defect for the cycle-report budget two hunks away.**
- **fixed by:** `e46d0b6` — the value is asserted.
- **pinned by:** `ImportCycleTests.TheFanOutBoundIsTheValueThatWasChosen`.
- **regression check:** change `MaxImportExpansions` and confirm a red.

### F-059 — "No opinion" treated as "no problem" for five operators and the conditional

- **status:** FIXED
- **severity:** 2
- **found:** 6→7 interval
- **symptom:** the fold returned `Unknown` for `&`, `|`, `^`, `<<`, `>>` and never looked at `?:` —
  and `Unknown` is not a refusal, it means the expression is written through. `(1&1)/0` and
  `(true?1:1)/0` reached the host's compiler as `CS0020`, `(1<<1)+2147483647` as `CS0220`.
- **fixed by:** `8010e2a` — bitwise operators evaluate in the promoted type; shifts evaluate in the
  left operand's own type with the count masked; a conditional over a literal condition folds to the
  taken arm, typed as the common type of both arms. A fault in the discarded arm still refuses,
  because C# reports it there too.
- **pinned by:** twenty-one new rows in `ConstantArithmeticDifferentialTests` (each red before, since
  the harness compiles the emitted code), and fourteen pinning the other direction.
- **regression check:** as F-040.
- **notes:** the "cannot say is the exempting answer" shape — the same generalisation cycle 17 reaches
  from a different direction (F-148).

### F-060 — A last-resort handler with no id and a message built from an empty name

- **status:** FIXED
- **severity:** 3 (a caller classifying compile failures by id saw nothing to classify)
- **found:** 6→7 interval
- **symptom:** the catch-all around compiling one call populated `Exception`, `Position` and `Error`
  but no `DiagnosticId`, and the text was built from the extension name alone — for an unnamed call
  the message was literally `Error while compiling `.
- **fixed by:** `be7b4c1` — raises `HED0005`, names the call (or "an expression"), includes the
  fault's own message.
- **pinned by:** `NativeOperatorRulesTests` rows for the `bool`/`bool?` bitwise shape, which is the
  reachable route into this handler.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~NativeOperatorRulesTests`.
- **notes:** the underlying `bool`/`bool?` bitwise pair still has **no dedicated diagnostic** — see the
  known-open register.

### F-061 — An unreadable `@<<` import threw out of the parse

- **status:** FIXED
- **severity:** 3 (the LSP published no diagnostics at all for the document)
- **found:** 6→7 interval
- **symptom:** `ParserSettings.ReadImport` opened the file with no guard from inside the tree walk —
  the region `DocumentParser`'s own `try` excludes. A half-typed import path threw
  `FileNotFoundException` out of `didOpen`, so the document got no diagnostics: not for the missing
  import, and not for anything else in it.
- **fixed by:** `1f3ef70` — guarded, reports `HED4009` positioned over the `@<<` directive; the import
  is skipped and the rest of the document still parses.
- **pinned by:** `ImportCycleTests.AnImportThatCannotBeReadIsReportedAndTheRestOfTheDocumentStillParses`;
  `LanguageServiceDiagnosticsTests.AMissingImportIsReportedInsteadOfEndingTheAnalysis`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~AnImportThatCannotBeRead`
  and `dotnet test src/Heddle.LanguageServices.Tests -f net8.0 --filter Name~AMissingImportIsReported`.

### F-062 — A guard written for the disk reader's exception set, over a public seam

- **status:** FIXED
- **severity:** 3
- **found:** cycle 7
- **symptom:** the unreadable-import guard caught only the exceptions the disk reader raises, while
  `ImportReader` is a public seam a host supplies — so the two idiomatic ways for a host to say "no
  such import" (returning `null`, letting a lookup throw) both escaped and took the parse down. That
  is the failure the guard was added to stop.
- **fixed by:** `e46d0b6` — the reader is treated as untrusted input.
- **pinned by:** `ImportCycleTests.AnImportReaderThatReturnsNullIsReportedRatherThanThrown`,
  `.AnImportReaderThatThrowsIsReportedRatherThanThrown`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~AnImportReaderThat`.
- **notes:** class: **a guard scoped to the implementation in front of you rather than to the seam's
  contract.** Same class as F-096, F-097, F-098.

### F-063 — Two components disagreed about what makes two imports the same document

- **status:** FIXED
- **severity:** 3 (six spellings of a self-importing file were read 1024 times — enough to exhaust the
  import budget, and before that budget existed, to hang)
- **found:** 6→7 interval
- **symptom:** the cycle guard keyed on `Path.GetFullPath` while the generator's reader resolves
  through `TemplateKey.TryNormalize` (appending `.heddle`, stripping a leading `~/`, `/` or `./`,
  unifying separators). So at build time one template had as many identities as it had spellings.
- **fixed by:** `6b2b876` — `ParserSettings` gains an `ImportIdentifier` seam alongside
  `ImportReader`, and the generator sets both from the same normaliser so the two cannot drift.
- **pinned by:** `ImportCycleTests.ASelfImportUnderManyKeySpellingsIsOneDocumentToTheCycleGuard`,
  `.TheCycleGuardFollowsTheReadersOwnNotionOfIdentity`;
  `PipelineDiagnosticsTests` (the end-to-end generator half). Both red with the seam removed.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~CycleGuard`.

### F-064 — A cache entry holding a `Type` from a collectible context, evicted only on failure

- **status:** FIXED
- **severity:** 5 (every workspace reload leaked another copy of its model assemblies)
- **found:** 6→7 interval
- **symptom:** a preparse entry carries the expression's result type, and for an expression naming a
  workspace model type that is a `Type` from the collectible load context. The cache was keyed on
  generated source and evicted only for cached *failures*, so one successful C#-tier analysis pinned
  the context for the life of the process and `ModelAssemblyManager.Unload()` freed nothing.
- **fixed by:** `49d17e9` — the cache moved into its own class holding no Roslyn types, and
  `UnregisterModelAssemblies` clears it alongside the registrations.
- **pinned by:** `ModelAssemblyReloadTests.ReloadCollectsPreviousModelContextAfterACSharpTierAnalysis`
  (red on the leak).
- **regression check:** `dotnet test src/Heddle.LanguageServices.Tests -f net8.0 --filter Name~ReloadCollectsPreviousModelContext`.
- **notes:** two comments claiming metadata references never pin a collectible context were corrected
  rather than the anchor removed: `RoslynReferenceProvider` **does** anchor an assembly to any
  reference built over its in-memory metadata, on purpose — the anchor defers the unload until the
  last holder is finished rather than preventing it.

### F-065 — Per-parse import state on a shared, public settings object

- **status:** FIXED
- **severity:** 3 (phantom `HED4006`s and an `InvalidOperationException` straight out of the parse)
- **found:** 6→7 interval
- **symptom:** the import stack and the report budgets lived on `ParserSettings`, which a host builds
  once and reuses, so two documents parsing through one instance shared them: each saw the other's
  imports as its own, and the cycle message — built by joining that stack while another thread
  appended to it — threw. Sixty-four concurrent parses of two independent three-file graphs produced
  twenty-two escaping exceptions and a run of phantom `HED4006`s.
- **fixed by:** `4ad3283` — the stack and budgets move to `ImportParseState`, held per thread.
- **pinned by:** `ImportCycleTests.OneSettingsObjectServesConcurrentParsesWithoutCrossTalk`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~OneSettingsObjectServesConcurrentParses`.
- **notes:** the thread-static fix introduced F-070. Sibling of F-043, which moved the *budget* off the
  same object three commits earlier — the stack was left behind.

### F-066 — A diagnostic documented for one of its two producers

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** 6→7 interval
- **symptom:** `HED4007`'s summary and registry row described expression, chain and block nesting
  only, while the diagnostic also carries the `@<<` import-depth case.
- **fixed by:** `66b86df`.
- **regression check:** none automated — the diagnostics gate checks presence, not completeness.
- **notes:** same class as F-009. The *message* half of the same id was unpinned at both producers —
  F-075.

### F-067 — The fix for an unfalsifiable assertion was itself unfalsifiable

- **status:** FIXED
- **severity:** 6
- **found:** 6→7 interval
- **symptom:** `CycleReportingIsBoundedHoweverManySpellingsReachIt` compared the diagnostic count
  against the budget *constant*, so it agreed with any budget — including one raised past the 64 the
  fixture can produce, at which point it observed nothing.
- **fixed by:** `66b86df` — the bound is written out and the constant's value asserted; raising the
  budget to 512 now reddens.
- **regression check:** raise the budget constant and confirm a red.
- **notes:** direct continuation of F-050. **Comparing a test's expectation against the production
  constant is the recurring anti-pattern of this series** (also F-046, F-058).

### F-068 — Code comments citing documents

- **status:** FIXED
- **severity:** off-scale (convention)
- **found:** 6→7 interval
- **symptom:** five code comments carried specification, plan and milestone ids. The rule is one-way:
  documents may cite code, code may not cite documents.
- **fixed by:** `66b86df`.
- **regression check:** grep the source tree for document-id shapes in comments.
- **notes:** **this register is subject to the same rule.** Do not answer a finding by adding an
  `F-0xx` reference to a comment or a test name.

### F-069 — `?.` emitted onto a type that has no nullable form

- **status:** FIXED
- **severity:** 2 (`CS8978` against the `.heddle` file, no Heddle diagnostic, no degrade)
- **found:** cycle 7
- **symptom:** the null-safe hop rewrite (F-056) spells the null-default hop `x?.M ?? default(T)`, and
  `?.` widens the member to `T?`. A ref struct has no nullable form, so a model with a `Span` or
  `ReadOnlySpan` property emitted code the consumer's compiler rejects outright.
- **fixed by:** `e46d0b6` — where the widening is impossible the receiver is spelled twice again,
  which is what that form always did for such a type. The duplication cannot compound: a ref-struct
  hop can only ever be the last one in a chain.
- **pinned by:** `ConstantArithmeticDifferentialTests.ARefStructPropertyEmitsCodeThatCompiles`
  (reverting the gate reproduces `CS8978`).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~ARefStructPropertyEmitsCodeThatCompiles`.
- **notes:** the commit that introduced it asserted the new spelling "says the same thing". The
  spelling-twice restoration then became reachable in a new position and produced F-082.

### F-070 — Thread-static state broke re-entrancy through a public callback

- **status:** FIXED
- **severity:** 3 (a cycle reported that was not there)
- **found:** cycle 7, verifier
- **symptom:** `ImportReader` is a public host callback, and a host that resolves an import by parsing
  re-enters the parser on the same thread with an import stack already on it. The inner parse read and
  mutated the outer parse's stack and budgets. Per-settings state had isolated those; the
  thread-static (F-065's fix) did not.
- **fixed by:** `d034dda` — the callback window is marked, and a parse beginning inside it gets fresh
  state and hands the outer parse's back untouched.
- **pinned by:** `ImportCycleTests.AParseBegunInsideAnImportReaderIsIndependentOfTheOuterParse` —
  **which does not redden, and says so**: an import is read before its name is pushed, so at the
  nesting this fixture reaches the outer stack is empty and the inner parse resets anyway. The review
  reproduced the false cycle from a deeper outer parse; the fixer could not get this shape there.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~AParseBegunInsideAnImportReader`
  — but understand it passes against the unfixed code. **Weak pin; candidate for the next cycle** (a
  deeper outer parse is the missing fixture).

### F-071 — A fan-out bound that charges a shared document once per path that reaches it

- **status:** FIXED
- **severity:** 4/3 (a legitimate design system failing the build)
- **found:** cycle 7
- **symptom:** the bound counts expansions rather than distinct documents, so fifty components each
  pulling twenty shared token files is over a thousand — failing a bound meant to stop an exponential.
- **fixed by:** `d034dda` — raised to 16384, which still caps the 2^21 blow-up the bound exists for
  while leaving real graphs an order of magnitude of headroom.
- **pinned by:** `ImportCycleTests.TheFanOutBoundIsTheValueThatWasChosen` (value assertion).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~FanOut`.
- **notes:** a *bound* is a policy number; measure it against a realistic graph, not only against the
  attack it was written for.

### F-072 — `?.` short-circuits the rest of the chain; the engine defaults one hop and keeps walking

- **status:** FIXED
- **severity:** 1 (two tiers, two different renderable answers, no diagnostic on either)
- **found:** closing round; confirmed-but-unfixed from the earlier cycles
- **symptom:** `Inner.Maybe.HasValue` over a null `Inner` produced nothing at all on the generated
  tier, while the engine read `HasValue` off `default(int?)` and produced `False`. The `.Value` form
  of the same path diverged the other way: the engine threw `InvalidOperationException` and the
  generated tier rendered empty. Four of four null-hop cases were red.
- **root cause:** the emitter wrote a member path as one null-conditional chain, so C#'s rule that
  `?.` governs everything to its right met an engine that defaults the failed hop and keeps walking.
- **fixed by:** `0db6222` — end the chain with parentheses before a plain `.`; `(a?.B).C` reads `C`
  off the default, which is the hop the engine performs.
- **pinned by:** `NullSafeHopChainTests.AHopThroughNullReadsTheMemberOfTheDefaultOnBothTiers`,
  `.APresentValueRendersIdenticallyOnBothTiers`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~NullSafeHopChainTests`.
- **notes:** this fix made the ref-struct hop form *compile* for a deep prefix and so made its
  receiver duplication reachable — F-082, a silent-wrong-output regression, in the very next cycle.

### F-073 — A ref-struct hop behind a reference hop did not compile

- **status:** FIXED
- **severity:** 2 (`Inner.Buf.Length` emitted two `CS8978`s into the consumer's build)
- **found:** closing round, by asking what else the chain-propagation rule touched — **not by any
  existing test**
- **root cause:** the ref-struct form spells its receiver twice, and spelling a propagating chain into
  the read half re-applies `?.` to a type with no nullable form.
- **fixed by:** `0db6222` — the same parenthesisation as F-072.
- **pinned by:** `NullSafeHopChainTests.ARefStructHopBehindAReferenceHopCompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~ARefStructHopBehindAReferenceHopCompiles`.

### F-074 — A cache drop that ran before the removal it was draining

- **status:** SUPERSEDED (the epoch this added was removed in the next cycle as redundant — F-093)
- **severity:** 3
- **found:** closing round
- **symptom:** the preparse cache could serve exactly the entry the drop existed to remove: the drop
  ran *before* the assemblies were unregistered, and a compile already in flight stores its result
  afterwards, into the map that was just emptied.
- **fixed by:** `0db6222` — entries carry the epoch their compile began under and are refused if it
  has moved; the drop happens after the removal.
- **pinned by:** `PreparseCacheEpochTests.AnEntryFromACompileThatStartedBeforeTheDropIsNotServedAfterIt`,
  `.AnEntryFromTheCurrentEpochIsServed` — **both deleted** by `f872b79`, replaced by
  `PreparseCacheGenerationTests`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PreparseCacheGenerationTests`.
- **notes:** read F-093 before touching this area — the epoch no longer exists, and the current shape
  holds one assembly generation at a time.

### F-075 — A diagnostic's message unpinned at both of its unrelated producers

- **status:** FIXED
- **severity:** 6
- **found:** closing round
- **symptom:** `HED4007` has two producers with unrelated faults and unrelated remedies — expression
  nesting and `@<<` import nesting — and collapsing both messages to one vague string was green.
- **fixed by:** `0db6222` — both pinned; the parse-depth path has a test again (a flat
  left-associative run reaches the reporting path without being able to reach the crash, because ANTLR
  loops left recursion).
- **pinned by:** `DeepNestingTests.AFlatRunPastTheLimitIsReported` and the import-depth message rows in
  `ImportCycleTests`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DeepNestingTests`.

### F-076 — A coverage column that let an entry exempt itself

- **status:** FIXED
- **severity:** 6
- **found:** closing round
- **symptom:** `ResolveOnly` removed an entry from byte-parity coverage on its own say-so, and seven
  entries declared `WithModel` sat outside the gate while rendering identically all along.
- **fixed by:** `0db6222` — everything not declared `ResolveOnly` is rendered and compared, and
  everything declared `ResolveOnly` is rendered too, **to prove it cannot be**.
- **pinned by:** `CorpusRenderParityTests.AnEntryDeclaredResolveOnlyGenuinelyDoesNotRender`,
  `.EveryDeclaredPrecompilingEntryIsInTheCorpus`; the column semantics are documented on
  `CorpusRender` in `src/TestCorpus/CorpusIntent.cs`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CorpusRenderParityTests`;
  declare a rendering template `ResolveOnly` and confirm a red.
- **notes:** closes the item the third cycle left open (F-031). Cycle 8 then found the `ResolveOnly`
  assertion was satisfied by `Assert.ThrowsAny<Exception>` — including the harness itself falling
  over (F-090).

### F-077 — A differential harness whose answer depended on test scheduling

- **status:** FIXED
- **severity:** 6
- **found:** closing round; visible as two unrelated cases going red under a mutation that only
  changed scheduling
- **symptom:** handing an assembly to Roslyn equips only the precompiled side; the dynamic reference
  resolves model types over what is actually *loaded* in the process. Corpus templates naming engine
  test models compiled or failed depending on whether an earlier test in the same run had loaded
  `Heddle.Tests.dll`.
- **fixed by:** `0db6222` — the harness loads what it references.
- **pinned by:** `ModelResolutionLoadOrderTests.TheHarnessLoadsExtraReferencesSoDifferentialSuitesDoNotDependOnTestOrder`
  (added by `f872b79`).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelResolutionLoadOrderTests`.
- **notes:** the suppression itself hides a real engine property; cycle 8 made that property a test
  rather than a side effect — F-094.

### F-078 — Arithmetic rows for types no template literal can produce

- **status:** FIXED (deleted)
- **severity:** 6
- **found:** closing round
- **symptom:** `Numeric.From` carried rows for `sbyte`/`byte`/`short`/`ushort`, which no template
  literal can produce — the language has no suffix for them, so the smallest a written number arrives
  as is `int`. `Numeric.Shift` masked its count to the operand width, which the C# `<<`/`>>` it is
  written in already does.
- **fixed by:** `0db6222`.
- **regression check:** none needed; if a suffix for a narrower type is ever added, these rows come
  back with it.

### F-079 — `Path.Combine` rejects characters on .NET Framework that .NET Core accepts

- **status:** KNOWN-OPEN
- **severity:** 3 (a throw out of the parse, on the platform where it can happen)
- **found:** closing round
- **symptom:** `Path.Combine` rejects `<`, `>` and `|` on .NET Framework and accepts them on .NET
  Core; `ParserSettings.ImportIdentity` canonicalises an `@<<` path to decide document identity.
- **fixed by:** partially — `0db6222` moved the combine inside `ImportIdentity`'s guard so a throw
  degrades the cache key instead of killing the parse. **Nothing on the development box can make it
  throw, so that change is reasoning, not evidence.**
- **pinned by:** `ImportCycleTests.AnImportPathThatCannotBeCanonicalisedStillParses` (726031b) — pins
  the degrade path, not the platform behaviour.
- **regression check:** none available on Linux. Requires a .NET Framework or Windows host.
- **notes:** deferred because the box cannot reach it. Recorded with the rest of the platform surface
  in `docs/generator_plan/unverified-platform-surface.md`.

### F-080 — On `netstandard2.0` an assembly with no file yields no metadata reference

- **status:** KNOWN-OPEN (by design; there is no API to fix it with)
- **severity:** 3
- **found:** closing round (written up in `docs/generator_plan/unverified-platform-surface.md`)
- **symptom:** `RoslynReferenceProvider.FromLoadedImage` returns `null` on .NET Framework — there is
  no `TryGetRawMetadata` — so the language service's byte-loaded model assemblies are invisible to the
  C# tier. **This is exactly the defect F-019 fixed for .NET Core.**
- **fixed by:** — knowingly not fixed.
- **pinned by:** nothing; no `netstandard2.0`-flavoured path is executed on the development box at
  all. The test project only adds `net48` on Windows.
- **regression check:** none available on Linux.
- **notes:** three more deliberate `netstandard2.0` differences are recorded in the same document:
  `AssemblyHelper.IsObservable`'s ALC exclusion compiles out, `MarkEngineEmitted` uses a different
  concurrency shape, and the `System.Range` interop compiles out. All unexercised.

### F-081 — A declared target framework runs zero tests and the run still exits 0

- **status:** KNOWN-OPEN
- **severity:** 6 (a third of the declared matrix silently does not execute)
- **found:** closing round
- **symptom:** `src/Heddle.Tests` declares `net6.0;net8.0;net10.0`; the net6.0 container cannot start
  on the development box, `dotnet test` reports `Passed!` for the other two and **exits 0**. Nothing
  in the output says a third of the matrix did not run.
- **fixed by:** — recorded as "worth fixing, and not fixed here"; it is build wiring, not engine code.
- **regression check:** grep a test log for `Testhost process for source(s) ... exited with error`
  before believing a green run covered every declared TFM.
- **notes:** net6.0 is where the `NET6_0_OR_GREATER` branches first turn on, so it is the one
  framework where a lower-bound behaviour could regress unobserved. The single highest-value item on
  that page.

### F-082 — A ref-struct hop off a reference receiver read that receiver twice

- **status:** FIXED
- **severity:** 1 (the commit before had turned the same template into a loud `CS8978`; **the fix
  converted a build failure into silent wrong output**)
- **found:** cycle 8, both reviewers
- **symptom:** `Inner.Buf.Length` over a getter that answers differently on its second call renders a
  value on the engine and throws `NullReferenceException` on the generated tier.
- **root cause:** the non-nullable-value hop form has always spelled its receiver twice (a ref struct
  has no nullable form to widen into), which was harmless while the receiver was always the model
  local. F-072's parenthesisation made the form compile for a deep prefix, and the duplication became
  reachable.
- **fixed by:** `726031b` — the receiver is bound with a type pattern, naming it once, from a counter
  owned by the emitter and shared with every expression writer it makes (a pattern variable belongs to
  the block its statement is in, so two paths in one block would collide).
- **pinned by:** `NullSafeHopChainTests.ARefStructHopReadsItsReceiverOnce`;
  `NullSafeHopChainTests.TwoRefStructHopsInOneExpressionGetDistinctLocals` (`31e618d`; a constant name
  leaves 567 integration tests green and breaks a real template with `CS0128`).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~NullSafeHopChainTests`.

### F-083 — A path ending *on* a ref struct emitted `CS0030`

- **status:** FIXED
- **severity:** 2
- **found:** cycle 8
- **symptom:** every consumer of a path's value boxes it, and a ref struct cannot be boxed.
  Pre-existing.
- **fixed by:** `726031b` — the shape degrades, and the engine's own refusal (`HED0005`, at compile
  time) is what the reader sees. An id and a position a host can report beat a raw `CS0030` against a
  `.heddle` file.
- **pinned by:** `NullSafeHopChainTests.APathEndingOnARefStructDegradesInsteadOfBreakingTheBuild`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~APathEndingOnARefStruct`.
- **notes:** **a first draft of the record claimed the engine can render this template. It cannot** —
  measured afterwards: the engine refuses with `HED0005` at compile time. Reading *through* a ref
  struct to a member of its own is unaffected; what leaves that path is an `int`.

### F-084 — Generated arithmetic inherited the consumer's overflow-checking setting

- **status:** FIXED
- **severity:** 1 (the same template rendered `1410065408` on one tier and threw `OverflowException`
  on the other, decided by an MSBuild property the template knows nothing about)
- **found:** cycle 8
- **root cause:** the engine's arithmetic is built from the unchecked expression-tree factories, so it
  wraps whatever the host sets; bare emitted operators do not.
- **fixed by:** `726031b` — emitted expressions wrapped in `unchecked`.
- **pinned by:** `CheckedOverflowContextTests.RuntimeOverflowWrapsWhicheverWayTheHostCompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CheckedOverflowContextTests`.
- **notes:** **the commit said the class was closed and it was closed on one path** — the C# tier
  pastes the author's expression through a different method and was left bare (F-095, cycle 9).
  Constant overflow is a separate question and still degrades.

### F-085 — A failed render permanently poisoned a compiled template

- **status:** FIXED
- **severity:** 3 (a hundred bad requests retired the template on that thread for good — for healthy
  requests too, with a recursion error describing nothing that happened)
- **found:** cycle 8
- **root cause:** the definition recursion counter was incremented and decremented without a
  `try/finally`, so any throw in between kept the increment. Both tiers. **Nothing in the repo
  asserted the recursion guard at all.**
- **fixed by:** `726031b`.
- **pinned by:** `DefinitionRecursionTests.AFailedRenderDoesNotSpendTheRecursionBudget`,
  `.ASelfCallingDefinitionIsStoppedRatherThanRunningOutOfStack`;
  `PrecompiledDefinitionRecursionTests.AFailedValueProducingRenderDoesNotSpendThePrecompiledRecursionBudget`
  (the `ProcessData` half added by `31e618d`).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DefinitionRecursionTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~PrecompiledDefinitionRecursionTests`.

### F-086 — A cache whose key can never repeat

- **status:** FIXED
- **severity:** 5 (it retained every generated source string for the life of the process)
- **found:** cycle 8
- **symptom:** a C#-tier compilation cache keyed on the generated source, which names a class after a
  fresh `Guid` per compile. Measured: eight compiles of three distinct expressions, eight adds, zero
  hits. A hit would have been worse than a miss — the entry class is looked up by the *current*
  context's guid, which another context's assembly does not contain.
- **fixed by:** `726031b`.
- **regression check:** none identified beyond reading the key.

### F-087 — An assembly that lost a name collision was retired for good

- **status:** FIXED
- **severity:** 3
- **found:** cycle 8
- **symptom:** it was marked classified before the name was claimed, so the loser was skipped on every
  later pass and stayed invisible to type resolution even after the name was freed.
- **fixed by:** `726031b`.
- **pinned by:** `AssemblyRegistrationTests.AFreedNameIsRetakenByTheLoadedAssemblyThatLostIt` (`31e618d`).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~AFreedNameIsRetaken`.

### F-088 — A cached *success* was never invalidated

- **status:** FIXED
- **severity:** 3
- **found:** cycle 8
- **symptom:** the reasoning was that nothing a later registration adds can take a type away. It can:
  a new assembly can make a name ambiguous (`CS0104`) or introduce a better overload candidate.
- **fixed by:** `726031b` — successes are generation-checked, and the cache drops a spent entry rather
  than merely refusing to read it.
- **pinned by:** `PreparseCacheGenerationTests.ASpentEntryIsDroppedRatherThanKeptAndIgnored`,
  `.AnEntryAtTheGenerationTheMapHoldsIsServed`, `.AnEntryIsNotServedOnceTheAssemblySetHasMovedOn`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PreparseCacheGenerationTests`.
- **notes:** sibling of F-034 (the *failure* half), five cycles earlier.

### F-089 — A branch whose gate is statically decided

- **status:** FIXED (deleted)
- **severity:** 6
- **found:** cycle 8
- **symptom:** the unreachable half of `WriteDynamicPath` wrote exactly the `?.` chain whose
  short-circuit the typed writer had just been fixed for (F-072). Both sides of its gate are constants
  this assembly reads.
- **fixed by:** `726031b`.
- **regression check:** none needed.

### F-090 — Five properties claimed by a commit message and pinned by nothing

- **status:** FIXED (three of five; two deferred, then closed in cycle 8's follow-up)
- **severity:** 6
- **found:** cycle 8, by mutation
- **symptom:** each was demonstrated by mutating production code and watching every suite stay green:
  - negative shift-count folding (`if (places < 0) places = 0;` reddened 0 of 587 tests while
    producing a real host-build break);
  - shift results were never byte-compared anywhere at all;
  - `ImportIdentity`'s catch was unreachable by any test;
  - the `ResolveOnly` exemption was satisfied by `Assert.ThrowsAny<Exception>` — including the harness
    itself falling over;
  - the `Assembly.LoadFrom` in the differential harness was unpinned.
- **fixed by:** `726031b` for the first four; the last became F-094.
- **pinned by:** `ConstantArithmeticDifferentialTests.AShiftProducesTheSameNumberOnBothTiers`;
  `ImportCycleTests.AnImportPathThatCannotBeCanonicalisedStillParses`;
  `CorpusRenderParityTests.AnEntryDeclaredResolveOnlyGenuinelyDoesNotRender` (tightened).
- **regression check:** the three suites above.

### F-091 — An internal member on a referenced model type errored where the engine renders

- **status:** FIXED
- **severity:** 3
- **found:** cycle 8 (recorded as "not fixed" — a policy decision with a large blast radius), closed
  immediately after
- **symptom:** the engine renders such a member; Roslyn's default `MetadataImportOptions.Public` makes
  it *absent* from the symbol model rather than inaccessible, so it is indistinguishable from a typo
  and drew `HED7008` at error severity — breaking the build over a template the engine renders.
- **fixed by:** `f872b79` — a second view of the *same references* opened with
  `MetadataImportOptions.All`, built lazily and only on the path about to report a failure, held
  weakly against the compilation it describes. A member the engine's visibility policy accepts there
  and this compilation cannot see is *hidden*, not missing: internal member → degrade under `HED7030`
  (warning); **private** member → still `HED7008`, because the engine rejects private too and the
  tiers agree; typo → still `HED7008`. Source receivers skip the probe.
- **pinned by:** `InaccessibleModelSymbolTests.AnInternalMemberOnAReferencedModelDegradesWithAWarningRatherThanFailingTheBuild`,
  `.APrivateMemberStaysAnError`, `.AMisspelledMemberOnAReferencedModelStaysAnError`,
  `.APublicMemberOnTheSameReferencedModelStillPrecompiles`;
  `MetadataAccessibilityProbeTests.*` (`31e618d`), including
  `.AGrantedInternalMemberResolvesInsteadOfDegrading` for `[InternalsVisibleTo]`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~InaccessibleModelSymbolTests`
  and `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~MetadataAccessibilityProbeTests`.
- **notes:** the obvious fix — degrade whenever the receiver came from metadata — was rejected: models
  normally live in referenced assemblies, so that is precisely where `HED7008` earns its keep.

### F-092 — Internal *types* are imported from metadata regardless of accessibility

- **status:** FIXED
- **severity:** 2 (a wall of `CS0122` against `.g.cs`, with no Heddle id and no `.heddle` position)
- **found:** cycle 8's follow-up — **assumed to behave like F-091 and it does not**
- **symptom:** no diagnostic fired at all; the emitter resolved the type, wrote its fully-qualified
  name into a generated cast, and the consumer's build died. Worse than the reported symptom and
  reachable by exactly the same model.
- **fixed by:** `f872b79` — same probe as F-091, applied to the type.
- **pinned by:** `InaccessibleModelSymbolTests.AnInternalReferencedModelTypeDegradesRatherThanEmittingACastTheConsumerCannotCompile`;
  `.AnInternalModelTypeWithAPublicInheritedMemberDegrades...` (`4680907`, the fixture that separates
  the type rule from the member rule — see F-103).
- **regression check:** as F-091.
- **notes:** the lesson the record draws: **when a report names one half of a mechanism, measure the
  other half rather than assuming it behaves the same way.**

### F-093 — The epoch was a second counter for something one counter already knew

- **status:** SUPERSEDED (removed)
- **severity:** 6
- **found:** cycle 8
- **symptom:** the epoch introduced by F-074 was subsumed by the same round's generation-strict
  staleness rule. Reviewers could also reverse two orderings without reddening anything.
- **fixed by:** `f872b79` — removed. The cache holds one assembly generation at a time, under one
  monitor: a result computed against a superseded set is refused admission, one from a newer set
  retires the map on the way in, and a reader asking at a generation the map is not holding gets
  nothing. `GetApplicationReferences` hands out the reference set and its generation from one
  lock-held read; the unregistration is a single expression. **One correction on top:** a reader whose
  generation is *older* than the map's must not retire it, or a straggler arriving after an
  unregistration throws away every entry the current set just built.
- **pinned by:** `PreparseCacheGenerationTests.AResultComputedAgainstASupersededSetIsRefusedAdmission`,
  `.AResultFromANewerSetRetiresTheOlderEntries`, `.RetargetingDropsEntriesRatherThanLeavingThemUnread`,
  `.AReaderFromASupersededSetDoesNotDragTheMapBackToIt`,
  `.TheSuiteLeavesTheCacheUsableAtTheLiveGeneration`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PreparseCacheGenerationTests`.
- **notes:** cycle 8 recorded the ordering claims as **unpinnable**: reversing either ordering left
  every suite green, and a test that races to observe the difference passes by luck when it is wrong.
  The fix's merit is that the reversible orderings no longer exist to be reversed. The note lives in
  `PreparseCacheGenerationTests` and `AssemblyRegistrationTests` in plain words.

### F-094 — A harness call that suppressed the property it should have tested

- **status:** FIXED
- **severity:** 6
- **found:** cycle 8 (one reviewer argued it hides a real engine property rather than testing it)
- **symptom:** the two tiers do not bind a model type from the same world — the generator resolves
  over the compilation's *references*, the engine over the assemblies the process has actually
  *loaded*. So `@model(){{X}}` can bind at build time and fail at first render. The harness's
  `Assembly.LoadFrom` was quietly suppressing the only place that was visible.
- **fixed by:** `f872b79` — the call stays (a differential test asks whether two tiers emit the same
  bytes from the same inputs, and "the model assembly is loaded" is an input), but the property is
  asserted head-on against a GUID-named assembly built at test time, and a second test pins the
  suppression itself.
- **pinned by:** `ModelResolutionLoadOrderTests.AModelTypeBindsAtBuildTimeFromAReferenceAndAtRunTimeOnlyOnceItsAssemblyIsLoaded`,
  `.TheHarnessLoadsExtraReferencesSoDifferentialSuitesDoNotDependOnTestOrder`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelResolutionLoadOrderTests`.

### F-095 — The overflow-context fix covered one of two expression paths

- **status:** FIXED
- **severity:** 1
- **found:** cycle 9, both reviewers leading with it
- **symptom:** the previous round wrapped the native expression writer; the C# tier pastes the
  author's expression through a different method and was left bare, so a template still rendered a
  wrapped number on one tier and threw `OverflowException` on the other for anyone using
  `ExpressionMode.FullCSharp`.
- **fixed by:** `31e618d` — **both sides wrapped, engine included.** Wrapping only the generator would
  have traded one divergence for another: C# checks a *constant* expression whatever the compilation
  says, so `@(@100000 * 100000 * 100000)` would have moved from "both tiers refuse" to "precompiled
  renders, engine refuses".
- **pinned by:** `CheckedOverflowContextTests.EmbeddedCSharpOverflowWrapsWhicheverWayTheHostCompiles`,
  `.AConstantOverflowInEmbeddedCSharpWrapsOnBothTiers`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CheckedOverflowContextTests`.
- **notes:** **this changed the engine.** A constant overflow in embedded C# used to be a compile
  error and now wraps — accepted because the engine was already inconsistent with itself (its native
  tier builds `Expression.Multiply`, which is unchecked). Written up in the language reference with
  `checked(…)` as the escape hatch.

### F-096 — A visibility check that only detects one of the two reference kinds

- **status:** FIXED
- **severity:** 2 (`CS0122` in `.g.cs` for a project-to-project reference)
- **found:** cycle 9
- **symptom:** `HED7030` detected an internal member by its *absence* from the symbol model, which is
  true only of a metadata reference. A Roslyn workspace hands the generator a `CompilationReference`
  for a project reference, where the member is present — so it resolved and was emitted.
- **root cause:** detection-by-absence and detection-by-accessibility are two halves and only one had
  been built.
- **fixed by:** `31e618d`.
- **pinned by:** `InaccessibleModelSymbolTests.AnInternalMemberOnAProjectReferencedModelDegradesRatherThanEmittingACS0122`,
  `.APublicMemberOnAProjectReferencedModelStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~InaccessibleModelSymbolTests`.

### F-097 — An accessibility gate scoped to one of four positions that spell a type

- **status:** FIXED
- **severity:** 2
- **found:** cycle 9
- **symptom:** the gate guarded `@model()` alone. Definition, slot and prop model types each resolved
  a type and wrote its name into a cast without asking whether this assembly may name it.
- **fixed by:** `31e618d`.
- **pinned by:** `InaccessibleModelSymbolTests.AnInternalTypeOnADefinitionDegradesRatherThanEmittingACastTheConsumerCannotCompile`,
  `.AnInternalSlotTypeDegradesRatherThanEmittingACastTheConsumerCannotCompile`,
  `.AnInternalPropTypeDegradesAudiblyRatherThanSilently`.
- **regression check:** as F-096.
- **notes:** one of three "guard narrower than its defect" findings in the same cycle (with F-096 and
  F-098). See the defect-class summary — this class was finally answered by enumeration in cycle 11.

### F-098 — A ref-struct check that guarded hops but not the model

- **status:** FIXED
- **severity:** 2
- **found:** cycle 9
- **symptom:** `@model(){{System.ReadOnlySpan<char>}}` emitted a signature that cannot compile, while
  the engine accepts the template and reports an ordinary catchable error at render.
- **fixed by:** `31e618d`.
- **pinned by:** `UnnameableModelSymbolTests.ARefStructModelDegradesInsteadOfBreakingTheBuild`,
  `.ARefStructDefinitionModelDegradesInsteadOfBreakingTheBuild`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableModelSymbolTests`.

### F-099 — A probe that reverted to the original error on an ambiguous name

- **status:** FIXED
- **severity:** 3 (`HED7008` at error severity over a template the engine renders)
- **found:** cycle 9
- **symptom:** `GetTypeByMetadataName` returns null when a name is found in more than one reference,
  and the caller kept the answer it already had.
- **fixed by:** `31e618d` — the plural `GetTypesByMetadataName` degrades instead. The same probe was
  also being built on *every* member miss, including from the estimator, which never reports — in an
  editor that is one extra compilation per keystroke through a half-typed member name. It now sits
  behind the reporting decision, and path resolution is memoised.
- **pinned by:** `MetadataAccessibilityProbeTests.AnAmbiguousReceiverTypeNameStillDegrades`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~MetadataAccessibilityProbeTests`.
- **notes:** the memo added here was itself wrong — F-102.

### F-100 — Six properties that nothing pinned, two of them one day old

- **status:** FIXED (four); two are unpinnable and recorded as such
- **severity:** 6
- **found:** cycle 9, by mutating production code and watching every suite stay green
- **symptom:** hop-local name uniqueness; the `ProcessData` half of the definition recursion guard;
  both `PreparseCache` retire rules; and all three `AssemblyHelper` orderings. One of the newest tests
  was also retargeting the process-global cache into a band it never left, silently disabling the C#
  tier cache for every test that ran after it.
- **fixed by:** `31e618d`.
- **pinned by:** `NullSafeHopChainTests.TwoRefStructHopsInOneExpressionGetDistinctLocals`;
  `PrecompiledDefinitionRecursionTests.AFailedValueProducingRenderDoesNotSpendTheRecursionBudgetEither`;
  `PreparseCacheGenerationTests.*`; `AssemblyRegistrationTests.AFreedNameIsRetakenByTheLoadedAssemblyThatLostIt`.
- **regression check:** the suites above.
- **notes:** **two of the `AssemblyHelper` orderings cannot be pinned** — they are claims about what no
  concurrent caller can observe, and a racing test passes by luck when the code is wrong. That is
  written in `AssemblyRegistrationTests` in plain words rather than covered by a test that would imply
  more. Do not "fix" it with a racing test.

### F-101 — A hop whose property *type* is internal still emits a name the consumer cannot compile

- **status:** KNOWN-OPEN
- **severity:** 2
- **found:** cycle 9; declined deliberately
- **symptom:** the emitted code names the property's type; if that type is internal to another
  assembly the consumer's build fails. Checking it where the member check sits would falsely degrade
  the ordinary `m?.Inner?.Name` shape, where no type name is ever written.
- **fixed by:** — recorded rather than half-fixed. Doing it properly needs a form-aware check at the
  point of emission.
- **pinned by:** nothing.
- **regression check:** none.
- **notes:** **status uncertain.** Later cycles rebuilt type nameability wholesale (F-110, F-119) and
  reworked property-type handling (F-122); the record never says whether either subsumed this case. If
  you are about to report it, first check whether `Classify`/`ClassifyTypeName` is consulted at the
  point where a hop's property type is spelled.

### F-102 — A memo keyed on a display string

- **status:** FIXED
- **severity:** 1 (latent)
- **found:** cycle 10 — **four of that cycle's eight findings were introduced by the previous commit**
- **symptom:** two references each declaring `Dup.Thing` produce one key, so the second walk was
  answered with the first's resolved type — which decides the null-safety form, the numeric widening,
  the formatter and the member name written into the file. The same key also made `["A.B"]` and
  `["A","B"]` one question.
- **root cause:** the memo added by F-099's fix keyed on the receiver's fully-qualified name plus the
  segments joined by a dot.
- **fixed by:** `4680907` — the key is the receiver **symbol** under `SymbolEqualityComparer` and a
  separator no identifier can carry.
- **pinned by:** `PathMemoizationTests.TwoTypesSharingAFullyQualifiedNameGetTheirOwnResolutions`,
  `.ASegmentCarryingADotIsNotTheSameQuestionAsTwoSegments`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~PathMemoizationTests`.
- **notes:** **nothing end-to-end reaches it today**, which is why it is pinned at the resolver rather
  than through a template, and the test says so. The lesson was reused one cycle later, deliberately,
  for the body-cache key (F-113) — the first time the loop reused one of its own findings.

### F-103 — A guard whose only test the same commit ate

- **status:** FIXED
- **severity:** 6
- **found:** cycle 10
- **symptom:** widening the accessibility gate from `@model()` to every type the emitter spells
  (F-097) left the `@model()` arm itself unpinned: the one test covering it survived on the *member*
  rule the same commit added, because its fixture's member is declared on the internal type.
- **fixed by:** `4680907` — a model whose member sits on a public base is the shape that separates
  them; removing the `@model()` call now reddens with six `CS0122`, measured.
- **pinned by:** `InaccessibleModelSymbolTests.AnInternalModelTypeWithAPublicInheritedMemberDegradesRatherThanEmittingACastTheConsumerCannotCompile`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~AnInternalModelTypeWithAPublicInheritedMember`.
- **notes:** class worth naming: **widening a rule can silently orphan the narrow rule's only pin**,
  because the new rule answers the old fixture.

### F-104 — `[Obsolete(…, error: true)]` on a model type or member breaks the consumer's build

- **status:** FIXED
- **severity:** 2
- **found:** cycle 10
- **symptom:** reflection ignores `[Obsolete]` entirely, so the engine renders; every generated
  mention of the name is a `CS0619` against a `.g.cs` the consumer did not write, attributed to a
  `.heddle` file and carrying no Heddle id.
- **fixed by:** `4680907` — same id and same degrade as the accessibility case (`HED7030`), on the
  type and on the member; the id's message broadened to cover both causes.
- **pinned by:** `UnnameableModelSymbolTests.AnErrorObsoleteModelTypeDegradesRatherThanEmittingANameTheConsumerCannotCompile`,
  `.AnErrorObsoleteMemberDegradesRatherThanEmittingAReadTheConsumerCannotCompile`,
  `.AWarningLevelObsoleteModelStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableModelSymbolTests`.
- **notes:** the **warning** form deliberately does not degrade — the generated file's blanket
  `#pragma warning disable` keeps `CS0618` out of the build, and degrading would take every deprecated
  model in a codebase off the precompiled tier. The same rule was missing for *exported functions*
  until cycle 18 (F-157), and for an enclosing type's arguments until cycle 13 (F-125).

### F-105 — A static class as `@model()`

- **status:** FIXED
- **severity:** 2 (`CS0721`: a static type cannot be a parameter, and the entry point takes the model
  as one)
- **found:** cycle 10
- **fixed by:** `4680907` — degrades; the engine declares no such parameter and renders the template.
- **pinned by:** `UnnameableModelSymbolTests.AStaticModelTypeDegradesRatherThanEmittingAParameterTheConsumerCannotCompile`.
- **regression check:** as F-104.
- **notes:** the direct sibling of the previous cycle's ref-struct arm (F-098), for the other type
  kind that cannot be a parameter. **Third cycle running that this guard was widened by one type kind
  at a time** — cycle 11 answered the question properly (F-110).

### F-106 — A slot-value type check the engine performs and the emitter did not

- **status:** FIXED
- **severity:** 3 and 1
- **found:** cycle 10
- **symptom:** the engine type-checks every `@out` value against the declared slot type and refuses
  the template (`HED5014`); the emitter checked only that it was inside a slot definition, so the same
  template precompiled and rendered — or threw `InvalidCastException` from the caller-content cast,
  depending on whether the caller's body happened to read a member.
- **fixed by:** `4680907` — the emitter runs the engine's own conversion table with the same
  `allowBoxToObject: false` the slot caller passes, wherever it can type the value.
- **pinned by:** `SlotValueTypeTests.ASlotValueOfAnUnrelatedTypeDegradesInsteadOfRenderingWhatTheEngineRefuses`,
  `.ASlotValueOfAnUnrelatedTypeDegradesEvenWhenTheCallerContentReadsAMember`,
  `.ASlotValueThatWouldHaveToBeBoxedDegrades`, `.AnAssignableSlotValueStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SlotValueTypeTests`.
- **notes:** where it cannot type the value it degrades in one shape and not the other, deliberately:
  a `:: dynamic` slot definition degraded (later refined to per-call-site — F-112, F-117), while an
  `@out(this)` inside an `@list` body kept precompiling (revisited in cycle 14 — F-135).

### F-107 — Two invalidation calls inside the branch where the case they handle cannot occur

- **status:** FIXED (removed)
- **severity:** 6 (plus a full re-classification pass per `Configure` that could not reach a different
  answer)
- **found:** cycle 10
- **symptom:** `InvalidateObservation` exists for a loaded assembly that **lost** a name collision —
  exactly the case where `TryAdd` fails — and two of its three call sites sat inside the success
  branch.
- **fixed by:** `4680907` — both removed; the one in `UnregisterModelAssemblies`, where a name is
  actually freed, is real and pinned.
- **pinned by:** `AssemblyRegistrationTests.AFreedNameIsRetakenByTheLoadedAssemblyThatLostIt`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter Name~AFreedNameIsRetaken`.

### F-108 — Test suites that wrote 58 probe assemblies and deleted none

- **status:** FIXED
- **severity:** 6 (test hygiene, with a real cross-suite hazard)
- **found:** cycle 10
- **symptom:** the registration and C#-tier suites write a GUID-named `.dll` beside the test binaries
  — they have to, for "this has never been loaded" to be a fact — and deleted none of them. They
  cannot be deleted when the writing test ends either: the file is loaded by then, and the C# tier
  builds its reference set from the locations of every observed assembly, so removing one mid-run
  leaves an unrelated suite short of a reference (measured: it reddens
  `NoAssemblyIsDroppedFromTheReferenceSet`). The collision test's `finally` also called the
  process-global unregister unconditionally, which would clear another suite's registrations.
- **fixed by:** `4680907` — deleted at process exit, best effort (`ProbeAssemblyFiles`); the `finally`
  now runs only on the paths that left its own registration in place.
- **regression check:** `ls src/Heddle.Tests/bin/**/ *.dll` for GUID-named leftovers after a run.

### F-109 — `ThreadLocal` per definition, reported as an unbounded slot table

- **status:** NOT-A-DEFECT
- **severity:** —
- **found:** cycle 10 (reported), measured and dismissed in the same cycle
- **symptom (as reported):** `DefinitionBaseExtension` holds one `ThreadLocal<int>` per definition per
  compiled template and never disposes it.
- **measurement:** `ThreadLocal<T>` carries a finalizer that returns the id, so ids recycle without
  `Dispose`. 200,000 definition instances over 10,000 compile-and-render cycles left the per-thread
  slot array at **256** entries, flat from the first round; 10,000 cycles of a single-definition
  template left it at 1024. The array tracks the peak number of instances awaiting finalization. A
  tight allocation loop that outruns the finalizer does grow it (100,000 abandoned instances reached
  131,072 slots) — a shape no template workload has.
- **fixed by:** — nothing changed.
- **regression check:** re-run the loop only if the disposal pattern changes.

### F-110 — Type nameability answered by enumerating kinds instead of asking one question

- **status:** FIXED
- **severity:** 2
- **found:** cycle 11 — **three commits in a row had widened the same guard by one type kind**
  (static classes, then ref structs, then error-obsolete types), and this cycle brought `void`,
  unbound generics, pointers, error-obsolete *containing* types and error-obsolete *property* types.
- **symptom:** five separate reported findings that were one incomplete answer to a single question:
  *can generated code in the consumer's assembly name this type, take it as a parameter, and cast
  `object` to it?*
- **fixed by:** `4f299ce` — answered in two predicates: `ClassifyTypeName` (may a name be written where
  a value of it lives) and `ClassifyModelType` (adds ref-struct-ness, the one restriction that applies
  only to a value that has to box). That split is what lets `Buf.Length` stay precompiled while
  `Span<char>` as a model degrades. Every `TypeKind` has a recorded verdict, `void` is caught by
  `SpecialType` rather than by kind (it is a `Struct`), and `ContainsTypeParameter` walks array and
  pointer elements, type arguments *and* containing types.
- **pinned by:** `TypeNameVerdictTests` (a 19-row table at first; rebuilt four times since — F-119,
  F-126, F-133, F-139, F-146); `UnnameableModelSymbolTests.AModelTheConsumersCompilerWouldRejectDegrades`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`.
- **notes:** **the enumeration was still not complete** — cycle 12 found it recursed into array
  elements but not type arguments (F-119), and cycle 13 found the containing-type walk deleted here
  was load-bearing (F-125).

### F-111 — An `@model` text that resolves to no symbol was emitted verbatim

- **status:** FIXED, then re-opened and fixed again in cycle 13 (F-127)
- **severity:** 2
- **found:** cycle 11
- **symptom:** the spelling was written straight into the entry point's parameter type, which is why
  `System.Int32*` never reached any type-kind guard.
- **fixed by:** `4f299ce` — fixed where the symbol is resolved rather than where the kinds are listed.
- **pinned by:** `UnnameableModelSymbolTests.ANameGeneratedCodeCannotWriteIsRefusedWithoutADiagnosticToShowForIt`.
- **regression check:** see F-127, which is the complete version.

### F-112 — A degrade is a cost, and a blanket refusal charges it to working templates

- **status:** FIXED
- **severity:** 4
- **found:** cycle 11
- **symptom:** the `:: dynamic` slot refusal added the previous cycle was over-broad: a reusable
  wrapper whose `@out` values are all assignable lost the precompiled tier silently, with both tiers
  producing identical bytes. **Nothing reddened, because only two corpus templates declare a slot and
  both use typed body models.**
- **fixed by:** `4f299ce` — the refusal is per call site, against the static type the caller actually
  passes: strictly more precompilation than before, and it still refuses only the case that genuinely
  cannot be decided.
- **pinned by:** `SlotValueTypeTests.ASlotDefinitionWithADynamicBodyModelStillPrecompilesWhenTheCallerValueFits`,
  `.ASlotDefinitionWithADynamicBodyModelDegradesWhenTheCallerValueCannotBeTyped`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SlotValueTypeTests`.
- **notes:** the per-call-site rule **rested on an assumption about the engine that was false** —
  F-117. Read that entry before trusting this one.

### F-113 — A body cache keyed without the value that decides the body

- **status:** FIXED
- **severity:** 1
- **found:** cycle 11, while fixing F-112
- **symptom:** the definition body is cached per definition and fills, so with two call sites into one
  `:: dynamic` definition the first one's verdict stood for the second.
- **fixed by:** `4f299ce` — the key gained the slot-value model, as an ordinal id under
  `SymbolEqualityComparer`, **not a display string, because two distinct types can share a fully
  qualified name** (the lesson from F-102, two cycles earlier).
- **pinned by:** `SlotValueTypeTests.TheSecondCallSiteIntoADynamicSlotDefinitionIsCheckedOnItsOwnValue`;
  later `DynamicDefinitionBodyTests.TheSecondCallSiteIntoADynamicDefinitionIsTypedOnItsOwnValue`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~SecondCallSiteInto`.

### F-114 — A reported repro that does not compile

- **status:** NOT-A-DEFECT (as reported) — the underlying defect is real and is covered by F-110
- **severity:** —
- **found:** cycle 11
- **symptom:** the error-obsolete property-type case was reported with `public Money Balance` where
  `Money` is error-obsolete — which is itself `CS0619`, so the model could never have been built.
- **measurement:** reaching the defect needs the property to carry its own *warning*-level
  `[Obsolete]` (an obsolete context suppresses the diagnostic on the types it mentions), or a model
  library not rebuilt since the type was deprecated.
- **notes:** recorded because **the report was accepted on its reasoning and the reasoning was checked
  against the compiler rather than against the reviewer.** Do the same with any repro that depends on
  a fixture compiling.

### F-115 — An unmeasured micro-optimisation, reverted

- **status:** SUPERSEDED (reverted)
- **severity:** —
- **found:** cycle 11
- **symptom:** marking a registered assembly as already classified to save a `GetName()` per
  observation pass came in alongside the cycle's fixes, correctly guarded and honestly flagged as
  untested.
- **fixed by:** `4f299ce` — removed. Nobody measured the cost it addresses, and the file it touches
  had had three defects that session.
- **notes:** the standing rule this establishes: **the same discipline that left `ThreadLocal` alone
  after measuring (F-109) forbids changing something without measuring.**

### F-116 — Literal slot values skipped, and a boxing arm with no test

- **status:** FIXED
- **severity:** 6
- **found:** cycle 11
- **symptom:** literal slot values were skipped rather than typed from their decoded value; the
  prop-default boxing arm had never had a test.
- **fixed by:** `4f299ce`.
- **pinned by:** `NullableDefaultDifferentialTests.AValueTypeDefaultOnAnObjectPropPrecompilesAndReproducesTheBoxedType`;
  `SlotValueTypeTests.ALiteralSlotValueOfAnUnrelatedTypeDegrades`,
  `.AnAssignableLiteralSlotValueStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SlotValueTypeTests`.

### F-117 — A rule mirrored from an assumption about the engine rather than from the engine

- **status:** FIXED
- **severity:** 3 (two of six call forms precompiled templates the engine refuses outright)
- **found:** cycle 12, both reviewers
- **symptom:** the per-call-site `:: dynamic` rule (F-112) rested on the assumption that the engine
  compiles such a body against the value the caller passes. It does not: `CompileModelAccessor` takes
  a dynamic exit before resolving anything whenever the callee declares `:: dynamic`, so a bare call
  or a member-path argument gives the body `ExType.Dynamic` and every `@out(this)` inside it is
  `HED5014`.
- **fixed by:** `6d013eb` — **the fix mirrors the measured engine rather than either reviewer's
  proposal.** One proposed a matrix, one a conservative `this`-only rule; measuring showed the matrix
  right and the conservative rule wrong in the other direction, because a *caller's prop read* keeps
  its static type through a `:: dynamic` definition (the prop-read path runs before the dynamic check).
  `dynamic` is carried in as the body's model, so an `@out` of a literal or of the definition's own
  prop stays typed and stays precompiled; only an `@out` that reads the model is refused.
- **pinned by:** `SlotValueTypeTests.ACallFormThatGivesTheBodyADynamicModelDegrades`,
  `.ADynamicModelStillPrecompilesWhenTheOutValueNeverReadsIt`,
  `.ACallerPropReadKeepsItsStaticTypeThroughADynamicDefinition`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SlotValueTypeTests`.
- **notes:** cycle 13 then measured the *whole* matrix and found the rule wider still (F-124), and
  cycle 18 found it was keyed on the wrong predicate entirely (F-156). This is the most-revised rule
  in the series — **measure the engine over a matrix before changing anything here.**

### F-118 — Two tests that passed for the wrong reason

- **status:** FIXED
- **severity:** 6
- **found:** cycle 12
- **symptom:** `AssertEngineRefuses` grepped for the id, and `HED5014` has two messages ("not
  assignable" and "must have a static type"), so the test could not tell which rule it had reproduced.
  And both tests were `ExpectDegrade`-only, **which cannot fail against the parent commit, where
  everything degraded.**
- **fixed by:** `6d013eb` — rebuilt to name the exact type pair and to carry a precompiling half;
  checked by re-inserting the parent's blanket refusal and watching them fail on the positive half.
- **regression check:** re-insert a blanket refusal in the slot-value path and confirm the positive
  rows redden.
- **notes:** **the general rule the series keeps re-learning: a test that only asserts a degrade
  cannot distinguish a rule from a blanket refusal. Every degrade assertion needs a paired case that
  must still precompile.** Earlier instances: F-036, F-041. Later: F-132.

### F-119 — "Complete" was not complete: the verdicts recursed into elements but not type arguments

- **status:** FIXED
- **severity:** 2
- **found:** cycle 12
- **symptom:** nine spellings — `Span<char>[]`, `List<Math>`, `Nullable<Span<char>>`,
  `(Span<char>, int)` and others — still emitted a `.g.cs` the consumer cannot compile.
- **fixed by:** `6d013eb` — one `Classify` serves both entry points, and the only verdict that varies
  by position is ref-struct-ness (allowed for a bare hop type; refused as a model, an array element or
  a type argument).
- **pinned by:** `TypeNameVerdictTests.ARefStructIsWritableOnItsOwnAndRefusedInEveryNestedPosition`,
  `.AnOrdinaryConstructedTypeStaysNameableInEveryPosition`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`.
- **notes:** **of some nineteen verdict arms only six were pinned; eight could be deleted with every
  generator test green.** All nineteen were mutated one at a time; sixteen went red, and the three
  that did not were deleted as duplicate walks — **one of those deletions was wrong** (F-125).

### F-120 — A grammar prelude the runtime never had

- **status:** FIXED
- **severity:** 3
- **found:** cycle 12
- **symptom:** `@model(){{int?}}` bound a strategy for a template the engine cannot resolve at all: a
  `?`-suffix prelude existed in the generator and in no shared grammar. Checking all four positions
  rather than the brief's assurance showed the runtime accepts `?` **nowhere** — and that `:: T?` on a
  definition does not "work" either, it silently drops the `?` and compiles the body against the
  unlifted type.
- **fixed by:** `6d013eb` — prelude deleted; `?` now reaches the same `HED7007` the engine's refusal
  mirrors. Rows added to both lockstep corpora, whose stated job was this parity and which had none.
- **pinned by:** `ModelTypeSpellingTests.ANullableSuffixOnTheModelDirectiveIsRefusedOnBothTiers`,
  `.TheNullableSpellingBothGrammarsDoHaveStillPrecompiles`; `TypeSpellingSymbolLockstepTests`,
  `TypeSpellingLockstepTests`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelTypeSpellingTests`.

### F-121 — The null literal has a type on one tier and not the other

- **status:** FIXED
- **severity:** 3
- **found:** cycle 12
- **symptom:** `null` types as `System.Object` to the engine and as "cannot say" to the emitter, so
  `@out(null)` into a typed slot precompiled what the engine refuses.
- **fixed by:** `6d013eb`.
- **pinned by:** `SlotValueTypeTests.ANullSlotValueDegradesBecauseTheEngineTypesItAsObject`,
  `.ANullSlotValueStillPrecompilesForAnObjectSlot`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter Name~ANullSlotValue`.
- **notes:** typing the null literal is what made the *next* cycle's divergence reachable — a `null`
  call-site value produced a page the engine will not compile at all (F-124).

### F-122 — An author-facing warning raised for a fault no author can fix

- **status:** FIXED
- **severity:** off-scale (diagnostic quality)
- **found:** cycle 12
- **symptom:** a property whose type is merely *unusable* — a pointer, `void` — raised `HED7030`,
  which is reserved for faults an author can act on.
- **fixed by:** `6d013eb` — the fault is carried rather than collapsed into a boolean.
- **pinned by:** `UnnameableModelSymbolTests.AMemberWhoseTypeNoGeneratedCodeCanHoldDegradesWithoutAnAuthorFacingWarning`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableModelSymbolTests`.

### F-123 — A dotted `@model` spelling whose last segment happens to name a real type

- **status:** FIXED
- **severity:** 2
- **found:** cycle 12
- **symptom:** it emitted raw text with no diagnostic at all.
- **fixed by:** `6d013eb` — the existence check is a dot-bounded suffix match: generous to a bare
  name, strict about namespace segments the author actually wrote.
- **pinned by:** `ModelTypeSpellingTests.ADottedModelSpellingThatBindsNowhereIsReported`,
  `.ADottedModelSpellingThatBindsIsLeftAlone`,
  `.ADottedModelSpellingThatNamesTheTailOfARealNamespaceIsNotCalledATypo`.
- **regression check:** as F-120.
- **notes:** the dot-boundary rule itself was then found unpinned (F-129), and the gate's
  global-namespace stop after that (F-134).

### F-124 — `:: dynamic` on a definition does not mean "untyped body"

- **status:** FIXED (for the call forms in the probed matrix; completed by F-130)
- **severity:** 1 and 3
- **found:** cycle 13
- **symptom:** the engine compiles the body **once per call site, against the static type of the value
  that call site passes** — `this` gives the caller's model, a literal gives the literal's type, `null`
  gives `System.Object`; only a bare call and a member path reach the accessor's dynamic exit. A body
  member the type does not carry is `HED0001` at compile time. The emitter computed that model and
  used it only for type-checking the body's `@out` values, leaving the body dynamic — so in every cell
  where the model was static and the member missed, generated code threw `RuntimeBinderException` at
  render where the engine had refused the template, and **where the value was `null` the dynamic read
  yielded empty and the page rendered what the engine will not compile at all.**
- **fixed by:** `27dd357` — the body is built in a *typed* context off that same model, so the existing
  member-path machinery answers with `HED7008` and a degrade, mirroring the engine's `HED0001`. All 33
  probed cells agree. The body cache is keyed on the call-site model.
- **pinned by:** `DynamicDefinitionBodyTests.ABodyReadingAMemberTheCallSiteValueLacksDegradesInsteadOfBindingItDynamically`,
  `.ABodyReadingAMemberTheCallSiteValueHasStillPrecompiles`, `.ABareCallKeepsAnUntypedBodyOnBothTiers`,
  `.AMemberPathCallKeepsAnUntypedBodyOnBothTiers`, `.TheSecondCallSiteIntoADynamicDefinitionIsTypedOnItsOwnValue`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DynamicDefinitionBodyTests`.
- **notes:** **the record corrects itself here**: this section "says was closed rather than a narrower
  version of the fix". A caller value the emitter could not type was handled by mode — slot mode
  degraded, plain mode kept a dynamically-bound body — and the matrix contained only four call forms.
  Cycle 14 closed the rest (F-130).

### F-125 — A walk deleted as a duplicate, that nothing else performed

- **status:** FIXED (restored)
- **severity:** 2
- **found:** cycle 13
- **symptom:** cycle 12 removed a loop over a containing type's type arguments from `IsObsoleteError`,
  calling it a duplicate of the walk `Classify` performs. `Classify` walks a type's **own** arguments;
  nothing walked an enclosing type's. Measured at the parent commit: a property of type
  `Outer<Legacy>.Inner`, where `Legacy` is error-obsolete, is judged writable and the consumer's build
  dies on two `CS0619`.
- **fixed by:** `27dd357` — restored **in `Classify`, not in `IsObsoleteError`**, so every verdict sees
  an enclosing type's arguments; it also subsumes the missing array/pointer arms of
  `ContainsTypeParameter`.
- **pinned by:** `TypeNameVerdictTests.AnErrorObsoleteArgumentOfAnEnclosingTypeIsRefusedOnTheEnclosingSpelling`
  and the hostile-model table in the integration suite.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`.
- **notes:** one of the two reviewers had reached the "duplicate" verdict having only tried a
  ref-struct argument, which C# cannot declare. A reported sibling route —
  `@model(){{List<Outer<Legacy>.Inner>}}` — **does not exist**: it degrades already, because the shared
  spelling grammar cannot resolve a nested type of a constructed generic at all. Measured and left
  alone.

### F-126 — A completeness suite that pinned the answer but not the arm

- **status:** FIXED
- **severity:** 6
- **found:** cycle 13
- **symptom:** the suite asserted only that each subject was refused and that the reason was non-empty.
  **That is how the wrong deletion (F-125) got authorised:** with an arm gone, a different arm answered
  its rows, the suite stayed green, and mutation testing reported the arm as dead.
- **fixed by:** `27dd357` — the rows carry the words their reason must contain. Two things fell out
  immediately: `unbound-generic` is answered by the **error-type** arm, not the type-parameter one
  (Roslyn fills an unbound generic's arguments with error symbols), and the row named
  `array-of-ref-struct` was built over `Span<T>`, the open definition, so it was refused for being an
  open generic and never consulted the ref-struct rule at all.
- **pinned by:** `TypeNameVerdictTests` (reason-text rows).
- **regression check:** mutate one verdict arm at a time and confirm the suite reddens; nineteen
  mutations were run, seventeen redden the completeness suite and the eighteenth reddens the
  hostile-model table.

### F-127 — The gate reported and the emission did not listen

- **status:** FIXED
- **severity:** 2
- **found:** cycle 13
- **symptom:** `IntegrationTests.Fixtures.Article`, `Fixtures.Article` and
  `System.Collections.Generic.List` all pass the name-existence gate — deliberately, since the runtime
  binds over what is *loaded* — and all three put four `CS0246` or `CS0305` into the consumer's build.
  The engine refuses every one of them.
- **root cause:** a `@model` spelling resolving to no symbol was still written into the entry point's
  parameter type verbatim. **What the gate diagnoses and what gets emitted are separate questions, and
  only the second was wrong** — the gate is unchanged.
- **fixed by:** `27dd357`.
- **pinned by:** `ModelTypeSpellingTests.AModelSpellingThatResolvesToNoSymbolIsNeverWrittenIntoTheEntryPoint`,
  `.AModelSpellingThatResolvesStillPrecompilesWithTheSameBody`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelTypeSpellingTests`.
- **notes:** the test added the previous cycle for this asserted "no errors" over a body that degraded
  for an unrelated reason, so the generated file that could not compile was never built. Its body is
  static text now.

### F-128 — A root reference as a slot value, reported as a latent hole

- **status:** NOT-A-DEFECT
- **severity:** —
- **found:** cycle 13
- **symptom (as reported):** `@out(::X)` is never type-checked.
- **measurement:** it cannot become one — `BuildParamExpr` refuses every root-reference call parameter
  outright, so such a template degrades before anything is emitted, whatever the type check would have
  said. Measured over four shapes, including one the engine renders and the emitter declines.
- **notes:** a note in the code records it so the next reader does not have to measure it again.

### F-129 — A boundary rule pinned by nothing, and a per-keystroke full-closure walk

- **status:** FIXED
- **severity:** 6 (plus a real allocation cost)
- **found:** cycle 13
- **symptom:** three separate mutations of the dot-boundary rule left every test green. The same gate
  composed a qualified name per type visited, over the whole reference closure, on a path that runs
  per keystroke in an editor.
- **fixed by:** `27dd357` — rows that end mid-identifier (the only thing separating it from a plain
  suffix test) and one where every segment is real and only the separator is not a dot; matching goes
  segment by segment from the right. Equivalence measured: 4,097 spellings drawn from the closure,
  zero disagreements; three full-closure misses fall from 11,528,976 bytes to 2,206,320.
- **pinned by:** `ModelTypeSpellingTests.ASpellingIsOnlyKnownWhenEverySegmentOfItIs`.
- **regression check:** as F-127.

### F-130 — "The emitter cannot say" read as "the engine has no type"

- **status:** FIXED
- **severity:** 1 and 3
- **found:** cycle 14, both reviewers, from different repros
- **symptom:** `TryTypeCallSiteBody` returned success for a plain definition on `null` — the answer
  meaning *the emitter has no static type for this call-site value* — and left the body dynamically
  bound. The engine signals a genuinely untyped model separately and definitely, as `dynamic`. So
  every call form outside cycle 13's matrix diverged: a computed native expression, a chained call and
  `this` inside an `@list` body all carry a real static type in the engine. Measured at the parent with
  a body reading a member the type lacks: the engine refuses at compile time in every one, naming the
  type (`[Int32]`, `[String]`, `[Boolean]`, `[MenuOption]`, `[Object]`), and the generated tier
  precompiled and either threw `RuntimeBinderException` at render or **rendered a page the engine will
  not compile at all**.
- **fixed by:** `e14e862` — `null` degrades in plain mode as in slot mode, and two engine rules are
  mirrored to get the precompilation back: a computed expression's type comes from the shared operator
  tables (`NativeOperatorRules.BinaryResult`/`UnaryResult`/`ClassifyTernary`), and the model inside an
  `@list` body is the collection's `IEnumerable<T>` argument (`dynamic` for a collection implementing
  no generic form, "cannot say" when a type reaches `IEnumerable<T>` more than once).
- **pinned by:** `DynamicDefinitionBodyTests.AComputedCallSiteValueTypesTheBodyToTheSameTypeOnBothTiers`,
  `.AComputedCallSiteValueStillPrecompilesWhenTheBodyFitsIt`, `.TheValueInsideAListBodyIsTheElementTypeOnBothTiers`,
  `.AnUntypeableCallSiteValueDegradesInsteadOfKeepingAnUntypedBody`, `.ALiteralCallSiteValuePrecompilesWhenTheBodyFitsIt`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DynamicDefinitionBodyTests`.
- **notes:** **a measurement correction lives here.** An earlier draft of the record — and the wording
  in commit `e14e862` itself — reports "of thirteen probed shapes, eight degraded before and one does
  now". That is not a parent-versus-child count and **no commit reproduces it**; the record's corrected
  figure is that over twenty call-site value forms the parent degrades **one** and the commit degrades
  **two**. Believe the record. The paired assertion that makes this testable: the type named in the
  generator's `HED7008` and in the engine's `HED0001` must agree — a degrade assertion alone would
  pass whatever type the emitter picked, and a wrong type is a cast the generated body throws on.

### F-131 — An `@list` body nested in a definition lost the definition's prop layout

- **status:** FIXED
- **severity:** 1
- **found:** cycle 14
- **symptom:** `<host(Name: string)>{{@list(Products){{@(Name)}}}}` rendered `[PROP]` on the engine and
  `[ELEMENT]` on the generated tier. Both precompiled, no diagnostic, different bytes — and the same
  template with the `@list` removed, or with `@if` in its place, agreed.
- **root cause:** the engine saves and restores the active prop layout around **definition bodies
  only**, so an `@list` body nested inside a definition still resolves its first path segment as a
  prop; the emitter built the item context with no layout.
- **fixed by:** `e14e862` — the layout propagates.
- **pinned by:** `PropsTests.APropSurvivesIntoANestedListBody` (the element row next to it — a name the
  layout does *not* carry still reads off the element — is what keeps this prop-first rather than the
  layout swallowing the body).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~PropsTests`.
- **notes:** the first member of the prop-layout class found; cycles 15 and 16 found the rest. The full
  enumeration is Table A in `docs/generator_plan/phase-8-docs-sweep.md`.

### F-132 — A table row asserting a degrade and an empty error list

- **status:** FIXED
- **severity:** 6
- **found:** cycle 14
- **symptom:** the `null` row of the `:: dynamic` body table asserted a degrade and an empty error
  list, which any refusal satisfies. Demonstrated: refusing the null literal outright left all nine
  tests green, and **refusing every literal call site outright failed only two rows and passed the
  other 672 integration tests.** Nothing anywhere asserted that a `:: dynamic` definition called with a
  literal ever precompiles.
- **fixed by:** `e14e862` — four rows that render the engine's bytes, one of them reading `Length` off
  a `string` literal so it fails both if the body is left dynamic and if it is typed as anything else.
- **pinned by:** `DynamicDefinitionBodyTests.ALiteralCallSiteValuePrecompilesWhenTheBodyFitsIt`.
- **regression check:** as F-130; both mutations (refuse the null literal; refuse every literal) must
  redden.

### F-133 — Two verdict arms no compilation can reach

- **status:** FIXED
- **severity:** 6
- **found:** cycle 14
- **symptom:** `Classify`'s `TypeKind` switch grouped `Unknown` with `Error` and `Module` with
  `Submission`, and no C# compilation produces a symbol carrying either of the first two — so through
  symbols alone both labels could be deleted with every suite green. The previous cycle's record named
  one of them.
- **fixed by:** `e14e862` — the kind table is a function of `TypeKind`, asked of every kind the enum
  declares, plus a row asserting the rows cover the enum, so a kind Roslyn adds later arrives with no
  verdict and the theory does not compile past it.
- **pinned by:** `TypeNameVerdictTests.EveryTypeKindHasAVerdictOfItsOwn`, `.TheKindRowsCoverTheEnum`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`;
  deleting either label reddens.

### F-134 — A gate's global-namespace stop, claimed covered and covered by nothing

- **status:** FIXED
- **severity:** 6
- **found:** cycle 14
- **symptom:** the previous cycle's record claimed the rows it added covered the name-existence gate's
  global-namespace stop. They end mid-identifier; the stop is about running out of namespaces with
  spelling left over. Removing it turns every leading-dot spelling — `.System.Object` and its kind —
  from refused into accepted; the review counted 241 such flips over 6,857 sampled spellings.
- **fixed by:** `e14e862`.
- **pinned by:** `ModelTypeSpellingTests` — the `.Probe.Only.UniqueProbe` row is the one that reddens
  when the stop goes.
- **regression check:** remove the global-namespace stop and confirm that row reddens.

### F-135 — `@out(this)` inside an `@list` body inside a slot definition was an unchecked precompile

- **status:** FIXED
- **severity:** 3
- **found:** cycle 14
- **symptom:** swept over seven slot parameter types, the parent precompiled all seven and rendered
  `[[c]]` — and five of those seven are templates the engine refuses outright (`string`, `int`, `bool`,
  `decimal` and an unrelated class, each `HED5014`).
- **fixed by:** `e14e862` — degrades exactly those five and keeps the two the engine accepts (the
  element type itself, and `object`).
- **pinned by:** `DynamicDefinitionBodyTests.AnOutValueInsideAListBodyIsCheckedAgainstTheElementType`.
- **regression check:** as F-130.
- **notes:** this was **a declared cost in cycle 10** (F-106) that turned out to be the reverse — the
  gap was not a conservative degrade, it was an unchecked precompile. When a record says "we
  deliberately do not check X", measure what happens in X's absence.

### F-136 — Prop-first resolution missing from the expression writer

- **status:** FIXED
- **severity:** 2 and 1
- **found:** cycle 15, both reviewers independently
- **symptom:** two faces, neither needing a shadowed name:
  - a prop the model has **no** member of — the ordinary case,
    `<host(n: int = 5)>{{[@(n + 1)]}}` — resolved to nothing and reported `HED7008` at **Error**, so a
    template the engine compiles and renders broke the consumer's build;
  - a prop that *did* shadow a member compiled quietly and rendered the model's value: `[8]` where the
    engine renders `[P1]`, `[t]` where it renders `[a][b]`, `[False]` where it renders `[True]`.
- **root cause:** `NativeExpressionCompiler` tries the active prop layout on a path's first segment
  before it looks at the scope type at all; the emitter's member-path reader mirrored that, and the
  expression writer was constructed with the model and **no layout**.
- **fixed by:** `c1b20dd` — both paths take the layout and resolve the first segment prop-first,
  hopping the rest off the slot's declared type.
- **pinned by:** `PropsTests.ANativeExpressionReadsAPropBeforeTheModel`, `.APropInAListDataExpressionIsTheProp`,
  `.AShadowingPropsTypeDecidesADynamicDefinitionsModel`, `.AShadowingPropsTypeDecidesWhatASlotAccepts`,
  `.AnIntPropThroughTheSameShapeIsRefusedByBothTiers`, `.AnIntPropIntoAStringSlotIsRefusedByBothTiers`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~PropsTests`.
- **notes:** measured over forty prop shapes: fourteen matched, five rendered different bytes and
  twenty-one degraded before; thirty-nine match and one degrades after, and the one is a template the
  engine refuses. **The record's "four paths" count was corrected by cycle 16 to at least six readers
  and at least six contexts** — see the two enumeration tables in
  `docs/generator_plan/phase-8-docs-sweep.md` before assuming this class is closed.

### F-137 — Prop-first resolution missing from the routine that types a computed call-site value

- **status:** FIXED
- **severity:** 3 and 4
- **found:** cycle 15 (sibling of F-136, same root cause one level up)
- **symptom:** a shadowed name got the shadowed *member's* type, so `Cols + 1` over a `string` prop was
  typed `Int32`, and the body built off that type reported a member the `string` it really receives has
  — `HED7008` at Error over a template the engine renders — or refused a `string` slot the engine fills.
- **root cause:** `ComputedValueType` resolved off the model and did not even take the body context the
  layout lives on. **The previous cycle's new typing (F-130) is what made it visible**; before that the
  two mistakes cancelled.
- **fixed by:** `c1b20dd`.
- **pinned by:** `PropsTests.AShadowingPropsTypeDecidesADynamicDefinitionsModel`,
  `.AShadowingPropsTypeDecidesWhatASlotAccepts`.
- **regression check:** as F-136.

### F-138 — `@list` over something that is not a list

- **status:** FIXED
- **severity:** 1
- **found:** cycle 15
- **symptom:** an extension declares the type it accepts and the engine checks the call's value before
  it compiles anything: `@list` accepts `IEnumerable`, so `@list(Obj)` over an `object`-typed member is
  a template the engine refuses (`HED0004`). The emitter mirrored no such check, so it precompiled and
  rendered — walking a string's characters where the value happened to be a string, rendering nothing
  where it was an `int`.
- **fixed by:** `c1b20dd` — the check is on the value's *static* type, so a `dynamic` value and a value
  the emitter could not type are both exempt.
- **pinned by:** `ListTests.ANonEnumerableValueIsRefusedByBothTiers`, `.EnumerableValuesStillPrecompile`
  (a `List<T>`, an array, a `string`, and a `List<object>` all still precompile).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ListTests`.
- **notes:** written out for `@list` alone, which is F-149 two cycles later: `[DataType]` is a rule, not
  a list of extensions.

### F-139 — Nine table rows that were one assertion wearing nine hats

- **status:** FIXED
- **severity:** 6
- **found:** cycle 15
- **symptom:** the type-kind table's nine `null` rows all asked `UnnameableKind` for a null answer,
  which is the same `default:` arm nine times: they passed and failed together, and none was about the
  kind it named.
- **fixed by:** `c1b20dd` — each row is asked of a real symbol of that kind, through the classifier the
  emitter actually calls. The settling mutation: making the classifier refuse every enum type leaves
  the old rows entirely green and reddens exactly the new `Enum` row.
- **pinned by:** `TypeNameVerdictTests.AKindTheTableRefusesCarriesItsOwnSentence`,
  `.AKindTheTableAllowsIsWrittenForASymbolOfThatKind`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`.
- **notes:** **"each row stands alone" held for six of the seven** — corrected by cycle 16 (F-146).

### F-140 — Two verdict rows that cannot be honestly pinned

- **status:** KNOWN-OPEN (recorded rather than pretended)
- **severity:** 6
- **found:** cycle 15
- **symptom:** `Structure` is Visual Basic's spelling of `Struct` and parses to the same value, so the
  `Struct` row already answers for it — the assertion about it is about **Roslyn's** enum, not about
  anything in this repository, and no change here can redden it. `Extension` is declared only by newer
  Roslyn; the generator compiles against Microsoft.CodeAnalysis.CSharp 4.4.0, which does not declare
  the name, so a case for it is `CS0117` — measured, not assumed.
- **fixed by:** — nothing to fix; covered by the completeness gate and by nothing else.
- **regression check:** none until the generator's own Roslyn moves.
- **notes:** an earlier description called this something that "would stop being true loudly", which
  overstated what it covers. Do not re-report; do not add a case for `Extension`.

### F-141 — Caller content compiled under the callee's model, with no layout and no slot mode

- **status:** FIXED
- **severity:** 1, 2 and 3 (four faces)
- **found:** cycle 16, both reviewers independently
- **symptom:** measured at the parent against the engine, model `GridModel { Name = "model", Cols = 7 }`:
  - `<outer(Cols: string = "PP")>{{@inner(this){{[@(Cols)]}}}}` — engine `([PP])`, generated `([7])`;
    silent, both tiers rendering, different bytes. The native form `@(Cols + "!")` is `([PP!])` against
    `([7!])`.
  - `<outer(label: string = "PP")>` with `@(label)` — engine renders `([PP])`; **the build fails** with
    `HED7008: 'GridModel' does not contain an accessible member 'label'`. That is the symptom the
    previous commit's message claims to have eliminated, alive one context over.
  - `@list(Name)` in caller content, prop `"PP"` against member `"ab"` — engine `([P][P])`, generated
    `([a][b])`: the enumerability gate had judged the *model member's* type.
  - a valued `@out` in caller content lexically inside a slot definition's body — engine renders, the
    emitter dropped the whole template; the bare `@out()` in the same place is the reverse (engine
    refuses with `HED5013`, emitter precompiled and rendered).
- **root cause:** the engine compiles caller content **before** it installs the definition's own prop
  layout and slot parameter type, so the caller's layout and slot mode are still active there. The
  emitter built that context from the callee and carried neither.
- **fixed by:** `2664141` — both travel now.
- **pinned by:** `PropsTests.CallerContentKeepsTheCallingDefinitionsProps`, `.SlotModeCallerContentKeepsThemToo`,
  `.CallerContentInsideASlotBodyIsStillInsideTheSlot`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~PropsTests`.
- **notes:** this is **Table A row 9** of the enumeration in `docs/generator_plan/phase-8-docs-sweep.md`.
  Read that table before adding any new `BodyContext` construction site: `RegionHostProps` is a second
  copy of `Props` at every site that sets either, so rows 4 and 5 agree with row 6 by construction.

### F-142 — A prop argument checked against the model member while emitted as the prop

- **status:** FIXED
- **severity:** 1
- **found:** cycle 16
- **symptom:** `<inner(q: int = 0)>` called as `q: Cols` with `<outer(Cols: string = "PP")>` rendered
  `[PP]` — a `string` boxed into an `int`-declared slot — where the engine refuses the template with
  `HED5003`. Reversed (`q: string`, `Cols: int`) it rendered `[5]` against the same refusal. It is
  prop-specific: a model member or a literal in the same position always precompiled.
- **root cause:** `TryBuildDynamicSetter` typed the argument off the caller's model while the writer
  three lines below emitted the caller's *prop*. **Worse than either half alone: the check and the
  emission disagreed about which value the argument even is.** Pre-existing, but F-136's fix is what
  made the two halves disagree.
- **fixed by:** `2664141`.
- **pinned by:** `PropsTests.APropArgumentIsCheckedAgainstThePropItReads`, `.APropArgumentThatFitsStillPrecompiles`.
- **regression check:** as F-141.
- **notes:** Table B row 5. Two over-degrades fell out of the change and were kept: a non-shadowing
  prop argument now precompiles, and a prop-rooted argument at a dynamic-tier call site no longer needs
  a typed caller model.

### F-143 — Every native expression refused on the dynamic tier before the layout was consulted

- **status:** FIXED
- **severity:** 4
- **found:** cycle 16
- **symptom:** `<host(n: int = 5)>{{@list(Tags){{[@(n + 1)]}}}}` dropped the whole template where the
  engine renders `[6][6]`.
- **root cause:** `BuildParamExpr` refused every native expression when the context was dynamic — an
  `@list` body's tier — before it looked at the layout, where the engine tries the prop layout first
  and only then asks for a model.
- **fixed by:** `2664141` — **the guard is gone rather than narrowed:** the writer, given no model
  type, already refuses any path that reads one, which is the engine's own order and answer.
- **pinned by:** `PropsTests.APropRootedExpressionNeedsNoModelInsideAListBody`.
- **regression check:** as F-141.
- **notes:** a constant expression needing no model precompiles for the same reason, which moved
  `at-escape-comment-adjacent.heddle` from `FallsBackSafely` to `Precompiles` in
  `src/TestCorpus/CorpusIntent.cs`.

### F-144 — What a chain hands the next link is text, not the producer's value

- **status:** FIXED
- **severity:** 1 and 3
- **found:** cycle 16
- **symptom:** measured at the parent commit —
  - `@list(len(Name))` with `Name = "abcd"` — engine `<4>` (it iterates the characters of `"4"`),
    generated **empty**; silent, both tiers rendering.
  - `@list((Cols))` with `Cols = 7` — engine `<7>`, generated empty. **Neither reviewer reported this
    one; it is the same defect one syntax over**, which is why the enumeration came first.
  - `<probe>{{[@(Length)]}} :: System.String` called as `@probe(len(Name))` — engine `[1]`, generated
    `InvalidCastException` at render.
  - `@list(upper(Name))` matched all along, because `upper` already returns a string — which is the
    whole reason this survived so long.
- **root cause:** a chain call-parameter is compiled as a chain and the engine hands the extension
  `callParameter.RenderType` — the last item's `InitStart` return, which for every flattenable chain is
  `AbstractExtension`'s default `typeof(string)`. The emitter flattened a one-item chain to the
  producer's own expression, under a comment claiming byte-identity.
- **fixed by:** `2664141` — the carrier is reproduced through `PrecompiledRuntime.CarrierValue`
  (`EmptyExtension`'s pass-through: `null` → empty string, a string is itself, anything else
  `ToString()`), and `CallSiteValueType` answers `System.String` for a chain.
- **pinned by:** `ListTests.AChainValueReachesListAsTheTextTheCarrierRenders`,
  `.ANativeCallsReturnTypeDecidesWhetherListAcceptsIt`;
  `DefinitionTests.AChainValueReachesATypedDefinitionAsText`;
  `DynamicDefinitionBodyTests.AChainedCallSiteValueTypesADynamicBodyByItsRenderType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ListTests`.
- **notes:** the sibling is a different syntax with a different answer — a multi-argument call is a
  *native expression*, not a chain, so the engine keeps the function's return type and refuses
  `@list(min(1, 2))`. `ComputedValueType` had no `CallNode` arm, so the gate exempted every call as
  "cannot say"; it asks the shared ranker now. That half was then found lossy — F-148.

### F-145 — A type-name gate added to a layout on suspicion, with no demonstrated red

- **status:** SUPERSEDED (removed in cycle 18 — F-158)
- **severity:** 6 at the time; it later cost a working template
- **found:** cycle 16 (added), measured in cycle 17, reversed in cycle 18
- **symptom:** `ResolveExtensionPropLayout` filled its slot types from `[Prop]` metadata without the
  `CanWriteTypeName` gate the definition layout applies. No call path makes an extension layout the
  *active* layout, so there was nothing to demonstrate and no test pretended otherwise.
- **notes:** read F-153 and F-158 before re-adding anything here. Measuring it for a red found the
  opposite of one: it cost a working precompiled template and emitted an `HED7030` telling the
  consumer to make an internal type public for no reason.

### F-146 — A verdict row that could not fail on the arm it named

- **status:** FIXED
- **severity:** 6
- **found:** cycle 16
- **symptom:** `Classify` short-circuited an array into a recursion on its element *before* consulting
  `UnnameableKind`, so `UnnameableKind(TypeKind.Array)` was unreachable and the `Array` row was a
  second copy of its element's row. Measured at the parent: an `Array` case left all thirty-seven rows
  green while each of the other six kinds reddened its own.
- **fixed by:** `2664141` — the table is consulted first (it returns null for `Array`, so no verdict
  changes).
- **pinned by:** `TypeNameVerdictTests` — an `Array` case now reddens the `Array` row. `Struct` reddens
  two, its own and `Array`, and **that is not removable**: an array is answered by recursing onto its
  element and every element has a kind with a row. The docstring says so instead of claiming
  independence.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`.

### F-147 — A native expression reading the element's own member inside an `@list` body degrades

- **status:** KNOWN-OPEN
- **severity:** 4
- **found:** cycle 16, reported rather than hidden
- **symptom:** `@(Name + "!")` over a `List<Product>` — the engine compiles it against the element
  type; the emitter has that type in `DynamicBodyModel` but emits the body's reads on the dynamic tier,
  so the expression degrades.
- **fixed by:** — deferred: typing the writer off `DynamicBodyModel` "is a change of a different shape
  and was left for a later cycle".
- **regression check:** none — it is a degrade, so nothing reddens.
- **notes:** the plain path `@(Name)` and the function form `@upper(Name)` are unaffected. Do not
  re-report; do estimate the cost before doing it, since it is precompilation left on the table rather
  than a divergence.

### F-148 — A kind funnel cannot say what a name says

- **status:** FIXED
- **severity:** 1 and 3
- **found:** cycle 17
- **symptom:** the check that types a call-site value routed a function call through the shared operand
  *descriptor*, which names the numeric primitives, `bool` and `string` and nothing else — every other
  return type arrived as "cannot say", **which every gate exempts**.
  - `@list(range(1, 3)){{<@()>}}` — the engine refuses (`HED0004`, `Heddle.Models.Range` against
    `System.Collections.IEnumerable`); the generated tier precompiled and rendered **empty**, having
    handed `ListExtension` a `Range` to iterate.
  - `<s(out:: object)>{{[@out(range(1, 3))]}}` — the engine refuses (`HED5014`); the generated tier
    precompiled and rendered.
  - a host `[ExportFunctions]` function returning a `DateTime` or a class of its own slips both gates
    the same way — measured over a purpose-built export container, four rows, all four confirmed.
- **root cause:** `range` is the only built-in whose return type is none of those three, and it is
  exactly the built-in a reader reaches for when they want to iterate.
- **fixed by:** `669eef8` — the chosen overload carries its declared return type (a metadata name in
  the built-in table, an `ITypeSymbol` for an export) and the call is typed from that.
  **`dynamic` is the one return type not taken at face value**: there is no such thing in metadata, the
  engine reads a `MethodInfo` whose return type is `System.Object`, and the emitter maps it there too.
- **pinned by:** `ExportFunctionTests.AnExportReturningANonPrimitiveIsRefusedByListOnBothTiers`,
  `.AnExportReturningANonPrimitiveIsCheckedAgainstTheSlotType`,
  `.AnExportReturningDynamicIsTypedAsObjectLikeTheEngineTypesIt`, `.AnExportWhoseReturnTypeFitsStillPrecompiles`;
  `ListTests.AListOverRangeIsRefusedByBothTiersBecauseARangeIsNotEnumerable`;
  `SlotValueTypeTests.ACallReturningANonPrimitiveIsCheckedAgainstTheSlotType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ExportFunctionTests`;
  without the `dynamic`→`object` mapping the `object` row degrades.
- **notes:** both shapes were already true at the parent commit — **the previous commit's degrade sweep
  read as complete and was not.**

### F-149 — `[DataType]` is a rule, not a list of extensions

- **status:** FIXED
- **severity:** 1
- **found:** cycle 17
- **symptom:** the emitter had the accepted-type check written out for `@list`'s `IEnumerable` and for
  nothing else, leaving `@for` — the only other `[DataType]` built-in the emitter still binds —
  unchecked. Measured with `Cols = 7`, `Name = "ab"`: `@for((Cols))` and `@for(len(Name))` are
  templates the engine refuses (`HED0004`, `System.String` against `[Heddle.Models.Range,
  System.Int32]`); **the parent commit rendered 7 and 2 iterations, and its own parent rendered 0** —
  both wrong, in different ways, and neither degraded.
- **fixed by:** `669eef8` — the check reads `[DataType]` off the bound extension over the base chain,
  with the same `inherit` the runtime uses, and applies to every extension the emitter binds.
- **pinned by:** `ForTests.AForOverAValueItDoesNotAcceptIsRefusedByBothTiers`,
  `.AForOverAnAcceptedValueStillPrecompiles`, `.AForOverANullableIntStillPrecompiles`;
  `AcceptedTypeTests.*` (cycle 18).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ForTests`
  and `--filter FullyQualifiedName~AcceptedTypeTests`.
- **notes:** generalising cost nothing measurable — the whole corpus was byte-identical. **The relation
  it compares with was wrong** until cycle 18 (F-155): reflection's `IsAssignableFrom` has no numeric
  widening, which is why `@for` over a `long` is a template the engine refuses.

### F-150 — Caller content under a `:: dynamic` callee was still untyped

- **status:** FIXED
- **severity:** 3
- **found:** cycle 17
- **symptom:** swept over twenty-one shapes against the engine —
  `@frame("ab"){{(@(Title))}}` and `@frame(this){{(@(Nope))}}` are refused by the engine at compile
  time (`HED0001`, naming `String` and the caller's model) and the generated tier **precompiled**.
- **root cause:** cycle 16 taught the emitter that `:: dynamic` does not declare an untyped body and
  applied it to the definition body only; the content the *call site* hands the definition kept the
  untyped context though it is compiled against the same model.
- **fixed by:** `669eef8` — the same rule types both.
- **pinned by:** `DynamicDefinitionBodyTests.CallerContentUnderADynamicCalleeIsTypedByTheCallSiteValue`,
  `.CallerContentStillPrecompilesWhereTheEngineCompilesIt`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DynamicDefinitionBodyTests`.
- **notes:** the half that must keep matching does: `@frame(Name){{(@(Length))}}` — a member path is the
  engine's own dynamic exit, so both tiers compile and both throw the same `RuntimeBinderException`
  where the member is missing. Sixteen of twenty-one matched before, eighteen after; three shapes still
  degrade — F-154.

### F-151 — A sweep category that could not hold the defect the cycle found

- **status:** FIXED (instrument added)
- **severity:** 6
- **found:** cycle 17
- **symptom:** the previous cycle's degrade sweep enumerates templates that newly **degrade**. That
  category cannot hold `@for` over a chain, which kept precompiling across the commit and rendered
  *different bytes* — seven iterations at the parent, none after. Such a template is invisible to a
  degrade sweep **and to a differential test whenever the engine refuses it**, because then there is no
  dynamic render to compare against.
- **fixed by:** `669eef8` — the sweep has two counts: newly degrading, and **bytes moved while still
  precompiling**. The instrument is the same corpus run captured twice — classification, every
  generated `.g.cs`, the manifest and the diagnostic list — and diffed.
- **regression check:** run the corpus capture twice across your change and diff all four artefacts.
  Report both counts in the landing.
- **notes:** a third category was still missing — F-159.

### F-152 — Two constructs examined rather than assumed

- **status:** NOT-A-DEFECT (both), with one cleanup
- **severity:** —
- **found:** cycle 17
- **detail:** `bctx.IsDynamic ? null : bctx.ModelSymbol` was a no-op at both sites: every construction
  site upholds `IsDynamic ⇒ ModelSymbol is null`, verified by reading all eleven of them. The guard
  read as though the two could disagree, so it is gone and the invariant is stated on the field, next
  to the pointer to `DynamicBodyModel` — the field that answers the *different* question of what the
  engine typed a dynamically-emitted body against.
  Separately, the writer the call-typing pass builds is thrown away undrained, so a refusal it proves
  is discarded — structurally harmless, because a call the ranker refuses has no return type either, so
  the value stays "cannot say", no gate can refuse on account of it, and the writer that *emits* the
  same expression is always reached.
- **pinned by:** `AmbiguousOverloadDiagnosticTests.AnIllegalCallInATypedValuePositionIsStillReported`
  (an ambiguous call as an `@list` value and as an `@out` slot value each still report `HED7025` at
  Error).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AmbiguousOverloadDiagnosticTests`.

### F-153 — A failed layout left truncated in the cache

- **status:** FIXED
- **severity:** 6 (only the `Failed` flag stood between a truncated layout and a caller)
- **found:** cycle 17
- **fixed by:** `669eef8` — the whole declaration list is checked before a single slot is built, so a
  failed layout carries no slots at all; the flag and every observable behaviour are unchanged.
- **notes:** measuring the *gate* for a red is what produced the reversal recorded in F-158. The
  measurement: a host extension declaring `[Prop("box", typeof(InternalModel), Optional = true)]` over
  an `internal` type, called as `@hiddenProp()` — the engine renders `[box=<null>]` and so does the
  generated tier **once the gate is removed**, identical bytes, because no consumer of an extension
  prop layout ever spells the slot type.

### F-154 — Three caller-content shapes degrade under a `:: dynamic` callee

- **status:** KNOWN-OPEN
- **severity:** 4
- **found:** cycle 17, reported rather than hidden
- **symptom:** a native expression, a function call and an `@if` in caller content under a
  `:: dynamic` callee degrade where the engine renders. Unchanged by that cycle.
- **fixed by:** — deferred.
- **regression check:** none — a degrade reddens nothing.
- **notes:** do not re-report. If you close it, the sweep must report all three counts (F-151, F-159).

### F-155 — Reflection's assignability claimed, not reproduced

- **status:** FIXED
- **severity:** 4 (six shapes the engine renders and the parent precompiled started degrading)
- **found:** cycle 18, both reviewers, in the previous commit
- **symptom:** `int` and `int?` into `[DataType(typeof(int?))]`, `List<string>`, `IList<string>` and
  `string[]` into `IEnumerable<object>`, and `string[]` into `object[]`. **The nullable rows are the
  sharpest: the gate unwrapped the *value's* nullable and compared against the un-unwrapped declared
  one, so `[DataType(typeof(int?))]` accepted nothing whatever — not even an `int?`. That declaration
  was entirely non-functional.**
- **root cause:** the gate compared with `SymbolEqualityComparer` over the base chain and the interface
  set — nominal identity. `Type.IsAssignableFrom` has generic variance, array covariance and the CLR's
  `Nullable<T>` treatment in it. **The doc comment enumerating "identity, a base class, an implemented
  interface, or the boxing to `object`" was itself the defect.**
- **fixed by:** `2d3fab6` — the gate asks the generator's existing symbol adapter for the CLR relation,
  which has verified Roslyn-vs-CLR corrections and a shared conformance corpus driven from both tiers.
- **pinned by:** `AcceptedTypeTests.AValueTheEngineAcceptsPrecompilesAndRendersTheEnginesBytes`,
  `.AValueTheEngineRefusesDegradesRatherThanPrecompiling`, `.AnInheritedAcceptedTypeIsAcceptedAlongsideTheDeclaredOne`;
  four `[DataType]`-declaring fixtures in `Fixtures/AcceptedTypeExtensions.cs`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AcceptedTypeTests`;
  two mutations must redden disjoint halves — an acceptance that answers yes to everything reddens
  every refusal assertion, and one back to nominal identity reddens every non-exact acceptance row.
- **notes:** **why no fixture caught it:** no built-in is affected, and neither the corpus nor the
  samples contained a single host extension declaring `[DataType]`. "The corpus is byte-identical" was
  true and closed nothing. Two claims in this entry were corrected by cycle 19 — "variance, array
  covariance and the nullable domain come with it by construction" was false for array covariance
  (F-163), and the row/mutation counts ("thirteen rows … five … six") do not reproduce.

### F-156 — `:: object` is the engine's predicate; `dynamic` is a spelling

- **status:** FIXED
- **severity:** 1 and 4
- **found:** cycle 18
- **symptom:** every rule in this area had been keyed on the *word* `dynamic`, so `:: object` and an
  undeclared model carried the whole class its twin had fixed, in both directions:
  - `<s(out:: object)>{{[@out(this)]}} :: object` called `@s(5)` — the engine refuses (`HED5014`,
    boxing switched off for this check); the generated tier precompiled and rendered `[|5|]`.
  - `<frame>{{@for(this)}} :: object` + `@frame(2)`, `<frame>{{@list(this)}} :: object` +
    `@frame("ab")`, `<frame>{{[@(Length)]}} :: object` + `@frame("ab")`, the same with caller content,
    and the undeclared form of each — the engine renders all of them and the generated tier degraded.
- **root cause:** the engine's test is `acceptType == typeof(object)` in
  `HeddleCompiler.CreateExtension`; the parser fills in `object` when no `::` is written and the alias
  table maps `dynamic` to `object`.
- **fixed by:** `2d3fab6` — re-keyed on the engine's predicate. **The two spellings do not collapse:**
  only `dynamic` sends the model accessor down its dynamic exit, so under `:: object` a member path at
  the call site resolves statically and a missing member is a compile-time refusal where the
  `:: dynamic` twin is a render-time throw both tiers share. Both halves are pinned.
- **pinned by:** `ObjectDefinitionBodyTests.*` — `.ABodyCallingForOnItsOwnModelPrecompilesUnderAnObjectDefinition`,
  `.ABodyCallingForOnAModelForDoesNotAcceptDegrades`, `.ABodyReadingAMemberOfItsOwnModelPrecompilesUnderAnObjectDefinition`,
  `.CallerContentUnderAnObjectDefinitionIsTypedByTheValueToo`, `.ADefinitionThatDeclaresNoModelBehavesTheSameWay`,
  `.ASlotValueTheEngineWillNotBoxDegradesUnderAnObjectDefinition`, `.AMemberPathCallSiteTypesAnObjectBodyStatically`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ObjectDefinitionBodyTests`.
- **notes:** the arm added alongside this fix — a caller whose own scope has no static type gives the
  body `dynamic` — **was itself the next cycle's regression** (F-161). A model-less document is nothing
  but that shape, so answering "cannot say" there took every one off the precompiled tier.

### F-157 — An `[Obsolete(…, error: true)]` exported function breaks the consumer's build

- **status:** FIXED
- **severity:** 2 — the highest-severity finding of that cycle, **pre-existing and on no known-open
  list**
- **found:** cycle 18
- **symptom:** reflection ignores `[Obsolete]`, so the engine calls the function and renders; the
  generated file writes the call out as C# into the consumer's assembly, where the error form is
  `CS0619`. Measured four ways — expression position and `@out` slot position, with a `string` return
  and a class return — the consumer's build stopped at both commits, on a `.g.cs` no one can edit, over
  a template that is not at fault.
- **root cause:** the emitter already had the doctrine for *types* (F-104); the rule had never been
  asked about the **method** a call is written to.
- **fixed by:** `2d3fab6` — asked over both names the emitted call spells: the container type and the
  method.
- **pinned by:** `DeprecatedExportTests.AnExportTheConsumersCompilerRejectsDegradesInsteadOfBreakingTheBuild`,
  `.AnExportTheConsumersCompilerAcceptsStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DeprecatedExportTests`;
  removing the method arm turns the degrading rows back into `CS0619` in the generated code.
- **notes:** three near neighbours keep it from being a refusal of exports, of `[Obsolete]`, or of the
  container: warning-level `[Obsolete]` must keep precompiling; an export whose *return* type the
  consumer may not name compiles and renders identically (the call site spells the method, not what it
  hands back); the same container's undecorated export is unaffected. **Cycle 19 corrected this
  entry:** three of the four measured cells were committed as rows and the fourth
  (slot-position-with-a-class-return) was measured and then not written down. It is a row now.

### F-158 — The extension prop-layout guard, reversed

- **status:** SUPERSEDED (removed) — closes F-145 and F-153
- **severity:** 4 (as it stood, it cost a working precompiled template)
- **found:** cycle 18, two reviewers measuring the guard added on suspicion two cycles earlier
- **verification, re-done rather than taken on trust:** the extension layout has exactly three
  consumers — the frozen props prototype, the parameter-name field and the binding row's fingerprint —
  and none writes a type name. The cast-emitting prop reader is unreachable from an extension layout,
  because every layout that becomes a body's active props is a *definition* layout. A dynamic setter
  refuses outright unless the argument type matches the slot exactly or widens numerically, so it too
  writes a keyword or nothing.
- **measurement:** with a host extension declaring `[Prop("badge", typeof(InternalBadge), Optional =
  true)]` over an `internal` type — gate on, every call shape degrades with `HED7030@Warning` while the
  engine renders; gate off, all of them precompile, the generated code compiles, and the bytes match
  the engine, through the default, a literal argument and a dynamic setter alike, and through the
  resolver gauntlet under `PrecompiledMismatchPolicy.Strict`, where the fingerprint round-trips.
- **fixed by:** `2d3fab6` — removed.
- **pinned by:** `ExtensionPropTypeNameTests.AnExtensionPropTypeThisAssemblyCannotNameStillPrecompiles`,
  `.TheLayoutFingerprintRoundTripsForAnUnnameablePropType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ExtensionPropTypeNameTests`.
- **notes:** **the definition layout's identical gate is untouched and is not the same case** — a
  definition layout really does become a body's active one. Do not "restore symmetry" here.

### F-159 — A third sweep category: newly precompiling

- **status:** FIXED (instrument added)
- **severity:** 6
- **found:** cycle 18, both reviewers
- **symptom:** the sweep had two counts (newly degrading; bytes moved while still precompiling).
  Neither can hold a template that *starts* precompiling — which is new generated code reaching
  consumers.
- **fixed by:** `2d3fab6`.
- **regression check:** capture the corpus twice (classification, generated source, rendered bytes) plus
  the ten samples' goldens and their generated files, and report all three counts.
- **notes:** that cycle: zero newly degrading, zero moved bytes, **one** newly precompiling — a
  model-less template whose definition declares no model and whose body branches around an `@out`
  projection; its intent row moved with it (`branching-out-projection.heddle`, `FallsBackSafely` →
  `Precompiles` in `src/TestCorpus/CorpusIntent.cs`).

### F-160 — Two more tests that could not fail

- **status:** FIXED
- **severity:** 6
- **found:** cycle 18
- **symptom:** an assertion read `slotDyn` where `objectDyn` was meant, leaving the third scenario of
  its own test — the reference conversion the engine's table *does* allow — with no byte assertion at
  all, only tier-versus-tier equality (proven by changing that scenario's caller content: the render
  moved and the test still passed). And the inherited-`[DataType]` walk had no test naming it: reading
  only the extension type's own attributes left every suite green.
- **fixed by:** `2d3fab6` — both; an extension declaring `int` over a base declaring `string` now pins
  the inherit from both sides, with a third type refused on both tiers.
- **pinned by:** `AcceptedTypeTests.AnInheritedAcceptedTypeIsAcceptedAlongsideTheDeclaredOne`;
  `ObjectDefinitionBodyTests` (the corrected scenario).
- **regression check:** as F-155 and F-156.

### F-161 — A region body given its host's props but not its host's model

- **status:** FIXED
- **severity:** 1 and 3
- **found:** cycle 19 — a regression introduced by the previous commit, **and by the correction it
  added alongside a fix, not by the fix**
- **symptom:** over a model-less document whose component declares `p: string`, fills a region, and
  calls `@d(p)` from that region body, with nothing spelling `dynamic` anywhere but the `@model`
  directive:
  - `<d>{{@for(this)}}` — the engine refuses (`HED0004`, `String` against `Range`/`int`); the commit
    precompiled it and rendered a bare newline, with no diagnostic at build or at run. The body model
    was `dynamic`, so the accepted-type gate took its dynamic exemption and `@for`'s `[DataType]` never
    ran.
  - `<d>{{[@(Nope)]}}` — the engine refuses (`HED0001`, on `String`); the commit precompiled it and
    threw `RuntimeBinderException` at render. The same through the caller's content.
  - the parent commit degraded all three.
- **root cause:** a region body was rebuilt from its host's model cast, model symbol and dynamic flag
  while the host's `DynamicBodyModel` was dropped — so the region kept the host's *prop layout* and
  lost the host's *model*, which is the one combination that reaches the arm F-156 added.
- **fixed by:** `f990a9c` — carry the host's `DynamicBodyModel` into the region body. **Which of the
  two candidates was the root was decided by measurement:** adding the missing prop-read guard to the
  arm closes all three faces but also degrades a template the engine renders; carrying the model closes
  them and costs nothing. Degrade cost: zero.
- **pinned by:** `DefinitionTests.ADefinitionCalledFromARegionBodyIsTypedByTheHostsProp`,
  `.ARegionBodyReadingItsHostsPropDirectlyStillPrecompiles`,
  `.ADefinitionCalledFromARegionBodyIsRefusedOnTheHostsPropType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DefinitionTests`.
- **notes:** the durable lesson of the cycle: **a correction bolted onto a fix inherits the fix's
  credibility and carries none of its measurement. When a new arm turns out to have exactly one caller,
  suspect the caller.**

### F-162 — `Nullable<TEnum>` reached `System.Enum` through a boxing correction

- **status:** FIXED
- **severity:** 1 and 3
- **found:** cycle 19
- **symptom:** an extension re-declaring an inherited `Enum` prop as `DayOfWeek?` is `HED5008` on the
  engine and refuses the template; the generated tier accepted the layout and rendered.
- **root cause:** the adapter's Roslyn-boxing correction was written as "boxing, except a nullable into
  an interface" — `System.Enum` is a class, so `DayOfWeek? → Enum` survived it, and the CLR says false
  because `Nullable<T>`'s base chain is `ValueType` and `object` and stops.
- **fixed by:** `f990a9c` — phrased as the CLR's own question: a boxing conversion out of a
  `Nullable<T>` is assignable only where the target is on `Nullable<T>`'s own hierarchy, which subsumes
  the interface case rather than sitting beside it.
- **pinned by:** `AcceptedTypeTests.ANullableRedeclarationOnTheNullableBaseChainStillAcceptsAndRendersIdentically`
  (the neighbour: `DayOfWeek?` against a `ValueType` base accepts and renders the engine's bytes);
  rows in `src/Heddle/Language/Binding/AssignabilityCorpus.cs` driven from both tiers.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~AssignabilityCorpusSymbolTests`
  and `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AssignabilityCorpusReflectionTests`.
- **notes:** described as the adapter's **only over-acceptance** across two independent sweeps.

### F-163 — Array covariance over element types the CLR reduces to one

- **status:** FIXED
- **severity:** 4
- **found:** cycle 19
- **symptom:** the CLR compares array element types after reducing an enum to its underlying primitive
  and each signed/unsigned integer pair to one representative, so `uint[] → int[]`, `byte[] ↔ sbyte[]`,
  `long[] ↔ ulong[]` and `DayOfWeek[] → int[]` are assignable, and it propagates through the array's
  own generic interfaces (`uint[] → IList<int>`) and through jagged arrays. **Roslyn classifies none of
  these as a conversion at all**, so the adapter refused every one: twenty rows in one sweep, thirteen
  in another. The engine renders them; the generated tier degraded.
- **fixed by:** `f990a9c` — one clause: reduce both sides' element types and re-ask (the re-ask answers
  the propagated forms without naming them, and terminates because the reduction is idempotent).
- **pinned by:** `AssignabilityCorpus` array rows in both directions; direction safety is pinned from
  the other side — a mutation that accepts any two arrays of equal rank reddens `int[] → object[]`,
  `int[] → ValueType[]`, `DayOfWeek[] → Enum[]`, `char[] → ushort[]` and `bool[] → byte[]`. The
  reflection-side driver re-derives every expectation from the live CLR relation on each run, so the
  committed values are generated data.
- **regression check:** as F-162.
- **notes:** **not a regression** — the nominal comparison the adapter replaced refused the same rows —
  but it makes cycle 18's "by construction rather than by list" claim false as written.
  `AssignabilityCorpus` had four array rows, none in the disagreeing family, and no `Nullable<enum>`
  row. It has both now.

### F-164 — A declared `:: T` was never checked against what the call site passed

- **status:** FIXED
- **severity:** 1 and 3
- **found:** cycle 19
- **symptom:** `<frame>{{[@()]}} :: System.String` called `@frame(Nested)` — the engine refuses
  (`HED0004`); the generated tier precompiled and rendered the `Nested`. With `:: System.Int32` and
  `@frame(Name)` it rendered the string; with a body reading `Length` it threw `InvalidCastException`
  at render.
- **root cause:** the declaration was read as the body's model and never as a constraint on the value,
  so generated code cast the value to `T` and carried on.
- **fixed by:** `f990a9c` — **mirroring the engine's asymmetry is the whole of the fix.** `CheckTypes`
  runs against the *input model type* its accessor produced, which exists only for a value the accessor
  resolved statically (a body prop read or a member path); a literal, `this`, a computed expression, a
  chain and a path ending in a dynamic hop leave it with none, and the engine then compares the
  declared type with itself and passes.
- **pinned by:** `DefinitionTests.ADeclaredDefinitionModelTheCallSiteValueDoesNotSatisfyDegrades`,
  `.ACallFormTheEngineDoesNotCompareStillPrecompiles` (three must-precompile rows beside the three
  refusals; a mutation that drops the exemptions reddens exactly those three).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DefinitionTests`.
- **notes:** a stricter rule than the engine's would take `@frame(5)`, `@frame(this)` and
  `@frame(len(Name))` off the precompiled tier. Resist tightening this.

### F-165 — An unreachable guard arm, kept and labelled

- **status:** NOT-A-DEFECT (documented decision)
- **severity:** —
- **found:** cycle 19
- **detail:** the export guard (F-157) checks both names an emitted call spells, the method and its
  container. The container arm has no reachable path: naming an obsolete-error type in
  `[ExportFunctions]` is `CS0619` in the assembly that declares the export, and no pragma there
  suppresses it. It stays, so the two names are guarded alike, with a clause saying so.
- **notes:** the stated reason — **an arm that looks live and is not is a worse trap than one that is
  labelled.** Contrast F-053, where a dead guard was deleted; the difference is that this one is
  symmetric with a live arm.

### F-166 — A `bool`/`bool?` bitwise operand pair has no diagnostic of its own

- **status:** KNOWN-OPEN (partially closed)
- **severity:** 3
- **found:** during the documentation survey that preceded cycle 1. **The id is out of chronological
  order** — it was catalogued into this register last, and is noted rather than renumbered.
- **symptom:** `NativeExpressionCompiler`'s bool arm passes the mismatched pair straight to
  `Expression.And`, whose `InvalidOperationException` escapes to the generic handler. Every comparable
  illegality in the tier is a positioned `HED1008`. The generator's shared table already pins this as
  the agreed verdict, so it is a *matched* defect, not drift.
- **partially fixed by:** `be7b4c1` — the catch-all now raises `HED0005` with a real message and
  position (F-060), so the shape is no longer id-less. It still does not raise `HED1008`.
- **pinned by:** `NativeOperatorRulesTests` rows for the shape (id and message content).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~NativeOperatorRulesTests`.
- **notes:** escalated deliberately: it needs a ruling, not an edit, because both tiers agree.

### F-167 — `floor(3)` / `ceil(3)` / `round`-on-`int` are compile errors

- **status:** KNOWN-OPEN
- **severity:** 3 (both tiers refuse; the documentation said they work)
- **found:** during the documentation survey that preceded cycle 1 (id out of chronological order, as
  F-166).
- **symptom:** the built-ins are `double`/`decimal`-only, and `int→double` and `int→decimal` tie under
  the flat Pareto rank, so an `int` argument is `HED1013` (ambiguous).
- **fixed by:** — the behaviour change (C# betterness in the runtime binder) is a filed next-window
  candidate; the documentation was corrected instead.
- **regression check:** none — this is current behaviour, not a divergence between tiers.
- **notes:** the same shape as the shipped `min(1, 2u)` counter-example. Do not "fix" it inside a
  review cycle; it widens accepted behaviour.

### F-168 — A shipped sample still uses removed MSBuild item metadata

- **status:** KNOWN-OPEN
- **severity:** off-scale (sample correctness)
- **found:** during the documentation survey that preceded cycle 1 (id out of chronological order).
- **symptom:** `samples/codegen-t4-successor/CodegenT4Successor.csproj` carries
  `<HeddleTemplate Include="templates\report.heddle" Name="BuildReport" />`; `Heddle.Generator.props`
  no longer reads `Name`, so the sample silently loses its intended key.
- **fixed by:** — escalated, not fixed. It is a live, golden-checked user-facing artefact.
- **regression check:** grep `samples/**/*.csproj` for `Name=` on `HeddleTemplate` items.

---

## Defect classes

**Read this section first.** Each class groups findings that share an underlying mistake, and says
whether anybody ever enumerated the class exhaustively. Where a class is not enumerated, the next
member is findable by construction — write the enumeration before fixing the reported instance.

### A — the emitter keys on a *spelling* where the engine keys on a resolved answer

**Members:** F-037 (cycle key = raw path text), F-063 (import identity vs the reader's own
normaliser), F-102 (path memo keyed on a display string), F-113 (body cache keyed on a display string),
F-120 (a `?` suffix the shared grammar does not have), F-123 (dotted `@model` matched by suffix),
F-156 (`:: object` vs the word `dynamic`).

**Enumerated?** **No.** Each was fixed where it was found. Nobody has listed every place a *string* is
used as the identity of a thing the engine resolves to a symbol, a type or a normalised key. The
cheapest sweep: grep for dictionary keys and comparisons built from `ToDisplayString`, fully-qualified
name text, or raw directive text, and ask what the engine compares in the same position.

### B — a reader that does not consult the prop layout, and a context that does not carry it

**Members:** F-131 (`@list` body nested in a definition), F-136 (the expression writer), F-137 (the
computed call-site typing), F-141 (caller content — the *context* half), F-142 (the prop-argument
check), F-143 (native expressions refused before the layout was consulted), and F-161 as the
generalisation (a context carrying the layout but not the model).

**Enumerated?** **Yes, for the layout — and this is the series' one success story.** Cycle 16 wrote out
two tables before fixing anything: every `BodyContext` construction site in `TemplateEmitter.cs` and
whether the layout travels with it (Table A, ten rows), and every reader of a path's first segment and
whether it tries the layout first (Table B, twelve rows). Cycle 17 had both re-derived independently by
two reviewers; both came back complete with no wrong verdict — the first completeness claim in the
series to survive two independent re-derivations. **Three cycles of "fixed it, and the same defect
turned up one path over" ended when the lists were written out in full.**

**The residual, and it is live:** the tables answer *does this context carry the prop layout*. They do
not answer *what else does a context carry*. F-161 is exactly that gap — a region body carried the
layout and dropped the model. **A future cycle should extend Table A with a column per piece of state a
`BodyContext` holds** (`Props`, `RegionHostProps`, `ModelSymbol`, `DynamicBodyModel`, slot mode, the
dynamic flag), because only the first column has ever been checked.

### C — can generated code in the consumer's assembly name this type?

**Members:** F-098 (ref struct as a model), F-104 (`[Obsolete(error:true)]` type/member), F-105 (static
class), F-110 (`void`, unbound generics, pointers, error-obsolete containing and property types),
F-111 and F-127 (a spelling that resolves to no symbol), F-119 (type arguments), F-122 (a property type
that is merely unusable), F-125 (an enclosing type's arguments), F-157 (an obsolete-error *method*),
F-101 (**open** — a hop's internal property type).

**Enumerated?** **Yes for `TypeKind`, no for positions.** Cycle 11 replaced the kind-by-kind widening
with two predicates, and cycle 14 made the kind table a function of `TypeKind` with a row asserting the
rows cover the enum — so a kind Roslyn adds later arrives with no verdict and the theory does not
compile past it. Recursion covers array elements, pointer elements, type arguments and containing
types.
**What was never enumerated is the set of *positions* where the emitter spells a name**: cycle 9 fixed
four (`@model`, definition, slot, prop) ad hoc, cycle 13 found the entry-point parameter still written
verbatim, and cycle 18 found the *method* of an exported call had never been asked. F-101 is the known
survivor. **Write the position list — every place the emitter writes an identifier into `.g.cs` — and
check each against `Classify`.**

### D — a rule implemented for the one case in front of the author

**Members:** F-019 (type resolution fixed, C#-tier metadata not), F-062 (a guard for the disk reader's
exceptions over a public seam), F-091/F-092 (member fixed, type assumed to behave the same and it does
not), F-095 (native expressions wrapped, embedded C# not), F-096 (metadata references, not compilation
references), F-097 (`@model()` but not definition/slot/prop), F-138 → F-149 (`@list`'s accepted type
written out; every other extension unchecked), F-148 (one shape of return type, because the descriptor
could only spell that shape), F-124 → F-130 → F-150 (the definition body, then every call form, then
caller content).

**Enumerated?** **Partly, and by two different techniques.** The `[DataType]` case is now closed *by
construction* — the check reads the attribute off the bound extension, so a new extension is covered
without anybody listing it. Same for call return types (read the overload's declared type) and
assignability (ask the adapter). **That is the durable answer this class has: read the authority
instead of a projection of it.** The cases still closed only by enumeration — which paths wrap in
`unchecked`, which reference kinds a visibility check understands, which positions type a body — have
no such guarantee.

### E — "cannot say" is the exempting answer

**Members:** F-059 (`Unknown` from the fold meant "emit as written"), F-121 (`null` had a type on one
tier and not the other), F-130 (the emitter's "I cannot type this" read as "the engine has no type"),
F-148 (a kind funnel that answers "cannot say" for every non-primitive), F-155 (a declared `int?` that
accepted nothing), F-161 (a body model of `dynamic` took the accepted-type gate's dynamic exemption).

**Enumerated?** **No, and this is the class most likely to yield the next silent-wrong-output finding.**
Every gate in the emitter exempts a value it cannot type; nothing lists those gates or asks, per gate,
what the engine does with the same value. The record's own lesson: *where two tiers must agree, every
"cannot say" belongs on the refusing side of the branch, and the way to keep that from costing the
precompiled tier is to shrink the set of things that cannot be said, not to widen what "cannot say" is
allowed to mean.* **Suggested sweep: enumerate every call site that treats a null/"unknown" type answer
as permission to proceed.**

### F — the fix that introduced the next defect

**Chains:** F-056 → F-069 → F-072/F-073 → F-082 (four commits, each introducing the next, ending in a
silent-wrong-output regression that the intermediate commit had made *loud*); F-023 → F-033 and F-034;
F-005 → F-020; F-050 → F-067; F-057 → F-058 and F-071; F-099 → F-102; F-074 → F-093; F-112 → F-117;
F-124 → F-130; F-136 → F-142; F-145 → F-153 → F-158; F-156 → F-161.

**Enumerated?** Not a code class — a process property, and the strongest single signal in this
register. The record notes it four separate times; in cycle 10, **four of eight findings were
introduced by the commit under review.** Practical consequence for a reviewer: *review the previous
cycle's commit first*, and specifically the arms and guards it added rather than the defect it fixed.

### G — tests that cannot fail

**Sub-patterns, with members:**
- *the test reads the production constant it is checking*: F-046, F-050, F-058, F-067.
- *a degrade-only assertion, which any blanket refusal satisfies*: F-036, F-118, F-132.
- *a row answered by a different arm than the one it names*: F-126, F-139, F-146, F-133.
- *the fixture degrades for an unrelated reason, so the code under test never runs*: F-127, F-103.
- *an exemption satisfied by any exception at all*: F-090 (`Assert.ThrowsAny<Exception>`, including the
  harness falling over), F-076.
- *the property was never pinned at all, and mutation proved it*: F-021, F-028, F-029, F-030, F-041,
  F-044, F-060, F-088, F-090, F-100, F-116, F-160.

**Enumerated?** **No — but the instrument is known and works:** mutate one production arm at a time and
watch the suite. Cycles 12, 13 and 16 each ran a full mutation matrix over a verdict table (nineteen
arms, then seventeen/eighteen reddening, then a seven-kind matrix) and each found real dead rows.
Nothing runs that matrix routinely.

### H — the instrument did not have a category for the defect

**Members:** F-017 (a green report reading stale golden bytes), F-077 (a harness whose answer depended
on test order), F-081 (a declared TFM that runs nothing and exits 0), F-151 (bytes moved while still
precompiling — invisible to a degrade sweep *and* to a differential test whenever the engine refuses
the template), F-159 (newly precompiling), F-155 (the corpus was byte-identical and closed nothing,
because no fixture declared the feature under test).

**Enumerated?** **The sweep now has three counts** — newly degrading, bytes moved while still
precompiling, newly precompiling — and that is believed complete for *corpus-visible* change. It is not
complete for behaviour the corpus cannot see: **the corpus contains no `[DataType]`-declaring host
extension until cycle 18, and had no array or `Nullable<enum>` assignability row until cycle 19.** A
sweep over a corpus that lacks a shape proves nothing about that shape, and "the corpus is
byte-identical" has twice been reported as evidence when it was not.

### I — an engine or CLR relation re-implemented instead of asked

**Members:** F-117 (a rule guessed at rather than measured), F-130 (typing mirrored from the shared
operator tables — the *right* version of this), F-148 (a lossy descriptor used as a type answer),
F-149 (`Type.IsAssignableFrom` written out as four cases), F-155 (the same, with a doc comment
asserting fidelity), F-162 and F-163 (the adapter's own CLR corrections, wrong in both directions).

**Enumerated?** **The single-source rule is now in place** (one adapter answers assignability, driven
from a corpus generated from the live CLR on the reflection side), but the adapter's corrections have
been wrong twice in two cycles, in opposite directions. The corpus *is* the enumeration; check that any
new relation family has rows in it before believing a sweep.
The stated rule: *when a rule mirrors an engine predicate, prefer calling the one adapter that already
answers it over restating it; when restating is unavoidable, write down the predicate's source
expression rather than a prose enumeration of the cases someone thought of.*

### J — shared mutable state, publication and lifetime (engine side)

**Members:** F-001, F-005, F-020, F-034, F-043, F-064, F-065, F-070, F-074/F-093, F-085, F-086, F-087,
F-088, F-107.

**Enumerated?** **No.** Two orderings in this area are explicitly unpinnable (recorded in
`AssemblyRegistrationTests` and `PreparseCacheGenerationTests`), and the answer taken was to remove the
reversible orderings rather than to test them. A racing test here passes by luck when the code is
wrong — do not add one.

### K — unbounded input

**Members:** F-025 (import cycles), F-026 (parse depth), F-037 (import chain length), F-038 (lexer mode
stack), F-057 (import fan-out), F-071 (fan-out charged per path).

**Enumerated?** **The three dimensions are known** — depth, fan-out, cycle — and each is bounded and
reported with a diagnostic. F-027 is the one residual and is not reachable from a bound. When adding a
recursive walk, the question already has an answer: bound it by count, measure the bound in **Release**
on a **1 MB** stack, and assert the value.

### L — documentation and gate defects

**Members:** F-006 to F-015, F-045, F-066, F-068.

**Enumerated?** Gates now exist for links and citations, diagnostic-id presence, option names and
defaults, and public-API mentions, and each was demonstrated red on its first run. Two documentation
classes have no gate and recur: *a description that is complete for some of a diagnostic's causes*
(F-009, F-066), and *a prose claim of completeness over a list* (F-015, F-155's doc comment).

---

## Known-open register

Do not re-report these. Each is deferred with a stated reason; if you intend to close one, read its
entry first, because several were deliberately not fixed rather than missed.

| id | one line | severity | why deferred |
| --- | --- | --- | --- |
| F-027 | prefix-operator runs of several thousand exhaust ANTLR's own lookahead | 3 | upstream (antlr/antlr4#744); no fixed count can see it, and neither a listener nor the grammar can reach it. Published numbers are indicative, not contractual |
| F-079 | `Path.Combine` rejects characters on .NET Framework that .NET Core accepts | 3 | the development box cannot make it throw; the mitigation in place is reasoning, not evidence |
| F-080 | on `netstandard2.0` an assembly with no file yields no metadata reference | 3 | there is no API to fix it with (`TryGetRawMetadata` does not exist there); no `netstandard2.0` path executes on this box at all |
| F-081 | a declared target framework runs zero tests and the run exits 0 | 6 | build wiring, not engine code; named the highest-value item on the platform page and still not done |
| F-101 | a hop whose *property type* is internal still emits a name the consumer cannot compile | 2 | checking it where the member check sits would falsely degrade the ordinary `m?.Inner?.Name` shape; doing it properly needs a form-aware check at the point of emission. **Status uncertain — later cycles may have subsumed it; verify before reporting** |
| F-140 | two type-kind verdict rows that cannot be honestly pinned (`Structure`, `Extension`) | 6 | `Structure` is a Roslyn alias no change here can move; `Extension` is not declared by the Roslyn the generator compiles against, so a case for it is `CS0117` |
| F-147 | a native expression reading the element's own member inside an `@list` body degrades | 4 | typing the writer off `DynamicBodyModel` is a change of a different shape; left for a later cycle |
| F-154 | three caller-content shapes under a `:: dynamic` callee degrade where the engine renders (a native expression, a function call, an `@if`) | 4 | reported rather than hidden; not attempted |
| F-166 | a `bool`/`bool?` bitwise operand pair has no `HED1008` of its own | 3 | both tiers agree, so it is a matched defect needing a ruling rather than an edit. Partially closed: it now carries `HED0005` with a real message and position |
| F-167 | `floor(3)` / `ceil(3)` / `round`-on-`int` are `HED1013` | 3 | the fix (C# betterness in the runtime binder) is a filed next-window candidate; it widens accepted behaviour |
| F-168 | a shipped sample still uses removed MSBuild item metadata | off-scale | escalated to the owning effort; the sample silently loses its intended key |

**Also open, and recorded inside their entries rather than as separate findings:**

- **Two `AssemblyHelper` orderings cannot be pinned** (F-100, F-093). They are claims about what no
  concurrent caller can observe; a racing test passes by luck when the code is wrong. The mitigation
  was to remove the reversible orderings, not to test them. Written in plain words in
  `AssemblyRegistrationTests` and `PreparseCacheGenerationTests`.
- **The no-load pin cannot catch a one-shot startup walk** (F-021), which has already run before the
  probe can be built. Catching it needs a child process, which no suite has.

### Fixed, but the pin is weak — candidates for the next cycle

These are recorded as FIXED and their tests **pass against a build in which the fix is reverted or
degraded**. Each is a place where a regression would be invisible.

| id | what the pin actually holds | why it is weak |
| --- | --- | --- |
| F-001 (registry half) | that reading registered names while registering does not throw | the commit itself says the registry fix "did not reproduce under a pre-fix mutation in 600 compiles, so its test is a guard, not a demonstration" |
| F-023 | that a repeated failing expression reports the same diagnostics | removing the replay leaves the suite green; the test says so |
| F-033 | that a repeated failing expression is reported at each caller's own position | replaying a fixed position leaves the suite green, because two documents sharing an expression do not reliably share a cache entry |
| F-070 | that a parse begun inside an import reader is independent of the outer parse | removing the isolation leaves it green — the fixture cannot reach the nesting that reproduced the defect. The review reproduced it from a deeper outer parse; the fixture does not |
| F-002, F-003, F-039 | nothing | fixed with no test at all |
| F-028 | not recorded | the fix reverted green when cycle 3 checked it, and no later commit is recorded as pinning it |
| F-145 / F-153 | n/a | a guard added on suspicion with no demonstrated red — later measured and **removed** (F-158). Recorded here as the precedent: an unmeasured guard is a cost, not a safety margin |
