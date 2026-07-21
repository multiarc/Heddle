# Phase 3 — jvm (spec)

## Header

- **Status:** Specified — ready for implementation
- **Source plan:** [phase-3-jvm.md](../../../plan/phase-3-jvm.md) (standing rulings in the
  [plan index](../../../plan/README.md); resolved Q&A register in
  [open-questions.md](../../../plan/open-questions.md) — every resolution there is binding on
  this spec; the load-bearing ones here are Q1.1, Q1.2 as modified, Q1.3, Q1.6, Q1.7, Q2.1,
  Q2.2 = A, Q6.2)
- **Assumes merged:** the complete Phase 1 implementation
  ([Phase 1 spec](../phase-1-cross-stack-foundation/README.md), WI1–WI8): the eight-entry golden
  corpus with manifest and `.verify.json` files under `src/Heddle.Performance/GoldenCorpus/`,
  [parity contract v2](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  the [metrics & publication protocol](../phase-1-cross-stack-foundation/metrics-protocol.md),
  and the published Phase 1 protocol run under `docs/benchmarks/<date>/` (the source of the
  Heddle reference rows). No JVM work item may start against a corpus entry that does not exist.

| Document | Purpose |
|---|---|
| [README.md](README.md) (this file) | Entry document: assumed state, design decisions, implementation plan, gates |
| [thymeleaf-controlled-feasibility.md](thymeleaf-controlled-feasibility.md) | The phase's riskiest port, retired first: divergence taxonomy (whitespace-only vs beyond-whitespace), the block-only authoring pattern, the feasibility-probe ladder, and the exclusion-evidence procedure |
| [construct-mapping.md](construct-mapping.md) | The full 8 × 2 × 2 cell matrix with normative template texts per workload, engine, and track, plus the Java model definitions |
| [harness-and-jmh.md](harness-and-jmh.md) | Harness layout under `benchmarks/jvm/`, Maven build, JTE precompilation, gate runner, JMH configuration, run procedure, and report assembly |

## Scope and goal

Deliver the JVM ecosystem entry of the cross-stack benchmark program: JTE 3.2.4
(performance/peer pick) and Thymeleaf 3.1.5.RELEASE (credibility pick) rendering all eight
Phase 1 corpus workloads in both fairness tracks under JMH 1.37 on the protocol machine, gated
by parity contract v2, published as one date-stamped report under `docs/benchmarks/<date>/` in
the protocol's format — with the Heddle reference row and Heddle-anchored ratio column
(Q2.2 = A, Q6.2), JVM allocation/GC figures per-ecosystem only, and the SAC 2026
disclosed-limitations note.

Out of scope (plan non-goals, carried): any other JVM engine; Kotlin/`.kte`; Spring MVC
integration (Thymeleaf is measured standalone through `org.thymeleaf.TemplateEngine`); GraalVM
native-image or alternative JVM vendors as extra columns; any corpus or contract edit *at this
phase's implementation time* (contract pressure found during implementation escalates as evidence
— the [cross-spec amendments ledger](../../common/cross-cutting-decisions.md#cross-spec-amendments-ledger)
for post-ship changes, the [exclusion-evidence procedure](thymeleaf-controlled-feasibility.md#exclusion-evidence-procedure)
for controlled-cell divergence — never absorbed locally; the one verifier-needle erratum this
phase surfaced was settled directly in the unshipped Phase 1 spec, see
[D6](#d6--jte-idiomatic-encoded-cells-need-a-verifier-needle-amendment-erratum-not-local-patch));
cross-runtime allocation comparison.

## Assumed state

Re-verified against current sources while authoring this spec; not trusted from the plan or
from spikes. Spike references: spike C (cross-language escaper research, primary sources) and
spike D (Criterion/JMH harness research) — every claim below marked *(read)*, *(fetched)*, or
*(executed)* was re-verified first-hand on 2026-07-20.

| Seam | Verified state |
|---|---|
| Phase 1 artifacts | **Normatively defined, not yet implemented** at authoring time: `src/Heddle.Performance/GoldenCorpus/` does not exist in the working tree *(read — directory listing)*. This spec binds to the Phase 1 spec's normative definitions (corpus format, manifest fields, `.verify.json` schema, N1–N5, gate semantics) and its **Assumes merged** line above; the implementer re-confirms the corpus exists and `verify-corpus` passes before WI2 |
| Repo layout | No top-level `benchmarks/` directory exists *(read — repo root listing: `docs/`, `src/`, `samples/`, `lib/`, `editors/`)*. `docs/benchmarks/<date>/` holds published reports (2026-07-11, 2026-07-18). Phase 1 D6 deliberately did **not** create a top-level benchmark directory for the corpus; its recorded revisit trigger (a phase 2–6 harness that cannot conveniently reach into `src/`) is **not** met here — the JVM harness reads the corpus by relative path (D8) |
| JTE version | `gg.jte:jte` **3.2.4** is `<latest>`/`<release>` on Maven Central *(fetched — `repo1.maven.org/maven2/gg/jte/jte/maven-metadata.xml`, lastUpdated 2026-04-29)*; `gg.jte:jte-maven-plugin` and `gg.jte:jte-runtime` are also 3.2.4 *(fetched)* — the plugin/dependency version alignment jte's docs require holds at this pin. This closes spike D open item 3 for JTE |
| Thymeleaf version | `org.thymeleaf:thymeleaf` **3.1.5.RELEASE** is `<latest>`/`<release>` on Maven Central *(fetched — `repo1.maven.org/maven2/org/thymeleaf/thymeleaf/maven-metadata.xml`, lastUpdated 2026-04-21)*. Closes spike D open item 3 for Thymeleaf |
| JMH version | `org.openjdk.jmh:jmh-core` **1.37** is `<latest>`/`<release>` on Maven Central; the metadata's `lastUpdated` is 2023-08-03, confirming spike D's finding that 1.37 is the current pin with no newer release *(fetched)* |
| JTE escaper tables | `gg.jte.html.escape.Escape` *(fetched — `casid/jte@main`, `jte-runtime/src/main/java/gg/jte/html/escape/Escape.java`)*: `htmlContent` escapes **only `& < >`** (`&amp; &lt; &gt;`); `htmlAttribute` escapes `' " & <` (`&#39; &#34; &amp; &lt;`) and **not** `>`. Spike C's tables confirmed at source. Consequences: D4 (controlled encoded) and D6 (idiomatic encoded) |
| JTE output seam | `gg.jte.TemplateEngine.checkOutput` *(fetched — `jte-runtime/src/main/java/gg/jte/TemplateEngine.java`)*: `if (contentType == ContentType.Html && !(templateOutput instanceof HtmlTemplateOutput)) return new OwaspHtmlTemplateOutput(templateOutput);` — a caller-supplied `gg.jte.html.HtmlTemplateOutput` (public interface: `TemplateOutput` + `setContext(String tagName, String attributeName)`) is used as-is. This is the engine-configuration seam D4 uses |
| JTE precompilation | `TemplateEngine.createPrecompiled(Path, ContentType)`, `createPrecompiled(ContentType)` (classpath form), and `createPrecompiled(Path, ContentType, ClassLoader, String packageName)` all exist *(fetched — TemplateEngine.java)*; `jte-maven-plugin` `generate` goal (phase `generate-sources`) supports `sourceDirectory`, `contentType`, `packageName`, `trimControlStructures` (default `false`) and multiple `<execution>` blocks *(fetched — jte.gg/maven-plugin)*. Generate mode packages template classes into the application JAR *(fetched — jte.gg/pre-compiling)* |
| JTE whitespace control | **No inline trim syntax exists** (no `{%- -%}`-style markers) — jte.gg/syntax reviewed in full and casid/jte#69 shows the feature requested without a built-in answer; the only whitespace mechanism is the build-plugin boolean `trimControlStructures` (default `false`) *(spike C §(d), re-checked against jte.gg/syntax while authoring)*. This closes the plan's flagged assumption (success criterion: "the phase spec records verified JTE whitespace-control behavior") — see D3 |
| Thymeleaf escaper | `org.thymeleaf.standard.processor.StandardTextTagProcessor` imports and uses `org.unbescape.html.HtmlEscape` *(fetched — `thymeleaf/thymeleaf@3.1-master` source)*; the HTML-mode path is `escapeHtml4Xml` = HTML4 named references defaulting to decimal, LEVEL_1 markup-significant only *(spike C, unbescape source)*. Spellings: `&amp; &lt; &gt; &quot; &#39;` — **byte-identical to the canonical N5 set**, so Thymeleaf's stock escaper is already oracle-canonical (D5) |
| Thymeleaf mechanisms | 3.1 tutorial *(fetched — thymeleaf.org/doc/tutorials/3.1/usingthymeleaf.html)*: `th:block` is the synthetic tag (§11.4); `th:switch`/`th:case` evaluate cases in order, first true wins, default is `th:case="*"` (§7.2, quoted verbatim in the [feasibility doc](thymeleaf-controlled-feasibility.md)); escaped inlining `[[...]]` ≡ `th:text`, unescaped `[(...)]` ≡ `th:utext`, active by default in tag bodies (§12.1); fragments `th:fragment="name(arg)"` invoked with `th:replace="~{tmpl :: name(${arg})}"` (§8.1); `xmlns:th` is an optional IDE incantation, not required (§3.1); the tutorial's own §3.1 processed-output example shows `<!DOCTYPE html>` preserved in output |
| Standalone Thymeleaf API | `new TemplateEngine()` + `ClassLoaderTemplateResolver` + `org.thymeleaf.context.Context` + `process(name, context, writer)` — the non-web entry point *(spike D §6, tutorial)* |
| JDK | Eclipse Temurin 25 (LTS) is the current recommended pin (spike D §5); latest Windows x64 GA build at authoring time is `jdk-25.0.3+9` *(fetched — api.adoptium.net v3 assets query)* |
| Composed-page twin shape | `src/Heddle.Performance/Runners/FluidTest.cs` + `Runners/README.md` *(read)*: the composed-page twins render layout-include + `section.*`/`comp.*` member lookups + `areas[name]` map lookups + one real loop over `area_names`; the multi-KB area fragments are **model data** (from `TwinContent.Areas`/`AreaComponent.Areas`), never template literals. The JVM ports mirror this construct set (construct-mapping.md §composed-page) |
| Corpus markup attribute shape | The eight workloads' normative markup ([workloads.md](../phase-1-cross-stack-foundation/workloads.md) template texts plus `trivial-substitution.heddle`, read end-to-end) carries at most one attribute per element **except** trivial-substitution's `<a class="link" href="…">` (two attributes, one substituted) — so attribute-*ordering* exposure is confined to that single tag, and every other element is single-attribute (load-bearing for the Thymeleaf feasibility analysis, divergence class B2) |
| Support tooling versions | Maven Central `<latest>/<release>` *(fetched)*: `jackson-databind` 2.22.1 (gate-runner JSON parsing), `maven-shade-plugin` 3.6.2, `maven-compiler-plugin` 3.14.1 (last stable 3.x; 4.0.0-beta excluded) |

## Design decisions

Ordered by the plan's internal ordering: Thymeleaf controlled-track feasibility first.

### D1 — Thymeleaf controlled track: block-only logic, literal markup, inlined expressions; feasibility probed first

- **Decision.** Controlled-track Thymeleaf templates are authored under the **block-only
  pattern**, normative in
  [thymeleaf-controlled-feasibility.md](thymeleaf-controlled-feasibility.md): every structural
  processor (`th:each`, `th:if`, `th:switch`/`th:case`, `th:replace`) sits on a synthetic
  `<th:block>` element that is removed whole from output; every output tag is **literal
  template text carrying no `th:*` attribute** (sanctioned exception: `th:attr` wherever an
  attribute *value* is substituted — `encoded-loop`'s `<td data-tag>`, trivial-substitution's
  `<a href>`/`<img src>` — because inlining has no attribute-value form); every substitution is
  an inlined expression — `[(${...})]` (unescaped) on the raw suite, `[[${...}]]` (escaped) on
  the encoded suite. Under this pattern the only engine-generated bytes are expression results
  and the only structural transformation is whole-element removal — the residual
  **non-whitespace** risk shrinks to the four named items in the feasibility doc (B2–B4, B9; B1's
  attribute-removal residue is whitespace-only and now passes via N3b), each probed by
  WI2 **before any other JVM port work** (plan internal ordering). Whitespace-only divergence
  passes via the contract's N2/N3/N3b/N4 (Q1.2 as settled by the 2026-07-20 N3b maintainer
  ruling — any whitespace-only divergence passes program-wide); a workload whose best-effort
  divergence is **non-whitespace** follows the exclusion procedure (evidence record, cell marked
  `excluded — documented evidence`, Thymeleaf stays in the idiomatic track for that workload, no
  replacement engine without user sign-off).
- **Rationale.** The controlled track's Are-We-Fast-Yet discipline explicitly permits disclosed
  non-idiomatic authoring (plan §Design direction), so the controlled templates may be shaped
  entirely around the byte gate. The block-only pattern eliminates the two largest divergence
  classes analytically (attribute-removal residue inside output tags; attribute reordering —
  impossible anyway given the corpus's one-attribute-per-tag markup, Assumed state) instead of
  hoping the engine behaves. `th:block` and inlining are core documented Standard Dialect
  mechanisms (tutorial §§11.4, 12.1). Probing feasibility first retires the phase's largest
  risk while it is cheap, per the plan.
- **Alternatives rejected.** `th:text`/`th:utext` on output tags (puts a removable attribute on
  every output tag — maximizes residue exposure; kept for the *idiomatic* track where it is the
  documented idiom); the TEXT template mode (abandons the HTML pipeline the credibility pick is
  famous for — the report would no longer measure "Thymeleaf" as practitioners know it, and the
  plan names DOM/markup behavior as the thing to confront, not dodge); preprocessing Thymeleaf's
  output before the gate beyond N1–N5 (non-conformant: the normalization list is closed).
- **Grounding.** [Feasibility doc](thymeleaf-controlled-feasibility.md) (divergence taxonomy +
  probe ladder); tutorial §§7.2/11.4/12.1 *(fetched, Assumed state)*;
  [parity-contract-v2 — exclusion policy](../phase-1-cross-stack-foundation/parity-contract-v2.md#exclusion-policy);
  [open-questions Q1.2](../../../plan/open-questions.md).

### D2 — Thymeleaf engine setup: standalone 3.1.5.RELEASE, ClassLoader resolver, cached templates, reused Context

- **Decision.** Pin `org.thymeleaf:thymeleaf:3.1.5.RELEASE` (Central-verified). One
  `TemplateEngine` per track, configured with a `ClassLoaderTemplateResolver`
  (`prefix = "thymeleaf/"`, `suffix = ".html"`, `templateMode = TemplateMode.HTML`,
  `setCacheable(true)`, default cache TTL — never expires). One render =
  `engine.process("<track>/<workload>", context, writer)` into a fresh `java.io.StringWriter`;
  the `org.thymeleaf.context.Context` (variables) is built **once** per workload at setup and
  reused across renders. No Spring, no servlet context, no `WebContext`.
- **Rationale.** The metrics protocol defines one render as the cached-template render path
  with template and model constructed once outside the loop; `setCacheable(true)` makes the
  parse cost setup-time (first `process` in `@Setup` warms the cache; the parity gate render
  does this anyway), and reusing `Context` keeps model construction out of the timed region.
  `StringWriter` per render is part of producing the output, matching every other ecosystem's
  render-to-string shape. Standalone API per plan non-goal (no Spring view resolution).
- **Alternatives rejected.** `FileTemplateResolver` (adds filesystem paths to the benchmark jar
  invocation; classpath resources ship inside the shaded jar — one artifact, one reproduce
  command); `StringTemplateResolver` (loses template-name-based fragment resolution
  `~{controlled/tile :: tile}` needs); constructing `Context` per render (times model-map
  population, which the protocol excludes).
- **Grounding.** Spike D §6 (standalone API);
  [metrics-protocol — metric rules](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules);
  Maven Central metadata *(fetched, Assumed state)*.

### D3 — JTE controlled track: whitespace-free authoring, `trimControlStructures = false`, generate-goal AOT

- **Decision.** Pin `gg.jte:jte:3.2.4` + `gg.jte:jte-maven-plugin:3.2.4` (one `${jte.version}`
  property — the alignment jte's docs require). Controlled JTE templates are authored
  **whitespace-free**: control structures (`@if`/`@elseif`/`@else`/`@endif`,
  `@for`/`@endfor`, `@template.*` calls) sit inline on the same line as the markup they
  produce, exactly mirroring the Heddle normative templates' single-line bodies, so no control
  structure ever owns a line and no trimming is needed. `trimControlStructures` stays at its
  default **`false`** in every plugin execution. This records the verified whitespace-control
  finding (Assumed state): JTE has **no inline trim syntax** — the dossier's assumption of
  dash-style markers is false; the only mechanism is the build-plugin boolean, and this spec's
  authoring style makes even that unnecessary. AOT posture: the **`generate`** goal
  (phase `generate-sources`) generates Java sources that javac compiles into the benchmarks
  jar; at runtime the engine is `TemplateEngine.createPrecompiled(null, contentType, null,
  "<packageName>")` — no runtime `javac`, no template parsing at render or even startup time
  *(verify at implementation: the 4-arg overload with a `null` class directory loads from the
  application class loader — pinned against `TemplateEngine.java`'s `RuntimeTemplateLoader(classDirectory,
  parentClassLoader, config.packageName)` construction, and it is the same path the documented
  `createPrecompiled(ContentType)` classpath form uses; if it rejects `null`, fall back to
  `createPrecompiled(ContentType)` semantics by using the default package name for the Html
  execution and a `Path`-based load for the Plain one — no output byte changes either way)*.
  Two plugin executions, because `ContentType` is a code-generation-time property:
  `Plain` over `src/main/jte-plain/` (package `heddle.jte.gen.plain`) and `Html` over
  `src/main/jte-html/` (package `heddle.jte.gen.html`); each source tree contains `controlled/`
  and `idiomatic/` subdirectories (subdirectories act as packages, so class names cannot
  collide).
- **Rationale.** Whitespace-free authoring minimizes dependence on any trim mechanism (the
  Phase 1 templates were designed for exactly this — plan risk table row 4); `false` is the
  default and the no-op given this authoring, so the pin is the most reversible choice. Generate
  mode is jte's own documented production-AOT recommendation ("to speed up startup and rendering
  on your production server, it is possible to precompile all templates during the build") and
  packages classes into the jar automatically — matching the single-shaded-jar reproduce
  command. Raw suite uses `ContentType.Plain` (the engine's non-encoding path, mirroring every
  other cast engine's raw mode, same discipline the Go Q6.1 ruling encodes); encoded suite uses
  `ContentType.Html`.
- **Alternatives rejected.** `trimControlStructures = true` (adds a transformation with no
  target — the templates have no control-structure-owned lines; and its output effect is
  whitespace-only, i.e. invisible to the gate, so enabling it buys nothing and costs a
  disclosed deviation from defaults); the `precompile` goal (compiles classes *after* the build
  into a directory that must ship beside or be merged into the jar — complicates the
  one-artifact reproduce command for zero AOT benefit over generate mode); runtime-compile
  `TemplateEngine.create(...)` (measures jte's on-demand mode, not the AOT posture the peer
  framing is about); `ContentType.Html` for the raw suite (pays escaper scanning on workloads
  defined to measure the non-encoding path).
- **Grounding.** jte.gg/pre-compiling + jte.gg/maven-plugin *(fetched)*; jte.gg/syntax + casid/jte#69
  (no inline trim — spike C §(d)); `TemplateEngine.java` *(fetched)*;
  [workloads.md — shared authoring rules](../phase-1-cross-stack-foundation/workloads.md#shared-authoring-rules-for-the-five-new-workloads)
  rule 1; [open-questions Q6.1](../../../plan/open-questions.md) (the cast-wide raw =
  non-encoding-path discipline).

### D4 — JTE controlled encoded suite renders through a custom `FiveEntityHtmlOutput`

- **Decision.** The two controlled encoded cells (fortunes-encoded, encoded-loop) render with
  the `ContentType.Html` engine into `heddle.benchmarks.jvm.jte.FiveEntityHtmlOutput`, a new
  class implementing `gg.jte.html.HtmlTemplateOutput` over an internal `StringBuilder`:
  `writeUserContent(String)` escapes exactly `&`→`&amp;`, `<`→`&lt;`, `>`→`&gt;`,
  `"`→`&quot;`, `'`→`&#39;` in **both** tag-body and attribute contexts (it records
  `setContext(tagName, attributeName)` and **throws** `IllegalStateException` if a
  `script`-tag or `on*`-attribute JavaScript context is ever entered — no such context exists
  in the corpus templates, so the throw is a tripwire, not a path); all `writeContent`
  overloads append verbatim; primitive `writeUserContent` overloads append their
  `String.valueOf` (never escapable). `TemplateEngine.checkOutput` uses a supplied
  `HtmlTemplateOutput` as-is (verified at source, Assumed state), so this is a supported
  engine seam, not a fork or a post-processing step.
- **Rationale.** Verified at source: stock `OwaspHtmlTemplateOutput` escapes only `& < >` in
  tag bodies — `'` and `"` pass through raw in text context, so stock controlled output
  diverges from the oracle at every text-context quote (fortunes rows 3 and 11, every
  `encoded-loop` Name/Comment cell) — a genuine **beyond-whitespace, beyond-spelling**
  divergence: N5 canonicalizes *spellings of escaped* characters, never characters an engine
  chose not to escape. Q1.1 prefers engine configuration to normalization wherever offered;
  this is precisely Phase 1's D3 precedent (Handlebars.Net's custom `ITextEncoder`, adopted for
  the intra-.NET *controlled* gate for the same unescaped-quote reason), applied through jte's
  equivalent seam. Emitting the canonical spellings directly (`&quot;`, `&#39;`) also renders
  N5 an identity transform on controlled JTE output. The controlled track's disclosed
  non-idiomatic authoring license covers it; the report discloses it in the JTE row notes.
- **Alternatives rejected.** Excluding the JTE controlled encoded cells (a configuration seam
  exists and is public API — exclusion would be unjustified under D11's policy);
  normalizing raw quotes into entities in the gate runner (adds a step outside the closed
  N1–N5 list — non-conformant by contract); authoring templates with pre-escaped data (changes
  what is measured — the escaping path *is* the encoded suite's dimension); asking upstream for
  spelling configuration (no such hook exists in `Escape.java` — fixed tables).
- **Grounding.** `Escape.java` + `TemplateEngine.checkOutput` + `HtmlTemplateOutput.java`
  *(all fetched, Assumed state)*;
  [Phase 1 D3](../phase-1-cross-stack-foundation/README.md#d3--handlebarsnet-encoded-twins-use-a-custom-itextencoder);
  [parity-contract-v2 — N5 rules](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5)
  (rule 3: configuration preferred);
  [open-questions Q1.1](../../../plan/open-questions.md).

### D5 — Encoded suite vs the Phase 1 alphabet and N5: per-engine reconciliation, gate-runner N5, executable escaper probe

- **Decision.** The reconciliation of both JVM engines' actual escaper output against the
  Phase 1 contract, closed per engine and per context:

  | Engine / path | `&` | `<` | `>` | `"` | `'` | Outside the five? | Reconciliation |
  |---|---|---|---|---|---|---|---|
  | Thymeleaf `th:text` / `[[...]]` (text and attribute) | `&amp;` | `&lt;` | `&gt;` | `&quot;` | `&#39;` | no (LEVEL_1) | **none needed** — byte-canonical already |
  | JTE controlled encoded (D4 custom output, both contexts) | `&amp;` | `&lt;` | `&gt;` | `&quot;` | `&#39;` | no | **none needed** — configured canonical |
  | JTE stock `htmlContent` (idiomatic text context) | `&amp;` | `&lt;` | `&gt;` | *raw* | *raw* | no | not a spelling issue — D6's erratum |
  | JTE stock `htmlAttribute` (idiomatic attribute context) | `&amp;` | `&lt;` | not escaped | `&#34;` | `&#39;` | no | `&#34;` → `&quot;` via **N5**; unescaped `>` cannot occur in the pinned data's attribute values (`Tag` contains no `<`/`>`) |

  Neither engine escapes any character outside the five in the paths used (verified: JTE
  `Escape.java` tables; unbescape LEVEL_1), so the untrusted-data alphabet needs no new
  exclusion from this phase and no JVM finding flows to the exclusion policy as
  broader-set evidence. The JVM gate runner **implements N5 in full** (the contract requires it
  of phases whose engines lack spelling configuration — jte's stock attribute path is such a
  path) exactly as specified: closed replacement table, single left-to-right scan, no rescan of
  replacements, encoded suite only. Before any encoded port is gated (plan risk row 5), WI1's
  **escaper probe** renders the pinned probe payload (`&<>"'` + `こんにちは` +
  `<script>alert('xss')</script>`) through all four escaping paths above in both text and
  attribute positions and asserts the table's spellings byte-for-byte; a mismatch is contract
  evidence escalated per Phase 1's machinery, never a local normalization hack.
- **Rationale.** Q1.1's resolution verbatim (configuration first, N5 for the rest); the probe
  turns spike C's source-read tables into an executed check at the pinned versions, which is
  the standard Phase 1 set with spike B (executed) over spike C (read).
- **Alternatives rejected.** Skipping N5 in the runner because controlled paths are canonical
  (idiomatic JTE attribute output needs it, and the verifier normalizes with N5 by contract);
  widening N5 for JTE's raw text-context quotes (that is divergence, not spelling — the exact
  distinction Phase 1's D3 rationale draws).
- **Grounding.** [parity-contract-v2 — N5](../phase-1-cross-stack-foundation/parity-contract-v2.md#entity-canonicalization-n5)
  and [untrusted-data alphabet](../phase-1-cross-stack-foundation/parity-contract-v2.md#untrusted-data-alphabet);
  spike C §§3–4; `Escape.java` *(fetched)*; plan risk table row 5.

### D6 — JTE idiomatic encoded cells need a verifier-needle amendment (erratum, not local patch)

- **Decision.** Stock JTE (`OwaspHtmlTemplateOutput`) leaves `'` and `"` unescaped in HTML
  *text* context (OWASP text-context rule; verified at source). As originally published, three
  Phase 1 verifier needles hard-coded text-context quote escaping and therefore structurally
  rejected this correct, secure, documentation-default output: fortunes-encoded `values` rows 3
  and 11 and its `required` entry, and encoded-loop's `values` entry
  `item &lt;4999&gt; &amp; &quot;co&quot;`. **This defect is now settled directly in Phase 1**:
  because the entire benchmark spec set is unshipped and pre-implementation, the erratum was
  corrected in-place in
  [golden-corpus.md — idiomatic verifier definitions](../phase-1-cross-stack-foundation/golden-corpus.md#idiomatic-verifier-definitions)
  (amendment note dated 2026-07-20), rather than routed through the post-ship amendments ledger.
  Phase 3 cites the amended needles as settled fact; there is no open state, no ratification
  gate, and no fallback branch. The amended needle set — the exact text now in Phase 1's
  golden-corpus.md — is:

  > *fortunes-encoded* — `values` row 3 is
  > `{ "text": "A computer scientist is someone who fixes things that aren", "count": 1 }`;
  > `values` row 11 is the two entries
  > `{ "text": "&lt;script&gt;alert(", "count": 1 }` and
  > `{ "text": ");&lt;/script&gt;", "count": 1 }`; `required` is
  > `[ { "text": "&lt;script&gt;alert(", "minCount": 1 }, { "text": ");&lt;/script&gt;", "minCount": 1 } ]`;
  > `forbidden` (`<script>alert(`) unchanged. *encoded-loop* — the `values` entry
  > `item &lt;4999&gt; &amp; &quot;co&quot;` is now `{ "text": "item &lt;4999&gt; &amp;", "count": 1 }`;
  > the **attribute-context** needle `tag-0&amp;&#39;0&#39;` is unchanged (attribute escapers do
  > escape quotes); `forbidden`/`required` (`<angle>` / `&lt;angle&gt;` × 5000) unchanged.

  Each replacement needle is a substring of the needle it replaced **that remains unique in each
  workload's output**, so every five-character escaper (Heddle oracle, all Phase 1 twins,
  Thymeleaf, and every other cast engine surveyed) keeps identical exact counts — the correction
  changes no existing pass/fail status, and the `verify-corpus` calibration corruptions still
  trip (`forbidden`, removed-row, and reorder needles all untouched). Because Phase 1's
  golden-corpus.md now carries the corrected needles, Phase 1's implementation
  (`Runners/IdiomaticChecks.cs` → `export-corpus`) naturally emits the corrected `.verify.json`
  files; WI4 authors idiomatic JTE straight against them (no `.NET`/records.md edit belongs to
  this phase). Thymeleaf idiomatic-encoded cells are unaffected regardless — its escaper already
  satisfied both the original and the amended needles.
- **Evidence.** `gg.jte.html.escape.Escape.htmlContent` escapes only `& < >` (source,
  casid/jte@main, verified 2026-07-20); the needles as originally published fail any
  OWASP-conformant context-aware text escaper, contradicting Q1.7's premise that idiomatic
  implementations use each engine's official default path.
- **Rationale.** The contract's closed rules leave no conformant local fix: N5 cannot invent
  escapes, and this phase is barred from patching Phase 1 artifacts *at implementation time* (plan
  back-compat: contract pressure is escalated as evidence, never absorbed locally). The correct
  disposition for a defect found in an **unshipped** sibling spec during **spec authoring** is a
  direct pre-implementation fix to that spec — which is what happened (orchestrator process ruling;
  see the golden-corpus.md amendment note below), leaving the post-ship amendments ledger for
  genuine post-ship changes.
- **Alternatives rejected.** Routing the correction through the post-ship amendments ledger with a
  provisional-default/ratification gate (the ledger is scoped to changes discovered *during
  implementation* / post-ship — spec-conventions.md; using it for an unshipped-spec authoring-time
  defect leaves needless open state); running idiomatic JTE through D4's custom output (no
  practitioner does this — it would fake the practitioner experience Q1.7 exists to measure); a
  JVM-local "verifier profile" (a silent fork of the contract); marking the cells excluded under
  Q1.2 (Q1.2/D11 is a *controlled-track* policy; misusing it here would misreport a verifier
  defect as an engine incapacity).
- **Grounding.** `Escape.java` *(fetched)*;
  [golden-corpus.md — idiomatic verifier definitions](../phase-1-cross-stack-foundation/golden-corpus.md#idiomatic-verifier-definitions)
  (amended needles + amendment note);
  [open-questions Q1.7](../../../plan/open-questions.md);
  [spec-conventions — amendments](../../common/spec-conventions.md#amendments-during-implementation)
  (ledger scope: post-ship / during-implementation changes).

### D7 — Idiomatic-track authoring standard, instantiated for the two engines (Q1.7)

- **Decision.** Idiomatic implementations are authored in-repo following official
  documentation patterns, doc URLs cited in a header comment per template/setup file (contract
  requirement). Concretely: **Thymeleaf** idiomatic templates are natural templates — multi-line
  indented HTML with `xmlns:th` on the root element, `th:text`/`th:utext` on output elements
  with prototype body text, `th:each` on the repeated element itself, `th:if` on the
  conditional element, `th:switch`/`th:case` for the four-way chain, `th:replace` fragments for
  the tile partial (cited: usingthymeleaf.html §§3, 5, 6, 7, 8, 12). **JTE** idiomatic templates
  are multi-line indented `.jte` files with `@param`, `@if`/`@elseif`/`@else`, `@for`,
  `@template.idiomatic.tile(...)` calls, rendered through the stock engine
  (`ContentType.Plain` raw / `ContentType.Html` encoded, default `OwaspHtmlTemplateOutput`
  wrapping a `StringOutput`) (cited: jte.gg/syntax, jte.gg/html-rendering,
  jte.gg/pre-compiling). Gate: the Phase 1 verifier only (never the byte gate); normative
  texts in [construct-mapping.md](construct-mapping.md).
- **Rationale.** Q1.7 resolution verbatim; the two engines' idioms are exactly what their
  tutorials teach, giving the report's idiomatic table defensible provenance.
- **Alternatives rejected.** Importing mbosecke/casid/agentgt fork templates (Q1.7 rejected
  third-party imports; that lineage also asserts no output equality — the precise gap this
  program fills).
- **Grounding.** [open-questions Q1.7](../../../plan/open-questions.md);
  [parity-contract-v2 — idiomatic-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate).

### D8 — Harness lives in `benchmarks/jvm/`; Maven, not Gradle

- **Decision.** The JVM harness is a new top-level directory `benchmarks/jvm/` — establishing
  `benchmarks/<ecosystem>/` as the cross-phase convention for the phase 2–6 harnesses — the
  same convention the Phase 2 twin records independently
  ([phase-2-rust D2](../phase-2-rust/README.md#d2--harness-location-benchmarksrust-a-new-top-level-benchmarks-directory),
  `benchmarks/rust/`); the two specs agree. No stronger existing repo convention exists (Assumed state:
  no top-level `benchmarks/`; `src/` is the .NET solution and MSBuild-owned — a Maven project
  inside it would sit under a foreign build system's root). The corpus stays where Phase 1 put
  it: the harness reads `../../src/Heddle.Performance/GoldenCorpus/` relative to
  `benchmarks/jvm/` (overridable via `-Dheddle.corpus=<path>`), so Phase 1's corpus-move
  trigger is not pulled. Build system: **Maven** (`pom.xml` + committed `mvnw` wrapper,
  Maven Wrapper distribution current at implementation), producing one shaded
  `target/benchmarks.jar`.
- **Rationale.** Maven over Gradle on three grounds: (1) JMH's officially documented setup is
  the Maven archetype/shade pattern — the uber-jar the JMH README treats as the golden path,
  and what `java -jar benchmarks.jar` reproduce commands assume; (2) `jte-maven-plugin` 3.2.4
  is version-aligned with the engine on Maven Central (verified — Assumed state), and the
  Gradle plugin publishes through the separate Gradle Plugin Portal channel (not present at
  the Central coordinates checked), one more registry to pin; (3) no Gradle daemon — Maven
  leaves no persistent background JVM on the protocol machine between build and measurement,
  a measurement-hygiene point on a box whose quietness the protocol records.
- **Alternatives rejected.** Gradle (daemon + second registry, no offsetting benefit here);
  placing the harness under `src/` (foreign to the .NET solution; `dotnet build`/CI globbing
  would trip over it); placing it under `docs/` (reports only, by strong existing convention).
- **Grounding.** Repo-root listing *(read)*; Central metadata for `jte-maven-plugin`
  *(fetched)*; JMH README (uber-jar usage) via spike D §4;
  [golden-corpus.md — location rationale](../phase-1-cross-stack-foundation/golden-corpus.md#location-and-layout).

### D9 — JMH configuration pin: 1.37, explicit-defaults, return-value Blackhole, gc profiler, DCE plausibility rule

- **Decision.** JMH `1.37` (`jmh-core` + `jmh-generator-annprocess`, Central-verified latest).
  Every benchmark class carries the full explicit annotation set — JMH 1.37's defaults stated
  explicitly, per the JMH samples' state-your-settings discipline (JMHSample_13):

  ```java
  @BenchmarkMode(Mode.AverageTime)
  @OutputTimeUnit(TimeUnit.NANOSECONDS)
  @Fork(5)
  @Warmup(iterations = 5, time = 10, timeUnit = TimeUnit.SECONDS)
  @Measurement(iterations = 5, time = 10, timeUnit = TimeUnit.SECONDS)
  @Threads(1)
  @State(Scope.Benchmark)
  ```

  No custom `jvmArgs` (stock JVM flags; JMH records the VM line in its output header). Engines,
  models, and parity gates live in `@Setup(Level.Trial)` — every fork re-asserts its gates in
  the same process that produces its numbers (contract gate rule 2). Every `@Benchmark` method
  **returns the rendered `String`** — JMH funnels return values through a `Blackhole`
  (JMHSample_08/09 rule); nothing else is measured inside the method beyond one render into a
  fresh output buffer. GC/allocation figures come from the same run via `-prof gc`
  (`gc.alloc.rate.norm`, B/op). **DCE plausibility rule** (plan risk row 3, made executable):
  after the run, every cell's `ns/op` must be ≥ its workload's oracle `byteLength / 10`
  (i.e. the time to write the output at 10 bytes/ns ≈ 10 GB/s, generously above any real
  single-thread string-assembly rate) **and** each engine-track column must be monotonically
  ordered consistently with output size between `trivial-substitution`, `large-loop`, and
  `encoded-loop`; any violating cell is quarantined — not published — until the harness's
  result-consumption path is audited and the anomaly explained in writing.
- **Rationale.** The protocol maps JMH's `Mode.AverageTime` `Score` to "wall time per render"
  (Phase 1 D12) — average-time mode with ns output feeds the protocol's ns-normalized ratio
  column with no conversion. Explicit defaults are the most defensible pin: deviating from
  maintainer defaults would itself need evidence, and 5 forks is precisely JMHSample_13's
  run-to-run-variance aggregation guidance. Expected wall-clock for the full run:
  32 benchmarks × 5 forks × (50 s + 50 s) ≈ 4.5 h plus fork startup — an overnight run on the
  protocol box, recorded in the run procedure.
- **Alternatives rejected.** Fewer forks/iterations to shorten the run (trades the variance
  evidence the protocol publishes for convenience); `Mode.SampleTime`/`Throughput` (D12 pinned
  avgt); a `Blackhole` parameter instead of returning (equivalent per JMH docs, but returning
  is the samples' primary recommendation and reads cleaner); custom heap flags for the 1 MB
  encoded-loop outputs (defaults suffice; any tuning would be an undisclosed advantage knob).
- **Grounding.** JMH samples 08/09/12/13 (spike D §4, primary maintainer sources);
  [metrics-protocol — statistic mapping](../phase-1-cross-stack-foundation/metrics-protocol.md#wall-time-statistic-mapping-q21);
  Central metadata *(fetched)*; plan §Design direction (JMH discipline) and risk rows 2–3, 7.

### D10 — JDK pin: Eclipse Temurin 25 (LTS), exact build recorded per run

- **Decision.** The protocol-machine JVM is **Eclipse Temurin 25 (LTS), Windows x64**, the
  latest GA build available when the measurement run executes (at authoring time:
  `jdk-25.0.3+9`, Adoptium API-verified). The report's environment block records the exact
  `java -version` build string alongside JMH's auto-captured VM line; the JDK is the same for
  build (`maven.compiler.release=25`) and measurement. One JVM, steady state, no
  alternative-vendor or native-image columns (plan non-goal).
- **Rationale.** Spike D §5: Temurin 25 is the current LTS and the community's active
  recommendation; Temurin ships native Windows builds for the protocol box. Floating the patch
  level (rather than freezing `25.0.3+9` months before the run) takes current security fixes
  while the environment block preserves exact reproducibility — the same posture the .NET
  reports take toward SDK patch versions.
- **Alternatives rejected.** Temurin 21 (still supported, but 25 is the recommended default
  for new 2026 work — spike D); pinning the exact patch build in this spec (would be stale by
  run time; the run's report is the binding record).
- **Grounding.** Spike D §5; Adoptium API *(fetched, Assumed state)*;
  [metrics-protocol — machine and environment](../phase-1-cross-stack-foundation/metrics-protocol.md#machine-and-environment-q16).

### D11 — Gate runner: Java implementation of N1–N5, the verifier, and the exclusion procedure

- **Decision.** `benchmarks/jvm/` ships a self-contained gate runner
  (`heddle.benchmarks.jvm.gate` package): corpus loader (manifest + goldens + `.verify.json`,
  Jackson `jackson-databind:2.22.1` for JSON), normalization pipeline N1–N5 exactly per
  contract (UTF-8 strict decode failing on invalid bytes, BOM surviving; `\r\n`→`\n` then
  `\r`→`\n`; the `>ws+<`→`><` collapse over the six-character whitespace set as a single
  left-to-right pass; trim; N5 closed table, encoded only), byte comparison against the golden,
  and the verifier interpreter (values / ordered markers / forbidden / required with the
  contract's exact semantics, including forbidden checked against **both** raw and normalized
  output). Failure surface mirrors `ParityCheck.Describe`: workload id, engine, track, expected/actual
  byte lengths, first-diff index, ±40-char excerpt in a 120-char `\n`-escaped window. A
  `GateCli` main class runs: `probe` (D5's escaper probe), `gate` (all cells' gates, exit 0 iff
  all pass or are recorded exclusions), and `calibrate` (verifier self-calibration against the
  corpus goldens and the contract's synthesized corruptions — two per raw, three per encoded
  workload — proving the Java verifier implementation rejects what Phase 1's C# one rejects).
  Controlled-cell exclusion (Thymeleaf residual risk) follows
  [thymeleaf-controlled-feasibility.md — evidence procedure](thymeleaf-controlled-feasibility.md#exclusion-evidence-procedure):
  per-workload evidence records in `thymeleaf-exclusion-evidence.md` in this spec folder
  (created only if triggered), report cells print `excluded — documented evidence` linking to it.
- **Rationale.** Contract gate rule 2 requires the assertion in the same invocation as timing
  (satisfied by `@Setup(Level.Trial)` calling the same gate code) *and* the program needs a
  standalone reproduce/verify command (report reproduce block, CI-checkable) — one gate
  implementation serves both. Calibration is the proof the cross-language verifier
  reimplementation is faithful (the same standard `verify-corpus` sets intra-.NET).
- **Alternatives rejected.** Hand-rolled JSON parsing (error-prone for zero dependency
  savings in a non-timed path); invoking the .NET `verify-corpus` from Java (gates must run
  inside the JMH fork process); regex `\s` for N3 (the contract pins the six-character set
  precisely because `\s` varies — Java's `\s` is ASCII-only by default but the explicit set is
  the portable, contract-literal implementation).
- **Grounding.** [parity-contract-v2 — normalization pipeline](../phase-1-cross-stack-foundation/parity-contract-v2.md#normalization-pipeline),
  [controlled-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#controlled-track-gate),
  [idiomatic-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate);
  Central metadata for Jackson *(fetched)*.

### D12 — Report format: protocol shape, Heddle reference row, per-ecosystem sidebar, SAC 2026 note

- **Decision.** One date-stamped `docs/benchmarks/<yyyy-MM-dd>/` directory: `index.md` in the
  protocol's exact section order plus committed JMH artifacts (`jmh-result.json` from
  `-rf json`, full console log `jmh-log.txt`). Tables: per track (controlled / idiomatic, never
  mixed — presentation rule 5), rows = engine × workload with role labels
  `JTE (performance/peer)` / `Thymeleaf (credibility)`, columns = `Score` (ns/op), `Error`
  (99.9% CI) — the D12/Q2.1 mapping — plus the ratio column. Each wall-time table carries the
  **Heddle reference row** labeled exactly
  `Heddle (reference — .NET 10, same machine, from <date> run)`, excerpted from the Phase 1
  protocol run, dashes in all non-wall-time cells; the **ratio column anchors to that Heddle
  row** (Q6.2/Q2.2 = A), dimensionless since both sides are ns. *Interpretation recorded (rule
  1 vs rule 5 interplay):* presentation rule 1 mandates the Heddle reference row per workload
  and rule 2 anchors the ratio column to it, while rule 5 bars mixing tracks in one table —
  this spec reads the Heddle row as labeled reference metadata (an excerpt of the same
  published Phase 1 number, identical in both tables), not a track participant, so it appears
  in **both** wall-time tables and rule 5 continues to govern the measured engine rows; each
  caption states the Heddle figure's provenance. If Phase 7's aggregation reads rules 1/5
  differently, that is a protocol-text ambiguity to settle by ledger erratum against Phase 1's
  metrics protocol — not by silently diverging report layouts (trigger recorded; this layout
  is the reversible choice since dropping a reference row from one table edits one report
  section). The **allocation/GC sidebar**
  is a separate per-ecosystem section: `gc.alloc.rate.norm` B/op per cell, baseline =
  **Thymeleaf** (the credibility pick — Phase 1 D13's within-ecosystem baseline rule), headed
  by the protocol's verbatim allocation label; no table or prose juxtaposes it with any
  non-JVM number, and the Heddle row never appears in it. Encoded-suite results carry the
  verbatim confinement caveat adjacent to them. The report contains this **disclosed-limitations
  note, verbatim**:

  > **Disclosed limitation — isolated-microbenchmark JIT profiles.** These numbers come from
  > isolated JMH microbenchmarks. Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the
  > JVM" (ACM SAC 2026, arXiv:2605.23570), show that isolated JMH runs can settle into
  > tiered-JIT compilation profiles unrepresentative of the same code running inside a larger
  > application, and that warmup iteration counts alone do not buy profile realism. The figures
  > here are steady-state per-render measurements — internally consistent across the two
  > engines, which are measured under the identical regime — and are not a prediction of
  > in-application rendering performance.

  Honest-reporting rules 1–6 apply; in particular, every workload where Thymeleaf beats JTE, or
  where any JVM result complicates Heddle's positioning, is named with numbers in the results
  narrative (plan success criterion). The Thymeleaf adoption framing in the report prose is
  grounded as "the engine Spring Boot ships a dedicated auto-configuring starter for
  (`spring-boot-starter-thymeleaf`) — the Spring ecosystem's batteries-included default" with
  that starter's Central listing cited; **no numeric install-base claim is published** (spike D
  §7 found no citable number — the plan's flagged assumption closes as "positioning claim only,
  downgraded wording").
- **Rationale.** Every element is the Phase 1 protocol applied; the two local choices —
  sidebar baseline (Thymeleaf) and the downgraded adoption wording — follow Phase 1 D13 and the
  evidence actually found.
- **Alternatives rejected.** Publishing "largest install base" verbatim (unverifiable — spike D
  §7; honest-reporting rule 1's spirit); putting alloc columns in the wall-time tables (invites
  the cross-runtime read the metrics ruling bars); a JTE alloc baseline (D13 fixed the
  credibility pick program-wide).
- **Grounding.** [metrics-protocol — presentation rules and publication format](../phase-1-cross-stack-foundation/metrics-protocol.md#presentation-rules-q22--a-q62);
  [open-questions Q2.2, Q6.2, Q1.3](../../../plan/open-questions.md); plan §Design direction
  (reporting) and success criteria; spike D §7; arXiv:2605.23570 (plan external grounding).

### D13 — Cold parse/compile cost is not measured in this phase

- **Decision.** No cold-cost numbers are collected or published: JTE under D3 has no runtime
  parse step (AOT — a cold-cost column would be an empty cell or a class-load time, a different
  operation), and a Thymeleaf-only parse figure is a one-engine column with no within-ecosystem
  comparison to anchor it. Deferred with a trigger (see [Deferred items](#deferred-items)).
- **Rationale.** Q1.3 makes cold cost per-ecosystem-optional, not required; YAGNI cuts a
  column with one populated row. Reversible: adding it later is additive to a future report.
- **Alternatives rejected.** Thymeleaf-only parse benchmark (no comparator; invites exactly
  the AOT-vs-runtime-parse misread Q1.3 exists to prevent).
- **Grounding.** [open-questions Q1.3](../../../plan/open-questions.md);
  [metrics-protocol — metric rules](../phase-1-cross-stack-foundation/metrics-protocol.md#metric-rules) rule 3.

## Implementation plan

Ordered per the plan's internal ordering: Thymeleaf controlled feasibility first, then JTE
controlled, idiomatic, encoded validation, measurement, report. Full file trees, signatures,
and command lines in [harness-and-jmh.md](harness-and-jmh.md).

### WI1 — Harness skeleton, gate runner, escaper probe

- **Files.** New: `benchmarks/jvm/pom.xml`, `benchmarks/jvm/mvnw`/`mvnw.cmd` (+ wrapper jar),
  `benchmarks/jvm/README.md` (harness quick-start linking back to this spec),
  `benchmarks/jvm/src/main/java/heddle/benchmarks/jvm/model/Models.java`,
  `.../gate/Corpus.java`, `.../gate/Normalizer.java`, `.../gate/Verifier.java`,
  `.../gate/GateCli.java`, `.../jte/FiveEntityHtmlOutput.java`,
  `.../engines/JteEngines.java`, `.../engines/ThymeleafEngines.java`.
- **Change.** Pom per harness-and-jmh.md (pins: jte 3.2.4, thymeleaf 3.1.5.RELEASE, JMH 1.37,
  jackson-databind 2.22.1, shade 3.6.2, compiler 3.14.1, release 25; two jte `generate`
  executions per D3). Models transcribe the exact pinned data of
  [construct-mapping.md — models](construct-mapping.md#java-models). Gate runner per D11.
  Escaper probe per D5.
- **Done when.** `mvnw -q verify` builds `target/benchmarks.jar` on Temurin 25;
  `java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli probe` prints the D5
  table's spellings and exits 0; `... GateCli calibrate` accepts all eight goldens and rejects
  every synthesized corruption with the correct check kind.

### WI2 — Thymeleaf controlled track: feasibility ladder, then all eight cells

- **Files.** New: `benchmarks/jvm/src/main/resources/thymeleaf/controlled/*.html` (eight
  workload templates + `tile.html` + `layout.html`),
  `benchmarks/jvm/src/main/resources/composed-page/*.txt` (the fragment model resources — see
  construct-mapping.md §composed-page); possibly new (only on a beyond-whitespace failure):
  `docs/spec/cross-stack-benchmarks/phase-3-jvm/thymeleaf-exclusion-evidence.md`.
- **Change.** Author the block-only templates in the probe-ladder order of
  [thymeleaf-controlled-feasibility.md](thymeleaf-controlled-feasibility.md#the-feasibility-probe-ladder),
  byte-gating each workload via `GateCli gate --engine thymeleaf --track controlled
  --workload <id>` before moving to the next; on any beyond-whitespace divergence, execute the
  evidence procedure and continue the ladder (an excluded cell does not block the others).
- **Done when.** Every workload either passes the controlled byte gate or has a complete
  evidence record; the Q1.2 outcome for Thymeleaf is fully known and written down before WI3
  begins.

### WI3 — JTE controlled track

- **Files.** New: `benchmarks/jvm/src/main/jte-plain/controlled/*.jte` (six raw templates +
  `tile.jte` + `layout.jte`), `benchmarks/jvm/src/main/jte-html/controlled/*.jte` (two encoded
  templates).
- **Change.** Whitespace-free templates per construct-mapping.md; encoded cells render through
  `FiveEntityHtmlOutput` (D4); raw cells through the `Plain` engine into `StringOutput`.
- **Done when.** `GateCli gate --engine jte --track controlled` exits 0 for all eight
  workloads (byte gate + encoded security floor).

### WI4 — Idiomatic tracks for both engines

- **Files.** New only: `benchmarks/jvm/src/main/resources/thymeleaf/idiomatic/*.html`,
  `benchmarks/jvm/src/main/jte-plain/idiomatic/*.jte`,
  `benchmarks/jvm/src/main/jte-html/idiomatic/*.jte` (each file's header comment citing its
  official doc pages, D7). No `.NET`/spec file is edited: the D6 verifier-needle correction is
  already settled in Phase 1's golden-corpus.md, so Phase 1's implementation
  (`Runners/IdiomaticChecks.cs` → `export-corpus`) emits the corrected `.verify.json` files this
  WI gates against.
- **Change.** Idiomatic templates per construct-mapping.md; the two JTE idiomatic-encoded cells
  render through the stock `OwaspHtmlTemplateOutput` and gate against the D6-amended needles
  (already in the corpus).
- **Done when.** `GateCli gate --track idiomatic` exits 0 for all sixteen cells.

### WI5 — JMH benchmark classes and smoke run

- **Files.** New: `benchmarks/jvm/src/main/java/heddle/benchmarks/jvm/bench/
  {ComposedPage,TrivialSubstitution,LargeLoop,MixedPage,ConditionalHeavy,FragmentHeavy,
  FortunesEncoded,EncodedLoop}Bench.java`.
- **Change.** One class per workload, D9's annotation set, four `@Benchmark` methods
  (`jteControlled`, `jteIdiomatic`, `thymeleafControlled`, `thymeleafIdiomatic`) each returning
  the rendered `String`; `@Setup(Level.Trial)` builds engines/models once and asserts the
  cell's gate (controlled byte gate / idiomatic verifier) — a gate failure throws and the fork
  produces no numbers. Excluded cells (WI2's Thymeleaf-controlled feasibility outcome) have their
  methods omitted, with a code comment naming the evidence record.
- **Done when.** A smoke run
  (`java -jar target/benchmarks.jar -f 1 -wi 1 -i 1 -w 1s -r 1s -foe true`) executes every
  non-excluded benchmark method, every gate passes in-process, and a deliberately corrupted
  golden (temporary local edit) makes the affected fork abort with the D11 failure shape.

### WI6 — Protocol run and published report

- **Files.** New: `docs/benchmarks/<run-date>/index.md`, `.../jmh-result.json`,
  `.../jmh-log.txt`.
- **Change.** Execute the run procedure of
  [harness-and-jmh.md — run procedure](harness-and-jmh.md#measurement-run-procedure) on the
  protocol machine (quiet box, full D9 settings, `-prof gc`, JSON + log capture); apply the
  DCE plausibility rule to every cell; author `index.md` per D12 (environment block, reproduce
  commands, workloads section with dimensions and gate statements, results narrative under
  honest-reporting rules, files section).
- **Done when.** The directory exists with all artifacts; a checklist review against
  metrics-protocol.md finds every required element (Heddle row + label format, ratio anchor,
  track captions, allocation label, confinement caveat, SAC note, reproduce commands,
  environment block); every plausibility check passed or its cell is absent with a stated
  reason; the plan's success criteria all check off.

## Public API / contract

No Heddle public API is added or changed; **no .NET code is touched by this phase** (the D6
verifier-needle erratum is settled in the Phase 1 spec and materializes through Phase 1's own
implementation, not through any Phase 3 work item). All Java types are internal to the unpublished benchmark harness (no artifact
is deployed to any registry). Thread-safety: the harness is single-threaded by design
(`@Threads(1)`); engines and models are built once per fork in `@Setup` and never mutated.

Externally consumed surface created by this phase:

| Artifact | Consumers | Normative definition |
|---|---|---|
| `docs/benchmarks/<date>/` JVM report | Phase 7 consolidated report (JVM rows); readers | [metrics-protocol.md](../phase-1-cross-stack-foundation/metrics-protocol.md), D12 |
| `benchmarks/jvm/` harness + `GateCli` | Phase 8 Linux cross-check (re-runs it); CI | [harness-and-jmh.md](harness-and-jmh.md) |
| `benchmarks/<ecosystem>/` location convention | Phases 2, 4, 5, 6 harnesses | [D8](#d8--harness-lives-in-benchmarksjvm-maven-not-gradle) |

## Diagnostics / error surface

**HED\* compiler diagnostics: n/a** — no compiler, grammar, or engine surface changes; the
[diagnostic registry](../../common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
is untouched. The harness's own error surface:

| Surface | Type / exit | Message shape | Trigger |
|---|---|---|---|
| Benchmark-time controlled gate | `IllegalStateException` from `@Setup(Level.Trial)`; JMH aborts the fork, no numbers | `Controlled gate failed: <workload> / <engine>. first diff at index <i> (of exp <n>/act <m>).\n  expected: ...<±40-char excerpt>...\n  actual:   ...` | normalized candidate bytes ≠ corpus entry |
| Benchmark-time idiomatic gate | same | `Idiomatic gate failed: <workload> / <engine> check <value\|marker\|forbidden\|required>: expected <N> of "<needle>", found <M>` | any verifier check misses |
| Encoded security floor | same, security-specific | `Security floor failed: <workload> / <engine>: raw "<script>alert(" found <M> times (expected 0)` | raw payload in un-normalized output |
| `GateCli gate` | exit 1 | per-cell `[PASS]`/`[FAIL]`/`[EXCLUDED — documented evidence]` lines + summary | any non-excluded cell fails |
| `GateCli probe` | exit 1 | `[FAIL] <engine>/<context>: char '<c>' rendered "<got>", expected "<want>"` | escaper output off the D5 table |
| `GateCli calibrate` | exit 1 | `[FAIL] <workload> calibration: corruption '<kind>' was NOT rejected` | Java verifier accepts a corruption |
| Corpus not found / manifest hash mismatch | exit 2 | `Corpus not found at <path> (set -Dheddle.corpus=...)` / `Corpus entry '<id>' bytes do not match manifest sha256` | missing Phase 1 artifacts or local corruption |
| `FiveEntityHtmlOutput` context tripwire | `IllegalStateException` at gate time (never at timing — gates render first) | `Unexpected JavaScript escaping context <tag>/<attr> — controlled templates must not create one` | a template change introduces a `script`/event-handler user-content context |

## Testing plan

**TDD verdict.** As in Phase 1, the gates are the executable spec and run test-first by
construction: WI1 lands the gate runner and its calibration **before any template exists**;
each WI2/WI3/WI4 cell starts red under `GateCli gate` and turns green as its template is
authored; JMH `@Setup` re-asserts every gate in every fork. No JUnit suite is added — the
harness's gate commands are its test suite, mirroring the intra-.NET arrangement
(`Heddle.Tests` is untouched; no `src/` code changes).

**Named checks.**
- *Calibration:* `GateCli calibrate` — the Java verifier accepts all eight goldens and rejects
  the contract's synthesized corruptions (2 per raw, 3 per encoded workload) with the correct
  check kind — the faithfulness proof for the cross-language verifier reimplementation.
- *Escaper probe:* `GateCli probe` — D5's table asserted byte-for-byte at pinned versions
  before any encoded gate runs.
- *Gates:* `GateCli gate` (all 32 cells) and the same assertions in every fork's `@Setup`.
- *Negative/security:* the encoded security floor (`<script>alert(` zero raw occurrences)
  asserted per engine per track; the corrupted-golden smoke check (WI5 done-when) proves a
  stale/edited oracle aborts timing.
- *Benchmarks:* the eight `*Bench` classes; WI6's published run is the phase's benchmark
  evidence and the recorded baseline for Phase 8's Linux cross-check.

**Regression gate (before merge).**
1. `mvnw -q verify` in `benchmarks/jvm/` — clean build on Temurin 25.
2. `GateCli probe`, `GateCli calibrate`, `GateCli gate` — all exit 0 (exclusions, if any,
   recorded not failing).
3. JMH smoke run (WI5 command) completes with every non-excluded method measured.
4. .NET side unaffected: `dotnet build -c Release` and
   `dotnet run … -- parity` / `-- verify-corpus` still pass (they must — this phase changes
   nothing .NET-visible; the D6 verifier needles live in the Phase 1 spec and its own
   implementation).
5. No diff under `src/Heddle.Language/generated/` (no grammar change declared).

## Back-compat and migration

- **Additive only.** New top-level `benchmarks/jvm/` directory and one new
  `docs/benchmarks/<date>/` report; **no existing file is modified by this phase**. The D6
  verifier-needle erratum is corrected in the Phase 1 spec (unshipped, pre-implementation) and is
  strictly-weakening by construction (every replacement needle is a substring of its predecessor
  that remains unique in each workload's output), provably changing no existing engine's gate
  outcome (D6).
- **Golden corpus and contract v2: consumed read-only.** Any contract pressure found by this
  phase *at implementation time* is routed through the exclusion-evidence procedure (D1) or, for a
  post-ship change, the amendments ledger — never absorbed locally, per the plan's back-compat
  rules. (The one verifier-needle erratum surfaced during authoring was settled directly in the
  unshipped Phase 1 spec — D6.)
- **Published reports** (2026-07-11, 2026-07-18, Phase 1's run): immutable, untouched.
- **Heddle engine:** unchanged — measured via the corpus excerpt only.
- **No breaking window engaged** ([D2 — breaking windows](../../common/cross-cutting-decisions.md#d2--breaking-changes-land-only-in-ratified-breaking-windows)):
  all new surface.

## Performance considerations

- The harness's correctness machinery is **setup-time only**: gates, corpus loads, and JSON
  parsing run in `@Setup(Level.Trial)`; the timed region is exactly one cached-template render
  into a fresh output buffer, returning the `String` (Blackhole-consumed).
- Models are built once per fork and reused; `Context` (Thymeleaf) is reused across renders
  (D2); JTE renders into a fresh `StringOutput`/`FiveEntityHtmlOutput` per call — output-buffer
  allocation is part of a render everywhere in the program.
- `encoded-loop` produces ~1 MB per render; at 5 measurement iterations × 10 s × 5 forks this
  is GC-heavy by design (escaping throughput is the owned dimension); `gc.alloc.rate.norm`
  captures it per-ecosystem.
- The custom `FiveEntityHtmlOutput` (controlled encoded JTE) is a straight-line per-char
  switch, the same shape as stock `Escape` — it measures five-character escaping, which is the
  suite's dimension; the report's JTE controlled-encoded rows disclose the custom output.
- Guarding benchmarks: the eight `*Bench` classes; the DCE plausibility rule (D9) guards the
  numbers' validity; WI6's run is the recorded baseline.

## Standards compliance

- **DRY:** the gate/normalization/verifier logic exists once (`gate` package) and serves both
  `GateCli` and every JMH `@Setup`; composed-page fragment strings exist once as resource files
  consumed by both engines' models (never transcribed into template text twice); the D6 needle
  correction lives in Phase 1's single source (golden-corpus.md → `IdiomaticChecks.cs` → export),
  never hand-edited JSON.
- **YAGNI:** no cold-cost column (D13), no shared cross-ecosystem gate library (each ecosystem
  implements the contract against the exported artifacts — Phase 1's recorded stance), no
  Kotlin/GraalVM/Spring variants, no report-generation tooling (one report, authored manually
  like every published one).
- **Open/closed at the right seam:** engine escaping is adjusted through public seams
  (`HtmlTemplateOutput`) rather than forks or output rewriting; exclusions extend the report
  through the contract's own cell-level policy rather than bending the gate.
- **Tests exempt from DRY:** the eight benchmark classes deliberately repeat the
  setup/gate/render pattern per workload so a failing fork is diagnosable at a glance
  (same call as Phase 1's benchmark classes).

## Deferred items

| Item | Trigger |
|---|---|
| Per-ecosystem cold parse/compile figures for the JVM (D13) | A reader/maintainer request for Thymeleaf parse cost, or a second JVM-report edition where a within-ecosystem comparator exists |
| Thymeleaf exclusion-evidence records (`thymeleaf-exclusion-evidence.md`) | Created only if WI2's ladder hits a beyond-whitespace divergence (D1) |
| `jte-gradle-plugin` route | Only if the repo ever adopts Gradle elsewhere; D8's Maven decision stands otherwise |
| Kotlin (`.kte`) variant | Out of plan scope permanently unless the plan is amended (plan non-goal) |
| GraalVM native-image / alternative JVM vendor columns | Same — plan non-goal; a protocol change would have to come from a Phase 1 amendment |
| Ubuntu 24.04 re-run of this harness | Phase 8 (user ruling Q5.2); the harness is written to be re-runnable there unchanged (`mvnw`, relative corpus path) |

## External references

- JTE — engine, syntax, HTML rendering, precompiling, Maven plugin:
  <https://jte.gg/>, <https://jte.gg/syntax/>, <https://jte.gg/html-rendering/>,
  <https://jte.gg/pre-compiling/>, <https://jte.gg/maven-plugin/>
- JTE source (escaper tables, output seam, precompiled loading):
  <https://github.com/casid/jte> — `jte-runtime/src/main/java/gg/jte/html/escape/Escape.java`,
  `gg/jte/html/HtmlTemplateOutput.java`, `gg/jte/html/OwaspHtmlTemplateOutput.java`,
  `gg/jte/TemplateEngine.java` *(fetched 2026-07-20)*; inline-trim absence:
  <https://github.com/casid/jte/issues/69>
- Thymeleaf — 3.1 tutorial (standalone API, th:block, switch/case, inlining, fragments):
  <https://www.thymeleaf.org/doc/tutorials/3.1/usingthymeleaf.html>; source:
  <https://github.com/thymeleaf/thymeleaf> (`StandardTextTagProcessor` → unbescape)
- unbescape `escapeHtml4Xml` (HTML4 named-to-decimal, LEVEL_1; no `&apos;` in HTML4):
  <https://github.com/unbescape/unbescape>
- Maven Central metadata (version-of-record verification, fetched 2026-07-20):
  <https://repo1.maven.org/maven2/gg/jte/jte/maven-metadata.xml>,
  <https://repo1.maven.org/maven2/gg/jte/jte-maven-plugin/maven-metadata.xml>,
  <https://repo1.maven.org/maven2/org/thymeleaf/thymeleaf/maven-metadata.xml>,
  <https://repo1.maven.org/maven2/org/openjdk/jmh/jmh-core/maven-metadata.xml>
- JMH — repo and canonical samples (DCE/Blackhole/forking/run-to-run discipline):
  <https://github.com/openjdk/jmh> — samples 08 (dead code), 09 (blackholes), 12 (forking),
  13 (run-to-run variance)
- Schiavio, Bulej & Binder, "Misleading Microbenchmarks on the JVM" (ACM SAC 2026):
  <https://arxiv.org/abs/2605.23570>
- Marr, Daloze & Mössenböck, "Cross-Language Compiler Benchmarking: Are We Fast Yet?"
  (DLS 2016): <https://github.com/smarr/are-we-fast-yet>
- Eclipse Temurin releases / Adoptium API:
  <https://adoptium.net/temurin/releases>, <https://api.adoptium.net/>
- Spring Boot Thymeleaf starter (the adoption-positioning citation, D12):
  <https://central.sonatype.com/artifact/org.springframework.boot/spring-boot-starter-thymeleaf>
- mbosecke/template-benchmark lineage (harness precedent only — asserts no output equality):
  <https://github.com/mbosecke/template-benchmark>
- Phase 1 spec documents bound by this spec:
  [README](../phase-1-cross-stack-foundation/README.md),
  [workloads](../phase-1-cross-stack-foundation/workloads.md),
  [parity-contract-v2](../phase-1-cross-stack-foundation/parity-contract-v2.md),
  [golden-corpus](../phase-1-cross-stack-foundation/golden-corpus.md),
  [metrics-protocol](../phase-1-cross-stack-foundation/metrics-protocol.md)
- Repo conventions: [spec-conventions](../../common/spec-conventions.md),
  [testing-standards](../../common/testing-standards.md),
  [coding-standards](../../common/coding-standards.md),
  [cross-cutting-decisions](../../common/cross-cutting-decisions.md)
