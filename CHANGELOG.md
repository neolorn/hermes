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
