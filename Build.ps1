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

# The scenarios of the Native AOT test application, here with a JIT and with dynamic code switched off, which is
# what the SDK does for a project with PublishAot. It takes the code paths the library takes under Native AOT
# and needs no native toolchain. AotTest.ps1 publishes the same program with Native AOT and runs that.
foreach ($framework in 'net10.0', 'net11.0') {
    exec { & dotnet run -c Release --no-build -f $framework --project ./test/ForkForward.Mediator.AotTests/ForkForward.Mediator.AotTests.csproj }
}

exec { & dotnet pack ./src/ForkForward.Mediator.Contracts/ForkForward.Mediator.Contracts.csproj -c Release --no-build }

exec { & dotnet pack ./src/ForkForward.Mediator/ForkForward.Mediator.csproj -c Release --no-build }

