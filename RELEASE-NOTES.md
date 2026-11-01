# Release notes

## 1.0.0-rc.3

The third release candidate of 1.0.0. It holds release 2: conversations, in the new package
`Hermes.Conversations`. It ships seven packages, the six of 1.0.0-rc.2 and
`Hermes.Conversations`, each at this version and each depending on the others it uses at
exactly this version. Until a final release, a release candidate may change or remove
anything; this one removes nothing and changes nothing a host relies on: every change to
the surface of 1.0.0-rc.2 is an addition, named below. A host that does not register
conversations runs as it did on 1.0.0-rc.2, once this release's migrations are applied.

### Changes since 1.0.0-rc.2

- **`TemplateKey.Reply`: a host template source reads it.** `TemplateKey` gains
  `string? Reply`, an optional `init` property: a key with `Reply` set and `Kind` and `Page`
  none names an automatic reply's template, whose one part is `text`. A host
  `ITemplateSource` that reads `Kind == null` as a page must now also read `Reply`, or it
  answers an automatic reply's key with a page's template. The library's own source reads
  it from `replies/<name>.<language>.text.liquid`.
- **The new package `Hermes.Conversations`.** `AddHermesConversations`, called after
  `AddHermes` (without it, it throws `InvalidOperationException` naming it), registers
  conversations, and `MapHermesStaffConversations` and `MapHermesPublicConversations` map
  their two groups, each with its hub at `/live`. Each group is mapped under a prefix of its
  own, since the two share relative routes. Where conversations are registered, a key under
  `Hermes:Conversations:` the package does not know stops the start with
  `messaging.startup.configurationinvalid`.
- **Staff chats.** Direct conversations of up to 16 participants and one group conversation
  a scope, every read and write asking the host's membership seam; messages of 1 to 4,000
  characters mentioning up to 32 persons, edited and deleted by their authors alone; read
  positions that move forward only, and each conversation's unread count; and away notices,
  sent as `Hermes:Conversations:AwayKind` once until the person reads.
- **The live connection.** The staff hub pushes a message, its edit or delete, a read
  position, typing and presence to the persons connected to any process who may read the
  conversation; each process listens for the others' writes on a connection of its own.
- **Support's public side.** A visitor, known by the HttpOnly cookie `messaging_visitor` and
  held only as a keyed hash, or a signed-in customer acknowledges the privacy notice,
  starts a conversation of a topic or leaves one through the offline form, and reads and
  writes their own, never seeing a note. A queued conversation goes, within its topic's
  hours, to the available member of the topic's scope with the fewest open conversations;
  outside hours or with nobody available the start answers `messaging.support.offline`.
  Limits per visitor and per source address answer `messaging.support.limited` with
  `Retry-After`, across processes. An offline conversation's replies are sent by email as
  `Support:OfflineReplyKind`. The public hub pushes the visitor's messages, typing, place in
  the queue, assignment and close, never a note.
- **The desk's tools.** Saved replies, tags, snoozing, transfers, closing and blocking
  through the staff group; automatic replies on `started`, `unavailable`, `outsidehours`,
  `waiting` and `closing`; and supervisors, who read a topic's conversations, its live view
  and its reports, and write in none of them.
- **The end of support.** A closed conversation's transcript, as plain text or sent by email
  as `Support:TranscriptKind` at most three times; its survey, answered once within seven
  days of the close; the library's page `transcript`, in `en` and `ar`; and the counters
  `hermes.conversations.started` and `hermes.conversations.closed`, each tagged `topic`.
- **Attachments.** Where `Hermes:Conversations:Attachments:Enabled` is on, with `Public` for
  the public group: a single-file upload, its type and first bytes checked, streamed to the
  host's store in protected 64 KiB chunks; scanned by the host's scanner before anyone opens
  it; and deleted, bytes first, when unnamed after 24 hours, with its message, its
  conversation or its owner's erasure. A public upload counts as one message toward the
  caller's limit, and a block or a notice not yet acknowledged refuses it as it refuses a
  message.
- **Retention, export and erasure.** A visitor's support conversation goes
  `Retention:Visitor` after it closes; where `Retention:Staff` is set, staff-chat messages
  older than it go; `IConversations.EndScopeAsync` deletes an ended scope's group
  conversation; a person's export holds their conversations, their own messages and their
  survey, naming nobody else; and erasure clears or deletes a person's conversations' values
  as each table's rule says.
- **`Hermes.Core`'s additions.** The interfaces `IConversations`, `IConversationMembership`,
  `IAttachmentStore` and `IAttachmentScanner`, each new, so no existing interface gains a
  member; their identifiers, records and enumerations; `ParticipantRecord.Online`;
  `SubjectExport.Conversations`, none where conversations are not registered; and
  `TemplateKey.Reply`. Each added member of an existing record is an optional `init`
  property, so a host's code that creates one compiles as before.
- **`Hermes.Testing`'s additions.** `InMemoryAttachmentStore` and
  `FixedAttachmentScanner(ScanResult answer = ScanResult.Clean)`, which `AddHermesTesting`
  registers as singletons by their own types and by their seams, a later registration of a
  seam replacing its double.
- **Codes added.** `messaging.conversation.*` (`notfound`, `invalid`, `full`, `closed`),
  `messaging.message.*` (`notfound`, `invalid`, `notauthor`, `deleted`),
  `messaging.support.*` (`offline`, `limited`, `blocked`, `noticerequired`, `alreadyopen`,
  `notclosed`, `transcriptlimit`, `surveyanswered`, `surveyclosed`, `surveyinvalid`),
  `messaging.reply.*` (`notfound`, `invalid`, `full`, `nameexists`) and
  `messaging.attachment.*` (`invalid`, `notfound`, `pending`, `refused`, `toolarge`,
  `type`).
- **Alert added.** `attachment-scan`, raised at an attachment's fifth failed scan.
- **Fixed.** A process that stops past its host's shutdown time no longer leaves the
  key-table timer's tick running while the host disposes the database's data source, and
  its stop no longer fails with `OperationCanceledException`; and `Hermes:Languages` set
  item by item through a web host's test factory, which gives the list itself an empty
  value, no longer stops the start.

### The seams a host supplies for conversations

A required seam missing stops the start with `messaging.startup.declarationmissing`, its
`details.key` as below.

- **`IConversationMembership`** (`conversationMembership`), required where conversations
  are registered: it answers who belongs to the host's organizations and groups, and so who
  reads each group conversation and who staffs each support topic.
- **`IAttachmentStore`** (`attachmentStore`) and **`IAttachmentScanner`**
  (`attachmentScanner`), each required where `Hermes:Conversations:Attachments:Enabled` is
  on: the object storage that keeps an attachment's protected chunks, and the scanner that
  reads each file before anyone opens it.
- **`IRecipientIdentity`**, as before, where the public group should know a signed-in
  customer: without it, every caller of the public group is a visitor.
- **`ITemplateSource`**, where the host registers its own: it reads `Reply` as above.

### Configuration keys and jobs

Each key below is under `Hermes:Conversations:` and is read at start, a value outside its
rule stopping the start with `messaging.startup.configurationinvalid` naming its key.

- `AwayKind`: the kind away notices are sent as; a kind not declared stops the start.
- `Topics:<name>`: each support topic's `TimeZone`, its weekly `Hours` and dated
  `Exceptions`, the `Scope` whose members staff it, and its optional `Supervisors` scope.
- `Support:Limit` (3 by default, 1 to 100), the open support conversations a staff member
  holds below which they are available; `Support:OfflineReplyKind`,
  `Support:TranscriptKind` and `Support:NoticeVersion`, each required where topics are
  configured, the two kinds declared ones; `Support:CloseAfter` (1 day by default, from
  1 hour to 30 days) and `Support:CloseWarning` (5 minutes by default, from 1 minute to
  1 hour, shorter than `CloseAfter`).
- `Limits:VisitorStarts` (5 a day by default), `Limits:VisitorMessages` (20 a minute) and
  `Limits:SourceStarts` (20 an hour from one source address), each 1 to 10,000.
- `Replies:<name>`: each automatic reply rule's `Trigger`, its `Topics`, and `After` for a
  `waiting` rule, from 1 minute to 1 hour.
- `Tags`: the tags staff may put, at most 50.
- `Attachments:Enabled` and `Attachments:Public` (both off by default),
  `Attachments:MaxBytes` (10 MiB by default, from 1 KiB to 50 MiB) and `Attachments:Types`
  (`image/jpeg`, `image/png` and `application/pdf` by default).
- `Retention:Visitor` (365 days by default, from 1 day to 10 years) and `Retention:Staff`
  (unset by default, from 30 days to 10 years).

Two jobs join the job table, each run every 30 seconds under the engine's job claims where
`Hermes:Jobs:Enabled` is on, in an application that registers conversations:

- `conversation-timers`: wakes snoozed conversations, fires `waiting` and `closing`, and
  closes a support conversation silent for `Support:CloseAfter`.
- `attachment-scans`: where attachments are enabled, scans each new attachment, deletes the
  bytes of the attachments that are to go, and raises `attachment-scan`.

The retention job also runs conversations' own retention step after its deletions, at
most 1,000 conversations a run for each rule.

### Migrations that drop or rewrite data

None. The six added since 1.0.0-rc.2, which this release's `hermes migrate` applies before
the release starts, add tables, columns and rows of the job table to the `messaging`
schema, and drop none:

- `Conversations`: the tables `conversations`, `participants`, `messages` and `mentions`.
- `LiveConnection`: the table `connections`, and each participant's last message told while
  away.
- `SupportSide`: support's columns of `conversations`, a visitor's keyed hash in
  `participants`, each message's owner, and the tables `support_agents`, `support_blocks`,
  `support_acknowledgements` and `support_counters`. It fills each existing message's new
  owner from its author, and changes no existing value.
- `DeskTools`: a support conversation's tags, language, fired replies and timers, the table
  `support_replies`, and the job `conversation-timers`.
- `SupportEnd`: a support conversation's survey and its transcript sends.
- `Attachments`: the table `attachments`, and the job `attachment-scans` and the alert
  `attachment-scan` admitted.

A process of 1.0.0-rc.2 beside them records and dispatches as before, and answers erasure
and export `messaging.release.newer`.

### What the deployment does

The library cannot meet these alone; the deployment meets each of them. Two are new in
1.0.0-rc.3: **Conversations** and **Template sources**; **Jobs** now covers
`conversation-timers` and `attachment-scans`.

- **Sender rules.** For every sender: SPF or DKIM; forward and reverse DNS that agree for the
  sending server's IP address; TLS. For a bulk sender (about 5,000 or more messages a day to
  Gmail): SPF and DKIM; a DMARC record, whose policy may be `none`; a From domain aligned
  with SPF or DKIM; one-click unsubscribe and a visible link on marketing and subscribed
  messages. Every From domain the deployment or a kind declares is one the submission
  server signs for with DKIM, its signature covering `From`, `List-Unsubscribe` and
  `List-Unsubscribe-Post` (RFC 8058 section 4). These are Gmail's and Yahoo's published
  rules.
- **The report key.** The host's request logging, tracing and reverse proxy redact the path
  of the SMS report route, which carries the report key.
- **Outbound tracing.** Where the host traces its outbound HTTP requests, it redacts the
  paths of the library's requests to the push services, since a push endpoint's path is
  its subscription.
- **Allowed push hosts.** An entry the deployment adds to `Hermes:Push:AllowedHosts` is a
  push service it vouches for.
- **Shared provider accounts.** Messages other software of the host hands to the providers
  directly, through `AddHermesChannels` alone, pass none of the engine's limits and rates,
  the allowed prefixes alone applying to them, so the provider account carries both.
- **Clocks.** The processes' clocks agree within one second, which a host synchronised by
  NTP keeps, since a claim's expiry, an alert's instant and a job's instant written by one
  process are judged by the clocks of the others.
- **Environment.** Every application that must send runs in an environment other than
  Development, since the development inbox captures every message where the environment is
  Development.
- **The in-app list.** `Hermes:InApp:AlwaysOn` is set alike in every application of the
  deployment, and the host's frontend shows an entry's `title` and `body` as text, never
  as markup.
- **Conversations.** The host's frontend shows a message's text, a file name and a topic's
  name as text, never as markup.
- **Write versions.** A key's write version is never set back to a lower one, a rollback
  included: an older release restarts with the current write versions, and a version made
  wrongly is replaced by a new, higher one.
- **Declarations.** Every application that dispatches, runs the retention job, maps the
  unsubscribe or preferences group, or calls `IPreferences`, `ISuppressions` or `ISubjects`
  declares, or retires, every kind of the deployment.
- **Observer.** A deployment that observes turns `Hermes:Observer:Enabled` on in every
  application.
- **Jobs.** Each job of the job table runs in at least one application, the `balance` job
  where SMS is used, `conversation-timers` where support topics are configured and
  `attachment-scans` where attachments are enabled, since a job no application runs raises
  no `job-stalled`.
- **Stopping.** The host's shutdown timeout (`HostOptions.ShutdownTimeout`, 30 seconds by
  default) and the orchestrator's grace period before it ends a process are above
  `Hermes:Dispatch:Timeout`, so a stopped pass, whose remaining calls and row waits share
  one such timeout counted from the stop, records each answer that comes within it.
- **PostgreSQL.** Version 16 or later. The integration tests run on `postgres:16.15`, so no
  statement needs a later version.
- **Sends in one host transaction.** A send recorded in a host's transaction holds, until
  that transaction ends, one advisory lock per `hashKeys` version held for its person and
  one for its idempotency key, in PostgreSQL's shared lock table, which holds
  `max_locks_per_transaction` times the server's `max_connections`,
  `max_prepared_transactions` and worker processes in all. A host records a large batch in
  transactions of bounded size, or each send without a transaction.
- **Rolling back.** A release older than the database's newest migration runs only during a
  deployment. While it runs, beside the newer release or alone after a rollback, it answers
  erasure and export `messaging.release.newer`, its retention job retires no key version
  and protects no value again, it does not dispatch the deliveries the newer release left
  on a channel or reason it does not know, and each of its processes raises the
  `newer-release` alert on its key-table timer, which reads the migration history. A
  deployment therefore rolls forward rather than back.
- **Template sources.** A host `ITemplateSource` that reads `Kind == null` as a page also
  reads `Reply`: a key with `Reply` set names an automatic reply.

## 1.0.0-rc.2

The second release candidate of 1.0.0. A delivery notice, and a delivery's export, now
carry the idempotency key its send was recorded with. It ships the six packages of
1.0.0-rc.1, each at this version and each depending on the others it uses at exactly this
version. Until a final release, a release candidate may change or remove anything; this
one removes nothing, and names each change since 1.0.0-rc.1 below.

### Changes since 1.0.0-rc.1

- **`DeliveryNotice`.** It gains `string? IdempotencyKey`, its last member: the key the
  notice's send was recorded with, as the host gave it, or none where the send had none,
  the delivery's content has gone or its person was erased. The notice's `ToString` prints
  it no more than it prints the person. The member is an optional `init` property, as
  `Person` and `Reason` are: the type's constructor is unchanged, takes no argument, and a
  host that creates a notice with an object initializer, in a test of its observer for
  instance, compiles as before and creates one without a key.
- **`DeliveryExport`.** It gains `string? IdempotencyKey`, its last member: the key the
  delivery keeps of its send while it keeps it, none where the delivery is marked past
  content retention, erased or rejected. Its `ToString` prints it no more than it prints
  the destination. It is an optional `init` property, so a host's code that creates an
  export compiles as before.
- **Each delivery keeps the key.** Every delivery of a send keeps its idempotency key,
  protected as its destination is: by the protector that protects the person's values,
  bound to its row and its person, and protected again with the delivery's other values
  when the content keys turn. A notice reads it from its delivery, not from the send's
  idempotency row, which lapses after `Hermes:Idempotency:Window`, so a notice told after
  that row has lapsed, the notice of a provider's report moving `sent` to `delivered` or
  `failed` among them, still carries the key.
- **The key goes with the content.** The retention job deletes it with the delivery's
  content, `Hermes:Retention:Content` after its final status, and a notice told after
  that carries none. `ISubjects.EraseAsync` deletes it with the person's other values, and
  the notices it keeps for the observer carry none, as they carry no person.
- **A key that does not open.** A notice whose key answers that the person's key was
  erased is told without it; one whose key does not open under its binding is told
  without it and raises `content-rejected`; one whose key cannot open yet, its protector's
  key unavailable or its protector not registered, is not told, and is told at a later run
  as a notice the observer did not take, so `observer-failing` covers a lasting hold.
- **No backfill.** A delivery recorded before the migration keeps no key, so its notices
  and its export carry none, even where its send's idempotency row is still live.
- **Migrations added.** `SendIdempotencyKey`, which this release's `hermes migrate` applies
  before the release starts; it is described below.

### Migrations that drop or rewrite data

None. `SendIdempotencyKey`, the one added since 1.0.0-rc.1, adds a nullable column to the
deliveries' table that holds the protected idempotency key each delivery keeps of its
send; it drops and rewrites no data. A process of 1.0.0-rc.1 beside it records and
dispatches as before, the deliveries it records keeping no key, and answers erasure and
export `messaging.release.newer`.

### What the deployment does

The library cannot meet these alone; the deployment meets each of them. None is new in
1.0.0-rc.2.

- **Sender rules.** For every sender: SPF or DKIM; forward and reverse DNS that agree for the
  sending server's IP address; TLS. For a bulk sender (about 5,000 or more messages a day to
  Gmail): SPF and DKIM; a DMARC record, whose policy may be `none`; a From domain aligned
  with SPF or DKIM; one-click unsubscribe and a visible link on marketing and subscribed
  messages. Every From domain the deployment or a kind declares is one the submission
  server signs for with DKIM, its signature covering `From`, `List-Unsubscribe` and
  `List-Unsubscribe-Post` (RFC 8058 section 4). These are Gmail's and Yahoo's published
  rules.
- **The report key.** The host's request logging, tracing and reverse proxy redact the path
  of the SMS report route, which carries the report key.
- **Outbound tracing.** Where the host traces its outbound HTTP requests, it redacts the
  paths of the library's requests to the push services, since a push endpoint's path is
  its subscription.
- **Allowed push hosts.** An entry the deployment adds to `Hermes:Push:AllowedHosts` is a
  push service it vouches for.
- **Shared provider accounts.** Messages other software of the host hands to the providers
  directly, through `AddHermesChannels` alone, pass none of the engine's limits and rates,
  the allowed prefixes alone applying to them, so the provider account carries both.
- **Clocks.** The processes' clocks agree within one second, which a host synchronised by
  NTP keeps, since a claim's expiry, an alert's instant and a job's instant written by one
  process are judged by the clocks of the others.
- **Environment.** Every application that must send runs in an environment other than
  Development, since the development inbox captures every message where the environment is
  Development.
- **The in-app list.** `Hermes:InApp:AlwaysOn` is set alike in every application of the
  deployment, and the host's frontend shows an entry's `title` and `body` as text, never
  as markup.
- **Write versions.** A key's write version is never set back to a lower one, a rollback
  included: an older release restarts with the current write versions, and a version made
  wrongly is replaced by a new, higher one.
- **Declarations.** Every application that dispatches, runs the retention job, maps the
  unsubscribe or preferences group, or calls `IPreferences`, `ISuppressions` or `ISubjects`
  declares, or retires, every kind of the deployment.
- **Observer.** A deployment that observes turns `Hermes:Observer:Enabled` on in every
  application.
- **Jobs.** Each job of the job table runs in at least one application, the `balance` job
  where SMS is used, since a job no application runs raises no `job-stalled`.
- **Stopping.** The host's shutdown timeout (`HostOptions.ShutdownTimeout`, 30 seconds by
  default) and the orchestrator's grace period before it ends a process are above
  `Hermes:Dispatch:Timeout`, so a stopped pass, whose remaining calls and row waits share
  one such timeout counted from the stop, records each answer that comes within it.
- **PostgreSQL.** Version 16 or later. The integration tests run on `postgres:16.15`, so no
  statement needs a later version.
- **Sends in one host transaction.** A send recorded in a host's transaction holds, until
  that transaction ends, one advisory lock per `hashKeys` version held for its person and
  one for its idempotency key, in PostgreSQL's shared lock table, which holds
  `max_locks_per_transaction` times the server's `max_connections`,
  `max_prepared_transactions` and worker processes in all. A host records a large batch in
  transactions of bounded size, or each send without a transaction.
- **Rolling back.** A release older than the database's newest migration runs only during a
  deployment. While it runs, beside the newer release or alone after a rollback, it answers
  erasure and export `messaging.release.newer`, its retention job retires no key version
  and protects no value again, it does not dispatch the deliveries the newer release left
  on a channel or reason it does not know, and each of its processes raises the
  `newer-release` alert on its key-table timer, which reads the migration history. A
  deployment therefore rolls forward rather than back.

## 1.0.0-rc.1

The first release candidate of 1.0.0. It holds everything releases 1.0.0 and 1.1.0
released, both of which are withdrawn, and adds the in-application notification list.
Until a final release, a release candidate may change or remove anything, and the notes of
each release candidate name every change and removal since the release before it. It ships
six packages, each at this version and each depending on the others it uses at exactly
this version:

- `Hermes.Core`: the public types and contracts every other package shares.
- `Hermes.Channels`: the channel providers (SMTP email and Web Push), the SMS channel's rules,
  and their registration.
- `Hermes`: the sending engine: kinds, the outbox, the checks, dispatch, templates, the
  endpoints and the schema.
- `Hermes.Testing`: in-memory providers, fixed secrets, an on-demand dispatcher and a test
  database's migration.
- `Hermes.Cli`: the .NET tool `hermes`, whose `hermes migrate` applies the migrations.
- `Hermes.Development`: the development inbox. In an application whose environment is
  Development, `AddHermesDevelopmentInbox` captures every message instead of sending it,
  whatever the order of the host's own provider registrations, and keeps each as a JSON
  record, an email's `.eml` beside it, in a folder of the user's own on the machine.
  `MapHermesDevelopmentInbox` maps a page and a small JSON API that read the records,
  answered only to the machine itself under a loopback host name, and `DevelopmentInbox`
  reads and waits for them in a .NET test. In any other environment both calls do nothing.
  `AddHermesTesting`'s in-memory providers are still used where it is registered, in
  either order.

The library ships no SMS gateway. A host that uses SMS, or maps the SMS report group,
registers its own `ISmsProvider`; without one the start stops with
`messaging.startup.declarationmissing`, `key` `smsProvider`.

The in-application list, in `Hermes.Core` and `Hermes`:

- A kind declares the in-app channel with `KindBuilder.InApp(target, targetValues)`. Its
  delivery, sent to a person, is rendered when the send is recorded in every declared
  language, its `title` at most 128 characters and its `body` at most 1,024, and is
  written, when dispatched, as an entry of the person's list, in every environment, with
  no provider, no rate and no quiet hours. A send of the channel to an anonymous
  recipient is refused.
- `IInApp` reads a person's list a page at a time, newest first, in the language asked
  for, counts their unread entries, and marks one entry, or all of them, read, which
  moves each delivery from `sent` to the new status `read` and tells the observer.
- `MapHermesInApp` maps the in-app group, `GET /inapp`, `GET /inapp/unread`,
  `POST /inapp/{id}/read` and `POST /inapp/read`, for the person signed in.
- An entry is kept `Hermes:Retention:InApp`, 180 days by default, and is erased and
  exported with its person.
- With `Hermes:InApp:AlwaysOn` on, the in-app channel of every category cannot be turned
  off: `IPreferences.SetAsync` refuses it with `messaging.preference.fixed` and the
  preferences read and route leave it out.

### For a host that copied 1.1.0's packages

Against 1.1.0's release commit, the public surface files and the release baseline of this
release differ by additions alone: 1.0.0-rc.1 removes nothing, and changes no public
member, route, status, body member, code, configuration key, enumeration value or metric
name that 1.1.0 had.

- **The version.** 1.0.0-rc.1 is below 1.1.0 in Semantic Versioning's precedence, so a host
  names `1.0.0-rc.1` itself for all six packages together, not a version from 1.1.0 up.
- **Public members added.** In `Hermes.Core`: `IInApp` (`ListAsync`, `UnreadCountAsync`,
  `MarkReadAsync`, `MarkAllReadAsync`), `InAppPage`, `InAppEntry`, `EntryId`,
  `InAppEntryExport`, `SubjectExport.Entries`, `Channel.InApp` and `DeliveryStatus.Read`.
  In `Hermes`: `KindBuilder.InApp(target, targetValues)` and `MapHermesInApp(prefix)`.
- **Routes added.** Under the in-app group's prefix: `GET /inapp` (200, 400, 401, 503),
  `GET /inapp/unread` (200, 401, 503), `POST /inapp/{id}/read` (204, 400, 401, 404, 413,
  415, 503) and `POST /inapp/read` (204, 400, 401, 413, 415, 503), with the bodies of a page
  of entries (`entries`, `next`), an entry (`id`, `kind`, `category`, `language`, `title`,
  `body`, `target`, `targetValues`, `writtenAt`, `readAt`) and the unread count (`unread`).
- **Codes added.** `messaging.preference.fixed`, `messaging.inapp.cursorinvalid` and
  `messaging.inapp.unknown`.
- **Configuration keys added.** `Hermes:Retention:InApp`, 180 days by default, and
  `Hermes:InApp:AlwaysOn`, off by default.
- **Values added.** The channel `inapp` and the status `read`. Code of a host that switches
  over `Channel` or `DeliveryStatus`, or an observer that reads a delivery's status, meets
  them only where a kind declares the in-app channel: a delivery reaches `read` only when
  its in-app entry is marked read, and the observer is told of that move.
- **Migrations added.** `InAppList`, which this release's `hermes migrate` applies before
  the release starts; it is described below.

### Migrations that drop or rewrite data

None. The migrations of this release create the schema `messaging` and its tables. Of them,
`InAppList`, the one added since 1.1.0, adds the in-app list's table and a nullable column
that holds an in-app delivery's rendered parts until it is dispatched, and widens the checks
of the closed sets to the new channel and status; it drops and rewrites no data. A process
of release 1.1.0 beside it leaves in-app deliveries and the `read` status alone, as it
leaves any row it does not know, and answers erasure and export `messaging.release.newer`.

### What the deployment does

The library cannot meet these alone; the deployment meets each of them.

- **Sender rules.** For every sender: SPF or DKIM; forward and reverse DNS that agree for the
  sending server's IP address; TLS. For a bulk sender (about 5,000 or more messages a day to
  Gmail): SPF and DKIM; a DMARC record, whose policy may be `none`; a From domain aligned
  with SPF or DKIM; one-click unsubscribe and a visible link on marketing and subscribed
  messages. Every From domain the deployment or a kind declares is one the submission
  server signs for with DKIM, its signature covering `From`, `List-Unsubscribe` and
  `List-Unsubscribe-Post` (RFC 8058 section 4). These are Gmail's and Yahoo's published
  rules.
- **The report key.** The host's request logging, tracing and reverse proxy redact the path
  of the SMS report route, which carries the report key.
- **Outbound tracing.** Where the host traces its outbound HTTP requests, it redacts the
  paths of the library's requests to the push services, since a push endpoint's path is
  its subscription.
- **Allowed push hosts.** An entry the deployment adds to `Hermes:Push:AllowedHosts` is a
  push service it vouches for.
- **Shared provider accounts.** Messages other software of the host hands to the providers
  directly, through `AddHermesChannels` alone, pass none of the engine's limits and rates,
  the allowed prefixes alone applying to them, so the provider account carries both.
- **Clocks.** The processes' clocks agree within one second, which a host synchronised by
  NTP keeps, since a claim's expiry, an alert's instant and a job's instant written by one
  process are judged by the clocks of the others.
- **Environment.** Every application that must send runs in an environment other than
  Development, since the development inbox captures every message where the environment is
  Development.
- **The in-app list.** `Hermes:InApp:AlwaysOn` is set alike in every application of the
  deployment, and the host's frontend shows an entry's `title` and `body` as text, never
  as markup.
- **Write versions.** A key's write version is never set back to a lower one, a rollback
  included: an older release restarts with the current write versions, and a version made
  wrongly is replaced by a new, higher one.
- **Declarations.** Every application that dispatches, runs the retention job, maps the
  unsubscribe or preferences group, or calls `IPreferences`, `ISuppressions` or `ISubjects`
  declares, or retires, every kind of the deployment.
- **Observer.** A deployment that observes turns `Hermes:Observer:Enabled` on in every
  application.
- **Jobs.** Each job of the job table runs in at least one application, the `balance` job
  where SMS is used, since a job no application runs raises no `job-stalled`.
- **Stopping.** The host's shutdown timeout (`HostOptions.ShutdownTimeout`, 30 seconds by
  default) and the orchestrator's grace period before it ends a process are above
  `Hermes:Dispatch:Timeout`, so a stopped pass, whose remaining calls and row waits share
  one such timeout counted from the stop, records each answer that comes within it.
- **PostgreSQL.** Version 16 or later. The integration tests run on `postgres:16.15`, so no
  statement needs a later version.
- **Sends in one host transaction.** A send recorded in a host's transaction holds, until
  that transaction ends, one advisory lock per `hashKeys` version held for its person and
  one for its idempotency key, in PostgreSQL's shared lock table, which holds
  `max_locks_per_transaction` times the server's `max_connections`,
  `max_prepared_transactions` and worker processes in all. A host records a large batch in
  transactions of bounded size, or each send without a transaction.
- **Rolling back.** A release older than the database's newest migration runs only during a
  deployment. While it runs, beside the newer release or alone after a rollback, it answers
  erasure and export `messaging.release.newer`, its retention job retires no key version
  and protects no value again, it does not dispatch the deliveries the newer release left
  on a channel or reason it does not know, and each of its processes raises the
  `newer-release` alert on its key-table timer, which reads the migration history. A
  deployment therefore rolls forward rather than back.
