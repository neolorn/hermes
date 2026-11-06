# Phase 26: the dispatch test that failed on the release run

## The cause

The cause is in the test, and the engine behaves as `01` MSG-SEND-007 gives.

`MSG_SEND_007_AC11_WithEveryEmailCallTakingTheTimeoutAndAThousandOptionalEmailsDueADueSmsIsSentAndAnEssentialEmailIsClaimedFirst` modelled "every email call takes `Dispatch:Timeout`" by moving the host's one `ManualClock` 20 seconds inside the email provider's call, and then ran `DispatchAsync`, whose `RunPassAsync` runs a pass of each channel side by side (`src/Hermes/Sending/Dispatcher.cs` line 32). The SMS pass reads its start instant from that clock before it claims (`src/Hermes/Sending/DispatchPass.cs` line 91), and before each delivery it hands back every delivery it has not begun once it has run for `Hermes:Dispatch:Interval`, 5 seconds by default (line 119; MSG-SEND-007, "A pass that has run for `Hermes:Dispatch:Interval` hands back"). Where the email pass reached its first call, and moved the clock 20 seconds, while the SMS pass was still between its start instant and its first delivery (in its claim, a round trip to PostgreSQL), the SMS pass read 20 seconds gone, handed its one delivery back to `waiting` with no attempt counted, and sent nothing: `Assert.Single` found no SMS. Which pass got there first depended on how fast the machine ran each of them, so the pull request's run and `main`'s passed on the same commit and the release run did not.

On the test clock the email call's 20 seconds were charged to the SMS pass, which in a deployment spends milliseconds of its own time while another channel's call runs, so the hand-back is the test's, not the engine's.

How it was established (none of it committed):
- Instrumented runs of the test, alone and six at a time, recorded the clock as the SMS went out: in 7 of 28 the email pass had already moved the clock (to `+5` seconds, the clock observed while its 20-second move fired its timers in order), so the SMS pass did overlap the move on this machine too; each still went out, its claim having returned first.
- A reproduction held the SMS pass at its claim (a session of the test holding the rate counter row the claim writes) until the first email call had moved the clock 20 seconds: three runs out of three ended with no SMS received and the SMS delivery `waiting` with no attempt, the release run's result. The same reproduction moving the clock 4 seconds, under the interval, sent the SMS.

## Items built

| Item | Tests |
|---|---|
| `01` MSG-SEND-007 criterion 11's test (`c6c35b2`): the test runs the SMS and email passes side by side itself, as `RunPassAsync` does, through `EngineHost.PassAsync(Channel)`, a pass of one channel; the email call moves the clock 20 seconds only once the SMS pass has ended, so no move falls inside it whatever the machine's speed, and the SMS pass's claim of the one SMS is asserted. The essential email is still the first the email pass calls | `MSG_SEND_007_AC11_WithEveryEmailCallTakingTheTimeoutAndAThousandOptionalEmailsDueADueSmsIsSentAndAnEssentialEmailIsClaimedFirst` |

**Tests of the same shape.** We read every test of the repository in which a provider's or seam's fake, or the test while passes run, moves a clock that a pass reads. A move inside one pass's own call (`Pass_ThatHasRunForTheInterval_HandsBackTheDeliveriesItHasNotReached`, `MSG_CHAN_001_AC2_AProviderThatThrowsOrDoesNotAnswerLeavesItsDeliveryWaitingAndLogsOnlyTheExceptionsType`) is decided by the engine's own order, since a pass works its deliveries one after another and the other channels have nothing to take; so is a move inside a call that then runs another process's pass before it returns (`MSG_SEND_007_AC2_ADeliveryWhoseClaimExpiredWhileAnEarlierOneOfThePassWasSentIsNotSentByTheFirstHolderOnceAnotherTookIt`, `MSG_SEND_007_AC9_ASecondProcessDoesNotTakeOverAnEmailDeliveryDuringItsSeventyFiveSecondsOfChecksAndItIsSentOnce`, `Pass_ChecksLastingLongerThanTheClaimTimeout_AreNotTakenOverMidwayAndTheDeliveryIsSentOnce`), the other channels' passes of either process having nothing to take. The hosted tests that move the clock while passes run (`MSG_SEND_007_AC13_WithFourPassesHeldByAHangingConsentSourceAnEssentialDeliveryRecordedAfterThemIsSentWithinIntervalAndOneTimeout`, `Dispatcher_ItsListeningConnectionDropped_ConnectsAgainAfterASecondAndRunsAPassOfEachChannel`) move it once, by the consent bound, or by 250 milliseconds a step until the listener is back, never by the interval while a pass is between its start and a delivery. No other test has the shape: a clock moved by `Dispatch:Interval` or more by one channel's pass while another channel's pass with work could be running.

## Items not built, and criteria proven in a later step

None.

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| M-044, the test 200 runs in a row alone | `dotnet run --project tests/Hermes.Tests -c Release --no-build -- --filter-method "*MSG_SEND_007_AC11_WithEveryEmailCall*"`, 200 times one after another on `c6c35b2`: 200 passed, none failed |
| M-044, `Hermes.Tests` 5 full runs in a row | `dotnet run --project tests/Hermes.Tests -c Release --no-build`, every test of every kind, five times one after another on `c6c35b2`: 5 runs, each 1,051 tests, 1,051 passed, none failed |

## Analyser suppressions

None.

## Resolved by rule

None.

## Open questions

None. The cause is in the test, so no engine question is asked and no release follows (M-044).

## Commit and checks

The code is `c6c35b2`'s. Every check below ran on it, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-commit-messages` (2 commits after `main`), `gate-release-compatibility`, `gate-sample-host` and `gate-test-names` (359 criteria) |
| Every test of every project, in `gate-tests` | Passed: 2,447 (1,539 unit, 819 integration, 89 contract; `Hermes.Tests` 1,051) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 354 commits, no leaks |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.
