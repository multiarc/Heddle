#!/usr/bin/env bash
# Decides what, if anything, a ref publishes. Every publishing job depends on this, so a tag that does not
# match one of the shapes below publishes nothing anywhere, and a pull request never publishes at all.
#
#   vX.Y.Z           release: the tagged commit must be on main
#   vX.Y.Z-alpha.N   pre-release, from any branch
#   vX.Y.Z-beta.N    pre-release, from any branch
#   vX.Y.Z-rc.N      pre-release, from any branch
#
# The pre-release number is dotted because SemVer compares an undotted "beta10" with "beta9" as text, so
# beta10 would sort below beta9 on nuget.org and npm. "beta.10" compares numerically.
#
# Outputs, appended to $GITHUB_OUTPUT:
#   is_tag         true for a v* tag, false for anything else (branches, pull requests)
#   version        the version as tagged, without the "v"            3.0.0-beta.2
#   prerelease     true when the tag carries a suffix
#   channel        the npm dist-tag: latest, alpha, beta or rc
#   vsix_version   the VS Code Marketplace version                     3.0.2002
#
# The Marketplace accepts only a plain X.Y.Z and never takes a version twice, so a pre-release cannot share
# its release's number. It maps to X.Y.(Z*10000 + rank*1000 + N), with alpha=1, beta=2, rc=3 and N<=999:
# deterministic, ordered as SemVer orders the tags, and never equal to a release. As in VS Code's own
# odd/even-minor scheme, a pre-release is numerically above the release it leads to, so users on the
# pre-release channel stay on pre-releases until the next one ships.
#
# Needs full history (checkout fetch-depth: 0) to check that a release tag is on main.

set -euo pipefail

out=${GITHUB_OUTPUT:?GITHUB_OUTPUT is not set}

if [[ "${GITHUB_REF:?GITHUB_REF is not set}" != refs/tags/v* ]]; then
  echo "is_tag=false" >> "$out"
  echo "Not a release tag (${GITHUB_REF}): nothing is published."
  exit 0
fi

tag=${GITHUB_REF#refs/tags/}
num='(0|[1-9][0-9]*)'
if [[ ! "$tag" =~ ^v${num}\.${num}\.${num}(-(alpha|beta|rc)\.([1-9][0-9]*))?$ ]]; then
  echo "::error::Tag ${tag} is not a release tag. Use vX.Y.Z for a release, or vX.Y.Z-alpha.N, vX.Y.Z-beta.N or vX.Y.Z-rc.N for a pre-release."
  exit 1
fi

major=${BASH_REMATCH[1]}
minor=${BASH_REMATCH[2]}
patch=${BASH_REMATCH[3]}
label=${BASH_REMATCH[5]}
number=${BASH_REMATCH[6]}
version=${tag#v}

if [ -z "$label" ]; then
  git fetch --no-tags --quiet origin main
  if ! git merge-base --is-ancestor "${GITHUB_SHA:?GITHUB_SHA is not set}" origin/main; then
    echo "::error::Release tag ${tag} is not on main. Tag a commit on main, or use a pre-release tag (${tag}-rc.1)."
    exit 1
  fi
  prerelease=false
  channel=latest
  vsix_version=$version
else
  if [ "${#number}" -gt 3 ]; then
    echo "::error::Tag ${tag}: a pre-release number above 999 has no Marketplace version."
    exit 1
  fi
  case "$label" in
    alpha) rank=1 ;;
    beta)  rank=2 ;;
    rc)    rank=3 ;;
  esac
  prerelease=true
  channel=$label
  vsix_version="${major}.${minor}.$(( patch * 10000 + rank * 1000 + number ))"
fi

{
  echo "is_tag=true"
  echo "version=${version}"
  echo "prerelease=${prerelease}"
  echo "channel=${channel}"
  echo "vsix_version=${vsix_version}"
} >> "$out"

echo "${tag}: version ${version}, prerelease ${prerelease}, npm dist-tag ${channel}, Marketplace ${vsix_version}"
