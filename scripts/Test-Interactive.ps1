# Run from an interactive Windows session. Cold and missing-client tests belong in an isolated session.
[CmdletBinding()]
param(
    [ValidateSet('Preview', 'Warm', 'Cold', 'ThemeFailure', 'WebViewFailure')]
    [string]$Scenario = 'Preview',
    [ValidateSet('Quick', 'Full', 'Direct')]
    [string]$Mode = 'Quick',
    [switch]$Duplicate,
    [string]$LauncherPath
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$exeName = 'NeuralOverdriveLauncher.exe'
if (-not $LauncherPath) { $LauncherPath = Join-Path $project ".local/neural-overdrive-launcher/win-x64/$exeName" }
if (-not (Test-Path -LiteralPath $LauncherPath -PathType Leaf) -or (Split-Path -Leaf $LauncherPath) -ne $exeName) {
    throw 'Provide the path to NeuralOverdriveLauncher.exe or run scripts/Build.ps1.'
}
$build = Split-Path -Parent (Resolve-Path -LiteralPath $LauncherPath).Path
$run = $build
if ($Mode -eq 'Direct' -and $Scenario -notin 'Warm', 'Cold') {
    throw 'Direct mode is available for the Warm and Cold scenarios.'
}

if ($Scenario -in 'ThemeFailure', 'WebViewFailure') {
    $run = Join-Path $project ".local/acceptance/$([guid]::NewGuid().ToString('N'))"
    New-Item -ItemType Directory -Path $run -Force | Out-Null
    Get-ChildItem -LiteralPath $build -Force | Where-Object Name -ne '.launcher-data' |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $run -Recurse }
    if ($Scenario -eq 'ThemeFailure') {
        Set-Content -LiteralPath (Join-Path $run 'theme/boot.html') -Value 'Intentional test-only corruption'
    }
}

$start = [System.Diagnostics.ProcessStartInfo]::new((Join-Path $run $exeName))
$start.UseShellExecute = $false
$start.ArgumentList.Add("--$($Mode.ToLowerInvariant())")
$start.ArgumentList.Add('--diagnostics')
if ($Scenario -eq 'Preview') { $start.ArgumentList.Add('--preview') }
if ($Scenario -eq 'WebViewFailure') {
    $emptyRuntime = Join-Path $run 'empty-webview-runtime'
    New-Item -ItemType Directory -Path $emptyRuntime | Out-Null
    $start.Environment['WEBVIEW2_BROWSER_EXECUTABLE_FOLDER'] = $emptyRuntime
}

$launched = [DateTime]::UtcNow
$first = [System.Diagnostics.Process]::Start($start)
if ($Duplicate) {
    Start-Sleep -Milliseconds 350
    $second = [System.Diagnostics.Process]::Start($start)
}
if (-not $first.WaitForExit(25000)) { throw 'Launcher is still open. Check its window in the interactive session.' }
Write-Output "$Scenario $Mode exit code $($first.ExitCode)"
$first.Dispose()
if ($Duplicate) {
    if (-not $second.WaitForExit(1000)) { throw 'Second launcher is still open.' }
    Write-Output "Second invocation exit code $($second.ExitCode)"
    $second.Dispose()
}
$logs = Join-Path $run '.launcher-data/logs'
Get-ChildItem -LiteralPath $logs -Filter 'launch-*.log' | Where-Object LastWriteTimeUtc -ge $launched |
    Sort-Object CreationTimeUtc | ForEach-Object { Get-Content -LiteralPath $_.FullName }
