# Phase 1 grammar specification — native expressions

Supplementary document of the [phase 1 spec](README.md). It contains the complete lexer
and parser rule changes for the native-expression tier, the coexistence analysis against
the three existing `call` alternatives, the editor-token classification, the regeneration
procedure, and the parse corpus that serves as the grammar's executable spec. Everything
here is normative; the entry document's implementation plan references these rules by
section.

## Verified current state

- [HeddleLexer.g4](../../../src/Heddle.Language/HeddleLexer.g4) — `mode CALL` currently
  contains exactly eight rules: `CALL_COMMENT`, `CSHARP_START` (`@` → `CS`),
  `CALL_PARAMSTART` (`(` → push `CALL`), `CALL_PARAMEND` (`)` → pop), `CALL_DELIM` (`:` →
  `DELIM`), `CALL_ROOT_REF` (`::` → `ROOT_REF`), `CALL_ID` (`ID_TOKEN WS*` → `ID`),
  `CALL_MEMB_P` (`.` → `MEMBER_P`), and `CALL_WS` (`WS+` → hidden channel). Every
  character this phase adds a token for is today a token-recognition error in `CALL`
  mode — no currently valid template contains one inside call parentheses.
- [HeddleParser.g4](../../../src/Heddle.Language/HeddleParser.g4) — the `call` rule has
  exactly the three alternatives the roadmap describes (C# expression, member path /
  empty, nested chain), in that order.
- [CSharp.g4](../../../src/Heddle.Language/CSharp.g4) provides the fragments this spec
  reuses: `INT` (decimal/hex/binary, digit separators, integer suffixes), `REAL`
  (`f`/`d`/`m` suffixes, exponents), `CHAR` (with the full escape set),
  `REGULAR_STRING_LITERALS` (regular-string body with escapes), `IDENTIFIER`,
  `WHITESPACE`.
- Whitespace inside `CALL` mode goes to the hidden channel (`CALL_WS`), so the expression
  parser rules below never need explicit `WS` handling.
- Generated code lives in `src/Heddle.Language/generated/` and is produced by
  [generate_cs.cmd](../../../src/Heddle.Language/generate_cs.cmd) (ANTLR 4.13.1,
  `-package Heddle.Language`); the runtime is `Antlr4.Runtime.Standard` 4.13.1.

## Token vocabulary additions

The final state of the `tokens { … }` declaration at the top of `HeddleLexer.g4` (the
parser references these via `tokenVocab=HeddleLexer`; the existing entries below are the
verified current list, order preserved):

```antlr
tokens {
    // existing entries — unchanged, verified against HeddleLexer.g4 (July 2026):
    TEXT, WS, IMPORT_TOKEN, ID, ROOT_REF, MEMBER_P, OUT, SUB_START, SUB_CLOSE,
    CSHARP_END, CSHARP_TOKEN, CSHARP_START, DEF_STARTNAME, DEF_ENDNAME, DELIM,
    DEF_START, DEF_CLOSE, RAW, OUT_PARAMSTART, OUT_PARAMEND, DEF_OUT,
    // NEW:
    TRUE, FALSE, NULL, INT_LIT, REAL_LIT, STRING_LIT, CHAR_LIT,
    OP_QQ, OP_QUESTION, OP_AND, OP_OR, OP_EQ, OP_NEQ,
    OP_LSHIFT, OP_RSHIFT, OP_LE, OP_GE, OP_LT, OP_GT,
    OP_PLUS, OP_MINUS, OP_STAR, OP_SLASH, OP_PERCENT,
    OP_AMP, OP_PIPE, OP_CARET, OP_NOT, OP_TILDE,
    LBRACKET, RBRACKET, COMMA
}
```

No existing token type is renamed or renumbered; the additions are appended.

## Lexer changes — `mode CALL` only

The final state of `mode CALL` (rules marked `NEW`; all existing rules keep their exact
current text and relative order). `CS`, `CS_NESTED`, `CALL_RETURNED`, `OUT_MODE`, and all
other modes are untouched.

```antlr
mode CALL;

CALL_COMMENT:
	COMMENT_BLOCK -> channel(HIDDEN);

CSHARP_START:
	OUT_ST -> popMode, pushMode(CS);

CALL_PARAMSTART: 
	PARA_ST -> type(OUT_PARAMSTART), pushMode(CALL);

CALL_PARAMEND: 
	PARA_CL -> type(OUT_PARAMEND), popMode;

CALL_DELIM: 
	EXT_DELIM -> type(DELIM);

CALL_ROOT_REF: DEF_T -> type(ROOT_REF);

// NEW — keyword literals. They carry the same trailing WS* as CALL_ID so that maximal
// munch cannot prefer CALL_ID for 'true ' (5 chars incl. the space) over a bare 'true'
// (4 chars); with equal lengths the rule-order tie-break applies and these win.
CALL_TRUE:  'true'  WS* -> type(TRUE);
CALL_FALSE: 'false' WS* -> type(FALSE);
CALL_NULL:  'null'  WS* -> type(NULL);

// NEW — numeric/string/char literals (fragments from CSharp.g4). REAL before INT is
// cosmetic (maximal munch already prefers the longer match, e.g. '1.5' over '1').
CALL_REAL:   REAL -> type(REAL_LIT);
CALL_INT:    INT  -> type(INT_LIT);
CALL_STRING: '"' REGULAR_STRING_LITERALS? '"' -> type(STRING_LIT);
CALL_CHAR:   CHAR -> type(CHAR_LIT);

CALL_ID: ID_TOKEN WS* -> type(ID);

CALL_MEMB_P: MEMB_P -> type(MEMBER_P);

// NEW — operators. Multi-character rules listed before their single-character prefixes
// for readability; maximal munch guarantees the longer match regardless of order.
CALL_OP_QQ:       '??' -> type(OP_QQ);
CALL_OP_QUESTION: '?'  -> type(OP_QUESTION);
CALL_OP_AND:      '&&' -> type(OP_AND);
CALL_OP_OR:       '||' -> type(OP_OR);
CALL_OP_EQ:       '==' -> type(OP_EQ);
CALL_OP_NEQ:      '!=' -> type(OP_NEQ);
CALL_OP_LSHIFT:   '<<' -> type(OP_LSHIFT);
CALL_OP_RSHIFT:   '>>' -> type(OP_RSHIFT);
CALL_OP_LE:       '<=' -> type(OP_LE);
CALL_OP_GE:       '>=' -> type(OP_GE);
CALL_OP_LT:       '<'  -> type(OP_LT);
CALL_OP_GT:       '>'  -> type(OP_GT);
CALL_OP_PLUS:     '+'  -> type(OP_PLUS);
CALL_OP_MINUS:    '-'  -> type(OP_MINUS);
CALL_OP_STAR:     '*'  -> type(OP_STAR);
CALL_OP_SLASH:    '/'  -> type(OP_SLASH);
CALL_OP_PERCENT:  '%'  -> type(OP_PERCENT);
CALL_OP_AMP:      '&'  -> type(OP_AMP);
CALL_OP_PIPE:     '|'  -> type(OP_PIPE);
CALL_OP_CARET:    '^'  -> type(OP_CARET);
CALL_OP_NOT:      '!'  -> type(OP_NOT);
CALL_OP_TILDE:    '~'  -> type(OP_TILDE);
CALL_LBRACKET:    '['  -> type(LBRACKET);
CALL_RBRACKET:    ']'  -> type(RBRACKET);
CALL_COMMA:       ','  -> type(COMMA);

CALL_WS: WS+ -> channel(HIDDEN);
```

Lexer design notes (each is normative):

1. **Keyword literals must carry `WS*`.** The roadmap's statement "keyword rules placed
   before `CALL_ID`" is necessary but not sufficient: `CALL_ID` is `ID_TOKEN WS*`, so for
   the input `true )` it matches five characters while a bare `'true'` rule matches four —
   maximal munch would pick `CALL_ID` and the keyword would never surface. Defining the
   keyword rules as `'true' WS*` makes the match lengths equal, and ANTLR's rule-order
   tie-break ("ANTLR gives precedence to the lexical rules specified first") then selects
   the keyword. This is a verified correction to the roadmap recorded as
   [D3 in the entry document](README.md#design-decisions).
2. **Longer identifiers still win.** `truely` matches six characters via `CALL_ID` versus
   four via `CALL_TRUE`, so maximal munch keeps it an `ID`. Keywords are case-sensitive:
   `True` is an `ID`.
3. **Parentheses reuse the existing tokens.** A grouping `(` inside an expression pushes
   another `CALL` mode and its `)` pops it — the push/pop pairs stay balanced exactly as
   they do for the nested-chain alternative today, and both are typed
   `OUT_PARAMSTART`/`OUT_PARAMEND`. Only the outermost `)` (the one whose pop returns to
   `CALL_RETURNED`) terminates the call. No new paren tokens exist.
4. **`:` stays `DELIM`, `::` stays `ROOT_REF`, `.` stays `MEMBER_P`** — the expression
   parser rules reuse them (ternary consumes `DELIM` only after `OP_QUESTION`).
5. **String forms.** Only the regular string literal (`"…"` with the standard C# escape
   set) is lexed. Verbatim (`@"…"`), interpolated (`$"…"`), and raw (`"""…"""`) strings
   are not: `@` is the C#-tier escape in `CALL` mode (`CSHARP_START`) and must stay so,
   and `$`/bare `"""` remain token errors. The custom `CALL_STRING` rule deliberately
   omits CSharp.g4's `UTF8_SUFFIX` so `"…"u8` does not lex as a native literal. See
   [D4](README.md#design-decisions).
6. **Numeric literals** accept everything the CSharp.g4 fragments accept: `0x`/`0X` hex,
   `0b`/`0B` binary, `_` digit separators, integer suffixes (`u`, `l`, `ul` in any
   case/order), real suffixes `f`/`d`/`m` and exponents. Their mapping to CLR types is
   pinned in [operator-semantics.md — literal typing](operator-semantics.md#literal-typing).
7. **No assignment tokens.** Every excluded construct fails in one of two ways, and both
   surface as a positioned `HED0003` syntax error (the error listener is attached to the
   parser only — verified in [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs) —
   so a CALL-mode token-recognition error is reported by the lexer and the parse then
   fails on the gap):
   - `=` and `$` have no CALL-mode token, so every construct containing them dies at
     that character: assignment `=`, the compound assignments `+=` `-=` `*=` `/=` `%=`
     `&=` `|=` `^=` `<<=` `>>=` `??=`, lambdas (`=>`), and interpolated strings
     (`$"…"`). The operator prefix lexes as its own token first under maximal munch
     (the `+` of `+=` becomes `OP_PLUS`), and the `=` that follows is the unrecognized
     character.
   - Constructs whose characters all lex but match no parser rule fail in the parser:
     `++`/`--` (two `OP_PLUS`/`OP_MINUS` tokens), `?.` (`OP_QUESTION` + `MEMBER_P`),
     cast syntax (`(Foo) Bar` — paren group then a stray `ID`), `is`/`as`/`new`
     (adjacent `ID`s), raw strings (`"""x"""` lexes as adjacent `STRING_LIT`s), and
     `"…"u8` (`STRING_LIT` + `ID`).

   The parse corpus pins every case (N01–N14); the sandbox-negative suite re-asserts the
   security-relevant ones.

## Parser changes

The `call` rule gains a fourth alternative, ordered **last** so ALL(\*)
first-alternative-wins preserves every existing parse:

```antlr
call: 
    extension_id? OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
    | extension_id? OUT_PARAMSTART WS* member_expression? OUT_PARAMEND
    | extension_id? OUT_PARAMSTART chain OUT_PARAMEND
    | extension_id? OUT_PARAMSTART native_expression OUT_PARAMEND
    ;
```

New rules (appended after `csharp_expression`):

```antlr
native_expression: expr;

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
    | ROOT_REF? ID                                                 # PathRootExpr
    ;

arg_list: OUT_PARAMSTART (expr (COMMA expr)*)? OUT_PARAMEND;

literal: INT_LIT | REAL_LIT | STRING_LIT | CHAR_LIT | TRUE | FALSE | NULL;
```

Parser design notes (normative):

1. **Precedence by alternative order.** ANTLR rewrites the direct left recursion into a
   precedence-climbing form in which the alternative listed first binds tightest; this
   rewrite is performed by the tool itself and is target-language independent, so it
   applies unchanged to the C# target
   ([ANTLR left-recursion doc](https://github.com/antlr/antlr4/blob/master/doc/left-recursion.md)).
   The order above reproduces the C# precedence table exactly: postfix (member, method,
   indexer) → unary → multiplicative → additive → shift → relational → equality → `&` →
   `^` → `|` → `&&` → `||` → `??` → `?:`. The four primary alternatives
   (`FunctionCallExpr`, `GroupExpr`, `LiteralExpr`, `PathRootExpr`) are non-left-recursive
   seeds; their position after the operators does not affect operator precedence.
2. **Right associativity.** `??` and `?:` carry `<assoc=right>` — ANTLR's left-recursive
   alternatives are left-associative by default and the option is declared per
   alternative (reference shape: `<assoc=right> e '?' e ':' e`). Without it,
   `a ?? b ?? c` would group `(a ?? b) ?? c`, which changes the static result type in
   mixed-nullability chains.
3. **Ternary reuses `DELIM`.** Within `expr`, `DELIM` (`:`) is consumed only in
   `TernaryExpr`, after an `OP_QUESTION` — so `:` remains unambiguous versus the chain
   delimiter. A chain can only appear via alternative 3 of `call`, which is ordered before
   alternative 4, so every currently valid `a():b()` parameter parses exactly as today.
4. **`MethodCallExpr` is parsed but always compile-rejected** (`HED1003`) — the alternative
   exists purely to convert "method call in a template expression" from an opaque parse
   error into a targeted diagnostic that names the remedy. It precedes `MemberHopExpr` so
   `x.Foo()` predicts the method-call shape.
5. **`FunctionCallExpr` takes expression arguments only.** Nested chains are not valid
   function arguments (`max(a():b(), 1)` is a parse error); the chain alternative of
   `call` is unreachable from inside `expr` by construction.
6. **Indexer arguments** allow the comma list inside one bracket pair
   (`Matrix[1, 2]` for multi-dimensional arrays / multi-parameter indexers).

## Coexistence analysis (alternative selection per input)

ALL(\*) resolves ambiguities "in favor of the production with the lowest number", which
makes the ordering below the back-compat mechanism
([ALL(\*) tech report](https://www.antlr.org/papers/allstar-techreport.pdf)). The
prediction outcomes to pin in the parse corpus:

| Input (parameter text) | Winning alternative | Why |
| --- | --- | --- |
| *(empty)*, `Name`, `A.B.C`, `::Total` | 2 — member path | Matches completely; alternatives are order-preferring. |
| `@expr` (inner `@`) | 1 — C# expression | `CSHARP_START` token only occurs there. |
| `fn(Name)`, `a():b()`, `(A)` | 3 — nested chain | Chain matches completely; genuinely ambiguous with alternative 4, resolved by order — **all currently valid input parses as today**. |
| `Count > 0`, `Price * Quantity`, `Name ?? "anon"` | 4 — native expression | Alternatives 2/3 cannot consume the operator token. |
| `max(A, B)`, `fn("x")`, `upper(Name) == X` | 4 — native expression | `COMMA`/literal/operator tokens are unreachable in alternatives 2/3. |
| `true`, `"text"`, `42` | 4 — native expression | Literal tokens no longer lex as `ID` (was: `@out(true)` = member path that failed compile on typed scopes, or a render-time `RuntimeBinderException` on dynamic scopes — see the entry document's back-compat section). |

Prediction-behavior notes:

- The alternative 3 / alternative 4 overlap on inputs like `fn(Name)` is a *true*
  ambiguity. In the default render path (`PredictionMode.SLL`, no `BailErrorStrategy` —
  verified in [DocumentParser](../../../src/Heddle/Language/DocumentParser.cs)) it is
  resolved silently in favor of alternative 3. In editor mode
  (`LL_EXACT_AMBIG_DETECTION`) ambiguity reports go to
  [HeddleSyntaxErrorListener](../../../src/Heddle/Language/HeddleSyntaxErrorListener.cs),
  which derives from `BaseErrorListener` and does not override `ReportAmbiguity` — so no
  spurious editor errors appear. Both behaviors are pinned by tests.
- The added prediction work applies to every call in every template, which is why the
  entry document's testing plan requires the `TemplateParseBenchmarks` before/after run
  on the existing fixture corpus (regression-gate rule 4 of the
  [testing standards](../common/testing-standards.md#regression-gates)).

## Editor-token classification

Three values are **appended** to `HeddleTokenType`
([HeddleToken.cs](../../../src/Heddle/Language/HeddleToken.cs)) — appending preserves the
numeric values of all existing members:

```csharp
Operator,       // every OP_* terminal, LBRACKET, RBRACKET, COMMA, and the ternary's OP_QUESTION/DELIM pair
Literal,        // INT_LIT, REAL_LIT, STRING_LIT, CHAR_LIT, TRUE, FALSE, NULL
FunctionName    // the ID of FunctionCallExpr and the ID of MethodCallExpr
```

Classification (emitted by `ExpressionAstBuilder` through `ParseContext.AddToken`, active
only when `ProvideLanguageFeatures` is set, exactly like the existing emission):

| Terminal in `expr` | `HeddleTokenType` |
| --- | --- |
| `ID` of `PathRootExpr` and `MemberHopExpr` | `Id` (existing) |
| `MEMBER_P` | `MemberSelector` (existing) |
| `ROOT_REF` | `RootReference` (existing) |
| `OUT_PARAMSTART` / `OUT_PARAMEND` of `GroupExpr` and `arg_list` | `OutParamStart` / `OutParamEnd` (existing) |
| `ID` of `FunctionCallExpr` / `MethodCallExpr` | `FunctionName` (new) |
| all literal terminals | `Literal` (new) |
| all operator terminals (incl. `OP_QUESTION` and the ternary `DELIM`) | `Operator` (new) |

Keyword-literal and `ID` tokens include their trailing whitespace in the token span (the
`WS*` in the rule), matching how `ID` tokens are already reported today.

## Regeneration procedure

1. Edit `HeddleLexer.g4` and `HeddleParser.g4` exactly as above. `CSharp.g4` is not
   modified.
2. Run [generate_cs.cmd](../../../src/Heddle.Language/generate_cs.cmd) (ANTLR
   4.13.1-complete.jar; the script downloads it on first run). The JS grammar build
   (`generate_js.cmd`) is regenerated in the same commit so the in-browser
   parser stays in sync with the C# one.
3. Commit the `.g4` diffs together with everything under
   `src/Heddle.Language/generated/` as **one dedicated regen commit** containing no other
   changes, per the [testing standards](../common/testing-standards.md#regression-gates)
   (phases 1 and 5 commit exactly one regen and diff-review it).
4. Generated code is never hand-edited
   ([coding standards](../common/coding-standards.md#repository-style-inferred-preserved)).

## Parse corpus (executable grammar spec)

The corpus lives as xUnit `[Theory]` data in `NativeExpressionParseTests`
(`src/Heddle.Tests/NativeExpressionParseTests.cs`); every ANTLR regen is validated
against it. Each entry is a full template string; the assertion is the resulting
`CallParameter` classification and, for expressions, the AST shape (via the public
`ExprNode` hierarchy). Entries marked *error* assert a positioned `HED0003` syntax error
and `Success == false`.

Positive entries:

| # | Template | Expected |
| --- | --- | --- |
| P01 | `@(Name)` | Member path (`ModelParameter = ["Name"]`, `NativeExpression == null`) — alternative 2 unchanged |
| P02 | `@out(a():b())` | Nested chain (`ChainParameter != null`) — alternative 3 unchanged |
| P03 | `@( @Model.X )` | C# expression (`CSharpExpression != null`) — alternative 1 unchanged |
| P04 | `@if(Count > 0){{x}}` | `BinaryNode(GreaterThan, PathNode[Count], LiteralNode(0))` |
| P05 | `@(Price * Quantity)` | `BinaryNode(Multiply, PathNode[Price], PathNode[Quantity])` |
| P06 | `@(Name ?? "anon")` | `BinaryNode(Coalesce, PathNode[Name], LiteralNode("anon"))` |
| P07 | `@(IsFeatured ? "★" : "")` | `TernaryNode(PathNode[IsFeatured], "★", "")` |
| P08 | `@(A + B * C == D && !E)` | precedence: `AndAlso(Equal(Add(A, Multiply(B, C)), D), Not(E))` |
| P09 | `@(a ?? b ?? c)` | right-assoc: `Coalesce(a, Coalesce(b, c))` |
| P10 | `@(a ? b : c ? d : e)` | right-assoc: `Ternary(a, b, Ternary(c, d, e))` |
| P11 | `@((A + B) * C)` | grouping beats precedence: `Multiply(Add(A, B), C)` |
| P12 | `@(::Total - Amount)` | `Subtract(PathNode{RootRef}[Total], PathNode[Amount])` |
| P13 | `@(max(A, B))` | `CallNode("max", [PathNode[A], PathNode[B]])` |
| P14 | `@(upper(Name) == X)` | `Equal(CallNode("upper", [Name]), PathNode[X])` |
| P15 | `@(Items[0].Name)` | `PathNode{Target = IndexNode(PathNode[Items], [0])}[Name]` |
| P16 | `@(Matrix[1, 2])` | `IndexNode(PathNode[Matrix], [1, 2])` |
| P17 | `@(0x1F & Flags \| 1 << Bits)` | hex literal; `Or(And(31, Flags), LeftShift(1, Bits))` |
| P18 | `@(1_000 + 2L + 1.5f + 2.5m)` | separator/suffix typing per the literal table |
| P19 | `@('c' + Name)` | char literal node |
| P20 | `@out(true)` | `LiteralNode(true)` (was an error — release-noted) |
| P21 | `@(Name.ToUpper())` | `MethodCallNode` — parses; compile-rejected `HED1003` |
| P22 | `@(!A.B.C)` | unary over a three-segment path |
| P23 | `@( Price  *  Quantity )` | interior whitespace is hidden — same AST as P05 (note: `@(trim(Name))` is **not** an expression — it stays an alternative-3 chain whose registered-function fallback happens at the compile seam, entry document D11) |
| P24 | `@(A.B > 0 ? len(Name) : 0)` | composite: path, comparison, function, ternary |

Negative entries (all: positioned `HED0003`, never an exception):

| # | Template | Rejected construct |
| --- | --- | --- |
| N01 | `@(X = 1)` | assignment |
| N02 | `@(X += 1)` | compound assignment |
| N03 | `@(X++)` | increment |
| N04 | `@(Items.Where(i => i))` | lambda (`=>`) |
| N05 | `@((Foo) Bar)` | cast syntax |
| N06 | `@(x is string)` | `is` (two adjacent IDs) |
| N07 | `@(new Foo())` | `new` (two adjacent IDs) |
| N08 | `@(a?.b)` | `?.` — the error is a parse error; the docs page explains `.` hops are already null-safe |
| N09 | `@($"x{A}")` | interpolated string |
| N10 | `@("""raw""")` | raw string |
| N11 | `@(a ? b)` | ternary missing `:` |
| N12 | `@(1 +)` | dangling operator |
| N13 | `@(max(1, ))` | dangling comma |
| N14 | `@("abc"u8)` | UTF-8 string suffix |

Corpus maintenance rule: a grammar change that alters any row is a spec change and goes
through this document first ([testing standards](../common/testing-standards.md#fixtures-and-goldens)).

## External grounding for this document

- [ANTLR left-recursion](https://github.com/antlr/antlr4/blob/master/doc/left-recursion.md) —
  precedence by alternative order, per-alternative `<assoc=right>`, tool-side
  (target-independent) rewriting. Re-verified July 2026 for this spec.
- [ALL(\*) tech report](https://www.antlr.org/papers/allstar-techreport.pdf) —
  first-alternative-wins ambiguity resolution.
- [ANTLR lexer maximal munch](https://github.com/antlr/antlr4/blob/master/doc/wildcard.md)
  and [rule-order tie-break](https://github.com/antlr/antlr4/blob/master/doc/faq/lexical.md) —
  the basis of the keyword-`WS*` correction.
- [C# operators and expressions](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/) —
  the precedence order the `expr` rule reproduces.
