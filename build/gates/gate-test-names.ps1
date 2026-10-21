#Requires -Version 7.0
<#
.SYNOPSIS
    Checks that each criterion of build/gates/built-items.txt has a test named after it (07 MSG-ENG-008).
.DESCRIPTION
    Each line of the file names one criterion, as "MSG-SEND-006 AC2". A criterion has its test
    where a test project declares a method named ITEM_ACn_Outcome, "MSG_SEND_006_AC2_..." for
    that line.
.PARAMETER Repository
    The repository to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$list = Join-Path $Repository 'build' 'gates' 'built-items.txt'
if (-not (Test-Path $list)) {
    throw 'build/gates/built-items.txt is missing.'
}

$sources = @(
    foreach ($folder in (Join-Path $Repository 'tests'), (Join-Path $Repository 'samples')) {
        if (Test-Path $folder) {
            Get-ChildItem -Path $folder -Recurse -File -Filter '*.cs' |
                Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' -and ($_.FullName -match '[\\/]tests[\\/]' -or $_.FullName -match '\.Tests[\\/]') }
        }
    }
)
$text = ($sources | ForEach-Object { Get-Content -Raw -Path $_.FullName }) -join "`n"

$failures = 0
$number = 0
foreach ($line in Get-Content -Path $list) {
    $number++
    $criterion = [regex]::Match($line, '^(?<item>MSG-[A-Z]+-\d{3}) (?<criterion>AC\d+)$')
    if (-not $criterion.Success) {
        Write-Host "built-items.txt line ${number}, ""$line"", does not name one criterion as ""MSG-SEND-006 AC2""."
        $failures++
        continue
    }

    $prefix = $criterion.Groups['item'].Value.Replace('-', '_') + '_' + $criterion.Groups['criterion'].Value + '_'
    $pattern = '\b(void|Task|ValueTask)\s+' + [regex]::Escape($prefix) + '[A-Za-z0-9]+\s*\('
    if ($text -cnotmatch $pattern) {
        Write-Host "$line has no test named $prefix<Outcome>."
        $failures++
    }
}

if ($failures -gt 0) {
    throw "$failures criterion line(s) of built-items.txt have no test named after them."
}

Write-Host "Each of the $number criteria of built-items.txt has its test."
