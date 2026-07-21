//! Controlled-track Askama runners (WI4). Template texts are normative in
//! `docs/spec/cross-stack-benchmarks/phase-2-rust/workload-ports.md`; escaping modes per
//! README D3 — raw cells carry `escape = "none"`, encoded cells use the default `Html`
//! escaper inferred from the `.html` extension (spellings reconciled by N5 in the gate, D4).
//! Askama compiles every template into the binary at build time, so "parse/compile outside
//! `render()`" holds by construction; the template structs themselves are `OnceLock`
//! singletons borrowing the shared models.

use std::collections::HashMap;
use std::sync::OnceLock;

use askama::Template;

use crate::models;
use crate::models::{
    ConditionalRow, EncodedLoopRow, FortuneRow, FragmentRow, LoopRow, MixedProduct,
};

// ---- composed-page (raw) ---------------------------------------------------------------------

#[derive(Template)]
#[template(path = "controlled/askama/composed-page.html", escape = "none")]
pub struct ComposedControlled<'a> {
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

impl ComposedControlled<'_> {
    /// The area lookup (README D7 — bracket indexing with a variable key is not a documented
    /// Askama construct; a `self` method call is).
    fn area(&self, name: &str) -> &str {
        &self.areas[name]
    }
}

pub fn composed() -> &'static ComposedControlled<'static> {
    static RUNNER: OnceLock<ComposedControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| {
        let m = models::composed();
        ComposedControlled {
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
        .expect("askama controlled composed-page render")
}

// ---- trivial-substitution (raw) --------------------------------------------------------------

#[derive(Template)]
#[template(path = "controlled/askama/trivial-substitution.html", escape = "none")]
pub struct SubstitutionControlled<'a> {
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

pub fn substitution() -> &'static SubstitutionControlled<'static> {
    static RUNNER: OnceLock<SubstitutionControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| {
        let m = models::substitution();
        SubstitutionControlled {
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
        .expect("askama controlled trivial-substitution render")
}

// ---- large-loop (raw) ------------------------------------------------------------------------

#[derive(Template)]
#[template(path = "controlled/askama/large-loop.html", escape = "none")]
pub struct LargeLoopControlled<'a> {
    pub items: &'a [LoopRow],
}

pub fn large_loop() -> &'static LargeLoopControlled<'static> {
    static RUNNER: OnceLock<LargeLoopControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| LargeLoopControlled {
        items: models::large_loop(),
    })
}

pub fn render_large_loop() -> String {
    large_loop()
        .render()
        .expect("askama controlled large-loop render")
}

// ---- mixed-page (raw) ------------------------------------------------------------------------

#[derive(Template)]
#[template(path = "controlled/askama/mixed-page.html", escape = "none")]
pub struct MixedControlled<'a> {
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

pub fn mixed() -> &'static MixedControlled<'static> {
    static RUNNER: OnceLock<MixedControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| {
        let m = models::mixed();
        MixedControlled {
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
        .expect("askama controlled mixed-page render")
}

// ---- conditional-heavy (raw) -----------------------------------------------------------------

#[derive(Template)]
#[template(path = "controlled/askama/conditional-heavy.html", escape = "none")]
pub struct ConditionalControlled<'a> {
    pub rows: &'a [ConditionalRow],
}

pub fn conditional() -> &'static ConditionalControlled<'static> {
    static RUNNER: OnceLock<ConditionalControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| ConditionalControlled {
        rows: models::conditional(),
    })
}

pub fn render_conditional_heavy() -> String {
    conditional()
        .render()
        .expect("askama controlled conditional-heavy render")
}

// ---- fragment-heavy (raw) --------------------------------------------------------------------

#[derive(Template)]
#[template(path = "controlled/askama/fragment-heavy.html", escape = "none")]
pub struct FragmentControlled<'a> {
    pub items: &'a [FragmentRow],
}

pub fn fragment() -> &'static FragmentControlled<'static> {
    static RUNNER: OnceLock<FragmentControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| FragmentControlled {
        items: models::fragment(),
    })
}

pub fn render_fragment_heavy() -> String {
    fragment()
        .render()
        .expect("askama controlled fragment-heavy render")
}

// ---- fortunes-encoded (encoded — default Html escaper, no `escape` override) -----------------

#[derive(Template)]
#[template(path = "controlled/askama/fortunes-encoded.html")]
pub struct FortunesControlled<'a> {
    pub rows: &'a [FortuneRow],
}

pub fn fortunes() -> &'static FortunesControlled<'static> {
    static RUNNER: OnceLock<FortunesControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| FortunesControlled {
        rows: models::fortunes(),
    })
}

pub fn render_fortunes_encoded() -> String {
    fortunes()
        .render()
        .expect("askama controlled fortunes-encoded render")
}

// ---- encoded-loop (encoded — default Html escaper, no `escape` override) ---------------------

#[derive(Template)]
#[template(path = "controlled/askama/encoded-loop.html")]
pub struct EncodedLoopControlled<'a> {
    pub items: &'a [EncodedLoopRow],
}

pub fn encoded_loop() -> &'static EncodedLoopControlled<'static> {
    static RUNNER: OnceLock<EncodedLoopControlled<'static>> = OnceLock::new();
    RUNNER.get_or_init(|| EncodedLoopControlled {
        items: models::encoded_loop(),
    })
}

pub fn render_encoded_loop() -> String {
    encoded_loop()
        .render()
        .expect("askama controlled encoded-loop render")
}
