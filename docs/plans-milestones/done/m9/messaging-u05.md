# M9 Messaging — U05 · Web surface: admin toggle

> **Unit plan (secondary tier, self-contained).** You are a fresh-context
> agent executing one unit of M9 (Messaging). Read **this file + your entry
> reads below** — that is all the context you need. The register is
> `docs/plans-milestones/plan-m9-messaging.md`; the scratch handoff note is
> `docs/plans-milestones/m9-messaging-handoff-notes.md`. **Atomicity
> contract:** ≤ 5 files, ≤ ~400 LOC, exit = one build + app smoke.
> **Unit-series rule:** never touch files outside your own Deliverables; no
> tests beyond the pinned list (none in this unit — U06 owns the Web tests);
> no new seams on frozen interfaces.

## Goal

F6 rendered: the GlobalAdmin toggle surface (the sibling's route is
`/admin/announcement-comments` — confirm the sibling's actual route in your
entry reads and use the parallel `/admin/messaging` shape) with the on/off
control — the `AdminAnnouncementCommentsController` shape verbatim.

## Context (the [PROPOSED] shape U00 locked)

- **F6 · The admin toggle is a single audit-trailed action.** A GlobalAdmin
  flips the feature on/off at `/admin/messaging`. The **service** (U02's
  `SetMessagingEnabledAsync`) is what commits the flag + the one
  `messaging.toggle` audit row (C-M9·3) — the controller only calls it and
  maps the `actorId` (the current user) + the `enabled` value. The
  controller is **GlobalAdmin-only** (the `AdminAnnouncementCommentsController`
  authorization shape — verify its exact attribute in your entry reads).
- **Read** (`IsMessagingEnabledAsync()`) renders the current state (a
  checkbox/radio reflecting it). **Write** (POST) calls
  `SetMessagingEnabledAsync(enabled, actorId)` — the service emits the audit
  row; the controller adds none.
- **F7 · Localized.** The admin-facing strings reuse the existing admin
  `kw-l` patterns (the sibling toggle's keys) — if your entry reads show the
  sibling toggle added its own keys, follow that; if not, the design doc's
  key list is authoritative.

## Entry reads (5)

1. `docs/design/m9-messaging-design.md` §toggle + §web (the locked shape).
2. `src/Kumunita.Web/Controllers/AdminAnnouncementCommentsController.cs`
   (the controller shape to copy — the authorization attribute + the GET/POST
   action pair + how it maps `actorId`).
3. `src/Kumunita.Web/Views/AdminAnnouncementComments/Index.cshtml` (the
   sibling toggle's view — the layout to copy for the new
   `Views/AdminMessaging/Index.cshtml`).
4. `src/Kumunita.Web/Models/AnnouncementViewModels.cs` (~line 149 — the
   `AnnouncementCommentsAdminViewModel` shape to mirror for the messaging
   admin view-model, if the controller declares one the same way).
5. `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (U04's nav — to
   confirm the resident-side link text the admin surface mirrors, if
   relevant).

## Deliverables (≤ 5)

- `src/Kumunita.Web/Controllers/AdminMessagingController.cs` — the
  GlobalAdmin controller (the `AdminAnnouncementCommentsController` shape —
  its route is `/admin/…` and it declares its own view-model class; mirror
  that): `Index` (GET — current state), `Index` (POST — flip).
- `src/Kumunita.Web/Views/AdminMessaging/Index.cshtml` — the on/off form
  (the `Views/AdminAnnouncementComments/Index.cshtml` layout — the sibling
  view lives in its **own** folder, not under `Views/Admin/`).
- The admin link (one line where the `announcementcomments` admin link lives
  — identify it in your entry reads; if the sibling has no admin-index link
  and is reached only by direct URL, follow that — no link added).

## Exit

`dotnet build Kumunita.slnx -c Debug` green. App smoke: as a **GlobalAdmin**,
`/admin/messaging` flips the toggle (verified by the resident surface
appearing/disappearing in `_AccountNav`); as a **non-admin**, the route 403s
(the sibling controller's authorization shape enforces this).

Append a `## U05 — admin toggle surface` section to the handoff note: the
link placement (the file + neighbor), the authorization attribute used, any
drift (e.g. the sibling toggle's link lived in a file the plan didn't name).

**Last action:** once the Exit above is satisfied and the `## U05` section is
appended, move **this unit's own plan file** from
`docs/plans-milestones/in-progress/messaging-u05.md` to
`docs/plans-milestones/done/m9/messaging-u05.md`. Each unit moves only its own
file as it completes — U06's plan is already sitting in `in-progress/`, so
the next agent just reads it there.
