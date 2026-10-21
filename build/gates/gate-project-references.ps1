#Requires -Version 7.0
<#
.SYNOPSIS
    Checks the projects' layout, references and grants (07 MSG-ENG-001, 04 MSG-LIB-001).
.DESCRIPTION
    Runs the repository's tests of the layout, the project references and the grants of
    InternalsVisibleTo, which read every project of the repository.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

& (Join-Path $PSScriptRoot 'run-repository-tests.ps1') -Classes 'Hermes.Repository.Tests.RepositoryLayoutTests', 'Hermes.Repository.Tests.ProjectReferenceTests'
