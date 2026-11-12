# Taken from psake https://github.com/psake/psake

<#
.SYNOPSIS
  This is a helper function that runs a scriptblock and checks the PS variable $lastexitcode
  to see if an error occcured. If an error is detected then an exception is thrown.
  This function allows you to run command-line programs without having to
  explicitly check the $lastexitcode variable.
.EXAMPLE
  exec { svn info $repository_trunk } "Error executing SVN. Please verify SVN command-line client is installed"
#>
function Exec
{
    [CmdletBinding()]
    param(
        [Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
        [Parameter(Position=1,Mandatory=0)][string]$errorMessage = ($msgs.error_bad_command -f $cmd)
    )
    & $cmd
    if ($lastexitcode -ne 0) {
        throw ("Exec: " + $errorMessage)
    }
}

# Build output uses the SDK artifacts layout (UseArtifactsOutput in Directory.Build.props):
#   artifacts/bin, artifacts/obj   compiled projects, kept between runs for incremental builds
#   artifacts/TestResults          test result files (.trx)
#   artifacts/package/release      the NuGet packages and their symbols
# Forward slashes, so that the script also runs under PowerShell on Linux and macOS.
$testResults = "./artifacts/TestResults"
$packages = "./artifacts/package/release"

# Results and packages from an earlier run must not be mistaken for this one's.
foreach ($path in $testResults, $packages) {
    if(Test-Path $path) { Remove-Item $path -Force -Recurse }
}

exec { & dotnet build -c Release }

exec { & dotnet test -c Release --no-build --results-directory $testResults -l trx }

exec { & dotnet pack ./src/ForkForward.Mediator.Contracts/ForkForward.Mediator.Contracts.csproj -c Release --no-build }

exec { & dotnet pack ./src/ForkForward.Mediator/ForkForward.Mediator.csproj -c Release --no-build }

