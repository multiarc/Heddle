# run-benchmarks.ps1 -- Phase 6 (Go) reproduce-it-yourself entry point.
#
# Performs, in order (harness-and-measurement.md, D9):
#   1. Toolchain version assertions (go1.26.5; templ v0.3.1020 via `go tool`, per D3).
#   2. `go tool templ generate` + `git diff --exit-code -- *_templ.go` (regeneration
#      freshness -- committed generated code must match the pinned generator).
#   3. `go vet ./...`
#   4. `go test ./...` (gates + unit tests; TestMain gates before anything can time).
#   5. Prebuild: `go test -c -o bench.exe ./suites`.
#   6. Two timed invocations at High process priority via `cmd /c start /high /wait /b`
#      (priority set at process creation, no race): BenchmarkRender, then the
#      BenchmarkColdParse sidebar. Defaults: -test.count=20, -test.benchtime=1s.
#   7. benchstat over each output.
#
# Windows PowerShell 5.1 compatible (no pipeline chain operators, ASCII only).
param(
    [int]$Count = 20,
    [string]$BenchTime = "1s",
    [switch]$VersionCheckOnly
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

function Assert-LastExit([string]$step) {
    if ($LASTEXITCODE -ne 0) {
        Write-Host ("FAIL: " + $step + " (exit code " + $LASTEXITCODE + ")")
        exit 1
    }
}

# --- 1. Toolchain version assertions (D3) ----------------------------------------------------
$goVersion = (& go version) -join " "
Assert-LastExit "go version"
if ($goVersion -notmatch "go1\.26\.5") {
    Write-Host ("FAIL: pinned toolchain is go1.26.5; found: " + $goVersion)
    exit 1
}
$templVersion = ((& go tool templ version) -join " ").Trim()
Assert-LastExit "go tool templ version"
if ($templVersion -ne "v0.3.1020") {
    Write-Host ("FAIL: pinned templ CLI is v0.3.1020; found: " + $templVersion)
    exit 1
}
Write-Host ("OK: " + $goVersion + "; templ " + $templVersion)
if ($VersionCheckOnly) {
    Write-Host "Version asserts passed; exiting (VersionCheckOnly)."
    exit 0
}

# --- 2. Regeneration freshness ---------------------------------------------------------------
& go tool templ generate
Assert-LastExit "go tool templ generate"
& git diff --exit-code -- "*_templ.go"
if ($LASTEXITCODE -ne 0) {
    Write-Host "FAIL: committed *_templ.go does not match the pinned generator's output."
    exit 1
}

# --- 3. Vet ----------------------------------------------------------------------------------
& go vet ./...
Assert-LastExit "go vet"

# --- 4. Gates + unit tests -------------------------------------------------------------------
& go test ./...
Assert-LastExit "go test (gates + unit tests)"

# --- 5. Prebuild -----------------------------------------------------------------------------
& go test -c -o bench.exe ./suites
Assert-LastExit "go test -c (prebuild)"

# --- 6. Timed invocations at High priority ---------------------------------------------------
if (-not (Test-Path "results")) {
    New-Item -ItemType Directory -Path "results" | Out-Null
}
$stamp = Get-Date -Format "yyyyMMdd"
$renderOut = "results\bench-render-" + $stamp + ".txt"
$coldOut = "results\bench-coldparse-" + $stamp + ".txt"

# Carets are cmd.exe escape characters, so the regex anchors are written ^^ below.
$renderCmd = "start /high /wait /b bench.exe -test.run ^^$ -test.bench ^^BenchmarkRender$ " +
    "-test.benchtime " + $BenchTime + " -test.count " + $Count + " > " + $renderOut + " 2>&1"
& cmd /c $renderCmd
Assert-LastExit "BenchmarkRender timed run"
Write-Host ("BenchmarkRender run written to " + $renderOut)

$coldCmd = "start /high /wait /b bench.exe -test.run ^^$ -test.bench ^^BenchmarkColdParse$ " +
    "-test.benchtime " + $BenchTime + " -test.count " + $Count + " > " + $coldOut + " 2>&1"
& cmd /c $coldCmd
Assert-LastExit "BenchmarkColdParse timed run"
Write-Host ("BenchmarkColdParse run written to " + $coldOut)
Write-Host "Note: both stdlib cold rows are 'cold parse only' (html/template escape-compilation is lazy)."
Write-Host "Note: templ sidebar cells are 'AOT - no runtime parse (compiled by go generate)'."

# --- 7. benchstat ----------------------------------------------------------------------------
$benchstatRender = "results\benchstat-render-" + $stamp + ".txt"
$benchstatCold = "results\benchstat-coldparse-" + $stamp + ".txt"
& go tool benchstat $renderOut | Out-File -Encoding utf8 $benchstatRender
Assert-LastExit "benchstat (render)"
& go tool benchstat $coldOut | Out-File -Encoding utf8 $benchstatCold
Assert-LastExit "benchstat (coldparse)"

Write-Host ("Done. benchstat summaries: " + $benchstatRender + ", " + $benchstatCold)
