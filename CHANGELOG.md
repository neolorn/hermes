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
