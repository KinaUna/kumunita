# ADR 0028 — Guardian controls: account-scope supervision of a child's account

Status: Accepted
Date: 2026-09-14
Amends: 0006 (a new `AccessVia` value + new `IUserInfoService` seams — all
compatible ADDs), 0003/ADR 0012 (the child-scoped suspension and membership
lanes admit the guardian standing), m2b (the child's invitation self-accept
gates on an active link)

## Context

The platform is invitation-only and one-neighborhood (`SECURITY.md` §1,
ADR 0002), and its population is not only adults — **there will be kids on the
platform**, and integrating them well is part of what makes the place a
community (`the-platform-as-integrator.md`). But online activity is a burden
and a risk for parents. The product want (2026-09-14), in the words it was
given: **keep it simple, controls only, no invasion of privacy for now.** A
parent adds an account for a child (the usual confirm-email process); then
suspends/locks it, allows/blocks the child's access to communities and groups,
and approves a group invitation sent to the child; and, when the child is old
enough, makes the child's account a normal, independent one.

This is a **new authorization lane**, not a reuse. The nearest primitive,
`DelegationGrant`, is the **opposite direction**: a delegate borrows the
*owner's* standing to act *as* the owner (invariant C2). A guardian does not
act *as* the child — the guardian holds a defined set of **controls over** the
child's account. The pattern is ADR 0013's: a new standing on a frozen surface,
action-scoped, audited, and — where the platform is strictest — deliberately
bounded.

The cardinal rule that governs the lane's shape: **the author's choice of
audience is absolute by default** (`SECURITY.md` §1; the `part-vs-whole` test,
`START-HERE.md`). A child is an author. A "parent manages a child's account"
feature is, at its core, a **deliberate privacy inversion** — the parent's
standing overrides the child's own choices on a scoped set of things. This ADR
records the decision and — above all — the decision that the inversion is
bounded to **account-level controls** and **never** extends to reading the
child's private content. `docs/design/guardian-controls-design.md` freezes the
invariants **G·1–G·5**, the FACES, and the seam tests this ADR points at.

## Decision

### A. The standing: `AccessVia.Guardian`, the **9th** value

An additive enum value appended **after** `Group` in
`Authorization/Decision.cs` — the exact precedent of the M1 `Admin` (7th) and
ADR 0013 `Group` (8th) appends: a value-addition, never a renumbering; no
stored audit row's meaning changes. It is **action-scoped** to the five
supervisory actions (§C) and **never** to a content read.

### B. The relationship: one document, `GuardianLink`

One row per (guardian, child) pair — a child may have one **or two** guardians
(each an active row); the child has guardian standing iff **any** active row
exists (the multi-group membership shape, not a single owner).

- `GuardianId`, `ChildId`, `Status` (`Active` → `Dissolved`), `CreatedAt`,
  `DissolvedAt?`, `DissolvedBy?` — the `DelegationGrant.RevokedBy` and the
  `GroupInvitation` state-machine precedent.
- **Additive (ADR 0004 §B.1):** registered on the **existing `M1DocTypes`**
  surface (a neighbor of `Schema.For<DelegationGrant>()`) — delta-detected by
  Marten, idempotent, no re-seed, no new `*DocTypes`, no DDL change under
  Marten 9's JSONB `data` column (the ADR 0010 `IsPrivate` upgrade-path
  precedent). No existing row's meaning changes.
- **Dissolve is effectively one-way in practice:** re-attaching is a fresh
  `Active` row — a deliberate new act, not an undo.

### C. The five supervisory actions — standing on **existing** lanes

The guardian's standing is exercised on the **management** lanes (suspend,
membership, invitation), which live on `IUserInfoService` — so the ADR 0006
frozen `CanAsync` / `CanSeeAsync` signatures stay **byte-identical** (the
ADR 0013 "one additive lane, nothing else moves on the frozen surface"
discipline).

1. **Suspend / lock** — `SuspendChildAsync` / `UnsuspendChildAsync`: the
   child-scoped analog of the GlobalAdmin `BlockAsync`/`UnblockAsync` lane;
   sets `Profile.Blocked` (the **same flag** `BlockedAccountMiddleware` and the
   directory already read, so enforcement is identical), audited
   `guardian.suspend` / `guardian.unsuspend` (`Via: Guardian`, target the child
   account). The GlobalAdmin lane is unchanged.
2. **Allow / block communities** — the ADR 0012 `AddCommunityMemberAsync` /
   `RemoveCommunityMemberAsync` lanes **admit the guardian standing** for a
   child target (membership is the child's posting/feed gate, ADR 0012's single
   seam); the audit row records the **narrower** standing, `Via: Guardian`
   (the ADR 0012 rule).
3. **Allow / block groups** — the group `AddGroupMemberAsync` /
   `RemoveGroupMemberAsync` lanes, the same `Via: Guardian` branch.
4. **Approve a group invitation** — `ApproveGroupInvitationAsync` resolves the
   child's `Pending` invitation as Accepted (the m2b accept-lane membership
   write + stamps), auditing `group.invite.approve` (`Via: Guardian`);
   **simultaneously** `AcceptGroupInvitationAsync` **gates**: if the invitee
   has an active `GuardianLink`, the self-accept is refused
   (`InvalidOperationException` → the Web's error, never a 500); the child's
   **decline** stays open (a child may always say no). The C-M2b·2 self-lane's
   "one recorded exception for a supervised account" (the ADR 0008 owner-row
   exception, carried to the supervised-child row).
5. **Formation + come-of-age** — `CreateGuardianLinkAsync` (the formation lane;
   the Web pairs it with `IIdentityService.RegisterAsync` so account + link +
   audit commit together, C3) and `DissolveGuardianLinkAsync` (the independence
   lane; the child's self-lanes restore, memberships **preserved**, C4-live).

All new seams are **compatible ADDs on `IUserInfoService`** (ADR 0006-E — the
M2 `GetProfilesAsync` / M3b `Moderate` ADD precedent); the ADR 0006-E
"named here (…)" list gains the six `IUserInfoService` methods and the
`AccessVia.Guardian` value.

### D. The invariants (owned by the design doc, pinned by seam tests)

- **G·1 — The guardian standing never reads the child's content.** The
  `AccessVia.Guardian` value appears on **no** `CanAsync` / `CanSeeAsync`
  decision; the child's content is authorized as a normal account. *This is the
  lane's whole honesty, and it is a test, not a promise.*
- **G·2 — Standing is from the active link, checked live (C4).** Every
  guardian lane resolves the child's active `GuardianLink` in the same read; a
  dissolve is live on the very next lane read (no projection, no cache).
- **G·3 — Action-scoped, deny-by-default.** A guardian action outside the five
  supervisory actions (or on a target with no active link) is a Deny —
  including, explicitly, any content read. A scope unknown to old code denies
  on new actions by default (ADR 0006-E).
- **G·4 — Formation is creation-based.** The standing basis is that the
  guardian *created* the child's account (`CreatedById`); there is **no**
  self-serve "claim guardianship over an existing account" lane.
- **G·5 — The safety valve always exists.** A GlobalAdmin may dissolve any
  active link and un-suspend any account, audited `Via: Admin`; a mistreated
  child can always reach it. No guardian may hold a child's account with no
  recourse.

### E. Audit — additive verbs, all `Allow`, all C3

`guardian.create`, `guardian.suspend`, `guardian.unsuspend`, `guardian.dissolve`
(target the child account; `Via: Guardian`, or `Via: Admin` on the dissolve
safety valve); `group.invite.approve` (target the group, `Via: Guardian`); the
existing `community.add-member` / `community.remove-member` / `group.add-member`
/ `group.remove-member` verbs now additionally carry `Via: Guardian` when a
guardian curates a child's membership. These are **management actions** (like
role-change / manual-verify), not access decisions on restricted content — the
audience-restricted Allow/Deny machinery is not engaged. No verb is repurposed;
no stored row's meaning changes.

## Consequences

- **The family case gains a *supervision* lane, not just a private-group one.**
  ADR 0010 gave a family a private organizing unit (a room); this lane gives a
  parent the routine care of a child's account — suspend, curate, approve,
  hand-over — without a GlobalAdmin in the loop for everyday life.

- **The cardinal privacy rule is held and *proven* held.** G·1 is a seam test
  and a "deliberate not-available" record: a family that later wants "my parent
  can see what I posted" is told that is a **new, separate, ADR-gated** decision
  (an ADR 0028 amendment), not a toggle. That is the difference between "we
  decided not to" and "it cannot happen" — the only acceptable form here.

- **A protected class is named without a protected data field.** "Minor" is
  carried by the **link**, not a stored age — `SECURITY.md`'s "what we don't
  store, we can't leak" is kept even as the control is added.

- **A new adversary (A7) is named, not assumed away:** a hostile or
  over-reaching guardian over a minor. Mitigated by G·4 (no self-serve claim
  over a stranger), G·1 (the exposure is account-level, never the child's
  private life), and G·5 (the safety valve, audited). Recorded in
  `SECURITY.md` §4 / §5.

- **The `Authority` chain gains a family legibility row:** the audit log now
  answers "this parent supervised this child's account for that period, by
  creation, and dissolved it when the child came of age" — traceable forward
  and backward (the `domains-of-integration.md` chain, extended one step).

- **The roadmap trio moves together** (AGENTS.md contract): `README.md`
  Features + Roadmap and `Milestones.cs` gain the `GU` named lane (pulled
  forward ahead of **M4**, the `ML` precedent); `MilestonesTests.cs` updates in
  the same commit. M4/M5/M6 stay Events / Projects / Portability.

- **Non-decisions (the "for now"):** no content read; no reading who contacted
  the child; no delegation of authorship (the guardian does not post *as* the
  child); no blanket "block all communities" toggle (participation control is
  per-member); no stored age. Each is *deferred*, not *denied* — each re-litigates
  as an ADR 0028 amendment when it earns its keep.

- **Backward-compat:** fully additive. `GuardianLink` rows and `Via: Guardian`
  audit rows do not exist until the lane ships; `AccessVia.Guardian` is an
  append; `Profile.Blocked` is an existing flag read by an existing enforcement
  path. Re-deploying an older image over a forward-migrated database is safe.
