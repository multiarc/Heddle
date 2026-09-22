#!/usr/bin/env bash
# Proves resolve-release-tag.sh decides what it claims to: which refs publish, which tag shapes are
# refused, and the Marketplace version a tag maps to - including the ordering property that mapping
# exists for, that a later release outranks an earlier pre-release. The resolver runs only when someone
# pushes a tag, so without this its first execution is a real release.
set -u

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
resolver="$here/resolve-release-tag.sh"
work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT

# The resolver asks git two things for a release tag: fetch main, and is this commit an ancestor of it.
# A shim answers both, so the table below tests the decision rather than this clone's history.
# MERGE_BASE_STATUS is the shim's answer to the ancestry question: 0 on main, 1 off it.
mkdir -p "$work/bin"
cat > "$work/bin/git" <<'SHIM'
#!/usr/bin/env bash
case "$1" in
  fetch)      exit 0 ;;
  merge-base) exit "${MERGE_BASE_STATUS:-0}" ;;
  *)          exit 0 ;;
esac
SHIM
chmod +x "$work/bin/git"
PATH="$work/bin:$PATH"

fail=0
out="$work/out"
status=0

run() { # <ref>
  : > "$out"
  GITHUB_OUTPUT="$out" GITHUB_REF="$1" GITHUB_SHA=0000000000000000000000000000000000000000 \
    bash "$resolver" >/dev/null 2>&1
  status=$?
}

value() { sed -n "s/^$1=//p" "$out"; }

check() { # <label> <expected> <actual>
  if [ "$2" != "$3" ]; then echo "SELFTEST FAIL: $1 expected '$2' got '$3'" >&2; fail=1; fi
}

publishes() { # <ref> <version> <prerelease> <channel> <vsix_version>
  run "$1"
  check "$1 exit" 0 "$status"
  check "$1 is_tag" true "$(value is_tag)"
  check "$1 version" "$2" "$(value version)"
  check "$1 prerelease" "$3" "$(value prerelease)"
  check "$1 channel" "$4" "$(value channel)"
  check "$1 vsix_version" "$5" "$(value vsix_version)"
  [ "$fail" -eq 0 ] && echo "selftest ok: $1 -> $2 (extension $5)"
}

silent() { # <ref> - a ref that publishes nothing, without failing the run
  run "$1"
  check "$1 exit" 0 "$status"
  check "$1 is_tag" false "$(value is_tag)"
  echo "selftest ok: $1 publishes nothing"
}

refused() { # <ref> - a tag shape the resolver must reject loudly
  run "$1"
  if [ "$status" -eq 0 ]; then
    echo "SELFTEST FAIL: $1 was accepted; it must be refused" >&2; fail=1
  else
    echo "selftest ok: $1 refused (exit $status)"
  fi
}

# 1) Refs that publish nothing.
silent refs/heads/main
silent refs/pull/42/merge
silent refs/tags/nightly
# A capital V matches neither the workflows' `v*.*.*` trigger nor the resolver's `refs/tags/v*` test,
# both of which are case-sensitive. It is not a tag as far as publishing is concerned, not a bad tag.
silent refs/tags/V1.2.3

# 2) Releases and pre-releases, with the Marketplace number each maps to.
publishes refs/tags/v3.0.0        3.0.0        false latest 3.0.0
publishes refs/tags/v3.0.1        3.0.1        false latest 3.0.10000
publishes refs/tags/v3.2.7        3.2.7        false latest 3.2.70000
publishes refs/tags/v3.0.0-alpha.1 3.0.0-alpha.1 true alpha 3.0.1001
publishes refs/tags/v3.0.0-beta.2  3.0.0-beta.2  true beta  3.0.2002
publishes refs/tags/v3.0.0-rc.1    3.0.0-rc.1    true rc    3.0.3001
publishes refs/tags/v3.0.1-rc.1    3.0.1-rc.1    true rc    3.0.13001
publishes refs/tags/v3.0.0-beta.999 3.0.0-beta.999 true beta 3.0.2999

# 3) The ordering the mapping exists for: within one minor line, each tag's extension number is strictly
#    above the one before it, so the Marketplace always offers the newer build - including a release that
#    follows a pre-release, which is the case a bare release number would strand.
ordered=""
for ref in v3.0.0 v3.0.0-alpha.1 v3.0.0-beta.2 v3.0.0-rc.1 v3.0.1 v3.0.1-rc.1 v3.0.2; do
  run "refs/tags/$ref"
  n=$(value vsix_version); n=${n##*.}
  ordered="$ordered $n"
done
previous=-1
for n in $ordered; do
  if [ "$n" -le "$previous" ]; then
    echo "SELFTEST FAIL: extension numbers are not strictly increasing:$ordered" >&2; fail=1; break
  fi
  previous=$n
done
[ "$fail" -eq 0 ] && echo "selftest ok: extension numbers strictly increase:$ordered"

# 4) Shapes that must be refused.
refused refs/tags/v1.2.3.4          # four components
refused refs/tags/v1.2              # two components reaching the resolver directly
refused refs/tags/v1.2.3-beta1      # undotted pre-release number
refused refs/tags/v1.2.3-beta.0     # zero pre-release number
refused refs/tags/v1.2.3-beta.1000  # above the 999 the Marketplace mapping can carry
refused refs/tags/v01.2.3           # leading zero
refused refs/tags/v1.2.3-preview.1  # label outside alpha/beta/rc

# 5) A release tag must be on main; a pre-release tag may be anywhere.
MERGE_BASE_STATUS=1 run refs/tags/v3.0.0
if [ "$status" -eq 0 ]; then
  echo "SELFTEST FAIL: a release tag off main was accepted" >&2; fail=1
else
  echo "selftest ok: release tag off main refused (exit $status)"
fi
MERGE_BASE_STATUS=1 run refs/tags/v3.0.0-rc.1
check "pre-release off main exit" 0 "$status"
check "pre-release off main vsix_version" 3.0.3001 "$(value vsix_version)"
[ "$fail" -eq 0 ] && echo "selftest ok: pre-release tag off main still publishes"

if [ "$fail" -ne 0 ]; then
  echo "::error::resolve-release-tag.sh self-test failed."
  exit 1
fi
echo "resolve-release-tag.sh self-test passed."
