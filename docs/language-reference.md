# Language Reference (TTL)

This is the complete reference for the Templater template language. Every construct below is
grounded in the ANTLR grammar — [TtlLexer.g4](../src/Templates.Language/TtlLexer.g4) (tokens)
and [TtlParser.g4](../src/Templates.Language/TtlParser.g4) (rules) — and illustrated with
real examples from the fixtures in
[src/Templates.Tests/TestTemplate](../src/Templates.Tests/TestTemplate).

> The historical [develop.txt](../develop.txt) uses an obsolete syntax (`<% %>` and `[ ]`).
> The current delimiters are `@% %@` (definitions) and `{{ }}` (blocks). Ignore `develop.txt`
> as a syntax source.

## Table of contents

1. [Mental model](#mental-model)
2. [Symbol cheat sheet](#symbol-cheat-sheet)
3. [Text and the `@` escape](#text-and-the--escape)
4. [Output blocks](#output-blocks)
5. [Member expressions `@(A.B.C)`](#member-expressions-abc)
6. [Root reference `@(::Member)`](#root-reference-member)
7. [Embedded C# expressions](#embedded-c-expressions)
8. [Definitions `@% … %@`](#definitions---)
9. [Default output `-> chain`](#default-output---chain)
10. [Type annotation `:: Type`](#type-annotation--type)
11. [Inheritance and override `<child:base>`](#inheritance-and-override-childbase)
12. [Subtemplates `{{ … }}`](#subtemplates--)
13. [Chaining with `:`](#chaining-with-)
14. [Recursion](#recursion)
15. [Imports `@<<{{ … }}`](#imports--)
16. [Comments `@* … *@`](#comments---)
17. [Raw blocks `@{ … }@` and `@:`](#raw-blocks----and-)
18. [Whitespace trimming `@\`](#whitespace-trimming-)
19. [How the lexer reads a template (modes)](#how-the-lexer-reads-a-template-modes)
20. [Behavioral nuances summary](#behavioral-nuances-summary)

---

## Mental model

A TTL document ([`ttl`](../src/Templates.Language/TtlParser.g4) rule) is a sequence of:

- **text** — literal output, copied verbatim;
- **output blocks** — `@…` directives that call *extensions* and emit their results;
- **definitions** — `@% … %@` blocks that declare reusable named templates;
- **imports** — `@<<{{ path }}` that pull definitions in from another file;
- **raw blocks** — verbatim regions that are not parsed.

```antlr
ttl: (definition | import_block | outblock | raw | text)*;
```

Everything special starts with `@`. Plain text never needs escaping unless it contains `@`.

---

## Symbol cheat sheet

| Syntax | Name | Meaning |
| --- | --- | --- |
| `@(expr)` | Output expression | Emit a member value or C# expression. |
| `@name(param)` | Extension call | Invoke extension `name` (e.g. `@list`, `@if`). |
| `a:b:c` | Chain | Pipe one call's output into the next. |
| `{{ … }}` | Block / subtemplate | Body of a definition or the inline template for a call. |
| `@% … %@` | Definition block | Declare one or more named templates. |
| `<name>` | Definition name | Names a template inside `@% … %@`. |
| `<child:base>` | Inheritance | Define `child` based on `base`. |
| `-> chain` | Default output | What a definition emits when invoked with no body. |
| `:: Type` | Type annotation | Strongly types a definition's model. |
| `::Member` | Root reference | Read `Member` from the root model, not the current one. |
| `@<<{{ path }}` | Import | Include definitions from another template file. |
| `@* … *@` | Comment | Ignored; never emitted. |
| `@{ … }@` | Raw block | Emitted verbatim; not parsed. |
| `@: …` | Raw line | Rest of the line emitted verbatim. |
| `@\` | Whitespace trim | Eat the following run of whitespace. |

---

## Text and the `@` escape

Any character that is not part of a directive is literal text and is emitted unchanged
(`TEXT` token, [TtlLexer.g4](../src/Templates.Language/TtlLexer.g4)). Unicode is fully
supported — fixtures contain text such as `<title>Привет!</title>`.

The only special character is **`@`**. To emit a literal `@`, double it: **`@@`**. In
[template.thtml](../src/Templates.Tests/TestTemplate/template.thtml) the sequence
`@@*Comment Test 8*@partial(...)` emits a literal `@` immediately followed by a comment and
then a `partial` call. To emit literal text that contains many `@`/braces, use a
[raw block](#raw-blocks----and-).

---

## Output blocks

An **output block** is `@` followed by a [chain](#chaining-with-) of one or more calls, with
an optional [subtemplate](#subtemplates--):

```antlr
outblock: OUT chain subtemplate?;
chain:    call (DELIM call)*;
call:     extension_id? OUT_PARAMSTART CSHARP_START csharp_expression OUT_PARAMEND
        | extension_id? OUT_PARAMSTART WS* member_expression? OUT_PARAMEND
        | extension_id? OUT_PARAMSTART chain OUT_PARAMEND ;
```

A **call** has three parts: an optional extension name, a parenthesized parameter, and an
optional `{{ … }}` body. The parameter is one of:

- **a member expression** — `@(Name)`, `@list(Products)`;
- **a C# expression** — introduced with an inner `@`, e.g. `@(@5)`, `@list(@model.Products.Where(p => p.Quantity < 95))`;
- **another chain** — nested calls;
- **empty** — `@out()`, `@(@null){{}}`, `@list(){{ … }}` — meaning "use the current context value".

When the extension name is omitted, the call uses the **empty extension**
(`@(...)` is `@` + an unnamed call). The empty/`html` extensions simply stringify the
current value — see [Built‑in Extensions](built-in-extensions.md#empty--unnamed).

Examples (from [template.thtml](../src/Templates.Tests/TestTemplate/template.thtml)):

```ttl
@(Name)                       @* member value *@
@(@"HTML ")                   @* C# string literal *@
@(@5)                         @* C# integer literal *@
@string(Text)                 @* call the "string" extension on member Text *@
@if(IsShow){{ <span>…</span> }}   @* call with a subtemplate body *@
```

---

## Member expressions `@(A.B.C)`

```antlr
member_expression: ROOT_REF? ID (MEMBER_P ID)*;
```

A member expression is a dotted path of identifiers resolved against the current model:

```ttl
@(Name)                @* current model's Name *@
@(ComplexObject.Data.Text)   @* nested property access (recursion.thtml) *@
```

Member access works on statically‑typed models (resolved by reflection) and on `dynamic`
models (resolved with the C# runtime binder). The "current model" is whatever value the
enclosing extension established — for example, inside `@list(Products){{ … }}` the current
model of the body is *one product*.

---

## Root reference `@(::Member)`

Prefix a member path with `::` to read from the **root** model — the value originally passed
to `Generate` — regardless of how deeply nested the current scope is.

From [recursion.thtml](../src/Templates.Tests/TestTemplate/recursion.thtml):

```ttl
@(@root.Count) C# Test          @* via embedded C# using the @root keyword *@
@(::Count) Model Access Test    @* via the :: root reference *@
```

`@(::Count)` and the embedded‑C# form `@(@root.Count)` reach the same root value; the `::`
form is pure template syntax and does not require `AllowCSharp`. Use it to reach top‑level
data (totals, page context, culture info) from inside loops and nested definitions, e.g.
`@(::ProductPage.DefaultCultureInfo.DateTimeFormat.ShortDatePattern)` in
[wierd-whitespace.thtml](../src/Templates.Tests/TestTemplate/wierd-whitespace.thtml).

---

## Embedded C# expressions

Inside a call's parentheses, an inner **`@`** switches the lexer into C# mode (`CS` mode,
[TtlLexer.g4](../src/Templates.Language/TtlLexer.g4)), so the rest of the parameter is parsed
as a real C# expression and compiled by Roslyn. This requires
`TemplateOptions.AllowCSharp = true`.

Supported forms include literals, member access, method calls, LINQ, lambdas, and object
initializers. Nested parentheses are handled (the lexer balances `(`/`)` within the
expression):

```ttl
@(@"Test".Length)                                   @* string literal + member (raw.thtml) *@
@(@5)                                               @* numeric literal *@
@list(@model.Products.Where(p => p.Quantity < 95))  @* LINQ + lambda (template.thtml) *@
@for(@new ForModel() { Last = model.Products.Count(), Step = 3 })   @* object initializer *@
@text(@new NameValuePair { Name = "input1-name", Value="input1-value" })
@if(@model.SubCategories.Count>0){{ … }}            @* boolean expression (recursion.thtml) *@
```

Two well‑known identifiers are available in embedded C#:

- **`model`** — the current model (e.g. `@model.Products`);
- **`root`** — the root model (e.g. `@(@root.Count)`), equivalent to the `::` reference.

There is also a `chained` value available to extensions such as `@for` (the loop index);
see [`Scope`](csharp-api.md#scope-the-data-view-during-rendering).

> **Parser nuance.** A C# expression that contains nested parentheses lexes the inner `)` as
> an `OUT_PARAMEND` token *inside* the expression (grammar rule
> `csharp_expression: (CSHARP_TOKEN | OUT_PARAMEND)+`). The named call form `@x(@Foo(1))` and
> the unnamed form `@(@Foo(1))` therefore classify those tokens identically — this is covered
> by the `NamedAndUnnamedCSharpCallsClassifyParenTokensEqually` regression test in
> [TtlTemplateTests.cs](../src/Templates.Tests/TtlTemplateTests.cs).

---

## Definitions `@% … %@`

A **definition block** declares one or more reusable named templates. Once declared, a
definition is invoked like any extension: `@name()`.

```antlr
definition: DEF_START def+ DEF_CLOSE;
def:        DEF_STARTNAME ID def_base? DEF_ENDNAME default_chain? subtemplate def_type?;
def_base:   DELIM ID;
def_type:   DEF_TYPE ID;
```

Anatomy of one definition:

```ttl
@%
  <text>                @* name, wrapped in < > *@
  {{                    @* body (subtemplate) *@
    <input type="text" name="@(Name)" value="@(Value)" @out() />
  }} :: dynamic         @* optional type annotation *@
%@
```

A single `@% … %@` block may declare many definitions back‑to‑back, as in
[template.thtml](../src/Templates.Tests/TestTemplate/template.thtml) which defines `text`,
`multi_text`, `labeled_text`, `test_list`, and more in one block. Definition blocks may
appear at the top level **or nested inside a subtemplate** — a call's body can introduce
local definitions before using them (see `multi_text` redefining itself inside its own body
in `template.thtml`).

Invoking a definition:

```ttl
@text(@new NameValuePair { Name = "input1-name", Value="input1-value" }){{ class="testcssclass1" }}
```

Here `@new NameValuePair {…}` is the model passed to `text`, and `{{ class="…" }}` is the
subtemplate that `text` exposes through `@out()`.

---

## Default output `-> chain`

A definition can specify a **default output chain** with `->`. This is the chain used to
select/produce the definition's body data, and it lets a definition be invoked with no
explicit parameter.

```antlr
default_chain: DEF_OUT chain;
```

```ttl
<default> -> (Model)
{{ Order #@(Id)! }} :: dynamic
```

`-> (Model)` means: when this definition runs, take `Model` off the incoming object as the
body's data. Several fixtures use the empty default chain `-> ()`, e.g. `<left> -> ()` in
[recursion.thtml](../src/Templates.Tests/TestTemplate/recursion.thtml) and `<default> -> ()`
in [vc-test.thtml](../src/Templates.Tests/TestTemplate/vc-test.thtml), meaning "use the
current value as‑is".

---

## Type annotation `:: Type`

A trailing `:: Type` strongly types a definition's model. The type name is resolved against
the namespaces brought in by [`@using()`](built-in-extensions.md#using) (and the model
type’s own assembly).

```ttl
<page>
{{ … }} :: ICollection<Product>          @* generic types are allowed *@

<text>
{{ … }} :: dynamic                       @* opt into dynamic dispatch *@

<list> -> ()
{{ … }} :: ICollection<Category>         @* recursion.thtml *@
```

`dynamic` makes member access late‑bound (resolved at render time via the C# runtime
binder), which is how `@(Name)` works against `ExpandoObject`/anonymous models. Concrete
types (e.g. `ICollection<Product>`) enable compile‑time member checking and faster access.

Generic and array type names are recognized by the lexer's type rule
(`ID_TYPE`, [TtlLexer.g4](../src/Templates.Language/TtlLexer.g4)), which accepts
`Namespace.Type<T1, T2>[]` forms.

---

## Inheritance and override `<child:base>`

A definition can inherit from another by name using `:`:

```antlr
def_base: DELIM ID;     @* the ':' before a base name *@
```

```ttl
<labeled_text:text>      @* labeled_text inherits text *@
{{
  <label>
    @text()              @* call the base definition from within the child *@
  </label>
}}
```

**Full override.** Re‑declaring an existing name as `<name:name>` replaces that definition
from that point onward. In [template.thtml](../src/Templates.Tests/TestTemplate/template.thtml):

```ttl
<text:text>
{{ Full override }}
```

After this declaration, later `@text()` calls render "Full override". Overrides are layered
in document order, which is what powers the multi‑pass layout output in
[vc-test.thtml](../src/Templates.Tests/TestTemplate/vc-test.thtml) (where `body`, `left`,
`center`, `right`, and `chef_videos` are progressively overridden between calls to
`@default()` and `@layout()`).

**Type compatibility rule.** If both a base and a child specify a type, the child's type
must be **assignable to** the base's type; otherwise compilation fails with
*"The {name} definition has incompatible type with base element. Should be assignable from
{baseType}"* (see the inheritance logic quoted in [develop.txt](../develop.txt) and
implemented in `DefinitionItem`). If only the base is typed, the child inherits that type;
a type, once specified anywhere in a base chain, cannot be changed to an incompatible one in
a child.

---

## Subtemplates `{{ … }}`

A `{{ … }}` block is a full nested template. It appears as a definition body and as the
inline body attached to a call.

```antlr
subtemplate: WS* SUB_START ttl SUB_CLOSE;
```

Because the body is itself a `ttl` rule, subtemplates may contain text, output blocks,
nested definitions, imports, and raw blocks — to any depth. A call exposes its subtemplate
to its extension; for example `@out()` renders the surrounding subtemplate, and
`@list(items){{ <li>@(Name)</li> }}` renders the `<li>` body once per element.

```ttl
@list(SubCategories)
{{
  <li>
    @(Name)
  </li>
}}
```

---

## Chaining with `:`

Calls are composed left‑to‑right with `:`; each call's output becomes the next call's
**chained** input.

```antlr
chain: call (DELIM call)*;
```

```ttl
@a(param):b():c(){{ … }}
```

The engine tracks the data type flowing through the chain, so each extension sees the
correct input type and the final output type is known at compile time. The `@out()`
extension is the usual consumer of the chained value: it emits whatever the previous call
produced (or wraps it in its own subtemplate). `@swap()` exchanges the model and chained
values for the duration of its body.

---

## Recursion

Definitions may call themselves, enabling tree rendering. From
[recursion.thtml](../src/Templates.Tests/TestTemplate/recursion.thtml), a `category`
definition that recurses into its subcategories:

```ttl
<category>
{{
  @if(@model.SubCategories.Count>0)
  {{
    <ul>
    @list(SubCategories)
    {{
      <li>
        <a href="@(Name)">@(Name)</a>
        @category()        @* recurse *@
      </li>
    }}
    </ul>
  }}
}} :: Category
```

Recursion depth is bounded by `TemplateOptions.MaxRecursionCount` (default **100**); see the
[C# API Reference](csharp-api.md#templateoptions). The expected output for this fixture is
[test-recursion.html](../src/Templates.Tests/TestTemplate/test-recursion.html).

---

## Imports `@<<{{ … }}`

The `@<<` directive imports the **definitions** of another template file into the current
parse, so you can share a library of definitions across templates.

```antlr
import_block: IMPORT_TOKEN WS* SUB_START text+ SUB_CLOSE;
```

```ttl
@<<{{ shared/widgets.thtml }}
```

The path between `{{ }}` is resolved relative to `TemplateOptions.RootPath`. Imports are
handled by the [`import` extension](built-in-extensions.md#import) machinery
([ImportExtension.cs](../src/Templates/Extensions/ImportExtension.cs)), which re‑parses the
referenced file into the current parse context.

> **`@<<` vs `@partial()`.** `@<<` / `@import` pull in *definitions* at compile time (no
> output of their own). [`@partial()`](built-in-extensions.md#partial) compiles a separate
> template by name and renders its output inline at run time. Choose `@<<`/`@import` to share
> reusable definitions; choose `@partial()` to embed another rendered template.

---

## Comments `@* … *@`

Comments are removed during lexing (routed to a hidden channel) and never appear in output.

```antlr
COMMENT: '@*' ('@' | '*'* ~[*@])* '*'+ '@' -> channel(HIDDEN);
```

Comments may appear almost anywhere — including **inside other tokens**. This is heavily
exercised by the fixtures:

```ttl
<title>Привет!</ti@*Comment Test 6*@tle>     @* comment splits a tag name (template.thtml) *@
@model(@*Comment Test 3*@){{TestData@*…*@Structure}}   @* inside a call and its body *@
<multi_text@*text area*@>                     @* inside a definition name *@
```

Comments are recognized in every lexer mode (DEFAULT, subtemplate, definition, import, call,
output), so they are safe to drop in anywhere.

---

## Raw blocks `@{ … }@` and `@:`

Raw regions are emitted **verbatim** and are not parsed for directives.

```antlr
RAW:     '@{' ('@' | '}'* ~[}@])* '}'+ '@';     @* block form *@
RW_LINE: '@:' ~[\r\n]* -> type(RAW);            @* line form: rest of line *@
```

- **Block** `@{ … }@` — everything between the delimiters is literal. Useful for emitting
  characters that would otherwise be parsed, e.g. a lone `.` between comments in
  [raw.thtml](../src/Templates.Tests/TestTemplate/raw.thtml):
  `@using(){{System@*Comment Test 2*@@{.}@Linq}}`.
- **Line** `@: …` — everything to the end of the line is literal. In
  [raw.thtml](../src/Templates.Tests/TestTemplate/raw.thtml),
  `@using(){{System@*Comment Test 1*@@:.` emits the `.` and the newline literally as part of
  the namespace text.

Raw blocks are recognized at the top level, in subtemplates, in output mode, and after a
call returns, so they compose with the rest of the syntax.

---

## Whitespace trimming `@\`

`@\` followed by any run of whitespace is consumed and produces no output (token `SKIP_WS`,
routed to the hidden channel). Use it to keep templates readable while controlling the
emitted whitespace — particularly at the end of a line, to suppress the trailing newline.

```antlr
fragment EAT_WS: '@\\' WS*;
SKIP_WS: EAT_WS -> channel(HIDDEN);
```

In [template.thtml](../src/Templates.Tests/TestTemplate/template.thtml), nearly every
directive line ends with `@\` to collapse the formatting newlines:

```ttl
@using(){{System.Linq}}@\
@model(@*Comment Test 3*@){{TestData@*Comment Test 4*@Structure}}@\
```

Whitespace that is *not* trimmed is preserved exactly — TTL is whitespace‑significant, which
is why the expected outputs (e.g.
[test-vc.html](../src/Templates.Tests/TestTemplate/test-vc.html)) contain the blank lines and
indentation that surround directives. The
[wierd-whitespace.thtml](../src/Templates.Tests/TestTemplate/wierd-whitespace.thtml) fixture
deliberately stress‑tests large runs of whitespace and `@using() {{   }}` with padding.

---

## How the lexer reads a template (modes)

TTL is context‑sensitive: the same characters mean different things depending on where they
appear. The ANTLR lexer implements this with a stack of **modes**
([TtlLexer.g4](../src/Templates.Language/TtlLexer.g4)). Understanding the modes explains why,
for example, `}}` ends a subtemplate but is plain text elsewhere.

| Mode | Entered when | Role | Notable exits |
| --- | --- | --- | --- |
| *(default)* | start of document | Top‑level text, `@…` directives, definitions, imports, raw blocks. | `@%`→DEF, `@<<`→IMPORT, `@`→OUT |
| `SUB_BLOCK` | after `{{` | Inside a subtemplate body; like default but `}}` closes it. | `}}`→pop |
| `DEF` | after `@%` | Inside a definition block: names `< >`, `:` base, `::` type, `->` default, `%@` close. | `{{`→SUB_BLOCK, `->`→OUT, `%@`→pop |
| `IMPORT_MODE` | after `@<<` | Reads the import path between `{{ }}`. | `}}`→pop |
| `OUT_MODE` | after `@` | Reads an extension name, then `(` opens the parameter. | `(`→CALL, raw/sub/def/import→transition |
| `CALL` | after `(` | Inside a parameter: member ids, `.`, `::` root ref, `:` delim, nested `(`. | `)`→pop, inner `@`→CS |
| `CALL_RETURNED` | after `)` | Decides what follows a call: `:` (chain), `@` (next out), `{{` (subtemplate), raw, etc. | many |
| `CS` | inner `@` inside a parameter | Embedded C# expression; balances nested `(` `)`, ends at the matching `)`. | matching `)`→pop |

You normally never think about modes — but they are the reason comments work everywhere,
why C# expressions can contain arbitrary parentheses, and why whitespace handling differs
slightly between a definition header and a body. For the full picture see
[Architecture → Lexing](architecture.md#1-lexing).

---

## Behavioral nuances summary

- **`@@` emits a literal `@`.** Everything else that isn't a directive is literal text.
- **Whitespace is significant.** Use `@\` to trim; otherwise spaces and newlines are emitted
  as written.
- **Comments can appear mid‑token** and are always stripped.
- **Embedded C# requires `AllowCSharp = true`**; `model`, `root`, and `chained` are the
  available identifiers.
- **`::Member` reads the root model**; plain `Member` reads the current model.
- **Definitions are invoked like extensions** (`@name()`), can be nested, typed, inherited,
  and fully overridden in document order.
- **HTML encoding is per‑extension** — see [Built‑in Extensions](built-in-extensions.md) and
  the `[EncodeOutput]` / `[NotEncode]` attributes. The unnamed `@(...)` output does **not**
  HTML‑encode; use `@string(...)` (or another `[EncodeOutput]` extension) when you need
  encoding.
- **Recursion is capped** by `TemplateOptions.MaxRecursionCount` (default 100).
- **Type mismatches and unknown members fail at compile time** (or, in `DEBUG`, when
  `Generate` is given a model of the wrong type) — see
  [error handling](csharp-api.md#errors-and-diagnostics).

Continue to the **[Built‑in Extensions](built-in-extensions.md)** reference for the helpers
you can call from these constructs.
