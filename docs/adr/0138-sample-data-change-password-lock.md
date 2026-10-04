# ADR 0138 — Sample-data change-password lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of changing their own password — except the sample `GlobalAdmin` — plus the resident self-serve change-password surface it guards

Status: Accepted
Date: 2026-10-04
Builds on: 0055 / 0056 (the sample-data opt-in lane — its `SampleData__Enabled`
config flag is the *existence gate* this surface is unreachable without, so the
admin option is "unreachable by construction" on a real deployment exactly as the
seeder is); 0078 (the closed `SampleAccountEmails` set — the *single source of
truth* this lane's "is this a sample account?" conjunct compares against, so the
lock and the notification-suppression gate cannot drift); 0050 (the ADR 0050
sign-up gate — the *exact template* this lane mirrors: one admin-settled
instance value on the `LocaleSettings` singleton, one audited write lane, the
gate authoritative on both the hidden form and the write path)
Amends: 0006 (its §A frozen `IIdentityService` surface gains four
*additive* methods — `IsSampleDataEnabledAsync` / `IsSamplePasswordChangeLockedAsync` /
`SetSamplePasswordChangeLockedAsync` / `IsChangePasswordLockedForAsync` — and the
existing `ChangePasswordAsync` gains an ADR-0138 self-serve guard; all
additive, so no existing caller's contract changes); 0129 (its embedded
sample-data document's `accounts` list is the closed set the lock keys on —
unchanged, now also read by the change-password decision)

A deployed demo site (`SampleData__Enabled=true`) is a **public** instance whose
sample accounts are *shared* credentials: everyone who signs in as "Anna the
resident" signs in as the *same* account. Until this ADR there was no resident
self-serve change-password surface at all (the `IIdentityService.
ChangePasswordAsync` seam existed but had no Web caller), and so no way to
change a sample password. Now that a change-password surface is landing (the
residents' "Change password" form), a new seam opens: a visitor testing the demo
could change a sample account's password and break the shared credential for the
everyone else who uses it. ADR 0138 closes that seam with a **GlobalAdmin option,
shown only when the sample data is enabled**: flip it on and the demo accounts
(every `SampleAccountEmails` member) are locked out of changing their own
password — **except the sample `GlobalAdmin`**, whose own credential the admin
must still be able to set/recover. It is a **named lane on the shipped
sample-data surface**, not a renumber or a milestone: the `Milestones.cs` /
README Roadmap / `MilestonesTests.cs` triple is **untouched**.

The one thing the whole lane respects:

- **The lock is a pure read decision, and the write guard is the single place
  the rule bites** (the ADR 0050 gate posture): the
  `IsChangePasswordLockedForAsync(subjectId)` read combines the four conjuncts in
  **one place**, so the resident `GET /account/password` notice and the
  `ChangePasswordAsync` self-serve guard **agree by construction**; the
  `UnauthorizedAccessException` (the one refusal) is thrown in the **Core**
  `ChangePasswordAsync` before any write (no audit row for the blocked attempt
  — the fail-closed pin), and the Web surfaces it. `IActionResult` / HTTP
  never cross the seam (ADR 0006-D).
- **The closed set is reused, not re-derived**: the "is this a sample account?"
  conjunct reads the *same* `SampleDataSeeder.SampleAccountEmails` the ADR 0078
  notification-suppression gate compares against — one source of truth, so the
  lock and the suppression cannot drift (the ADR 0129 embedded document is the
  single origin of both).

## Context

A deployed demo site (`Production` + `SampleData__Enabled=true`, ADR 0056) is
*public*: the seed admin keeps its `SeedAdmin__` setup-token lane, the other demo
accounts get random high-entropy passwords, and a credentials summary is emailed
to the seed admin. But the accounts themselves are **shared** — "Anna", "Ben",
"Carla" are one account each, and anyone who has the credential (or, in
Development, the documented weak one) signs in as *that* account. A demo's whole
point is that many visitors can exercise the platform against the *same*
accounts; a single visitor changing one of those passwords breaks it for the
rest.

Two facts about the current surface shape the decision:

- **There was no resident change-password surface at all** — the
  `IIdentityService.ChangePasswordAsync(subjectId, newPassword, byAdmin)` seam
  existed in Core but had **no Web caller** (the only password-write surfaces
  were the one-time `SeedAdmin__` setup lane, and nothing else). So there was
  nothing to lock; the lane had to add the resident surface *and* the lock
  together (the user confirmed the full scope).
- **The sample set is code-owned and closed** (ADR 0078 / 0129):
  `SampleDataSeeder.SampleAccountEmails` is the single source of truth
  "this account is a sample account" already relies on (the production
  notification-suppression gate). The lock keys on the *same* set — no second,
  drift-prone list.

## Decision

### The four-conjunct decision (the single place the rule lives)

`IIdentityService.IsChangePasswordLockedForAsync(string subjectId)` returns
`true` (locked) **iff** all four hold:

1. `IsSampleDataEnabledAsync()` — the instance carries the
   `SampleData__Enabled` flag (a pure config read; a real deployment never
   carries it, so the lock is *unreachable by construction*, ADR 0056);
2. `IsSamplePasswordChangeLockedAsync()` — the admin opted in (the
   `LocaleSettings.SamplePasswordChangeLocked` gate, the `false` floor);
3. the subject is a **sample account** — its `Profile.Email` is a member of the
   closed `SampleAccountEmails` set (the ADR 0078 single source of truth); and
4. the subject is **not** a `GlobalAdmin` (the sample admin is exempt).

Conjuncts 3 and 4 short-circuit, so **every real (non-sample) account and the
sample admin are never locked** — only a non-admin sample account on an opted-in
demo instance is. This is a *deliberate inverse* of the codebase `true`-floor
convention (the `LocaleSettings.MessagingEnabled` shape): the default is
`false` (locked-out is **off**), so a fresh or real instance never blocks a
password change.

### The write guard (enforcement)

`IIdentityService.ChangePasswordAsync(subjectId, newPassword, byAdmin)` — the
self-serve lane only (`byAdmin: false`) — is additionally subject to
`IsChangePasswordLockedForAsync`: when it returns `true`, the method throws
`UnauthorizedAccessException` **before** any write (no audit row for the blocked
attempt; no security-stamp rotation, so the account stays usable). The admin
reset lane (`byAdmin: true`) is **always allowed** — an admin must be able to
recover a demo credential they set. The Web's `POST /account/password` maps the
exception to the static `ChangePasswordLocked` notice (defense in depth over the
Core guard).

### The admin option (shown only when sample data is enabled)

A new `GlobalAdmin`-gated `/admin/sample` surface (the ADR 0050
`AdminSignupController` template, its own controller so the `AdminController`
test-pinned constructor is untouched):

- `GET /admin/sample` → **404** when `IsSampleDataEnabledAsync()` is false (a
  real instance — the surface is unreachable by construction); otherwise the
  lock toggle (seeded from `IsSamplePasswordChangeLockedAsync`, the `false`
  floor).
- `POST /admin/sample` → **404** when the flag is unset (the gate is
  authoritative on the write path too — a crafted POST to a real instance is
  refused, the ADR 0050 gate shape); otherwise
  `SetSamplePasswordChangeLockedAsync(locked, actor)` — the single audited
  write lane (exactly one `AccessAudit` row, `Via: Admin`, action
  `sample.set-password-lock`, `TargetKind`/`TargetId` "sample"), then a
  surfaced `info` + redirect (the change is live on the very next
  `IsChangePasswordLockedForAsync` / render — data, not config).

The link to `/admin/sample` on `/admin/platform` is rendered **only** when
`IsSampleDataEnabledAsync()` is true (the `AdminPlatformViewModel.ShowSampleData`
flag, set in `AdminController.Platform` from the existing `IIdentityService`
dependency — no test-pinned constructor change).

### The resident surface

A new self-serve `GET /account/password` + `POST /account/password` on
`AccountController` (the subject is the signed-in principal, never a path
param):

- **GET** — the change-password form (current / new / confirm); when
  `IsChangePasswordLockedForAsync` returns `true`, the static
  `ChangePasswordLocked` notice instead (the ADR 0050 `SignupClosed` shape).
- **POST** — the guard is authoritative (a locked account is denied with the
  notice, no write); otherwise the **current** password is verified against the
  account (`CheckPasswordAsync` — the self-serve lane proves it is really this
  resident), and the new password is written through
  `ChangePasswordAsync(subject, new, byAdmin: false)` (the single audited write
  lane, `Via: Owner`). On success the resident is signed out (the credential
  just changed — confirm the new one on the next sign-in) and redirected to the
  login surface with an `info` flash.

"Change password" is a **settings tab** (a `nav.change_password` kw-l key in
the shared `_SettingsTabs.cshtml` sub-nav, reached from the account menu's
single "Settings" entry) — moved out of the account dropdown 2026-10-04, the
ADR 0028 "Children" / ADR 0080 idiom; a locked sample account sees the notice
when it opens the page (the link stays, the write is refused).

### Persistence

`LocaleSettings.SamplePasswordChangeLocked` — an **additive** `bool` on the
existing `LocaleSettings` singleton (the same doc the ADR 0050 `IsSignupOpen` /
ADR 0077 `NotifyAdminsOnSignup` / ADR 0019 / ADR 0020 lanes read and write),
default `false` (locked-out off). No new document, no migration — a missing
row reads as **not locked** (the `false` floor), so existing instances are
unaffected (ADR 0004 §B.1 additive-field shape).

### Localization

The new `kw-l` keys (`account.change_password_*`, `nav.change_password`,
`admin.sample_*`) are registered in the `KnownTranslationKeys` closed registry
in **all four** languages (en/de/fr/da) — the `KwLRegistryConsistencyTests`
"every view key is registered" invariant and the per-key parity pins hold.

## Consequences

**Positive**

- A demo site can be exercised by many visitors against the shared sample
  accounts without one visitor breaking the credential for the rest — the
  "test features, don't break the shared login" property the demo exists for.
- The sample `GlobalAdmin` keeps its own password lane (the admin can always
  set/recover a demo credential), and real residents are **never** affected —
  the lock is a pure demo-instance concern, gated on the `SampleData__Enabled`
  flag so it is unreachable by construction on a real deployment.
- The decision is **one** pure read in Core, so the resident form, the locked
  notice, and the `ChangePasswordAsync` guard cannot disagree (the ADR 0050
  gate-is-authoritative posture); the closed sample set is the **same** source
  of truth ADR 0078 uses, so the lock and the suppression gate cannot drift.
- Every flip is audited (`sample.set-password-lock`), every self-serve change
  is audited (`password.change`, `Via: Owner`) — the audit-by-default
  invariant holds end to end.

**Negative / cost**

- A resident change-password surface now exists that did not before — a new
  self-serve write lane with a current-password check and a sign-out-on-success
  behavior (the `ChangePasswordAsync` security-stamp rotation invalidates the
  account's other sessions). This is the intended scope the user confirmed.
- The lock applies to *sample accounts only* (by the closed `SampleAccountEmails`
  set) — an account created *on* a demo instance by a real resident is **not**
  a sample account (its email is not in the set) and is unaffected. That is the
  correct boundary (the shared credential is the sample *seed* accounts), but it
  is a closed-set membership, not a "demo instance" flag — a real resident who
  happens to use a sample-set email would be treated as a sample account. This
  is acceptable (the sample-set emails are deliberately-fictional
  `examplium.com` addresses, distinct from the real `kumunita.com` domain, ADR
  0056), and the same closed set ADR 0078 already relies on.

## Alternative considered

- **Lock by `SampleData__Enabled` + role (no per-account set)** — rejected: it
  would lock *every* non-admin account on a demo instance (including a real
  resident who signed up on it), and would need no closed set. The user's
  requirement is specifically the *sample* accounts (the shared seed
  credentials), and reusing the ADR 0078 closed set is the single source of
  truth that already answers "is this a sample account" — so the closed-set
  membership is the honest, drift-proof conjunct.
- **No resident surface; admin-only reset with an implicit lock** — rejected:
  the user explicitly confirmed the full scope (the resident form **and** the
  admin option). And an implicit lock with no way for the admin to see or toggle
  it would leave the demo's "can I change a password?" question unanswered.
