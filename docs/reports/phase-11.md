# Phase 11: release candidates

## Items built

| Item | Tests |
|---|---|
| The amendments of M-027 under `docs/` (`dade2d6`) | None; the documents themselves |
| The tags `v1.0.0`, `v1.1.0` and `v1.2.0` taken out of the clone with `git tag -d`, once the owner had deleted the releases and the remote's tags; no tag of the remote was touched | None; see below |
| `07` MSG-ENG-011 in the commit-message gate (`0c6c559`): the release commit `chore: release V` and its changelog heading `## [V] - YYYY-MM-DD` for V a final release `X.Y.Z` or a release candidate `X.Y.Z-rc.N`, `N` from 1; a commit whose first line begins `chore: release ` and names anything else fails; a release candidate's tag is a release, for the changelog's `Added` while nothing is released and for the previous release tag from which a release tag's run checks, which `previous-release-tag.ps1` takes in Semantic Versioning's precedence | `MSG_ENG_011_AC2_TheReleaseCommitChangingItsFilesAsTheValueGivesPasses` (`1.0.0`, `1.0.0-rc.1`, `1.0.0-rc.10`, `2.3.4-rc.2`), `MSG_ENG_011_AC3_AReleaseCommitNamingAVersionOutsideBothFormsFails` (ten forms), `MSG_ENG_011_AC3_AReleaseCandidatesReleaseCommitWhoseChangelogHeadingNamesAnotherVersionFails`, `MSG_ENG_011_AC3_OnlyADocsCommitChangingOnlyDocsFollowsTheReleaseCommitInItsPullRequest` (a final release's and a release candidate's), `MSG_ENG_011_AC1_ChangelogLineUnderChangedPassesOnceAReleaseIsTagged` (`v1.0.0`, `v1.0.0-rc.1`, `v1.0.0-rc.10`), `MSG_ENG_011_AC1_ChangelogLineUnderChangedFailsAfterATagThatIsNoRelease` (`v1.0.0-rc.1` moved out of it, `v1.0.0-rc.0`, `v1.0.0-rc.01`, `v1.0.0-beta.1` and `v1.0.0-RC.1` in), `PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut`, `PreviousReleaseTag_AReleaseCandidate_IsAReleaseTakenInSemanticVersioningsPrecedence`, `PreviousReleaseTag_FirstRelease_IsNone` |
| `04` MSG-LIB-006 in the release gate (`c9d12a2`): the baseline is what the tag of the newest final release before Head holds, on a pull request as on a release tag, a release candidate's tag never being it, so before the first final release nothing binds; a release tag's version, a release candidate's included, is held to what its `X.Y.Z` needs against that release; the `!` or `BREAKING CHANGE:` a pull request's break needs is looked for since that release; `previous-release-tag.ps1 -Final` gives that release | `MSG_LIB_006_AC2_WithNoFinalReleaseTaggedAPullRequestAndAReleaseTagThatRemoveAPublicMemberPass` (a pull request, `v1.0.0-rc.2` and `v1.0.0`), `MSG_LIB_006_AC2_WithAFinalReleaseTaggedTheSameRemovalFailsInAPullRequestAndInAReleaseCandidatesTag` (a pull request and `v1.1.0-rc.1`), `MSG_LIB_006_AC1_APullRequestIsComparedWithTheNewestFinalReleaseAndNeverWithANewerReleaseCandidate`, `MSG_LIB_006_AC1_TheBaselineComparedWithIsWhatTheNewestFinalReleasesTagHoldsNotTheBaseOrAReleaseCandidate`, `MSG_LIB_006_AC1_AReleaseTagBelowWhatTheChangeNeedsFails` (six release candidates' tags added), `MSG_LIB_006_AC1_BeforeTheFirstReleaseTheBaselineIsEmptySoTheGatePassesWithoutComparing`, `PreviousReleaseTag_Final_LeavesOutEveryReleaseCandidate`, and the gate's other tests, unchanged |
| `07` MSG-ENG-011's release job (`0805537`): the pipeline runs for a release candidate's tag, `v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+`, beside a final release's, and the release job passes `--prerelease` to `gh release create` for a tag `vX.Y.Z-rc.N`, `N` from 1, and for no other | `MSG_ENG_011_AC4_TheReleaseJobAloneCanWriteToTheContentsAndRunsOnlyForAReleaseTagAfterTheGates`, `MSG_ENG_011_AC4_AWorkflowOutsideTheReleaseRuleFails` (the release candidates' tags missing or widened; the pre-release flag missing, always passed, or read from another tag form) |
| The test-name gate's list: MSG-LIB-006 AC2 (`ed9b268`) | `gate-test-names`: 286 criteria |
| `CHANGELOG.md`'s sections `1.2.0`, `1.1.0` and `1.0.0` gathered under `## [Unreleased]`, one `### Added` holding their 75 entries (`fa279fb`) | `gate-commit-messages`; see below |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The release commit `chore: release 1.0.0-rc.1`, with `RELEASE-NOTES.md` as one section for 1.0.0-rc.1 | 11, after the review | It comes in this pull request once the review has passed, on the lead's message (the plan's row 11) |
| The tag `v1.0.0-rc.1` | 11, after the merge | `07` MSG-ENG-011: the tag goes on the release commit once the merge has brought it into `main`, on the lead's message |
| `07` MSG-ENG-011 criterion 4 for 1.0.0-rc.1, its release marked a pre-release | 11, the tag's run | The release tag's run |
| `06` MSG-OPS-007 criterion 1 for 1.0.0-rc.1 | 11, the release commit | `RELEASE-NOTES.md` is written in the release commit |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| The tags taken out of the clone | `git tag --list` lists none, and `git ls-remote --tags origin` lists none, before and after. |
| `CHANGELOG.md`'s gathering loses no entry | The entry lines of the file before and after, headings and blank lines aside and sorted, are identical: 75 entries. In a scratch clone of the branch, a commit `chore: release 1.0.0-rc.1` moving them under `## [1.0.0-rc.1] - 2026-10-31` passes `gate-commit-messages`, on the commits after `main` and on the whole history (193 commits). |
| `04` MSG-LIB-006, the read of the current set against the newest final release's baseline | The read itself is the release set test of `Hermes.Tests`, unchanged; the gate's test above proves that the baseline and the shipped lists laid out for it are the final release's, not the base's or a release candidate's. With no tag in the clone, `gate-release-compatibility -Base origin/main` answers that no final release precedes Head. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each gate's tests were run red before its change: 25 of the commit gate's, 14 of the release gate's and 8 of the workflow's. |
| `07` MSG-ENG-009, the packages added | None. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `07` MSG-ENG-011 criterion 4: the tag `v1.0.0-rc.1`'s run, which triggers on the release candidate's tag pattern and creates the tag's release, marked a pre-release, holding every package at the tag's version, only once its gates have passed.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **A release commit's name.** A commit whose first line begins `chore: release ` names a release, and fails where what follows is not `X.Y.Z` or `X.Y.Z-rc.N` with `N` from 1 (`1.0.0-rc.0`, `1.0.0-beta.1`, `v1.0.0`, `the next version`); `07` MSG-ENG-011 names the release commit by its message `chore: release V` and its criterion 3 fails a version outside the two forms, so whatever stands in V's place is read as the version. `X.Y.Z` keeps the gate's earlier reading, three runs of digits.
2. **A release candidate's tag is a release in the commit gate.** `07` MSG-ENG-011 gives a release as a tag `v<V>`, V a final release or a release candidate, so a release candidate's tag ends "`Added` while nothing is released", and the commits a release tag's run checks are those since the previous release tag, a release candidate's included (MSG-ENG-012).
3. **Precedence.** The previous release tag is the highest in Semantic Versioning 2.0.0's precedence: `X.Y.Z` numerically, a release candidate below its final release, `rc.N` numerically (`04` MSG-LIB-006: "`rc.10` follows `rc.9`").
4. **The pipeline's tag pattern.** GitHub's filter patterns cannot say "`N` from 1", so `v[0-9]+.[0-9]+.[0-9]+-rc.[0-9]+` also starts a run for `v1.0.0-rc.0`; such a tag's run releases nothing, since the release job releases only a tag on the commit `chore: release` of its version, which the commit gate in that run's gates fails, and the pre-release flag is given only for `N` from 1 (`07` MSG-ENG-011: the release job runs after the gates job has passed).
5. **The baseline before the first final release.** Where no final release precedes Head and a pull request changes the baseline, the set is read against an empty baseline and no shipped list (`04` MSG-LIB-006: "before the first final release no such tag holds one, so the baseline is empty").
6. **`-Base` of the release gate.** It now names only the commit after which the baseline's change is checked, the comparison reading the final release's tag; the gate still refuses to run without it where a final release precedes Head, since it would otherwise leave the baseline's change unchecked (`04` MSG-LIB-006).
7. **The gathered entries' order.** One `### Added`, every entry being one, in the order they were added, 1.0.0's, then 1.1.0's, then 1.2.0's, as a commit adds its line at the end of its section (`07` MSG-ENG-011's changelog value; Keep a Changelog 1.1.0 orders the types, not the entries).

## Open questions

None. The commit gate's changelog rules and the gathering do not conflict: every gathered entry is under `Added`, which the rule asks while nothing is released, and the simulated release commit passes the gate.

## Commit and checks

The report names `fa279fb84a1d5b2ff98e3071de67da23302d7667`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,023 (`Hermes.Tests` 1,003, `Hermes.Repository.Tests` 455, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 43, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (6 commits after `main`), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-sample-host` and `gate-test-names` (286 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 192 commits, no leaks |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner was fetched again before the scan.

The working folder `tmp/` was emptied at the start of the run, save the secret scanner's, the tests' and the tools' folders.
