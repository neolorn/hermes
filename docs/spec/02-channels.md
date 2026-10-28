# 02 — Channels

What each channel does with a delivery the dispatcher hands it, how it calls its
provider, and how it classes the provider's answer (`01` MSG-SEND-007). Every value
named `Hermes:...` is configuration (`08` section 4).

---

## 1. The provider contracts

**MSG-CHAN-001** — Each channel SHALL reach its provider through a contract of
`Hermes.Core` that a host can replace, and every provider SHALL answer each call with an
`Attempt` that carries no text the provider answered.

**Values (M-002, M-005, M-012).** `IEmailProvider`, `ISmsProvider` and `IPushProvider`,
with the signatures of `04` MSG-LIB-002. An `Attempt` is one of:
- `Accepted(providerReference)`, the provider reference where the provider answers one;
- `Transient(reason, status, retryAfter)`;
- `Permanent(reason, status)`;
- `Gone(reason, status)`, from a push provider only (MSG-PUSH-005).

`reason` is a code of `08` section 6 and `status` the provider's numeric status (an SMTP
reply code, an HTTP status) where it gave one. A provider never throws for an answer it
received: a timeout, a refused connection and every reply are answers. An exception that
escapes a provider is a fault of that provider: the dispatcher records it as
`Transient(providerfault)` and logs the exception's type, never its message, which can
carry a destination. `Hermes.Channels` ships one provider for each channel: SMTP
(section 2), WE (section 3) and Web Push (section 4). The shipped SMS and push providers
make their requests through the named clients `Hermes.Sms` and `Hermes.Push` of
`IHttpClientFactory`, which follow no redirect and time out after
`Hermes:Dispatch:Timeout`; the `Hermes.Push` client's handler chain carries the
library's handler that replaces a response's content with an empty one unless its status
is 2xx, since Lib.Net.Http.WebPush reads an error answer's body whole, with no token and
no bound, and the library keeps no provider text. The two clients carry the library's
handlers alone: their registration clears every additional handler a host's
`ConfigureHttpClientDefaults` adds (`ConfigureAdditionalHttpMessageHandlers`, which runs
after a host's defaults) and sets their primary handler itself, so no retry a host adds
for its own clients posts an SMS again within one call, and no push service's
`Retry-After` is waited out within one; and it removes the factory's own request logging
(`RemoveAllLoggers()`), which writes a request's address, a push endpoint's path among
it, so no log line holds one (`06` MSG-OPS-005); a test reaches a local listener by
replacing a client's primary handler, which a deployment never needs to do.

*Source: M-002, M-005, M-012*

**Acceptance criteria**
1. A host-registered provider replaces the shipped one for its channel with no other
   change.
2. A provider that throws leaves its delivery waiting for its next attempt, and the log
   entry names the exception's type and holds nothing of its message.
3. No attempt record and no log entry holds text a provider answered.
4. Under a host's `ConfigureHttpClientDefaults` adding a retrying handler, a timed-out
   SMS request is made once and a push service's 429 is answered at once.
5. A push request through the shipped provider, under a host logging Information for
   every category, leaves no log line or scope holding its endpoint.

---

## 2. Email

**MSG-EMAIL-001** — Email SHALL be submitted over SMTP (RFC 6409) to the configured
submission server, over TLS, authenticated, with the server's certificate validated.

**Values (M-010).** The server is `Hermes:Email:Host` and `Hermes:Email:Port`, with
`Hermes:Email:Security` `ImplicitTls` (the default, RFC 8314's preference; port 465 by
default) or `StartTls` (port 587 by default); a server that does not offer TLS is never
used without it. The library validates the certificate itself:
- its name matches `Hermes:Email:Host`;
- its chain builds to a trusted authority: the system's or, where
  `Hermes:Email:TrustedAuthority` holds a PEM certificate, that authority alone;
- every certificate of the chain below the authority that names a revocation source (a
  CRL distribution point or an OCSP responder) has its status read there, online, and
  good; a status that cannot be read fails;
- a certificate that names no revocation source is accepted only where it is the leaf
  and its validity period is at most `Hermes:Email:ShortLivedMaxValidity` (default 7
  days, the CA/Browser Forum's short-lived certificate, which carries no revocation
  source), or where it chains to `Hermes:Email:TrustedAuthority` and
  `Hermes:Email:TrustedAuthorityRevocation` is `none` (a private authority that
  publishes no revocation).

Any other result fails the attempt as `Transient(tlsfailed)` and raises
`email-submission-failing` (`06` MSG-OPS-006); no setting turns validation off. The
account's name and password are the secret `emailSubmission` (`06` MSG-OPS-008). MailKit
is the client; any SMTP submission server can be the server, by configuration alone.

*Source: M-010*

**Acceptance criteria**
1. A server offering no TLS, a certificate for another host, a chain to an authority not
   trusted, and a certificate whose named revocation source cannot be read each send
   nothing and raise the alert.
2. With `Hermes:Email:TrustedAuthority` set, a certificate issued by that authority
   validates and one issued by a second test authority does not; the chain is built with
   that authority as its only anchor (`X509ChainTrustMode.CustomRootTrust`), checked in
   review.
3. A leaf valid for 6 days that names no revocation source validates; one valid for 90
   days that names none does not.
4. Another submission server is used by configuration alone.

---

**MSG-EMAIL-002** — Each email SHALL go out from the sender its kind declares, or the
deployment's default sender, and that address SHALL be its envelope sender too.

**Values (M-010).** `Email(from: ..., replyTo: ...)` on a kind, each an `EmailAddress`
(`04` MSG-LIB-002), or `Hermes:Email:From:Address` and `Hermes:Email:From:DisplayName`.
An address is an RFC 5322 `addr-spec` of at most 254 characters, checked at startup. The
envelope sender is the From address, since a submission server MAY refuse a sender its
account may not use (RFC 6409 section 6.1). Every From domain is one the submission
server signs for (`06` MSG-OPS-007).

*Source: M-010*

**Acceptance criteria**
1. An email of a kind with its own sender carries it in From and as its envelope sender.
2. A kind whose `from` is not an address stops the start.

---

**MSG-EMAIL-003** — Each email SHALL be a UTF-8 MIME message with a plain-text part and,
where the kind's template has one, an HTML part as its alternative, and SHALL carry
nothing the library adds to track it.

**Values (M-010).** `multipart/alternative` with `text/plain` first and `text/html`
second, or `text/plain` alone. `Message-ID` is the delivery's identifier at the From
address's domain; `Date` is set by the provider at each attempt, so a delivery held by
quiet hours carries the instant it went out; `Auto-Submitted: auto-generated` (RFC
3834); the subject from the template (`03` MSG-TPL-005). No tracking pixel, no tracked
link and no remote image is added. An email whose rendered subject, text and HTML
together exceed `Hermes:Email:MaxBytes` in UTF-8 (default 1 MiB) is refused at the send
with `messaging.send.toolong`, `details.channel` `email`.

*Attachments (designed for, not built).* A later `Send.Attachments` adds parts after the
body. The content protector already protects bytes (`04` MSG-LIB-002), and attachments
come through a new provider interface an email provider implements beside
`IEmailProvider` (`04` MSG-LIB-006), so a provider written for the first release never
receives a message whose attachments it would drop.

*Source: M-004, M-010*

**Acceptance criteria**
1. An email of a kind with an HTML template carries both parts; one without carries the
   text part alone.
2. A delivery held by quiet hours carries a `Date` of the instant it was submitted.
3. No email carries anything the library adds to tell whether it was opened.

---

**MSG-EMAIL-004** — An SMTP answer SHALL be classed as accepted, transient or permanent.

**Values (M-010).**

| Answer | Class | Reason |
|---|---|---|
| 250 to the end of the data | `Accepted`, no provider reference | |
| A 4xx reply at any step | `Transient` | `temporary` |
| A timeout, or a connection refused or broken | `Transient` | `timeout`, `connectionfailed` |
| A reply that does not read as SMTP | `Transient` | `connectionfailed` |
| A 530 at any step, which MailKit raises without its step or code | `Transient`, raising `email-submission-failing` | `authfailed`, no status |
| A 5xx reply to `RCPT TO` | `Permanent` | `recipientrefused` |
| A 5xx reply to the data or its end | `Permanent` | `messagerefused` |
| TLS not completed or the certificate not valid (MSG-EMAIL-001) | `Transient`, raising `email-submission-failing` | `tlsfailed` |
| An authentication failure (535, or any 5xx to `AUTH`), or a server offering no authentication mechanism the client supports | `Transient`, raising `email-submission-failing` | `authfailed` |
| A 5xx reply to `MAIL FROM` | `Transient`, raising `email-submission-failing` | `senderrefused` |
| Any other 5xx reply (to the greeting, `EHLO` or `STARTTLS`) | `Transient`, raising `email-submission-failing` | `serverrefused` |

The rows are tried from the top, and the first that fits applies. The answers that raise
the alert are the deployment's problem, not the address's, so they keep the delivery for
a later attempt. Every class records the reply code as `status` where there is one; no
reply text is kept.

*Source: M-010*

**Acceptance criteria**
1. A 550 to `RCPT TO` ends the delivery `failed` with the reason `recipientrefused`.
2. A 535, and a server that offers no authentication, leave the delivery waiting and
   raise the alert.

---

**MSG-EMAIL-005** — A dispatcher pass SHALL send its email deliveries through one
provider session, which authenticates once and reconnects when its connection breaks.

**Values (M-010).** `IEmailProvider.OpenAsync` answers an `IEmailSession` (`04`
MSG-LIB-002); the pass opens it at its first email delivery and disposes of it at its
end. The SMTP session connects and authenticates at its first message, and connects and
authenticates again before the next message after its connection broke, or after the
server closed it (a 421 reply included).

*Source: M-010*

**Acceptance criteria**
1. A pass of ten email deliveries authenticates once.
2. A connection broken after the third delivery is opened again for the fourth.

---

**MSG-EMAIL-006** — Every email of an optional category SHALL carry a one-click
unsubscribe (RFC 8058) and a visible unsubscribe link, and no email of an essential
category SHALL carry either.

**Values (M-006, M-007, M-010).** The message carries `List-Unsubscribe: <address>` and
`List-Unsubscribe-Post: List-Unsubscribe=One-Click`, where the address is
`Hermes:PublicBaseAddress` followed by `/unsubscribe/` and the token (`05` MSG-API-005).
The same address is the template value `unsubscribe` (`03` MSG-TPL-002), which the
`text` part, and the `html` part where there is one, of an optional category's email
use, and which an essential category's email does not; startup fails otherwise (`03`
MSG-TPL-004). The token is made when the send is recorded, dated from the later of that
instant and the send's `NotBefore` (`05` MSG-API-005), so an email scheduled ahead goes
out with a working link, and the address is stored with the delivery as its protected
value `unsubscribe` (`01` MSG-SEND-018), which the header takes at dispatch. RFC 8058
needs a DKIM signature covering both headers for a receiver to offer one-click; DKIM
signing is the submission server's (`06` MSG-OPS-007).

*Source: M-006, M-007, M-010*

**Acceptance criteria**
1. An optional category's email carries both headers and a visible link; an essential
   category's carries neither.
2. An optional category's email template without `unsubscribe`, and an essential
   category's with it, stop the start.
3. An email whose send's `NotBefore` lies 300 days ahead, under a `TokenLifetime` of 180
   days, goes out with a link that verifies.

---

**MSG-EMAIL-007** — An email's delivery SHALL end `sent` when the submission server
accepts it; the first release reads no later delivery or bounce report for email.

**Values (M-010).** *Bounces and complaints (designed for, not built).* A later release
asks for delivery status notifications (RFC 3461) carrying the delivery's identifier as
the envelope identifier (`ENVID`), so the envelope sender stays the From address
(MSG-EMAIL-002), and reads the returned reports (RFC 3464), or the submission server's
own delivery events, through an `IEmailFeedback` contract that moves a `sent` delivery
to `delivered` or `failed`. An address that bounced for good is then suppressed for
every category, essential ones included, a scope of its own beside the per-category
suppression of `01` MSG-SEND-012. A delivery's status already admits both moves (`01`
MSG-SEND-017), so the feedback adds a contract and a suppression scope and changes no
existing table.

*Source: M-004, M-010*

**Acceptance criteria**
1. An email accepted by the server ends `sent`, and nothing in the first release moves
   it further.

---

## 3. SMS

**MSG-SMS-001** — Each SMS SHALL be checked, when the send is recorded, against its
kind's budget of segments, counted as the network counts them.

**Values (M-008).** A text made only of characters of the GSM 7-bit default alphabet and
its extension table (3GPP TS 23.038 section 6.2.1) is encoded in septets, an extension
character taking two (the escape and the character, section 6.2.1.1); one segment holds
160 septets, and a longer text is split into segments of 153 septets each, the
concatenation header taking the rest (3GPP TS 23.040 section 9.2.3.24.1), never
splitting an escape pair. Any other text is UCS-2: one segment holds 70 UTF-16 code
units, and a longer text is split into segments of 67, never splitting a surrogate pair.
Segments are counted by packing the text in that order. A kind declares
`Sms(maxSegments: n)`, from 1 to 10, default 1; a rendered text over it is refused at
the send with `messaging.send.toolong`, `details.channel` `sms`. An Arabic text one
character over 70 costs two segments, so each kind's budget is its own decision.

*Source: M-008*

**Acceptance criteria**
1. A 70-character Arabic text is one segment; a 71-character one is two, and is refused
   under a budget of one.
2. A 160-character text of the default alphabet is one segment; 159 such characters and
   one `€` are two.
3. A text whose 153rd septet would be an escape starts its second segment with the
   escape.

---

**MSG-SMS-002** — Each SMS SHALL go out under the sender name its kind declares, or the
deployment's default, and only to a number the deployment allows.

**Values (M-006, M-008).** `Sms(sender: ...)` or `Hermes:Sms:Sender`. A sender is an
alphanumeric name of 1 to 11 characters of `A` to `Z`, `a` to `z`, `0` to `9` and space,
or a number of 1 to 15 digits (3GPP TS 23.040 section 9.1.2.5); a provider MAY narrow
the rule (`ISmsProvider.IsValidSender`), and startup checks every declared sender
against the registered provider's. A sender name is registered with the provider and the
operators beforehand; one nobody registered is refused by the provider
(`Permanent(senderrefused)`). A send whose phone number starts with none of
`Hermes:Sms:AllowedPrefixes` (E.164 prefixes; `+` alone allows every number) is refused
with `messaging.send.destinationnotallowed`, and the rule is checked again at dispatch
(check 9 of `01` MSG-SEND-006): a delivery whose number a narrowed list no longer allows
ends `failed` with the reason `destinationnotallowed`, so narrowing the list in an
incident stops what is already queued, without a restart.

*Source: M-005, M-006, M-008*

**Acceptance criteria**
1. A kind's own sender name is used for its SMS.
2. With `Hermes:Sms:AllowedPrefixes` `+20`, a send to `+44...` is refused and records
   nothing.
3. A prefix removed from the list while a delivery to it waits ends that delivery
   `failed` with the reason `destinationnotallowed`, without a restart.

---

**MSG-SMS-003** — The WE adapter SHALL send through WE's HTTP API, authenticated with
the account's credential, so that WE's report of each message reaches its delivery.

**Values (M-008).** What WE's documentation states, as the decision log records it: the
credential is Basic authentication over the static string `username:password:accountid`,
the secret `smsWe` (`06` MSG-OPS-008); a balance can be read; the delivery report is an
unauthenticated GET over plain HTTP carrying `messageId` and `status` in its query
string. The base address is `Hermes:Sms:We:BaseAddress`. These are taken from WE's API
document, which the adapter's build needs in hand (plan, phase 7):
- the request and answer fields, and which answers are temporary
  (`Transient(temporary)`) and which final (`Permanent`, with `recipientrefused`,
  `senderrefused` or `messagerefused` where the answer says which, otherwise
  `permanent`);
- whether a request can carry the delivery reference (`00` glossary) and the report echo
  it; where it cannot, the identifier WE answers is recorded as the provider reference
  and the report is matched by it (MSG-SMS-004);
- whether the report address is given with each request (then the adapter passes the
  message's `ReportAddress`, MSG-SMS-004, and `Hermes:Sms:ReportBaseAddress` is required
  where SMS is dispatched (`04` MSG-LIB-003) through the adapter in an engine
  application) or set once for the account;
- whether WE can call an `https` address; where it can call only plain `http`, the
  deployment serves the report group on a listener of its own exempt from HTTPS
  redirection, and the key then travels in clear (`10` MSG-THR-001);
- whether UCS-2 text is accepted under a registered sender name;
- the balance's answer and its unit.

*Source: M-008, M-020*

**Acceptance criteria**
1. A send's request and the report of it name the same delivery, by its delivery
   reference or its provider reference.
2. No credential appears in configuration files or in a log entry the library writes.

---

**MSG-SMS-004** — A delivery report SHALL be treated as hostile input: it can only move
an SMS delivery the library sent, or is sending, to `delivered` or `failed`, and SHALL
NOT do anything else.

**Values (M-008).** The report route (`05` MSG-API-006) carries a key in its path,
compared in constant time with every held version of the secret `smsReportKey`; a
request whose key does not match is answered 404 and changes nothing. Wherever
`Hermes:Sms:ReportBaseAddress` is set, the engine fills each `SmsMessage`'s
`ReportAddress` (`04` MSG-LIB-002) with that address, the mapped report group's absolute
address, followed by `/sms/report/` and the key of the `smsReportKey` write version; it
is null otherwise, and a provider that gives a report address with each message passes
it. Each process limits, per source address as the host's forwarded-headers setup gives
it, the requests whose key does not match to `Hermes:Sms:WrongKeyRate` a minute; a
request whose key matches is never limited. A request whose key matches is passed whole
to the provider's `ReadReportAsync` (`04` MSG-LIB-002), which answers the delivery
reference or the provider reference it names, the outcome (`delivered`, `failed` or
`notfinal`) and the response the provider expects to receive, or that the report does
not read, with the response. The reading's response is sent whatever the report's
effect, so a forger learns nothing from it.

The report is matched by delivery reference where it names one (each attempt has its
own, `01` MSG-SEND-005), and by provider reference otherwise, under the advisory lock of
the reference: the first eight bytes, read as a big-endian signed 64-bit integer, of the
SHA-256 of `sms-report`, a zero byte and the reference (a reference is no personal data,
so the lock needs no key); the handler then locks the delivery's row
(`SELECT ... FOR UPDATE`) before it reads its status. Every update that takes an SMS
delivery out of `inflight` (`01` MSG-SEND-007), `claimlost` included, first takes the
lock of the provider reference its answer carries, where it carries one, then the
delivery's row, and reads the outcomes kept on it under that row lock, so a report and
the update it races are serialised and cannot miss each other; the watch job takes a
held report's lock before it drops it. The rows are tried from the top, and the first
that fits applies:

| The report names | Effect |
|---|---|
| Anything, outcome `notfinal` | Nothing changes |
| An `inflight` SMS delivery, a final outcome | The outcome is kept on the delivery, one per attempt, a later `delivered` replacing a kept `failed` and nothing replacing a kept `delivered`, and applied when the update that takes it out of `inflight` is made: a kept `delivered` ends the delivery `delivered` whatever status that update would give it, a check's included, since the message arrived, and a kept `failed` applies as the row for the status it then has; where an attempt's kept outcome is `delivered`, the next renewal ends the delivery `delivered` instead of calling, counting no attempt and writing the delivery's notice (`01` MSG-SEND-017) |
| A `sent` SMS delivery's last attempt, outcome `delivered` | The delivery moves to `delivered` |
| A `sent` SMS delivery's last attempt, outcome `failed` | The delivery moves to `failed` with the reason `reportedfailed` (MSG-SMS-005) |
| An earlier attempt of a `sent` or `waiting` SMS delivery, outcome `delivered` | The delivery moves to `delivered` and no further attempt is made, since one of its messages arrived |
| A `waiting` SMS delivery's last attempt, which was transient or has no answer yet, outcome `delivered` | The delivery moves to `delivered` and no further attempt is made, since the provider took the message after all; an attempt with no answer is recorded `transient` with the reason `claimlost` (`01` MSG-SEND-007) in the same update |
| A `waiting` SMS delivery's last attempt, which was transient or has no answer yet, outcome `failed` | Nothing changes; the retry goes ahead |
| A provider reference no delivery holds yet, or a delivery in a status this release does not know (`01` MSG-SEND-007) | The report is kept, with its source address, for `Hermes:Sms:ReportHold` (default 10 minutes), at most `Hermes:Sms:ReportHoldMax` of them across the deployment, counted and inserted under one advisory lock (the first eight bytes of the SHA-256 of the text `sms-report-hold`, read as the other locks are), and applied when an outcome records that reference; the watch job (`06` MSG-OPS-004) drops one held longer and logs it, uncounted, since a genuine report of an attempt whose answer never named its reference lapses so too, and one over the cap is dropped at once, counted as rejected and logged |
| An SMS delivery on which a provider call is recorded (a call is recorded from the renewal before it, which records its delivery reference), in any other status; or an earlier attempt, outcome `failed` | Nothing changes |
| An SMS delivery on which no provider call is recorded, or no SMS delivery | Nothing changes; counted as rejected |

A report that does not read, or whose key does not match, is counted as rejected too,
and nothing else is. Every rejected report is logged with its source address and nothing
of its content, and counted in a counter row per minute
(`INSERT ... ON CONFLICT DO UPDATE SET count = count + 1`); more than
`Hermes:Sms:ReportRejectThreshold` within `Hermes:Sms:ReportRejectWindow` (default 1
hour), summed over those rows, raise the `sms-report-rejected` alert. Where the report
echoes the delivery reference, that reference is 128 random bits, so one guessed names
nothing; where reports are matched by the provider's own identifier, whose form the
provider sets, the key is the only bar to a forger (`10` MSG-THR-001), and a timed-out
attempt, which answered no provider reference, cannot be matched, so its retry goes
ahead.

Where WE gives one report address per account and other software of the host sends
through the same account (an identity library's sign-in codes, for example), the reports
of both arrive at one address; the host routes the reports that are not this library's,
since it alone knows both senders (M-020).

*Source: M-008, M-020*

**Acceptance criteria**
1. A report with the wrong key, and a report with the right key naming a guessed
   delivery reference, change nothing and are counted; one naming a provider reference
   no delivery holds is held, and logged uncounted when it lapses.
2. A report of delivery moves a `sent` delivery to `delivered`; a report of a final
   failure moves it to `failed`; a report of an intermediate state changes nothing and
   is not counted.
3. A report that arrives while its delivery is `inflight` is applied once the attempt's
   answer is recorded, and, where the report echoes the delivery reference, a report of
   delivery for a delivery waiting after a timed-out attempt ends it `delivered` with no
   further attempt.
4. A late failure report of a first attempt does not move a delivery whose second
   attempt was sent.
5. Over `Hermes:Sms:WrongKeyRate` wrong-key requests a minute from one source address, a
   request is answered 429, while reports with the right key from that address are not.
6. With `Hermes:Sms:ReportBaseAddress` set, the provider receives each SMS with that
   address, `/sms/report/` and the write version's key as its `ReportAddress`, and
   without it, none.
7. A `delivered` report of a first attempt, kept while the second attempt's check 2 ends
   the delivery `expired`, ends it `delivered`.

---

**MSG-SMS-005** — An SMS delivery that a report ends `failed`, whatever its status when
the report came, SHALL release its places under its category's per-destination limit
(`01` MSG-SEND-014), and only those.

*Source: M-006, M-008*

A text the network never delivered should not count against what the person may still
receive. Its place under the deployment's cap stays taken, so a forged failure report
cannot keep that cap from filling.

**Acceptance criteria**
1. After a failure report, a further delivery to the number fits under the limit the
   failed one had used.

---

**MSG-SMS-006** — The SMS balance SHALL be read on a schedule, a fast drain or a low
balance SHALL raise an alert, and below the floor only essential categories' SMS SHALL
go out.

**Values (M-008).** `ISmsProvider.BalanceAsync` answers the balance in the provider's
own unit (messages or money, as it counts), that it does not report one, or a failure.
The balance job (`06` MSG-OPS-004) reads it every `Hermes:Sms:BalancePoll` and records
each reading with its instant; spend is the fall between two readings, and a rise is a
top-up, not spend. The `sms-balance` alert is raised when either holds:
- **drain:** the spend in the last `Hermes:Sms:DrainWindow` (default 1 hour) is at least
  `Hermes:Sms:DrainMinSpend` (default 10 units) and exceeds `Hermes:Sms:DrainFactor`
  (default 3) times the mean spend per such window over the trailing
  `Hermes:Sms:DrainBaseline` (default 7 days), judged only once the readings span at
  least `Hermes:Sms:DrainMinHistory` (default 1 day);
- **low:** at the last window's rate of spend, the balance would reach
  `Hermes:Sms:BalanceFloor` within `Hermes:Sms:FloorWarning` (default 24 hours).

An SMS delivery of an optional category, checked while the last recorded balance is
below the floor, ends `balancefloor` (`01` MSG-SEND-006); an essential one goes out.
Until a first balance is recorded the floor refuses nothing. Reads failing for
`Hermes:Sms:BalanceUnreadableAfter` (default 1 hour) raise `sms-balance-unreadable`. A
provider that does not report a balance has no floor and raises neither balance alert.

*Source: M-008*

**Acceptance criteria**
1. Below the floor, an optional category's SMS ends `balancefloor` and an essential
   one's is sent.
2. A drain over the factor raises the alert through the alert sink; before the minimum
   history, the first spend raises nothing.
3. With a provider that reports no balance, no SMS ends `balancefloor` and no balance
   alert is raised.

---

## 4. Browser push

**MSG-PUSH-001** — Browser push SHALL follow Web Push: the protocol of RFC 8030, the
encryption of RFC 8291 (`aes128gcm`) and the application-server identification of RFC
8292 (VAPID).

**Values (M-002, M-009).** Lib.Net.Http.WebPush's `PushServiceClient` is the client,
over the named client `Hermes.Push`, with `AutoRetryAfter` off, so that a 429 comes back
to the dispatcher's retries (`01` MSG-SEND-016) instead of being waited out inside the
call. The VAPID subject `Hermes:Push:Subject` is a `mailto:` or `https:` URI, required.
A VAPID token is made for each push service origin and key version, expires 12 hours
after it is made, and is kept and reused for one hour, then replaced, since Apple asks
for no more than one new token an hour. The library's test encrypts a message with the
client to the receiver key and authentication secret of RFC 8291 section 5 and decrypts
it with the test's own implementation of section 3, which reproduces section 5's worked
example, so the encryption is checked against the standard and not only against itself.

*Source: M-002, M-009*

**Acceptance criteria**
1. A push request carries `Content-Encoding: aes128gcm`, `TTL`, `Urgency` where the
   urgency is not `normal` (RFC 8030 section 5.3 makes `normal` the default when the
   header is absent), and `Authorization: vapid t=..., k=...`, and its token's `exp` is
   at most 12 hours after it was made, by the system clock: the client sets `exp`
   itself, the one instant the library does not take from its `TimeProvider`.
2. A message the client encrypts to RFC 8291 section 5's receiver key and authentication
   secret decrypts, under the test's own section 3 implementation, to the plaintext.
3. A 429 answer returns from the call at once.

---

**MSG-PUSH-002** — A push message SHALL carry a notification the host's service worker
shows, in a documented JSON shape, within the protocol's size.

**Values (M-009).** The payload is the UTF-8 JSON object
`{ "title", "body", "url", "tag", "lang", "dir", "icon", "badge" }` rendered from the
kind's push template (`03` MSG-TPL-001): `title` and `body` always present, `lang` the
send's language and `dir` its direction, `url` where the notification opens, `tag`
replacing an earlier notification with the same tag on the device; a member whose
optional part the template lacks is left out. A payload over 3,993 bytes, the most RFC
8291's single record carries within the 4,096 bytes a push service must accept, is
refused at the send with `messaging.send.toolong`, `details.channel` `push`. Showing the
notification is the host's service worker's (`05` MSG-API-004 documents what it reads).

The kind declares `Push(ttl:, urgency:)`: `ttl` from 1 second to 28 days, default 1 day;
`urgency` one of `08` section 2, default `normal` (RFC 8030 section 5.3). A request's
`TTL` is the lesser of the kind's `ttl` and the time left before the kind's expiry (`01`
MSG-SEND-015), and at least 1 second. A send MAY carry `PushTopic`; its requests then
carry `Topic`: the base64url, without padding, of the first 24 bytes of the keyed hash
of the kind and the topic (`06` MSG-OPS-008, domain `push-topic`), 32 characters,
computed once when the send is recorded and stored with the delivery, so every request
of the send carries the same one whatever key version a process writes under (it is
never computed again nor looked up, so it is outside the retirement of `hashKeys`
versions, `06` MSG-OPS-008), and the push service replaces a pending message with the
same topic and learns nothing of it.

*Declarative Web Push (designed for, not built).* Safari's format that needs no service
worker is a second payload shape the push template can render. Only some Safari versions
take it, so a subscription will record the format its browser takes, as the page posts
it, and a send will render both shapes, since the subscriptions are known only at
dispatch.

*Source: M-004, M-007, M-009*

**Acceptance criteria**
1. A payload carries the members above in the send's language and direction.
2. A payload over 3,993 bytes is refused at the send.
3. A kind with `ttl` 1 day and an expiry of 10 minutes sends `TTL` of at most 600.
4. Two sends with the same `PushTopic` carry the same `Topic`, which is 32 base64url
   characters and differs from one made with another kind.

---

**MSG-PUSH-003** — A person's browser SHALL be able to register its push subscription,
and remove it, for the person signed in, and a push delivery SHALL go to that person's
live subscriptions as they stand when it is dispatched.

**Values (M-009).** The body is the browser's `PushSubscription.toJSON()` with the
subscription's `applicationServerKey` added (`05` MSG-API-004). A subscription is kept
for the person with: its endpoint and keys, protected (`01` MSG-SEND-018); its
endpoint's keyed hash (`06` MSG-OPS-008, domain `push-endpoint`), unique; the VAPID key
version whose public key is the posted `applicationServerKey` (MSG-PUSH-007); its push
service, read from the endpoint's host (`08` section 2); the instant it was made; the
expiry the browser gave, if any; and the instant it last accepted a message. Registering
runs in one transaction, whose `lock_timeout` is `Hermes:Dispatch:Timeout` (a timeout
answered 503 `messaging.request.unavailable`), under the endpoint's locks (its keyed
hash under every `hashKeys` version this process holds, in ascending order, each lock's
key made as `01` MSG-SEND-014 makes a place's) and then the person's, exclusively (the
keyed hash of the person reference, domain `person`, under the same versions in the same
order, each key made the same way, a layout two releases share): it looks the endpoint
up under every version it holds, locking the row it finds (`SELECT ... FOR UPDATE`), a
row gone by then counting as none, then reads the versions in use as a lookup does (`01`
MSG-SEND-006), answering 503 `messaging.request.unavailable` where one is not held (each
protector call it makes below bounded by `Hermes:Dispatch:Timeout`, one that throws or
times out answering 503 `messaging.request.unavailable` and raising `key-unavailable`
for the protector's `Id`); where a row holds the endpoint, it protects the endpoint and
keys again for this person under that row's identifier and updates the row, its key
version and person included; otherwise it inserts a row under the write version; then,
where the person holds more than `Hermes:Push:MaxSubscriptions` (default 10), it removes
the least recently used: the one whose later of its making and its last acceptance is
oldest, never the one just registered. A subscription is **live** while it is not past
its expiry (MSG-PUSH-006) and its endpoint's host is allowed (MSG-PUSH-004). A push
delivery to a person with no live subscription ends `nosubscription` (`01`
MSG-SEND-006).

*Source: M-007, M-009, M-012*

**Acceptance criteria**
1. A registered subscription receives the person's next push; a removed one does not.
2. A subscription registered after a send, by the same person, receives that send's push
   when it is dispatched later; one registered by another person never does.
3. The endpoint and keys are not stored in plain text, and an endpoint moved to another
   person opens only for that person.

---

**MSG-PUSH-004** — A subscription's endpoint SHALL be accepted only for a push service
the deployment allows, so the library never sends a request to an address a browser made
up.

**Values (M-009).** The endpoint is an absolute `https` address of at most 2,048
characters, on port 443, with no user information, whose host is a DNS name (never an IP
address) matching `Hermes:Push:AllowedHosts`: by default `fcm.googleapis.com`,
`*.push.services.mozilla.com`, `*.push.apple.com` and `*.notify.windows.com`, each
checked against the push service's current documentation when the build adds it. A
wildcard entry covers at least three labels after its `*.`, so no entry admits a whole
public domain; an entry beyond the defaults is the deployment's to vouch for (`06`
MSG-OPS-007). `p256dh` is a point on P-256 of 65 bytes and `auth` 16 bytes, both
base64url. An endpoint outside the rule is refused with
`messaging.push.endpointnotallowed`, and keys outside it with
`messaging.request.malformed`. The host is checked again before every request, so a host
removed from the list stops its subscriptions being used at once, and the named client
follows no redirect.

*Source: M-009*

**Acceptance criteria**
1. An endpoint at another host, at an IP address, on another port, over `http` or with
   user information is refused and stored nowhere.
2. A subscription whose host is removed from `Hermes:Push:AllowedHosts` receives
   nothing, without a restart.
3. A push request is never redirected.
4. An allowed-host entry `*.com` stops the start.

---

**MSG-PUSH-005** — A push service's answer SHALL be classed per subscription, a
subscription the service says is gone SHALL be removed, and the delivery's outcome SHALL
follow from its subscriptions' answers.

**Values (M-009).** Before an attempt's first call (check 9 of `01` MSG-SEND-006), the
dispatcher opens every live subscription of the person the attempt goes to and checks
its VAPID version: where any cannot open yet (as `01` MSG-SEND-006 says of a value) or
records a version this process does not hold, the whole delivery is held for a key and
nothing is called; a subscription answering `Erased` is removed, and one that does not
open under its binding (`01` MSG-SEND-018) is removed and raises `content-rejected`,
neither being called; where no live subscription is left, the delivery ends
`nosubscription`. Each request to one subscription is then one provider call, made with
a `PushRequest.WebPush` (`04` MSG-LIB-002), under its own timeout and claim renewal
(`01` MSG-SEND-007), the renewal's transaction checking that the subscription's row
still exists and still holds the delivery's person, and skipping that call where it does
not, so an endpoint moved to another person during an attempt receives none of it (a
skipped call is not recorded, the renewal is still made, and the subscription counts as
gone for the outcome, its row left as it is); each call is recorded with its
subscription's row, a link set to null when that subscription is removed, and its answer
is recorded as it comes, by an update conditional on the claim:

| Answer | Class | Reason |
|---|---|---|
| 200, 201, 202 | `Accepted` (a 201 means the service took the message, never that it was shown, RFC 8030 section 5) | |
| 404, 410 | `Gone`: the subscription is removed | `gone` |
| 400 | `Permanent`, counted towards `push-auth-failing`, since a push service answers it for a rejected authorization too | `badrequest` |
| 401, 403 | `Transient`, counted towards `push-auth-failing` | `authfailed` |
| 413 | `Permanent` | `messagerefused` |
| 429 | `Transient`, honouring `Retry-After` | `ratelimited` |
| A 5xx, a timeout, a connection refused or broken | `Transient` | `temporary`, `timeout`, `connectionfailed` |
| Any other status (a redirect included, since the client follows none) | `Permanent` | `badrequest` |

A 400, 401 or 403 is counted per push service (`08` section 2) in a counter row per
minute (`INSERT ... ON CONFLICT DO UPDATE SET count = count + 1`); more than
`Hermes:Push:AuthFailThreshold` (default 20) within `Hermes:Push:AuthFailWindow`
(default 1 hour), summed over that service's rows, raise `push-auth-failing` for it, so
one subscription made under another application server key cannot hold the alert raised.

The delivery's outcome over its subscriptions, the rows tried from the top and the first
that fits applying, the first, and the subscriptions the second leaves out, judged over
all of the delivery's recorded calls, and the others over its last attempt's:
- one or more accepted, in any attempt: the delivery ends `sent`, and no subscription is
  tried again for it;
- an attempt recorded `claimlost` (`01` MSG-SEND-007), or one answered transient at a
  check, which made no call: the next goes to every live subscription of the person that
  answered no call of the delivery permanent or gone; where none is left, the delivery
  ends `failed` with the reason of its last permanent answer where one was given, and
  `nosubscription` otherwise;
- none accepted and one or more transient: the attempt is transient, and the next goes
  only to those subscriptions that answered transient and are still live and the
  person's when it is dispatched; where none is, the delivery ends `nosubscription`;
- every subscription gone: the delivery ends `nosubscription`;
- otherwise: the delivery ends `failed`, with the reason of its last permanent answer.

*Source: M-007, M-009*

**Acceptance criteria**
1. A 410 removes the subscription, and the person's next push does not use it.
2. A 403 leaves the delivery waiting; 21 answers of 400, 401 or 403 from one push
   service within an hour raise the alert for that service, and 20 do not.
3. With two subscriptions answering 201 and 503, the delivery ends `sent` after one
   attempt; answering 503 and 410, the next attempt goes to the first alone, and to none
   if it has moved to another person.

---

**MSG-PUSH-006** — A subscription past the expiry its browser gave SHALL NOT be used,
and SHALL be removed.

**Values (M-009).** The subscription job (`06` MSG-OPS-004) removes subscriptions past
their expiry every `Hermes:Jobs:SubscriptionsInterval` (default 1 hour); one idle past
`Hermes:Push:IdleRetention` is removed by the retention job (`01` MSG-SEND-019).

*Source: M-009*

**Acceptance criteria**
1. A subscription whose `expirationTime` has passed receives nothing, and is gone after
   the subscription job's next run.

---

**MSG-PUSH-007** — Each push request SHALL be signed with the VAPID key version its
subscription was made with, so that VAPID keys rotate without losing a subscription.

**Values (M-009).** The VAPID keys are the versions of the secret `pushVapidKeys`, each
a P-256 private key as its 32-byte scalar; each version's public key is derived from it
(an uncompressed point, 65 bytes). `GET /push/key` (`05` MSG-API-004) answers the public
key of the version new subscriptions use (`06` MSG-OPS-008). The browser posts the key
it subscribed with, and the subscription records the held version whose public key it
is. A key the key table records for a version in use that this process does not hold is
answered 503 `messaging.request.unavailable`, so the page tries again; a key whose
version is retired, held or not, or that no version has, is refused with
`messaging.push.keyunknown`, so the page subscribes again under the key `GET /push/key`
answers, since a subscription signed for with another key would be answered as gone and
one recorded under a retired version could lose its key (`06` MSG-OPS-008). A request
for a subscription whose version this process does not hold holds the delivery for a key
(`01` MSG-SEND-006). A version is retired only once no subscription records it (`06`
MSG-OPS-008).

*Source: M-007, M-009*

A push service ties a subscription to the public key it was made with and refuses, or
answers "gone" for, a request signed with another (`decision-log.md` M-009).

**Acceptance criteria**
1. After a new version is added and made the one new subscriptions use, a subscription
   made under the old version still receives pushes signed with the old key, and a new
   subscription is made and signed under the new one.
2. A subscription posted with the old key after the switch records the old version.
3. A version that is not a valid P-256 scalar stops the start.

---

### 4.1 Push to native phone apps (designed for, not built)

**MSG-PUSH-008** — A later release SHALL add push to native apps (Firebase Cloud
Messaging for Android, Apple Push Notification service for iOS) as further push
providers, with no change to a send.

**Values (M-004).** Each push provider declares the platform it serves
(`IPushProvider.Platform`, `04` MSG-LIB-002), one provider per platform, and a request
goes to the provider of its subscription's platform; the first release registers the
`webpush` one. `PushRequest` is a type with cases, the first release's being `WebPush`
with its endpoint, keys and VAPID version, so a native case adds its own members (a
device token) and changes none. Nothing in the first release depends on every
subscription being a browser's.

*Source: M-004*

---

## 5. Further channels (designed for, not built)

**MSG-CHAN-002** — A later release SHALL add a channel (WhatsApp, the in-application
list, any other) as a provider contract, its kinds' templates and its destination, with
no change to how a host sends.

**Values (M-004).** A channel is a name in the closed set of `08` section 2, a provider
contract of `Hermes.Core` (a new interface, `04` MSG-LIB-006), the destination a send
carries for it, and the templates a kind declares for it; the delivery, its checks, its
retries, its statuses and its retention are the same for every channel. A channel whose
provider sends only pre-approved templates (WhatsApp's) names the provider's template in
the kind's declaration for that channel.

*Source: M-004*
