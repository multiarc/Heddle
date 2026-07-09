"use strict";

/*
 * WS5 — worker diagnostics (parse-only baseline).
 *
 * Drives the real Heddle worker parse path (DocumentParser -> ANTLR v2 grammar ->
 * HeddleErrorListener -> ParseContext) through helpers/parse.js, which applies the
 * exact shared error->annotation mapping the worker ships
 * (mode/heddle/toAnnotations.js).
 *
 * POSITIVE rows (valid v2 templates) must yield ZERO annotations.
 * NEGATIVE rows (syntactically invalid templates) must yield a POSITIONED HED0003
 * syntax error. Rows mirror a representative subset of the C# grammar corpora
 * NativeExpressionParseTests (P01-P24 / N01-N14) and PropsParseTests
 * (PP01-PP24 / NP01-NP15).
 */

const test = require("node:test");
const assert = require("node:assert");
const { parseAnnotations } = require("./helpers/parse");

// ---------------------------------------------------------------------------
// POSITIVE corpus: valid v2 constructs -> zero annotations.
// ---------------------------------------------------------------------------
const POSITIVE = [
    // Native expressions (mirrors NativeExpressionParseTests P01-P24).
    ["P01 member path", "@(Name)"],
    ["P03 csharp tier", "@( @Model.X )"],
    ["P04 comparison in @if", "@if(Count > 0){{x}}"],
    ["P05 multiply", "@(Price * Quantity)"],
    ["P06 coalesce", "@(Name ?? \"anon\")"],
    ["P07 ternary", "@(IsFeatured ? \"a\" : \"\")"],
    ["P08 precedence", "@(A + B * C == D && !E)"],
    ["P11 grouping", "@((A + B) * C)"],
    ["P12 root-reference arithmetic", "@(::Total - Amount)"],
    ["P13 function call", "@(max(A, B))"],
    ["P15 postfix index then hop", "@(Items[0].Name)"],
    ["P16 multi-dim index", "@(Matrix[1, 2])"],
    ["P17 hex + bitwise shift", "@(0x1F & Flags | 1 << Bits)"],
    ["P18 literal typing", "@(1_000)"],
    ["P19 char literal", "@('c' + Name)"],
    ["P21 method call", "@(Name.ToUpper())"],
    ["P22 unary over path", "@(!A.B.C)"],
    ["P23 interior whitespace", "@( Price  *  Quantity )"],
    ["P24 composite", "@(A.B > 0 ? len(Name) : 0)"],
    // Named arguments (mirrors PropsParseTests PP01-PP14).
    ["PP01 positional + named args", "@card(Article, style: \"wide\", compact: true)"],
    ["PP02 no named args", "@card(Article)"],
    ["PP03 named only", "@card(style: \"wide\")"],
    ["PP04 root-ref named value", "@card(Article, style: ::Site.DefaultCardStyle)"],
    ["PP05 unary named value", "@card(Article, compact: !Hidden)"],
    ["PP06 ternary named value", "@card(Article, style: Featured ? \"wide\" : \"plain\")"],
    ["PP08 native positional + named", "@card(Price * 2, style: \"x\")"],
    ["PP10 this expression", "@out(this)"],
    ["PP11 this member hop", "@out(this.Name)"],
    ["PP14 named args then chain", "@card(Article, style: \"wide\"):html()"],
    // Prop declarations & slots (mirrors PropsParseTests PP15-PP24).
    ["PP15 two props w/ defaults", "@% <card(style: string = \"plain\", compact: bool = false)>{{x}} :: Article %@"],
    ["PP16 slot only", "@% <picker(out:: MenuOption)>{{x}} :: Menu %@"],
    ["PP17 prop + slot", "@% <combo(style: string = \"plain\", out:: Option)>{{x}} :: Menu %@"],
    ["PP19 dotted generic type", "@% <grid(items: System.Collections.Generic.List<string>)>{{x}} %@"],
    ["PP20 signed numeric default", "@% <pad(width: int = -4)>{{x}} %@"],
    ["PP22 empty prop list", "@% <card()>{{x}} %@"],
    ["PP24 array type name", "@% <note(tags: string[])>{{x}} %@"],
    // Plain content stays clean.
    ["plain html + output", "<div class=\"card\">\n  Hello {{Name}}, welcome!\n</div>"],
];

test("positive corpus produces zero annotations", async (t) => {
    for (const [label, template] of POSITIVE) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template);
            assert.deepStrictEqual(
                annotations,
                [],
                `expected no annotations for ${JSON.stringify(template)}, got ${JSON.stringify(annotations)}`
            );
        });
    }
});

// ---------------------------------------------------------------------------
// NEGATIVE corpus: invalid v2 constructs -> a positioned HED0003 error.
// (mirrors NativeExpressionParseTests N01-N14 / PropsParseTests NP01-NP15)
// ---------------------------------------------------------------------------
const NEGATIVE = [
    ["N01 assignment", "@(X = 1)"],
    ["N02 compound assignment", "@(X += 1)"],
    ["N03 increment", "@(X++)"],
    ["N04 lambda", "@(Items.Where(i => i))"],
    ["N05 cast syntax", "@((Foo) Bar)"],
    ["N06 is", "@(x is string)"],
    ["N07 new", "@(new Foo())"],
    ["N08 null-conditional", "@(a?.b)"],
    ["N11 ternary missing colon", "@(a ? b)"],
    ["N12 dangling operator", "@(1 +)"],
    ["N13 dangling comma", "@(max(1, ))"],
    ["NP03 second positional", "@card(A, B)"],
    ["NP04 positional after named", "@card(style: \"x\", Article)"],
    ["NP05 missing value", "@card(Article, style:)"],
    ["NP06 missing name", "@card(Article, : \"x\")"],
    ["NP08 missing ':' in prop", "@% <card(style string)>{{x}} %@"],
    ["NP09 identifier as default", "@% <card(style: string = Name)>{{x}} %@"],
    ["NP11 dangling comma in prop list", "@% <card(style: string,)>{{x}} %@"],
    ["NP13 keyword as prop name", "@% <card(true: bool)>{{x}} %@"],
    ["NP15 missing comma between named args", "@card(Article, style: \"a\" style: \"b\")"],
];

test("negative corpus produces a positioned HED0003 error", async (t) => {
    for (const [label, template] of NEGATIVE) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template);
            assert.ok(
                annotations.length >= 1,
                `expected at least one annotation for ${JSON.stringify(template)}`
            );
            const first = annotations[0];
            assert.strictEqual(first.type, "error");
            assert.match(first.text, /^HED0003: /, "annotation text must carry the HED0003 syntax-error id");
            assert.ok(first.text.length > "HED0003: ".length, "annotation text must include the ANTLR message");
            assert.ok(Number.isInteger(first.row) && first.row >= 0, "row must be a non-negative integer");
            assert.ok(Number.isInteger(first.column) && first.column >= 0, "column must be a non-negative integer");
        });
    }
});

// ---------------------------------------------------------------------------
// Exact position + phrasing checks for representative rows. These pin the
// offset (row/column) and the ANTLR message the worker surfaces.
// ---------------------------------------------------------------------------
const POSITIONED = [
    // template, row, column, expected text (exact)
    ["@(X = 1)", 0, 1, "HED0003: no viable alternative at input '(X ='"],
    ["@(1 +)", 0, 1, "HED0003: no viable alternative at input '(1 +)'"],
    ["@card(A, B)", 0, 10, "HED0003: mismatched input ')' expecting DELIM"],
    // A recovery reported by ANTLR with a NULL exception (missing token); the
    // old ParseContext dropped these entirely, so this row guards the fix.
    ["@card(Article, : \"x\")", 0, 15, "HED0003: missing ID at ':'"],
];

test("representative rows carry the exact offset and ANTLR phrasing", async (t) => {
    for (const [template, row, column, text] of POSITIONED) {
        await t.test(JSON.stringify(template), async () => {
            const annotations = await parseAnnotations(template);
            assert.ok(annotations.length >= 1, "expected an annotation");
            assert.strictEqual(annotations[0].row, row);
            assert.strictEqual(annotations[0].column, column);
            assert.strictEqual(annotations[0].text, text);
            assert.strictEqual(annotations[0].type, "error");
        });
    }
});

// ---------------------------------------------------------------------------
// Multiline offset: the error row/column must track the line the fault is on.
// ---------------------------------------------------------------------------
test("multiline template reports the error on the correct row", async () => {
    const template = "Header line\n@(1 +)\nfooter";
    const annotations = await parseAnnotations(template);
    assert.strictEqual(annotations.length, 1);
    assert.strictEqual(annotations[0].row, 1);
    assert.strictEqual(annotations[0].column, 1);
    assert.match(annotations[0].text, /^HED0003: /);
});

test("multiline error inside nested markup keeps the column offset", async () => {
    const template = "<div>\n  {{Name}}\n  @card(A, B)\n</div>";
    const annotations = await parseAnnotations(template);
    assert.strictEqual(annotations.length, 1);
    assert.strictEqual(annotations[0].row, 2);
    assert.strictEqual(annotations[0].column, 12);
    assert.strictEqual(annotations[0].text, "HED0003: mismatched input ')' expecting DELIM");
});
