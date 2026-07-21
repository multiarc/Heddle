// Gate library self-test (Phase 4 WI3; spec: harness-and-run.md §Gate implementation —
// `test/gate-selftest.mjs`): fixtures for each pipeline step (CRLF, BOM survival,
// lone-surrogate rejection, inter-tag collapse incl. multi-run, N3b removal to nothing incl. a
// space-vs-nothing pair that must compare equal, edge trim, every N5 spelling variant incl.
// leading zeros and hex case, no-rescan, non-five-char NCRs untouched) plus the verifier
// calibration re-run: for each workload the verifier accepts the committed golden and rejects
// the Phase 1 canonical corruptions — two per raw workload, three per encoded — each with the
// correct failing check kind (pins mirror src/Heddle.Performance/Runners/IdiomaticChecks.cs
// and the synthesis rules of GoldenCorpus.cs / golden-corpus.md §Verification).
// Also runs the WI2 model smoke (pinned cardinalities and spot values).
import {
  normalize,
  stripWhitespace,
  canonicalizeEntities,
  countOccurrences,
} from "../src/gate/normalize.mjs";
import { assertControlledCell, assertSecurityFloor, GateFailure } from "../src/gate/controlled.mjs";
import { verify } from "../src/gate/verifier.mjs";
import { WORKLOADS, loadCorpusEntry, loadVerifyDefinition } from "../src/gate/corpus.mjs";

let passed = 0;
let failed = 0;
const failures = [];

function check(name, fn) {
  try {
    fn();
    passed++;
  } catch (error) {
    failed++;
    failures.push(`  [FAIL] ${name}: ${error.message}`);
  }
}

function assertEqual(actual, expected, what = "value") {
  if (actual !== expected) {
    throw new Error(`${what}: expected ${JSON.stringify(expected)}, got ${JSON.stringify(actual)}`);
  }
}

function assertThrows(fn, substring) {
  try {
    fn();
  } catch (error) {
    if (substring && !error.message.includes(substring)) {
      throw new Error(`threw, but message lacks "${substring}": ${error.message}`);
    }
    return;
  }
  throw new Error("expected an exception, none was thrown");
}

// ---- 1. Normalization fixtures (N1–N4, N3b) ---------------------------------------------------

check("N2: CRLF and lone CR unify to LF", () =>
  assertEqual(normalize("a\r\nb\rc", "raw"), "a\nb\nc"));

check("N3: inter-tag whitespace run collapses to nothing", () =>
  assertEqual(normalize("<a>x</a> \t\n <b>y</b>", "raw"), "<a>x</a><b>y</b>"));

check("N3: multiple inter-tag runs collapse in one pass", () =>
  assertEqual(normalize("<a> </a>\n<b>\t</b> <c></c>", "raw"), "<a></a><b></b><c></c>"));

check("N3: six-character class includes VT and FF", () =>
  assertEqual(normalize("<a>\v</a>\f<b></b>", "raw"), "<a></a><b></b>"));

check("N4: edge trim strips only the six-character class", () =>
  assertEqual(normalize(" \t\v\f\r\nx y \n", "raw"), "x y"));

check("N4: is not String.trim — U+00A0 NBSP survives the edge trim", () =>
  assertEqual(normalize(" x ", "raw"), " x "));

check("N3b: every whitespace run anywhere is removed to nothing", () =>
  assertEqual(stripWhitespace("a b\t\tc\nd \r e"), "abcde"));

check("N3b: space-vs-nothing pair compares equal (presence-vs-absence)", () =>
  assertEqual(stripWhitespace("<td >a b</td>"), stripWhitespace("<td>ab</td>")));

check("N3b: run-length pair compares equal", () =>
  assertEqual(stripWhitespace("a  b"), stripWhitespace("a b")));

check("N3b: does not touch non-whitespace Unicode (Japanese intact)", () =>
  assertEqual(stripWhitespace("こんにちは 0"), "こんにちは0"));

check("N1: lone surrogate is rejected as invalid UTF-8", () =>
  assertThrows(() => normalize("ok\uD800broken", "raw"), "lone surrogate"));

check("N1/BOM: leading U+FEFF is not stripped by the pipeline", () =>
  assertEqual(normalize("﻿x", "raw"), "﻿x"));

check("N1/BOM: BOM is not whitespace — it survives the N3b strip", () =>
  assertEqual(stripWhitespace("﻿ x"), "﻿x"));

// ---- 2. N5 entity canonicalization fixtures ---------------------------------------------------

const n5Cases = [
  // named (case-sensitive)
  ["&amp;", "&amp;"],
  ["&lt;", "&lt;"],
  ["&gt;", "&gt;"],
  ["&quot;", "&quot;"],
  ["&apos;", "&#39;"],
  ["&AMP;", "&AMP;"], // wrong-case named entity: untouched
  // decimal, with and without leading zeros
  ["&#38;", "&amp;"],
  ["&#038;", "&amp;"],
  ["&#0060;", "&lt;"],
  ["&#62;", "&gt;"],
  ["&#34;", "&quot;"],
  ["&#39;", "&#39;"],
  ["&#0039;", "&#39;"],
  // hex, case-insensitive in `x` and digits, with leading zeros
  ["&#x26;", "&amp;"],
  ["&#X26;", "&amp;"],
  ["&#x3c;", "&lt;"],
  ["&#x3C;", "&lt;"],
  ["&#x3e;", "&gt;"],
  ["&#X03E;", "&gt;"],
  ["&#x22;", "&quot;"],
  ["&#x27;", "&#39;"], // the Handlebars-JS hex-family case D4 exists for
  ["&#X0027;", "&#39;"],
  // non-five-character references: untouched
  ["&#8482;", "&#8482;"],
  ["&#x60;", "&#x60;"],
  ["&#x3D;", "&#x3D;"],
  ["&eacute;", "&eacute;"],
  ["&#380;", "&#380;"], // shares the digits "38" but is a different codepoint
];
for (const [input, expected] of n5Cases) {
  check(`N5: ${JSON.stringify(input)} -> ${JSON.stringify(expected)}`, () =>
    assertEqual(canonicalizeEntities(input), expected));
}

check("N5: single pass, output never rescanned (&amp;#39; is not double-canonicalized)", () =>
  assertEqual(canonicalizeEntities("&amp;#39;"), "&amp;#39;"));

check("N5: mixed sentence canonicalizes each spelling independently", () =>
  assertEqual(
    canonicalizeEntities("a&#x27;b&quot;c&#38;d&apos;e&#8482;f"),
    "a&#39;b&quot;c&amp;d&#39;e&#8482;f",
  ));

check("N5: applies to encoded suite only in normalize()", () => {
  assertEqual(normalize("&#x27;", "encoded"), "&#39;");
  assertEqual(normalize("&#x27;", "raw"), "&#x27;");
});

// ---- 3. Controlled gate fixtures against the real corpus machinery ----------------------------

check("controlled: golden text accepted verbatim (identity candidate)", () => {
  const entry = loadCorpusEntry("trivial-substitution");
  assertControlledCell({ engine: "selftest", workload: "trivial-substitution", output: entry.text });
});

check("controlled: whitespace-only divergence passes (CRLF + inter-tag + inner runs)", () => {
  const entry = loadCorpusEntry("trivial-substitution");
  const mangled = `  ${entry.text.replaceAll("><", "> \r\n <").replaceAll(" ", "  ")}\t\n`;
  assertControlledCell({ engine: "selftest", workload: "trivial-substitution", output: mangled });
});

check("controlled: a leading BOM survives to fail the byte compare", () => {
  const entry = loadCorpusEntry("trivial-substitution");
  assertThrows(
    () =>
      assertControlledCell({
        engine: "selftest",
        workload: "trivial-substitution",
        output: `﻿${entry.text}`,
      }),
    "first diff at 0",
  );
});

check("controlled: lone surrogate fails as invalid UTF-8 (N1)", () => {
  const entry = loadCorpusEntry("trivial-substitution");
  assertThrows(
    () =>
      assertControlledCell({
        engine: "selftest",
        workload: "trivial-substitution",
        output: `${entry.text}\uDFFF`,
      }),
    "lone surrogate",
  );
});

check("controlled: non-whitespace divergence fails with the contract surface", () => {
  const entry = loadCorpusEntry("trivial-substitution");
  assertThrows(
    () =>
      assertControlledCell({
        engine: "selftest",
        workload: "trivial-substitution",
        output: entry.text.replace("HB-2001", "HB-9999"),
      }),
    "first diff at",
  );
});

check("controlled: Handlebars-style &#x27; output passes an encoded cell via N5", () => {
  const entry = loadCorpusEntry("fortunes-encoded");
  const hexSpelled = entry.text.replaceAll("&#39;", "&#x27;");
  if (hexSpelled === entry.text) throw new Error("fixture inert: golden contains no &#39;");
  assertControlledCell({ engine: "selftest", workload: "fortunes-encoded", output: hexSpelled });
});

check("security floor: raw payload is rejected on un-normalized output", () => {
  const entry = loadCorpusEntry("fortunes-encoded");
  assertThrows(
    () =>
      assertSecurityFloor({
        engine: "selftest",
        workload: "fortunes-encoded",
        rawOutput: entry.text.replace("&lt;script&gt;alert(", "<script>alert("),
        oracleText: entry.text,
      }),
    'raw "<script>alert(" found 1 times',
  );
});

check("security floor: escaped-form count mismatch is rejected", () => {
  const entry = loadCorpusEntry("fortunes-encoded");
  assertThrows(
    () =>
      assertSecurityFloor({
        engine: "selftest",
        workload: "fortunes-encoded",
        rawOutput: entry.text.replace("&lt;script&gt;alert(", "&lt;script&gt;alerted("),
        oracleText: entry.text,
      }),
    "expected 1",
  );
});

// ---- 4. WI2 model smoke: pinned cardinalities and spot values ---------------------------------

const models = {};
for (const { id } of WORKLOADS) {
  models[id] = (await import(`../src/models/${id}.mjs`)).model;
}

check("models: pinned cardinalities 36/200/48/12/5000/5000", () => {
  assertEqual(models["mixed-page"].products.length, 36, "mixed-page products");
  assertEqual(models["conditional-heavy"].rows.length, 200, "conditional-heavy rows");
  assertEqual(models["fragment-heavy"].items.length, 48, "fragment-heavy items");
  assertEqual(models["fortunes-encoded"].rows.length, 12, "fortunes rows");
  assertEqual(models["large-loop"].items.length, 5000, "large-loop items");
  assertEqual(models["encoded-loop"].items.length, 5000, "encoded-loop items");
});

check("models: spot values byte-exact", () => {
  assertEqual(models["mixed-page"].products[0].name, "Product 01");
  assertEqual(models["mixed-page"].products[35].sku, "MX-1036");
  assertEqual(models["mixed-page"].products[2].on_sale, true, "product 03 on_sale");
  assertEqual(models["conditional-heavy"].rows[199].name, "unit-199");
  assertEqual(models["conditional-heavy"].rows[0].is_bronze, true);
  assertEqual(models["conditional-heavy"].rows[0].is_active, false, "row 0 is_active (0 % 5 === 0)");
  assertEqual(models["fragment-heavy"].items[47].name, "tile-47");
  assertEqual(models["fragment-heavy"].items[1].value, 11);
  assertEqual(models["fragment-heavy"].items[2].badge, "sale");
  assertEqual(models["large-loop"].items[4999].name, "row-4999");
  assertEqual(
    models["fortunes-encoded"].rows[10].message,
    '<script>alert("This should not be displayed in a browser alert box.");</script>',
    "row-11 XSS payload",
  );
  assertEqual(models["fortunes-encoded"].rows[11].message, "フレームワークのベンチマーク");
  assertEqual(models["encoded-loop"].items[0].tag, "tag-0&'0'");
  assertEqual(models["encoded-loop"].items[4999].comment, `'q' & <angle> "d" こんにちは 4999`);
  assertEqual(models["trivial-substitution"].sku, "HB-2001");
  assertEqual(models["trivial-substitution"].price, 4200);
  assertEqual(models["composed-page"].area_names.length, 7, "area_names");
  assertEqual(models["composed-page"].areas["Alert Top Section Below Nav"], "");
  assertEqual(models["composed-page"].section.meta, "<title>Title</title>");
});

check("models: deep-frozen at module load", () => {
  for (const { id } of WORKLOADS) {
    if (!Object.isFrozen(models[id])) throw new Error(`${id} model is not frozen`);
  }
  if (!Object.isFrozen(models["mixed-page"].products[0])) throw new Error("nested row not frozen");
  if (!Object.isFrozen(models["composed-page"].areas)) throw new Error("areas not frozen");
});

check("models: composed-page transcription matches the golden (whitespace-stripped)", () => {
  // The golden composed-page output is the ordered fragment concatenation; comparing the
  // N3b-stripped projections proves the multi-KB area transcription byte-exact on every
  // non-whitespace byte — the same projection the WI4/WI5 byte gate uses.
  const m = models["composed-page"];
  const concatenated =
    m.section.meta +
    m.section.social +
    m.comp.assets_styles +
    m.comp.custom_styles +
    m.comp.head_scripts +
    m.comp.body_scripts +
    m.area_names.map((n) => m.areas[n] ?? "").join("") +
    m.comp.assets_scripts +
    m.section.page_scripts +
    m.section.endpage_scripts +
    m.comp.body_end_scripts;
  assertControlledCell({ engine: "selftest", workload: "composed-page", output: concatenated });
});

// ---- 5. Verifier calibration re-run -----------------------------------------------------------
// Corruption synthesis mirrors GoldenCorpus.cs (RemoveFirst / SwapFirst / ReplaceFirst) and the
// per-workload pins of IdiomaticChecks.cs (golden-corpus.md §Verification). Two corruptions per
// raw workload, three per encoded workload; each must be rejected with the expected check kind.

function removeFirst(text, segment) {
  const at = text.indexOf(segment);
  return at < 0 ? null : text.slice(0, at) + text.slice(at + segment.length);
}

function replaceFirst(text, from, to) {
  const at = text.indexOf(from);
  return at < 0 ? null : text.slice(0, at) + to + text.slice(at + from.length);
}

/** Swaps the first occurrence of `a` with the first occurrence of `b` after it. */
function swapFirst(text, a, b) {
  const atA = text.indexOf(a);
  if (atA < 0) return null;
  const atB = text.indexOf(b, atA + a.length);
  if (atB < 0) return null;
  return (
    text.slice(0, atA) + b + text.slice(atA + a.length, atB) + a + text.slice(atB + b.length)
  );
}

// Per-workload calibration pins (IdiomaticChecks.cs; composed-page pins are its exported
// markers[0]/markers[1], resolved from the verify.json to avoid re-transcription).
const fortunesFirstRow =
  "<tr><td>1</td><td>A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1</td></tr>";
const encodedLoopTag0 = "tag-0&amp;&#39;0&#39;";
const encodedLoopFirstRow =
  `<tr><td data-tag="${encodedLoopTag0}">item &lt;0&gt; &amp; &quot;co&quot;</td>` +
  "<td>&#39;q&#39; &amp; &lt;angle&gt; &quot;d&quot; こんにちは 0</td></tr>";

const calibrationPins = {
  "composed-page": (def) => ({
    removedSegment: def.markers[1],
    removedKind: "marker",
    swapA: def.markers[0],
    swapB: def.markers[1],
  }),
  "trivial-substitution": () => ({
    removedSegment: "HB-2001",
    removedKind: "value",
    swapA: 'class="sku"',
    swapB: 'class="rating"',
  }),
  "large-loop": () => ({
    removedSegment: "<tr><td>row-0</td><td>0</td></tr>",
    removedKind: "value",
    swapA: "<tr><td>row-0</td><td>0</td></tr>",
    swapB: "<tr><td>row-2500</td><td>2500</td></tr>",
  }),
  "mixed-page": () => ({
    removedSegment: '<article class="card">',
    removedKind: "value",
    swapA: "<header>",
    swapB: 'class="hero"',
  }),
  "conditional-heavy": () => ({
    removedSegment: "unit-000",
    removedKind: "value",
    swapA: "unit-000",
    swapB: "unit-100",
  }),
  "fragment-heavy": () => ({
    removedSegment: "tile-00",
    removedKind: "value",
    swapA: "tile-00",
    swapB: "tile-24",
  }),
  "fortunes-encoded": () => ({
    removedSegment: fortunesFirstRow,
    removedKind: "value",
    swapA: "<tr><th>id</th><th>message</th></tr>",
    swapB: "フレームワークのベンチマーク",
    unescapeEscaped: "&lt;script&gt;alert(",
    unescapeRaw: "<script>alert(",
  }),
  "encoded-loop": () => ({
    removedSegment: encodedLoopFirstRow,
    removedKind: "value",
    swapA: encodedLoopTag0,
    swapB: "item &lt;2500&gt;",
    unescapeEscaped: "&lt;angle&gt;",
    unescapeRaw: "<angle>",
  }),
};

const calibrationMatrix = [];

function calibrate(id, def, golden, corruptionName, corrupted, expectedKind) {
  check(`calibration ${id}: '${corruptionName}' rejected (${expectedKind})`, () => {
    if (corrupted === null || corrupted === golden) {
      throw new Error(`corruption '${corruptionName}' could not be synthesized (pin not found in golden)`);
    }
    const found = verify(def, corrupted, id);
    if (found.length === 0) throw new Error(`corruption '${corruptionName}' was NOT rejected`);
    if (!found.some((f) => f.kind === expectedKind)) {
      throw new Error(
        `corruption '${corruptionName}' rejected, but not by the expected '${expectedKind}' check (got: ${found[0].message})`,
      );
    }
    calibrationMatrix.push(`  ${id}: ${corruptionName} -> rejected (${expectedKind})`);
  });
}

for (const { id } of WORKLOADS) {
  const entry = loadCorpusEntry(id);
  const def = loadVerifyDefinition(id);
  const pins = calibrationPins[id](def);

  check(`calibration ${id}: verifier accepts the committed golden`, () => {
    const found = verify(def, entry.text, id);
    if (found.length > 0) throw new Error(found.map((f) => f.message).join("; "));
    calibrationMatrix.push(`  ${id}: golden -> accepted`);
  });

  calibrate(id, def, entry.text, "removed-row",
    removeFirst(entry.text, pins.removedSegment), pins.removedKind);
  calibrate(id, def, entry.text, "reordered",
    swapFirst(entry.text, pins.swapA, pins.swapB), "marker");
  if (pins.unescapeEscaped) {
    calibrate(id, def, entry.text, "unescaped",
      replaceFirst(entry.text, pins.unescapeEscaped, pins.unescapeRaw), "forbidden");
  }
}

// ---- report -----------------------------------------------------------------------------------

console.log("gate-selftest calibration matrix:");
for (const line of calibrationMatrix) console.log(line);
console.log(`gate-selftest: ${passed} passed, ${failed} failed.`);
if (failed > 0) {
  console.log(failures.join("\n"));
  process.exit(1);
}
