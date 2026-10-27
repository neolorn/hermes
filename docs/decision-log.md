# Decision log

Why each choice of the specification was made and what was rejected. Supplementary: the
chapters are complete without it. An entry that changes an earlier one carries
`> **Amended.**` under the earlier entry's heading.

---

## M-001 — A product-agnostic sending library, designed for conversations

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
