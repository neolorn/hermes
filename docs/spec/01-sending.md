# 01 — Sending

What a host declares, how it asks for a message to be sent, and what the library does
between that call and the provider: the checks a delivery passes, how it is dispatched
and retried, the statuses it ends in, and how long its data is kept. Every value named
`Hermes:...` is configuration (`08` section 4).

---

## 1. Declarations

**MSG-SEND-001** — A message SHALL be sent only as one of the **kinds** the host
declared at startup. A kind declares its name, its category, the channels it may go out
on and, for each channel, the channel's options (`02`); its templates come from the
template source (`03`).

**Values (M-005, M-026).** The host declares kinds in `AddHermes` (`04` MSG-LIB-003)
through `DeclarationBuilder.Kind(name, category)`, then `Email(...)`, `Sms(...)`,
`Push(...)` and `InApp(...)` for each channel the kind may use (`04` MSG-LIB-002 gives
their parameters). A kind's name and a category's name are 1 to 64 lower-case letters
and digits separated by single `.`, `-` or `_`. A kind MAY declare `ExpiresAfter`, a
duration after which a delivery not yet sent is not sent at all (MSG-SEND-015). A kind
the host no longer sends is declared retired, `DeclarationBuilder.Retire(name)`, rather
than dropped: a send of it is a fault, as of an undeclared kind, and each of its
deliveries not yet final ends `cancelled` at check 1 when it is next claimed
(MSG-SEND-006), so the observer is told and retention runs as for any other; a delivery
of a kind its application neither declares nor retires is deleted by the retention job,
logged by count, once `Hermes:Retention:Record` has passed since the later of its
recording and its due instant (MSG-SEND-019). Every application of a deployment that
dispatches (`Hermes:Dispatch:Enabled` on), runs the retention job, maps the unsubscribe
or preferences group (`05` MSG-API-003, MSG-API-005), or calls `IPreferences`,
`ISuppressions` or `ISubjects`, declares, or retires, every kind of the deployment,
since each reads a kind's category and a category's rules from its own declarations
(MSG-SEND-007). Startup fails with `messaging.startup.declarationinvalid`,
`details.declaration` naming the kind and `details.field` the member, for:
- a name outside the rule, or declared twice, or both declared and retired;
- a category not declared;
- a kind with no channel;
- a non-positive `ExpiresAfter`;
- an email `from` or `replyTo` that is not an address (`02` MSG-EMAIL-002);
- an SMS `maxSegments` below 1 or above 10, or a `sender` outside the provider's rule
  (`02` MSG-SMS-002);
- a push `ttl` below 1 second or above 28 days;
- an in-app target or target value outside its rule, or target values without a target
  (`02` MSG-INAPP-001);
- any template failure of `03` MSG-TPL-004.

*Source: M-005*

A closed set of kinds is what lets every template be checked before the first send, and
gives every limit, preference and report a stable name to attach to.

**Acceptance criteria**
1. Sending a kind the host did not declare, or retired, is a fault of the calling code
   (`07` MSG-ENG-003), and nothing is recorded.
2. Each condition listed above fails startup with `messaging.startup.declarationinvalid`
   naming the kind and the member.
3. A kind retired while three of its deliveries wait, due, ends them `cancelled` at the
   next pass of their channel, and the observer is told; one dropped instead leaves its
   deliveries waiting until the retention job deletes them after
   `Hermes:Retention:Record`.

---

**MSG-SEND-002** — Every kind SHALL belong to one **category**, which the host declares
and which carries the rules every kind in it shares: whether a person may turn it off,
whether it needs consent, whether quiet hours apply, its limits, and its labels.

**Values (M-005, M-006, M-026).** `DeclarationBuilder.Category(name)` declares:
- `Essential()`: the person cannot turn the category off (MSG-SEND-010), no suppression
  applies to it (MSG-SEND-012), quiet hours never apply to it, and its SMS goes out
  below the balance floor (`02` MSG-SMS-006). Without it the category is **optional**:
  the person may turn it off per channel, and its email carries an unsubscribe link
  (`02` MSG-EMAIL-006);
- `RequiresConsent(purpose)`, only on an optional category: each delivery needs the
  recipient's consent to `purpose` at the moment it is dispatched (MSG-SEND-011);
  `purpose` follows the name rule of MSG-SEND-001;
- `QuietHours(bool)`, only on an optional category: whether quiet hours apply
  (MSG-SEND-013); on by default where the category requires consent, off otherwise;
- `Limit(channel, count, window)`: a cap per destination, or per person for push and
  in-app (MSG-SEND-014); none by default; a category MAY declare several for one
  channel, each with its own window;
- `DeploymentLimit(channel, count, window)`: a cap across every destination
  (MSG-SEND-014); none by default; several MAY be declared likewise;
- `Label(language, text)`: the category's name as people read it, in each declared
  language, used by the library's own pages (`05` MSG-API-005) and nowhere else;
  required for an optional category that has an email kind.

Startup fails with `messaging.startup.declarationinvalid`, `details.declaration` the
category, for: an essential category with a consent purpose or quiet hours; a limit
whose count is below 1, whose window is below 1 minute or above
`Hermes:Retention:Places`, or whose channel no kind of the category uses; two `Limit`s,
or two `DeploymentLimit`s, on one channel with the same window; a label missing for a
declared language where one is required, or longer than 128 characters.

*Source: M-005, M-006*

**Acceptance criteria**
1. A kind's deliveries follow its category's rules and no others.
2. Each condition listed above fails startup naming the category.

---

## 2. The send

**MSG-SEND-003** — A host SHALL send through one operation, `IMessages.SendAsync`, which
records the send and its deliveries and returns; nothing is handed to a provider inside
the call.

**Values (M-005, M-026).** The `Send` record (`04` MSG-LIB-002) carries:
- `Kind`: a declared kind's name;
- `Recipient`: `new Recipient.Person(reference)` for a person the host identifies (a
  reference of 1 to 128 characters, the same for every message to that person), or
  `new Recipient.Anonymous()` for someone the host has no reference for;
- `Email`, `Phone`: the destination for each of those channels the send uses: an address
  of RFC 5321's mailbox form, at most 254 characters, whose domain MAY be given in
  Unicode and is then turned into its ASCII form (IDNA), the form that is sent and
  hashed, a local part outside ASCII being refused; a phone number in E.164;
- `Language`: a BCP 47 tag, matched to a declared language (`03` MSG-TPL-003) ignoring
  case, and failing that by its primary language subtag alone (`ar-EG` to a declared
  `ar`), the send's language then being the declared one it matched; one that matches
  none, or none, is replaced by `Hermes:DefaultLanguage`;
- `TimeZone`: an IANA time zone identifier; one that does not resolve, or none, is
  replaced by `Hermes:TimeZone`;
- `Values`: the template's values (`03` MSG-TPL-002);
- `Channels`: the channels this send uses, a subset of the kind's; absent, every channel
  the kind declares for which the send has a destination, push and in-app included for a
  person;
- `NotBefore`: an instant before which nothing is dispatched; absent, now;
- `IdempotencyKey`: optional, 1 to 128 printable ASCII characters (MSG-SEND-009);
- `PushTopic`: optional, 1 to 128 characters (`02` MSG-PUSH-002).

A push delivery needs no destination: it goes to the person's subscriptions as they
stand when it is dispatched (`02` MSG-PUSH-003); nor does an in-app delivery, which
writes to the person's list in every declared language (`02` MSG-INAPP-001,
MSG-INAPP-002). The send is refused, nothing recorded, with a failure (`08` section 1):
- `messaging.send.nodestination`, `details.channel`, where a channel the send names has
  no destination, push and in-app for an anonymous recipient included, and without
  `details.channel` where the send is left with no channel at all;
- `messaging.send.destinationinvalid`, `details.channel`, where a destination does not
  read;
- `messaging.send.destinationnotallowed`, `details.channel` `sms`, where a phone number
  starts with none of `Hermes:Sms:AllowedPrefixes` (`02` MSG-SMS-002);
- `messaging.send.toolong`, `details.channel`, where a rendered message exceeds its
  channel's limit (`02` MSG-EMAIL-003, MSG-SMS-001, MSG-PUSH-002, MSG-INAPP-002; `03`
  MSG-TPL-007);
- `messaging.send.renderlimit`, `details.channel`, where a render passes its steps (`03`
  MSG-TPL-007);
- `messaging.send.renderfailed`, `details.channel`, where a filter cannot compute from
  the send's values (a division by zero, an overflow, an argument the filter cannot
  take), or a member is missing from data the send passes, or an assigned name, a loop
  variable or `forloop` is read where it was not set (`03` MSG-TPL-002, MSG-TPL-007);
- `messaging.send.idempotencyconflict` (MSG-SEND-009);
- `messaging.key.unavailable`, `details.key`, where the send needs a key version this
  process does not hold, or one past its use (`06` MSG-OPS-008).

These are faults of the calling code (`07` MSG-ENG-003): a kind not declared; a channel
the kind does not declare; a value a template uses that the send does not pass; a value
misnamed, named twice or under a reserved name (`03` MSG-TPL-002); a person reference,
`IdempotencyKey` or `PushTopic` outside its bounds. The receipt (`SendReceipt`) carries
the send's identifier and one delivery identifier per channel. `SendAsync` writes
nothing in the host's transaction but the send's own rows: the send, its deliveries and,
where it has a key, its idempotency row, deleting a lapsed one of the same kind and key
(MSG-SEND-009); the send's row holds its identifier, its kind and its instant, nothing
of the recipient. For a person, the send takes the person's locks of `02` MSG-PUSH-003
in shared mode, right after the idempotency key's lock where it has one and before it
reads the key's row, so an erasure, which takes them exclusively, is never passed by a
send it cannot see (MSG-SEND-020). `IMessages` is a scoped service, and resolves the
content protector from the scope it was called in, for each call; an exception the
protector throws propagates to the caller, and the send records nothing.

*Source: M-003, M-005, M-007*

**Acceptance criteria**
1. `SendAsync` returns before any provider is called; a provider is called only by the
   dispatcher (MSG-SEND-007).
2. Each refusal above records nothing and names the channel where it has one; each fault
   records nothing.
3. A send to a person with no channel named sends on every declared channel it has a
   destination for, push and in-app included.
4. A send naming a language that matches no declared one is rendered in the default
   language, and one naming `ar-EG` where `ar` is declared is rendered in `ar`.

---

**MSG-SEND-004** — A send SHALL be recorded in the host's transaction where the host
gives one, so that it commits or rolls back with the host's own write, and SHALL wake
the dispatchers when it commits.

**Values (M-005).**
`SendAsync(Send send, DbTransaction transaction, CancellationToken cancellationToken)`
records the send on the transaction's connection, inside it; the library commits
nothing. The transaction SHALL be an open `NpgsqlTransaction` whose connection's
database holds every migration of the library's release (`06` MSG-OPS-002), checked once
per connection string; anything else is a fault of the calling code. The library uses no
retrying execution strategy on a host's transaction. The send's advisory locks (the
idempotency key's, and the person's, one per `hashKeys` version held) are held until the
host's transaction ends, each taking a slot of PostgreSQL's shared lock table until
then, so a host records a large batch in transactions of bounded size (`06`
MSG-OPS-007); a concurrent send with the same idempotency key (MSG-SEND-009), or the
host's own locks taken in another order, can raise PostgreSQL's deadlock (40P01) at any
isolation, and under `REPEATABLE READ` or `SERIALIZABLE` its serialization failure
(40001) or a unique violation (23505), which the host retries as it retries any. Since
the person's lock is held until then, a host's transaction that has sent for a person
does not call `ISubjects.EraseAsync` for that person before it ends: the erasure would
wait for it (MSG-SEND-020). Without a transaction, the call records the send in a
transaction of its own.

With `Hermes:Dispatch:Notify` on (the default), the send runs
`pg_notify('messaging_dispatch', '')` inside its transaction, which PostgreSQL delivers
only when it commits, and every dispatcher listens (MSG-SEND-007). PostgreSQL commits
the transactions that notified one at a time, across the whole server, so a deployment
whose commit rate that would limit turns the notification off and relies on
`Hermes:Dispatch:Interval` alone.

*Source: M-005*

An order confirmed and its confirmation message are one fact: neither should exist
without the other.

**Acceptance criteria**
1. A send recorded in a host transaction that rolls back leaves no send and no delivery,
   and wakes no dispatcher.
2. With the notification on, a send recorded in a host transaction that commits is
   dispatched without waiting for `Hermes:Dispatch:Interval`; before the commit the
   dispatcher does not see it.

---

**MSG-SEND-005** — A send SHALL become **one delivery per channel**, each dispatched,
retried, refused and reported on its own.

**Values (M-005, M-026).** An email or SMS delivery carries its destination. A push
delivery carries the person and is sent, when dispatched, to each of the person's live
subscriptions, under the outcome rules of `02` MSG-PUSH-005. An in-app delivery carries
the person and its rendered parts, which its dispatch moves into the person's entry
(`02` MSG-INAPP-001). The deliveries of one send share the send's identifier. Each
attempt that calls its provider draws its own delivery reference (`00` glossary),
recorded on the attempt when its claim is renewed, before the provider is called
(MSG-SEND-007).

*Source: M-005*

**Acceptance criteria**
1. A send on three channels yields three deliveries with their own statuses, and every
   attempt that calls its provider carries a reference of its own.

---

## 3. Before a delivery goes out

**MSG-SEND-006** — Every delivery SHALL pass, when the dispatcher takes it and before
any provider is called, these checks in this order; the first that fails ends the
delivery in the status it names, or holds it where it says so, and nothing is sent.

| # | Check | Fails with |
|---|---|---|
| 1 | Not cancelled, not of a retired kind (MSG-SEND-001), the recipient not erased, and its values open (MSG-SEND-008, MSG-SEND-018, MSG-SEND-020) | `cancelled`; `failed` for a value that does not open under its binding; held for a value that cannot open yet |
| 2 | Not past the kind's `ExpiresAfter` from the send (MSG-SEND-015) | `expired` |
| 3 | For email and SMS, the destination is not suppressed for the category (MSG-SEND-012) | `suppressed` |
| 4 | The person has not turned the category off on this channel (MSG-SEND-010) | `optedout` |
| 5 | Outside the category's quiet hours, where they apply (MSG-SEND-013) | held until they end |
| 6 | Within the provider's rate: its claim took its place in the rate counter (MSG-SEND-014) | never fails, the place being taken with the claim |
| 7 | The recipient consents to the category's purpose, where it requires one (MSG-SEND-011) | `noconsent`; a source that cannot answer is a transient answer |
| 8 | Within the category's limits (MSG-SEND-014) | `limited` |
| 9 | The channel's own check: for SMS, the number still within `Hermes:Sms:AllowedPrefixes` (`02` MSG-SMS-002) and the balance floor (`02` MSG-SMS-006); for push, a live subscription, every live subscription's values open and its VAPID version held (`02` MSG-PUSH-003, MSG-PUSH-005) | `failed` (reason `destinationnotallowed`); `balancefloor`; `nosubscription`; held for a key |

Checks 3, 4, 5 and 7 do not apply to an essential category. Checks 3, 5 and 6 never
apply to an in-app delivery, which has no destination, interrupts nobody and has no
provider rate, and check 4 does not where `Hermes:InApp:AlwaysOn` is on (`02`
MSG-INAPP-001, MSG-INAPP-004). Each check is made against the state at that moment,
never the state at the send: a consent withdrawn or a preference turned off after the
send stops the delivery. A check that looks up a keyed hash (3, 8) computes it under
every `hashKeys` version this process holds, looks it up, then reads the key versions in
use, in the same transaction and never from a cache (`06` MSG-OPS-008); where a version
in use is one this process does not hold, the delivery is held for a key, never taken as
"not suppressed" or "under the limit".

**Held for a key.** Where a value cannot open yet (its protector answers
`KeyUnavailable`, throws or does not answer within `Hermes:Dispatch:Timeout`, its
envelope names a protector this application does not register, or its format byte is one
kept for a later release, MSG-SEND-018), or a lookup or a push request needs a version
this process does not hold (`06` MSG-OPS-008), the delivery returns to `waiting` without
counting an attempt, due after `Hermes:Retry:Initial`, with the reason `keyunavailable`
recorded, and the `key-unavailable` alert is raised (`06` MSG-OPS-006). A message is
never lost because one process lacks a key another holds. A run of holds is bounded: the
delivery records the instant of its first hold since its last counted attempt, cleared
when an attempt is counted, and a hold `Hermes:Dispatch:MaxHold` (default 7 days) or
more after that instant ends the delivery `failed` instead, with the reason
`keyunavailable`, so no delivery waits for ever for a key no process will hold. Its
expiry comes first: a hold whose next attempt would fall after the kind's expiry ends it
`expired` (MSG-SEND-015).

*Source: M-005, M-006, M-007*

**Acceptance criteria**
1. Each check, failing, ends or holds the delivery as named and calls no provider.
2. A preference turned off between the send and its dispatch stops the delivery.
3. An essential category's delivery passes checks 3, 4, 5 and 7 whatever the recipient's
   preferences, suppressions, consents and time of day.
4. A process that does not hold `hashKeys` version 2 never dispatches to a destination
   suppressed under version 2, and a delivery it holds for a key is sent by a process
   that holds it, however many passes it was held.
5. A delivery not claimed for want of a place in the rate does not ask the consent
   source, nor open its values.
6. A delivery held for a key with no attempt counted for `Hermes:Dispatch:MaxHold` ends
   `failed` with the reason `keyunavailable` at its next hold, and a send whose
   `NotBefore` lies beyond `MaxHold` is held, not failed, at its first.

---

**MSG-SEND-007** — Dispatchers SHALL take due deliveries, run the checks, call the
channel's provider and record the answer, so that several dispatchers, in one process or
several, never make one attempt twice.

**Values (M-005, M-012, M-026).** Every instant is the library's `TimeProvider`'s,
passed to each statement as `@now` (`07` MSG-ENG-005). A delivery is **due** when it is
`waiting` and its due instant has passed, or `inflight` and its claim has expired. A
claim records its holder, its instant and the instant it expires, which the holder sets,
when it claims, extends or renews, to `@now` plus its own
`Hermes:Dispatch:ClaimTimeout`, so a claim is judged by the settings of the application
that holds it, whatever those of one that would take it over. A **pass** is of one
channel: it draws a holder of its own and claims due deliveries of its channel, of the
kinds its application declares or retires (MSG-SEND-001) and the reasons its release
knows, in one transaction: it locks its channel's rate counter row for the current
second of `@now` (MSG-SEND-014, made at zero where there is none), claims the kinds of
essential categories in one statement and then the others and the retired ones together
in a second, and adds what it claimed to the row's count:

```sql
WITH due AS MATERIALIZED (
  SELECT id FROM messaging.deliveries
  WHERE kind = ANY(@kinds) AND channel = @channel
    AND (reason IS NULL OR reason = ANY(@reasons))
    AND ((status = 'waiting' AND due_at <= @now)
      OR (status = 'inflight' AND claim_expires_at <= @now))
  ORDER BY due_at LIMIT @limit
  FOR UPDATE SKIP LOCKED)
UPDATE messaging.deliveries d
SET status = 'inflight', claimed_by = @holder, claimed_at = @now,
    claim_expires_at = @now + @claimTimeout
FROM due WHERE d.id = due.id
RETURNING ...
```

with `@limit`, in the first statement, the lesser of `Hermes:Dispatch:BatchSize` and
what the row's count leaves of the channel's rate, and in the second that lesser less
what the first took, and `@claimTimeout` this application's
`Hermes:Dispatch:ClaimTimeout`; so a delivery is claimed only with its place in the
provider's rate, no pass of any process claims past the rate, an essential delivery
never waits behind an optional one of its channel, and a backlog over the rate waits
unclaimed and unopened. A pass of the in-app channel, which has no rate, locks no
counter row, and its `@limit` is `Hermes:Dispatch:BatchSize` alone. A delivery that
checks 1 to 5 end or hold, or that a pass hands back, gives its place back to the row it
was taken from (`count = count - 1`). A delivery also records the instant it became due:
its due instant when recorded, its next due instant after a counted attempt, and the due
instant a hold for quiet hours gives it; a hold for a key or for a lock that timed out,
and a hand-back, leave it, so `dispatch-lag` (`06` MSG-OPS-005), measured from it, shows
a backlog that waits on the rate, a key or a lock. A row whose channel, status or reason
this release does not know, written by a newer release running beside it (`06`
MSG-OPS-002), is left as it is: no pass or job of this release claims or moves it, the
observer is not told of it, neither `dispatch-lag`, the health check nor the gauge of
`06` MSG-OPS-005 counts it, and `ReadAsync` leaves it out (erasure and export, while
such a row can exist, answer `messaging.release.newer`, MSG-SEND-020); the retention job
deletes it once `Hermes:Retention:Record` has passed since the later of its recording
and its due instant, logging how many it deleted, so a row a rolled-back release left
holds nothing for longer. An attempt or a call whose class or reason, or a notice whose
status or reason, this release does not know, inside a delivery it does know, is left
out of `ReadAsync` and is not delivered by this release's `observer` job. A value in an
envelope format kept for a later release is met at check 1 (MSG-SEND-006). A row of a
kind no dispatching application declares or retires stays `waiting` and shows in
`dispatch-lag` until the retention job deletes it (MSG-SEND-001), so a kind dropped
where it should have been retired is seen.

A claimed delivery whose last counted attempt has no answer, whatever its status when
claimed, had a holder that stopped before recording it: the attempt is recorded as
`transient` with the reason `claimlost` (for SMS under the locks of `02` MSG-SMS-004,
applying any report kept on it); the delivery then ends `failed` with that reason where
its attempts are used up, and otherwise returns to `waiting`, due as MSG-SEND-016 gives,
the pass going on to its next delivery; save a push delivery, which ends as `02`
MSG-PUSH-005 gives (one accepted in any attempt ends it `sent`, the lost attempt keeping
`claimlost`).

A pass the host stops (the hosted service's stopping token, or the token a
`RunPassAsync` caller cancels) claims nothing more and begins no renewal, so no provider
call but that of a renewal already begun, and at once returns each claimed delivery
whose renewal it has not begun, the one whose check waits on a seam included, to
`waiting`, due at once, by an update conditional on its claim that counts no attempt, so
a rolling deployment never leaves a batch waiting for its claims to expire. It then lets
each call in progress, and the call of a renewal it had begun, finish within one
`Hermes:Dispatch:Timeout` counted from the stop, which its remaining row waits share
(each `lock_timeout` set to what is left, in whole milliseconds rounded up), a call
still running at that deadline being cancelled: a provider call's answer is recorded as
any other where time is left for its outcome, one left none recording nothing, so its
attempt is recorded `claimlost` when the delivery is next claimed, and a seam call's
answer changes nothing, its delivery being handed back already; a push delivery that has
made a call of its attempt makes no further call, one whose renewal it had begun
included, and its attempt is recorded `claimlost`, and it ends or waits as `02`
MSG-PUSH-005 gives (`06` MSG-OPS-007 names the host's shutdown timeout this needs).
`RunPassAsync` then throws `OperationCanceledException`, as it does at once, claiming
nothing, for a token already cancelled when it is called.

For each claimed delivery, in order, the pass runs the checks of MSG-SEND-006; before
each check and each seam call it makes for the delivery, a pass whose claim on it is
older than half of `Hermes:Dispatch:Timeout` first **extends** the claim, setting its
instant and its expiry by one update conditional on the delivery still being `inflight`
under its own claim not yet expired, counting nothing, and skips the delivery where that
update changes nothing, so the claim's age while its pass works on it stays below twice
`Hermes:Dispatch:Timeout`, the margin `06` MSG-OPS-003 leaves for clocks, and checks
that last longer than the claim timeout are not taken over midway; then, immediately
before each provider call (for push, one call per subscription, `02` MSG-PUSH-005), it
**renews** the claim in one update conditional on the delivery still being `inflight`
under its own claim (its holder) not yet expired, setting its instant and its expiry,
which at the attempt's first call also counts the attempt and records its delivery
reference, and it skips the delivery where that update changes nothing; an SMS renewal
is a transaction that first locks the delivery's row (`SELECT ... FOR UPDATE`) and reads
the reports kept on it in a later statement, as an outcome does (`02` MSG-SMS-004), so a
report kept a moment before is never missed; a push renewal also checks its subscription
(`02` MSG-PUSH-005); an in-app delivery's provider call is the writing of its entry, in
one transaction with its outcome (`02` MSG-INAPP-001). Each call is bounded as a whole,
its answer read included, by `Hermes:Dispatch:Timeout`. The pass records the outcome by
one update conditional on the delivery still being `inflight` under its claim, which
changes nothing where the claim was taken over or the delivery was cancelled; every
update of a pass, at a check, a hold, an extension, a renewal or an outcome, carries
that condition, and every one but check 8's, which keeps its own timeout and hold
(MSG-SEND-014), and a stopping pass's, which take what is left of its timeout (above),
runs under a `lock_timeout` of `Hermes:Dispatch:Timeout`, the SMS renewal's row lock,
the claim's lock of the counter row and every give-back and hand-back included: a
timeout leaves the delivery as it is until its claim expires, and a claim whose lock
times out claims nothing. Every update that takes an SMS delivery out of `inflight`
takes the locks of `02` MSG-SMS-004 and applies the reports kept on it. A seam of the
host that throws during a pass or a job, or that does not answer within
`Hermes:Dispatch:Timeout` (each protector and alert sink call being bounded so, as the
consent source and the observer are), is logged by the exception's type alone, and the
pass or job goes on: a consent source's exception is a transient answer at check 7
(MSG-SEND-011), a content protector's a hold for a key (MSG-SEND-006), an alert sink's
ignored. A transient answer at check 7 is counted as an attempt by the same conditional
update that records it; a delivery held for a key or by quiet hours, or handed back, is
never counted. An attempt is counted before its call, so a holder that keeps failing
still uses up `Hermes:Retry:MaxAttempts`. A pass opens values and calls providers in a
service scope of its own.

The hosted dispatcher runs, in each process, passes of each channel its release knows,
at most `Hermes:Dispatch:MaxPasses` of one channel at once, each working its deliveries
one after another, so one channel's slow provider or seam never lengthens another
channel's turn. A pass of a channel starts when a notification of MSG-SEND-004 arrives
on the dispatcher's listening connection, every `Hermes:Dispatch:Interval` in any case,
and after a pass of the channel that took its whole limit, none of which it held for a
key: at the start of the next second, whether or not that pass still runs, and at once
where it ends with room left in the current second's row (its deliveries' places given
back); a trigger that finds `MaxPasses` passes of the channel running starts the next as
soon as one ends. A pass that has run for `Hermes:Dispatch:Interval` hands back, before
it begins its next delivery, every claimed delivery it has not yet reached, to
`waiting`, due at once, by an update conditional on its claim that counts no attempt, so
a slow seam, a held lock or a slow provider never keeps a new pass, essential kinds
first, from starting for longer than that interval and the work of the delivery each
pass is on. So passes of a channel overlap across seconds, the configured rates are
reached without cycling the backlog wherever `MaxPasses` passes of a process can carry
them, more processes carrying more, and load never multiplies passes, connections or
sessions past that bound. The listening connection is one dedicated session connection
per process, on the secret `listenConnection` where the deployment gives one and
`connection` otherwise (a transaction-mode connection pooler cannot carry `LISTEN`);
when it drops, the dispatcher connects again after 1 second, doubling the wait up to
`Hermes:Dispatch:Interval`, and runs a pass of each channel after each reconnection, the
interval covering the gap. The dispatcher is a hosted service, with its listening
connection, that the library registers where `Hermes:Dispatch:Enabled` and
`Hermes:Dispatch:Hosted` are both on, so a host runs it in some applications and not in
others; `IDispatcher.RunPassAsync` (`04` MSG-LIB-002) runs one pass of each channel its
release knows, side by side, answering the sum, whenever it is called in an application
with `Hermes:Dispatch:Enabled` on, whether or not the hosted dispatcher runs there, so a
host that drives its passes itself sets `Hermes:Dispatch:Hosted` off and keeps every
other part of dispatching (the shipped providers, the observer, the `observer` and
`balance` jobs, `06` MSG-OPS-004); called where `Hermes:Dispatch:Enabled` is off, it
throws `InvalidOperationException` before it claims anything, a fault of the calling
code; and under `AddHermesTesting` the hosted dispatcher and `observer` job do not run
(`04` MSG-LIB-008).

An attempt's answer (`04` MSG-LIB-002, `Attempt`) is one of:
- **accepted**: the provider took the message (`02` names what that is per channel): the
  delivery ends `sent`, with the provider reference where there is one;
- **transient**: a timeout, a refused or broken connection, or an answer `02` names as
  transient: the delivery waits for its next attempt (MSG-SEND-016);
- **permanent**: an answer `02` names as permanent: the delivery ends `failed`;
- **gone**, for push only: the subscription no longer exists (`02` MSG-PUSH-005).

An attempt records its class and reason (`08` sections 2 and 6), and each of its
provider calls the call's class, reason and the provider's numeric status where it gave
one, a push call naming its subscription's row and push service, never its endpoint. An
attempt answered at a check has no call. No text a provider answers is stored or logged,
since it can carry a destination.

*Source: M-005, M-008, M-009, M-012*

**Acceptance criteria**
1. Two dispatchers running at once over the same due deliveries call the provider once
   per delivery per attempt, and so do two passes of one process.
2. A delivery whose claim expired while an earlier delivery of the same pass was being
   sent is not sent by the first holder once another has taken it.
3. Each answer class leads to the status above, and no attempt record holds provider
   text.
4. With a test `TimeProvider` moved past a delivery's due instant, the next pass takes
   it, and not before.
5. A row whose channel or reason this release does not know, inserted by a test after it
   widens the column's constraint as a newer release's migration would, and a row of a
   kind the application neither declares nor retires, are never claimed or moved, and
   the first is deleted only once `Hermes:Retention:Record` has passed since the later
   of its recording and its due instant.
6. A pass stopped during the provider call of the second delivery of a batch of ten
   returns the other eight to `waiting` at once, due at once, with no attempt counted,
   then records the second delivery's answer; a pass whose token a `RunPassAsync` caller
   cancels does the same, and the call then throws `OperationCanceledException`; one
   stopped while the protector answers at check 1 returns that delivery too.
7. A delivery taken from a stopped holder whose last counted attempt has no answer, and
   handed back by a stopped pass before it is reached, records that attempt `transient`
   with the reason `claimlost` when it is next claimed; an SMS `delivered` report of
   that attempt arriving while it waits ends it `delivered`.
8. A push delivery whose pass is stopped during the first of its two calls makes no
   second call, and its attempt is recorded `claimlost`.
9. With a protector that answers each call in 15 seconds, the default call timeout and
   `Hermes:Dispatch:ClaimTimeout` 70 seconds, a second process running passes does not
   take over an email delivery of five protected values during its 75 seconds of checks,
   and the delivery is sent once.
10. With `Hermes:Sms:Rate` 5, `Hermes:Dispatch:MaxPasses` 4 and forty SMS sends notified
    within one second, the processes' passes claim at most five of them for that second
    and open no other's values, and `dispatch-lag` reads the last of them from when it
    became due.
11. With every email call taking `Hermes:Dispatch:Timeout` and 1,000 emails of an
    optional category due, a due SMS is sent by the next pass of SMS, and an essential
    email recorded after them is claimed before them.
12. With `Hermes:Dispatch:MaxPasses` 4, twenty notifications arriving while four passes
    of a channel run start one more pass of it as soon as one ends, and never a fifth
    beside them.
13. With the consent source of an optional category hanging and four passes of its
    channel working that category's deliveries, an essential delivery of the channel
    recorded after them is sent within `Hermes:Dispatch:Interval` and one
    `Hermes:Dispatch:Timeout`.

---

**MSG-SEND-008** — A host SHALL be able to cancel a send's deliveries that have not yet
been claimed.

**Values (M-005).** `IMessages.CancelAsync(SendId send, CancellationToken)` ends each of
the send's deliveries that is `waiting` as `cancelled`, and answers which it cancelled;
a delivery `inflight` or finished is left as it is. A send the library does not hold is
answered with `messaging.send.unknown`.

*Source: M-005*

**Acceptance criteria**
1. Cancelling a send with one delivery waiting and one sent cancels the first only.

---

**MSG-SEND-009** — A send carrying an idempotency key SHALL be recorded once: a second
send with the same key answers the first send's receipt and records nothing.

**Values (M-005, M-007).** The key's scope is the kind and the recipient: the person
reference, or for an anonymous recipient the keyed hashes of its destinations (the
email's, then the phone's, each as its 32 bytes, an absent one as 32 zero bytes). A key
row records the kind, the scope, the key, the receipt, the instant, the instant it
lapses, and the digest of the send: the keyed hash (`06` MSG-OPS-008, domain
`idempotency`) of the send's canonical form below, with the `hashKeys` version it was
made under; never the send itself. Inside the send's transaction, the send takes the
advisory lock of the first eight bytes, read as a big-endian signed 64-bit integer, of
the SHA-256 of `idempotency`, a zero byte, the kind, a zero byte and the key, then looks
for a row of that kind and key whose scope matches under any `hashKeys` version this
process holds (for a person, the reference itself), and reads the versions in use as a
lookup by hash does (MSG-SEND-006):
- none, or one whose lapse has passed, which it deletes: the send is new, and its row is
  written with its scope, its digest under the write version, and the instant it lapses,
  `@now` plus this application's `Hermes:Idempotency:Window` (default 24 hours), so a
  row is judged by the window of the application that wrote it;
- a live one: its digest is compared, in constant time, with the send's digest under the
  row's version; the same digest answers the first receipt, another is refused with
  `messaging.send.idempotencyconflict`.

A unique index over kind, scope and key backs the lock. Under `REPEATABLE READ`, two
sends for an anonymous recipient with one key, committed at once by processes writing
under different `hashKeys` versions, may both be recorded, since neither sees the
other's row and their scopes differ; a host that needs exactly one there sends under
`READ COMMITTED` or `SERIALIZABLE`. The retention job deletes rows whose lapse has
passed (MSG-SEND-019).

**The canonical form** is the bytes, in order: the format byte `0x01`; then each member
of the send as the host gave it, before any default is applied, in this order: `Kind`,
`Recipient`, `Email`, `Phone`, `Language`, `TimeZone`, `Values`, `Channels`,
`NotBefore`, `PushTopic` (`IdempotencyKey` is the key, not part of the form). A text
written "as text" is its UTF-8 length in 4 bytes, big-endian, then its UTF-8 bytes, with
no form byte. An absent member is the byte `0x00`; a present one is a byte naming its
form, then its bytes:
- a string (`0x01`): the string as text;
- the recipient: `0x02` and the reference as text for a person, `0x03` for anonymous;
- a whole number (`0x04`): 8 bytes, big-endian, two's complement;
- a decimal number (`0x05`): as text, its invariant-culture form with trailing zeros of
  its fraction, and a trailing point, removed (`1.50` as `1.5`, `2.0` as `2`);
- a boolean (`0x06`): one byte, `0x01` for true and `0x00` for false;
- an instant (`0x07`): its UTC ticks, 8 bytes, big-endian;
- a list (`0x08`): its count in 4 bytes, then each map's bytes without a form byte;
- a map (`0x09`), `Values` included: its count in 4 bytes, then each entry in ordinal
  order of its name, as the name as text and the value's form byte and bytes;
- `Channels` (`0x0A`): its count in 4 bytes, then each channel's `08` spelling as text,
  in ordinal order.

*Source: M-005, M-007*

**Acceptance criteria**
1. Two sends with one key inside the window yield one send and the same receipt, inside
   a host transaction too, and across a `hashKeys` write-version change under
   `READ COMMITTED`.
2. A second send with the key and another value is refused and records nothing; one with
   the same values written in another order is not.
3. A send with a key whose row's lapse has passed, not yet deleted, is recorded as a new
   send.
4. A row written by an application whose window is 7 days is still live on day 2 to
   another application whose window is 1 day, and the retention job leaves it until its
   own lapse.

---

## 4. The person's choices

**MSG-SEND-010** — A person SHALL be able to turn each optional category off, and back
on, for each channel its kinds use; an essential category SHALL NOT be turned off.

**Values (M-006, M-026).** Preferences are kept per person reference, category and
channel, on by default, and written under the person's locks of `02` MSG-PUSH-003 taken
shared, by `INSERT ... ON CONFLICT DO UPDATE`, so an erasure, which takes them
exclusively, never misses one (MSG-SEND-020), and a host's transaction that has sent for
the person does not block the write, save behind a registration or an erasure waiting
for it, each of which waits no longer than `Hermes:Dispatch:Timeout`; the write's own
transaction has that `lock_timeout` too, a timeout letting the database's error through
`SetAsync` and answered 503 `messaging.request.unavailable` by the route (`05`
MSG-API-003). `IPreferences.ReadAsync(person)` and
`IPreferences.SetAsync(person, category, channel, isOn)` serve the host; the endpoints
of `05` MSG-API-003 serve the person signed in. Setting a preference of an essential
category is refused with `messaging.preference.essential`; a category not declared, or a
channel the category's kinds do not use, with `messaging.preference.unknown`; where
`Hermes:InApp:AlwaysOn` is on, setting one of the in-app channel is refused with
`messaging.preference.fixed`, and `ReadAsync` leaves that channel out (`02`
MSG-INAPP-004). A preference changes only deliveries checked after it is set.

*Source: M-006, M-007*

**Acceptance criteria**
1. A category turned off on SMS stops the person's SMS deliveries of that category and
   none of its email deliveries.
2. Setting a preference of an essential category is refused and changes nothing.

---

**MSG-SEND-011** — A delivery of a category that requires consent SHALL be dispatched
only when the host's consent source answers, at that moment, that the recipient consents
to the category's purpose.

**Values (M-006, M-026).** The host registers an `IConsentSource` (`04` MSG-LIB-005);
`ConsentsAsync(ConsentQuery query, CancellationToken)` takes the recipient, the
destination the delivery would use (none for push and in-app), the purpose and the
channel, and answers `Given`, `NotGiven` or `Unavailable`. `NotGiven` ends the delivery
`noconsent`. `Unavailable`, a call that does not answer within
`Hermes:Dispatch:Timeout`, and a call that throws are a transient answer: it counts as
an attempt, the delivery waits for its next, and after the last it ends `failed` with
the reason `consentunavailable`, or `expired` where the kind's expiry comes first. The
source is never asked for an essential category. A deployment that declares a category
requiring consent and registers no consent source fails startup with
`messaging.startup.declarationmissing`, `details.key` `consentSource`. A source meeting
a recipient case it does not know (`04` MSG-LIB-002) answers `NotGiven`.

*Source: M-006*

**Acceptance criteria**
1. A delivery whose recipient's consent the source answers `NotGiven` ends `noconsent`.
2. A consent withdrawn after the send and before the dispatch stops the delivery.
3. A deployment with a consent category and no consent source does not start.

---

**MSG-SEND-012** — An unsubscribe SHALL suppress the destination for the category at
once, and every later delivery of that category to that destination SHALL end
`suppressed`, whoever the recipient.

**Values (M-006).** A suppression is recorded by the unsubscribe endpoints (`05`
MSG-API-005) or by `ISuppressions.AddAsync(channel, destination, category)`, and
withdrawn by `ISuppressions.RemoveAsync(channel, destination, category)`; adding one for
an essential category is refused with `messaging.suppression.essential`, one naming a
category not declared, a channel other than `email` and `sms`, or a destination that
does not read, with `messaging.suppression.invalid`, and one needing a key version this
process does not hold with `messaging.key.unavailable`; `RemoveAsync` answers
`messaging.key.unavailable` and removes nothing where a version in use is not held,
since it must find the suppression under every one. It is kept as the destination's
keyed hash (`06` MSG-OPS-008, domain `email` or `sms`) with the category, the `hashKeys`
version and the instant, never the destination itself, so it holds no address or number
and survives the person's erasure (MSG-SEND-020). A suppression lasts
`Hermes:Retention:Suppression:Email` or `Hermes:Retention:Suppression:Sms` (default:
indefinitely). A person turning a category back on through preferences does not withdraw
a suppression, since the library holds no destination to match; the host, which knows
the person's current address and number, calls `RemoveAsync` with them when the person
turns the category on.

*Source: M-006*

**Acceptance criteria**
1. After an unsubscribe, a delivery of the category to the destination ends
   `suppressed`, for a person and for an anonymous recipient alike.
2. The suppression table holds no destination in plain text.

---

**MSG-SEND-013** — A delivery of a category whose quiet hours apply SHALL be held, when
its check falls inside quiet hours in the recipient's time zone, until they end.

**Values (M-006, M-026).** Quiet hours run from `Hermes:QuietHours:Start` to
`Hermes:QuietHours:End` (default 21:00 to 09:00 local wall-clock time; a window that
crosses midnight included) in the send's time zone. A delivery that meets check 5 inside
them gets a due instant at the window's end plus a random delay of up to
`Hermes:QuietHours:Spread`, and returns to `waiting` without counting an attempt. Where
a start or end falls in a daylight-saving gap, the first instant after the gap is used;
where it falls in an overlap, the first occurrence. A start equal to the end turns quiet
hours off. Quiet hours never apply to an in-app delivery, which interrupts nobody (`02`
MSG-INAPP-001).

*Source: M-006*

Quiet hours keep an offer from waking someone at night; they never hold back what the
person is waiting for, which is why an essential category is never subject to them.

**Acceptance criteria**
1. A delivery of a category with quiet hours, checked at 23:30 in Africa/Cairo, is
   dispatched after 09:00 there.
2. An essential delivery checked at 23:30 is dispatched at once.
3. A delivery held for quiet hours gives its place in the provider's rate counter back,
   and its consent source is not asked.

---

**MSG-SEND-014** — A delivery SHALL be dispatched only within its category's limits and
its provider's rate.

**Values (M-005, M-006, M-007, M-026).** At check 8, in one transaction at
`READ COMMITTED` whose locks, taken together, wait no longer than
`Hermes:Dispatch:Timeout` (the transaction setting `lock_timeout` before each lock to
what is left of it, in whole milliseconds rounded up, the delivery being held as for a
lock that timed out where nothing is left), the dispatcher takes the advisory locks of
the category's limits on the channel, then locks the delivery's row
(`SELECT ... FOR UPDATE`) and confirms that it is still `inflight` under this pass's
claim, taking no place where it is not; it then counts the **places** taken in each
window, and takes one place for each limit where every count is under its limit:
- a `Limit` place is keyed by its channel, its category, its window in whole seconds and
  the destination's keyed hash for email and SMS, or the person reference's (domain
  `person`) for push and in-app; its locks are taken under every `hashKeys` version this
  process holds, in ascending order of version, its places counted under every such
  version (the versions in use then read as for a lookup, MSG-SEND-006), and a new place
  written under the write version;
- a `DeploymentLimit` place is keyed by its channel, its category and its window, and
  has one lock;
- a lock's key is the first eight bytes, read as a big-endian signed 64-bit integer, of
  the SHA-256 of the place's key: its parts in the order above, joined by zero bytes, a
  channel and a category as the UTF-8 bytes of their names, a window as its whole
  seconds in decimal ASCII digits and a hash as its 32 bytes, a layout two releases
  share and never change; `Limit` locks are taken before `DeploymentLimit` locks, each
  of the two in ascending order of window, and for one window in ascending order of
  version.

A lock that times out (55P03) holds the delivery, due after `Hermes:Dispatch:Interval`,
without counting an attempt, so one stuck session never stops a category's dispatch for
longer.

The delivery that would exceed a limit ends `limited`. A delivery takes its limit places
once: one held after check 8, or retried, keeps them. A limit place is released when its
delivery ends in a final status other than `sent` and `delivered`, so a message that
never went out does not count, save that a delivery on which a provider call is
recorded, a delivery a provider's report ends `failed` (reason `reportedfailed`)
included, whatever its status, releases its `Limit` places only, never its
`DeploymentLimit` places (`02` MSG-SMS-005), since its message may have gone out, so
neither a forged report nor a cancellation keeps the deployment's cap from filling. Each
place records the instant it lapses, `@now` plus the writing application's
`Hermes:Retention:Places` (default 31 days), which is at least each of its limits'
windows (MSG-SEND-002), and is counted by the instant it was taken; a place's link to
its delivery is set to null when the delivery's record is deleted (MSG-SEND-019), so a
record kept for less time than its places leaves them counting.

Each provider's rate (`Hermes:Email:Rate`, `Hermes:Sms:Rate`, `Hermes:Push:Rate`,
deliveries per second across the deployment) is kept in one counter row per channel and
second of `@now`, whose places a pass takes when it claims (MSG-SEND-007), its
transaction locking the row (`INSERT ... ON CONFLICT DO NOTHING`, then
`SELECT ... FOR UPDATE`) so passes of every process take them one after another; a
delivery keeps its place through check 6, so it is past the rate before the consent
source is asked, and gives it back where an earlier check ends or holds it. The in-app
channel has no provider and so no rate: its deliveries take no place in a counter
(MSG-SEND-007).

*Source: M-005, M-006, M-007*

**Acceptance criteria**
1. With a limit of 2 SMS per day, the third delivery to one number within 24 hours ends
   `limited`, and after one of the first two is reported failed, a further delivery is
   sent.
2. Two dispatchers checking two deliveries for the last place at once dispatch one, and
   so do two whose processes write under different `hashKeys` versions.
3. With a `DeploymentLimit` of 3 SMS per hour, the fourth SMS of the category to any
   number within the hour ends `limited`, and a report that one of the first three
   failed does not let a fourth through.
4. With limits of 2 a day and 5 a week on one channel, a first SMS leaves each with room
   for the rest of its count.
5. With `Hermes:Sms:Rate` 5, no counter second holds more than five SMS deliveries
   across two processes.
6. A place written by an application whose `Hermes:Retention:Places` is 90 days is kept
   by a retention job whose own is 31 days until the place's lapse.

---

## 5. Retries, expiry and statuses

**MSG-SEND-015** — A delivery of a kind that declares `ExpiresAfter` SHALL NOT be
dispatched later than that duration after its send, and SHALL end `expired` when it
would be.

**Values (M-005).** Check 2 ends a delivery past its expiry `expired`; a transient
answer, or a hold for a key, whose next attempt would fall after the expiry ends it
`expired` too, at once, before `Hermes:Dispatch:MaxHold` is weighed (MSG-SEND-006). A
push request's `TTL` is never later than the expiry (`02` MSG-PUSH-002).

*Source: M-005*

A sign-in code that arrives after it has lapsed only confuses the person.

**Acceptance criteria**
1. A delivery whose first check comes after its expiry ends `expired` without a provider
   call, and one whose retry would fall after it ends `expired`.

---

**MSG-SEND-016** — A transient answer SHALL be retried with exponential back-off and
jitter until the delivery is sent, its attempts are used up, or it expires.

**Values (M-005).** After the `n`th attempt (`n` from 1), the next is due
`Hermes:Retry:Initial` times `Hermes:Retry:Factor` to the power `n - 1`, capped at
`Hermes:Retry:Max`, then increased by a random part of up to `Hermes:Retry:Jitter` of it
(default 20%). Where the provider gave a `Retry-After` (for push, the latest of those
its calls gave) that falls later, the next attempt is due then, capped at
`Hermes:Retry:Max` from now. After `Hermes:Retry:MaxAttempts` attempts the delivery ends
`failed`, with its last attempt's reason.

*Source: M-005*

**Acceptance criteria**
1. A delivery answered transient three times and then accepted ends `sent` after four
   attempts at growing intervals.
2. A delivery answered transient on every attempt ends `failed` after the last.
3. A 429 with `Retry-After` delays the next attempt at least that long, and at most
   `Hermes:Retry:Max`.

---

**MSG-SEND-017** — Every delivery SHALL reach a final status and move from it at most
once, from `sent`, and the host SHALL be able to read a delivery's status and be told of
each final status a delivery reaches.

**Values (M-005, M-026).** The statuses are in `08` section 3. A **final** status is one
after which no attempt is made. Of the final statuses, `sent` alone may change once
more: by a provider's report, to `delivered` or to `failed` (`02` MSG-SMS-004), or, for
an in-app delivery, by its entry being marked read, to `read` (`02` MSG-INAPP-003). A
delivery held for a key ends, at the latest, at its first hold `Hermes:Dispatch:MaxHold`
after its run of holds began (MSG-SEND-006). `IMessages.ReadAsync(SendId)` answers the
send's deliveries with their statuses, instants and attempts, or
`messaging.send.unknown`.

An application with `Hermes:Observer:Enabled` on writes a **notice** for each final
status and each move from `sent` it makes, in the transaction that makes it, carrying
the delivery's next sequence number, which the delivery's row keeps and that transaction
advances under the row's lock, so a deployment that observes turns it on in every
application; an application with it and `Hermes:Dispatch:Enabled` on registers an
`IDeliveryObserver` (`04` MSG-LIB-005) and tells it of each, at least once and in order
for each delivery: a notice is kept until the observer has taken it (its `ObserveAsync`
returned without throwing), it is delivered only once no notice of the same delivery
with a lower sequence number is pending, and notices are delivered by the `observer` job
alone (`06` MSG-OPS-004), which no pass waits for, so a slow observer never slows
dispatch, save under `AddHermesTesting`, where the `observer` job does not run and a
test's pass delivers them (`04` MSG-LIB-008). The job delivers notices of different
deliveries up to `Hermes:Observer:MaxCalls` (default 4) at once. Whoever delivers a
notice, the job or a test's pass, claims it (a holder and the instant the claim expires,
`@now` plus its own `Hermes:Dispatch:ClaimTimeout`) immediately before its
`ObserveAsync`, by an update conditional on its being unclaimed or its claim expired,
and once the observer has taken it deletes it by a delete conditional on that claim, so
a taken notice keeps nothing of the person; each `ObserveAsync` is bounded by
`Hermes:Dispatch:Timeout`, a call past it counting as a failure, and the processes'
clocks agree as `06` MSG-OPS-003 says, so two never deliver one notice at once. The
`observer-failing` alert is raised while the oldest pending notice is older than
`Hermes:Observer:LagThreshold`, as the watch job finds (`06` MSG-OPS-004); a notice
older than `Hermes:Retention:Record` is dropped and logged by the retention job
(MSG-SEND-019). An application with it off writes no notice.

A notice carries the idempotency key its send was recorded with (MSG-SEND-009), none
where the send had none, so a host that keyed its send with a reference of its own
finds its own record of the delivery. The key is kept with the send for as long as the
send's record, not read from its idempotency row, which lapses while a delivery's
outcome can still come, and goes with the person at an erasure (MSG-SEND-020, M-028).

*Source: M-005, M-028*

**Acceptance criteria**
1. No delivery is attempted after reaching a final status, none moves from a final
   status other than `sent`, and none stays `inflight` past its claim's timeout.
2. The observer is told of a final status after it commits, told again after it threw,
   and told of a report's move only after the status it moved from.
3. An observer that keeps throwing raises the alert once its oldest notice passes the
   threshold.
4. A notice of a send recorded with an idempotency key carries the key, also after the
   key's row has lapsed and for a move a provider's report makes; a notice of a send
   recorded without one carries none.

---

## 6. Content, retention and erasure

**MSG-SEND-018** — A send's content SHALL be rendered when the send is recorded, and
every destination, unsubscribe address and rendered content SHALL be stored only as a
content protector's output, bound to its row and its person.

**Values (M-007, M-026).** Each channel's template is rendered in the send's language
when `SendAsync` records it (`03`), the in-app channel's in every declared language
(`02` MSG-INAPP-002), so a template changed afterwards does not change a recorded send.
Each destination, each rendered part and an email's unsubscribe address is stored as an
envelope: the format byte `0x01`, the protector's `Id` as its length in one byte and its
ASCII bytes, then the protector's output. The format bytes `0x02` to `0x0F` are kept for
later releases: a value in one of them cannot open yet here (MSG-SEND-006), since a
newer release wrote it, and a value in any other format does not open under its binding,
as `Rejected`. A person's values are protected by the last registered protector's
`ProtectAsync` (`04` MSG-LIB-002), called with the person reference and the context
`<table>:<row id>:<field>`, to which the output is bound, so a value copied to another
row or field does not open: `<table>` is `deliveries` for a delivery's values and
`subscriptions` for a push subscription's and `entries` for an in-app entry's, whatever
the tables are called; `<row id>` is the row's identifier in lower-case hexadecimal,
hyphenated 8-4-4-4-12; `<field>` is `destination`, `unsubscribe` (the email's one-click
address, made with its token at the send and kept for the header, `02` MSG-EMAIL-006), a
rendered part's name (`03` MSG-TPL-001: `subject`, `text`, `html`, `title`, `body`,
`url`, `tag`, `icon`, `badge`), an in-app part's name and its language's declared tag
joined by `.` (`title.en`, `body.ar`), `target` for an in-app delivery's or entry's
target values, or for a subscription `endpoint`, `p256dh` or `auth`; a layout two
releases share and never change (`08` section 8); an anonymous recipient's values are
always protected by the library's own protector, whatever protector is registered. A
value is opened by the protector its envelope names. A host protector's `Id` SHALL NOT
be `messaging`, or startup fails with `messaging.startup.declarationinvalid`.

The library's own protector, `Id` `messaging`, writes the `contentKeys` version (4
bytes, big-endian), a random 12-byte nonce, the AES-256-GCM ciphertext and its 16-byte
tag, its associated data being the context's UTF-8 bytes, a zero byte and the person
reference's (empty for none), so a value opens only in its row and for its person; it is
always registered for reading, so values it made stay readable after a host registers
another protector (a host's own, over its identity system's per-person keys, say, `04`
MSG-LIB-005). A row holding values of the library's protector records their
`contentKeys` version in an indexed column (`06` MSG-OPS-008).

At check 1, a value whose protector answers `Erased` (the person's key was erased) ends
its delivery `cancelled`; one answering `Rejected` (it does not open under its binding,
which only a change made to the database outside the library can cause), or in a format
neither `0x01` nor kept for a later release, ends it `failed` with the reason
`valuerejected` and raises the `content-rejected` alert; one answering `KeyUnavailable`,
throwing or not answering within `Hermes:Dispatch:Timeout`, naming a protector not
registered, or in a format kept for a later release, holds the delivery for a key
(MSG-SEND-006). A push subscription's values answer at check 9 (`02` MSG-PUSH-005). A
delivery stores no hash of its destination: a lookup by destination computes the keyed
hash from the opened value at that moment.

*Source: M-005, M-007, M-020*

**Acceptance criteria**
1. No destination, unsubscribe address or rendered content is stored in plain text.
2. A template changed after a send leaves that send's content as rendered.
3. A protected value copied into another delivery's row, or a row whose person reference
   is changed in the database, does not open, and its delivery ends `failed` with the
   reason `valuerejected` and raises the alert.

---

**MSG-SEND-019** — The library's data SHALL be deleted when its time is up, each period
configuration.

**Values (M-005, M-007, M-008, M-009, M-026).** The retention job deletes, in batches:
- a delivery's content, destination and unsubscribe address `Hermes:Retention:Content`
  (default 30 days) after it reached its final status (for `sent`, after its last move);
  a delivery not yet final keeps them;
- a delivery's record `Hermes:Retention:Record` (default 365 days) after the same
  instant, keeping until then the kind, category, channel, statuses with their instants,
  attempts, the provider reference and the person reference, so an erasure still finds
  it: no content, no destination; a send's row goes with the last of its deliveries'
  records;
- an idempotency row whose lapse has passed (MSG-SEND-009);
- a limit place whose lapse has passed (MSG-SEND-014);
- a suppression past its lifetime (MSG-SEND-012);
- a push subscription that has accepted nothing for `Hermes:Push:IdleRetention` (default
  180 days);
- an in-app entry `Hermes:Retention:InApp` (default 180 days) after it was written (`02`
  MSG-INAPP-005);
- an observer notice older than `Hermes:Retention:Record`, logged as it goes
  (MSG-SEND-017);
- a delivery whose channel, status or reason this release does not know, or of a kind
  its application neither declares nor retires, `Hermes:Retention:Record` after the
  later of its recording and its due instant, logged by count (MSG-SEND-001,
  MSG-SEND-007);
- an SMS balance reading older than `Hermes:Sms:DrainBaseline`, a rejected-report
  counter row older than `Hermes:Sms:ReportRejectWindow` (`02` MSG-SMS-004,
  MSG-SMS-006), a push answer counter row older than `Hermes:Push:AuthFailWindow` (`02`
  MSG-PUSH-005), and a rate counter row whose second has passed (MSG-SEND-014).

*Source: M-005, M-007, M-008, M-009, M-026*

**Acceptance criteria**
1. A delivery 31 days past its final status holds no content, no destination and no
   unsubscribe address; one 366 days past it holds no record; a delivery still `waiting`
   past both keeps everything.
2. Each other row above is gone after its period.

---

**MSG-SEND-020** — Erasing a person SHALL remove what the library holds about them, and
the library SHALL answer what it holds for a person's export.

**Values (M-007, M-026).** `ISubjects.EraseAsync(person)` answers a `Result`. It runs in
one transaction at `READ COMMITTED` whose `lock_timeout` is `Hermes:Dispatch:Timeout`, a
timeout letting the database's error through, so an erasure that waits for a host's open
transaction (MSG-SEND-004) never waits for ever. It first takes the person's locks of
`02` MSG-PUSH-003 exclusively, under which subscriptions are registered and preferences
written and which a send, a preference write and the in-app list's writes and reads take
shared (MSG-SEND-003, MSG-SEND-010, `02` MSG-INAPP-001, MSG-INAPP-003), and then reads
the migration history: where its database holds a migration its release does not know
(`06` MSG-OPS-002), it answers `messaging.release.newer`, `details.migration` the first
it does not know, since a newer release's tables may hold the person's values;
otherwise, where it would need a key version this process does not hold, it answers
`messaging.key.unavailable`; either way it changes nothing, so an erasure is all or
nothing and the caller retries it, on a process of the newest release for the first. It
then ends the person's `waiting` and `inflight` deliveries `cancelled`, an update that
waits for a check-8 transaction holding one of their rows (MSG-SEND-014), so its later
statements see any place that transaction took (an `inflight` delivery's late outcome
then changes nothing, MSG-SEND-007); it deletes their preferences, push subscriptions,
in-app entries and idempotency rows, and deletes the content, destination, unsubscribe
address and person reference of every delivery for them, whatever its status, and the
idempotency key kept with their sends (MSG-SEND-017), keeping the rest of each record
until its retention ends. Their pending observer notices, and the notices of the
deliveries it cancels, are kept for the observer with no person reference and no
idempotency key, so the host still learns how each delivery ended. The deliveries it
cancels release their limit places as any cancellation does (MSG-SEND-014); it then
deletes the limit places keyed by their reference's hash, and clears the delivery link
of the other places their deliveries took, so no kept place ties a kept record to a
destination hash. Suppressions and the other limit places stay: they hold no destination
and nothing of the person, and a suppression keeps the person's refusal in force.

`ISubjects.ExportAsync(person)` answers a `Result<SubjectExport>` (`04` MSG-LIB-002), or
`messaging.key.unavailable` or `messaging.release.newer` as erasure does: the person's
preferences; their subscriptions, each with its push service and the instants it was
made and last used, never its endpoint or keys; their in-app entries, each with its
kind, category, the instants it was written and read, and its parts in every language it
holds with its target and target values, its values answering as a delivery's do below;
their live idempotency rows, each with its kind, key and instant; and every delivery
whose record names them, each with its kind, channel, status and instants, and with its
destination and content while it holds them, one past content retention being marked so
and carrying neither, a value whose protector answers `Erased` being marked erased and
carrying no destination and no content, and one that does not open under its binding
being marked rejected, carrying no destination and no content, and raising
`content-rejected`; a value that cannot open yet (MSG-SEND-006), a protector call that
throws or does not answer within `Hermes:Dispatch:Timeout` included, answers the export
`messaging.key.unavailable`. The export runs in one transaction at `READ COMMITTED`
whose `lock_timeout` is `Hermes:Dispatch:Timeout`, a timeout letting the database's
error through as for erasure, takes the person's locks shared, then reads the migration
history as erasure does and the person's rows, and commits; it opens the values after,
outside any transaction, so its protector calls hold no lock and no session idle; an
erasure committed after its read leaves the export as it read, its values answering
`Erased` only where the person's key was erased (a host's protector over per-person
keys, `04` MSG-LIB-005).

*Falling back to another channel (designed for, not built).* A kind may later declare an
ordered list of channels where the next is used when a delivery on one ends in a status
the list names; the deliveries of a send already share its identifier, so a fallback is
a new delivery of the same send and changes no table.

*Source: M-004, M-005, M-007, M-020, M-028*

**Acceptance criteria**
1. After erasure, nothing the library holds names the person or holds a destination, an
   unsubscribe address or content of theirs, no place links a kept record to a
   destination hash, their suppressions still apply, and the observer is told that their
   waiting deliveries were cancelled.
2. An export holds every delivery still carrying content with its destination, every
   delivery past content retention marked so with neither, the person's live idempotency
   rows, and no push endpoint or key; a delivery whose value does not open under its
   binding is exported marked rejected, with no destination or content, and raises
   `content-rejected`; one whose value answers `Erased` is exported marked erased, with
   no destination or content; one whose value cannot open yet makes the export answer
   `messaging.key.unavailable`.
3. An erasure that meets a key version this process does not hold changes nothing and
   answers `messaging.key.unavailable`.
4. A dispatcher at check 8 when the person is erased leaves no place keyed by their
   reference's hash or linked to their deliveries, and neither it nor the erasure is
   aborted.
5. With a migration this release does not know in the history, an erasure and an export
   change nothing and answer `messaging.release.newer` naming it, also where a key
   version in use is not held.
