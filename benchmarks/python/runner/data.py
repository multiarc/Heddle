"""Workload models -- the Python construction of the Phase 1 model data (Phase 5 D6).

All eight workloads' render contexts as plain dicts with the Phase 1 snake_case keys,
materialized once at module import (module-level, per README D6). Workloads 3-8 are built
from the exact generation formulas pinned in Phase 1 `workloads.md`; `trivial-substitution`
is transcribed from the C# source (`SubstitutionContent.cs`).

`composed-page` (ledger E20/E22) carries ONLY structured navigation data under the `nav`
key, loaded once at import from the corpus fixture
`GoldenCorpus/fixtures/composed-page/nav.json` (the single source of truth the five
non-.NET ecosystems load from) via the same corpus-dir resolution the gate uses
(`gates.CORPUS_DIR`). Every fragment of literal page text -- chrome, blobs, asset/script
snippets -- lives in the TEMPLATES (E22: the model-preparation tier carries DATA only).

The model tier carries data, never display strings (E21): derived display text
(`row-{i}`, `MX-{sku_number}`, `note {seq}`, the blurb sentence, the media caption /
image src / display price) is composed by the templates as literal-plus-substitution.
Zero-padded identity names (`item-{i:02d}`, `unit-{i:03d}`, `Product {i:02d}`) and the
encoded-suite payloads stay model-side by design.

Numeric fields (`price`, `value`, `year`, `id`, `sku_number`, `batch`, `seq`, `delta`, ...)
are ints and render via `str(int)` (invariant by construction). `rating` is the pinned
STRING "4.8" -- a transcribed literal, not a formatted float. Parity is enforced by the
gate, not by trusting transcription (the byte gate is the transcription check).

Spec: docs/spec/cross-stack-benchmarks/phase-5-python/README.md (D6);
models normative in docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/workloads.md;
ledger E20/E21/E22 in docs/spec/records.md.
"""

import json

from .gates import CORPUS_DIR

# ---- composed-page (workloads.md workload 1, E20/E22: structured nav only) --------------------

_NAV_FIXTURE = CORPUS_DIR / "fixtures" / "composed-page" / "nav.json"


def _composed() -> dict:
    # Strict UTF-8, loaded once at import; schema (snake_case, declaration order):
    # {menus: [{tabs: [{label, href, css, has_dropdown, dropdown_css,
    #   columns: [{sections: [{title, href, title_linked, links: [{label, href}]}]}]}]}],
    #  footer_columns: [<column shape>]}
    with _NAV_FIXTURE.open(encoding="utf-8") as f:
        return {"nav": json.load(f)}


# ---- trivial-substitution (SubstitutionContent.cs) --------------------------------------------


def _substitution() -> dict:
    return {
        "title": "Heddle Handbook",
        "sku": "HB-2001",
        "price": 4200,
        "brand": "Heddle Press",
        "category": "Reference",
        "availability": "In stock",
        "url": "/catalog/handbook",
        "image_url": "/img/handbook.png",
        "summary": "A concise field guide to the engine.",
        "rating": "4.8",  # pinned string literal, never a formatted float
    }


# ---- large-loop (LoopContent.cs: RowCount = 5000, Value = i; E21: templates compose row-@(Value)) ----


def _large_loop() -> dict:
    return {"items": [{"value": i} for i in range(5000)]}


# ---- mixed-page (workloads.md workload 4; 36 products, i in [1, 36]) --------------------------


def _mixed() -> dict:
    return {
        "page_title": "Mercantile - Catalog",
        "store_name": "Mercantile",
        "hero_heading": "Autumn hardware sale",
        "hero_tagline": "Hand-picked tools, fair prices, shipped tomorrow.",
        "show_banner": True,
        "banner_text": "Free shipping on orders over 60.",
        "show_debug_panel": False,
        "footer_note": "Prices include VAT where applicable.",
        "year": 2026,
        "support_email": "support at mercantile.example",
        "products": [
            {
                "name": f"Product {i:02d}",
                # E21: templates compose the display SKU (MX-<sku_number>) and the
                # blurb sentence around <batch>.
                "sku_number": 1000 + i,
                "price": 950 + i * 7,
                "on_sale": i % 3 == 0,
                "batch": i,
            }
            for i in range(1, 37)
        ],
    }


# ---- conditional-heavy (workloads.md workload 5; 200 rows, i in [0, 199]) ---------------------


def _conditional() -> dict:
    return {
        "rows": [
            {
                "name": f"unit-{i:03d}",
                "seq": i,  # E21: templates compose the note text (note <seq>)
                "is_bronze": i % 4 == 0,
                "is_silver": i % 4 == 1,
                "is_gold": i % 4 == 2,
                "has_note": i % 2 == 0,
                "is_active": i % 5 != 0,
            }
            for i in range(200)
        ]
    }


# ---- fragment-heavy (workloads.md workload 6, E20; 48 rows, i in [0, 47]) ---------------------


def _fragment() -> dict:
    kinds = ["tile", "card", "media", "stat"]
    badges = ["new", "hot", "sale", "std"]
    items = []
    for i in range(48):
        kind = kinds[i % 4]
        badge = badges[i % 4]
        items.append(
            {
                "kind": kind,  # informational; engines dispatch on the booleans
                "is_tile": kind == "tile",
                "is_card": kind == "card",
                "is_media": kind == "media",
                "is_stat": kind == "stat",
                "name": f"item-{i:02d}",  # identity data -- the one padded value
                "value": i * 11,
                "badge": badge,
                "delta": i % 7 - 3,
                # On EVERY row (no engine needs a null guard); price is an int --
                # E21: templates compose the caption, image src, and display price.
                "promo": {"label": badge, "price": 9 + i},
            }
        )
    return {"items": items}


# ---- fortunes-encoded (workloads.md workload 7; the 12 pinned messages, byte-for-byte) --------

# Rows 4 and 8 carry U+2014 em dashes; row 11 is the TechEmpower XSS payload; row 12 the
# Japanese string (non-ASCII pinned as escapes so the source is ASCII-stable).
_FORTUNE_MESSAGES = [
    "A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1",
    "A computer program does what you tell it to do, not what you want it to do.",
    "A computer scientist is someone who fixes things that aren't broken.",
    "A list is only as strong as its weakest link. \u2014 Donald Knuth",
    "After enough decimal places, nobody gives a damn.",
    "Any program that runs right is obsolete.",
    "Computers make very fast, very accurate mistakes.",
    "Emacs is a nice operating system, but I prefer UNIX. \u2014 Tom Christiansen",
    "Feature: A bug with seniority.",
    "fortune: No such file or directory",
    '<script>alert("This should not be displayed in a browser alert box.");</script>',
    "\u30d5\u30ec\u30fc\u30e0\u30ef\u30fc\u30af\u306e\u30d9\u30f3\u30c1\u30de\u30fc\u30af",
]


def _fortunes() -> dict:
    return {
        "rows": [
            {"id": i + 1, "message": message}
            for i, message in enumerate(_FORTUNE_MESSAGES)
        ]
    }


# ---- encoded-loop (workloads.md workload 8; 5000 rows, i in [0, 4999]) ------------------------


def _encoded_loop() -> dict:
    hiragana = "\u3053\u3093\u306b\u3061\u306f"  # こんにちは
    return {
        "items": [
            {
                "tag": f"tag-{i}&'{i % 7}'",
                "name": f'item <{i}> & "co"',
                "comment": f"'q' & <angle> \"d\" {hiragana} {i}",
            }
            for i in range(5000)
        ]
    }


# ---- the module-level registry (built once per process) ---------------------------------------

MODELS: dict[str, dict] = {
    "composed-page": _composed(),
    "trivial-substitution": _substitution(),
    "large-loop": _large_loop(),
    "mixed-page": _mixed(),
    "conditional-heavy": _conditional(),
    "fragment-heavy": _fragment(),
    "fortunes-encoded": _fortunes(),
    "encoded-loop": _encoded_loop(),
}
