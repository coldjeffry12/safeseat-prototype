[CmdletBinding()]
param([string]$DotnetPath)
$ErrorActionPreference = 'Stop'
# Release template: package.ps1 copies this beside the SafeSeat-demo folder.
$appHost = Join-Path $PSScriptRoot 'SafeSeat-demo/SafeSeat.exe'
if (-not (Test-Path -LiteralPath $appHost -PathType Leaf)) { throw 'SafeSeat-demo/SafeSeat.exe is missing. Use the released launcher beside that folder.' }
if (-not $DotnetPath) {
    $portable = Join-Path (Split-Path -Parent $PSScriptRoot) 'work/toolchain/dotnet/dotnet.exe'
    if (Test-Path -LiteralPath $portable -PathType Leaf) { $DotnetPath = $portable }
    else { $DotnetPath = (Get-Command dotnet -CommandType Application -ErrorAction Stop).Source }
}
$helper = Join-Path $PSScriptRoot 'SafeSeat-demo/apphost.ps1'
if (-not (Test-Path -LiteralPath $helper -PathType Leaf)) { throw 'SafeSeat-demo/apphost.ps1 is missing. Extract the complete package.' }
. $helper
$exitCode = Invoke-SafeSeatAppHost -ExecutablePath $appHost -DotnetPath $DotnetPath
if ($exitCode -ne 0) { throw "SafeSeat exited with code $exitCode. A compatible x64 .NET 8 Windows Desktop runtime is required." }
