package corpus_test

import (
	"strings"
	"testing"

	"heddle.dev/benchmarks/go/internal/corpus"
)

// Verifier calibration (README D12; golden-corpus.md §Verification): the Go verifier must
// accept each workload's committed golden and reject every synthesized corruption with the
// correct failing check kind — two corruptions per raw workload, three per encoded workload
// (6×2 + 2×3 = 18 rejections). The corruption pins mirror the Phase 1 verify-corpus command
// (IdiomaticChecks.cs). This proves the Go verifier is the same verifier, not a lookalike.

// pins holds one workload's calibration corruption recipes.
type pins struct {
	workload       string
	removedSegment string
	removedKind    corpus.FailureKind
	swapA, swapB   string
	// unescapeFrom/To — encoded workloads only ("" = raw workload, two corruptions).
	unescapeFrom, unescapeTo string
}

// encodedLoopRow0 is row 0 of the encoded-loop golden (all three cells escaped with the
// canonical spellings), the workload's removed-row segment.
const encodedLoopRow0 = `<tr><td data-tag="tag-0&amp;&#39;0&#39;">item &lt;0&gt; &amp; &quot;co&quot;</td><td>&#39;q&#39; &amp; &lt;angle&gt; &quot;d&quot; こんにちは 0</td></tr>`

var calibrationPins = [8]pins{
	{
		workload:       "composed-page",
		removedSegment: `<meta property="og:image" content="/files/catalog/img.jpg">`,
		removedKind:    corpus.KindMarker,
		swapA:          "<title>Title</title>",
		swapB:          `<meta property="og:image" content="/files/catalog/img.jpg">`,
	},
	{
		workload:       "trivial-substitution",
		removedSegment: "HB-2001",
		removedKind:    corpus.KindValue,
		swapA:          `class="sku"`,
		swapB:          `class="rating"`,
	},
	{
		workload:       "large-loop",
		removedSegment: "<tr><td>row-0</td><td>0</td></tr>",
		removedKind:    corpus.KindValue,
		swapA:          "<tr><td>row-0</td><td>0</td></tr>",
		swapB:          "<tr><td>row-2500</td><td>2500</td></tr>",
	},
	{
		workload:       "mixed-page",
		removedSegment: `<article class="card">`,
		removedKind:    corpus.KindValue,
		swapA:          "<header>",
		swapB:          `class="hero"`,
	},
	{
		workload:       "conditional-heavy",
		removedSegment: "unit-000",
		removedKind:    corpus.KindValue,
		swapA:          "unit-000",
		swapB:          "unit-100",
	},
	{
		workload:       "fragment-heavy",
		removedSegment: "tile-00",
		removedKind:    corpus.KindValue,
		swapA:          "tile-00",
		swapB:          "tile-24",
	},
	{
		workload:       "fortunes-encoded",
		removedSegment: "<tr><td>1</td><td>A bad random number generator: 1, 1, 1, 1, 1, 4.33e67, 1, 1, 1</td></tr>",
		removedKind:    corpus.KindValue,
		swapA:          "<tr><th>id</th><th>message</th></tr>",
		swapB:          "フレームワークのベンチマーク",
		unescapeFrom:   "&lt;script&gt;alert(",
		unescapeTo:     "<script>alert(",
	},
	{
		workload:       "encoded-loop",
		removedSegment: encodedLoopRow0,
		removedKind:    corpus.KindValue,
		swapA:          "tag-0&amp;&#39;0&#39;",
		swapB:          "item &lt;2500&gt;",
		// Phase 1 pin: the workload carries no script payload; its escaped→raw corruption
		// is the comment's angle text, which must always be escaped (forbidden: <angle>).
		unescapeFrom: "&lt;angle&gt;",
		unescapeTo:   "<angle>",
	},
}

// ---- corruption synthesis (golden-corpus.md §Verification rules) -----------------------------

// removeFirst deletes the first occurrence of segment ('removed row').
func removeFirst(t *testing.T, golden, segment string) string {
	t.Helper()
	at := strings.Index(golden, segment)
	if at < 0 {
		t.Fatalf("removed-segment pin not found: %q", segment)
	}
	return golden[:at] + golden[at+len(segment):]
}

// swapFirst swaps the first occurrence of a with the first occurrence of b after it
// ('reordered section').
func swapFirst(t *testing.T, golden, a, b string) string {
	t.Helper()
	ia := strings.Index(golden, a)
	if ia < 0 {
		t.Fatalf("swap pin A not found: %q", a)
	}
	ib := strings.Index(golden[ia+len(a):], b)
	if ib < 0 {
		t.Fatalf("swap pin B not found after A: %q", b)
	}
	ib += ia + len(a)
	return golden[:ia] + b + golden[ia+len(a):ib] + a + golden[ib+len(b):]
}

// unescapeFirst replaces the first escaped form with its raw form ('unescaped payload').
func unescapeFirst(t *testing.T, golden, escaped, raw string) string {
	t.Helper()
	if !strings.Contains(golden, escaped) {
		t.Fatalf("unescape pin not found: %q", escaped)
	}
	return strings.Replace(golden, escaped, raw, 1)
}

func loadPair(t *testing.T, workload string) (*corpus.VerifyDef, string) {
	t.Helper()
	def, err := corpus.LoadVerify(workload)
	if err != nil {
		t.Fatal(err)
	}
	golden, err := corpus.LoadGolden(workload)
	if err != nil {
		t.Fatal(err)
	}
	return def, golden
}

func assertRejectedWith(t *testing.T, def *corpus.VerifyDef, corrupted string, kind corpus.FailureKind, label string) {
	t.Helper()
	failures := corpus.Verify(def, corrupted)
	if len(failures) == 0 {
		t.Fatalf("%s/%s: corruption must be rejected", def.Workload, label)
	}
	for _, f := range failures {
		if f.Kind == kind {
			return
		}
	}
	t.Fatalf("%s/%s: expected a %s failure, got: %v", def.Workload, label, kind, failures)
}

// ---- calibration tests -----------------------------------------------------------------------

func TestVerifierAcceptsAllEightCommittedGoldens(t *testing.T) {
	for _, id := range corpus.Workloads {
		def, golden := loadPair(t, id)
		if failures := corpus.Verify(def, golden); len(failures) != 0 {
			t.Errorf("%s: golden must be accepted, got: %v", id, failures)
		}
	}
}

func TestVerifierRejectsRemovedRowCorruptions(t *testing.T) {
	for _, p := range calibrationPins {
		def, golden := loadPair(t, p.workload)
		corrupted := removeFirst(t, golden, p.removedSegment)
		assertRejectedWith(t, def, corrupted, p.removedKind, "removed-row")
	}
}

func TestVerifierRejectsReorderedSectionCorruptions(t *testing.T) {
	for _, p := range calibrationPins {
		def, golden := loadPair(t, p.workload)
		corrupted := swapFirst(t, golden, p.swapA, p.swapB)
		assertRejectedWith(t, def, corrupted, corpus.KindMarker, "reordered-section")
	}
}

func TestVerifierRejectsUnescapedPayloadCorruptions(t *testing.T) {
	seen := 0
	for _, p := range calibrationPins {
		if p.unescapeFrom == "" {
			continue // raw workloads carry no escaped payload (two corruptions only)
		}
		seen++
		def, golden := loadPair(t, p.workload)
		corrupted := unescapeFirst(t, golden, p.unescapeFrom, p.unescapeTo)
		assertRejectedWith(t, def, corrupted, corpus.KindForbidden, "unescaped-payload")
	}
	if seen != 2 {
		t.Fatalf("exactly the two encoded workloads carry an unescape pin, got %d", seen)
	}
}

// The rejection total is structural: 6 raw × 2 + 2 encoded × 3 = 18 corruptions.
func TestCalibrationCoversEighteenCorruptions(t *testing.T) {
	total := 0
	for _, p := range calibrationPins {
		total += 2
		if p.unescapeFrom != "" {
			total++
		}
	}
	if total != 18 {
		t.Fatalf("calibration must synthesize 18 corruptions, got %d", total)
	}
}

// ---- verifier mechanics ----------------------------------------------------------------------

func TestVerifierCountsAreNonOverlappingAndWhitespaceStripped(t *testing.T) {
	def := &corpus.VerifyDef{
		Workload: "unit",
		Suite:    "raw",
		Values:   []corpus.ValueCheck{{Text: "a b", Count: 2}},
		Markers:  []string{"a b"},
	}
	// Needle "a b" strips to "ab"; the output's spacing must not affect the count.
	if failures := corpus.Verify(def, "ab  a\tb"); len(failures) != 0 {
		t.Errorf("whitespace-insensitive matching must accept, got: %v", failures)
	}
}

func TestVerifierForbiddenScansRawAndNormalizedOutput(t *testing.T) {
	def := &corpus.VerifyDef{
		Workload:  "unit",
		Suite:     "encoded",
		Forbidden: []string{"<script>alert("},
	}
	// The raw payload split by whitespace is still caught in the raw scan.
	failures := corpus.Verify(def, "<script>\nalert(1)")
	if len(failures) != 1 || failures[0].Kind != corpus.KindForbidden {
		t.Errorf("forbidden must scan the raw output whitespace-stripped, got: %v", failures)
	}
}

func TestVerifierMarkerOrderIsStrict(t *testing.T) {
	def := &corpus.VerifyDef{
		Workload: "unit",
		Suite:    "raw",
		Markers:  []string{"first", "second"},
	}
	if failures := corpus.Verify(def, "second first"); len(failures) == 0 {
		t.Error("out-of-order markers must be rejected")
	}
	if failures := corpus.Verify(def, "first second"); len(failures) != 0 {
		t.Errorf("in-order markers must be accepted, got: %v", failures)
	}
}
