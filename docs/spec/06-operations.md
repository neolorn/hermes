# 06 — Operations

Configuration, the schema and its migrations, the database connection, the background
work, what the library reports about itself, the deployment's part in deliverability,
and the secrets and keys it needs. Every value named `Hermes:...` is configuration (`08`
section 4).

---

**MSG-OPS-001** — Every behaviour a deployment may want to change SHALL be
configuration, bound from the host's configuration section `Hermes`, with the default
and the rule `08` section 4 gives, validated when the application starts.

**Values (M-005, M-012).** The library checks each key of `08` section 4 itself, on the
raw configuration value and before binding, so a value outside its rule stops the start
with `messaging.startup.configurationinvalid`, `details.key` naming the key as `08`
spells it; the section is then bound through .NET's options, by the configuration
binder's source generator, with source-generated validation as a second line. A key
holding a list takes its default only where its section has no child after binding,
never by the options type starting with the default's items, since the binder adds
configured items to those already there. A value read again while running takes effect
for deliveries checked after it changes; a reloaded value outside its rule is ignored,
the last valid value staying in force, and raises `configuration-invalid` (MSG-OPS-006)
until it is put right. A key `08` marks "start only" takes effect at the next start. The
engine's keys (`08` section 4 names them), and the culture and time-zone checks here and
in `03` MSG-TPL-003, apply only where `AddHermes` is registered (`04` MSG-LIB-003). A
process registering it whose system offers no time zone but UTC (no time zone data
installed) stops the start with `messaging.startup.configurationinvalid`, `details.key`
`TimeZone`, since every send's zone would otherwise fall back to UTC unseen (`01`
MSG-SEND-003). The checks that depend on which endpoint groups an application maps run
when the host starts, after its groups are mapped.

*Source: M-005, M-012*

**Acceptance criteria**
1. Every key of `08` section 4 has its default when absent and stops the start when
   outside its rule.
2. A change to `Hermes:QuietHours:Start` while running applies to the next delivery
   checked; a change to a start-only key applies only after a restart; a reloaded value
   outside its rule leaves the last valid value in force and raises the alert.
3. Configuring `Hermes:Push:AllowedHosts` with one host leaves that host alone allowed.

---

**MSG-OPS-002** — The library's tables SHALL live in the schema `messaging`, created and
changed only by the library's migrations, applied by the deployment's pipeline before
the new version starts and never by the application at startup.

**Values (M-012, M-013).** The schema name is fixed, and does not change with the
library's name (`00`); the migration history table is `messaging.__migrations_history`,
so the library's history never meets a host's. The migrations are applied by
`hermes migrate`, the command of the `Hermes.Cli` tool of the same release, which reads
one JSON document on its standard input and nothing else:
`{ "connection": "<connection string>", "applicationRoles": [ "<role>", ... ] }`, the
roles optional. It applies the pending migrations one at a time through EF Core's
migrator, each to its own target and so in its own transaction, then grants each role
`USAGE` on the schema and `SELECT`, `INSERT`, `UPDATE` and `DELETE` on each of its
tables, so a host whose application runs under another role than the migration's can use
them. It exits 0 on success; on a failure it exits 1 and writes one JSON line on
standard error, `{ "code": "<code>", "details": { ... } }`, with a code of `08` section
1 (`messaging.migrate.inputinvalid`, `messaging.migrate.connectionfailed` or
`messaging.migrate.failed`), the migrations before the failing one staying applied.

The migrations EF Core's tool generates are made `internal sealed` in the commit that
adds them, so they stay off the public surface (`04` MSG-LIB-002), and that commit then
runs `dotnet format` on the migration's main file, which the tool writes without the
generated-code mark its other files carry, so the format gate (`07` MSG-ENG-012) passes.
Each migration leaves the schema usable by the release before it, so the processes of
two releases run side by side during a deployment; a change that cannot is split across
two releases. At startup the library checks that the database holds every migration of
its release and stops the start with `messaging.startup.schemamismatch`,
`details.migration` the first missing, where it does not; later migrations, of a newer
release, are accepted. A migration that drops or rewrites data is named in the release
notes.

*Source: M-012, M-013*

**Acceptance criteria**
1. Starting against a database missing the release's last migration stops the start.
2. `hermes migrate` twice in a row applies nothing the second time and exits 0 both
   times.
3. A role named in `applicationRoles` can read and write every table of the schema, and
   input that does not read exits 1 with `messaging.migrate.inputinvalid`.

---

**MSG-OPS-003** — The library SHALL use the host's PostgreSQL database through its own
connection, so a host may give it the same database as its own or another.

**Values (M-005, M-012, M-013).** The connection string is the secret `connection`
(MSG-OPS-008); the library's own work (the dispatcher, the jobs, its endpoints, a send
made without the host's transaction) runs on it. The dispatcher's listening connection
(`01` MSG-SEND-007) is a session connection on the secret `listenConnection` where the
deployment gives one, for a `connection` that passes through a transaction-mode pooler,
and on `connection` otherwise. A host that records sends in its own transactions (`01`
MSG-SEND-004) gives the library the database those transactions run on. The library uses
no retrying execution strategy on a host's transaction, and its columns holding a closed
set (a status, a channel) are `text` with a `CHECK` constraint, so the host's data
source needs no type mapping of the library's.

Every instant the library stores or compares is its `TimeProvider`'s (`07` MSG-ENG-005),
so a claim, whose expiry its holder writes (`01` MSG-SEND-007), is judged by the clocks
of the processes. The processes' clocks SHALL agree within one second, which a host
synchronised by NTP keeps: that is below every application's
`Hermes:Dispatch:ClaimTimeout` less twice its `Hermes:Dispatch:Timeout` (at least 30
seconds), so no live claim is taken over, and below every `Hermes:Dispatch:Interval` (at
least 3 seconds, so a job's one-second slack, MSG-OPS-004, still leaves a margin), so an
instant one process writes for an alert or a job is not judged passed by another before
its time; the per-second rates (`01` MSG-SEND-014), whose grain it equals, so that a
rate may be passed by at most its own count in a second, and the notified dispatch (`01`
MSG-SEND-004) rely on it too.

*Source: M-005, M-012, M-013*

**Acceptance criteria**
1. A send recorded in a host transaction on a data source the host built, with no
   configuration of the library's, commits with the host's write.

---

**MSG-OPS-004** — The library's background work SHALL run as hosted services in the
host's process, each turned on or off per application by configuration, and SHALL do
nothing twice when several processes run it.

**Values (M-005, M-007).**

| Job | Runs | Turned on by |
|---|---|---|
| `dispatch` (`01` MSG-SEND-007) | As `01` MSG-SEND-007 gives: on each notification of a committed send, every `Hermes:Dispatch:Interval`, and the other instants it names | `Hermes:Dispatch:Enabled` and `Hermes:Dispatch:Hosted` |
| `observer` (`01` MSG-SEND-017) | Every `Hermes:Dispatch:Interval`; it completes at once, delivering nothing, where `Hermes:Observer:Enabled` is off | `Hermes:Dispatch:Enabled` |
| `balance` (`02` MSG-SMS-006) | Every `Hermes:Sms:BalancePoll`, only in an application where SMS is used (`04` MSG-LIB-003), which alone takes its claim | `Hermes:Dispatch:Enabled`, whatever `Hermes:Jobs:Enabled` says, as `observer` is |
| `retention` (MSG-OPS-008; `01` MSG-SEND-019) | Daily at `Hermes:Jobs:RetentionAt`, in `Hermes:TimeZone` | `Hermes:Jobs:Enabled` |
| `subscriptions` (`02` MSG-PUSH-006) | Every `Hermes:Jobs:SubscriptionsInterval` | `Hermes:Jobs:Enabled` |
| `watch` (MSG-OPS-005, MSG-OPS-006, MSG-OPS-007, MSG-OPS-008; `01` MSG-SEND-017, MSG-SEND-018; `02` MSG-SMS-004, MSG-PUSH-005) | Every `Hermes:Dispatch:Interval` | `Hermes:Jobs:Enabled` |

The dispatcher's work is claimed delivery by delivery (`01` MSG-SEND-007), and it has no
row in the job table; `dispatch-lag` watches it (MSG-OPS-005). Every other job holds a
claim on its row of the library's job table while it runs: a holder, an instant and the
instant the claim expires, `@now` plus the holder's own `Hermes:Jobs:ClaimTimeout`,
taken by a conditional update where the row is free or its claim has expired and its
last start is at least the claimant's own interval for the job, less one second, before
`@now` (so a timer's small delays never skip a run; for the retention job, where no run
has started since the last `Hermes:Jobs:RetentionAt`), so each job runs once per
interval across the deployment, renewed with a new expiry after each unit of the run's
work (a batch; each notice the `observer` job delivers and each condition the watch job
judges; the `balance` job's reading and each alert call it makes) by an update
conditional on the claim still being its own (a run whose renewal changes nothing stops
before its next unit), and released, with the instant of completion, when the run ends,
so one process at a time runs each job and one that dies holds it no longer than the
timeout. The retention and subscriptions jobs work in batches of
`Hermes:Jobs:BatchSize`, each selecting its rows `FOR UPDATE SKIP LOCKED`, so a row a
host's open transaction holds (a lapsed idempotency row a send deletes, `01`
MSG-SEND-009) is left for a later run rather than waited for. A job that fails is logged
and runs again at its next time. The watch job raises `job-stalled` for a job, the watch
job itself included since any process running jobs evaluates it, whose last completion
and last renewal are both older than `Hermes:Jobs:StallFactor` of its intervals plus
`Hermes:Dispatch:Timeout`, the longest one unit may take, once a process that may run it
has started it (its row records the first start), so a job no application runs is never
stalled; it also raises `observer-failing` (`01` MSG-SEND-017), raises again and clears
`sms-report-rejected` and `push-auth-failing` by their counts (`02` MSG-SMS-004,
MSG-PUSH-005), clears `newer-release` (MSG-OPS-007), `key-unavailable` and
`content-key-limit` (MSG-OPS-008) and `content-rejected` (`01` MSG-SEND-018), and drops
the SMS reports held past `Hermes:Sms:ReportHold` (`02` MSG-SMS-004), whose number the
report route keeps under `Hermes:Sms:ReportHoldMax`. Besides the jobs, every process
that registers `AddHermes`, whether or not it runs jobs, keeps the key table current on
a timer of its own (MSG-OPS-008).

*Source: M-005, M-007, M-008, M-009, M-012*

**Acceptance criteria**
1. With `Hermes:Dispatch:Enabled` false, an application records sends, its hosted
   services dispatch none, and `IDispatcher.RunPassAsync` throws
   `InvalidOperationException`; with it on and `Hermes:Dispatch:Hosted` false, no pass
   runs until `RunPassAsync` is called, which then dispatches through the shipped
   providers, and the `observer` job tells the observer.
2. Two processes running the retention job at once delete each row once, and two running
   the balance job, started some seconds apart, record one reading per poll.
3. A process that dies holding a job's claim leaves it to another process after
   `Hermes:Jobs:ClaimTimeout`.
4. An application with `Hermes:Dispatch:Enabled` off runs no balance job, and one with
   it on where SMS is used runs it with `Hermes:Jobs:Enabled` off.
5. A job no application runs raises no `job-stalled`.

---

**MSG-OPS-005** — The library SHALL log what it does without writing any destination,
content or credential, and SHALL publish metrics and a health check a host can collect.

**Values (M-005, M-012).** Log entries go through source-generated `LoggerMessage`
methods and name deliveries by identifier, kind, category, channel, status and reason,
never by an address, a number, an endpoint, a person reference, or text. Metrics are
published on the .NET meter of `08` section 8, with its instruments. The health check of
`08` section 8, registered by `AddHermes` in an application with
`Hermes:Dispatch:Enabled` on, so a lag never fails another application's health, reports
unhealthy, and the watch job raises `dispatch-lag` (MSG-OPS-006), while the oldest
delivery `waiting` or `inflight` whose became-due instant (`01` MSG-SEND-007) has passed
became due longer ago than `Hermes:Dispatch:LagThreshold`, so a delivery held for a key,
or waiting on the rate, counts steadily and one on its back-off, before its `NotBefore`
or in quiet hours does not, a row this release does not know left out of both (`01`
MSG-SEND-007).

*Source: M-005, M-012*

**Acceptance criteria**
1. A test capturing every log entry of a send's life, its failures included, finds no
   destination, no content and no person reference.
2. The meter reports a delivery's final status, and the health check turns unhealthy
   past the threshold.

---

**MSG-OPS-006** — The library SHALL raise each alert condition of `08` section 5 through
the alert sink when it starts to hold, raise it again at a fixed interval while it
holds, and clear it once when it stops.

**Values (M-005, M-012).** `IAlertSink.RaiseAsync(condition, scope, details)` and
`ClearAsync(condition, scope)` (`04` MSG-LIB-002); the library's own sink logs each at
the error level. A condition is raised when it starts to hold and again each time
`Hermes:Alerts:Repeat` (default 1 hour) has passed since it was last raised while it
still holds, and cleared once when it stops holding, as `08` section 5 defines for each.
A condition's state, per scope (holding or not, when it was last raised, and for
`key-unavailable`, `content-key-limit`, `content-rejected` and `newer-release` the
instant until which it holds, which each process that meets it writes as `@now` plus
twice its own `Hermes:Dispatch:Interval` where later than the one recorded, on the
library's own connection, never inside a host's transaction, at most once per that
interval for each scope, so the watch job judges it by the writer's interval, whatever
its own, and clears it by an update conditional on the instant it read, so a write
landing between its read and its clear is not lost), is kept in the library's alert
table, so the deployment raises it once however many processes see it.

*Source: M-005, M-012*

**Acceptance criteria**
1. With `Hermes:Alerts:Repeat` 1 hour, a condition holding from 00:00 to 02:30 is raised
   at 00:00, 01:00 and 02:00 and cleared once.
2. Two processes seeing the same condition raise it once.

---

**MSG-OPS-007** — The deployment SHALL meet what the library cannot do alone, and the
release notes SHALL state it.

**Values (M-003, M-005, M-007, M-008, M-009, M-010, M-012, M-013).**
- **Sender rules.** For every sender: SPF or DKIM; forward and reverse DNS that agree
  for the sending server's IP address; TLS. For a bulk sender (about 5,000 or more
  messages a day to Gmail): SPF and DKIM; a DMARC record (its policy may be `none`); a
  From domain aligned with SPF or DKIM; one-click unsubscribe and a visible link on
  marketing and subscribed messages (`02` MSG-EMAIL-006). Every From domain a deployment
  or a kind declares (`02` MSG-EMAIL-002) is one the submission server signs for with
  DKIM, its signature covering `From`, `List-Unsubscribe` and `List-Unsubscribe-Post`
  (RFC 8058 section 4). These are Gmail's and Yahoo's published rules (`decision-log.md`
  M-010).
- **The report key.** The host's request logging, tracing and reverse proxy redact the
  path of the SMS report route (`05` MSG-API-006).
- **Outbound tracing.** Where the host traces its outbound HTTP requests, it redacts the
  paths of the library's requests to the push services and the SMS provider, since a
  push endpoint's path is its subscription (`02` MSG-CHAN-001).
- **Allowed push hosts.** An entry the deployment adds to `Hermes:Push:AllowedHosts` is
  a push service it vouches for (`02` MSG-PUSH-004).
- **Shared provider accounts.** Messages an identity library sends through the bridge's
  transports pass none of the engine's limits and rates, the allowed prefixes alone
  applying to them, so the provider account carries both (`04` MSG-LIB-007).
- **Clocks.** The processes' clocks agree as MSG-OPS-003 says.
- **Write versions.** A key's write version is never set back to a lower one, a rollback
  included: an older release restarts with the current write versions; a version made
  wrongly is replaced by a new, higher one (MSG-OPS-008).
- **Declarations.** Every application that dispatches, runs the retention job, maps the
  unsubscribe or preferences group, or calls `IPreferences`, `ISuppressions` or
  `ISubjects` declares, or retires, every kind of the deployment (`01` MSG-SEND-001).
- **Observer.** A deployment that observes turns `Hermes:Observer:Enabled` on in every
  application (`01` MSG-SEND-017).
- **Jobs.** Each job with a row in the job table (MSG-OPS-004) runs in at least one
  application, the `balance` job where SMS is used, since a job no application runs
  raises no `job-stalled`.
- **Stopping.** The host's shutdown timeout (`HostOptions.ShutdownTimeout`, 30 seconds
  by default) and the orchestrator's grace period before it ends a process are above
  `Hermes:Dispatch:Timeout`, so a stopped pass, whose remaining calls and row waits
  share one such timeout counted from the stop, records each answer that comes within it
  (`01` MSG-SEND-007).
- **PostgreSQL.** Version 16 or later; the integration tests run on the floor,
  `postgres:16.15` (`07` MSG-ENG-008), so no statement needs a later version.
- **Sends in one host transaction.** A send recorded in a host's transaction holds,
  until that transaction ends, one advisory lock per `hashKeys` version held for its
  person and one for its idempotency key, in PostgreSQL's shared lock table, which holds
  `max_locks_per_transaction` times the server's `max_connections`,
  `max_prepared_transactions` and worker processes in all; a host records a large batch
  in transactions of bounded size, or each send without a transaction (`01`
  MSG-SEND-004).
- **Rolling back.** A release older than the database's newest migration runs only
  during a deployment: while it runs, beside the newer release or alone after a
  rollback, it answers erasure and export `messaging.release.newer`, its retention job
  retires no key version and protects no value again, the deliveries the newer release
  left on a channel or reason it does not know are not dispatched by it (MSG-OPS-008;
  `01` MSG-SEND-007, MSG-SEND-020), and each of its processes raises the `newer-release`
  alert on its key-table timer, which reads the migration history (MSG-OPS-008), so a
  deployment rolls forward rather than back.

*Source: M-003, M-005, M-007, M-008, M-009, M-010, M-012, M-013*

**Acceptance criteria**
1. The release notes of each release (`RELEASE-NOTES.md`, `07` MSG-ENG-001) state every
   requirement above (checked in review).

---

**MSG-OPS-008** — The library SHALL read every secret its application's used parts need
through the secret source when the application starts, SHALL never hold a secret in
configuration, logs or its tables, and SHALL rotate every key by adding a version
without losing what an older version made or letting a process act on a version it does
not hold.

**Values (M-005, M-007, M-009, M-012).** The secrets, by name:

| Name | What it is | Used by |
|---|---|---|
| `connection` | The database connection string, UTF-8 | MSG-OPS-003 |
| `listenConnection` | Optional: a session connection string for the dispatcher's listening connection, UTF-8 | MSG-OPS-003; `01` MSG-SEND-007 |
| `contentKeys` | Versions, each 32 bytes: the library protector's AES-256-GCM keys | `01` MSG-SEND-018 |
| `hashKeys` | Versions, each at least 32 bytes: the keys of the keyed hash | `01` MSG-SEND-009, MSG-SEND-012, MSG-SEND-014; `02` MSG-PUSH-002, MSG-PUSH-003; `05` MSG-API-005 |
| `tokenKeys` | Versions, each at least 32 bytes: the keys signing unsubscribe tokens | `02` MSG-EMAIL-006; `05` MSG-API-005 |
| `smsReportKey` | Versions, each at least 16 bytes: the report route's key, written in the route as base64url | `02` MSG-SMS-003, MSG-SMS-004 |
| `emailSubmission` | The submission account, UTF-8 JSON `{ "userName", "password" }` | `02` MSG-EMAIL-001 |
| `smsWe` | WE's credential string | `02` MSG-SMS-003 |
| `pushVapidKeys` | Versions, each a P-256 private key as its 32-byte scalar | `02` MSG-PUSH-007 |

The keys are needed by these parts: `hashKeys` by the engine (`AddHermes`);
`contentKeys` by an engine application that records sends (`04` MSG-LIB-003),
dispatches, maps the push subscription group or runs the retention job, an export in
another answering `messaging.key.unavailable` for any value it cannot open (`01`
MSG-SEND-020); `tokenKeys` by one that records sends where an optional category has an
email kind, or that maps the unsubscribe group; `smsReportKey` by an engine application
where SMS is dispatched (`04` MSG-LIB-003) and `Hermes:Sms:ReportBaseAddress` is set
(`02` MSG-SMS-004), or by one that maps the report group; `pushVapidKeys` by one where
push is dispatched, or that maps the push subscription group. A process reads, checks
and records in the key table only the keys a used part of it needs, save the last-made
instants a pass writes for a token's versions (below) (an application without the engine
has no key table, so it reads the keys its shipped providers need and checks their form
only); a key no used part needs is not read, so a copy left in an application that does
not use it never revives or records a version. A secret a used part needs (`04`
MSG-LIB-003) that the source does not answer stops the start with
`messaging.startup.secretunavailable`; one that answers outside its form (short, not a
scalar of P-256, JSON that does not read) with `messaging.startup.secretinvalid`; both
name it in `details.key`. Secrets are held in byte arrays, never in strings where the
API allows, and are not logged. The single-valued secrets (`connection`,
`listenConnection`, `emailSubmission`, `smsWe`) are used at their highest version, and a
password changed in place in them is an ordinary change.

**The keyed hash** is HMAC-SHA-256 under a `hashKeys` version of a domain, a zero byte
and the value, the domain keeping one kind of value from ever matching another: `email`,
an address in the form it is sent (its domain in ASCII form) and in lower case; `sms`, a
number in E.164; `push-endpoint`, an endpoint as given; `push-topic`, a kind, a zero
byte and a topic; `person`, a person reference; `idempotency`, a send's canonical form
(`01` MSG-SEND-009). A stored hash is made under the write version and looked up under
every version this process holds, the versions in use then read as below, and no lock
and no identity rests on one version's hash: a lock derived from a keyed hash is taken
under every version this process holds, in ascending order of version (`01`
MSG-SEND-014; `02` MSG-PUSH-003), and a lock or identity that needs no secrecy is an
unkeyed SHA-256 (`01` MSG-SEND-009; `02` MSG-SMS-004).

**Versions of the five keys** (`contentKeys`, `hashKeys`, `tokenKeys`, `smsReportKey`,
`pushVapidKeys`). New values are made under a write version:
`Hermes:Secrets:ContentKeys:WriteVersion`, `Hermes:Secrets:HashKeys:WriteVersion`,
`Hermes:Secrets:TokenKeys:WriteVersion`, `Hermes:Secrets:SmsReportKey:WriteVersion` and
`Hermes:Secrets:PushVapidKeys:WriteVersion`, all start only. Where a key has one
version, that is its write version; where it has more, its write version is required, so
a version never becomes the one written under just by being added. To rotate, the
deployment adds the new version to every process with the write version held at the old
one, and once every process holds it, makes it the write version. A write version never
moves back to a lower one: a version found wrongly made is replaced by a new, higher
version in the same way. A row records the version it was made under, and a row holding
values of the library's protector records its `contentKeys` version in an indexed
column; a token names its versions (`05` MSG-API-005); a report key matches any held
version.

**The key table** records, for each version of the five keys: its check value, the first
16 bytes of HMAC-SHA-256 under the version of the text `messaging-key-check` (for
`pushVapidKeys`, the whole 65-byte public key), written the first time a process starts
holding it; the instant values were first made under it; the instant they were last made
under it; and whether it is retired. It also records the longest
`Hermes:Unsubscribe:TokenLifetime` of any process that reads `tokenKeys` (one that makes
tokens or one that verifies them), each such process writing its own where longer, at
start and on the timer below, so the retention job judges token-related versions by the
longest lifetime a token is made or verified under, whatever its own; while none is
recorded no token has been made, and the lifetime adds nothing to the conditions below.
Every process that registers `AddHermes`, whether or not it runs jobs, on its own
connection and never from a send, so no host transaction touches the key table, sets at
start, before it makes any value and in one statement, the first-made instant of each of
its write versions where it is empty and their last-made instant, clearing their retired
mark, then writes their last-made instant every `Hermes:Dispatch:Interval` and when it
stops, at each tick also reading the migration history and, where the database holds a
migration its release does not know, writing on the `newer-release` alert's row the
instant it holds until (MSG-OPS-006, MSG-OPS-007). A version whose check value differs
from the one recorded stops the start with `secretinvalid`, so a key changed in place
never silently breaks what was made under it. A process that meets a version in use that
it does not hold acts on nothing made under it: it holds a delivery for a key (`01`
MSG-SEND-006), answers an endpoint request 503 `messaging.request.unavailable` (`05`
MSG-API-004, MSG-API-005) and a library call `messaging.key.unavailable`, and raises
`key-unavailable` (MSG-OPS-006). A lookup by hash reads the key versions in use after
the lookup, in the same transaction, and never from a cache, so a version first used
between the two is never missed.

**In use and retired.** A version is in use from the instant values are first made under
it until the retention job marks it retired, and the versions in use that a lookup reads
are those the key table records as first made under and not retired. The job marks a
version retired once nothing needs it and values were last made under it at least
`Hermes:Secrets:RetireAfter` (default 7 days) before, so no process's write version is
ever marked, by an update conditional on its last-made instant being the one the job
read; a process writing a version's last-made instant clears the mark. Where the
database holds a migration its release does not know (MSG-OPS-002), read in the
transaction of each batch that protects values again and in the transaction that judges
a version unneeded and marks it, the retention job marks no version retired and protects
no value again, since a newer release's tables and columns may hold values under it; a
push registration, which protects a subscription's values for the person posting it, is
a new write and not this. Nothing needs a version:
- of `contentKeys`, once no row's column names it; the retention job protects again, in
  batches, every row's values under a version lower than the write version, a row's
  values together, in one update conditional on each stored value still being the one it
  read, which also sets the row's column, so a value erased or deleted meanwhile stays
  gone and no column names a version its values have left; a row holding a value the job
  cannot open is left as it is;
- of `hashKeys`, once no row's hash is under it, no delivery that is not final and holds
  an unsubscribe address was recorded before `Hermes:Secrets:RetireAfter` had passed
  since values were last made under it, and the longest token lifetime the key table
  records and `Hermes:Secrets:RetireAfter` together have passed since values were last
  made under it, so no token still valid, or in an email still to go out, names it; a
  suppression cannot be hashed again, since it holds no destination, so a version lives
  as long as a suppression under it, and an unsubscribe first writes its token's
  version's last-made instant (`05` MSG-API-005), so none is recorded under a version
  already marked;
- of `tokenKeys`, once no delivery that is not final and holds an unsubscribe address
  was recorded before `Hermes:Secrets:RetireAfter` had passed since values were last
  made under it, and the longest token lifetime the key table records and
  `Hermes:Secrets:RetireAfter` together have passed since values were last made under
  it; a pass that hands an email carrying an unsubscribe address to its provider first
  writes, on its own connection and at most once per `Hermes:Dispatch:Interval` per
  version, the current instant as the last-made instant of the token's `tokenKeys` and
  `hashKeys` versions where the one recorded is older, so a token dated from a later
  `NotBefore` (`05` MSG-API-005) keeps its versions in use for its whole lifetime; it
  does so whether or not its application holds `tokenKeys`, which a pass never needs,
  and holds nothing for the token's versions;
- of `pushVapidKeys`, once no subscription records it; a subscription recorded under a
  version other than the write version (a browser's older subscription posted again)
  writes that version's last-made instant in its own transaction, and a key posted for a
  retired version is refused (`02` MSG-PUSH-007), so no subscription lands under a
  version the job has marked.

An `smsReportKey` version is never marked, and its absence never stops a start: a report
is matched against the versions this process holds, and the deployment removes a version
once the provider no longer calls with it, the key table recording when a report last
matched each version. A deployment removes from its secret source only a version marked
retired, or such an `smsReportKey` version; any other version in use, of a key a used
part of this application needs, that the source does not answer stops the start with
`secretunavailable`, so an application that never uses a key starts without it.

A `contentKeys` version is meant for at most `Hermes:Secrets:ContentKeys:MaxValues`
values (default 2^31, under the 2^32 that random 96-bit nonces allow one AES-GCM key,
NIST SP 800-38D section 8.3). A process reserves values in blocks of
`Hermes:Secrets:ContentKeys:ReserveBlock` (default 10,000), by one atomic update of the
version's count in the key table on its own connection, before it protects under them,
so a crash loses no count; past `MaxValues`, each process whose write version it is
writes the `content-key-limit` alert's instant on its key-table timer (MSG-OPS-006), so
the alert holds while any process still writes under that version, and a reservation
that would pass 2^32 is refused until the write version moves on: a send that needed it
answers `messaging.key.unavailable`, the push subscription route 503
`messaging.request.unavailable` (`05` MSG-API-004), and the retention job leaves the
values it would protect again under their version.

*Source: M-005, M-007, M-009, M-012*

**Acceptance criteria**
1. Each secret, missing where its part is used, stops the start naming it.
2. A version of a key whose bytes changed since it was first used stops the start; a
   password changed in `emailSubmission` does not.
3. Removing a `hashKeys` version a suppression still uses stops the start.
4. With two `contentKeys` versions and no write version configured, the start stops;
   with it at 1, new values are made under version 1; after it is raised to 2, under
   version 2, and values under 1 still open.
5. A value erased while the retention job protects it again stays erased.
6. A `hashKeys` version the retention job has marked retired is no longer read as in use
   and can be removed without stopping a start, and a process that writes under it again
   clears the mark.
7. With a migration this release does not know in the history, the retention job marks
   no version retired and protects no value again.
8. Without such a migration, a row holding a value the retention job cannot open keeps
   its values and its column, and the `contentKeys` version it names is not marked
   retired.
9. With a migration this release does not know in the history, a process raises
   `newer-release` within `Hermes:Dispatch:Interval`, and the watch job clears it once
   the instant last written has passed.
10. A `tokenKeys` or `hashKeys` version is not marked retired while an email whose token
    names it waits, nor until the longest recorded `TokenLifetime` and `RetireAfter`
    have passed since that email went out, a process mapping the unsubscribe group with
    a lifetime of 5 years holding it against a retention job whose own is 2 years.
11. An application whose hosted dispatcher sends SMS, without
    `Hermes:Sms:ReportBaseAddress` and mapping no report group, starts without
    `smsReportKey`; one with `Hermes:Dispatch:Hosted` off where push is used does not
    start without `pushVapidKeys`.
