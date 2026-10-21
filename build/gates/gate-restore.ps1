#Requires -Version 7.0
<#
.SYNOPSIS
    Restores every project in locked mode, with NuGet's audit of every package (07 MSG-ENG-002, MSG-ENG-010).
.DESCRIPTION
    A restore that would change a lockfile fails, and so does a package, transitive ones
    included, with a published advisory, since the audit's warnings are errors.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path

dotnet restore (Join-Path $root 'Hermes.sln') --locked-mode --force
if ($LASTEXITCODE -ne 0) {
    throw "The locked restore or its audit failed with exit code $LASTEXITCODE."
}
