# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- `AddHermes`, which registers the engine and checks its configuration keys when the host
  starts, a reloaded value outside its rule leaving the last valid one in force
- Kinds, categories and retired kinds declared in `AddHermes`, each checked when the host
  starts, the first outside its rule stopping the start
- The secret, consent, observer, protector and alert seams a host supplies, the start
  stopping where a required one is missing or two host protectors share an `Id`
- `hermes migrate`, the `Hermes.Cli` tool that applies the release's migrations to the
  schema `messaging` and grants the application's roles the use of its tables
- A start against a database lacking one of the release's migrations stops, naming it
- The secrets the application's used parts need, read when the host starts, a key
  missing, outside its form or without its write version stopping the start
- The key table, which records each key version's check value and when it was last used
  to write, a version whose bytes changed or that is in use but not held stopping the start
- The library's own content protector and the envelope every stored value is kept in, a
  value opening only in its row and for its person, by the protector its envelope names
- Alerts raised through the alert sink when a condition starts to hold and each repeat
  while it holds, cleared once, raised once however many processes see it; the
  `configuration-invalid`, `newer-release` and `content-key-limit` conditions
- The meter `Hermes` with the instruments `hermes.deliveries`, `hermes.attempts`,
  `hermes.dispatch.oldest_due_age` and `hermes.provider.duration`
- A category's label longer than 128 characters, counted as Unicode scalar values, or
  holding a lone surrogate, stops the start
- `Retry:Factor` and `Jobs:StallFactor`, decimal numbers read in the invariant culture
  and checked when the host starts and on each reload
- `Templates:Path`, taken from the host's content root, a path naming a file or a folder
  the process cannot read stopping the start, and one naming nothing holding no template
- `hermes migrate` checks that the database has each role of `applicationRoles` before it
  applies anything, grants the reading alone of the migration history and the use of the
  schema's sequences, and checks each grant, answering `messaging.migrate.grantfailed`
  and keeping none where one does not hold
- The provider contracts `IEmailProvider`, `IEmailSession`, `ISmsProvider` and
  `IPushProvider`, their messages, and the `Attempt` each call answers, which carries no
  text a provider answered
- `SmsText.Measure`, which answers an SMS text's encoding and its segments as the network
  counts them, never splitting an escape pair or a surrogate pair
- The rules a channel's values are checked against: an email address as an RFC 5322
  `addr-spec` of at most 254 characters, an SMS sender by 3GPP TS 23.040, the allowed
  number prefixes, and the push hosts a wildcard of at least three labels allows
- The shipped Web Push provider: RFC 8030 requests encrypted by RFC 8291 and signed by
  RFC 8292 with the subscription's VAPID key version, each token reused for an hour,
  over the named client `Hermes.Push`, which follows no redirect, carries the library's
  handler alone and logs no address, every answer classed and none of its text kept
- `AddHermesChannels`, which registers the shipped providers alone and checks the keys
  of each channel whose section is present when the host starts, and the VAPID keys
  where the shipped push provider is used, a value outside its rule stopping the start
- `AddHermes` registers the shipped providers and checks the keys of each channel a kind
  uses, a kind's `from`, `replyTo` and SMS sender, and the configuration given to the
  call, the start stopping on the first outside its rule
- The shipped SMTP provider: submission through MailKit to `Email:Host` over implicit TLS
  or `STARTTLS`, authenticated with `emailSubmission`, the server's certificate validated
  by the library with each named revocation source read online, one connection per
  session opened again once it broke, every answer classed and none of its text kept
- The send's types: `Send` and its values, `SendReceipt`, `SendRecord` with its
  deliveries, attempts and calls, `Result` and `Failure`, and the `IMessages` and
  `ITemplateSource` contracts, none of which prints a destination, a value or a person
- Templates rendered strictly and within their bounds: the allowed tags and filters, the
  library's `format_date` and `format_number`, HTML parts encoded, a render past its steps,
  over its channel's limit or unable to compute refused with its code
- Every kind's templates and the library's pages read when the host starts, from the
  host's template source, the template folder and the assemblies passed to
  `declare.Templates`, a part missing, not parsing or outside the allowed tags, filters,
  arguments, ranges and nesting stopping the start and naming it; the unsubscribe pages
  shipped in `en` and `ar`
- The send's tables: sends, deliveries, attempts and their calls, idempotency keys, the
  rate counters, notices and the job table, each closed set refused by the database
  outside its values
- The keyed hash under each held `hashKeys` version, the advisory locks' keys and a
  send's canonical form, in the layouts two releases share
- `IMessages.SendAsync`, which checks a send, renders it in its language and time zone,
  protects its content and destination and records one delivery per channel, on the
  host's transaction where one is given, waking the dispatchers when it commits; a send
  with an idempotency key recorded once inside its window
- `IMessages.CancelAsync`, which cancels a send's waiting deliveries, and
  `IMessages.ReadAsync`, which answers a send's deliveries, attempts and calls
