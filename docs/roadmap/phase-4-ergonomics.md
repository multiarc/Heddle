# Phase 4 — Ergonomics Quick Wins

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Depends on: nothing hard; two items are nicer with phase 1 shipped. **Zero grammar change.**

## Goal

Remove the small daily frictions template authors hit constantly, at minimal engineering
cost: counted loops without C#, the `->` double‑render trap, preamble whitespace noise, and
the unreadable import spelling. Individually tiny; together they change how the language
*feels*.

## Work items

### 4.1 `@for` sugar — counted loops without C#

Today a counted loop requires embedded C# to construct the model:
`@for(@new ForModel() { Last = 3 })`. The extension
([ForIndexExtension.cs](../../src/Heddle/Extensions/ForIndexExtension.cs)) is
`[DataType(typeof(ForModel))]` and renders empty for any other input.

**Change:** accept an `int` model as "iterate 0 to N‑1":

```heddle
@for(3){{ <li>@out()</li> }}        @* needs phase 1 literals; renders 3 items, index chained *@
@for(Count){{ ... }}                @* works even without phase 1 — int member path *@
```

- `ProcessData`/`RenderData`: `int n` → behave as `ForModel { Last = n }`. Keep `ForModel`
  handling unchanged. (Confirmed: [ForModel](../../src/Heddle/Models/ForModel.cs) is
  `Start?`/`Last`/`Step?`, and the extension loop is `Last`‑exclusive —
  `for (i = Start ?? 0; i < Last; i += Step ?? 1)` — so `Last = n` yields 0…n‑1 as stated.)
- `[DataType]` validation: extend to accept both via a second `[DataType(typeof(int))]`
  attribute — **verified:** `DataTypeAttribute` is declared
  `[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]`
  ([DataTypeAttribute.cs](../../src/Heddle/Attributes/DataTypeAttribute.cs)), so stacking a
  second attribute works; no `InitStart` fallback needed.
- `@for(5)` with a *literal* requires phase 1 (literals become `ConstantParameter`);
  `@for(Count)` with an int member works with today's parameter tiers — ship the int
  handling regardless of phase 1 status.
- **If phase 1 has shipped:** add `range(start, last[, step])` → `ForModel` to the default
  function registry, giving `@for(range(2, 10, 2))` with zero new syntax.
- **Step validation (decided July 2026): `step == 0` is an error, `step < 0` is an
  error** — a positioned compile error where the value is statically known (literal), a
  runtime error otherwise. Rationale: with `ForIndexExtension`'s `Last`‑exclusive loop
  (`i += Step`), a zero step never terminates and a negative step either never terminates
  or silently never runs — both are bugs, never intent. A negative or zero *count*
  (`@for(-3)`, `@for(0)`) stays a natural empty render — the loop condition is false
  immediately, matching `Count = 0` data-driven cases where empty is the correct output.

Precedent: this is the ecosystem‑standard shape — Liquid loops over literal ranges
(`{% for i in (1..5) %}`) and Jinja2 ships `range([start, ]stop[, step])` as a default global
function; a registered `range()` returning `ForModel` follows Jinja2 exactly, down to the
stop‑exclusive semantics `ForIndexExtension` already implements.

### 4.2 Compile warning for the `->` double‑render trap

A definition with a default output (`-> chain`) renders at its declaration; calling it by
name (`@article_list()`) renders it a second time — the documented "don't also call it"
trap, currently silent.

**Change:** in [HeddleCompiler](../../src/Heddle/Runtime/HeddleCompiler.cs), when
`CreateExtension` resolves a call to a **definition** that carries a default output chain
(the `DefaultChains` pass, `Compile` lines ~65–92, knows which ones do), emit a
`HeddleCompileWarning`: *"Definition &lt;name&gt; has a default output (`->`) and is also
called by name — it will render twice. Remove the `->` or the call."* Respect override
layering: warn based on the definition instance resolved at that call site.

### 4.3 Opt‑in directive line trimming

Preambles today need `@\` on every line to avoid emitting blank lines:

```heddle
@using(){{System.Linq}}@\
@model(){{Blog}}@\
```

**Change:** `TemplateOptions.TrimDirectiveLines` (opt‑in, default `false` in 1.x;
**decided July 2026: flips to default‑on in 2.0**, bundled with the phase 2 profile flip so
hosts absorb one breaking window). When enabled: an output block or definition/import block that (a)
produces no output element (the blocks removed via `RemoveEmptyItem` — `@using`, `@model`,
`@profile` — plus `@% … %@` definitions and `@<<` imports) and (b) occupies a line by itself
(only whitespace before it on the line, only whitespace + newline after it), swallows that
trailing newline and the line's leading whitespace.

- Implementation seam: the compiler already removes these blocks and re‑shifts positions
  (`RemoveEmptyItem`, `RemoveDefinitions`, `ShiftBySkippedTokens`); extend the removed span
  over the adjacent whitespace/newline in the surrounding text pieces before
  `RuntimeDocument` slices them (`GetDocumentPieces`/`OptimizeCallTree`).
- `@\` keeps working unchanged; the option just makes it unnecessary in the common case.

Precedent: Liquid (`{%- -%}`), Handlebars (`~`), and Go templates (`{{- -}}`) all use per‑tag
trim markers, but whole‑line directive trimming matches Jinja2's `trim_blocks`/`lstrip_blocks`
most closely — and Jinja2 ships those as opt‑in engine options, off by default, which is
exactly the `TrimDirectiveLines` approach (per‑tag markers stay available there too, as `@\`
does here).

### 4.4 Document `@<<` vs `@import()` — two distinct imports

**Correction (source‑verified): `@<<` is *not* sugar for the `import` extension — they are
two independent implementations.** `@<<` is dedicated syntax: its own lexer token
(`fragment IMP: '@<<'` + `IMPORT_MODE`,
[HeddleLexer.g4](../../src/Heddle.Language/HeddleLexer.g4)) and parser rule (`import_block`,
[HeddleParser.g4](../../src/Heddle.Language/HeddleParser.g4)), handled at *parse* time in
`HeddleMainListener.ExitImport_block`
([HeddleMainListener.cs](../../src/Heddle/Language/HeddleMainListener.cs)): the imported file
is parsed into an isolated context, then definitions/default chains are merged and imported
output chains re‑based to the import site. `@import(){{path}}`
([ImportExtension.cs](../../src/Heddle/Extensions/ImportExtension.cs)) is an ordinary
by‑name extension call whose `InitStart` parses the file at compile/init time *directly into
the live `ParseContext`* — no isolation, no position re‑basing — and nothing in the repo
(fixture, test, or example) exercises `@import(` today.

**Decided (July 2026): keep both implementations and document the difference — no
delegation, no behavior change.** The two forms are presented as intentionally distinct
imports: `@<<{{ path }}` is the **composition import** (isolated parse, definitions merged,
positions re‑based — the canonical way to share definition libraries), and
`@import(){{ path }}` is the **inline‑parse variant** (the file is parsed directly into the
live context at compile time). Plan: (1) side‑by‑side fixtures pinning each form's *current*
behavior on the same imported file, documenting exactly where they diverge; (2) a
language‑reference section describing both forms and when to choose which; (3)
[patterns.md](../patterns.md) recipes use `@<<` for definition libraries.

## Back‑compat analysis

- 4.1: `@for(intValue)` previously rendered empty (silent no‑op) → now iterates. Strictly a
  fix of a silent failure; release note. `ForModel` behavior unchanged.
- 4.2: warning only — no behavior change. Hosts treating warnings as errors may need the
  fixture cleanup the warning is pointing at (that is the point).
- 4.3: opt‑in in 1.x — goldens unchanged when off. When on, only whitespace around
  zero‑output whole‑line blocks changes. The 2.0 default flip is a documented breaking
  change in the same window as the phase 2 profile flip (release note: set
  `TrimDirectiveLines = false` to keep 1.x whitespace byte‑exact).
- 4.4: docs + pinning fixtures only — zero behavior change; both forms keep their current
  semantics (decision: keep both, document the difference).

## Risks & mitigations

- **4.3 span arithmetic** interacting with `@\`‑eaten whitespace and hidden‑channel comment
  positions (`SkippedTokens` shifting) — the riskiest item of the four; mitigate with
  whitespace‑torture fixtures (directive + comment + `@\` on one line, CRLF and LF variants).
  (M)
- **4.2 false positives** with override layering (`<name:name>` re‑declarations where only
  one layer has `->`) — pin with fixtures for: base with `->` called before an override
  without it, and vice versa. (S)
- ~~4.1 `[DataType]` multiplicity assumption~~ — **resolved:** `AllowMultiple = true` is
  already set on `DataTypeAttribute`; stacked attributes work. (–)
- **4.4 divergence surprises** — with both forms kept, users may still assume equivalence;
  mitigation is exactly the deliverable: pinning fixtures + a docs section stating the
  difference and the choose‑which guidance. (S)

## Success criteria

1. `@for(3){{x}}` renders `xxx` and `@for(Count)` iterates `Count` times — both with
   `AllowCSharp = false`.
2. The double‑render fixture (`-> ` definition + by‑name call) produces exactly one warning
   with both positions referenced; removing either resolves it.
3. A preamble template renders without blank lines under `TrimDirectiveLines = true` and
   byte‑identically to today with it `false` (golden pair).
4. Both import forms' behaviors are pinned by side‑by‑side fixtures, and the
   language‑reference section documents the distinction and when to choose which.
5. Entire existing suite passes unchanged with all new options off.

## Validation scenarios

| Scenario | Expected |
| --- | --- |
| `@for(3){{<i>@out()</i>}}` | `<i>0</i><i>1</i><i>2</i>` (index on chained channel, as today) |
| `@for(Count)` with `Count = 0` | empty output (no iterations) |
| `@for(range(2, 8, 3)){{@out() }}` (phase 1 present) | `2 5 ` (start/step honored) |
| **Negative** `@for(range(1, 5, 0))` | error — zero step (compile‑time: literal) |
| **Negative** `@for(range(5, 1, -1))` | error — negative step (compile‑time: literal) |
| **Negative** `range` with model‑driven `Step = 0` | runtime error (not statically known) |
| `@for(-3)` | empty output (negative count = no iterations, like `Count = 0`) |
| **Negative** `@for(Name)` with `Name: string` | compile‑time type error (not a silent empty render) |
| `-> ` definition also called by name | warning; document renders (unchanged behavior) plus warning text names the definition |
| Preamble (`@using`+`@model`+comment) with TrimDirectiveLines on/off | no blank lines / byte‑identical to today |
| Directive followed by trailing spaces then CRLF | trimmed cleanly (CRLF and LF fixtures) |
| Whole‑line directive with `@\` already present + option on | no double‑trim (idempotent) |

## Testing

### Impact analysis

Four independent items; only 4.3 has real blast radius — span arithmetic on the
compile‑time removal seam, the same corruption class as phase 3's stripping (shared
torture corpus, shared review). 4.1 is a localized extension change, 4.2 is read‑only
compiler analysis (a warning cannot change output), 4.4 is docs + pinning fixtures with
zero behavior change by decision.

### Regression requirements

- Entire suite passes with all new options off (success criterion 5).
- Trim golden pair: byte‑identical to today with the option off; the on/off pair pinned
  (criterion 3).
- **Warning false‑positive scan**: 4.2's double‑render warning must fire zero times
  across the entire existing fixture corpus (any hit is a false positive by definition —
  the corpus predates the warning).
- The CRLF/LF/`@\`/comment torture corpus, shared with phase 3.

### TDD verdict

**Yes across the board — cheap to apply at this size:**

- 4.1's decision matrix (zero step → error, negative step → error, negative count →
  empty) lands as failing tests before the extension changes.
- 4.3's torture cases are written first *because* it is the risk item — each whitespace
  interaction is a red test before the span arithmetic exists.
- 4.4 is **characterization testing by definition**: capture the current behavior of both
  import forms as pinning fixtures *before* writing the docs that describe them.

### The verification loop

Per‑item mini‑loops — the four items are independent and land separately:

1. Write the item's failing spec (matrix tests / torture fixtures / pinning pair).
2. Implement until green.
3. Item gate: the item's own scenarios plus the shared regression gates (suite with
   options off, false‑positive scan for 4.2, golden pair for 4.3).
4. Red → fix code; a torture‑fixture expectation only changes with maintainer
   re‑ratification.
5. Exit per item; the phase exits when all four items' criteria are green together
   (one combined run — the items share the removal seam and must not interact).

### The final suite (including deferred phase 9 items)

- `range`/step error tests, the double‑render warning fixtures (including override
  layering), the trim torture corpus, and the import pinning pair as permanent suite
  members.
- **Deferred to [phase 9](phase-9-demo-and-integration.md):** no new sample — the
  current‑engine demos (`ssr-aspnetcore`, `definition-library`) are **upgraded** to use
  `@for` sugar and `TrimDirectiveLines`, so the gallery goldens keep exercising these
  features end‑to‑end (recorded in the phase 9 gallery notes).

## Size estimate

| Work item | Size |
| --- | --- |
| 4.1 `@for` int handling (+ `range` function if phase 1 present) | S |
| 4.2 double‑render warning | S |
| 4.3 directive line trimming | M (span arithmetic + fixtures) |
| 4.4 import forms: pinning fixtures + docs | S |
| Tests across all items | S/M |

## .NET 10 opportunities (net10.0 target)

Checked against [What's new in .NET libraries for .NET 10](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries),
[What's new in C# 14](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-14), and the
[.NET 10 performance post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/):
**essentially nothing in this phase is .NET 10‑specific.** .NET 10's string/span additions
(span normalization APIs, `CompareOptions.NumericOrdering`) don't touch line‑boundary
scanning, and the search APIs that *would* help 4.3 all predate it. The useful notes:

- **4.3 trimming spans:** `MemoryExtensions.IndexOfAnyExcept` is net7.0+ and
  `SearchValues<char>` net8.0+ (verified via API monikers) — neither exists on the
  `netstandard2.0`/`net6.0` targets. Since 4.3 scans a handful of chars around an
  already‑known block position at compile time, a plain `char` loop shared by all four
  TFMs beats an `#if`‑forked vectorized search; don't add conditional compilation for this.
- **`range()` bridge:** `System.Range`/`System.Index` are netstandard2.1/netcoreapp3.0+ —
  absent from the `netstandard2.0` target (only via the extra `Microsoft.Bcl.Memory`
  package) — and `Range` has no step while `Index` carries from‑end semantics `ForModel`
  doesn't model. Keep `ForModel` as the sole bridge type; `System.Range` buys nothing here.
- **C# 14:** built with the .NET 10 SDK under `LangVersion=latest`, all four TFMs compile
  as C# 14; the conveniences relevant to this phase (null‑conditional assignment, `field`,
  `nameof` on unbound generics) are compile‑time only and safe on every target.
- **4.2 diagnostic ID:** modern .NET libraries assign stable, individually suppressible
  prefixed IDs ([`SYSLIB0XXX` obsoletions / `SYSLIB1XXX` analyzer diagnostics](https://learn.microsoft.com/en-us/dotnet/fundamentals/syslib-diagnostics/obsoletions-overview));
  give the double‑render warning one (e.g. `HED0001`) — which means adding a code/ID
  property to [HeddleCompileWarning](../../src/Heddle/Data/HeddleCompileWarning.cs), which
  today carries only message/position/fix.

Runtime‑side .NET 10 gains (JIT escape analysis, stack‑allocated spans) accrue to the
`net10.0` target with zero code change from this phase.

## Open questions

None — all resolved July 2026 and integrated above: `TrimDirectiveLines` flips default‑on
in 2.0 bundled with the profile flip (4.3), `range` errors on zero and negative step while
a negative count renders empty (4.1), and the import forms stay two documented
implementations (4.4).

## External grounding

- Liquid `for` over literal ranges (`{% for i in (1..5) %}`) —
  <https://shopify.github.io/liquid/tags/iteration/>
- Jinja2 `range([start, ]stop[, step])` default global function and whitespace control —
  <https://jinja.palletsprojects.com/en/stable/templates/>
- Jinja2 `trim_blocks` / `lstrip_blocks` environment options (opt‑in, off by default) —
  <https://jinja.palletsprojects.com/en/stable/api/>
- Handlebars `~` whitespace control —
  <https://handlebarsjs.com/guide/expressions.html#whitespace-control>
- Go text/template `{{-` / `-}}` trimming (since Go 1.6) —
  <https://pkg.go.dev/text/template>

Internal findings above are grounded in repo sources rather than links:
`src/Heddle/Attributes/DataTypeAttribute.cs` (`AllowMultiple = true`),
`src/Heddle/Models/ForModel.cs` + `src/Heddle/Extensions/ForIndexExtension.cs`
(`Last`‑exclusive loop), and `src/Heddle.Language/HeddleLexer.g4` /
`src/Heddle.Language/HeddleParser.g4` / `src/Heddle/Language/HeddleMainListener.cs` vs
`src/Heddle/Extensions/ImportExtension.cs` (two separate import implementations).
