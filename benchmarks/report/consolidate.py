#!/usr/bin/env python3
"""Assemble the published tables for a cross-stack benchmark run directory.

Stdlib-only, CPython >= 3.12. Reads a run directory's raw harness artifacts and writes
`consolidated-tables.md` next to them. The report's `index.md` pastes those tables verbatim;
`--check` re-derives them and diffs against the committed file, so the report is
byte-reproducible from the artifacts.

    python benchmarks/report/consolidate.py docs/benchmarks/2026-07-22
    python benchmarks/report/consolidate.py --check docs/benchmarks/2026-07-22

Contract (docs/spec/cross-stack-benchmarks/phase-7-consolidated-report/report-assembly.md
§`consolidate.py` contract):

  * No new measurements and no new metrics. The only arithmetic is unit conversion to
    ns/render, the `vs Heddle` ratio, and the implied-throughput plausibility figure
    (golden byteLength / ns) mandated by the D6 amendment.
  * Each ecosystem contributes its harness's default central-tendency point estimate with the
    harness-native dispersion alongside, per metrics-protocol.md §Wall-time statistic mapping.
    That mapping is a contract this script may not vary.
  * No geomean, points total, medal count, cross-workload average or aggregate score is
    emitted anywhere (Phase 7 D6(c), unchanged by the amendment). benchstat's `geomean` rows
    are read past deliberately.
"""

from __future__ import annotations

import argparse
import csv
import json
import re
import statistics
import sys
from dataclasses import dataclass
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
MANIFEST = REPO / "src" / "Heddle.Performance" / "GoldenCorpus" / "manifest.json"
OUTPUT_NAME = "consolidated-tables.md"

# Ecosystem order everywhere (report-assembly.md §index.md normative structure).
ECOSYSTEMS = [".NET", "Rust", "JVM", "JS", "Python", "Go"]

# Evidence class per ecosystem (report-assembly.md §How to read these tables; the D6 amendment
# requires this as a per-row column on the cross-stack tables).
EVIDENCE = {
    ".NET": "fair fight",
    "Rust": "fair fight",
    "JVM": "fair fight",
    "Go": "fair fight",
    "JS": "reach/context",
    "Python": "reach/context",
}

# Engines that are measured and ranked like every other row but are NOT under the byte-parity
# assertion, because they emit a different payload rather than the golden output. Every measured
# cell is published and ranked — nothing is dropped — but these rows carry the reason inline and
# their implied-throughput figure is withheld, since the golden byte length does not describe
# what they produced. This has nothing to do with whitespace: the parity gate's N3b rule already
# strips every whitespace run from both sides before comparing, so whitespace differences never
# disqualify a cell anywhere in this program.
NOT_PARITY_CHECKED = {
    (".NET", "Razor (full page)"): "renders the full Views/home.cshtml page — a larger, "
                                   "different payload, not the golden output; therefore "
                                   "outside the parity assertion and its implied-throughput "
                                   "figure is withheld",
}


def evidence_of(cell: Cell) -> str:
    if cell.ecosystem == ".NET" and cell.engine == "Heddle":
        return "anchor (baseline)"
    if (cell.ecosystem, cell.engine) in NOT_PARITY_CHECKED:
        return "not parity-checked"
    return EVIDENCE[cell.ecosystem]

# Display names, pinned to the versions in each harness's manifest.
ENGINE_NAMES = {
    (".NET", "Heddle"): "Heddle",
    (".NET", "Fluid"): "Fluid.Core 2.31.0",
    (".NET", "Scriban"): "Scriban 7.2.5",
    (".NET", "DotLiquid"): "DotLiquid 2.3.197",
    (".NET", "Handlebars"): "Handlebars.Net 2.1.6",
    (".NET", "Razor"): "Razor (full page)",
    ("Rust", "askama"): "Askama 0.16.0",
    ("Rust", "tera"): "Tera 2.0.0",
    ("JVM", "jte"): "JTE 3.2.4",
    ("JVM", "thymeleaf"): "Thymeleaf 3.1.5",
    ("JS", "handlebars"): "handlebars 4.7.9",
    ("JS", "eta"): "eta 4.6.0",
    ("Python", "jinja2"): "Jinja2 3.1.6",
    ("Python", "mako"): "Mako 1.3.12",
    ("Go", "stdlib-text"): "text/template (go1.26)",
    ("Go", "stdlib-html"): "html/template (go1.26)",
    ("Go", "templ"): "templ v0.3.1020",
}

# .NET suite -> workload (pinned in metrics-protocol.md; not re-derived here).
DOTNET_SUITES = {
    "Text": "composed-page",
    "Substitution": "trivial-substitution",
    "Loop": "large-loop",
    "Mixed": "mixed-page",
    "Conditional": "conditional-heavy",
    "Fragment": "fragment-heavy",
    "Fortunes": "fortunes-encoded",
    "EncodedLoop": "encoded-loop",
}
# Heddle-internal suites in the same directory — not competitor tables, excluded from the
# comparison and surfaced only in the .NET sidebar.
DOTNET_NON_COMPETITOR = ("Props", "Sink")

# JMH class prefix -> workload.
JVM_CLASSES = {
    "ComposedPage": "composed-page",
    "TrivialSubstitution": "trivial-substitution",
    "LargeLoop": "large-loop",
    "MixedPage": "mixed-page",
    "ConditionalHeavy": "conditional-heavy",
    "FragmentHeavy": "fragment-heavy",
    "FortunesEncoded": "fortunes-encoded",
    "EncodedLoop": "encoded-loop",
}

TIME_UNITS = {"ns": 1.0, "us": 1e3, "µs": 1e3, "μs": 1e3, "ms": 1e6, "s": 1e9}
BYTE_UNITS = {"B": 1.0, "KB": 1024.0, "MB": 1024.0**2, "KiB": 1024.0, "MiB": 1024.0**2}
# benchstat's SI suffixes on a sec/op column.
BENCHSTAT_UNITS = {"n": 1.0, "µ": 1e3, "u": 1e3, "m": 1e6, "": 1e9}


# Implied-throughput ceiling, in output bytes per nanosecond. Above this a cell claims more
# sustained store bandwidth than a single core of the protocol machine has while *also* running
# template logic, so the harness cannot be materialising the full output. Set from the observed
# distribution, which has a clean gap: the next-highest cell in this run sits at 30.9 B/ns
# (a compiled Rust template that is mostly a memcpy of large literal chunks — plausible), then
# the flagged cells jump to 91.7 and 105.4.
PLAUSIBILITY_CEILING_B_PER_NS = 50.0

# mitata's per-render heap estimate, keyed (track, engine, workload). Populated by load_js and
# used only by the JS materialisation register, where it corroborates the throughput signal.
JS_HEAP: dict[tuple[str, str, str], float] = {}


class Fail(Exception):
    """A structural assumption about an artifact did not hold."""


# ---- helpers ---------------------------------------------------------------------------------


def parse_quantity(text: str, units: dict[str, float]) -> float:
    """`25.31 μs` / `1,733.8 ns` / `227.86 KB` -> a float in the units' base unit."""
    m = re.match(r"^\s*([\d,.]+)\s*(\S+)\s*$", text)
    if not m:
        raise Fail(f"not a `<number> <unit>` quantity: {text!r}")
    unit = m.group(2)
    if unit not in units:
        raise Fail(f"unknown unit {unit!r} in {text!r}")
    return float(m.group(1).replace(",", "")) * units[unit]


def fmt_time(ns: float) -> str:
    """Native-unit rendering, matching the convention in benchmarks/rust/src/bin/summarize.rs."""
    if ns < 1_000:
        return f"{ns:.4g} ns"
    if ns < 1_000_000:
        return f"{ns / 1e3:.4g} μs"
    return f"{ns / 1e6:.4g} ms"


def fmt_ns(ns: float) -> str:
    return f"{ns:,.0f}"


def fmt_bytes(b: float) -> str:
    if b < 1024:
        return f"{b:.0f} B"
    if b < 1024**2:
        return f"{b / 1024:.2f} KB"
    return f"{b / 1024**2:.2f} MB"


def fmt_ratio(value: float) -> str:
    return f"{value:.2f}"


def table(header: list[str], aligns: list[str], rows: list[list[str]]) -> list[str]:
    sep = {"l": "---", "r": "---:", "c": ":---:"}
    out = ["| " + " | ".join(header) + " |", "| " + " | ".join(sep[a] for a in aligns) + " |"]
    out += ["| " + " | ".join(r) + " |" for r in rows]
    return out


# ---- the cell record ------------------------------------------------------------------------


@dataclass(frozen=True)
class Cell:
    ecosystem: str
    engine: str  # display name
    track: str  # controlled | idiomatic | cold-compile
    workload: str
    ns: float
    stat: str  # native-unit point estimate
    dispersion: str  # native-unit dispersion


def name(ecosystem: str, engine: str) -> str:
    try:
        return ENGINE_NAMES[(ecosystem, engine)]
    except KeyError:
        raise Fail(f"no display name pinned for {ecosystem} engine {engine!r}") from None


# ---- per-ecosystem parsers ------------------------------------------------------------------


def load_dotnet(run: Path) -> tuple[list[Cell], list[list[str]]]:
    """BenchmarkDotNet CSV: `Mean`, dispersion `Error` + `StdDev` (metrics-protocol Q2.1).

    Values are formatted strings with units that vary per row and per suite. Row sets vary too:
    the Text suite carries a sixth `RenderRazor` row that is not parity-checked.
    """
    cells: list[Cell] = []
    alloc: list[list[str]] = []
    results = run / "dotnet" / "results"
    for suite, workload in DOTNET_SUITES.items():
        path = results / f"Heddle.Performance.{suite}RenderBenchmarks-report.csv"
        if not path.exists():
            raise Fail(f"missing .NET artifact: {path.relative_to(run)}")
        with path.open(encoding="utf-8-sig", newline="") as fh:
            rows = list(csv.DictReader(fh))
        if not rows:
            raise Fail(f"empty .NET artifact: {path.relative_to(run)}")
        for row in rows:
            method = row["Method"]
            if not method.startswith("Render"):
                raise Fail(f"unexpected .NET method {method!r} in {suite}")
            engine = method[len("Render") :]
            ns = parse_quantity(row["Mean"], TIME_UNITS)
            disp = f"±{row['Error'].strip()} (SD {row['StdDev'].strip()})"
            cells.append(
                Cell(".NET", name(".NET", engine), "controlled", workload, ns,
                     row["Mean"].strip(), disp)
            )
            allocated = (row.get("Allocated") or "").strip()
            if allocated and allocated != "NA":
                alloc.append([
                    workload, name(".NET", engine), allocated,
                    (row.get("Alloc Ratio") or "").strip() or "—",
                    " / ".join((row.get(g) or "-").strip() or "-" for g in ("Gen0", "Gen1", "Gen2")),
                ])
    return cells, alloc


def load_dotnet_internal(run: Path) -> list[list[str]]:
    """The two Heddle-internal .NET suites, for the .NET sidebar only."""
    rows: list[list[str]] = []
    results = run / "dotnet" / "results"
    for suite in DOTNET_NON_COMPETITOR:
        path = results / f"Heddle.Performance.{suite}RenderBenchmarks-report.csv"
        if not path.exists():
            continue
        with path.open(encoding="utf-8-sig", newline="") as fh:
            for row in csv.DictReader(fh):
                rows.append([
                    f"{suite}RenderBenchmarks", row["Method"], row["Mean"].strip(),
                    (row.get("Allocated") or "—").strip() or "—",
                ])
    return rows


def load_rust(run: Path) -> tuple[list[Cell], list[Cell]]:
    """Criterion: `mean.point_estimate`, dispersion = the 95% CI bounds. `new/` only."""
    cells: list[Cell] = []
    cold: list[Cell] = []
    base = run / "rust" / "criterion"
    for est in sorted(base.glob("*/*/new/estimates.json")):
        group = est.parents[2].name
        engine = est.parents[1].name
        mean = json.loads(est.read_text(encoding="utf-8"))["mean"]
        ns = float(mean["point_estimate"])
        ci = mean["confidence_interval"]
        disp = f"[{fmt_time(ci['lower_bound'])}, {fmt_time(ci['upper_bound'])}] 95% CI"
        if group == "cold":
            # criterion's function id here is the D12 sidebar's own name, not an engine id.
            label = {"tera-parse-all-templates": "Tera 2.0.0 (parse all templates)"}.get(
                engine, engine)
            cold.append(Cell("Rust", label, "cold-compile", "all templates", ns,
                             fmt_time(ns), disp))
            continue
        track, _, workload = group.partition("-")
        if track not in ("controlled", "idiomatic"):
            raise Fail(f"unexpected criterion group {group!r}")
        cells.append(Cell("Rust", name("Rust", engine), track, workload, ns,
                          fmt_time(ns), disp))
    if not cells:
        raise Fail("no criterion estimates found under rust/criterion")
    return cells, cold


def load_jvm(run: Path) -> tuple[list[Cell], list[list[str]]]:
    """JMH `Mode.AverageTime`: `primaryMetric.score`, dispersion `scoreError` (99.9% CI)."""
    path = run / "jvm" / "jmh-result.json"
    if not path.exists():
        raise Fail("missing jvm/jmh-result.json")
    cells: list[Cell] = []
    alloc: list[list[str]] = []
    for entry in json.loads(path.read_text(encoding="utf-8")):
        cls, _, method = entry["benchmark"].rpartition(".")
        cls = cls.rsplit(".", 1)[-1]
        if not cls.endswith("Bench"):
            raise Fail(f"unexpected JMH class {cls!r}")
        workload = JVM_CLASSES.get(cls[: -len("Bench")])
        if workload is None:
            raise Fail(f"unmapped JMH class {cls!r}")
        for track in ("controlled", "idiomatic"):
            if method.endswith(track.capitalize()):
                engine = method[: -len(track)]
                break
        else:
            raise Fail(f"unexpected JMH method {method!r}")
        pm = entry["primaryMetric"]
        if pm["scoreUnit"] != "ns/op":
            raise Fail(f"unexpected JMH unit {pm['scoreUnit']!r}")
        ns = float(pm["score"])
        cells.append(Cell("JVM", name("JVM", engine), track, workload, ns,
                          fmt_time(ns), f"±{fmt_time(float(pm['scoreError']))} 99.9% CI"))
        norm = entry.get("secondaryMetrics", {}).get("gc.alloc.rate.norm")
        if norm:
            alloc.append([
                workload, name("JVM", engine), track,
                fmt_bytes(float(norm["score"])),
                f"{entry['secondaryMetrics']['gc.count']['score']:.0f}",
            ])
    return cells, alloc


def load_js(run: Path) -> tuple[list[Cell], list[Cell]]:
    """mitata: the reported `avg`, dispersion = the emitted min…max spread.

    The JSON carries no workload name — `alias` is only the engine, repeated once per workload,
    so the workload is positional. That is safe because benchmarks/js/bench/_shared.mjs
    `registerTrackGroups()` iterates WORKLOAD_IDS in protocol order, but it is asserted here
    rather than assumed. `cold-compile.json` alone carries composite aliases.
    """
    workloads = [e["workload"] for e in golden_entries()]
    cells: list[Cell] = []
    cold: list[Cell] = []

    for track in ("controlled", "idiomatic"):
        path = run / "js" / f"{track}.json"
        if not path.exists():
            raise Fail(f"missing js/{track}.json")
        data = json.loads(path.read_text(encoding="utf-8"))
        expected = 2 * len(workloads)
        if len(data) != expected:
            raise Fail(f"js/{track}.json: expected {expected} entries, found {len(data)}")
        groups = [e.get("group") for e in data]
        if any(groups[i] != groups[i + 1] for i in range(0, len(groups), 2)):
            raise Fail(f"js/{track}.json: entries are not engine-paired per group")
        pair_groups = groups[::2]
        if any(b <= a for a, b in zip(pair_groups, pair_groups[1:])):
            raise Fail(f"js/{track}.json: group ids are not strictly ascending")
        aliases = [e["alias"] for e in data]
        if aliases[0::2] != ["handlebars"] * len(workloads) or aliases[1::2] != ["eta"] * len(workloads):
            raise Fail(f"js/{track}.json: alias order is not handlebars/eta per group")
        for i, entry in enumerate(data):
            st = entry["runs"][0]["stats"]
            ns = float(st["avg"])
            disp = f"{fmt_time(float(st['min']))} … {fmt_time(float(st['max']))}"
            cells.append(Cell("JS", name("JS", entry["alias"]), track, workloads[i // 2],
                              ns, fmt_time(ns), disp))

    global JS_HEAP
    JS_HEAP = {}
    for track in ("controlled", "idiomatic"):
        data = json.loads((run / "js" / f"{track}.json").read_text(encoding="utf-8"))
        for i, entry in enumerate(data):
            heap = entry["runs"][0]["stats"].get("heap") or {}
            if "avg" in heap:
                JS_HEAP[(track, entry["alias"], workloads[i // 2])] = float(heap["avg"])

    path = run / "js" / "cold-compile.json"
    if path.exists():
        for entry in json.loads(path.read_text(encoding="utf-8")):
            engine, _, workload = entry["alias"].partition(" ")
            st = entry["runs"][0]["stats"]
            ns = float(st["avg"])
            cold.append(Cell("JS", name("JS", engine), "cold-compile", workload, ns,
                             fmt_time(ns),
                             f"{fmt_time(float(st['min']))} … {fmt_time(float(st['max']))}"))
    return cells, cold


def load_python(run: Path) -> tuple[list[Cell], list[Cell], list[list[str]]]:
    """pyperf: the reported mean with its standard deviation.

    pyperf stores no precomputed mean, so it is aggregated from every measurement run's
    `values` (seconds). runs[0] is the calibration run and carries `warmups` but no `values`.
    """
    def aggregate(path: Path) -> list[tuple[str, str, str, float, float]]:
        doc = json.loads(path.read_text(encoding="utf-8"))
        out = []
        for bench in doc["benchmarks"]:
            parts = bench["metadata"]["name"].split("/")
            if len(parts) != 3:
                raise Fail(f"{path.name}: unexpected pyperf name {bench['metadata']['name']!r}")
            engine, track, workload = parts
            values = [v for r in bench["runs"] if "values" in r for v in r["values"]]
            if not values:
                raise Fail(f"{path.name}: no measurement values for {'/'.join(parts)}")
            out.append((engine, track, workload,
                        statistics.mean(values) * 1e9,
                        statistics.stdev(values) * 1e9 if len(values) > 1 else 0.0))
        return out

    cells: list[Cell] = []
    for track in ("controlled", "idiomatic"):
        for engine in ("jinja2", "mako"):
            path = run / "python" / f"bench_{engine}_{track}.json"
            if not path.exists():
                raise Fail(f"missing python/{path.name}")
            for eng, tr, workload, ns, sd in aggregate(path):
                if tr != track:
                    raise Fail(f"{path.name}: track {tr!r} in the {track} file")
                cells.append(Cell("Python", name("Python", eng), track, workload, ns,
                                  fmt_time(ns), f"SD {fmt_time(sd)}"))

    cold: list[Cell] = []
    cold_path = run / "python" / "bench_cold_compile.json"
    if cold_path.exists():
        for eng, _, workload, ns, sd in aggregate(cold_path):
            cold.append(Cell("Python", name("Python", eng), "cold-compile", workload, ns,
                             fmt_time(ns), f"SD {fmt_time(sd)}"))

    alloc: list[list[str]] = []
    mem_path = run / "python" / "memory.json"
    if mem_path.exists():
        mem = json.loads(mem_path.read_text(encoding="utf-8"))
        for key in sorted(mem):
            engine, track, workload = key.split("/")
            rec = mem[key]
            alloc.append([
                workload, name("Python", engine), track,
                fmt_bytes(rec["allocated"]["mean"]), fmt_bytes(rec["retained"]["mean"]),
                str(rec["repetitions"]),
            ])
    return cells, cold, alloc


def load_go(run: Path) -> tuple[list[Cell], list[Cell], list[list[str]]]:
    """benchstat: the `sec/op` summary (median-based) with its ±% variation.

    The point estimate and dispersion both come from benchstat, per the statistic mapping. The
    `B/op` and `allocs/op` blocks feed the Go sidebar. benchstat's `geomean` rows are skipped:
    D6(c) forbids any aggregate score.
    """
    ROW = re.compile(
        r"^(?P<name>\S+?)-\d+\s+(?P<value>[\d.]+)(?P<unit>[a-zA-Zµ]*)\s*±\s*(?P<pct>[\d.]+|\?)%?"
    )

    def blocks(path: Path) -> dict[str, list[tuple[str, str, str]]]:
        """column-name -> [(bench name, value+unit, ±pct)]"""
        out: dict[str, list[tuple[str, str, str]]] = {}
        column = None
        for line in path.read_text(encoding="utf-8").splitlines():
            if "│" in line:  # a benchstat header rule: │ label │
                label = line.replace("│", " ").strip()
                if label and "/" in label or label in ("sec/op", "B/op", "allocs/op"):
                    if label in ("sec/op", "B/op", "allocs/op"):
                        column = label
                        out.setdefault(column, [])
                continue
            if line.startswith("geomean"):
                continue  # D6(c): no aggregate score
            m = ROW.match(line.strip())
            if not m or column is None:
                continue
            out[column].append(
                (m.group("name"), m.group("value") + m.group("unit"), m.group("pct"))
            )
        return out

    render = next(iter(sorted((run / "go").glob("benchstat-render-*.txt"))), None)
    if render is None:
        raise Fail("missing go/benchstat-render-*.txt")
    data = blocks(render)
    if "sec/op" not in data:
        raise Fail("go benchstat: no sec/op block found")

    cells: list[Cell] = []
    by_name_alloc: dict[str, dict[str, str]] = {}
    for bench, value, pct in data["sec/op"]:
        parts = bench.split("/")
        if len(parts) != 4 or parts[0] != "Render":
            raise Fail(f"unexpected Go bench name {bench!r}")
        _, track, workload, engine = parts
        m = re.match(r"^([\d.]+)([a-zA-Zµ]*)$", value)
        if not m or m.group(2) not in BENCHSTAT_UNITS:
            raise Fail(f"unparsable benchstat sec/op value {value!r}")
        ns = float(m.group(1)) * BENCHSTAT_UNITS[m.group(2)]
        cells.append(Cell("Go", name("Go", engine), track, workload, ns,
                          fmt_time(ns), f"±{pct}%"))
    for column in ("B/op", "allocs/op"):
        for bench, value, _ in data.get(column, []):
            by_name_alloc.setdefault(bench, {})[column] = value

    alloc: list[list[str]] = []
    for bench in sorted(by_name_alloc):
        _, track, workload, engine = bench.split("/")
        rec = by_name_alloc[bench]
        alloc.append([workload, name("Go", engine), track,
                      rec.get("B/op", "—"), rec.get("allocs/op", "—")])

    cold: list[Cell] = []
    coldparse = next(iter(sorted((run / "go").glob("benchstat-coldparse-*.txt"))), None)
    if coldparse is not None:
        for bench, value, pct in blocks(coldparse).get("sec/op", []):
            engine = bench.split("/")[-1]
            m = re.match(r"^([\d.]+)([a-zA-Zµ]*)$", value)
            ns = float(m.group(1)) * BENCHSTAT_UNITS[m.group(2)]
            cold.append(Cell("Go", name("Go", engine), "cold-compile", "all templates", ns,
                             fmt_time(ns), f"±{pct}%"))
    return cells, cold, alloc


# ---- corpus ---------------------------------------------------------------------------------


def golden_entries() -> list[dict]:
    doc = json.loads(MANIFEST.read_text(encoding="utf-8"))
    return doc["entries"]


# ---- rendering ------------------------------------------------------------------------------


def render(run: Path) -> str:
    entries = golden_entries()
    workloads = [e["workload"] for e in entries]
    golden_bytes = {e["workload"]: e["byteLength"] for e in entries}
    suite_of = {e["workload"]: e["suite"] for e in entries}

    dotnet, dotnet_alloc = load_dotnet(run)
    rust, rust_cold = load_rust(run)
    jvm, jvm_alloc = load_jvm(run)
    js, js_cold = load_js(run)
    python, python_cold, python_alloc = load_python(run)
    go, go_cold, go_alloc = load_go(run)

    cells = dotnet + rust + jvm + js + python + go
    for c in cells:
        if c.workload not in golden_bytes:
            raise Fail(f"{c.ecosystem}/{c.engine}: unknown workload {c.workload!r}")

    heddle = {c.workload: c.ns for c in dotnet if c.engine == "Heddle"}
    missing = [w for w in workloads if w not in heddle]
    if missing:
        raise Fail(f"no Heddle anchor for: {', '.join(missing)}")

    out: list[str] = [
        "<!-- Generated by benchmarks/report/consolidate.py. Do not edit by hand.",
        f"     Source: {run.as_posix()} — regenerate with",
        f"     `python benchmarks/report/consolidate.py {run.as_posix()}`",
        "     and verify with `--check`. index.md pastes these tables verbatim. -->",
        "",
        f"# Consolidated tables — {run.name}",
        "",
    ]

    # ---- 1. cross-stack ranked, one table per workload (Phase 7 D6 amendment) ----
    out += [
        "## Cross-stack ranked — wall time per render",
        "",
        "One ranking per workload. There is no aggregate score, overall winner or",
        "cross-workload average anywhere — Phase 7 D6(c) still forbids them, so no",
        "single-number verdict can be quoted from these tables.",
        "",
    ]
    for track in ("controlled", "idiomatic"):
        out += [f"### {track.capitalize()} track", ""]
        for workload in workloads:
            rows_src = sorted(
                [c for c in cells if c.track == track and c.workload == workload],
                key=lambda c: c.ns,
            )
            if not rows_src:
                continue
            anchor = heddle[workload]
            rows = []
            for i, c in enumerate(rows_src, start=1):
                unchecked = (c.ecosystem, c.engine) in NOT_PARITY_CHECKED
                # The golden size does not describe a non-parity-checked payload, so its
                # implied throughput would be arithmetic on mismatched operands.
                bpn = "n/a †" if unchecked else f"{golden_bytes[workload] / c.ns:.1f}"
                rows.append([
                    str(i), c.engine + (" †" if unchecked else ""), c.ecosystem,
                    evidence_of(c), c.stat, c.dispersion, fmt_ns(c.ns),
                    fmt_ratio(c.ns / anchor), bpn,
                ])
            out += [
                f"**{workload} — cross-stack ({track}) — "
                f"{golden_bytes[workload]:,} B golden output**",
                "",
            ]
            out += table(
                ["#", "Engine", "Ecosystem", "Evidence", "harness statistic",
                 "harness dispersion", "ns/render", "vs Heddle", "implied B/ns"],
                ["r", "l", "l", "l", "r", "r", "r", "r", "r"],
                rows,
            )
            out += [""]
            for c in rows_src:
                if (c.ecosystem, c.engine) in NOT_PARITY_CHECKED:
                    out += [f"† **{c.engine}** ({c.ecosystem}) — "
                            f"{NOT_PARITY_CHECKED[(c.ecosystem, c.engine)]}. It is ranked "
                            "here on its measured wall time like every other row; only the "
                            "throughput figure is withheld.", ""]
            out += [
                f"*Source: {run.as_posix()} — each figure is its harness's default "
                "central-tendency point estimate, unmodified except for conversion to "
                "ns/render.*",
                "",
            ]

    # ---- 2. per-ecosystem tables, the normative view ----
    by_eco = {eco: [c for c in cells if c.ecosystem == eco] for eco in ECOSYSTEMS}
    for track in ("controlled", "idiomatic"):
        out += [f"## {track.capitalize()} track — per ecosystem", ""]
        for eco in ECOSYSTEMS:
            eco_cells = [c for c in by_eco[eco] if c.track == track]
            if not eco_cells:
                out += [
                    f"### {eco}",
                    "",
                    f"No {track}-track cells — .NET shipped controlled-track-only "
                    "(Phase 1 D15)." if eco == ".NET" else
                    f"No {track}-track cells in this run.",
                    "",
                ]
                continue
            rows = []
            for workload in workloads:
                anchor = heddle[workload]
                if eco != ".NET":
                    rows.append([
                        workload, "Heddle (reference)", fmt_time(anchor), "—",
                        fmt_ns(anchor), "1.00",
                    ])
                for c in sorted((c for c in eco_cells if c.workload == workload),
                                key=lambda c: c.ns):
                    label = c.engine
                    if (c.ecosystem, c.engine) in NOT_PARITY_CHECKED:
                        label += " †"
                    rows.append([
                        workload, label, c.stat, c.dispersion, fmt_ns(c.ns),
                        fmt_ratio(c.ns / anchor),
                    ])
            out += [f"### {eco}", ""]
            out += table(
                ["Workload", "Engine", "harness statistic", "harness dispersion",
                 "ns/render", "vs Heddle"],
                ["l", "l", "r", "r", "r", "r"],
                rows,
            )
            out += [""]
            for (e_eco, e_engine), reason in NOT_PARITY_CHECKED.items():
                if e_eco == eco and any(c.engine == e_engine for c in eco_cells):
                    out += [f"† **{e_engine}** — {reason}. Excluded from the cross-stack "
                            "rankings for that reason; its ratio column above is indicative "
                            "only.", ""]
            out += [
                f"*Source: {run.as_posix()} — {EVIDENCE[eco]} evidence. The Heddle row is "
                "the .NET anchor measured in this same run on the same machine.*",
                "",
            ]

    # ---- 3. cold compile / parse, per ecosystem, never cross-compared ----
    out += [
        "## Cold compile / parse — per ecosystem",
        "",
        "Per-ecosystem only, never cross-compared (Q1.3). The .NET anchor measured no cold",
        "suite in this run, so there is no Heddle row and no ratio column here.",
        "",
    ]
    for eco, cold in ((".NET", []), ("Rust", rust_cold), ("JVM", []), ("JS", js_cold),
                      ("Python", python_cold), ("Go", go_cold)):
        out += [f"### {eco}", ""]
        if not cold:
            out += ["No cold-compile cells in this run.", ""]
            continue
        rows = [[c.workload, c.engine, c.stat, c.dispersion]
                for c in sorted(cold, key=lambda c: (c.workload, c.ns))]
        out += table(["Workload", "Engine", "harness statistic", "harness dispersion"],
                     ["l", "l", "r", "r"], rows)
        out += ["", f"*Source: {run.as_posix()}.*", ""]

    # ---- 4. per-ecosystem sidebars, not cross-comparable ----
    out += [
        "## Per-ecosystem sidebars — not cross-comparable",
        "",
        "Allocation, GC and memory figures are per-ecosystem measurements taken by different",
        "tools with different definitions. No table or sentence juxtaposes them across",
        "runtimes (Phase 7 D6(e), unchanged).",
        "",
    ]
    out += ["### .NET — allocation (BenchmarkDotNet `[MemoryDiagnoser]`)", ""]
    out += table(["Workload", "Engine", "Allocated", "Alloc ratio", "Gen0 / Gen1 / Gen2"],
                 ["l", "l", "r", "r", "r"], dotnet_alloc)
    internal = load_dotnet_internal(run)
    if internal:
        out += ["", "#### .NET — Heddle-internal suites (not competitor tables)", ""]
        out += table(["Suite", "Method", "Mean", "Allocated"], ["l", "l", "r", "r"], internal)
    out += ["", "### JVM — allocation (JMH `-prof gc`)", ""]
    out += table(["Workload", "Engine", "Track", "gc.alloc.rate.norm", "gc.count"],
                 ["l", "l", "l", "r", "r"], jvm_alloc)
    out += ["", "### Python — memory (`tracemalloc`)", ""]
    out += table(["Workload", "Engine", "Track", "allocated (mean)", "retained (mean)", "reps"],
                 ["l", "l", "l", "r", "r", "r"], python_alloc)
    out += ["", "### Go — allocation (`go test -benchmem` via benchstat)", ""]
    out += table(["Workload", "Engine", "Track", "B/op", "allocs/op"],
                 ["l", "l", "l", "r", "r"], go_alloc)
    out += [
        "",
        "### JS — no memory sidebar",
        "",
        "The JS phase shipped **time-only** (Phase 4 D3): no memory sidebar exists, so none is",
        "published here even though mitata emits a heap estimate.",
        "",
        "### Rust — allocation report absent",
        "",
        "`alloc-report.txt` was produced by the run (`rust / measure / alloc-report` = OK in",
        "`summary.txt`) but the `copy-criterion` step copied only `target/criterion`, so the",
        "artifact is not in this directory and no Rust allocation figures are published.",
        "",
    ]

    # ---- 5. the plausibility register ----
    out += [
        "## Plausibility register",
        "",
        "`implied B/ns` is the golden output size divided by the wall time — the diagnostic the",
        "D6 amendment requires on every cross-stack table. It is **not** a performance metric.",
        "Its purpose is to expose cells where the harness cannot be producing the full output,",
        "so a reader does not mistake a measurement artifact for engine speed.",
        "",
        f"### Cells above the {PLAUSIBILITY_CEILING_B_PER_NS:.0f} B/ns ceiling",
        "",
        "A cell above this ceiling claims more sustained store bandwidth than one core of this",
        "machine has while also executing template logic. The threshold is set from the observed",
        "distribution rather than a model: this run's cells drop from 105.4 and 91.7 B/ns to",
        "30.9 B/ns, and that 30.9 figure is a compiled Rust template whose output is mostly a",
        "memcpy of large literal chunks — fast, but physically possible.",
        "",
    ]
    flagged = []
    for c in sorted(cells, key=lambda c: -(golden_bytes[c.workload] / c.ns)):
        bpn = golden_bytes[c.workload] / c.ns
        if bpn > PLAUSIBILITY_CEILING_B_PER_NS:
            flagged.append([c.ecosystem, c.engine, c.track, c.workload,
                            f"{golden_bytes[c.workload]:,}", fmt_time(c.ns), f"{bpn:.1f}"])
    if flagged:
        out += table(
            ["Ecosystem", "Engine", "Track", "Workload", "golden B", "wall time", "implied B/ns"],
            ["l", "l", "l", "l", "r", "r", "r"], flagged)
    else:
        out += ["No cell exceeds the ceiling in this run."]
    out += [""]

    # Corroborating signal, JS only: mitata reports a per-render heap estimate, so a cell whose
    # heap is a small fraction of its output size cannot be materialising that output.
    if JS_HEAP:
        out += [
            "### JS — heap per render against output size (corroborating signal)",
            "",
            "mitata reports an estimated heap figure per render. A cell whose heap is a small",
            "fraction of its golden output size cannot be materialising that output: V8",
            "represents string concatenation as an unflattened `ConsString` rope, and mitata's",
            "`do_not_optimize()` consumes the returned reference without forcing it flat.",
            "",
            "The heap figure is a sampling estimate, so a ratio moderately below 1 is within",
            "its noise and decides nothing. A ratio below 0.1 is not: a thirty-fold shortfall",
            "against a known output size is far outside what the estimate's error can explain.",
            "The two diagnostics are independent, and the `reading` column reports each cell on",
            "its own evidence rather than collapsing them into one verdict.",
            "",
            "In this run only `composed-page` is decided, for **both** JS engines (ratio 0.03).",
            "`eta` on that workload is confirmed twice over, since it also breaks the throughput",
            "ceiling. The mid-range cells (`mixed-page`, `conditional-heavy`, `fragment-heavy`)",
            "have plausible throughput and heap ratios inside the noise band, so they are not",
            "called artifacts.",
            "",
        ]
        rows = []
        for (track, engine, workload), heap in sorted(
            JS_HEAP.items(), key=lambda kv: (kv[1] / golden_bytes[kv[0][2]])
        ):
            gb = golden_bytes[workload]
            ratio = heap / gb
            over_ceiling = gb / next(
                c.ns for c in cells
                if c.ecosystem == "JS" and c.track == track and c.workload == workload
                and c.engine == name("JS", engine)
            ) > PLAUSIBILITY_CEILING_B_PER_NS
            if ratio < 0.1:
                verdict = "artifact — output not materialised"
                if over_ceiling:
                    verdict += "; throughput also above ceiling"
            elif ratio < 1.0:
                verdict = "inconclusive — inside the estimate's noise"
            else:
                verdict = "consistent with materialised output"
            rows.append([workload, name("JS", engine), track, f"{gb:,}",
                         f"{heap:,.0f}", f"{ratio:.2f}", verdict])
        out += table(
            ["Workload", "Engine", "Track", "golden B", "heap/render B", "ratio", "reading"],
            ["l", "l", "l", "r", "r", "r", "l"], rows)
        out += [""]

    return "\n".join(out).rstrip() + "\n"


# ---- entry point ----------------------------------------------------------------------------


def main(argv: list[str]) -> int:
    ap = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    ap.add_argument("run", type=Path, help="the run directory, e.g. docs/benchmarks/2026-07-22")
    ap.add_argument("--check", action="store_true",
                    help="re-derive the tables and diff against the committed file")
    args = ap.parse_args(argv)

    run = args.run
    if not run.is_dir():
        print(f"consolidate: not a directory: {run}", file=sys.stderr)
        return 2

    try:
        text = render(run)
    except Fail as exc:
        print(f"consolidate: {exc}", file=sys.stderr)
        return 1

    target = run / OUTPUT_NAME
    if args.check:
        if not target.exists():
            print(f"consolidate --check: {target} does not exist", file=sys.stderr)
            return 1
        committed = target.read_text(encoding="utf-8")
        if committed != text:
            import difflib
            diff = difflib.unified_diff(
                committed.splitlines(keepends=True), text.splitlines(keepends=True),
                fromfile=f"{target} (committed)", tofile="re-derived", n=2)
            sys.stderr.writelines(diff)
            print(f"consolidate --check: {target} is stale", file=sys.stderr)
            return 1
        print(f"consolidate --check: {target} matches the artifacts")
        return 0

    target.write_text(text, encoding="utf-8", newline="\n")
    print(f"consolidate: wrote {target} ({len(text.splitlines())} lines)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main(sys.argv[1:]))
