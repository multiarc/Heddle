# Phase 8 specification — streaming & async rendering

**Status: Specified — ready for implementation.**

- Roadmap source: [Phase 8 — Streaming & Async Rendering API](../../roadmap/phase-8-streaming-async.md)
- Assumed prior phases (per [D5](../common/cross-cutting-decisions.md#d5--sequential-implementation-order)):
  [phase 1](../phase-1-native-expressions/README.md),
  [phase 2](../phase-2-safe-output/README.md),
  [phase 3](../phase-3-branching/README.md),
  [phase 4](../phase-4-ergonomics/README.md),
  [phase 5](../phase-5-props-and-slots/README.md),
  [phase 6](../phase-6-tooling-lsp/README.md),
  [phase 7](../phase-7-build-time-compilation/README.md)
  (with [generated-code.md](../phase-7-build-time-compilation/generated-code.md) as the interop contract)
- Common specs: [conventions](../common/spec-conventions.md) ·
  [coding standards](../common/coding-standards.md) ·
  [testing standards](../common/testing-standards.md) ·
  [cross-cutting decisions](../common/cross-cutting-decisions.md)

This spec is a single document; there are no supplementary files.

## Scope and goal

Render a compiled template into a `TextWriter` or a UTF-8 `IBufferWriter<byte>` sink with
no full-output string materialization: `Generate(model, TextWriter)` and
`Generate(model, IBufferWriter<byte>)` alongside today's `string Generate(model)`, which
stays byte- and allocation-identical. Zero grammar change; engine API only. The phase also
migrates the formatter built-ins to span formatting per TFM (ratified in the roadmap), and
delivers the byte-sink fast path for phase 7's pre-encoded `"…"u8` static pieces —
phase 8 is the consumer [cross-cutting D4](../common/cross-cutting-decisions.md#d4--utf-8-static-piece-emission-u8-belongs-to-phase-7)
shaped that emission for.

Out of scope, decided below: async render methods (D8), async model resolution (D8),
UTF-8 caching of runtime-document static pieces (D6), and any change to the
`ProcessData` value-composition path (D12).

## Assumed state

All claims in this section were re-verified against source in July 2026. Corrections to
roadmap/architecture-doc claims are marked **[correction]** and carried into the affected
decision records.

- **The renderer seam.** [`IScopeRenderer`](../../../src/Heddle/Data/IScopeRenderer.cs)
  is a shipped public interface with exactly two members: `void Render(string data)` and
  `string ToString()` (the latter is vestigial — it is satisfied by `object.ToString()`
  and [`HtmlEncodedRenderer`](../../../src/Heddle/Data/HtmlEncodedRenderer.cs) already
  ships without overriding it; nothing in the engine calls `ToString()` through the
  interface). [`Scope`](../../../src/Heddle/Data/Scope.cs) — since phase 3 a declared
  `readonly struct` with the internal `ScopeLocals` frame — carries
  `public readonly IScopeRenderer Renderer` and the public proxy transform
  `RenderProxy(IScopeRenderer)`, which `AbstractHtmlExtension.RenderData` uses to install
  `HtmlEncodedRenderer` for `RenderType.Encode` carriers.
- **The string sink.** [`ScopeRenderer`](../../../src/Heddle/Data/ScopeRenderer.cs) is
  `#if`-split: on `NET8_0_OR_GREATER` it appends to a `StringBuilder` (capacity-seeded);
  downlevel it collects a `List<string>` and concatenates via
  [`ExStringBuilder.Concat`](../../../src/Heddle/Strings/ExStringBuilder.cs)
  (`FastAllocateString` + span copies). **[correction]** The roadmap's "size-adaptive
  buffer, `ExStringBuilder`/`LinearList` backing" is stale for modern TFMs: the net8+/
  net10 backing is a plain `StringBuilder`, `LinearList<T>` is an `internal` list type in
  `Heddle.Data` not referenced by the render path, and `ExStringBuilder` serves the
  render path only through `Concat` on downlevel targets and in `Execute`
  (`NormalStrategy`/`OptimizedStrategy`, `ListExtension.ProcessData`).
- **The high-water logic lives in the template, not the renderer.**
  [`HeddleTemplate.Generate`](../../../src/Heddle/HeddleTemplate.cs) creates a fresh
  `ScopeRenderer` per render seeded from `volatile int _maxLength` (net8+, chars) or
  `_maxElementCount` (downlevel, fragment count), renders, then raises the field with a
  10 % margin (`newMax * 110 / 100`, capped `int.MaxValue / 2` on net8+). It also
  maintains `_runners`/`_disposeAfterComplete` bookkeeping and a `#if DEBUG` model-type
  check. `Generate`'s signature is
  `string Generate(object data, object chained = null, object callerData = null)`, and
  the root scope is `new Scope(data, callerData, data, chained, renderer)` carrying the
  phase 3 root locals frame (`_runtimeDocument.NeedsLocals ? new ScopeLocals() : null`,
  per phase 3 WI3 — both `Generate` scope sites provision it).
- **The render/value funnel.** `IProcessStrategy` (public since phase 7) has
  `void Render(in Scope)` — the streaming path writing through `scope.Renderer` — and
  `string Execute(in Scope)` — the value path used by chain non-terminals
  ([`TemplateChain.RenderData`](../../../src/Heddle/Runtime/TemplateChain.cs) calls
  `ProcessData` on every item except the last) and by
  `AbstractExtension.GetInnerResult`. Verified: **`Execute`/`ProcessData` never touch
  `scope.Renderer`** — string building there goes through `string[]` +
  `ExStringBuilder.Concat` ([`RuntimeDocument`](../../../src/Heddle/Runtime/RuntimeDocument.cs)
  `NormalStrategy`/`OptimizedStrategy`) or `string.Concat`. The two paths are already
  cleanly separated at the interface.
- **[correction] One render-path full materialization exists:**
  [`PartialExtension.RenderData`](../../../src/Heddle/Extensions/PartialExtension.cs)
  renders `scope.Renderer.Render(InnerTemplate?.Generate(scope.ModelData, scope.ChainedData))`
  — the partial's entire output becomes a string even on the streaming path. The roadmap
  claim that "streaming wins apply to the `RenderData` path, which is already the
  engine's fast path" holds everywhere else (`ListExtension.RenderData` streams per item
  through `RenderInnerResult`; `SwapExtension`, `IfExtension`, bodies, chains all write
  through the funnel), but `@partial` breaks the no-materialization guarantee unless
  fixed — D11.
- **Encoding call sites (phase 2 D11).** Exactly two, both `WebUtility.HtmlEncode`:
  [`AbstractHtmlExtension.ProcessData`](../../../src/Heddle/Core/AbstractHtmlExtension.cs)
  and `HtmlEncodedRenderer.Render`. Phase 2's D11 forbids introducing a third in 1.x and
  reserves the pluggable `TextEncoder` swap for the 2.0 window.
- **Formatter built-ins, verified per extension.** All five format via
  `value.ToString(format[, culture])` into `scope.Renderer.Render(string)` from
  `RenderDataInternal`/`RenderData`, with the value path (`ProcessDataInternal`/
  `ProcessData`) producing the same string as a return value. The per-extension state D10
  migrates, re-read from source:
  - [`IntegerExtension`](../../../src/Heddle/Extensions/IntegerExtension.cs) (`int`) —
    `AbstractHtmlExtension`, `[EncodeOutput]`, `[DataType(typeof(int))]` +
    `[DataType(typeof(long))]`. Three branches: `int`-typed model, `long`-typed model,
    then a `Convert.ChangeType(model, typeof(long), InvariantCulture)` coercion whose
    `InvalidCastException`/`OverflowException`/`FormatException` are caught and render
    nothing. Format string from `GetInnerResult(scope.Parent())`; the empty-format branch
    calls `ToString(CultureInfo.InvariantCulture)` (general `"G"` semantics), non-empty
    calls `ToString(format, CultureInfo.InvariantCulture)`.
  - [`DateExtension`](../../../src/Heddle/Extensions/DateExtension.cs) (`date`) —
    `[EncodeOutput]`, `[DataType(typeof(DateTime))]`; `DateTime`-only (any other model
    renders nothing — no coercion attempt), format defaulted to `"d"` when the inner
    result is empty, always `CultureInfo.InvariantCulture`.
  - [`MoneyExtension`](../../../src/Heddle/Extensions/MoneyExtension.cs) (`money`) —
    `[EncodeOutput]`, `[DataType(typeof(decimal))]`. The inner result is a **culture
    name, not a format**: the format string is always `"c"`; a static
    `ConcurrentDictionary<string, CultureInfo>` cache (`OrdinalIgnoreCase`) resolves the
    locale, and the no-locale branch calls `decimalValue.ToString("c")` — **thread-current
    culture**, a deliberate per-type quirk this phase preserves exactly. Non-`decimal`
    models go through `Convert.ChangeType(…, typeof(decimal), InvariantCulture)` with
    `InvalidCastException`/`FormatException` rendering nothing (no `OverflowException`
    catch here, unlike `int` — also preserved).
  - [`TimeExtension`](../../../src/Heddle/Extensions/TimeExtension.cs) (`time`) —
    `[EncodeOutput]`, and it formats **`DateTime`, not `TimeSpan`**
    (`[DataType(typeof(DateTime))]`; an `is DateTime` test guards the render); default
    format `"t"`, always `CultureInfo.InvariantCulture`.
  - [`GuidExtension`](../../../src/Heddle/Extensions/GuidExtension.cs) (`guid`) — a plain
    `AbstractExtension` with **no `[EncodeOutput]`** (always a raw carrier; it never
    installs an encode proxy of its own — `Guid` output is HTML-inert and
    culture-insensitive). No culture parameter anywhere:
    `guid.ToString(GetInnerResult(parentData))`, where an empty inner result yields the
    `"D"` format by `Guid.ToString`'s documented contract and an invalid specifier throws
    `FormatException` at render time (existing behavior, preserved).
  - [`StringExtension`](../../../src/Heddle/Extensions/StringExtension.cs) (`string`) —
    `[EncodeOutput]` carrier, **not a formatter**: it renders string models directly (or
    a `Convert.ChangeType`-to-string coercion, falling back to `ToString()` on
    `InvalidCastException`) and renders its inner body when the model is null. There is
    no format step to migrate; D10 records it as out of the funnel's scope.
- **Public template surface.** `HeddleTemplate` is `sealed` and implements
  [`IHeddleTemplate`](../../../src/Heddle/IHeddleTemplate.cs), a shipped public interface
  declaring `string Generate(...)` — adding members to it is a binary break for external
  implementers (coding-standards interface rule).
- **Phase 7 artifacts this phase binds to** (from
  [generated-code.md](../phase-7-build-time-compilation/generated-code.md), re-verified
  against its audited July 2026 form): the piece table `internal const string PN` with
  opt-in u8 twins `internal static ReadOnlySpan<byte> PNU8 => "…"u8;` (emitted when
  `HeddleEmitUtf8Pieces=true`, LangVersion ≥ 11, no unpaired surrogate — HED7005
  downgrades); `PrecompiledRuntime.GenerateString(IProcessStrategy, object, object, object)`
  — whose signature is pinned with **no locals parameter**: when a document root hosts
  branch participants, the generated `Root` field arrives *already wrapped* in
  `PrecompiledRuntime.WithLocalsFrame(new Body0())` at its field initializer, so the
  phase 3 locals frame **rides the strategy, not the entry point** (phase 7's audited
  correction — an earlier draft's "GenerateString provisions the frame" was retracted
  there); the typed entry shape
  `public static string Generate(Shop.Product model, object chained = null, object callerData = null)
  => PrecompiledRuntime.GenerateString(Root, model, chained, callerData);`;
  `PrecompiledCapabilities.Utf8Pieces` (enum member, `{ None = 0, StringOutput = 1,
  Utf8Pieces = 2 }`); manifest `schemaVersion` 1 with the registration gate rejecting
  unsupported versions (HED7102). Generated `Render` bodies write pieces as
  `scope.Renderer.Render(PN);` under `#line hidden` — the exact call site D7's emitter
  update rewrites. The byte-path emitter sketch in generated-code.md
  (`sink.Write(P3U8)` directly) is explicitly non-normative and assigned to this phase.
- **Benchmarks.** [`TextRenderBenchmarks`](../../../src/Heddle.Performance/TextRenderBenchmarks.cs)
  (`[MemoryDiagnoser]`) renders the component-heavy home page via
  [`HeddleTest`](../../../src/Heddle.Performance/Runners/HeddleTest.cs) against the Razor
  equivalent. This is the standing workload every sink benchmark reuses.
- **Spans on downlevel TFMs.** The `netstandard2.0` build already compiles `Span<char>`
  code (`ExStringBuilder`) via the *transitive* System.Memory dependency of
  `Microsoft.CodeAnalysis.CSharp`; [Heddle.csproj](../../../src/Heddle/Heddle.csproj) has
  no direct reference. WI2 pins the reference explicitly. The package facts this spec's
  per-TFM stories rest on, verified July 2026:
  - The System.Memory package (latest 4.6.3) targets `netstandard2.0` and `net462`, and
    its reference assembly contains `IBufferWriter<T>` (`Advance`/`GetMemory`/`GetSpan`)
    **and** `BuffersExtensions` (including
    `Write<T>(this IBufferWriter<T>, ReadOnlySpan<T>)`) — both reach every Heddle TFM.
  - `ArrayBufferWriter<T>` is **not** in the package — it is in-box from
    netcoreapp3.0/netstandard2.1 only. Consequence carried into WI2: the net48 test lane
    cannot use `ArrayBufferWriter<byte>` and runs the adapter tests over the test-owned
    buffer writers instead (same assertions, same fixtures).
  - `TextWriter.Write(ReadOnlySpan<char>)` exists on netcoreapp2.1+/netstandard2.1 only —
    absent from `netstandard2.0`/`net48` (verified against the API's applies-to list);
    the `TextWriterScopeRenderer` downlevel span path therefore rents from
    `ArrayPool<char>` and calls `Write(char[], int, int)` (present everywhere).
  - `Encoder.Convert`/`Encoding.GetBytes` span overloads are netcoreapp2.1+/
    netstandard2.1 only; the **pointer** overloads
    (`Convert(char*, int, byte*, int, bool, out, out, out)`,
    `GetBytes(char*, int, byte*, int)`) reach `netstandard2.0` and every .NET Framework
    target (verified applies-to). [Heddle.csproj](../../../src/Heddle/Heddle.csproj)
    already sets `<AllowUnsafeBlocks>true</AllowUnsafeBlocks>`, so the `fixed` fallbacks
    compile without a project change.

## Design decisions

### D1 — The sink seam is the existing `IScopeRenderer`; the string path is not refactored

**Decision.** No internal seam is cut inside `ScopeRenderer`. The sink abstraction is the
already-shipped `IScopeRenderer` interface that `Scope.Renderer` is typed as: the string
path keeps `ScopeRenderer` exactly as it is (both `#if` branches untouched), and the two
new sinks arrive as sibling implementations — `TextWriterScopeRenderer` and
`Utf8ScopeRenderer` (D2) — chosen by the `Generate` overload that starts the render.
`HtmlEncodedRenderer` already proves the pattern: it is a proxy `IScopeRenderer` threaded
via `Scope.RenderProxy`.

**Rationale.** The roadmap's success criterion 4 (zero string-path regression) is
satisfied *by construction*: not one instruction on the string path changes. Extensions
already dispatch `scope.Renderer.Render(string)` through the interface, so no new
indirection is added anywhere. **[correction]** The roadmap's "introduce an internal sink
seam under `ScopeRenderer`: the existing string-buffer backing becomes one
implementation" described a refactor that verification shows is unnecessary — the seam it
wants already exists as the public interface, and cutting a second, internal one would
add a virtual hop to the hottest path in the engine for zero capability gain
(precedence rule 4).
**Alternatives rejected.** An internal `IOutputSink` inside `ScopeRenderer` (adds an
indirection per `Render` call on the string path; the benchmark gate would have to prove
a zero-cost abstraction instead of proving nothing changed); making `ScopeRenderer`
abstract with three subclasses (a binary-compat hazard — `ScopeRenderer` is public and
instantiable today).

### D2 — Capability interfaces `ISpanScopeRenderer` / `IUtf8ScopeRenderer`; `IScopeRenderer` never changes

**Decision.** Two additive public interfaces in `Heddle.Data`:

```csharp
public interface ISpanScopeRenderer : IScopeRenderer
{
    /// <summary>Renders a character span. Equivalent to Render(new string(data)) with fewer allocations.</summary>
    void Render(ReadOnlySpan<char> data);
}

public interface IUtf8ScopeRenderer : ISpanScopeRenderer
{
    /// <summary>Writes pre-encoded UTF-8 bytes verbatim. Callers must pass valid UTF-8 —
    /// the engine only calls this with compiler-validated "…"u8 pieces (phase 7 D15).</summary>
    void RenderUtf8(ReadOnlySpan<byte> utf8);
}
```

`IScopeRenderer` itself gains no members (shipped-interface rule,
[coding standards](../common/coding-standards.md#api-design-and-compatibility)); the
phase 6 `PublicApiSurfaceTests` golden is updated additively in the same change.
Capability discovery is a type test (`renderer is IUtf8ScopeRenderer u8`), used in
exactly three places: `ScopeRendererExtensions` (D10), `PrecompiledRuntime.WritePiece`
(D7), and extension authors who opt in. `HtmlEncodedRenderer` implements
`ISpanScopeRenderer` (D9) but **not** `IUtf8ScopeRenderer` — pre-encoded bytes must never
bypass an encode proxy, and the type test makes that fall out automatically: under a
proxy, `WritePiece` takes the string branch and the bytes get encoded.

**Rationale.** Interface segregation exactly as the coding standards prescribe for
shipped interfaces; default interface members are not an option because `netstandard2.0`
and `net48` lack runtime support. The `IUtf8ScopeRenderer : ISpanScopeRenderer`
derivation reflects reality (a byte sink can always accept chars by transcoding) and
keeps the type-test ladder one level deep.
**Alternatives rejected.** Members on `IScopeRenderer` (binary break for external
implementers — `HtmlEncodedRenderer`-style user proxies exist as a documented pattern);
an abstract `ScopeRendererBase` class (forces a base class on user proxies);
generic-virtual `Render<T>` members on the interface (generic virtual dispatch cost, and
formatting belongs in one static helper, D10).

### D3 — Public surface: sink overloads on `HeddleTemplate` only; the one source-compat corner recorded

**Decision.** Two new methods on `HeddleTemplate` (signatures normative, `callerData`
mirrored per the roadmap):

```csharp
public void Generate(object data, TextWriter writer, object chained = null, object callerData = null);
public void Generate(object data, IBufferWriter<byte> writer, object chained = null, object callerData = null);
```

`ArgumentNullException` on `writer == null` (host-programming error); `data`/`chained`/
`callerData` semantics identical to the string overload (null model remains legal).
`IHeddleTemplate` is **not** widened — the sink methods live on the sealed class only,
and extracting a streaming interface is deferred until a DI-abstraction consumer exists
(Deferred items). Behavior: same compile-guard exceptions as the string path
(`TemplateInitException`/`TemplateCompileException`/`ObjectDisposedException`), same
`_runners` bookkeeping, no flush and no dispose of the sink (D4).

The one known compatibility corner is recorded: a call written as the *positional
literal* `template.Generate(data, null)` compiled against 1.x re-compiles against this
phase as **CS0121** (ambiguous between the `TextWriter` and `IBufferWriter<byte>`
overloads — both are better than `object` for `null`, neither is better than the other).
This is compile-time-only and self-announcing; the fix is `Generate(data)` or
`Generate(data, chained: null)`. Binary compatibility is unaffected (existing assemblies
keep binding the string overload). Accepted per the .NET library breaking-change
taxonomy, where overload additions of this class are sanctioned source-level risks;
the migration note ships in `csharp-api.md` (WI10). No typed argument can silently
reroute: any expression statically typed `object` still exact-matches the string
overload, and a statically-typed `TextWriter`/`IBufferWriter<byte>` argument is new code.

**Rationale.** Mirroring the ratified roadmap surface keeps `Generate` one family with
one parameter order (`data` first, like every existing overload). The ambiguity corner is
strictly better than the single-overload alternative (with only one sink overload,
`Generate(data, null)` would *silently rebind* and throw `ArgumentNullException` at run
time — a worse failure than a compile error).
**Alternatives rejected.** Widening `IHeddleTemplate` (binary break); distinct names
(`GenerateTo`, `RenderTo` — splits one operation across two names for a corner case;
Fluid solves this with writer-first parameters, but Heddle's established family puts the
model first and consistency wins); `[OverloadResolutionPriority]` on the string overload
(it would make the string overload beat the sink overloads for *every* call, since
`TextWriter`→`object` is always applicable — it cannot express "prefer only for null").
**Grounding.** [Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes)
(source-breaking overload additions vs binary breaks);
[Fluid `IFluidTemplate.RenderAsync`](https://github.com/sebastienros/fluid/blob/main/Fluid/IFluidTemplate.cs)
(writer-first precedent, considered and not followed).

### D4 — Sink semantics: write-through, no engine buffering, no flush, no dispose; high-water state is string-path-only

**Decision.** Both sink renderers write **through** on every `Render`/`RenderUtf8` call —
the engine adds no buffering layer of its own. For `TextWriterScopeRenderer` that means
`writer.Write(...)` per call (the `TextWriter` supplies whatever buffering it has — a
`StreamWriter`'s internal buffer, ASP.NET Core's 16 KB `HttpResponseStreamWriter`, a
`StringWriter`'s builder); for `Utf8ScopeRenderer` it means bytes land in
writer-provided `GetSpan` segments and are committed with `Advance` before the call
returns — the `IBufferWriter<byte>` *is* the buffer. The engine never calls `Flush`,
`FlushAsync`, `Complete`, or `Dispose` on a sink: the host owns the sink's lifecycle,
which is exactly the `Response.BodyWriter` contract (buffers until the host flushes).
Write-through is what makes ordering correct by construction: renderer proxies
(`HtmlEncodedRenderer`) and direct piece writes interleave in call order with no
cross-buffer reordering hazard.

The adaptive high-water state (`_maxLength`/`_maxElementCount`) is **neither read nor
updated** by sink renders. It sizes the string path's output buffer; sink renders have no
output buffer. Cross-feeding sink-observed sizes into the string path's seed would couple
the two paths for a speculative win and would make string-path behavior depend on which
other overloads ran — rejected outright. The warm-template validation scenario ("string
path keeps its adaptive-buffer behavior") is therefore trivially preserved.

**Rationale.** Every layer of engine-side buffering would need its own flush semantics,
error story, and pool policy — all owned better by the sink the host chose (the
`IBufferWriter` contract exists precisely so producers don't buffer privately). The
high-water rule keeps `string Generate` bit-for-bit deterministic against its pre-phase
behavior.
**Alternatives rejected.** An internal pooled staging buffer in front of `TextWriter`
(duplicates what `StreamWriter`/`HttpResponseStreamWriter` already do; hosts that pass
an unbuffered writer get documented guidance instead of hidden state); auto-flush after
render (breaks the PipeWriter pattern where the host composes multiple writes before one
`FlushAsync`); updating `_maxLength` from sink renders (couples paths, races with
concurrent string renders for no measured benefit).
**Grounding.** [Request and response operations in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/request-response)
(`BodyWriter` "buffers data until a flush operation is triggered"; call
`PipeWriter.FlushAsync` manually); [System.Buffers overview](https://learn.microsoft.com/en-us/dotnet/standard/io/buffers)
(`IBufferWriter<T>` is "a contract for synchronous buffered writing").

### D5 — UTF-8 transcoding mechanics for char input (normative algorithm)

**Decision.** `Utf8ScopeRenderer` transcodes char input with two tiers, split at an
internal constant `MaxUtf8SizeHint = 16 * 1024` bytes (= 16 384).

1. **Single-call tier** — when `data.Length * 3 <= MaxUtf8SizeHint` (3 is the UTF-8
   worst case per UTF-16 code unit; surrogate pairs produce 4 bytes per 2 units, ≤ 3·L):
   one `GetSpan(data.Length * 3)`, one `Encoding.UTF8.GetBytes(chars, span)` (span
   overload on the net6.0/net8.0/net10.0 builds; netstandard2.0/net48: `fixed` pointers
   with `GetBytes(char*, int, byte*, int)` — the pointer overload reaches both targets,
   and `AllowUnsafeBlocks` is already on), one `Advance(bytesWritten)`. No surrogate can
   split — the whole logical value converts in one call. The tier boundary in chars:
   values up to **5 461** UTF-16 units take this tier (5 461 × 3 = 16 383 ≤ 16 384;
   5 462 × 3 = 16 386 tips into tier 2).
2. **Chunked tier** — larger inputs run the stateful `Encoder.Convert` loop: a lazily
   created per-renderer `Encoder` (`Encoding.UTF8.GetEncoder()`, created on first use,
   reused across chunked writes; the renderer is per-render and single-threaded, D15)
   converts successive slices directly into `GetSpan(MaxUtf8SizeHint)` segments,
   `Advance` per iteration, with `flush: true` on the final chunk so no state ever
   crosses a `Render` call boundary. The `Encoder` carries a trailing high surrogate
   between iterations — this is the documented mitigation for pair-splitting at chunk
   boundaries.

**The chunked loop, normatively** (net6+ shape; the downlevel form differs only in
calling the pointer overload of `Convert` under `fixed` and slicing manually):

```csharp
private void RenderChunked(ReadOnlySpan<char> chars)      // chars.Length > 5461
{
    _encoder ??= Encoding.UTF8.GetEncoder();               // per-renderer, per-render (D15)
    while (true)
    {
        Span<byte> span = _writer.GetSpan(MaxUtf8SizeHint); // contract: at least the hint
        _encoder.Convert(chars, span, flush: true,
            out int charsUsed, out int bytesUsed, out bool completed);
        _writer.Advance(bytesUsed);                         // commit before the next GetSpan
        if (completed)
            break;
        chars = chars.Slice(charsUsed);                     // remaining tail; loop
    }
}
```

Why this exact shape is correct, from the verified `Encoder.Convert` contract:

- Each iteration passes the **entire remaining tail** with `flush: true`. `flush`
  semantics apply to end-of-input: while output space runs out first, `Convert` fills as
  much as fits, reports `charsUsed`/`bytesUsed`/`completed == false`, and holds any
  half-consumed surrogate in encoder state; only the iteration that consumes the true
  end of input acts on `flush` — so "flush on the final chunk" falls out without
  tracking which chunk is final.
- A surrogate pair straddling an output-budget boundary is safe by construction: the
  encoder consumes the high surrogate into its internal state (`charsUsed` counts it,
  `bytesUsed` does not include its bytes) and emits the full 4-byte sequence at the
  start of the next iteration. The docs pin this exactly: `completed` "can also be set
  to `false`, even though the `charsUsed` and `charCount` parameters are equal … if
  there is still data in the `Encoder` object".
- The documented `ArgumentException` ("output buffer too small to contain any of the
  converted input") cannot fire: `GetSpan(MaxUtf8SizeHint)` returns ≥ 16 384 bytes by
  the `IBufferWriter` contract, and the largest single conversion unit is 4 bytes.
- **Ill-formed input parity:** a *lone* surrogate in a dynamic value (phase 7's HED7005
  only guards static pieces; .NET strings may carry lone surrogates at run time) is
  replaced by `Encoding.UTF8`'s default replacement fallback — U+FFFD, bytes
  `EF BF BD` — identically in the single-call tier, the chunked tier's `flush: true`
  final call, and in the parity oracle's own `Encoding.UTF8.GetBytes(stringResult)`
  normalization. The three-sink property test therefore holds even for ill-formed
  values; no special-casing is needed or added.

Worked size examples (the numbers WI2's adapter tests pin):

| Input | Tier | `GetSpan` hint | Bytes written | Iterations |
| --- | --- | --- | --- | --- |
| `"</h1>\n  <p>Made by "` (19 ASCII chars, a typical piece) | 1 | 57 | 19 | 1 |
| 1 200-char Cyrillic value (2-byte scalars) | 1 | 3 600 | 2 400 | 1 |
| 4 000 units = 3 000 ASCII + 500 emoji pairs (4 B/pair) | 1 | 12 000 | 5 000 | 1 |
| 5 461 ASCII chars (tier boundary) | 1 | 16 383 | 5 461 | 1 |
| 100 000 ASCII chars | 2 | 16 384/iter | 100 000 | 7 (⌈100 000⁄16 384⌉) |
| 100 000 CJK chars (3-byte scalars) | 2 | 16 384/iter | 300 000 | 19 (⌈300 000⁄16 384⌉) |

The over-request in tier 1 (3·L for mostly-ASCII text) costs nothing durable: the unused
tail of the span is simply not `Advance`d, and the writer hands it out again on the next
`GetSpan` — pooled writers do not leak or fragment from conservative hints of this size.

`RenderUtf8(ReadOnlySpan<byte>)` is a straight copy via `BuffersExtensions.Write`
(verified present in the System.Memory package's netstandard2.0 reference assembly, so
it reaches all TFMs), which loops `GetSpan`/`CopyTo`/`Advance` for segments smaller than
the input. `Render(string)` forwards to the span path (`data.AsSpan()`);
`string.IsNullOrEmpty`/empty-span writes are skipped, mirroring `ScopeRenderer`. The
implementation must honor the `IBufferWriter` contract as verified: `GetSpan(sizeHint)`
returns *at least* the hint but never assume exact sizes; never write to a span after
`Advance`; request a fresh span every iteration.

**Rationale.** Sizing by the 3·L worst case skips the `GetByteCount` pre-pass (a full
extra scan) for the overwhelmingly common small-piece case; 16 KB keeps `GetSpan`
requests comfortably inside default pool segment sizes while making the chunked tier
rare (a 16 KB single write is already an outlier for template pieces). The stateful
encoder is the only correct chunking tool — converting slices independently corrupts any
surrogate pair straddling a boundary.
**Alternatives rejected.** Exact `GetByteCount` sizing (extra scan per write);
`Encoding.TryGetBytes`/`Utf8.TryWrite` (net8-only; would fork the algorithm per TFM for
no output difference — the pointer/span `GetBytes` reach every target); renting
intermediate `byte[]` from `ArrayPool` and copying into the writer (an extra copy — the
writer's own spans are the destination); `System.Text.Rune` walking (absent on
.NET Framework; the `Encoder` already owns the state problem).
**Grounding.** [`Encoder.Convert`](https://learn.microsoft.com/en-us/dotnet/api/system.text.encoder.convert)
("saves state between calls", including "the high surrogate of a surrogate pair";
"designed to be used in a loop"; the `completed`-false-at-equal-counts remark; the
too-small-buffer `ArgumentException`; overload availability re-verified July 2026 —
span form netcoreapp2.1+/netstandard2.1, pointer form netstandard2.0/.NET Framework 2.0+);
[`BuffersExtensions.Write`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.buffersextensions.write)
(the straight-copy loop, in System.Memory.dll; presence in the netstandard2.0 package
confirmed against the [System.Memory reference source](https://github.com/dotnet/corefx/blob/release/2.1/src/System.Memory/ref/System.Memory.cs));
[System.Buffers overview](https://learn.microsoft.com/en-us/dotnet/standard/io/buffers)
(GetSpan/Advance rules quoted in *IBufferWriter common problems*);
[`IBufferWriter<T>.GetSpan`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1.getspan)
(never returns empty for `sizeHint: 0`, may throw when the request cannot be satisfied);
[Character encoding in .NET](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction)
(lone surrogate halves are meaningless; `Rune` unavailable on .NET Framework).

### D6 — No UTF-8 piece cache on the runtime document: cross-cutting D4 supersedes the roadmap paragraph

**Decision.** The runtime backend (`RuntimeDocument` and its strategies) keeps
`string`/`char` internals exactly as
[cross-cutting D4](../common/cross-cutting-decisions.md#d4--utf-8-static-piece-emission-u8-belongs-to-phase-7)
pins: **no** lazily cached UTF-8 `byte[]` copies of document pieces are added. On the
byte sink, runtime-compiled templates transcode every static piece per render through
D5's single-call tier. **[correction]** This supersedes the roadmap's "UTF-8 path
specifics" paragraph ("`RuntimeDocument`'s document pieces get lazily cached UTF-8
`byte[]` copies … the cache is unbounded"): both texts are dated July 2026, but D4 is
the later, cross-phase ratification recorded for the whole spec set, it *names and
rejects* exactly this cache ("adds dual-encoding complexity to every render path for a
win the precompiled path gets for free"), and this spec effort was directed to hold D4.
The roadmap's associated sub-decision (unbounded cache, no configuration surface) is
thereby moot; its spirit — pre-encoded bytes proportional to template size, living with
the compiled artifact — survives as phase 7's `"…"u8` pieces, which cost nothing at run
time at all.

Why per-render transcoding is acceptable, quantified and gated: UTF-8 transcoding of the
ASCII-heavy static text that dominates templates runs through SIMD fast paths at
multi-GB/s throughput, so on the standing home-page workload the transcode cost is a
small fraction of render time — and the byte sink still deletes the full-output string,
the host's own re-encode, and the copy between them. The claim is *proven, not
asserted*, by `SinkRenderBenchmarks` (WI9): `RenderUtf8Buffer` must beat `RenderString`
on allocated bytes on every runtime, and its mean must land within the same order as
`RenderTextWriter`. **Revisit trigger** (named, per the no-speculation rule): if that
benchmark ever attributes ≥ 25 % of `RenderUtf8Buffer` mean time to static-piece
transcoding (measured by a piece-only fixture), a narrow, per-`RuntimeDocument` lazy
cache may be re-proposed — as a maintainer-ratified change with that benchmark attached.

**Rationale.** As recorded in cross-cutting D4 itself; additionally, a runtime cache
would have to answer invalidation-on-recompile, memory attribution, and
`FileSystemWatcher` re-compile interactions (`HeddleTemplate` hot-swaps
`_processStrategy`) — complexity with no path to beating "the compiler already embedded
the bytes" (phase 7).
**Alternatives rejected.** The roadmap's lazily cached per-document UTF-8 `byte[]`
copies, unbounded (the alternative D4 names and rejects: "adds dual-encoding complexity
to every render path for a win the precompiled path gets for free" — and it doubles the
retained memory of every compiled document whether or not the byte sink is ever used);
a bounded/evicting variant of the same cache (inherits every invalidation question and
adds an eviction policy nobody asked for — no concrete memory report exists, the
roadmap's own revisit condition); transcoding pieces once per *render* into a pooled
scratch buffer that outlives the piece write (an extra copy per piece versus writing
straight into the sink's own span, D5 — strictly worse than the chosen per-piece
single-call transcode).

### D7 — Phase 7 interop: `WritePiece` + sink `Generate*` support APIs; emitter update to schemaVersion 2

**Decision.** The byte-sink fast path binds to phase 7's contract by name:

- **Engine side** (`Heddle.Precompiled.PrecompiledRuntime`, additions):

  ```csharp
  /// <summary>Renders a precompiled root strategy to a TextWriter sink. Generated-code entry point.</summary>
  public static void GenerateToWriter(IProcessStrategy root, object model, object chained,
      object callerData, TextWriter writer);

  /// <summary>Renders a precompiled root strategy to a UTF-8 buffer writer. Generated-code entry point.</summary>
  public static void GenerateUtf8(IProcessStrategy root, object model, object chained,
      object callerData, IBufferWriter<byte> writer);

  /// <summary>Single piece-write hook for generated bodies: writes the pre-encoded u8 twin
  /// when the scope's renderer is a UTF-8 sink, the string form otherwise (including under
  /// encode proxies, which are deliberately not IUtf8ScopeRenderer).</summary>
  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static void WritePiece(in Scope scope, string piece, ReadOnlySpan<byte> utf8Piece);
  ```

  `GenerateToWriter`/`GenerateUtf8` construct the matching renderer, build the root
  `Scope` exactly as `GenerateString` does, and call `root.Render(scope)`. They take
  **no locals provisioning of their own** — per phase 7's audited contract,
  `GenerateString` has no locals parameter and the phase 3 frame rides the strategy: a
  participant-hosting document root arrives already wrapped
  (`internal static readonly IProcessStrategy Root = PrecompiledRuntime.WithLocalsFrame(new Body0());`),
  so the same wrapped `Root` serves all three entry points identically. `WritePiece` is
  the *only* place the u8/string choice is made — one hook, engine-side, mirroring
  phase 7's one-`EmitPiece` principle.
- **Emitter side** (`Heddle.Generator`, phase 8 update):

  1. Typed entry points gain the two sink overloads, emitted for **every** template
     (opt-in-independent, exactly mirroring the string entry's shape — model strongly
     typed, `object` for `:: dynamic` templates per phase 7 D11, `chained`/`callerData`
     defaulted). For phase 7's worked example 1 (`Views_Product`, model
     `Shop.Product`), the complete emitted form is:

     ```csharp
     /// <summary>Typed entry point — the recommended host API (spec D11).</summary>
     public static string Generate(Shop.Product model, object chained = null, object callerData = null)
         => PrecompiledRuntime.GenerateString(Root, model, chained, callerData);

     /// <summary>Renders into a TextWriter with no full-output materialization (phase 8).</summary>
     public static void Generate(Shop.Product model, TextWriter writer, object chained = null, object callerData = null)
         => PrecompiledRuntime.GenerateToWriter(Root, model, chained, callerData, writer);

     /// <summary>Renders UTF-8 into an IBufferWriter&lt;byte&gt; with no full-output materialization (phase 8).</summary>
     public static void Generate(Shop.Product model, IBufferWriter<byte> writer, object chained = null, object callerData = null)
         => PrecompiledRuntime.GenerateUtf8(Root, model, chained, callerData, writer);
     ```

     The D3 `Generate(model, null)` ambiguity corner applies to generated entry points
     identically (same overload family, same CS0121, same migration note).
  2. When `HeddleEmitUtf8Pieces=true` and the u8 twin exists (phase 7 D15 validation
     passed), generated `Render` bodies emit
     `PrecompiledRuntime.WritePiece(in scope, PN, PNU8);` where they previously emitted
     `scope.Renderer.Render(PN);` — a one-statement rewrite at phase 7's single
     piece-write call site, still under `#line hidden`. Before/after, on example 1's
     first piece:

     ```csharp
     // schemaVersion 1 (phase 7 emitter, or phase 8 emitter without the opt-in):
     scope.Renderer.Render(P0);
     // schemaVersion 2 with HeddleEmitUtf8Pieces=true (u8 twin present):
     PrecompiledRuntime.WritePiece(in scope, P0, P0U8);
     ```

     `Execute` bodies are untouched (the value path stays strings, D12); templates whose
     u8 twin was suppressed (HED7005) keep the plain `Render(PN)` form piece-by-piece —
     the twin gate is per-template, exactly phase 7's D15 downgrade unit.
  3. The manifest `schemaVersion` becomes **2** for all phase-8-generator output (its
     code references phase-8 engine members), and the phase-8 engine's registration gate
     accepts versions 1 and 2 — schema-1 assemblies from the phase 7 generator keep
     registering and render on all three sinks (their pieces simply transcode via D5, as
     does every template built without the opt-in). `PrecompiledCapabilities.Utf8Pieces`
     keeps its phase 7 meaning and now also tells hosts which templates hit the
     zero-transcode tier.

The normative shape replaces generated-code.md's non-normative sketch (`sink.Write(P3U8)`
directly): generated code writes pieces **through the renderer hook**, never the raw
sink, so proxy renderers (`HtmlEncodedRenderer`) and future sinks compose correctly and
ordering is preserved through one funnel.

**Rationale.** This is exactly the D4-shaped consumption: `PNU8` spans over the
assembly's static data flow to the `IBufferWriter<byte>` as a straight copy with zero
allocation and zero encoding. One `WritePiece` hook keeps the u8 decision out of every
generated body and lets the engine evolve sink capabilities without regenerating
consumer assemblies beyond a schema bump.
**Alternatives rejected.** Direct `sink.Write(PNU8)` in generated bodies (bypasses
proxies; breaks under `RenderProxy`; couples generated code to a concrete sink);
a parallel `IUtf8ProcessStrategy` interface with byte-render methods (forks the render
protocol phase 7 just unified — the capability test inside one hook costs a guarded
type check per piece and nothing else); keeping the generator untouched in phase 8
(leaves D4's entire payoff unrealized — the roadmap's "phase 7 amplifies it" is this
work item).
**Grounding.** [generated-code.md — the piece table and the u8 tier](../phase-7-build-time-compilation/generated-code.md#the-piece-table-and-the-u8-tier-d15-shape);
[generated-code.md — the generated-support API](../phase-7-build-time-compilation/generated-code.md#the-generated-support-api);
phase 7 [D6/D15](../phase-7-build-time-compilation/README.md#design-decisions) (manifest
schema gate, u8 validation).

### D8 — Async surface: none in v1 — synchronous sinks plus host-driven flushing; async model resolution stays out

**Decision.** No async render APIs ship in this phase: no `GenerateAsync`, no
`CancellationToken` parameters, no async members on any renderer interface.
`RenderData`/`ProcessData` and the whole extension contract stay synchronous. The
sanctioned backpressure pattern is the ecosystem's own: render synchronously into an
`IBufferWriter<byte>` (a `PipeWriter` qualifies — `PipeWriter : IBufferWriter<byte>`),
then the **host** awaits `FlushAsync` — including periodically for very large pages,
using `PipeWriter.UnflushedBytes` (net6-era API, available on every supported runtime)
to decide when. Async model resolution is likewise rejected for v1, adopting the
roadmap's decision record verbatim: Heddle is data-in/text-out; awaitable member access
would infect every extension signature for a need the host solves by resolving data
before rendering. Revisit triggers, named: a concrete integration that cannot flush
between renders (e.g. server-driven streaming of *one* template larger than any
tolerable buffer window), or demonstrated demand for deferred-region SSR. If an async
surface is ever added, the pre-made shape is a flush-only seam
(`ValueTask FlushAsync(CancellationToken)` on a sink wrapper), not async rendering —
recorded so the future addition doesn't reopen the extension contract.

**Verification of the leans, with ecosystem grounding.**

- *`PipeWriter` pattern*: `HttpResponse.BodyWriter` **is** a `PipeWriter` that "buffers
  data until a flush operation is triggered", and the official guidance for writing to
  it directly is to "call `PipeWriter.FlushAsync` manually" — synchronous production +
  async flush is the documented ASP.NET Core shape, not a Heddle invention.
- *`TextWriter.WriteAsync` realities*: the base implementation is `virtual`, allocates a
  `Task` per call, and `TextWriter` permits only one in-flight operation
  (`InvalidOperationException`: "The text writer is currently in use by a previous write
  operation") — an async-per-`Render`-call engine would issue thousands of awaited
  micro-writes per page against writers that mostly complete synchronously into a
  buffer. ASP.NET Core's own `HttpResponseStreamWriter` exists to absorb sync writes
  into a 16 KB buffer precisely so frameworks don't do this.
- *Razor precedent*: Razor pages write synchronously into a paged `ViewBuffer`
  (`ViewBufferPage` list) and the framework flushes asynchronously at defined points
  (`ViewBuffer.WriteToAsync`, explicit `FlushAsync()`) — the render loop itself is not
  await-per-write; sync-over-buffered is the mainstream design phase 8 mirrors.
- *`ValueTask` guidance*: irrelevant while no async surface exists; recorded for the
  future seam — Microsoft's guidance is to default to `Task` and reach for `ValueTask`
  only for high-frequency, usually-synchronous completions awaited exactly once, which a
  flush-only API would satisfy.

**Alternatives rejected.** `GenerateAsync(model, PipeWriter, CancellationToken)` with
engine-side periodic flushes (moves a host policy — flush cadence — into the engine;
requires the render tree to become async or to block on `FlushAsync`, which is
sync-over-async); async-everything renderer interfaces (per-call `Task`/state-machine
overhead on the hottest loop, contradicting the per-call realities above; would also
force `IExtension` churn the roadmap explicitly guards against); shipping
`CancellationToken` on synchronous methods (a token that is only ever checked between
synchronous writes adds API weight without responsiveness — hosts cancel by abandoning
the render or via the sink's own failure).
**Grounding.** [Request and response operations in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/request-response);
[`HttpResponse.BodyWriter`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpresponse.bodywriter);
[dotnet/aspnetcore discussion #62464](https://github.com/dotnet/aspnetcore/discussions/62464)
(when manual `BodyWriter` flushes are required);
[`TextWriter.WriteAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.io.textwriter.writeasync)
(virtual base, per-call `Task`, single in-flight operation);
[dotnet/aspnetcore #49172](https://github.com/dotnet/aspnetcore/issues/49172) (sync
writes against response writers work only up to the buffered window — the host, not the
engine, must own the flush);
[ViewBuffer.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.ViewFeatures/src/Buffers/ViewBuffer.cs)
and [dotnet/aspnetcore #65605](https://github.com/dotnet/aspnetcore/issues/65605)
(Razor buffers all view output; writes happen at `WriteToAsync`/explicit flush);
[Understanding the Whys, Whats, and Whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/);
[`PipeWriter.UnflushedBytes`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter.unflushedbytes).

### D9 — Encoding parity across sinks: the phase 2 seams unchanged; encode-then-transcode pinned; the 2.0 slot-in named

**Decision.** Encoding behavior is identical across sinks because the *encoding sites do
not move*: the two phase 2 D11 seams (`AbstractHtmlExtension.ProcessData`,
`HtmlEncodedRenderer.Render`) remain the only encoders, and both operate at the
`char`/`string` level *before* any byte conversion. The pinned pipeline order on the
UTF-8 sink is therefore **encode (chars) → transcode (UTF-8)** — never the reverse, and
never double: `HtmlEncodedRenderer` is not `IUtf8ScopeRenderer` (D2), so pre-encoded u8
pieces and raw byte writes cannot skip past an active encode proxy (`WritePiece` falls
back to the string branch, which the proxy encodes, then the inner byte sink
transcodes). `HtmlEncodedRenderer` gains the `ISpanScopeRenderer` member with a 1.x
bridge implementation — `Render(ReadOnlySpan<char> data)` materializes the string and
calls the existing seam (`WebUtility.HtmlEncode` has only `(string)` and
`(string, TextWriter)` overloads — no span input exists, verified) — so span-writing
callers work under encode proxies today at a documented cost, and the 2.0 encoder swap
upgrades exactly one method body: under the phase 2 D11 pluggable `TextEncoder`, the
bridge becomes the span `Encode(ReadOnlySpan<char>, Span<char>, out _, out _)` loop into
the inner renderer, and a UTF-8 inner sink may additionally use
`EncodeUtf8(ReadOnlySpan<byte>, Span<byte>, out _, out _)` — the `OperationStatus`-based
chunk loop the API is designed for. Neither is built now (phase 2 D11: no new encoder
call sites in 1.x; no `SearchValues`-based hand-rolled scan either — the .NET 10 runtime
already rebuilt the inbox encoder scan on `SearchValues`, so the 2.0 swap inherits that
for free, and D16 keeps this phase free of `#if` forks).

The parity contract is a named golden matrix, `SinkEncodingParityTests` (WI4/WI8): for
every corpus fixture × {`Text`, `Html`} profile × {runtime-compiled, precompiled}
backend, the outputs of {string, `TextWriter`, `IBufferWriter<byte>`} sinks are
byte-identical after normalizing the byte sink via `Encoding.UTF8.GetBytes(stringResult)`
— the property oracle from the roadmap's TDD verdict, which is strictly stronger than
sampled goldens. All twelve cells, spelled out (the string sink is each row's oracle;
"= string" means char-identical for the `TextWriter` cell and UTF-8-normalized
byte-identical for the `IBufferWriter<byte>` cell):

| # | Profile | Backend | Sink | Expected result | Proving test lane |
| --- | --- | --- | --- | --- | --- |
| 1 | `Text` | runtime | string | The pre-phase golden, byte-identical (nothing on this path changed — D1) | existing golden suite (regression gate) |
| 2 | `Text` | runtime | `TextWriter` | = string (raw carriers write the same strings through `writer.Write`) | `SinkParityTests` |
| 3 | `Text` | runtime | `IBufferWriter<byte>` | = string after UTF-8 normalization (every piece/value transcodes via D5) | `SinkParityTests` |
| 4 | `Text` | precompiled | string | = cell 1 (phase 7 differential harness invariant, unchanged) | phase 7 differential harness |
| 5 | `Text` | precompiled | `TextWriter` | = string; pieces write as strings through `WritePiece`'s string branch when no u8 twin, and `TextWriterScopeRenderer` is never `IUtf8ScopeRenderer` | differential byte lane (WI7) |
| 6 | `Text` | precompiled | `IBufferWriter<byte>` | = string normalized; with `HeddleEmitUtf8Pieces=true` the identical bytes arrive via the zero-transcode `RenderUtf8` branch — same output, different path, asserted both ways | differential byte lane + `PrecompiledUtf8FastPathTests` |
| 7 | `Html` | runtime | string | The pre-phase golden incl. the phase 2 XSS corpus — `HtmlEncodedRenderer` proxies exactly as today | existing golden suite + phase 2 corpus |
| 8 | `Html` | runtime | `TextWriter` | = string: encode (chars, unchanged seams) happens before the writer sees anything; the proxy's output strings hit `writer.Write` | `SinkEncodingParityTests` |
| 9 | `Html` | runtime | `IBufferWriter<byte>` | = string normalized: encode (chars) → transcode (D5); the proxy is not `IUtf8ScopeRenderer`, so no byte write can bypass it | `SinkEncodingParityTests` |
| 10 | `Html` | precompiled | string | = cell 7 (phase 7 reproduces the phase 2 redirect at generation time — `EmptyHtmlExtension`, `RenderType.Encode`) | phase 7 differential harness |
| 11 | `Html` | precompiled | `TextWriter` | = string: `WritePiece` under an encode proxy takes the string branch (the proxy is not `IUtf8ScopeRenderer`), so pieces are encoded like any value | `SinkEncodingParityTests` |
| 12 | `Html` | precompiled | `IBufferWriter<byte>` | = string normalized — **the cell the D2/D9 design exists for**: even with u8 twins emitted, encode-proxied pieces route string → encode → transcode; pre-encoded bytes never skip the proxy | `SinkEncodingParityTests` + `PrecompiledUtf8FastPathTests` (asserts the fast path is *not* taken under the proxy) |

The byte-level content of the `Html` cells is already pinned character-by-character by
phase 2's [encoding-pin rows E01–E14](../phase-2-safe-output/README.md#testing-plan)
(`WebUtility.HtmlEncode` baseline, with the 2.0 deltas pre-measured there); this matrix
does not restate those bytes — it proves the *sinks* cannot change them.

**Rationale.** One encoding site per phase 2's design means parity is structural, not
tested-into-existence; the matrix test then guards the *structure* against regression.
Keeping the WebUtility bridge honest (string-per-encoded-span in 1.x) is the documented
cost the roadmap already concedes for hosts pinning the legacy encoder after 2.0.
**Alternatives rejected.** Implementing a span scan-and-copy against `WebUtility`'s
character table now (a third encoding implementation to keep bit-compatible — exactly
what phase 2 D11 forbids); making `HtmlEncodedRenderer` a `IUtf8ScopeRenderer`
(pre-encoded bytes would bypass encoding — an XSS hole by construction).
**Grounding.** [`WebUtility.HtmlEncode`](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode);
[`TextEncoder.Encode` span overload](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.textencoder.encode);
[`TextEncoder.EncodeUtf8`](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.textencoder.encodeutf8)
(`OperationStatus` loop contract, re-verified July 2026);
[dotnet/runtime PR #114494](https://github.com/dotnet/runtime/pull/114494) (encoder scan
rebuilt on `SearchValues` in .NET 10);
[phase 2 D10/D11](../phase-2-safe-output/README.md#d10--encoding-semantics-per-value-category-phase-1-interaction).

### D10 — Formatter built-ins span-format per TFM; the write helper is one static funnel

**Decision.** Adopting the roadmap's ratified decision with its per-TFM matrix: the
formatter built-ins migrate to span formatting in this phase. Normatively `int`, `date`,
`money` (the ratified list) — and `time` and `guid` by the same rule, since they share
the same `ToString`-into-`Render` call shape (the per-type quirks tabled below
notwithstanding) and leaving two of five formatters on the old path would be a
pointless inconsistency (recorded as the only scope extension, zero new mechanism). The
mechanics:

- A public span bridge and a value-format funnel in `Heddle.Data`:

  ```csharp
  public static class ScopeRendererExtensions
  {
      /// <summary>Span write against any renderer: dispatches to ISpanScopeRenderer when
      /// implemented, otherwise materializes the string (documented downlevel cost).</summary>
      public static void Render(this IScopeRenderer renderer, ReadOnlySpan<char> data);

  #if NET6_0_OR_GREATER   // net6.0 / net8.0 / net10.0 builds only
      /// <summary>Formats value directly into the renderer: IUtf8SpanFormattable into a UTF-8
      /// sink (net8+ builds), else ISpanFormattable into a stackalloc char span, else ToString.
      /// Output characters are identical on every tier — only allocations differ.</summary>
      public static void Render<T>(this IScopeRenderer renderer, T value, string format,
          IFormatProvider formatProvider) where T : struct, ISpanFormattable;
  #endif
  }
  ```

  `Render<T>`'s body is a three-tier ladder in exactly one method:

  1. **UTF-8 tier** (`#if NET8_0_OR_GREATER` *inside* the method — `IUtf8SpanFormattable`
     does not exist on the net6.0 build): when
     `renderer is IUtf8ScopeRenderer u8 && value is IUtf8SpanFormattable utf8Formattable`,
     format via `IUtf8SpanFormattable.TryFormat` straight into the sink's `GetSpan` —
     no string, no char round-trip (the boxing the `is` test implies for a struct `T` is
     eliminated by the net8+ JIT's specialized generic instantiation; the benchmark gate
     verifies the fast path allocates zero). A `false` return (destination too small —
     `GetSpan(256)` makes this pathological-format-only) falls through to tier 2.
  2. **Char-span tier** (all `NET6_0_OR_GREATER` builds): `value.TryFormat(stackalloc
     char[256], out int written, format, formatProvider)`, then write via the span
     bridge. On `false` — formats longer than 256 chars, e.g. pathological custom
     patterns — fall through to tier 3.
  3. **String tier**: `renderer.Render(value.ToString(format, formatProvider))` — the
     exact pre-phase call, kept as the semantic definition of the other two tiers.

- **Per-extension migration** — each `RenderDataInternal`/`RenderData` replaces its
  `scope.Renderer.Render(x.ToString(…))` composition with the funnel call inside
  `#if NET6_0_OR_GREATER`; the `netstandard2.0`/`net48` builds keep today's string-based
  formatting **verbatim** (the interfaces do not exist there — documented, not worked
  around). `ProcessData`/`ProcessDataInternal` implementations are untouched on every
  TFM — they must return strings by contract (D12). No control flow changes anywhere:
  every type test, coercion, catch block, and default stays. The exact per-type calls
  and the quirks they preserve (verified against source — Assumed state):

  | Extension | Funnel call(s) (net6+ builds) | Per-type quirks preserved |
  | --- | --- | --- |
  | `int` | `Render(data, format, CultureInfo.InvariantCulture)` for the `int` branch; `Render(longData, format, CultureInfo.InvariantCulture)` for the `long` and coerced branches; empty format passes through (empty `TryFormat` format ≡ `"G"` ≡ the current `ToString(InvariantCulture)` branch) | The `Convert.ChangeType(…, typeof(long), …)` coercion and its three catch-and-render-nothing handlers run **before** the funnel call, unchanged. |
  | `date` | `Render(date, dateFormat, CultureInfo.InvariantCulture)` — `dateFormat` already defaulted to `"d"` upstream | `DateTime`-only guard unchanged; no coercion path exists. |
  | `money` | `Render(decimalValue, "c", (IFormatProvider)localeInfo)` — `localeInfo` may be null | Null provider ≡ thread-current culture in both `ToString("c")` and `decimal.TryFormat` (both resolve `NumberFormatInfo.CurrentInfo`) — the current-culture no-locale branch survives byte-exact. The `CultureCache` lookup and the narrower catch set (no `OverflowException`) are untouched. |
  | `time` | `Render(date, dateFormat, CultureInfo.InvariantCulture)` — `dateFormat` defaulted to `"t"` upstream | Still formats `DateTime`, never `TimeSpan` — the funnel does not change the carrier type. |
  | `guid` | `Render(guid, format, null)` — `Guid` ignores the provider in `ToString` and `TryFormat` alike (culture-insensitive by contract) | **Not** an `[EncodeOutput]` carrier — the only migrated extension that never runs under an encode proxy, so it takes the full fast path even on the `Html` profile. Empty format ≡ `"D"`; an invalid specifier throws `FormatException` on every tier (both `ToString` and `TryFormat` throw for bad `Guid` formats) — behavior preserved, not smoothed. |
  | `string` | **not migrated** | There is no format step — `StringExtension` renders strings it already has. Its span win comes free through D9/D2 (`Render(string)` is unchanged), and boxing-avoidance for the generic funnel does not apply (`string` is a reference type; the funnel's `where T : struct` correctly excludes it). Recorded so "the formatter built-ins" is a closed list of five, not an open analogy. |

- Same formatting output on every TFM, pinned by running the formatter goldens per
  target (`FormatterSpanGoldenTests`, WI8): `ISpanFormattable.TryFormat` and
  `IUtf8SpanFormattable.TryFormat` are contractually equivalent to `ToString(format,
  provider)` for the BCL primitives involved — `int`, `long`, `DateTime`, `decimal` and
  `Guid` all implement `ISpanFormattable` on net6+ and `IUtf8SpanFormattable` on net8+
  (per-type verification in the interface docs' implementor lists).
- Encode-carrier caveat, recorded: four of the five (`int`, `date`, `money`, `time`) are
  `[EncodeOutput]`, so under the `Html` profile with `RenderType.Encode` the active
  renderer is `HtmlEncodedRenderer` — the span path then bridges through one string (D9)
  because 1.x encoding needs a string anyway, and the UTF-8 tier is skipped
  automatically (the proxy is not `IUtf8ScopeRenderer`). The full allocation win applies
  to `RenderType.Raw` carriers — the `Text`-profile default today, plus `guid` on every
  profile — and returns automatically for encoded carriers with the 2.0 encoder swap.
  Formatter output *bytes* are identical regardless.

**Rationale.** One funnel means the TFM ladder (`IUtf8SpanFormattable` → span → string)
exists in exactly one method; the formatters stay one-line calls. `where T : struct`
matches every consumer, guarantees JIT specialization, and forbids null flowing into the
format path.
**Alternatives rejected.** Generic members on the renderer interfaces (generic virtual
dispatch; D2); per-TFM public constraint differences (`, IUtf8SpanFormattable` on net8+
signatures — confusing surface for zero capability gain over the internal type test);
keeping the helper `internal` (the roadmap ratified an additive public renderer API for
extension authors "where callers have them" — the public funnel is that API, and custom
extensions formatting numbers are the second consumer that justifies it).
**Grounding.** [`ISpanFormattable`](https://learn.microsoft.com/en-us/dotnet/api/system.ispanformattable)
(net6+); [`IUtf8SpanFormattable`](https://learn.microsoft.com/en-us/dotnet/api/system.iutf8spanformattable)
(net8+, implemented by the primitives above);
[`Utf8.TryWrite`](https://learn.microsoft.com/en-us/dotnet/api/system.text.unicode.utf8.trywrite)
(net8 UTF-8 formatting family, background); roadmap "Sink abstraction" ratified
paragraph.

### D11 — `@partial` streams through the caller's renderer (roadmap correction executed)

**Decision.** `PartialExtension.RenderData` stops materializing the inner template's
output. `HeddleTemplate` gains an **internal** streaming entry:

```csharp
internal void Render(object data, object chained, object callerData, IScopeRenderer renderer)
```

— the shared render core (WI3). Its body is exactly the current `Generate` prologue and
epilogue with the renderer parameterized, verified against
[HeddleTemplate.cs](../../../src/Heddle/HeddleTemplate.cs):

1. compile guards — `TemplateInitException` ("Compile first") when `CompileResult` is
   null, else `TemplateCompileException(CompileResult.ErrorList)` when
   `_processStrategy` is null;
2. the `#if DEBUG` model-type check (`TemplateProcessingException` on mismatch);
3. the `ObjectDisposedException` guard on `_disposeAfterComplete`;
4. `_runners++` / `try … finally { _runners--; dispose-after-complete }` bookkeeping;
5. root `Scope` construction over the **supplied** renderer — the exact existing
   construction *including phase 3's root-frame provisioning*
   (`_runtimeDocument.NeedsLocals ? new ScopeLocals() : null`, phase 3 WI3), so all
   three callers — string `Generate`, both sink overloads, and the streamed `@partial`
   core — inherit root frames identically; dropping the frame here would break every
   root-level `@if`/`@elif`/`@else` set on sink renders (`ElseExtension`'s frameless
   path throws) and void phase 3 E13's fresh-root-frame guarantee for partials;
6. `_processStrategy.Render(scope)`.

The string `Generate` becomes a wrapper that adds what only the string path needs around
that core: `new ScopeRenderer(_maxLength)` (net8+; `_maxElementCount` downlevel), the
high-water read/update (`newMax * 110 / 100`, `int.MaxValue / 2` cap on net8+),
`renderer.ToString()` and `renderer.Clear()`. The core has exactly **three callers**:
`string Generate`, the two D3 sink overloads (each constructing its adapter renderer),
and `PartialExtension.RenderData`. The extension change, before/after
([PartialExtension.cs](../../../src/Heddle/Extensions/PartialExtension.cs)):

```csharp
// before — the single render-path full materialization (Assumed state [correction]):
public override void RenderData(in Scope scope)
{
    scope.Renderer.Render(InnerTemplate?.Generate(scope.ModelData, scope.ChainedData));
}

// after — the partial streams through the caller's renderer:
public override void RenderData(in Scope scope)
{
    InnerTemplate?.Render(scope.ModelData, scope.ChainedData, null, scope.Renderer);
}
```

Observable difference: none in output bytes (the golden corpus pins it); in behavior,
the partial's pieces now reach the sink incrementally instead of after the whole
sub-render completes, and the inner template no longer allocates a `ScopeRenderer` +
full sub-output string per call — on **all** paths including the existing string path
(the outer `ScopeRenderer` receives the partial's pieces directly; fewer intermediate
allocations there too). The `null` third argument is `callerData`: the current code path
also passes no caller data into `InnerTemplate.Generate` (two-argument call), so the
streamed call is argument-for-argument identical.
`PartialExtension.ProcessData` is untouched — a parent consuming the partial's value
still gets a string, by contract (D12). The inner template's own `_maxLength` no longer
learns from streamed renders (it has no buffer in play), which only affects the
seeding of *direct* `InnerTemplate.Generate(...)` string calls — the outer template's
high-water still observes the full composed output. Byte-identity is enforced by the
existing golden corpus (`partial.heddle` is in it) and the three-sink parity run.

**Rationale.** Verification (Assumed state) showed `@partial` is the single remaining
full-materialization on the render path; without this fix the no-materialization
guarantee (D13) would hold only for partial-free templates, which the benchmark home
page is not. The internal entry reuses the exact bookkeeping the public paths use — one
render prologue/epilogue, three callers.
**Alternatives rejected.** Leaving `@partial` materializing on sink paths (a silent
O(sub-output) allocation that falsifies success criterion 1 on realistic pages);
exposing the streaming entry publicly (no external consumer; `RenderProxy` +
`Generate` overloads cover host composition — deferred until demanded); fixing it only
when the renderer is a sink adapter (two behaviors for one method; the string path
benefits identically and the benchmark gate proves no regression).

### D12 — The `ProcessData`/`Execute` string bridge stays; the boundary is pinned

**Decision.** Value composition keeps materializing strings, unchanged and deliberately:
chain non-terminals (`TemplateChain.RenderData` calls `ProcessData` on every item but
the last), `GetInnerResult` consumers (formatter format-strings, `@partial` name
resolution, `list`'s `ProcessData`, every extension reading its body as a value — which
after phase 5 includes definition bodies consumed as values: `SlotContent` renders the
caller body through the same funnel members, streaming on the `Render` path and
materializing only when a parent consumes it via `Execute`), and
`IProcessStrategy.Execute` all produce strings via the existing
`string[]`/`ExStringBuilder.Concat`/`string.Concat` machinery, which — verified — never
touches `scope.Renderer`. The normative boundary statement: **a sink receives bytes/chars
only from `Render`/`RenderData` calls; `Execute`/`ProcessData` results are values and may
be materialized at any size**. Consequently the no-materialization guarantee (D13)
defines its bound over *values*: a template that funnels its whole body through a chain
(`@a(X):b()` where the body is the page) legitimately materializes that body as the
chain's intermediate value — that is value semantics, not a streaming defect, and it is
excluded from the guarantee by the definition's V term. No buffer-strategy change is
needed or made on this path; `Execute`'s allocation profile is byte-for-byte the
pre-phase one on every sink, which is what makes cross-sink behavior of chains trivially
identical.

**Rationale.** Chains and inner-value reads *need* values — that is their contract
(roadmap: "value composition needs values; out of scope"). Because the two paths never
shared state, streaming sinks coexist with the bridge with zero interaction to specify
beyond this boundary sentence.
**Alternatives rejected.** A "streaming Execute" returning rope-like structures
(re-architecture of every `ProcessData` implementation and the extension contract for a
path whose outputs are typically format strings and chain links); per-sink `Execute`
variants (forks semantics phase 7 just froze into generated code).

### D13 — The no-materialization guarantee, operationally defined and testable

**Decision.** The guarantee, stated operationally: *for a compiled template rendered to
a `TextWriter` or `IBufferWriter<byte>` sink, per-render managed allocation is bounded by
O(V + W), where V is the largest single value materialized by the render (dynamic value
strings, chain intermediates, `GetInnerResult` reads — D12's term) and W is a constant
working set (the renderer adapter, at most one lazily created `Encoder`, boxing already
present on the delegate boundary); it is never O(N) in total output size N. In
particular no full-output `string`, `StringBuilder`, or `byte[]` is created, and the
engine itself performs no LOH-sized (≥ 85 000 B) allocation when no single value is
LOH-sized.* The string path is explicitly exempt — it materializes by definition.

Proven by three named assets (WI8/WI9):

1. `SinkAllocationTests.LargeOutputByteSinkAllocatesBounded` — the `streaming-large`
   fixture (> 1 MB output, largest single piece < 4 KB, no chains) rendered to a
   test-owned pooled buffer writer must allocate **< 64 KB** per render
   (`GC.GetAllocatedBytesForCurrentThread()` delta after warm-up; net6+/net8+/net10
   test TFMs — the API is unavailable on net48, where the TFM-gated test is skipped).
2. `SinkAllocationTests.AllocationIsOutputSizeInvariant` — the same fixture with its
   repeated section doubled must stay within 10 % of the single-size per-render
   allocation (sub-linear growth ⇒ no O(N) term).
3. `SinkRenderBenchmarks` (`[MemoryDiagnoser]`) — `RenderUtf8Buffer` and
   `RenderTextWriter` allocated B/op must sit below `RenderString` by at least the
   final output size of the home-page workload, per runtime, gated by the
   testing-standards benchmark rule (allocated bytes never increase).

**Rationale.** "No full-string materialization" is only enforceable as a measured
allocation bound; the two-fixture-sizes trick turns an asymptotic claim into an exact
assertion. The V term is honest about what the engine still allocates (values surface as
strings/boxes at the extension boundary by contract — phase 1 delegates return `object`).
**Alternatives rejected.** Asserting "zero allocation" (false — value strings and the
adapter exist); GC-event/ETW-based LOH assertions (brittle across runtimes; the
< 64 KB bound plus the size-invariance test subsumes the LOH claim for the engine's own
allocations).

### D14 — No `HED8xxx` diagnostics; host errors throw

**Decision.** This phase claims **no** diagnostic IDs. It adds no compile-path behavior:
no new template construct, no new compile error or warning, no generator diagnostic (the
D7 emitter update reuses phase 7's existing IDs — HED7005 already covers u8 downgrades,
HED7102 the schema gate). All new failure modes are host programming errors at public
API entry points and throw per the coding standards: `ArgumentNullException`
(`writer == null`), plus whatever the host's own sink throws through the render
(`IBufferWriter.GetSpan` may throw on unsatisfiable requests — propagated, never
swallowed). The `HED8xxx` block stays reserved and unused, exactly like phase 6's
record.

**Rationale.** Streaming is runtime API; inventing warnings for it would dilute the
diagnostic registry. Phase 6 set the precedent for recording an explicit empty claim.
**Alternatives rejected.** A runtime `HED8xxx` "diagnostic" for sink misuse such as a
null writer (the registry is for *template* problems surfaced through
`HeddleCompileError`/`HeddleCompileWarning`; host-programming errors throw by the coding
standards — routing them through the diagnostic channel would give template tooling IDs
that can never appear at compile time); a generator warning when
`HeddleEmitUtf8Pieces=true` produces no u8 twins (phase 7's HED7005 already reports the
only cause with a position; a phase 8 duplicate would fire twice for one fact).

### D15 — Thread safety: per-render adapters, single-owner sinks, concurrent renders preserved

**Decision.** The existing guarantee — one compiled `HeddleTemplate` serving concurrent
renders — extends to sinks as follows. Every `Generate` call constructs its own renderer
adapter; adapters hold no shared or static state (the lazy `Encoder` in
`Utf8ScopeRenderer` is per-adapter, hence per-render). Extension instances remain
stateless per the existing contract; all per-render state stays in the `Scope` lineage
(phase 3 rule, unchanged). The **sink itself is single-owner for the duration of the
call**: `IBufferWriter<byte>` implementations are not thread-safe and `TextWriter`
rejects overlapped operations — so the documented contract is that a host must not
touch the sink from another thread until `Generate` returns, and must not pass one sink
to two simultaneous renders. Different sinks on different threads against the same
template are fully supported and tested: `ConcurrentSinkRenderTests` (WI8) renders the
corpus flagship from N threads — mixing string, `TextWriter`, and byte sinks — and
asserts every output byte-identical to the single-threaded golden (the phase 3
opposite-conditions parallel test pattern). Pre-existing quirk, noted not fixed: the
`_runners++`/`--` bookkeeping uses non-atomic increments on a `volatile int` (dispose
racing only); the sink overloads share the same core (D11) and neither improve nor
worsen it — tightening it is out of scope and recorded under Deferred items.

**Rationale.** Per-render adapter construction is the same lifecycle the string path has
always used for `ScopeRenderer`; pushing sink synchronization onto the host matches
every .NET writer contract the sinks wrap.
**Alternatives rejected.** Engine-side locking around sink writes (would serialize the
hottest loop to protect against a misuse every wrapped writer type already documents as
unsupported — `TextWriter` itself throws on overlapped operations); pooling/reusing
renderer adapters across renders (a per-render adapter is two small allocations at most
— adapter + lazy `Encoder` — and pooling would reintroduce exactly the shared mutable
state the per-render rule exists to exclude); fixing the pre-existing non-atomic
`_runners` bookkeeping in this phase (touching the dispose protocol is unrelated risk in
a phase whose gate is "the string path is bit-identical"; recorded under Deferred items
with its trigger instead).

### D16 — .NET 10 stance for this phase: nothing conditional, one measurement consequence

**Decision.** Zero `#if NET10_0_OR_GREATER` blocks, confirming
[cross-cutting D7](../common/cross-cutting-decisions.md#d7--net-10-stance-no-conditional-compilation-by-default)
and the roadmap's own finding ("this phase needs no `#if NET10_0_OR_GREATER` blocks at
all"). The API gating in this spec is exactly net6 (`ISpanFormattable`, span
`TextWriter.Write`) and net8 (`IUtf8SpanFormattable`) as designed. C# 14 first-class
span conversions apply on all TFMs via `LangVersion` (call-site ergonomics only — a
`string` argument still exact-matches `Render(string)`, so no existing caller reroutes;
verified in the roadmap's overload-safety note). No `SearchValues` usage is introduced
(D9). The one operational consequence, carried into WI9: allocation/time baselines are
recorded **per runtime** — .NET 10's escape analysis (spans over non-escaping arrays,
delegate stack allocation) legitimately shifts net8-build-on-net10 numbers, so
comparisons are like-for-like per runtime, never cross-runtime.

**Rationale.** The roadmap's verified .NET 10 review found no render-path API in .NET 10
this phase could gate on (its UTF-8 what's-new items are hex-conversion and PEM
helpers), and cross-cutting D7 makes no-`#if` the default absent a benchmarked win.
**Alternatives rejected.** A net10-gated transcode path over hypothetical newer encoding
APIs (verified negative — no such API exists in the .NET 10 libraries what's-new);
gating the D10 UTF-8 formatting tier at net10 instead of net8 (`IUtf8SpanFormattable`
is net8-era; raising the gate would drop the fast path from the net8.0 build for
nothing); cross-runtime baseline comparison in the benchmark gate (would misattribute
.NET 10 JIT wins/regressions to phase 8 code — the per-runtime rule is the correction).

## Implementation plan

Work items in order; each runs the canonical loop (spec-first per the TDD verdict,
implement, gate, fix forward). "Gate" always means: full `dotnet test` (all TFMs) with
goldens byte-identical, grammar-stability check (no `generated/` diff — phase 8 is a
no-grammar phase), and benchmarks when the item touches a hot path.

1. **WI1 — Characterization oracle.** Capture the pre-phase baseline: one full golden
   run and a saved `SinkRenderBenchmarks`-precursor run of `TextRenderBenchmarks` per
   runtime (net8, net10). No product code. *Completion:* baselines checked into the
   PR description per the testing-standards baseline policy.
2. **WI2 — Capability interfaces and sink adapters.** New files
   `src/Heddle/Data/ISpanScopeRenderer.cs`, `src/Heddle/Data/IUtf8ScopeRenderer.cs`,
   `src/Heddle/Data/TextWriterScopeRenderer.cs`, `src/Heddle/Data/Utf8ScopeRenderer.cs`,
   `src/Heddle/Data/ScopeRendererExtensions.cs` (shapes per D2/D5/D10; match file-scoped
   vs block-scoped namespace style per surrounding-file rule — new files use block
   scoping per the standards). Edit `src/Heddle/Heddle.csproj`: add the explicit
   `System.Memory` package reference to the `netstandard2.0` group (pinning the
   currently-transitive dependency). Unit tests first: adapter-level tests over
   `ArrayBufferWriter<byte>`/`StringWriter` including the D5 tier split (the worked-size
   table in D5 is the `[Theory]` data), the tier-boundary rows (5 461/5 462 chars), and
   the `GetSpan`-contract compliance (a deliberately stingy test writer that returns
   minimum-size spans). Per-TFM caveat, verified: `ArrayBufferWriter<byte>` is in-box
   netcoreapp3.0+/netstandard2.1 only and **not** in the System.Memory package — on the
   net48 test target the same tests run over the test-owned writers (the stingy writer
   plus a plain growable test writer), same assertions, same fixtures. *Completion:*
   adapter tests green on all TFMs incl. net48.
3. **WI3 — Render core and public overloads.** Extract
   `HeddleTemplate.Render(object, object, object, IScopeRenderer)` (internal, D11) from
   `Generate`; re-express `string Generate` over it preserving exact semantics
   (high-water read/update, `ToString`/`Clear`, DEBUG type check, runner/dispose flow);
   add the two public `Generate` sink overloads (D3). Write `SinkParityTests` first —
   the property oracle: every corpus fixture rendered through all three sinks,
   `bytes == Encoding.UTF8.GetBytes(stringResult)` and `textWriterResult == stringResult`.
   *Completion:* parity green; `RenderString` benchmark shows zero regression vs WI1
   baseline (if the shared core costs anything measurable on the string path, inlining
   the prologue back into `Generate` is the sanctioned fallback — precedence rule 4
   beats DRY here).
4. **WI4 — `HtmlEncodedRenderer` span member + parity matrix.** Add the
   `ISpanScopeRenderer` implementation (D9 bridge). Write `SinkEncodingParityTests`
   (matrix per D9) first. *Completion:* matrix green, incl. `Html`-profile fixtures from
   the phase 2 XSS corpus.
5. **WI5 — Formatter migration.** Edit `IntegerExtension`, `DateExtension`,
   `MoneyExtension`, `TimeExtension`, `GuidExtension` per D10 (`RenderDataInternal`
   only). `FormatterSpanGoldenTests` run the existing formatter fixtures per TFM plus
   long-format edge rows (> 256-char formats exercising the fallback). *Completion:*
   per-TFM goldens byte-identical to pre-phase output.
6. **WI6 — `@partial` streaming.** Edit `PartialExtension.RenderData` per D11.
   *Completion:* `partial.heddle` goldens identical on all three sinks; home-page
   benchmark allocation drops or holds (never rises).
7. **WI7 — Phase 7 interop.** Engine: add `GenerateToWriter`/`GenerateUtf8`/`WritePiece`
   to `PrecompiledRuntime` (`src/Heddle/Precompiled/PrecompiledRuntime.cs`). Generator:
   emit sink entry points always and `WritePiece` piece calls under the u8 opt-in; bump
   emitted `schemaVersion` to 2; widen the engine registration gate to accept {1, 2}
   (D7). Extend the phase 7 differential harness with a byte-sink lane: every corpus
   fixture precompiled and rendered via `GenerateUtf8` must byte-match the runtime
   backend's string render UTF-8-encoded — with `HeddleEmitUtf8Pieces` both off and on.
   `PrecompiledUtf8FastPathTests` additionally asserts (via an instrumented test writer)
   that an opted-in template writes its static pieces without invoking the transcoding
   path. *Completion:* differential byte lane green; Verify.SourceGenerators snapshots
   updated and diff-reviewed; schema-1 assembly fixture still registers and renders.
8. **WI8 — Adversarial and guarantee tests.** New fixtures under
   `src/Heddle.Tests/TestTemplate/`: `streaming-unicode.heddle` +
   `generated-streaming-unicode.html` (multi-byte corpus seeded with the docs' own
   `Café — Привет!` strings, emoji, surrogate pairs, mixed scripts),
   `streaming-large.heddle` (> 1 MB output per D13). Test classes:
   `Utf8ChunkBoundaryTests` (chunked-tier inputs > 5 461 chars — the D5 tier boundary —
   with surrogate pairs positioned to straddle every chunk edge, a lone-surrogate row
   asserting the D5 replacement-fallback parity, plus the stingy-writer variant — written
   failing before the `Encoder.Convert` loop lands, per the TDD verdict),
   `SinkAllocationTests` (D13), `ConcurrentSinkRenderTests` (D15),
   `DownlevelDegradationTests` (net48/netstandard2.0 behavior: `TextWriter` path fully
   functional; byte-sink value writes fall back through strings — asserted by
   allocation-free-tier absence, not by output difference; output stays byte-identical).
   *Completion:* all green on all TFMs; chunk tests demonstrably red-before/green-after.
9. **WI9 — Benchmarks.** New `src/Heddle.Performance/SinkRenderBenchmarks.cs`
   (`[MemoryDiagnoser]`), run per runtime (net8, net10; D16), reporting the standard
   BenchmarkDotNet columns (Mean, Error, StdDev, Gen0/Gen1/Gen2, Allocated). Every
   benchmark, its workload, and its acceptance criterion:

   | Benchmark | Workload / sink | Acceptance criterion |
   | --- | --- | --- |
   | `RenderString` | The standing home-page workload (`TestTemplates/home.heddle` + `layout.heddle` via the `HeddleTest` setup) → `string Generate` | Mean within BenchmarkDotNet error of the WI1 baseline; Allocated **identical** to baseline (success criterion 4 — zero regression). |
   | `RenderTextWriter` | Same workload → `Generate(model, writer)` over a counting no-op `TextWriter` (measures engine-side cost with zero sink cost) | Allocated ≤ `RenderString` − (final output length × 2 B) — the full-output string is gone; mean in the same order as `RenderString`. |
   | `RenderUtf8Buffer` | Same workload → `Generate(model, bufferWriter)` over a reusable pooled buffer writer reset per iteration | Allocated < `RenderString` by at least the final output size, **on every runtime** (D13 asset 3 / success criterion 1); mean within the same order as `RenderTextWriter`. |
   | `RenderUtf8PiecesOnly` | A static-only fixture variant (the home page with dynamic values stripped) → byte sink | No gate — this is the **measurement** powering D6's ≥ 25 % transcode-share revisit trigger: `(RenderUtf8PiecesOnly mean) / (RenderUtf8Buffer mean)` bounds the static-piece transcode share. |
   | `RenderRazor` | The Razor equivalent via `RazorViewToStringRenderer` (existing runner) | No gate — retained context so the phase's numbers stay comparable with the engine's standing benchmark story. |

   Precompiled lane: the phase 7 differential benchmark gains a `GenerateUtf8` variant
   with `HeddleEmitUtf8Pieces` on, whose Allocated must not exceed the runtime-backend
   `RenderUtf8Buffer` figure (the u8 tier can only remove work). *Completion:* success
   criteria 1 and 4 numbers recorded per runtime; allocation columns satisfy D13(3);
   baselines archived per the testing-standards baseline policy.
10. **WI10 — Docs and index.** Update `docs/csharp-api.md` (sink overloads, the
    `Generate(data, null)` migration note, sink-ownership/flush guidance incl. the
    `Response.BodyWriter` pattern) and `docs/custom-extensions.md` (capability
    interfaces, span `Render` bridge, the "never cache the renderer" note restated).
    Update the [spec index](../README.md) row (done with this spec). The
    `samples/streaming-ssr` demo (minimal-API endpoint rendering into
    `Response.BodyWriter`, then `FlushAsync`) is deferred to the phase 9 gallery per
    the roadmap. *Completion:* docs build green (`npm run docs:build`, uppercase-drive
    cwd caveat).

## Public API contract

All additions carry XML docs and land in the phase 6 `PublicApiSurfaceTests` goldens in
the same change. Everything is additive; no member of any shipped type changes.

```csharp
namespace Heddle.Data
{
    /// <summary>Renderer capability: accepts character spans without requiring a string.</summary>
    public interface ISpanScopeRenderer : IScopeRenderer
    {
        void Render(ReadOnlySpan<char> data);
    }

    /// <summary>Renderer capability: accepts pre-encoded UTF-8 bytes (phase 7 u8 pieces).
    /// Encode proxies deliberately do not implement this interface.</summary>
    public interface IUtf8ScopeRenderer : ISpanScopeRenderer
    {
        void RenderUtf8(ReadOnlySpan<byte> utf8);
    }

    /// <summary>IScopeRenderer over a host-supplied TextWriter. Write-through: no internal
    /// buffering; never flushes or disposes the writer. Single render ownership (not thread-safe).</summary>
    public sealed class TextWriterScopeRenderer : ISpanScopeRenderer
    {
        public TextWriterScopeRenderer(TextWriter writer);          // ArgumentNullException
        public void Render(string data);                            // skips null/empty
        public void Render(ReadOnlySpan<char> data);                // net6+: writer.Write(span);
                                                                    // downlevel: ArrayPool<char> copy
    }

    /// <summary>IScopeRenderer over a host-supplied IBufferWriter&lt;byte&gt;, producing UTF-8.
    /// Write-through into writer-provided spans; never completes or flushes the writer.
    /// Single render ownership (not thread-safe).</summary>
    public sealed class Utf8ScopeRenderer : IUtf8ScopeRenderer
    {
        public Utf8ScopeRenderer(IBufferWriter<byte> writer);       // ArgumentNullException
        public void Render(string data);
        public void Render(ReadOnlySpan<char> data);                // D5 two-tier transcode
        public void RenderUtf8(ReadOnlySpan<byte> utf8);            // straight copy
    }

    public class HtmlEncodedRenderer : IScopeRenderer, ISpanScopeRenderer   // existing class
    {
        public void Render(ReadOnlySpan<char> data);                // new: 1.x string bridge (D9)
    }

    /// <summary>Sink-agnostic write helpers for extension authors.</summary>
    public static class ScopeRendererExtensions
    {
        public static void Render(this IScopeRenderer renderer, ReadOnlySpan<char> data);

        // net6.0 / net8.0 / net10.0 builds only (ISpanFormattable does not exist downlevel):
        public static void Render<T>(this IScopeRenderer renderer, T value, string format,
            IFormatProvider formatProvider) where T : struct, ISpanFormattable;
    }
}

namespace Heddle
{
    public sealed class HeddleTemplate   // existing class; IHeddleTemplate unchanged
    {
        /// <summary>Renders into a TextWriter with no full-output materialization.
        /// The caller owns the writer: nothing is flushed or disposed.</summary>
        public void Generate(object data, TextWriter writer, object chained = null, object callerData = null);

        /// <summary>Renders UTF-8 into an IBufferWriter&lt;byte&gt; (e.g. a PipeWriter) with no
        /// full-output materialization. The caller owns the writer and any FlushAsync.</summary>
        public void Generate(object data, IBufferWriter<byte> writer, object chained = null, object callerData = null);

        // internal: void Render(object data, object chained, object callerData, IScopeRenderer renderer);
    }
}

namespace Heddle.Precompiled
{
    public static class PrecompiledRuntime   // existing class; additions per D7
    {
        public static void GenerateToWriter(IProcessStrategy root, object model, object chained,
            object callerData, TextWriter writer);
        public static void GenerateUtf8(IProcessStrategy root, object model, object chained,
            object callerData, IBufferWriter<byte> writer);
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void WritePiece(in Scope scope, string piece, ReadOnlySpan<byte> utf8Piece);
    }
}
```

**Per-member contracts and the per-TFM story.** Every affected member's shape on each
build, normative (the API-availability facts behind each row are verified in Assumed
state — *Spans on downlevel TFMs*):

| Member | net6.0 / net8.0 / net10.0 builds | netstandard2.0 / net48 | Contract notes |
| --- | --- | --- | --- |
| `TextWriterScopeRenderer..ctor(TextWriter)` | identical | identical | `ArgumentNullException` on null; captures the writer, allocates nothing else. |
| `TextWriterScopeRenderer.Render(string)` | `writer.Write(data)` | identical | Skips `string.IsNullOrEmpty(data)` — mirrors `ScopeRenderer.Render`. |
| `TextWriterScopeRenderer.Render(ReadOnlySpan<char>)` | `writer.Write(span)` — the span overload exists netcoreapp2.1+, so all modern builds have it; `StreamWriter` and `HttpResponseStreamWriter` override it with true span paths | `ArrayPool<char>.Shared.Rent(span.Length)` → `CopyTo` → `writer.Write(array, 0, span.Length)` → `Return` in `finally` — the span overload does not exist downlevel | Skips empty spans. Note the BCL's own *base* `TextWriter.Write(ReadOnlySpan<char>)` does the same rent-copy dance — the downlevel fallback is the platform's own strategy, not an invention. |
| `Utf8ScopeRenderer..ctor(IBufferWriter<byte>)` | identical | identical | `ArgumentNullException` on null; the lazy `Encoder` field starts null. |
| `Utf8ScopeRenderer.Render(string)` | forwards to the span path via `data.AsSpan()` | identical (`AsSpan` via System.Memory) | Skips null/empty. |
| `Utf8ScopeRenderer.Render(ReadOnlySpan<char>)` | D5 two-tier: `Encoding.UTF8.GetBytes(chars, span)` single-call / span `Encoder.Convert` loop | same tiers; single-call via `fixed (char* c = …) fixed (byte* b = …) Encoding.UTF8.GetBytes(c, len, b, spanLen)`; chunked via the pointer `Encoder.Convert` overload under `fixed` with manual slice offsets (`AllowUnsafeBlocks` already on) | Byte output identical on every TFM — the overloads differ in calling convention only. Empty spans skipped before any `GetSpan`. |
| `Utf8ScopeRenderer.RenderUtf8(ReadOnlySpan<byte>)` | `BuffersExtensions.Write(writer, utf8)` | identical — `BuffersExtensions` is in the System.Memory package for netstandard2.0/net462 | Caller passes valid UTF-8 (engine callers only pass compiler-validated u8 pieces, D2); no validation is performed — validating would re-scan what the C# compiler already guaranteed. |
| `HtmlEncodedRenderer.Render(ReadOnlySpan<char>)` | `new string(span)` → existing `Render(string)` seam | `span.ToString()` → same seam (same allocation, different ctor surface) | The documented 1.x bridge cost (D9): one string per encoded span write, upgraded in the 2.0 window. Skips empty spans. |
| `ScopeRendererExtensions.Render(IScopeRenderer, ReadOnlySpan<char>)` | `renderer is ISpanScopeRenderer s ? s.Render(data) : renderer.Render(data.ToString())` | identical logic | The string materialization happens only for renderers that never learned spans (external `IScopeRenderer` implementations); all engine renderers implement `ISpanScopeRenderer` after this phase. |
| `ScopeRendererExtensions.Render<T>(…) where T : struct, ISpanFormattable` | full D10 ladder (UTF-8 tier gated `#if NET8_0_OR_GREATER` inside the body) | **member absent** — `ISpanFormattable` does not exist downlevel; the method is compiled out (`#if NET6_0_OR_GREATER`) and downlevel formatter builds keep their string-based code verbatim (D10) | The only public member with a per-TFM *surface* difference; recorded in the API golden per TFM (the phase 6 `PublicApiSurfaceTests` goldens are per-target). |
| `HeddleTemplate.Generate(object, TextWriter, …)` / `(object, IBufferWriter<byte>, …)` | identical | identical | `ArgumentNullException` on `writer`; all compile-guard exceptions of the string path; no flush/dispose (D4); null model legal. |
| `PrecompiledRuntime.GenerateToWriter` / `GenerateUtf8` | identical | identical | Construct the adapter, build the root `Scope` as `GenerateString` does, `root.Render(scope)` — no locals provisioning (the strategy arrives wrapped, D7). |
| `PrecompiledRuntime.WritePiece(in Scope, string, ReadOnlySpan<byte>)` | `scope.Renderer is IUtf8ScopeRenderer u8 ? u8.RenderUtf8(utf8Piece) : scope.Renderer.Render(piece)` | identical | `AggressiveInlining`; the *only* u8/string decision point (D7). Under encode proxies the type test fails by design (D2/D9). |

Thread-safety summary: the two adapter classes and every `Scope` derived from them are
single-render, single-thread artifacts; `ScopeRendererExtensions` is stateless;
`PrecompiledRuntime` additions are static and stateless (they construct per-call
adapters); `HeddleTemplate` keeps its existing concurrent-render contract (D15).
Generated-code surface (consumer assemblies, normative via D7): typed entry points gain
the two sink overloads mirroring `Generate`'s shape with the model strongly typed (full
emitted forms in D7).

## Diagnostics

None. This phase claims no `HED8xxx` IDs (D14) — no compile-path behavior is added, and
the emitter update reuses phase 7's existing IDs (HED7005 u8 downgrade, HED7102 schema
gate). The `HED8xxx` block remains reserved to phase 8 in the
[registry](../common/cross-cutting-decisions.md#d1--stable-diagnostic-ids-hedxxxx) and
unused. Host-programming errors throw at entry points per the
[coding standards](../common/coding-standards.md#error-handling-and-diagnostics).

## Testing plan

TDD verdict carried from the roadmap, unweakened: **characterization-first, then
test-first against the property oracle.** Pin the untouched engine (WI1) before any
seam work; develop the adapters against `UTF8(stringResult)` equality across the whole
corpus (stronger than hand-written expectations); write the chunk-boundary fixtures
failing before the `Encoder.Convert` loop exists; gate the perf-sensitive steps with
benchmarks, not unit tests. The verification loop instantiated: WI1 oracle → WI3 seam
step with zero-delta gate → adapters test-first (WI2/WI4) → adversarial unicode before
chunking (WI8) → formatter/per-TFM goldens (WI5) → combined run of everything for phase
exit.

Named assets this phase leaves behind (homes per the
[testing standards](../common/testing-standards.md#suite-homes)):

| Asset | Home | Purpose |
| --- | --- | --- |
| `SinkParityTests` | Heddle.Tests | Property oracle: corpus × three sinks, byte-identical (UTF-8-normalized). |
| `SinkEncodingParityTests` | Heddle.Tests | D9 matrix: sinks × profiles × backends, golden-pinned. |
| `Utf8ChunkBoundaryTests` | Heddle.Tests | Surrogates straddling every chunk edge; stingy-writer `GetSpan` compliance. |
| `streaming-unicode.heddle` / golden | Heddle.Tests/TestTemplate | Multi-byte torture fixture (docs-seeded corpus). |
| `streaming-large.heddle` | Heddle.Tests/TestTemplate | > 1 MB output fixture powering the allocation bound. |
| `SinkAllocationTests` | Heddle.Tests | D13 operational guarantee (bounded + size-invariant), net6+ TFMs. |
| `ConcurrentSinkRenderTests` | Heddle.Tests | D15: parallel mixed-sink renders byte-identical. |
| `DownlevelDegradationTests` | Heddle.Tests | net48/netstandard2.0 documented degradation is itself tested. |
| `FormatterSpanGoldenTests` | Heddle.Tests | D10: formatter output identical per TFM, span tiers active where supported. |
| Differential byte lane + `PrecompiledUtf8FastPathTests` | Heddle.Generator.IntegrationTests | D7: precompiled byte renders match; u8 pieces bypass transcoding. |
| `SinkRenderBenchmarks` | Heddle.Performance | Success criteria 1/4; D6 trigger measurement; per-runtime baselines. |

Regression gate: the standard combined invocation (build all TFMs; full test suite with
goldens byte-identical; grammar-stability — no `generated/` diff; benchmarks per WI1/WI9
with the allocated-bytes-never-increase rule; docs build for WI10). Phase exit = roadmap
success criteria 1–7 green in one combined run — criterion 6's ASP.NET endpoint runs as
a test-project minimal host in WI8's suite now, and graduates to the
`samples/streaming-ssr` gallery item in phase 9 (deferred deliverable, recorded).

## Back-compat and migration

- **Behavior preserved, proven:** `string Generate` output and allocation profile are
  untouched by construction (D1) and gated by benchmark (WI3); the golden corpus runs
  byte-identical; `IScopeRenderer`, `IHeddleTemplate`, `IExtension`, `IProcessStrategy`
  signatures unchanged; extension authors' `scope.Renderer.Render(string)` works against
  every sink unchanged.
- **Known source-compat corner:** positional `Generate(data, null)` becomes CS0121
  (D3); migration note ships in `csharp-api.md`. No binary breaks anywhere.
- **Phase 7 consumers:** schema-1 precompiled assemblies keep registering and render on
  all sinks; regenerating with the phase 8 toolchain yields schema 2 + sink entry
  points (D7).
- **2.0 window:** this phase adds nothing to the window and obstructs nothing in it —
  the encoder swap lands inside `HtmlEncodedRenderer`/`AbstractHtmlExtension` exactly as
  phase 2 D11 planned, upgrading the D9 span bridge from string-based to span/`EncodeUtf8`
  encoding with no phase 8 API change.

## Performance considerations

Hot paths touched and their guards: the render prologue refactor (WI3 — guarded by
`RenderString` zero-delta), `@partial` render (WI6 — home-page allocation must not
rise), formatter `RenderDataInternal` (WI5 — formatter goldens + benchmark), generated
piece writes (WI7 — one inlined capability test per piece; differential benchmarks).
Allocation expectations: byte-sink renders allocate O(V + W) per D13; `TextWriter`
renders allocate only value strings (plus the downlevel `ArrayPool` span bridge);
`WritePiece`'s type test is a guarded, JIT-devirtualizable check. The string path's
adaptive high-water behavior is bit-identical (D4). Per-runtime baselines (D16). The D6
transcode cost is measured, with a numeric revisit trigger, not assumed away.

## Standards compliance

Where the balanced-mode principles actually bit in this phase: **OCP/ISP** — the sink
seam reuses the shipped `IScopeRenderer` boundary and adds capabilities as small opt-in
interfaces instead of touching a shipped interface (D1/D2), exactly the sanctioned
pattern; **YAGNI** cut the async surface (D8), the runtime u8 cache (D6), a public
streaming interface on `IHeddleTemplate` (D3), engine-side sink buffering (D4), and a
speculative `IBufferWriter<char>` sink — each with a named trigger under Deferred items;
**DRY** consolidated the render prologue into one internal core with three consumers
(D11) and the value-format TFM ladder into one funnel (D10) — but the spec explicitly
licenses un-DRYing the render core if the benchmark says so (WI3), because measured
hot-path performance outranks DRY in the precedence order; **SRP** kept encoding in the
phase 2 seams and transcoding in the sink adapters — the two never blend (D9).
Public-API additions follow the FDG additive rules with the one recorded source corner
(D3).

## Deferred items

| Item | Revisit trigger |
| --- | --- |
| Async flush seam (`ValueTask FlushAsync(CancellationToken)` on a sink wrapper) or any async render API | A concrete integration that cannot flush between renders, or ratified deferred-region SSR demand (D8). |
| Async model resolution | Same record as the roadmap; concrete demand only (D8). |
| Runtime-document UTF-8 piece cache | `SinkRenderBenchmarks` attributing ≥ 25 % of byte-sink render time to static-piece transcoding (D6). |
| Public streaming interface (widened `IHeddleTemplate` sibling) | A DI-abstraction consumer that cannot use the sealed class (D3). |
| `IBufferWriter<char>` sink (Fluid's `IFluidOutput` shape) | A consumer that needs char-buffer output; today `TextWriter` covers the char side. |
| Span formatting for boxed primitives in the unnamed carrier (`EmptyExtension`/`EmptyHtmlExtension`) | A benchmark showing carrier `ToString` allocations matter on the home-page workload (D10 scoped to the formatter built-ins). |
| Atomic `_runners` bookkeeping | Any observed dispose-race defect; it predates this phase (D15). |
| `HeddleEmitUtf8Pieces` default-on | Phase 7 D15's own trigger (ecosystem LangVersion floor). |
| `samples/streaming-ssr` gallery demo | Phase 9 harness (scheduled; the in-test minimal host ships now, WI8). |

## External references

- [`IBufferWriter<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1) ·
  [`IBufferWriter<T>.GetSpan`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.ibufferwriter-1.getspan) ·
  [System.Buffers overview](https://learn.microsoft.com/en-us/dotnet/standard/io/buffers) —
  GetSpan/Advance contract, sizeHint semantics, "common problems" list (D4/D5).
- [`PipeWriter`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter) ·
  [`PipeWriter.UnflushedBytes`](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipelines.pipewriter.unflushedbytes) ·
  [`HttpResponse.BodyWriter`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.http.httpresponse.bodywriter) ·
  [Request and response operations in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/request-response) ·
  [dotnet/aspnetcore discussion #62464](https://github.com/dotnet/aspnetcore/discussions/62464) —
  the synchronous-render + host-`FlushAsync` pattern (D4/D8).
- [`TextWriter.WriteAsync`](https://learn.microsoft.com/en-us/dotnet/api/system.io.textwriter.writeasync) ·
  [dotnet/aspnetcore #49172](https://github.com/dotnet/aspnetcore/issues/49172) ·
  [dotnet/aspnetcore #38210](https://github.com/dotnet/aspnetcore/issues/38210) —
  per-call `Task`, single in-flight op, response-writer buffering realities (D8).
- [ViewBuffer.cs](https://github.com/dotnet/aspnetcore/blob/main/src/Mvc/Mvc.ViewFeatures/src/Buffers/ViewBuffer.cs) ·
  [dotnet/aspnetcore #65605](https://github.com/dotnet/aspnetcore/issues/65605) —
  Razor's paged view buffering and write points (D8).
- [Understanding the Whys, Whats, and Whens of ValueTask](https://devblogs.microsoft.com/dotnet/understanding-the-whys-whats-and-whens-of-valuetask/) —
  the recorded shape for any future async seam (D8).
- [`Encoder.Convert`](https://learn.microsoft.com/en-us/dotnet/api/system.text.encoder.convert) ·
  [Character encoding in .NET](https://learn.microsoft.com/en-us/dotnet/standard/base-types/character-encoding-introduction) —
  stateful chunked transcoding, surrogate carry-over (D5).
- [`WebUtility.HtmlEncode`](https://learn.microsoft.com/en-us/dotnet/api/system.net.webutility.htmlencode) ·
  [`TextEncoder.Encode`](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.textencoder.encode) ·
  [`TextEncoder.EncodeUtf8`](https://learn.microsoft.com/en-us/dotnet/api/system.text.encodings.web.textencoder.encodeutf8) ·
  [dotnet/runtime PR #114494](https://github.com/dotnet/runtime/pull/114494) —
  encoder surfaces now and at the 2.0 swap; .NET 10 SearchValues-backed scan (D9).
- [`ISpanFormattable`](https://learn.microsoft.com/en-us/dotnet/api/system.ispanformattable) ·
  [`IUtf8SpanFormattable`](https://learn.microsoft.com/en-us/dotnet/api/system.iutf8spanformattable) ·
  [`Utf8.TryWrite`](https://learn.microsoft.com/en-us/dotnet/api/system.text.unicode.utf8.trywrite) —
  the per-TFM formatting ladder (D10).
- [System.Memory package](https://www.nuget.org/packages/System.Memory) — spans and
  `IBufferWriter<T>` on netstandard2.0/net462 (WI2); latest 4.6.3, targets verified
  July 2026. Package surface confirmed against the
  [System.Memory reference source](https://github.com/dotnet/corefx/blob/release/2.1/src/System.Memory/ref/System.Memory.cs):
  `IBufferWriter<T>` and `BuffersExtensions.Write` present, `ArrayBufferWriter<T>`
  absent (Assumed state, WI2).
- [`BuffersExtensions.Write`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.buffersextensions.write) —
  the `RenderUtf8` straight-copy primitive (D5); `System.Memory.dll`.
- [`ArrayBufferWriter<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraybufferwriter-1) —
  applies-to netstandard2.1/netcoreapp3.0+ only — the verified reason WI2's net48 lane
  uses test-owned writers.
- [`TextWriter.Write`](https://learn.microsoft.com/en-us/dotnet/api/system.io.textwriter.write) —
  the `Write(ReadOnlySpan<char>)` overload is netcoreapp2.1+/netstandard2.1 (absent from
  netstandard2.0/net48 — the `TextWriterScopeRenderer` downlevel `ArrayPool` fallback);
  `Write(char[], int, int)` reaches every TFM.
- [`Guid.ToString`](https://learn.microsoft.com/en-us/dotnet/api/system.guid.tostring) —
  null/empty format defaults to `"D"`; invalid specifiers throw `FormatException`
  (the D10 `guid` row's preserved behavior).
- [`ArrayPool<T>`](https://learn.microsoft.com/en-us/dotnet/api/system.buffers.arraypool-1) —
  the downlevel char-span bridge in `TextWriterScopeRenderer` (API contract table).
- [Breaking changes and .NET libraries](https://learn.microsoft.com/en-us/dotnet/standard/library-guidance/breaking-changes) —
  the D3 overload-addition classification.
- [Fluid `IFluidOutput`](https://github.com/sebastienros/fluid/blob/main/Fluid/IFluidOutput.cs) ·
  [`RazorPageBase.Output`](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.mvc.razor.razorpagebase.output) —
  ecosystem sink shapes (considered in D2/D3).
