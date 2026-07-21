#!/usr/bin/env python3
"""validate.py — Phase 8 findings-validation tooling (spec D15/D16/D17.4; stdlib-only).

Computes every part-2 validation table and verdict from exactly two inputs:

  (a) ``windows-source.json``  — hand-transcribed Windows ranks, ratios-to-Heddle
      (``r``, quoted from each report's own ratio column) and dimensionless
      dispersions (``d``) with per-value source citations. NEVER absolute times
      (D17.4 — structurally enforced below).
  (b) ``linux-results.json``   — the same shape for this Linux run (part 1 data).

Verdicts (D16):
  1. material rank flip     — order differs across OSes AND the pair is resolved on
                              BOTH OSes: |r_A − r_B| > (d_A + d_B) · min(r_A, r_B),
                              evaluated on each OS's own ratios (ratio form — no
                              absolute time needed).
  2. material gap movement  — |r_L / r_W − 1| > max(D, 0.05) with
                              D = (d_eng,W + d_Heddle,W) + (d_eng,L + d_Heddle,L)
                              (LINEAR sums — interval arithmetic, not quadrature).
  3. dispersion character   — max(d_W, d_L) / min(d_W, d_L) ≥ 2 AND max ≥ 0.01;
                              zero-dispersion guard: when min = 0 the ratio is
                              undefined and the cell is material iff max ≥ 0.01
                              (never divides by zero).

Everything is published; "material" only controls prominence. Exit 1 on malformed
input (missing citation, absolute-time field, schema violation); verdicts are data,
not errors (exit 0).

Usage:
  python3 validate.py --windows-source windows-source.json \
                      --linux-results linux-results.json [--out tables.md]
"""

from __future__ import annotations

import argparse
import json
import sys

SCHEMA_ID = "heddle-linux-crosscheck/windows-source@1"

GAP_FLOOR = 0.05          # D16.2: the program's own established 5% stability line
DISPERSION_RATIO = 2.0    # D16.3
DISPERSION_FLOOR = 0.01   # D16.3: 1% relative-dispersion floor

# D17.4 — no absolute-time / point-estimate field may appear ANYWHERE in either
# input. Closed list of forbidden key tokens (case-insensitive, matched on whole
# underscore/dash/camel-separated words so e.g. "meanTimeNs" and "wall_time" are
# both caught, while "rank"/"track" are not).
FORBIDDEN_KEY_TOKENS = frozenset({
    "time", "times", "ns", "us", "usec", "ms", "msec", "sec", "secs", "seconds",
    "nanoseconds", "microseconds", "milliseconds",
    "mean", "median", "min", "max", "avg", "average", "stddev", "stdev", "error",
    "score", "point", "estimate", "wall", "elapsed", "duration", "p50", "p75",
    "p99", "p999", "ci", "lower", "upper", "absolute", "bytes", "allocated",
})

# The only keys allowed on a value-carrying (engine / heddle) object.
ALLOWED_VALUE_KEYS = frozenset({"rank", "r", "d", "source", "note"})


class ValidationError(Exception):
    """Malformed input (exit 1)."""


def _split_key_tokens(key: str) -> list[str]:
    out, word = [], []
    for ch in key:
        if ch in "_- .":
            if word:
                out.append("".join(word).lower())
                word = []
        elif ch.isupper() and word and not word[-1].isupper():
            out.append("".join(word).lower())
            word = [ch]
        else:
            word.append(ch)
    if word:
        out.append("".join(word).lower())
    return out


def reject_absolute_time_fields(obj, path="$"):
    """D17.4 structural check: reject ANY absolute-time-shaped field, recursively."""
    if isinstance(obj, dict):
        for key, value in obj.items():
            if isinstance(key, str):
                bad = [t for t in _split_key_tokens(key) if t in FORBIDDEN_KEY_TOKENS]
                if bad:
                    raise ValidationError(
                        f"D17.4 violation: absolute-time/point-estimate field "
                        f"'{key}' at {path} (forbidden token(s): {', '.join(bad)}). "
                        f"windows-source.json stores only ranks, ratios (r) and "
                        f"dimensionless dispersions (d) — never absolute times."
                    )
            reject_absolute_time_fields(value, f"{path}.{key}")
    elif isinstance(obj, list):
        for i, value in enumerate(obj):
            reject_absolute_time_fields(value, f"{path}[{i}]")


def _check_value_obj(obj, path, *, require_source, require_rank):
    if not isinstance(obj, dict):
        raise ValidationError(f"{path}: expected an object")
    extra = set(obj) - ALLOWED_VALUE_KEYS
    if extra:
        raise ValidationError(
            f"{path}: unexpected key(s) {sorted(extra)} — only "
            f"{sorted(ALLOWED_VALUE_KEYS)} are allowed (D17.4 structural rule)"
        )
    for field in ("r", "d"):
        if field not in obj or not isinstance(obj[field], (int, float)) or isinstance(obj[field], bool):
            raise ValidationError(f"{path}: missing or non-numeric '{field}'")
        if obj[field] < 0:
            raise ValidationError(f"{path}: '{field}' must be >= 0")
    if obj["r"] <= 0:
        raise ValidationError(f"{path}: 'r' must be > 0 (a wall-time ratio)")
    if require_rank and ("rank" not in obj or not isinstance(obj["rank"], int) or isinstance(obj["rank"], bool)):
        raise ValidationError(f"{path}: missing integer 'rank'")
    if require_source and not (isinstance(obj.get("source"), str) and obj["source"].strip()):
        raise ValidationError(
            f"{path}: missing 'source' citation — every transcribed Windows value "
            f"must cite its source file within the published Windows directory"
        )


def load_side(path_or_obj, *, windows: bool):
    """Load + structurally validate one side (dict or file path)."""
    if isinstance(path_or_obj, (str, bytes)):
        with open(path_or_obj, "r", encoding="utf-8") as fh:
            doc = json.load(fh)
    else:
        doc = path_or_obj
    if not isinstance(doc, dict):
        raise ValidationError("input root must be a JSON object")
    reject_absolute_time_fields(doc)
    if windows and doc.get("schema") != SCHEMA_ID:
        raise ValidationError(f"windows-source: 'schema' must be '{SCHEMA_ID}'")
    ecosystems = doc.get("ecosystems")
    if not isinstance(ecosystems, dict) or not ecosystems:
        raise ValidationError("input must carry a non-empty 'ecosystems' object")
    for eco, eco_obj in ecosystems.items():
        tracks = eco_obj.get("tracks")
        if not isinstance(tracks, dict) or not tracks:
            raise ValidationError(f"ecosystems.{eco}: missing 'tracks'")
        for track, track_obj in tracks.items():
            cells = track_obj.get("cells")
            if not isinstance(cells, dict) or not cells:
                raise ValidationError(f"ecosystems.{eco}.tracks.{track}: missing 'cells'")
            for workload, cell in cells.items():
                base = f"ecosystems.{eco}.tracks.{track}.cells.{workload}"
                engines = cell.get("engines")
                if not isinstance(engines, dict) or not engines:
                    raise ValidationError(f"{base}: missing 'engines'")
                for engine, vobj in engines.items():
                    _check_value_obj(vobj, f"{base}.engines.{engine}",
                                     require_source=windows, require_rank=windows)
                heddle = cell.get("heddle")
                _check_value_obj(heddle, f"{base}.heddle",
                                 require_source=windows, require_rank=False)
    return doc


# --- D16 predicates (pure; unit-tested by test_validate.py) --------------------

def pair_resolved(r_a: float, d_a: float, r_b: float, d_b: float) -> bool:
    """D16.1 resolution test in ratio form (strict >)."""
    return abs(r_a - r_b) > (d_a + d_b) * min(r_a, r_b)


def rank_flip_verdict(r_a_w, d_a_w, r_b_w, d_b_w, r_a_l, d_a_l, r_b_l, d_b_l) -> str:
    """Pairwise D16.1 verdict: 'held' | 'not resolved' | 'material flip'."""
    order_w = (r_a_w > r_b_w) - (r_a_w < r_b_w)
    order_l = (r_a_l > r_b_l) - (r_a_l < r_b_l)
    if order_w == order_l:
        return "held"
    if pair_resolved(r_a_w, d_a_w, r_b_w, d_b_w) and pair_resolved(r_a_l, d_a_l, r_b_l, d_b_l):
        return "material flip"
    return "not resolved"


def gap_movement(r_w: float, r_l: float) -> float:
    """D16.2 movement |r_L / r_W − 1| (r_w > 0 guaranteed by input validation)."""
    return abs(r_l / r_w - 1.0)


def combined_dispersion(d_eng_w, d_heddle_w, d_eng_l, d_heddle_l) -> float:
    """D16.2 D — LINEAR sums (interval arithmetic, not quadrature)."""
    return (d_eng_w + d_heddle_w) + (d_eng_l + d_heddle_l)


def gap_material(movement: float, dispersion_sum: float) -> bool:
    """D16.2: material iff movement > max(D, 0.05) (strict >)."""
    return movement > max(dispersion_sum, GAP_FLOOR)


def dispersion_character_change(d_w: float, d_l: float) -> bool:
    """D16.3 with the zero-dispersion guard (min(d)=0 never divides by zero)."""
    lo, hi = min(d_w, d_l), max(d_w, d_l)
    if lo == 0.0:
        # Appear-from-nothing dispersion above the 1% floor is material;
        # a zero-vs-sub-1% pair is not.
        return hi >= DISPERSION_FLOOR
    return (hi / lo) >= DISPERSION_RATIO and hi >= DISPERSION_FLOOR


# --- Table computation ---------------------------------------------------------

def validate_cell(win_cell: dict, lin_cell: dict) -> dict:
    """All three D16 verdicts for one workload cell. Returns a plain dict."""
    result = {"engines": {}, "pairs": []}
    win_h, lin_h = win_cell["heddle"], lin_cell["heddle"]

    common = [e for e in win_cell["engines"] if e in lin_cell["engines"]]
    for engine in common:
        w, l = win_cell["engines"][engine], lin_cell["engines"][engine]
        movement = gap_movement(w["r"], l["r"])
        disp_sum = combined_dispersion(w["d"], win_h["d"], l["d"], lin_h["d"])
        result["engines"][engine] = {
            "r_W": w["r"], "r_L": l["r"],
            "d_W": w["d"], "d_L": l["d"],
            "movement": movement, "D": disp_sum,
            "gap_material": gap_material(movement, disp_sum),
            "dispersion_change": dispersion_character_change(w["d"], l["d"]),
        }
    # Heddle anchor's own dispersion character (published like every cell value).
    result["heddle"] = {
        "d_W": win_h["d"], "d_L": lin_h["d"],
        "dispersion_change": dispersion_character_change(win_h["d"], lin_h["d"]),
    }
    # D16.1 pairwise over {engines + Heddle anchor} (r_Heddle from its own entry).
    participants = [(e, win_cell["engines"][e], lin_cell["engines"][e]) for e in common]
    participants.append(("Heddle", win_h, lin_h))
    for i in range(len(participants)):
        for j in range(i + 1, len(participants)):
            (name_a, wa, la), (name_b, wb, lb) = participants[i], participants[j]
            verdict = rank_flip_verdict(
                wa["r"], wa["d"], wb["r"], wb["d"],
                la["r"], la["d"], lb["r"], lb["d"],
            )
            result["pairs"].append({"pair": (name_a, name_b), "verdict": verdict})
    return result


def validate_all(windows_doc: dict, linux_doc: dict) -> dict:
    out = {}
    for eco, w_eco in windows_doc["ecosystems"].items():
        l_eco = linux_doc["ecosystems"].get(eco)
        if l_eco is None:
            out[eco] = {"status": "absent on Linux (manifest-recorded)"}
            continue
        eco_out = {}
        for track, w_track in w_eco["tracks"].items():
            l_track = l_eco["tracks"].get(track)
            if l_track is None:
                eco_out[track] = {"status": "absent on Linux (manifest-recorded)"}
                continue
            track_out = {}
            for workload, w_cell in w_track["cells"].items():
                l_cell = l_track["cells"].get(workload)
                if l_cell is None:
                    track_out[workload] = {"status": "excluded/absent on Linux (manifest-recorded)"}
                    continue
                track_out[workload] = validate_cell(w_cell, l_cell)
            eco_out[track] = track_out
        out[eco] = eco_out
    return out


def render_markdown(results: dict) -> str:
    """Part-2 validation tables. Every cell publishes r_W, r_L, movement %, D, d_W,
    d_L and the verdicts — 'material' controls prominence, never visibility."""
    lines = ["# Findings validation — computed tables (D15/D16)", ""]
    for eco, eco_res in results.items():
        lines.append(f"## {eco}")
        if "status" in eco_res:
            lines += [f"> {eco_res['status']}", ""]
            continue
        for track, track_res in eco_res.items():
            lines.append(f"### track: {track}")
            if "status" in track_res:
                lines += [f"> {track_res['status']}", ""]
                continue
            lines.append("")
            lines.append("| workload | engine | r_W | r_L | movement % | D | d_W | d_L | gap verdict | dispersion verdict |")
            lines.append("|---|---|---|---|---|---|---|---|---|---|")
            for workload, cell in track_res.items():
                if "status" in cell:
                    lines.append(f"| {workload} | — | — | — | — | — | — | — | {cell['status']} | — |")
                    continue
                for engine, row in cell["engines"].items():
                    gap = "**material**" if row["gap_material"] else "held"
                    disp = "**character change**" if row["dispersion_change"] else "held"
                    lines.append(
                        f"| {workload} | {engine} | {row['r_W']:.4g} | {row['r_L']:.4g} | "
                        f"{row['movement'] * 100:.2f}% | {row['D']:.4g} | {row['d_W']:.4g} | "
                        f"{row['d_L']:.4g} | {gap} | {disp} |"
                    )
                hd = cell["heddle"]
                disp = "**character change**" if hd["dispersion_change"] else "held"
                lines.append(
                    f"| {workload} | Heddle (anchor) | 1 | 1 | — | — | {hd['d_W']:.4g} | "
                    f"{hd['d_L']:.4g} | — | {disp} |"
                )
            lines.append("")
            lines.append("| workload | pair | rank verdict |")
            lines.append("|---|---|---|")
            for workload, cell in track_res.items():
                if "status" in cell:
                    continue
                for pair in cell["pairs"]:
                    verdict = pair["verdict"]
                    if verdict == "material flip":
                        verdict = "**material flip**"
                    elif verdict == "not resolved":
                        verdict = "order not resolved within disclosed dispersion"
                    lines.append(f"| {workload} | {pair['pair'][0]} vs {pair['pair'][1]} | {verdict} |")
            lines.append("")
    return "\n".join(lines) + "\n"


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Phase 8 findings validation (D15/D16/D17.4)")
    parser.add_argument("--windows-source", required=True,
                        help="windows-source.json (transcribed ranks/ratios/d — never absolute times)")
    parser.add_argument("--linux-results", required=True,
                        help="this run's ratios/d in the same shape (part 1 data)")
    parser.add_argument("--out", help="write the markdown tables here (default: stdout)")
    args = parser.parse_args(argv)

    try:
        windows_doc = load_side(args.windows_source, windows=True)
        linux_doc = load_side(args.linux_results, windows=False)
    except (ValidationError, OSError, json.JSONDecodeError) as exc:
        print(f"validate.py: malformed input: {exc}", file=sys.stderr)
        return 1

    results = validate_all(windows_doc, linux_doc)
    markdown = render_markdown(results)
    if args.out:
        with open(args.out, "w", encoding="utf-8", newline="\n") as fh:
            fh.write(markdown)
    else:
        sys.stdout.write(markdown)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
