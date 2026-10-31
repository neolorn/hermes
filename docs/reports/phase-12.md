# Phase 12: the notice's key

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-005 (`2885653`): `DeliveryNotice.IdempotencyKey`, `string?`, declared after `At` as the last member, an optional `init` property as `Person` and `Reason` are (M-029); the notice's `ToString` prints it no more than it prints the person | `MSG_LIB_002_DeliveryNoticePrintsNeitherItsPersonNorItsIdempotencyKey`; `gate-public-surface` |
| First round (`f5a2952`, `dbaa147`): the key kept in clear with the send and cleared at an erasure, as M-028 read; replaced in the second round by M-029 and M-030 | Replaced, see below |
| `01` MSG-SEND-017, MSG-SEND-018, MSG-SEND-019, MSG-SEND-020 and `06` MSG-OPS-002, MSG-OPS-008 (`4726a8f`): the migration `SendIdempotencyKey`, changed in place, now adds a nullable `bytea` column `idempotency_key` to `messaging.deliveries`, and the sends row keeps no key; `SendAsync` protects the key once per delivery for the delivery's person, by the protector MSG-SEND-018 names, bound to `deliveries:<row id>:idempotencykey`, its `contentKeys` version in the delivery's column; the retention job protects it again with the delivery's other values and deletes it with the delivery's content; an erasure deletes it with the person's other values; the `observer` job opens it under the notice's claim, within `Hermes:Dispatch:Timeout`: opened, the notice carries it; `Erased`, told without it; `Rejected` or in no format, told without it and `content-rejected` raised; held, the claim released and the notice left for a later run | `MSG_SEND_017_AC4_ANoticeCarriesItsSendsKeyAfterTheKeysRowHasLapsedAndNoneForASendWithoutOne`, `MSG_SEND_017_AC4_TheNoticeOfAReportsMoveToDeliveredCarriesTheSendsKeyAfterTheKeysRowHasLapsed`, `MSG_SEND_017_AC4_ANoticeToldAfterItsDeliverysContentHasGoneCarriesNoKey`, `MSG_SEND_017_AC5_EachDeliveryKeepsItsSendsKeyProtectedAndItOpensInItsOwnRowAlone`, `MSG_SEND_017_AC6_ANoticeWhoseKeyAnswersErasedIsToldWithoutIt`, `MSG_SEND_017_AC6_ANoticeWhoseKeyAnswersRejectedIsToldWithoutItAndRaisesContentRejected`, `MSG_SEND_017_AC6_ANoticeWhoseKeyIsHeldIsNotToldAndIsToldWithItsKeyAtARunAfterTheKeyOpens`, `MSG_SEND_018_AC1_NoDestinationRenderedContentOrKeptIdempotencyKeyIsStoredInPlainText`, `MSG_SEND_019_AC1_ContentGoes31DaysPastTheFinalStatusTheRecord366DaysPastItAndAWaitingDeliveryKeepsEverything` (the key among the columns that go and stay), `MSG_SEND_020_AC1_AfterAnErasureNothingNamesThePersonOrHoldsTheirKeyNoKeptPlaceLinksTheirRecordsAndTheObserverIsToldOfTheCancellations` (the key's column among those an erasure empties), `MSG_OPS_008_AC4_TheRetentionJobProtectsADeliveryItsKeptKeyIncludedAndASubscriptionAgainUnderTheWriteVersionAndRetiresTheOldOne`, `MSG_OPS_002_ADeliveryRecordedWithoutTheKeysColumnAsAProcessOf1Rc1RecordsItIsDispatchedAndItsNoticeCarriesNoKey`; `MigrationsGateTests` (10 migrations) |
| `01` MSG-SEND-020's export and `04` the `SubjectExport` row (`cad0152`, its fix in `5440ac8`): `DeliveryExport.IdempotencyKey`, `string?`, declared last, never printed; the export opens the key with the delivery's other values whenever the row holds it, so a delivery is marked past content retention only where it holds none of its values, the key among them, and a key that does not open under its binding marks the delivery rejected and one that cannot open yet answers `messaging.key.unavailable`; none where the delivery is marked past content retention, erased or rejected | `MSG_SEND_020_AC2_TheExportHoldsThePersonsPreferencesIdempotencyRowsAndDeliveriesWithTheirDestinationsContentAndKeptKeys`, the export's tests of a delivery past content retention, erased and rejected (each now keyed and carrying no key, the one past content retention emptying the key's column as the retention job does), `MSG_SEND_020_AC2_AnInAppDeliveryDispatchedIsExportedWithTheKeyItKeepsAndNoContentNotMarkedPastContentRetention`, `MSG_SEND_020_AC2_AnInAppDeliveryDispatchedIsExportedMarkedPastContentRetentionWithNoKeyOnceRetentionHasTakenIt`, `MSG_LIB_002_DeliveryExportPrintsNeitherItsDestinationNorItsContentNorItsIdempotencyKey`; `gate-public-surface` |
| `CHANGELOG.md` under `## [Unreleased]`: the two members, the migration and the notice's answers under `### Added`, the erasure under `### Changed`, each in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-SEND-017 AC4 (`545bb12`), AC5 and AC6 (`2714847`) | `gate-test-names`: 289 criteria |
| `06` MSG-OPS-007: `RELEASE-NOTES.md`'s section `## 1.0.0-rc.2` (`69aa598`, brought up to date in `6d1689d`): the changes since 1.0.0-rc.1, both new members and that neither changes a constructor, the key kept by each delivery, protected and gone with its content, the notice's answers for a key that does not open, no backfill; the migration, which drops and rewrites nothing, with what a 1.0.0-rc.1 process beside it does; the sixteen duties, none new | `gate-commit-messages` |
| `08` and `10` | Their rows as M-029 and M-030 changed them (`08` section 8's field `idempotencykey`, `10`'s leaked-copy row) are what the code above follows; neither asks for more |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The release commit `chore: release 1.0.0-rc.2` | 12, after the review | `07` MSG-ENG-011: it comes in this pull request once the review has passed, on the lead's message |
| The tag `v1.0.0-rc.2`, and `07` MSG-ENG-011 criterion 4 for it | 12, after the merge | The tag goes on the release commit once the merge has brought it into `main`, and its run checks the release |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `07` MSG-ENG-008, a test with or before the code it proves | First round: with the sending code of `2885653`, both MSG-SEND-017 AC4 tests failed; without the erasure's change, the MSG-SEND-020 AC1 test failed. Second round: with the sending code of `3185991` (the key kept in clear with the send), ten of this round's tests failed, the MSG-SEND-017 AC4, AC5 and AC6 tests, MSG-SEND-018 AC1, MSG-SEND-019 AC1, MSG-SEND-020 AC1, MSG-OPS-008 AC4 and the 1.0.0-rc.1 test; the export's tests and the unit test did not compile before `DeliveryExport.IdempotencyKey`. Third round: with the export of `65260c9`, the test of an in-app delivery dispatched with a key failed, exported marked past content retention with no key; the test of the same delivery after content retention passed, as it should. |
| `07` MSG-ENG-009, the packages added | None. |
| `06` MSG-OPS-002, a 1.0.0-rc.1 process beside the new schema | 1.0.0-rc.1's model writes `deliveries` with the columns it knows and reads named columns alone, so the nullable column changes none of its statements and the rows it records keep no key; the test above empties the column of a keyed delivery, as such a row is, and the current release dispatches it and tells its notice without a key. Its erasure and export answer `messaging.release.newer` against a database holding a migration it does not know, as `SubjectTests` already proves, and its retention job protects nothing again (`06` MSG-OPS-008), so it never rewrites a delivery's values beside the key. |
| `06` MSG-OPS-007 criterion 1 for 1.0.0-rc.2 | We read the section against each value of MSG-OPS-007: the six packages at this version; every change since 1.0.0-rc.1, taken from this phase's commits and `PublicAPI.Unshipped.txt`, which holds four entries, the two members' getters and `init`s; the migration, which drops and rewrites nothing; and the sixteen duties of 1.0.0-rc.1's section, each once, none new. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The member's form.** `IdempotencyKey` is an optional `init` property declared after `At`, as M-029 confirms; `DeliveryExport.IdempotencyKey` likewise, after `Rejected` (`04` the `SubjectExport` row: "declared last").
2. **A lapsed row in the tests.** The tests delete the idempotency rows, as the retention job does with a row past its lapse (`01` MSG-SEND-009), so nothing could still be read from them.
3. **The migration changed in place.** `SendIdempotencyKey` was generated again under the same name, with a new instant, `20261031160450`, and its first form deleted, since neither `07` nor the migrations gate binds a migration no release or `main` holds (the commit gate's released files are the `PublicAPI.Shipped.txt` files and the release baseline alone) and M-030 accepts it; 1.0.0-rc.2 adds one migration.
4. **The column.** Nullable `bytea`, as each of a delivery's protected values is, with no index and no default; its name is the field's in the schema's snake case, `idempotency_key`, while the context names the field `idempotencykey` (`01` MSG-SEND-018; `08` section 8).
5. **The key's `contentKeys` version.** The delivery's column records it with its other values' (`01` MSG-SEND-018: "A row holding values of the library's protector records their `contentKeys` version"), and the retention job's update protecting a delivery again sets the key with the others, conditional on it being the one read (`06` MSG-OPS-008).
6. **Where the notice opens the key.** Under the notice's claim, after the claim is taken, so only the claimant raises `content-rejected`, and a held key releases the claim as a notice the observer did not take does (`01` MSG-SEND-017: "is told at a later run as one the observer did not take"). A call that throws or does not answer within `Hermes:Dispatch:Timeout` answers held, as at check 1 (`01` MSG-SEND-018).
7. **The export's key and content retention.** The key is one of a delivery's values, so the export opens it with the others whenever the row holds it, and marks a delivery past content retention only where the row holds none of its values, the key among them (`01` MSG-SEND-020: each delivery "with its destination, its content and the idempotency key it keeps (M-030) while it holds them"; M-029: "What an erasure deletes, an export shows"). An in-app delivery whose parts moved into its entry at dispatch, and which keeps a key, is exported with the key and no content, not marked past content retention, its content exported under its entry; once the retention job has emptied its key with its content, it is exported marked past content retention with no key. A delivery whose content a 1.0.0-rc.1 retention job emptied but whose key it did not is exported with the key until this release's retention job takes it. The round-2 form, which opened the key only where the delivery held content, hid the key of a dispatched in-app delivery, and was corrected in `5440ac8` on the lead's review.
8. **A 1.0.0-rc.1 retention job during a deployment.** It empties a delivery's content and its `contentKeys` column but not the key's column, which it does not know; this release's retention job takes such a delivery at its next run, its content deletion selecting a delivery that holds a key past content retention, before it judges any version retired, in the same run.

## Open questions

The first round's four questions were answered by M-029 and the two of the second round by M-030; none is open, and nothing is parked.

1. **"Positional" and "constructor".** Answered by M-029: an optional member declared last; the constructor is unchanged.
2. **The sends recorded before the migration.** Answered by M-029: no backfill; the release notes say so.
3. **The export.** Answered by M-029 and M-030: `DeliveryExport.IdempotencyKey`, built above.
4. **`10` on a leaked database copy.** Answered by M-029 and M-030: the key is a protected value of each delivery, and `10` names it.
5. **The context the key is bound to.** Answered by M-030: `deliveries:<row id>:idempotencykey`, each delivery keeping the key.
6. **A notice whose key does not open.** Answered by M-030: built above, and the export keeps its rules for a delivery's values.

## Commit and checks

The report names `5440ac8`, on which every check below ran, the working tree holding nothing but this report's own edit:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,034 (`Hermes.Tests` 1,013, `Hermes.Repository.Tests` 455, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 44, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (15 commits after `main`), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations`, `gate-sample-host` and `gate-test-names` (289 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 212 commits, no leaks |
| The pipeline's `gates` run on the first report commit, `073d3d0` | Passed: run 41215735744 |
| The pipeline's `gates` run on the second report commit, `65260c9` | Passed: run 41219515578 |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.

The working folder `tmp/` was emptied at the start of the run, save the secret scanner's, the tests' and the tools' folders.
