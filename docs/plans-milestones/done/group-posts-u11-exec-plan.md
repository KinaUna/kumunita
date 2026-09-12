# U11 execution plan (working) — group posts: docs close + folder moves

> My (unit U11's) working plan for this pass. The **authoritative spec** is
> `group-posts-u11-plan.md` (the authored unit) + the **master register**
> `plan-group-posts.md` + the **U10 handoff section**. Doc unit — **no code
> semantics change, no tests**; `Kumunita.Web` is touched only in
> `Milestones.cs` (static data), so I still build to keep green.

## Entry reads (done)

- U10 handoff section (gate ALL THREE PASS; several plan files still in
  `in-progress/`; U7's note flags the stale 403 line in the archived
  `done/group-posts-u07-plan.md` as "left as-is for U11/U12").
- `README.md` — Roadmap (M0–M6) + Features (the **media precedent**: the
  shipped lane has **no** milestone letter, one bullet in **Features**, not
  in the M-named Roadmap list).
- `Milestones.cs` — lettered `Entry(Id, Title, Status)` list, M0–M6; **media
  has no entry here either** (it is not an M-lettered milestone).
- `ARCHITECTURE.md` — §4.2 `IAuthorizationService` sketch, §5 data model
  (`Post`, `PostReply`, `AccessAudit` via-list).
- `done/` — `group-posts-u01…u10-plan.md` **all present** ✓. `in-progress/`
  still holds: the handoff note (stays — living scratch), my own
  `group-posts-u11-plan.md` (moves at my Exit), `group-posts-u12-plan.md`
  (stays — U12's spec), and **nine stray `group-posts-uNN-exec-plan.md`
  working files (U1–U9)** — the media precedent archived its exec plans to
  `done/`; I reconcile them there.

## Deliverables (3 files, small additive edits)

1. **`README.md` — Features**: one new bullet, the media-line shape, **no
   milestone letter**: **Group posts** — the post channel *inside* each
   group (`/groups/{id}/posts`): members-only feed / detail / create /
   reply; membership is the sole access decision — no moderator or
   break-glass peek, no audience (ADR 0013). The M4/M5/M6 Roadmap list is
   **untouched** (media precedent: non-lettered shipped lanes live in
   Features, not the M-roadmap).
2. **`src/Kumunita.Web/Milestones.cs`**: one matching `Entry`, slotted after
   M3 (shipping order) with `StatusDone`: id `GP`, title
   "Group posts — the members-only post channel inside groups (ADR 0013)".
   README↔Milestones.cs contract: both name the same lane, both say done.
3. **`docs/ARCHITECTURE.md`** — two additive spots (the plan's (a)/(b)):
   - (a) §5 `Content` block: one comment line under `Post`/`PostReply` —
     group post = `Post.groupId` non-empty, membership sole lane, audience
     written empty, `componentId` empty (never in component feeds),
     `PostReply` unchanged (lane-neutral); ADR 0013 pointer.
   - (b) §4.2 `IAuthorizationService` sketch: one short comment block after
     the `VisibleSet` line — the group lane (`CanSeeGroupAsync` /
     `CanSeeGroupFeedAsync`, `AccessVia.Group`, in-scope `read` delegation
     ⇒ `Via Delegation`, **no** Moderator/BreakGlass branch, audit rows
     always); plus `Group` appended to the §5 `AccessAudit` via-list
     (the 8th value landed in U5 — the line is stale without it; U12's
     consistency check greps this).

## Folder moves (reconcile)

- `done/` u01–u10 plan files — **verified present, no move needed** ✓
- Move the nine stray `group-posts-u{01…09}-exec-plan.md` working files
  from `in-progress/` → `done/` (media precedent; U7's Exit-move debt).
- Handoff note **stays** in `in-progress/` (living scratch, never archived).
- `group-posts-u11-plan.md` (my spec) moves to `done/` **at my Exit**.
- `group-posts-u12-plan.md` stays (U12's spec file).

## Exit checklist

- [ ] Three doc edits landed (README / Milestones.cs / ARCHITECTURE.md)
- [ ] `dotnet build Kumunita.slnx -c Debug` green (Milestones.cs is code)
- [ ] `## U11 — …` section appended to the handoff note **before** the move
- [ ] `group-posts-u11-plan.md` moved to `done/`
- [ ] Nothing staged or committed — the user reviews first
