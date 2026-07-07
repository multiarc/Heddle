# Heddle integration gallery

Ten small, complete, runnable projects — one per supported way to integrate Heddle. Each is a real app you can
`git clone` + `dotnet run`, and each is also an **end-to-end CI test**: every sample captures deterministic output
that CI compares against a committed golden. A broken sample *is* a failed integration test (phase 9).

Run one interactively (the human mode the sample's README narrates):

```bash
dotnet run --project samples/dynamic-models
```

Run it in capture mode (what CI does — writes deterministic artifacts into `out/`, then compares against `golden/`):

```bash
dotnet run --project samples/dynamic-models -c Release -- --capture out
bash samples/tools/compare-golden.sh samples/dynamic-models
```

## The samples

| # | Sample | Source of record | Demonstrates |
| --- | --- | --- | --- |
| 1 | [`ssr-aspnetcore`](ssr-aspnetcore) | Roadmap (current engine); phase 4 upgrade | Minimal-API SSR, resolver + layout/page composition, caching, `@for` + `TrimDirectiveLines` |
| 2 | [`definition-library`](definition-library) | Roadmap (current engine); phase 4 upgrade | `@<<` composition import, definition overrides, `<name:name>` narrowing |
| 3 | [`dynamic-models`](dynamic-models) | Roadmap (current engine) | `:: dynamic`, runtime-loaded template strings |
| 4 | [`sandboxed-user-templates`](sandboxed-user-templates) | [Phase 1](../docs/spec/phase-1-native-expressions/README.md) | `ExpressionMode.Native`, `FunctionRegistry` as the trust boundary, rejected constructs with positioned `HED1xxx` |
| 5 | [`html-safe-output`](html-safe-output) | [Phase 2](../docs/spec/phase-2-safe-output/README.md) | `OutputProfile.Html`, `@raw`, encoder pluggability; runs under both defaults (the 2.0 rehearsal) |
| 6 | [`custom-extensions`](custom-extensions) | [Phase 3](../docs/spec/phase-3-branching/README.md) | The public `Scope.Publish`/`TryRead` channel + a branch-protocol participant |
| 7 | [`component-props-slots`](component-props-slots) | [Phase 5](../docs/spec/phase-5-props-and-slots/README.md) | The card/layout/picker component library: typed props with defaults, parameterized slots |
| 8 | [`codegen-t4-successor`](codegen-t4-successor) | [Phase 7](../docs/spec/phase-7-build-time-compilation/README.md) | Build-time text/code generation, no runtime Heddle dependency (asserted structurally) |
| 9 | [`precompiled-app`](precompiled-app) | Phase 7 | Pre-compilation + mixed mode + discovery enumeration; the differential rule (precompiled == dynamic twin) |
| 10 | [`streaming-ssr`](streaming-ssr) | [Phase 8](../docs/spec/phase-8-streaming-async/README.md) | `Generate(data, IBufferWriter<byte>)` into `Response.BodyWriter` + `FlushAsync`; string-parity asserted in capture |
| — | `editors/vscode` walkthrough | [Phase 6](../docs/spec/phase-6-tooling-lsp/README.md) | LSP setup + typed completion tour — see [docs/editor-support.md](../docs/editor-support.md) and [editors/vscode](../editors/vscode). Documentation + the phase 6 extension smoke test, not a runnable golden project. |

## Adding a sample

1. Copy the skeleton: folder, csproj (net10.0, ProjectReference to the engine),
   README.md written FIRST — it is the sample's spec and must be followable verbatim
   from `git clone` + `dotnet run` with no undocumented steps.
2. Implement the human mode, then `--capture` (deterministic artifacts into `out/`).
3. Capture goldens: `UPDATE_GOLDEN=1 bash samples/tools/compare-golden.sh samples/<name>`
   — review the goldens as part of the PR.
4. Add the folder name to the `samples.yml` matrix list and to the index table above
   (name, what it shows, owning phase/spec link).
5. Run the seeded-failure check: break one asserted property on a scratch branch,
   confirm exactly your job goes red, revert, link the run in the PR.

## Conventions (D11)

- **Two modes.** `dotnet run` is the human mode; `dotnet run -- --capture out` writes every deterministic artifact
  into `out/` and exits 0 (non-zero on any internal assertion failure). Web samples bind port 0, self-request with
  `HttpClient`, write the bodies, and shut down.
- **Determinism.** Fixed model data (no clocks, GUIDs, randomness), invariant culture, ordered enumerations.
- **Goldens.** One file in `golden/` per captured artifact; byte-identical after CRLF→LF normalization; the file
  sets must match exactly. A golden diff is a review event, never a silent fix.
- **In-capture asserts.** Cross-artifact rules a file diff cannot express (the phase 7 differential, phase 8
  string/byte parity) are asserted inside capture mode with a clear failure message.
- **Ownership.** The sample's source-of-record phase owns fix-forward when it rots; the harness (comparer, workflow,
  conventions) is owned by phase 9.

The `out/` directories are gitignored; the `golden/` directories are committed.
