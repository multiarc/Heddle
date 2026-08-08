//! Shared workload models — the Rust construction of the Phase 1 model data, normative in
//! `docs/spec/cross-stack-benchmarks/phase-2-rust/workload-ports.md` §Model construction
//! (which mirrors Phase 1 `workloads.md`). One module builds every model exactly once
//! (`OnceLock`), mirroring the .NET `Shared`-instance discipline. All numeric formatting is
//! integer `Display` (no locale, matching C# invariant `int` formatting). Per ledger E21/E22
//! the model tier carries DATA only — no derived display strings and no literal page text.

use std::sync::OnceLock;

use serde::{Deserialize, Serialize};

// ---- composed-page ---------------------------------------------------------------------------

/// The structured navigation model (ledger E20; E22 removed the text half). The model is
/// `ComposedModel { nav }` and NOTHING else — every fragment of literal page text (chrome,
/// alert/secondary-menu blobs, asset/script snippets) lives in the TEMPLATES. The nav data is
/// loaded once at init from the corpus fixture
/// `benchmarks/dotnet/GoldenCorpus/fixtures/composed-page/nav.json` (snake_case keys, the
/// single source of truth every non-.NET ecosystem loads from; hash-recorded in the manifest's
/// `fixtures` section).
#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NavModel {
    pub menus: Vec<MegaMenu>,
    pub footer_columns: Vec<NavColumn>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct MegaMenu {
    pub tabs: Vec<MenuTab>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct MenuTab {
    pub label: String,
    pub href: String,
    pub css: String,
    pub has_dropdown: bool,
    pub dropdown_css: String,
    pub columns: Vec<NavColumn>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NavColumn {
    pub sections: Vec<NavSection>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NavSection {
    pub title: String,
    pub href: String,
    pub title_linked: bool,
    pub links: Vec<NavLink>,
}

#[derive(Serialize, Deserialize)]
#[serde(deny_unknown_fields)]
pub struct NavLink {
    pub label: String,
    pub href: String,
}

/// The composed-page workload model — dictionary/Hash views nest under a `nav` key
/// (workloads.md workload 1, E20/E22).
#[derive(Serialize)]
pub struct ComposedModel {
    pub nav: NavModel,
}

pub fn composed() -> &'static ComposedModel {
    static MODEL: OnceLock<ComposedModel> = OnceLock::new();
    MODEL.get_or_init(|| ComposedModel {
        nav: crate::corpus::load_nav().unwrap_or_else(|e| panic!("{e}")),
    })
}

// ---- trivial-substitution --------------------------------------------------------------------

#[derive(Serialize)]
pub struct SubstitutionModel {
    pub title: &'static str,
    pub sku: &'static str,
    pub price: i32,
    pub brand: &'static str,
    pub category: &'static str,
    pub availability: &'static str,
    pub url: &'static str,
    pub image_url: &'static str,
    pub summary: &'static str,
    pub rating: &'static str,
}

pub fn substitution() -> &'static SubstitutionModel {
    static MODEL: OnceLock<SubstitutionModel> = OnceLock::new();
    MODEL.get_or_init(|| SubstitutionModel {
        title: "Heddle Handbook",
        sku: "HB-2001",
        price: 4200,
        brand: "Heddle Press",
        category: "Reference",
        availability: "In stock",
        url: "/catalog/handbook",
        image_url: "/img/handbook.png",
        summary: "A concise field guide to the engine.",
        rating: "4.8",
    })
}

// ---- large-loop ------------------------------------------------------------------------------

/// Rows carry ONLY the value (E21): the display name `row-{i}` is composed by the templates
/// as the literal `row-` + the value substitution.
#[derive(Serialize)]
pub struct LoopRow {
    pub value: i32,
}

pub fn large_loop() -> &'static Vec<LoopRow> {
    static MODEL: OnceLock<Vec<LoopRow>> = OnceLock::new();
    MODEL.get_or_init(|| (0..5000).map(|i: i32| LoopRow { value: i }).collect())
}

// ---- mixed-page ------------------------------------------------------------------------------

#[derive(Serialize)]
pub struct MixedModel {
    pub page_title: &'static str,
    pub store_name: &'static str,
    pub hero_heading: &'static str,
    pub hero_tagline: &'static str,
    pub show_banner: bool,
    pub banner_text: &'static str,
    pub show_debug_panel: bool,
    pub footer_note: &'static str,
    pub year: i32,
    pub support_email: &'static str,
    pub products: Vec<MixedProduct>,
}

/// E21: `sku_number`/`batch` are ints — the templates compose the display SKU
/// (`MX-` + sku_number) and the blurb sentence (around the batch substitution).
#[derive(Serialize)]
pub struct MixedProduct {
    pub name: String,
    pub sku_number: i32,
    pub price: i32,
    pub on_sale: bool,
    pub batch: i32,
}

pub fn mixed() -> &'static MixedModel {
    static MODEL: OnceLock<MixedModel> = OnceLock::new();
    MODEL.get_or_init(|| MixedModel {
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
        products: (1..=36)
            .map(|i: i32| MixedProduct {
                name: format!("Product {i:02}"),
                sku_number: 1000 + i,
                price: 950 + i * 7,
                on_sale: i % 3 == 0,
                batch: i,
            })
            .collect(),
    })
}

// ---- conditional-heavy -----------------------------------------------------------------------

/// E21: the int `seq` replaces the note string — the templates compose the note text as the
/// literal `note ` + the seq substitution.
#[derive(Serialize)]
pub struct ConditionalRow {
    pub name: String,
    pub seq: i32,
    pub is_bronze: bool,
    pub is_silver: bool,
    pub is_gold: bool,
    pub has_note: bool,
    pub is_active: bool,
}

pub fn conditional() -> &'static Vec<ConditionalRow> {
    static MODEL: OnceLock<Vec<ConditionalRow>> = OnceLock::new();
    MODEL.get_or_init(|| {
        (0..200)
            .map(|i: i32| ConditionalRow {
                name: format!("unit-{i:03}"),
                seq: i,
                is_bronze: i % 4 == 0,
                is_silver: i % 4 == 1,
                is_gold: i % 4 == 2,
                has_note: i % 2 == 0,
                is_active: i % 5 != 0,
            })
            .collect()
    })
}

// ---- fragment-heavy --------------------------------------------------------------------------

/// E20: 48 rows of four dispatched fragment kinds (12 each) with one level of nesting (the
/// card fragment renders badge + price against `promo`). The model carries DATA only (E21):
/// derived display text — the media caption (`Caption for ` + name), the image source
/// (`/img/` + name + `.jpg`) and the display price (price + `.99`) — is composed by the
/// TEMPLATES as literal-plus-substitution.
#[derive(Serialize)]
pub struct FragmentRow {
    /// `{ "tile", "card", "media", "stat" }[i % 4]` — informational; engines dispatch on the
    /// precomputed booleans, never on this string (common-denominator dispatch).
    pub kind: &'static str,
    pub is_tile: bool,
    pub is_card: bool,
    pub is_media: bool,
    pub is_stat: bool,
    /// `item-{i:02}` — identity data, the one padded value.
    pub name: String,
    pub value: i32,
    pub badge: &'static str,
    pub delta: i64,
    /// On EVERY row — no engine needs a null guard.
    pub promo: FragmentPromo,
}

#[derive(Serialize)]
pub struct FragmentPromo {
    /// == the row's badge.
    pub label: &'static str,
    /// `9 + i`, whole-currency units — an int, not a string (the display price is the
    /// template-composed `@(Price).99`).
    pub price: i32,
}

pub fn fragment() -> &'static Vec<FragmentRow> {
    static MODEL: OnceLock<Vec<FragmentRow>> = OnceLock::new();
    MODEL.get_or_init(|| {
        (0..48)
            .map(|i: i32| {
                let kind = ["tile", "card", "media", "stat"][(i % 4) as usize];
                let badge = ["new", "hot", "sale", "std"][(i % 4) as usize];
                FragmentRow {
                    kind,
                    is_tile: kind == "tile",
                    is_card: kind == "card",
                    is_media: kind == "media",
                    is_stat: kind == "stat",
                    name: format!("item-{i:02}"),
                    value: i * 11,
                    badge,
                    delta: (i % 7) as i64 - 3,
                    promo: FragmentPromo {
                        label: badge,
                        price: 9 + i,
                    },
                }
            })
            .collect()
    })
}

// ---- fortunes-encoded ------------------------------------------------------------------------

/// The 12 pinned fortune messages (Phase 1 workloads.md — workload 7), byte-for-byte.
/// Rows 4 and 8 carry U+2014 em dashes; row 11 is the TechEmpower XSS payload; row 12 the
/// Japanese string.
const FORTUNE_MESSAGES: [&str; 12] = [
    "A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1",
    "A computer program does what you tell it to do, not what you want it to do.",
    "A computer scientist is someone who fixes things that aren't broken.",
    "A list is only as strong as its weakest link. \u{2014} Donald Knuth",
    "After enough decimal places, nobody gives a damn.",
    "Any program that runs right is obsolete.",
    "Computers make very fast, very accurate mistakes.",
    "Emacs is a nice operating system, but I prefer UNIX. \u{2014} Tom Christiansen",
    "Feature: A bug with seniority.",
    "fortune: No such file or directory",
    "<script>alert(\"This should not be displayed in a browser alert box.\");</script>",
    "フレームワークのベンチマーク",
];

#[derive(Serialize)]
pub struct FortuneRow {
    pub id: i32,
    pub message: &'static str,
}

pub fn fortunes() -> &'static Vec<FortuneRow> {
    static MODEL: OnceLock<Vec<FortuneRow>> = OnceLock::new();
    MODEL.get_or_init(|| {
        FORTUNE_MESSAGES
            .iter()
            .enumerate()
            .map(|(i, message)| FortuneRow {
                id: i as i32 + 1,
                message,
            })
            .collect()
    })
}

// ---- encoded-loop ----------------------------------------------------------------------------

#[derive(Serialize)]
pub struct EncodedLoopRow {
    pub tag: String,
    pub name: String,
    pub comment: String,
}

pub fn encoded_loop() -> &'static Vec<EncodedLoopRow> {
    static MODEL: OnceLock<Vec<EncodedLoopRow>> = OnceLock::new();
    MODEL.get_or_init(|| {
        (0..5000)
            .map(|i: i32| EncodedLoopRow {
                tag: format!("tag-{i}&'{}'", i % 7),
                name: format!("item <{i}> & \"co\""),
                comment: format!("'q' & <angle> \"d\" こんにちは {i}"),
            })
            .collect()
    })
}

// ---- tests (spec Testing plan — model pins; expected counts restated as literals) ------------

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn large_loop_has_5000_rows_with_pinned_edges() {
        let rows = large_loop();
        assert_eq!(rows.len(), 5000);
        // E21: value only — the display name `row-{i}` is template-composed.
        assert_eq!(rows[0].value, 0);
        assert_eq!(rows[2500].value, 2500);
        assert_eq!(rows[4999].value, 4999);
    }

    #[test]
    fn conditional_has_200_rows_with_pinned_distributions() {
        let rows = conditional();
        assert_eq!(rows.len(), 200);
        assert_eq!(rows.iter().filter(|r| r.is_bronze).count(), 50);
        assert_eq!(rows.iter().filter(|r| r.is_silver).count(), 50);
        assert_eq!(rows.iter().filter(|r| r.is_gold).count(), 50);
        assert_eq!(
            rows.iter()
                .filter(|r| !r.is_bronze && !r.is_silver && !r.is_gold)
                .count(),
            50
        );
        assert_eq!(rows.iter().filter(|r| r.has_note).count(), 100);
        assert_eq!(rows.iter().filter(|r| r.is_active).count(), 160);
        assert_eq!(rows[0].name, "unit-000");
        assert_eq!(rows[199].name, "unit-199");
        // E21: the int seq replaces the note string (`note {i}` is template-composed).
        assert_eq!(rows[7].seq, 7);
        assert_eq!(rows[199].seq, 199);
    }

    #[test]
    fn fragment_has_48_rows_with_pinned_dispatch_and_nesting() {
        let rows = fragment();
        assert_eq!(rows.len(), 48);
        // E20 identity/value pins.
        assert_eq!(rows[0].name, "item-00");
        assert_eq!(rows[47].name, "item-47");
        assert_eq!(rows[47].value, 47 * 11);
        assert_eq!(rows.iter().filter(|r| r.badge == "new").count(), 12);
        assert_eq!(rows[1].badge, "hot");
        // Dispatch cycle: 12 of each kind, booleans precomputed and consistent with `kind`.
        for k in ["tile", "card", "media", "stat"] {
            assert_eq!(rows.iter().filter(|r| r.kind == k).count(), 12);
        }
        assert!(rows[0].is_tile && rows[1].is_card && rows[2].is_media && rows[3].is_stat);
        for r in rows {
            let flags = [r.is_tile, r.is_card, r.is_media, r.is_stat]
                .iter()
                .filter(|&&b| b)
                .count();
            assert_eq!(flags, 1, "{}: exactly one kind boolean", r.name);
            let expected =
                [r.is_tile, r.is_card, r.is_media, r.is_stat][["tile", "card", "media", "stat"]
                    .iter()
                    .position(|k| *k == r.kind)
                    .unwrap()];
            assert!(expected, "{}: kind/boolean mismatch", r.name);
            // The one nesting level: promo on EVERY row, label == badge, price = 9 + i.
            assert_eq!(r.promo.label, r.badge);
        }
        // Delta and promo-price pins: i = 0 → -3 / 9; i = 47 → 47 % 7 - 3 = 2 / 56.
        assert_eq!(rows[0].delta, -3);
        assert_eq!(rows[0].promo.price, 9);
        assert_eq!(rows[47].delta, 2);
        assert_eq!(rows[47].promo.price, 56);
    }

    #[test]
    fn mixed_has_36_products_with_12_on_sale() {
        let model = mixed();
        assert_eq!(model.products.len(), 36);
        assert_eq!(model.products.iter().filter(|p| p.on_sale).count(), 12);
        assert_eq!(model.page_title, "Mercantile - Catalog");
        assert_eq!(model.support_email, "support at mercantile.example");
        assert!(model.show_banner);
        assert!(!model.show_debug_panel);
        assert_eq!(model.year, 2026);
        assert_eq!(model.products[0].name, "Product 01");
        // E21: ints replace the sku/blurb strings (`MX-{n}` / the blurb sentence are
        // template-composed).
        assert_eq!(model.products[0].sku_number, 1001);
        assert_eq!(model.products[0].batch, 1);
        assert_eq!(model.products[35].name, "Product 36");
        assert_eq!(model.products[35].sku_number, 1036);
        assert_eq!(model.products[35].batch, 36);
        assert_eq!(model.products[35].price, 950 + 36 * 7);
    }

    #[test]
    fn fortunes_has_12_pinned_rows() {
        let rows = fortunes();
        assert_eq!(rows.len(), 12);
        assert_eq!(rows[0].id, 1);
        assert_eq!(rows[11].id, 12);
        // Row 11: the exact XSS payload string.
        assert_eq!(
            rows[10].message,
            "<script>alert(\"This should not be displayed in a browser alert box.\");</script>"
        );
        // Row 12: the Japanese string.
        assert_eq!(rows[11].message, "フレームワークのベンチマーク");
        // Rows 4/8 em dashes are U+2014.
        assert!(rows[3].message.contains('\u{2014}'));
        assert!(rows[7].message.contains('\u{2014}'));
        assert_eq!(rows[2].message.matches('\'').count(), 1);
    }

    #[test]
    fn encoded_loop_has_5000_rows_with_pinned_row_0() {
        let rows = encoded_loop();
        assert_eq!(rows.len(), 5000);
        assert_eq!(rows[0].tag, "tag-0&'0'");
        assert_eq!(rows[0].name, "item <0> & \"co\"");
        assert_eq!(rows[0].comment, "'q' & <angle> \"d\" こんにちは 0");
        assert_eq!(rows[4999].tag, "tag-4999&'1'");
    }

    #[test]
    fn substitution_pins() {
        let model = substitution();
        assert_eq!(model.title, "Heddle Handbook");
        assert_eq!(model.sku, "HB-2001");
        assert_eq!(model.price, 4200);
        assert_eq!(model.summary, "A concise field guide to the engine.");
        assert_eq!(model.rating, "4.8");
    }

    #[test]
    fn composed_nav_loads_from_the_corpus_fixture_with_pinned_counts() {
        let nav = &composed().nav;
        // Two mega menus of six tabs each; four footer columns (E20).
        assert_eq!(nav.menus.len(), 2);
        for menu in &nav.menus {
            assert_eq!(menu.tabs.len(), 6);
        }
        assert_eq!(nav.footer_columns.len(), 4);
        // 12 dropdown tabs; 32 menu columns + 4 footer columns = 36; 220 + 25 = 245 links —
        // the counts the verifier derives from this same model.
        assert_eq!(
            nav.menus
                .iter()
                .flat_map(|m| &m.tabs)
                .filter(|t| t.has_dropdown)
                .count(),
            12
        );
        let menu_columns: usize = nav
            .menus
            .iter()
            .flat_map(|m| &m.tabs)
            .map(|t| t.columns.len())
            .sum();
        assert_eq!(menu_columns + nav.footer_columns.len(), 36);
        let menu_links: usize = nav
            .menus
            .iter()
            .flat_map(|m| &m.tabs)
            .flat_map(|t| &t.columns)
            .flat_map(|c| &c.sections)
            .map(|s| s.links.len())
            .sum();
        let footer_links: usize = nav
            .footer_columns
            .iter()
            .flat_map(|c| &c.sections)
            .map(|s| s.links.len())
            .sum();
        assert_eq!(menu_links + footer_links, 245);
        // Pinned entries: the first tab and the footer's unique privacy deep link.
        let tab0 = &nav.menus[0].tabs[0];
        assert_eq!(tab0.label, "Shop All Products");
        assert_eq!(tab0.css, "shop-all-products");
        assert!(tab0.has_dropdown);
        assert_eq!(tab0.dropdown_css, "dropdown_2columns");
        assert_eq!(nav.footer_columns[0].sections[0].title, "Need Help?");
        let privacy: Vec<&NavLink> = nav
            .footer_columns
            .iter()
            .flat_map(|c| &c.sections)
            .flat_map(|s| &s.links)
            .filter(|l| l.href == "/content/privacy-policy")
            .collect();
        assert_eq!(privacy.len(), 1);
        assert_eq!(privacy[0].label, "Privacy Policy");
    }

    #[test]
    fn composed_nav_values_are_rule_4_clean() {
        // workloads.md rule 4 / E20 sanitization: every nav text value is ASCII with none of
        // `& < > " '` — raw and would-be-escaped renderings coincide on every engine.
        fn assert_clean(context: &str, s: &str) {
            assert!(
                s.is_ascii() && !s.contains(['&', '<', '>', '"', '\'']),
                "rule-4 violation in {context}: {s:?}"
            );
        }
        fn check_column(col: &NavColumn) {
            for s in &col.sections {
                assert_clean("section title", &s.title);
                assert_clean("section href", &s.href);
                for l in &s.links {
                    assert_clean("link label", &l.label);
                    assert_clean("link href", &l.href);
                }
            }
        }
        let nav = &composed().nav;
        for tab in nav.menus.iter().flat_map(|m| &m.tabs) {
            assert_clean("tab label", &tab.label);
            assert_clean("tab href", &tab.href);
            assert_clean("tab css", &tab.css);
            assert_clean("tab dropdown_css", &tab.dropdown_css);
            tab.columns.iter().for_each(check_column);
        }
        nav.footer_columns.iter().for_each(check_column);
    }
}
