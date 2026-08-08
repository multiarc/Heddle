package model

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sync"

	"heddle.dev/benchmarks/go/internal/corpus"
)

// composed-page model — pure structured data.
// The model is ComposedModel{Nav} and NOTHING else: every fragment of literal page text
// (the former embedded blob areas and asset/script snippets) lives in the templates, owned
// by the per-engine template tier ("the model-preparation tier carries DATA only"). The
// structured navigation — two mega menus plus the footer link columns — is loaded once,
// on first use, from the corpus fixture `fixtures/composed-page/nav.json` (snake_case
// keys, exported by the .NET harness's export-corpus command and hash-recorded in the manifest's
// `fixtures` section), resolved through the same corpus-dir resolution the gate uses
// (corpus.Dir(); go:embed cannot reach ../../dotnet, so this is a runtime file read).
// Transcription errors cannot ship: the controlled byte gate compares the rendered page
// against the corpus entry, so any fixture drift fails loudly before timing.

// NavLink is one navigation link.
type NavLink struct {
	Label string `json:"label"`
	Href  string `json:"href"`
}

// NavSection is one titled link group; TitleLinked is a precomputed boolean (no engine
// evaluates a string test).
type NavSection struct {
	Title       string    `json:"title"`
	Href        string    `json:"href"`
	TitleLinked bool      `json:"title_linked"`
	Links       []NavLink `json:"links"`
}

// NavColumn is one column of sections (dropdown column or footer column).
type NavColumn struct {
	Sections []NavSection `json:"sections"`
}

// MenuTab is one top-level menu tab; HasDropdown is precomputed.
type MenuTab struct {
	Label       string      `json:"label"`
	Href        string      `json:"href"`
	Css         string      `json:"css"`
	HasDropdown bool        `json:"has_dropdown"`
	DropdownCss string      `json:"dropdown_css"`
	Columns     []NavColumn `json:"columns"`
}

// MegaMenu is one mega menu (wholesale / retail).
type MegaMenu struct {
	Tabs []MenuTab `json:"tabs"`
}

// NavModel is the structured navigation root (nav.json's document shape).
type NavModel struct {
	Menus         []MegaMenu  `json:"menus"`
	FooterColumns []NavColumn `json:"footer_columns"`
}

// ComposedModel is the composed-page workload model: the nav, and nothing else.
type ComposedModel struct {
	Nav NavModel
}

var (
	composedOnce  sync.Once
	composedModel ComposedModel
)

// Composed returns the pinned composed-page model, loading nav.json exactly once. A
// missing or malformed fixture panics with the export-corpus regeneration hint — the
// benchmark cannot run meaningfully without the fixture, and a silent zero model would
// fail the byte gate with a far less actionable diff.
func Composed() ComposedModel {
	composedOnce.Do(func() {
		path := filepath.Join(corpus.Dir(), "fixtures", "composed-page", "nav.json")
		raw, err := os.ReadFile(path)
		if err != nil {
			panic("model: cannot read composed-page nav fixture " + path +
				": " + err.Error() +
				" (regenerate via the corpus exporter: dotnet run -c Release --project benchmarks/dotnet -- export-corpus)")
		}
		var nav NavModel
		if err := json.Unmarshal(raw, &nav); err != nil {
			panic("model: cannot parse composed-page nav fixture " + path + ": " + err.Error())
		}
		composedModel = ComposedModel{Nav: nav}
	})
	return composedModel
}
