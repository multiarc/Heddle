// Idiomatic-track render path for the stdlib surfaces — the same reused pre-grown
// per-cell bytes.Buffer shape as the controlled track's render.go, stated per track so
// each package is self-contained (the templ engine splits its tracks the same way).
package idiomatic

import (
	"bytes"
	"io"
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

// render resets the cell's buffer, executes the cached template, and returns the output.
// A template execution error is a harness defect, never a gate outcome — panic loudly.
func render(t executer, buf *bytes.Buffer, data any) string {
	buf.Reset()
	if err := t.Execute(buf, data); err != nil {
		panic("stdlibtpl/idiomatic: render failed: " + err.Error())
	}
	return buf.String()
}
