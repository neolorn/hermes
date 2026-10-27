# Phase 5a: the sample host and the endpoint contract

## Items built

| Item | Tests |
|---|---|
| `07` MSG-ENG-001 criterion 2, the sample host (`samples/Bakery`, its tests in `samples/Bakery.Tests`, both in `samples/Samples.sln`), and its gate of MSG-ENG-012 (`gate-sample-host.ps1`) | `MSG_ENG_001_AC2_TheSampleHostBuildsFromThePackagesPackedFromThisCommitAndItsTestsPass`, `GateSampleHost_RepositoryWithoutTheSampleSolution_Fails`; the sample host's own tests, run by the gate: `PlaceOrder_SignedInCustomer_IsToldByEmailAndSms`, `PlaceOrder_NoOneSignedIn_Is401AndSendsNothing`, `SendOffer_ACustomer_Is403`, `SendOffer_ToACustomerWithAPushSubscriptionAndOneWhoTurnedItsEmailOff_GoesByEmailToTheOneAndByPushToTheOther`, `SendOffer_AfterTheCustomerUnsubscribedThroughTheEmailsLink_SendsThemNoMoreEmail`, `SmsReport_AWrongKey_Is404` |
| `07` MSG-ENG-001 criterion 1, the sample host's layout, solution and package sources | `MSG_ENG_001_AC1_SampleSolutionIsAccepted`, `MSG_ENG_001_AC1_SampleProjectOutsideItsFolderIsFound`, `MSG_ENG_001_AC1_SampleProjectTheSampleSolutionDoesNotListIsFound`, `MSG_ENG_001_AC1_SampleTestProjectWithoutItsSampleHostIsFound`, `MSG_ENG_001_AC1_SamplePackageSourcesFollowTheRule`, `MSG_ENG_001_AC1_SamplePackageSourcesOutsideTheRuleAreFound`, `MSG_ENG_001_AC1_SampleHostWithoutItsPackageSourcesIsFound`, `MSG_ENG_001_AC1_FileOfTopLevelStatementsIsAccepted`, `MSG_ENG_001_AC1_FileWithoutANamespaceIsFound`, `MSG_ENG_001_AC1_ProjectFoldersFollowTheLayout` |
| `07` MSG-ENG-006 | `MSG_ENG_006_AC1_TheMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05`, `MSG_ENG_006_AC1_EveryBodyOfTheSerializerContextIsOneARouteReadsOrAnswers`, `MSG_ENG_006_AC1_AStatus05GivesThatTheEndpointDoesNotDeclareIsFound`, `MSG_ENG_006_AC1_ARequestMember05GivesThatTheBodyDoesNotHoldIsFound`, `MSG_ENG_006_AC1_ARoute05DoesNotGiveIsFound`, `OfSketch_ABodyAs05WritesIt_AnswersEachMemberPath`, `Of_ABodyWithNestedBodies_AnswersEachMemberPathAsTheSerializerContextNamesIt` |
| `04` MSG-LIB-001, the sample host's row in the references test | `MSG_LIB_001_AC1_SampleHostAndItsTestsWithinTheirRowAreAccepted`, `MSG_LIB_001_AC1_SampleHostReferenceOutsideItsRowIsFound`, `MSG_LIB_001_AC1_SampleTestsReferencingALibraryProjectIsFound` |
| `07` MSG-ENG-009, the library's own packages in the sample host | `MSG_ENG_009_AC1_TheLibrarysOwnPackagesInTheSampleHostAndTheirVersionsAreNoThirdPartyOnes`, `MSG_ENG_009_AC1_TheLibrarysOwnPackageOutsideTheSampleHostIsFound` |

The release set of `04` MSG-LIB-006 now reads the routes and bodies through the same readers as the endpoint contract test.

## Defects found

| Place | Defect | Fix | Test |
|---|---|---|---|
| `MessagingServiceCollectionExtensions` | `PushSubscriptions` was registered as a singleton while it takes the scoped `ContentProtection`, so a host whose service provider checks lifetimes (ASP.NET Core's in development, as the sample host's tests run it) failed to start with the push subscription group mapped | Registered per request; it holds no state and is resolved only from the request's services | `AddHermes_OnAProviderThatValidatesScopesWhenBuilt_HoldsNoSingletonTakingAScopedService` |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |
| `02` MSG-CHAN-001 criterion 4, the SMS half | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-003 criterion 4 | 6, 7 | Unchanged from phase 2 |
| `06` MSG-OPS-004 criterion 1, the shipped SMS provider | 7 | The WE adapter, unchanged from phase 4 |
| `04` MSG-LIB-006, a release's baseline | 8 | Unchanged from phase 5 |
| `07` MSG-ENG-001, Janus's source and its mapping in the root `nuget.config` | 6 | The plan's |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds: no identifier, text, default or fixture names a host product, company or application. The fixtures use `example.com` and `example.net`. |
| `07` MSG-ENG-001, the sample host's invented domain | A bakery: a customer is told of an order by email and SMS, and of the weekly offers by email and push; `orders` is essential and `offers` optional. It sends through the in-memory providers of `Hermes.Testing` and maps every endpoint group under `/notifications`. |
| `07` MSG-ENG-008, a test with or before the code it proves | The `test` commits prove the layout, references and endpoint rules before the sample host's commit; the `fix` commit carries its test. |
| `07` MSG-ENG-009, the packages added | No third-party package is added: the sample host's projects take only packages already in `Directory.Packages.props`, and the library's own. |
| `07` MSG-ENG-001, the grants | The sample host and its tests grant nothing. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `07` MSG-ENG-001 criterion 2 on the pipeline's machine: `gate-sample-host` runs there as one of the gates, and its test runs again in the integration set.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

| Place | Out of step | Governing item | Rule applied |
|---|---|---|---|
| `LayoutRules` | `samples/Bakery/Program.cs`, the sample host's top-level statements, cannot declare a namespace | `07` MSG-ENG-001 | A file whose code beyond its using directives and assembly attributes is statements and no type needs no namespace; a file of a type, or of attributes alone, still does. |
| `list-tests.ps1` | The sample host's test project builds only from the packages packed from the commit | `07` MSG-ENG-001, MSG-ENG-008 | The test-name and tests gates list `tests/` alone; `gate-sample-host` builds and runs the sample host's tests. |
| `samples/Bakery.Tests` | Its tests carry no `Category` trait | `07` MSG-ENG-008 | They are the fourth kind, in their own project, which the three runs by trait do not read. |
| `ReferenceRules` | The grants of the sample host | `07` MSG-ENG-001 | Not read: it is no source project, so it may grant nothing, and only its gate builds it. |
| `Directory.Packages.props` | Central Package Management needs a version for the library's own packages | `07` MSG-ENG-001, MSG-ENG-009 | `Hermes` and `Hermes.Testing` are listed only when `HermesPackageVersion` is set, which only `gate-sample-host` sets, at the version it packed. |
| `gate-sample-host` | A version without a release tag (`0.0.0-alpha.0.N`) can repeat across commits | `07` MSG-ENG-001 | The sample host restores into a packages folder of its own under `tmp/sample-host/`, emptied each run, so no package packed from an earlier commit is taken. |
| `ApiContract` | The statuses `05` gives by its conventions rather than in each table | `07` MSG-ENG-006; `05` MSG-API-001, MSG-API-002 | A route reading a JSON body adds 400, 413 and 415; a section needing a signed-in person adds 401, unless the route's answers say it needs none. |

## Open questions

This phase asked none.

## Commit and checks

The report names commit `d5eab18339e7b6bd1c5d989ad2dc8ca5b06aa27e`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,136 (`Hermes.Tests` 503, `Hermes.Channels.Tests` 359, `Hermes.Repository.Tests` 201, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14); the Janus test projects hold no test yet |
| Every contract test | Passed: 72 (`Hermes.Repository.Tests` 54, `Hermes.Tests` 18) |
| Integration tests this phase writes or touches, run here by class | Passed: `SampleHostGateTests`, `TestNameGateTests`, `TestsGateTests`, `EndpointGroupTests`, `PushTests` |
| The sample host's gate (`gate-sample-host`) | Passed: its 6 tests, run by `SampleHostGateTests` |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 242 criteria |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 7 commits after `main` |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 123 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

The secret scanner was fetched again before the scan.