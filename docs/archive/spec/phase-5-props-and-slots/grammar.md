# Phase 5 grammar specification — props & slots

Supplementary document of the [phase 5 spec](README.md). It contains the complete lexer
and parser rule changes for prop declarations, call-side named arguments, the `this`
expression, and the slot-parameter declaration; the coexistence and ambiguity analysis
against every existing call and definition form (the roadmap's `:` collision analysis,
verified); the editor-token classification; the regeneration procedure; and the parse
corpus that serves as the grammar's executable spec. Everything here is normative; the
entry document's implementation plan references these rules by section.

## Verified current state

- [HeddleLexer.g4](../../../src/Heddle.Language/HeddleLexer.g4) `mode DEF` contains
  exactly ten rules: `DEF_COMMENT` (hidden), `DEF_STARTNAME` (`<`), `TYPE_ID`
  (`ID_TYPE → ID`), `DEF_ENDNAME` (`>`), `SUB_START` (`{{` → push `SUB_BLOCK`),
  `DEF_OUT` (`->` → push `OUT_MODE`), `DEF_TYPE` (`::`), `DELIM` (`:`), `DEF_CLOSE`
  (`%@` → pop), `DEF_WS` (hidden). **No rule matches `(`, `)`, `,`, `=`, `-` (bare), or
  any literal character** — every character of the new declaration surface is a
  token-recognition error in `DEF` mode today, confirming the roadmap's constraint 4.
- The `ID_TYPE` fragment is, exactly (`HeddleLexer.g4` line 12, anchor `fragment ID_TYPE`;
  wrapped here for width — the source is one line):

  ```antlr
  fragment ID_TYPE: IDENTIFIER ('.' IDENTIFIER)* SINGLE_LINE_WS*
      ('<' SINGLE_LINE_WS* ID_TYPE SINGLE_LINE_WS* (',' SINGLE_LINE_WS* ID_TYPE)* SINGLE_LINE_WS* '>')?
      '[]'*;
  ```

  Three properties of this rule are load-bearing below: the trailing `SINGLE_LINE_WS*`
  after the dotted name makes a bare-keyword rule lose maximal munch to `ID_TYPE` unless
  the keyword rule carries the same trailing pattern (the phase 1 `CALL_ID` analysis,
  re-applied in lexer note 1); the generic argument list is **recursive** and consumes
  its own `<`, `,`, `>`, and interior single-line whitespace inside the one token
  (`System.Collections.Generic.List<string>` and nested forms like
  `Dictionary<string, int[]>` are each a single `ID` — lexer note 4, corpus row PP19);
  and the terminal `'[]'*` accepts any array-rank suffix run (`string[]`, `int[][]` —
  corpus row PP24).
- `mode CALL` is assumed in its **phase 1 final state**
  ([phase 1 grammar — lexer changes](../phase-1-native-expressions/grammar.md#lexer-changes--mode-call-only)):
  keyword literals (`TRUE`/`FALSE`/`NULL` with the `WS*` tie), literal tokens
  (`INT_LIT`, `REAL_LIT`, `STRING_LIT`, `CHAR_LIT`), the operator tokens, `LBRACKET`/
  `RBRACKET`/`COMMA`, with `CALL_ID: ID_TOKEN WS* -> type(ID)` and
  `CALL_DELIM: EXT_DELIM -> type(DELIM)` unchanged from today.
- `mode CS` (the inner-`@` C# tier): entered from `CALL` via
  `CSHARP_START: OUT_ST -> popMode, pushMode(CS)`; its **only exit** is
  `CS_CSHARP_END: PARA_CL -> type(OUT_PARAMEND), popMode` — the call-closing `)`. A
  top-level `,` inside `CS` is an ordinary `CSHARP_TOKEN`. This is the source evidence
  for the entry document's D4 correction (no per-argument C# tier).
- [HeddleParser.g4](../../../src/Heddle.Language/HeddleParser.g4):
  `def: DEF_STARTNAME ID def_base? DEF_ENDNAME default_chain? subtemplate def_type?;`
  `def_base: DELIM ID;` `def_type: DEF_TYPE ID;` `default_chain: DEF_OUT chain;`
  (the default chain reuses `chain`/`call`, so the new 5th alternative is reachable from
  `-> (…)` with no extra work). The `call` rule is assumed in its phase 1 final state
  (four alternatives + the left-recursive `expr`).
- [CSharp.g4](../../../src/Heddle.Language/CSharp.g4) provides every fragment reused
  below: `INT`, `REAL`, `CHAR`, `REGULAR_STRING_LITERALS`, `IDENTIFIER`, `WHITESPACE`,
  `SINGLE_LINE_WS` (verified present). `CSharp.g4` is not modified.
- Generated code lives in `src/Heddle.Language/generated/`, produced by
  [generate_cs.cmd](../../../src/Heddle.Language/generate_cs.cmd) (ANTLR 4.13.1,
  `-package Heddle.Language`); runtime `Antlr4.Runtime.Standard` 4.13.1.

## Token vocabulary additions

The final state of the `tokens { … }` declaration at the top of `HeddleLexer.g4` (the
parser references these via `tokenVocab=HeddleLexer`). The existing entries are the
verified current list, order preserved; the phase 1 block is that spec's
[final state](../phase-1-native-expressions/grammar.md#token-vocabulary-additions),
unchanged; this phase appends two entries. No existing token is renamed or renumbered:

```antlr
tokens {
    // existing entries — unchanged, verified against HeddleLexer.g4 (July 2026):
    TEXT, WS, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE,
    CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DELIM,
    DEF_START, DEF_CLOSE, RAW, OUT_PARAMSTART, OUT_PARAMEND, DEF_OUT,
    // phase 1 additions — unchanged:
    TRUE, FALSE, NULL, INT_LIT, REAL_LIT, STRING_LIT, CHAR_LIT,
    OP_QQ, OP_QUESTION, OP_AND, OP_OR, OP_EQ, OP_NEQ,
    OP_LSHIFT, OP_RSHIFT, OP_LE, OP_GE, OP_LT, OP_GT,
    OP_PLUS, OP_MINUS, OP_STAR, OP_SLASH, OP_PERCENT,
    OP_AMP, OP_PIPE, OP_CARET, OP_NOT, OP_TILDE,
    LBRACKET, RBRACKET, COMMA,
    // NEW (phase 5):
    THIS, ASSIGN
}
```

`THIS` is the `this` keyword in `CALL` mode; `ASSIGN` is the `=` of a prop default in
`DEF_PROPS` mode. Every other terminal this phase needs already exists (`ID`, `DELIM`,
`DEF_TYPE`, `COMMA`, `OP_MINUS`, the literal tokens, `OUT_PARAMSTART`/`OUT_PARAMEND`).

## Lexer changes

### `mode CALL` — one new rule

Placed **immediately after `CALL_NULL`**, keeping the keyword-literal group contiguous.
The only load-bearing ordering constraint is that it precede `CALL_ID` (rule order is
the tie-break; see note 1) — its position relative to the literal and operator rules is
immaterial (no shared match prefixes) but is pinned here for regen determinism:

```antlr
// NEW (phase 5) — the 'this' keyword. Same trailing-WS* technique as CALL_TRUE et al.:
// CALL_ID is ID_TOKEN WS*, so a bare 'this' rule would lose maximal munch for input
// "this " (5 chars via CALL_ID vs 4). Equal lengths make rule order decide.
CALL_THIS: 'this' WS* -> type(THIS);
```

No other `CALL` change: named arguments reuse the existing `ID`, `DELIM`, and `COMMA`
tokens. `CS`, `CS_NESTED`, `CALL_RETURNED`, `OUT_MODE`, and all string/interpolation
modes are untouched.

### `mode DEF` — one new rule

Placed immediately after `DEF_ENDNAME` (position is pinned for determinism; no `DEF`
rule shares a first character with `(`, so ordering is not load-bearing):

```antlr
// NEW (phase 5) — a '(' after the definition name opens the prop list. Reuses the
// OUT_PARAMSTART token type; pushes the dedicated DEF_PROPS mode (it does NOT push
// CALL — the declaration surface has its own vocabulary).
DEF_PROPSTART: PARA_ST -> type(OUT_PARAMSTART), pushMode(DEF_PROPS);
```

### New `mode DEF_PROPS` (inserted directly after the `mode DEF` block)

```antlr
mode DEF_PROPS;

DEFP_COMMENT: COMMENT_BLOCK -> channel(HIDDEN);

DEFP_PROPSEND: PARA_CL -> type(OUT_PARAMEND), popMode;

// '::' before ':' for readability; maximal munch already prefers the longer match.
DEFP_SLOT_TYPE: DEF_T -> type(DEF_TYPE);
DEFP_DELIM:    EXT_DELIM -> type(DELIM);
DEFP_ASSIGN:   '=' -> type(ASSIGN);
DEFP_COMMA:    ',' -> type(COMMA);
DEFP_MINUS:    '-' -> type(OP_MINUS);

// Keyword literals for defaults. They carry ID_TYPE's exact trailing pattern
// (SINGLE_LINE_WS*) so that for input "true " both this rule and DEFP_TYPE_ID match
// five characters — the lengths tie and rule order selects the keyword (note 1).
DEFP_TRUE:  'true'  SINGLE_LINE_WS* -> type(TRUE);
DEFP_FALSE: 'false' SINGLE_LINE_WS* -> type(FALSE);
DEFP_NULL:  'null'  SINGLE_LINE_WS* -> type(NULL);

// Literals for defaults (fragments from CSharp.g4). REAL before INT is cosmetic —
// maximal munch already prefers '1.5' over '1'. The string rule deliberately mirrors
// phase 1's CALL_STRING (no UTF8_SUFFIX — "…"u8 is not a template literal).
DEFP_REAL:   REAL -> type(REAL_LIT);
DEFP_INT:    INT  -> type(INT_LIT);
DEFP_STRING: '"' REGULAR_STRING_LITERALS? '"' -> type(STRING_LIT);
DEFP_CHAR:   CHAR -> type(CHAR_LIT);

// Prop names AND type names: one rule. ID_TYPE covers plain identifiers, dotted
// names, generics (List<string> is a single token — its '<'/'>' never meet
// DEF_ENDNAME), and arrays (string[]).
DEFP_TYPE_ID: ID_TYPE -> type(ID);

DEFP_WS: WS+ -> channel(HIDDEN);
```

Lexer design notes (each normative):

1. **Keyword tie-break.** `DEFP_TYPE_ID` matches `true ` as five characters
   (`IDENTIFIER` + `SINGLE_LINE_WS*`), so bare `'true'` (four) would lose maximal
   munch. Defining the keyword rules with the identical trailing `SINGLE_LINE_WS*`
   equalizes the lengths; ANTLR then "gives precedence to the lexical rules specified
   first" — the phase 1 correction re-applied with `ID_TYPE`'s own whitespace pattern
   ([lexical FAQ](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md),
   [maximal munch](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md)).
   Longer identifiers still win (`truely` → `ID`); keywords are case-sensitive
   (`True` → `ID`).
2. **Mode isolation is the additive proof.** ANTLR only matches rules of the current
   mode ([lexer rules doc](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md)).
   `DEF_PROPS` is reachable solely through the new `DEF_PROPSTART` rule, whose trigger
   character is a token error in `DEF` mode today — therefore **no existing template's
   token stream can change**, and the ten existing `DEF` rules are byte-identical.
3. **`-` vs `->`.** `DEF_PROPS` deliberately contains no `->` rule, so `DEFP_MINUS`
   cannot collide with `DEF_OUT` (which lives in `DEF` mode, outside the parens).
   `OP_MINUS` here serves only signed numeric defaults (`= -4`); the parser restricts
   it to `INT_LIT`/`REAL_LIT` (`def_literal`).
4. **Generics never close the header early.** `List<string>` lexes as one `ID` token
   (`ID_TYPE` consumes the balanced generic argument list), so its `>` cannot be taken
   for `DEF_ENDNAME`; the header's closing `>` is only reachable after `DEFP_PROPSEND`
   pops back to `DEF`. This is the same mechanism that already protects
   `:: ICollection<Article>` today.
5. **Reserved names fall out of the token set.** `true`/`false`/`null` lex as keyword
   tokens in `DEF_PROPS`, so a prop named `true` is a syntax error (`HED0003`), never a
   semantic case. `this` and `out` lex as `ID` here and are rejected at walk time with
   `HED5015` (entry doc D3) — `this` because a `this:` named argument is unlexable at
   call sites (`CALL_THIS` wins there), `out` because it is the slot mechanism's name.
6. **Comments and whitespace.** `@* … *@` and all whitespace go to the hidden channel
   inside the prop list (multi-line headers are legal), matching every other mode; the
   listener's `SkippedTokens` machinery is unaffected.

## Parser changes

### Call side

The `call` rule gains a **fifth** alternative, ordered last so ALL(\*)
first-alternative-wins preserves every existing parse (the phase 1 mechanism):

```antlr
call:
    extension_id? OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
    | extension_id? OUT_PARAMSTART WS* member_expression? OUT_PARAMEND
    | extension_id? OUT_PARAMSTART chain OUT_PARAMEND
    | extension_id? OUT_PARAMSTART native_expression OUT_PARAMEND
    | extension_id? OUT_PARAMSTART (expr COMMA)? named_argument (COMMA named_argument)* OUT_PARAMEND
    ;

named_argument: ID DELIM expr;
```

The `expr` rule gains one primary (non-left-recursive seed) alternative, listed between
`LiteralExpr` and `PathRootExpr`. The full post-phase-5 rule text (every other
alternative keeps its exact phase 1 text and order — the one marked line is the only
change; `arg_list` and `literal` are unchanged and repeated for completeness):

```antlr
expr:
      expr MEMBER_P ID arg_list                                    # MethodCallExpr
    | expr MEMBER_P ID                                             # MemberHopExpr
    | expr LBRACKET expr (COMMA expr)* RBRACKET                    # IndexExpr
    | op=(OP_NOT | OP_MINUS | OP_PLUS | OP_TILDE) expr             # UnaryExpr
    | expr op=(OP_STAR | OP_SLASH | OP_PERCENT) expr               # MultiplicativeExpr
    | expr op=(OP_PLUS | OP_MINUS) expr                            # AdditiveExpr
    | expr op=(OP_LSHIFT | OP_RSHIFT) expr                         # ShiftExpr
    | expr op=(OP_LT | OP_GT | OP_LE | OP_GE) expr                 # RelationalExpr
    | expr op=(OP_EQ | OP_NEQ) expr                                # EqualityExpr
    | expr OP_AMP expr                                             # BitAndExpr
    | expr OP_CARET expr                                           # BitXorExpr
    | expr OP_PIPE expr                                            # BitOrExpr
    | expr OP_AND expr                                             # AndAlsoExpr
    | expr OP_OR expr                                              # OrElseExpr
    | <assoc=right> expr OP_QQ expr                                # CoalesceExpr
    | <assoc=right> expr OP_QUESTION expr DELIM expr               # TernaryExpr
    | ID arg_list                                                  # FunctionCallExpr
    | OUT_PARAMSTART expr OUT_PARAMEND                             # GroupExpr
    | literal                                                      # LiteralExpr
    | THIS                                                         # ThisExpr    // NEW (phase 5)
    | ROOT_REF? ID                                                 # PathRootExpr
    ;

arg_list: OUT_PARAMSTART (expr (COMMA expr)*)? OUT_PARAMEND;

literal: INT_LIT | REAL_LIT | STRING_LIT | CHAR_LIT | TRUE | FALSE | NULL;
```

`ThisExpr`'s precedence slot: as a primary seed it does not participate in operator
precedence at all (the phase 1 left-recursion note — primaries after the operators do
not affect the operator table), and the left-recursive postfix alternatives compose over
it exactly as over any primary — `this.Name` parses via `MemberHopExpr` into
`PathNode{Target = ThisNode}[Name]` (corpus row PP11), `this` as a function argument via
`arg_list` (PP12), `this == null` via `EqualityExpr`. Its placement between
`LiteralExpr` and `PathRootExpr` groups it with the keyword literals; no input reaching
`PathRootExpr` can begin with `THIS` (distinct token), so the seed order among the three
is not semantics-bearing either — pinned for regen determinism.

### Definition side

```antlr
def:
    DEF_STARTNAME ID def_props? def_base? DEF_ENDNAME default_chain? subtemplate def_type?
    ;

def_props: OUT_PARAMSTART (def_prop_item (COMMA def_prop_item)*)? OUT_PARAMEND;

def_prop_item: def_prop | def_slot;

// name : Type [= literal]
def_prop: ID DELIM ID def_prop_default?;

// out :: Type   (the identifier must be 'out' — enforced at walk time, HED5016)
def_slot: ID DEF_TYPE ID;

def_prop_default: ASSIGN def_literal;

def_literal:
    OP_MINUS? (INT_LIT | REAL_LIT)
    | STRING_LIT
    | CHAR_LIT
    | TRUE
    | FALSE
    | NULL
    ;
```

Parser design notes (normative):

1. **Alternative 5 is unreachable for every currently valid input.** It requires either
   a top-level `COMMA` (unlexable pre-phase-1, a guaranteed parse failure in
   alternatives 1–4 since) or a top-level `ID DELIM` pair, which alternatives 2–4 cannot
   consume to completion (see the coexistence table). Alternatives 1–4 keep their exact
   phase 1 text and order.
2. **The positional slot is `expr`.** The AST builder — not the grammar — classifies
   it (entry doc D4): a pure `PathNode` (no target; `ROOT_REF` allowed) fills
   `ModelParameter`/`RootReference` and compiles bit-identically to alternative 2
   (member tier, dynamic scopes, definition monomorphisation all preserved); a
   `ThisNode` maps to the empty model parameter; anything else becomes
   `NativeExpression`. This avoids duplicating `member_expression` in a second
   position, where it would be a strict prefix of `expr` and force useless prediction
   work.
3. **The named-argument `:` cannot collide.** `DELIM` is consumed in exactly four
   parser positions after this phase: the chain separator (only after a completed
   `call`, i.e. after `)`), the ternary arm separator (only after `OP_QUESTION` inside
   `expr`), `def_base` (only between the header `ID` and `DEF_ENDNAME` in `DEF` mode),
   and `named_argument` (only after a bare `ID` in argument position). A bare
   identifier cannot start a chain (every chain element requires parens), and a ternary
   `:` is unreachable without its `?` — the roadmap's constraint 3 analysis holds and
   is pinned by corpus rows PP06/PP13/PP14.
4. **`def_prop` vs `def_slot` need one token of lookahead past the identifier**
   (`DELIM` vs `DEF_TYPE` — distinct tokens, `::` wins maximal munch over `:`), so the
   decision is trivially SLL-decidable.
5. **`def_literal` is deliberately not `expr`.** Defaults are literals by design
   (entry doc D2); an identifier or operator after `=` is a parse error (`HED0003`),
   pinned by corpus rows NP09/NP10.
6. **Walk-time (not grammar) checks**, for better positions/messages: `def_slot`'s
   identifier must be `out` (`HED5016`); at most one `def_slot` per header
   (`HED5017`); no duplicate prop names per header (`HED5007`); reserved prop names
   (`HED5015`). The grammar accepts these shapes so the errors are positioned semantic
   diagnostics rather than opaque syntax errors.

## Coexistence and ambiguity analysis

ALL(\*) resolves true ambiguities "in favor of the production with the lowest number"
([ALL(\*) tech report](https://www.antlr.org/papers/allstar-techreport.pdf)); ordering
plus mode isolation is the back-compat mechanism. Prediction outcomes to pin:

| Input (parameter text) | Winning alternative | Why |
| --- | --- | --- |
| *(empty)*, `Name`, `A.B.C`, `::Total` | 2 — member path | Unchanged; alternative 5 requires a `DELIM` or `COMMA` it never sees. |
| `@expr` (inner `@`) | 1 — C# expression | `CSHARP_START` occurs only there. |
| `fn(Name)`, `a():b()`, `(A)` | 3 — nested chain | Unchanged (the phase 1 alt 3/alt 4 order rule). |
| `Count > 0`, `max(A, B)`, `true` | 4 — native expression | Unchanged from phase 1. |
| `this` | 4 — native expression (`ThisExpr`) | New token; alternatives 2/3 cannot consume `THIS`. |
| `style: "wide"` | 5 — named arguments | Alt 2 stops at `DELIM`; alt 3 needs parens on `style`; alt 4's `expr` cannot consume a top-level `DELIM`. Only alt 5 completes. |
| `Article, style: "wide"` | 5 — named arguments | No other alternative can consume a top-level `COMMA`. |
| `cond ? a : b` | 4 — native expression | Not alt 5: the input starts `ID OP_QUESTION`, and `named_argument` demands `ID DELIM` at the top level. The ternary `DELIM` is consumed inside `expr`. |
| `Price * 2, style: "x"` | 5 | The positional `expr` seat takes the native expression; the `COMMA` forces alt 5. |

Prediction-behavior notes:

- **No new true ambiguity.** Alternative 5 completes only on inputs containing a
  top-level `COMMA` or a top-level `ID DELIM` prefix followed by a value — both parse
  failures in every earlier alternative. The only genuinely ambiguous pair remains
  phase 1's alt 3/alt 4 overlap, resolved by order exactly as before. Gate: the
  editor-mode `LL_EXACT_AMBIG_DETECTION` sweep (the mode
  [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) already uses for
  tooling) runs over the **entire existing fixture corpus plus this phase's corpus**
  at WI2 exit and must report no ambiguity beyond the known alt 3/alt 4 pair.
- **SLL stability.** `DocumentParser` parses SLL-first with LL fallback. For every
  pre-phase-5 input, the token stream is unchanged (mode isolation; `THIS` only fires
  on the previously-keyword-free `this`+boundary sequence in `CALL`, which previously
  lexed as `ID` — see the back-compat row below) and the added alternative only extends
  the prediction DFA with paths gated on tokens those inputs do not contain — so SLL
  outcomes for the existing corpus are unchanged. Cost is guarded by phase 1's
  `TemplateParseBenchmarks` before/after run (regression-gate rule 4).
- ⚠ **One lexing change inside `CALL` is deliberate:** the identifier `this` inside
  call parens previously lexed as `ID` (a member path — `@(this)` compiled as a lookup
  of a property named `this`, which **cannot exist** in C#, so it always failed
  compilation on typed scopes with `HED0001`, or failed at render on dynamic scopes).
  It now lexes as `THIS` with defined semantics — error → works, the same class as
  phase 1's `@out(true)` row. Pinned by corpus row PP10 and the release note.
- **`DEF_PROPS` adds zero prediction load to existing templates**: its tokens appear in
  no existing stream (note 2 above), and the `def` rule's new optional element is
  decided by a single token (`OUT_PARAMSTART` vs `DELIM`/`DEF_ENDNAME`).
- **`CALL_RETURNED` chaining is untouched**: the `)` closing a named-argument call pops
  to `CALL_RETURNED` exactly as today, so `:`-chains and `{{` bodies after the call are
  unchanged (row PP14).

## Editor-token classification

No `HeddleTokenType` additions (entry doc D18). Emission through the existing
`ParseContext.AddToken` gate, active only under `ProvideLanguageFeatures`:

| Terminal | `HeddleTokenType` |
| --- | --- |
| `named_argument`'s `ID` (the prop name) | `Id` |
| `named_argument`'s `DELIM` | `Delim` |
| named-argument value tokens | per the [phase 1 classification](../phase-1-native-expressions/grammar.md#editor-token-classification) |
| `THIS` | `Literal` (the keyword-literal class: `true`/`false`/`null`) |
| `def_props` `OUT_PARAMSTART`/`OUT_PARAMEND` | `OutParamStart` / `OutParamEnd` |
| `def_prop` name `ID` and type `ID`; `def_slot` `ID`s | `Id` |
| `def_prop`'s `DELIM` | `Delim` |
| `def_slot`'s `DEF_TYPE` | `DefType` (the existing `::` class) |
| `ASSIGN`, `COMMA`, `OP_MINUS` in `def_props` | `Operator` |
| `def_literal` literal tokens | `Literal` |

Keyword and `ID` tokens include their trailing whitespace in the token span (the
`WS*`/`SINGLE_LINE_WS*` in the rules), matching existing reporting.

## Regeneration procedure

1. Edit `HeddleLexer.g4` and `HeddleParser.g4` exactly as above. `CSharp.g4` is not
   modified.
2. Run [generate_cs.cmd](../../../src/Heddle.Language/generate_cs.cmd) (ANTLR
   4.13.1-complete.jar; downloaded on first run). Regenerate the JS grammar
   (`generate_js.cmd`) in the same commit so the in-browser parser stays in sync.
3. Commit the `.g4` diffs together with everything under
   `src/Heddle.Language/generated/` as **one dedicated regen commit** containing no
   other changes — the phases-1-and-5 rule of the
   [testing standards](../common/testing-standards.md#regression-gates), diff-reviewed.
4. Generated code is never hand-edited
   ([coding standards](../common/coding-standards.md#repository-style-inferred-preserved)).

## Parse corpus (executable grammar spec)

The corpus lives as xUnit `[Theory]` data in `PropsParseTests`
(`src/Heddle.Tests/PropsParseTests.cs`); every ANTLR regen is validated against it.
Definition-side entries are full `@% … %@` templates (bodies shortened to `{{x}}`);
call-side entries assume the referenced definition is declared in the same template.
Assertions are the resulting DTO shapes (`CallParameter` classification,
`PropArguments`, `PropDeclarations`, `SlotTypeName`) and, for expressions, the AST via
the public `ExprNode` hierarchy. Entries marked *error* assert a positioned `HED0003`
syntax error and `Success == false`.

Positive entries:

| # | Template (core) | Expected |
| --- | --- | --- |
| PP01 | `@card(Article, style: "wide", compact: true)` | Alt 5; positional → member shape (`ModelParameter = ["Article"]`); `PropArguments = [style: LiteralNode("wide"), compact: LiteralNode(true)]` |
| PP02 | `@card(Article)` | Alt 2 unchanged (`PropArguments == null`) — the all-defaults binding row |
| PP03 | `@card(style: "wide")` | Alt 5, no positional → empty model parameter (current-model passthrough) |
| PP04 | `@card(Article, style: ::Site.DefaultCardStyle)` | Named value `PathNode{RootRef}[Site, DefaultCardStyle]` |
| PP05 | `@card(Article, compact: !Hidden)` | Named value `UnaryNode(Not, PathNode[Hidden])` |
| PP06 | `@card(Article, style: Featured ? "wide" : "plain")` | Ternary value — its `DELIM` consumed inside `expr`, not as an argument separator |
| PP07 | `@card(Article, style: upper(Kind))` | Named value `CallNode("upper", …)` |
| PP08 | `@card(Price * 2, style: "x")` | Positional classified `NativeExpression` (`BinaryNode`), one named argument |
| PP09 | `@card(this, style: "x")` | Positional `ThisNode` → empty model parameter |
| PP10 | `@out(this)` | Alt 4; `ThisExpr` (was: member path of a property named `this` that could never resolve — error → works, release-noted) |
| PP11 | `@out(this.Name)` | `PathNode{Target = ThisNode}[Name]` |
| PP12 | `@(len(this))` | `this` as a function argument |
| PP13 | `@out(a():b())` | Alt 3 nested chain — **unchanged** (the roadmap's "chain still works" validation row) |
| PP14 | `@card(Article, style: "wide"):html()` | Named-args call followed by a chain — `CALL_RETURNED` untouched |
| PP15 | `<card(style: string = "plain", compact: bool = false)>{{x}} :: Article` | Two `PropDeclaration`s with decoded defaults `"plain"`/`false` |
| PP16 | `<picker(out:: MenuOption)>{{x}} :: Menu` | `SlotTypeName = "MenuOption"`, no props |
| PP17 | `<combo(style: string = "plain", out:: Option)>{{x}}` | One prop + slot in one list |
| PP18 | `<fancy(tone: string = "info"):card>{{x}}` | Props coexist with `def_base` (`:card` follows the `)`) |
| PP19 | `<grid(items: System.Collections.Generic.List<string>)>{{x}}` | Dotted generic type name as one `ID` token |
| PP20 | `<pad(width: int = -4)>{{x}}` | Signed numeric default (`OP_MINUS INT_LIT`) |
| PP21 | `<flag(active: bool = true, label: string)>{{x}}` | Defaulted prop may precede a required one — declaration order is free |
| PP22 | `<card()>{{x}}` | Empty prop list — legal, `PropDeclarations` empty |
| PP23 | `<card(style: string)> -> (Article, style: "wide") {{x}}` | Named arguments inside a default-output chain (alt 5 via `default_chain`) |
| PP24 | `<note(tags: string[])>{{x}}` | Array type name (`ID_TYPE`'s `[]*`) |

Negative entries (all: positioned `HED0003`, never an exception):

| # | Template (core) | Rejected construct |
| --- | --- | --- |
| NP01 | `@card(Article, style: @ Model.X )` | C# tier as a named-argument value — the `@` enters `CS` mode, whose token stream cannot begin `expr` (the D4 correction's executable pin) |
| NP02 | `@card(Article, style: a():b())` | Nested chain as a named-argument value (values are expressions, phase 1 note 5 carried) |
| NP03 | `@card(A, B)` | Second positional argument |
| NP04 | `@card(style: "x", Article)` | Positional after named |
| NP05 | `@card(Article, style:)` | Missing value |
| NP06 | `@card(Article, : "x")` | Missing name |
| NP07 | `@card(Article,, style: "x")` | Empty argument slot |
| NP08 | `<card(style string)>` | Missing `:` between name and type |
| NP09 | `<card(style: string = Name)>` | Identifier as default (defaults are literals, D2) |
| NP10 | `<card(style: string = 1 + 2)>` | Expression as default |
| NP11 | `<card(style: string,)>` | Dangling comma in the prop list |
| NP12 | `@card(Article, this: 1)` | `this` as an argument name (`THIS` token where `named_argument` needs `ID`) |
| NP13 | `<card(true: bool)>` | Keyword as a prop name (`TRUE` token in `DEF_PROPS`) |
| NP14 | `<card(style: string = -"x")>` | Sign on a non-numeric literal |
| NP15 | `@card(Article, style: "a" style: "b")` | Missing comma between named arguments |

Shapes that **parse** but are compile-time diagnostics (owned by
`PropsInheritanceTests`/`SlotProjectionTests`, listed here so the corpus boundary is
explicit): `<card(out: Option)>` → `HED5015`; `<card(foo:: Option)>` → `HED5016`;
`<card(a: int, a: int)>` → `HED5007`; `<x(out:: A, out:: B)>` → `HED5017`;
`@out(X)` outside a slot definition → `HED5012`.

Corpus maintenance rule: a grammar change that alters any row is a spec change and goes
through this document first
([testing standards](../common/testing-standards.md#fixtures-and-goldens)).

## External grounding for this document

- [ANTLR lexer rules, modes, and commands](https://github.com/antlr/antlr4/blob/master/doc/lexer-rules.md) —
  mode isolation ("the lexer can only return tokens matched by entering a rule in the
  current mode"), `pushMode`/`popMode`/`type()`/`channel()`. Re-verified July 2026 for
  the `DEF_PROPS` additive argument.
- [ANTLR maximal munch](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md) and
  [rule-order tie-break](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md) —
  the keyword-`WS*` length-tie technique (`CALL_THIS`, `DEFP_TRUE`/`FALSE`/`NULL`).
- [ALL(\*) tech report](https://www.antlr.org/papers/allstar-techreport.pdf) —
  first-alternative-wins; the alternative-5 ordering argument.
- [Phase 1 grammar specification](../phase-1-native-expressions/grammar.md) — the CALL
  final state, the `expr` rule this document extends, and the corpus conventions.
