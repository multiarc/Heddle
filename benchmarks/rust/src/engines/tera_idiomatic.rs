//! Idiomatic-track Tera runners. Every template file carries a header comment citing the
//! official Tera docs (keats.github.io/tera) pages its patterns follow: *Getting
//! started* (one `Tera` instance holding the parsed template set), *Inheritance*
//! (composed-page — base with a live body block; mixed-page is single-file by rule —
//! layout composition is composed-page's dimension), *Control structures* (for / if),
//! *Include* (composed-page chrome fragments + nested nav partials, fragment-heavy
//! per-kind partials) and *Auto-escaping* (ONE default-escaping instance — `.html` names
//! keep autoescape on; no `safe` filter anywhere — the inert chrome is literal template
//! text, and every raw-suite substitution contains no characters HTML escaping rewrites,
//! so default escaping is byte-neutral).
//! Templates are registered with `add_template_file` and contexts built once, so
//! parse/compile sits outside every `render()`.

use std::path::Path;
use std::sync::OnceLock;

use tera::{Context, Tera};

use crate::models;

// ---- the single default-escaping idiomatic instance ------------------------------------------

const IDIOMATIC_TEMPLATES: [&str; 29] = [
    // composed-page: chrome-fragment includes, the nested nav partial chain,
    // the base layout with the live body block, and the extending child page.
    "idiomatic/tera/chrome/alert-top.html",
    "idiomatic/tera/chrome/secondary-wholesale-menu.html",
    "idiomatic/tera/chrome/secondary-retail-menu.html",
    "idiomatic/tera/chrome/alert-below.html",
    "idiomatic/tera/chrome/assets-styles.html",
    "idiomatic/tera/chrome/assets-scripts.html",
    "idiomatic/tera/chrome/custom-styles.html",
    "idiomatic/tera/chrome/head-scripts.html",
    "idiomatic/tera/chrome/body-scripts.html",
    "idiomatic/tera/chrome/body-end-scripts.html",
    "idiomatic/tera/nav/link.html",
    "idiomatic/tera/nav/section.html",
    "idiomatic/tera/nav/column.html",
    "idiomatic/tera/nav/mega-menu.html",
    "idiomatic/tera/shared/composed-page-base.html",
    "idiomatic/tera/composed-page.html",
    "idiomatic/tera/trivial-substitution.html",
    "idiomatic/tera/large-loop.html",
    // mixed-page is single-file by rule — layout composition is
    // composed-page's dimension.
    "idiomatic/tera/mixed-page.html",
    "idiomatic/tera/conditional-heavy.html",
    // fragment-heavy: four dispatched per-kind partials plus the card's two
    // sub-partials, then the dispatching main template.
    "idiomatic/tera/shared/fragment-heavy-tile.html",
    "idiomatic/tera/shared/fragment-heavy-badge.html",
    "idiomatic/tera/shared/fragment-heavy-price.html",
    "idiomatic/tera/shared/fragment-heavy-card.html",
    "idiomatic/tera/shared/fragment-heavy-media-row.html",
    "idiomatic/tera/shared/fragment-heavy-stat.html",
    "idiomatic/tera/fragment-heavy.html",
    "idiomatic/tera/fortunes-encoded.html",
    "idiomatic/tera/encoded-loop.html",
];

/// The idiomatic instance — default autoescape (on for `.html` names — the documented
/// practitioner posture).
pub fn tera_idiomatic() -> &'static Tera {
    static TERA: OnceLock<Tera> = OnceLock::new();
    TERA.get_or_init(build_fresh)
}

/// Cold-parse support: a fresh instance parsing the same 29 template files the
/// runtime `tera_idiomatic` instance holds.
pub fn build_fresh() -> Tera {
    let mut tera = Tera::default();
    let base = Path::new(env!("CARGO_MANIFEST_DIR")).join("templates");
    for name in IDIOMATIC_TEMPLATES {
        tera.add_template_file(base.join(name), Some(name))
            .unwrap_or_else(|e| panic!("tera idiomatic: cannot register {name}: {e}"));
    }
    tera
}

// ---- contexts (built once from the shared models) --------------------------------------------

fn composed_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        Context::from_serialize(models::composed()).expect("tera idiomatic composed-page context")
    })
}

fn substitution_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        Context::from_serialize(models::substitution())
            .expect("tera idiomatic trivial-substitution context")
    })
}

fn large_loop_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        let mut ctx = Context::new();
        ctx.insert("items", models::large_loop());
        ctx
    })
}

fn mixed_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        Context::from_serialize(models::mixed()).expect("tera idiomatic mixed-page context")
    })
}

fn conditional_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        let mut ctx = Context::new();
        ctx.insert("rows", models::conditional());
        ctx
    })
}

fn fragment_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        let mut ctx = Context::new();
        ctx.insert("items", models::fragment());
        ctx
    })
}

fn fortunes_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        let mut ctx = Context::new();
        ctx.insert("rows", models::fortunes());
        ctx
    })
}

fn encoded_loop_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        let mut ctx = Context::new();
        ctx.insert("items", models::encoded_loop());
        ctx
    })
}

// ---- render fns ------------------------------------------------------------------------------

fn render(name: &str, ctx: &Context) -> String {
    tera_idiomatic()
        .render(name, ctx)
        .unwrap_or_else(|e| panic!("tera idiomatic {name} render: {e}"))
}

pub fn render_composed_page() -> String {
    render("idiomatic/tera/composed-page.html", composed_context())
}

pub fn render_trivial_substitution() -> String {
    render(
        "idiomatic/tera/trivial-substitution.html",
        substitution_context(),
    )
}

pub fn render_large_loop() -> String {
    render("idiomatic/tera/large-loop.html", large_loop_context())
}

pub fn render_mixed_page() -> String {
    render("idiomatic/tera/mixed-page.html", mixed_context())
}

pub fn render_conditional_heavy() -> String {
    render(
        "idiomatic/tera/conditional-heavy.html",
        conditional_context(),
    )
}

pub fn render_fragment_heavy() -> String {
    render("idiomatic/tera/fragment-heavy.html", fragment_context())
}

pub fn render_fortunes_encoded() -> String {
    render("idiomatic/tera/fortunes-encoded.html", fortunes_context())
}

pub fn render_encoded_loop() -> String {
    render("idiomatic/tera/encoded-loop.html", encoded_loop_context())
}
