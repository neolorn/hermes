# 10 — Threat model

What is worth protecting, who would go after it, and which requirement stands in the
way.

---

## 1. Assets

| Asset | Where it is | Protected by |
|---|---|---|
| Destinations (addresses, numbers, push endpoints and keys) | The outbox, subscriptions | Encryption at rest bound to its row, and keyed hashes (`01` MSG-SEND-018, `06` MSG-OPS-008) |
| Message content | The outbox | Encryption at rest, retention (`01` MSG-SEND-018, MSG-SEND-019) |
| A person's choices (preferences, suppressions) | Their tables | Identity checks on the endpoints (`05` MSG-API-002); signed tokens (`05` MSG-API-005); suppressions hold no destination |
| Provider credentials, the VAPID private keys, the content, hash, token and report keys | The secret source only | `06` MSG-OPS-008 |
| The SMS balance | The provider account | Allowed prefixes, limits, the floor, the drain alert (`01` MSG-SEND-014, `02` MSG-SMS-002, MSG-SMS-006) |

---

## 2. Threats and controls

**MSG-THR-001** — Each threat below SHALL be met by the control it names, and each
control SHALL have the test its requirement gives.

| Threat | Control |
|---|---|
| A forged SMS delivery report marks a message delivered or failed | A secret key in the route, compared in constant time; a report can only move an SMS delivery to `delivered` or `failed`; rejected reports counted and alerted (`02` MSG-SMS-004). Where the report echoes the delivery reference, a forger must also hold the 128 random bits of an attempt, and a report of an earlier attempt can only mark a delivery delivered; where reports are matched by the provider's own identifier, the key alone stands in the way. Someone who holds the key (over plain HTTP where the gateway calls only `http`, or from a request log that was not redacted, `05` MSG-API-006) can mark sent SMS delivered or failed: the host's observer is told so and may act on it, and a forged failure gives back a per-destination place, never a place under the deployment's cap (`01` MSG-SEND-014) |
| A browser registers a push "subscription" pointing at an internal address, so the server sends requests into the network (server-side request forgery) | Endpoints only at allowed push services, by DNS name, `https` on 443, checked again before every request, no redirects (`02` MSG-PUSH-004) |
| Someone unsubscribes another person by guessing a link | Tokens signed with HMAC-SHA-256, verified in constant time, with a lifetime (`05` MSG-API-005, `07` MSG-ENG-004) |
| A mail scanner opening a link unsubscribes the person | `GET` never unsubscribes (`05` MSG-API-005) |
| The unsubscribe route tells whether an address is suppressed | It answers the same either way (`05` MSG-API-005) |
| The token in an unsubscribe address leaks to another site | Pages answered with `Referrer-Policy: no-referrer` and a policy that loads nothing (`05` MSG-API-005) |
| A value carrying HTML or script runs in a recipient's mail client | HTML encoding of every value, no unencoded output, and a captured value's markup kept only where the library's wrappers encoded every other value a filter combines with it, a list's included (`03` MSG-TPL-005, MSG-TPL-007) |
| A value carrying a line break adds a header to an email | Control characters in a subject or display name replaced (`03` MSG-TPL-005) |
| A template loops for ever, or builds a value without bound | Rendering bounded in steps, levels and size (`03` MSG-TPL-007) |
| Sends to many numbers, of a host's or an attacker's making, drain the SMS balance (toll fraud) | Allowed prefixes, checked at the send and again at dispatch, so a list narrowed in an incident stops what is queued; limits per destination and across the deployment for each category; the floor; the drain alert (`01` MSG-SEND-014, `02` MSG-SMS-002, MSG-SMS-006); the host's own controls first (MSG-THR-002) |
| A database copy leaks addresses, numbers or messages | Encryption at rest, the idempotency key kept with a send included (`01` MSG-SEND-017, M-029); suppressions, places, idempotency digests and lookups hold keyed hashes only (`01` MSG-SEND-009, MSG-SEND-012, MSG-SEND-014, MSG-SEND-018); no check value is kept of a password (`06` MSG-OPS-008). A phone number has few possible values, so someone holding both a copy and `hashKeys` can find every hashed number by trying each; `hashKeys` lives in the secret source alone |
| Someone with write access to the database swaps two rows' protected destinations to redirect a message | Each protected value bound to its table, row, field and person (`01` MSG-SEND-018) |
| A provider's answer carries a destination into the records or the logs | Attempts keep a reason code and a numeric status only (`02` MSG-CHAN-001) |
| A log leaks addresses, numbers, endpoints, references or text | Logs carry identifiers only, checked in the gate (`06` MSG-OPS-005); the HTTP clients log no request address (`02` MSG-CHAN-001); a host's outbound traces are redacted by the deployment (`06` MSG-OPS-007) |
| A man in the middle reads mail on its way to the submission server | TLS required, its certificate validated with its revocation status (`02` MSG-EMAIL-001) |
| A push service or anyone between reads a push message | RFC 8291 encryption to the browser's key (`02` MSG-PUSH-001); the topic a keyed hash (`02` MSG-PUSH-002) |
| One person reads or changes another's preferences, subscriptions or in-app entries | Every such route acts only for the signed-in person, and another person's entry answers as one that does not exist (`05` MSG-API-002, MSG-API-008; `02` MSG-INAPP-003) |
| Another site makes a signed-in person's browser change their preferences or mark their in-app entries read | JSON bodies read only as `application/json`; the host's own forgery protection on those groups (`04` MSG-LIB-004, `05` MSG-API-001, MSG-API-008) |
| A value carrying markup runs as script in the host's in-app list | Entries hold plain text, which the host's frontend shows as text, never as markup (`05` MSG-API-008) |
| A report endpoint is flooded, or its key guessed | A key of at least 128 random bits, compared in constant time; wrong-key requests limited per source address and bodies bounded, which bound the load; someone holding the key can make at most `Hermes:Sms:ReportHoldMax` held reports at once, the rest dropped, counted and logged (`02` MSG-SMS-004, `05` MSG-API-006) |
| A process that does not hold a newer key version acts as though nothing were made under it, sending to a suppressed address or past a limit | Lookups computed under every version the process holds, then the versions in use read; one in use that the process does not hold makes its lookups wait, never pass (`06` MSG-OPS-008; `01` MSG-SEND-006) |
| Someone with write access to the database moves a subscription, a delivery or an in-app entry to another person | Each protected value bound to its person as well as its row (`01` MSG-SEND-018) |
| Someone with write access to the database alters a protected value | The value does not open, its delivery ends `failed` and `content-rejected` is raised; an altered format byte that lands in the range kept for later releases holds it instead, with `key-unavailable` raised, until `Hermes:Dispatch:MaxHold` ends it `failed` (`01` MSG-SEND-006, MSG-SEND-018) |
| A process deployed without a key version, or without the protector another process writes with, loses the messages it claims | A delivery held for a key is never counted as an attempt and waits for a process that holds it, with `key-unavailable` raised, for a run of holds of up to `Hermes:Dispatch:MaxHold` (`01` MSG-SEND-006; `06` MSG-OPS-008) |
| An older release running beside a newer one retires a key version the newer one's rows still need, or reports an erasure done that it could not finish | A process whose database holds a migration its release does not know marks no version retired and answers erasure and export `messaging.release.newer` (`06` MSG-OPS-008; `01` MSG-SEND-020) |
| A key changed in place, or removed while in use, silently breaks suppressions, tokens or subscriptions | Check values recorded per version of the five keys; a version still in use cannot be removed, and only the retention job marks one retired, never sooner than `Hermes:Secrets:RetireAfter` after values were last made under it; a write version must be chosen where several versions are held (`06` MSG-OPS-008) |
| The development inbox captures, instead of sending, in a deployment, or is reached there | It acts only where the host's environment is Development, and in any other neither call registers or maps anything (`02` MSG-DEV-001) |
| A site the developer visits reads captured codes through the browser (DNS rebinding, another origin) | The inbox answers only requests from a loopback address under a loopback host name, sends no CORS headers, and clears only with its own header (`02` MSG-DEV-005) |
| Script in a captured message runs in the inbox page | Every value written as text under a strict policy; an email's HTML served from its own route under a policy and sandbox with no script (`02` MSG-DEV-005) |
| Captured messages are read from the disk by another user, or committed | A folder only its owner reads, outside the application's content root, its records deleted after `KeepFor` (`02` MSG-DEV-002) |

*Source: M-007, M-008, M-009, M-010, M-012, M-022, M-024, M-026*

**Acceptance criteria**
1. Every control above that the library itself carries has a test carrying the
   identifier of the requirement that names it; the host's own controls, the redaction
   of request logs and the clocks are the deployment's (`06` MSG-OPS-007).

---

**MSG-THR-002** — The library SHALL send nothing a host did not declare: there is no
endpoint that sends a message, and every send names a declared kind.

*Source: M-005*

A host's own endpoints that lead to a send (a sign-up asking for a code) carry their own
abuse controls; the library's limits are the last line, not the first.

**Acceptance criteria**
1. No route of `05` records a send.

---

**MSG-THR-003** — A delivery SHALL go only to the destination its send carried or, for
push, only to subscriptions that are the person's when it is dispatched.

*Source: M-005*

**Acceptance criteria**
1. A subscription another person registers, before or after a send, is never used for
   it.

---

**MSG-THR-004** — The repository's whole history SHALL be scanned for secrets before
every push, by the implementer (the first push, which comes before the pipeline exists,
`07` MSG-ENG-011, included), and by the pipeline on every pull request, every push to
the default branch and every release tag (`07` MSG-ENG-012), and a finding SHALL stop
the push it comes before and block the merge; no finding is silenced but by gitleaks'
default configuration and the scanner configuration's one entry form (`07` MSG-ENG-001).

*Source: M-012, M-014, M-015*

**Acceptance criteria**
1. A commit carrying a credential-shaped string fails the scan; the test builds that
   string at run time, in a repository it makes under `tmp/`, so no such string is ever
   written in the repository's own history.
2. The secret-scanning gate's script fails on the same string on a line that carries
   gitleaks' inline allow marker; where a `.gitleaksignore` is tracked or sits at the
   scanned repository's root, whatever it lists; and on a finding a `.gitleaksignore` in
   the script's working directory lists.
