# U5 execution plan (working) — group posts: authorization group lane

> My (unit U5's) working plan for this pass. The **authoritative spec** is
> `group-posts-u05-plan.md` (Goal / Entry reads / Deliverables / Exit) + the
> **master register** `plan-group-posts.md` (invariant G·1/G·3/G·4/G·5 pins) +
> the **design doc** `docs/design/group-posts-design.md` Part 2 **§2.1** (the
> frozen four-method group-lane ADD — U2 froze it under U2-A1/A2; U6–U12
> implement against it). Prior section read: **U4** (in
> `group-posts-handoff-notes.md`) — the lane's full C# contract is §2.1
> (+ the frozen decision algorithm and the two row shapes); the lane decides
> over `Post.GroupId` (U4, now in place) via the live
> `IUserInfoService.GetGroupIdsAsync` read (the lane owns its reads — ADR
> 0006-D); **no** moderator / break-glass branch (G·4 — standing "not
> available", not a deferral).

## Scope (in / out)
- **In (mine):** 3 files — all in `src/Kumunita.Core/Authorization/`:
  1. **Modify** `Decision.cs` — append the 8th `AccessVia` value `Group`
     (after the M1 `Admin` precedent), with the pinned doc-comment
     (group-lane standing, ADR 0013; least-distortion additive slot; frozen
     values untouched).
  2. **Modify** `IAuthorizationService.cs` — append the **four** exact
     overloads from §2.1 (the `CanSeeGroupAsync` pair with `targetPostId`
     + the `CanSeeGroupFeedAsync` pair with `candidateCount`), with the
     §2.1 doc-comments (anchored to G·1/G·4/G·5, C4 live membership, the
     session-overload C3 note) — mirroring the frozen overloads' style. The
     four frozen method signatures stay byte-untouched (ADR 0006-E lane).
  3. **Modify** `AuthorizationService.cs` — implement all four: the
     group-lane **core** (delegation → live member-lookup; **no** Moderate,
     **no** BreakGlass, **no** audience — G·4/G·1), the always-on audit row
     (Allow **and** Deny), standalone commit for the non-session overloads,
     the `IDocumentSession` overloads appending into the caller's
     transaction (C3).
- **Out:** everything else — no `Post.cs` change (U4, done), no
  `PostService` / `GroupPostDraft` (U6), no Web (U7/U8), **no tests** (U9),
  no `M3DocTypes.cs` change, no ADR edit, no doc file touched. The touched
  project is `Kumunita.Core` only.

## Constraints I keep pinned while I write
- **The exact signatures** (design doc §2.1 — frozen; any mismatch is a
  drift pause), parameter names and positions verbatim:
  - `Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId);`
  - `Task<Decision> CanSeeGroupAsync(string actorId, string groupId, string? targetPostId, IDocumentSession session);`
  - `Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount);`
  - `Task<Decision> CanSeeGroupFeedAsync(string actorId, string groupId, int candidateCount, IDocumentSession session);`
- **The decision algorithm** (the §2.1 core, frozen):
  ```
  grant = GetActiveGrantAsync(actorId)            // C2 scope = grants per action
  if grant != null && grant.Scope contains "read":
      principal = grant.OwnerId                    // in-scope read ⇒ OWNER's standing (G·6)
      isDelegated = true
  else:
      principal = actorId                          // out-of-scope acts as self (M1 C2)
      isDelegated = (grant != null)
  groups = GetGroupIdsAsync(principal)             // live membership (C4) — the PRINCIPAL's
  allowed = groups.Contains(groupId)
  via     = isDelegated ? AccessVia.Delegation : AccessVia.Group   // Allow **and** Deny
  return Decision(allowed, via, principal)
  ```
  - Absent **by contract** (G·4/G·1): no owner-skip branch (an author is
    allowed iff still a member — G4 FACES); **no** `HasBreakGlassAsync` /
    AdminOverride read; **no** `ModeratorAssignment` / `Component.ModeratorAccess`
    path; **no** `EvaluateAudience`. The action on every group-lane row is
    **"read"**.
  - The in-scope branch reads the **owner's** membership
    (`GetGroupIdsAsync(grant.OwnerId)`); the out-of-scope branch reads the
    actor's own. This mirrors the §2.1 core exactly (note the existing
    `ResolveActorAsync` reuses the *actor's* group set — the group lane
    loads the *principal's*; they are deliberately different).
- **Two row shapes** (the only two that exist; §2.1 + `AccessAudit.cs`):
  - **decision** shape (single-target — `CanSeeGroupAsync` pair):
    `TargetKind "grouppost"`, **`TargetId = targetPostId ?? groupId`**
    (detail ⇒ the post id, create-gate ⇒ the group id), `VisibleCount` /
    `HiddenCount` **null**, `Via` Group / Delegation, `Outcome` per
    membership.
  - **aggregate** shape (feed — `CanSeeGroupFeedAsync` pair):
    `TargetId` **null**, **`VisibleCount` / `HiddenCount`** =
    `(candidateCount, 0)` on Allow, `(0, candidateCount)` on Deny (G·5,
    C-M3·3 analog).
  - Always: `ActorId = actorId`, `EffectivePrincipalId = principle`
    (the owner when acting under delegation), `Action "read"`,
    `Id Guid ...ToString("N")`, `At` server time, `TargetKind "grouppost"`.
- **Transaction shape (C3):** the standalone (non-session) overloads open
  their **own** session, decide, `Store` the row, and `SaveChangesAsync` in
  one commit (the M1 `CanAsync` standalone precedent). The `IDocumentSession`
  overloads `Store` the row into the **caller's** in-flight transaction and
  let the caller commit — the row lands atomically with the domain write
  (the `CanAsync(..., session)` lane, ADR 0006-E compatible).
- **cref hygiene (build-green):** every new `<see cref>` in the three files
  resolves **now**: `AccessVia.Group` (I add it), the four new interface
  methods (I add them), `IUserInfoService.GetGroupIdsAsync` /
  `GetActiveGrantAsync`, `DelegationGrant`, `AccessAudit` — no forward
  reference to a not-yet-existing member (unlike U4's `CreateGroupPostAsync`
  which U6 adds; I avoid that case).
- **No drift:** if a file's state contradicts the §2.1 pin on any point
  not in my closed set above, I stop and record `## U5 — Drift pause` in
  the handoff note instead of improvising.

## Entry reads (done)
| Read | Why |
|------|-----|
| `docs/plans-milestones/in-progress/group-posts-u05-plan.md` | my sealed spec (Goal / Entry reads / Deliverables) |
| `docs/plans-milestones/in-progress/group-posts-u04-exec-plan.md` | the unit's working-plan shape I mirror + the precedent for the cref adaptation I avoid |
| `docs/plans-milestones/in-progress/group-posts-handoff-notes.md` (U4 section, prior) | U4's hand-off: the lane's full C# contract is §2.1 (the **4** group-lane methods per U2-A1/A2, not the register's 2-overload draft); `AccessVia.Group` appended **after** `Admin`; no moderator/break-glass branch; `Post.cs` not needed here |
the **frozen** exact C# for the four seams; the frozen decision algorithm (the grant/in-scope/out-of-scope/principal core); the two row shapes;
| `src/Kumunita.Core/Authorization/IAuthorizationService.cs` | the frozen interface — the overloads to slot the ADDs next to, the doc-comment style to mirror, the `IDocumentSession` C3 phrasing |
| `src/Kumunita.Core/Authorization/Decision.cs` | the `AccessVia` enum — where `Group` goes, the `Admin`-precedent doc-comment to mirror |
| `src/Kumunita.Core/Authorization/AuthorizationService.cs` | the **whole** file is the implementation: how `CanAsync`/`CanSeeAsync` build the `Decision`, open/commit the session (standalone vs `IDocumentSession` overload), store the `AccessAudit` row, and how `ResolveActorAsync` / `GetActiveGrantAsync` / `GetGroupIdsAsync` resolve delegation — the group lane **reuses the shape**, not the body (it never touches break-glass/moderation/audience). |
| `src/Kumunita.Core/Authorization/AccessAudit.cs` | the two row shapes to match (decision vs aggregate), readonly |
| `src/Kumunita.Core/Authorization/AccessAction.cs` | `AccessAction.Read` (`Id = "read"`) — the group lane's only action; `grant.Scope` is a list of action **ids** |
| `docs/adr/0013-group-posts-membership-lane.md` (U3) | the ADR that licenses exactly this ADD set (4 methods + `AccessVia.Group`); confirms break-glass/moderator are **not available**, not deferrals |

## Steps
1. Write this exec plan file (this step).
2. **Modify** `src/Kumunita.Core/Authorization/Decision.cs` — append
   `Group` as the **8th** value of `AccessVia`, **after `Admin`**, with the
   pinned doc-comment (group-lane standing, ADR 0013, group posts milestone;
   membership in the target group is the only lane that allows; the M1
   `Admin`-value precedent — an additive enum value, frozen values
   untouched).
3. **Modify** `src/Kumunita.Core/Authorization/IAuthorizationService.cs` —
   append the **four** exact overloads from §2.1 (with `<summary>`
   doc-comments mirroring the frozen overloads' style): the
   `CanSeeGroupAsync` pair (single-target, `targetPostId` ⇒ decision row,
   the G·3 create-gate session-lane note: row survives, Allow+row in one
   `SaveChangesAsync` (atomic, C3)) + the `CanSeeGroupFeedAsync` pair
   (whole-channel, `candidateCount` ⇒ aggregate row, the C3 session-lane).
4. **Modify** `src/Kumunita.Core/Authorization/AuthorizationService.cs` —
   implement all four:
   - a group-lane **core** method (delegation resolve → live
     `GetGroupIdsAsync(principal)` membership lookup → `Decision`; **no**
     break-glass, **no** moderation, **no** audience);
   - the two `CanSeeGroupAsync` overloads — standalone: open own session,
     decide, `Store` the **decision-shape** audit row, `SaveChangesAsync`;
     `IDocumentSession`: decide, `Store` into the caller's transaction;
   - the two `CanSeeGroupFeedAsync` overloads — same split;
     `Store` the **aggregate-shape** row
     (`VisibleCount` / `HiddenCount` from `candidateCount`, `TargetId`
     null).
5. **Build** — `dotnet build Kumunita.slnx -c Debug` (the runner path
   AGENTS.md pins); the touched project is `Kumunita.Core`.

## Exit (per the unit plan's "Exit" + the master workflow template)
- `Kumunita.Core` builds **green**; `IAuthorizationService` gained exactly
  four methods (the §2.1 frozen set), `Decision.AccessVia` gained exactly
  one value (`Group`, 8th, after `Admin`), and `AuthorizationService`
  implements them — the two `CanSeeGroup*` method **pairs** with
  `targetPostId` + the two `CanSeeGroupFeed*` method **pairs** with
  `candidateCount`; frozen signatures / frozen `Decision` /
  `AccessAudit` / `IUserInfoService` byte-untouched; **no**
  `AccessAction.Moderate` path, **no** break-glass, **no** audience
  evaluation on the lane.
- Handoff section (heading `## U5 — Authorization group lane`) appended to
  `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`
  **before** the plan file move.
- `group-posts-u05-plan.md` moved to `docs/plans-milestones/done/` (a plain
  file move — **not** staged, **not** committed; the user reviews
  everything first).

## Verification
- `Decision.cs` diff: one `Group` member appended after `Admin`; the other
  seven `AccessVia` values + `AccessOutcome` + `Decision` + `VisibleSet`
  byte-untouched.
- `IAuthorizationService.cs` diff: four new methods appended; the four
  frozen method signatures byte-untouched.
- `AuthorizationService.cs` diff: the group-lane core + four public
  methods added; the four frozen public methods, `Decide` / `DecideAsync`
  / `CanSeeInternalAsync` / `EvaluateAudience` / `ResolveActorAsync` /
  `HasBreakGlassAsync` / `ActorContext` byte-untouched.
- No new file created in the source tree; the three `.cs` files above are
  the only touched source files.
- Build output: solution builds green (zero errors; no new warnings from
  the new doc-comments).
- Handoff file gained exactly one `## U5` section; U1–U4 sections
  byte-untouched.
- `git status --short` (no `git add`): the three `.cs` files (modified),
  the exec plan (new, untracked), `group-posts-u05-plan.md` (moved to
  `done/`) — **nothing** staged, **no** commit.
