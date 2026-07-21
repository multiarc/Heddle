// Idiomatic-track functional-equivalence verifier (Phase 4 WI3; spec: harness-and-run.md
// §Gate implementation; contract: parity-contract-v2.md §Idiomatic-track gate). Definitions
// are consumed from the corpus `<id>.verify.json` files — never re-authored here. All matching
// runs on the whitespace-stripped projection of the normalized candidate (N1–N5, then N3b)
// with the same strip applied to every needle, mirroring IdiomaticChecks.Verify in the
// intra-.NET suite (including its marker-miss semantics: report and keep scanning from the
// current position so every missing/out-of-order marker is reported).
import { normalize, stripWhitespace, countOccurrences } from "./normalize.mjs";

function excerpt(s) {
  const short = s.length <= 48 ? s : `${s.slice(0, 48)}…`;
  return short.replaceAll("\n", "\\n");
}

/**
 * Runs the verifier definition against a candidate's raw output. Returns an array of failure
 * objects `{ kind, entry, expected, found, message }` — empty when accepted. `kind` is one of
 * `value | marker | forbidden | required`.
 */
export function verify(definition, rawOutput, context = `${definition.workload}`) {
  const failures = [];
  const fail = (kind, entry, expected, found) =>
    failures.push({
      kind,
      entry,
      expected,
      found,
      message: `[FAIL] ${context} ${kind}: expected ${expected} of "${excerpt(entry)}", found ${found}`,
    });

  const normalized = normalize(rawOutput, definition.suite, context);
  const stripped = stripWhitespace(normalized);
  const strippedRaw = stripWhitespace(rawOutput);

  for (const v of definition.values ?? []) {
    const found = countOccurrences(stripped, stripWhitespace(v.text));
    if (found !== v.count) fail("value", v.text, v.count, found);
  }

  let pos = 0;
  for (const marker of definition.markers ?? []) {
    const needle = stripWhitespace(marker);
    const at = stripped.indexOf(needle, pos);
    if (at < 0) {
      fail("marker", marker, `1 (in order after index ${pos})`, 0);
      continue; // keep scanning so every missing/out-of-order marker is reported
    }
    pos = at + needle.length;
  }

  for (const f of definition.forbidden ?? []) {
    const needle = stripWhitespace(f);
    // Zero occurrences in raw AND normalized output; both scanned whitespace-stripped, which
    // only strengthens detection (harness-and-run.md §Gate implementation).
    const found = Math.max(countOccurrences(strippedRaw, needle), countOccurrences(stripped, needle));
    if (found !== 0) fail("forbidden", f, 0, found);
  }

  for (const r of definition.required ?? []) {
    const found = countOccurrences(stripped, stripWhitespace(r.text));
    if (found < r.minCount) fail("required", r.text, `>= ${r.minCount}`, found);
  }

  return failures;
}

/** Throws on the first verification failure (idiomatic gate hard-stop shape). */
export function assertIdiomaticCell({ engine, workload, definition, render, output }) {
  const raw = output !== undefined ? output : render();
  const failures = verify(definition, raw, `${engine} ${workload}`);
  if (failures.length > 0) {
    throw new Error(failures.map((f) => f.message).join("\n"));
  }
}
