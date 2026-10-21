#Requires -Version 7.0
<#
.SYNOPSIS
    Checks each commit's message and changelog line (07 MSG-ENG-011).
.DESCRIPTION
    Every commit after Base up to Head, save merge commits, has a Conventional Commits header
    of an allowed type and scope, a lower-case description with no full stop and a first line
    of at most 72 characters; a body, if any, of dash fragments of at most 72 characters; and
    a footer, if any, of "Implements:" and "BREAKING CHANGE:" lines alone. A commit of type
    feat, fix or perf, or marked as breaking, adds its line under the changelog's Unreleased
    section, under "Added" while no release is tagged.
.PARAMETER Repository
    The repository to check; by default, this one.
.PARAMETER Base
    The commit after which to check; none checks the whole history up to Head.
.PARAMETER Head
    The last commit to check.
#>
[CmdletBinding()]
param(
    [string] $Repository = (Join-Path $PSScriptRoot '..' '..'),
    [string] $Base = '',
    [string] $Head = 'HEAD'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$types = 'feat|fix|refactor|perf|test|docs|build|ci|chore'
$scopes = 'core|channels|engine|janus|testing|cli|sample'
$headerPattern = "^(?<type>$types)(\((?<scope>$scopes)\))?(?<breaking>!)?: (?<description>\S(.*\S)?)$"
$footerPattern = '^(Implements: MSG-[A-Z]+-\d{3}(, MSG-[A-Z]+-\d{3})*|BREAKING CHANGE: \S.*)$'
$sectionTypes = 'Added|Changed|Deprecated|Removed|Fixed|Security'

function Invoke-Git {
    $output = & git -C $Repository @args
    if ($LASTEXITCODE -ne 0) { throw "git $args failed." }
    $output
}

function Get-MessageProblems([string] $message) {
    $lines = @($message -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') { $lines = @($lines | Select-Object -SkipLast 1) }

    $header = $lines[0]
    $match = [regex]::Match($header, $headerPattern)
    if (-not $match.Success) {
        'the first line is not "type(scope): description" of an allowed type and scope'
        return
    }

    $description = $match.Groups['description'].Value
    if ($header.Length -gt 72) { 'the first line is longer than 72 characters' }
    if ($description -cmatch '\p{Lu}') { 'the description is not lower case' }
    if ($description.EndsWith('.')) { 'the description ends with a full stop' }

    $breaking = $match.Groups['breaking'].Success
    if ($lines.Count -gt 1) {
        if ($lines[1] -ne '') { 'the first line is not followed by a blank line' }

        $paragraphs = [System.Collections.Generic.List[string[]]]::new()
        $current = [System.Collections.Generic.List[string]]::new()
        foreach ($line in ($lines | Select-Object -Skip 2)) {
            if ($line -eq '') {
                if ($current.Count -gt 0) { $paragraphs.Add($current.ToArray()); $current.Clear() }
            }
            else { $current.Add($line) }
        }
        if ($current.Count -gt 0) { $paragraphs.Add($current.ToArray()) }

        if ($paragraphs.Count -gt 0) {
            $last = $paragraphs[$paragraphs.Count - 1]
            $isFooter = @($last | Where-Object { $_ -cnotmatch $footerPattern }).Count -eq 0
            if ($isFooter) {
                if (@($last | Where-Object { $_ -clike 'BREAKING CHANGE:*' }).Count -gt 0) { $breaking = $true }
                $paragraphs.RemoveAt($paragraphs.Count - 1)
            }

            foreach ($paragraph in $paragraphs) {
                foreach ($line in $paragraph) {
                    if ($line -notmatch '^- \S') { "the body line ""$line"" is not a dash fragment, and the footer holds only Implements: and BREAKING CHANGE: lines" }
                    elseif ($line.Length -gt 72) { "the body line ""$line"" is longer than 72 characters" }
                }
            }
        }
    }

    if ($match.Groups['type'].Value -in 'feat', 'fix', 'perf' -or $breaking) {
        [pscustomobject]@{ ChangelogRequired = $true }
    }
}

function Test-ChangelogLine([string] $commit) {
    $diff = @(& git -C $Repository show --format= --unified=0 --no-color $commit -- CHANGELOG.md)
    if ($LASTEXITCODE -ne 0) { throw "git show $commit failed." }

    $added = [System.Collections.Generic.List[int]]::new()
    $number = 0
    foreach ($line in $diff) {
        $hunk = [regex]::Match($line, '^@@ -\d+(,\d+)? \+(?<start>\d+)(,\d+)? @@')
        if ($hunk.Success) { $number = [int]$hunk.Groups['start'].Value; continue }
        if ($line.StartsWith('+++') -or $line.StartsWith('---')) { continue }
        if ($line.StartsWith('+')) {
            if ($line -match '^\+- \S') { $added.Add($number) }
            $number++
        }
        elseif (-not $line.StartsWith('-') -and $number -gt 0) { $number++ }
    }
    if ($added.Count -eq 0) { return $false }

    $changelog = @(& git -C $Repository show "${commit}:CHANGELOG.md")
    if ($LASTEXITCODE -ne 0) { return $false }
    $released = @(& git -C $Repository tag --list 'v*' --merged $commit).Count -gt 0

    foreach ($lineNumber in $added) {
        $section = $null
        $subsection = $null
        for ($index = 0; $index -lt $lineNumber - 1; $index++) {
            $text = $changelog[$index]
            if ($text -match '^## ') { $section = $text; $subsection = $null }
            elseif ($text -match "^### (?<name>$sectionTypes)$") { $subsection = $Matches['name'] }
        }
        $underUnreleased = $section -eq '## [Unreleased]'
        $underSection = $null -ne $subsection -and ($released -or $subsection -eq 'Added')
        if ($underUnreleased -and $underSection) { return $true }
    }
    return $false
}

$range = if ($Base) { "$Base..$Head" } else { $Head }
$commits = @(Invoke-Git rev-list --no-merges --reverse $range)
$failures = 0

foreach ($commit in $commits) {
    $message = (Invoke-Git log -1 --format=%B $commit) -join "`n"
    $results = @(Get-MessageProblems $message)
    $problems = @($results | Where-Object { $_ -is [string] })
    if (@($results | Where-Object { $_ -isnot [string] }).Count -gt 0 -and -not (Test-ChangelogLine $commit)) {
        $problems += 'it changes behaviour and adds no line under the changelog''s Unreleased section'
    }

    $subject = ($message -split "`n")[0]
    foreach ($problem in $problems) {
        Write-Host "$($commit.Substring(0, 12)) ""$subject"": $problem."
        $failures++
    }
}

if ($failures -gt 0) {
    throw "$failures problem(s) in the commit messages of $range."
}

Write-Host "$($commits.Count) commit(s) of $range follow the commit-message and changelog rule."
