---
id: adr-0118
title: "M17: Bookmarks (save posts, events, todos, etc. for quick personal access)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/design/m17-bookmarks-design.md
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0118 — M17: Bookmarks (save posts, events, todos, etc. for quick personal access)

Status: Accepted
Date: 2026-09-29

> **Abstract:** M17 makes a resident's quick-access list real — a standalone
> core surface over one document, one new surface, one service seam, and zero
> new authorization surface. A bookmark is a *personal-by-id* record (the
> ADR 0105 M9 shape), not an audience decision; it grants nothing and is a
> subset of the visible world. The option analysis, the amendment history
> (the `inventory` / `directory` / `project` / `board` kind expansions), and
> the test list live in the linked design doc. **Who is it for:** a developer
> touching `Kumunita.Core.Bookmarks` or the `/bookmarks` surface. **When to
> read the rest:** before adding a `TargetKind`, or before changing the
> write-lane visibility check.

## Context

The neighborhood has everywhere to **find** content but nowhere to say
*these are mine to come back to* (the README M17 row). The shape is already
proven by M5 (a standalone core surface on its own bounded context +
`*DocTypes` surface, ADR 0004 §B.1) and M9 (a *participant-by-id* record
whose access story is identity comparison, never an audience decision —
ADR 0105). The constraint: **compose the frozen seams, never extend them** —
no new `AccessAction`, `AccessVia`, `Decide()` branch, `IAuthorizationService`
method, or adapter. One rule keeps it safe: you may only bookmark **what you
can see**, and a bookmark **grants nothing**.

## Decision

- **D1 — One document, one new surface, one new bounded context.**
  `Kumunita.Core.Bookmarks` with one doc `Bookmark` (`OwnerId`, `TargetKind`,
  `TargetId`, `Created`) on a new `BookmarkDocTypes` surface (delta-detected,
  idempotent, **zero migrations**). `TargetKind` is a **string** on a closed
  set (the M5 `KanbanStatuses` shape); a unique index on
  `(OwnerId, TargetKind, TargetId)` is the idempotency witness.
- **D2 — A bookmark is a personal-by-id record, never an audience decision.**
  No adapter, no `AccessAction` / `AccessVia`, no `Decide()` branch, no
  `IAuthorizationService` method (the ADR 0105 shape). The owner's own list is
  a personal read: no `CanSeeAsync` pass, no `AccessAudit` row.
- **D3 — Bookmarking requires visibility; bookmarking grants nothing.**
  The write lane re-runs the target's **own** frozen `CanAsync(Read)` (the
  same call the detail page uses) and **refuses 404** (not 403) when the
  target is not visible.
- **D4 — One row per (owner, target); toggle = idempotent add / remove.**
  No `IsDeleted` flag — removal is physical; a re-bookmark is a fresh
  `Created` timestamp.
- **D5 — The list degrades, never leaks.** A row whose target is absent,
  soft-deleted, or no longer visible renders as the closed `bm.list.degraded`
  label (no title, link, or id) and remains removable.
- **D6 — M17 is a core surface, not a lane.** No off-by-default admin toggle
  (the ADR 0101 admin-toggle shape **inverted**).
- **D7 — No notification lane (deferred).** M17 composes only the frozen
  `IAuthorizationService` + the owning surfaces' read seams; the "nudge the
  author on bookmark" lane is a follow-on with its own ADR.

## Consequences

- A resident's quick-access list is real end-to-end; a dead pin degrades to
  the closed label and is still removable.
- **Zero new authorization surface — the strongest form:** the frozen seams
  are byte-identical after M17; the only authorization call is the D3
  write-lane check (the target's own frozen `CanAsync(Read)`).
- A pin is a **subset of the visible world, never a window into it** — a
  `GlobalAdmin` who is not the owner gets a non-leaky 404, no audit row.
- **Zero migrations** for existing surfaces; the new table + two indexes are
  additive.
- The leak surface of a dangling row is **exactly one static string**
  (the `bm.list.degraded` key) — a greyed-out bookmark is better than a
  silently-removed one, without leaking anything.
- The closed `TargetKind` set is deliberately **narrow, not open** — a new
  content surface joins with one vocabulary word + one list-resolution branch
  (the amendment history lives in the design doc).

**Follow-on lanes (each its own ADR):** the "nudge the author on bookmark"
lane (D7); bookmark folders / manual ordering; bookmark sharing; bookmarks
on groups themselves.
