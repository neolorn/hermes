#Requires -Version 7.0
<#
.SYNOPSIS
    Fetches gitleaks 8.30.1 into tmp/tools/, checked against its published SHA-256.
.DESCRIPTION
    The step the secret-scanning gate and the test gate both run first (07 MSG-ENG-001).
    Writes the scanner's path to the output.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$version = '8.30.1'

# The digests published in gitleaks_8.30.1_checksums.txt of the release.
$digests = @{
    'darwin_arm64'  = 'b40ab0ae55c505963e365f271a8d3846efbc170aa17f2607f13df610a9aeb6a5'
    'darwin_x64'    = 'dfe101a4db2255fc85120ac7f3d25e4342c3c20cf749f2c20a18081af1952709'
    'linux_arm64'   = 'e4a487ee7ccd7d3a7f7ec08657610aa3606637dab924210b3aee62570fb4b080'
    'linux_x64'     = '551f6fc83ea457d62a0d98237cbad105af8d557003051f41f3e7ca7b3f2470eb'
    'windows_arm64' = 'b95f5e4f5c425cedca7ee203d9afd29597e692c4924a12ed42f970537c72cc0f'
    'windows_x64'   = 'd29144deff3a68aa93ced33dddf84b7fdc26070add4aa0f4513094c8332afc4e'
}

$architecture = switch ([System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture) {
    'X64' { 'x64' }
    'Arm64' { 'arm64' }
    default { throw "No gitleaks $version build is pinned for architecture $_." }
}
$system = if ($IsWindows) { 'windows' } elseif ($IsMacOS) { 'darwin' } elseif ($IsLinux) { 'linux' } else { throw 'Unknown operating system.' }
$platform = "${system}_$architecture"
$extension = if ($IsWindows) { 'zip' } else { 'tar.gz' }
$archiveName = "gitleaks_${version}_$platform.$extension"

$root = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
$tools = Join-Path $root 'tmp' 'tools' "gitleaks-$version"
$archive = Join-Path $tools $archiveName
$scanner = Join-Path $tools ($IsWindows ? 'gitleaks.exe' : 'gitleaks')

New-Item -ItemType Directory -Force -Path $tools | Out-Null

if (-not (Test-Path $archive)) {
    $address = "https://github.com/gitleaks/gitleaks/releases/download/v$version/$archiveName"
    Invoke-WebRequest -Uri $address -OutFile $archive
}

$actual = (Get-FileHash -Algorithm SHA256 -Path $archive).Hash.ToLowerInvariant()
if ($actual -ne $digests[$platform]) {
    Remove-Item -Force $archive
    if (Test-Path $scanner) { Remove-Item -Force $scanner }
    throw "$archiveName does not match its published SHA-256."
}

if (-not (Test-Path $scanner)) {
    if ($IsWindows) {
        Expand-Archive -Path $archive -DestinationPath $tools -Force
    }
    else {
        tar -xzf $archive -C $tools gitleaks
        if ($LASTEXITCODE -ne 0) { throw "Extracting $archiveName failed." }
    }
}

Write-Output $scanner
