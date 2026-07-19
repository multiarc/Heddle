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

// ---------------------------------------------------------------------------
// Phase 2 (post-2.0): the `@@` literal-@ escape. The rows below also prove the
// JS-target sempred translation in HeddleLexerExtended runs (a broken
// predicate would throw ReferenceError on every '@@').
// ---------------------------------------------------------------------------
const ESCAPE_POSITIVE = [
    ["E01 escape between text", "hello @@ world"],
    ["E02 lone pair", "@@"],
    ["E03 even run", "@@@@"],
    ["E04 escape then directive", "@@@(Name)"],
    ["E05 escape inside a {{ }} body", "@if(X){{ a @@ b }}"],
    // Comment adjacency, valid shape: '@' starts the directive, '@*c*@' is a
    // hidden comment, and 'if(X)' continues the output — the guard keeps the
    // second '@' available for the comment.
    ["E06 comment adjacency", "@@*c*@if(X){{y}}"],
];

test("@@ escape corpus produces zero annotations", async (t) => {
    for (const [label, template] of ESCAPE_POSITIVE) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template);
            assert.deepStrictEqual(annotations, [],
                `expected no annotations for ${JSON.stringify(template)}, got ${JSON.stringify(annotations)}`);
        });
    }
});

test("@@* adjacency guard: the escape does NOT fire before a comment", async () => {
    // If AT_ESCAPE (mis)fired here, '@@' would be RAW and '* adjacent' plain
    // text — a clean parse. The guard instead yields '@' OUT + comment-less
    // '*', which is a positioned HED0003, matching the C# lexer's token stream.
    const annotations = await parseAnnotations("text @@* adjacent");
    assert.ok(annotations.length >= 1, "expected HED0003 annotations");
    assert.match(annotations[0].text, /^HED0003: /);
    assert.strictEqual(annotations[0].row, 0);
    assert.strictEqual(annotations[0].column, 6);
});

// ---------------------------------------------------------------------------
// Phase 7 (post-2.0): named content regions.
// ---------------------------------------------------------------------------
const REGION_POSITIVE = [
    ["R01 public region", "@%<:header>{{Default}}%@"],
    ["R02 typed region", "@%<:item :: Article>{{Body}}%@"],
    ["R03 whitespace around the visibility ':'", "@%<: name>{{x}}%@"],
    ["R04 generic-typed region", "@%<:item :: List<Article>>{{B}}%@"],
    ["R05 region fill in a call body", "@card(A){{ @%<header:header>{{H}}%@ }}"],
    ["R06 declaration + fill document", "@%<:header>{{D}}%@\n@card(A){{ @%<header:header>{{H}}%@ }}"],
    ["R07 region beside a definition", "@%<card>{{x}}<:footer>{{f}}%@"],
];

test("region corpus produces zero annotations", async (t) => {
    for (const [label, template] of REGION_POSITIVE) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template);
            assert.deepStrictEqual(annotations, [],
                `expected no annotations for ${JSON.stringify(template)}, got ${JSON.stringify(annotations)}`);
        });
    }
});

// ---------------------------------------------------------------------------
// Robustness (JSACE-A-OBS): malformed input must NEVER throw out of the parse
// path. `{{ @if }}` used to crash with an unhandled "Empty Stack" — a directive
// opened right after an *unconnected* `{{` (which emits SUB_START without a
// pushMode) made the grammar's double `popMode, popMode` (OUT_SUB_CL /
// CALL_RETURN_SUB_CL) underflow the lexer mode stack. The guard lives in
// HeddleLexerExtended.popMode; these rows pin the repro and its neighborhood.
// ---------------------------------------------------------------------------
test("mode-stack underflow repro: {{ @if }} yields positioned annotations, no throw", async () => {
    // The underflow itself is now recorded (HeddleLexerExtended.popMode) at the
    // offending `}}`, because the same event fails the C# compile; the parser's
    // own diagnostic still follows. Two positioned errors, zero throws.
    const annotations = await parseAnnotations("{{ @if }}");
    assert.strictEqual(annotations.length, 2);
    assert.strictEqual(annotations[0].row, 0);
    assert.strictEqual(annotations[0].column, 7);
    assert.strictEqual(annotations[0].text, "HED0003: unexpected block terminator");
    assert.strictEqual(annotations[0].type, "error");
    assert.strictEqual(annotations[1].row, 0);
    assert.strictEqual(annotations[1].column, 4);
    assert.strictEqual(annotations[1].text, "HED0003: no viable alternative at input 'if }}'");
    assert.strictEqual(annotations[1].type, "error");
});

// ---------------------------------------------------------------------------
// Underflow visibility: every input that trips the popMode underflow recovery
// must surface a positioned "unexpected block terminator" — the C# engine
// FAILS compile on this exact class (Lexer.PopMode throws, caught at the
// Compile boundary as "[1,0:0]Operation is not valid..."), so a silently
// clean JS parse here would show a green editor for a document the engine
// rejects. One error per offending token; a doc with two underflows gets two.
// ---------------------------------------------------------------------------
const UNDERFLOW_CLASS = [
    // template, [row, column] of the "unexpected block terminator" annotation
    ["{{ @if }}", 0, 7],
    ["{{ @for }}", 0, 8],
    ["{{\n@if\n}}", 2, 0],
];

test("popMode underflow class surfaces a positioned block-terminator error", async (t) => {
    for (const [template, row, column] of UNDERFLOW_CLASS) {
        await t.test(JSON.stringify(template), async () => {
            const annotations = await parseAnnotations(template);
            const terminator = annotations.filter(
                (a) => a.text === "HED0003: unexpected block terminator"
            );
            assert.strictEqual(terminator.length, 1, "exactly one underflow error per offending token");
            assert.strictEqual(terminator[0].row, row);
            assert.strictEqual(terminator[0].column, column);
            assert.strictEqual(terminator[0].type, "error");
        });
    }
});

test("two underflow faults in one document each get their own error", async () => {
    const annotations = await parseAnnotations("{{ @if }} {{ @for }}");
    const terminators = annotations.filter(
        (a) => a.text === "HED0003: unexpected block terminator"
    );
    assert.strictEqual(terminators.length, 2);
    assert.deepStrictEqual(
        terminators.map((a) => [a.row, a.column]),
        [[0, 7], [0, 18]]
    );
});

// ---------------------------------------------------------------------------
// DELIBERATELY-CLEAN rows. Review cycle 1 classed these shapes as a
// "silent-clean divergence" (green in JS, rejected by C#) and proposed they
// error. That premise was falsified against the real engine (executed
// HeddleTemplate.Compile, model typeof(object)):
//   "{{@if}}", "{{@if(X)}}", "{{@for(x in Y)}}", "}} }}"  -> Success=True, 0 errors
//   "{{ @if(X) }}", "{{ @if(X) }} }}", "{{ @if(X){{y}} }}", "@if(X){{y}} }}"
//     -> fail ONLY with HED0001 "Property X not found" — the identical
//        model-binding error the unquestionably-valid "@if(X){{y}}" gets on a
//        model without X. None of them underflow the mode stack (verified by
//        deleting the popMode override: no "Empty Stack" throw).
// So these are syntactically VALID; annotating them would be a false positive
// (red squiggle on a document the engine compiles). These rows pin the clean
// parse on purpose — do not "fix" them to expect errors without re-running
// the C# ground truth above.
// ---------------------------------------------------------------------------
const CSHARP_VALID_SHAPES = [
    ["no-space directive in sub", "{{@if}}"],
    ["no-space parenthesized directive in sub", "{{@if(X)}}"],
    ["spaced parenthesized directive in sub", "{{ @if(X) }}"],
    ["no-space @for in sub", "{{@for(x in Y)}}"],
    ["sub then stray closer", "{{ @if(X) }} }}"],
    ["stray closers only", "}} }}"],
    ["nested sub then closer", "{{ @if(X){{y}} }}"],
    ["block then stray closer", "@if(X){{y}} }}"],
];

test("C#-verified-valid shapes stay annotation-free (no underflow false positives)", async (t) => {
    for (const [label, template] of CSHARP_VALID_SHAPES) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template);
            assert.deepStrictEqual(annotations, [],
                `expected no annotations for ${JSON.stringify(template)}, got ${JSON.stringify(annotations)}`);
        });
    }
});

test("worker keeps annotating after a formerly-crashing document", async () => {
    // The crash used to leave the worker dead; subsequent parses must behave
    // normally. Parse the repro, then a valid doc (clean), then a plain
    // malformed doc (ordinary positioned error).
    await parseAnnotations("{{ @if }}");
    assert.deepStrictEqual(await parseAnnotations("@if(X){{y}}"), []);
    const after = await parseAnnotations("@(1 +)");
    assert.strictEqual(after.length, 1);
    assert.strictEqual(after[0].text, "HED0003: no viable alternative at input '(1 +)'");
});

// Fuzz neighborhood: none may throw; each either genuinely parses (clean) or
// yields >=1 sane positioned HED0003 annotation.
const MALFORMED_FUZZ = [
    ["F01 directive after unconnected sub, @for", "{{ @for }}"],
    ["F02 empty sub", "{{ }}"],
    ["F03 lone @{", "@{"],
    ["F04 unterminated comment", "@* unterminated comment"],
    ["F05 unterminated string", "@(\"unterminated string)"],
    ["F06 region syntax at doc scope", "<:>"],
    ["F07 lone }@", "}@"],
    ["F08 @% without close", "@%"],
    ["F09 def header without body", "@% <card>"],
    ["F10 deeply nested unclosed {{", "{{ {{ {{ {{"],
    ["F11 two crash-shaped subs", "{{ @if }} {{ @for }}"],
    ["F12 %@ inside sub", "{{ %@ }}"],
    ["F13 @@-escape comment inside sub", "{{ @@*c*@ }}"],
    ["F14 bare @if", "@if"],
    ["F15 unclosed @if(", "@if("],
    ["F16 unclosed sub body", "@if(X){{"],
    ["F17 stray closers", "}} }} }}"],
    ["F18 crash shape across lines", "{{\n@if\n}}"],
];

test("malformed fuzz corpus never throws and only yields sane positioned annotations", async (t) => {
    for (const [label, template] of MALFORMED_FUZZ) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template); // must not throw
            for (const a of annotations) {
                assert.ok(Number.isInteger(a.row) && a.row >= 0,
                    `row must be a non-negative integer, got ${JSON.stringify(a)}`);
                assert.ok(Number.isInteger(a.column) && a.column >= 0,
                    `column must be a non-negative integer, got ${JSON.stringify(a)}`);
                assert.match(a.text, /^HED0003: /);
                assert.strictEqual(a.type, "error");
            }
        });
    }
});

// ---------------------------------------------------------------------------
// heddle_worker.js onUpdate backstop (JSACE-A-OBS): a residual parse-time
// throw must never escape onUpdate — it is converted into the errors collected
// so far, or a single positioned fallback annotation. The worker is an Ace
// CommonJS module; load it in isolation with the Ace/lint dependencies
// stubbed, the shared toAnnotations mapping REAL, and a DocumentParser stub
// that throws on demand, then drive the real onUpdate.
// ---------------------------------------------------------------------------
const fs = require("node:fs");
const path = require("node:path");

function loadHeddleWorker(DocumentParserStub) {
    const workerPath = path.resolve(__dirname, "..", "src", "mode", "heddle_worker.js");
    const source = fs.readFileSync(workerPath, "utf8");
    function Mirror(sender) {
        this.sender = sender;
        this.doc = { getValue: () => "" };
        this.deferredUpdate = { schedule: () => {} };
    }
    Mirror.prototype.setTimeout = function () {};
    const stubs = {
        "../lib/oop": {
            inherits: (ctor, superCtor) => {
                ctor.super_ = superCtor;
                ctor.prototype = Object.create(superCtor.prototype, {
                    constructor: { value: ctor, enumerable: false, writable: true, configurable: true }
                });
            },
            mixin: (obj, other) => Object.assign(obj, other)
        },
        "../lib/lang": {
            arrayToMap: (arr) => {
                const map = {};
                for (const item of arr) {
                    map[item] = 1;
                }
                return map;
            }
        },
        "../worker/mirror": { Mirror },
        "../mode/heddle": { WorkerMode: function WorkerMode() {} },
        "./heddle/DocumentParser": { DocumentParser: DocumentParserStub },
        // The REAL shared mapping — the assertion target.
        "./heddle/toAnnotations": require("../src/mode/heddle/toAnnotations"),
        "./html/saxparser": { SAXParser: function SAXParser() {} },
        "./css/csslint": { CSSLint: { getRules: () => [] } },
        "./css/csslint_email": { CSSLint: { getRules: () => [] } },
        "./javascript/jshint": { JSHINT: function () {} }
    };
    const moduleObj = { exports: {} };
    const requireStub = (id) => {
        if (!Object.prototype.hasOwnProperty.call(stubs, id)) {
            throw new Error("heddle_worker.js required an unstubbed module: " + id);
        }
        return stubs[id];
    };
    new Function("require", "exports", "module", source)(requireStub, moduleObj.exports, moduleObj);
    return moduleObj.exports.HeddleWorker;
}

function makeSender() {
    const events = [];
    return { events, emit: (name, data) => events.push({ name, data }) };
}

test("onUpdate backstop: a DocumentParser constructor throw yields the (0,0) fallback annotation", () => {
    const HeddleWorker = loadHeddleWorker(function () {
        throw new Error("forced constructor failure");
    });
    const sender = makeSender();
    const worker = new HeddleWorker(sender);
    worker.doc = {
        getValue: () => "{{ some doc }}",
        indexToPosition: (index) => ({ row: 0, column: index })
    };
    worker.onUpdate(); // must not throw
    const annotate = sender.events.filter((e) => e.name === "annotate");
    assert.strictEqual(annotate.length, 1);
    assert.deepStrictEqual(annotate[0].data, [{
        row: 0,
        column: 0,
        text: "HED0003: unrecoverable syntax error",
        type: "error"
    }]);
    assert.ok(!sender.events.some((e) => e.name === "codeok"),
        "a failed parse must not emit codeok");
});

test("onUpdate backstop: a parse-time throw surfaces the errors collected so far", () => {
    const HeddleWorker = loadHeddleWorker(function () {
        this.context = {
            errors: [{
                message: "unexpected block terminator",
                exception: null,
                position: { startIndex: 3, length: 2, line: 1, column: 3 }
            }]
        };
        this.parseGetErrors = () => { throw "Empty Stack"; };
    });
    const sender = makeSender();
    const worker = new HeddleWorker(sender);
    worker.doc = {
        getValue: () => "{{ }}...",
        indexToPosition: (index) => ({ row: 0, column: index })
    };
    worker.onUpdate(); // must not throw
    const annotate = sender.events.filter((e) => e.name === "annotate");
    assert.strictEqual(annotate.length, 1);
    assert.deepStrictEqual(annotate[0].data, [{
        row: 0,
        column: 3,
        text: "HED0003: unexpected block terminator",
        type: "error"
    }]);
    assert.ok(!sender.events.some((e) => e.name === "codeok"),
        "a failed parse must not emit codeok");
});

test("malformed region syntax surfaces positioned HED0003 annotations", async (t) => {
    const rows = [
        ["missing region name", "@%<:>{{x}}%@", 0, 4, "HED0003: missing ID at '>'"],
        ["region without body", "@%<:name>%@", 0, 9, "HED0003: mismatched input '%@' expecting {WS, SUB_START}"],
    ];
    for (const [label, template, row, column, text] of rows) {
        await t.test(label, async () => {
            const annotations = await parseAnnotations(template);
            assert.ok(annotations.length >= 1, "expected an annotation");
            assert.strictEqual(annotations[0].row, row);
            assert.strictEqual(annotations[0].column, column);
            assert.strictEqual(annotations[0].text, text);
            assert.strictEqual(annotations[0].type, "error");
        });
    }
});
