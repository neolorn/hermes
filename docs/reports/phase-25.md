# Phase 25: the Cequens SMS package, 1.0.0-rc.7

## Items built

| Item | Tests |
|---|---|
| `04` MSG-LIB-001 and `07` MSG-ENG-001, the package's projects (`15d1625`): `src/Hermes.Sms.Cequens` references `Hermes.Core`, `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions` and `Microsoft.Extensions.Http`, its description the text of `00` section 2.2's cell; `tests/Hermes.Sms.Cequens.Tests` references it, `Hermes.Testing` (`88891b1`), xunit.v3, `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.PostgreSql`; the package grants its internals to its test project alone; `Hermes.sln`, `samples/nuget.config` and the repository's rules (the package list, the references and grants table, the public type table, the layout) take the package; no other package may reference it | `MSG_LIB_001_AC1_SmsCequensReferencingAProjectOutsideItsRowIsFound`, `MSG_LIB_001_AC1_SmsCequensReferencingTheCoreAndTheHttpFactoryIsAccepted`, `MSG_LIB_001_AC1_APackageReferencingSmsCequensIsFound`, `MSG_ENG_001_AC1_GrantOutsideTheRuleIsFound` and `MSG_ENG_001_AC1_GrantWithinTheRuleIsAccepted` (their new rows), `MSG_ENG_001_AC1_PackageWithoutItsProjectIsFound` (its new row), and the repository's existing tests over the new assembly (sealing, tokens, reflection, names, public types, codes) |
| `02` MSG-SMS-007, the registration (`ebfd7fa`): `AddHermesCequensSms(IServiceCollection)`, the package's one public type, removes every earlier `ISmsProvider` and registers the provider (transient, so it follows the lifetime of the host's `ISecretSource`), the named client `Hermes.Sms.Cequens` (no redirect, every additional handler cleared after a host's `ConfigureHttpClientDefaults`, `RemoveAllLoggers()`, the factory's own timeout kept as the outer bound) and a lifecycle service | `AddHermesCequensSms_ProviderRegisteredBefore_IsReplaced`, `AddHermesCequensSms_CalledTwice_RegistersOneProviderAndOneStartCheck`, `AddHermesCequensSms_NullServices_Throws`, `SendAsync_RateLimitedUnderAHostsRetryingHandler_IsAnsweredAfterOneRequest` |
| MSG-SMS-007, the secret `smsCequens` and its start check: in `StartingAsync`, an unanswered secret, an answer with no version, or no `ISecretSource` registered, throws `MessagingStartupException` `messaging.startup.secretunavailable`, and a highest version outside 1 to 8,192 bytes of `!` to `~` throws `messaging.startup.secretinvalid`, each with `key` `smsCequens`, through `MessagingStartupException`'s public constructor, so the core's surface is unchanged | `MSG_SMS_007_AC6_StartWithSmsCequensUnansweredFailsWithSecretUnavailable`, `StartingAsync_NoSecretSourceRegistered_FailsWithSecretUnavailable`, `StartingAsync_SecretAnsweredWithNoVersion_FailsWithSecretUnavailable`, `StartingAsync_HighestVersionOutsideTheForm_FailsWithSecretInvalid` (empty, 8,193 bytes, a space, a line feed, a byte outside ASCII), `StartingAsync_HighestVersionWithinTheForm_Starts` |
| MSG-SMS-007, the token at a call (`3c22aa1`, M-043's answers): a send or a balance that finds `smsCequens` unanswered, or its highest version outside the form, makes no request and answers `Transient(keyunavailable)`, a balance `Failed(keyunavailable)`; nothing throws | `SendAsync_SecretNoLongerAnswered_IsTransientKeyUnavailableAndMakesNoRequest`, `SendAsync_HighestVersionOutsideTheForm_IsTransientKeyUnavailableAndMakesNoRequest`, `BalanceAsync_SecretNoLongerAnswered_IsFailedKeyUnavailableAndMakesNoRequest`, `BalanceAsync_HighestVersionOutsideTheForm_IsFailedKeyUnavailableAndMakesNoRequest` |
| MSG-SMS-007, the send: one `POST https://apis.cequens.com/sms/v1/messages` with `Accept: application/json`, `Authorization: Bearer` and the highest version read at each call, and the body `messageText`, `senderName`, `messageType` (`text`, `unicode`), `recipients` (the number without its `+`) and `dlrUrl` only where the report address is set; over 20 segments, `Permanent(messagerefused)` with no request | `MSG_SMS_007_AC1_SendMakesOnePostWithTheHighestTokenAndTheBodyAndIsAcceptedWithTheSmsId`, `MSG_SMS_007_AC1_SendWithoutAReportAddressCarriesNoDlrUrl`, `MSG_SMS_007_AC1_EncodingIsSentAsItsMessageType`, `MSG_SMS_007_AC3_MessageOfTwentyOneSegmentsMakesNoRequestAndIsRefused`, `SendAsync_MessageOfTwentySegments_IsSent`, `MSG_SMS_007_AC6_NewHighestVersionIsUsedByTheNextSendWithNoRestart` |
| MSG-SMS-007, the answer table, every row: the gateway in the test process answers Cequens's documented answers (the send's 200, its 400s with codes -8, -9, -12, -13, -14, -16, -17 and -41, the 401), and, where the reference documents none, the same envelope with code -6 and the error table's text, or an empty body; a status no other row names is an answer that does not read; the call's bound is its caller's `Dispatch:Timeout`, so its row is proven under the dispatcher (`88891b1`), on PostgreSQL through `Hermes.Testing`, with `Dispatch:Timeout` at one second and a gateway that never answers | `MSG_SMS_007_AC2_EachDocumentedAnswerGivesItsAttempt` (21 rows), `MSG_SMS_007_AC2_RefusedConnectionIsTransientConnectionFailed`, `MSG_SMS_007_AC2_TlsNotCompletedIsTransientTlsFailed`, `MSG_SMS_007_AC2_NoAnswerWithinTheCallsBoundIsTransientTimeoutThroughTheDispatcher` (integration; its log lines hold no number, text, code, person reference or token), `SendAsync_ClientGivesUpBeforeAnAnswer_IsTransientTimeout` |
| MSG-SMS-007, an answer's body (`f17c1f6`): the response is taken at its headers and its body read to at most 64 KiB; a longer one, declared or streamed, does not read; a connection broken while the body is read is `connectionfailed` | `SendAsync_AnswerBodyOfALength_ReadsOnlyUpTo64KiB` (64 KiB reads, one byte more does not, declared and streamed, for a 200 and a 400), `BalanceAsync_AnswerBodyOfALength_ReadsOnlyUpTo64KiB` |
| MSG-SMS-007, the report: whatever the method, a query with one non-empty `msgid` and one `statusid` reads the provider reference `msgid` and `delivered`, `failed` or `notfinal`; any other does not read; each is answered 200 with an empty body and no content type; no request is made | `MSG_SMS_007_AC4_ReportReadsItsOutcomeWithTheProviderReferenceMsgid` (`1`, `2`, `3`), `MSG_SMS_007_AC4_ReportWithoutMsgidDoesNotRead`, `MSG_SMS_007_AC4_ReportWithoutStatusidDoesNotRead`, `ReadReportAsync_PostedReport_ReadsItsQuery`, `ReadReportAsync_MsgidGivenTwice_DoesNotRead`, `ReadReportAsync_ReportRead_MakesNoRequestToTheGateway` |
| MSG-SMS-007, the balance: one `GET https://apis.cequens.com/sms/v1/account/balance`; `Balance(data.Account.Credit)`, `Failed` with `authfailed`, `ratelimited`, `connectionfailed`, `tlsfailed`, `timeout` or `temporary` | `MSG_SMS_007_AC5_BalanceReadsTheAccountsCredit`, `MSG_SMS_007_AC5_UnauthorizedBalanceIsFailedAuthFailed`, `BalanceAsync_GatewayAnswer_IsReadAsMsgSms007Gives`, `BalanceAsync_RequestFailsBeforeAnAnswer_IsFailedWithTheTablesReason`, `BalanceAsync_ClientGivesUpBeforeAnAnswer_IsFailedTimeout`, `BalanceAsync_HighestVersion_IsTheBearerToken` |
| MSG-SMS-007, `IsValidSender` (`true` for every sender) and the logs: the package writes no log line, and its client's request logging is removed | `IsValidSender_SenderTheSenderRuleAllows_IsTrue`, `MSG_SMS_007_AC7_UnderDebugLoggingNoLineHoldsTheNumberTheTextTheTokenOrTheAnswersText` (a host client with the factory's logging shows the capture sees an address) |
| `CHANGELOG.md`: three `Added` lines under `Unreleased` | `gate-commit-messages` |
| The test-name gate's list: MSG-SMS-007 AC1 to AC7 | `gate-test-names`: 359 criteria |

The gateway's answers in the tests are those of Cequens's reference: the balance's from the saved page `get-account-balance.htm`; the send's from the reference's own Markdown, `developer.cequens.com/reference/sending-sms.md`, since the saved `Sending SMS.html` holds the example names but not their bodies; the report's query from `developer.cequens.com/reference/dlr-webhook-1.md`. No test and no code reaches Cequens.

## Items not built, and criteria proven in a later step

| Item or criterion | Step | Reason |
|---|---|---|
| The release commit `chore: release 1.0.0-rc.7` | After the answer to open question 4 | An engine change, if that is the answer, comes before it in this pull request |
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

1. **The client's bound (Tier 2).** Answered by `1c92b08` (M-043, "Answers at phase 25"): the caller's `Dispatch:Timeout`, reading (A). Criterion 2's timeout row is proven under the dispatcher (`88891b1`); no code changed.
2. **A send's answer the table does not name (Tier 2).** Answered by `1c92b08`: reading (A), `Transient(connectionfailed)` with the status, as built; the table has the row.
3. **The token at a call (Tier 3).** Answered by `1c92b08`: `Transient(keyunavailable)`, a balance `Failed(keyunavailable)`, no request and no throw; built in `3c22aa1`.
4. **What a provider's `keyunavailable` costs a delivery (Tier 3, new).** MSG-SMS-007 now says a call that finds `smsCequens` unanswered answers `Transient(keyunavailable)` "so the delivery waits as for a key this process does not hold, with no attempt spent", and M-043 says it waits "until the secret source answers again". `01` MSG-SEND-007's "Held for a key" names what holds a delivery without counting an attempt (a value that cannot open, a lookup or a push request needing a version this process does not hold), and a provider's answer is not among them. The engine follows `01`: `DispatchPass.RenewAsync` counts the attempt immediately before the call, and `RecordAsync` records a `Transient` answer through the retry schedule, so each such call spends an attempt, backs off, and the delivery ends `failed` with the reason `keyunavailable` after `Hermes:Retry:MaxAttempts` (default 8), not after `Hermes:Dispatch:MaxHold`, and raises no `key-unavailable` alert. The provider answers as MSG-SMS-007 gives; the engine is outside this phase's items. Parked: the release commit, since an engine change made in this release would have to come before it in this pull request (`07` MSG-ENG-011).

## Commit and checks

The code is `88891b1`'s. Every check below ran on it, the working tree clean:

| Check | Result |
|---|---|
| Fast checks: `dotnet format --verify-no-changes`, the build with warnings as errors and the analysers | Passed, on each commit |
| Every gate (`run-gates.ps1 -Base origin/main`), run without a pipe | Passed: all 13, among them `gate-commit-messages` (8 commits after `main`), `gate-release-compatibility` (no final release, so no break), `gate-sample-host` and `gate-test-names` (359 criteria) |
| Every test of every project, in `gate-tests` | Passed: 2,447 (1,539 unit, 819 integration, 89 contract; `Hermes.Tests` 1,051, `Hermes.Repository.Tests` 502, `Hermes.Channels.Tests` 384, `Hermes.Conversations.Tests` 217, `Hermes.Development.Tests` 114, `Hermes.Sms.Cequens.Tests` 80, `Hermes.Core.Tests` 47, `Hermes.Cli.Tests` 28, `Hermes.Testing.Tests` 24) |
| Secret scanning of the whole history (`gate-secret-scan`) | Passed: 348 commits, no leaks |

The gate of record is the pipeline's `gates` run on the report's own commit, whose identifier and result the message naming the pull request gives. The commit-message gate and the secret scan are run again on the report's commit before the push.
