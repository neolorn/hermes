# Phase 15: the live connection

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-002; `05` MSG-API-009 (`dd59ce2`), the lead's answer to phase 14's second question: a scope's reference is 1 to 128 characters, as a person reference is; outside it, `OpenGroupAsync` answers and the group route refuses with `messaging.request.malformed`, `member` `reference` | `OpenGroup_AReferenceOutsideItsBound_Is400NamingIt` |
| `07` MSG-ENG-001 (`7a82438`), the answer to the third: the sample host maps the in-app group | `InApp_ASignedInCustomer_ReadsTheirListAndAnAnonymousCallerIs401`, in `gate-sample-host` |
| `05` MSG-API-009 (`b8f81a4`), the answer to the first: `GET /conversations` declares the 503 it answers when the person's locks are not had in time | `MSG_ENG_006_AC1_TheMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05`, against the chapter's new line |
| `09` MSG-CONV-006, MSG-CONV-008; `06` MSG-OPS-002; `08` section 8 (`dfcd749`): the table `connections` (each staff-hub connection's person, process and last-seen instant), deleted at its person's erasure; each participant's `away_told`, the message they were last told of while away; the migration `LiveConnection` | `MSG_CONV_009_AC2_ErasingAnAuthorClearsTheirMessagesAndRowsAndLeavesOtherMessagesUntouched` (the person's connections among the rows it finds gone); `gate-migrations` (12 migrations) |
| `09` MSG-CONV-005, MSG-CONV-006, MSG-CONV-008; `04` MSG-LIB-002; `05` MSG-API-009; `06` MSG-OPS-003; `07` MSG-ENG-006, MSG-ENG-009; `08` sections 4 and 8 (`b84e1b2`): the staff hub at `{prefix}/live` in the staff group, its person from the identity seam at connect and a connection without one refused; `message`, `messageChanged`, `read`, `typing` and `presence` pushed, and `Typing` taken; a write's `NOTIFY messaging_conversations` sent in its transaction, so it goes out when the write commits; each process listening on a connection of the package's own, on `listenConnection` where it is given and `connection` otherwise, and pushing to its connections whose person may read the conversation now, a group's readers from one `MembersAsync` call an event; presence from fresh `connections` rows, refreshed every 30 seconds and gone after 90, told on a person's first and last connection and when a stale row is removed, shown in direct conversations alone; `ParticipantRecord.Online` and a participant's `online`; `MessagesAsync`'s `after` and the route's, oldest first; away notices as `Hermes:Conversations:AwayKind` names, checked at start; `Microsoft.AspNetCore.SignalR.Client` in the package's tests at the shared framework's version | `MSG_CONV_008_AC1_AMessageWrittenThroughOneProcessReachesAClientConnectedToAnotherAfterItIsStored`, `MSG_CONV_008_AC2_AClientThatReconnectsReadsWhatItMissedWithAfter`, `MSG_CONV_008_AC3_TheNotificationsPayloadHoldsNoPersonReferenceOrText`, `MSG_CONV_008_AC4_ANonMemberReceivesNothingOfAGroupConversationAndAMemberTheSeamDropsNothingFromTheNextEvent`, `MSG_CONV_008_AC5_AnAwayParticipantIsToldOnceUntilTheyReadAndAConnectedOneIsNotTold`, `MSG_CONV_008_AC5_AMentionIsToldThroughAMuteAndAMutedParticipantNotMentionedIsNot`, `MSG_CONV_008_AC6_AwayKindNamingAnUndeclaredKindStopsTheStartNamingTheKey`, `MSG_CONV_005_AC1_TypingReachesTheOthersAndIsNeverStored`, `MSG_CONV_006_AC4_PresenceTurnsOfflineWithin90SecondsOfAProcessStopping`, `Connect_ThePersonsFirstConnection_TellsTheOtherParticipantsTheyAreOnlineAndTheLastThatTheyAreNot`, `Connect_WithNoPersonSignedIn_IsRefused`, `Write_WithNoAwayKind_TellsNoOne`; `MSG_API_009_AC2_TheContractTestFindsTheStaffHubsRouteAndMethods`, `MSG_API_009_AC2_AHubMethod05GivesThatTheHubDoesNotHaveIsFound`, and the endpoint contract test's other tests, which now read the hubs of `05` and of the mapped endpoints |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-CONV-008 AC1 to AC6, MSG-CONV-005 AC1, MSG-CONV-006 AC4, MSG-API-009 AC2 (`b84e1b2`) | `gate-test-names`: 310 criteria |
| The merges: `main` after phase 14's merge (`a0d7f23`), `origin/phase-15-docs-b` at `d417221` (`acd26d4`); the first merge of `phase-15-docs` was dropped before any push, as asked | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The hub's `conversation` push | Parked on open question 1 | No event of the notification names a conversation added to a person's list |
| The public hub | 16 | M-033: C50 is phase 16's |
| The release gate's reading of the hubs | 21 | M-031 |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `04` MSG-LIB-006, release 2's additions | `ParticipantRecord.Online` is an optional `init` member; `MessagesAsync`'s new parameter changes a member of `IConversations`, which no release holds yet. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **When a push goes out.** A write's `pg_notify` runs in its own transaction, so PostgreSQL delivers it when the write commits and never for a write rolled back; typing, which stores nothing, notifies in a transaction of its own that writes the typist's participant row where it is missing.
2. **What a push carries.** Each process reads the store when the notification arrives: a message's text opened under its author's key as a read opens it (a text that cannot open yet is not pushed, the client reading it with `after`; one that does not open is pushed with no text and raises `content-rejected`); a read position from the participant's row. Pushes are written through the package's serializer context, as JSON elements, so the host's SignalR options never change their shape.
3. **The readers of an event.** A direct conversation's participants who have not left, and a group's members from one `MembersAsync` call an event, each intersected with the persons connected to the pushing process. `read`, `typing` and `presence` go to the other readers, and `message` and `messageChanged` to all, the author's other connections included.
4. **Presence.** A person goes online with their first fresh row and offline with their last: connecting, disconnecting and the 30-second refresh, which also removes every row older than 90 seconds, tell the processes once for each direct conversation the person is in. A read answers `online` from the rows fresh at that instant, so a stopped process's connections read offline once 90 seconds have passed even before a refresh removes them.
5. **Who is away.** A person with no fresh row when the message is written, decided in a transaction under their shared locks; a direct conversation's participants who have not left but the author, and a group conversation's persons mentioned but the author, their participant row written first where it is missing. Muted and not mentioned, a person is not told; mentioned, they are told through a mute (M-032).
6. **Once until read.** `away_told` records the message told; a person is told again only once their read position is at or past it, so reading the told message itself is enough.
7. **The away send.** Made after the write commits, through `IMessages`, for the person as recipient with no destination, so the kind's channels that need none (push, in-app) carry it; its values are text and a flag (`03` MSG-TPL-002), and a refusal is logged with its code, never failing the write.
8. **`after` and `before`.** Given together, or naming a message the conversation does not hold, they are outside their rule: `messaging.request.malformed` naming the member.
9. **The hub's person.** The identity seam reads the request through `IHttpContextAccessor`, so the hub sets the accessor's context to the connection's own before asking it.
10. **The contract test's hubs.** A hub is read from `05` by the route its section's text gives ("hub at `/live`") and its table of hub methods; from the mapped endpoints by the hub's metadata, the methods a client calls (public on the hub's type) and the methods it sends, which the hub's type lists beside their names, since a hub sends by name.
11. **A connection's process.** Each process names itself by an identifier made when it starts, so a refresh touches its own rows alone.
12. **The listener's backoff.** As the dispatcher's: one second, doubled to the 30-second refresh.

## Open questions

1. **The `conversation` push.** `05` lists `conversation(conversation)`, "one added to the person's list", but M-033 gives the notification five events (`message`, `changed`, `read`, `typing`, `presence`), none naming a conversation added. A direct conversation started or a participant added would need a sixth event, `conversation:<conversationId>:<participantId>` say, which `08` section 8 would list. The hub lists the method, so the contract test holds, but never sends it. Should the notification gain that event? It parks the push alone.

## Commit and checks

The report names `b84e1b2`, on which every check below ran, the working tree holding nothing but this report's own edit:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,128 (`Hermes.Tests` 1,032, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Conversations.Tests` 50, `Hermes.Core.Tests` 45, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (8 commits after `main`, the merges among them), `gate-dependency-allow-list` (`Microsoft.AspNetCore.SignalR.Client` in a test project alone), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations` (12 migrations), `gate-sample-host` (the sample host's 12 tests) and `gate-test-names` (310 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 256 commits, no leaks |

Two runs of the new live tests found a race: a client's start returns before the hub has recorded its connection, so a message written at once could find its reader away. The tests' host now waits for the connection's row before a test goes on; the hub is unchanged, as a client connecting is not yet online until its row is written. The live and away tests then passed in three runs in a row.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
