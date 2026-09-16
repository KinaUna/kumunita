# U2 — Group posts: design doc Part 2 (seams, contracts, test list, gate, drift-guard)

**Milestone:** group posts · **Read first (5 min):** the master register's *Pinned contract* block (`docs/plans-milestones/plan-group-posts.md`) — you freeze it. **No repo-wide scan.**

## Goal
Append `## Seams & contracts (Part 2, written by U2)` to `docs/design/group-posts-design.md` — the **exact C#** U4–U9 must match (the master register's Pinned contract block is the *draft* — confirm it against real code; if you must deviate, the deviation is written here and a `## U2 — contract adjustment` handoff note records why), the 19 pinned test names, the three-test gate, and the **drift-guard**. **No code, no build.**

## Entry reads
1. `docs/design/group-posts-design.md` Part 1 (U1 — the invariant/FACE source)
2. `docs/plans-milestones/plan-group-posts.md` (master — Pinned contract + test list)
3. `src/Kumunita.Core/Authorization/IAuthorizationService.cs` (verbatim — the 4 frozen methods + session overloads to mirror) + `src/Kumunita.Core/Authorization/Decision.cs` (the 7-value `AccessVia` enum + `Decision`/`VisibleSet` shapes)
4. `src/Kumunita.Core/Authorization/AuthorizationService.cs` — **only** the decision-algorithm + audit-writing region (search for the owner-branch / break-glass / `MatchGroups` markers; ~200 lines, not the whole file)
5. `src/Kumunita.Core/Posts/PostService.cs` — **only** the doc-comment headers of `ListFeedAsync` / `GetPostAsync` / `CreatePostAsync` / `CreateReplyAsync` + the ctor line (the group-surface method shapes should read like siblings)
6. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` — the `GetGroupIdsAsync` doc (the live membership read the lane uses)
7. `docs/design/m3-posts-design.md` §2.5/§2.7 (the gate + drift-guard shape to emulate)

## Deliverables (1 append, same file)
`docs/design/group-posts-design.md`:
- `### 2.1 frozen seam list (exact C#)` — the group lane ADDs verbatim: the two `CanSeeGroupAsync` overloads, `AccessVia.Group` (the 8th value, the M1 `Admin`-precedent comment), the `PostService` group surface (3 methods + `GroupPostDraft`), the `Post.GroupId` line — with the **decision algorithm** spelled out: delegation (in-scope `read`, principal = owner) → owner-as-member → live `GetGroupIdsAsync(principal)` membership; **no** Moderate, **no** BreakGlass (G·4); and the **audit shape** (feed aggregate: TargetKind `"grouppost"`, TargetId null, counts; detail: TargetId = post id; deny rows' Via = `Group` — pin the deny-Via here or record the deviation).
- `### 2.2 new/changed Core types (exact C#)` — the `Post.GroupId` additive (with the "non-empty ⇒ group-post" G·2 comment to mirror M3b's `Status` ADD comment style), `GroupPostDraft`, the 3 `PostService` methods with doc-comments pinning G·1–G·8 as M3's `PostService` doc-comments do.
- `### 2.3 lane-exclusivity + reply-inherits rules` — the 2 tables: (a) group-post vs component-post (lane exclusivity, feed exclusion, authoring gate, audience-writes-empty), (b) replies (parent Allow ⇒ reply visible; parent Deny ⇒ reply not evaluated, no row).
- `### 2.4 acceptance gate (U10 records)` — closed loop / handoff / part-vs-whole per the master register.
- `### 2.5 pinned seam tests (exact names, 19)` — the 19 names from the master register, file `tests/Kumunita.Core.Tests/GroupPostServiceTests.cs` (the master list is authoritative; if U2 renames one, the master register's test table is the *only* file U2 may otherwise touch — and a handoff note is mandatory). This is the list unit-series rule 3 freezes against.
- `### 2.6 module-boundary impact notes` — UserInfo reads **unchanged** (the lane reuses `GetGroupIdsAsync`, no new UserInfo seam); Posts calls the two authorization ADDs in-transaction (C3); Web only calls `PostService` (no direct `IAuthorizationService` calls); the ADR 0013 cross-reference line.
- `### 2.7 drift-guard (frozen once written)` — the G·1–G·8 table, G1–G13, the U2 seam signatures, `Post.GroupId`, `GroupPostDraft`, the 19 test names.

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U2
