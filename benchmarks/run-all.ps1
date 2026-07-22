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
#   ... -JsStabilityRepeat        # opt in to the JS five-run stability procedure (Phase 4 D13)
#
# Windows PowerShell 5.1 compatible: no pipeline chain operators, no ternary, ASCII only.

[CmdletBinding()]
param(
    [switch]$Smoke,

    [ValidateSet('dotnet', 'rust', 'jvm', 'js', 'python', 'go')]
    [string[]]$Ecosystem = @('dotnet', 'rust', 'jvm', 'js', 'python', 'go'),

    [string]$OutDir = '',

    # Phase 4 D13: the five-run stability procedure is a separate publication-gating step,
    # never auto-run. This switch opts in (full mode only; runs BEFORE the timed suites,
    # matching the Linux run-all.sh measurement order).
    [switch]$JsStabilityRepeat
)

$ErrorActionPreference = 'Continue'

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

# The eight protocol suites (phase 1 metrics-protocol: 'The protocol's first exercise').
$DotnetSuites = @(
    'TextRenderBenchmarks',
    'SubstitutionRenderBenchmarks',
    'LoopRenderBenchmarks',
    'MixedRenderBenchmarks',
    'ConditionalRenderBenchmarks',
    'FragmentRenderBenchmarks',
    'FortunesRenderBenchmarks',
    'EncodedLoopRenderBenchmarks'
)

$PerfDir = Join-Path $RepoRoot 'src\Heddle.Performance'
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
            [void](Invoke-Step -Eco 'dotnet' -Phase 'gate' -Name 'gate-parity' -WorkDir $PerfDir `
                -Command 'dotnet run -c Release -f net10.0 -- parity')
            if ($script:Failed) { break }
            [void](Invoke-Step -Eco 'dotnet' -Phase 'gate' -Name 'gate-verify-corpus' -WorkDir $PerfDir `
                -Command 'dotnet run -c Release -f net10.0 -- verify-corpus')
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
            # Phase 1 protocol shape: Release, net10.0, BenchmarkDotNet defaults,
            # MemoryDiagnoser via suite attributes; one --filter run per protocol suite.
            foreach ($suite in $DotnetSuites) {
                $bdnCmd = 'dotnet run -c Release -f net10.0 -- --filter *' + $suite + '*'
                if ($Smoke) { $bdnCmd = $bdnCmd + ' --job Dry' }
                [void](Invoke-Step -Eco 'dotnet' -Phase $measurePhase -Name ('suite-' + $suite) -WorkDir $PerfDir -Command $bdnCmd)
            }
            Copy-Artifacts -Eco 'dotnet' -Name 'copy-bdn-artifacts' `
                -Source (Join-Path $PerfDir 'BenchmarkDotNet.Artifacts') -Dest (Join-Path $OutDir 'dotnet')
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
                    -Command ('cargo bench ' + $benchTargets + ' -- --noplot'))
                [void](Invoke-Step -Eco 'rust' -Phase $measurePhase -Name 'alloc-report' -WorkDir $RustDir `
                    -Command 'cargo run --release --features alloc-count --bin alloc_report')
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
                Write-Host 'NOTE: full JMH run uses the committed annotation regime (Fork 5, 5x10s/5x10s).' -ForegroundColor Yellow
                Write-Host '      Expect roughly 4.5-5.5 hours unattended (Phase 3 procedure).' -ForegroundColor Yellow
                [void](Invoke-Step -Eco 'jvm' -Phase $measurePhase -Name 'jmh-full' -WorkDir $JvmDir `
                    -Command ('java -jar target\benchmarks.jar -prof gc -rf json -rff ' + $rff))
            }
        }
        'js' {
            # Phase 4 shapes via the committed launcher run.ps1 (High priority class,
            # node --expose-gc --allow-natives-syntax, stdout captured to artifacts/).
            $runPs1 = 'powershell -NoProfile -ExecutionPolicy Bypass -File run.ps1'
            if (-not $Smoke) {
                if ($JsStabilityRepeat) {
                    [void](Invoke-Step -Eco 'js' -Phase $measurePhase -Name 'stability-repeat5' -WorkDir $JsDir `
                        -Command ($runPs1 + ' bench/controlled.mjs -Repeat 5'))
                    Write-Host 'NOTE: compute the five-run RSD verdict (Phase 4 D13 thresholds) from' -ForegroundColor Yellow
                    Write-Host '      artifacts\stability\run-*.json before publishing JS numbers.' -ForegroundColor Yellow
                }
                else {
                    Write-Host 'NOTE: the JS five-run stability procedure (Phase 4 D13) is a separate,' -ForegroundColor Yellow
                    Write-Host '      publication-gating step. Re-run with -JsStabilityRepeat to execute it.' -ForegroundColor Yellow
                }
            }
            [void](Invoke-Step -Eco 'js' -Phase $measurePhase -Name 'bench-controlled' -WorkDir $JsDir `
                -Command ($runPs1 + ' bench/controlled.mjs'))
            [void](Invoke-Step -Eco 'js' -Phase $measurePhase -Name 'bench-idiomatic' -WorkDir $JsDir `
                -Command ($runPs1 + ' bench/idiomatic.mjs'))
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
            $pyScripts = @('bench_jinja2_controlled', 'bench_jinja2_idiomatic', 'bench_mako_controlled', 'bench_mako_idiomatic', 'bench_cold_compile')
            foreach ($s in $pyScripts) {
                [void](Invoke-Step -Eco 'python' -Phase $measurePhase -Name $s -WorkDir $PyDir `
                    -Command ($VenvPy + ' ' + $s + '.py --affinity=4' + $pySmokeArgs + ' -o ' + (Join-Path $pyOut ($s + '.json'))))
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
            [void](Invoke-Step -Eco 'go' -Phase $measurePhase -Name 'run-benchmarks' -WorkDir $GoDir -Command $goCmd)
            Copy-Artifacts -Eco 'go' -Name 'copy-results' `
                -Source (Join-Path $GoDir 'results') -Dest (Join-Path $OutDir 'go')
        }
    }
}

Write-Summary
if ($script:Failed) {
    Write-Host 'RESULT: FAILED (one or more steps exited nonzero -- see summary above).' -ForegroundColor Red
    exit 1
}
Write-Host 'RESULT: OK (all steps green; WARN rows, if any, are recorded non-fatal steps).' -ForegroundColor Green
exit 0
