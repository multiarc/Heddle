// S1 probe execution (WI2). Each test is one probe of templ-feasibility.md §S1; the
// observed bytes are additionally recorded in benchmarks/go/README.md §S1 results.
package spike

import (
	"context"
	"strings"
	"testing"

	"github.com/a-h/templ"

	"heddle.dev/benchmarks/go/internal/corpus"
	"heddle.dev/benchmarks/go/internal/model"
)

func render(t *testing.T, c templ.Component) string {
	t.Helper()
	var b strings.Builder
	if err := c.Render(context.Background(), &b); err != nil {
		t.Fatalf("render failed: %v", err)
	}
	return b.String()
}

// ---- P1 — smallest-workload byte gate --------------------------------------------------------

func TestP1TrivialSubstitutionPassesTheByteGate(t *testing.T) {
	output := render(t, trivialSubstitution(model.Substitution))

	// The full controlled gate: N1–N4 pipeline, N3b strip on both sides, byte compare
	// against the committed corpus entry.
	if err := corpus.CheckControlled("trivial-substitution", "raw", "templ-spike",
		func() string { return output }); err != nil {
		t.Fatal(err)
	}

	// Evidence: is the normalized candidate byte-identical to the stored oracle even
	// BEFORE the N3b comparison strip?
	golden, err := corpus.LoadGolden("trivial-substitution")
	if err != nil {
		t.Fatal(err)
	}
	normalized := corpus.NormalizeRaw(output)
	if normalized == golden {
		t.Log("P1: normalized output is byte-identical to the oracle pre-N3b (no whitespace divergence at all)")
	} else {
		t.Logf("P1: whitespace-only divergence reconciled by N3/N3b (normalized %d bytes vs oracle %d)",
			len(normalized), len(golden))
	}
}

func TestP1SingleSpacesBetweenExpressionsSurviveVerbatim(t *testing.T) {
	// The mixed-page footer construct: `{ a } { b } { c }` separated by single literal
	// spaces. F3: collapsing maps a lone space to itself (fixed point) — the spaces must
	// survive verbatim in the rendered output.
	output := render(t, spacedExpressions("Mercantile", "2026", "support at mercantile.example"))
	const want = "<p>Mercantile 2026 support at mercantile.example</p>"
	if output != want {
		t.Fatalf("single spaces did not survive verbatim:\n  want %q\n  got  %q", want, output)
	}
}

// ---- P2 — escaper bytes ----------------------------------------------------------------------

// n5Table lists every spelling the contract's N5 table recognizes for the five
// markup-significant characters; any escaper output must be composed of literal safe text
// plus spellings from this set.
var n5Table = []string{
	"&amp;", "&#38;", "&#x26;",
	"&lt;", "&#60;", "&#x3C;",
	"&gt;", "&#62;", "&#x3E;",
	"&quot;", "&#34;", "&#x22;",
	"&#39;", "&#x27;", "&apos;",
}

// assertSpellingsInsideN5 walks every entity-shaped span ("&…;") of the escaped output and
// asserts it is one of the N5 table's recognized spellings.
func assertSpellingsInsideN5(t *testing.T, label, escaped string) {
	t.Helper()
	for i := 0; i < len(escaped); i++ {
		if escaped[i] != '&' {
			continue
		}
		end := strings.IndexByte(escaped[i:], ';')
		if end < 0 {
			t.Fatalf("%s: unterminated entity at %d in %q", label, i, escaped)
		}
		entity := escaped[i : i+end+1]
		found := false
		for _, spelling := range n5Table {
			if entity == spelling {
				found = true
				break
			}
		}
		if !found {
			t.Fatalf("%s: escaper spelling %q is OUTSIDE the N5 table — non-whitespace divergence (Q1.2 exclusion contingency)", label, entity)
		}
		i += end
	}
}

func TestP2TextPathEscaperBytes(t *testing.T) {
	// A fortunes-shaped row whose message covers all five specials plus Japanese.
	const message = `& < > " ' こんにちは`
	output := render(t, fortunesRow("1", message))
	t.Logf("P2 text path: %q", output)

	// Every emitted spelling must be inside the N5 table.
	assertSpellingsInsideN5(t, "text path", output)

	// Pin the exact observed spellings (templ.EscapeString → html.EscapeString: five
	// characters, decimal quotes — F5).
	const wantRow = `<tr><td>1</td><td>&amp; &lt; &gt; &#34; &#39; こんにちは</td></tr>`
	if output != wantRow {
		t.Fatalf("text-path bytes changed:\n  want %q\n  got  %q", wantRow, output)
	}

	// After N5 the output must carry the canonical spellings, byte-for-byte.
	canonical := corpus.N5Canonicalize(output)
	const wantCanonical = `<tr><td>1</td><td>&amp; &lt; &gt; &quot; &#39; こんにちは</td></tr>`
	if canonical != wantCanonical {
		t.Fatalf("N5 canonicalization mismatch:\n  want %q\n  got  %q", wantCanonical, canonical)
	}

	// The Japanese string is byte-intact (the stdlib escaper touches only the five ASCII).
	if !strings.Contains(output, "こんにちは") {
		t.Fatal("Japanese string must pass through byte-intact")
	}
}

func TestP2AttributePathEscaperBytes(t *testing.T) {
	// An encoded-loop-shaped row: data-tag={ tag } attribute plus two text expressions,
	// using the pinned row-0 values (tag covers & and '; name covers < > and ").
	row := model.EncodedLoop.Items[0]
	output := render(t, encodedRow(row.Tag, row.Name, row.Comment))
	t.Logf("P2 attribute path: %q", output)

	assertSpellingsInsideN5(t, "attribute path", output)

	// Pin the exact observed bytes: templ auto-quotes the attribute and escapes both paths
	// with the same five-character decimal-quote table.
	const wantRow = `<tr><td data-tag="tag-0&amp;&#39;0&#39;">item &lt;0&gt; &amp; &#34;co&#34;</td><td>&#39;q&#39; &amp; &lt;angle&gt; &#34;d&#34; こんにちは 0</td></tr>`
	if output != wantRow {
		t.Fatalf("attribute-path bytes changed:\n  want %q\n  got  %q", wantRow, output)
	}

	// After N5, the bytes equal the oracle's canonical row 0 exactly.
	const wantCanonical = `<tr><td data-tag="tag-0&amp;&#39;0&#39;">item &lt;0&gt; &amp; &quot;co&quot;</td><td>&#39;q&#39; &amp; &lt;angle&gt; &quot;d&quot; こんにちは 0</td></tr>`
	if canonical := corpus.N5Canonicalize(output); canonical != wantCanonical {
		t.Fatalf("N5 canonicalization mismatch:\n  want %q\n  got  %q", wantCanonical, canonical)
	}
}

func TestP2EscapeFreeDataRoundTripsUnescaped(t *testing.T) {
	// '+'-free (and escapable-free) data round-trips byte-identical — the raw suites'
	// no-op-escaper premise (authoring rule 4).
	output := render(t, fortunesRow("7", "Computers make very fast, very accurate mistakes."))
	const want = "<tr><td>7</td><td>Computers make very fast, very accurate mistakes.</td></tr>"
	if output != want {
		t.Fatalf("escape-free data must round-trip unescaped:\n  want %q\n  got  %q", want, output)
	}
}

// ---- P3 — style passthrough ------------------------------------------------------------------

// styleCSS is the exact mixed-page <style> content (workloads.md workload 4), the authored
// bytes the rendered output must reproduce.
const styleCSS = `body{font:16px/1.5 system-ui;margin:0;color:#222}header{background:#1a2b3c;color:#fff;padding:12px 24px}nav a{color:#9cf;margin-right:12px;text-decoration:none}main{max-width:960px;margin:0 auto;padding:24px}.hero{background:#f4f6f8;padding:32px;border-radius:8px}.banner{background:#fff4d6;padding:8px 16px;border-radius:4px}.grid{display:flex;flex-wrap:wrap;gap:16px}.card{border:1px solid #ddd;border-radius:6px;padding:16px;width:280px}.card h3{margin:0 0 8px}.price{font-weight:700}.sale{color:#b00020;font-weight:700}footer{border-top:1px solid #ddd;margin-top:32px;padding:16px 24px;color:#666}`

func TestP3StyleContentPassesThroughByteIdentical(t *testing.T) {
	output := render(t, styleBlock())
	t.Logf("P3: %d bytes rendered", len(output))

	const prefix, suffix = "<style>", "</style>"
	if !strings.HasPrefix(output, prefix) || !strings.HasSuffix(output, suffix) {
		t.Fatalf("style element shape changed: %q", output)
	}
	content := output[len(prefix) : len(output)-len(suffix)]
	if content == styleCSS {
		return // F4 holds: rendered without any changes — no fallback needed
	}
	// Only a confirmed NON-whitespace change routes to the @templ.Raw fallback
	// (port-mapping rule 6); a whitespace-only difference passes via N3b.
	if corpus.N3bStrip(content) == corpus.N3bStrip(styleCSS) {
		t.Logf("P3: whitespace-only difference in style content (reconciled by N3b): %q", content)
		return
	}
	t.Fatalf("P3: NON-whitespace change to style content — the @templ.Raw fallback branch applies:\n  want %q\n  got  %q", styleCSS, content)
}

// ---- P4 — inter-statement whitespace (evidence only) -----------------------------------------

func TestP4ComposedPageBoundaryBytes(t *testing.T) {
	// The composed-page CSS-comment neighborhood: comp-assets-styles →
	// comp-custom-styles → comp-head-scripts, issued as consecutive @templ.Raw calls on
	// separate lines — the fragments verbatim from the pinned model data.
	link := model.Composed.Comp["assets_styles"]
	css := model.Composed.Comp["custom_styles"]
	script := model.Composed.Comp["head_scripts"]
	output := render(t, boundary(link, css, script))

	// Evidence: the exact bytes templ emits between consecutive statement nodes.
	zeroSeparator := link + css + script
	switch {
	case output == zeroSeparator:
		t.Log("P4: templ emits ZERO separator bytes between consecutive @templ.Raw nodes — boundary already byte-exact vs the oracle")
	case corpus.N3bStrip(output) == corpus.N3bStrip(zeroSeparator):
		t.Logf("P4: whitespace-only separator between @templ.Raw nodes (passes via N3b). Observed output: %q", output)
	default:
		t.Fatalf("P4: NON-whitespace divergence at the inter-statement boundary:\n  want (stripped) %q\n  got  %q",
			zeroSeparator, output)
	}

	// The oracle's non-tag-flanked boundary must survive the comparison projection: the
	// stripped candidate must contain the oracle's `/>/*CSSCommentTest*/<script` bytes.
	if !strings.Contains(corpus.N3bStrip(output), `/>/*CSSCommentTest*/<script`) {
		t.Fatalf("P4: CSS-comment boundary bytes missing from the stripped output: %q", corpus.N3bStrip(output))
	}
}
