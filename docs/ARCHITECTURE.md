---
id: architecture-hub
title: "Kumunita — Architecture"
tier: reference
kind: architecture
status: current
audiences: [developer, agent, platform-maintainer, community-admin]
related:
  - docs/architecture/solution-layout.md
  - docs/architecture/identity-access.md
  - docs/architecture/data-model.md
  - docs/architecture/cqrs-lite-wolverine.md
  - docs/architecture/localization.md
  - docs/architecture/extension-seams.md
  - docs/adr/0001-core-stack-and-identity-model.md
  - docs/philosophy/START-HERE.md
updated: 2026-11-02
---

# Kumunita — Architecture

> **Abstract:** the technical reference for how Kumunita is built — the
> relationship to the philosophy, the technology stack, the map of the
> deeper sections (each its own doc under [`architecture/`](architecture/)),
> and the small sections kept inline: the modular-monolith layout, the
> front-end, deployment & configuration, and testing. **Who is it for:** a
> developer or agent who needs the system shape before diving into a
> section. **When to read the rest:** the section you need — pick it
> from the table below.


Kumunita is a self-hosted community platform. One deployment serves exactly one
neighborhood; each community runs its own instance with its own Postgres. There is no
multi-tenant data model. This is the technical reference. Rationale lives in `docs/adr/`;
the public summary lives in `README.md`; the security & privacy threat model (the top
priority of this product) lives in `docs/SECURITY.md`; and the *why* — the development
philosophy behind all of it — lives in `docs/philosophy/`.

## Relationship to the philosophy

This document describes **how** the system is built; [`docs/philosophy/`](philosophy/)
describes **why**. The philosophy's one claim — that the platform's value is the *linkage*
between a neighborhood's differentiated parts, not the count of parts — is the lens this
architecture is organized through. Two concrete mappings are worth keeping in view:

- **The value chain drives the module order.** The roadmap is an ascent up the value
  chain from [`docs/philosophy/the-platform-as-integrator.md`](philosophy/the-platform-as-integrator.md)
  (signal → shared awareness → understanding → decision → coordination → outcome):

  | Milestone | Value-chain step it adds |
  |---|---|
  | **M0** scaffold | the substrate — no value yet, just a place the rest can link into |
  | **M1** identity, groups, delegation, authorization | the **access model** — the linkage that turns *signals* into *shared awareness* for the right audience |
  | **M2** directory & profiles | **shared awareness** — every resident is listed; each opt-in contact block is audience-gated |
  | **M3** posts, components, moderation | **understanding → decision** — a signal reaches its audience; a report links to a moderator |
  | **M4** events, RSVP, reminders | **coordination** — a decision becomes an owned, scheduled, reminded action |
  | **M5** projects (goals, tasks, contributors) | **coordination → outcome** — many signals re-linked into one goal with owners |
  | **M6** notifications | **shared awareness** — the platform reaches out to the resident where they are |
  | **M7** pagination and filtering | **navigation** — a growing archive stays browsable |
  | **M8** search | **understanding** — finding what already exists in the neighborhood's memory |
  | **M9** messaging | **shared awareness ↔ understanding** — a direct 1:1 channel between residents; a signal finds its recipient without the public surface |
  | **M10** PWA and responsive design | **portability of the surface** — the same platform in the resident's pocket |
  | **M11** portability (import/export) | **outcome + world seams** — the loop closes *into* the residents' lives |
  | **M12** iCal | **outcome + world seams** — events land in the calendars residents already check |
  | **M13** logging and analytics | **feedback** — the operator sees how the platform is used |
  | **M14** integration of Events and Projects | **coordination** — the two coordination surfaces interlock |
  | **M15** translation bulk | **world seams + coordination** — the platform's translations leave and re-enter the platform as one batch; a new language extends the set in the same loop |
  | **M16** inventory | **coordination** — the neighborhood's shared, community-owned, and private things become trackable: check-out / check-in, where they are, and who has used them |
  | **M17** bookmarks | **personal quick access** — the resident's own personal list of saved posts, events, todos, announcements, pages, **projects, and boards**, **organizable** into a personal folder tree (depth ≤ 2, unique name per owner's tree; a folder's deletion moves its bookmarks to the root — the bookmark is never lost) — a personal-by-id pointer, never an audience decision, never granting anything (ADR 0118 + ADR 0168) |
  | **M18** recurring events | **coordination** — a recurring event is not five separate events but one event with a recurrence rule; the RSVP + audit + notification seams all operate on the *series*, not the instance (ADR 0119) |
  | **M19** guest accounts | **shared awareness** — the identity model extends from "a member of this community" to "a member, or a guest invited by a member," and the authorization model resolves both through the same `IAuthorizationService` seam (ADR 0120) |
  | **M20** notification quiet times | **shared awareness** — the notification system respects the resident's attention rhythm; the "deferred" state is a first-class concept in the notification lifecycle (ADR 0121) |
  | **M21** document management | **outcome + world seams** — the neighborhood's official documents live in the platform with per-document access controls; a shared repository closes the loop into the residents' paperwork (ADR 0122) |
  | **M23** extended profiles | **shared awareness → understanding** — the directory is not just a list of names but a discoverable index of people by skill, interest, and expertise; the bio + tag fields are the integration points between the profile and the post/reply/tag surfaces (ADR 0123) |
  | **M22** onboarding | **shared awareness** — a new resident is walked through the account-setup surface that already exists (display name, avatar, UI language, time zone, date format, contact) and the banner clears once they finish; a guided shell over the frozen owner-scope lanes + one additive completion field, not a new data surface (ADR 0132) |

  (Named lanes — `GP` group posts, media (ADR 0011), `ML` multilingual (ADR 0005), and `ML-UI` live-UI multilingual (ADR 0015) — ship on their own design docs and value-chain steps, not as M-letter rows in this table; `ML` and `ML-UI` are *shipped* lanes, `GP` and media likewise.)

  **Milestone status (kept in sync with `Milestones.cs` / README / STATUS).** **M27 user-scoped portability is shipped** (ADR 0148) — a resident exports / backs up their own authored data and, on import, resolves conflicts per entity (add elsewhere or discard); the `UserPortabilityService` rides the M11 (ADR 0108) context + archive machinery, the verified-resident self-lane is the gate, and the `IAuthorizationService` surface is unchanged (zero new authorization surface). **SITE site content customization is shipped** (ADR 0150) — the two landing surfaces' hero text is admin-editable and every other section is show/hide; a new `Kumunita.Core.SiteContent` bounded context owns the `SiteContent` **singleton** doc (one row per instance, `Id = "singleton"`, the `LocaleSettings` shape) + the `ISiteContentService` read (`GetAsync`, best-effort, in-code fallback) / write (`SaveAsync`, one `site.save` `AccessAudit` row `Via = Admin`) seam; the `/admin/site` surface is a dedicated `AdminSiteController` (GlobalAdmin-gated, ADR 0050 shape); the `kw-l` registry entries stay (the canonical `en` source text); zero new `AccessAction` / `AccessVia` / authorization branch; **SITE-2 hero translations is shipped** (ADR 0157, the ADR 0150 §D5 "future SITE-2 translation lane"): a `SiteContentTranslation` row (one row per language, the `SiteContentDocTypes` surface) + the `GetTranslationsAsync` / `Add` / `Update` / `RemoveTranslationAsync` seams (GlobalAdmin-only, one `sitetranslation.*` audit row per write) + the hero render overlay on `/` and `/about` (a blank hero field falls back to the singleton's value, then to the shipped `kw-l` floor); the `SiteContent` singleton + the `SaveAsync` lane + the `kw-l` registry are untouched; **M28 guardian time limits is shipped** (ADR 0151) — a child-scoped allow/block schedule (hours × days of week, in the child's own time zone) on the GU guardian-controls lane (ADR 0028) that gates *when* a supervised child may use the platform: a sign-out gate (the `BlockedAccountMiddleware` analog), the GU-lane inverse of the M20 per-resident notification quiet times; zero new authorization surface. **IMPROVE is shipped** (the `IMPROVE` named lane, U00–U09) — the platform's own *integration* pass, not a new capability: the four largest files are differentiated (the shared composer trio extracted to one helper, the two longest views split into named partials, the translation-key registry split into per-surface groups), the ADR index no longer has missing rows, the two agent-instruction docs no longer duplicate, the long handoff notes + design docs open with a 10-second summary, and the security & privacy seam is audited with *who* + *why* on every row; the close flip lands the `IMPROVE` entry in `Milestones.cs` / the README Roadmap / STATUS / this file / `MilestonesTests.cs` / `WhatsNew.cs` (the six-member contract, `AGENTS.md`); the audit ledger + reduction baseline are in `docs/archive/lanes/improve/improve-audit.md`; **no new feature — the codebase is measurably smaller, the docs measurably more navigable, the seams measurably more tested, and the existing tests still pass.** **M29 admin surface labels is shipped** (ADR 0152) — a `GlobalAdmin` edits the display name of each top-navigation item and the related page header follows the choice (a rename stays consistent across the surface — the nav and the `<h1>` agree); a new `Kumunita.Core.SurfaceLabels` bounded context owns the `SurfaceLabels` **singleton** doc (one row per instance, `Id = "singleton"`, all 13 labels `null` = the shipped `kw-l` text, the `SiteContent` shape) + the `ISurfaceLabelsService` read (`GetLabelAsync`, best-effort, `kw-l` floor) / write (`SaveAsync`, one `surface_labels.save` `AccessAudit` row `Via = Admin`) seam; the `/admin/labels` surface is a dedicated `AdminSurfaceLabelsController` (GlobalAdmin-gated, the ADR 0150 shape); the 13 nav items + 13 surface headers resolve the label from the singleton (a label is a display override, never a re-route — no route change); the `kw-l` registry entries stay (the canonical `en` source text); zero new `AccessAction` / `AccessVia` / authorization branch. **M30 admin onboarding is shipped** (ADR 0153) — a guided walk-through for a new GlobalAdmin through the seven most important initial settings (community name, languages, moderation, notifications, storage limits, site content, issue escalation): a new `Kumunita.Core.AdminOnboarding` bounded context owns the `AdminOnboarding` **singleton** doc (one row per instance, `Id = "singleton"`, one additive `CompletedAt: DateTimeOffset?` field, `null` = not-yet-guided — the `SiteContent` / `LocaleSettings` shape, ADR 0005 B / ADR 0150) + the `IAdminOnboardingService` read (`GetAsync`, best-effort, `null` floor) / write (`CompleteAsync`, one `admin_onboarding.complete` `AccessAudit` row `Via = Admin`) seam; the `/admin/onboarding` surface is a dedicated `AdminOnboardingController` (GlobalAdmin-gated, the ADR 0150 shape) + the dismissible admin banner (the M22 `_OnboardingBanner` partial's admin sibling, rendered iff GlobalAdmin AND `CompletedAt` is `null`); a links-only guided shell — no new write lane for any of the seven settings, `SiteContent` + `LocaleSettings` + the M22 `Profile.OnboardingCompletedAt` untouched; zero new `AccessAction` / `AccessVia` / authorization branch. **M31 production error handling is shipped** (ADR 0154) — a report-an-issue form on the 500 error page (a resident says what went wrong) + a GlobalAdmin triage surface at `/admin/error-reports`: a new `Kumunita.Core.ErrorReports` bounded context owns the `ErrorReport` **non-singleton** doc (one row per report, the `UsageEvent` row-per-event shape, `TriageStatus` closed to `{"new", "triaged"}` in M31 — `resolved` is M32's) + the `IErrorReportService` read (`ListAsync`, no audit) / write (`CreateAsync` — one `errorreport.create` `AccessAudit` row; `MarkTriagedAsync` — idempotent, one `errorreport.triage` `AccessAudit` row `Via = Admin`) seams + the `ErrorReportDocTypes` registration surface; the `/admin/error-reports` surface is a dedicated `ErrorReportAdminController` (GlobalAdmin-gated, the ADR 0001-B thin-token rule) + the enhanced public `/Home/Error` report form (the one place an anonymous visitor sees a product surface, M31·2); the closed `errorreport.*` `kw-l` key set × en/de/fr/da; one additive `AccessVia.Anonymous` value, zero new `AccessAction` / `IAuthorizationService` branch, the existing `Posts/Report` doc (ADR 0023) untouched. **M32 issue submission & escalation is shipped** (ADR 0155) — the M32 extension of the M31 `ErrorReports` surface (no new context, no new doc, Core stays HTTP-free): the additive `ErrorReport` fields (the Origin/ResolvedAt/ResolvedBy/ResolutionNote — the 15-member M32 ceiling) + the `MarkResolvedAsync` seam + the public `/issues/new` general issue-submission surface + the `/admin/error-reports/{id}` detail view + the resolve + escalate actions + the `IEscalationForwarder` Web-layer HTTP service (the repo's first outbound HTTP, the `KUMUNITA_ESCALATION_ENDPOINT` env var) + the closed `issue.*` / `errorreport.resolve.*` / `errorreport.escalate.*` `kw-l` key set × en/de/fr/da + the M30 onboarding step-7 route re-point. **M33 storage metrics history is shipped** (ADR 0156) — the M33 storage-metrics-history lane (additive on the M24 `Usage` surface, no new context, no new doc-type for the M24 snapshot, Core stays HTTP-free + Wolverine-free): the `StorageMetricsSample` doc (the 8-member M33·2 ceiling, one per UTC day, deterministic `Id`) + the `StorageHistoryDocTypes` surface (the ADR 0004 §B.1 parallel-surface shape) + the `IStorageMetricsService.GetHistoryAsync` read seam (the 5th method, pinned {30,90,180,365} window) + the `StorageMetricsCaptureService` (the Wolverine-free capture + the 365-day `RetentionDays` retention) + the `StorageMetricsCaptureTick` + the `StorageMetricsCaptureHandler` + the `Program.cs` boot seed + the `storage.trend.*` `kw-l` key set × en/de/fr/da + the `/admin/storage` Trend section (a per-day table + an inline `<svg>` sparkline, no JS chart library); zero `AccessAudit` rows (capture and read). **ErrorReports/ (ESC extension)** (ADR 0159) — the ESC escalation authorization + receiving inbox lane: the EscalationToken + EscalationOutboundConfig docs + the IEscalationTokenService seam (the ListAsync / GenerateAsync / RevokeAsync / ValidateAsync read + write lanes) + the AcceptInboundAsync seam on IErrorReportService + the AccessVia.Escalation additive value + the four additive ErrorReport fields (the FromInstance / EscalationReceivedAt / EscalationSourceId / EscalationTokenId — the 19-member ESC ceiling) + the Origin "escalated" value + the EscalationDocTypes registration surface. **M34 analytics history is shipped** (ADR 0162) — the M34 analytics-history lane (additive on the M13 `Usage` surface, no new context, no new doc, no new capture lane, Core stays HTTP-free + Wolverine-free): the `UsageDailyTotal` + `UsageHistoryResult` DTOs (the 2-member M34·1 ceilings) + the `IUsageAnalyticsService.GetHistoryAsync` read seam (the 3rd method, pinned {7,30,90,180,365} window) + the M13 surface extension (the `AdminAnalyticsController` + the `AnalyticsViewModel.History` member + the `Views/AdminAnalytics/Index.cshtml` Trend section — a per-day table + an inline `<svg>` sparkline, no JS chart library) + the closed `analytics.trend.*` `kw-l` key set × en/de/fr/da; **derived, not captured** (the M13 `UsageEvent` doc + `UsagePurgeService` retention are **reused**, not re-added — no `UsageDailySample` capture doc, no new doc-type surface, no capture tick / handler / boot-seed); zero `AccessAudit` rows (the read is a read — the M13 "the audit row is on the export only" discipline held). **M35 kiosk mode is shipped** (ADR 0164) — the M35 kiosk-mode lane (the last milestone on the roadmap — the close flips M35 → done and promotes **no** successor, M35·11): a new `Kumunita.Core.Kiosk` bounded context owns the `KioskPlaylist` **singleton** doc (one row per instance, the `SiteContent` shape — the `Views` ordered list + the two display-only intervals) + the `KioskDocTypes` surface + the `IKioskService` seam (`GetPlaylistAsync` + `SavePlaylistAsync`, one `kiosk.save` `AccessAudit` row `Via = Admin` per GlobalAdmin write); the `/kiosk` signed-in display reuses the **frozen** M5 `IProjectService` board-detail + todos-list read seams + the M4 `IEventService` calendar-range seam — the same `Read` decision re-run on every fetch, so a slide never reveals more than that viewer could already see on that surface's own feed — and `@await`s the existing board / calendar / todos Razor partials verbatim (the ADR 0104 chrome-hide idiom); **zero** new `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource` adapter (the kiosk adds no authorization surface of its own); the two intervals are display-only, never an access input; Core stays HTTP-free + Wolverine-free; additive on 0149 [the per-resident display-preference singleton shape] + 0091 / 0090 [the frozen board / calendar / todos read seams + render partials the kiosk reuses] + 0104 [the chrome-hide idiom] + 0031 [the `client/lib/kiosk.ts` plain-TS state machine] + 0015 [the `kiosk.*` `kw-l` registry] + 0110 [the `WhatsNew` registry — the required sixth close-flip member]. **M36 @mentions is shipped** (ADR 0165) — a cross-surface `@<resident>` reference token (type a resident's **directory display name** after an `@`, the platform **resolves it against the frozen M2 resident directory** and (a) **notifies the mentioned resident** on that surface (a mention notification kind per surface — the ADR 0076 D2 "follow-on lane" now wired: the reserved `post.mention` goes live on posts + replies, and six sibling kinds join the closed vocabulary for group posts, pages, events, to-do comments, announcements, and messages) and (b) **renders the name as a link** to the resident's directory page (a **post-render pass** over the **frozen** `MarkdownRenderer` — the one-renderer pin preserved; unresolved / ambiguous / self tokens stay plain text): one new shared Core module `Kumunita.Core.Mentions` (the pure `MentionTokens` parser + the `IMentionService` read seam over the frozen `IDirectoryService` — the M34 "derived, not captured" shape, the M13 `IUsageAnalyticsService` / M25 `IStorageMetricsService` read-seam precedent) + an additive mention-emitter step in each surface's frozen write lane (the `PostService` post + reply, group post + reply, `PageService`, `EventService`, `ProjectService.CreateTodoCommentAsync`, `AnnouncementService`, `MessagingService` lanes — signatures + audit rows untouched, M36·4) + the `MentionRenderer` render pass (a server-side C# pass at the `MarkdownRenderer` boundary — the ADR 0165 D6 shape, the one-renderer pin preserved) + the closed mention `kw-l` key set (7 kinds × 4 keys) × en/de/fr/da (M36·7); **zero** new bounded context / doc / doc-type surface / index / EF migration (a mention is **derived**, not captured, M36·1); **zero** new `AccessAction` / `AccessVia` / `Decide()` branch / `IAuditableResource` adapter (M36·5 — a mention is a *personal read*; the emission emits **zero** `AccessAudit` rows); the author is never notified of their own mention and an ambiguous or blocked name is inert (M36-5); detection runs on the **publish** lane, not draft-save (M36·8, ADR 0037); Core stays HTTP-free + Wolverine-free (M36·9, ADR 0006-D); **zero** new dependency, **zero** new npm package, **zero** build step (M36·6 / M36·7 — the ADR 0031 plain-TS pin honored by the server-side shape); additive on 0076 [the ADR 0076 D2 reserved `post.mention` kind + the `EmitAsync` writer + the `NotificationKinds` vocabulary + the per-kind settings toggle, all **reused / extended additively**] + 0023 [the frozen M2 `IDirectoryService` read seam — **reused, not re-shaped**] + 0005 / 0015 [the `kw-l` registry the seven mention kinds' keys join] + 0025 / 0031 / 0033 [the frozen `MarkdownRenderer` the `MentionRenderer` pass rides — **unchanged**] + 0013 / 0039 / 0054 / 0067 / 0105 / 0012 [the frozen per-surface write lanes — **additive, not re-shape**] + 0037 [publish-lane detection — draft-save inert] + 0006 [zero new authorization surface] + 0110 [the `WhatsNew` registry — the required sixth close-flip member]. **M37 personal notes is shipped** (ADR 0167) — a private note a resident attaches to any content entity (post / group post / event / page / project / to-do / board / document / directory — the closed `TargetKind` 9-kind set): one new bounded context `Kumunita.Core.PersonalNotes` (the `PersonalNote` + `NoteFolder` docs on the new `PersonalNotesDocTypes` surface — additive, delta-detected, zero migrations for existing surfaces; the unique `pn_uidx_owner_target` index is the F1 idempotency witness; the unique `nf_uidx_owner_parent_name` index is the D6 unique-name witness) + the `IPersonalNoteService` seam (the 8 note-method + 4 folder-tree methods — `CreateNoteAsync` / `GetNoteAsync` / `ListNotesAsync` / `UpdateNoteBodyAsync` / `DeleteNoteAsync` / `AddTagAsync` / `RemoveTagAsync` / `HasNoteAsync` + `CreateFolderAsync` / `RenameFolderAsync` / `DeleteFolderAsync` / `MoveNoteToFolderAsync` — over the frozen `IDocumentStore` + the owning surfaces' frozen read seams + the frozen `MarkdownRenderer` — the D4 body-render pin) + the `POST /notes/attach` write lane (the D3 write-lane visibility check re-run at the Web layer — the target's own frozen `CanAsync(Read)` decision, never a note-row decision; Deny / absent / soft-deleted ⇒ 404, no row survives — the M16 create-gate posture) + the `GET /notes` list (the D2 personal read, C-M37·2: zero `AccessAudit` rows, zero `CanSeeAsync` passes over the note rows — the ADR 0105 "operator has no read standing" precedent; a `GlobalAdmin` who is not the author gets a non-leaky 404) + the `GET /notes/has/{targetKind}/{targetId}` F1 witness + the `_NoteButton` partial wired into the nine target detail surfaces + the `/notes` list view (grouped by `TargetKind` + nested under the owner's folder tree, the `pn.list.degraded` label on dangling rows, per-row edit / move / tag / delete controls, the tag chips) + the `pn.nav` entry in the top nav + the closed 24-key `pn.*` `kw-l` key set × en/de/fr/da; **zero** new `AccessAction` / `AccessVia` / `Decide()` branch / `IAuthorizationService` method (C-M37·1 — the surface adds **no** new authorization surface; a note is a *personal-by-id* record, never an audience decision) **+ the 2026-10-25 page-level notes amendment** (the closed `TargetKind` set grows to **21** kinds — ten `page-*` kinds let a resident attach a note to a *page itself*: `/announcements`, `/community`, the `/groups` index, a group's detail page, `/events`, `/projects`, a project's detail page, `/projects/todos`, `/projects/boards`, `/inventory`, `/documents`; the `_NoteButton` + `note-modal.ts` surface gains multi-instance support via a `data-note-instance` random suffix so a page can carry both a page-level and an item-level note; the closed `pn.*` `kw-l` key set grows from 30 to **40** keys — ten `pn.list.kind.page-*` × en/de/fr/da; **zero** new bounded context / doc / migration / authorization surface — the amendment composes only the frozen `IPersonalNoteService` + the owning surfaces' frozen read seams — ADR 0167 amendment); additive on 0118 [the M17 `Bookmark` / M9 ADR 0105 personal-by-id shape — **reused, not re-shaped**] + 0004 §B.1 [the `*DocTypes` parallel-surface shape — **additive, delta-detected, zero migrations for existing surfaces**] + 0025 / 0031 [the frozen `MarkdownRenderer` the D4 body-render pin rides — **unchanged**] + 0006 [zero new authorization surface] + 0015 [the `kw-l` registry the `pn.*` keys join] + 0110 [the `WhatsNew` registry — the required sixth close-flip member]. **M37 is the last milestone on the roadmap** — there is no M38 to promote (the M36·10 / M35·11 "last milestone, no successor" framing, re-applied). **FDW first day of week is shipped** (ADR 0169) — a per-resident display preference (default Monday) that orders the Quiet-hours + Guardian time-limit day pickers and anchors the events calendar's week/month grid: one additive `Profile.FirstDayOfWeek` field on the existing `UserInfo` context (the ADR 0004 §B.1 shape — delta-detected, idempotent, **zero migration**) + the pure `WeekAnchor` resolver (`Default` = Monday; `Resolve` floors out-of-range values; `OrderDays` is a permutation of 0–6, so the *submitted* picker value is unchanged) + the single owner-scope write lane `SetProfileFirstDayOfWeekAsync` (the `SetProfilePageSizeAsync` shape — fail-closed `KeyNotFoundException`, **zero** audit rows) — **zero** new `AccessAction` / `AccessVia` / `Decide()` branch / `IAuthorizationService` method (a display preference is not an audience decision); a **named lane** — `Milestones.cs` / README Roadmap milestone ordering / `MilestonesTests.cs` are **byte-identical**.

- **The seams are the architecture.** The [modular monolith](#3-modular-monolith--bounded-contexts) section is the
  integration discipline applied: few stable module interfaces over one process,
  with the **access model** ([identity & access](architecture/identity-access.md)) as the most load-bearing contract. The philosophy
  names the failure modes this layout prevents (god part, parts-work-seams-don't,
  signals-without-loops) — see [`docs/philosophy/anti-patterns.md`](philosophy/anti-patterns.md).

Neither document overrides the other: ADRs and `SECURITY.md` remain authoritative for
specifics; the philosophy remains authoritative for *how to judge a design*.

## 1. Technology stack

| Layer         | Choice                     | Notes                                   |
|---------------|----------------------------|-----------------------------------------|
| Runtime       | .NET 10 (LTS)              |                                         |
| Web           | ASP.NET Core MVC + Razor   | server-rendered, classic                |
| Front-end     | Plain TypeScript           | `tsc` only; no bundler / dev server     |
| Data          | Marten + EF (Identity)     | Postgres: domain docs in `mt`; Identity tables in `identity` |
| Messaging/jobs| Wolverine                  | in-process; CQRS-lite                   |
| Event sourcing| — (not used)               | documents + projections instead         |
| Auth (now)    | ASP.NET Core Identity      | cookie                                  |
| Auth (later)  | OpenIddict                 | OIDC, cross-neighborhood federation     |
| Database      | PostgreSQL                 | one per instance                        |
| Packaging     | Docker (multi-stage)       | Coolify on a VPS                        |
| Email (dev)   | Mailpit                    | local SMTP + web inbox                  |

Rationale: ADR 0001 (stack); ADR 0004 (persistence split & schema evolution).

## 2. The sections

The deeper sections are their own docs under [`architecture/`](architecture/)
(split in `D09`). Each row is a link + when to read it:

| Section | When to read it |
|---|---|
| [Solution layout](architecture/solution-layout.md) | before creating a new bounded context, a new `*DocTypes` surface, or a new Web controller — the tree is the contract for where each belongs |
| [Identity & access](architecture/identity-access.md) | before any change to `IUserInfoService`, `IAuthorizationService`, the `Audience` doc, or the audit row shape |
| [Data model (Marten documents)](architecture/data-model.md) | before touching a `*DocTypes.cs` surface, adding a document, or writing a migration |
| [CQRS-lite & side effects (Wolverine)](architecture/cqrs-lite-wolverine.md) | before adding a new command handler, a new email kind, or a new scheduled job |
| [Localization (multilingual)](architecture/localization.md) | before adding a new `kw-l` key, a static page, or a language |
| [Extension seams (deferred, by design)](architecture/extension-seams.md) | before proposing a new bounded context, a new integration, or a new world seam |

## 3. Modular monolith & bounded contexts

In-process modules behind interfaces — not separate services today; the interfaces are
the seam for later extraction.

- **IdentityModule** — authentication; issues the thin principal.
- **UserInfoModule** — who people are: profiles, groups, delegation.
- **AuthorizationModule** — what they may do: audiences, policy, audit.
- **LocalizationModule** — language catalog, default language, and translated UI
  strings (ADR 0005); consumed by the presentation layer, never by feature
  authorization. (Static pages live in the `Pages` context now, ADR 0039.)
- **Feature modules** — Directory, Posts, Pages, Moderation, Media, Tags, Events (M4 ✓ — ADR 0054), Projects (M5 ✓ — ADR 0067), Notifications (M6 ✓ — ADR 0076), Search (M8 ✓ — ADR 0091), Messaging (M9 ✓ — ADR 0105), GroupMessaging (GM ✓ — ADR 0278, the N:1 generalization of M9), Usage (M13 ✓ — ADR 0114), Logging (M13 ✓ — ADR 0114, the BCL-only file sink).
  Directory and Posts are both *consumers* of the single bulk visibility
  capability (`CanSeeAsync`, [identity & access — Services](architecture/identity-access.md)) — list authorization is one platform
  primitive, not per-feature logic. Media (ADR 0011) is a byte-store module:
  content-addressed payloads on a dedicated volume behind the HTTP-free
  `IMediaStore` seam, cataloged in `mt`, served only through an audited app
  endpoint (the profile avatar is the reference lane). The same store now
  carries **two** lanes over **one** volume + **one** catalog (C-ATT·1/3):
  the **content-image** lane (ADR 0025, `GET /content-image/{id}`, inline
  `<img>`) and the **file-attachment** download lane (ADR 0034,
  `GET /attachment/{id}`, `Content-Disposition: attachment` + `nosniff`). The
  attachment lane is a **separate** lane — a separate route, a separate
  allowlist (`MediaOptions.AttachmentAllowedContentTypes`, distinct from the
  image `AllowedContentTypes`), and download semantics — over the **same**
  `IMediaStore` / `MediaObject` / `MaxBytes`. The owning doc carries an
  additive `AttachmentIds` POCO field on `Post` / `PostReply` / `Announcement`
  (ADR 0004 §B.1, **zero migrations**), **separate** from `ImageIds`; the
  ids are derived **server-side** from the body (Core never parses a body,
  C-ATT·4). The serve route reuses the owning resource's single
  `CanAsync(…Read…)` decision — a **reply resolves its parent post's** decision
  (C-ATT·8; a document-session load, **no new `PostService`** seam), a post
  its own, an announcement its flat scope gate (zero `AccessAudit` rows).
  Every miss is a 404; one `Deny` row on a UGC deny, zero elsewhere (C-ATT·7/10).

Dependency rule: feature modules depend on the three identity/access modules (and Marten),
never the reverse. AuthorizationModule may call UserInfoModule to resolve groups; it never
calls feature modules.

The M1 design for these modules is in [`docs/design/m1-identity-access.md`](design/m1-identity-access.md);
the interface set, the thin-principal shape, and change management are
frozen by [ADR 0006](adr/0006-module-boundary-contracts.md) — the [Services draft](architecture/identity-access.md) is a
sketch; the ADR is authoritative for what changes and through what.

## 4. Front-end

- Razor Layout + per-page views.
- One TS module per page under `client/` (e.g. `client/posts.ts`), plus `client/lib/`
  (`api.ts` with CSRF-aware fetch, toasts, flash).
- Build: `tsc` -> `wwwroot/js`; dev: `tsc --watch`. No bundler, no HMR (accepted tradeoff).
- Loaded via `<script type="module" src="~/js/posts.js">`; imports are relative with
  `.js` extensions (browser ESM).
- **CSRF:** every mutating request from `api.ts` sends the anti-forgery token; the server
  validates it on all non-GET endpoints (enforced, not opt-in — see OPS.md §10).
- **Strings:** all user-facing text is resolved server-side through the localization
  provider (Razor tag helper over `TranslationResource`); `client/*.ts` holds logic, not
  display strings. Static pages (terms, help, privacy, conduct) are `PageKind.System`
  page docs rendered by the single page engine ([localization](architecture/localization.md), ADR 0039–0043).

## 5. Deployment & configuration

> **Least-privilege DB role (the two-role split, ADR 0171 — Accepted 2026-10-25, amending ADR 0004 §B).** The prod-hardening lane's F22 (DB-18) reconciles ADR 0004's boot-ownership invariant ("the app role owns the DB so it can create the `mt` and `identity` schemas at boot") with the least-privilege requirement by **splitting the DDL-creation capability across two named roles**: **`kumunita_migrate`** (the migration role — owns the `kumunita` database + the `mt` + `identity` schemas, the only role allowed DDL, the role the boot schema-apply + the EF `MigrateAsync` + the grant step run as) and **`kumunita`** (the runtime app role — demoted to `CONNECT` + `USAGE` + `SELECT` / `INSERT` / `UPDATE` / `DELETE` DML + a **forced exception**: schema-level `CREATE` on `mt` only, because Wolverine + Marten create their runtime tables (`mt.wolverine_*`, `mt.mt_*`) **as the app role** during `app.StartAsync()`; the app role is refused `CREATE SCHEMA` / `DROP SCHEMA` / `DROP DATABASE` — SQLSTATE `42501`). The runtime DML path (`Program.cs:95` Marten + `Program.cs:474` EF) is **byte-identical** to the pre-split baseline; the only new surface is the **`ConnectionStrings:KumunitaMigrate`** env key (read by `SchemaBootstrap.ApplyAsync` + the `Program.cs` boot path — when unset, the path degrades to the single-role baseline, so a not-yet-migrated deployment is unchanged). The provisioning DDL lives in `dev-db-init/01-app-role.sql` (the two roles + the DB owned by `kumunita_migrate`) + `dev-db-init/02-app-schemas.sh` (pre-creates `mt AUTHORIZATION kumunita_migrate` + `GRANT USAGE, CREATE ON SCHEMA mt TO kumunita` against the app DB). The enforcement witness is the `AppRole_CannotDropSchema` Testcontainers test (`tests/Kumunita.Core.Tests/F22_AppRoleCannotDropSchemaTests.cs`). The prod ops half (provision `kumunita_migrate` + demote the app role + set `ConnectionStrings:KumunitaMigrate`) is owner-executed, **recorded, not assumed done** — see the `## Summary` table in `docs/archive/lanes/prod-hardening/hardening-handoff-notes.md`.

One instance per neighborhood on a VPS via Coolify.

  compose (dev/local): app (multi-stage) + postgres:18 + mailpit

Env contract:
  Community__Name            # per-instance display name (NOT "Kumunita")
  Community__SupportEmail
  SMTP__Host / Port / Secure / User / Pass
  SeedAdmin__Email           # initial GlobalAdmin address
  SeedAdmin__Token           # ONE-TIME setup token — consumed and invalidated on first
                             # login ("set your password" doubles as first login); env
                             # never holds a reusable admin credential. See OPS.md §2
  ConnectionStrings__Kumunita
  # Prod-hardening lane (F1–F22) — optional; each degrades to the pre-lane
  # baseline (byte-identical) when unset. See the F22 blockquote above (the
  # ADR 0171 two-role split) and OPS.md §10 for the full knob set.
  ConnectionStrings__KumunitaMigrate   # F22 (DB-18) — the kumunita_migrate
                                        # role's DSN; read by SchemaBootstrap
                                        # .ApplyAsync + the Program.cs boot
                                        # path; unset ⇒ single-role baseline.
  SENTRY_DSN                           # F10 — the Sentry.AspNetCore error-
                                        # tracker DSN; unset ⇒ no registration.
  VirusScan__Host / VirusScan__Port    # F16 — the ClamAV daemon endpoint;
                                        # unset ⇒ the IVirusScanner seam is
                                        # absent, the serve-lane gate is
                                        # fail-open.
  MailSuppression__IngestToken         # F17 — the bearer token the
                                        # POST /api/mail/suppress ingest
                                        # route requires; unset ⇒ the route
                                        # is not registered.
  DbTls__SslMode / DbTls__CaPath       # F21 — app↔DB TLS; unset ⇒ the
                                        # connection string is byte-identical
                                        # to the pre-lane baseline.

First run (versioned migrations + seeder): apply Marten migrations -> apply Identity
migrations -> create GlobalAdmin for SeedAdmin__Email with the token as the one-time
setup credential (invalidated on first use) + send verification email -> seed default
Components (Safety, Maintenance, Social, Governance) -> seed the language catalog
(source language `en` enabled and set as default; source-language UI strings
materialized from the image, ADR 0005). Re-running the seeder is a no-op if the admin
already exists.

**Schema evolution is versioned, not auto-upgrade** (ADR 0004). Every domain
schema change is a Marten storage feature — a `FeatureSchemaBase` subclass
(Weasel objects — tables, indexes, etc.) registered in `StoreOptions.Storage`;
`KumunitaFeature` is the first (`mt.community`, ADR 0004 §B). Applied idempotently by
`ApplyAllConfiguredChangesToDatabaseAsync()` (each object delta-detected against the
live catalog, so a second run over an existing database is a no-op); forward-only.
(In Marten 9 the pre-9.x `IMigration`/`StoreOptions.Migrations` step model no longer
exists; the DDL is exportable for review via `WriteMigrationFileAsync()` — see
`KumunitaFeature`. The `mt` schema has no operator-visible applied-step ledger —
idempotency is delta-detection, not a recorded sequence. Identity schema changes are
EF Core migrations recorded in `identity.__EFMigrationsHistory`.) Both are forward-only.
Auto-upgrade (schema derived from document shapes) is dev-only, if used at all — it
is never run against production.

Ops: TLS via Coolify / Let's Encrypt; `/health` (reports **degraded** when the email
dead-letter count is non-zero — [CQRS-lite — durable email handler](architecture/cqrs-lite-wolverine.md)); scheduled `pg_dump` + offsite copy.

## 6. Testing

- `Kumunita.Core.Tests` (xUnit): the authorization table (the "hedge post" case) is a
  first-class test; handler side-effects via Marten TestWidgets. Explicit rows for the
  [decision algorithm](architecture/identity-access.md) invariants: an **empty `All`-mode audience denies** (no vacuous truth), and a
  **delegate with an out-of-scope action is denied** even when the effective principal is
  the owner. Bulk/single agreement: `CanSeeAsync` and `CanAsync` **agree on the
  empty-`All`-audience invariant** (shared `MatchGroups`, [decision algorithm](architecture/identity-access.md)). Directory: uses
  `CanSeeAsync`; **`contactVisibility` is never evaluated on a profile hidden by
  `visibility`** (no rows, no log entries for the contact block). Side effects:
  **a command handler's domain + audit writes commit even when the email send fails**
  (a failed SMTP must not roll back `FileReport` or drop the `AccessAudit` row — [durable email handler](architecture/cqrs-lite-wolverine.md)).
- Concurrency: a **stale `Store.Update` raises `StaleConcurrencyError` and the handler
  maps it to 409** (no domain write, no orphaned audit row — [data model](architecture/data-model.md) convention).
- `Kumunita.Web.Tests`: a few Playwright e2e flows (register -> verify -> post -> audience gate).
- Target: authorization + delegation exhaustively unit-tested; happy paths e2e-tested.

