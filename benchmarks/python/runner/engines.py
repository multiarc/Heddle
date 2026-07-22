"""Engine wiring -- the four engine objects and per-track template loading (Phase 5 WI3).

Raw workloads render through each engine's untouched non-escaping default output path;
encoded workloads through one engine-level escaping configuration each (README D4):

- ``jinja2-raw``     -- ``Environment(autoescape=False)`` with the library whitespace
  defaults pinned explicitly in code (D3: ``trim_blocks=False``, ``lstrip_blocks=False``,
  ``keep_trailing_newline=False``).
- ``jinja2-encoded`` -- a separate ``Environment`` identical but ``autoescape=True``
  (controlled track; the idiomatic environment uses ``select_autoescape()`` -- D5).
- ``mako-raw``       -- ``TemplateLookup`` with ``default_filters`` left at its ``None``
  default (internally ``["str"]``).
- ``mako-encoded``   -- a separate ``TemplateLookup(default_filters=["h"])`` (controlled
  track; idiomatic encoded templates carry ``<%page expression_filter="h"/>`` -- D5).

Nothing else differs between raw and encoded engine objects. No escape-bypass syntax
exists anywhere in the templates (D4); no per-expression escape filter appears in
controlled encoded templates.

Spec: docs/spec/cross-stack-benchmarks/phase-5-python/README.md (D3/D4/D5);
      docs/spec/cross-stack-benchmarks/phase-5-python/templates.md (engine wiring);
      docs/spec/cross-stack-benchmarks/phase-5-python/harness.md (bench script shape).
"""

from __future__ import annotations

from pathlib import Path

import jinja2
import mako.template
from jinja2 import Environment, FileSystemLoader, select_autoescape
from mako.lookup import TemplateLookup

TEMPLATES_DIR = Path(__file__).resolve().parents[1] / "templates"

#: The two encoded-suite workload ids (Phase 1 workloads.md); everything else is raw.
ENCODED_WORKLOADS = frozenset({"fortunes-encoded", "encoded-loop"})


def _jinja2_env(track: str, encoded: bool) -> Environment:
    loader = FileSystemLoader(str(TEMPLATES_DIR / "jinja2" / track))
    if track == "idiomatic":
        # D5: select_autoescape() -- escaping engages for the `.html`-named encoded
        # templates and stays off for the `.jinja`-named raw templates.
        return Environment(loader=loader, autoescape=select_autoescape())
    # D3: the library whitespace defaults pinned explicitly in code; D4: autoescape is
    # the only difference between the raw and encoded environments.
    return Environment(
        loader=loader,
        autoescape=encoded,
        trim_blocks=False,
        lstrip_blocks=False,
        keep_trailing_newline=False,
    )


def _mako_lookup(track: str, encoded: bool) -> TemplateLookup:
    directories = [str(TEMPLATES_DIR / "mako" / track)]
    if track == "controlled" and encoded:
        # D4: engine-level escaping only -- `h` is markupsafe.escape.
        return TemplateLookup(directories=directories, default_filters=["h"])
    # Raw default: default_filters=None is internally ["str"] -- no escaping. The
    # idiomatic lookup is also default-filtered; its encoded templates carry
    # <%page expression_filter="h"/> themselves (D5).
    return TemplateLookup(directories=directories)


# The four engine objects per track, built lazily and cached (module-level singletons --
# one long-lived engine object per configuration, README "Performance considerations").
_engines: dict[tuple[str, str, bool], object] = {}


def _engine(engine: str, track: str, encoded: bool):
    key = (engine, track, encoded)
    if key not in _engines:
        if engine == "jinja2":
            _engines[key] = _jinja2_env(track, encoded)
        elif engine == "mako":
            _engines[key] = _mako_lookup(track, encoded)
        else:
            raise KeyError(f"unknown engine: {engine!r}")
    return _engines[key]


def template_filename(engine: str, track: str, workload: str) -> str:
    """The per-track template file name for one cell (templates.md file layout)."""
    if engine == "jinja2":
        if track == "idiomatic" and workload in ENCODED_WORKLOADS:
            return f"{workload}.html"  # select_autoescape engages by extension (D5)
        return f"{workload}.jinja"
    if engine == "mako":
        return f"{workload}.mako"
    raise KeyError(f"unknown engine: {engine!r}")


def load(engine: str, track: str, workload: str):
    """Loads one cell's template from its track directory through the appropriate
    engine object. Raises ``FileNotFoundError`` for cells whose template file does not
    exist (gate_all reports these UNREGISTERED)."""
    if track not in ("controlled", "idiomatic"):
        raise KeyError(f"unknown track: {track!r}")
    filename = template_filename(engine, track, workload)
    path = TEMPLATES_DIR / engine / track / filename
    if not path.is_file():
        raise FileNotFoundError(path)
    encoded = workload in ENCODED_WORKLOADS
    return _engine(engine, track, encoded).get_template(filename)


def render(template, ctx: dict) -> str:
    """One render = one complete output string from the cached template, as a single
    positional-args call (``bench_func`` rejects kwargs -- harness.md): Jinja2 takes the
    context as one mapping argument, Mako expands it."""
    if isinstance(template, mako.template.Template):
        return template.render(**ctx)
    return template.render(ctx)
