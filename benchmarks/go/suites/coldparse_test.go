// BenchmarkColdParse — the per-ecosystem, non-comparable cold-cost sidebar
// (harness-and-measurement.md §Cold parse/compile sidebar, Q1.3). Two cells only:
//
//	BenchmarkColdParse/stdlib-text — parse the six raw-suite template sources per iteration
//	BenchmarkColdParse/stdlib-html — parse the two encoded-suite template sources per iteration
//
// Both rows are labeled "cold parse only" in the report: html/template's contextual
// escaping analysis runs lazily at the first Execute, not at Parse, so a Parse-only loop
// deliberately does not capture escape-compilation. templ has no runtime parse step — its
// report cells print "AOT — no runtime parse (compiled by go generate)" instead of a
// number; that asymmetry is the sidebar's only load-bearing point. These figures never
// appear as a cross-language column.
package suites

import (
	"testing"

	"heddle.dev/benchmarks/go/internal/stdlibtpl"
)

// coldSink keeps each iteration's parsed template set alive.
var coldSink any

func BenchmarkColdParse(b *testing.B) {
	b.Run("stdlib-text", func(b *testing.B) {
		b.ReportAllocs()
		for b.Loop() {
			coldSink = stdlibtpl.ColdParseText()
		}
	})
	b.Run("stdlib-html", func(b *testing.B) {
		b.ReportAllocs()
		for b.Loop() {
			coldSink = stdlibtpl.ColdParseHTML()
		}
	})
}
