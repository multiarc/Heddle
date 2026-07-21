// encoded-loop model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 3; formulas
// from Phase 1 workloads.md workload 8). 5,000 rows, i in [0, 4999]; every cell contains
// characters from the five-character set and every comment carries こんにちは. All values
// satisfy the untrusted-data alphabet.
import { deepFreeze } from "./_deep-freeze.mjs";

const items = [];
for (let i = 0; i < 5000; i++) {
  items.push({
    tag: `tag-${i}&'${i % 7}'`,
    name: `item <${i}> & "co"`,
    comment: `'q' & <angle> "d" こんにちは ${i}`,
  });
}

export const model = deepFreeze({ items });
