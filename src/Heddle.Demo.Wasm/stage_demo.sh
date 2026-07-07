#!/usr/bin/env bash
# Phase 9 D9 — local staging: publish the WASM host and copy the bundle into docs/public/demo so that
# `npm run docs:dev` (or docs:preview) lights up the typed demo layer locally. The docs workflow runs these
# same two commands in CI. Without this, the page runs in the no-WASM fallback layer (the local-dev default).
set -euo pipefail

here="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo="$(cd "$here/../.." && pwd)"

echo "Publishing Heddle.Demo.Wasm (Release)…"
dotnet publish "$here/Heddle.Demo.Wasm.csproj" -c Release

dest="$repo/docs/public/demo"
mkdir -p "$dest"
cp -R "$here/bin/Release/net10.0/publish/wwwroot/." "$dest/"

echo "Staged demo bundle into $dest"
du -sh "$dest" 2>/dev/null || true
