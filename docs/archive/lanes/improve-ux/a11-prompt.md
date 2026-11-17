---
id: a11-prompt
title: "Prompt: IMPROVE-UX A11 — the resident-invite admin setting (off / on / on-with-approval) + the Core rule"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0304-email-invitations.md
  - docs/adr/0050-admin-managed-signup-gate-open-or-invitation-only.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **A11** — the **admin setting that lets residents invite
> neighbours**, the gate A12's resident "Invite a neighbour" surface will
> read. A07 shipped the `Invitation` doc + `IInvitationService`, A08 the
> `?invite=` sign-up gate, A09 the `/admin/invitations` surface + email,
> A10 the inviter-acceptance notify + the honest closed sign-up text.
> A11 is the **fifth unit** of the invitation lane (A07–A12): it adds a
> new admin-settled instance value (the **resident-invite mode**:
> *off* / *on* / *on-with-approval*) and a **testable Core rule** that
> reads it and decides a resident's invite posture. **Who is it for:**
> the next coding agent (fresh context). **When to read the rest:** when
> starting A11 (A07–A10 are shipped; the setting + rule do not exist yet —
> A12's resident surface and approval queue will *consume* them).

# Fresh-context prompt: IMPROVE-UX A11

You are continuing the Kumunita repository on `development` (HEAD
`6c09e885`, **A10 just landed** — the inviter-acceptance notify + the
`account.signup_closed_body` rewrite; **A09 before it** —
`/admin/invitations` + `InviteAsync` / `ListAsync` + the invitation
email; **A08 before that** — the `?invite=` sign-up gate +
`ValidatePresentedAsync` / `AcceptPresentedAsync`; **A07** — ADR 0304 +
the `Invitation` doc + `IInvitationService`). A11 is the **setting + rule
half** of the resident-invite pair (A11/A12). The plan card (in
`plan-improve-ux.md`, the A11 row):

> A11 — Resident invitations: admin setting (off / on /
> on-with-approval) + Core rule. `// TODO ❓` who may invite and how many
> per period. Read first: `AdminSignupController.cs`, A07 service.
> **Done when: setting stored; rule tested.**

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`docs/adr/0304-email-invitations.md`** — the ADR for the invitation
  lane. **D4** the invariants (single-use, expiring, revocable, works
  while sign-up is closed); **D6** the open questions A09 settled (the
  expiry default) — the **resident-invite setting is anticipated by the
  lane but not yet settled**; **D7** the erasure. A11 settles the
  3-state resident-invite mode — if you settle it as a decision, amend
  this ADR (or add a short note) in the same commit (the A07 / A10
  precedent: "a unit that makes a decision writes or amends an ADR in the
  same commit").
- **`src/Kumunita.Core/Identity/IIdentityService.cs`** +
  **`IdentityService.AccountOps.cs`** → **`IsNotifyAdminsOnSignupAsync()`
  / `SetNotifyAdminsOnSignupAsync(notify, adminSubjectId)`** (≈line 419 /
  461) and **`IsSignupOpenAsync` / `SetSignupOpenAsync`** — the
  **singleton-toggle precedent** (ADR 0019 / 0020 / 0050): one
  admin-settled instance value, one audited write lane, **exactly one
  `AccessAudit` row, `Via = Admin`**. A11's new value is the same shape
  but **3-state, not bool** — so it is an **enum**, stored like these
  toggles. Copy this shape; the floor is `Off`.
- **`src/Kumunita.Core/UserInfo/Profile.cs`** ≈line 439 → the
  **`EventRsvpMode`** 3-state enum (`ChildDecides` / `GuardianApproves` /
  `GuardianNotifies`) — the **closest precedent for a 3-state
  admin-settled posture enum** (the naming + the "default is one of the
  three" shape). A11's enum (e.g. `ResidentInviteSetting` with
  `Off` / `On` / `OnWithApproval`) mirrors it.
- **`src/Kumunita.Web/Controllers/AdminSignupController.cs`** — the
  **admin-surface template** A11 extends: `[Authorize(Roles =
  Roles.GlobalAdmin)]`, a **dedicated controller** (the ADR 0050 rationale
  — `AdminController`'s ctor is pinned by two Web test harnesses), a thin
  wrapper over one `IIdentityService` seam, the `SignupAdminViewModel`
  (the `IsOpen` + `NotifyAdmins` bools), the `Save` / `SaveNotify` POST
  actions, the `TempData["info"]` flash. A11 adds the **3-state setting**
  to this surface (a new `SaveResidentInvite(mode)` POST mirroring
  `SaveNotify`). **Do not** change the `IsSignupOpenAsync` /
  `SetSignupOpenAsync` / `IsNotifyAdminsOnSignupAsync` /
  `SetNotifyAdminsOnSignupAsync` seams or their actions.
- **`src/Kumunita.Web/Views/Admin/Signup.cshtml`** — the admin view (the
  open / invite-only toggle + the notify toggle). A11 adds the
  3-state radio group.
- **`src/Kumunita.Core/Identity/IInvitationService.cs`** +
  **`InvitationService.cs`** (A07/A09) — the invitation seam the rule is
  **adjacent** to: the A11 rule decides whether a resident may *create*
  an invitation (A12's surface calls it before `CreateAsync` /
  `InviteAsync`). The `Invitation` doc + `EmailInvitationStatus` enum
  (the `Pending` / `Accepted` / `Revoked` / `Expired` shape) are the
  relationship to keep straight. The rule does **not** change any of
  those contracts.
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and
  the in-process `dotnet exec` invocation.
- **`git status`** + the branch (A10's `6c09e885` + A09's `efd82deb` must
  be intact; you are on `development`).

**The one critical seam detail (get this right first).** The setting is
a **new admin-settled instance value** — the **singleton-toggle shape**
(the `NotifyAdminsOnSignup` / `IsSignupOpenAsync` precedent), **not** a
per-row flag on the `Invitation` doc. It is **3-state** (`Off` / `On` /
`OnWithApproval`), stored and read through **one new `IIdentityService`
pair** (a `Get…` + a `Set…`, the `IsNotifyAdminsOnSignupAsync` /
`SetNotifyAdminsOnSignupAsync` shape), the `Set…` writes **exactly one
`AccessAudit` row, `Via = Admin`** (the action / `TargetKind` follow the
`signup.set-open` / `timezone.set-default` singleton shape — e.g. action
`signup.resident-invite`, `TargetKind` "signup"). The **floor is `Off`**
(a fresh instance ships with resident self-invite *off* — the ADR 0304
D4 "works while closed" posture; resident invite is opt-in, set by a
GlobalAdmin). The **rule** is a **pure, testable decision** that reads
the setting and returns a resident's invite posture (`Off` → refuse;
`On` → allow; `OnWithApproval` → allow, flag for the A12 approval
queue). The `// TODO ❓` open question — **who may invite and how many
per period** — is **flagged, not built**: A11 ships the *setting-driven*
rule; the who-may-invite + rate-limit refinement is A12's (or a follow-on),
and the prompt says to leave it as a `// TODO ❓` comment + a one-line
note, not a half-built rate limiter.

The **work**:

1. **The 3-state enum (Core).** `ResidentInviteSetting` (or the name the
   `EventRsvpMode` / `GuestAccess` precedent favours) with
   `Off` / `On` / `OnWithApproval`, `Off` the floor. A plain code-owned
   closed set (the `EventRsvpMode` shape) — not a stored string.
2. **The setting seam (Core).** `IIdentityService` + the impl:
   `GetResidentInviteSettingAsync()` → the enum (the `Off` floor) +
   `SetResidentInviteSettingAsync(ResidentInviteSetting mode,
   string adminSubjectId)` — the singleton-toggle shape, **exactly one
   `AccessAudit` row, `Via = Admin`** (the `SetNotifyAdminsOnSignupAsync`
   precedent). Unit tests pin the floor, the round-trip (all three states),
   and the one audit row.
3. **The rule (Core).** A testable decision seam that consults the
   setting and returns the invite posture for a resident — e.g. on
   `IInvitationService` (`CanInviteResidentAsync(residentId, ct)` or a
   small `InvitationPolicy`) returning `(bool MayInvite, bool
   RequiresApproval)`. Pins: `Off` → `MayInvite = false`; `On` →
   `MayInvite = true, RequiresApproval = false`; `OnWithApproval` →
   `MayInvite = true, RequiresApproval = true`. Add the
   **`// TODO ❓` who may invite and how many per period** as a comment
   + a one-line note (flagged, not built). **Do not** change the
   `CreateAsync` / `RevokeAsync` / `ValidateAsync` / `AcceptAsync` /
   `ListAsync` / `InviteAsync` contracts.
4. **The admin surface (Web).** `AdminSignupController`: add the 3-state
   setting to `SignupAdminViewModel` + a `SaveResidentInvite(mode)` POST
   (mirroring `SaveNotify`), the `Views/Admin/Signup.cshtml` 3-option
   radio group, the `TempData["info"]` flash. The `[Authorize(Roles =
   Roles.GlobalAdmin)]` gate is unchanged; the `IsSignupOpenAsync` /
   `SetSignupOpenAsync` / notify seams are unchanged.
5. **Keys (4 languages).** The 3 option labels + the flash strings
   (an `admin.*` / `signup.*` surface — **not** closed-set-pinned, so no
   pin re-baseline, but **C02 four-language parity** must stay green).
   de/fr/da machine-written, flagged for the parked native review. **Do
   NOT** add a `notifications.*` kind (A10 already added
   `invitation.accepted`; the A12 approval-queue kind is **not** A11's).

**Tests:**
- **Core** (a new `ResidentInviteSettingTests` over the
  `PostgresFixture` harness, the `IdentityServiceAccountNotificationTests`
  two-store boot as the template): (a) the **floor is `Off`** (a fresh
  instance); (b) the setting **round-trips** for all three states
  (`Get` after `Set`); (c) the write audit row — **exactly one**
  `AccessAudit`, `Via = Admin`, the singleton action; (d) the **rule** —
  `Off` → refuse; `On` → allow, no approval; `OnWithApproval` → allow +
  the approval flag set; (e) the `// TODO ❓` who-may-invite + rate is
  **flagged, not enforced** (the rule does not reject on rate yet).
- **Web** (the `AdminSignupController` direct-construction harness): (f)
  the POST **stores** the setting (the round-trip) + the GET seeds the
  form with the current value; (g) the view renders the **3-option radio**
  (not a raw enum value).

**Scope rules:**

- **Do not touch** the A07/A08/A09/A10 invitation seams
  (`CreateAsync` / `RevokeAsync` / `ValidateAsync` / `AcceptAsync` /
  `AcceptPresentedAsync` / `ValidatePresentedAsync` / `ListAsync` /
  `InviteAsync` + the A10 inviter-acceptance notify) — A11 is the
  **setting + rule** only. The A09 `/admin/invitations` surface is
  **already built** — A11 does not re-wire it; A12's approval queue will
  attach there.
- **Do not start** A12 (the resident "Invite a neighbour" profile/account
  menu + the admin approval queue on `/admin/invitations`), A13, A14, or
  A15 — A11 is the **admin setting + the testable rule** only. The
  `Done when` bar ("setting stored; rule tested") is the only acceptance
  gate.
- **One commit per change** (the house rule): A11 is one `feat(ux):`
  commit (`feat(ux): resident-invite admin setting (off/on/on-with-approval)
  + the invite rule (IMPROVE-UX A11)`). If you settle the 3-state setting
  + the who-may-invite rule as a decision, split a `docs(ux):` ADR
  0304-amendment commit first (the A07 / A10 precedent).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged for
  the parked native review). The **C02 four-language parity** test
  (`C02_DataAsEmbeddedResourcesTests`) must stay green.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress —
  A11 adds a setting + a rule (not a tracked anti-pattern); confirm it
  before committing.
- ⚠️ **Known pre-existing reds (not yours):** on `development`,
  `dotnet test --project tests/Kumunita.Web.Tests` currently has **4
  failures that pre-date A11** (verified on base): two
  `SecGate.DataClassCoverageTests` and `ImproveHarnessTests.CodeCheck
  _Gate_Passes` / `ImproveCheck_Gate_Passes` (the latter on **ADR index
  drift — ADRs 0303 / 0304 on disk but missing from the ADR index**). Do
  **not** attribute these to A11 or try to fix them here; your A11 suites
  must be green.

**Before committing:** `dotnet build`, then run the narrowest tests via
the in-process runner (see `testing-and-ci.md`): your new A11 Core tests
+ the `AdminSignupController` tests (the ADR 0050 signup-gate seam still
intact) + the `IdentityServiceAccountNotificationTests` (the ADR 0077
admin-lane tests) + the `C02_DataAsEmbeddedResourcesTests` four-language
parity + the `KwLRegistryConsistencyTests`. If a test fails, read the
error, adjust, continue — a failed test is a recoverable step, not a
stopping point.

Then report: where you stored the setting (the `IIdentityService` pair,
the singleton-toggle shape, the one `AccessAudit` row) + the enum name +
the `Off` floor, the rule seam (its name + the 3-state pins) + the
`// TODO ❓` you flagged, the `AdminSignupController` / view / keys you
added, the new test class(es) + test count, the commit hash(es) (and the
ADR 0304-amendment commit, if any), and the A07/A08/A09/A10 seams you
confirmed unchanged (all `IInvitationService` contracts + the A10
inviter-acceptance notify + the ADR 0050 signup-gate seams). Do not commit
unrelated work.
