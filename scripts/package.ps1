#requires -Version 7.0
[CmdletBinding()]
param([string]$DotnetPath = 'dotnet', [string]$OutputRoot, [switch]$SkipBuild, [switch]$SkipGuiSmoke)
$ErrorActionPreference = 'Stop'
$repo = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot)))
if (-not $OutputRoot) { $OutputRoot = Join-Path $repo '.packages' }
$output = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($OutputRoot))
$comparison = [System.StringComparison]::OrdinalIgnoreCase
if ($output.Equals([System.IO.Path]::GetPathRoot($output), $comparison)) { throw 'Use a project output folder, not a drive root.' }
if ($output.Equals($repo, $comparison)) { throw 'The output folder must not be the repository root.' }
$repoPrefix = $repo + [System.IO.Path]::DirectorySeparatorChar
$outputPrefix = $output + [System.IO.Path]::DirectorySeparatorChar
$outputInsideRepo = $output.StartsWith($repoPrefix, $comparison)
New-Item -ItemType Directory -Path $output -Force | Out-Null
$dotnetCommand = (Get-Command $DotnetPath -CommandType Application -ErrorAction Stop).Source
if (-not $SkipBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -DotnetPath $dotnetCommand -SkipUiSmoke:$SkipGuiSmoke }
$env:DOTNET_CLI_HOME = Join-Path $repo '.build-home/cli'
$env:APPDATA = Join-Path $repo '.build-home/appdata'
$env:LOCALAPPDATA = Join-Path $repo '.build-home/localappdata'
$env:NUGET_PACKAGES = Join-Path $repo '.build-home/packages'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:DOTNET_NOLOGO = '1'
# Only these general documents belong in public releases. Local diagnostics, even when
# hashed, may identify hardware or contain developer paths and are never package inputs.
$publicDocNames = @(
    'ACCEPTANCE_TESTS.md', 'BLOCKERS_AND_ALTERNATIVES.md', 'CI.md',
    'CODE_REVIEW_FIXES.md', 'CODE_SIGNING_POLICY.md',
    'DEMO_README.md', 'DEPENDENCIES.md', 'HARDWARE_VALIDATION.md',
    'PRIVACY.md', 'PROGRESS.md', 'RELEASE_VERIFICATION.md',
    'SAFETY_AND_RECOVERY.md', 'TECHNICAL_FEASIBILITY.md',
    'VERIFICATION_REPORT.md', 'sbom.cdx.json'
)
$publicDocs = @($publicDocNames | ForEach-Object {
    $candidate = Join-Path (Join-Path $repo 'docs') $_
    if (Test-Path -LiteralPath $candidate -PathType Leaf) { Get-Item -LiteralPath $candidate }
})
$publicDocPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($document in $publicDocs) {
    [void]$publicDocPaths.Add([System.IO.Path]::GetRelativePath($repo, $document.FullName).Replace('\', '/'))
}
# Every release is assembled independently of an existing public demo directory. In particular,
# never archive a previously used demo folder, which may contain profiles or unrelated local files.
$packageStage = Join-Path $repo ('.build-home/package-stage-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $packageStage | Out-Null
$stagedDemo = Join-Path $packageStage 'SafeSeat-demo'
$stagedLauncher = Join-Path $packageStage 'Run-SafeSeat.ps1'
$evidence = Join-Path $repo 'docs/evidence'
New-Item -ItemType Directory -Path $evidence -Force | Out-Null
& $dotnetCommand publish (Join-Path $repo 'src/SafeSeat.App/SafeSeat.App.csproj') -c Release --no-build --no-restore --no-self-contained -o $stagedDemo -p:CopyOutputSymbolsToPublishDirectory=false -m:1 -nr:false |
    Tee-Object -FilePath (Join-Path $evidence 'publish.txt')
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
if ($SkipGuiSmoke) {
    'NOT TESTED: published apphost GUI smoke explicitly skipped. Packaging does not verify visible UI, physical devices or independent operation.' |
        Set-Content -LiteralPath (Join-Path $evidence 'package-smoke.txt')
} else {
    . (Join-Path $PSScriptRoot 'apphost.ps1')
    $uiExitCode = Invoke-SafeSeatAppHost -ExecutablePath (Join-Path $stagedDemo 'SafeSeat.exe') -DotnetPath $dotnetCommand -Arguments @('--smoke-ui', (Join-Path $evidence 'package-smoke.txt'))
    if ($uiExitCode -ne 0) { throw 'Published apphost UI smoke failed.' }
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'apphost.ps1') -Destination (Join-Path $stagedDemo 'apphost.ps1') -Force
Copy-Item -LiteralPath (Join-Path $repo 'LICENSE') -Destination (Join-Path $stagedDemo 'LICENSE') -Force
$runtimeRoot = Split-Path -Parent $dotnetCommand
Copy-Item -LiteralPath (Join-Path $runtimeRoot 'LICENSE.txt') -Destination (Join-Path $stagedDemo 'DOTNET-LICENSE.txt') -Force
Copy-Item -LiteralPath (Join-Path $runtimeRoot 'ThirdPartyNotices.txt') -Destination (Join-Path $stagedDemo 'DOTNET-THIRD-PARTY-NOTICES.txt') -Force
foreach ($document in $publicDocs) {
    $relative = [System.IO.Path]::GetRelativePath($repo, $document.FullName)
    $destination = Join-Path $stagedDemo $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $destination) -Force | Out-Null
    Copy-Item -LiteralPath $document.FullName -Destination $destination -Force
}
$demoReadme = (Get-Content -Raw -LiteralPath (Join-Path $repo 'docs/DEMO_README.md')).Replace(
    '](SAFETY_AND_RECOVERY.md)', '](docs/SAFETY_AND_RECOVERY.md)').Replace(
    '](HARDWARE_VALIDATION.md)', '](docs/HARDWARE_VALIDATION.md)')
$demoReadme | Set-Content -LiteralPath (Join-Path $stagedDemo 'README.md')
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'launch-demo.ps1') -Destination $stagedLauncher -Force
if (Get-ChildItem -LiteralPath $stagedDemo -Recurse -Force | Where-Object {
    $relative = [System.IO.Path]::GetRelativePath($stagedDemo, $_.FullName).Replace('\', '/')
    $relative -match '(^|/)(profiles|evidence|bin|obj|\.git|\.build-home|\.packages|\.package-tests-[^/]+)(/|$)' -or $_.Extension -ieq '.pdb'
}) {
    throw 'Fresh publish contains private diagnostics, profiles, caches or debug symbols. Packaging was refused.'
}
$stage = Join-Path $repo ('.build-home/source-stage-' + [Guid]::NewGuid().ToString('N') + '/SafeSeat')
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$files = @(Get-ChildItem -LiteralPath $repo -Recurse -File -Force | Where-Object {
    $relative = [System.IO.Path]::GetRelativePath($repo, $_.FullName).Replace('\', '/')
    $outsideChosenOutput = -not $outputInsideRepo -or -not $_.FullName.StartsWith($outputPrefix, $comparison)
    $allowedDocumentation = -not $relative.StartsWith('docs/', $comparison) -or $publicDocPaths.Contains($relative)
    $outsideChosenOutput -and $allowedDocumentation -and
        $relative -notmatch '(^|/)(bin|obj|\.build-home|\.git|profiles|publish|\.packages|\.package-tests-[^/]+)(/|$)' -and
        $_.Extension -notin @('.zip', '.pdb')
})
foreach ($file in $files) {
    $relative = [System.IO.Path]::GetRelativePath($repo, $file.FullName)
    $target = Join-Path $stage $relative
    New-Item -ItemType Directory -Path (Split-Path -Parent $target) -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
}
$sourceZip = Join-Path $output 'SafeSeat-source.zip'
$demoZip = Join-Path $output 'SafeSeat-demo.zip'
Compress-Archive -LiteralPath $stage -DestinationPath $sourceZip -Force
Compress-Archive -LiteralPath $stagedDemo,$stagedLauncher -DestinationPath $demoZip -Force
Get-FileHash -LiteralPath $sourceZip,$demoZip -Algorithm SHA256 | ForEach-Object {
    "$($_.Hash.ToLowerInvariant())  $([System.IO.Path]::GetFileName($_.Path))"
} | Set-Content -LiteralPath (Join-Path $output 'SHA256SUMS.txt')
# Merge fresh release files into the public working copy only after creating the archives.
# Existing local profiles and unrelated files are preserved here, but were never archive inputs.
Copy-Item -LiteralPath $stagedDemo -Destination $output -Recurse -Force
Copy-Item -LiteralPath $stagedLauncher -Destination (Join-Path $output 'Run-SafeSeat.ps1') -Force
Write-Output "Packaged $($files.Count) public source/documentation files. Local diagnostics, profiles, caches and PDBs were excluded. No SDK or shared runtime was bundled."
Get-Content -LiteralPath (Join-Path $evidence 'package-smoke.txt')
