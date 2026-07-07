# Phase 8 — Streaming & Async Rendering API

> Part of the [Heddle evolution roadmap](README.md). Status: **planned**.
> Depends on: nothing (phase 7 amplifies it — a generated backend can emit UTF‑8 literals
> directly). **Zero grammar change** — engine API only.

## Goal

Render without materializing the whole output as a `string`: `Generate(model, TextWriter)`
and a UTF‑8 `Generate(model, IBufferWriter<byte>)` alongside today's `string Generate(model)`.
For high‑throughput SSR — Heddle's best benchmark story — the full‑string materialization and
the char→byte re‑encoding the host then performs are the remaining structural allocations.
Sink‑based rendering is the ecosystem norm here: Razor pages write to a `TextWriter`
(`RazorPageBase.Output`), and Fluid renders through a sink abstraction
(`IFluidOutput : IBufferWriter<char>`, with `TextWriter` overloads as extensions).

Async **model resolution** is explicitly evaluated and (lean) rejected for v1 — see the
decision record below.

## Design

### Sink abstraction

Today all output flows through [ScopeRenderer](../../src/Heddle/Data/ScopeRenderer.cs)
(`Render(string)` into a size‑adaptive buffer, `ExStringBuilder`/`LinearList` backing), with
`HtmlEncodedRenderer` as the encoding proxy (phase 2 pipeline) — and extensions write via
`scope.Renderer.Render(...)`. That public contract **must not change**.

- Introduce an internal sink seam under `ScopeRenderer`: the existing string‑buffer backing
  becomes one implementation; a `TextWriter` adapter and a UTF‑8 `IBufferWriter<byte>`
  adapter become the others. `Render(string)` keeps working against any sink.
- Additive API on the renderer for allocation‑free writes where callers have them:
  `Render(ReadOnlySpan<char>)`, and on modern TFMs value‑formatting overloads (
  `Render<T>(T value, string? format)` using `ISpanFormattable`/`IUtf8SpanFormattable` for
  ints/dates/decimals). **Decided (July 2026): the formatter built‑ins (`int`, `date`,
  `money`) migrate to span formatting *in this phase*, on the frameworks that support
  it** — specified per TFM: `net6.0`+ formats via `ISpanFormattable` (no intermediate
  string on the char paths), `net8.0`/`net10.0` additionally via `IUtf8SpanFormattable`
  on the UTF‑8 sink (no string, no char round‑trip), `netstandard2.0`/.NET Framework keep
  today's string‑based formatting (the interfaces don't exist there — documented, not
  worked around). Same formatting output on every TFM; only the allocation profile
  differs — pinned by running the formatter goldens per target.
- `HtmlEncodedRenderer` encodes **into** the sink (span‑based scan‑and‑copy) instead of
  producing intermediate encoded strings — same observable behavior, fewer allocations.
  [Phase 2](phase-2-safe-output.md) has **decided (July 2026)**: `HtmlEncoder` becomes the
  pluggable default in the 2.0 window — so this phase's UTF‑8 sink encodes directly via
  `EncodeUtf8(ReadOnlySpan<byte>, Span<byte>, …)`/span `Encode`, with no intermediate
  strings. The facts behind it, recorded here: `WebUtility.HtmlEncode` has exactly two
  overloads — `(string)` and `(string, TextWriter)` — no span or UTF‑8 input API; when a
  host pins the legacy `WebUtility` encoder via the phase‑2 option, encoded output on the
  UTF‑8 sink falls back through strings (documented cost).
- The `ProcessData` path (chains, `GetInnerResult`) legitimately materializes strings —
  value composition needs values; out of scope. Streaming wins apply to the `RenderData`
  path, which is already the engine's fast path.

### UTF‑8 path specifics

- **Pre‑encoded static text**: `RuntimeDocument`'s document pieces get lazily cached UTF‑8
  `byte[]` copies (per compiled document, computed once) — for text‑heavy templates most
  output bytes are then a straight `memcpy` into the buffer writer. **Decided (July 2026):
  the cache is unbounded and exposes no size/eviction options** — it is proportional to
  compiled template size (not data), lives and dies with the compiled document, and a
  configuration knob would be noise; revisit only on a concrete memory report.
- Dynamic values: encode via pooled spans (`Encoding.UTF8.GetBytes` into stackalloc/pooled
  buffers; `IUtf8SpanFormattable` on net8+ to skip the string entirely for primitives).
- Multi‑targeting (verified): `ISpanFormattable` exists on .NET 6+ and
  `IUtf8SpanFormattable` on .NET 8+ only — implemented by the primitives Heddle formats
  (`int`, `DateTime`, `decimal`, `Guid`, …); neither reaches `netstandard2.0`/`net48`.
  `IBufferWriter<T>` **is** available there — in‑box since netcoreapp2.1/netstandard2.1,
  and the System.Memory package targets netstandard2.0/net462 — so the `IBufferWriter`
  overload can ship on all TFMs. Downlevel it degrades honestly: pre‑encoded static pieces
  still win, but dynamic values fall back to string + `Encoding.UTF8.GetBytes` (document
  this; the full allocation win is net8+ only).

### Public API additions ([HeddleTemplate](../../src/Heddle/HeddleTemplate.cs))

```csharp
string Generate(object model);                                  // unchanged
void   Generate(object model, TextWriter writer);               // new
void   Generate(object model, IBufferWriter<byte> writer);      // new (UTF-8)
```

`callerData` overloads mirrored. No async render methods in v1 — sinks are synchronous by
design (see decision record); hosts needing backpressure wrap the buffer writer (e.g.
`PipeWriter` — which implements `IBufferWriter<byte>` — used synchronously between
flushes). That is the documented ASP.NET Core pattern, not a Heddle invention:
`HttpResponse.BodyWriter` *is* a `PipeWriter` that buffers writes until a flush, and the
official guidance for writing to it directly is to call `FlushAsync` manually — the host
renders synchronously into `Response.BodyWriter`, then
`await Response.BodyWriter.FlushAsync()` (or flushes periodically for very large pages).

### Decision record — async model resolution: **out of scope v1**

Heddle's model is *data in, text out*: templates never fetch. Making member access
awaitable would infect every extension signature (`ProcessData`/`RenderData` are sync, the
entire extension ecosystem is sync) for a need better served by the host resolving data
before rendering. Revisit only if concrete demand appears (e.g. streaming SSR with deferred
regions); record here when it does.

## Back‑compat analysis

- Purely additive public API; `string Generate` behavior and perf unchanged (string sink
  remains the default backing with its adaptive high‑water‑mark logic).
- `IExtension` authors: `scope.Renderer.Render(string)` keeps working unchanged; span/UTF‑8
  overloads are opt‑in. `HtmlEncodedRenderer` behavior identical (golden‑verified).
- Risk of behavioral drift between sinks is handled by running the golden suite through
  **all three** sinks.

## Risks & mitigations

- **Renderer refactor blast radius** (`ScopeRenderer`/`ExStringBuilder` are hot,
  perf‑tuned code): benchmark‑guard every step; the string path must show zero regression
  before any sink ships. (M)
- **Encoding correctness at chunk boundaries** (UTF‑8 encoding of values spanning pooled
  segments; surrogate pairs): the risk is real — a lone surrogate half is meaningless, so
  converting each chunk independently corrupts any pair straddling a boundary. Standard
  mitigation: encode each logical value in a single call where possible; where char input
  must be chunked, use the stateful `Encoder.Convert` loop (the `Encoder` saves state
  between calls and carries a trailing high surrogate over) — `System.Text.Rune` helps
  walk scalar values on modern TFMs but does not exist on .NET Framework. Dedicated
  fuzz‑ish tests with multi‑byte content (the docs' own `Café — Привет!` strings are the
  seed corpus). (M)
- **API surface creep** (async temptation): the decision record above is the guard. (S)

## Success criteria

1. Rendering the benchmark home page to a pooled `IBufferWriter<byte>` allocates **no
   full‑output string** (allocation assertions via BenchmarkDotNet `[MemoryDiagnoser]`),
   with total allocations measurably below the string path.
2. All three sinks produce **byte‑identical** output across the full golden corpus
   (UTF‑8 sink compared against `Encoding.UTF8.GetBytes(stringResult)`).
3. Encoding behavior (phase 2 profiles, `@html`/`@string`) identical across sinks —
   golden‑pinned.
4. `string Generate` shows zero perf/allocation regression vs the pre‑phase baseline.
5. Unicode torture fixtures (multi‑byte, surrogate pairs, mixed scripts) pass byte‑exact on
   the UTF‑8 path.
6. A sample ASP.NET Core minimal‑API endpoint renders via `PipeWriter` (as
   `IBufferWriter<byte>`) end‑to‑end — synchronous render into `Response.BodyWriter`,
   then `FlushAsync` (the writer buffers until flushed).
7. Formatter built‑ins (`int`, `date`, `money`) render identical output on every TFM with
   the span paths active where supported (`ISpanFormattable` on net6+,
   `IUtf8SpanFormattable` on net8+) — formatter goldens run per target.

## Validation scenarios

| Scenario | Expected |
| --- | --- |
| Benchmark home page → string vs TextWriter vs IBufferWriter | byte‑identical; allocation profile: buffer path lowest |
| Golden corpus through all three sinks | byte‑identical everywhere |
| `Html` profile + encoded values on UTF‑8 path | single‑encoded, byte‑exact vs string path |
| Large page (>1 MB output) | no LOH full‑output allocation on sink paths |
| Multi‑byte content (`Café — Привет!`, emoji, surrogate pairs) split across pooled segments | byte‑exact |
| Warm template repeated renders | string path keeps its adaptive‑buffer behavior (no regression) |
| netstandard2.0/net48 targets | TextWriter path works; documented UTF‑8 limitations |
| ASP.NET Core sample via PipeWriter | correct response, no intermediate string |

## Testing

### Impact analysis

The highest perf‑regression risk on the roadmap: `ScopeRenderer`/`ExStringBuilder` are
hot, perf‑tuned code that **every render crosses**, and the sink seam is cut directly
through them. `HtmlEncodedRenderer` changes encode mechanics (into‑sink instead of
intermediate strings). The public API additions are additive and low‑risk; the refactor
underneath them is the whole game.

### Regression requirements

- The string path shows **zero perf/allocation regression before any sink ships**
  (success criterion 4) — benchmark‑guarded at every refactor step, not at the end.
- Per‑runtime baselines: net8 and net10 runtime numbers differ by design (JIT escape
  analysis) — always compare like‑for‑like, refresh baselines per runtime.
- The golden corpus runs through **all three sinks** byte‑identically (criterion 2);
  encoding behavior pinned across sinks (criterion 3).
- netstandard2.0/net48: the documented degradation path is itself tested (TextWriter
  works; UTF‑8 value formatting falls back through strings).

### TDD verdict

**Characterization‑first, then test‑first — classic TDD would mislead here.** The
sequence matters: pin current behavior (goldens + allocation baselines) *before* cutting
the sink seam, because a refactor is only safe under a pinned oracle. The sink adapters
then develop against a ready‑made **property test**: UTF‑8 sink output ==
`Encoding.UTF8.GetBytes(stringResult)` across the whole corpus — an oracle stronger than
any hand‑written expectation. Chunk‑boundary encoding is adversarial‑fixture‑first
(surrogate pairs straddling pooled‑segment boundaries, written failing before the
`Encoder.Convert` loop exists). For the perf‑critical refactor itself, benchmarks — not
unit tests — are the gate.

### The verification loop

1. Capture the oracle: golden corpus + BenchmarkDotNet allocation baselines on the
   untouched renderer.
2. Cut **one** seam step (e.g. extract the backing interface); run goldens + benchmarks —
   both must hold exactly.
3. Repeat step 2 until the string sink is an implementation of the seam with zero delta.
4. Add each sink adapter test‑first against the property oracle; unicode adversarial
   fixtures before chunking logic.
5. Migrate `HtmlEncodedRenderer` and the formatter built‑ins (per‑TFM); re‑run the
   three‑sink corpus + per‑TFM formatter goldens.
6. Red → fix code; a baseline may only move with an explicit maintainer‑reviewed
   justification (perf regressions never land silently). Exit: success criteria 1–7.

### The final suite (including deferred phase 9 items)

- Three‑sink golden runs, the BenchmarkDotNet suite with `[MemoryDiagnoser]` per runtime,
  the unicode/surrogate corpus, netstandard2.0 degradation tests, and per‑TFM formatter
  goldens.
- **Deferred to [phase 9](phase-9-demo-and-integration.md):** the
  `samples/streaming-ssr` demo — synchronous render into `Response.BodyWriter` via
  `PipeWriter`, response bytes asserted in CI.

## Size estimate

| Work item | Size |
| --- | --- |
| Sink seam in `ScopeRenderer` + TextWriter adapter | M |
| UTF‑8 adapter + pre‑encoded static pieces + value formatting | M/L |
| `HtmlEncodedRenderer` span‑based encoding | S/M |
| Benchmarks + golden‑through‑sinks + unicode suite | M |
| Docs (`csharp-api.md`) + ASP.NET Core sample | S |

## .NET 10 opportunities (net10.0 target)

Researched July 2026 against official sources (.NET 10 is the current LTS, C# 14). The honest
headline: **this phase needs no `#if NET10_0_OR_GREATER` blocks at all.** The .NET 10 wins
arrive through two other gates — `LangVersion` 14 (applies to every TFM in the multi‑target)
and the .NET 10 *runtime* (applies to whichever build runs on it, including the net8.0 one).
The API‑level gating stays at net6/net8 exactly as designed above.

### C# 14 first‑class span conversions — call‑site ergonomics, not perf

- **Mechanism**: C# 14 makes array→`Span<T>`/`ReadOnlySpan<T>`, `Span<T>`→`ReadOnlySpan<T>`
  (covariant), and `string`→`ReadOnlySpan<char>` true implicit language conversions that
  participate in overload resolution, type inference, and extension‑method receivers;
  betterness now prefers `ReadOnlySpan<T>` over `Span<T>` —
  [first‑class span types spec (C# 14)](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-14.0/first-class-span-types).
- **Benefit for the sink surface**: `Render(ReadOnlySpan<char>)` becomes directly callable
  with `string`/`char[]` arguments — extension authors pass what they have, no `.AsSpan()`;
  span‑taking helpers don't need duplicate array/string overloads; renderer extension
  methods declared on `ReadOnlySpan<char>` now work on `string` receivers. Quantified
  honestly: **zero runtime perf delta** — the compiler lowers to the same conversion
  helpers (`MemoryExtensions.AsSpan(string)`, the span operators) that explicit calls used.
  This is API‑count and call‑site convenience only.
- **Conditionality**: gated on **`LangVersion >= 14`, not TFM** — the spec's codegen notes
  confirm the `string`→span conversion emits `MemoryExtensions.AsSpan`, which exists even
  on .NET Framework/netstandard2.0 via System.Memory. So the ergonomics apply to *all*
  Heddle TFMs once the repo builds with C# 14; nothing here motivates net10‑only API.
- **Overload‑safety notes** (from the spec's breaking‑changes list):
  `Render(string)` + `Render(ReadOnlySpan<char>)` is safe on every LangVersion — a `string`
  argument exact‑matches the `string` overload, so existing callers cannot silently
  reroute. But the new betterness rules exist **only at LangVersion ≥ 14**, and Heddle's
  NuGet consumers compile with their own LangVersion — so never design a public overload
  pair (e.g. `IEnumerable<T>` vs span) that needs C# 14 betterness to disambiguate;
  `[OverloadResolutionPriority]` (C# 13) is the escape hatch if a pair ever turns ambiguous.

### .NET 10 JIT — escape analysis covers this phase's fallback paths for free

- **Mechanism** (all .NET 10‑new per
  [What's new in the .NET 10 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)):
  stack allocation of small fixed‑size arrays of value types *and* of reference types;
  escape analysis through **local struct fields — which includes `Span<T>`**, so a span
  over a small non‑escaping array no longer forces it to the heap
  ([dotnet/runtime#113977](https://github.com/dotnet/runtime/pull/113977),
  [#116124](https://github.com/dotnet/runtime/pull/116124)); and **delegate escape
  analysis** — non‑escaping `Func` instances get stack‑allocated
  ([dotnet/runtime#115172](https://github.com/dotnet/runtime/pull/115172); the
  [.NET 10 perf post](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-10/)
  measures ~88 B → ~24 B and ~3× on its delegate benchmark).
- **Benefit here**: the paths this phase documents as honest fallbacks — string +
  `Encoding.UTF8.GetBytes` on downlevel value formatting, small transient buffers around
  the encoder — get cheaper with no code changes *when* the temporary is small,
  fixed‑size, and provably non‑escaping after inlining. That's a safety net for the rare
  paths not worth hand‑tuning; `stackalloc` and pooled buffers remain the primary tools,
  because anything crossing a non‑inlined call still escapes.
- **Conditionality**: **runtime‑gated, not TFM‑gated, no `#if`** — a net8.0 Heddle build
  running on the .NET 10 runtime gets these JIT wins too. Practical consequence for the
  success criteria: run the BenchmarkDotNet suite per‑runtime; allocation baselines for the
  same code will differ between the net8 and net10 runtimes.

### GC — nothing to change, one citable data point

.NET 10's GC‑relevant change is the new default **Arm64 write‑barrier** implementation —
the official runtime notes cite 8%–20% GC pause improvements under the new defaults
([What's new in the .NET 10 runtime](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/runtime)).
DATAS‑by‑default is .NET 9‑era, not 10. Nothing in .NET 10 changes LOH thresholds or
large‑object behavior for the >1 MB scenario — the phase's mitigation (chunked pooled
segments, no full‑output LOH allocation) is exactly as necessary on net10 as on net8.

### Negative findings — verified not .NET 10‑specific

- **UTF‑8/Encoding/MemoryExtensions**: the
  [.NET 10 libraries what's‑new](https://learn.microsoft.com/en-us/dotnet/core/whats-new/dotnet-10/libraries)
  adds **no** new Encoding/UTF‑8 conversion API relevant to the render path (its UTF‑8
  items are `Convert` hex↔UTF‑8 and `PemEncoding.FindUtf8`; strings got span‑based
  normalization — none apply here). Everything this phase leans on stays net6/net8‑era:
  `ISpanFormattable` (net6), `IUtf8SpanFormattable`/`Encoding.TryGetBytes`/`Utf8.TryWrite`
  (net8) — already covered by the TFM gating above.
- **SearchValues**: no new API surface in .NET 10; `SearchValues<char>/<byte>/<string>`
  are net8‑era. If the `HtmlEncodedRenderer` scan‑and‑copy uses `SearchValues`, gate it
  `NET8_0_OR_GREATER` — .NET 10 only makes existing usage transparently faster.
- **ArrayPool**: no .NET 10‑specific `ArrayPool` changes in the official what's‑new or
  perf material; nothing to adopt or gate.
- **System.IO.Pipelines**: no new `PipeWriter`/pipelines API in .NET 9 or 10.
  [`PipeWriter.UnflushedBytes`/`CanGetUnflushedBytes`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter.unflushedbytes)
  — the right primitives for the ASP.NET Core sample's "flush periodically for very large
  pages" logic — are .NET 6‑era
  ([dotnet/runtime#48913](https://github.com/dotnet/runtime/issues/48913)), available on
  every supported runtime. One adjacent behavior note: the .NET 9+ `JsonSerializer`
  `PipeWriter` paths throw on custom writers that don't implement `UnflushedBytes`
  ([dotnet/aspnetcore#65511](https://github.com/dotnet/aspnetcore/issues/65511)) — Heddle
  only *consumes* `IBufferWriter<byte>` and is unaffected, but the sample's docs shouldn't
  encourage wrapping `Response.BodyWriter` in a custom `PipeWriter` without forwarding it.

## Open questions

None — both resolved July 2026 and integrated above: the formatter built‑ins migrate to
span formatting in this phase with the per‑TFM support matrix specified (Sink
abstraction), and the pre‑encoded‑UTF‑8 cache is unbounded with no configuration surface
(UTF‑8 path specifics).

## External grounding

- [`IUtf8SpanFormattable` API](https://learn.microsoft.com/en-us/dotnet/api/system.iutf8spanformattable) —
  .NET 8+ only; `TryFormat(Span<byte>, out int, ReadOnlySpan<char>, IFormatProvider)`;
  implemented by `Int32`, `DateTime`, `DateTimeOffset`, `Decimal`, `Double`, `Guid`, ….
- [`ISpanFormattable` API](https://learn.microsoft.com/en-us/dotnet/api/system.ispanformattable) —
  .NET 6+ only; absent from netstandard2.0/net48 — hence the TFM gating above.
- [`IBufferWriter<T>` API](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) —
  in‑box since netcoreapp2.1/netstandard2.1 (System.Memory.dll/netstandard.dll); the
  [System.Memory package](https://www.nuget.org/packages/System.Memory) targets
  netstandard2.0 and net462, covering Heddle's downlevel TFMs.
- [`PipeWriter` API](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter) —
  `public abstract class PipeWriter : System.Buffers.IBufferWriter<byte>`; `FlushAsync`
  "makes bytes written available to `PipeReader`".
- [Request and response operations in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/request-response) —
  `HttpResponse.BodyWriter` is a `PipeWriter` that "buffers data until a flush operation
  is triggered"; "call `PipeWriter.FlushAsync` manually"; pipelines recommended over
  streams for performance.
- [`TextEncoder` API (base of `HtmlEncoder`)](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.textencoder) —
  `Encode(TextWriter, …)`, span `Encode(ReadOnlySpan<char>, Span<char>, …)`,
  `EncodeUtf8(ReadOnlySpan<byte>, Span<byte>, …)`, `TryEncodeUnicodeScalar`; the
  System.Text.Encodings.Web package reaches netstandard2.0.
- [`WebUtility.HtmlEncode` API](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode) —
  overloads are `(string)` and `(string, TextWriter)` only; no span or UTF‑8 input.
- [`Encoder.Convert` API](https://learn.microsoft.com/en-us/dotnet/api/system.text.encoder.convert) —
  "the `Encoder` object saves state between calls", including "the high surrogate of a
  surrogate pair"; "designed to be used in a loop" over chunked input. Background:
  [Character encoding in .NET](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction)
  (surrogate halves are meaningless alone; "the `Rune` type isn't available in
  .NET Framework").
- [Fluid — `IFluidOutput`](https://github.com/sebastienros/fluid/blob/main/Fluid/IFluidOutput.cs) —
  `IFluidOutput : IBufferWriter<char>`, "a buffered, flushable output target for template
  rendering"; [`IFluidTemplate.RenderAsync`](https://github.com/sebastienros/fluid/blob/main/Fluid/IFluidTemplate.cs)
  renders into it, with `TextWriter` overloads as extensions.
- [`RazorPageBase.Output` API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.razor.razorpagebase.output) —
  "Gets the `TextWriter` that the page is writing output to" — Razor renders to the
  response writer, never a full string.
