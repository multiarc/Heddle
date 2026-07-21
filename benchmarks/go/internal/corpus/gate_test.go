package corpus_test

import (
	"strings"
	"testing"

	"heddle.dev/benchmarks/go/internal/corpus"
	"heddle.dev/benchmarks/go/internal/model"
)

// ---- corpus integrity ------------------------------------------------------------------------

func TestManifestHashesMatchCommittedFiles(t *testing.T) {
	if err := corpus.CheckManifest(); err != nil {
		t.Fatal(err)
	}
}

func TestAllEightGoldensLoadAsUtf8(t *testing.T) {
	for _, id := range corpus.Workloads {
		golden, err := corpus.LoadGolden(id)
		if err != nil {
			t.Fatal(err)
		}
		if golden == "" {
			t.Errorf("%s: golden must be non-empty", id)
		}
	}
}

func TestAllEightVerifyDefinitionsLoad(t *testing.T) {
	for _, id := range corpus.Workloads {
		def, err := corpus.LoadVerify(id)
		if err != nil {
			t.Fatal(err)
		}
		if def.Workload != id {
			t.Errorf("%s: verify.json names %q", id, def.Workload)
		}
		if def.Suite != "raw" && def.Suite != "encoded" {
			t.Errorf("%s: unexpected suite %q", id, def.Suite)
		}
		if len(def.Values) == 0 || len(def.Markers) == 0 {
			t.Errorf("%s: values and markers must be non-empty", id)
		}
	}
}

// ---- controlled gate mechanics ---------------------------------------------------------------

// The gate machinery is exercised end-to-end against a real corpus entry by rendering the
// oracle's own bytes back at it (engine ports land in WI3+; this proves the plumbing).
func TestControlledGateAcceptsTheOracleItself(t *testing.T) {
	golden, err := corpus.LoadGolden("trivial-substitution")
	if err != nil {
		t.Fatal(err)
	}
	if err := corpus.CheckControlled("trivial-substitution", "raw", "self",
		func() string { return golden }); err != nil {
		t.Fatal(err)
	}
	// A whitespace-only re-spacing passes by construction (N3b, 2026-07-20 ruling).
	if err := corpus.CheckControlled("trivial-substitution", "raw", "self",
		func() string { return strings.ReplaceAll(golden, "><", ">\n <") }); err != nil {
		t.Fatalf("whitespace-only divergence must pass: %v", err)
	}
	// A non-whitespace divergence fails with the Diagnostics shape.
	err = corpus.CheckControlled("trivial-substitution", "raw", "self",
		func() string { return strings.Replace(golden, "HB-2001", "HB-9999", 1) })
	if err == nil {
		t.Fatal("non-whitespace divergence must fail the gate")
	}
	if !strings.Contains(err.Error(), "first diff at") {
		t.Errorf("failure surface must carry the first-diff index, got: %v", err)
	}
}

func TestEncodedGateSecurityFloor(t *testing.T) {
	golden, err := corpus.LoadGolden("fortunes-encoded")
	if err != nil {
		t.Fatal(err)
	}
	// The oracle itself satisfies the floor.
	if err := corpus.CheckControlled("fortunes-encoded", "encoded", "self",
		func() string { return golden }); err != nil {
		t.Fatal(err)
	}
	// A raw payload in the un-normalized candidate fails with the security message.
	err = corpus.CheckControlled("fortunes-encoded", "encoded", "self",
		func() string { return strings.Replace(golden, "&lt;script&gt;alert(", "<script>alert(", 1) })
	if err == nil || !strings.Contains(err.Error(), "forbidden payload") {
		t.Errorf("raw payload must fail the security floor, got: %v", err)
	}
	// A missing escaped form fails the expected-count assert.
	err = corpus.CheckControlled("fortunes-encoded", "encoded", "self",
		func() string { return strings.Replace(golden, "&lt;script&gt;alert(", "&lt;div&gt;noop(", 1) })
	if err == nil || !strings.Contains(err.Error(), "escaped form found") {
		t.Errorf("wrong escaped-payload count must fail the security floor, got: %v", err)
	}
}

// ---- untrusted-data alphabet (README D5) -----------------------------------------------------

func TestAlphabetAcceptsThePinnedEncodedModels(t *testing.T) {
	for _, v := range model.EncodedValues() {
		if err := corpus.CheckAlphabet(v.Name, v.Value); err != nil {
			t.Fatal(err)
		}
	}
}

func TestAlphabetRejectsViolatingFixtures(t *testing.T) {
	// One deliberately violating fixture per forbidden class; each must fail with the named
	// error carrying the offending character.
	fixtures := []struct {
		name, value, wantSubstring string
	}{
		{"fixture.Plus[0]", "a+b", "U+002B"},
		{"fixture.Equals[0]", "a=b", "U+003D"},
		{"fixture.Backtick[0]", "a`b", "U+0060"},
		{"fixture.NumRef[0]", "a&#39;b", `substring "&#"`},
		{"fixture.Latin1[0]", "café", "U+00E9"},
		{"fixture.Nbsp[0]", "a b", "U+00A0"},
		{"fixture.Latin1Hi[0]", "aÿb", "U+00FF"},
		{"fixture.Astral[0]", "a\U0001F600b", "U+1F600"},
		{"fixture.Control[0]", "a\x01b", "U+0001"},
	}
	for _, f := range fixtures {
		err := corpus.CheckAlphabet(f.name, f.value)
		if err == nil {
			t.Errorf("%s: %q must violate the alphabet", f.name, f.value)
			continue
		}
		msg := err.Error()
		if !strings.Contains(msg, "violates untrusted-data alphabet") ||
			!strings.Contains(msg, f.name) || !strings.Contains(msg, f.wantSubstring) {
			t.Errorf("%s: named error must carry the value label and %q, got: %v", f.name, f.wantSubstring, err)
		}
	}
}

func TestAlphabetAllowsBmpAboveLatin1(t *testing.T) {
	// BMP >= U+0100 (excluding surrogates) is inside the alphabet: Japanese and U+2014.
	for _, v := range []string{"こんにちは", "フレームワーク", "a — b", "4.33e67"} {
		if err := corpus.CheckAlphabet("fixture.Ok[0]", v); err != nil {
			t.Errorf("%q must be inside the alphabet: %v", v, err)
		}
	}
}
