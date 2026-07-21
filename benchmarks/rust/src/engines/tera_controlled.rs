//! Controlled-track Tera runners (WI4). Template texts are normative in
//! `docs/spec/cross-stack-benchmarks/phase-2-rust/workload-ports.md`; escaping modes per
//! README D3 — TWO `OnceLock` instances: `tera_controlled_raw` with
//! `autoescape_on(std::iter::empty::<&str>())` (the documented disable form) and
//! `tera_controlled_encoded` with default autoescape (template names end `.html`).
//! Templates are registered with `add_template_file` and contexts are built once, so
//! parse/compile sits outside every `render()`.

use std::path::Path;
use std::sync::OnceLock;

use tera::{Context, Tera};

use crate::models;

// ---- instances (README D3) -------------------------------------------------------------------

const RAW_TEMPLATES: [&str; 8] = [
    "controlled/tera/composed-page-layout.html",
    "controlled/tera/composed-page.html",
    "controlled/tera/trivial-substitution.html",
    "controlled/tera/large-loop.html",
    "controlled/tera/mixed-page.html",
    "controlled/tera/conditional-heavy.html",
    "controlled/tera/fragment-heavy-tile.html",
    "controlled/tera/fragment-heavy.html",
];

const ENCODED_TEMPLATES: [&str; 2] = [
    "controlled/tera/fortunes-encoded.html",
    "controlled/tera/encoded-loop.html",
];

fn build_instance(names: &[&str], autoescape_off: bool) -> Tera {
    let mut tera = Tera::default();
    if autoescape_off {
        tera.autoescape_on(std::iter::empty::<&str>());
    }
    let base = Path::new(env!("CARGO_MANIFEST_DIR")).join("templates");
    for name in names {
        tera.add_template_file(base.join(name), Some(name))
            .unwrap_or_else(|e| panic!("tera controlled: cannot register {name}: {e}"));
    }
    tera
}

/// Cold-parse support (README D12): a fresh raw instance parsing the same 8 template files
/// the runtime `tera_controlled_raw` instance holds.
pub fn build_fresh_raw() -> Tera {
    build_instance(&RAW_TEMPLATES, true)
}

/// Cold-parse support (README D12): a fresh encoded instance parsing the same 2 template
/// files the runtime `tera_controlled_encoded` instance holds.
pub fn build_fresh_encoded() -> Tera {
    build_instance(&ENCODED_TEMPLATES, false)
}

/// The controlled-raw instance — autoescape fully off (README D3 quadrant 1).
pub fn tera_controlled_raw() -> &'static Tera {
    static TERA: OnceLock<Tera> = OnceLock::new();
    TERA.get_or_init(|| build_instance(&RAW_TEMPLATES, true))
}

/// The controlled-encoded instance — default autoescape on `.html` names (D3 quadrant 2).
pub fn tera_controlled_encoded() -> &'static Tera {
    static TERA: OnceLock<Tera> = OnceLock::new();
    TERA.get_or_init(|| build_instance(&ENCODED_TEMPLATES, false))
}

// ---- contexts (built once from the shared models) --------------------------------------------

fn composed_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        Context::from_serialize(models::composed()).expect("tera composed-page context")
    })
}

fn substitution_context() -> &'static Context {
    static CTX: OnceLock<Context> = OnceLock::new();
    CTX.get_or_init(|| {
        Context::from_serialize(models::substitution()).expect("tera trivial-substitution context")
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
    CTX.get_or_init(|| Context::from_serialize(models::mixed()).expect("tera mixed-page context"))
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

fn render_raw(name: &str, ctx: &Context) -> String {
    tera_controlled_raw()
        .render(name, ctx)
        .unwrap_or_else(|e| panic!("tera controlled raw {name} render: {e}"))
}

fn render_encoded(name: &str, ctx: &Context) -> String {
    tera_controlled_encoded()
        .render(name, ctx)
        .unwrap_or_else(|e| panic!("tera controlled encoded {name} render: {e}"))
}

pub fn render_composed_page() -> String {
    render_raw("controlled/tera/composed-page.html", composed_context())
}

pub fn render_trivial_substitution() -> String {
    render_raw(
        "controlled/tera/trivial-substitution.html",
        substitution_context(),
    )
}

pub fn render_large_loop() -> String {
    render_raw("controlled/tera/large-loop.html", large_loop_context())
}

pub fn render_mixed_page() -> String {
    render_raw("controlled/tera/mixed-page.html", mixed_context())
}

pub fn render_conditional_heavy() -> String {
    render_raw(
        "controlled/tera/conditional-heavy.html",
        conditional_context(),
    )
}

pub fn render_fragment_heavy() -> String {
    render_raw("controlled/tera/fragment-heavy.html", fragment_context())
}

pub fn render_fortunes_encoded() -> String {
    render_encoded("controlled/tera/fortunes-encoded.html", fortunes_context())
}

pub fn render_encoded_loop() -> String {
    render_encoded("controlled/tera/encoded-loop.html", encoded_loop_context())
}
