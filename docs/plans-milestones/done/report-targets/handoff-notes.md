# RT (Report targets) — the "Report this …" lane for events, projects, boards, to-dos, group posts

**Status:** Shipped 2026-10-27 (ADR 0173, v0.71.0)
**Scope:** One new `ModerationService` write lane (`FileReportTargetAsync`) +
the read-path helper it needs (`LoadReportTargetResourceAsync`, now also
used by the existing `AssignReportAsync` / `UnlockAsync` /
`ResolveReportAsync`), one new `ReportTargetsController` with five thin
endpoints, one shared `_ReportModal` partial, five ⋮-dropdown integrations,
the moderation queue + resolve views extended to render the five new target
kinds, the five `posts.{kind}_report_button` kw-l keys × en/de/fr/da, and
the nine `FileReportTargetAsync_*` tests in
`ModerationServiceTests` (T1–T5).

> **TL;DR:**
> **What did this lane ship?** The "Report this event / project / board /
> to-do / group post" affordance — the same standing the post/reply lanes
> have (C-M3b·1), the same `AccessVia.Admin` two-negatives audit pin, the
> same `"filed"` Status literal, the same `ReportFiled` notification
> emission (no-reason-disclosure), zero new `AccessAction` / `AccessVia` /
> `Decide()` branch / adapter, zero migrations (two additive `Report`
> fields, ADR 0004 §B.1).
> **What is the one seam it created or changed?** The `Report` document's
> closed `TargetKind` vocabulary grows from `{post, reply}` to
> `{post, reply, event, project, board, todo, grouppost}`; the
> `ModerationController.Index` / `.Resolve` read paths now switch on
> `TargetKind` (the `LoadReportTargetResourceAsync` helper) to load the
> correct document from the correct bounded context.
> **What is the one thing a future reader must not re-litigate?** The
> `TargetKind` vocabulary is **closed** — `FileReportTargetAsync` throws
> `ArgumentException` on an unknown kind **before** any row is written
> (T5 pin, no partial write). A new target kind (e.g. "announcement" or
> "document") is a **new additive value** in the closed set + a new
> `LoadReportTargetResourceAsync` switch arm + a new
> `ReportTargetsController` endpoint + a new kw-l key — not a new
> `ModerationService` lane, not a new `AccessAction`, not a new adapter.

## Why this lane existed

M3b (Moderation, ADR 0003) + ADR 0023 (reply reports) shipped the report
machinery against community posts + replies only. The five other UGC
surfaces — events (M4), projects (M5), boards (PL), to-dos (PL), group
posts (GP) — had **no** report affordance. A resident who saw harassment
on an event description, a board, or a group post had no in-app path to
tell a moderator. This lane extends the existing machinery to those five
surfaces without touching any of the existing post/reply code paths
(additive fields on `Report`, one new service method, one new controller,
one new shared partial).

## The shape (the D1–D8 decision set, ADR 0173)

- **D1** — Two additive fields on `Report`: `TargetKind` (string?, one of
  the closed vocabulary; `null` for the original post/reply lanes) +
  `TargetId` (string?, the target doc's own id; `null` for post/reply).
  Both nullable → delta-detected, zero-migration (ADR 0004 §B.1).
- **D2** — `ModerationService.FileReportTargetAsync(targetKind, targetId,
  componentId, authorId, actorId, reason, session)` — the same standing as
  `FileReportAsync` (C-M3b·1, resident-facing intake, no
  `IAuthorizationService` call). Closed-vocabulary validation before any
  row is written. One `SaveChangesAsync` (C3).
- **D3** — `LoadReportTargetResourceAsync(report, session)` — the private
  read-path helper the three existing read lanes now call. Switches on
  `report.TargetKind` to load the correct document + wrap it in its
  existing `ToAuditableResource` adapter. `null` → the original post
  lane. `KeyNotFoundException` on a missing target (fail-closed).
- **D4** — `ReportTargetsController` — five thin endpoints, one per target
  kind, each `[Authorize]` + `[ValidateAntiForgeryToken]` +
  `[EnableRateLimiting("report")]` (the existing 10/hour/IP policy,
  re-used).
- **D5** — `_ReportModal` — a Bootstrap 5 modal + POST form. Makes **no
  service call** (the surface's own Read decision is the precondition).
  Model: `(ActionUrl, ModalId, TitleKey)`.
- **D6** — The ⋮ dropdown integration on each of the five surfaces: one
  new `<li><button data-bs-toggle="modal" data-bs-target="#{ModalId}">`
  item. The partial is rendered **outside** the dropdown (a modal is a
  document-level div).
- **D7** — Five new kw-l keys × en/de/fr/da:
  `posts.{event,project,board,todo,grouppost}_report_button`.
- **D8** — The moderation queue + resolve views render the five new target
  kinds (badge + title + author + body preview) alongside the existing
  post/reply rows.

## The tests (T1–T5, `ModerationServiceTests`)

| Test | What it pins |
|---|---|
| T1 — `FileReportTargetAsync_Filing_WritesReportWithTargetKind` | The Report row carries `TargetKind` + `TargetId`, `PostId = ""`, `Status = "filed"`; the audit row carries `Action = "report.event.file"`, `Via = AccessVia.Admin` (the two-negatives pin), `TargetKind = "event"`, `TargetId = "t1-event"`. |
| T2 — `FileReportTargetAsync_AllFiveKinds_WritesRows` | The closed vocabulary end-to-end: each of the five kinds files a row with the right `TargetKind` + the per-kind audit Action tag. |
| T3 — `FileReportTargetAsync_SelfReport_NoNotificationEmission` | The `authorId == actorId` skip (the `FileReportAsync` self-report pin, re-applied) — no `ReportFiled` emission, but the row + audit still commit. |
| T4 — `FileReportTargetAsync_ThirdPartyReport_EmitsNotification` | The `ReportFiled` emission stages an email for the author with the `notification:report.filed:{reportId}` key (the §6.3 key shape, re-applied). |
| T5 — `FileReportTargetAsync_UnknownKind_Throws_NoPartialWrite` | The closed vocabulary is enforced at the service boundary: an unknown kind throws `ArgumentException` **before** any row is written (no report row, no audit row). |

## The close-flip (the required sixth member, ADR 0110 / AGENTS.md)

- **WhatsNew.cs** — v0.71.0 entry added (newest-first head).
- **WhatsNewTests.cs** — `The_ONB_OnboardingWALK_Entry_Is_Shipped` updated:
  head is now 0.71.0 (Report targets, ADR 0173), 0.70.0 (Kiosk
  conditional refresh) slides one row back.
- **ADR 0173** — `docs/adr/0173-report-targets-lane.md` (the decision
  record).
- **Milestones.cs / README / STATUS.md / ARCHITECTURE.md /
  MilestonesTests.cs** — **not touched** (this is a named lane, not a
  milestone — the M0–M37 + named-lane roadmap is byte-identical; the
  ADR 0173 decision record + the WhatsNew entry are the durable records).

## What a future lane must know

- The `TargetKind` vocabulary is **closed** (D2, T5 pin). Adding a new
  target kind is an **additive value** in the closed set + a new
  `LoadReportTargetResourceAsync` switch arm + a new
  `ReportTargetsController` endpoint + a new kw-l key — not a new
  `ModerationService` lane, not a new `AccessAction`, not a new adapter.
- The `Report.PostId` / `Report.ReplyId` discriminators are **unchanged**
  for the original post/reply lanes. `null` `TargetKind` means "original
  post/reply lane" (the `LoadReportTargetResourceAsync` fallback).
- The `_ReportModal` partial makes **no service call** (D5). The surface's
  own Read decision is the precondition — any signed-in resident who can
  see the target may file a report (C-M3b·1). Do not add a service call
  to the partial; add the gate to the surface's Read decision.
- The `ReportTargetsController` re-uses the existing `"report"` rate-limit
  policy (10/hour/IP). Do not create a new policy per target kind — the
  policy is per-IP, not per-target-kind (a resident who spams reports
  against events is the same threat as one who spams reports against posts).
