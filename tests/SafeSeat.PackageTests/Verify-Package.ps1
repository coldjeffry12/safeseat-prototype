#requires -Version 7.0
[CmdletBinding()]
param([string]$DotnetPath = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$package = Join-Path $repo 'scripts/package.ps1'
if (-not (Test-Path -LiteralPath (Join-Path $repo 'src/SafeSeat.App/bin/Release/net8.0-windows/SafeSeat.exe') -PathType Leaf)) {
    throw 'Build the current public source first with scripts/build.ps1 -SkipUiSmoke. This test never builds or launches the GUI.'
}
$dotnetCommand = (Get-Command $DotnetPath -CommandType Application -ErrorAction Stop).Source
$id = [Guid]::NewGuid().ToString('N')
$scratch = Join-Path $repo ('.build-home/package-tests-' + $id)
$standardOutput = Join-Path $scratch 'standard-output'
$customOutput = Join-Path $repo ('.package-tests-' + $id)
$privateDirectory = Join-Path $repo ('docs/evidence/package-tests-' + $id)
$sentinel = 'SYNTHETIC_PRIVATE_EVIDENCE_' + $id
$privateEvidence = Join-Path $privateDirectory 'private-machine-report.txt'
$profileText = '{"syntheticPrivateProfile":"' + $id + '"}'
$staleText = 'SYNTHETIC_LOCAL_STALE_DLL_' + $id
New-Item -ItemType Directory -Path $scratch,$privateDirectory -Force | Out-Null
Set-Content -LiteralPath $privateEvidence -Value $sentinel -NoNewline

$checks = 0
function Assert-Package {
    param([bool]$Condition, [string]$Message)
    if (-not $Condition) { throw ('FAIL: package privacy: ' + $Message) }
    $script:checks++
}

function Test-Archives {
    param([string]$Output, [string]$CustomDirectoryName)
    $sourcePath = Join-Path $Output 'SafeSeat-source.zip'
    $demoPath = Join-Path $Output 'SafeSeat-demo.zip'
    Assert-Package (Test-Path -LiteralPath $sourcePath -PathType Leaf) 'source archive exists'
    Assert-Package (Test-Path -LiteralPath $demoPath -PathType Leaf) 'demo archive exists'
    foreach ($archivePath in @($sourcePath,$demoPath)) {
        $archive = [System.IO.Compression.ZipFile]::OpenRead($archivePath)
        try {
            $names = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
            Assert-Package (@($names | Where-Object { $_ -match '(^|/)(evidence|profiles|bin|obj|\.git|\.build-home|\.packages|\.package-tests-[^/]+)(/|$)' }).Count -eq 0) 'archive excludes private evidence, profiles and cache directories'
            Assert-Package (@($names | Where-Object { $_ -match '(?i)\.pdb$' }).Count -eq 0) 'archive excludes PDB files'
            Assert-Package (@($names | Where-Object { $_.EndsWith('/stale-local.dll', [System.StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) 'archive does not reuse existing local DLLs'
            if ($CustomDirectoryName) {
                Assert-Package (@($names | Where-Object { $_.Contains($CustomDirectoryName, [System.StringComparison]::OrdinalIgnoreCase) }).Count -eq 0) 'custom output never becomes a source/archive input'
            }
            foreach ($entry in $archive.Entries) {
                if ($entry.Length -eq 0) { continue }
                $stream = $entry.Open()
                $reader = [System.IO.StreamReader]::new($stream)
                try { $contents = $reader.ReadToEnd() }
                finally { $reader.Dispose() }
                Assert-Package (-not $contents.Contains($sentinel, [System.StringComparison]::Ordinal)) 'synthetic private evidence content excluded'
                Assert-Package (-not $contents.Contains($profileText, [System.StringComparison]::Ordinal)) 'existing local profile content excluded'
                Assert-Package (-not $contents.Contains($staleText, [System.StringComparison]::Ordinal)) 'existing local stale DLL content excluded'
                if ($entry.FullName -match '(?i)\.(dll|exe)$') {
                    # PE CodeView records can retain a source-machine PDB path even when the
                    # separate PDB file is omitted. Binary inspection never executes the file.
                    Assert-Package (-not $contents.Contains($repo, [System.StringComparison]::OrdinalIgnoreCase)) 'published binary excludes the local repository path'
                }
            }
        }
        finally { $archive.Dispose() }
    }
    $source = [System.IO.Compression.ZipFile]::OpenRead($sourcePath)
    try {
        Assert-Package (@($source.Entries | Where-Object { $_.FullName.Replace('\', '/').EndsWith('/src/SafeSeat.Core/Settings.cs', [System.StringComparison]::Ordinal) }).Count -eq 1) 'source archive contains actual application source'
    }
    finally { $source.Dispose() }
    $demo = [System.IO.Compression.ZipFile]::OpenRead($demoPath)
    try {
        Assert-Package (@($demo.Entries | Where-Object { $_.FullName.Replace('\', '/').EndsWith('/SafeSeat.exe', [System.StringComparison]::Ordinal) }).Count -eq 1) 'demo archive contains the apphost without executing it'
    }
    finally { $demo.Dispose() }
    Assert-Package (@(Get-ChildItem -LiteralPath (Join-Path $Output 'SafeSeat-demo') -Recurse -File -Filter '*.pdb').Count -eq 0) 'fresh public working folder contains no PDB'
    Assert-Package (-not (Test-Path -LiteralPath (Join-Path $Output 'SafeSeat-demo/docs/evidence'))) 'fresh working copy did not receive private diagnostics'
}

# Existing user-local files must survive packaging, but must never become archive inputs.
foreach ($output in @($standardOutput,$customOutput)) {
    $profileDirectory = Join-Path $output 'SafeSeat-demo/profiles'
    New-Item -ItemType Directory -Path $profileDirectory -Force | Out-Null
    $profile = Join-Path $profileDirectory 'default.json'
    $stale = Join-Path $output 'SafeSeat-demo/stale-local.dll'
    Set-Content -LiteralPath $profile -Value $profileText -NoNewline
    Set-Content -LiteralPath $stale -Value $staleText -NoNewline
    & $package -DotnetPath $dotnetCommand -OutputRoot $output -SkipBuild -SkipGuiSmoke
    Assert-Package ((Get-Content -LiteralPath $profile -Raw) -ceq $profileText) 'existing local profile preserved exactly'
    Assert-Package ((Get-Content -LiteralPath $stale -Raw) -ceq $staleText) 'existing local stale DLL preserved exactly'
    Test-Archives -Output $output -CustomDirectoryName ([System.IO.Path]::GetFileName($customOutput))
}
Assert-Package ((Get-Content -LiteralPath $privateEvidence -Raw) -ceq $sentinel) 'private evidence fixture preserved without deletion'
Write-Output ('PASS: ' + $checks + ' package privacy checks; source/demo archives exclude synthetic private diagnostics, profiles, caches and PDBs.')
Write-Output 'Fixture/output directories were preserved. No GUI, apphost, probe or input observer was launched.'
