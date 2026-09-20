# ADR 0050 — Admin-managed sign-up gate: open vs invitation-only

Status: Accepted
Date: 2026-09-16

## Context

Self-service sign-up (the `AccountController.Signup` lane — `RegisterAsync` +
the verification email + the admin manual-verify valve) is deliberately left
**open** today so the development team can create accounts without an admin
standing in the way. That is the right call for the current circle, and two
documents already commit the instance to closing it before the community
widens:

- **README — *Deferred (future, by design)*:** "The long-term default should be
  **invitation-only accounts**… open sign-up is an opt-in, not the default…
  Decide and land before the community is open beyond the development circle
  (SECURITY.md §6 open items — the control that answers adversary A2, the
  signup bot)."
- **SECURITY.md §6 — *Open items*:** the "Sign-up is currently open,
  self-service" row, A2 (signup bots), "the control that answers A2 better than
  rate-limiting alone."

The requirement that prompted this ADR: **sign-up should be admin-managed, not
hard-coded.** For testing and for future expansion, a GlobalAdmin should be able
to flip the instance between **open** (residents may self-register) and
**invitation-only** (closed to new self-service accounts), without a rebuild and
without touching the host.

The natural home is the same "instance value, admin-settled, `true`/sane floor"
shape the two closest siblings already use — ADR 0019 (time zone) and ADR 0020
(date format) — both of which store an admin-settled platform value on the
`LocaleSettings` singleton (id `"singleton"`, ADR 0005 B), read it through a
plain service method, and write it through a **single audited** lane that appends
exactly one `AccessAudit` row (`Via = Admin`) in the same session/commit. A
boolean policy toggle is a weaker cousin of the same trio: a single admin-settled
value, no per-resident override, no hard-coded fallback tier — just a value
whose **floor is `true`** (a fresh instance is open, so a development deployment
is never locked out of its own accounts).

This ADR lands the **control surface and the gate**, not the invitation
mechanism. The invitation lane (an admin invites a resident, the resident
self-serves a password from an admin-sent link) is a separate, larger design
(token lifecycle, expiry, admin UX — the exact "decision needed" the two open
items name) and is deliberately **out of scope** here. What this ADR delivers is
the *switch* those items are waiting on: the ability to close self-service
sign-up today, and to reopen it.

## Decision

- **Store the policy as an additive field on the existing singleton — a new
  document, not a fourth.** `Kumunita.Core.Localization.LocaleSettings.IsSignupOpen`
  (`bool`, default `true`) joins `DefaultLanguageCode`, `DefaultTimezone`, and
  `DefaultDateFormat` on the **same** `mt`-schema document (id `"singleton"`,
  the ADR 0005 B / ADR 0019 / ADR 0020 shape). One "platform defaults" document,
  and a boolean is the cheapest member of the trio.

- **Additive, migration-free (ADR 0004 §B.1).** The field defaults to `true`, so
  an existing `LocaleSettings` row reads as "open" without a migration, and a
  **fresh** instance — whose singleton is materialized by the seeder — also
  starts **open**. No migration file: Marten applies the added boolean column
  automatically, the exact shape of the `LocaleSettings.DefaultTimezone` /
  `DefaultDateFormat` additions.

- **A `true` floor — the read seam never returns "closed" on a missing row.**
  `IIdentityService.IsSignupOpenAsync()` loads the singleton and returns
  `settings is null || settings.IsSignupOpen`. The `null` clause is the floor:
  a missing or un-migrated row is treated as **open**, so a deployment can never
  be locked out of the very account-creation lane it needs to reach its admins.
  (The inverse of ADR 0020's "blank reads as the floor" — here the floor is the
  *permissive* value, because the consequence of a wrong guess is lockout, not
  a cosmetic date format.)

- **One seam ADD on `IIdentityService` — read and audited write.**
  - **Read:** `Task<bool> IsSignupOpenAsync()` — no audit row (a plain read,
    matching the `GetDefaultTimezoneAsync` / `GetDefaultDateFormatAsync` shape).
  - **Write:** `Task SetSignupOpenAsync(bool open, string adminSubjectId)` — the
    **single audited write lane** (the exact shape of
    `SetDefaultDateFormatAsync` / `SetDefaultTimezoneAsync`): load-or-create the
    singleton, set `IsSignupOpen`, and store exactly one `AccessAudit` row
    (`Action = "signup.set-open"`, `TargetKind = "signup"`, `TargetId = "signup"`,
    `Via = Admin`, `Outcome = Allow`, `ActorId = adminSubjectId`), committed in
    the same session. The Web boundary owns the GlobalAdmin standing check (the
    Core seam does not re-check `User` — the ADR 0019/0020 split).

- **Admin surface (`/admin/signup`, GlobalAdmin-gated).** A dedicated
  `AdminSignupController` — **not** a new action on `AdminController` — mirroring
  `AdminTimezoneController` (ADR 0019) and `AdminDateFormatController` (ADR
  0020) exactly: `[Route("admin/signup")]`,
  `[Authorize(Roles = Roles.GlobalAdmin)]`, a thin `View`/`Save` pair that maps
  the boolean to the two labeled options ("Open — residents can sign up" /
  "Invitation-only — new self-service accounts are gated"), and a
  `TempData["info"]` + redirect on save. The `/admin` index link is a single
  `admin.signup_title` button alongside the language/time-zone/date-format
  entries.

- **The gate is authoritative on both the read and write surface.**
  - **GET `/account/signup`:** if the gate is closed, render a static
    `SignupClosed` notice (a no-bind view model — nothing to tamper with)
    instead of the form.
  - **POST `/account/signup`:** the gate is re-checked **as the first statement**,
    before any model-state validation or `RegisterAsync` — so a closed instance
    refuses the write even for a hand-crafted form post, not merely a hidden
    form. (The gate is checked at the boundary, not in a view-model, so the
    refusal cannot be bypassed by a different model.)
  - **Affordances hidden, not just gated:** the signed-out "Sign up" nav item
    (`_AccountNav`) and the "No account yet?" line on `/account/login` are only
    rendered when the gate is open — a closed instance presents no
    self-service path at all.

- **Translations.** Seven new `<kw-l>` keys, added to **all four** of
  `KnownTranslationKeys` (`EnValues` / `DeValues` / `FrValues` / `DaValues`) —
  `admin.signup_*` (the admin control surface) and `account.signup_closed_*`
  (the closed notice). The `KwLRegistryConsistencyTests` + U04 completeness
  tests pin that every key is present and non-empty in each language, so the
  four-way set is enforced, not optional.

- **No config, no restart.** The value is **data** on an `mt`-schema document,
  not appsettings — a change is live on the very next request (the same "data,
  not config" posture as ADR 0019 / ADR 0020).

- **Out of scope (deliberately):** the invitation mechanism itself — the token
  lifecycle, expiry, and admin invite UX that the README Deferred section and
  SECURITY.md §6 both name as "decide the mechanism before wider rollout."
  This ADR lands the **gate**, which is the control both documents say answers
  A2 better than rate-limiting alone; the invitation lane is the follow-on that
  makes "closed" a place a resident is actually invited *to*, rather than a dead
  end.

## Consequences

Positive
- **The README and SECURITY.md open item is now a switch, not a TODO.** The two
  documents that committed the instance to invitation-only before wider rollout
  now have the control they were waiting on: a GlobalAdmin can close self-service
  sign-up on the next request, and reopen it, with no rebuild and no host change.
- **Mirrors the ADR 0019 / ADR 0020 precedent exactly.** The "admin writes a
  singleton + audited lane; the read seam floors to the sane value" shape is the
  same, the seams are the same, the test pattern is the same. A developer who
  knows the time-zone and date-format features knows this one; the `true` floor
  is the same "missing row → safe default" clause, pointed at the permissive
  value.
- **The gate is authoritative, not decorative.** Because the POST path re-checks
  the gate before any write and the nav/login affordances are *hidden* (not just
  disabled), a closed instance presents no self-service path — not even to a
  hand-crafted form post. The refusal is a property of the write lane, not of the
  form.
- **No migration, no lockout risk.** The field is additive (ADR 0004 §B.1) and
  floors to `true`, so a fresh or pre-existing instance is never accidentally
  locked out of account creation by a missing or stale row.

Negative / accepted risks
- **"Closed" is currently a dead end, not an invitation.** With the gate shut
  and no invitation lane yet, a resident who is not an admin has no way in at
  all. This is **accepted** and **intentional** for the current circle (the
  development team already has accounts, and an admin can still create them
  through the Guardian / admin lanes); the invitation mechanism is the
  follow-on that closes the loop, and this ADR deliberately does not block on
  it.
- **A second "is sign-up open?" read path exists.** `AccountController.Signup`
  (GET and POST) and `_AccountNav` each call `IsSignupOpenAsync()` independently
  per request. They all read the same singleton through the same seam, so they
  agree — the same "multiple read sites that must keep the same floor" shape the
  timezone / date-format ADRs name; there is no single cached resolver because
  the value is a one-row read and the floor is identical everywhere.
- **The toggle does not revoke or disable existing accounts.** Closing the gate
  stops *new* self-service registrations; it has no effect on residents who
  already have accounts (including the development team's), which is the intent
  — "existing residents are unaffected" is stated in the admin surface's own
  lede.
