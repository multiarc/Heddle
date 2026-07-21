# Golden oracle corpus

The committed parity reference for the cross-stack benchmark program (phases 2–6): one
`<workload>.golden.html` per workload containing **Heddle's rendered output in the stored
normalized form**, plus `manifest.json` (byte length, SHA-256, generating commit per entry) and
one `<workload>.verify.json` per workload (the idiomatic-verifier definition exported from
`Runners/IdiomaticChecks.cs`).

## Contract summary

- **Stored form** — each golden holds the oracle after the contract's stored-form normalization
  (N1–N4, plus N5 for encoded entries — an identity transform on Heddle output): exactly
  `TwinContent.Normalize` (unify line endings to `\n`, collapse whitespace runs between tags to
  nothing, trim), encoded as **UTF-8 without BOM and with no trailing newline**. The contract's
  N3b whitespace-run strip is a **comparison-time projection** applied symmetrically to both
  sides at gate time; it is never baked into these files, so they stay readable and diffable.
- **The gate** — everywhere: `strip(normalize(candidate)) == strip(oracle)` (N3b on both sides).
  Intra-.NET the corpus is additionally freshness-checked byte-exact against a live Heddle
  render (`GoldenCorpus.AssertFresh`) in every render benchmark's `[GlobalSetup]`, and by
  `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- verify-corpus`
  (freshness + verifier calibration).
- **Regeneration** — an ordinary versioned change (no version numbers, no ceremony): run
  `… -- export-corpus` at a **clean** commit, review the diff, commit. The tool refuses a dirty
  tree without `--allow-dirty`, which stamps `generatingCommit` with `+dirty` (never committed;
  `verify-corpus` warns on it).
- **Git handling** — `.gitattributes` pins `*.golden.html -text` (byte-exact round-trip; the
  files intentionally end without a newline) and the JSON sidecars as `text eol=lf`.

Normative definitions: [golden-corpus.md](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/golden-corpus.md)
(on-disk format, manifest schema, export tool, verification, verifier definitions),
[parity-contract-v2.md](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md)
(normalization pipeline N1–N5 + N3b, gates, exclusion policy), and
[workloads.md](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/workloads.md)
(the eight workload shapes). Contract v1 for the intra-.NET raw suites:
[Runners/README.md](../Runners/README.md).

## Fidelity note — what the composed page actually contains

(Verbatim from [Runners/README.md](../Runners/README.md); the `composed-page` corpus entry is
exported exactly as Heddle renders it today — spec D5.)

The rendered page is fully static: the model is an empty `object` and every Heddle extension returns
a fixed string. Under the current engine, rendering `home.heddle` (which extends `layout.heddle`)
emits the reusable-section defaults and the component/area calls **in order**, but not the layout's
literal HTML skeleton, the account-link block, or the overridden `<body:body>` slider (the
`@body()` call resolves to the empty default). The twins therefore reproduce exactly that ordered
fragment sequence:

```
section.meta → section.social
→ assets_styles → custom_styles → head_scripts → body_scripts
→ [loop] Alert-Above, Secondary-Wholesale, Secondary-Retail, Wholesale-Mega,
         Retail-Mega, Alert-Below (empty), Footer-Links
→ assets_scripts → page_scripts (empty) → endpage_scripts (empty) → body_end_scripts
```

This keeps the parity comparison honest (all five engines render byte-identical output). Note that
`RazorTest` is a pre-existing twin that renders the full HTML page from `Views/home.cshtml` and is
**not** under the parity assertion; bringing the Heddle corpus and the Razor view back into a single
composed page is a separate template-authoring concern outside D1's scope.

### Root cause (investigated for D2 — no engine change)

The omission is **specific to the `@<<` layout-extend path when the extending document is the
compile entry point**, not to the templates or the harness wiring. Confirmed empirically: compiling
`layout.heddle` *directly* as the entry point (`TemplateOptions("layout")`) renders the full ~60 KB
composed page — `<!DOCTYPE html>`, the header/account-link chrome, `@body()`, and the footer all
present. Compiling `home.heddle` (which does `@<<{{layout.heddle}}`) as the entry point instead
emits only the section defaults + component/area calls (output begins at `<title>Title</title>`,
not `<!DOCTYPE html>`), and `@body()` resolves to its empty default so the `<body:body>` slider is
absent. Making `home` render the composed page therefore requires an engine change to the `@<<`
composition semantics, which is out of scope for the D benchmark workstream. Swapping the entry to
`layout` would render more chrome but drops the `<body:body>` override and would force every twin to
embed the layout's literal HTML verbatim (parity-drift risk) — so the workload is left as the
parity-asserted fragment sequence and documented precisely here and in the published numbers. The
non-negotiable property (all five engines do identical, parity-checked work) holds regardless.
