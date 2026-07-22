<#
.SYNOPSIS
    Protocol launcher for the JS benchmark harness (Phase 4 WI1; normative behavior in
    docs/spec/cross-stack-benchmarks/phase-4-js/harness-and-run.md, Dependency pinning).

.DESCRIPTION
    Starts `node --expose-gc --allow-natives-syntax <script>` with stdout captured to
    artifacts/<name>.txt (in addition to the JSON the script writes itself), sets the process
    priority class to High immediately after start, waits for exit, and propagates the exit
    code. `-Repeat N` runs the same invocation N times sequentially into
    artifacts/stability/run-<k>.txt/.json (the D13 Windows stability verification procedure).

.EXAMPLE
    ./run.ps1 bench/controlled.mjs
    ./run.ps1 bench/controlled.mjs -Repeat 5
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$Script,

    [Parameter(Mandatory = $false)]
    [ValidateRange(1, 100)]
    [int]$Repeat = 1
)

$ErrorActionPreference = 'Stop'
$harnessRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$scriptPath = Join-Path $harnessRoot $Script
if (-not (Test-Path $scriptPath)) {
    Write-Error "run.ps1: script not found: $scriptPath"
    exit 1
}

# The artifact base name mirrors the bench script name (bench/controlled.mjs -> controlled).
$name = [System.IO.Path]::GetFileNameWithoutExtension($Script)
$artifactsDir = Join-Path $harnessRoot 'artifacts'
if (-not (Test-Path $artifactsDir)) { New-Item -ItemType Directory -Path $artifactsDir | Out-Null }

$flags = @('--expose-gc', '--allow-natives-syntax')

function Invoke-BenchRun([string]$stdoutPath) {
    $stderrPath = [System.IO.Path]::GetTempFileName()
    try {
        $p = Start-Process -FilePath 'node' `
            -ArgumentList ($flags + @($scriptPath)) `
            -WorkingDirectory $harnessRoot `
            -RedirectStandardOutput $stdoutPath `
            -RedirectStandardError $stderrPath `
            -NoNewWindow -PassThru
        # High priority class immediately after start (D13 launcher posture; never Realtime).
        try { $p.PriorityClass = [System.Diagnostics.ProcessPriorityClass]::High } catch {
            Write-Warning "run.ps1: could not set High priority class: $_"
        }
        $p.WaitForExit()
        $err = Get-Content -Raw -ErrorAction SilentlyContinue $stderrPath
        if ($err) { Write-Host $err }
        return $p.ExitCode
    }
    finally {
        Remove-Item -Force -ErrorAction SilentlyContinue $stderrPath
    }
}

if ($Repeat -le 1) {
    $stdoutPath = Join-Path $artifactsDir "$name.txt"
    $code = Invoke-BenchRun $stdoutPath
    Write-Host "run.ps1: $Script exited with code $code; capture: artifacts/$name.txt"
    exit $code
}

# -Repeat mode: N consecutive full runs into artifacts/stability/run-<k>.txt/.json (D13).
$stabilityDir = Join-Path $artifactsDir 'stability'
if (-not (Test-Path $stabilityDir)) { New-Item -ItemType Directory -Path $stabilityDir | Out-Null }

for ($k = 1; $k -le $Repeat; $k++) {
    $stdoutPath = Join-Path $stabilityDir "run-$k.txt"
    $code = Invoke-BenchRun $stdoutPath
    if ($code -ne 0) {
        Write-Host "run.ps1: repeat $k/$Repeat failed with exit code $code - aborting the sequence."
        exit $code
    }
    # The bench script writes its JSON artifact as artifacts/<name>.json; snapshot it per run.
    $jsonSource = Join-Path $artifactsDir "$name.json"
    if (Test-Path $jsonSource) {
        Copy-Item -Force $jsonSource (Join-Path $stabilityDir "run-$k.json")
    }
    else {
        Write-Warning "run.ps1: expected JSON artifact not found after run ${k}: artifacts/$name.json"
    }
    Write-Host "run.ps1: repeat $k/$Repeat complete."
}
Write-Host "run.ps1: $Repeat consecutive runs captured under artifacts/stability/."
exit 0
