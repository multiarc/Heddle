# Phase 7 — grammar realization (`<:name>` regions)

Supplementary to [phase-7-named-content-regions.md](phase-7-named-content-regions.md) (the entry
document — read it first for scope, decisions, and the four-layer plan). This document fixes the
**exact grammar diff** for the public-region marker `<:name>` / `<:name :: Type>`, proves it is
purely additive, and makes the ANTLR regeneration a reviewed work item.

Source citations give `path` + member; load-bearing line numbers are marked *(verify at
implementation — line numbers drift)*.

## Verified starting point

| Fact | Source |
|---|---|
| The definition rule is `def: DEF_STARTNAME ID def_props? def_base? DEF_ENDNAME default_chain? subtemplate def_type?`. The name `ID` is **mandatory** — nothing matches `< : name >` today, so `<:name>` is a hard syntax error now. | [`HeddleParser.g4`](../../../src/Heddle.Language/HeddleParser.g4) rule `def` *(≈14–16, verify)* |
| `def_base: DELIM ID` is the `<child:base>` inheritance suffix — a `DELIM` (`:`) **after** the name. `def_type: DEF_TYPE ID` is the trailing model type (`}} :: Blog`). `def_slot: ID DEF_TYPE ID` lives inside `def_props` (`out:: Type`) and is **not** touched here. | [`HeddleParser.g4`](../../../src/Heddle.Language/HeddleParser.g4) rules `def_base`, `def_type`, `def_slot` |
| In lexer mode `DEF` the tokens `DEF_STARTNAME` (`<`), `DELIM` (`:`), `DEF_TYPE` (`::`), `ID` (via `TYPE_ID: ID_TYPE -> type(ID)`), `DEF_ENDNAME` (`>`) and hidden `DEF_WS` **already exist**. `<:item :: Article>` already tokenizes as `DEF_STARTNAME DELIM ID DEF_TYPE ID DEF_ENDNAME` in `DEF` mode — the lexer needs **no** change. | [`HeddleLexer.g4`](../../../src/Heddle.Language/HeddleLexer.g4) mode `DEF` *(≈88–104, verify)* |
| The grammar is **generated and committed**: `generate_cs.cmd` runs `antlr-4.13.1-complete.jar` over `HeddleLexer.g4`+`HeddleParser.g4` into `generated/`, and `generated/HeddleParser.cs` / `HeddleParserBaseListener.cs` are in the tree. | [`generate_cs.cmd`](../../../src/Heddle.Language/generate_cs.cmd), [`generated/`](../../../src/Heddle.Language/generated/) |

## The diff (parser only)

Only `HeddleParser.g4` changes. Add one alternative to `def` and one tiny helper rule:

```antlr
def:
      DEF_STARTNAME ID def_props? def_base? DEF_ENDNAME default_chain? subtemplate def_type?   // unchanged
    | DEF_STARTNAME DELIM ID def_region_type? DEF_ENDNAME subtemplate                          // NEW (phase 7): <:name> public region
    ;

// NEW (phase 7): the in-header model type of a public region (<:item :: Article>). The type sits
// inside the angle brackets (R7's ratified <:name :: Type> shape), distinct from a definition's
// trailing def_type (}} :: T). A region carries no prop list, no base, and no default output chain.
def_region_type: DEF_TYPE ID;
```

**No lexer edit.** Every token in the new alternative already exists in mode `DEF`.

### Why it is unambiguous and additive

- **Disambiguation is one token of lookahead.** After `DEF_STARTNAME` (`<`), the existing
  alternative requires `ID`; the new alternative requires `DELIM` (`:`). `ID` and `DELIM` are
  disjoint token types, so ANTLR's ALL(\*) decision is a single-token switch — no ambiguity with
  `<child:base>` (which always has an `ID` before its `DELIM`) or plain `<name>`.
- **Purely additive.** `< : name …` matches **no** production today (the old `def` requires `ID`
  immediately after `DEF_STARTNAME`), so it is a syntax error (`HED0003`) in every template that
  exists. Adding a production for a currently-unparseable input cannot change the parse of any
  input that parses today. The **eleven** existing `DEF`-mode lexer rules (`DEF_COMMENT`,
  `DEF_STARTNAME`, `TYPE_ID`, `DEF_ENDNAME`, `DEF_PROPSTART`, `SUB_START`, `DEF_OUT`, `DEF_TYPE`,
  `DELIM`, `DEF_CLOSE`, `DEF_WS`) and their token stream are byte-for-byte unchanged (the lexer is
  untouched), which the grammar-stability regression gate confirms.
- **In-header type, by design.** `<:item :: Article>` places `:: Article` **inside** the brackets
  via `def_region_type`, matching R7 verbatim. A region has no trailing `def_type`, no `def_props`,
  and no `default_chain`: a region is rendered only by being called (`@item(this)`), never by a
  document-end default chain, and it declares no props of its own (it reads the enclosing
  component's props — see the entry document, D6). This is the deliberate YAGNI cut; widening the
  region header later (props, base) is a compatible follow-on.

## Listener/parse-context wiring

The generated `DefContext` gains nullable accessors `DELIM()` and `def_region_type()`; the existing
`ID()`, `subtemplate()`, `def_props()`, `def_base()`, `def_type()`, `default_chain()` are unchanged.
Because `DELIM` in the new alternative is a **direct** child of `def` (the `<child:base>` `DELIM`
lives inside the `def_base()` sub-context), `context.DELIM() != null` is the exact discriminator for
the region form.

[`ParseContext.CreateDefinition`](../../../src/Heddle/Language/ParseContext.cs) *(≈161–255, verify)*
gains a leading branch:

```csharp
if (context.DELIM() != null)          // <:name> / <:name :: Type> — a public region declaration
    return CreateRegionDefinition(context, out chain);
```

`CreateRegionDefinition` builds a `DefinitionItem` for the region exactly as the no-base branch does
(name from `context.ID()`, body from `context.subtemplate()`, `modelType` from
`context.def_region_type()?.ID()` or `"object"` when omitted), then sets the new flags
`IsRegion = true` and `IsPublicRegion = true` and prepares the region's `RegionDeclaration`. The
`RegionDeclaration` is **appended to the enclosing component's `Regions` list on the `EnterDef`
store-success path** (after the ≈54–58 duplicate check passes, alongside the `DefinitionsBlock` store at
≈60) — **not** eagerly here — so a rejected duplicate `<:name>` is never recorded in `Regions` (region
table, "Append ordering"; entry document, D3/D6). The private inner definitions of a component body (plain
`<name>` parsed while `ParseContext.InDefintionContext` is true) set `IsRegion = true`,
`IsPublicRegion = false` — *(verify at implementation: confirm `InDefintionContext` is true while a
component body's inner `@%…%@` definitions are parsed; if not, thread an explicit in-body flag
through [`HeddleMainListener.EnterSubtemplate`](../../../src/Heddle/Language/HeddleMainListener.cs)
≈130–167).*

Editor-token emission (`AddToken`) for the region's `DEF_STARTNAME`, `DELIM`, `ID`, `DEF_TYPE`, `ID`,
`DEF_ENDNAME`, `SUB_START`, `SUB_CLOSE` mirrors the existing branches so the LSP colourizes the new
form (entry document, WI5).

## Regeneration work item (licensed grammar change)

Per the [coding standards](../common/coding-standards.md) grammar-stability rule, a `.g4` edit is a
**licensed** change executed as one reviewed unit:

1. Edit `src/Heddle.Language/HeddleParser.g4` with the diff above.
2. Run `src/Heddle.Language/generate_cs.cmd` (ANTLR **4.13.1**, matching
   `Antlr4.Runtime.Standard` 4.13.1 in [`Heddle.Language.csproj`](../../../src/Heddle.Language/Heddle.Language.csproj)).
3. Commit the regenerated `generated/HeddleParser.cs`, `generated/HeddleParserListener.cs`,
   `generated/HeddleParserBaseListener.cs` **together** with the `.g4` edit — no hand-editing of
   generated files. `generated/HeddleLexer.cs` must come back byte-identical (lexer untouched); a
   non-empty lexer diff means the change slipped its scope and must be rejected.
4. The grammar-stability gate re-runs the generator in CI and diffs the committed output; the lexer
   diff being empty and the parser diff being exactly the two-rule addition is the gate.
