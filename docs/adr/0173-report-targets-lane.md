# ADR 0173 — Report targets: the "Report this…" lane for events, projects,
boards, to-dos, and group posts

Status: Accepted
Date: 2026-10-27
Extends the **M3b report-filing precedent** (ADR 0003 / the
`ModerationService.FileReportAsync` post-lane and
`FileReplyReportAsync` reply-lane, both with their `report.file` /
`report.reply.file` audit-tag pins, the `AccessVia.Admin` two-negatives pin,
and the `"filed"` Status-literal pin), the **`Report` document shape**
(ADR 0004 §B.1 additive-surface convention — the additive
`TargetKind` / `TargetId` fields are delta-detected, idempotent,
zero-migration), the **`_ReportModal` shared partial idiom** (the ADR 0082
"one ⋮ dropdown" shape + the ADR 0118 modal idiom), the **frozen
`NotificationService.EmitAsync` seam** (the `ReportFiled` kind,
`notification:report.filed:{reportId}` key — the ADR 0076 M6 surface,
re-applied per-target), and the **closed `KnownTranslationKeys` registry +
the en/de/fr/da parity pins** (ADR 0015 / ADR 0052). This ADR makes **any
resident who can see an event, project, board, to-do, or group post file a
moderation report against it** — the same standing the post and reply
report lanes already have (C-M3b·1, the "any resident who can currently see
it" pin), with **zero new `AccessAction` / `AccessVia` / `Decide()` branch /
adapter** (the `ModerationService` compose-not-extend convention) and
**zero migrations** (the two additive `Report` fields are delta-detected).

## Context

M3b (Moderation, ADR 0003) shipped the **report + assign + resolve**
machinery against **community posts** (`FileReportAsync`,
`/posts/{id}/report`) and, in ADR 0023, **replies**
(`FileReplyReportAsync`, `/posts/{id}/replies/{replyId}/report`). The
`Report` document, the `ModerationService` write lanes, the
`AccessAudit` filing row (`AccessVia.Admin` — NOT `Report` — reserved for
the read branch, C-M3b·2; NOT `Owner` — C1), the `NotificationKinds.
ReportFiled` emission to the author (the 2026-09-25 no-reason-disclosure
fix), and the moderation queue + resolve read paths all exist and are
battle-tested.

But the platform has **five other UGC surfaces** — **events** (M4, ADR 0054),
**projects** (M5, ADR 0086), **Kanban boards** (PL, ADR 0147), **to-dos**
(PL, ADR 0086), and **group posts** (GP, ADR 0013) — that a resident can
see and that an author can (mistakenly or deliberately) populate with content
that violates the community's norms. None of these five had a report
affordance. The consequence: a resident who saw harassment on an event
description, a board, or a group post had **no path to tell a moderator**
short of asking a GlobalAdmin out-of-band — the exact gap M3b closed for
posts, re-opened for the other five surfaces.

The constraint that keeps the extension safe is the one that already keeps
M3b safe (C-M3b·1, the compose-not-extend pin): the `ModerationService`
gains **one new write lane** (`FileReportTargetAsync`) that re-uses the
*exact same* `Report` document, the *exact same* `AccessAudit` row shape
(`AccessVia.Admin`, the two-negatives pin), the *exact same*
`"filed"` Status literal, and the *exact same* `ReportFiled` notification
emission (the author is notified; the self-report case is skipped; the
reason is never disclosed) — the only difference is **two additive fields
on the `Report` document** (`TargetKind` + `TargetId`) that carry the
target's identity, so the queue and resolve read paths can load the correct
document from the correct bounded context and render its title + author.
No new `AccessAction`, no new `AccessVia`, no new `Decide()` branch, no new
adapter. The `LoadReportTargetResourceAsync` helper (the
`AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` read path)
switches on `TargetKind` to load the correct document type and wrap it in
its existing `ToAuditableResource` adapter — the ADR 0006 module-boundary
contract is intact (the `Moderation` context reads from `Events` /
`Projects` / `Posts` through their frozen service seams, never through a
new cross-context surface).

The Web-layer surface is one new controller
(`ReportTargetsController`) with five thin endpoints (one per target kind),
each `[Authorize]` + `[ValidateAntiForgeryToken]` +
`[EnableRateLimiting("report")]` (the existing 10/hour/IP policy,
re-used — no new rate-limit policy), and a new shared Razor partial
(`_ReportModal`) that renders the Bootstrap 5 modal + POST form the five
surfaces' ⋮ dropdowns target (the ADR 0082 modal idiom — the partial makes
no service call; the surface's own Read decision is the precondition; any
signed-in resident who can see the target may file a report).

## Decision

**D1 — Two additive fields on `Report`, zero migrations.**
`Report.TargetKind` (string?, one of the closed vocabulary
`"event" | "project" | "board" | "todo" | "grouppost"`; `null` for the
original post/reply lanes) and `Report.TargetId` (string?, the target
document's own id; `null` for post/reply) are added to the `Report`
document. Both are **nullable** (the ADR 0004 §B.1 additive convention —
existing rows are unaffected; Marten's delta-detection creates the two
columns on first boot, idempotent). `Report.PostId` and `Report.ReplyId`
are **unchanged** (the original discriminators stay on their own lanes;
`FileReportTargetAsync` sets `PostId = string.Empty` to keep the original
discriminator out of the new lane's rows).

**D2 — One new write lane: `FileReportTargetAsync`.**
`ModerationService.FileReportTargetAsync(targetKind, targetId,
componentId, authorId, actorId, reason, session)` — the same standing as
`FileReportAsync` (C-M3b·1, resident-facing intake, no
`IAuthorizationService` call — it is not an access decision). Validates
`targetKind` against the closed vocabulary (an unknown kind throws
`ArgumentException` **before** any row is written — no partial write).
Writes the `Report` row (`Status = "filed"`, the §2.3 item-2 literal pin),
the `AccessAudit` row (`Action = $"report.{targetKind}.file"`,
`TargetKind = targetKind`, `TargetId = targetId`, `Via = AccessVia.Admin`
— the two-negatives pin, NOT `Report`, NOT `Owner`), and the
`ReportFiled` notification emission to the author (skipped in the
self-report case — the `FileReportAsync` pin, re-applied; **no reason
snippet** — the 2026-09-25 disclosure fix, re-applied). One
`SaveChangesAsync` (C3, no partial write). Returns `1` (one row created).

**D3 — The read-path helper: `LoadReportTargetResourceAsync`.**
A private `ModerationService` method that the three existing read lanes
(`AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync`) now call
instead of inlining the post-load. It switches on
`report.TargetKind`: `"event"` → `IEventService.GetAsync` →
`EventToAuditableResource`; `"project"` → `IProjectService.GetProjectAsync`
→ `ProjectToAuditableResource`; `"board"` → `IProjectService.GetBoardAsync`
→ `KanbanBoardToAuditableResource`; `"todo"` →
`IProjectService.GetTodoAsync` → `TodoItemToAuditableResource`;
`"grouppost"` → `PostService.GetGroupPostAsync` →
`PostToAuditableResource`; `null` (the original post/reply lane) → the
existing `Post` + `PostToAuditableResource` path. Throws
`KeyNotFoundException` on a missing target (fail-closed — the same
posture as the existing post-lane's `KeyNotFoundException`).

**D4 — One new controller: `ReportTargetsController`.**
Five thin endpoints, one per target kind:
`POST /events/{id}/report`, `POST /projects/projects/{id}/report`,
`POST /projects/boards/{id}/report`, `POST /projects/todos/{id}/report`,
`POST /groups/{groupId}/posts/{postId}/report`. Each: `[Authorize]` +
`[ValidateAntiForgeryToken]` + `[EnableRateLimiting("report")]` (the
existing 10/hour/IP policy, re-used). Each: loads the target document
through its frozen service seam (the D3 read-path helper's service calls,
re-applied at the Web layer for the gate), then calls
`FileReportTargetAsync` in the same session (C3, the caller-owned
write-session shape). On success: `TempData["info"]` + redirect back to
the surface (the `_BookmarkButton` obs-2 flash shape).

**D5 — One shared partial: `_ReportModal`.**
A Bootstrap 5 modal (the ADR 0082 modal idiom — the same `.modal.fade`
container the translation modals use) with a POST form (anti-forgery token,
optional `reason` textarea, the `posts.report_reason_placeholder` kw-l key
× en/de/fr/da). The partial makes **no service call** (reporting is a
write, not a state read; the surface's own Read decision is the
precondition). Model: `(string ActionUrl, string ModalId, string
TitleKey)` — the three values the surface needs to wire its dropdown item
(`data-bs-target="#{ModalId}"`) to the form's POST target + the modal's
title label.

**D6 — The ⋮ dropdown integration.**
Each of the five surfaces' ⋮ action dropdown gains one new
`<li><button data-bs-toggle="modal" data-bs-target="#{ModalId}">Report
this {kind}</button></li>` item (the house idiom — the ADR 0082 "one ⋮
dropdown" shape). The dropdown item's label goes through `<kw-l>` with the
new `posts.{kind}_report_button` key (en floor in
`KnownTranslationKeys`). The `_ReportModal` partial is rendered
**outside** the dropdown (a modal is a document-level div; it must not
live inside a `<ul>`), after the dropdown's closing `</div>`.

**D7 — The translation keys.**
Five new keys, one per target kind, × en/de/fr/da (the ADR 0015 / ADR 0052
parity pins): `posts.event_report_button`, `posts.project_report_button`,
`posts.board_report_button`, `posts.todo_report_button`,
`posts.grouppost_report_button`. The `posts.report_reason_placeholder` key
already exists (the post-report lane's placeholder, re-used — no new key
needed for the modal's textarea placeholder).

**D8 — The moderation queue + resolve views.**
`ModerationQueueViewModel.ReportRow` and
`ModerationResolveViewModel` gain four additive properties
(`TargetKind`, `TargetId`, `TargetTitle`, `TargetAuthorName` — all
nullable, `null` for the original post/reply rows). The
`ModerationController.Index` (queue) and `ModerationController.Resolve`
(detail) actions resolve the new target kinds through the D3 helper
(`LoadReportTargetResourceAsync`) and populate the four properties. The
`Moderation/Index.cshtml` queue row renders a target-kind badge + title +
author for the new kinds (the existing post/reply badge + title + author
shape, re-applied). The `Moderation/Resolve.cshtml` detail page renders the
target-kind badge, title, author, and body preview for the new targets
(the existing post/reply body-preview shape, re-applied).

## Consequences

- A resident who can see any of the five UGC surfaces can now file a
  moderation report against it — the same standing as the post and reply
  report lanes (C-M3b·1), the same rate limit (10/hour/IP), the same
  audit trail (`AccessVia.Admin`, the two-negatives pin), the same
  notification emission (the author is notified, the reason is never
  disclosed).
- The moderation queue and resolve detail render the new target kinds
  (badge + title + author + body preview) alongside the existing post/reply
  rows — the GlobalAdmin's workflow is unchanged (the same assign/unlock/
  resolve buttons, the same `Report.Status` transitions).
- **Zero new `AccessAction` / `AccessVia` / `Decide()` branch / adapter**
  (C-M3b·1, the compose-not-extend pin, re-applied).
- **Zero migrations** (the two additive `Report` fields are delta-detected
  — ADR 0004 §B.1).
- The `Report` document's `PostId` / `ReplyId` discriminators are
  **unchanged** for the original post/reply lanes (the `TargetKind` /
  `TargetId` pair is the new lane's identity; `null` `TargetKind` means
  "original post/reply lane").
