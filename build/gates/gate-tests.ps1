#Requires -Version 7.0
<#
.SYNOPSIS
    Runs the unit, integration and contract tests, each kind on its own by its Category trait (07 MSG-ENG-008).
.DESCRIPTION
    A run that finds no test of its kind passes: Microsoft.Testing.Platform's exit code 8,
    for no test run, is accepted.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path

# The integration tests run the scanner this step leaves under tmp/tools/.
& (Join-Path $PSScriptRoot 'fetch-secret-scanner.ps1') | Out-Null

dotnet build (Join-Path $root 'Hermes.sln') --configuration Release -p:RestoreLockedMode=true -nodeReuse:false
if ($LASTEXITCODE -ne 0) {
    throw "The build failed with exit code $LASTEXITCODE."
}

$projects = Get-ChildItem -Path (Join-Path $root 'tests') -Filter '*.csproj' -Recurse -File |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Sort-Object FullName
$failures = [System.Collections.Generic.List[string]]::new()

foreach ($kind in 'unit', 'integration', 'contract') {
    foreach ($project in $projects) {
        Write-Host "== $kind tests of $($project.BaseName)"
        dotnet run --project $project.FullName --configuration Release --no-build -- --filter-trait "Category=$kind" --ignore-exit-code 8
        if ($LASTEXITCODE -ne 0) {
            $failures.Add("$kind tests of $($project.BaseName) (exit code $LASTEXITCODE)")
        }
    }
}

if ($failures.Count -gt 0) {
    throw "Tests failed: $($failures -join '; ')."
}
