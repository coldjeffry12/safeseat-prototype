[CmdletBinding()]
param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$appHost = Join-Path $repo 'src/SafeSeat.App/bin/Release/net8.0-windows/SafeSeat.exe'
if (-not (Test-Path -LiteralPath $appHost -PathType Leaf)) { throw 'SafeSeat.exe is missing. Build first using scripts/build.ps1.' }
. (Join-Path $PSScriptRoot 'apphost.ps1')
$exitCode = Invoke-SafeSeatAppHost -ExecutablePath $appHost -DotnetPath $DotnetPath
if ($exitCode -ne 0) { throw "SafeSeat exited with code $exitCode. A compatible x64 .NET 8 Windows Desktop runtime is required." }
