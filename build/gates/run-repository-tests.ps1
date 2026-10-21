#Requires -Version 7.0
<#
.SYNOPSIS
    Builds tests/Hermes.Repository.Tests and runs the given classes of it, for the gates those tests are.
.PARAMETER Classes
    The full names of the test classes to run.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]] $Classes
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$project = Join-Path $root 'tests' 'Hermes.Repository.Tests' 'Hermes.Repository.Tests.csproj'

dotnet build $project --configuration Release -p:RestoreLockedMode=true -nodeReuse:false
if ($LASTEXITCODE -ne 0) {
    throw "The build of the repository's tests failed with exit code $LASTEXITCODE."
}

$filters = foreach ($class in $Classes) { '--filter-class'; $class }
dotnet run --project $project --configuration Release --no-build -- @filters
if ($LASTEXITCODE -ne 0) {
    throw "The repository's tests $($Classes -join ', ') failed with exit code $LASTEXITCODE."
}
