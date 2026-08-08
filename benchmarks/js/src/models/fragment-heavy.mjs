// fragment-heavy model — Phase 4 WI2 (spec: templates-and-models.md §Models rule 3; formulas
// from Phase 1 workloads.md workload 6, ledger E20). 48 rows, i in [0, 47], four dispatched
// kinds (12 each); dispatch runs on the precomputed is_* booleans, never on the kind string
// (common-denominator dispatch). The model carries DATA only (E21): promo.price is a NUMBER,
// and derived display text (media caption, image src, ".99" display price) is composed by the
// templates as literal-plus-substitution.
import { deepFreeze } from "./_deep-freeze.mjs";

const kinds = ["tile", "card", "media", "stat"];
const badges = ["new", "hot", "sale", "std"];

const items = [];
for (let i = 0; i < 48; i++) {
  const kind = kinds[i % 4];
  const badge = badges[i % 4];
  items.push({
    kind,
    is_tile: kind === "tile",
    is_card: kind === "card",
    is_media: kind === "media",
    is_stat: kind === "stat",
    name: `item-${String(i).padStart(2, "0")}`,
    value: i * 11,
    badge,
    delta: (i % 7) - 3,
    promo: { label: badge, price: 9 + i },
  });
}

export const model = deepFreeze({ items });
