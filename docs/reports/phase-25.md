# Phase 25: the Cequens SMS package, 1.0.0-rc.7

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-001 and `07` MSG-ENG-001, the package's projects (`15d1625`): `src/Hermes.Sms.Cequens` references `Hermes.Core`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Http`, its description the text of `00` section 2.2's cell; `tests/Hermes.Sms.Cequens.Tests` references it, xunit.v3 and `Microsoft.AspNetCore.Mvc.Testing`; the package grants its internals to its test project alone; `Hermes.sln`, `samples/nuget.config` and the repository's rules (the package list, the references and grants table, the public type table, the layout) take the package; no other package may reference it | `MSG_LIB_001_AC1_SmsCequensReferencingAProjectOutsideItsRowIsFound`, `MSG_LIB_001_AC1_SmsCequensReferencingTheCoreAndTheHttpFactoryIsAccepted`, `MSG_LIB_001_AC1_APackageReferencingSmsCequensIsFound`, `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound` and `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted` (their new rows), `MSG_ENG_001_AC1_PackageWithoutItsProjectIsFound` (its new row), and the repository's existing tests over the new assembly (sealing, tokens, reflection, names, public types, codes) |
| `02` MSG-SMS-007, the registration (`ebfd7fa`): `AddHermesCequensSms(IServiceCollection)`, the package's one public type, removes every earlier `ISmsProvider` and registers the provider (transient, so it follows the lifetime of the host's `ISecretSource`), the named client `Hermes.Sms.Cequens` (no redirect, every additional handler cleared after a host's `ConfigureHttpClientDefaults`, `RemoveAllLoggers()`) and a lifecycle service | `AddHermesCequensSms_ProviderRegisteredBefore_IsReplaced`, `AddHermesCequensSms_CalledTwice_RegistersOneProviderAndOneStartCheck`, `AddHermesCequensSms_NullServices_Throws`, `SendAsync_RateLimitedUnderAHostsRetryingHandler_IsAnsweredAfterOneRequest` |
| MSG-SMS-007, the secret `smsCequens` and its start check: in `StartingAsync`, an unanswered secret, an answer with no version, or no `ISecretSource` registered, throws `MessagingStartupException` `messaging.startup.secretunavailable`, and a highest version outside 1 to 8,192 bytes of `!` to `~` throws `messaging.startup.secretinvalid`, each with `key` `smsCequens`, through `MessagingStartupException`'s public constructor, so the core's surface is unchanged | `MSG_SMS_007_AC6_StartWithSmsCequensUnansweredFailsWithSecretUnavailable`, `StartingAsync_NoSecretSourceRegistered_FailsWithSecretUnavailable`, `StartingAsync_SecretAnsweredWithNoVersion_FailsWithSecretUnavailable`, `StartingAsync_HighestVersionOutsideTheForm_FailsWithSecretInvalid` (empty, 8,193 bytes, a space, a line feed, a byte outside ASCII), `StartingAsync_HighestVersionWithinTheForm_Starts` |
| MSG-SMS-007, the send: one `POST https://apis.cequens.com/sms/v1/messages` with `Accept: application/json`, `Authorization: Bearer` and the highest version read at each call, and the body `messageText`, `senderName`, `messageType` (`text`, `unicode`), `recipients` (the number without its `+`) and `dlrUrl` only where the report address is set; over 20 segments, `Permanent(messagerefused)` with no request | `MSG_SMS_007_AC1_SendMakesOnePostWithTheHighestTokenAndTheBodyAndIsAcceptedWithTheSmsId`, `MSG_SMS_007_AC1_SendWithoutAReportAddressCarriesNoDlrUrl`, `MSG_SMS_007_AC1_EncodingIsSentAsItsMessageType`, `MSG_SMS_007_AC3_MessageOfTwentyOneSegmentsMakesNoRequestAndIsRefused`, `SendAsync_MessageOfTwentySegments_IsSent`, `MSG_SMS_007_AC6_NewHighestVersionIsUsedByTheNextSendWithNoRestart`, `SendAsync_SecretNoLongerAnswered_ThrowsAndMakesNoRequest` |
| MSG-SMS-007, the answer table, every row but the timeout's (open question 1): the gateway in the test process answers Cequens's documented answers (the send's 200, its 400s with codes -8, -9, -12, -13, -14, -16, -17 and -41, the 401), and, where the reference documents none, the same envelope with code -6 and the error table's text, or an empty body | `MSG_SMS_007_AC2_EachDocumentedAnswerGivesItsAttempt` (21 rows), `MSG_SMS_007_AC2_RefusedConnectionIsTransientConnectionFailed`, `MSG_SMS_007_AC2_TlsNotCompletedIsTransientTlsFailed`, `SendAsync_ClientGivesUpBeforeAnAnswer_IsTransientTimeout` |
| MSG-SMS-007, the report: whatever the method, a query with one non-empty `msgid` and one `statusid` reads the provider reference `msgid` and `delivered`, `failed` or `notfinal`; any other does not read; each is answered 200 with an empty body and no content type; no request is made | `MSG_SMS_007_AC4_ReportReadsItsOutcomeWithTheProviderReferenceMsgid` (`1`, `2`, `3`), `MSG_SMS_007_AC4_ReportWithoutMsgidDoesNotRead`, `MSG_SMS_007_AC4_ReportWithoutStatusidDoesNotRead`, `ReadReportAsync_PostedReport_ReadsItsQuery`, `ReadReportAsync_MsgidGivenTwice_DoesNotRead`, `ReadReportAsync_ReportRead_MakesNoRequestToTheGateway` |
| MSG-SMS-007, the balance: one `GET https://apis.cequens.com/sms/v1/account/balance`; `Balance(data.Account.Credit)`, `Failed` with `authfailed`, `ratelimited`, `connectionfailed`, `tlsfailed`, `timeout` or `temporary` | `MSG_SMS_007_AC5_BalanceReadsTheAccountsCredit`, `MSG_SMS_007_AC5_UnauthorizedBalanceIsFailedAuthFailed`, `BalanceAsync_GatewayAnswer_IsReadAsMsgSms007Gives`, `BalanceAsync_RequestFailsBeforeAnAnswer_IsFailedWithTheTablesReason`, `BalanceAsync_ClientGivesUpBeforeAnAnswer_IsFailedTimeout`, `BalanceAsync_HighestVersion_IsTheBearerToken` |
| MSG-SMS-007, `IsValidSender` (`true` for every sender) and the logs: the package writes no log line, and its client's request logging is removed | `IsValidSender_SenderTheSenderRuleAllows_IsTrue`, `MSG_SMS_007_AC7_UnderDebugLoggingNoLineHoldsTheNumberTheTextTheTokenOrTheAnswersText` (a host client with the factory's logging shows the capture sees an address) |
| `CHANGELOG.md`: the `Added` line under `Unreleased` | `gate-commit-messages` |
| The test-name gate's list: MSG-SMS-007 AC1 to AC7 | `gate-test-names`: 359 criteria |

The gateway's answers in the tests are those of Cequens's reference: the balance's from the saved page `get-account-balance.htm`; the send's from the reference's own Markdown, `developer.cequens.com/reference/sending-sms.md`, since the saved `Sending SMS.html` holds the example names but not their bodies; the report's query from `developer.cequens.com/reference/dlr-webhook-1.md`. No test and no code reaches Cequens.

## Items not built, and criteria proven in a later step

| Item or criterion | Step | Reason |
|---|---|---|
| MSG-SMS-007, the named client's own bound of `Dispatch:Timeout`, and criterion 2's timeout row | After the answer | Open question 1 |
| The release commit `chore: release 1.0.0-rc.7` | After the review | The lead's message |
| The tag `v1.0.0-rc.7` | After the merge | The lead's |

## Criteria checked without a test

| Criterion | How it was checked |
|---|---|
| `02` MSG-SMS-007 criterion 8 | `git diff --stat 8df7e22 HEAD -- 'src/*/PublicAPI.*.txt'` lists `src/Hermes.Sms.Cequens/PublicAPI.Unshipped.txt` alone, and `gate-public-surface` passes. |
| `00` section 2.2, `08` sections 7 and 8 | We read the package's description against the cell, the secret's name `smsCequens` and the client's name `Hermes.Sms.Cequens` against the rows. |
| `00` MSG-OVR-001 criterion 1 | We read every file this phase changes: no value names a host product, company or application; the gateway's name is the package's, by M-043. |

## Analyser suppressions

None. This phase changes no rule of `.editorconfig`.

## Resolved by rule

1. **`InvalidRecipients`, `src/Hermes.Sms.Cequens/CequensAnswer.cs`.** The table reads "the number in `data.InvalidRecipients`"; the reference gives the member as a string listing the refused recipients, and shows only the empty one. A send carries one number (`02` MSG-CHAN-001, M-043), so a list of its refused recipients that is not empty, or blank, names that number, and the send is `Permanent(recipientrefused)`.
2. **No secret source registered, `CequensStartup.cs`.** MSG-SMS-007 names `secretunavailable` for a source that does not answer `smsCequens`; `08` section 1's row is "a secret a used part needs is not answered", so a host with no source at all fails with the same code and key, and not with `declarationmissing`, which MSG-LIB-005 keeps for the seams it requires.
3. **The report's query, `CequensSmsProvider.cs`.** MSG-SMS-007 reads "a query with one `msgid` and one `statusid`", so either given twice does not read, and an empty `msgid`, which names no message, does not read either.
4. **`Retry-After` on a 429.** The table gives `Transient(ratelimited)` and Cequens documents no such header, so none is read.
5. **The commits' scope.** `07` MSG-ENG-011's scopes name no area for the package and the scope is optional, so the phase's commits carry none.

## Open questions

1. **The client's bound, MSG-SMS-007 (Tier 2).** The named client is to be "built as `Hermes.Push` is", which times out after `Hermes:Dispatch:Timeout`, and the table's timeout row is "no answer within `Dispatch:Timeout`". `Hermes.Push` reads it through `CallTimeout`, internal to `Hermes.Channels`; the package references `Hermes.Core` alone, which offers no reading of it, and `AddHermesCequensSms` takes no configuration. As built, the client keeps the factory's own timeout (100 seconds), the provider answers `Transient(timeout)` (`Failed(timeout)` for the balance) when its client gives up, and the engine's two callers bound the call by `Dispatch:Timeout` already: the dispatcher and the balance job record their own bound's expiry as `timeout`. Parked: the client's own bound and criterion 2's timeout row. Readings, each with its smallest change:
   - (A) The caller's bound is the bound: MSG-SMS-007 says the call is bounded by its caller's token, and criterion 2's timeout row is proven through the dispatcher, which needs a test of `tests/Hermes.Sms.Cequens.Tests` over `Hermes.Testing` and PostgreSQL. No code change.
   - (B) The package reads `Hermes:Dispatch:Timeout` from the host's `IConfiguration` service at each call, `00:00:20` where unset, with `Microsoft.Extensions.Configuration.Abstractions` and `Microsoft.Extensions.Configuration.Binder` (both within its row), and sets the client's timeout from it as `Hermes.Push` does; `08` section 4's sentence on `Dispatch:Timeout` names the package among its readers.
   - (C) `AddHermesCequensSms(IServiceCollection, IConfiguration)`, as `AddHermesChannels` takes it: a change to the package's public surface as `04` gives it.
2. **A send's answer the table does not name (Tier 2).** A 200 whose `replyCode` is not 0, and a status the table names no row for (a 2xx other than 200, a 3xx, since the client follows no redirect), are built as "an answer that does not read", `Transient(connectionfailed)` with the status, since `CequensAnswer.Send` must answer every status; a different answer changes that one arm and its rows. Readings: (A) as built, the attempt retried, at the risk of a second text where the gateway took the first; (B) `Permanent(permanent)` with the status, no retry and no second text, at the risk of a text never sent.
3. **The token at a call (Tier 3).** The start checks `smsCequens`; where, later, the source answers no version or a highest version outside the form, the provider throws `InvalidOperationException` before any request, which the dispatcher records as `Transient(providerfault)` and the balance job as `Failed(providerfault)`, logging the exception's type alone; no chapter names the attempt. Readings we see, without a proposal since it concerns a credential: the fault as built; `Transient(keyunavailable)`, the delivery held with no attempt counted until `Dispatch:MaxHold`, as the push provider answers for a key version it does not hold; `Transient(authfailed)`, as a refused token.

## Commit and checks

The code is `ebfd7fa`'s. Every check below ran on it, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every unit test of every project, run here | Passed: 1,529 (`Hermes.Tests` 550, `Hermes.Channels.Tests` 384, `Hermes.Repository.Tests` 260, `Hermes.Development.Tests` 96, `Hermes.Conversations.Tests` 86, `Hermes.Sms.Cequens.Tests` 69, `Hermes.Core.Tests` 47, `Hermes.Testing.Tests` 19, `Hermes.Cli.Tests` 18) |
| The contract tests, and the integration tests the change touches (`PackTests`, `PublicSurfaceBuildTests`, `RootFileTests`) | Passed: 89 contract, 8 integration |
| `gate-public-surface`, `gate-project-references`, `gate-dependency-allow-list`, `gate-test-names` (359 criteria), `gate-release-compatibility` (no final release, so no break) | Passed |
| The commit-message gate (`gate-commit-messages -Base origin/main`) | Passed: 3 commits after `main` |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 343 commits, no leaks |

Both are run again on the report's own commit before the push.

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives.
