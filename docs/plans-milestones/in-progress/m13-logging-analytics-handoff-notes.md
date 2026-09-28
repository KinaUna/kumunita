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
