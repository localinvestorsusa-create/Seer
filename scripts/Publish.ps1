$ErrorActionPreference = 'Stop'

$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$projectFile = Join-Path $projectRoot 'Recorder\Recorder.csproj'
$distFolder = Join-Path $projectRoot 'dist'
$releaseName = 'Seer-app-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$appFolder = Join-Path $distFolder $releaseName
$zipFile = Join-Path $distFolder ($releaseName + '.zip')

if (-not (Test-Path -LiteralPath $projectFile -PathType Leaf)) {
    throw "Recorder.csproj was not found at $projectFile"
}

# Each build gets a new folder so a running older version is not disturbed.
$distFull = [System.IO.Path]::GetFullPath($distFolder)
$appFull = [System.IO.Path]::GetFullPath($appFolder)
if ([System.IO.Path]::GetDirectoryName($appFull) -ne $distFull) {
    throw "Unsafe publish folder: $appFull"
}

New-Item -ItemType Directory -Path $distFolder -Force | Out-Null
if (Test-Path -LiteralPath $appFolder) { throw "Release folder already exists: $appFolder" }

Write-Host 'Restoring Recorder dependencies...'
& dotnet restore $projectFile --runtime win-x64 --ignore-failed-sources --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet restore failed with exit code $LASTEXITCODE"
}

Write-Host 'Publishing Recorder for Windows...'
& dotnet publish $projectFile --configuration Release --runtime win-x64 --no-restore --self-contained true --output $appFolder --nologo
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

if (-not (Test-Path -LiteralPath (Join-Path $appFolder 'Recorder.exe') -PathType Leaf)) {
    throw 'Publish finished without Recorder.exe.'
}

@'
Seer for Windows x64

1. Run Recorder.exe in this folder. Keep the other files beside it.
2. Click Install FFmpeg once, or choose your existing ffmpeg.exe.
3. Press right Alt to start recording. Click the black square to stop.
4. Recordings are saved in your Windows Downloads folder.

This ZIP is a local copy and does not update automatically. For an installable
version that checks GitHub Releases for updates, use the Seer Setup.exe build.
'@ | Set-Content -LiteralPath (Join-Path $appFolder 'RUN-SEER.txt') -Encoding UTF8

Compress-Archive -LiteralPath $appFolder -DestinationPath $zipFile
Write-Host "App folder: $appFolder"
Write-Host "ZIP package: $zipFile"
Write-Host 'Done. Extract the ZIP to use the updated app on Windows x64.'
