# Shared, process-local apphost launch support. Dot-sourcing this file starts no process.
function ConvertTo-SafeSeatProcessArgument {
    param([AllowEmptyString()][string]$Value)
    if ($Value.IndexOf([char]0) -ge 0) { throw 'A process argument contains an invalid NUL character.' }
    # Windows command-line quoting for Windows PowerShell/.NET Framework, where ArgumentList
    # is unavailable. Backslashes before quotes and the final closing quote must be doubled.
    $quoted = New-Object System.Text.StringBuilder
    [void]$quoted.Append('"')
    $slashes = 0
    foreach ($character in $Value.ToCharArray()) {
        if ($character -eq '\') { $slashes++; continue }
        if ($character -eq '"') {
            [void]$quoted.Append(('\' * (2 * $slashes + 1)))
            [void]$quoted.Append('"')
        }
        else {
            [void]$quoted.Append(('\' * $slashes))
            [void]$quoted.Append($character)
        }
        $slashes = 0
    }
    [void]$quoted.Append(('\' * (2 * $slashes)))
    [void]$quoted.Append('"')
    return $quoted.ToString()
}

function Invoke-SafeSeatAppHost {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$ExecutablePath,
        [Parameter(Mandatory = $true)][string]$DotnetPath,
        [string[]]$Arguments = @()
    )
    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        throw 'SafeSeat.exe is missing. Build or extract the complete SafeSeat package first.'
    }
    if (Test-Path -LiteralPath $DotnetPath -PathType Leaf) {
        $runtimeHost = (Resolve-Path -LiteralPath $DotnetPath -ErrorAction Stop).ProviderPath
    }
    else {
        $commands = @(Get-Command -Name $DotnetPath -CommandType Application -ErrorAction Stop)
        if ($commands.Count -ne 1) { throw 'Specify exactly one existing dotnet runtime host path.' }
        $runtimeHost = $commands[0].Source
    }
    $appHost = (Resolve-Path -LiteralPath $ExecutablePath -ErrorAction Stop).ProviderPath
    $runtimeRoot = Split-Path -Parent $runtimeHost
    $start = New-Object System.Diagnostics.ProcessStartInfo
    $start.FileName = $appHost
    $start.WorkingDirectory = Split-Path -Parent $appHost
    $start.UseShellExecute = $false
    # Preserve the executable's PerMonitorV2/asInvoker manifest. Only this child receives the roots.
    $start.EnvironmentVariables['DOTNET_ROOT'] = $runtimeRoot
    $start.EnvironmentVariables['DOTNET_ROOT_X64'] = $runtimeRoot
    if ($start.PSObject.Properties.Name -contains 'ArgumentList') {
        foreach ($argument in $Arguments) {
            if ($null -eq $argument -or $argument.IndexOf([char]0) -ge 0) { throw 'Invalid process argument.' }
            $start.ArgumentList.Add($argument)
        }
    }
    else {
        $start.Arguments = (($Arguments | ForEach-Object { ConvertTo-SafeSeatProcessArgument -Value $_ }) -join ' ')
    }
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $start
    try {
        try {
            if (-not $process.Start()) { throw 'Windows did not create the SafeSeat process.' }
        }
        catch {
            $launchError = $_.Exception
            while ($launchError.InnerException) { $launchError = $launchError.InnerException }
            if ($launchError -is [System.ComponentModel.Win32Exception]) {
                throw "Windows refused to start SafeSeat.exe (Win32 $($launchError.NativeErrorCode)): $($launchError.Message). Launcher stopped; follow the applicable Windows application policy."
            }
            throw "SafeSeat.exe could not start: $($launchError.Message)"
        }
        $process.WaitForExit()
        return $process.ExitCode
    }
    finally { $process.Dispose() }
}
