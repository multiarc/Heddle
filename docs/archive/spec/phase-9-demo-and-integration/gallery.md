# Phase 9 gallery supplement — samples layout, authoring template, inventory, CI harness

Supplement to the [phase 9 specification](README.md). This document pins the
integration demo gallery for the implementer: the `samples/` layout and conventions,
the sample-authoring template (written as the standing template future samples use),
the complete inventory — one entry per deferred item collected from the phase 1–8 specs
plus the roadmap's current-engine demos — with each sample's golden shape, and the full
CI workflow. Decision rationale lives in the entry document (D11–D13, D15).

## Layout and conventions

```
samples/
  README.md                    ← gallery index (the D13 table) + the authoring template below
  tools/
    compare-golden.sh          ← shared comparer (contract below)
    compare-golden-selftest.sh ← proves the comparer can fail (WI1 check)
  Heddle.Samples.sln           ← every sample project + engine ProjectReferences
  ssr-aspnetcore/ … streaming-ssr/   (ten folders, one per inventory row)
```

Per-sample skeleton (normative):

```
samples/<name>/
  README.md            ← the sample's spec: one-paragraph story, prerequisites,
                          run instructions followable verbatim (criterion 6),
                          what the golden pins and why
  <PascalName>.csproj  ← net10.0; ProjectReference to the engine project(s) it shows
  Program.cs
  templates/           ← .heddle files (and imports) the sample renders
  golden/              ← committed expected outputs, LF line endings
  out/                 ← written by --capture; gitignored
```

Conventions (restating the entry doc's D11 as the checklist a sample author works down):

1. **Two modes.** `dotnet run` is the human mode the README narrates (serve, print,
   explore). `dotnet run -- --capture out` is the CI mode: write every deterministic
   artifact into `out/`, exit 0 on success, non-zero on any internal assertion failure.
2. **Determinism.** Fixed model data (no clocks, no GUIDs, no randomness), invariant
   culture, ordered enumerations. Web samples bind port 0 in capture mode, self-request
   with `HttpClient`, write response bodies, and shut down.
3. **Goldens.** One file in `golden/` per captured artifact, same relative names as
   `out/`. Comparison is byte-identical after CRLF→LF normalization on both sides;
   the file *sets* must match exactly. `UPDATE_GOLDEN=1 bash tools/compare-golden.sh
   <dir>` regenerates; a golden diff is a review event, never a fix
   ([testing standards](../common/testing-standards.md#fixtures-and-goldens)).
4. **In-capture asserts.** Cross-artifact rules that a file diff cannot express
   (the phase 7 differential, phase 8 string/byte parity) are asserted inside capture
   mode with a clear failure message — the golden still pins the shared output.
5. **Ownership.** The sample's source-of-record phase owns fix-forward when it rots
   (roadmap verification loop); the harness (comparer, workflow, conventions) is owned
   here.

### `tools/compare-golden.sh` contract

```
usage: compare-golden.sh <sample-dir>
  For every file under <sample-dir>/golden/ : require the same relative path under out/,
  normalize CRLF→LF on both, byte-compare; collect all mismatches (do not stop at first).
  Fail if out/ contains files absent from golden/ (and vice versa).
  UPDATE_GOLDEN=1: copy out/ over golden/ (normalized) instead of comparing; exit 0.
  Exit codes: 0 identical; 1 any mismatch/missing/extra; 2 usage or missing out/.
```

`compare-golden-selftest.sh` fabricates four fixture pairs — content mismatch, extra
file, missing file, CRLF-only difference — and asserts exits 1/1/1/0. It runs as the
first step of every `samples.yml` job (cheap, and keeps the comparer honest forever).

## The authoring template

Verbatim scaffold in `samples/README.md` — written as the standing template that
phases 1–8 *would have used* and that any future sample must use:

```
## Adding a sample

1. Copy the skeleton: folder, csproj (net10.0, ProjectReference to the engine),
   README.md written FIRST — it is the sample's spec and must be followable verbatim
   from `git clone` + `dotnet run` with no undocumented steps.
2. Implement the human mode, then --capture (deterministic artifacts into out/).
3. Capture goldens: UPDATE_GOLDEN=1 bash samples/tools/compare-golden.sh samples/<name>
   — review the goldens as part of the PR.
4. Add the folder name to the samples.yml matrix list and to the index table above
   (name, what it shows, owning phase/spec link).
5. Run the seeded-failure check: break one asserted property on a scratch branch,
   confirm exactly your job goes red, revert, link the run in the PR.
```

## The inventory

One entry per D13 row. Every entry names its source of record, its project shape, its
captured artifacts, and its golden shape. Template sketches are illustrative of the
constructs the sample must exercise; the golden files pin the reviewed reality.

### 1. `samples/ssr-aspnetcore` — classic SSR

- **Source of record:** roadmap current-engine row; upgraded per
  [phase 4's deferred item](../phase-4-ergonomics/README.md#testing-plan) (`@for` sugar,
  `TrimDirectiveLines` on).
- **Shape:** ASP.NET Core minimal API; `TemplateResolver` over `templates/` (layout +
  two pages + shared definitions); the caching story is exercised by requesting the same
  page twice in capture mode and asserting byte-equal responses (no public compile
  counter exists on the resolver — verified; equality across the cached second hit is
  the observable contract this sample pins, and the README narrates that the second
  request is served from the resolver cache).
- **Constructs:** resolver + `@partial` layout/page composition, `@list`, `@if`/`@elif`/
  `@else`, `@for(…)` sugar, `TrimDirectiveLines`.
- **Capture:** GETs `/` and `/articles/1`; writes `index.html`, `article-1.html`.
- **Golden:** the two response bodies.

### 2. `samples/definition-library` — composition imports

- **Source of record:** roadmap current-engine row; phase 4 upgrade applies.
- **Shape:** console app; a `library.heddle` definition library imported via `@<<`
  into a page template; one definition overridden locally; one consumed through
  `<name:name>` narrowing.
- **Capture:** renders the page; writes `page.html`.
- **Golden:** `page.html` — the override and narrowing results are visible in the
  output (the README annotates which line proves which feature).

### 3. `samples/dynamic-models` — dynamic + runtime template strings

- **Source of record:** roadmap current-engine row.
- **Shape:** console app; a template string assembled at runtime, compiled with
  `:: dynamic`, rendered against an anonymous-object graph and again against a
  `Dictionary`-shaped graph.
- **Capture:** writes `dynamic-anon.txt`, `dynamic-dictionary.txt`.
- **Golden:** both outputs.

### 4. `samples/sandboxed-user-templates` — the trust boundary

- **Source of record:** [phase 1's deferred item](../phase-1-native-expressions/README.md#testing-plan).
- **Shape:** console app framing the scenario the phase 1 spec names: "user-supplied
  template + registry trust boundary end-to-end". A set of `user-templates/*.heddle`
  inputs — benign ones using operators/literals/member paths and a host-registered
  whitelisted function, and hostile ones attempting C#-tier escapes and unregistered
  calls — compiled under `ExpressionMode.Native` with a curated `FunctionRegistry`.
- **Capture:** writes `rendered/<name>.txt` for each accepted template and
  `rejected.txt` — one line per rejected template: `name: HEDxxxx @ offset:length
  message` (the positioned-diagnostic pin; never-executes is asserted in capture by a
  side-effect canary function that must not fire).
- **Golden:** the rendered outputs + `rejected.txt`.

### 5. `samples/html-safe-output` — the Html profile and the 2.0 rehearsal

- **Source of record:** [phase 2's deferred item](../phase-2-safe-output/README.md#testing-plan).
- **Shape:** console app rendering one template containing hostile model strings
  (`<script>`, quotes, non-ASCII) **twice**: once with today's default (`Text`) and once
  with `OutputProfile.Html` (+ a `@raw` pass-through island). A third render swaps the
  encoder through the pluggable seam to a tagging stub, proving pluggability.
- **Capture:** writes `profile-text.html`, `profile-html.html`, `custom-encoder.html`.
- **Golden:** all three — the pair *is* the standing 2.0-migration rehearsal (when the
  D2 window flips the default, this sample's README and goldens are the migration
  before/after, updated as a reviewed spec event).

### 6. `samples/custom-extensions` — the public channel

- **Source of record:** [phase 3's deferred item](../phase-3-branching/README.md#testing-plan).
- **Shape:** console app defining a third-party-style extension pair: a publisher
  (writes a value to `Scope.Publish` under its own key — respecting the reserved
  `heddle.` prefix rule) and a reader; plus a **branch-protocol participant** — an
  extension that reads `BranchState` and renders only on the not-taken path, composed
  into an `@if`/`@elif`/`@else` set.
- **Capture:** writes `channel.txt` (publisher/reader round-trip) and `branch.txt`
  (participant behavior across both branch outcomes).
- **Golden:** both outputs.

### 7. `samples/component-props-slots` — the component library

- **Source of record:** [phase 5's deferred item](../phase-5-props-and-slots/README.md#testing-plan)
  ("the card/layout/picker component library … the component-story showcase").
- **Shape:** console app; `components.heddle` defines `<card(style: string = "plain",
  compact: bool = false)> :: Article` with a parameterized `@out(expr)` slot, a
  `<layout>` wrapper, and a `<picker>` composing cards; a page template drives them with
  named arguments (defaults, overrides, required props).
- **Capture:** writes `components-page.html`.
- **Golden:** the page — the README maps each visible region to the prop/slot feature
  it proves (the phase 5 narrative twin, per its spec).

### 8. `samples/codegen-t4-successor` — build-time generation, no runtime dependency

- **Source of record:** [phase 7's deferred item](../phase-7-build-time-compilation/README.md#testing-plan).
- **Shape:** console app whose build uses `Heddle.Generator` to turn
  `templates/*.heddle` into generated C# (`EmitCompilerGeneratedFiles` +
  `CompilerGeneratedFilesOutputPath=generated/` so the emitted source is inspectable);
  `Program.cs` calls the generated typed entry points; the project's *runtime*
  dependency set contains no engine assemblies.
- **Capture:** copies `generated/**/*.cs` into `out/generated/`, writes the program's
  rendered output `codegen-output.txt`, and **structurally asserts** the no-runtime-
  dependency claim: publish to a temp dir and fail if `Heddle.dll`/`Heddle.Language.dll`
  are present.
- **Golden:** the generated sources + `codegen-output.txt` (generator output changes are
  spec-reviewed diffs, exactly like the phase 7 snapshot suite).

### 9. `samples/precompiled-app` — precompilation, mixed mode, discovery

- **Source of record:** phase 7's deferred item (same testing plan; "the precompiled
  sample's output equal to its dynamic twin").
- **Shape:** console app with templates precompiled into the assembly (manifest +
  registration via `PrecompiledRuntime.Bind`), one deliberately *not* precompiled
  (mixed mode — resolved dynamically), `PrecompiledMismatchPolicy` left at `Fallback`.
- **Capture:** writes `precompiled-output.html` (rendered via the precompiled path),
  `discovery.txt` (ordered `PrecompiledTemplates.Entries` listing: key, fingerprint
  fields), and runs the **differential** in-process: render the same template + data
  through a dynamically compiled twin and fail capture on any byte difference before
  writing `differential.txt` (`identical` + the byte count).
- **Golden:** all three files.

### 10. `samples/streaming-ssr` — bytes to the response

- **Source of record:** [phase 8's deferred item](../phase-8-streaming-async/README.md#testing-plan)
  ("minimal-API endpoint rendering into `Response.BodyWriter`, then `FlushAsync`" —
  the spec's WI8 in-test host, graduated to a runnable sample).
- **Shape:** ASP.NET Core minimal API; the endpoint renders with
  `template.Generate(model, Response.BodyWriter)` then `await Response.BodyWriter.FlushAsync()`;
  a `/text-writer` endpoint shows the `TextWriter` sink for contrast.
- **Capture:** self-requests both endpoints, writes `streamed.html` and
  `textwriter.html`; **parity assert** in capture: the `IBufferWriter<byte>` bytes
  decoded as UTF-8 must equal the `string Generate` output for the same template + data
  (the phase 8 encoding-parity rule, exercised at integration level).
- **Golden:** both bodies.

### Walkthrough row — `editors/vscode`

Index-table row only (entry doc D13): links the
`docs/editor-support.md` page (created by phase 6's D22) and the `editors/vscode` extension
source; the tour (install the pinned tool, open a `.heddle` file, see typed completion/
hover/diagnostics) is documentation verified by phase 6's standing extension smoke test
— no `samples/` project, no golden, no matrix row.

## The CI workflow

`.github/workflows/samples.yml`, complete (entry doc D12 — a dedicated workflow;
[dotnet.yml](../../../.github/workflows/dotnet.yml) untouched):

```yaml
# Integration demo gallery: every sample is a CI job with golden assertions.
# A red job here is an integration regression in the owning phase's feature.
name: Heddle Samples

on:
  push:
    branches: [ "main" ]
    paths:
      - 'samples/**'
      - 'src/**'
      - '.github/workflows/samples.yml'
  pull_request:
    branches: [ "main" ]
    paths:                          # identical list, repeated verbatim — path filters
      - 'samples/**'                # are per-trigger; keep both lists in sync
      - 'src/**'
      - '.github/workflows/samples.yml'

permissions:
  contents: read

jobs:
  sample:
    strategy:
      fail-fast: false          # one broken sample must not mask the others
      matrix:
        sample:
          - ssr-aspnetcore
          - definition-library
          - dynamic-models
          - sandboxed-user-templates
          - html-safe-output
          - custom-extensions
          - component-props-slots
          - codegen-t4-successor
          - precompiled-app
          - streaming-ssr
    runs-on: ubuntu-latest
    steps:
    - uses: actions/checkout@v7
    - name: Setup .NET
      uses: actions/setup-dotnet@v5
      with:
        dotnet-version: 10.0.x
    - name: Comparer self-test
      run: bash samples/tools/compare-golden-selftest.sh
    - name: Run sample (capture mode)
      run: dotnet run --project samples/${{ matrix.sample }} -c Release -- --capture out
    - name: Compare against goldens
      run: bash samples/tools/compare-golden.sh samples/${{ matrix.sample }}
```

Notes pinned against the verified workflow landscape:

- **Why not `dotnet.yml`:** it is entangled with NuGet beta/release publishing and runs
  a two-OS matrix; samples are ubuntu-only (no OS-specific sample exists — entry doc,
  Deferred items) and must never gate or slow package publishing.
- **Triggering on `src/**`** is the point: engine changes re-prove every integration —
  the classes of regression (packaging, resolver file behavior, options plumbing) unit
  tests structurally miss.
- **Isolation scenario** (roadmap validation): a seeded break in one sample reds exactly
  one matrix job — verified by the WI2 seeded-failure procedure and re-run per harness
  change.
- **Growth rule:** a new sample = one folder + one matrix list entry + one index row
  (the authoring template) — no new infrastructure, which is the roadmap's success
  criterion 5 answered structurally.

## Traceability

The roadmap-phase → re-verified-surface table lives in the entry document
([D15](README.md#d15--phase-exit-is-the-roadmaps-combined-end-to-end-run)); this
inventory is its per-sample expansion. The combined phase-exit run = all ten matrix
jobs green + smoke S1–S5 green + the engine regression gate green, recorded in the PR
as the roadmap's closing artifact.
