package corpus

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"strings"
	"unicode/utf8"
)

// ---- corpus access ---------------------------------------------------------------------------

// Dir returns the committed corpus directory, resolved repo-relative from this source file
// via runtime.Caller so `go test` works from any working directory
// (benchmarks/go/internal/corpus → ../../../../src/Heddle.Performance/GoldenCorpus).
func Dir() string {
	_, thisFile, _, ok := runtime.Caller(0)
	if !ok {
		panic("gate: runtime.Caller failed — cannot resolve the corpus directory")
	}
	return filepath.Join(filepath.Dir(thisFile),
		"..", "..", "..", "..", "src", "Heddle.Performance", "GoldenCorpus")
}

// Workloads holds the eight workload ids in workload-number order (Phase 1 workloads.md).
var Workloads = [8]string{
	"composed-page",
	"trivial-substitution",
	"large-loop",
	"mixed-page",
	"conditional-heavy",
	"fragment-heavy",
	"fortunes-encoded",
	"encoded-loop",
}

// LoadGolden reads <workload>.golden.html — the normalized oracle (stored N1–N5 form,
// UTF-8 no BOM, no trailing newline) — and fails on unreadable or invalid-UTF-8 content.
func LoadGolden(workload string) (string, error) {
	path := filepath.Join(Dir(), workload+".golden.html")
	raw, err := os.ReadFile(path)
	if err != nil {
		return "", fmt.Errorf("gate: corpus %s: cannot read %s: %w (regenerate via the Phase 1 export-corpus tool: dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- export-corpus)", workload, path, err)
	}
	if !utf8.Valid(raw) {
		return "", fmt.Errorf("gate: corpus %s: golden is not valid UTF-8", workload)
	}
	return string(raw), nil
}

// ---- manifest --------------------------------------------------------------------------------

// Manifest mirrors manifest.json (golden-corpus.md §Manifest).
type Manifest struct {
	Schema    string          `json:"$schema"`
	Generator string          `json:"generator"`
	Entries   []ManifestEntry `json:"entries"`
}

// ManifestEntry is one workload's manifest record.
type ManifestEntry struct {
	Workload         string `json:"workload"`
	Suite            string `json:"suite"`
	File             string `json:"file"`
	ByteLength       int64  `json:"byteLength"`
	Hash             string `json:"hash"`
	GeneratingCommit string `json:"generatingCommit"`
	GeneratedUtc     string `json:"generatedUtc"`
}

// LoadManifest reads and parses manifest.json.
func LoadManifest() (*Manifest, error) {
	path := filepath.Join(Dir(), "manifest.json")
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("gate: corpus manifest: cannot read %s: %w", path, err)
	}
	var m Manifest
	if err := json.Unmarshal(raw, &m); err != nil {
		return nil, fmt.Errorf("gate: corpus manifest: cannot parse manifest.json: %w", err)
	}
	return &m, nil
}

// CheckManifest recomputes every corpus file's SHA-256 and byte length and checks them
// against the manifest, so a corrupted checkout is distinguished from a divergent render.
func CheckManifest() error {
	m, err := LoadManifest()
	if err != nil {
		return err
	}
	if len(m.Entries) != len(Workloads) {
		return fmt.Errorf("gate: corpus manifest has %d entries, want %d", len(m.Entries), len(Workloads))
	}
	for i, entry := range m.Entries {
		if entry.Workload != Workloads[i] {
			return fmt.Errorf("gate: corpus manifest entry %d is %q, want %q (workload-number order)", i, entry.Workload, Workloads[i])
		}
		raw, err := os.ReadFile(filepath.Join(Dir(), entry.File))
		if err != nil {
			return fmt.Errorf("gate: corpus file %s cannot be read: %w", entry.File, err)
		}
		sum := sha256.Sum256(raw)
		hash := "sha256:" + hex.EncodeToString(sum[:])
		if hash != entry.Hash || int64(len(raw)) != entry.ByteLength {
			return fmt.Errorf("gate: corpus file %s hash mismatch vs manifest (file %s/%d bytes, manifest %s/%d bytes)",
				entry.File, hash, len(raw), entry.Hash, entry.ByteLength)
		}
	}
	return nil
}

// ---- controlled byte gate --------------------------------------------------------------------

// The encoded-suite security-floor needles (parity-contract-v2 §Controlled-track gate 5).
const (
	rawPayload     = "<script>alert("
	escapedPayload = "&lt;script&gt;alert("
)

// CheckControlled runs the controlled byte gate for one cell: render → N1 → normalize
// (N2–N4, +N5 when suite == "encoded") → N3b-strip both sides → compare the non-whitespace
// UTF-8 bytes with the corpus entry. Encoded cells additionally assert the security floor.
// nil = the cell passes.
func CheckControlled(workload, suite, engine string, render func() string) error {
	golden, err := LoadGolden(workload)
	if err != nil {
		return err
	}
	output := render()
	if !N1Valid(output) {
		return fmt.Errorf("gate: %s/%s diverged: candidate output is not valid UTF-8 (N1)", workload, engine)
	}
	if suite == "encoded" {
		if err := checkSecurityFloor(workload, engine, output); err != nil {
			return err
		}
	}
	expected := N3bStrip(golden)
	actual := N3bStrip(NormalizeForSuite(output, suite))
	if expected == actual {
		return nil
	}
	at := firstDiff(expected, actual)
	return fmt.Errorf("gate: %s/%s diverged: first diff at %d (exp %d/act %d)\n  expected: ...%s...\n  actual:   ...%s...",
		workload, engine, at, len(expected), len(actual), window(expected, at), window(actual, at))
}

// checkSecurityFloor asserts the raw payload rule on the UN-normalized candidate output plus
// the expected escaped-payload count after N5 (from the verifier's `required` entry when one
// exists — fortunes-encoded: 1; encoded-loop carries no script payload: 0).
func checkSecurityFloor(workload, engine, output string) error {
	if n := strings.Count(output, rawPayload); n != 0 {
		return fmt.Errorf("gate: %s/%s forbidden payload %q found %d times (want 0)", workload, engine, rawPayload, n)
	}
	def, err := LoadVerify(workload)
	if err != nil {
		return err
	}
	want := 0
	for _, r := range def.Required {
		if r.Text == escapedPayload {
			want = r.MinCount
		}
	}
	found := countOccurrences(N3bStrip(NormalizeEncoded(output)), escapedPayload)
	if found != want {
		return fmt.Errorf("gate: %s/%s escaped form found %d (want %d)", workload, engine, found, want)
	}
	return nil
}

// ---- untrusted-data alphabet assert (README D5) ----------------------------------------------

// CheckAlphabet scans one materialized encoded-suite model value against the contract's
// untrusted-data alphabet and returns a named error on the first violation. name is the
// "<workload>.<field>[<row>]" label of the value. Forbidden: '+', '=', '`', the "&#"
// substring, U+00A0–U+00FF (Latin-1 supplement), and any code point requiring a surrogate
// pair (astral plane); C0/C1 controls are covered by the ranges below.
func CheckAlphabet(name, value string) error {
	if strings.Contains(value, "&#") {
		return fmt.Errorf("gate: model value %s violates untrusted-data alphabet: substring \"&#\"", name)
	}
	for _, r := range value {
		switch {
		case r == '+' || r == '=' || r == '`':
			return alphabetError(name, r)
		case r < 0x20 || r == 0x7F: // C0 controls + DEL (never appear in the pinned data)
			return alphabetError(name, r)
		case r >= 0x80 && r <= 0xFF: // C1 controls + Latin-1 supplement (U+00A0–U+00FF)
			return alphabetError(name, r)
		case r >= 0xD800 && r <= 0xDFFF: // surrogate range (invalid scalar; belt-and-braces)
			return alphabetError(name, r)
		case r > 0xFFFF: // astral plane — requires a surrogate pair
			return alphabetError(name, r)
		}
	}
	return nil
}

func alphabetError(name string, r rune) error {
	return fmt.Errorf("gate: model value %s violates untrusted-data alphabet: %q U+%04X", name, r, r)
}

// ---- diagnostics helpers (README §Diagnostics message shape) ---------------------------------

// firstDiff returns the index of the first differing byte (= common length when one side is
// a prefix of the other).
func firstDiff(expected, actual string) int {
	n := min(len(expected), len(actual))
	for i := 0; i < n; i++ {
		if expected[i] != actual[i] {
			return i
		}
	}
	return n
}

// window returns a 120-byte excerpt starting ±40 bytes before at, '\n'-escaped and clamped
// to UTF-8 rune boundaries (the ParityCheck.Describe shape).
func window(s string, at int) string {
	start := max(0, at-40)
	for start > 0 && !utf8.RuneStart(s[start]) {
		start--
	}
	end := min(len(s), start+120)
	for end < len(s) && !utf8.RuneStart(s[end]) {
		end++
	}
	return strings.ReplaceAll(s[start:end], "\n", "\\n")
}
