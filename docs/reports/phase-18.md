# Phase 18: the end of support

## Items built

| Item | Tests |
|---|---|
| `09` MSG-CONV-004, MSG-CONV-005, MSG-CONV-009; `03` MSG-TPL-006; `01` MSG-SEND-018; `06` MSG-OPS-002; `08` section 8 (`0ffc35a`): the migration `SupportEnd`: on `conversations` the survey's columns `survey_at`, `survey_resolved`, `survey_rating` (1 to 5), `survey_comment` with `survey_comment_key_version`, `survey_person`, and `transcript_sends` (0 to 3), with their checks and indexes; the comment known to key retirement, re-protection and erasure under the context `conversations:<id>:surveycomment`, deleted with a customer's conversation; erasing the staff member rated empties `survey_person` and keeps the survey; the library's page `transcript`, its one part `text`, in `en` and `ar`; the counters `hermes.conversations.started` and `hermes.conversations.closed` | `MSG_CONV_009_AC1_ACustomersSupportTextOfflineAddressAndSurveyCommentAreProtectedAgainForTheCustomer`, `Erase_AStaffMemberOfASupportConversation_EmptiesTheirReferencesAndKeepsTheCustomersText`, `ReadAsync_TheTranscriptPage_IsTheLibrarysOwnInEachShippedLanguage`, `MSG_OPS_005_TheMeterHermesPublishesTheInstrumentsOfTheReference`; `gate-migrations` (15 migrations) |
| `09` MSG-CONV-002, MSG-CONV-005, MSG-CONV-006, MSG-CONV-008; `04` MSG-LIB-002; `05` MSG-API-009; `08` sections 1 and 4 (`9516140`): a topic's `Supervisors` read at start, both keys or neither; supervisors read the topic's conversations and messages, notes included, and receive the staff hub's pushes for them, but neither write, tag, snooze, transfer, close nor list them; `GET /support/live` and `GET /support/reports` for the topic's supervisors alone, anyone else answered `messaging.conversation.notfound`; the reports computed when read with `percentile_cont`, per staff member and for the topic, over at most 92 days in the topic's zone; `IConversations.LiveAsync` and `ReportAsync` with `LiveConversation` and `ReportFigures`; every close, by staff, by the visitor or customer, or for its silence, one `system` message without text in its transaction; each start and close counted by topic; each snooze, and each wake, a transaction of its own that pushes the conversation to its assignee | `MSG_CONV_005_AC7_OnlyASupervisorOfTheTopicReadsItsLiveViewAndReports`, `MSG_CONV_005_AC7_ASupervisorReadsNotesButNeitherWritesNorListsTheTopicsConversations`, `Push_ANoteOfTheTopic_ReachesItsConnectedSupervisor`, `MSG_CONV_005_AC8_ReportsGiveTheMediansThe90thPercentilesAndTheSurveysPerStaffMemberAndTopic`, `Reports_APeriodThatDoesNotReadOrPasses92Days_IsMalformed`, `Reports_NinetyTwoDays_AreAPeriod`, `Read_ATopicsSupervisors_AreItsScopeAndNoneWhereUnset`, `Read_SupervisorsHalfGivenOrOutsideTheirRule_IsTheirKey`, `Close_ByStaffOrByTheVisitor_WritesOneSystemMessageWithoutTextPushedAsAnyMessage`, `Counters_EachStartAndEachClose_AreCountedByTopic`, `Wake_ASnoozeThatEnds_ReachesTheAssigneesHub`, `MSG_LIB_002_TheLiveViewAndReportsPrintNoPersonReference` (`d0fc27d`) |
| `09` MSG-CONV-004; `05` MSG-API-010; `03` MSG-TPL-004, MSG-TPL-007; `08` sections 1 and 4 (`fc82dda`): the public group's transcript route, plain text in UTF-8 of at most 1 MiB, as an attachment named `transcript-<id>.txt`; its email route, as `Support:TranscriptKind`, at most three times, each send's idempotency key `conversation:<id>:transcript:<n>`, the value bounded at half `Hermes:Email:MaxBytes`, a send the engine refuses answering its code as 400; the survey route, once, within seven days of the close; the codes `notclosed`, `transcriptlimit`, `surveyinvalid`, `surveyanswered` and `surveyclosed`; `Support:TranscriptKind` required where topics are configured; the page checked at start in every declared language; a transcript's render under ten times `MaxSteps`, rendered again with the oldest messages left out past either bound | `MSG_CONV_004_AC1_TheTranscriptHoldsEveryMessageButNotesInItsLanguageNamesNoStaffAndIsServedOnlyClosedToItsVisitor`, `Transcript_PastItsSteps_LeavesOutTheOldestMessagesAndSaysHowMany`, `MSG_CONV_004_AC2_EmailingItSendsTheDeclaredKindAtMostThreeTimes`, `MSG_CONV_004_AC3_TheSurveyIsTakenOnceWithinSevenDaysAndItsCommentIsProtected`, `Survey_MoreThanSevenDaysAfterTheClose_IsClosed`, `Start_TopicsWithoutATranscriptKind_IsConfigurationInvalidNamingIt`, `Start_ALanguageWithoutATranscriptPage_IsDeclarationInvalidNamingTheTranscript`, `MSG_API_010_AC1_TheContractTestFindsExactlyThePublicRoutesAndHub` and the contract test's other tests |
| `CHANGELOG.md` under `## [Unreleased]`: each of the above in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-CONV-004 AC1 to AC3, MSG-CONV-005 AC7 and AC8 (`fc82dda`) | `gate-test-names`: 334 criteria |
| The merges: `phase-18-docs` at `220abd5` (`866b8b5`), `44a3b06` (`113e8b5`) and `e186d4f` (`e1c781c`); `phase-17` after main's merge of phase 16 (`49f75fa`); `main` after phase 17's merge (`8f12098`) | The chapters are the phase's reference |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `04` MSG-LIB-006, release 2's additions | `LiveConversation` and `ReportFigures` are new records; `IConversations`' new members change an interface no release holds yet. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`. CA1716 refused a parameter named `to` on `IConversations.ReportAsync`; it is named `through`, as your answer and `04` now give.

## Resolved by rule

1. **What a supervisor may do.** Read: the conversation and messages routes, read positions, and the staff hub's pushes. Not work: a write, tags, snoozing, transfer, close and block answer as for a person who may not work the conversation (`notfound`). A supervisor who is also of the topic's scope works it as its staff do.
2. **A supervisor's pushes.** Every event of the topic's conversations but `conversation`, which means one added to the person's list; the members of the supervising scope are asked when the event is pushed.
3. **Snooze and wake pushes.** The staff hub has no status event, so a snooze and a wake push `conversation`, the conversation as it now reads, to its assignee alone (your reading).
4. **The live view.** Ordered by queueing, oldest first; `place` counts the queued open ones in that order; `waited` is the whole minutes since its last queueing.
5. **The report's period.** `from` through `to` in the topic's time zone, from the start of `from` to the start of the day after `to`, a start a clock change skips moved to the first minute after it. `to` before `from`, or more than 92 days counting both, answers `member` `to`; a date that does not read names its own member.
6. **The report's figures.** Conversations closed within the period, grouped by their assignee when they closed, which a close keeps; one never assigned counts for the topic alone, with no waiting or handling time. `resolvedShare` is a share from 0 to 1, `meanRating` the mean of the ratings; the times are seconds as numbers with their fractions. Staff rows are ordered by person reference.
7. **The transcript's authors.** `visitor` for the visitor's or the customer's own messages, `library` for the library's (automatic replies and the close's message), `staff` for every other, an erased staff member included. A deleted message, and the close's message, carry an empty text.
8. **Leaving out messages.** The fewest oldest messages that let the render pass, found by halving; a key that cannot open a message answers 503, as other reads do.
9. **The transcript email's count.** A send is counted only once the engine takes it, under the conversation's lock, so a refused send leaves the count; the next send's key is the next number.
10. **A refused send.** Its failure code as 400, except `messaging.key.unavailable`, which stays 503.
11. **The survey's body.** A body that does not read as JSON of the right types answers `messaging.request.malformed`; values outside their rule answer `surveyinvalid`, checked before the conversation's state. An empty comment is none.
12. **The survey's window.** Seven days from the close, measured when the answer is written.

## Open questions

None.

## Commit and checks

**The branch phase-18-b replaces phase-18.** I first pushed this phase as `phase-18`. One commit's header there was 74 characters, over the 72 the commit-message gate allows. The local gates found it, but I misread their summary, and my pre-push check ran the gate through a pipe that swallowed its exit code, so the branch went out and its first report wrongly said all 13 gates passed. Pushed history is never rewritten, so `phase-18-b` was made from `phase-18`'s head with that one header reworded, now `0ffc35a` "feat(engine): hold the survey, the transcript page and the counters". Every commit before it keeps its hash, and the commits after it differ only in their parents. `git diff` between the two heads is empty. `phase-18` stays as it was and is deleted after the merge. Every gate now runs without a pipe, so a failure stops the push.

The code is `d0fc27d`'s. Every check below ran on this branch with this report's correction committed, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,250 (`Hermes.Tests` 1,042, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Conversations.Tests` 160, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (22 commits after `main`, the merges among them), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations` (15 migrations), `gate-sample-host` and `gate-test-names` (334 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 303 commits, no leaks |

The first gate run on `phase-18-b` failed one test outside this phase: `MSG_DEV_001_AC1_AnEmailHandedToTheProvidersDirectlyIsWrittenToTheInboxAndReachesNoneOfTheHostsProviders(order: ProvidersAfter)` of `Hermes.Development.Tests`. The development inbox's start line names its folder, a random GUID, and that GUID happened to hold `4521`, one of the values the test's log check refuses (`Captured.cs`). It is a chance collision, about one run in two thousand, not a leak. The second run, above, passed every gate. The test's check is a defect to fix in its own change; this phase leaves it.

The chapters merged at the phase's start changed `05`, so the endpoint contract test failed between that merge and the commits that built the routes; it passes from `fc82dda`. The metrics test likewise failed from the merge until `0ffc35a` added the two counters.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
