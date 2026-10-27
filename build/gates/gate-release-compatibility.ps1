#Requires -Version 7.0
<#
.SYNOPSIS
    Fails a change whose release version is below what it needs (04 MSG-LIB-006, 07 MSG-ENG-012).
.DESCRIPTION
    Compares the current set (the public surface, the routes with their methods and statuses, the
    JSON bodies' members and types, the codes, the configuration keys, the spellings and the
    metric names and kinds, as the contract tests read them) with the baseline: the
    build/gates/release-baseline.json and PublicAPI.Shipped.txt files Base holds. Before the first
    release no tag holds a baseline, so it is empty and nothing is a break.

    On a release tag, the tag's version must be above the previous release's by what the change
    needs: a removal or a change, a request member a caller must now send, or a member added to
    an interface a host implements without a default implementation needs a major release; an
    addition a minor one. Anywhere else (a pull request, a push), the version is a pre-release, and
    a change that needs a major release fails unless a commit since the last release carries ! or
    BREAKING CHANGE: (07 MSG-ENG-011).
.PARAMETER Repository
    The repository to check; by default, this one.
.PARAMETER Base
    The commit whose baseline the change is compared with: the previous release tag, or a pull
    request's base.
.PARAMETER Head
    The commit checked.
.PARAMETER Needs
    A needs.json the comparison already wrote, read in place of building and comparing; for the
    gate's own tests.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [string] $Base = '',
    [string] $Head = 'HEAD',
    [string] $Needs = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Repository = (Resolve-Path $Repository).Path
$commit = & git -C $Repository rev-parse --verify --quiet "$Head^{commit}"
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw "-Head $Head names no commit." }

$release = '^v(\d+)\.(\d+)\.(\d+)$'
$tagged = @(& git -C $Repository tag --points-at $commit | Where-Object { $_ -cmatch $release } | Sort-Object -Descending { [version]$_.Substring(1) })
$previous = [string](& (Join-Path $PSScriptRoot 'previous-release-tag.ps1') -Repository $Repository -Head $commit)

if (-not $previous) {
    Write-Host 'No release precedes Head, so the baseline is empty and nothing is a break.'
    return
}

if (-not $Needs) {
    if (-not $Base) { throw "A release ($previous) precedes Head, so -Base must name the commit whose baseline the change is compared with." }

    $folder = Join-Path $Repository 'tmp' 'release-gate'
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    New-Item -ItemType Directory -Force (Join-Path $folder 'shipped') | Out-Null

    $baseline = & git -C $Repository show "${Base}:build/gates/release-baseline.json" 2>$null
    if ($LASTEXITCODE -ne 0) { $baseline = '{}' }
    Set-Content -Path (Join-Path $folder 'baseline.json') -Value $baseline

    $files = @(& git -C $Repository ls-tree -r --name-only $Base -- src | Where-Object { $_ -cmatch '^src/[^/]+/PublicAPI\.Shipped\.txt$' })
    foreach ($file in $files) {
        $project = ($file -split '/')[1]
        $lines = & git -C $Repository show "${Base}:$file"
        if ($LASTEXITCODE -ne 0) { throw "git show ${Base}:$file failed." }
        Set-Content -Path (Join-Path $folder 'shipped' "$project.txt") -Value $lines
    }

    $env:HERMES_RELEASE_GATE = $folder
    try {
        $output = dotnet run --project (Join-Path $Repository 'tests' 'Hermes.Tests' 'Hermes.Tests.csproj') --configuration Release -p:RestoreLockedMode=true -- --filter-method '*ReleaseGate_WritesTheCurrentSetAndWhatTheChangeFromTheBaselineNeeds' --progress off 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        Remove-Item Env:HERMES_RELEASE_GATE
    }

    $Needs = Join-Path $folder 'needs.json'
    if ($code -ne 0 -or -not (Test-Path $Needs)) {
        $output | Write-Host
        throw "The current set could not be read and compared (exit code $code)."
    }
}

$answer = Get-Content -Raw -Path $Needs | ConvertFrom-Json
$level = [string]$answer.needs
foreach ($line in @($answer.lines)) { Write-Host $line }
Write-Host "Since $previous the change needs a $level release."

if ($tagged.Count -gt 0) {
    $version = [version]$tagged[0].Substring(1)
    $before = [version]$previous.Substring(1)
    $enough = switch ($level) {
        'major' { $version.Major -gt $before.Major }
        'minor' { $version.Major -gt $before.Major -or ($version.Major -eq $before.Major -and $version.Minor -gt $before.Minor) }
        default { $version -gt $before }
    }
    if (-not $enough) {
        throw "$($tagged[0]) is below the $level release the change since $previous needs."
    }

    Write-Host "$($tagged[0]) is the $level release the change needs, or above it."
    return
}

if ($level -eq 'major') {
    $messages = (& git -C $Repository log --format='%B%x00' "$previous..$commit") -join "`n"
    if ($LASTEXITCODE -ne 0) { throw "git log $previous..$commit failed." }
    $marked = @($messages -split "`0" | Where-Object {
            $lines = @($_.Trim() -split "`n")
            $lines[0] -cmatch '^[a-z]+(\([a-z]+\))?!: ' -or @($lines | Where-Object { $_ -cmatch '^BREAKING CHANGE: ' }).Count -gt 0
        })
    if ($marked.Count -eq 0) {
        throw "The change needs a major release, and no commit since $previous carries ! or BREAKING CHANGE:."
    }

    Write-Host "A commit since $previous marks the break."
}