# 00 — Overview

Specification for a reusable .NET library that **sends messages** (email, SMS and
browser push notifications) for any application that imports it, and that is designed
from the start to carry **conversations** (support chat with a queue and a shared inbox,
and staff group and private chats) in a later release.

`Hermes` is the library's name (M-022): its namespaces, its project and package
identifiers, its solution file, its configuration section, its registration and mapping
methods, the command of `Hermes.Cli` and the names `08` section 8 lists carry it. No
type is named after the library (`07` MSG-ENG-007), and the names that are stored or
shared by two releases (the schema, the protector's `Id`, the notification channel, the
key-check text) are neutral and never change.

Intended experience for a new project: **import → declare what it sends → send.**

**Status:** specification. Everything needed to build is in documents 00 to 10.
`decision-log.md` records why each choice was made and what was rejected; it is
background and never required to build. Requirements cite their source decision so the
reasoning is one lookup away.

---

## 1. Reading this specification

### 1.1 Document set

| Document | Covers |
|---|---|
| **00-overview** | This document: what the library is, its parts, what is in and out of the first release, glossary |
| **01-sending** | Kinds and categories, the send operation, the checks at dispatch, preferences, consent, suppressions, quiet hours, limits, dispatch, retries, statuses, retention and erasure |
| **02-channels** | Email, SMS and browser push: what each channel does and how its provider is called; the development inbox |
| **03-content** | Templates, languages, values, escaping, bounds and the checks made at startup |
| **04-library-contract** | Packages, the public surface and its signatures, registration, the seams a host supplies, versioning |
| **05-api-contract** | The endpoints the library ships and a host mounts |
| **06-operations** | Configuration, schema and migrations, background work, observability, alerts, secrets |
| **07-engineering-conventions** | Layout, setup, design rules, names, testing, dependencies, version control, gates |
| **08-reference** | Codes, enumerations, statuses, configuration keys, alert conditions, attempt reasons, secrets, metrics and other names |
| **09-conversations** | The conversation model, designed now and built in a later release |
| **10-threat-model** | Assets, threats and their controls |
| `decision-log.md` | Rationale. **Supplementary; not required to build** |

`08-reference` is authoritative for every name, code, key, default and status, and
`05-api-contract` for every route and wire shape; where another chapter names one, it is
looked up there.

### 1.2 Requirement format

Every requirement carries an identifier, a normative statement and its source decision.
**Acceptance criteria accompany every requirement that can be tested.** A requirement
that states a design constraint for a later release, or a fact, carries none.

> **MSG-SEND-001** — A message SHALL be sent only as one of the kinds the host declared
> at startup.
>
> *Source: M-005*

Identifiers are stable. A withdrawn requirement's identifier is retired, never reused.
Every identifier starts with `MSG-`, so it never collides with an identifier of another
library the host uses.

### 1.3 Normative language

**SHALL** / **SHALL NOT**: mandatory; a build that does otherwise is non-conformant.
**SHOULD** / **SHOULD NOT**: strong default; departure requires a recorded reason.
**MAY**: genuinely optional. Anything not marked is descriptive context.

### 1.4 Acceptance criteria

Each criterion is objectively checkable and written to be turned into a test. A
requirement whose criteria cannot be checked without human judgment is under-specified
and is raised, never guessed at.

### 1.5 Lengths

A length in characters counts Unicode scalar values (code points other than surrogates),
wherever its passage names no other unit: printable ASCII characters count one each
(`01` MSG-SEND-009), an SMS text counts in the units of its encoding (`02` MSG-SMS-001),
and an email address counts in its ASCII form, the form that is sent (`01` MSG-SEND-003;
`02` MSG-EMAIL-002). A string holding a surrogate that is not part of a pair is not
text, and fails the check of its length.

*Source: M-016*

---

## 2. What this is

**MSG-OVR-001** — The library SHALL be **product-agnostic**: it SHALL NOT name, assume
or depend on any host's applications, business, data or layout. Where a part runs (which
of a host's applications mounts which endpoints, which process runs the background work)
is the host's choice.

*Source: M-001*

**Acceptance criteria**
1. No identifier, text, default or test fixture of the library names a host product,
   company or application (checked in review, since a test would have to name them).
2. Every endpoint group of `05` can be mounted on its own, in any application of the
   host, and works without the others being mounted in the same application.

---

**MSG-OVR-002** — The library SHALL be **a library, not a service**: imported by the
host and running in-process, with its own database schema and migrations, its own
endpoints mounted where the host chooses, and its own background work run by the host's
process. It SHALL NOT read or write the host's tables.

*Source: M-001*

**Acceptance criteria**
1. Nothing of the library needs a process, container or network service of its own; its
   only outside dependencies are the host's PostgreSQL database, the providers it is
   configured for, and the certificate authorities' revocation services the submission
   server's certificate names (`02` MSG-EMAIL-001).
2. Every table the library uses lives in its own schema (`06` MSG-OPS-002).

### 2.1 Tech stack

| Layer | Choice |
|---|---|
| Runtime | .NET 10 |
| Database | PostgreSQL, through EF Core and the Npgsql provider |
| Email | SMTP submission through MailKit |
| SMS | A gateway behind a contract, through the provider the host registers (`02` MSG-CHAN-001) |
| Browser push | Web Push (RFC 8030, 8291, 8292) through Lib.Net.Http.WebPush |
| Templates | Liquid, through Fluid |

Versions are pinned in `07` MSG-ENG-009.

*Source: M-001, M-002, M-013, M-022*

### 2.2 The parts

| Package | What it holds | Who uses it |
|---|---|---|
| `Hermes.Core` | The public types and contracts every other package shares | Every other package |
| `Hermes.Channels` | The channel providers (SMTP email and Web Push), the SMS channel's rules, and their registration. No database, no templates, no background work | An application that only hands a message to a provider |
| `Hermes` | The sending engine: kinds, the outbox, the checks, dispatch, templates, the endpoints, the schema | An application that sends its own messages |
| `Hermes.Testing` | In-memory providers, fixed secrets, an on-demand dispatcher and a test database's migration | A host's tests, and the sample host (`07` MSG-ENG-001) |
| `Hermes.Cli` | A .NET tool, command `hermes`, that applies the migrations (`06` MSG-OPS-002) | A deployment's pipeline |
| `Hermes.Development` | The development inbox: in Development, every message kept on the machine instead of sent, with a page, a JSON API and a test helper to read it. It does nothing in any other environment | An application while it is developed, and its tests and tools (`02` MSG-DEV-001) |
| `Hermes.Conversations` | Conversations (`09`). **Release 2** | An application with support or staff chat |

The engine depends on the channels and never the reverse, so an application that needs a
provider alone never carries the engine. No package knows another library: a host that
uses this library beside others (an identity library, say) connects them in its own
code, through the seams of `04` MSG-LIB-005 and the providers of `02` (M-020).

*Source: M-001, M-003, M-020, M-022, M-024*

---

## 3. The first release, and what it is designed for

**MSG-OVR-003** — The first release SHALL send **email, SMS and browser push**, and
SHALL be designed so that each item below is added later without a breaking change to
the public surface (`04` MSG-LIB-006):

| Designed for, not built | Where its design lives |
|---|---|
| Conversations: support chat with a queue, availability, transcripts, a survey, automatic replies, attachments; staff company, team and private chats | `09` |
| Voice and video in a conversation; customer email in the support inbox; response-time targets; a help center; an AI assistant | `09` MSG-CONV-012 |
| Push to native phone apps (Firebase Cloud Messaging, Apple Push Notification service) | `02` MSG-PUSH-008 |
| Safari's declarative Web Push payload | `02` MSG-PUSH-002 |
| WhatsApp, and any other channel | `02` MSG-CHAN-002 |
| Falling back to another channel when one fails | `01` MSG-SEND-020 |
| Email attachments | `02` MSG-EMAIL-003 |
| Email bounce and complaint feedback | `02` MSG-EMAIL-007 |

The first release candidate, `1.0.0-rc.1`, holds the in-application notification list
(`02` section 5) with all that came before it (`04` MSG-LIB-006, M-027).

*Source: M-001, M-004, M-026*

---

## 4. Glossary

| Term | Meaning |
|---|---|
| **Host** | The application that imports the library |
| **Kind** | A message the host declares: its name, category, channels and templates (`01`) |
| **Category** | A group of kinds sharing one rule for opting out, consent, quiet hours and limits (`01`) |
| **Recipient** | Who a message is for: a person the host identifies by its own reference, or someone anonymous, known only by a destination |
| **Person reference** | An opaque string the host gives for a person, the same for every message to that person; the library never interprets it |
| **Destination** | An email address or a phone number; for push, the person's subscriptions; for in-app, none |
| **Send** | One call that asks for one kind to go to one recipient on one or more channels |
| **Delivery** | One send on one channel: dispatched, retried and reported on its own; an email or SMS delivery has one destination, a push delivery goes to each of the person's subscriptions, and an in-app delivery writes one entry to the person's list |
| **Attempt** | One try at a delivery: its checks and its provider calls (one, or for push one per subscription); an attempt answered at a check makes no call |
| **Provider** | What carries a delivery: an SMTP server, an SMS gateway, a push service, or for in-app the library's own list |
| **Outbox** | The library's table of deliveries waiting, in flight or finished |
| **Quiet hours** | The hours, in the recipient's time zone, in which a category's deliveries wait, where its quiet hours apply |
| **Suppression** | A destination the library will not send a category to, after an unsubscribe |
| **Delivery reference** | 128 random bits the library draws for each attempt and passes to the provider, so a report can name the attempt it reports on |
| **Provider reference** | What a provider answers to identify a message it took, where it answers one |
| **Entry** | One item of a person's in-application list: an in-app delivery's parts in every declared language, kept for the host to show (`02` section 5) |
