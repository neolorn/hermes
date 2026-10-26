# Phase 2: providers

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-002, the provider contracts (`IEmailProvider`, `IEmailSession`, `ISmsProvider`, `IPushProvider`, `Attempt`, `AttemptReason`, `EmailMessage`, `SmsMessage`, `PushRequest`, `BalanceReading`, the SMS report types) | `MSG_LIB_002_EmailMessagePrintsNeitherItsDestinationNorItsContentNorItsUnsubscribeAddress`, `MSG_LIB_002_SmsMessagePrintsNeitherItsNumberNorItsTextNorItsReferences`, `MSG_LIB_002_WebPushPrintsNeitherItsEndpointNorItsKeysNorItsPayloadNorItsTopic`, and the contract tests of phase 0 over the surface files |
| `02` MSG-SMS-001 (the segment count, `SmsText.Measure`) | `MSG_SMS_001_AC2_ATextOf160CharactersOfTheDefaultAlphabetIsOneSegmentAnd159WithOneEuroAreTwo`, `MSG_SMS_001_AC3_ATextWhose153rdSeptetWouldBeAnEscapeStartsItsSecondSegmentWithTheEscape`, `Measure_ArabicText_IsUcs2InSegmentsOf70ThenOf67`, `Measure_CharacterOutsideTheAlphabetAndItsExtension_MakesTheTextUcs2`, `Measure_EachCharacterOfTheDefaultAlphabet_TakesOneSeptet`, `Measure_EachCharacterOfTheExtensionTable_TakesTwoSeptets`, `Measure_EmptyText_IsOneSegment`, `Measure_SurrogatePairAtTheEndOfASegment_StartsTheNextSegment`, `Measure_Null_Throws` |
| `02` MSG-SMS-002 (the sender rule, the provider's narrowing of it, the allowed prefixes) | `IsSender_NameOrNumber_IsASender`, `IsSender_OutsideTheBaseRule_IsNoSender`, `IsPrefix_PlusAndUpToSixDigits_IsAPrefix`, `IsPrefix_OtherText_IsNoPrefix`, `Allows_Number_IsAllowedWhereItStartsWithAPrefix`, `Allows_PlusAlone_AllowsEveryNumber`, `Allows_NoPrefix_AllowsNoNumber`, `Allows_Null_Throws`, `StartAsync_KindSenderOutsideTheBaseRule_StopsTheStart`, `StartAsync_KindSenderTheSmsProviderRefuses_StopsTheStart`, `StartAsync_DefaultSenderTheSmsProviderRefuses_StopsTheStartNamingTheKey`, `StartAsync_SendersTheSmsProviderAccepts_Starts`, `StartAsync_SenderTheSmsProviderRefuses_StopsTheStart`, `StartAsync_SenderTheSmsProviderAccepts_Starts` |
| `02` MSG-EMAIL-002 (the address rule, and the startup check of a kind's `from` and `replyTo`) | `MSG_EMAIL_002_AC2_AKindWhoseFromIsNotAnAddressStopsTheStart`, `MSG_EMAIL_002_AC2_AKindWhoseFromHasADisplayNameOutsideItsRuleStopsTheStart`, `MSG_EMAIL_002_AC2_AKindWhoseReplyToIsNotAnAddressStopsTheStart`, `MSG_EMAIL_002_AC2_AKindWhoseFromAndReplyToAreAddressesStarts`, `IsAddress_AddrSpec_IsAnAddress`, `IsAddress_NoAddrSpecOrAnObsoleteOrCommentedForm_IsNoAddress`, `IsAddress_Length_IsAtMost254Characters`, `IsAddress_Null_Throws` |
| `02` MSG-PUSH-001 (the shipped Web Push provider) | `MSG_PUSH_001_AC1_RequestCarriesItsEncodingTtlUrgencyAndVapidAuthorization`, `MSG_PUSH_001_AC1_UrgencyOtherThanNormalIsSent`, `MSG_PUSH_001_AC1_NormalUrgencySendsNoUrgency`, `MSG_PUSH_001_AC2_MessageDecryptsUnderTheTestsOwnSection3ToItsPlaintext`, `MSG_PUSH_001_AC3_RateLimitedAnswerReturnsAtOnce`, `Decrypt_Section5Message_IsItsPlaintext`, `Derive_Section5Example_ReproducesAppendixA`, `SendAsync_Topic_IsCarried`, `SendAsync_WithinAnHourOfTheToken_ReusesItForTheSameOriginAndVersion`, `SendAsync_AnHourAfterTheToken_ReplacesIt`, `SendAsync_ServiceDoesNotAnswerWithinTheTimeout_IsTransientTimeout`, `SendAsync_ConnectionRefused_IsTransientConnectionFailed` |
| `02` MSG-PUSH-005 (the provider's classing of each answer) | `SendAsync_PushServiceAnswer_IsClassedAsMsgPush005Gives`, `SendAsync_RetryAfterAsADate_IsTheTimeLeftByTheClock`, `MSG_PUSH_001_AC3_RateLimitedAnswerReturnsAtOnce` |
| `02` MSG-PUSH-007 (signing with the subscription's key version, the form check of `pushVapidKeys`) | `SendAsync_KeyVersion_SignsWithThatVersionsKey`, `SendAsync_KeyVersionNotHeld_IsTransientKeyUnavailableAndCallsNothing`, `MSG_PUSH_007_AC3_VersionThatIsNotAValidScalarStopsTheStart`, `StartAsync_ShippedPushWithNoSecretSource_StopsTheStartAsADeclarationMissing`, `StartAsync_ShippedPushWithNoVapidKey_StopsTheStartAsASecretUnavailable`, `SendAsync_AfterTheStart_SignsWithTheHeldVersion`, `SendAsync_VersionNotHeld_AnswersKeyUnavailable`, `SendAsync_ShippedPushUnderTheEngine_SignsWithTheHeldVapidKeyAndTheConfiguredSubject` |
| `02` MSG-CHAN-001 (the named client `Hermes.Push`, the provider's replacement by the host's) | `MSG_CHAN_001_AC1_HostPushProviderReplacesTheShippedOneWithNoOtherChange`, `MSG_CHAN_001_AC1_AHostPushProviderReplacesTheShippedOneUnderTheEngine`, `MSG_CHAN_001_AC4_PushRateLimitedUnderAHostsRetryingHandlerIsAnsweredAtOnce`, `MSG_CHAN_001_AC5_PushRequestLogsNothingOfItsEndpoint`, `Register_ErrorAnswer_ReachesTheClientWithNoContent`, `Register_SuccessAnswer_KeepsItsContent`, `Register_Timeout_IsTheCallTimeoutInForce`, `Register_UnderAHostsDefaults_FollowsNoRedirectAndCarriesTheLibrarysHandlerAlone`, `SendAsync_ShippedPushUnderTheEngine_TimesOutAfterTheEnginesDispatchTimeout` |
| `02` MSG-PUSH-004 (the allowed hosts' rule and their check at the start) | `MSG_PUSH_004_AC4_AllowedHostEntryCoveringAWholeDomainStopsTheStart`, `IsHostName_DnsName_IsAHostName`, `IsHostName_OtherText_IsNoHostName`, `IsHostName_Length_IsAtMost253CharactersAnd63ALabel`, `IsAllowedHostEntry_NameOrWildcardOverThreeLabels_IsAnEntry`, `IsAllowedHostEntry_OtherText_IsNoEntry`, `Allows_Host_IsAllowedByAnExactEntryOrBySubdomainOfAWildcard` |
| `06` MSG-OPS-001 (the channels' keys of `08` section 4 but the SMTP server's and `Sms:We:BaseAddress`) | `MSG_OPS_001_AC1_EveryChannelKeyAbsentHasItsDefault`, `MSG_OPS_001_AC1_ChannelKeyPresentIsBound`, `MSG_OPS_001_AC1_ChannelKeyOutsideItsRuleStopsTheStart`, `MSG_OPS_001_AC1_ListItemOutsideItsRuleStopsTheStartNamingTheList`, `MSG_OPS_001_AC1_ChannelKeyAtTheEdgeOfItsRuleIsWithinIt`, `MSG_OPS_001_AC1_RequiredChannelKeyAbsentStopsTheStart`, `MSG_OPS_001_AC1_ValueThatPutsAnotherChannelKeyOutsideItsRuleStopsTheStartNamingThatKey`, `MSG_OPS_001_AC1_KeyOfAPartNotUsedIsNeitherReadNorRequired`, `MSG_OPS_001_AC2_ReloadedChannelValueWithinItsRuleTakesEffect`, `MSG_OPS_001_AC2_ReloadedChannelValueOutsideItsRuleLeavesTheLastValidValueInForceUntilItIsPutRight`, `MSG_OPS_001_AC2_ReloadedRequiredChannelKeyRemovedLeavesTheLastValidValueInForce`, `MSG_OPS_001_AC2_ReloadedListWithAnItemOutsideItsRuleLeavesTheWholeListInForce`, `MSG_OPS_001_AC2_ReloadedValueThatBreaksAnotherChannelKeysRuleLeavesBothInForce`, `MSG_OPS_001_AC2_ReloadedKeyOfAPartNotUsedIsNotRead`, `MSG_OPS_001_AC2_AReloadedChannelValueOutsideItsRuleRaisesTheAlertUntilItIsPutRight`, `MSG_OPS_001_AC3_OneAllowedHostLeavesThatHostAloneAllowed` |
| `04` MSG-LIB-003 (`AddHermesChannels`, the channels' checks under both registrations, the shipped provider's use) | `MSG_LIB_003_AC2_AChannelValueOutsideItsRuleStopsTheStartNamingTheKey`, `MSG_LIB_003_AC2_ARequiredChannelKeyAbsentWhereAKindUsesTheChannelStopsTheStart`, `MSG_LIB_003_AC3_AnEmailOnlyApplicationStartsWithNoSmsOrPushKeyAndNoSmsOrPushSecret`, `StartAsync_ChannelKeysOfAChannelNoKindUses_AreNeitherReadNorRequired`, `StartAsync_ApplicationThatDoesNotDispatch_StartsWithNoPushSubject`, `SendAsync_BeforeTheStart_Throws`, `StartingAsync_UnderTheEngine_LeavesTheChecksToIt` |
| `00` section 1.5, a length in characters, for `Email:From:DisplayName` and a kind's display name | `MSG_OPS_001_AC1_ChannelKeyOutsideItsRuleStopsTheStart` (an empty `Email:From:DisplayName`), `MSG_EMAIL_002_AC2_AKindWhoseFromHasADisplayNameOutsideItsRuleStopsTheStart` |
| `08` section 4, `Sms:BalanceFloor`, `Sms:DrainFactor` and `Sms:DrainMinSpend` as decimal numbers | `MSG_OPS_001_AC1_ChannelKeyPresentIsBound`, `MSG_OPS_001_AC1_ChannelKeyOutsideItsRuleStopsTheStart`, `MSG_OPS_001_AC1_ChannelKeyAtTheEdgeOfItsRuleIsWithinIt` |

Tests that prove a test's own means rather than a criterion: `Decrypt_Section5Message_IsItsPlaintext` and `Derive_Section5Example_ReproducesAppendixA` hold the test's own RFC 8291 section 3 decryption to section 5 and Appendix A, before it judges the provider's messages.

`build/gates/built-items.txt` lists 56 criteria: the 45 of phases 0 and 1, and MSG-SMS-001 AC2 and AC3, MSG-PUSH-001 AC1 to AC3, MSG-CHAN-001 AC5, MSG-OPS-001 AC3, MSG-PUSH-004 AC4, MSG-PUSH-007 AC3, MSG-EMAIL-002 AC2, MSG-LIB-003 AC3.

## Items not built, and criteria proven in a later phase

| Item or criterion | Phase | Reason |
|---|---|---|
| `02` MSG-EMAIL-001, MSG-EMAIL-003, MSG-EMAIL-004 and MSG-EMAIL-005, the SMTP provider; MSG-EMAIL-002 criterion 1; `08` section 4 `Email:Host`, `Email:Port`, `Email:Security`, `Email:TrustedAuthority`, `Email:TrustedAuthorityRevocation` and `Email:ShortLivedMaxValidity`; `06` MSG-OPS-008 `emailSubmission` and criterion 2 | 2, once answered | Parked by open question 1 |
| `02` MSG-CHAN-001 criterion 1 | 2 (email, once open question 1 is answered), 7 (SMS) | Proven here for push; the shipped SMTP and WE providers are not built |
| `02` MSG-CHAN-001 criterion 4, the SMS half | 7 | The named client `Hermes.Sms` comes with the WE adapter; the push half is proven here |
| `02` MSG-CHAN-001 criteria 2 and 3 | 3 | They need the dispatcher and the attempt records; the push provider keeps no answer's text (`Register_ErrorAnswer_ReachesTheClientWithNoContent`) |
| `02` MSG-EMAIL-007 criterion 1 | 3 | It needs the dispatcher |
| `02` MSG-SMS-001 criterion 1 | 3 | The refusal under a budget of one is the send's; the count is proven by `Measure_ArabicText_IsUcs2InSegmentsOf70ThenOf67` |
| `02` MSG-SMS-002 criteria 1 to 3 | 3 | They need `SendAsync` and the dispatcher; the rules they apply are built here |
| `02` MSG-PUSH-002 | 3 | The payload is rendered from the push template, and its size refused at the send |
| `02` MSG-PUSH-005 criteria 1 to 3, the per-subscription outcome and `push-auth-failing` | 3, 4 | They need the dispatcher and the subscriptions; the classing of each answer is built here |
| `02` MSG-PUSH-007 criteria 1 and 2 | 4 | They need the push subscription group |
| `02` MSG-PUSH-004 criteria 1 to 3 | 4 | They need the push subscription group; the rule of `Push:AllowedHosts` and the redirect the client never follows are built here |
| `04` MSG-LIB-003 criterion 4 | 2 (email, once open question 1 is answered), 6 (Janus), 7 (SMS) | `emailSubmission`, `Email:Host`, `smsWe` and `Sms:We:BaseAddress` are not built; `Push:Subject`'s part is proven by `StartAsync_ApplicationThatDoesNotDispatch_StartsWithNoPushSubject` |
| `06` MSG-OPS-001 criterion 2, a change applying to the next delivery checked | 3 | It needs the dispatcher |
| `08` section 4 `Sms:We:BaseAddress` | 7 | The WE adapter's key |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `00` MSG-OVR-001 criterion 1 | We read every file this phase adds: no identifier, text, default or fixture names a host product, company or application. The fixtures use `example.com`, `example.net`, `example.test` and RFC 8291 section 5's own values. |
| `07` MSG-ENG-008, a test with or before the code it proves | Each commit of this phase carries its tests with the code they prove. |
| `07` MSG-ENG-009, the packages added | We read each package this phase adds against the table: Lib.Net.Http.WebPush 3.3.1, and Microsoft.Extensions.Http, Configuration.Abstractions, Configuration.Binder, DependencyInjection.Abstractions, Hosting.Abstractions and Options at 10.0.14 for `Hermes.Channels`; Microsoft.AspNetCore.Mvc.Testing 10.0.14 for its tests' push service. `gate-dependency-allow-list` passes on them. |
| `07` MSG-ENG-012's log gate | `MSG_CHAN_001_AC5_PushRequestLogsNothingOfItsEndpoint` captures every logger's lines and scopes at Information for every category and asserts none holds the endpoint. |

## Criteria awaiting the pipeline's run

- `07` MSG-ENG-012 criteria 1 and 2: the `gates` job's run on this phase's pull request, whose result the message naming the pull request gives.

## Analyser suppressions

The code suppresses no rule, and this phase changes no rule of `.editorconfig`.

## Resolved by rule

| Place | Out of step | Governing item | Rule applied |
|---|---|---|---|
| `AddressRule` | An `addr-spec` admits comments, folding white space, obsolete forms and characters outside ASCII | `02` MSG-EMAIL-002; RFC 5322 section 3.4.1 | An address is an `addr-spec` of `dot-atom` or quoted local part and `dot-atom` or literal domain, in ASCII, with no CFWS and no obsolete form, at most 254 characters; RFC 5322 section 3.4.1 marks the others as forms a generator does not make. |
| `HostRule` | "A DNS name" is not given a grammar | `02` MSG-PUSH-004 | A host name of RFC 1123 section 2.1: labels of 1 to 63 letters, digits and hyphens, at most 253 characters, its last label not all digits, so no IP address reads as a name. |
| `SenderRule`, `ChannelsStartup` | A sender checked "against the registered provider's" rule where no SMS provider is registered | `02` MSG-SMS-002 | The base rule alone holds; a registered provider's `IsValidSender` narrows it. |
| `WebPushProvider` | A request for a key version this process does not hold reaches the provider | `02` MSG-PUSH-007; `01` MSG-SEND-006 | It calls nothing and answers `Transient(keyunavailable)`, the code of a key not held, so the dispatcher holds the delivery for a key. |
| `ChannelKeys` | `Sms:ReportBaseAddress`'s scheme is not given | `08` section 4; `10` | `http` or `https`: the report group is an HTTP route, and `10` names reports over plain HTTP as a case it covers. |
| `ChannelKeys` | Which channel keys are read where the providers are registered alone | `04` MSG-LIB-003; `08` section 4 | A channel's keys are read and checked only where its section is present, and `Push:Subject` only where the shipped push provider is used, as a channel's keys are required only where it is used. |
| `ChannelsConfiguration` | `configuration-invalid` where the providers are registered alone, which has no alert table | `06` MSG-OPS-001, MSG-OPS-006 | The last valid value stays in force and the key is kept as invalid; no alert is raised, since the alerts are the engine's. Under `AddHermes`, the channels' invalid keys raise the alert with the engine's. |
| `ChannelsStartup`, `MessagingStartup` | Which configuration the startup checks read | `04` MSG-LIB-003; `06` MSG-OPS-001 | The one given to `AddHermes` or `AddHermesChannels`, which the options are bound from, so a host registering over a configuration of its own is checked on what is bound. |
| `DeclarationCheck` | A kind's `EmailAddress` display name has no rule | `02` MSG-EMAIL-002; `08` section 4 | The rule of `Email:From:DisplayName`, 1 to 128 characters, as both name the sender an email shows. |
| `RepositoryFixture` | The scanner configuration rule needs each file its allow-list entries name to exist in the fixture | `07` MSG-ENG-001, MSG-ENG-012 | The fixture copies the files the scanner configuration names, test infrastructure only. |

## Open questions

1. **`07` MSG-ENG-009, MailKit 4.18.1 (Tier 2).** MailKit 4.18.1 resolves System.Security.Cryptography.Pkcs 10.0.0, which carries GHSA-846f-f764-76mx (high) and GHSA-2fmv-rv2c-59x7 (moderate), both fixed in 10.0.13, so the locked restore's audit fails. The table pins 4.18.1 and moving it is a decision-log entry, while MSG-ENG-010 takes security updates. Readings: (a) MailKit 4.18.2, which changes nothing but MimeKit 4.18.2 and so Pkcs 10.0.13; (b) MailKit 4.18.1 with Pkcs 10.0.13 pinned centrally, a new package in the table. Smallest fix: the version in MSG-ENG-009's row, or a new row. Parked: the SMTP provider (`02` MSG-EMAIL-001, MSG-EMAIL-003 to MSG-EMAIL-005), MSG-EMAIL-002 criterion 1, the SMTP server's keys of `08` section 4, `emailSubmission` and `06` MSG-OPS-008 criterion 2, `02` MSG-CHAN-001 criterion 1's email half and `04` MSG-LIB-003 criterion 4's email part. Not answered.

## Commit and gates

The report names commit `685e3bd76e3dd9f829c13a69f64799e46bbf89e5`. Each gate's result there, from the local run of `build/gates/run-gates.ps1 -Base origin/main -Head 685e3bd76e3dd9f829c13a69f64799e46bbf89e5` on a clean working tree:

| Gate | Result |
|---|---|
| Build and analysers, warnings as errors (`gate-build`) | Passed |
| `dotnet format --verify-no-changes` (`gate-format`) | Passed |
| Locked restore and the vulnerability audit (`gate-restore`) | Passed |
| Unit, integration and contract tests (`gate-tests`) | Passed: unit 773 (`Hermes.Tests` 272, `Hermes.Channels.Tests` 266, `Hermes.Repository.Tests` 180, `Hermes.Core.Tests` 37, `Hermes.Cli.Tests` 18), integration 158 (`Hermes.Repository.Tests` 92, `Hermes.Tests` 56, `Hermes.Cli.Tests` 10), contract 59 (`Hermes.Repository.Tests` 54, `Hermes.Tests` 5); the other test projects hold no test yet |
| Public surface files up to date (`gate-public-surface`) | Passed |
| Dependency allow-list (`gate-dependency-allow-list`) | Passed |
| Project references and grants (`gate-project-references`) | Passed |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 56 commits, no leaks |
| Commit message format, changelog line and released files (`gate-commit-messages`) | Passed: 7 commits after `main` |
| Acceptance-criterion test names (`gate-test-names`) | Passed: 56 criteria |
| Migrations applied twice to an empty database (`gate-migrations`) | Passed: the first run applied `20261026082731_Foundations`, the second nothing |
| Log gate | Passed within `gate-tests`: `MSG_CHAN_001_AC5_PushRequestLogsNothingOfItsEndpoint`, `ContentProtectionTests` and `LogTests` find no secret, destination or person in any captured line |
| Release compatibility, sample host | No script yet (phases 5 and 6) |

The pipeline's run on the commit pushed with this report is given in the message naming the pull request.
