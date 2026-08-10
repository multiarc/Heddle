# Thymeleaf controlled-track feasibility — analysis, authoring pattern, probe ladder, exclusion procedure

> Operational documentation of the completed cross-stack benchmark program — the contract the harnesses implement. Collapsed decision record: [cross-cutting-decisions.md § Program record — cross-stack benchmarks](../../docs/spec/common/cross-cutting-decisions.md#program-record--cross-stack-benchmarks-closed).

Companion document of the JVM harness (`benchmarks/jvm/`). Thymeleaf was the program's
flagged riskiest controlled-track port in the entire cast; the program's internal ordering
retired this risk **first**, before any other JVM work. This document does three things: (1) analyzes
exactly which Thymeleaf output divergence is *whitespace-only* (reconciled by the contract's
N2/N3/N3b/N4 — including the 2026-07-20 N3b whitespace-run collapse — and therefore a **pass**
under Q1.2 as settled) versus *beyond-whitespace* (the **non-whitespace** residual risk that alone
can trigger exclusion); (2) pins the concrete Thymeleaf mechanisms the
controlled templates use and the authoring pattern that minimizes the residual set; (3) defines
the executable feasibility-probe ladder and the documented evidence-collection procedure that
runs if exclusion triggers. Decision of record: README D1.

## What the gate actually tolerates (Q1.2 as modified)

The controlled gate compares the **non-whitespace** bytes of `normalize(candidate)` against those
of the stored (already normalized) oracle — N3b removes every whitespace run from both sides at
comparison. The closed pipeline
([contract — normalization pipeline](parity-contract-v2.md#normalization-pipeline))
erases exactly:

- **N2** — line-ending differences (`\r\n`/`\r` → `\n`);
- **N3** — any whitespace run **between `>` and `<`** (inter-tag), erased to nothing;
- **N3b** — **every** whitespace run, anywhere, removed to **nothing** from both sides at
  comparison (2026-07-20 maintainer step) — so the gate compares non-whitespace bytes and any
  whitespace-only difference, run-length or presence/absence, passes;
- **N4** — leading/trailing whitespace of the whole document;
- **N5** (encoded suite only) — spelling variants of the five escaped characters.

Everything **non-whitespace** is a byte divergence. Two consequences frame the whole analysis:

1. **Template layout is free between tags.** Thymeleaf preserves template text verbatim in
   output (its 3.x engine is event-based over AttoParser; it echoes what it does not process),
   so any line-breaking/indentation the controlled templates need is erased by N2/N3/N4. This is
   the entire class Q1.2's resolution predicted — and it passes.
2. **Whitespace *inside* a tag or *inside* an element's text run is now reconciled too.** Since
   the 2026-07-20 N3b ruling, a hypothetical `<td >` (trailing space where an attribute was
   removed, vs the oracle's `<td>`) or `bronze ` (trailing space inside a span's text, vs
   `bronze`) is a **whitespace-only divergence that passes** — these are exactly the
   presence-vs-absence cases N3b resolves: it removes every whitespace run to nothing from both
   sides, so `<td >`→`<td>` and `bronze `→`bronze` compare equal to the oracle. Only a
   **non-whitespace** divergence
   (a reordered/requoted/added-character byte) can now fail. The authoring pattern below still
   keeps output dense as the simplest way to stay well inside the gate.

## The mechanism set (all core Standard Dialect, all cited)

Controlled templates use exactly these documented mechanisms — nothing else:

> **Path note (2026-08-08).** The non-entry templates (`layout`, `chrome-fragments`, and the
> per-kind fragment files) now live under `{track}/shared/`, matching the reference tree shape;
> fragment references written here as `~{controlled/tile :: …}` / `~{controlled/layout :: …}`
> read `~{controlled/shared/tile :: …}` / `~{controlled/shared/layout :: …}` in the sources.
> Entry templates are unchanged at `thymeleaf/{track}/<workload>.html`.

| Mechanism | Used for | Doc (usingthymeleaf.html, 3.1) |
|---|---|---|
| Synthetic `<th:block>` element — processed and **removed whole** from output | carrier for every structural processor | §11.4 "Synthetic th:block tag" |
| `th:each="x : ${list}"` on `th:block` | loops | §6 "Iteration" |
| `th:if="${cond}"` on `th:block` | single toggles | §7 "Conditional evaluation" |
| `th:switch="${true}"` + `th:case="${bool}"` / `th:case="*"` on nested `th:block`s | the four-way tier chain — "as soon as one `th:case` attribute is evaluated as `true`, every other `th:case` attribute in the same switch context is evaluated as `false`. The default option is specified as `th:case="*"`" (verbatim) — i.e. a genuine short-circuiting chain, 1–4 evaluations per row | §7.2 "Switch statements" |
| Escaped inlined expression `[[${...}]]` (≡ `th:text`) | encoded-suite substitutions in text context | §12.1 "Expression inlining" |
| Unescaped inlined expression `[(${...})]` (≡ `th:utext`) | raw-suite substitutions | §12.1 |
| `th:fragment="tile(item)"` on `th:block` + `th:replace="~{controlled/tile :: tile(${item})}"` on `th:block` | the per-kind fragment partials (E20: `tile`, `card`, `media-row`, `stat`, plus the nested `badge`/`price` the card invokes) and the nav fragments (`mega_menu` → `nav_column` → `nav_section` → `nav_link`) | §8.1 "Including template fragments" |
| Parameterized-fragment **layout composition** (E20): the page's root `th:block` carries `th:replace="~{controlled/layout :: layout(~{:: slider-body})}"` — the whole page is replaced by the layout, so the local `th:fragment="slider-body"` definition nested inside it never renders in place — and the layout fragment `layout(bodySlot)` splices the body at its slot with `<th:block th:replace="${bodySlot}"/>` | composed-page: full-page layout with a live body slot | §8.2 "Fragment specification syntax" (fragment expressions as arguments, `~{:: selector}`), §8.3 "Flexible layouts" (`<th:block th:replace="${links}"/>` is the tutorial's own slot-insertion form) |
| `th:attr="name=${expr}"` — **the only sanctioned `th:*` attribute on output tags**, used solely where an attribute *value* is substituted (inlining works in tag bodies, not attribute values); **multi-assignment** `th:attr="a=…,b=…"` where two generated attributes must appear in a pinned order (see the amended B2) | `encoded-loop`'s `<td data-tag="…">`; `trivial-substitution`'s `<a class="link" href="…">` and `<img src="…">`; composed-page's nav `href`/`class` substitutions; fragment-heavy's `<img th:attr="src='/img/'+${item.name}+'.jpg',alt=${item.name}" />` (E21 attribute-side composition by expression concatenation — inlining cannot run in attribute values) | §5.1 "Setting the value of any attribute" (multi-assignment shown there) |

Explicitly **not** used on the controlled track: `th:text`/`th:utext` on output elements
(idiomatic-track idiom), `xmlns:th` (optional IDE incantation, omitted so no root-attribute
removal occurs), comments/prototype-only comments, `th:remove`, TEXT template mode.

**The block-only invariant.** Every output tag in a controlled template is literal text with
no `th:*` attribute (the `th:attr` value-substitution sites above are the only exception);
every `th:block` sits **between tags** (its own
open/close markers are flanked by `>`/`<` or by line breaks), never inside an element's text
run. Under this invariant the engine's output is: literal template bytes, minus whole
`th:block` elements, plus expression-result bytes — and any whitespace where a `th:block` stood
is inter-tag by construction, i.e. N3/N4 territory.

## Divergence taxonomy

### Class W — whitespace-only (passes via N2/N3/N3b/N4; no action)

| # | Behavior | Why it lands in W |
|---|---|---|
| W1 | Template authored multi-line for readability; oracle stored normalized | all layout differences are inter-tag → N3, edges → N4, line endings → N2 |
| W2 | Bytes left where a removed `<th:block …>`/`</th:block>` stood (its line, its indentation) | blocks are placed between tags (invariant) → the residue is a `>ws<` gap → N3 |
| W3 | Windows-checkout or writer-produced `\r\n` anywhere | N2 |

### Class B — potential beyond-whitespace divergence (the residual risk, each with disposition)

| # | Risk | Analysis | Disposition |
|---|---|---|---|
| B1 | **Attribute-removal residue** — removing a processed `th:*` attribute could leave `<td >` or a doubled space inside the tag | **whitespace-only → now passes.** Since the 2026-07-20 N3b ruling, an extra, doubled, or lone space inside a tag (`<td >` vs `<td>`) is a whitespace-only divergence that passes the controlled gate by construction — N3b removes every whitespace run to nothing from both sides, so presence-vs-absence of the residue reconciles. The only residue that could still fail is a *non-whitespace* one (a reordered/added-character byte), which is B2's concern, not B1's | no longer an exclusion risk; still exercised by rungs 1/7 as evidence that the residue (if any) is whitespace-only — a passing outcome |
| B2 | **Attribute reordering / requoting** of literal attributes (the 3.1 tutorial's own processed-output example shows `<meta content="…" http-equiv="…"/>` for input `<meta http-equiv="…" content="…"/>`) | *Amended (E20/E22 re-run, 2026-08-08):* the "exactly one two-attribute element" premise is retired — the E22 chrome carries **dozens** of multi-attribute literal tags (`<html class lang>`, `<meta http-equiv content>`, `<link href rel type>`, the eight-attribute search `<input>`, …). The re-run split the class in two, both **executed** at rung 8: **(a) unprocessed literal tags echo byte-exact, attributes in authored order** — the whole chrome passed the byte gate with zero reorder/requote hits; **(b) processed tags append `th:attr`-generated attributes at the END of the tag, after remaining literal attributes** — observed as a real non-whitespace divergence (`<a th:attr="href=…" class="drop">` rendered `<a class="drop" href="…">` against the oracle's `href`-first order; first diff at stripped index 7,414). Authoring rule from the exhausted-variant step: when a generated attribute must precede a literal one, route **both** through one multi-assignment `th:attr` in oracle order (`th:attr="href=${tab.href},class='drop'"` — generated attributes are emitted in specification order); when the literal attribute comes first (trivial-substitution's `<a class="link" href="…">`), append-at-end is already the oracle order | rungs 1 and 8 (executed 2026-08-08, both pass with the rule above); any residual hit → evidence record |
| B3 | **DOCTYPE processing** — 2.x-era Thymeleaf translated DOCTYPEs; the 3.1 tutorial's processed-output example (§3.1) shows `<!DOCTYPE html>` in → `<!DOCTYPE html>` out, suggesting 3.x echoes it | probed directly (mixed-page and fortunes-encoded both start `<!DOCTYPE html>`) | rungs 2, 5; any rewrite (added SYSTEM id, case change) → evidence record |
| B4 | **`th:utext`/`[(...)]` markup re-serialization** — unescaped insertion is markup-aware in 3.x; if the parser re-serialized inserted fragments (requoting, void-tag rewriting) rather than echoing them, composed-page's multi-KB HTML fragments (which include `™`, pre-escaped `&amp;`, and `<link …/>` self-closed tags) would diverge | *Retired by redesign (E20/E22, 2026-08-08):* no workload inserts markup through the unescaped path any more — the former blob areas are **literal template text** (chrome-fragment `th:block` fragments), echoed by the parser and inserted as parsed fragment models, and every `[(...)]` site substitutes markup-free ASCII (rule 4). The former blob content's `™`/`&`/`'` bytes now ride the literal-echo path instead, proven byte-exact at rung 8 (with the template read encoding pinned UTF-8 in `ThymeleafEngines` for the non-ASCII `™`) | retired by redesign; rung 8's byte gate covers the literal-echo path that replaced it |
| B5 | **Escaper set/spelling** off the canonical five | closed analytically: `th:text`/`[[...]]` uses unbescape `escapeHtml4Xml` = LEVEL_1 (only `& < > " '`) with spellings `&amp; &lt; &gt; &quot; &#39;` — byte-identical to the oracle's canonical set (verified at source, README D5); additionally executed by `GateCli probe` before any encoded gate | retired by analysis + probe |
| B6 | **Numeric conversion** — `${r.id}`, `${p.price}`, `${r.value}` are Java `Integer`s | `Integer.toString` on values in `[0, 5999]` is byte-identical to .NET invariant `int` formatting (plain ASCII digits, no separators/locale) | retired by analysis |
| B7 | **Inlining delimiter collision** — literal `[[`/`[(` in template text would be parsed as inlining | no workload's literal markup or pinned data contains `[[` or `[(` (checked against every normative template and model string) | retired by analysis |
| B8 | **`<style>` body processing** — mixed-page's CSS block | expression inlining processes tag bodies, but the CSS contains no `[[`/`[(` sequences (B7) and JS/CSS-specific inlining engages only with an explicit `th:inline` attribute, which controlled templates never set | rung 5 confirms byte-for-byte |
| B9 | **Void-element / tag-minimization serialization** on processed elements — a processed void tag (`th:attr` on `<img>`; `<meta>`/`<img>` in mixed-page) could in principle be re-serialized self-closing (`<img … />`) where the template authored the no-slash HTML5 form | near-nil in practice: Thymeleaf HTML mode preserves the authored no-slash form and pure-echoes non-`th:attr` void tags — but the guarantee is empirical, not source-closed here, so it is named rather than asserted | rungs 1 (trivial-substitution `<img>`/`<a>`) and 5 (mixed-page `<meta>`/`<img>`); rung 6 adds the processed self-closed form (media-row's `th:attr` `<img … />` — authored slash preserved, executed pass 2026-08-08); any rewrite → evidence record |
| B10 | **Fragment-selector tag-name collision** *(discovered at the 2026-08-08 rung-8 re-run)* — `~{template :: name}` is a **markup selector**: it matches `th:fragment="name"` *and* any element whose tag name is `name`, and `th:replace` inserts **every** match. A fragment named `meta` therefore pulled in all six literal `<meta>` tags of the file alongside nothing else (observed: +318 stripped bytes, head chrome duplicated from index 401) | authoring rule: fragment names must not collide with HTML element names used in the same template file — the section-default fragment is named `meta_section` (both tracks). Purely an authoring-pattern constraint; with non-colliding names the mechanism is byte-exact (rung 8 pass) | closed by the naming rule; any future collision reproduces as a deterministic non-whitespace divergence caught by the gate |

**Bottom line** *(amended at the 2026-08-08 re-run)*: with the block-only pattern, the
*analytical* residual **non-whitespace** risk reduces to B2 (attribute normalization/ordering —
now split into the passing literal-echo half and the append-at-end rule for generated
attributes), B3 (DOCTYPE echo), B9 (void-element serialization on processed tags), and the
newly named B10 (fragment-selector tag-name collision, closed by a naming rule). B1
(attribute-removal residue) is whitespace-only and **passes** via N3b (2026-07-20 ruling); B4
was retired by the E20/E22 redesign itself (no unescaped markup insertion remains anywhere);
everything else is either class W (whitespace-only → pass) or closed at source level. Each named
class is confined to named workloads and caught by the whole-output byte gate at rungs 1/5/6/8 —
so there is no undecided outcome, only these named empirical **non-whitespace** checks, and the
2026-08-08 re-run below shows each of them executed to a pass. That is the precise shrinkage
Q1.2's resolution anticipated (further widened by N3b) — and it is why this document expected all
eight cells to pass, while still specifying the failure path completely.

## The feasibility probe ladder

Executed as WI2,
**before any JTE or idiomatic work**. Each rung: author the controlled template
(normative text in [jvm-construct-mapping.md](jvm-construct-mapping.md)), run
`GateCli gate --engine thymeleaf --track controlled --workload <id>`, and either move on
(pass) or execute the evidence procedure (fail) and continue — rungs are independent; one
excluded cell never blocks the rest. Ordered cheapest-first / risk-isolated:

| Rung | Workload | New behavior it isolates |
|---|---|---|
| 1 | `trivial-substitution` | baseline echo fidelity: literal tags + `[(...)]` inlining, `th:attr` value substitution (B1), and the corpus's only two-attribute tag `<a class="link" href="…">` (B2) |
| 2 | `fortunes-encoded` | `[[...]]` escaping (B5 executed), DOCTYPE echo (B3), UTF-8 payload intact |
| 3 | `large-loop` | `th:each` on `th:block` at 5,000 iterations (W2 at scale); *(E21)* the display name is template-composed `row-[(${r.value})]` |
| 4 | `conditional-heavy` | `th:switch`/`th:case`/`*` chain + `th:if` toggles, blocks nested inside `<li>` between tags; *(E21)* the note text is template-composed `note [(${r.seq})]` |
| 5 | `mixed-page` | full skeleton: DOCTYPE (B3), `<style>` body (B8), page- and row-level `th:if`, nested loop; *(E21)* template-composed `MX-[(${p.skuNumber})]` and the blurb sentence around `[(${p.batch})]` |
| 6 | `fragment-heavy` | *(E20)* four-way `th:switch="${true}"` boolean dispatch per row to per-kind fragment **files** (`tile`/`card`/`media-row`/`stat`), one nesting level (card → `badge` + `price` against `${item.promo}`), and the E21 template compositions `[(${promo.price})].99`, `Caption for [(${item.name})]`, and the `th:attr` expression-concatenated `src='/img/'+${item.name}+'.jpg'` (B9 on a processed self-closed `<img … />`) |
| 7 | `encoded-loop` | `th:attr` on an output tag (B1 — the sanctioned exception), attribute-context escaping, 1 MB output |
| 8 | `composed-page` | *(E20/E22)* parameterized-fragment layout composition (`layout(~{:: slider-body})` + `th:replace="${bodySlot}"` slot), the full literal chrome incl. multi-attribute tags and IE conditional comments (B2/B3), chrome-fragment library insertion, nested nav fragments over the `nav.json` model (245 `nav_link` insertions), `th:attr` generated-attribute ordering (B2b), fragment-name collision hazard (B10) |

A rung passes only via the full contract gate (byte comparison + encoded security floor where
applicable) — never by eyeball. After rung 8 the phase's Q1.2 exposure is fully known; WI3
begins regardless of the pass/fail mix.

## Re-run of record — 2026-08-08 (amendment E20/E21/E22 redesign)

The E20/E21/E22 redesign changed the template text of five workloads, so the affected rungs
were **re-executed** against the regenerated corpus (manifest of the E20 export; N3b pins
39,481 / 5,840 for composed-page / fragment-heavy) before any timing work. Environment:
Thymeleaf 3.1.5.RELEASE, the benchmark jar's pinned toolchain, `GateCli gate --engine
thymeleaf --track controlled --workload <id>` per rung. Results, in execution order:

| Rung | Workload | Result (executed) |
|---|---|---|
| 3 | `large-loop` | **PASS** first authoring — `row-[(${r.value})]` composition |
| 4 | `conditional-heavy` | **PASS** first authoring — `note [(${r.seq})]` composition |
| 5 | `mixed-page` | **PASS** first authoring — `MX-[(${p.skuNumber})]` + template-side blurb |
| 6 | `fragment-heavy` | **PASS** first authoring — boolean `th:switch` dispatch to per-kind fragment files, nested badge/price, `th:attr` composed `src`/`alt` on the self-closed `<img … />` (B9: authored slash preserved) |
| 8 | `composed-page` | **PASS** at the third authoring iteration; the two intermediate failures were both non-whitespace, both resolved by documented authoring variants, neither reaching the exclusion threshold — see below |

Rung 8's two iterations (gate output retained verbatim in the port record):

1. **B10 (new class):** the section-default fragment named `meta` made
   `~{controlled/layout :: meta}` match every literal `<meta>` element of the file
   (markup-selector semantics) — `first diff at index 401 (of exp 39481/act 39799)`,
   expected `…img.jpg"/><linkrel="image_src"…`, actual `…img.jpg"/><metacharset="utf-8">…`.
   Variant applied: rename to `meta_section` (naming rule now in B10).
2. **B2b (generated-attribute placement):** `<a th:attr="href=${tab.href}" class="drop">`
   rendered `<a class="drop" href="#">` against the oracle's `<a href="#" class="drop">` —
   `first diff at index 7414 (of exp 39481/act 39481)`. Variant applied: multi-assignment
   `th:attr="href=${tab.href},class='drop'"` (generated attributes emit in specification
   order; rule now in B2).

After the two variants: **all sixteen Thymeleaf cells pass** — `gate --engine thymeleaf`:
16 passed, 0 failed, 0 excluded (both tracks, all eight workloads), with `probe` (all 6
escaping paths byte-for-byte) and `calibrate` (all 8 workloads) green. **The exclusion
path was not invoked**; `thymeleaf-exclusion-evidence.md` remains uncreated, and
`GateCli.EXCLUDED_CONTROLLED_CELLS` stays empty. Unaffected rungs 1/2/7 re-ran green in the
same full-engine gate.

One engine-module consequence recorded here: the E22 chrome carries a non-ASCII literal
(`™` in the secondary menus), so `ThymeleafEngines` pins the template read encoding to
UTF-8 (`ClassLoaderTemplateResolver.setCharacterEncoding("UTF-8")`) instead of inheriting
the platform default — a byte-gate precondition, not a behavior change.

## Exclusion-evidence procedure

Runs per failing rung, only for divergence the gate reports **after** normalization (a raw
diff that N2–N4 would erase is not a failure — the gate itself applies the pipeline).

1. **Exhaust authoring variants first.** For the failing workload, attempt every documented
   alternative mechanism that could plausibly change the divergent bytes (e.g. for B1:
   `th:attr` placement variants; for B4: `th:utext` on a literal wrapper vs `[(...)]`
   inlining), re-running the gate per variant. "Best-effort authoring" (Q1.2's words) means
   this list is enumerated and tried, not sampled.
2. **Record the evidence.** Create/append `thymeleaf-exclusion-evidence.md` alongside this
   document in `benchmarks/docs/` (created only when first triggered — the program closed
   with the exclusion path never invoked, so no such file exists) with, per workload:
   - workload id, engine + exact versions (Thymeleaf 3.1.5.RELEASE, JDK build), date;
   - the gate's failure output verbatim: expected/actual byte lengths, first-diff index, the
     ±40-char/120-char-window excerpt of both sides (`GateCli`'s `Describe`-shaped report);
   - **every template variant attempted**, full text, each with its own first-diff excerpt;
   - the engine behavior responsible, named and cited (doc section or engine source path) —
     the taxonomy row it instantiates (one of the non-whitespace classes B2–B4 or B9; a
     whitespace-only divergence passes via N3b and never reaches this procedure);
   - the classification argument: why the divergence is **non-whitespace** and beyond N2/N3/N3b/N4
     and N5 (byte positions shown to be a reordered / requoted / added-character byte, not
     whitespace).
3. **Apply the contract's consequences** ([exclusion policy pt 2](parity-contract-v2.md#exclusion-policy)):
   the workload's Thymeleaf **controlled cell** is `excluded — documented evidence`, linking to
   the record; Thymeleaf remains fully in the idiomatic track for that workload; the JMH class
   omits that one method (code comment naming the record); the report prints the exclusion
   marker in that cell (honest-reporting rule 6) — never a blank, never a curve-graded number.
   **No replacement engine** enters the cast without user sign-off (standing ruling; this document
   does not propose one).
4. **Escalate, don't absorb.** If the divergence implicates the *contract* rather than the
   engine (e.g. a normalization-pipeline ambiguity discovered while classifying), and it surfaces
   *at implementation time* (Phase 1 shipped), it goes to the
   [cross-spec amendments ledger](../../docs/spec/common/cross-cutting-decisions.md#cross-spec-amendments-ledger)
   as evidence — this phase never patches the contract locally. (The verifier-needle erratum in
   README D6
   was instead settled directly in the still-unshipped Phase 1 spec, because it was found during
   authoring, not implementation.)

## Why this is the expected-pass design, stated honestly

This document's job was to surface the risk, not to promise it away. The claim this document
makes is deliberately narrow: *given* the block-only pattern, every divergence class except
B2–B4 and B9 is closed by construction, at source level, or (the whitespace-only classes W1–W3
and B1) reconciled by the N2/N3/N3b/N4 whitespace normalization; and B2–B4 and B9 — the residual
**non-whitespace** classes — are each confined to named workloads with an executable probe and a
complete failure path. The claim it does **not** make: that Thymeleaf echoes non-whitespace bytes
perfectly — that is precisely what rungs 1–8 exist to establish, per workload, before a single
nanosecond is measured.
