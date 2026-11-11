---
id: a08-prompt
title: "Prompt: IMPROVE-UX A08 — wire `?invite=` into sign-up so an invited neighbour can register while the gate is closed"
tier: living
kind: plan
status: current
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
> IMPROVE-UX **A08** — the **Web seam of the invitation lane** that A07's
> Core `InvitationService` was built to serve. A07 shipped the
> `Invitation` doc + `IInvitationService` (create/revoke/validate/accept);
> A08 is the first *surface* on it: let `POST /account/signup` accept a
> `?invite=` token **while the sign-up gate is closed**, prefill the
> invited email, require it to match, and mark the invitation accepted.
> **Who is it for:** the next coding agent (fresh context). **When to read
> the rest:** when starting A08 (A07 is shipped; the Core seam exists, the
> Web surface does not).

# Fresh-context prompt: IMPROVE-UX A08

You are continuing the Kumunita repository on `development` (HEAD
`20bb4d06`, **A07 just landed** — ADR 0304 + the `Invitation` doc +
`IInvitationService`/`InvitationService` + `InvitationServiceTests`, all
green). A08 is the **first Web unit** of the invitation lane (A07–A12).
The plan card (in `plan-improve-ux.md`, the A08 row):

> A08 — Sign-up accepts `?invite=` while the gate is closed; email
> prefilled + must match; marks accepted; tests.
> Read first: `AccountController.cs` `Signup`, the `SignupClosed` view.
> **Done when: invited person can sign up with gate closed.**

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`docs/adr/0304-email-invitations.md`** — the ADR you are implementing
  against. D3 is the `IInvitationService` seam (the `ValidateAsync` /
  `AcceptAsync` contracts you call); D4 the invariants (single-use,
  expiring, **works while sign-up is closed**); D5 what is *not* changed.
- **`src/Kumunita.Core/Identity/IInvitationService.cs`** — the seam A07
  shipped. Note `AcceptAsync(tokenHash, email, displayName, password)`
  **already** (a) validates the token, (b) enforces that the signing-up
  email matches the invited address (the single-address pin), (c) runs the
  existing `RegisterAsync` lane, and (d) marks the row `Accepted`. A08's
  job is to *reach* that seam with the presented token — not to re-do the
  account creation.
- **`src/Kumunita.Web/Controllers/AccountController.cs`** — `Signup` GET
  (`:598`) and POST (`:638`), **both** currently gated on
  `identity.IsSignupOpenAsync()` (the ADR 0050 gate). This is the code you
  change. The `Signup` POST already catches
  `DisallowedEmailDomainException` + `InvalidOperationException` (the
  "email already exists" re-render shape) — the invitation path must fit
  the same re-render / flash conventions.
- **`src/Kumunita.Web/Views/Account/SignupClosed.cshtml`** + the
  `SignupClosedViewModel` / `SignupViewModel` in
  `src/Kumunita.Web/Models/SignupViewModel.cs` — the closed-page text
  (the `account.signup_closed_body` promise A08 starts honouring) and the
  form shape (the `Email` field A08 prefills).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (A07's commit `20bb4d06` must be intact;
  you are on `development`).

**The one critical seam detail (get this right first).** A07's
`AcceptAsync` takes a **token hash**, not the raw token. The Web host
**cannot** compute that hash itself: `IdentityService.Sha256Hex` is
`internal` to the Core assembly (no `InternalsVisibleTo` to the Web host)
— this is the ADR 0302 **Trap A** (the `KioskLinkService.
ValidatePresentedAsync` precedent: the hash lives in **one place**, in
Core). So A07's `IInvitationService` currently has **no presented-token
overload**. Before A08 can call `AcceptAsync` from the controller, it must
either (a) add a `ValidatePresentedAsync(presentedToken)` +
`AcceptPresentedAsync(presentedToken, email, displayName, password)` pair
to `IInvitationService`/`InvitationService` (the ADR 0302 D3 R16-overload
shape — hash the presented token inside Core with the shared
`IdentityService.Sha256Hex`, then delegate to the hash-based seam), or
(b) confirm whether A07 intended A08 to hash in the Web layer — **do not
invent a second hasher in the Web assembly** (that breaks the "the hash is
one fact" invariant). If you add overload(s), they land **in this unit**
(A08 is the first caller and owns the seam shape) with tests.

The **Web work** (all in `Kumunita.Web` unless noted):

1. **`?invite=` on the gate.** In `Signup` GET + POST: read the presented
   token (query param `invite` on the GET; a hidden form field carried into
   the POST — the token is a credential, keep it out of the visible form).
   When the gate is closed **and** a token is presented, resolve it via
   the Core seam; on a valid token, proceed to the signup form (prefilled)
   instead of the `SignupClosed` notice. A closed gate with **no** valid
   token still renders `SignupClosed` (the ADR 0050 floor is unchanged).
2. **Prefill + must-match.** The invited email is prefilled in the form
   (`SignupViewModel.Email`), and the POST requires the signing-up email to
   match the invitation's `Email` (A07's `AcceptAsync` already enforces
   this at the Core layer — surface a friendly re-render notice on mismatch
   rather than a raw exception).
3. **Mark accepted.** A successful sign-up runs A07's `AcceptAsync` (which
   does the `RegisterAsync` + the `Accepted` mark + the
   `invitation.accept` audit row) — the invited person then lands on the
   same "check your inbox for the verification link" flash as the open-gate
   path (A07's seam already returns the `ThinPrincipal`).
4. **`SignupClosed` honesty.** When closed + no token, the page should tell
   the resident the *real* path (an admin or an invited neighbour sends
   you an invitation link), not "an administrator will add your account"
   with no seam. New text goes to en/de/fr/da (de/fr/da machine-written,
   flagged for the parked native review).

**Tests** in `tests/Kumunita.Web.Tests/` (the Web controller/integration
shape — use an existing `AccountController`/signup test as the harness
template): (a) gate **closed** + a valid `?invite=` → the signup form
renders (prefilled), not the closed notice; (b) gate **closed** + no/invalid
token → `SignupClosed` (the ADR 0050 floor intact); (c) a sign-up POST with
the token **and the matching email** succeeds and the `Invitation` row is
`Accepted` (A07's seam does the write); (d) a sign-up POST with the token
and a **mismatched email** is refused (the A07 single-address pin) with a
friendly re-render, not a crash; (e) if you added a presented-token
overload in step 1, pin that it agrees with the hash-based seam on the same
token (the ADR 0302 R16 "the two paths agree" test).

**Scope rules:**

- **Do not touch** the `Invitation` doc, `CoreDocTypes`, the eraser, or the
  `InvitationService` hash/accept *logic* from A07 — you may **add** a
  presented-token overload (the Trap A shape) if needed to call it from the
  Web, but do not change the existing `CreateAsync`/`RevokeAsync`/
  `ValidateAsync`/`AcceptAsync` contracts.
- **Do not start** A09 (`/admin/invitations`), A10 (the acceptance notify),
  A11 (the resident-invite setting) or A12 (the resident "Invite a
  neighbour") — A08 is the `?invite=` sign-up wiring only. The
  `Done when` bar ("invited person can sign up with gate closed") is the
  only acceptance gate.
- **One commit per change** (the house rule): A08 is one `feat(ux):`
  commit (`feat(ux): wire ?invite= sign-up while the gate is closed
  (IMPROVE-UX A08)`); if you add a `// TODO ❓`-flagged ADR amendment for
  the presented-token overload, split it as a `docs(ux):` commit first
  (the R10 / A07 precedent).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged for
  the parked native review). The **translation parity test** (the C02 class)
  must stay green.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress —
  A08 adds no tracked anti-pattern, but confirm it before committing.

**Before committing:** `dotnet build`, then run the narrowest tests via the
in-process runner (see `testing-and-ci.md`): your new A08 Web tests +
`InvitationServiceTests` (A07's seam still intact) + the translation parity
test (the C02 class). If a test fails, read the error, adjust, continue —
a failed test is a recoverable step, not a stopping point.

Then report: the presented-token approach you took (the A07 overload vs.
confirming an existing path — and why), the controller/view changes, the
new test class + test count, the commit hash(es), and any `// TODO ❓` you
flagged. Do not commit unrelated work.
