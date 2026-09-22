# U11 — Group posts: close the milestone (ARCHITECTURE.md / README / Milestones.cs + folder moves)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
Close the milestone's **docs ↔ code parity**, per the register: **README Roadmap**, **`Milestones.cs`** (its doc-comment names the README Roadmap as the source of truth — the two move together), and **`docs/ARCHITECTURE.md`** (the group channel now exists on Posts/Authorization — the bounded-context map and the persistence note must say so). Then the **folder moves**: verify every prior unit's plan file is already in `done/` and confirm the handoff note stays in `in-progress/` (it is living scratch, never archived). This unit edits **only** those three docs (plus the ADR index *if* U3's entry is missing) — and moves no code.

## Entry reads
1. `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` — **only the U10 section**
2. `README.md` — the **Roadmap** section only (the shipped-features pattern: how M3 / media were recorded — named lines, no milestone letter; group posts follows the media precedent)
3. `src/Kumunita.Web/Milestones.cs` (the home-page roadmap list + the doc-comment naming README as source of truth — read to pin the exact entry shape and where the group-posts line slots)
4. `docs/ARCHITECTURE.md` — the **Posts** and **Authorization** sections only (where the group lane + the `/groups/{id}/posts` surface get their two lines) + `docs/plans-milestones/done/` (`ls` — confirm `group-posts-u01-plan.md` … `group-posts-u10-plan.md` are all present)

## Deliverables (3 files, all small additive edits)
- `README.md` — Roadmap: add the **group posts** shipped line (membership-scoped channel: members-only feed/detail/reply under `/groups/{id}/posts`; no milestone letter — M4/M5/M6 stay Events/Projects/Portability). One line, the same shape as the media line.
- `src/Kumunita.Web/Milestones.cs` — the matching home-page entry (the README↔`Milestones.cs` contract; both must say the same thing).
- `docs/ARCHITECTURE.md` — two additive lines max: (a) **Posts/`Post`** — a group post is `Post.GroupId ≠ ""` with an empty `Audience`, exclusive to the group feed, lane-neutral `PostReply` (ADR 0013 pointer); (b) **Authorization** — the group lane: `AccessVia.Group` + the two `CanSeeGroupAsync` overloads, in-scope `read` delegation, **no** Moderate/BreakGlass on group posts, audit rows always.

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U11
