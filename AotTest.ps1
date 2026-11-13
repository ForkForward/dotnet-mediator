<#
.SYNOPSIS
  Publishes test/ForkForward.Mediator.AotTests with Native AOT for this machine and runs the result.

.DESCRIPTION
  The published program has no JIT. It runs the scenarios in test/ForkForward.Mediator.AotTests/Program.cs and
  exits with the number of failures. The build treats warnings as errors, so a warning from the trimming or AOT
  analyzers fails here too.

  With -Trimmed the program is published self-contained and trimmed instead: it keeps its JIT, and the trimmer
  removes what it cannot see being used. The same scenarios run, with the answers that are right with a JIT.

  Publishing with Native AOT needs the platform's native toolchain: the C++ build tools of Visual Studio on
  Windows, clang and the zlib headers on Linux, the Xcode command line tools on macOS. Without them, use the
  container image mcr.microsoft.com/dotnet/sdk:11.0-aot. Publishing trimmed needs nothing extra.

  ./Build.ps1 runs the same scenarios without publishing, with a JIT and dynamic code switched off. That covers
  the code paths the library takes under Native AOT on any machine; only this script proves the real thing.

.EXAMPLE
  ./AotTest.ps1

.EXAMPLE
  ./AotTest.ps1 -Frameworks net11.0

.EXAMPLE
  ./AotTest.ps1 -Trimmed
#>
[CmdletBinding()]
param(
    # The target frameworks to publish for. The library has an asset for each.
    [string[]]$Frameworks = @('net10.0', 'net11.0'),
    # Publish self-contained and trimmed, with a JIT, instead of with Native AOT.
    [switch]$Trimmed
)

$ErrorActionPreference = 'Stop'
$project = './test/ForkForward.Mediator.AotTests/ForkForward.Mediator.AotTests.csproj'
$name = 'ForkForward.Mediator.AotTests'
if ($env:OS -eq 'Windows_NT') { $name += '.exe' }

$kind = 'Native AOT'
$folder = 'aot'
$mode = @()
if ($Trimmed) {
    $kind = 'trimming'
    $folder = 'trimmed'
    $mode = @('-p:PublishAot=false', '-p:PublishTrimmed=true', '--self-contained')
}

foreach ($framework in $Frameworks) {
    $output = "./artifacts/$folder/$framework"
    if (Test-Path $output) { Remove-Item $output -Force -Recurse }

    Write-Host "== Publishing with $kind for $framework"
    & dotnet publish $project -c Release -f $framework --use-current-runtime @mode -o $output --nologo
    if ($LASTEXITCODE -ne 0) { throw "Publishing with $kind for $framework failed." }

    Write-Host "== Running the published program for $framework"
    & (Join-Path $output $name)
    if ($LASTEXITCODE -ne 0) { throw "$LASTEXITCODE scenario(s) failed with $kind for $framework." }
}

Write-Host "$kind`: all scenarios passed for $($Frameworks -join ' and ')."
