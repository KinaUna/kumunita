# M13 — Logging and usage analytics — handoff notes

> **The unit series' shared note file.** Each unit (U00–U07) appends its
> section here as it completes, recording: the exact shapes it shipped
> (or copied from the design doc verbatim), the pin names it implemented,
> the `Program.cs` line numbers it touched (if any), the drift it found
> (if any), and the gap it leaves for the next unit. The design doc
> (`docs/design/m13-logging-analytics-design.md`) is the authority; this
> file is the **delta log** — what each unit actually did, against that
> authority.

---

## U00 — Lock the design (2026-09-28)

**Role.** Sign-off gate for the whole M13 milestone. Authored the design
doc + ADR 0114 + the one ADR README index row + this handoff-notes
seed. Touched **only** the Deliverable files (no code, no tests, no
`Program.cs`).

**Deliverables landed (U00's scope):**

- `docs/design/m13-logging-analytics-design.md` — **authored**, all 27
  sections present (§capture, §surface-key, §middleware, §aggregation,
  §admin-surface, §kw-l, §retention, §sink, §pinned tests, §gate,
  invariants, FACES, §deferred lanes, §drift-guard, plus the design-doc
  template sections: Context / Goals-Non-goals / Human cost / Parts
  affected / Seams & contracts / Feedback loops / Emergent impact /
  Local-optimization check / FACES check / Rollout & rollback / Risks /
  Integration step served / World seams).
- `docs/adr/0114-logging-and-usage-analytics.md` — **authored** (Status:
  Accepted, Date: 2026-09-28, Context / Decision D1–D8 / Consequences /
  Alternatives-considered-and-rejected).
- `docs/adr/README.md` — **one index row** added after the 0113 row
  (line 119): the 0114 row, `Accepted` status.
- This file — **seeded** with the U00 section.

**Decisions locked (no vetoes; D1–D8 locked as proposed):**

- **D1 · Capture shape.** `UsageEvent { Id, At, ActorId, RouteTemplate }`
  + the pure `UsageCapturePolicy.Decide(UsageCaptureInput) →
  UsageCaptureDecision` in `Kumunita.Core.Usage` + one Web middleware
  (`UsageCaptureMiddleware`). **No** status code, no email, no body, no
  UA, no IP. Skip when no endpoint (true 404) or static file. The
  `ActorId` is the `Kumunita.Core.Identity.ClaimTypes.Subject` claim
  value, empty when anonymous. The `UsageCaptureInput` /
  `UsageCaptureDecision` POCO shapes are locked verbatim in §capture.
- **D2 · Surface-key normalization.** The pure `SurfaceKey.Map(string
  routeTemplate) → string` in Core — the top-level segment → surface-key
  map (a **closed list** of 26 top-level segments + the `"other"`
  fallback), locked verbatim in §surface-key. U02's `SurfaceKey` class
  + U04's `UsageAnalyticsService` `GroupBy` call copy this map exactly.
- **D3 · Aggregation seam.** `IUsageAnalyticsService` in Core:
  `GetWindowAsync(int windowDays)` → `UsageAnalyticsResult { WindowDays,
  Total, AuthenticatedTotal, AnonymousTotal, DistinctActors,
  SurfaceRanking }` + `GetCsvRowsAsync(int windowDays)` →
  `IReadOnlyList<UsageCsvRow>`. `windowDays` pin: 7/30/90; unknown →
  `ArgumentOutOfRangeException`. `SurfaceRanking` sorted by `Total`
  descending, ties `Surface` ascending. **`NewSignups` is dropped** —
  `User : IdentityUser` has no created-at column (only `ExternalId`);
  named deferral, own ADR. The POCO shapes are locked verbatim in
  §aggregation.
- **D4 · The admin surface.** `AdminAnalyticsController`
  (`[Authorize(Roles = Roles.GlobalAdmin)]`) + `Views/Admin/Analytics.cshtml`
  + `_AdminNav.cshtml` "Analytics" tab. Ctor: `(IDocumentStore store,
  IUsageAnalyticsService analytics)`. `GET /admin/analytics?window=30`
  (default) → `AnalyticsViewModel`. `GET /admin/analytics/export?window=30`
  → CSV (`text/csv; charset=utf-8` + `Content-Disposition: attachment;
  filename="kumunita-usage-{window}d.csv"` + `Cache-Control: no-store`) +
  **one `AccessAudit` row** (`TargetKind == "analytics"`, `Action ==
  "analytics.export"`, `ActorId` = the `ClaimTypes.Subject` value, `Via =
  AccessVia.Admin`, `Outcome = AccessOutcome.Allow`). The
  `AnalyticsViewModel` six fields + the ten `admin.analytics_*` `kw-l`
  keys × en/de/fr/da are locked verbatim in §admin-surface + §kw-l.
- **D5 · Retention tick.** `UsagePurgeService` (Core, static,
  `PurgeAsync(store, now, ct)` — the `AuditPurgeService` shape) +
  `UsagePurgeHandler` (Web, `AuditPurgeHandler` shape verbatim) +
  `UsagePurgeTick` (one `TimeoutMessage(TimeSpan.FromDays(1))` record).
  Retention constant: **365 days** (`UsagePurgeService.RetentionDays =
  365`), a **platform constant**, not a config knob. Locked verbatim in
  §retention.
- **D6 · File log sink.** **BCL-only** `FileLoggerProvider` / `FileLogger`
  / `LogLine` (JSON-lines writer) / `RollingFileSink` (`BuildFileName` +
  `Retain`) + `AddFileSink(LoggingBuilder, dir, retentionDays)` extension
  in `Kumunita.Core.Logging`. `FileSinkOptions` POCO (the
  `CommunityOptions` bind shape). The `LogLine.Write` escaping rules are
  locked verbatim in §sink: `\` → `\\`, `"` → `\"`, newline → `\n`, tab
  → `\t`; the `timestamp` field in ISO-8601 `"o"` format; the `level`
  field as the `LogLevel` enum name in **lowercase**; the `category`
  field verbatim (escaped); the `message` field escaped; the `exception`
  field as `exception?.ToString()` escaped, **omitted when null**.
- **D7 · 19 pinned tests.** The exact 19 test names are locked verbatim
  in §pinned tests. U01–U05 implement them verbatim (3 sink + 4 policy +
  2 surface-key + 3 middleware + 4 aggregation + 3 admin-surface).
- **D8 · The U07 docs flip.** M13 → `StatusDone`, M14 → `StatusNext`, in
  one unit (U07), `MilestonesTests` re-pinned to M14 (the C-M11·8
  precedent verbatim — the exact-order pin M0…M14 unchanged).

**Invariants locked (C-M13·1–7):**

- **C-M13·1 · The feedback is local.** M13 transmits **no** usage signal
  to any third party (SECURITY.md §5: "No third-party analytics, no
  telemetry"). Pinned by the D6 sink-shape pin + the D4 surface pin.
- **C-M13·2 · The row is minimal.** A `UsageEvent` row carries
  **exactly** `Id` / `At` / `ActorId` / `RouteTemplate` — no email, no
  profile field, no request body, no user-agent, no IP, no status code.
  Pinned by the `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip`
  reflection pin.
- **C-M13·3 · The surface renders no per-account data.**
  `/admin/analytics` renders **aggregates over the window** — never a
  row per `ActorId`, never an email, never a "resident X did Y" view.
  Pinned by the D4 surface pin + the (b) handoff acceptance test.
- **C-M13·4 · The template is the unit.** A recorded row's
  `RouteTemplate` is a **route template** (`GET /posts/{id}`), never a
  concrete path; a request with **no** recognized endpoint (a true 404,
  a static file, a malformed path) is **not recorded**. Pinned by the
  `Policy_Skips_No_Endpoint` / `Policy_Skips_StaticFile_Endpoint` /
  `Policy_Records_Template_Not_Concrete_Path` pins.
- **C-M13·5 · A capture never fails the request.** The middleware's
  `IDocumentStore.LightweightSession()` write is in a `try/catch` that
  **logs the exception and re-throws nothing**. Pinned by the
  `UsageCaptureMiddleware_Skips_When_Store_Throws` Web pin.
- **C-M13·6 · Zero new authorization surface.** M13 adds **no**
  `AccessAction` / `AccessVia` / `Decide()` branch /
  `IAuditableResource`. The `IAuthorizationService` frozen surface is
  unchanged.
- **C-M13·7 · Docs parity holds at the flip.** M13 → `StatusDone`, M14 →
  `StatusNext`, in one unit (U07), `MilestonesTests` re-pinned to M14.

**FACES locked (F1–F5):**

- **F1 · The operator sees the week.** A GlobalAdmin opens
  `/admin/analytics?window=7` and sees the `Total` request count, the
  authenticated/anonymous split, the `DistinctActors` count, and the
  per-surface ranking for the last 7 days.
- **F2 · The operator exports the 90 days.** A GlobalAdmin opens
  `/admin/analytics/export?window=90` and downloads a
  `kumunita-usage-90d.csv`; the `AccessAudit` table gains one
  `analytics.export` row.
- **F3 · The stranger sees nothing.** A resident without `GlobalAdmin`
  gets the sign-in challenge on `/admin/analytics`.
- **F4 · The capture is honest.** A `GET /posts/{id}` by a signed-in
  resident stores exactly one `UsageEvent` row; a `GET /no/such/route`
  stores **zero** rows.
- **F5 · The sink is boring.** The host's logs land in `app-YYYYMMDD.log`
  under the configured directory; a file older than the `RetentionDays`
  value is deleted at the next boot; the `docker logs` console sink is
  unchanged.

**ADR number confirmed free.** The `docs/adr/` folder contains `0001`
through `0113` (113 ADR files + `README.md`), and `docs/adr/README.md`'s
index ends at the `0113` row (line 119). `0114` is the next free number —
verified at U00 against the live tree.

**Drift found (source-driven confirmations; no register-vs-source
contradiction was found, so the register's D-item text is the locked
text):**

1. **The ADR number 0114 is confirmed free** (see above).
2. **The `Program.cs` middleware position is confirmed against the live
   tree.** `Program.cs` line 516 `app.UseRouting()`, line 525
   `app.UseAuthentication()`, line 534
   `app.UseMiddleware<PrivilegedStampMiddleware>()`, line 536
   `app.UseAuthorization()` — the D1 / §middleware "after
   `PrivilegedStampMiddleware`, before `UseAuthorization()`" position is
   the gap between lines 534 and 536 (U03's pin). The
   `ClaimTypes.Subject` claim shape is confirmed against
   `AdminController.AdminSubjectId` (line 107 —
   `user.FindFirst(Kumunita.Core.Identity.ClaimTypes.Subject)?.Value`).
   The `AccessAudit` POCO shape is confirmed against
   `src/Kumunita.Core/Authorization/AccessAudit.cs` (the `TargetKind` /
   `Action` / `ActorId` / `Via` / `Outcome` fields the D4 export row
   writes).

**Gap left for U01.** The design doc's §sink code sketches are the
authority; U01 implements the `Kumunita.Core.Logging` surface
(`FileSinkOptions` / `LogLine` / `RollingFileSink` / `FileLoggerProvider`
/ `FileLogger` / `AddFileSink`) + the `Program.cs` wiring (the
`AddFileSink` call next to the `AddLogging()` line, the
`Logging__File__Directory` / `Logging__File__RetentionDays` config read,
the `appsettings.json` `Logging:File` section) + the 3 D6 sink pins.

---

## U01 — RollingFileSink + AddFileSink (2026-09-28)

**Role.** D6 rendered as code — the `Kumunita.Core.Logging` surface
(BCL-only, zero new NuGet dependency), the `Program.cs` wiring, the
`appsettings.json` `Logging:File` section, and the 3 D6 sink pins.
Touched only the U01 deliverables (the `Logging/` folder, `Program.cs`,
`appsettings.json`, `LoggingTests.cs`). Did **not** touch the `Usage`
context, the middleware, the admin surface, or the retention tick
(U02–U05).

**Deliverables landed (U01's scope, 8):**

- `src/Kumunita.Core/Logging/FileSinkOptions.cs`
- `src/Kumunita.Core/Logging/LogLine.cs`
- `src/Kumunita.Core/Logging/RollingFileSink.cs`
- `src/Kumunita.Core/Logging/FileLoggerProvider.cs`
- `src/Kumunita.Core/Logging/FileLogger.cs`
- `src/Kumunita.Core/Logging/AddFileSink.cs`
- `tests/Kumunita.Core.Tests/LoggingTests.cs` (the 3 D6 sink pins)
- `src/Kumunita.Web/Program.cs` (one `AddFileSink` call) +
  `src/Kumunita.Web/appsettings.json` (one `Logging:File` section)

**(a) The `FileSinkOptions` fields as written:**

- `SectionName` — `const string = "Logging__File"`
- `Directory` — `string`, default `"logs"`
- `RetentionDays` — `int`, default `14`

(the `CommunityOptions` / `MediaOptions` bind shape — a `SectionName`
constant + two POCO properties, bound in `Program.cs` by the
`GetSection(FileSinkOptions.SectionName).Get<FileSinkOptions>()` read.)

**(b) The three D6 pin test names + pass/red:**

- `RollingFileSink_LogLine_Is_ValidJsonLines` — **pass**
- `RollingFileSink_FileNaming_Is_Daily` — **pass**
- `RollingFileSink_Retention_Deletes_Older_Files` — **pass**

Run via `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.LoggingTests"` →
`Total: 3, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`. (The repo's
`dotnet test` discovery path is broken — see AGENTS.md; the in-process
xunit.v3 runner is the reliable path.)

**(c) The `Program.cs` wiring:** line **53** (the
`builder.Logging.AddFileSink(...)` call; the config read is line **52**,
the `var fileSinkOptions = ...Get<FileSinkOptions>()` read, both between
`builder.Services.AddLogging()` at line 41 and the
`AddSingleton<ILogger>` bootstrap bridge at line 56). The sink is
**additive** — the console sink above stays; the file sink is a second
`ILoggerProvider` on the same `ILoggerFactory`.

**(d) The `appsettings.json` `Logging:File` section as written:**

```json
"Logging": {
  "LogLevel": {
    "Default": "Information",
    "Microsoft.AspNetCore": "Warning"
  },
  "File": {
    "Directory": "logs",
    "RetentionDays": 14
  }
},
```

**(e) Compile warnings:** **0** warnings, 0 errors on
`dotnet build Kumunita.slnx -c Debug`.

**Drift found (the design sketch's `LoggingBuilder` was an older-version
API; U01 reconciled to the .NET 10 public surface — the D6 intent is
unchanged, only the concrete type names moved):**

1. **`LoggingBuilder` is `internal` in .NET 10** — the public surface is
   `ILoggingBuilder`. The design doc's §sink sketch names
   `LoggingBuilder AddFileSink(this LoggingBuilder b, ...)`; U01's
   `AddFileSink.cs` uses `ILoggingBuilder` (the `ILoggerProvider` /
   `AddProvider` / `Retain` shape is byte-for-byte the D6 sketch). This is
   the "memory is an older major version" trap AGENTS.md flags — the
   concrete `LoggingBuilder` type became internal across the
   `Microsoft.Extensions.Logging` line; only the `ILoggingBuilder`
   interface is public.
2. **`ILogger.BeginScope` is the 1-arg form in .NET 10** — the design
   sketch's `BeginScope<TState>(TState state, Func<TState, string> factory)`
   no longer matches the `ILogger` interface (the 2-arg factory overload
   was dropped from the public interface). U01's `FileLogger` implements
   `IDisposable BeginScope<TState>(TState state) where TState : notnull`
   returning a no-op `NullScope.Instance` (the file sink carries no
   structured scope state; the `Log` formatter already receives the
   `TState`). The design sketch's `IDisposable?` return was also
   tightened to non-nullable to avoid a CS8603 nullability warning —
   the `NullScope` singleton keeps the contract honest.
3. **The wiring is `builder.Logging.AddFileSink(...)`, not
   `builder.Services.Logging.AddFileSink(...)`.** The design doc's §sink
   sketch names `builder.Services.Logging.AddFileSink(dir, days)`; the
   `WebApplicationBuilder`'s `Logging` property (an `ILoggingBuilder`) is
   the canonical, host-blessed surface (no need to reach into
   `services` for the builder). `builder.Services.Log` /
   `builder.Services.Logging` do not resolve on this .NET 10 surface
   (CS1061 — the extension property is not exposed on
   `IServiceCollection` in this closure), so `builder.Logging` is the
   correct idiom. Recorded here so U07's docs flip (the ADR / design-doc
   reference to the `Program.cs` wiring) names the line that actually
   ships (line 53), not the sketch's.
4. **The test's `SetLastWriteTimeUtc` takes a `DateTime`, not a
   `DateTimeOffset`.** The design sketch's retention pin plants files with
   "distinct mtimes"; the BCL `File.SetLastWriteTimeUtc` signature is
   `(string, DateTime)`. U01's test converts the `DateTimeOffset`
   `now`-offsets via `.UtcDateTime` when planting the mtimes — the
   `Retain` contract (the `File.GetLastWriteTimeUtc(f) < cutoff`
   comparison over `DateTimeOffset`) is unchanged.

**Gap left for U02.** The `Kumunita.Core.Usage` context
(`UsageEvent` POCO + the pure `UsageCapturePolicy.Decide`), the
`SurfaceKey.Map` closed-list normalization, and the D1/D2 pins. The
`sink` lane is complete — the host's logs land in
`app-YYYYMMDD.log` under the configured directory, a file older than
`RetentionDays` is deleted at boot, and the `docker logs` console sink is
unchanged (F5).

---

## U02 — Usage context (2026-09-29)

**Role.** D1 + D2 rendered as code — the `Kumunita.Core.Usage` bounded
context (`UsageEvent` POCO + the `UsageDocTypes` registration surface +
the pure `UsageCapturePolicy.Decide` + the pure `SurfaceKey.Map`
closed-list mapper), the `Program.cs` `UsageDocTypes.Configure(opts);`
wiring, and the 6 D1/D2 pins. BCL-only, zero new NuGet dependency.
Touched only the U02 deliverables (the `Usage/` folder, the two test
files, `Program.cs`). Did **not** touch the `Kumunita.Core.Logging`
sink (U01, done), the capture middleware, the `IUsageAnalyticsService`
aggregation seam, the admin surface, or the retention tick (U03–U05).

**Deliverables landed (U02's scope):**

- `src/Kumunita.Core/Usage/UsageEvent.cs`
- `src/Kumunita.Core/Usage/UsageDocTypes.cs`
- `src/Kumunita.Core/Usage/UsageCapturePolicy.cs` (the
  `UsageCaptureInput` / `UsageCaptureDecision` POCOs + the
  `UsageCapturePolicy.Decide` static, one file — the design doc
  §capture shape)
- `src/Kumunita.Core/Usage/SurfaceKey.cs`
- `tests/Kumunita.Core.Tests/UsageCapturePolicyTests.cs` (4 pins)
- `tests/Kumunita.Core.Tests/SurfaceKeyTests.cs` (2 pins)
- `src/Kumunita.Web/Program.cs` (one `UsageDocTypes.Configure(opts);`
  line)

**(a) The `UsageEvent` fields as written:**

- `Id` — `string`, `string.Empty` default (the M3 "string Id"
  convention — the conventional document identity; no non-default
  convention or business-key index pinned)
- `At` — `DateTimeOffset` (the capture instant, UTC — the middleware's
  request instant, set by the U03 capture, not the policy)
- `ActorId` — `string`, `string.Empty` default (the
  `ClaimTypes.Subject` value, `string.Empty` when anonymous — C-M13·2)
- `RouteTemplate` — `string`, `string.Empty` default (the **route
  template**, e.g. `GET /posts/{id}`, never a concrete path — C-M13·4)

**No** status code, **no** email, **no** request body, **no**
user-agent, **no** IP (C-M13·2 — the status code would leak the access
decision, which the `AccessAudit` lane already owns). Pinned by the
`UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` reflection pin.

**(b) The `UsageCapturePolicy` surface:**

- `UsageCaptureInput` (pure input POCO, `init`-only):
  `HasEndpoint` (`bool`), `IsStaticFile` (`bool`), `RouteTemplate`
  (`string?`), `ActorId` (`string`, `string.Empty` default) — the small
  projection the U03 middleware builds from the `HttpContext` (so the
  policy is testable without an `HttpContext`, D1).
- `UsageCaptureDecision` (pure output POCO, `init`-only):
  `Record` (`bool`), `RouteTemplate` (`string`, `string.Empty`
  default), `ActorId` (`string`, `string.Empty` default).
- `UsageCapturePolicy.Decide(UsageCaptureInput) → UsageCaptureDecision`
  — pure static, `ArgumentNullException.ThrowIfNull(input)`. The two
  skip rules (C-M13·4): `!HasEndpoint` ⇒ `Record = false` (a true 404,
  a malformed path); `IsStaticFile` ⇒ `Record = false` (a static file is
  noise, not a usage surface). Otherwise `Record = true` with
  `RouteTemplate = input.RouteTemplate ?? string.Empty` + `ActorId =
  input.ActorId` (the `ActorId`-empty-for-anonymous rule, C-M13·2).
  Mirrors the `EventReminderService` "Wolverine-free static class" house
  shape (the Web middleware is the thin host adapter).

**(c) The `SurfaceKey` closed list:** **26** top-level segments (each
mapping to itself — `posts` / `events` / `groups` / `admin` /
`messages` / `todos` / `boards` / `projects` / `search` / `about` /
`terms` / `help` / `privacy` / `conduct` / `language` / `settings` /
`account` / `my` / `pages` / `community` / `attachments` /
`content-image` / `notifications` / `calendar` / `whats-new` /
`health`) + the **`"other"`** fallback (one bucket, never a crash — an
unknown segment, a bare `/`, or a `null` input). `SurfaceKey.Map(string?
routeTemplate) → string` — pure static BCL-only string-map, strips the
`METHOD ` prefix, takes the first non-empty path segment, lower-cases,
and looks it up. **Byte-for-byte the design doc §surface-key sketch**
(the private field is named `Pinned` rather than `Map` to avoid a
CS0102 field/method name collision with the `Map` method — the D2 map
contents are unchanged). U04's `UsageAnalyticsService` `GroupBy` call
copies this same map (the D3 §aggregation `SurfaceRanking`
`.GroupBy(e => SurfaceKey.Map(e.RouteTemplate))` call).

**(d) The wiring line numbers:**

- `src/Kumunita.Web/Program.cs` **line 175** — the
  `UsageDocTypes.Configure(opts);` call, immediately after the
  `M9DocTypes.Configure(opts);` call (the last existing `*DocTypes`
  line, the house per-milestone "comment + one-line `Configure(opts)`"
  pattern). Inside the `builder.Services.AddMarten(opts => { … })`
  lambda (the single `*DocTypes.Configure(opts)` registration surface in
  this repo — the dev-only `ApplyAllDatabaseChangesOnStartup` loop and
  the `SchemaBootstrap` versioned boot both pick the surface up
  automatically through the host-registered `StoreOptions`).
- `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — **no line added**
  (see the drift note below).

**(e) The 6 pin test names + pass/red:**

- `Policy_Skips_No_Endpoint` — **pass**
- `Policy_Skips_StaticFile_Endpoint` — **pass**
- `Policy_Records_Template_Not_Concrete_Path` — **pass**
- `Policy_Anonymous_Record_Has_Empty_ActorId` — **pass**
- `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` — **pass**
- `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip` — **pass**

Run via `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\
Kumunita.Core.Tests.dll -class "Kumunita.Core.Tests.UsageCapturePolicyTests"`
→ `Total: 4, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`, and
`dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
-class "Kumunita.Core.Tests.SurfaceKeyTests"` → `Total: 2, Errors: 0,
Failed: 0, Skipped: 0, Not Run: 0`. (The repo's `dotnet test` discovery
path is broken — see AGENTS.md; the in-process xunit.v3 runner is the
reliable path.)

**(f) Compile warnings:** **0** warnings, 0 errors on
`dotnet build Kumunita.slnx -c Debug`.

**Drift found (the design sketch named a `SchemaBootstrap.cs` line that
has no `StoreOptions` surface; the source is the authority):**

1. **The `*DocTypes.Configure(opts)` block lives only in `Program.cs`.**
   The design doc §capture + the U02 plan both name "one line in
   `SchemaBootstrap.cs`" for the `UsageDocTypes.Configure(opts);` wiring,
   and §Context item 7 names "`SchemaBootstrap.cs` — the one-line
   `UsageDocTypes.Configure(opts);` additions U02 makes." **Verified
   against the live tree:** `SchemaBootstrap.cs` (and the whole
   `Bootstrap/` folder) has **no** `AddMarten` / `StoreOptions` /
   `.Configure(` / `Schema.For` surface — it resolves the already-
   configured `IDocumentStore` from DI (`sp.GetRequiredService
   <IDocumentStore>()`, line 36) and calls
   `store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync()`
   (line 55). The single `*DocTypes.Configure(opts)` registration
   surface in this repo is the `builder.Services.AddMarten(opts => { … })`
   lambda in `Program.cs` (lines 90–176), where every one of the
   `M1DocTypes` / `M3DocTypes` / `MediaDocTypes` / `PageDocTypes` /
   `TagDocTypes` / `M4DocTypes` / `M5DocTypes` / `M6DocTypes` /
   `M9DocTypes` calls lives. The `SchemaBootstrap` versioned boot picks
   the new surface up automatically through the host-registered
   `StoreOptions` (the M4/M5/M6/M9 precedent comments all say exactly
   this: "the SchemaBootstrap versioned boot both pick the surface up
   automatically"). So the wiring is in `Program.cs` line 175 — the
   `SchemaBootstrap.cs` "one line" in the design sketch is the same
   source-vs-sketch drift U01 flagged (`LoggingBuilder` → `ILoggingBuilder`)
   — the D1 intent (register `UsageEvent` on a parallel `*DocTypes`
   surface) is unchanged, only the concrete file the line lands in
   differs. Recorded here so U07's docs flip (the ADR / design-doc
   reference to the wiring) names the line that actually ships
   (`Program.cs:175`), not the sketch's.
2. **The test project's implicit usings are reduced** — `System.Linq`
   is **not** in `Kumunita.Core.Tests`'s implicit usings (the `LoggingTests`
   U01 file explicitly adds `using System.IO;`, and `SideEffectHarnessTests`
   / `UserInfoServiceTests` explicitly add `using System.Linq;`).
   U02's `SurfaceKeyTests` adds `using System.Linq;` explicitly (for
   `.Select` / `.ToHashSet`).
3. **xunit.v3's `Assert.Equal(expected, actual, stringMessage)` does
   not resolve to the 3-arg string-message overload** — it binds to the
   `IEnumerable<char>` overload (the 3rd arg becomes an
   `IEqualityComparer<char>`), a CS1503. U02's
   `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` uses a 2-arg
   `Assert.True(expected == actual, message)` per-iteration instead —
   the assertion is the same (each of the 26 pinned segments maps to
   its key, plus the `other` fallback), the message is preserved.
4. **`PropertyInfo` has no `IsStatic` property** (that's `MethodInfo` /
   `FieldInfo`) — the design's reflection pin sketch's
   `.Where(p => !p.IsStatic)` does not compile. `GetProperties
   (BindingFlags.Public | BindingFlags.Instance)` already excludes
   statics, so U02's `UsageEvent_Row_Has_No_Email_No_Body_No_Ua_No_Ip`
   drops the `.Where` filter and asserts the `HashSet` of the four
   property names equals `{ Id, At, ActorId, RouteTemplate }` (count 4)
   + the seven forbidden-name `Assert.DoesNotContain` checks
   (`Email` / `Body` / `UserAgent` / `Ua` / `Ip` / `Status` /
   `StatusCode`). The C-M13·2 boundary is pinned the same.
5. **A test-fixture bug (caught by the first run, not by the design):**
   U02's first `SurfaceKey_Maps_RouteTemplates_To_The_Pinned_Set` fixture
   used `GET /search?q=test` — the `?q=test` query string makes the
   first segment `search?q=test`, not `search`, so the verbatim
   `SurfaceKey.Map` (the authority) correctly returned `other`. The
   fixture was wrong (a real route template from `RoutePattern.RawText`
   never carries a query string), not the code. U02's fixture now uses
   `GET /search` (a clean template), and the pin passes against the
   unmodified verbatim `SurfaceKey.Map`.

**Gap left for U03.** The `Kumunita.Web.Middleware.UsageCaptureMiddleware`
(the D1 §middleware thin host adapter — the `RequestDelegate`
middleware that projects the `HttpContext` into the
`UsageCaptureInput`, calls `UsageCapturePolicy.Decide`, and on a
`Record` decision writes one `UsageEvent` via
`IDocumentStore.LightweightSession()` in its own `try/catch` —
C-M13·5 "a capture never fails the request"), its `Program.cs` pipeline
position (after `UseAuthentication()` line 546 + after
`PrivilegedStampMiddleware` line 555 + before `UseAuthorization()`
line 557 — i.e. **between `Program.cs` lines 555 and 557 in the live
tree after U01's `AddFileSink` wiring shifted everything — re-verify
against the live tree before inserting**), and the 3 D1 middleware pins
(`UsageCaptureMiddleware_Records_One_Usegevent_Per_Recognized_Request`,
`UsageCaptureMiddleware_Skips_True_404`,
`UsageCaptureMiddleware_Skips_When_Store_Throws`). The `Usage` context
surface is complete — the `UsageEvent` doc is registered on the
`UsageDocTypes` parallel surface (the `M3DocTypes` precedent), the
capture policy is pure and pinned, and the `SurfaceKey` closed list is
pinned verbatim (the U04 aggregation's `GroupBy` copies this same map).
