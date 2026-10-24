[CmdletBinding()]
param([string]$DotnetPath = 'dotnet', [switch]$SkipTests, [switch]$SkipUiSmoke)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$scratch = Join-Path $repo '.build-home'
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
# Process-only build isolation: no first-run certificate, telemetry, profile/PATH registration.
$env:DOTNET_CLI_HOME = Join-Path $scratch 'cli'
$env:APPDATA = Join-Path $scratch 'appdata'
$env:LOCALAPPDATA = Join-Path $scratch 'localappdata'
$env:NUGET_PACKAGES = Join-Path $scratch 'packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_NOLOGO = '1'
$evidence = Join-Path $repo 'docs/evidence'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
Push-Location $repo
try {
    & $DotnetPath restore SafeSeat.sln --configfile NuGet.Config -p:BuildInParallel=false -m:1 -nr:false 2>&1 |
        Tee-Object -FilePath (Join-Path $evidence 'build.txt')
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    & $DotnetPath build SafeSeat.sln --no-restore -c Release -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false 2>&1 |
        Tee-Object -FilePath (Join-Path $evidence 'build.txt') -Append
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    if (-not $SkipTests) {
        & $DotnetPath tests/SafeSeat.Tests/bin/Release/net8.0/SafeSeat.Tests.dll 2>&1 |
            Tee-Object -FilePath (Join-Path $evidence 'tests.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Core tests failed.' }
        & $DotnetPath tests/SafeSeat.NativeTests/bin/Release/net8.0/SafeSeat.NativeTests.dll 2>&1 |
            Tee-Object -FilePath (Join-Path $evidence 'native-tests.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Native ABI/parser tests failed.' }
        if ($env:OS -eq 'Windows_NT' -and -not $SkipUiSmoke) {
            . (Join-Path $PSScriptRoot 'apphost.ps1')
            $uiExitCode = Invoke-SafeSeatAppHost -ExecutablePath (Join-Path $repo 'src/SafeSeat.App/bin/Release/net8.0-windows/SafeSeat.exe') -DotnetPath $DotnetPath -Arguments @('--smoke-ui', (Join-Path $evidence 'ui-smoke.txt'))
            if ($uiExitCode -ne 0) { throw 'Offscreen apphost UI smoke failed.' }
            Get-Content -LiteralPath (Join-Path $evidence 'ui-smoke.txt')
        } elseif ($SkipUiSmoke) {
            $skipNotice = 'NOT TESTED: apphost UI smoke explicitly skipped. Console test results do not verify visible UI, physical devices or independent operation.'
            $skipNotice | Set-Content -LiteralPath (Join-Path $evidence 'ui-smoke.txt')
            Write-Output $skipNotice
        }
    }
}
finally { Pop-Location }
