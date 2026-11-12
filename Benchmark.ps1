<#
.SYNOPSIS
  Compares the speed and the allocations of the library in this repository with a published package.

.DESCRIPTION
  Two builds of the same benchmark sources are measured on the same machine:

    current    test/ForkForward.Mediator.Benchmarks            the library as it is in this repository
    baseline   test/ForkForward.Mediator.Benchmarks.Baseline   a published package, by default the last release

  They run in alternating rounds, and the side that goes first changes every round, so a busy moment
  on the machine does not hit one side only.

  For each benchmark the fastest iteration of each side, over all rounds, is compared. Other work on
  the machine only ever adds time, so the fastest iteration is the measurement least disturbed by it.
  Comparing medians instead reports differences of 5 to 15 % between two builds of identical code.

  The benchmarks in ReferenceBenchmarks do not use the library, so both sides must measure the same
  there. How far apart they are tells how noisy the machine was.

  The benchmarks in StartupBenchmarks are checked for allocations only. Their timings vary too much
  from run to run to be a gate; they are reported for information.

  Exit code:
    0  no regression
    1  regression: slower than allowed on a quiet machine, or more memory allocated
    2  inconclusive: slower than allowed, but the reference benchmarks show the machine was too noisy
    3  the benchmarks did not build or did not run

  Next to summary.md the run writes verdict.json. With -MergeVerdicts, several of those, from runs on
  different machines, are judged together: a benchmark is slower only when every run measured it slower.
  The two sides are two separately compiled assemblies of the same benchmarks, and on some machines the
  JIT lays one of them out less favourably; one run then shows a difference of 10 % in a single benchmark
  that another machine does not see at all. Allocations do not depend on the machine: more in any run counts.

.EXAMPLE
  ./Benchmark.ps1

.EXAMPLE
  ./Benchmark.ps1 -BaselinePackage MediatR -BaselineVersion 12.5.0 -BaselineNamespace MediatR

.EXAMPLE
  ./Benchmark.ps1 -Filter *Send* -Rounds 1

.EXAMPLE
  ./Benchmark.ps1 -CompareOnly -MaxTimeRegressionPercent 5

.EXAMPLE
  ./Benchmark.ps1 -MergeVerdicts ./run-1/verdict.json, ./run-2/verdict.json
#>
[CmdletBinding()]
param(
    # How many times each side is measured.
    [int]$Rounds = 3,
    # A benchmark fails when it is slower than the baseline by more than this.
    [double]$MaxTimeRegressionPercent = 10,
    # A benchmark fails when it allocates more than the baseline by more than this.
    [double]$MaxAllocationRegressionPercent = 1,
    # When a reference benchmark differs between the two sides by more than this, timings are not trusted.
    [double]$MaxNoisePercent = 5,
    # Package to compare with. Default: the one in the baseline project,
    # test/ForkForward.Mediator.Benchmarks.Baseline/ForkForward.Mediator.Benchmarks.Baseline.csproj.
    [string]$BaselinePackage,
    [string]$BaselineVersion,
    # Its namespace: ForkForward.Mediator from 12.7.0 on, MediatR for MediatR itself and for 12.6.0-preview.1.
    [string]$BaselineNamespace,
    # BenchmarkDotNet filter, for example *Send* or *Startup*.
    [string]$Filter = '*',
    [string]$OutputDirectory = './artifacts/benchmarks',
    # Judge the measurements of the previous run again, for example with other limits, without measuring.
    [switch]$CompareOnly,
    # Judge the verdict.json files of several runs together; nothing is measured. See the description.
    [string[]]$MergeVerdicts
)

$ErrorActionPreference = 'Stop'
$invariant = [System.Globalization.CultureInfo]::InvariantCulture
# Benchmark classes with a special role. Keep in step with test/ForkForward.Mediator.Benchmarks/MediatorBenchmarks.cs.
$referenceType = 'ReferenceBenchmarks'
$allocationsOnlyTypes = @('StartupBenchmarks')

function Format-Time([double]$nanoseconds) {
    if ($nanoseconds -ge 1e9) { return ($nanoseconds / 1e9).ToString('0.00', $invariant) + ' s' }
    if ($nanoseconds -ge 1e6) { return ($nanoseconds / 1e6).ToString('0.00', $invariant) + ' ms' }
    if ($nanoseconds -ge 1e3) { return ($nanoseconds / 1e3).ToString('0.00', $invariant) + ' us' }
    return $nanoseconds.ToString('0.00', $invariant) + ' ns'
}

function Format-Bytes([double]$bytes) {
    if ($bytes -ge 1048576) { return ($bytes / 1048576).ToString('0.00', $invariant) + ' MB' }
    if ($bytes -ge 1024) { return ($bytes / 1024).ToString('0.00', $invariant) + ' KB' }
    return $bytes.ToString('0', $invariant) + ' B'
}

function Format-Change([double]$ratio) {
    $percent = ($ratio - 1) * 100
    $sign = ''
    if ($percent -gt 0) { $sign = '+' }
    return $sign + $percent.ToString('0.0', $invariant) + ' %'
}

# Reads every BenchmarkDotNet report below a folder: benchmark name -> one entry per round.
function Read-Results([string]$directory) {
    $results = @{}
    $reports = Get-ChildItem -Path $directory -Recurse -Filter '*-report-full.json' -ErrorAction SilentlyContinue
    foreach ($report in $reports) {
        $json = Get-Content -Path $report.FullName -Raw | ConvertFrom-Json
        foreach ($benchmark in $json.Benchmarks) {
            if ($null -eq $benchmark.Statistics) { continue }
            $name = $benchmark.Type + '.' + $benchmark.Method
            if (-not $results.ContainsKey($name)) { $results[$name] = @() }
            $allocated = 0.0
            if ($null -ne $benchmark.Memory) { $allocated = [double]$benchmark.Memory.BytesAllocatedPerOperation }
            $results[$name] += [pscustomobject]@{
                Type      = $benchmark.Type
                Fastest   = [double]$benchmark.Statistics.Min
                Allocated = $allocated
            }
        }
    }
    return $results
}

# Several runs judged together. Time: slower only when every run says so, and only trusted when every
# run was quiet. Allocations: more in any run.
function Merge-Verdicts([string[]]$files) {
    $runs = @()
    foreach ($file in $files) {
        $runs += Get-Content -Path $file -Raw -Encoding utf8 | ConvertFrom-Json
    }
    $failed = @($runs | Where-Object { $_.ExitCode -eq 3 })
    if ($failed.Count -gt 0) {
        Write-Host "$($failed.Count) of $($runs.Count) runs did not measure."
        exit 3
    }
    $names = @($runs | ForEach-Object { $_.Rows } | ForEach-Object { $_.Name } | Sort-Object -Unique)
    $confirmedSlower = @()
    $unconfirmedSlower = @()
    $allocatesMore = @()
    $lines = @()
    $header = '| Benchmark |'
    $separator = '|---|'
    for ($i = 1; $i -le $runs.Count; $i++) { $header += " Time, run $i |"; $separator += '---:|' }
    for ($i = 1; $i -le $runs.Count; $i++) { $header += " Memory, run $i |"; $separator += '---:|' }
    $lines += $header + ' Result |'
    $lines += $separator + '---|'
    foreach ($name in $names) {
        $rows = @($runs | ForEach-Object { $_.Rows | Where-Object { $_.Name -eq $name } })
        $slowerIn = @($runs | Where-Object { $_.Slower -contains $name })
        $heavierIn = @($runs | Where-Object { $_.AllocatesMore -contains $name })
        $result = 'ok'
        if ($rows[0].IsReference) {
            $result = 'reference'
        }
        elseif ($heavierIn.Count -gt 0) {
            $allocatesMore += $name
            $result = '**allocates more**'
            if ($slowerIn.Count -eq $runs.Count) { $confirmedSlower += $name; $result = '**slower, allocates more**' }
        }
        elseif ($slowerIn.Count -eq $runs.Count) {
            $confirmedSlower += $name
            $result = '**slower**'
        }
        elseif ($slowerIn.Count -gt 0) {
            $unconfirmedSlower += $name
            $result = "slower in $($slowerIn.Count) of $($runs.Count) runs, not confirmed"
        }
        elseif (-not $rows[0].TimeIsGated) {
            $result = 'ok (allocations only)'
        }
        $cells = @($name)
        $cells += $rows | ForEach-Object { Format-Change ([double]$_.TimeRatio) }
        $cells += $rows | ForEach-Object { if ([double]::IsInfinity([double]$_.AllocationRatio)) { 'n/a' } else { Format-Change ([double]$_.AllocationRatio) } }
        $cells += $result
        $lines += '| ' + ($cells -join ' | ') + ' |'
    }
    $allQuiet = @($runs | Where-Object { -not $_.Quiet }).Count -eq 0
    $exitCode = 0
    $verdict = 'No regression.'
    if ($allocatesMore.Count -gt 0) {
        $exitCode = 1
        $verdict = 'Regression: more memory allocated in ' + ($allocatesMore -join ', ') + '.'
        if ($confirmedSlower.Count -gt 0) { $verdict += ' Also slower: ' + ($confirmedSlower -join ', ') + '.' }
    }
    elseif ($confirmedSlower.Count -gt 0 -and $allQuiet) {
        $exitCode = 1
        $verdict = 'Regression: slower in ' + ($confirmedSlower -join ', ') + ", in every one of $($runs.Count) runs."
    }
    elseif ($confirmedSlower.Count -gt 0) {
        $exitCode = 2
        $verdict = 'Inconclusive: ' + ($confirmedSlower -join ', ') + ' measured slower in every run, but at least one machine was too noisy to trust timings. Run it again.'
    }
    if ($unconfirmedSlower.Count -gt 0) {
        $verdict += ' ' + ($unconfirmedSlower -join ', ') + ' measured slower in some runs but not in others: the usual sign of two builds laid out differently by the JIT, not of a change in the code.'
    }
    $report = @()
    $report += '## Benchmark comparison'
    $report += ''
    $report += "**$verdict**"
    $report += ''
    $report += "- Baseline: $($runs[0].Libraries.baseline)"
    $report += "- Current: $($runs[0].Libraries.current)"
    $report += "- Runs: $($runs.Count), each on its own machine; machine noise " + (($runs | ForEach-Object { ([double]$_.NoisePercent).ToString('0.0', $invariant) + ' %' }) -join ', ')
    $report += "- Limits: time +$(([double]$runs[0].Limits.Time).ToString($invariant)) %, allocations +$(([double]$runs[0].Limits.Allocations).ToString($invariant)) %. Time counts only when every run measured it; allocations count in any run"
    $report += ''
    $report += $lines
    $report += ''
    if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $report -Encoding utf8 }
    $report | Out-Host
    exit $exitCode
}

if ($MergeVerdicts) {
    Merge-Verdicts $MergeVerdicts
}

# Environment variables, not -p: arguments: BenchmarkDotNet builds the projects again by itself,
# and only the environment reaches that build.
if ($BaselinePackage) { $env:BaselinePackage = $BaselinePackage }
if ($BaselineVersion) { $env:BaselineVersion = $BaselineVersion }
if ($BaselineNamespace) { $env:BaselineNamespace = $BaselineNamespace }

$sides = [ordered]@{
    baseline = './test/ForkForward.Mediator.Benchmarks.Baseline/ForkForward.Mediator.Benchmarks.Baseline.csproj'
    current  = './test/ForkForward.Mediator.Benchmarks/ForkForward.Mediator.Benchmarks.csproj'
}

if (-not $CompareOnly) {
    if (Test-Path $OutputDirectory) { Remove-Item $OutputDirectory -Recurse -Force }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
}
if (-not (Test-Path $OutputDirectory)) {
    Write-Host "There are no measurements in $OutputDirectory. Run without -CompareOnly first."
    exit 3
}
$OutputDirectory = (Resolve-Path $OutputDirectory).Path

if (-not $CompareOnly) {
    foreach ($side in $sides.Keys) {
        Write-Host "== Building $side"
        & dotnet build "$($sides[$side])" -c Release --nologo -v q
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Building the $side benchmarks failed."
            exit 3
        }
    }
}

# What each side really measures, read from the library next to the benchmark.
$libraries = @{}
foreach ($side in $sides.Keys) {
    $output = Join-Path (Split-Path $sides[$side]) 'bin/Release'
    # ForkForward.Mediator.dll from 12.7.0 on; MediatR.dll for MediatR itself and for 12.6.0-preview.1.
    # The newest one: an earlier build against another baseline may have left its library in the folder.
    $library = Get-ChildItem -Path $output -Recurse -Include 'ForkForward.Mediator.dll', 'MediatR.dll' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $version = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($library.FullName)
    $libraries[$side] = ($version.ProductName + ' ' + ($version.ProductVersion -split '\+')[0])
}
$libraries['baseline'] = 'package ' + $libraries['baseline']
$commit = $null
try { $commit = (& git rev-parse --short HEAD) } catch { }
if ($commit) { $libraries['current'] = "this repository at $commit, " + $libraries['current'] }
else { $libraries['current'] = 'this repository, ' + $libraries['current'] }

if (-not $CompareOnly) {
    for ($round = 1; $round -le $Rounds; $round++) {
        # The side that goes first changes every round, so neither always runs on the warmer machine.
        $order = @($sides.Keys)
        if ($round % 2 -eq 0) { [array]::Reverse($order) }
        foreach ($side in $order) {
            Write-Host "== Round $round of $Rounds, $side"
            $roundDirectory = Join-Path $OutputDirectory "$side/round-$round"
            # The arguments are written out in quotes, not passed as an array or a bare variable. On Linux
            # and macOS PowerShell replaces an unquoted argument that has wildcards with the names of the
            # matching files, like a shell does, also when it comes from a variable: the filter * became
            # the list of files in this folder, and no benchmark matched it.
            & dotnet run -c Release --no-build --project "$($sides[$side])" -- --filter "$Filter" --artifacts "$roundDirectory"
            if ($LASTEXITCODE -ne 0) {
                Write-Host "Running the $side benchmarks failed."
                exit 3
            }
            # BenchmarkDotNet exits with 0 when the filter matches nothing.
            if (-not (Get-ChildItem -Path $roundDirectory -Recurse -Filter '*-report-full.json' -ErrorAction SilentlyContinue)) {
                Write-Host "The $side benchmarks measured nothing. Does the filter '$Filter' match a benchmark?"
                exit 3
            }
        }
    }
}

$baseline = Read-Results (Join-Path $OutputDirectory 'baseline')
$current = Read-Results (Join-Path $OutputDirectory 'current')
if ($baseline.Count -eq 0 -or $current.Count -eq 0) {
    Write-Host 'No benchmark results were found.'
    exit 3
}
# With -CompareOnly the number of rounds is whatever the previous run measured.
$Rounds = ($current.Values | ForEach-Object { @($_).Count } | Measure-Object -Maximum).Maximum

$rows = @()
foreach ($name in ($current.Keys | Sort-Object)) {
    if (-not $baseline.ContainsKey($name)) { continue }
    # The fastest iteration of each side over all rounds: other work on the machine only ever adds time.
    $baselineTime = ($baseline[$name] | Measure-Object -Property Fastest -Minimum).Minimum
    $currentTime = ($current[$name] | Measure-Object -Property Fastest -Minimum).Minimum
    $baselineAllocated = ($baseline[$name] | Measure-Object -Property Allocated -Minimum).Minimum
    $currentAllocated = ($current[$name] | Measure-Object -Property Allocated -Minimum).Minimum
    $allocationRatio = 1.0
    if ($baselineAllocated -gt 0) { $allocationRatio = $currentAllocated / $baselineAllocated }
    elseif ($currentAllocated -gt 0) { $allocationRatio = [double]::PositiveInfinity }
    $rows += [pscustomobject]@{
        Name              = $name
        IsReference       = ($current[$name][0].Type -eq $referenceType)
        TimeIsGated       = ($allocationsOnlyTypes -notcontains $current[$name][0].Type)
        BaselineTime      = $baselineTime
        CurrentTime       = $currentTime
        TimeRatio         = $currentTime / $baselineTime
        BaselineAllocated = $baselineAllocated
        CurrentAllocated  = $currentAllocated
        AllocationRatio   = $allocationRatio
    }
}

$noisePercent = 0.0
foreach ($row in ($rows | Where-Object { $_.IsReference })) {
    $noisePercent = [Math]::Max($noisePercent, [Math]::Abs($row.TimeRatio - 1) * 100)
}
$quiet = $noisePercent -le $MaxNoisePercent

$slower = @()
$allocatesMore = @()
$lines = @()
$lines += '| Benchmark | Baseline | Current | Time | Allocated before | Allocated now | Memory | Result |'
$lines += '|---|---:|---:|---:|---:|---:|---:|---|'
foreach ($row in $rows) {
    $result = 'ok'
    if ($row.IsReference) {
        $result = 'reference'
    }
    else {
        $isSlower = $row.TimeIsGated -and (($row.TimeRatio - 1) * 100 -gt $MaxTimeRegressionPercent)
        $isHeavier = ($row.AllocationRatio - 1) * 100 -gt $MaxAllocationRegressionPercent
        if ($isSlower) { $slower += $row.Name }
        if ($isHeavier) { $allocatesMore += $row.Name }
        if ($isSlower -and $isHeavier) { $result = '**slower, allocates more**' }
        elseif ($isSlower) { $result = '**slower**' }
        elseif ($isHeavier) { $result = '**allocates more**' }
        elseif (-not $row.TimeIsGated) { $result = 'ok (allocations only)' }
    }
    $memoryChange = 'n/a'
    if (-not [double]::IsInfinity($row.AllocationRatio)) { $memoryChange = Format-Change $row.AllocationRatio }
    $lines += '| ' + (@(
            $row.Name,
            (Format-Time $row.BaselineTime),
            (Format-Time $row.CurrentTime),
            (Format-Change $row.TimeRatio),
            (Format-Bytes $row.BaselineAllocated),
            (Format-Bytes $row.CurrentAllocated),
            $memoryChange,
            $result) -join ' | ') + ' |'
}

$exitCode = 0
$verdict = 'No regression.'
if ($allocatesMore.Count -gt 0) {
    $exitCode = 1
    $verdict = 'Regression: more memory allocated in ' + ($allocatesMore -join ', ') + '.'
    if ($slower.Count -gt 0) { $verdict += ' Also slower: ' + ($slower -join ', ') + '.' }
}
elseif ($slower.Count -gt 0 -and $quiet) {
    $exitCode = 1
    $verdict = 'Regression: slower in ' + ($slower -join ', ') + '.'
}
elseif ($slower.Count -gt 0) {
    $exitCode = 2
    $verdict = 'Inconclusive: ' + ($slower -join ', ') + ' measured slower, but the machine was too noisy to trust timings. Run it again.'
}

$report = @()
$report += '## Benchmark comparison'
$report += ''
$report += "**$verdict**"
$report += ''
$report += "- Baseline: $($libraries['baseline'])"
$report += "- Current: $($libraries['current'])"
$report += "- Rounds: $Rounds per side, alternating; the fastest iteration of each side is compared"
$report += "- Limits: time +$($MaxTimeRegressionPercent.ToString($invariant)) %, allocations +$($MaxAllocationRegressionPercent.ToString($invariant)) %. $($allocationsOnlyTypes -join ', ') is checked for allocations only"
$report += "- Machine noise: $($noisePercent.ToString('0.0', $invariant)) % (largest difference between the two sides in the reference benchmarks; timings are trusted up to $($MaxNoisePercent.ToString($invariant)) %)"
$report += ''
$report += $lines
$report += ''

$summaryFile = Join-Path $OutputDirectory 'summary.md'
Set-Content -Path $summaryFile -Value $report -Encoding utf8
# For -MergeVerdicts: the same judgement, machine-readable.
$verdictFile = Join-Path $OutputDirectory 'verdict.json'
[ordered]@{
    ExitCode      = $exitCode
    Verdict       = $verdict
    Quiet         = $quiet
    NoisePercent  = $noisePercent
    Rounds        = $Rounds
    Limits        = [ordered]@{ Time = $MaxTimeRegressionPercent; Allocations = $MaxAllocationRegressionPercent; Noise = $MaxNoisePercent }
    Libraries     = [ordered]@{ baseline = $libraries['baseline']; current = $libraries['current'] }
    Slower        = @($slower)
    AllocatesMore = @($allocatesMore)
    Rows          = @($rows)
} | ConvertTo-Json -Depth 4 | Set-Content -Path $verdictFile -Encoding utf8
if ($env:GITHUB_STEP_SUMMARY) { Add-Content -Path $env:GITHUB_STEP_SUMMARY -Value $report -Encoding utf8 }
$report | Out-Host
Write-Host "Report: $summaryFile"
exit $exitCode
