# 08 — Reference

Authoritative for every name, code, enumeration, status, configuration key and default,
alert condition, attempt reason, secret and metric. Another chapter that names one looks
it up here; a change to any of them is made here in the same change (`04` MSG-LIB-006).

---

## 1. Codes

A failure's code, the HTTP status an endpoint answers it with, and its `details`.
"(startup)" is a code that stops the start, "(result)" one a library call answers as a
failure and no endpoint answers, "(tool)" one `hermes migrate` writes (`06`
MSG-OPS-002).

| Code | Status | When | Details |
|---|---|---|---|
| `messaging.startup.declarationinvalid` | (startup) | A kind, category, template, page or protector `Id` is malformed, or two host protectors share one `Id` (`04` MSG-LIB-005; `01` MSG-SEND-001, MSG-SEND-002, MSG-SEND-018; `02` MSG-EMAIL-002, MSG-SMS-002, MSG-INAPP-001; `03` MSG-TPL-004) | `declaration` (for a protector, its `Id`), `field` (for a protector, `Id`) |
| `messaging.startup.declarationmissing` | (startup) | A required seam is not registered (`04` MSG-LIB-005) | `key` |
| `messaging.startup.configurationinvalid` | (startup) | A configuration value is outside its rule (`06` MSG-OPS-001; `03` MSG-TPL-003) | `key` |
| `messaging.startup.secretunavailable` | (startup) | A secret a used part needs, or a version still in use of a key a used part needs, is not answered (`06` MSG-OPS-008) | `key` |
| `messaging.startup.secretinvalid` | (startup) | A secret is outside its form, or a version's check value differs from the one recorded (`06` MSG-OPS-008) | `key` |
| `messaging.startup.schemamismatch` | (startup) | The database lacks a migration of the release (`06` MSG-OPS-002) | `migration` |
| `messaging.send.nodestination` | (result) | A channel the send names has no destination, or the send is left with no channel (`01` MSG-SEND-003; `02` MSG-INAPP-001) | `channel`, where there is one |
| `messaging.send.destinationinvalid` | (result) | A destination does not read (`01` MSG-SEND-003) | `channel` |
| `messaging.send.destinationnotallowed` | (result) | A phone number outside `Sms:AllowedPrefixes` (`02` MSG-SMS-002) | `channel` |
| `messaging.send.toolong` | (result) | A rendered message, or a value a render builds, exceeds its channel's limit (`02` MSG-EMAIL-003, MSG-SMS-001, MSG-PUSH-002, MSG-INAPP-002; `03` MSG-TPL-007) | `channel` |
| `messaging.send.renderlimit` | (result) | A render passes `Templates:MaxSteps` (`03` MSG-TPL-007) | `channel` |
| `messaging.send.renderfailed` | (result) | A filter cannot compute from the send's values (a division by zero, an overflow, or an argument it cannot take), or a member is missing from data the send passes, or an assigned name, a loop variable or `forloop` is read where it was not set (`03` MSG-TPL-002, MSG-TPL-007; `01` MSG-SEND-003) | `channel` |
| `messaging.send.idempotencyconflict` | (result) | An idempotency key reused with another send (`01` MSG-SEND-009) | |
| `messaging.send.unknown` | (result) | A send the library does not hold (`01` MSG-SEND-008, MSG-SEND-017) | |
| `messaging.key.unavailable` | (result) | A key version this process does not hold, or a content key past its use, is needed, or, for an export or a read of the in-app list, a value cannot open yet (`06` MSG-OPS-008; `01` MSG-SEND-003, MSG-SEND-006, MSG-SEND-012, MSG-SEND-020; `02` MSG-INAPP-003) | `key`: the secret, or the protector's `Id`; absent for a format kept for a later release |
| `messaging.release.newer` | (result) | An erasure or an export on a process whose database holds a migration its release does not know, so that a newer release's tables may hold the person's values (`01` MSG-SEND-020) | `migration` |
| `messaging.preference.unknown` | 404 | A category not declared, or a channel the category's kinds do not use (`01` MSG-SEND-010; `05` MSG-API-003) | `category`, `channel` |
| `messaging.preference.essential` | 422 | Setting a preference of an essential category, on or off (`01` MSG-SEND-010) | `category` |
| `messaging.preference.fixed` | 422 | Setting a preference of the in-app channel, on or off, where `InApp:AlwaysOn` is on (`01` MSG-SEND-010; `02` MSG-INAPP-004; `05` MSG-API-003) | `category`, `channel` |
| `messaging.inapp.cursorinvalid` | 400 | A cursor of the in-app list that does not read (`02` MSG-INAPP-003; `05` MSG-API-008) | |
| `messaging.inapp.unknown` | 404 | An in-app entry that does not exist or is another person's (`02` MSG-INAPP-003; `05` MSG-API-008) | |
| `messaging.suppression.essential` | (result) | A suppression for an essential category (`01` MSG-SEND-012) | `category` |
| `messaging.suppression.invalid` | (result) | A suppression naming a category not declared, a channel other than `email` and `sms`, or a destination that does not read (`01` MSG-SEND-012) | `member` |
| `messaging.push.keyunknown` | 422 | A subscription posted with a key whose VAPID version is retired, held or not, or that no version has (`02` MSG-PUSH-007; `05` MSG-API-004) | |
| `messaging.push.endpointnotallowed` | 422 | A subscription endpoint outside the allowed push services (`02` MSG-PUSH-004) | |
| `messaging.unsubscribe.invalid` | 404 | An unsubscribe token that does not verify (`05` MSG-API-005) | |
| `messaging.inbox.notfound` | 404 | A message the development inbox does not hold, or none that matches (`05` MSG-API-007) | |
| `messaging.inbox.notlocal` | 403 | A request to the development inbox from another machine or under another host name (`02` MSG-DEV-005) | |
| `messaging.request.malformed` | 400 | A body, a query parameter or a header that does not read (`05` MSG-API-001, MSG-API-007, MSG-API-008) | `member` |
| `messaging.request.unauthenticated` | 401 | No signed-in person where one is needed (`05` MSG-API-002) | |
| `messaging.request.toolarge` | 413 | A body over 16 KiB (`05` MSG-API-001, MSG-API-005, MSG-API-006) | |
| `messaging.request.mediatype` | 415 | A JSON route's body not sent as `application/json` (`05` MSG-API-001) | |
| `messaging.request.ratelimited` | 429 | A report source over `Sms:WrongKeyRate` (`05` MSG-API-006) | |
| `messaging.request.unavailable` | 503 | A key version in use that this process does not hold, a posted push key the key table records for such a version, a content key's use refused on the push subscription route, an unsubscribe token whose format byte is kept for a later release, an in-app entry's value that cannot open yet, or a person's locks not had within `Dispatch:Timeout` by the preference route, the in-app routes or a push registration, or a push registration's or an in-app read's protector call that throws or does not answer within it (`06` MSG-OPS-008; `01` MSG-SEND-010; `02` MSG-PUSH-003, MSG-PUSH-007, MSG-INAPP-003; `05` MSG-API-003, MSG-API-004, MSG-API-005, MSG-API-008); answered with `Retry-After` | |
| `messaging.migrate.inputinvalid` | (tool) | The tool's input does not read, or names a role the database does not have (`06` MSG-OPS-002) | `member` |
| `messaging.migrate.connectionfailed` | (tool) | The tool cannot connect (`06` MSG-OPS-002) | |
| `messaging.migrate.failed` | (tool) | A migration failed (`06` MSG-OPS-002) | `migration` |
| `messaging.migrate.grantfailed` | (tool) | A role's grants failed, or a role does not hold what was granted, after the migrations applied; no grant of the run stays (`06` MSG-OPS-002) | `role` |

---

## 2. Enumerations

| Enumeration | Values | C# type and members |
|---|---|---|
| Channels | `email` · `sms` · `push` · `inapp` (`02` section 5); designed for, not built: `whatsapp` (`02` MSG-CHAN-002) | `Channel`: `Email` · `Sms` · `Push` · `InApp` |
| Push urgencies | `very-low` · `low` · `normal` · `high` (RFC 8030 section 5.3) | `PushUrgency`: `VeryLow` · `Low` · `Normal` · `High` |
| Push services, from the endpoint's host | `fcm` (`fcm.googleapis.com`) · `mozilla` (`*.push.services.mozilla.com`) · `apple` (`*.push.apple.com`) · `windows` (`*.notify.windows.com`) · `other` (any other allowed host) | `PushService`: `Fcm` · `Mozilla` · `Apple` · `Windows` · `Other` |
| Push platforms | `webpush`; designed for, not built: `android`, `ios` (`02` MSG-PUSH-008) | `PushPlatform`: `WebPush` |
| SMS encodings | `gsm7` · `ucs2` (`02` MSG-SMS-001) | `SmsEncoding`: `Gsm7` · `Ucs2` |
| SMS report outcomes | `delivered` · `failed` · `notfinal` (`02` MSG-SMS-004) | `SmsReportOutcome`: `Delivered` · `Failed` · `NotFinal` |
| Attempt classes | `accepted` · `transient` · `permanent` · `gone` (`02` MSG-CHAN-001) | `AttemptClass`: `Accepted` · `Transient` · `Permanent` · `Gone` |
| Consent answers | `given` · `notgiven` · `unavailable` (`01` MSG-SEND-011) | `ConsentAnswer`: `Given` · `NotGiven` · `Unavailable` |
| Directions | `ltr` · `rtl` (`03` MSG-TPL-003) | A template value, not a C# type |
| Email security | `ImplicitTls` · `StartTls` (`02` MSG-EMAIL-001) | A configuration value, not a C# type |
| Trusted authority revocation | `published` · `none` (`02` MSG-EMAIL-001) | A configuration value, not a C# type |

---

## 3. Delivery statuses

A **final** status is one after which no attempt is made. Of the final statuses, `sent`
alone may move once more, by a provider's report or feedback, to `delivered` or
`failed`, or, for an in-app delivery, by its entry being marked read, to `read` (`01`
MSG-SEND-017).

| Status | `DeliveryStatus` member | Final | Meaning |
|---|---|---|---|
| `waiting` | `Waiting` | No | Recorded; not yet due, or waiting for its next attempt, quiet hours or a provider's rate |
| `inflight` | `InFlight` | No | Claimed by a dispatcher |
| `sent` | `Sent` | Yes, moving once by a report or, in-app, a read | Accepted by the provider, or, in-app, its entry written |
| `delivered` | `Delivered` | Yes | Reported delivered by the provider |
| `failed` | `Failed` | Yes | A permanent answer, the attempts used up, a number no longer within the allowed prefixes at dispatch, a run of holds for a key past `Dispatch:MaxHold`, or reported failed; its reason (section 6) says which |
| `cancelled` | `Cancelled` | Yes | Cancelled while waiting, its recipient erased, a value whose key was erased, or its kind retired (`01` MSG-SEND-001) |
| `expired` | `Expired` | Yes | Not sent within the kind's `ExpiresAfter` |
| `suppressed` | `Suppressed` | Yes | The destination is suppressed for the category |
| `optedout` | `OptedOut` | Yes | The person turned the category off on the channel |
| `noconsent` | `NoConsent` | Yes | The consent source answered that the recipient does not consent to the category's purpose |
| `limited` | `Limited` | Yes | A limit of the category is reached for the destination, the person or the deployment |
| `balancefloor` | `BalanceFloor` | Yes | An SMS of an optional category, checked while the balance is below the floor |
| `nosubscription` | `NoSubscription` | Yes | A push to a person with no live subscription, or whose every subscription is gone |
| `read` | `Read` | Yes | An in-app delivery whose entry the person marked read (`02` MSG-INAPP-003) |

---

## 4. Configuration

Every key is under the configuration section `Hermes`. A key marked "start only" takes
effect at the next start (`06` MSG-OPS-001). "Where email is used" means where the
channel is used as `04` MSG-LIB-003 defines it; so for SMS and push. The keys of the
engine, every key but those under `Email:`, `Sms:` and `Push:` and `Dispatch:Timeout`,
which the shipped providers read wherever they are used, are read and checked only where
`AddHermes` is registered (`04` MSG-LIB-003); the keys under `DevelopmentInbox:` only
where the development inbox acts (`02` MSG-DEV-001). A key holding a list takes its
default only where none of its items is configured. A number in a rule is a whole number
unless the rule says a decimal number, which is read in the invariant culture, `.` its
separator; a length in characters is counted as `00` section 1.5 gives.

| Key | Default | Rule |
|---|---|---|
| `PublicBaseAddress` | none; required where an optional category has a kind with email | The absolute `https` address of the mapped unsubscribe group, its prefix included |
| `Languages` | none; required where `AddHermes` is registered | One or more BCP 47 tags, each resolving to a predefined culture other than the invariant one (`03` MSG-TPL-003); start only |
| `DefaultLanguage` | none; required where `AddHermes` is registered | One of `Languages`; start only |
| `TimeZone` | `UTC` | An IANA time zone identifier; a system offering no time zone but UTC stops the start (`06` MSG-OPS-001) |
| `Templates:Path` | `templates` | A folder, a relative one taken from the host's content root (`IHostEnvironment.ContentRootPath`); a path naming nothing holds no template (`03` MSG-TPL-006), and one naming a file, or a folder the process cannot read, is outside the rule; start only |
| `Templates:MaxSteps` | `100000` | 1,000 to 10,000,000; start only |
| `Templates:MaxRecursion` | `20` | 1 to 100; start only |
| `Messages:Enabled` | `true` | Whether this application records sends; off, `SendAsync` throws and the application needs no `contentKeys` unless another part needs them (`04` MSG-LIB-003; `06` MSG-OPS-008); start only |
| `Dispatch:Enabled` | `true` | Whether this application dispatches: its shipped providers used, its observer told, its `observer` and `balance` jobs run, and `RunPassAsync` allowed (`01` MSG-SEND-007; `04` MSG-LIB-003); start only |
| `Dispatch:Hosted` | `true` | Where `Dispatch:Enabled` is on, whether the library runs the hosted dispatcher and its listening connection; off, passes run only through `RunPassAsync` (`01` MSG-SEND-007); start only |
| `Dispatch:Notify` | `true` | Whether a send wakes the dispatchers with `pg_notify` (`01` MSG-SEND-004); start only |
| `Dispatch:Interval` | `00:00:05` | 3 seconds to 5 minutes |
| `Dispatch:BatchSize` | `100` | 1 to 1,000 |
| `Dispatch:MaxPasses` | `4` | The passes of one channel a process's hosted dispatcher runs at once, 1 to 32 (`01` MSG-SEND-007); start only |
| `Dispatch:Timeout` | `00:00:20` | 1 second to 2 minutes |
| `Dispatch:ClaimTimeout` | `00:02:00` | At least twice `Dispatch:Timeout` plus 30 seconds, at most 1 hour |
| `Dispatch:LagThreshold` | `00:05:00` | Above `Dispatch:Interval`, at most 1 day |
| `Dispatch:MaxHold` | `7.00:00:00` | How long a run of holds for a key may last, from the first hold since the last counted attempt; a hold past it ends the delivery `failed` (`01` MSG-SEND-006); 1 hour to 90 days |
| `Observer:Enabled` | `false` | Whether notices are written for an `IDeliveryObserver` (`01` MSG-SEND-017); start only |
| `Observer:MaxCalls` | `4` | The notices of different deliveries the `observer` job delivers at once, 1 to 64 (`01` MSG-SEND-017); start only |
| `Observer:LagThreshold` | `00:15:00` | Above `Dispatch:Interval`, at most 1 day |
| `Retry:Initial` | `00:00:30` | 1 second to 1 hour |
| `Retry:Factor` | `2` | A decimal number, 1 to 10 |
| `Retry:Max` | `01:00:00` | At least `Retry:Initial`, at most 1 day |
| `Retry:Jitter` | `0.2` | A decimal number, 0 to 1, a fraction of the delay |
| `Retry:MaxAttempts` | `8` | 1 to 50 |
| `Idempotency:Window` | `1.00:00:00` | 1 minute to 30 days |
| `QuietHours:Start` | `21:00` | A time of day |
| `QuietHours:End` | `09:00` | A time of day; equal to `QuietHours:Start` turns quiet hours off |
| `QuietHours:Spread` | `00:30:00` | 0 to 4 hours |
| `Retention:Content` | `30.00:00:00` | 1 day to 3 years |
| `Retention:Record` | `365.00:00:00` | At least `Retention:Content`, at most 10 years |
| `Retention:Places` | `31.00:00:00` | 1 day to 1 year; a limit's window above it fails its category's declaration (`01` MSG-SEND-002); start only |
| `Retention:Suppression:Email` | none: indefinitely | None, or 30 days to 10 years |
| `Retention:Suppression:Sms` | none: indefinitely | None, or 30 days to 10 years |
| `Retention:InApp` | `180.00:00:00` | 1 day to 3 years, from the instant an in-app entry was written (`02` MSG-INAPP-005) |
| `Jobs:Enabled` | `true` | Start only |
| `Jobs:RetentionAt` | `03:00` | A time of day, in `TimeZone` |
| `Jobs:SubscriptionsInterval` | `01:00:00` | 1 minute to 1 day |
| `Jobs:BatchSize` | `1000` | 100 to 100,000 |
| `Jobs:StallFactor` | `3` | A decimal number, 2 to 100 |
| `Jobs:ClaimTimeout` | `00:10:00` | At least `Dispatch:ClaimTimeout`, at most 1 day |
| `Database:Retries` | `3` | How many times the library runs again one of its own transactions that PostgreSQL aborts for a deadlock (40P01) or a serialization failure (40001), 0 to 10 (`07` MSG-ENG-005) |
| `Alerts:Repeat` | `01:00:00` | 5 minutes to 1 day |
| `Unsubscribe:TokenLifetime` | `730.00:00:00` | 180 days to 10 years |
| `Secrets:RetireAfter` | `7.00:00:00` | How long after values were last made under a key version the retention job may mark it retired (`06` MSG-OPS-008); 1 day to 1 year |
| `Secrets:ContentKeys:WriteVersion` | the one version held; required where more than one is held | A version held, checked only where the key is needed (`06` MSG-OPS-008); start only |
| `Secrets:ContentKeys:MaxValues` | `2147483648` | 1 to 4,294,967,296 |
| `Secrets:ContentKeys:ReserveBlock` | `10000` | 100 to 1,000,000 |
| `Secrets:HashKeys:WriteVersion` | the one version held; required where more than one is held | A version held, checked only where the key is needed (`06` MSG-OPS-008); start only |
| `Secrets:TokenKeys:WriteVersion` | the one version held; required where more than one is held | A version held, checked only where the key is needed (`06` MSG-OPS-008); start only |
| `Secrets:SmsReportKey:WriteVersion` | the one version held; required where more than one is held | A version held, checked only where the key is needed (`06` MSG-OPS-008); start only |
| `Secrets:PushVapidKeys:WriteVersion` | the one version held; required where more than one is held | A version held, checked only where the key is needed (`06` MSG-OPS-008); start only |
| `Email:Host` | none; required where the shipped email provider is used | A host name; start only |
| `Email:Port` | `465` with `ImplicitTls`, `587` with `StartTls` | 1 to 65535; start only |
| `Email:Security` | `ImplicitTls` | `ImplicitTls` or `StartTls`; start only |
| `Email:TrustedAuthority` | none: the system's authorities | One PEM certificate; start only |
| `Email:TrustedAuthorityRevocation` | `published` | `published` or `none` (`02` MSG-EMAIL-001); start only |
| `Email:ShortLivedMaxValidity` | `7.00:00:00` | 1 to 10 days |
| `Email:From:Address` | none; required where email is used | An address (`02` MSG-EMAIL-002) |
| `Email:From:DisplayName` | none | 1 to 128 characters |
| `Email:Rate` | `10` | Deliveries per second across the deployment, 1 to 1,000 |
| `Email:MaxBytes` | `1048576` | 10 KiB to 25 MiB |
| `Sms:Sender` | none; required where SMS is used | A sender (`02` MSG-SMS-002) |
| `Sms:AllowedPrefixes` | none; required where SMS is used | One or more of `+` followed by 0 to 6 digits; `+` alone allows every number |
| `Sms:Rate` | `5` | Deliveries per second across the deployment, 1 to 1,000 |
| `Sms:ReportBaseAddress` | none; where set, the engine fills each SMS's `ReportAddress` from it, for a provider whose gateway takes a report address with each message (`02` MSG-SMS-004) | The absolute address of the mapped report group, its prefix included |
| `Sms:WrongKeyRate` | `60` | Requests with a wrong key a minute per source address, per process, 1 to 10,000 |
| `Sms:ReportRejectThreshold` | `20` | 1 to 10,000 |
| `Sms:ReportRejectWindow` | `01:00:00` | 1 minute to 1 day |
| `Sms:ReportHold` | `00:10:00` | 1 minute to 1 day |
| `Sms:ReportHoldMax` | `1000` | Reports held at once across the deployment (`02` MSG-SMS-004), 10 to 1,000,000 |
| `Sms:BalancePoll` | `00:15:00` | 1 minute to 1 day |
| `Sms:BalanceFloor` | `0` | A decimal number, zero or more, in the provider's unit |
| `Sms:FloorWarning` | `1.00:00:00` | 1 hour to 30 days |
| `Sms:DrainWindow` | `01:00:00` | 5 minutes to 1 day |
| `Sms:DrainFactor` | `3` | A decimal number, 1.5 to 100 |
| `Sms:DrainBaseline` | `7.00:00:00` | At least twice `Sms:DrainWindow`, at most 90 days |
| `Sms:DrainMinHistory` | `1.00:00:00` | At least `Sms:DrainWindow`, at most `Sms:DrainBaseline` |
| `Sms:DrainMinSpend` | `10` | A decimal number, zero or more, in the provider's unit |
| `Sms:BalanceUnreadableAfter` | `01:00:00` | 5 minutes to 1 day |
| `Push:Subject` | none; required where the shipped push provider is used | A `mailto:` or `https:` URI |
| `Push:AllowedHosts` | `fcm.googleapis.com`, `*.push.services.mozilla.com`, `*.push.apple.com`, `*.notify.windows.com` | Host names; a leading `*.` matches any subdomain and covers at least three labels after its `*.` |
| `Push:MaxSubscriptions` | `10` | Per person, 1 to 100 |
| `Push:Rate` | `50` | Deliveries per second across the deployment, 1 to 1,000 |
| `Push:IdleRetention` | `180.00:00:00` | 7 days to 3 years |
| `Push:AuthFailThreshold` | `20` | 1 to 10,000 |
| `Push:AuthFailWindow` | `01:00:00` | 1 minute to 1 day |
| `InApp:AlwaysOn` | `false` | Whether the in-app channel of every optional category cannot be turned off (`02` MSG-INAPP-004), set alike in every application of the deployment |
| `DevelopmentInbox:Folder` | `hermes-inbox` in the operating system's temporary folder | An absolute path (`02` MSG-DEV-002); start only |
| `DevelopmentInbox:KeepFor` | `7.00:00:00` | 1 hour to 90 days; start only |
| `DevelopmentInbox:MaxMessages` | `1000` | 10 to 100,000; start only |
| `DevelopmentInbox:AllowRemote` | `false` | Whether the inbox's group answers requests from other machines and under other host names (`02` MSG-DEV-005); start only |

---

## 5. Alert conditions

An alert's state is kept per condition and scope (`06` MSG-OPS-006). In `details`, an
instant is written in ISO 8601 in UTC, a duration in whole seconds, and an amount as a
decimal number with `.` as its separator.

| Condition | Scope | Raised when | Clears when | Details |
|---|---|---|---|---|
| `email-submission-failing` | none | An attempt fails on TLS, the certificate, authentication (a 530 included), `MAIL FROM`, or the session itself (`serverrefused`) (`02` MSG-EMAIL-001, MSG-EMAIL-004) | An email is accepted | `reason` |
| `sms-balance` | none | A drain, or a balance that would reach the floor within `Sms:FloorWarning` (`02` MSG-SMS-006) | A reading at which neither holds | `which` (`drain` or `low`), `balance` |
| `sms-balance-unreadable` | none | The balance has not been read for `Sms:BalanceUnreadableAfter` (`02` MSG-SMS-006) | A balance is read | `since` |
| `sms-report-rejected` | none | More rejected reports within `Sms:ReportRejectWindow` than `Sms:ReportRejectThreshold` (`02` MSG-SMS-004) | The count within the window is at or below the threshold | `count` |
| `push-auth-failing` | The push service | More answers of 400, 401 or 403 from the push service within `Push:AuthFailWindow` than `Push:AuthFailThreshold` (`02` MSG-PUSH-005) | The count within the window is at or below the threshold | `service`, `count` |
| `dispatch-lag` | The channel | The oldest delivery `waiting` or `inflight` whose became-due instant has passed became due longer ago than `Dispatch:LagThreshold` (`06` MSG-OPS-005; `01` MSG-SEND-007) | It is not | `channel`, `age` |
| `job-stalled` | The job | A job a process has started (its row records the first start) has neither completed nor renewed its claim for `Jobs:StallFactor` of its intervals plus `Dispatch:Timeout` (`06` MSG-OPS-004) | It completes or renews its claim | `job` |
| `newer-release` | none | A process whose database holds a migration its release does not know writes on the alert's row the instant it holds until, now plus twice its `Dispatch:Interval` where later than the one recorded, at most once per that interval per process (`06` MSG-OPS-006, MSG-OPS-007, MSG-OPS-008) | A run of the watch job finds that instant passed, and clears it by an update conditional on the instant it read | `migration`, the first it does not know |
| `configuration-invalid` | The key | A value reloaded while running is outside its rule (`06` MSG-OPS-001) | The key holds a valid value again | `key` |
| `content-key-limit` | The version | A `contentKeys` version used for more than `Secrets:ContentKeys:MaxValues` values is a process's write version, the process writing on the alert's row, on its key-table timer, the instant it holds until, now plus twice its `Dispatch:Interval` where later than the one recorded (`06` MSG-OPS-006, MSG-OPS-008) | A run of the watch job finds that instant passed, and clears it by an update conditional on the instant it read | `version`, `count` |
| `key-unavailable` | The secret, or the protector's `Id`; none for a value in a format kept for a later release | A process holds a delivery for a key, or answers a request or call as key unavailable (save an unsubscribe token whose format byte is kept for a later release, `05` MSG-API-005), each such answer writing on the alert's row the instant it holds until, now plus twice the process's `Dispatch:Interval` where later than the one recorded, at most once per that interval per process (`06` MSG-OPS-006, MSG-OPS-008) | A run of the watch job finds that instant passed, and clears it by an update conditional on the instant it read | `version`, where there is one |
| `content-rejected` | The table, as the context names it (`deliveries` or `subscriptions`) | A protected value does not open under its binding, each such value writing on the alert's row the instant it holds until, now plus twice the process's `Dispatch:Interval` where later than the one recorded, at most once per that interval per process (`06` MSG-OPS-006; `01` MSG-SEND-018) | A run of the watch job finds that instant passed, and clears it by an update conditional on the instant it read | `table` |
| `observer-failing` | none | The oldest pending notice is older than `Observer:LagThreshold` (`01` MSG-SEND-017) | It is not | `age` |

---

## 6. Attempt reasons

| Reason | `AttemptReason` member | Meaning |
|---|---|---|
| `timeout` | `Timeout` | No answer within `Dispatch:Timeout` |
| `connectionfailed` | `ConnectionFailed` | The connection was refused or broke, or a reply did not read |
| `tlsfailed` | `TlsFailed` | TLS was not completed, or the certificate did not validate (`02` MSG-EMAIL-001) |
| `authfailed` | `AuthFailed` | The provider refused the credential, or offered no way to give it (SMTP 535, no mechanism; HTTP 401 or 403) |
| `senderrefused` | `SenderRefused` | The provider refused the sender (SMTP 5xx to `MAIL FROM`; an SMS sender name) |
| `recipientrefused` | `RecipientRefused` | The provider refused the destination (SMTP 5xx to `RCPT TO`; an SMS number) |
| `messagerefused` | `MessageRefused` | The provider refused the message (SMTP 5xx to the data; push 413; an SMS text) |
| `badrequest` | `BadRequest` | A push service answered 400, or a status no other row names (`02` MSG-PUSH-005) |
| `ratelimited` | `RateLimited` | The provider answered 429 |
| `temporary` | `Temporary` | The provider's temporary answer (SMTP 4xx, HTTP 5xx, an SMS provider's temporary class) |
| `permanent` | `Permanent` | An SMS provider's final answer with no more specific reason |
| `gone` | `Gone` | The push subscription no longer exists (404, 410) |
| `providerfault` | `ProviderFault` | The provider threw (`02` MSG-CHAN-001) |
| `keyunavailable` | `KeyUnavailable` | A value could not be opened, a lookup made, or a request signed under a key version this process does not hold, a protector it does not register, or an envelope format kept for a later release: the delivery is held, no attempt counted, and ends `failed` with this reason where a run of holds passes `Dispatch:MaxHold` (`01` MSG-SEND-006, MSG-SEND-018) |
| `consentunavailable` | `ConsentUnavailable` | The consent source answered `unavailable`, did not answer in time, or threw (`01` MSG-SEND-011) |
| `reportedfailed` | `ReportedFailed` | A provider's report showed the message failed (`02` MSG-SMS-004) |
| `claimlost` | `ClaimLost` | The holder of the attempt stopped before recording its answer (`01` MSG-SEND-007) |
| `serverrefused` | `ServerRefused` | The SMTP server refused the session (a 5xx to the greeting, `EHLO` or `STARTTLS`) (`02` MSG-EMAIL-004) |
| `destinationnotallowed` | `DestinationNotAllowed` | The number is no longer within `Sms:AllowedPrefixes` when the delivery is checked (`02` MSG-SMS-002) |
| `valuerejected` | `ValueRejected` | A protected value did not open under its binding, or its envelope's format is neither `0x01` nor one kept for a later release (`01` MSG-SEND-018) |

A delivery that ends `failed` after its last attempt carries that attempt's reason.

---

## 7. Secrets

`connection` · `listenConnection` · `contentKeys` · `hashKeys` · `tokenKeys` ·
`smsReportKey` · `emailSubmission` · `pushVapidKeys`. Their forms and uses are `06`
MSG-OPS-008.

---

## 8. Metrics and other names

The meter is `Hermes`, with these instruments:

| Instrument | Kind | Unit | Tags | Measures |
|---|---|---|---|---|
| `hermes.deliveries` | Counter | `{delivery}` | `channel`, `status` | Each final status a delivery reaches, and each move from `sent` |
| `hermes.attempts` | Counter | `{attempt}` | `channel`, `class`, `reason` | Each attempt, by its class and reason, an attempt answered at a check included |
| `hermes.dispatch.oldest_due_age` | Gauge | `s` | `channel` | How long ago the oldest delivery `waiting` or `inflight` whose became-due instant has passed became due, a synchronous `Gauge<double>` the watch job records each time it judges `dispatch-lag`, 0 where no such delivery is, only the process whose run judged it recording it, so the latest point across processes is the reading (`06` MSG-OPS-005; `01` MSG-SEND-007) |
| `hermes.provider.duration` | Histogram | `s` | `channel`, `class` | Each provider call's duration |

These names carry the library's name (`00`): the meter and its instruments, the solution
file `Hermes.sln` (`07` MSG-ENG-001), the health check `hermes.dispatch` (`06`
MSG-OPS-005), the named HTTP client `Hermes.Push` (`02` MSG-CHAN-001), the configuration
section, the command `hermes`, and the development inbox's default folder `hermes-inbox`
and header `X-Hermes-Inbox` (`02` MSG-DEV-002, MSG-DEV-005).

These names are stored, or shared by the processes of two releases, and never change:

| Name | What it names |
|---|---|
| `messaging` | The schema (`06` MSG-OPS-002) and the library protector's `Id` (`01` MSG-SEND-018) |
| `messaging.__migrations_history` | The migration history table (`06` MSG-OPS-002) |
| `messaging_dispatch` | The notification channel a send wakes the dispatchers on (`01` MSG-SEND-004) |
| `deliveries`, `subscriptions`, `entries`; `destination`, `unsubscribe`, the part names of `03` MSG-TPL-001, an in-app part's name and language tag joined by `.`, `target`, `endpoint`, `p256dh`, `auth` | The tables and fields a protected value's context names, the layout of that context (`01` MSG-SEND-018) |
| `email`, `sms`, `push-endpoint`, `push-topic`, `person`, `idempotency` | The keyed hash's domains (`06` MSG-OPS-008) |
| `sms-report`, `idempotency` | The texts that begin the unkeyed lock keys (`01` MSG-SEND-009; `02` MSG-SMS-004) |
| `sms-report-hold` | The text of the lock the held reports are counted under (`02` MSG-SMS-004) |
| `messaging-key-check` | The text a key version's check value is made from (`06` MSG-OPS-008) |
| `dispatch`, `observer`, `balance`, `retention`, `subscriptions`, `watch` | The jobs (`06` MSG-OPS-004); all but `dispatch` have a row in the job table and are what `job-stalled` names |
| `unsubscribe`, `unsubscribed`, `unsubscribeinvalid` | The library's pages (`03` MSG-TPL-006) |
