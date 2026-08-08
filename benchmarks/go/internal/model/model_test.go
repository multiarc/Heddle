package model_test

import (
	"fmt"
	"strings"
	"testing"

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
	// E21: the row carries ONLY Value — the display name `row-{i}` is template-composed.
	if rows[0].Value != 0 || rows[4999].Value != 4999 {
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
	// E21: SkuNumber/Batch are ints — templates compose `MX-@(SkuNumber)` and the blurb.
	if m.Products[0].Name != "Product 01" || m.Products[0].SkuNumber != 1001 ||
		m.Products[0].Batch != 1 ||
		m.Products[35].Name != "Product 36" || m.Products[35].SkuNumber != 1036 ||
		m.Products[35].Batch != 36 || m.Products[35].Price != 950+36*7 {
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
	// E21: Seq is an int — the template composes `note @(Seq)`.
	if rows[0].Name != "unit-000" || rows[199].Name != "unit-199" || rows[7].Seq != 7 {
		t.Errorf("row pins drifted")
	}
}

func TestFragmentHas48RowsWithPinnedKindsAndPromos(t *testing.T) {
	items := model.Fragment.Items
	if len(items) != 48 {
		t.Fatalf("got %d items", len(items))
	}
	// E20 pins: item-{i:D2} identity names, Value = i*11, Delta = i%7-3, Promo on every row.
	if items[0].Name != "item-00" || items[47].Name != "item-47" || items[47].Value != 47*11 ||
		items[1].Badge != "hot" || items[0].Delta != -3 || items[6].Delta != 3 ||
		items[47].Delta != 47%7-3 {
		t.Errorf("row pins drifted: %+v %+v", items[0], items[47])
	}
	kindCounts := map[string]int{}
	for i, it := range items {
		kindCounts[it.Kind]++
		// Exactly one dispatch boolean fires, and it matches Kind.
		set := 0
		for _, b := range []bool{it.IsTile, it.IsCard, it.IsMedia, it.IsStat} {
			if b {
				set++
			}
		}
		want := map[string]bool{"tile": it.IsTile, "card": it.IsCard, "media": it.IsMedia, "stat": it.IsStat}
		if set != 1 || !want[it.Kind] {
			t.Errorf("row %d dispatch booleans inconsistent with Kind %q: %+v", i, it.Kind, it)
		}
		// Promo is on EVERY row: Label == Badge, Price = 9 + i (an int).
		if it.Promo.Label != it.Badge || it.Promo.Price != 9+i {
			t.Errorf("row %d promo drifted: %+v", i, it.Promo)
		}
	}
	for _, kind := range []string{"tile", "card", "media", "stat"} {
		if kindCounts[kind] != 12 {
			t.Errorf("got %d %q rows, want 12", kindCounts[kind], kind)
		}
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

// ---- composed-page nav model (E20 structured nav; E22 no text blobs) -------------------------

func TestComposedNavHasThePinnedShape(t *testing.T) {
	m := model.Composed()
	nav := m.Nav
	if len(nav.Menus) != 2 {
		t.Fatalf("got %d menus, want 2 (wholesale + retail)", len(nav.Menus))
	}
	for i, menu := range nav.Menus {
		if len(menu.Tabs) != 6 {
			t.Errorf("menu %d has %d tabs, want 6", i, len(menu.Tabs))
		}
	}
	if len(nav.FooterColumns) != 4 {
		t.Errorf("got %d footer columns, want 4", len(nav.FooterColumns))
	}
	// Spot pins against the fixture's declaration order (nav.json, snake_case keys).
	tab := nav.Menus[0].Tabs[0]
	if tab.Label != "Shop All Products" || tab.Href != "#" || tab.Css != "shop-all-products" ||
		!tab.HasDropdown || tab.DropdownCss != "dropdown_2columns" {
		t.Errorf("menu 0 tab 0 drifted: %+v", tab)
	}
	if len(tab.Columns) == 0 || len(tab.Columns[0].Sections) == 0 {
		t.Fatalf("menu 0 tab 0 columns drifted")
	}
	sec := tab.Columns[0].Sections[0]
	if sec.Title != "FISH" || sec.TitleLinked || len(sec.Links) == 0 ||
		sec.Links[0].Label != "Salmon" || sec.Links[0].Href != "/products/wild-salmon" {
		t.Errorf("menu 0 tab 0 section 0 drifted: %+v", sec)
	}
	fsec := nav.FooterColumns[0].Sections[0]
	if fsec.Title != "Need Help?" || fsec.TitleLinked ||
		fsec.Links[0].Label != "Customer Service" ||
		fsec.Links[0].Href != "/content/contact-customer-service" {
		t.Errorf("footer column 0 section 0 drifted: %+v", fsec)
	}
}

// The fixture loads exactly once (sync.Once): repeated calls return views over the same
// backing arrays, never a re-read.
func TestComposedNavLoadsOnce(t *testing.T) {
	a, b := model.Composed(), model.Composed()
	if len(a.Nav.Menus) == 0 || &a.Nav.Menus[0] != &b.Nav.Menus[0] {
		t.Errorf("Composed() must return the once-loaded model")
	}
}

// Rule-4 sanitization (workloads.md workload 1, blocking pre-decision for every port):
// every nav text value is printable ASCII with none of & < > " ' — raw and would-be-escaped
// renderings coincide, so no default-escaping engine can double-escape.
func TestComposedNavValuesAreRule4Clean(t *testing.T) {
	checkRule4 := func(where, value string) {
		t.Helper()
		for _, r := range value {
			if r < 0x20 || r > 0x7E || strings.ContainsRune(`&<>"'`, r) {
				t.Errorf("%s violates rule 4: %q U+%04X in %q", where, r, r, value)
				return
			}
		}
	}
	var checkColumns func(where string, cols []model.NavColumn)
	checkColumns = func(where string, cols []model.NavColumn) {
		for ci, col := range cols {
			for si, sec := range col.Sections {
				at := fmt.Sprintf("%s.columns[%d].sections[%d]", where, ci, si)
				checkRule4(at+".title", sec.Title)
				checkRule4(at+".href", sec.Href)
				for li, link := range sec.Links {
					checkRule4(fmt.Sprintf("%s.links[%d].label", at, li), link.Label)
					checkRule4(fmt.Sprintf("%s.links[%d].href", at, li), link.Href)
				}
			}
		}
	}
	nav := model.Composed().Nav
	for mi, menu := range nav.Menus {
		for ti, tab := range menu.Tabs {
			at := fmt.Sprintf("menus[%d].tabs[%d]", mi, ti)
			checkRule4(at+".label", tab.Label)
			checkRule4(at+".href", tab.Href)
			checkRule4(at+".css", tab.Css)
			checkRule4(at+".dropdown_css", tab.DropdownCss)
			checkColumns(at, tab.Columns)
		}
	}
	checkColumns("footer_columns", nav.FooterColumns)
}
