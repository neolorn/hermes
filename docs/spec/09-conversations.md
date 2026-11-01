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

**Support's tables (M-034).**
- `conversations` gains support's columns: `topic`, `assignee`, `queued_at`,
  `assigned_at`, `offline`, `closed_at` and `closed_by`.
- `participants` holds either `person` or `visitor` (the visitor's keyed hash), exactly
  one of them.
- `support_agents` (MSG-CONV-002), `support_blocks` and `support_counters`
  (MSG-CONV-011) and `support_acknowledgements` (MSG-CONV-010) are support's own.

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
- **The cookie.** The reference is kept in the cookie `messaging_visitor` (a neutral
  name, `08` section 8): HttpOnly, Secure, SameSite Lax, its path the public prefix,
  renewed on use, and living as long as `Hermes:Conversations:Retention:Visitor`
  (MSG-CONV-010).
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
(`08` section 4), its name under `01` MSG-SEND-001's name rule, with:
- `TimeZone`, an IANA time zone, required;
- `Hours`, from `Monday` to `Sunday`, each a list of `HH:mm-HH:mm`, its ranges sorted,
  not overlapping, each starting before it ends; a day not configured is closed;
- `Exceptions`, by date (`yyyy-MM-dd`), each `Closed` or its own list;
- `Scope`, a kind (`Scope:Kind`, `Organization` or `Group`) and a reference
  (`Scope:Reference`) for the membership seam (MSG-CONV-006), both required;
- `Supervisors`, a kind and a reference as `Scope`'s, optional: the scope whose members
  supervise the topic, nobody where it is unset (MSG-CONV-005, M-036).

The staff of a topic are its scope's members. A topic's display name is the host
frontend's. A topic is open within its hours while a member is available, as the
public topics route answers it (`05` MSG-API-010).

**Who answers (M-034).**
- **Available.** A staff member is available while they hold a staff-hub connection
  (MSG-CONV-008), have set themselves available, and hold fewer open support
  conversations than `Hermes:Conversations:Support:Limit` (default 3).
- **Being set available.** Through the staff group's availability route (`05`
  MSG-API-009), stored in the `support_agents` table. It holds a person reference, so
  the rule that the engine knows each table applies (`06` MSG-OPS-002).
- **Assignment.** It is automatic, and runs only within a topic's hours: when a
  conversation is queued, when a member frees up, sets themselves available or connects,
  and every 30 seconds, the oldest queued conversation of each topic goes to the
  available member with the fewest open conversations. Ties go to the one longest since
  their last assignment.
- **The person's list.** A staff member's list of conversations (`05` MSG-API-009)
  holds the open support conversations assigned to them, and the staff hub's
  `conversation` push tells them of one assigned (MSG-CONV-008).
- **The queue.** It is first in, first out per topic.
- **One open conversation per topic.** A visitor or a customer holds one open
  conversation per topic: a second start on the same topic answers 409
  `messaging.support.alreadyopen`, with `details.conversation` the open one's
  identifier. The current conversation route answers the newest open one (`05`
  MSG-API-010).
- **The visitor's place.** It is in the conversation's read, and pushed over the public
  hub as `place` (`05` MSG-API-010).

**Offline (M-034).** Outside a topic's hours, or within them while no member is
available, starting a conversation answers 409 `messaging.support.offline`, and the
visitor uses the offline form instead.
- The form takes an email address and the text. It becomes a conversation flagged
  `offline`, queued like any other.
- Staff replies to it are sent through sending, as the kind named by
  `Hermes:Conversations:Support:OfflineReplyKind`, to `Recipient.Anonymous` with that
  address. The key is required where topics are configured, and is read at start, where
  a kind not declared stops the start naming it, as `AwayKind` does (MSG-CONV-008).
  The kind's values are `conversation` and `message`, the conversation's and the
  reply's identifiers as text, `topic`, the topic's name, and `text`, the reply itself.
- The address is a protected value of the conversation, its context
  `conversations:<id>:email` (`01` MSG-SEND-018).

**Transfer (M-034).** A staff member transfers a conversation to another member of the
topic's scope, assigned at once, or to another topic's queue. The receiver does not
accept. A transfer to a person outside the topic's scope, or to a topic not configured,
answers 400 `messaging.conversation.invalid`, `details.field` `person` or `topic`.

**Staff access (M-034).**
- A member of the topic's scope reads a support conversation of the topic, transfers,
  closes and blocks it, and reads the topic's queue.
- Anyone else is answered 404 `messaging.conversation.notfound`, or an empty list for
  the queue.
- A member who is not the assignee writing is answered 404
  `messaging.conversation.notfound`.
- Staff-side pushes of a support conversation go to its assignee alone (MSG-CONV-008),
  and from plan phase 18 to the topic's supervisors too (MSG-CONV-005).

**Closing (M-035).** Staff close a support conversation through the staff group's close
route, the visitor or the customer their own through the public group's (`05`
MSG-API-009, MSG-API-010), and it closes by itself after a silence (MSG-CONV-005).
Every close sets the status `closed` and writes one `system` message without text, in
the close's transaction, stored and pushed as any message: its type says what happened.
Plan phase 18 builds it for the staff's and the visitor's closes.

*Source: M-011, M-034, M-035, M-036*

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

**Rules (M-035).**
- Each rule is `Hermes:Conversations:Replies:<name>` (`08` section 4), its name under
  `01` MSG-SEND-001's name rule, with:
  - `Trigger`: `started`, `unavailable`, `waiting`, `outsidehours` or `closing`;
  - `Topics`: the topics it serves, all when absent;
  - for `waiting`, `After`, from 1 minute to 1 hour.
- A rule whose `Topics` names a topic not configured, or with `After` while its trigger
  is not `waiting`, fails the start with `messaging.startup.configurationinvalid`.
- A rule fires at most once per conversation.
- `started` fires on a conversation created queued, by a start or an offline form, never
  on a transfer. `unavailable` fires on an offline
  form's conversation started within its topic's hours while no member is available,
  and `outsidehours` on one started outside them (MSG-CONV-002). `waiting` fires once a
  queued conversation has waited `After`. `closing` fires
  `Hermes:Conversations:Support:CloseWarning` before an automatic close
  (MSG-CONV-005).
- An automatic reply on an offline form's conversation is written to the conversation
  only, never emailed.
- The message written is of type `automatic`, `authorKind` `library`, in the
  conversation's language. That is the visitor's or the customer's language as the
  public side read it from `Accept-Language`, for a customer as for a visitor, since the
  engine holds no person's language (M-035, amended on M-036's review), matched as `01`
  MSG-SEND-003 matches. It is matched when the conversation starts and kept in the
  column `language` of `conversations`.
- Where two rules fire at once, both write, in name order.

**The template (M-035).**
- A rule's template is found by `TemplateKey.Reply`, the rule's name, with `Kind` and
  `Page` none (`04` MSG-LIB-002); the library's own source looks in
  `replies/<name>.<language>.text.liquid` (`03` MSG-TPL-006).
- A reply's values are `topic`; `place`, the visitor's place, or none; `wait`, the
  minutes waited; and `opens`, the topic's next opening instant in the topic's time
  zone, which the template formats: none within the topic's hours, otherwise the start
  of its next range, searched up to a year ahead, and none where there is none. A
  reply's template sees these four values alone.
- A value that is none (`place`, `opens`) reads as defined: false in a condition, empty
  in an output, blank to `default`. A template guards it with `{% if %}`.
- A rendered reply is bounded as a message's text is, at 4,000 characters
  (MSG-CONV-001). A reply that fails to render, or renders past the bound, writes
  nothing and is logged, and its rule counts as fired.
- Every rule's template is checked at start in every declared language, as kinds' are
  (`03` MSG-TPL-001, MSG-TPL-004). A missing one fails the start with
  `messaging.startup.templatemissing`, naming the rule and the language. One that does
  not parse fails it with `messaging.startup.declarationinvalid`, `details.declaration`
  the rule and `details.field` `<language>.text`.

*Source: M-011, M-035, M-036*

**Acceptance criteria**
1. Each trigger writes one `automatic` message in the conversation's language from its
   template, once per conversation.
2. A rule's missing template fails the start, naming it.
3. A host source that answers pages for `Kind == null` and reads `Reply` serves a reply.
   The library's own source serves one from `replies/`.

---

**MSG-CONV-004** — When a support conversation ends, the visitor SHALL be offered its
transcript, to download or by email, and a short survey.

**Values (M-011).** The transcript is rendered from the conversation's messages, less
internal notes, in the visitor's language. Emailing it is a send of a kind the host
declares, so it passes every check of `01`. The survey asks whether the issue was
resolved and how the staff member did (a rating and an optional comment), and is kept
for reports per staff member and topic.

**The transcript (M-036).**
- It is rendered from the conversation's messages, every type but `note`, oldest first,
  in the conversation's language (MSG-CONV-003).
- It is rendered through the page template `transcript`, whose one part is `text` (`03`
  MSG-TPL-006). The library ships its own in `en` and `ar`, as its other pages, and a
  host source may answer it instead.
- Its values are `topic`; `openedAt` and `closedAt`, the instants opened and closed;
  `language` and `direction`; `messages`, a list of `author`, `sentAt` and `text`, where
  `author` is `visitor` (the visitor or the customer), `staff` or `library`, so staff
  are never named by reference; and `omitted`, the number of messages left out.
- It is plain text, UTF-8, at most 1 MiB. Past that, the oldest messages are left out,
  and `omitted` counts them. Its render runs under a step limit of ten times
  `Hermes:Templates:MaxSteps` (`03` MSG-TPL-007), and a render past either bound
  renders again with the oldest messages left out.
- The public group's transcript route serves it to the visitor (by cookie) or the
  customer, as an attachment named `transcript-<id>.txt`, for a closed conversation
  only (`05` MSG-API-010). Otherwise it answers 409 `messaging.support.notclosed`.

**By email (M-036).**
- The public group's transcript email route sends it as the kind named by
  `Hermes:Conversations:Support:TranscriptKind` (`08` section 4). The key is required
  where topics are configured, and is read at start, where a kind not declared stops the
  start naming it, as `AwayKind` does (MSG-CONV-008).
- The visitor and the customer alike give `email`, since the engine holds no person's
  address. The send goes to a visitor as `Recipient.Anonymous`, and to the customer as
  `Recipient.Person`, each with that `Email`.
- The kind's values are `topic`, `closedAt` and `transcript`, the rendered text, bounded
  at half of `Hermes:Email:MaxBytes` with the oldest messages left out, so that the
  email fits (`02` MSG-EMAIL-003).
- A conversation's transcript is sent at most 3 times, each send's idempotency key
  `conversation:<id>:transcript:<n>`, `n` from 1 to 3. The fourth answers 409
  `messaging.support.transcriptlimit`. The sends are counted in the column
  `transcript_sends` of `conversations`, 0 to 3, since idempotency rows lapse and cannot
  count.
- It is sent for a closed conversation only, as the transcript is served. Every check of
  `01` applies to the send, and a send the engine refuses answers its failure code (`08`
  section 1) as 400.

**The survey (M-036).**
- The public group's survey route (`05` MSG-API-010) takes `resolved` (a boolean),
  `rating` (1 to 5) and an optional `comment` (up to 1,000 characters). One outside its
  rule answers 400 `messaging.support.surveyinvalid`, `field` `resolved`, `rating` or
  `comment`, and a conversation not closed 409 `messaging.support.notclosed`.
- The survey is answered once: a second answers 409
  `messaging.support.surveyanswered`. It is answered within 7 days of the close: after
  that it answers 409 `messaging.support.surveyclosed`.
- It rates the staff member assigned when the conversation closed. A conversation never
  assigned keeps its survey with no staff member.
- It is kept in columns of `conversations`: `survey_at`, `survey_resolved`,
  `survey_rating`, `survey_comment` with `survey_comment_key_version` (as
  `email_key_version`), and `survey_person`, the staff member rated, a
  person reference under the rule that the engine knows each table (`06` MSG-OPS-002).
  Erasing that staff member handles `survey_person` as a delivery's person reference:
  the survey stays, and counts for the topic alone.
- The comment is a protected value of the conversation, under the same key as its
  messages, its context `conversations:<id>:surveycomment` (MSG-CONV-009). It is kept
  and erased with the conversation.

*Source: M-011, M-036*

**Acceptance criteria**
1. The transcript holds every message but notes, in the conversation's language, names
   no staff member, and is served only for a closed conversation, only to its visitor
   or customer.
2. Emailing it sends the declared kind, at most three times.
3. The survey is taken once, within seven days, and its comment is protected.

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

**Saved replies (M-035).**
- A saved reply has a name (1 to 64 characters) and a text (1 to 4,000), plain text
  without templating. It is either shared by a topic or a staff member's own.
- A topic holds at most 200, and a person at most 100. A name is unique among its
  owner's replies.
- Staff of a topic manage its shared replies, and each person their own, through the
  staff group's replies routes, with `topic` or `mine` (`05` MSG-API-009).
- A name or text outside its bounds answers 400 `messaging.reply.invalid`, `field`
  `name` or `text`; a reply past its owner's count 409 `messaging.reply.full`; a name
  its owner already has 409 `messaging.reply.nameexists`; another person's own reply
  404 `messaging.reply.notfound`. A topic's replies, for a person outside its scope,
  answer 404 `messaging.reply.notfound` on every replies route, never 403.
- The table `support_replies` holds a person reference for one's own, so the rule that
  the engine knows each table applies (`06` MSG-OPS-002). Erasure deletes a person's own
  replies.

**Tags (M-035).**
- The tags are declared in `Hermes:Conversations:Tags`, a list of names under `01`
  MSG-SEND-001's name rule, at most 50. A name outside the rule, or a 51st, fails the
  start with `messaging.startup.configurationinvalid`.
- Staff add and remove them on a support conversation they may read, by putting the
  whole set (`05` MSG-API-009). They are kept in the column `tags` of `conversations`.
- A conversation's tags, and a snoozed one's waking instant, are on staff bodies only:
  the public side never answers them (`05` MSG-API-009, MSG-API-010).
- An undeclared tag answers 400 `messaging.conversation.invalid`, `field` `tags`. A tag
  later dropped from the declaration stays on its conversations, and is refused only
  when it is put again.
- Tags are not statuses: statuses stay the fixed set of `08` section 3 (MSG-CONV-001).

**Snoozing (M-035).**
- Snoozing a support conversation they may read with `until`, from 5 minutes to 30 days
  ahead, sets its status `snoozed` (`05` MSG-API-009). An `until` outside that range
  answers 400 `messaging.conversation.invalid`, `field` `until`.
- The conversation wakes to `open` at `until`, or at once when the visitor or the
  customer writes.
- A snoozed conversation keeps its assignee, and counts toward their limit
  (MSG-CONV-002).
- Tagging or snoozing a closed conversation answers 409
  `messaging.conversation.closed`, and a conversation that is not support 400
  `messaging.conversation.invalid`, `field` `type`.

**Automatic closing (M-035).**
- `Hermes:Conversations:Support:CloseAfter` (default 24 hours) is the silence after which
  an open or snoozed support conversation closes.
- The silence is measured from the last `text` written by a staff member, a visitor or a
  customer. Library messages (`automatic`, `system`) and notes never restart it.
- `Hermes:Conversations:Support:CloseWarning` (default 5 minutes, shorter than
  `CloseAfter`) is how long before the close the `closing` rule fires (MSG-CONV-003).
- Such a `text` after the warning restarts the silence. The close sets the status
  `closed` and writes one `system` message without text, as every close does
  (MSG-CONV-002).

**The timers (M-035).**
- The job `conversation-timers` runs every 30 seconds under the engine's job claims (`06`
  MSG-OPS-004). It fires `waiting` and `closing`, wakes snoozes, and closes.
- Each timer fires once, survives a restart, and fires nothing on a closed conversation.
  Each timer's next instant is a column of `conversations`, `waiting_at`,
  `closing_at`, `close_at` or `snoozed_until`, moved in the transaction that fires it,
  and the rules already fired are kept in `replies_fired` (`08` section 8).

**Supervisors (M-036).**
- Each topic may name `Supervisors`, a membership scope (kind and reference, as
  `Scope`, MSG-CONV-002; `08` section 4). Its members supervise the topic, through
  `IsMemberAsync` on every call (MSG-CONV-006). Unset, nobody supervises the topic, and
  its live view and reports answer 404 to everyone.
- A supervisor reads every conversation of the topic, notes included, as its staff do,
  through the conversation and messages routes, and receives the staff hub's pushes for
  them (MSG-CONV-008). A supervisor only reads: a write by one who is not the assignee
  answers as it does for any other staff member who is not assigned. The topic's
  conversations are not in the supervisor's list of conversations.
- The staff group's live route, with `topic`, answers the topic's open and snoozed
  conversations, each with its status, assignee, place (none once assigned) and whole
  minutes waited since it was queued (`05` MSG-API-009). It is over HTTP: the client
  reads it again.
- A person who supervises no topic named answers 404 `messaging.conversation.notfound`.
- `IConversations` answers both views (`04` MSG-LIB-002).

**Reports (M-036).**
- The staff group's reports route, with `topic`, `from` and `to` (`yyyy-MM-dd`, both
  included), is for the topic's supervisors (`05` MSG-API-009). The period is whole days
  in the topic's time zone, at most 92; one that does not read or is longer answers 400
  `messaging.request.malformed`, `member` `from` or `to`.
- It answers, per staff member and for the topic as a whole, over the conversations
  closed within the period, each counted for its assignee when it closed:
  - conversations closed;
  - the median and 90th percentile of waiting time (queued to assigned) and of handling
    time (assigned to closed), in seconds, as PostgreSQL's `percentile_cont` computes
    them;
  - surveys answered (MSG-CONV-004), the share resolved, and the mean rating.
- A figure with no data is none. Everything is computed when read, from the
  conversations' rows. Nothing is aggregated or stored.
- Two counters join `08` section 8, each tagged `topic`: `hermes.conversations.started`
  counts every support conversation, offline ones included, when it is created, and
  `hermes.conversations.closed` each close, by staff, by the visitor or automatically.

*Source: M-011, M-033, M-035, M-036*

**Acceptance criteria**
1. Typing reaches the others and is never stored.
2. Saved replies keep their bounds and owners. Another person's own reply answers 404.
3. An undeclared tag is refused.
4. A snoozed conversation wakes at its instant, and at once on the visitor's message.
5. A conversation silent past `CloseAfter` closes, the warning first, and a message
   after the warning restarts the silence.
6. A restart fires no timer twice and misses none, and nothing fires on a closed
   conversation.
7. Only a supervisor of the topic reads its live view and reports. A supervisor reads
   notes.
8. Reports give the medians, the 90th percentiles, the survey figures per staff member
   and topic, over a period of at most 92 days.

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
exists. A topic's supervisors read its support conversations, notes included
(MSG-CONV-005, M-036).

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

*Source: M-011, M-020, M-032, M-033, M-036*

**Acceptance criteria**
1. In a group conversation, a non-member's every read and write answers 404. A member
   the seam no longer answers for is refused on the next call.
2. In a direct conversation, a non-participant answers 404, and so does one who left, at
   once. Participants can be added up to 16, and the 17th answers 409.
3. Edits and deletes work on one's own messages only. Another person's answers 403. A
   deleted message reads with no text, and editing it answers 409.
4. Presence turns offline within 90 seconds of a process stopping.

---

## 4. Attachments

**MSG-CONV-007** — An attachment SHALL be stored through a storage seam the host
supplies, checked by a scanning seam before anyone can open it, and bounded in size and
type.

**Values (M-011).** The size and the allowed media types are configuration. An
attachment is protected as its message is (MSG-CONV-009), as bytes (`04` MSG-LIB-002). A
storage seam is required where attachments are on; object storage is the expected
implementation.

**Switch and bounds (M-037).**
- `Hermes:Conversations:Attachments:Enabled`, default `false` (`08` section 4).
- `Public`, default `false`: whether the public group takes attachments too.
- `MaxBytes`, default 10 MiB, from 1 KiB to 50 MiB.
- `Types`, the allowed media types, default `image/jpeg`, `image/png` and
  `application/pdf`.
- A file's declared type must be allowed. For the types the library knows (JPEG, PNG,
  WebP, GIF, PDF), its first bytes must match it too.
- Otherwise it answers 400 `messaging.attachment.type`. Over the size it answers 413
  `messaging.attachment.toolarge`.
- A file name outside its rule (MSG-CONV-009) answers 400
  `messaging.attachment.invalid`, `field` `name`, and a body that is not one file the
  same with `field` `file`.
- Where attachments are disabled, or the public group's are, its attachment routes
  answer 404 `messaging.conversation.notfound`.

**The storage seam (M-037).** `IAttachmentStore` of `Hermes.Core`, required where
attachments are enabled (`04` MSG-LIB-005):
- `PutAsync(string key, Stream content, CancellationToken)`;
- `OpenAsync(string key, CancellationToken)`, answering `Stream?`;
- `DeleteAsync(string key, CancellationToken)`.

The key is drawn by the library, 128 random bits in base64url, and names nothing. The
store holds only protected bytes (MSG-CONV-009): one stored object per attachment, its
64 KiB chunks each an envelope bound to `attachments:<id>:chunk:<n>`, framed by length
and streamed, never held whole. Re-protection writes a new object under a new key, then
deletes the old.

**The table (M-037).** `attachments` holds (`08` section 8):
- `id`;
- `conversation_id` and `message_id`, each a foreign key set to null when its row goes,
  so the job still deletes the stored bytes;
- `named_at`;
- `uploader` (a person reference, none for a visitor), `visitor` (the keyed hash) with
  `hash_version`, and `owner` (the person whose key protects it, none for a visitor's);
- `name` (protected) with `content_key_version`, which covers the chunks too;
- `media_type`, `size`, `chunks` and `store_key`;
- `status`, `scan_attempts`, `scan_at` (the next attempt) and `created_at`.

**The scanning seam (M-037).** `IAttachmentScanner` of `Hermes.Core`, required where
attachments are enabled (`04` MSG-LIB-005):
`ScanAsync(Stream content, string mediaType, CancellationToken)`, answering a
`ScanResult`, `Clean`, `Infected` or `Failed` (`04` MSG-LIB-002).
- A new job, `attachment-scans`, runs every 30 seconds under the engine's job claims
  (`06` MSG-OPS-004), and scans each pending attachment.
- `Clean` opens it.
- `Infected` deletes its bytes from the store and marks the row `infected`.
- `Failed` retries with backoff, up to 5 times, then marks the row `failed` and raises
  the alert `attachment-scan` (`06` MSG-OPS-006).

**Upload and use (M-037).**
- An attachment is uploaded on its own, a single-file multipart body, through the staff
  group's `/conversations/{id}/attachments` and, where `Public` is on, the public
  group's `/support/conversations/{id}/attachments` (`05` MSG-API-009, MSG-API-010).
  It answers 201 with `id`, `name`, `mediaType`, `size` and `status` (`pending`).
- A message names up to 10 of its writer's attachments in `attachments`. An attachment
  is named by one message only. More than 10, another writer's, one already named, or
  one unknown answers 400 `messaging.message.invalid`, `field` `attachments`.
- A message lists its attachments, each with `id`, `name`, `mediaType`, `size` and
  `status`: `pending`, `clean`, `infected` or `failed` (`08` section 2).
- One not named within 24 hours is deleted by the `attachment-scans` job.

**Download (M-037).**
- The attachment route streams it through the library after the conversation's access
  check, never as a link to the store, with `Content-Disposition: attachment`, its media
  type, and `X-Content-Type-Options: nosniff`.
- While pending it answers 409 `messaging.attachment.pending`. Infected or failed, it
  answers 409 `messaging.attachment.refused`. One the conversation does not hold
  answers 404 `messaging.attachment.notfound`.
- A visitor never reaches one in a `note`: it answers them as one the conversation does
  not hold.

**Retention (M-037).** The `attachment-scans` job removes, deleting the stored bytes
first and retrying a failed deletion at its next pass, the rows whose conversation went,
whose message went or was deleted, whose name an erasure cleared, or that were never
named within 24 hours (MSG-CONV-009). An infected row keeps its row without its bytes.

*Source: M-011, M-037*

**Acceptance criteria**
1. Nothing opens before a clean scan. Infected is deleted and refused, and failed raises
   the alert.
2. A file of a refused type, or one whose bytes deny its declared type, is refused, and
   one over `MaxBytes` answers 413.
3. Downloads stream after the access check, with `nosniff` and as an attachment.
4. An unnamed upload is gone after 24 hours.

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
  `read`, `typing` or `presence`, the id a message's or a participant's, or, for
  support (M-034), `place`, `assigned`, `closed` or `conversation`, the id the
  conversation's own. It is never a person reference.
- Each process listens on a connection of `Hermes.Conversations`' own, opened from
  `listenConnection` where it is set and from the library's connection otherwise (`06`
  MSG-OPS-003). Dispatch and conversations never wait on each other.
- Each process pushes to its own connections whose person may read the conversation now:
  for a direct conversation, its participants who have not left; for a group
  conversation, the scope's members from one `MembersAsync` call per event, intersected
  with the persons connected to that process (MSG-CONV-006); for a support conversation,
  on the staff side, its assignee alone (MSG-CONV-002, M-034) and the topic's
  supervisors (MSG-CONV-005, M-036).

**Away notices (M-033).**
- **Who.** A participant of a direct conversation, or a person mentioned in a group
  conversation, who holds no staff-hub connection when the message is written and has
  not muted the conversation, is told through sending. A mention is told even where the
  conversation is muted (MSG-CONV-006).
- **How often.** Once per person per conversation, until their read position passes the
  message that was told. The send's idempotency key is
  `conversation:<id>:away:<participantId>:<messageId>`.
- **The kind.** It is the one named by `Hermes:Conversations:AwayKind`, read at start,
  which must be a declared kind, or the start fails naming the key. Unset, no away
  notice is sent.
- **Values.** The kind's values are `conversation` and `message`, the conversation's and
  the message's identifiers as text, `conversationType`, the conversation's type as `08`
  spells it, and `mentioned`, a flag of whether the person was mentioned (`03`
  MSG-TPL-002). The text is never included: it is protected, and a notice must not carry
  it.
- The kind's own channels, preferences and quiet hours apply, as for any send.

*Source: M-011, M-033, M-034*

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

**Support (M-034).**
- A signed-in customer's conversation (MSG-CONV-001) is protected under their key,
  through the protector a person's values are written with (`04` MSG-LIB-005).
- A visitor's values are protected as an anonymous recipient's are (`01` MSG-SEND-018):
  through the library's protector, with no person, bound to their row. A visitor's
  conversations go by retention (MSG-CONV-010), never by erasure.
- Staff replies are under the same key.
- A survey's comment is under the conversation's key, its context
  `conversations:<id>:surveycomment` (MSG-CONV-004, M-036).

**Attachments (M-037).**
- An attachment's bytes are protected in chunks of 64 KiB. Each chunk is an envelope
  under the key of its message's protection, bound to `attachments:<id>:chunk:<n>`, so
  no whole file is held in memory. The chunks are stored through the storage seam
  (MSG-CONV-007).
- The file name (1 to 255 characters, with path separators and control characters
  refused) is a protected value of the row, its context `attachments:<id>:name`.
- Key retirement keeps an attachment's key version in use. Re-protection reads and
  rewrites its chunks through the store, under the rule that the engine knows each table
  (`06` MSG-OPS-002).
- An attachment goes, from the store and its row, with its message whenever the message
  goes, and when an erasure clears its message's text.

*Source: M-011, M-032, M-034, M-036, M-037*

**Acceptance criteria**
1. A message's protected text copied to another row does not open. A key version a
   message uses is never retired.
2. Erasing an author clears their messages' text, which then reads `Erased`, with the
   library's protector and a host's alike, and removes their participant and mention
   rows. Other messages are untouched.
3. The store holds only protected chunks, and a chunk moved to another attachment does
   not open.
4. Erasure and the message's going delete the stored bytes, and a key version an
   attachment uses is never retired.

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
- The notice's text is the host's. The library records the acknowledgement in
  `support_acknowledgements`: the visitor's hash, the version, the instant and the
  language.
- The version is `Hermes:Conversations:Support:NoticeVersion`, required where topics are
  configured.
- A visitor's message, or offline form, before an acknowledgement of the current version
  answers 409 `messaging.support.noticerequired`. A signed-in customer is never asked.

**Erasure (M-034).** Erasing a person (`01` MSG-SEND-020) deletes their support
conversations with every row of them. A visitor's conversations go by retention (plan
phase 20).

**Retention (M-038).**
- **Visitors.** A visitor's support conversation, with its messages, attachments and
  survey, goes `Hermes:Conversations:Retention:Visitor` after it closes (default 365
  days, `08` section 4). The visitor's acknowledgement goes with their last
  conversation.
- **Staff chats.** `Hermes:Conversations:Retention:Staff` is unset by default, which
  keeps staff chats while the organization exists. Set from 30 days to 10 years, it
  deletes staff-chat messages older than it, with their attachments. A reply to a
  message gone this way reads `replyTo` none, and a read position on one reads as the
  latest earlier message.
- **Ending a scope.** `IConversations.EndScopeAsync(ConversationScope scope, ...)` (`04`
  MSG-LIB-002) answers `Result` and deletes a scope's group conversation with every row
  of it, for the host to call when an organization or group ends. It needs no person:
  it is the host's own call.
- **Customers.** A customer's support conversations stay while the person does, and go
  with the person's erasure.
- **The job.** The engine's retention job (`06` MSG-OPS-004) deletes the rows, at most
  1,000 conversations per run. The attachments of the rows it deletes stay, their
  `conversation_id` or `message_id` set to null, until `attachment-scans` deletes their
  bytes and then their rows (MSG-CONV-007).

**Export (M-038).** A person's export (`01` MSG-SEND-020) gains their conversations, in
`SubjectExport.Conversations` (`04` MSG-LIB-002), null where conversations are not
registered:
- their support conversations as the customer, with every message but notes;
- the staff chats where they wrote, with their own messages only;
- for each, its id, type, topic and opened and closed instants;
- each message's instant, author (`person` for the person's own, `staff` for a staff
  member's, `library` for an `automatic` or `system` message) and text;
- the attachments' names, media types and sizes, without their bytes;
- the person's survey answer (MSG-CONV-004).

Others' references never appear: a staff author reads `staff`, as in the transcript. A
message's text and a survey's comment answer as a delivery's values do in the export:
marked erased, or marked rejected and raising `content-rejected`, with no text, and one
that cannot open yet answers the export `messaging.key.unavailable`.

*Source: M-011, M-034, M-038*

**Acceptance criteria**
1. No visitor message is taken before the current notice is acknowledged. A customer is
   never asked.
2. Erasing a customer deletes their support conversations.
3. A visitor's conversation is gone `Retention:Visitor` after close, with every row and
   its attachments' bytes.
4. With `Retention:Staff` unset, nothing of a staff chat goes. Set, older messages go.
5. `EndScopeAsync` removes the scope's conversation entirely.

---

**MSG-CONV-011** — The public side SHALL be limited per visitor and per source address,
and staff SHALL be able to block a visitor.

**Values (M-034).**
- **Limits.** Counters live in the database, in `support_counters`, across processes, as
  `01` MSG-SEND-014 counts. Each is a key under `Hermes:Conversations:Limits:` (`08`
  section 4):
  - per visitor, conversations started per day (`VisitorStarts`, default 5) and messages
    per minute (`VisitorMessages`, default 20);
  - per source address, conversations started per hour (`SourceStarts`, default 20).
- A signed-in customer is limited as a visitor is, counted by their person's keyed hash.
- Over a limit answers 429 `messaging.support.limited` with `Retry-After`.
- Acknowledging the notice (`05` MSG-API-010) answers 429 where the visitor or the
  source address is already at its starts limit, and counts nothing.
- **The source address.** It is the request's remote address, as the host's
  forwarded-headers set-up gives it. IPv6 is grouped by its /64. The address is stored
  as a keyed hash under the domain `source` (`08` section 8).
- **Blocking.** Staff block a visitor or a customer by their conversation, until an
  instant or until lifted, and lift the block. A blocked one starting or writing answers
  403 `messaging.support.blocked`. Blocking by address is not offered, since addresses
  are shared. The `support_blocks` table holds a person reference or a visitor hash, so
  the rule that the engine knows each table applies (`06` MSG-OPS-002).

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
