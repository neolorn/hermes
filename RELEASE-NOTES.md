# Release notes

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
