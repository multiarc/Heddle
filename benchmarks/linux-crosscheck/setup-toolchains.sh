#!/usr/bin/env bash
# setup-toolchains.sh — install the pinned per-engine toolchains on Ubuntu 24.04
# userland (Phase 8 spec D4/D5, WI3). Same version NUMBERS as the Windows runs,
# linux-x64 platform builds; every unavoidable difference lands in the
# version-deltas.md draft this script emits.
#
# usage: ./setup-toolchains.sh [--dry-run] [--allow-jdk-fallback] [--allow-python-fallback]
#
#   --dry-run                print the install plan and emit the version-deltas.md
#                            draft without installing anything
#   --allow-jdk-fallback     SR-3 mirror: permit Temurin/JDK 23 when no Temurin 25 GA
#                            build is downloadable (recorded as a delta row)
#   --allow-python-fallback  SR-3: permit deadsnakes python3.13 when no 3.14 is
#                            available for noble (recorded as a delta row); the
#                            spec's own fallback (source build of 3.14.6 with
#                            --enable-optimizations --with-lto, D5) remains the
#                            primary fallback and is printed as such
#
# Failure surface (spec Diagnostics): exit 1 per component,
#   "pin unavailable: <component> <version>".

set -euo pipefail
. "$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" >/dev/null 2>&1 && pwd)/common.sh"

# --- Pins (D4.2 platform artifacts; version NUMBERS mirror the Windows pins) ---
RUST_TOOLCHAIN="1.97.1"                 # rustup toolchain, host x86_64-unknown-linux-gnu (Phase 2 D1 pin)
NODE_VERSION="24.18.0"                  # nodejs.org official linux-x64 tarball (Phase 4 pin)
GO_VERSION="1.26.5"                     # go.dev/dl official linux-amd64 tarball (Phase 6 pin)
TEMURIN_MAJOR="25"                      # Temurin 25 GA linux-x64 (Phase 3)
TEMURIN_FALLBACK_MAJOR="23"             # SR-3 fallback (flag-gated, delta-recorded)
PYTHON_MINOR="3.14"                     # deadsnakes python3.14 (D5)
PYTHON_SOURCE_FALLBACK="3.14.6"         # D5 fallback: source build --enable-optimizations --with-lto
PYTHON_SR3_FALLBACK_MINOR="3.13"        # SR-3 fallback (flag-gated, delta-recorded)

# .NET SDK: the spec pins "the exact Windows-run SDK version" read from the published
# Windows protocol report's environment block. SR-1: no such report is published yet,
# so this is PARAMETERIZED with the current 10.x line observed on the Windows machine.
# Before the bare-metal run, replace/override with the published env-block value:
#   DOTNET_SDK_VERSION=<published value> ./setup-toolchains.sh
DOTNET_SDK_VERSION="${DOTNET_SDK_VERSION:-10.0.302}"   # SR-1 placeholder — see note above

INSTALL_ROOT="${LCX_TOOLCHAIN_ROOT:-$HOME/heddle-toolchains}"

DRY_RUN=0
ALLOW_JDK_FALLBACK=0
ALLOW_PY_FALLBACK=0
while [ $# -gt 0 ]; do
  case "$1" in
    --dry-run) DRY_RUN=1 ;;
    --allow-jdk-fallback) ALLOW_JDK_FALLBACK=1 ;;
    --allow-python-fallback) ALLOW_PY_FALLBACK=1 ;;
    -h|--help)
      awk 'NR > 1 { if ($0 !~ /^#/) exit; sub(/^# ?/, ""); print }' "${BASH_SOURCE[0]}"
      exit 0 ;;
    *) lcx_die "unknown argument: $1" ;;
  esac
  shift
done

lcx_mkout
DELTAS="$LCX_OUT_DIR/version-deltas.md"

pin_unavailable() { # component version
  echo "pin unavailable: $1 $2" >&2
  exit 1
}

run() {
  if [ "$DRY_RUN" = "1" ]; then
    echo "DRY-RUN: $*"
  else
    "$@"
  fi
}

delta_rows=()
add_delta_row() { # component windows_value linux_value delta caveat
  delta_rows+=("| $1 | $2 | $3 | $4 | $5 |")
}

lcx_note "install root: $INSTALL_ROOT (tarball toolchains); dry-run=$DRY_RUN"
run mkdir -p "$INSTALL_ROOT"

# --- Rust: rustup pin ---------------------------------------------------------
lcx_note "Rust: rustup toolchain install $RUST_TOOLCHAIN (host x86_64-unknown-linux-gnu)"
if [ "$DRY_RUN" = "1" ]; then
  echo "DRY-RUN: rustup toolchain install $RUST_TOOLCHAIN --profile minimal"
else
  command -v rustup >/dev/null 2>&1 || pin_unavailable "rustup" "(rustup itself missing — install via https://rustup.rs, then re-run)"
  rustup toolchain install "$RUST_TOOLCHAIN" --profile minimal || pin_unavailable "rust" "$RUST_TOOLCHAIN"
  rustup run "$RUST_TOOLCHAIN" rustc -vV
fi
add_delta_row "Rust toolchain" "$RUST_TOOLCHAIN (windows-msvc)" "$RUST_TOOLCHAIN (x86_64-unknown-linux-gnu)" "no (platform build only)" "—"

# --- Node: official linux-x64 tarball ----------------------------------------
NODE_TARBALL="node-v${NODE_VERSION}-linux-x64.tar.xz"
NODE_URL="https://nodejs.org/dist/v${NODE_VERSION}/${NODE_TARBALL}"
lcx_note "Node: $NODE_URL"
if [ "$DRY_RUN" = "1" ]; then
  echo "DRY-RUN: curl -fLO $NODE_URL && tar -C $INSTALL_ROOT -xJf $NODE_TARBALL"
else
  curl -fL -o "$INSTALL_ROOT/$NODE_TARBALL" "$NODE_URL" || pin_unavailable "node" "$NODE_VERSION"
  tar -C "$INSTALL_ROOT" -xJf "$INSTALL_ROOT/$NODE_TARBALL"
fi
add_delta_row "Node.js" "$NODE_VERSION (win-x64)" "$NODE_VERSION (linux-x64)" "no (platform build only)" "—"

# --- Go: official linux-amd64 tarball ----------------------------------------
GO_TARBALL="go${GO_VERSION}.linux-amd64.tar.gz"
GO_URL="https://go.dev/dl/${GO_TARBALL}"
lcx_note "Go: $GO_URL"
if [ "$DRY_RUN" = "1" ]; then
  echo "DRY-RUN: curl -fLO $GO_URL && tar -C $INSTALL_ROOT -xzf $GO_TARBALL"
else
  curl -fL -o "$INSTALL_ROOT/$GO_TARBALL" "$GO_URL" || pin_unavailable "go" "$GO_VERSION"
  rm -rf "$INSTALL_ROOT/go"
  tar -C "$INSTALL_ROOT" -xzf "$INSTALL_ROOT/$GO_TARBALL"
  "$INSTALL_ROOT/go/bin/go" version
fi
add_delta_row "Go" "$GO_VERSION (windows/amd64)" "$GO_VERSION (linux/amd64)" "no (platform build only)" "—"

# --- .NET SDK: dotnet-install.sh at the Windows-run version (SR-1 parameterized) ---
lcx_note ".NET SDK: dotnet-install.sh --version $DOTNET_SDK_VERSION (SR-1: placeholder until a Windows protocol report publishes the env-block value)"
if [ "$DRY_RUN" = "1" ]; then
  echo "DRY-RUN: curl -fL https://dot.net/v1/dotnet-install.sh | bash -s -- --version $DOTNET_SDK_VERSION --install-dir $INSTALL_ROOT/dotnet"
else
  curl -fL -o "$INSTALL_ROOT/dotnet-install.sh" https://dot.net/v1/dotnet-install.sh || pin_unavailable "dotnet-install.sh" "(download failed)"
  bash "$INSTALL_ROOT/dotnet-install.sh" --version "$DOTNET_SDK_VERSION" --install-dir "$INSTALL_ROOT/dotnet" || pin_unavailable "dotnet-sdk" "$DOTNET_SDK_VERSION"
  "$INSTALL_ROOT/dotnet/dotnet" --version
fi
add_delta_row ".NET SDK" "TBD — no published Windows protocol report yet (SR-1)" "$DOTNET_SDK_VERSION (linux-x64)" "**pending (SR-1)**" "re-baseline against the published Windows report's environment block before the bare-metal run"

# --- Temurin: 25 GA linux-x64, SR-3 fallback to 23 behind a flag ---------------
temurin_install() { # major -> 0 on success
  local major="$1"
  local api="https://api.adoptium.net/v3/binary/latest/${major}/ga/linux/x64/jdk/hotspot/normal/eclipse"
  lcx_note "Temurin: $api"
  if [ "$DRY_RUN" = "1" ]; then
    echo "DRY-RUN: curl -fL -o temurin-${major}.tar.gz '$api' && tar -C $INSTALL_ROOT -xzf temurin-${major}.tar.gz"
    return 0
  fi
  curl -fL -o "$INSTALL_ROOT/temurin-${major}.tar.gz" "$api" || return 1
  tar -C "$INSTALL_ROOT" -xzf "$INSTALL_ROOT/temurin-${major}.tar.gz" || return 1
}
if temurin_install "$TEMURIN_MAJOR"; then
  add_delta_row "Temurin JDK" "Temurin $TEMURIN_MAJOR (windows x64)" "Temurin $TEMURIN_MAJOR (linux x64; exact GA build recorded from 'java -version')" "no (platform build only; GA-build delta recorded if the Windows build is no longer published)" "—"
else
  if [ "$ALLOW_JDK_FALLBACK" = "1" ]; then
    lcx_note "Temurin $TEMURIN_MAJOR unavailable; SR-3 fallback to Temurin $TEMURIN_FALLBACK_MAJOR (flag-enabled, delta recorded)"
    temurin_install "$TEMURIN_FALLBACK_MAJOR" || pin_unavailable "temurin" "$TEMURIN_MAJOR (and SR-3 fallback $TEMURIN_FALLBACK_MAJOR)"
    add_delta_row "Temurin JDK" "Temurin $TEMURIN_MAJOR (windows x64)" "Temurin $TEMURIN_FALLBACK_MAJOR (linux x64) — SR-3 fallback" "**yes**" "inline caveat obligation on every JVM findings-validation statement (D4.3)"
  else
    pin_unavailable "temurin" "$TEMURIN_MAJOR (pass --allow-jdk-fallback to permit the SR-3 JDK-$TEMURIN_FALLBACK_MAJOR fallback, recorded as a delta)"
  fi
fi

# --- CPython: deadsnakes 3.14; D5 source-build fallback; SR-3 3.13 flag --------
lcx_note "CPython: deadsnakes PPA python${PYTHON_MINOR} (D5)"
deadsnakes_install() { # minor
  local minor="$1"
  if [ "$DRY_RUN" = "1" ]; then
    echo "DRY-RUN: sudo add-apt-repository -y ppa:deadsnakes/ppa && sudo apt-get update"
    echo "DRY-RUN: sudo apt-get install -y python${minor} python${minor}-venv python${minor}-dev"
    return 0
  fi
  sudo add-apt-repository -y ppa:deadsnakes/ppa || return 1
  sudo apt-get update || return 1
  sudo apt-get install -y "python${minor}" "python${minor}-venv" "python${minor}-dev" || return 1
  "python${minor}" -VV
}
if deadsnakes_install "$PYTHON_MINOR"; then
  add_delta_row "CPython" "3.14.x python.org PGO installer (Windows)" "deadsnakes python${PYTHON_MINOR} (micro + package version recorded post-install; build provenance from CONFIG_ARGS)" "micro delta recorded if PPA differs from the Windows run's micro" "provenance disclosed in the toolchain table (D5)"
else
  echo "setup-toolchains.sh: deadsnakes python${PYTHON_MINOR} unavailable for noble." >&2
  echo "  D5 primary fallback (spec): source-build CPython ${PYTHON_SOURCE_FALLBACK} with" >&2
  echo "    ./configure --enable-optimizations --with-lto   (record CONFIG_ARGS + compiler)" >&2
  if [ "$ALLOW_PY_FALLBACK" = "1" ]; then
    lcx_note "SR-3 fallback: deadsnakes python${PYTHON_SR3_FALLBACK_MINOR} (flag-enabled, delta recorded)"
    deadsnakes_install "$PYTHON_SR3_FALLBACK_MINOR" || pin_unavailable "cpython" "$PYTHON_MINOR (and SR-3 fallback $PYTHON_SR3_FALLBACK_MINOR)"
    add_delta_row "CPython" "3.14.x python.org PGO installer (Windows)" "deadsnakes python${PYTHON_SR3_FALLBACK_MINOR} — SR-3 fallback (spec's own fallback is a ${PYTHON_SOURCE_FALLBACK} source build, D5)" "**yes (minor)**" "inline caveat obligation on every Python findings-validation statement (D4.3)"
  else
    pin_unavailable "cpython" "$PYTHON_MINOR (use the D5 source-build fallback, or pass --allow-python-fallback for the SR-3 ${PYTHON_SR3_FALLBACK_MINOR} fallback, recorded as a delta)"
  fi
fi

# --- version-deltas.md draft ---------------------------------------------------
{
  echo "# version-deltas.md — DRAFT (WI3; finalized at publication)"
  echo
  echo "Baseline rule (D4): the delta is measured against **each published Windows report's"
  echo "environment block**, not against spec tables. SR-1: no Windows protocol report is"
  echo "published yet, so the Windows-value column below is provisional and every row must be"
  echo "re-baselined before the bare-metal run."
  echo
  echo "| component | Windows source run value (with report link) | Linux value | delta? | caveat obligation |"
  echo "|---|---|---|---|---|"
  for row in "${delta_rows[@]}"; do echo "$row"; done
  echo
  echo "Library pins (Cargo.lock, pom.xml, package-lock.json, requirements.txt, go.mod, csproj)"
  echo "are platform-independent and verified by each harness's own reproduce path (D4.1); they"
  echo "appear here only if an install proves impossible at the pinned version."
  echo
  echo "Zero deltas is the target; this table is published even when empty."
} > "$DELTAS"
lcx_note "version-deltas.md draft -> $DELTAS"

if [ "$DRY_RUN" = "1" ]; then
  lcx_note "dry-run complete — nothing installed."
else
  lcx_note "toolchain installs complete. Add to PATH for the session:"
  echo "  export PATH=\"$INSTALL_ROOT/node-v${NODE_VERSION}-linux-x64/bin:$INSTALL_ROOT/go/bin:$INSTALL_ROOT/dotnet:\$PATH\""
  echo "  export DOTNET_ROOT=\"$INSTALL_ROOT/dotnet\""
  echo "  export JAVA_HOME=\"\$(ls -d $INSTALL_ROOT/jdk-* 2>/dev/null | head -n 1)\"; export PATH=\"\$JAVA_HOME/bin:\$PATH\""
fi
