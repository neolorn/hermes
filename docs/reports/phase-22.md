# Phase 22: sender names take the SMS alphabet's basic characters, 1.0.0-rc.4

## Items built

| Item | Tests |
|---|---|
| `02` MSG-SMS-002 (`c7c26bf`): a sender name is 1 to 11 characters, each a letter, a digit, space or one of `! " # $ % & ' ( ) * + , - . / : ; < = > ? @ _`, the printable ASCII characters of the GSM 7-bit default alphabet's basic set (M-040); the numeric form, a provider narrowing the rule, and startup checking every declared sender are unchanged. The one rule serves the channels' key check, the declarations' check, `InMemorySmsProvider.IsValidSender` and the development inbox's provider | `MSG_SMS_002_AC4_ASenderWithAFullStopPassesStartupAndIsTheSenderOfItsSms`, `MSG_SMS_002_AC4_ASenderWithAFullStopPassesStartup`, `MSG_SMS_002_AC4_ASenderWithABracketPastElevenCharactersOrOutsideTheSetIsRefusedAtStartup` (`Bakery[1]`, `Bakery Co.12`, `Bakery~`, `Bakery€`), `IsSender_NameOrNumber_IsASender` and `IsSender_OutsideTheBaseRule_IsNoSender` (every punctuation character of the set; each of the extension table's, the backtick, `£`, a tab), `InMemorySmsProvider_IsValidSender_AppliesTheSenderRule`, `StartAsync_KindSenderOutsideTheBaseRule_StopsTheStart` |
| `CHANGELOG.md`: the `Fixed` line, under `## [1.0.0-rc.4]` since the release commit | `gate-commit-messages` |
| The test-name gate's list: MSG-SMS-002 AC4 (`c7c26bf`) | `gate-test-names`: 350 criteria |
| The release commit `chore: release 1.0.0-rc.4` (`208fcaf`): `CHANGELOG.md`'s section `[1.0.0-rc.4] - 2026-11-01` and `RELEASE-NOTES.md`'s section `1.0.0-rc.4`. No `PublicAPI.Unshipped.txt` holds an entry, and the release set read at it is rc.3's baseline line for line, so neither changes | `gate-commit-messages`, `gate-release-compatibility` |

## Items not built, and criteria proven in a later step

| Item or criterion | Step | Reason |
|---|---|---|
| The tag `v1.0.0-rc.4` | After the merge | The lead's |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: the test senders are `Bakery Co.`, `Shop Co.` and the like, and no value names a host product, company or application. |
| `06` MSG-OPS-007, the notes of 1.0.0-rc.4 | They name the one change, that no surface, route, code or key changes, that no migration is added, and that the deployment's list of 1.0.0-rc.3 holds. |

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **Two old test values the wider set admits.** `Orders!` was a kind's sender the declarations' check refused, and is now a sender; the test refuses `Orders[1]` in its place. `+2010000000` was refused as a number with a plus sign; it is now a name of 11 characters of the set, so it moved to the senders, and `+20100000000`, of 12, is refused in its place. A sender a provider does not take is still the provider's to refuse (`ISmsProvider.IsValidSender`, `Permanent(senderrefused)`).
2. **The notes.** A release candidate's notes name every change since the one before (`04` MSG-LIB-006), so the release commit holds `RELEASE-NOTES.md`'s section `1.0.0-rc.4`, as the gate lets it.
3. **The baseline.** The release set holds no sender rule, and the set read at the release commit is rc.3's baseline exactly, so the release commit leaves `release-baseline.json` as it is.

## Open questions

None.

## Commit and checks

The code is `c7c26bf`'s, and the release commit is `208fcaf`. Every check below ran on `208fcaf`, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every test of every project, run here | Passed: 2,343 (`Hermes.Tests` 1,048, `Hermes.Repository.Tests` 481, `Hermes.Channels.Tests` 384, `Hermes.Conversations.Tests` 217, `Hermes.Development.Tests` 114, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 24), in `gate-tests`; the sample host's in `gate-sample-host` |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-commit-messages` (3 commits after `main`), `gate-release-compatibility` and `gate-test-names` (350 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 327 commits, no leaks |

The new tests failed on the old rule before `SenderRule` changed, 9 of them, and passed after. The gate of record is the pipeline's `gates` run on the report's own commit, whose result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.
