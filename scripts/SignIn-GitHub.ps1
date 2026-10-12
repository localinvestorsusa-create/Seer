$ErrorActionPreference = 'Stop'
$root = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ghCommand = Get-Command gh -ErrorAction SilentlyContinue
$ghPath = if ($ghCommand) { $ghCommand.Source } else { Join-Path $root 'tools\gh.exe' }
if (-not (Test-Path -LiteralPath $ghPath -PathType Leaf)) {
    throw 'Install GitHub CLI from https://cli.github.com/ and then rerun this task.'
}
& $ghPath auth login --hostname github.com --git-protocol https --web
if ($LASTEXITCODE -ne 0) { throw "GitHub sign-in failed: $LASTEXITCODE" }
