# ADR 0120 — Guest accounts (limited-privilege, time-bounded accounts on the M1 identity surface)

Status: Accepted
Date: 2026-09-30

The README roadmap names M19 — the **last** milestone in `Milestones.All` —
exactly: "**Guest accounts**: limited-privilege accounts for consultants,
coaches, teachers, speakers, entertainers, and similar community-adjacent
roles who need a place in the platform without full resident standing;
**admins set the limits on what a guest may access and for how long**."
"admins set" is the **admin-settled standing** (D1/D2/D6); "what a guest may
access" is a **closed surface set with an empty floor** (D4); "for how long"
is a **bounded window on the allowance, evaluated per request** (D3). M19
adds the *outside* of the resident circle: a limited, bounded guest standing.

This ADR rides **frozen** seams, extending none of them:

- **The M1 identity surface (ADR 0001)** — the thin-token / fat-authorization
  split (ADR 0001-B), the additive standing fields on `Profile`
  (`Verified` / `Blocked` — the ADR 0004 §B.1 additive-field precedent), and
  the claim-mint at the Identity↔cookie seam (`KumunitaClaimsPrincipalFactory`).
- **The frozen `IAuthorizationService` + `Decide()` (ADR 0006)** — M19 adds
  **no** new `AccessAction`, **no** new `AccessVia`, **no** new adapter,
  **no** branch in `Decide()` (C-M19·6).
- **The composable-role / claim-append precedent (ADR 0030)** — D5's
  `Roles.Guest` is a new claim *string* on the existing `Kumunita.Role`
  claim *type*; the `ClaimTypes.All` set is **untouched**.
- **The admin-settled policy + one audited write lane + `/admin` controller
  (ADR 0050)** — D2's `SetGuestAccessAsync` mirrors
  `SetSignupOpenAsync` exactly (load-or-create, store, one `AccessAudit` row
  `Via = Admin` in the same session); D6's `/admin/guests` mirrors
  `AdminSignupController` (dedicated controller, gate checked first on GET
  + POST, affordances hidden-not-disabled).

## Context

A guest is an *outside* person — a consultant, coach, teacher, speaker,
entertainer — whom a resident invites into a **limited, time-bounded** slice
of the platform. The M1 identity surface already ships everything a guest
needs to *be a real actor*: a real `User`, a real `Profile`, a real
`SubjectId`, the thin-token / fat-authorization split, the
`Verified`/`Blocked` additive standing fields, the claim-mint at the
Identity↔cookie seam, and the `AccessAudit` lane. ADR 0050 already ships the
**admin-settled policy + one audited write lane + `/admin` controller**
shape. What the surface does *not* have is the *outside* of the resident
circle: a standing that is (a) **not** `Member` (the guest is outside the
circle), (b) **limited** to a closed set of read surfaces (an admin opts a
guest *in* to specific surfaces, never to full membership), and (c) **bounded
in time** (live only while `ValidFrom ≤ now < ValidUntil`).

The constraint that shapes the decision is the same one that shaped ADR
0030 and ADR 0050: **compose the frozen seams, never extend them.** The
guest is a *standing on the account* (an additive `Profile` flag + one small
allowance document), not a new entity, not a parallel store, not a new
bounded context (D1). The guest's "what may I see" is answered per request by
the **same** `CanAsync`/`CanSeeAsync` calls a resident uses — the guest
simply carries a different standing (no `Member`, the `Guest` claim + the
allowance set) into those decisions (D5). The "when" is **data** on the
allowance (a bounded window read at the seam), not config and not a
background job (D3 — the ADR 0050 "data, not config" posture). The one rule
that keeps M19 safe (C-M19·6): **zero new authorization surface** — the
frozen `IAuthorizationService` seams, the audit log, and the resident
standing are byte-identical after M19.

## Decision

**D1 — The guest is a standing on the account, not a new `Profile`
subclass.** A guest is expressed as an additive flag on the existing
`Profile` (`bool IsGuest`, additive, ADR 0004 §B.1 — the `Blocked`/`Verified`
field precedent) **plus** a dedicated admin-settled **allowance document**
(D2). The guest is still a real Identity account (real `User`, real
`Profile`, real `SubjectId`) so the frozen authorization seams and the audit
log treat the guest exactly like any other actor. *Forbids:* a separate
`GuestProfile` document type, a parallel account store, or any new bounded
context for guests — the guest is a standing, not a new entity.

**D2 — The allowance is an admin-settled, time-bounded document, one audited
write lane.** A new document `Kumunita.Core.Identity.GuestAccess` (id = the
guest's `SubjectId`) carries the *limited* standing: the bounded window
(D3), the closed surface set (D4), and the settling admin. One **single
audited write lane** on `IIdentityService`
(`SetGuestAccessAsync(GuestAccess, string adminSubjectId)`) — the exact
shape of ADR 0050's `SetSignupOpenAsync` (load-or-create, store, one
`AccessAudit` row `Via = Admin` in the same session) — plus a plain read
(`GetGuestAccessAsync`) the claim mint (U02) and the admin surface (U04)
ride. The Web boundary owns the GlobalAdmin standing check; the Core seam
does not re-check `User`. *Forbids:* a free-form string list for
`AllowedSurfaces` (it must be a closed enum so a typo cannot silently grant a
surface), a second write lane, or an unaudited direct
`IDocumentSession.Store` from a controller.

**D3 — "When" is a bounded window on the allowance, evaluated per request.**
The standing is live only while `ValidFrom ≤ now < ValidUntil`. A guest
outside that window has **no standing** (the factory mints no `Guest` role),
exactly as an unverified account is denied `Member`. No expiry job, no
projection: the window is read at the Identity↔cookie seam
(`KumunitaClaimsPrincipalFactory`), the same place `Verified`/`Blocked` are
consulted. The "when" is settled by *data*, not config, and not a background
job (the ADR 0050 "data, not config" posture). *Forbids:* a background
"expire the guest" job, a projection that lags the window, or an unbounded
guest standing (no `ValidUntil`).

**D4 — The allowed surface set is a closed enum (the floor is empty).** A
guest may be allowed onto a **closed, enumerated** set of read surfaces
(`Announcements`, `Events`, `Directory`); each surface is a distinct value,
an admin composes a set, and a *typo or a wrong value grants nothing*. The
floor (empty set) is the least-privileged default — a guest account with no
surfaces is a signed-in shell, not an error. *Forbids:* free-form surface
strings, a boolean-per-surface field on `Profile`, or granting `Member` to
make a surface "just work."

**D5 — The guest standing is a claim minted at the Identity↔cookie seam,
riding the frozen authorization.** `Roles.Guest = "Guest"` is added to the
`Roles` static class (the ADR 0030 composable-role / claim-append precedent —
a new claim string, the existing claim set untouched in shape; the
`ClaimTypes.All` set is **untouched** — a guest's `Guest` role rides the
existing `Kumunita.Role` claim *type*, a value not a new type).
`KumunitaClaimsPrincipalFactory` mints `Guest` **iff** the account is a
guest (`Profile.IsGuest`), not blocked, and the allowance window is live
(D3). A guest **does not** get `Member` (D4 floor) and **does not** get any
community standing. The frozen `IAuthorizationService` seams are *untouched*.
*Forbids:* a new branch in `Decide()` for guests, an extended `CanAsync`
signature, or encoding guest standing in a relational table.

**D6 — The admin surface is `/admin/guests`, GlobalAdmin-gated, the ADR 0050
precedent.** A dedicated `AdminGuestsController` — **not** a new action on
`AdminController` (whose constructor is pinned by two Web-layer test
harnesses) — mirroring `AdminSignupController`: `[Route("admin/guests")]`,
`[Authorize(Roles = Roles.GlobalAdmin)]`, a thin list / create / set-window /
set-surfaces surface over `IIdentityService.SetGuestAccessAsync`. The gate is
checked **first** on both GET and POST; affordances on `/admin` index are
**hidden-not-disabled** for non-GlobalAdmin actors. *Forbids:* a new action
on the fat `AdminController`, a self-service guest creation path, or an admin
surface that writes the allowance without a GlobalAdmin standing check.

**D7 — Deferred lanes (each a future ADR, listed here so a unit does not
reach for them).** (1) Per-guest content *authors* (a guest posting into a
group / an event note) — M19 is *read-only standing*. (2) Guest *invitations*
(a resident invites a guest, the guest self-serves a password from an
admin-sent link) — the ADR 0050 invitation-mechanism follow-on applied to
guests. (3) *Per-surface fine-grained control* (a guest sees *some*
announcements but not *some* events) — per-resource grants ride the existing
audience machinery. (4) Guest *notification / messaging* (the M6 / M9
surfaces). (5) Guest *analytics / audit depth* (a dedicated guest-usage
report on the M13 surface). *Forbids:* a unit implementing any of these
inside an M19 unit.

## Consequences

**Positive:**

- **A limited, time-bounded guest standing is real** (the README M19 row,
  end-to-end): a GlobalAdmin settles the window + the closed surface set on
  one audited write (D1/D2/D6, GATE-3's
  `SetGuestAccess_Stores_One_Document_And_One_Audit_Row` pin), the guest
  signs in to a limited shell (D5 — GATE-1's
  `Guest_Within_Window_Mints_Guest_Not_Member` pin), and the standing is
  live only inside the window, settled by data and read per request (D3 —
  GATE-1's `Guest_Outside_Window_Mints_No_Guest` pin). No restart, no job.
- **Zero new authorization surface — the strongest form** (C-M19·6): M19 adds
  **no** new `AccessAction`, **no** new `AccessVia`, **no** new adapter,
  **no** branch in `Decide()`, **no** new `IAuthorizationService` method,
  **no** new claim *type* (D5 — the `Roles.Guest` claim rides the existing
  `Kumunita.Role` claim *type*; the `ClaimTypes.All` set is **untouched**).
  The frozen `IAuthorizationService` seams, the `AccessAudit` lane, and the
  resident standing are **byte-identical** after M19 (the GATE-4's
  `Claim_Shape_Pin_Only_New_Claim_String_Is_Guest` pin, the §drift-guard).
- **The guest is auditable like any actor** (C-M19·5): the allowance write
  appends exactly one `AccessAudit` row (`Via = Admin`,
  `Action = "guest.set-standing"`); the guest's content reads append the same
  `AccessAudit` rows a resident's reads do (the frozen seams, untouched). No
  unaudited access (ADR 0006-E).
- **The floor is least-privileged, not an error** (C-M19·4, D4): a guest with
  no surfaces signs in to a shell with no gated content — the closed
  `GuestSurface` set floors to nothing, and a typo / unknown value grants
  nothing (GATE-2's `GetGuestAccess_With_No_Allowance_Returns_Null` pin).
- **Zero migrations** (D1/D2): the one additive `Profile.IsGuest` `bool` and
  the new `GuestAccess` document are delta-detected and applied
  idempotently at boot (the ADR 0004 §B.1 additive-surface shape) — every
  existing account reads `IsGuest = false` (the pre-M19 state, unchanged).

**Neutral / cost:**

- **The named trade — a *coarse, closed* surface set for zero new
  authorization surface:** M19 buys a limited, time-bounded, admin-settled
  guest standing with **one additive `Profile` flag + one small document +
  one enum + one claim**, in exchange for a **coarse, closed surface set**
  (D4) — a guest gets "the announcements surface" or "the events surface,"
  not "announcement #42 but not #47." The per-resource fine-grain (D7 lane 3)
  and the guest-as-author (D7 lane 1) are deliberately *not* in M19: they are
  larger designs that need their own ADRs, and M19's value (a
  consultant/coach/teacher who can *see* the feed they're engaged with, for a
  bounded window, with nothing to lose if they're not) is delivered by the
  closed set.
- **One extra mt read on the guest path only** (D5): the claim-mint reads the
  `GuestAccess` window **only** when `Profile.IsGuest == true` — non-guests
  short-circuit before the read (C-M19·6's "zero new reads on the non-guest
  path"). The `now` is `DateTimeOffset.UtcNow` — the window is evaluated per
  request at the seam (D3, no job, no projection).
- **The `AllowedSurfaces` shape is a `[Flags]` scalar** (D2, the
  design-doc §1.a amendment): the register's prose showed `GuestSurface[]`
  for readability; the closed bit-flag union (D4) is the idiomatic C#, so the
  document carries a single scalar `GuestSurface AllowedSurfaces` (defaulting
  to `GuestSurface.None`, the empty floor). A representation annotation only
  — the D4 *decision* (closed set, empty floor, a new surface is a new enum
  value) is unchanged.

**Follow-on lanes (each its own ADR — the design doc's §deferred):**
per-guest content authors (D7 lane 1); guest invitations (D7 lane 2);
per-surface fine-grained control (D7 lane 3); guest notification / messaging
(D7 lane 4); guest analytics / audit depth (D7 lane 5).

## Amendments

- **2026-09-30 — D2 `AllowedSurfaces` shape annotation: array → `[Flags]`
  scalar.** The register's D2 prose block originally read
  `GuestSurface[] AllowedSurfaces`. The `GuestSurface` enum is `[Flags]` (a
  closed bit-flag union — D4), so the document carries a single scalar
  `GuestSurface AllowedSurfaces` (defaulting to `GuestSurface.None`, the
  empty floor) rather than an array. U00 pinned the scalar form in the
  design doc's §4 + §9 (the authoritative C#) and U01/U02/U04 all implement
  it. **The D4 *decision* is unchanged** — only the field's representation
  moved from an array to a `[Flags]` scalar. Status remains **Accepted**.

## Supersedes

- **None.** M19 **adds a standing**; it does not supersede an earlier ADR.
  It *rides* ADR 0001-B (thin token / fat authorization), ADR 0030
  (composable roles / claim append), ADR 0004 §B.1 (additive surface), and
  ADR 0050 (the admin-settled policy + one audited write lane + `/admin`
  controller shape). The frozen `IAuthorizationService` seams, the
  `AccessAudit` lane, and the resident standing are **unchanged**
  (C-M19·1/2/6). This is stated explicitly so a later reader knows M19 is an
  **additive standing, not a correction** — no earlier ADR's decisions are
  revised or re-scoped by this one.

## Affected files

- `src/Kumunita.Core/Identity/GuestSurface.cs` — new (the `[Flags]`
  `GuestSurface` enum) (D4).
- `src/Kumunita.Core/Identity/GuestAccess.cs` — new (the `GuestAccess`
  document) (D2).
- `src/Kumunita.Core/UserInfo/Profile.cs` — the additive `IsGuest` flag (D1).
- `src/Kumunita.Core/Identity/IIdentityService.cs` — the `GetGuestAccessAsync`
  + `SetGuestAccessAsync` ADD (D2).
- `src/Kumunita.Core/Identity/IdentityService.cs` — the implementations of the
  two new members (the `SetSignupOpenAsync` body pattern, the single audited
  write lane) (D2).
- `src/Kumunita.Core/M1DocTypes.cs` — the `Identity.GuestAccess` registration
  (D2).
- `src/Kumunita.Core/Identity/ThinPrincipal.cs` — the `Roles.Guest` constant
  (the `ClaimTypes.All` set **untouched**) (D5).
- `src/Kumunita.Web/Security/KumunitaClaimsPrincipalFactory.cs` — the new
  `IIdentityService` dependency + the `Guest` mint branch (D5).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` + the four locale
  files — the 10 `admin.guests_*` / `account.guest_welcome` `kw-l` keys in
  en/de/fr/da (D6).
- `src/Kumunita.Web/Controllers/AdminGuestsController.cs` +
  `src/Kumunita.Web/Models/GuestAdminViewModel.cs` +
  `src/Kumunita.Web/Views/AdminGuests/Index.cshtml` + the `/admin` index
  affordance — the `/admin/guests` surface (D6, F1).
- `src/Kumunita.Web/Milestones.cs` + `README.md` + `docs/STATUS.md` +
  `docs/ARCHITECTURE.md` + `tests/Kumunita.Web.Tests/MilestonesTests.cs` —
  the **end-of-roadmap** re-pin (M19 → `StatusDone`; no `StatusNext` remains)
  (U05 close, in one unit).
- **New tests** (the GATE-1…4 pins): `tests/Kumunita.Core.Tests/` — the
  `SetGuestAccessAsync` / `GetGuestAccessAsync` seam pins (U01, GATE-3 /
  C-M19·4); `tests/Kumunita.Web.Tests/` — the `GuestClaimMint` pins (U02,
  GATE-1 / GATE-4) and the `AdminGuestsController` pins (U04, GATE-3 / D6).
