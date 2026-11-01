# Phase 14: the model and staff chats

## Items built

| Item | Tests |
|---|---|
| `06` MSG-OPS-001 (`e149ab2`), the lead's answer to phase 13's open question: the engine reads an empty value on a section with keys beneath it, as a web host's test factory gives the parents of the keys it sets, as no value, so `Hermes:Languages` set item by item through the factory no longer stops the start | `Check_EveryParentKeyEmptyAsAWebHostsTestFactoryGivesIt_PassesAndBinds`; the sample host's `Start_ALanguageSetThroughTheTestFactory_StartsThoughTheFactoryGivesItsListAnEmptyValue`, in `gate-sample-host`; both failed before the change |
| `04` MSG-LIB-002, MSG-LIB-005 (`af9a3e2`): `ConversationId`, `MessageId`, `ParticipantId`; `ConversationScope`; `ConversationPage`, `ConversationRecord`, `ParticipantRecord`, `MessagePage`, `MessageRecord` with `AuthorKind`, none printing a person reference, a scope or a text; the enumerations `ConversationType`, `ConversationStatus`, `MessageType`, `ScopeKind`, `AuthorKind` with their `08` spellings; `IConversations`, one member a route, answering `Result<T>`; `IConversationMembership` | `MSG_LIB_002_TheConversationRecordsPrintNoPersonReferenceTextOrScope`; `MSG_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive` and `MSG_LIB_002_AC2_PublicApiFilesListExactlyThePublicTypes`; `gate-public-surface` |
| `09` MSG-CONV-001, MSG-CONV-009; `06` MSG-OPS-002; `01` MSG-SEND-018, MSG-SEND-020; `08` sections 5 and 8 (`c138eea`): the tables `conversations`, `participants`, `messages` and `mentions` in the engine's context, folder `Conversations` and migration `Conversations`, each closed set checked by the database, one group conversation a scope and one participant row a person and conversation; the three tables holding a person reference or a protected value added to key retirement, re-protection and erasure by `AddHermes` itself, so an erasure reaches them wherever the engine runs; a message's text bound to `messages:<id>:text`, `content-rejected` scoped `messages` | `MSG_CONV_009_AC1_AMessageIsProtectedAgainUnderTheWriteVersionAndItsOldVersionRetiredOnlyThen`, `MSG_CONV_009_AC1_AKeyVersionAMessageUsesIsNeverRetired`; `gate-migrations` (11 migrations) |
| `09` MSG-CONV-001, MSG-CONV-006, MSG-CONV-009; `04` MSG-LIB-003, MSG-LIB-004, MSG-LIB-005; `05` MSG-API-009; `07` MSG-ENG-006; `08` section 1 (`7b32c58`): `IConversations` registered scoped by `AddHermesConversations`; the start stopping with `messaging.startup.declarationmissing`, `key` `conversationMembership`, without the seam; the staff chats, every call reaching the conversation first (a group's through `IsMemberAsync`, asked each call; a direct one's through the person's participant row), then reading the request's rules, in one transaction under the shared locks of each person it writes a row for; the text protected under its author's key before the transaction and opened after it; the twelve routes of `MapHermesStaffConversations` on `MessagingConversationsEndpointRouteBuilderExtensions`, their bodies in the package's own serializer context | `MSG_CONV_006_AC1_ANonMembersEveryReadAndWriteAnswersNotFound`, `MSG_CONV_006_AC1_AMemberTheSeamNoLongerAnswersForIsRefusedOnTheNextCall`, `MSG_CONV_006_AC1_TheGroupConversationIsCreatedOnceForItsScope`, `MSG_CONV_006_AC2_ANonParticipantAndOneWhoLeftAnswerNotFoundAtOnce`, `MSG_CONV_006_AC2_ParticipantsAreAddedUpTo16AndThe17thAnswersFull`, `MSG_CONV_006_AC2_StartingWithNoneOrMoreThan15OthersIsRefused`, `MSG_CONV_006_AC3_EditsAndDeletesWorkOnOnesOwnMessagesOnlyAndADeletedMessageReadsWithNoText`, `MSG_CONV_001_AC1_EachBoundIsRefusedAtItsEdge`, `MSG_CONV_001_AC1_AReplyNamesAMessageOfTheSameConversationDeletedOrNot`, `MSG_CONV_001_AC2_TheReadPositionMovesForwardOnlyAndTheUnreadCountsFollowIt`, `MSG_CONV_009_AC1_AMessagesProtectedTextCopiedToAnotherRowDoesNotOpen`, `MSG_CONV_009_AC2_ErasingAnAuthorClearsTheirMessagesAndRowsAndLeavesOtherMessagesUntouched` (the library's protector and a host's), `MSG_API_009_AC1_TheDirectConversationRoutesDoWhatTheirOperationsDo`, `MSG_API_009_AC1_TheGroupConversationRouteDoesWhatItsOperationDoes`, `MSG_LIB_005_AC1_ConversationsRegisteredWithoutTheMembershipSeamStopTheStartNamingIt`, `MSG_LIB_003_AC7_AddHermesConversationsRegistersIConversationsAsAScopedService`, `MapHermesStaffConversations_WithoutAddHermesConversations_ThrowsNamingIt`, and the other tests of `StaffChatTests` and `RegistrationTests`; `MSG_ENG_006_AC1_TheMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05` and `MSG_ENG_006_AC1_EveryBodyOfTheLibrarysSerializerContextsIsOneARouteReadsOrAnswers`, now mapping the staff group and reading the package's serializer context |
| `07` MSG-ENG-001 (`f75a711`): the sample host registers a membership seam, its staff the members of one organization, the shop, and maps the staff conversations group under `/notifications`, behind its staff policy | `StaffChat_TheStaffWriteInTheShopsConversationAndACustomerIs403`, in `gate-sample-host` |
| `04` MSG-LIB-006; `06` MSG-OPS-002 (`4d09412`): the release set counts `IConversationMembership` among the interfaces a host implements, whose members the gate guards, and `IConversations` among the library's; the migrations gate's test expects the eleventh migration | `MSG_LIB_006_AC1_EveryPublicInterfaceOfTheContractsIsOneAHostImplementsOrOneTheLibraryDoes`, `GateMigrations_ThisRepository_AppliesTheMigrationsThenNothing` |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit, the fix under `### Fixed` | `gate-commit-messages` |
| The test-name gate's list: MSG-CONV-009 AC1 (`c138eea`); MSG-CONV-001 AC1 and AC2, MSG-CONV-006 AC1 to AC3, MSG-CONV-009 AC2, MSG-API-009 AC1 (`7b32c58`) | `gate-test-names`: 301 criteria |
| The merges: `phase-13` (`67e2e53`), `origin/release-2-decisions` (`48b2e10`), `origin/phase-14-docs` at `7fdc41b` (`bac2a78`), `main` after phase 13's merge (`9f2b812`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `IConversationMembership.MembersAsync`'s use, and a mute's effect on notices | 15 | M-032: mute silences notices, which the live connection brings; the seam's member is in place and unread by the library |
| A hub, and the endpoint contract test's reading of hubs | 15 | No hub exists yet |
| Support's columns and tables, `visitor` and `library` authors, `note`, `automatic` and `system` messages written | 16 to 18 | M-032: a person writes `text` alone in phase 14 |
| Export of the conversations' tables, `SubjectExport.Conversations` | 20 | M-031 |
| `00` MSG-OVR-001 criterion 1; `09` MSG-CONV-012 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `09` MSG-CONV-012 | `MessageType` and the origin are open: the column `type` and `origin` are text checked against the spellings, and a later member adds a spelling and a migration of the check constraint, no change to a public member. |
| `04` MSG-LIB-006, release 2's additions | Every change to `1.0.0-rc.2`'s surface is an addition, in the `PublicAPI.Unshipped.txt` files of `Hermes.Core` and `Hermes.Conversations`; no record gains a `required` member. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **What answers first.** A call reaches the conversation before it reads any rule of the request, so a person who may not read it is answered `messaging.conversation.notfound` whatever else is wrong with the request (`09` MSG-CONV-006: "a non-member's every read and write answers 404"); the routes read their bodies first, as every group does, so a body that does not read is 400 or 415 before the conversation is reached.
2. **The seam outside the transaction.** `IsMemberAsync` and `ScopesAsync` are asked before the transaction opens, as the in-app list opens its values after its own, so no host call holds a row or a lock; the conversation's type and scope never change, and a direct conversation's participant row is read again in the transaction.
3. **A group participant row.** Written, without a mute and with no read position, the first time a member reads or writes the conversation, opening it included (M-032).
4. **Erased and deleted.** `erased` is true where the author is a person and the erasure emptied the author; a deleted message whose author is later erased reads both. A text a host protector answers erased reads `erased` with no text; a text that does not open under its binding reads with no text, not erased, and raises `content-rejected` for `messages`, as the in-app list does for an entry. A text that cannot open yet answers `messaging.key.unavailable`, so the route answers 503.
5. **Order.** Messages by sent instant then identifier, newest first; the read position compares the same pair, so it moves forward only even within a millisecond; conversations by their last instant then identifier, the cursor the in-app list's form over that pair; a direct conversation's participants by the instant they joined, then person.
6. **`before`.** A message identifier the conversation does not hold is outside its rule: 400 `messaging.request.malformed`, `member` `before` (`05`: "for one outside its rule").
7. **Values that do not read.** A route's `{id}` that does not read answers 404 `messaging.conversation.notfound` (`05`); a `{messageId}` that does not read, 404 `messaging.message.notfound`; a `replyTo` or `messageId` in a body that does not read, and a `participants` or `mentions` item that is null, 400 `messaging.request.malformed` naming the member; a `type` naming no type, 400 `messaging.message.invalid`, `field` `type`.
8. **Adding participants.** A person already in the conversation, or named twice, changes nothing, and one who left comes back with a new joined instant; a reference outside 1 to 128 characters is `messaging.conversation.invalid`, `field` `participants`, as at the start; an empty list changes nothing and answers 204. The count of 16 is checked after the additions, in the same transaction, which then commits nothing.
9. **Mentions.** A person named twice is mentioned once; the person may mention themselves, being a participant or a member; an edit keeps the mentions and a delete clears them (M-032).
10. **Locks.** Each call takes, shared, the locks of the person and of each person it writes a row for (the others of a direct conversation, the persons a message mentions), so an erasure of any of them waits for it (`09` MSG-CONV-009).
11. **Protection.** The text is protected before the transaction, bounded by `Hermes:Dispatch:Timeout`, as a push registration's values are; a protector that throws or does not answer, or a key not held, answers `messaging.key.unavailable` and raises `key-unavailable`, and nothing is written.
12. **The tables' registration.** `AddHermes` registers the three tables with key retirement and erasure, not `AddHermesConversations`, since the tables are the engine's and an erasure must reach rows a process with the package wrote (`06` MSG-OPS-002).
13. **Bodies.** The package holds its own serializer context, under the engine's options, and answers through the engine's `ErrorBody`, `Refusal` and request reader, which its grant reaches; a 201 answer is the package's own result type, declared in the route's metadata as `JsonAnswer` declares 200.
14. **The staff group needs the identity seam.** Mapping it records the group with the engine's mapped groups, so the start checks `IRecipientIdentity` as for the in-app group (`04` MSG-LIB-004).
15. **The order of the package's start checks.** A key the package does not know is checked before the membership seam, so a host with both faults is told the first in the order phase 13 began.

## Open questions

1. **`05`'s `GET /conversations` and 503.** Every other route of MSG-API-009 answers **503** "as for `GET /inapp`", and the amendment says the routes answer 503 as the in-app routes do, but the list route's row names no 503. The route answers 503 on a lock timeout as the others do, and declares none, so the contract test holds to `05` as written. Should the row gain 503? It parks nothing.
2. **A scope reference's bound.** The chapters bound a person reference at 128 characters but give a scope's reference none; the route takes any. Should it carry a bound, and with which refusal? It parks nothing.
3. **The sample host's in-app group.** `07` MSG-ENG-001 says the sample host maps every endpoint group; it has never mapped the in-app group, since it declares no in-app kind. Noted only; this phase does not change it.

## Commit and checks

The report names `4d09412`, on which every check below ran, the working tree holding nothing but this report's own edit:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,113 (`Hermes.Tests` 1,030, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 45, `Hermes.Conversations.Tests` 37, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (17 commits after `main`, the merges among them), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations` (11 migrations), `gate-sample-host` (the sample host's 11 tests) and `gate-test-names` (301 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 242 commits, no leaks |

A first run of the gates found two unpushed commit messages over 72 characters, and the secret scanner's generic rule took the migrations gate's test, a list of migration names on one line, for a key: the messages were reworded and the list written one name a line, in those unpushed commits, before the run above. Nothing found was a credential.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
