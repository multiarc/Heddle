// Package templeng hosts the templ controlled ports (one .templ file per workload,
// generated *_templ.go committed — README D3). Render path per port-mapping.md rule 8:
// component.Render(ctx, buf) into a reused pre-grown bytes.Buffer with a background
// context.Context created once; construction of the component value (a cheap closure) is
// inside the render — it is templ's per-render entry point, exactly how a caller invokes a
// cached templ template (there is no separate parse to exclude).
package templeng

import (
	"bytes"
	"context"

	"github.com/a-h/templ"

	"heddle.dev/benchmarks/go/internal/model"
)

// ctx is the background context, built once (rule 8).
var ctx = context.Background()

// newBuf returns a buffer pre-grown to the workload's output size, so growth never lands
// in a timed region (encoded-loop's ~1.1 MB escaped output is the sizing case).
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

// render resets the cell's buffer, renders the component, and returns the output.
// A render error is a harness defect, never a gate outcome — panic loudly.
func render(c templ.Component, buf *bytes.Buffer) string {
	buf.Reset()
	if err := c.Render(ctx, buf); err != nil {
		panic("templeng: render failed: " + err.Error())
	}
	return buf.String()
}

// ---- the eight controlled cells --------------------------------------------------------------

// RenderComposedPage renders workload 1 via templ.
func RenderComposedPage() string { return render(composedPage(model.Composed), composedBuf) }

// RenderTrivialSubstitution renders workload 2 via templ.
func RenderTrivialSubstitution() string {
	return render(trivialSubstitution(model.Substitution), trivialBuf)
}

// RenderLargeLoop renders workload 3 via templ.
func RenderLargeLoop() string { return render(largeLoop(model.LargeLoop), largeLoopBuf) }

// RenderMixedPage renders workload 4 via templ.
func RenderMixedPage() string { return render(mixedPage(model.Mixed), mixedBuf) }

// RenderConditionalHeavy renders workload 5 via templ.
func RenderConditionalHeavy() string {
	return render(conditionalHeavy(model.Conditional), conditionalBuf)
}

// RenderFragmentHeavy renders workload 6 via templ.
func RenderFragmentHeavy() string { return render(fragmentHeavy(model.Fragment), fragmentBuf) }

// RenderFortunesEncoded renders workload 7 via templ (its default escaping path).
func RenderFortunesEncoded() string { return render(fortunesEncoded(model.Fortunes), fortunesBuf) }

// RenderEncodedLoop renders workload 8 via templ (its default escaping path).
func RenderEncodedLoop() string { return render(encodedLoop(model.EncodedLoop), encodedLoopBuf) }
