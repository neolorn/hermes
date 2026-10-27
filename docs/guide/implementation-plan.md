# Implementation plan

The order in which the specification is built. What each item means is in `docs/spec/`;
this plan only orders it.

## 1. How to read the specification

Read `00-overview`, `07-engineering-conventions` and `04-library-contract` in full
before the first phase. `08-reference` is authoritative for every name, code, key,
default and status, and `05-api-contract` for every route and wire shape.
`decision-log.md` is rationale.

## 2. Working rules

- Each phase is built with tests first, one logical change per commit, the fast checks
  on every commit, the commit-message gate and the secret scan before each push, and the
  full gate (`07` MSG-ENG-012) in the pipeline on every push, its run on the last commit
  pushed being the gate of record (M-018), then a report in the shape of section 5. A
  phase begins only when the one before it has a clean report and its pull request is
  merged into `main` after the owner's review.
- A phase builds the part of each item it names; where an item is split across phases,
  the table says which part each builds. A criterion that needs an item of a later phase
  is proven in that phase, and the report of the earlier one names it as waiting there.
  Where an item needs behaviour of a later phase's item (a check of `01` MSG-SEND-006
  whose item is built later, the `unsubscribe` value before its token), that behaviour
  is built in the later phase, with the criterion that proves it.
- Each phase implements, in code, the rows of `08` that its items use: the codes, keys,
  enumerations, statuses, alert conditions, reasons and names.
- Items designed for and not built (the rows of `00` MSG-OVR-003, `02` MSG-PUSH-008 and
  MSG-CHAN-002, every paragraph marked "designed for, not built", and `09`) are placed
  in no phase; what the first release must do for them is stated in the items that are.
- A question about the specification parks its item and what depends on it; the rest of
  the phase goes on.

## 3. Release 1: sending

| # | Phase | Items | Proves |
|---|---|---|---|
| 0 | Repository and gates | The docs committed first; `07` all but the sample host (phase 5a), Janus's source (phase 6) and `RELEASE-NOTES.md` (phase 8) of MSG-ENG-001, MSG-ENG-006 (phase 5a), Janus's package in MSG-ENG-009's allow-list, the release compatibility gate of MSG-ENG-012 (phase 5), the migrations gate of MSG-ENG-012 (phase 1, with `hermes migrate`) and the release step of MSG-ENG-011, the gates as scripts; `04` MSG-LIB-001 (all but Janus's package and the sample host's row), MSG-LIB-002 (the projects and their empty surface files, `Hermes.Cli`'s project coming in phase 1 with `hermes migrate`, since a tool cannot build without its entry point; each type of MSG-LIB-002 then comes with the first item that uses it); `00` MSG-OVR-001 criterion 1; `10` MSG-THR-004 | An empty solution passes every gate of `07` MSG-ENG-012 that applies to it, locally, and in the pipeline on its pull request |
| 1 | Foundations | `07` MSG-ENG-012's migrations gate; `06` MSG-OPS-001, MSG-OPS-002, MSG-OPS-003, MSG-OPS-005, MSG-OPS-006, MSG-OPS-008; `04` MSG-LIB-003, MSG-LIB-005; `01` MSG-SEND-001, MSG-SEND-002, MSG-SEND-018; `00` MSG-OVR-002 | A host registers with one call; a declaration, configuration value or secret outside its rule stops the start naming it; `hermes migrate` applies twice, the second time nothing; a protected value opens in its row and nowhere else; a key changed in place stops the start |
| 2 | Providers | `02` MSG-CHAN-001, MSG-EMAIL-001 to MSG-EMAIL-005, MSG-EMAIL-007, MSG-SMS-001, MSG-SMS-002, MSG-PUSH-001, MSG-PUSH-002, MSG-PUSH-005, MSG-PUSH-007: the SMTP and Web Push providers, each call and the classing of its answers; the SMS provider contract, the segment count and the sender rule | The SMTP and Web Push providers each send to a local server speaking their protocol and class every answer; the push encryption checks against RFC 8291 |
| 3 | The send and the dispatcher | `01` MSG-SEND-003 to MSG-SEND-009, MSG-SEND-015 to MSG-SEND-017; `03` all; `02` the size refusals of MSG-EMAIL-003, MSG-SMS-001 and MSG-PUSH-002; `06` MSG-OPS-004 (the dispatch and observer jobs); `04` MSG-LIB-008 | A declared kind, recorded in a host transaction, is rendered, wakes the dispatchers on commit, is dispatched once by two dispatchers, retried, and ends in one final status the observer is told of |
| 4 | The person's choices and the endpoints | `01` MSG-SEND-010 to MSG-SEND-014; `02` MSG-EMAIL-006, MSG-SMS-004 to MSG-SMS-006 (against a test SMS provider), MSG-PUSH-003, MSG-PUSH-004, MSG-PUSH-006; `05` all; `04` MSG-LIB-004; `06` MSG-OPS-004 (the balance, subscription and watch jobs); `00` MSG-OVR-001 criterion 2 | Every check of `01` MSG-SEND-006 stops what it names; every endpoint group works mounted alone |
| 5 | Retention, erasure and the release gate | `01` MSG-SEND-019, MSG-SEND-020; `06` MSG-OPS-004 (the retention job); `04` MSG-LIB-006 (the compatibility gate, against an empty baseline); `10` MSG-THR-001 to MSG-THR-003 | Erasure leaves nothing naming the person; no log line of the integration tests holds a destination or content; every control of the threat model has its test |
| 5a | The sample host and the endpoint contract | `07` MSG-ENG-001 (the sample host, and its gate of MSG-ENG-012), MSG-ENG-006; `04` MSG-LIB-001 for the sample host's row in the references test | The sample host builds from the packages packed from the same commit and its tests pass in the gate; the endpoint contract test finds exactly the routes, statuses and bodies of `05` |
| 6 | The bridges | `04` MSG-LIB-007, and MSG-LIB-001 for Janus's package; `07` MSG-ENG-001 (Janus's source with its mapping in the root `nuget.config`), and MSG-ENG-009 for Janus's package in the allow-list | Janus's sign-in codes go out through `Hermes.Janus.Channels`, and the sample host still builds and passes |
| 7 | The WE adapter | `02` MSG-SMS-003, and the report routing of MSG-SMS-004 and `04` MSG-LIB-007 | An SMS reaches a real number through WE and its report reaches its delivery |
| 8 | Release 1.0.0 | The rename to the final name; `04` MSG-LIB-006 (the first baseline); `07` MSG-ENG-011 (the tag and the publish); `06` MSG-OPS-007 (the release notes) | Version 1.0.0 of every package on the package feed, with its changelog and release notes |

**Waiting on inputs.**
- Phase 6: the Janus identity library's package (its identifier, its feed and the
  version to use) and the reference of the parts of its API the bridges use (its
  transports, request access, per-person field encryption, subject events and export
  contributor); the type and encrypted fields the bridge declares to Janus, and the
  answer its SMS transport gives Janus for a number outside
  `Hermes:Sms:AllowedPrefixes`, are named in `04` MSG-LIB-007 once that reference is in
  hand. The bridge's consent source needs the consent reading the Janus identity library
  adds in its round H2; until then the bridge registers none, and a host that declares a
  category requiring consent supplies its own (`decision-log.md` M-003).
- Phase 7: WE's API document and the account's credential (`decision-log.md` M-008).
- Phase 8: the final name and the package feed, both the owner's; 1.0.0 includes the WE
  adapter, so it follows phase 7.

## 4. Release 2: conversations

Planned after release 1, from `09`. Its phases are written when release 1 is out, with
the acceptance criteria `09` leaves to that release.

## 5. Phase report

`docs/reports/phase-NN.md`, holding only:
- the items built, each with its tests' names;
- the items not built, each with its reason, and the criteria proven and the behaviour
  built in a later phase;
- every criterion checked without a test, with how it was checked;
- every criterion that only the pipeline's run can check (the pipeline half of phase 0's
  proof, `07` MSG-ENG-012's criteria), named as awaiting that run, whose result the
  message naming the pull request gives;
- every analyser suppression, with its justification;
- **Resolved by rule**;
- **Open questions**: every question the phase asked, answered or not, with the items it
  parked and the entry that answered it;
- the commit the report names, the fast checks and the checks before the push, green,
  the gate of record being the pipeline's `gates` run on the report's own commit, whose
  identifier and result the run's last message gives (`07` MSG-ENG-012, M-018).

The report is committed alone, in a `docs:` commit on top of the commit it names
(written again, in a new `docs:` commit, each time the phase's work goes on after a
push); the secret scan and the commit-message gate are run again on that commit, which
the implementer then pushes on its working branch and names in a pull request into
`main`, left unmerged until the owner's message says the review passed. The message to
the owner names the branch, the commit and the pull request, with those two results and
the pipeline's result on it.
