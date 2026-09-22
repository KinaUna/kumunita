# ADR 0030 — Role independence: elevated roles are composable, not mutually exclusive

Status: Accepted
Date: 2026-09-14

## Context

ADR 0003 defined three roles (Member, Moderator, GlobalAdmin) and ADR 0021 added a
fourth (Translator), and stated the intended standing of each. The **authorization and
read path** always treated a resident's roles as an independent *set*: the claim factory
(`KumunitaClaimsPrincipalFactory.BuildRoleListAsync`) mints a `Kumunita.Role` claim for
**every** role an account holds, `KumunitaPrincipal.RoleSet`/`HasRole`/`IsGlobalAdmin`/
`IsModerator`/`IsTranslator` each test for one role in isolation, and every feature gate
(`[Authorize(Roles = "GlobalAdmin,Translator")]`, announcement scopes, group lanes,
`ModeratorComponent` claims, …) composes them. Nothing in the running system ever
*required* exactly-one-elevated-role.

The mutual exclusivity existed only in the **write path** — the `/admin` role-assignment
UI. `SetRoleViewModel` carried a single `string Role`, `AdminController.SetRole`
normalized it to exactly one role, and `IIdentityService.SetRoleAsync(string role)`
forced it: its `else` branches *stripped* the other two roles every time. The consequence
the community hit in practice: **a GlobalAdmin could not also be a Translator or a
Moderator.** That broke a real, common need — someone standing in *temporarily* for
another resident (a departing admin, a translator on holiday, a moderator out for a
few weeks) while **keeping their existing standing**, so the arrangement can be toggled
back later without losing anything. The old shape made "add a second hat" impossible
without dropping the first.

## Decision

Roles are **independent and composable**: a resident may hold any subset of
`GlobalAdmin`, `Moderator`, and `Translator` at the same time. `Member` remains the
implicit verified-resident standing and is *not* one of the assignable elevated roles —
it is the "nothing else selected" state.

This is a **write-path-only** change; the read/authorization path already behaved this
way and is untouched:

- **The Core seam takes a *set*.** `IIdentityService.SetRoleAsync` now accepts
  `IReadOnlyCollection<string> roles` (the elevated roles to grant) instead of a single
  `string role`. `IdentityService.SetRoleAsync` computes a `wants` set from the three
  elevated roles (a stray `Member` is filtered out defensively), applies
  `AddToRole`/`RemoveFromRole` for each role *independently* (a role not in the set is
  removed, one in the set is kept/added), and folds any change into the same
  security-stamp rotation and the same single `AccessAudit` row as before. Component
  scope still applies exactly as before: the scope is the **complete** moderator scope
  and is cleared unless `Moderator` is in the set (invariant C5 / C4 unchanged).
- **The Web view model and controller pass the set through.** `SetRoleViewModel.Role`
  (a `string`) is replaced by `RoleNames` (a `string[]`); `AdminController.SetRole`
  filters to the three elevated roles, deduplicates, and hands the set to the Core seam
  (still a thin wrapper — all the real decision + audit lives in Core). The
  "Role updated." / error `TempData` behaviour is unchanged.
- **The `/admin` UI is checkboxes, not a single-select.** The one role `<select>`
  (Member / Moderator / Translator / GlobalAdmin) is replaced by three independent
  checkboxes (GlobalAdmin / Moderator / Translator), pre-checked to the account's
  current set. Nothing checked = a plain Member. The Moderator **scope** checkboxes
  remain, and are meaningful only when the Moderator checkbox is on (the Core seam
  already clears scope for a non-Moderator target — that behaviour is preserved, so
  the existing "scope is ignored for a non-Moderator" pin still holds).

## Consequences

Positive
- A GlobalAdmin can be a Translator (stand in for the community's translators), a
  Moderator can also be a GlobalAdmin, and so on — "add a second hat" now works, and
  removing one hat leaves the other intact. This is exactly the "stand in temporarily,
  then change it back easily" need the change was made for.
- The write path now matches the read path: roles were already a set everywhere
  authorization reads them; the assignment surface just can't keep pretending they are
  mutually exclusive.
- No change to the standing rules: role management is still GlobalAdmin-only
  (`RequireGlobalAdminAsync` is unchanged), scope is still a GlobalAdmin decision, the
  audit attribution (`via: Admin`, action `"role"`) and the security-stamp rotation are
  unchanged, and no new claim type / document / `AccessVia` member is introduced.

Negative / accepted risks
- The `/admin` row now carries three role checkboxes instead of one dropdown. A
  non-technical volunteer has one more control to reason about, but each box is a
  plain "on / off" for a role name they already know — strictly simpler than a
  dropdown-plus-a-scope-list, and it removes the surprising "selecting X silently drops
  Y" behaviour.
- `IIdentityService.SetRoleAsync`'s signature is breaking for any external caller. The
  only in-repo caller is `AdminController.SetRole` (updated); the tests were updated to
  the new set-based shape. Per ADR 0006 the module surface is "frozen; changes are
  breaking" — this is an intentional, documented breaking change (this ADR), not a
  silent one.
