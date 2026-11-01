# 05 — API contract

The endpoints the library ships. Each group is mapped by its own call under the prefix
the host chooses (`04` MSG-LIB-004); the routes below are relative to that prefix. Every
value named `Hermes:...` is configuration (`08` section 4).

Each hub of `Hermes.Conversations` (`09` MSG-CONV-008) is listed here beside the routes
of its group, with its route, its method names and its payload shapes, which are public
surface as the routes and wire shapes are (`04` MSG-LIB-006): the endpoint contract test
finds them from the phase that adds the hub (`07` MSG-ENG-006), and from plan phase 21
the release gate reads them and its baseline records them (M-031).

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

*Source: M-005, M-026*

**Acceptance criteria**
1. A request with no signed-in person to a preferences, subscription or in-app route
   that needs one is 401.
2. No route of those groups reads or changes another person's preferences, subscriptions
   or in-app entries.

---

## 2. Preferences (`MapHermesPreferences`)

**MSG-API-003** — The preferences group SHALL let the person signed in read and set, per
optional category and channel, whether they receive it.

| Method and route | Body | Answers |
|---|---|---|
| `GET /preferences` | | **200** `{ "categories": [ { "name", "essential", "channels": [ { "channel", "on" } ] } ] }`: every declared category with the channels its kinds use, in declaration order, the in-app channel left out where `Hermes:InApp:AlwaysOn` is on (`02` MSG-INAPP-004) |
| `PUT /preferences/{category}/{channel}` | `{ "on": true \| false }` | **204**; **404** `messaging.preference.unknown` for a category not declared or a channel its kinds do not use; **422** `messaging.preference.essential` for an essential category, whatever `on` says; **422** `messaging.preference.fixed` for the in-app channel where `Hermes:InApp:AlwaysOn` is on (`02` MSG-INAPP-004); **503** `messaging.request.unavailable` with `Retry-After` where the person's locks are not had within `Hermes:Dispatch:Timeout` (`01` MSG-SEND-010) |

Both routes need a signed-in person (MSG-API-002). A category's name is its declared
name; how it reads to the person is the host's frontend's to say. Turning a category on
does not withdraw a suppression; the host withdraws it with the person's address (`01`
MSG-SEND-012).

*Source: M-006, M-026*

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

---

## 6. The development inbox (`MapHermesDevelopmentInbox`)

**MSG-API-007** — The development inbox's group SHALL serve its page and answer, as
JSON, the messages the inbox holds, in Development alone.

| Method and route | Answers |
|---|---|
| `GET /` | **200** the page (`text/html`, `02` MSG-DEV-004) |
| `GET /assets/inbox.js`, `GET /assets/inbox.css` | **200** the page's script and style |
| `GET /messages` | **200** `{ "messages": [ <summary> ] }`, newest first; query `to`, `person`, `channel`, `app`, `kind`, `after`, `limit` |
| `GET /messages/latest` | **200** `<message>`, the newest that matches; query as `GET /messages` without `limit`, and `wait`; **404** `messaging.inbox.notfound` where none matches once `wait` has passed |
| `GET /messages/{id}` | **200** `<message>`; **404** `messaging.inbox.notfound` |
| `GET /messages/{id}/eml` | **200** the email's `.eml` (`message/rfc822`, as an attachment named `<id>.eml`); **404** `messaging.inbox.notfound` for an id the inbox does not hold or that is not an email |
| `GET /messages/{id}/html` | **200** the email's HTML as `02` MSG-DEV-005 serves it (`text/html`); **404** `messaging.inbox.notfound` where the inbox holds no such email or it has no HTML |
| `DELETE /messages` | **204**, every record and `.eml` deleted; **400** `messaging.request.malformed`, `details.member` `X-Hermes-Inbox`, without the header `X-Hermes-Inbox: clear` |

Every route also answers **403** `messaging.inbox.notlocal` as `02` MSG-DEV-005 gives,
and a query parameter outside its rule **400** `messaging.request.malformed`,
`details.member` naming it. The query parameters: `to`, an email address (matched in any
case) or a phone number (matched exactly); `person`; `channel`, a channel's `08`
spelling; `app`; `kind`; `after`, an instant in ISO 8601 with its offset, messages
captured after it alone; `limit`, 1 to 1,000, default 100; `wait`, whole seconds from 0
to 30, default 0, for which `GET /messages/latest` waits for a match before it answers
404.

| Shape | Members |
|---|---|
| `<summary>` | `{ "id", "channel", "app", "at", "to", "person", "kind", "category", "language", "summary" }` |
| `<message>` | The summary's members, and `"text"`, `"links"` (an array of strings), `"email"`, `"sms"` and `"push"`, two of the last three `null` |
| `email` | `{ "from": { "address", "displayName" }, "replyTo", "subject", "text", "html", "messageId", "unsubscribe", "headers": [ { "name", "value" } ] }`, `replyTo` as `from` or `null` |
| `sms` | `{ "sender", "text", "encoding", "segments", "deliveryReference" }` |
| `push` | `{ "service", "notification": { "title", "body", "url", "tag", "lang", "dir", "icon", "badge" }, "ttl", "urgency", "topic" }` |

`id` is the record's 32 hexadecimal digits; `at` an instant in ISO 8601 UTC with
milliseconds; `segments` and `ttl` (whole seconds) are numbers; `channel`, `encoding`,
`service` and `urgency` are their `08` spellings; every member a message lacks is
`null`. The members mean what `02` MSG-DEV-003 gives. The record a `<message>` is read
from is the same JSON (`02` MSG-DEV-002).

*Source: M-024*

**Acceptance criteria**
1. An email, an SMS and a push captured in Development are each answered by
   `GET /messages/{id}` with their members, and listed by `GET /messages` newest first.
2. `GET /messages/latest?to=...&wait=10`, asked before the message is sent, answers it
   as soon as it is captured; with nothing sent it answers 404 once ten seconds have
   passed.
3. Outside Development the group answers no route.

---

## 7. The in-application list (`MapHermesInApp`)

**MSG-API-008** — The in-app group SHALL let the person signed in read their list a page
at a time and their unread count, and mark one entry, or all of theirs, read.

| Method and route | Body | Answers |
|---|---|---|
| `GET /inapp` | | **200** `{ "entries": [ <entry> ], "next" }`, newest first, `next` the cursor of the next page or `null`; query `cursor`, and `size`, 1 to 100, default 20; **400** `messaging.inapp.cursorinvalid` for a cursor that does not read; **400** `messaging.request.malformed`, `details.member` `size`, for a size outside its rule; **503** `messaging.request.unavailable` with `Retry-After` where an entry's value cannot open yet, a protector call throws or does not answer, or the person's locks are not had, within `Hermes:Dispatch:Timeout` (`02` MSG-INAPP-003) |
| `GET /inapp/unread` | | **200** `{ "unread": <number> }`; **503** as for `GET /inapp` where the person's locks are not had |
| `POST /inapp/{id}/read` | `{ }` | **204**, an entry already read included; **404** `messaging.inapp.unknown` for an `id` that does not read, an entry that does not exist, or another person's; **503** as for `GET /inapp` where the person's locks are not had |
| `POST /inapp/read` | `{ }` | **204**, every unread entry of the person marked read; **503** as for `GET /inapp` where the person's locks are not had |

Every route needs a signed-in person (MSG-API-002). The two `POST` routes read their
empty object as `application/json` alone (MSG-API-001), so a cross-site form cannot
reach them (`10` MSG-THR-001). `GET /inapp` reads in the language of the request's
`Accept-Language` (RFC 9110): of its tags in order of weight, the first that matches a
declared language as `01` MSG-SEND-003 matches a send's is passed to `ListAsync`, and
none where none matches or the header is absent, so the entries read in
`Hermes:DefaultLanguage` (`02` MSG-INAPP-002).

| Shape | Members |
|---|---|
| `<entry>` | `{ "id", "kind", "category", "writtenAt", "readAt", "language", "title", "body", "target", "targetValues" }` |

`id` is the entry's identifier, lower-case and hyphenated; `writtenAt` and `readAt` are
instants in ISO 8601 UTC with milliseconds; `language` is the declared language the
entry reads in; `readAt`, `body` and `target` are `null` where the entry has none;
`targetValues` is an object of the target values by name, each a string, a number, a
boolean or an instant as `writtenAt` is written, and `{}` where the kind declares none.
`title` and `body` are plain text, which the host's frontend shows as text, never as
markup.

**What the host's frontend does.** It shows the list and its count from these routes,
and builds each entry's link from its `target` and `targetValues`, which name a place in
the host's own screens. A host that shows the list live refreshes it from its own
realtime channel, which the observer's notices of `sent` and `read` feed (`01`
MSG-SEND-017); the library serves no socket.

*Source: M-026*

**Acceptance criteria**
1. A signed-in person's `GET /inapp` with `Accept-Language: ar, en;q=0.5` answers their
   entries newest first in Arabic, and none of another person's.
2. `POST /inapp/{id}/read` for another person's entry answers 404 and changes nothing;
   for the person's own, 204, and `GET /inapp/unread` then answers one fewer.
3. A `POST /inapp/read` sent as a form is answered 415 and marks nothing.

---

## 8. Staff conversations (`MapHermesStaffConversations`)

**MSG-API-009** — The staff conversations group of `Hermes.Conversations` SHALL let the
person signed in read and write the direct and group conversations `09` MSG-CONV-006
lets them, each route thin over one operation of `IConversations` (`04` MSG-LIB-002).

| Method and route | Body | Answers |
|---|---|---|
| `GET /conversations` | | **200** `{ "conversations": [ <conversation> ], "next" }`: the person's direct conversations they have not left, the existing group conversations of their scopes and the open support conversations assigned to them, newest activity first, `next` the cursor of the next page or `null`; query `cursor`, and `size`, 1 to 100, default 20; **400** `messaging.request.malformed`, `details.member` `cursor` or `size`, for a cursor that does not read or a size outside its rule; **503** as for `GET /inapp` |
| `POST /conversations/direct` | `{ "participants" }` | **201** `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }`; **400** `messaging.conversation.invalid`, `details.field` `participants`, for none or more than 15 others, a reference named twice, or the person themselves; **503** as for `GET /inapp` |
| `GET /conversations/group/{kind}/{reference}` | | **200** `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }`: the scope's conversation, created the first time a member opens it; **400** `messaging.conversation.invalid`, `details.field` `kind`, for a kind that does not read; **400** `messaging.request.malformed`, `details.member` `reference`, for a reference outside 1 to 128 characters; **404** `messaging.conversation.notfound` for a person who is not a member; **503** as for `GET /inapp` |
| `GET /conversations/{id}` | | **200** `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }`; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `GET /conversations/{id}/messages` | | **200** `{ "messages": [ <message> ] }`, newest first, or oldest first from `after`; query `before` or `after`, a message's identifier, and `size`, 1 to 100, default 50; **400** `messaging.request.malformed`, `details.member` `before`, `after` or `size`, for one outside its rule; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `POST /conversations/{id}/messages` | `{ "text", "type", "replyTo", "mentions" }` | **201** `{ "id", "authorKind", "author", "type", "text", "replyTo", "mentions", "sentAt", "editedAt", "deletedAt", "erased" }`; **400** `messaging.message.invalid`, `details.field` `text`, `mentions`, `replyTo` or `type`; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `PUT /conversations/{id}/messages/{messageId}` | `{ "text" }` | **204**; **400** `messaging.message.invalid`, `details.field` `text`; **403** `messaging.message.notauthor` for another person's message; **404** `messaging.message.notfound`, or `messaging.conversation.notfound`; **409** `messaging.message.deleted` for a deleted message; **503** as for `GET /inapp` |
| `DELETE /conversations/{id}/messages/{messageId}` | | **204**, a message already deleted included; **403** `messaging.message.notauthor` for another person's message; **404** `messaging.message.notfound`, or `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `POST /conversations/{id}/read` | `{ "messageId" }` | **204**; **404** `messaging.message.notfound` for a message the conversation does not hold, or `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `PUT /conversations/{id}/mute` | `{ "muted" }` | **204**; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `POST /conversations/{id}/participants` | `{ "participants" }` | **204**; **400** `messaging.conversation.invalid`, `details.field` `type`, for a group conversation; **404** `messaging.conversation.notfound`; **409** `messaging.conversation.full` past 16 participants; **503** as for `GET /inapp` |
| `POST /conversations/{id}/leave` | `{ }` | **204**; **400** `messaging.conversation.invalid`, `details.field` `type`, for a group conversation; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `GET /support/queue` | | **200** `{ "conversations": [ <conversation> ] }`: the topic's queued conversations, oldest first, or an empty list for a person outside the topic's scope; query `topic`; **400** `messaging.request.malformed`, `details.member` `topic`, for a topic not configured |
| `PUT /support/availability` | `{ "available" }` | **204**; **503** as for `GET /inapp` |
| `POST /support/conversations/{id}/transfer` | `{ "person", "topic" }`, one of the two | **204**; **400** `messaging.conversation.invalid`, `details.field` `person` for a person outside the topic's scope, or `topic` for a topic not configured; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `POST /support/conversations/{id}/close` | `{ }` | **204**; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `POST /support/conversations/{id}/block` | `{ "until" }` | **204**; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |
| `DELETE /support/conversations/{id}/block` | | **204**; **404** `messaging.conversation.notfound`; **503** as for `GET /inapp` |

| Shape | Members |
|---|---|
| `<conversation>` | `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }` |
| `<participant>` | `{ "id", "person", "online" }` |
| `<message>` | `{ "id", "authorKind", "author", "type", "text", "replyTo", "mentions", "sentAt", "editedAt", "deletedAt", "erased" }` |

A conversation is answered as `<conversation>` and a message as `<message>`, the routes
above writing each out. `id`, `replyTo` and `messageId` are identifiers, lower-case and
hyphenated; `sentAt`, `editedAt`, `deletedAt` and `lastAt` are instants in ISO 8601 UTC
with milliseconds, `lastAt` the last message's, or the conversation's creation where it
has none; `type`, `kind` and `authorKind` are their `08` spellings; `scope` is `null`
but for a group conversation; `participants` holds a direct conversation's participants
who have not left, and is `[]` for a group or support conversation; `online` is whether
the participant holds a staff-hub connection (`09` MSG-CONV-006); `topic`, `status`,
`place` and `offline` are a support conversation's (its topic's name, its status's `08`
spelling, the visitor's place in the queue, `null` once it is assigned, and whether it
came through the offline form, `09` MSG-CONV-002), and `null` for another; `person` and
`mentions` are person references; `authorKind` is `person`, `visitor`, or `library` for
an `automatic` or `system` message, and `author` is the person reference where it is
`person`, `null` otherwise and once the author is erased (`09` MSG-CONV-009); `text` is
`null` once the message is deleted or its author erased, and `erased` says which;
`replyTo`, `editedAt` and `deletedAt` are `null` where the message has none. In a
request, `type` is optional and defaults to `text`, and `replyTo` and `mentions` are
optional; `until` is an instant, or `null` for a block until it is lifted.

Every route needs a signed-in person (MSG-API-002), the one `IRecipientIdentity`
answers. A route naming a conversation the person may not read, or by an `id` that does
not read, answers 404 `messaging.conversation.notfound`, never 403 (`09` MSG-CONV-006).
The participants and leave routes are for direct conversations alone. Bodies and
refusals are the engine's, as MSG-API-001 gives. `MapHermesStaffConversations` answers
the group's `RouteGroupBuilder`, to which the host adds the authorization that decides
who may use it (`09` MSG-CONV-006). The support routes (M-034) work support's queue
(`09` MSG-CONV-002, MSG-CONV-011); a staff member writes `text` or `note` in a support
conversation they are assigned through the messages route, and a visitor never sees a
`note` (`09` MSG-CONV-001). A member of the topic's scope reads a support conversation
of the topic, transfers, closes and blocks it; a member who is not the assignee writing,
and anyone else, is answered 404 `messaging.conversation.notfound` (`09` MSG-CONV-002).

**The staff hub (M-033).** The group also maps the staff hub at `/live`, so the host's
authorization covers it too (`09` MSG-CONV-008). The person comes from
`IRecipientIdentity` when a client connects, and a connection without one is refused.
Writes stay HTTP: the client sends `Typing` alone. A client that reconnects reads what
it missed from the messages route with `after`, since every push follows the store.

| Hub method | Sent by | Arguments |
|---|---|---|
| `message` | The server | `conversationId`, `message`: a new message, as `<message>` |
| `messageChanged` | The server | `conversationId`, `message`: an edit or a delete, as `<message>` |
| `read` | The server | `conversationId`, `participantId`, `messageId` |
| `typing` | The server | `conversationId`, `participantId` |
| `presence` | The server | `participantId`, `online` |
| `conversation` | The server | `conversation`: one added to the person's list, as `<conversation>` |
| `Typing` | The client | `conversationId` |

*Source: M-032, M-033, M-034*

**Acceptance criteria**
1. Each route does what its operation does, and the endpoint contract test finds exactly
   these routes.
2. The contract test finds the hub's route and methods.

---

## 9. Public conversations (`MapHermesPublicConversations`)

**MSG-API-010** — The public conversations group of `Hermes.Conversations` SHALL let a
visitor or a signed-in customer start a support conversation, or leave a message through
the offline form, and read and write their conversation (`09` MSG-CONV-001,
MSG-CONV-002).

| Method and route | Body | Answers |
|---|---|---|
| `GET /support/topics` | | **200** `{ "topics": [ { "name", "open" } ] }`: each topic, `open` whether it is within its hours with a member available, or offline |
| `POST /support/notice` | `{ "version" }` | **204**; **403** `messaging.support.blocked`; **429** `messaging.support.limited` with `Retry-After` where the visitor or the source address is already at its starts limit, counting nothing |
| `POST /support/conversations` | `{ "topic", "text" }` | **201** `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }`; **409** `messaging.support.offline` outside the topic's hours, or within them while no member is available; **409** `messaging.support.noticerequired` for a visitor before an acknowledgement of the current notice; **409** `messaging.support.alreadyopen`, `details.conversation` the open one's identifier, for a second start on a topic with one open; **403** `messaging.support.blocked`; **429** `messaging.support.limited` with `Retry-After` |
| `POST /support/offline` | `{ "topic", "email", "text" }` | **201** `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }`; **409** `messaging.support.noticerequired` for a visitor before an acknowledgement of the current notice; **403** `messaging.support.blocked`; **429** `messaging.support.limited` with `Retry-After` |
| `GET /support/conversations/current` | | **200** `{ "id", "type", "scope": { "kind", "reference" }, "participants": [ <participant> ], "unread", "muted", "lastAt", "topic", "status", "place", "offline" }`: the newest open conversation, with the visitor's place; **404** `messaging.conversation.notfound` where none is open |
| `GET /support/conversations/{id}/messages` | | **200** `{ "messages": [ <message> ] }`, newest first, or oldest first from `after`, never a `note`; query `before` or `after`, and `size`, as for the staff messages route; **400** `messaging.request.malformed`, `details.member` `before`, `after` or `size`, for one outside its rule; **404** `messaging.conversation.notfound` |
| `POST /support/conversations/{id}/messages` | `{ "text" }` | **201** `{ "id", "authorKind", "author", "type", "text", "replyTo", "mentions", "sentAt", "editedAt", "deletedAt", "erased" }`; **400** `messaging.message.invalid`, `details.field` `text`; **404** `messaging.conversation.notfound`; **409** `messaging.support.noticerequired` for a visitor before an acknowledgement of the current notice; **403** `messaging.support.blocked`; **429** `messaging.support.limited` with `Retry-After` |
| `POST /support/conversations/{id}/read` | `{ "messageId" }` | **204**; **404** `messaging.message.notfound` for a message the conversation does not hold, or `messaging.conversation.notfound` |
| `POST /support/conversations/{id}/close` | `{ }` | **204**; **404** `messaging.conversation.notfound` |

No route needs a signed-in person. Where `IRecipientIdentity` answers a person, the
conversation is that person's, with no cookie and no notice; otherwise the visitor is
known by the cookie `messaging_visitor`, drawn on the first call that needs one (`09`
MSG-CONV-001). A route naming a conversation that is not the visitor's or the
customer's, or by an `id` that does not read, answers 404
`messaging.conversation.notfound`. The bodies are the staff group's shapes, `<message>`
never holding a `note` (`09` MSG-CONV-001). Bodies and refusals are the engine's, as
MSG-API-001 gives. `MapHermesPublicConversations` answers the group's
`RouteGroupBuilder`.

**The public hub.** The group also maps the public hub at `/live`, authenticated by the
same cookie (`09` MSG-CONV-008). Its payloads are the staff hub's shapes.

| Hub method | Sent by | Arguments |
|---|---|---|
| `message` | The server | `conversationId`, `message`: a new message, as `<message>`, never a `note` |
| `messageChanged` | The server | `conversationId`, `message`: an edit or a delete, as `<message>` |
| `typing` | The server | `conversationId`, `participantId` |
| `place` | The server | `position`: the visitor's place in the queue (`09` MSG-CONV-002) |
| `assigned` | The server | `conversationId` |
| `closed` | The server | `conversationId` |
| `Typing` | The client | `conversationId` |

*Source: M-034*

**Acceptance criteria**
1. The contract test finds exactly the public routes and hub.
