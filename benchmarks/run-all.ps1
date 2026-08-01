# run-all.ps1 -- Windows master runner for the cross-stack benchmark program.
#
# One command runs every ecosystem's gates and then its measurement (or smoke) pass
# with the parameters the phase specs make normative:
#   .NET    docs/spec/cross-stack-benchmarks/phase-1-cross-stack-foundation/metrics-protocol.md
#   Rust    docs/spec/cross-stack-benchmarks/phase-2-rust/README.md (D9/WI10)
#   JVM     docs/spec/cross-stack-benchmarks/phase-3-jvm/harness-and-jmh.md
#   JS      docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md (+ run.ps1)
#   Python  docs/spec/cross-stack-benchmarks/phase-5-python/harness.md
#   Go      docs/spec/cross-stack-benchmarks/phase-6-go/harness-and-measurement.md (+ run-benchmarks.ps1)
# The Linux counterpart is benchmarks/linux-crosscheck/run-all.sh; this script keeps the
# same phase order (gates first, stop on first red; then dotnet -> rust -> jvm -> js ->
# python -> go) so both sides of the cross-check read the same way.
#
# usage (from the repo root):
#   powershell -ExecutionPolicy Bypass -File benchmarks\run-all.ps1            # full measurement
#   powershell -ExecutionPolicy Bypass -File benchmarks\run-all.ps1 -Smoke     # short functional pass
#   ... -Ecosystem rust,go        # subset
#   ... -OutDir E:\bench-out      # artifact/log destination (no spaces in the path)
#   ... -Budget baseline          # ~30 min/ecosystem instead of the ~10 min default
#   ... -JsPasses 24              # more JS repeat passes than the profile default
#
# Windows PowerShell 5.1 compatible: no pipeline chain operators, no ternary, ASCII only.

[CmdletBinding()]
param(
    [switch]$Smoke,

    [ValidateSet('dotnet', 'rust', 'jvm', 'js', 'python', 'go')]
    [string[]]$Ecosystem = @('dotnet', 'rust', 'jvm', 'js', 'python', 'go'),

    [string]$OutDir = '',

    # Ledger E6 measurement budget. Every harness's committed source/script default IS 'short',
    # so a bare single-harness invocation is the ~10 min shape; 'baseline' layers CLI overrides
    # on top -- roughly 3x the capture samples and one extra warmup run -- for ~30 min each.
    # Named -Budget, not -Profile: PowerShell already has an automatic variable of that
    # name (the profile script path), and a parameter would shadow it inside this script.
    [ValidateSet('short', 'baseline')]
    [string]$Budget = 'short',

    # JS repeat passes per render track, aggregated by bench/aggregate.mjs into the published
    # artifact plus the D13 verdict. 0 means "take the profile default" (18 short / 54 baseline);
    # 5 is D13's floor and aggregate.mjs refuses fewer.
    [ValidateRange(0, 200)]
    [int]$JsPasses = 0
)

$ErrorActionPreference = 'Continue'

# --- Measurement budget profiles (ledger E6) -------------------------------------------------
#
# Nothing here changes WHAT is measured, only how many times. The committed source/script
# defaults are the 'short' shape; 'baseline' layers CLI overrides for roughly 3x the capture
# samples plus one extra warmup run. .NET is the exception to that ratio: it is overhead-bound
# (one process per method, plus JIT and MemoryDiagnoser), so its baseline values are simply
# BenchmarkDotNet's own adaptive-default shape -- the regime the protocol pinned before E6.
if ($Budget -eq 'baseline') {
    $dotnetProfileArgs = ' --warmupCount 7 --iterationCount 15'
    $rustProfileArgs   = ' --warm-up-time 4 --measurement-time 30'
    $jmhProfileArgs    = ' -wi 2 -i 9'
    $pyValuesArgs      = ' --values 9 --warmups 2'
    $pyColdArgs        = ''            # pyperf default 20 processes
    $goProfileArgs     = ' -Count 42'
    $profileJsPasses   = 54
}
else {
    $dotnetProfileArgs = ''            # [ShortRunJob] in source: LaunchCount 1 / W3 / I3
    $rustProfileArgs   = ''            # source: warmup 3 s, measurement 10 s
    $jmhProfileArgs    = ''            # annotations: Fork 3, W 1x2s, M 3x1s
    $pyValuesArgs      = ''            # pyperf default 3 values
    $pyColdArgs        = ' --processes 7'
    $goProfileArgs     = ''            # script default -Count 14
    $profileJsPasses   = 18
}
if ($JsPasses -eq 0) { $JsPasses = $profileJsPasses }
if ($JsPasses -lt 5) {
    Write-Host 'ERROR: -JsPasses must be at least 5 (Phase 4 D13 verdict floor).'
    exit 1
}

# --- Layout ---------------------------------------------------------------------------------
$BenchRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $BenchRoot
if ($OutDir -eq '') {
    $OutDir = Join-Path $BenchRoot ('out\windows-run-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
}
if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $OutDir = Join-Path (Get-Location).Path $OutDir
}
if ($OutDir -match ' ') {
    Write-Host 'ERROR: -OutDir must not contain spaces (paths are passed through cmd.exe unquoted).'
    exit 1
}
$LogDir = Join-Path $OutDir 'logs'
New-Item -ItemType Directory -Force -Path $OutDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

# Fixed program order (anchor-first, then the program's priority order -- same as Linux).
$AllOrder = @('dotnet', 'rust', 'jvm', 'js', 'python', 'go')
$Selected = @()
foreach ($e in $AllOrder) {
    if ($Ecosystem -contains $e) { $Selected += $e }
}

# The eight protocol suites (phase 1 metrics-protocol: 'The protocol's first exercise'), named
# after the workloads they measure. Each is one `bench-crossstack --filter` step, so a suite gets
# its own log, its own exit code and its own place to resume from.
$DotnetSuites = @(
    'ComposedPageBenchmarks',
    'TrivialSubstitutionBenchmarks',
    'LargeLoopBenchmarks',
    'MixedPageBenchmarks',
    'ConditionalHeavyBenchmarks',
    'FragmentHeavyBenchmarks',
    'FortunesEncodedBenchmarks',
    'EncodedLoopBenchmarks'
)

$DotnetDir = Join-Path $BenchRoot 'dotnet'
$RustDir = Join-Path $BenchRoot 'rust'
$JvmDir = Join-Path $BenchRoot 'jvm'
$JsDir = Join-Path $BenchRoot 'js'
$PyDir = Join-Path $BenchRoot 'python'
$GoDir = Join-Path $BenchRoot 'go'
$VenvPy = Join-Path $PyDir '.venv\Scripts\python.exe'

# --- Step machinery -------------------------------------------------------------------------
$script:Results = New-Object System.Collections.ArrayList
$script:Failed = $false

function Invoke-Step {
    param(
        [string]$Eco,
        [string]$Phase,      # gate | measure | smoke | copy
        [string]$Name,
        [string]$WorkDir,
        [string]$Command,    # a cmd.exe command line; stderr is merged inside cmd
        [switch]$NonFatal
    )
    $logName = (($Eco + '-' + $Name) -replace '[^A-Za-z0-9\.\-_]', '_') + '.log'
    $logPath = Join-Path $LogDir $logName
    Write-Host ''
    Write-Host ('== [' + $Eco + '/' + $Phase + '] ' + $Name) -ForegroundColor Cyan
    Write-Host ('   dir: ' + $WorkDir)
    Write-Host ('   cmd: ' + $Command)
    Write-Host ('   log: ' + $logPath)

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $code = 1
    Push-Location $WorkDir
    $writer = New-Object System.IO.StreamWriter($logPath, $false, (New-Object System.Text.UTF8Encoding($false)))
    try {
        $writer.WriteLine('# step: ' + $Eco + '/' + $Name)
        $writer.WriteLine('# dir:  ' + $WorkDir)
        $writer.WriteLine('# cmd:  ' + $Command)
        $writer.WriteLine('# date: ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
        $writer.WriteLine('')
        # cmd /c merges stderr before PowerShell sees it (avoids PS 5.1 NativeCommandError
        # wrapping); each line is teed to the console and the step log.
        cmd /c ($Command + ' 2>&1') | ForEach-Object {
            $line = [string]$_
            $writer.WriteLine($line)
            Write-Host $line
        }
        $code = $LASTEXITCODE
        $writer.WriteLine('')
        $writer.WriteLine('# exit: ' + $code)
    }
    catch {
        $msg = ($_ | Out-String)
        $writer.WriteLine('# launcher exception: ' + $msg)
        Write-Host ('launcher exception: ' + $msg)
        $code = 1
    }
    finally {
        $writer.Close()
        Pop-Location
    }
    $sw.Stop()

    $status = 'OK'
    if ($code -ne 0) {
        if ($NonFatal) { $status = 'WARN' } else { $status = 'FAIL'; $script:Failed = $true }
    }
    [void]$script:Results.Add([pscustomobject]@{
        Ecosystem = $Eco
        Phase     = $Phase
        Step      = $Name
        ExitCode  = $code
        Seconds   = [math]::Round($sw.Elapsed.TotalSeconds, 1)
        Status    = $status
    })
    if ($status -eq 'FAIL') {
        Write-Host ('== FAIL [' + $Eco + '/' + $Name + '] exit code ' + $code) -ForegroundColor Red
    }
    elseif ($status -eq 'WARN') {
        Write-Host ('== WARN (non-fatal) [' + $Eco + '/' + $Name + '] exit code ' + $code) -ForegroundColor Yellow
    }
    else {
        Write-Host ('== OK [' + $Eco + '/' + $Name + '] ' + [math]::Round($sw.Elapsed.TotalSeconds, 1) + ' s') -ForegroundColor Green
    }
    return $code
}

function Copy-Artifacts {
    param([string]$Eco, [string]$Name, [string]$Source, [string]$Dest)
    if (-not (Test-Path $Source)) {
        Write-Host ('   (no artifacts at ' + $Source + ' -- skipping copy)')
        return
    }
    try {
        New-Item -ItemType Directory -Force -Path $Dest | Out-Null
        Copy-Item -Recurse -Force -Path (Join-Path $Source '*') -Destination $Dest
        Write-Host ('   artifacts: ' + $Source + ' -> ' + $Dest)
        [void]$script:Results.Add([pscustomobject]@{
            Ecosystem = $Eco; Phase = 'copy'; Step = $Name; ExitCode = 0; Seconds = 0; Status = 'OK'
        })
    }
    catch {
        Write-Host ('   WARN: artifact copy failed: ' + $_) -ForegroundColor Yellow
        [void]$script:Results.Add([pscustomobject]@{
            Ecosystem = $Eco; Phase = 'copy'; Step = $Name; ExitCode = 1; Seconds = 0; Status = 'WARN'
        })
    }
}

function Get-ToolVersion {
    param([string]$CommandLine)
    $out = cmd /c ($CommandLine + ' 2>&1')
    if ($LASTEXITCODE -ne 0 -and (-not $out)) { return 'NOT FOUND' }
    if ($null -eq $out) { return 'NOT FOUND' }
    return (($out | Select-Object -First 1) -join ' ').Trim()
}

# --- Preamble: toolchain versions, pin deltas (SR-3 style: warn, never fail) ----------------
$PreambleLog = Join-Path $LogDir 'preamble.log'
$Preamble = New-Object System.Collections.ArrayList
function Note {
    param([string]$Text, [string]$Color = 'Gray')
    Write-Host $Text -ForegroundColor $Color
    [void]$Preamble.Add($Text)
}

$mode = 'MEASUREMENT (spec protocol shapes)'
if ($Smoke) { $mode = 'SMOKE (short functional flags -- results carry NO measurement validity)' }

Note '=============================================================================='
Note ' Heddle cross-stack benchmarks -- Windows master runner'
Note ('   date:       ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Note ('   mode:       ' + $mode)
Note ('   budget:     ' + $Budget + '   (E6 measurement budget)')
Note ('   ecosystems: ' + ($Selected -join ', '))
Note ('   out dir:    ' + $OutDir)
Note '=============================================================================='
Note ''
Note '-- Toolchain versions (spec pins in parentheses; deltas WARN, never fail: SR-3) --'

$vDotnet = Get-ToolVersion 'dotnet --version'
Note ('   dotnet SDK : ' + $vDotnet + '   (protocol records the exact SDK; 10.0.302 was the observed line)')

$vCargo = Get-ToolVersion 'cargo --version'
$vRustc = Get-ToolVersion 'rustc --version'
Note ('   cargo      : ' + $vCargo)
Note ('   rustc      : ' + $vRustc + '   (pin: 1.97.1)')
if ($vRustc -notmatch '1\.97\.1') { Note '   WARN: rustc differs from the pinned 1.97.1 (record as a version delta).' 'Yellow' }

$vJava = Get-ToolVersion 'java -version'
Note ('   java       : ' + $vJava + '   (pin: Temurin 25)')
$JdkMajor = 0
if ($vJava -match 'version "(\d+)') { $JdkMajor = [int]$Matches[1] }
if ($JdkMajor -ne 25) { Note ('   WARN: installed JDK major is ' + $JdkMajor + ', pin is Temurin 25 (record as a version delta).') 'Yellow' }
$vMvn = Get-ToolVersion 'mvn --version'
Note ('   maven      : ' + $vMvn + '   (harness builds use its own mvnw.cmd wrapper)')

$vNode = Get-ToolVersion 'node --version'
$vNpm = Get-ToolVersion 'npm --version'
Note ('   node       : ' + $vNode + '   (pin: v24.18.0)')
Note ('   npm        : ' + $vNpm)
$NodeIsPinned = ($vNode.Trim() -eq 'v24.18.0')
if (-not $NodeIsPinned) { Note '   WARN: node differs from the pinned v24.18.0 (npm ci will run with --engine-strict=false; record as a version delta).' 'Yellow' }

$vPy = 'NOT FOUND'
if (Test-Path $VenvPy) { $vPy = Get-ToolVersion ($VenvPy + ' --version') }
else { $vPy = Get-ToolVersion 'python --version' }
Note ('   python     : ' + $vPy + '   (pin: CPython 3.14.6; harness venv at benchmarks\python\.venv)')
if ($vPy -notmatch '3\.14\.6') { Note '   WARN: python differs from the pinned CPython 3.14.6 (record as a version delta).' 'Yellow' }

$vGo = Get-ToolVersion 'go version'
Note ('   go         : ' + $vGo + '   (pin: go1.26.x; run-benchmarks.ps1 hard-asserts go1.26.5)')
if ($vGo -notmatch 'go1\.26\.') { Note '   WARN: go differs from the pinned 1.26.x line (record as a version delta).' 'Yellow' }
Push-Location $GoDir
$vTempl = Get-ToolVersion 'go tool templ version'
Pop-Location
Note ('   templ      : ' + $vTempl + '   (pin: v0.3.1020, via go tool)')

# --- toolchain.json: the pin deltas, machine-readable -----------------------------------------
# The notes above are for a human reading the step log. A published report also needs the deltas
# in its environment block, and reconstructing them by hand from artifact metadata after the fact
# is error-prone -- an earlier report had to do exactly that. benchmarks/report/consolidate.py
# reads this file and generates the pin-drift table from it. SR-3 posture is unchanged: drift is
# recorded, never fatal.
$toolchain = [ordered]@{
    '.NET SDK' = @{ pin = '(protocol records the observed line)'; actual = $vDotnet; drift = $false }
    'Rust'     = @{ pin = '1.97.1';     actual = $vRustc; drift = ($vRustc -notmatch '1\.97\.1') }
    'JDK'      = @{ pin = 'Temurin 25'; actual = $vJava;  drift = ($JdkMajor -ne 25) }
    'Node.js'  = @{ pin = 'v24.18.0';   actual = $vNode;  drift = (-not $NodeIsPinned) }
    'CPython'  = @{ pin = '3.14.6';     actual = $vPy;    drift = ($vPy -notmatch '3\.14\.6') }
    'Go'       = @{ pin = 'go1.26.x';   actual = $vGo;    drift = ($vGo -notmatch 'go1\.26\.') }
    'templ'    = @{ pin = 'v0.3.1020';  actual = $vTempl; drift = ($vTempl -notmatch 'v0\.3\.1020') }
}
$toolchainPath = Join-Path $OutDir 'toolchain.json'
# -Depth 3 keeps the nested pin/actual/drift objects; the default (2) would stringify them.
$toolchain | ConvertTo-Json -Depth 3 | Set-Content -Path $toolchainPath -Encoding utf8NoBOM
Note ('   toolchain.json written to ' + $toolchainPath + ' (pin deltas, machine-readable)')

# Goldens-rewrite hazard: a stray dotnet-hosted watcher (Heddle demo/docs tooling) touching
# the working tree during export/verify would dirty the corpus manifest. Best-effort check:
# list running dotnet PIDs so the operator can eyeball them.
$dotnetProcs = @(Get-Process -Name 'dotnet' -ErrorAction SilentlyContinue)
if ($dotnetProcs.Count -gt 0) {
    Note ''
    Note ('   WARN: ' + $dotnetProcs.Count + ' dotnet process(es) are running (PIDs: ' + (($dotnetProcs | ForEach-Object { $_.Id }) -join ', ') + ').') 'Yellow'
    Note '   If any is a Heddle file-watcher (demo/docs tooling), stop it before measuring:' 'Yellow'
    Note '   a watcher rewriting goldens mid-run is a corpus-freshness hazard (verify-corpus would go red).' 'Yellow'
}
Note ''
$Preamble | Out-File -Encoding utf8 $PreambleLog

# --- Phase 1: GATES (all selected ecosystems, stop on first red) ----------------------------
Write-Host '################################################################################'
Write-Host '## PHASE 1: GATES (stop on first red -- a red gate must be triaged before'
Write-Host '##          anything later runs; same rule as linux-crosscheck/run-all.sh)'
Write-Host '################################################################################'

$MvnRelease = ''
if ($JdkMajor -gt 0 -and $JdkMajor -lt 25) {
    $MvnRelease = ' -Dmaven.compiler.release=23'
    Write-Host ('NOTE: JDK ' + $JdkMajor + ' < 25 -- passing' + $MvnRelease + ' to the JVM build (pom pins release=25).') -ForegroundColor Yellow
}

$NpmCiSuffix = ''
if (-not $NodeIsPinned) { $NpmCiSuffix = ' --engine-strict=false' }

foreach ($eco in $Selected) {
    switch ($eco) {
        'dotnet' {
            # The full cell registry: byte gate on the controlled track, functional verifier on the
            # idiomatic one, security floor on the encoded workloads, plus the materialisation
            # trailer and the precompiled-coverage statement.
            [void](Invoke-Step -Eco 'dotnet' -Phase 'gate' -Name 'gate' -WorkDir $DotnetDir `
                -Command 'dotnet run -c Release -- gate')
            if ($script:Failed) { break }
            # The gate itself, gated: harness self-checks including the six-technique differential.
            [void](Invoke-Step -Eco 'dotnet' -Phase 'gate' -Name 'gate-selftest' -WorkDir $DotnetDir `
                -Command 'dotnet run -c Release -- selftest')
            if ($script:Failed) { break }
            [void](Invoke-Step -Eco 'dotnet' -Phase 'gate' -Name 'gate-verify-corpus' -WorkDir $DotnetDir `
                -Command 'dotnet run -c Release -- verify-corpus')
        }
        'rust' {
            [void](Invoke-Step -Eco 'rust' -Phase 'gate' -Name 'gate' -WorkDir $RustDir `
                -Command 'cargo run --release --bin gate')
        }
        'jvm' {
            [void](Invoke-Step -Eco 'jvm' -Phase 'gate' -Name 'gate-build-verify' -WorkDir $JvmDir `
                -Command ('.\mvnw.cmd -q clean verify' + $MvnRelease))
        }
        'js' {
            [void](Invoke-Step -Eco 'js' -Phase 'gate' -Name 'npm-ci' -WorkDir $JsDir `
                -Command ('npm ci' + $NpmCiSuffix))
            if ($script:Failed) { break }
            [void](Invoke-Step -Eco 'js' -Phase 'gate' -Name 'selftest' -WorkDir $JsDir `
                -Command 'npm run selftest')
            if ($script:Failed) { break }
            [void](Invoke-Step -Eco 'js' -Phase 'gate' -Name 'gate' -WorkDir $JsDir `
                -Command 'npm run gate')
        }
        'python' {
            if (-not (Test-Path $VenvPy)) {
                [void](Invoke-Step -Eco 'python' -Phase 'gate' -Name 'venv-create' -WorkDir $PyDir `
                    -Command 'python -m venv .venv')
                if ($script:Failed) { break }
            }
            [void](Invoke-Step -Eco 'python' -Phase 'gate' -Name 'pip-install' -WorkDir $PyDir `
                -Command ($VenvPy + ' -m pip install -r requirements.txt'))
            if ($script:Failed) { break }
            [void](Invoke-Step -Eco 'python' -Phase 'gate' -Name 'selftest' -WorkDir $PyDir `
                -Command ($VenvPy + ' -m runner.selftest'))
            if ($script:Failed) { break }
            [void](Invoke-Step -Eco 'python' -Phase 'gate' -Name 'gate-all' -WorkDir $PyDir `
                -Command ($VenvPy + ' -m runner.gate_all'))
        }
        'go' {
            [void](Invoke-Step -Eco 'go' -Phase 'gate' -Name 'gate' -WorkDir $GoDir `
                -Command 'go test ./suites')
        }
    }
    if ($script:Failed) { break }
}

function Write-Summary {
    Write-Host ''
    Write-Host '################################################################################'
    Write-Host '## SUMMARY'
    Write-Host '################################################################################'
    $table = $script:Results | Format-Table -AutoSize Ecosystem, Phase, Step, ExitCode, Seconds, Status | Out-String
    Write-Host $table
    $summaryPath = Join-Path $OutDir 'summary.txt'
    $header = @(
        ('mode:       ' + $mode),
        ('budget:     ' + $Budget + '   (E6 measurement budget)'),
        ('ecosystems: ' + ($Selected -join ', ')),
        ('finished:   ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')),
        ''
    )
    ($header + $table) | Out-File -Encoding utf8 $summaryPath
    Write-Host ('summary: ' + $summaryPath)
    Write-Host ('logs:    ' + $LogDir)
}

if ($script:Failed) {
    Write-Host ''
    Write-Host 'A GATE IS RED. Stopping before any measurement (triage the gate first).' -ForegroundColor Red
    Write-Summary
    exit 1
}

Write-Host ''
Write-Host 'All selected gates are green.' -ForegroundColor Green

# --- Phase 2: MEASUREMENT (or smoke) --------------------------------------------------------
Write-Host ''
Write-Host '################################################################################' -ForegroundColor Yellow
Write-Host '## MACHINE-STATE RULES (phase specs; read before trusting any number)' -ForegroundColor Yellow
Write-Host '##  * Protocol machine only: the AMD Ryzen 9 9950X / Windows 11 box of the' -ForegroundColor Yellow
Write-Host '##    published runs (metrics-protocol.md, Q1.6). Numbers from any other' -ForegroundColor Yellow
Write-Host '##    machine are not comparable and must not be merged into reports.' -ForegroundColor Yellow
Write-Host '##  * Quiet machine: close foreground applications, browsers, editors with' -ForegroundColor Yellow
Write-Host '##    background indexing, and any other builds/watchers for the duration.' -ForegroundColor Yellow
Write-Host '##  * Power: AC power, High Performance power plan; no sleep/hibernate timers' -ForegroundColor Yellow
Write-Host '##    that could fire mid-run.' -ForegroundColor Yellow
Write-Host '##  * Per-harness stability settings (High priority classes, --affinity=4,' -ForegroundColor Yellow
Write-Host '##    elevated shell for pyperf) are applied by the steps below and must be' -ForegroundColor Yellow
Write-Host '##    recorded in the run report''s environment block.' -ForegroundColor Yellow
if ($Smoke) {
    Write-Host '##  SMOKE MODE: the above is informational -- smoke results carry no validity.' -ForegroundColor Yellow
}
Write-Host '################################################################################' -ForegroundColor Yellow
Write-Host ''
Write-Host '################################################################################'
if ($Smoke) { Write-Host '## PHASE 2: SMOKE (functional pass; failures are recorded, run continues)' }
else { Write-Host '## PHASE 2: MEASUREMENT (failures are recorded, run continues)' }
Write-Host '################################################################################'

$measurePhase = 'measure'
if ($Smoke) { $measurePhase = 'smoke' }

foreach ($eco in $Selected) {
    switch ($eco) {
        'dotnet' {
            # Phase 1 protocol shape: Release, net10.0, the harness's own ShortRun default,
            # MemoryDiagnoser via suite attributes; one --filter run per protocol suite. Each suite
            # measures BOTH fairness tracks (the Track parameter), which is why the .NET measure
            # phase is about twice the length it was when the leg was controlled-track only.
            foreach ($suite in $DotnetSuites) {
                $bdnCmd = 'dotnet run -c Release -- bench-crossstack --filter *' + $suite + '*'
                if ($Smoke) { $bdnCmd = $bdnCmd + ' --job Dry' }
                elseif ($dotnetProfileArgs -ne '') { $bdnCmd = $bdnCmd + $dotnetProfileArgs }
                [void](Invoke-Step -Eco 'dotnet' -Phase $measurePhase -Name ('suite-' + $suite) -WorkDir $DotnetDir -Command $bdnCmd)
            }
            # The three sidebars. None of them contributes a cross-stack row -- techniques is Heddle
            # against itself, cold measures a different step of the same engines, and internal pins
            # engine properties no other ecosystem has an analogue for -- so they run last, where a
            # failure in them cannot mask a missing comparison row.
            foreach ($sidebar in @('bench-techniques', 'bench-cold', 'bench-internal')) {
                $cmd = 'dotnet run -c Release -- ' + $sidebar
                if ($Smoke) { $cmd = $cmd + ' --job Dry' }
                elseif ($dotnetProfileArgs -ne '') { $cmd = $cmd + $dotnetProfileArgs }
                [void](Invoke-Step -Eco 'dotnet' -Phase $measurePhase -Name $sidebar -WorkDir $DotnetDir -Command $cmd)
            }

            Copy-Artifacts -Eco 'dotnet' -Name 'copy-bdn-artifacts' `
                -Source (Join-Path $DotnetDir 'BenchmarkDotNet.Artifacts') -Dest (Join-Path $OutDir 'dotnet')
        }
        'rust' {
            # Phase 2 D9 via the WI5 finding (mirrored from linux-crosscheck/run-rust.sh):
            # the lib target does not set bench = false, so the three Criterion bench
            # targets are selected explicitly; sources are untouched.
            $benchTargets = '--bench controlled --bench idiomatic --bench cold'
            if ($Smoke) {
                [void](Invoke-Step -Eco 'rust' -Phase $measurePhase -Name 'criterion-test-pass' -WorkDir $RustDir `
                    -Command ('cargo bench ' + $benchTargets + ' -- --test'))
            }
            else {
                [void](Invoke-Step -Eco 'rust' -Phase $measurePhase -Name 'criterion-bench' -WorkDir $RustDir `
                    -Command ('cargo bench ' + $benchTargets + ' -- --noplot' + $rustProfileArgs))
                # --out lands the D13 artifact straight in the run dir: Copy-Artifacts only handles
                # directories, and before this the report existed nowhere but the step log.
                [void](New-Item -ItemType Directory -Force -Path (Join-Path $OutDir 'rust'))
                [void](Invoke-Step -Eco 'rust' -Phase $measurePhase -Name 'alloc-report' -WorkDir $RustDir `
                    -Command ('cargo run --release --features alloc-count --bin alloc_report -- --out "' + (Join-Path $OutDir 'rust\alloc-report.txt') + '"'))
                # summarize reads heddle-reference.toml; while the Phase 1 Windows reference
                # rows are pending it fails loudly by design -- captured, non-fatal.
                [void](Invoke-Step -Eco 'rust' -Phase $measurePhase -Name 'summarize' -WorkDir $RustDir -NonFatal `
                    -Command 'cargo run --release --bin summarize')
            }
            Copy-Artifacts -Eco 'rust' -Name 'copy-criterion' `
                -Source (Join-Path $RustDir 'target\criterion') -Dest (Join-Path $OutDir 'rust\criterion')
        }
        'jvm' {
            $jvmOut = Join-Path $OutDir 'jvm'
            New-Item -ItemType Directory -Force -Path $jvmOut | Out-Null
            $rff = Join-Path $jvmOut 'jmh-result.json'
            if ($Smoke) {
                [void](Invoke-Step -Eco 'jvm' -Phase $measurePhase -Name 'jmh-smoke' -WorkDir $JvmDir `
                    -Command ('java -jar target\benchmarks.jar -f 1 -wi 1 -i 1 -w 1s -r 1s -foe true -prof gc -rf json -rff ' + $rff))
            }
            else {
                Write-Host 'NOTE: JMH regime = annotations (Fork 3, 1x2s warmup, 3x1s measure)' -ForegroundColor Yellow
                Write-Host ("      + budget '" + $Budget + "' overrides:" + $jmhProfileArgs) -ForegroundColor Yellow
                Write-Host '      Expect ~9 min (short) / ~23 min (baseline); it was 4.5 h before ledger E6.' -ForegroundColor Yellow
                [void](Invoke-Step -Eco 'jvm' -Phase $measurePhase -Name 'jmh-full' -WorkDir $JvmDir `
                    -Command ('java -jar target\benchmarks.jar' + $jmhProfileArgs + ' -prof gc -rf json -rff ' + $rff))
            }
        }
        'js' {
            # Phase 4 shapes via the committed launcher run.ps1 (High priority class,
            # node --expose-gc --allow-natives-syntax, stdout captured to artifacts/).
            #
            # The two render tracks run $JsPasses times each and are aggregated (ledger E6).
            # mitata exposes no per-cell time budget -- B.run() builds its own options object, so
            # min_cpu_time (642 ms) is unreachable from the public API -- which left this
            # ecosystem measuring 32 cells in 31 s against 15-31 s/cell everywhere else. Its
            # share of the uniform budget is spent on independent processes instead: run.ps1
            # -Repeat N, then bench/aggregate.mjs medians the per-pass avg into
            # artifacts/<track>.json and emits the D13 verdict. The stability procedure IS the
            # measurement now, rather than a separate gate someone has to remember -- which is
            # how the withdrawn 2026-07-22 run shipped JS numbers with no RSD verdict at all.
            #
            # cold-compile stays a single pass: compile-dominated D10 sidebar, not a protocol
            # cell, so repeating it buys a verdict for numbers no ranking consumes.
            $runPs1 = 'powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1'
            foreach ($jsTrack in @('controlled', 'idiomatic')) {
                if ($Smoke) {
                    [void](Invoke-Step -Eco 'js' -Phase $measurePhase -Name ('bench-' + $jsTrack) -WorkDir $JsDir `
                        -Command ($runPs1 + ' bench/' + $jsTrack + '.mjs'))
                }
                else {
                    [void](Invoke-Step -Eco 'js' -Phase $measurePhase -Name ('bench-' + $jsTrack + '-x' + $JsPasses) -WorkDir $JsDir `
                        -Command ($runPs1 + ' bench/' + $jsTrack + '.mjs -Repeat ' + $JsPasses))
                }
            }
            [void](Invoke-Step -Eco 'js' -Phase $measurePhase -Name 'bench-cold-compile' -WorkDir $JsDir `
                -Command ($runPs1 + ' bench/cold-compile.mjs'))
            Copy-Artifacts -Eco 'js' -Name 'copy-artifacts' `
                -Source (Join-Path $JsDir 'artifacts') -Dest (Join-Path $OutDir 'js')
        }
        'python' {
            # Phase 5 D8: five pyperf Runner scripts, library defaults, --affinity=4, JSON
            # outputs, from an ELEVATED shell (psutil REALTIME_PRIORITY_CLASS path).
            $isElevated = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
            if (-not $isElevated) {
                Write-Host 'WARN: shell is NOT elevated -- pyperf''s REALTIME_PRIORITY_CLASS elevation' -ForegroundColor Yellow
                Write-Host '      will be silently skipped (AccessDenied swallowed). A measurement run' -ForegroundColor Yellow
                Write-Host '      must use an elevated PowerShell (Phase 5 D8).' -ForegroundColor Yellow
            }
            $pyOut = Join-Path $OutDir 'python'
            New-Item -ItemType Directory -Force -Path $pyOut | Out-Null
            $pySmokeArgs = ''
            if ($Smoke) { $pySmokeArgs = ' --debug-single-value' }
            # The four render scripts keep pyperf's default 20x3x1 (Phase 5 D8) -- at ~9 min for
            # the 32 protocol cells they already sit inside ledger E6's uniform budget. The
            # cold-compile sidebar does not: at defaults it cost 288 s, a third of the
            # ecosystem's time for a non-comparable sidebar (D10), so it runs with 7 processes.
            $pyScripts = @('bench_jinja2_controlled', 'bench_jinja2_idiomatic', 'bench_mako_controlled', 'bench_mako_idiomatic', 'bench_cold_compile')
            foreach ($s in $pyScripts) {
                $pyShapeArgs = ''
                if ($s -eq 'bench_cold_compile') { $pyShapeArgs = $pyColdArgs }
                else { $pyShapeArgs = $pyValuesArgs }
                [void](Invoke-Step -Eco 'python' -Phase $measurePhase -Name $s -WorkDir $PyDir `
                    -Command ($VenvPy + ' ' + $s + '.py --affinity=4' + $pyShapeArgs + $pySmokeArgs + ' -o ' + (Join-Path $pyOut ($s + '.json'))))
            }
            # Memory pass -- tracemalloc, separate from timing (Phase 5 D11).
            $memArgs = ''
            if ($Smoke) { $memArgs = ' --reps 5' }
            [void](Invoke-Step -Eco 'python' -Phase $measurePhase -Name 'mem-tracemalloc' -WorkDir $PyDir `
                -Command ($VenvPy + ' mem_tracemalloc.py' + $memArgs + ' -o ' + (Join-Path $pyOut 'memory.json')))
        }
        'go' {
            # Phase 6 reproduce path: the committed run-benchmarks.ps1 (version asserts,
            # templ freshness, vet, gates, prebuild, High-priority timed runs, benchstat).
            $goCmd = 'powershell -NoProfile -ExecutionPolicy Bypass -File run-benchmarks.ps1'
            if ($Smoke) { $goCmd = $goCmd + ' -Count 1 -BenchTime 100ms' }
            elseif ($goProfileArgs -ne '') { $goCmd = $goCmd + $goProfileArgs }
            [void](Invoke-Step -Eco 'go' -Phase $measurePhase -Name 'run-benchmarks' -WorkDir $GoDir -Command $goCmd)
            Copy-Artifacts -Eco 'go' -Name 'copy-results' `
                -Source (Join-Path $GoDir 'results') -Dest (Join-Path $OutDir 'go')
        }
    }
}

Write-Summary

# --- consolidated tables ----------------------------------------------------------------------
# Publishing used to mean copying artifacts into docs/benchmarks/<date>/ and then running
# consolidate.py by hand. That hand step is exactly where table ORDER and transcribed figures drift
# from the artifacts, so the runner does it here: the out dir gets its own consolidated-tables.md
# and summary-tables.md, generated in tier order, before anyone looks at a number. Publishing is
# then a copy -- no regeneration, no hand-ordered tables, no transcription.
#
# Non-fatal by design: a full sweep is expensive and a table-generation failure must not discard
# it. The step is recorded like any other, so `RESULT: OK` still means every measurement is green
# and a WARN row here says the tables need attention, not the run.
if (-not $Smoke) {
    # Stdlib-only and CPython >= 3.12, so the interpreter on PATH is enough -- the harness venv
    # under benchmarks/python is pyperf's, and consolidate.py must not depend on it.
    $consolidateCmd = 'python benchmarks\report\consolidate.py "' + $OutDir + '"'
    [void](Invoke-Step -Eco 'report' -Phase $measurePhase -Name 'consolidate' -WorkDir $RepoRoot `
        -NonFatal -Command $consolidateCmd)
    Write-Summary
}

if ($script:Failed) {
    Write-Host 'RESULT: FAILED (one or more steps exited nonzero -- see summary above).' -ForegroundColor Red
    exit 1
}
Write-Host 'RESULT: OK (all steps green; WARN rows, if any, are recorded non-fatal steps).' -ForegroundColor Green
exit 0
