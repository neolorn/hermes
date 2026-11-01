# Phase 16: support's public side

## Items built

| Item | Tests |
|---|---|
| `09` MSG-CONV-001, MSG-CONV-009, MSG-CONV-010, MSG-CONV-011; `06` MSG-OPS-002; `08` section 8 (`d591acd`): the migration `SupportSide`: support's columns on `conversations` (`topic`, `assignee`, `queued_at`, `assigned_at`, `offline`, `closed_at`, `closed_by`, `customer`, `email` and `email_key_version`); a participant's `person` or `visitor`, exactly one, with `hash_version`; each message's `owner`, written from its author for the messages already held; the tables `support_agents`, `support_blocks`, `support_acknowledgements` and `support_counters`; each known to key retirement, re-protection and erasure: a customer's support conversations deleted with every row at their erasure, an assignee's and a closer's reference emptied at theirs, a message's text protected again for its owner; the hash domains `visitor` and `source`, the context `conversations:<id>:email` | `MSG_CONV_009_AC1_ACustomersSupportTextAndOfflineAddressAreProtectedAgainForTheCustomer`, `Erase_AStaffMemberOfASupportConversation_EmptiesTheirReferencesAndKeepsTheCustomersText`, `Erase_ACustomer_DeletesTheirSupportConversationsWithEveryRowOfThem`; `gate-migrations` (13 migrations) |
| `08` section 4; `09` MSG-CONV-002, MSG-CONV-010, MSG-CONV-011 (`fc9071a`): support's keys read at start: each topic's `TimeZone`, `Hours`, `Exceptions` and `Scope`, `Support:Limit`, `Support:OfflineReplyKind` and `Support:NoticeVersion`, the last two required where topics are configured and the kind a declared one, the three `Limits:` and `Retention:Visitor`, each outside its rule stopping the start naming its key; a topic within its hours in its time zone, a date's exception first | `SupportConfigurationTests` (each key's rule, the hours read in the topic's zone, an exception replacing its day), `Start_OfflineReplyKindNotDeclared_StopsTheStartNamingTheKey` |
| `09` MSG-CONV-001, MSG-CONV-002, MSG-CONV-009, MSG-CONV-010, MSG-CONV-011; `04` MSG-LIB-002, MSG-LIB-004; `05` MSG-API-009, MSG-API-010; `08` section 1 (`10a3a10`): `MapHermesPublicConversations` and its eight routes, anonymous callers allowed; the visitor's reference drawn on the first call that needs one, kept in `messaging_visitor` (HttpOnly, Secure, SameSite Lax, the public prefix's path, living as `Retention:Visitor`, renewed on use) and held only as its keyed hash; a signed-in customer as the identity seam answers them, never asked for the notice; the notice's acknowledgement with its version, instant and language; one open conversation per topic; offline outside hours or with nobody available; the offline form's conversation queued, its address protected, its staff replies sent by email as `OfflineReplyKind` with `conversation`, `message`, `topic` and `text`; automatic assignment, first in, first out per topic, to the available member with the fewest open conversations, ties to the longest since their last; the place in a read; transfer to a member or a topic's queue; close; blocks until an instant or lifted; the three limits counted in the database across processes, `Retry-After` answered; the staff routes for the queue, availability, transfer, close and block; a member of the topic's scope reading and working a support conversation, its assignee alone writing `text` or `note`, anyone else answered 404; a staff member's list holding their open assigned support conversations; `ConversationRecord`'s `Topic`, `Status`, `Place` and `Offline`, and `IConversations`' six support members | `MSG_CONV_002_AC1_AConversationGoesToTheAvailableMemberWithTheFewestOpenTiesToTheLongestSinceTheirLast`, `MSG_CONV_002_AC1_TheQueueIsFirstInFirstOutAndAMemberWhoFreesUpTakesTheOldest`, `MSG_CONV_002_AC2_TheVisitorsPlaceIsRight`, `MSG_CONV_002_AC3_ATransferMovesTheConversationToAMemberOrAnotherTopicsQueue`, `MSG_CONV_002_AC4_OutsideHoursOrWithNobodyAvailableTheStartIsOfflineAndTheOfflineFormIsAnsweredByEmail`, `MSG_CONV_001_AC3_TheVisitorsReferenceIsNeverStoredInClearAndAnotherBrowsersCookieOpensNothing`, `MSG_CONV_001_AC4_AVisitorNeverReadsANote`, `MSG_CONV_010_AC1_NoVisitorMessageIsTakenBeforeTheCurrentNoticeIsAcknowledgedAndACustomerIsNeverAsked`, `MSG_CONV_010_AC2_ErasingACustomerDeletesTheirSupportConversations`, `MSG_CONV_011_AC1_EachLimitAnswers429AtItsEdgeAcrossTwoProcesses`, `MSG_CONV_011_AC2_ABlockedVisitorIsRefusedUntilTheBlockEndsOrIsLifted`, `Start_ASecondOnTheSameTopic_IsAlreadyOpenNamingTheOpenOneAndCurrentIsTheNewest`, `StaffAccess_AMemberReadsAndOnlyTheAssigneeWritesAndAnyoneElseIsNotFound`, `Assign_AMemberSetAvailableWhoConnects_TakesTheQueuedConversation`, `MSG_LIB_002_ASupportConversationPrintsItsTopicStatusPlaceAndOffline` |
| `09` MSG-CONV-001, MSG-CONV-002, MSG-CONV-008; `05` MSG-API-010; `08` section 8 (`5fb3b21`): the public hub at `{prefix}/live`, its caller the visitor's cookie or the signed-in customer, a connection with neither refused; `message`, `messageChanged`, `typing`, `place`, `assigned` and `closed` pushed to the visitor or customer, never a `note`, and `Typing` taken; a support conversation's events on the staff hub to its assignee alone; the events `place`, `assigned`, `closed` and `conversation`; `conversation` pushed to a person when a direct conversation they are in starts or gains participants, or a support conversation is assigned to them, closing phase 15's open question | `MSG_CONV_002_AC2_TheVisitorsPlaceIsPushedWhenTheQueueMoves`, `MSG_CONV_001_AC4_AVisitorNeverReceivesANote`, `Push_AnAssignment_TellsTheVisitorAssignedAndTheAssigneeTheConversation`, `Push_TypingAndMessages_ReachTheAssigneeFromTheVisitorAndTheVisitorFromTheAssignee`, `Connect_WithNoCookieAndNoPerson_IsRefused`, `Push_ADirectConversationStarted_TellsItsParticipantsWithTheConversation`, `MSG_CONV_008_AC3_TheNotificationsPayloadHoldsNoPersonReferenceOrText` (its payloads now hold the new events) |
| `07` MSG-ENG-006; `05` MSG-API-010 (`69dd806`): the contract test maps the staff and public groups under prefixes of their own, reads each section of `05` and its hub under its group's prefix, and a hub method's arguments before the cell's first colon | `MSG_API_010_AC1_TheContractTestFindsExactlyThePublicRoutesAndHub`, `MSG_API_010_AC1_APublicRoute05DoesNotGiveIsFound`, and the contract test's other tests |
| `08` section 5; `04` MSG-LIB-004 (`6573ae6`): an offline address that does not open under its binding sends nothing and raises `content-rejected` for `conversations`; the public group mapped alone | `OfflineReply_AnAddressCopiedFromAnotherRow_SendsNothingAndRaisesContentRejectedForConversations`, `MSG_LIB_004_AC1_ThePublicConversationsGroupMappedAloneWorks`, `MapHermesPublicConversations_WithoutAddHermesConversations_ThrowsNamingIt`, `MapHermesPublicConversations_AfterAddHermesConversations_MapsItsNineRoutesUnderThePrefixAllowingAnonymousCallers` |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit | `gate-commit-messages` |
| `68c8d7d`: the message limit's test waits until its four writes fall in one fixed window | `MSG_CONV_011_AC1_EachLimitAnswers429AtItsEdgeAcrossTwoProcesses` |
| The test-name gate's list: MSG-CONV-002 AC1 to AC4, MSG-CONV-001 AC3 and AC4, MSG-CONV-010 AC1 and AC2, MSG-CONV-011 AC1 and AC2, MSG-API-010 AC1 | `gate-test-names`: 321 criteria |
| The merges: `origin/phase-16-docs-b` at `eda990c` (`996c9f7`) and at `1153ab3` (`204f000`), and `main` after phase 15's merge (`189b144`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| A visitor's conversations by retention | 20 | M-034: they go by retention, never by erasure |
| The topic's supervisors as readers and pushes' receivers | 18 | Your answer to the phase's third question |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `04` MSG-LIB-006, release 2's additions | `ConversationRecord`'s four members are optional `init` members; `IConversations`' new members change an interface no release holds yet. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The columns beyond the amendment's list.** The rules needed `conversations.customer`, `conversations.email` with `email_key_version`, `messages.owner` and `hash_version` beside each keyed hash; your answer named them, and the chapters now list them.
2. **Erasing a staff member.** Their references are emptied: `assignee` and `closed_by` on `conversations`, `author` on `messages`. Their `support_agents` row is deleted. A customer's text they wrote stays, under the customer's key. An open conversation of theirs goes back to its topic's queue.
3. **Who a message is protected for.** Its owner: in a staff chat the author; in a customer's support conversation the customer, staff replies included; in a visitor's, nobody, as an anonymous recipient's value. A staff edit is protected again for the owner. A message reads `erased` when its author is erased and its text was cleared.
4. **Writes in a closed conversation.** A visitor's, a customer's or a staff member's write in a closed support conversation is answered 404, as one they may not write. A staff transfer of a closed one is answered the same way. Closing one already closed changes nothing.
5. **The offline form and one open conversation per topic.** The form takes the same rule as a start: with one open on the topic it answers `messaging.support.alreadyopen`. An offline conversation is queued whatever the hours, and is assigned within them.
6. **A support conversation's mentions and types.** No mentions; a staff member writes `text` or `note`; only a `text` reply of an offline conversation is sent by email.
7. **What a limit counts.** A start counts the visitor's or customer's starts and the source's, both or neither, in one transaction after every other check passes, so a start refused for another reason counts nothing. A message counts the visitor's messages. Each counter is a fixed window from midnight UTC, the hour or the minute, raised only below its limit by one statement, so two processes never pass it. The 30-second refresh removes counters a day past their window.
8. **The source address.** IPv4 is its four bytes, an IPv4-mapped IPv6 address reads as IPv4, and IPv6 is its first eight bytes. A request with no remote address counts no source limit.
9. **The acknowledgement's language.** The request's `Accept-Language` matched to a declared language, as the in-app list matches it, else the default language.
10. **Hashes looked up.** A visitor's rows are found by their reference's hash under every `hashKeys` version held. New rows and counters are written under the write version.
11. **Assignment's safety across processes.** Each pass locks the topic's oldest queued conversation, skipping one another process holds, then the chosen member's availability row, and counts their open conversations again under it.
12. **The cookie's value.** `visitor:` and 22 base64url characters (16 bytes). Any other value is no cookie. A hub connection never draws or renews one.
13. **The place's push.** Every change to a topic's queue (a start, an offline form, an assignment, a transfer or a close of a queued one) tells each queued conversation of the topic. Each process pushes `place` with the place read when the event arrives, and nothing once the conversation is assigned.
14. **The `conversation` push.** It carries `<conversation>` as a read answers it for each receiver. A direct conversation's participants get it at its start and when participants are added. A support conversation's assignee gets it at assignment.
15. **The queue route's and the public routes' 503.** `05` gives no 503 on `GET /support/queue` or the public routes, though a customer's and a staff member's calls take the person's locks. The routes declare what `05` gives and still answer 503 when the locks are not had in time. See open question 1.

## Open questions

1. **503 on the queue route and the public routes.** `GET {staff}/support/queue` and the public routes take the person's locks (a staff member's, a customer's), so they can answer 503 as `GET /inapp` does, but `05` lists no 503 for them. Should `05` add `**503** as for GET /inapp` to those rows? Until then they declare what `05` gives. Nothing else parks.

## Commit and checks

The report names `68c8d7d`, on which every check below ran, the working tree holding nothing but this report's own edit:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,183 (`Hermes.Tests` 1,037, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Conversations.Tests` 99, `Hermes.Core.Tests` 46, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (13 commits after `main`, the merges among them), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations` (13 migrations), `gate-sample-host` and `gate-test-names` (321 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 277 commits, no leaks |

A first run of the gates found the message limit's test failing once: its four writes fell either side of a minute's fixed window, so the fourth was counted in a new window. The test now waits until all four fall in one window (`68c8d7d`); the limit itself is unchanged. The support tests then passed three runs in a row, and every gate passed on the run above.

The chapters merged at the phase's start changed `05`, so the endpoint contract test failed between that merge and the commits that built the routes; it passes from `69dd806`.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
