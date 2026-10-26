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
| `07` MSG-ENG-001 (all but the sample host, Janus's source and `RELEASE-NOTES.md`) | `MSG_ENG_001_AC1_ProjectsFollowTheLayout`, `MSG_ENG_001_AC1_ProjectOutsideTheLayoutIsFound`, `MSG_ENG_001_AC1_SourceProjectWithoutItsTestProjectIsFound`, `MSG_ENG_001_AC1_ProjectTheSolutionDoesNotListIsFound`, `MSG_ENG_001_AC1_PackageWithoutItsProjectIsFound`, `MSG_ENG_001_AC1_CommandLinePackageIsNotYetRequired`, `MSG_ENG_001_AC1_SolutionOtherThanTheTwoIsFound`, `MSG_ENG_001_AC1_SampleSolutionIsAccepted`, `MSG_ENG_001_AC1_ProjectFoldersFollowTheLayout`, `MSG_ENG_001_AC1_FolderOutsideTheLayoutIsFound`, `MSG_ENG_001_AC1_NamespaceThatDoesNotMatchItsFolderIsFound`, `MSG_ENG_001_AC1_FileWithoutANamespaceIsFound`, `MSG_ENG_001_AC1_PublicTypeOutsideTheProjectsRootIsFound`, `MSG_ENG_001_AC1_TwoPublicTypesInOneFileAreFound`, `MSG_ENG_001_AC1_TwoPublicTypesInABlockScopedNamespaceAreFound`, `MSG_ENG_001_AC1_TypeWithItsNestedCasesIsOneTopLevelType`, `MSG_ENG_001_AC1_RootHoldsTheFilesTheLayoutLists`, `MSG_ENG_001_AC1_RootOfTheListedFilesIsAccepted`, `MSG_ENG_001_AC1_RootFileOutsideTheListIsFound`, `MSG_ENG_001_AC1_ListedRootFileMissingIsFound`, `MSG_ENG_001_AC1_PackageSourcesFollowTheRule`, `MSG_ENG_001_AC1_PackageSourcesOutsideTheRuleAreFound`, `MSG_ENG_001_AC1_PackageSourceMappingOutsideTheRuleIsFound`, `MSG_ENG_001_AC1_IgnoreFileListsThePackedPackages`, `MSG_ENG_001_AC1_IgnoreFileWithoutThePackedPackagesIsFound`, `MSG_ENG_001_AC1_GrantsFollowTheRule`, `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound`, `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted`, `MSG_ENG_001_AC1_GrantOfATestProjectIsFound`, `MSG_ENG_001_AC1_GrantOfEveryFormIsReadFromTheBuiltAssembly`, `MSG_ENG_001_AC1_ScannerConfigurationAddsOnlyTestVectorEntries`, `MSG_ENG_001_AC1_TestVectorEntryOfTheFormIsAccepted`, `MSG_ENG_001_AC1_EntryOutsideTheFormIsFound`, `MSG_ENG_001_AC1_ConfigurationThatDoesNotExtendTheDefaultAloneIsFound` |
| `07` MSG-ENG-002 | `MSG_ENG_002_AC1_ProjectsAndSharedFilesSetOnlyWhatTheRuleAllows`, `MSG_ENG_002_AC1_ProjectSettingWhatOnlyTheSharedFilesSetIsFound`, `MSG_ENG_002_AC1_ProjectPropertyThatTurnsAWarningOffIsFound`, `MSG_ENG_002_AC1_PropertyThatTurnsAWarningOffInATargetIsFound`, `MSG_ENG_002_AC1_ItemThatTurnsAWarningOffIsFound`, `MSG_ENG_002_AC1_ReferenceThatTurnsAWarningOffIsFound`, `MSG_ENG_002_AC1_SharedBuildSettingsTurningAWarningOffIsFound`, `MSG_ENG_002_AC1_SharedBuildSettingsOtherThanTheRulesAreFound`, `MSG_ENG_002_AC1_SharedBuildFileBesideTheRootsIsFound`, `MSG_ENG_002_AC1_PackageVersionsSettingAnotherPropertyIsFound`, `MSG_ENG_002_AC1_AnalyserConfigurationBesideTheRootEditorConfigIsFound`, `MSG_ENG_002_AC1_RootEditorConfigTmpAndBuildOutputAreLeftOut`, `MSG_ENG_002_AC1_RepositoryWithoutGlobalJsonIsFound`, `MSG_ENG_002_AC1_SdkOtherThanTheRulesIsFound`, `MSG_ENG_002_AC2_AWarningFailsTheBuild`, `MSG_ENG_002_AC2_AnMSBuildWarningFailsTheBuild`, `MSG_ENG_002_AC2_ALockedRestoreThatDiffersFromTheLockfileFails` |
| `07` MSG-ENG-004 | `MSG_ENG_004_AC1_EveryPublicClassIsSealedOrAbstract`, `MSG_ENG_004_AC1_EveryPublicAsynchronousMethodTakesACancellationToken`, `MSG_ENG_004_AC1_PublicClassNeitherSealedNorAbstractIsFound`, `MSG_ENG_004_AC1_PublicAsynchronousMethodWithoutATokenIsFound`, `MSG_ENG_004_AC1_PublicMemberWithoutDocumentationFailsTheBuild`, `MSG_ENG_004_AC2_NoMemberIsLookedUpByName`, `MSG_ENG_004_AC2_LookupOfMembersByNameIsFound`, `MSG_ENG_004_AC2_LookupsOfGeneratedCodeAreLeftOut` |
| `07` MSG-ENG-005 | `MSG_ENG_005_AC2_NoHandWrittenStatementReadsTheDatabaseClock`, `MSG_ENG_005_AC2_StatementReadingTheDatabaseClockIsFound`, `MSG_ENG_005_AC2_StatementTakingTheInstantAsAParameterIsAccepted`, `MSG_ENG_005_AC2_StatementInAFileUnderSrcIsFound`, `MSG_ENG_005_AC2_CommentQuotingTheDatabaseClockIsNoStatement`, `MSG_ENG_005_AC3_NoHandWrittenStatementTakesASessionLevelAdvisoryLock`, `MSG_ENG_005_AC3_SessionLevelAdvisoryLockIsFound`, `MSG_ENG_005_AC3_TransactionScopedAdvisoryLockIsAccepted`, `MSG_ENG_005_AC3_HandWrittenStatementOfAMigrationIsFound`, `MSG_ENG_005_AC3_SqlFileUnderSrcIsRead` |
| `07` MSG-ENG-007 | `MSG_ENG_007_AC1_NoPublicTypeNameHoldsTheLibrarysName`, `MSG_ENG_007_AC1_PublicTypeNamedAfterTheLibraryIsFound` |
| `07` MSG-ENG-008 | `MSG_ENG_008_AC1_NoMockingPackageIsReferenced`, `MSG_ENG_008_AC1_MockingPackageTakenThroughAnotherIsFound`, `MSG_ENG_008_AC2_EveryListedCriterionWithItsTestPasses`, `MSG_ENG_008_AC2_ListedCriterionWithoutItsTestFails`, `MSG_ENG_008_AC3_TestsOfOneKindEachRunAndPass`, `MSG_ENG_008_AC3_TestWithoutExactlyOneKindFailsTheTestsGate` |
| `07` MSG-ENG-009 (all but Janus's package) | `MSG_ENG_009_AC1_PackagesAndToolsAreWithinTheTable`, `MSG_ENG_009_AC1_PackageOutsideTheTableIsFound`, `MSG_ENG_009_AC1_PackageVersionOutsideTheTableIsFound`, `MSG_ENG_009_AC1_PinnedPackageAtAnotherVersionIsFound`, `MSG_ENG_009_AC1_PackageTakenByAReferenceOutsideTheProjectFileIsFound`, `MSG_ENG_009_AC1_ToolOtherThanDotnetEfIsFound`, `MSG_ENG_009_AC1_DotnetEfAtAVersionOtherThanEntityFrameworkCoresIsFound` |
| `07` MSG-ENG-010 | `MSG_ENG_010_AC1_ARestoreResolvingAPackageWithAPublishedAdvisoryFailsTheBuild` |
| `07` MSG-ENG-011 (all but the release step and the release commit's exemption) | `MSG_ENG_011_AC1_CommitsWithinTheRulePass`, `MSG_ENG_011_AC1_CommitMessageOutsideTheRuleFails`, `MSG_ENG_011_AC1_NameInItsOwnSpellingPasses`, `MSG_ENG_011_AC1_BehaviourChangingCommitWithoutAChangelogLineFails`, `MSG_ENG_011_AC1_ChangelogLineOutsideUnreleasedFails`, `MSG_ENG_011_AC1_ChangelogLineUnderChangedFailsAfterATagThatIsNoRelease`, `MSG_ENG_011_AC1_ChangelogLineUnderChangedPassesOnceAReleaseIsTagged`, `MSG_ENG_011_AC1_MergeCommitsThePlatformWritesAreNotChecked`, `MSG_ENG_011_AC1_LocalMergeCommitOutsideTheRuleFails`, `MSG_ENG_011_AC2_CommitChangingAReleasedFileFails`, `MSG_ENG_011_AC2_CommitDeletingAReleasedFileFails`, `MSG_ENG_011_AC2_NewProjectsPublicApiFileAndAFileMovedUnchangedPass` |
| `07` MSG-ENG-012 (all but the migrations, release compatibility and sample host gates) | The gates as scripts under `build/gates/`, run by `run-gates.ps1` locally and by the `gates` job of `.github/workflows/gates.yml`; criteria awaiting the pipeline's run (below) |
| `04` MSG-LIB-001 (all but Janus's package and the sample host's row) | `MSG_LIB_001_AC1_EachProjectReferencesOnlyWhatItsRowAllows`, `MSG_LIB_001_AC1_ProjectReferenceOutsideTheTableIsFound`, `MSG_LIB_001_AC1_PackageReferenceOutsideTheTableIsFound`, `MSG_LIB_001_AC1_PackageReferenceInTheSharedBuildSettingsIsFound`, `MSG_LIB_001_AC1_GlobalPackageReferenceIsFound`, `MSG_LIB_001_AC1_ShippedProjectWithoutItsBuildOnlyReferencesIsFound`, `MSG_LIB_001_AC1_CoreReferencingAspNetCoreIsFound`, `MSG_LIB_001_AC1_AspNetCoreWithoutNeedIsFound`, `MSG_LIB_001_AC1_FrameworkOtherThanAspNetCoreIsFound`, `MSG_LIB_001_AC1_ProjectReachingAspNetCoreWithoutTheFrameworkIsFound`, `MSG_LIB_001_AC2_ChannelsReferencesNeitherEntityFrameworkCoreNorAspNetCoreNorFluid`, `MSG_LIB_001_AC2_ChannelsTakingFluidThroughAnotherPackageIsFound`, `MSG_LIB_001_AC2_ChannelsReachingTheEngineThroughAProjectIsFound`, `MSG_LIB_001_AC2_ChannelsReachingAspNetCoreThroughAProjectIsFound`, `MSG_LIB_001_AC3_EachPackageDependsOnTheLibrarysOtherPackagesAtTheExactVersionPacked`, `MSG_LIB_001_AC3_DependencyOnALibraryPackageNotAtTheExactVersionPackedIsFound`, `MSG_LIB_001_AC3_DependencyAtTheExactVersionPackedIsAccepted` |
| `04` MSG-LIB-002 (the projects and their empty surface files) | `MSG_LIB_002_AC1_AddingAPublicTypeWithoutItsPublicApiFileFailsTheBuild`, `MSG_LIB_002_AC1_RemovingAPublicTypeWithoutItsPublicApiFileFailsTheBuild`, `MSG_LIB_002_AC1_PublicSurfaceGateFailsOnAPublicTypeNoFileLists`, `MSG_LIB_002_AC2_EveryPublicTypeIsOneTheTablesGive`, `MSG_LIB_002_AC2_PublicTypeNoTableGivesIsFound`, `MSG_LIB_002_AC2_PublicApiFilesListExactlyThePublicTypes`, `MSG_LIB_002_AC2_PublicTypeTheFilesDoNotListIsFound` |
| `00` MSG-OVR-001 criterion 1 | Checked in review (below) |
| `10` MSG-THR-004 | `MSG_THR_004_AC1_ACommitCarryingACredentialShapedStringFailsTheScan`, `MSG_THR_004_AC1_ACredentialShapedStringRemovedLaterStillFailsTheScan`, `MSG_THR_004_AC2_ALineCarryingTheInlineAllowMarkerFailsTheScan`, `MSG_THR_004_AC2_ATrackedGitleaksIgnoreFailsTheScanWhateverItLists`, `MSG_THR_004_AC2_AGitleaksIgnoreAtTheScannedRootFailsTheScanWhateverItLists`, `MSG_THR_004_AC2_AFindingAGitleaksIgnoreOfTheWorkingDirectoryListsFailsTheScan` |

Tests of a rule that no criterion carries: `SettingViolations_ProjectWithoutItsLockfile_IsFound` (`07` MSG-ENG-002's values); `Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone`, `ManifestViolations_PackageAsTheRuleGivesIt_HasNone`, `ManifestViolations_AnotherPackageProperty_IsFound`, `ManifestViolations_PropertyNotAsTheRuleGivesIt_IsFound` (MSG-ENG-011's releases); `FetchSecretScanner_ArchiveOtherThanThePublished_Fails`, `FetchSecretScanner_ChangedScanner_IsReplacedByTheArchives` (MSG-ENG-001's pinned scanner); `PreviousReleaseTag_FirstRelease_IsNone`, `PreviousReleaseTag_TagsOfOtherShapes_AreLeftOut`, `RunGates_WorkingTreeOfHead_RunsEachGate`, `RunGates_FailingGate_FailsTheRun`, `RunGates_HeadOtherThanTheCheckedOutCommit_Refuses`, `RunGates_WorkingTreeOtherThanHeads_Refuses`, `RunGates_UntrackedFileOfAnExcludedPath_Runs`, `RunGates_NoGateScript_Fails`, `TestNameGate_NoCriterionListed_Fails`, `TestsGate_NoTestProject_Fails`, `PublicSurfaceGate_NoProjectUnderSrc_Fails` (MSG-ENG-012's scripts).

Tests that prove a test's own means rather than a criterion: `RepositoryFixture_AsMade_KeepsEveryRule`, `AssemblySurface_ShippedAssemblies_AreEachPackagesBuiltAssembly`, `SecretScan_HistoryWithoutSecrets_Passes`, `CSharpSource_StringLiteral_IsReadWhole`, `CSharpSource_Comment_IsBlankedAndHoldsNoLiteral`.

`build/gates/built-items.txt` lists 22 criteria: MSG-ENG-001 AC1, MSG-ENG-002 AC1 and AC2, MSG-ENG-004 AC1 and AC2, MSG-ENG-005 AC2 and AC3, MSG-ENG-007 AC1, MSG-ENG-008 AC1, AC2 and AC3, MSG-ENG-009 AC1, MSG-ENG-010 AC1, MSG-ENG-011 AC1 and AC2, MSG-LIB-001 AC1, AC2 and AC3, MSG-LIB-002 AC1 and AC2, MSG-THR-004 AC1 and AC2.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `07` MSG-ENG-001 criterion 2, the sample host and its gate | 6 | Plan, phase 0 and phase 6 |
| `07` MSG-ENG-001 Janus's source and its mapping in `nuget.config`; MSG-ENG-009 and `04` MSG-LIB-001 for Janus's package | 6 | Plan; the package's identifier and feed are an input of phase 6 |
| `04` MSG-LIB-001, the sample host's row | 6 | Plan, with the sample host |
| `04` MSG-LIB-001, `Hermes`'s build-only `Microsoft.EntityFrameworkCore.Design` reference | 1 | Plan; it makes the migrations of phase 1. The rule that a shipped project carries its build-only references is built and tested here |
| `RELEASE-NOTES.md` | 8 | Plan |
| `Hermes.Cli`'s project and its test project, the migrations gate of MSG-ENG-012 | 1 | Plan, with `hermes migrate` |
| The release compatibility gate of MSG-ENG-012 | 5 | Plan |
| The release step of MSG-ENG-011 (the tag and the publish) and the release commit's exemption of MSG-ENG-011 criterion 2 | 8 | Plan |
| `07` MSG-ENG-003 | 1 onward | No public operation and no startup code exist yet; its criterion is proven with the first of each |
| `07` MSG-ENG-005 criteria 1 and 4 | 1 | They need the schema and the library's first transaction |
| `07` MSG-ENG-006 criterion 1 | 4 | The endpoints are built in phase 4 |
| `07` MSG-ENG-007 criterion 1, for codes, configuration keys, wire spellings and metric instruments | 1 onward | Proven as each phase implements the rows of `08` it uses; the type-name part is proven here |
| `04` MSG-LIB-002, each type of the tables | 1 onward | Each type comes with the first item that uses it (plan, phase 0) |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds: no identifier, text, default or fixture names a host product, company or application. The test fixtures use `example.invalid` and the names of the specification. |
| `07` MSG-ENG-001, checked in review | The fetch script holds the SHA-256 of each gitleaks 8.30.1 archive as published in `gitleaks_8.30.1_checksums.txt` of the release. The workflow uses `actions/checkout` v7.0.1 (`3d3c42e5aac5ba805825da76410c181273ba90b1`) and `actions/setup-dotnet` v6.0.0 (`a98b56852c35b8e3190ac28c8c2271da59106c68`), each the latest release on 2026-10-22, pinned by its full commit SHA, on `ubuntu-24.04`. The gates are PowerShell 7 scripts. |
| `07` MSG-ENG-002, checked in review | `.editorconfig` is the output of `dotnet new editorconfig` of SDK 10.0.402, unchanged, with the turn-offs of MSG-ENG-002 appended, each with its reason above it. |
| `07` MSG-ENG-010, alerting on | The repository's `vulnerability-alerts` endpoint answers 204. |
| `07` MSG-ENG-008, a test with or before the code it proves | `0aeec5b` brought the gates' scripts before their tests (`3715163`, `e1cd35e`). It is pushed, and it stays as it is. Every commit after `5396fe8` carries its tests with the code they prove. |
| `07` MSG-ENG-011, checked in review | Each commit's description is imperative. The bodies of `0aeec5b`, `dcf2a77` and `e1cd35e` describe the code, not the reasons for it. They are pushed, and they stay as they are. Every other body gives reasons only, and only where the diff does not explain itself. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2, and the pipeline half of phase 0's proof: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `10` MSG-THR-004, a finding blocking the merge: the `gates` check is the one check `main`'s protection requires, and its secret-scan gate fails on a finding.

## Analyser suppressions

The code suppresses no analyser rule. `.editorconfig` turns off exactly the rules of `07` MSG-ENG-002, each with that item's reason: CA1303 everywhere; CA1812 under `src/`, `tests/` and `samples/`; CA1034 under `src/Hermes.Core/`; CA1030 for `IAlertSink`; CA1032 for `MessagingStartupException`; CA1861 under `src/Hermes/Migrations/`; CA1707, CA1515, CS1591 and CA2007 under `tests/` and in the sample host's test project.

The probes that `AssemblyContractProbeTests` builds under `tmp/`, assemblies made to break the contract rules, are built with `-p:TreatWarningsAsErrors=false -p:MSBuildTreatWarningsAsErrors=false`. They are fixtures, not projects of the repository.

## Resolved by rule

| Place | Out of step | Governing item | Rule applied |
|---|---|---|---|
| `tests/Hermes.Repository.Tests/Hermes.Repository.Tests.csproj` | MSG-LIB-001's table gave no row for the one test project with no source project | `07` MSG-ENG-004 criterion 1; since M-015, `04` MSG-LIB-001 | The repository's tests reference every shipped project, since the contract test reads their built assemblies. M-015 adds this row to `04` MSG-LIB-001's table as built, so `04` now states it. |
| Each test project's `OutputType` `Exe` and `UseMicrosoftTestingPlatformRunner` `true` | xunit.v3 runs its own runner unless told to run on Microsoft.Testing.Platform | `07` MSG-ENG-008 | A test project runs xunit.v3 on Microsoft.Testing.Platform, which these two properties, neither of them listed in MSG-ENG-002, make it do. |
| `global.json` names SDK 10.0.402; `.config/dotnet-tools.json` takes `dotnet-ef` 10.0.13 | The machine's SDK was 10.0.401 (runtime 10.0.12); .NET 10.0.13 is a security release of 2026-10-13 | `07` MSG-ENG-009, MSG-ENG-010 | The SDK is the latest .NET 10 security release, and `dotnet-ef` is at the EF Core release matching that SDK's runtime. |
| The library's own project references in `src/` | MSG-LIB-001's table gives what each project may reference | `00` section 2.2; `04` MSG-LIB-001 | Each package's project references the library projects its row names, as section 2.2 gives the packages' dependencies. |
| `MSG_ENG_004_AC2_NoMemberIsLookedUpByName` | A lookup by name can be told only by the type it calls, which the source text does not give | `07` MSG-ENG-004 criterion 2 | The test reads, from the IL of each method body of each built assembly, the members the body references, leaves out the methods of a type marked `GeneratedCodeAttribute` or nested in one, and flags `Delegate.CreateDelegate` by name. |
| The refusals of `run-gates.ps1`, `gate-test-names.ps1`, `gate-tests.ps1`, `gate-public-surface.ps1` and `fetch-secret-scanner.ps1` | Each gave its reason only in its thrown error, which PowerShell wraps at the console's width: 120 columns locally, 80 on the runner. Two tests that read the reason passed locally and failed in the pipeline's run 40271135668 | `07` MSG-ENG-012 criterion 2 | A gate writes its reason on a line of its own, so the gate gives the same result locally and in the pipeline. |

## Open questions

1. **`04` MSG-LIB-001, "the library's packages depend on one another at exactly the same version" (Tier 2).** What the code needed: a packed package's dependency on another of the library's packages written as `[v]`. What the build gave: `dotnet pack` writes a `ProjectReference` as a minimum version, and no project property makes it exact. Readings: (a) "exactly" means a `[v]` range, set by one target in `Directory.Build.props` on NuGet's internal `_ProjectReferencesWithVersions` item; (b) the minimum version pack writes satisfies the item. Parked: the exact-version dependency. **Answered by M-015: reading (a).** `07` MSG-ENG-011's releases give the target, and `04` MSG-LIB-001 criterion 3 its test. Built: `Directory.Build.props`, and the tests of MSG-LIB-001 AC3.
2. **`07` MSG-ENG-011, a description "in lower case" (recorded at `5396fe8` as Resolved by rule).** Readings: (a) a description holds no upper-case letter; (b) it begins with a lower-case letter, and a name keeps its own spelling. The gate applied (a). A second reading was plausible, so it was no Tier 1. **Answered by M-015: reading (b).** Built: `gate-commit-messages.ps1`, and `MSG_ENG_011_AC1_NameInItsOwnSpellingPasses`.

## Commit and gates

The report names commit `74a5278195dd897f2b183d322a0729a4ddaf1a48`. Each gate's result there, from the local run of `build/gates/run-gates.ps1 -Base origin/main -Head 74a5278195dd897f2b183d322a0729a4ddaf1a48` on a clean working tree:

| Gate | Result |
|---|---|
| Build and analysers, warnings as errors (`gate-build`) | Passed |
| `dotnet format --verify-no-changes` (`gate-format`) | Passed |
| Locked restore and the vulnerability audit (`gate-restore`) | Passed |
| Unit, integration and contract tests (`gate-tests`) | Passed: unit 178, integration 90, contract 38, in `Hermes.Repository.Tests`; the other test projects hold no test yet |
| Public surface files up to date (`gate-public-surface`) | Passed |
| Dependency allow-list (`gate-dependency-allow-list`) | Passed |
| Project references and grants (`gate-project-references`) | Passed |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 26 commits, no leaks |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 25 commits after `main` |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 22 criteria |
| Migrations, release compatibility, sample host | No script yet (phases 1, 5 and 6) |
| Log gate | No integration test of this phase logs |

The pipeline's `gates` runs on the commits pushed: on `5396fe866091a65e6442332bb410727bed70538f`, run 39683705731 (https://github.com/neolorn/hermes/actions/runs/39683705731), passed; on `d2c4d896f34b7db199891ae969750ffa55e399c3`, the last commit pushed, run 40271135668 (https://github.com/neolorn/hermes/actions/runs/40271135668), failed: `gate-tests` failed on `RunGates_HeadOtherThanTheCheckedOutCommit_Refuses` and `TestNameGate_NoCriterionListed_Fails`, every other gate passed. Resolved by rule gives the cause and the fix, `74a5278`. The run on the commit pushed with this report is given in the message naming the pull request.
