<#
.SYNOPSIS
  Measures what the mediator's calls cost once everything is warm: published with Native AOT against a JIT.

.DESCRIPTION
  Publishes test/ForkForward.Mediator.AotTests twice for this machine, once with Native AOT and once
  self-contained with a JIT, and runs both with --throughput (test/ForkForward.Mediator.AotTests/Throughput.cs).
  The two programs take turns, so that both meet the same machine in the same state, and each number in the
  result is the median of the runs.

  In a run every call is warmed up for a second, which is long enough for a JIT to reach its optimised code,
  and then measured in five rounds of about 200 ms.

  This is a measurement and not a gate: the script fails only when publishing or a run fails. Benchmark.ps1 is
  the gate, and it compares this branch with the last release, both with a JIT.

  Publishing with Native AOT needs the platform's native toolchain; see AotTest.ps1. On a machine without it,
  -Modes jit measures the JIT alone.

.EXAMPLE
  ./AotThroughput.ps1

.EXAMPLE
  ./AotThroughput.ps1 -Frameworks net11.0 -Runs 5

.EXAMPLE
  ./AotThroughput.ps1 -Modes jit
#>
[CmdletBinding()]
param(
    # The target frameworks to measure. The library has an asset for each.
    [string[]]$Frameworks = @('net10.0', 'net11.0'),
    # How many times each program is run.
    [ValidateRange(1, 99)][int]$Runs = 3,
    # What to measure. Native AOT needs the native toolchain.
    [ValidateSet('jit', 'aot')][string[]]$Modes = @('jit', 'aot')
)

$ErrorActionPreference = 'Stop'
$project = './test/ForkForward.Mediator.AotTests/ForkForward.Mediator.AotTests.csproj'
$name = 'ForkForward.Mediator.AotTests'
if ($env:OS -eq 'Windows_NT') { $name += '.exe' }
$results = './artifacts/throughput'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture

$title = @{ jit = 'JIT'; aot = 'Native AOT' }
# The project sets PublishAot. Without it, and self-contained, it is an ordinary application with a JIT that
# does not depend on which runtimes the machine has.
$publish = @{ jit = @('-p:PublishAot=false', '--self-contained'); aot = @() }

function Get-Median([double[]]$values) {
    $sorted = @($values | Sort-Object)
    $middle = [int][math]::Floor($sorted.Count / 2)
    if ($sorted.Count % 2 -eq 1) { return $sorted[$middle] }
    return ($sorted[$middle - 1] + $sorted[$middle]) / 2
}

function Format-Number([double]$value, [string]$format) { return $value.ToString($format, $invariant) }

$report = New-Object System.Collections.Generic.List[string]
$report.Add('## Throughput: Native AOT against a JIT')
$report.Add('')
$report.Add("$([System.Runtime.InteropServices.RuntimeInformation]::OSDescription), $([Environment]::ProcessorCount) processors. Time and allocated memory per call, median of $Runs run(s); every call warmed up for a second first. A measurement, not a gate.")

foreach ($framework in $Frameworks) {
    foreach ($mode in $Modes) {
        $output = "$results/$mode/$framework"
        if (Test-Path $output) { Remove-Item $output -Force -Recurse }

        Write-Host "== Publishing for $framework, $($title[$mode])"
        $arguments = $publish[$mode]
        & dotnet publish $project -c Release -f $framework --use-current-runtime @arguments -o $output --nologo
        if ($LASTEXITCODE -ne 0) { throw "Publishing for $framework, $($title[$mode]), failed." }
    }

    $cases = New-Object System.Collections.Generic.List[string]
    $times = @{}
    $bytes = @{}
    for ($run = 1; $run -le $Runs; $run++) {
        foreach ($mode in $Modes) {
            Write-Host "== $framework, $($title[$mode]), run $run of $Runs"
            $lines = @(& (Join-Path "$results/$mode/$framework" $name) --throughput)
            if ($LASTEXITCODE -ne 0) { throw "The run for $framework, $($title[$mode]), failed with exit code $LASTEXITCODE." }

            foreach ($line in $lines) {
                Write-Host $line
                if ($line -notmatch '^THROUGHPUT\|') { continue }
                # THROUGHPUT|name|median ns per call|fastest ns per call|bytes allocated per call
                $parts = $line.Split('|')
                $case = $parts[1]
                if (-not $cases.Contains($case)) { $cases.Add($case) }
                $key = "$mode|$case"
                if (-not $times.ContainsKey($key)) { $times[$key] = @(); $bytes[$key] = @() }
                $times[$key] += [double]::Parse($parts[2], $invariant)
                $bytes[$key] += [double]::Parse($parts[4], $invariant)
            }
        }
    }
    if ($cases.Count -eq 0) { throw "The runs for $framework printed no measurements." }

    $both = ($Modes -contains 'jit') -and ($Modes -contains 'aot')
    $report.Add('')
    $report.Add("### $framework")
    $report.Add('')
    if ($both) {
        $report.Add('| Call | JIT | Native AOT | Native AOT against JIT | Allocated, JIT | Allocated, Native AOT |')
        $report.Add('|---|---:|---:|---:|---:|---:|')
    }
    else {
        $report.Add("| Call | $($title[$Modes[0]]) | Allocated |")
        $report.Add('|---|---:|---:|')
    }
    foreach ($case in $cases) {
        if ($both) {
            $jit = Get-Median $times["jit|$case"]
            $aot = Get-Median $times["aot|$case"]
            $change = ($aot / $jit - 1) * 100
            $sign = ''
            if ($change -ge 0) { $sign = '+' }
            $report.Add("| $case | $(Format-Number $jit 'F1') ns | $(Format-Number $aot 'F1') ns | $sign$(Format-Number $change 'F1') % | $(Format-Number (Get-Median $bytes["jit|$case"]) 'F0') B | $(Format-Number (Get-Median $bytes["aot|$case"]) 'F0') B |")
        }
        else {
            $only = $Modes[0]
            $report.Add("| $case | $(Format-Number (Get-Median $times["$only|$case"]) 'F1') ns | $(Format-Number (Get-Median $bytes["$only|$case"]) 'F0') B |")
        }
    }
}

$text = $report -join [Environment]::NewLine
if (-not (Test-Path $results)) { New-Item -ItemType Directory -Force $results | Out-Null }
$text | Out-File -FilePath "$results/throughput.md" -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { $text | Out-File -FilePath $env:GITHUB_STEP_SUMMARY -Append -Encoding utf8 }

Write-Host ''
Write-Host $text
