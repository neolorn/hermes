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
| `RELEASE-NOTES.md` for 1.0.0, as `07` MSG-ENG-001 and `06` MSG-OPS-007 give it (`83e7083`) | Checked in review; see below |
| The test-name gate's list: MSG-CHAN-001 AC4, MSG-LIB-003 AC4, MSG-LIB-005 AC3, MSG-ENG-011 AC3 (`84e81e8`) | `gate-test-names`: 246 criteria |

Test hosts register a host's SMS provider first, which a test's own registration replaces, since the library ships none.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| Plan phase 7, the SMS gateway | None | Retired by M-022: the library ships no SMS gateway; a host that uses SMS registers its own `ISmsProvider` |
| The release commit `chore: release 1.0.0`: the Shipped and Unshipped moves, the baseline, the changelog's heading | 8 | It comes in this pull request once the review has passed, on the owner's word |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds or changes: no identifier, text, default or fixture names a host product, company or application. |
| M-022, no SMS gateway outside `docs/` | `git grep -i` over the repository, `docs/` aside, for the gateway's name, `smsWe`, `Sms:We`, `Hermes.Sms`, adapter, gateway and telecom finds nothing but the statement in `RELEASE-NOTES.md` that the library ships no SMS gateway. `Egypt Standard Time` and `ar-EG` are a time zone and a language of the fixtures, not a gateway. |
| `06` MSG-OPS-007 criterion 1 | We read `RELEASE-NOTES.md` against each value of MSG-OPS-007: every requirement is stated. No migration of 1.0.0 drops or rewrites data: each migration's `Up` creates, and drops nothing (`06` MSG-OPS-002). |
| The release gate's read, outside its tests | On this branch, the gate run with a base before the baseline existed read the set and reported each difference from the empty baseline at the head; run on a commit (not pushed, on no branch) whose baseline is the set the read wrote, it passed. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each new case of the commit gate, the release gate and the workflow rule failed before the change that passes it; the seam's tests failed before the seam check. |
| `07` MSG-ENG-009, the packages added | No package is added. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `07` MSG-ENG-011 criterion 4: the release tag's run, which creates the tag's release holding every package at the tag's version only once its gates have passed. No tag is pushed in this phase.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The changelog heading's date.** "The commit's date in UTC" is the release commit's author date, which the platform's merge of its pull request keeps; the commit gate reads it in UTC, so a commit made after midnight east of UTC carries the day before.
2. **The release commit's followers in its pull request.** The commit gate takes them as the commits after the release commit up to the head, save the platform's merge of its pull request and what follows that merge, so a later pull request on `main` is no follower.
3. **The release commit and `RELEASE-NOTES.md`.** The release job reads the tag's `RELEASE-NOTES.md`, so the commit gate fails a release commit that holds none; the file is written here, before the release commit, on the owner's order, and the release commit's value still lets that commit change it.
4. **The baseline's comparison.** The release gate compares the baseline at the head with the set read there section by section, as sets, an empty section the same as none; the release commit writes the file from that set.
5. **No package feed.** The owner's messages name none, so the release job publishes to none.

## Open questions

This phase asked none.

## Commit and checks

The report names commit `83e708347941e60e9e335442d249ed205ec57eb6`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,152 (`Hermes.Tests` 507, `Hermes.Channels.Tests` 362, `Hermes.Repository.Tests` 210, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14) |
| Every contract test | Passed: 58 (`Hermes.Repository.Tests` 40, `Hermes.Tests` 18) |
| Integration tests this phase touches, run here by class | Passed: `CommitMessageGateTests`, `ReleaseGateTests`, `PackTests`, `SampleHostGateTests` (80 tests); in `Hermes.Tests`, the classes on the test host this phase changes: `AlertTests`, `ChannelsUnderEngineTests`, `ContentProtectionTests`, `DispatchTests`, `KeyTableTests`, `LogTests`, `SchemaTests`, `SendTests` (172 tests). `HostedDispatchTests`, `SmsReportTests`, `BalanceTests` and `Hermes.Testing.Tests`' integration tests passed on `efebdd5`, and no commit since changes `src/` or those tests |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 246 criteria |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 10 commits after `main`; and the whole history, 143 commits |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 146 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

The secret scanner was fetched again before the scan.
