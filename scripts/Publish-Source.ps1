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
    & git push --set-upstream origin main
    if ($LASTEXITCODE -ne 0) { throw 'Could not upload Seer source to GitHub. The local commit is saved; retry this task.' }
    Write-Host "Source published: $repo"
}
finally {
    Pop-Location
}
