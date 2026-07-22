#!/usr/bin/env python3
"""test_validate.py — threshold-straddling fixtures for validate.py (spec WI5 /
Testing plan): D16.1 resolved/unresolved flips, D16.2 movement on both sides of
max(D, 0.05), D16.3 dispersion ratio and 1% floor boundaries, the min(d)=0
zero-dispersion guard, and the D17.4 structural rejection of absolute-time fields.
stdlib-only; run: python3 test_validate.py
"""

import copy
import unittest

import validate as v


def wcell(engine_r, engine_d, heddle_d, rank=2):
    return {
        "engines": {
            "engineA": {"rank": rank, "r": engine_r, "d": engine_d,
                        "source": "docs/benchmarks/2099-01-01/example-report.md"},
        },
        "heddle": {"r": 1.0, "d": heddle_d,
                   "source": "docs/benchmarks/2099-01-01/example-report.md"},
    }


def lcell(engine_r, engine_d, heddle_d):
    return {
        "engines": {"engineA": {"r": engine_r, "d": engine_d}},
        "heddle": {"r": 1.0, "d": heddle_d},
    }


def windows_doc(cell):
    return {
        "schema": v.SCHEMA_ID,
        "ecosystems": {"rust": {"tracks": {"controlled": {"cells": {"composed-page": cell}}}}},
    }


def linux_doc(cell):
    return {
        "ecosystems": {"rust": {"tracks": {"controlled": {"cells": {"composed-page": cell}}}}},
    }


class TestRankFlip(unittest.TestCase):
    """D16.1 — flip material iff order differs AND resolved on BOTH OSes."""

    def test_resolved_flip_is_material(self):
        verdict = v.rank_flip_verdict(
            1.2, 0.01, 1.5, 0.01,   # Windows: A < B, resolved (0.3 > 0.024)
            1.5, 0.01, 1.2, 0.01,   # Linux:   A > B, resolved
        )
        self.assertEqual(verdict, "material flip")

    def test_flip_unresolved_on_one_os_is_not_material(self):
        verdict = v.rank_flip_verdict(
            1.2, 0.01, 1.5, 0.01,     # Windows resolved
            1.32, 0.05, 1.30, 0.05,   # Linux flipped but 0.02 <= 0.1*1.30
        )
        self.assertEqual(verdict, "not resolved")

    def test_same_order_is_held(self):
        verdict = v.rank_flip_verdict(
            1.2, 0.01, 1.5, 0.01,
            1.1, 0.01, 1.6, 0.01,
        )
        self.assertEqual(verdict, "held")

    def test_resolution_boundary_exact_is_unresolved(self):
        # |r_A - r_B| == (d_A + d_B) * min(r_A, r_B) exactly -> strict > fails.
        # Binary-exact values: |1.0 - 1.5| = 0.5 == (0.25 + 0.25) * 1.0.
        self.assertFalse(v.pair_resolved(1.0, 0.25, 1.5, 0.25))    # 0.5 == 0.5
        self.assertTrue(v.pair_resolved(1.0, 0.25, 1.53, 0.25))    # 0.53 > 0.5
        # A flip whose Windows side sits exactly on the boundary is not material.
        verdict = v.rank_flip_verdict(
            1.0, 0.25, 1.5, 0.25,   # Windows: exactly at threshold -> unresolved
            1.4, 0.01, 1.2, 0.01,   # Linux: flipped and resolved
        )
        self.assertEqual(verdict, "not resolved")


class TestGapMovement(unittest.TestCase):
    """D16.2 — |r_L/r_W − 1| > max(D, 0.05); D is a LINEAR sum."""

    def test_linear_sum_not_quadrature(self):
        self.assertAlmostEqual(v.combined_dispersion(0.01, 0.02, 0.03, 0.04), 0.10)

    def test_floor_active_just_above(self):
        # D = 0.02 < floor 0.05 -> threshold is 0.05.
        d_sum = v.combined_dispersion(0.005, 0.005, 0.005, 0.005)
        self.assertAlmostEqual(d_sum, 0.02)
        self.assertTrue(v.gap_material(v.gap_movement(1.0, 1.0501), d_sum))

    def test_floor_active_just_below(self):
        d_sum = v.combined_dispersion(0.005, 0.005, 0.005, 0.005)
        self.assertFalse(v.gap_material(v.gap_movement(1.0, 1.0499), d_sum))

    def test_floor_boundary_exact_is_not_material(self):
        # movement == 0.05 exactly -> strict > fails.
        self.assertFalse(v.gap_material(0.05, 0.02))

    def test_dispersion_dominant_just_above(self):
        # D = 0.08 > floor -> threshold is D.
        d_sum = v.combined_dispersion(0.02, 0.02, 0.02, 0.02)
        self.assertAlmostEqual(d_sum, 0.08)
        self.assertTrue(v.gap_material(v.gap_movement(1.0, 1.081), d_sum))

    def test_dispersion_dominant_just_below(self):
        d_sum = v.combined_dispersion(0.02, 0.02, 0.02, 0.02)
        self.assertFalse(v.gap_material(v.gap_movement(1.0, 1.079), d_sum))

    def test_dispersion_boundary_exact_is_not_material(self):
        self.assertFalse(v.gap_material(0.08, 0.08))

    def test_movement_is_symmetric_ratio_form(self):
        self.assertAlmostEqual(v.gap_movement(2.0, 1.0), 0.5)
        self.assertAlmostEqual(v.gap_movement(1.0, 2.0), 1.0)


class TestDispersionCharacter(unittest.TestCase):
    """D16.3 — ratio ≥ 2 AND max ≥ 0.01, with the zero-dispersion guard."""

    def test_ratio_exactly_two_with_max_at_floor_is_material(self):
        self.assertTrue(v.dispersion_character_change(0.005, 0.01))   # ratio 2, max 0.01

    def test_ratio_just_below_two_is_not_material(self):
        self.assertFalse(v.dispersion_character_change(0.01, 0.0199))

    def test_ratio_above_two_but_below_one_percent_floor_is_not_material(self):
        self.assertFalse(v.dispersion_character_change(0.004, 0.009))  # ratio 2.25, max < 0.01

    def test_order_symmetric(self):
        self.assertTrue(v.dispersion_character_change(0.02, 0.005))
        self.assertTrue(v.dispersion_character_change(0.005, 0.02))

    def test_zero_dispersion_guard_material_above_floor(self):
        # min(d)=0: no division; material iff max >= 0.01.
        self.assertTrue(v.dispersion_character_change(0.0, 0.012))

    def test_zero_dispersion_guard_not_material_below_floor(self):
        self.assertFalse(v.dispersion_character_change(0.0, 0.009))

    def test_zero_vs_zero_is_not_material(self):
        self.assertFalse(v.dispersion_character_change(0.0, 0.0))

    def test_zero_dispersion_never_raises(self):
        try:
            v.dispersion_character_change(0.0, 0.5)
            v.dispersion_character_change(0.5, 0.0)
        except ZeroDivisionError:  # pragma: no cover
            self.fail("D16.3 zero-dispersion guard divided by zero")


class TestStructuralD174(unittest.TestCase):
    """D17.4 — any absolute-time field is rejected; citations are mandatory."""

    def test_valid_input_loads(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        v.load_side(doc, windows=True)  # must not raise

    def test_absolute_time_field_rejected_top_level(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        doc["meanTimeNs"] = 123.4
        with self.assertRaises(v.ValidationError) as ctx:
            v.load_side(doc, windows=True)
        self.assertIn("D17.4", str(ctx.exception))

    def test_absolute_time_field_rejected_inside_cell(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        cell = doc["ecosystems"]["rust"]["tracks"]["controlled"]["cells"]["composed-page"]
        cell["engines"]["engineA"]["point_estimate"] = 41.5
        with self.assertRaises(v.ValidationError) as ctx:
            v.load_side(doc, windows=True)
        self.assertIn("D17.4", str(ctx.exception))

    def test_absolute_time_field_rejected_on_linux_side_too(self):
        doc = linux_doc(lcell(1.2, 0.01, 0.008))
        cell = doc["ecosystems"]["rust"]["tracks"]["controlled"]["cells"]["composed-page"]
        cell["heddle"]["wall_time_ms"] = 0.9
        with self.assertRaises(v.ValidationError):
            v.load_side(doc, windows=False)

    def test_unknown_key_on_value_object_rejected(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        cell = doc["ecosystems"]["rust"]["tracks"]["controlled"]["cells"]["composed-page"]
        cell["engines"]["engineA"]["throughput"] = 9.9
        with self.assertRaises(v.ValidationError):
            v.load_side(doc, windows=True)

    def test_citationless_windows_entry_rejected(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        cell = doc["ecosystems"]["rust"]["tracks"]["controlled"]["cells"]["composed-page"]
        del cell["engines"]["engineA"]["source"]
        with self.assertRaises(v.ValidationError) as ctx:
            v.load_side(doc, windows=True)
        self.assertIn("source", str(ctx.exception))

    def test_linux_side_does_not_require_citations(self):
        v.load_side(linux_doc(lcell(1.2, 0.01, 0.008)), windows=False)

    def test_wrong_schema_id_rejected(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        doc["schema"] = "something-else"
        with self.assertRaises(v.ValidationError):
            v.load_side(doc, windows=True)

    def test_missing_windows_rank_rejected(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        cell = doc["ecosystems"]["rust"]["tracks"]["controlled"]["cells"]["composed-page"]
        del cell["engines"]["engineA"]["rank"]
        with self.assertRaises(v.ValidationError):
            v.load_side(doc, windows=True)

    def test_nonpositive_ratio_rejected(self):
        doc = windows_doc(wcell(1.2, 0.01, 0.008))
        cell = doc["ecosystems"]["rust"]["tracks"]["controlled"]["cells"]["composed-page"]
        cell["engines"]["engineA"]["r"] = 0.0
        with self.assertRaises(v.ValidationError):
            v.load_side(doc, windows=True)


class TestEndToEnd(unittest.TestCase):
    """Whole-pipeline checks: verdicts are data; everything is published."""

    def test_tables_publish_every_cell_regardless_of_verdict(self):
        w = v.load_side(windows_doc(wcell(1.2, 0.005, 0.005)), windows=True)
        l = v.load_side(linux_doc(lcell(1.21, 0.005, 0.005)), windows=False)
        results = v.validate_all(w, l)
        cell = results["rust"]["controlled"]["composed-page"]
        row = cell["engines"]["engineA"]
        self.assertFalse(row["gap_material"])          # 0.83% movement, under floor
        self.assertFalse(row["dispersion_change"])
        md = v.render_markdown(results)
        for token in ("r_W", "r_L", "movement %", "engineA", "Heddle (anchor)"):
            self.assertIn(token, md)

    def test_material_gap_end_to_end(self):
        w = v.load_side(windows_doc(wcell(1.0, 0.005, 0.005)), windows=True)
        l = v.load_side(linux_doc(lcell(1.10, 0.005, 0.005)), windows=False)
        cell = v.validate_all(w, l)["rust"]["controlled"]["composed-page"]
        self.assertTrue(cell["engines"]["engineA"]["gap_material"])  # 10% > max(0.02, 0.05)

    def test_zero_dispersion_cell_end_to_end(self):
        # The WI5-named fixture: a min(d)=0 cell flows through validate_all safely.
        w = v.load_side(windows_doc(wcell(1.2, 0.0, 0.0)), windows=True)
        l = v.load_side(linux_doc(lcell(1.2, 0.012, 0.0)), windows=False)
        cell = v.validate_all(w, l)["rust"]["controlled"]["composed-page"]
        self.assertTrue(cell["engines"]["engineA"]["dispersion_change"])   # 0 -> 1.2%
        self.assertFalse(cell["heddle"]["dispersion_change"])              # 0 vs 0

    def test_absent_linux_ecosystem_reported_not_crashed(self):
        w = v.load_side(windows_doc(wcell(1.2, 0.01, 0.008)), windows=True)
        l = copy.deepcopy(linux_doc(lcell(1.2, 0.01, 0.008)))
        l["ecosystems"]["go"] = l["ecosystems"].pop("rust")
        l = v.load_side(l, windows=False)
        results = v.validate_all(w, l)
        self.assertIn("absent on Linux", results["rust"]["status"])

    def test_heddle_anchor_participates_in_rank_pairs(self):
        # Engine crosses the anchor between OSes, resolved on both -> material flip.
        w = v.load_side(windows_doc(wcell(0.8, 0.01, 0.01)), windows=True)
        l = v.load_side(linux_doc(lcell(1.3, 0.01, 0.01)), windows=False)
        cell = v.validate_all(w, l)["rust"]["controlled"]["composed-page"]
        pair_verdicts = {tuple(p["pair"]): p["verdict"] for p in cell["pairs"]}
        self.assertEqual(pair_verdicts[("engineA", "Heddle")], "material flip")


if __name__ == "__main__":
    unittest.main(verbosity=2)
