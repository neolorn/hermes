# Phase 21: the contract and release candidate 1.0.0-rc.3

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-002, MSG-LIB-008 (`470b3d8`, `53347b7`): `InMemoryAttachmentStore`, which keeps each object's bytes under its key until it is deleted, and `FixedAttachmentScanner(ScanResult answer = ScanResult.Clean)`, which reads each file to its end and answers its result, in `Hermes.Testing`; `AddHermesTesting` registers both as singletons, by their own types and by their seams, and a host's own store or scanner registered after it is what the seam answers | `MSG_LIB_008_InMemoryAttachmentStore_KeepsWhatItIsGivenUnderItsKeyUntilDeleted`, `MSG_LIB_008_FixedAttachmentScanner_ReadsEachFileAndAnswersItsResult`, `AddHermesTesting_TheAttachmentsDoubles_AreOneSingletonByTheirTypeAndSeamAndALaterOneReplacesThem`, `AddHermesTesting_AHostsOwnStoreAndScannerRegisteredAfterIt_ReplaceTheDoublesTheSeamsAnswer`, the public type table's tests, the conversations' missing-seam theory |
| `07` MSG-ENG-006 criterion 2; `04` MSG-LIB-006 criterion 3 (`2c64615`): the contract test maps every group and hub, each `Hermes.Conversations` group under a prefix of its own, and finds exactly `05`'s routes, hub routes, hub methods and payload shapes; the release set gains the section `hubs`, each hub's route and each method with the side that sends it and its arguments, its payloads among the bodies, read by `gate-release-compatibility`, a line removed needing a major release and one added a minor | `MSG_ENG_006_AC2_TheContractTestFindsExactly05sRoutesAndHubsEveryGroupUnderItsPrefix`, `MSG_LIB_006_AC3_TheCurrentSetHoldsTheHubsRoutesMethodsAndPayloadShapes`, `MSG_LIB_006_AC3_AHubMethodRemovedNeedsAMajorReleaseAndOneAddedAMinor`, `gate-release-compatibility` |
| `07` MSG-ENG-001 criterion 4 (`e7aabd0`): the sample host maps every group of the library: the engine's, in-app and the staff conversations with their hub under `/notifications`, and the public conversations group with its hub under `/help`; a support topic `orders` of the shop, its kinds `support.reply` and `support.transcript` in the essential category `support` with their email templates, and the privacy notice's version; attachments on, the public group's too, with the in-memory store and the fixed scanner of `Hermes.Testing` | `MSG_ENG_001_AC4_TheSampleHostMapsEveryGroupOfTheLibraryWithAttachmentsOnAndTheTestingFakes`, the sample host's `SupportConversation_ACustomerLeavesTheOfflineFormWithAFileAndTheStaffsReplyGoesByEmail`, `Hubs_TheStaffsAndThePublicSidesAreMappedAndTheStaffsNeedsASignedInMember` and `StaffChat_TheStaffWriteInTheShopsConversationAndACustomerIs403`, `gate-sample-host` |
| `06` MSG-OPS-007 (`794669b`): `RELEASE-NOTES.md` section `1.0.0-rc.3`: every change since 1.0.0-rc.2, all additive, with `TemplateKey.Reply`'s warning first; the package `Hermes.Conversations`; the seams a host supplies; the keys and the two jobs; the six migrations, none of which drops or rewrites data; and the deployment's duties, `Conversations` and `Template sources` new | Checked in review; see below |
| `CHANGELOG.md` under `## [Unreleased]`: each `feat` above in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-ENG-006 AC2, MSG-LIB-006 AC3 (`2c64615`) and MSG-ENG-001 AC4 (`e7aabd0`) | `gate-test-names`: 349 criteria |
| The merges: `phase-21-docs` at `c01adde` (`bede1e6`); `main` after phase 20's merge (`6536944`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later step

| Item or criterion | Step | Reason |
|---|---|---|
| The release commit `chore: release 1.0.0-rc.3`: PublicAPI moved to Shipped for every package, `CHANGELOG.md` section `1.0.0-rc.3`, and `release-baseline.json` rewritten to rc.3's set, hubs included | After the review, on the lead's message | M-039: it follows the review, as phase 12's did |
| `06` MSG-OPS-007 criterion 2, every gate passing on the release commit | The release commit | It is that commit's |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. The sample host is the neutral bakery it was. |
| `06` MSG-OPS-007 criterion 1, release 2's notes | We read the section against M-039's list: every `Added` and the two `Fixed` lines of `CHANGELOG.md` that touch 1.0.0-rc.2's surface are named; `Hermes.Core`'s, `Hermes.Testing`'s and `Hermes.Conversations`'s PublicAPI files since `v1.0.0-rc.2` hold additions only, and no `PublicAPI.Shipped.txt` lost a line; the keys and their defaults are `08` section 4's; the migrations are the six of `src/Hermes/Migrations` after `SendIdempotencyKey`; the deployment's list is `06`'s. |
| `04` MSG-LIB-006, release 2's additions | As above: no member of 1.0.0-rc.2's surface is removed or changed, each record's new member is an optional `init` property, and every seam is a new interface. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **`AddHermesTesting` registers the attachments' doubles always.** M-039's amendment says it registers them where attachments are enabled. `AddHermesTesting` runs on the service collection, before configuration is read, so it cannot tell. Registering always changes nothing where attachments are off, since the seams are read only where they are on, and a host's own store or scanner registered after it replaces its double, which a test shows. The lead accepted this reading.
2. **The sample's support conversation goes through the offline form.** The staff hub's assignment needs a live connection, and the sample's tests hold no hub client. So the test shows the start answering `messaging.support.offline` with nobody connected, at any hour. It then leaves the offline form with a file, names the file in a message, and reads it answering `messaging.attachment.pending` before its scan. The staff member takes the conversation by transfer and replies, and the reply goes by email as `support.reply`. The hubs' mapping is shown by their negotiation: the public hub's answers anyone, and the staff hub's a signed-in staff member only.
3. **The sample's topic.** `orders`, in `Africa/Cairo`, staffed by the shop's scope, open 08:00 to 20:00 every day but Friday. No test depends on the hour.
4. **The hubs' lines.** `HUB <route>` for each hub, and `HUB <route> server <method> <arguments>` or `HUB <route> client <method> <arguments>` for each method, the arguments by name, in order and separated by commas. The payloads' members are in `bodies`, as every body's are. The baseline of 1.0.0-rc.2 holds no `hubs` section, so before the release commit every hub line is an addition.
5. **The notes' "Fixed".** Only the fixes that touch 1.0.0-rc.2's behaviour are named: the key-table timer at a late stop, and `Hermes:Languages` set item by item through a test factory. The others fixed conversations before any release held them.

## Open questions

None.

## Commit and checks

The code is `53347b7`'s. Every check below ran on this branch, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every test of every project, run here | Passed: 2,319 (`Hermes.Tests` 1,047, `Hermes.Repository.Tests` 481, `Hermes.Channels.Tests` 362, `Hermes.Conversations.Tests` 217, `Hermes.Development.Tests` 114, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 23), in `gate-tests`; the sample host's 14 in `gate-sample-host` |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-release-compatibility`, `gate-migrations` (16 migrations), `gate-sample-host` and `gate-test-names` (349 criteria). The last commit's message was first written with a body line that was not a dash fragment, which `gate-commit-messages` refused; it was rewritten before any push, and the gate then passed on 18 commits after `main`, the merges among them |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 322 commits, no leaks |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
