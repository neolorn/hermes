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

*Source: M-011, M-032*

**Acceptance criteria**
1. Each bound is refused at its edge: a text of 0 or 4,001 characters, a 33rd mention, a
   mention outside the conversation, a reply into another conversation, and a `note` in
   a `group` or `direct` conversation.
2. The read position moves forward only, and the unread counts follow it.

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

*Source: M-011*

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

*Source: M-011*

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

*Search (designed for).* Messages are encrypted under per-person keys (MSG-CONV-009), so
the database cannot search them. Search comes through a later seam that keeps an index
under the same protection; the model needs no change for it, and its design is made in
the release that builds it.

*Source: M-011, M-020, M-032*

**Acceptance criteria**
1. In a group conversation, a non-member's every read and write answers 404. A member
   the seam no longer answers for is refused on the next call.
2. In a direct conversation, a non-participant answers 404, and so does one who left, at
   once. Participants can be added up to 16, and the 17th answers 409.
3. Edits and deletes work on one's own messages only. Another person's answers 403. A
   deleted message reads with no text, and editing it answers 409.

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

*Source: M-011*

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
- handles their author references as the engine handles a delivery's person reference;
- clears their messages' text, as a delivery's content is deleted, whichever protector
  wrote it.

Their messages stay as rows, and their text reads `Erased`. Everyone else's messages are
untouched.

*Source: M-011, M-032*

**Acceptance criteria**
1. A message's protected text copied to another row does not open. A key version a
   message uses is never retired.
2. Erasing an author clears their messages' text, which then reads `Erased`, with the
   library's protector and a host's alike, and removes their participant and mention
   rows. Other messages are untouched.

---

**MSG-CONV-010** — How long a conversation is kept SHALL be configuration.

**Values (M-011).** A support conversation with a person is kept while the person
exists, and erased with them (`01` MSG-SEND-020); one with a visitor without an account
is kept `Hermes:Conversations:Retention:Visitor` after it closes (default one year); a
staff chat's messages are kept `Hermes:Conversations:Retention:Staff` (default: while
the organization exists). A visitor without an account is shown a privacy notice before
their first message, and their acknowledgement is recorded; a person with an account
accepted the host's notice when the account was made, and is shown none.

*Source: M-011*

---

**MSG-CONV-011** — The public side SHALL be limited per visitor and per source address,
and staff SHALL be able to block a visitor.

*Source: M-011*

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
