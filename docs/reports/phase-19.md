# Phase 19: attachments

## Items built

| Item | Tests |
|---|---|
| The fix of the development inbox's log check (`c9df589`): a value the check refuses holds a character no folder GUID has, so the start line can no longer collide with it | `Used_EachValue_HoldsACharacterAFolderGuidNeverHas` |
| `06` MSG-OPS-008 (`c58e2e1`): `KeyTableTimer`'s stop ends a tick in flight before it returns, even with the host's time over, so the host never disposes what the tick uses | `StopAsync_MidTickWithTheHostsTimeOver_EndsTheTickBeforeItReturns`, failing before the fix |
| `09` MSG-CONV-007, MSG-CONV-009; `04` MSG-LIB-002, MSG-LIB-005; `06` MSG-OPS-004, MSG-OPS-006, MSG-OPS-008; `08` sections 2, 5 and 8 (`32949c7`): `IAttachmentStore`, `IAttachmentScanner`, `AttachmentId`, `AttachmentRecord`, `AttachmentDownload`, `AttachmentStatus` and `ScanResult` in `Hermes.Core`; the migration `Attachments`, the table `attachments` with the columns of `08` section 8, its checks, its indexes and its two foreign keys set to null when their rows go; the job `attachment-scans` and the alert `attachment-scan` admitted by the job and alert checks; the attachments' keyed tables, the name a value of the owner's, the uploader and the visitor's hash; a keyed table may leave its re-protection to a step of its own, `IProtectAgain`, which the retention job runs after the keyed tables; no `contentKeys` version an attachment's row records is retired | `MSG_CONV_009_AC4_AKeyVersionAnAttachmentUsesIsNeverRetiredAndTheGenericStepLeavesItsRow`, `MSG_LIB_006_AC1_EveryPublicInterfaceOfTheContractsIsOneAHostImplementsOrOneTheLibraryDoes`, the public type table's tests; `gate-migrations` (16 migrations) |
| `09` MSG-CONV-007, MSG-CONV-009; `05` MSG-API-009, MSG-API-010; `04` MSG-LIB-005; `08` sections 1 and 4 (`296cdc2`): the keys `Conversations:Attachments:Enabled`, `Public`, `MaxBytes` and `Types`; both seams required where attachments are enabled; the upload routes of both groups, a single-file multipart body streamed to the store, its name, type and first bytes checked, over `MaxBytes` answering 413; one stored object of 64 KiB chunks, each protected alone under its context `attachments:<id>:chunk:<n>` and framed by its length; `WriteAsync`, and the public group's write, naming up to 10 of the writer's attachments; a message's attachments in its bodies; the download routes, streamed after the access check with `Content-Disposition: attachment` and `nosniff`, a visitor never reaching a note's; the job's scans with backoff, the alert at the fifth failure, cleared at a later `Clean` or `Infected`; the removal of the unnamed after 24 hours, of those whose message, conversation or name went, bytes first, a failed deletion tried again; the re-protection step, writing a new object then leaving the old one to the job; the codes `messaging.attachment.type`, `toolarge`, `pending`, `refused`, `notfound` and `invalid` | `MSG_CONV_007_AC1_NothingOpensBeforeACleanScan`, `MSG_CONV_007_AC1_AnInfectedFileIsDeletedFromTheStoreAndRefusedItsRowKept`, `MSG_CONV_007_AC1_AFifthFailedScanMarksItFailedAndRaisesTheAlertWhichALaterCleanScanClears`, `MSG_CONV_007_AC2_ARefusedTypeOrBytesThatDenyTheirTypeAreRefusedAndAFileOverMaxBytesAnswers413`, `MSG_CONV_007_AFileNameOutsideItsRuleOrABodyThatIsNotOneFileIsInvalid`, `MSG_CONV_007_AttachmentsDisabledOrThePublicGroupsAnswerAsAConversationUnknown`, `MSG_CONV_007_AC3_DownloadsStreamAfterTheAccessCheckWithNosniffAndAsAnAttachment`, `MSG_CONV_007_AC3_AVisitorUploadsNamesAndOpensAttachmentsButNeverOneInANote`, `MSG_CONV_007_AC4_AnUnnamedUploadIsGoneAfter24Hours`, `MSG_CONV_007_AMessageNamesUpToTenOfItsWritersOwnAttachmentsEachOnce`, `MSG_CONV_009_AC3_TheStoreHoldsOnlyProtectedChunksAndAChunkMovedToAnotherAttachmentDoesNotOpen`, `MSG_CONV_009_AC4_TheMessagesGoingAndErasureDeleteTheStoredBytesAStoreAwayTriedAgain`, `MSG_CONV_009_ReProtectionWritesANewObjectUnderTheWriteVersionThenTheOldOneIsDeleted`, `MSG_LIB_005_AC1_AttachmentsEnabledWithoutTheirStoreOrScannerStopTheStartNamingIt`, `AttachmentsOff_WithoutTheirSeams_Starts`, `AttachmentsKeyOutsideItsRule_StopsTheStartNamingIt`, the tests of `AttachmentConfigurationTests`, and the contract test's tests |
| `09` MSG-CONV-007, MSG-CONV-010, MSG-CONV-011; `05` MSG-API-010; `08` section 1 (`10c47bb`): the public group's upload takes the public message write's checks and answers before any byte is stored: a blocked caller answers 403 `messaging.support.blocked`, a visitor before the current notice's acknowledgement 409 `messaging.support.noticerequired`, and each upload counts as one message toward the caller's messages limit, 429 `messaging.support.limited` with `Retry-After` past it | `MSG_CONV_010_AC1_NoPublicUploadIsTakenBeforeTheCurrentNoticeIsAcknowledged`, `MSG_CONV_011_AC2_ABlockedVisitorsUploadIsRefusedAsTheirMessageIs`, `MSG_CONV_011_AC1_EachPublicUploadCountsAsAMessageTowardTheVisitorsLimit`, and the contract test's tests |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-CONV-007 AC1 to AC4, MSG-CONV-009 AC3 and AC4 (`296cdc2`) | `gate-test-names`: 340 criteria |
| The merges: `phase-19-docs` at `195d8a3` (`5274327`), `d531d09` (`2563e32`) and `a13c3d2` (`29ef1b0`); `main` after phase 18's merge (`3096bbc`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `04` MSG-LIB-006, release 2's additions | The attachment types and seams are new; `IConversations`' new members and `WriteAsync`'s new parameter change an interface no release holds yet. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`. CA1308 refused `ToLowerInvariant` for media types; they are folded by a helper that lowers ASCII capitals alone.

## Resolved by rule

1. **Who reaches an attachment.** One no message names yet is reached by its uploader alone; once named, by anyone who may read the conversation, a visitor never one in a note. Staff upload where they may write (a support conversation's assignee while it is open) and download where they may read (a supervisor included).
2. **The public group's upload.** The caller's open conversation, under the public message write's checks (your ruling, now in `05`, `09` and `08`). They run in the write's order: the block and the notice before the file is read; then the file's name, type and first bytes; then the count, in its own transaction; then the store. A file refused for its name or type is not counted, as a text outside its rule is not. The source address has only a starts limit, which a message write does not count toward, so an upload counts toward none of the source's limits (see the open question).
3. **Types.** A declared type's parameters are dropped and its ASCII case folded before it is compared with `Types`, which is read the same way; a type named twice in `Types` is outside its rule. An empty file of a type the library knows fails its first bytes; one of another type is taken with no chunk.
4. **A write's `attachments`.** An identifier that does not read names one unknown (`message.invalid`, `attachments`); a `null` member is malformed; one named twice in a write answers `message.invalid`, `attachments`.
5. **A message's attachments' order.** The first uploaded first, since no column holds the order they were named in.
6. **A value that does not open.** A name or chunk that does not open under its binding answers the download `attachment.notfound`, leaves the attachment out of a message's list, and raises `content-rejected` with the table `attachments`; a key not held answers 503 as a text does. A chunk that fails after the answer began ends the response.
7. **The recorded version.** `content_key_version` is the lowest version among the name and the chunks, so no version they use is retired before re-protection.
8. **Scans.** A content the job cannot read, or a scanner that throws, counts as `Failed`. The tries after the first to fourth failures wait 1, 2, 4 and 8 minutes. The alert has no scope and names the attachment in `attachment`.
9. **Bytes no row names.** An upload refused or failed after its bytes were stored deletes them at once; where the store fails, a row without a conversation (`failed`, `application/octet-stream`, no name) keeps the key for the job. Re-protection leaves the old object to the job the same way, or the new one where the row changed meanwhile.
10. **The 24 hours.** The job first sets the conversation of each attachment unnamed past 24 hours to none, so no write can name it while its bytes go.
11. **A second file.** A body whose second part follows the stored first forgets the first, which the job then deletes, and answers `attachment.invalid`, `file`.
12. **The body's bound.** The upload raises the server's request bound to `MaxBytes` plus 64 KiB of multipart framing; a body past it answers 413 `attachment.toolarge`.
13. **Re-protection.** Only attachments the library protected are rewritten; a host protector's are left. It runs where a store is registered; the job runs where `Hermes:Jobs:Enabled` and attachments are both on.
14. **The download's headers.** `Content-Length` is the recorded size, and the file name is given as `filename` and, for any other character, `filename*`.

## Open questions

1. **The source's limit for an upload.** `09` MSG-CONV-011 now says a public upload counts "toward the visitor's or customer's limits and the source's". The source's only limit is `SourceStarts`, conversations started per hour, and the public message write counts nothing toward it. So an upload counts toward the caller's `VisitorMessages` alone. Should an upload also count toward `SourceStarts`, or toward a new per-source messages limit? Only that count waits on the answer.

## Commit and checks

The code is `29ef1b0`'s. Every check below ran on this branch, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every test of every project, run here | Passed: 2,300 (`Hermes.Tests` 1,044, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Conversations.Tests` 207, `Hermes.Development.Tests` 114, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-commit-messages` (23 commits after `main`, the merges among them), `gate-release-compatibility`, `gate-migrations` (16 migrations), `gate-sample-host` and `gate-test-names` (340 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 313 commits, no leaks |

The engine's commit `32949c7` was built and its contract tests run on its own tree before it was committed.

The first gate run on `29ef1b0` failed one test outside this phase: `Holding_ASinkThatDoesNotAnswer_IsLeftOnceTheDispatchTimeoutPassesAndTheRaiseStaysRecorded` of `Hermes.Tests` timed out while every project ran at once. Run alone three times it passed, and the second run of every gate, above, passed.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
