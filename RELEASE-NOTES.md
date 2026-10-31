# Release notes

## 1.2.0

A minor release, of additions alone. It ships all six packages at this version, each
depending on the others it uses at exactly this version, and adds the in-application
list to `Hermes.Core` and `Hermes`:

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

The release adds the channel `inapp`, the status `read`, the codes
`messaging.preference.fixed`, `messaging.inapp.cursorinvalid` and
`messaging.inapp.unknown`, the keys `Hermes:Retention:InApp` and `Hermes:InApp:AlwaysOn`,
and the in-app group's routes and bodies. It removes and changes nothing.

### Migrations that drop or rewrite data

None. The migration `InAppList` adds the in-app list's table and a nullable column that
holds an in-app delivery's rendered parts until it is dispatched, and widens the checks of
the closed sets to the new channel and status; it drops and rewrites no data. A process
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
  as markup. These duties are new in 1.2.0.
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

## 1.1.0

A minor release, of additions alone. It adds a sixth package, and ships all six at this
version, each depending on the others it uses at exactly this version:

- `Hermes.Development`: the development inbox. In an application whose environment is
  Development, `AddHermesDevelopmentInbox` captures every message instead of sending it,
  whatever the order of the host's own provider registrations, and keeps each as a JSON
  record, an email's `.eml` beside it, in a folder of the user's own on the machine.
  `MapHermesDevelopmentInbox` maps a page and a small JSON API that read the records,
  answered only to the machine itself under a loopback host name, and `DevelopmentInbox`
  reads and waits for them in a .NET test. In any other environment both calls do nothing.
  `AddHermesTesting`'s in-memory providers are still used where it is registered, in
  either order.
- `Hermes.Core`, `Hermes.Channels`, `Hermes`, `Hermes.Testing` and `Hermes.Cli`, as in
  1.0.0; they change nothing a host or caller uses.

The release adds the codes `messaging.inbox.notfound` and `messaging.inbox.notlocal`, the
keys `Hermes:DevelopmentInbox:Folder`, `KeepFor`, `MaxMessages` and `AllowRemote`, and the
inbox's routes and bodies. It removes and changes nothing.

### Migrations that drop or rewrite data

None. The release adds no migration.

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
  Development. This duty is new in 1.1.0.
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

## 1.0.0

The first release. It ships five packages, each at this version and each depending on the
others it uses at exactly this version:

- `Hermes.Core`: the public types and contracts every other package shares.
- `Hermes.Channels`: the channel providers (SMTP email and Web Push), the SMS channel's rules,
  and their registration.
- `Hermes`: the sending engine: kinds, the outbox, the checks, dispatch, templates, the
  endpoints and the schema.
- `Hermes.Testing`: in-memory providers, fixed secrets, an on-demand dispatcher and a test
  database's migration.
- `Hermes.Cli`: the .NET tool `hermes`, whose `hermes migrate` applies the migrations.

The library ships no SMS gateway. A host that uses SMS, or maps the SMS report group,
registers its own `ISmsProvider`; without one the start stops with
`messaging.startup.declarationmissing`, `key` `smsProvider`.

### Migrations that drop or rewrite data

None. The migrations of this release create the schema `messaging` and its tables.

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
