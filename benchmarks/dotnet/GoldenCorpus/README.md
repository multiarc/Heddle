# Golden oracle corpus

The committed parity reference for the cross-stack benchmark program (phases 2–6): one
`<workload>.golden.html` per workload containing **Heddle's rendered output in the stored
normalized form**, plus `manifest.json` (byte length, SHA-256, generating commit per entry),
one `<workload>.verify.json` per workload (the idiomatic-verifier definition, exported from
`../src/Corpus/VerifierDefinitions.cs`), and `fixtures/` (exported model data the non-.NET
ports load — today `fixtures/composed-page/nav.json`, the structured navigation — recorded in
the manifest's `fixtures` section with the same hash discipline as the goldens).

## Contract summary

- **Stored form** — each golden holds the oracle after the contract's stored-form normalization
  (N1–N4, plus N5 for encoded entries — an identity transform on Heddle output): unify line
  endings to `\n`, collapse whitespace runs between tags to nothing, trim the edges; encoded as
  **UTF-8 without BOM and with no trailing newline**. The contract's
  N3b whitespace-run strip is a **comparison-time projection** applied symmetrically to both
  sides at gate time; it is never baked into these files, so they stay readable and diffable.
- **The gate** — everywhere: `strip(normalize(candidate)) == strip(oracle)` (N3b on both sides).
  Intra-.NET the corpus is additionally freshness-checked byte-exact against a live Heddle
  render by
  `dotnet run -c Release --project benchmarks/dotnet -- verify-corpus`
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
(the eight workload shapes).

## Full-page composition — what the composed page contains (ledger E20)

Since the E20 redesign the `composed-page` entry is a genuine **full HTML page**, rendered
through Heddle's documented layout-as-definition idiom
(docs/language-reference.md §"Composition without coupling" — **no engine change**):

- `layout.heddle` is **definition-only** — the whole ~150-line chrome (doctype, IE
  conditionals, head, header, footer) lives *inside* a `<layout>{{ … }} :: ComposedModel`
  definition, so importing the file renders nothing;
- a bare `@out()` marks the **live body slot**; `home.heddle` does `@<<{{layout.heddle}}` and
  calls `@layout(){{ …slider markup… }}`, whose body splices at the slot (both tracks carry the
  same slider body — the verifier's removed-segment calibration pin is that slider, so an empty
  body FAILS);
- the model is **pure structured data** (ledger E22): `ComposedModel { Nav }` and nothing
  else. The inert chrome fragments — the alert banner, both secondary menus, the pinned-empty
  alert-below slot, and the fixed asset/script snippets — are named definitions in the
  definition-only `chrome-fragments.heddle` library (imported by the layout via `@<<`) that
  the chrome calls at its composition points; the two mega menus and the footer links are
  **structured data** in `NavData` rendered by `@list(Nav.Menus){{@mega_menu()}}` /
  `@list(Nav.FooterColumns){{@nav_column()}}` over nested `mega_menu` → `nav_column` →
  `nav_section` → `nav_link` definitions. No C# fixture serves text to any engine — the old
  `AreaData`/`TwinContent` dictionaries and the Heddle extensions are deleted;
- the structured nav is exported to `fixtures/composed-page/nav.json` (snake_case, LF,
  manifest-hashed) — the single source of truth the five non-.NET ports load their nav models
  from;
- every nav text value is sanitized to workloads.md rule 4 (ASCII, none of `& < > " '`), so
  default-escaping engines cannot double-escape; `NavData`'s static constructor asserts the rule.

Every twin composes with its **own native layout mechanism** (Razor `Layout`/`@RenderBody`,
Liquid capture-then-include, Handlebars partial blocks, …) — the normative per-engine table is
in
[workloads.md — workload 1](../../../docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/workloads.md).
