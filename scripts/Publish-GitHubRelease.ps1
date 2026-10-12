$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repo = (Get-Content -LiteralPath (Join-Path $root 'release\github-repo-url.txt') -Raw).Trim().TrimEnd('/')
if ($repo -notmatch '^https://github\.com/[^/\s]+/[^/\s]+$') {
    throw 'Put your public GitHub repository URL in release\github-repo-url.txt first.'
}
$ghCommand = Get-Command gh -ErrorAction SilentlyContinue
$ghPath = if ($ghCommand) { $ghCommand.Source } else { Join-Path $root 'tools\gh.exe' }
if (-not (Test-Path -LiteralPath $ghPath -PathType Leaf)) {
    throw 'GitHub CLI was not found. Use the bundled tools\gh.exe or install it from https://cli.github.com/.'
}

$token = (& $ghPath auth token 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
    throw 'In the VS Code terminal, run .\tools\gh.exe auth login --web and finish GitHub sign-in.'
}
$repoName = $repo.Substring('https://github.com/'.Length)
$privateText = (& $ghPath repo view $repoName --json isPrivate --jq '.isPrivate')
if ($LASTEXITCODE -ne 0) { throw "GitHub repository $repoName was not found or is not accessible." }
if ($privateText.Trim() -ne 'false') { throw 'The GitHub repository must be public for anyone to download Seer and receive updates.' }
$branch = (& $ghPath repo view $repoName --json defaultBranchRef --jq '.defaultBranchRef.name')
if ($LASTEXITCODE -ne 0) { throw 'Could not check the repository default branch.' }
if ([string]::IsNullOrWhiteSpace($branch) -or $branch.Trim() -eq 'null') {
    $publicReadme = Get-Content -LiteralPath (Join-Path $root 'release\PUBLIC-README.md') -Raw
    $base64Readme = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($publicReadme))
    Write-Host 'Creating the first README commit in the empty public repository...'
    & $ghPath api --method PUT "repos/$repoName/contents/README.md" -f 'message=Add Seer download instructions' -f "content=$base64Readme" -f 'branch=main' --silent
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the first README commit in the GitHub repository.' }
}

$versionFile = Join-Path $root 'release\next-version.txt'
$version = (Get-Content -LiteralPath $versionFile -Raw).Trim()
if ($version -notmatch '^(?<major>[0-9]+)\.(?<minor>[0-9]+)\.(?<patch>[0-9]+)$') {
    throw 'release\next-version.txt must contain a version such as 1.0.0.'
}
$major = [int]$Matches.major
$minor = [int]$Matches.minor
$patch = [int]$Matches.patch
$dist = Join-Path $root 'dist'
$name = 'Seer-' + $version + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 6)
$releaseDir = Join-Path $dist $name

& (Join-Path $PSScriptRoot 'Build-GitHubInstaller.ps1') -OutputDir $releaseDir
if ($LASTEXITCODE -ne 0) { throw "Installer build failed: $LASTEXITCODE" }

$previousToken = $env:VPK_TOKEN
try {
    $env:VPK_TOKEN = $token.Trim()
    Push-Location $root
    try {
        Write-Host "Publishing Seer $version to GitHub Releases..."
        & dotnet tool run vpk -- upload github --outputDir (Join-Path $releaseDir 'packages') --repoUrl $repo --publish true --tag "v$version"
        if ($LASTEXITCODE -ne 0) { throw "GitHub upload failed: $LASTEXITCODE" }
    }
    finally { Pop-Location }
}
finally {
    $env:VPK_TOKEN = $previousToken
    $token = $null
}

$nextVersion = "$major.$minor.$($patch + 1)"
Set-Content -LiteralPath $versionFile -Value $nextVersion -NoNewline
Write-Host "Published: $repo/releases/tag/v$version"
Write-Host "Public download page: $repo/releases/latest"
Write-Host "Next version is now $nextVersion. Installed copies will check for updates while running."
