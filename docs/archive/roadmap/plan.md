# Heddle Evolution — Phased Roadmap Documents

## Context

The re-assessment (docs/assessment.md) concluded Heddle's core thesis is sound but four debts limit it: Roslyn-welded expressions, raw-by-default output, missing branching ergonomics, and tooling. The user has chosen the evolution order and constraints, and wants **one independent plan per phase, each with clear success criteria and validation scenarios**, delivered as markdown files under `docs/roadmap/`.

User decisions (fixed):
1. **Phase 1 first: native expression language** — sandbox-capable, Roslyn-independent. Surface = **bare expressions in call parens** extending today's sigil-less member paths (`@if(Count > 0)`, `@(Price * Quantity)`); the inner-`@` form stays full C#/Roslyn. Capability scope: properties/indexers/operators/literals + **registered whitelisted functions**; no arbitrary method calls.
2. **Phase 2: safer output default** (output profiles; Html profile encodes unnamed `@(...)`).
3. **Phase 3: `else`/`elif` as ordinary Extension types — NOT keywords, NOT chained**: standalone `@elif`/`@else` blocks coordinate with a preceding `@if` through a **published local context** (each item publishes something the next extension can pick up on); they are optional, text between the branch blocks renders normally, and the design stays declarative — no imperative if/else statement structure is added to the language.
4. Remaining phases prioritized by user value (my ordering, justified in the index): quick ergonomics wins → named props/slots → LSP tooling → build-time/AOT backend → streaming/async API.

## Deliverable

Create `docs/roadmap/` with an index + 8 phase documents. Each phase doc is **independent** (readable/executable without the others) and contains: Goal, Design, File-level changes, Back-compat analysis, Risks, **Success criteria** (measurable), **Validation scenarios** (concrete templates + expected outcomes, incl. negative/security tests), and Size estimates (S/M/L).

Files to create:
- `docs/roadmap/README.md` — index: phase list, ordering rationale, dependency notes (all phases independently shippable; 3 benefits from 1 only ergonomically; 7 depends on 1).
- `docs/roadmap/phase-1-native-expressions.md`
- `docs/roadmap/phase-2-safe-output.md`
- `docs/roadmap/phase-3-branching.md`
- `docs/roadmap/phase-4-ergonomics.md`
- `docs/roadmap/phase-5-props-and-slots.md`
- `docs/roadmap/phase-6-tooling-lsp.md`
- `docs/roadmap/phase-7-build-time-compilation.md`
- `docs/roadmap/phase-8-streaming-async.md`

Files to modify:
- `docs/.vitepress/config.mts` — add `'roadmap/**'` to `srcExclude` (line 46; same contributor-only treatment as `assessment.md`). No sidebar changes.
- `docs/assessment.md` — in §6 (Recommendation), add one line linking to `roadmap/README.md` and note the user-chosen phase order supersedes the assessment's suggested sequencing (assessment suggested safety first; the chosen order runs expressions → safety → branching).

No code changes in this task — the roadmap documents ARE the deliverable. No CHANGELOG entry (docs-only, unpublished pages).

---

## Phase doc content (verified designs — carry into the docs)

All seams below were verified against the source by exploration + design agents this session.

### Phase 1 — Native expression tier (sandbox, Roslyn-free)

**Goal:** `@if(Count > 0)`, `@(Price * Quantity)`, `@(Name ?? "anon")` compile to `System.Linq.Expressions` delegates with `AllowCSharp = false`; zero Roslyn on this path.

**Design essentials:**
- **Operator set: complete C#/C-style, exactly the operators `System.Linq.Expressions` supports natively** — if you know C#, it just works; no surprising gaps. Precedence is the C# table verbatim:

  | Level (high→low) | Operators | Expression-tree mapping |
  |---|---|---|
  | primary | `()` grouping, `.` member (null-safe like member tier), `[]` indexer, `name(args)` function | MemberAccess / ArrayIndex-indexer / Call (whitelist) |
  | unary | `!` `-` `+` `~` | Not, Negate, UnaryPlus, OnesComplement |
  | multiplicative | `*` `/` `%` | Multiply, Divide, Modulo |
  | additive | `+` `-` (`+` = string concat if an operand is string) | Add/Concat, Subtract |
  | shift | `<<` `>>` | LeftShift, RightShift |
  | relational | `<` `<=` `>` `>=` | LessThan … GreaterThanOrEqual (lifted-null like C#) |
  | equality | `==` `!=` | Equal, NotEqual (object.Equals fallback for mixed refs) |
  | bitwise | `&`, then `^`, then `\|` | And, ExclusiveOr, Or |
  | logical | `&&`, then `\|\|` (bool-only, short-circuit) | AndAlso, OrElse |
  | null-coalescing | `??` (right-assoc) | Coalesce |
  | conditional | `?:` (right-assoc) | Condition |

  Ternary is **in** (revised from the earlier no-ternary draft): the `?` token gates the `:`, so `:` remains unambiguous vs the chain DELIM — inside the expr rule `:` is only consumed after `?`; the nested-chain alternative stays ordered first so every currently-valid `a():b()` parameter parses as today. `?.` is deliberately absent: `.` hops are already null-safe (member-tier semantics) — documented. Excluded for simplicity: assignment/compound-assignment, `++`/`--`, lambdas, casts, `is`/`as`, `new`, arbitrary method calls (unchanged constraint), string interpolation (use `+` or the `format` function).
- Literals, C#-matching: integer (incl. `0x` hex, `L` suffix), real with `f`/`d`/`m` suffixes, `"string"` with standard escapes, `'c'` char, `true`/`false`/`null`.
- Lexer (`src/Heddle.Language/HeddleLexer.g4`): add the operator/literal tokens above to `mode CALL` only (keyword rules before `CALL_ID`; maximal munch orders `??` before `?`, `<<`/`<=` before `<`, etc.). Every new token was previously a lex error in CALL mode → no valid template changes meaning. CS/CALL_RETURNED modes untouched. Regen via `src/Heddle.Language/generate_cs.cmd` (ANTLR 4.13.1), commit `generated/`.
- Parser (`HeddleParser.g4`): 4th `call` alternative, ordered **last**: `extension_id? OUT_PARAMSTART native_expression OUT_PARAMEND`, with a left-recursive `expr` rule encoding the precedence table above (one labeled alternative per level; ternary reuses the DELIM token after `?`). Method-call syntax `x.Foo()` is *parsed but always compile-rejected* with a targeted error pointing to registered functions / the `@` C# tier. Operator semantics (numeric promotion, lifted nulls, enum bitwise) are pinned by a table-driven test comparing against C#-compiled expected values.
- AST: new `ExprNode` hierarchy in `src/Heddle/Language/Expressions/` carried on `CallParameter` as `NativeExpression` (extend `IsModelTypeParameter` predicate, `src/Heddle/Language/CallParameter.cs`). Built in `ParseContext.CreateItem` (~lines 399–494).
- Compile: new `NativeExpressionCompiler` (`src/Heddle/Runtime/Expressions/`) → emits the **existing** `CompiledParameter` shape (`Func<object,object,object,object>` over model/chained/root) or `ConstantParameter` for literal folding. New branch in `HeddleCompiler.CompileItem` (lines 254–351). Extract the property-resolution loop from `CompileModelAccessor` (353–425) into a shared helper so paths inside expressions get identical semantics (null-safe hops, `[Hidden]`, accessibility).
- Disambiguation (`name(args)` vs nested chains): chain alternative stays first → all currently-valid input parses as today. Functions reach the expression grammar only where chains can't match (multi-arg, literal args, embedded in operators). For standalone `fn(Path)`: resolution order **definition → extension → registered function** at the `TemplateFactory.Create` failure path; compile warning on shadowing.
- `FunctionRegistry` (new): `Register(string name, Delegate)` / `(string, MethodInfo)`, layered over a frozen `Default` with safe built-ins (`upper, lower, trim, len, contains, startswith, endswith, replace, substr, format, str, abs, min, max, round, floor, ceil`; invariant culture). Exposed as `TemplateOptions.Functions`; propagated via the options copy-ctor. Sandbox invariant: compiler can only emit property getters (member-tier rules), host-registered MethodInfos, and its own intrinsics — no `Type.GetMethod` path from user input.
- Options: `enum ExpressionMode { MemberPathsOnly, Native (default), FullCSharp }` on `TemplateOptions`; `AllowCSharp` becomes a bridge property over it (kept for back-compat, exact behavior of the guard at `HeddleCompiler.cs:301` preserved). Dynamic-scope operands in native expressions = compile error in v1 ("requires a typed model").

**Success criteria:** (1) the three flagship snippets compile+render with AllowCSharp=false and zero Roslyn invocation; (2) full existing suite + golden files pass unchanged under the `Native` default; (3) `MemberPathsOnly` restores today's errors; (4) all sandbox-negative templates produce positioned `HeddleCompileError`s, never execution; (5) registered functions work standalone and inside expressions.

**Validation scenarios (include in doc as a table):** positive — `@if(Items.Count > 0)`, decimal promotion `@(Price * Quantity)`, `??` on null, ternary `@(IsFeatured ? "★" : "")`, bitwise/shift `@(Flags & 4)`, `@(1 << Bits)`, mixed precedence `@(A + B * C == D && !E)` (must equal the C#-computed result), root-ref arithmetic `@(::Total - Amount)`, null-safe hop `@if(A.B > 0)` with `A=null` → false, `@(max(A, B))`, chain-vs-ternary coexistence `@out(a():b())` still parses as a nested chain; negative — `@(Name.ToUpper())` → method-call error, `@(System.IO.File.ReadAllText("x"))` → rejected, unregistered `@(evil(1))` → error, `@(X = 1)` assignment → parse error, `&&` on non-bool → compile error, dynamic model + operators → typed-model error, `MemberPathsOnly` + any expression → option-naming error.

**Size:** lexer/parser M; AST+wiring M; compiler L; registry M; options S; tests M.

### Phase 2 — Safe output default (OutputProfile)

**Goal:** `OutputProfile.Html` makes unnamed `@(...)` HTML-encode with `@raw(...)` opt-out; `Text` keeps today's behavior. Default `Text` in 1.x; flip to `Html` in 2.0 (documented breaking change).

**Design essentials:**
- `enum OutputProfile { Text, Html }`; `TemplateOptions.OutputProfile` + copy-ctor propagation (lines 35–44 — **also fix the existing omission of `ProvideLanguageFeatures` there**) + add to `Equals`/`GetHashCode`.
- Effective profile lives on `CompileContext` (not mutating the host's options instance); `@profile(html)` directive via new `ProfileExtension` following the `ModelExtension` compile-time-directive pattern (`InitStart` sets state, returns null, block removed). Positional semantics like `@model` (top-of-file).
- Redirect seam: in `HeddleCompiler.CreateExtension` (~line 468), when name is `""` and profile is Html, resolve `"html"` (`EmptyHtmlExtension`) instead — reuses the entire existing `[EncodeOutput]`/`DirectRender`/`WebUtility.HtmlEncode` pipeline; `TemplateFactory` untouched (its static registry must not be profile-mutated).
- `raw` opt-out: add `[ExtensionName("raw")]` as second name on `EmptyExtension` (registry verified free; multi-name supported).
- Cache: the live cache is `TemplateResolver.TemplatesCache` keyed by path (`DocumentsCache` is dead code) — add profile to the resolver's key and an optional profile parameter to its ctor.
- Container extensions (list/if/for/out) forward raw → no double-encoding introduced. Add a compile **warning** for `@(X):html()` under Html (redundant encode feeding auto-encode).
- Docs to update in the same phase: `built-in-extensions.md` HTML-encoding section + empty-extension entry (also fix its claim that `[NotEncode]` works per-property — it is dead code), `language-reference.md` nuances, `custom-extensions.md`.

**Success criteria:** (1) `Name = "<b>x</b>"` renders encoded under Html, raw under Text (both golden-pinned); (2) `@raw(Name)` passes through under Html; (3) `@profile(html)`/`@profile(text)` flip a compile; (4) existing suite untouched under default Text; (5) one resolver serves both profiles without cache collision.

**Validation scenarios:** XSS regression `@profile(html)@(UserInput)` with `<script>alert(1)</script>` → encoded; `@raw(TrustedHtml)` passthrough; body inheritance `@if(Flag){{@(UserInput)}}`; double-encode warning on `@(X):html()`; negative `@profile(pdf)` → error listing valid values; copy-ctor round-trip test (incl. the `ProvideLanguageFeatures` fix).

**Size:** all items S, except tests/goldens+docs M.

### Phase 3 — Branching via declarative local context flow (zero grammar change)

**Goal:** optional `@elif`/`@else` as ordinary standalone `[ExtensionName]` extensions that coordinate with a preceding `@if`/`@ifnot` through a **published local context** — no keywords, no chain syntax, no imperative statement structure. Text between the branch blocks renders normally, as if outside the set. The underlying publish/read mechanism is a general, public extension capability (each item can publish something for later extensions to pick up) — if/elif/else is merely its first consumer.

**Surface (no new syntax at all):**
```heddle
@if(IsFeatured){{ <b>Featured</b> }}
this text always renders
@elif(IsArchived){{ <i>Archived</i> }}
@else(){{ Regular }}
```

**Design essentials:**
- **Local context channel**: `Scope` (readonly struct, `src/Heddle/Data/Scope.cs`) gains one internal reference field to a small mutable frame (working name `ScopeLocals`), **lazily created on first publish** (zero cost for templates that never branch). A frame is scoped to one body execution: root frame per `Generate` call; fresh frame when descending into a subtemplate body (`AbstractExtension.RenderInnerResult`/`GetInnerResult` and the partial/definition invocation paths — enumerate all body-execution entry points during implementation). Sibling blocks within one body share the frame; nested bodies and each `@list`/`@for` iteration get fresh frames → isolation falls out naturally.
- **Public API** (exact naming at implementation): `scope.Publish(string key, object value)` / `scope.TryRead(string key, out object value)` — exposed to custom extensions and documented in `custom-extensions.md` as the sanctioned way for sibling extensions to coordinate declaratively.
- **Branch protocol**: `IfExtension`/`IfNotExtension` additionally publish `BranchState { bool Satisfied }` under a reserved key after evaluating. `ElifExtension` (new, alias `elseif`): reads the state — satisfied → render nothing, republish; not satisfied → evaluate own condition, render body if true, republish; **no state present → behaves exactly like `if`** (standalone-friendly, "optional" per requirement). `ElseExtension` (new): satisfied → nothing; not satisfied or missing → render body. A new `@if` overwrites the state (starts a new set). Bodies compile against `parent` exactly like `IfExtension` today.
- **No lexer/parser/`TemplateChain` changes.** No thunks or laziness machinery needed — each branch is its own output block, so only the winning branch's body ever executes; short-circuit is inherent.
- **Compile-time lint** (S): the compiler processes output chains in document order — emit a `HeddleCompileWarning` for `@elif`/`@else` with no preceding `@if`/`@ifnot`/`@elif` at the same body level.
- Thread safety: the frame lives in the render-scoped `Scope` lineage, never on extension instances (instances are shared across concurrent renders).
- Recorded as an open design note (not v1): auto-publishing a minimal outcome record per sibling item ({extension name, produced output}) as a future generalization of "every item publishes".
- Pin during design: interaction with `@out()` projection (does projected content execute in the caller's frame or the definition's? — decide and test) and with `@partial` (fresh frame).

**Back-compat:** effectively perfect — no parsing changes, new extension names only (`elif`/`elseif`/`else` verified free in the registry), one additive internal field on `Scope`. Verify no measurable perf change from the extra `Scope` field (passed by `in`; benchmark suite).

**Risks:** missing a body-execution entry point when creating frames (leaks state across bodies) — mitigate with an explicit inventory + isolation fixtures; per-iteration frame allocation in hot loops — mitigated by lazy creation (only allocates when something publishes); `@out()` frame semantics ambiguity — resolve with a pinned fixture before implementation.

**Success criteria:** (1) the flagship interleaved fixture renders exactly one branch plus all interleaved text for each condition state (goldens); (2) **zero grammar changes** (`generated/` untouched) and the entire existing suite byte-identical; (3) per-iteration isolation inside `@list` (alternating-condition fixture); (4) nested sets independent (if-set inside an if body); (5) standalone `@elif` behaves as `if`, standalone `@else` renders its body, and the lint warning fires for both; (6) parallel renders with opposite conditions show no cross-talk; (7) a sample custom publisher/consumer extension pair works via the public API (doc example); (8) benchmark: no regression on templates that never publish.

**Validation scenarios:** flagship interleaved template under A / !A∧B / neither; two sequential sets `@if(A){{1}}@else(){{2}} @if(B){{3}}@else(){{4}}` (second set independent); branch set inside `@list(Items)` with per-element conditions (state resets each iteration); if-set nested inside an `@if` body (frames isolated); branch set split across an `@out()` projection (pinned behavior); `@else()` with no preceding `@if` (documented render + warning); concurrency test with opposite conditions; hot-loop benchmark (`@list` over 10k items with if/else inside).

**Size:** Scope frame + creation-point inventory M; branch extensions S/M; lint S; tests M; docs (incl. custom-extensions.md section) S.

### Phases 4–8 (prioritized by user value; scoped, lighter detail — each doc still gets full success criteria + validation scenarios)

**Phase 4 — Ergonomics quick wins** (cheapest daily-felt improvements):
- `@for` sugar: accept an integer count / native expression (`@for(5)`, `@for(Count)`) and auto-construct the iteration — no more `@new ForModel{...}` C# requirement (ForIndexExtension gains int `[DataType]` handling).
- Compile warning when a `-> ` default-output definition is also called by name (double-render trap; detectable in `HeddleCompiler` when resolving definition calls).
- Opt-in `TemplateOptions.TrimDirectiveLines`: whole-line directives (`@using`, `@model`, `@profile`, definitions) swallow their trailing newline — removes most `@\` preamble noise.
- Document `@import(){{path}}` as the readable alias of `@<<{{path}}` (the `import` extension already exists — docs-only item).
- Success criteria: `@for(3){{x}}` renders `xxx` with AllowCSharp=false; warning fires on the double-render fixture; goldens unchanged when options off. Validation: preamble template with TrimDirectiveLines on/off golden pair; `@for(Count)` with model-driven count; negative `@for(Name)` (string) → type error.

**Phase 5 — Named props & parameterized slots** (component completeness; the largest language design item — doc includes a design-first milestone with grammar sketches, not final grammar):
- Call-site named arguments (`@card(Article, style: "wide", compact: true)`), props declared in the definition header with types/defaults, props readable in bodies; parameterized slots (pass values into the `@out()` projection).
- Constraints recorded: must compose with abstract (late-bound) definitions and inheritance narrowing; named args build on Phase-1 literal/expression parsing in CALL mode; `:` inside parens must not collide with chain DELIM (named-arg `name:` appears only after `,` or first-arg identifier + `:` — grammar note).
- Success criteria: a card/layout fixture using 2 props with defaults + 1 parameterized slot renders correctly and type-errors on prop typos at the right call site. Validation: props on inherited definitions (narrowing), prop shadowing model member, missing required prop → positioned error.

**Phase 6 — Editor tooling / LSP** (after grammar churn from 1/5 settles; 3 adds no grammar):
- LSP server over the existing `ProvideLanguageFeatures` token stream + `ExType` data: member completion inside `@(...)` (typed — few template languages can do this correctly), hover types, diagnostics from `HeddleCompileError` positions, go-to-definition across `@<<` imports; VS Code extension packaging reusing the TextMate grammar (`docs/coloring-scheme/heddle.tmLanguage.json`).
- Success criteria: in VS Code — completion after `@(` lists the current model's members inside a `@list` body; a type error shows a squiggle at the template position; F12 on a definition call jumps across an import. Validation: the blog-model fixtures from language-reference as an editor test corpus.

**Phase 7 — Build-time compilation backend (AOT / T4-successor)** (depends on Phase 1 — expressions must be Roslyn-free or codegen-able):
- Source generator / MSBuild task compiling `.heddle` → C# at build time; shared ANTLR front end; runtime path remains the semantic reference; Native AOT + trimming supported; real stack traces into generated code.
- Success criteria: a sample NativeAOT console app renders the benchmark home page with zero runtime Roslyn/expression-tree compilation; byte-identical output vs the runtime path across the full fixture corpus (differential test); startup time measured near-zero vs runtime compile. Validation: fixture-corpus differential harness; trimmed+AOT publish smoke test in CI.

**Phase 8 — Streaming & async rendering API**:
- `Generate(TextWriter)` / `IBufferWriter<byte>` UTF-8 sink alongside `string Generate`; evaluate async model resolution separately (may be out of scope — record decision).
- Success criteria: rendering the benchmark page to a pooled buffer allocates no full-output string; BenchmarkDotNet shows allocation reduction; encoding behavior identical across sinks (goldens via sink capture). Validation: large-page render through both APIs compared byte-for-byte.

---

## Execution steps

1. Create `docs/roadmap/README.md` (index: table of phases with one-line goals, ordering rationale — user-fixed 1→2→3, then value-ordered 4→8; dependency notes; link back to `assessment.md`).
2. Create the 8 phase docs from the content specs above. Keep each self-contained: restate the minimal context it needs (don't require reading other phases). Use the repo's doc style (relative links into `../src/...`, `heddle` code fences, en-dashes).
3. Update `docs/.vitepress/config.mts`: `srcExclude: ['assessment.md', 'roadmap/**']` with a one-line comment matching the existing assessment comment style.
4. Update `docs/assessment.md` §6: one sentence + link noting the executable roadmap lives in `roadmap/README.md` and records the chosen phase order (expressions → safe default → branching → value-ordered rest).

## Verification

- `cd docs && npm run docs:build` (or the package.json script name found there) — VitePress build must pass; roadmap pages must NOT appear in the built site (check `.vitepress/dist` for absence), assessment exclusion still effective. Note the Windows drive-casing caveat: run from an uppercase-drive cwd or rely on the existing `preserveSymlinks` fix.
- Verify every relative link in the new docs resolves (files exist at the referenced paths; `../src/...` links follow the existing REPO_BLOB rewrite convention).
- Read-through check: each phase doc independently answers Goal / Design / Back-compat / Success criteria / Validation scenarios / Size without needing another phase doc open.
