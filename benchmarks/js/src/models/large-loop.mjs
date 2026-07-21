// large-loop model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 3; formulas from
// Phase 1 workloads.md: 5,000 rows, name "row-" + i, value i). Generation formula transcribed,
// not its output; built once at module load and deep-frozen.
import { deepFreeze } from "./_deep-freeze.mjs";

const items = [];
for (let i = 0; i < 5000; i++) {
  items.push({ name: `row-${i}`, value: i });
}

export const model = deepFreeze({ items });
