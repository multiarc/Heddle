// Package corpus implements the Phase 1 parity-contract-v2 machinery for the Go harness:
// the normalization pipeline (N1–N5), the controlled byte gate, the idiomatic verifier, and
// the untrusted-data alphabet assert.
//
// Contract: docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/parity-contract-v2.md
// Go implementation rules: docs/spec/cross-stack-benchmarks/phase-6-go/harness-and-measurement.md
package corpus

import (
	"regexp"
	"strings"
	"unicode/utf8"
)

// contractWhitespace is the contract's closed six-character whitespace set —
// { TAB, LF, VT, FF, CR, SPACE } — used everywhere an explicit class is required.
// Never a language \s: Go's RE2 \s is the ASCII subset [\t\n\f\r ], which omits VT (U+000B).
const contractWhitespace = "\t\n\v\f\r "

var (
	// N3 — inter-tag whitespace: the explicit six-character class, not \s (RE2 \s omits VT).
	// A single ReplaceAllString pass suffices: the replacement "><" reintroduces no whitespace,
	// so replacements cannot create new matches.
	n3Pattern = regexp.MustCompile(">[\t\n\v\f\r ]+<")

	// N3b — every whitespace run, anywhere, removed to nothing (the 2026-07-20 maintainer step).
	n3bPattern = regexp.MustCompile("[\t\n\v\f\r ]+")
)

// N1Valid reports whether the candidate is valid UTF-8 (invalid UTF-8 is a gate failure).
// A leading U+FEFF (BOM) is NOT stripped anywhere in the pipeline — it flows into the
// comparison and fails it.
func N1Valid(s string) bool { return utf8.ValidString(s) }

// N2LineEndings replaces every "\r\n" with "\n", then every remaining "\r" with "\n".
func N2LineEndings(s string) string {
	return strings.ReplaceAll(strings.ReplaceAll(s, "\r\n", "\n"), "\r", "\n")
}

// N3InterTag collapses every run of one-or-more contract-whitespace characters that sits
// between '>' and '<' to nothing.
func N3InterTag(s string) string {
	return n3Pattern.ReplaceAllString(s, "><")
}

// N3bStrip removes every run of one-or-more contract-whitespace characters — to nothing,
// anywhere in the text. Applied symmetrically at comparison time to both the normalized
// candidate and the loaded oracle (and to every verifier needle); never baked into the
// stored oracle.
func N3bStrip(s string) string {
	return n3bPattern.ReplaceAllString(s, "")
}

// N4Trim removes leading and trailing contract whitespace — exactly the six characters,
// never strings.TrimSpace (which trims Unicode space and would exceed the closed definition:
// a leading U+00A0 must survive).
func N4Trim(s string) string {
	return strings.Trim(s, contractWhitespace)
}

// N5Canonicalize maps every recognized spelling of the five markup-significant characters to
// the canonical spelling (encoded suite only). Single left-to-right scan; replacements are
// non-overlapping and never rescanned (so data that escaped to "&amp;#39;" is not
// double-canonicalized) — a hand-rolled scanner, deliberately not sequential ReplaceAll
// calls (which would rescan). Named entities match case-sensitively; numeric references
// match with any number of leading zeros and case-insensitive hex digits/'x'. The spelling
// of any other escaped character (e.g. "&#43;") is not canonicalized.
func N5Canonicalize(s string) string {
	var b strings.Builder
	b.Grow(len(s))
	for i := 0; i < len(s); {
		if s[i] == '&' {
			if length, canonical, ok := matchEntity(s[i:]); ok {
				if canonical != "" {
					b.WriteString(canonical)
				} else {
					b.WriteString(s[i : i+length]) // recognized numeric ref outside the five-char set
				}
				i += length
				continue
			}
		}
		b.WriteByte(s[i])
		i++
	}
	return b.String()
}

// canonicalFor returns the canonical spelling for one of the five markup-significant
// characters, or "" if code is not one of them.
func canonicalFor(code uint32) string {
	switch code {
	case 0x26:
		return "&amp;"
	case 0x3C:
		return "&lt;"
	case 0x3E:
		return "&gt;"
	case 0x22:
		return "&quot;"
	case 0x27:
		return "&#39;"
	}
	return ""
}

// matchEntity attempts to match a recognized entity at the start of rest (which begins with
// '&'). Returns the matched length and the canonical replacement ("" = recognized numeric
// reference left untouched); ok=false when no entity is recognized at this position.
func matchEntity(rest string) (length int, canonical string, ok bool) {
	// Named spellings, matched case-sensitively (the contract's recognized set only).
	named := [...]struct{ spelling, canonical string }{
		{"&amp;", "&amp;"},
		{"&lt;", "&lt;"},
		{"&gt;", "&gt;"},
		{"&quot;", "&quot;"},
		{"&apos;", "&#39;"},
	}
	for _, n := range named {
		if strings.HasPrefix(rest, n.spelling) {
			return len(n.spelling), n.canonical, true
		}
	}

	// Numeric references: &#digits; or &#x/Xhexdigits;.
	if len(rest) < 4 || rest[1] != '#' {
		return 0, "", false
	}
	radix, digitsStart := uint32(10), 2
	if rest[2] == 'x' || rest[2] == 'X' {
		radix, digitsStart = 16, 3
	}
	var code uint32
	j := digitsStart
	for j < len(rest) {
		d, valid := digitValue(rest[j], radix)
		if !valid {
			break
		}
		if code <= 0x10FFFF { // saturate — huge references can never hit the five-char set
			code = code*radix + d
		}
		j++
	}
	if j == digitsStart || j >= len(rest) || rest[j] != ';' {
		return 0, "", false // no digits, or unterminated — not a numeric reference
	}
	return j + 1, canonicalFor(code), true
}

func digitValue(c byte, radix uint32) (uint32, bool) {
	switch {
	case c >= '0' && c <= '9':
		return uint32(c - '0'), true
	case radix == 16 && c >= 'a' && c <= 'f':
		return uint32(c-'a') + 10, true
	case radix == 16 && c >= 'A' && c <= 'F':
		return uint32(c-'A') + 10, true
	}
	return 0, false
}

// NormalizeRaw applies the stored-form pipeline for a raw-suite candidate: N2 → N3 → N4
// (N1 is asserted separately by the gate).
func NormalizeRaw(s string) string {
	return N4Trim(N3InterTag(N2LineEndings(s)))
}

// NormalizeEncoded applies the stored-form pipeline for an encoded-suite candidate:
// N2 → N3 → N4 → N5.
func NormalizeEncoded(s string) string {
	return N5Canonicalize(NormalizeRaw(s))
}

// NormalizeForSuite normalizes per suite ("encoded" applies N5; anything else is raw).
func NormalizeForSuite(s, suite string) string {
	if suite == "encoded" {
		return NormalizeEncoded(s)
	}
	return NormalizeRaw(s)
}
