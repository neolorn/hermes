# Phase 23: the key-table timer stops once, 1.0.0-rc.5

## Items built

| Item | Tests |
|---|---|
| `06` MSG-OPS-008, the stop writes once (`cfee913`): the first call to `KeyTableTimer.StopAsync` sets the timer's own guard by `Interlocked.Exchange`, so two calls racing write once between them, then stops the loop, waits for a tick in flight and writes as before, skipping the write where the host's time to stop is already over; every later call returns at once, with no write, no wait and no read of a service (M-041). No `ObjectDisposedException` is caught | `MSG_OPS_008_AC12_AHostStoppedTwiceTheSecondTimeAfterItsDataSourceIsDisposedWritesTheLastMadeInstantsOnceAndNeitherStopThrows`, `Timer_StoppedAgain_WritesNothingMore`, and the class's existing `Timer_EachIntervalAndTheStop_WriteTheWriteVersionsLastMadeInstant` for the first stop's write |
| `CHANGELOG.md`: the `Fixed` line under `Unreleased` (`cfee913`) | `gate-commit-messages` |
| The test-name gate's list: MSG-OPS-008 AC12 (`cfee913`) | `gate-test-names`: 351 criteria |

## Items not built, and criteria proven in a later step

| Item or criterion | Step | Reason |
|---|---|---|
| The release commit `chore: release 1.0.0-rc.5` | After the review | On the lead's message |
| The tag `v1.0.0-rc.5` | After the merge | The lead's |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no value names a host product, company or application. |

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

None.

## Open questions

None.

## Commit and checks

The code is `cfee913`'s. Every check below ran on its tree, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,345 (`Hermes.Tests` 1,050, `Hermes.Repository.Tests` 481, `Hermes.Channels.Tests` 384, `Hermes.Conversations.Tests` 217, `Hermes.Development.Tests` 114, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 24), in `gate-tests`; the sample host's 14 in `gate-sample-host` |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-release-compatibility` and `gate-test-names` (351 criteria); `gate-commit-messages` refused the commit's first message, a body fragment wrapped onto a second line, so the unpushed commit's message was amended and the gate passed on `cfee913` (2 commits after `main`) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: no leaks |

The criterion 12 test failed on the old timer with the `ObjectDisposedException` of M-041, thrown from the second stop's write, and `Timer_StoppedAgain_WritesNothingMore` failed with the second stop's instant written; both passed after the guard. The gate of record is the pipeline's `gates` run on the report's own commit, whose result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.
