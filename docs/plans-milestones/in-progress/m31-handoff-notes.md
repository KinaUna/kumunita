# M31 — Production error handling — rolling handoff note

> **Milestone open (U00).** This is the **scratch tier** (rolling handoff
> note) of M31's three-tier contract. One `##` section per unit, appended
> (never rewritten), in order (U00, U01, … U07). Each unit writes exactly one
> short section before it exits; the next unit reads **only** that section +
> its own entry-reads list.
>
> - **Register** — `docs/plans-milestones/plan-m31-production-error-handling.md`
> - **Design doc (primary)** — `docs/design/m31-production-error-handling-design.md`
>   (U01/U02 author)
> - **ADR** — **ADR 0154** (the next free number after 0153 — the ADR index in
>   `docs/adr/README.md` confirms 0153 is the current highest: 0153 = M30
>   `Accepted — **Done** (M30)`, 0152 = M29, 0151 = M28, 0150 = SITE. **0154
>   is free**; U00 verified this at kickoff.)
> - **Scope** — the `ErrorReport` **non-singleton** doc (one row per report,
>   the `Posts/Report` / `Usage/UsageEvent` row-per-event shape) in a **new
>   bounded context** `Kumunita.Core.ErrorReports` (M31·1) + the
>   `IErrorReportService` read + two audited-write seams (M31·5 / M31·6) +
>   the `ErrorReportDocTypes` registration surface (the ADR 0004 §B.1
>   parallel-surface pattern, M31·3) + the **enhanced** `/Home/Error` 500
>   page (the report form + the post-submission confirmation — the form is
>   **public**, anonymous-safe, non-blocking; M31·2) + the **`POST
>   /Home/Error/Report`** submission action (M31·5) + the
>   **`/admin/error-reports`** GlobalAdmin surface (the read-only list + the
>   `POST /admin/error-reports/{id}/triage` mark-as-triaged action — the
>   `[Authorize(Roles = GlobalAdmin)]` standard admin gate, the thin-token
>   rule ADR 0001-B; M31·4 / M31·9) + the **closed `errorreport.*` `kw-l`
>   key set** (~20 keys) × en/de/fr/da (M31·7).
> - **Out of scope (named deferrals to M32)** — the general issue-submission
>   lane (a resident files an issue *not tied to an error page*) · the local
>   resolution lane (the admin marks a report `resolved` with a resolution
>   note) · the escalation forwarding lane (forwarding a report to a
>   configurable endpoint, an env-var URL) · the `resolved` triage status
>   (M31's `TriageStatus` value set is **closed** to `{"new", "triaged"}`;
>   M32 **adds** `"resolved"` additively per ADR 0004 §B.1, it does not
>   re-shape the existing two) · the per-report detail view with a resolution
>   note · and the `Milestones.cs` / README / `MilestonesTests` trio until
>   the milestone *ships* (U07 owns the six-member close flip).
> - **Frozen base (unchanged)** — the `Posts/Report` doc
>   (`src/Kumunita.Core/Posts/Report.cs`, the M3b content-moderation report,
>   ADR 0023) is **untouched** (M31·10 — M31 does not add fields to it, does
>   not reuse it, does not change its `M3DocTypes` registration); the
>   `Usage/UsageEvent` row-per-event doc shape (the POCO + the conventional
>   string `Id`) is the registration shape M31 mirrors (not a field source —
>   the `ErrorReport` field set is the 11-member ceiling in the register's
>   Assumptions, pinned by the design doc §2.2 in U02); the ADR 0004 §B.1
>   parallel-surface pattern (the `ErrorReportDocTypes.Configure(opts)`
>   shape, the `M3DocTypes` / `M4DocTypes` / `M6DocTypes` / `UsageDocTypes`
>   precedent — `UsageDocTypes` is the closest analog, a row-per-event
>   surface) is the registration shape; the ADR 0015 D1 `kw-l` provider-floor
>   discipline (the `en` value is the source text); the ADR 0001-B thin-token
>   rule (the admin surface is `[Authorize(Roles = GlobalAdmin)]`); the
>   ADR 0006 C3 single-write-lane shape (one write session storing the
>   `ErrorReport` doc + exactly one `AccessAudit` doc in the same session).
> - **New invariants (locked in ADR 0154, U00):** M31·1 platform-error
>   signal, separate from content-moderation · M31·2 report form is on the
>   500 page, always available, non-blocking · M31·3 `ErrorReport` doc is
>   Marten-native, new parallel surface · M31·4 admin list is a
>   GlobalAdmin-gated read (no per-row `IAuthorizationService`, no audit on
>   reads) · M31·5 report submission is a single-write-lane (one
>   `AccessAudit` row; the `Via` tag is U01/U02's §2.1 pin) · M31·6 triage
>   is a GlobalAdmin-gated idempotent single-write-lane (`Via = Admin`;
>   idempotent — re-stamping an already-`triaged` row is a no-op) · M31·7
>   closed `errorreport.*` `kw-l` key set parity-pinned ×4 · M31·8 the
>   six-member close flip is U07's responsibility · M31·9 no new
>   authorization surface (no new `AccessAction`, no new `Decide()` branch,
>   no new `IAuthorizationService` seam; the error page is public — the
>   admin surface is `[Authorize(Roles = GlobalAdmin)]`) · M31·10 the
>   existing `Posts/Report` doc is **untouched**.

<!-- U00 appends its section below this line. One ## section per unit, in order
     (U00, U01, … U07). Never rewrite a prior section. -->

## U00 — Kickoff verified

All 8 entry reads completed; **no drift** against the register. Facts the
next unit (U01 — design doc Part 1) needs:

- **The frozen base M31 enhances (the current 500 error page):**
  - `Error()` action — `src/Kumunita.Web/Controllers/HomeController.cs`
    **line 153** (`public IActionResult Error()`), returning
    `View(new ErrorViewModel { RequestId = Activity.Current?.Id ??
    HttpContext.TraceIdentifier })`. U04 enhances this to read the exception
    from `IExceptionHandlerFeature` (per the register's U04 deliverable).
  - `ErrorViewModel` — `src/Kumunita.Web/Models/ErrorViewModel.cs`, **8
    lines**, **2 members**: `RequestId` (`string?`) + the computed
    `ShowRequestId` (`bool`, `!string.IsNullOrEmpty(RequestId)`). U04 adds 5
    more fields (`ExceptionType`, `ExceptionMessage`, `FormSubmitted`,
    `FormDescription`, `FormContactEmail`).
  - `Error.cshtml` — `src/Kumunita.Web/Views/Shared/Error.cshtml`, **19
    lines**. Current markup: the `error.title` / `error.subtitle` `kw-l` keys
    (both `text-danger`), the conditional `RequestId` block (gated by
    `Model.ShowRequestId`), the `error.development_title` /
    `error.development_hint` `kw-l` keys. U04 appends the report form + the
    post-submission confirmation **below** the existing content.
  - `UseExceptionHandler("/Home/Error")` — `src/Kumunita.Web/Program.cs`
    **line 612**, inside the `if (!app.Environment.IsDevelopment())` block
    (the production-only path; the handler target is the `HomeController`
    `Error` action, **not** a middleware — confirmed per the register's
    U00 entry-read #5).
- **The `Posts/Report` doc boundary (M31·10 — untouched):**
  - File: `src/Kumunita.Core/Posts/Report.cs` — namespace
    `Kumunita.Core.Posts`, the M3b **content-moderation** report (ADR 0023
    is the reply-report-target lane; the doc's doc-comment says "dormant
    report row (M3b workflow) … the table is registered in M3 for forward
    compatibility").
  - 8 POCO fields: `Id`, `PostId`, `ReporterId`, `ComponentId?`, `Reason?`,
    `Status?` (nullable until M3b's write lane sets it), `At`
    (`DateTimeOffset`), `ReplyId?` (the ADR 0023 target discriminator).
  - **Untouched flag:** M31 does **not** add fields to this doc, does **not**
    reuse it, does **not** alter its `M3DocTypes` registration, does **not**
    touch the M3b moderation queue. The `TargetKind` strings are distinct:
    M31's `ErrorReport` audit row uses `"error-report"`; the existing
    `Posts/Report` doc uses `"post"` / `"reply"` (per the register's M31·10
    pin). The two report concepts are **separate docs in separate contexts**
    (`Kumunita.Core.ErrorReports` is **new** in M31 — the folder does not
    exist yet; `Kumunita.Core.Posts` is the existing context the M3b doc
    lives in).
- **The row-per-event doc shape M31 mirrors (not a field source):**
  - `src/Kumunita.Core/Usage/UsageEvent.cs` — namespace
    `Kumunita.Core.Usage`, 4-field POCO (`Id` `string`, `At`
    `DateTimeOffset`, `ActorId` `string`, `RouteTemplate` `string`). The
    `Id` is the conventional string `Id` (Marten generates it) — the same
    shape the register's `ErrorReport` doc uses (`Id` is the first of the
    11-member ceiling). The register's Assumptions pin the **11-member**
    `ErrorReport` field set (the `UsageEvent` 4-field set is **not** the
    field source — the register's Assumptions §"The `ErrorReport` doc shape"
    is the authority, pinned by the design doc §2.2 in U02).
- **The ADR 0004 §B.1 parallel-surface precedent (the `ErrorReportDocTypes`
  registration shape):**
  - The `*DocTypes.Configure(opts)` block in `src/Kumunita.Web/Program.cs`
    spans lines **107–243** (the `M1DocTypes` at 107 through the
    `StorageSettingsDocTypes` at 243). `UsageDocTypes` is at **line 225** —
    the closest analog to M31's `ErrorReportDocTypes` (both are row-per-event
    surfaces). U03 adds the `ErrorReportDocTypes.Configure(opts);` call next
    to this block (one line added, per the register's U03 deliverable).
  - The `*DocTypes` surfaces registered in `Program.cs` (the precedent list
    the register names, all confirmed present): `M1DocTypes` (107) ·
    `M3DocTypes` (113) · `MediaDocTypes` (119) · `PageDocTypes` (125) ·
    `SiteContentDocTypes` (134) · `SurfaceLabelsDocTypes` (143) ·
    `AdminOnboardingDocTypes` (152) · `TagDocTypes` (158) · `M4DocTypes`
    (167) · `M5DocTypes` (176) · `M6DocTypes` (185) · `M9DocTypes` (195) ·
    `M16DocTypes` (207) · `M17DocTypes` (217) · `UsageDocTypes` (225) ·
    `DocumentDocTypes` (235) · `StorageSettingsDocTypes` (243). The register's
    U00 entry-read #5 and U03 entry-read #6 both name the `M3DocTypes` /
    `M4DocTypes` / `M6DocTypes` precedent; all three are present.
- **The ADR index (`docs/adr/README.md`) — 0154 is free:**
  - 0150 — SITE (Site content customization) — `Accepted`
  - 0151 — M28 (Guardian time limits) — `Accepted`
  - 0152 — M29 (Admin surface labels) — `Accepted`
  - 0153 — M30 (Admin onboarding) — `Accepted — **Done** (M30)`
  - **0154 — FREE** (no row in the index; this is M31's ADR number, per the
    register). The register's U00 entry-read #8 confirms "0153 = M30, 0152 =
    M29, 0151 = M28" — all three verified present with the expected labels.
- **The precedent ADR list (for the U01 design doc + the U02 ADR 0154
  draft):**
  - **ADR 0004 §B.1** — Marten-native, parallel-surface pattern (the
    `ErrorReportDocTypes.Configure(opts)` registration shape; the
    `M3DocTypes` / `M4DocTypes` / `M6DocTypes` / `UsageDocTypes` precedent).
  - **ADR 0001-B** — the thin-token rule (the admin surface is
    `[Authorize(Roles = GlobalAdmin)]`; "may this actor see that resource?"
    is resolved by the `Authorization` service per request, not encoded in an
    identity claim).
  - **ADR 0023** — the `Posts/Report` doc's ADR (the reply-report-target
    lane; the M31·10 boundary — the `Posts/Report` doc is **untouched**).
- **The M32 deferral list (for the U01 design doc's `## Scope` Out section):**
  the general issue-submission lane · the local resolution lane · the
  escalation forwarding lane · the `resolved` triage status (M31's
  `TriageStatus` value set is closed to `{"new", "triaged"}`; M32 **adds**
  `"resolved"` additively per ADR 0004 §B.1) · the per-report detail view
  with a resolution note.
- **The `kw-l` key registry (the closure target, M31·7):** the closed
  `errorreport.*` `kw-l` key set (~20 keys) is authored by U05 (the register's
  U05 deliverable) and consumed by U04's error page + U05's admin views. The
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  pins the four-language parity (en/de/fr/da). The `en` values are the source
  text (the ADR 0015 D1 `kw-l` provider-floor discipline); the `de` / `fr` /
  `da` values are the translations (the M30·6 four-language pin). The 20 keys
  are named in the register's Assumptions §"The `kw-l` key set is closed and
  four-language (~20 keys)" — U01's design doc Part 1 does **not** need to
  re-list them (that is U02's §2.3 deliverable); U01 only names the invariant
  (M31·7) and the FACES (M31-8).
