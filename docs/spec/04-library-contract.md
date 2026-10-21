# 04 — Library contract

What a host references, what it calls, what it supplies, and what the library promises
not to break.

---

**MSG-LIB-001** — The library SHALL ship as the packages of `00` section 2.2, and each
project SHALL reference only what this item allows it.

**Values (M-001, M-003, M-013).**

| Project | May reference |
|---|---|
| `Hermes.Core` | The base class library only |
| `Hermes.Channels` | `Hermes.Core`; MailKit; Lib.Net.Http.WebPush; the Microsoft.Extensions abstractions of `07` MSG-ENG-009 (its lifecycle services run the startup checks, since reading secrets is asynchronous) |
| `Hermes` | `Hermes.Core`, `Hermes.Channels`; EF Core, its relational layer and the Npgsql provider; Fluid.Core; ASP.NET Core's shared framework |
| `Hermes.Janus.Channels` | `Hermes.Core`, `Hermes.Channels`, Janus's main package |
| `Hermes.Janus` | `Hermes.Core`, `Hermes`, `Hermes.Janus.Channels`, Janus's main package |
| `Hermes.Testing` | `Hermes.Core`, `Hermes.Channels`, `Hermes` |
| `Hermes.Cli` (a .NET tool, command `hermes`) | `Hermes` |
| A test project | Its source project, `Hermes.Testing`, and the test packages of `07` MSG-ENG-009 |
| The sample host and its test project | The packed packages above, and for its tests the test packages of `07` MSG-ENG-009 |

Every shipped project references `Microsoft.CodeAnalysis.PublicApiAnalyzers` and every
packed project `MinVer`, both as build-only references; `Hermes` references
`Microsoft.EntityFrameworkCore.Design` as a build-only reference for making migrations.
A project that references ASP.NET Core's shared framework, or a project that does,
references the framework itself (`FrameworkReference` `Microsoft.AspNetCore.App`), so no
assembly the framework serves resolves as a package copy; `Hermes.Core` and
`Hermes.Channels` never do. No package but the two `Hermes.Janus` packages names Janus
or any other library of a host. The library's packages depend on one another at exactly
the same version, since they are released together. `Hermes.Conversations` is release
2's (`09`).

*Source: M-001, M-003, M-012, M-013*

**Acceptance criteria**
1. A test reads each project's references and fails on one this table does not allow.
2. `Hermes.Channels` references neither EF Core, nor ASP.NET Core, nor Fluid.

---

**MSG-LIB-002** — The public surface SHALL be exactly what the packages' public API
files list, each public type SHALL have the shape below, and a change to it SHALL be
visible in review.

**Values (M-012).** Data types are records with `init` members, so that a later release
adds members without a break, a non-nullable member being `required`; every asynchronous
member takes a `CancellationToken` as its last parameter and answers a `ValueTask`. A
type described as having cases is an abstract record whose cases are nested sealed
records of those names, made with `new` (`new Attempt.Accepted(reference)`), which a
seam reads by pattern matching; a value of any other type derived from it is a fault of
the code that made it (for `Recipient`, the unknown case below). An enumeration is a C#
`enum` whose members are named, and declared in the order, as `08` sections 2, 3 and 6
give; each member carries its wire spelling by `JsonStringEnumMemberName`, and wherever
the library writes a member as text (a body, a column, a metric tag, an alert's details,
a log entry) it writes that spelling. A record that carries a destination, content, a
token, a key, an endpoint or a person reference (`Send`, `EmailMessage`, `SmsMessage`,
`PushRequest.WebPush`, `ConsentQuery`, `DeliveryExport`, `Recipient.Person`,
`DeliveryNotice`, `IdempotencyKeyExport`, whose `Key` a host may build from a person's
data, and every case of `SendValue`, whose values are a template's content) overrides
`PrintMembers` to print none of them, so a log line that formats one writes none.

**Values and identifiers** (`Hermes.Core`):

| Type | Shape |
|---|---|
| `Send` | `string Kind`, `Recipient Recipient`, `string? Email`, `string? Phone`, `string? Language`, `string? TimeZone`, `IReadOnlyDictionary<string, SendValue> Values`, `IReadOnlyList<Channel>? Channels`, `DateTimeOffset? NotBefore`, `string? IdempotencyKey`, `string? PushTopic` (`01` MSG-SEND-003) |
| `SendValue` | Cases `PlainText(string Value)` (named so, since `Text` would match the namespace `System.Text`, CA1724), `WholeNumber(long Value)`, `DecimalNumber(decimal Value)`, `Flag(bool Value)`, `Instant(DateTimeOffset Value)` and `List(IReadOnlyList<IReadOnlyDictionary<string, SendValue>> Items)`, a list's entries holding no list (`03` MSG-TPL-002) |
| `Recipient` | Cases `Person(string Reference)` and `Anonymous()`. The set is open: a later release MAY add a case, and a seam meeting a case it does not know answers as for a refusal (consent `NotGiven`) |
| `SendId`, `DeliveryId` | `readonly record struct SendId(Guid Value)` and `readonly record struct DeliveryId(Guid Value)`, UUID version 7 |
| `SendReceipt` | `SendId Send`, `IReadOnlyList<DeliveryReceipt> Deliveries`; `DeliveryReceipt` is `Channel Channel`, `DeliveryId Delivery` |
| `SendRecord` | `SendId Send`, `string Kind`, `IReadOnlyList<DeliveryRecord> Deliveries`; `DeliveryRecord` is `DeliveryId Delivery`, `Channel Channel`, `DeliveryStatus Status`, `AttemptReason? Reason`, `DateTimeOffset RecordedAt`, `DateTimeOffset LastMovedAt`, `IReadOnlyList<AttemptRecord> Attempts`; `AttemptRecord` is `int Number`, `DateTimeOffset At`, `AttemptClass? AnswerClass` (none while it has no answer), `AttemptReason? Reason`, `IReadOnlyList<CallRecord> Calls`; `CallRecord` is `AttemptClass? AnswerClass` (likewise), `AttemptReason? Reason`, `int? Status`, `PushService? Service` (`01` MSG-SEND-007) |
| `Result`, `Result<T>`, `Failure` | `Result`: `bool IsSuccess`, `Failure? Failure`, and the factories `static Result Success()`, `static Result Fail(Failure failure)`, `static Result<T> Success<T>(T value)` and `static Result<T> Fail<T>(Failure failure)`. `Result<T>`: `bool IsSuccess`, `T? Value`, `Failure? Failure`. `Failure`: `string Code` of `08` section 1 and `IReadOnlyDictionary<string, string> Details` |
| `MessagingStartupException` | The exception a "(startup)" code of `08` section 1 is thrown in when the host starts: `string Code`, `IReadOnlyDictionary<string, string> Details` |
| `EmailAddress` | `string Address`, `string? DisplayName` |
| `CategoryPreferences`, `ChannelPreference` | `string Category`, `bool Essential`, `IReadOnlyList<ChannelPreference> Channels`; `Channel Channel`, `bool On` |
| `SubjectExport` | `IReadOnlyList<CategoryPreferences> Preferences`; `IReadOnlyList<SubscriptionExport> Subscriptions`, each `PushService Service`, `DateTimeOffset MadeAt`, `DateTimeOffset? LastAcceptedAt`; `IReadOnlyList<DeliveryExport> Deliveries`, each `string Kind`, `Channel Channel`, `DeliveryStatus Status`, `DateTimeOffset RecordedAt`, `DateTimeOffset LastMovedAt`, `string? Destination`, `IReadOnlyDictionary<string, string> Content` (by part), `bool ContentDeleted` (past content retention, so it carries no destination or content), `bool Erased` (its values' key was erased, so it carries no destination or content), `bool Rejected` (a value did not open under its binding, so it carries none either); `IReadOnlyList<IdempotencyKeyExport> IdempotencyKeys`, each `string Kind`, `string Key`, `DateTimeOffset RecordedAt`, the person's live idempotency rows (`01` MSG-SEND-020) |
| Enumerations | `Channel`, `DeliveryStatus`, `AttemptClass`, `AttemptReason`, `PushUrgency`, `PushService`, `PushPlatform`, `SmsEncoding`, `SmsReportOutcome`, `ConsentAnswer`, with the members of `08` sections 2, 3 and 6 that are built |

**Operations** (`Hermes.Core`, implemented by `Hermes`):

| Type | Members |
|---|---|
| `IMessages` | `SendAsync(Send, CancellationToken)` and `SendAsync(Send, DbTransaction, CancellationToken)` answer `Result<SendReceipt>`, the second holding the send's advisory locks until the host's transaction ends (one per `hashKeys` version for a person and one for an idempotency key, each a slot of PostgreSQL's shared lock table, so a host records a large batch in transactions of bounded size, `01` MSG-SEND-004); `CancelAsync(SendId, ...)` answers `Result<IReadOnlyList<DeliveryId>>`; `ReadAsync(SendId, ...)` answers `Result<SendRecord>` (`01`) |
| `IPreferences` | `ReadAsync(string person, ...)` answers `IReadOnlyList<CategoryPreferences>`, every declared category with the channels its kinds use; `SetAsync(string person, string category, Channel channel, bool isOn, ...)` answers `Result` (`01` MSG-SEND-010) |
| `ISuppressions` | `AddAsync(Channel channel, string destination, string category, ...)` and `RemoveAsync(...)` with the same parameters answer `Result` (`01` MSG-SEND-012) |
| `ISubjects` | `EraseAsync(string person, ...)` answers `Result` (erasing a person the library holds nothing of succeeds and does nothing, save where it answers `messaging.release.newer` or `messaging.key.unavailable`, `01` MSG-SEND-020; it waits for every open transaction that recorded a send for the person, so a host never calls it while such a transaction of its own is open, `01` MSG-SEND-004); `ExportAsync(string person, ...)` answers `Result<SubjectExport>` (`01` MSG-SEND-020) |

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
| | `KindBuilder` | `Email(EmailAddress? from = null, EmailAddress? replyTo = null)`, `Sms(int maxSegments = 1, string? sender = null)`, `Push(TimeSpan? ttl = null, PushUrgency urgency = PushUrgency.Normal)`, `ExpiresAfter(TimeSpan duration)`, each answering the builder (`01` MSG-SEND-001) |
| | `CategoryBuilder` | `Essential()`, `RequiresConsent(string purpose)`, `QuietHours(bool apply)`, `Limit(Channel channel, int count, TimeSpan window)`, `DeploymentLimit(Channel channel, int count, TimeSpan window)`, `Label(string language, string text)`, each answering the builder (`01` MSG-SEND-002) |
| | `MessagingEndpointRouteBuilderExtensions` | The four mapping methods of MSG-LIB-004 |
| | `IDispatcher` | `RunPassAsync(...)` runs one pass of each channel and answers the number of deliveries they took, in an application with `Hermes:Dispatch:Enabled` on (where it is off, it throws `InvalidOperationException` before it claims anything); a pass whose token is cancelled stops as `01` MSG-SEND-007 gives and the call then throws `OperationCanceledException` (`01` MSG-SEND-007) |
| `Hermes.Janus.Channels` | `MessagingJanusChannelsServiceCollectionExtensions` | `AddHermesJanusChannels(IServiceCollection, IConfiguration)` answers the `IServiceCollection` (MSG-LIB-007) |
| `Hermes.Janus` | `MessagingJanusServiceCollectionExtensions` | `AddHermesJanus(IServiceCollection)` answers the `IServiceCollection` (MSG-LIB-007) |
| `Hermes.Testing` | `InMemoryEmailProvider`, `InMemorySmsProvider`, `InMemoryPushProvider`, `FixedSecretSource`, `TestDispatcher`, `MessagingTestDatabase`, `MessagingTestingServiceCollectionExtensions` | The members of MSG-LIB-008 |

Everything else is internal: the shipped providers, their options, the endpoints'
request and response types (their wire shape is the contract, `05`), and the tables.

*Source: M-004, M-005, M-008, M-012*

**Acceptance criteria**
1. The build fails when a public member is added or removed without its public API file
   changing.
2. The public API files list each type and member of the tables above and of
   MSG-LIB-008, with the types given, and no other public type.

---

**MSG-LIB-003** — A host SHALL register the library with one call, declaring what it
sends in that call, and the declarations and the configuration SHALL be validated when
the application starts.

**Values (M-005, M-012).** `services.AddHermes(configuration, declare => { ... })` binds
the configuration section `Hermes` (through the configuration binder's source
generator), registers the engine, the shipped providers (as `AddHermesChannels` does),
the dispatcher and the background jobs (`06` MSG-OPS-004), and runs every startup check
of `01`, `02`, `03` and `06`, and the key table's start statement (`06` MSG-OPS-008), in
the `StartingAsync` of an `IHostedLifecycleService`, which the host calls on every such
service before any hosted service's `StartAsync`, so they complete before a host's own
hosted service starts or the application serves a request; a failure stops the start
with a `MessagingStartupException` carrying the code `08` gives it, and a call to
`IMessages`, `IPreferences`, `ISuppressions`, `ISubjects` or `IDispatcher` before they
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
`Hermes:Push`) is present, and in one that registers Janus's transports
(`AddHermesJanusChannels`, MSG-LIB-007) where `Hermes:Janus:Transports` names it; the
shipped provider of a channel is **used** where the channel is, the host registers no
provider of its own for it, and the application dispatches (`Hermes:Dispatch:Enabled`
on, whether or not its hosted dispatcher runs, `Hermes:Dispatch:Hosted`), registers the
providers alone, or registers Janus's transport for that channel, whatever
`Hermes:Dispatch:Enabled` says; so an application that only records sends, and runs no
such transport, reads no provider's secret. Where the report group is mapped
(MSG-LIB-004) and the host registers no SMS provider of its own, the shipped SMS
provider's `ReadReportAsync` is called there, which reads none of its keys or secrets,
so neither is required for it. A channel is **dispatched** in an application where it is
used and the application dispatches, registers the providers alone, or registers Janus's
transport for it, so its passes, the hosted dispatcher's or those `RunPassAsync` runs
(`01` MSG-SEND-007), or its providers can send on it; the keys those passes need are
read where it is (`06` MSG-OPS-008). A channel's own keys (the sender, the rate, the
allowed prefixes and hosts) are required where the channel is used; a shipped provider's
keys and secrets (the SMTP server's keys and `emailSubmission`, `Push:Subject`,
`Sms:We:BaseAddress` and `smsWe`) only where that shipped provider is used (`08` section
4). `AddHermes` registers `IMessages`, `IPreferences`, `ISuppressions` and `ISubjects`
as scoped services. An application **records sends** where `Hermes:Messages:Enabled` is
on (the default); where it is off, `IMessages.SendAsync` throws
`InvalidOperationException`, a fault of the calling code, so an application that only
maps the unsubscribe or preferences group holds no key that opens content (`06`
MSG-OPS-008).

*Source: M-005, M-007, M-012*

**Acceptance criteria**
1. A host with valid declarations and configuration starts with the one call.
2. A configuration value outside its rule (`08` section 4) stops the start, naming the
   key.
3. An application whose kinds use only email, and that maps neither the report nor the
   push subscription group, starts with no SMS or push key and no SMS or push secret.
4. An application with `Hermes:Dispatch:Enabled` off and no transport of MSG-LIB-007
   starts with no `emailSubmission`, `smsWe`, `Email:Host`, `Sms:We:BaseAddress` or
   `Push:Subject`; one that registers `AddHermesJanusChannels` with
   `Hermes:Janus:Transports` naming `email` does not start without `emailSubmission`.
5. An application with `Hermes:Messages:Enabled` and `Hermes:Dispatch:Enabled` off,
   `Hermes:Jobs:Enabled` off and no push subscription group starts without
   `contentKeys`, and its `SendAsync` throws.
6. With a host's hosted service registered before `AddHermes` that sends in its
   `StartAsync`, the send is recorded only after the startup checks ran.

---

**MSG-LIB-004** — Every endpoint group of `05` SHALL be mapped by its own call, under
the prefix the host chooses, so a host mounts each where it wants and leaves out the
rest.

**Values (M-001).** `MapHermesPreferences`, `MapHermesPushSubscriptions`,
`MapHermesUnsubscribe` and `MapHermesSmsReports`, each an extension of
`IEndpointRouteBuilder` taking the prefix and answering the group's `RouteGroupBuilder`:
- the preferences and push subscription groups need the identity seam (MSG-LIB-005),
  read JSON bodies only as `application/json` (so a cross-site form cannot reach them),
  and a host MAY add its own requirements (authorisation, rate limiting, request-forgery
  protection) as for any of its endpoints; `GET /push/key` calls `AllowAnonymous()`, so
  no authorisation requirement the host adds to the group reaches it;
- the unsubscribe and report groups are called by mail providers and the SMS provider,
  which hold no session: they call `AllowAnonymous()` and `DisableAntiforgery()` and
  read their forms themselves, and a host adds no authorisation and no forgery
  protection to them, which would refuse RFC 8058's one-click `POST` and the provider's
  reports.

*Source: M-001*

**Acceptance criteria**
1. Each group works mapped alone in an application with none of the others.
2. A group mapped under `/notifications` answers there and nowhere else.
3. The one-click `POST` succeeds in an application whose fallback authorisation policy
   requires a signed-in person and which registers antiforgery.

---

**MSG-LIB-005** — A host SHALL supply what only it knows through the seams of
MSG-LIB-002, and the library SHALL refuse to start without a required one.

**Values (M-005, M-006, M-007).**

| Seam | Required | `details.key` |
|---|---|---|
| `ISecretSource` | Always | `secretSource` |
| `IRecipientIdentity` | Where the preferences or push subscription group is mapped | `recipientIdentity` |
| `IConsentSource` | Where a category requires consent (`01` MSG-SEND-011) | `consentSource` |
| `IContentProtector` | No: the library's own is used, and always kept for reading values it made and for every anonymous recipient's values. A host MAY register several of its own: the last registered writes, a value is opened by the registered protector whose `Id` its envelope names, and two with one `Id` stop the start with `messaging.startup.declarationinvalid` | |
| `ITemplateSource` | No: the library's own is used (`03` MSG-TPL-006) | |
| `IDeliveryObserver` | Where `Hermes:Observer:Enabled` and `Hermes:Dispatch:Enabled` are both on (`01` MSG-SEND-017) | `deliveryObserver` |
| `IAlertSink` | No: the library's own logs each condition at the error level | |
| `TimeProvider` | No: the system clock | |

A required seam missing stops the start with `messaging.startup.declarationmissing`, its
`details.key` as above.

*Source: M-005, M-006, M-007*

**Acceptance criteria**
1. Each required seam, missing where it is required, stops the start naming it.
2. With two host protectors registered, values are written by the last and values of the
   first still open; two with one `Id` stop the start.

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
the first release no tag holds one, so the baseline is empty. An addition (a route, a
status, a response member, a code, a key, an enumeration value, a metric) is minor; a
change or a removal, and a request member a caller must now send, are major.

*Source: M-012*

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

**MSG-LIB-007** — The two `Hermes.Janus` packages SHALL let a host that uses the Janus
identity library connect the two, with neither library depending on the other.

**Values (M-003, M-012).** `Hermes.Janus.Channels`, for an application that needs the
providers, with or without the engine: `services.AddHermesJanusChannels(configuration)`
registers, for each channel `Hermes:Janus:Transports` names, Janus's transport for it,
and the shipped provider it goes over where the host registers none, as
`AddHermesChannels` would (registering it does not make an application one that
registers the providers alone, MSG-LIB-003): `IMailTransport` over this library's email
provider, and `ISmsTransport` over its SMS provider, with Janus's report reading through
the provider's `ReadReportAsync`, each channel then being used (MSG-LIB-003), so Janus's
own messages (sign-in codes, links, alerts) go through the same provider configuration,
while Janus keeps its own rules for when to send; a channel the key does not name gets
no transport, and needs none of its keys or secrets unless the engine uses it. The SMS
transport refuses a number outside `Hermes:Sms:AllowedPrefixes` without calling the
provider, answering Janus as Janus's transport contract has it answer a refused number
(named with the rest of Janus's API, plan, phase 6), since a sign-in route is where SMS
pumping aims and narrowing the prefixes must stop it there too. Otherwise those messages
do not pass through the engine: its checks, limits, balance floor, rates and alerts do
not apply to them, so the provider account's own rate must carry both the engine's
sends, which `Hermes:Sms:Rate` and `Hermes:Email:Rate` bound, and Janus's (`06`
MSG-OPS-007).

`Hermes.Janus`, for an application that uses the engine: `services.AddHermesJanus()`
registers:
- `IRecipientIdentity` over Janus's `IRequestAccess`, the person reference being the
  subject identifier Janus gives;
- `IContentProtector`, `Id` `janus`, over Janus's per-person field encryption
  (`IPersonalFields`), under a type and encrypted fields the bridge declares to Janus
  (named once the reference of Janus's API is in hand, plan, phase 6), so that Janus's
  erasure of a person makes this library's copies of their messages unreadable; it joins
  the unit of work the send runs in (`IMessages` resolves it from the scope it was
  called in, `01` MSG-SEND-003), so a person made in the same transaction is protected
  under their new key, and it binds the context by writing, at the head of the
  plaintext, the context's UTF-8 length in 2 bytes, big-endian, then its UTF-8 bytes,
  and checking them on reading, a layout two releases share (`08` section 8), since
  Janus's own additional data names the subject, type and field only; it is called only
  for a person, the engine protecting an anonymous recipient's values with its own
  protector (`01` MSG-SEND-018);
- a subscriber to Janus's subject events and an export contributor over `ISubjects`, so
  a person's erasure in Janus erases here (`01` MSG-SEND-020) and their export includes
  what is held here; where `EraseAsync` answers a failure, the subscriber fails the
  event, so Janus delivers it again;
- `IConsentSource` over the consent reading Janus gives a host, once Janus offers one
  (`decision-log.md` M-003).

How SMS delivery reports of both libraries reach the right one when they share WE's
account is settled once WE's document shows how its report addresses are set (`02`
MSG-SMS-004, M-008).

*Source: M-003, M-008, M-012*

**Acceptance criteria**
1. With `Hermes.Janus.Channels` registered, a sign-in code Janus sends goes out through
   this library's SMS provider.
2. With `Hermes.Janus.Channels` registered and `Hermes:Janus:Transports` naming `email`
   alone, an application whose kinds use SMS and with `Hermes:Dispatch:Enabled` off
   starts with no SMS secret, and registers Janus's mail transport and no SMS transport.
3. Janus's SMS transport refuses a number outside `Hermes:Sms:AllowedPrefixes` and calls
   no provider.
4. With `Hermes.Janus` registered, a send for a person made in the same unit of work is
   protected under that person's key, and erasing the person in Janus leaves nothing
   here naming them, while their export includes their deliveries still holding content.

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
| `FixedSecretSource` | An `ISecretSource` whose parameterless constructor answers version 1 of the five keys of `06` MSG-OPS-008, `emailSubmission` and `smsWe`, each in its form and derived at run time from the SHA-256 of its name, so no credential-shaped text is written in the repository, and answers neither connection secret; `SetSecret(string name, SecretVersions versions)` and `RemoveSecret(string name)` |
| `TestDispatcher` | `TestDispatcher(IDispatcher dispatcher, TimeProvider timeProvider)`, the clock being the `TimeProvider` the library is registered with; `RunUntilIdleAsync(CancellationToken cancellationToken)` runs passes until a round of them, one of each channel, takes no delivery (so a channel whose rate the clock's current second has used up waits for the test to move the clock) and answers the number of deliveries taken, throwing `OperationCanceledException` once its token is cancelled, as `RunPassAsync` does |
| `MessagingTestDatabase` | A static class; `MigrateAsync(string connection, CancellationToken cancellationToken)` answers `ValueTask` and applies the release's migrations to that database as `hermes migrate` does (`06` MSG-OPS-002), so a host's test, and the sample host's, brings its test database to the schema |
| `MessagingTestingServiceCollectionExtensions` | `AddHermesTesting(IServiceCollection services, Action<FixedSecretSource>? configure = null)` answers the `IServiceCollection`; it registers each in-memory provider and one `FixedSecretSource` as singletons, each resolvable by its own type and by its contract, in place of the shipped providers and the host's secret source, `configure` running on that `FixedSecretSource` at once, so a test sets `connection` there before the host starts, and removes the hosted dispatcher and `observer` job, so only a test's passes dispatch, in either order of the two calls (`AddHermes` registers neither service where `AddHermesTesting` came first); a secret source the host registers after it replaces `FixedSecretSource`, as any later registration of a seam does; a test's pass delivers, after its deliveries, every pending notice in order, as the job does, so a report's move, a cancellation and an erasure reach the observer too (`01` MSG-SEND-007, MSG-SEND-017) |

Nothing in it skips a check of `01`.

*Source: M-012, M-013*

**Acceptance criteria**
1. A host test sends a kind and reads the rendered message from the in-memory provider.
2. A test can make an attempt transient and see the retry once its clock passes the
   back-off.
