# 09 — Conversations (designed now, built later)

**Release 2.** This chapter fixes the model of conversations, so that the first
release's sending carries what they will need and release 2 adds the
`Hermes.Conversations` package without a breaking change. Release 2 builds the chapter
in plan phases 13 to 21 and is released as `1.0.0-rc.3`, every change it makes to the
surface of `1.0.0-rc.2` being an addition (`04` MSG-LIB-006, M-031). Each requirement's
acceptance criteria come with the phase that builds it, from that phase's decision-log
entry, and are written here then; until then the requirement states design constraints
and carries none. MSG-CONV-012 carries none in any phase: it is checked in review, since
the origin and the message types are open enumerations, so a value added later is
additive. The configuration keys, under `Hermes:Conversations:`, join `08` section 4 in
the phase that adds them, and are checked only where conversations are registered (`06`
MSG-OPS-001); the tables are the engine's (`06` MSG-OPS-002).

---

## 1. What a conversation is

**MSG-CONV-001** — A conversation SHALL be of one of three types, sharing one model:
`support` (a person asking for help, answered by staff from a queue), `group` (staff in
a company-wide or team chat) and `direct` (two or more people in a private chat).

**Values (M-011).** A conversation holds its type, its participants, its messages, its
status and, for `support`, its topic and its assignment. A participant is a person (the
host's person reference, as in `01`) or, in `support` only, a visitor without one,
identified by a reference the library draws and keeps in the visitor's browser. When a
visitor is sent something (a transcript by email), the send's recipient is
`Recipient.Anonymous` with the address the visitor gave; a visitor holds no push
subscription. `Recipient`'s set of cases is open (`04` MSG-LIB-002), so a later case
adds no break. A message holds its author, its text, its attachments, its type (`text`;
`note`, which only staff see; `automatic`; `system`) and its instants (sent, edited,
deleted). Each participant has a read position. Where it runs (which application mounts
the staff side and which the public side) is the host's choice (`00` MSG-OVR-001): the
two sides are separate endpoint and connection groups, mapped separately.

**The tables (M-032).** They live in the engine's folder `Conversations` and its schema,
each under the rule that the engine knows it (`06` MSG-OPS-002):
- `conversations`: its type (`support`, `group`, `direct`); its origin, `web` being the
  only value in release 2; its status; for a `group` conversation, its scope (a kind and
  a reference, unique together, MSG-CONV-006); and its created and last-message
  instants. Support's columns come with plan phase 16 (MSG-CONV-002).
- `participants`: a person in a conversation, with their read position, their mute, and
  the instants they joined and left.
- `messages`: its author, its type, its protected text, the message it replies to, and
  its sent, edited and deleted instants.
- `mentions`: a message's mentioned persons, one row each.

A person reference is held as the in-app list holds an entry's (`02` MSG-INAPP-001).
Each protected value's context follows `08` section 8's form, a message's text being
`messages:<id>:text` (MSG-CONV-009).

**Statuses and types (M-032).** A conversation's status is one of the fixed set of `08`
section 3. A queued support conversation is `open` with no assignee, and `group` and
`direct` conversations are always `open` in release 2; the statuses of desk work
(MSG-CONV-005) are tags, not statuses. The message types, `text`, `note`, `automatic`
and `system`, are an open enumeration, and `call` is not reserved (MSG-CONV-012). In a
`group` or `direct` conversation a person writes `text` alone, and a `note` answers 400.
The library writes no `system` message in plan phase 14.

**Bounds (M-032).** These are fixed values, not configuration keys:
- a text is 1 to 4,000 characters, counted in UTF-16 code units, as .NET counts;
- a message mentions at most 32 distinct persons;
- a direct conversation holds at most 16 participants.

**Read position and replies (M-032).** A read position is the identifier of the last
message read, and it moves forward only. A conversation's unread count is its messages
after it that are neither the person's own nor deleted. A reply names a message of the
same conversation, deleted or not; any other answers 400.

**The visitor (M-034).**
- **The reference.** The public side draws `visitor:` followed by 128 random bits in
  base64url on the first call that needs one.
- **The cookie.** The reference is kept in the cookie `hermes_visitor`: HttpOnly, Secure,
  SameSite Lax, its path the public prefix, renewed on use, and living as long as
  `Hermes:Conversations:Retention:Visitor` (MSG-CONV-010).
- **Storage.** The reference is a bearer credential, so the database holds only its keyed
  hash, under the domain `visitor` (`08` section 8).
- **The public hub.** It is authenticated by the same cookie (MSG-CONV-008).

**A signed-in customer (M-034).** Where `IRecipientIdentity` answers a person on the
public side, the conversation is that person's: no cookie and no notice (MSG-CONV-010),
and its values are protected under their key (MSG-CONV-009).

**Notes in support (M-034).** A staff member writes `text` or `note` in a support
conversation they are assigned, through `05` MSG-API-009's messages route. A visitor
never sees a `note`, in reads or pushes.

*Source: M-011, M-032, M-034*

**Acceptance criteria**
1. Each bound is refused at its edge: a text of 0 or 4,001 characters, a 33rd mention, a
   mention outside the conversation, a reply into another conversation, and a `note` in
   a `group` or `direct` conversation.
2. The read position moves forward only, and the unread counts follow it.
3. The visitor's reference is never stored in clear, and another browser's cookie opens
   nothing.
4. A visitor never receives a `note`.

---

## 2. Support

**MSG-CONV-002** — A support conversation SHALL be queued, by topic, to the first
available staff member with room for it, within the hours the deployment sets.

**Values (M-011).**
- **Availability:** each topic has a weekly schedule in a time zone, with dated
  exceptions (a holiday, a closed day), all configuration.
- **Who answers:** a staff member is available while signed in, set as available, and
  under their limit of open conversations.
- **The queue:** first in, first out per topic; the visitor sees their place.
- **Handing over:** a conversation may be transferred to another staff member or topic.
- **Outside hours, or when nobody can take it:** an offline form takes the message and
  an email address, and becomes a conversation answered by email through sending.

**Topics (M-034).** Each topic is configuration, `Hermes:Conversations:Topics:<name>`
(`08` section 4), with:
- `TimeZone`, an IANA time zone;
- `Hours`, from `Monday` to `Sunday`, each a list of `HH:mm-HH:mm`;
- `Exceptions`, by date, each `Closed` or its own list;
- `Scope`, a kind and a reference for the membership seam (MSG-CONV-006).

The staff of a topic are its scope's members. A topic's display name is the host
frontend's.

**Who answers (M-034).**
- **Available.** A staff member is available while they hold a staff-hub connection
  (MSG-CONV-008), have set themselves available, and hold fewer open support
  conversations than `Hermes:Conversations:Support:Limit` (default 3).
- **Being set available.** Through the staff group's availability route (`05`
  MSG-API-009), stored in the `agents` table. It holds a person reference, so the rule
  that the engine knows each table applies (`06` MSG-OPS-002).
- **Assignment.** It is automatic. When a conversation is queued, or a member frees up,
  the oldest queued conversation of each topic goes to the available member with the
  fewest open conversations. Ties go to the one longest since their last assignment.
- **The queue.** It is first in, first out per topic.
- **The visitor's place.** It is in the conversation's read, and pushed over the public
  hub as `place` (`05` MSG-API-010).

**Offline (M-034).** Outside a topic's hours, or within them while no member is
available, starting a conversation answers 409 `messaging.support.offline`, and the
visitor uses the offline form instead.
- The form takes an email address and the text. It becomes a conversation flagged
  `offline`, queued like any other.
- Staff replies to it are sent through sending, as the kind named by
  `Hermes:Conversations:Support:OfflineReplyKind`, to `Recipient.Anonymous` with that
  address. The key is required where topics are configured.
- The address is a protected value of the conversation.

**Transfer (M-034).** A staff member transfers a conversation to another member of the
topic's scope, assigned at once, or to another topic's queue. The receiver does not
accept.

*Source: M-011, M-034*

**Acceptance criteria**
1. FIFO per topic, to the available member with the fewest open conversations, within
   the topic's hours and exceptions in its time zone.
2. The visitor's place is right and pushed.
3. A transfer moves the conversation.
4. Outside hours, or with nobody available, the start answers `offline`, and the
   offline form's conversation is answered by email of `OfflineReplyKind`.

---

**MSG-CONV-003** — Automatic replies SHALL be rules, each a trigger and a reply whose
text is a template in each declared language.

**Values (M-011).** The triggers: a conversation started, no staff member available, a
visitor waiting longer than a set time, outside hours, and a conversation about to close
for silence. A reply's text is a template (`03`), found through the template source by a
member `TemplateKey` gains in the release that builds conversations, since its parameter
type is an `init`-only record (`04` MSG-LIB-002); the message it writes has the type
`automatic`.

*Source: M-011*

---

**MSG-CONV-004** — When a support conversation ends, the visitor SHALL be offered its
transcript, to download or by email, and a short survey.

**Values (M-011).** The transcript is rendered from the conversation's messages, less
internal notes, in the visitor's language. Emailing it is a send of a kind the host
declares, so it passes every check of `01`. The survey asks whether the issue was
resolved and how the staff member did (a rating and an optional comment), and is kept
for reports per staff member and topic.

*Source: M-011*

---

**MSG-CONV-005** — Staff SHALL have the tools a support desk needs.

**Values (M-011).** Internal notes, saved replies, tags and statuses, snoozing, typing
and read indicators, automatic closing after a configured silence, a supervisor view of
live conversations, and reports of waiting time, handling time and survey scores.

**Typing and read indicators (M-033).**
- `Typing` is relayed over the staff hub to the conversation's other connected readers
  (MSG-CONV-008), and never stored.
- A client sends it at most every 3 seconds while typing. A receiver shows it for 5
  seconds after the last one.
- A person whose participant row does not exist yet gets one written first.
- Moving a read position (MSG-CONV-001) sends `read` to the other readers.

*Source: M-011, M-033*

**Acceptance criteria**
1. Typing reaches the others and is never stored.

---

## 3. Staff chats

**MSG-CONV-006** — Who may read and write a group conversation SHALL follow the host's
membership, checked on every read and write, so a person who leaves loses access at
once.

**Values (M-011).** A company-wide conversation's members are the members of an
organization, and a team's the members of a group, as the host's membership seam answers
(a host answers it from its identity system's organizations and groups). A direct
conversation has the participants who started it and those added. Mentions, replies to a
message, editing and deleting one's own messages, muting, presence, and a push
notification to a member who is away (a send of a kind the host declares) are part of
the model.

**The membership seam (M-032).** `IConversationMembership` of `Hermes.Core` (`04`
MSG-LIB-002, MSG-LIB-005) is required where conversations are registered. It answers
whether a person is a member of a scope, a scope's members, and a person's scopes, a
scope (`ConversationScope`) being a kind, `Organization` or `Group`, and a reference. The
library caches none of its answers: every read and write of a group conversation asks
`IsMemberAsync`.

**Group conversations (M-032).** There is one per scope. It is created the first time a
member opens it, and opening it again creates nothing more. A member's participant row
(read position, mute) is written the first time they read or write it.

**Direct conversations (M-032).**
- A person starts one by naming 1 to 15 others: distinct references, and not themselves.
- Any participant may add others, up to 16 in all.
- A participant may leave. Leaving ends their access at once, and their messages stay.
- Someone who left comes back only by being added again.
- The library does not check that a named reference exists. The host's authorization on
  the staff group decides who may use it (`05` MSG-API-009).

**Access (M-032).** Anyone who is not a participant of a direct conversation, or has left
it, and anyone who is not a member of a group conversation's scope, is answered
`messaging.conversation.notfound` (404), never 403, so nobody learns a conversation
exists.

**Mentions (M-032).** A mention is a list of person references sent with the message,
never parsed from the text. In a direct conversation each one is a participant; in a
group conversation each one is a member, checked through the seam. Mute silences a
conversation's notices except the person's own mentions (MSG-CONV-008).

**Edits and deletes (M-032).**
- A person edits or deletes only their own `text` messages, at any time; another
  person's answers 403 `messaging.message.notauthor`, since the person can see it.
- An edit replaces the text and sets the edited instant. No history is kept.
- A delete clears the text and its mentions and sets the deleted instant. The row stays,
  and reads answer it as deleted with no text.
- Editing a deleted message answers 409 `messaging.message.deleted`.

**The routes (M-032).** `05` MSG-API-009's routes are each thin over one operation of
`IConversations` (`04` MSG-LIB-002), for the person `IRecipientIdentity` answers.

**Presence (M-033).**
- A person is online while they hold a staff-hub connection on any process
  (MSG-CONV-008).
- The `connections` table holds each connection's person, process and last-seen instant.
  A process refreshes its rows every 30 seconds, and a row older than 90 seconds is gone.
- Presence is shown in direct conversations only: as each participant's `online` in the
  conversation's read, and as `presence` pushes to the other participants. Group
  conversations show none.
- `connections` holds a person reference, so the rule that the engine knows each table
  applies (`06` MSG-OPS-002): erasure deletes its rows.

*Search (designed for).* Messages are encrypted under per-person keys (MSG-CONV-009), so
the database cannot search them. Search comes through a later seam that keeps an index
under the same protection; the model needs no change for it, and its design is made in
the release that builds it.

*Source: M-011, M-020, M-032, M-033*

**Acceptance criteria**
1. In a group conversation, a non-member's every read and write answers 404. A member
   the seam no longer answers for is refused on the next call.
2. In a direct conversation, a non-participant answers 404, and so does one who left, at
   once. Participants can be added up to 16, and the 17th answers 409.
3. Edits and deletes work on one's own messages only. Another person's answers 403. A
   deleted message reads with no text, and editing it answers 409.
4. Each route does what its operation does, and the endpoint contract test finds exactly
   these routes.
5. Presence turns offline within 90 seconds of a process stopping.

---

## 4. Attachments

**MSG-CONV-007** — An attachment SHALL be stored through a storage seam the host
supplies, checked by a scanning seam before anyone can open it, and bounded in size and
type.

**Values (M-011).** The size and the allowed media types are configuration. An
attachment is protected as its message is (MSG-CONV-009), as bytes (`04` MSG-LIB-002). A
storage seam is required where attachments are on; object storage is the expected
implementation.

*Source: M-011*

---

## 5. Live connection

**MSG-CONV-008** — Messages SHALL be written over HTTP and pushed to the participants
connected over a WebSocket, through ASP.NET Core SignalR, with separate hubs for the
staff side and the public side, and SHALL reach participants connected to any of the
host's processes.

**Values (M-011).** A message is stored before it is pushed, so a participant who
reconnects reads what they missed from the store. Processes tell one another of a new
message through PostgreSQL's `NOTIFY` on the library's connection, carrying identifiers
only, and each pushes to the participants connected to it, so no service outside the
database is needed (`00` MSG-OVR-002). A participant who is not connected is told
through sending (push or email), under a kind the host declares, so their preferences
and quiet hours apply.

**The staff hub (M-033).**
- `MapHermesStaffConversations` also maps the staff hub at `{prefix}/live`, inside the
  same group, so the host's authorization covers it (`05` MSG-API-009).
- The person comes from `IRecipientIdentity` at connect, and a connection without one is
  refused.
- The hub pushes `message`, `messageChanged`, `read`, `typing`, `presence` and
  `conversation`, and takes one method from the client, `Typing`: writes stay HTTP.
- Its route, methods and shapes are public surface (`04` MSG-LIB-006).

**Catching up (M-033).** The messages route takes `after`, a message's identifier, and
answers oldest first from it. Every push follows the store, so a client that reconnects
reads from its last message.

**Fan-out (M-033).**
- After a write commits, it sends `NOTIFY messaging_conversations` with
  `<event>:<conversationId>:<id>` (`08` section 8). The event is `message`, `changed`,
  `read`, `typing` or `presence`. The id is a message's or a participant's, never a
  person reference.
- Each process listens on a connection of `Hermes.Conversations`' own, opened from
  `listenConnection` where it is set and from the library's connection otherwise (`06`
  MSG-OPS-003). Dispatch and conversations never wait on each other.
- Each process pushes to its own connections whose person may read the conversation now:
  for a direct conversation, its participants who have not left; for a group
  conversation, the scope's members from one `MembersAsync` call per event, intersected
  with the persons connected to that process (MSG-CONV-006).

**Away notices (M-033).**
- **Who.** A participant of a direct conversation, or a person mentioned in a group
  conversation, who holds no staff-hub connection when the message is written and has
  not muted the conversation, is told through sending. A mention is told even where the
  conversation is muted (MSG-CONV-006).
- **How often.** Once per person per conversation, until their read position passes the
  message that was told. The send's idempotency key is
  `conversation:<id>:away:<participantId>:<messageId>`.
- **The kind.** It is the one named by `Hermes:Conversations:AwayKind`, which must be a
  declared kind, or the start fails naming the key. Unset, no away notice is sent.
- **Values.** The kind's values are the conversation's identifier and type, the
  message's identifier, and whether the person was mentioned. The text is never
  included: it is protected, and a notice must not carry it.
- The kind's own channels, preferences and quiet hours apply, as for any send.

*Source: M-011, M-033*

**Acceptance criteria**
1. A message written through process A reaches a client connected to process B, after
   it is stored.
2. A client that reconnects reads what it missed with `after`.
3. `NOTIFY`'s payload holds no person reference or text.
4. A non-member connected to the hub receives nothing of a group conversation, and a
   member the seam stops answering for receives nothing from the next event.
5. An away participant is told once until they read. A mention is told through a mute.
   A connected one is not told.
6. `AwayKind` naming an undeclared kind fails the start.
7. The contract test finds the hub's route and methods.

---

## 6. Privacy and retention

**MSG-CONV-009** — Every message and attachment SHALL be encrypted at rest under the key
of the person it concerns, through the content protector of `01` MSG-SEND-018.

**Values (M-011).** A support conversation's messages, staff replies included, are under
the visitor's or customer's key, so erasing that person makes the conversation
unreadable. A staff chat's messages are each under their author's key. Encryption is at
rest, not end to end: staff must read support conversations, hand them over, search them
and send transcripts.

**Staff chats (M-032).** A staff chat message's text is protected under its author's key,
through the engine's content protection (`01` MSG-SEND-018), and bound to its row. A
write for a person takes their person lock shared, as the in-app list does (`02`
MSG-INAPP-001), so an erasure cannot race it. Erasing a person (`01` MSG-SEND-020):
- deletes their participant and mention rows;
- handles their author references as the engine handles a delivery's person reference.

Their messages stay as rows, and their text, no longer opening, reads `Erased`. Everyone
else's messages are untouched.

**Support (M-034).**
- A signed-in customer's conversation (MSG-CONV-001) is protected under their key,
  through the host protector (`04` MSG-LIB-005).
- A visitor's conversation is protected through the library protector, with the visitor
  reference as its person, as `01` MSG-SEND-018 protects anonymous values.
- Staff replies are under the same key.

*Source: M-011, M-032, M-034*

**Acceptance criteria**
1. A message's protected text copied to another row does not open. A key version a
   message uses is never retired.
2. Erasing an author makes their messages read `Erased` and removes their participant
   and mention rows. Other messages are untouched.

---

**MSG-CONV-010** — How long a conversation is kept SHALL be configuration.

**Values (M-011).** A support conversation with a person is kept while the person
exists, and erased with them (`01` MSG-SEND-020); one with a visitor without an account
is kept `Hermes:Conversations:Retention:Visitor` after it closes (default one year); a
staff chat's messages are kept `Hermes:Conversations:Retention:Staff` (default: while
the organization exists). A visitor without an account is shown a privacy notice before
their first message, and their acknowledgement is recorded; a person with an account
accepted the host's notice when the account was made, and is shown none.

**The privacy notice (M-034).**
- The notice's text is the host's. The library records the acknowledgement: the
  visitor's hash, the version, the instant and the language.
- The version is `Hermes:Conversations:Support:NoticeVersion`, required where topics are
  configured.
- A visitor's message before an acknowledgement of the current version answers 409
  `messaging.support.noticerequired`. A signed-in customer is never asked.

**Erasure (M-034).** Erasing a person (`01` MSG-SEND-020) deletes their support
conversations with every row of them. A visitor's conversations go by retention (plan
phase 20).

*Source: M-011, M-034*

**Acceptance criteria**
1. No visitor message is taken before the current notice is acknowledged. A customer is
   never asked.
2. Erasing a customer deletes their support conversations.

---

**MSG-CONV-011** — The public side SHALL be limited per visitor and per source address,
and staff SHALL be able to block a visitor.

**Values (M-034).**
- **Limits.** Counters live in the database, across processes, as `01` MSG-SEND-014
  counts. Each is a key under `Hermes:Conversations:Limits:` (`08` section 4):
  - per visitor, conversations started per day (`VisitorStarts`, default 5) and messages
    per minute (`VisitorMessages`, default 20);
  - per source address, conversations started per hour (`SourceStarts`, default 20).
- Over a limit answers 429 `messaging.support.limited` with `Retry-After`.
- **The source address.** It is the request's remote address, as the host's
  forwarded-headers set-up gives it. IPv6 is grouped by its /64. The address is stored
  as a keyed hash under the domain `source` (`08` section 8).
- **Blocking.** Staff block a visitor or a customer by their conversation, until an
  instant or until lifted, and lift the block. A blocked one starting or writing answers
  403 `messaging.support.blocked`. Blocking by address is not offered, since addresses
  are shared. The `blocks` table holds a person reference or a visitor hash, so the rule
  that the engine knows each table applies (`06` MSG-OPS-002).

*Source: M-011, M-034*

**Acceptance criteria**
1. Each limit answers 429 at its edge, across two processes.
2. A blocked visitor is refused until the block ends or is lifted.

---

## 7. Later still

**MSG-CONV-012** — These SHALL be addable without changing the model above: voice and
video in a conversation, customer email in the support inbox, response-time targets, a
help center, an AI assistant, and WhatsApp as a way into support.

**Values (M-004, M-011).** A call is a message type, `call`, whose signalling runs over
the same hubs (WebRTC), its media between the participants or through a relay. An email
into the inbox is a conversation whose origin is `email`, answered by email through
sending. A response-time target is a rule on a topic's conversations, measured from
their messages. A help center is a separate set of articles; an AI assistant writes
`automatic` messages from it. WhatsApp is a channel of sending (`02` MSG-CHAN-002) and
an origin of a support conversation.

*Source: M-004, M-011*
