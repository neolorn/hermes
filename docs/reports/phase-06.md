# Phase 6: standalone

## Items built

| Item | Tests |
|---|---|
| The removal of `src/Hermes.Janus`, `src/Hermes.Janus.Channels`, `tests/Hermes.Janus.Tests` and `tests/Hermes.Janus.Channels.Tests` (`8460276`), with every row naming them: `04` MSG-LIB-001's two rows of the references test, MSG-LIB-002's two rows of public types, the packages of `07` MSG-ENG-001's layout and of the repository tests' references, and the mapping of `samples/nuget.config` | `MSG_LIB_001_AC1_ProjectReferencesFollowTheRule`, `MSG_ENG_001_AC1_ProjectFoldersFollowTheLayout`, `MSG_ENG_001_AC1_SamplePackageSourcesFollowTheRule`, `MSG_ENG_001_AC2_TheSampleHostBuildsFromThePackagesPackedFromThisCommitAndItsTestsPass`, `Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone` |
| `07` MSG-ENG-011 criterion 2 as M-021 amends it, in the commit gate | `MSG_ENG_011_AC2_ShippedFileHoldingNoEntryDeletedWithTheProjectFileBesideItPasses`, `MSG_ENG_011_AC2_ShippedFileHoldingNoEntryDeletedWhileTheProjectFileStaysFails`, `MSG_ENG_011_AC2_ShippedFileHoldingAnEntryDeletedWithTheProjectFileBesideItFails`, `MSG_ENG_011_AC2_CommitDeletingAReleasedFileFails` |
| `07` MSG-ENG-011, the commit scopes without `janus` | `MSG_ENG_011_AC1_CommitMessageOutsideTheRuleFails` (the case `build(extra): ...`, a scope outside the list) |
| `07` MSG-ENG-001, the grants of `Hermes.Channels` to `Hermes` and `Hermes.Testing` alone | `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound`, `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted` |
| `04` MSG-LIB-003 criterion 4 as M-020 has it, its email and push halves | `MSG_LIB_003_AC4_AnApplicationThatDoesNotDispatchAndRegistersNoProvidersAloneStartsWithNoEmailOrPushProviderKeyOrSecret`, `MSG_LIB_003_AC4_TheProvidersAloneWithTheEmailSectionPresentDoNotStartWithoutEmailSubmission` |
| `07` MSG-ENG-009, the allow-list without the bridge's row | The rule's note no longer names a package to join it; the table the test holds never had the row |

Where a test needs a grantee, a project or a scope outside the rules, it takes a made-up name (`Hermes.Extra`, `extra`), so no test names another library.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |
| `02` MSG-CHAN-001 criterion 4, the SMS half | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-003 criterion 4, the SMS half (`smsWe`, `Sms:We:BaseAddress`) | 7 | The WE adapter |
| `06` MSG-OPS-004 criterion 1, the shipped SMS provider | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-006, a release's baseline | 8 | Unchanged from phase 5 |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds or changes: no identifier, text, default or fixture names a host product, company or application. |
| The plan's proof, that nothing names another library | `git grep -i janus` outside `docs/` finds nothing. |
| `08` section 4, `Janus:Transports`; `08` section 8, the protector `Id` `janus` | No code read the key or named the protector: the bridges' projects held no type. |
| `07` MSG-ENG-008, a test with or before the code it proves | The commit gate's three new cases, the scope case and the grant case each failed before the change that passes it; the criterion 4 tests prove behaviour phase 2 built; the removal is proven by the rules it changes. |
| `07` MSG-ENG-009, the packages added | No package is added. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## The pipeline's earlier runs

The `gates` runs 40595893496 on `36266ee` and 40597661836 on `25fa54b` failed in `gate-tests` alone, on `Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone`: M-020 had taken the bridges' rows out of `00` section 2.2 while their projects, parked by open question 1, were still in. The removal passes it here.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

This phase resolved nothing by rule.

## Open questions

1. **`07` MSG-ENG-011 criterion 2 against the plan's phase 6 (Tier 2).** The removal deleted two `PublicAPI.Shipped.txt` files holding only `#nullable enable`, which criterion 2 failed outside the release commit. Answered by M-021, reading (a): a `PublicAPI.Shipped.txt` that holds no entry, as the commit's parent holds it, may be deleted in the commit that deletes the project file beside it; the same deletion while that project file stays, and the deletion of a file holding an entry, still fail. The commit gate checks it (`bbb614e`), and the removal is built on it (`8460276`).

## Commit and checks

The report names commit `aa7c1ccbb89bfc6624b6f3b3b8af6cb4594caefa`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,136 (`Hermes.Tests` 504, `Hermes.Channels.Tests` 360, `Hermes.Repository.Tests` 199, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14) |
| Every contract test | Passed: 58 (`Hermes.Repository.Tests` 40, `Hermes.Tests` 18); the repository's contract tests read the five packages left |
| Integration tests this phase touches, run here by class | Passed: `CommitMessageGateTests`, `PackTests`, `SampleHostGateTests` |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 242 criteria |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 11 commits after `main`, the removal among them |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 135 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

The secret scanner was fetched again before the scan.