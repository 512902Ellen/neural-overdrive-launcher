# Downloads the pinned Microsoft SDK ZIP and verifies the official SHA-512 before extraction.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
$version = (Get-Content -Raw -LiteralPath (Join-Path $project 'global.json') | ConvertFrom-Json).sdk.version
$local = Join-Path $project '.local'
$destination = Join-Path $local 'sdk'
$dotnet = Join-Path $destination 'dotnet.exe'
if ((Test-Path -LiteralPath $dotnet) -and
    ((& $dotnet --list-sdks) -match ('^' + [regex]::Escape($version) + ' '))) {
    Write-Host "Local .NET SDK $version is already available."
    return
}

$archive = Join-Path $local "dotnet-sdk-$version-win-x64.zip"
$url = "https://builds.dotnet.microsoft.com/dotnet/Sdk/$version/dotnet-sdk-$version-win-x64.zip"
$metadata = Invoke-RestMethod -Uri 'https://dotnetcli.blob.core.windows.net/dotnet/release-metadata/10.0/releases.json'
$file = $metadata.releases | ForEach-Object sdks | Where-Object version -eq $version |
    ForEach-Object files | Where-Object { $_.rid -eq 'win-x64' -and $_.name -like '*.zip' } |
    Select-Object -First 1
if ($file.url -ne $url -or $file.hash -notmatch '^[a-f0-9]{128}$') {
    throw "Microsoft release metadata did not match SDK $version."
}
New-Item -ItemType Directory -Force -Path $local | Out-Null
Invoke-WebRequest -Uri $url -OutFile $archive
if ((Get-FileHash -LiteralPath $archive -Algorithm SHA512).Hash -ine $file.hash) {
    throw 'SDK checksum mismatch.'
}
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::ExtractToDirectory($archive, $destination, $true)
if (-not ((& $dotnet --list-sdks) -match ('^' + [regex]::Escape($version) + ' '))) {
    throw "Local SDK $version was not found after extraction."
}
Remove-Item -LiteralPath $archive
Write-Host "Local .NET SDK $version installed."
