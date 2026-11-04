# Phase 24: the second stop waits for the first stop's write, 1.0.0-rc.6

## Items built

| Item | Tests |
|---|---|
| `06` MSG-OPS-008, a later stop waits for the first stop's write (`b680ba2`): the timer keeps the first stop's task in a `TaskCompletionSource<Task>` set by `Interlocked.CompareExchange`, so two stops racing write once between them; every later stop writes nothing, reads no service and returns that task, whatever its own cancellation token says (M-042). The first stop's work, its skip when the host's time to stop is over, its wait for a tick in flight and its warnings on a failed write are unchanged | `MSG_OPS_008_AC13_ASecondStopCalledWhileTheFirstStopsWriteRunsReturnsOnlyOnceThatWriteHasEndedAndTheInstantsAreWrittenOnce` (another session holds the key table's rows, so the first stop's write waits; the second stop, its token already cancelled, has not returned until they are released, and the last-made instant is the first stop's), with criterion 12's `MSG_OPS_008_AC12_AHostStoppedTwiceTheSecondTimeAfterItsDataSourceIsDisposedWritesTheLastMadeInstantsOnceAndNeitherStopThrows` and `Timer_StoppedAgain_WritesNothingMore` still passing |
| `CHANGELOG.md`: the `Fixed` line, under `## [1.0.0-rc.6]` since the release commit | `gate-commit-messages` |
| The test-name gate's list: MSG-OPS-008 AC13 (`b680ba2`) | `gate-test-names`: 352 criteria |
| The release commit `chore: release 1.0.0-rc.6` (`76523c1`): `CHANGELOG.md`'s section `[1.0.0-rc.6] - 2026-11-04`, the commit's date in UTC, and `RELEASE-NOTES.md`'s section `1.0.0-rc.6`. No `PublicAPI.Unshipped.txt` holds an entry, and the release set read at it is rc.5's baseline, so neither changes | `gate-commit-messages`, `gate-release-compatibility` |

## Items not built, and criteria proven in a later step

| Item or criterion | Step | Reason |
|---|---|---|
| The tag `v1.0.0-rc.6` | After the merge | The lead's |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no value names a host product, company or application. |
| `06` MSG-OPS-007, the notes of 1.0.0-rc.6 | They name the one change, that no surface, route, code or key changes, that no migration is added, and that the deployment's list of 1.0.0-rc.3 holds. |

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

None.

## Open questions

1. **The first docs commit's first line.** The commit-message gate refused `8d13f3f` on the first branch, `phase-24`, its first line being 76 characters, over MSG-ENG-011's 72. We parked the push. The lead cut `phase-24b` from `main` with `335035c`, the same documents under a 52-character first line, and our two commits, never pushed, were moved onto it.

## Commit and checks

The code is `b680ba2`'s, and the release commit is `76523c1`. Every check below ran on `76523c1`, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every test of every project, run here | Passed: 2,346 (`Hermes.Tests` 1,051, `Hermes.Repository.Tests` 481, `Hermes.Channels.Tests` 384, `Hermes.Conversations.Tests` 217, `Hermes.Development.Tests` 114, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 24), in `gate-tests`; the sample host's in `gate-sample-host` |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-commit-messages` (3 commits after `main`), `gate-release-compatibility` and `gate-test-names` (352 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 339 commits, no leaks |

The criterion 13 test failed on the timer of 1.0.0-rc.5, its second stop returning while the first stop's write still waited, and passed after the change, three runs in a row. Before the move, the release commit's first heading carried the local date, 2026-11-05, and the gate refused it; the heading carries the commit's date in UTC. The gate of record is the pipeline's `gates` run on the report's own commit, whose result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.
