// Controlled-track byte gate + encoded security floor (Phase 4 WI3; spec: harness-and-run.md
// §Gate implementation; contract: parity-contract-v2.md §Controlled-track gate). Per cell:
// render once, normalize (N1–N5), then N3b-strip BOTH the candidate output and the loaded
// corpus text, TextEncoder-encode (UTF-8, never emits a BOM), and compare the resulting byte
// sequences. Failure carries the contract's surface: workload, engine, byte lengths,
// first-diff index, ±40-char excerpt (`\n`-escaped).
import { normalize, stripWhitespace, canonicalizeEntities, countOccurrences } from "./normalize.mjs";
import { loadCorpusEntry } from "./corpus.mjs";

const encoder = new TextEncoder();
const excerptDecoder = new TextDecoder("utf-8", { fatal: false });

/** Gate failure with the contract's failure surface as the message. */
export class GateFailure extends Error {}

function excerptAround(bytes, index) {
  const from = Math.max(0, index - 40);
  const to = Math.min(bytes.length, index + 40);
  return excerptDecoder.decode(bytes.subarray(from, to)).replaceAll("\n", "\\n");
}

/**
 * Byte-compares one controlled cell against its corpus entry. `render` is the cell's
 * render function (or pass `output` directly). Throws GateFailure on any non-whitespace
 * divergence; returns silently on pass.
 */
export function assertControlledCell({ engine, workload, render, output }) {
  const entry = loadCorpusEntry(workload);
  const raw = output !== undefined ? output : render();
  const cell = `${engine} ${workload}`;

  const normalized = normalize(raw, entry.suite, cell);
  const candidateBytes = encoder.encode(stripWhitespace(normalized));
  const oracleBytes = encoder.encode(stripWhitespace(entry.text));

  const n = Math.min(candidateBytes.length, oracleBytes.length);
  let i = 0;
  while (i < n && candidateBytes[i] === oracleBytes[i]) i++;
  if (i !== candidateBytes.length || i !== oracleBytes.length) {
    throw new GateFailure(
      `[FAIL] ${cell}: length exp ${oracleBytes.length} / act ${candidateBytes.length}; first diff at ${i}\n` +
        `    expected: ...${excerptAround(oracleBytes, i)}...\n` +
        `    actual:   ...${excerptAround(candidateBytes, i)}...`,
    );
  }

  if (entry.suite === "encoded") {
    assertSecurityFloor({ engine, workload, rawOutput: raw, oracleText: entry.text });
  }
}

/**
 * Encoded-suite security floor (contract rule 5, defense in depth): the raw substring
 * `<script>alert(` occurs zero times in the UN-normalized candidate output, and the escaped
 * form (`&lt;script&gt;alert(` after N5) occurs the corpus-derived expected number of times.
 */
export function assertSecurityFloor({ engine, workload, rawOutput, oracleText }) {
  const rawHits = countOccurrences(rawOutput, "<script>alert(");
  if (rawHits !== 0) {
    throw new GateFailure(
      `[FAIL] ${engine} ${workload} security: raw "<script>alert(" found ${rawHits} times (expected 0)`,
    );
  }
  const escaped = "&lt;script&gt;alert(";
  const expected = countOccurrences(oracleText, escaped);
  const actual = countOccurrences(canonicalizeEntities(rawOutput), escaped);
  if (actual !== expected) {
    throw new GateFailure(
      `[FAIL] ${engine} ${workload} security: escaped "&lt;script&gt;alert(" found ${actual} times (expected ${expected})`,
    );
  }
}
