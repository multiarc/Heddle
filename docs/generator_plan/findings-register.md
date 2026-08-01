# Heddle generator review series — findings register

**What this is.** A durable, per-finding record of what the review cycles over `src/Heddle.Generator`
(and the engine code those cycles reached into) found, fixed, deferred or retracted. It is assembled
from two sources that disagree in places:

- `docs/generator_plan/phase-8-docs-sweep.md` — the per-cycle prose record, written after each cycle.
  Believed over a commit message wherever the two conflict: several commit messages were later
  measured wrong and corrected there, and a future reader may hit the uncorrected message first.
- the commit messages of the review series, `cd3a665 .. 801dfde` on this branch.

Test names and their doc comments (`src/Heddle.Tests`, `src/Heddle.Generator.Tests`,
`src/Heddle.Generator.IntegrationTests`, `src/Heddle.LanguageServices.Tests`) and
`src/TestCorpus/CorpusIntent.cs` were read to fill the **pinned by** field.

**This register is class-keyed, not a log.** The unit of a finding is a CLASS, not an instance. Where
several cycles found the same defect keyed on the same wrong predicate at different call sites, the
entries were merged: one entry, one root cause, one row per instance, every instance keeping its own
repro, its own `file:line`, its own commit and its own regression check. **No id was deleted.** Every
id absorbed into another entry has a redirect row in *Merged and retired ids*, and appears again in
the **absorbs** field of the entry that holds it, so `F-0xx` is always findable by search.

**How to use it.** Read it at the *start* of a review cycle, in this order:

1. **Defect classes**, near the end. Four cycles in a row found one member of a class, fixed it, and
   the next cycle found another member. That section says which classes were enumerated exhaustively
   and which were not. Start in a class with unenumerated members. It now also records what
   consolidation revealed: several classes have far more members than they claimed.
2. **Known-open register**, last. Do not re-report these; each is deferred with a stated reason.
3. The individual entries, to decide whether a new finding is a duplicate. Every entry carries a
   **regression check** — the shortest thing you can run to confirm it is still fixed. That field is
   the point of the document: nobody has re-run the repro of a finding fixed ten cycles ago. A merged
   entry carries *all* of its members' checks; run the ones in your area, not just the first.

> This register is documentation. It may cite code, tests and commits. **Code and tests must never
> cite this register** — no finding ids in comments or test names. The repo already carries the rule
> that code may not cite documents (F-068 is the sweep that swept it); this document does not create
> an exception to it.

**Reading the fields.**

- **severity** uses the series' own scale: `1` silent wrong output · `2` generated code breaks the
  consumer's build · `3` an error on one tier where the other renders · `4` silent loss of the
  precompiled tier for a working template · `5` leaks · `6` tests that cannot fail. Some findings are
  documentation or process defects and sit off that scale; those say so instead of borrowing a number.
  A merged entry states the range and each row carries its own.
- **absorbs** lists the ids merged into this entry. Each is a row of the entry, not a deletion.
- **found** names the cycle. Cycles 1–3 and 8–19 have prose sections in the record. **Cycles 4–7 have
  no prose section at all** and are reconstructed from commit messages alone, which is why entries
  there often say "not recorded" for role and repro detail.
- **regression check** assumes the repo root. `-f net8.0` keeps runs short; note the declared `net6.0`
  container does not start on the development box (F-081) and a run that silently skips it still
  exits 0. **`src/Heddle.LanguageServices.Tests` and `src/Heddle.Tool.Tests` are `net10.0`-only** —
  never pass `-f net8.0` to those two, it exits `NETSDK1005` and runs zero tests. Checks in this file
  are written accordingly; keep it that way when you add one.
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
| 21, 22 | *(no prose section; recorded here)* | see the entries |

**179 ids catalogued, in 79 entries.** 99 ids are rows of a merged class-level entry; one (F-115) is
retired to the table at the end. By status of the surviving entries:

| status | entries | ids they hold |
| --- | --- | --- |
| FIXED | 62 | 158 |
| KNOWN-OPEN | 11 | 11 |
| NOT-A-DEFECT | 5 | 6 |
| SUPERSEDED | 1 | 3 |
| retired | — | 1 (F-115) |
| **total** | **79** | **179** |

F-178 is counted FIXED and also appears in the known-open register, because one direction of it is
fixed and the other is deferred; that is why that table has twelve rows.

## Contents, by status

### FIXED (80 entries, holding 178 ids)

| id | absorbs | severity | title |
| --- | --- | --- | --- |
| F-001 | — | 1 | Unsynchronised publication of a shared lookup table under a legal concurrent host call |
| F-002 | F-003 | 3 | The extension scan: marked scanned before the work succeeded, and `GetTypes()` unguarded |
| F-004 | F-019 | 3 | `Assembly.Location` used as the test for "the host loaded this" — in both components that ask |
| F-005 | F-020 | 3 | Observation staleness: load order decided what resolved, then a count gate could not tell |
| F-006 | F-011, F-012, F-013, F-014 | 6 / off-scale | Gates whose predicate never reaches the text they claim to check |
| F-007 | F-008 | off-scale | Documentation asserting what the code no longer does |
| F-009 | F-066 | off-scale | A diagnostic documented by some of its causes |
| F-010 | F-031, F-076 | 6 | The corpus parity gate did not cover what it claimed to cover |
| F-015 | — | off-scale | A checkable completeness claim stated over an incomplete list |
| F-016 | — | 6 | Test names claiming more than the test checks |
| F-017 | — | off-scale | A green report read stale bytes because the capture path is project-relative |
| F-018 | — | off-scale | A work item reported met on both halves of its own done-when, and was not |
| F-021 | — | 6 | "The engine loads nothing" was pinned for scanning only |
| F-022 | — | 2 | A hex formatter dropped the leading zero of every byte below 0x10 |
| F-023 | F-033 | 3, 1 | What a cached compile entry carries: the diagnostics, and not the caller's position |
| F-024 | F-035, F-040, F-047, F-048, F-049, F-054, F-059, F-078 | 2 and 4 | The constant fold's model of C# constant evaluation, wrong seven ways |
| F-025 | F-037, F-044, F-063 | 3 | The import graph: cycles, chain depth, and what makes two imports the same document |
| F-026 | F-032, F-042, F-045, F-051, F-052, F-075 | 3 | The parse depth bound: absent, misplaced, too high, measured in the wrong configuration |
| F-028 | — | off-scale | A generator diagnostic reported at `Location.None` |
| F-029 | — | 1 | An equality comparer whose hash disagreed with its equality |
| F-034 | F-030, F-074, F-088, F-093 | 3 | The preparse cache's staleness rule: failures, successes, ordering and the epoch |
| F-036 | F-041, F-118, F-132 | 6 | A degrade-only assertion cannot distinguish a rule from a blanket refusal |
| F-038 | — | 3 | A six-character template threw out of the compile |
| F-039 | — | off-scale | A reference built twice on every cache miss |
| F-043 | F-065, F-070 | 3 | Per-parse import state that outlived or was shared across a parse |
| F-046 | F-050, F-058, F-067 | 6 | A test that reads the production constant it is checking |
| F-053 | F-089 | 6 | A guard, and a branch, that could never fire |
| F-055 | F-056, F-069, F-072, F-073, F-082, F-083 | 1 and 2 | The emitted member-hop form: receiver duplication, `?.` propagation, and ref structs |
| F-057 | F-071 | 3, 4 | Import fan-out: unbounded, then charged per path instead of per document |
| F-060 | — | 3 | A last-resort handler with no id and a message built from an empty name |
| F-061 | F-062 | 3 | An unreadable `@<<` import threw out of the parse — and the guard was scoped to one reader |
| F-064 | — | 5 | A cache entry holding a `Type` from a collectible context, evicted only on failure |
| F-068 | — | off-scale | Code comments citing documents |
| F-077 | F-094 | 6 | A differential harness whose answer depended on test scheduling |
| F-084 | F-095 | 1 | Generated arithmetic inherited the consumer's overflow-checking setting — on both paths |
| F-085 | — | 3 | A failed render permanently poisoned a compiled template |
| F-086 | — | 5 | A cache whose key can never repeat |
| F-087 | F-107 | 3, 6 | An assembly that lost a name collision was retired for good |
| F-090 | F-100, F-160 | 6 | Properties claimed and pinned by nothing, found by mutation |
| F-091 | F-092, F-096, F-097, F-103 | 3 and 2 | An accessibility gate narrower than its defect: one member kind, one reference kind, one position |
| F-098 | F-101, F-105, F-110, F-119, F-122, F-125 | 2 | Can generated code in the consumer's assembly name this type? Answered kind by kind, position by position |
| F-099 | — | 3 | A probe that reverted to the original error on an ambiguous name |
| F-102 | F-113 | 1 | A cache key missing the term that decides the answer |
| F-104 | F-157 | 2 | `[Obsolete(…, error: true)]` is invisible to reflection and fatal to the consumer's build |
| F-106 | F-116, F-121, F-135 | 3 and 1 | The slot-value type check, implemented for some value forms |
| F-108 | — | 6 | Test suites that wrote 58 probe assemblies and deleted none |
| F-111 | F-120, F-123, F-127, F-129, F-134 | 2, 3 | The `@model` spelling: what the grammar accepts, what the gate reports, what is emitted |
| F-112 | F-117, F-124, F-130, F-150, F-156, F-161, F-170 | 1, 3, 4 | What model a body gets from its call site — the most-revised rule in the series |
| F-126 | F-133, F-139, F-146 | 6 | Verdict rows answered by an arm other than the one they name |
| F-131 | F-136, F-137, F-141, F-142, F-143 | 1, 2, 3, 4 | A reader that does not consult the prop layout, and a context that does not carry it |
| F-138 | F-149 | 1 | An extension's accepted type, checked for the one extension in front of the author |
| F-144 | — | 1 and 3 | What a chain hands the next link is text, not the producer's value |
| F-148 | F-172 | 1 and 3 | "Cannot say" exempts every gate — a kind funnel, and an element type that cannot be named |
| F-151 | F-159 | 6 | Sweep categories that could not hold the change the cycle made |
| F-155 | F-162, F-163 | 4, 1, 3 | Reflection's assignability claimed, not reproduced — and the adapter's corrections, wrong twice |
| F-164 | — | 1 and 3 | A declared `:: T` was never checked against what the call site passed |
| F-169 | — | 3 | The callee's body was built before the caller's content; the engine compiles them the other way round |
| F-171 | F-174 | 1, 6 | The emitter's body identity had a term the engine's does not, and two that decided nothing |
| F-173 | — | 3 and 4 | A region that declares its own slot never entered slot mode |
| F-176 | — | 2 | A host extension type the consumer's assembly may not name, written into `.g.cs` |
| F-178 | — | 2 and 3 | The generator's import identity is a template key; the engine's is a canonical disk path |
| F-180 | — | 2 and 3 | The `@using` namespace filter rejected namespaces that do exist, and treated a `@using` as inert |
| F-181 | — | 3 and 2 | A backslash separated path segments on every platform, and a `.` was dropped only as a side effect of `..` |
| F-182 | — | 2 | A public `[ExportFunctions]` container nested in an internal one failed the whole build |
| F-183 | — | 2 | A sole exported overload was bound on arity alone |
| F-184 | — | 1 | `CallSiteValueType` had no arm for an embedded-C# call parameter |
| F-185 | — | 3 and 4 | The build tier's name index dropped the leading dot of a namespace-less type, and had no assembly-qualified arm |
| F-188 | — | 4 | The constant-overflow refusal went stale when the emission became `unchecked` |
| F-189 | F-187 | 1 | The fold reported agreement on a number and called it agreement on a type |
| F-190 | — | 1 and 2 | The one C# string literal the emitter wrote by hand |
| F-191 | — | 2 | Nothing asked whether an embedded C# expression compiles |
| F-192 | — | 2 and 4 | A `@using` body judged by a name walk rather than by whether its directive compiles |
| F-193 | — | 2 | A `#line` file name is a `pp_string` and was written unescaped |
| F-194 | — | 2 | `LiteralFormatter` had no non-finite arm, and the guard lived on the other caller |
| F-195 | — | 3 | The hosted resolver could not read a template from disk on Linux or macOS |
| F-196 | — | 4 | The build tier's signature key told apart what reflection cannot |
| F-197 | — | 4 | The refusal of `chained` and `root` was a word-boundary regex over raw text |
| F-202 | — | 2 | A probe tree parsed with the defaults cannot be grafted onto a consumer compilation that sets parse options |
| F-203 | F-081 | 6 | A test filter that matches nothing exits 0, so every stale regression check passed |
| F-200 | — | 3 and 2 | A `@using` alias and a `using static` bound nothing, because the resolver read every body as a namespace |

### KNOWN-OPEN — do not re-report (15)

| id | severity | title |
| --- | --- | --- |
| F-027 | 3 | A run of several thousand prefix operators exhausts ANTLR's own lookahead |
| F-079 | 3 | `Path.Combine` rejects characters on .NET Framework that .NET Core accepts |
| F-080 | 3 | On `netstandard2.0` an assembly with no file yields no metadata reference |
| F-140 | 6 | Two verdict rows that cannot be honestly pinned |
| F-147 | 4 and 3 | A native expression reading the element's own member inside an `@list` body degrades — and a plain path to a member it lacks throws at render |
| F-154 | 4 | Three caller-content shapes degrade under a `:: dynamic` callee |
| F-166 | 3 | A `bool`/`bool?` bitwise operand pair has no diagnostic of its own |
| F-167 | 3 | `floor(3)` / `ceil(3)` / `round`-on-`int` are compile errors |
| F-168 | off-scale | A shipped sample still uses removed MSBuild item metadata |
| F-178 (open half) | 3 | The generator resolves an import spelled `/lib.heddle`, `~/lib.heddle` or `lib` where the engine refuses all three |
| F-179 | 3 | An import-only library compiled standalone reports an error it would never raise in place |
| F-186 | 2 | An untypeable argument still binds a sole exported overload, and the emitted call does not compile |
| F-198 | 3 | The embedded-C# probe sees consumer internals the engine's standalone compile cannot |
| F-199 | 3 | A hosted `GetTemplate` cannot load the file its own search found |
| F-201 | 6 (the pin) / 3 (the behaviour) | A ref-struct model throws a raw `InvalidCastException`, and only in Release, and only in a full-suite run |

### SUPERSEDED — the fix or guard was later replaced, removed or reversed (6 ids)

| id | where it lives now |
| --- | --- |
| F-074, F-093 | rows of **F-034** — the epoch that was added and then removed as redundant |
| F-145, F-153, F-158 | **F-145**, the merged entry: a gate added on suspicion, measured, reversed |
| F-115 | retired — see *Retired entries* at the end |
| F-177 | superseded by **F-180** — the symptom stays fixed, but the fix's premise was wrong in two directions |

### NOT-A-DEFECT — measured and dismissed; do not re-report (6 ids, 5 entries)

| id | absorbs | title |
| --- | --- | --- |
| F-109 | — | `ThreadLocal` per definition, reported as an unbounded slot table |
| F-114 | — | A reported repro that does not compile |
| F-128 | — | A root reference as a slot value, reported as a latent hole |
| F-152 | — | Two constructs examined rather than assumed |
| F-165 | F-175 | Two unreachable arms, one kept and labelled and one deleted |

## Merged and retired ids — redirects

Every id that no longer heads its own section. Search for the id and you land here; the entry named
in the second column holds its repro, its citations and its regression check as a row.

| id | → | id | → | id | → |
| --- | --- | --- | --- | --- | --- |
| F-003 | F-002 | F-008 | F-007 | F-011 | F-006 |
| F-012 | F-006 | F-013 | F-006 | F-014 | F-006 |
| F-019 | F-004 | F-020 | F-005 | F-030 | F-034 |
| F-031 | F-010 | F-032 | F-026 | F-033 | F-023 |
| F-035 | F-024 | F-037 | F-025 | F-040 | F-024 |
| F-041 | F-036 | F-042 | F-026 | F-044 | F-025 |
| F-045 | F-026 | F-047 | F-024 | F-048 | F-024 |
| F-049 | F-024 | F-050 | F-046 | F-051 | F-026 |
| F-052 | F-026 | F-054 | F-024 | F-056 | F-055 |
| F-058 | F-046 | F-059 | F-024 | F-062 | F-061 |
| F-063 | F-025 | F-065 | F-043 | F-066 | F-009 |
| F-067 | F-046 | F-069 | F-055 | F-070 | F-043 |
| F-071 | F-057 | F-072 | F-055 | F-073 | F-055 |
| F-074 | F-034 | F-075 | F-026 | F-076 | F-010 |
| F-078 | F-024 | F-082 | F-055 | F-083 | F-055 |
| F-088 | F-034 | F-089 | F-053 | F-092 | F-091 |
| F-093 | F-034 | F-094 | F-077 | F-095 | F-084 |
| F-096 | F-091 | F-097 | F-091 | F-100 | F-090 |
| F-101 | F-098 | F-103 | F-091 | F-105 | F-098 |
| F-107 | F-087 | F-110 | F-098 | F-113 | F-102 |
| F-115 | retired | F-116 | F-106 | F-117 | F-112 |
| F-118 | F-036 | F-119 | F-098 | F-120 | F-111 |
| F-121 | F-106 | F-122 | F-098 | F-123 | F-111 |
| F-124 | F-112 | F-125 | F-098 | F-127 | F-111 |
| F-129 | F-111 | F-130 | F-112 | F-132 | F-036 |
| F-133 | F-126 | F-134 | F-111 | F-135 | F-106 |
| F-136 | F-131 | F-137 | F-131 | F-139 | F-126 |
| F-141 | F-131 | F-142 | F-131 | F-143 | F-131 |
| F-146 | F-126 | F-149 | F-138 | F-150 | F-112 |
| F-153 | F-145 | F-156 | F-112 | F-157 | F-104 |
| F-158 | F-145 | F-159 | F-151 | F-160 | F-090 |
| F-161 | F-112 | F-162 | F-155 | F-163 | F-155 |
| F-170 | F-112 | F-172 | F-148 | F-174 | F-171 |
| F-175 | F-165 | | | | |

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

### F-002 — The extension scan: marked scanned before the work succeeded, and `GetTypes()` unguarded

- **status:** FIXED
- **absorbs:** F-003
- **severity:** 3 (both)
- **found:** cycle 1, adversary
- **the predicate:** the host-facing extension scan treated "we looked at this assembly" and "we
  successfully registered what is in it" as the same fact, in two different routines.

  | id | symptom | root cause | fixed by |
  | --- | --- | --- | --- |
  | F-002 | `RegisterExportedExtensions` marked an assembly scanned *before* registering it, so a host that caught a registration failure and retried got a silent no-op | ordering inside `RegisterExportedExtensions`; the mark now follows success | `cd3a665` |
  | F-003 | `LoadExtensions` called `GetTypes()` unguarded, so an `[ExportExtensions]` in its parameterless form threw `ReflectionTypeLoadException` out of the host's startup call because of one unresolvable type reference | the same call in `ReflectionHelper` already skipped what cannot load; this one did not | `cd3a665` — skips what cannot load |

- **pinned by:** nothing, for either. No test in the series is named for either.
- **regression check:** none available. Read the mark/register ordering in `RegisterExportedExtensions`
  and confirm the mark is after the successful registration; read the guard around `GetTypes()` in
  `LoadExtensions`.
- **notes:** **both fixed WITHOUT a test**; both are rows of the weak-pin table at the end.

### F-004 — `Assembly.Location` used as the test for "the host loaded this" — in both components that ask

- **status:** FIXED
- **absorbs:** F-019
- **severity:** 3 (in a single-file or WASM publish the engine resolved nothing at all)
- **found:** cycle 1 (adversary) and cycle 2; both measured against a real `PublishSingleFile` host
- **the predicate:** the observation filter and the reference provider each keyed on
  `Assembly.Location` instead of on what that check meant to exclude. `Location` is empty for the
  *entire application* in a single-file or WASM publish. **Two components asked the same wrong
  question and only one was fixed first** — the archetype of "a record says closed, one half was
  never done".

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-004 | the engine observed no assemblies; `@model System.DateTime` failed to resolve and `Register(yourAssembly)` — the documented migration step — did not repair it. The SDK had been emitting `IL3000` on that line throughout | `7fd1999` — the filter now excludes dynamic assemblies, collectible and custom load contexts, and the engine's own emitted expression assemblies (tracked explicitly, since the ALC check does not exist on `netstandard2.0`) |
  | F-019 | 23 observed assemblies, **0** metadata references, every C#-tier template failing with `Predefined type 'System.Object' is not defined`. The language service hit the same gate from the other side, because it byte-loads model assemblies: a document whose model resolved fine for `@model` drew 11 errors the moment an expression used the C# tier. `RoslynReferenceProvider` still gated on `Assembly.Location` after F-004 fixed `AssemblyHelper`; the rule written alongside F-004 covered "type resolution **and C#-tier metadata**" and only the first half was implemented | `01922e5` — reads the loaded metadata image when there is no file to read |

- **pinned by:** `CSharpTierMetadataTests.AnAssemblyWithNoFileBehindItStillYieldsMetadata`,
  `.NoAssemblyIsDroppedFromTheReferenceSet` — added a cycle later than F-004's fix, by
  `01922e5`, with F-019's.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~CSharpTierMetadataTests`.
  Mutation: delete the `Location` guard's replacement and confirm a red.
- **notes:** **cycle 2's mutation testing found F-004's fix entirely unpinned** — deleting the
  `Location` guard passed 1,832 tests. On `netstandard2.0` the F-019 half is still broken by design —
  `TryGetRawMetadata` does not exist there; see F-080.

### F-005 — Observation staleness: load order decided what resolved, then a count gate could not tell

- **status:** FIXED
- **absorbs:** F-020
- **severity:** 3 (both; F-020 sticky — the type stayed unresolvable until an unrelated later load repaired it)
- **found:** cycle 1 (verifier and adversary), cycle 2 (demonstrated 3/3)
- **the predicate:** "has the set of loaded assemblies moved since we built the type maps?" — asked
  first not at all, then with a counter that cannot distinguish "nothing happened" from "one left and
  one arrived".

  | id | symptom | root cause | fixed by |
  | --- | --- | --- | --- |
  | F-005 | an assembly loaded after the first resolution was permanently invisible; whether a type resolved depended on whether an unrelated earlier compile had happened. This falsified, in the same words, four documents the docs sweep had just landed | observation never rebuilt the type maps | `7fd1999` — maps carry the stamp of the assembly set they were built from and rebuild when it moves; a count gate and reference-identity fast path keep it off the hot path (the naive version cost 27s → 49s on the suite; back to 26s) |
  | F-020 | an unload followed by a load restored the count, so observation concluded nothing had happened and the newly loaded assembly stayed invisible — the exact shape of a collectible context reload, which the language service performs on every model reload | `_observedCount` counted every loaded assembly including the excluded collectibles. **The optimisation added by F-005's fix created it** | `e818ac1` — an order-sensitive identity digest of the loaded set, which cannot cancel out; allocation-free, so the per-resolve cost that motivated the count gate is unchanged. Generation is also published before the stamp, so a reader between the two writes cannot conclude both that nothing loaded and that its maps are current |

- **pinned by:** `AssemblyRegistrationTests.AnAssemblyLoadedAfterTheFirstResolutionStillResolves`
  (reddens within 28 compiles against the pre-fix shape);
  `.AnAssemblyLoadedAfterACollectibleUnloadStillResolves` (unloads in its own frame, because a debug
  build roots every local until the method returns);
  `CSharpTierMetadataTests.NoAssemblyIsDroppedFromTheReferenceSet`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AssemblyRegistrationTests`;
  mutation: revert the digest to a count and confirm the test reddens with `Couldn't resolve type`.
- **notes:** the claim "load order does not decide what resolves" was still false after F-005's
  commit, by a different route. Class F: the optimisation in the fix was the next defect.

### F-006 — Gates whose predicate never reaches the text they claim to check

- **status:** FIXED
- **absorbs:** F-011, F-012, F-013, F-014
- **severity:** 6, except F-006 itself which is off-scale (documentation)
- **found:** cycle 1, verifier (F-011 predicted by the sweep plan, then confirmed)
- **the predicate:** each of these gates reported green over a *projection* of the text — a regex over
  a whole file, a substring, a link subset, an extraction that could not see arguments — rather than
  over the rows or the rendered output it claimed to be checking.

  | id | symptom | fixed by | regression check |
  | --- | --- | --- | --- |
  | F-006 | the `HED1005` row's entire description vanished from the rendered page — an unescaped `\|` inside a markdown table cell | `7fd1999` | render `docs/native-expressions.md` (or grep the `HED1005` row) and confirm the description survives the table cell |
  | F-011 | the generator-side `ClaimedIds` helper regexed `` `HED\d{4}` `` over the whole registry file, so a mere cross-reference mention anywhere in that document satisfied "claimed" — leaving the registry cross-reference hole open. The runtime-side copy was correctly section- and row-anchored | `7fd1999` | `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~PipelineDiagnosticsTests`; mutation: add a bare `` `HED7099` `` mention outside the registry table and confirm it does not satisfy the gate |
  | F-012 | `DiagnosticIdTests` matched ids by substring, so a longer id containing a shorter one satisfied the shorter one's row | `7fd1999` | `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DiagnosticIdTests` |
  | F-013 | `DocumentationLinkTests` skipped every same-directory link — 435 of 803 — and checked only the first number of a line range, which is precisely the defect that prompted the gate | `7fd1999` | `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DocumentationLinkTests`; mutation: introduce a broken same-directory link and a broken range end, and confirm both redden |
  | F-014 | `PublicApiDocMentionTests`' extraction missed any documented call written with arguments, so the whole window's new API was invisible to it | `7fd1999` | `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PublicApiDocMentionTests`; mutation: document a call with arguments against a member that does not exist and confirm a red |

- **pinned by:** each gate itself, now anchored: `PipelineDiagnosticsTests`' registry-parsing helper,
  `DiagnosticIdTests` (id-anchored), `DocumentationLinkTests`, `PublicApiDocMentionTests`. F-006 is
  pinned by nothing — the documentation gates check id *presence*, not row rendering.
- **notes:** F-012's record also corrects that gate's own doc comment, which claimed a block filter
  the code does not have — it gates all 85 constants. The class recurs: a gate green because it never
  reaches the rendered text.

### F-007 — Documentation asserting what the code no longer does

- **status:** FIXED
- **absorbs:** F-008
- **severity:** off-scale (documentation/sample)
- **found:** cycle 1
- **the predicate:** prose currency — a document written against an API shape that was never checked
  by running it, and a document rewritten *after* the thing it denies had landed. No gate reaches
  this class of claim.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-007 | the new startup-order sample called `RegisterFrom` on `TemplateOptions.Functions`, which defaults to `null`, so the sample threw | `7fd1999` |
  | F-008 | the document was rewritten one commit after a module initializer was added and re-asserted that none exists | `7fd1999` |

- **pinned by:** nothing. The documentation-currency rule landed as a convention, not a gate; the
  sample code in the documentation is not compiled by any gate.
- **regression check:** none automated. Read the sample; read the claim against the code.

### F-009 — A diagnostic documented by some of its causes

- **status:** FIXED
- **absorbs:** F-066
- **severity:** off-scale (documentation)
- **found:** cycle 1 (F-009), 6→7 interval (F-066)
- **the predicate:** the diagnostics gate checks that an id is *named* in its owning document, never
  that the description covers every producer. Two ids were documented by a subset of their causes.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-009 | `HED7104`'s published description named two of the three things that raise it | `7fd1999` |
  | F-066 | `HED4007`'s summary and registry row described expression, chain and block nesting only, while the diagnostic also carries the `@<<` import-depth case | `66b86df` |

- **regression check:** none automated — the diagnostics gate checks presence, not completeness.
  Compare the causes in the code that raises the id with the published description.
- **notes:** the *message* half of `HED4007` was unpinned at both producers — that is a row of F-026.
  Class L records this as one of two documentation classes with no gate.

### F-010 — The corpus parity gate did not cover what it claimed to cover

- **status:** FIXED
- **absorbs:** F-031, F-076
- **severity:** 6
- **found:** cycle 1 (verifier), cycle 2 (recorded as "not fixed"), closing round
- **the predicate:** "is every corpus template byte-compared between the tiers?" — answered by a
  hand-maintained list, then by a column an entry could set on itself.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-010 | a row in `src/TestCorpus/CorpusIntent.cs` stated an intent no harness enforced | `7fd1999` (row reworded) |
  | F-031 | `CorpusRenderParityTests` was ten hand-listed template names against thirty-two eligible, with nothing to notice the other twenty-two | `66b86df` — the set comes from the intent table, and a declared entry missing from the corpus is a red build |
  | F-076 | `ResolveOnly` removed an entry from byte-parity coverage on its own say-so, and seven entries declared `WithModel` sat outside the gate while rendering identically all along | `0db6222` — everything not declared `ResolveOnly` is rendered and compared, and everything declared `ResolveOnly` is rendered too, **to prove it cannot be** |

- **pinned by:** `CorpusRenderParityTests.EveryDeclaredPrecompilingEntryIsInTheCorpus`,
  `.AnEntryDeclaredResolveOnlyGenuinelyDoesNotRender`, `.ModelLessCorpusTemplateRendersIdentically`;
  the column semantics are documented on `CorpusRender` in `src/TestCorpus/CorpusIntent.cs`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CorpusRenderParityTests`;
  add a template to `src/TestCorpus` without an intent row and confirm the build reddens; declare a
  rendering template `ResolveOnly` and confirm a red.
- **notes:** cycle 8 then found the `ResolveOnly` assertion was satisfied by
  `Assert.ThrowsAny<Exception>` — including the harness itself falling over. That is a row of F-090.

### F-015 — A checkable completeness claim stated over an incomplete list

- **status:** FIXED
- **severity:** off-scale (documentation)
- **found:** cycle 1, verifier
- **symptom:** `editor-support.md` named 6 of the 11 excluded options while asserting "every compile
  option that affects analysis has a key here".
- **fixed by:** `7fd1999`.
- **pinned by:** `WorkspaceOptionParityTests` (the doc-facing legs added by the sweep).
- **regression check:** `dotnet test src/Heddle.LanguageServices.Tests --filter FullyQualifiedName~WorkspaceOptionParityTests`
  — **no `-f` flag: that project is `net10.0`-only.**

### F-016 — Test names claiming more than the test checks

- **status:** FIXED
- **severity:** 6
- **found:** cycle 1, verifier
- **symptom:** two test names asserted a property the body did not check.
- **fixed by:** `7fd1999` — renamed to what they check, with the behavioural pin named alongside.
- **pinned by:** n/a (this is a naming defect).
- **regression check:** none automated. **This class recurs constantly** — F-036 (with F-041, F-118,
  F-132), F-046 (with F-050, F-058, F-067), F-126 (with F-133, F-139, F-146), F-090 (with F-100,
  F-160). Read those four merged entries before writing a test name here.

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
- **regression check:** none. Recorded because the record's own "success criteria met" line is not
  evidence.

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
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ObservingAndRenderingLoadsNoReferencedAssembly`;
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

### F-023 — What a cached compile entry carries: the diagnostics, and not the caller's position

- **status:** FIXED
- **absorbs:** F-033
- **severity:** 3 (F-023), 1 (F-033 — a one-line document was told its error was on line four)
- **found:** cycle 2, cycle 3
- **the predicate:** what belongs in a cache entry that is replayed to a later caller. It was decided
  twice without asking which parts of a diagnostic are caller-specific: first the diagnostics were
  dropped, then the fix replayed the first caller's coordinates to everyone.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-023 | the first caller to compile a broken expression received the errors; every later one received none, fell through to a different path, and got a different list — the same template reported 1 error then 5, in one process. The preparse cache entry held the value only | `01922e5` — diagnostics are part of the cached entry and replay to every caller |
  | F-033 | the first caller's coordinates were stamped onto every later caller. Positions had been correct before F-023's fix introduced this | `2576bc2` — cached diagnostics carry messages only and are re-stamped by each caller |

- **pinned by:** `CSharpTierMetadataTests.TheSameFailingExpressionReportsTheSameDiagnosticsEveryTime`;
  `.ARepeatedFailingExpressionIsReportedAtEachCallersOwnPosition`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~TheSameFailingExpressionReportsTheSameDiagnostics`
  and `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ARepeatedFailingExpressionIsReportedAtEachCallersOwnPosition`.
- **notes:** **both pins are guards, not red-verified pins, and both tests say so.** Once the public
  keys were fixed (F-022) both paths produced identical text and positions, so removing the replay
  leaves the suite green; and replaying a fixed position also leaves the suite green, because two
  documents sharing an expression do not reliably share a cache entry here. Both are rows of the
  weak-pin table. F-023's fix also introduced F-034's failure-invalidation hole.

### F-024 — The constant fold's model of C# constant evaluation, wrong seven ways

- **status:** FIXED
- **absorbs:** F-035, F-040, F-047, F-048, F-049, F-054, F-059, F-078
- **severity:** 2 (emitted code the consumer's compiler rejects) and 4 (needless degrade), in both
  directions throughout
- **found:** cycles 2, 3, 4, 5, 6 and the 6→7 interval — **six cycles, one predicate**
- **the predicate:** *would the C# compiler reject this constant expression?* The fold exists so that
  an expression C# refuses is left unwritten and degrades to the dynamic tier, because the two tiers
  disagree about *when* an arithmetic fault is found: `@(2147483647+1)` renders `-2147483648` on the
  engine and is a build error precompiled; `@(1/0)` throws `DivideByZeroException` on the engine and
  is refused by C# at compile time. Every entry below is the same predicate answered wrongly for one
  more operand shape. **`Unknown` is not a refusal — it means the expression is written through**,
  which is why every gap in the model is a build break rather than a degrade.

  | id | cycle | symptom, both tiers | root cause | fixed by |
  | --- | --- | --- | --- | --- |
  | F-024 | 2 | `@(2147483647+1)` and `@(1/0)`: raw `CS0020`/`CS0220` against the `.heddle` file, manifest still claiming success. Adding the generator to a working project turned rendering templates into build errors | the emitter wrote arithmetic operators through verbatim; there was no fold at all | `570cf84` — a constant fold that leaves an expression C# would refuse unwritten. Timid by construction: anything it cannot evaluate with certainty is treated as not constant |
  | F-035 | 3 | five classes still broke the host build (`(2147483647+0)+(1+0)`, `+2147483647+1`, `uint` overflow, `decimal` overflow), and legal C# was refused, taking whole templates off the precompiled tier | the fold widened every integer to `long` and lost the operand type C# evaluates in | `2576bc2` — rewritten to track the type C# evaluates in, including the constant `int`→`uint` conversion and the `-2147483648` literal rule |
  | F-040 | 4 | `(4294967295u + 1)` folded as `(4294967295 + 0)`, was emitted, and failed the host's build with a raw `CS0220` — verbatim the symptom the fold exists to prevent. `(3000000000 / 2)` folded as division by zero and was refused. **2,081 build breaks and 1,636 needless degrades** by differential fuzzing against the real compiler | an `Int`-kind `Numeric` keeps its value in the signed field; the `uint` conversion read the unsigned one, so every `int` operand promoted to `uint` arrived as zero. Complementing a `uint` was computed in `ulong` and narrowed under check (so every `~x` on a `uint` looked like an overflow), and negating a floating constant returned it unchanged | `1b64d10` |
  | F-047 | 5 | `(-1u)/0` reached the host build as `CS0020` | unary minus on a `uint` yields `long` in C#, so it is decidable; the fold left it undecided, and undecided means "emit as written" | `25b2554` |
  | F-048 | 5 | 68 cases emitted and rejected by the host's compiler: refusing to decide whenever a `ulong` met any signed operand was too broad — C# converts a non-negative integer constant to `ulong` implicitly, which is how `ulong + 'a'` is legal | a refusal scoped to a whole operand *kind* rather than to the illegal pairs | `25b2554` |
  | F-049 | 5 | the literal-minimum rule needlessly degraded suffixed and parenthesised forms (severity 4) | it modelled C#'s reading of `-2147483648` as one `int` constant, but the emitter writes `2147483648U`, which C# types as `long` — **it described text that is never emitted** | `25b2554` — deleted rather than repaired |
  | F-059 | 6→7 | `(1&1)/0` and `(true?1:1)/0` reached the host's compiler as `CS0020`, `(1<<1)+2147483647` as `CS0220` | the fold returned `Unknown` for `&`, `\|`, `^`, `<<`, `>>` and never looked at `?:` | `8010e2a` — bitwise operators evaluate in the promoted type; shifts evaluate in the left operand's own type with the count masked; a conditional over a literal condition folds to the taken arm, typed as the common type of both arms. A fault in the discarded arm still refuses, because C# reports it there too |
  | F-078 | closing | `Numeric.From` carried rows for `sbyte`/`byte`/`short`/`ushort`, which no template literal can produce — the language has no suffix for them. `Numeric.Shift` masked its count to the operand width, which the C# `<<`/`>>` it is written in already does | modelling operand types and operations that cannot occur | `0db6222` — rows deleted |
  | F-054 | 6 | `Unify`'s summary still described the rule deleted a commit earlier, and `Wider`'s summary had been orphaned onto an unrelated helper by an insertion between a doc comment and its method (off-scale) | comment currency inside the fold | `cb4c426` |

- **pinned by:** `ConstantArithmeticDifferentialTests` —
  `.ConstantOverflowDegradesInsteadOfBreakingTheBuild`,
  `.ConstantDivisionByZeroDegradesInsteadOfBreakingTheBuild`, `.LegalArithmeticStillPrecompiles`,
  the mixed-width rows added by `1b64d10` (eleven), the unary-minus-on-`uint` rows,
  `.AnUnsignedLongMeetingACharWrapsToTheEnginesNumber`, the twenty-one rows added by
  `8010e2a` (each red before, since the harness compiles the emitted code) and the fourteen pinning
  the other direction, `.AShiftProducesTheSameNumberOnBothTiers` (F-090), and F-049's case moved into
  the *legal* set.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`
  — this one command covers every row above. Narrower checks that still work:
  `--filter FullyQualifiedName~AnUnsignedLongMeetingAChar` (F-048). Deleting the fold reddens 17 of 32 rows.
- **notes:** the fold's narrowness is pinned deliberately, because a fix that quietly moved arithmetic
  off the fast tier would be its own regression. **A test had already enshrined the wrong answer**
  before F-049 — asserting a degrade for an expression the emitted code compiles and the engine
  renders as `-2147483649`. A test can pin a defect. The *replacement* narrow guard for F-048 turned
  out to be dead code — F-053. And the class this fold keeps landing in is E: "cannot say" is the
  exempting answer, reached again from a different direction in F-148. The suite's own can't-fail
  defects are F-036 and F-041, rows of F-036.

### F-025 — The import graph: cycles, chain depth, and what makes two imports the same document

- **status:** FIXED
- **absorbs:** F-037, F-044, F-063
- **severity:** 3 throughout (process death at exit 134; 863,109 diagnostics at build time; a template
  read 1024 times)
- **found:** cycles 2, 3, 4 and the 6→7 interval
- **the predicate:** *is this import the same document as one already being parsed?* — and, once
  that had an answer, *how deep may a chain of distinct documents go?* The identity question was got
  wrong three times: from the raw spelling, then from `Path.GetFullPath` while the generator's reader
  used a different normaliser.

  | id | symptom, measured | root cause | fixed by |
  | --- | --- | --- | --- |
  | F-025 | an `@<<` import parsed the imported document in place with no visited set, so a document that imported its way back to one already being parsed recursed until the stack ran out — exit 134, ~27,500 frames, on a template typo. A `StackOverflowException` cannot be caught, so the process died. Self-import did the same | no visited set at all | `d2beb20` — imports carry the chain being parsed; one that reaches a document already on it reports `HED4006` and skips the repeated import |
  | F-037 | a chain of 4000 distinct files — no cycle anywhere — exhausted the stack. And `./a.heddle` and `d/../a.heddle` read as different documents, so a cycle walked past the guard and eight spellings produced 863,109 diagnostics | depth measured per document, and cycle identity taken from the raw spelling | `2576bc2` |
  | F-044 | the existing normalisation test's documents each imported their own spelling, so the raw key repeated and the cycle fired **without** the normalisation — severity 6 | a fixture that cannot see the rule it names | `1b64d10` — a cycle whose spellings never repeat; reverting the normalisation reddens |
  | F-063 | six spellings of a self-importing file were read 1024 times — enough to exhaust the import budget, and before that budget existed, to hang. At build time one template had as many identities as it had spellings | the cycle guard keyed on `Path.GetFullPath` while the generator's reader resolves through `TemplateKey.TryNormalize` (appending `.heddle`, stripping a leading `~/`, `/` or `./`, unifying separators) | `6b2b876` — `ParserSettings` gains an `ImportIdentifier` seam alongside `ImportReader`, and the generator sets both from the same normaliser so the two cannot drift |

- **pinned by:** `ImportCycleTests.ATwoDocumentImportCycleIsAnErrorNotAStackOverflow`,
  `.ADocumentImportingItselfIsAnErrorNotAStackOverflow`, `.TheCycleErrorNamesTheDocumentsInvolved`,
  `.TheSameImportOnTwoSeparateBranchesIsNotACycle` (the near neighbour that keeps the guard from being
  tightened into breaking diamond imports), `.AnUnboundedImportChainIsReportedInsteadOfKillingTheProcess`,
  `.ACycleIsCaughtHoweverTheImportPathIsSpelled`, `.ACycleWhoseSpellingsNeverRepeatIsStillCaught`,
  `.ASelfImportUnderManyKeySpellingsIsOneDocumentToTheCycleGuard`,
  `.TheCycleGuardFollowsTheReadersOwnNotionOfIdentity` (both red with the seam removed);
  `PipelineDiagnosticsTests` (the end-to-end generator half of F-063).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ImportCycleTests`
  covers every row. Narrower:
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ACycleWhoseSpellingsNeverRepeatIsStillCaught` (F-044),
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~CycleGuard` (F-063).
- **notes:** the identity question came back a fourth time in cycle 22, from the other side — the
  generator's key against the engine's canonical disk path (F-178, class A). Read F-178 before
  touching `ImportIdentifier` again. The *fan-out* dimension is F-057; per-parse *state* is F-043.

### F-026 — The parse depth bound: absent, misplaced, too high, measured in the wrong configuration

- **status:** FIXED (bound now a fixed count of 250)
- **absorbs:** F-032, F-042, F-045, F-051, F-052, F-075
- **severity:** 3 (process death, exit 134), plus 6 for the pin defects
- **found:** cycles 2, 3, 4, 6 and the closing round
- **the predicate:** *how deep may a recursive walk go before it must fail catchably?* Heddle's
  expression and chain builders recurse once per operator or level; flat operator runs, parenthesis
  nesting, indexer chains, prefix runs, nested conditionals and `??` chains all reach it, and a
  `StackOverflowException` cannot be caught. Getting a bound right needed four separate corrections
  and then three separate pin defects.

  | id | cycle | what was wrong | fixed by |
  | --- | --- | --- | --- |
  | F-026 | 2 | no bound at all: a single template file took the process down. Fixed across four commits, and the sequence is the finding — `ecaca87` builders fail catchably, parse boundary reports `HED4007` (half the defect: the parser's own recursion still terminated the process); `50832db` **reversal of instrument** — the first attempt probed remaining stack with `EnsureSufficientExecutionStack`, whose answer depends on thread stack size and build configuration, so the same template passed on one host and died on another and no fixed-depth test was portable; replaced by a fixed depth count of 1000, enforced in two places (an ANTLR parse listener for the parser's own recursion; an iterative tree-depth check for left-associative runs, which ANTLR parses with a loop and so never recurses on) | `ecaca87`, `50832db`, then F-042/F-051 below |
  | F-032 | 3 | a deep left-associative run plus one stray `@(` still killed the process — exit 134, 52,309 `RuleContext.GetText()` frames. `EnsureTreeWithinLimit` sat *after* the early `return tree.GetText()` that fires when a parse reported errors, and `GetText()` recurses over the whole tree. **It mattered most exactly where the bound was sold hardest** — an editor's document has a syntax error most of the time, and the generator is the compiler | `2576bc2` |
  | F-042 | 4 | at a bound of 1000, a 1 MB thread — the Windows default, and what the thread pool hands out — overflows on the parser-recursive shapes at around 350, so a generator hosted in MSBuild died on a template a few hundred characters long. A fixed count is only host-independent if it sits below *every* host's limit | `1b64d10` — 300, measured against 300/500/800 last-safe depths at 1/2/4 MB |
  | F-051 | 6 | 300 was a **Debug** measurement. A Release build — what a source generator actually runs as — dies at 284 on a 1 MB thread where Debug survives to 503. The guard fired at 293, so for the parser-recursive shapes it could never fire first: every input deep enough to trip the bound killed the process before reaching it | `0f9ba46` — limit 250, below the Release figure; both numbers recorded in the comment |
  | F-052 | 6 | the test written to pin the depth property **aborted the net8.0 Release run after 1,143 of 1,865 tests**, breaking CI in a configuration nobody had run | `0f9ba46` — deleted; the limit's value stays asserted, which is the part a test can hold. Verifying the crash property needs a child process comparing exit codes, which no suite does. **Do not re-add a test that reaches the crash in-process** |
  | F-075 | closing | `HED4007` has two producers with unrelated faults and unrelated remedies — expression nesting and `@<<` import nesting — and collapsing both messages to one vague string was green (severity 6) | `0db6222` — both pinned; the parse-depth path has a test again (a flat left-associative run reaches the reporting path without being able to reach the crash, because ANTLR loops left recursion) |
  | F-045 | 4 | the language reference contradicted itself: one section still promised imports could nest "to any depth"; the 64-level import bound was stated nowhere; and the block-nesting figure was the internal rule count (1000), not the ~330 nested blocks actually allowed (off-scale) | `1b64d10`; pinned by nothing, no automated check |

- **pinned by:** `DeepNestingTests.TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`,
  `.AFlatRunPastTheLimitIsReported`;
  `DeepNestingGeneratorTests.ADeeplyNestedTemplateFailsTheBuildInsteadOfTheCompiler` (a `[Theory]` over
  five shapes: `prefix`, `conditional`, `coalesce`, `flat`, `blocks`),
  `.AnOrdinarilyDeepTemplateStillPrecompiles`; the import-depth message rows in `ImportCycleTests`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DeepNestingTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DeepNestingGeneratorTests`.
  Narrower: `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`
  (F-042/F-051). Mutation: change the limit constant and confirm the value assertion reddens.
  **Re-measure in Release, on a 1 MB stack, if the bound is ever revisited.**
- **two pins this entry used to name no longer exist, and the checks that ran them ran nothing.**
  Verified against `dotnet test --list-tests` for both projects:
  - `DeepNestingGeneratorTests.ADeepTemplateThatAlsoHasASyntaxErrorIsStillBounded` — **F-032's only
    pin, gone.** The suite now holds two members and neither builds a deep run with a stray `@(`. The
    fix itself is in place and carries its own explanation: `src/Heddle/Language/DocumentParser.cs`,
    the `ParseDepthGuard.EnsureTreeWithinLimit(tree)` call placed *before* the error return, with the
    comment "this cannot sit after the error return … an editor's document has a syntax error most of
    the time". **Read that placement; nothing executes it.** F-032 is now a row of the weak-pin table.
  - `DeepNestingTests.APrefixRunPastTheLimitIsReportedOnASmallStack` — removed on purpose, and that
    removal *is* F-052. `DeepNestingTests`' own comment says why: a test that parses a
    parser-recursive shape past the limit does not go red, it takes the test host down. Do not
    re-add it; the entry naming it as a live pin was stale.
- **notes:** the generator gets the bound by source-linking the parser, asserted at the generator's own
  entry point — it matters more there, because an unbounded build-time parse takes down the compiler
  or the IDE rather than failing a build. The bound's *value* was also unpinned over the range
  125–1499 — that is F-046, filed under the can't-fail-test class it belongs to. Residual: F-027.

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

### F-034 — The preparse cache's staleness rule: failures, successes, ordering and the epoch

- **status:** FIXED (F-074 and F-093 are SUPERSEDED rows — the epoch they added no longer exists)
- **absorbs:** F-030, F-074, F-088, F-093
- **severity:** 3 (F-034, F-074, F-088), 6 (F-030, F-093)
- **found:** cycles 2, 3, 8, and the closing round
- **the predicate:** *may this cache entry still be served, given that the set of observed assemblies
  may have moved since it was computed?* Answered four times. **Read this whole entry before touching
  the cache — the epoch a middle round added was removed again, and the current shape holds one
  assembly generation at a time.**

  | id | status | symptom | fixed by |
  | --- | --- | --- | --- |
  | F-030 | FIXED | no test exercised the path that serves a cached compile failure at all | `2576bc2`, later `f872b79`/`31e618d` (the generation rules) |
  | F-034 | FIXED | a failure cached before a host registered an assembly survived the registration for the life of the process — a fault that used to heal on the next compile became load-order-decided forever. Nothing invalidated the cache when the assembly set changed; introduced by F-023's fix, one file over from the observation gate that exists to prevent exactly this property | `2576bc2` — a cached failure is retired when the observed generation moves |
  | F-088 | FIXED | the *success* half, five cycles later. The reasoning was that nothing a later registration adds can take a type away. It can: a new assembly can make a name ambiguous (`CS0104`) or introduce a better overload candidate | `726031b` — successes are generation-checked, and the cache drops a spent entry rather than merely refusing to read it |
  | F-074 | SUPERSEDED | the cache could serve exactly the entry the drop existed to remove: the drop ran *before* the assemblies were unregistered, and a compile already in flight stores its result afterwards, into the map that was just emptied | `0db6222` — entries carry the epoch their compile began under and are refused if it has moved; the drop happens after the removal. Its two tests (`PreparseCacheEpochTests.AnEntryFromACompileThatStartedBeforeTheDropIsNotServedAfterIt`, `.AnEntryFromTheCurrentEpochIsServed`) were **both deleted** by `f872b79` |
  | F-093 | SUPERSEDED | the epoch F-074 added was subsumed by the same round's generation-strict staleness rule; reviewers could also reverse two orderings without reddening anything | `f872b79` — removed. The cache holds one assembly generation at a time, under one monitor: a result computed against a superseded set is refused admission, one from a newer set retires the map on the way in, and a reader asking at a generation the map is not holding gets nothing. `GetApplicationReferences` hands out the reference set and its generation from one lock-held read; the unregistration is a single expression. **One correction on top:** a reader whose generation is *older* than the map's must not retire it, or a straggler arriving after an unregistration throws away every entry the current set just built |

- **pinned by:** `CSharpTierMetadataTests.AFailureCachedBeforeRegistrationDoesNotSurviveIt`;
  `PreparseCacheGenerationTests.RetargetingDropsEntriesRatherThanLeavingThemUnread`,
  `.AResultFromANewerSetEvictsTheOlderEntriesRatherThanRelabellingThem`,
  `.AnEntryAtTheGenerationTheMapHoldsIsServed`, `.AnEntryIsNotServedOnceTheAssemblySetHasMovedOn`,
  `.AResultComputedAgainstASupersededSetIsRefusedAdmission`, `.AResultFromANewerSetRetiresTheOlderEntries`,
  `.AResultComputedBeforeAnUnregistrationIsRefusedAfterIt`,
  `.AReaderFromASupersededSetDoesNotDragTheMapBackToIt`,
  `.TheSuiteLeavesTheCacheUsableAtTheLiveGeneration`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PreparseCacheGenerationTests`
  and `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AFailureCachedBeforeRegistrationDoesNotSurviveIt`.
- **notes:** cycle 8 recorded the ordering claims as **unpinnable**: reversing either ordering left
  every suite green, and a test that races to observe the difference passes by luck when it is wrong.
  The fix's merit is that the reversible orderings no longer exist to be reversed. The note lives in
  `PreparseCacheGenerationTests` and `AssemblyRegistrationTests` in plain words. Class J; also listed
  in the known-open tail as an unpinnable ordering.

### F-036 — A degrade-only assertion cannot distinguish a rule from a blanket refusal

- **status:** FIXED
- **absorbs:** F-041, F-118, F-132
- **severity:** 6
- **found:** cycles 3, 4, 12 and 14 — **the rule the series keeps re-learning**
- **the predicate:** a test that only asserts *this template degraded* passes against a build that
  refuses everything. **Every degrade assertion needs a paired case that must still precompile.**

  | id | cycle | symptom, with the mutation that proves it | fixed by |
  | --- | --- | --- | --- |
  | F-036 | 3 | every constant-fold case used `@model(){{dynamic}}`, under which constant-only expressions degrade anyway — **deleting the entire fold left all 876 generator tests green** | `2576bc2` — typed models make a degrade attributable; deleting the fold now reddens 17 of 32 |
  | F-041 | 4 | no fold case paired an unsuffixed `uint`-range literal with a small `int`, and the one `uint` case in the suite passed *because of* the bug (F-040) | `1b64d10` — eleven mixed-width cases added |
  | F-118 | 12 | `AssertEngineRefuses` grepped for the id, and `HED5014` has two messages ("not assignable" and "must have a static type"), so the test could not tell which rule it had reproduced. And both tests were `ExpectDegrade`-only, **which cannot fail against the parent commit, where everything degraded** | `6d013eb` — rebuilt to name the exact type pair and to carry a precompiling half; checked by re-inserting the parent's blanket refusal and watching them fail on the positive half |
  | F-132 | 14 | the `null` row of the `:: dynamic` body table asserted a degrade **and an empty error list**, which any refusal satisfies. Demonstrated: refusing the null literal outright left all nine tests green, and **refusing every literal call site outright failed only two rows and passed the other 672 integration tests.** Nothing anywhere asserted that a `:: dynamic` definition called with a literal ever precompiles | `e14e862` — four rows that render the engine's bytes, one of them reading `Length` off a `string` literal so it fails both if the body is left dynamic and if it is typed as anything else |

- **pinned by:** the retyped `ConstantArithmeticDifferentialTests` (F-036, F-041);
  `DynamicDefinitionBodyTests.ALiteralCallSiteValuePrecompilesWhenTheBodyFitsIt` (F-132).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DynamicDefinitionBodyTests`.
  The mutations that must redden: delete the fold's entry point (~17 fold rows); re-insert a blanket
  refusal in the slot-value path (the positive rows); refuse the null literal; refuse every literal
  call site.
- **notes:** class G. Siblings by a different sub-pattern: F-046 (tests that read the production
  constant), F-126 (rows answered by another arm), F-090 (never pinned at all).

### F-038 — A six-character template threw out of the compile

- **status:** FIXED
- **severity:** 3 (an uncatchable-shaped failure from a malformed template)
- **found:** cycle 3, by a test written for something else
- **symptom:** `@(1)}}` underflowed the lexer's mode stack and the `InvalidOperationException`
  escaped the compile. Pre-existing, unrelated to depth.
- **fixed by:** `2576bc2`.
- **pinned by:** **nothing.** The entry used to name
  `DeepNestingTests.UnbalancedClosersAreReportedRatherThanThrown`; that test does not exist —
  verified against `dotnet test src/Heddle.Tests -f net8.0 --list-tests`, and the check that ran it
  matched zero tests and exited 0. `DeepNestingTests` now holds two members, neither of them this.
- **regression check:** none automated. Read the guard: `src/Heddle/Language/DocumentParser.cs`, the
  `catch (InvalidOperationException)` around `RunParse`, whose comment names the repro verbatim —
  "the lexer's mode stack underflows on unbalanced closers — `@(1)}}` is enough" — and which adds a
  `HeddleDiagnosticIds.SyntaxError` error reading "Unbalanced or unexpected token; the template could
  not be tokenized." Confirm the catch and the error are still there. **A one-line test would close
  this**: parse `@(1)}}` and assert one error carrying that id rather than an escaping exception.
- **notes:** class K — the fourth unbounded-input dimension, alongside depth, fan-out and cycles.
  Now a row of the weak-pin table: the fix is in the code and nothing executes it.

### F-039 — A reference built twice on every cache miss

- **status:** FIXED
- **severity:** off-scale (waste, not a divergence)
- **found:** cycle 3, by review — **not by any test**
- **symptom:** `RoslynReferenceProvider` built each reference twice on a cache miss. Pre-existing.
- **fixed by:** `2576bc2`, as part of anchoring the assembly.
- **pinned by:** not recorded.
- **regression check:** none identified. Row of the weak-pin table.

### F-043 — Per-parse import state that outlived or was shared across a parse

- **status:** FIXED
- **absorbs:** F-065, F-070
- **severity:** 3 throughout
- **found:** cycle 4, the 6→7 interval, cycle 7 (verifier)
- **the predicate:** *where does state that belongs to one parse live?* Answered three times: on a
  public settings object a host builds once and reuses; then partly moved; then moved to a
  thread-static, which a public re-entrant callback defeats.

  | id | symptom, measured | root cause | fixed by |
  | --- | --- | --- | --- |
  | F-043 | a host reusing one settings object stopped reporting import cycles from the thirty-third parse, while still skipping them | the cycle-report budget lived on `ParserSettings` and was never reset | `1b64d10` — reset per top-level parse |
  | F-065 | sixty-four concurrent parses of two independent three-file graphs produced **twenty-two escaping exceptions and a run of phantom `HED4006`s**: each document saw the other's imports as its own, and the cycle message — built by joining that stack while another thread appended to it — threw | the import *stack* was left on `ParserSettings` when F-043 moved the *budget* off it three commits earlier — same class, different field | `4ad3283` — the stack and budgets move to `ImportParseState`, held per thread |
  | F-070 | `ImportReader` is a public host callback, and a host that resolves an import by parsing re-enters the parser on the same thread with an import stack already on it. The inner parse read and mutated the outer parse's stack and budgets, reporting a cycle that was not there. Per-settings state had isolated those; the thread-static did not | F-065's fix | `d034dda` — the callback window is marked, and a parse beginning inside it gets fresh state and hands the outer parse's back untouched |

- **pinned by:** `ImportCycleTests.ReusingOneSettingsObjectKeepsReportingCycles`,
  `.OneSettingsObjectServesConcurrentParsesWithoutCrossTalk`,
  `.AParseBegunInsideAnImportReaderIsIndependentOfTheOuterParse`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ReusingOneSettingsObjectKeepsReportingCycles`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~OneSettingsObjectServesConcurrentParses`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AParseBegunInsideAnImportReader`.
- **notes:** **F-070's pin does not redden, and says so**: an import is read before its name is pushed,
  so at the nesting this fixture reaches the outer stack is empty and the inner parse resets anyway.
  The review reproduced the false cycle from a deeper outer parse; the fixer could not get this shape
  there. **Weak pin; candidate for the next cycle** — a deeper outer parse is the missing fixture. It
  is a row of the weak-pin table. Class J.

### F-046 — A test that reads the production constant it is checking

- **status:** FIXED
- **absorbs:** F-050, F-058, F-067
- **severity:** 6
- **found:** cycle 5 (two reviewers converging), cycle 5, cycle 7, 6→7 interval
- **the predicate:** **comparing a test's expectation against the production constant is the
  recurring anti-pattern of this series.** A copy only notices a change if something compares it to
  something else. Four bounds, four cycles, one mistake.

  | id | bound | symptom | fixed by | regression check |
  | --- | --- | --- | --- | --- |
  | F-046 | parse depth | the suite tolerated any limit value from 125 to 1499, so the previous cycle's headline change (1000 → 300) could have been skipped entirely and 2,766 tests would still have passed. The test constant mirroring the production value was left at 1000, under a comment saying it exists as a copy precisely so a change cannot pass unnoticed | `25b2554` — the value is asserted directly, and the property it was chosen for is stated as a test | change the limit constant by one and confirm a red |
  | F-050 | cycle-report budget | the assertion was set at 64, which is exactly what that fixture produces with the budget deleted, so it could not fail | `25b2554` — compares against the budget | `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~CycleReportingIsBounded` |
  | F-067 | cycle-report budget again | **F-050's fix was itself unfalsifiable**: comparing the diagnostic count against the budget *constant* agrees with any budget — including one raised past the 64 the fixture can produce, at which point it observes nothing | `66b86df` — the bound is written out and the constant's value asserted; raising the budget to 512 now reddens | raise the budget constant and confirm a red |
  | F-058 | import fan-out | both tests read `MaxImportExpansions` itself, so they agreed with any value it was raised to; raising it to 65536 left them green. **The same commit that added them had diagnosed and fixed exactly this defect for the cycle-report budget two hunks away** | `e46d0b6` — the value is asserted | change `MaxImportExpansions` and confirm a red |

- **pinned by:** `DeepNestingTests.TheGuardsLimitIsTheValueMeasuredAgainstTheSmallestSupportedStack`
  (the `.APrefixRunPastTheLimitIsReportedOnASmallStack` this entry used to name alongside it no longer
  exists — see F-026, and do not re-add it: removing it *was* F-052);
  `ImportCycleTests.CycleReportingIsBoundedHoweverManySpellingsReachIt`,
  `.TheFanOutBoundIsTheValueThatWasChosen`.
- **regression check:** the four mutations above, plus
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~DeepNestingTests` and
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ImportCycleTests`.
- **notes:** class G, sub-pattern one. The bounds themselves are F-026 (depth) and F-057 (fan-out).

### F-053 — A guard, and a branch, that could never fire

- **status:** FIXED (both deleted)
- **absorbs:** F-089
- **severity:** 6
- **found:** cycle 6 (adversary), cycle 8
- **the predicate:** *does this guard's condition ever decide anything?* Both were answered by
  fingerprinting the code with the condition forced, not by reading it.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-053 | the `ulong`/negative-operand guard added in cycle 5 (as F-048's replacement) is dead code: replacing its condition with `if (false)` produced a byte-identical fingerprint of `CompilerWouldReject` over **2,587,410** folded expressions. The checked cast in `Convert` already overflows for exactly that set | `cb4c426` — guard and helper deleted, the explanation moved to the cast where the behaviour actually lives |
  | F-089 | the unreachable half of `WriteDynamicPath` wrote exactly the `?.` chain whose short-circuit the typed writer had just been fixed for (F-055's F-072 row). Both sides of its gate are constants this assembly reads | `726031b` — deleted |

- **regression check:** re-run the fingerprint sweep if a similar guard is proposed: replace its
  condition with `if (false)` and compare fold verdicts over the fuzz corpus. Nothing else is needed —
  both branches are gone.
- **notes:** it was the removal of the *old broad* condition (F-024's F-048 row) that fixed the 68
  cases; the replacement never fired. **Three lines of justification are not evidence.** Contrast
  F-165, where an unreachable arm was deliberately kept and labelled, and its own contrast F-175 where
  one was deleted — the difference is stated there. Related: F-145, a gate added on suspicion and
  later measured to cost a working template.

### F-055 — The emitted member-hop form: receiver duplication, `?.` propagation, and ref structs

- **status:** FIXED
- **absorbs:** F-056, F-069, F-072, F-073, F-082, F-083
- **severity:** 1 (F-056, F-072, F-082) and 2 (F-069, F-073, F-083); F-055 itself off-scale
- **found:** cycle 6 (adversary), cycle 7, the closing round, cycle 8
- **the predicate:** *what C# does the emitter write for one hop of a member path, and does it read
  the receiver the same number of times, with the same null semantics, as the engine's expression
  tree?* **Four commits in this chain each introduced the next defect** — the register's clearest
  instance of class F, ending in a silent-wrong-output regression that the intermediate commit had
  made loud.

  | id | cycle | symptom, both tiers | root cause | fixed by |
  | --- | --- | --- | --- | --- |
  | F-055 | 6 | a `ParseDepthGuard` comment claimed member paths are free (off-scale) | they are free to *parse* and not free to *compile* — the comment hid F-056 | `cb4c426` |
  | F-056 | 6 | LINQ expression trees are trees, not DAGs, so hop k held two copies of everything below it: an n-hop path compiled to 3·2^(n+1)−5 nodes and called 2^(n+1)−1 getters. **Fourteen hops took 859 ms to compile, sixteen took 7.6 s, seventeen produced IL the runtime refused with `InvalidProgramException`.** The emitter had the same doubling in its non-nullable-value hop, bounded at one duplication: it charged 2n+1 reads where the engine charges n+1. Observable through any getter that logs, materialises lazily or queries | a null-safe hop spelled its receiver twice, in every tier | `13c7172` — each receiver bound to a local, in the member tier, the dynamic tier and the native-expression tier's array and indexer accesses; the emitter switched to `x?.M ?? default(T)` |
  | F-069 | 7 | a model with a `Span` or `ReadOnlySpan` property emitted code the consumer's compiler rejects outright: `CS8978` against the `.heddle` file, no Heddle diagnostic, no degrade | F-056's new spelling widens the member to `T?` via `?.`, and a ref struct has no nullable form | `e46d0b6` — where the widening is impossible the receiver is spelled twice again, which is what that form always did for such a type. The duplication cannot compound: a ref-struct hop can only ever be the last one in a chain |
  | F-072 | closing | `Inner.Maybe.HasValue` over a null `Inner` produced **nothing at all** on the generated tier while the engine read `HasValue` off `default(int?)` and produced `False`. The `.Value` form diverged the other way: the engine threw `InvalidOperationException` and the generated tier rendered empty. Four of four null-hop cases red, no diagnostic on either tier | the emitter wrote a member path as one null-conditional chain, so C#'s rule that `?.` governs everything to its right met an engine that defaults the failed hop and keeps walking | `0db6222` — end the chain with parentheses before a plain `.`; `(a?.B).C` reads `C` off the default, which is the hop the engine performs |
  | F-073 | closing | `Inner.Buf.Length` emitted two `CS8978`s into the consumer's build. Found by asking what else the chain-propagation rule touched — **not by any existing test** | the ref-struct form spells its receiver twice, and spelling a propagating chain into the read half re-applies `?.` to a type with no nullable form | `0db6222` — the same parenthesisation as F-072 |
  | F-082 | 8 | `Inner.Buf.Length` over a getter that answers differently on its second call renders a value on the engine and throws `NullReferenceException` on the generated tier. **The commit before had turned the same template into a loud `CS8978`; the fix converted a build failure into silent wrong output** | the non-nullable-value hop form has always spelled its receiver twice; F-072's parenthesisation made the form compile for a deep prefix, and the duplication became reachable | `726031b` — the receiver is bound with a type pattern, naming it once, from a counter owned by the emitter and shared with every expression writer it makes (a pattern variable belongs to the block its statement is in, so two paths in one block would collide) |
  | F-083 | 8 | a path ending *on* a ref struct emitted `CS0030`: every consumer of a path's value boxes it, and a ref struct cannot be boxed. Pre-existing | — | `726031b` — the shape degrades, and the engine's own refusal (`HED0005`, at compile time) is what the reader sees. An id and a position a host can report beat a raw `CS0030` against a `.heddle` file |

- **pinned by:** `NullSafeHopEvaluationTests.TheMemberTierReadsEachHopExactlyOnce`,
  `.TheNativeExpressionTierReadsEachHopExactlyOnce`, `.TheDynamicTierReadsEachHopExactlyOnce`,
  `.AnIndexedHopEvaluatesItsReceiverOnce`, `.ADeepPathStillCompiles`,
  `.TheAccessorTreeGrowsLinearlyWithPathLength`;
  `MemberHopEvaluationCountTests.BothTiersReadTheModelTheSameNumberOfTimes`;
  `ConstantArithmeticDifferentialTests.ARefStructPropertyEmitsCodeThatCompiles` (reverting the gate
  reproduces `CS8978`);
  `NullSafeHopChainTests.AHopThroughNullReadsTheMemberOfTheDefaultOnBothTiers`,
  `.APresentValueRendersIdenticallyOnBothTiers`, `.ARefStructHopBehindAReferenceHopCompiles`,
  `.ARefStructHopReadsItsReceiverOnce`, `.TwoRefStructHopsInOneExpressionGetDistinctLocals`
  (`31e618d`; a constant name leaves 567 integration tests green and breaks a real template with
  `CS0128`), `.APathEndingOnARefStructDegradesInsteadOfBreakingTheBuild`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~NullSafeHopEvaluationTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~MemberHopEvaluationCountTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~NullSafeHopChainTests`.
  Narrower: `--filter FullyQualifiedName~ARefStructPropertyEmitsCodeThatCompiles` (F-069),
  `--filter FullyQualifiedName~ARefStructHopBehindAReferenceHopCompiles` (F-073),
  `--filter FullyQualifiedName~APathEndingOnARefStruct` (F-083), all in `Heddle.Generator.IntegrationTests -f net8.0`.
- **notes:** the commit that introduced F-069 asserted the new spelling "says the same thing".
  **A first draft of the record claimed the engine can render F-083's template. It cannot** — measured
  afterwards: the engine refuses with `HED0005` at compile time. Reading *through* a ref struct to a
  member of its own is unaffected; what leaves that path is an `int`. Property-*type* nameability at a
  hop is F-098's F-101 row.

### F-057 — Import fan-out: unbounded, then charged per path instead of per document

- **status:** FIXED
- **absorbs:** F-071
- **severity:** 3 (a build that never finishes), then 4/3 (a legitimate design system failing the build)
- **found:** the 6→7 interval, cycle 7
- **the predicate:** *how much total import expansion may one top-level parse do?* — a question the
  cycle guard cannot answer, because it only knows what is currently on the import stack.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-057 | a graph where each file imports the next one twice is acyclic and twenty levels deep — well inside the depth ceiling of sixty-four — and expanded to **2,097,151 parses with no diagnostic at all**; at the ceiling, upwards of 10^19. A document already parsed and popped is re-read | `8a30aed` — total expansions per top-level parse bounded (1024 initially), overflow described once as `HED4008`. Repeats cannot simply be collapsed: each expansion contributes the imported document's output chains at its own position |
  | F-071 | the bound counts expansions rather than distinct documents, so fifty components each pulling twenty shared token files is over a thousand — **failing a bound meant to stop an exponential** | `d034dda` — raised to 16384, which still caps the 2^21 blow-up the bound exists for while leaving real graphs an order of magnitude of headroom |

- **pinned by:** `ImportCycleTests.AnAcyclicImportFanOutIsBoundedAndReported`,
  `.TheFanOutOverflowIsDescribedOnce`, `.TheFanOutBudgetIsRestoredForEachTopLevelParse`,
  `.TheFanOutBoundIsTheValueThatWasChosen` (value assertion).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~FanOut`.
- **notes:** the bound was also unpinned — F-058, a row of F-046. **A bound is a policy number; measure
  it against a realistic graph, not only against the attack it was written for.** Class K.

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
- **notes:** the underlying `bool`/`bool?` bitwise pair still has **no dedicated diagnostic** — F-166,
  known-open, and this fix is its partial closure.

### F-061 — An unreadable `@<<` import threw out of the parse — and the guard was scoped to one reader

- **status:** FIXED
- **absorbs:** F-062
- **severity:** 3 (both; F-061 published no diagnostics at all for the document)
- **found:** the 6→7 interval, cycle 7
- **the predicate:** *what may a host's `ImportReader` do?* First nothing guarded it; then the guard
  was written for the exception set the **disk** reader raises, over a public seam a host supplies.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-061 | `ParserSettings.ReadImport` opened the file with no guard from inside the tree walk — the region `DocumentParser`'s own `try` excludes. A half-typed import path threw `FileNotFoundException` out of `didOpen`, so the document got **no diagnostics: not for the missing import, and not for anything else in it** | `1f3ef70` — guarded, reports `HED4009` positioned over the `@<<` directive; the import is skipped and the rest of the document still parses |
  | F-062 | the two idiomatic ways for a host to say "no such import" — returning `null`, letting a lookup throw — both escaped and took the parse down. **That is the failure the guard was added to stop** | `e46d0b6` — the reader is treated as untrusted input |

- **pinned by:** `ImportCycleTests.AnImportThatCannotBeReadIsReportedAndTheRestOfTheDocumentStillParses`,
  `.AnImportReaderThatReturnsNullIsReportedRatherThanThrown`,
  `.AnImportReaderThatThrowsIsReportedRatherThanThrown`;
  `LanguageServiceDiagnosticsTests.AMissingImportIsReportedInsteadOfEndingTheAnalysis`.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AnImportThatCannotBeRead`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AnImportReaderThat`, and
  `dotnet test src/Heddle.LanguageServices.Tests --filter FullyQualifiedName~AMissingImportIsReported`
  — **no `-f` flag on the last: that project is `net10.0`-only.**
- **notes:** class D — **a guard scoped to the implementation in front of you rather than to the
  seam's contract.** Same class as F-091's F-096/F-097 rows.

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
- **regression check:** `dotnet test src/Heddle.LanguageServices.Tests --filter FullyQualifiedName~ReloadCollectsPreviousModelContext`
  — **no `-f` flag: that project is `net10.0`-only.**
- **notes:** two comments claiming metadata references never pin a collectible context were corrected
  rather than the anchor removed: `RoslynReferenceProvider` **does** anchor an assembly to any
  reference built over its in-memory metadata, on purpose — the anchor defers the unload until the
  last holder is finished rather than preventing it. Class J.

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

### F-077 — A differential harness whose answer depended on test scheduling

- **status:** FIXED
- **absorbs:** F-094
- **severity:** 6
- **found:** closing round (visible as two unrelated cases going red under a mutation that only
  changed scheduling), cycle 8
- **the predicate:** the two tiers do not bind a model type from the same world — the generator
  resolves over the compilation's *references*, the engine over the assemblies the process has
  actually *loaded*. The harness papered over that with an `Assembly.LoadFrom`, which first made the
  suite order-dependent and then hid the engine property it was compensating for.

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-077 | handing an assembly to Roslyn equips only the precompiled side. Corpus templates naming engine test models compiled or failed depending on whether an earlier test in the same run had loaded `Heddle.Tests.dll` | `0db6222` — the harness loads what it references |
  | F-094 | `@model(){{X}}` can bind at build time and fail at first render; the harness's `Assembly.LoadFrom` was quietly suppressing the only place that was visible | `f872b79` — the call stays (a differential test asks whether two tiers emit the same bytes from the same inputs, and "the model assembly is loaded" is an input), but the property is asserted head-on against a GUID-named assembly built at test time, and a second test pins the suppression itself |

- **pinned by:** `ModelResolutionLoadOrderTests.TheHarnessLoadsExtraReferencesSoDifferentialSuitesDoNotDependOnTestOrder`,
  `.AModelTypeBindsAtBuildTimeFromAReferenceAndAtRunTimeOnlyOnceItsAssemblyIsLoaded`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelResolutionLoadOrderTests`.
- **notes:** class H — the instrument had no category for the defect. **Practical warning for anyone
  re-running these checks: do not run two `dotnet test` processes against this solution
  concurrently.** The suites share process-global registration state and probe assemblies (F-108),
  and a concurrent run can fail every row of an unrelated suite.

### F-079 — `Path.Combine` rejects characters on .NET Framework that .NET Core accepts

- **status:** KNOWN-OPEN
- **severity:** 3 (a throw out of the parse, on the platform where it can happen)
- **found:** closing round
- **symptom:** `Path.Combine` rejects `<`, `>` and `|` on .NET Framework and accepts them on .NET
  Core; `ParserSettings.ImportIdentity` canonicalises an `@<<` path to decide document identity.
- **fixed by:** partially — `0db6222` moved the combine inside `ImportIdentity`'s guard so a throw
  degrades the cache key instead of killing the parse. **Nothing on the development box can make it
  throw, so that change is reasoning, not evidence.**
- **pinned by:** `ImportCycleTests.AnImportPathThatCannotBeCanonicalisedStillParses` (`726031b`) — pins
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
  C# tier. **This is exactly the defect F-004's F-019 row fixed for .NET Core.**
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
  that page. **The mirror hazard, and it has bitten this register once:**
  `src/Heddle.LanguageServices.Tests` and `src/Heddle.Tool.Tests` are `net10.0`-only, so passing
  `-f net8.0` to either exits `NETSDK1005` and runs zero tests. Every check in this file against
  those two projects is written without `-f`.

### F-084 — Generated arithmetic inherited the consumer's overflow-checking setting — on both paths

- **status:** FIXED
- **absorbs:** F-095
- **severity:** 1
- **found:** cycle 8, cycle 9 (both reviewers leading with it)
- **the predicate:** the engine's arithmetic is built from the **unchecked** expression-tree
  factories, so it wraps whatever the host sets; bare emitted operators do not. The same template
  rendered `1410065408` on one tier and threw `OverflowException` on the other, decided by an MSBuild
  property the template knows nothing about.

  | id | path | fixed by |
  | --- | --- | --- |
  | F-084 | the native expression writer | `726031b` — emitted expressions wrapped in `unchecked` |
  | F-095 | **the commit said the class was closed and it was closed on one path.** The C# tier pastes the author's expression through a different method and was left bare, so a template still diverged for anyone using `ExpressionMode.FullCSharp` | `31e618d` — **both sides wrapped, engine included.** Wrapping only the generator would have traded one divergence for another: C# checks a *constant* expression whatever the compilation says, so `@(@100000 * 100000 * 100000)` would have moved from "both tiers refuse" to "precompiled renders, engine refuses" |

- **pinned by:** `CheckedOverflowContextTests.RuntimeOverflowWrapsWhicheverWayTheHostCompiles`,
  `.EmbeddedCSharpOverflowWrapsWhicheverWayTheHostCompiles`,
  `.AConstantOverflowInEmbeddedCSharpWrapsOnBothTiers`.
- **amended, cycle 23:** wrapping the emission made the constant-overflow refusal in `ConstantFolding`
  **stale**, and it stayed stale for the rest of the series — twenty-two spellings degraded that the consumer's
  compiler would have accepted. Measured and fixed as F-188. The general lesson: **a change that widens what
  the consumer's compiler accepts should be followed by re-deriving every refusal that existed only because it
  did not.** Nothing did that here for fifteen cycles.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CheckedOverflowContextTests`.
- **notes:** **F-095's fix changed the engine.** A constant overflow in embedded C# used to be a
  compile error and now wraps — accepted because the engine was already inconsistent with itself (its
  native tier builds `Expression.Multiply`, which is unchecked). Written up in the language reference
  with `checked(…)` as the escape hatch. Constant overflow in *native* expressions is a separate
  question and still degrades — F-024. Class D.

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
- **notes:** class J.

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
- **notes:** class J. The other cache-key defects are F-102 (a key missing a deciding term) and F-171
  (a key with a term the engine does not have).

### F-087 — An assembly that lost a name collision was retired for good

- **status:** FIXED
- **absorbs:** F-107
- **severity:** 3 (F-087), 6 (F-107)
- **found:** cycle 8, cycle 10
- **the predicate:** *when is an assembly's classification allowed to be final?* — and its mirror,
  *where does invalidating that classification actually matter?*

  | id | symptom | fixed by |
  | --- | --- | --- |
  | F-087 | an assembly was marked classified before the name was claimed, so the loser of a name collision was skipped on every later pass and stayed invisible to type resolution even after the name was freed | `726031b` |
  | F-107 | `InvalidateObservation` exists for a loaded assembly that **lost** a name collision — exactly the case where `TryAdd` fails — and two of its three call sites sat inside the *success* branch, where that case cannot occur. Plus a full re-classification pass per `Configure` that could not reach a different answer | `4680907` — both removed; the one in `UnregisterModelAssemblies`, where a name is actually freed, is real and pinned |

- **pinned by:** `AssemblyRegistrationTests.AFreedNameIsRetakenByTheLoadedAssemblyThatLostIt` (`31e618d`).
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AFreedNameIsRetaken`.
- **notes:** class J.

### F-090 — Properties claimed and pinned by nothing, found by mutation

- **status:** FIXED (F-090: three of five immediately, two in the follow-up; F-100: four of six, two
  are unpinnable and recorded as such)
- **absorbs:** F-100, F-160
- **severity:** 6
- **found:** cycles 8, 9 and 18 — each by mutating production code and watching every suite stay green
- **the predicate:** none. This is the *instrument*: mutate one production arm at a time and watch the
  suite. It has been run three times and found real dead rows every time. **Nothing runs it routinely.**

  | id | properties found unpinned | fixed by | pinned by |
  | --- | --- | --- | --- |
  | F-090 | negative shift-count folding (`if (places < 0) places = 0;` reddened 0 of 587 tests while producing a real host-build break); shift results were never byte-compared anywhere at all; `ImportIdentity`'s catch was unreachable by any test; the `ResolveOnly` exemption was satisfied by `Assert.ThrowsAny<Exception>` — **including the harness itself falling over**; the `Assembly.LoadFrom` in the differential harness was unpinned | `726031b` for the first four; the last became F-094, a row of F-077 | `ConstantArithmeticDifferentialTests.AShiftProducesTheSameNumberOnBothTiers`; `ImportCycleTests.AnImportPathThatCannotBeCanonicalisedStillParses`; `CorpusRenderParityTests.AnEntryDeclaredResolveOnlyGenuinelyDoesNotRender` (tightened) |
  | F-100 | hop-local name uniqueness; the `ProcessData` half of the definition recursion guard; both `PreparseCache` retire rules; and all three `AssemblyHelper` orderings. One of the newest tests was also retargeting the process-global cache into a band it never left, **silently disabling the C# tier cache for every test that ran after it** | `31e618d` | `NullSafeHopChainTests.TwoRefStructHopsInOneExpressionGetDistinctLocals`; `PrecompiledDefinitionRecursionTests.AFailedValueProducingRenderDoesNotSpendTheRecursionBudgetEither`; `PreparseCacheGenerationTests.*`; `AssemblyRegistrationTests.AFreedNameIsRetakenByTheLoadedAssemblyThatLostIt` |
  | F-160 | an assertion read `slotDyn` where `objectDyn` was meant, leaving the third scenario of its own test — the reference conversion the engine's table *does* allow — with **no byte assertion at all**, only tier-versus-tier equality (proven by changing that scenario's caller content: the render moved and the test still passed). And the inherited-`[DataType]` walk had no test naming it: reading only the extension type's own attributes left every suite green | `2d3fab6` — both; an extension declaring `int` over a base declaring `string` now pins the inherit from both sides, with a third type refused on both tiers | `AcceptedTypeTests.AnInheritedAcceptedTypeIsAcceptedAlongsideTheDeclaredOne`; `ObjectDefinitionBodyTests` (the corrected scenario) |

- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ImportCycleTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CorpusRenderParityTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~NullSafeHopChainTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~PrecompiledDefinitionRecursionTests`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~PreparseCacheGenerationTests`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AFreedNameIsRetaken`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AcceptedTypeTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ObjectDefinitionBodyTests`.
- **notes:** **two of the `AssemblyHelper` orderings cannot be pinned** — they are claims about what no
  concurrent caller can observe, and a racing test passes by luck when the code is wrong. That is
  written in `AssemblyRegistrationTests` in plain words rather than covered by a test that would imply
  more. **Do not "fix" it with a racing test.** Class G, sub-pattern "never pinned at all".

### F-091 — An accessibility gate narrower than its defect: one member kind, one reference kind, one position

- **status:** FIXED
- **absorbs:** F-092, F-096, F-097, F-103
- **severity:** 3 (F-091) and 2 (F-092, F-096, F-097); 6 (F-103)
- **found:** cycle 8 and its follow-up, cycle 9 (three of that cycle's findings), cycle 10
- **the predicate:** *may generated code in the consumer's assembly reach this symbol?* Every row
  below is that question asked for a smaller set than the emitter actually spells. **This is the
  class that was finally answered by enumeration in cycle 11 (F-098) and by sink enumeration in
  cycle 22 (F-176, F-177).**

  | id | what was outside the gate | symptom | fixed by |
  | --- | --- | --- | --- |
  | F-091 | internal *members* on a referenced model | the engine renders such a member; Roslyn's default `MetadataImportOptions.Public` makes it *absent* from the symbol model rather than inaccessible, so it is indistinguishable from a typo and drew `HED7008` at error severity — breaking the build over a template the engine renders | `f872b79` — a second view of the *same references* opened with `MetadataImportOptions.All`, built lazily and only on the path about to report a failure, held weakly against the compilation it describes. A member the engine's visibility policy accepts there and this compilation cannot see is *hidden*, not missing: internal member → degrade under `HED7030` (warning); **private** member → still `HED7008`, because the engine rejects private too and the tiers agree; typo → still `HED7008`. Source receivers skip the probe |
  | F-092 | internal *types* | **assumed to behave like F-091 and it does not.** No diagnostic fired at all; the emitter resolved the type, wrote its fully-qualified name into a generated cast, and the consumer's build died in a wall of `CS0122` against `.g.cs`, with no Heddle id and no `.heddle` position. Worse than the reported symptom and reachable by exactly the same model | `f872b79` — same probe as F-091, applied to the type |
  | F-096 | `CompilationReference`s | `HED7030` detected an internal member by its *absence* from the symbol model, which is true only of a metadata reference. A Roslyn workspace hands the generator a `CompilationReference` for a project reference, where the member is present — so it resolved and was emitted as `CS0122` in `.g.cs`. Detection-by-absence and detection-by-accessibility are two halves and only one had been built | `31e618d` |
  | F-097 | definition, slot and prop model types | the gate guarded `@model()` alone. Each of the other three resolved a type and wrote its name into a cast without asking whether this assembly may name it | `31e618d` |
  | F-103 | the `@model()` arm's own pin | widening the gate from `@model()` to every type the emitter spells (F-097) left the `@model()` arm itself unpinned: the one test covering it survived on the *member* rule the same commit added, because its fixture's member is declared on the internal type | `4680907` — a model whose member sits on a public base is the shape that separates them; removing the `@model()` call now reddens with six `CS0122`, measured |

- **pinned by:** `InaccessibleModelSymbolTests.AnInternalMemberOnAReferencedModelDegradesWithAWarningRatherThanFailingTheBuild`,
  `.APrivateMemberStaysAnError`, `.AMisspelledMemberOnAReferencedModelStaysAnError`,
  `.APublicMemberOnTheSameReferencedModelStillPrecompiles`,
  `.AnInternalReferencedModelTypeDegradesRatherThanEmittingACastTheConsumerCannotCompile`,
  `.AnInternalMemberOnAProjectReferencedModelDegradesRatherThanEmittingACS0122`,
  `.APublicMemberOnAProjectReferencedModelStillPrecompiles`,
  `.AnInternalTypeOnADefinitionDegradesRatherThanEmittingACastTheConsumerCannotCompile`,
  `.AnInternalSlotTypeDegradesRatherThanEmittingACastTheConsumerCannotCompile`,
  `.AnInternalPropTypeDegradesAudiblyRatherThanSilently`,
  `.AnInternalModelTypeWithAPublicInheritedMemberDegradesRatherThanEmittingACastTheConsumerCannotCompile`;
  `MetadataAccessibilityProbeTests.*` (`31e618d`), including
  `.AGrantedInternalMemberResolvesInsteadOfDegrading` for `[InternalsVisibleTo]`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~InaccessibleModelSymbolTests`
  and `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~MetadataAccessibilityProbeTests`.
  Narrower: `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AnInternalModelTypeWithAPublicInheritedMember` (F-103).
- **notes:** the obvious fix for F-091 — degrade whenever the receiver came from metadata — was
  rejected: models normally live in referenced assemblies, so that is precisely where `HED7008` earns
  its keep. The lesson the record draws from F-092: **when a report names one half of a mechanism,
  measure the other half rather than assuming it behaves the same way.** And from F-103: **widening a
  rule can silently orphan the narrow rule's only pin**, because the new rule answers the old fixture.
  Classes C and D.

### F-098 — Can generated code in the consumer's assembly name this type? Answered kind by kind, position by position

- **status:** FIXED (F-101 closed as subsumed in cycle 22)
- **absorbs:** F-101, F-105, F-110, F-119, F-122, F-125
- **severity:** 2 throughout; F-122 off-scale (diagnostic quality)
- **found:** cycles 9, 10, 11, 12, 13, and cycle 22 for the F-101 closure
- **the predicate:** **one question — *can generated code in the consumer's assembly name this type,
  take it as a parameter, and cast `object` to it?* — answered for one `TypeKind` at a time, by three
  commits in a row, before anybody asked it once.** F-110 is where it was finally answered as a
  predicate; F-119 and F-125 are the recursion positions that answer missed.

  | id | cycle | the kind or position outside the answer | symptom | fixed by |
  | --- | --- | --- | --- | --- |
  | F-098 | 9 | ref struct as a model | `@model(){{System.ReadOnlySpan<char>}}` emitted a signature that cannot compile, while the engine accepts the template and reports an ordinary catchable error at render | `31e618d` |
  | F-105 | 10 | static class as a model | `CS0721`: a static type cannot be a parameter, and the entry point takes the model as one. The engine declares no such parameter and renders the template. **Third cycle running that this guard was widened by one type kind at a time** | `4680907` — degrades |
  | F-110 | 11 | `void`, unbound generics, pointers, error-obsolete *containing* types and error-obsolete *property* types | five separate reported findings that were one incomplete answer to a single question | `4f299ce` — answered in two predicates: `ClassifyTypeName` (may a name be written where a value of it lives) and `ClassifyModelType` (adds ref-struct-ness, the one restriction that applies only to a value that has to box). That split is what lets `Buf.Length` stay precompiled while `Span<char>` as a model degrades. Every `TypeKind` has a recorded verdict, `void` is caught by `SpecialType` rather than by kind (it is a `Struct`), and `ContainsTypeParameter` walks array and pointer elements, type arguments *and* containing types |
  | F-119 | 12 | type arguments | nine spellings — `Span<char>[]`, `List<Math>`, `Nullable<Span<char>>`, `(Span<char>, int)` and others — still emitted a `.g.cs` the consumer cannot compile: the verdicts recursed into elements but not type arguments | `6d013eb` — one `Classify` serves both entry points, and the only verdict that varies by position is ref-struct-ness (allowed for a bare hop type; refused as a model, an array element or a type argument) |
  | F-125 | 13 | an *enclosing* type's arguments | cycle 12 removed a loop over a containing type's type arguments from `IsObsoleteError`, calling it a duplicate of the walk `Classify` performs. `Classify` walks a type's **own** arguments; nothing walked an enclosing type's. Measured at the parent: a property of type `Outer<Legacy>.Inner`, where `Legacy` is error-obsolete, is judged writable and the consumer's build dies on two `CS0619` | `27dd357` — restored **in `Classify`, not in `IsObsoleteError`**, so every verdict sees an enclosing type's arguments; it also subsumes the missing array/pointer arms of `ContainsTypeParameter` |
  | F-122 | 12 | the *severity* of the answer | a property whose type is merely *unusable* — a pointer, `void` — raised `HED7030`, which is reserved for faults an author can act on | `6d013eb` — the fault is carried rather than collapsed into a boolean |
  | F-101 | 9, closed 22 | a hop whose property *type* is internal | declined deliberately in cycle 9: checking it where the member check sits would falsely degrade the ordinary `m?.Inner?.Name` shape, where no type name is ever written. **Subsumed:** the form-aware check the entry asked for exists — `SymbolTypeResolver.cs:319-322` asks `ClassifyTypeName(prop.Type)` for **every** hop before `ResolvePath` returns `Resolved`, and `MemberPathWriter.cs:87`/`:93` is where that type is written (`default(T)` in the null-safe form, the receiver's own name in the ref-struct form). The model-only restrictions are deliberately not asked there, so the ordinary shape still precompiles | — |

- **measured for F-101 (cycle 22):** an error-obsolete property type degrades with `HED7030`; a merely
  *unusable* property type — a pointer — degrades in silence, which is the split F-122 introduced; a
  public-typed neighbour precompiles and renders byte-identically. **The literal shape F-101 names is
  not expressible in C#:** a *public* property whose type is `internal` is `CS0053: Inconsistent
  accessibility`, which is why no cycle in thirteen ever produced its repro.
- **residual cost, recorded:** F-101's error-obsolete row is a **degrade where the engine renders** —
  severity 4, exactly the cost the entry predicted when it declined the fix. It is not free; it is
  cheaper than the `CS0619` it replaces.
- **pinned by:** `TypeNameVerdictTests` (a 19-row table at first; rebuilt four times since — see
  F-126), including `.ARefStructIsWritableOnItsOwnAndRefusedInEveryNestedPosition`,
  `.AnOrdinaryConstructedTypeStaysNameableInEveryPosition`,
  `.AnErrorObsoleteArgumentOfAnEnclosingTypeIsRefusedOnTheEnclosingSpelling`;
  `UnnameableModelSymbolTests.ARefStructModelDegradesInsteadOfBreakingTheBuild`,
  `.ARefStructDefinitionModelDegradesInsteadOfBreakingTheBuild`,
  `.AStaticModelTypeDegradesRatherThanEmittingAParameterTheConsumerCannotCompile`,
  `.AModelTheConsumersCompilerWouldRejectDegrades`,
  `.AMemberWhoseTypeNoGeneratedCodeCanHoldDegradesWithoutAnAuthorFacingWarning`, and the
  property-type rows of `UnnameableModelSymbolTests` and `InaccessibleModelSymbolTests`; plus the
  hostile-model table in the integration suite.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableModelSymbolTests`.
- **notes:** F-125's "duplicate" verdict was reached by a reviewer who had only tried a ref-struct
  argument, which C# cannot declare; and it got authorised because the completeness suite pinned the
  answer and not the arm (F-126). A reported sibling route — `@model(){{List<Outer<Legacy>.Inner>}}` —
  **does not exist**: it degrades already, because the shared spelling grammar cannot resolve a nested
  type of a constructed generic at all. Measured and left alone. Class C: the *kinds* are enumerated;
  the *positions* were not, until cycle 22 enumerated the sinks — see F-176 and F-177.

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
- **notes:** the memo added here was itself wrong — F-102. Class F.

### F-102 — A cache key missing the term that decides the answer

- **status:** FIXED
- **absorbs:** F-113
- **severity:** 1 (F-102 latent; F-113 reachable)
- **found:** cycle 10 (**four of that cycle's eight findings were introduced by the previous commit**),
  cycle 11
- **the predicate:** *does this key identify the question being asked?* Twice it did not: once because
  it was a display string, once because a term was simply absent.

  | id | symptom | root cause | fixed by |
  | --- | --- | --- | --- |
  | F-102 | two references each declaring `Dup.Thing` produce one key, so the second walk was answered with the first's resolved type — which decides the null-safety form, the numeric widening, the formatter and the member name written into the file. The same key also made `["A.B"]` and `["A","B"]` one question | the memo added by F-099's fix keyed on the receiver's fully-qualified name plus the segments joined by a dot | `4680907` — the key is the receiver **symbol** under `SymbolEqualityComparer` and a separator no identifier can carry |
  | F-113 | the definition body is cached per definition and fills, so with two call sites into one `:: dynamic` definition **the first one's verdict stood for the second** | the key lacked the slot-value model, which is the value that decides the body | `4f299ce` — the key gained the slot-value model, as an ordinal id under `SymbolEqualityComparer`, **not a display string, because two distinct types can share a fully qualified name** — the lesson from F-102, two cycles earlier, and **the first time the loop reused one of its own findings** |

- **pinned by:** `PathMemoizationTests.TwoTypesSharingAFullyQualifiedNameGetTheirOwnResolutions`,
  `.ASegmentCarryingADotIsNotTheSameQuestionAsTwoSegments`;
  `SlotValueTypeTests.TheSecondCallSiteIntoADynamicSlotDefinitionIsCheckedOnItsOwnValue`;
  `DynamicDefinitionBodyTests.TheSecondCallSiteIntoADynamicDefinitionIsTypedOnItsOwnValue`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~PathMemoizationTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SecondCallSiteInto`.
- **notes:** **nothing end-to-end reaches F-102 today**, which is why it is pinned at the resolver
  rather than through a template, and the test says so. Class A. The body key's *other* two defects —
  a term the engine does not have, and two terms that decide nothing — are F-171.

### F-104 — `[Obsolete(…, error: true)]` is invisible to reflection and fatal to the consumer's build

- **status:** FIXED
- **absorbs:** F-157
- **severity:** 2 (F-157 was the highest-severity finding of its cycle, **pre-existing and on no
  known-open list**)
- **found:** cycle 10, cycle 18
- **the predicate:** reflection ignores `[Obsolete]` entirely, so the engine renders; every generated
  mention of the name is a `CS0619` against a `.g.cs` the consumer did not write, attributed to a
  `.heddle` file and carrying no Heddle id. The rule was asked at some of the names the emitter
  spells and not others.

  | id | name never asked about | symptom | fixed by |
  | --- | --- | --- | --- |
  | F-104 | the model type and the model member | every generated mention is `CS0619` | `4680907` — same id and same degrade as the accessibility case (`HED7030`), on the type and on the member; the id's message broadened to cover both causes |
  | F-157 | the **method** an exported call is written to | the generated file writes the call out as C# into the consumer's assembly, where the error form is `CS0619`. Measured four ways — expression position and `@out` slot position, with a `string` return and a class return — the consumer's build stopped at both commits, on a `.g.cs` no one can edit, over a template that is not at fault | `2d3fab6` — asked over both names the emitted call spells: the container type and the method |

- **pinned by:** `UnnameableModelSymbolTests.AnErrorObsoleteModelTypeDegradesRatherThanEmittingANameTheConsumerCannotCompile`,
  `.AnErrorObsoleteMemberDegradesRatherThanEmittingAReadTheConsumerCannotCompile`,
  `.AWarningLevelObsoleteModelStillPrecompiles`;
  `DeprecatedExportTests.AnExportTheConsumersCompilerRejectsDegradesInsteadOfBreakingTheBuild`,
  `.AnExportTheConsumersCompilerAcceptsStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableModelSymbolTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DeprecatedExportTests`;
  removing the method arm turns the degrading rows back into `CS0619` in the generated code.
- **notes:** the **warning** form deliberately does not degrade — the generated file's blanket
  `#pragma warning disable` keeps `CS0618` out of the build, and degrading would take every deprecated
  model in a codebase off the precompiled tier. Three near neighbours keep F-157 from being a refusal
  of exports, of `[Obsolete]`, or of the container: warning-level `[Obsolete]` must keep precompiling;
  an export whose *return* type the consumer may not name compiles and renders identically (the call
  site spells the method, not what it hands back); the same container's undecorated export is
  unaffected. **Cycle 19 corrected F-157:** three of the four measured cells were committed as rows
  and the fourth (slot-position-with-a-class-return) was measured and then not written down. It is a
  row now. The enclosing-type-arguments case is F-098's F-125 row; the *container* arm of F-157 is
  unreachable and deliberately kept — F-165. Classes C and D.

### F-106 — The slot-value type check, implemented for some value forms

- **status:** FIXED
- **absorbs:** F-116, F-121, F-135
- **severity:** 3 and 1 (F-106), 6 (F-116), 3 (F-121), 3 (F-135)
- **found:** cycles 10, 11, 12, 14
- **the predicate:** the engine type-checks **every** `@out` value against the declared slot type and
  refuses the template (`HED5014`). The emitter mirrored that for some value forms and not others,
  and each gap is a template that precompiles where the engine refuses.

  | id | value form outside the check | symptom | fixed by |
  | --- | --- | --- | --- |
  | F-106 | all of them — the emitter checked only that it was *inside* a slot definition | the same template precompiled and rendered, or threw `InvalidCastException` from the caller-content cast, depending on whether the caller's body happened to read a member | `4680907` — the emitter runs the engine's own conversion table with the same `allowBoxToObject: false` the slot caller passes, wherever it can type the value |
  | F-116 | literal slot values (skipped rather than typed from their decoded value); and the prop-default boxing arm had never had a test | severity 6 | `4f299ce` |
  | F-121 | the `null` literal | `null` types as `System.Object` to the engine and as "cannot say" to the emitter, so `@out(null)` into a typed slot precompiled what the engine refuses | `6d013eb` |
  | F-135 | `@out(this)` inside an `@list` body inside a slot definition | swept over seven slot parameter types, the parent precompiled all seven and rendered `[[c]]` — and **five of those seven are templates the engine refuses outright** (`string`, `int`, `bool`, `decimal` and an unrelated class, each `HED5014`) | `e14e862` — degrades exactly those five and keeps the two the engine accepts (the element type itself, and `object`) |

- **pinned by:** `SlotValueTypeTests.ASlotValueOfAnUnrelatedTypeDegradesInsteadOfRenderingWhatTheEngineRefuses`,
  `.ASlotValueOfAnUnrelatedTypeDegradesEvenWhenTheCallerContentReadsAMember`,
  `.ASlotValueThatWouldHaveToBeBoxedDegrades`, `.AnAssignableSlotValueStillPrecompiles`,
  `.ALiteralSlotValueOfAnUnrelatedTypeDegrades`, `.AnAssignableLiteralSlotValueStillPrecompiles`,
  `.ANullSlotValueDegradesBecauseTheEngineTypesItAsObject`,
  `.ANullSlotValueStillPrecompilesForAnObjectSlot`;
  `NullableDefaultDifferentialTests.AValueTypeDefaultOnAnObjectPropPrecompilesAndReproducesTheBoxedType`;
  `DynamicDefinitionBodyTests.AnOutValueInsideAListBodyIsCheckedAgainstTheElementType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SlotValueTypeTests`,
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DynamicDefinitionBodyTests`.
  Narrower: `--filter FullyQualifiedName~ANullSlotValue` (F-121).
- **notes:** typing the null literal (F-121) is what made the *next* cycle's divergence reachable — a
  `null` call-site value produced a page the engine will not compile at all (F-112's F-124 row).
  F-135 was **a declared cost in cycle 10** that turned out to be the reverse — the gap was not a
  conservative degrade, it was an unchecked precompile. **When a record says "we deliberately do not
  check X", measure what happens in X's absence.** The `:: dynamic` slot arm's own history is F-112.

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
- **fixed by:** `4680907` — deleted at process exit, best effort (`ProbeAssemblyFiles`,
  `src/Heddle.Tests/ProbeAssemblyFiles.cs`); the `finally` now runs only on the paths that left its
  own registration in place.
- **regression check:** `ls src/Heddle.Tests/bin/**/*.dll` for GUID-named leftovers after a run.
- **notes:** the process-global state this describes is why **two concurrent `dotnet test` processes
  over this solution can fail every row of an unrelated suite.** Run the register's checks serially.

### F-109 — `ThreadLocal` per definition, reported as an unbounded slot table

- **status:** NOT-A-DEFECT
- **severity:** —
- **found:** cycle 10 (reported), measured and dismissed in the same cycle
- **symptom (as reported):** `DefinitionBaseExtension` holds one `ThreadLocal<int>` per definition per
  compiled template and never disposes it.
- **measurement — this is what stops it being re-reported:** `ThreadLocal<T>` carries a finalizer that
  returns the id, so ids recycle without `Dispose`. 200,000 definition instances over 10,000
  compile-and-render cycles left the per-thread slot array at **256** entries, flat from the first
  round; 10,000 cycles of a single-definition template left it at 1024. The array tracks the peak
  number of instances awaiting finalization. A tight allocation loop that outruns the finalizer does
  grow it (100,000 abandoned instances reached 131,072 slots) — **a shape no template workload has.**
- **fixed by:** — nothing changed.
- **regression check:** re-run the loop only if the disposal pattern changes.

### F-111 — The `@model` spelling: what the grammar accepts, what the gate reports, what is emitted

- **status:** FIXED
- **absorbs:** F-120, F-123, F-127, F-129, F-134
- **severity:** 2 (F-111, F-123, F-127), 3 (F-120), 6 (F-129, F-134)
- **found:** cycles 11, 12, 13, 14
- **the predicate:** three separate questions that were repeatedly confused for one — *does the shared
  grammar parse this spelling*, *does the gate report it as unknown*, and *what does the emitter
  write into the entry point's parameter type*. **F-127 is the sharpest statement of the split: what
  the gate diagnoses and what gets emitted are separate questions, and only the second was wrong.**

  | id | cycle | symptom | fixed by |
  | --- | --- | --- | --- |
  | F-111 | 11 | an `@model` text resolving to no symbol was written straight into the entry point's parameter type, which is why `System.Int32*` never reached any type-kind guard | `4f299ce` — fixed where the symbol is resolved rather than where the kinds are listed. **Re-opened and completed by F-127** |
  | F-120 | 12 | `@model(){{int?}}` bound a strategy for a template the engine cannot resolve at all: a `?`-suffix prelude existed in the generator and in no shared grammar. Checking all four positions rather than the brief's assurance showed the runtime accepts `?` **nowhere** — and that `:: T?` on a definition does not "work" either, it silently drops the `?` and compiles the body against the unlifted type | `6d013eb` — prelude deleted; `?` now reaches the same `HED7007` the engine's refusal mirrors. Rows added to both lockstep corpora, whose stated job was this parity and which had none |
  | F-123 | 12 | a dotted `@model` spelling whose last segment happens to name a real type emitted raw text with no diagnostic at all | `6d013eb` — the existence check is a dot-bounded suffix match: generous to a bare name, strict about namespace segments the author actually wrote |
  | F-127 | 13 | `IntegrationTests.Fixtures.Article`, `Fixtures.Article` and `System.Collections.Generic.List` all pass the name-existence gate — deliberately, since the runtime binds over what is *loaded* — and all three put four `CS0246` or `CS0305` into the consumer's build. The engine refuses every one of them. **The gate is unchanged; the emission was the defect** | `27dd357` |
  | F-129 | 13 | three separate mutations of the dot-boundary rule left every test green. The same gate composed a qualified name per type visited, over the whole reference closure, on a path that runs **per keystroke** in an editor | `27dd357` — rows that end mid-identifier (the only thing separating it from a plain suffix test) and one where every segment is real and only the separator is not a dot; matching goes segment by segment from the right. Equivalence measured: 4,097 spellings drawn from the closure, zero disagreements; three full-closure misses fall from 11,528,976 bytes to 2,206,320 |
  | F-134 | 14 | the previous cycle's record claimed the rows it added covered the gate's global-namespace stop. They end mid-identifier; the stop is about running out of namespaces with spelling left over. Removing it turns every leading-dot spelling — `.System.Object` and its kind — from refused into accepted; **241 such flips over 6,857 sampled spellings** | `e14e862` |

- **pinned by:** `TypeNameVerdictTests.ANameGeneratedCodeCannotWriteIsRefusedWithoutADiagnosticToShowForIt`;
  `ModelTypeSpellingTests.ANullableSuffixOnTheModelDirectiveIsRefusedOnBothTiers`,
  `.TheNullableSpellingBothGrammarsDoHaveStillPrecompiles`,
  `.ADottedModelSpellingThatBindsNowhereIsReported`, `.ADottedModelSpellingThatBindsIsLeftAlone`,
  `.AModelSpellingThatResolvesToNoSymbolIsNeverWrittenIntoTheEntryPoint`,
  `.AModelSpellingThatResolvesStillPrecompilesWithTheSameBody`,
  and the `.Probe.Only.UniqueProbe` row (the one that reddens when the global-namespace stop goes);
  `TypeSpellingSymbolLockstepTests.ASpellingIsOnlyKnownWhenEverySegmentOfItIs` (**this is where the
  dot-boundary rule is pinned — not in `ModelTypeSpellingTests`, where an earlier draft of this
  register put it**); `TypeSpellingLockstepTests`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelTypeSpellingTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableModelSymbolTests`.
  Mutations that must redden: remove the global-namespace stop (the `.Probe.Only.UniqueProbe` row);
  mutate the dot-boundary rule three ways.
- **notes:** the test added in cycle 12 for F-111 asserted "no errors" over a body that degraded for
  an unrelated reason, so the generated file that could not compile was never built — class G's
  "fixture degrades for an unrelated reason" pattern. Its body is static text now. Classes A and C.

### F-112 — What model a body gets from its call site — the most-revised rule in the series

- **status:** FIXED
- **absorbs:** F-117, F-124, F-130, F-150, F-156, F-161, F-170
- **severity:** 1, 3 and 4 across the rows — silent wrong output, build-time refusals the generator
  precompiled, and working templates taken off the tier
- **found:** cycles 11, 12, 13, 14, 17, 18, 19 and 21 — **eight cycles, one rule.**
- **the predicate, as the engine states it:** `HeddleCompiler.CreateExtension` asks
  `acceptType == typeof(object)`, and `ResolveType(definition.ModelType) != typeof(object)` for a
  region. The parser fills in `object` when no `::` is written and the alias table maps `dynamic` to
  `object`, so `dynamic`, `object`, `System.Object` and declaring nothing all fail that test alike —
  and the engine then compiles the body **once per call site, against the static type of the value
  that call site passes.** Only `dynamic` additionally sends the model accessor down its dynamic exit.
  The emitter got this wrong seven times in a row, each time from a different direction.
  **Measure the engine over a matrix before changing anything here.**

  | id | cycle | what the emitter keyed on instead | symptom, both tiers | fixed by |
  | --- | --- | --- | --- | --- |
  | F-112 | 11 | a blanket refusal of `:: dynamic` slot definitions (added the previous cycle) | a reusable wrapper whose `@out` values are all assignable lost the precompiled tier silently, with both tiers producing identical bytes. **Nothing reddened, because only two corpus templates declare a slot and both use typed body models** (severity 4) | `4f299ce` — the refusal is per call site, against the static type the caller actually passes: strictly more precompilation than before, and it still refuses only the case that genuinely cannot be decided |
  | F-117 | 12 | an *assumption* that the engine compiles such a body against the value the caller passes | it does not: `CompileModelAccessor` takes a dynamic exit before resolving anything whenever the callee declares `:: dynamic`, so a bare call or a member-path argument gives the body `ExType.Dynamic` and every `@out(this)` inside it is `HED5014`. Two of six call forms precompiled templates the engine refuses outright | `6d013eb` — **the fix mirrors the measured engine rather than either reviewer's proposal.** One proposed a matrix, one a conservative `this`-only rule; measuring showed the matrix right and the conservative rule wrong in the other direction, because a *caller's prop read* keeps its static type through a `:: dynamic` definition (the prop-read path runs before the dynamic check) |
  | F-124 | 13 | the computed call-site model was used only for type-checking the body's `@out` values, leaving the body itself dynamic | `this` gives the caller's model, a literal gives the literal's type, `null` gives `System.Object`; only a bare call and a member path reach the accessor's dynamic exit. A body member the type does not carry is `HED0001` at compile time. So in every cell where the model was static and the member missed, generated code threw `RuntimeBinderException` at render where the engine had refused the template, and **where the value was `null` the dynamic read yielded empty and the page rendered what the engine will not compile at all** | `27dd357` — the body is built in a *typed* context off that same model, so the existing member-path machinery answers with `HED7008` and a degrade, mirroring the engine's `HED0001`. All 33 probed cells agree. The body cache is keyed on the call-site model |
  | F-130 | 14 | "the emitter cannot say" read as "the engine has no type" | `TryTypeCallSiteBody` returned success for a plain definition on `null` and left the body dynamically bound. The engine signals a genuinely untyped model separately and definitely, as `dynamic`. Every call form outside cycle 13's matrix diverged: a computed native expression, a chained call and `this` inside an `@list` body all carry a real static type in the engine. Measured at the parent with a body reading a member the type lacks: the engine refuses at compile time in every one, naming the type (`[Int32]`, `[String]`, `[Boolean]`, `[MenuOption]`, `[Object]`), and the generated tier precompiled and either threw `RuntimeBinderException` at render or **rendered a page the engine will not compile at all** | `e14e862` — `null` degrades in plain mode as in slot mode, and two engine rules are mirrored to get the precompilation back: a computed expression's type comes from the shared operator tables (`NativeOperatorRules.BinaryResult`/`UnaryResult`/`ClassifyTernary`), and the model inside an `@list` body is the collection's `IEnumerable<T>` argument (`dynamic` for a collection implementing no generic form, "cannot say" when a type reaches `IEnumerable<T>` more than once) |
  | F-150 | 17 | the rule applied to the definition **body** only | the content the *call site* hands the definition kept the untyped context though it is compiled against the same model. Swept over twenty-one shapes: `@frame("ab"){{(@(Title))}}` and `@frame(this){{(@(Nope))}}` are refused by the engine at compile time (`HED0001`, naming `String` and the caller's model) and the generated tier **precompiled** | `669eef8` — the same rule types both |
  | F-156 | 18 | the **word** `dynamic` | `:: object` and an undeclared model carried the whole class its twin had just had fixed, in both directions: `<s(out:: object)>{{[@out(this)]}} :: object` called `@s(5)` — the engine refuses (`HED5014`, boxing switched off for this check) and the generated tier precompiled and rendered `[\|5\|]`; `<frame>{{@for(this)}} :: object` + `@frame(2)`, `<frame>{{@list(this)}} :: object` + `@frame("ab")`, `<frame>{{[@(Length)]}} :: object` + `@frame("ab")`, the same with caller content, and the undeclared form of each — the engine renders all of them and the generated tier degraded | `2d3fab6` — re-keyed on the engine's predicate. **The two spellings do not collapse:** only `dynamic` sends the model accessor down its dynamic exit, so under `:: object` a member path at the call site resolves statically and a missing member is a compile-time refusal where the `:: dynamic` twin is a render-time throw both tiers share. Both halves are pinned |
  | F-161 | 19 | an arm added *alongside* F-156's fix: a caller whose own scope has no static type gives the body `dynamic` | **a regression introduced by the previous commit, and by the correction it added alongside a fix, not by the fix.** Over a model-less document whose component declares `p: string`, fills a region, and calls `@d(p)` from that region body, with nothing spelling `dynamic` anywhere but the `@model` directive: `<d>{{@for(this)}}` — the engine refuses (`HED0004`, `String` against `Range`/`int`); the commit precompiled it and rendered a bare newline, with no diagnostic at build or at run (the body model was `dynamic`, so the accepted-type gate took its dynamic exemption and `@for`'s `[DataType]` never ran). `<d>{{[@(Nope)]}}` — the engine refuses (`HED0001`, on `String`); the commit precompiled it and threw `RuntimeBinderException` at render. The same through the caller's content. **The parent commit degraded all three.** A model-less document is nothing but that shape, so answering "cannot say" there took every one off the precompiled tier | `f990a9c` — carry the host's `DynamicBodyModel` into the region body. **Which of the two candidates was the root was decided by measurement:** adding the missing prop-read guard to the arm closes all three faces but also degrades a template the engine renders; carrying the model closes them and costs nothing. Degrade cost: zero |
  | F-170 | 21 | the **text** of a *region's* declaration | `TryRegionBodyContext` branched on the declaration's text — empty or the word `object` took the arm that types the body from the call site, and everything else went to `DefinitionBodyContext`, whose untyped answer for `dynamic` and for any spelling resolving to `System.Object` left the body with no model at all. **No region path called `TryTypeCallSiteBody`.** With a host whose element shadows one member and a single call site: `@%<row>{{[@(Tag)] [@(Missing)]}} :: dynamic%@@list(Items){{@row(this)}}` inside a component body — the engine refuses at compile (`HED0001`, naming the element type); the generated tier precompiled, the consumer's build was green, and it threw `RuntimeBinderException` at render. The silent-wrong-output face, two call sites: `@%<row>{{[@(Tag)]}} :: dynamic%@@row(this)\|@list(Items){{@row(this)}}` — the engine prints `[host]\|[host]`, the generated tier `[host]\|[element]`. Both render; no diagnostic on either tier | `27dd357`-line successor, cycle 21 — `TryRegionBodyContext` resolves the declaration first and asks whether the answer is `System.Object`, then routes that arm through `TryTypeCallSiteBody`: the same rule and the same code a non-region definition takes. **Widening the string test to include `dynamic` is not the fix** and was measured: it closes the `dynamic` cells and leaves every `System.Object` cell open |

- **class expansions recorded (do not repeat these; extend them):**
  - F-124: 33 probed cells, all agree after.
  - F-130: over **twenty** call-site value forms the parent degrades **one** and the commit degrades
    **two**.
  - F-150: 21 shapes; sixteen matched before, eighteen after; the three still degrading are F-154.
  - F-170: 24 cells per spelling, five spellings, both region declaration syntaxes. Before: nothing /
    `:: object` correct; `:: dynamic` 11 generator-renders-where-engine-refuses and 2 different-bytes;
    `:: System.Object` 15 and 3; a declared real type correct. After: 18 cells moved to both-refuse,
    4 different-bytes to matching, and a further **16 that had been degrading now precompile** and
    render the engine's bytes.
- **pinned by:** `SlotValueTypeTests.ASlotDefinitionWithADynamicBodyModelStillPrecompilesWhenTheCallerValueFits`,
  `.ASlotDefinitionWithADynamicBodyModelDegrades`,
  `.ACallFormThatGivesTheBodyADynamicModelDegrades`,
  `.ADynamicModelStillPrecompilesWhenTheOutValueNeverReadsIt`,
  `.ACallerPropReadKeepsItsStaticTypeThroughADynamicDefinition`;
  `DynamicDefinitionBodyTests.ABodyReadingAMemberTheCallSiteValueLacksDegradesInsteadOfBindingItDynamically`,
  `.ABodyReadingAMemberTheCallSiteValueHasStillPrecompiles`, `.ABareCallKeepsAnUntypedBodyOnBothTiers`,
  `.AMemberPathCallKeepsAnUntypedBodyOnBothTiers`, `.TheSecondCallSiteIntoADynamicDefinitionIsTypedOnItsOwnValue`,
  `.AComputedCallSiteValueTypesTheBodyToTheSameTypeOnBothTiers`,
  `.AComputedCallSiteValueStillPrecompilesWhenTheBodyFitsIt`,
  `.TheValueInsideAListBodyIsTheElementTypeOnBothTiers`,
  `.ALiteralCallSiteValuePrecompilesWhenTheBodyFitsIt`,
  `.CallerContentUnderADynamicCalleeIsTypedByTheCallSiteValue`,
  `.CallerContentStillPrecompilesWhereTheEngineCompilesIt`;
  `ObjectDefinitionBodyTests.ABodyCallingForOnItsOwnModelPrecompilesUnderAnObjectDefinition`,
  `.ABodyCallingForOnAModelForDoesNotAcceptDegrades`,
  `.ABodyReadingAMemberOfItsOwnModelPrecompilesUnderAnObjectDefinition`,
  `.CallerContentUnderAnObjectDefinitionIsTypedByTheValueToo`,
  `.ADefinitionThatDeclaresNoModelBehavesTheSameWay`,
  `.ASlotValueTheEngineWillNotBoxDegradesUnderAnObjectDefinition`,
  `.AMemberPathCallSiteTypesAnObjectBodyStatically`;
  `DefinitionTests.ADefinitionCalledFromARegionBodyIsTypedByTheHostsProp`,
  `.ARegionBodyReadingItsHostsPropDirectlyStillPrecompiles`,
  `.ADefinitionCalledFromARegionBodyIsRefusedOnTheHostsPropType`;
  `RegionTests.ARegionWhoseModelResolvesToObjectIsTypedByItsCallSite` (four spellings),
  `.ARegionReadingAMemberTheElementHasStillPrecompiles`,
  `.ASharedRegionBodyPrintsTheFirstCallSitesModelWhateverTheSpelling`,
  `.ARegionDeclaringARealTypeIsStillTypedByIt`,
  `.RegionDeclaringNoModelTakesTheValueItsCallSitePasses`.
- **regression check:** all four suites, and run all four —
  `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~SlotValueTypeTests`,
  `… --filter FullyQualifiedName~DynamicDefinitionBodyTests`,
  `… --filter FullyQualifiedName~ObjectDefinitionBodyTests`,
  `… --filter FullyQualifiedName~DefinitionTests`,
  `… --filter FullyQualifiedName~RegionTests`.
  Mutations recorded as reddening: restoring F-170's spelling test reddens 10; widening it to
  `dynamic` still reddens the `System.Object` rows.
- **notes:** **the record corrects itself twice inside this class.** (1) F-124's section "says was
  closed rather than a narrower version of the fix" — a caller value the emitter could not type was
  handled by mode, and the matrix contained only four call forms. (2) F-130: an earlier draft, and
  commit `e14e862`'s own message, report "of thirteen probed shapes, eight degraded before and one
  does now". That is not a parent-versus-child count and **no commit reproduces it**; believe the
  corrected twenty-form figure above. The paired assertion that makes F-130 testable: **the type named
  in the generator's `HED7008` and in the engine's `HED0001` must agree** — a degrade assertion alone
  would pass whatever type the emitter picked, and a wrong type is a cast the generated body throws
  on. **Reach is wide, not exotic:** `IsRegion` is `InDefintionContext`, so every definition nested
  inside a component body is a region. The durable lesson of F-161: **a correction bolted onto a fix
  inherits the fix's credibility and carries none of its measurement. When a new arm turns out to have
  exactly one caller, suspect the caller.** Classes A, D, E and F all claim members here.

### F-114 — A reported repro that does not compile

- **status:** NOT-A-DEFECT (as reported) — the underlying defect is real and is covered by F-098
- **severity:** —
- **found:** cycle 11
- **symptom (as reported):** the error-obsolete property-type case was reported with
  `public Money Balance` where `Money` is error-obsolete.
- **measurement — this is what stops it being re-reported:** that model is itself `CS0619`, so it
  could never have been built. Reaching the defect needs the property to carry its own *warning*-level
  `[Obsolete]` (an obsolete context suppresses the diagnostic on the types it mentions), or a model
  library not rebuilt since the type was deprecated.
- **notes:** recorded because **the report was accepted on its reasoning and the reasoning was checked
  against the compiler rather than against the reviewer.** Do the same with any repro that depends on
  a fixture compiling.

### F-126 — Verdict rows answered by an arm other than the one they name

- **status:** FIXED
- **absorbs:** F-133, F-139, F-146
- **severity:** 6
- **found:** cycles 13, 14, 15, 16 — **the type-kind verdict table was rebuilt four times**
- **the predicate:** *does this row fail when the arm it names is broken?* Every row below passed for
  a reason other than the one its name claims, and the instrument that finds them is the same each
  time: mutate one verdict arm and watch which rows redden.

  | id | cycle | symptom, with the settling mutation | fixed by |
  | --- | --- | --- | --- |
  | F-126 | 13 | the suite asserted only that each subject was refused and that the reason was non-empty. **That is how the wrong deletion (F-098's F-125 row) got authorised:** with an arm gone, a different arm answered its rows, the suite stayed green, and mutation testing reported the arm as dead | `27dd357` — the rows carry the words their reason must contain. Two things fell out immediately: `unbound-generic` is answered by the **error-type** arm, not the type-parameter one (Roslyn fills an unbound generic's arguments with error symbols), and the row named `array-of-ref-struct` was built over `Span<T>`, the open definition, so it was refused for being an open generic and **never consulted the ref-struct rule at all** |
  | F-133 | 14 | `Classify`'s `TypeKind` switch grouped `Unknown` with `Error` and `Module` with `Submission`, and no C# compilation produces a symbol carrying either of the first two — so through symbols alone both labels could be deleted with every suite green | `e14e862` — the kind table is a function of `TypeKind`, asked of every kind the enum declares, plus a row asserting the rows cover the enum, so a kind Roslyn adds later arrives with no verdict and the theory does not compile past it |
  | F-139 | 15 | the table's nine `null` rows all asked `UnnameableKind` for a null answer, which is **the same `default:` arm nine times**: they passed and failed together, and none was about the kind it named | `c1b20dd` — each row is asked of a real symbol of that kind, through the classifier the emitter actually calls. Settling mutation: making the classifier refuse every enum type leaves the old rows entirely green and reddens exactly the new `Enum` row |
  | F-146 | 16 | `Classify` short-circuited an array into a recursion on its element *before* consulting `UnnameableKind`, so `UnnameableKind(TypeKind.Array)` was unreachable and the `Array` row was a second copy of its element's row. Measured at the parent: an `Array` case left all thirty-seven rows green while each of the other six kinds reddened its own | `2664141` — the table is consulted first (it returns null for `Array`, so no verdict changes) |

- **pinned by:** `TypeNameVerdictTests` — the reason-text rows, `.TheKindRowsCoverTheEnum`,
  `.TheKindNamesNoRowCanBeAbout`, `.AKindTheTableRefusesCarriesItsOwnSentence`,
  `.AKindTheTableAllowsIsWrittenForASymbolOfThatKind`, and the `Array` case.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TypeNameVerdictTests`.
  Mutations: mutate one verdict arm at a time and confirm the suite reddens — nineteen mutations were
  run, seventeen redden the completeness suite and the eighteenth reddens the hostile-model table;
  deleting either of F-133's labels reddens; an `Array` case now reddens the `Array` row.
- **notes:** **`Struct` reddens two rows, its own and `Array`, and that is not removable**: an array is
  answered by recursing onto its element and every element has a kind with a row. The docstring says
  so instead of claiming independence. "Each row stands alone" held for six of the seven after F-139
  and was corrected by F-146. Two rows in this table **cannot be honestly pinned at all** — F-140,
  known-open. Class G, sub-pattern "a row answered by a different arm".

### F-128 — A root reference as a slot value, reported as a latent hole

- **status:** NOT-A-DEFECT
- **severity:** —
- **found:** cycle 13
- **symptom (as reported):** `@out(::X)` is never type-checked.
- **measurement — this is what stops it being re-reported:** it cannot become one. `BuildParamExpr`
  refuses every root-reference call parameter outright, so such a template degrades before anything is
  emitted, whatever the type check would have said. Measured over four shapes, including one the
  engine renders and the emitter declines.
- **notes:** a note in the code records it so the next reader does not have to measure it again.

### F-131 — A reader that does not consult the prop layout, and a context that does not carry it

- **status:** FIXED
- **absorbs:** F-136, F-137, F-141, F-142, F-143
- **severity:** 1, 2, 3 and 4 across the rows
- **found:** cycles 14, 15 (both reviewers independently), 16 (both reviewers independently)
- **the predicate, as the engine states it:** `NativeExpressionCompiler` tries the **active prop
  layout on a path's first segment before it looks at the scope type at all**; and the engine compiles
  caller content *before* it installs the definition's own prop layout and slot parameter type, so the
  caller's layout and slot mode are still active there. Every row below is a context that did not
  carry the layout, or a reader that did not consult it.

  | id | cycle | symptom, both tiers, at the parent commit | root cause | fixed by |
  | --- | --- | --- | --- | --- |
  | F-131 | 14 | `<host(Name: string)>{{@list(Products){{@(Name)}}}}` rendered `[PROP]` on the engine and `[ELEMENT]` on the generated tier. Both precompiled, no diagnostic, different bytes — and the same template with the `@list` removed, or with `@if` in its place, agreed | the engine saves and restores the active prop layout around **definition bodies only**, so an `@list` body nested inside a definition still resolves its first path segment as a prop; the emitter built the item context with no layout | `e14e862` — the layout propagates |
  | F-136 | 15 | two faces, neither needing a shadowed name: a prop the model has **no** member of — the ordinary case, `<host(n: int = 5)>{{[@(n + 1)]}}` — resolved to nothing and reported `HED7008` at **Error**, so a template the engine compiles and renders broke the consumer's build; and a prop that *did* shadow a member compiled quietly and rendered the model's value — `[8]` where the engine renders `[P1]`, `[t]` where it renders `[a][b]`, `[False]` where it renders `[True]` | the emitter's member-path reader mirrored the engine's prop-first order, and **the expression writer was constructed with the model and no layout** | `c1b20dd` — both paths take the layout and resolve the first segment prop-first, hopping the rest off the slot's declared type |
  | F-137 | 15 | a shadowed name got the shadowed *member's* type, so `Cols + 1` over a `string` prop was typed `Int32`, and the body built off that type reported a member the `string` it really receives has — `HED7008` at Error over a template the engine renders — or refused a `string` slot the engine fills | `ComputedValueType` resolved off the model and did not even take the body context the layout lives on. **The previous cycle's new typing (F-112's F-130 row) is what made it visible**; before that the two mistakes cancelled | `c1b20dd` |
  | F-141 | 16 | four faces, model `GridModel { Name = "model", Cols = 7 }`: `<outer(Cols: string = "PP")>{{@inner(this){{[@(Cols)]}}}}` — engine `([PP])`, generated `([7])`, silent, both tiers rendering, different bytes (the native form `@(Cols + "!")` is `([PP!])` against `([7!])`); `<outer(label: string = "PP")>` with `@(label)` — engine renders `([PP])` and **the build fails** with `HED7008: 'GridModel' does not contain an accessible member 'label'`, **the symptom the previous commit's message claims to have eliminated, alive one context over**; `@list(Name)` in caller content, prop `"PP"` against member `"ab"` — engine `([P][P])`, generated `([a][b])`, the enumerability gate having judged the *model member's* type; a valued `@out` in caller content lexically inside a slot definition's body — engine renders, the emitter dropped the whole template, and the bare `@out()` in the same place is the reverse (engine refuses with `HED5013`, emitter precompiled and rendered) | the emitter built the caller-content context from the callee and carried neither the layout nor the slot mode | `2664141` — both travel now |
  | F-142 | 16 | `<inner(q: int = 0)>` called as `q: Cols` with `<outer(Cols: string = "PP")>` rendered `[PP]` — a `string` boxed into an `int`-declared slot — where the engine refuses the template with `HED5003`. Reversed (`q: string`, `Cols: int`) it rendered `[5]` against the same refusal. Prop-specific: a model member or a literal in the same position always precompiled | `TryBuildDynamicSetter` typed the argument off the caller's model while the writer three lines below emitted the caller's *prop*. **Worse than either half alone: the check and the emission disagreed about which value the argument even is.** Pre-existing, but F-136's fix is what made the two halves disagree | `2664141` |
  | F-143 | 16 | `<host(n: int = 5)>{{@list(Tags){{[@(n + 1)]}}}}` dropped the whole template where the engine renders `[6][6]` (severity 4) | `BuildParamExpr` refused every native expression when the context was dynamic — an `@list` body's tier — **before** it looked at the layout | `2664141` — **the guard is gone rather than narrowed:** the writer, given no model type, already refuses any path that reads one, which is the engine's own order and answer |

- **pinned by:** `PropsTests.APropSurvivesIntoANestedListBody` (the element row next to it — a name the
  layout does *not* carry still reads off the element — is what keeps this prop-first rather than the
  layout swallowing the body), `.ANativeExpressionReadsAPropBeforeTheModel`,
  `.APropInAListDataExpressionIsTheProp`, `.AShadowingPropsTypeDecidesADynamicDefinitionsModel`,
  `.AShadowingPropsTypeDecidesWhatASlotAccepts`, `.AnIntPropThroughTheSameShapeIsRefusedByBothTiers`,
  `.AnIntPropIntoAStringSlotIsRefusedByBothTiers`, `.CallerContentKeepsTheCallingDefinitionsProps`,
  `.SlotModeCallerContentKeepsThemToo`, `.CallerContentInsideASlotBodyIsStillInsideTheSlot`,
  `.APropArgumentIsCheckedAgainstThePropItReads`, `.APropArgumentThatFitsStillPrecompiles`,
  `.APropRootedExpressionNeedsNoModelInsideAListBody`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~PropsTests`
  — one command covers every row above.
- **notes:** measured over forty prop shapes for F-136: fourteen matched, five rendered different
  bytes and twenty-one degraded before; thirty-nine match and one degrades after, and the one is a
  template the engine refuses. **The record's "four paths" count was corrected by cycle 16 to at least
  six readers and at least six contexts** — see the two enumeration tables in
  `docs/generator_plan/phase-8-docs-sweep.md` before assuming this class is closed; F-141 is Table A
  row 9 and F-142 is Table B row 5. `RegionHostProps` is a second copy of `Props` at every site that
  sets either, so Table A rows 4 and 5 agree with row 6 by construction. Two over-degrades fell out of
  F-142's change and were kept deliberately: a non-shadowing prop argument now precompiles, and a
  prop-rooted argument at a dynamic-tier call site no longer needs a typed caller model. F-143's
  change also moved `at-escape-comment-adjacent.heddle` from `FallsBackSafely` to `Precompiles` in
  `src/TestCorpus/CorpusIntent.cs`. **Class B — and the series' one enumeration success story; read
  the class-B section before adding any `BodyContext` construction site.**

### F-138 — An extension's accepted type, checked for the one extension in front of the author

- **status:** FIXED
- **absorbs:** F-149
- **severity:** 1 (both)
- **found:** cycles 15 and 17
- **the predicate, as the engine states it:** an extension declares the type it accepts via
  `[DataType]` and the engine checks the call's value **before it compiles anything**. That is a rule
  over every extension, not a list of two.

  | id | cycle | symptom, both tiers | fixed by |
  | --- | --- | --- | --- |
  | F-138 | 15 | `@list` accepts `IEnumerable`, so `@list(Obj)` over an `object`-typed member is a template the engine refuses (`HED0004`). The emitter mirrored no such check, so it precompiled and rendered — walking a string's characters where the value happened to be a string, rendering nothing where it was an `int` | `c1b20dd` — the check is on the value's *static* type, so a `dynamic` value and a value the emitter could not type are both exempt |
  | F-149 | 17 | the check was written out for `@list`'s `IEnumerable` and for nothing else, leaving `@for` — the only other `[DataType]` built-in the emitter still binds — unchecked. Measured with `Cols = 7`, `Name = "ab"`: `@for((Cols))` and `@for(len(Name))` are templates the engine refuses (`HED0004`, `System.String` against `[Heddle.Models.Range, System.Int32]`); **the parent commit rendered 7 and 2 iterations, and its own parent rendered 0** — both wrong, in different ways, and neither degraded | `669eef8` — the check reads `[DataType]` off the bound extension over the base chain, with the same `inherit` the runtime uses, and applies to every extension the emitter binds |

- **pinned by:** `ListTests.ANonEnumerableValueIsRefusedByBothTiers`, `.EnumerableValuesStillPrecompile`
  (a `List<T>`, an array, a `string`, and a `List<object>` all still precompile);
  `ForTests.AForOverAValueItDoesNotAcceptIsRefusedByBothTiers`, `.AForOverAnAcceptedValueStillPrecompiles`,
  `.AForOverANullableIntStillPrecompiles`; `AcceptedTypeTests.*` (cycle 18).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ListTests`,
  `… --filter FullyQualifiedName~ForTests`, `… --filter FullyQualifiedName~AcceptedTypeTests`.
- **notes:** generalising cost nothing measurable — the whole corpus was byte-identical. **The
  relation it compares with was wrong** until cycle 18 (F-155): reflection's `IsAssignableFrom` has no
  numeric widening, which is why `@for` over a `long` is a template the engine refuses. Class D, and
  **the durable answer that class has: read the authority instead of a projection of it.** The
  `[DataType]` case is now closed *by construction* — a new extension is covered without anybody
  listing it.

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

### F-145 — A type-name gate added to a layout on suspicion, measured, reversed

- **status:** SUPERSEDED (removed) — F-153's half is FIXED and stands
- **absorbs:** F-153, F-158
- **severity:** 6 at the time (F-145); 4 as it stood, because it cost a working precompiled template
  (F-158); 6 (F-153)
- **found:** cycle 16 (added), cycle 17 (measured), cycle 18 (reversed, two reviewers)
- **the story, in one place, because re-adding it is the obvious mistake:**

  | id | status | what happened |
  | --- | --- | --- |
  | F-145 | SUPERSEDED | `ResolveExtensionPropLayout` filled its slot types from `[Prop]` metadata without the `CanWriteTypeName` gate the definition layout applies. **No call path makes an extension layout the *active* layout**, so there was nothing to demonstrate and no test pretended otherwise |
  | F-153 | FIXED | separately, only the `Failed` flag stood between a truncated layout and a caller. `669eef8` — the whole declaration list is checked before a single slot is built, so a failed layout carries no slots at all; the flag and every observable behaviour are unchanged. **Measuring the F-145 *gate* for a red is what produced the reversal**: a host extension declaring `[Prop("box", typeof(InternalModel), Optional = true)]` over an `internal` type — the engine renders `[box=<null>]` and so does the generated tier **once the gate is removed**, identical bytes, because no consumer of an extension prop layout ever spells the slot type |
  | F-158 | SUPERSEDED (removal) | **verification re-done rather than taken on trust:** the extension layout has exactly three consumers — the frozen props prototype, the parameter-name field and the binding row's fingerprint — and none writes a type name. The cast-emitting prop reader is unreachable from an extension layout, because every layout that becomes a body's active props is a *definition* layout. A dynamic setter refuses outright unless the argument type matches the slot exactly or widens numerically, so it too writes a keyword or nothing. **Measurement:** with a host extension declaring `[Prop("badge", typeof(InternalBadge), Optional = true)]` over an `internal` type — gate on, every call shape degrades with `HED7030@Warning` while the engine renders; gate off, all of them precompile, the generated code compiles, and the bytes match the engine, through the default, a literal argument and a dynamic setter alike, and through the resolver gauntlet under `PrecompiledMismatchPolicy.Strict`, where the fingerprint round-trips. `2d3fab6` — removed |

- **pinned by:** `ExtensionPropTypeNameTests.AnExtensionPropTypeThisAssemblyCannotNameStillPrecompiles`,
  `.TheLayoutFingerprintRoundTripsForAnUnnameablePropType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ExtensionPropTypeNameTests`.
- **notes:** **the definition layout's identical gate is untouched and is not the same case** — a
  definition layout really does become a body's active one. **Do not "restore symmetry" here.** The
  precedent this sets is in the weak-pin table: **an unmeasured guard is a cost, not a safety
  margin.** Compare F-053, a dead guard deleted after fingerprinting; the difference is that this one
  was not dead, it was live and wrong.

### F-147 — A native expression reading the element's own member inside an `@list` body degrades

- **status:** KNOWN-OPEN
- **severity:** 4 **and 3** (severity amended in cycle 21)
- **found:** cycle 16, reported rather than hidden
- **symptom:** `@(Name + "!")` over a `List<Product>` — the engine compiles it against the element
  type; the emitter has that type in `DynamicBodyModel` but emits the body's reads on the dynamic tier,
  so the expression degrades.
- **fixed by:** — deferred: typing the writer off `DynamicBodyModel` "is a change of a different shape
  and was left for a later cycle".
- **regression check:** none — it is a degrade, so nothing reddens.
- **notes:** the function form `@upper(Name)` is unaffected. **The plain path `@(Name)` is not, and the
  entry said it was.** Measured in cycle 21: `@list(Products){{[@(Nope)]}}` precompiles and throws
  `RuntimeBinderException` at render where the engine refuses at compile with `HED0001` naming
  `Product` — and the same for a `string` element, and for the ambiguous-element case (closed
  separately as F-148's F-172 row, which refuses the body outright because there the emitter cannot
  name the type at all). **So this entry carries a severity-3 divergence as well as the severity-4
  degrade it records:** a member the element type lacks is a build-time refusal on the engine and a
  render-time throw on the generated tier. Still do not re-report; the fix is the same one deferred
  here, and it is now worth more than the entry says. Class E: this is the divergence reachable
  through `IsUntypedReceiver`.

### F-148 — "Cannot say" exempts every gate — a kind funnel, and an element type that cannot be named

- **status:** FIXED
- **absorbs:** F-172
- **severity:** 1 and 3
- **found:** cycles 17 and 21
- **the predicate:** **every gate in the emitter exempts "cannot say".** So any routine that answers
  "cannot say" where the engine has a definite type turns a build-time refusal into a precompiled
  template. Two such routines were found; F-172 is explicitly "the surviving member of F-148".

  | id | cycle | what answered "cannot say" | symptom, both tiers | fixed by |
  | --- | --- | --- | --- | --- |
  | F-148 | 17 | the check that types a *call*'s value routed a function call through the shared operand **descriptor**, which names the numeric primitives, `bool` and `string` and nothing else | `@list(range(1, 3)){{<@()>}}` — the engine refuses (`HED0004`, `Heddle.Models.Range` against `System.Collections.IEnumerable`); the generated tier precompiled and rendered **empty**, having handed `ListExtension` a `Range` to iterate. `<s(out:: object)>{{[@out(range(1, 3))]}}` — the engine refuses (`HED5014`); the generated tier precompiled and rendered. A host `[ExportFunctions]` function returning a `DateTime` or a class of its own slips both gates the same way — measured over a purpose-built export container, four rows, all four confirmed. **`range` is the only built-in whose return type is none of those three, and it is exactly the built-in a reader reaches for when they want to iterate** | `669eef8` — the chosen overload carries its declared return type (a metadata name in the built-in table, an `ITypeSymbol` for an export) and the call is typed from that. **`dynamic` is the one return type not taken at face value**: there is no such thing in metadata, the engine reads a `MethodInfo` whose return type is `System.Object`, and the emitter maps it there too |
  | F-172 | 21 | `ListElementModel` returned null for a collection reaching `IEnumerable<T>` twice | over a collection implementing `IEnumerable<int>` and `IEnumerable<string>`: `@list(Multi){{@list(this){{y}}}}` — the engine refuses (`HED0004`, `System.Int32` against `System.Collections.IEnumerable`), the generated tier precompiled and rendered empty; `@list(Multi){{[@(Nope)]}}` — the engine refuses (`HED0001`, on `System.Int32`), the generated tier precompiled and threw at render. The `@list` body carried the dynamic flag with **no** model behind it, `CallSiteValueType` answered "cannot say", and `AcceptedTypeSatisfied` exempts that. The engine's reflection walk does pick one of the two (confirmed independently by a `HED5014` message naming `System.Int32`), so **the engine has a definite element type here and compiles the whole body against it** | cycle 21 — ambiguity is now a **third answer**, distinct from "cannot say", and the body is refused. Not being able to reproduce the order the runtime chose in is a reason to leave the body to the dynamic tier, not to emit one against no type at all |

- **degrade cost of F-172 — remeasured in cycle 22, and it is more than the one cell first recorded.**
  The refusal is taken **before the body is inspected**, so it declines every `@list` over such a
  collection, including bodies that could not have needed the element type:

  | body | engine | generated |
  | --- | --- | --- |
  | `@list(Multi){{@for(this){{y}}}}` | renders `[yyy]` | degrade (the cell first recorded) |
  | `@list(Multi){{[x]}}` — **reads nothing at all** | renders `[[x][x]]` | degrade |
  | `@list(Multi){{@list(this){{y}}}}` / `[@(Nope)]` / `@for(Multi)` | refuses | degrade (correct) |
  | `@list(Single){{[@(Length)]}}` (neighbour) | renders `[[1][1]]` | precompiles, matches |

  A third shape was reported by cycle 22's adversary — a slot projection,
  `@list(Multi){{@box(this){{c}}}}` — and the spelling constructed to reproduce it refuses on **both**
  tiers (`HED5014`), so it is not recorded as a cost cell. **Two cells, not one and not three.**
- **why F-172 is recorded rather than narrowed:** narrowing to the bodies that do need the element
  type means building the body first against no type and asking afterwards whether it consulted one —
  which is exactly the state this finding exists to prevent, a body on the dynamic tier with no model
  behind it that every gate downstream exempts. The cost is pinned instead by
  `ListTests.TheAmbiguityRefusalAlsoDeclinesBodiesThatDoNotNeedTheElementType`, so a cycle that
  narrows the rule reddens those rows and has to say what it did. **This is the register's own lesson
  from F-112 ("a blanket refusal charges the cost to working templates") recorded with its price
  rather than acted on blind.**
- **pinned by:** `ExportFunctionTests.AnExportReturningANonPrimitiveIsRefusedByListOnBothTiers`,
  `.AnExportReturningANonPrimitiveIsCheckedAgainstTheSlotType`,
  `.AnExportReturningDynamicIsTypedAsObjectLikeTheEngineTypesIt`,
  `.AnExportWhoseReturnTypeFitsStillPrecompiles`;
  `ListTests.AListOverRangeIsRefusedByBothTiersBecauseARangeIsNotEnumerable`,
  `.AnAmbiguousElementTypeIsRefusedRatherThanTreatedAsUntyped` (two bodies),
  `.AnUnambiguousElementTypeIsUnaffected` (the neighbour — the same nested `@list` body over a
  nameable enumerable element still precompiles and renders the engine's bytes, and over a nameable
  non-enumerable element both tiers refuse for the enumerability rule, which is a different rule),
  `.TheAmbiguityRefusalAlsoDeclinesBodiesThatDoNotNeedTheElementType`;
  `SlotValueTypeTests.ACallReturningANonPrimitiveIsCheckedAgainstTheSlotType`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ExportFunctionTests`
  and `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ListTests`.
  Mutations: without the `dynamic`→`object` mapping the `object` row degrades; removing the ambiguity
  arm reddens 2 of 2, both reporting that the template precompiled.
- **notes:** both F-148 shapes were already true at the parent commit — **the previous commit's
  degrade sweep read as complete and was not.** Class E, and F-172 is the shape of the answer: a
  third answer, "the engine has one and I cannot name it", which is neither a type nor "cannot say".

### F-151 — Sweep categories that could not hold the change the cycle made

- **status:** FIXED (instrument added, twice)
- **absorbs:** F-159
- **severity:** 6
- **found:** cycles 17 and 18 (F-159 by both reviewers)
- **the predicate:** *what kinds of change can a corpus sweep see?* The answer was one category, then
  two, and is now three.

  | id | the category that was missing | why the existing ones could not hold it | fixed by |
  | --- | --- | --- | --- |
  | F-151 | **bytes moved while still precompiling** | the degrade sweep enumerates templates that newly *degrade*. That cannot hold `@for` over a chain, which kept precompiling across the commit and rendered *different bytes* — seven iterations at the parent, none after. Such a template is invisible to a degrade sweep **and to a differential test whenever the engine refuses it**, because then there is no dynamic render to compare against | `669eef8` — two counts; the instrument is the same corpus run captured twice (classification, every generated `.g.cs`, the manifest and the diagnostic list) and diffed |
  | F-159 | **newly precompiling** | neither existing count can hold a template that *starts* precompiling — which is new generated code reaching consumers | `2d3fab6` |

- **regression check:** capture the corpus twice across your change — classification, generated
  source, rendered bytes — plus the ten samples' goldens and their generated files, diff all of them,
  and **report all three counts in the landing.** (Run the sample capture from inside the sample
  directory with `--capture out` after deleting the directory — F-017.)
- **notes:** cycle 18's own numbers: zero newly degrading, zero moved bytes, **one** newly
  precompiling — a model-less template whose definition declares no model and whose body branches
  around an `@out` projection; its intent row moved with it (`branching-out-projection.heddle`,
  `FallsBackSafely` → `Precompiles` in `src/TestCorpus/CorpusIntent.cs`). Class H — and the sweep is
  **not** complete for behaviour the corpus cannot see; see that class.

### F-152 — Two constructs examined rather than assumed

- **status:** NOT-A-DEFECT (both), with one cleanup
- **severity:** —
- **found:** cycle 17
- **measurement — this is what stops them being re-reported:**
  - `bctx.IsDynamic ? null : bctx.ModelSymbol` was a no-op at both sites: **every construction site
    upholds `IsDynamic ⇒ ModelSymbol is null`, verified by reading all eleven of them.** The guard
    read as though the two could disagree, so it is gone and the invariant is stated on the field,
    next to the pointer to `DynamicBodyModel` — the field that answers the *different* question of
    what the engine typed a dynamically-emitted body against.
  - the writer the call-typing pass builds is thrown away undrained, so a refusal it proves is
    discarded — **structurally harmless**, because a call the ranker refuses has no return type
    either, so the value stays "cannot say", no gate can refuse on account of it, and the writer that
    *emits* the same expression is always reached.
- **pinned by:** `AmbiguousOverloadDiagnosticTests.AnIllegalCallInATypedValuePositionIsStillReported`
  (an ambiguous call as an `@list` value and as an `@out` slot value each still report `HED7025` at
  Error).
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AmbiguousOverloadDiagnosticTests`.

### F-154 — Three caller-content shapes degrade under a `:: dynamic` callee

- **status:** KNOWN-OPEN
- **severity:** 4
- **found:** cycle 17, reported rather than hidden
- **symptom:** a native expression, a function call and an `@if` in caller content under a
  `:: dynamic` callee degrade where the engine renders. Unchanged by that cycle.
- **fixed by:** — deferred.
- **regression check:** none — a degrade reddens nothing.
- **notes:** do not re-report. If you close it, the sweep must report all three counts (F-151).

### F-155 — Reflection's assignability claimed, not reproduced — and the adapter's corrections, wrong twice

- **status:** FIXED
- **absorbs:** F-162, F-163
- **severity:** 4 (F-155, F-163), 1 and 3 (F-162)
- **found:** cycle 18 (both reviewers, in the previous commit), cycle 19
- **the predicate:** `Type.IsAssignableFrom` — the relation the engine actually uses. It was first
  written out as four cases, then asked of an adapter whose own CLR corrections were wrong in both
  directions in two consecutive cycles.

  | id | cycle | symptom, both tiers | root cause | fixed by |
  | --- | --- | --- | --- | --- |
  | F-155 | 18 | six shapes the engine renders and the parent precompiled started degrading: `int` and `int?` into `[DataType(typeof(int?))]`, `List<string>`, `IList<string>` and `string[]` into `IEnumerable<object>`, and `string[]` into `object[]`. **The nullable rows are the sharpest: the gate unwrapped the *value's* nullable and compared against the un-unwrapped declared one, so `[DataType(typeof(int?))]` accepted nothing whatever — not even an `int?`. That declaration was entirely non-functional** | the gate compared with `SymbolEqualityComparer` over the base chain and the interface set — nominal identity. `Type.IsAssignableFrom` has generic variance, array covariance and the CLR's `Nullable<T>` treatment in it. **The doc comment enumerating "identity, a base class, an implemented interface, or the boxing to `object`" was itself the defect** | `2d3fab6` — the gate asks the generator's existing symbol adapter for the CLR relation, which has verified Roslyn-vs-CLR corrections and a shared conformance corpus driven from both tiers |
  | F-162 | 19 | an extension re-declaring an inherited `Enum` prop as `DayOfWeek?` is `HED5008` on the engine and refuses the template; the generated tier accepted the layout and rendered | the adapter's Roslyn-boxing correction was written as "boxing, except a nullable into an interface" — `System.Enum` is a class, so `DayOfWeek? → Enum` survived it, and **the CLR says false because `Nullable<T>`'s base chain is `ValueType` and `object` and stops** | `f990a9c` — phrased as the CLR's own question: a boxing conversion out of a `Nullable<T>` is assignable only where the target is on `Nullable<T>`'s own hierarchy, which subsumes the interface case rather than sitting beside it |
  | F-163 | 19 | the CLR compares array element types after reducing an enum to its underlying primitive and each signed/unsigned integer pair to one representative, so `uint[] → int[]`, `byte[] ↔ sbyte[]`, `long[] ↔ ulong[]` and `DayOfWeek[] → int[]` are assignable, and it propagates through the array's own generic interfaces (`uint[] → IList<int>`) and through jagged arrays. **Roslyn classifies none of these as a conversion at all**, so the adapter refused every one: twenty rows in one sweep, thirteen in another. The engine renders them; the generated tier degraded | the adapter's own correction set had no array clause | `f990a9c` — one clause: reduce both sides' element types and re-ask (the re-ask answers the propagated forms without naming them, and terminates because the reduction is idempotent) |

- **pinned by:** `AcceptedTypeTests.AValueTheEngineAcceptsPrecompilesAndRendersTheEnginesBytes`,
  `.AValueTheEngineRefusesDegradesRatherThanPrecompiling`,
  `.AnInheritedAcceptedTypeIsAcceptedAlongsideTheDeclaredOne`,
  `.ANullableRedeclarationOnTheNullableBaseChainStillAcceptsAndRendersIdentically` (the neighbour:
  `DayOfWeek?` against a `ValueType` base accepts and renders the engine's bytes); four
  `[DataType]`-declaring fixtures in `Fixtures/AcceptedTypeExtensions.cs`; rows in
  `src/Heddle/Language/Binding/AssignabilityCorpus.cs`, driven from both tiers, including the array
  rows in both directions.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AcceptedTypeTests`,
  `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~AssignabilityCorpusSymbolTests`,
  `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~AssignabilityCorpusReflectionTests`.
  Mutations that must redden disjoint halves: an acceptance that answers yes to everything reddens
  every refusal assertion; one back to nominal identity reddens every non-exact acceptance row; one
  that accepts any two arrays of equal rank reddens `int[] → object[]`, `int[] → ValueType[]`,
  `DayOfWeek[] → Enum[]`, `char[] → ushort[]` and `bool[] → byte[]`.
- **notes:** **why no fixture caught F-155:** no built-in is affected, and neither the corpus nor the
  samples contained a single host extension declaring `[DataType]`. "The corpus is byte-identical" was
  true and closed nothing. Cycle 19 corrected two of F-155's claims: "variance, array covariance and
  the nullable domain come with it by construction" was false for array covariance (F-163), and the
  row/mutation counts ("thirteen rows … five … six") do not reproduce. F-162 is described as the
  adapter's **only over-acceptance** across two independent sweeps; F-163 is **not a regression** —
  the nominal comparison the adapter replaced refused the same rows — but it makes cycle 18's "by
  construction rather than by list" claim false as written. `AssignabilityCorpus` had four array rows,
  none in the disagreeing family, and no `Nullable<enum>` row; it has both now. The reflection-side
  driver re-derives every expectation from the live CLR relation on each run, so the committed values
  are generated data. Class I.

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
  `@frame(len(Name))` off the precompiled tier. **Resist tightening this.**

### F-165 — Two unreachable arms, one kept and labelled and one deleted

- **status:** NOT-A-DEFECT (documented decisions)
- **absorbs:** F-175
- **severity:** —
- **found:** cycles 19 and 21
- **the measurement, and the rule it settles — this is what stops both being re-reported:** an
  unreachable arm is reported every few cycles. The answer is not uniform, and the deciding question
  is *is this arm symmetric with a live one?*

  | id | the arm | why it is unreachable | decision, and the reason |
  | --- | --- | --- | --- |
  | F-165 | the **container** arm of the export guard (F-104's F-157 row), which checks both names an emitted call spells — the method and its container | naming an obsolete-error type in `[ExportFunctions]` is `CS0619` in the assembly that declares the export, and no pragma there suppresses it | **kept**, with a clause saying so, so the two names are guarded alike. **An arm that looks live and is not is a worse trap than one that is labelled** |
  | F-175 | `IntegralText`'s `char` case | its two callers are the enum branch — and C# admits no enum over `char` — and the narrow-integral literal writer, whose own switch has no `char` arm | **deleted**, and the method's contract written down instead. It is symmetric with nothing — it is a case in a list of cases, and a reader counting the list would conclude the callers accept `char` |

- **notes:** contrast F-053, where a dead guard was deleted after a fingerprint sweep proved it
  decided nothing. The three decisions together give the rule: **fingerprint it; then keep it if it
  pairs with a live arm and delete it if it reads as a member of a list.**

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
- **notes:** the same shape as the shipped `min(1, 2u)` counter-example. **Do not "fix" it inside a
  review cycle; it widens accepted behaviour.**

### F-168 — A shipped sample still uses removed MSBuild item metadata

- **status:** KNOWN-OPEN
- **severity:** off-scale (sample correctness)
- **found:** during the documentation survey that preceded cycle 1 (id out of chronological order).
- **symptom:** `samples/codegen-t4-successor/CodegenT4Successor.csproj` carries
  `<HeddleTemplate Include="templates\report.heddle" Name="BuildReport" />`; `Heddle.Generator.props`
  no longer reads `Name`, so the sample silently loses its intended key.
- **fixed by:** — escalated, not fixed. It is a live, golden-checked user-facing artefact.
- **regression check:** grep `samples/**/*.csproj` for `Name=` on `HeddleTemplate` items.

### F-169 — The callee's body was built before the caller's content; the engine compiles them the other way round

- **status:** FIXED
- **severity:** 3
- **found:** cycle 21 — a regression introduced by the previous commit
- **symptom:** `<frame>{{[@(Title)]}} :: dynamic` and, inside one component body,
  `<outer>{{@frame(this){{@frame(5)}}}}`. The engine refuses at compile — `HED0001: Property Title not
  found in Type [Int32]`. The commit under review **precompiled** it, and threw
  `InvalidCastException: Unable to cast 'System.Int32' to '…RegionArticle'` at render. The parent
  commit degraded it. **A build-time refusal became a shipped template that throws at render.**
- **root cause:** the emitter built a callee's body at `TemplateEmitter.cs:1235` and the content the
  caller wrote inside the call at `:1280`; `HeddleCompiler.CreateExtension` compiles the caller content
  first (`:1136`) and the body second (`:1147`). The inversion was inert until the same commit made
  first arrival decide the typing of a body two call sites share — after which the order settled which
  of the two typed it, and the emitter picked the wrong one.
- **class expansion:** seven order-sensitive constructs were built and measured. **Exactly one site is
  inverted** — the caller content of the *same* call. The mirror, an unrelated call, `@if`/`@else`,
  `@list`-then-direct and chains in both directions all matched before the fix. The three spellings
  that resolve to `System.Object` (`dynamic`, `object`, none at all) all reproduce it.
- **fixed by:** cycle 21 — the caller-content block moved above the body build, and the slot-type
  resolution hoisted above both arms so it still runs unconditionally.
- **pinned by:** `DynamicDefinitionBodyTests.CallerContentTypesASharedBodyBeforeTheCallsOwnBodyDoes`
  (four spellings) with `.TwoCallSitesOfOneModelStillPrecompileWhateverTheOrder` as the neighbour that
  must keep precompiling. Restoring the old order reddens 4 of 4, each reporting that the template
  precompiled.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~DynamicDefinitionBodyTests`.
- **notes:** class F — one of two defects that commit introduced alongside a fix that was measured
  sound; the other is F-171's F-174 row.

### F-171 — The emitter's body identity had a term the engine's does not, and two that decided nothing

- **status:** FIXED
- **absorbs:** F-174
- **severity:** 1 (F-171), 6 (F-174)
- **found:** cycle 21 — **both introduced by the commit under review**
- **the predicate:** *what is the emitter's body identity, term by term, and does the engine key on
  the same terms?* The engine memoizes each compiled item by the parsed `OutputItem` it came from and
  saves and restores the region fill scope around the body compile **without putting it in that
  memo** — so a second call site filling a region differently still runs the first site's body, fills
  included.

  | id | term | symptom / measurement | fixed by |
  | --- | --- | --- | --- |
  | F-171 | the fill-scope digest — **a term the engine does not have** | inside one definition body, `@panel(){{@%<head:head>{{[A]}}%@}}\|@panel(){{@%<head:head>{{[B]}}%@}}` — the engine renders `[A]\|[A]`, the generated tier `[A]\|[B]`. Both render; no diagnostic. **Class expansion: 17 enclosing-body shapes; 11 diverged** — every shape that is not a parser isolation boundary (definition body, filled-then-unfilled, unfilled-then-filled, three calls, `@if`/`@list`/`@for` bodies inside a definition body, a region body, a slot-definition body, the caller content of a third call, two regions filled differently). Document scope, `@if` at document scope and a document with no definitions block matched, because the parser hands each call site its own copy of the definition tree there | cycle 21 — the fill scope came **out of both keys**, and the two keys were folded into one string computed once, because they were two spellings of one question. The remaining terms are the definition and the parse context it was reached through, which is the engine's own identity. The cache key's parse-context term moved from the context's absolute offset to its **reference identity**, the term the sharing key already used; without that the document-scope neighbour collapses too |
  | F-174 | two terms that decided nothing | dropping the fill-scope digest from the sharing key reddened 0 of 847; dropping the `DynamicBodyModel` equality term from the sharing test reddened 0 of 847 and 0 of 555. The other two terms are load-bearing: collapsing the parse-context identity to an absolute offset reddens 2, and deleting the degrade arm reddens 1 | cycle 21 — **both terms were removed rather than pinned.** The fill-scope digest went with F-171, where it is the defect. The `DynamicBodyModel` term is **redundant by construction and not merely untested**: every context reaching the sharing rule got its model from `DefinitionBodyContext` or `TryTypeCallSiteBody`, which arm is taken is a property of the definition rather than of the call site, and both arms leave the model symbol and the dynamic body model in step — so agreement on the tier and the model symbol already implies agreement on the third. Re-adding it changes nothing over 879 tests, which is what the argument predicts |

- **pinned by:** `RegionTests.TwoFillsOfOneRegionInOneBodyRunTheFirstFill` (seven enclosing bodies)
  and `.TwoFillsAtDocumentScopeEachRunTheirOwn` as the neighbour that must keep both fills. Putting
  the digest back reddens 7 of 7 and leaves the neighbour green; keying by absolute offset instead of
  parse-context identity reddens the neighbour and `.TwoCallsAreIndependentlyScoped`. Those fixtures
  are also F-174's pin — they pin the identity the two keys now share.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~RegionTests`.
- **notes:** **a one-line fix does not work.** Dropping the digest from the sharing key alone leaves
  the equality check succeeding without transplanting the first context, so the cache key still
  splits. The rule F-174 states, and the register's answer to every "this term is untested" report:
  **say which it is — untested, or unable to decide anything — and delete it if it is the second. A
  term that cannot decide is a claim the reader will believe.** Classes A, B and G.

### F-173 — A region that declares its own slot never entered slot mode

- **status:** FIXED
- **severity:** 3 and 4
- **found:** cycle 21
- **symptom:** `@%<comp>{{@%<r(out:: …Host)>{{[@out()]}}%@@r(){{<@(Tag)>}}}} :: …Host%@`
  - as written — the engine refuses (`HED5013`: a definition with a slot parameter requires a value on
    its `@out`); the generated tier precompiled and rendered `[]`.
  - with `[@out(this)]` — the engine renders `[<host>]`; the generated tier degraded.
- **root cause:** `TryRegionBodyContext` never set the slot type, so a region body was never in slot
  mode and both of `BuildOutCall`'s gates took the wrong branch. The engine's
  `compileContext.SlotParameterType = slotType` is **not** region-special-cased — in contrast to
  `ActivePropLayout = definition.IsRegion ? savedLayout : layout` on the line immediately above it,
  which is.
- **fixed by:** cycle 21 — the region arm applies the definition's own slot type, as the non-region arm
  does. The *enclosing* slot mode was already correctly cleared on both tiers; this is the region's own
  declaration.
- **pinned by:** `RegionTests.AValuelessOutInsideARegionsOwnSlotIsRefusedByBothTiers` and
  `.AValuedOutInsideARegionsOwnSlotProjectsTheCallerContent`. Removing the slot application reddens
  both — one because the template precompiled, one because it degraded.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~RegionTests`.
- **notes:** class B — this is the `SlotType` column of the cycle-21 grid, wrong at both region sites.

### F-176 — A host extension type the consumer's assembly may not name, written into `.g.cs`

- **status:** FIXED
- **severity:** 2
- **found:** cycle 22
- **symptom:** against a referenced assembly carrying `[assembly: ExportExtensions]` (the parameterless
  *All* form, which needs no cooperation from the extension author):

  | template | engine | generated (before) |
  | --- | --- | --- |
  | `@secret(Name)` — `internal sealed class SecretExtension` | renders `<ab>` | `error CS0122` ×2 |
  | `@boxed(Name, width: 7)` — `internal` + `[Prop]` | renders `{7:ab}` | `error CS0122` ×1 |
  | `@legacy(Name)` — `[Obsolete("gone", true)] public sealed class LegacyExtension` | renders `[ab]` | `error CS0619` ×2 |

  No diagnostic and no degrade: the manifest said precompiled and the consumer's build stopped on a
  generated file they cannot edit.
- **root cause:** `ExtensionBinder` asks nothing about accessibility or `[Obsolete]` — the whole file has
  no `Accessibility`, `IsObsolete` or `Public` outside the `internal` keyword — and the emitter spells
  `Info.GlobalName` into the field's declared type and into the `new` that fills it. The engine's
  discovery is `assembly.GetTypes()` filtered only by `IsImplement<IExtension>() &&
  IsHaveAttribute<ExtensionNameAttribute>` (`src/Heddle/Runtime/TemplateFactory.cs:245-262`), and
  `Activator.CreateInstance` instantiates a non-public type with a public constructor and ignores
  `[Obsolete]` outright.
- **class expansion:** `grep -rn "GlobalName" src/Heddle.Generator --include=*.cs` gives exactly four
  emission sites — the branch-role body extension, the two writes of the plain custom writer, and the
  parameterized writer — plus one diagnostic-text use. All three unguarded ones were demonstrated. The
  fourth was reachable only through `Info.IsEngineAssembly`, which is `string.Equals(assemblyName,
  "Heddle")`, so a host assembly whose simple name is literally `Heddle` would route a role extension
  to it; the fix is placed before that branch, so it is covered without needing the measurement.
- **fixed by:** cycle 22 — the question is asked once at the choke point in `BuildCall`, before any of
  the three writers allocates a field, through `ClassifyTypeName` and the existing `HED7030`
  author-facing warning. `ClassifyTypeName` and not `ClassifyModelType`: no extension is boxed into a
  model, and no extension could be a ref struct.
- **pinned by:** `UnnameableExtensionTypeTests` — three degrade rows and four neighbours that must keep
  pre-compiling and rendering the engine's bytes: a public extension in the *same probe assembly*, an
  ordinary public host extension, a warning-level `[Obsolete]` one, and one whose declared `[Prop]`
  **type** is unnameable (a gate a prior cycle deliberately removed — F-145 — and this row says so).
  Removing the check reddens 3 of 3 with `CS0122` ×2, `CS0122` ×1 and `CS0619` ×2 — the exact messages
  the finding reports.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UnnameableExtensionTypeTests`.
- **notes:** **the position defect class C predicted.** The error-obsolete row needs a probe assembly
  built at test time, because an error-obsolete type cannot appear in a `typeof` export list in C# at
  all — which is the property under test.

### F-177 — `@using` text copied into `.g.cs` as a C# `using` directive

- **status:** SUPERSEDED by F-180. The symptom below is real and stays fixed, but the fix was correct about
  the emission and wrong about the premise in two ways: the filter it installed could not read
  `global::System.Linq` or `System . Linq`, and its stated reason — "a `@using` body is free text the engine
  never consults" — holds only for a document with no embedded C#. Both are measured in F-180.
- **severity:** 2
- **found:** cycle 22
- **symptom:**

  | template | engine | generated (before) |
  | --- | --- | --- |
  | `@using(){{Zork.Nope}}@\` + `hello` | renders `hello` | `error CS0246` |
  | `@using(){{1 + 2}}@\` + `hello` | renders `hello` | `CS1001`+`CS1002`+`CS8805`+`CS0201` — **the `.g.cs` no longer parses** |
  | `@using(){{System.Linq}}@\` + `hello` | renders `hello` | precompiles, clean |

  The second row is worse than one bad name: an unparseable compilation unit takes every other template
  in the same compilation down with it.
- **root cause:** the collected `@using` bodies were written out verbatim as `using <text>;`, with no
  validation anywhere between the parse and the emission. To the engine a `@using` body is advice about
  resolving a model type name — `UsingExtension.InitStart` only calls `CSharpContext.ImportNamespace` —
  so a body naming nothing is never consulted and the template renders.
- **class expansion:** exactly two members — this, and the verbatim embedded-C# expression. The second
  is a documented opt-in (`FullCSharp` says "this text is C#"); `@using` is not.
- **fixed by:** cycle 22 — the directive is omitted when the text names no namespace this compilation
  can see (`SymbolTypeResolver.NamespaceExists`, walked from the merged global namespace). The
  **collected list is untouched**: it is what `SymbolTypeIndex` resolves a model type name through.
  Omitting costs nothing — generated code is fully qualified everywhere except embedded C#, and code
  that needed a namespace the compilation does not contain could not have compiled against it either.
- **pinned by:** `UsingDirectiveTests` — the three rows above plus the pair that says the collected list
  still decides: a `@model(){{Fixtures.Cart}}` that resolves only because a `@using` qualifies it, and
  the same spelling without the `@using`, which degrades. Removing the guard reddens 2 of 894 with the
  CS0246 and the CS1001/CS1002/CS8805/CS0201 above.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UsingDirectiveTests`.
- **notes:** filtering the *list* instead of the emission was measured equivalent — a namespace that does
  not exist can match nothing in either resolver arm — and reddens nothing. The emission site was chosen
  as the narrower change, not because the two differ. **Class C is not limited to types:** a position
  list built by asking "which *types* does the emitter spell" would have missed this.

### F-178 — The generator's import identity is a template key; the engine's is a canonical disk path

- **status:** FIXED in one direction; the other recorded as known-open
- **severity:** 2 (the direction fixed) and 3 (the direction left open)
- **found:** cycle 22
- **symptom:** one `lib.heddle` on disk and as an `AdditionalFile`, target `@<<{{SPELLING}}` + `@greet()`,
  13 spellings:

  | spelling | engine | generator (before) |
  | --- | --- | --- |
  | `lib.heddle`, `./lib.heddle`, `.//lib.heddle` | renders `[[hello]]` | precompiled, matches |
  | `x\lib.heddle`, `LIB.heddle`, `Lib.heddle`, `./../outside/lib.heddle` | refuses `HED4009`+`HED1001` | `HED7011` + degrade (both refuse) |
  | `x/../lib.heddle`, `sub/../lib.heddle`, `sub/./../lib.heddle` | **renders** | **`error HED7011` — breaks the build** |
  | `/lib.heddle`, `~/lib.heddle`, `lib` | **refuses** `HED4009`+`HED1001` | **precompiled, renders** |

- **root cause:** the generator installs `TemplateKey.TryNormalize` as both `ImportIdentifier` and the
  import-map lookup. `ParserSettings.ImportIdentity` otherwise falls back to
  `Path.GetFullPath(Path.Combine(RootPath, importPath))`, and the disk reader takes the path verbatim.
  `TryNormalize` strips `~/`, `./` and a leading `/`, appends `.heddle` to an extension-less final
  segment, and **rejects** `..` — falling back to the raw spelling, so the map misses. `GetFullPath`
  resolves `..`, keeps a leading `/` as absolute, and appends nothing.
- **fixed by:** cycle 22 — `..` is applied before the key is derived
  (`HeddleTemplateGenerator.ApplyParentSegments`), the way `GetFullPath` applies it, with `.` and
  repeated separators dropped on the way. A `..` with nothing left to cancel against is **kept**, so a
  spelling reaching above the root still fails key derivation and still refuses, as the engine does.
  `TemplateKey.TryNormalize` is untouched: a template key genuinely may not contain a `..`, and widening
  it would reach the resolver and the registry. `HED7011`'s message was wrong for every one of these
  rows ("Add it as a `<HeddleTemplate>` item" — the file *is* an item) and now also says what the
  spelling is matched against.
- **class expansion:** `grep -rn "ImportIdentifier"` gives 5 hits in the generator (7 across `src/**/*.cs`):
  the property, two reads in
  `ParserSettings`, the one generator assignment, and a doc line. The **LanguageServices facade sets
  neither `ImportReader` nor `ImportIdentifier`** — it parses through `DocumentParser.Runtime`, whose
  `ImportReader` is `null` — so the LSP already had the engine's semantics exactly and needed no
  treatment. That is the third spelling of the question, and it is closed by grep.
- **pinned by:** `ImportSpellingTests` — four spellings the engine resolves to the same file (including
  the three that only resolve once `..` is applied), each asserted to render the engine's bytes through
  both tiers, and three that name nothing on either tier (`./../outside/…`, a case difference, and a
  `..` that escapes). Dropping `ApplyParentSegments` reddens 3 of 893 with the `HED7011` build error;
  letting an uncancellable `..` be swallowed reddens the escaping row.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ImportSpellingTests`.
- **notes:** the remaining direction is in the known-open register: the generator resolves `/lib.heddle`,
  `~/lib.heddle` and `lib` where the engine refuses all three. Narrowing it would take working
  precompiled templates off the tier over spellings the documentation teaches — `@partial(){{child}}`
  already spells a template without its extension, and `~/` is a documented host idiom in
  `TemplateKey`'s own contract — so it is recorded rather than closed. `LIB.heddle` under a
  case-insensitive filesystem is a fourth divergent row this box cannot produce; not guessed at.
  Class A, and the fourth time import identity has been the finding — see F-025.
- **amended, cycle 23:** the fix above was **incomplete in the same function**, and F-181 is what it left. A
  `.` was dropped only as a side effect of the `..` walk, so a spelling carrying only `.` never reached it and
  broke the build over four templates the engine renders; and the normalisation introduced an unconditional
  `\`→`/` replacement, making a backslash a separator on hosts where the engine's `Path.GetFullPath` does not.
  The open half below is unchanged and stays open — cycle 23 widened its measured population from 3 spellings
  to 7.

### F-179 — An import-only library compiled standalone reports an error it would never raise in place

- **status:** KNOWN-OPEN (a trap, not a divergence)
- **severity:** 3
- **found:** cycle 22
- **detail:** a `.heddle` file meant only to be imported is also a `<HeddleTemplate>` item, so the
  generator parses and compiles it **on its own**. A fragment that is only well-formed inside an
  importer then fails that standalone pass, and the front end's error is forwarded as `HED7012`, an
  error against the consumer's build.
- **why it is not drift:** both tiers refuse that file when it is compiled standalone — the engine's
  own compiler would raise the same error given the same input, so there is nothing between the tiers
  to diverge. The trap is that **the engine never compiles that file standalone in production**: in a
  real host it only ever reaches the compiler already expanded into the document that imports it, so
  the error is one no run of the application can produce.
- **opt-out, and it is documented:** `Precompile="false"` on the `<HeddleTemplate>` item. The item stays
  in the import map — importers still resolve it — and the standalone pass runs in advisory mode only.
  `HED7011`'s own message names it.
- **recorded because:** a future cycle will find this and report it as a tier divergence. It is not one,
  and the answer is a build-file change rather than an emitter change.
- **not re-measured by the fixer of cycle 22:** the shape is recorded as the reviewer reported it,
  together with the opt-out verified in the generator (`ParseAndReport`'s `advisoryOnly` path) and in
  `HED7011`'s message text. The measurement to add, if it is ever escalated, is which fragment shapes
  actually fail the standalone pass.

### F-180 — The `@using` namespace filter rejected namespaces that do exist, and treated a `@using` as inert

- **status:** FIXED
- **severity:** 2 (the direction that broke the build) and 3 (the direction that rendered)
- **found:** cycle 23 (both introduced by 540c096, the commit that fixed F-177)
- **supersedes:** F-177
- **symptom:** under `ExpressionMode.FullCSharp`, over `@list(@model.Products.Where(…))`:

  | template | engine | generated (540c096) |
  | --- | --- | --- |
  | `@using(){{System.Linq}}` | renders `<i>Pricey</i>` | matches |
  | `@using(){{global::System.Linq}}` | renders `<i>Pricey</i>` | `error CS1061` ×2 |
  | `@using(){{System . Linq}}` | renders `<i>Pricey</i>` | `error CS1061` ×2 |
  | `@using(){{Zork.Nope}}` + `@(@model.Products.Count)` | **refuses** | **renders `Count: 2`** |
  | `@using(){{1 + 2}}` + the same | **refuses** | **renders `Count: 2`** |

- **root cause:** two halves of one wrong premise. `SymbolTypeResolver.NamespaceExists` split the body on `.`
  and matched segments ordinally, which no C# name spelling survives — `global::` and interior whitespace are
  both ordinary. And the premise itself: F-177 recorded a `@using` body as advice the engine never consults,
  which is true only of a document with no embedded C#. `CSharpContext.ImportNamespace` collects every body and
  `CSharpPreparseTemplate.tcs` / `CSharpClassTemplate.tcs` write each one out as `using @();` into the code
  `ContextCompilation.Compile` compiles — but only when `ExpressionMode == FullCSharp && Methods.Count > 0`. So
  a body naming nothing makes the ENGINE refuse the template as soon as one embedded expression exists.
- **class expansion:** the class is what the generator does with the collected list, and it has exactly two
  members (`grep -rn "_usings" src/Heddle.Generator`, 14 hits — **this grep was written without `-r`, so as
  printed it produced no output at all**): the emission in `RenderFile`, and the list handed
  to `ResolveModelType` at five sites. The resolution list was correct and is untouched.
- **fixed by:** cycle 23 (`ec613c2`) — `NamespaceExists` parses (`SyntaxFactory.ParseName`, rejecting a parse
  diagnostic or a trailing remainder, flattening identifier / qualified / `global::` alias-qualified nodes) and
  walks the merged global namespace; and `TemplateEmitter.BuildCSharpExpr` refuses the expression, hence the
  template, when any collected body names no namespace, so the engine's own refusal is what the reader gets.
- **pinned by:** `UsingDirectiveTests` — three spellings that must reach the pasted expression, two unusable
  bodies with an embedded expression that must degrade (each asserting the engine refuses too), and three
  neighbours with no embedded expression that must keep pre-compiling. Restoring the split reddens 2; dropping
  the expression-level guard reddens 2; dropping the emission filter reddens 3.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UsingDirectiveTests`.
- **notes:** the emission stays filtered rather than gated on "did this file paste any C#". Both shapes are
  behaviourally identical on all six measured rows; the gate additionally removed the directive from the
  `codegen-t4-successor` sample's generated-source golden, which is a shipped artifact and not worth moving.

### F-181 — A backslash separated path segments on every platform, and a `.` was dropped only as a side effect of `..`

- **status:** FIXED
- **severity:** 3 (the backslash direction, introduced by 540c096) and 2 (the `.` direction, pre-existing)
- **found:** cycle 23
- **symptom:** one `lib.heddle` at the root and one at `sub/`, 21 spellings, Linux:

  | spelling | engine | generator (540c096) |
  | --- | --- | --- |
  | `x\..\lib.heddle`, `x\../lib.heddle`, `x/..\lib.heddle` | `HED4009` | **renders `[[hello]]`** |
  | `sub/./lib.heddle`, `sub/././lib.heddle`, `./sub/./lib.heddle`, `sub/lib.heddle/.` | renders `[[sub]]` | **`error HED7011`** |
  | `./lib.heddle/.`, `lib.heddle/.` | renders `[[hello]]` | **`error HED7011`** |

  The other 12 spellings matched before and still do. 21/21 agree after.
- **root cause:** `HeddleTemplateGenerator.ApplyParentSegments` early-returned unless the spelling contained
  `..` (its own doc comment claimed to handle `.`), and then normalised with `Replace('\\','/')` — a platform
  question restated as string surgery. `Path.GetFullPath`, which is what the engine resolves an import through,
  treats `\` as a separator only on Windows.
- **fixed by:** cycle 23 (`ec613c2`) — `CanonicalizeImportPath` splits on `{Path.DirectorySeparatorChar,
  Path.AltDirectorySeparatorChar}`, drops `.` and interior empty segments, cancels `..`, keeps a trailing empty
  segment (so `lib.heddle/` still names a directory neither tier reads while `lib.heddle/.` names the file), and
  joins with `/`. No early-out.
- **pinned by:** `ImportSpellingTests` — eight spellings that must resolve to the root library, four with
  interior dots that must resolve to the `sub/` one (so a rule that merely dropped what it did not understand
  cannot pass by landing on the right file for the wrong reason), five that must refuse on both tiers, and four
  backslash rows asserting only that the two tiers reach the SAME verdict, because the right verdict differs by
  platform. Adding `'\\'` to the separator set reddens 3; restoring the early-out reddens 6; dropping the
  trailing-empty rule reddens 1.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ImportSpellingTests`.
- **notes:** the Windows half of the backslash rows is unexecuted here and belongs on
  `unverified-platform-surface.md` beside the case-insensitive-filesystem row F-178 records. The pin is written
  as an agreement assertion so it is meaningful on either host rather than encoding this box's answer.
- **amended, cycle 24:** **the fix was half-applied along its own call chain.** `HeddleTemplateGenerator.cs:397`
  composes the two layers in a single expression —
  `TemplateKey.TryNormalize(CanonicalizeImportPath(importPath), out var key) ? key : importPath` — and
  `TemplateKey.TryNormalizeCore:112` does `relativePath.Replace('\\', '/')` unconditionally on every platform.
  So on Linux the outer call keeps `a\b.heddle` as one segment and the inner call splits it into two.
  Measured: `sub\lib.heddle` and `.\lib.heddle` bound a DIFFERENT FILE than the engine reads — engine
  `HED4009`+`HED1001`, generator no diagnostics and manifest `Precompiled`. Fixed in cycle 24 (`08872ff`) at
  the generator's call rather than inside `TemplateKey`: the `\`→`/` unification is deliberate for KEYS (a host
  may spell a registry lookup `Views\Home`) and `TemplateKey` is public API that also drives the runtime
  registry, whereas an import spelling is resolved against disk. **The population, not the assertion, was the
  defect**: this entry's four backslash rows were measured entirely against corpus misses — three carry a `..`
  that `TryNormalize` rejects outright, and the fourth collapsed to a key absent from the test's corpus, so no
  row existed where a backslash-collapsed key actually resolved. Unchecked residual: `TryMakeRelative:83-88`
  does the same replace on real discovered file paths and matches the root prefix `OrdinalIgnoreCase`, so on a
  case-sensitive filesystem a template under `/proj/Views` with root `/proj/views` gets a key whose `ToPath`
  does not exist; it needs a real MSBuild layout to construct.

### F-182 — A public `[ExportFunctions]` container nested in an internal one failed the whole build

- **status:** FIXED
- **severity:** 2
- **found:** cycle 23
- **symptom:** a probe assembly declaring `internal static class InternalOuter { public static class
  NestedFunctions { public static string NestedEcho(string) } }` and an ordinary `public static class
  PublicFunctions`. `FunctionRegistry.RegisterFrom` over it does **not** throw and registers both;
  `@(nestedecho("x"))` renders `&lt;x&gt;`. The generator reported `error HED7021` at `Location.None`, which
  fails the entire compilation — so `@(plainecho("x"))`, an unrelated public container in the same assembly,
  produced nothing either.
- **root cause:** `FunctionExportResolver.IsPubliclyVisible` walked the containing-type chain demanding `Public`
  at every level. `FunctionRegistry.cs:126` is `container.IsPublic || container.IsNestedPublic` — one question
  about the type's own declared accessibility — and the shared table `ExportRules.IsContainerEligible` states it
  verbatim, its doc comment adding "which the symbol side must accept too".
- **class expansion:** every place the generator computes an accessibility fact a shared table already states,
  from `grep -rn "DeclaredAccessibility\|IsPublic\|IsAccessible" src/Heddle.Generator --include=*.cs` (the set
  is closed by that grep — no other file mentions accessibility): the container check (wrong, fixed); the
  exported *method* check (correct); `SymbolTypeResolver.ToMemberAccess` (correct); `IsAccessibleFromCompilation`,
  which answers a C#-only question no shared table states (correct by construction); and `ExtensionBinder`, which
  filters on the interface and the name attribute alone, matching `TemplateFactory.cs:245-262`.
- **fixed by:** cycle 23 (`ec613c2`) — `container.DeclaredAccessibility == Accessibility.Public`, with the
  chained fix that the question of whether generated code may SPELL the container moved to where the call is
  written (`NativeExpressionWriter.CanWriteCallTo`), beside the two `[Obsolete(error:true)]` arms. From a
  referenced assembly the name is CS0122 and the call degrades — one template, silently — instead of the build.
  Not through `ClassifyTypeName`: a static class is refused there as unable to hold a value, and a call receiver
  is not a value position. That file's comment saying the container arm has no reachable path is now wrong and
  says so.
- **pinned by:** `NestedExportContainerTests` — the runtime measurement that `RegisterFrom` accepts it, the
  degrade with no build error, and the same-assembly public container that must precompile and render. Restoring
  the chain walk reddens 2 of 3; dropping the accessibility arm reddens 1 with the generated code failing on
  CS0122.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~NestedExportContainerTests`.
- **notes:** `HED7021`'s message ("Heddle's runtime registry throws when the host assembly is registered") was
  false as delivered — the measurement above is what falsifies it — and is true again now that the diagnostic
  fires only where `RegisterContainer` genuinely throws. Rewording it would have hidden the predicate mismatch
  instead of removing it.

### F-183 — A sole exported overload was bound on arity alone

- **status:** FIXED (a residual is open as F-186)
- **severity:** 2
- **found:** cycle 23
- **symptom:** `@(titlecase(5))` against the sole `TitleCase(string)` — engine `HED1012 No overload of function
  'titlecase' takes (int)`; generated `.g.cs` `error CS1503` ×2. Same for `twice(5)`.
- **root cause:** `ExportFunctionBinder.TryBind` short-circuited on `overloads.Count == 1 && arity matches` and
  performed no applicability check — the comment conflated SELECTION with APPLICABILITY. The engine's
  `NativeExpressionCompiler.BindOverload` runs `OverloadRank.Bind` unconditionally and has no such shortcut.
- **class expansion:** the generator's overload binders, closed by `grep -rn "OverloadRank.Bind"
  src/Heddle.Generator` — two hits. `DefaultFunctionBinder.TryBind` has no shortcut and was already correct.
- **fixed by:** cycle 23 (`ec613c2`) — the shortcut survives, because an argument the estimator cannot type
  proves nothing and demanding types would take every `f(this)` and `f(SomeModelMember)` off the tier; it is
  gated on `ExcludedByTypedArguments`, since `OverloadRank.TryRank` decides applicability argument by argument,
  so one argument that WAS typed and converts to nothing rules the candidate out whatever the rest turn out to
  be. Both tiers of the two-tier bind are consulted so a `params` signature is not newly refused. The verdict is
  `HED7025` quoting the runtime's own `HED1012` sentence.
- **pinned by:** `AmbiguousOverloadDiagnosticTests` — two inapplicable sole-overload calls that must be build
  errors, and two neighbours (a fitting literal, a fitting member path) that must still precompile. Removing the
  gate reddens 2.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AmbiguousOverloadDiagnosticTests`.
- **notes:** deleting the shortcut outright was measured and rejected: it sends every single-`params` overload
  down the expanded tier (which this writer does not emit) and every untypeable argument to a degrade. See F-186
  for the residual the shortcut leaves.

### F-184 — `CallSiteValueType` had no arm for an embedded-C# call parameter

- **status:** FIXED
- **severity:** 1 → in practice 3 on the shapes reached (the engine refuses at compile time and the generator
  rendered)
- **found:** cycle 23
- **symptom:** under FullCSharp, with a typed model:

  | shape | engine | generated (540c096) |
  | --- | --- | --- |
  | `@out(@model.Products.Count)` into a `string` slot | refuses `HED5014` | **renders `[[q]]`** |
  | `@list(@model.Products.Count)` | refuses `HED0004` | **renders** |
  | `:: object` body fed by `@model.Title` | renders `[[q]]` | **degrades** |

- **root cause:** `CallParameter` has four positional shapes, closed by one boolean
  (`IsModelTypeParameter => ChainParameter == null && CSharpExpression == null && NativeExpression == null`), and
  `CallSiteValueType` had arms for three. A `CSharpExpression` fell through to `return null` — "cannot say" —
  while the engine sends the same text to `CSharpContext.ParseAndGetResultType`, compiles it with Roslyn and
  reads back the most definite type it ever has for a call-site value. Prior cycles missed it because
  `ExpressionMode.FullCSharp` is off by default.
- **class expansion:** all eight call sites of `CallSiteValueType`, enumerated from source (the method is private
  to `TemplateEmitter.cs`, so the population is closed). `AcceptedTypeSatisfied`, `SlotValueAssignable` and
  `ListElementModel` change; `ObjectDefinitionBodyModel` changes and gains a template;
  `DynamicDefinitionBodyModel` and the declared-`:: T` check are UNREACHABLE for this shape and were measured
  unchanged (both tiers still throw `InvalidCastException` on the latter).
- **fixed by:** cycle 23 (`ec613c2`) — `Binding/CSharpExpressionTyper` reproduces the engine's own wrapper (the
  collected `using`s, `namespace Heddle.Runtime`, `object PreProcessData(<model> model, object chained, object
  root)`, `return unchecked(<expr>);`), adds it to the compilation and reads `GetTypeInfo` on the return
  expression, including the engine's two `ExType.Dynamic` arms for an anonymous type and `dynamic`. Memoized on
  the model SYMBOL and the expression text, never a display string. Null stays "cannot say" wherever the probe
  produces no type or an error type.
- **pinned by:** `CSharpVerbatimTests` — two degrade rows each asserting the engine refuses, two neighbours with
  a satisfying C# type that must render the engine's bytes, and the `:: object` body row. Reverting the arm to
  `return null` reddens 3.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CSharpVerbatimTests`.
- **notes:** this is the entry that closes `SlotValueAssignable`'s exemption, recorded twice-unclosed under
  defect class E. It was not reachable by probing because every shape tried went through `TryTypeCallSiteBody`;
  the embedded-C# shape does not.

### F-185 — The build tier's name index dropped the leading dot of a namespace-less type, and had no assembly-qualified arm

- **status:** FIXED
- **severity:** 3 (both divergent directions) and 4 (the assembly-qualified degrade)
- **found:** cycle 23
- **symptom:**

  | spelling | engine | generator (540c096) |
  | --- | --- | --- |
  | `.GlobalNamespaceModel` | renders | **`error HED7007`** |
  | `GlobalNamespaceModel.Inner` | **refuses** | **renders** |
  | `.GlobalNamespaceModel.Inner` | renders | **`error HED7007`** |
  | `Ns.Article, <assembly>` | renders | **degrades, silently** |
  | `GlobalNamespaceModel.Inner, <assembly>` | renders | **degrades, silently** |

- **root cause:** three members of one class. (a) The runtime's full-name key is `type.Namespace + "." +
  shortName` and `Type.Namespace` is NULL for a global-namespace type, so its key carries a leading dot;
  `Qualify` returned the bare name, which diverges in both directions at once. (b) `ResolveSimpleType`'s first
  arm is `typeName.Contains(",") → Type.GetType` with a `.`→`+` retry ladder; the generator had no comma arm.
  (c) The runtime's DOTTED arm disambiguates several candidates by retrying `import + "." + typeName`, and the
  generator ran the SHORT-NAME rule (is a candidate's own namespace imported?) for both.
- **fixed by:** cycle 23 (`ec613c2`) — the leading dot restored; a `TryResolveAssemblyQualified` arm whose
  assembly half is parsed by Roslyn's own `AssemblyIdentity.TryParseDisplayName` and which requires every
  component the spelling states to match (so a stated Version or PublicKeyToken is honoured rather than ignored);
  and `TryResolve` restructured arm-for-arm against `ResolveSimpleType`. The type half needs no `+` ladder
  because the index already carries the dotted alias; the only gap was the leading dot, covered by also trying
  `"." + typeName`.
- **pinned by:** `ModelTypeSpellingTests` — five spellings that must bind on both tiers, one that must refuse on
  both, and an assembly-qualified spelling naming an absent assembly that must bind on neither (so the arm is a
  lookup, not a way of ignoring the qualifier). Dropping the leading dot reddens 3; removing the comma arm
  reddens 2; dropping the `"." + typeName` retry reddens 1; dropping the assembly-name match reddens 1.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ModelTypeSpellingTests`.
- **notes:** (c) is fixed BY CONSTRUCTION and not by measurement — it needs two assemblies declaring the same
  `Ns.Type`, which this reference closure does not contain. This entry is the "largest unexamined surface left"
  that defect class A named as its residual; that residual is now closed.
- **amended, cycle 24:** the claim that the arm "requires every component the spelling states to match, which is
  what the CLR's own load does with it" is **measurably false**. `Type.GetType` against an ALREADY-LOADED
  assembly binds by simple name and treats the rest of the identity as advice: `Heddle, Version=99.0.0.0`
  resolves, and so do a wrong `Culture` and a wrong `PublicKeyToken` — the fixtures assembly is strong-named
  and still resolves. For an assembly the context has NOT loaded the binder is stricter
  (`System.Linq, Version=99.0.0.0` → null, `Version=1.0.0.0` → resolves). So the generator refused
  `Version=99.0.0.0` where the engine resolved it, a silent severity-4 introduced by this very entry's fix.
  Cycle 24 (`08872ff`) dropped the version comparison — a version drifts on its own with every build — and
  kept the public-key-token check, with the asymmetry and the unloaded-assembly caveat in the doc comment.
  `NoVersionStated` is gone with it. Class F, the fourth consecutive cycle.

### F-188 — The constant-overflow refusal went stale when the emission became `unchecked`

- **status:** FIXED
- **severity:** 4
- **found:** cycle 23
- **symptom:** `@((2147483647 + 1))` renders `-2147483648` on the engine and degraded on the generator; likewise
  `2147483647*2`, `0u-1u`, `9223372036854775807L+1L` and eighteen more spellings. `unchecked(2147483647 + 1)`
  compiles — measured directly against Roslyn — so the build error the refusal existed to prevent can no longer
  happen.
- **root cause:** `ConstantFolding.Apply` evaluated checked and turned an `OverflowException` into a refusal, on
  the premise that C# rejects a constant overflow outright. `NativeExpressionWriter.WriteRoot` has wrapped every
  emission in `unchecked(` since the overflow-context fix (F-084), and `docs/language-reference.md` had already
  been updated to say so.
- **fixed by:** cycle 23 (`ec613c2`) — integral arithmetic wraps, which is the number the engine's unchecked
  `Expression.Add` produces. The three overflows `unchecked` does NOT settle still refuse, and each was measured
  against Roslyn rather than assumed: a division by constant zero (`CS0020`), a `decimal` overflow (`CS0463` —
  decimal overflow is outside the checked context entirely), and the smallest signed value over `-1`, which C#
  folds SILENTLY to that same value while the engine raises `OverflowException` at render.
- **pinned by:** `ConstantArithmeticDifferentialTests`, restructured rather than weakened — 17 rows moved from a
  degrade assertion to a BYTE-PARITY assertion (both tiers must wrap to the same number), 5 rows assert what
  `unchecked` does not settle, and the mixed int/uint column became F-187's two theories. Restoring `checked`
  reddens 15; removing the `MinValue / -1` guard reddens 2; removing the `OverflowException` catch reddens 8.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`.
- **notes:** the first version of this fix made ALL integral arithmetic wrap, which turns `int.MinValue / -1`
  into silent wrong output — the generator would render a number the engine raises on. It was caught by
  compiling the snippets against Roslyn before believing the fold, and is the reason the `DivisionOverflows`
  guard exists.

### F-189 — The fold reported agreement on a number and called it agreement on a type

- **status:** FIXED
- **absorbs:** F-187, which moves here from the known-open register
- **severity:** 1 (silent wrong output)
- **found:** cycle 24
- **symptom:** template `@model(){{string}}@(Length + (E))` over `"hello"`:

  | E | engine | generated (before) |
  | --- | --- | --- |
  | `(0u)-(1u)` | 4294967300 | 4294967300 — matches |
  | `(0)-(1u)` | 4 | degrades — the containment works |
  | **`(0-0u)-(1u)`** | **4** | **4294967300** |
  | `(0+0u)-(1u)`, `(0*1u)-(1u)`, `(1u-1)-(1u)`, `((0-0u)-(0u))-(1u)`, `(0&0u)-(1u)` | 4 | 4294967300 |
  | `~(0-0u)` | 4 | 4294967300 |
  | `(4294967295u-0)<<31` | 9223372034707292165 | 2147483653 |

  Measured over a 2880-cell matrix (12 operands × 2 unary forms × 10 operators × 12 operands): 1971 match,
  809 degrade-where-engine-renders, 96 degrade-where-engine-refuses, **4 different-bytes**, 0 build breaks.
  The rendering probe over the pre-fix behaviour gives 8 of 8 different bytes.
- **root cause:** `tiersPromoteDifferently` was computed in `Numeric.Unify` and consumed where it arose;
  `Folded` carried only a `Numeric` (kind + value), so the flag could not travel to an enclosing operation.
  The outer operation saw two `UInt` kinds and wrapped, where the engine had evaluated the inner pair in
  `long`. **The containment F-187 recorded was never removed — it leaked**, and F-187's own note said
  "severity 1 if the containment is removed".
- **class expansion:** every place a folded constant's TYPE, not its value, escapes into something that
  promotes from it. Members, from `src/Heddle.Generator/Emit/ConstantFolding.cs`: `FoldBinary` (the reported
  path), `FoldTernary:71` (the flag-less `Unify` overload), the bitwise arm `:143` (discarded the flag),
  `FoldShift:157` (never unified), `FoldUnary` (`~` does not reconverge, `-` does) — and, found by expanding
  the class rather than the repro, `NativeExpressionWriter.WriteCall`'s arguments and `WriteBinary` against a
  NON-CONSTANT operand.
- **predicate, side by side:** the code keyed on *did this one operation's unsigned arithmetic wrap?* The
  engine keys on *what does binary numeric promotion over the two operand TYPES give?* (`NumericTable.TryPromote`:
  `uint` + signed → `long`; C# converts a non-negative int CONSTANT to `uint` and evaluates there.)
- **fixed by:** cycle 24 (`08872ff`) — `Folded` carries both tiers' `Numeric`, the engine promotion is computed
  separately (`Numeric.UnifyAsEngine`), the fold refuses when the two numbers differ, and a surviving TYPE
  difference travels with the value. At the non-constant boundary the writer asks `Estimate(partner)`: signed
  integral, real and string reconverge on `long`; everything else does not.
- **pinned by:** `ConstantArithmeticDifferentialTests.AMixedPairFeedingASecondOperatorDegrades` (11 rows),
  `.AMixedPairWhoseSecondOperatorReconvergesStillPrecompiles` (6), `.AMixedPairMeetingAMemberFollowsThatMembersType`
  (5). Making the engine promotion equal C#'s reddens **18 of 129**, including six of F-024's own rows;
  forcing `TiersDiffer => false` reddens 4.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ConstantArithmeticDifferentialTests`.
- **notes:** checked against the defect it replaces (F-024, needless degrades): the four
  `LegalArithmeticStillPrecompiles` rows carrying a mixed pair — `5-1u`, `4294967295u-1`, `3000000000+1`,
  `3000000000/2` — all still precompile, which is what the writer-side partner check exists for.
  `AnIntMeetingAUintThatDoesNotWrapPrecompilesAndMatches` asserted bytes only and its name lied; it now
  asserts precompiled too. Classes A and D.

### F-190 — The one C# string literal the emitter wrote by hand

- **status:** FIXED
- **severity:** 1 (the silent face) and 2 (the build face)
- **found:** cycle 24
- **symptom:** a host declaring `[Prop("<name>", typeof(int), Default = 3)]`:

  | prop name | emitted | result |
  | --- | --- | --- |
  | `cols`, `a"b`, a raw tab, a raw NUL | correct | 0 errors |
  | **`a\b`** | `"a\b"` | **0 errors, decodes to `a` + U+0008** — `ArgumentException: 'a\b' is not a declared [Prop] parameter` at render, where the engine renders |
  | **`a\`** | `"a\"` | CS1010, CS1513, CS1002 |
  | **`a\rb`, `a\nb`** | a raw CR/LF inside the literal | 7 errors each |

- **root cause:** `TemplateEmitter.cs:985` built the literal itself —
  `"\"" + s.Name.Replace("\"", "\\\"") + "\""` — covering only the quote. The grammar's owner is
  `CSharpEscape.StringLiteral` (`src/Heddle/Language/Expressions/CSharpEscape.cs:13`), which the emitter uses
  at **every other** literal site. Reachable from any host `[Prop]` name: `PropLayoutCore.Build` admits
  arbitrary strings, refusing only null/whitespace, reserved and duplicate names.
- **class expansion:** the 11 string sinks that reach `.g.cs` in a position with an escaping grammar, closed by
  grep over `Emit/TemplateEmitter.cs` and `Emit/PieceWriter.cs`. Ten already delegated to the owner
  (`CSharpEscape` for literals, `ClassifyTypeName`/`FullyQualified` for type names); this one restated it. The
  two other uncovered positions found in the same pass are F-193 (the `#line` file name) and F-194 (a prop
  default's literal form).
- **fixed by:** cycle 24 (`08872ff`) — the site calls `CSharpEscape.StringLiteral`.
- **pinned by:** `ExtensionParametersDifferentialTests.PropNamesNeedingEscapesSurviveIntoTheNameIndexMap`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ExtensionParametersDifferentialTests`.
- **notes:** **class G as well as class D — the arm was executed by nothing.** Deleting the escape entirely
  reddened **0 of 554** Generator.Tests and **0 of 939** IntegrationTests. A hand-rolled restatement of a
  grammar someone else owns, unpinned, is the exact shape class D's sub-class names.

### F-191 — Nothing asked whether an embedded C# expression compiles

- **status:** FIXED, with one term of the engine's compilation unit still unreproduced (see notes)
- **severity:** 2
- **found:** cycle 24
- **symptom:** under `ExpressionMode.FullCSharp`, 25 shapes measured, 8 divergent. Six break the consumer's
  build where the ENGINE REFUSES: `@(@model.NoSuchMember)` → CS1061; `@(@model.Title +)` → CS1525;
  `@(@model.Title.Substring(1,2,3))` → CS1501; `@(@model.Products.Where(…))` with no `@using` → CS1061;
  `@(@model.Dead)` on an `[Obsolete(error:true)]` member → CS0619; `@(@checked(2147483647+1))` → CS0220.
  Two break the build where the engine RENDERS: `@(@new Product().Name)` and `@(@new CSharpContext()…)` → CS0246.
  Two more render where the engine refuses: `@out(@default(string))` and `@out(@null)` into an `out:: string` slot.
- **root cause:** `CSharpExpressionTyper` checked only whether the expression's own symbol was an error type and
  otherwise returned null — and null is "cannot say", which is the EXEMPTING answer at `SlotValueAssignable`,
  `AcceptedTypeSatisfied` and `IsUntypedReceiver`. The engine instead bails on `compilation.GetDiagnostics()`
  reporting ANY error (`src/Heddle/Runtime/CSharpContext.cs:192`). Cycle 23 built the probe compilation that
  answers this and routed its answer to the permitting side.
- **class expansion:** the engine's compilation unit term by term, from `CSharpContext.ParseAndGetResultType`
  (:121-183) and `Preparse` (:185-226) — that pair is the whole of the engine's answer, so the population is
  closed. Four terms were unreproduced: the model type's namespace import (:140), the model's generic type
  arguments' namespaces (:145-148), the whole-unit diagnostics check (:192-199), and `GetConstantValue` read
  FIRST with `Value?.GetType() ?? typeof(object)` (:209-214).
- **fixed by:** cycle 24 (`08872ff`) — the typer returns a three-field answer and `BuildCSharpExpr` refuses on
  `!Compiles`. Three of the four terms are added: the model and generic-argument namespaces (in the probe AND
  in `.g.cs`, gated on the file actually containing pasted C#), and the constant arm.
- **pinned by:** `EmbeddedCSharpCompilesTests` (21 rows). Removing the gate reddens 7 with the exact CS ids;
  removing the constant rule reddens the two `out:: string` rows; removing the namespace emission reddens the
  `new Product()` row.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~EmbeddedCSharpCompilesTests`.
- **notes:** **the strongest instance of class E yet.** F-184 enumerated the eight CONSUMERS of
  `CallSiteValueType`'s answer and never asked whether the answer was FAITHFUL; this is that residual. The
  fourth term is recorded in the source rather than inherited silently: the probe uses
  `_compilation.AddSyntaxTrees`, so consumer internals are visible to it where they are not to the engine's
  standalone compile. It is in the known-open register.

### F-192 — A `@using` body judged by a name walk rather than by whether its directive compiles

- **status:** FIXED
- **absorbs:** the unpinned-guard finding of the same cycle (the `ParseName` guard reddened 0 of 11)
- **severity:** 2 (the build break) and 4 (three legal forms refused)
- **found:** cycle 24
- **symptom:** 26 bodies measured against the real oracle — does `using <body>;` compile in the engine's own
  unit shape? 22 agree, 4 do not:

  | body | generator (before) | engine | consequence |
  | --- | --- | --- | --- |
  | `System.Linq //c` | accepted | does not compile | `.g.cs` emits `using System.Linq //c;` — the `;` is commented out → **consumer build CS1002** |
  | `static System.Math` | rejected | compiles | engine renders; generator omits and refuses any embedded C# |
  | `X = System.Linq`, `Alias = global::System.Linq` | rejected | compiles | as above |

- **root cause:** the guard was `parsed.ContainsDiagnostics || parsed.FullSpan.Length != text.Length`. A
  trailing `//` comment is trivia INSIDE `FullSpan`, so both terms pass — and the guard's own doc comment says
  it exists to stop a body that is not a name from breaking the generated file. A name walk also cannot see a
  `using static` or a using-alias, which are legal directives that name no namespace.
- **fixed by:** cycle 24 (`08872ff`) — the predicate is now "does `using <body>;` compile here": parse
  diagnostics, a one-directive shape check, and semantic diagnostics.
- **pinned by:** `UsingDirectiveTests.AUsingBodyIsJudgedByWhetherItsDirectiveCompiles` (9),
  `.AUsingBodyWithEmbeddedCSharpFollowsTheEnginesVerdict` (4). Removing the guard reddens 9; dropping the
  one-directive check reddens 1.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~UsingDirectiveTests`.
- **notes:** found while writing the pin — a body carrying its own `;` would have **declared a type into the
  consumer's assembly**. Refused and pinned. The `ParseName` guard cycle 23 installed was itself executed by
  nothing (0 of 11 under mutation), which is why the `//` face survived a cycle; class G again.

### F-193 — A `#line` file name is a `pp_string` and was written unescaped

- **status:** FIXED
- **severity:** 2
- **found:** cycle 24
- **symptom:** varying only the template's path:

  | path | result |
  | --- | --- |
  | `views/ok.heddle` | 0 errors |
  | **`views/o"k.heddle`** | **CS1025 ×2** |
  | **`views/o<LF>k.heddle`** | **CS1010 ×N** |
  | `views/o\k.heddle` | **0 errors — a backslash is NOT a defect here** |

- **root cause:** `TemplateEmitter.cs:3492` and `:3497` wrote `_lineDirectiveFile` straight into
  `#line (…) "<path>"`. That file name is a C# `pp_string`: escape sequences are NOT processed and the string
  is terminated by `"` or a newline. `_lineDirectiveFile` is the raw `AdditionalText.Path` when the template
  is out of root (`HeddleTemplateGenerator.cs:674-684`), and both `"` and a newline are legal filename
  characters on Linux and macOS.
- **fixed by:** cycle 24 (`08872ff`) — a path carrying `"` or any of the five C# line terminators emits
  `#line hidden` instead: the source mapping is lost, the build is kept.
- **pinned by:** `TemplateNameMetadataTests.ATemplatePathWithNoLineDirectiveSpellingLosesTheMappingNotTheBuild`
  and `.ABackslashInATemplatePathKeepsItsLineMapping`.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~TemplateNameMetadataTests`.
- **notes:** **record explicitly that the backslash is not a member of this class.** It looks exactly like the
  class-D string-surgery pattern and it is measurably fine, because `pp_string` does not process escapes — so
  Windows paths need no treatment and "fixing" them would be the defect. The second pin exists to stop a
  future cycle escaping it.

### F-194 — `LiteralFormatter` had no non-finite arm, and the guard lived on the other caller

- **status:** FIXED
- **severity:** 2
- **found:** cycle 24
- **symptom:** `[Prop("w", typeof(double), Default = <expr>)]`:

  | default | emitted | result |
  | --- | --- | --- |
  | `1.5`, `double.Epsilon` | `1.5D` | 0 errors |
  | **`double.PositiveInfinity`** | `new object[] { InfinityD }` | **CS0103, and the manifest still says precompiled** |
  | **`double.NegativeInfinity`, `double.NaN`** | `-InfinityD`, `NaND` | **CS0103** |

  `double.PositiveInfinity` and `NaN` are `const double` fields, so they are legal attribute arguments and the
  host source compiles cleanly.
- **root cause:** `src/Heddle/Language/Expressions/LiteralFormatter.cs:25-26` emits
  `f.ToString("G9", …) + "F"` / `d.ToString("G17", …) + "D"`, which for a non-finite value yields `InfinityD`
  or `NaND` — not a C# literal.
- **class expansion:** both callers of `LiteralFormatter.Format`, closed by
  `grep -rn "LiteralFormatter.Format" src` (2 production sites). `NativeExpressionWriter.cs:180` is fed a
  parsed template literal and is GUARDED at parse time (`ExpressionAstBuilder.cs:389,397,409` refuse a
  non-finite). `TemplateEmitter.cs:2374` is fed a Roslyn `TypedConstant` prop default and had no guard.
- **fixed by:** cycle 24 (`08872ff`) — both `float` and `double`.
- **pinned by:** `ExtensionParametersDifferentialTests.ARealDefaultWithNoLiteralFormDegrades` and
  `.AFiniteRealDefaultStillPrecompiles`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~ExtensionParametersDifferentialTests`.
- **notes:** textbook class D — the rule was written for the one caller in front of the author, and the other
  caller is the one that can actually see the value.

### F-195 — The hosted resolver could not read a template from disk on Linux or macOS

- **status:** FIXED (a third defect in the same function is known-open)
- **severity:** 3
- **found:** cycle 24
- **symptom:** with real files at `<root>/views/home/index.heddle` and `<root>/views/index.heddle`, on Linux:
  `Search("index", "home", View)` → **null**; `Search("home/index", "", View)` → **null**. 0 of 4 candidate
  locations resolve. The precompiled first loop still works (it runs `TemplateKey.TryNormalize`), so on Linux
  a hosted resolver serves ONLY precompiled templates and silently never falls back to disk.
- **root cause:** `src/Heddle/Runtime/TemplateResolver.cs:11-14` hard-codes Windows separators —
  `@"\views\{1}\{0}"`, `@"\views\base\{1}\{0}", `@"\views\partial\{1}\{0}"` — and `:172` does
  `viewName.Replace("~/", "/").Replace('/', '\\')` on every platform. On Unix `\` is an ordinary filename
  character, so `Path.Combine` and `File.Exists` never see a separator. Engine side; the same mistake as F-181.
- **fixed by:** cycle 24 (`08872ff`) — the patterns are root-relative and `/`-separated, and `searched` reports
  the paths actually probed rather than the un-substituted pattern.
- **pinned by:** `HostedTemplateResolverTests` (13 tests; the arms previously had **zero**). Restoring
  `\views\{1}\{0}` reddens 6.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~HostedTemplateResolverTests`.
- **notes:** a secondary defect in the same function, also fixed: `:226` added
  `Path.Combine(path, viewName)` with the UN-substituted pattern, so the "searched locations" in the error read
  `\views\{1}\{0}/index.heddle` — a path nobody probed. **Class G: `grep -rn "TemplatePathType" src/Heddle.Tests`
  showed only `TemplatePathType.None` was ever exercised**, which is why this survived to cycle 24.

### F-196 — The build tier's signature key told apart what reflection cannot

- **status:** FIXED
- **severity:** 4
- **found:** cycle 24
- **symptom:** ten signature shapes measured on both sides. Two collapse differently:

  | shape | build-tier `SignatureKey` | runtime `Type.FullName` |
  | --- | --- | --- |
  | `(int a, int b)` vs `(int x, int y)` | **distinct** (element names carried) | **identical** — `System.ValueTuple``2[[System.Int32,…]]` |
  | `object` vs `dynamic` | **distinct** (`System.Object` vs `dynamic`) | **identical** — `System.Object` |
  | `nint`/`IntPtr`, `int?`, `List<int>`, `string[]`, nested types | agree | agree |

  Build side end-to-end with two containers exporting a CLR-identical signature: 2 overloads, 2 manifest rows,
  `overloadCount=1` each — where the runtime's `SameSignature` returns true and `AddOrReplace` keeps ONE.
  `PrecompiledGauntlet.cs:152-160` then sees `liveCount=0 < row.OverloadCount=1` → `FunctionBindingMismatch`
  → the whole template falls back at run time.
- **root cause:** `FunctionExportResolver.cs:276` keys on
  `type.ToDisplayString(FullyQualifiedFormat, global omitted, EscapeKeywordIdentifiers)`;
  `FunctionRegistry.cs:164` keys on `type.FullName ?? type.Name`. Both feed the SAME dedup rule
  (`ExportRules.SameSignature` → `ExportBookkeeping.AddOrReplace`), and metadata does not carry tuple element
  names or the `dynamic` attribute in a `FullName`.
- **fixed by:** cycle 24 (`08872ff`) — the build-side key loses the same two distinctions.
- **pinned by:** `ExportBookkeepingTests.TheRuntimeSignatureKeyLosesWhatMetadataDoesNotCarry` — **the runtime
  half is now executed, not derived.** Reverting `SignatureKey` reddens 2.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter FullyQualifiedName~ExportBookkeepingTests`.
- **notes:** closes the class-A residual named as `SignatureKey` versus `Type.FullName`. Residual: a tuple
  NESTED inside another type argument still carries its element names, because `ExpandValueTuple` does not
  exist in the Roslyn the generator compiles against; stated in the doc comment.

### F-197 — The refusal of `chained` and `root` was a word-boundary regex over raw text

- **status:** FIXED
- **severity:** 4
- **found:** cycle 24
- **symptom:** `@("root".Length)` — engine renders `4`, generator degraded.
  `@(model.Args.Select(chained => chained.Name).Count())` — engine renders `1`, generator degraded.
- **root cause:** `TemplateEmitter.cs:2932-2933` used `Regex.IsMatch(csharp, @"\b" + …Chained + @"\b")`. The
  class is every occurrence of those two words that is not the engine's parameter: string literals, lambda
  parameters, member names, comments.
- **fixed by:** cycle 24 (`08872ff`) — a binder question asked over the probe tree, so a lambda parameter
  shadows and a literal is not an identifier.
- **pinned by:** `CSharpVerbatimTests` — restoring the regex reddens 3.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~CSharpVerbatimTests`.
- **notes:** class D's sub-class — a language question (is this identifier bound to that parameter?) restated
  as text matching. The probe compilation F-191 built is what made the real question askable.

### F-202 — A probe tree parsed with the defaults cannot be grafted onto a consumer compilation that sets parse options

- **status:** FIXED
- **severity:** 2 (generated code breaks the consumer's build)
- **found:** cycle 25, from a sample that would not build — not from a review
- **symptom:** `samples/codegen-t4-successor` and `samples/precompiled-app` failed `dotnet build` with
  `error HED7020: … ArgumentException: Inconsistent syntax tree features (Parameter 'trees')`, followed by
  `error CS0234: 'Templates_Report' does not exist in namespace 'Heddle.Generated'` — the entry-point type the
  consumer's own source names, gone because every template in the project left the precompiled tier.
- **root cause:** two probe compilations parsed a tree with DEFAULT options and grafted it onto the consumer's
  compilation — `SymbolTypeResolver.UsingDirectiveCompilesCore` and `CSharpExpressionTyper.Resolve`, each a
  `CSharpSyntaxTree.ParseText(source)` followed by `_compilation.AddSyntaxTrees(tree)`. Roslyn requires every
  tree in a compilation to have been parsed alike.
- **reach, and it is much wider than the two samples:** a plain `dotnet build` on SDK 10.0.110 passes
  `/features:"InterceptorsNamespaces=…"` **unasked**, so *every* consumer's compilation has non-empty
  `ParseOptions.Features`. Every consumer with a `@using` or an embedded C# expression was affected; the other
  eight samples survived only because no template in them reaches a probe.
- **introduced by this review series:** `ec613c2` (the typer graft) and `08872ff` (the directive-check graft).
  Bisected by building the sample per commit: clean at `ec613c2` and `703567d`, broken from `08872ff` onward,
  because the samples carry a `@using` and no embedded C# so only the second site is reachable from them. **Both
  sites break independently** — reverting either alone reddens exactly its own rows.
- **fixed by:** cycle 25 (`801dfde`) — `Binding/ProbeParseOptions.For(compilation)` returns the options of a
  tree the compilation already holds, falling back to defaults only when it holds none; both probes parse
  through it. Not `context.ParseOptionsProvider`: the invariant Roslyn enforces is among the trees of the
  compilation being added to, so reading the options off one of them makes the graft legal **by construction**
  rather than by coinciding with the driver's configuration. It is also the only reading that asks the probe's
  question under the language version and preprocessor symbols the consumer's own compiler will use.
- **class expansion:** every construction of a syntax tree or compilation across `src/`, by grep for
  `AddSyntaxTrees` / `ParseText` / `CSharpSyntaxTree.` / `CSharpCompilation.Create` / `ParseSyntaxTree` /
  `WithParseOptions` / `SyntaxFactory.Parse*`. Two defects, both fixed. Three clean:
  `SymbolTypeResolver.cs:697` creates a compilation with **no trees at all**; `CSharpContext.cs:189-191` and
  `ContextCompilation.cs:141-142` each create a fresh compilation whose only tree is their own — and at run time
  no host parse options exist to agree with. `Heddle.LanguageServices`, `Heddle.LanguageServer`,
  `Heddle.Language` and `Heddle.Tool` construct no tree or compilation at all.
- **pinned by:** `ConsumerParseOptionsTests` (4) — a consumer compilation carrying `LanguageVersion.Latest`,
  a feature flag and preprocessor symbols, applied to the trees **and to the driver** as a real build does.
  Two rows redden without the resolver fix, two without the typer fix; one is the near-neighbour proving the
  options change only legality and not output, and one is the degrade neighbour so a "fix" that stopped the
  probe asking cannot pass.
- **regression check:** `dotnet test src/Heddle.Generator.Tests -f net8.0 --filter FullyQualifiedName~ConsumerParseOptionsTests`.
- **notes:** **why no suite could see it — class G and class H together.** Every generator harness built its
  compilation from zero trees or from trees parsed with `CSharpParseOptions.Default`, so the one thing that
  triggers the fault was the one thing no test varied. `GeneratorHarness` now accepts parse options and hands
  the same ones to the driver. The emitter's catch-and-report is what turned a probe fault into a hard build
  error rather than a silent degrade — that behaviour is correct and unchanged, and it is the only reason this
  surfaced at all.

### F-203 — A test filter that matches nothing exits 0, so every stale regression check passed

- **status:** FIXED
- **absorbs:** F-081, whose recorded instances were the wrong ones
- **severity:** 6, but it is the instrument every other entry's regression check runs on
- **found:** cycle 25
- **symptom:** `dotnet test src/Heddle.Tests -f net8.0 --filter "Name~ThisTestDoesNotExistAnywhere"` ran zero
  tests, printed nothing at quiet verbosity, and **exited 0**.
- **why it matters more than its severity:** every regression check in this register is a `--filter` command.
  A filter naming a test that has been renamed or deleted reported success while executing nothing. That is
  exactly how two shipped fixes came to be documented as pinned with nothing running them (the parse-depth
  bound and the unbalanced-closer guard, both found only when the register was consolidated), and how
  `ec613c2` silently disarmed a third by renaming `AUnsignedLongMeetingACharDegrades…` to
  `AnUnsignedLongMeetingACharWraps…`. Making an empty run an error retroactively converts all 106 checks from
  "green if the name still exists" into checks that are actually executed.
- **F-081's recorded instances were both wrong, and are corrected here.** It cited a declared target framework
  running zero tests and a wrong-TFM invocation. Measured on SDK 10.0.110: `-f net6.0` with no .NET 6 runtime
  aborts and exits **1**; `-f net8.0` against the net10.0-only LanguageServices project fails `NETSDK1005` and
  exits **1**. Neither is the defect. The live instance is the filter, and it was never written down.
- **fixed by:** cycle 25 — `tests.runsettings` at the repository root sets
  `RunConfiguration.TreatNoTestsAsError`, wired to every project through `RunSettingsFilePath` in
  `Directory.Build.props`. Verified both ways: a stale filter now exits 1; a filter matching real tests exits 0
  and runs them.
- **regression check:** `dotnet test src/Heddle.Tests -f net8.0 --filter "Name~ThisTestDoesNotExistAnywhere"`
  must exit **non-zero**. (This is the one check in the register that passes by failing.)
- **notes:** the first attempt at the settings file broke every run — an XML comment containing `--filter`,
  which XML forbids, so the settings file was rejected and a real filter also exited 1. Caught because the
  positive control was run beside the negative one. **A guard is not verified until both of its answers are
  measured** — the same rule this register keeps applying to production code applies to the harness.
- **what the guard found the moment it was switched on:** **26 checks — every one written as
  `--filter Name~…` — matched zero tests.** Not because a name was stale: `Name~` with the exact, correct,
  full method name also matches nothing, because this xUnit adapter supports only `FullyQualifiedName`. A
  quarter of this register's regression checks had therefore never executed anything, and had passed silently
  every time. Two further checks named the wrong project (`ExportBookkeepingTests` lives in `Heddle.Tests` and
  `TemplateNameMetadataTests` in `Heddle.Generator.Tests`; both checks pointed at
  `Heddle.Generator.IntegrationTests`) — merged verbatim from proposed deltas without the project being
  verified. All 28 corrected; the whole set of 74 unique checks now runs and passes.
- **how it survived the consolidation that was supposed to catch it:** that pass explicitly claimed *"all 65
  runnable filters validated against `dotnet test --list-tests`; every one matches ≥1 test"* — and it did that
  by **reimplementing the filter grammar** (its own `~`, `=`, `!~`, `!=`, `&`, `|` matcher) against a list of
  names, rather than running the tool. Its reimplementation assumed `Name` worked. **That is defect class D's
  sub-class — restating a grammar its owner already implements — committed by the verification harness while
  verifying.** The lesson generalises past this file: a check on a tool's behaviour must invoke the tool.

### F-200 — A `@using` alias and a `using static` bound nothing, because the type resolver read every body as a namespace

- **status:** FIXED
- **severity:** 3 (a spelling the engine refuses that a language resolves) and 2 (the build tier calling a legal
  spelling a typo)
- **found:** cycle 25, **by the maintainer, not by a review cycle** — "type should not be resolved by
  exclusively full name; this is a language, not a metadata storage"
- **symptom:** with a body the cycle-24 gate now accepts as legal, both tiers refuse everything needing it:

  | template | engine (672ab71) | generated (672ab71) |
  | --- | --- | --- |
  | `@using(){{X = Ns}}` + `@model(){{X.Article}}` | **refuses** | **`error HED7007`** |
  | `@using(){{X = Ns.Article}}` + `@model(){{X}}` | **refuses** | **`error HED7007`** |
  | `@using(){{static Ns.Outer}}` + `@model(){{Inner}}` | **refuses** | **`error HED7007`** |
  | `@model(){{global::Ns.Article}}` | **refuses** | degrades |

  Twelve spellings measured directly against both resolvers: all UNRESOLVED on both tiers.
- **root cause:** one wrong model, faithfully mirrored. `UsingExtension.InitStart` → `CSharpContext.ImportNamespace`
  collects every body into an `ICollection<string>`, and `ResolveSimpleType` asks that collection only two
  questions: `imports.Contains(t.Namespace)` and `import + "." + typeName`. An alias body answers neither, so it
  is inert; `global::` is a qualifier no index key carries, so every arm refuses it. **The list is not a list of
  namespaces — it is a list of C# using-directive headers, two of whose three forms bind a name.** F-192 made
  those two forms legal without making either mean anything, so the door stood half-open for one cycle.
- **class expansion:** every consumer of the collected list, from
  `grep -rn "CSharpContext.Namespaces\|_namespaces" src --include=*.cs`. Three: the verbatim emission into the
  preparse and class templates (correct — the C# compiler reads it), the reflection resolver (wrong, fixed), and
  `DocumentAnalyzer`, which reaches the same resolver through the same carrier and was fixed by the same change.
  The build tier's mirror (`SymbolTypeIndex.TryResolve`) is the fourth and was fixed arm for arm.
- **fixed by:** cycle 25 (`cde2a29`) — `Heddle.Language.Binding.UsingDirectives` classifies a body into alias /
  `static` target / namespace; `ResolveSimpleType` and `SymbolTypeIndex.TryResolve` gain a `global::` arm BEFORE
  the index and alias / `static` arms AFTER it. **After the index is what makes it additive by construction
  rather than by testing.** The classifier is hand-written because type resolution outlives the C# tier in a
  trimmed publish (`HeddleFeatures.CSharpTierEnabled=false` links the whole `Microsoft.CodeAnalysis` graph away),
  so the grammar is asked of the compiler in a GATE instead: `UsingDirectiveClassificationTests` parses
  `using <body>;` with Roslyn over 20 bodies and asserts the classifier agrees with Roslyn's own verdict.
- **pinned by:** `TypeSpellingLockstepTests` / `TypeSpellingSymbolLockstepTests` (24 new rows each side including
  controls that must stay unresolved), `UsingDirectiveClassificationTests`, `AliasTypeResolutionTests` (10
  end-to-end differentials byte-compared on both tiers), `AliasTypeProjectionTests` (the editor path). 13
  mutations red, each on the tier it belongs to — including one whose only job is additivity: moving the alias
  arm BEFORE the index reddens with `imports ["…TieAlpha", "TieProbe = …TieBeta.TieProbe"] — Expected
  …TieAlpha.TieProbe, Actual …TieBeta.TieProbe`.
- **regression check:** `dotnet test src/Heddle.Generator.IntegrationTests -f net8.0 --filter FullyQualifiedName~AliasTypeResolutionTests`.
- **notes:** additivity was the ratified constraint and it is proved, not asserted — every new spelling was
  UNRESOLVED on both tiers before, and `git diff -U0` over both lockstep suites has exactly one `-` line (the
  closing quote of an extended literal). **Where C# and Heddle still disagree is recorded rather than fixed:**
  C# gives an alias precedence over an imported namespace (measured), Heddle keeps the index's answer, because
  changing it would move a spelling that resolves today. That is a row in the breaking-windows candidate
  register, pinned on both tiers, folded together with head-first scope walking for `A.B.C` since it is the same
  ordering question.
  **The general lesson, and the reason this entry exists at all: a differential review cannot find a wrong
  model.** The generator mirrored the engine exactly, so 24 cycles scored this green. What found it was asking
  what the C# compiler does with the same text — and the fix is only trustworthy because that question is now a
  gate rather than a memory.


---

## Defect classes

**Read this section first.** Each class groups findings that share an underlying mistake, and says
whether anybody ever enumerated the class exhaustively. Where a class is not enumerated, the next
member is findable by construction — write the enumeration before fixing the reported instance.

**What consolidation showed.** Merging the register's 179 ids into 79 entries changed the honest
membership counts of four classes, in every case upward. Where the count below differs from what the
class used to claim, the difference is stated. The general finding: **the register was
under-reporting class size because each cycle appended an entry rather than joining one, so a class
that had been hit five times looked like five unrelated findings.** Two classes turned out to be one
predicate answered eight and seven times respectively (F-112, F-024), and one — class G — has more
members than any other class in the file.

### A — the emitter keys on a *spelling* where the engine keys on a resolved answer

**Members, as merged:** F-025 (rows F-037 cycle key = raw path text, F-063 import identity vs the
reader's own normaliser), F-102 (rows F-102 path memo keyed on a display string, F-113 body cache key
missing the deciding term), F-111 (rows F-120 a `?` suffix the shared grammar does not have, F-123
dotted `@model` matched by suffix), F-112 (rows F-156 `:: object` vs the word `dynamic`, F-170 the
same again one path over — a region's own declaration), F-171 (a body identity term the engine does
not key on), F-178 (the import identity — a normalised template key where the engine has a canonical
disk path).

**Consolidation note: the class is larger than it looked, and one predicate accounts for a third of
it.** `:: dynamic` versus `acceptType == typeof(object)` was answered wrongly seven times over eight
cycles; those are now the rows of F-112 rather than seven entries, but the *count of times this class
was hit* is what should be read from it. **Import identity has been the finding four times**
(F-025's F-037 and F-063 rows, F-178, and the `Path.Combine` platform half F-079).

**Enumerated in cycle 22, and it produced a severity-2 defect too.** The grid of every place a *string*
stands as the identity of something the engine resolves — dictionary keys and comparisons built from
`ToDisplayString`, fully-qualified name text, or raw directive text — was written out against what the
engine compares in the same position. The row that diverged was the import identity (F-178), and it is
closed by `grep -rn "ImportIdentifier"`: five hits, one of which is the generator's own assignment. The
**LanguageServices facade sets neither `ImportReader` nor `ImportIdentifier`** and parses through
`DocumentParser.Runtime`, so the LSP already had the engine's rule exactly and needed no treatment.

**Record explicitly that the `"dynamic"` string test is NOT a member of this class.** The emitter's
`def.ModelType == "dynamic"` looks exactly like F-112's F-156 row, which was the same comparison one
path over and *was* a defect. It is correct here, and for a reason a future cycle must not re-derive by
intuition: `HeddleCompiler.cs:585` compares the same text, so the engine keys on the spelling in that
position too. Resolving it would be the divergence. This trap has now been walked into once and
disarmed once.

**The named residual was written out arm for arm in cycle 23 and produced three members** (F-185), which
is what the residual was for. `SymbolTypeIndex`'s name maps against `ReflectionHelper`'s: the runtime's
full-name key carries a **leading dot** for a namespace-less type and the index returned the bare name
(divergent in both directions at once); there was **no assembly-qualified arm** at all against the runtime's
`typeName.Contains(",")` branch; and the **dotted** arm ran the short-name disambiguation instead of the
runtime's `import + "." + typeName` retry. Two were measured divergent, the third is fixed by construction —
it needs two assemblies declaring the same `Ns.Type`, which this reference closure does not contain.

**Residual, resolved in cycle 24 — the class has no unexamined surface left.** `SignatureKey` versus
`Type.FullName` was the last one carrying a defect: the two feed the SAME dedup rule and collapse differently
on tuple element names and on `object`/`dynamic` (F-196). The other two were located and closed by argument
rather than by edit: the **diagnostic position by text search** squiggles the first textual match and its
dedup key makes two distinct failures collapse into one — real, off-scale, recorded; and the **two key shapes
sharing one dedup set** (`_seenInaccessibleTypes`, written at `TemplateEmitter.cs:3143` with a method display
and at `:3256` with a fully-qualified type) are disjoint by construction, since a method display always
carries `(` and a fully-qualified type always starts `global::`. That argument is now a comment in the code,
**with its own caveat recorded**: it was not proved over every `ITypeSymbol` shape, and a tuple type's
fully-qualified name does contain parentheses.

### B — a reader that does not consult the prop layout, and a context that does not carry it

**Members, as merged:** F-131 in full — its rows are the `@list` body nested in a definition (F-131),
the expression writer (F-136), the computed call-site typing (F-137), caller content, the *context*
half (F-141), the prop-argument check (F-142), and native expressions refused before the layout was
consulted (F-143). Plus F-112's F-161 row as the generalisation (a context carrying the layout but not
the model), and F-173 (a context carrying neither the layout's neighbour, the slot type).

**Enumerated? Yes, for the layout — and this is the series' one success story.** Cycle 16 wrote out
two tables before fixing anything: every `BodyContext` construction site in `TemplateEmitter.cs` and
whether the layout travels with it (Table A, ten rows), and every reader of a path's first segment and
whether it tries the layout first (Table B, twelve rows). Cycle 17 had both re-derived independently by
two reviewers; both came back complete with no wrong verdict — the first completeness claim in the
series to survive two independent re-derivations. **Three cycles of "fixed it, and the same defect
turned up one path over" ended when the lists were written out in full.** That is exactly what
consolidating this class into one entry preserves: six ids, one table, one command.

**The residual was live, and held two defects.** The tables answered *does this context carry the prop
layout* and not *what else does a context carry*; F-161 was already that gap. Cycle 21 wrote the
missing grid — the eleven `BodyContext` construction sites against the seven pieces of state one holds
— and the two uninspected columns each produced a finding:

| column | verdict |
| --- | --- |
| `Props`, `RegionHostProps`, `Fills` | correct at all eleven sites |
| `SlotType` | **wrong at both region sites** — a region never entered its own slot mode (F-173) |
| `ModelSymbol`, `ModelCast`, `IsDynamic`, `DynamicBodyModel` | **wrong at the typed-region site** for the two spellings that resolve to `System.Object` (F-170, a row of F-112) |

**The register's own predicted residual was the finding.** Two further questions the grid does not
answer, and the second is where F-171 came from:
- *what does each site carry that it should not* — the mirror of the question above.
- *what is the emitter's body identity, term by term, and does the engine key on the same terms?* The
  emitter had a term the engine does not (the fill scope), and it moved bytes on eleven of seventeen
  enclosing-body shapes.

### C — can generated code in the consumer's assembly name this type?

**Members, as merged:** F-098 in full — ref struct as a model (F-098), static class (F-105), the
kind-by-kind answer replaced by two predicates (F-110), type arguments (F-119), a property type that
is merely unusable (F-122), an enclosing type's arguments (F-125), and a hop's property type
(F-101, closed in cycle 22 as subsumed). Plus F-104 (an `[Obsolete(error:true)]` type, member and
*method*), F-111 (a spelling that resolves to no symbol — rows F-111 and F-127), F-091's rows for the
accessibility half, and cycle 22's two: F-176 (the bound extension's own type) and F-177 (`@using`).

**Enumerated? Yes for `TypeKind`, and now yes for positions.** Cycle 11 replaced the kind-by-kind
widening with two predicates, and cycle 14 made the kind table a function of `TypeKind` with a row
asserting the rows cover the enum — so a kind Roslyn adds later arrives with no verdict and the theory
does not compile past it. Recursion covers array elements, pointer elements, type arguments and
containing types.
**Positions were enumerated in cycle 22, and the enumeration immediately produced two severity-2
defects that eleven cycles of probing had not reached.** Before it, positions were fixed one reported
instance at a time: cycle 9 fixed four (`@model`, definition, slot, prop), cycle 13 found the
entry-point parameter still written verbatim, cycle 18 found the *method* of an exported call had never
been asked.

The enumeration closes on the **sinks**, not on a list of call sites: everything that reaches `.g.cs`
goes through `CodeWriter.Line`/`Raw`, the `_fieldDecls` buffer, the `_methodDecls` buffer, or the
manifest builder, and `grep -rn "w\.Line(\|w\.Raw(\|_fieldDecls\.\|_methodDecls\." src/Heddle.Generator
--include=*.cs` reaches exactly two files (`Emit/TemplateEmitter.cs`, `Emit/PieceWriter.cs`). Walking the
positions against `Classify` found the **bound extension's own type** unguarded at three of its four
writes (F-176) — the fourth reachable only through an assembly literally named `Heddle`, and covered by
placing the check before that branch rather than by measuring it.

**The class is not limited to types.** `@using` (F-177) has the same failure mode for a name that is
never resolved to a symbol at all: text from the template written into a compilation unit, where a name
resolving to nothing is `CS0246` and text that is not a name stops the file parsing. A position list
built by asking "which *types* does the emitter spell" would have missed it.

**Residual: one position has no gate and no demonstrated reach.** The export argument cast
(`Binding/ExportFunctionBinder.cs:133`) writes `parameterType.ToDisplayString(FullyQualifiedFormat)`
straight into a cast with no `Classify` call. It is safe today only because of the *range* of the
argument estimator — the parameter types it can reach are the ones `ToSymbol` produces — rather than
because anything checks. **Widening the estimator re-opens it**, and nothing in the build would say so.

F-101 was the last recorded survivor and is now closed as subsumed; see it in F-098.

**Cycle 23 added a member the class had not reached: an exported function's CONTAINER type** (F-182's
chained half). Accepting a nested-public container — which the engine accepts and a shared table states —
made a name reachable that generated code may not spell from a referenced assembly, where it is `CS0122`.
The question moved to where the call is written (`NativeExpressionWriter.CanWriteCallTo`) rather than
through `ClassifyTypeName`, because a static class is refused there as unable to hold a value and a call
receiver is not a value position. **A position list built by asking "which types does the emitter spell"
would have missed this too** — the container is spelled as a call receiver, not as a value.

**And the residual above is now joined from the other side.** F-186 is the same seam seen from the
estimator's end: the argument cast is safe because of the estimator's range, and the estimator's range is
*also* what lets an untypeable argument bind an inapplicable overload unchecked. One seam, two open faces.

### D — a rule implemented for the one case in front of the author

**Members, as merged:** F-004 (type resolution fixed, C#-tier metadata not — rows F-004/F-019),
F-061's F-062 row (a guard for the disk reader's exceptions over a public seam), F-091 in full
(member fixed and type assumed to behave the same and it does not; metadata references but not
compilation references; `@model()` but not definition/slot/prop), F-084 (native expressions wrapped,
embedded C# not — rows F-084/F-095), F-138 (`@list`'s accepted type written out; every other
extension unchecked — rows F-138/F-149), F-148 (one shape of return type, because the descriptor
could only spell that shape), F-112 (the definition body, then every call form, then caller content,
then regions — rows F-124 → F-130 → F-150 → F-170), F-104 (the type and the member, then the method).

**A sub-class named in cycle 23, because four of its members were one mistake: a platform or language
question restated as string surgery.** F-181 split a path on a hard-coded `/` after replacing `\` with it,
where `Path.DirectorySeparatorChar` answers the question and `Path.GetFullPath` is what the engine actually
resolves through. F-180 split a C# name on `.` and compared segments ordinally, where `SyntaxFactory.ParseName`
answers it and no real spelling — `global::`, interior whitespace — survives the split. F-185 had no
assembly-qualified arm and would have needed one; it was written with `AssemblyIdentity.TryParseDisplayName`
rather than by cutting on the comma. The pattern is findable by grep — a `Split`, `Replace`, `Contains` or
`IndexOf` over text whose grammar something already parses — and the rule is: **ask the component that owns
the grammar, or mirror the engine's own code path exactly and say which.** Restating it is how the two tiers
drift, and on a path it also makes the answer wrong on two of the three supported operating systems.

**ENUMERATED in cycle 24.** Method:
`grep -rnE '\.(Split|Replace|IndexOf|LastIndexOf|Substring|TrimStart|TrimEnd|EndsWith|StartsWith|Contains)\(' src/Heddle.Generator src/Heddle --include=*.cs`
→ **37 generator sites and 77 engine sites**, every one classified by "who owns this grammar, and does this
agree on all three operating systems". The sweep yielded **four new members** — F-190 (a C# string literal
written by hand where `CSharpEscape` is the owner), F-193 (a `#line` file name, a `pp_string`, written
unescaped), F-195 (the hosted resolver's Windows-only path patterns, engine side), F-197 (a language question
answered by a word-boundary regex) — plus the layer of F-181 that survived its own fix.

**Six sites were closed with a stated argument rather than a guess**, which is what makes the enumeration
worth keeping: `ReflectionHelper.cs:396` matches an exception message thrown by the same file and not
localized (a text coupling worth a constant, not a defect); `ParseContext.cs:540` compares `StartsWith("@:")`
without a `StringComparison`, and the lexer's token alphabet closes it — `HeddleLexer.g4:43-44,65` admits
exactly `@{…}@`, `@:…` and `@@`, so the second character is always `{` or `:`, neither ignorable under any
collation (given `StringComparison.Ordinal` anyway, for hygiene); `TemplateKey.cs:83-88` is deliberate and
documented; `DefaultFunctionBinder.cs:211-261` walks a closed hand-written table; `HeddleTemplateGenerator.cs:623-632`
splits a normalized key rather than a path; and the diagnostic-position text searches
(`HeddleTemplateGenerator.cs:536-545`, `TemplateEmitter.cs:3189`) are off-scale — they squiggle the first
textual match, so two distinct failures spelled alike collapse to one diagnostic.

**And record what is NOT a member, because it looks exactly like one:** a backslash in a `#line` file name is
measurably fine — `pp_string` does not process escapes — so Windows paths need no treatment and "fixing" them
would itself be the defect. F-193 carries a second pin whose only job is to stop a future cycle escaping it.

**Consolidation note: this class and class A are largely the same findings seen from two sides.** A
rule keyed on the wrong predicate (A) and a rule applied at one of several sites (D) produced the same
merges. Where they differ is the fix: A is fixed by asking the engine's question; D is fixed by
finding every site, or by making the site-list unnecessary.

**Enumerated? Partly, and by two different techniques.** The `[DataType]` case is now closed *by
construction* — the check reads the attribute off the bound extension, so a new extension is covered
without anybody listing it. Same for call return types (read the overload's declared type) and
assignability (ask the adapter). **That is the durable answer this class has: read the authority
instead of a projection of it.** The cases still closed only by enumeration — which paths wrap in
`unchecked`, which reference kinds a visibility check understands, which positions type a body — have
no such guarantee.

### E — "cannot say" is the exempting answer

**Members, as merged:** F-024's F-059 row (`Unknown` from the fold meant "emit as written"), F-106's
F-121 row (`null` had a type on one tier and not the other), F-112's F-130 row (the emitter's "I
cannot type this" read as "the engine has no type"), F-148 in full (a kind funnel that answers "cannot
say" for every non-primitive; an element type the emitter cannot name), F-155 (a declared `int?` that
accepted nothing), F-112's F-161 row (a body model of `dynamic` took the accepted-type gate's dynamic
exemption), F-147 (known-open — the reads `IsUntypedReceiver` governs).

**Enumerated in cycle 21, and it yielded a silent-wrong-output finding as predicted.** The emitter has
fourteen gates that consult a call-site value's type. Eleven put "cannot say" on the refusing side.
**Three do not:** `SlotValueAssignable`, `AcceptedTypeSatisfied`, and `IsUntypedReceiver`. Two of the
three are demonstrated divergent — F-148's F-172 row reaches `AcceptedTypeSatisfied`, and the
`@list`-body member read recorded under F-147 reaches the reads `IsUntypedReceiver` governs.
**`SlotValueAssignable`'s exemption remains unclosed, and is now twice-unclosed:** no divergence could
be reached through it, because on every shape tried `TryTypeCallSiteBody` refuses first — which is a
statement about the shapes tried, not a proof. Cycle 22 failed to reach one by probing as well.
**Two independent reviewers failing to construct a case is evidence that the closure needs an argument
from source — that `TryTypeCallSiteBody` refuses first for every shape, derived from its predicate —
rather than more shapes.** Probing here has now been tried twice and returned nothing both times.

The record's own lesson stands: *where two tiers must agree, every "cannot say" belongs on the refusing
side of the branch, and the way to keep that from costing the precompiled tier is to shrink the set of
things that cannot be said, not to widen what "cannot say" is allowed to mean.* F-148's F-172 row is
the shape of the answer — a third answer, "the engine has one and I cannot name it", which is neither
a type nor "cannot say".

**CLOSED in cycle 23, by the argument rather than by more probing (F-184).** `CallParameter` has four
positional shapes and the population is closed by one boolean
(`IsModelTypeParameter => ChainParameter == null && CSharpExpression == null && NativeExpression == null`).
`CallSiteValueType` had arms for three of the four. The missing one is the embedded-C# parameter — the one
shape `TryTypeCallSiteBody` does not refuse first, which is exactly why two cycles of probing could not reach
it, and it is the shape where the engine has the **most** definite answer it ever has (it hands the text to
Roslyn). Six divergent rows, spanning `SlotValueAssignable` **and** `AcceptedTypeSatisfied`.

**And cycle 24 found the residual that closure left.** F-184 enumerated the eight CONSUMERS of
`CallSiteValueType`'s answer and never asked whether the answer was FAITHFUL. It was not: the typer checked
only whether the expression's own symbol was an error type, so an expression that does not compile at all
returned "cannot say" — the exempting answer — and six shapes the engine refuses reached the consumer's build
as raw C# errors (F-191). **Enumerating who reads an answer is not the same as establishing that the answer is
right.**

**Two lessons, both already written here and both confirmed:** the fix shrank the set of things that cannot
be said rather than widening what "cannot say" may mean; and *an exemption that survives two rounds of
probing should be attacked by closing the population from source, not by probing a third time.* `FullCSharp`
being off by default is what kept the fourth shape out of every probe space.

### F — the fix that introduced the next defect

**Chains, with the merged homes named so they are still followable:**
F-055's own rows are a four-commit chain in one entry — F-056 → F-069 → F-072/F-073 → F-082, each
introducing the next, ending in a silent-wrong-output regression that the intermediate commit had made
*loud*. Then: F-023 → F-033 (both rows of F-023) and F-023 → F-034; F-005 → F-020 (both rows of
F-005); F-046's F-050 → F-067; F-057 → F-058 (F-046) and F-057 → F-071; F-099 → F-102; F-034's F-074
→ F-093; F-112's internal chain F-112 → F-117 → F-124 → F-130 → F-150 → F-156 → F-161 → F-170;
F-131's F-136 → F-142; F-145 → F-153 → F-158 (all one entry now); F-169 and F-171's F-174 row (both
introduced by the commit under review in cycle 21, alongside a fix that was measured sound).

**Consolidation note.** Merging made this class *easier* to read, not harder: a chain that used to be
four or eight separate entries linked by "notes" is now one entry whose rows are in order. **The
count did not change; the visibility did.**

**Cycle 23 added three more, and two of them are one commit:** F-177 → F-180, where a single fix traded a
severity-2 for a severity-3 **in opposite directions** — it dropped directives the C# needed, and it also
started rendering templates the engine refuses, because its stated premise ("the engine never consults a
`@using` body") was only true of documents with no embedded C#. F-178 → F-181, the same function gaining two
new defects: an unconditional `\`→`/` replacement, and a `.` that only ever dropped as a side effect of the
`..` walk. And F-084 → F-188, a slower variant worth naming separately: **a fix that WIDENED what the
consumer's compiler accepts left behind a refusal that existed only because it did not**, and nothing
re-derived that refusal for fifteen cycles.

**Cycle 24 makes it four consecutive cycles.** `ec613c2`'s new assembly-qualified arm required every
component of a stated assembly identity to match, on the stated premise that this "is what the CLR's own load
does with it" — and `Type.GetType` does not: for an already-loaded assembly it binds by simple name and
ignores version, culture and public key token (F-185's amendment, fixed as part of F-192's cycle). The same
commit also **broke one of this register's own regression checks** by renaming a test, so
`--filter FullyQualifiedName~AUnsignedLongMeetingAChar` matched nothing and exited 0 — the failure mode the consolidation
had just cleaned up twice.

**Enumerated?** Not a code class — a process property, and the strongest single signal in this
register. The record notes it four separate times; in cycle 10, **four of eight findings were
introduced by the commit under review**, and cycles 21, 22 and 23 each found defects introduced by the
commit under review — three consecutive cycles. Practical consequence for a reviewer: *review the previous
cycle's commit first*, and specifically the arms and guards it added rather than the defect it fixed. Second
consequence, for a fixer: **check each repair against the defect it REPLACES, not only against the defect it
closes.** Both of cycle 22's headline fixes would have been caught by that one question.

### G — tests that cannot fail

**Sub-patterns, with members as merged. This is the largest class in the register — now 27 ids.**

**Cycle 24 added three, and all three are the same shape: a guard nothing executes.** Deleting
`TemplateEmitter.cs:985`'s hand-rolled escape entirely reddened **0 of 554 + 0 of 939** (F-190). Cycle 23's
`ParseName` guard on a `@using` body reddened **0 of 11** (F-192) — which is why its `//`-comment face
survived a whole cycle. And `grep -rn "TemplatePathType" src/Heddle.Tests` showed only `TemplatePathType.None`
was ever exercised, so the hosted resolver's View/PartialView/Master arms — every one of them broken on Linux
and macOS — had **zero** coverage (F-195). **The mutation that reddens nothing is the cheapest instrument in
this series and the most consistently skipped.**
- *the test reads the production constant it is checking*: **F-046 in full** (rows F-046, F-050,
  F-058, F-067). Four bounds, four cycles.
- *a degrade-only assertion, which any blanket refusal satisfies*: **F-036 in full** (rows F-036,
  F-041, F-118, F-132).
- *a row answered by a different arm than the one it names*: **F-126 in full** (rows F-126, F-133,
  F-139, F-146) — the type-kind verdict table, rebuilt four times.
- *the fixture degrades for an unrelated reason, so the code under test never runs*: F-111's F-127
  row, F-091's F-103 row.
- *an exemption satisfied by any exception at all*: F-090 (`Assert.ThrowsAny<Exception>`, including
  the harness falling over), F-010's F-076 row.
- *the property was never pinned at all, and mutation proved it*: F-021, F-028, F-029, F-034's F-030
  row, F-036's F-041 row, F-025's F-044 row, F-060, F-034's F-088 row, **F-090 in full** (rows F-090,
  F-100, F-160), F-106's F-116 row, F-171's F-174 row (where two of the terms turned out to be
  undecidable rather than untested, and were deleted).

**Consolidation note: this class was under-counted more than any other.** It reads as four repeated
mistakes rather than twenty-four unrelated ones, and three of the four sub-patterns now have exactly
one entry each holding every instance. **If you are writing a test in this repository, those three
entries — F-036, F-046, F-126 — are the checklist.**

**Enumerated? No — but the instrument is known and works:** mutate one production arm at a time and
watch the suite. Cycles 12, 13 and 16 each ran a full mutation matrix over a verdict table (nineteen
arms, then seventeen/eighteen reddening, then a seven-kind matrix) and each found real dead rows.
Cycles 8, 9 and 18 ran it over production code generally (F-090). **Nothing runs that matrix
routinely.**

**Cycle 21 is the register's first commit where every arm it added is load-bearing:** all six were
mutated and all six reddened (14, 2, 7, 4, 2 and 4 tests). **And the very next key that commit wrote is
the counter-example.** The folded body key carried three terms; dropping the definition's name reddened
0 of 1431, dropping its declaration span reddened 0 of 1431, and the parse-context term alone as the
whole key reddened 0 of 1431. Cycle 22 resolved it the way F-171's F-174 row says to. The two terms are
redundant **by construction**: every path that hands two live `DefinitionItem`s one `ParseContext` — the
copy constructor, `OverrideWith`, and the region-fill materializer, which takes its name and its span
from the same candidate — carries the name and the span across with it, and the one path that does not,
`ParseContext.IsolateContext`, gives the copy a *new* context, which only splits further. So they were
deleted and the argument written down. The surviving term reddens 30+ tests. **A commit where every arm
reddens is a property of that commit, not of its author.**

### H — the instrument did not have a category for the defect

**Members, as merged:** F-017 (a green report reading stale golden bytes), F-077 in full (a harness
whose answer depended on test order, and the property that harness call was hiding — rows F-077,
F-094), F-081 (a declared TFM that runs nothing and exits 0), F-151 in full (bytes moved while still
precompiling — invisible to a degrade sweep *and* to a differential test whenever the engine refuses
the template; and newly precompiling — rows F-151, F-159), F-155 (the corpus was byte-identical and
closed nothing, because no fixture declared the feature under test).

**Enumerated? The sweep now has three counts** — newly degrading, bytes moved while still
precompiling, newly precompiling — and that is believed complete for *corpus-visible* change. It is not
complete for behaviour the corpus cannot see: **the corpus contained no `[DataType]`-declaring host
extension until cycle 18, and had no array or `Nullable<enum>` assignability row until cycle 19.** A
sweep over a corpus that lacks a shape proves nothing about that shape, and "the corpus is
byte-identical" has twice been reported as evidence when it was not.

**A third time, in cycle 23, and the clearest instance yet.** All four sweep counts came back **zero** —
newly degrading, bytes moved, newly precompiling, and the fourth count added that cycle (newly precompiling
*and* newly divergent). The corpus contains **none of the eight shapes that cycle changed**: no `FullCSharp`
template, no `@using` naming an unresolvable namespace, no import spelling carrying `.`, `..` or a backslash,
no nested-public export container, no inapplicable sole-overload call, no namespace-less or
assembly-qualified `@model`, and no overflowing constant. A zero sweep was therefore evidence that nothing
*else* moved and **no evidence at all about the cycle's own subject** — the evidence for that was a 58-row
per-shape matrix run against both tiers before and after. State which of the two a sweep count is; they are
not interchangeable.

**A fifth instrument defect, and the one with the longest reach: every baseline in this series was taken in
DEBUG.** At `cde2a29` the integration suite is 1016/1016 in Debug and **1015/1016 in Release** — and the failing
test passes ALONE in Release, so it needs both the configuration and the ordering. The same split is present at
the parent commit, so it is not new; it has simply never been visible. Roughly twenty-five cycles of "all suites
green" were therefore a Debug measurement reported as a suite measurement. `Heddle.Tests` has had a Release leg
for some time; the generator suites have not. Recorded as F-201.

**A fourth instrument defect, added here because it cost this consolidation a false alarm:** running
two `dotnet test` processes over this solution **concurrently** can fail every row of an unrelated
suite. The suites share process-global registration state and write probe assemblies beside the test
binaries (F-108). `ConstantArithmeticDifferentialTests` reported 99 of 99 failing under a concurrent
run and 99 of 99 passing immediately afterwards, alone. **Run the register's regression checks
serially, and do not believe a red from a parallel run without repeating it alone.**

### I — an engine or CLR relation re-implemented instead of asked

**Members, as merged:** F-112's F-117 row (a rule guessed at rather than measured), F-112's F-130 row
(typing mirrored from the shared operator tables — the *right* version of this), F-148 (a lossy
descriptor used as a type answer), F-138's F-149 row (`Type.IsAssignableFrom` written out as four
cases), **F-155 in full** (the same, with a doc comment asserting fidelity; and the adapter's own CLR
corrections wrong in both directions — rows F-155, F-162, F-163).

**Enumerated? The single-source rule is now in place** (one adapter answers assignability, driven
from a corpus generated from the live CLR on the reflection side), but the adapter's corrections have
been wrong twice in two cycles, in opposite directions. The corpus *is* the enumeration; check that any
new relation family has rows in it before believing a sweep.
The stated rule: *when a rule mirrors an engine predicate, prefer calling the one adapter that already
answers it over restating it; when restating is unavoidable, write down the predicate's source
expression rather than a prose enumeration of the cases someone thought of.*

### J — shared mutable state, publication and lifetime (engine side)

**Members, as merged:** F-001, F-005 (both rows), F-034 (all five rows), F-043 (all three rows),
F-064, F-085, F-086, F-087 (both rows).

**Enumerated? No.** Two orderings in this area are explicitly unpinnable (recorded in
`AssemblyRegistrationTests` and `PreparseCacheGenerationTests`), and the answer taken was to remove the
reversible orderings rather than to test them. A racing test here passes by luck when the code is
wrong — do not add one.

### K — unbounded input

**Members, as merged:** F-025 (import cycles and chain length — rows F-025, F-037), F-026 (parse
depth, all six rows), F-038 (lexer mode stack), F-057 (import fan-out, both rows).

**Enumerated? The three dimensions are known** — depth, fan-out, cycle — and each is bounded and
reported with a diagnostic. F-027 is the one residual and is not reachable from a bound. When adding a
recursive walk, the question already has an answer: bound it by count, **measure the bound in Release
on a 1 MB stack**, and assert the value (not the constant — F-046).

### L — documentation and gate defects

**Members, as merged:** F-006 (the four gates plus the rendered-table row), F-007 (both rows), F-009
(both rows), F-010 (all three rows), F-015, F-016, F-026's F-045 row, F-068.

**Enumerated?** Gates now exist for links and citations, diagnostic-id presence, option names and
defaults, and public-API mentions, and each was demonstrated red on its first run. Two documentation
classes have no gate and recur: *a description that is complete for some of a diagnostic's causes*
(F-009, both rows), and *a prose claim of completeness over a list* (F-015, F-155's doc comment).

---

## Known-open register

Do not re-report these. Each is deferred with a stated reason; if you intend to close one, read its
entry first, because several were deliberately not fixed rather than missed.

| id | one line | severity | why deferred |
| --- | --- | --- | --- |
| F-027 | prefix-operator runs of several thousand exhaust ANTLR's own lookahead | 3 | upstream (antlr/antlr4#744); no fixed count can see it, and neither a listener nor the grammar can reach it. Published numbers are indicative, not contractual |
| F-079 | `Path.Combine` rejects characters on .NET Framework that .NET Core accepts | 3 | the development box cannot make it throw; the mitigation in place is reasoning, not evidence |
| F-080 | on `netstandard2.0` an assembly with no file yields no metadata reference | 3 | there is no API to fix it with (`TryGetRawMetadata` does not exist there); no `netstandard2.0` path executes on this box at all |
| F-140 | two type-kind verdict rows that cannot be honestly pinned (`Structure`, `Extension`) | 6 | `Structure` is a Roslyn alias no change here can move; `Extension` is not declared by the Roslyn the generator compiles against, so a case for it is `CS0117` |
| F-147 | a native expression reading the element's own member inside an `@list` body degrades, and a plain path to a member the element lacks throws at render where the engine refuses | 4 and 3 | typing the writer off `DynamicBodyModel` is a change of a different shape; left for a later cycle. Cycle 21 measured the severity-3 face and raised the entry's value |
| F-154 | three caller-content shapes under a `:: dynamic` callee degrade where the engine renders (a native expression, a function call, an `@if`) | 4 | reported rather than hidden; not attempted |
| F-166 | a `bool`/`bool?` bitwise operand pair has no `HED1008` of its own | 3 | both tiers agree, so it is a matched defect needing a ruling rather than an edit. Partially closed: it now carries `HED0005` with a real message and position |
| F-167 | `floor(3)` / `ceil(3)` / `round`-on-`int` are `HED1013` | 3 | the fix (C# betterness in the runtime binder) is a filed next-window candidate; it widens accepted behaviour |
| F-168 | a shipped sample still uses removed MSBuild item metadata | off-scale | escalated to the owning effort; the sample silently loses its intended key |
| F-178 (open half) | an import spelled `/lib.heddle`, `~/lib.heddle` or `lib` precompiles and renders where the engine refuses all three (`HED4009`+`HED1001`) | 3 | closing it means refusing spellings the documentation teaches — `@partial(){{child}}` already spells a template without its extension, and `~/` is a documented host idiom in `TemplateKey`'s own contract — so the fix would take working precompiled templates off the tier to match a refusal. Measured over 13 spellings; the `..` direction, which broke the build over a template the engine renders, is FIXED |
| F-186 | an argument the operand estimator cannot type still binds a sole exported overload, and the emitted call is `error CS1503` ×2 | 2 | the cure is not the shortcut. Deleting it degrades every `f(this)` and every `f(ModelMember)` — a broad severity-4 across ordinary host functions — and sends a single `params` overload down the expanded tier this writer does not emit. The real cure is to stop the estimator being lossy at this seam: `ExportFunctionBinder` is handed `OperandKind` where the emitter already holds an `ITypeSymbol` for a resolved member path (`ComputedValueType` / `ResolvedTypeOf`). Handing it symbols shrinks "cannot say" instead of widening what it may mean, which is what class E prescribes — and it is a change of a different shape from cycle 23's. **Measured:** `@model(){{…Order}}` + `@(rokstr(Total))` over a `Money` struct with a sole `ROkStr(int)` — engine `HED1012`, generator `CS1503` ×2; the control `@(rokstr(Count))` renders `os3` on both |
| F-201 | a `ref struct` model throws a raw `InvalidCastException` where the test asserts a `TemplateProcessingException` — **in Release only, and only in a full-suite run** | 6 for the pin, 3 for the behaviour underneath | measured at `cde2a29` and at its parent `672ab71`, so it is not this cycle's: Debug 1016/1016 green, Release 1015/1016; the same test passes ALONE in Release. `ModelParameter.GetParameter` casts a `string` to `ReadOnlySpan<char>` through a compiled lambda and the raw `InvalidCastException` escapes instead of being wrapped. Order-dependent, so something earlier in the suite changes the path taken. **Not fixed because the instrument finding matters more than the row** — see class H: every baseline in this series was taken in Debug for this suite, so a Release-only failure was invisible to ~25 cycles of verification |
| F-198 | the embedded-C# probe compiles INSIDE the consumer's compilation, so consumer internals are visible to it where the engine's standalone compile cannot see them | 3 | `CSharpExpressionTyper` adds its probe tree via `_compilation.AddSyntaxTrees`; `CSharpContext.Preparse` builds a fresh `CSharpCompilation.Create(null, {tree}, refs)` in which the consumer is a metadata reference. An expression naming a consumer `internal` therefore compiles for the build tier and would not for the engine. **Unmeasured** — `DifferentialHarness` seeds no consumer source, so the shape cannot be constructed there — and not fixable by adding a standalone compilation, because the model type lives in the consumer's SOURCE, not its references. Recorded in the source at the probe rather than inherited silently. Opened by F-191's fix **Amended cycle 25: the reasoning is now MEASURED, not argued.** Rebuilding the typer's probe as a standalone `CSharpCompilation.Create` over `_compilation.References` — the construction that would close this gap — makes a model type declared in the consumer's SOURCE invisible, so every expression over it stops compiling and the template silently leaves the precompiled tier. `Heddle.Generator.IntegrationTests` stays green under that mutation for exactly the reason recorded here (`DifferentialHarness` seeds no consumer source); `ConsumerParseOptionsTests` catches it, and is the first test in the repository that can observe the constraint. |
| F-199 | a hosted `GetTemplate` cannot load the file its own search just found | 3 | `TemplateResolver.GetTemplate`'s hosted arms build `TemplateOptions(Path.GetFileNameWithoutExtension(path))` with `RootPath = _rootPath`, so `FullPath` composes `<root>/<filename>` rather than the path the search returned. Found while fixing F-195 and deliberately not fixed with it: the answer turns on what `TemplateName` and `RootPath` mean for a hosted view, and changing them moves the options fingerprint `PrecompiledGauntlet` compares. Needs a ruling, not an edit |
| F-179 | an **import-only** library file is compiled standalone, so an error it only ever raises in isolation becomes a build error (`HED7012`) | 3 | **not drift** — both tiers refuse the file when it is compiled on its own, so there is nothing to diverge. It is a trap because the engine never compiles that file standalone in production: it only ever reaches the compiler expanded into an importer. The documented opt-out is `Precompile="false"` on the `<HeddleTemplate>` item, which keeps the file in the import map and out of the standalone pass. Recorded so a future cycle does not report it as a divergence |

**Also open, and recorded inside their entries rather than as separate findings:**

- **Two `AssemblyHelper` orderings cannot be pinned** (F-090's F-100 row, F-034's F-093 row). They are
  claims about what no concurrent caller can observe; a racing test passes by luck when the code is
  wrong. The mitigation was to remove the reversible orderings, not to test them. Written in plain
  words in `AssemblyRegistrationTests` and `PreparseCacheGenerationTests`.
- **The no-load pin cannot catch a one-shot startup walk** (F-021), which has already run before the
  probe can be built. Catching it needs a child process, which no suite has.
- **`SlotValueAssignable`'s "cannot say" exemption is unclosed** (class E). Two independent reviewers
  failed to construct a divergence through it. The closure needs an argument from source, not more
  probing.
- **The export argument cast has no gate** (`Binding/ExportFunctionBinder.cs:133`, class C). Safe today
  only because of the argument estimator's range; widening the estimator re-opens it silently.

### Fixed, but the pin is weak — candidates for the next cycle

These are recorded as FIXED and their tests **pass against a build in which the fix is reverted or
degraded**. Each is a place where a regression would be invisible.

| id | what the pin actually holds | why it is weak |
| --- | --- | --- |
| F-001 (registry half) | that reading registered names while registering does not throw | the commit itself says the registry fix "did not reproduce under a pre-fix mutation in 600 compiles, so its test is a guard, not a demonstration" |
| F-023 (F-023 row) | that a repeated failing expression reports the same diagnostics | removing the replay leaves the suite green; the test says so |
| F-023 (F-033 row) | that a repeated failing expression is reported at each caller's own position | replaying a fixed position leaves the suite green, because two documents sharing an expression do not reliably share a cache entry |
| F-043 (F-070 row) | that a parse begun inside an import reader is independent of the outer parse | removing the isolation leaves it green — the fixture cannot reach the nesting that reproduced the defect. The review reproduced it from a deeper outer parse; the fixture does not |
| F-002 (both rows), F-039 | nothing | fixed with no test at all |
| F-026 (F-032 row) | nothing | **found by this consolidation.** The entry named `DeepNestingGeneratorTests.ADeepTemplateThatAlsoHasASyntaxErrorIsStillBounded`; that test does not exist, and the check that ran it matched zero tests and exited 0. The fix — `EnsureTreeWithinLimit` placed before the error return in `DocumentParser.cs` — is in place and executed by nothing. It was a severity-3 process death (exit 134, 52,309 frames) and it mattered most in the editor, where a document has a syntax error most of the time |
| F-038 | nothing | **found by this consolidation.** The entry named `DeepNestingTests.UnbalancedClosersAreReportedRatherThanThrown`; that test does not exist. The guard is the `catch (InvalidOperationException)` in `DocumentParser.cs` and nothing executes it. A one-line test closes it: parse `@(1)}}`, assert one `SyntaxError` rather than an escaping exception |
| F-028 | not recorded | the fix reverted green when cycle 3 checked it, and no later commit is recorded as pinning it |
| F-145 (F-145 / F-153 rows) | n/a | a guard added on suspicion with no demonstrated red — later measured and **removed** (F-158). Recorded here as the precedent: **an unmeasured guard is a cost, not a safety margin** |

---

## Retired entries

One-off resolved defects whose full entry no longer earns its space: the fix is in place, a live test
pins it, the mechanism cannot recur, and no defect class claims it. **The id stays resolvable here.**
Anything severity 1 or 2, anything in a named class, anything that was ever a regression, and anything
whose pin is missing or weak was kept in full instead — which is why this table is short.

| id | what it was | status | fixing commit | pinned by |
| --- | --- | --- | --- | --- |
| F-115 | an unmeasured micro-optimisation — marking a registered assembly as already classified to save a `GetName()` per observation pass. It came in alongside a cycle's fixes, correctly guarded and honestly flagged as untested, and was **removed** in the same cycle: nobody had measured the cost it addressed, and the file it touched had had three defects that session. The standing rule it establishes: *the same discipline that left `ThreadLocal` alone after measuring (F-109) forbids changing something without measuring* | SUPERSEDED (reverted) | `4f299ce` | n/a — the code is gone |
