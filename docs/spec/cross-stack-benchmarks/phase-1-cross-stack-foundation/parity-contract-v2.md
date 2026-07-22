# Parity contract v2 — full text

Supplementary document of the [Phase 1 — cross-stack-foundation spec](README.md). This is the
**normative cross-language parity contract** that phases 2–6 implement and phase 7 aggregates
under. It supersedes nothing: contract v1
([src/Heddle.Performance/Runners/README.md](../../../../src/Heddle.Performance/Runners/README.md),
"Parity rule (D1-R3)") remains the accurate description of the existing intra-.NET raw suites; v2
is a superset that extends v1 to cross-language use and to the encoded suite. Workload shapes are
in [workloads.md](workloads.md); the oracle artifacts are in the
[golden corpus](golden-corpus.md).

## Vocabulary

- **Oracle** — the committed golden output of a workload: the byte content of its
  [corpus entry](golden-corpus.md#on-disk-format), which is Heddle's rendered output after the
  normalization pipeline below, encoded as UTF-8 without BOM.
- **Candidate** — the output of any engine implementation (a .NET twin or a phase 2–6 port)
  for the same workload.
- **Controlled track** — semantically-equivalent, equivalently-authored templates; gate =
  byte-identical to the oracle after normalization.
- **Idiomatic track** — per-engine idiomatic templates; gate = the machine-checkable
  [functional-equivalence verifier](#idiomatic-track-gate).

## Normalization pipeline

The **closed list** of transformations applied to a candidate's output (and, at export time,
to Heddle's output — the oracle is stored already-normalized) before the byte comparison.
Nothing outside this list is ever applied; a gate implementation that adds a step is
non-conformant.

Definitions used below: *whitespace* is exactly the six-character set
`{ U+0009 TAB, U+000A LF, U+000B VT, U+000C FF, U+000D CR, U+0020 SPACE }`. (V1's .NET
implementation uses the regex class `\s`, which is wider in .NET; the narrower closed set is
output-equivalent for every workload in the set because no template emits non-ASCII whitespace
between tags, and it is the portable definition ecosystems implement identically **with an
explicit six-character class, not a language `\s`** — `\s` membership varies by engine: .NET's
is Unicode-wide, and Go's RE2 `\s` is the ASCII subset `[\t\n\f\r ]`, which **omits VT (U+000B)**
that this set includes. No workload emits VT, so the two remain output-equivalent here.)

| # | Step | Rule | Applies to |
|---|---|---|---|
| N1 | Decode | The candidate's bytes are decoded as UTF-8. Invalid UTF-8 is a gate failure. A leading U+FEFF (BOM) is **not** stripped — it survives to the comparison and fails it (an engine that emits a BOM is emitting different bytes). | all suites |
| N2 | Line endings | Replace every `\r\n` with `\n`, then every remaining `\r` with `\n`. | all suites |
| N3 | Inter-tag whitespace | Collapse every run of one-or-more whitespace characters that sits between `>` and `<` to nothing (v1: regex `>\s+<` → `><`, applied non-overlappingly, left to right, repeatedly until no match — the .NET implementation's single `Regex.Replace` pass is equivalent because replacements cannot create new matches: the replacement `><` contains no whitespace). | all suites |
| N3b | Whitespace-run collapse (comparison step) | The controlled gate compares the two outputs' **non-whitespace bytes only**. Collapse **every** run of one-or-more whitespace characters — to **nothing** (remove it entirely, *not* to a single space), anywhere in the text (not only between tags) — from **both** the normalized candidate and the normalized oracle, then compare. This is the final comparison step; it is applied symmetrically at gate time and is **not** baked into the stored oracle. | all suites |
| N4 | Trim | Remove leading and trailing whitespace from the whole text. | all suites |
| N5 | Entity canonicalization | Apply the [canonicalization table](#entity-canonicalization-n5) below. | **encoded suite only** |

**The comparison:** UTF-8-encode the normalized candidate and the oracle (no BOM), remove every
whitespace run from each (N3b), and compare the resulting non-whitespace byte sequences. Equal
bytes = pass.

**N3b — maintainer decision (2026-07-20).** N3b is the one uniform whitespace step the maintainer
added to the closed list on 2026-07-20, applied identically to the oracle and to every engine's
output in every ecosystem. Its purpose and ratified effect: **any whitespace-only divergence
between a candidate and the oracle passes the controlled byte gate**, and the
[exclusion path](#exclusion-policy) applies **only** to non-whitespace divergence. Because N3b
removes each whitespace run *entirely* (rather than collapsing it to a single space), the gate is
invariant to **every** whitespace-only difference — run-length changes (`a  b` vs `a b`) **and**
presence-vs-absence (`a b` vs `ab`, `<td >` vs `<td>`, `>␠/*` vs `>/*`) alike; the byte gate stays
strict on all non-whitespace content. This reconciles Q1.2's literal ruling ("an engine whose
output differs from the oracle only in whitespace still passes … exclusion applies only to
divergence beyond whitespace") with the closed-list principle: rather than a per-phase pipeline
fork (non-conformant) or a curve (forbidden), the list gains one uniform, disclosed step that every
conformant gate implements. It closes the escalated D13 question (option A).

*Disclosed trade-off (stated once).* Because the gate ignores all whitespace, a *genuine*
inter-word spacing difference — words the oracle single-spaces that a candidate runs together or
re-spaces — is whitespace-only and therefore **passes** the controlled gate, by the maintainer's
ruling. The residual fidelity duty falls to (a) the idiomatic-track verifier, whose needle matching
runs on the *same* whitespace-stripped projection (so it is whitespace-insensitive by the identical
rule — see [idiomatic-track gate](#idiomatic-track-gate)), and (b) the per-report fidelity notes.
No workload in the set has model-driven content whose inter-word spacing is semantically
load-bearing, so this trade-off masks no real defect in the cast.

*Relationship to N3/N4 and stored bytes.* N3b is a comparison-time projection, **not** a
stored-form transform: the oracle is stored in the readable **N1–N5** form (§On-disk format), so
its committed bytes are unchanged. N3 (inter-tag erase → nothing) and N4 (edge trim) remain as
storage-normalization steps that define those stored bytes — they keep every committed golden
byte-identical to v1's `TwinContent.Normalize` output. At comparison N3b **subsumes** both (removing
all whitespace does everything N3 and N4 did, and more), so the two steps no longer disagree on
whitespace's fate: N3, N4, and N3b all remove it. This closes the space-vs-nothing gap the earlier
"collapse to a single space" rule left open (where N3 erased inter-tag runs to nothing while the
collapse left a lone space elsewhere). *Labeling note:* the inter-tag step keeps its label **N3**;
the new step is **N3b** — there is deliberately no "N3a", the suffix simply marks N3b as the
whitespace step introduced after N3, so every existing "N3" reference across the spec set remains
valid and unchanged, and the range notations (`N2–N4`, `N1–N4`, `N1–N5`) transparently include N3b.

Steps N2–N4 (excluding N3b) are v1's three steps carried forward verbatim
(`TwinContent.Normalize`, [TwinContent.cs](../../../../src/Heddle.Performance/Runners/TwinContent.cs));
N1 (defined output encoding), N3b (the 2026-07-20 whitespace comparison step), and N5 are the v2
extensions. Raw-suite comparisons remain v1-compatible for every existing golden by construction:
N1 adds only the encoding definition cross-language use demands, N3b changes no stored bytes (it is
applied at comparison to both sides, never written to the corpus), and N5 never touches a raw
suite. The cross-stack corpus is exported fresh in the N1–N5 stored form; no re-export of an
already-shipped artifact is implied.

### Entity canonicalization (N5)

Engines legitimately differ on the entity spelling of the same escaped character (per-engine
survey: two spelling families — decimal/named vs hex; see the spike-C table cited in
[README D2](README.md#d2--entity-canonicalization-is-scoped-to-the-five-character-set-q11)).
N5 maps every semantically-identical spelling of the **five markup-significant characters** to
one canonical spelling. It is scoped to exactly these five characters; the spelling of any other
escaped character is **not** canonicalized — an engine that entity-escapes characters outside
this set (e.g. Go `html/template`'s `+` → `&#43;`, Handlebars-JS's `` ` `` → `&#x60;` /
`=` → `&#x3D;`) produces a genuine byte divergence, which the
[untrusted-data alphabet](#untrusted-data-alphabet) is designed to keep out of the corpus
workloads and which otherwise flows into the [exclusion policy](#exclusion-policy) as evidence.

Replacement table — each row lists every recognized input spelling (matched case-sensitively for
named entities; numeric references match with any number of leading zeros in the digits and
case-insensitive hex digits and `x`) and the canonical output:

| Character | Recognized spellings | Canonical |
|---|---|---|
| `&` U+0026 | `&amp;` \| `&#38;` \| `&#x26;` (+ leading-zero/case variants) | `&amp;` |
| `<` U+003C | `&lt;` \| `&#60;` \| `&#x3C;` | `&lt;` |
| `>` U+003E | `&gt;` \| `&#62;` \| `&#x3E;` | `&gt;` |
| `"` U+0022 | `&quot;` \| `&#34;` \| `&#x22;` | `&quot;` |
| `'` U+0027 | `&#39;` \| `&#x27;` \| `&apos;` | `&#39;` |

Rules:

1. Matching is a single left-to-right scan; replacements are non-overlapping and the output of a
   replacement is never rescanned (so data that *escaped to* `&amp;#39;` — a literal `&#39;` in
   source data — is not double-canonicalized; the alphabet additionally bans the `&#` substring
   in untrusted data as belt-and-braces).
2. The canonical spellings are exactly the five Heddle emits
   (`WebUtility.HtmlEncode` family / `ContextEncoders.EscapeAttribute`:
   `&amp; &lt; &gt; &quot; &#39;` — pinned by
   [OutputProfileEncodingTests](../../../../src/Heddle.Tests/OutputProfileEncodingTests.cs) and
   re-confirmed by executed probe, spike B). The oracle is therefore already canonical and N5 is
   an identity transform on it.
3. **Engine configuration is preferred to normalization wherever an engine offers it** (Q1.1
   resolution). Intra-.NET this is fully achieved — all five configured escaping paths emit the
   canonical spellings byte-for-byte (Handlebars.Net via the custom encoder,
   [README D3](README.md#d3--handlebarsnet-encoded-twins-use-a-custom-itextencoder)) — so the
   intra-.NET gate needs no N5 implementation at all. Phases whose engines have no spelling
   configuration (Askama's fixed `&#x27;`, Handlebars-JS's fixed hex family) implement N5 in
   their gate runner.

## Untrusted-data alphabet

An **authoring constraint on encoded-suite model values** (the "untrusted" strings substituted
into encoded templates), part of the contract because it is what makes the byte gate achievable
across all cast escapers. Every untrusted value MUST consist only of:

- ASCII printable characters U+0020–U+007E, **excluding** `+` (U+002B), `=` (U+003D), and
  `` ` `` (U+0060), and never containing the two-character substring `&#`;
- BMP characters U+0100–U+FFFF excluding the surrogate range (U+D800–U+DFFF).

Explicitly forbidden: C0/C1 controls, U+00A0–U+00FF (Latin-1 supplement), and astral-plane
characters (anything requiring a surrogate pair).

Grounding (each exclusion traces to a measured escaper divergence):

| Excluded | Because |
|---|---|
| U+00A0–U+00FF | `WebUtility.HtmlEncode` (Heddle's default text encoder) numeric-escapes this range (`é` → `&#233;`) while Heddle's own `@attr` encoder and every twin escape filter pass it through — executed probe, spike B |
| astral plane | `WebUtility.HtmlEncode` escapes surrogate pairs to decimal NCRs (U+1F600 → `&#128512;`) — pinned by `OutputProfileEncodingTests` rows |
| `+` | Go `html/template` escapes `+` → `&#43;` even in quoted-text/attribute contexts (fixed stdlib table; golang/go#42506) — phase 6 would otherwise be structurally excluded by data choice |
| `` ` ``, `=` | Handlebars-JS escapes both (`&#x60;`, `&#x3D;`) in its default path (fixed `badChars` table) — phase 4 likewise |
| `&#` substring | Guards N5 against any appearance of double-canonicalization (rule 1 above) |

The alphabet constrains **data**, not template literals (template literal text is identical
across engines by authoring and is never escaped). Both encoded workloads' pinned values
([workloads.md](workloads.md)) satisfy it, including the XSS payload and the Japanese strings.

## Controlled-track gate

1. **What is compared.** For each workload: normalize the candidate output
   ([pipeline](#normalization-pipeline)), remove every whitespace run from both it and the loaded
   corpus entry (N3b), UTF-8-encode, and compare the non-whitespace bytes. Any difference fails.
2. **When it runs.** Before any timing, in the same process/invocation that will produce the
   timed numbers — a hard gate. Intra-.NET: the `ParityCheck.Assert*` call in each benchmark
   class's `[GlobalSetup]` (twins vs live Heddle render — its comparison applies **N3b**, the
   whitespace strip on both sides, exactly like every other instance of this gate) **plus** the
   corpus-freshness check (live Heddle render vs committed corpus, which stays **byte-exact on the
   stored N1–N5 form** because it compares Heddle's own output to its own committed oracle — see
   [golden-corpus.md](golden-corpus.md#verification)). Phases 2–6: each harness runner loads the
   corpus entry and asserts before its measurement loop; a failed assertion must abort the run
   with no numbers emitted for that suite.
3. **Failure surface.** The gate reports: workload id, engine name, byte lengths
   (expected/actual), the index of the first differing byte, and an excerpt around it (the
   intra-.NET shape is `ParityCheck.Describe`: ±40 chars of context, 120-char window,
   `\n`-escaped). No benchmark numbers may be published for a suite whose gate did not pass in
   the same run.
4. **Whitespace-only divergence passes by construction** (Q1.2, as settled by the 2026-07-20
   maintainer decision): N2 reconciles line endings and N3b — removing every whitespace run
   (to nothing) from both sides before comparison — reconciles **all** remaining whitespace
   differences, so **any** divergence that is whitespace-only passes the controlled gate, whether
   it is a run-length change or a presence-vs-absence change (N3's inter-tag erase and N4's edge
   trim shape the stored oracle but no longer bound what passes — N3b subsumes them at comparison).
   This is the full extent of tolerated divergence on the raw suites; the encoded suite
   additionally tolerates only the N5 spellings. The [exclusion path](#exclusion-policy) is
   therefore reserved for **non-whitespace** divergence only.
5. **Encoded-suite security floor.** In addition to byte equality, the encoded gate asserts the
   raw payload rule: the raw substring `<script>alert(` occurs zero times in the *un-normalized*
   candidate output, and the escaped form (`&lt;script&gt;alert(` after N5) occurs the expected
   number of times. Byte equality with the oracle already implies this; the explicit check exists
   so a corrupted oracle can never silently waive the Fortunes rule (defense in depth, and it
   produces a security-specific failure message).

## Idiomatic-track gate

An idiomatic implementation passes when the per-workload **verifier** accepts its output. The
verifier is machine-checkable and is the only bar — "looks about right" is never the standard.
Verifier definitions are committed alongside the corpus
([`<workload>.verify.json`](golden-corpus.md#idiomatic-verifier-definitions)); a conformant
verifier implementation:

1. Normalizes the candidate with steps N1–N4 (and N5 for encoded workloads), then applies N3b —
   removing every whitespace run — to the normalized output **and** to every needle string
   (`values`/`markers`/`required`/`forbidden` text) before matching. The idiomatic track is thus
   whitespace-insensitive by the **same** closed rule as the controlled track: all matching is on
   the non-whitespace projection, so a needle's internal spaces and the output's spacing never
   affect a match or a count. (The `forbidden` raw-output check in step 4 additionally scans the
   un-normalized output, where whitespace removal only strengthens detection.)
2. **Value coverage** — for each `values[]` entry `{ "text": T, "count": N }`: the number of
   non-overlapping occurrences of `T` (whitespace removed) in the whitespace-stripped normalized
   output equals `N` exactly. The value
   lists name distinctive strings (first/last rows, full sentences, per-row markers), chosen per
   workload in [golden-corpus.md](golden-corpus.md#idiomatic-verifier-definitions).
3. **Ordered structural markers** — for `markers[]` `[M1 … Mk]`: searching the normalized output
   left to right, `M1` is found, then `M2` strictly after the end of `M1`'s match, and so on.
   Any miss or order violation fails.
4. **Escaping checks (encoded workloads only)** — for each `forbidden[]` entry: zero occurrences
   in **both** the raw and the normalized output; for each `required[]` entry
   `{ "text": T, "minCount": N }`: at least `N` occurrences in the normalized output.
5. **Failure reporting** — a failed check reports its kind (`value` / `marker` / `forbidden` /
   `required`), the offending entry, and expected-vs-found counts or positions.

Acceptance calibration (also a Phase 1 deliverable): each workload's verifier accepts the
workload's golden output and rejects its canonical corruptions — one data row removed and two
structural sections swapped (**every workload**), plus, **for encoded workloads only**, one
escaped payload replaced by its raw form. Raw workloads carry no escaped payload, so that third
corruption is inapplicable to them: raw workloads are calibrated against two corruptions, encoded
workloads against three. The intra-.NET `verify-corpus` command runs exactly this calibration
([golden-corpus.md](golden-corpus.md#verification)).

**Idiomatic authoring standard (Q1.7).** Idiomatic implementations are authored in-repo
following each engine's official documentation patterns, with the doc pages cited per
implementation — never imported from third-party benchmark repos. Each idiomatic implementation
file carries a header comment listing the official doc URLs it follows.

## Exclusion policy

1. **Structural exclusions.** Engines that structurally cannot target byte-exact output are
   excluded from the controlled track with a documented reason and are never graded on a curve.
   The four known structural exclusions: **Pug**, **Slim**, **Haml** (whitespace-significant
   syntaxes), **maud** (non-file DSL). None of these is in the cast; the list exists so the
   policy is concrete when someone proposes such an engine.
2. **Best-effort failure path (Q1.2).** An otherwise-file-based cast engine that cannot reach
   byte-exactness despite best-effort authoring: expected divergence is whitespace-only, which the
   gate tolerates in full (N3b removes every whitespace run from both sides at comparison, so
   **any** whitespace-only divergence — run-length or presence/absence — passes; 2026-07-20
   maintainer decision). Only divergence **beyond whitespace** triggers the fallback: the engine stays in the idiomatic track, its
   controlled-track cell is marked **excluded — documented evidence** (the report cell links to a
   written record of the divergent bytes, the authoring attempts made, and the engine behavior
   responsible), and no replacement engine enters the cast without user sign-off. Thymeleaf
   (phase 3) is the flagged risk for a non-whitespace divergence; templ whitespace behavior
   (phase 6) is now covered by N3b (its only identified controlled-track risk was whitespace-only)
   and invokes this path only if a genuine non-whitespace divergence surfaces.
3. **Whole-workload feasibility.** A workload that cannot pass the intra-.NET gate across all
   four twins is redesigned or dropped **before** any corpus entry is exported — no ecosystem
   ever ports a workload without a demonstrated-passable gate (this phase's ordering rule).
   Because the intra-.NET gate applies the **same** normalize-then-N3b comparison as every
   controlled gate (§Controlled-track gate), this admission bar is neither stricter nor looser
   than the measurement gate the published artifact is graded against: a workload is admitted
   exactly when its twins would pass that measurement gate — whitespace-only twin divergence is
   tolerated here too, and only non-whitespace divergence forces a redesign.

## What v2 changes for existing artifacts

| Artifact | Effect |
|---|---|
| Contract v1 (Runners README) | Untouched and still accurate for the intra-.NET raw suites; gains a pointer to this document. (V2's intra-.NET gate adds the N3b comparison strip, but it changes no existing verdict — every current twin is already byte-identical after N2/N3/N4, so removing whitespace from both sides leaves the outcome identical — so v1's description stays accurate for every shipped golden.) |
| Existing three raw workloads | Unaffected: their outputs never exercised N1's BOM rule (no twin emits one) or N5 (raw suite) |
| Razor twin | Remains outside every gate, as today; not part of the cross-stack contract |
| Published 2026-07-11 / 2026-07-18 reports | Immutable; nothing retroactive |
