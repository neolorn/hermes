# Phase 17: automatic replies and desk tools

## Items built

| Item | Tests |
|---|---|
| `09` MSG-CONV-003, MSG-CONV-005; `03` MSG-TPL-001, MSG-TPL-006; `04` MSG-LIB-002; `06` MSG-OPS-002, MSG-OPS-004; `08` sections 1, 3 and 8 (`9725096`): the migration `DeskTools`: on `conversations` the columns `tags`, `language`, `replies_fired`, `snoozed_until`, `waiting_at`, `closing_at` and `close_at`, with their indexes; the table `support_replies`, a reply shared by a topic or a person's own, exactly one of the two, its name unique per owner, a person's own deleted at their erasure; the job `conversation-timers` in the job table, every 30 seconds, known to `job-stalled`; `TemplateKey.Reply`, the library's own source reading `replies/<name>.<language>.text.liquid`; the startup code `messaging.startup.templatemissing` | `RelativePath_AnAutomaticReplysKey_IsUnderRepliesByItsNameLanguageAndPart`, `MSG_CONV_003_AC3_TheLibrarysOwnSourceServesAReplyFromReplies`, `Erase_AStaffMember_DeletesTheirOwnSavedRepliesAndKeepsATopicsShared` (`f78d0ba`); `gate-migrations` (14 migrations) |
| `09` MSG-CONV-003, MSG-CONV-005; `05` MSG-API-009, MSG-API-010; `04` MSG-LIB-002; `03` MSG-TPL-004; `08` sections 1 and 4 (`3ec9d58`): the keys `Hermes:Conversations:Replies` (each rule's `Trigger`, `Topics` and, for `waiting`, `After`), `Hermes:Conversations:Tags`, `Support:CloseAfter` and `Support:CloseWarning`, read at start, each outside its rule stopping the start naming its key; every rule's template parsed and walked at start in every declared language, a missing one `messaging.startup.templatemissing` and one that does not parse `messaging.startup.declarationinvalid`; the staff routes for tags, snoozing and saved replies, with the codes `messaging.conversation.closed` and the four `messaging.reply` codes; `IConversations`' six desk members, `SavedReplyId`, `SavedReplyRecord`, and `ConversationRecord`'s `Tags` and `SnoozedUntil`; a conversation's `tags` and `snoozedUntil` on staff bodies only, the public routes answering a body without them; a snoozed conversation keeping its assignee and counting toward their limit, a visitor's or customer's message waking it; the 503 declared on `GET /support/queue` and the seven public routes that take a person's locks | `MSG_CONV_005_AC2_SavedRepliesKeepTheirBoundsAndOwners`, `MSG_CONV_005_AC2_ATopicsRepliesAreSharedByItsStaffAndUnknownToOthers`, `MSG_CONV_005_AC2_APersonHoldsAHundredRepliesAndATopicTwoHundred`, `MSG_CONV_005_AC3_AnUndeclaredTagIsRefusedAndTheTagsAreOnStaffBodiesOnly`, `MSG_CONV_005_AC3_ATagDroppedFromTheDeclarationStaysUntilItIsPutAgain`, `MSG_CONV_005_AC3_ATagOutsideTheNameRuleOrAFiftyFirstFailsTheStart`, `MSG_CONV_005_AC4_ASnoozeKeepsItsRangeAndTheVisitorsMessageWakesIt`, `MSG_CONV_005_AC4_ASnoozedConversationKeepsItsAssigneeAndCountsTowardTheirLimit`, `MSG_CONV_005_AC4_TaggingOrSnoozingAClosedOrANonSupportConversationIsRefused`, `MSG_CONV_003_AC1_RulesAreReadWithTheirTriggersTopicsAndWait`, `MSG_CONV_003_AC2_ARulesMissingTemplateFailsTheStartNamingIt`, `Start_ARulesTemplateThatDoesNotParse_IsDeclarationInvalidNamingTheRuleAndItsField`, `Start_ARuleNamingATopicNotConfigured_IsConfigurationInvalid`, `SupportConfigurationTests` (each new key's rule), `MSG_API_010_AC1_TheContractTestFindsExactlyThePublicRoutesAndHub` and the contract test's other tests |
| `09` MSG-CONV-011 (`792755d`): a block's `until` given through the staff route read in UTC on any host; the same fix is on `phase-16` as `c4c3e53` | `Block_AnUntilThroughTheRoute_IsReadInUtcWhateverTheHostsZone`, which fails without the fix on a host east of UTC |
| `09` MSG-CONV-003, MSG-CONV-005; `06` MSG-OPS-004 (`eb51639`): a support conversation's language, read from `Accept-Language` at its start as the in-app list reads it, else the default language, for a visitor and a customer; `started`, and on an offline form's conversation `outsidehours` or `unavailable`, fired as it starts; `waiting` and `closing` fired by the job; one `automatic` message a rule, by the library, in the conversation's language, once per conversation, in name order, never emailed, a reply that fails to render or to be protected writing nothing, logged, and counted as fired; the four values `topic`, `place`, `wait` and `opens`, `opens` searched up to a year ahead in the topic's zone; the job `conversation-timers` under the job claims, waking snoozes, firing `waiting` and `closing`, and closing a conversation silent past `CloseAfter` with a `system` message without text; the silence moved only by a staff member's, a visitor's or a customer's `text`; a snoozed conversation on its assignee's list and the caller's current one | `MSG_CONV_003_AC1_EachTriggerWritesOneAutomaticMessageInTheConversationsLanguageOnce`, `MSG_CONV_003_AC1_ATransferFiresNoStartAndAnAssignedConversationNoWait`, `MSG_CONV_003_AC1_AReplyRenderedPastItsBoundWritesNothingAndCountsAsFired`, `MSG_CONV_005_AC4_ASnoozedConversationWakesAtItsInstant`, `MSG_CONV_005_AC5_ATextAfterTheWarningRestartsTheSilenceAndANoteDoesNot`, `MSG_CONV_005_AC6_ARestartFiresNoTimerTwiceAndMissesNoneAndNothingFiresOnAClosedConversation`, `RunAsync_TwiceWithinItsInterval_TakesTheClaimOnce`, `NextOpening_IsTheNextRangesStartInTheTopicsZoneAndNoneWithoutHours` |
| `09` MSG-CONV-005 (`e487994`): one owner's saved replies, a topic's or a person's own, written under a transaction lock of that owner, so writers at once count them and check their names in turn | `AddReply_ManyAtOnceUnderOneName_AddsOneAndAnswersTheRestNameExists`, which failed with a unique-index error before the lock |
| `04` MSG-LIB-002 (`b12d0b2`): the assembly contract test's table of public types gains `SavedReplyId` and `SavedReplyRecord`, which `3ec9d58` added without it | `MSG_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive`, which the first gate run found failing |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-CONV-003 AC1 to AC3, MSG-CONV-005 AC2 to AC6 (`f78d0ba`) | `gate-test-names`: 329 criteria |
| The merges: `phase-17-docs` at `2de2020` (`c05ba21`) and at `649bc7e` (`11dd40a`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The release notes' line for template sources (`06` MSG-OPS-007) | The release of conversations | The release notes gain a section per release; `CHANGELOG.md` carries the line now (`9725096`) |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `04` MSG-LIB-006, release 2's additions | `ConversationRecord`'s two members are optional `init` members; `TemplateKey.Reply` is an optional `init` member; `IConversations`' new members change an interface no release holds yet. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **Tags.** The set is put whole; a repeated tag counts once. Only a tag not already on the conversation must be declared, so one dropped from the declaration stays until the set leaves it out, and is refused when put back.
2. **Snoozing.** A snoozed conversation is still the conversation's open state for every rule but the queue: its assignee writes in it, it may be transferred, tagged or snoozed again, it stays on their list, it is the caller's current conversation and their open one on its topic (a second start answers `alreadyopen`), and its visitor's typing is relayed. A queued conversation snoozed leaves the queue until it wakes, and returns at its old place. A wake changes the status alone and pushes nothing.
3. **Saved replies' reads.** `GET /support/replies` takes exactly one of `topic` and `mine`, `mine` with no value or `true`; neither, both, or a repeated one answer 400 `messaging.request.malformed`, `member` `topic`. The list is ordered by name, then identifier. A topic's reply is changed and removed by any member of the topic's scope.
4. **Saved replies' counts and names under concurrency.** Each owner's replies are written under a transaction lock of that owner (the topic, or the person), so two writers count and check names in turn.
5. **The language.** A visitor's and a customer's conversation take the language `Accept-Language` matches among the declared languages, as the in-app list and the notice's acknowledgement match it, else the default language (your answer).
6. **The replies at a start.** They fire after the start's transaction and the assignment that follows it, in a transaction of their own under the conversation's lock, so `place` is none where the conversation was assigned at once. `outsidehours` fires where the start falls outside the topic's hours, and otherwise `unavailable` where no member is available, both on an offline form's conversation only.
7. **The reply's values.** `wait` is the whole minutes since the conversation was queued, its last queueing after a transfer. `opens` is the start of the topic's next range after now in the topic's zone, a date's exception before its weekday, none within its hours or where there is none within a year; a start a clock change skips moves to the first minute after it. `place` is the place as a read answers it.
8. **A value that is none.** Strict rendering refuses a name it reads whose value is nil, so a reply reading `place` would fail whenever the visitor was assigned. A value that is none is therefore given as a defined value that is false in a condition, empty in an output and blank to `default`; a template writes `{% if place %}` as it would for a nil. See open question 2.
9. **Replies fired at once.** They are written in name order, each a microsecond after the one before, so a read keeps that order.
10. **The waiting instant.** It is the conversation's queueing instant plus the shortest `After` of a `waiting` rule serving its topic that has not fired, none once it is assigned, closed or snoozed. A transfer to a topic's queue sets it again from the new queueing; a rule that fired before the transfer does not fire again.
11. **The closing instant.** It is `CloseWarning` before the close while a `closing` rule serving the topic has not fired. A rule fires once, so a silence restarted after the warning ends in a close without a second warning.
12. **The automatic close.** Its `system` message is by the library, owned as the conversation's other messages are, with no text; the close sets no `closed_by`, as the visitor's own close does; the topic's queue is told and its assignment runs again. Every close, a staff one included, clears the conversation's timers.
13. **The timers' pass.** Wake, then `waiting`, then `closing`, then the close, each conversation in a transaction of its own under its row's lock, re-checking what it fires, at most 100 conversations a unit a pass, the claim renewed between units.
14. **The silence's start.** A conversation's silence starts at its creation, the visitor's or customer's first message being a `text`.
15. **A snooze's `until`.** It takes the two instant forms the API reads, in UTC.

## Open questions

1. **Does a staff close write a `system` message?** `09` reads: the automatic close "is a `system` message and the status `closed`, as a staff close is". Phase 16's staff close writes no message, and phase 17 leaves it so; only the automatic close writes one. Should a staff close, and the visitor's own close, also write a `system` message without text? This parks nothing built: the automatic close is done either way.
2. **A chapter line for a value that is none.** `09` gives `place` and `opens` as none at times, and `03` makes rendering strict. The rule in item 8 of the resolved list makes such a value readable. Should `09` (or `03`) say so, so a host's template author knows to test it with `{% if %}`? Nothing parks.

## Commit and checks

The report names `e487994`, on which every check below ran, the working tree holding nothing but this report's own edit:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,222 (`Hermes.Tests` 1,040, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Conversations.Tests` 135, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 46, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (31 commits after `main`, phase 16's not yet merged and the merges among them), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations` (14 migrations), `gate-sample-host` and `gate-test-names` (329 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 289 commits, no leaks |

A first run of the gates found the assembly contract test failing: the desk's commit added `SavedReplyId` and `SavedReplyRecord` to `Hermes.Core` without adding them to the test's table of the types `04` gives. `b12d0b2` adds them; every gate passed on the run above.

Phase 17 branches from `phase-16`, whose pull request is not merged yet, so this pull request's diff holds phase 16's commits until then; `phase-17` merges `main` once it is.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
