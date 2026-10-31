# 04 — Library contract

What a host references, what it calls, what it supplies, and what the library promises
not to break.

---

**MSG-LIB-001** — The library SHALL ship as the packages of `00` section 2.2, and each
project SHALL reference only what this item allows it.

**Values (M-001, M-003, M-013, M-015).**

| Project | May reference |
|---|---|
| `Hermes.Core` | The base class library only |
| `Hermes.Channels` | `Hermes.Core`; MailKit; Lib.Net.Http.WebPush; the Microsoft.Extensions abstractions of `07` MSG-ENG-009 (its lifecycle services run the startup checks, since reading secrets is asynchronous) |
| `Hermes` | `Hermes.Core`, `Hermes.Channels`; EF Core, its relational layer and the Npgsql provider; Fluid.Core; ASP.NET Core's shared framework |
| `Hermes.Testing` | `Hermes.Core`, `Hermes.Channels`, `Hermes` |
| `Hermes.Cli` (a .NET tool, command `hermes`) | `Hermes` |
| `Hermes.Development` | `Hermes.Core`, `Hermes.Channels` (and MimeKit through it, for the email it keeps and the links it reads); ASP.NET Core's shared framework |
| `tests/Hermes.Tests` | `Hermes`, `Hermes.Testing` and `Hermes.Development`, whose group its endpoint contract test maps and whose routes, bodies, codes and keys the release set reads with the engine's (MSG-LIB-006; `07` MSG-ENG-006), and the test packages of `07` MSG-ENG-009 |
| Any other test project | Its source project, `Hermes.Testing`, and the test packages of `07` MSG-ENG-009 |
| `tests/Hermes.Repository.Tests` | Every shipped project, whose built assemblies its contract tests read (`07` MSG-ENG-004), and the test packages of `07` MSG-ENG-009 |
| The sample host and its test project | The packed packages above, and for its tests the test packages of `07` MSG-ENG-009 |

Every shipped project references `Microsoft.CodeAnalysis.PublicApiAnalyzers` and every
packed project `MinVer`, both as build-only references; `Hermes` references
`Microsoft.EntityFrameworkCore.Design` as a build-only reference for making migrations.
A project that references ASP.NET Core's shared framework, or a project that does,
references the framework itself (`FrameworkReference` `Microsoft.AspNetCore.App`), so no
assembly the framework serves resolves as a package copy; `Hermes.Core` and
`Hermes.Channels` never do. No package names any other library of a host (M-020). The
library's packages depend on one another at exactly the version packed (`[v]`, written
as `07` MSG-ENG-011 gives), since they are released together and grant one another their
internals (`07` MSG-ENG-001), which the release gate does not guard (MSG-LIB-006): NuGet
then reports a host's mix of versions at its restore (NU1605 where a direct reference
takes a lower version, NU1608 a higher one, NU1107 where two exact ranges meet), where a
minimum version would let a higher one through unreported, to fail at run time.
`Hermes.Conversations` is release 2's (`09`).

*Source: M-001, M-003, M-012, M-013, M-015, M-020, M-024, M-025*

**Acceptance criteria**
1. A test reads each project's references and fails on one this table does not allow.
2. `Hermes.Channels` references neither EF Core, nor ASP.NET Core, nor Fluid.
3. A test packs the packages from one commit and fails on a dependency of one of them on
   another of the library's packages that is not the exact version packed (`[v]`).

---

**MSG-LIB-002** — The public surface SHALL be exactly what the packages' public API
files list, each public type SHALL have the shape below, and a change to it SHALL be
visible in review.

**Values (M-012, M-026).** Data types are records with `init` members, so that a later
release adds members without a break, a non-nullable member being `required`; every
asynchronous member takes a `CancellationToken` as its last parameter and answers a
`ValueTask`. A type described as having cases is an abstract record whose cases are
nested sealed records of those names, made with `new`
(`new Attempt.Accepted(reference)`), which a seam reads by pattern matching; a value of
any other type derived from it is a fault of the code that made it (for `Recipient`, the
unknown case below). An enumeration is a C# `enum` whose members are named, and declared
in the order, as `08` sections 2, 3 and 6 give; each member carries its wire spelling by
`JsonStringEnumMemberName`, and wherever the library writes a member as text (a body, a
column, a metric tag, an alert's details, a log entry) it writes that spelling. A record
that carries a destination, content, a token, a key, an endpoint or a person reference
(`Send`, `EmailMessage`, `SmsMessage`, `PushRequest.WebPush`, `ConsentQuery`,
`DeliveryExport`, `Recipient.Person`, `DeliveryNotice`, `IdempotencyKeyExport`,
`InAppEntry`, `InAppEntryExport`, whose `Key` a host may build from a person's data,
every case of `SendValue`, whose values are a template's content, and `InboxMessage`,
`InboxEmail`, `InboxSms`, `InboxPush` and `InboxNotification`) overrides `PrintMembers`
to print none of them, so a log line that formats one writes none.

**Values and identifiers** (`Hermes.Core`):

| Type | Shape |
|---|---|
| `Send` | `string Kind`, `Recipient Recipient`, `string? Email`, `string? Phone`, `string? Language`, `string? TimeZone`, `IReadOnlyDictionary<string, SendValue> Values`, `IReadOnlyList<Channel>? Channels`, `DateTimeOffset? NotBefore`, `string? IdempotencyKey`, `string? PushTopic` (`01` MSG-SEND-003) |
| `SendValue` | Cases `PlainText(string Value)` (named so, since `Text` would match the namespace `System.Text`, CA1724), `WholeNumber(long Value)`, `DecimalNumber(decimal Value)`, `Flag(bool Value)`, `Instant(DateTimeOffset Value)` and `List(IReadOnlyList<IReadOnlyDictionary<string, SendValue>> Items)`, a list's entries holding no list (`03` MSG-TPL-002) |
| `Recipient` | Cases `Person(string Reference)` and `Anonymous()`. The set is open: a later release MAY add a case, and a seam meeting a case it does not know answers as for a refusal (consent `NotGiven`) |
| `SendId`, `DeliveryId`, `EntryId` | `readonly record struct SendId(Guid Value)`, `readonly record struct DeliveryId(Guid Value)` and `readonly record struct EntryId(Guid Value)`, UUID version 7 |
| `SendReceipt` | `SendId Send`, `IReadOnlyList<DeliveryReceipt> Deliveries`; `DeliveryReceipt` is `Channel Channel`, `DeliveryId Delivery` |
| `SendRecord` | `SendId Send`, `string Kind`, `IReadOnlyList<DeliveryRecord> Deliveries`; `DeliveryRecord` is `DeliveryId Delivery`, `Channel Channel`, `DeliveryStatus Status`, `AttemptReason? Reason`, `DateTimeOffset RecordedAt`, `DateTimeOffset LastMovedAt`, `IReadOnlyList<AttemptRecord> Attempts`; `AttemptRecord` is `int Number`, `DateTimeOffset At`, `AttemptClass? AnswerClass` (none while it has no answer), `AttemptReason? Reason`, `IReadOnlyList<CallRecord> Calls`; `CallRecord` is `AttemptClass? AnswerClass` (likewise), `AttemptReason? Reason`, `int? Status`, `PushService? Service` (`01` MSG-SEND-007) |
| `Result`, `Result<T>`, `Failure` | `Result`: `bool IsSuccess`, `Failure? Failure`, and the factories `static Result Success()`, `static Result Fail(Failure failure)`, `static Result<T> Success<T>(T value)` and `static Result<T> Fail<T>(Failure failure)`. `Result<T>`: `bool IsSuccess`, `T? Value`, `Failure? Failure`. `Failure`: `string Code` of `08` section 1 and `IReadOnlyDictionary<string, string> Details` |
| `MessagingStartupException` | The exception a "(startup)" code of `08` section 1 is thrown in when the host starts: `string Code`, `IReadOnlyDictionary<string, string> Details` |
| `EmailAddress` | `string Address`, `string? DisplayName` |
| `CategoryPreferences`, `ChannelPreference` | `string Category`, `bool Essential`, `IReadOnlyList<ChannelPreference> Channels`; `Channel Channel`, `bool On` |
| `InAppPage`, `InAppEntry` | `IReadOnlyList<InAppEntry> Entries`, `string? Next` (the next page's cursor, none on the last); `EntryId Entry`, `string Kind`, `string Category`, `DateTimeOffset WrittenAt`, `DateTimeOffset? ReadAt`, `string Language` (the one it reads in), `string Title`, `string? Body`, `string? Target`, `IReadOnlyDictionary<string, SendValue> TargetValues` (`02` MSG-INAPP-003) |
| `SubjectExport` | `IReadOnlyList<CategoryPreferences> Preferences`; `IReadOnlyList<SubscriptionExport> Subscriptions`, each `PushService Service`, `DateTimeOffset MadeAt`, `DateTimeOffset? LastAcceptedAt`; `IReadOnlyList<DeliveryExport> Deliveries`, each `string Kind`, `Channel Channel`, `DeliveryStatus Status`, `DateTimeOffset RecordedAt`, `DateTimeOffset LastMovedAt`, `string? Destination`, `IReadOnlyDictionary<string, string> Content` (by part), `bool ContentDeleted` (past content retention, so it carries no destination or content), `bool Erased` (its values' key was erased, so it carries no destination or content), `bool Rejected` (a value did not open under its binding, so it carries none either); `IReadOnlyList<InAppEntryExport> Entries`, each `string Kind`, `string Category`, `DateTimeOffset WrittenAt`, `DateTimeOffset? ReadAt`, `IReadOnlyDictionary<string, string> Content` (by part and language, `title.en`), `string? Target`, `IReadOnlyDictionary<string, SendValue> TargetValues`, `bool Erased` and `bool Rejected` (as for a delivery, so it carries no content or target values); `IReadOnlyList<IdempotencyKeyExport> IdempotencyKeys`, each `string Kind`, `string Key`, `DateTimeOffset RecordedAt`, the person's live idempotency rows (`01` MSG-SEND-020) |
| Enumerations | `Channel`, `DeliveryStatus`, `AttemptClass`, `AttemptReason`, `PushUrgency`, `PushService`, `PushPlatform`, `SmsEncoding`, `SmsReportOutcome`, `ConsentAnswer`, with the members of `08` sections 2, 3 and 6 that are built |

**Operations** (`Hermes.Core`, implemented by `Hermes`):

| Type | Members |
|---|---|
| `IMessages` | `SendAsync(Send, CancellationToken)` and `SendAsync(Send, DbTransaction, CancellationToken)` answer `Result<SendReceipt>`, the second holding the send's advisory locks until the host's transaction ends (one per `hashKeys` version for a person and one for an idempotency key, each a slot of PostgreSQL's shared lock table, so a host records a large batch in transactions of bounded size, `01` MSG-SEND-004); `CancelAsync(SendId, ...)` answers `Result<IReadOnlyList<DeliveryId>>`; `ReadAsync(SendId, ...)` answers `Result<SendRecord>` (`01`) |
| `IPreferences` | `ReadAsync(string person, ...)` answers `IReadOnlyList<CategoryPreferences>`, every declared category with the channels its kinds use; `SetAsync(string person, string category, Channel channel, bool isOn, ...)` answers `Result` (`01` MSG-SEND-010) |
| `ISuppressions` | `AddAsync(Channel channel, string destination, string category, ...)` and `RemoveAsync(...)` with the same parameters answer `Result` (`01` MSG-SEND-012) |
| `ISubjects` | `EraseAsync(string person, ...)` answers `Result` (erasing a person the library holds nothing of succeeds and does nothing, save where it answers `messaging.release.newer` or `messaging.key.unavailable`, `01` MSG-SEND-020; it waits for every open transaction that recorded a send for the person, so a host never calls it while such a transaction of its own is open, `01` MSG-SEND-004); `ExportAsync(string person, ...)` answers `Result<SubjectExport>` (`01` MSG-SEND-020) |
| `IInApp` | `ListAsync(string person, string? language, string? cursor, int? size, ...)` answers `Result<InAppPage>`; `UnreadCountAsync(string person, ...)` answers `int`; `MarkReadAsync(string person, EntryId entry, ...)` answers `Result`; `MarkAllReadAsync(string person, ...)` answers `int`, the number of entries it marked (`02` MSG-INAPP-003) |

**Provider contracts** (`Hermes.Core`):

| Type | Members |
|---|---|
| `Attempt` | Cases `Accepted(string? ProviderReference)`, `Transient(AttemptReason Reason, int? Status, TimeSpan? RetryAfter)`, `Permanent(AttemptReason Reason, int? Status)`, `Gone(AttemptReason Reason, int? Status)` (`02` MSG-CHAN-001) |
| `IEmailProvider`, `IEmailSession` | `OpenAsync(...)` answers an `IEmailSession` (`IAsyncDisposable`), whose `SendAsync(EmailMessage, ...)` answers an `Attempt` (`02` MSG-EMAIL-005). `EmailMessage`: `EmailAddress From`, `EmailAddress? ReplyTo`, `string ToAddress`, `string Subject`, `string Text`, `string? Html`, `string MessageId`, `Uri? Unsubscribe`, `string Language` |
| `ISmsProvider` | `SendAsync(SmsMessage, ...)` answers an `Attempt`; `IsValidSender(string sender)` answers a `bool`; `BalanceAsync(...)` answers a `BalanceReading`, with cases `Balance(decimal Units)`, `NotSupported()` and `Failed(AttemptReason Reason)`; `ReadReportAsync(SmsReportRequest, ...)` answers an `SmsReportReading` (`02` MSG-SMS-004, MSG-SMS-006). `SmsMessage`: `string ToNumber`, `string Sender`, `string Text`, `string DeliveryReference`, `SmsEncoding Encoding`, `int Segments`, `Uri? ReportAddress` |
| `SmsText` | A static class; `Measure(string text)` answers an `SmsMeasure` (`SmsEncoding Encoding`, `int Segments`) by `02` MSG-SMS-001's rule, so a host that builds an `SmsMessage` itself fills both members as the engine does |
| `SmsReportRequest`, `SmsReportReading` | Request: `string Method`, `IReadOnlyList<KeyValuePair<string, string>> Query`, `Form` and `Headers`, `string? ContentType`, `ReadOnlyMemory<byte> Body` (at most 16 KiB). Reading: `bool Reads`, `string? DeliveryReference`, `string? ProviderReference`, `SmsReportOutcome? Outcome`, `SmsReportResponse Response` (`int Status`, `string? ContentType`, `ReadOnlyMemory<byte> Body`) |
| `IPushProvider` | `PushPlatform Platform`; `SendAsync(PushRequest, ...)` answers an `Attempt` (`02` MSG-PUSH-005, MSG-PUSH-008). `PushRequest` has an abstract get-only `PushPlatform Platform`, which each case answers with its own (the one member not `init`, since a case's platform is fixed), and cases, the first release's `WebPush(string Endpoint, string P256dh, string Auth, int KeyVersion, ReadOnlyMemory<byte> Payload, TimeSpan Ttl, PushUrgency Urgency, string? Topic)`, answering `webpush` |

**Seams a host supplies** (MSG-LIB-005; `Hermes.Core`):

| Seam | Members |
|---|---|
| `ISecretSource` | `ReadAsync(string name, ...)` answers `SecretVersions?`: `IReadOnlyList<SecretVersion> Versions`, each `int Version` (from 1) and `ReadOnlyMemory<byte> Value` (`06` MSG-OPS-008) |
| `IRecipientIdentity` | `CurrentAsync(...)` answers `string?`: the person reference of the person signed in for the current request, read by the host from its own request context, or none |
| `IConsentSource` | `ConsentsAsync(ConsentQuery, ...)` answers a `ConsentAnswer`; `ConsentQuery` is `Recipient Recipient`, `string? Destination`, `string Purpose`, `Channel Channel` (`01` MSG-SEND-011) |
| `IContentProtector` | `string Id` (1 to 32 lower-case letters and digits, never `messaging`); `ProtectAsync(string person, ReadOnlyMemory<byte> plaintext, string context, ...)` answers `byte[]`; `UnprotectAsync(string person, ReadOnlyMemory<byte> protectedValue, string context, ...)` answers an `UnprotectResult`, with cases `Opened(ReadOnlyMemory<byte> Plaintext)`, `Erased()`, `KeyUnavailable()` and `Rejected()` (the value does not open under its binding); a call a pass, a job, an export or a push registration makes is bounded by `Hermes:Dispatch:Timeout` (`01` MSG-SEND-007, MSG-SEND-018, MSG-SEND-020; `02` MSG-PUSH-003) |
| `ITemplateSource` | `ReadAsync(TemplateKey, ...)` answers `string?`, none for a key it does not recognise, so a later key's shape never breaks a source; `TemplateKey` is `string? Kind`, `Channel? Channel`, `string? Page` (the first two or the last set), `string Language`, `string Part` (`03` MSG-TPL-006) |
| `IDeliveryObserver` | `ObserveAsync(DeliveryNotice, ...)` answers `ValueTask`; a notice is taken when it returns, and not taken when it throws or runs past `Hermes:Dispatch:Timeout` (`01` MSG-SEND-017). `DeliveryNotice`: `SendId Send`, `DeliveryId Delivery`, `string Kind`, `string Category`, `Channel Channel`, `string? Person`, `DeliveryStatus Status`, `AttemptReason? Reason`, `DateTimeOffset At`; never a destination or content |
| `IAlertSink` | `RaiseAsync(string condition, string? scope, IReadOnlyDictionary<string, string> details, ...)` and `ClearAsync(string condition, string? scope, ...)` answer `ValueTask`, the condition, scope and details as `08` section 5 gives them; a call a pass or job makes is bounded by `Hermes:Dispatch:Timeout` (`06` MSG-OPS-006; `01` MSG-SEND-007) |

**Registration and the rest:**

| Package | Type | Members |
|---|---|---|
| `Hermes.Channels` | `MessagingChannelsServiceCollectionExtensions` | `AddHermesChannels(IServiceCollection, IConfiguration)` answers the `IServiceCollection` |
| `Hermes` | `MessagingServiceCollectionExtensions` | `AddHermes(IServiceCollection, IConfiguration, Action<DeclarationBuilder>)` answers the `IServiceCollection` (MSG-LIB-003) |
| | `DeclarationBuilder` | `Kind(string name, string category)` answers a `KindBuilder`; `Retire(string name)` answers the `DeclarationBuilder` (`01` MSG-SEND-001); `Category(string name)` answers a `CategoryBuilder`; `Templates(Assembly assembly)` answers the `DeclarationBuilder` (`03` MSG-TPL-006) |
| | `KindBuilder` | `Email(EmailAddress? from = null, EmailAddress? replyTo = null)`, `Sms(int maxSegments = 1, string? sender = null)`, `Push(TimeSpan? ttl = null, PushUrgency urgency = PushUrgency.Normal)`, `InApp(string? target = null, IReadOnlyList<string>? targetValues = null)`, `ExpiresAfter(TimeSpan duration)`, each answering the builder (`01` MSG-SEND-001) |
| | `CategoryBuilder` | `Essential()`, `RequiresConsent(string purpose)`, `QuietHours(bool apply)`, `Limit(Channel channel, int count, TimeSpan window)`, `DeploymentLimit(Channel channel, int count, TimeSpan window)`, `Label(string language, string text)`, each answering the builder (`01` MSG-SEND-002) |
| | `MessagingEndpointRouteBuilderExtensions` | The five mapping methods of MSG-LIB-004 in `Hermes` |
| | `IDispatcher` | `RunPassAsync(...)` runs one pass of each channel and answers the number of deliveries they took, in an application with `Hermes:Dispatch:Enabled` on (where it is off, it throws `InvalidOperationException` before it claims anything); a pass whose token is cancelled stops as `01` MSG-SEND-007 gives and the call then throws `OperationCanceledException` (`01` MSG-SEND-007) |
| `Hermes.Testing` | `InMemoryEmailProvider`, `InMemorySmsProvider`, `InMemoryPushProvider`, `FixedSecretSource`, `TestDispatcher`, `MessagingTestDatabase`, `MessagingTestingServiceCollectionExtensions` | The members of MSG-LIB-008 |
| `Hermes.Development` | `MessagingDevelopmentExtensions` | `AddHermesDevelopmentInbox(IHostApplicationBuilder)` answers the builder; `MapHermesDevelopmentInbox(IEndpointRouteBuilder, string prefix)` answers the group's `RouteGroupBuilder`, which holds no endpoint outside Development (`02` MSG-DEV-001; `05` MSG-API-007) |
| | `DevelopmentInbox` | `DevelopmentInbox()` reads the default folder and `DevelopmentInbox(string folder)` the one given; `string Folder`; `DateTimeOffset MadeAt`; `ListAsync(InboxFilter filter, ...)` answers `IReadOnlyList<InboxMessage>`, newest first; `LatestAsync(InboxFilter filter, ...)` answers `InboxMessage?`; `WaitForAsync(InboxFilter filter, TimeSpan timeout, ...)` answers an `InboxMessage` and throws `TimeoutException` once `timeout` has passed with none; `ClearAsync(...)` answers `ValueTask` (`02` MSG-DEV-006) |
| | `InboxFilter` | `string? To`, `string? Person`, `Channel? Channel`, `string? App`, `string? Kind`, `DateTimeOffset? After` |
| | `InboxMessage` | `string Id`, `Channel Channel`, `string App`, `DateTimeOffset At`, `string? To`, `string? Person`, `string? Kind`, `string? Category`, `string? Language`, `string Summary`, `string Text`, `IReadOnlyList<string> Links`, `InboxEmail? Email`, `InboxSms? Sms`, `InboxPush? Push` (`02` MSG-DEV-003; `05` MSG-API-007) |
| | `InboxEmail`, `InboxSms`, `InboxPush`, `InboxNotification` | `InboxEmail`: `EmailAddress From`, `EmailAddress? ReplyTo`, `string Subject`, `string Text`, `string? Html`, `string MessageId`, `Uri? Unsubscribe`, `IReadOnlyList<KeyValuePair<string, string>> Headers`, `string EmlPath` (the `.eml` beside the record). `InboxSms`: `string Sender`, `string Text`, `SmsEncoding Encoding`, `int Segments`, `string DeliveryReference`. `InboxPush`: `PushService Service`, `InboxNotification Notification`, `TimeSpan Ttl`, `PushUrgency Urgency`, `string? Topic`. `InboxNotification`: `string Title`, `string Body`, `string? Url`, `string? Tag`, `string? Lang`, `string? Dir`, `string? Icon`, `string? Badge` |

Everything else is internal: the shipped providers, their options, the endpoints'
request and response types (their wire shape is the contract, `05`), and the tables.

*Source: M-004, M-005, M-008, M-012, M-020, M-024, M-026*

**Acceptance criteria**
1. The build fails when a public member is added or removed without its public API file
   changing.
2. The public API files list each type and member of the tables above and of
   MSG-LIB-008, with the types given, and no other public type.

---

**MSG-LIB-003** — A host SHALL register the library with one call, declaring what it
sends in that call, and the declarations and the configuration SHALL be validated when
the application starts.

**Values (M-005, M-012, M-026).**
`services.AddHermes(configuration, declare => { ... })` binds the configuration section
`Hermes` (through the configuration binder's source generator), registers the engine,
the shipped providers (as `AddHermesChannels` does), the dispatcher and the background
jobs (`06` MSG-OPS-004), and runs every startup check of `01`, `02`, `03` and `06`, and
the key table's start statement (`06` MSG-OPS-008), in the `StartingAsync` of an
`IHostedLifecycleService`, which the host calls on every such service before any hosted
service's `StartAsync`, so they complete before a host's own hosted service starts or
the application serves a request; a failure stops the start with a
`MessagingStartupException` carrying the code `08` gives it, and a call to `IMessages`,
`IPreferences`, `ISuppressions`, `ISubjects`, `IInApp` or `IDispatcher` before they
complete throws `InvalidOperationException`, a fault of the calling code. The providers'
own checks run the same way, under `AddHermesChannels` too, and a shipped provider
called before they complete throws `InvalidOperationException`.
`services.AddHermesChannels(configuration)` registers the providers alone, with their
own checks, for an application that only hands messages to a provider; the engine's keys
(`08` section 4 names them) and its culture and time zone checks (`06` MSG-OPS-001)
apply only where `AddHermes` is registered. A host replaces a shipped provider or a seam
by registering its own before or after the call.

A channel is **used** where a declared kind uses it, in an application that registers
the providers alone where its configuration section (`Hermes:Email`, `Hermes:Sms`,
`Hermes:Push`) is present; the shipped provider of a channel is **used** where the
channel is, the host registers no provider of its own for it (the development inbox's
count as the host's own, `02` MSG-DEV-001), and the application dispatches
(`Hermes:Dispatch:Enabled` on, whether or not its hosted dispatcher runs,
`Hermes:Dispatch:Hosted`) or registers the providers alone, whatever
`Hermes:Dispatch:Enabled` says; so an application that only records sends, and runs no
such transport, reads no provider's secret. Where the report group is mapped
(MSG-LIB-004), the host's SMS provider's `ReadReportAsync` is called there
(MSG-LIB-005). A channel is **dispatched** in an application where it is used and the
application dispatches or registers the providers alone, so its passes, the hosted
dispatcher's or those `RunPassAsync` runs (`01` MSG-SEND-007), or its providers can send
on it; the keys those passes need are read where it is (`06` MSG-OPS-008). A channel's
own keys (the sender, the rate, the allowed prefixes and hosts) are required where the
channel is used; a shipped provider's keys and secrets (the SMTP server's keys and
`emailSubmission`, and `Push:Subject`) only where that shipped provider is used (`08`
section 4). `AddHermes` registers `IMessages`, `IPreferences`, `ISuppressions`,
`ISubjects` and `IInApp` as scoped services. An application **records sends** where
`Hermes:Messages:Enabled` is on (the default); where it is off, `IMessages.SendAsync`
throws `InvalidOperationException`, a fault of the calling code, so an application that
only maps the unsubscribe or preferences group holds no key that opens content (`06`
MSG-OPS-008).

*Source: M-005, M-007, M-012, M-020, M-022, M-024, M-026*

**Acceptance criteria**
1. A host with valid declarations and configuration starts with the one call.
2. A configuration value outside its rule (`08` section 4) stops the start, naming the
   key.
3. An application whose kinds use only email, and that maps neither the report nor the
   push subscription group, starts with no SMS or push key and no SMS or push secret.
4. An application with `Hermes:Dispatch:Enabled` off that registers the providers alone
   for no channel starts with no `emailSubmission`, `Email:Host` or `Push:Subject`; one
   that registers the providers alone with `Hermes:Email` present does not start without
   `emailSubmission` (M-020).
5. An application with `Hermes:Messages:Enabled` and `Hermes:Dispatch:Enabled` off,
   `Hermes:Jobs:Enabled` off and no push subscription group starts without
   `contentKeys`, and its `SendAsync` throws.
6. With a host's hosted service registered before `AddHermes` that sends in its
   `StartAsync`, the send is recorded only after the startup checks ran.

---

**MSG-LIB-004** — Every endpoint group of `05` SHALL be mapped by its own call, under
the prefix the host chooses, so a host mounts each where it wants and leaves out the
rest.

**Values (M-001, M-024, M-026).** `MapHermesPreferences`, `MapHermesPushSubscriptions`,
`MapHermesInApp`, `MapHermesUnsubscribe`, `MapHermesSmsReports` and
`MapHermesDevelopmentInbox`, each an extension of `IEndpointRouteBuilder` taking the
prefix and answering the group's `RouteGroupBuilder`:
- the preferences, push subscription and in-app groups need the identity seam
  (MSG-LIB-005), read JSON bodies only as `application/json` (so a cross-site form
  cannot reach them), and a host MAY add its own requirements (authorisation, rate
  limiting, request-forgery protection) as for any of its endpoints; `GET /push/key`
  calls `AllowAnonymous()`, so no authorisation requirement the host adds to the group
  reaches it;
- the unsubscribe and report groups are called by mail providers and the SMS provider,
  which hold no session: they call `AllowAnonymous()` and `DisableAntiforgery()` and
  read their forms themselves, and a host adds no authorisation and no forgery
  protection to them, which would refuse RFC 8058's one-click `POST` and the provider's
  reports;
- `MapHermesDevelopmentInbox` of `Hermes.Development` maps the development inbox's group
  (`05` MSG-API-007) in Development alone: it calls `AllowAnonymous()`, since a
  developer's tools hold no session, and is guarded as `02` MSG-DEV-005 gives.

*Source: M-001, M-024, M-026*

**Acceptance criteria**
1. Each group works mapped alone in an application with none of the others.
2. A group mapped under `/notifications` answers there and nowhere else.
3. The one-click `POST` succeeds in an application whose fallback authorisation policy
   requires a signed-in person and which registers antiforgery.

---

**MSG-LIB-005** — A host SHALL supply what only it knows through the seams of
MSG-LIB-002, and the library SHALL refuse to start without a required one.

**Values (M-005, M-006, M-007, M-026).**

| Seam | Required | `details.key` |
|---|---|---|
| `ISecretSource` | Always | `secretSource` |
| `ISmsProvider` | Where SMS is used (MSG-LIB-003), or the report group is mapped: the library ships none (`02` MSG-CHAN-001) | `smsProvider` |
| `IRecipientIdentity` | Where the preferences, push subscription or in-app group is mapped | `recipientIdentity` |
| `IConsentSource` | Where a category requires consent (`01` MSG-SEND-011) | `consentSource` |
| `IContentProtector` | No: the library's own is used, and always kept for reading values it made and for every anonymous recipient's values. A host MAY register several of its own: the last registered writes, a value is opened by the registered protector whose `Id` its envelope names, and two with one `Id` stop the start with `messaging.startup.declarationinvalid` | |
| `ITemplateSource` | No: the library's own is used (`03` MSG-TPL-006) | |
| `IDeliveryObserver` | Where `Hermes:Observer:Enabled` and `Hermes:Dispatch:Enabled` are both on (`01` MSG-SEND-017) | `deliveryObserver` |
| `IAlertSink` | No: the library's own logs each condition at the error level | |
| `TimeProvider` | No: the system clock | |

A required seam missing stops the start with `messaging.startup.declarationmissing`, its
`details.key` as above.

*Source: M-005, M-006, M-007, M-022, M-026*

**Acceptance criteria**
1. Each required seam, missing where it is required, stops the start naming it.
2. With two host protectors registered, values are written by the last and values of the
   first still open; two with one `Id` stop the start.
3. With SMS used, or the report group mapped, and no `ISmsProvider` registered, the
   start stops naming `smsProvider`, under `AddHermes` and `AddHermesChannels` alike; an
   application whose kinds use only email, and that maps no report group, starts with no
   SMS provider.

---

**MSG-LIB-006** — The library SHALL follow Semantic Versioning 2.0.0: a release that
changes or removes a public member, an endpoint or its wire shape, a code, a
configuration key, a value of an enumeration or status, or a metric's name, or that adds
to an interface a host implements a member without a default implementation, is a major
release.

**Values (M-012).** The interfaces a host implements are the provider contracts and the
seams of MSG-LIB-002. A designed-for feature that needs more of a host comes as a new
interface, never as a member added to one of these. The library's schema is its own and
private, so a migration is not part of this rule; a migration that drops or rewrites
data is named in the release notes (`06` MSG-OPS-002). The release gate's baseline is
`build/gates/release-baseline.json`, written in each release commit beside
`PublicAPI.Shipped.txt`: the routes with their methods and statuses, each JSON body's
member names and types, the codes, the configuration keys, the spellings of the
enumerations and statuses, and the metric names and kinds. The gate takes the current
set as the contract tests read it (`07` MSG-ENG-006, MSG-ENG-007: the built assemblies,
the serializer contexts, and a test host's endpoint metadata and meter) and compares it
with the baseline as the previous release tag holds it (on a pull request, as its base
holds it), since the release commit rewrites the file to the release's own set; before
the first release no tag holds one, so the baseline is empty. Where a pull request
changes the baseline, which only its release commit may (`07` MSG-ENG-011), the gate
also checks that the baseline at its head is the set it reads there. An addition (a
route, a status, a response member, a code, a key, an enumeration value, a metric) is
minor; a change or a removal, and a request member a caller must now send, are major.

*Source: M-012, M-023*

**Acceptance criteria**
1. The release gate compares the public surface, the endpoints and their wire shapes,
   the codes, the configuration keys, the enumerations and statuses and the metric names
   with the previous release's, and fails a release whose version is below what changed
   needs, a member added to a host-implemented interface without a default
   implementation counted as a break (`07` MSG-ENG-012). That rule is applied to the
   version of a release tag; on a pull request, whose version is a pre-release, the gate
   fails a change that needs a major release where no commit since the last release
   carries `!` or `BREAKING CHANGE:` (`07` MSG-ENG-011); before the first release its
   baseline is empty, so nothing is a break.

---

**MSG-LIB-007** — *Retired by M-020.* No package of this library knows another library.
A host that uses an identity library beside this one connects the two in its own code:
its identity library's messages handed to the providers alone (MSG-LIB-003), and the
engine's seams (MSG-LIB-005: the recipient's identity, the content protector, the
consent source) and `ISubjects` answered from the identity library's own surface.

---

**MSG-LIB-008** — `Hermes.Testing` SHALL give a host's tests, and the sample host,
in-memory providers, fixed secrets, a dispatcher run on demand and a test database's
migration, so a test sends real messages through the engine and reads what each provider
received.

**Values (M-012).**

| Type | Members |
|---|---|
| `InMemoryEmailProvider` | An `IEmailProvider`; `IReadOnlyList<EmailMessage> Received`; `AnswerWith(Func<EmailMessage, Attempt> answer)`, by default every message `Accepted(null)` |
| `InMemorySmsProvider` | An `ISmsProvider`; `IReadOnlyList<SmsMessage> Received`; `AnswerWith(Func<SmsMessage, Attempt> answer)`, by default every message `Accepted(null)`; `IsValidSender` applies `02` MSG-SMS-002's rule; `SetBalance(BalanceReading balance)`, by default `NotSupported()`; `ReadReportWith(Func<SmsReportRequest, SmsReportReading> reader)`, by default a reading that does not read, answered 200 with an empty body |
| `InMemoryPushProvider` | An `IPushProvider` for `webpush`; `IReadOnlyList<PushRequest> Received`, the payload unencrypted; `AnswerWith(Func<PushRequest, Attempt> answer)`, by default every request `Accepted(null)` |
| `FixedSecretSource` | An `ISecretSource` whose parameterless constructor answers version 1 of the five keys of `06` MSG-OPS-008 and `emailSubmission`, each in its form and derived at run time from the SHA-256 of its name, so no credential-shaped text is written in the repository, and answers neither connection secret; `SetSecret(string name, SecretVersions versions)` and `RemoveSecret(string name)` |
| `TestDispatcher` | `TestDispatcher(IDispatcher dispatcher, TimeProvider timeProvider)`, the clock being the `TimeProvider` the library is registered with; `RunUntilIdleAsync(CancellationToken cancellationToken)` runs passes until a round of them, one of each channel, takes no delivery (so a channel whose rate the clock's current second has used up waits for the test to move the clock) and answers the number of deliveries taken, throwing `OperationCanceledException` once its token is cancelled, as `RunPassAsync` does |
| `MessagingTestDatabase` | A static class; `MigrateAsync(string connection, CancellationToken cancellationToken)` answers `ValueTask` and applies the release's migrations to that database as `hermes migrate` does (`06` MSG-OPS-002), so a host's test, and the sample host's, brings its test database to the schema |
| `MessagingTestingServiceCollectionExtensions` | `AddHermesTesting(IServiceCollection services, Action<FixedSecretSource>? configure = null)` answers the `IServiceCollection`; it registers each in-memory provider and one `FixedSecretSource` as singletons, each resolvable by its own type and by its contract, in place of the shipped providers and the host's secret source, `configure` running on that `FixedSecretSource` at once, so a test sets `connection` there before the host starts, and removes the hosted dispatcher and `observer` job, so only a test's passes dispatch, in either order of the two calls (`AddHermes` registers neither service where `AddHermesTesting` came first); its in-memory providers are used in place of the development inbox's wherever both are registered, in either order (`02` MSG-DEV-001); a secret source the host registers after it replaces `FixedSecretSource`, as any later registration of a seam does; a test's pass delivers, after its deliveries, every pending notice in order, as the job does, so a report's move, a cancellation and an erasure reach the observer too (`01` MSG-SEND-007, MSG-SEND-017) |

Nothing in it skips a check of `01`.

*Source: M-012, M-013, M-022, M-024*

**Acceptance criteria**
1. A host test sends a kind and reads the rendered message from the in-memory provider.
2. A test can make an attempt transient and see the retry once its clock passes the
   back-off.
