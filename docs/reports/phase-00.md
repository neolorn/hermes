# Phase 0: repository and gates

## Repository

| Setting | State |
|---|---|
| `neolorn/hermes`, private | Set |
| Default branch `main` | Set |
| Dependency vulnerability alerting | Set |
| Secret scanning | **Not set.** GitHub answers "Secret scanning is not available for this repository" (HTTP 422) for a private repository of this account. The gitleaks scan of the whole history still runs before every push and in the `gates` job. |
| Squash merges | Off |
| `main` protected | Set: the `gates` check required, administrators included, force pushes and deletion refused |

## Items built

| Item | Tests |
|---|---|
| `07` MSG-ENG-001 (all but the sample host, Janus's source and `RELEASE-NOTES.md`) | `MSG_ENG_001_AC1_ProjectsFollowTheLayout`, `MSG_ENG_001_AC1_ProjectOutsideTheLayoutIsFound`, `MSG_ENG_001_AC1_SourceProjectWithoutItsTestProjectIsFound`, `MSG_ENG_001_AC1_ProjectTheSolutionDoesNotListIsFound`, `MSG_ENG_001_AC1_ProjectFoldersFollowTheLayout`, `MSG_ENG_001_AC1_FolderOutsideTheLayoutIsFound`, `MSG_ENG_001_AC1_NamespaceThatDoesNotMatchItsFolderIsFound`, `MSG_ENG_001_AC1_PublicTypeOutsideTheProjectsRootIsFound`, `MSG_ENG_001_AC1_TwoPublicTypesInOneFileAreFound`, `MSG_ENG_001_AC1_GrantsFollowTheRule`, `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound`, `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted`, `MSG_ENG_001_AC1_ScannerConfigurationAddsOnlyTestVectorEntries`, `MSG_ENG_001_AC1_TestVectorEntryOfTheFormIsAccepted`, `MSG_ENG_001_AC1_EntryOutsideTheFormIsFound`, `MSG_ENG_001_AC1_ConfigurationThatDoesNotExtendTheDefaultAloneIsFound` |
| `07` MSG-ENG-002 | `MSG_ENG_002_AC1_NoProjectSetsATargetFrameworkAListedPropertyOrAPackageVersion`, `MSG_ENG_002_AC1_ProjectSettingWhatOnlyTheSharedFilesSetIsFound`, `MSG_ENG_002_AC1_ProjectWithoutItsLockfileIsFound`, `MSG_ENG_002_AC2_AWarningFailsTheBuild`, `MSG_ENG_002_AC2_ALockedRestoreThatDiffersFromTheLockfileFails` |
| `07` MSG-ENG-003 | No test yet: no public operation exists (see Items not built) |
| `07` MSG-ENG-004 | `MSG_ENG_004_AC1_EveryPublicClassIsSealedOrAbstract`, `MSG_ENG_004_AC1_EveryPublicAsynchronousMethodTakesACancellationToken`, `MSG_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound`, `MSG_ENG_004_AC1_PublicAsynchronousMethodWithoutATokenIsFound`, `MSG_ENG_004_AC1_PublicMemberWithoutDocumentationFailsTheBuild`, `MSG_ENG_004_AC2_NoMemberIsLookedUpByName`, `MSG_ENG_004_AC2_LookupOfMembersByNameIsFound` |
| `07` MSG-ENG-005 | `MSG_ENG_005_AC2_NoHandWrittenStatementReadsTheDatabaseClock`, `MSG_ENG_005_AC2_StatementReadingTheDatabaseClockIsFound`, `MSG_ENG_005_AC2_StatementTakingTheInstantAsAParameterIsAccepted`, `MSG_ENG_005_AC3_NoHandWrittenStatementTakesASessionLevelAdvisoryLock`, `MSG_ENG_005_AC3_SessionLevelAdvisoryLockIsFound`, `MSG_ENG_005_AC3_TransactionScopedAdvisoryLockIsAccepted` |
| `07` MSG-ENG-007 | `MSG_ENG_007_AC1_NoPublicTypeNameHoldsTheLibrarysName`, `MSG_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound` |
| `07` MSG-ENG-008 | `MSG_ENG_008_AC1_NoMockingPackageIsReferenced`, `MSG_ENG_008_AC1_MockingPackageTakenThroughAnotherIsFound`, `MSG_ENG_008_AC2_EveryListedCriterionWithItsTestPasses`, `MSG_ENG_008_AC2_ListedCriterionWithoutItsTestFails`, `MSG_ENG_008_AC2_TestOfACriterionWithAHigherNumberDoesNotCount` |
| `07` MSG-ENG-009 (all but Janus's package) | `MSG_ENG_009_AC1_PackagesAndToolsAreWithinTheTable`, `MSG_ENG_009_AC1_PackageOutsideTheTableIsFound`, `MSG_ENG_009_AC1_PackageVersionOutsideTheTableIsFound`, `MSG_ENG_009_AC1_PinnedPackageAtAnotherVersionIsFound`, `MSG_ENG_009_AC1_ToolOtherThanDotnetEfIsFound` |
| `07` MSG-ENG-010 | `MSG_ENG_010_AC1_ARestoreResolvingAPackageWithAPublishedAdvisoryFailsTheBuild` |
| `07` MSG-ENG-011 (all but the release step) | `MSG_ENG_011_AC1_CommitsWithinTheRulePass`, `MSG_ENG_011_AC1_CommitMessageOutsideTheRuleFails`, `MSG_ENG_011_AC1_BehaviourChangingCommitWithoutAChangelogLineFails`, `MSG_ENG_011_AC1_ChangelogLineOutsideUnreleasedFails`, `MSG_ENG_011_AC1_MergeCommitsAreNotChecked` |
| `07` MSG-ENG-012 (all but the migrations, release compatibility and sample host gates) | The gates as scripts under `build/gates/`, run by `run-gates.ps1` locally and by the `gates` job of `.github/workflows/gates.yml`; criteria awaiting the pipeline's run (below) |
| `04` MSG-LIB-001 (all but Janus's package) | `MSG_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows`, `MSG_LIB_001_AC1_ProjectReferenceOutsideTheTableIsFound`, `MSG_LIB_001_AC1_PackageReferenceOutsideTheTableIsFound`, `MSG_LIB_001_AC1_ShippedProjectWithoutItsBuildOnlyReferencesIsFound`, `MSG_LIB_001_AC1_CoreReferencingAspNetCoreIsFound`, `MSG_LIB_001_AC2_ChannelsReferencesNeitherEntityFrameworkCoreNorAspNetCoreNorFluid`, `MSG_LIB_001_AC2_ChannelsTakingFluidThroughAnotherPackageIsFound` |
| `04` MSG-LIB-002 (the projects and their empty surface files) | `MSG_LIB_002_AC1_AddingAPublicTypeWithoutItsPublicApiFileFailsTheBuild`, `MSG_LIB_002_AC1_RemovingAPublicTypeWithoutItsPublicApiFileFailsTheBuild`, `MSG_LIB_002_AC2_PublicApiFilesListExactlyThePublicTypes`, `MSG_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound` |
| `00` MSG-OVR-001 criterion 1 | Checked in review (below) |
| `10` MSG-THR-004 | `MSG_THR_004_AC1_ACommitCarryingACredentialShapedStringFailsTheScan`, `MSG_THR_004_AC1_ACredentialShapedStringRemovedLaterStillFailsTheScan` |

Tests that prove a test's own means rather than a criterion: `RepositoryFixture_AsMade_KeepsEveryRule`, `AssemblySurface_ShippedAssemblies_AreEachPackagesBuiltAssembly`, `SecretScan_HistoryWithoutSecrets_Passes`.

`build/gates/built-items.txt` lists 18 criteria: MSG-ENG-001 AC1, MSG-ENG-002 AC1 and AC2, MSG-ENG-004 AC1 and AC2, MSG-ENG-005 AC2 and AC3, MSG-ENG-007 AC1, MSG-ENG-008 AC1 and AC2, MSG-ENG-009 AC1, MSG-ENG-010 AC1, MSG-ENG-011 AC1, MSG-LIB-001 AC1 and AC2, MSG-LIB-002 AC1 and AC2, MSG-THR-004 AC1.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `07` MSG-ENG-001 criterion 2, the sample host and its gate | 6 | Plan, phase 0 and phase 6 |
| `07` MSG-ENG-001 Janus's source and its mapping in `nuget.config`; MSG-ENG-009 and `04` MSG-LIB-001 for Janus's package | 6 | Plan; the package's identifier and feed are an input of phase 6 |
| `RELEASE-NOTES.md` | 8 | Plan |
| `Hermes.Cli`'s project and its test project, the migrations gate of MSG-ENG-012 | 1 | Plan, with `hermes migrate` |
| The release compatibility gate of MSG-ENG-012 | 5 | Plan |
| The release step of MSG-ENG-011 (the tag and the publish) | 8 | Plan |
| `07` MSG-ENG-003 criterion 1 | 1 onward | Proven with the first public operation and the first startup code |
| `07` MSG-ENG-005 criteria 1 and 4 | 1 | They need the schema and the library's first transaction |
| `07` MSG-ENG-006 criterion 1 | 4 | The endpoints are built in phase 4 |
| `07` MSG-ENG-007 criterion 1, for codes, configuration keys, wire spellings and metric instruments | 1 onward | Proven as each phase implements the rows of `08` it uses; the type-name part is proven here |
| `04` MSG-LIB-002 criterion 2, for each type of the tables | 1 onward | Each type comes with the first item that uses it (plan, phase 0) |
| `04` MSG-LIB-001, the packages' dependencies on one another at exactly the same version | Parked | Open question 1 |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds: no identifier, text, default or fixture names a host product, company or application. The test fixtures use `example.invalid` and the names of the specification. |
| `07` MSG-ENG-001, checked in review | The fetch script holds the SHA-256 of each gitleaks 8.30.1 archive as published in `gitleaks_8.30.1_checksums.txt` of the release. The workflow uses `actions/checkout` v7.0.1 (`3d3c42e5aac5ba805825da76410c181273ba90b1`) and `actions/setup-dotnet` v6.0.0 (`a98b56852c35b8e3190ac28c8c2271da59106c68`), each the latest release on 2026-10-22, pinned by its full commit SHA, on `ubuntu-24.04`. The gates are PowerShell 7 scripts. |
| `07` MSG-ENG-002, checked in review | `.editorconfig` is the output of `dotnet new editorconfig` of SDK 10.0.402, unchanged, with the turn-offs of MSG-ENG-002 appended, each with its reason above it. |
| `07` MSG-ENG-010, alerting on | The repository's `vulnerability-alerts` endpoint answers 204. |
| `07` MSG-ENG-008 and MSG-ENG-011, checked in review | The tests of each item were written before the gate or rule they prove was run green. Each commit's description is imperative, and a body is given only where the diff does not explain itself. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2, and the pipeline half of phase 0's proof: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `10` MSG-THR-004, a finding blocking the merge: the `gates` check is the one check `main`'s protection requires, and its secret-scan gate fails on a finding.

## Analyser suppressions

The code suppresses no analyser rule. `.editorconfig` turns off exactly the rules of `07` MSG-ENG-002, each with that item's reason: CA1303 everywhere; CA1812 under `src/`, `tests/` and `samples/`; CA1034 under `src/Hermes.Core/`; CA1030 for `IAlertSink`; CA1032 for `MessagingStartupException`; CA1861 under `src/Hermes/Migrations/`; CA1707, CA1515, CS1591 and CA2007 under `tests/` and in the sample host's test project.

The probe that `AssemblyContractProbeTests` builds under `tmp/`, an assembly made to break the contract rules, is built with `-p:TreatWarningsAsErrors=false`. It is a fixture, not a project of the repository.

## Resolved by rule

| Place | Out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Hermes.Repository.Tests/Hermes.Repository.Tests.csproj` | MSG-LIB-001's table gives no row for the one test project with no source project | `07` MSG-ENG-004 criterion 1 | The repository's tests reference every shipped project, since the contract test reads their built assemblies; the reference is test infrastructure and touches no runtime code or shipped surface. |
| Each test project's `OutputType` `Exe` and `UseMicrosoftTestingPlatformRunner` `true` | xunit.v3 runs its own runner unless told to run on Microsoft.Testing.Platform | `07` MSG-ENG-008 | A test project runs xunit.v3 on Microsoft.Testing.Platform, which these two properties, neither of them listed in MSG-ENG-002, make it do. |
| `global.json` names SDK 10.0.402; `.config/dotnet-tools.json` takes `dotnet-ef` 10.0.13 | The machine's SDK was 10.0.401 (runtime 10.0.12); .NET 10.0.13 is a security release of 2026-10-13 | `07` MSG-ENG-009, MSG-ENG-010 | The SDK is the latest .NET 10 security release, and `dotnet-ef` is at the EF Core release matching that SDK's runtime. |
| The library's own project references in `src/` | MSG-LIB-001's table gives what each project may reference | `00` section 2.2; `04` MSG-LIB-001 | Each package's project references the library projects its row names, as section 2.2 gives the packages' dependencies. |
| `build/gates/gate-commit-messages.ps1` | "lower case" for a description | `07` MSG-ENG-011 | A description holds no upper-case letter. |
| `MSG_ENG_004_AC2_NoMemberIsLookedUpByName` | A lookup by name can be told only by the type it calls, which the source text does not give | `07` MSG-ENG-004 criterion 2 | The test reads the member references of each built assembly. No generated code in the packages makes such a call today, so none is exempted. |

## Open questions

1. **`04` MSG-LIB-001, "the library's packages depend on one another at exactly the same version" (Tier 2).** What the code needs: a packed package's dependency on another of the library's packages written as `[v]`. What the build gives: `dotnet pack` writes a `ProjectReference` as a minimum version (`Hermes.Channels` 0.0.0-alpha.0.3 depends on `Hermes.Core` `version="0.0.0-alpha.0.3"`, read by NuGet as that version or later), and no project property makes it exact. Readings and their smallest fixes:
   - (a) "Exactly" means a `[v]` range: one target in `Directory.Build.props`, run before pack, that rewrites the referenced projects' versions to `[v]`. It relies on NuGet's internal `_ProjectReferencesWithVersions` item, a pattern `07` does not name, so it needs a `07` and decision-log entry.
   - (b) The minimum version pack writes satisfies the item, since the packages are always released together: no change.
   
   Parked: the exact-version dependency alone. Nothing built in phase 0 depends on it; packing is first used by the sample host's gate in phase 6. Unanswered.

## Commit and gates

The report names commit `83b0a0d53911ebf4a3c8876ac43ed96306473562`. Each gate's result there, from the local run of `build/gates/run-gates.ps1 -Base origin/main -Head HEAD`:

| Gate | Result |
|---|---|
| Build and analysers, warnings as errors (`gate-build`) | Passed |
| `dotnet format --verify-no-changes` (`gate-format`) | Passed |
| Locked restore and the vulnerability audit (`gate-restore`) | Passed |
| Unit, integration and contract tests (`gate-tests`) | Passed: unit 64, integration 43, contract 31, in `Hermes.Repository.Tests`; the other test projects hold no test yet and pass with exit code 8 |
| Public surface files up to date (`gate-public-surface`) | Passed |
| Dependency allow-list (`gate-dependency-allow-list`) | Passed |
| Project references and grants (`gate-project-references`) | Passed |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 11 commits, no leaks |
| Commit message format and changelog line (`gate-commit-messages`) | Passed: 10 commits after `main` |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 18 criteria |
| Migrations, release compatibility, sample host | No script yet (phases 1, 5 and 6) |
| Log gate | No integration test of this phase logs |
