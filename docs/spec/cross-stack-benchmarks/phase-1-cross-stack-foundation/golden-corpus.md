# Golden oracle corpus — format, export, verification

Supplementary document of the [Phase 1 — cross-stack-foundation spec](README.md). It specifies
the on-disk corpus phases 2–6 consume as their parity reference: location, per-entry format and
metadata, the export tool (net-new — no golden/corpus infrastructure exists in
`src/Heddle.Performance` today; verified, spike A §7), the verification commands, and the
regeneration policy. The gate that consumes the corpus is defined in
[parity-contract-v2.md](parity-contract-v2.md); the workloads themselves in
[workloads.md](workloads.md).

## Location and layout

```
src/Heddle.Performance/GoldenCorpus/
  README.md                      ← corpus contract summary + the composed-page fidelity note (verbatim)
  manifest.json                  ← one entry per workload: length, hash, generating commit
  composed-page.golden.html
  trivial-substitution.golden.html
  large-loop.golden.html
  mixed-page.golden.html
  conditional-heavy.golden.html
  fragment-heavy.golden.html
  fortunes-encoded.golden.html
  encoded-loop.golden.html
  composed-page.verify.json      ← idiomatic-verifier definition (one per workload)
  … (.verify.json for all eight)
```

Rationale for the location: the corpus is produced by and re-verified against the code that
lives in `src/Heddle.Performance`, so it sits beside its generator; phases 2–6 read the files by
repo-relative path (`src/Heddle.Performance/GoldenCorpus/<id>.golden.html`). No new top-level
repo directory is introduced (most reversible choice; the trigger to revisit is a phase 2–6 spec
demonstrating that its harness cannot conveniently reach into `src/` — then the corpus *moves*
in one ordinary versioned change, exactly as Q1.5 allows).

## On-disk format

Each `<workload>.golden.html` contains **the normalized oracle**: Heddle's rendered output for
the workload after the contract's **stored-form** normalization steps
(N1–N4 plus N5 for encoded entries — N5 is an identity transform on Heddle output, whose spellings
are already canonical), encoded as **UTF-8 without BOM, with no appended trailing newline** (N4's
trim means the content never ends in whitespace; the file's bytes are exactly the normalized
string's UTF-8 bytes). The stored form is **exactly v1's `TwinContent.Normalize` output** (N2/N3/N4)
plus N1's encoding rule and N5, so it is human-readable and byte-identical to the existing goldens.
N3b (the 2026-07-20 whitespace-run-collapse step) is **not** applied at export and is **not** baked
into the stored oracle — it is a comparison-time projection that removes every whitespace run from
**both** the loaded oracle and the candidate at gate time
([contract — N3b](parity-contract-v2.md#normalization-pipeline)). Storing the readable N1–N5 form
(not the whitespace-stripped projection) keeps the goldens diffable and their committed bytes
stable.

Storing the normalized form (rather than raw render bytes) is deliberate: the gate everywhere is
`strip(normalize(candidate)) == strip(oracle)` (N3b applied symmetrically), so consumers apply the
N1–N5 pipeline **once, to their own output**, then remove whitespace from both their output and the
loaded oracle and compare — no consumer re-implements Heddle-side normalization. The raw render
remains reproducible at any time from the recorded generating commit.

Git handling: the spec adds a `.gitattributes` rule
`src/Heddle.Performance/GoldenCorpus/*.golden.html -text` (and `*.verify.json` / `manifest.json`
as `text eol=lf`) in the same change that introduces the directory, so no checkout smudging can
alter the golden bytes (per the
[testing-standards line-ending rule](../../common/testing-standards.md#fixtures-and-goldens);
`-text` rather than `eol=lf` because the goldens intentionally have no trailing newline and must
round-trip byte-exact).

## Manifest

`manifest.json` — UTF-8, LF, one object:

```json
{
  "$schema": "manifest schema v1 (informal; fields below are normative)",
  "generator": "Heddle.Performance export-corpus",
  "entries": [
    {
      "workload": "fortunes-encoded",
      "suite": "encoded",
      "file": "fortunes-encoded.golden.html",
      "byteLength": 1234,
      "hash": "sha256:9f2c…64-hex-lowercase…",
      "generatingCommit": "0123456789abcdef0123456789abcdef01234567",
      "generatedUtc": "2026-07-21T09:00:00Z"
    }
  ]
}
```

Field semantics (all required):

| Field | Meaning |
|---|---|
| `workload` | The stable workload id ([workloads.md](workloads.md#the-set-at-a-glance)) |
| `suite` | `raw` or `encoded` — tells a consumer whether N5 applies to its own output |
| `file` | Corpus file name, relative to `GoldenCorpus/` |
| `byteLength` | Exact size in bytes of the corpus file |
| `hash` | `sha256:` + lowercase-hex SHA-256 of the corpus file bytes |
| `generatingCommit` | Full 40-hex SHA of the repo commit the export ran at (`git rev-parse HEAD`) |
| `generatedUtc` | Export timestamp, ISO-8601 UTC — informational only, never part of any gate |

Entries are ordered by workload number (1–8). The manifest is regenerated whole on every export.

## Export tool

Net-new code in `src/Heddle.Performance` (spike A §7 confirmed nothing exists to reuse there;
the `Heddle.Tests` golden-file pattern is a precedent, not a shared mechanism).

- **Entry point:** `dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- export-corpus`
  (a new verb in `Program.cs`, beside the existing `parity` verb). Optional flag `--allow-dirty`.
- **Implementation:** new `Runners/GoldenCorpus.cs` (internal static class) with a single
  registry of the eight workloads:
  `(string Id, string Suite, Func<string> RenderHeddle)` — the render funcs reuse the existing
  and new `*HeddleTest` runners. Export, per entry: render → normalize to the stored form
  (`TwinContent.Normalize` = N2/N3/N4; encoded entries need no extra step, N5 is identity on Heddle
  output) → UTF-8-encode (no BOM) → write `<id>.golden.html` → compute SHA-256 → append manifest
  entry. **N3b is deliberately not applied at export**: it is the gate's comparison-time whitespace
  strip (applied to both sides at gate time), not a stored-form step, so the on-disk oracle stays
  readable and byte-stable while the oracle and every candidate still pass through the identical
  comparison ([contract — N3b](parity-contract-v2.md#normalization-pipeline)). Also writes every `<id>.verify.json` from the check definitions in
  `Runners/IdiomaticChecks.cs` (single source of truth in C#; the JSON is the exported,
  cross-language-consumable form).
- **Commit stamping:** the tool runs `git rev-parse HEAD` and `git status --porcelain`
  (`System.Diagnostics.Process`, working directory = repo root). A non-empty status output means
  a dirty tree: the tool **refuses to export** and exits 1 with
  `export-corpus: working tree is dirty; commit first or pass --allow-dirty.` With
  `--allow-dirty` it exports and records `"<sha>+dirty"` as `generatingCommit` (useful mid-work,
  never committed — a committed `+dirty` manifest fails review by inspection and `verify-corpus`
  prints a warning line for it).
- **Determinism requirement:** exporting twice at the same commit produces byte-identical
  corpus files and identical manifests except `generatedUtc` (plan success criterion
  "regenerating any golden output at its recorded commit reproduces the committed bytes
  exactly" — checkable because every model is a deterministic static construction, and the
  render path has no time/random/locale dependence; all numeric formatting in models is
  invariant-culture `int` formatting).

## Verification

`dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- verify-corpus` — a second
new verb, exit 0 iff all checks pass:

1. **Freshness** — for each of the eight entries: render Heddle live, normalize to the stored form
   (N1–N5, `TwinContent.Normalize` + N5; N3b is not a stored-form step), UTF-8-encode, compare
   byte-for-byte with the committed corpus file; also recompute SHA-256 and compare with
   the manifest. Prints `[PASS]`/`[FAIL] <workload> …` lines in the `ParityCheck.Report` style
   (first-diff index + excerpt on failure).
2. **Verifier calibration** — for each workload: run the idiomatic verifier
   (`IdiomaticChecks`) against the corpus entry (must accept), then against its applicable
   synthesized corruptions — two per raw workload, three per encoded workload — each of which
   must be rejected with the correct failing check kind:
   - *removed row* — delete the first occurrence of the workload's row-level marker segment
     (per-workload substring pinned in `IdiomaticChecks`, e.g. the full first `<tr>…</tr>` of
     `large-loop`). The two rowless raw anchors pin a whole segment instead: **composed-page**
     deletes its `SectionSocial` marker fragment (trips the ordered-`markers` check for the
     missing fragment); **trivial-substitution** deletes its `HB-2001` sku value (trips the
     `values` check, `HB-2001` → 1 dropping to 0);
   - *reordered section* — swap the first two ordered row/section segments. For the two rowless
     raw anchors the pinned pair is: **composed-page** — swap the leading `SectionMeta` and
     `SectionSocial` marker fragments; **trivial-substitution** — swap the `class="sku"` and
     `class="rating"` marker segments. Either swap trips the markers-in-order check;
   - *unescaped payload* (encoded workloads only) — replace the first `&lt;script&gt;alert(`
     with `<script>alert(`.
3. Prints a warning (not a failure) if any manifest `generatingCommit` carries `+dirty`.

The **benchmark-time gate** stays where it is today: `ParityCheck.Assert*` in every render
benchmark's `[GlobalSetup]` (twin-vs-live-Heddle), extended to the five new workloads — its
comparison applies **N3b** (the whitespace strip on both the normalized twin and the normalized
Heddle output), so it compares only non-whitespace bytes, exactly like every other controlled
gate. The corpus freshness check is additionally invoked from each `[GlobalSetup]` via
`GoldenCorpus.AssertFresh(workloadId)` so a benchmark can never time against a stale committed
oracle; freshness stays **byte-exact on the stored N1–N5 form** (Heddle's own output vs its own
committed oracle — N3b is not applied there). (Cheap: one extra render + compare per suite,
setup-time only.)

## Idiomatic verifier definitions

One `<workload>.verify.json` per workload, exported from `IdiomaticChecks.cs`. Schema (all
fields required; empty arrays allowed):

```json
{
  "workload": "fortunes-encoded",
  "suite": "encoded",
  "values":   [ { "text": "A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1", "count": 1 } ],
  "markers":  [ "<!DOCTYPE html>", "<table>", "<tr><th>id</th><th>message</th></tr>", "</table>" ],
  "forbidden": [ "<script>alert(" ],
  "required":  [ { "text": "&lt;script&gt;alert(", "minCount": 1 }, { "text": ");&lt;/script&gt;", "minCount": 1 } ]
}
```

Semantics are defined in the
[contract's idiomatic-track gate](parity-contract-v2.md#idiomatic-track-gate). The per-workload
check contents (normative; `IdiomaticChecks.cs` transcribes them):

| Workload | `values` (text → exact count) | `markers` (in order) | `forbidden` / `required` |
|---|---|---|---|
| composed-page | the whole `TwinContent.SectionMeta` literal (normalized) → 1; the first 60 characters of the `AreaComponent.Areas["Footer Links"]` entry (normalized) → 1 *(needle strings are computed in `IdiomaticChecks.cs` from those members — `SectionMeta` is a short const, the area fragments are multi-KB literals; the exported `.verify.json` contains the resolved literal needles)* | one distinctive ≥ 20-char substring from each of, in order: `SectionMeta`, `SectionSocial`, the `CompAssetsStyles` styles fragment, each non-empty `AreaOrder` area fragment (`"Alert Top Section Below Nav"` is empty and skipped), and the `CompBodyEndScripts` fragment — substrings chosen in `IdiomaticChecks.cs`, resolved into the exported JSON | — |
| trivial-substitution | `Heddle Handbook` → 1; `HB-2001` → 1; `A concise field guide to the engine.` → 1; `4.8` → 1 | `<article>`, `<h1>`, `class="sku"`, `class="rating"`, `</article>` | — |
| large-loop | `<tr><td>row-0</td><td>0</td></tr>` → 1; `<tr><td>row-4999</td><td>4999</td></tr>` → 1; `<tr><td>row-` → 5000 | `row-0`, `row-2500`, `row-4999` | — |
| mixed-page | `Mercantile - Catalog` → 1; `Autumn hardware sale` → 1; `Product 01` → 1; `Product 36` → 1; `MX-1036` → 1; `<article class="card">` → 36; `<p class="sale">On sale</p>` → 12; `Free shipping on orders over 60.` → 1 | `<!DOCTYPE html>`, `<header>`, `class="hero"`, `class="grid"`, `<footer>`, `</html>` | — |
| conditional-heavy | `unit-000` → 1; `unit-199` → 1; `<li>` → 200; `<span class="t0">bronze</span>` → 50; `<span class="t3">platinum</span>` → 50; `<small>` → 100; `<b>active</b>` → 160 | `<ul class="matrix">`, `unit-000`, `unit-100`, `unit-199`, `</ul>` | — |
| fragment-heavy | `<section class="tile">` → 48; `tile-00` → 1; `tile-47` → 1; `<span class="badge">new</span>` → 12 | `<div class="panel">`, `tile-00`, `tile-24`, `tile-47`, `</div>` | — |
| fortunes-encoded | each of the 12 escaped messages → 1 (escaped form of each pinned string; ten are identical to their raw form) **except** rows 3 and 11, whose text-context quote entities are weakened to quote-agnostic substrings (see the amendment below): row 3 → `A computer scientist is someone who fixes things that aren` → 1; row 11 → `&lt;script&gt;alert(` → 1 and `);&lt;/script&gt;` → 1; `<tr><td>` → 12 (the header row uses `<th>` and is not counted) | `<!DOCTYPE html>`, `<table>`, `<tr><th>id</th><th>message</th></tr>`, `フレームワークのベンチマーク`, `</table>` | forbidden: `<script>alert(`; required: `&lt;script&gt;alert(` → minCount 1 and `);&lt;/script&gt;` → minCount 1 |
| encoded-loop | `tag-0&amp;&#39;0&#39;` → 1; `item &lt;4999&gt; &amp;` → 1 (text-context quote entity weakened — see the amendment below); `<tr><td data-tag="` → 5000; `こんにちは` → 5000 | first row's `data-tag` value, row 2500's `item &lt;2500&gt;`, last row's comment tail `こんにちは 4999` | forbidden: `<script>alert(`, `<angle>` *(raw form of the comment's angle text — it must always be escaped)*; required: `&lt;angle&gt;` → minCount 5000 |

> **Amendment (2026-07-20, evidence from Phase 3 — jvm).** The three fortunes-encoded needles
> and the one encoded-loop `values` needle above that embedded HTML **text-context** quote
> entities (`&quot;`, `&#39;`) are weakened to the quote-agnostic substrings shown. Reason: an
> OWASP-conformant context-aware escaper (e.g. stock JTE's `htmlContent` — verified at source,
> `gg.jte.html.escape.Escape`, casid/jte@main, 2026-07-20) correctly escapes only `& < >` in HTML
> text context and leaves `"`/`'` raw, so the original needles structurally rejected a correct
> idiomatic-default output — contradicting the idiomatic-track premise that idiomatic
> implementations use each engine's official default path. Each replacement is a substring of the
> needle it replaces **that remains unique in each workload's output**, so exact counts are
> preserved for every five-character escaper (the Heddle oracle and all Phase 1 twins): no
> existing pass/fail status changes, and the verifier-calibration corruptions (`forbidden`,
> removed-row, reordered-section — all untouched) still trip. The **attribute-context** needle
> `tag-0&amp;&#39;0&#39;` (encoded-loop) is deliberately **not** weakened — attribute escapers
> do escape quotes. The security floor (`forbidden`: `<script>alert(`) is untouched.

Counting note: `values` counts run against the **whitespace-stripped projection** of the normalized
output (N1–N5, then N3b removes every whitespace run) with the **same strip applied to each needle
string** before matching, so neither inter-tag nor inter-word spacing can affect a match or a count:
a multi-tag needle like `<tr><td>row-0</td><td>0</td></tr>` and a spaced needle like `A bad random
number generator: 1, 1, …` are both matched with all whitespace removed on both sides, and each
needle remains a distinctive non-whitespace substring unique to its intended site (so removing
whitespace neither destroys an occurrence nor forges a new one). N3b does not erase any
verifier-calibration corruption: *removed row* deletes a structural marker/value, *reordered
section* swaps ordered markers, and *unescaped payload* substitutes a non-whitespace escaped→raw
form — each is a non-whitespace change that survives the whitespace strip and still trips its check.

## Regeneration policy (Q1.5 — resolved: no ceremony)

The corpus lives under ordinary git versioning, which suffices. Regenerating or expanding it is
an **ordinary versioned change**: run `export-corpus` at a clean commit, review the diff
(`manifest.json` shows old/new lengths, hashes, and commits at a glance), commit. There is **no
corpus version number, no bump ceremony, and no mandatory re-run of already-shipped ecosystems'
gates** — shipped phases pick up a changed corpus the same way they pick up any repo change. The
drift risk this accepts is a recorded user ruling
([open-questions Q1.5](../../../plan/open-questions.md#q15--what-is-the-golden-corpus-regeneration-policy-once-phases-26-have-consumed-it--status-resolved));
auditability comes from `generatingCommit` + `hash` per entry, which make any change fully
traceable in history.
