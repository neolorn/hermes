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
