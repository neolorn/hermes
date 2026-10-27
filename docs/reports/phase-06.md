# Phase 6: standalone

## Items built

| Item | Tests |
|---|---|
| `07` MSG-ENG-011, the commit scopes without `janus` | `MSG_ENG_011_AC1_CommitMessageOutsideTheRuleFails` (the case `build(janus): ...`) |
| `07` MSG-ENG-001, the grants of `Hermes.Channels` to `Hermes` and `Hermes.Testing` alone | `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound` (the grant to the channels bridge, now outside the rule), `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted` |
| `04` MSG-LIB-003 criterion 4 as M-020 has it, its email and push halves | `MSG_LIB_003_AC4_AnApplicationThatDoesNotDispatchAndRegistersNoProvidersAloneStartsWithNoEmailOrPushProviderKeyOrSecret`, `MSG_LIB_003_AC4_TheProvidersAloneWithTheEmailSectionPresentDoNotStartWithoutEmailSubmission` |
| `07` MSG-ENG-009, the allow-list without the bridge's row | The rule's note no longer names a package to join it; the table the test holds never had the row |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The removal of `src/Hermes.Janus`, `src/Hermes.Janus.Channels`, `tests/Hermes.Janus.Tests` and `tests/Hermes.Janus.Channels.Tests`, with the rows naming them: `04` MSG-LIB-001's two rows and the public types of MSG-LIB-002, the packages of `07` MSG-ENG-001's layout, the mapping of `samples/nuget.config` | Parked | Open question 1 |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |
| `02` MSG-CHAN-001 criterion 4, the SMS half | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-003 criterion 4, the SMS half (`smsWe`, `Sms:We:BaseAddress`) | 7 | The WE adapter |
| `06` MSG-OPS-004 criterion 1, the shipped SMS provider | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-006, a release's baseline | 8 | Unchanged from phase 5 |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds or changes: no identifier, text, default or fixture names a host product, company or application. |
| `08` section 4, `Janus:Transports`; `08` section 8, the protector `Id` `janus` | No code reads the key or names the protector: the bridges' projects held no type. |
| `07` MSG-ENG-008, a test with or before the code it proves | The scope and grant rules each failed their new case before the change that passes it; the criterion 4 tests prove behaviour phase 2 built. |
| `07` MSG-ENG-009, the packages added | No package is added. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## The pipeline's run

The `gates` run 40595893496 on `36266ee` failed in `gate-tests`, every other gate passing: `PackTests.Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone` (`07` MSG-ENG-011) finds that the descriptions of `Hermes.Janus` and `Hermes.Janus.Channels` are no cell of `00` section 2.2, since M-020 took their rows out of the table. The projects' removal, parked by open question 1, is what passes it; a description outside `00`, or a test that skips the two packages, would weaken the rule. The run on this phase fails the same way until that answer comes.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

This phase resolved nothing by rule.

## Open questions

1. **`07` MSG-ENG-011 criterion 2 against the plan's phase 6 (Tier 2).**
   - Item: phase 6 takes the two bridge projects out (M-020).
   - What the code needs: deleting `src/Hermes.Janus/PublicAPI.Shipped.txt` and `src/Hermes.Janus.Channels/PublicAPI.Shipped.txt` with their projects. Each holds only `#nullable enable`, and no release tag exists.
   - What the specification says: `07` MSG-ENG-011 criterion 2 fails a commit that deletes an existing `PublicAPI.Shipped.txt`, save the release commit, whose exemption comes with the release step (phase 8). Run on the removal, `gate-commit-messages` failed it twice, once for each file, so we took the commit back before any push.
   - Readings: (a) the criterion governs only a file that holds released API, so a file holding no entry may go with its project; (b) the criterion holds as written, and the projects come out in the release commit of phase 8; (c) the criterion holds as written until the first release, and the projects wait for an amendment that names this removal.
   - Smallest fix for (a): criterion 2 and its value in `07` add "a `PublicAPI.Shipped.txt` holding no entry, deleted with its project, is neither", and `gate-commit-messages.ps1` passes such a deletion.
   - Smallest fix for (b): the plan moves the removal to phase 8's release commit.
   - Smallest fix for (c): an M-entry that names the two files as exempt for this one removal, which the gate cannot express without a list of exempt paths.

   Parked by it: the removal of the four projects and every row naming them, and with it the pipeline's green run (see above). Nothing else depends on it.

## Commit and checks

The report names commit `9af8a69c726568f6e3dd605495141466b2b0539d`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,138 (`Hermes.Tests` 504, `Hermes.Channels.Tests` 360, `Hermes.Repository.Tests` 201, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14); the Janus test projects hold no test |
| Every contract test | Passed: 72 (`Hermes.Repository.Tests` 54, `Hermes.Tests` 18) |
| Integration tests this phase touches, run here by class | Passed: `CommitMessageGateTests` |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 5 commits after `main` |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 129 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

The secret scanner was fetched again before the scan.