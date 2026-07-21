//! Shared workload models — the Rust construction of the Phase 1 model data, normative in
//! `docs/spec/cross-stack-benchmarks/phase-2-rust/workload-ports.md` §Model construction
//! (which mirrors Phase 1 `workloads.md`). One module builds every model exactly once
//! (`OnceLock`), mirroring the .NET `Shared`-instance discipline. All numeric formatting is
//! `i32` `Display` (no locale, matching C# invariant `int` formatting).

use std::collections::HashMap;
use std::sync::OnceLock;

use serde::Serialize;

// ---- composed-page ---------------------------------------------------------------------------

/// Fragment data files, copied byte-exactly from `TwinContent.cs` / `AreaComponent.cs`
/// (`data/composed-page/*`, pinned `-text` in `.gitattributes`).
pub const SECTION_META: &str = include_str!("../data/composed-page/section-meta.html");
pub const SECTION_SOCIAL: &str = include_str!("../data/composed-page/section-social.html");
pub const COMP_ASSETS_STYLES: &str = include_str!("../data/composed-page/comp-assets-styles.html");
pub const COMP_CUSTOM_STYLES: &str = include_str!("../data/composed-page/comp-custom-styles.html");
pub const COMP_HEAD_SCRIPTS: &str = include_str!("../data/composed-page/comp-head-scripts.html");
pub const COMP_BODY_SCRIPTS: &str = include_str!("../data/composed-page/comp-body-scripts.html");
pub const COMP_ASSETS_SCRIPTS: &str =
    include_str!("../data/composed-page/comp-assets-scripts.html");
pub const COMP_BODY_END_SCRIPTS: &str =
    include_str!("../data/composed-page/comp-body-end-scripts.html");

/// The seven area fragments in `TwinContent.AreaOrder` order
/// (`area-6.html` — "Alert Top Section Below Nav" — is the pinned empty entry).
const AREA_FRAGMENTS: [&str; 7] = [
    include_str!("../data/composed-page/area-1.html"),
    include_str!("../data/composed-page/area-2.html"),
    include_str!("../data/composed-page/area-3.html"),
    include_str!("../data/composed-page/area-4.html"),
    include_str!("../data/composed-page/area-5.html"),
    include_str!("../data/composed-page/area-6.html"),
    include_str!("../data/composed-page/area-7.html"),
];

/// The area names in the exact order `layout.heddle` issues them (`TwinContent.AreaOrder`).
pub const AREA_ORDER: [&str; 7] = [
    "Alert Top Section Above Nav",
    "Secondary Wholesale Menu",
    "Secondary Retail Menu",
    "Wholesale Top Mega Menu",
    "Retail Top Mega Menu",
    "Alert Top Section Below Nav", // empty content
    "Footer Links",
];

#[derive(Serialize)]
pub struct ComposedModel {
    pub section_meta: &'static str,
    pub section_social: &'static str,
    pub section_page_scripts: &'static str,
    pub section_endpage_scripts: &'static str,
    pub comp_assets_styles: &'static str,
    pub comp_custom_styles: &'static str,
    pub comp_head_scripts: &'static str,
    pub comp_body_scripts: &'static str,
    pub comp_assets_scripts: &'static str,
    pub comp_body_end_scripts: &'static str,
    pub area_names: Vec<String>,
    pub areas: HashMap<String, String>,
}

pub fn composed() -> &'static ComposedModel {
    static MODEL: OnceLock<ComposedModel> = OnceLock::new();
    MODEL.get_or_init(|| ComposedModel {
        section_meta: SECTION_META,
        section_social: SECTION_SOCIAL,
        section_page_scripts: "",
        section_endpage_scripts: "",
        comp_assets_styles: COMP_ASSETS_STYLES,
        comp_custom_styles: COMP_CUSTOM_STYLES,
        comp_head_scripts: COMP_HEAD_SCRIPTS,
        comp_body_scripts: COMP_BODY_SCRIPTS,
        comp_assets_scripts: COMP_ASSETS_SCRIPTS,
        comp_body_end_scripts: COMP_BODY_END_SCRIPTS,
        area_names: AREA_ORDER.iter().map(|n| n.to_string()).collect(),
        areas: AREA_ORDER
            .iter()
            .zip(AREA_FRAGMENTS.iter())
            .map(|(name, fragment)| (name.to_string(), fragment.to_string()))
            .collect(),
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

#[derive(Serialize)]
pub struct LoopRow {
    pub name: String,
    pub value: i32,
}

pub fn large_loop() -> &'static Vec<LoopRow> {
    static MODEL: OnceLock<Vec<LoopRow>> = OnceLock::new();
    MODEL.get_or_init(|| {
        (0..5000)
            .map(|i: i32| LoopRow {
                name: format!("row-{i}"),
                value: i,
            })
            .collect()
    })
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

#[derive(Serialize)]
pub struct MixedProduct {
    pub name: String,
    pub sku: String,
    pub price: i32,
    pub on_sale: bool,
    pub blurb: String,
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
                sku: format!("MX-{}", 1000 + i),
                price: 950 + i * 7,
                on_sale: i % 3 == 0,
                blurb: format!(
                    "A dependable workshop staple from batch {i}, checked for daily use and backed by our lifetime guarantee."
                ),
            })
            .collect(),
    })
}

// ---- conditional-heavy -----------------------------------------------------------------------

#[derive(Serialize)]
pub struct ConditionalRow {
    pub name: String,
    pub note: String,
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
                note: format!("note {i}"),
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

#[derive(Serialize)]
pub struct FragmentRow {
    pub name: String,
    pub value: i32,
    pub badge: &'static str,
}

pub fn fragment() -> &'static Vec<FragmentRow> {
    static MODEL: OnceLock<Vec<FragmentRow>> = OnceLock::new();
    MODEL.get_or_init(|| {
        (0..48)
            .map(|i: i32| FragmentRow {
                name: format!("tile-{i:02}"),
                value: i * 11,
                badge: ["new", "hot", "sale", "std"][(i % 4) as usize],
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
        assert_eq!(rows[0].name, "row-0");
        assert_eq!(rows[0].value, 0);
        assert_eq!(rows[4999].name, "row-4999");
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
        assert_eq!(rows[7].note, "note 7");
    }

    #[test]
    fn fragment_has_48_rows_with_pinned_badges() {
        let rows = fragment();
        assert_eq!(rows.len(), 48);
        assert_eq!(rows[0].name, "tile-00");
        assert_eq!(rows[47].name, "tile-47");
        assert_eq!(rows[47].value, 47 * 11);
        assert_eq!(rows.iter().filter(|r| r.badge == "new").count(), 12);
        assert_eq!(rows[1].badge, "hot");
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
        assert_eq!(model.products[0].sku, "MX-1001");
        assert_eq!(model.products[35].name, "Product 36");
        assert_eq!(model.products[35].sku, "MX-1036");
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
    fn composed_fragments_are_nonempty_except_area_6() {
        let model = composed();
        assert_eq!(model.area_names.len(), 7);
        assert_eq!(model.areas.len(), 7);
        for (i, name) in AREA_ORDER.iter().enumerate() {
            let fragment = &model.areas[*name];
            if *name == "Alert Top Section Below Nav" {
                assert!(fragment.is_empty(), "area-6 must be the empty entry");
            } else {
                assert!(!fragment.is_empty(), "area-{} must be non-empty", i + 1);
            }
        }
        assert_eq!(model.section_meta, "<title>Title</title>");
        assert!(
            model
                .section_social
                .starts_with("<meta property=\"og:image\"")
        );
        assert!(model.section_page_scripts.is_empty());
        assert!(model.section_endpage_scripts.is_empty());
        assert_eq!(model.comp_custom_styles, "/* CSS Comment Test */");
    }
}
