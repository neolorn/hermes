#Requires -Version 7.0
<#
.SYNOPSIS
    Runs every gate script present under build/gates/ and reports each one's result (07 MSG-ENG-012).
.DESCRIPTION
    The pipeline's gates job runs this script, and so does the implementer, on the commit a
    phase report names. Each gate runs in its own PowerShell process; every gate runs even
    when one before it fails, and the script fails when any of them did.
.PARAMETER Base
    The commit after which the commit-message gate checks; none checks the whole history.
.PARAMETER Head
    The last commit the commit-message gate checks.
#>
[CmdletBinding()]
param(
    [string] $Base = '',
    [string] $Head = 'HEAD'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$results = [System.Collections.Generic.List[pscustomobject]]::new()

foreach ($gate in Get-ChildItem -Path $PSScriptRoot -Filter 'gate-*.ps1' -File | Sort-Object Name) {
    $arguments = @()
    if ($gate.Name -eq 'gate-commit-messages.ps1') {
        $arguments = @('-Base', $Base, '-Head', $Head)
    }

    Write-Host "::group::$($gate.BaseName)"
    pwsh -NoProfile -NonInteractive -File $gate.FullName @arguments
    $code = $LASTEXITCODE
    Write-Host '::endgroup::'

    $result = if ($code -eq 0) { 'passed' } else { "failed (exit code $code)" }
    $results.Add([pscustomobject]@{ Gate = $gate.BaseName; Result = $result })
}

$results | Format-Table -AutoSize | Out-String | Write-Host

if (@($results | Where-Object { $_.Result -ne 'passed' }).Count -gt 0) {
    throw 'One or more gates failed.'
}
