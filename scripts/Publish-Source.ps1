param(
    [string]$CommitMessage = 'Update Seer source'
)

$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$repo = (Get-Content -LiteralPath (Join-Path $root 'release\github-repo-url.txt') -Raw).Trim().TrimEnd('/')
if (-not (Test-Path -LiteralPath (Join-Path $root '.git'))) {
    throw 'This source folder is not connected to Git. Open the Git-connected Recorder-source folder to publish source changes.'
}

Push-Location $root
try {
    $remote = ((& git remote get-url origin).Trim().TrimEnd('/') -replace '\.git$', '')
    if ($LASTEXITCODE -ne 0 -or $remote -ne $repo) {
        throw "Git origin must be $repo before publishing source."
    }
    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') {
        throw 'Switch to the main branch before publishing Seer source.'
    }

    # Only source, documentation, and project settings are candidates. Git's
    # ignore rules keep build products, packages, tools, and signing keys out.
    $paths = @(
        '.github', '.gitignore', '.vscode', 'GITHUB-RELEASE.md', 'LICENSE',
        'NuGet.Config', 'PRIVACY.md', 'README.md', 'Recorder.sln', 'Recorder',
        'SIGNING.md', 'START-HERE.md', 'dotnet-tools.json', 'release', 'scripts'
    )
    & git add --all -- $paths
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage Seer source files.' }

    $staged = @(& git diff --cached --name-only)
    foreach ($path in $staged) {
        $allowed = $path -match '^(Recorder/[^/]+\.(cs|csproj|resx)|scripts/[^/]+\.ps1|release/(PUBLIC-README\.md|github-repo-url\.txt|next-version\.txt|Seer\.ico)|\.vscode/[^/]+\.json|\.github/workflows/[^/]+\.ya?ml)$' -or
            $path -in @('.gitignore', 'GITHUB-RELEASE.md', 'LICENSE', 'NuGet.Config', 'PRIVACY.md', 'README.md', 'Recorder.sln', 'SIGNING.md', 'START-HERE.md', 'dotnet-tools.json')
        if (-not $allowed) {
            & git restore --staged -- $path
            throw "Review and add this new file manually before publishing: $path"
        }
    }
    & git diff --cached --check
    if ($LASTEXITCODE -ne 0) { throw 'Fix the staged whitespace errors before publishing source.' }

    $untracked = @(& git ls-files --others --exclude-standard)
    if ($untracked.Count -gt 0) {
        throw "Review these new files before publishing: $($untracked -join ', ')"
    }

    if ($staged.Count -gt 0) {
        & git commit -m $CommitMessage
        if ($LASTEXITCODE -ne 0) { throw 'Could not commit the source changes.' }
    }
    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    $ghPath = if ($ghCommand) { $ghCommand.Source } else { Join-Path $root 'tools\gh.exe' }
    if (-not (Test-Path -LiteralPath $ghPath -PathType Leaf)) {
        throw 'GitHub CLI is missing. Install it from https://cli.github.com/ and sign in before publishing source.'
    }
    $token = & $ghPath auth token 2>$null
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($token)) {
        throw 'Run the Sign in to GitHub (one time) task before publishing source.'
    }

    # Supply the credential to this one Git process without putting it in a
    # command line, Git remote URL, project file, or Git credential helper.
    $previousCount = [Environment]::GetEnvironmentVariable('GIT_CONFIG_COUNT', 'Process')
    $index = if ([string]::IsNullOrWhiteSpace($previousCount)) { 0 } else { [int]$previousCount }
    $keyName = "GIT_CONFIG_KEY_$index"
    $valueName = "GIT_CONFIG_VALUE_$index"
    $previousKey = [Environment]::GetEnvironmentVariable($keyName, 'Process')
    $previousValue = [Environment]::GetEnvironmentVariable($valueName, 'Process')
    try {
        $basic = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes("x-access-token:$($token.Trim())"))
        [Environment]::SetEnvironmentVariable('GIT_CONFIG_COUNT', ($index + 1).ToString(), 'Process')
        [Environment]::SetEnvironmentVariable($keyName, 'http.https://github.com/.extraheader', 'Process')
        [Environment]::SetEnvironmentVariable($valueName, "AUTHORIZATION: basic $basic", 'Process')
        & git push --set-upstream origin main
        if ($LASTEXITCODE -ne 0) { throw 'Could not upload Seer source to GitHub. The local commit is saved; retry this task.' }
    }
    finally {
        [Environment]::SetEnvironmentVariable('GIT_CONFIG_COUNT', $previousCount, 'Process')
        [Environment]::SetEnvironmentVariable($keyName, $previousKey, 'Process')
        [Environment]::SetEnvironmentVariable($valueName, $previousValue, 'Process')
        $token = $null
        $basic = $null
    }
    Write-Host "Source published: $repo"
}
finally {
    Pop-Location
}
