---
id: adr-0088
title: "Projects / to-dos / Kanban-board user-added translations (the ADR 0022/0029/0059 lane carried to the M5/PL surface)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/group-events-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0088](0088-projects-todos-boards-translations.md) — Projects / to-dos / Kanban-board user-added translations (the ADR [0022](0022-user-added-post-reply-translations.md)/0029/0059 lane carried to the M5/PL surface)

Status: Accepted
Date: 2026-09-21
Additive to: **0022** (post / reply translations), **0029** (announcement
translations), **0048** (edit + remove lanes), **0059** (event translations —
the closest template), **0021** (the Translator standing), **0027** (display /
chip-swap), **0049** (detail default-visible variant), **0051** (list-surface
variant default), **0067** (the M5 Projects context), **0086** (the PL goals &
projects lane). Additive on **0004 §B.1** (delta-detect schema) and **0006**
(module boundary contracts).

> **Abstract:** the M5 Projects surface (ADR [0067](0067-m5-projects-todos-and-kanban.md)) and the PL goals & projects
> lane (ADR [0086](0086-goals-projects-lane.md)) shipped their full feature sets but, like the M4 events
> surface did before ADR [0059](0059-event-translations.md), **excluded the user-added translation lane** —
> every other resident-facing UGC surface already carries it. This ADR
> carries the ADR [0022](0022-user-added-post-reply-translations.md) / 0029 / 0048 / 0059 lane over to the M5/PL bounded
> context, for the **three surfaces the user named: projects, to-dos, and
> Kanban boards** — a `TodoItem`, a `KanbanBoard`, and a `Project` each gain
> user-added **title + body translations** (add / edit / remove) on the row's
> **creator**, a **Translator** (ADR [0021](0021-translator-role-delegated-translation-editing.md)), a **GlobalAdmin** (ADR [0017](0017-announcement-edit-lane-author-or-globaladmin.md)), and
> (to-dos only) the **assignee** (the ADR [0067](0067-m5-projects-todos-and-kanban.md) collaborator branch); displayed
> with the ADR [0027](0027-post-reply-translation-display-and-swap.md) chip-swap + ADR [0049](0049-default-language-variant-selection.md) default-visible variant on the detail
> page, and the ADR [0051](0051-list-surface-variant-default.md) viewer-language selection on the `/projects` feeds.
> **`ProjectGoal` is deliberately out** (the recorded follow-on). **No new
> `AccessAction`, no new role, no new `AccessVia`** — the 12 write lanes
> self-compose their C3 session (the ADR [0067](0067-m5-projects-todos-and-kanban.md) §4 convention, **no
> `IDocumentSession` parameter**). **For** a developer touching
> `Kumunita.Core.Projects` translations, the `IProjectService` translation
> seams, the `ProjectsController` translation lanes, or the `tests/*`
> translation families. The three translation-row field sets, the 12-seam
> table, the standing matrix, the audit table, and the full Web surface are
> in the [handoff
> note](../design/adr-handoffs.md#adr-0088--projects--to-dos--kanban-board-user-added-translations-moved-detail).

## Context

The M5 Projects surface (ADR [0067](0067-m5-projects-todos-and-kanban.md)) and the PL lane (ADR [0086](0086-goals-projects-lane.md)) shipped their
full feature sets — to-dos, subtasks, Kanban boards + lanes + cards, goals,
projects — but **excluded the user-added translation lane**. Every other
resident-facing UGC surface now carries it: posts / replies (ADR [0022](0022-user-added-post-reply-translations.md)), group /
community name & description (ADR [0026](0026-group-community-name-description-translations.md)), announcements (ADR [0029](0029-announcement-translations.md) + edit/remove
in ADR [0048](0048-translation-edit-and-remove-lanes.md)), pages (ADR [0039](0039-pages-hierarchy-audience-translations.md) / 0047), and events (ADR [0059](0059-event-translations.md)). The M5 to-do /
board / project detail pages are the remaining UGC surfaces still authored-in
language only — so a deployment cannot yet be exercised in a second language
end-to-end on the **projects** feed or detail, the stated multilingual goal
(ADR [0005](0005-multilingual-support.md)). This ADR carries the lane over to the three named surfaces.

**Scope — `ProjectGoal` is deliberately out.** The user named *projects, to-
dos, Kanban boards*. `ProjectGoal` is the same PL lane and *would* be cheap to
add (identical `Title` + `Description` shape as a `Project`), but this ADR
does not add it: the lane is carried to exactly the three surfaces named, and
a goal gains the identical lane by a trivial follow-on ADR (the same
`ProjectGoalTranslation` POCO + 4 seams + 3 Web lanes as `Project`'s). Keeping
scope to the three named entities keeps this ADR reviewable and its tests
bounded; `ProjectGoal` is recorded here as the explicit follow-on.

## Decision

- **D1 — The three translation rows.** Three POCOs in
  `Kumunita.Core.Projects` — `TodoTranslation` (parent `TodoItem` /
  `TodoItemId`), `BoardTranslation` (parent `KanbanBoard` / `BoardId`),
  `ProjectTranslation` (parent `Project` / `ProjectId`) — each mirroring
  `EventTranslation` (ADR [0059](0059-event-translations.md)) but with the **Projects body-optional**
  adjustment (`Body?` is **optional** — unlike `EventTranslation`'s required
  `Body` — because the M5 surfaces are title-usable; at least one of
  `Title` / `Body` must be non-blank at write time, enforced server-side).
  Each carries `Id`, `<Parent>Id`, `LanguageCode`, `Title?`, `Body?`,
  `AuthorId`, `Created`, registered on the **`ProjectDocTypes`** surface with
  a **`(ParentId, LanguageCode)` unique index** (the `PostTranslation` /
  `AnnouncementTranslation` / `EventTranslation` business-key convention).
  **Zero migration for existing rows** (ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1). See the handoff note
  for the full field sets.
- **D2 — Twelve `IProjectService` seams (four per entity).** `IProjectService`
  gains exactly **12 methods** (3 entities × 4 seams) — **read**
  (`Get<E>TranslationsAsync`, a **read, not a decision** — no authorization,
  no audit), **add** (`Add<E>TranslationAsync`, stores the row + its
  `AccessAudit` row), **update** (`Update<E>TranslationAsync`, re-stamps
  `Created` + `AuthorId`), **remove** (`Remove<E>TranslationAsync`) — mirroring
  the ADR [0048](0048-translation-edit-and-remove-lanes.md) / 0059 shape. **Seam shape — the M5/PL convention (ADR [0067](0067-m5-projects-todos-and-kanban.md)
  §4), not the M3 one:** these write lanes **self-compose** their own C3
  session; they carry **no `IDocumentSession` parameter**. The domain write and
  the `AccessAudit` row commit atomically in that one self-composed session.
  See the handoff note for the full table + the error-contract detail.
- **D3 — The standing: creator ∪ assignee (to-do) ∪ Translator ∪
  GlobalAdmin.** A **pure role-claim check** (the M5 §3.4 shape), enforced
  server-side by three new pure helpers `ProjectService.Resolve<Todo|Board|
  Project>TranslationStanding` (the C3 single-source pin). The matrix is the
  **translation** matrix, *not* the edit standing — the ADR [0059](0059-event-translations.md) author ∪
  Translator ∪ GlobalAdmin matrix **plus the to-do's existing ADR [0067](0067-m5-projects-todos-and-kanban.md) assignee
  branch**: the **creator** (`AccessVia.Owner`); the **to-do's assignee**
  (to-do only, `AccessVia.Admin`); a **Translator** of **anyone's** row
  (`AccessVia.Admin`); a **GlobalAdmin** of **anyone's** row (`AccessVia.Admin`);
  a plain Member — **no** standing. This is *broader than* the existing edit
  standing (a Translator who cannot *edit* a to-do's title may still
  *translate* it). No new `AccessVia` member, no new role. A **pure display
  helper** `ProjectService.CanAdd<E>Translation` drives the Web controls. See
  the handoff note for the full matrix.
- **D4 — Audit.** Each write lane stores one `AccessAudit` row in the self-
  composed session (C3): `TargetId = <parent id>`, `Outcome = Allow`, `Via`
  from the D3 standing, the existing `TargetKind` constants reused verbatim
  (`"todo"` / `"board"` / `"project"`), and the ADR [0059](0059-event-translations.md) `eventtranslation.*`
  idiom per entity — `todotranslation.*` / `boardtranslation.*` /
  `projecttranslation.*` (`.add` / `.update` / `.remove`). The **read** lane
  writes **no** audit row. **No new `AccessAction`** is introduced. See the
  handoff note for the full `Via` table.
- **D5 — The Web surface.** `ProjectsController` gains **nine** thin POST lanes
  (3 entities × 3): `POST /projects/todos/{id}/translations[/update|/remove]`,
  `POST /projects/boards/{id}/translations[/update|/remove]`,
  `POST /projects/projects/{id}/translations[/update|/remove]` — all
  `[ValidateAntiForgeryToken]`, the standard shape-error / `NotFound` /
  `ForbidResult` / success-redirect contract, with the parent `GetAsync`
  precondition (C-M3·1) re-running the read decision. **Display:** the detail
  pages use the ADR [0027](0027-post-reply-translation-display-and-swap.md) chip-swap + ADR [0049](0049-default-language-variant-selection.md) default-visible variant (the
  to-do / board wrap title + body in a single `data-td-group`; the project uses
  two groups) + the ADR [0082](0082-content-actions-dropdown.md) `⋮` dropdown / modal idiom; the feeds use the ADR
  0051 viewer-language selection. `ProjectsController` gains an optional
  `ITranslationProvider?` constructor parameter (last position, defaulting to
  `null`). See the handoff note for the full surface.

## Consequences

- **The three remaining named UGC surfaces gain the translation lane**, so a
  deployment can be exercised in a second language end-to-end across posts,
  replies, announcements, groups, pages, events, **and now projects / to-dos /
  boards** — the goal the M6 milestone pulled forward (ADR [0005](0005-multilingual-support.md)). `ProjectGoal`
  is the recorded follow-on.
- **One row per (parent, language) pair** (the `(ParentId, LanguageCode)`
  unique index); **full lane (add / edit / remove)**, the ADR [0048](0048-translation-edit-and-remove-lanes.md) shape.
- **The standing is the ADR [0021](0021-translator-role-delegated-translation-editing.md) / ADR [0030](0030-role-independence-composable-elevated-roles.md) translation matrix, plus the to-do's
  ADR [0067](0067-m5-projects-todos-and-kanban.md) assignee branch** — the one difference from ADR [0059](0059-event-translations.md)'s events
  (events have no assignee concept), and deliberately broader than the M5
  *edit* standing. No new role, no new `AccessVia`.
- **Audit-by-default is preserved** (every add / edit / remove writes its own
  `AccessAudit` row); **display reuses ADR [0027](0027-post-reply-translation-display-and-swap.md) / 0049 / 0051**, not a new
  mechanism; the **M5/PL self-composed-session convention is preserved** (the
  12 write lanes take no `IDocumentSession`, so the controller never opens one
  around them).
- **`Milestones.cs` / the README Roadmap / `MilestonesTests.cs` are untouched**
  — a named lane on the M5/PL surface, not a milestone (the ADR [0086](0086-goals-projects-lane.md) `PL`
  precedent).
