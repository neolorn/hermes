# Phase 10: the in-application list

## Items built

| Item | Tests |
|---|---|
| The amendments of M-026 under `docs/` (`7ab039e`) | None; the documents themselves |
| `04` MSG-LIB-002: `IInApp`, `InAppPage`, `InAppEntry`, `EntryId`, `InAppEntryExport` and `SubjectExport.Entries`, none printing a part, a target or a target value (`45b2ee1`) | `MSG_LIB_002_InAppEntryPrintsNeitherItsPartsNorItsTargetNorItsTargetValues`, `MSG_LIB_002_InAppEntryExportPrintsNeitherItsContentNorItsTargetNorItsTargetValues`, and the public type table's rows |
| `08` the channel `inapp` and the status `read`; `06` MSG-OPS-002: the migration `InAppList`, which widens the closed sets' checks and adds the table `entries` and the column `deliveries.parts`, dropping and rewriting nothing (`5893224`) | `GateMigrations_ThisRepository_AppliesTheMigrationsThenNothing` (nine migrations, then none), `Insert_DeliveryInsideTheClosedSets_IsTaken` (`inapp`, `read`), and the release set's `IInApp` row |
| `08` the keys `Hermes:Retention:InApp` and `Hermes:InApp:AlwaysOn` and the codes `messaging.preference.fixed`, `messaging.inapp.cursorinvalid` and `messaging.inapp.unknown` (`ce80c77`) | The rows of `ConfigurationTests` for each key's rule, its default and its reload |
| `02` MSG-INAPP-001 and MSG-INAPP-002 at the send; `01` MSG-SEND-001 and MSG-SEND-003; `03` MSG-TPL-001 and MSG-TPL-002: `KindBuilder.InApp(target, targetValues)` checked at startup; the parts rendered in every declared language in its culture, within 128 and 1,024 characters; the target values taken from the send; the channel refused for an anonymous recipient (`3aab963`) | `MSG_INAPP_001_AC1_ATargetValueTheSendDoesNotPassIsAFaultOfTheCallingCode`, `MSG_INAPP_001_AC1_AnSmsSendOfAnInAppKindNeedsNoTargetValue`, `MSG_INAPP_001_AC2_ASendNamingInappForAnAnonymousRecipientIsRefusedAndRecordsNothing`, `MSG_INAPP_001_AC3_AnInAppTargetOrTargetValueOutsideItsRuleFailsStartupNamingTheKind`, `MSG_INAPP_001_AC3_AnInAppKindWithNoTargetOrWithATargetAndItsValuesIsAccepted`, `MSG_INAPP_002_AC1_AnInAppDeliveryHoldsItsPartsInEveryDeclaredLanguageEachInItsLanguageAndItsTargetValues`, `MSG_INAPP_002_AC2_ASendWhoseEnglishTitleRendersPastItsLengthIsRefusedInWhateverLanguageItIsSent`, `MSG_INAPP_002_AC2_ABodyPastItsLengthIsRefused`, `MSG_SEND_003_AC3_APersonsSendWithNoChannelNamedIncludesTheInAppChannelAndAnAnonymousOneLeavesItOut`, `MSG_TPL_004_AC1_AnInAppTitleMissingForADeclaredLanguageStopsTheStartNamingIt`, `MSG_TPL_004_AC1_AnInAppKindNeedsNoBody` |
| `02` MSG-INAPP-001 at dispatch and MSG-INAPP-004's check 4; `01` MSG-SEND-006, MSG-SEND-007, MSG-SEND-011, MSG-SEND-013, MSG-SEND-014 and MSG-SEND-017: the in-app pass, with no counter and no quiet hours, limits counted per person, consent still asked, each value protected again under the entry's row, the entry written and the delivery ended `sent` in one transaction (`210e68e`) | `MSG_INAPP_001_AC1_ADispatchedInAppDeliveryLeavesOneEntryInItsPersonsListEndsSentAndHoldsNoRenderedPartOfItsOwn`, `MSG_SEND_007_AC1_AnInAppPassTakesNoPlaceInARateCounter`, `MSG_SEND_010_AC1_AnInAppDeliveryOfACategoryThePersonTurnedOffOnInappEndsOptedOut`, `MSG_INAPP_004_AC1_WithTheKeyOnAnInAppDeliveryOfACategoryThePersonTurnedOffIsWrittenAndTheirSmsIsNotSent`, `MSG_INAPP_004_AC2_WithTheKeyOnAnInAppDeliveryOfACategoryRequiringConsentToAPersonWhoDoesNotConsentEndsNoConsent`, `MSG_SEND_013_AC1_QuietHoursNeverHoldAnInAppDelivery`, `MSG_SEND_014_AC1_AnInAppLimitCountsPerPerson`, `MSG_SEND_017_AC1_TheObserverIsToldOfAnInAppDeliverysMoveToSent` |
| `02` MSG-INAPP-002 at the read and MSG-INAPP-003; `01` MSG-SEND-017: `IInApp`, registered scoped; a page newest first with an opaque cursor; the language matched to the entry's, then the default, then the send's; values opened after the read's transaction; an entry marked read, or all of them, moving each delivery from `sent` to `read` (`e54e9a4`) | `MSG_INAPP_003_AC1_TwentyFiveEntriesReadWithSizeTwentyAnswerTheNewestTwentyAndACursorWhosePageAnswersTheOtherFiveAndNoCursor`, `MSG_INAPP_002_AC1_AnEntrySentInArabicReadAskingEnAnswersItsEnglishPartsAndAskingAnUndeclaredLanguageItsDefaultLanguagesParts`, `MSG_INAPP_003_AC2_MarkingAnEntryReadLowersTheUnreadCountByOneAndTellsTheObserverMarkingItAgainChangesNothing`, `MSG_INAPP_003_AC2_MarkingEveryEntryReadAnswersHowManyItMarkedAndMovesTheirDeliveriesToRead`, `MSG_INAPP_003_AC3_MarkingAnotherPersonsEntryReadAnswersInappUnknownAndChangesNothing`, `MSG_INAPP_003_ACursorThatDoesNotReadAnswersInappCursorInvalid`, `MSG_INAPP_003_ASizeOutsideOneToAHundredIsAFaultOfTheCallingCode`, `MSG_INAPP_003_AnEntryWhoseValueDoesNotOpenUnderItsBindingIsLeftOutAndRaisesContentRejected` |
| `02` MSG-INAPP-004 for the preferences; `01` MSG-SEND-010; `05` MSG-API-003: `SetAsync` refuses the in-app channel with `messaging.preference.fixed`, a 422 on the route, and the read and `GET /preferences` leave it out (`843bbaa`) | `MSG_INAPP_004_WithTheKeyOnSettingTheInAppChannelIsRefusedFixedAndReadingLeavesItOut`, `MSG_INAPP_004_WithTheKeyOffTheInAppChannelIsSetAndReadAsAnyOther`, `MSG_API_003_WithTheKeyOnAPutOfTheInAppChannelIs422FixedAndGetLeavesItOut` |
| `02` MSG-INAPP-005 criterion 1; `01` MSG-SEND-019; `06` MSG-OPS-004 and MSG-OPS-008: entries older than `Hermes:Retention:InApp` deleted in batches, a final delivery's parts cleared with its content, entries and parts protected again under the write version, a `contentKeys` version an entry needs never retired (`1f6e2aa`) | `MSG_INAPP_005_AC1_AnEntry181DaysOldIsGoneAndOne179DaysOldIsListed`, `MSG_SEND_019_AC1_AFinalInAppDeliveryThatKeptItsPartsLosesThem31DaysPastItsFinalStatus`, `MSG_OPS_008_AC4_TheRetentionJobProtectsAnInAppDeliverysPartsAndAnEntryAgainUnderTheWriteVersionAndRetiresTheOldOne` |
| `02` MSG-INAPP-005 criterion 2; `01` MSG-SEND-020: erasure deletes the entries and clears the deliveries' parts; the export holds each entry in every language with its target and target values (`5d8afba`) | `MSG_INAPP_005_AC2_AfterErasureThePersonsListIsEmptyAndTheirEarlierExportHeldEachEntry`, and `SubjectTests`, unchanged |
| `05` MSG-API-008, MSG-API-001 and MSG-API-002; `04` MSG-LIB-004 and MSG-LIB-005; `07` MSG-ENG-006: `MapHermesInApp` and its four routes, the list read in the first declared language of `Accept-Language`, the two `POST` routes reading an empty object as `application/json` alone, the identity seam required where the group is mapped; the endpoint contract test reading the section's shape and its "as for" route (`2a734ec`) | `MSG_API_008_AC1_ASignedInPersonsGetWithAcceptLanguageArThenEnAnswersTheirEntriesNewestFirstInArabicAndNoneOfAnotherPersons`, `MSG_API_008_AC2_MarkingAnotherPersonsEntryReadAnswers404AndChangesNothingAndTheirOwn204AndTheUnreadCountThenOneFewer`, `MSG_API_008_AC3_APostOfInappReadSentAsAFormIsAnswered415AndMarksNothing`, `MSG_API_008_ASizeOutsideItsRuleOrACursorThatDoesNotReadAnswers400`, `MSG_API_008_ThePagesCursorReadsTheNextPage`, `MSG_API_002_AC1_AnInAppRouteWithNoSignedInPersonAnswers401` (four routes), `Language_AcceptLanguage_AnswersTheFirstDeclaredMatchByWeight`, `MSG_LIB_004_AC1_TheInAppGroupMappedAloneWorks`, `MSG_LIB_005_AC1_RecipientIdentityMissingWhereTheInAppGroupIsMappedStopsTheStartNamingIt`, `MSG_ENG_006_AC1_TheMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05`, the other `EndpointContractTests` |
| `06` MSG-OPS-007: a 1.2.0 section of `RELEASE-NOTES.md` above 1.1.0's (`0e6a7e5`) | Checked in review; see below |
| The test-name gate's list: MSG-INAPP-001 to MSG-INAPP-005 and MSG-API-008 (`cd8242c`) | `gate-test-names`: 285 criteria |
| `06` MSG-OPS-007's in-app list duty, under `docs/` (`0695234`) | None; the documents themselves |
| The release commit `chore: release 1.2.0` (`81c642c`), as `07` MSG-ENG-011's release commit value gives it: each `PublicAPI.Shipped.txt` holds its entries and those of its `PublicAPI.Unshipped.txt` (`Hermes.Core` 90 added, 1,064 in all; `Hermes` 2 added, 28 in all; `Hermes.Development` 162, `Hermes.Testing` 33, `Hermes.Channels` 2 and `Hermes.Cli` none, each unchanged; no entry marked `*REMOVED*`) under `#nullable enable` in ordinal order; each `PublicAPI.Unshipped.txt` holds `#nullable enable` alone; `build/gates/release-baseline.json` is the set the release gate reads at that commit, the in-app list's routes, bodies, codes and keys among it; `CHANGELOG.md`'s Unreleased entries are under `## [1.2.0] - 2026-10-31`, the commit's date in UTC, below an empty `## [Unreleased]`; `RELEASE-NOTES.md` is as `0e6a7e5` wrote it | `gate-commit-messages` and `gate-release-compatibility` on it; see below |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The tag `v1.2.0` | 10, after the merge | `07` MSG-ENG-011: the tag goes on the release commit once the pull request's merge has brought it into `main`, on the lead's message |
| `07` MSG-ENG-011 criterion 4 for 1.2.0 | 10, the tag's run | The release tag's run |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds or changes: no identifier, text, default or fixture names a host product, company or application. |
| `00` MSG-OVR-003, the list added as additions alone | `gate-release-compatibility -Base origin/main` passes: every difference since `v1.1.0` is an addition. |
| `06` MSG-OPS-002, a process of release 1.1.0 beside the new schema | The rules 1.1.0 already carries and tests: `MSG_SEND_007_AC5_ARowOfAChannelOrReasonThisReleaseDoesNotKnowAndARowOfAKindNeitherDeclaredNorRetiredAreNeverClaimed`, `MSG_SEND_007_AC5_ARowOfAChannelOrReasonThisReleaseDoesNotKnowIsDeletedOnlyOnceTheRecordsRetentionHasPassedSinceTheLaterOfItsRecordingAndDueInstant`, `MSG_SEND_020_AC5_AnUnknownMigrationAnswersReleaseNewerToAnErasureAndAnExportEvenWhereAKeyVersionIsNotHeld` and `MSG_OPS_008_AC7_WithAMigrationThisReleaseDoesNotKnowTheRetentionJobRetiresNoVersionAndProtectsNoValueAgain` (a migration a release does not know answers erasure and export `messaging.release.newer` and retires nothing). The migration adds a table and a nullable column and widens the checks, so every statement of 1.1.0 still runs on the new schema. |
| `05` MSG-API-002 criterion 2 for the in-app group | `MSG_API_008_AC1` (none of another person's entries listed) and `MSG_API_008_AC2` (another person's entry 404, nothing changed). |
| `06` MSG-OPS-007 criterion 1 for 1.2.0 | We read the 1.2.0 section of `RELEASE-NOTES.md` against each value of MSG-OPS-007: the packages, the channel, status, codes, keys, routes and bodies added; the migration, which drops and rewrites nothing; and every deployment duty. The 1.1.0 and 1.0.0 sections are unchanged. |
| `10` MSG-THR-001, the in-app list's four rows | The rows name MSG-API-002, MSG-API-008, MSG-INAPP-003, MSG-API-001, MSG-LIB-004 and MSG-SEND-018, each with tests carrying it; `MSG_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement` passes. |
| `02` MSG-INAPP-001, an entry written in Development | The in-app pass calls no provider, so the development inbox, which replaces providers, captures nothing of it; `InAppDispatchTests` run with the in-memory providers and write the entry. |
| `07` MSG-ENG-011 criteria 2 and 3 and `04` MSG-LIB-006 on the release commit | `gate-commit-messages` passes on the release commit, which it checks as a release commit; `gate-release-compatibility -Base origin/main` on it reads the set at that commit, finds the baseline there equal to it, and finds that the change since `v1.1.0` needs a minor release, every difference an addition. The baseline was written from the release gate's own read at that tree, its line endings LF. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each item's tests were written with it and run red before the code that passes them. |
| `07` MSG-ENG-009, the packages added | None. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `07` MSG-ENG-011 criterion 4: the release tag's run, which creates the tag's release holding every package at the tag's version only once its gates have passed. The tag `v1.2.0` goes on the release commit after the merge.

## Analyser suppressions

None. The migration's generated designer file carries the tool's `#pragma warning disable 612, 618`, as every earlier migration's does. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The private schema.** An in-app delivery keeps its rendered parts and target values in one `jsonb` column, `deliveries.parts`, an object from field to envelope (`title.en`, `body.ar`, `target`), until its dispatch clears it; an entry keeps its values the same way in `entries.parts`. Each value is bound to `<table>:<row>:<field>` (`08` section 6).
2. **The target values' text.** They are protected as one JSON object, each value an object of its one case (`text`, `whole`, `decimal`, `flag` or `instant`), a decimal and an instant as invariant text, so each reads back as it was sent.
3. **Protector calls outside the write.** The dispatcher protects an entry's values before the transaction that writes it, bounded by `Hermes:Dispatch:Timeout` and extending the claim; a value that cannot be protected yet holds the delivery as any key wait does.
4. **Lock order.** The dispatcher, the list and the marks take the person's locks shared before any row lock, the order erasure takes them; marking read locks the deliveries' rows before the entries', the order a delivery's deletion takes them.
5. **The entries' foreign key.** `entries.delivery_id` is set to null when the retention job deletes a delivery's record, so the record and the entry keep their own retentions.
6. **The cursor.** 24 bytes, the instant written in microseconds and the entry's identifier, written base64url without padding; one of another length, not canonical, or past the instants a `DateTimeOffset` holds does not read.
7. **A page and the entries left out.** A page reads `size` rows; an entry that is erased or rejected is left out of it, so a page may hold fewer, and its cursor still follows the last row read.
8. **The read language.** The asked tag is matched to the languages the entry holds, exactly and then by its primary subtag; failing that the default language, and failing that the send's.
9. **`content-rejected` for entries.** Raised with the scope and `details.table` `entries`, as for the other tables.
10. **A protector call on a read that throws or does not answer.** It answers `messaging.key.unavailable`, as the export's does, which the route answers 503 with `Retry-After`.
11. **`Accept-Language`.** Tags are taken by weight, ties in their order; a tag of weight 0 and `*` match nothing.
12. **The contract reader.** `05`'s in-app section says "Every route needs a signed-in person", gives a 503 "as for `GET /inapp`" and names the shape `<entry>` in its own table of shapes; the test's reader of `05` now reads each of these, and three of its assertions pin them.
13. **The release commit's date.** The commit's author and committer dates are one instant, `2026-10-31T03:16:27Z`, of the date its changelog heading gives, so the heading and the date the commit gate reads agree.

## Open questions

1. **The message of the M-026 documents commit.** `be2fbaa`'s body was three lines of prose, which `gate-commit-messages` refuses, and the commit was not yet pushed; rewording it would have made a new commit of `docs/spec/`, `docs/guide/` and `docs/decision-log.md`, which is the lead's. Answered by the lead's reword of its message alone, its contents unchanged: `7ab039e`, its body three dash fragments, our commits replayed onto it with their trees unchanged. It parked nothing once answered.
2. **The in-app list's deployment duties.** `08` gives `Hermes:InApp:AlwaysOn` as set alike in every application, and `05` and `10` give the frontend's showing of an entry's `title` and `body` as text, never as markup; `06` MSG-OPS-007 listed neither. Answered by `0695234`, which adds the duty "In-app list" to MSG-OPS-007 and M-026 to its source; the 1.2.0 section of `RELEASE-NOTES.md` already states it as written there. It parked nothing once answered.

## Commit and checks

The report names the release commit, `81c642c3c8c7877cd14a2ef8f6ce15300dc61593`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 1,971 (`Hermes.Tests` 1,003, `Hermes.Repository.Tests` 403, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 43, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19) |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (16 commits after `main`, the release commit checked as one), `gate-release-compatibility` (the baseline at the head is the set read there; since `v1.1.0` the change needs a minor release, which 1.2.0 is), `gate-sample-host` and `gate-test-names` (285 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 185 commits, no leaks |
| The pipeline's `gates` run on `2003fc7`, before the release commit | Passed: run 41162594646 |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner was fetched again before the scan.

The working folder `tmp/` holds files of earlier runs that this phase did not empty.
