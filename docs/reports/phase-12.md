# Phase 12: the notice's key

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-005 (`2885653`): `DeliveryNotice.IdempotencyKey`, `string?`, declared after `At` as the last member, an optional `init` property as `Person` and `Reason` are; the notice's `ToString` prints it no more than it prints the person; two entries in `Hermes.Core`'s `PublicAPI.Unshipped.txt` | `MSG_LIB_002_DeliveryNoticePrintsNeitherItsPersonNorItsIdempotencyKey`; `gate-public-surface` |
| `01` MSG-SEND-017 and `06` MSG-OPS-002 (`f5a2952`): the migration `SendIdempotencyKey`, a nullable `text` column `idempotency_key` of `messaging.sends`, made `internal sealed` and formatted; `SendAsync` records the key with the send, and the `observer` job reads it from the send, not from the idempotency row, so it lives as long as the send's record, which the retention job deletes once its last delivery is gone | `MSG_SEND_017_AC4_ANoticeCarriesItsSendsKeyAfterTheKeysRowHasLapsedAndNoneForASendWithoutOne`, `MSG_SEND_017_AC4_TheNoticeOfAReportsMoveToDeliveredCarriesTheSendsKeyAfterTheKeysRowHasLapsed`, `MSG_OPS_002_ASendRecordedAsAProcessOf1Rc1RecordsItIsDispatchedAndItsNoticeCarriesNoKey`; `MigrationsGateTests` (10 migrations) |
| `01` MSG-SEND-020 (`dbaa147`): an erasure clears the key of every send of the person's deliveries, in its transaction and before it clears their person reference, so the notices it keeps for the observer carry no key | `MSG_SEND_020_AC1_AfterAnErasureNothingNamesThePersonOrHoldsTheirKeyNoKeptPlaceLinksTheirRecordsAndTheObserverIsToldOfTheCancellations` (the key absent from every table, and no notice told carrying a person or a key) |
| `CHANGELOG.md` under `## [Unreleased]`: the member and the migration under `### Added`, the erasure under `### Changed`, each in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-SEND-017 AC4 (`545bb12`) | `gate-test-names`: 287 criteria |
| `06` MSG-OPS-007: `RELEASE-NOTES.md`'s section `## 1.0.0-rc.2` (`69aa598`), above 1.0.0-rc.1's: the changes since 1.0.0-rc.1, `DeliveryNotice`'s new member and what it means for its constructor among them; the migration, which drops and rewrites nothing, with what a 1.0.0-rc.1 process beside it does; the sixteen duties, none new | `gate-commit-messages` |
| `08` and `10` wherever they name what a notice or a send's record holds | None: neither names it (see below), so nothing in the code follows from them |

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
| `07` MSG-ENG-008, a test with or before the code it proves | With the sending code of `2885653` and this phase's tests, both MSG-SEND-017 AC4 tests failed and the 1.0.0-rc.1 test passed, as a test of what must keep working does; without the erasure's change, the MSG-SEND-020 AC1 test failed on the key found in `messaging.sends`. |
| `07` MSG-ENG-009, the packages added | None. |
| `08` and `10` on what a notice or a send's record holds | We searched both for the notice, the observer, the send's record, `sends` and the idempotency key: `08` names `Observer:Enabled`, `Observer:MaxCalls`, `Idempotency:Window`, the `observer-failing` alert, the code `messaging.send.idempotencyconflict` and the hash domain `idempotency`, none of which says what a notice or a send's record holds; `10`'s row on a leaked database copy names what holds keyed hashes, and neither the notice nor the send's record. See open question 4. |
| `06` MSG-OPS-002, a 1.0.0-rc.1 process beside the new schema | 1.0.0-rc.1's model writes `sends` with its three columns and reads named columns alone, so the nullable column changes none of its statements; the test above records a send's row as it does and the current release dispatches it and tells its notice without a key. Its erasure and export answer `messaging.release.newer` against a database holding a migration it does not know, as `SubjectTests` already proves, so it never erases beside the new column. |
| `06` MSG-OPS-007 criterion 1 for 1.0.0-rc.2 | We read the section against each value of MSG-OPS-007: the six packages at this version; every change since 1.0.0-rc.1, taken from this phase's commits and `PublicAPI.Unshipped.txt`, which holds two entries, the member's getter and `init`; the migration, which drops and rewrites nothing; and the sixteen duties of 1.0.0-rc.1's section, each once, none new. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The member's form.** `IdempotencyKey` is an optional `init` property declared after `At`. `04` MSG-LIB-005 lists `string? IdempotencyKey` last among `DeliveryNotice`'s members, and the type is a sealed record of properties with a constructor taking no argument, its other nullable members, `Person` and `Reason`, optional `init` properties; "last" is the member's place in that list and in the type's declaration. See open question 1.
2. **A lapsed row in the tests.** The tests delete the idempotency rows, as the retention job does with a row past its lapse (`01` MSG-SEND-009), so nothing could still be read from them.
3. **Which sends an erasure reaches.** The sends of the person's deliveries, found through `deliveries.person` before the erasure clears it, in the erasure's transaction; a send has one recipient, so no other person's key is touched. A send to an anonymous recipient has no person, so its key stays until the send's record goes (`01` MSG-SEND-020 erases by person). A send whose deliveries the retention job has already deleted is found by no person, and the same job deletes it once its last delivery is gone (`01` MSG-SEND-019).
4. **The migration's column.** Nullable `text`, as the idempotency row's key is, with no index, since nothing selects by it, and no default; its name is the property's in the schema's snake case.

## Open questions

1. **"Positional" and "constructor".** M-028's rejected alternatives and the plan's row speak of `IdempotencyKey` as `DeliveryNotice`'s "last positional member" and of "the change to `DeliveryNotice`'s constructor", but the type is not a positional record: its constructor takes no argument and has no deconstruction, and adding the member changes neither. We built the additive form (resolved by rule 1), and the release notes say the constructor is unchanged. Was a positional record, or a `required` member, meant? Either would break every host that creates a notice. Nothing is parked.
2. **The sends recorded before the migration.** Their notices carry no key. A send whose idempotency row is still live could have its key copied from the row, whose receipt names the send; neither `01` nor M-028 asks for it, so the migration copies nothing. Should it? Nothing is parked.
3. **The export.** M-028 says a key is exported and erased with its person. `ISubjects.ExportAsync` exports the person's live idempotency rows, as `01` MSG-SEND-020 says, and so not the key kept with a send after its row has lapsed, while the erasure now deletes it. Should the export carry the keys kept with the person's sends? Nothing is parked.
4. **`10` on a leaked database copy.** The key was held in clear for `Hermes:Idempotency:Window`, one day by default; it is now held with the send for `Hermes:Retention:Record`, 365 days by default, and `10`'s row on a leaked copy does not name it. Should `10` name it, as M-028 treats the key as the person's? Nothing is parked.

## Commit and checks

The report names `69aa598`, on which every check below ran, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,026 (`Hermes.Tests` 1,006, `Hermes.Repository.Tests` 455, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 43, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (6 commits after `main`), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations`, `gate-sample-host` and `gate-test-names` (287 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 203 commits, no leaks |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.

The working folder `tmp/` was emptied at the start of the run, save the secret scanner's, the tests' and the tools' folders.
