#Requires -Version 7.0
<#
.SYNOPSIS
    Builds Hermes.sln and runs the given classes of tests/Hermes.Repository.Tests, for the gates those tests are.
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

# Every project is built, since a test reads the built assembly of each (07 MSG-ENG-001).
dotnet build (Join-Path $root 'Hermes.sln') --configuration Release -p:RestoreLockedMode=true -nodeReuse:false
if ($LASTEXITCODE -ne 0) {
    throw "The build of Hermes.sln failed with exit code $LASTEXITCODE."
}

$filters = foreach ($class in $Classes) { '--filter-class'; $class }
dotnet run --project $project --configuration Release --no-build -- @filters
if ($LASTEXITCODE -ne 0) {
    throw "The repository's tests $($Classes -join ', ') failed with exit code $LASTEXITCODE."
}
