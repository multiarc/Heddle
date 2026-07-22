package model_test

import (
	"strings"
	"testing"

	"heddle.dev/benchmarks/go/internal/corpus"
	"heddle.dev/benchmarks/go/internal/model"
)

// Model pins (README Testing plan) — expected counts restated as literals against the
// pinned formulas of workloads.md / port-mapping.md §Model transcription.

func TestSubstitutionPins(t *testing.T) {
	m := model.Substitution
	if m.Title != "Heddle Handbook" || m.Sku != "HB-2001" || m.Price != 4200 ||
		m.Summary != "A concise field guide to the engine." || m.Rating != "4.8" {
		t.Errorf("substitution pins drifted: %+v", m)
	}
}

func TestLargeLoopHas5000RowsWithPinnedEdges(t *testing.T) {
	rows := model.LargeLoop.Items
	if len(rows) != 5000 {
		t.Fatalf("got %d rows", len(rows))
	}
	if rows[0].Name != "row-0" || rows[0].Value != 0 ||
		rows[4999].Name != "row-4999" || rows[4999].Value != 4999 {
		t.Errorf("edge rows drifted: %+v %+v", rows[0], rows[4999])
	}
}

func TestMixedHas36ProductsWith12OnSale(t *testing.T) {
	m := model.Mixed
	if len(m.Products) != 36 {
		t.Fatalf("got %d products", len(m.Products))
	}
	onSale := 0
	for _, p := range m.Products {
		if p.OnSale {
			onSale++
		}
	}
	if onSale != 12 {
		t.Errorf("got %d on-sale products, want 12", onSale)
	}
	if m.Products[0].Name != "Product 01" || m.Products[0].Sku != "MX-1001" ||
		m.Products[35].Name != "Product 36" || m.Products[35].Sku != "MX-1036" ||
		m.Products[35].Price != 950+36*7 {
		t.Errorf("product pins drifted: %+v %+v", m.Products[0], m.Products[35])
	}
	if !m.ShowBanner || m.ShowDebugPanel || m.Year != 2026 ||
		m.SupportEmail != "support at mercantile.example" {
		t.Errorf("page pins drifted: %+v", m)
	}
}

func TestConditionalHas200RowsWithPinnedDistributions(t *testing.T) {
	rows := model.Conditional.Rows
	if len(rows) != 200 {
		t.Fatalf("got %d rows", len(rows))
	}
	var bronze, silver, gold, platinum, note, active int
	for _, r := range rows {
		switch {
		case r.IsBronze:
			bronze++
		case r.IsSilver:
			silver++
		case r.IsGold:
			gold++
		default:
			platinum++
		}
		if r.HasNote {
			note++
		}
		if r.IsActive {
			active++
		}
	}
	if bronze != 50 || silver != 50 || gold != 50 || platinum != 50 || note != 100 || active != 160 {
		t.Errorf("distributions drifted: %d/%d/%d/%d note %d active %d", bronze, silver, gold, platinum, note, active)
	}
	if rows[0].Name != "unit-000" || rows[199].Name != "unit-199" || rows[7].Note != "note 7" {
		t.Errorf("row pins drifted")
	}
}

func TestFragmentHas48RowsWithPinnedBadges(t *testing.T) {
	items := model.Fragment.Items
	if len(items) != 48 {
		t.Fatalf("got %d items", len(items))
	}
	if items[0].Name != "tile-00" || items[47].Name != "tile-47" || items[47].Value != 47*11 ||
		items[1].Badge != "hot" {
		t.Errorf("tile pins drifted")
	}
	newBadges := 0
	for _, it := range items {
		if it.Badge == "new" {
			newBadges++
		}
	}
	if newBadges != 12 {
		t.Errorf("got %d new badges, want 12", newBadges)
	}
}

func TestFortunesHas12PinnedRows(t *testing.T) {
	rows := model.Fortunes.Rows
	if len(rows) != 12 {
		t.Fatalf("got %d rows", len(rows))
	}
	if rows[0].Id != 1 || rows[11].Id != 12 {
		t.Errorf("ids drifted")
	}
	if rows[10].Message != `<script>alert("This should not be displayed in a browser alert box.");</script>` {
		t.Errorf("XSS payload drifted: %q", rows[10].Message)
	}
	if rows[11].Message != "フレームワークのベンチマーク" {
		t.Errorf("Japanese row drifted: %q", rows[11].Message)
	}
	// Rows 4/8 em dashes are U+2014; row 1 carries no '+'.
	if !strings.ContainsRune(rows[3].Message, '—') || !strings.ContainsRune(rows[7].Message, '—') {
		t.Errorf("em dashes must be U+2014")
	}
	if strings.ContainsRune(rows[0].Message, '+') {
		t.Errorf("row 1 must be 4.33e67 with no '+'")
	}
}

func TestEncodedLoopHas5000RowsWithPinnedRow0(t *testing.T) {
	items := model.EncodedLoop.Items
	if len(items) != 5000 {
		t.Fatalf("got %d rows", len(items))
	}
	if items[0].Tag != "tag-0&'0'" || items[0].Name != `item <0> & "co"` ||
		items[0].Comment != `'q' & <angle> "d" こんにちは 0` {
		t.Errorf("row 0 drifted: %+v", items[0])
	}
	if items[4999].Tag != "tag-4999&'1'" {
		t.Errorf("row 4999 drifted: %+v", items[4999])
	}
}

// ---- composed-page fragments -----------------------------------------------------------------

func TestComposedFragmentsAreNonemptyExceptArea6(t *testing.T) {
	m := model.Composed
	if len(m.AreaNames) != 7 || len(m.Areas) != 7 {
		t.Fatalf("area shape drifted")
	}
	for _, name := range m.AreaNames {
		if name == "Alert Top Section Below Nav" {
			if m.Areas[name] != "" {
				t.Errorf("%q must be the empty entry", name)
			}
		} else if m.Areas[name] == "" {
			t.Errorf("%q must be non-empty", name)
		}
	}
	if m.Section["meta"] != "<title>Title</title>" {
		t.Errorf("section meta drifted: %q", m.Section["meta"])
	}
	if !strings.HasPrefix(m.Section["social"], `<meta property="og:image"`) {
		t.Errorf("section social drifted")
	}
	if m.Section["page_scripts"] != "" || m.Section["endpage_scripts"] != "" {
		t.Errorf("page_scripts/endpage_scripts must be empty")
	}
	if m.Comp["custom_styles"] != "/* CSS Comment Test */" {
		t.Errorf("custom_styles drifted: %q", m.Comp["custom_styles"])
	}
}

// The transcription byte-check: the fragments assembled in twin order with zero separator
// bytes, run through the stored-form pipeline (N2–N4), must be byte-identical to the
// committed composed-page oracle — proving the embedded copies match the C# literals at the
// corpus's generating commit.
func TestComposedAssemblyMatchesTheCorpusOracle(t *testing.T) {
	golden, err := corpus.LoadGolden("composed-page")
	if err != nil {
		t.Fatal(err)
	}
	assembled := corpus.NormalizeRaw(model.ComposedAssembled())
	if assembled != golden {
		at := 0
		n := min(len(assembled), len(golden))
		for at < n && assembled[at] == golden[at] {
			at++
		}
		t.Fatalf("assembled fragments diverge from the oracle: first diff at %d (exp %d/act %d bytes)",
			at, len(golden), len(assembled))
	}
}
