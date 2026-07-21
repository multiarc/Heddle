// Cold-parse entry points for the per-ecosystem sidebar (harness-and-measurement.md
// §Cold parse/compile sidebar, Q1.3): parse the full controlled workload template set from
// source, per surface, the way a process would on first load. Scope caveat: html/template
// runs its contextual escaping analysis lazily at the first Execute, not at Parse, so both
// rows are labeled "cold parse only" (not parse + escape-compile). templ has no runtime
// parse step at all — its sidebar cells print
// "AOT — no runtime parse (compiled by go generate)" in the report.
package stdlibtpl

import (
	htmltemplate "html/template"
	texttemplate "text/template"
)

// ColdParseText parses the six raw-suite text/template controlled sources from scratch
// (composed-page's layout + home land in one associated-template set) and returns the
// parsed set to keep it alive in the caller's sink.
func ColdParseText() any {
	composed := texttemplate.New("composed-page")
	texttemplate.Must(composed.Parse(composedLayoutSrc))
	texttemplate.Must(composed.Parse(composedHomeSrc))
	return []*texttemplate.Template{
		composed,
		texttemplate.Must(texttemplate.New("trivial-substitution").Parse(trivialSubstitutionSrc)),
		texttemplate.Must(texttemplate.New("large-loop").Parse(largeLoopSrc)),
		texttemplate.Must(texttemplate.New("mixed-page").Parse(mixedPageSrc)),
		texttemplate.Must(texttemplate.New("conditional-heavy").Parse(conditionalHeavySrc)),
		texttemplate.Must(texttemplate.New("fragment-heavy").Parse(fragmentHeavySrc)),
	}
}

// ColdParseHTML parses the two encoded-suite html/template controlled sources from
// scratch — parse only; the escaper's context analysis is deferred to first Execute and is
// deliberately out of this cell's scope (see the package comment).
func ColdParseHTML() any {
	return []*htmltemplate.Template{
		htmltemplate.Must(htmltemplate.New("fortunes-encoded").Parse(fortunesEncodedSrc)),
		htmltemplate.Must(htmltemplate.New("encoded-loop").Parse(encodedLoopSrc)),
	}
}
