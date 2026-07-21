// BenchmarkRender — one sub-benchmark per gated cell, named for benchstat grouping
// (harness-and-measurement.md §Benchmark shape, D8):
//
//	BenchmarkRender/<track>/<workload>/<engine>
//
// Body shape, normative: b.ReportAllocs() in every benchmark (allocation figures are
// structural, not flag-dependent); for b.Loop() with a package-level sink (keeps the
// result alive, excludes setup from timing). Template parse / templ component setup /
// model materialization all happen in package init or TestMain — never here — and every
// gate has already passed in TestMain before any benchmark in this package can time.
package suites

import "testing"

// sink keeps every timed render's result alive (belt-and-braces against dead-code
// elimination, alongside b.Loop's own guarantee).
var sink string

// workloadOrder fixes the benchmark ordering to the Phase 1 workload numbering.
var workloadOrder = []string{
	"composed-page",
	"trivial-substitution",
	"large-loop",
	"mixed-page",
	"conditional-heavy",
	"fragment-heavy",
	"fortunes-encoded",
	"encoded-loop",
}

func BenchmarkRender(b *testing.B) {
	for _, track := range []string{"controlled", "idiomatic"} {
		b.Run(track, func(b *testing.B) {
			for _, workload := range workloadOrder {
				b.Run(workload, func(b *testing.B) {
					for _, c := range cells {
						if c.Track != track || c.Workload != workload {
							continue
						}
						render := c.Render
						b.Run(c.Engine, func(b *testing.B) {
							b.ReportAllocs()
							for b.Loop() {
								sink = render()
							}
						})
					}
				})
			}
		})
	}
}
