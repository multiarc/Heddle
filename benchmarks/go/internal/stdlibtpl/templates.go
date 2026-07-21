// Package stdlibtpl hosts the stdlib-engine controlled ports: text/template for the six
// raw workloads and html/template for the two encoded workloads (README D1 / Q6.1 — both
// surfaces of the one credibility-pick stdlib engine; benchmark ids stdlib-text /
// stdlib-html). Template sources are Go raw string literals authored densely — byte-for-byte
// the pinned Heddle/twin shape with {{…}} actions in place of @(…) substitutions, no
// whitespace beyond what the pinned shape carries. Every template is parsed once in a
// package var block (the cached-template render path); renders execute into a reused
// pre-grown buffer (render.go).
//
// Normative texts: docs/spec/cross-stack-benchmarks/phase-6-go/port-mapping.md
// §Controlled track — stdlib surfaces (workload shapes from Phase 1 workloads.md).
package stdlibtpl

import (
	htmltemplate "html/template"
	texttemplate "text/template"
)

// ---- workload 1 — composed-page (text/template) ----------------------------------------------

// composedHomeSrc is the home template (the include mapping of the twin table).
const composedHomeSrc = `{{template "layout" .}}`

// composedLayoutSrc is the layout template — one line, no whitespace between actions,
// mirroring LiquidTemplates.LayoutTemplate's zero-separator concatenation.
const composedLayoutSrc = `{{define "layout"}}{{index .Section "meta"}}{{index .Section "social"}}{{index .Comp "assets_styles"}}{{index .Comp "custom_styles"}}{{index .Comp "head_scripts"}}{{index .Comp "body_scripts"}}{{range .AreaNames}}{{index $.Areas .}}{{end}}{{index .Comp "assets_scripts"}}{{index .Section "page_scripts"}}{{index .Section "endpage_scripts"}}{{index .Comp "body_end_scripts"}}{{end}}`

// ---- workload 2 — trivial-substitution (text/template) ---------------------------------------

// Dense one-line <article> card, ten {{.Member}} substitutions in pinned order, including
// the two attribute positions.
const trivialSubstitutionSrc = `<article><h1>{{.Title}}</h1><p class="sku">{{.Sku}}</p><p class="price">{{.Price}}</p><p class="brand">{{.Brand}}</p><p class="cat">{{.Category}}</p><p class="avail">{{.Availability}}</p><a class="link" href="{{.Url}}"><img src="{{.ImageUrl}}"></a><p class="sum">{{.Summary}}</p><p class="rating">{{.Rating}}</p></article>`

// ---- workload 3 — large-loop (text/template) -------------------------------------------------

// Value is an int; text/template renders ints via fmt (%v), identical bytes to strconv.Itoa.
const largeLoopSrc = `{{range .Items}}<tr><td>{{.Name}}</td><td>{{.Value}}</td></tr>{{end}}`

// ---- workload 4 — mixed-page (text/template) -------------------------------------------------

// The pinned skeleton transcribed line-for-line (line breaks between sibling elements are
// N2/N3-erased; the <style> line and all text-bearing elements stay dense); the footer
// keeps its single literal spaces.
const mixedPageSrc = `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<title>{{.PageTitle}}</title>
<style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
</head>
<body>
<header>
<h1>{{.StoreName}}</h1>
<nav><a href="/">Home</a><a href="/catalog">Catalog</a><a href="/deals">Deals</a><a href="/about">About</a><a href="/support">Support</a><a href="/account">Account</a></nav>
</header>
<main>
{{if .ShowBanner}}<div class="banner">{{.BannerText}}</div>{{end}}
<section class="hero">
<h2>{{.HeroHeading}}</h2>
<p>{{.HeroTagline}}</p>
</section>
<section class="grid">
{{range .Products}}<article class="card"><h3>{{.Name}}</h3><p class="sku">{{.Sku}}</p><p class="price">{{.Price}}</p>{{if .OnSale}}<p class="sale">On sale</p>{{end}}<p class="blurb">{{.Blurb}}</p></article>{{end}}
</section>
{{if .ShowDebugPanel}}<pre class="debug">debug</pre>{{end}}
</main>
<footer>
<p>{{.FooterNote}}</p>
<p>{{.StoreName}} {{.Year}} {{.SupportEmail}}</p>
</footer>
</body>
</html>`

// ---- workload 5 — conditional-heavy (text/template) ------------------------------------------

// The pinned single-line <ul class="matrix"> body: four-way chain per row plus the two
// toggles.
const conditionalHeavySrc = `<ul class="matrix">{{range .Rows}}<li>{{if .IsBronze}}<span class="t0">bronze</span>{{else if .IsSilver}}<span class="t1">silver</span>{{else if .IsGold}}<span class="t2">gold</span>{{else}}<span class="t3">platinum</span>{{end}}<em>{{.Name}}</em>{{if .HasNote}}<small>{{.Note}}</small>{{end}}{{if .IsActive}}<b>active</b>{{end}}</li>{{end}}</ul>`

// ---- workload 6 — fragment-heavy (text/template) ---------------------------------------------

// The associated-template mechanism is the stdlib's partial-with-current-row construct:
// {{define "tile"}} + {{template "tile" .}} inside the range.
const fragmentHeavySrc = `{{define "tile"}}<section class="tile"><h3>{{.Name}}</h3><p class="v">{{.Value}}</p><span class="badge">{{.Badge}}</span></section>{{end}}<div class="panel">{{range .Items}}{{template "tile" .}}{{end}}</div>`

// ---- workload 7 — fortunes-encoded (html/template) -------------------------------------------

// The pinned one-line skeleton; html/template's contextual escaper fires on the
// text-context substitutions ({{.Message}}), with spellings reconciled by N5 (D4).
const fortunesEncodedSrc = `<!DOCTYPE html><html><head><title>Fortunes</title></head><body><table><tr><th>id</th><th>message</th></tr>{{range .Rows}}<tr><td>{{.Id}}</td><td>{{.Message}}</td></tr>{{end}}</table></body></html>`

// ---- workload 8 — encoded-loop (html/template) -----------------------------------------------

// {{.Tag}} sits in quoted-attribute context; the other two substitutions in text context.
const encodedLoopSrc = `<table>{{range .Items}}<tr><td data-tag="{{.Tag}}">{{.Name}}</td><td>{{.Comment}}</td></tr>{{end}}</table>`

// ---- parsed templates (once, at package init — the cached-template render path) --------------

var (
	composedTpl = func() *texttemplate.Template {
		t := texttemplate.New("composed-page")
		texttemplate.Must(t.Parse(composedLayoutSrc))
		return texttemplate.Must(t.Parse(composedHomeSrc))
	}()
	trivialSubstitutionTpl = texttemplate.Must(texttemplate.New("trivial-substitution").Parse(trivialSubstitutionSrc))
	largeLoopTpl           = texttemplate.Must(texttemplate.New("large-loop").Parse(largeLoopSrc))
	mixedPageTpl           = texttemplate.Must(texttemplate.New("mixed-page").Parse(mixedPageSrc))
	conditionalHeavyTpl    = texttemplate.Must(texttemplate.New("conditional-heavy").Parse(conditionalHeavySrc))
	fragmentHeavyTpl       = texttemplate.Must(texttemplate.New("fragment-heavy").Parse(fragmentHeavySrc))
	fortunesEncodedTpl     = htmltemplate.Must(htmltemplate.New("fortunes-encoded").Parse(fortunesEncodedSrc))
	encodedLoopTpl         = htmltemplate.Must(htmltemplate.New("encoded-loop").Parse(encodedLoopSrc))
)
