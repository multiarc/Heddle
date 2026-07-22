// fragment-heavy model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 3; formulas
// from Phase 1 workloads.md workload 6). 48 rows, i in [0, 47].
import { deepFreeze } from "./_deep-freeze.mjs";

const items = [];
for (let i = 0; i < 48; i++) {
  items.push({
    name: `tile-${String(i).padStart(2, "0")}`,
    value: i * 11,
    badge: ["new", "hot", "sale", "std"][i % 4],
  });
}

export const model = deepFreeze({ items });
