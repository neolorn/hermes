#Requires -Version 7.0
<#
.SYNOPSIS
    Fails a change whose release version is below what it needs (04 MSG-LIB-006, 07 MSG-ENG-012).
.DESCRIPTION
    Compares the current set (the public surface, the routes with their methods and statuses, the
    JSON bodies' members and types, the codes, the configuration keys, the spellings and the
    metric names and kinds, as the contract tests read them) with the baseline: the
    build/gates/release-baseline.json and PublicAPI.Shipped.txt files the tag of the newest final
    release before Head holds, on a pull request as on a release tag. A release candidate's tag,
    vX.Y.Z-rc.N, is never that release. Before the first final release no tag holds a baseline, so
    it is empty and nothing is a break.

    On a release tag, a release candidate's included, the tag's version X.Y.Z must be above the
    newest final release's by what the change needs: a removal or a change, a request member a
    caller must now send, or a member added to an interface a host implements without a default
    implementation needs a major release; an addition a minor one. Anywhere else (a pull request,
    a push), the version is a pre-release, and a change that needs a major release fails unless a
    commit since the newest final release carries ! or BREAKING CHANGE: (07 MSG-ENG-011).

    Where the change from Base changes build/gates/release-baseline.json, which only a release
    commit may (07 MSG-ENG-011), the baseline at Head must be the set read there, before the first
    final release as after it.
.PARAMETER Repository
    The repository to check; by default, this one.
.PARAMETER Base
    The commit after which the change is checked: the previous release tag, or a pull request's
    base.
.PARAMETER Head
    The commit checked.
.PARAMETER Needs
    A needs.json the comparison already wrote, read in place of building and comparing; for the
    gate's own tests.
.PARAMETER Current
    A current.json the read already wrote, read in place of building where the baseline at Head is
    compared with the set read there; for the gate's own tests.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [string] $Base = '',
    [string] $Head = 'HEAD',
    [string] $Needs = '',
    [string] $Current = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Repository = (Resolve-Path $Repository).Path
$commit = & git -C $Repository rev-parse --verify --quiet "$Head^{commit}"
if ($LASTEXITCODE -ne 0 -or -not $commit) { throw "-Head $Head names no commit." }

# A release tag at Head, a release candidate's included, is held to what its version X.Y.Z needs
# against the newest final release before it, which alone is the baseline (04 MSG-LIB-006).
$release = '^v(?<version>\d+\.\d+\.\d+)(-rc\.[1-9]\d*)?$'
$tagged = @(& git -C $Repository tag --points-at $commit | Where-Object { $_ -cmatch $release } | Sort-Object -Descending { [version]([regex]::Match($_, $release).Groups['version'].Value) })
$previous = [string](& (Join-Path $PSScriptRoot 'previous-release-tag.ps1') -Repository $Repository -Head $commit -Final)

$baselineFile = 'build/gates/release-baseline.json'
$baselineChanged = $false
if ($Base) {
    & git -C $Repository diff --quiet $Base $commit -- $baselineFile
    if ($LASTEXITCODE -eq 1) { $baselineChanged = $true }
    elseif ($LASTEXITCODE -ne 0) { throw "git diff $Base $commit failed." }
}

if (-not $previous -and -not $baselineChanged) {
    Write-Host 'No final release precedes Head, so the baseline is empty and nothing is a break.'
    return
}

if (($previous -and -not $Needs) -or ($baselineChanged -and -not $Current)) {
    if (-not $Base) { throw "A final release ($previous) precedes Head, so -Base must name the commit after which the change is checked." }

    $folder = Join-Path $Repository 'tmp' 'release-gate'
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
    New-Item -ItemType Directory -Force (Join-Path $folder 'shipped') | Out-Null

    # The baseline is what the newest final release's tag holds; before the first, it is empty.
    $baseline = '{}'
    $files = @()
    if ($previous) {
        $baseline = & git -C $Repository show "${previous}:$baselineFile" 2>$null
        if ($LASTEXITCODE -ne 0) { $baseline = '{}' }
        $files = @(& git -C $Repository ls-tree -r --name-only $previous -- src | Where-Object { $_ -cmatch '^src/[^/]+/PublicAPI\.Shipped\.txt$' })
    }
    Set-Content -Path (Join-Path $folder 'baseline.json') -Value $baseline

    foreach ($file in $files) {
        $project = ($file -split '/')[1]
        $lines = & git -C $Repository show "${previous}:$file"
        if ($LASTEXITCODE -ne 0) { throw "git show ${previous}:$file failed." }
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
    $Current = Join-Path $folder 'current.json'
    if ($code -ne 0 -or -not (Test-Path $Needs) -or -not (Test-Path $Current)) {
        $output | Write-Host
        throw "The current set could not be read and compared (exit code $code)."
    }
}

# The release commit writes the baseline as the set read at it, and only docs: commits follow it
# in its pull request, so a baseline the change rewrites is the set read at Head (04 MSG-LIB-006).
if ($baselineChanged) {
    $held = & git -C $Repository show "${commit}:$baselineFile" 2>$null
    if ($LASTEXITCODE -ne 0) { throw "Head holds no $baselineFile, which only a release commit changes." }

    $atHead = ($held -join "`n") | ConvertFrom-Json -AsHashtable
    $read = Get-Content -Raw -Path $Current | ConvertFrom-Json -AsHashtable
    $differences = [System.Collections.Generic.List[string]]::new()
    foreach ($section in @(@($atHead.Keys) + @($read.Keys) | Sort-Object -Unique)) {
        $heldLines = [System.Collections.Generic.HashSet[string]]::new([string[]]@($atHead[$section] | Where-Object { $null -ne $_ }), [StringComparer]::Ordinal)
        $readLines = [System.Collections.Generic.HashSet[string]]::new([string[]]@($read[$section] | Where-Object { $null -ne $_ }), [StringComparer]::Ordinal)
        foreach ($line in $readLines) { if (-not $heldLines.Contains($line)) { $differences.Add("${section}: read at Head, not in the baseline: $line") } }
        foreach ($line in $heldLines) { if (-not $readLines.Contains($line)) { $differences.Add("${section}: in the baseline, not read at Head: $line") } }
    }

    if ($differences.Count -gt 0) {
        foreach ($difference in $differences) { Write-Host $difference }
        throw "The baseline at Head is not the set the release gate reads there, as a release commit writes it."
    }

    Write-Host 'The baseline at Head is the set the release gate reads there.'
}

if (-not $previous) {
    Write-Host 'No final release precedes Head, so the baseline is empty and nothing is a break.'
    return
}

$answer = Get-Content -Raw -Path $Needs | ConvertFrom-Json
$level = [string]$answer.needs
foreach ($line in @($answer.lines)) { Write-Host $line }
Write-Host "Since $previous the change needs a $level release."

if ($tagged.Count -gt 0) {
    $version = [version]([regex]::Match($tagged[0], $release).Groups['version'].Value)
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