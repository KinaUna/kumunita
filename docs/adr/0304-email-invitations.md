---
id: adr-0304
title: "Email invitations: a single-use, expiring, revocable credential that opens sign-up while the gate is closed — the invitation lane's Core seam"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0050-admin-managed-signup-gate-open-or-invitation-only.md
  - docs/adr/0142-delete-account.md
  - docs/adr/0159-escalation-authorization.md
  - docs/adr/0302-shared-kiosk-link.md
  - docs/plans-milestones/in-progress/plan-improve-ux.md
supersedes: []
superseded_by: []
lane: improve-ux
updated: 2026-11-11
---

# ADR 0304 — Email invitations: a single-use, expiring, revocable credential that opens sign-up while the gate is closed

Status: Accepted
Date: 2026-11-11
Builds on: 0050 (the `IsSignupOpenAsync` gate A08's `?invite=` must open),
0142 (the account-ops conventions + the `AccountSignup` notification emit at
`IdentityService.cs:213`), 0159 (the `EscalationToken` hash-at-rest
precedent — the token-shape template), 0302 (the `KioskLink` doc + service
— the **exact structural template** this ADR mirrors for the token hash,
the `Create/Revoke/Validate` triad, and the audit-row shape). Amends:
nothing — the `IIdentityService.RegisterAsync` seam is unchanged (A08 wires
the `?invite=` gate above it; A10 adds the acceptance notification).

> **Abstract:** IMPROVE-UX finding **E9** — invitation-only sign-up is a
> dead end: the closed page promises "an administrator will add your
> account" but no create/invite action exists. This ADR adds the
> *invitation* (decision D7 of
> [plan-improve-ux](../plans-milestones/in-progress/plan-improve-ux.md)):
> a **single-use, expiring, revocable** credential that opens sign-up for
> one email address while the platform gate (`IsSignupOpenAsync`) is
> closed. The token is **CSPRNG-generated (256 bits), shown to the inviter
> exactly once at creation, and stored only as its SHA-256 hex digest**
> (the 0159 / `IdentityToken` hash-at-rest rule, the ADR 0302
> `KioskLink.TokenHash` precedent). The Core seam (`Invitation` doc +
> `IInvitationService` — create / revoke / validate / accept) is pure
> Core (no Web coupling) so A08's sign-up gate, A09's admin surface, and
> A12's resident "Invite a neighbour" can all call it. **Who is it for:**
> the developer implementing IMPROVE-UX A08–A12 and the SEC reviewer.
> **When to read the rest:** before touching the invitation code, or when
> reviewing the invitation lane.

## Context

The platform's sign-up gate (ADR 0050) defaults to **closed**
(`IsSignupOpenAsync` → `false` — a fresh instance ships invitation-only).
The `AccountController.Signup` POST checks the gate and renders the
`SignupClosed` view when closed. The closed page says "an administrator
will add your account" — but **no admin action creates or invites
accounts**: the only account-creation paths are self sign-up (gated) and
the guardian add-a-child lane (ADR 0146). IMPROVE-UX E9 confirms this is a
resident dead-end: the promise on the closed page has no seam behind it.

What already exists and is kept: the `IIdentityService.RegisterAsync`
seam (the account-creation lane the invitation's `AcceptAsync` runs), the
`IdentityPlatformGateService` (the `IsSignupOpenAsync` gate A08's
`?invite=` will open), the `IdentityService.Sha256Hex` shared hasher (the
one-place digest invariant), and the `KioskLink` doc + `KioskLinkService`
(create / revoke / validate triad, the token-hash-at-rest shape, the
audit-row convention) — the **exact structural template** this ADR
mirrors.

## Decision

- **D1 — The `Invitation` doc: one row per invitation.** Members: `Id`
  (Marten-generated, conventional string — the `KioskLink` /
  `IdentityToken` / `EscalationToken` credential shape), `Email`
  (lowercased, trimmed — the invited address), `InviterSubjectId` (the
  resident or admin who created it — the audit principal), `TokenHash`
  (the SHA-256 hex digest of the long random token — **never the raw
  token**, the 0159 / F2 / ADR 0302 D2 rule), `CreatedAt`, `ExpiresAt`,
  `Status` (enum `Pending` / `Accepted` / `Revoked` / `Expired`),
  `AcceptedAt?`, `AcceptedSubjectId?`. A **new doc type** on the
  **existing `CoreDocTypes`** surface (the ADR 0004 §B.1 additive
  precedent — delta-detected by Marten, no DDL, no new `*DocTypes`).
  Carries `[DataClass(DataClassKind.Secret, Erasure.Delete,
  ExportScope.Never)]` (the ADR 0298 SecGate rule — an owner credential,
  the `KioskLink` shape; erased with the inviter's account via
  `ResidentAccountRowsEraser`).

- **D2 — The token shape.** CSPRNG-generated (`RandomNumberGenerator`),
  **32 random bytes base64url, `kinv_`-prefixed** (a 0159 `kesc_`-style
  high-entropy format — 256 bits, so brute-force validation is
  infeasible), shown to the inviter **exactly once** at creation (returned
  by `CreateAsync`; the caller shows it and it is gone — the store holds
  only the hash, so the raw token is **unrecoverable by design**, the
  0159 ESC·3 / ADR 0302 D2 rule). `// TODO ❓` the token length (proposed:
  256 bits as stated) — the SEC review may adjust it.

- **D3 — The `IInvitationService` seam (pure Core — no Web coupling, so
  A08–A12 can all call it):**
  - `CreateAsync(email, inviterSubjectId, expiresAt)` → the row + the raw
    token. Exactly one `invitation.create` `AccessAudit` row
    (`Via: Owner`, `TargetKind = "invitation"` — the `KioskLink`
    one-row convention). The `email` is lowercased + trimmed before
    storage.
  - `RevokeAsync(invitationId, inviterSubjectId)` → sets
    `Status = Revoked`, exactly one `invitation.revoke` audit row (same
    shape). **Owner-scoped**: a non-inviter cannot revoke. Idempotent:
    a missing or already-revoked/accepted invitation is a no-op returning
    `false` (the 0159 ESC·9 / ADR 0302 D3 precedent — no audit row, no
    state change).
  - `ValidateAsync(tokenHash)` → the row if `Status == Pending` and not
    past `ExpiresAt`, else `null` (the caller renders the denied/closed
    shape). Writes **zero** audit rows (the M35·5 / 0159 "the read is not
    an access decision" floor). The **hash** (not the raw token) is the
    lookup key (the `IdentityService.FindTokenAsync` precomputed-constant
    pattern — the hash is a local constant, never inside a `.Where(...)`
    expression).
  - `AcceptAsync(tokenHash, email, displayName, password)` → verifies the
    token (row exists, `Status == Pending`, not past `ExpiresAt`) **and**
    that the signing-up `email` matches the invitation's `Email` (the
    token is only good for the invited address), marks the row `Accepted`
    (`AcceptedAt` = now, `AcceptedSubjectId` = the new account's subject
    id), runs the existing `IIdentityService.RegisterAsync` lane, writes
    exactly one `invitation.accept` audit row (`Via: Owner`,
    `TargetKind = "invitation"`), and returns the `ThinPrincipal`.
    **Single-use**: a second `AcceptAsync` for the same token is refused
    (the row is `Accepted` — `ValidateAsync` returns `null`). A
    `RegisterAsync` failure (e.g. email already exists) propagates — the
    invitation row is **not** marked `Accepted` in that case (the
    account-creation is the primary domain op; the invitation-mark is the
    derivative, the ADR 0142 D6 cross-store ordering precedent).

- **D4 — Invariants (what A08–A12 build against).** The invitation is
  **single-use** (one account per token — the `AcceptAsync` marks the row
  `Accepted` and a second attempt is refused), **expiring** (the
  `ExpiresAt` is set at creation; `ValidateAsync` / `AcceptAsync` refuse
  past-expiry), **revocable** (revoke takes effect immediately — the next
  `ValidateAsync` refuses it), **works while sign-up is closed** (A08's
  `?invite=` gate opens the sign-up for the invited email regardless of
  `IsSignupOpenAsync`), and is **audited under the inviter** (the
  `Via: Owner` convention — the inviter is the actor, the ADR 0302 D4
  precedent). The token is a **credential** in the 0159 sense
  (hash-at-rest, shown once, revocable) but a **different standing**: it
  confers sign-up eligibility for one email address, never a write
  standing on existing content.

- **D5 — What is NOT changed.** The `IIdentityService.RegisterAsync` seam
  contract is unchanged — `AcceptAsync` calls it, not replaces it. The
  `IdentityPlatformGateService` is unchanged — A08's `?invite=` wiring
  reads `IsSignupOpenAsync` and, when closed, checks the invitation
  token **before** refusing (the gate is not bypassed; the invitation is
  the exception). The `AccountController.Signup` POST is unchanged by
  this ADR — A08 owns the `?invite=` wiring. The `AdminSignupController`
  is unchanged — A09/A11 own the admin/resident invitation settings.

- **D6 — Open questions (the SEC review settles).** `// TODO ❓`
  (a) the **invitation expiry length** (this ADR ships no default —
  `CreateAsync` takes `expiresAt` as a parameter; the A09/A12 surfaces
  will set it; the proposed default is **7 days**);
  (b) the **max invitations per inviter per period** (this ADR ships no
  ceiling — the A11/A12 surfaces can enforce a rate limit; the proposed
  limit is **5 per 30 days**);
  (c) whether a **resident-invited** invitation needs admin approval
  (A11's setting: off / on / on-with-approval — the `on-with-approval`
  mode adds a `PendingApproval` state to the invitation; this ADR ships
  only the `Pending` / `Accepted` / `Revoked` / `Expired` machine, the
  approval state is A11's). Recorded here as held, not decided.

- **D7 — Erasure.** The `Invitation` row is erased with the **inviter's**
  account (the `InviterSubjectId` match in
  `ResidentAccountRowsEraser` — block 15, the `KioskLink` /
  `DeletionRequest` owner-scoped erasure shape). When the **invited**
  person accepts and later deletes their account, the `Invitation` row
  (already `Accepted`) is **not** erased (it belongs to the inviter, not
  the invitee — the `KioskLink.OwnerSubjectId` precedent).

## Consequences

- A **new Marten doc type** (`Invitation`) registered on the existing
  `CoreDocTypes` surface (the ADR 0004 §B.1 additive delta — no
  migration, no new `*DocTypes`); the `DataClassCoverageTests`
  registered-type count rises by one (83 → 84) and the new row is
  `Delete`-erased (the inviter account's eraser owns it — the
  `ResidentAccountRowsEraser` block 15).
- Three new audit verbs (`invitation.create` / `invitation.revoke` /
  `invitation.accept`) join the audit trail — the invitation's lifecycle
  (create → accept | revoke) with the inviter as the actor.
- The token is a **credential** in the 0159 sense (hash-at-rest, shown
  once, revocable, single-use) but a **different standing**: it confers
  sign-up eligibility for one email address, never access to existing
  content.
- The `IIdentityService.RegisterAsync` seam and the
  `IdentityPlatformGateService` are unchanged — the invitation is an
  *additional* way to open sign-up, not a new registration lane.
- **Delivery:** A07 (this ADR + the doc + the service + the tests) lands
  first; A08 (the `?invite=` sign-up wiring), A09 (the `/admin/invitations`
  surface), A10 (the acceptance notify), A11 (the resident-invite setting)
  and A12 (the resident "Invite a neighbour") build on it.
## Amendments

**2026-11-11 (IMPROVE-UX A11) — the resident-invite mode is settled (resolves D6 c, partially).**
A11 ships the **3-state resident-invite setting** + a **testable rule** that
reads it and returns a resident's invite posture — A12's resident
"Invite a neighbour" surface will consume both.

- **The enum (Core).** `ResidentInviteSetting` (a code-owned closed set,
  the `EventRsvpMode` / `EmailInvitationStatus` shape — **not** a stored
  string) with three states: `Off` (the **floor** — a fresh instance
  ships with resident self-invite *off*; resident invite is opt-in, set
  by a GlobalAdmin), `On` (the resident's created invitations are
  immediately usable), and `OnWithApproval` (the resident's created
  invitations are gated behind the admin approval queue A12 ships —
  the D6 c "approval state" is what this mode enables).
- **Storage.** `LocaleSettings.ResidentInviteSetting` (the
  `NotifyAdminsOnSignup` / `IsSignupOpen` singleton field shape — the
  `SetSignupOpenAsync` / `SetNotifyAdminsOnSignupAsync`
  singleton-toggle lane, the same `LocaleSettings` doc the other
  instance gates read/write). The read floor is `Off` — a missing
  singleton or an unset value both yield `Off`.
- **The seam (Core).** `IIdentityService.GetResidentInviteSettingAsync()`
  → the enum (the `Off` floor), and
  `IIdentityService.SetResidentInviteSettingAsync(ResidentInviteSetting
  mode, string adminSubjectId)` → the singleton write + **exactly one**
  `AccessAudit` row (`Via = Admin`, action `signup.set-resident-invite`,
  `TargetKind = "signup"`, `TargetId = "signup"` — the
  `signup.set-open` / `signup.set-notify` singleton shape). The
  `IIdentityPlatformGateService` surface (C3-03, ADR 0293) carries the
  two methods; `IdentityService` keeps the thin forwarder (the C08
  test-compat facade shape, the existing 12-gate surface).
- **The rule (Core, on `IInvitationService`).**
  `GetResidentInvitePostureAsync(residentSubjectId, ct)` → a `ResidentInvitePosture`
  (a `record (bool MayInvite, bool RequiresApproval)`) — the testable
  decision that A12's resident "Invite a neighbour" surface will call
  **before** `CreateAsync` / `InviteAsync`. `Off` →
  `MayInvite = false`; `On` → `MayInvite = true, RequiresApproval =
  false`; `OnWithApproval` → `MayInvite = true, RequiresApproval = true`.
  A **read** — zero audit rows (the `IsSignupOpenAsync` / `ValidateAsync`
  "the read is not an access decision" floor — the *write* decision is
  A12's `CreateAsync` lane's job). The rule does **not** change any of
  the D3 seams (`CreateAsync` / `RevokeAsync` / `ValidateAsync` /
  `AcceptAsync` / `ListAsync` / `InviteAsync` + the A08 presented-token
  overloads + the A10 inviter-acceptance notify).
- **D6 c (the approval state) — clarified.** This ADR still ships only
  the `Pending` / `Accepted` / `Revoked` / `Expired` state machine.
  A11 settles the **mode** (`OnWithApproval`) and the **rule** (the
  `RequiresApproval` flag on `ResidentInvitePosture`) — A12's approval
  queue will add the `PendingApproval` row state (and the admin-approve
  lane) when it ships. A11's rule is the *gate*; A12's row state is the
  *ledger*.
- **D6 b (the rate ceiling) — remains open.** A11 ships the
  setting-driven rule; the `// TODO ❓` who-may-invite + how-many-per-
  period refinement is **flagged, not built** (a `// TODO ❓` comment at
  the rule's seam, the A10 precedent). A12 (or a follow-on) will settle
  it; the proposed 5-per-30-days ceiling from D6 b stands.
- **Web surface.** `AdminSignupController` gains a
  `SaveResidentInvite(mode)` POST (mirroring `SaveNotify`), the
  `SignupAdminViewModel` carries the 3-state setting, and
  `Views/AdminSignup/Index.cshtml` renders a 3-option radio group. The
  `[Authorize(Roles = Roles.GlobalAdmin)]` gate and the
  `IsSignupOpenAsync` / `SetSignupOpenAsync` / `IsNotifyAdminsOnSignupAsync`
  / `SetNotifyAdminsOnSignupAsync` seams are **unchanged** (the ADR 0050
  signup-gate surface is intact). The 3 option labels + the flash
  strings land in the 4-language `admin.*` key surface (C02 parity, the
  de/fr/da machine-written + flagged-for-parked-native-review shape).