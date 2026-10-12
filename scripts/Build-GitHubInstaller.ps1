param(
    [string]$RepositoryUrl,
    [string]$OutputDir
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project = Join-Path $root 'Recorder\Recorder.csproj'
$version = (Get-Content -LiteralPath (Join-Path $root 'release\next-version.txt') -Raw).Trim()
if ($version -notmatch '^[0-9]+\.[0-9]+\.[0-9]+$') {
    throw 'release\next-version.txt must contain a version such as 1.0.0.'
}

if ([string]::IsNullOrWhiteSpace($RepositoryUrl)) {
    $RepositoryUrl = (Get-Content -LiteralPath (Join-Path $root 'release\github-repo-url.txt') -Raw).Trim()
}
if ($RepositoryUrl -notmatch '^https://github\.com/[^/\s]+/[^/\s]+/?$') {
    throw 'Create a public GitHub repository, then put its URL in release\github-repo-url.txt before building an installer.'
}
$RepositoryUrl = $RepositoryUrl.TrimEnd('/')

$dist = Join-Path $root 'dist'
if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $name = 'Seer-' + $version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
    $OutputDir = Join-Path $dist $name
}
$releaseDir = [System.IO.Path]::GetFullPath($OutputDir)
$distFull = [System.IO.Path]::GetFullPath($dist)
if ([System.IO.Path]::GetDirectoryName($releaseDir) -ne $distFull) {
    throw "The output folder must be directly inside $distFull"
}
if (Test-Path -LiteralPath $releaseDir) { throw "Release folder already exists: $releaseDir" }

$publishDir = Join-Path $releaseDir 'publish'
$packagesDir = Join-Path $releaseDir 'packages'
New-Item -ItemType Directory -Path $publishDir, $packagesDir -Force | Out-Null

Push-Location $root
try {
    Write-Host "Restoring the pinned Velopack packaging tool..."
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed: $LASTEXITCODE" }

    Write-Host "Building Seer $version for Windows x64..."
    & dotnet publish $project --configuration Release --runtime win-x64 --self-contained true --output $publishDir --nologo "-p:Version=$version" -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
    if (-not (Test-Path -LiteralPath (Join-Path $publishDir 'Recorder.exe') -PathType Leaf)) {
        throw 'Publish finished without Recorder.exe.'
    }

    # The installed app reads this exact repository URL to find future releases.
    Set-Content -LiteralPath (Join-Path $publishDir 'github-repo-url.txt') -Value $RepositoryUrl -NoNewline

    Write-Host 'Packing the Windows installer and update feed...'
    $oldVeloTemp = $env:VELOPACK_TEMP
    $oldTemp = $env:TEMP
    $oldTmp = $env:TMP
    try {
        $localTemp = Join-Path $releaseDir 'velopack-temp'
        New-Item -ItemType Directory -Path $localTemp -Force | Out-Null
        $env:VELOPACK_TEMP = $localTemp
        $env:TEMP = $localTemp
        $env:TMP = $localTemp
        & dotnet tool run vpk -- pack --packId 'Seer.ScreenRecorder' --packVersion $version --packDir $publishDir --mainExe 'Recorder.exe' --packTitle 'Seer' --runtime 'win-x64' --outputDir $packagesDir --icon (Join-Path $root 'release\Seer.ico') --noPortable
        if ($LASTEXITCODE -ne 0) { throw "vpk pack failed: $LASTEXITCODE" }
    }
    finally {
        $env:VELOPACK_TEMP = $oldVeloTemp
        $env:TEMP = $oldTemp
        $env:TMP = $oldTmp
    }
}
finally {
    Pop-Location
}

$installer = Get-ChildItem -LiteralPath $packagesDir -Filter '*Setup.exe' -File | Select-Object -First 1
if (-not $installer) { throw "No Setup.exe was produced in $packagesDir" }
Write-Host "Installer: $($installer.FullName)"
Write-Host "Update packages: $packagesDir"
Write-Host 'The installer is ready for local review. It becomes public only after the GitHub release is published.'
