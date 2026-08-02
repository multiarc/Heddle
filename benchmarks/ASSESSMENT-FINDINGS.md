# Benchmark harness assessment — findings and proposed fixes

Assessment of the .NET benchmark leg (`benchmarks/dotnet`) and the Heddle sink render paths,
performed 2026-08-02 against `c74c3b37` ("New bench"). Each finding carries a proposed fix
concrete enough to implement directly. Ordered by priority.

**Out of scope here (deferred by decision):** the published `docs/benchmarks/2026-08-02` tables
were measured by the pre-flip harness (old utf8-sink anchor: no `RenderHeddleUtf8`/
`RenderHeddleTextWriter` rows, 65,808 B allocated on the 338 B trivial-substitution, no Gen2 on
composed-page). Regenerate with `bench-crossstack` after the fixes below land.

---

## 1. Bench-path sinks must stop computing checksums — minimal materialization instead

**Where:** `HeddleEngine.RenderToSink` (`Engines/HeddleEngine.cs:232-259`),
`Gate/Materialisation.cs` (`ChecksumBufferWriter`, `ChecksumTextWriter`),
`CrossStackSuite.RenderHeddleUtf8Sink` / `RenderHeddleTextWriterSink`, `TechniqueBenchmarks`.

**Problem:** the timed sink rows fold an FNV-1a hash over every output unit — for the
TextWriter sink through a *per-char virtual call* — and allocate a fresh 64 KB buffer per op.
No competitor row pays anything like this: their timed body is "engine renders, string comes
out." The sink rows therefore measure harness plumbing as much as the engine, which both
distorts them and makes them useless as a fair "what does streaming cost" answer.

**Design principle for the fix:** *verification belongs to the gate, measurement to the bench.*
Correctness of every sink is already proven, untimed, in the same process before anything is
timed: `CrossStackSuite.Setup` gates every cell, and `SelfTest.SinkMaterialisation` +
`TechniqueDifferential` assert count, checksum, and byte-for-byte parity through the checksum
writers. Re-proving content on every timed iteration is redundant — the historical failures the
checksums guard against (count-only sink, the JS lazy-rope run) are *engine/sink laziness*
properties, established once per process, not per iteration.

**Proposed fix — two new bench-only sinks, the simplest honest materialization possible:**

```
// Bench/BenchSinks.cs (new) — measurement sinks; never used by the gate.

sealed class BenchBufferWriter : IBufferWriter<byte>
{
    byte[] _buffer;            // pre-sized once (see sizing below), reused every op
    int _written;
    GetSpan/GetMemory  => _buffer.AsSpan(_written) (grow only if short — setup makes this cold);
    Advance(count)     => _written += count;              // nothing else. No hash, no read-back.
    Reset()            => _written = 0;
    int WrittenCount   => _written;
}

sealed class BenchTextWriter : TextWriter
{
    char[] _buffer;            // pre-sized once, reused every op
    int _length;
    Write(char c)                    => _buffer[_length++] = c;
    Write(string s)                  => s.AsSpan().CopyTo(_buffer.AsSpan(_length)); _length += s.Length;
    Write(char[] b, int i, int n)    => bulk copy;        // Buffer/Span copy, no per-char loop
    Write(ReadOnlySpan<char> s)      => bulk copy;
    Reset() / int Length
}
```

- **UTF-8 sink row:** the engine writes bytes directly into the reusable buffer; the sink does
  literally nothing but advance an offset. This is the true floor — identical to what a pooled
  `PipeWriter` costs a real streaming caller.
- **TextWriter sink row:** one bulk `CopyTo` per engine write. This is the minimal work any
  real char-stream consumer performs (a `StringWriter`/`StreamWriter` does at least this), so
  the row measures "engine + cheapest possible consumer," which is the fair definition of the
  technique.
- **Return value:** `RenderToSink` returns the written count (`long`) instead of a hash;
  BenchmarkDotNet consumes it, so the call cannot be dropped. The engine's writes themselves
  cannot be elided by the JIT — they are stores through spans into escaping heap arrays across
  non-inlined virtual calls. The string technique row in `bench-techniques` returns
  `output.Length` for symmetry (producing the string is the work; consuming its length
  suffices).
- **Reuse and sizing:** one instance per suite, `Reset()` at the top of each call —
  BenchmarkDotNet executes benchmark bodies sequentially, so this is safe. Pre-size in
  `[GlobalSetup]` by rendering the workload once through the same sink (untimed) and keeping
  the high-water capacity; that also absorbs `Utf8ScopeRenderer.RenderSingle`'s 3×-worst-case
  `GetSpan` reservation so no growth ever lands in the timed region. This removes the per-op
  64 KB allocation entirely — the sink rows' `Allocated` column then reflects the engine, not
  the harness.
- **Anti-elision guard, kept but moved out of the loop:** in `[GlobalSetup]` (or SelfTest),
  render once through each *bench* sink and assert count + decoded content equal the gated
  string render. That pins "these exact sinks saw the full output in this process" without
  taxing a single timed iteration. The existing `ChecksumBufferWriter`/`ChecksumTextWriter`
  stay exactly as they are for the gate path (`HeddleEngine.Render`) and
  `SelfTest`/`MaterialisationTrailer` — their cost is irrelevant there.
- **Doc updates:** `CrossStackSuite.RenderHeddle*Sink` and the `RenderToSink` summary currently
  say "returns a checksum folded in as the engine writes" — rewrite to describe count-return +
  gate-proven sinks. `Materialisation.cs`'s header stays valid (it documents the *gate*
  writers) but should state explicitly that bench sinks are intentionally dumb and why that is
  now safe (gate + setup assertion run first in the same process).

**Effect:** the utf8/textwriter rows become directly interpretable — engine streaming cost plus
an honest minimal consumer — and the within-sweep asymmetry (sink rows paying FNV the anchor
never paid) disappears, which also deletes finding 3's disclosure obligation from the previous
revision of this file.

## 2. Anchor cell name collision and mislabel — `"Heddle (utf8)"` names the string path

**Where:** `Engines/HeddleEngine.cs` — `Name` const (line 38) and `Cells` (line 208).

**Problem:** the string-sink anchor cell is displayed as `"Heddle (utf8)"` (the stale `Name`
const), and the *actual* utf8-sink cell computes the identical label via `SinkLabel(Sink.Utf8)`.
The gate prints two indistinguishable `Heddle (utf8)/<track>/<workload>` lines per workload, the
anchor is labeled with the one technique it no longer uses, and any future lookup keyed on
`Cell.Engine` alone is ambiguous (`CrossStackSuite.Setup` resolves correctly only because its
`InCrossStack` filter happens to disambiguate).

**Proposed fix:** rename `Name` to `"Heddle"`. Verified safe: `Controlled.AssertCell` /
`Verifier.AssertCell` key the corpus by *workload* and use the engine string only in failure
messages; `CrossStackSuite.Engines` maps its `"Heddle"` key to `HeddleEngine.Name` and follows
the rename automatically; `consolidate.py` keys .NET rows off benchmark *method* names, so the
report is unaffected. Optionally align the non-anchor cell labels with the report's display
names — `"Heddle (utf8 sink)"` / `"Heddle (textwriter sink)"` — so gate output and consolidated
tables use one vocabulary. Update the `Name` const's doc ("The technique suffix is
deliberate…"), which describes the old scheme.

## 3. Stale documentation contradicting the committed design

Three blocks still argue the pre-flip decision and will mislead the next reader:

- **`HeddleEngine.cs` class header (lines 21–33):** still says "Only UTF-8 is wired into the
  cross-stack sweep… Ranking .NET on the UTF-16 path was measuring an allocator cliff." This is
  the argument `CrossStackSuite.Render`'s new doc explicitly repudiates.
  **Fix:** rewrite to the current shape — string anchor in the sweep (like-for-like with the
  five .NET competitors), utf8/textwriter measured as technique rows beside it, all three sinks
  gated; keep the LOH observation but as a note on *why the utf8 row is interesting on
  composed-page*, not as the anchor rationale.
- **`Registry.cs` `Cell.InCrossStack` doc (lines 27–33):** says "only the UTF-8 one is wired
  into run-all and the report (ledger E10)". **Fix:** "the STRING sink carries the flag; the
  utf8/textwriter sinks are measured as non-ranked technique rows in the same sweep and
  compared exhaustively in bench-techniques."
- **`CrossStackSuite.cs` line 113:** `<see cref="RenderHeddleSink"/>` is a dangling cref.
  **Fix:** point at `RenderHeddleUtf8Sink` (and mention `RenderHeddleTextWriterSink`).

Also confirm the benchmarks ledger entry that recorded the anchor decision (E10 /
MATERIALISATION-CHECK area) reflects the flip.

## 4. Library — `HtmlEncodedRenderer` defeats the UTF-8 sink on encoded workloads

**Where:** `src/Heddle/Data/HtmlEncodedRenderer.cs:29-51`.

**Problem:** on `OutputProfile.Html` output, every encoded value takes span →
`new string(span)` → `TextEncoder.Encode(string)` (second allocation) → char `Render(string)` →
UTF-16→UTF-8 transcode. The proxy deliberately not implementing `IUtf8ScopeRenderer` is sound
(pre-encoded bytes must never bypass encoding) — but the *implementation* of the proxy can be
allocation-free without weakening that invariant, and today it also makes the
`IUtf8SpanFormattable` zero-alloc value path unreachable behind it.

**Proposed fix, staged, invariant preserved (the proxy still never exposes `RenderUtf8`):**

1. **Clean-span pass-through (small, high value):** in `Render(ReadOnlySpan<char>)`, call
   `_encoder.FindFirstCharacterToEncode(span)`; if `-1`, forward the span directly to the inner
   `ISpanScopeRenderer` — zero allocations for the common nothing-to-escape case. (Legacy
   `_encoder == null` path keeps current behavior.)
2. **Chunked encode for dirty spans:** encode via
   `TextEncoder.Encode(ReadOnlySpan<char> src, Span<char> dst, out consumed, out written)` into
   a stackalloc char buffer in a loop, forwarding each chunk to the inner span renderer. Zero
   allocation, any input size, still funnels 100% of chars through the encoder.
3. **Optional phase 2 (measure first):** when the inner sink is a UTF-8 sink, encode straight
   to UTF-8 via `TextEncoder.EncodeUtf8` (transcode chunk → stackalloc utf8 → encode into the
   writer's span). Only worth it if profiling shows the char-chunk path hot after (1)+(2);
   keep the proxy's public surface unchanged either way.
4. `Render(string)` becomes `Render(value.AsSpan())` so both overloads share the new path.

Gate/tests already in place to protect this change: encoded-suite byte gate + security floor,
`SinkParityTests`, and the technique differential — all assert byte-identical output across
sinks. Benchmark relevance: this is why the utf8 sink's advantage shrinks on `fortunes-encoded`
and `encoded-loop`; after the fix the technique rows on encoded workloads become meaningful.

## 5. Library — runtime backend transcodes static text every render

**Where:** `src/Heddle/Runtime/RuntimeDocument.cs` — `DocumentStrategy.Render` (line 238) and
`NormalStrategy.Render` (line 308); static pieces are UTF-16 strings, so every UTF-8-sink render
re-transcodes all static content via `Encoding.UTF8.GetBytes`. Pre-encoded final-form bytes
exist only on the precompiled backend (`PrecompiledRuntime.WritePiece` + `u8` literals).

**Proposed fix:** cache the UTF-8 encoding of static pieces on the runtime document, eagerly at
document build (templates compile once; the encode cost is one-time and off the render path):

- Store `byte[] PieceUtf8` beside `Piece` in the `DataProcessor` element (and the single
  `_document` in `DocumentStrategy`).
- In both `Render` methods: `if (scope.Renderer is IUtf8ScopeRenderer u8) u8.RenderUtf8(element.PieceUtf8); else scope.Renderer.Render(element.Piece);` — exactly the shape of the
  precompiled `WritePiece`, whose semantics the parity gates already pin. Static pieces reach
  the sink directly (never through the encode proxy), so the never-bypass-encoding invariant is
  untouched.
- Memory cost: ~+50% of static content per compiled template for ASCII-dominated templates
  (UTF-8 bytes beside UTF-16 chars). If that matters for large template sets, make it lazy
  (encode on first UTF-8 render, `Volatile`/`LazyInitializer`), but eager is simpler and the
  default recommendation.
- Safety net already exists: `SelfTest.TechniqueDifferential`, `SinkParityTests`, and the
  benchmark byte gate all fail on any divergence between sinks.

This closes the gap the assessment identified: after it, "bytes already prepared in final form"
holds on *both* backends for static content, and the runtime utf8 row stops paying a per-render
transcode tax the precompiled row doesn't.

## 6. Minor nits

- `ChecksumTextWriter.Encoding` returns `Encoding.UTF8` while consuming chars —
  return `Encoding.Unicode` for honesty (`Gate/Materialisation.cs:39`). One-line.
- `HeddleEngine.Render`'s `Sink.TextWriter` gate case renders twice (checksum writer + string
  for the oracle). Correct and untimed; keep the explanatory comment accurate if the gate flow
  changes during fix 1.
- After fix 1, `ChecksumBufferWriter`'s `Grow` and the 3×-worst-case reservation only ever run
  on the gate path; no further action.

---

## Verified-sound (no action)

For the record, the assessment confirmed:

- `Generate(model, IBufferWriter<byte>)` streams UTF-8 directly into caller-provided spans —
  no full-output string, no intermediate `byte[]`, correct surrogate handling in the chunked
  `Encoder.Convert` loop (input is always the true remainder, so `flush: true` is sound).
- The gate architecture is genuinely strong: corpus byte gate + security floor, sink
  count/checksum cross-assertions, six-technique differential, and the strict 3-sink
  runtime-vs-precompiled parity gate (`gate-precompiled`) — all in the process that times.
- The string anchor is like-for-like with all five .NET competitors (cached parsed template →
  materialized string), and `consolidate.py` correctly quarantines the technique rows from
  every ranking, margin, and summary.
- `newest_go_artifact` (oldest-file bug fix) and the HED7031 build-time coverage diagnostic are
  correct improvements.
