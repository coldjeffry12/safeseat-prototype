[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$files = Get-ChildItem -LiteralPath (Join-Path $repo 'src') -Recurse -File -Filter '*.cs'
$prohibited = @('SendInput\s*\(', 'PostMessage\w*\s*\(', 'SetForegroundWindow\s*\(',
    'SetWindowsHookEx\w*\s*\(', 'BlockInput\s*\(', 'ClipCursor\s*\(', 'SetCursorPos\s*\(',
    'WriteProcessMemory\s*\(', 'CreateRemoteThread\s*\(', 'NtLoadDriver\s*\(',
    'Microsoft\.Win32\.Registry\.', 'TcpListener', 'HttpListener', 'File\.AppendAllText')
$hits = @($files | Select-String -Pattern $prohibited)
if ($hits.Count -gt 0) { $hits | ForEach-Object { Write-Output $_ }; throw 'Prohibited API pattern found.' }
$native = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/SafeSeat.Windows/Native.cs')
if ($native -match 'RidevNoLegacy|RidevCaptureMouse|RidevNoHotKeys|RidevInputSink') { throw 'Input suppression or background observation flag found.' }
Write-Output 'PASS: source scan found no prohibited injection, suppression, hooking, registry-write or listener API patterns.'
Write-Output 'PASS: manifest requests asInvoker and uiAccess=false.'
$manifest = [xml](Get-Content -Raw (Join-Path $repo 'src/SafeSeat.App/app.manifest'))
if ($manifest.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.level -ne 'asInvoker' -or
    $manifest.assembly.trustInfo.security.requestedPrivileges.requestedExecutionLevel.uiAccess -ne 'false') { throw 'Unexpected elevation manifest.' }
Write-Output 'LIMIT: a source-pattern scan is supporting evidence, not proof of OS integrity or physical safety.'
