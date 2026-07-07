#!/usr/bin/env bash
# Phase 9 D11/WI1 — proves compare-golden.sh can actually fail (the "test the tests" gate). Fabricates four
# fixture pairs and asserts the comparer's exit codes: content mismatch -> 1, extra out file -> 1,
# missing out file -> 1, CRLF-only difference -> 0 (normalization). Runs as the first step of every samples.yml job.
set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
comparer="$here/compare-golden.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

fail=0
expect() { # <label> <expected-exit> <actual-exit>
  if [ "$2" -ne "$3" ]; then echo "SELFTEST FAIL: $1 expected exit $2 got $3" >&2; fail=1;
  else echo "selftest ok: $1 (exit $3)"; fi
}

# 1) content mismatch -> 1
d="$work/mismatch"; mkdir -p "$d/golden" "$d/out"
printf 'hello\n' > "$d/golden/a.txt"; printf 'world\n' > "$d/out/a.txt"
bash "$comparer" "$d" >/dev/null 2>&1; expect "content-mismatch" 1 "$?"

# 2) extra file in out/ -> 1
d="$work/extra"; mkdir -p "$d/golden" "$d/out"
printf 'x\n' > "$d/golden/a.txt"; printf 'x\n' > "$d/out/a.txt"; printf 'y\n' > "$d/out/b.txt"
bash "$comparer" "$d" >/dev/null 2>&1; expect "extra-out-file" 1 "$?"

# 3) missing file in out/ -> 1
d="$work/missing"; mkdir -p "$d/golden" "$d/out"
printf 'x\n' > "$d/golden/a.txt"; printf 'y\n' > "$d/golden/b.txt"; printf 'x\n' > "$d/out/a.txt"
bash "$comparer" "$d" >/dev/null 2>&1; expect "missing-out-file" 1 "$?"

# 4) CRLF-only difference -> 0 (normalized)
d="$work/crlf"; mkdir -p "$d/golden" "$d/out"
printf 'line1\nline2\n' > "$d/golden/a.txt"; printf 'line1\r\nline2\r\n' > "$d/out/a.txt"
bash "$comparer" "$d" >/dev/null 2>&1; expect "crlf-only" 0 "$?"

if [ "$fail" -ne 0 ]; then echo "comparer self-test FAILED" >&2; exit 1; fi
echo "comparer self-test passed"
