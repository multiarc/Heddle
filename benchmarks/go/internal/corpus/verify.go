package corpus

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"strings"
)

// ---- <id>.verify.json model (golden-corpus.md §Idiomatic verifier definitions) ---------------

// VerifyDef is one workload's exported idiomatic-verifier definition.
type VerifyDef struct {
	Workload  string          `json:"workload"`
	Suite     string          `json:"suite"`
	Values    []ValueCheck    `json:"values"`
	Markers   []string        `json:"markers"`
	Forbidden []string        `json:"forbidden"`
	Required  []RequiredCheck `json:"required"`
}

// ValueCheck requires an exact non-overlapping occurrence count.
type ValueCheck struct {
	Text  string `json:"text"`
	Count int    `json:"count"`
}

// RequiredCheck requires a minimum occurrence count (encoded workloads).
type RequiredCheck struct {
	Text     string `json:"text"`
	MinCount int    `json:"minCount"`
}

// LoadVerify reads and parses <workload>.verify.json.
func LoadVerify(workload string) (*VerifyDef, error) {
	path := filepath.Join(Dir(), workload+".verify.json")
	raw, err := os.ReadFile(path)
	if err != nil {
		return nil, fmt.Errorf("gate: corpus %s: cannot read %s: %w (regenerate via the Phase 1 export-corpus tool: dotnet run -c Release --project src/Heddle.Performance -f net10.0 -- export-corpus)", workload, path, err)
	}
	var def VerifyDef
	if err := json.Unmarshal(raw, &def); err != nil {
		return nil, fmt.Errorf("gate: corpus %s: cannot parse verify.json: %w", workload, err)
	}
	return &def, nil
}

// ---- failure surface (README §Diagnostics) ---------------------------------------------------

// FailureKind is the contract's four check kinds.
type FailureKind string

// The four check kinds of parity-contract-v2 §Idiomatic-track gate.
const (
	KindValue     FailureKind = "value"
	KindMarker    FailureKind = "marker"
	KindForbidden FailureKind = "forbidden"
	KindRequired  FailureKind = "required"
)

// Failure is one failed verifier check: its kind plus the Diagnostics-table message tail.
type Failure struct {
	Kind    FailureKind
	Message string
}

func (f Failure) String() string { return fmt.Sprintf("verifier %s: %s", f.Kind, f.Message) }

// ---- verifier --------------------------------------------------------------------------------

// Verify runs the idiomatic verifier against a candidate's raw output, per
// parity-contract-v2 §Idiomatic-track gate: normalize (N1–N4, +N5 for encoded), then apply
// the N3b whitespace strip to the output AND to every needle before matching; `values` are
// exact non-overlapping counts, `markers` are strictly ordered, `forbidden` must be absent
// from both raw and normalized output, `required` is a minimum count. Empty result =
// accepted.
func Verify(def *VerifyDef, rawOutput string) []Failure {
	stripped := N3bStrip(NormalizeForSuite(rawOutput, def.Suite))
	strippedRaw := N3bStrip(rawOutput)
	var failures []Failure

	for _, v := range def.Values {
		found := countOccurrences(stripped, N3bStrip(v.Text))
		if found != v.Count {
			failures = append(failures, Failure{KindValue,
				fmt.Sprintf("expected %d of %q, found %d", v.Count, excerpt(v.Text), found)})
		}
	}

	pos := 0
	for _, marker := range def.Markers {
		needle := N3bStrip(marker)
		at := indexFrom(stripped, needle, pos)
		if at < 0 {
			// Keep scanning subsequent markers from the current position so every
			// out-of-order/missing marker is reported.
			failures = append(failures, Failure{KindMarker,
				fmt.Sprintf("expected 1 of %q in order after index %d, found 0", excerpt(marker), pos)})
			continue
		}
		pos = at + len(needle)
	}

	for _, f := range def.Forbidden {
		needle := N3bStrip(f)
		found := max(countOccurrences(strippedRaw, needle), countOccurrences(stripped, needle))
		if found != 0 {
			failures = append(failures, Failure{KindForbidden,
				fmt.Sprintf("expected 0 of %q, found %d", excerpt(f), found)})
		}
	}

	for _, r := range def.Required {
		found := countOccurrences(stripped, N3bStrip(r.Text))
		if found < r.MinCount {
			failures = append(failures, Failure{KindRequired,
				fmt.Sprintf("expected %d of %q, found %d", r.MinCount, excerpt(r.Text), found)})
		}
	}

	return failures
}

// countOccurrences counts non-overlapping occurrences of needle in haystack.
func countOccurrences(haystack, needle string) int {
	if needle == "" {
		return 0
	}
	count, index := 0, 0
	for {
		at := indexFrom(haystack, needle, index)
		if at < 0 {
			return count
		}
		count++
		index = at + len(needle)
	}
}

// indexFrom is strings.Index starting at from (absolute result index, -1 = not found).
func indexFrom(haystack, needle string, from int) int {
	if from > len(haystack) {
		return -1
	}
	at := strings.Index(haystack[from:], needle)
	if at < 0 {
		return -1
	}
	return from + at
}

// excerpt truncates a needle for failure messages (48 runes + ellipsis, '\n'-escaped).
func excerpt(s string) string {
	runes := []rune(s)
	if len(runes) > 48 {
		s = string(runes[:48]) + "…"
	}
	return strings.ReplaceAll(s, "\n", "\\n")
}
