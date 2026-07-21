// conditional-heavy model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 3;
// formulas from Phase 1 workloads.md workload 5). 200 rows, i in [0, 199]; branching data is
// precomputed booleans (no engine evaluates comparisons — README D5).
import { deepFreeze } from "./_deep-freeze.mjs";

const rows = [];
for (let i = 0; i < 200; i++) {
  rows.push({
    name: `unit-${String(i).padStart(3, "0")}`,
    note: `note ${i}`,
    is_bronze: i % 4 === 0,
    is_silver: i % 4 === 1,
    is_gold: i % 4 === 2,
    has_note: i % 2 === 0,
    is_active: i % 5 !== 0,
  });
}

export const model = deepFreeze({ rows });
