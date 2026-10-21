#Requires -Version 7.0
<#
.SYNOPSIS
    Scans every commit of every ref for secrets with gitleaks (10 MSG-THR-004).
.PARAMETER Repository
    The repository to scan; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scanner = & (Join-Path $PSScriptRoot 'fetch-secret-scanner.ps1')
$configuration = Join-Path $PSScriptRoot 'gitleaks.toml'

& $scanner git $Repository --config $configuration --log-opts=--all --redact --no-banner --exit-code 1
if ($LASTEXITCODE -ne 0) {
    throw "The secret scan failed with exit code $LASTEXITCODE."
}
