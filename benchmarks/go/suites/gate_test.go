// Package suites hosts the phase's gates and benchmarks. TestMain runs every registered
// gate — corpus integrity, the untrusted-data alphabet assert, every controlled cell's byte
// gate, and every idiomatic cell's verification — before m.Run(), so no benchmark in the
// package can time until every gate passed in the same invocation (parity-contract-v2
// §Controlled-track gate 2). The same checks are exposed as ordinary Test functions so plain
// `go test ./suites` is the phase's parity command (the Go analogue of the .NET `-- parity`
// verb).
//
// Spec: docs/spec/cross-stack-benchmarks/phase-6-go/harness-and-measurement.md
package suites

import (
	"fmt"
	"os"
	"testing"

	"heddle.dev/benchmarks/go/internal/corpus"
	"heddle.dev/benchmarks/go/internal/model"
	"heddle.dev/benchmarks/go/internal/stdlibtpl"
	"heddle.dev/benchmarks/go/internal/templeng"
	templidiomatic "heddle.dev/benchmarks/go/internal/templeng/idiomatic"
)

// cell is one gated workload × engine × track combination.
type cell struct {
	Track    string // "controlled" | "idiomatic"
	Engine   string // "stdlib-text" | "stdlib-html" | "templ" (+ "quicktemplate" if D10 fires)
	Workload string // Phase 1 workload id
	Suite    string // "raw" | "encoded"
	Render   func() string
}

// cells is the gate registry. Engines register their cells here as they land:
// WI3 adds the eight stdlib controlled cells, WI4 the eight templ controlled cells,
// WI5 the sixteen idiomatic cells.
var cells = []cell{
	// WI3 — stdlib controlled: text/template on the six raw workloads, html/template on
	// the two encoded (README D1 / Q6.1).
	{"controlled", "stdlib-text", "composed-page", "raw", stdlibtpl.RenderComposedPage},
	{"controlled", "stdlib-text", "trivial-substitution", "raw", stdlibtpl.RenderTrivialSubstitution},
	{"controlled", "stdlib-text", "large-loop", "raw", stdlibtpl.RenderLargeLoop},
	{"controlled", "stdlib-text", "mixed-page", "raw", stdlibtpl.RenderMixedPage},
	{"controlled", "stdlib-text", "conditional-heavy", "raw", stdlibtpl.RenderConditionalHeavy},
	{"controlled", "stdlib-text", "fragment-heavy", "raw", stdlibtpl.RenderFragmentHeavy},
	{"controlled", "stdlib-html", "fortunes-encoded", "encoded", stdlibtpl.RenderFortunesEncoded},
	{"controlled", "stdlib-html", "encoded-loop", "encoded", stdlibtpl.RenderEncodedLoop},
	// WI4 — templ controlled: all eight workloads (templ.Raw for the composed-page
	// HTML-fragment data; the default escaping path on the encoded pair).
	{"controlled", "templ", "composed-page", "raw", templeng.RenderComposedPage},
	{"controlled", "templ", "trivial-substitution", "raw", templeng.RenderTrivialSubstitution},
	{"controlled", "templ", "large-loop", "raw", templeng.RenderLargeLoop},
	{"controlled", "templ", "mixed-page", "raw", templeng.RenderMixedPage},
	{"controlled", "templ", "conditional-heavy", "raw", templeng.RenderConditionalHeavy},
	{"controlled", "templ", "fragment-heavy", "raw", templeng.RenderFragmentHeavy},
	{"controlled", "templ", "fortunes-encoded", "encoded", templeng.RenderFortunesEncoded},
	{"controlled", "templ", "encoded-loop", "encoded", templeng.RenderEncodedLoop},
	// WI5 — stdlib idiomatic: the same Q6.1 surface split as controlled, authored per the
	// official docs (port-mapping.md §Idiomatic track), gated by the Phase 1 verifier.
	{"idiomatic", "stdlib-text", "composed-page", "raw", stdlibtpl.RenderIdiomaticComposedPage},
	{"idiomatic", "stdlib-text", "trivial-substitution", "raw", stdlibtpl.RenderIdiomaticTrivialSubstitution},
	{"idiomatic", "stdlib-text", "large-loop", "raw", stdlibtpl.RenderIdiomaticLargeLoop},
	{"idiomatic", "stdlib-text", "mixed-page", "raw", stdlibtpl.RenderIdiomaticMixedPage},
	{"idiomatic", "stdlib-text", "conditional-heavy", "raw", stdlibtpl.RenderIdiomaticConditionalHeavy},
	{"idiomatic", "stdlib-text", "fragment-heavy", "raw", stdlibtpl.RenderIdiomaticFragmentHeavy},
	{"idiomatic", "stdlib-html", "fortunes-encoded", "encoded", stdlibtpl.RenderIdiomaticFortunesEncoded},
	{"idiomatic", "stdlib-html", "encoded-loop", "encoded", stdlibtpl.RenderIdiomaticEncodedLoop},
	// WI5 — templ idiomatic: all eight workloads, naturally formatted with idiomatic
	// component decomposition (internal/templeng/idiomatic).
	{"idiomatic", "templ", "composed-page", "raw", templidiomatic.RenderComposedPage},
	{"idiomatic", "templ", "trivial-substitution", "raw", templidiomatic.RenderTrivialSubstitution},
	{"idiomatic", "templ", "large-loop", "raw", templidiomatic.RenderLargeLoop},
	{"idiomatic", "templ", "mixed-page", "raw", templidiomatic.RenderMixedPage},
	{"idiomatic", "templ", "conditional-heavy", "raw", templidiomatic.RenderConditionalHeavy},
	{"idiomatic", "templ", "fragment-heavy", "raw", templidiomatic.RenderFragmentHeavy},
	{"idiomatic", "templ", "fortunes-encoded", "encoded", templidiomatic.RenderFortunesEncoded},
	{"idiomatic", "templ", "encoded-loop", "encoded", templidiomatic.RenderEncodedLoop},
}

// checkCell dispatches one cell to its track's gate.
func checkCell(c cell) error {
	switch c.Track {
	case "controlled":
		return corpus.CheckControlled(c.Workload, c.Suite, c.Engine, c.Render)
	case "idiomatic":
		def, err := corpus.LoadVerify(c.Workload)
		if err != nil {
			return err
		}
		if failures := corpus.Verify(def, c.Render()); len(failures) != 0 {
			msg := fmt.Sprintf("verify: %s/%s", c.Workload, c.Engine)
			for _, f := range failures {
				msg += "\n  " + f.String()
			}
			return fmt.Errorf("%s", msg)
		}
		return nil
	default:
		return fmt.Errorf("gate: %s/%s: unknown track %q", c.Workload, c.Engine, c.Track)
	}
}

// runAllGates runs corpus integrity, the alphabet assert, and every registered cell's gate.
func runAllGates() error {
	if err := corpus.CheckManifest(); err != nil {
		return err
	}
	for _, v := range model.EncodedValues() {
		if err := corpus.CheckAlphabet(v.Name, v.Value); err != nil {
			return err
		}
	}
	for _, c := range cells {
		if err := checkCell(c); err != nil {
			return err
		}
	}
	return nil
}

// TestMain gates before any benchmark can time (contract gate rule 2: before any timing, in
// the same invocation). Any failure prints the failure surface and exits 1 — no numbers.
func TestMain(m *testing.M) {
	if err := runAllGates(); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	os.Exit(m.Run())
}

// ---- the same gates as ordinary Test functions -----------------------------------------------

func TestCorpusIntegrity(t *testing.T) {
	if err := corpus.CheckManifest(); err != nil {
		t.Fatal(err)
	}
}

func TestEncodedModelAlphabet(t *testing.T) {
	for _, v := range model.EncodedValues() {
		if err := corpus.CheckAlphabet(v.Name, v.Value); err != nil {
			t.Fatal(err)
		}
	}
}

func TestControlledGates(t *testing.T) {
	for _, c := range cells {
		if c.Track != "controlled" {
			continue
		}
		t.Run(c.Workload+"/"+c.Engine, func(t *testing.T) {
			if err := checkCell(c); err != nil {
				t.Fatal(err)
			}
		})
	}
}

func TestIdiomaticGates(t *testing.T) {
	for _, c := range cells {
		if c.Track != "idiomatic" {
			continue
		}
		t.Run(c.Workload+"/"+c.Engine, func(t *testing.T) {
			if err := checkCell(c); err != nil {
				t.Fatal(err)
			}
		})
	}
}
