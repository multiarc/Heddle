# Benchmark harness assessment — resolution status

Assessment of the .NET benchmark leg (`benchmarks/dotnet`) and the Heddle sink render paths,
originally performed 2026-08-02 against `c74c3b37` ("New bench"). Re-assessed 2026-08-04 against
`5cd58406`. **All six findings are resolved**, verified by code review, a clean Release build
(0 warnings, 0 errors), and `selftest` (141/141 checks: sink materialisation, six-technique
differential, encoded security floor). Two trivial doc leftovers remain — listed at the end.

**Discharged 2026-08-04:** `docs/benchmarks/` has been regenerated with the fixed harness. The
2026-08-02 tables — measured by the pre-flip anchor (utf8 checksum sink) — are replaced by the
full six-ecosystem run of 2026-08-03 (`docs/benchmarks/2026-08-03/`), measured on the Windows
protocol machine (newest commit at run start: `5cd58406`) and consolidated with `--check` green.

---

## 1. Bench-path sinks computed checksums with heavy overhead — RESOLVED (`72bebc62`)

Implemented as proposed, and further:

- `Bench/BenchSinks.cs` (new): `BenchBufferWriter` (`Advance` moves an offset, nothing else —
  the pooled-`PipeWriter` floor) and `BenchTextWriter` (bulk `CopyTo` in every overload, no
  per-char virtual dispatch). Pre-sized in `[GlobalSetup]` past the high-water mark
  (`oracle.Length * 4 + 4096` covers `Utf8ScopeRenderer`'s 3× worst-case span reservation),
  reused via `Reset()` — the per-op 64 KB allocation and per-unit FNV are gone from every timed
  region.
- `RenderToSink` was replaced by `RenderToBuffer` / `RenderToWriter` / `RenderToString` with
  **caller-owned sinks and a hoisted model** — the old shape also called `ModelFor` inside every
  timed iteration, an allocation the precompiled suite never paid; that asymmetry (which
  compressed the runtime-vs-precompiled ratio toward 1.0) is fixed too.
- Benchmark bodies return the written count; the anti-elision proof moved to setup:
  `CrossStackSuite.PrepareBenchSinks` / `TechniqueSetup.AssertSinks` render once through the
  exact bench sinks and assert byte/char equality against the gated string render, per process,
  untimed. The gate's `Checksum*` writers are untouched and still back `SelfTest` and the
  MATERIALISATION-CHECK trailer.

## 2. `"Heddle (utf8)"` name collision/mislabel — RESOLVED (`72bebc62`)

`Name` is now `"Heddle"` (with a doc note recording the old defect); non-anchor cells are
labeled `"Heddle (utf8 sink)"` / `"Heddle (textwriter sink)"`, matching `consolidate.py`'s
display names — one vocabulary across gate output, bench IDs, and report. No two cells share a
name.

## 3. Stale documentation contradicting the anchor flip — RESOLVED (`72bebc62`)

- `HeddleEngine.cs` class header now states the string-sink anchor rationale ("materialise your
  runtime's native string" invariant), records the earlier utf8-anchor reasoning as a corrected
  mistake, and keeps the LOH observation as the reason the utf8 *technique row* is interesting
  on composed-page — exactly the proposed shape.
- `Registry.cs` `Cell.InCrossStack` doc now describes the string-sink flag and the non-ranked
  technique rows.
- The dangling `RenderHeddleSink` cref now points at `RenderHeddleUtf8Sink` /
  `RenderHeddleTextWriterSink`.

## 4. `HtmlEncodedRenderer` defeated the UTF-8 sink on encoded output — RESOLVED (`a773a509`)

Stages 1 and 2 of the proposal implemented, invariant preserved (the proxy still never
implements `IUtf8ScopeRenderer`):

- Clean-span pass-through: `FindFirstCharacterToEncode` (configured encoder) or a hand-written
  scanner mirroring `WebUtility.HtmlEncode`'s exact trigger set (legacy path); spans with
  nothing to encode forward directly to the inner span sink, zero allocation, and a clean
  prefix is forwarded before the dirty tail.
- Chunked encode for dirty tails: `TextEncoder.Encode(span, stackalloc char[256], …)` loop with
  a correct zero-progress fallback to the string path (oversized encoded scalar).
- Covered by a new 244-line `HtmlEncodedRendererSpanParityTests` suite plus the existing
  encoded-workload byte gate and security floor.
- Stage 3 (`EncodeUtf8` straight-to-UTF-8) remains deliberately deferred pending profiling, as
  proposed.

## 5. Runtime backend re-transcoded static text every render — RESOLVED (`72bebc62`)

`RuntimeDocument` now caches UTF-8 eagerly at document build: `DocumentStrategy._documentUtf8`
and `DataProcessor.PieceUtf8` (encoded once in the `NormalStrategy` constructor). Both `Render`
paths take the `WritePiece` shape — `IUtf8ScopeRenderer` gets `RenderUtf8(cachedBytes)`, all
other sinks get the chars — with the renderer type-tested once per document, not per piece.
Static pieces still reach the sink directly (never through the encode proxy), so the
never-bypass-encoding invariant is untouched. "Bytes prepared in final form" now holds on both
backends for static content; byte-identity proven by the technique differential (selftest) and
the strict `gate-precompiled` parity gate.

## 6. Minor nits — RESOLVED (`72bebc62`)

`ChecksumTextWriter.Encoding` returns `Encoding.Unicode` with a comment recording the
correction; the gate's double-render comment remains accurate under the new API.

---

## Residual doc leftovers (trivial, no behavior impact)

1. `Bench/TechniqueBenchmarks.cs` class header (~line 18) still says the cross-stack row "is
   the runtime UTF-8 sink" — the ranked row is the runtime **string** sink. One-line fix.
2. `Engines/HeddleEngine.cs:167` — a comment in the gate's TextWriter case still references
   `RenderToSink`, which no longer exists (now `RenderToBuffer`/`RenderToWriter`/
   `RenderToString`).

## Verification performed (2026-08-04)

- `dotnet build -c Release`: 0 warnings, 0 errors.
- `dotnet run -c Release -- selftest`: 141 passed, 0 failed — includes sink materialisation
  (count + checksum vs string render, all eight workloads) and the six-technique differential
  (runtime + precompiled × three sinks, byte-for-byte), which runtime-proves the `PieceUtf8`
  cache and the `HtmlEncodedRenderer` chunked path.
- Grep confirms no non-comment references to the removed `RenderToSink` API remain.

## Verified-sound (unchanged from the original assessment)

- `Generate(model, IBufferWriter<byte>)` streams UTF-8 directly into caller-provided spans —
  no full-output string, no intermediate `byte[]`, correct surrogate handling in the chunked
  `Encoder.Convert` loop.
- The gate architecture: corpus byte gate + security floor, sink count/checksum
  cross-assertions, six-technique differential, strict 3-sink runtime-vs-precompiled parity
  gate — all in the process that times.
- The string anchor is like-for-like with all five .NET competitors, and `consolidate.py`
  quarantines the technique rows from every ranking, margin, and summary.
