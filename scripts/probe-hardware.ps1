[CmdletBinding()]
param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$probe = Join-Path $repo 'tests/SafeSeat.NativeTests/bin/Release/net8.0/SafeSeat.NativeTests.dll'
if (-not (Test-Path -LiteralPath $probe)) { throw 'Build first using scripts/build.ps1.' }
$evidence = Join-Path $repo 'docs/evidence'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
& $DotnetPath $probe --inventory | Set-Content -LiteralPath (Join-Path $evidence 'inventory.json')
if ($LASTEXITCODE -ne 0) { throw 'Read-only collection inventory failed.' }
& $DotnetPath $probe --readiness | Set-Content -LiteralPath (Join-Path $evidence 'hardware-readiness.json')
if ($LASTEXITCODE -ne 0) { throw 'Read-only readiness query failed.' }
Get-Content -Raw -LiteralPath (Join-Path $evidence 'hardware-readiness.json')
Write-Output 'No assignments were loaded by this CLI probe. Use the GUI readiness view for the selected profile.'
Write-Output 'Counts describe collections/candidate groups, not physically confirmed devices. No input observer was started.'
