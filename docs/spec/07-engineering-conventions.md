# 07 — Engineering conventions

How the library is built. Every rule here is checked by the build, a test or a gate,
save the parts marked "checked in review", which the owner's review of each phase
checks.

---

## 1. Layout and setup

**MSG-ENG-001** — The repository SHALL hold one solution, `Hermes.sln`, with one project
per package of `04` MSG-LIB-001 under `src/` and one test project per source project
under `tests/`, and a sample host with its test project in a solution of its own under
`samples/`, built only from the packed packages.

**Values (M-012, M-013, M-015).** A source project is `src/<Package>`; its test project
`tests/<Package>.Tests`; the tests over the whole repository (its projects' references
and grants, its layout and shared settings, the secret scan, the contract tests over the
packages' built assemblies (MSG-ENG-004, MSG-ENG-007's type names, `04` MSG-LIB-002),
`04` MSG-LIB-001, `10` MSG-THR-004) live in `tests/Hermes.Repository.Tests`, the one
test project with no source project. Each project's namespace root is its name; a file's
namespace matches its folder; one public top-level type per file, a type's cases
declared in its file. Projects are flat but for these folders: in `Hermes`, `Sending`
(`01`), `Content` (`03`), `Endpoints` (`05`), `Operations` (`06`) and `Migrations`, into
which the migrations are generated; in `Hermes.Channels`, `Email`, `Sms` and `Push`
(`02`); every other project, the test projects and the sample host's included, keeps its
files at its root (which file goes in which folder is checked in review). A shipped
project's types are internal unless `04` MSG-LIB-002 makes them public, and its public
types sit at its root, so each is in its package's namespace; a test project's test
classes are public, since xUnit.net runs no other. `InternalsVisibleTo` is granted only
from a source project to its own test project; from `Hermes.Channels` to `Hermes`, and
`Hermes.Testing`, since the rules they need (the address and sender rules, the allowed
hosts and prefixes; the segment count is public, `SmsText` of `Hermes.Core`) live in
`Hermes.Channels`; and from `Hermes` to `Hermes.Cli`, which applies the migrations
`Hermes` holds (`06` MSG-OPS-002), and to `Hermes.Testing`, which removes, or keeps from
being registered, the engine's hosted dispatcher and `observer` job by their types and
applies the migrations for a test (`04` MSG-LIB-008). The packages are released together
(`04` MSG-LIB-001). The sample host has an invented domain naming no real product or
company (checked in review), declares a few kinds in an essential and an optional
category, sends through the in-memory providers of `Hermes.Testing`, maps every endpoint
group, and runs in the gate (MSG-ENG-012) from packages packed from the same commit into
`artifacts/packages/`, which `.gitignore` lists, at a version the gate passes it; it
alone has no lockfile and no locked restore, since its packages change with every
commit. The sample host and its test project are outside `Hermes.sln`, in
`samples/Samples.sln`, and `samples/nuget.config` adds `artifacts/packages/` to the
package sources for them alone, so no other restore needs the folder. Both
`nuget.config` files carry `packageSourceMapping`: the library's own package identifiers
from `artifacts/packages/` only (in `samples/nuget.config`), and every other identifier
from nuget.org only, so no package is taken from a source it was not mapped to.

The repository's other files: at its root the solution (`Hermes.sln`),
`Directory.Build.props`, `Directory.Packages.props`, `global.json`, `.editorconfig`,
`.gitignore`, `nuget.config` (the package sources), `.config/dotnet-tools.json` (the
local .NET tools), `CHANGELOG.md` and `RELEASE-NOTES.md` (the release notes of `06`
MSG-OPS-007: one section per release, headed by its version and written in its release
commit, holding the deployment's duties as they stand at that release and the migrations
of that release that drop or rewrite data, `06` MSG-OPS-002); the pipeline's definition
under `.github/workflows/`; the gates' scripts under `build/gates/`, with the secret
scanner's configuration there and `build/gates/release-baseline.json`, the release
gate's baseline (`04` MSG-LIB-006), and `build/gates/built-items.txt`, the acceptance
criteria proven so far by a test, one per line as `MSG-SEND-006 AC2`, which the
test-name gate reads and each phase extends with the criteria it proves. The secret
scanner is gitleaks 8.30.1, its download checked against its published SHA-256, which
the fetch script itself holds (checked in review), and left under `tmp/tools/` by one
step of `build/gates/` that the secret-scanning and the test gates both run first. Its
configuration extends gitleaks' default and adds entries of one form only, for a
published test vector the tests must hold as a literal (RFC 8291 section 5's receiver
private key and authentication secret): one file under `tests/` and one exact value,
both required to match, with the document and section as its reason; every other secret
a test needs (the SMTP test authority's keys, a fixed secret of `Hermes.Testing`) is
made at run time. No allow-list but gitleaks' default and this one form reaches the
scan: it runs with `--ignore-gitleaks-allow`, so a line's inline `gitleaks:allow` marker
silences nothing, with `--gitleaks-ignore-path` naming a folder that holds no file, and
with no `--baseline-path`; and it fails where a `.gitleaksignore` (a list of findings
gitleaks would otherwise skip) is tracked or sits at the scanned repository's root,
where gitleaks reads one whatever its flags say. The gates' scripts are PowerShell 7
scripts, and the pipeline runs on GitHub's `ubuntu-24.04` runner using no action but
`actions/checkout` and `actions/setup-dotnet` (the SDK of `global.json`), each at its
latest release when the workflow is written and pinned by that release's full commit SHA
(all checked in review).

*Source: M-012, M-013, M-015, M-020*

**Acceptance criteria**
1. A test fails on a project outside this layout, a grant outside this rule, or an entry
   the repository adds to the scanner's configuration that is not of the form above.
2. The sample host builds from the packages packed from the same commit, and its tests
   pass.

---

**MSG-ENG-002** — Every project SHALL target .NET 10 with the settings of one committed
`Directory.Build.props`, and the SDK SHALL be fixed in `global.json`.

**Values (M-012, M-015).** `Nullable` enable, `ImplicitUsings` disable,
`TreatWarningsAsErrors` true, `MSBuildTreatWarningsAsErrors` true (MSBuild's and the
SDK's own warnings, which `TreatWarningsAsErrors` leaves warnings), `AnalysisLevel`
latest-all, `EnforceCodeStyleInBuild` true, `Deterministic` true,
`GenerateDocumentationFile` true, `EnableConfigurationBindingGenerator` true,
`NuGetAudit` true with `NuGetAuditMode` all; none overridden by a project. `global.json`
names a .NET 10 SDK with `rollForward` `latestPatch`. Package versions live in
`Directory.Packages.props` (Central Package Management) with a committed lockfile per
project, restored in locked mode in the pipeline (the sample host aside, MSG-ENG-001).
The shared-framework rule is `04` MSG-LIB-001's. Of the analysers' rules,
`.editorconfig` turns off only these, each with a comment line above it giving the
reason: CA1707 under `tests/` and in the sample host's test project (a test's name
carries its criterion's identifier, MSG-ENG-008); CA1515 in the same places (xUnit.net
runs only public test classes, in an executable test project); CA1812 under `src/`,
under `tests/` and in the sample host (an internal class the dependency injection
container makes is made, though no code calls `new` on it); CA1030 for `IAlertSink` (an
alert is an awaited call with a token, not an event); CA1034 under `src/Hermes.Core/` (a
case of a type with cases is a nested record reached through it,
`new Attempt.Accepted(...)`, `04` MSG-LIB-002); CA1303 everywhere (exception messages
are for developers and never shown to people); CA1032 for `MessagingStartupException`
(it always carries a code and details, which the standard constructors would leave out);
CA1861 under `src/Hermes/Migrations/` (EF Core's generator passes literal column arrays
to the migration operations, and a migration runs once); and CS1591 and CA2007 under
`tests/` and in the sample host's test project (a test states no contract of its own,
and xUnit.net's own analyser refuses the `ConfigureAwait(false)` CA2007 asks for). Every
project carries `[assembly: CLSCompliant(false)]`, written once in
`Directory.Build.props`, for CA1014. Formatting is defined once in `.editorconfig`,
which is the output of `dotnet new editorconfig` for the SDK of `global.json`,
unchanged, with the turn-offs above added (checked in review), and checked by
`dotnet format --verify-no-changes`.

*Source: M-012, M-015*

**Acceptance criteria**
1. No project file sets a target framework, a listed property or a package version, and
   `Directory.Packages.props` sets no property but `ManagePackageVersionsCentrally`; no
   project file, `Directory.Build.props` or `Directory.Packages.props` turns a warning
   off or keeps it from failing the build, by setting, whatever its value, a property
   among `NoWarn`, `WarningsNotAsErrors`, `WarningLevel`, `MSBuildWarningsAsMessages`,
   `MSBuildWarningsNotAsErrors`, `RunAnalyzers`, `RunAnalyzersDuringBuild`,
   `EnableNETAnalyzers`, `CodeAnalysisRuleSet`, `NuGetAuditLevel`, `AnalysisMode`, and
   the `AnalysisLevel` or `AnalysisMode` of one category of rules, by an item's metadata
   (`NoWarn` on a reference) or by an item (`NuGetAuditSuppress`, an analyser
   configuration file); and no `.editorconfig` or `.globalconfig` but the root
   `.editorconfig` is tracked or sits in the working tree outside `tmp/` and the
   projects' `bin/` and `obj/`.
2. A warning fails the build, the compiler's, an analyser's, NuGet's, MSBuild's or the
   SDK's alike; a locked restore that differs from the lockfile fails.

---

## 2. Design

**MSG-ENG-003** — An expected outcome SHALL be returned as a result, never thrown; a
fault SHALL be thrown.

**Values (M-012).** `Result` and `Result<T>` (`Hermes.Core`) carry success or a failure
with its code and details (`08` section 1). A refusal that a person, a provider or a
host's data can cause is a failure: a destination that does not read, a rendered message
over its limit or a render over its steps (`03` MSG-TPL-007). A mistake only the calling
code can make is a fault: an undeclared kind, a value a template uses that the send does
not pass, a reserved value name; it throws `ArgumentException` or
`InvalidOperationException`, and a broken invariant throws. A `catch` never turns a
fault into a success, and an exception is never swallowed: it is handled, rethrown, or
logged and the operation answers a failure.

*Source: M-012*

**Acceptance criteria**
1. No public operation throws for a code of `08` section 1; a "(startup)" code is thrown
   only as a `MessagingStartupException` while the host starts (`04` MSG-LIB-003).

---

**MSG-ENG-004** — Every type SHALL be sealed unless designed for inheritance, every
asynchronous method SHALL take a `CancellationToken`, data crossing a boundary SHALL be
immutable, and there SHALL be no static mutable state and no reflection.

**Values (M-012).** Records for data, `init` members, read-only collections; JSON
through source-generated contexts only (`05` MSG-API-001); configuration through the
binder's source generator and source-generated options validation (`06` MSG-OPS-001);
logging through source-generated `LoggerMessage` methods (`06` MSG-OPS-005); the clock
through `TimeProvider`; random values through `RandomNumberGenerator`; the built-in
dependency injection container only. Every public type and member of a shipped project
carries XML documentation stating its contract, the failures it answers and the faults
it throws. Every comparison of a secret or a value derived from one (a token's HMAC, the
report key) uses `CryptographicOperations.FixedTimeEquals`.

*Source: M-012*

**Acceptance criteria**
1. A contract test reads the built assemblies' metadata (`System.Reflection.Metadata`,
   in tests only) and fails on a public class neither sealed nor abstract (a public type
   designed for inheritance is abstract), and on a public asynchronous method without a
   `CancellationToken` (save `IAsyncDisposable.DisposeAsync`); the build fails on a
   shipped project's public member without documentation.
2. A test fails on a lookup or call of members by name (`System.Reflection` member
   lookups, `Activator`, `Type.GetType`) outside generated code; reading an assembly's
   embedded resources is allowed.

---

**MSG-ENG-005** — Persistence SHALL go through EF Core with the Npgsql provider over the
library's own `DbContext`, and every multi-row change SHALL be one transaction.

**Values (M-005, M-012, M-013).** Hand-written SQL, where EF Core cannot express a
statement (the claim of `01` MSG-SEND-007, the places and counters of MSG-SEND-014,
`pg_notify` and `LISTEN`, advisory locks), lives beside the code that needs it and takes
its values as parameters. Every advisory lock is transaction-scoped
(`pg_advisory_xact_lock`, or `pg_advisory_xact_lock_shared` where `01` says shared),
taken inside the transaction it serialises, so a pooler never strands one. Every
transaction of the library, save the migrations', whose tool reads no call timeout, sets
`idle_in_transaction_session_timeout` (`SET LOCAL`) to twice `Hermes:Dispatch:Timeout`,
and a push registration's to four times it, since its three protector calls, each
bounded by that timeout, run between two of its statements (`02` MSG-PUSH-003), so the
server ends a session that stalls inside one (a frozen or cut-off process) and frees its
locks, rather than leaving them held for as long as the connection lingers. A
transaction that changes several rows of the library's tables locks them in one order
(checked in review), the deliveries first, then push subscriptions, notices and places,
then every other table's rows, one table at a time, each by identifier
(`SELECT ... ORDER BY id FOR UPDATE`), so no two of the library's own transactions
deadlock on rows; a statement that skips locked rows (a claim) keeps its own order,
since it never waits; one of its own transactions that PostgreSQL aborts all the same,
for a deadlock (40P01) or a serialization failure (40001), is run again up to
`Hermes:Database:Retries` times (default 3), after which a pass or a job leaves the work
to its next run, as a stopped holder does, and a library call, a route or a start lets
the database's error through. A host's transaction is never run again by the library
(`01` MSG-SEND-004). Every statement takes the current instant as a parameter, `@now`,
from the library's `TimeProvider`, never the database's `now()`, so a test clock governs
the database's view of time as it governs the code's (`06` MSG-OPS-003). A column
holding a closed set is `text` with a `CHECK` constraint (`06` MSG-OPS-003). Identifiers
are UUID version 7; a delivery reference (`00` glossary) is 128 bits from
`RandomNumberGenerator`, written as base64url.

*Source: M-005, M-012, M-013*

**Acceptance criteria**
1. A status or channel outside the closed set is refused by the database.
2. No hand-written statement calls `now()`, `current_timestamp` or `clock_timestamp()`.
3. No hand-written statement calls `pg_advisory_lock` or `pg_advisory_lock_shared`.
4. A transaction of the library's own that PostgreSQL aborts with 40001 is run again, at
   most `Hermes:Database:Retries` times.

---

**MSG-ENG-006** — Endpoints SHALL be ASP.NET Core minimal APIs returning typed results,
each declaring the responses `05` gives it.

*Source: M-012*

**Acceptance criteria**
1. A contract test reads every mapped endpoint's metadata and finds exactly the routes,
   methods and response statuses of `05`, and finds each request and response body's
   members, read from the library's serializer contexts as the release gate reads them
   (`04` MSG-LIB-006), to be those `05` gives.

---

## 3. Names

**MSG-ENG-007** — Codes SHALL be lower case, dot-separated, starting with `messaging.`
(`messaging.send.nodestination`); configuration keys SHALL be the configuration paths of
`08` section 4 under `Hermes`; enumerations SHALL be spelled on the wire as `08` spells
them; and no type SHALL be named after the library.

**Values (M-012).** The library's name appears only in namespaces, project and package
identifiers, the solution file (`Hermes.sln`), the configuration section, the
registration and mapping methods (`AddHermes...`, `MapHermes...`), the command of
`Hermes.Cli`, and the public names of `08` section 8 that `08` lists as changing with
it.

*Source: M-012*

**Acceptance criteria**
1. A test fails on a code the library answers that `08` section 1 does not list, a
   configuration key it binds that section 4 does not list, a wire spelling that differs
   from `08`, a metric instrument a test host's meter publishes that section 8 does not
   list, and a public type whose name holds the library's name.

---

## 4. Testing

**MSG-ENG-008** — Tests SHALL be written first (checked in review), use xunit.v3 on
Microsoft.Testing.Platform and hand-written fakes, and name the acceptance criterion
they prove.

**Values (M-012, M-013, M-015).** Each test carries the trait `Category` with one of
`unit` (nothing external), `integration` (something outside the process: real PostgreSQL
in a container through Testcontainers, the image `postgres:16.15` (the floor, `06`
MSG-OPS-007), one container per test class that needs one; `git` and the secret scanner;
or the SDK's build and pack, writing under `tmp/`) and `contract` (the public surface,
the endpoints and the reference tables); the sample host's tests are a fourth kind in
their own project. The tests of `tests/Hermes.Repository.Tests` that run `git` and the
secret scanner (`10` MSG-THR-004) are `integration`, running the scanner the gates'
fetch step leaves under `tmp/tools/`. The pipeline runs each kind on its own, by that
trait; a run that finds no test of its kind passes (Microsoft.Testing.Platform's exit
code 8 for no test is accepted), so a phase with none of a kind yet is not failed for
it, and a test of `tests/` that carries none of the three kinds, or more than one, fails
the gate, since no run, or more than one, would take it. An acceptance-criterion test is
named `ITEM_ACn_Outcome` (`MSG_SEND_006_AC2_PreferenceTurnedOffStopsDelivery`); every
other test `Method_Scenario_Outcome`. No mocking package. Providers are tested against
servers that speak their protocol: a scripted SMTP listener in the test process,
speaking SMTP over TLS with certificates from a test authority whose revocation list a
listener in the test process serves, and answering each step as a test sets; an HTTP
listener in the test process for the SMS and push providers, reached by replacing the
named clients' primary handlers (`02` MSG-CHAN-001). Every criterion a test can decide
has one; the rest are named in the phase report with how they were checked.

*Source: M-012, M-013, M-015*

**Acceptance criteria**
1. No mocking package is referenced.
2. Every testable criterion of a built item has a test carrying its identifier.
3. A test of `tests/` that carries no `Category` trait, more than one, or a value other
   than the three, fails the tests gate.

---

## 5. Dependencies

**MSG-ENG-009** — These SHALL be the only third-party packages, each referenced only by
the projects `04` MSG-LIB-001 allows.

| Purpose | Package | Licence |
|---|---|---|
| Data access | `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL`; `Microsoft.EntityFrameworkCore.Design` (making migrations, build-only); `dotnet-ef`, a local .NET tool at the EF Core version, in `.config/dotnet-tools.json` (making migrations) | MIT, PostgreSQL |
| Hosting abstractions | `Microsoft.Extensions.DependencyInjection.Abstractions`, `Microsoft.Extensions.Configuration.Abstractions`, `Microsoft.Extensions.Configuration.Binder`, `Microsoft.Extensions.Options`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Http` | MIT |
| Email | `MailKit` (bringing `MimeKit`) | MIT |
| Browser push | `Lib.Net.Http.WebPush` (bringing `Lib.Net.Http.EncryptedContentEncoding`) | MIT |
| Templates | `Fluid.Core` | MIT |
| Public surface | `Microsoft.CodeAnalysis.PublicApiAnalyzers` (build-only) | MIT |
| Versioning | `MinVer` (build-only) | Apache 2.0 |
| Tests | `xunit.v3`, `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing` | Apache 2.0, MIT, MIT |

MailKit 4.18.2, Fluid.Core 2.40.0 and Lib.Net.Http.WebPush 3.3.1 are pinned at those
versions, whose listings were checked on 2026-10-21 (MailKit's on 2026-10-26) and
against whose sources the chapters' claims about them were checked (MailKit's against
4.18.1, whose code 4.18.2 keeps, M-017), so moving one, for a security update too
(MSG-ENG-010), is a decision-log entry; every other package is taken at its latest
stable release when added, the Microsoft packages at the release that matches the SDK's
.NET 10 runtime, and its listing checked then (all checked in review). The reasons and
the rejected alternatives are M-002, M-013 and M-017. Adding a package is a decision-log
entry.

*Source: M-002, M-012, M-013, M-015, M-017, M-020*

**Acceptance criteria**
1. The set of direct package references is within this table, and the tool manifest
   names `dotnet-ef` alone; a pull request adding another fails a check.

---

**MSG-ENG-010** — A package with a known vulnerability SHALL fail the build,
vulnerability alerting SHALL be on for the repository, and a security update SHALL be
taken as soon as it lands (checked in review).

**Values (M-012).** NuGet's audit of every package, transitive ones included, runs in
each restore (MSG-ENG-002), and its warnings fail the build.

*Source: M-012*

**Acceptance criteria**
1. A restore resolving a package version with a published advisory fails the build.

---

## 6. Version control and gates

**MSG-ENG-011** — Work SHALL be trunk-based with short-lived branches reaching the
default branch by pull request (save the first, `docs:` commit, which the implementer
pushes to the empty default branch before setting its protection), commit messages SHALL
follow Conventional Commits 1.0.0, a `CHANGELOG.md` SHALL follow Keep a Changelog 1.1.0,
and MinVer SHALL derive the version from the tag.

**Values (M-012, M-015).**
- **Commit message:** a type of `feat`, `fix`, `refactor`, `perf`, `test`, `docs`,
  `build`, `ci`, `chore`; an optional scope naming the package's area (`core`,
  `channels`, `engine`, `testing`, `cli`, `sample`); a description in the imperative
  (checked in review), beginning with a lower-case letter (a name it mentions keeps its
  own spelling: `add the SMTP provider`), with no full stop, the first line at most 72
  characters; a body only where the diff cannot explain itself (checked in review), as
  dash fragments of at most 72 characters, each a reason or a non-obvious consequence; a
  footer carrying the items the commit implements (`Implements: MSG-SEND-006`) and
  nothing else; `!` or `BREAKING CHANGE:` for a break of `04` MSG-LIB-006.
- **Changelog:** every commit that changes behaviour, one of type `feat`, `fix` or
  `perf` or marked `!`, adds its line under `Unreleased` in the same commit (`Added`
  while nothing is released).
- **History:** no force push to the default branch, and no merge of a red check. A
  commit that is not pushed and that no other branch has merged MAY be amended or
  rebased to meet a gate; history that is pushed or merged is never rewritten.
  `PublicAPI.Shipped.txt` changes only in a release commit, as
  `build/gates/release-baseline.json` does (`04` MSG-LIB-006), save that one holding no
  entry (nothing but `#nullable enable` and blank lines) MAY be deleted in the commit
  that deletes the project file beside it, since it records nothing released (M-021).
- **Releases:** a release is a tag `v<major>.<minor>.<patch>` on the default branch,
  made by the implementer on the merged release commit when the owner's message says to
  release; the pipeline packs the packages from the tag and attaches them to the tag's
  release, and where the owner names a package feed, publishes them there too (M-020).
  Each package carries its identifier, its description (the text of its "What it holds"
  cell in `00` section 2.2, without markup or references), as its authors the name the
  owner's first message of phase 0 gives, and the repository's address, and `Hermes.Cli`
  also `PackAsTool` and `ToolCommandName` (`hermes`); phase 0 sets them, in
  `Directory.Build.props` and the project files, and phase 1 `Hermes.Cli`'s two with its
  project, and every other package property is left unset. A package's dependency on
  another of the library's packages is the exact version packed (`[v]`, `04`
  MSG-LIB-001): `dotnet pack` writes a project reference as that version or later, and
  the .NET 10 SDK has no setting for an exact one, so `Directory.Build.props` holds one
  target, run after NuGet's `_GetProjectReferenceVersions`, that sets the
  `ProjectVersion` of each of NuGet's `_ProjectReferencesWithVersions` items to `[v]`;
  both are names inside NuGet's pack targets, not a documented setting, so `04`
  MSG-LIB-001's criterion 3 is what catches an SDK that changes them.

*Source: M-012, M-014, M-015, M-020, M-021*

**Acceptance criteria**
1. A commit message outside the rule, and a behaviour-changing commit without a
   changelog line, fail a check.
2. A commit that changes the content of an existing `PublicAPI.Shipped.txt` or
   `build/gates/release-baseline.json`, or deletes one, fails a check, save the release
   commit, whose exemption comes with the release step (plan, phase 8); a file that
   moves unchanged with its project (the rename of phase 8) is neither; nor is a
   `PublicAPI.Shipped.txt` holding no entry deleted in the commit that deletes the
   project file beside it (M-021). The same deletion while that project file stays, or
   the deletion of a file that holds an entry, fails.

---

**MSG-ENG-012** — The pipeline SHALL block on every gate below, and each gate SHALL be a
script the implementer runs locally with the same result, save the log gate, which the
integration tests' run checks.

| Gate | Source |
|---|---|
| Build and analysers, warnings as errors | MSG-ENG-002, MSG-ENG-004 |
| `dotnet format --verify-no-changes` | MSG-ENG-002 |
| Locked restore and the vulnerability audit | MSG-ENG-002, MSG-ENG-010 |
| Unit, integration and contract tests | MSG-ENG-008 |
| Public surface files up to date | `04` MSG-LIB-002 |
| Release compatibility of the public surface, endpoints and wire shapes, codes, configuration keys, enumerations and statuses, and metric names | `04` MSG-LIB-006 |
| Dependency allow-list | MSG-ENG-009 |
| Project references and grants | MSG-ENG-001; `04` MSG-LIB-001 |
| Migrations applied twice in a row to an empty database, the second applying nothing | `06` MSG-OPS-002 |
| No destination, content or person reference in any log line of the integration tests: each integration test captures its log lines with an in-memory logger provider and asserts at its end that none holds a destination, a content value or a person reference the test used; it is checked by the integration tests' run and has no script of its own | `06` MSG-OPS-005 |
| Secret scanning of the whole history (gitleaks) | `10` MSG-THR-004 |
| Commit message format, changelog line, and `PublicAPI.Shipped.txt` and the release baseline unchanged outside a release commit | MSG-ENG-011 |
| Acceptance-criterion test names present for each criterion of `build/gates/built-items.txt` | MSG-ENG-008 |
| The sample host builds from the packed packages, its code passes `dotnet format --verify-no-changes` on `samples/Samples.sln`, and its tests pass | MSG-ENG-001, MSG-ENG-002 |

The gates live as scripts under `build/gates/`, which the pipeline runs in one job,
`gates` (the one status check branch protection requires), on every pull request, every
push to the default branch and every release tag, the publish of MSG-ENG-011 waiting for
the last; the commit-message gate checks the commits a pull request or a push brings,
and for a release tag the commits since the previous release tag (the whole history for
the first), save the merge commits the platform writes, while the secret scan reads the
whole history each time (`10` MSG-THR-004); a gate whose part is built in a later phase
(the migrations gate, plan phase 1; the release compatibility gate, phase 5; the sample
host's, phase 5a) has its script added by that phase, and the job runs every script
present. Before it pushes, the implementer runs the fast checks, every unit test, the
commit-message gate and the secret scan, and the integration tests it writes or its
change touches, never the whole integration set; the pipeline's `gates` run on the last
commit pushed, the report's own commit, is the gate of record, which the implementer
waits for in the same run and names with its result (the first `docs:` commit, pushed
before the gates exist, is checked by the secret scan alone) (M-018, M-019).

*Source: M-012, M-014, M-015, M-018, M-019*

**Acceptance criteria**
1. Each gate runs on pull request, and any failing blocks the merge.
2. Each gate's script, run locally on a commit, gives the result the pipeline gives on
   it, whether or not the implementer runs it before a push (M-019).
