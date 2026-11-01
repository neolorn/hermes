# Phase 20: retention, export and threats

## Items built

| Item | Tests |
|---|---|
| `01` MSG-SEND-020; `04` MSG-LIB-002; `06` MSG-OPS-004 (`fa2ca74`): `ConversationExport`, `ConversationMessageExport`, `AttachmentExport` and `SurveyExport` in `Hermes.Core`, none of which prints a text, a name or a topic; `SubjectExport.Conversations`, nullable and not `required`, declared last, none where conversations are not registered; the export reads a package's conversations in its own transaction, under the person's locks, and opens them after it commits; the retention job runs a package's retention steps after its own deletions and before it protects values again | `MSG_SEND_020_AC6_APersonsExportHoldsTheirConversationsAndNobodyElsesReferences`, the engine-only export holding no conversations (`InAppSubjectTests`), the public type table's tests |
| `09` MSG-CONV-010; `01` MSG-SEND-020; `06` MSG-OPS-004; `08` section 4; `10` MSG-THR-001 (`39b9a93`): `Conversations:Retention:Staff`, 30 days to 10 years, unset by default; the retention job's conversations step, a visitor's support conversation going `Retention:Visitor` after its close with every row of it and the visitor's acknowledgement with their last conversation, and staff-chat messages older than `Retention:Staff` going where it is set, at most 1,000 conversations a run for each, their attachments left to `attachment-scans`; `IConversations.EndScopeAsync`; a person's export of their conversations, as `09` gives it, a value that cannot open yet answering `messaging.key.unavailable`; the threat rows of M-038 each proven by a test carrying each requirement it names, and no log line holding a message's text or an attachment's bytes | `MSG_CONV_010_AC3_AVisitorsConversationIsGoneRetentionVisitorAfterCloseWithEveryRowAndItsAttachmentsBytes`, `MSG_CONV_010_AC4_WithRetentionStaffUnsetNothingOfAStaffChatGoes`, `MSG_CONV_010_AC4_WithRetentionStaffSetOlderMessagesGoWithTheirAttachmentsAndTheRestReadOn`, `MSG_CONV_010_AC5_EndScopeAsyncRemovesTheScopesConversationEntirely`, `MSG_SEND_020_AC6_APersonsExportHoldsTheirConversationsAndNobodyElsesReferences`, `MSG_SEND_020_AC6_AnExportWhoseMessageCannotOpenYetAnswersKeyUnavailable`, `MSG_THR_001_AC2_EachConversationThreatRowsControlHasAPassingTestCarryingEachRequirementItNames`, `MSG_THR_001_AC3_NoLogLineHoldsMessageTextOrAttachmentBytes`, `Read_AValueOutsideItsRule_IsItsKey` (`Retention:Staff`) |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-CONV-010 AC3 to AC5, MSG-SEND-020 AC6, MSG-THR-001 AC2 and AC3 (`39b9a93`) | `gate-test-names`: 346 criteria |
| The merges: `phase-20-docs` at `579a05c` (`19952d4`) and `a8ef0c5` (`447b74d`); `main` after phase 19's merge (`dd75323`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `06` MSG-OPS-007, the host frontend's duty for conversations | It is the deployment's, as `10` MSG-THR-001 criterion 1 says; the library answers plain text only, which the contract test's bodies show. |
| `10` section 1, the conversations' assets | The table is the chapter's; each control it names is one of MSG-THR-001's rows, proven above. |
| `04` MSG-LIB-006, release 2's additions | The export records are new; `SubjectExport.Conversations` is optional and declared last, and `IConversations.EndScopeAsync` changes an interface no release holds yet. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The 1,000 conversations a run.** Each rule takes at most 1,000: the visitors' conversations past their period, oldest close first, and the staff chats holding messages past theirs. The rest wait for the next run.
2. **Staff chats.** Direct and group conversations. A support conversation's messages are never a staff chat's, so `Retention:Staff` leaves them. A staff chat whose every message went keeps its row and its participants.
3. **A read position on a message gone.** It is set to none, which reads as the latest earlier message: every earlier message went with it, so every message left is after it.
4. **The acknowledgement.** It goes where no conversation of the same visitor hash is left.
5. **EndScopeAsync.** Only the scope's group conversation; a scope without one succeeds. It pushes nothing, since the conversation's members have just lost it.
6. **The export's conversations.** Support conversations as the customer, every message but notes, a deleted message without its text; staff chats, direct or group, where the person wrote, their own messages only, deleted ones included. They are ordered by their opening, `Topic` and `ClosedAt` only on support conversations. An attachment whose name does not open is left out, a survey comment that does not open answers none, and each table whose value did not open raises `content-rejected` once.
7. **The log test.** It captures every logger at `Trace`, the library's, ASP.NET Core's and Entity Framework Core's, over a customer's conversation, an upload whose scan fails and then passes, its download, the export, the close, the transcript, a refused file and a staff chat.
8. **Threat criterion 2.** The rows marked M-038 name `MSG-API-009`, `MSG-API-010`, `MSG-CONV-001`, `MSG-CONV-002`, `MSG-CONV-006`, `MSG-CONV-007`, `MSG-CONV-008`, `MSG-CONV-011` and the deployment's `MSG-OPS-007`; each of the library's has a test carrying it.

## Open questions

None.

## Commit and checks

The code is `39b9a93`'s. Every check below ran on this branch, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every test of every project, run here | Passed: 2,311 (`Hermes.Tests` 1,044, `Hermes.Repository.Tests` 480, `Hermes.Channels.Tests` 362, `Hermes.Conversations.Tests` 217, `Hermes.Development.Tests` 114, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-commit-messages` (18 commits after `main`, the merges among them), `gate-release-compatibility`, `gate-migrations` (16 migrations), `gate-sample-host` and `gate-test-names` (346 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 317 commits, no leaks |

The engine's commit `fa2ca74` was built, and its contract and export tests run, on its own tree before it was committed. This branch holds phase 19's commits until #20 merges; `main` is then merged into it.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
