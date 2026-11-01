# Decision log

Why each choice of the specification was made and what was rejected. Supplementary: the
chapters are complete without it. An entry that changes an earlier one carries
`> **Amended.**` under the earlier entry's heading.

---

## M-001 — A product-agnostic sending library, designed for conversations

> **Amended.** M-022: `Hermes` is the final name, and no rename comes before the first
> release.

**Date:** 2026-10-21 · **Status:** accepted

**TL;DR.** One product, under the working name `Hermes`, that sends email, SMS and
browser push for any application, and is designed from the start to carry conversations
(support chat and staff chats) in a later release. It is a set of libraries an
application imports, not a service, and it knows nothing of any host.

- **Product-agnostic.** The owner's rule for every library of his: standalone and
  reusable, never shaped by one host's applications or business. Where a part runs is
  the host's choice; what the library owes every host is that its parts mount separately
  (`00` MSG-OVR-001).
- **A library, not a service.** An identity library sends its sign-in codes through this
  one; a separate service would put a network hop and a second deployment between every
  sign-in and its code, and one more thing to run. Each application keeps the library's
  tables in its own database and runs its background work in its own process.
- **The first release sends; conversations come shortly after.** The owner's priority
  (2026-10-20): email, SMS and push now; conversations planned and designed now, built
  after the first release. Every other feature (the in-application list, native push,
  WhatsApp, voice and video, Libredesk's further features) is designed for and decided
  later.
- **One product, several packages.** The owner asked for one library that also becomes
  something like Libredesk (an open-source support desk). One name, one repository, one
  specification; inside it, the channels, the engine, the bridges and conversations are
  separate packages, so an application that needs only to hand a message to a provider
  carries no engine, and one without conversations carries no chat.
- **Working name.** `Hermes`, the messenger, until the owner names it. The rename is
  made before the first release, since after it a rename of packages would be a major
  release; no type carries the name, and every name that is stored or shared by two
  releases is neutral, so the rename touches identifiers and public names only.

*Rejected:* a separate messaging service (above); one package holding everything (an
identity library would carry a support desk); using Libredesk itself (it keeps its own
user list, cannot encrypt each person's chats under their own key or erase them with the
person, has no SMS or push and no staff chats, and its AGPL-3.0 licence requires
publishing changes made to it).

---

## M-002 — The libraries the channels and templates use

**Date:** 2026-10-21 · **Status:** accepted

- **MailKit** (4.18.1, 2026-09-27, MIT, targets net10.0) for SMTP. Microsoft's own
  advice for new code is MailKit over `System.Net.Mail.SmtpClient`. It supports delivery
  status notification requests and envelope identifiers, which the later bounce feedback
  needs. The library validates the server's certificate itself through MailKit's
  validation callback, so the rule (name, chain, revocation read online where a
  certificate names a source, an optional private authority) is the library's and not a
  default that could change. The reply code of every step, an authentication failure's
  included, is readable from MailKit's exceptions, save a 530, which MailKit raises
  without its step or code and which is classed as an authentication failure; every
  other answer is classed by its code.
- **Lib.Net.Http.WebPush** (3.3.1, 2025-03-09, MIT) for Web Push. It implements RFC
  8291's `aes128gcm` and RFC 8292's `vapid` scheme, TTL, Urgency (sent only where it is
  not `normal`, the header's default) and Topic. Its newest target is net6.0, which a
  net10.0 project consumes; since its maintenance pace is slow, the library's test
  checks its encryption against RFC 8291 with a decryption of its own, because the
  client draws its key and salt inside and cannot be fed section 5's fixed values. Its
  own retry on 429 (`AutoRetryAfter`, uncapped by default) is turned off, so the wait
  stays in the dispatcher's back-off, claim and attempt count. One VAPID authentication
  and token cache per key version lets each subscription be signed with its own key. It
  reads an error answer's body whole, with no token and no bound, so the library's
  handler on its HTTP client empties a non-2xx answer's body before the client reads it.
  *Rejected:* `WebPush` (1.0.13, MPL-2.0), whose source still uses the pre-RFC `aesgcm`
  encoding and the old `WebPush` authorization scheme; writing the protocol by hand (the
  cryptographic composition is where errors hide).
- **Fluid** (Fluid.Core 2.40.0, 2026-08-20, MIT) for Liquid templates: lists for an
  order's lines, conditions, an HTML encoder, and limits on steps and recursion. Its
  limits count statements only, and its `capture` renders into a buffer of its own, so
  the library adds what they miss: its own `capture` block over a bounded buffer, a size
  bound on every buffer and filter result (computed before the call for `replace`,
  `join` and `url_encode`, whose result can outgrow their inputs), literal bounded
  ranges, an explicit list of filters, its own date and number filters in the send's
  zone and culture, and a check of each template's syntax tree at start, since Fluid
  resolves filters only when it renders. Fluid throws the same exception for both of its
  limits, so a render runs on the library's subclass of its context, which tells a limit
  from a fault by its step count. The HTML and JSON encoders allow every Unicode range,
  so Arabic text is not escaped into three times its size against a channel's limit.
  *Rejected:* plain placeholders (no lists); Razor (compiles code from templates);
  Mustache libraries (fewer safeguards against runaway templates).

- **Fluid's recursion count** is given back as each loop ends, so by the time a limit's
  exception leaves a render the count no longer shows it; since a template reads nothing
  but its own text, the depth of its loops is fixed by that text and is checked at
  start, leaving only the step count to be read at render. `replace` with an empty
  search would throw from inside Fluid, so the library answers it with its input.
- **A capture keeps its markup only with every other value encoded.** A captured value
  is already encoded HTML, and a string filter combining it with a raw value, as input
  or argument, a list included, would emit that value unencoded or escape the capture,
  so in an HTML part the library's wrappers encode every value not already encoded
  before combining and mark the result encoded.
- **What a host's data can break in a filter is a failure,** not a fault: a division by
  zero, an overflow, or an argument a filter cannot take (a `slice` length that is not a
  whole number, `round` digits out of range) refuses the send with a code, since an
  order's values can cause it; a filter missing an argument it needs is the template's
  error and stops the start. So is a member missing from data a send passes (the first
  line of an empty list), which Fluid's strict variables throw for as for a name never
  passed; the library tells the two apart by checking, before the render, that the send
  passes every top-level name the templates it renders read, which is the calling code's
  duty. A format given to `format_date` or `format_number` is a literal and a numeric
  precision is bounded, since .NET builds a string of up to a billion digits for one,
  before any of the render's own limits can see it; which filters need arguments is
  listed, since Fluid reads most missing ones as nil.
- **The two render limits are read at start only,** since the depth of nesting is
  checked against one of them then, and a lower value reloaded later would turn that
  check's guarantee into a thrown exception.
- **The three versions are pins,** since what the chapters say of these libraries (their
  exceptions, their limits, how they read an answer) was checked against those versions'
  sources; a newer version is taken by a decision that checks it again.

---

## M-003 — Bridges to the Janus identity library

*Superseded by M-020: no package of this library knows another library.*

**Date:** 2026-10-21 · **Status:** accepted

Janus, the owner's identity library, takes its mail and SMS transports from this library
(Janus's plan, Milestone 2 step 6) and keeps its own rules for when it may send. Neither
library depends on the other; two bridge packages connect them (`04` MSG-LIB-007):
- `Hermes.Janus.Channels` implements Janus's `IMailTransport` and `ISmsTransport` over
  this library's providers and nothing else, for the channels its own key names, and
  registers the shipped providers they go over, so an application that only needs
  Janus's codes to go out carries no engine, no schema and no templates of this library.
  Those messages pass none of the engine's limits and rates, which is as it should be
  (Janus keeps its own), so the provider account carries both; the allowed prefixes
  alone apply to them, since a sign-in route is where SMS pumping aims and narrowing the
  list is the first response to it;
- `Hermes.Janus` implements this library's identity, content protection, subject events
  and consent over Janus's. Its protector joins the unit of work a send runs in, so a
  person made in the same transaction already has a key, and binds the row context
  itself, since Janus's own additional data names subject, type and field only. It is
  asked only for a person; an anonymous recipient's values stay under the library's own.

Janus's consent reading for hosts comes with its round H2; the bridge's consent source
waits for it, and until then a host that declares a category requiring consent supplies
its own.

*Rejected:* Janus depending on this library (Janus is generic); this library depending
on Janus (so is it); one bridge package (it would pull the engine into every application
that only needs the transports).

---

## M-004 — What the first release is designed for but does not build

**Date:** 2026-10-21 · **Status:** accepted

Each item of `00` MSG-OVR-003 is placed in the model now so its release adds members and
packages, never breaks: conversations (`09`); voice and video; native push; Safari's
declarative Web Push; WhatsApp and other channels; the in-application list; channel
fallback; email bounce feedback; email attachments; Libredesk's further features
(customer email in the inbox, response-time targets, a help center, an AI assistant).
The owner's word (2026-10-21): none of the further features in the first release. What
keeps each additive: every public data type is an `init`-only record; `Recipient` is an
open set whose unknown case fails closed; the protector takes bytes; a push provider
names its platform, one per platform; the template key has room for pages and replies;
and a feature that needs more of a host comes as a new interface, never as a member
added to one a host implements.

---

## M-005 — How a host sends

**Date:** 2026-10-21 · **Status:** accepted

- **Declared kinds in declared categories.** A closed set lets every template be checked
  at start and gives limits, preferences and reports a stable name.
- **The host passes the destination.** It knows the person's address and number (an
  identity library answers them); the library holds push subscriptions itself, since
  only it receives them. *Rejected:* a resolver seam asked at dispatch (a second source
  of truth for contact details). Who is signed in, for the library's own endpoints,
  comes through an identity seam the host implements from its own request context, since
  the core types cannot depend on ASP.NET Core.
- **Recorded in the host's transaction, dispatched after.** An order and its
  confirmation commit together; the provider is never called inside the host's
  transaction, and the send writes nothing but its own rows, so no shared row is locked
  inside a host's transaction. The send wakes the dispatchers with `pg_notify` inside
  that transaction, which PostgreSQL delivers only on commit, since neither ADO.NET nor
  a host's transaction offers a commit callback. PostgreSQL commits notifying
  transactions one at a time across the server, so the notification can be turned off
  where that would limit a host. *Rejected:* waking in process after the call (it cannot
  know when a host's transaction commits, and other processes would wait for their
  interval).
- **Rendered at the send.** A missing value fails the call that made the mistake, and a
  later template change does not alter a recorded message. A language the deployment
  does not declare, or a time zone that does not resolve, falls back to the defaults
  rather than failing, since either can come from a person's own settings. Each declared
  language must resolve to a predefined culture, never the invariant one a slim
  container falls back to, and its direction comes from that culture.
- **Checks at dispatch, in a fixed order, against the state then.** A consent withdrawn
  or a preference turned off between the send and the dispatch stops the delivery. The
  provider's rate is checked before consent, so a delivery waiting for a free second
  does not ask the consent source again on every pass.
- **At-least-once dispatch with claims renewed before each call.** Several dispatchers
  may run. A pass claims a batch with `FOR UPDATE SKIP LOCKED` in a materialised step,
  and renews each claim, counting the attempt, just before its provider call, so a later
  delivery of a long batch whose claim lapsed is not sent by two holders, and a holder
  that keeps dying still uses up the attempts, its lost attempt recorded as `claimlost`.
  Retries back off with jitter; every delivery ends in one final status, `sent` alone
  moving once more by a report; an observer is told, in order. A dispatcher claims only
  the kinds its application declares or retires, since it cannot apply a kind's checks
  without its declaration, so every dispatching application declares or retires every
  kind, and a row of a kind no dispatcher takes shows in the lag alert rather than
  failing.
- **One clock.** Every instant the library stores or compares is its `TimeProvider`'s,
  passed to each statement, so tests can move time and the database's clock never
  disagrees with the code's; the processes' clocks must then agree within one second,
  which NTP keeps and which is below every claim's margin and every alert's and job's
  interval, and equal to the per-second rates' grain.
- **A claim carries its own expiry,** set by its holder from its own claim timeout, so
  two applications configured differently, or one reloading, never judge each other's
  live claims by the wrong timeout; the alerts processes write likewise record until
  when they hold, from the writer's own interval, and are cleared only if unchanged
  since read; an idempotency row and a limit place record when they lapse, and the key
  table the longest token lifetime any application makes or verifies tokens under (none
  recorded meaning no token was ever made), so the application that runs the retention
  job never deletes or retires by a shorter window than the writer's or the verifier's.
- **Attempts keep codes, never text.** A provider's answer can hold the destination (an
  SMTP reply naming the address, a push client's exception holding the subscription), so
  an attempt keeps a reason from a closed set and the numeric status only.
- **Cancel, idempotency, expiry.** A host may cancel what is not yet claimed. An
  idempotency key makes a retried call record once: the send serialises on an advisory
  lock of the kind and the key, looks the row up under every `hashKeys` version the
  process holds, and replaces it only past its window, since a unique violation would
  abort the host's transaction and an old row must not block a new send; it is compared
  by a keyed digest of the send, since the send itself is not kept in clear and an
  unkeyed digest of a short code would give the code up; an anonymous recipient's scope
  is its destinations' hashes. A kind's expiry stops a code that has lapsed from being
  sent, and caps a push's `TTL`.
- **Faults and failures.** A mistake only code can make (an undeclared kind, a missing
  value) is a fault; anything a host's data can cause (a long order over a render limit)
  is a failure with a code.
- **Everything configurable.** The owner's standing rule; `08` section 4 lists every
  key.

- **One reference per attempt.** A late report of a first attempt that failed at the
  network must not undo a second attempt that arrived, so each attempt carries a
  reference of its own, and a report of an earlier attempt can only mark a delivery
  delivered.
- **A missing key holds, never loses.** A process deployed without a key version, or
  without the protector another process writes with, holds what it cannot open and never
  counts it as an attempt, so the message waits for a process that can send it, with an
  alert, instead of failing after the retries.
- **Two releases side by side.** The older release leaves alone any row whose channel,
  status or reason it does not know, so a newer release can add one without the older
  one failing deliveries it has no provider for, and deletes such a row only once the
  record period has passed since the later of its recording and its due instant, so a
  newer release's delivery still waiting is not lost.
- **The digest's form is fixed byte by byte** and versioned, so a retried send matches
  across releases and two forms of the same values never conflict.
- **A claim outlives slow checks.** A pass extends its claim during a delivery's checks
  whenever half the call timeout has passed, and the limit check's locks share one call
  timeout between them, so a slow protector or consent source cannot let another process
  take the delivery over midway, start again and be taken over in turn, leaving a
  delivery that never ends, and the claim's age stays inside the margin the clocks
  leave.
- **A hold is bounded.** A delivery held for a key no process will ever hold would wait,
  and keep its content, for ever; a run of holds longer than `Dispatch:MaxHold` fails it
  instead. The run is measured from its first hold, not from the send, so a send
  scheduled far ahead is held, not failed, by a passing outage.
- **Shared locks for sends, an exclusive one for erasure.** A send for a person takes
  the person's lock shared, so sends never wait for one another, while an erasure waits
  for every send it could not see and no send commits after it unseen.
- **One row order, and retries of the library's own transactions only.** Every multi-row
  change of the library locks rows in one fixed order, so its own transactions never
  deadlock on rows; a deadlock or serialization failure that happens all the same is run
  again a configurable number of times, never on a host's transaction, which the host
  retries as it retries any.
- **A narrowed prefix list stops what is queued.** Narrowing the allowed prefixes is the
  first response to SMS pumping, so the rule is checked again at dispatch, not only at
  the send.
- **A send's locks cost room in the server's lock table,** one per key version for the
  person and one per idempotency key until the host's transaction ends, so the
  deployment duties name the bound and a large batch is split.
- **A delivery is claimed with its place in the rate.** A pass takes its deliveries'
  places in the channel's counter in the transaction that claims them, under the row's
  lock, so no pass of any process claims past the rate and a backlog over it waits
  unopened instead of being opened and held each second; a delivery an early check ends
  or holds gives its place back. A pass of a channel that took its whole limit is
  followed at the next second, and at once where its places came back, so passes overlap
  and the configured rate is reached; a batch any of which was held for a key is not
  followed at once, so a missing key never makes the dispatcher cycle its backlog. A
  pass hands back what it has not reached after one interval, before its next delivery,
  so a slow seam or a held lock never keeps essential messages out of a new pass for
  longer than that and the delivery each pass is on. The lag is measured from when a
  delivery became due, which a hold for a key does not move, over every delivery whose
  became-due instant has passed, so a held backlog shows steadily.
- **Each channel has its own passes and essential messages go first.** A pass is of one
  channel, and claims the essential categories' kinds before the rest, so a newsletter
  backlog never makes a sign-in code or a password reset expire, and one channel's slow
  provider or seam never stops another. A process runs a bounded number of passes of a
  channel at once, a new one each second while the rate has room, so passes overlap
  across seconds and reach the configured rate where the bound can carry it, while load
  never multiplies passes, connections and SMTP sessions past the bound. Notices are the
  observer job's alone, so a slow observer never slows dispatch.
- **A kind is retired, not dropped.** A retired kind's waiting deliveries end cancelled,
  so the observer learns of them and their content goes with retention; a kind simply
  dropped leaves its deliveries waiting, shown by the lag, until the retention job
  deletes them after the record period, so no content is kept for ever.
- **Format bytes kept for later releases.** A value or a token in a format a newer
  release may write waits for that release; any other unknown byte can only be damage,
  so it is refused as such. Otherwise a garbled link would answer "try again" for ever.
  A token in such a format raises no alert, since anyone can write one into the public
  unsubscribe address, while a newer release's own tokens come with deliveries whose
  holds already announce it.
- **Every update of a pass names the status it expects,** so a delivery cancelled or
  erased under a pass is never sent or moved by it; a host's seam that throws is an
  answer the pass knows (a transient consent, a hold, an ignored alert), so one failing
  seam never strands a batch until its claims lapse; each seam call a pass or job makes
  is bounded by the dispatch timeout, so a seam that hangs is answered as one that
  throws.
- **The observer is opt-in.** Notices are written only where a deployment turns them on,
  so a deployment with no observer keeps no backlog, and one whose observer keeps
  failing is told by an alert; notices older than the record's retention are dropped. A
  notice is claimed while it is delivered, as a delivery is, so two deliverers never
  deliver one at once and the order per delivery holds, which lets the job deliver
  several deliveries' notices at once; a notice the observer has taken is deleted, so
  none outlives its use holding a person's reference.
- **Every application that reads a category's rules declares, or retires, every kind,**
  whether it dispatches, runs the retention job, maps the unsubscribe or preferences
  group, or calls those services, since a public application that did not would answer a
  valid unsubscribe link as invalid.
- **Values a host passes are a closed set of cases** (text, whole and decimal numbers,
  flags, instants, lists), so the engine reads each by its case and the idempotency form
  has one encoding for each.

---

## M-006 — Preferences, consent, suppressions, quiet hours, limits

**Date:** 2026-10-21 · **Status:** accepted

- **Essential or optional categories.** What the person is waiting for (a code, an order
  update) cannot be turned off; anything else can, per channel.
- **Consent asked at dispatch.** Promotional messages need consent; the host's consent
  source answers it at the moment of sending, with the destination for a recipient
  without an account. A source that cannot answer, or does not answer in time, is
  retried like a provider.
- **Suppressions as keyed hashes.** An unsubscribe must hold for people without an
  account too, without keeping the address; RFC 8058's one-click `POST`, never a `GET`.
  Their lifetime is configuration, by default indefinite, so a deployment worried about
  recycled numbers can bound it. Turning a category back on does not lift a suppression,
  since the library holds no address to match; the host lifts it with the person's
  address.
- **Quiet hours for optional categories only.** The owner accepted them (2026-10-20): an
  offer waits until morning in the recipient's time zone; nothing the person is waiting
  for ever does. The defaults are 21:00 to 09:00, configurable; no regulator's rule was
  found that the library could cite, so the window is the deployment's choice. Quiet
  hours are checked before the consent source, and a delivery held for the night gives
  back the rate place its claim took, so the source is asked only about a message about
  to go out.
- **Limits and rates.** Per-destination limits (per person for push, which has no
  destination) and deployment-wide limits per category, taken under advisory locks in a
  fixed order as places that a message that never went out gives back; provider rates
  counted per second across the deployment in one row; allowed number prefixes, since
  SMS pumping spreads over many numbers that per-destination limits never see. A
  reported failure gives back the per-destination place only, so a forged report cannot
  hold the deployment's cap open. The host's own endpoints carry the first line; these
  are the last. A limit check bounds its lock waits by the dispatch timeout, so one
  stuck session holding a category's lock never stops that category's dispatch for
  longer.
- **Preferences are written under the person's lock taken shared,** as sends take it, so
  erasure, the only writer that must not miss one, still waits for them, while a host's
  open transaction that has sent for the person does not block a preference write, save
  behind a registration or an erasure waiting for it, each bounded by the dispatch
  timeout.

---

## M-007 — Content protection, retention, erasure and keys

**Date:** 2026-10-21 · **Status:** accepted

- **Protected at rest, bound to the row and the person.** Destinations and content are
  encrypted through a content protector the host may replace; with the Janus bridge it
  is Janus's per-person encryption, so a person's erasure makes their messages
  unreadable. Each value is bound to its table, row, field and person as associated
  data, so a value moved to another row, or a row moved to another person, does not
  open, and stored in an envelope naming its protector, so values made before a host
  changes protector stay readable; a host keeps a former protector of its own registered
  beside the new one, the last registered writing. The protector takes bytes, so
  attachments need no change.
- **Lookups by keyed hash,** with a domain per kind of value so an email's hash never
  equals a number's.
- **Retention.** Content 30 days after a delivery's final status and a record 365, both
  configurable, a delivery not yet final keeping both; idempotency rows, limit places,
  idle subscriptions and, where configured, suppressions have their own periods.
- **Erasure** cancels the person's waiting and in-flight deliveries and removes their
  data, the places that would tie a kept record to their destination included; their
  suppressions stay, since they hold no destination and keep the refusal in force. Their
  export carries each delivery's destination with its content, since both are theirs.
- **Keys rotate by version, and no process acts on a version it lacks.** Every key is a
  list of versions; new values use a write version, which must be named once a key holds
  more than one, so adding a version never makes it the one written under; a deployment
  adds a version to every process before any writes under it. A row or token names its
  version. The key table records, per version, a check value (never of a password, which
  it would expose to guessing), when values were first made under it and when last, each
  process writing these at start and on an interval, outside any host's transaction. A
  start stops where a key was changed in place or a version still in use is missing, and
  a running process that meets a version it does not hold waits instead of reading
  "nothing made under it", which would send to a suppressed address or past a limit. A
  content key is meant for at most 2^31 values, and none is made under it past 2^32, the
  bound random nonces allow.

- **No lock or identity rests on one version's hash.** During a rotation two processes
  write under different `hashKeys` versions, so a lock derived from a keyed hash is
  taken under every version the process holds, in a fixed order, and a lock that needs
  no secrecy (a report reference, an idempotency key the table already holds in clear)
  is an unkeyed SHA-256. A stored hash is looked up under every version the process
  holds, the versions in use read after the lookup and never cached, so a version
  another process has started writing under is either computed or waited for.
- **Versions are retired by the library, not by guesswork.** "In use" must be readable
  at the moment of a lookup, so the key table marks a version retired once the retention
  job finds nothing that needs it, and a deployment removes only a retired version; a
  process still writing under one clears the mark. A version is marked only once nothing
  has been made under it for `Secrets:RetireAfter`, and for the token-related keys for
  the token lifetime besides, so neither a write version nor a key a live token names is
  ever marked. A token is dated from when its email can first go out, so an email
  scheduled ahead carries a working link; its keys then stay in use while its email
  waits, and for the lifetime after it goes out, since the dispatcher marks them made
  when it sends it. An unsubscribe marks its token's hash key made before it records a
  suppression under it, so a token lifetime raised after a version was retired never
  leaves a suppression under a version the deployment may remove. A write version never
  moves back, since values made under the higher one would then never be protected again
  and that version could never be retired; a version made wrongly is replaced by a newer
  one. A browser's old push key is refused once retired, so its page subscribes again
  under the current one.
- **Content-key counts are reserved in blocks** before use, on the library's own
  connection, so a crash loses none and no host transaction touches the key table; every
  process that registers the engine keeps it current on its own timer, whether or not it
  runs jobs. A process checks at start only the versions of the keys its own parts need,
  so an application that never uses push or unsubscribe tokens starts without those keys
  while another application of the deployment uses them; whether an application records
  sends is its own setting, so one that only answers the unsubscribe and preferences
  pages, often the most exposed, holds no key that opens content. The alert that a
  content key nears its limit is written by the processes still writing under that
  version, so it clears once none does, whatever each process's write version. A record
  keeps its person reference as long as the record, so a later erasure still finds it
  and the places that link it; an export holds every record that names the person and
  their live idempotency keys, since both are data held about them. Keeping write
  versions from moving back is a deployment duty, a rollback included, rather than a
  start check, since such a check would refuse the processes still starting at the old
  write version while a rotation rolls out.
- **A value that does not open under its binding** can only come from a change made to
  the database outside the library: its delivery fails and an alert is raised.
- **Erasure is all or nothing,** and the observer still learns how the person's
  deliveries ended, without their reference. It is serialised with a dispatcher's limit
  check by the delivery's row, which the check locks after its own advisory locks, and
  with the person's own writes by the person's lock, so neither deadlocks with the other
  and no place or subscription is left behind for the erased person. Its wait for a
  host's open transaction is bounded by the dispatch timeout, so a host's mistake fails
  the call instead of hanging it.
- **An older release does not judge what it cannot see.** Where the database holds a
  newer release's migration, a process marks no key retired and refuses erasure and
  export, since a newer release's tables may hold values under a key, or of the person,
  that it knows nothing of; the caller retries on a process of the newest release. A
  rollback past a migration therefore suspends erasure and key retirement until the
  newer release runs again, so the deployment duties say so, an alert shows it, and a
  deployment rolls forward. For the same reason an older release's retention job
  protects no value again, since a row may hold a newer column it would leave under the
  old key while marking the row moved.
- **The protected value's context is a fixed layout,** its table and field names neutral
  and listed, since every stored value is bound to it and two releases must agree.

---

## M-008 — SMS through WE

> **Amended.** M-022: the library ships no SMS provider. WE is the gateway one
> deployment contracted, and its adapter is that host's, behind `ISmsProvider`; what
> this entry decides for reports, the balance and segments holds for every provider.

**Date:** 2026-10-21 · **Status:** accepted, one input open

WE (Telecom Egypt) is the contracted SMS provider (the owner, 2026-10-20). What its
documentation states, as recorded when Janus's abuse controls were decided: Basic
authentication over the static string `username:password:accountid`; a balance endpoint;
a delivery report sent to the receiver as a plain-HTTP GET with `messageId` and `status`
in the query string, unauthenticated. WE publishes no API documentation (its business
page mentions API integration only, checked 2026-10-21), so the rest comes from the
document the owner holds. **Open:** that document, needed before the WE adapter is built
(`02` MSG-SMS-003 lists what is taken from it), and the answer it gives on report
addresses, which decides how the bridge routes reports when an identity library sends
through the same account.

- **Reports are hostile.** The route carries a secret key, since WE signs nothing and
  its message identifier's form is unknown; only requests with a wrong key are
  rate-limited, so the provider's own reports are never turned away. The report contract
  takes the whole request, so a provider that posts a body needs no new contract. A
  report and the dispatcher's record of the same message take one advisory lock, so
  neither misses the other; a report that beats the record is kept and applied; a report
  of delivery for a message whose answer timed out ends it delivered, so the person gets
  no second code, and a kept report of delivery wins over the end a later check would
  give, since the message did arrive. Both sides lock the delivery's row before reading
  it, so a report of an earlier attempt and the update of a later one cannot miss each
  other; of two reports of one attempt, a `delivered` is never replaced, so a forged
  failure cannot cause a second code. An SMS renewal locks the row too and reads the
  kept reports after, so a report kept a moment before its call is never missed. A held
  report that lapses is not counted as rejected, since a genuine report of a timed-out
  attempt lapses so where the provider echoes no reference of ours. Only reports that
  cannot be genuine are counted as rejected, so the alert keeps its meaning on ordinary
  traffic. The key travels in the path, so the deployment redacts that path in its
  request logs. The engine fills the report address on each message wherever its base
  address is set, so a host's own provider that takes one with each message gets it as
  the shipped adapter does; whether the shipped adapter needs it is the answer WE's
  document gives.
- **The balance** is read on a schedule in the provider's own unit (WE sells bundles); a
  drain is judged only after enough history and above a minimum spend, so the first
  day's first message raises nothing; a provider without a balance turns the floor off.
  Only an application that dispatches and uses SMS reads it, so an application without
  the credential never answers the poll with a failure or a stale reading.
- **Segments** are counted by packing: GSM 7-bit (3GPP TS 23.038 section 6.2.1, an
  extension character taking two septets, section 6.2.1.1) or UCS-2; 160 septets or 70
  code units in one segment, 153 or 67 in each of several (3GPP TS 23.040, the
  concatenation header), never splitting an escape or a surrogate pair. Whether WE takes
  UCS-2 text under a registered sender name is one of the answers its document gives.
  The counter is public, so a host that hands a message to a provider itself counts as
  the engine does.

- **Counting rejected reports** uses a counter row per minute, so the alert's window is
  exact across processes, and a report held past its time is dropped and logged,
  uncounted, by the watch job when it lapses, not by a daily job. Held reports are
  capped across the deployment, under one lock, since anyone holding the key could
  otherwise write them without bound.

---

## M-009 — Browser push

**Date:** 2026-10-21 · **Status:** accepted

The hosts in view are websites or installed web apps for now (the owner, 2026-10-20), so
push is Web Push: no Apple or Google account, supported by Chrome, Edge, Firefox and
Safari, on iOS and iPadOS 16.4 and later for web apps added to the home screen. A push
service answers 404 or 410 for a gone subscription, which is removed; a 201 means
accepted, never shown. Safari revokes push permission from a site whose push shows no
notification, so every push shows one. Subscriptions are accepted only for known push
services, by DNS name, a wildcard covering at least three labels after its `*.`, since
otherwise a browser could make the server send requests to any address. A subscription
past the expiry its browser gave is removed, since the browser has already let it go.

- **VAPID keys by version, taken from the browser.** A push service ties a subscription
  to the public key it was made with: Mozilla's answers 404, as for a gone subscription,
  to a request signed with another, and FCM answers 400. So each subscription records
  the version whose public key the browser actually subscribed with, which the page
  posts beside the subscription (the subscription's own JSON leaves it out), and its
  requests are signed with that version; a 400 counts towards the authorization alert.
  Otherwise a rotation, a page that cached the old key, or a process with another write
  version would delete every Firefox subscription in silence.
- **Tokens** expire 12 hours after they are made and are reused for one hour, since
  Apple asks for no more than one new token an hour.
- **The push service** is read from the endpoint's host, not the browser's `User-Agent`,
  which names browsers ambiguously.
- **Retries** go only to subscriptions still the person's, so a browser that changed
  hands between attempts, or during one, never receives the earlier person's message.
  Whether a push delivery was accepted is judged over all of its calls, so a message one
  of its subscriptions accepted before its holder stopped ends `sent`, never `failed`,
  and a subscription that refused for good is never tried again for it. The push
  subscription group needs the VAPID private keys, since it records the held version a
  posted key belongs to; a deployment that wants no private key in its public
  application maps the group in the dispatching one. A registration whose protector
  fails or times out is answered 503 and raises `key-unavailable`, as a held delivery
  does, so a failing protector is seen from the route too.
- **The authorization alert counts answers,** per push service within a window, since
  the key a page posts cannot be checked: one subscription made under another key is
  refused on every push, and would otherwise hold the deployment-wide alert raised for
  months and hide a broken key; the watch job clears it once the window is quiet.

Native push is a later provider of the same contract, one provider per platform.

---

## M-010 — Email

**Date:** 2026-10-21 · **Status:** accepted

SMTP submission with implicit TLS on 465 by default (RFC 8314's preference), STARTTLS on
587 configurable, the certificate always validated, never validation off. A certificate
that names a revocation source has its status read there, and one that cannot be read
fails; one that names none is accepted only as a short-lived leaf (the CA/Browser
Forum's short-lived certificates, at most 7 days from 15 March 2026, carry no revocation
source, ballot SC-063) or under a private authority the deployment says publishes none,
so a private CA without a revocation service and the new short-lived certificates both
work without weakening the rule for the rest. A server that offers no authentication the
client can use, or that refuses the session itself, raises the same alert as a refused
password. Any SMTP submission server, by configuration. One provider session per
dispatcher pass, so a pass authenticates once. One-click unsubscribe (RFC 8058) and a
visible link on optional categories, as Gmail and Yahoo require of bulk senders; for
every sender, SPF or DKIM and forward and reverse DNS; for a bulk sender, SPF and DKIM,
DMARC and a From domain aligned with one of them, so every From domain a kind declares
must be one the submission server signs for. The first release reports `sent` on the
server's acceptance; bounce feedback is designed for, through delivery status
notifications carrying an envelope identifier (so the envelope sender stays the From
address the server allows), suppressing a dead address for every category. The
unsubscribe address is made once, at the send, and kept protected with the delivery, so
the header and the body carry the same address and the token never lies in clear.

---

## M-011 — Conversations

**Date:** 2026-10-21 · **Status:** accepted for the design; built in a later release

The owner's requirements (2026-10-20): a support chat queued to the first available
staff member within set hours, persistent and encrypted, a transcript offered by
download or email, a survey; a company-wide chat, team chats and private chats;
attachments; automatic replies, configurable; voice and video designed for. Libredesk is
the model for the support desk. `09` fixes the model.

- **Encryption at rest, not end to end,** under the key of the person a conversation
  concerns, because staff must read, hand over, search and send support conversations.
  Search over per-person encrypted messages is a known hard problem, left to a later
  seam.
- **A visitor** is anonymous to sending and holds no push subscription; `Recipient`
  stays an open set so a later case breaks nothing.
- **Fan-out across processes** through PostgreSQL's `NOTIFY`, since a message broker
  would be a service of the library's own.
- **Automatic replies** are rules with a template text per language.
- **A privacy notice** only for a visitor without an account (the owner, 2026-10-20); a
  person with an account accepted the host's notice at sign-up.

---

## M-012 — How the library is built

> **Amended.** M-027: release candidates of 1.0.0 until the owner calls a release final;
> the compatibility rule binds from the first final release.
>
> **Amended.** M-014: the implementer pushes its own work, after the secret scan, and a
> phase reaches `main` by its reviewed pull request.
>
> **Amended.** M-015: the packages depend on one another at the exact version packed;
> MSBuild's own warnings fail the build; the scan takes no allow-list but gitleaks'
> default and its one entry form; a test of no kind fails the gate.

**Date:** 2026-10-21 · **Status:** accepted

The same discipline as the owner's other libraries: .NET 10, warnings as errors, tracked
public surface, results for expected outcomes, sealed types, source-generated JSON,
configuration binding and logging, tests first with hand-written fakes and real
PostgreSQL, Conventional Commits, Keep a Changelog, MinVer, gitleaks over the whole
history, and a sample host in the gate.

- **No type named after the library,** the rule of the owner's identity library: the
  name sits in namespaces, package identifiers, the configuration section, the
  registration and mapping methods and a few public names, so a rename touches no type.
- **Migrations by tool, never at start.** A release's migrations are applied by its own
  `hermes migrate` before its version starts, each leaving the schema usable by the
  release before it, so two releases run side by side during a deployment.
- **Alerts** through a seam, raised when a condition starts, repeated at an interval and
  cleared once, with their state in the library's tables so several processes raise one.
  A reloaded setting outside its rule is ignored with an alert, so a typo in a live file
  never stops dispatch.
- **Semantic Versioning** over the public surface, the endpoints and their wire shapes,
  the codes, the configuration keys, the enumerations and statuses and the metric names,
  and over the interfaces hosts implement, to which a member without a default
  implementation is never added in a minor release; the schema is the library's own and
  private, so it is outside the rule, a destructive migration being named in the release
  notes.
- **Gates run locally too,** as scripts, so the implementer runs a phase's full gate
  before it pushes the phase's work. A rule no script can decide (a commit body's need,
  the imperative mood, tests written first) is marked "checked in review", never claimed
  as checked by a gate. The scripts are PowerShell 7, which runs alike on the owner's
  machine and on GitHub's Ubuntu runner.

- **The repository's files have fixed places,** the pipeline's definition, the gates'
  scripts, the sample host's package folder and the list of criteria proven so far
  included, so phase 0 creates nothing the conventions do not place; the secret scanner
  is pinned to a version and checked by its published digest, since a scanner fetched
  unchecked would run with the repository's rights.
- **Analyser rules turned off are named, each with its reason,** where the spec's own
  names and shapes would break them (identifiers in test names, public test classes,
  nested case records, classes the container makes, an alert sink's awaited calls, a
  startup exception that always carries a code, a test's documentation and
  `ConfigureAwait`, which xUnit.net's own analyser refuses, and the literal column
  arrays of EF Core's generated migrations, whose main file carries no generated-code
  mark); every other rule of `latest-all` stays, and every assembly declares itself not
  CLS-compliant once, in `Directory.Build.props`.
- **One required check.** The gates run in one pipeline job, which branch protection
  requires, so a gate a later phase adds needs no change to the protection and no
  required check waits for a gate that does not exist yet; the test-name gate reads the
  criteria proven so far, since an item can be built across phases. The docs reach the
  empty default branch by one direct push, before the protection exists, since there is
  no branch to open a pull request against; a release tag runs the gates too, and its
  publish waits for them.
- **The packages are released together** and depend on one another at exactly the same
  version, so the rules both the channels and the engine need live once, in
  `Hermes.Channels`, and are granted to the packages that use them, the test package
  included, save the segment count, which is public in `Hermes.Core` (M-008); the
  migrations live in `Hermes` and are granted to the command that applies them, and the
  engine's hosted services and migrations to the test package that removes, or keeps
  from being registered, the one and applies the other, since no public type may carry
  them and reflection is ruled out.
- **The sample host alone restores without a lockfile,** since it builds from packages
  packed at the same commit, whose version and hashes change with every commit.
- **An application that only records sends holds no provider secret,** since the shipped
  providers are used only where an application dispatches or runs the identity library's
  transports over them, each transport for a channel the bridge's own key names, not for
  every channel configured, since the engine's kinds may use a channel the identity
  library does not; a public application that records sends and runs no such transport
  then needs neither the submission password, the SMS credential nor the VAPID keys,
  unless it maps the push subscription group, and a host whose identity library sends by
  email alone needs no SMS account. Whether an application dispatches is one key and
  whether the library runs its passes another, so a host that drives its passes itself
  keeps the shipped providers, the observer, the balance and the keys its passes need,
  while a recording application holds none of them and may not run a pass. A balance is
  read where the application dispatches and SMS is used, whether or not that application
  runs the other jobs.
- **A stopped pass hands back at once each delivery whose renewal it has not begun,**
  one waiting on a seam included, so a rolling deployment does not leave a claimed batch
  waiting for its claims to expire, then lets the provider calls it has begun, and the
  call of a renewal under way, finish within one call timeout from the stop, cancelling
  one still running then, so an answer that comes with time left to record it is
  recorded rather than lost, and begins no other; the host's shutdown timeout is set
  above the call timeout for this, so the call timeout's default, 20 seconds, sits under
  .NET's and the common orchestrators' 30. A seam call that ends after the hand-back
  changes nothing, since its delivery is no longer the pass's; a cancelled
  `RunPassAsync` then throws, as any .NET call whose token is cancelled does. A lost
  attempt is known by its missing answer, not by the status its delivery had, so a
  delivery handed back after it was taken from a stopped holder still records its lost
  attempt.
- **The formatting file is the SDK's own,** unchanged but for the analyser turn-offs,
  and the pipeline's actions are the two it cannot do without, pinned by commit, so
  neither is a choice the implementer makes; a generated migration's main file, which
  carries no generated-code mark, is formatted in the commit that adds it, so the format
  gate holds without a hand edit of its own.
- **A job renews its claim after each unit of its work,** a batch, a notice or a
  condition, and is judged stalled only when it neither completes nor renews, so a long
  but moving run is never reported stalled or taken over; the retention job skips rows a
  host's open transaction holds, so a host's transaction never stalls it. A job's run
  starts only once its interval, less one second for a timer's delays, has passed since
  the last start, so several processes together run it once per interval, not once each.
- **The packages carry only what the owner's repository gives them,** the owner's name,
  the repository's address and each package's description, and the command tool's own
  two properties, so no property is left to a default the build would invent.
- **The log gate is the integration tests' own assertion,** each test checking its
  captured lines against the destinations, content and references it used, since only
  the test knows which strings are personal.
- **The engine's settings bind only the engine,** so an application of the providers or
  the bridge alone needs no languages, templates or time zone data; the call timeout is
  the providers' too.
- **The library's HTTP clients carry its handlers alone,** since a host's defaults for
  every client (a retrying resilience handler, as common service templates add) would
  otherwise post an SMS again within one call, behind the dispatcher's own accounting,
  and the factory's own request logging would write every push endpoint into the host's
  log; a host's outbound tracing, beyond the library's reach, records each request's
  path too, so the deployment redacts it.
- **The startup checks run before anything else of the host,** in the hosted lifecycle's
  first step, so a host's own service that sends at its start never meets a library
  whose keys and schema are unchecked.
- **A transaction of the library cannot outlive a stalled holder,** since the server
  ends a session idle inside one after twice the call timeout (four times for a push
  registration, whose three protector calls run between two statements; the export opens
  its values after its transaction, so a person with many values is no exception; the
  migrations' transactions alone setting none, since their tool reads no call timeout),
  and a pass's row waits are bounded, a stopping pass's within one call timeout from the
  stop, so a frozen process never holds a limit's lock or a delivery's row for as long
  as its connection lingers.
- **The release notes are a file of their own,** one section per release, holding the
  deployment's duties and the release's destructive migrations, since the changelog's
  sections are fixed by its format and neither is a change of the code. The duties name
  only what a deployment can meet: a job runs somewhere where it has work, and the
  dispatch interval's floor stays above the clocks' agreement.
- **Package sources are mapped,** each identifier to the one source it may come from, so
  a package published elsewhere under one of the library's or Janus's identifiers is
  never taken by an unlocked restore.
- **The release gate's baseline is a committed file** written with each release, beside
  the shipped public surface, so the gate compares against what was released, not
  against a rebuild; additions are minor, changes and removals major.
- **Records holding destinations, content or secrets print none of them,** since a
  record's generated text names every member and a host's own logging may format one.
- **The enumerations' C# names are fixed beside their wire spellings,** since the
  members are shipped surface and no single naming policy gives the wire's forms.
- **The folders inside a project are named,** one per chapter for the engine and one per
  channel for the providers, so the first migration's folder and the rest are no choice
  of the implementer's; public types sit at the package's root, so a folder never
  changes a namespace a host writes.
- **Configuration is checked on the raw values,** before binding, so a refusal names the
  key as `08` spells it; the generated validator stays as a second line. Rules the .NET
  analysers cannot check (sealed public types, a token on every asynchronous method) are
  a test over the built assemblies' metadata.

---

## M-013 — Data access, hosting abstractions and test packages

> **Amended.** M-015: the test vectors' entry names a file under `tests/`, and the scan
> takes no other allow-list but gitleaks' default.

**Date:** 2026-10-21 · **Status:** accepted

- **PostgreSQL 16 or later,** the integration tests running on that floor's pinned
  image, so no statement the library writes needs a later version.
- **PostgreSQL through EF Core and Npgsql.** The hosts in view run PostgreSQL, and a
  send joins the host's own transaction, so the library uses the host's database through
  the same provider; hand-written SQL where EF Core cannot express a statement (claims,
  advisory locks, `pg_notify`). The schema is fixed at `messaging`, a neutral name that
  never changes with the library's, with its own history table, since a run-time schema
  name would need EF Core's internal services. A closed set is a `text` column with a
  `CHECK`, since a PostgreSQL enumeration would need a mapping on the host's data
  source. The dispatcher's `LISTEN` needs a session connection, so a deployment whose
  connection passes through a transaction pooler gives a second one for it. *Rejected:*
  a database of the library's own (it could not commit with the host's write); a
  configurable schema (above).
- **The Microsoft.Extensions abstractions** (dependency injection, configuration and its
  binder, options, logging, the HTTP client factory) in `Hermes.Channels`, which needs
  registration, validation, logging and named HTTP clients without ASP.NET Core.
- **Build and test packages.** `Microsoft.CodeAnalysis.PublicApiAnalyzers` tracks the
  public surface; `MinVer` versions from tags; `xunit.v3` on Microsoft.Testing.Platform;
  `Testcontainers.PostgreSql` for real databases; `Microsoft.AspNetCore.Mvc.Testing` for
  endpoint tests. SMTP is tested against a scripted listener in the test process, over
  TLS from a test authority that serves its own revocation list, which can give every
  reply code a test needs, which no SMTP server in a container can. *Rejected:* a
  mocking package (fakes say what a seam must do, mocks only what a test expects); an
  SMTP server image (it cannot be made to answer each code, and its certificate would
  need the same test authority).

- **`Microsoft.Extensions.Hosting.Abstractions`** in `Hermes.Channels` as well, since
  its startup checks read secrets asynchronously and so run as a hosted service.
- **`dotnet-ef`**, EF Core's own tool, as a local tool at the EF Core version, since the
  migrations are generated by it and a tool installed globally would differ from machine
  to machine.
- **A test database is migrated through `Hermes.Testing`,** since the sample host's
  tests may use only the packed packages and the engine refuses a database without the
  release's migrations; the member applies them as the command does.
- **Published test vectors are the one allow-list entry the scanner takes,** one file
  and one exact value each, since RFC 8291's receiver key must be held as a literal for
  the encryption to be checked against the standard; every other test secret is made at
  run time, so a credential-shaped string never enters the history otherwise.

---

## M-014 — The implementer pushes its own work and takes the owner's documents itself

**Date:** 2026-10-22 · **Status:** accepted

The owner's word (2026-10-22): the implementer creates the repository, takes each update
of the documents itself and pushes its own work, so the owner's part is the messages,
the review, and the few things only the owner holds (the package feed, WE's credential).

- **The repository** is created by the implementer in phase 0, with the GitHub access of
  the machine it runs on: private, default branch `main`, dependency vulnerability
  alerting and secret scanning on, squash merges off; it pushes the first `docs:` commit
  to the empty `main` and then protects `main` (the `gates` check required, force pushes
  and deletion refused). A setting the account does not allow is reported and never
  worked around, and the build goes on: the rules below bind the implementer whether or
  not the platform enforces them. The first commit is scanned before its push with the
  scanner and configuration the gates will use.
- **An update of the documents** arrives as an archive in the owner's downloads folder,
  named in the owner's message with each file's `git hash-object`; the implementer
  unpacks it apart, checks every hash and only then commits the documents in a `docs:`
  commit of their own. It still never edits them.
- **Pushes.** At the end of a phase, after the full-history secret scan on the report's
  own commit, the implementer pushes its working branch, opens a pull request into
  `main` and leaves it unmerged; it merges it, with a merge commit and its `gates` check
  green, only when the owner's message says the review passed. It never pushes to `main`
  after the first commit, never pushes by force and never rewrites a pushed commit, so a
  commit reaches GitHub only after the scan, and `main` only after the review.
- **The run ends with the pull request,** so each phase starts from a reviewed `main`
  and no work is built on a phase the review may still change; a review that does not
  pass, a failed pipeline run or an answered question continues the phase on the same
  branch, with new commits and a new report commit on top.
- **What the implementer writes on GitHub keeps the no-trace rule:** the branch's name,
  the pull request's text and the merge commit's message enter history, so they are
  checked as a commit is, and the implementer acts under the owner's own account only.
- **The release tag** is pushed by the implementer on the merged release commit when the
  owner's message says to release, the one tag it pushes.

*Rejected:* the owner pushing each ready commit (the checks that matter, the scan before
a push and the review before a merge, both stay, so the owner's step added no check);
going on into the next phase before the review (work built on an unreviewed phase is
redone when the review changes it).

---

## M-015 — Phase 0's question and review: exact versions, and the gates' gaps

**Date:** 2026-10-26 · **Status:** accepted

Phase 0 asked one question. Its review before the merge found places where a gate let
through what its chapter forbids; the ones below needed a rule, and the rest are defects
of the build against chapters that already state the rule, fixed in the same phase.

- **The packages depend on one another at the exact version packed (`[v]`)** (question 1
  of phase 0). The packages grant one another their internals (`07` MSG-ENG-001), which
  the release gate does not guard (`04` MSG-LIB-006), so a host holding two of them at
  different versions could run one against internals the other's release changed and
  fail at run time; with an exact range, NuGet reports the mix at the host's restore
  instead (NU1605 where a direct reference takes a lower version, NU1608 a higher one,
  NU1107 where two exact ranges meet). `dotnet pack` writes a project reference as that
  version or later, and the .NET 10 SDK has no setting for an exact one, so
  `Directory.Build.props` holds one target, run after NuGet's
  `_GetProjectReferenceVersions`, that sets the `ProjectVersion` of each of NuGet's
  `_ProjectReferencesWithVersions` items to `[v]` (read in the SDK's
  `NuGet.Build.Tasks.Pack.targets`, and tried: the packed dependency reads
  `version="[1.2.3-alpha.0.4]"`). Both names belong to NuGet's pack targets and are not
  a documented setting, so a test packs the packages and fails on a dependency written
  another way (`04` MSG-LIB-001 criterion 3), which catches an SDK that changes them.
  *Rejected:* the minimum version pack writes (a later version of the other package
  taken unreported); a `.nuspec` file per package (every dependency, the third-party
  ones too, kept by hand beside the project's references, a second source of truth).
- **MSBuild's and the SDK's own warnings fail the build.** `TreatWarningsAsErrors` makes
  the compiler's, the analysers' and NuGet's warnings errors, but a warning MSBuild or
  the SDK raises stays a warning (a probe build with a `Warning` task passes with it
  alone and fails with `MSBuildTreatWarningsAsErrors`), so both are set (`07`
  MSG-ENG-002). Since only the root `.editorconfig` turns rules off, a project file or
  shared build file that turns a warning off or keeps it a warning (by a property, an
  item's metadata or an item), and any other `.editorconfig` or a `.globalconfig`, fail
  the settings test.
- **The configuration's one entry form is the only allow-list beside gitleaks'
  default.** gitleaks also skips a line that carries its inline `gitleaks:allow` marker,
  and the findings a `.gitleaksignore` file lists, which it reads from the working
  directory and from the scanned repository's root whatever its configuration says
  (`cmd/root.go` of 8.30.1). The scan therefore runs with `--ignore-gitleaks-allow`,
  with an ignore path holding no file and with no `--baseline-path`, and fails where a
  `.gitleaksignore` is tracked or sits at the repository's root, so a credential-shaped
  string on a marked line, or one a `.gitleaksignore` lists wherever that file is, fails
  the gate's script (`07` MSG-ENG-001, `10` MSG-THR-004), as M-013's one entry form
  meant. The entry's file is one of the tests'.
- **Every test runs.** The tests gate runs each kind by its trait, so a test carrying no
  `Category` of the three would run in none; such a test of `tests/`, or one carrying
  more than one, fails the gate (`07` MSG-ENG-008 criterion 3). A test that runs the
  SDK's build or pack, writing under `tmp/`, is an `integration` test, as one that runs
  `git` or the secret scanner is.
- **`PublicAPI.Shipped.txt` and the release baseline change only in a release commit,**
  which the commit-message gate now checks (`07` MSG-ENG-011 criterion 2); a file that
  moves unchanged with its project, as at the rename of phase 8, is no change, and the
  release commit's exemption comes with the release step.
- **"Lower case"** in a commit's description means it begins with a lower-case letter,
  and a name it mentions keeps its own spelling, since a description often names a type
  or a protocol (`07` MSG-ENG-011). The phase's report had settled it as no capital
  anywhere, which is a second reading, so a question, not a rule it could apply alone.
- **Where the scan runs.** Before every push, by the implementer, and in the pipeline on
  every pull request, push to the default branch and release tag, a finding stopping the
  push it comes before (`10` MSG-THR-004, as `07` MSG-ENG-012 lists). *Rejected:* a
  pipeline run on every push of every branch (a working branch's pull request is opened
  with its first push, and runs the gates on each push after it).
- **The repository's tests reference every shipped project,** since their contract tests
  read the built assemblies (`07` MSG-ENG-004); `04` MSG-LIB-001's table gains their
  row.
- **Checked in review:** the release a package is taken at when added (`07`
  MSG-ENG-009), since only that day's listing shows it.
- **The sample host's row of `04` MSG-LIB-001** is built with the sample host, in phase
  6 (plan).

---

## M-016 — Phase 1's questions: lengths, the template folder, decimal factors, roles

**Date:** 2026-10-26 · **Status:** accepted

Phase 1 asked four questions, each Tier 2. Its review found nothing else that needed a
rule.

- **A length in characters counts Unicode scalar values** (question 1). A label (`01`
  MSG-SEND-002), a person reference and a `PushTopic` (MSG-SEND-003) and
  `Email:From:DisplayName` (`08` section 4) hold at most 128 characters. Counted in
  UTF-16 code units, a label of Arabic text could hold 128 letters and one of characters
  outside the Basic Multilingual Plane only 64; counted in grapheme clusters, the count
  turns on the Unicode version of the runtime's segmentation, so a value one release
  accepts the next could refuse. Scalar values (code points other than surrogates) are
  fixed by the text alone, and are what the identity library counts a name in. `00`
  section 1.5 states the unit once, for every passage that names no other (printable
  ASCII characters, an SMS text's encoding units, an email address in the ASCII form
  that is sent); a string holding a surrogate that is not part of a pair is not text,
  and fails the check of its length. *Rejected:* UTF-16 code units (a limit that differs
  by script); grapheme clusters (a limit that moves with the runtime).
- **A template folder that does not exist holds no template** (question 2). Templates
  come from a host's source, the folder and the assemblies a host passes, in that order
  (`03` MSG-TPL-006), and each part a declared language requires is checked at startup
  wherever it comes from (MSG-TPL-004), so a missing folder cannot let a kind go without
  its template; a host whose templates all come from its source or its assemblies needs
  no folder. A path naming a file, or a folder the process cannot read, is outside the
  rule of `Templates:Path` and stops the start (`08` section 4). *Rejected:* the folder
  required (an empty folder kept only to pass a check).
- **The factors and the amounts are decimal numbers** (question 3). `Retry:Factor`
  multiplies a delay and `Jobs:StallFactor` an interval, so 1.5 means as much there as
  it does for `Sms:DrainFactor`; `Sms:BalanceFloor` and `Sms:DrainMinSpend` are compared
  with a balance the provider gives as a `decimal` (`04` MSG-LIB-002). `08` section 4
  now says that a number in a rule is whole unless the rule says a decimal number, read
  in the invariant culture, and names each decimal one.
- **`hermes migrate` checks the roles before it applies anything, and the grants after
  it makes them** (question 4). Once connected, the tool reads which of
  `applicationRoles` the database has, comparing each name as text, and answers a role
  it lacks `messaging.migrate.inputinvalid`, `details.member` `applicationRoles`, before
  any migration applies: the fault is in the input, and is found before the schema
  changes. The comparison as text also refuses a name longer than PostgreSQL's 63-byte
  limit on identifiers, which a `GRANT` cuts short, and so would give to the role named
  by its first 63 bytes. A `GRANT` made by a role that holds a privilege on a table
  without the right to grant it warns and grants nothing (PostgreSQL's `GRANT`, Notes),
  so the tool checks in the grants' transaction that each role holds each privilege, and
  a grant that fails or is not found answers the new code
  `messaging.migrate.grantfailed`, `details.role` the role, no grant of the run staying.
  The grants also take only what the application needs: `SELECT` alone on the migration
  history, which it reads and never writes, and `USAGE` on the schema's sequences, which
  a table's grant does not cover (`06` MSG-OPS-002 criteria 3 to 5; `08` section 1).
  *Rejected:* a new code for a role the database lacks, the migrations staying applied
  (an input fault found only after the schema changed); the database's error let through
  (the tool would then write no JSON line, against MSG-OPS-002).

---

## M-017 — Phase 2's question: MailKit 4.18.2

**Date:** 2026-10-26 · **Status:** accepted

Phase 2 asked one question, Tier 2.

- **MailKit moves to 4.18.2** (question 1). MailKit 4.18.1 brings MimeKit 4.18.1, which
  resolves System.Security.Cryptography.Pkcs 10.0.0, and the restore's audit reports two
  advisories against it (GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7), fixed in 10.0.13, so
  the build fails as `07` MSG-ENG-010 means it to. MailKit 4.18.2 (2026-10-23) depends
  on MimeKit 4.18.2 and System.Formats.Asn1 10.0.13 or later, and MimeKit 4.18.2
  (2026-10-21) on System.Security.Cryptography.Pkcs 10.0.13 or later; their release
  notes on nuget.org give that bump as each release's only change, so the chapters'
  claims, checked against 4.18.1's sources, still hold, and the SMTP provider's tests,
  built against 4.18.2, check them again (`07` MSG-ENG-009). *Rejected:* Pkcs 10.0.13
  pinned centrally (it mends this repository's restore alone: a host restoring
  `Hermes.Channels` at MailKit 4.18.1 still resolves 10.0.0 unless the package carries
  Pkcs as a dependency of its own, a package outside MSG-ENG-009's table and a second
  fix beside the one its authors made).

---

## M-018 — The full gate runs in the pipeline, not on the implementer's machine

**Date:** 2026-10-27 · **Status:** accepted

Every phase ran the full gate twice: once on the implementer's machine before the
report, and again in the pipeline on the push, which runs every script of `07`
MSG-ENG-012 and blocks the merge (the `gates` check is required). The local run is
dropped. The fast checks stay on every commit, narrowed to the unit tests of the
projects a commit changes; before every push the implementer runs every unit test, the
commit-message gate and the full-history secret scan, the two gates whose failure after
a push could only be mended by rewriting what was pushed. The pipeline's `gates` run on
the last commit pushed is the gate of record: after every push the implementer waits for
it in the same run, fixes a failure in new commits and pushes again, and a test a
failing run names may be run locally to find its cause. The pipeline's runner is the
Linux the deployment runs on, and twice on 2026-10-26 it failed a test the local run had
passed. MSG-ENG-012's rule that each gate is a script the implementer can run locally
with the same result stands. *Rejected:* keeping the local full gate (a second run of
the same scripts, on another platform, in every phase); moving the commit-message gate
and the secret scan to the pipeline as well (a failure after a push could only be mended
by rewriting a pushed commit).

---

## M-019 — Phase 5's report: the endpoint contract placed, the sample host first, the gates paragraph brought to M-018

**Date:** 2026-10-27 · **Status:** accepted · **Follows from:** M-018

Phase 5's report found MSG-ENG-006 in no phase: phase 0 took all of `07` before any
endpoint existed, and no later phase names it. It is built in a new phase 5a, beside the
sample host, now that every endpoint group exists (phase 4) and the release gate reads
the serializer contexts its criterion names (phase 5). The sample host moves to phase 5a
from phase 6: it sends through `Hermes.Testing`'s in-memory providers and needs nothing
of Janus, while phase 6's bridges wait on Janus's package and the reference of its API
(the plan's "Waiting on inputs"), so building it now keeps the work moving. Phase 6
keeps the bridges alone, and the sample host must still build and pass once they are in.
Phase 5a works on `phase-05a` and reports in `docs/reports/phase-05a.md`.

MSG-ENG-012's paragraph after its table still had the implementer run every gate script
on the report's commit before the push, which M-018 removed from the working guide and
the plan but not from the chapter, and a chapter wins over the working guide; phase 4 ran
the whole integration set four times on that reading. The paragraph now says what M-018
says: before a push, the fast checks, every unit test, the commit-message gate, the
secret scan and the integration tests a change writes or touches; the pipeline's run on
the last commit pushed is the gate of record. Each gate stays a script that gives the
pipeline's result when run locally (criterion 2). *Rejected:* waiting for Janus's
package before building the sample host (it does not use Janus); renumbering the later
phases (every chapter's reference to phase 6, 7 and 8 would move).

**Propagated to:** `07` MSG-ENG-012 (the paragraph after the table, criterion 2);
`implementation-plan.md` (phase 0's row, phase 5a's row, phase 6's row).

---

## M-020 — A standalone library: the Janus bridges come out, and the release does not wait for WE

> **Amended.** M-022: no WE adapter follows the release; phase 7 is retired.

**Date:** 2026-10-28 · **Status:** accepted · **Supersedes:** M-003

The owner's rule: this library, the identity library and every host are standalone, and
none knows another; a host that uses two of them connects them in its own code, and its
deployment (its server, its package feed, its gateways) is its own project. M-003's two
bridge packages, `Hermes.Janus` and `Hermes.Janus.Channels`, broke that rule: they
referenced the identity library and named it throughout `00`, `04`, `07` and `08`.

They come out. MSG-LIB-007 is retired; every other mention of the identity library, its
configuration key `Hermes:Janus:Transports`, its protector `Id`, its commit scope, its
allow-list row and its package source are taken out of the chapters. Nothing the bridges
did is lost to a host: an identity library's own messages reach the providers through
`AddHermesChannels`, the providers alone (MSG-LIB-003); the recipient's identity, the
content protector and the consent source are seams a host answers (MSG-LIB-005); and
`ISubjects` takes a person's erasure and export from whatever tells the host of them.
Where two senders share WE's report address, the host routes the reports that are not
this library's, since only it knows both. Plan phase 6 becomes the removal of the two
projects, their tests and every rule naming them.

A package feed is a deployment's, so a release attaches its packages to the tag's release
and publishes them to a feed only where the owner names one (`07` MSG-ENG-011). Release
1.0.0 does not wait for WE's API document: where it is not in hand, phase 8 comes before
phase 7, and the WE adapter follows in a minor release, since it adds a provider and
changes nothing a host already uses. *Rejected:* keeping the bridges as packages a host
may skip (the library would still name another library); moving them into a third
package of the owner's (a fourth project that knows two others).

**Propagated to:** `00` (status, package table and its note); `01` MSG-SEND-018,
MSG-SEND-020; `02` MSG-SMS-003, MSG-SMS-004; `04` MSG-LIB-001, MSG-LIB-003, MSG-LIB-005,
MSG-LIB-007 (retired); `06` (shared provider accounts); `07` MSG-ENG-001, MSG-ENG-009,
MSG-ENG-011; `08` sections 4 and 8; `09` (company-wide conversations); the working
guide (section 6 step 1); `implementation-plan.md` (phases 0, 6, 7 and 8, and the inputs
waited on).

---

## M-021 — Phase 6's question: an empty `PublicAPI.Shipped.txt` leaves with its project

**Date:** 2026-10-28 · **Status:** accepted · **Follows from:** M-020

Phase 6 removes the two bridge projects (M-020), and with them their
`PublicAPI.Shipped.txt` files, each holding nothing but `#nullable enable`. MSG-ENG-011
criterion 2 fails every commit that deletes such a file outside the release commit, so
the commit gate refused the removal, and phase 6 parked it. The rule exists so that a
released member never leaves the public surface without the release that records it
(`04` MSG-LIB-006). A `PublicAPI.Shipped.txt` holding no entry records no released
member, and deleting it in the commit that deletes the project file beside it removes
nothing a host could have used.

So such a file MAY be deleted with its project; every other deletion, and every change,
still waits for the release commit. The commit gate tells the case apart from the file
as the commit's parent holds it (nothing but `#nullable enable` and blank lines) and
from the project file in the same folder deleted in the same commit. A project that has
shipped a member keeps an entry, so the exception never reaches released surface.
*Rejected:* taking the bridges out in phase 8's release commit (the release commit would
carry an unrelated removal, and every run fails until then, since `00` no longer
describes the two packages); an exemption naming the two paths (a list the gate would
keep for a one-off).

**Propagated to:** `07` MSG-ENG-011 (the history value, criterion 2);
`implementation-plan.md` (phase 6's row).

---

## M-022 — The library ships no SMS gateway, and `Hermes` is the final name

**Date:** 2026-10-29 · **Status:** accepted · **Amends:** M-001, M-008, M-020

The owner's rule of M-020 reaches the SMS gateway too: a library that carries one
provider's API is shaped by one deployment's contract, as the bridges were by one
identity library. WE is the gateway one deployment contracted, not a part of this
library. SMS gateways share no protocol (each has its own request, credential, answers,
report and balance), so no provider the library could ship serves every host, as SMTP
and Web Push do for theirs.

So `Hermes.Channels` ships no SMS provider. A host that uses SMS registers its own
`ISmsProvider`, a seam required where SMS is used or the report group is mapped (`04`
MSG-LIB-005, `details.key` `smsProvider`). The contract already carries what a gateway
needs: the whole report request; a report matched by the delivery reference or by the
gateway's own identifier, under the route's key; the report address given with each
message or held once for the account; a balance in the gateway's unit, or none. Where
one account's reports reach the report group for two senders, the host's provider hands
the other sender's to it and answers them `notfinal`, so the contract routes them with
no route of the host's. MSG-SMS-003 is retired, and what it listed as WE's answers
becomes what any provider decides (`02` MSG-CHAN-001). The secret `smsWe`, the key
`Hermes:Sms:We:BaseAddress` and the named client `Hermes.Sms` go; the plain-HTTP case
moves to the report group (`05` MSG-API-006). Plan phase 7 is retired, and phase 8 makes
the removal before the release.

`Hermes` is the final name (the owner, 2026-10-29): no rename comes before 1.0.0, and
the names `08` section 8 lists keep it.

*Rejected:* a WE adapter in a package of its own within this library (the library would
still ship and release one provider's API, and change when that provider does); a
generic HTTP provider configured by request and report templates (each gateway's
credential, answer classes and report form differ, so the configuration would be a
second language with no tests of its own).

**Propagated to:** `00` (the name, status, stack and package tables); `02` MSG-CHAN-001,
MSG-SMS-003 (retired), MSG-SMS-004; `04` MSG-LIB-003, MSG-LIB-005, MSG-LIB-008
(`FixedSecretSource`); `05` MSG-API-006; `06` MSG-OPS-004, MSG-OPS-007, MSG-OPS-008;
`07` MSG-ENG-008, MSG-ENG-011; `08` sections 4, 7 and 8; `10` MSG-THR-001;
`implementation-plan.md` (phases 7 and 8, and the inputs waited on).

---

## M-023 — The release step: the release commit, its checks, and the release job

> **Amended.** M-027: a release commit and a tag may name a release candidate,
> `X.Y.Z-rc.N`, whose release is marked as a pre-release.

**Date:** 2026-10-29 · **Status:** accepted · **Follows from:** M-015, M-020

M-015 made `PublicAPI.Shipped.txt` and the release baseline change only in a release
commit and left that commit's exemption to the release step, and M-020 put the packages
on the tag's release; neither said how the gate knows a release commit or what makes the
release. Phase 8 needs both, so they are fixed here rather than asked.

- **The release commit** is named by its message, `chore: release X.Y.Z`, and the gate
  checks what it may do rather than trusting the name: it changes only the surface
  files, the baseline, the changelog and the release notes, and each only as a release
  does (the unshipped entries moved, `*REMOVED*` entries taken out, the changelog's
  `Unreleased` entries under the version's heading). A commit of that message doing
  anything else fails, so the name exempts nothing a release would not do.
- **The release commit is the last that builds anything in its pull request;** only
  `docs:` commits on `docs/` may follow it, so the tag on it carries all the pull
  request built. The release commit therefore comes after the review has passed, and a
  fix found in review comes before it.
- **The baseline is checked where it is written:** a pull request that changes it must
  leave it equal to the set the release gate reads at its head, so the next release
  compares against what was released.
- **The tag goes on the release commit,** after the merge, so MinVer gives the packages
  the tag's version and the report commit after it changes no package.
- **The release job** runs only for a release tag and after the gates pass on it, and is
  the one job with write access to the repository's contents, since creating a release
  needs it and nothing else does; it uses the runner's GitHub CLI, so no action is
  added.

*Rejected:* an exemption by a list of release commits kept in the repository (a list the
gate trusts, which the commit could edit itself); a tag on the merge commit (not the
commit the review and the pull request's gates passed); a release action from the
marketplace (a third party with write access to the repository, for what the runner's
CLI does).

**Propagated to:** `04` MSG-LIB-006; `07` MSG-ENG-011 (the release commit and releases
values, criteria 2 to 4); `implementation-plan.md` (phase 8, and the inputs waited on).

---

## M-024 — The development inbox

**Date:** 2026-10-29 · **Status:** accepted · **Follows from:** M-001, M-012

A host building on the library asked for a way to read, while it is developed, what the
library sends: a person signing in needs the code, a browser test and a setup tool need
it from another process, and the in-memory providers serve tests in one process alone.
Every host has the need, and a host's own provider would have to rebuild an email the
library builds internally, so the library ships it, in release 1.1.0, as additions only
(`04` MSG-LIB-006).

- **A sixth package, `Hermes.Development`.** The application itself references it, so it
  is not `Hermes.Testing`, which a production build should not carry; its page and API
  need ASP.NET Core, which `Hermes.Channels` keeps out; and an application that uses the
  providers alone has no engine to put it in.
- **Development alone, without a condition in the host.** Both calls act only where the
  host's environment is Development and do nothing elsewhere, so a host writes them
  unconditionally and an unset environment, Production in .NET, captures nothing. A
  refusal to start elsewhere was weighed: it is no safer, since a deployment wrongly set
  to Development passes it too, and it makes every host write the condition.
- **The inbox wins over the host's providers in either order, and the in-memory
  providers win over the inbox.** A host registering its real SMS gateway after the call
  must not send real texts from a developer's machine; and a host's tests, which ASP.NET
  Core's test host runs in Development, must keep reading the in-memory providers.
- **Files are the store.** One folder per user and machine is the one thing every
  process of a developer reaches, an application that uses the providers alone has no
  database, and an email's `.eml` opens in any mail client. A file written under a
  temporary name and renamed, the record last, is never read half written.
- **The page, the API and the helper read the files.** A person opens the page; a
  browser test or a setup tool asks `GET /messages/latest` with `wait`, one call
  (Mailpit's `latest` convention); a .NET test waits on `DevelopmentInbox`. Each message
  comes with its links taken out, so no host parses HTML.
- **Safety.** The records hold codes in plain text, so: the inbox acts in Development
  alone; it answers only the machine itself and only under a loopback host name, the
  `Host` check being webpack-dev-server's answer to DNS rebinding; it sends no CORS
  headers; an email's HTML is shown from its own route under a policy and a sandbox with
  no script, links opening in a new tab, as mail clients show untrusted mail; the folder
  is its owner's alone, outside the application, and cleaned.

*Rejected:* an outside mail catcher (email only, and the SMTP provider's validated TLS
makes it awkward to reach); the messages in the library's database (an application with
the providers alone has none); a separate inbox program (one more process to start,
nothing gained); detecting codes in a message's text (each host's texts differ, and a
test matches its own pattern); real delivery to a browser in development, and SMS
delivery reports there.

**Propagated to:** `00` (the document set, the package table); `02` section 6,
MSG-DEV-001 to MSG-DEV-006; `04` MSG-LIB-001, MSG-LIB-002, MSG-LIB-003, MSG-LIB-004,
MSG-LIB-008; `05` MSG-API-007; `06` MSG-OPS-007; `07` MSG-ENG-001; `08` sections 1, 4
and 8; `10` MSG-THR-001; `implementation-plan.md` (phase 9, and the inputs waited on).

---

## M-025 — Phase 9's question: the release set reads the development inbox too

**Date:** 2026-10-29 · **Status:** accepted · **Follows from:** M-023, M-024

The release gate reads its set (the routes, the bodies, the codes, the keys) through
`tests/Hermes.Tests`, which `04` MSG-LIB-001 let reference only `Hermes` and
`Hermes.Testing`. So the development inbox's group, its codes and its keys would be
missing from the baseline release 1.1.0 writes, and a later change to them would pass
the gate unseen; phase 9 tested the group against `05` in `Hermes.Development.Tests`
instead, and asked.

`tests/Hermes.Tests` may reference `Hermes.Development` too, so one contract test maps
every group of `05`, the inbox's in a host whose environment is Development, and the
release set holds the inbox's routes, bodies, codes and keys with the engine's. A test
project's reference reaches no package a host takes, so nothing a host depends on
changes. It is built in phase 9's pull request before its release commit, whose baseline
then holds them. *Rejected:* the release gate reading a second set from
`Hermes.Development.Tests` and joining the two (two builds and a join in the gate, for
what one reference does); leaving the inbox out of the set (its contract would be
guarded by no release).

**Propagated to:** `04` MSG-LIB-001 (the table); `07` MSG-ENG-006 (criterion 1).

---

## M-026 — The in-application list

> **Amended.** M-027: release 1.2.0 was never tagged; the in-application list ships in
> the first release candidate, `1.0.0-rc.1`.

**Date:** 2026-10-31 · **Status:** accepted · **Follows from:** M-004, M-005, M-006,
M-007

A host building on the library asked for the `inapp` channel the first release was
designed for (M-004): a host showing a list of a person's notifications keeps one more
store of personal data to erase and export, one more place that decides what a person
sees, and the same list rebuilt by every host. Every host with a signed-in person has
the need, so the library builds it, in release 1.2.0, as additions only (`04`
MSG-LIB-006).

- **A channel whose provider is the library's store.** A kind declares `InApp(...)`
  beside its other channels. Its delivery passes the checks as any other and,
  dispatched, writes one **entry** to the person's list instead of calling an outside
  provider, so the send, its deliveries, the observer and retention stay as they are.
  Only a person has a list: an anonymous recipient's send naming `inapp` is refused as
  push's is.
- **Rendered at the send, in every declared language.** The host asked that an entry be
  read in the language the reader asks for at that moment, not the send's, and that old
  entries keep reading after a template changes. Rendering the in-app parts in each
  declared language when the send is recorded meets both: the read picks the language,
  and a template changed later changes no recorded send, as `01` MSG-SEND-018 already
  holds. The rendered parts are protected values like any content, and a read never
  fails to render. A language declared later is missing from older entries, which then
  read in the default language. *Rejected:* keeping each entry's template version and
  values and rendering at the read (every version of every in-app template kept for as
  long as an entry lives, a read that can fail to render, and the values kept in a form
  no other channel keeps).
- **Where an entry leads is the host's.** A kind's in-app channel declares a target key
  and which of the send's values go with it (an order's identifier, say); the entry
  answers both and the host turns them into a link. The library knows nothing of the
  host's screens.
- **"Cannot be turned off" is a deployment setting.** `Hermes:InApp:AlwaysOn` makes the
  in-app channel of every optional category impossible to turn off and leaves it off the
  preferences routes. `Essential()` was weighed and is not it: it also lifts
  suppressions, quiet hours and the SMS floor on the category's other channels, which a
  host that only wants its list complete does not want. Consent still applies: an offer
  needs its consent in a list as anywhere.
- **Never held.** An entry interrupts nobody, so quiet hours never apply to it, and it
  has no provider rate. A limit applies only where a category declares one for `inapp`.
- **Retention of its own.** A list is read for months, so an entry lives
  `Hermes:Retention:InApp` (default 180 days) from its creation, not
  `Hermes:Retention:Content`. Erasure removes a person's entries and export answers
  them, as for every other channel.
- **Read is a status.** An entry stored ends its delivery `sent`; marked read, the
  delivery moves from `sent` to a new final status, `read`, the one move `sent` may make
  on this channel, so the observer is told of both in order (`01` MSG-SEND-017) and a
  host refreshes an open screen or badges another device from its notices. The library
  builds no socket of its own. Read is one way: nothing marks an entry unread.
- **A read API and its routes.** `IInApp` answers a page of a person's entries newest
  first and the unread count, and marks one or all read; `MapHermesInApp` maps the same
  for the person signed in, as `MapHermesPushSubscriptions` does, and another person's
  entry answers as one that does not exist.

*Rejected:* a socket or server-sent events in the library (the host's realtime choice,
which the notices already feed); entries kept in the delivery's row (content retention
would delete them after 30 days); marking an entry unread (no host asked, and it would
make `read` move again).

**Propagated to:** `00` MSG-OVR-003 and the glossary; `01` MSG-SEND-001, MSG-SEND-002,
MSG-SEND-003, MSG-SEND-005, MSG-SEND-006, MSG-SEND-007, MSG-SEND-010, MSG-SEND-011,
MSG-SEND-013, MSG-SEND-014, MSG-SEND-017 to MSG-SEND-020; `02` MSG-CHAN-001,
MSG-CHAN-002, section 5 (MSG-INAPP-001 to MSG-INAPP-005), MSG-DEV-001; `03` MSG-TPL-001,
MSG-TPL-002; `04` MSG-LIB-002 to MSG-LIB-005; `05` MSG-API-002, MSG-API-003,
MSG-API-008; `06` MSG-OPS-002, MSG-OPS-004; `08` sections 1 to 4 and 8; `10`
MSG-THR-001; `implementation-plan.md` (phase 10).

---

## M-027 — Release candidates until the final 1.0.0

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** M-012, M-023, M-026

The owner, 2026-10-31: none of his libraries is final or stable enough yet for the
compatibility rule to bind, so each ships release candidates until he calls a release
final, and Hermes starts again at 1.0.0.

- **Releases 1.0.0 and 1.1.0 are withdrawn.** The owner deletes their releases and tags
  on the remote; 1.2.0 was never tagged. No feed ever held them (M-020), and the one
  host that uses the library keeps a copy of its own, so nothing outside the repository
  points at them. History keeps their release commits; only the tags and the releases
  go.
- **What they released is the first release candidate, `1.0.0-rc.1`.** Phase 11 gathers
  the changelog's three released sections back under `Unreleased`, writes the release
  notes as one section, and makes the release commit `chore: release 1.0.0-rc.1`.
- **A release candidate is `X.Y.Z-rc.N`,** a SemVer 2.0.0 pre-release numbered from 1
  for each version, tagged `vX.Y.Z-rc.N`, its release marked as a pre-release. MinVer
  reads such a tag as it is. A release candidate is made on the lead's message; a final
  release on the owner's.
- **Before the first final release, nothing binds.** A release candidate of 1.0.0 may
  change or remove anything: SemVer says a pre-release may not meet what its version
  will promise. Each release candidate's release notes name every change and removal
  since the release before it, so a host knows what to adapt. From the first final
  release on, `04` MSG-LIB-006 binds against the newest final release; a release
  candidate is never that baseline, and a release candidate `X.Y.Z-rc.N` is held to what
  `X.Y.Z` needs.
- **Why the rule itself stays.** A host takes a final release's minor and patch versions
  without reading the library's code, and the rule, enforced by the gate, is what makes
  that safe; a break is not forbidden, it is a new major version that tells the host to
  look. It was turned on too early here, at a 1.0.0 the design had not yet earned, not
  wrongly.

*Rejected:* versions `0.y.z` (SemVer's other way of saying "not yet stable", but the
owner asked for release candidates of 1.0.0, which carry the version the first final
release will have); keeping 1.0.0 and 1.1.0 and spending a major version on every break
(versions that claim a stability the design does not have, and majors used up while it
settles).

**Propagated to:** `00` section 3; `04` MSG-LIB-006; `06` MSG-OPS-002; `07`
MSG-ENG-011; `implementation-plan.md` (phase 11, and the inputs waited on).

---

## M-028 — A delivery notice carries its send's idempotency key

> **Amended.** M-029: the member is an optional property declared last, not a
> positional one; the key is kept as the send's content is, protected and gone with it,
> and an export shows it. M-030: each delivery keeps it, as one of its values.

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** M-005

A host that hands the library another component's messages keys each send with that
component's own reference (`01` MSG-SEND-009), and has to give each outcome back under
that reference. The first host to do so found that nothing the observer is told names
it: a `DeliveryNotice` carries the library's `SendId` alone, and `IMessages.ReadAsync`
answers deliveries, not the key. The host would have to keep a table of its own from
each receipt's `SendId` to its key: a second record of every send, beside the
library's, with a retention and an erasure of its own to keep in step.

- **A notice carries the key its send was recorded with,**
  `DeliveryNotice.IdempotencyKey`, none where the send had none. It is the host's own
  value, given back as given.
- **The key is kept with the send,** not read from its idempotency row: the row lapses
  after `Hermes:Idempotency:Window`, while a delivery's outcome can come later (a
  provider's report moving `sent` to `delivered` or `failed`). It is kept for as long
  as the send's record.
- **It goes with the person.** The library already treats a key as the person's (it is
  exported and erased with them, MSG-SEND-020), since a host may put anything in it.
  An erasure deletes the key kept with the person's sends, and the notices kept for the
  observer carry none, as they carry no person reference.
- **A notice still carries no destination and no content.**

*Rejected:* a host table from `SendId` to key (every host that keys its sends would keep
the same second record); `IMessages.ReadAsync` answering the key (a lookup per notice,
and nothing to look up once the row has lapsed); a new positional member placed other
than last (it would reorder what every reader of a notice already deconstructs).

**Propagated to:** `01` MSG-SEND-017, MSG-SEND-020; `04` MSG-LIB-005;
`implementation-plan.md` (phase 12).

---

## M-029 — Phase 12's questions: the notice's key, kept as content

> **Amended.** M-030: each delivery keeps the key, as one of its values, not the send.

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** M-028

Phase 12 built M-028 and asked four questions.

- **The member is an optional property declared last,** as `Person` and `Reason` are.
  `DeliveryNotice` is not a positional record: its constructor takes nothing, so the
  addition changes no host's code. M-028's "last positional member", and its rejection
  of another position, rested on a wrong reading of the type, and the plan's "change to
  the constructor" with them. The release notes rightly say the constructor is
  unchanged.
- **No backfill.** A send recorded before the migration carries no key in its notices,
  even where its idempotency row is still live. Copying keys forward would make a
  migration that only adds rewrite rows, to serve the sends in flight across one
  upgrade. The release notes say so.
- **The key is kept as the send's content is.** M-028 kept it in clear for
  `Hermes:Retention:Record` (365 days by default). Its idempotency row holds it for
  `Hermes:Idempotency:Window` (a day), and the send's content is protected and goes
  after `Hermes:Retention:Content` (30 days). A host may put anything in a key, which is
  why the library already treats it as the person's. It is now a protected value (M-007,
  `06` MSG-OPS-008), opened in its own row alone, and it goes when the last of the
  send's deliveries' content goes (MSG-SEND-019) or with the person (MSG-SEND-020). A
  notice written after that carries none; outcomes come well inside the content's
  retention.
- **An export shows it.** What an erasure deletes, an export shows: each delivery's
  export carries the key its send keeps, while kept (`DeliveryExport.IdempotencyKey`).
- **`10` names it** in the row of a leaked database copy, protected as the content is.

*Rejected:* keeping the key in clear and naming the risk in `10` alone (a year of a
host's values readable from a copy, where the library protects everything else a send
carried); deleting it with its idempotency row (an outcome after a day, a provider's
report among them, would lose its key).

**Propagated to:** `01` MSG-SEND-017, MSG-SEND-019, MSG-SEND-020; `04` MSG-LIB-005;
`10`; `implementation-plan.md` (phase 12).

---

## M-030 — Phase 12's second questions: the key, one of a delivery's values

**Date:** 2026-10-31 · **Status:** accepted · **Amends:** M-028, M-029

M-029 made the key kept with a send a protected value. Phase 12 asked which table and
field bind it, and what the observer does when it does not open. A send's row holds
nothing of its recipient and no protector's value, so protecting a key there would need
a binding, a person, a key version and a re-protection of its own, for one field.

- **Each delivery keeps the key, as one of its values.** It is kept as the delivery's
  destination is: protected for the delivery's person, bound to
  `deliveries:<row id>:idempotencykey`, opened by the protector its envelope names, its
  `contentKeys` version in the delivery's existing column, re-protected with the
  delivery's other values, and deleted with its content at `Hermes:Retention:Content`
  and at an erasure. The send's row keeps no key. A send on two channels keeps the key
  twice, once in each delivery, each bound to its own row.
- **A notice whose key does not open** answers as check 1 does for a delivery's values,
  save that a notice is never ended: `Erased` is told without the key, as a notice after
  an erasure is; `Rejected`, or a format neither `0x01` nor kept for a later release, is
  told without the key and raises `content-rejected`; a held key (`KeyUnavailable`, a
  throw or no answer in time, a protector not registered, a format kept for a later
  release) holds the notice for the next run, as one the observer did not take, so a
  hold that lasts raises `observer-failing` past `Hermes:Observer:LagThreshold`.
- **The export** shows the key as one of the delivery's values, under the rules it
  already has for them: marked past content retention, erased or rejected, it carries
  none; a key that cannot open yet answers the export `messaging.key.unavailable`.
- **The name** `idempotencykey` joins the fields of `08` section 8, a layout two
  releases share and never change.

*Rejected:* a protected value on the send's row (a binding without a person, a
key-version column and a re-protection of its own, for one field); holding a notice
whose key was erased or rejected (it would never open, and the delivery's later notices
would wait behind it for ever); telling a held notice without its key (the host would
lose its key for good over a moment's outage).

**Propagated to:** `01` MSG-SEND-017 to MSG-SEND-020; `04` MSG-LIB-005; `08` section 8;
`10`; `implementation-plan.md` (phase 12).

---

## M-031 — Release 2: its phases, and where conversations live

**Date:** 2026-11-01 · **Status:** accepted · **Amends:** MSG-LIB-001, MSG-LIB-002, MSG-LIB-005, MSG-LIB-006, MSG-OPS-002, MSG-ENG-001, MSG-ENG-006, MSG-ENG-009

**TL;DR.** Release 2 builds `09` in nine phases, 13 to 21, in the order the implementer's
map proposed (2026-11-01). It ships as `1.0.0-rc.3` (M-027). Conversations' tables live in
the engine's one context and one migration chain, so erasure, export, key retirement and
the schema check reach them directly. The package holds the behaviour, the endpoints and
the hubs. Every addition to rc.2's surface is additive. This entry settles phase 13 and the
rules every later phase follows; each later phase gets its own entry before it starts.

- **The phases.**
  - 13: foundations.
  - 14: the model and staff chats.
  - 15: the live connection.
  - 16: support's public side.
  - 17: automatic replies and desk tools.
  - 18: the end of support.
  - 19: attachments.
  - 20: retention, export and threats.
  - 21: contract and release candidate.

  Each is its own branch and pull request, cut from `main` after the previous one merges.
- **The tables (C1, C2).** Every conversation table and its configuration live in the
  engine's `MessagingContext` and in its one migration chain in `Hermes/Migrations`,
  under the schema `messaging` and `messaging.__migrations_history` (MSG-OPS-002). They are
  created in every deployment, and stay empty where conversations are not registered.
  `hermes migrate` applies them as it applies every other.
- **The entities.** Conversation entities live in a new engine folder, `Conversations`. They
  are internal, and the engine grants `Hermes.Conversations` its internals (MSG-ENG-001).
- **The engine knows each table (C4).** A phase that adds a table holding a protected
  value, a keyed hash or a person reference adds it in that same phase to:
  - key retirement's in-use checks (`NoContentRow`, `NoHash`) and its re-protection;
  - erasure (`Subjects`), and the person locks where the table is written for a person;
  - from phase 20, export.

  The phase's tests prove each of these for that table: a key version a conversation row
  uses is never retired, and an erased person's rows no longer open. No phase adds a
  table without them.
- **The package (C63).** `Hermes.Conversations` may reference `Hermes`, `Hermes.Core` and the
  ASP.NET Core shared framework (MSG-LIB-001). Its folders are:
  - `Chats` (`09` section 3);
  - `Support` (section 2);
  - `Attachments` (section 4);
  - `Live` (section 5);
  - `Endpoints`.

  It grants its internals to its test project, `tests/Hermes.Conversations.Tests`, and to
  `Hermes.Testing`.

  `Microsoft.AspNetCore.SignalR.Client` joins MSG-ENG-009, at the shared framework's version,
  for test projects only. The SignalR server comes with the shared framework.
- **Registration.** `AddHermesConversations(this IServiceCollection services)` is called
  after `AddHermes`, and start fails without `AddHermes`. Its keys sit under
  `Hermes:Conversations:` and are checked raw at start only where it is registered
  (MSG-OPS-001). A key it does not know fails the start, as elsewhere.
- **Seams (C3).** Each seam conversations need is a new interface in `Hermes.Core`, as every
  seam is (MSG-LIB-005). None is a new member of an existing interface. It is required at
  start only where conversations are registered, and where it is missing the start fails
  with its `declarationmissing` key.
- **Export (C5).** `SubjectExport` gains an optional member, `Conversations`, nullable and
  not `required`, filled in phase 20. No public record ever gains a `required` member: an
  addition is nullable or optional, as `DeliveryNotice.IdempotencyKey` was.
- **Hubs are surface (C47).** Each hub's route, method names and payload shapes are public
  surface. They are listed in `05` beside the routes, recorded in the release baseline,
  and found by the endpoint contract test (MSG-ENG-006) in the phase that adds them.
  Phase 21 makes the release gate read them.
- **`TemplateKey` (L19).** Phase 17's entry adds its member with the rule's new text. Until
  then nothing changes it.
- **MSG-CONV-012 (C64).** It is checked in review: the origin and the message types are open
  enumerations, so a value added later is additive. It gets no acceptance criteria.
- **Phase 13's criteria.**
  1. `AddHermesConversations` starts with one call after `AddHermes`, and without
     `AddHermes` the start fails naming it.
  2. A key under `Hermes:Conversations:` that the package does not know fails the start.
  3. The package's references and grants are exactly the above, and the repository tests
     prove them.
  4. The sample host references the package and starts.
  5. The standing rule's hooks exist, so phase 14's first table only adds itself to them.
     Prove it with a table defined in the tests only, which also proves that no key
     version it uses is retired.

*Rejected:*
- **A second context and migration chain in the package, sharing the history table:** the
  engine would have to know another assembly's migrations to tell a newer release (L5),
  and two chains have no order between them.
- **A history table of its own:** MSG-OPS-002 fixes one.
- **Seams in the package:** every other seam lives in `Hermes.Core`, and a host
  implementing one should not need the package's ASP.NET Core references.
- **A version other than `1.0.0-rc.3`:** M-027 holds until the owner calls a release final.

**Propagated to:**
- `00` section 2.2;
- `04` MSG-LIB-001, MSG-LIB-002 (`SubjectExport`, the rule on additions), MSG-LIB-005,
  MSG-LIB-006;
- `05` (hubs as surface);
- `06` MSG-OPS-001, MSG-OPS-002;
- `07` MSG-ENG-001, MSG-ENG-006, MSG-ENG-009;
- `09` preamble (the criteria come with each phase);
- `implementation-plan.md` (release 2's phases).

> **Amended.** 2026-11-01, on the review of its propagation: called without `AddHermes`,
> `AddHermesConversations` itself throws `InvalidOperationException` naming it, a fault
> of the calling code (MSG-ENG-003), at the call and not at start; a key under
> `Hermes:Conversations:` that the package does not know fails the start with
> `messaging.startup.configurationinvalid`, `details.key` the key, a rule for that
> section alone, so "as elsewhere" above was wrong, since no other key the library does
> not know fails a start; `Hermes.Testing` may reference `Hermes.Conversations`, so its
> grant has a use; `tests/Hermes.Tests` references it, for the endpoint contract test;
> and `conversations` joins the commit scopes of MSG-ENG-011. Propagated to `04`
> MSG-LIB-001, MSG-LIB-003; `06` MSG-OPS-001; `07` MSG-ENG-011; `08` sections 1 and 4;
> `implementation-plan.md` (phase 13).

---

## M-032 — Phase 14: the model and staff chats

**Date:** 2026-11-01 · **Status:** accepted · **Amends:** MSG-CONV-001, MSG-CONV-006, MSG-CONV-009, MSG-LIB-002, MSG-LIB-005

**TL;DR.** Phase 14 builds the conversation model and the staff side's `group` and
`direct` conversations over HTTP. The live connection is phase 15's. Group access asks the
host's membership seam on every read and write, and anyone who is not a participant or a
member is answered as if the conversation did not exist. A message's text is protected
under its author's key. Edits and deletes apply to one's own messages only, and a deleted
message stays as a mark without its text. This entry settles the map's C6, C8 to C11,
C35 to C38, C41, C53 to C55 (staff part) and C61.

- **The tables** (engine folder `Conversations`, schema `messaging`, M-031's standing rule
  applied to each):
  - `conversations`: its type (`support`, `group`, `direct`); its origin (`web`, the
    only value in release 2, C10); its status (`open`, `snoozed`, `closed`, C6); for
    `group`, its scope (kind and reference, unique together); and its created and
    last-message instants. Support's columns come with phase 16.
  - `participants`: a person in a conversation, with their read position, their mute,
    and the instants they joined and left.
  - `messages`: its author, its type, its protected text, the message it replies to, and
    its sent, edited and deleted instants.
  - `mentions`: a message's mentioned persons, one row each.

  A person reference is held as the in-app list holds `entries.person`. Each protected
  value follows `08` section 8's context form, the text's being `messages:<id>:text`
  (C53).
- **Statuses (C6).** They are a fixed set in `08` section 3. A queued support
  conversation is `open` with no assignee. Phase 17's statuses for desk work are tags,
  not statuses. `group` and `direct` conversations are always `open` in release 2.
- **Message types (C11).** The types are `text`, `note`, `automatic` and `system`, an open
  enumeration. `call` is not reserved. In phase 14 a person writes `text` only, and
  `note` answers 400. The library writes no `system` message in this phase.
- **Bounds (C8).**
  - A text is 1 to 4,000 characters (UTF-16 code units, as .NET counts).
  - A message mentions at most 32 distinct persons.
  - A direct conversation holds at most 16 participants.
  - These are fixed values, not keys.
- **Edits and deletes (C8, C41).**
  - A person edits or deletes only their own `text` messages, at any time.
  - An edit replaces the text and sets the edited instant. No history is kept.
  - A delete clears the text and its mentions and sets the deleted instant. The row
    stays, and reads answer it as deleted with no text.
  - Editing a deleted message answers 409.
- **Read position (C9).** It is the identifier of the last message read, and it moves
  forward only. A conversation's unread count is the messages after it that are not the
  person's own and not deleted.
- **Replies.** A reply names a message of the same conversation, deleted or not. Any other
  answers 400.
- **The membership seam (C35).** It is a new interface in `Hermes.Core`,
  `IConversationMembership`, and is required where conversations are registered:
  - `ValueTask<bool> IsMemberAsync(string person, ConversationScope scope, CancellationToken)`;
  - `IAsyncEnumerable<string> MembersAsync(ConversationScope scope, CancellationToken)`;
  - `IAsyncEnumerable<ConversationScope> ScopesAsync(string person, CancellationToken)`.

  `ConversationScope` is a record of `ScopeKind Kind` (`Organization` or `Group`) and
  `string Reference`. The library caches none of the answers: every read and write of a
  group conversation asks `IsMemberAsync`.
- **Group conversations (C36).** There is one per scope. It is created the first time a
  member opens it, and creating it again creates nothing more. A member's participant
  row (read position, mute) is written the first time they read or write it.
- **Direct conversations (C37).**
  - A person starts one by naming 1 to 15 others: distinct references, and not
    themselves.
  - Any participant may add others, up to 16 in all.
  - A participant may leave. Leaving ends their access at once, and their messages stay.
  - Someone who left comes back only by being added again.
  - The library does not check that a named reference exists. The host's authorization
    on the staff group decides who may use it.
- **Mentions (C38).** A mention is a list of person references sent with the message,
  never parsed from the text. In a direct conversation each one is a participant. In a
  group conversation each one is a member, checked through the seam. Mute silences a
  conversation's notices except the person's own mentions (phase 15).
- **Protection (MSG-CONV-009, staff part).** A staff chat message's text is protected under
  its author's key, through the engine's content protection, and bound to its row.
- **Erasure (C54, C55, staff part).** Erasing a person:
  - deletes their participant and mention rows;
  - handles their author references as the engine handles a delivery's recipient
    reference.

  Their messages stay as rows, and their text, no longer opening, reads `Erased`.
  Everyone else's messages are untouched.
- **The surface.**
  - **The interface.** `IConversations` in `Hermes.Core` (beside `IInApp`) carries each
    operation for a person.
  - **The routes.** `MapHermesStaffConversations(this IEndpointRouteBuilder routes,
    string prefix)` answers the `RouteGroupBuilder` for the host's authorization. Its
    routes are thin over `IConversations`, and the person comes from
    `IRecipientIdentity`:
    - `GET {prefix}/conversations?cursor=&size=`: the person's direct conversations
      (not left) and the existing group conversations of their scopes, newest activity
      first, each with its unread count, mute and last instant;
    - `POST {prefix}/conversations/direct` with `participants`: **201**;
    - `GET {prefix}/conversations/group/{kind}/{reference}`: the scope's conversation,
      created on first use by a member;
    - `GET {prefix}/conversations/{id}`;
    - `GET {prefix}/conversations/{id}/messages?before=&size=`: newest first, size 1 to
      100, default 50;
    - `POST {prefix}/conversations/{id}/messages` with `text`, `replyTo` and
      `mentions`: **201**;
    - `PUT` and `DELETE {prefix}/conversations/{id}/messages/{messageId}`;
    - `POST {prefix}/conversations/{id}/read` with `messageId`;
    - `PUT {prefix}/conversations/{id}/mute` with `muted`;
    - `POST {prefix}/conversations/{id}/participants` with `participants`, and
      `POST {prefix}/conversations/{id}/leave`, both for direct conversations only.
  - **Bodies and errors.** Bodies and errors are the engine's (`EndpointJson`,
    `ErrorBody`).
- **Codes (C61).**
  - `messaging.conversation.notfound` **404**: unknown, not a participant, not a member,
    or left. Never 403, so nobody learns a conversation exists.
  - `messaging.conversation.invalid` **400**, with `details.field`.
  - `messaging.conversation.full` **409**.
  - `messaging.message.notfound` **404**.
  - `messaging.message.invalid` **400**, with `details.field` `text`, `mentions`,
    `replyTo` or `type`.
  - `messaging.message.notauthor` **403**. The person can see the message, so nothing
    leaks.
  - `messaging.message.deleted` **409**.
- **Locks.** A write for a person takes their person lock shared, as the in-app list does,
  so an erasure cannot race it.
- **Phase 14's criteria.**
  1. In a group conversation, a non-member's every read and write answers 404. A member
     the seam no longer answers for is refused on the next call.
  2. In a direct conversation, a non-participant answers 404, and so does one who left,
     at once. Participants can be added up to 16, and the 17th answers 409.
  3. Edits and deletes work on one's own messages only. Another person's answers 403. A
     deleted message reads with no text, and editing it answers 409.
  4. A message's protected text copied to another row does not open. A key version a
     message uses is never retired.
  5. Erasing an author makes their messages read `Erased` and removes their participant
     and mention rows. Other messages are untouched.
  6. Each bound is refused at its edge: text of 0 or 4,001 characters, a 33rd mention, a
     mention outside the conversation, a reply into another conversation, a `note`.
  7. The read position moves forward only, and the unread counts follow it.
  8. Each route does what its operation does, and the endpoint contract test finds
     exactly these routes.

**Propagated to:**
- `04` MSG-LIB-002 (`IConversations`, `ConversationScope`), MSG-LIB-005
  (`IConversationMembership`);
- `05` (the staff routes);
- `08` sections 1, 3 and 8;
- `09` MSG-CONV-001, MSG-CONV-006, MSG-CONV-009, with their criteria.

> **Amended.** 2026-11-01, on the review of its propagation:
> - `IConversations` has one member per route, `(string person, ..., CancellationToken)`
>   answering `Result<T>`, over records for a conversation, a participant, a message and
>   their pages, with identifiers `ConversationId`, `MessageId` and `ParticipantId`.
> - The bodies are `<conversation>` `{ "id", "type", "scope", "participants", "unread",
>   "muted", "lastAt" }`, `<participant>` `{ "id", "person" }`, `<message>` `{ "id",
>   "author", "type", "text", "replyTo", "mentions", "sentAt", "editedAt", "deletedAt",
>   "erased" }`, the list `{ "conversations": [ <conversation> ], "next" }` and the
>   messages `{ "messages": [ <message> ] }`.
> - The message route's body carries `type`, optional, default `text`.
> - Editing, deleting, read, mute, participants and leave answer 204; the group route
>   answers 200, even the first time.
> - Starting with none or more than 15 others, a reference named twice or oneself
>   answers `messaging.conversation.invalid` (`participants`). The participants and
>   leave routes on a group conversation answer it with `type`, and a kind that does not
>   read with `kind`.
> - Read naming a message the conversation does not hold answers
>   `messaging.message.notfound`. Deleting a deleted message answers 204. Leave reads
>   `{ }`.
> - The list takes the in-app list's `size` and `cursor` rules, answering
>   `messaging.request.malformed`; the messages route keeps 1 to 100, default 50.
> - The routes answer 503 `messaging.request.unavailable` as the in-app routes do.
> - `08` section 2 gains the conversation types, origins, message types and scope kinds;
>   the conversation statuses are public, as `ConversationStatus`.
> - `IConversationMembership`'s `details.key` is `conversationMembership`.
> - `MapHermesStaffConversations` is on
>   `MessagingConversationsEndpointRouteBuilderExtensions`, and joins MSG-LIB-004.
> - The criterion that each route does what its operation does moves to `05`
>   MSG-API-009.
> - An asynchronous member answers a `ValueTask`, or an `IAsyncEnumerable<T>` for a
>   sequence (MSG-LIB-002).
> - Erasing an author also clears their messages' text, which then reads `Erased`, as a
>   delivery's content is deleted, whichever protector wrote it.
> - `messages` joins `01` MSG-SEND-018's context and `content-rejected`'s scope.
> - `<message>` and `MessageRecord` gain `authorKind` (`AuthorKind`): `person`,
>   `visitor`, or `library` for an `automatic` or `system` message. `author` is the
>   person reference only where it is `person`. `visitor` comes with this phase's
>   enumeration.
> - `AddHermesConversations` registers `IConversations` as a scoped service, as
>   `AddHermes` registers `IInApp`.
>
> Propagated to `01` MSG-SEND-018; `04` MSG-LIB-002, MSG-LIB-003, MSG-LIB-004,
> MSG-LIB-005; `05` MSG-API-009; `08` sections 1, 2, 3 and 5; `09` MSG-CONV-006,
> MSG-CONV-009; `implementation-plan.md` (phase 14).

---

## M-033 — Phase 15: the live connection

**Date:** 2026-11-01 · **Status:** accepted · **Amends:** MSG-CONV-005, MSG-CONV-006, MSG-CONV-008

**TL;DR.** The staff side gets a SignalR hub that pushes what HTTP wrote, after it is stored,
to the participants connected to any process. Processes tell each other through `NOTIFY`
on a listening connection of the package's own, carrying identifiers that name no person.
Typing is relayed and never stored. Presence is a connection, shown in direct
conversations. A person who is away hears of a direct message or a mention through
sending, once per conversation until they read it. This entry settles the map's C34, C39,
C40 and C47 to C49. C50 (the public hub) is phase 16's.

- **The hub (C47).**
  - **Route.** `MapHermesStaffConversations` also maps the staff hub at `{prefix}/live`,
    inside the same `RouteGroupBuilder`, so the host's authorization covers it.
  - **Person.** The person comes from `IRecipientIdentity` at connect, and a connection
    without one is refused.
  - **Server to client:**
    - `message(conversationId, message)`: a new message, in the shape the messages
      route answers;
    - `messageChanged(conversationId, message)`: an edit or a delete;
    - `read(conversationId, participantId, messageId)`;
    - `typing(conversationId, participantId)`;
    - `presence(participantId, online)`;
    - `conversation(conversation)`: one added to the person's list.
  - **Client to server.** One method only, `Typing(conversationId)`: writes stay HTTP.
  - **Surface.** The hub's route, methods and shapes are surface (M-031).
- **Catching up.** The messages route gains `after={messageId}`, which answers oldest
  first. A client that reconnects reads from its last message, since every push follows
  the store.
- **Fan-out (C48).**
  - **The channel.** After a write commits, the write sends `NOTIFY messaging_conversations`
    with `<event>:<conversationId>:<id>`. The event is `message`, `changed`, `read`,
    `typing` or `presence`. The id is a message's or a participant's, never a person
    reference.
  - **Listening.** Each process listens on a connection of the package's own, opened from
    `listenConnection` where set and else the library's connection (MSG-OPS-003).
    Dispatch and conversations never wait on each other.
  - **Who receives.** Each process pushes to its own connections whose person may read
    the conversation now. For a direct conversation, that is the participants not left.
    For a group conversation, it is the scope's members from one `MembersAsync` call per
    event, intersected with the persons connected to that process.
- **Typing (C34).**
  - `Typing` is relayed to the conversation's other connected readers, and never stored.
  - A client sends it at most every 3 seconds while typing. A receiver shows it for 5
    seconds after the last one.
  - A person whose participant row does not exist yet gets one written first.
- **Read indicators (C34).** Moving a read position (phase 14) sends `read` to the other
  readers.
- **Presence (C39).**
  - A person is online while they hold a staff-hub connection on any process.
  - The `connections` table holds each connection's person, process and last-seen
    instant. A process refreshes its rows every 30 seconds, and a row older than 90
    seconds is gone.
  - Presence is shown in direct conversations only: as each participant's `online` in
    the conversation's read, and as `presence` pushes to the other participants. Group
    conversations show none.
  - `connections` holds a person reference, so M-031's standing rule applies:
    erasure deletes the rows.
- **Away notices (C40, C49).**
  - **Who.** A participant of a direct conversation, or a person mentioned in a group
    conversation, who holds no staff-hub connection when the message is written and has
    not muted the conversation, is told through sending. A mention is told even where
    the conversation is muted (M-032).
  - **How often.** Once per person per conversation, until their read position passes
    the message that was told. The send's idempotency key is
    `conversation:<id>:away:<participantId>:<messageId>`.
  - **The kind.** It is the one named by `Hermes:Conversations:AwayKind`. It must be a
    declared kind, or the start fails naming the key. Unset, no away notice is sent.
  - **Values.** The kind's values are the conversation's identifier and type, the
    message's identifier, and whether the person was mentioned. The text is never
    included: it is protected, and a notice must not carry it.
  - The kind's own channels, preferences and quiet hours apply, as for any send.
- **Phase 15's criteria.**
  1. A message written through process A reaches a client connected to process B, after
     it is stored.
  2. A client that reconnects reads what it missed with `after`.
  3. `NOTIFY`'s payload holds no person reference or text.
  4. A non-member connected to the hub receives nothing of a group conversation, and a
     member the seam stops answering for receives nothing from the next event.
  5. Typing reaches the others and is never stored.
  6. Presence turns offline within 90 seconds of a process stopping.
  7. An away participant is told once until they read. A mention is told through a
     mute. A connected one is not told.
  8. `AwayKind` naming an undeclared kind fails the start.
  9. The contract test finds the hub's route and methods.

**Propagated to:**
- `05` (the hub, `after`);
- `06` MSG-OPS-003 (the listening connection);
- `08` sections 4 (`AwayKind`) and 8 (the channel, the `connections` table);
- `09` MSG-CONV-005, MSG-CONV-006, MSG-CONV-008, with their criteria.

> **Amended.** 2026-11-01, on the review of its propagation:
> - The away kind's values are `conversation`, `conversationType`, `message` and
>   `mentioned`. `AwayKind` is read at start.
> - A participant carries `online`, in the body and in `ParticipantRecord`.
> - `MessagesAsync` takes `after` beside `before`.
> - The criterion that the contract test finds the hub moves to `05` MSG-API-009.
> - `GET /conversations` also answers 503, as `GET /inapp` does.
> - A scope's reference is 1 to 128 characters, as a person reference; outside the bound
>   the group route answers 400 `messaging.request.malformed`, `member` `reference`.
> - The sample host maps the in-app group too, built in this phase (`07` MSG-ENG-001).
>
> Propagated to `04` MSG-LIB-002; `05` MSG-API-009; `07` MSG-ENG-001; `08` section 4;
> `09` MSG-CONV-008; `implementation-plan.md` (phase 15).

---

## M-034 — Phase 16: support's public side

**Date:** 2026-11-01 · **Status:** accepted · **Amends:** MSG-CONV-001, MSG-CONV-002, MSG-CONV-009, MSG-CONV-010, MSG-CONV-011

**TL;DR.**
- A visitor is known by a reference the library draws, kept in an HttpOnly cookie and
  stored only as a keyed hash.
- A signed-in customer is their person.
- Topics are configuration. Each one has hours in a time zone and the membership scope
  whose members staff it.
- A queued conversation goes at once to the available member with the fewest open
  conversations. With nobody available, the visitor gets the offline form, answered by
  email.
- A visitor acknowledges the host's privacy notice before writing.
- The public side is limited by counters in the database, and staff can block a visitor.

This entry settles the map's C7, C12 to C19, C50 to C52 and C57 to C60.

- **The visitor (C7, C50).**
  - **The reference.** The public side draws `visitor:` followed by 128 random bits in
    base64url on the first call that needs one.
  - **The cookie.** The reference is kept in `hermes_visitor`: HttpOnly, Secure,
    SameSite Lax, path the public prefix, renewed on use, and living as long as
    `Retention:Visitor`.
  - **Storage.** It is a bearer credential, so the database holds only its keyed hash,
    under a new domain, `visitor` (`08` section 8).
  - **The public hub.** It is authenticated by the same cookie.
- **A signed-in customer (C51, C52).**
  - Where `IRecipientIdentity` answers a person on the public side, the conversation is
    that person's: no cookie, no notice, and protection under their key through the host
    protector.
  - A visitor's conversation is protected through the library protector, with the visitor
    reference as its person, as `01` MSG-SEND-018 protects anonymous values.
  - Staff replies are under the same key (MSG-CONV-009).
- **Topics (C12, C13).**
  - Each topic is configuration, `Hermes:Conversations:Topics:<name>`, its name under
    `08` section 3's name rule. It has:
    - `TimeZone` (IANA);
    - `Hours`, from `Monday` to `Sunday`, each a list of `HH:mm-HH:mm`;
    - `Exceptions`, by date, each `Closed` or its own list;
    - `Scope`: a kind and a reference for the membership seam (M-032).
  - The staff of a topic are its scope's members.
  - A topic's display name is the host frontend's.
- **Who answers (C14, C15, C16).**
  - **Available.** A staff member is available while they hold a staff-hub connection
    (M-033), have set themselves available, and hold fewer open support conversations
    than `Hermes:Conversations:Support:Limit` (default 3).
  - **Being set available.** Through `PUT {staff}/support/availability`, stored in an
    `agents` table, which holds a person reference, so M-031's rule applies.
  - **Assignment.** It is automatic. When a conversation is queued, or a member frees up,
    the oldest queued conversation of each topic goes to the available member with the
    fewest open conversations. Ties go to the one longest since their last assignment.
  - **The queue.** It is first in, first out per topic.
  - **The visitor's place.** It is in the conversation's read, and pushed over the
    public hub as `place(position)` (C17).
- **Offline (C18).** Outside a topic's hours, or within them while no member is
  available, starting a conversation answers **409** `messaging.support.offline`, and the
  visitor uses the offline form instead.
  - The form takes an email address and the text. It becomes a conversation flagged
    `offline`, queued like any other.
  - Staff replies to it are sent through sending, as the kind named by
    `Hermes:Conversations:Support:OfflineReplyKind`, to `Recipient.Anonymous` with that
    address. The key is required where topics are configured.
  - The address is a protected value of the conversation.
- **Transfer (C19).** A staff member transfers to another member of the topic's scope,
  assigned at once, or to another topic's queue. The receiver does not accept.
- **The privacy notice (C57).**
  - The notice's text is the host's. The library records the acknowledgement: the
    visitor's hash, the version, the instant and the language.
  - The version is `Hermes:Conversations:Support:NoticeVersion`, required where topics
    are configured.
  - A visitor's message before an acknowledgement of the current version answers **409**
    `messaging.support.noticerequired`. A signed-in customer is never asked.
- **Limits (C58, C59).**
  - Counters live in the database, across processes, as MSG-SEND-014 counts. Each is a
    key under `Hermes:Conversations:Limits:`:
    - per visitor, conversations started per day (`VisitorStarts`, default 5) and
      messages per minute (`VisitorMessages`, default 20);
    - per source address, conversations started per hour (`SourceStarts`, default 20).
  - Over a limit answers **429** `messaging.support.limited` with `Retry-After`.
  - The source address is the request's remote address, as the host's forwarded-headers
    set-up gives it. IPv6 is grouped by its /64. The address is stored as a keyed hash
    under a new domain, `source`.
- **Blocking (C60).**
  - Staff block a visitor or a customer by their conversation, until an instant or until
    lifted, and lift the block.
  - A blocked one starting or writing answers **403** `messaging.support.blocked`.
  - Blocking by address is not offered, since addresses are shared.
  - The `blocks` table holds a person reference or a visitor hash, so M-031's rule
    applies.
- **The routes.**
  - **Public.** `MapHermesPublicConversations(this IEndpointRouteBuilder routes, string
    prefix)` answers its `RouteGroupBuilder`, with:
    - `GET {prefix}/support/topics`: each topic, open now or offline;
    - `POST {prefix}/support/notice` with `version`;
    - `POST {prefix}/support/conversations` with `topic` and `text`: **201**;
    - `POST {prefix}/support/offline` with `topic`, `email` and `text`: **201**;
    - `GET {prefix}/support/conversations/current`: the open conversation, with its
      place;
    - `GET` and `POST {prefix}/support/conversations/{id}/messages` (with `after`, as
      M-033);
    - `POST {prefix}/support/conversations/{id}/read` and `.../close`;
    - the hub `{prefix}/live`: `message`, `messageChanged`, `typing`, `place`,
      `assigned` and `closed`, and `Typing` from the client.
  - **Staff, on M-032's group.**
    - `GET {staff}/support/queue?topic=`;
    - `PUT {staff}/support/availability`;
    - `POST {staff}/support/conversations/{id}/transfer` with `person` or `topic`;
    - `POST {staff}/support/conversations/{id}/close`;
    - `POST` and `DELETE {staff}/support/conversations/{id}/block`.

    A staff member writes `text` or `note` in a support conversation they are assigned,
    through M-032's messages route. A visitor never sees a `note`, in reads or pushes.
- **Erasure (C55, support part).** Erasing a person deletes their support conversations
  with every row of them. A visitor's conversations go by retention (phase 20).

**Phase 16's criteria.**
1. FIFO per topic, to the available member with the fewest open conversations, within
   the topic's hours and exceptions in its time zone.
2. The visitor's place is right and pushed.
3. A transfer moves the conversation.
4. Outside hours, or with nobody available, the start answers `offline`, and the
   offline form's conversation is answered by email of `OfflineReplyKind`.
5. No visitor message is taken before the current notice is acknowledged. A customer is
   never asked.
6. The visitor's reference is never stored in clear, and another browser's cookie
   opens nothing.
7. Each limit answers 429 at its edge, across two processes.
8. A blocked visitor is refused until the block ends or is lifted.
9. A visitor never receives a `note`.
10. Erasing a customer deletes their support conversations.

**Propagated to:**
- `05` (the public routes and hub, the staff support routes);
- `08` sections 1, 4 and 8 (codes, keys, domains, tables);
- `09` MSG-CONV-001, MSG-CONV-002, MSG-CONV-009, MSG-CONV-010, MSG-CONV-011, with
  their criteria.

> **Amended.** 2026-11-01, on the review of its propagation:
> - Topic names follow `01` MSG-SEND-001's name rule, not `08` section 3's, which has
>   none.
> - A topic's keys: `TimeZone`, `Scope:Kind` and `Scope:Reference` are required; `Hours`
>   ranges are sorted, not overlapping, each starting before it ends, and a day not
>   configured is closed; `Exceptions` are keyed `yyyy-MM-dd`. `Support:Limit` is 1 to
>   100, each `Limits:*` 1 to 10,000, and `NoticeVersion` 1 to 64 characters.
> - `Retention:Visitor` is `365.00:00:00`, 1 day to 10 years.
> - `OfflineReplyKind` is checked at start, as `AwayKind` is.
> - The support routes' writes answer 204, with the bodies `{ "available" }`,
>   `{ "until" }`, the visitor's `{ "text" }` and read's `{ "messageId" }`. `GET
>   current` with none open answers 404. The offline and notice routes answer `limited`
>   and `blocked`. The public hub's payloads are the staff hub's, with
>   `assigned(conversationId)`, `closed(conversationId)` and `Typing(conversationId)`.
> - A support conversation carries its `topic`, `status`, `place` and `offline`.
> - The offline address's context is `conversations:<id>:email`.
> - Support's tables are `support_agents`, `support_blocks`, `support_acknowledgements`
>   and `support_counters`, in place of `agents` and `blocks`. Support's columns on
>   `conversations` are `topic`, `assignee`, `queued_at`, `assigned_at`, `offline`,
>   `closed_at` and `closed_by`. `participants` holds either `person` or `visitor` (the
>   keyed hash), exactly one.
> - A customer's conversation is protected through the protector a person's values are
>   written with.
> - `05` MSG-API-010 gets its own criterion: the contract test finds exactly the public
>   routes and hub.
> - The cookie is `messaging_visitor`, a neutral name, since it is stored and shared by
>   two releases (`00`).
> - The contract test maps each conversations group under its own prefix.
> - `conversations` joins `01` MSG-SEND-018's context and `content-rejected`'s scope.
> - A transfer to a person outside the topic's scope answers 400
>   `messaging.conversation.invalid`, field `person`; to a topic not configured, the
>   same with field `topic`.
> - The offline form also answers `messaging.support.noticerequired` for a visitor who
>   has not acknowledged the current notice.
>
> Propagated to `01` MSG-SEND-018; `04` MSG-LIB-002, MSG-LIB-004; `05` MSG-API-009,
> MSG-API-010; `07` MSG-ENG-006; `08` sections 1, 4, 5 and 8; `09` MSG-CONV-001,
> MSG-CONV-002, MSG-CONV-009, MSG-CONV-010, MSG-CONV-011; `implementation-plan.md`
> (phase 16).

> **Amended.** 2026-11-01, on the review of phase 16's work:
> - A visitor's values are protected as an anonymous recipient's are, with no person,
>   bound to their row. A visitor's conversations go by retention, never by erasure.
> - `messaging_conversations` gains the events `place`, `assigned`, `closed` and
>   `conversation`, each `<event>:<conversationId>:<conversationId>`.
> - A member of the topic's scope reads a support conversation of the topic, transfers,
>   closes and blocks it, and reads the topic's queue. Anyone else is answered 404, or an
>   empty list for the queue. A member who is not the assignee writing is answered 404.
>   Staff-side pushes go to the assignee alone; phase 18 adds the topic's supervisors.
> - A staff member's list of conversations holds the open support conversations assigned
>   to them, and the staff hub's `conversation` push tells them of one.
> - `OfflineReplyKind`'s values are `conversation`, `message`, `topic` and `text`, the
>   reply itself.
> - `POST {public}/support/notice` answers 429 where the visitor or the source address is
>   already at its starts limit, and counts nothing.
> - A visitor or a customer holds one open conversation per topic: a second start on the
>   same topic answers 409 `messaging.support.alreadyopen`, with `details.conversation`.
>   `current` answers the newest open one.
> - Support's storage columns join `08` section 8: `conversations.customer`, the offline
>   `email` with its key version, `messages.owner`, and the hash version columns.
>   Signed-in customers are limited as visitors are, counted by their person's keyed
>   hash. Assignment runs only within hours: on queueing, a member freeing up, setting
>   themselves available or connecting, and every 30 seconds. A topic is `open` within
>   its hours while a member is available.
>
> Propagated to `01` MSG-SEND-018; `05` MSG-API-009, MSG-API-010; `08` sections 1 and 8;
> `09` MSG-CONV-002, MSG-CONV-008, MSG-CONV-009, MSG-CONV-011; `implementation-plan.md`
> (phase 16).

---

## M-035 — Phase 17: automatic replies and desk tools

**Date:** 2026-11-01 · **Status:** accepted · **Amends:** MSG-CONV-003, MSG-CONV-005, MSG-LIB-002 (`TemplateKey`)

**TL;DR.**
- Automatic replies are rules in configuration, each a trigger and a reply template found
  through a new `TemplateKey` member, `Reply`.
- The desk gets saved replies (shared per topic, or a staff member's own), tags declared in
  configuration, snoozing until an instant, and automatic closing after a silence, with
  the closing rule's warning first.
- One job runs every timer.

This entry settles the map's C20 to C23, C28 to C31 and L19.

- **Rules (C20, C22).**
  - Each rule is `Hermes:Conversations:Replies:<name>`, its name under `01` MSG-SEND-001's
    name rule, with:
    - `Trigger`: `started`, `unavailable`, `waiting`, `outsidehours` or `closing`;
    - `Topics`: the topics it serves, all when absent;
    - for `waiting`, `After`, from 1 minute to 1 hour.
  - A rule fires at most once per conversation.
  - `started` fires when a conversation is queued. `unavailable` fires when it is queued
    while no member is available. `outsidehours` fires on an offline form's conversation.
    `waiting` fires once a queued conversation has waited `After`. `closing` fires
    `Support:CloseWarning` before an automatic close.
  - The message written is of type `automatic`, `authorKind` `library`, in the
    conversation's language. That is the visitor's or the customer's language as the
    public side read it (`Accept-Language` for a visitor, the person's for a customer),
    matched as `01` MSG-SEND-003 matches.
  - Where two rules fire at once, both write, in name order.
- **The template (C21, L19).**
  - `TemplateKey` gains `string? Reply`. A key now names one of three things:
    - a kind and a part;
    - a page;
    - a reply, by `Reply` alone with `Kind` and `Page` null.
  - MSG-LIB-002's rule is rewritten to say so. The release notes warn that a host source
    reading `Kind == null` as a page must now also read `Reply`.
  - The library's own source looks in `replies/<name>.<language>.text.liquid`.
  - A reply's values are:
    - `topic`;
    - `place`, the visitor's place, or none;
    - `wait`, the minutes waited;
    - `opens`, the topic's next opening instant, or none, formatted by the template.
- **The start check (C23).** Every rule's template is checked in every declared language,
  as `03` MSG-TPL-001 and MSG-TPL-004 check kinds. A missing one fails the start with
  `messaging.startup.templatemissing`, naming the rule and the language.
- **Saved replies (C28).**
  - A saved reply has a name (1 to 64 characters) and a text (1 to 4,000), plain text
    without templating. It is either shared by a topic or a staff member's own.
  - A topic holds at most 200, and a person at most 100.
  - Staff of a topic manage its shared replies, and each person their own:
    `GET`, `POST`, `PUT` and `DELETE {staff}/support/replies`, with `topic` or `mine`.
  - The table `support_replies` holds a person reference for one's own, so M-031's rule
    applies. Erasure deletes a person's own replies.
- **Tags (C29).**
  - The tags are declared in `Hermes:Conversations:Tags`, a list of names under the
    name rule, at most 50.
  - Staff add and remove them on a support conversation they may read:
    `PUT {staff}/support/conversations/{id}/tags` with the whole set.
  - An undeclared tag answers 400 `messaging.conversation.invalid`, `field` `tags`.
  - Tags are not statuses: statuses stay M-032's fixed set.
- **Snoozing (C30).**
  - `POST {staff}/support/conversations/{id}/snooze` with `until`, from 5 minutes to 30
    days ahead, sets the status `snoozed`.
  - The conversation wakes to `open` at `until`, or at once when the visitor or the
    customer writes.
  - A snoozed conversation keeps its assignee, and counts toward their limit.
- **Automatic closing (C31).**
  - `Hermes:Conversations:Support:CloseAfter` (default 24 hours, 1 hour to 30 days) is the
    silence after which an open or snoozed support conversation closes.
  - The silence is measured from its last message of any type but `note`.
  - `Support:CloseWarning` (default 5 minutes, 1 minute to 1 hour, shorter than
    `CloseAfter`) is how long before the close the `closing` rule fires.
  - A message after the warning restarts the silence. The close is a `system` message and
    the status `closed`, as a staff close is.
- **The job.**
  - A new job, `conversation-timers`, runs every 30 seconds under the engine's job claims
    (`06` MSG-OPS-004). It fires `waiting` and `closing`, wakes snoozes, and closes.
  - Each timer fires once, survives a restart, and fires nothing on a closed
    conversation. Each timer's next instant is a column, moved in the transaction that
    fires it.
- **Phase 17's criteria.**
  1. Each trigger writes one `automatic` message in the conversation's language from its
     template, once per conversation.
  2. A rule's missing template fails the start, naming it.
  3. A host source that answers pages for `Kind == null` and reads `Reply` serves a reply.
     The library's own source serves one from `replies/`.
  4. Saved replies keep their bounds and owners. Another person's own reply answers 404.
  5. An undeclared tag is refused.
  6. A snoozed conversation wakes at its instant, and at once on the visitor's message.
  7. A conversation silent past `CloseAfter` closes, the warning first, and a message
     after the warning restarts the silence.
  8. A restart fires no timer twice and misses none, and nothing fires on a closed
     conversation.

**Propagated to:**
- `03` MSG-TPL-001, MSG-TPL-004, MSG-TPL-006;
- `04` MSG-LIB-002 (`TemplateKey`);
- `05` (the routes);
- `06` MSG-OPS-004 (the job);
- `08` sections 1, 4 and 8;
- `09` MSG-CONV-003, MSG-CONV-005, with their criteria.

> **Amended.** 2026-11-01, on the review of its propagation:
> - `messaging.startup.templatemissing` is a startup code of its own, its details `rule`
>   and `language`.
> - The saved replies' routes: `GET /support/replies`, with `topic` or `mine`, answers
>   `{ "replies": [ <reply> ] }`, `<reply>` being `{ "id", "name", "text", "topic" }`
>   with `topic` `null` for one's own, and a topic not configured 400
>   `messaging.request.malformed`, `member` `topic`. `POST /support/replies` with
>   `{ "name", "text", "topic" }` answers 201 with the reply. `PUT` and
>   `DELETE /support/replies/{id}`, the first with `{ "name", "text" }`, answer 204.
> - A name or text outside its bounds answers 400 `messaging.reply.invalid`, `field`
>   `name` or `text`; a reply past its owner's count 409 `messaging.reply.full`; another
>   person's own reply 404 `messaging.reply.notfound`. A name is unique per owner, and a
>   second answers 409 `messaging.reply.nameexists`.
> - `IConversations` gains `SetTagsAsync`, `SnoozeAsync`, `RepliesAsync`,
>   `AddReplyAsync`, `ChangeReplyAsync` and `RemoveReplyAsync`, with a `SavedReplyId`
>   and a `SavedReplyRecord` (`Id`, `Name`, `Text`, `Topic`). `ConversationRecord`
>   gains `Tags` and `SnoozedUntil`, and `<conversation>` gains `tags` and
>   `snoozedUntil`.
> - The tags and snooze routes answer 204, 404 `messaging.conversation.notfound` and
>   503. An `until` outside its range answers 400 `messaging.conversation.invalid`,
>   `field` `until`. Either route on a conversation that is not support answers the same
>   with `field` `type`, and on a closed one 409 `messaging.conversation.closed`, a new
>   code. Snoozing follows the tags' rule: a support conversation the staff member may
>   read.
> - Tags are kept in the column `tags` of `conversations`. A tag dropped from the
>   declaration stays on its conversations, and is refused only when it is put again.
> - `conversation-timers` is turned on by `Hermes:Jobs:Enabled`, where conversations are
>   registered, and has its row in `06` MSG-OPS-004's table.
> - The conversation's language is matched when it starts and kept in the column
>   `language` of `conversations`: a visitor's from `Accept-Language`, a customer's read
>   as `01` MSG-SEND-003 reads a person's language.
> - `unavailable` fires on an offline form's conversation started within its topic's
>   hours while no member is available, and `outsidehours` on one started outside them.
>   An automatic reply on an offline form's conversation is written to the conversation
>   only, never emailed.
> - A reply's `opens` is given in the topic's time zone, and its template sees the four
>   values alone. A rendered reply is bounded at 4,000 characters, as a message's text
>   is. One that fails to render, or renders past the bound, writes nothing and is
>   logged, and its rule counts as fired.
> - A rule's `Topics` naming a topic not configured, `After` on a rule that is not
>   `waiting`, a tag name outside the name rule, and a 51st tag each fail the start with
>   `messaging.startup.configurationinvalid`.
> - The release notes state the `TemplateKey` warning among what the deployment meets
>   (`06` MSG-OPS-007).
> - `08` section 3 gives `snoozed` its meaning.
> - A topic's replies, for a person outside its scope, answer 404
>   `messaging.reply.notfound` on every replies route, never 403.
> - `tags` and `snoozedUntil` are on staff bodies only: the public routes and hub never
>   answer them.
>
> Propagated to `04` MSG-LIB-002; `05` MSG-API-009, MSG-API-010; `06` MSG-OPS-004,
> MSG-OPS-007; `08` sections 1, 3 and 4; `09` MSG-CONV-003, MSG-CONV-005;
> `implementation-plan.md` (phase 17).

> **Amended.** 2026-11-01, on the review of M-036's propagation:
> - A customer's language, like a visitor's, is read from `Accept-Language` and matched,
>   since the engine holds no person's language. This replaces the reading through `01`
>   MSG-SEND-003 in the note above.
>
> Propagated to `09` MSG-CONV-003; `implementation-plan.md` (phase 17).

---

## M-036 — Phase 18: the end of support

**Date:** 2026-11-01 · **Status:** accepted · **Amends:** MSG-CONV-004, MSG-CONV-005

**TL;DR.**
- A closed support conversation offers its transcript as plain text: to download, or by
  email as a send of a host-declared kind.
- It also offers a survey: resolved or not, a rating of 1 to 5, and an optional comment,
  answered once within seven days.
- Supervisors are the members of a scope each topic names. They see the topic's live
  conversations and its reports, computed when read.

This entry settles the map's C24 to C27, C32 and C33.

- **The transcript (C24).**
  - It is rendered from the conversation's messages, every type but `note`, oldest
    first, in the conversation's language (M-035).
  - It is rendered through a page template, `transcript`, with the library's own in each
    declared language (`03` MSG-TPL-006). A host source may answer it instead.
  - The values are the topic, the instants opened and closed, and each message's author
    kind, instant and text. Staff are named only as `staff`, never by reference.
  - It is plain text, UTF-8, at most 1 MB. Past that, the oldest messages are left out,
    and the transcript says so.
  - `GET {public}/support/conversations/{id}/transcript` serves it to the visitor (by
    cookie) or the customer, as an attachment, for a closed conversation only. Otherwise
    it answers 409 `messaging.support.notclosed`.
- **By email (C25).**
  - `POST {public}/support/conversations/{id}/transcript/email` sends it as the kind
    `Hermes:Conversations:Support:TranscriptKind`. The kind is required where topics are
    configured, and checked at start like `AwayKind`.
  - The send goes to a visitor's given `email`, as `Recipient.Anonymous`, or to the
    customer as their person. The kind's values are `topic`, `closedAt` and `transcript`,
    the rendered text.
  - At most 3 sends per conversation. The fourth answers 429 `messaging.support.limited`.
  - Every check of `01` applies to the send.
- **The survey (C26, C27).**
  - `POST {public}/support/conversations/{id}/survey` takes `resolved` (a boolean),
    `rating` (1 to 5) and an optional `comment` (up to 1,000 characters).
  - The survey is answered once: a second answers 409 `messaging.support.surveyanswered`.
    It is answered within 7 days of the close: after that it answers 409
    `messaging.support.surveyclosed`.
  - It rates the staff member assigned when the conversation closed. A conversation never
    assigned keeps its survey with no staff member.
  - The comment is a protected value of the conversation, under the same key as its
    messages (`conversations:<id>:surveycomment`). It is kept and erased with the
    conversation.
- **Supervisors (C32).**
  - Each topic gains `Supervisors`, a membership scope (kind and reference, as `Scope`).
    Its members supervise the topic, through `IsMemberAsync` on every call.
  - A supervisor reads every conversation of the topic, notes included, as its staff do.
  - `GET {staff}/support/live?topic=` answers the topic's open and snoozed conversations,
    each with its status, assignee, place and minutes waited. It is over HTTP: the client
    reads it again.
  - A person who supervises no topic named answers 404 `messaging.conversation.notfound`.
- **Reports (C33).**
  - `GET {staff}/support/reports?topic=&from=&to=` is for the topic's supervisors. The
    period is whole days in the topic's time zone, at most 92.
  - It answers, per staff member and for the topic as a whole:
    - conversations closed;
    - the median and 90th percentile of waiting time (queued to assigned) and of handling
      time (assigned to closed);
    - surveys answered, the share resolved, and the mean rating.
  - Everything is computed when read, from the conversations' rows. Nothing is
    aggregated or stored.
  - Two counters join `08` section 8: `hermes.conversations.started` and
    `hermes.conversations.closed`, each tagged `topic`.
- **Phase 18's criteria.**
  1. The transcript holds every message but notes, in the conversation's language, names
     no staff member, and is served only for a closed conversation, only to its visitor
     or customer.
  2. Emailing it sends the declared kind, at most three times.
  3. The survey is taken once, within seven days, and its comment is protected.
  4. Only a supervisor of the topic reads its live view and reports. A supervisor reads
     notes.
  5. Reports give the medians, the 90th percentiles, the survey figures per staff member
     and topic, over a period of at most 92 days.

**Propagated to:**
- `03` MSG-TPL-006 (the page `transcript`);
- `05` (the routes);
- `08` sections 1, 4 and 8;
- `09` MSG-CONV-004, MSG-CONV-005, with their criteria.

> **Amended.** 2026-11-01, on the review of its propagation:
> - The visitor and the customer alike give `email` for the transcript email, since the
>   engine holds no person's address. A customer's send is `Recipient.Person` with that
>   `Email`.
> - The transcript is at most 1 MiB in UTF-8. The value given to the kind is bounded at
>   half of `Hermes:Email:MaxBytes`, the oldest messages left out, so that the email
>   fits. The transcript's render runs under ten times `Templates:MaxSteps`, and a
>   render past either bound renders again with the oldest messages left out.
> - `POST {public}/support/conversations/{id}/transcript/email` takes `{ "email" }` and
>   answers 202; 400 `messaging.request.malformed`, `member` `email`, or the failure
>   code of a send the engine refuses; 404; 409 `messaging.support.notclosed`. A fourth
>   email answers 409 `messaging.support.transcriptlimit`, not 429. Each send's
>   idempotency key is `conversation:<id>:transcript:<n>`, `n` from 1 to 3. The download
>   is named `transcript-<id>.txt`.
> - The survey answers 204; 400 `messaging.support.surveyinvalid`, `field` `resolved`,
>   `rating` or `comment`; 404; 409 `messaging.support.notclosed`, `surveyanswered` or
>   `surveyclosed`. It is kept in the columns `survey_at`, `survey_resolved`,
>   `survey_rating`, `survey_comment` (protected) and `survey_person` of
>   `conversations`. Erasing the staff member rated handles `survey_person` as a
>   delivery's person reference: the survey stays, and counts for the topic alone.
> - `GET {staff}/support/live` answers `{ "conversations": [ <live> ] }`, `<live>` being
>   `{ "id", "status", "assignee", "place", "waited" }`, `waited` in whole minutes since
>   it was queued and `place` `null` once assigned; a topic not configured 400
>   `messaging.request.malformed`, `member` `topic`; a non-supervisor 404
>   `messaging.conversation.notfound`; 503.
> - `GET {staff}/support/reports` answers
>   `{ "topic": <figures>, "staff": [ <figures> ] }`, `<figures>` being the members
>   `person`, `closed`, `waitMedian`, `waitP90`, `handleMedian`, `handleP90`, `surveys`,
>   `resolvedShare` and `meanRating`, times in seconds and `null` with no data. `from`
>   and `to` are `yyyy-MM-dd`, both included; a period that does not read or passes 92
>   days answers 400 `messaging.request.malformed`, `member` `from` or `to`; a
>   non-supervisor 404; 503.
>   The figures cover the conversations closed within the period, each counted for its
>   assignee when it closed, the percentiles as `percentile_cont` computes them.
> - `IConversations` gains `LiveAsync` and `ReportAsync`, with the records
>   `LiveConversation` and `ReportFigures`.
> - `Supervisors` is optional. Unset, nobody supervises the topic, and its live view and
>   reports answer 404 to everyone.
> - A supervisor only reads, through the conversation and messages routes. A write by a
>   supervisor who is not the assignee answers as for any other staff member who is not
>   assigned. Supervisors receive the staff hub's pushes for every conversation of the
>   topic, notes included. The topic's conversations are not in their list of
>   conversations.
> - The page `transcript` has one part, `text`, and the library ships it in `en` and
>   `ar`, as its other pages. Its values are `topic`, `openedAt`, `closedAt`,
>   `language`, `direction`, `messages` (a list of `author`, `sentAt` and `text`,
>   `author` being `visitor`, `staff` or `library`) and `omitted`, the number of
>   messages left out.
> - Where topics are configured, a `transcript` part missing for a declared language
>   fails the start with `messaging.startup.declarationinvalid`, `details.declaration`
>   `transcript`.
> - `surveycomment` joins `01` MSG-SEND-018's context fields. A topic's `Supervisors`
>   joins its keys in `09` MSG-CONV-002, a supervisor's reads join `09` MSG-CONV-006's
>   access, and the survey's comment joins `09` MSG-CONV-009's protection.
> - `hermes.conversations.started` counts every support conversation, offline ones
>   included, when it is created; `hermes.conversations.closed` counts each close, by
>   staff, by the visitor or automatically.
>
> Propagated to `01` MSG-SEND-018; `03` MSG-TPL-004, MSG-TPL-006, MSG-TPL-007; `04`
> MSG-LIB-002; `05` MSG-API-009, MSG-API-010; `08` sections 1, 4 and 8; `09`
> MSG-CONV-002, MSG-CONV-004, MSG-CONV-005, MSG-CONV-006, MSG-CONV-009;
> `implementation-plan.md` (phase 18).

---

# Index

| Title | Entry |
|---|---|
| A product-agnostic sending library, designed for conversations | M-001 |
| The libraries the channels and templates use | M-002 |
| Bridges to the Janus identity library | M-003 |
| What the first release is designed for but does not build | M-004 |
| How a host sends | M-005 |
| Preferences, consent, suppressions, quiet hours, limits | M-006 |
| Content protection, retention, erasure and keys | M-007 |
| SMS through WE | M-008 |
| Browser push | M-009 |
| Email | M-010 |
| Conversations | M-011 |
| How the library is built | M-012 |
| Data access, hosting abstractions and test packages | M-013 |
| The implementer pushes its own work and takes the owner's documents itself | M-014 |
| Phase 0's question and review: exact versions, and the gates' gaps | M-015 |
| Phase 1's questions: lengths, the template folder, decimal factors, roles | M-016 |
| Phase 2's question: MailKit 4.18.2 | M-017 |
| The full gate runs in the pipeline, not on the implementer's machine | M-018 |
| Phase 5's report: the endpoint contract placed, the sample host first, the gates paragraph brought to M-018 | M-019 |
| A standalone library: the Janus bridges come out, and the release does not wait for WE | M-020 |
| Phase 6's question: an empty `PublicAPI.Shipped.txt` leaves with its project | M-021 |
| The library ships no SMS gateway, and `Hermes` is the final name | M-022 |
| The release step: the release commit, its checks, and the release job | M-023 |
| The development inbox | M-024 |
| Phase 9's question: the release set reads the development inbox too | M-025 |
| The in-application list | M-026 |
| Release candidates until the final 1.0.0 | M-027 |
| A delivery notice carries its send's idempotency key | M-028 |
| Phase 12's questions: the notice's key, kept as content | M-029 |
| Phase 12's second questions: the key, one of a delivery's values | M-030 |
| Release 2: its phases, and where conversations live | M-031 |
| Phase 14: the model and staff chats | M-032 |
| Phase 15: the live connection | M-033 |
| Phase 16: support's public side | M-034 |
| Phase 17: automatic replies and desk tools | M-035 |
| Phase 18: the end of support | M-036 |
