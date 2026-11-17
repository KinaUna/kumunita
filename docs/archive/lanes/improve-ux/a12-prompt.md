---
id: a12-prompt
title: "Prompt: IMPROVE-UX A12 — the resident 'Invite a neighbour' surface + the admin approval queue"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0304-email-invitations.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **A12** — the **resident-facing** half of the invitation lane:
> the **"Invite a neighbour"** surface a resident can use (profile/account
> menu) + the **admin approval queue** on `/admin/invitations` that
> activates when the admin-settled resident-invite mode is
> `OnWithApproval` (A11's setting, ADR 0304 §Amendments). A07–A11 all
> shipped: A07 the `Invitation` doc + `IInvitationService`, A08 the
> `?invite=` sign-up gate, A09 the `/admin/invitations` surface + the
> invitation email, A10 the inviter-acceptance notify, A11 the 3-state
> `ResidentInviteSetting` (the `Off` / `On` / `OnWithApproval` enum) +
> the testable rule `IInvitationService.GetResidentInvitePostureAsync`.
> A12 is the **seventh (final) unit** of the lane — it *consumes* A11's
> rule (the `MayInvite` / `RequiresApproval` posture) to gate the
> resident surface, and ships the **`PendingApproval` row state + the
> admin-approve lane** that A11's `OnWithApproval` mode enables (ADR 0304
> D6 c). **Who is it for:** the next coding agent (fresh context).
> **When to read the rest:** when starting A12 (A07–A11 are shipped;
> A11's setting + rule exist and are tested — A12 builds on them, it does
> not re-settle them).

# Fresh-context prompt: IMPROVE-UX A12

You are continuing the Kumunita repository on `development` (HEAD
`56e1cff9`, **A11 just landed** — the 3-state `ResidentInviteSetting`
+ `LocaleSettings.ResidentInviteSetting` field + the
`IIdentityService.GetResidentInviteSettingAsync` /
`SetResidentInviteSettingAsync` seam + the testable rule
`IInvitationService.GetResidentInvitePostureAsync` →
`ResidentInvitePosture(MayInvite, RequiresApproval)` + the
`admin.signup_resident_invite_*` keys + the
`AdminSignupController.SaveResidentInvite(mode)` POST; **A10 before
that** — the inviter-acceptance notify + the `account.signup_closed_body`
rewrite; **A09 before that** — `/admin/invitations` + `InviteAsync` /
`ListAsync` + the invitation email; **A08** — the `?invite=` sign-up
gate + `ValidatePresentedAsync` / `AcceptPresentedAsync`; **A07** —
ADR 0304 + the `Invitation` doc + `IInvitationService`). A12 is the
**final unit** of the invitation lane (A07–A12). The plan card (in
`plan-improve-ux.md`, the A12 row):

> A12 — Resident "Invite a neighbour" (profile/account menu) + admin
> approval queue on `/admin/invitations` when approval is on; keys.
> Read first: A09 view. **Done when: resident invite works in both
> modes.**

The "two modes" are A11's setting: **`On`** (the resident's created
invitations are immediately usable) and **`OnWithApproval`** (the
resident's created invitations are gated behind the admin approval
queue — ADR 0304 D6 c's `PendingApproval` row state + the admin-approve
lane, which A11 settled the *mode* + the *rule* for but did not ship).
**`Off` is the floor** — the resident surface is hidden and its seam
refuses (A11's `ResidentInvitePosture.Refused`). A12 ships the two modes
that A11 settled.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`docs/adr/0304-email-invitations.md`** — the ADR for the invitation
  lane. **D1** the `Invitation` doc (the `Pending` / `Accepted` /
  `Revoked` / `Expired` state machine — A12 adds **`PendingApproval`**
  to the enum, the ADR 0304 D6 c "approval state"); **D3** the
  `IInvitationService` seam (the `CreateAsync` / `RevokeAsync` /
  `ValidateAsync` / `AcceptAsync` / `ListAsync` / `InviteAsync`
  contracts — A12 will add **`ApproveAsync`** or extend
  `ListAsync`'s projection to surface the approval state — the ADR 0304
  D3 "compatible ADD" shape); **D4** the invariants (single-use,
  expiring, revocable — A12 does **not** change these); **D6 c**
  (the approval state — A11 settled the *mode* + the *rule*; A12
  settles the *row state* + the *admin-approve lane*); **§Amendments
  2026-11-11** (A11's settled shape — the enum, the seam, the rule —
  the contract A12 builds against); **D6 b** (the rate ceiling — the
  `// TODO ❓` who-may-invite + how-many-per-period question A11
  flagged; A12 may settle it or continue to flag it — the A10
  precedent: "a unit that makes a decision writes or amends an ADR in
  the same commit" — if A12 settles it, amend ADR 0304 in the same
  commit).
- **`src/Kumunita.Core/Identity/Invitation.cs`** — the `Invitation`
  doc + the `EmailInvitationStatus` enum + A11's
  `ResidentInviteSetting` enum + the `ResidentInvitePosture` record.
  A12 adds **`EmailInvitationStatus.PendingApproval`** (the ADR 0304 D6
  c row state) — a **compatible ADD** to the closed set (the
  `EmailInvitationStatus` precedent — the `Pending` / `Accepted` /
  `Revoked` / `Expired` shape).
- **`src/Kumunita.Core/Identity/IInvitationService.cs`** +
  **`InvitationService.cs`** — the invitation seam. A11's rule
  `GetResidentInvitePostureAsync` (≈line 502) is the **gate** A12's
  resident surface reads before calling `CreateAsync` / `InviteAsync`.
  A12 ships the **admin-approve lane** (a new
  `ApproveAsync(invitationId, adminSubjectId)` or similar — the
  `RevokeAsync` owner-scoped no-op shape, the `invitation.approve`
  audit row, the `PendingApproval → Pending` transition — the ADR 0304
  D3 "compatible ADD" precedent). **Do not** change the A07/A08/A09/A10
  contracts (`CreateAsync` / `RevokeAsync` / `ValidateAsync` /
  `AcceptAsync` / `AcceptPresentedAsync` / `ValidatePresentedAsync` /
  `ListAsync` / `InviteAsync` + the A10 inviter-acceptance notify +
  A11's rule).
- **`src/Kumunita.Web/Controllers/AdminInvitationsController.cs`** +
  **`Views/AdminInvitations/`** — the A09 `/admin/invitations` surface
  A12's approval queue attaches to. The approval queue is a **new
  section** on this surface (the `PendingApproval` rows + the
  admin-approve button) — the A09 view is **already built**; A12
  extends it, does not re-wire it.
- **`src/Kumunita.Web/Controllers/AdminSignupController.cs`** +
  **`Views/AdminSignup/Index.cshtml`** — A11's admin setting surface
  (the `SaveResidentInvite(mode)` POST + the 3-option radio group). A12
  does **not** change this surface — the admin sets the mode here; the
  resident surface A12 ships *consumes* the mode.
- **`src/Kumunita.Core/Identity/IIdentityService.cs`** +
  **`IdentityService.AccountOps.cs`** → **`GetResidentInviteSettingAsync`**
  / **`SetResidentInviteSettingAsync`** (the A11 seam, the
  `NotifyAdminsOnSignup` / `IsSignupOpenAsync` singleton-toggle shape) —
  the **setting A12's resident surface reads** (via A11's rule
  `GetResidentInvitePostureAsync`, which already reads it).
- **`src/Kumunita.Web/Views/Account/`** (the profile/account menu) —
  the resident surface A12 adds the "Invite a neighbour" entry to.
  The entry is **hidden** when the mode is `Off` (A11's rule returns
  `MayInvite = false`), **visible** when `On` or `OnWithApproval`
  (the resident may create; the mode controls whether the row lands
  `Pending` or `PendingApproval`).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and
  the in-process `dotnet exec` invocation.
- **`git status`** + the branch (A11's `56e1cff9` + A10's `6c09e885`
  must be intact; you are on `development`).

**The one critical seam detail (get this right first).** A12 is the
**resident surface + the admin approval queue** — it **consumes** A11's
rule (the `MayInvite` / `RequiresApproval` posture) and **ships** the
`PendingApproval` row state + the admin-approve lane. The **gate** is
A11's `GetResidentInvitePostureAsync` (A12's resident surface reads it
before calling `CreateAsync` / `InviteAsync`); the **ledger** is the
new `PendingApproval` row state + the `invitation.approve` audit row
(A12 ships these). The two are **deliberately separate** — the mode
controls *who may create*, the row state records *what a row can do*
(the ADR 0304 D4 invariant — A12 does not change the single-use /
expiring / revocable contract). The `// TODO ❓` open question —
**who may invite and how many per period** (ADR 0304 D6 b) — is
**flagged, not built**: A12 may settle it (a rate ceiling — the
proposed 5-per-30-days) or continue to flag it (a `// TODO ❓` comment
+ a one-line note, the A10 precedent). If A12 settles it, amend ADR 0304
in the same commit (the A10 precedent: "a unit that makes a decision
writes or amends an ADR in the same commit").

The **work**:

1. **The `PendingApproval` row state (Core).** Add
   `EmailInvitationStatus.PendingApproval` to the enum (the ADR 0304 D6
   c "approval state" — the `Pending` / `Accepted` / `Revoked` /
   `Expired` closed set, the compatible ADD). The `Invitation` doc does
   **not** gain a new field (the `Status` enum is the state machine —
   the ADR 0304 D1 shape). The transition is
   `PendingApproval → Pending` (the admin-approve lane) — a
   `PendingApproval` row is **not** usable (the A08 `?invite=` sign-up
   gate's `ValidateAsync` / `AcceptAsync` refuse it — the existing
   "a `Pending` row past `ExpiresAt` is refused" shape, the ADR 0304 D1
   virtual-state precedent).
2. **The admin-approve lane (Core).** A new
   `ApproveAsync(invitationId, adminSubjectId)` on `IInvitationService`
   (the `RevokeAsync` owner-scoped no-op shape, the
   `invitation.approve` audit row, `Via = Admin`, `TargetKind =
   "invitation"`, `TargetId = invitationId`). The transition is
   `PendingApproval → Pending` (the row becomes usable — the A08
   `?invite=` gate accepts it). Idempotent: a missing / already-
   approved / already-revoked / already-accepted invitation is a no-op
   returning `false` (the 0159 ESC·9 / ADR 0302 D3 precedent — no audit
   row, no state change). A GlobalAdmin may approve (the
   `AdminInvitationsController` enforces the standing — the Core seam
   does not re-check `User`, the ADR 0050 split).
3. **The resident surface (Web).** A new
   `/account/invite` (or the profile/account menu's "Invite a
   neighbour" entry) that:
   - **Reads** A11's rule (`GetResidentInvitePostureAsync`) to decide
     whether the surface is **visible** (`MayInvite = true`) or
     **hidden** (`MayInvite = false` — the `Off` floor).
   - **Creates** an invitation (the existing `InviteAsync` seam) with
     the **`Status`** set to `Pending` (when `RequiresApproval = false`
     — the `On` mode) or `PendingApproval` (when `RequiresApproval =
     true` — the `OnWithApproval` mode).
   - The **resident** is the `InviterSubjectId` (the ADR 0304 D3 audit
     principal — the existing `Via = Owner` shape).
   - The **A08 sign-up gate** is unchanged — it reads the invitation's
     `Status` (a `Pending` row is usable, a `PendingApproval` row is
     refused — the ADR 0304 D1 shape).
4. **The admin approval queue (Web).** On `/admin/invitations`, a new
   section listing the `PendingApproval` rows (email, created, expires)
   + an **Approve** button per row (the `ApproveAsync` seam). The
   section is **hidden** when the mode is `Off` (no resident invitations
   can exist) and **visible** when the mode is `OnWithApproval` (the
   resident surface can create `PendingApproval` rows). The `On` mode
   has **no** approval queue (the resident's invitations land
   `Pending` directly — the existing A09 list surface is unchanged).
5. **Keys (4 languages).** The resident surface's labels (the "Invite a
   neighbour" entry, the form, the success flash) + the admin approval
   queue's labels (the section heading, the Approve button, the
   approved flash) + any new `notifications.*` kind (the
   `invitation.approved` nudge to the inviter when an admin approves
   their resident invitation — the A10 `invitation.accepted` precedent,
   the `NotificationKinds` closed set). en + de/fr/da machine-written,
   flagged for the parked native review. **C02 four-language parity**
   must stay green. If A12 adds a `notifications.*` kind, the
   `NotificationsControllerTests` closed-set count may need a re-baseline
   (the A10 precedent: "the documented re-baseline", the
   `closed-set-translation-key-test-pins` memory).

**Tests:**
- **Core** (a new `ResidentInviteApprovalTests` over the
  `PostgresFixture` harness, the `InvitationServiceTests` two-store
  boot as the template): (a) the `PendingApproval` row state — a
  `PendingApproval` row is **refused** by `ValidateAsync` /
  `AcceptAsync` (the ADR 0304 D1 virtual-state shape, the A08
  `?invite=` gate's existing "a `Pending` row past `ExpiresAt` is
  refused" precedent); (b) the admin-approve lane —
  `ApproveAsync` transitions `PendingApproval → Pending` (the row
  becomes usable — the A08 `?invite=` gate accepts it after the
  approve); (c) the audit row — exactly one `invitation.approve`,
  `Via = Admin`, `TargetKind = "invitation"`, `TargetId = invitationId`
  (the `invitation.create` / `invitation.revoke` / `invitation.accept`
  one-row convention, the ADR 0304 D3 shape); (d) the idempotent no-op
  — a missing / already-approved / already-revoked / already-accepted
  invitation is a no-op returning `false` (the 0159 ESC·9 / ADR 0302 D3
  precedent — no audit row, no state change); (e) the resident surface
  creates `Pending` when `RequiresApproval = false` (the `On` mode) and
  `PendingApproval` when `RequiresApproval = true` (the
  `OnWithApproval` mode) — the A11 rule is the gate, the row state is
  the ledger.
- **Web** (the `AdminInvitationsController` direct-construction harness):
  (f) the approval queue section is **visible** when the mode is
  `OnWithApproval` (the `PendingApproval` rows + the Approve button)
  and **hidden** when the mode is `Off` or `On` (the `On` mode has no
  approval queue — the existing A09 list surface is unchanged); (g) the
  Approve button calls `ApproveAsync` (the row transitions
  `PendingApproval → Pending` — the existing A08 `?invite=` gate
  accepts it after the approve); (h) the resident surface's
  "Invite a neighbour" entry is **visible** when `MayInvite = true`
  (the `On` / `OnWithApproval` modes) and **hidden** when `MayInvite =
  false` (the `Off` floor — A11's rule returns `Refused`).

**Scope rules:**

- **Do not touch** the A07/A08/A09/A10/A11 invitation seams
  (`CreateAsync` / `RevokeAsync` / `ValidateAsync` / `AcceptAsync` /
  `AcceptPresentedAsync` / `ValidatePresentedAsync` / `ListAsync` /
  `InviteAsync` + the A10 inviter-acceptance notify + A11's rule
  `GetResidentInvitePostureAsync` + the A11 setting seam
  `GetResidentInviteSettingAsync` / `SetResidentInviteSettingAsync`) —
  A12 **consumes** A11's rule and **adds** the `PendingApproval` row
  state + the `invitation.approve` lane. The A11 admin setting surface
  (`AdminSignupController.SaveResidentInvite` + the 3-option radio
  group) is **already built** — A12 does not re-wire it; the resident
  surface A12 ships *consumes* the mode.
- **Do not change** the ADR 0304 D4 invariants (single-use, expiring,
  revocable) — the `PendingApproval` row state is **additive** (the
  `EmailInvitationStatus` closed set, the compatible ADD); the existing
  `Pending` / `Accepted` / `Revoked` / `Expired` machine is **unchanged**.
- **Do not start** A13, A14, or A15 — A12 is the **final** unit of the
  invitation lane (A07–A12). The `Done when` bar ("resident invite works
  in both modes") is the only acceptance gate.
- **One commit per change** (the house rule): A12 is one `feat(ux):`
  commit (`feat(ux): resident 'Invite a neighbour' + the admin approval
  queue (IMPROVE-UX A12)`). If you settle the rate ceiling (the
  `// TODO ❓` who-may-invite + how-many-per-period question, ADR 0304
  D6 b) as a decision, split a `docs(ux):` ADR 0304-amendment commit
  first (the A10 / A11 precedent).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged
  for the parked native review). The **C02 four-language parity** test
  (`C02_DataAsEmbeddedResourcesTests`) must stay green. If A12 adds a
  `notifications.*` kind, the `NotificationsControllerTests` closed-set
  count may need a re-baseline (the A10 precedent: "the documented
  re-baseline", the `closed-set-translation-key-test-pins` memory).
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress —
  A12 adds a surface + a row state (not a tracked anti-pattern); confirm
  it before committing.
- ⚠️ **Known pre-existing reds (not yours):** on `development`,
  `dotnet test --project tests/Kumunita.Web.Tests` currently has **4
  failures that pre-date A12** (verified on base): two
  `SecGate.DataClassCoverageTests` and
  `ImproveHarnessTests.CodeCheck_Gate_Passes` /
  `ImproveCheck_Gate_Passes` (the latter on **ADR index drift — ADRs
  0303 / 0304 on disk but missing from the ADR index**). Do **not**
  attribute these to A12 or try to fix them here; your A12 suites must
  be green.

**Before committing:** `dotnet build`, then run the narrowest tests via
the in-process runner (see `testing-and-ci.md`): your new A12 Core tests
+ the `AdminInvitationsController` tests (the A09 `/admin/invitations`
surface + the new approval queue) + the `InvitationServiceTests` (the
A07/A08/A09/A10 invitation seams still intact) + the
`ResidentInviteSettingTests` (the A11 setting + rule still intact) +
the `C02_DataAsEmbeddedResourcesTests` four-language parity + the
`KwLRegistryConsistencyTests`. If a test fails, read the error, adjust,
continue — a failed test is a recoverable step, not a stopping point.

Then report: where you stored the `PendingApproval` row state (the
`EmailInvitationStatus` enum, the ADR 0304 D6 c "approval state") + the
`ApproveAsync` seam (its name + the `invitation.approve` audit row) +
the resident surface (the `/account/invite` route + the
`MayInvite` gate) + the admin approval queue (the section on
`/admin/invitations` + the Approve button) + the keys you added (the
resident surface + the approval queue + any new `notifications.*` kind),
the new test class(es) + test count, the commit hash(es) (and the ADR
0304-amendment commit, if any), and the A07/A08/A09/A10/A11 seams you
confirmed unchanged (all `IInvitationService` contracts + the A10
inviter-acceptance notify + A11's rule `GetResidentInvitePostureAsync`
+ the A11 setting seam + the ADR 0050 signup-gate seams). Do not commit
unrelated work.
