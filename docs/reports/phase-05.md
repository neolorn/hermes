# Phase 5: retention, erasure and the release gate

## Items built

| Item | Tests |
|---|---|
| `01` MSG-SEND-019 (the `retention` job's deletions: content, records, lapsed idempotency rows and places, suppressions, idle subscriptions, notices, balance readings, rejected reports, push auth failures, rate counters and unknown rows) | `MSG_SEND_019_AC1_ContentGoes31DaysPastTheFinalStatusTheRecord366DaysPastItAndAWaitingDeliveryKeepsEverything`, `MSG_SEND_019_AC2_ALapsedIdempotencyRowAndALapsedLimitPlaceAreGoneAndTheLiveOnesStay`, `MSG_SEND_019_AC2_ASuppressionPastItsLifetimeAndASubscriptionIdlePastIdleRetentionAreGone`, `MSG_SEND_019_AC2_ANoticeOlderThanTheRecordsRetentionIsGoneAndItsDeletionLogged`, `MSG_SEND_019_AC2_ADeliveryOfAKindNeitherDeclaredNorRetiredGoesTheRecordsRetentionAfterTheLaterOfItsRecordingAndDueInstantLoggedByCount`, `MSG_SEND_019_AC2_OldBalanceReadingsSaveTheLastBalanceAndOldReportAuthFailureAndRateCountersAreGone` |
| `01` MSG-SEND-020 (`ISubjects`: `EraseAsync` and `ExportAsync`) | `MSG_SEND_020_AC1_AfterAnErasureNothingNamesThePersonNoKeptPlaceLinksTheirRecordsAndTheObserverIsToldOfTheCancellations`, `MSG_SEND_020_AC1_AnErasureDeletesThePersonsSubscriptionsAndThePushLimitPlacesKeyedByTheirReference`, `MSG_SEND_020_AC2_TheExportHoldsThePersonsPreferencesIdempotencyRowsAndDeliveriesWithTheirDestinationsAndContent`, `MSG_SEND_020_AC2_TheExportHoldsThePersonsSubscriptionsWithTheirServiceAndInstantsAndAPushDeliveryWithItsContent`, `MSG_SEND_020_AC2_ADeliveryPastContentRetentionIsExportedMarkedSoWithNoDestinationOrContent`, `MSG_SEND_020_AC2_AValueThatDoesNotOpenUnderItsBindingIsExportedMarkedRejectedAndRaisesContentRejected`, `MSG_SEND_020_AC2_AValueWhoseProtectorAnswersErasedIsExportedMarkedErasedWithNoDestinationOrContent`, `MSG_SEND_020_AC2_AValueThatCannotOpenYetAnswersTheExportKeyUnavailable`, `MSG_SEND_020_AC2_AProtectorThatThrowsOrDoesNotAnswerInTimeAnswersTheExportKeyUnavailable`, `MSG_SEND_020_AC3_AHashKeysVersionInUseThatTheProcessDoesNotHoldAnswersKeyUnavailableAndChangesNothing`, `MSG_SEND_020_AC4_AnErasureWhileADispatcherIsAtCheck8LeavesNoPlaceKeyedByThePersonOrLinkedToTheirDeliveriesAndNeitherSideAborts`, `MSG_SEND_020_AC5_AnUnknownMigrationAnswersReleaseNewerToAnErasureAndAnExportEvenWhereAKeyVersionIsNotHeld`, `Erase_APersonTheLibraryHoldsNothingOf_SucceedsAndChangesNothing` |
| `06` MSG-OPS-004 (the `retention` job: its daily run at `Jobs:RetentionAt`, its claim, its batches) | `MSG_OPS_004_AC2_TwoProcessesRunningTheRetentionJobAtOnceDeleteEachRowOnce`, `Retention_WithJobsOn_RunsAtRetentionAtInTheTimeZoneAndNotBefore`, `Next_TheFirstInstantAfterNowAtTheLocalTime_TakesATimeTheZoneSkipsAtTheFirstTimeAfterIt`, `Last_TheLastInstantAtOrBeforeNowAtTheLocalTime` |
| `06` MSG-OPS-008 criteria 5 to 8 and 10 (values protected again under the write version, versions nothing needs marked retired, the token versions' last-made instant written as an email goes out) | `MSG_OPS_008_AC4_TheRetentionJobProtectsADeliveryAndASubscriptionAgainUnderTheWriteVersionAndRetiresTheOldOne`, `MSG_OPS_008_AC5_AValueErasedWhileTheRetentionJobProtectsItAgainStaysErased`, `MSG_OPS_008_AC6_ARetiredHashKeysVersionCanBeRemovedWithoutStoppingAStartAndAProcessWritingUnderItClearsTheMark`, `MSG_OPS_008_AC7_WithAMigrationThisReleaseDoesNotKnowTheRetentionJobRetiresNoVersionAndProtectsNoValueAgain`, `MSG_OPS_008_AC8_ARowTheRetentionJobCannotOpenKeepsItsValuesAndColumnAndItsContentKeysVersionIsNotRetired`, `MSG_OPS_008_AC10_ATokenVersionIsKeptWhileItsEmailWaitsAndUntilTheLongestTokenLifetimeAndRetireAfterHavePassedSinceItWentOut` |
| `01` MSG-SEND-001 criterion 3, MSG-SEND-007 criterion 5, MSG-SEND-009 criterion 4 and MSG-SEND-014 criterion 6, the retention job's halves | `MSG_SEND_001_AC3_ADroppedKindsDeliveryWaitsUntilTheRetentionJobDeletesItOnceTheRecordsRetentionHasPassed`, `MSG_SEND_007_AC5_ARowOfAChannelOrReasonThisReleaseDoesNotKnowIsDeletedOnlyOnceTheRecordsRetentionHasPassedSinceTheLaterOfItsRecordingAndDueInstant`, `MSG_SEND_009_AC4_TheRetentionJobOfAnApplicationWhoseWindowIs1DayLeavesARowWrittenUnder7DaysUntilItsOwnLapse`, `MSG_SEND_014_AC6_APlaceWrittenUnderRetentionPlacesOf90DaysIsKeptByAJobWhoseOwnIs31DaysUntilThePlacesLapse` |
| `04` MSG-LIB-006 (the release compatibility gate, against an empty baseline) and its script of `07` MSG-ENG-012 | `MSG_LIB_006_AC1_AnAdditionNeedsAMinorReleaseAndTheSameSetAPatch`, `MSG_LIB_006_AC1_ARemovalOrAChangeNeedsAMajorRelease`, `MSG_LIB_006_AC1_AMemberAddedToAHostImplementedInterfaceWithoutADefaultAndARequestMemberACallerMustSendAreBreaks`, `MSG_LIB_006_AC1_BeforeTheFirstReleaseTheBaselineIsEmptySoNothingIsABreak`, `MSG_LIB_006_AC1_TheCurrentSetHoldsEverySectionTheGateCompares`, `MSG_LIB_006_AC1_EveryPublicInterfaceOfTheContractsIsOneAHostImplementsOrOneTheLibraryDoes`, `MSG_LIB_006_AC1_BeforeTheFirstReleaseTheBaselineIsEmptySoTheGatePassesWithoutComparing`, `MSG_LIB_006_AC1_OnAPullRequestAChangeThatNeedsAMajorReleasePassesOnlyWhereACommitSinceTheReleaseMarksTheBreak`, `MSG_LIB_006_AC1_AReleaseTagBelowWhatTheChangeNeedsFails`, `ReleaseGate_WritesTheCurrentSetAndWhatTheChangeFromTheBaselineNeeds` |
| `07` MSG-ENG-012's log gate, `06` MSG-OPS-005 criterion 1 (every integration test's engine host checks its library log lines at its stop) | `MSG_OPS_005_AC1_ATestWhoseLibraryLogsADestinationItSentFailsWhenItsHostStops`, and every integration test that starts an engine host |
| `10` MSG-THR-001 | `MSG_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement`, `MSG_THR_001_AC1_AControlNoTestCarriesIsFoundAndTheDeploymentsAreNot` |
| `10` MSG-THR-002 | `MSG_THR_002_AC1_NoRouteOfTheFourGroupsRecordsASend` |
| `10` MSG-THR-003 | `MSG_THR_003_AC1_ASubscriptionAnotherPersonRegistersBeforeOrAfterASendIsNeverUsedForIt` |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |
| `02` MSG-CHAN-001 criterion 4, the SMS half | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-003 criterion 4 | 6, 7 | Unchanged from phase 2 |
| `06` MSG-OPS-004 criterion 1, the shipped SMS provider | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-006, a release's baseline | 8 | The release step writes `build/gates/release-baseline.json` from the set the gate's test writes; until then the baseline is empty |
| `07` MSG-ENG-006, the endpoints' contract test | Not placed | Phase 0 took `07` before any endpoint existed, and no later phase names this criterion; the release gate reads the routes, statuses and bodies from the endpoint metadata and serializer context that test will read |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds: no identifier, text, default or fixture names a host product, company or application. The fixtures use `example.com`, `example.net`, `example.test`, `localhost` and `127.0.0.x`. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each `feat` commit carries its tests; the `test` commits prove criteria whose code an earlier commit holds. |
| `07` MSG-ENG-009, the packages added | No package is added. |
| `07` MSG-ENG-001, the grants | This phase adds no project and no grant. |
| `04` MSG-LIB-006, the gate's whole path | Run once in a clone outside the repository, with a fake `v0.1.0` whose baseline held a code the build no longer has and no commit marking a break: the gate built the set, found the code removed, and failed for want of `!` or `BREAKING CHANGE:`. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- The log gate over the whole integration set: this phase ran only the integration tests it writes or touches, and the pipeline's run is the first over every test with the check in place.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

| Place | Out of step | Governing item | Rule applied |
|---|---|---|---|
| `Subjects` | What an export holds of a delivery's unsubscribe address | `01` MSG-SEND-020 | Left out of `Content`: it is the library's own link, not content the host sent. |
| `Subjects` | The order of an erasure's steps | `01` MSG-SEND-020 | In one transaction: the person's locks, the unknown-migration check, the `hashKeys` check, the cancellations with their notices, then the deletions and the clearing of each delivery's person, destination, content and key version. |
| `RetentionJob` | The balance readings past `Sms:DrainBaseline` | `01` MSG-SEND-019; `02` MSG-SMS-006 | Deleted, save the last reading holding a balance, which MSG-SMS-006's floor reads. |
| `RetentionJob` | `held_reports` past their hold | `02` MSG-SMS-004 | Left to the `watch` job, which drops them, as phase 4 built. |
| `RetentionJob` | A local `RetentionAt` the time zone skips | `06` MSG-OPS-004 | The first minute after it that exists. |
| `RetentionKeys` | A row holding a value the job cannot open, or a value not of the library's protector | `06` MSG-OPS-008 | The row is left as it is, values and version, and the version it names stays in use. |
| `RetentionKeys` | A value the write version can no longer reserve | `06` MSG-OPS-008 | The table's protection stops for that run; the values stay under their version. |
| `RetentionKeys` | When an email's token may still name a version | `06` MSG-OPS-008 | While a delivery that is not final holds an unsubscribe address recorded before `RetireAfter` had passed since the version was last made, and until the longest recorded token lifetime, `RetireAfter` and `Dispatch:Interval` have passed since then. |
| `RetentionKeys` | `smsReportKey` | `06` MSG-OPS-008 | Never marked retired: a report address the provider holds names its version for as long as it may report, which no row records. |
| `DispatchPasses` | How often an email pass writes its token's versions as made | `06` MSG-OPS-008 | At most once a `Dispatch:Interval` a version, which is why the retirement waits an interval more. |
| `PushSubscriptions` | An endpoint another person registers after a send to its holder | `10` MSG-THR-003; `02` MSG-PUSH-003 | The subscription moves to that person, and the first person's delivery finds no subscription. |
| `ThreatModelRules` | Requirements the control column names that the library does not carry | `10` MSG-THR-001 | `06` MSG-OPS-007 (the deployment's), MSG-THR-002 (the host's own controls) and `02` MSG-SMS-003 (named only as the plain HTTP a report key can leak over). |
| `ReleaseSet` | The routes' prefix | `04` MSG-LIB-006 | The set maps each group under `/notifications`, so a route is compared by one stable pattern. |
| `ReleaseSet` | The interfaces a host implements | `04` MSG-LIB-006, MSG-LIB-002 | The provider contracts and seams; a contract test holds every public interface of `Hermes.Core` to that list or to the four the library implements. |
| `ReleaseComparison` | A member added to an interface or a request body new since the baseline | `04` MSG-LIB-006 | Minor: a new interface or body binds no host or caller yet. |
| `gate-release-compatibility` | The gate with no release before the commit | `04` MSG-LIB-006 | It passes without building, the baseline being empty. |
| `EngineHost` | Which log lines the log gate reads, and which values | `07` MSG-ENG-012; `06` MSG-OPS-005 | The library's own categories, against each destination, person reference and text value a test sends of at least four characters, so a short code cannot match a count or a time. |

## Open questions

This phase asked none.

## Commit and checks

The report names commit `f8bf7d8b33aab30c17d8ed347b03bf16a3f943e0`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,116 (`Hermes.Tests` 502, `Hermes.Channels.Tests` 359, `Hermes.Repository.Tests` 182, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14); the Janus test projects hold no test yet |
| Integration tests this phase writes or touches, run here by class | Passed |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 240 criteria |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 8 commits after `main` |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 115 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

`tmp/` was emptied at the start of the phase, the secret scanner included, which was fetched again before the scan.