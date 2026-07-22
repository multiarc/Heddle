// mixed-page model — Phase 4 WI2 (spec: templates-and-models.md §Models rules 3–4; pinned
// scalars and product formulas from Phase 1 workloads.md workload 4 /
// src/Heddle.Performance/Runners/MixedContent.cs). 36 products, i in [1, 36].
import { deepFreeze } from "./_deep-freeze.mjs";

const products = [];
for (let i = 1; i <= 36; i++) {
  products.push({
    name: `Product ${String(i).padStart(2, "0")}`,
    sku: `MX-${1000 + i}`,
    price: 950 + i * 7,
    on_sale: i % 3 === 0,
    blurb: `A dependable workshop staple from batch ${i}, checked for daily use and backed by our lifetime guarantee.`,
  });
}

export const model = deepFreeze({
  page_title: "Mercantile - Catalog",
  store_name: "Mercantile",
  hero_heading: "Autumn hardware sale",
  hero_tagline: "Hand-picked tools, fair prices, shipped tomorrow.",
  show_banner: true,
  banner_text: "Free shipping on orders over 60.",
  show_debug_panel: false,
  footer_note: "Prices include VAT where applicable.",
  year: 2026,
  support_email: "support at mercantile.example",
  products,
});
