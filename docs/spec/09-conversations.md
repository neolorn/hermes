# 09 — Conversations (designed now, built later)

**Not in the first release.** This chapter fixes the model of conversations now, so that
the first release's sending carries what they will need and the later build adds the
`Hermes.Conversations` package without a breaking change. Its requirements state design
constraints and carry no acceptance criteria until the release that builds them writes
them; its configuration keys, under `Hermes:Conversations:`, join `08` section 4 in that
release.

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

*Source: M-011*

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
(the Janus bridge answers it from Janus's organizations and groups). A direct
conversation has the participants who started it and those added. Mentions, replies to a
message, editing and deleting one's own messages, muting, presence, and a push
notification to a member who is away (a send of a kind the host declares) are part of
the model.

*Search (designed for).* Messages are encrypted under per-person keys (MSG-CONV-009), so
the database cannot search them. Search comes through a later seam that keeps an index
under the same protection; the model needs no change for it, and its design is made in
the release that builds it.

*Source: M-011*

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

*Source: M-011*

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
