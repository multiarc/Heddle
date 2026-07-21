// Idiomatic-track ports for the stdlib surfaces: text/template on the six raw workloads,
// html/template on the two encoded workloads (README D1 / Q6.1 — same surface split as the
// controlled track). Authoring standard (Q1.7/D16): naturally formatted multi-line
// templates with indentation, {{- -}} trim markers where the Go docs use them, and template
// composition via the {{define}}/{{template}} idiom — the way the official docs teach the
// engine, not the byte-exact controlled shape. These sources may diverge freely in
// whitespace and structure; they are gated by the Phase 1 idiomatic verifier
// (<id>.verify.json semantics), not the byte gate.
//
// Doc citations (Q1.7 — official documentation patterns followed here):
//   - https://pkg.go.dev/text/template   (Actions, Text and spaces / trim markers,
//     Nested template definitions, Examples)
//   - https://pkg.go.dev/html/template   (same template API; contextual auto-escaping)
//
// Spec: docs/spec/cross-stack-benchmarks/phase-6-go/port-mapping.md
// §Idiomatic track — both engines, all eight workloads.
package stdlibtpl

import (
	htmltemplate "html/template"
	texttemplate "text/template"

	"heddle.dev/benchmarks/go/internal/model"
)

// ---- workload 1 — composed-page (text/template) ----------------------------------------------

// Layout composition via nested template definitions ({{define "layout"}} +
// {{template "layout" .}}), one fragment per line; the HTML fragments are runtime data and
// text/template passes them through unescaped.
const idiomaticComposedSrc = `{{define "layout"}}
{{index .Section "meta"}}
{{index .Section "social"}}
{{index .Comp "assets_styles"}}
{{index .Comp "custom_styles"}}
{{index .Comp "head_scripts"}}
{{index .Comp "body_scripts"}}
{{range .AreaNames -}}
{{index $.Areas .}}
{{end -}}
{{index .Comp "assets_scripts"}}
{{index .Section "page_scripts"}}
{{index .Section "endpage_scripts"}}
{{index .Comp "body_end_scripts"}}
{{end}}
{{- template "layout" .}}`

// ---- workload 2 — trivial-substitution (text/template) ---------------------------------------

const idiomaticTrivialSubstitutionSrc = `<article>
  <h1>{{.Title}}</h1>
  <p class="sku">{{.Sku}}</p>
  <p class="price">{{.Price}}</p>
  <p class="brand">{{.Brand}}</p>
  <p class="cat">{{.Category}}</p>
  <p class="avail">{{.Availability}}</p>
  <a class="link" href="{{.Url}}"><img src="{{.ImageUrl}}"></a>
  <p class="sum">{{.Summary}}</p>
  <p class="rating">{{.Rating}}</p>
</article>
`

// ---- workload 3 — large-loop (text/template) -------------------------------------------------

// Trim markers around the range body, the shape the text/template docs' "Text and spaces"
// section teaches for loop output.
const idiomaticLargeLoopSrc = `{{range .Items -}}
<tr>
  <td>{{.Name}}</td>
  <td>{{.Value}}</td>
</tr>
{{end -}}
`

// ---- workload 4 — mixed-page (text/template) -------------------------------------------------

const idiomaticMixedPageSrc = `<!DOCTYPE html>
<html>
  <head>
    <meta charset="utf-8">
    <title>{{.PageTitle}}</title>
    <style>body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}</style>
  </head>
  <body>
    <header>
      <h1>{{.StoreName}}</h1>
      <nav>
        <a href="/">Home</a>
        <a href="/catalog">Catalog</a>
        <a href="/deals">Deals</a>
        <a href="/about">About</a>
        <a href="/support">Support</a>
        <a href="/account">Account</a>
      </nav>
    </header>
    <main>
      {{if .ShowBanner}}<div class="banner">{{.BannerText}}</div>{{end}}
      <section class="hero">
        <h2>{{.HeroHeading}}</h2>
        <p>{{.HeroTagline}}</p>
      </section>
      <section class="grid">
        {{range .Products -}}
        <article class="card">
          <h3>{{.Name}}</h3>
          <p class="sku">{{.Sku}}</p>
          <p class="price">{{.Price}}</p>
          {{if .OnSale}}<p class="sale">On sale</p>{{end}}
          <p class="blurb">{{.Blurb}}</p>
        </article>
        {{end -}}
      </section>
      {{if .ShowDebugPanel}}<pre class="debug">debug</pre>{{end}}
    </main>
    <footer>
      <p>{{.FooterNote}}</p>
      <p>{{.StoreName}} {{.Year}} {{.SupportEmail}}</p>
    </footer>
  </body>
</html>
`

// ---- workload 5 — conditional-heavy (text/template) ------------------------------------------

const idiomaticConditionalHeavySrc = `<ul class="matrix">
  {{range .Rows -}}
  <li>
    {{if .IsBronze -}}
    <span class="t0">bronze</span>
    {{- else if .IsSilver -}}
    <span class="t1">silver</span>
    {{- else if .IsGold -}}
    <span class="t2">gold</span>
    {{- else -}}
    <span class="t3">platinum</span>
    {{- end}}
    <em>{{.Name}}</em>
    {{if .HasNote}}<small>{{.Note}}</small>{{end}}
    {{if .IsActive}}<b>active</b>{{end}}
  </li>
  {{end -}}
</ul>
`

// ---- workload 6 — fragment-heavy (text/template) ---------------------------------------------

// The partial-with-current-row construct exactly as the docs' nested-template-definition
// examples show it: a named {{define}} invoked with {{template "tile" .}} per row.
const idiomaticFragmentHeavySrc = `{{define "tile"}}
<section class="tile">
  <h3>{{.Name}}</h3>
  <p class="v">{{.Value}}</p>
  <span class="badge">{{.Badge}}</span>
</section>
{{end}}
{{- define "panel"}}
<div class="panel">
  {{range .Items}}{{template "tile" .}}{{end}}
</div>
{{end}}
{{- template "panel" .}}`

// ---- workload 7 — fortunes-encoded (html/template) -------------------------------------------

// html/template's contextual auto-escaper fires on {{.Id}}/{{.Message}}; the authoring is
// otherwise identical to how the html/template docs teach a data-driven page.
const idiomaticFortunesEncodedSrc = `<!DOCTYPE html>
<html>
  <head>
    <title>Fortunes</title>
  </head>
  <body>
    <table>
      <tr><th>id</th><th>message</th></tr>
      {{range .Rows -}}
      <tr>
        <td>{{.Id}}</td>
        <td>{{.Message}}</td>
      </tr>
      {{end -}}
    </table>
  </body>
</html>
`

// ---- workload 8 — encoded-loop (html/template) -----------------------------------------------

// {{.Tag}} in quoted-attribute context, the other two substitutions in text context — the
// escaper picks the context per the html/template package docs.
const idiomaticEncodedLoopSrc = `<table>
  {{range .Items -}}
  <tr>
    <td data-tag="{{.Tag}}">{{.Name}}</td>
    <td>{{.Comment}}</td>
  </tr>
  {{end -}}
</table>
`

// ---- parsed templates (once, at package init — the cached-template render path) --------------

var (
	idiomaticComposedTpl            = texttemplate.Must(texttemplate.New("composed-page").Parse(idiomaticComposedSrc))
	idiomaticTrivialSubstitutionTpl = texttemplate.Must(texttemplate.New("trivial-substitution").Parse(idiomaticTrivialSubstitutionSrc))
	idiomaticLargeLoopTpl           = texttemplate.Must(texttemplate.New("large-loop").Parse(idiomaticLargeLoopSrc))
	idiomaticMixedPageTpl           = texttemplate.Must(texttemplate.New("mixed-page").Parse(idiomaticMixedPageSrc))
	idiomaticConditionalHeavyTpl    = texttemplate.Must(texttemplate.New("conditional-heavy").Parse(idiomaticConditionalHeavySrc))
	idiomaticFragmentHeavyTpl       = texttemplate.Must(texttemplate.New("fragment-heavy").Parse(idiomaticFragmentHeavySrc))
	idiomaticFortunesEncodedTpl     = htmltemplate.Must(htmltemplate.New("fortunes-encoded").Parse(idiomaticFortunesEncodedSrc))
	idiomaticEncodedLoopTpl         = htmltemplate.Must(htmltemplate.New("encoded-loop").Parse(idiomaticEncodedLoopSrc))
)

// ---- per-cell reused buffers (render.go's newBuf; same sizing cases as controlled) -----------

var (
	idComposedBuf    = newBuf(64 << 10)
	idTrivialBuf     = newBuf(1 << 10)
	idLargeLoopBuf   = newBuf(256 << 10)
	idMixedBuf       = newBuf(32 << 10)
	idConditionalBuf = newBuf(32 << 10)
	idFragmentBuf    = newBuf(16 << 10)
	idFortunesBuf    = newBuf(4 << 10)
	idEncodedLoopBuf = newBuf(2 << 20)
)

// ---- the eight idiomatic cells (raw = stdlib-text, encoded = stdlib-html) --------------------

// RenderIdiomaticComposedPage renders workload 1 idiomatically via text/template.
func RenderIdiomaticComposedPage() string {
	return render(idiomaticComposedTpl, idComposedBuf, model.Composed)
}

// RenderIdiomaticTrivialSubstitution renders workload 2 idiomatically via text/template.
func RenderIdiomaticTrivialSubstitution() string {
	return render(idiomaticTrivialSubstitutionTpl, idTrivialBuf, model.Substitution)
}

// RenderIdiomaticLargeLoop renders workload 3 idiomatically via text/template.
func RenderIdiomaticLargeLoop() string {
	return render(idiomaticLargeLoopTpl, idLargeLoopBuf, model.LargeLoop)
}

// RenderIdiomaticMixedPage renders workload 4 idiomatically via text/template.
func RenderIdiomaticMixedPage() string {
	return render(idiomaticMixedPageTpl, idMixedBuf, model.Mixed)
}

// RenderIdiomaticConditionalHeavy renders workload 5 idiomatically via text/template.
func RenderIdiomaticConditionalHeavy() string {
	return render(idiomaticConditionalHeavyTpl, idConditionalBuf, model.Conditional)
}

// RenderIdiomaticFragmentHeavy renders workload 6 idiomatically via text/template.
func RenderIdiomaticFragmentHeavy() string {
	return render(idiomaticFragmentHeavyTpl, idFragmentBuf, model.Fragment)
}

// RenderIdiomaticFortunesEncoded renders workload 7 idiomatically via html/template.
func RenderIdiomaticFortunesEncoded() string {
	return render(idiomaticFortunesEncodedTpl, idFortunesBuf, model.Fortunes)
}

// RenderIdiomaticEncodedLoop renders workload 8 idiomatically via html/template.
func RenderIdiomaticEncodedLoop() string {
	return render(idiomaticEncodedLoopTpl, idEncodedLoopBuf, model.EncodedLoop)
}
