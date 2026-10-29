# Phase 9: the development inbox

## Items built

| Item | Tests |
|---|---|
| The amendments of M-024 under `docs/` (`89e6fa0`) | None; the documents themselves |
| `04` MSG-LIB-001 and `07` MSG-ENG-001: the project `src/Hermes.Development`, referencing `Hermes.Core` and `Hermes.Channels` and taking ASP.NET Core's shared framework, its description `00` section 2.2's; `tests/Hermes.Development.Tests`, with `Hermes.Testing`; the grant from `Hermes.Channels`; the package mapped in `samples/nuget.config` (`6e8d620`) | `MSG_LIB_001_AC1_DevelopmentReferencingTheEngineIsFound`, `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound` (Hermes to Hermes.Development), `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted` (Hermes.Channels to Hermes.Development), `MSG_ENG_001_AC1_PackageWithoutItsProjectIsFound` (Hermes.Development), `Pack_EachPackage_CarriesItsDescriptionAuthorsAndAddressAlone`, and the public type table's row |
| The email's MIME message and a push endpoint's service named by one code each in `Hermes.Channels`, the SMTP session's and the inbox's alike (`161bb43`) | The SMTP and push tests of `Hermes.Channels.Tests` and `Hermes.Tests`, unchanged; `MSG_DEV_002_AC4_AnEmailsEmlReadsToTheSameHeadersAndPartsAsTheMessageTheSmtpProviderSubmits` |
| `02` MSG-DEV-001 and `08` section 4's `DevelopmentInbox:` keys: capture in Development whatever the order of the host's own provider registrations, `AddHermesTesting`'s providers winning in either order, nothing outside Development, one start line (`7498b59`) | `MSG_DEV_001_AC1_AnEmailAnSmsAndAPushTheEngineDispatchesAreWrittenToTheInboxAndNoneReachesTheHostsProviders`, `MSG_DEV_001_AC1_AnEmailHandedToTheProvidersDirectlyIsWrittenToTheInboxAndReachesNoneOfTheHostsProviders`, `MSG_DEV_001_AC2_OutsideDevelopmentTheCallRegistersNothingAndTheHostsOwnProvidersAreCalled`, `MSG_DEV_001_AC2_OutsideDevelopmentTheMappingMapsNoRoute`, `MSG_DEV_001_AC3_WithAddHermesTestingBeforeOrAfterTheInMemoryProvidersReceiveEveryMessageAndTheInboxHoldsNone`, `MSG_DEV_001_AC4_TheStartLogsOneLineNamingTheFolderAndTheGroupAndNothingOfAMessage`, `AddHermesDevelopmentInbox_WithAKeyOutsideItsRule_StopsTheStartNamingTheKey`, `AddHermesDevelopmentInbox_OutsideDevelopment_ReadsNoKey`, `AddHermesDevelopmentInbox_WithAServiceProviderFactoryTheHostSetsAfterIt_StopsTheStart` |
| `02` MSG-DEV-002 and MSG-DEV-003: a record per message, an email's `.eml` beside it, each written under a temporary name and renamed, the record last; the folder its owner's alone, cleaned as it grows; the dispatched delivery's kind, category, language and person carried from the engine (`61a6bfc`) | `MSG_DEV_002_AC1_EachMessageIsARecordNamedByItsInstantChannelAndIdAndAnEmailsEmlBesideIt`, `MSG_DEV_002_AC1_AReaderListingTheFolderWhileSeveralWritersWriteNeverReadsAPartialRecordAndFindsEachEmailsEmlBesideIt`, `MSG_DEV_002_AC2_RecordsOlderThanKeepForAreDeletedWithTheirEmlWhenAMessageIsWritten`, `MSG_DEV_002_AC2_RecordsBeyondTheNewestMaxMessagesAreDeletedWithTheirEml`, `MSG_DEV_002_AC2_ATmpFileOlderThanAMinuteIsDeletedAndEveryOtherFileIsLeft`, `MSG_DEV_002_AC3_AFolderTheLibraryCreatesIsItsOwnersAloneOnLinux`, `MSG_DEV_002_AC4_AnEmailsEmlReadsToTheSameHeadersAndPartsAsTheMessageTheSmtpProviderSubmits`, `InboxSettings_WithNoFolderConfigured_ReadHermesInboxInTheTemporaryFolder`, `MSG_DEV_003_AC1_ARecordOfEachChannelTheEngineDispatchedHoldsItsMembersAndItsDeliverysKindCategoryLanguageAndPerson`, `MSG_DEV_003_AC1_AnEmailHandedToTheProvidersDirectlyHoldsNoKindCategoryOrPersonAndItsOwnLanguage`, `MSG_DEV_003_AC1_AnSmssSummaryIsItsFirstHundredCharactersCountedAsScalarValues`, `MSG_DEV_003_AC2_EachAbsoluteLinkOfTheHtmlAndTheTextIsHeldOnceInTheOrderMet`, `MSG_DEV_003_AC2_AnSmssLinksAreThoseOfItsTextAndAPushsItsAbsoluteUrl`, `MSG_DEV_003_AC3_APushRecordHoldsNoPartOfItsSubscriptionsEndpoint` |
| `05` MSG-API-007 and `02` MSG-DEV-005: the group, its routes, bodies and codes, answering only the machine itself under a loopback host name, an email's HTML only from its own route under its policy and sandbox (`3a5e67f`); the endpoint contract test brought to it | `MSG_API_007_AC1_AnEmailAnSmsAndAPushAreEachAnsweredWithTheirMembersAndListedNewestFirst`, `MSG_API_007_AC1_AMessageIsAnsweredAsTheSameJsonItsRecordHolds`, `MSG_API_007_AC1_TheListIsFilteredByEachQueryParameterAndBoundedByLimit`, `MSG_API_007_AC1_AQueryParameterOutsideItsRuleIsAnswered400NamingIt`, `MSG_API_007_AC1_AParameterOfTheOtherRouteIsIgnored`, `MSG_API_007_AC1_AMessageTheInboxDoesNotHoldIsAnswered404`, `MSG_API_007_AC1_AnEmailsEmlIsAnsweredAsAnAttachmentNamedByItsId`, `MSG_API_007_AC1_ClearWithItsHeaderDeletesEveryRecordAndEml`, `MSG_API_007_AC2_LatestWithWaitAnswersTheMessageAsSoonAsItIsCaptured`, `MSG_API_007_AC2_LatestWithWaitAndNothingSentAnswers404OnceTheWaitHasPassed`, `MSG_API_007_AC2_LatestWithoutWaitAnswersTheNewestMatchOr404AtOnce`, `MSG_API_007_AC3_OutsideDevelopmentTheGroupAnswersNoRoute`, `MSG_DEV_005_AC1_ARequestFromAnotherAddressOrUnderAnotherHostIsAnswered403OnEveryRoute`, `MSG_DEV_005_AC1_ARequestFromTheMachineUnderALoopbackNameIsAnswered`, `MSG_DEV_005_AC1_WithAllowRemoteOnARequestFromAnotherMachineIsAnsweredAsFromTheMachine`, `MSG_DEV_005_AC2_NoAnswerCarriesAnAccessControlHeaderEvenToARequestFromAnotherOrigin`, `MSG_DEV_005_AC2_ClearWithoutItsHeaderIsAnswered400AndClearsNothing`, `MSG_DEV_005_AC3_ThePageItsScriptAndItsStyleCarryThePagesPolicyAndHeaders`, `MSG_DEV_005_AC3_EveryOtherAnswerCarriesTheThreeHeaders`, `MSG_DEV_005_AC3_AnEmailsHtmlCarriesItsPolicyAndHeaders`, `MSG_DEV_005_AC4_AnEmailsHtmlHoldingAScriptIsServedSandboxedWithItsBaseAddedAndItsEmlUnchanged`, `MSG_DEV_005_AC4_TheBaseIsPutAtTheStartOfTheHeadOrOfTheDocument`; the contract tests `MSG_ENG_006_AC1_TheInboxsMappedEndpointsAreExactlyTheRoutesMethodsStatusesAndBodiesOf05`, `MSG_ENG_006_AC1_EveryBodyOfTheInboxsSerializerContextIsOneARouteAnswers`, `MSG_ENG_006_AC1_AStatusOfTheInboxThatTheEndpointDoesNotDeclareIsFound`, `MSG_ENG_006_AC1_AMemberOfAShapeThatTheBodyDoesNotHoldIsFound`, and the engine's `EndpointContractTests`, which no longer read the inbox's rows |
| `07` and `06` MSG-OPS-005: every integration test of the new package checks its log lines for each value it sent (`605d6b1`) | The integration tests of `CaptureTests` and `RecordTests`, each failing where a `Hermes.` line holds a value it sent |
| `02` MSG-DEV-004: one plain, self-contained page, its script and style embedded in the package, no framework, nothing fetched from outside (`cd48676`) | `MSG_DEV_004_AC1_ThePageReferencesOnlyItsOwnScriptAndStyleUnderTheGroupsAddressAndAnEmptyIcon`, `MSG_DEV_004_AC1_TheScriptAndStyleReferenceNoResourceOutsideTheApplication`, `MSG_DEV_004_AC2_TheScriptWritesNoValueAsMarkup`, `MSG_DEV_004_AC3_TheScriptAsksForNewMessagesEverySecondAfterTheNewestItHolds`, `MSG_DEV_005_AC3_ThePageItsScriptAndItsStyleCarryThePagesPolicyAndHeaders`; and the browser check below |
| `02` MSG-DEV-006: `DevelopmentInbox` (`d76916b`) | `MSG_DEV_006_AC1_ATestThatMakesAnInboxThenSendsAnEmailHoldingALinkWaitsForItAndFindsTheLink`, `MSG_DEV_006_AC1_AWaitAnswersAMessageCapturedAfterTheFiltersAfterAtOnce`, `MSG_DEV_006_AC2_AWaitThatNoMessageMatchesThrowsTimeoutExceptionOnceItsTimeoutHasPassed`, `MSG_DEV_006_AC3_AfterClearTheFolderHoldsNoRecordAndNoEml`, `ListAsync_WithAFilter_AnswersTheMatchingMessagesNewestFirst`, `ListAsync_OfAFolderThatDoesNotExist_AnswersNone`, `DevelopmentInbox_MadeWithoutAFolder_ReadsTheDefaultFolderAndRecordsTheMillisecondItWasMade`, `WaitForAsync_WithItsTokenCancelled_ThrowsOperationCanceledException`, `DevelopmentInbox_WithAnArgumentOutsideItsRule_Throws` |
| `07` MSG-ENG-001: the sample host runs with the inbox, mapped at `/dev/inbox`, and its tests keep the in-memory providers (`32e301e`) | `PlaceOrder_InTheTests_GoesThroughTheInMemoryProvidersAndLeavesTheDevelopmentInboxEmpty`, `DevelopmentInbox_AskedFromOutsideTheMachine_Is403`, the sample host's other tests, and `gate-sample-host` |
| `RELEASE-NOTES.md`: a 1.1.0 section above 1.0.0's, with the new deployment duty of `06` MSG-OPS-007 (`96284d2`) | Checked in review; see below |
| The test-name gate's list: MSG-DEV-001 to MSG-DEV-006 and MSG-API-007 (`bbfa1d6`) | `gate-test-names`: 270 criteria |

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| The release commit `chore: release 1.1.0` and the tag `v1.1.0` | 9, in this pull request | The owner's order: the release commit comes once the review has passed, on the owner's word, and the tag after its merge |
| `07` MSG-ENG-011 criterion 4 for 1.1.0 | 9, the tag's run | The release tag's run |
| `00` MSG-OVR-001 criterion 1 | Each phase | Checked in review; see below |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds or changes: no identifier, text, default or fixture names a host product, company or application. The sample host's names are the bakery's of earlier phases. |
| `02` MSG-DEV-004, the page as a person reads it | We ran a scratch host on `127.0.0.1` with the inbox, holding an email with HTML and a script, an SMS in Arabic holding a link, and a push in Arabic, right to left, with a `data:` icon, and read the page in the desktop application's built-in browser. We checked the list in the light and dark schemes; the push shown as a right-to-left notification with Open and Copy on its link; the SMS as a bubble, its direction automatic, with "UCS-2 · 1 part"; the email's facts, its tabs (Message, Plain text, Headers with 7 rows; the arrow keys move between the tabs and wrap) and its `.eml` link; the filters (the channel `sms` leaves 1 entry, the recipient `SOMEONE` 1); the application select; and Clear, which emptied the list and said "The inbox is empty.". The host's log showed the page asking every second with `after=`. The built-in browser refuses every frame a page loads (`ERR_BLOCKED_BY_CLIENT`, its own rule), so we read the email's `/html` route in a tab of its own: it showed the email with its style, and the console reported the script blocked because the frame is sandboxed without `allow-scripts`. We also ran the sample host in Development against a throwaway PostgreSQL 16.15 container: it logged its inbox line, an order's email and SMS went through its hosted dispatcher into the inbox with the kind `order.received`, the category `orders`, the language `en` and the person `c-1`, and its page listed both. The container and its password file were removed. |
| `02` MSG-DEV-004 criteria 2 and 3 and MSG-DEV-005's page rules, in review as well as tested | We read `inbox.js`: every value is written through `textContent` or an attribute, no `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `eval` or `Function`; the only addresses are the group's own routes; the email's HTML is shown only in an `iframe` of its own route, sandboxed without scripts. |
| `06` MSG-OPS-007 criterion 1 for 1.1.0 | We read the 1.1.0 section of `RELEASE-NOTES.md` against each value of MSG-OPS-007: the packages, the codes, keys, routes and bodies added, no migration (none is added), and the deployment's duties, among them the new one that every application that must send runs in an environment other than Development. The 1.0.0 section is unchanged. |
| `10` MSG-THR-001, the inbox's four threats | The table's new rows name MSG-DEV-001, MSG-DEV-002 and MSG-DEV-005, each with tests carrying it; `MSG_THR_001_AC1_EveryControlTheLibraryCarriesHasATestCarryingItsRequirement` passes. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each item's tests were written first and failed before the code that passes them. |
| `07` MSG-ENG-009, the packages added | No package is added from outside: `Hermes.Development` takes ASP.NET Core's shared framework, and the sample's reference is to the new package of this repository. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.
- `02` MSG-DEV-002 criterion 3: `MSG_DEV_002_AC3_AFolderTheLibraryCreatesIsItsOwnersAloneOnLinux` checks the folder's mode `0700` on the pipeline's Linux runner; on this machine, Windows, it checks only that the folder is created.

## Analyser suppressions

| Rule | Where | Justification |
|---|---|---|
| CA1056, a URI-like property as a string | `InboxNotification.Url` | `04` MSG-LIB-002 gives it as the payload's text, which no rule makes an absolute address. |

This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **The inbox's precedence over the host's providers, in either order.** `AddHermesDevelopmentInbox` puts its providers in place through `ConfigureContainer` with the default service provider factory, after every registration of the host's; `AddHermesTesting`'s providers are seen there and left to win. A host that sets its own factory after the call replaces it, so the start then stops with `InvalidOperationException` naming the call, rather than sending.
2. **The inbox's endpoint contract test.** `04` MSG-LIB-001 lets `Hermes.Tests` reference only `Hermes` and `Hermes.Testing`, so the test of the inbox's group against `05` lives in `Hermes.Development.Tests` (`InboxContract`), and `Hermes.Tests`' reading of `05` leaves the inbox's section to it.
3. **Writing on Windows.** A reader that meets a record Windows still holds during its rename tries again, up to 5 times, 10 milliseconds apart; a record that is gone is no record.
4. **Several writers.** MSG-DEV-002 criterion 1 is proven with 4 writers in one process writing to one folder, the rename being what keeps a reader from a partial record whatever the writer.
5. **The base of an email's HTML.** `<base target="_blank">` goes at the start of the HTML's `head`; where there is none, after a leading doctype, so the document keeps its mode, or else at the start.
6. **The sample host's keys.** The sample keeps `FixedSecretSource` from `Hermes.Testing` as its key source, since no key material may be committed; it no longer calls `AddHermesTesting`, which its tests do.
7. **The page's polling.** The page asks for the messages after the newest it holds every second, and reads the whole list every tenth time or whenever it holds none, so a clearing or clean-up done elsewhere shows.
8. **The clearing header.** `X-Hermes-Inbox` clears only with the one value `clear`.

## Open questions

1. **The release set and the new package.** The release gate reads its set (the routes, bodies, codes and keys) through `Hermes.Tests`, which `04` MSG-LIB-001 does not let reference `Hermes.Development`, so the inbox's routes, bodies, codes and keys are not in the set the release commit of 1.1.0 would write; its public types are, through its `PublicAPI` files. Two ways would bring them in: a grant in `04` for `Hermes.Tests` to reference `Hermes.Development`, or the release gate also reading a set from `Hermes.Development.Tests`. Asked here; unanswered. It parks nothing built: every gate passes, and the inbox's contract is tested against `05` by its own contract test.

## Commit and checks

The report names `bbfa1d6`. On its tree, clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed |
| Every unit test | Passed: 1,255 (`Hermes.Tests` 507, `Hermes.Channels.Tests` 362, `Hermes.Repository.Tests` 218, `Hermes.Development.Tests` 95, `Hermes.Core.Tests` 41, `Hermes.Cli.Tests` 18, `Hermes.Testing.Tests` 14) |
| Every contract test | Passed: 69 (`Hermes.Repository.Tests` 47, `Hermes.Tests` 18, `Hermes.Development.Tests` 4) |
| Integration tests this phase touches, run here by class | Passed: `Hermes.Development.Tests`' integration tests (18); in `Hermes.Tests`, `DispatchTests`, `PushTests`, `HostedDispatchTests`, `DispatchHealthTests` and `EndpointGroupTests` (101), the engine's classes whose provider resolution changes; `Hermes.Testing.Tests`' `TestingHostTests` (5); `PackTests` and `SampleHostGateTests` (4) |
| The sample host's gate (`gate-sample-host`) | Passed |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 270 criteria |
| Commit message format and changelog line (`gate-commit-messages`) | Passed: 12 commits after `main` |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 163 commits, no leaks |

The full integration set is the pipeline's (M-018). The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.

The secret scanner was fetched again before the scan.
