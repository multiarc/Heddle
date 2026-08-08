// large-loop model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 3; formulas from
// Phase 1 workloads.md: 5,000 rows, value i). Rows carry ONLY the value (E21): the display
// name "row-{i}" is composed by the templates as the literal "row-" + the value substitution —
// pre-formatting it model-side would move rendering work out of the engine under test.
import { deepFreeze } from "./_deep-freeze.mjs";

const items = [];
for (let i = 0; i < 5000; i++) {
  items.push({ value: i });
}

export const model = deepFreeze({ items });
