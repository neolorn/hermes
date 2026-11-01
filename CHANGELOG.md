# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `Hermes.Conversations`, the conversations package, registered by `AddHermesConversations`
  after `AddHermes`; called without `AddHermes`, it throws `InvalidOperationException`
  naming it, and where conversations are registered a key under `Hermes:Conversations:`
  the package does not know stops the start with `messaging.startup.configurationinvalid`
- The sample host registers conversations
- `IConversations`, a person's direct and group conversations, with `ConversationId`,
  `MessageId`, `ParticipantId`, `ConversationScope`, the conversation and message records
  and pages, and the enumerations `ConversationType`, `ConversationStatus`, `MessageType`,
  `ScopeKind` and `AuthorKind`; and `IConversationMembership`, the seam that answers who
  belongs to the host's organizations and groups, none of whose records prints a person
  reference or a text
- The migration `Conversations`, which adds the tables `conversations`, `participants`,
  `messages` and `mentions` to the engine's schema; it drops and rewrites nothing. A
  message's text is protected under its author's key and bound to its row, the retention
  job protects it again as it does a delivery's values, no key version a message uses is
  retired, and erasing a person clears the text of their messages and deletes their
  participant and mention rows
- Staff chats: `AddHermesConversations` registers `IConversations` as a scoped service and
  needs `IConversationMembership` registered, its start stopping with
  `messaging.startup.declarationmissing` (`key` `conversationMembership`) without it, and
  `MapHermesStaffConversations` maps the staff conversations group: direct conversations
  of up to 16 participants and one group conversation a scope, whose every read and write
  asks the membership seam, a conversation the person may not read answering
  `messaging.conversation.notfound`; messages of 1 to 4,000 characters mentioning up to 32
  persons, edited and deleted by their authors alone; read positions that move forward
  only, and each conversation's unread count
- The sample host lets its staff talk in the shop's group conversation

### Fixed

- `Hermes:Languages` set item by item through a web host's test factory, which gives the
  list itself an empty value, no longer stops the start

## [1.0.0-rc.2] - 2026-10-31

### Added

- `DeliveryNotice.IdempotencyKey`, the key the notice's send was recorded with, its last
  member; a notice prints it no more than it prints the person
- The migration `SendIdempotencyKey`, which gives each delivery the idempotency key of its
  send, protected as its destination is and gone with its content, so a notice carries the
  key after the key's row has lapsed; it drops and rewrites nothing
- A notice whose key answers erased is told without it, one whose key does not open under
  its binding is told without it and raises `content-rejected`, and one whose key cannot
  open yet is told at a later run
- `DeliveryExport.IdempotencyKey`, its last member: the key a delivery keeps of its send,
  carried while the delivery keeps it, an in-app delivery's after its dispatch included,
  and none where the delivery is marked past content retention, erased or rejected; an
  export prints it no more than it prints the destination

### Changed

- An erasure also deletes the idempotency key each of the person's deliveries keeps, and
  the notices it keeps for the observer carry none

## [1.0.0-rc.1] - 2026-10-31

### Added

- `AddHermes`, which registers the engine and checks its configuration keys when the host
  starts, a reloaded value outside its rule leaving the last valid one in force
- Kinds, categories and retired kinds declared in `AddHermes`, each checked when the host
  starts, the first outside its rule stopping the start
- The secret, consent, observer, protector and alert seams a host supplies, the start
  stopping where a required one is missing or two host protectors share an `Id`
- `hermes migrate`, the `Hermes.Cli` tool that applies the release's migrations to the
  schema `messaging` and grants the application's roles the use of its tables
- A start against a database lacking one of the release's migrations stops, naming it
- The secrets the application's used parts need, read when the host starts, a key
  missing, outside its form or without its write version stopping the start
- The key table, which records each key version's check value and when it was last used
  to write, a version whose bytes changed or that is in use but not held stopping the start
- The library's own content protector and the envelope every stored value is kept in, a
  value opening only in its row and for its person, by the protector its envelope names
- Alerts raised through the alert sink when a condition starts to hold and each repeat
  while it holds, cleared once, raised once however many processes see it; the
  `configuration-invalid`, `newer-release` and `content-key-limit` conditions
- The meter `Hermes` with the instruments `hermes.deliveries`, `hermes.attempts`,
  `hermes.dispatch.oldest_due_age` and `hermes.provider.duration`
- A category's label longer than 128 characters, counted as Unicode scalar values, or
  holding a lone surrogate, stops the start
- `Retry:Factor` and `Jobs:StallFactor`, decimal numbers read in the invariant culture
  and checked when the host starts and on each reload
- `Templates:Path`, taken from the host's content root, a path naming a file or a folder
  the process cannot read stopping the start, and one naming nothing holding no template
- `hermes migrate` checks that the database has each role of `applicationRoles` before it
  applies anything, grants the reading alone of the migration history and the use of the
  schema's sequences, and checks each grant, answering `messaging.migrate.grantfailed`
  and keeping none where one does not hold
- The provider contracts `IEmailProvider`, `IEmailSession`, `ISmsProvider` and
  `IPushProvider`, their messages, and the `Attempt` each call answers, which carries no
  text a provider answered
- `SmsText.Measure`, which answers an SMS text's encoding and its segments as the network
  counts them, never splitting an escape pair or a surrogate pair
- The rules a channel's values are checked against: an email address as an RFC 5322
  `addr-spec` of at most 254 characters, an SMS sender by 3GPP TS 23.040, the allowed
  number prefixes, and the push hosts a wildcard of at least three labels allows
- The shipped Web Push provider: RFC 8030 requests encrypted by RFC 8291 and signed by
  RFC 8292 with the subscription's VAPID key version, each token reused for an hour,
  over the named client `Hermes.Push`, which follows no redirect, carries the library's
  handler alone and logs no address, every answer classed and none of its text kept
- `AddHermesChannels`, which registers the shipped providers alone and checks the keys
  of each channel whose section is present when the host starts, and the VAPID keys
  where the shipped push provider is used, a value outside its rule stopping the start
- `AddHermes` registers the shipped providers and checks the keys of each channel a kind
  uses, a kind's `from`, `replyTo` and SMS sender, and the configuration given to the
  call, the start stopping on the first outside its rule
- The shipped SMTP provider: submission through MailKit to `Email:Host` over implicit TLS
  or `STARTTLS`, authenticated with `emailSubmission`, the server's certificate validated
  by the library with each named revocation source read online, one connection per
  session opened again once it broke, every answer classed and none of its text kept
- The send's types: `Send` and its values, `SendReceipt`, `SendRecord` with its
  deliveries, attempts and calls, `Result` and `Failure`, and the `IMessages` and
  `ITemplateSource` contracts, none of which prints a destination, a value or a person
- Templates rendered strictly and within their bounds: the allowed tags and filters, the
  library's `format_date` and `format_number`, HTML parts encoded, a render past its steps,
  over its channel's limit or unable to compute refused with its code
- Every kind's templates and the library's pages read when the host starts, from the
  host's template source, the template folder and the assemblies passed to
  `declare.Templates`, a part missing, not parsing or outside the allowed tags, filters,
  arguments, ranges and nesting stopping the start and naming it; the unsubscribe pages
  shipped in `en` and `ar`
- The send's tables: sends, deliveries, attempts and their calls, idempotency keys, the
  rate counters, notices and the job table, each closed set refused by the database
  outside its values
- The keyed hash under each held `hashKeys` version, the advisory locks' keys and a
  send's canonical form, in the layouts two releases share
- `IMessages.SendAsync`, which checks a send, renders it in its language and time zone,
  protects its content and destination and records one delivery per channel, on the
  host's transaction where one is given, waking the dispatchers when it commits; a send
  with an idempotency key recorded once inside its window
- `IMessages.CancelAsync`, which cancels a send's waiting deliveries, and
  `IMessages.ReadAsync`, which answers a send's deliveries, attempts and calls
- `IDispatcher.RunPassAsync`, which runs a pass of email and of SMS: each claims its due
  deliveries within the second's rate, essential kinds first, checks each, renews its
  claim, calls the provider and records the answer, retrying a transient one with
  back-off until it is sent, its attempts are used up or it expires; a delivery whose
  value cannot open yet is held for a key, and a stop hands back what it has not begun
- The hosted dispatcher, where `Dispatch:Enabled` and `Dispatch:Hosted` are on: passes
  of each channel on each committed send's notification, every `Dispatch:Interval` and
  after each connection of its listening connection on `listenConnection` or
  `connection`, at most `Dispatch:MaxPasses` of a channel at once
- The `observer` job, where `Dispatch:Enabled` is on: every `Dispatch:Interval` one
  process tells the observer of each pending notice, in order for each delivery, up to
  `Observer:MaxCalls` of different deliveries at once, each call bounded by
  `Dispatch:Timeout`, and deletes a notice once the observer took it
- Each control character of an email's subject and of a sender's or reply address's
  display name is replaced by a space, so no value starts a header of its own
- The health check `hermes.dispatch`, where `Dispatch:Enabled` is on: unhealthy while
  the oldest due delivery became due longer ago than `Dispatch:LagThreshold`
- `Hermes.Testing`: in-memory email, SMS and push providers, fixed secrets, a dispatcher
  run on demand, a test database's migration and `AddHermesTesting`, under which only a
  test's passes dispatch and each delivers the pending notices
- Each call to the alert sink is bounded by `Dispatch:Timeout`; a sink that does not
  answer in time is logged and the alert stays recorded, so a pass or job never waits on it
- `ISuppressions`, which suppresses an email address or phone number for an optional
  category, keeping only its keyed hash, and withdraws a suppression under every
  `hashKeys` version; a dispatched delivery of the category to a suppressed destination
  ends `suppressed`, and one whose lookup needs a version this process lacks is held
- `IPreferences`, which reads a person's preferences for every declared category and
  sets whether they receive an optional category on a channel its kinds use, under the
  person's locks taken shared within `Dispatch:Timeout`; a delivery of a category the
  person turned off on its channel ends `optedout` when it is checked
- Each email of an optional category carries a one-click unsubscribe address,
  `PublicBaseAddress`, `/unsubscribe/` and a token signed under `tokenKeys` and dated
  from the later of the send and its `NotBefore`, in its `List-Unsubscribe` and
  `List-Unsubscribe-Post` headers and as the template value `unsubscribe`; the address
  is stored protected with the delivery, and an essential category's email carries none
- Quiet hours: a delivery of a category they apply to, checked between
  `QuietHours:Start` and `QuietHours:End` in the send's time zone, waits until they end
  plus a random part of `QuietHours:Spread`, counting no attempt and giving its place in
  the rate back
- Consent: a delivery of a category that requires it asks the host's consent source at
  dispatch, within `Dispatch:Timeout`, ending `noconsent` where it is not given and
  retrying where the source cannot answer, then ending `failed` with
  `consentunavailable`
- Category limits: at dispatch a delivery takes one place under each of its category's
  `Limit`s and `DeploymentLimit`s on its channel, under their advisory locks within
  `Dispatch:Timeout`, or ends `limited` where a window is full; a delivery that ends
  unsent gives its places back, and one on which a provider call is recorded gives back
  only its per-destination places
- The preferences group, `MapHermesPreferences(prefix)`: `GET /preferences` and
  `PUT /preferences/{category}/{channel}` for the person the host's `IRecipientIdentity`
  answers, 401 where it answers none; JSON bodies read and written through the library's
  own serializer context, only as `application/json` and at most 16 KiB; a person's locks
  not had within `Dispatch:Timeout` answered 503 with `Retry-After`
- The unsubscribe group, `MapHermesUnsubscribe(prefix)`: RFC 8058's one-click
  `POST /unsubscribe/{token}` suppresses the address the token names by hash for its
  category, and `GET` shows the `unsubscribe` page in the token's language, which asks
  before it does; the group allows anonymous requests, takes no forgery protection, sets
  no cookie, never redirects and answers every page with `no-store`, `no-referrer`,
  `nosniff` and a closed content security policy; a token naming a key version in use
  but not held is answered 503 and raises `key-unavailable`
- The report group, `MapHermesSmsReports(prefix)`: `GET` and `POST /sms/report/{key}`
  take the SMS provider's delivery reports, the key compared in constant time with every
  held `smsReportKey` version and wrong keys limited per source address; a report can
  only move an SMS delivery to `delivered` or `failed`, is kept on an `inflight` one until
  its answer is recorded, is held where no delivery holds its provider reference yet, and
  is counted as rejected where it names nothing, raising `sms-report-rejected` past the
  threshold; a failure report releases the delivery's per-destination places only
- With `Hermes:Sms:ReportBaseAddress` set, each SMS carries its report address: that
  address, `/sms/report/` and the `smsReportKey` write version's key, which the start then
  needs where SMS is dispatched; a report whose key matches records when its version last
  matched in the key table
- The `balance` job: where the application dispatches and SMS is used, whatever
  `Hermes:Jobs:Enabled` says, it reads the SMS balance every `Hermes:Sms:BalancePoll` and
  records each reading, raising `sms-balance` for a drain over `Hermes:Sms:DrainFactor` of
  the baseline or a balance that would reach `Hermes:Sms:BalanceFloor` within
  `Hermes:Sms:FloorWarning`, and `sms-balance-unreadable` once reads have failed for
  `Hermes:Sms:BalanceUnreadableAfter`; below the floor an optional category's SMS ends
  `balancefloor`, and a provider that reports no balance has no floor
- The push subscription group, `MapHermesPushSubscriptions(prefix)`: `GET /push/key`
  answers the VAPID public key of the write version to anyone; `POST` and
  `DELETE /push/subscriptions` register and remove the signed-in person's subscription,
  refusing an endpoint outside `Hermes:Push:AllowedHosts` (https, port 443, no user
  information) with `422 messaging.push.endpointnotallowed` and a key no held version has
  with `422 messaging.push.keyunknown`; the endpoint and keys are stored protected, and
  past `Hermes:Push:MaxSubscriptions` the least recently used subscription is removed
- Push delivery: each attempt goes to the person's live subscriptions, signed with the
  VAPID version each was registered under, with a TTL no longer than the time left to the
  delivery's expiry; `404` and `410` remove the subscription, one acceptance ends the
  delivery `sent`, and `401` and `403` from one push service over
  `Hermes:Push:AuthFailThreshold` within an hour raise `push-auth-failing`
- The `subscriptions` job removes subscriptions past their expiration time every
  `Hermes:Jobs:SubscriptionsInterval`
- The `watch` job: where `Hermes:Jobs:Enabled` is on, every `Hermes:Dispatch:Interval` it
  raises and clears `dispatch-lag` per channel, recording `hermes.dispatch.oldest_due_age`,
  `job-stalled` for each job a process has started, `observer-failing`,
  `sms-report-rejected` and `push-auth-failing` by their counts; it clears
  `newer-release`, `key-unavailable`, `content-key-limit` and `content-rejected` once the
  instant a process wrote has passed, and drops the SMS reports held past
  `Hermes:Sms:ReportHold`, which are never applied once lapsed
- `push-auth-failing` carries the push service in its details
- A push pass stopped during a delivery's call makes none of its other calls: an
  acceptance among those made ends it `sent`, and otherwise its attempt is recorded
  `claimlost` and the next goes to every live subscription not refused
- `ISubjects`: `EraseAsync` cancels the person's waiting and inflight deliveries, deletes
  their preferences, subscriptions, idempotency rows and the limit places keyed by them,
  and deletes the destination, content and person reference of every delivery for them,
  all or nothing; `ExportAsync` answers what the library holds for them; both answer
  `messaging.release.newer` where the database holds a migration the release does not know
- The `retention` job: where `Hermes:Jobs:Enabled` is on, daily at
  `Hermes:Jobs:RetentionAt` in `Hermes:TimeZone`, it deletes a final delivery's content,
  destination and unsubscribe address after `Hermes:Retention:Content` and its record
  after `Hermes:Retention:Record`, lapsed idempotency rows and limit places, suppressions
  past their lifetime, idle subscriptions, old notices and counters, and the deliveries
  this release or application does not know, in batches that skip rows held elsewhere
- The `retention` job protects again, in batches, every value under a `contentKeys`
  version below the write version, and marks retired each `contentKeys`, `hashKeys`,
  `tokenKeys` and `pushVapidKeys` version nothing needs whose values were last made at
  least `Hermes:Secrets:RetireAfter` before; a token's versions stay until the longest
  recorded token lifetime and `RetireAfter` have passed since its last email went out;
  it does neither while the database holds a migration the release does not know
- A host whose service provider checks lifetimes, as ASP.NET Core's does in
  development, starts with the push subscription group mapped: its service no
  longer holds a host protector for the life of the process
- A sample host, a bakery that tells its customers of their orders by email and SMS
  and of its weekly offers by email and push, built from the packed packages alone,
  with tests that run it against PostgreSQL
- The library ships no SMS provider: where SMS is used, or the report group is mapped,
  a start with no `ISmsProvider` of the host's stops with
  `messaging.startup.declarationmissing`, `key` `smsProvider`, under `AddHermes` and
  `AddHermesChannels` alike
- `FixedSecretSource` answers only the secrets the library reads: the five keys of the
  key tables and `emailSubmission`
- `AddHermesDevelopmentInbox`, of the new package `Hermes.Development`, which in Development
  keeps every message on the machine instead of sending it, whatever the order of the host's
  provider registrations, and does nothing in any other environment; `AddHermesTesting`'s
  in-memory providers are still used where it is registered
- The development inbox's records: one JSON record a message, with the application, the
  delivery's kind, category, language and person and the message's links, and an email's
  `.eml` beside it as the SMTP provider would submit it, in a folder its owner alone reads,
  cleaned as it grows past `Hermes:DevelopmentInbox:KeepFor` and `MaxMessages`
- The development inbox's group, `MapHermesDevelopmentInbox`: a small JSON API over the
  captured messages, `GET /messages/latest` able to wait for one, each email's `.eml` and its
  HTML under a policy and sandbox that run no script, answered only to the machine itself
  under a loopback host name unless `Hermes:DevelopmentInbox:AllowRemote` is on
- The development inbox's page: every captured message, newest first and checked for new ones
  every second, with filters, each email as it arrives in a sandboxed frame with its text,
  headers and `.eml`, each SMS as a phone shows it and each push as a notification, every
  link with Open and Copy, and Clear; one self-contained page that loads nothing from outside
- `DevelopmentInbox`, for a .NET test or tool: it lists, reads the latest of and clears the
  captured messages, and waits for one captured after it was made, without HTTP
- The sample host runs with the development inbox, its page at `/dev/inbox/`, while its tests
  keep sending through the in-memory providers of `Hermes.Testing`
- `IInApp` and its types, `InAppPage`, `InAppEntry` and `EntryId`, for a person's
  in-application list, and `SubjectExport.Entries`, each of the person's entries as
  `InAppEntryExport`; none prints a part, a target or a target value
- The channel `inapp` and the status `read`, and the migration `InAppList`, which adds the
  in-app list's table and a column for an in-app delivery's parts, and drops or rewrites
  nothing
- The keys `Hermes:Retention:InApp`, 180 days by default, and `Hermes:InApp:AlwaysOn`, off by
  default, each checked against its rule when the host starts and read again when it reloads
- `KindBuilder.InApp(target, targetValues)`, which lets a kind go to its person's
  in-application list: the send renders its `title` and `body` in every declared language,
  each in that language's culture, refuses one past 128 or 1,024 characters with
  `messaging.send.toolong`, and refuses the channel for an anonymous recipient with
  `messaging.send.nodestination`
- The in-app pass: the dispatcher writes an in-app delivery's entry to its person's list in
  every environment, with no provider, no rate and no quiet hours, and ends the delivery `sent`
- `IInApp`, registered scoped: a person's list read a page at a time, newest first, in the
  language asked for, with an opaque cursor and `messaging.inapp.cursorinvalid` for one that
  does not read; the unread count; and an entry, or all of them, marked read, which moves
  each delivery from `sent` to `read` and tells the observer
- With `Hermes:InApp:AlwaysOn` on, `IPreferences.SetAsync` refuses the in-app channel with
  `messaging.preference.fixed`, answered 422 by the preference route, and `ReadAsync` and
  `GET /preferences` leave the channel out
- The retention job deletes, in batches, each in-app entry older than `Hermes:Retention:InApp`,
  clears a final in-app delivery's parts with the rest of its content, and protects an
  entry's values and a waiting in-app delivery's parts again under the write version
- Erasing a person deletes their in-app entries and clears their in-app deliveries' parts,
  and their export holds each entry with its parts in every language, its target and its
  target values
- `MapHermesInApp`, the in-app group: `GET /inapp` in the language of the request's
  `Accept-Language`, `GET /inapp/unread`, and `POST /inapp/{id}/read` and `POST /inapp/read`,
  which read an empty object as `application/json` alone, all for the person signed in
