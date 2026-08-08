// mixed-page model. Pinned scalars and product formulas transcribed from
// benchmarks/dotnet/src/Models/MixedContent.cs. 36 products, i in [1, 36]. sku_number and
// batch are NUMBERS: the display SKU ("MX-" + sku_number) and the blurb sentence around
// batch are composed by the templates as literal-plus-substitution. The zero-padded product
// name stays model-side by design (row identity, not display text).
import { deepFreeze } from "./_deep-freeze.mjs";

const products = [];
for (let i = 1; i <= 36; i++) {
  products.push({
    name: `Product ${String(i).padStart(2, "0")}`,
    sku_number: 1000 + i,
    price: 950 + i * 7,
    on_sale: i % 3 === 0,
    batch: i,
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
