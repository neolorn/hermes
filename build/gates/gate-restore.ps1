#Requires -Version 7.0
<#
.SYNOPSIS
    Restores every project in locked mode, with NuGet's audit of every package (07 MSG-ENG-002, MSG-ENG-010).
.DESCRIPTION
    A restore that would change a lockfile fails, and so does a package, transitive ones
    included, with a published advisory, since the audit's warnings are errors.
.PARAMETER Repository
    The repository whose Hermes.sln to restore; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$solution = Join-Path $Repository 'Hermes.sln'
if (-not (Test-Path $solution)) {
    throw 'Hermes.sln is missing.'
}

dotnet restore $solution --locked-mode --force
if ($LASTEXITCODE -ne 0) {
    throw "The locked restore or its audit failed with exit code $LASTEXITCODE."
}
