# Run from an interactive Windows session. Cold and missing-client tests belong in an isolated session.
[CmdletBinding()]
param(
    [ValidateSet('Preview', 'Warm', 'Cold', 'ThemeFailure', 'WebViewFailure')]
    [string]$Scenario = 'Preview',
    [ValidateSet('Quick', 'Full', 'Direct')]
    [string]$Mode = 'Quick',
    [switch]$Duplicate
)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$build = Join-Path $project '.local/mvp/win-x64'
$run = $build
$exeName = 'CodexBootAnimation.exe'
if (-not (Test-Path -LiteralPath (Join-Path $build $exeName))) {
    throw 'Build the MVP first with scripts/Build.ps1.'
}
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
