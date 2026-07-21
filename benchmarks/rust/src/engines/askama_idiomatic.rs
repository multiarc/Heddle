//! Idiomatic-track Askama runners (WI5). Authoring standard per README D8/Q1.7 — every
//! template file carries a doc-citation header comment; this declaration file's patterns
//! follow the Askama 0.16 book pages cited per workload below: *Getting started* /
//! *Creating templates* (derive structs with `#[template(path = …)]`), *Template syntax —
//! Template inheritance* (composed-page, mixed-page), *Template syntax — For / If /
//! Include*, *Filters — safe* (composed-page's trusted fragments only) and *Filters —
//! escape* (default `Html` escaper everywhere — no `escape` override on any struct;
//! README D3 quadrants 3–4). Askama compiles templates at build time, so parse/compile
//! sits outside every `render()`; the structs are `OnceLock` singletons borrowing the
//! shared models.

use std::collections::HashMap;
use std::sync::OnceLock;

use askama::Template;

use crate::models;
use crate::models::{
    ConditionalRow, EncodedLoopRow, FortuneRow, FragmentRow, LoopRow, MixedProduct,
};

// ---- composed-page (raw; inheritance, `safe` on the trusted fragments) -----------------------
// Doc citations: Askama book *Template syntax — Template inheritance*, *Filters — safe*.

#[derive(Template)]
#[template(path = "idiomatic/askama/composed-page.html")]
pub struct ComposedIdiomatic<'a> {
    pub section_meta: &'a str,
    pub section_social: &'a str,
    pub section_page_scripts: &'a str,
    pub section_endpage_scripts: &'a str,
    pub comp_assets_styles: &'a str,
    pub comp_custom_styles: &'a str,
    pub comp_head_scripts: &'a str,
    pub comp_body_scripts: &'a str,
    pub comp_assets_scripts: &'a str,
    pub comp_body_end_scripts: &'a str,
    pub area_names: &'a [String],
    pub areas: &'a HashMap<String, String>,
}

impl ComposedIdiomatic<'_> {
    /// The area lookup (README D7 — bracket indexing with a variable key is not a
    /// documented Askama construct; a `self` method call is).
    fn area(&self, name: &str) -> &str {
        &self.areas[name]
    }
}

pub fn composed() -> &'static ComposedIdiomatic<'static> {
    static RUNNER: OnceLock<ComposedIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| {
        let m = models::composed();
        ComposedIdiomatic {
            section_meta: m.section_meta,
            section_social: m.section_social,
            section_page_scripts: m.section_page_scripts,
            section_endpage_scripts: m.section_endpage_scripts,
            comp_assets_styles: m.comp_assets_styles,
            comp_custom_styles: m.comp_custom_styles,
            comp_head_scripts: m.comp_head_scripts,
            comp_body_scripts: m.comp_body_scripts,
            comp_assets_scripts: m.comp_assets_scripts,
            comp_body_end_scripts: m.comp_body_end_scripts,
            area_names: &m.area_names,
            areas: &m.areas,
        }
    })
}

pub fn render_composed_page() -> String {
    composed()
        .render()
        .expect("askama idiomatic composed-page render")
}

// ---- trivial-substitution (raw) --------------------------------------------------------------
// Doc citations: Askama book *Getting started*, *Creating templates*.

#[derive(Template)]
#[template(path = "idiomatic/askama/trivial-substitution.html")]
pub struct SubstitutionIdiomatic<'a> {
    pub title: &'a str,
    pub sku: &'a str,
    pub price: i32,
    pub brand: &'a str,
    pub category: &'a str,
    pub availability: &'a str,
    pub url: &'a str,
    pub image_url: &'a str,
    pub summary: &'a str,
    pub rating: &'a str,
}

pub fn substitution() -> &'static SubstitutionIdiomatic<'static> {
    static RUNNER: OnceLock<SubstitutionIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| {
        let m = models::substitution();
        SubstitutionIdiomatic {
            title: m.title,
            sku: m.sku,
            price: m.price,
            brand: m.brand,
            category: m.category,
            availability: m.availability,
            url: m.url,
            image_url: m.image_url,
            summary: m.summary,
            rating: m.rating,
        }
    })
}

pub fn render_trivial_substitution() -> String {
    substitution()
        .render()
        .expect("askama idiomatic trivial-substitution render")
}

// ---- large-loop (raw) ------------------------------------------------------------------------
// Doc citations: Askama book *Template syntax — For*.

#[derive(Template)]
#[template(path = "idiomatic/askama/large-loop.html")]
pub struct LargeLoopIdiomatic<'a> {
    pub items: &'a [LoopRow],
}

pub fn large_loop() -> &'static LargeLoopIdiomatic<'static> {
    static RUNNER: OnceLock<LargeLoopIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| LargeLoopIdiomatic {
        items: models::large_loop(),
    })
}

pub fn render_large_loop() -> String {
    large_loop()
        .render()
        .expect("askama idiomatic large-loop render")
}

// ---- mixed-page (raw; inheritance) -----------------------------------------------------------
// Doc citations: Askama book *Template syntax — Template inheritance*, *For*, *If*.

#[derive(Template)]
#[template(path = "idiomatic/askama/mixed-page.html")]
pub struct MixedIdiomatic<'a> {
    pub page_title: &'a str,
    pub store_name: &'a str,
    pub hero_heading: &'a str,
    pub hero_tagline: &'a str,
    pub show_banner: bool,
    pub banner_text: &'a str,
    pub show_debug_panel: bool,
    pub footer_note: &'a str,
    pub year: i32,
    pub support_email: &'a str,
    pub products: &'a [MixedProduct],
}

pub fn mixed() -> &'static MixedIdiomatic<'static> {
    static RUNNER: OnceLock<MixedIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| {
        let m = models::mixed();
        MixedIdiomatic {
            page_title: m.page_title,
            store_name: m.store_name,
            hero_heading: m.hero_heading,
            hero_tagline: m.hero_tagline,
            show_banner: m.show_banner,
            banner_text: m.banner_text,
            show_debug_panel: m.show_debug_panel,
            footer_note: m.footer_note,
            year: m.year,
            support_email: m.support_email,
            products: &m.products,
        }
    })
}

pub fn render_mixed_page() -> String {
    mixed()
        .render()
        .expect("askama idiomatic mixed-page render")
}

// ---- conditional-heavy (raw) -----------------------------------------------------------------
// Doc citations: Askama book *Template syntax — If*, *For*.

#[derive(Template)]
#[template(path = "idiomatic/askama/conditional-heavy.html")]
pub struct ConditionalIdiomatic<'a> {
    pub rows: &'a [ConditionalRow],
}

pub fn conditional() -> &'static ConditionalIdiomatic<'static> {
    static RUNNER: OnceLock<ConditionalIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| ConditionalIdiomatic {
        rows: models::conditional(),
    })
}

pub fn render_conditional_heavy() -> String {
    conditional()
        .render()
        .expect("askama idiomatic conditional-heavy render")
}

// ---- fragment-heavy (raw; include-per-iteration) ---------------------------------------------
// Doc citations: Askama book *Template syntax — Include*, *For*.

#[derive(Template)]
#[template(path = "idiomatic/askama/fragment-heavy.html")]
pub struct FragmentIdiomatic<'a> {
    pub items: &'a [FragmentRow],
}

pub fn fragment() -> &'static FragmentIdiomatic<'static> {
    static RUNNER: OnceLock<FragmentIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| FragmentIdiomatic {
        items: models::fragment(),
    })
}

pub fn render_fragment_heavy() -> String {
    fragment()
        .render()
        .expect("askama idiomatic fragment-heavy render")
}

// ---- fortunes-encoded (encoded — default Html escaper, no filters) ---------------------------
// Doc citations: Askama book *Filters — escape*, *Template syntax — For*.

#[derive(Template)]
#[template(path = "idiomatic/askama/fortunes-encoded.html")]
pub struct FortunesIdiomatic<'a> {
    pub rows: &'a [FortuneRow],
}

pub fn fortunes() -> &'static FortunesIdiomatic<'static> {
    static RUNNER: OnceLock<FortunesIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| FortunesIdiomatic {
        rows: models::fortunes(),
    })
}

pub fn render_fortunes_encoded() -> String {
    fortunes()
        .render()
        .expect("askama idiomatic fortunes-encoded render")
}

// ---- encoded-loop (encoded — default Html escaper, no filters) -------------------------------
// Doc citations: Askama book *Filters — escape*, *Template syntax — For*.

#[derive(Template)]
#[template(path = "idiomatic/askama/encoded-loop.html")]
pub struct EncodedLoopIdiomatic<'a> {
    pub items: &'a [EncodedLoopRow],
}

pub fn encoded_loop() -> &'static EncodedLoopIdiomatic<'static> {
    static RUNNER: OnceLock<EncodedLoopIdiomatic<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| EncodedLoopIdiomatic {
        items: models::encoded_loop(),
    })
}

pub fn render_encoded_loop() -> String {
    encoded_loop()
        .render()
        .expect("askama idiomatic encoded-loop render")
}
