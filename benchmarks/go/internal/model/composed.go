package model

import (
	"embed"
	"path"
)

// composed-page fragments — RUNTIME DATA in every engine (the .NET twins read them from
// TwinContent/AreaComponent, never transcribe them into template text; port-mapping.md
// §Model transcription). The files under data/composed-page/ are byte-exact copies of the
// C# verbatim literals (identical to the byte-verified copies at
// benchmarks/rust/data/composed-page/*, pinned -text in benchmarks/go/.gitattributes) and
// are embedded so their whitespace — part of the bytes — cannot be smudged. Transcription
// errors cannot ship: the controlled byte gate compares the assembled output against the
// corpus entry, so any drift fails loudly before timing.

//go:embed data/composed-page
var composedData embed.FS

func fragment(name string) string {
	raw, err := composedData.ReadFile(path.Join("data/composed-page", name))
	if err != nil {
		panic("model: missing embedded composed-page fragment " + name + ": " + err.Error())
	}
	return string(raw)
}

// AreaOrder lists the seven area names in the exact order layout.heddle issues them
// (TwinContent.AreaOrder); "Alert Top Section Below Nav" is the pinned empty entry.
var AreaOrder = [7]string{
	"Alert Top Section Above Nav",
	"Secondary Wholesale Menu",
	"Secondary Retail Menu",
	"Wholesale Top Mega Menu",
	"Retail Top Mega Menu",
	"Alert Top Section Below Nav", // empty content
	"Footer Links",
}

// ComposedModel is the composed-page model (port-mapping.md §Workload 1): section and
// component fragments keyed by the twin keys, plus the ordered area names and their
// fragments.
type ComposedModel struct {
	Section   map[string]string
	Comp      map[string]string
	AreaNames []string
	Areas     map[string]string
}

// Composed is the pinned composed-page model, materialized once from the embedded
// fragments. Keys mirror the twin keys (LiquidTemplates.LayoutTemplate): Section — meta,
// social, page_scripts, endpage_scripts; Comp — assets_styles, custom_styles, head_scripts,
// body_scripts, assets_scripts, body_end_scripts.
var Composed = func() ComposedModel {
	areaFiles := [7]string{
		"area-1.html", "area-2.html", "area-3.html", "area-4.html",
		"area-5.html", "area-6.html", "area-7.html",
	}
	areas := make(map[string]string, len(AreaOrder))
	for i, name := range AreaOrder {
		areas[name] = fragment(areaFiles[i])
	}
	return ComposedModel{
		Section: map[string]string{
			"meta":            fragment("section-meta.html"),
			"social":          fragment("section-social.html"),
			"page_scripts":    "",
			"endpage_scripts": "",
		},
		Comp: map[string]string{
			"assets_styles":    fragment("comp-assets-styles.html"),
			"custom_styles":    fragment("comp-custom-styles.html"),
			"head_scripts":     fragment("comp-head-scripts.html"),
			"body_scripts":     fragment("comp-body-scripts.html"),
			"assets_scripts":   fragment("comp-assets-scripts.html"),
			"body_end_scripts": fragment("comp-body-end-scripts.html"),
		},
		AreaNames: AreaOrder[:],
		Areas:     areas,
	}
}()

// ComposedAssembled concatenates the fragments in twin order with zero separator bytes —
// the oracle's documented shape (LiquidTemplates.LayoutTemplate chains its output actions
// with no whitespace). Used by tests to byte-check the transcription against the corpus;
// engines render through their own composition machinery, never through this helper.
func ComposedAssembled() string {
	m := Composed
	out := m.Section["meta"] + m.Section["social"] +
		m.Comp["assets_styles"] + m.Comp["custom_styles"] +
		m.Comp["head_scripts"] + m.Comp["body_scripts"]
	for _, name := range m.AreaNames {
		out += m.Areas[name]
	}
	return out + m.Comp["assets_scripts"] + m.Section["page_scripts"] +
		m.Section["endpage_scripts"] + m.Comp["body_end_scripts"]
}
