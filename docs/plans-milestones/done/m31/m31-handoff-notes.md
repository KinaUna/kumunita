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

## U01 — design doc Part 1

`docs/design/m31-production-error-handling-design.md` Part 1 authored
(Context / Scope In-Out / Invariants / FACES + the frozen-base header + the
one-thing summary). All 9 entry reads verified; **no drift** against the
register (the U00 facts hold — the `Error()` action shape, the 2-member
`ErrorViewModel`, the 19-line `Error.cshtml`, the `Posts/Report` dormant doc,
the `UsageEvent` row-per-event shape, ADR 0004 §B.1, ADR 0023). Pinned in
Part 1, by id, for U02:

- **10 invariants (M31·1–M31·10):** M31·1 platform-error signal, separate
  context · M31·2 form on the 500 page, always available, non-blocking ·
  M31·3 Marten-native, new parallel surface (`ErrorReportDocTypes`) ·
  M31·4 admin list = GlobalAdmin-gated read, no audit on reads · M31·5
  submission = single-write-lane, exactly one `AccessAudit` row, `Via` tag is
  **U02's §2.1 pin** (additive-append precedent ADR 013/028/036/041) · M31·6
  triage = GlobalAdmin-gated **idempotent** single-write-lane (`Via =
  Admin`), `TriageStatus` closed to `{"new", "triaged"}` · M31·7 closed
  `errorreport.*` `kw-l` set ×4 (exact set is U02's §2.3) · M31·8 six-member
  close flip is U07's (`WhatsNew.cs` `0.47.0`) · M31·9 no new authorization
  surface (error page public; admin `[Authorize(Roles = GlobalAdmin)]`) ·
  M31·10 `Posts/Report` doc **untouched**.
- **8 FACES (M31-1–M31-8):** M31-1 anonymous submit → row + 1 audit +
  confirmation · M31-2 signed-in submit → `SubjectId` set + `Via = Resident`
  · M31-3 GlobalAdmin list read (newest-first, no audit) · M31-4 mark
  `new`→`triaged` (stamp + 1 audit, `Via = Admin`) · M31-5 re-stamp
  already-`triaged` = no-op · M31-6 non-GlobalAdmin → 403 · M31-7 no submit
  → page renders unchanged · M31-8 labels resolve per request language
  (`kw-l`).
- **Nuances for U02:** the `Error()` action (`HomeController` line 153)
  currently reads only `Activity.Current?.Id ?? HttpContext.TraceIdentifier`;
  the `IExceptionHandlerFeature` read is U04's enhancement, not a Part-2
  seam pin. The `ErrorReport` field set (11-member ceiling) is the register's
  Assumptions — U02 pins it in §2.2; the `UsageEvent` 4-field set is the
  *shape* analog only, not the field source. The `*DocTypes` block in
  `Program.cs` spans lines 107–243 (`UsageDocTypes` at 225 is the closest
  analog — `ErrorReportDocTypes.Configure(opts)` goes next to it, per U03).
  The 16 pinned test names + the three-test gate are U02's §2.4/§2.5.

## U02 — design doc Part 2 + ADR 0154

Part 2 (`## Seams & contracts (Part 2, written by U02)`) appended to
`docs/design/m31-production-error-handling-design.md` (§2.1–§2.6) + ADR 0154
(`docs/adr/0154-production-error-handling.md`, `Status: Draft`) + the 0154 row
in `docs/adr/README.md` (`Status: Draft`). All entry reads verified; **no
drift** against Part 1 (the 10 invariants + 8 FACES hold). U03's entry point:
**§2.1 (the seam + `Via` pin) + §2.2 (the Core types)**.

- **(a) The 3 seam method names** (`IErrorReportService`, §2.1):
  `CreateAsync(ErrorReportDraft)` · `MarkTriagedAsync(string reportId, string
  actorId)` · `ListAsync(int maxCount = 100)`. U03 **may** add a trailing
  optional `CancellationToken ct = default` (the house style, a superset — not
  a drift); names/params/return-types/`maxCount = 100` default are frozen.
- **(b) The 16 test names (verbatim, §2.4):** `M31_1_CreateAnonymous_Stores_
  ErrorReport_And_AuditRow` · `M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow`
  · `M31_5_Create_Writes_ExactlyOne_AccessAuditRow` ·
  `M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow` ·
  `M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp` ·
  `M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow` ·
  `M31_4_List_Returns_All_Reports_NewestFirst` ·
  `M31_3_ErrorReport_Doc_FieldSet_Ceiling` ·
  `M31_2_Error_Page_Shows_Report_Form` ·
  `M31_5_Error_Report_Post_Creates_ErrorReport` ·
  `M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error` ·
  `M31_2_Error_Report_Post_Confirmation_Visible` ·
  `M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports` ·
  `M31_4_Admin_List_NonGlobalAdmin_Denied` ·
  `M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row` ·
  `M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp`.
- **(c) The 3 gate tests (by name, §2.5):** closed-loop (anonymous 500 →
  submit → row + 1 audit + `errorreport.thanks`) · handoff (GlobalAdmin mark
  `triaged` → stamp + 1 audit `Via = Admin`; second POST = no-op) ·
  part-vs-whole (the 16 in §2.4 are the whole; closed-loop + handoff are the
  parts; all 16 must be green together).
- **(d) The `Via`-tag pin (U02's decision, §2.1 — U03 implements verbatim):**
  the `CreateAsync` audit row's `Via` = **`AccessVia.Resident`** (ADR 0041)
  for a **non-blank** `SubjectId`; **`AccessVia.Anonymous`** (a **new
  additive** enum value, the 12th — appended after `Resident` in
  `src/Kumunita.Core/Authorization/Decision.cs` with a doc comment citing
  ADR 0154 + the ADR 013/028/036/041 append precedent) for a **blank**
  `SubjectId`. None of the eleven frozen values fits "unsigned visitor"
  (`Resident` requires a signed-in actor), so the least-distortion slot is a
  new value — the repo's established answer to "a new standing with no
  existing tag." The eleven frozen values are **never re-shaped**. The
  `MarkTriagedAsync` audit row's `Via = AccessVia.Admin`. **This is not a new
  authorization surface** (M31·9 holds — no new `AccessAction` / `Decide()`
  branch / `IAuthorizationService` method; the `AccessVia` enum is the
  *record* of "by what right," not a *gate*). **U03 must add the `Anonymous`
  enum value** as part of its deliverables (it is named in §2.1/§2.2; it is
  the one additive `AccessVia` append M31 makes).
- **(e) ADR 0154 + `Posts/Report` untouched flag:** ADR 0154
  (`docs/adr/0154-production-error-handling.md`) is **`Status: Draft`**
  (U07 flips it → `Accepted` at close). The `Posts/Report` doc
  (`src/Kumunita.Core/Posts/Report.cs`, the M3b content-moderation report,
  ADR 0023) is **untouched** (M31·10) — M31 adds a *new* doc in a *new*
  context (`Kumunita.Core.ErrorReports`), not a new field on the existing
  one; the `TargetKind` strings are distinct (`"error-report"` vs `"post"` /
  `"reply"`). **§2.6 drift-guard frozen** (the 10 invariants, the 8 FACES,
  the seam surface, the 11-member field set, the `ErrorReportDraft` shape,
  the `ErrorReportDocTypes` shape, the `Via` pin, the 20-key set, the 16 test
  names, the M32 boundary) — a later unit that finds a mismatch records a
  `## U<m> — Drift pause` (unit-series rule §9) instead of improvising.

## U03 — Core (ErrorReport + service + DI)

Core context `Kumunita.Core.ErrorReports` created (5 new files) + 3 wiring
edits (the `AccessVia.Anonymous` enum append, the DI registration, the
`Program.cs` `Configure` call). `dotnet build Kumunita.slnx -c Debug`
**green (0 errors, 0 warnings)**. All entry reads verified; **no functional
drift** against §2.1/§2.2 — the `Via` pin (`AccessVia.Resident` for
non-blank `SubjectId`, `AccessVia.Anonymous` for blank) is implemented
**verbatim**. U04's entry point: the 3 seam methods below + the `Via` pin.

- **(a) The 3 method names as implemented**
  (`src/Kumunita.Core/ErrorReports/IErrorReportService.cs`,
  `ErrorReportService.cs`): `CreateAsync(ErrorReportDraft draft,
  CancellationToken ct = default)` · `MarkTriagedAsync(string reportId,
  string actorId, CancellationToken ct = default)` · `ListAsync(int maxCount
  = 100, CancellationToken ct = default)` — each with the **trailing optional
  `CancellationToken ct = default`** (§2.1's sanctioned superset, the
  `AdminOnboardingService` house style — a superset, not a drift).
  `CreateAsync`: one write session, stores the `ErrorReport`
  (`TriageStatus = "new"`, `TriagedAt`/`TriagedBy` stay `null`) + **exactly
  one** `AccessAudit` row (`Action = "errorreport.create"`, `TargetKind =
  "error-report"`, `Outcome = Allow`, `Via =` the §2.1 pin —
  `string.IsNullOrEmpty(draft.SubjectId) ? AccessVia.Anonymous :
  AccessVia.Resident`), returns the stored report. `MarkTriagedAsync`: **one
  read session** (not a write session) — if the report is **missing or
  already `triaged`, returns `null` (no-op, no audit row, no state change,
  M31·6)**; else opens a write session, stamps
  `TriageStatus`/`TriagedAt`/`TriagedBy`, stores the report + **exactly one**
  `AccessAudit` row (`Action = "errorreport.triage"`, `TargetKind =
  "error-report"`, `Outcome = Allow`, `Via = AccessVia.Admin`), returns the
  stamped report. `ListAsync`: **read session** (`QuerySession`),
  `.OrderByDescending(x => x.Created).Take(maxCount)` — **no audit row
  (M31·4)**, returns an empty list when there are no reports.
- **(b) The `ErrorReportDocTypes` shape**
  (`src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs`): **1
  `opts.Schema.For<ErrorReports.ErrorReport>()` call + 2 chained
  `.Index(x => x.TriageStatus)` / `.Index(x => x.Created)` calls** — the
  repo's established **chained-`.Index()` convention** (the M6 `Notification`
  / M17 `Bookmark` / M5 `TodoItem` feed-ordering-index shape). **Deviates
  from §2.2's `opts.Schema.For<T>(o => { o.Index(...); })` lambda-config
  form only in syntax, not in semantic intent** (the same 2 indexes on the
  same 2 fields) — this Marten version's `Schema.For<T>()` takes **no**
  config lambda (CS1501 — the AGENTS.md "version-pinned Marten API" trap),
  so the chained form is the compile-correct shape. U06's drift pin
  (`M31_3_ErrorReport_Doc_FieldSet_Ceiling`) asserts the **field set**, not
  the registration syntax — the 11-member `ErrorReport` doc
  (`src/Kumunita.Core/ErrorReports/ErrorReport.cs`) is **verbatim** §2.2,
  `TriageStatus` defaults `"new"`, `TriagedAt`/`TriagedBy` default `null`.
- **(c) The DI registration line**
  (`src/Kumunita.Core/DependencyInjection.cs`, immediately after the
  `AdminOnboarding.IAdminOnboardingService` registration, the
  `M30 (ADR 0153, U03)` block): `services.AddTransient<ErrorReports.IErrorReportService>(sp
  => new ErrorReports.ErrorReportService(sp.GetRequiredService<Marten.IDocumentStore>()));`
  — the `IAdminOnboardingService` / `ISiteContentService` fully-qualified
  "AddTransient with the store injected" shape (the ADR 0006-D Core
  stays-HTTP-free rule holds — `ErrorReportService` composes the host-
  registered `IDocumentStore` only).
- **(d) The `Program.cs` line added** (`src/Kumunita.Web/Program.cs`,
  immediately after the `StorageSettingsDocTypes.Configure(opts);` line —
  the last line of the `*DocTypes` block, just before the closing `})` of
  the `Marten` registration): `ErrorReportDocTypes.Configure(opts);` — the
  `M3/Media/Usage/Document` "one line per bounded context" precedent.
  `SchemaBootstrap.cs` line 48 calls
  `ApplyAllConfiguredChangesToDatabaseAsync()`, which applies **every**
  configured Marten schema change — the new `ErrorReport` surface is picked
  up **automatically** at the versioned boot with no per-DocTypes list
  (confirmed, the design-doc §2.2 "Wiring" note holds).
- **(e) The `AccessVia.Anonymous` append**
  (`src/Kumunita.Core/Authorization/Decision.cs`, appended **after** the
  existing `Resident` value — the **12th** `AccessVia` value, the
  eleven frozen values `Owner`/`Audience`/`Delegation`/`Moderator`/`Report`/
  `BreakGlass`/`Admin`/`Group`/`Guardian`/`Community`/`Resident` are
  **never re-shaped**): a `/// <summary>` doc comment citing ADR 0154
  (M31·5) + the ADR 0013 `Group` / ADR 0028 `Guardian` / ADR 0036 `Community`
  / ADR 0041 `Resident` additive-append precedent, and noting this is a
  *record* of "by what right," not a gate (M31·9 — no new `AccessAction` /
  `Decide()` branch / `IAuthorizationService` surface). No re-order, no
  re-shape.
- **(f) Compile warnings:** **0** warnings, **0** errors (the pre-existing
  `CS8600`/`CS8604` warnings in `SampleDataSeeder.cs` / `SmtpSender.cs`
  that were present before this unit's edits are unchanged — not introduced
  by U03). The one fix applied during this unit (both in **U03's own new
  files**, no drift): the §2.2 `ErrorReportService.ListAsync` snippet's
  `.OrderDescending(x => x.Created)` (a typo — no such LINQ operator exists;
  CS1660) was corrected to `.OrderByDescending(x => x.Created)` (the
  repo's pinned shape, `AnnouncementService`/`MessagingService`/
  `UserInfoService` all use `.OrderByDescending(.Created)`); the §2.2
  `ErrorReportDocTypes` lambda-config form was replaced with the chained
  `.Index()` form (item (b) above). Both changes are **in U03's new files
  only** and preserve §2.2's semantic intent exactly — **not** a drift
  pause (the design doc's intent is unchanged; the fixes make the code
  compile-correct in this Marten version, the exact class of bug AGENTS.md
  warns about). U04's entry: the 3 seam methods above; the `Via` pin
  (`AccessVia.Resident` / `AccessVia.Anonymous` for `CreateAsync`;
  `AccessVia.Admin` for `MarkTriagedAsync`); the 5 new
  `ErrorViewModel` fields U04 adds; the 20 `kw-l` keys U05 authors (U04
  consumes keys 1–8 on the error page).

## U04 — error page enhancement

`dotnet build Kumunita.slnx -c Debug` **green (0 errors, 0 warnings)**. All
entry reads verified; **no drift** against the register's U04 deliverable or
§2.1/§2.3 — the 5 `ErrorViewModel` fields are the register's list verbatim,
the 7 form `kw-l` keys 1–8 are consumed verbatim, the U03 seam is called
**as-is** (the `Via` pin is U03's done work — not re-litigated). 5 files
touched (4 in `src/Kumunita.Web` + this note). U05's entry point: keys 1–8
below are now **consumed** by the error page (the `en` values the `kw-l`
TagHelper / `ITranslationProvider.GetAsync` resolve are the §2.3 floor); the
`ErrorReportFormModel` bind-model shape + the `POST /Home/Error/Report` route
+ the `FormSubmitted` confirmation are the page U05's admin view mirrors.

- **(a) The `Error()` action**
  (`src/Kumunita.Web/Controllers/HomeController.cs`, `Error()` — now an
  `IActionResult` returning a `View(...)`; the `IExceptionHandlerFeature`
  read is one line: `Exception? ex =
  HttpContext.Features.Get<IExceptionHandlerFeature>()?.Error;`, then
  `ExceptionType = ex?.GetType().Name` + `ExceptionMessage = ex?.Message`
  + the existing `RequestId = Activity.Current?.Id ??
  HttpContext.TraceIdentifier`). The `using
  Microsoft.AspNetCore.Diagnostics;` was added for
  `IExceptionHandlerFeature`. **The `POST /Home/Error/Report` action**
  (`ErrorReport(ErrorReportFormModel form)`, `async Task<IActionResult>`,
  `[HttpPost("/Home/Error/Report")]` + `[ValidateAntiForgeryToken]`, the
  house `AdminOnboardingController.Complete` shape): reads
  `requestId`/`userAgent`(truncated to 256)/`subjectId`
  (`KumunitaPrincipal.SubjectId(User) ?? string.Empty`); on
  `!ModelState.IsValid` re-renders the `Error` view with
  `FormSubmitted=false` + the resident's typed `FormDescription` /
  `FormContactEmail` preserved (a blank description → 400 re-render, never
  a 500, M31·5); else builds `new ErrorReportDraft(SubjectId, Description,
  ContactEmail, RequestId, ExceptionType, UserAgent)` and calls
  `await ErrorReports.CreateAsync(draft).ConfigureAwait(false)` inside a
  `try/catch` that **swallows** (a report failure must never surface as a
  500 to the resident, M31·5 "never a 500 back to the resident"), then
  re-renders the `Error` view with `FormSubmitted = true` (the
  `errorreport.thanks` confirmation — no redirect, no modal, M31·2).
  `IErrorReportService` is injected as an **optional** last ctor param
  (`IErrorReportService? errorReports = null`, the `PostService?` /
  `ISiteContentService?` house shape — null in test constructions, the form
  still renders + a submit degrades to the confirmation). A private
  `Truncate(string?, int)` helper caps the user agent at 256 (the
  register's U04 shape).
- **(b) The `ErrorViewModel` fields added (5)**
  (`src/Kumunita.Web/Models/ErrorViewModel.cs`): `ExceptionType`
  (`string?`), `ExceptionMessage` (`string?`), `FormSubmitted`
  (`bool`, default `false`), `FormDescription` (`string`, default `""`),
  `FormContactEmail` (`string?`) — **verbatim** the register's U04 list.
  `RequestId` + `ShowRequestId` unchanged. **New bind model**
  (`src/Kumunita.Web/Models/ErrorReportFormModel.cs`): `Description`
  (`[Required]` — the register's U04 "required, non-blank" pin; no extra
  `MinLength` over-constraint) + `ContactEmail` (`[EmailAddress]
  [MaxLength(254)]` — the 254-char email cap is the RFC 5321 maximum, the
  `ContactEmail` field's natural bound) — the POST action's bind target
  (the view's `@model` is `ErrorViewModel`, so the form uses explicit `name`
  attributes — the echo-back comes from `Model.FormDescription` /
  `Model.FormContactEmail`).
- **(c) The `Error.cshtml` form markup**
  (`src/Kumunita.Web/Views/Shared/Error.cshtml`): the existing
  `error.title` / `error.subtitle` / `error.request_id` /
  `error.development_title` / `error.development_hint` `kw-l` keys are
  **intact** (the register's U04 "keep the existing … content intact" pin);
  the `@Model.ExceptionType` / `@Model.ExceptionMessage` fields are
  **deliberately NOT rendered** on the public page (a Razor/security
  choice — they would leak implementation details, file paths, SQL,
  internal state; they are captured into the `ErrorReportDraft` via the
  POST action's `IExceptionHandlerFeature` read + the `ErrorViewModel`
  fields are present per the register's U04 list — M31·2 / SECURITY.md
  privacy posture; the public page stays generic, the request ID is the
  resident's correlation handle); **below** an `<hr>`: if
  `Model.FormSubmitted` → the `errorreport.thanks` confirmation
  (key 8, the `alert-success`); else → the report form consuming **the 7
  `kw-l` keys 1–7 verbatim** (`errorreport.title` /
  `errorreport.intro` / `errorreport.description.label` /
  `errorreport.description.placeholder` / `errorreport.email.label` /
  `errorreport.email.placeholder` / `errorreport.submit`) + a
  `<textarea name="Description">` + an `<input type="email"
  name="ContactEmail">` + a submit button, POSTing to `/Home/Error/Report`
  with `@Html.AntiForgeryToken()`. The form is **public** (no
  `[Authorize]` gate, M31·2 / M31·9). **Razor traps honored (AGENTS.md):**
  the 7 keys are resolved **server-side** (the `EffectiveLanguageCode.
  ResolveAsync` + `ITranslationProvider.GetAsync` house seam, the
  `_OnboardingBanner.cshtml` shape) into local vars (`rpTitle` …
  `rpThanks`), with the `KnownTranslationKeys.EnValues` provider-floor
  fallback (ADR 0015 D1) — so **no `<kw-l>` inside a quoted attribute
  value** and **no un-awaited `Task<string>`** in markup.
  `@section Scripts { <partial name="_ValidationScriptsPartial" /> }`
  included (the layout `RenderSectionAsync("Scripts")` is `required:
  false`).
- **(d) Compile warnings:** **0** new warnings — the 68 solution warnings
  are all pre-existing (test files + `_Layout.cshtml` / `Groups/Detail.cshtml`
  / `Posts/Detail.cshtml` / `PostsController.cs` / `ProjectsController.cs` /
  `_WhatsNewToast.cshtml` / `_PageForm.cshtml` / `_BoardCard.cshtml` /
  `TodosIndex.cshtml` / `AccountController.cs` — **none** in `Error.cshtml` /
  `HomeController.cs` / `ErrorViewModel.cs` / `ErrorReportFormModel.cs`,
  verified by `grep -E "Error\.cshtml|ErrorViewModel|ErrorReportFormModel|
  HomeController"` on the build output → empty). Two compile fixes during
  this unit (both in **U04's own view only**, no drift): (1) the first pass
  referenced `ITranslationProvider` unqualified (CS0246) — fixed with
  `@using Kumunita.Core.Localization` + the `@inject` lines; (2) the first
  pass used a `static string L(...)` local fn with a
  `.GetAwaiter().GetResult()` bridge (a non-async `@{}` block) — replaced
  with the house `_OnboardingBanner.cshtml` shape: a `static async Task<
  string> L(...)` local fn + direct `await tp.GetAsync(key, lang)` (the
  Razor `@{}` block is async, so `await` is the compile-correct + house-
  idiomatic shape; the resolution is the same `GetAsync` the TagHelper path
  uses).
- **(e) `m31-u04.md` move:** **skipped** — the file does not exist in
  `in-progress/` (only `m31-u00.md` is present, already moved to
  `done/m31/`); the register's "if the file exists" condition is not met.

## U05 — admin surface + kw-l keys

`dotnet build Kumunita.slnx -c Debug` **green (0 errors, 0 warnings in my
files — 68 solution warnings are all pre-existing, none in
`ErrorReportAdminController` / `AdminErrorReportViewModel` /
`ErrorReports/Index.cshtml` / `KnownTranslationKeys.cs`, verified by
`grep -E "ErrorReportAdminController|AdminErrorReportViewModel|ErrorReports/
Index"` on the build output → empty)**. All entry reads verified; **no
drift** against the register's U05 deliverable or §2.1/§2.3 — the two routes
are the register's verbatim, the `AdminErrorReportViewModel` fields are the
register's list (2), the 20 `errorreport.*` keys are the §2.3 closed set
verbatim (en values = §2.3 source text; de/fr/da = U05's translations), the
`Via` pin + the U03 seam are called **as-is** (U03's done work — not
re-litigated). 4 files touched (3 new in `src/Kumunita.Web` + the registry
modify). U06's entry point: the 4 `AdminErrorReportTests` (§2.4 items 13–16)
target the controller + the `errorreport.list.*` keys 9–20.

- **(a) The two routes**
  (`src/Kumunita.Web/Controllers/ErrorReportAdminController.cs`):
  `GET /admin/error-reports` → `IErrorReportService.ListAsync(100)` →
  `AdminErrorReportViewModel` (M31·4 — a read, no per-row authz, no audit
  row). `POST /admin/error-reports/{id}/triage` →
  `IErrorReportService.MarkTriagedAsync(id, actorId)` → redirect to
  `/admin/error-reports` (the `AdminOnboardingController.Complete` shape —
  `[ValidateAntiForgeryToken]` POST + `TempData["info"]` flash +
  `RedirectToAction(nameof(Index))`). The flash (the
  `errorreport.list.flash_triaged` key, the U05 closed set, M31·7) is set
  **only** when `MarkTriagedAsync` returns non-null (an effective
  `"new"` → `"triaged"` transition, M31·6); a no-op (an already-`triaged`
  report) sets no flash → no second audit row (M31·6 idempotency). The
  `[Authorize(Roles = GlobalAdmin)]` gate is the standard admin surface (the
  `AdminOnboardingController` shape, ADR 0001-B thin-token rule); **no new
  `AccessAction` / `Decide()` branch / `IAuthorizationService` surface**
  (M31·9). The `ILocalizationService` + `ITranslationProvider` ctor params
  are **optional** (default null, the `AdminOnboardingController` house
  shape — null in test constructions renders the
  `KnownTranslationKeys.EnValues` source text, the kw-l floor, ADR 0015 D1).
- **(b) The `AdminErrorReportViewModel` fields (2)**
  (`src/Kumunita.Web/Models/AdminErrorReportViewModel.cs`):
  `IReadOnlyList<ErrorReport> Reports` (the `ListAsync` read, newest first,
  empty when no reports) + `bool FlashTriaged` (true when the just-processed
  action was an effective triage, M31·6) — **verbatim** the register's U05
  list.
- **(c) The 20 `kw-l` key names (verbatim, §2.3):** keys 1–8 (consumed by
  U04's error page — **already present in the registry** from U04? **No** —
  U04 *consumed* them in the view but did **not** author them in the
  registry; U05 authors the **complete** 20-key set):
  `errorreport.title` / `errorreport.intro` /
  `errorreport.description.label` / `errorreport.description.placeholder` /
  `errorreport.email.label` / `errorreport.email.placeholder` /
  `errorreport.submit` / `errorreport.thanks` / `errorreport.list.title` /
  `errorreport.list.empty` / `errorreport.list.status.new` /
  `errorreport.list.status.triaged` / `errorreport.list.mark_triaged` /
  `errorreport.list.request_id` / `errorreport.list.exception` /
  `errorreport.list.description` / `errorreport.list.reporter` /
  `errorreport.list.created` / `errorreport.list.anonymous` /
  `errorreport.list.flash_triaged`.
- **(d) The four-language status:** all 20 keys are present, non-empty, in
  **en/de/fr/da** (verified by `grep -c "\"errorreport.$k\""` on
  `KnownTranslationKeys.cs` → **4** for each of the 20 keys; inserted
  immediately after the `adminonboarding.banner.action` line in each
  language, mirroring the M30·6 / M22 D7 four-language pin). The `en` values
  are the §2.3 source text (the ADR 0015 D1 `kw-l` provider-floor
  discipline); the `de` / `fr` / `da` values are the translations (U05's
  authoring, the M30·6 four-language pin).
- **(e) Compile warnings:** **0** new warnings — the 68 solution warnings
  are all pre-existing (test files + `_Layout.cshtml` / `_WhatsNewToast.cshtml`
  / `AccountController.cs` / `PostsController.cs` / `ProjectsController.cs` /
  `SampleDataSeeder.cs` / `SmtpSender.cs` / `DocumentsOrganizationTests.cs` /
  `GuardianAssignmentTests.cs` / `SampleDataCorpusBackfillTests.cs` /
  `PageServiceTests.cs` / `UserInfoServiceTests.cs` — **none** in my 4
  files, verified by `grep -E
  "ErrorReportAdminController|AdminErrorReportViewModel|ErrorReports/
  Index"` on the build output → empty). **One compile fix** during this unit
  (in **U05's own view only**, no drift): the first pass used an `@{ }`
  local-variable block inside the `@foreach (var r in Model.Reports)` block
  (RZ1010 — the `@foreach` already opens a code block, so the inner `@{ }`
  is invalid); fixed by declaring the 4 local `var` (`desc` / `descShort` /
  `exc` / `excShort`) directly in the `@foreach` body (no `@{ }` wrapper) —
  the house `AdminSite/Index.cshtml` shape (a `var` declaration inside the
  `@foreach` body, not a nested `@{ }`). **Razor traps honored (AGENTS.md):**
  (1) the status chip uses a conditional `@if/@else` with **two separate
  `<kw-l>` elements** (each with a static key:
  `errorreport.list.status.new` + `errorreport.list.status.triaged`), so the
  `KwLRegistryConsistencyTests` static scan finds both keys — **no
  interpolated `@(cond ? "kw1" : "kw2")` key** (the `KwLRegistryConsistencyTests`
  scan only matches literal string keys, not Razor expressions); (2) the
  flash string is **server-resolved** by the controller's `FlashAsync` (the
  `AdminOnboardingController.FlashAsync` idiom), so the view only renders
  the resolved string — **no un-awaited `Task<string>`** in markup; (3) all
  Bootstrap classes (`text-bg-warning` / `text-bg-success` / `badge` /
  `list-group-item` / `d-flex` / `align-items-start` / `gap-3` / `flex-grow-
  1` / `small` / `mt-1` / `text-muted` / `fw-semibold` / `btn` / `btn-sm` /
  `btn-primary` / `text-nowrap` / `ms-2` / `me-0` / `alert` / `alert-success`)
  are **static strings** — **no `style=`/`data-*` attributes built by
  interpolating quoted strings via `@(...)`**.
- **(f) `m31-u05.md` move:** **skipped** — the file does not exist in
  `in-progress/` (the register's "if the file exists" condition is not met;
  only `m31-u00.md` is present, already moved to `done/m31/`).

## U06 — seam tests (16) + gate recorded

**16/16 green; the three-test gate passes; no drift pause, no still-open
drift.** `dotnet build Kumunita.slnx -c Debug` **green (0 errors, 0 new
warnings — all pre-existing)**. Three new test files (the §2.4 pinned names
verbatim — the §2.6 drift guard held; the 16 are the names frozen in U02's
§2.4, not re-litigated) + the design doc `### Run result` gate append.
U07's entry point: the three gate tests below (all green) + the
`MilestonesTests.M31_Is_The_Single_InProgress_Milestone` pin (still green —
U07 flips it to `M32_...` at close) + the 16 names verbatim.

- **(a) The 3 test file paths:** `tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs`
  (8) · `tests/Kumunita.Web.Tests/ErrorReportPageTests.cs` (4) ·
  `tests/Kumunita.Web.Tests/AdminErrorReportTests.cs` (4).
- **(b) The 16 test names (verbatim, §2.4):** `M31_1_CreateAnonymous_Stores_
  ErrorReport_And_AuditRow` · `M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow`
  · `M31_5_Create_Writes_ExactlyOne_AccessAuditRow` ·
  `M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow` ·
  `M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp` ·
  `M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow` ·
  `M31_4_List_Returns_All_Reports_NewestFirst` ·
  `M31_3_ErrorReport_Doc_FieldSet_Ceiling` ·
  `M31_2_Error_Page_Shows_Report_Form` ·
  `M31_5_Error_Report_Post_Creates_ErrorReport` ·
  `M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error` ·
  `M31_2_Error_Report_Post_Confirmation_Visible` ·
  `M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports` ·
  `M31_4_Admin_List_NonGlobalAdmin_Denied` ·
  `M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row` ·
  `M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp`.
- **(c) The 16 pass/red counts:** **16 pass / 0 red** (Core 8/8, Web 8/8).
  Verification path (the AGENTS.md runner quirk — `dotnet test` discovery is
  unreliable on this machine): `dotnet exec
  tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll
  -displayName "*M31*"` → `Total: 8, Errors: 0, Failed: 0` (live Postgres via
  Testcontainers, ~9.5s); `dotnet exec
  tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll
  -displayName "*M31*"` → `Total: 9, Errors: 0, Failed: 0` (the 9th is the
  pre-existing `MilestonesTests.M31_Is_The_Single_InProgress_Milestone`,
  still green). **Part-vs-whole:** the full Web suite ran **977 total,
  0 failed, 1 skipped** (no regression; the `MilestonesTests` +
  `WhatsNewTests` close-flip pins are intact — U07's preconditions hold).
- **(d) The three-test gate status:** **closed-loop — GREEN** (covers
  `M31_1_...` + `M31_5_Create_...` + `M31_5_Error_Report_Post_Creates_...` +
  `M31_2_Error_Report_Post_Confirmation_...`) · **handoff — GREEN** (covers
  `M31_6_MarkTriaged_New_...` + `M31_6_MarkTriaged_Writes_...` +
  `M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp` + `M31_6_Admin_MarkTriaged_GlobalAdmin_...`
  + `M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp`) · **part-vs-whole —
  GREEN** (the 16 are the whole; all green together — 16/16). Recorded in
  the design doc `### Run result (M31 acceptance gate — 2026-10-09)`.
- **(e) Still-open drift:** **none.** No `## U<m> — Drift pause` section in
  the note (U00–U05 each recorded "no drift"). The `Via` pin
  (`AccessVia.Resident` non-blank / `AccessVia.Anonymous` blank — U02's §2.1
  decision, U03's implementation) is asserted verbatim by
  `M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow` (Anonymous) and
  `M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow` (Resident) — not
  re-litigated. Two **test-harness** compile fixes during this unit (both in
  **U06's own test files**, no code drift): (1) the `M31_2_Error_Page_Shows_
  Report_Form` public-page pin originally asserted
  `Assert.DoesNotContain("[Authorize]", src)`, which the view's own
  documentation comment ("NOT inside any [Authorize] gate") tripped —
  replaced with a comment-proof, meaningful pin: the view has **no
  role-claim gate** (`IsGlobalAdmin` / `IsInRole` / `KumunitaPrincipal.Is*`
  all absent — the page is the one place an anonymous visitor sees a product
  surface, M31·2 / M31·9); (2) two `x is null` patterns in the NSubstitute
  `Arg.Is<ErrorReportDraft>` expression trees (CS8122 — `is` pattern
  matching is not allowed in an expression tree) were replaced with
  `== null`. Both changes preserve the §2.4 / §2.1 pins exactly — **not** a
  drift pause (the design doc's intent is unchanged). **`m31-u06.md` move:
  skipped** — the file does not exist in `in-progress/` (the register's "if
  the file exists" condition is not met; consistent with U04 / U05).
  U07's entry: flip the `Milestones.cs` `M31` row `StatusNext` → `StatusDone`
  + promote `M32` `StatusPlanned` → `StatusNext` (order unchanged), re-pin
  `MilestonesTests.M31_Is_The_Single_InProgress_Milestone` →
  `M32_...`, append the `WhatsNew.cs` `0.47.0` entry (newest-first, naming
  M31 + ADR 0154), the README / `STATUS.md` / `ARCHITECTURE.md` parity
  appends, flip ADR 0154 `Draft` → `Accepted` + the index row `**Done**
  (M31)`, and the `done/m31/` move.

## U07 — close

**The six-member close flip lands (M31·8); the milestone is done.** No code
change (docs + `Milestones.cs` + `MilestonesTests.cs` + `WhatsNew.cs` +
`WhatsNewTests.cs` only — the M31·8 / hard-rule boundary held: no Core, no
Web controller/view/service, no register re-shape, no ADR re-write beyond
the `Draft`→`Accepted` flip + the index tag). All of U06's preconditions held
(16/16 green, the three-test gate green, no `## U<m> — Drift pause`, no
still-open drift — so this is a clean close, not a drift pause). U07 is the
final M31 unit; the register + this note move to `done/m31/` (the `done/m30/`
/ `done/m29/` subfolder convention).

- **(a) The `Milestones.cs` flip** (`src/Kumunita.Web/Milestones.cs`) — the
  `M31` row `StatusNext` → **`StatusDone`** + the `M32` row `StatusPlanned`
  → **`StatusNext`**. The order is **unchanged** (`…"M30","M31","M32"` — the
  ADR 013/089/093/109 "named lane, not a renumber" precedent; M32 is the
  next letter in place, not a new letter). `M33`/`M34` remain `StatusPlanned`
  (untouched).
- **(b) The `MilestonesTests` re-pin**
  (`tests/Kumunita.Web.Tests/MilestonesTests.cs`) — `M31_Is_The_Single_
  InProgress_Milestone` **replaced** with `M32_Is_The_Single_InProgress_
  Milestone` (the single-`StatusNext` assertion now names `M32`; the
  `StatusPlanned` set is now `{"M33","M34"}`; the `StatusDone` "not in" set
  now excludes `M32`) + **`"M31"` appended** to the
  `Shipped_Milestones_Are_Marked_Done` done-list (after `"M30"`). The
  `Roadmap_Covers_M0_Through_M34_Plus_Named_Lanes_In_Order` order pin is
  unchanged (the `…,"M30","M31","M32","M33","M34"` sequence is intact — the
  no-renumber rule).
- **(c) The `WhatsNew.cs` `0.47.0` entry** (`src/Kumunita.Web/WhatsNew.cs`,
  **newest-first**, date `2026-10-09`) — the sixth close-flip member (ADR
  0110 / AGENTS.md), naming M31 + ADR 0154 (the register's exact text: the
  report-an-issue form on the 500 page, the GlobalAdmin triage at
  `/admin/error-reports`, one new bounded context `ErrorReports`, one new
  doc `ErrorReport`, one new service `IErrorReportService`, the closed
  `errorreport.*` `kw-l` key set × en/de/fr/da). `0.46.0`/M30 slides one row
  back, `0.45.0`/M29 two rows back. **The `WhatsNewTests` head pin
  re-pinned** (`The_Improve_Lane_Reduction_Entry_Is_Shipped`) to name the
  new head: `0.47.0`/`2026-10-09` + the "error handling" capability (M31),
  `0.46.0`/M30 "admin onboarding" one row back, `0.45.0`/M29 "surface
  label" two rows back — the head-pin discipline (a copy-paste that drops or
  re-orders the head is caught) is held, the same way M30's close re-pinned
  it.
- **(d) The three parity appends** (the doc↔code parity pair set, AGENTS.md):
  `README.md` — the status-summary block gains the **`**M31 is done** —
  production error handling (…; ADR 0154)`** line (the M30 shape) and the
  planned-line re-scopes to **`**M32–M34 are planned** — three new
  milestones queued next: … (M32), … (M33), … (M34)`** (M31 removed from the
  planned set, M32 promoted to next — consistent with the `Milestones.cs`
  flip); the **Roadmap `M31` line** gains the **`**Done.** (ADR 0154)`** tail
  (the M30 `**Done.** (ADR 0153)` shape, verbatim). `STATUS.md` — the
  M30-terminated status line gains the **`**M31 is done** — production error
  handling (a report-an-issue form on the 500 error page + a GlobalAdmin
  triage surface at `/admin/error-reports`; one new bounded context, one new
  doc, one new service; ADR 0154)`** tail (the M30 shape).
  `ARCHITECTURE.md` — the M30-terminated milestone-status line gains the
  **`**M31 production error handling is shipped** (ADR 0154) — …`**
  `ErrorReports/` context line (the M30 `AdminOnboarding/` shape — the new
  bounded context, the `ErrorReport` non-singleton doc, the
  `IErrorReportService` read + audited-write seams, the
  `ErrorReportDocTypes` surface, the `/admin/error-reports`
  `ErrorReportAdminController`, the public `/Home/Error` report form, the
  closed `errorreport.*` `kw-l` set, the one additive `AccessVia.Anonymous`
  value, the `Posts/Report` doc ADR 0023 untouched).
- **(e) The ADR flip + index tag** — `docs/adr/0154-production-
  error-handling.md`: **`Status: Draft` → `Status: Accepted`** (the register
  + design doc §2.6 drift-guard held — no ADR re-write, only the status
  line). `docs/adr/README.md`: the `0154` index row **`Draft` → `Accepted —
  **Done** (M31)`** (the M30 `0153` row shape, `Accepted — **Done** (M30)`).
- **(f) The `done/m31/` move (last)** — `git mv` the register
  `plan-m31-production-error-handling.md` → `done/m31/` + `git mv` the
  handoff note `in-progress/m31-handoff-notes.md` → `done/m31/
  m31-handoff-notes.md`. **Only `m31-u00.md` exists** (already in
  `done/m31/`); `m31-u04`/`u05`/`u06.md` were never created (the register's
  "if the file exists" condition not met, consistent with U04/U05/U06), so
  the only moves are the register + the handoff note. `done/m31/` now holds
  the register + the unit plan (`m31-u00.md`) + the handoff note (the
  `done/m30/` / `done/m29/` subfolder convention).

**Exit:** `dotnet build Kumunita.slnx -c Debug` **green**; `dotnet exec
tests/Kumunita.Web.Tests/bin/Debug/net10.0/Kumunita.Web.Tests.dll`
**green** — the `MilestonesTests` single-in-progress pin now on `M32` (the
order + the done-list hold), the `WhatsNewTests` `0.47.0` newest-first head
pin green (the head re-pin holds), and the rest of the suite unregressed.
**The M31 milestone is done — the six-member close flip is the last M31 unit;
there is no next M31 agent.** The next thing is **M32 (Issue submission &
escalation)** as a fresh milestone (the `Milestones.cs` `M32` row is now
`StatusNext`, the roadmap order is frozen at `…M30,M31,M32`).
