# U6 execution plan (working) — group posts: `PostService` group surface (feed + detail + create)

> My (unit U6's) working plan for this pass. The **authoritative spec** is
> `group-posts-u06-plan.md` (Goal / Entry reads / Deliverables / Exit) + the
> **master register** `plan-group-posts.md` (invariant G·1–G·8 pins) + the
> **design doc** `docs/design/group-posts-design.md` Part 2 **§2.2** (the three
> frozen `PostService` group-surface methods + `GroupPostDraft` + the
> Shapes, U2 froze them; §2.1 is the lane U5 landed). Prior section read:
> **U5** (in `group-posts-handoff-notes.md`) — the group lane is **live** and
> `Post.GroupId` (U4) is in place; U6 composes them, never re-derives access
> (the lane owns its reads — ADR 0006-D).

## Scope (in / out)
- **In (mine):** 2 files — the **closed Deliverables set** (≤ 2 files, no
  cleanups):
  1. **New** `src/Kumunita.Core/Posts/GroupPostDraft.cs` — the frozen input
     record: `public sealed record GroupPostDraft(string GroupId, string? Title, string Body);`
     — **no `Audience` member** (G·8): the service writes it non-null **empty**.
  2. **New** (appended) `src/Kumunita.Core/Posts/PostService.cs` — the three
     group-surface methods, **exact** shapes per §2.2:
     - `ListGroupFeedAsync(string groupId, string actorId, int page)` — one
       **standalone** `CanSeeGroupFeedAsync` (the aggregate feed row, G·5) over
       the paged `GroupId == groupId` candidate set; Allow ⇒ the candidates,
       Deny ⇒ empty list + `HiddenCount = candidates.Count`; **0 candidates ⇒**
       empty result, no decision, no row; **no** audience evaluation (G·1/G·8).
     - `GetGroupPostAsync(string groupId, string postId, string actorId)` —
       load-by-id + the fail-closed shapes (missing / empty `GroupId` /
       `GroupId != groupId` ⇒ `(null, [])` + no row); else one **standalone**
       `CanSeeGroupAsync(targetPostId = postId)` (the detail decision row,
       TargetId = postId, G·5); Allow ⇒ replies **as-is** (G·7, no second
       pass); Deny ⇒ `(null, [])`.
       - one **session-variant**
         `CanSeeGroupAsync(actorId, draft.GroupId, targetPostId: null, session)`
         — the **create gate is the group-lane decision** (G·3): Deny ⇒
         `SaveChangesAsync()` (persist the row) **then**
         `UnauthorizedAccessException`; Allow ⇒ build the
         `Post` (`ComponentId = string.Empty` G·2, `GroupId = draft.GroupId`,
         `Audience = new Audience()` G·8, …) + store + **one** `SaveChangesAsync()`
         (the gate row + the post commit atomically, C3).
        `ActorId`/`EffectivePrincipalId` is the owner when delegated (G·6);
       **the `actorRoles` parameter does not exist** here (contrast
       `CreatePostAsync` — a non-member GlobalAdmin is **denied**, G8 FACES/G·4),
       no break-glass, no membership read (the lane owns its reads — ADR 0006-D).
- **Out:** everything else — no `Post.cs` change (U4, done), no
  `Authorization/*` change (U5, done), no Web (U7/U8), **no tests** (U9), no
  `M3DocTypes.cs` change, no ADR edit. The touched project is `Kumunita.Core`
  only. `CreateReplyAsync` / `HidePostAsync` / `RemovePostAsync` / `GetPostAsync`
  / the two M3 feeds are **byte-untouched** (G·7 reply-inheritance reuses the
  existing lane-neutral `CreateReplyAsync`; the two M3 feeds already filter on
  `ComponentId` and a group post writes it empty, so G12/G13 hold **structurally**
  — U6 adds **no** filter, design doc §2.3(a)).

## Constraints I keep pinned while I write
- **The exact signatures** (design doc §2.2 + §2.1 — frozen; any mismatch is a
  drift pause), parameter names and positions verbatim:
  - `Task<FeedResult> ListGroupFeedAsync(string groupId, string actorId, int page);`
  - `Task<PostDetailResult> GetGroupPostAsync(string groupId, string postId, string actorId);`
  - `Task<Post> CreateGroupPostAsync(GroupPostDraft draft, string actorId, IDocumentSession session);`
  - `public sealed record GroupPostDraft(string GroupId, string? Title, string Body);`
- **The lane calls** (design doc §2.1, U5 landed them byte-exact):
  - feed ⇒ `CanSeeGroupFeedAsync(actorId, groupId, candidates.Count)` (standalone —
    a plain read, no caller transaction; the `ListFeedAsync` precedent).
  - detail ⇒ `CanSeeGroupAsync(actorId, groupId, postId)` (standalone — the detail
    decision row, TargetId = the post id, tests #17/#18).
  - create gate ⇒ `CanSeeGroupAsync(actorId, draft.GroupId, null, session)` (the
    session overload — the row is stored in the caller's transaction; TargetId =
    the group id, the channel as the gate's target; the row must **survive** the
    Deny throw — G6 FACES).
  - Absent by contract (G·4/G·1): **no** audience-lane `CanAsync` / `CanSeeAsync`
    call, **no** `AccessAction.Moderate`, **no** break-glass, **no**
    membership read — the service calls **only** the group-lane methods (test #19
    pins that at the seam level; G7/G8 at the decision level).
- **Result shapes** (reused, no new records): `FeedResult(Visible: paged,
  HiddenCount, Page, Total)` and `PostDetailResult(Post?, Replies)` — the M3
  detail/reply shape (`Query<PostReply>().Where(r => r.PostId == postId)
  .OrderBy(r => r.Created)` returned **as-is**; no reply-level evaluation, G·7).
- **Write pins** (G·2/G·8): the `Post` is written with
  `ComponentId = string.Empty`, `GroupId = draft.GroupId` (non-empty, enforced
  before any decision), `Audience = new Audience()` (non-null, **empty**),
  `AuthorId = actorId`, `Id = Guid…("N")`, `Created = UtcNow`. Written **only**
  here.
- **Guard style** (mirror the siblings verbatim — the same null-actor message,
  the same `page < 1 ⇒ 1` clamp, the same `ArgumentException` style):
  - feed/detail: `string.IsNullOrEmpty(groupId/postId) ⇒ throw` + the
    `actorId` guard; `page < 1 ⇒ 1` (feed only).
  - create: `draft`/`session` null-checked, `actorId` guarded, `draft.GroupId`
    non-empty enforced (**before** any decision — no row for such input; G·3).
- **cref hygiene (build-green):** every new `<see cref>` resolves **now**:
  `IAuthorizationService.CanSeeGroupFeedAsync(string, string, int)`,
  `IAuthorizationService.CanSeeGroupAsync(string, string, string?)`,
  `IAuthorizationService.CanSeeGroupAsync(string, string, string?, IDocumentSession)`,
  `FeedResult`, `PostDetailResult`, `Post`, `PostReply`, `GroupPostDraft`,
  `UnauthorizedAccessException`, `Kumunita.Core.Authorization.Audience` (fully
  qualified in the new record's doc-comment) — **no** forward reference to a
  not-yet-existing member.
- **No drift:** if the current `PostService` / `IAuthorizationService` state
  contradicts the §2.1/§2.2 pin on any point not in my closed set above, I stop
  and record `## U6 — Drift pause` in the handoff note instead of improvising.
- **`Post.cs` cref note (NOT a U6 edit):** U4 left
  `<c>PostService.CreateGroupPostAsync</c>` (a `<c>` literal) in `Post.cs`
  because the member did not exist when U4 wrote it — **I am now landing that
  member** (so the build stays green either way). Upgrading that one `<c>` →
  `<see cref>` in `Post.cs` is **deferred to U12** (the "Final consistency
  check" unit): U6's Deliverables are pinned to ≤ 2 files and `Post.cs` is not
  one of them (unit-series rule 1). Flagging it here so it is not lost.

## Entry reads (done)
| Read | Why |
|------|-----|
| `docs/plans-milestones/in-progress/group-posts-u06-plan.md` | my sealed spec (Goal / Entry reads / Deliverables) |
| `docs/plans-milestones/in-progress/group-posts-u05-exec-plan.md` | the unit's working-plan shape I mirror + the cref-adaptation precedent I avoid (I never forward-reference) |
| `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` (U5 section, prior) | U5's hand-off: the lane is **live**; create gate = `CanSeeGroupAsync(targetPostId:null, session)`; feed → `CanSeeGroupFeedAsync`; detail → `CanSeeGroupAsync(targetPostId=postId)`; `GroupPostDraft` writes `GroupId` non-empty, `ComponentId` empty, `Audience` non-null empty (G·2/G·8); no moderator/break-glass branch |
| the **frozen** exact C# `C#` shapes + the per-method doc-comments + the "Shapes (U6 implements exactly these)" list; the two §2.3 rule tables (lane split + reply inheritance) |
| `src/Kumunita.Core/Posts/PostService.cs` | **the whole file**: `ListFeedAsync` / `ListAllFeedAsync` (the feed shape I mirror — same guards, same `QuerySession`, same `PageSize`), `GetPostAsync` (the detail/replies shape I mirror — fail-closed `Post=null`, replies as-is), `CreatePostAsync` (the create-guard + `store`/`SaveChangesAsync` + `UnauthorizedAccessException` style I mirror) |
| `src/Kumunita.Core/Posts/Post.cs` | U4's `GroupId` field (non-nullable, default `string.Empty`, after `Status`); `Audience` is non-null — the `Post` I write; readonly (I do **not** modify it) |
| `src/Kumunita.Core/Posts/{PostDraft,FeedResult,PostDetailResult}.cs` | the `GroupPostDraft` shape to mirror (`PostDraft`) + the two result records I return **verbatim** (reuse, no new shapes) |
| `src/Kumunita.Core/Authorization/{IAuthorizationService,Decision}.cs` | U5's group-lane overloads (the exact call shapes) + the `Decision.Allowed` field I branch on |
| `src/Kumunita.Core/Authorization/Audience.cs` | confirms `new Audience()` is non-null + **empty** (G·8) — `Mode = Any`, `Grants = empty` |

## Steps
1. Write this exec plan file (this step).
2. **Create** `src/Kumunita.Core/Posts/GroupPostDraft.cs` — the frozen record
   (3 positional members; **no** `Audience`) with the §2.2 doc-comment (G·2/G·8,
   `<see cref>` only on members that resolve now).
3. **Append** to `src/Kumunita.Core/Posts/PostService.cs` — a section marker +
   the three methods, **exact** shapes per §2.2, siblings' guard/error/doc-comment
   style, **only** the group-lane authz calls (no audience/moderate/break-glass):
   `ListGroupFeedAsync` → standalone `CanSeeGroupFeedAsync`;
   `GetGroupPostAsync` → load + fail-closed + standalone `CanSeeGroupAsync(postId)`
   + replies as-is; `CreateGroupPostAsync` → session-variant `CanSeeGroupAsync(null,
   session)`, Deny ⇒ persist row **then** throw, Allow ⇒ build post + store + one
   `SaveChangesAsync`.
4. **Build** — `dotnet build Kumunita.slnx -c Debug` (the runner path AGENTS.md
   pins); the touched project is `Kumunita.Core`.
5. **Regress** — `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
   (the reliable runner, AGENTS.md quirk) to confirm the 235 inherited
   M1/M2/M3/M3b/media seam tests still pass (U5 baseline) — no new tests added
   (U9 owns the 19 group seam tests).

## Exit (per the unit plan's "Exit" + the master workflow template)
- `Kumunita.Core` builds **green**; `PostService` gained exactly **three** public
  methods (the §2.2 frozen set) + `GroupPostDraft` (new record, no `Audience`);
  `ListGroupFeedAsync` / `GetGroupPostAsync` / `CreateGroupPostAsync` issue
  **only** the group-lane calls (no `CanAsync`/`CanSeeAsync`/`Moderate`/break-glass
  path — G·1/G·4/test #19); `Post` written with `ComponentId` empty + `GroupId`
  non-empty + `Audience` non-null empty (G·2/G·8); the create-gate Deny persists
  its row **before** the throw (G·3/G6); the two M3 feeds + `CreateReplyAsync` +
  `GetPostAsync` + `Hide/RemovePostAsync` **byte-untouched** (G·7); **no** new
  file in the source tree beyond `GroupPostDraft.cs`.
- Handoff section (heading `## U6 — PostService group surface`) appended to
  `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` **before**
  the plan file move.
- `group-posts-u06-plan.md` moved to `docs/plans-milestones/done/` (a plain
  file move — **not** staged, **not** committed; the exec-plan stays here in
  `in-progress/`, the u01–u05 group-posts precedent).

## Verification
- `GroupPostDraft.cs`: new file, one record, three positional members
  (`GroupId`, `Title?`, `Body`), **no** `Audience` member; doc-comment `<see
  cref>` all resolve; no forward reference.
- `PostService.cs` diff: **three** methods + one section marker appended; the
  seven pre-existing methods, the ctor, `PageSize`, and the `using` directives
  byte-untouched; `CreateReplyAsync` / `HidePostAsync` / `RemovePostAsync` /
  `GetPostAsync` / the two M3 feeds untouched.
- Build output: solution builds green (zero errors; **no** new warnings from
  the new doc-comments).
- `Kumunita.Core.Tests.dll` run green (the 235 inherited tests still
  pass — no group seam tests yet; U9 adds them).
- Handoff file gained exactly one `## U6` section; U1–U5 sections byte-untouched.
- `git status --short` (no `git add`): the new `GroupPostDraft.cs`,
  `PostService.cs` (modified), the exec plan (new, untracked),
  `group-posts-u06-plan.md` (moved to `done/`) — **nothing** staged, **no**
  commit.
