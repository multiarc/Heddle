#!/usr/bin/env bash
# Phase 9 D11 — the shared golden comparer for the sample gallery.
#
# usage: compare-golden.sh <sample-dir>
#   For every file under <sample-dir>/golden/: require the same relative path under <sample-dir>/out/,
#   normalize CRLF->LF on both sides, byte-compare; collect ALL mismatches (do not stop at first).
#   Fail if out/ contains files absent from golden/ (and vice versa) — the file sets must match exactly.
#   UPDATE_GOLDEN=1: copy out/ over golden/ (CRLF->LF normalized) instead of comparing; exit 0.
#   Exit codes: 0 identical; 1 any mismatch/missing/extra; 2 usage or missing out/.
set -u

[ "$#" -eq 1 ] || { echo "usage: compare-golden.sh <sample-dir>" >&2; exit 2; }

dir="$1"
[ -d "$dir" ] || { echo "::error::no such sample dir: $dir" >&2; exit 2; }
dir="$(cd "$dir" && pwd)"
golden="$dir/golden"
out="$dir/out"

norm() { sed 's/\r$//' "$1"; }   # strip trailing CR (CRLF->LF); LF-only files unchanged

# List regular files under a root as paths relative to it (no leading ./).
list_rel() { ( cd "$1" && find . -type f | sed 's|^\./||' | sort ); }

if [ "${UPDATE_GOLDEN:-}" = "1" ]; then
  [ -d "$out" ] || { echo "::error::no out/ directory in $dir (run the sample with --capture out first)" >&2; exit 2; }
  rm -rf "$golden"
  mkdir -p "$golden"
  while IFS= read -r rel; do
    [ -z "$rel" ] && continue
    mkdir -p "$golden/$(dirname "$rel")"
    norm "$out/$rel" > "$golden/$rel"
  done < <(list_rel "$out")
  echo "updated goldens in $golden"
  exit 0
fi

[ -d "$golden" ] || { echo "::error::no golden/ directory in $dir" >&2; exit 2; }
[ -d "$out" ]    || { echo "::error::no out/ directory in $dir (run the sample with --capture out first)" >&2; exit 2; }

status=0

# Every golden file must exist in out/ and match after normalization.
while IFS= read -r rel; do
  [ -z "$rel" ] && continue
  if [ ! -f "$out/$rel" ]; then
    echo "::error::missing in out/: $rel" >&2
    status=1
    continue
  fi
  if ! diff -u <(norm "$golden/$rel") <(norm "$out/$rel") > "$dir/.golden-diff" 2>&1; then
    echo "::error::golden mismatch: $rel" >&2
    sed 's/^/    /' "$dir/.golden-diff" >&2
    status=1
  fi
done < <(list_rel "$golden")

# Every out file must be represented in golden/ (no extra artifacts).
while IFS= read -r rel; do
  [ -z "$rel" ] && continue
  if [ ! -f "$golden/$rel" ]; then
    echo "::error::extra file in out/ (not in golden/): $rel" >&2
    status=1
  fi
done < <(list_rel "$out")

rm -f "$dir/.golden-diff"
[ "$status" -eq 0 ] && echo "golden OK: $dir"
exit "$status"
