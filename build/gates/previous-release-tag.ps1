#Requires -Version 7.0
<#
.SYNOPSIS
    Writes the release tag before Head, from which the commit-message gate checks a release tag (07 MSG-ENG-011, MSG-ENG-012).
.DESCRIPTION
    A release tag is v<V>, V a final release <major>.<minor>.<patch> or a release candidate
    <major>.<minor>.<patch>-rc.<N>, N from 1; another tag is no release. Writes the highest
    release tag Head's parent reaches, in Semantic Versioning's precedence (a release candidate
    below its final release, rc.10 above rc.9), or nothing for the first release, whose gate
    checks the whole history.
.PARAMETER Repository
    The repository to read; by default, this one.
.PARAMETER Head
    The release tag's commit.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [Parameter(Mandatory)]
    [string] $Head
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$parent = & git -C $Repository rev-parse --verify --quiet "$Head^"
if ($LASTEXITCODE -ne 0) { return }

$tags = @(& git -C $Repository tag --list --merged $parent)
if ($LASTEXITCODE -ne 0) { throw "git tag --merged $parent failed." }

$releases = @(foreach ($tag in $tags) {
        $match = [regex]::Match($tag, '^v(?<version>\d+\.\d+\.\d+)(-rc\.(?<candidate>[1-9]\d*))?$')
        if ($match.Success) {
            $candidate = $match.Groups['candidate']
            [pscustomobject]@{
                Tag       = $tag
                Version   = [version]$match.Groups['version'].Value
                Final     = -not $candidate.Success
                Candidate = if ($candidate.Success) { [bigint]::Parse($candidate.Value, [cultureinfo]::InvariantCulture) } else { [bigint]::Zero }
            }
        }
    })

if ($releases.Count -gt 0) {
    Write-Output ($releases | Sort-Object -Descending -Property Version, Final, Candidate | Select-Object -First 1).Tag
}
