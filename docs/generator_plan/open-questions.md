# Generator code-sharing plan — open-questions register

The consolidated Q&A register for the six phases. Numbering is `Q<phase>.<n>`, matching each
phase plan's own Open-questions section. Every question carries the owning plan's
**recommended answer as a provisional default** — provisional defaults are not rulings; a
phase whose question is unresolved is not at DoR for the affected work items (fix-first
groups are unaffected unless noted). **Status: all questions open.**

Q3.1 and Q4.1 are the same question recorded by both owners; a single ruling resolves both.

## Phase 0 — test-fallback-guardrails

- **Q0.1 — Should feature-area suites eventually convert to resolver-path rendering
  wholesale, or does the corpus sweep remain the permanent posture carrier?** The gauntlet's
  verdict is per-template, so per-assertion conversion adds registry churn and suite time
  without new coverage ([phase 0 D4](phase-0-test-fallback-guardrails.md)).
  *Recommendation:* the corpus sweep suffices; feature suites keep direct-invoke isolation
  and contribute their templates to the corpus; revisit only if a feature area's templates
  cannot be expressed as corpus entries.

## Phase 1 — template-emitter

- **Q1.1 — Should `maxRecursionCount` join the precompiled options fingerprint?**
  Today the build-baked value wins on the precompiled tier while the dynamic tier reads
  `TemplateOptions`, giving divergent deep-recursion *error* behavior between tiers
  ([01 F19](../research/generator-code-sharing/01-template-emitter.md)). Owner: the
  precompilation spec. *Recommendation:* record the divergence as intentional (D23 posture)
  or add the field to the fingerprint at the next schema bump — decide once, in the spec.
- **Q1.2 — Scheduling of the planned non-string coercion-rail change.** The
  `OutExtension.RenderData` comment marks the `as string ?? string.Empty` drop as slated to
  change; that is a byte-changing runtime change with a generated-code twin. *Recommendation:*
  file as a breaking-windows candidate; phase 1's WI12 pins the current rail with
  differential tests until ruled.
- **Q1.3 — Should the runtime surface a diagnostic on dangling region-fill candidates?**
  Runtime silently skips (`continue`), generator refuses to precompile — intentional today
  but invisible to users. Would claim an `HED5xxx` ID if ratified. *Recommendation:* defer to
  the region-fill spec; keep behavior split documented until then.
- **Q1.4 — Tighten the shared participant scan's over-provision on shadowed
  `[ScopeChannel]` names?** The shared scan can over-provision a locals frame when a
  definition shadows a participant name (safe direction). *Recommendation:* keep the
  over-provision; revisit if the named trigger (a `definitionExists`-aware scan consumer)
  materializes.

## Phase 2 — document-shaper

- **Q2.1 — Empty-default-chain asymmetry: which side's behavior is intended?** The shaper
  skips empty default chains; the runtime adds a zero-length element that influences
  `OptimizeCallTree` strategy selection (bytes unaffected)
  ([02 minor](../research/generator-code-sharing/02-document-shaper.md)). *Recommendation:*
  leave + document + characterization pin; revisit on benchmark evidence plus a maintainer
  ruling.
- **Q2.2 — Ownership of a degrade-visibility diagnostic for the generator's blanket
  `catch (Exception)`.** Emitter defects currently degrade to the dynamic path with no
  signal. *Recommendation:* assign to phase 6 (diagnostics) rather than claiming an ID here;
  phase 2's clamp fix removes the known thrower either way.

## Phase 3 — binding-layer

- **Q3.1 — Member-visibility policy** *(same question as Q4.1; joint ruling).* Should
  `protected internal` getters, inherited non-public properties, and base-interface members
  be template-visible? Generator currently says yes, runtime says no — the runtime's answer
  is the documented sandbox. *Recommendation:* runtime observable behavior is normative (the
  narrower sandbox wins); the generator tightens; any widening goes to the breaking-windows
  next-window register.
- **Q3.2 — `[ExportFunctions]` precedence: first-container-wins or merge?** Runtime merges
  overloads across containers; generator binds the first container exclusively.
  *Recommendation:* merge, per runtime semantics — manifest bookkeeping now; multi-container
  emission degrades until phase 4's rank core lands.
- **Q3.3 — `[ExtensionReplace]` support in the generator.** No generator counterpart today.
  *Recommendation:* adopt the full runtime precedence in the shared rule-core rather than
  refuse-to-precompile.
- **Q3.4 — Add a prop-layout manifest row + gauntlet check?** Prop-slot layout drift is the
  only silent-wrong-output surface with no gauntlet coverage. *Recommendation:* yes —
  additive schema row, coordinated with phase 5's `PrecompiledSchema` constants.
- **Q3.5 — Model type-name ambiguity and implicit namespaces.** The generator's implicit
  `System`/`System.Collections.Generic` fallback can bind a different type than the
  runtime's all-assemblies scan (which throws on ambiguity). *Recommendation:* the generator
  drops the implicit-namespace fallback and never binds a name the runtime would call
  ambiguous — degrade to dynamic instead.
- **Q3.6 — Ineligible export container: silent skip vs diagnostic.** Runtime throws;
  generator silently skips, masking a host configuration error until first render.
  *Recommendation:* new generator warning; ID claimed from the registry at spec time (the
  plan's `HED7018` literal is a placeholder — see the README's ID-serialization note).

## Phase 4 — expression-writers

- **Q4.1 — Member-visibility policy** *(same as Q3.1; joint ruling before either phase
  adopts the shared `MemberVisibility` core).* *Recommendation:* runtime behavior normative;
  error-shape fixes (statics → positioned not-found; shadowing → deterministic most-derived)
  in scope now; widening filed to the breaking-windows register.
- **Q4.2 — Overload-selection semantics: keep Heddle's flat Pareto rank or move toward C#
  betterness?** *Recommendation:* keep the flat rank (the docs promise only
  exact > widening > boxing); the generator conforms via degrade-on-ambiguity plus
  cast-pinned emission; C# betterness would be a runtime behavior change → breaking-windows
  candidate.
- **Q4.3 — Dynamic-binder context for generated dynamic hops.** Generated `(dynamic)` binds
  in the consumer assembly; the runtime binds in Heddle's context — visibility of `internal`
  members differs. *Recommendation:* `PrecompiledRuntime.DynamicMember` reproduces the
  runtime's Heddle-context binding for cross-tier parity; routing is schema-gated per the
  plan's D11; the typed-vs-dynamic visibility asymmetry is filed as a spec-clarification
  candidate.

## Phase 5 — pipeline-config

- **Q5.1 — `Precompile`/`Name` item metadata: wire or remove?** Declared in props/targets,
  never read ([05 F8](../research/generator-code-sharing/05-pipeline-config.md)).
  *Recommendation:* wire `Precompile` (it expresses "stay in the import map but emit no
  entry point," which `Remove` cannot — verified `Remove` breaks imports via HED7011);
  remove `Name`.
- **Q5.2 — Should the `View`/`PartialView`/`Master` resolver arms consult the precompiled
  registry?** Today only the `None` arm does, so MVC-style lookups bypass precompilation
  entirely. *Recommendation:* not in this phase — a later additive item with its own
  decision record (search-order/precedence design needed).

## Phase 6 — diagnostics-utilities

- **Q6.1 — Forwarded-warning real-ID change: fix or next-window candidate?** Surfacing
  runtime warning IDs at build time changes what `NoWarn`/suppressions match (stale
  `NoWarn HED7013` + `TreatWarningsAsErrors` is the edge). *Recommendation:* ship as a fix
  with a migration-note line; not window-gated.
- **Q6.2 — LSP default output profile: align to `Html`?** The LSP's `WorkspaceConfig`
  defaults to `Text` where the engine/generator default is `Html`. *Recommendation:* align
  to `Html`; `"outputProfile": "text"` remains the opt-out.
- **Q6.3 — Diagnostic-catalog `MessageFormat` end-state.** How far the catalog's message
  templates extend beyond the rows the generator consumes. *Recommendation:* consumed rows
  only for now (HED7xxx + the F2 twins); revisit when a new diagnostic area ships or
  intra-runtime message drift is observed.
