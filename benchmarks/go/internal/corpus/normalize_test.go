package corpus

import (
	"strings"
	"testing"
)

// Spec: README Testing plan — N2–N4 equivalence vectors (contract six-character whitespace
// set edge cases), N5 table + no-rescan case, N1 BOM survival.

func TestN2LineEndingVectors(t *testing.T) {
	vectors := map[string]string{
		"a\r\nb":     "a\nb",
		"a\rb":       "a\nb",
		"a\r\n\rb\r": "a\n\nb\n",
		"a\nb":       "a\nb",
	}
	for input, want := range vectors {
		if got := N2LineEndings(input); got != want {
			t.Errorf("N2(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestN3CollapsesEachOfTheSixWhitespaceCharsBetweenTags(t *testing.T) {
	// The explicit six-character class — including VT (U+000B), which RE2 \s omits.
	for _, ws := range []rune{'\t', '\n', '\v', '\f', '\r', ' '} {
		input := "<a>" + string(ws) + "<b>"
		if got := N3InterTag(input); got != "<a><b>" {
			t.Errorf("N3 char U+%04X: got %q, want \"<a><b>\"", ws, got)
		}
	}
}

func TestN3CollapsesMultiCharRunsBetweenTags(t *testing.T) {
	if got := N3InterTag("<a> \t\n\v\f\r <b>"); got != "<a><b>" {
		t.Errorf("got %q", got)
	}
	if got := N3InterTag("<ul>\n  <li>x</li>\n  </ul>"); got != "<ul><li>x</li></ul>" {
		t.Errorf("got %q", got)
	}
	// Adjacent inter-tag runs in one pass (replacement "><" reintroduces no whitespace).
	if got := N3InterTag("<a> <b> <c>"); got != "<a><b><c>" {
		t.Errorf("got %q", got)
	}
}

func TestN3LeavesNonInterTagWhitespaceAlone(t *testing.T) {
	for _, s := range []string{"<p>a  b</p>", "/> \n/* css */", "a > b < c"} {
		if got := N3InterTag(s); got != s {
			t.Errorf("N3(%q) = %q, want unchanged", s, got)
		}
	}
	// Non-ASCII whitespace (U+00A0) is outside the closed set and must survive.
	if got := N3InterTag("<a> <b>"); got != "<a> <b>" {
		t.Errorf("U+00A0 must survive N3, got %q", got)
	}
}

func TestN3bRemovesEveryWhitespaceRunToNothing(t *testing.T) {
	vectors := map[string]string{
		"a b":            "ab",
		"a \t\n\v\f\r b": "ab",
		"<p>a  b</p>":    "<p>ab</p>",
		"/>\n/* css */":  "/>/*css*/",
	}
	for input, want := range vectors {
		if got := N3bStrip(input); got != want {
			t.Errorf("N3b(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestN3bPresenceVsAbsenceComparesEqual(t *testing.T) {
	// The composed-page boundary class: ">␠/*" vs ">/*" must reduce to the same bytes when
	// the strip is applied to both sides.
	if N3bStrip("> /*") != N3bStrip(">/*") {
		t.Error("presence-vs-absence whitespace divergence must compare equal under N3b")
	}
	if N3bStrip("*/ <script") != N3bStrip("*/<script") {
		t.Error("presence-vs-absence whitespace divergence must compare equal under N3b")
	}
}

func TestN4TrimsOnlyTheSixCharSet(t *testing.T) {
	if got := N4Trim(" \t\n\v\f\r x \t\n\v\f\r "); got != "x" {
		t.Errorf("got %q", got)
	}
	// Never strings.TrimSpace: a leading U+00A0 (Unicode space, outside the closed set)
	// must survive.
	if got := N4Trim(" x"); got != " x" {
		t.Errorf("U+00A0 must survive N4, got %q", got)
	}
	if got := N4Trim("  x "); got != " x" {
		t.Errorf("got %q", got)
	}
	if strings.TrimSpace(" x") == " x" {
		t.Error("sanity: TrimSpace would be output-equivalent here — vector no longer proves the distinction")
	}
}

func TestN1BomSurvivesThePipeline(t *testing.T) {
	// N1: a leading U+FEFF is NOT stripped — it flows into the comparison and fails it.
	const bom = "\uFEFF"
	normalized := NormalizeRaw(bom + "<a><b>")
	if !strings.HasPrefix(normalized, bom) {
		t.Fatalf("BOM must survive N2–N4, got %q", normalized)
	}
	if N3bStrip(normalized) == N3bStrip("<a><b>") {
		t.Error("a BOM-prefixed candidate must still differ from the oracle after N3b")
	}
	if !N1Valid(bom + "x") {
		t.Error("a BOM is valid UTF-8; N1 fails it via the byte comparison, not via decode")
	}
	if N1Valid("\xff\xfe") {
		t.Error("invalid UTF-8 must fail N1")
	}
}

func TestN5ContractTableVectors(t *testing.T) {
	vectors := map[string]string{
		// Every recognized spelling family → canonical.
		"&#x27;":   "&#39;",
		"&#034;":   "&quot;",
		"&#X3C;":   "&lt;",
		"&apos;":   "&#39;",
		"&#38;":    "&amp;",
		"&#60;":    "&lt;",
		"&#62;":    "&gt;",
		"&#34;":    "&quot;",
		"&#x0026;": "&amp;",
		// Canonical spellings are identity (the oracle is already canonical).
		"&amp;&lt;&gt;&quot;&#39;": "&amp;&lt;&gt;&quot;&#39;",
		// Full hex/decimal family vector.
		"&#60;script&#62;alert(&#34;x&#34;);": "&lt;script&gt;alert(&quot;x&quot;);",
	}
	for input, want := range vectors {
		if got := N5Canonicalize(input); got != want {
			t.Errorf("N5(%q) = %q, want %q", input, got, want)
		}
	}
}

func TestN5NoRescanOfReplacements(t *testing.T) {
	// Data that escaped TO "&amp;#39;" (a literal "&#39;" in source data) must not be
	// double-canonicalized — the sequential-ReplaceAll rescan bug this scanner exists to
	// avoid ("&amp;#39;" would rescan to "&#39;" and then be treated as an apostrophe).
	if got := N5Canonicalize("&amp;#39;"); got != "&amp;#39;" {
		t.Errorf("N5 must not rescan replacements: got %q, want \"&amp;#39;\"", got)
	}
}

func TestN5LeavesNonFiveCharReferencesUntouched(t *testing.T) {
	for _, s := range []string{"&#43;", "a &#x60; b", "&copy; & &#", "&#x3D;"} {
		if got := N5Canonicalize(s); got != s {
			t.Errorf("N5(%q) = %q, want unchanged (scoped to the five characters)", s, got)
		}
	}
	// Case-sensitivity: named entities match case-sensitively.
	if got := N5Canonicalize("&APOS;&Amp;"); got != "&APOS;&Amp;" {
		t.Errorf("named entities are case-sensitive, got %q", got)
	}
}

func TestNormalizeForSuiteAppliesN5OnlyToEncoded(t *testing.T) {
	if got := NormalizeForSuite("&#34;", "encoded"); got != "&quot;" {
		t.Errorf("encoded suite must apply N5, got %q", got)
	}
	if got := NormalizeForSuite("&#34;", "raw"); got != "&#34;" {
		t.Errorf("raw suite must not apply N5, got %q", got)
	}
}
