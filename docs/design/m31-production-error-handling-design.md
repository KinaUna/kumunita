# M31 — Production error handling (design doc)

> **Milestone M31 — Production error handling.** The README / `Milestones.cs`
> line, verbatim: "**Production error handling** — a first-class
> report-an-issue affordance on error pages so a resident can easily say what
> went wrong, plus a GlobalAdmin surface listing the reports so they can
> triage and act." M31 ships **the error-page intake + the admin triage**:
> the 500 error page (`/Home/Error`) gains a short report form (what were you
> trying to do? + optional contact email) that stores an `ErrorReport` row in
> a **new bounded context** (`Kumunita.Core.ErrorReports`) + one `AccessAudit`
> row, and a **new `/admin/error-reports` GlobalAdmin surface** lists the
> reports newest-first and lets the admin mark a report **triaged**
> (idempotent; one `AccessAudit` row per effective triage).
>
> **Three-tier contract.** This file is the **primary** tier of M31's
> contract: it pins the **invariants (M31·1–M31·10)**, the **FACES
> (M31-1–M31-8)**, and (in Part 2) the exact `ErrorReport` field set, the
> `IErrorReportService` seam contract, the `AccessAudit` `Via` pin, the
> closed `errorreport.*` `kw-l` key set, the pinned seam-test names, the
> acceptance gate, and the drift guard. The register
> (`docs/plans-milestones/plan-m31-production-error-handling.md`) is the
> **secondary** tier (unit-level deliverables + exit criteria).
> `docs/plans-milestones/in-progress/m31-handoff-notes.md` is the **scratch**
> tier (one short section per unit, appended, never rewritten). When the
> three disagree, **this file wins for the pinned shapes**; the register wins
> for *which files exist* and *what each unit does*.
>
> **Part 1 (this file, U01):** the context, the scope (In / Out, incl. the
> named M32 deferrals), the **ten invariants** (M31·1–M31·10), and the
> **eight FACES** (M31-1–M31-8), plus the frozen-base assumptions.
> **Part 2 (U02):** the seams & contracts (the exact `ErrorReport` field
> set, the `IErrorReportService` 3-method surface, the `ErrorReportDraft`
> record, the `ErrorReportDocTypes` registration shape, the `AccessAudit`
> `Via` pin for the submission row, the closed `errorreport.*` `kw-l` key
> set, the 16 pinned test names, the acceptance gate, the drift guard) +
> **ADR 0154**.
>
> **The frozen base.** M31 is built **on top of** the ADR 0004 §B.1
> Marten-native / parallel-surface pattern (the `ErrorReportDocTypes`
> registration shape, the `M3DocTypes` / `M4DocTypes` / `M6DocTypes` /
> `UsageDocTypes` precedent), the ADR 0001-B thin-token rule (the admin
> surface is `[Authorize(Roles = GlobalAdmin)]`), the ADR 0006 C3
> single-write-lane shape (one write session storing the `ErrorReport` doc +
> exactly one `AccessAudit` doc in the same session), the ADR 0015 D1
> `kw-l` provider-floor discipline (the `en` value is the source text), the
> `Usage/UsageEvent` row-per-event doc shape (the POCO + the conventional
> string `Id` — the shape `ErrorReport` mirrors, not the field source), and
> the ADR 013/028/036/041 additive-`AccessVia`-append precedent (the eleven
> frozen `AccessVia` values are never re-shaped). All of these still bind
> **unchanged**. M31 adds **one new non-singleton doc** (`ErrorReport`, one
> row per report), **one new bounded context** (`Kumunita.Core.ErrorReports`),
> **one new registration surface** (`ErrorReportDocTypes`), **one enhanced
> 500 error page** (+ the `POST /Home/Error/Report` submission action),
> **one new GlobalAdmin surface** (`/admin/error-reports` +
> `POST /admin/error-reports/{id}/triage`), and the **closed
> `errorreport.*` `kw-l` key set** × en/de/fr/da — but it adds **no**
> re-shape of the existing `Posts/Report` doc (ADR 0023, M31·10), **no** new
> `AccessAction` / `Decide()` branch / `IAuthorizationService` method
> (M31·9), **no** `resolved` triage status and **no** escalation forwarding
> lane (the M32 boundary), and **no** login gate on the error page's report
> form (M31·2). It is **additive**. **No EF migration** (a new Marten doc
> type is additive per ADR 0004 §B.1; the delta is applied idempotently at
> boot).
>
> **The `ErrorReport` field set is the ceiling** (the 11-member set pinned by
> the register's Assumptions and re-pinned in Part 2 §2.2 — no field outside
> the set may appear in the doc). **The `TriageStatus` value set is closed
> in M31** to `{"new", "triaged"}` — M32 **adds** `"resolved"` additively, it
> does not re-shape the existing two. **The existing `Posts/Report` doc is
> untouched** (M31 adds a *new* doc in a *new* context, not a new field on an
> existing one, the ADR 0006 module-boundary contract). **The closed
> `errorreport.*` `kw-l` key set is the floor** (Part 2 §2.3 pins the exact
> set).
>
> **The one thing every unit must respect:** M31 is the **error-page report +
> admin triage** — the resident says what went wrong (the 500 page's form is
> always available, anonymous-safe, non-blocking, M31·2), and the admin
> acknowledges it (the `GlobalAdmin`-gated list + the idempotent
> mark-as-triaged, M31·4 / M31·6). The `ErrorReport` doc is a
> **platform-error signal, not a content-moderation report** (M31·1 /
> M31·10 — the `Posts/Report` doc is untouched). The `ErrorReport` doc is
> **Marten-native, registered in a new parallel surface** (M31·3). The report
> submission is **a single-write-lane** — one `AccessAudit` row, never a 500
> back to the resident (M31·5). The triage action is **a
> GlobalAdmin-gated idempotent single-write-lane** — re-stamping an
> already-`triaged` row is a no-op (M31·6). Every new user-visible string is
> a **closed `errorreport.*` key in four languages** (M31·7). The
> `Milestones.cs` / README / `MilestonesTests` / `WhatsNew.cs` six-member
> close flip is **U07's** (M31·8). There is **no new authorization surface**
> (M31·9). It does **not** add a general issue-submission lane, a local
> resolution lane, an escalation forwarding lane, a `resolved` status, or a
> per-report detail view (all M32).

## Context

When a resident hits a 500 error, the current `/Home/Error` page (the
`UseExceptionHandler("/Home/Error")` target, `Program.cs` line 612, the
production-only pipeline block) shows a generic "Something went wrong"
message + the request ID (`error.title` / `error.subtitle` /
`error.request_id` `kw-l` keys). The resident has no way to say *what they
were doing* when the error happened, and the GlobalAdmin has no surface to
collect, read, or acknowledge those reports. The exception is logged to the
M13 file sink (the `app-*.log` files), but the log is machine-oriented — it
captures the stack trace, not the resident's perspective ("I was trying to
RSVP for the Saturday event and the page went blank"). The
`IExceptionHandlerFeature` exception object is available to the `Error()`
action (`HomeController` line 153, currently only reads
`Activity.Current?.Id ?? HttpContext.TraceIdentifier` into the 2-member
`ErrorViewModel`) but is never surfaced to a human on either side.

M31 closes this gap with a **two-surface lane**: (1) the **500 error page**
gains a short report form (what were you trying to do? + optional contact
email for anonymous residents) that stores an `ErrorReport` row + one
`AccessAudit` row and renders a "Thanks" confirmation; and (2) a
**`/admin/error-reports` GlobalAdmin surface** lists the reports
newest-first, shows the resident's description + the exception type
(captured from the `IExceptionHandlerFeature`) + the request ID (for log
correlation), and lets the admin mark a report as **triaged** (one
`AccessAudit` row per effective triage, idempotent).

The boundary with M32 is explicit and pinned: **M31 is intake + triage** —
the resident says what went wrong, the admin acknowledges it. **M32 is
resolution + escalation** — the admin fixes it locally (marks it `resolved`
with a resolution note) or forwards it to a configurable endpoint, and a
resident may file a *general issue* not tied to an error page. M31 does
**not** ship any of that: no `resolved` status, no escalation forwarding, no
general issue-submission lane, no per-report detail view with a resolution
note.

## Scope

**In (M31's closed surface):**

- the `ErrorReport` **non-singleton doc** (one row per report; a new
  `Kumunita.Core.ErrorReports` bounded context) — the 11-member ceiling
  (`Id` / `SubjectId` / `Description` / `ContactEmail?` / `RequestId` /
  `ExceptionType?` / `UserAgent?` / `Created` / `TriageStatus` /
  `TriagedAt?` / `TriagedBy?`), `TriageStatus` **closed in M31** to
  `{"new", "triaged"}`; the row-per-event POCO shape mirrored from
  `Usage/UsageEvent` (the `Id` is conventional string; the field set is the
  register's Assumptions, **not** the `UsageEvent` 4-field set);
- the `IErrorReportService` + `ErrorReportService` (the `CreateAsync` +
  `MarkTriagedAsync` audited write-lanes + the `ListAsync` read — the ADR
  0006 C3 single-write-lane shape, one `AccessAudit` row per effective
  write);
- the `ErrorReportDocTypes` (the ADR 0004 §B.1 additive doc type, the
  `UsageDocTypes` parallel surface — one `.Schema.For<ErrorReport>()` call,
  the `(TriageStatus, Created)` index for the admin list ordering;
  **no EF migration**);
- the DI registration (`IErrorReportService` → `ErrorReportService`) + the
  `ErrorReportDocTypes.Configure(opts);` call next to the existing
  `*DocTypes` block in `Program.cs` (the `UsageDocTypes` line 225 neighbor);
- the **enhanced `/Home/Error` action** (reads the exception from
  `IExceptionHandlerFeature`, populates `ExceptionType` + `ExceptionMessage`
  + the `RequestId`) + the **enhanced `ErrorViewModel`** (5 new members) +
  the **enhanced `Error.cshtml`** (the report form + the post-submission
  confirmation, rendered **below** the existing content, **not** inside an
  `[Authorize]` gate);
- the **`POST /Home/Error/Report`** submission action (binds
  `Description` + `ContactEmail`, validates non-blank description, calls
  `IErrorReportService.CreateAsync`, re-renders with the confirmation — a
  blank description is a form-level 400 re-render, never a 500);
- the **`/admin/error-reports` GlobalAdmin surface** — the
  `[Authorize(Roles = GlobalAdmin)]` controller (the
  `AdminOnboardingController` shape), `GET /admin/error-reports` (the
  `ListAsync` read, newest-first, **no per-row `IAuthorizationService`
  call, no audit on reads**) + `POST /admin/error-reports/{id}/triage`
  (the idempotent `MarkTriagedAsync`), the `AdminErrorReportViewModel`, and
  `Views/Admin/ErrorReports/Index.cshtml`;
- the **closed `errorreport.*` `kw-l` key set** × en/de/fr/da (~20 keys,
  the exact set pinned in Part 2 §2.3 — authored by U05, consumed by U04's
  error page + U05's admin views); and
- the test pins (the `ErrorReportServiceTests` / `ErrorReportPageTests` /
  `AdminErrorReportTests` classes — the 16 pinned names, Part 2 §2.4).

**Out (named deferrals for M32):**

- **The general issue-submission lane** — a resident files a general issue
  *not tied to an error page* (a dedicated affordance, its own surface; M31's
  form lives only on the 500 page).
- **The local resolution lane** — the admin marks a report `resolved` with a
  resolution note (the `resolved` status is M32's; M31's `TriageStatus`
  value set is **closed** to `{"new", "triaged"}`, and M32 **adds**
  `"resolved"` additively per ADR 0004 §B.1 — it does not re-shape the
  existing two).
- **The escalation forwarding lane** — forwarding a report to a configurable
  endpoint (an env-var URL); M31 never sends mail from `ContactEmail` either
  (the field is stored + read by the admin only).
- **The per-report detail view with a resolution note** — M31's admin
  surface is the list + the mark-as-triaged action; a detail page is M32's.
- **The `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip** until the
  milestone *ships* (U07 owns it — the `WhatsNew.cs` `0.47.0` entry is
  appended by U07, not by an earlier unit).

## Invariants (pinned for M31)

- **M31·1 — The `ErrorReport` is a platform-error signal, not a
  content-moderation report.** The doc lives in a **new bounded context**
  (`Kumunita.Core.ErrorReports`), separate from `Kumunita.Core.Posts`. The
  existing `Posts/Report` doc (the M3b content-moderation report, ADR 0023)
  is **untouched** — M31 does not add fields to it, does not reuse it, does
  not change its registration. The two report concepts are **separate docs
  in separate contexts** and never mix.
- **M31·2 — The report form is on the 500 error page, always available,
  non-blocking.** The form is rendered on `/Home/Error` (the
  `UseExceptionHandler` target). It is **not gated** — an anonymous visitor
  who hits a 500 can file a report (no login requirement). Submitting the
  form **never changes the error page's rendering** or the error's outcome;
  it is an optional affordance, not a gate. The confirmation ("Thanks, your
  report was filed") is the only feedback; there is no redirect, no modal,
  no blocking step.
- **M31·3 — The `ErrorReport` doc is Marten-native, registered in a new
  parallel surface.** The doc is a POCO with a conventional string `Id` (the
  `Posts/Report` / `Usage/UsageEvent` row-per-event shape), registered in a
  new `ErrorReportDocTypes.Configure(opts)` surface (the ADR 0004 §B.1
  parallel-surface pattern, the `M3DocTypes` / `M4DocTypes` / `M6DocTypes`
  precedent). **No EF migration.** The delta is applied idempotently at boot
  (the `SchemaBootstrap` + the dev-loop `Program.cs` path both pick it up).
- **M31·4 — The admin list is a GlobalAdmin-gated read.** The
  `/admin/error-reports` surface is `[Authorize(Roles = GlobalAdmin)]` (the
  standard admin gate, the thin-token rule ADR 0001-B). The list is a
  **read-only** listing — **no per-row `IAuthorizationService` call**, no
  `AccessAudit` row on reads (a read, not an access decision). A
  non-`GlobalAdmin` (including a signed-in resident) gets a 403.
- **M31·5 — The report submission is a single-write-lane.** One
  `IErrorReportService.CreateAsync(draft)` stores the `ErrorReport` row +
  writes exactly **one** `AccessAudit` row (the `Via` tag is **U01/U02's
  design decision** — the design doc §2.1 pins it; the repo precedent
  (ADR 013/028/036/041) is an **additive** `AccessVia` enum append when a new
  standing has no existing tag, or an existing value if one fits — the
  eleven frozen values are never re-shaped; action `errorreport.create`;
  `TargetKind` "error-report"; the `AccessOutcome.Allow` tag). The write is
  **idempotent-safe** (a double-submit creates two rows — the admin sees
  both; the dedup is the admin's judgment call, not a system constraint).
  The write never throws a 500 back to the resident (a form-level validation
  error is a 400 re-render, not a crash).
- **M31·6 — The triage action is a GlobalAdmin-gated idempotent
  single-write-lane.** One `IErrorReportService.MarkTriagedAsync(reportId,
  actorId)` stamps `TriageStatus = "triaged"`, `TriagedAt = now`,
  `TriagedBy = actorId` + writes exactly **one** `AccessAudit` row
  (`Via = Admin`; action `errorreport.triage`; `TargetKind` "error-report").
  The triage is **idempotent** — marking an already-`triaged` report as
  triaged is a no-op (no second audit row, no state change). The
  `TriageStatus` value set is **closed in M31** to `{"new", "triaged"}`;
  `resolved` is M32's.
- **M31·7 — The closed `errorreport.*` `kw-l` key set is parity-pinned in
  four languages.** Every new user-visible string M31 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The closed set (~20 keys, the exact
  list in Part 2 §2.3) is authored by U05 and consumed by U04's error page
  + U05's admin views.
- **M31·8 — The six-member close flip is U07's responsibility.** The
  `Milestones.cs` / README / `STATUS.md` / `ARCHITECTURE.md` /
  `MilestonesTests.cs` / `WhatsNew.cs` six-member close flip lands in U07.
  The `WhatsNew.cs` registry gains one new entry (newest-first, the `0.47.0`
  row) naming M31 + ADR 0154 — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.
- **M31·9 — No new authorization surface.** No new `AccessAction`, no new
  `Decide()` branch, no new `IAuthorizationService` surface. The `Via` tag
  on the `AccessAudit` row for the report-submission lane is **U01/U02's
  design decision** (the design doc §2.1 pins it; the repo precedent, ADR
  013/028/036/041, is an additive `AccessVia` enum append when a new standing
  has no existing tag — the eleven frozen values are never re-shaped). The
  error page is **public** (no authz gate — the 500 page is the one place in
  the app where an anonymous visitor sees a product surface). The admin
  surface is `[Authorize(Roles = GlobalAdmin)]` (the standard admin gate, the
  `AdminOnboardingController` / `AdminSurfaceLabelsController` shape). The
  thin-token rule (ADR 0001-B) is held.
- **M31·10 — The existing `Posts/Report` doc is untouched.** M31 does not add
  fields to `Posts/Report`, does not change its `Status` vocabulary, does not
  alter its `M3DocTypes` registration, does not touch the M3b moderation
  queue. The two report concepts (platform-error vs content-moderation) are
  **separate docs in separate contexts** and the `TargetKind` strings are
  distinct (`"error-report"` vs `"post"` / `"reply"`).

## FACES (pinned, 8)

- **M31-1 — An anonymous visitor hits a 500, sees the report form, submits
  it.** An `ErrorReport` row is stored (`SubjectId = ""`, the anonymous
  shape) + exactly one `AccessAudit` row (the `Via` tag is Part 2 §2.1's pin)
  + the "Thanks" confirmation is visible — no redirect, no modal (M31·2,
  M31·5).
- **M31-2 — A signed-in resident hits a 500 and submits the form.** The
  `ErrorReport` row is stored with `SubjectId` set to the resident's
  `ClaimTypes.Subject` value + exactly one `AccessAudit` row (`Via =
  Resident`, ADR 0041) (M31·2, M31·5).
- **M31-3 — A GlobalAdmin visits `/admin/error-reports`.** They see all
  `ErrorReport` rows, newest first, each row showing status / exception
  type / description / reporter / created / request ID — a read-only listing,
  no per-row `IAuthorizationService` call, no audit row (M31·4).
- **M31-4 — A GlobalAdmin marks a `new` report as triaged.**
  `TriageStatus = "triaged"` / `TriagedAt = now` / `TriagedBy = actorId` are
  stamped + exactly one `AccessAudit` row (`Via = Admin`, action
  `errorreport.triage`, `TargetKind` "error-report") (M31·6).
- **M31-5 — A GlobalAdmin marks an already-`triaged` report as triaged.**
  No-op — no second audit row, no state change (M31·6).
- **M31-6 — A non-`GlobalAdmin` (including a signed-in resident) visits
  `/admin/error-reports`.** They get a 403 — the standard admin gate
  (`[Authorize(Roles = GlobalAdmin)]`), the thin-token rule (M31·4, M31·9).
- **M31-7 — An anonymous visitor hits a 500 and does *not* submit the form.**
  The error page renders unchanged (the form is an optional affordance, not
  a gate; the existing "Something went wrong" + request-ID content is
  intact) (M31·2).
- **M31-8 — The report form's labels resolve per request language.** The
  `errorreport.title` / `errorreport.intro` / `errorreport.description.*` /
  `errorreport.email.*` / `errorreport.submit` / `errorreport.thanks` /
  `errorreport.list.*` `kw-l` keys resolve through the existing `kw-l`
  TagHelper chain; every key is present, non-empty, in en/de/fr/da (M31·7).

---

*Part 2 (U02) appends: the `IErrorReportService` seam contract (exact C#),
the `ErrorReport` field set (the 11-member ceiling), the `ErrorReportDraft`
record, the `ErrorReportDocTypes` registration shape, the `AccessAudit`
`Via` pin for the submission row, the closed `errorreport.*` `kw-l` key set,
the 16 pinned test names, the acceptance gate, the drift guard — plus
**ADR 0154**.*

---

## Seams & contracts (Part 2, written by U02)

> **Authoritative for U03–U06.** These shapes are the copy-paste-exact C# the
> Core/Web units implement against. Any later unit that finds a mismatch
> between the code it is about to write and a pin in §2.1–§2.6 records a
> `## U<m> — Drift pause` (unit-series rule §9) instead of improvising. The
> pinned C# below is the **seam contract** — U03 implements it verbatim; U04
> and U05 consume it; U06's tests assert against it.

### 2.1 frozen seam list (exact C#)

The single new service seam. **Three methods** — the audited write-lanes
(`CreateAsync`, `MarkTriagedAsync`) + the read-lane (`ListAsync`). This is
the **only** new Core seam M31 adds (M31·9 — no new `AccessAction`, no new
`Decide()` branch, no new `IAuthorizationService` surface).

```csharp
namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The input to <see cref="IErrorReportService.CreateAsync"/> — the six
/// members the Web layer binds from the 500 error page (the resident's
/// subject + description + optional contact email) and the request context
/// (request ID + exception type + user agent). Never a doc; never stored
/// directly (CreateAsync projects it onto an ErrorReport row).
/// </summary>
public sealed record ErrorReportDraft(
    string SubjectId,          // ClaimTypes.Subject; string.Empty when anonymous
    string Description,        // required — the resident's free-text "what were you doing"
    string? ContactEmail,      // optional — for anonymous follow-up; never mailed in M31
    string RequestId,          // the HttpContext.TraceIdentifier, for log correlation
    string? ExceptionType,     // from IExceptionHandlerFeature; null if the feature is absent
    string? UserAgent);        // the browser UA, truncated to 256 chars by the caller

/// <summary>
/// The read + two audited-write lanes for the ErrorReport doc (ADR 0154).
/// The ADR 0006 C3 single-write-lane shape: each write opens one write
/// session that commits the ErrorReport doc + exactly one AccessAudit row
/// together (invariant C3, strong consistency). Core stays HTTP-free
/// (ADR 0006-D); the Web layer is the only place the subject / request
/// context are produced.
/// </summary>
public interface IErrorReportService
{
    /// <summary>
    /// Store one ErrorReport row (TriageStatus "new") + exactly one
    /// AccessAudit row in one write session. The Via tag is the §2.1 pin:
    /// AccessVia.Resident for a non-blank SubjectId; AccessVia.Anonymous
    /// (the new additive value, below) for a blank SubjectId. Never a
    /// 500 back to the resident — a form-level validation error is the
    /// Web layer's 400 re-render, not this lane.
    /// </summary>
    Task<ErrorReport> CreateAsync(ErrorReportDraft draft);

    /// <summary>
    /// Stamp a new report triaged (TriageStatus / TriagedAt / TriagedBy)
    /// + exactly one AccessAudit row (Via = Admin) in one write session.
    /// Returns null (a no-op — no audit row, no state change) when the
    /// report is missing or already triaged (M31·6 idempotency pin).
    /// </summary>
    Task<ErrorReport?> MarkTriagedAsync(string reportId, string actorId);

    /// <summary>
    /// Newest-first listing for the admin surface. A read, not an access
    /// decision — no per-row IAuthorizationService call, no AccessAudit
    /// row (M31·4). Returns an empty list when there are no reports.
    /// </summary>
    Task<IReadOnlyList<ErrorReport>> ListAsync(int maxCount = 100);
}
```

> **Note on `CancellationToken`.** The pinned signatures above match the
> register's §2.1 seam list verbatim (no `CancellationToken`), so U06's
> seam tests and any call-site compile against this exact surface. U03 **may**
> add a trailing optional `CancellationToken ct = default` to each method
> (the `AdminOnboardingService` / `SiteContentService` house style) — that is
> a **superset** that does not change the pinned call-site shape, so it is not
> a drift. It is the *only* freedom U03 has on this surface; the method
> names, parameter names, return types, and the `maxCount = 100` default are
> frozen.

**The `AccessAudit` `Via` pin for the `CreateAsync` audit row (the single
decision this §2.1 owns, M31·5 / M31·9):**

- **Non-blank `SubjectId` (a signed-in resident):** `Via = AccessVia.Resident`
  (ADR 0041 — the all-residents standing; the actor is signed in, a non-empty
  `SubjectId`). This is the FACES M31-2 shape.
- **Blank `SubjectId` (an anonymous visitor):** `Via = AccessVia.Anonymous`
  — a **new additive** `AccessVia` enum value, appended **after**
  `Resident` in `src/Kumunita.Core/Authorization/Decision.cs`, with a doc
  comment citing ADR 0154 + the ADR 013/028/036/041 append precedent. The
  eleven frozen `AccessVia` values (Owner, Audience, Delegation, Moderator,
  Report, BreakGlass, Admin, Group, Guardian, Community, Resident) are
  **never re-shaped**; `Anonymous` is the 12th.

**Why a new value (not an existing one):** none of the eleven frozen values
fits the "unsigned visitor filing a platform-error signal" standing — `Resident`
explicitly requires a *signed-in* actor (ADR 0041), `Admin`/`Guardian`/`Group`/
`Community`/`Moderator`/`Owner`/`Delegation`/`Audience`/`BreakGlass`/`Report`
each encode a standing the anonymous submitter does not hold. The repo
precedent for exactly this situation (a new standing with no existing tag) is
an additive `AccessVia` append — the M1 `Admin` (ADR 0006), ADR 0013 `Group`,
ADR 0028 `Guardian`, ADR 0036 `Community`, ADR 0041 `Resident` all followed it.
This is **permitted** by the register (M31·9 allows a new `AccessVia` value
*only* when §2.1 pins it — and this §2.1 does) and is **not** a new
authorization surface (no `AccessAction`, no `Decide()` branch, no
`IAuthorizationService` method — the `AccessVia` enum is the *record* of
"by what right," not a *gate*).

**The frozen `IAuthorizationService` 4-method surface (unchanged, M31·9):**

```csharp
// src/Kumunita.Core/Authorization/IAuthorizationService.cs — UNCHANGED in M31.
// M31 adds NO method to this surface. Pinned here so U03 does not reach for it.
public interface IAuthorizationService
{
    Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target);
    Task<Decision> CanAsync(string actorId, AccessAction action, IAuditableResource target, IDocumentSession session);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates);
    Task<VisibleSet> CanSeeAsync(string actorId, AccessAction action, IEnumerable<IAuditableResource> candidates, IDocumentSession session);
}
```

The `AccessAudit` doc the write-lanes emit (the real shape, `AccessAudit.cs`)
is **unchanged** — M31 only populates it, it does not re-shape it. The
`CreateAsync` audit row uses the `SiteContentService.SaveAsync` /
`AdminOnboardingService.CompleteAsync` field set: `Id =
Guid.NewGuid().ToString("N")`, `At = DateTimeOffset.UtcNow`, `ActorId =` the
`SubjectId` (blank when anonymous), `EffectivePrincipalId =` the `SubjectId`,
`Action = "errorreport.create"`, `TargetKind = "error-report"`,
`TargetId =` the new report's `Id`, `Via =` the §2.1 pin (Resident or
Anonymous), `Outcome = AccessOutcome.Allow`. The `MarkTriagedAsync` audit row
is identical in shape with `Action = "errorreport.triage"`, `ActorId =`
`actorId`, `Via = AccessVia.Admin`.

### 2.2 new M31-owned Core types (exact C#)

**The `ErrorReport` doc — the 11-member ceiling (M31·3, ADR 0154 D1).**
No field outside this set may appear in the doc; no field in the set may be
dropped. This is the **field ceiling** the U03 drift pin
(`M31_3_ErrorReport_Doc_FieldSet_Ceiling`) asserts.

```csharp
namespace Kumunita.Core.ErrorReports;

/// <summary>
/// One platform-error report row (ADR 0154, M31·1 — a platform-error
/// signal, NOT a content-moderation report; the Posts/Report doc, ADR 0023,
/// is untouched). One row per report (the Usage/UsageEvent row-per-event
/// shape; the Id is the conventional string identity, Marten-generated).
/// The field set below is the 11-member ceiling (D1) — no field outside
/// this set may appear in the doc.
/// </summary>
public sealed class ErrorReport
{
    public string Id { get; set; } = string.Empty;

    /// <summary>The report's subject: ClaimTypes.Subject; string.Empty when anonymous.</summary>
    public string SubjectId { get; set; } = string.Empty;

    /// <summary>The resident's free-text "what were you trying to do" — required, non-blank.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Optional contact email (anonymous follow-up); never mailed in M31.</summary>
    public string? ContactEmail { get; set; }

    /// <summary>The HttpContext.TraceIdentifier, for log correlation.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>The exception type (e.g. NullReferenceException); null if IExceptionHandlerFeature is absent.</summary>
    public string? ExceptionType { get; set; }

    /// <summary>The browser UA, truncated to 256 chars by the caller; null if absent.</summary>
    public string? UserAgent { get; set; }

    /// <summary>The report's creation instant, UTC.</summary>
    public DateTimeOffset Created { get; set; }

    /// <summary>
    /// The triage state. CLOSED IN M31 to {"new", "triaged"} (D2). M32
    /// adds "resolved" additively (ADR 0004 §B.1 — a string field, no
    /// migration); M32 does not re-shape these two.
    /// </summary>
    public string TriageStatus { get; set; } = "new";

    /// <summary>The triage instant, UTC; null until triaged.</summary>
    public DateTimeOffset? TriagedAt { get; set; }

    /// <summary>The GlobalAdmin's ClaimTypes.Subject who triaged; null until triaged.</summary>
    public string? TriagedBy { get; set; }
}
```

**The `IErrorReportService` + `ErrorReportDraft`** — pinned verbatim in §2.1
above (the §2.1 interface is the canonical source; §2.2 does not re-state it).

**The `ErrorReportService` write-lane shapes** (U03 implements these; the
`AdminOnboardingService.CompleteAsync` / `SiteContentService.SaveAsync`
single-write-lane shape is the precedent — one write session, doc + exactly
one `AccessAudit` row, `SaveChangesAsync`):

```csharp
using Kumunita.Core.Authorization;
using Marten;

namespace Kumunita.Core.ErrorReports;

/// <summary>
/// The read + two audited-write lanes for the ErrorReport doc (ADR 0154).
/// Composes the host-registered Marten IDocumentStore (the
/// AdminOnboardingService / SiteContentService shape) — reads open their
/// own QuerySession; each audited write opens one write IDocumentSession
/// and commits the doc + its single AccessAudit row together (C3).
/// </summary>
public sealed class ErrorReportService : IErrorReportService
{
    private readonly IDocumentStore _store;

    public ErrorReportService(IDocumentStore store) => _store = store;

    /// <summary>Store one ErrorReport row (new) + exactly one AccessAudit row (one session).</summary>
    public async Task<ErrorReport> CreateAsync(ErrorReportDraft draft)
    {
        var report = new ErrorReport
        {
            Id           = Guid.NewGuid().ToString("N"),
            SubjectId    = draft.SubjectId,
            Description  = draft.Description,
            ContactEmail = draft.ContactEmail,
            RequestId    = draft.RequestId,
            ExceptionType= draft.ExceptionType,
            UserAgent    = draft.UserAgent,
            Created      = DateTimeOffset.UtcNow,
            TriageStatus = "new"             // the M31 floor; TriagedAt/TriagedBy stay null
        };

        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        session.Store(report);
        // Exactly one AccessAudit row (the §2.1 Via pin) — the SiteContentService
        // / AdminOnboardingService field set. Via = Resident (non-blank
        // SubjectId) or Anonymous (blank SubjectId, the new additive value).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = draft.SubjectId,
            EffectivePrincipalId = draft.SubjectId,
            Action               = "errorreport.create",
            TargetKind           = "error-report",
            TargetId             = report.Id,
            Via                  = string.IsNullOrEmpty(draft.SubjectId)
                                       ? AccessVia.Anonymous   // §2.1 pin (new additive value)
                                       : AccessVia.Resident,    // §2.1 pin (ADR 0041)
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync();
        return report;
    }

    /// <summary>
    /// Stamp a new report triaged + exactly one AccessAudit row (one
    /// session). Returns null (a no-op — no audit row, no state change)
    /// when the report is missing or already triaged (M31·6).
    /// </summary>
    public async Task<ErrorReport?> MarkTriagedAsync(string reportId, string actorId)
    {
        await using var session = _store.OpenSession(new Marten.Services.SessionOptions());
        var stored = await session.LoadAsync<ErrorReport>(reportId);
        if (stored is null || stored.TriageStatus == "triaged")
        {
            return null; // no-op — a missing or already-triaged report (M31·6)
        }
        stored.TriageStatus = "triaged";
        stored.TriagedAt    = DateTimeOffset.UtcNow;
        stored.TriagedBy    = actorId;
        session.Store(stored);
        // Exactly one AccessAudit row (Via = Admin — the §2.1 pin).
        session.Store(new AccessAudit
        {
            Id                   = Guid.NewGuid().ToString("N"),
            At                   = DateTimeOffset.UtcNow,
            ActorId              = actorId,
            EffectivePrincipalId = actorId,
            Action               = "errorreport.triage",
            TargetKind           = "error-report",
            TargetId             = reportId,
            Via                  = AccessVia.Admin,
            Outcome              = AccessOutcome.Allow
        });
        await session.SaveChangesAsync();
        return stored;
    }

    /// <summary>Newest-first listing (Created DESC); a read, never audited (M31·4).</summary>
    public async Task<IReadOnlyList<ErrorReport>> ListAsync(int maxCount = 100)
    {
        using var session = _store.QuerySession();
        return await session.Query<ErrorReport>()
            .OrderDescending(x => x.Created)
            .Take(maxCount)
            .ToListAsync();
    }
}
```

**The `ErrorReportDocTypes` registration surface** (ADR 0004 §B.1, the
`UsageDocTypes` / `M3DocTypes` parallel-surface shape — the `UsageDocTypes`
is the closest analog, a row-per-event surface). One `.Schema.For<ErrorReport>()`
call + the `(TriageStatus, Created)` index for the admin list ordering (the
`WHERE TriageStatus = ? ORDER BY Created DESC` query).

```csharp
using Kumunita.Core.ErrorReports;
using Marten;

namespace Kumunita.Core;

/// <summary>
/// The M31 error-report bounded context's Marten-native document
/// registration surface (ADR 0004 §B.1, the UsageDocTypes / M3DocTypes
/// parallel-surface shape). ErrorReport uses the conventional string Id
/// identity (Marten default), so only the (TriageStatus, Created) index
/// for the admin list ordering needs pinning.
/// </summary>
public static class ErrorReportDocTypes
{
    public static void Configure(StoreOptions opts)
    {
        opts.Schema.For<ErrorReport>(o =>
        {
            o.Index(x => x.TriageStatus);
            o.Index(x => x.Created);
        });
    }
}
```

**Wiring (U03's one-line additions):** `DependencyInjection.cs` gains
`services.AddTransient<IErrorReportService, ErrorReportService>();` (the
`IUsageAnalyticsService` → `UsageAnalyticsService` registration shape);
`Program.cs` gains `ErrorReportDocTypes.Configure(opts);` next to the
existing `*DocTypes` block (the `UsageDocTypes` line-225 neighbor). The
`SchemaBootstrap` versioned boot path picks the new surface up
automatically (the `*DocTypes` surfaces are all registered there, the
`UsageDocTypes` precedent).

### 2.3 the closed `errorreport.*` `kw-l` key set

The **20 keys** (verbatim from the register's Assumptions; the exact set is
**frozen** by the §2.6 drift guard). Each with its **en** value — the source
text, the ADR 0015 D1 `kw-l` provider-floor discipline. The `de` / `fr` / `da`
values are **U05's** to author (the M30·6 four-language pin; the
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
asserts every key is present, non-empty, in all four languages, M31·7).

| # | Key | en value (source text) |
|---|-----|------------------------|
| 1  | `errorreport.title` | Tell us what went wrong |
| 2  | `errorreport.intro` | If something went wrong, let us know what you were trying to do so we can look into it. |
| 3  | `errorreport.description.label` | What were you trying to do? |
| 4  | `errorreport.description.placeholder` | e.g. I was trying to sign up for the Saturday event and the page went blank |
| 5  | `errorreport.email.label` | Your email (optional) |
| 6  | `errorreport.email.placeholder` | you@example.com |
| 7  | `errorreport.submit` | Send report |
| 8  | `errorreport.thanks` | Thanks — your report has been filed. |
| 9  | `errorreport.list.title` | Error reports |
| 10 | `errorreport.list.empty` | No error reports yet. |
| 11 | `errorreport.list.status.new` | New |
| 12 | `errorreport.list.status.triaged` | Triaged |
| 13 | `errorreport.list.mark_triaged` | Mark as triaged |
| 14 | `errorreport.list.request_id` | Request ID |
| 15 | `errorreport.list.exception` | Exception |
| 16 | `errorreport.list.description` | Description |
| 17 | `errorreport.list.reporter` | Reporter |
| 18 | `errorreport.list.created` | Reported |
| 19 | `errorreport.list.anonymous` | Anonymous |
| 20 | `errorreport.list.flash_triaged` | Report marked as triaged. |

> **Key-set note.** The error-page form consumes keys 1–8 (`errorreport.title`
> through `errorreport.thanks`); the admin list view consumes keys 9–20
> (`errorreport.list.*`). The form's "Thanks" confirmation is key 8 (the
> `Model.FormSubmitted` branch renders key 8 in place of the form, keys 1–7).
> The `errorreport.list.anonymous` key (19) is the display value for a blank
> `SubjectId` in the admin list's reporter column.

### 2.4 pinned seam tests (exact names)

The **16 tests** across three files (U06 implements one test per pinned name;
U06's `## U06` handoff section lists the 16 verbatim so U07 can confirm). The
test **names** are frozen (the §2.6 drift guard); the test **bodies** are
U06's.

**`tests/Kumunita.Core.Tests/ErrorReportServiceTests.cs` — 8 tests:**

1. `M31_1_CreateAnonymous_Stores_ErrorReport_And_AuditRow`
2. `M31_2_CreateSignedIn_Stores_SubjectId_And_AuditRow`
3. `M31_5_Create_Writes_ExactlyOne_AccessAuditRow`
4. `M31_6_MarkTriaged_New_Updates_TriageStatus_And_AuditRow`
5. `M31_6_MarkTriaged_AlreadyTriaged_Is_NoOp`
6. `M31_6_MarkTriaged_Writes_ExactlyOne_AccessAuditRow`
7. `M31_4_List_Returns_All_Reports_NewestFirst`
8. `M31_3_ErrorReport_Doc_FieldSet_Ceiling`

**`tests/Kumunita.Web.Tests/ErrorReportPageTests.cs` — 4 tests:**

9. `M31_2_Error_Page_Shows_Report_Form`
10. `M31_5_Error_Report_Post_Creates_ErrorReport`
11. `M31_5_Error_Report_Post_Validation_BlankDescription_Renders_Error`
12. `M31_2_Error_Report_Post_Confirmation_Visible`

**`tests/Kumunita.Web.Tests/AdminErrorReportTests.cs` — 4 tests:**

13. `M31_4_Admin_List_SignedIn_GlobalAdmin_Sees_Reports`
14. `M31_4_Admin_List_NonGlobalAdmin_Denied`
15. `M31_6_Admin_MarkTriaged_GlobalAdmin_Updates_Row`
16. `M31_6_Admin_MarkTriaged_AlreadyTriaged_NoOp`

### 2.5 acceptance gate (U06 records)

Three-test shape (the three are the **parts**; the 16 in §2.4 are the
**whole**; all must pass together, the part-vs-whole pin):

- **closed-loop** — an anonymous visitor hits a 500, submits the report
  form, the `ErrorReport` row (`SubjectId = ""`, `TriageStatus = "new"`) +
  exactly one `AccessAudit` row (`Via = AccessVia.Anonymous`, action
  `errorreport.create`, `TargetKind` "error-report") exist, and the
  `errorreport.thanks` confirmation is visible. (Covers M31-1 + M31·5.)
- **handoff** — a `GlobalAdmin` marks the report `triaged` via
  `POST /admin/error-reports/{id}/triage`, `TriageStatus` / `TriagedAt` /
  `TriagedBy` are stamped, exactly one `AccessAudit` row (`Via = Admin`,
  action `errorreport.triage`) is written, and a second `POST` to the same
  report is a no-op (no second audit row). (Covers M31-4 / M31-5 + M31·6.)
- **part-vs-whole** — the 16-test list in §2.4 is the **whole**; the
  closed-loop + handoff are the **parts**; the gate passes only when all
  16 are green together (a single red in any of the 16 fails the gate, even
  if the closed-loop + handoff parts are green).

U06 appends `### Run result (M31 acceptance gate — <date>)` to this design
doc: the three gate test names, their pass/red status, the 16-test count
(16/16 expected), and one line per any `## U<m> — Drift pause` section in the
handoff note (each resolved or still open).

### 2.6 drift-guard (frozen once written)

The following are **frozen pins**; any mismatch found by a later unit is a
`## U<m> — Drift pause` (unit-series rule §9), not a silent fix:

- **The 10 invariants** — M31·1 through M31·10 (Part 1 §Invariants).
- **The 8 FACES** — M31-1 through M31-8 (Part 1 §FACES).
- **The `IErrorReportService` 3-method surface** — the exact signatures in
  §2.1 (`CreateAsync(ErrorReportDraft)`, `MarkTriagedAsync(string, string)`,
  `ListAsync(int = 100)`).
- **The `ErrorReport` doc field set** — the 11-member ceiling in §2.2 (no
  field outside the set may appear in the doc; no field in the set may be
  dropped).
- **The `ErrorReportDraft` record shape** — the 6 positional members in §2.1
  (`SubjectId`, `Description`, `ContactEmail`, `RequestId`, `ExceptionType`,
  `UserAgent`).
- **The `ErrorReportDocTypes` registration shape** — one
  `.Schema.For<ErrorReport>()` + the `(TriageStatus, Created)` index (§2.2).
- **The `AccessAudit` `Via` pin** — `AccessVia.Resident` (non-blank
  `SubjectId`) / `AccessVia.Anonymous` (blank `SubjectId`, the new additive
  value) for the `CreateAsync` row; `AccessVia.Admin` for the
  `MarkTriagedAsync` row (§2.1).
- **The §2.3 `kw-l` key set** — the 20 keys, verbatim (the exact set is
  frozen; the en values are the source text; the de/fr/da values are U05's).
- **The 16 test names** — §2.4, verbatim (the names are frozen; the bodies
  are U06's).
- **The M32 boundary** — no `resolved` triage status, no escalation
  forwarding lane, no general issue-submission lane, no per-report detail
  view with a resolution note (Part 1 §Scope Out). The `Posts/Report` doc
  (ADR 0023) is **untouched** (M31·10).

---

*Part 2 (U02) end. The ADR 0154 (`docs/adr/0154-production-error-handling.md`,
`Status: Draft`) is the companion document — it names the Decision /
Consequences that this design doc pins in detail. The ADR index
(`docs/adr/README.md`) gains the 0154 row (`Status: Draft`).*
