# U1 — Group posts: design doc Part 1

**Milestone:** group posts · **Read first (5 min):** `docs/plans-milestones/plan-group-posts.md` (master register — Understanding, Assumptions, Invariants G·1–G·8, FACES G1–G13) — this unit writes those into the design doc. **No repo-wide scan.**

## Goal
Author `docs/design/group-posts-design.md` Part 1 — **Context, Scope (in/out incl. the deferral list), Invariants (G·1–G·8), FACES (G1–G13)** — mirroring `docs/design/m3-posts-design.md` §1. **No code, no build.** Also **create** the handoff-note file and the in-progress note section.

## Entry reads (the template set — read in this order)
1. `docs/plans-milestones/plan-group-posts.md` (master — the source of truth for this unit)
2. `docs/design/m3-posts-design.md` §1 only (the Context/Scope/Invariants/FACES template to emulate — do NOT read the whole doc)
3. `docs/philosophy/how-it-works.md` (the "post to a group" promise this milestone settles + the moderator/privacy pitch)
4. `docs/adr/0010-private-groups.md` (why private groups can *never* be an audience — the core motivation), `docs/adr/0006-module-boundary-contracts.md` (C1–C6, D/E lanes), `docs/adr/0003-roles-and-moderator-scoping.md` (moderator-off; what G·4 must respect)
5. `src/Kumunita.Core/Posts/Post.cs` (the frozen POCO incl. M3b's `Status` ADD), `src/Kumunita.Core/UserInfo/Group.cs` (Group + GroupMembership shapes)

## Deliverables (2 files)
1. **New:** `docs/design/group-posts-design.md` (~220 lines). Sections:
   - `## Context` — M2+M2b shipped groups as membership/privacy/invitations; M3 shipped posts + audience; how-it-works.md already promises "post to a group" and "when membership changes, all your past posts reach exactly the right people"; what's missing is the group's *own channel*; the arrow moved (group = member channel, not just an access-reuse unit).
   - `## Scope` — **In:** `Post.GroupId` additive, the `IAuthorizationService` group lane (2 new overloads) + `AccessVia.Group`, `PostService` group surface (feed/detail/create), `GroupPostDraft`, Web channel surface (feed/detail/composer/reply under `/groups/{id}/posts`), 19 seam tests + recorded gate, ADR 0013. **Out (deferred list, each named):** moderation/report flow on group posts, notifications on new group posts, search over group posts, cross-posting into components, pagination UI beyond existing paging. **Explicitly NOT available (privacy-first):** break-glass on group posts, moderator peek, non-member authoring.
   - `## Invariants (pinned for group posts)` — G·1…G·8 verbatim from the master register (each with a one-line note on what unit pins where).
   - `## FACES (pinned, 13)` — G1…G13 table verbatim from the master register.
2. **New:** `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` — file header (one line: "rolling handoff notes, appended never rewritten") + `## U1 — design doc Part 1` section: 5–6 lines listing the 8 invariants by id, the 13 FACES, and the "break-glass is not a deferral" flag (so U3/U5 don't re-litigate).

## Exit
Both files exist with all sections; handoff-note entry written. **No build.** Then move this unit plan file: `docs/plans-milestones/in-progress/group-posts-u01-plan.md` → `docs/plans-milestones/done/`.
