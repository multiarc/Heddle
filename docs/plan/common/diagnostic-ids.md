# Diagnostic-ID allocations

Several phases introduce new `HEDxxxx` diagnostics. Their allocations are planned **once here** so
they cannot collide, and so the single claim into the repository's
[claimed-diagnostic-ids registry](../../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry)
is coordinated. Shared by [Phase 2](../phase-2-authoring-ergonomics.md),
[Phase 3](../phase-3-html-context-encoding-lint.md),
[Phase 7](../phase-7-named-content-regions.md), and
[Phase 8](../phase-8-extension-parameters.md) (which reuses the prop family, relaxes `HED5005`, and
adds the generator twin `HED7017`).
[Phase 8](../phase-8-extension-parameters.md) claims **no new call-time id** — it reuses the existing
prop diagnostics (`HED5001`–`HED5004`) and *relaxes* **`HED5005` only** (see below). `HED5006`
(`DefinitionHasNoProps`) is untouched: it never fires on the extension surface, because an extension
short-circuits to `HED5005` first. A malformed-`[Prop]` declaration is diagnosed on **both** tiers — on
the **dynamic tier** it reuses the existing declaration-side prop ids
`HED5007`/`HED5009`/`HED5010`/`HED5015` (now reachable when the declaration source is an extension
attribute), and the generator id **`HED7017`** is its build-time twin. ([Phase 6](../phase-6-range-type.md)
also needs **no new id** — it keeps the existing `HED4001` positive-step validation on the new `Range`
type.)

## Current allocation (grounded)

Verified against [HeddleDiagnosticIds.cs](../../../src/Heddle/Data/HeddleDiagnosticIds.cs) and the
[registry](../../spec/common/cross-cutting-decisions.md#claimed-diagnostic-ids-registry). Family
next-free ids relevant to this plan:

| Family | Meaning | Highest allocated | Next free |
|---|---|---|---|
| `HED0xxx` | core / syntax | `HED0004` | `HED0005` |
| `HED2xxx` | output profiles / encoding | `HED2003` | **`HED2004`** |
| `HED4xxx` | template semantics & ergonomics | `HED4004` | **`HED4005`** |
| `HED5xxx` | props / slots | `HED5018` | **`HED5019`** |
| `HED7xxx` | generator / build-time | `HED7016` | **`HED7017`** |

## Planned claims

| Diagnostic | Phase | Severity | Purpose | Lean |
|---|---|---|---|---|
| **`HED4005`** | [2](../phase-2-authoring-ergonomics.md) | warning | `{{ identifier }}`-in-text misread — "did you mean `@(identifier)`?" | Ergonomics family, alongside the `HED4001`/`HED4002` author-guidance diagnostics. Preferred over the next-free core id `HED0005`, which would group it with resolver/syntax errors instead of author guidance. |
| **`HED2004`** | [3](../phase-3-html-context-encoding-lint.md) | warning | Bare `@(value)` in an HTML attribute / `<script>` / URL position under `OutputProfile.Html` without the matching `@attr`/`@js`/`@url` encoder. | Next free in the output-profiles/encoding family that already owns `@profile`/`@raw`/encoding diagnostics (`HED2001`–`HED2003`). |
| **`HED5019`+ (reserved block)** | [7](../phase-7-named-content-regions.md) | error (compile) | Named-region diagnostics (definitions) that fire **only on the new public-region surface**: overriding a **private-but-declared** region; a **duplicate public-region declaration**; a region override-body **type mismatch** against a matched public region. (There is no "unknown-region" error and no "missing-required-region" error — an override whose name matches no public region keeps today's local-definition-override meaning, and regions are default-backed, hence optional.) | Reserve a small contiguous block (`HED5019`–`HED502x`) in the props/slots family, alongside the existing slot diagnostics `HED5012`–`HED5018`. Exact count follows the ratified surface. |
| **`HED5001`–`HED5005` (reused / relaxed, not new)** + declaration-side `HED5007`/`HED5009`/`HED5010`/`HED5015` | [8](../phase-8-extension-parameters.md) | error (compile) | Extension **parameters** reuse the existing prop diagnostics — unknown parameter (`HED5001`), required unbound (`HED5002`), type mismatch (`HED5003`), duplicate argument (`HED5004`) — now applied to extensions. The phase **additively relaxes `HED5005` only** (named args to a non-definition) so a *parameter-declaring* extension accepts its declared named args; an extension declaring no parameters still errors with `HED5005` exactly as today (its message wording is updated). **`HED5006` (`DefinitionHasNoProps`) is untouched** — it never fires on an extension (the extension short-circuits to `HED5005` first), so there is nothing to relax. A malformed `[Prop]` declaration reuses the declaration-side prop diagnostics `HED5007` (duplicate declaration) / `HED5009` (default not convertible) / `HED5010` (unresolved type) / `HED5015` (reserved name) on the **dynamic tier**, now reachable when the declaration source is an extension attribute. | No new id at call time — this is a relaxation of `HED5005` plus reuse of the prop family. The malformed-`[Prop]` condition draws the declaration-side ids on the dynamic tier and the next free generator id **`HED7017`** as its build-time twin. |

## Rules

- The `@@` literal-`@` escape in [Phase 2](../phase-2-authoring-ergonomics.md) **removes** a
  compile error (today `@@` fails to parse); it needs no new id.
- Each id is claimed in the repository registry in the same change that introduces it (the D1
  stable-id discipline), so the registry never lags the code.
- `HED2004` and `HED4005` are **warning** severity — they can fire on templates valid today and
  must never break a build (ruling [R5](standing-rulings.md#standing-rulings-user-set)). The
  `HED5019`+ named-region diagnostics are compile **errors**, but fire **only** on the *new*
  public-region surface — a callee that declares `<:name>` public regions — never on a template
  valid today (a call-body override against a callee with no public regions retains its current
  local-definition-override semantics), so they too are additive.
- [Phase 8](../phase-8-extension-parameters.md)'s **`HED5005`-only relaxation** is additive the
  same way the `@@` change is: today named args to an extension are a hard `HED5005` error, so no
  template that compiles today exercises the relaxed path — it only makes a previously-erroring shape
  compile for a parameter-declaring extension. `HED5006` is **not** part of the relaxation; it never
  reaches the extension surface (the extension short-circuits to `HED5005` first).
