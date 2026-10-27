#Requires -Version 7.0
<#
.SYNOPSIS
    Builds the sample host from the packages packed from this commit, checks its formatting and runs its tests (07 MSG-ENG-012, MSG-ENG-001, MSG-ENG-002).
.DESCRIPTION
    Packs each package of src/ into artifacts/packages/, emptied first, the pack's build output
    under tmp/sample-host/ so the repository's own is left as it is, and reads the one version
    the packages were packed at. Restores samples/Samples.sln at that version into a packages
    folder of its own under tmp/sample-host/, so no package an earlier pack left at the same
    version in the machine's cache is taken, then checks its formatting with dotnet format
    --verify-no-changes, builds it with its analysers, every warning an error, and runs each of
    its test projects. The sample host's tests start PostgreSQL containers, so the gate needs
    Docker.
.PARAMETER Repository
    The repository whose sample host to check; by default, this one.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..')
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Repository = (Resolve-Path $Repository).Path
$solution = Join-Path $Repository 'samples' 'Samples.sln'
if (-not (Test-Path $solution)) {
    throw 'samples/Samples.sln is missing.'
}

$projects = @(
    if (Test-Path (Join-Path $Repository 'src')) {
        Get-ChildItem -Path (Join-Path $Repository 'src') -Filter '*.csproj' -Recurse -File |
            Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
            Sort-Object FullName
    }
)
if ($projects.Count -eq 0) {
    throw 'src/ holds no package to pack.'
}

& docker version --format '{{.Server.Version}}' | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is not available, so the sample host''s tests cannot start their databases.'
}

$work = Join-Path $Repository 'tmp' 'sample-host'
$packed = Join-Path $Repository 'artifacts' 'packages'
foreach ($folder in $work, $packed) {
    if (Test-Path $folder) { Remove-Item -Recurse -Force $folder }
}
New-Item -ItemType Directory -Force $packed | Out-Null

foreach ($project in $projects) {
    dotnet pack $project.FullName --configuration Release --artifacts-path (Join-Path $work 'build') --output $packed -p:RestoreLockedMode=true -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw "The pack of $($project.BaseName) failed with exit code $LASTEXITCODE."
    }
}

# Each package is <Id>.<version>.nupkg, its Id its project's name.
$versions = @(@(
    foreach ($project in $projects) {
        $package = @(Get-ChildItem -Path $packed -Filter "$($project.BaseName).*.nupkg" | Where-Object { $_.Name -match "^$([regex]::Escape($project.BaseName))\.\d" })
        if ($package.Count -ne 1) {
            throw "The pack left $($package.Count) package(s) of $($project.BaseName), not one."
        }
        $package[0].Name.Substring($project.BaseName.Length + 1, $package[0].Name.Length - $project.BaseName.Length - 1 - '.nupkg'.Length)
    }
) | Sort-Object -Unique)
if ($versions.Count -ne 1) {
    throw "The packages were packed at more than one version: $($versions -join ', ')."
}
Write-Host "The packages were packed at $($versions[0])."

# MSBuild reads both as properties, and NuGet the second as its packages folder, in every command below.
$env:HermesPackageVersion = $versions[0]
$env:NUGET_PACKAGES = Join-Path $work 'packages'
try {
    dotnet restore $solution
    if ($LASTEXITCODE -ne 0) {
        throw "The sample host's restore failed with exit code $LASTEXITCODE."
    }

    dotnet format $solution --verify-no-changes --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet format found changes to make in the sample host (exit code $LASTEXITCODE)."
    }

    dotnet build $solution --configuration Release --no-restore -nodeReuse:false
    if ($LASTEXITCODE -ne 0) {
        throw "The sample host's build failed with exit code $LASTEXITCODE."
    }

    $tests = @(Get-ChildItem -Path (Join-Path $Repository 'samples') -Filter '*.Tests.csproj' -Recurse -File | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } | Sort-Object FullName)
    if ($tests.Count -eq 0) {
        throw 'samples/ holds no test project, so the sample host''s tests would not run.'
    }

    foreach ($test in $tests) {
        dotnet run --project $test.FullName --configuration Release --no-build
        if ($LASTEXITCODE -ne 0) {
            throw "The tests of $($test.BaseName) failed with exit code $LASTEXITCODE."
        }
    }
}
finally {
    Remove-Item Env:HermesPackageVersion, Env:NUGET_PACKAGES
}

Write-Host 'The sample host built from the packages packed from this commit, and its tests passed.'