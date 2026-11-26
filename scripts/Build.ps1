[CmdletBinding()]
param(
    [switch]$Restore,
    [switch]$UpdateLock,
    [switch]$Test,
    [switch]$RestoreOnly
)

$ErrorActionPreference = 'Stop'
if (($UpdateLock -or $RestoreOnly) -and -not $Restore) {
    throw '-UpdateLock and -RestoreOnly require -Restore.'
}
$project = Split-Path -Parent $PSScriptRoot
$local = Join-Path $project '.local'
$dotnet = Join-Path $local 'sdk/dotnet.exe'
if (-not (Test-Path -LiteralPath $dotnet -PathType Leaf)) {
    throw 'Run scripts/Install-LocalSdk.ps1 to provision the pinned project SDK.'
}

$settings = @{
    DOTNET_CLI_HOME = Join-Path $local 'dotnet-home'
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
    DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
    DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
    MSBUILDDISABLENODEREUSE = '1'
    NUGET_PACKAGES = Join-Path $local 'nuget/packages'
    NUGET_HTTP_CACHE_PATH = Join-Path $local 'nuget/http-cache'
    NUGET_PLUGINS_CACHE_PATH = Join-Path $local 'nuget/plugins-cache'
    NUGET_SCRATCH = Join-Path $local 'nuget/scratch'
    CODEX_BOOT_TEST_ROOT = Join-Path $local 'test-data'
}
$original = @{}
$launcher = 'src/Launcher/Launcher.csproj'
$tests = 'tests/Launcher.Tests/Launcher.Tests.csproj'
$output = Join-Path $local 'launcher/win-x64'

function Invoke-Dotnet {
    & $dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet $($args[0]) failed with exit code $LASTEXITCODE." }
}

Push-Location -LiteralPath $project
try {
    foreach ($item in $settings.GetEnumerator()) {
        $original[$item.Key] = [Environment]::GetEnvironmentVariable($item.Key, 'Process')
        [Environment]::SetEnvironmentVariable($item.Key, $item.Value, 'Process')
    }
    Invoke-Dotnet --version
    if ($Restore) {
        $lock = '--locked-mode'
        if ($UpdateLock) { $lock = '--force-evaluate' }
        Invoke-Dotnet restore $launcher --configfile NuGet.Config $lock
        if ($Test) { Invoke-Dotnet restore $tests --configfile NuGet.Config }
    }
    if ($RestoreOnly) { return }
    Invoke-Dotnet publish $launcher -c Release --no-restore --disable-build-servers -o $output
    if ($Test) {
        Invoke-Dotnet run --project $tests -c Release --no-restore --disable-build-servers
        & node --test tests/theme.test.cjs
        if ($LASTEXITCODE -ne 0) { throw 'Theme tests failed.' }
    }
    Write-Host "Launcher executable at $(Join-Path $output 'CodexBootAnimation.exe')"
}
finally {
    foreach ($item in $original.GetEnumerator()) {
        [Environment]::SetEnvironmentVariable($item.Key, $item.Value, 'Process')
    }
    Pop-Location
}
