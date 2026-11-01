# Phase 13: foundations of conversations

## Items built

| Item | Tests |
|---|---|
| `07` MSG-ENG-011 (`e4fd67c`): the scope `conversations`, allowed by the commit-message gate | `MSG_ENG_011_AC1_CommitScopedConversationsPasses` (`feat(conversations)` and `test(conversations)`) |
| `00` section 2.2; `04` MSG-LIB-001, MSG-LIB-002, MSG-LIB-003; `06` MSG-OPS-001; `07` MSG-ENG-001, MSG-ENG-009; `08` sections 1 and 4 (`1904162`): the project `src/Hermes.Conversations`, its description the `00` section 2.2 cell, referencing `Hermes.Core`, `Hermes` and ASP.NET Core's shared framework, with its five folders to come (`Chats`, `Support`, `Attachments`, `Live`, `Endpoints`), granting its internals to its test project and `Hermes.Testing`; the engine's grant to it and its folder `Conversations`; its test project `tests/Hermes.Conversations.Tests`; `Hermes.Testing` and `tests/Hermes.Tests` allowed to reference it; `Microsoft.AspNetCore.SignalR.Client` allowed in test projects alone; `MessagingConversationsServiceCollectionExtensions.AddHermesConversations(IServiceCollection)`, answering the `IServiceCollection`, which throws `InvalidOperationException` naming `AddHermes` at the call without it, and registers one check at start, however often it is called; the check: a key at or under `Hermes:Conversations` that the package does not know stops the start with `messaging.startup.configurationinvalid`, `details.key` the key as `08` section 4 spells keys, the first in ordinal order | `MSG_LIB_003_AC7_AHostCallingAddHermesConversationsOnceAfterAddHermesStarts`, `MSG_LIB_003_AC7_AddHermesConversationsWithoutAddHermesThrowsAtTheCallNamingIt`, `AddHermesConversations_CalledTwice_StartsAndChecksOnce`, `AddHermesConversations_NoServices_ThrowsArgumentNull`, `MSG_OPS_001_AC4_AKeyUnderConversationsThePackageDoesNotKnowStopsTheStartNamingIt` (a key under the section, an item of a list under it, the section itself), `AddHermes_AKeyUnderConversationsWithoutThePackage_StartsSinceNothingReadsIt`, `ConversationsConfigurationTests` (4); `MSG_LIB_001_AC1_ConversationsReferencingAProjectOutsideItsRowIsFound`, `MSG_LIB_001_AC1_ConversationsReferencingTheCoreAndTheEngineIsAccepted`, `MSG_LIB_001_AC1_TheTestingPackageReferencingConversationsIsAccepted`, `MSG_LIB_001_AC1_TheEngineTestsReferencingConversationsAreAccepted`, `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound` and `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted` (the package's grants and the engine's to it), `MSG_ENG_001_AC1_PackageWithoutItsProjectIsFound` for the package, `MSG_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive` and `MSG_LIB_002_AC2_PublicApiFilesListExactlyThePublicTypes` for `Hermes.Conversations.dll`, `Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone`; `gate-project-references`, `gate-public-surface`, `gate-dependency-allow-list` |
| `07` MSG-ENG-001 (`64a0d23`): the sample host references `Hermes.Conversations`, from the packages packed from the commit, and calls `AddHermesConversations` after `AddHermes` | `MSG_ENG_001_AC3_TheSampleHostReferencesConversationsAndRegistersThemAfterTheEngine`; the sample host's own tests, in `gate-sample-host` (`MSG_ENG_001_AC2_TheSampleHostBuildsFromThePackagesPackedFromThisCommitAndItsTestsPass`), among them `Start_AKeyUnderConversationsTheyDoNotKnow_StopsTheStartSinceTheBakeryRegistersThem`, and each of its other tests, which start it |
| `06` MSG-OPS-002 and MSG-OPS-008, `01` MSG-SEND-020, the standing rule of M-031 (`e60404c`, its tests named in `b03c74e`): a table of the schema `messaging` joins key retirement, its re-protection and erasure through an internal description, `KeyedTable` (its person column, protected values with their `contentKeys` version column, its `hashKeys` version column, and whether an erasure deletes or empties a person's rows), which the engine's services take as `IEnumerable<KeyedTable>`; none is registered, so nothing changes until phase 14's first table registers itself | `MSG_OPS_002_AC6_ATableDefinedInTheTestsIsProtectedAgainAndItsOldVersionRetiredOnlyThen`, `MSG_OPS_002_AC6_AContentKeysVersionARowOfATableDefinedInTheTestsUsesIsNotRetired`, `MSG_OPS_002_AC6_AHashKeysVersionARowOfATableDefinedInTheTestsRecordsIsNotRetired`, `Erase_APersonsRowsOfKeyedTables_AreDeletedOrEmptiedAsEachTableSaysAndOtherPeoplesRowsStay`, `Erase_WhileAWriterHoldsThePersonsLocksShared_WaitsForItAndErasesTheRowItWrote`, `KeyedTableTests` (9); `KeyRetirementTests`, `SubjectTests`, `RetentionTests` and `InAppSubjectTests` unchanged and passing |
| `CHANGELOG.md` under `## [Unreleased]`, `### Added`: the package and the sample host, each in its commit | `gate-commit-messages` |
| The test-name gate's list: MSG-LIB-003 AC7, MSG-OPS-001 AC4 (`1904162`), MSG-ENG-001 AC3 (`64a0d23`), MSG-OPS-002 AC6 (`b03c74e`) | `gate-test-names`: 293 criteria |
| The chapters of `phase-13-docs` (`afb8bf3`), merged in `64f86c6` | The phase's reference from the merge on |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| A reference to `Hermes.Conversations` from `Hermes.Testing` and from `tests/Hermes.Tests` | The phases that need them | Allowed now (`04` MSG-LIB-001); neither has anything of the package to use yet, the endpoint contract test's reference coming with the first endpoint or hub |
| A project's reference to `Microsoft.AspNetCore.SignalR.Client`, and its `PackageVersion` | 15 | Allowed now in test projects (`07` MSG-ENG-009); the first hub's tests take it |
| `07` MSG-ENG-006's hubs | From 15 | No hub exists yet |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no identifier, text, default or fixture names a host product, company or application. |
| `04` MSG-LIB-006, release 2's additions | The phase adds one public type and its one method, in `PublicAPI.Unshipped.txt` of the new package; no file of `1.0.0-rc.2`'s surface changes. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **Where `AddHermes` is found.** The call looks for the engine's configuration state among the services, which `AddHermes` alone registers, so `AddHermes` must come before it in the same collection (`04` MSG-LIB-003: "called without `AddHermes` before it, the call itself throws").
2. **The check's key.** The package knows no key yet, so every key at or under `Hermes:Conversations` is one it does not know, the section itself holding a value among them; `details.key` spells it from the section `Hermes`, as `08` section 4 and the engine's checks do (`Conversations:Unknown`), and of several the first in ordinal order is named, so a start always names the same one.
3. **A section's empty value.** A web host's test factory hands its settings to the host as a command line, which gives each parent of a key an empty value, so `Hermes:Conversations` read as holding `""` beside `Hermes:Conversations:Unknown`; a section with keys beneath it and an empty value holds no key of its own, while an empty value with nothing beneath it is a key. The sample host's test found it; `ConversationsConfigurationTests` proves both.
4. **The engine's keys.** The unknown-key rule is `Hermes:Conversations:`'s alone (`06` MSG-OPS-001); the engine's check is unchanged, and a host without the package that sets a key there starts, since nothing reads it.
5. **Called twice.** The second call registers nothing more, so the check runs once.
6. **The hooks' form.** One description, `KeyedTable`, per table, its identifiers checked as lower-case SQL identifiers since they are written into statements; key retirement adds a `NOT EXISTS` over each table's version column to its conditions, re-protection reads each table's values in batches under `FOR UPDATE SKIP LOCKED` and writes them back only where unchanged, as the deliveries' do, and an erasure deletes or empties a person's rows after the engine's own tables, under the person's locks it already holds.
7. **The repository tests' reference.** `tests/Hermes.Repository.Tests` references the package, since its contract tests read every shipped assembly from their own output (`04` MSG-LIB-001: "Every shipped project, whose built assemblies its contract tests read").
8. **The sample's source mapping.** `samples/nuget.config` maps `Hermes.Conversations` to the packed packages, as it maps each of the library's packages (`07` MSG-ENG-001), and the repository fixture's copy follows.
9. **The merge of the chapters.** The commit-message gate checks every commit but the platform's merges, so the local merge of `phase-13-docs` carries a header of its own, `docs: merge the chapters for phase 13`.
10. **Commit scopes.** The package's commit is scoped `conversations`, after the scope's own commit.

## Open questions

1. **The engine's lists under a test factory.** As in rule 3, a host whose tests set `Hermes:Languages:0` through a web host's test factory would give `Hermes:Languages` an empty value, which the engine's check of `Languages` (`06` MSG-OPS-001) reads as a value and refuses. No test of this repository sets a list that way, and nothing in this phase depends on it; should the engine's check read a section's empty value as the package's does? It parks nothing.

## Commit and checks

The report names `b03c74e`, on which every check below ran, the working tree holding nothing but this report's own edit:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every test of every project, run here | Passed: 2,084 (`Hermes.Tests` 1,027, `Hermes.Repository.Tests` 479, `Hermes.Channels.Tests` 362, `Hermes.Development.Tests` 113, `Hermes.Core.Tests` 44, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 19, `Hermes.Conversations.Tests` 12), in `gate-tests` |
| Every gate (`run-gates.ps1 -Base origin/main`) | Passed: all 13, among them `gate-commit-messages` (9 commits after `main`), `gate-release-compatibility` (no final release precedes Head, so the baseline is empty and nothing is a break), `gate-migrations`, `gate-sample-host` (the sample host's 9 tests) and `gate-test-names` (293 criteria) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 228 commits, no leaks |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push. The secret scanner is the archive kept in `tmp/tools/`, checked against its published SHA-256 before each scan.
