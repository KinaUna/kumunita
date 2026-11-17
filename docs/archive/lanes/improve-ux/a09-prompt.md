---
id: a09-prompt
title: "Prompt: IMPROVE-UX A09 — the /admin/invitations surface: invite, list, resend, revoke + the invitation email"
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
> IMPROVE-UX **A09** — the **admin surface of the invitation lane** that
> A07's Core `InvitationService` + A08's `?invite=` gate were built to
> serve. A07 shipped the `Invitation` doc + `IInvitationService`
> (create/revoke/validate/accept); A08 wired the **receiving** side
> (a `?invite=` token opens sign-up while the gate is closed). A09 is
> the **sending** side: an admin invites one or more emails from
> `/admin/invitations`, lists sent/accepted/expired, revokes, and the
> invitation is delivered as an **email carrying the `?invite=` link**.
> **Who is it for:** the next coding agent (fresh context). **When to
> read the rest:** when starting A09 (A07 + A08 are shipped; the Core
> seam and the receiving gate exist, the admin surface does not).

# Fresh-context prompt: IMPROVE-UX A09

You are continuing the Kumunita repository on `development` (HEAD
`0e5fcf73`, **A08 just landed** — the `?invite=` sign-up gate + the
`ValidatePresentedAsync`/`AcceptPresentedAsync` overloads on
`IInvitationService`; **A07 before it** — ADR 0304 + the `Invitation`
doc + `IInvitationService`/`InvitationService` + `InvitationServiceTests`,
all green). A09 is the **second Web unit** of the invitation lane
(A07–A12) and the first *admin* surface on it. The plan card (in
`plan-improve-ux.md`, the A09 row):

> A09 — `/admin/invitations`: invite one or more emails, list
> sent/accepted/expired, resend, revoke; invitation email template;
> keys. Read first: an admin list view e.g.
> `Views/AdminGuests/Index.cshtml`, `EmailMessages.cs`.
> **Done when: admin invites end to end.**

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`docs/adr/0304-email-invitations.md`** — the ADR you are implementing
  against. **D3** is the `IInvitationService` seam (the `CreateAsync` /
  `RevokeAsync` contracts you call, plus the A08 presented-token
  overloads); **D4** the invariants (single-use, expiring, revocable,
  works while sign-up is closed); **D6(a)** the open question you are now
  settling: **the invitation expiry length — the ADR ships no default, and
  says "the A09/A12 surfaces will set it" (the proposed default is
  7 days)**; **D7** the erasure (the `Invitation` row is erased with the
  *inviter's* account — that is already wired, not A09's job).
- **`src/Kumunita.Core/Identity/IInvitationService.cs`** +
  **`InvitationService.cs`** — the seam A07/A08 shipped. A09 calls
  `CreateAsync(email, inviterSubjectId, expiresAt)` → `(token, row)` and
  `RevokeAsync(invitationId, inviterSubjectId)`. **Note the gap:** A07
  shipped **no list-read** — there is no way to list an inviter's (or
  all) invitations by status. That is the one new Core seam A09 needs
  (see below). Do **not** call `AcceptAsync`/`AcceptPresentedAsync` from
  here — that is the resident's receiving lane (A08).
- **`src/Kumunita.Core/Identity/IdentityService.cs`** →
  **`BuildVerificationEmailAsync`** (≈line 604) — the **exact
  invitation-email template precedent**: it builds `(subject, body)` from
  translation keys (`email.verify_subject` / `email.verify_body`) + a
  `string.Format(template, displayName, link)` + a raw-English fallback
  floor, and the caller stages it via `IMailerStage`. Mirror this shape
  for the invitation email (a new `email.invitation_*` pair + the
  `?invite=` link as the `{1}` argument).
- **`src/Kumunita.Core/Identity/IMailerStage.cs`** — the durable
  outbox seam: `StageAsync(session, idempotencyKey, recipient, subject,
  body)`. The idempotency-key scheme is per-email (§6.2 —
  `verify:{userId}:{attempt}`, `setup:{userId}`, …); the invitation's is
  `invite:{invitationId}`.
- **`src/Kumunita.Web/Controllers/AdminGuestsController.cs`** — the
  **admin-surface template** for A09: `[Authorize(Roles = Roles
  .GlobalAdmin)]`, a **dedicated controller** (the ADR 0050 rationale —
  `AdminController`'s ctor is pinned by two Web test harnesses, so a new
  dependency there would break them), a thin wrapper over one Core seam,
  the audit row is the **service's** (`Via = Admin`). Copy this shape.
- **`src/Kumunita.Web/Views/AdminGuests/Index.cshtml`** — the admin
  list-view template (the form + list rows + the kw-l localized
  strings).
- **`src/Kumunita.Web/Models/SignupViewModel.cs`** (A08) +
  **`Views/Account/Signup.cshtml`** (A08) — the **receiving** side you
  are now linking to: the invitation link is
  **`/account/signup?invite=<token>`**. The email A09 sends must
  carry exactly that link (A08's gate resolves it).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and
  the in-process `dotnet exec` invocation.
- **`git status`** + the branch (A08's commit `0e5fcf73` must be intact;
  you are on `development`).

**The one critical seam detail (get this right first).** A07's
`IInvitationService` has **create / revoke / validate / accept** but
**no list-read** — there is currently no way to query an inviter's
invitations (or all of them) by status for the A09 list view. A09
therefore adds **one read seam** to `IInvitationService`/
`InvitationService` — e.g. `ListAsync(inviterSubjectId?)` returning the
inviter's rows (or all rows when the admin is listing the whole
community) ordered newest-first, each carrying `Email`, `Status`
(`EmailInvitationStatus`), `CreatedAt`, `ExpiresAt`, `AcceptedAt`. It is
a **read lane — zero audit rows** (the `ValidateAsync` "the read is a
config load, not an access decision" floor, ADR 0304 D3). It is
**owner-scoped or GlobalAdmin** — the service does not re-check
`User`; the **Web controller enforces the standing** (`[Authorize
(Roles = Roles.GlobalAdmin)]`, the `AdminGuestsController` split, ADR
0050). The **virtual-Expired** state (ADR 0304 D1/D6): a `Pending` row
past `ExpiresAt` is *shown* as expired in the list (compute it from
`ExpiresAt` in the read — the enum is never explicitly set; the
`ValidateAsync` post-load comparison is the precedent). These land
**in this unit** (A09 is the first caller and owns the seam shape) with
tests. Do **not** change the existing `CreateAsync`/`RevokeAsync`/
`ValidateAsync`/`AcceptAsync`/`ValidatePresentedAsync`/
`AcceptPresentedAsync` contracts.

The **Web + Core work**:

1. **The list read seam (Core).** `ListAsync` on
   `IInvitationService`/`InvitationService` as above (read, zero audit,
   owner-or-GlobalAdmin, virtual-Expired surfaced). Unit tests in
   `InvitationServiceTests` (the A07/A08 harness — Testcontainers).
2. **`/admin/invitations` (Web).** A **dedicated
   `AdminInvitationsController`** (`[Route("admin/invitations")]`,
   `[Authorize(Roles = Roles.GlobalAdmin)]`, mirroring
   `AdminGuestsController`):
   - **GET** — the list: one section of rows (email, status —
     pending / accepted / revoked / expired, created, expires), newest
     first. A friendly empty state (`.kmb-empty` + one next action, the
     R29–R33 reference shape, e.g. `Groups/Index.cshtml:144`).
   - **POST (invite)** — accept one or more emails (a textarea /
     repeated field, split on newlines/commas), call
     `CreateAsync(email, inviterSubjectId, expiresAt)` for each (the
     inviter is the acting GlobalAdmin's subject id,
     `KumunitaPrincipal.SubjectId(user)`), stage **one invitation email
     per invitee** (see 3), and report which were created. Invalid /
     already-used / duplicate emails are reported, not silently dropped.
   - **POST (revoke)** — call `RevokeAsync(invitationId, inviterSubjectId)`
     (owner-scoped no-op if not the actor's — the A07 idempotent shape).
   - **POST (resend)** — `// TODO ❓` the resend semantics: the token is
     shown-once + hashed (ADR 0304 D2), so a *resend* is a **fresh
     invitation** (new token) for the same email — not a re-email of a
     dead token. Create a new row, send a new email, optionally revoke
     the superseded one. Record the choice as a `// TODO ❓` if you
     settle it, and note it for the SEC review.
3. **The invitation email (Core + Web).** A new
   `BuildInvitationEmailAsync(displayName, inviteLink, preferredLanguage)`
   (or equivalent) mirroring `BuildVerificationEmailAsync`: translation
   keys `email.invitation_subject` / `email.invitation_body`
   (`string.Format(template, displayName, inviteLink)` + a raw-English
   fallback floor); the **`inviteLink` is
   `{BaseUrl}/account/signup?invite=<token>`** (resolve `BaseUrl` the
   way the verification link does — `VerificationOptions.Value.BaseUrl`,
   or the `IInstanceBaseUrlReader` relative-path fallback). Stage it via
   `IMailerStage.StageAsync(session, idempotencyKey: "invite:" +
   invitationId, recipient: invitedEmail, subject, body)`. The body must
   name the inviter's community and the **expiry** so the resident knows
   the window.
4. **The default expiry (ADR 0304 D6(a)).** A09 owns the `expiresAt`
   value A07 left open: set the proposed **7 days** from `CreateAsync`'s
   call sites (or a small `InvitationOptions` if a config seam is
   cleaner) and record it. `// TODO ❓` whether it is admin-configurable
   (default 7 days) — the SEC review may settle it.
5. **Keys.** New strings go to **en/de/fr/da** (de/fr/da machine-written,
   flagged for the parked native review). The admin UI keys: a fresh
   namespace (e.g. `admin.invitations.*` or `invitations.*`). ⚠️ **Do
   NOT use `inv.*`** — that is the **Inventory** namespace
   (`inv.detail.*`, `inv.list.title` = "Inventory"); it is unrelated and
   already closed-set-adjacent. `admin.*`, `email.*` and `account.*` are
   **not** closed-set pinned (only `about.*`, `bm.*`, `pn.*`, `sort.*`
   are — see the `KnownTranslationKeys_ParityTests` /
   `BookmarksControllerTests` closed-set pins), so adding there needs no
   pin re-baseline — but the **C02 four-language parity** test
   (`C02_DataAsEmbeddedResourcesTests`) must stay green (every new `en`
   key present in de/fr/da).

**Tests:**
- **Core** (`InvitationServiceTests`, the A07/A08 Testcontainers
  harness): (a) `ListAsync` returns the inviter's rows newest-first and
  surfaces the **virtual-Expired** state (a `Pending` row past
  `ExpiresAt` shows as expired); (b) `ListAsync` writes **zero** audit
  rows (the read floor); (c) `ListAsync` is **owner-scoped** (another
  inviter's rows are not returned) or GlobalAdmin-visible per the seam
  shape you choose — pin whichever you settle.
- **Web** (the `AdminGuestsControllerTests` /
  `AdminSignupControllerTests` direct-construction harness as the
  template — `AdminInvitationsControllerTests`): (d) **invite** one or
  more emails → `CreateAsync` called once per valid email + one email
  staged per invitee (assert the `IMailerStage` was called with the
  `invite:{id}` idempotency key, the invitee's address, and a body
  carrying the `?invite=` link); (e) **list** renders the rows with the
  correct status labels (incl. an expired `Pending` row); (f) **revoke**
  a pending row → `RevokeAsync` called (owner-scoped); (g) the **empty
  state** renders when there are no invitations; (h) the **non-GlobalAdmin
  is refused** (the `[Authorize]` boundary — the `AdminGuestsController`
  gate test shape).

**Scope rules:**

- **Do not touch** the `Invitation` doc, `CoreDocTypes`, the eraser, or
  A07/A08's `CreateAsync`/`RevokeAsync`/`ValidateAsync`/
  `AcceptAsync`/`ValidatePresentedAsync`/`AcceptPresentedAsync`
  *contracts* — you **add** the `ListAsync` read seam (the A09 gap) but
  do not change what already exists. The A08 `?invite=` receiving gate is
  **already built** — A09 only *produces the link + email* that gate
  consumes; do not re-wire it.
- **Do not start** A10 (the acceptance notify — the A08
  `AcceptPresentedAsync` lane already emits the `invitation.accept`
  audit row; A10 adds the *notification* to the inviter), A11 (the
  resident-invite admin setting + Core rule), or A12 (the resident
  "Invite a neighbour") — A09 is the **admin** `/admin/invitations`
  surface + the invitation email only. The `Done when` bar ("admin
  invites end to end") is the only acceptance gate.
- **One commit per change** (the house rule): A09 is one `feat(ux):`
  commit (`feat(ux): /admin/invitations — invite, list, resend, revoke
  + the invitation email (IMPROVE-UX A09)`); if you settle the ADR
  0304 D6(a) expiry / D6(b) rate-limit as an amendment, split it as a
  `docs(ux):` commit first (the A07 precedent).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged
  for the parked native review). The **translation parity test** (the
  C02 class) must stay green.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress —
  A09 adds the list's friendly empty state (the `.kmb-empty` shape), not
  a tracked anti-pattern; confirm it before committing.

**Before committing:** `dotnet build`, then run the narrowest tests via
the in-process runner (see `testing-and-ci.md`): your new A09 Core tests
+ `InvitationServiceTests` (A07/A08 seam still intact) + the
`AdminInvitationsControllerTests` + the C02 four-language parity test.
If a test fails, read the error, adjust, continue — a failed test is a
recoverable step, not a stopping point.

Then report: the list-read seam shape you added (owner-scoped vs.
GlobalAdmin — and why), the resend + expiry decisions you settled (or
the `// TODO ❓` you flagged), the controller/view changes, the new
test class(es) + test count, the commit hash(es), and the A07/A08 seams
you confirmed unchanged. Do not commit unrelated work.
