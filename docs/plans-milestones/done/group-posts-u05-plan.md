# U5 — Group posts: authorization group lane (`AccessVia.Group` + `CanSeeGroupAsync`)

**Milestone:** group posts · **Register:** `docs/plans-milestones/plan-group-posts.md` · **Read first

## Goal
The **new group lane** on the Authorization module — the one seam this milestone is allowed to add (ADR 0006-E compatible lane, ADR 0013): `AccessVia.Group` (the 8th enum value, the M1 `Admin` precedent), the two `CanSeeGroupAsync` overloads on `IAuthorizationService`, and the `AuthorizationService` implementation — delegation → member-lookup → **no** Moderate, **no** BreakGlass (G·4), audit rows always (G·5), live membership (C4).

## Entry reads
1. `docs/design/group-posts-design.md` §2.1 (frozen signatures + algorithm + audit shapes + deny-Via pin)
2. `src/Kumunita.Core/Authorization/IAuthorizationService.cs` (the frozen interface — the overloads to slot the ADDs next to, doc-comment style verbatim)
3. `src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia` enum — where `Group` goes, with an `Admin`-precedent doc comment)
4. `src/Kumunita.Core/Authorization/AuthorizationService.cs` (the **whole** file is the implementation — ~500 lines, fine in one read; find how `CanAsync`/`CanSeeAsync` build `Decision`, resolve delegation, read `GetGroupIdsAsync`, and write `AccessAudit` (both standalone-commit and the `IDocumentSession` overload shapes) — the group lane reuses **all** of that plumbing)
5. `src/Kumunita.Core/Authorization/AccessAudit.cs` (the row shapes: single-target vs aggregate) — to confirm the group-lane rows match, **read-only**

## Deliverables (3 files)
- `src/Kumunita.Core/Authorization/Decision.cs` — append `Group` to `AccessVia` with a doc-comment: "the group-lane standing (group posts milestone, ADR 0013): membership in the target group is the only lane that allows; the M1 `Admin`-value precedent — least-distortion slot, an additive enum value, frozen values untouched."
- `src/Kumunita.Core/Authorization/IAuthorizationService.cs` — append the two **exact** overloads from §2.1 with doc-comments anchored to G·1/G·4/G·5 and C4 (live `GetGroupIdsAsync` read, strong consistency) and the session-overload C3 note (mirroring the frozen overloads' doc style).
- `src/Kumunita.Core/Authorization/AuthorizationService.cs` — implement both:
  - resolve the acting principal (owner branch = the actor; a `read`-in-scope delegation ⇒ principal = owner, via `Delegation`; out-of-scope ⇒ Deny),
  - **live** membership read via the existing `IUserInfoService.GetGroupIdsAsync(principal)` (C4 — do not cache/proj ect),
  - Allow ⇒ `Via` = `Group` (or `Delegation` when the delegate branch is what put the principal in scope) per the §2.1 denial/Via pins, Deny ⇒ `Via` per the §2.1 pin,
  - the audit row **always** (Allow and Deny) — standalone commit for the non-session overload; the `IDocumentSession` overload appends into the caller's transaction (C3),
  - **no** `AccessAction.Moderate` path, **no** break-glass, **no** audience matching (G·4) — this is the *absence* U9's `PostService_MakesNoModerateOrBreakGlassCallOnGroupPosts` will assert at the service level.

## Exit
Handoff note (append to `docs/plans-milestones/in-progress/group-posts-handoff-notes.md`): `## U5
