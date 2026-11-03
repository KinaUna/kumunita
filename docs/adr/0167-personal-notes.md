---
id: adr-0167
title: "M37: Personal notes (a private note a resident attaches to"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m37-personal-notes-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0167](0167-personal-notes.md) — Personal notes (M37) — every resident can keep private notes about anything (a post, a reply, a group, an announcement, a tag, a member) + optional folders + optional tags + self-management + search, in their own words

Status: Accepted
Date: 2026-10-16

> **Abstract:** M37 — "Personal notes." A new `Kumunita.Core.PersonalNotes`
> bounded context (the **33rd**) + a single `/personal-notes` surface (an
> **additive** extension of the existing `/bookmarks` M17 pattern) + a
> **closed 9-kind `TargetKind`** set (`post` / `reply` / `group` /
> `announcement` / `tag` / `member` / `page` / `event` / `poll`) — the
> **only** kinds a note may target. A note is **personal to one resident**
> (the owning user's `Id` + a **denormalized** `TargetId` + `TargetKind`),
> **private by default** (only the owner sees it — **never** a global
> reader — M37·2), **optional** (no resident is required to use it —
> M37·4), **self-managed** (no `AccessAction` / `AccessVia` / `Decide()` /
> `IAuthorizationService` surface — M37·3), **free-form Markdown body**
> (**not** a pointer — the note carries the text, not a link — M37·5),
> **optionally** organized into a **folder** (a second document, the
> `NoteFolder`) + **optionally** tagged (a **denormalized** `Tags` array on
> the note — the **not-a-pointer** pattern, M37·6), **searchable** by the
> owner over title + body + tags (M37·7), **self-managed** (create / edit /
> delete / move / rename folder — no admin surface, no moderator surface —
> M37·8), and **degrades, never leaks** (a deleted target degrades the
> note's link gracefully — **the note itself survives** — M37·9). **No
> new doc-type surface** (the `PersonalNote` + `NoteFolder` docs are
> registered on the existing `CoreDocTypes` / `PostDocTypes` surfaces —
> M37·10), **no `AccessAction` / `AccessVia` / `Decide()` /
> `IAuthorizationService` surface** (M37·3), **no JS chart library, no new
> JS dependency** (the `/personal-notes` surface is server-rendered, no
> build step — M37·11), **no `AccessAudit` row** on the read (M37·12),
> **no EF migration** (no EF table added). **For** a developer touching
> `Kumunita.Core.PersonalNotes`, the `INotesService` seam, the
> `/personal-notes` surface, or the `tests/*` personal-notes families.
> The D6 folder-tree detail, the D9 six-member close flip, the neutral /
> cost list, the follow-on lanes, and the Amends ledger are in the [handoff
> note](../design/adr-handoffs.md#adr-0167--personal-notes-m37-moved-detail).

## Context

The platform's M17 lane (ADR [0141](0141-guardian-community-approval-lane.md)) gave every resident a **bookmark**
surface: a private per-user list of posts / replies / groups they want to
come back to. That is a **link** (a pointer to content that belongs to
someone else), **not a note** (free-form text in the resident's own words
about that content). The M37 README milestone names the gap exactly:
"Personal notes — every resident can keep private notes about anything (a
post, a reply, a group, an announcement, a tag, a member) + optional folders
+ optional tags + self-management + search, in their own words." M37 is the
**M5-shaped** capability (a **new bounded context** + a **single surface** +
a **closed kind set**), the 33rd context, and the **last milestone** on the
roadmap. The compose-frozen-seams constraint (ADR [0006](0006-module-boundary-contracts.md)-D) and the
self-managed pattern (M37·3) are the **load-bearing decisions**.

## Decision

- **D1 — One new bounded context, two documents, one closed kind set
  (M37·1).** A new `Kumunita.Core.PersonalNotes` context (the **33rd**)
  with **two** Marten documents — `PersonalNote` (the note) + `NoteFolder`
  (the folder) — registered on the existing doc-type surfaces (M37·10).
  The **closed 9-kind `TargetKind`** set (`post` / `reply` / `group` /
  `announcement` / `tag` / `member` / `page` / `event` / `poll`) is the
  **only** kinds a note may target — **closed**, not open.
- **D2 — Personal by id: no adapter, no shared surface (M37·2).** A note is
  **owned** by exactly one resident (the `OwnerId` on the document) and
  **visible** to exactly that resident. **Never** a global reader,
  **never** a moderator / admin read. **No** `IAuthorizationService`
  surface, **no** `AccessVia`, **no** adapter — the ownership check is the
  service comparing `OwnerId` to the authenticated user (M37·3).
- **D3 — Attach requires the target's visibility; the note grants
  nothing (M37·2).** Attaching a note to a target **requires** that the
  resident can already **see** that target (the target's own access rules
  apply — a private group note is only attachable if the resident is a
  member). The note **grants no access** to the target: seeing a note
  does not make the target visible (the note is a **link**, not a
  **permission**).
- **D4 — Content, not a pointer (M37·5).** The note carries the resident's
  **free-form Markdown body** (the text **is** the content) + a
  **denormalized** `TargetId` + `TargetKind` (the pointer is metadata for
  the link, not the content). **Not** a pointer document: the note does
  **not** store a link URL, and **no** other context reads the note to
  resolve the target.
- **D5 — Normalized tags on the note (M37·6).** A note may carry an
  **optional** `Tags` array (a **denormalized** set of tag names on the
  note — the **not-a-pointer** pattern, the M37·6 pin). **Not** a separate
  `TagAssignment` doc; **not** a reference to a `Tag` document.
- **D6 — A bounded folder tree (M37·1).** A `NoteFolder` is a second
  document on the same context: a **bounded** tree of folders (depth ≤ 2)
  that a note may optionally belong to. A **folder** has a `Name`, an
  `OwnerId`, and a `ParentId` (a self-reference for the depth ≤ 2
  tree). **Folder deletion moves the notes to the root** (no cascade
  delete — the notes survive their folder). See the handoff note for the
  full folder-tree detail.
- **D7 — Degrades, never leaks (M37·9).** When a target is **deleted**
  (a post removed, a group disbanded, a member leaving), the note's link
  **degrades gracefully**: the note survives, the link renders as a
  placeholder ("This link is no longer available"), and **nothing** in
  the note reveals the target's content beyond what the resident could
  already see. **The note is never auto-deleted** when its target is
  (M37·9).
- **D8 — No visibility toggle, no sharing (M37·2 / M37·4).** A note is
  **private** and **optional**: there is **no** "make this note visible
  to my group" toggle, **no** "share this note" link, **no** "pin this
  note to the group" surface. **No** resident is required to use the
  feature. The M37·4 "optional" pin is the **load-bearing** constraint.
- **D9 — The six-member close flip (M37·12).** On the **last** milestone
  ship, the close flip updates the **six** members: `Milestones.cs`
  (the `M37` entry → `StatusDone`, **no** successor),
  `MilestonesTests.cs` (the "last milestone has no successor" pin),
  [`README.md`](../../README.md), [`STATUS.md`](../STATUS.md), [`ARCHITECTURE.md`](../ARCHITECTURE.md) (the M37 row), and
  **`WhatsNew.cs`** (the **required sixth member**, ADR [0110](0110-platform-versioning-and-whats-new.md)). See the
  handoff note for the full close-flip detail.

## Consequences

- **Every resident gains a private, self-managed notes surface** — the
  M37·8 "self-managed" pin: create / edit / delete / move / rename
  folder, all in the resident's own session, no admin / moderator
  surface.
- **The notes are the resident's own words** (M37·5 "content, not a
  pointer"): a note is **not** a pointer to someone else's content, and
  **no** other context reads the note to resolve the target.
- **The platform's self-managed pattern (M37·3) extends to a 33rd context**
  with a **closed 9-kind** `TargetKind` set — the **only** kinds a note
  may target.
- **No `AccessAction` / `AccessVia` / `Decide()` / `IAuthorizationService`
  surface** (M37·3): the ownership check is the service comparing
  `OwnerId` to the authenticated user — the **simplest** possible
  access model.
- **No JS chart library, no new JS dependency** (M37·11): the
  `/personal-notes` surface is server-rendered, no build step.
- **No `AccessAudit` row on the read** (M37·12): the resident's own notes
  are not an **auditable** access (the M37·12 pin).
- **No EF migration** (no EF table added): the `PersonalNote` +
  `NoteFolder` docs are on the existing Marten doc-type surfaces
  (M37·10).
- **The follow-on lanes** (nudge, sharing, search, platform-wide tags,
  drag-drop, export) are **named deferrals** — see the handoff note for
  the full catalog.
