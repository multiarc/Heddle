// Controlled-track render path for the stdlib surfaces: every render executes the parsed
// template into a reused per-cell bytes.Buffer, Reset() at the top of each call and
// pre-grown once to the workload's output size so neither growth policy nor the reset is a
// per-engine variable (harness-and-measurement.md §Benchmark shape), returning buf.String().
package stdlibtpl

import (
	"bytes"
	"io"

	"heddle.dev/benchmarks/go/internal/model"
)

// executer is the shared surface of *text/template.Template and *html/template.Template.
type executer interface {
	Execute(wr io.Writer, data any) error
}

// newBuf returns a buffer pre-grown to the workload's output size (encoded-loop's ~1.1 MB
// escaped output is the sizing case), so growth never lands in a timed region.
func newBuf(capacity int) *bytes.Buffer {
	buf := new(bytes.Buffer)
	buf.Grow(capacity)
	return buf
}

var (
	composedBuf    = newBuf(64 << 10)
	trivialBuf     = newBuf(1 << 10)
	largeLoopBuf   = newBuf(256 << 10)
	mixedBuf       = newBuf(32 << 10)
	conditionalBuf = newBuf(32 << 10)
	fragmentBuf    = newBuf(16 << 10)
	fortunesBuf    = newBuf(4 << 10)
	encodedLoopBuf = newBuf(2 << 20)
)

// render resets the cell's buffer, executes the cached template, and returns the output.
// A template execution error is a harness defect, never a gate outcome — panic loudly.
func render(t executer, buf *bytes.Buffer, data any) string {
	buf.Reset()
	if err := t.Execute(buf, data); err != nil {
		panic("stdlibtpl: render failed: " + err.Error())
	}
	return buf.String()
}

// ---- the eight controlled cells (raw = stdlib-text, encoded = stdlib-html) -------------------

// RenderComposedPage renders workload 1 via text/template (stdlib-text).
func RenderComposedPage() string {
	return render(composedTpl, composedBuf, model.Composed)
}

// RenderTrivialSubstitution renders workload 2 via text/template (stdlib-text).
func RenderTrivialSubstitution() string {
	return render(trivialSubstitutionTpl, trivialBuf, model.Substitution)
}

// RenderLargeLoop renders workload 3 via text/template (stdlib-text).
func RenderLargeLoop() string {
	return render(largeLoopTpl, largeLoopBuf, model.LargeLoop)
}

// RenderMixedPage renders workload 4 via text/template (stdlib-text).
func RenderMixedPage() string {
	return render(mixedPageTpl, mixedBuf, model.Mixed)
}

// RenderConditionalHeavy renders workload 5 via text/template (stdlib-text).
func RenderConditionalHeavy() string {
	return render(conditionalHeavyTpl, conditionalBuf, model.Conditional)
}

// RenderFragmentHeavy renders workload 6 via text/template (stdlib-text).
func RenderFragmentHeavy() string {
	return render(fragmentHeavyTpl, fragmentBuf, model.Fragment)
}

// RenderFortunesEncoded renders workload 7 via html/template (stdlib-html).
func RenderFortunesEncoded() string {
	return render(fortunesEncodedTpl, fortunesBuf, model.Fortunes)
}

// RenderEncodedLoop renders workload 8 via html/template (stdlib-html).
func RenderEncodedLoop() string {
	return render(encodedLoopTpl, encodedLoopBuf, model.EncodedLoop)
}
