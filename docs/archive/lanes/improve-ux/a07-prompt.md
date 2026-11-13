---
id: a07-prompt
title: "Prompt: IMPROVE-UX A07 — the ADR 0304 invitation doc + service (the invite lane's core seam)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0142-delete-account.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A07** — the ADR + the Core `Invitation` doc + the create/revoke/accept
> service that **starts the invitation lane** (A07–A12). It is the closed-loop
> fix for E9 (invitation-only sign-up is a dead end: the closed page promises
> "an administrator will add your account" but no create/invite action
> exists). **Who is it for:** the next coding agent (fresh context).
> **When to read the rest:** when starting A07 (the Core seam that A08's
> `?invite=` sign-up, A09's `/admin/invitations` surface, A10's acceptance
> notify, A11's resident-invite setting, and A12's resident "Invite a
> neighbour" all build on).

# Fresh-context prompt: IMPROVE-UX A07

You are continuing the Kumunita repository on `development` (HEAD `669a4fd5`,
R33 — the final empty-states batch — just landed and closes the empty-states
work). The lane's running log is
`docs/plans-milestones/in-progress/improve-ux-handoff-notes.md`. A07 is the
**first invitation unit** — a new lane, not a continuation of the empty-states
work.

**Implement IMPROVE-UX unit A07 only** — the ADR + the `Invitation` doc + the
create/revoke/accept service. The plan card (in `plan-improve-ux.md`, the A07
row):

> A07 — **ADR** + Core `Invitation` doc (email, inviter, token *hash*,
> expiry, status) + create/revoke/accept service; tests. Read first:
> `IdentityService.cs:140-200` (`RegisterAsync`),
> `IdentityPlatformGateService.cs`. Done when: **tests: expired/revoked
> refused, single use.**

Decision D7 (in the plan's Decisions table) is the shape you are building:
*email invitations from admins, plus an admin setting that lets residents
invite neighbours (optionally with admin approval)* — single-use, expiring
links that work while sign-up is closed. A07 is the **Core seam + ADR**; the
Web surface (A08–A12) is separate later units — **do not start them**.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** — the
  doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the comment
  rule, the don't-pause-mid-task rule).
- **`docs/adr/0142-delete-account.md`** — the existing self-service-delete /
  account-creation lane this sits beside (the `IdentityService` account-ops
  conventions, the `AccountSignup` notification emit at `IdentityService.cs:213`).
- **`src/Kumunita.Core/Identity/`** — `IdentityService.cs` (`RegisterAsync`
  at `:148` + the shared body, `IIdentityService.cs`),
  `IdentityPlatformGateService.cs` + `IIdentityPlatformGateService.cs` (the
  `IsSignupOpenAsync` gate A08's `?invite=` must open),
  `IdentityPrincipalStore.cs` (the user store). The **token-hash-at-rest +
  revocable + single-use** shape is the exact parallel to the `KioskLink` doc
  (ADR 0302, `src/Kumunita.Core/Kiosk/KioskLink.cs` +
  `IKioskLinkService.cs`) — mirror that precedent for `TokenHash`, the
  SHA-256 hex-digest-only rule, the `RevokedAt?` / status fields, and the
  service's `Create/Revoke/Validate` triad. For the digest, reuse the shared
  **`IdentityService.Sha256Hex`** helper the `KioskLink` service already
  uses (`src/Kumunita.Core/Identity/IdentityService.cs`) — do not reinvent
  the hash.
- **`src/Kumunita.Web/Controllers/AccountController.cs`** — `Signup` GET
  (`:598`) and POST (`:638`), both gated on `IsSignupOpenAsync()`; the
  `SignupClosed` view. You need this for context (A08 wires `?invite=`),
  not to change it.
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (R33's commit must be intact; you are on
  `development`).

The **Core work** (all in `src/Kumunita.Core/Identity/` unless noted):

1. **ADR 0304** (check `docs/adr` for the next free number — the highest
   currently is **0303**, so 0304 is expected, but re-check; do not reuse an
   existing number): the invitation decision. State the D7 shape, the
   token-*hash*-at-rest rule, the single-use / expiring / works-while-sign-up-
   is-closed / revocable invariants, and — like ADR 0301 (grace period) and
   ADR 0302 (kiosk link) did — flag the open question(s) as
   `// TODO ❓` for the later SEC review: invitation **expiry length**,
   **max invitations per inviter per period**, and whether a
   **resident-invited** invitation needs admin approval (A11's setting).
2. **`Invitation` doc** (one row per invitation): `Email` (lowercased,
   trimmed), `InviterSubjectId`, `TokenHash` (the SHA-256 of the long random
   token — **never the raw token**), `CreatedAt`, `ExpiresAt`, `Status`
   (enum `Pending / Accepted / Revoked / Expired`), `AcceptedAt?`,
   `AcceptedSubjectId?`. Register it on the **existing `CoreDocTypes`**
   surface (the ADR 0004 §B.1 additive precedent — delta-detected by Marten,
   no DDL, no new `*DocTypes`). Carries `[DataClass]` (the ADR 0298 SecGate
   rule).
3. **`IInvitationService` + `InvitationService`** —
   `CreateAsync(email, inviterSubjectId, expiresAt)` (generates the long
   random token, stores only the hash, returns the raw token **once** for the
   caller to embed in the link/email), `RevokeAsync(invitationId,
   inviterSubjectId)`, `ValidateAsync(tokenHash)` (returns the row if
   `Status == Pending` and not past `ExpiresAt`, else `null` — the caller
   renders the denied/closed shape), and `AcceptAsync(tokenHash, email,
   displayName, password)` (verifies the token **and** that the signing-up
   email matches the invitation's email, marks the row `Accepted`, and runs
   the existing `RegisterAsync` lane — one account per invitation, the
   **single-use** pin). Audit rows: one `invitation.create` + one
   `invitation.revoke` + one `invitation.accept` (`Via: Owner` for the
   inviter, `TargetKind = "invitation"`, the `KioskLink` precedent); the
   validate read is unaudited (a config load, not an access decision).
4. **DI registration** in the `Identity/` `DependencyInjection.cs` (the
   `IKioskService` precedent).
5. **Tests** in `tests/Kumunita.Core.Tests/` — an `InvitationServiceTests`
   class: (a) an **expired** invitation is refused (`ValidateAsync` returns
   `null` past `ExpiresAt`), (b) a **revoked** invitation is refused, (c)
   **single use** — a second `AcceptAsync` for the same token is refused once
   the row is `Accepted`, (d) `AcceptAsync` with a **mismatched email** is
   refused (the token is only good for the invited address), (e) the stored
   value is a hash, never the raw token (the "hash only at rest" pin).

**Scope rules:**

- **Do not touch** `AccountController.Signup` / the `SignupClosed` view /
  `AdminSignupController` — A08 (the `?invite=` wiring), A09
  (`/admin/invitations`), A10 (the acceptance notify), A11 (the resident
  setting) and A12 (the resident "Invite a neighbour") own those. A07 is the
  Core seam + ADR only.
- **Do not start A08–A12 or the operator lane (O01–O06).** The unit card's
  "Done when" is the only acceptance bar.
- **One commit per change** (the house rule): the ADR + the `Invitation` doc
  + the service + the tests + the DI registration land in **one**
  `feat(ux):` commit (`feat(ux): ADR 0304 + Invitation doc + create/revoke/
  accept service (IMPROVE-UX A07)`); the ADR is a `docs(ux):` commit if you
  split it (the R10 precedent: `docs(ux): ADR 0301 — … (IMPROVE-UX R10)`).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged for
  the parked native review) — but A07 is Core + ADR, so the only new strings
  are the audit-row action names (which are not localized).
- **The translation parity test** must stay green (it will — you add no
  localized keys).

**Before committing:** run the narrowest tests via the in-process runner
(see `testing-and-ci.md`): `InvitationServiceTests` (your new class) +
`IdentityServiceTests` (the existing `RegisterAsync` lane intact) + the
translation parity test (the C02 class), and `dotnet build`. If any test
fails, read the error, adjust, continue — a failed test is a recoverable
step, not a stopping point.

Then report: the ADR number you used, the `Invitation` doc shape, the
service method signatures, the test class name + the number of tests, the
commit hash(es), and the `// TODO ❓` open questions you flagged for the
later SEC review (expiry length, max-per-inviter, resident-invite approval).
Do not commit unrelated work.
