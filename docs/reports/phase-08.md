# Phase 8: the release step

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-005 criterion 3: the seam `ISmsProvider` required where SMS is used or the report group is mapped, under `AddHermes` and `AddHermesChannels` alike (`82338da`) | `MSG_LIB_005_AC3_SmsUsedWithNoSmsProviderStopsTheStartNamingIt`, `MSG_LIB_005_AC3_TheReportGroupMappedWithNoSmsProviderStopsTheStartNamingIt`, `MSG_LIB_005_AC3_AnEmailOnlyApplicationThatMapsNoReportGroupStartsWithNoSmsProvider`, `MSG_LIB_005_AC3_TheProvidersAloneWithTheSmsSectionPresentAndNoSmsProviderDoNotStartNamingIt`, `MSG_LIB_005_AC3_TheProvidersAloneWithNoSmsSectionStartWithNoSmsProvider` |
| `FixedSecretSource` without `smsWe` (`c9c9aab`) | `FixedSecretSource_New_AnswersVersionOneOfEachSecretInItsFormAndNoConnection`, `MSG_LIB_003_AC3_AnEmailOnlyApplicationStartsWithNoSmsOrPushKeyAndNoSmsOrPushSecret` |
| The `Hermes.Channels` package description, as its cell in `00` section 2.2 now reads (`160b5fc`) | `Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone` |
| `10` MSG-THR-001, MSG-SMS-003 out of the exemptions, since the table now names `05` MSG-API-006 (`efebdd5`) | `MSG_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement` |
| `02` MSG-CHAN-001 criterion 4, as M-022 has it (push alone) | `MSG_CHAN_001_AC4_PushRateLimitedUnderAHostsRetryingHandlerIsAnsweredAtOnce` |
| `04` MSG-LIB-003 criterion 4, as M-022 has it (no SMS key or secret named) | `MSG_LIB_003_AC4_AnApplicationThatDoesNotDispatchAndRegistersNoProvidersAloneStartsWithNoEmailOrPushProviderKeyOrSecret`, `MSG_LIB_003_AC4_TheProvidersAloneWithTheEmailSectionPresentDoNotStartWithoutEmailSubmission` |
| `06` MSG-OPS-004 criterion 1, its SMS half through a provider the host registers | `MSG_OPS_004_AC1_WithHostedOffNoPassRunsUntilRunPassAsyncIsCalled` (SMS, the host's provider), `MSG_OPS_004_AC1_WithHostedOffAPassRunOnDemandDeliversPushThroughTheShippedProvider`, `MSG_OPS_004_AC1_WithDispatchingOffRunPassAsyncThrowsAndTheApplicationStillRecordsSends`, `MSG_OPS_004_AC1_WithDispatchOffTheObserverJobDoesNotRun` |
| `07` MSG-ENG-011 criteria 2 and 3, the commit gate's check of a release commit (`a330ee4`) | `MSG_ENG_011_AC2_TheReleaseCommitChangingItsFilesAsTheValueGivesPasses`, `MSG_ENG_011_AC3_AReleaseCommitChangingAFileOutsideItsValueFails`, `MSG_ENG_011_AC3_AReleaseCommitChangingOneOfItsFilesOtherwiseThanTheValueGivesFails`, `MSG_ENG_011_AC3_AReleaseCommitDeletingTheReleaseNotesFails`, `MSG_ENG_011_AC3_OnlyADocsCommitChangingOnlyDocsFollowsTheReleaseCommitInItsPullRequest`, `MSG_ENG_011_AC3_ACommitAfterTheMergeOfTheReleaseCommitsPullRequestIsNotItsFollower` |
| `04` MSG-LIB-006 and `07` MSG-ENG-011 criterion 3, the release gate's check of a changed baseline at a pull request's head, before the first release as after it (`d52e47c`) | `MSG_ENG_011_AC3_APullRequestChangingTheBaselinePassesOnlyWhereTheBaselineAtItsHeadIsTheSetReadThere`, `MSG_ENG_011_AC3_APullRequestThatLeavesTheBaselineDoesNotCompareItWithTheSetReadThere` |
| `07` MSG-ENG-011's release job: only for a release tag, after the gates job, the one job that can write to the contents, packing every package at the tag's version and creating the tag's release with `gh`, its notes `RELEASE-NOTES.md`, the packages attached (`c4013b5`) | `MSG_ENG_011_AC4_TheReleaseJobAloneCanWriteToTheContentsAndRunsOnlyForAReleaseTagAfterTheGates`, `MSG_ENG_011_AC4_AWorkflowOutsideTheReleaseRuleFails` (the workflow's shape; the criterion itself awaits the tag's run, below) |
| The review's first fix: the release job's checkout sets `persist-credentials: false`, so its token, which can write to the contents, is not left in `.git/config` while the restore and the build run; `gh` takes it from `GH_TOKEN` alone (`4792ac9`) | `MSG_ENG_011_AC4_TheReleaseJobAloneCanWriteToTheContentsAndRunsOnlyForAReleaseTagAfterTheGates`, `MSG_ENG_011_AC4_AWorkflowOutsideTheReleaseRuleFails` (the cases without the setting and with it `true`) |
| The review's second fix: before it packs, the release job refuses a tag whose commit's subject is not `chore: release` and the tag's version, so a tag on a merge commit or a report's commit releases nothing (`9cfabe7`) | `MSG_ENG_011_AC4_TheReleaseJobAloneCanWriteToTheContentsAndRunsOnlyForAReleaseTagAfterTheGates`, `MSG_ENG_011_AC4_AWorkflowOutsideTheReleaseRuleFails` (the cases without the expected subject and reading another commit's) |
| The release commit `chore: release 1.0.0` (`c87626d`), as `07` MSG-ENG-011's release commit value gives it: each `PublicAPI.Shipped.txt` holds its entries and those of its `PublicAPI.Unshipped.txt` (`Hermes.Core` 974, `Hermes.Testing` 33, `Hermes` 26, `Hermes.Channels` 2, `Hermes.Cli` none; no entry marked `*REMOVED*`) under `#nullable enable` in ordinal order; each `PublicAPI.Unshipped.txt` holds `#nullable enable` alone; `build/gates/release-baseline.json` is the set the release gate reads at that commit; `CHANGELOG.md`'s Unreleased entries are under `## [1.0.0] - 2026-10-29`, the commit's date in UTC, below an empty `## [Unreleased]`; `RELEASE-NOTES.md` is as `83e7083` wrote it | `gate-commit-messages` and `gate-release-compatibility` on it; see below |
| `RELEASE-NOTES.md` for 1.0.0, as `07` MSG-ENG-001 and `06` MSG-OPS-007 give it (`83e7083`) | Checked in review; see below |
| The test-name gate's list: MSG-CHAN-001 AC4, MSG-LIB-003 AC4, MSG-LIB-005 AC3, MSG-ENG-011 AC3 (`84e81e8`) | `gate-test-names`: 246 criteria |

Test hosts register a host's SMS provider first, which a test's own registration replaces, since the library ships none.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| Plan phase 7, the SMS gateway | None | Retired by M-022: the library ships no SMS gateway; a host that uses SMS registers its own `ISmsProvider` |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds or changes: no identifier, text, default or fixture names a host product, company or application. |
| M-022, no SMS gateway outside `docs/` | `git grep -i` over the repository, `docs/` aside, for the gateway's name, `smsWe`, `Sms:We`, `Hermes.Sms`, adapter, gateway and telecom finds nothing but the statement in `RELEASE-NOTES.md` that the library ships no SMS gateway. `Egypt Standard Time` and `ar-EG` are a time zone and a language of the fixtures, not a gateway. |
| `06` MSG-OPS-007 criterion 1 | We read `RELEASE-NOTES.md` against each value of MSG-OPS-007: every requirement is stated. No migration of 1.0.0 drops or rewrites data: each migration's `Up` creates, and drops nothing (`06` MSG-OPS-002). |
| `07` MSG-ENG-011 criteria 2 and 3 and `04` MSG-LIB-006 on the release commit | `gate-commit-messages` passes on the release commit, which it checks as a release commit; `gate-release-compatibility -Base origin/main -Head c87626d` reads the set at that commit and finds the baseline there equal to it. The baseline was written from the release gate's own read at that tree, its line endings LF. |
| The release gate's read, outside its tests | On this branch, the gate run with a base before the baseline existed read the set and reported each difference from the empty baseline at the head; run on a commit (not pushed, on no branch) whose baseline is the set the read wrote, it passed. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each new case of the commit gate, the release gate and the workflow rule failed before the change that passes it; the seam's tests failed before the seam check. |
| `07` MSG-ENG-009, the packages added | No package is added. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `07` MSG-ENG-011 criterion 4: the release tag's run, which creates the tag's release holding every package at the tag's version only once its gates have passed. The tag `v1.0.0` goes on the release commit after the merge, on the owner's word.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The changelog heading's date.** "The commit's date in UTC" is the release commit's author date, which the platform's merge of its pull request keeps; the commit gate reads it in UTC, so a commit made after midnight east of UTC carries the day before.
2. **The release commit's followers in its pull request.** The commit gate takes them as the commits after the release commit up to the head, save the platform's merge of its pull request and what follows that merge, so a later pull request on `main` is no follower.
3. **The release commit and `RELEASE-NOTES.md`.** The release job reads the tag's `RELEASE-NOTES.md`, so the commit gate fails a release commit that holds none; the file is written here, before the release commit, on the owner's order, and the release commit's value still lets that commit change it.
4. **The baseline's comparison.** The release gate compares the baseline at the head with the set read there section by section, as sets, an empty section the same as none; the release commit writes the file from that set.
5. **No package feed.** The owner's messages name none, so the release job publishes to none.
6. **The release commit's date.** The commit carries as its author date the instant its changelog heading was dated from (`2026-10-29T00:58:05Z`), so the heading and the date the commit gate reads agree.

## Open questions

This phase asked none.

## Commit and checks

The report names the release commit, `c87626db9b0d626de23c1826cbc962cf6202910f`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,156 (`Hermes.Tests` 507, `Hermes.Channels.Tests` 362, `Hermes.Repository.Tests` 214, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14) |
| Every contract test | Passed: 58 (`Hermes.Repository.Tests` 40, `Hermes.Tests` 18) |
| Integration tests this phase touches, run here by class | Passed on the release commit: `CommitMessageGateTests`, `ReleaseGateTests`, `WorkflowTests`, `PackTests` (93 tests). Passed on `83e7083`: `SampleHostGateTests`; in `Hermes.Tests`, the classes on the test host this phase changes: `AlertTests`, `ChannelsUnderEngineTests`, `ContentProtectionTests`, `DispatchTests`, `KeyTableTests`, `LogTests`, `SchemaTests`, `SendTests` (172 tests). `HostedDispatchTests`, `SmsReportTests`, `BalanceTests` and `Hermes.Testing.Tests`' integration tests passed on `efebdd5`, and no commit since changes `src/` or those tests |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 246 criteria |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 14 commits after `main`, the release commit checked as one; and the whole history, 143 commits on `83e7083` |
| The release gate on the release commit (`gate-release-compatibility`) | Passed: the baseline at the head is the set read there; no release precedes it, so nothing is a break |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 150 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

The secret scanner was fetched again before the scan.
