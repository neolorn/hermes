# 05 — API contract

The endpoints the library ships. Each group is mapped by its own call under the prefix
the host chooses (`04` MSG-LIB-004); the routes below are relative to that prefix. Every
value named `Hermes:...` is configuration (`08` section 4).

---

## 1. Conventions

**MSG-API-001** — Every JSON body SHALL be read and written through the library's own
serializer contexts, under their own options, never through the application's, and every
request body SHALL be bounded.

**Values (M-012).** Member names are camelCase, read case-sensitively; enumerations are
their `08` spelling; numbers are JSON numbers; an unknown member is ignored. A JSON body
is read only as `application/json`, any other media type being **415**
`messaging.request.mediatype`. A body over 16 KiB is **413**
`messaging.request.toolarge`. A body that does not read is **400**
`messaging.request.malformed`, `details.member` naming the member where one is at fault.
A refusal is answered as `{ "code": "<code>", "details": { ... } }` with the status `08`
section 1 gives the code. A host's settings for its own endpoints change nothing here. A
503 carries `Retry-After` of `Hermes:Dispatch:Interval` in whole seconds, at least 1,
and a 429 the whole seconds until the source's minute window frees.

*Source: M-012*

**Acceptance criteria**
1. With the application's JSON options set to another naming policy, the library's
   endpoints read and answer camelCase.
2. A malformed body is answered 400, a form body to a JSON route 415, and a 17 KiB body
   413, each with its code.

---

**MSG-API-002** — An endpoint that needs a signed-in person SHALL answer **401**
`messaging.request.unauthenticated` where the identity seam answers none, and SHALL act
only for the person it answers.

*Source: M-005*

**Acceptance criteria**
1. A request with no signed-in person to a preferences or subscription route that needs
   one is 401.
2. No route of those groups reads or changes another person's preferences or
   subscriptions.

---

## 2. Preferences (`MapHermesPreferences`)

**MSG-API-003** — The preferences group SHALL let the person signed in read and set, per
optional category and channel, whether they receive it.

| Method and route | Body | Answers |
|---|---|---|
| `GET /preferences` | | **200** `{ "categories": [ { "name", "essential", "channels": [ { "channel", "on" } ] } ] }`: every declared category with the channels its kinds use, in declaration order |
| `PUT /preferences/{category}/{channel}` | `{ "on": true \| false }` | **204**; **404** `messaging.preference.unknown` for a category not declared or a channel its kinds do not use; **422** `messaging.preference.essential` for an essential category, whatever `on` says; **503** `messaging.request.unavailable` with `Retry-After` where the person's locks are not had within `Hermes:Dispatch:Timeout` (`01` MSG-SEND-010) |

Both routes need a signed-in person (MSG-API-002). A category's name is its declared
name; how it reads to the person is the host's frontend's to say. Turning a category on
does not withdraw a suppression; the host withdraws it with the person's address (`01`
MSG-SEND-012).

*Source: M-006*

**Acceptance criteria**
1. A person's `PUT` turning a category's SMS off is read back by `GET` and stops their
   SMS of that category (`01` MSG-SEND-010).

---

## 3. Push subscriptions (`MapHermesPushSubscriptions`)

**MSG-API-004** — The push subscription group SHALL give a browser the key to subscribe
with, and let the person signed in register and remove a subscription.

| Method and route | Body | Answers |
|---|---|---|
| `GET /push/key` | | **200** `{ "publicKey": "<base64url>" }`: the VAPID public key of the version new subscriptions use (`02` MSG-PUSH-007), which the browser passes to `subscribe` as `applicationServerKey`. Needs no signed-in person: the route calls `AllowAnonymous()` |
| `POST /push/subscriptions` | The browser's `PushSubscription.toJSON()` with the key it subscribed with added: `{ "endpoint", "expirationTime", "keys": { "p256dh", "auth" }, "applicationServerKey" }`, the last the base64url of `subscription.options.applicationServerKey` | **204**; **422** `messaging.push.endpointnotallowed` (`02` MSG-PUSH-004); **422** `messaging.push.keyunknown` for a key whose version is retired, held or not, or that no version has (`02` MSG-PUSH-007); **400** `messaging.request.malformed` where `endpoint`, a key or `applicationServerKey` is missing or does not read; **503** `messaging.request.unavailable` with `Retry-After` where a key version in use is not held by this process, the posted key is one the key table records for such a version (`02` MSG-PUSH-007), or a content key's use is refused (`06` MSG-OPS-008), or where the person's locks are not had, or a protector call throws or does not answer, within `Hermes:Dispatch:Timeout` (`02` MSG-PUSH-003) |
| `DELETE /push/subscriptions` | `{ "endpoint" }` | **204**, whether or not the person held it; a subscription another person holds is left as it is; **503** `messaging.request.unavailable` with `Retry-After` where a key version in use is not held by this process (a removal takes no person's lock) |

The two subscription routes need a signed-in person (MSG-API-002).

**What the host's frontend and service worker do.** The page subscribes with the key
`GET /push/key` answers and posts the subscription with that key added, and the service
worker, on `pushsubscriptionchange`, posts the new subscription the same way, with the
key it subscribed with; on a 422 `messaging.push.keyunknown` the page unsubscribes and
subscribes again with the key `GET /push/key` answers. The `data` of a push event is the
JSON object of `02` MSG-PUSH-002; a service worker shows it with `showNotification`
(`payload.title` as the title; `body`, `lang`, `dir`, `tag`, `icon` and `badge` as the
options of the same names; `url` in the options' `data`) and opens `url` on
`notificationclick`. Safari revokes push permission from a site whose push shows no
notification (`decision-log.md` M-009), so a service worker shows every push it
receives.

*Source: M-007, M-009*

**Acceptance criteria**
1. A subscription posted by a signed-in person receives that person's push deliveries.
2. A subscription deleted receives nothing more.
3. `GET /push/key` answers 200 with no signed-in person.
4. A subscription posted with a key no version has, or a retired version's, is refused
   422 and stored nowhere; one posted with the key of a version in use that this process
   does not hold is answered 503.

---

## 4. Unsubscribe (`MapHermesUnsubscribe`)

**MSG-API-005** — The unsubscribe group SHALL suppress a destination for a category on
RFC 8058's one-click `POST` with a valid token, and SHALL offer a person who follows the
link a page that asks before it does.

| Method and route | Body | Answers |
|---|---|---|
| `POST /unsubscribe/{token}` | `List-Unsubscribe=One-Click`, as `multipart/form-data` or `application/x-www-form-urlencoded` (RFC 8058), or the page's form | The destination is suppressed for the category (`01` MSG-SEND-012), whatever the body. **200**: where the form carries the field `page`, the page `unsubscribed`; otherwise an empty body, whether or not the destination was already suppressed. **404** for a token that does not verify, suppressing nothing: the page `unsubscribeinvalid` in the default language where the form carries `page`, otherwise `messaging.unsubscribe.invalid`. **413** `messaging.request.toolarge` for a body over 16 KiB, suppressing nothing. **503** `messaging.request.unavailable` with `Retry-After` for a token naming a version that is in use but not held by this process, or whose format byte is one kept for a later release (`06` MSG-OPS-008), never 404; the first alone raises `key-unavailable` (`06` MSG-OPS-006), since anyone can write the second |
| `GET /unsubscribe/{token}` | | **200**: the page `unsubscribe` (`03` MSG-TPL-006) in the language the token records, whose form posts to the same address with the field `page`; it suppresses nothing itself. **404**: the page `unsubscribeinvalid` in the default language, for a token that does not verify. **503** as for `POST` |

**The token.** The base64url, without padding, of:

| Bytes | Field |
|---|---|
| 1 | Format, `0x01`; `0x02` to `0x0F` are kept for later releases |
| 4 | The `tokenKeys` version it is signed under, big-endian |
| 4 | The `hashKeys` version its hash is made under, big-endian |
| 1 + n | The category's name: its length, then its UTF-8 bytes |
| 1 + n | The language: its length, then its UTF-8 bytes |
| 4 | The day it is dated from, the later of the send's recording and its `NotBefore` (`02` MSG-EMAIL-006), in days since 1970-01-01 UTC, big-endian |
| 32 | The destination's keyed hash (`06` MSG-OPS-008, domain `email`) |
| 32 | HMAC-SHA-256 of every byte before it, under that `tokenKeys` version |

A token verifies when it is in its canonical encoding (decoding it and encoding the
bytes again gives the same text), decodes to this layout with format `0x01`, both
versions are held, its HMAC matches in constant time, the day it is dated from is no
more than `Hermes:Unsubscribe:TokenLifetime` (default 2 years) before the current day,
and its category is a declared optional category. It carries no destination, so a token
in a forwarded email suppresses only the address it was sent to, by hash. Before the
transaction that writes the suppression, the `POST` writes, in its own transaction, the
current instant as the last-made instant of the token's `hashKeys` version where the one
recorded is older, clearing its retired mark (`06` MSG-OPS-008), so a suppression that a
token kept valid by a raised `Hermes:Unsubscribe:TokenLifetime` makes never lands under
a version the retention job has marked.

The routes need no signed-in person, set no cookie and never redirect (RFC 8058 section
3.2), and answer the same whether or not the destination was already suppressed. Each
page is answered with `Cache-Control: no-store`, `Referrer-Policy: no-referrer`,
`X-Content-Type-Options: nosniff` and a `Content-Security-Policy` of
`default-src 'none'`, `style-src 'unsafe-inline'`, `form-action 'self'`,
`base-uri 'none'` and `frame-ancestors 'none'`, so the token in the address leaves the
page with nothing it loads.

*Source: M-005, M-006, M-007, M-010*

A `GET` never unsubscribes, since mail scanners open links to check them.

**Acceptance criteria**
1. The one-click `POST` suppresses the destination for the category, and answers the
   same, with no cookie and no redirect, whether or not it was already suppressed.
2. A `GET` of the same address suppresses nothing; posting its form suppresses and
   answers the `unsubscribed` page.
3. A token altered in any character (save a format byte changed to one kept for a later
   release, answered 503 and raising no alert), one past its lifetime, and one naming a
   category no longer declared or now essential are 404 and suppress nothing.
4. A token made under older `tokenKeys` and `hashKeys` versions still verifies after new
   ones are added, and the suppression it makes applies to later sends.
5. Each page carries the four headers above, and a `POST` body over 16 KiB is 413.
6. A token whose `hashKeys` version the retention job has marked retired, still held and
   within a `TokenLifetime` raised since, suppresses under that version and clears its
   mark.

---

## 5. SMS delivery reports (`MapHermesSmsReports`)

**MSG-API-006** — The report group SHALL take the SMS provider's delivery reports and
answer each as the provider expects, whatever its effect.

| Method and route | Answers |
|---|---|
| `GET /sms/report/{key}` and `POST /sms/report/{key}` | **404**, empty, where `key` does not match (`02` MSG-SMS-004); **429** `messaging.request.ratelimited` with `Retry-After` where the source has sent more than `Hermes:Sms:WrongKeyRate` requests with a wrong key in the last minute; otherwise the response the provider's `ReadReportAsync` gives, **200** with an empty body unless the provider says otherwise; **413** `messaging.request.toolarge` for a body over 16 KiB |

The routes need no signed-in person. The report's effect is `02` MSG-SMS-004's; its
shape is the gateway's own, which the host's provider reads (`02` MSG-CHAN-001). The
mapped group's absolute address, with the key, is what the deployment gives the gateway,
or the provider passes with each message (`Hermes:Sms:ReportBaseAddress`, `02`
MSG-SMS-004). Where the gateway calls only plain `http`, the deployment serves the group
on a listener of its own exempt from HTTPS redirection, and the key then travels in
clear (`10` MSG-THR-001).

The key is in the request's path, which the host's request logging, its tracing and its
reverse proxy record unless told not to, beyond the library's reach; the deployment
redacts the report route's path in each of them (`06` MSG-OPS-007).

*Source: M-008, M-022*

**Acceptance criteria**
1. A report of delivery for a sent SMS moves it to `delivered` and is answered as the
   provider expects.
2. A forged report with the right key is answered as a real one is, and changes nothing.
3. Over `Hermes:Sms:WrongKeyRate` wrong-key requests a minute from one source address, a
   request is answered 429, and a report with the right key from that address is not.
4. A body over 16 KiB is answered 413 and passed to no provider.
