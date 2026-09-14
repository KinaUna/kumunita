# GU U07 — Web: `GuardianController` + view models

> **Sealed unit.** One fresh agent, ~32K context window. This file is
> self-contained: it names every entry read, every deliverable, and the exit
> criteria. The register (`docs/plans-milestones/in-progress/
> plan-guardian-controls.md`) is the cross-reference; when the two disagree,
> **this file wins for what to do** and the register wins for *which files
> exist and in what order*.

## Goal

The Web **thin controller** + **view models** for the guardian surface (the
ADR 0012 / ADR 0013 thin-controller + one-additive-lane discipline): the
routes a guardian uses to add a child, list their children, suspend /
unsuspend, curate memberships, approve invitations, and dissolve. The
controller **delegates every write + standing decision to the Core seams**
(U04/U05/U06) — it does no standing logic itself; it resolves the actor's
`SubjectId` (the cookie principal, the single identity source) and passes
it as the `guardianId`/`actorId` argument. **No views** (U08), **no tests**
(U10). **G·1 in the Web: no route reads or links to a child's private
content** — the controller's only reads are the `GuardianLink` + the
child's *membership rows* (curation data), never their posts / profile body.

## Context you need (read these first, in this order)

1. `docs/design/guardian-controls-design.md` § **`## Pinned contract`** + the
   **Parts affected** → the Web surface (U01 pinned the route list + the VM
   field sets) — the *primary* source.
2. `docs/adr/0028-...md` §C (the five actions, each mapped to a route) + §D
   G·5 (the dissolve safety valve is **GlobalAdmin-only** in the Web — a
   guardian's own dissolve button is the `viaAdmin: false` path; the admin
   shell's is `viaAdmin: true` — but **this controller** only exposes the
   `viaAdmin: false` path; the admin shell is a **different** surface, out of
   scope here) — the ADR authority.
3. `src/Kumunita.Web/Controllers/GroupsController.cs` (~line 36–175) — the
   **thin-controller precedent**: `[Authorize] [Route("…")] public sealed class
   …Controller(IUserInfoService …, …) : Controller`; `private static string?
   SubjectId(ClaimsPrincipal user) => KumunitaPrincipal.SubjectId(user);`;
   the GET/POST form-pair shape (`Create()` returns `View(new …Model())`,
   `Create(model)` does the write, `ModelState.IsValid` first, `TempData
   ["info"]` + `RedirectToAction`); the `try/catch (UnauthorizedAccessException)`
   → 403 / 404 shape. U07 mirrors this **exactly**.
4. `src/Kumunita.Web/Models/GroupViewModel.cs` (top) — the **view-model shape
   precedent**: `public sealed record …ViewModel(…)` with a heavy XML
   doc-comment pinning the **exact field projection** ("exact N-field
   projection (…pin)"). U07's VMs follow this `sealed record` + doc-pin voice.
5. `src/Kumunita.Core/UserInfo/IUserInfoService.cs` § the GU seams (U04/U05/
   U06) — the **signatures the controller calls**: `CreateGuardianLinkAsync`,
   `SuspendChildAsync`, `UnsuspendChildAsync`, `DissolveGuardianLinkAsync`,
   `ApproveGroupInvitationAsync`, plus the **read lanes** the list/detail
   pages need (`GetGroupIdsAsync`, `GetCommunityIdsAsync`,
   `GetPendingInvitationsForUserAsync`, `GetProfileAsync`) — call **only**
   these.

## Deliverables (≤4 files)

### 1. `src/Kumunita.Web/Controllers/GuardianController.cs` (new)

`[Authorize] [Route("me/children")] public sealed class GuardianController(
IUserInfoService userInfo, IIdentityService identity) : Controller` (the
`identity` param is for the **add-a-child** lane's `RegisterAsync` — see
route 8; if U04–U06's seams already cover registration, drop it and note it
in the handoff).

`private static string? SubjectId(System.Security.Claims.ClaimsPrincipal user)
=> KumunitaPrincipal.SubjectId(user);` (the single identity source — never a
form field).

Routes (each a thin `SubjectId`-resolve → seam-call → `Redirect`/`View`, with
`[ValidateAntiForgeryToken]` on every POST and a `try/catch (InvalidOperationException)`
→ 400/409 + `[FromRoute]`/`[FromForm]` binding; mirror the `GroupsController`
voice):

1. **`GET me/children`** → `Index()` — the guardian's **own** child list
   (the `GuardianLink` rows where `GuardianId == SubjectId`, `Status == Active`,
   joined to each child's `Profile.DisplayName`). **No** child content.
2. **`GET me/children/{childId}`** → `Detail(childId)` — **one** child's
   curation view: their **group** memberships (`GetGroupIdsAsync`) +
   **community** memberships (`GetCommunityIdsAsync`) + their **pending**
   group invitations (`GetPendingInvitationsForUserAsync(childId)`). **G·1:
   no posts, no profile body** — the view shows membership *ids/names* only.
3. **`POST me/children/{childId}/suspend`** → `Suspend(childId)` — calls
   `SuspendChildAsync(childId, SubjectId)`; on success `TempData["info"]` +
   `RedirectToAction(nameof(Detail), new { childId })`.
4. **`POST me/children/{childId}/unsuspend`** → `Unsuspend(childId)` —
   `UnsuspendChildAsync(childId, SubjectId)`.
5. **`POST me/children/{childId}/memberships/group`** → `CurateGroup(childId,
   [FromForm] string groupId, [FromForm] bool add)` — `add ?
   AddGroupMemberAsync(groupId, childId, SubjectId) :
   RemoveGroupMemberAsync(groupId, childId, SubjectId)` (U05's guardian
   branch records `Via: Guardian` in Core — the controller just passes the
   actor).
6. **`POST me/children/{childId}/invitations/{groupId}/approve`** →
   `ApproveInvitation(childId, groupId)` — `ApproveGroupInvitationAsync
   (groupId, childId, SubjectId)`.
7. **`POST me/children/{childId}/dissolve`** → `Dissolve(childId)` —
   `DissolveGuardianLinkAsync(linkId, SubjectId, viaAdmin: false)` (the
   **guardian's own** path; the GlobalAdmin `viaAdmin: true` shell is out of
   scope). Requires the `GuardianLink` id — resolve it from the active
   (guardian, child) row first.
8. **`GET me/children` (form) + `POST me/children`** → `AddChild()` /
   `AddChild(AddChildForm)` — the **add-a-child** lane: `GET` returns
   `View(new AddChildForm())`; `POST` does `var child = await
   identity.RegisterAsync(displayName, email, password);` → `child.SubjectId`
   (the `ThinPrincipal.SubjectId` field — `RegisterAsync` returns
   `Task<ThinPrincipal>`) → **in the same request** `await
   userInfo.CreateGuardianLinkAsync(child.SubjectId, SubjectId)` (U04's
   formation seam — **one commit**, G·4). The usual verify-email flow is the
   child's own job after this (the `OutboxEmail` the `RegisterAsync` lane
   already stages); the controller does **not** verify.

**Every** route: `SubjectId` is resolved from the cookie principal (the
single identity source, never a form field); a null `SubjectId` is a 403 /
`UnauthorizedResult`. **No route** reads a child's posts, profile body, or any
audience-restricted content (G·1).

### 2. `src/Kumunita.Web/Models/GuardianViewModels.cs` (new)

`namespace Kumunita.Web.Models;` — `sealed record` VMs in the `GroupViewModel`
doc-pin voice (exact-field projections):

- `public sealed record ChildAccountItem(string ChildId, string DisplayName,
  bool Blocked);` (the `Index` row: `ChildId` is the route `{childId}`,
  `Blocked` drives the suspend/unsuspend button state).
- `public sealed record MembershipEditorModel(string ChildId,
  IReadOnlyList<string> GroupIds, IReadOnlyList<string> CommunityIds,
  IReadOnlyList<string> PendingInvitations);` (the `Detail` view: the three
  curation sets — **ids only**, G·1).
- `public sealed record PendingInvitationItem(string GroupId, string
  GroupName, string InvitedAt);` (one pending invite row on the child).
- `public sealed class AddChildForm { [Required] DisplayName; [Required,
  EmailAddress] Email; [Required, DataType(DataType.Password)] Password; }`
  (the add-a-child form — a `class` with `DataAnnotations`, not a `record`,
  since it's a form model, mirroring `GroupCreateModel`'s shape).

## Exit

`dotnet build Kumunita.slnx -c Debug` green. `GuardianController` compiles and
every route resolves the actor from the cookie principal (never a form field)
and delegates to a named Core seam; **no route reads a child's content**
(G·1). `GuardianViewModels.cs` compiles (4 VMs). **No views** (U08), **no
tests** (U10). Handoff note (append): 6–8 lines starting `## U07 —
GuardianController + view models` — (a) the route table (method + path +
seam, one line each), (b) a confirmation the add-a-child POST does
`RegisterAsync` + `CreateGuardianLinkAsync` in one request (G·4), (c) a
confirmation **no route reads child content** (G·1), (d) the 4 VM names +
their exact field sets, (e) whether `IIdentityService` was needed (and for
which route), (f) any compile warnings.
