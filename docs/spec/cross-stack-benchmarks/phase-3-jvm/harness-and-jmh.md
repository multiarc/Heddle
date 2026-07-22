# Harness and JMH — layout, build, gates, measurement, report assembly

Supplementary document of the [Phase 3 — jvm spec](README.md). It pins the `benchmarks/jvm/`
harness end-to-end: directory layout, the Maven build (with the JTE precompilation
executions), engine construction, the gate runner's implementation contract, the JMH benchmark
shape, the measurement-run procedure on the protocol machine, and how the published report is
assembled. Decisions of record: README
[D2](README.md#d2--thymeleaf-engine-setup-standalone-315release-classloader-resolver-cached-templates-reused-context),
[D3](README.md#d3--jte-controlled-track-whitespace-free-authoring-trimcontrolstructures--false-generate-goal-aot),
[D8](README.md#d8--harness-lives-in-benchmarksjvm-maven-not-gradle)–[D12](README.md#d12--report-format-protocol-shape-heddle-reference-row-per-ecosystem-sidebar-sac-2026-note).

## Directory layout

```
benchmarks/jvm/
  pom.xml
  mvnw / mvnw.cmd / .mvn/wrapper/…        ← committed Maven wrapper
  README.md                                ← quick-start; links to this spec
  src/main/java/heddle/benchmarks/jvm/
    model/Models.java                      ← all pinned models (construct-mapping.md)
    engines/JteEngines.java                ← the two precompiled JTE engines + render helpers
    engines/ThymeleafEngines.java          ← the two Thymeleaf engines + reused Contexts
    jte/FiveEntityHtmlOutput.java          ← D4 custom HtmlTemplateOutput
    gate/Corpus.java                       ← manifest/golden/.verify.json loading + sha256 check
    gate/Normalizer.java                   ← N1–N5 (incl. N3b), contract-literal
    gate/Verifier.java                     ← values/markers/forbidden/required interpreter
    gate/GateCli.java                      ← probe | gate | calibrate verbs
    bench/<Workload>Bench.java             ← 8 JMH classes (WI5)
  src/main/jte-plain/
    controlled/ *.jte  (composed-page, layout, trivial-substitution, large-loop,
                        mixed-page, conditional-heavy, fragment-heavy, tile)
    idiomatic/  *.jte  (same eight names)
  src/main/jte-html/
    controlled/ fortunes-encoded.jte, encoded-loop.jte
    idiomatic/  fortunes-encoded.jte, encoded-loop.jte
  src/main/resources/
    thymeleaf/controlled/*.html            ← eight + tile.html + layout.html
    thymeleaf/idiomatic/*.html
    composed-page/*.txt                    ← fragment model resources (construct-mapping.md)
```

Corpus path resolution: default `../../src/Heddle.Performance/GoldenCorpus/` relative to the
harness working directory (`benchmarks/jvm/`), overridable with
`-Dheddle.corpus=<absolute-or-relative path>`; `Corpus` verifies every entry's bytes against
the manifest's `sha256` at load and fails fast (exit 2) on mismatch or absence.

## Maven build (`pom.xml`)

Coordinates `heddle.benchmarks:jvm-benchmarks:1.0-SNAPSHOT`, never deployed. Pins (all
Central-verified, README Assumed state):

| Property / artifact | Version |
|---|---|
| `maven.compiler.release` | `25` |
| `jte.version` → `gg.jte:jte` (runtime dep) + `gg.jte:jte-maven-plugin` | `3.2.4` |
| `org.thymeleaf:thymeleaf` | `3.1.5.RELEASE` |
| `jmh.version` → `org.openjdk.jmh:jmh-core` (compile) + `jmh-generator-annprocess` (annotation processor path) | `1.37` |
| `com.fasterxml.jackson.core:jackson-databind` | `2.22.1` |
| `maven-compiler-plugin` | `3.14.1` |
| `maven-shade-plugin` | `3.6.2` |

Build steps:

1. **`jte-maven-plugin`, two `generate` executions** (phase `generate-sources`, per D3), both
   with `trimControlStructures` left at default `false`:

   | execution id | `sourceDirectory` | `contentType` | `packageName` |
   |---|---|---|---|
   | `jte-plain` | `${project.basedir}/src/main/jte-plain` | `Plain` | `heddle.jte.gen.plain` |
   | `jte-html` | `${project.basedir}/src/main/jte-html` | `Html` | `heddle.jte.gen.html` |

   Generated sources are added to the compile source path and compiled by javac into the jar
   (generate-mode behavior, jte.gg/pre-compiling); the `controlled/`/`idiomatic/`
   subdirectories become subpackages, so the two executions cannot collide.
2. **Compile** with release 25; JMH annotation processor on `annotationProcessorPaths`.
3. **Shade** (phase `package`): one `target/benchmarks.jar`, main class
   `org.openjdk.jmh.Main`, service-file transformer enabled (JMH + Thymeleaf resources merge
   cleanly). `GateCli` is invoked with `java -cp target/benchmarks.jar …` (no second jar).

## Engine construction (once per fork, in `@Setup(Level.Trial)`)

```java
// JTE — AOT classes from the application class loader (D3):
TemplateEngine jtePlain = TemplateEngine.createPrecompiled(null, ContentType.Plain, null, "heddle.jte.gen.plain");
TemplateEngine jteHtml  = TemplateEngine.createPrecompiled(null, ContentType.Html,  null, "heddle.jte.gen.html");
// (verify at implementation — README D3: null classDirectory ⇒ class-loader loading;
//  documented fallback recorded there.)

// One render, raw suite:            one render, controlled encoded (D4):
StringOutput out = new StringOutput();            FiveEntityHtmlOutput out = new FiveEntityHtmlOutput();
jtePlain.render("controlled/mixed-page.jte",      jteHtml.render("controlled/encoded-loop.jte",
    Models.MIXED, out);                               Models.ENCODED_ITEMS, out);
return out.toString();                            return out.toString();
// Idiomatic encoded renders pass a plain StringOutput — the engine wraps it in
// OwaspHtmlTemplateOutput itself (stock path).

// Thymeleaf (D2):
ClassLoaderTemplateResolver r = new ClassLoaderTemplateResolver();
r.setPrefix("thymeleaf/"); r.setSuffix(".html");
r.setTemplateMode(TemplateMode.HTML); r.setCacheable(true);
TemplateEngine thymeleaf = new TemplateEngine();   // org.thymeleaf.TemplateEngine
thymeleaf.setTemplateResolver(r);
Context ctx = new Context();                       // built once per workload, reused
ctx.setVariable("rows", Models.CONDITIONAL_ROWS);
// One render:
StringWriter w = new StringWriter();
thymeleaf.process("controlled/conditional-heavy", ctx, w);
return w.toString();
```

`FiveEntityHtmlOutput` contract (D4): implements `gg.jte.html.HtmlTemplateOutput` over a
`StringBuilder`; `writeContent` overloads append verbatim; `writeUserContent(String)` escapes
`& < > " '` → `&amp; &lt; &gt; &quot; &#39;` in every non-JavaScript context; primitive
`writeUserContent` overloads append `String.valueOf`; `setContext(tag, attr)` records context
and throws `IllegalStateException` on a `script` tag or `on*` attribute (tripwire — no such
context exists in the templates).

## Gate runner implementation contract (D11)

`Normalizer` — contract-literal, in this order, nothing else:

| Step | Implementation |
|---|---|
| N1 | `new String(bytes, StandardCharsets.UTF_8)` after a strict-decode check (`CharsetDecoder` with `CodingErrorAction.REPORT`); invalid UTF-8 ⇒ gate failure; a leading U+FEFF is **kept** |
| N2 | replace `"\r\n"` → `"\n"`, then `'\r'` → `'\n'` |
| N3 | single left-to-right scan collapsing every run of `{TAB, LF, VT, FF, CR, SPACE}` between `>` and `<` to nothing (explicit char-set scan, not `\s`) |
| N3b | remove every run of `{TAB, LF, VT, FF, CR, SPACE}` anywhere to **nothing** (not to a space; 2026-07-20 maintainer step) from **both** the normalized candidate and the loaded golden at comparison time — so the gate compares non-whitespace bytes and any whitespace-only divergence (run-length or presence/absence) passes; N3/N4 remain only to shape the stored golden |
| N4 | trim leading/trailing characters from the same six-character set |
| N5 | encoded suite only: the contract's closed replacement table (named entities case-sensitive; numeric refs with any leading zeros, case-insensitive hex and `x`), single pass, replacements never rescanned |

Comparison: `Arrays.equals(stripWs(normalized).getBytes(UTF_8), stripWs(golden).getBytes(UTF_8))`, where `stripWs` applies N3b (removes every whitespace run). Encoded suites
additionally assert the security floor on the **un-normalized** candidate: zero
`<script>alert(` and the expected count of the escaped form after N5.

`Verifier` — consumes `<id>.verify.json` (Jackson), semantics exactly per the
[idiomatic-track gate](../phase-1-cross-stack-foundation/parity-contract-v2.md#idiomatic-track-gate):
normalize N1–N4 (+N5 encoded); `values` = exact non-overlapping counts; `markers` = strictly
ordered occurrence; `forbidden` = zero in raw **and** normalized; `required` = min counts in
normalized; failure reports kind + needle + expected/found.

`GateCli` verbs (all exit-code gated; used by CI, the reproduce block, and humans):

```
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli probe
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli gate [--engine jte|thymeleaf] [--track controlled|idiomatic] [--workload <id>]
java -cp target/benchmarks.jar heddle.benchmarks.jvm.gate.GateCli calibrate
```

`gate` with no filters runs all 32 cells; cells recorded as excluded (feasibility-doc
procedure) print `[EXCLUDED — documented evidence]` and do not fail the exit code.
`calibrate` synthesizes the contract's corruptions from the goldens (removed row / reordered
sections per workload as pinned in
[golden-corpus.md — verification](../phase-1-cross-stack-foundation/golden-corpus.md#verification),
plus unescaped payload for encoded) and requires each to be rejected with the correct check
kind.

## JMH benchmark shape (D9)

One class per workload, e.g.:

```java
@BenchmarkMode(Mode.AverageTime)
@OutputTimeUnit(TimeUnit.NANOSECONDS)
@Fork(5)
@Warmup(iterations = 5, time = 10, timeUnit = TimeUnit.SECONDS)
@Measurement(iterations = 5, time = 10, timeUnit = TimeUnit.SECONDS)
@Threads(1)
@State(Scope.Benchmark)
public class ConditionalHeavyBench {
    // engines + contexts, built in setup
    @Setup(Level.Trial)
    public void setup() {
        // build engines/models; then, in the same process that will be timed:
        Gates.assertControlled("conditional-heavy", "jte",       this::renderJteControlled);
        Gates.assertControlled("conditional-heavy", "thymeleaf", this::renderThymeleafControlled);
        Gates.assertIdiomatic ("conditional-heavy", "jte",       this::renderJteIdiomatic);
        Gates.assertIdiomatic ("conditional-heavy", "thymeleaf", this::renderThymeleafIdiomatic);
    }
    @Benchmark public String jteControlled()       { return renderJteControlled(); }
    @Benchmark public String jteIdiomatic()        { return renderJteIdiomatic(); }
    @Benchmark public String thymeleafControlled() { return renderThymeleafControlled(); }
    @Benchmark public String thymeleafIdiomatic()  { return renderThymeleafIdiomatic(); }
}
```

Rules the classes must observe:

- Every `@Benchmark` method **returns the rendered `String`** (JMH's implicit Blackhole —
  JMHSample_08/09); the method body is exactly one render into a fresh output buffer.
- Gates run in `@Setup(Level.Trial)` — once per fork, in the timed process, before any
  iteration (contract gate rule 2); a gate throw aborts the fork with no numbers.
- Excluded cells: the corresponding `@Benchmark` method and its gate line are omitted, with a
  comment naming the evidence record.
- No benchmark-level `@Fork(jvmArgs=…)`, no `@CompilerControl`, no per-class deviations from
  the shared annotation set — one regime for all 32 cells (internal consistency is the basis
  of the report's engine-vs-engine claims).

## Measurement-run procedure

On the protocol machine only (Windows 11 / Ryzen 9 9950X — Q1.6), Temurin 25 latest GA (D10):

1. Quiet the box exactly as for the published .NET runs (no interactive use, background apps
   closed); record `java -version`, `mvnw -version`, Windows build, and repo commit for the
   environment block.
2. `cd benchmarks/jvm && ./mvnw -q clean verify` (build + `GateCli calibrate`/`gate` wired to
   the Maven `verify` phase via exec — a failed gate fails the build; the run never starts on
   a red gate).
3. Full run (single invocation, all benchmarks, GC profiler, JSON + log capture):

   ```
   java -jar target/benchmarks.jar -prof gc -rf json -rff jmh-result.json | Tee-Object jmh-log.txt
   ```

   Expected duration ≈ 4.5–5.5 h (32 benchmarks × 5 forks × 100 s measured time + startup);
   run unattended.
4. **DCE plausibility pass** (D9, mandatory before publication): for every cell check
   `ns/op ≥ oracle byteLength / 10` and the size-ordering consistency across
   `trivial-substitution` < `large-loop` < `encoded-loop` per engine-track column; quarantine
   any violation (cell absent from the report with a stated reason) until audited.
5. Copy `jmh-result.json` and `jmh-log.txt` into the new `docs/benchmarks/<yyyy-MM-dd>/`
   directory and author `index.md`.

## Report assembly (D12)

`index.md` sections, in the protocol's order:

1. `# Benchmark run — <yyyy-MM-dd>` and an intro naming the suites and both reproduce
   commands (`./mvnw -q clean verify` and the step-3 JMH invocation).
2. `## Environment` — fenced block: JMH 1.37; exact Temurin build string (JMH's `# VM version`
   line plus `java -version` output); Windows 11 build; Ryzen 9 9950X; jte 3.2.4 /
   Thymeleaf 3.1.5.RELEASE; Maven + wrapper version; `Commit <shorthash>`.
3. `## The workloads` — one bullet per workload: id, owned dimension (from Phase 1
   workloads.md), suite, and which gate ran per cell (controlled byte gate / idiomatic
   verifier / `excluded — documented evidence` with link); the verbatim encoded confinement
   caveat sits with the encoded bullets.
4. `## Results — what this run actually shows` — narrative + tables:
   - two wall-time tables (one per track, captioned `controlled` / `idiomatic`), rows
     `JTE (performance/peer)` and `Thymeleaf (credibility)` per workload, columns `Score`
     ns/op, `Error` (99.9% CI), ratio; each table carries the Heddle reference row labeled
     `Heddle (reference — .NET 10, same machine, from <date> run)` (wall-time cells only,
     dashes elsewhere), and the ratio column anchors to it;
   - the allocation/GC sidebar (separate section): `gc.alloc.rate.norm` B/op per cell,
     baseline column anchored to Thymeleaf, headed by the verbatim allocation label; no
     Heddle row, no non-JVM number anywhere near it;
   - the verbatim SAC 2026 disclosed-limitations note (D12);
   - prose naming, with numbers, every workload where Thymeleaf beats JTE and every result
     that complicates Heddle's positioning (honest-reporting rule 2 + plan success criterion);
   - the role framing: compiled-AOT vs runtime-markup engine differences stated as expected
     design consequences, not a ranking verdict; the JTE controlled-encoded rows note the D4
     custom output; the Thymeleaf adoption sentence uses the downgraded starter-based wording
     (D12).
5. `## Files` — links to `jmh-result.json`, `jmh-log.txt`.

Cross-checks before publishing: every honest-reporting rule 1–6; presentation rules 1–5;
Q1.3/allocation scoping (no cold-cost numbers, no cross-runtime juxtaposition); the plan's
success-criteria checklist ticked line by line.
