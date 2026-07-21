// Package model transcribes the pinned Phase 1 workload models — exact strings and
// generation formulas — into Go structs materialized once in package var blocks (the Go
// analogue of the .NET Shared discipline). Field names keep the C# spelling (exported —
// reflection needs them); all numeric formatting is strconv.Itoa/%d only (every pinned
// number is an int), so output is locale-independent by construction.
//
// Normative source: docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/workloads.md
// Mapping rules: docs/spec/cross-stack-benchmarks/phase-6-go/port-mapping.md §Model transcription
package model

import "fmt"

// ---- trivial-substitution --------------------------------------------------------------------

// SubstitutionModel is the trivial-substitution card model (SubstitutionContent.cs).
type SubstitutionModel struct {
	Title        string
	Sku          string
	Price        int
	Brand        string
	Category     string
	Availability string
	Url          string
	ImageUrl     string
	Summary      string
	Rating       string
}

// Substitution is the pinned trivial-substitution model, materialized once.
var Substitution = SubstitutionModel{
	Title:        "Heddle Handbook",
	Sku:          "HB-2001",
	Price:        4200,
	Brand:        "Heddle Press",
	Category:     "Reference",
	Availability: "In stock",
	Url:          "/catalog/handbook",
	ImageUrl:     "/img/handbook.png",
	Summary:      "A concise field guide to the engine.",
	Rating:       "4.8",
}

// ---- large-loop ------------------------------------------------------------------------------

// LoopRow is one large-loop table row.
type LoopRow struct {
	Name  string
	Value int
}

// LoopModel is the large-loop model root.
type LoopModel struct{ Items []LoopRow }

// LargeLoop holds the 5,000 pinned rows (Name = "row-" + i, Value = i).
var LargeLoop = func() LoopModel {
	items := make([]LoopRow, 5000)
	for i := range items {
		items[i] = LoopRow{Name: fmt.Sprintf("row-%d", i), Value: i}
	}
	return LoopModel{Items: items}
}()

// ---- mixed-page ------------------------------------------------------------------------------

// MixedProduct is one mixed-page catalog card.
type MixedProduct struct {
	Name   string
	Sku    string
	Price  int
	OnSale bool
	Blurb  string
}

// MixedModel is the mixed-page model (MixedContent.cs).
type MixedModel struct {
	PageTitle      string
	StoreName      string
	HeroHeading    string
	HeroTagline    string
	ShowBanner     bool
	BannerText     string
	ShowDebugPanel bool
	FooterNote     string
	Year           int
	SupportEmail   string
	Products       []MixedProduct
}

// Mixed is the pinned mixed-page model: 36 products, i in [1, 36].
var Mixed = func() MixedModel {
	products := make([]MixedProduct, 36)
	for n := range products {
		i := n + 1
		products[n] = MixedProduct{
			Name:   fmt.Sprintf("Product %02d", i),
			Sku:    fmt.Sprintf("MX-%d", 1000+i),
			Price:  950 + i*7,
			OnSale: i%3 == 0,
			Blurb:  fmt.Sprintf("A dependable workshop staple from batch %d, checked for daily use and backed by our lifetime guarantee.", i),
		}
	}
	return MixedModel{
		PageTitle:      "Mercantile - Catalog",
		StoreName:      "Mercantile",
		HeroHeading:    "Autumn hardware sale",
		HeroTagline:    "Hand-picked tools, fair prices, shipped tomorrow.",
		ShowBanner:     true,
		BannerText:     "Free shipping on orders over 60.",
		ShowDebugPanel: false,
		FooterNote:     "Prices include VAT where applicable.",
		Year:           2026,
		SupportEmail:   "support at mercantile.example",
		Products:       products,
	}
}()

// ---- conditional-heavy -----------------------------------------------------------------------

// ConditionalRow is one conditional-heavy matrix row.
type ConditionalRow struct {
	Name     string
	Note     string
	IsBronze bool
	IsSilver bool
	IsGold   bool
	HasNote  bool
	IsActive bool
}

// ConditionalModel is the conditional-heavy model root.
type ConditionalModel struct{ Rows []ConditionalRow }

// Conditional holds the 200 pinned rows, i in [0, 199].
var Conditional = func() ConditionalModel {
	rows := make([]ConditionalRow, 200)
	for i := range rows {
		rows[i] = ConditionalRow{
			Name:     fmt.Sprintf("unit-%03d", i),
			Note:     fmt.Sprintf("note %d", i),
			IsBronze: i%4 == 0,
			IsSilver: i%4 == 1,
			IsGold:   i%4 == 2,
			HasNote:  i%2 == 0,
			IsActive: i%5 != 0,
		}
	}
	return ConditionalModel{Rows: rows}
}()

// ---- fragment-heavy --------------------------------------------------------------------------

// FragmentRow is one fragment-heavy tile.
type FragmentRow struct {
	Name  string
	Value int
	Badge string
}

// FragmentModel is the fragment-heavy model root.
type FragmentModel struct{ Items []FragmentRow }

// Fragment holds the 48 pinned tiles, i in [0, 47].
var Fragment = func() FragmentModel {
	badges := [4]string{"new", "hot", "sale", "std"}
	items := make([]FragmentRow, 48)
	for i := range items {
		items[i] = FragmentRow{
			Name:  fmt.Sprintf("tile-%02d", i),
			Value: i * 11,
			Badge: badges[i%4],
		}
	}
	return FragmentModel{Items: items}
}()

// ---- fortunes-encoded ------------------------------------------------------------------------

// FortuneRow is one fortunes table row.
type FortuneRow struct {
	Id      int
	Message string
}

// FortuneModel is the fortunes-encoded model root.
type FortuneModel struct{ Rows []FortuneRow }

// fortuneMessages are the 12 pinned messages, byte-for-byte (workloads.md workload 7).
// Rows 4 and 8 carry U+2014 em dashes; row 11 is the TechEmpower XSS payload; row 12 the
// Japanese string; row 1 is 4.33e67 (no '+' — the untrusted-data alphabet excludes it).
var fortuneMessages = [12]string{
	"A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1",
	"A computer program does what you tell it to do, not what you want it to do.",
	"A computer scientist is someone who fixes things that aren't broken.",
	"A list is only as strong as its weakest link. — Donald Knuth",
	"After enough decimal places, nobody gives a damn.",
	"Any program that runs right is obsolete.",
	"Computers make very fast, very accurate mistakes.",
	"Emacs is a nice operating system, but I prefer UNIX. — Tom Christiansen",
	"Feature: A bug with seniority.",
	"fortune: No such file or directory",
	"<script>alert(\"This should not be displayed in a browser alert box.\");</script>",
	"フレームワークのベンチマーク",
}

// Fortunes holds the 12 pinned rows, Ids 1–12.
var Fortunes = func() FortuneModel {
	rows := make([]FortuneRow, len(fortuneMessages))
	for i, message := range fortuneMessages {
		rows[i] = FortuneRow{Id: i + 1, Message: message}
	}
	return FortuneModel{Rows: rows}
}()

// ---- encoded-loop ----------------------------------------------------------------------------

// EncodedLoopRow is one encoded-loop table row; every cell carries escapable characters.
type EncodedLoopRow struct {
	Tag     string
	Name    string
	Comment string
}

// EncodedLoopModel is the encoded-loop model root.
type EncodedLoopModel struct{ Items []EncodedLoopRow }

// EncodedLoop holds the 5,000 pinned rows, i in [0, 4999].
var EncodedLoop = func() EncodedLoopModel {
	items := make([]EncodedLoopRow, 5000)
	for i := range items {
		items[i] = EncodedLoopRow{
			Tag:     fmt.Sprintf("tag-%d&'%d'", i, i%7),
			Name:    fmt.Sprintf("item <%d> & \"co\"", i),
			Comment: fmt.Sprintf("'q' & <angle> \"d\" こんにちは %d", i),
		}
	}
	return EncodedLoopModel{Items: items}
}()

// ---- untrusted-data alphabet surface (README D5) ---------------------------------------------

// LabeledValue is one materialized encoded-suite model string with its
// "<workload>.<field>[<row>]" label for the gate's alphabet assert.
type LabeledValue struct {
	Name  string
	Value string
}

// EncodedValues enumerates every materialized encoded-suite model string (both encoded
// workloads' untrusted values) for the gate startup alphabet scan.
func EncodedValues() []LabeledValue {
	values := make([]LabeledValue, 0, len(Fortunes.Rows)+3*len(EncodedLoop.Items))
	for i, row := range Fortunes.Rows {
		values = append(values, LabeledValue{fmt.Sprintf("fortunes-encoded.Message[%d]", i), row.Message})
	}
	for i, row := range EncodedLoop.Items {
		values = append(values,
			LabeledValue{fmt.Sprintf("encoded-loop.Tag[%d]", i), row.Tag},
			LabeledValue{fmt.Sprintf("encoded-loop.Name[%d]", i), row.Name},
			LabeledValue{fmt.Sprintf("encoded-loop.Comment[%d]", i), row.Comment})
	}
	return values
}
