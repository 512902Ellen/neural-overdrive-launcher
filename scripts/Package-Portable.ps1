# Run after Build.ps1. Package the Windows x64 self-contained output.
param([switch]$Beta)

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$local = Join-Path $project '.local'
$build = Join-Path $local 'launcher/win-x64'
$exe = Get-Item -LiteralPath (Join-Path $build 'CodexBootAnimation.exe')
$version = $exe.VersionInfo.FileVersionRaw.ToString(3)
$channel = if ($Beta) { '-beta' } else { '' }
$name = "CodexBootAnimation-$version$channel-win-x64"
$output = New-Item -ItemType Directory -Path (Join-Path $local 'release') -Force
$destination = Join-Path $output.FullName "$name.zip"
$temporary = Join-Path $output.FullName "$name-$([guid]::NewGuid().ToString('N')).tmp"
$packages = Join-Path $local 'nuget/packages'
$sdk = (Get-Content -LiteralPath (Join-Path $project 'global.json') -Raw | ConvertFrom-Json).sdk.version
[xml]$launcher = Get-Content -LiteralPath (Join-Path $project 'src/Launcher/Launcher.csproj') -Raw
$runtime = $launcher.Project.PropertyGroup.RuntimeFrameworkVersion
$webview = ($launcher.Project.ItemGroup.PackageReference | Where-Object Include -eq 'Microsoft.Web.WebView2').Version.Trim('[', ']')

$notices = [ordered]@{
    'licenses/dotnet-runtime-license.txt' = Join-Path $packages "microsoft.netcore.app.runtime.win-x64/$runtime/LICENSE.TXT"
    'licenses/dotnet-runtime-third-party-notices.txt' = Join-Path $packages "microsoft.netcore.app.runtime.win-x64/$runtime/THIRD-PARTY-NOTICES.TXT"
    'licenses/windows-desktop-license.txt' = Join-Path $packages "microsoft.windowsdesktop.app.runtime.win-x64/$runtime/LICENSE"
    'licenses/windows-desktop-third-party-notices.txt' = Join-Path $local "sdk/sdk/$sdk/Sdks/Microsoft.NET.Sdk.WindowsDesktop/THIRD-PARTY-NOTICES.TXT"
    'licenses/webview2-sdk-license.txt' = Join-Path $packages "microsoft.web.webview2/$webview/LICENSE.txt"
    'licenses/webview2-sdk-notice.txt' = Join-Path $packages "microsoft.web.webview2/$webview/NOTICE.txt"
}
$docs = 'README.md', 'README.zh-CN.md', 'CHANGELOG.md', 'docs/windows-acceptance.md', 'docs/roadmap.md'
if ($Beta) {
    $docs += 'THIRD_PARTY_NOTICES.md', 'docs/releases/v0.2.0-beta.md', 'assets/demo/neural-overdrive.gif',
        'assets/demo/settings-en.png', 'assets/demo/settings-zh-CN.png', 'themes/neural-overdrive/reference/codex-cyber-boot-v03.html'
}
if (Test-Path -LiteralPath (Join-Path $project 'LICENSE') -PathType Leaf) { $docs += 'LICENSE' }

try {
    $archive = [IO.Compression.ZipFile]::Open($temporary, 'Create')
    try {
        function Add-Entry([string]$file, [string]$entry) {
            $null = [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file, "$name/$entry", 'Optimal')
        }

        Add-Entry $exe.FullName $exe.Name
        foreach ($file in Get-ChildItem -LiteralPath $build -File | Sort-Object Name) {
            if ($file.Extension -eq '.dll' -or $file.Name -match '^CodexBootAnimation\.(deps|runtimeconfig)\.json$') {
                Add-Entry $file.FullName $file.Name
            }
        }
        foreach ($directory in Get-ChildItem -LiteralPath $build -Directory | Sort-Object Name) {
            if ($directory.Name -notmatch '^[a-z]{2}(-[A-Za-z]+)?$' -and $directory.Name -ne 'runtimes') { continue }
            foreach ($file in Get-ChildItem -LiteralPath $directory.FullName -File -Recurse -Filter '*.dll' | Sort-Object FullName) {
                Add-Entry $file.FullName ([IO.Path]::GetRelativePath($build, $file.FullName).Replace('\', '/'))
            }
        }
        foreach ($file in 'theme/boot.html', 'theme/preview.png') { Add-Entry (Join-Path $build $file) $file }
        foreach ($file in $docs) { Add-Entry (Join-Path $project $file) $file }
        foreach ($notice in $notices.GetEnumerator()) { Add-Entry $notice.Value $notice.Key }
    }
    finally { $archive.Dispose() }
    [IO.File]::Move($temporary, $destination, $true)
    $hash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath "$destination.sha256" -Encoding ascii -Value "$hash  $name.zip"
    Get-Item -LiteralPath $destination | Select-Object FullName, Length
    Write-Output $hash
}
finally { [IO.File]::Delete($temporary) }
