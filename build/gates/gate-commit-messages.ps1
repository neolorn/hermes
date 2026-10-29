#Requires -Version 7.0
<#
.SYNOPSIS
    Checks each commit's message, changelog line and released files (07 MSG-ENG-011).
.DESCRIPTION
    Every commit after Base up to Head, save the merge commits the platform writes (committed
    by GitHub, noreply@github.com), has a Conventional Commits header of an allowed type and
    scope, a description that begins with a lower-case letter and has no full stop, and a first
    line of at most 72 characters; a body, if any, of dash fragments of at most 72 characters;
    and a footer, if any, of "Implements:" and "BREAKING CHANGE:" lines alone. A commit of type
    feat, fix or perf, or marked as breaking, adds its line under the changelog's Unreleased
    section, under "Added" while no release is tagged. No commit but a release commit changes or
    deletes an existing PublicAPI.Shipped.txt or build/gates/release-baseline.json; adding one,
    or moving one unchanged, is no change, and a PublicAPI.Shipped.txt holding no entry may be
    deleted in the commit that deletes the project file beside it.

    A release commit, "chore: release X.Y.Z", changes no file but each PublicAPI.Shipped.txt, to
    its parent's entries and those of the PublicAPI.Unshipped.txt beside it, less each one marked
    *REMOVED*, under #nullable enable in ordinal order; each PublicAPI.Unshipped.txt, to
    #nullable enable alone; the release baseline, whose content the release gate checks;
    CHANGELOG.md, whose Unreleased entries move under "## [X.Y.Z] - YYYY-MM-DD", the commit's
    date in UTC, below an empty "## [Unreleased]"; and RELEASE-NOTES.md, which it holds. In its
    pull request, up to the platform's merge of it, only docs: commits that change nothing
    outside docs/ follow it.
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
$scopes = 'core|channels|engine|testing|cli|sample'
$headerPattern = "^(?<type>$types)(\((?<scope>$scopes)\))?(?<breaking>!)?: (?<description>\S(.*\S)?)$"
$footerPattern = '^(Implements: MSG-[A-Z]+-\d{3}(, MSG-[A-Z]+-\d{3})*|BREAKING CHANGE: \S.*)$'
$sectionTypes = 'Added|Changed|Deprecated|Removed|Fixed|Security'
$releaseTagPattern = '^v\d+\.\d+\.\d+$'
$releasePattern = '^chore: release (?<version>\d+\.\d+\.\d+)$'
$platformCommitter = 'noreply@github.com'
$emptyTree = '4b825dc642cb6eb9a060e54bf8d69288fbee4904'

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

    # A name the description mentions keeps its own spelling: "add the SMTP provider".
    $description = $match.Groups['description'].Value
    if ($header.Length -gt 72) { 'the first line is longer than 72 characters' }
    if ($description -cnotmatch '^\p{Ll}') { 'the description does not begin with a lower-case letter' }
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
    $released = @(Invoke-Git tag --list --merged $commit | Where-Object { $_ -cmatch $releaseTagPattern }).Count -gt 0

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

function Test-ReleasedFile([string] $path) {
    $path -ceq 'build/gates/release-baseline.json' -or ($path -split '/')[-1] -ceq 'PublicAPI.Shipped.txt'
}

# Whether $path is a PublicAPI.Shipped.txt that holds no entry in $parent (nothing but
# #nullable enable and blank lines) and whose folder's project file $deleted holds (M-021).
function Test-EntryFreeShippedFileGoingWithItsProject([string] $parent, [string] $path, [string[]] $deleted) {
    if (($path -split '/')[-1] -cne 'PublicAPI.Shipped.txt') { return $false }

    $folder = $path.Substring(0, [Math]::Max(0, $path.LastIndexOf('/')))
    $projects = @($deleted | Where-Object { $_.EndsWith('.csproj', [StringComparison]::Ordinal) -and $_.Substring(0, [Math]::Max(0, $_.LastIndexOf('/'))) -ceq $folder })
    if ($projects.Count -eq 0) { return $false }

    $lines = @(Invoke-Git show "${parent}:$path")
    return @($lines | Where-Object { ([string]$_).Trim() -cnotin '', '#nullable enable' }).Count -eq 0
}

# PublicAPI.Shipped.txt and the release baseline change only in a release commit, which
# Get-ReleaseCommitProblems checks in their place (M-023); a PublicAPI.Shipped.txt holding no
# entry may go in the commit that deletes the project file beside it (M-021).
function Get-ReleasedFileProblems([string] $commit, [string] $parent) {
    $changes = @(Invoke-Git diff --name-status --find-renames=100% $parent $commit)
    $deleted = @($changes | ForEach-Object { $fields = $_ -split "`t"; if ($fields[0] -eq 'D') { $fields[1] } })
    foreach ($change in $changes) {
        $fields = $change -split "`t"
        $status = $fields[0]
        $path = $fields[1]
        if ($status -like 'R*') {
            $moved = $fields[2]
            $kept = $status -eq 'R100' -and ($moved -split '/')[-1] -ceq ($path -split '/')[-1] -and $path -cne 'build/gates/release-baseline.json'
            if ((Test-ReleasedFile $path) -and -not $kept) { "it deletes $path, which changes only in a release commit" }
        }
        elseif ($status -eq 'D' -and (Test-ReleasedFile $path) -and -not (Test-EntryFreeShippedFileGoingWithItsProject $parent $path $deleted)) { "it deletes $path, which changes only in a release commit" }
        elseif ($status -in 'M', 'T' -and (Test-ReleasedFile $path)) { "it changes $path, which changes only in a release commit" }
    }
}

# The files a release commit changes (M-023).
function Test-ReleaseCommitFile([string] $path) {
    $path -cin 'build/gates/release-baseline.json', 'CHANGELOG.md', 'RELEASE-NOTES.md' -or ($path -split '/')[-1] -cin 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt'
}

function Test-Present([string] $commit, [string] $path) {
    & git -C $Repository cat-file -e "${commit}:$path" 2>$null
    $LASTEXITCODE -eq 0
}

# The lines of $path at $commit, carriage returns and the blank lines at its end aside; none
# where $commit holds no such file.
function Get-Lines([string] $commit, [string] $path) {
    if (-not (Test-Present $commit $path)) { return }
    $lines = @(Invoke-Git show "${commit}:$path" | ForEach-Object { ([string]$_).TrimEnd("`r") })
    while ($lines.Count -gt 0 -and $lines[-1] -eq '') { $lines = @($lines | Select-Object -SkipLast 1) }
    $lines
}

# The PublicAPI.Shipped.txt the release commit writes in $folder: its parent's entries and those
# of the PublicAPI.Unshipped.txt beside it, less each one marked *REMOVED*, under
# #nullable enable in ordinal order.
function Get-ReleasedShipped([string] $parent, [string] $prefix) {
    $lines = @(Get-Lines $parent "${prefix}PublicAPI.Shipped.txt") + @(Get-Lines $parent "${prefix}PublicAPI.Unshipped.txt")
    $removed = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $lines) { if ($line.StartsWith('*REMOVED*', [StringComparison]::Ordinal)) { [void]$removed.Add($line.Substring(9)) } }
    $entries = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in $lines) {
        if ($line.Trim() -cin '', '#nullable enable' -or $line.StartsWith('*REMOVED*', [StringComparison]::Ordinal) -or $removed.Contains($line)) { continue }
        [void]$entries.Add($line)
    }
    [string[]] $sorted = @($entries)
    [Array]::Sort($sorted, [StringComparer]::Ordinal)
    @('#nullable enable') + $sorted
}

# A release commit changes no file but its own, each as the release commit's value gives it
# (07 MSG-ENG-011, M-023); the baseline's content is the release gate's to check, since only
# a build reads the set it holds (04 MSG-LIB-006).
function Get-ReleaseCommitProblems([string] $commit, [string] $parent, [string] $version) {
    foreach ($change in @(Invoke-Git diff --name-status --no-renames $parent $commit)) {
        $fields = $change -split "`t"
        if ($fields[0] -eq 'D') { "it deletes $($fields[1]), which a release commit does not delete" }
        elseif (-not (Test-ReleaseCommitFile $fields[1])) { "it changes $($fields[1]), which a release commit does not change" }
    }

    $lists = @(@(Invoke-Git ls-tree -r --name-only $parent) + @(Invoke-Git ls-tree -r --name-only $commit) | Where-Object { ($_ -split '/')[-1] -cin 'PublicAPI.Shipped.txt', 'PublicAPI.Unshipped.txt' })
    $prefixes = @($lists | ForEach-Object { $_.Substring(0, $_.LastIndexOf('/') + 1) } | Sort-Object -Unique -CaseSensitive)
    foreach ($prefix in $prefixes) {
        $shipped = "${prefix}PublicAPI.Shipped.txt"
        if ((Test-Present $commit $shipped) -and ((Get-Lines $commit $shipped) -join "`n") -cne ((Get-ReleasedShipped $parent $prefix) -join "`n")) {
            "$shipped is not its parent's entries and those of the PublicAPI.Unshipped.txt beside it, less each one marked *REMOVED*, under #nullable enable in ordinal order"
        }

        $unshipped = "${prefix}PublicAPI.Unshipped.txt"
        if ((Test-Present $commit $unshipped) -and ((Get-Lines $commit $unshipped) -join "`n") -cne '#nullable enable') {
            "$unshipped holds more than #nullable enable"
        }
    }

    # The commit's date in UTC: the author date, which a merge of its pull request keeps.
    $seconds = [long](Invoke-Git log -1 --format=%at $commit)
    $date = [DateTimeOffset]::FromUnixTimeSeconds($seconds).UtcDateTime.ToString('yyyy-MM-dd', [cultureinfo]::InvariantCulture)
    $heading = "## [$version] - $date"
    $expected = [System.Collections.Generic.List[string]]::new()
    $moved = $false
    foreach ($line in @(Get-Lines $parent 'CHANGELOG.md')) {
        $expected.Add($line)
        if (-not $moved -and $line -ceq '## [Unreleased]') { $expected.Add(''); $expected.Add($heading); $moved = $true }
    }
    if (-not $moved -or ((Get-Lines $commit 'CHANGELOG.md') -join "`n") -cne ($expected -join "`n")) {
        "CHANGELOG.md does not move the Unreleased entries under $heading below an empty ## [Unreleased]"
    }

    if (-not (Test-Present $commit 'RELEASE-NOTES.md')) { 'it holds no RELEASE-NOTES.md' }
}

function Test-PlatformMerge([string] $commit) {
    $fields = (Invoke-Git log -1 --format='%P%x09%ce' $commit) -split "`t"
    @($fields[0] -split ' ' | Where-Object { $_ }).Count -gt 1 -and $fields[1] -ceq $platformCommitter
}

# In its pull request only docs: commits that change nothing outside docs/ follow a release
# commit, so the tag on it carries everything the pull request built: its followers are the
# commits after it up to Head, save the platform's merge of its pull request and what follows that.
function Get-FollowerProblems([string] $release, [hashtable] $problems) {
    $descendants = @(Invoke-Git rev-list --ancestry-path "$release..$Head")
    $merges = @($descendants | Where-Object { Test-PlatformMerge $_ })
    $later = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($merge in $merges) {
        [void]$later.Add($merge)
        foreach ($commit in @(Invoke-Git rev-list --ancestry-path "$merge..$Head")) { [void]$later.Add($commit) }
    }

    foreach ($commit in $descendants) {
        if ($later.Contains($commit)) { continue }
        $subject = [string](Invoke-Git log -1 --format=%s $commit)
        $outside = @(Invoke-Git diff --name-only --no-renames "$commit^" $commit | Where-Object { -not $_.StartsWith('docs/', [StringComparison]::Ordinal) })
        if ($subject -cnotmatch '^docs(\([a-z]+\))?: ' -or $outside.Count -gt 0) {
            if (-not $problems.ContainsKey($commit)) { $problems[$commit] = [System.Collections.Generic.List[string]]::new() }
            $problems[$commit].Add("it follows the release commit $($release.Substring(0, 12)) in its pull request and is not a docs: commit changing only docs/")
        }
    }
}

$range = if ($Base) { "$Base..$Head" } else { $Head }
$commits = @(Invoke-Git rev-list --reverse $range)
$failures = 0
$checked = 0

$followers = @{}
foreach ($commit in $commits) {
    if ([string](Invoke-Git log -1 --format=%s $commit) -cmatch $releasePattern) { Get-FollowerProblems $commit $followers }
}

foreach ($commit in $commits) {
    $fields = (Invoke-Git log -1 --format='%P%x09%ce' $commit) -split "`t"
    $parents = @($fields[0] -split ' ' | Where-Object { $_ })
    if ($parents.Count -gt 1 -and $fields[1] -ceq $platformCommitter) { continue }
    $checked++

    $message = (Invoke-Git log -1 --format=%B $commit) -join "`n"
    $results = @(Get-MessageProblems $message)
    $problems = @($results | Where-Object { $_ -is [string] })
    if (@($results | Where-Object { $_ -isnot [string] }).Count -gt 0 -and -not (Test-ChangelogLine $commit)) {
        $problems += 'it changes behaviour and adds no line under the changelog''s Unreleased section'
    }

    $parent = if ($parents.Count -gt 0) { $parents[0] } else { $emptyTree }
    $subject = ($message -split "`n")[0].TrimEnd("`r")
    $release = [regex]::Match($subject, $releasePattern)
    if ($release.Success) { $problems += @(Get-ReleaseCommitProblems $commit $parent $release.Groups['version'].Value) }
    else { $problems += @(Get-ReleasedFileProblems $commit $parent) }
    if ($followers.ContainsKey($commit)) { $problems += @($followers[$commit]) }

    foreach ($problem in $problems) {
        Write-Host "$($commit.Substring(0, 12)) ""$subject"": $problem."
        $failures++
    }
}

if ($failures -gt 0) {
    throw "$failures problem(s) in the commits of $range."
}

Write-Host "$checked commit(s) of $range follow the commit-message, changelog and released-file rule."
