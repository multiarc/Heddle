# Findings register

Short by design. It lists only: mistake patterns that recur, code that looks wrong but is not,
open items, and fixes whose tests would not catch a regression. One-off fixed bugs are not listed —
the test suite is their record. **A finding becomes a test, not a row here** — see
[`review-protocol.md`](review-protocol.md) § Findings land as tests; rows exist only for what no
test can hold. (The register began as the generator review series' register; it is now the
repository's standing one.)

- Full history (179 ids, every RCA, repro and commit): `git show fac16a1:docs/generator_plan/findings-register.md`,
  and the per-cycle prose in `git show c4691266:docs/generator_plan/phase-8-docs-sweep.md`.
  Look up any `F-xxx` there.
- Code and tests must never cite this register. No finding ids in comments or test names.
- Severity scale: `1` silent wrong output · `2` breaks consumer build · `3` error on one tier only ·
  `4` silent loss of the precompiled tier · `5` leaks · `6` tests that cannot fail.

## Recurring mistakes

Each of these was found many times across cycles. Before reporting a new finding, check whether it
is a member of one of these; if it is, enumerate the class from source before fixing one instance.

**A. A string used as an identity.** A display name, raw directive text, or path text used as a key
or comparison where the engine compares a resolved thing (a symbol, a canonical disk path). Hit ~15
times. When adding any key or `==` over a name or path, ask what the engine compares in the same
position. Import identity: `grep -rn "ImportIdentifier"`.

**B. A context built without state its readers need.** A `BodyContext` construction site missing the
prop layout, slot type, or model. Enumerated (site × field grid, cycle 21). When adding a site or a
field, redo the grid row.

**C. Generated code spells a name the consumer's assembly cannot use.** Everything reaching `.g.cs`
must pass `ClassifyTypeName`/`CanWriteTypeName` — types, type arguments, containers of exported
calls, `@using` text. Sinks are `CodeWriter.Line`/`Raw`, `_fieldDecls`, `_methodDecls`, the manifest
builder — all in `Emit/TemplateEmitter.cs` and `Emit/PieceWriter.cs`. The last open gap — the export
argument cast — closed when `TryComputeCasts` grew the classifier check, pinned by
`ExportArgumentTypingTests.AParameterTypeGeneratedCodeMayNotNameDegradesRatherThanBreakingTheConsumersBuild`.

**D. A rule implemented only for the case in front of the author.** Sibling sites keep the bug.
Either enumerate every site, or read the authority (the attribute, the declared type, the adapter)
so no site list is needed. Sub-rule: never re-implement a grammar with
`Split`/`Replace`/`IndexOf`/regex over text — ask the owner (`Path.*`, `SyntaxFactory.ParseName`,
`AssemblyIdentity.TryParseDisplayName`, `CSharpEscape`), or mirror the engine's exact code path and
say which.

**E. "Cannot say" treated as the exempting answer.** Where the two tiers must agree, "cannot say"
must land on the refusing side of a gate. Gates that exempt it: `SlotValueAssignable`,
`AcceptedTypeSatisfied`, `IsUntypedReceiver`. If two rounds of probing cannot reach a divergence,
close the population from source instead of probing again.

**F. The fix introduces the next defect.** Happened in at least four consecutive cycles. Review the
previous cycle's commit first — especially the arms and guards it added. Check a repair against the
defect it replaces, not only the defect it closes.

**G. Tests that cannot fail.** The largest class (27 ids). Before trusting a new test, mutate the
production arm it names and watch it redden — a mutation that reddens nothing is a finding. Known
shapes:
- the test reads the production constant it checks
- a degrade-only assertion (a blanket refusal passes it)
- a verdict row answered by a different arm than the one it names
- `Assert.ThrowsAny<Exception>` (the harness falling over passes it)
- a `--filter` that matches nothing exits 0
- xUnit silently drops a theory row whose arguments duplicate another row's — the count drops,
  `Skipped:` stays 0. A theory row is not a test until the count says so.

**H. The instrument cannot see the defect.** Rules that keep measurements honest:
- A zero corpus-sweep count proves nothing about shapes the corpus lacks. Say whether your zero
  means "nothing else moved" or "the subject was measured" — they are different claims.
- Run regression checks serially. Two concurrent `dotnet test` runs over this solution can fail
  every row of an unrelated suite.
- Baselines were Debug-only for ~25 cycles. Run the generator suites in Release too (F-201 is the
  standing example of what Debug hides).
- Never pass `-f net8.0` to `src/Heddle.LanguageServices.Tests` or `src/Heddle.Tool.Tests`
  (net10.0-only; it exits `NETSDK1005` with zero tests run).

**I. An engine or CLR relation re-implemented instead of asked.** Call the one adapter that answers
it. If restating is unavoidable, copy the predicate's source expression, not a prose list of cases.

**J. Shared mutable state (engine side).** Not enumerated. Two orderings are unpinnable by design —
do not add racing tests; remove the reversible ordering instead.

**K. Unbounded input.** Any recursive walk: bound it by count, measure the bound in Release on a
1 MB stack, and assert the value, not the constant.

**L. Documentation.** Two shapes recur and have no gate: a diagnostic described by only some of its
causes, and a prose claim of completeness over a list.

## Do not "fix" these — they are correct

Each was reported as a defect at least once, then measured. Re-deriving these by intuition is how
they get broken.

- The emitter's `def.ModelType == "dynamic"` string compare. The engine compares the same text
  (`HeddleCompiler.cs:585`). "Resolving" it would create the divergence.
- A backslash in a `#line` file name. `pp_string` processes no escapes; escaping it would be the bug.
- `ThreadLocal<int>` per definition. Measured: ids recycle via the finalizer; no template workload
  grows the slot array.
- `@out(::X)` never type-checked. Unreachable: `BuildParamExpr` refuses every root-reference call
  parameter first.
- The export guard's container arm. Unreachable today, kept deliberately so both spelled names are
  guarded alike. Rule for unreachable arms: keep and label it if it pairs with a live arm; delete it
  if it reads as a member of a list.
- The two `_seenInaccessibleTypes` key shapes sharing one set. Disjoint by construction (argued in a
  code comment, with its tuple-type caveat).
- A repro whose fixture cannot compile is not a defect. Check the repro compiles before accepting
  its reasoning.
- The probe loader refusing `bin`/`obj` and every project reference. It reads as over-caution and is
  not: `LoadFrom` holds the file for the compiler server's life, so loading a build output breaks the
  *next* build. It is also what makes the IDE and the CLI agree. An extension declared in the
  compilation being built is unprobeable for the same reason and degrades — that is the answer, not a
  gap.

## Open — do not re-report, do re-measure

If a check passes or a stated reason tests false, the entry is wrong — report that; it is a real
finding. Six former known-opens died exactly that way.

| id | one line | severity | check / status |
| --- | --- | --- | --- |
| F-027 | runs of thousands of prefix operators exhaust ANTLR's own lookahead | 3 | upstream (antlr/antlr4#744); unreachable by any bound of ours |
| F-079 | `Path.Combine` rejects characters on .NET Framework that .NET Core accepts | 3 | degrade pinned on the net48 CI leg: `NetFrameworkDegradePathTests.AnImportPathWithCharactersTheFrameworkRejectsStillParses` asserts the framework's throw and the parse surviving it |
| F-080 | on `netstandard2.0` an assembly with no file yields no metadata reference | 3 | no API exists there to fix it; pinned both ways by `NetFrameworkDegradePathTests.AnAssemblyWithNoFileYieldsAReferenceOnlyWhereTheRuntimeExposesItsMetadata` — empty on net48, served on modern TFMs |
| F-140 | two type-kind verdict rows cannot be honestly pinned (`Structure`, `Extension`) | 6 | `grep -n 'Microsoft.CodeAnalysis.CSharp' src/Heddle.Generator/Heddle.Generator.csproj` — re-test if the version moves past 4.x |
| F-198 | embedded C# binds inside the consumer's compilation — the probe's question, and now the emitted fragment's own binding — so a consumer-`internal` member resolves where the engine's separate generated assembly raises `RuntimeBinderException`. The generator is *more permissive* on that corner, never less | 3 | measured; `ConsumerParseOptionsTests` is the one test that observes the constraint. Model-path member reads are exempt by construction (`PrecompiledRuntime.DynamicMember` pins its binder context to Heddle's assembly) |
| Q8.8 | the `ToString("R")` drift fix is formally unclosable without a Windows `net48` run — the 23 green cases are a revert-detector, not proof of sufficiency | 3 | needs a Windows `net48` leg; nothing on a Linux box can close it |
| Q8.13 | the value-path coercion rail (a boxed non-string drops to empty on the value path, stringifies on the render path) is pinned only as emitted shape — ruled "implement a byte-level fixture on both tiers", not implemented | 6 | build the host extension whose `ProcessData` consumes its body's `Execute` result and returns a non-string; today's tripwire is `StrategyShapeDifferentialTests`' `strategy-nonstring-value` |
| Q8.15 | intermittent full-solution failures reproduced only in a shared working tree (a concurrent MSBuild `IncrementalClean` rewriting `bin/**` under running test hosts), never in isolation — the two originally named tests never reproduced | 6 | before blaming a test, re-run 25× from a `git archive HEAD` copy; a retry attribute is not an acceptable resolution |

Also open, without their own ids:

- Two `AssemblyHelper` orderings cannot be pinned; a racing test passes by luck. Stated in
  `AssemblyRegistrationTests` and `PreparseCacheGenerationTests`.
- The no-load pin cannot catch a one-shot startup walk; catching it needs a child process comparing
  the loaded set before and after first touch (the future-work shape; no suite spawns one today).
- The hook probe observes body typing, chained typing and the zero-output answer, and **nothing
  else**. A hook that mutates compile state — `ScopeType`, the import set, the output profile — is
  outside the protocol and no check looks for one. What makes that safe today is a property of the
  population, not a guard: every compile-state-mutating hook is an engine directive with dedicated
  emitter handling. A third-party hook that mutated compile state would be emitted for what it
  typed and silently not for what it changed. Closing it means a protocol that observes the scope
  after the hook runs, which nothing does yet.

## Unverified platform surface (recorded 2026-07-29)

Everything here is **unmeasured, not suspected-broken**. The development box is Linux with
.NET 8/10 only — no Windows, no .NET Framework, no Mono, no .NET 6. "Verified" claims from the
review series mean "verified where we could look".

- **A declared TFM can run zero tests and still exit 0.** `src/Heddle.Tests` declares
  `net6.0;net8.0;net10.0`; on a box without the .NET 6 runtime the net6.0 test host fails to start,
  `dotnet test` reports `Passed!` for the other two and exits 0. Any CI image missing a declared
  runtime silently drops that framework's coverage while staying green. **The highest-value unfixed
  item on this surface: fail the build when a declared TFM's test host cannot start** (a
  build-wiring change, not an engine change).
- **No `netstandard2.0`-flavoured code path has ever executed.** Four sites differ on .NET
  Framework by design, exercised nowhere: `RoslynReferenceProvider.FromLoadedImage` returns `null`
  (no `TryGetRawMetadata`, so byte-loaded model assemblies stay invisible to the C# tier there);
  `AssemblyHelper.IsObservable` compiles out the `AssemblyLoadContext` exclusion;
  `AssemblyHelper.MarkEngineEmitted` uses `lock` + `TryGetValue`/`Add` instead of `AddOrUpdate`;
  `Models/Range.cs` compiles out the `System.Range` interop.
- **The `Path.Combine` guard in import identity is reasoning, not evidence.** .NET Framework
  rejects `<`, `>`, `|` where .NET Core accepts them; the combine was moved inside the guard so a
  Framework throw degrades the cache key instead of killing the parse, but no available platform
  can make the combine throw. (The `GetFullPath` half *is* exercised — the NUL case reaches it on
  net8.)
- **`ParseDepthGuard.MaxDepth = 250` was measured on one host** (1 MB Linux thread, Release:
  last-safe depths 284–574 by shape). Whether a Windows, .NET Framework, or IIS request thread
  overflows before 250 rule levels is unknown; verifying the ANTLR-recursive shapes needs a
  child-process exit-code harness no suite has (the flat shape is covered by
  `DeepNestingTests.AFlatRunPastTheLimitIsReported`).
- **Sample goldens are a Linux/net8.0 artifact** (line endings pinned by `.gitattributes`). Culture
  was measured and the tiers do not diverge — both reach `value.ToString()` the same way; the
  sensitive path is the bare `@(expr)` numeric render, not the formatting extensions (those use
  `CultureInfo.InvariantCulture`). The residue is a suite defect: engine goldens hard-code
  invariant strings and fail under a non-invariant culture; those assertions should be made
  culture-explicit.

What would close this, in value order: (1) fail the build on a non-starting declared TFM;
(2) a Windows CI leg running `net48` + `net6.0` + `net8.0` + `net10.0`; (3) a per-platform
child-process depth harness.

## Weak pins — a regression here is invisible

None at present. A fix whose test cannot catch its regression does not stay here — write the pin,
or move the item to Open with the reason no pin can exist (`review-protocol.md` § Findings land as
tests). Every row this section held is now a mutation-rehearsed test; two rows turned out stale
(their named tests already existed and rehearsed red).
