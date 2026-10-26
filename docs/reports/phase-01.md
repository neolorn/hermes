# Phase 1: foundations

## Items built

| Item | Tests |
|---|---|
| `06` MSG-OPS-001 (all but the keys parked by questions 2 and 3) | `MSG_OPS_001_AC1_EveryKeyAbsentHasItsDefault`, `MSG_OPS_001_AC1_KeyPresentIsBound`, `MSG_OPS_001_AC1_KeyOutsideItsRuleStopsTheStart`, `MSG_OPS_001_AC1_KeyAtTheEdgeOfItsRuleIsWithinIt`, `MSG_OPS_001_AC1_WriteVersionIsNotCheckedWithTheOtherKeys`, `MSG_OPS_001_AC1_LanguagesOutsideTheirRuleStopTheStart`, `MSG_OPS_001_AC1_DefaultLanguageMatchesItsLanguageWhateverItsCase`, `MSG_OPS_001_AC1_WindowsTimeZoneIdentifierIsOutsideTheRule`, `MSG_OPS_001_AC2_ReloadedValueWithinItsRuleTakesEffect`, `MSG_OPS_001_AC2_StartOnlyKeyChangedWhileRunningTakesEffectOnlyAtTheNextStart`, `MSG_OPS_001_AC2_ReloadedValueOutsideItsRuleLeavesTheLastValidValueInForce`, `MSG_OPS_001_AC2_ReloadedValueThatBreaksAnotherKeysRuleLeavesBothInForce`, `MSG_OPS_001_AC2_ReloadedTimeZoneOutsideItsRuleLeavesTheLastValidOneInForce`, `MSG_OPS_001_AC2_ReloadedKeyRemovedTakesItsDefault`, `MSG_OPS_001_AC2_AReloadedValueOutsideItsRuleRaisesTheAlertUntilItIsPutRight` |
| `06` MSG-OPS-002 | `MSG_OPS_002_AC1_StartingAgainstADatabaseMissingTheReleasesLastMigrationStopsTheStart`, `MSG_OPS_002_AC2_MigrateTwiceAppliesNothingTheSecondTimeAndExitsZeroBothTimes`, `MSG_OPS_002_AC3_RoleNamedInApplicationRolesCanReadAndWriteEveryTable`, `MSG_OPS_002_AC3_InputThatDoesNotReadExitsOneWithInputInvalid` |
| `06` MSG-OPS-003 (the library's own data source on `connection`; `listenConnection` comes with the dispatcher) | `Start_ConnectionOfSeveralVersions_UsesTheHighest`, `Start_ConnectionThatDoesNotRead_StopsTheStartAsInvalid`, `Insert_KeyOutsideTheFiveKeys_IsRefusedByTheDatabase` |
| `06` MSG-OPS-005 (the meter and its instruments, the log entries) | `MSG_OPS_005_TheMeterHermesPublishesTheInstrumentsOfTheReference`, `MSG_OPS_005_EachInstrumentIsOfTheMeterHermes`, `MSG_OPS_008_NoLogEntryOfAHostsLifeHoldsASecretADestinationOrAPerson` |
| `06` MSG-OPS-006 | `MSG_OPS_006_AC1_AConditionHoldingFromMidnightToHalfPastTwoIsRaisedEachHourAndClearedOnce`, `MSG_OPS_006_AC2_TwoProcessesSeeingTheSameConditionRaiseItOnce` |
| `06` MSG-OPS-008 (the secrets the engine's parts need, the key table, its timer, the content key reservations, `newer-release` and `content-key-limit`) | `MSG_OPS_008_AC1_ConnectionMissingStopsTheStartNamingIt`, `MSG_OPS_008_AC1_HashKeysMissingStopsTheStartNamingIt`, `MSG_OPS_008_AC1_ContentKeysMissingWhereSendsAreRecordedStopsTheStartNamingIt`, `MSG_OPS_008_AC1_ContentKeysMissingWhereTheApplicationDispatchesStopsTheStartNamingIt`, `MSG_OPS_008_AC1_ContentKeysMissingWhereJobsRunStopsTheStartNamingIt`, `MSG_OPS_008_AC1_TokenKeysMissingWhereAnOptionalCategoryHasAnEmailKindStopsTheStartNamingIt`, `MSG_OPS_008_AC1_KeyVersionOutsideItsFormStopsTheStartNamingIt`, `MSG_OPS_008_AC2_AVersionWhoseBytesChangedSinceFirstUsedStopsTheStart`, `MSG_OPS_008_AC4_TwoContentKeyVersionsWithNoWriteVersionStopTheStart`, `MSG_OPS_008_AC4_NewValuesAreMadeUnderTheWriteVersionAndValuesUnderAnOlderOneStillOpen`, `MSG_OPS_008_AC9_AMigrationThisReleaseDoesNotKnowRaisesNewerReleaseWithinTheInterval`, `MSG_OPS_008_AC11_PushUsedWithTheHostedDispatcherOffDoesNotStartWithoutPushVapidKeys`, `MSG_OPS_008_AStartStoppedOnASecretWritesNothingOfIt` |
| `04` MSG-LIB-003 (the startup checks, and the keys each used part needs) | `MSG_LIB_003_AC1_AHostWithValidDeclarationsAndConfigurationStartsWithTheOneCall`, `MSG_LIB_003_AC2_ValueThatPutsAnotherKeyOutsideItsRuleStopsTheStartNamingThatKey` |
| `04` MSG-LIB-005 (all but `IRecipientIdentity` and `ITemplateSource`) | `MSG_LIB_005_AC1_SecretSourceMissingStopsTheStartNamingIt`, `MSG_LIB_005_AC1_ConsentSourceMissingWhereACategoryRequiresConsentStopsTheStartNamingIt`, `MSG_LIB_005_AC1_ConsentSourceIsNotRequiredWhereNoCategoryRequiresConsent`, `MSG_LIB_005_AC1_DeliveryObserverMissingWhereObserverAndDispatchAreOnStopsTheStartNamingIt`, `MSG_LIB_005_AC1_DeliveryObserverIsNotRequiredWhereDispatchIsOff`, `MSG_LIB_005_AC1_SeamRegisteredAfterTheCallIsFound`, `MSG_LIB_005_AC2_TwoHostProtectorsWithOneIdStopTheStart`, `MSG_LIB_005_AC2_WithTwoHostProtectorsValuesAreWrittenByTheLastAndValuesOfTheFirstStillOpen`, `MSG_LIB_005_LibrarysOwnAlertSinkLogsEachConditionAtTheErrorLevel` |
| `01` MSG-SEND-001 (the declarations and their startup checks) | `MSG_SEND_001_AC2_KindNameOutsideTheRuleFailsStartup`, `MSG_SEND_001_AC2_KindNameOfTheRuleWithEachSeparatorIsAccepted`, `MSG_SEND_001_AC2_KindDeclaredTwiceFailsStartup`, `MSG_SEND_001_AC2_KindBothDeclaredAndRetiredFailsStartup`, `MSG_SEND_001_AC2_RetiredNameOutsideTheRuleFailsStartup`, `MSG_SEND_001_AC2_KindRetiredTwiceFailsStartup`, `MSG_SEND_001_AC2_KindOfACategoryNotDeclaredFailsStartup`, `MSG_SEND_001_AC2_KindWithNoChannelFailsStartup`, `MSG_SEND_001_AC2_NonPositiveExpiresAfterFailsStartup`, `MSG_SEND_001_AC2_SmsMaxSegmentsOutsideOneToTenFailsStartup`, `MSG_SEND_001_AC2_SmsMaxSegmentsAtTheEdgeIsAccepted`, `MSG_SEND_001_AC2_PushTtlOutsideOneSecondToTwentyEightDaysFailsStartup`, `MSG_SEND_001_AC2_PushTtlAtTheEdgeIsAccepted` |
| `01` MSG-SEND-002 (the declarations and their startup checks, all but the label length parked by question 1) | `MSG_SEND_002_AC2_CategoryNameOutsideTheRuleFailsStartup`, `MSG_SEND_002_AC2_CategoryDeclaredTwiceFailsStartup`, `MSG_SEND_002_AC2_EssentialCategoryWithAConsentPurposeFailsStartup`, `MSG_SEND_002_AC2_EssentialCategoryWithQuietHoursFailsStartup`, `MSG_SEND_002_AC2_EssentialCategoryWithoutQuietHoursIsAccepted`, `MSG_SEND_002_AC2_ConsentPurposeOutsideTheNameRuleFailsStartup`, `MSG_SEND_002_AC2_LimitOutsideItsRuleFailsStartup`, `MSG_SEND_002_AC2_DeploymentLimitOutsideItsRuleFailsStartup`, `MSG_SEND_002_AC2_LimitWindowAboveRetentionPlacesFailsStartup`, `MSG_SEND_002_AC2_LimitOnAChannelNoKindOfTheCategoryUsesFailsStartup`, `MSG_SEND_002_AC2_TwoLimitsOnOneChannelWithTheSameWindowFailStartup`, `MSG_SEND_002_AC2_TwoDeploymentLimitsOnOneChannelWithTheSameWindowFailStartup`, `MSG_SEND_002_AC2_SeveralLimitsOnOneChannelWithTheirOwnWindowsAreAccepted`, `MSG_SEND_002_AC2_LabelMissingForADeclaredLanguageOfAnOptionalCategoryWithAnEmailKindFailsStartup`, `MSG_SEND_002_AC2_LabelOfEachDeclaredLanguageWhateverItsCaseIsAccepted`, `MSG_SEND_002_AC2_LabelIsNotRequiredWithoutAnEmailKind` |
| `01` MSG-SEND-018 (the envelope, the library's protector, the protection of a person's and an anonymous recipient's values, the binding to row, field and person) | `MSG_SEND_018_AProtectedValueOpensInItsRowForItsPersonAndNowhereElse`, `MSG_SEND_018_AValueOfAHostProtectorOpensInItsRowForItsPersonAndNowhereElse`, `Protect_NoHostProtector_WritesTheLibrarysEnvelopeAndLayout`, `Protect_AnonymousRecipient_UsesTheLibrarysProtectorWhateverIsRegistered`, `Open_FormatKeptForALaterRelease_IsHeldNamingNoKey`, `Open_ValueInNoFormatOrAMalformedEnvelope_IsRejected`, `Open_EnvelopeNamingAProtectorNotRegistered_IsHeldNamingIt`, `Open_LibraryValueAltered_IsRejected`, `Of_ARow_IsItsTableItsIdInLowerCaseHyphenatedHexAndItsField` |
| `00` MSG-OVR-002 | `MSG_OVR_002_AC2_EveryTableTheLibraryUsesLivesInItsOwnSchema`; criterion 1 checked without a test (below) |
| `07` MSG-ENG-012, the migrations gate | `GateMigrations_ThisRepository_AppliesTheMigrationsThenNothing`, `GateMigrations_RepositoryWithoutTheTool_Fails`; `build/gates/gate-migrations.ps1`, which the `gates` job runs as it runs every script present |
| `07` MSG-ENG-003, the startup codes | `MSG_ENG_003_AC1_AStartupCodeIsThrownOnlyAsTheStartupExceptionWhileTheHostStarts` |
| `07` MSG-ENG-005 criterion 4 | `MSG_ENG_005_AC4_ATransactionAbortedWith40001IsRunAgainAtMostRetriesTimes` |
| `07` MSG-ENG-007 criterion 1, for codes, configuration keys, alert conditions, secrets and metric instruments | `MSG_ENG_007_AC1_EveryCodeAnAssemblyHoldsIsOneSection1Lists`, `MSG_ENG_007_AC1_CodeSection1DoesNotListIsFound`, `MSG_ENG_007_AC1_EveryConfigurationKeyTheEngineBindsIsOneSection4Lists`, `MSG_ENG_007_AC1_TheAlertConditionsAreThoseSection5Lists`, `MSG_ENG_007_AC1_EverySecretTheEngineReadsIsOneSection7Lists`, `MSG_ENG_007_AC1_EveryInstrumentATestHostsMeterPublishesIsOneSection8Lists` |
| `04` MSG-LIB-002, the types of the seams this phase uses | `MSG_LIB_002_ConsentQueryPrintsNeitherItsRecipientNorItsDestination`, `MSG_LIB_002_PersonPrintsNoReference`, `MSG_LIB_002_DeliveryNoticePrintsNoPerson`, `MSG_LIB_002_SecretVersionPrintsNoValue`, `MSG_LIB_002_OpenedPrintsNoPlaintext`, and the contract tests of phase 0 over the surface files |
| `Hermes.Cli`, `hermes migrate` | The tests of `06` MSG-OPS-002, and `Run_ArgumentsOtherThanMigrate_ExitsOne`, `Migrate_ConnectionThatCannotConnect_ExitsOneWithConnectionFailed`, `Migrate_MigrationThatFails_ExitsOneNamingIt` |

Tests of a rule that no criterion carries: `Start_ConnectionWithNoVersion_StopsTheStartAsUnavailable`, `Start_ContentKeysWhereNoPartNeedsThem_IsNotRead`, `Start_TokenKeysWhereNoSendsAreRecorded_IsNotRead`, `Start_TokenKeysWhereEveryEmailKindIsEssential_IsNotRead`, `Start_PushVapidKeysWherePushIsNotDispatched_IsNotRead`, `Start_PushVapidKeyThatIsNoScalarOfP256_StopsTheStartAsInvalid`, `Start_KeyVersionsOfTheirForms_PassTheSecrets`, `Start_KeyVersionNumberedBelowOne_StopsTheStartAsInvalid`, `Start_KeyVersionNumberedTwice_StopsTheStartAsInvalid`, `Start_WriteVersionNotAVersionHeld_StopsTheStartNamingIt`, `Start_WriteVersionOfAVersionHeld_PassesTheSecrets`, `Start_WriteVersionOfAKeyNoPartNeeds_IsNotChecked`, `Start_HeldVersions_RecordEachCheckValue`, `Start_WriteVersions_SetTheirFirstAndLastMadeInstantsAndClearTheirRetiredMark`, `Start_VersionInUseNotHeld_StopsTheStartAsUnavailable`, `Start_VersionRetiredNotHeld_Starts`, `Start_ProcessReadingTokenKeys_RecordsTheLongestTokenLifetime`, `Start_ProcessNotReadingTokenKeys_RecordsNoTokenLifetime`, `Timer_EachIntervalAndTheStop_WriteTheWriteVersionsLastMadeInstant`, `Timer_WriteVersionUsedForMoreThanMaxValues_RaisesContentKeyLimit`, `Timer_WriteVersionAtMaxValues_RaisesNothing`, `HoldsUntil_EachWrite_RecordsNowPlusTwiceTheIntervalWhereLaterAndRaisesWhenItStartsToHold`, `HoldsUntil_AgainWithinTheInterval_WritesNothing`, `Protect_AnonymousRecipient_BindsTheLibrarysValueToAnEmptyPerson`, `Protect_HostProtectorRegistered_WritesItsEnvelope`, `Open_ValueOfTheLibrarysProtectorAfterAHostProtectorIsRegistered_Opens`, `Open_LibraryValueUnderAVersionNotHeld_IsHeldNamingTheContentKeys`, `Protect_ContentKeyValues_AreReservedInBlocksInTheKeyTable`, `Protect_ReservationThatWouldPassTwoToThe32_IsRefusedAsKeyUnavailable` (`06` MSG-OPS-008's values); `RunAsync_TransactionAbortedTwiceWith40001_CommitsOnTheThirdRun`, `RunAsync_RetriesZero_RunsOnce`, `RunAsync_EachTransaction_SetsItsIdleTimeoutToTwiceTheDispatchTimeout` (`07` MSG-ENG-005's values); `Start_DatabaseHoldingEveryMigration_PassesTheSchemaCheck`, `Start_DatabaseHoldingALaterReleasesMigration_IsAccepted` (`06` MSG-OPS-002's values); `Start_HostProtectorIdOutsideTheRule_StopsTheStart`, `Start_HostProtectorIdOfTheRule_IsAccepted`, `AddHermes_HostSinkRegisteredBefore_IsKept` (`04` MSG-LIB-005's values); `OptionalCategoryWithAnEmailKind_WithoutPublicBaseAddress_StopsTheStartNamingIt`, `EssentialCategoryWithAnEmailKind_WithoutPublicBaseAddress_Starts`, `Category_CalledAgainOnItsBuilder_KeepsTheLastValue` (`01` MSG-SEND-002's values; `08` section 4); `InvalidKey_SystemOfferingNoTimeZoneButUtc_NamesTheTimeZone`, `Reload_EachReload_SignalsTheOptionsChange` (`06` MSG-OPS-001's values); `Tables_AreTheNamesTheContextGivesThem` (`08` section 8); `ManifestViolations_FrameworkReferenceTheSdkWrites_IsAccepted`, `ManifestViolations_ToolPackageType_IsAcceptedOnTheToolAlone` (`07` MSG-ENG-011's releases).

Tests that prove a test's own means rather than a criterion: `Listed_Section1_HoldsTheStartupResultAndToolCodes`, `Strings_EngineAssembly_HoldsTheToolsCodesAsConstants`, `FirstColumn_Section4_HoldsTheKeysWithTheirPaths`.

`build/gates/built-items.txt` lists 43 criteria: the 22 of phase 0, and MSG-OPS-001 AC1 and AC2, MSG-OPS-002 AC1, AC2 and AC3, MSG-OPS-006 AC1 and AC2, MSG-OPS-008 AC1, AC2, AC4, AC9 and AC11, MSG-LIB-003 AC1 and AC2, MSG-LIB-005 AC1 and AC2, MSG-SEND-001 AC2, MSG-SEND-002 AC2, MSG-OVR-002 AC2, MSG-ENG-003 AC1, MSG-ENG-005 AC4.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `01` MSG-SEND-002, a label longer than 128 characters | Open | Question 1 |
| `08` section 4 `Templates:Path`, the folder's existence | Open | Question 2 |
| `08` section 4 `Retry:Factor` and `Jobs:StallFactor`, their check and binding | Open | Question 3 |
| `06` MSG-OPS-002, `hermes migrate`'s answer for a role the database does not have | Open | Question 4; the database's error goes through as the tool's failure |
| `01` MSG-SEND-001 criteria 1 and 3, MSG-SEND-002 criterion 1 | 3 | They need `SendAsync` and the dispatcher |
| `01` MSG-SEND-018 criteria 1 to 3, and the indexed `contentKeys` column of a row | 3 | They need the deliveries table and check 1 of MSG-SEND-006; this phase proves the binding through the protection the send will call |
| `06` MSG-OPS-001 criterion 2, a change applying to the next delivery checked | 3 | It needs the dispatcher |
| `06` MSG-OPS-001 criterion 3 | 2 | `Push:AllowedHosts` is the push provider's key |
| `06` MSG-OPS-003 criterion 1, and `listenConnection` | 3 | They need the send in a host transaction and the dispatcher's listening connection |
| `06` MSG-OPS-005 criteria 1 and 2, the health check `hermes.dispatch` | 3 | They need a send's life and the deliveries the health check judges; the instruments are published here and recorded by the items that measure |
| `06` MSG-OPS-008 criterion 2, a password changed in `emailSubmission` | 2 | The SMTP provider reads `emailSubmission` |
| `06` MSG-OPS-008 criterion 3 | 4 | It needs the suppressions of `01` MSG-SEND-012 |
| `06` MSG-OPS-008 criteria 5 to 8 and 10 | 5 | They need the retention job |
| `06` MSG-OPS-008 criterion 9, the watch job's clear | 4 | The watch job is built in phase 4; the raise is proven here |
| `06` MSG-OPS-008 criterion 11, the SMS part, and `smsReportKey`, `emailSubmission` and `smsWe` | 2 | The SMS and SMTP providers and `Sms:ReportBaseAddress` |
| `06` MSG-OPS-008, `messaging.key.unavailable` from `SendAsync` and the `key-unavailable` and `content-rejected` writers | 3 | They need `SendAsync` and check 1 of `01` MSG-SEND-006; the reservation answers unavailable here |
| `04` MSG-LIB-003 criteria 3 and 4 | 2 | They need the providers' secrets and keys |
| `04` MSG-LIB-003 criterion 5, `SendAsync` throwing; criterion 6 | 3 | They need `SendAsync`; the `contentKeys` part is proven by `Start_ContentKeysWhereNoPartNeedsThem_IsNotRead` |
| `04` MSG-LIB-005, `IRecipientIdentity` | 4 | It is required where the preferences or push subscription group is mapped |
| `04` MSG-LIB-005, `ITemplateSource` | 3 | With the templates of `03` |
| `07` MSG-ENG-005 criterion 1 | 3 | The status and channel columns come with the deliveries table |
| `07` MSG-ENG-003, the public operations' results | 3 onward | The first public operation that answers a code is `SendAsync` |
| `07` MSG-ENG-007 criterion 1, wire spellings | 4 | The endpoints write the first wire shapes |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-002 criterion 1 | We read the packages this phase adds: Entity Framework Core 10.0.14 and the Npgsql provider 10.0.3, both of `07` MSG-ENG-009. The engine registers through `AddHermes` in the host's container, its startup checks run in the host's lifecycle, and its timer is a hosted service of the host's process. It opens no listener and starts no process; its one outside dependency is the host's PostgreSQL database. |
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds: no identifier, text, default or fixture names a host product, company or application. The test fixtures use `example.test` and the names of the specification. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each commit of this phase carries its tests with the code they prove. |
| `07` MSG-ENG-012's log gate | Each integration test that uses a person or a destination, `ContentProtectionTests` and `LogTests`, captures every logger's lines and asserts at its end that none holds them; the other integration tests use neither. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2, for every gate and the migrations gate added here: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

`src/Hermes/Migrations/20261026082731_Foundations.Designer.cs` and `src/Hermes/Migrations/MessagingContextModelSnapshot.cs` hold `#pragma warning disable 612, 618`, as `dotnet ef` writes them: the generated model may name members a later Entity Framework release marks obsolete, and the files are generated, never edited by hand. The code suppresses no other rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

| Place | Out of step | Governing item | Rule applied |
|---|---|---|---|
| `global.json` names SDK 10.0.403; `.config/dotnet-tools.json` takes `dotnet-ef` 10.0.14 | .NET 10.0.14 is a security release of 2026-10-25; the machine's SDK is 10.0.402 | `07` MSG-ENG-009, MSG-ENG-010 | The SDK is the latest .NET 10 security release, and `dotnet-ef` is at the EF Core release matching its runtime. The local runs used the published SDK 10.0.403 archive, its SHA-512 checked against the release's metadata. |
| `DeclarationCheck`, `SeamCheck` | "lower-case letters and digits" names no alphabet | `01` MSG-SEND-001, MSG-SEND-018; `04` MSG-LIB-002 | ASCII `a` to `z` and `0` to `9`: MSG-SEND-018 writes a protector's `Id` as ASCII bytes. |
| `DeclarationCheck` | MSG-SEND-002's list of startup failures does not repeat the name rule for a category | `01` MSG-SEND-001 | The name rule of MSG-SEND-001 holds for a category's name, the only place it is stated. |
| `KindBuilder`, `CategoryBuilder` | A setter called twice is in no list of startup failures | `01` MSG-SEND-001, MSG-SEND-002; `04` MSG-LIB-002 | The lists are closed and `04` gives the methods as setters, so the last call holds; `Limit` and `DeploymentLimit` add, and their duplicates fail as MSG-SEND-002 says. |
| `details.field` of `messaging.startup.declarationinvalid` | The member's spelling is not given | `01` MSG-SEND-001, MSG-SEND-002; `08` section 1 | A kind's field is the member as MSG-SEND-001 writes it; a category's is the builder method, as `08` gives `Id` for a protector. |
| `ConfigurationRules` | A year's length in the rules of `08` section 4 | `08` section 4; `05` MSG-API-005 | 365 days: `08` gives `Unsubscribe:TokenLifetime`'s default as `730.00:00:00`, which `05` calls 2 years. |
| `MigrationInput` | How `hermes migrate` reads its document is not given | `06` MSG-OPS-002; `05` MSG-API-001 | As `05` MSG-API-001 reads a body, the library's one rule for reading JSON: camelCase, case-sensitive, an unknown member ignored, `details.member` only where one member is at fault. |
| `PackageRules` | The pack rule's manifest took neither `frameworkReferences`, which the SDK writes for the framework reference `04` MSG-LIB-001 requires, nor `packageTypes`, which it writes for `Hermes.Cli`'s tool package | `04` MSG-LIB-001; `07` MSG-ENG-011 | Each is allowed, the second on the tool alone, with `ManifestViolations_FrameworkReferenceTheSdkWrites_IsAccepted` and `ManifestViolations_ToolPackageType_IsAcceptedOnTheToolAlone`. |
| `PublicSurfaceBuildTests` | The probe copied none of `Hermes.Core`'s sources, so its surface files no longer matched once the phase added types | `04` MSG-LIB-002 criterion 1 | The probe copies the project and its lock file and starts from empty surface files. |
| `ContentEnvelope` | An envelope of format `0x01` whose `Id` breaks the `Id` rule, or whose length passes the value's end | `01` MSG-SEND-018; `04` MSG-LIB-002 | It is a value in no format, and does not open, as `Rejected`. |
| `ContentProtection` | A value naming a host protector in an anonymous recipient's row | `01` MSG-SEND-018 | An anonymous recipient's values are always the library's protector's, so no host protector made it under that binding: it does not open, as `Rejected`. |
| `ContentProtection` | A library value under a `contentKeys` version this process does not hold | `06` MSG-OPS-008; `08` section 1 | It answers `KeyUnavailable` and is held naming `contentKeys`, the secret. |
| `TimerChecks` | `configuration-invalid` needs a judge that runs while the process does | `06` MSG-OPS-001, MSG-OPS-006 | It is judged on the key-table timer, which every process registering `AddHermes` runs. |
| `Alerts` | A sink that throws | `04` MSG-LIB-005; `07` MSG-ENG-003 | The failure is logged at the error level and the alert table keeps the state it recorded, so the next raise comes a repeat later. |
| `Alerts` | A condition with no scope in a table keyed by condition and scope | `04` MSG-LIB-002; `06` MSG-OPS-006 | It is kept under the empty scope, and the sink is given none. |

## Open questions

1. **`01` MSG-SEND-002, a label "longer than 128 characters" (Tier 2; also MSG-SEND-003's person reference and `PushTopic`, and `08` section 4 `Email:From:DisplayName`).** Readings: (a) UTF-16 code units; (b) Unicode scalar values; (c) grapheme clusters. Smallest fix: the unit named once in `07` or at each limit. Parked: the 128-character label check of MSG-SEND-002. Not answered.
2. **`08` section 4 `Templates:Path`, "A folder" (Tier 2).** Readings: (a) the folder must exist, a host of assembly templates alone making an empty one; (b) a well-formed path is enough, a missing folder holding no template. Smallest fix: a clause in the row. Parked: the check of `Templates:Path`. Not answered.
3. **`08` section 4 `Retry:Factor` "1 to 10" and `Jobs:StallFactor` "2 to 100" (Tier 2).** Readings: (a) whole numbers; (b) decimal ones, as `Sms:DrainFactor` is written "1.5 to 100". Smallest fix: a word in each row. Parked: the two keys' check and binding. Not answered.
4. **`06` MSG-OPS-002, a role of `applicationRoles` the database does not have (Tier 2).** The grant fails, and `08` section 1's tool codes name none for it. Readings: (a) checked before any migration applies, answering `messaging.migrate.inputinvalid` with `member` `applicationRoles`; (b) a new code, such as `messaging.migrate.grantfailed` with `role`, the migrations staying applied. Smallest fix: a clause in the `inputinvalid` row, or a new row. Parked: the tool's answer for such a role. Not answered.

## Commit and gates

The report names commit `a4c49a86f9a85fe7601c96ec62c4fc268b0ea3b7`. Each gate's result there, from the local run of `build/gates/run-gates.ps1 -Base origin/main -Head a4c49a86f9a85fe7601c96ec62c4fc268b0ea3b7` on a clean working tree:

| Gate | Result |
|---|---|
| Build and analysers, warnings as errors (`gate-build`) | Passed |
| `dotnet format --verify-no-changes` (`gate-format`) | Passed |
| Locked restore and the vulnerability audit (`gate-restore`) | Passed |
| Unit, integration and contract tests (`gate-tests`) | Passed: unit 424 (`Hermes.Tests` 221, `Hermes.Repository.Tests` 180, `Hermes.Cli.Tests` 18, `Hermes.Core.Tests` 5), integration 148 (`Hermes.Repository.Tests` 92, `Hermes.Tests` 52, `Hermes.Cli.Tests` 4), contract 59 (`Hermes.Repository.Tests` 54, `Hermes.Tests` 5); the other test projects hold no test yet |
| Public surface files up to date (`gate-public-surface`) | Passed |
| Dependency allow-list (`gate-dependency-allow-list`) | Passed |
| Project references and grants (`gate-project-references`) | Passed |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 42 commits, no leaks |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 15 commits after `main` |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 43 criteria |
| Migrations applied twice to an empty database (`gate-migrations`) | Passed: the first run applied `20261026082731_Foundations`, the second nothing, the history and schema unchanged |
| Log gate | Passed within `gate-tests`: `ContentProtectionTests` and `LogTests` find no secret, destination or person in any captured line |
| Release compatibility, sample host | No script yet (phases 5 and 6) |

No commit of this phase was pushed before this report. The pipeline's `gates` run on the commit pushed with this report is given in the message naming the pull request.
