# ADR 0194 — Sample-data MFA (two-factor) lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of the MFA settings surface — except the sample `GlobalAdmin` — plus the login leg 1.5b forced-enrollment exemption that keeps the sample admin sign-in-able

Status: Accepted
Date: 2026-10-27
Amends: 0138 (its `IsChangePasswordLockedForAsync` four-conjunct decision, its
`LocaleSettings.SamplePasswordChangeLocked` additive-field-on-the-singleton
shape, and its "surface-replaced-by-notice" posture all become the *template*
this ADR re-applies to the MFA surface — ADR 0138 is the source of the rule;
this ADR does not restate it). Amends: 0056 (the `SampleData__Enabled`
config-flag existence gate — the same "unreachable by construction" posture
the MFA lock is gated on). Amends: the F3/U4 login leg 1.5b (the
"GlobalAdmin-with-MFA-off is redirected to /settings/security/mfa to enroll"
enforcement, ADR 0174 / the `AccountController.Login` seam) — the sample
`GlobalAdmin` is **exempt** from that redirect on a `SampleData__Enabled`
instance (the `IsSampleDataAccountAsync` seam, conjuncts 1+3 only, so a
locked instance still lets the sample admin sign in with the shared
password; a real, non-sample `GlobalAdmin` is still forced to enroll).

A deployed demo site (`SampleData__Enabled=true`, ADR 0056) is a **public**
instance whose sample accounts are *shared* credentials: everyone who signs
in as "Anna" signs in as the *same* account. ADR 0138 closed one seam on that
surface: a visitor testing the demo could change a sample account's
password and break the shared credential for the everyone else. This ADR
closes the *other* shared-credential seam on the same surface: a visitor
could enable MFA on a sample account (minting a TOTP secret + recovery codes
that the *visitor* keeps) and break the shared credential for the everyone
else — the credential no longer works with just the password, and the TOTP
secret lives on the visitor's device, not on the instance. ADR 0194
re-applies the ADR 0138 shape to the MFA surface (the
`SecuritySettingsController` MFA enrollment / recovery-rotation / disable
legs) so the two locks are a coherent pair: one admin page
(`/admin/sample`), two independent toggles (password + MFA), the same
four-conjunct decision for both (the `false` floor on the admin gate, the
closed `SampleAccountEmails` set, the sample-`GlobalAdmin` exemption), and
the same "surface-replaced-by-notice" posture.

The one thing the whole lane respects:

- **The lock is a pure read decision, and the write guard is the single place
  the rule bites** (the ADR 0138 / ADR 0050 gate posture, re-applied to the
  MFA surface): the `IsMfaLockedForAsync(subjectId)` read combines the four
  conjuncts in **one place**, so the resident `GET /settings/security/mfa`
  notice and the `MfaEnable` / `MfaRotateRecovery` / `MfaDisable`
  enforcement guards **agree by construction**; the guards are authoritative
  on the write path (a crafted POST is refused before any MFA write — the
  ADR 0138 ChangePassword guard shape, the defense-in-depth over the GET
  notice). `IActionResult` / HTTP never cross the seam (ADR 0006-D).
- **The closed set is reused, not re-derived**: the "is this a sample
  account?" conjunct reads the *same* `SampleDataSeeder.SampleAccountEmails`
  the ADR 0078 / ADR 0129 / ADR 0138 lanes compare against — one source of
  truth, so the MFA lock, the password lock, and the
  notification-suppression gate cannot drift (the ADR 0129 embedded document
  is the single origin of all three).
- **The sample `GlobalAdmin` exemption is preserved** (the ADR 0138 shape,
  re-applied): the sample admin keeps its own MFA lane, exactly as it keeps
  its own password lane. But the *login* leg 1.5b enforcement is a separate
  concern from the MFA surface lock, and the two would **collide** without
  the `IsSampleDataAccountAsync` exemption (a locked instance would redirect
  the sample admin to `/settings/security/mfa` on every sign-in — but that
  page is itself locked for the sample accounts, so the admin could never
  sign in with the shared password). The exemption is a *two-conjunct*
  read (conjuncts 1+3 only — the admin-toggle gate is NOT consulted, so a
  locked instance still lets its admin through; the `GlobalAdmin` check is
  NOT applied either, since the exemption the login path needs is
  *precisely for* the sample `GlobalAdmin` — a non-admin sample account
  never reaches leg 1.5b, since that leg is `GlobalAdmin`-only).

## Context

ADR 0138 landed the password lock. The MFA surface (`SecuritySettingsController`,
ADR 0174 / ADR 0174 D1 errata) was already shipped (the per-resident
enrollment + the `GlobalAdmin` login-enforcement leg 1.5b) when the user
reported the MFA-on-shared-credential seam. The two seams are
structurally identical (a shared credential the demo shouldn't let a
visitor break), so the *natural* shape is to re-apply the ADR 0138 decision
to the MFA surface rather than invent a new one — one admin page, two
toggles, the same four-conjunct decision, the same notice shape, the same
audit-row shape. The *only* new seam the MFA surface needs (that the
password surface did not) is the login leg 1.5b exemption — because a
locked instance would otherwise redirect the sample admin to a page that
is itself locked.

Two facts about the current surface shape the decision:

- **The MFA surface is `GlobalAdmin`-gated at login** (leg 1.5b) but
  *optional* for non-admins (leg 1.5c) — so a non-admin sample account
  would never be redirected to enrollment by leg 1.5b (the exemption the
  login path needs is *precisely for* the sample `GlobalAdmin`), and a
  non-admin sample account *can* enroll MFA from the
  `/settings/security/mfa` surface (the seam this ADR closes). The two
  surfaces (the login redirect, the MFA enrollment page) are
  structurally different concerns that need different exemptions (the
  `IsMfaLockedForAsync` decision for the surface, the
  `IsSampleDataAccountAsync` decision for the login redirect).
- **The `GlobalAdmin`-enforcement leg 1.5b is the *only* place a sample
  admin would be stuck** (a non-admin sample account is not redirected by
  leg 1.5b, and a non-sample `GlobalAdmin` is not a sample account). So
  the exemption the login path needs is a *two-conjunct* read (1+3), not
  the full four-conjunct decision (1+2+3+4) — the admin-toggle gate is
  *not* a conjunct of the login exemption (a locked instance still lets
  its admin through), and the `GlobalAdmin` check is *not* a conjunct of
  the login exemption either (the exemption is *for* the `GlobalAdmin`).

## Decision

### The four-conjunct decision (the single place the MFA-lock rule lives)

`IIdentityService.IsMfaLockedForAsync(string subjectId)` returns `true`
(locked) **iff** all four hold:

1. `IsSampleDataEnabledAsync()` — the instance carries the
   `SampleData__Enabled` flag (a pure config read; a real deployment never
   carries it, so the lock is *unreachable by construction*, ADR 0056);
2. `IsSampleMfaLockedAsync()` — the admin opted in (the
   `LocaleSettings.SampleMfaLocked` gate, the `false` floor);
3. the subject is a **sample account** — its `Profile.Email` is a member of
   the closed `SampleAccountEmails` set (the ADR 0078 / ADR 0129 single
   source of truth); and
4. the subject is **not** a `GlobalAdmin` (the sample admin is exempt).

Conjuncts 3 and 4 short-circuit, so **every real (non-sample) account and
the sample admin are never locked** — only a non-admin sample account on an
opted-in demo instance is. This is a *deliberate inverse* of the
codebase `true`-floor convention (the `LocaleSettings.MessagingEnabled`
shape): the default is `false` (locked-out is **off**), so a fresh or real
instance never blocks MFA. The conjuncts (1)+(3)+(4) are shared with the
ADR 0138 password lock via the private `IsSampleAccountAsync` /
`IsSampleNonAdminAsync` helpers (the two locks cannot drift); only the
admin-toggle gate (conjunct 2) is lane-specific (`SampleMfaLocked` for
this lane, `SamplePasswordChangeLocked` for ADR 0138).

### The login-exemption decision (the two-conjunct read)

`IIdentityService.IsSampleDataAccountAsync(string subjectId)` returns
`true` **iff** both hold:

1. `IsSampleDataEnabledAsync()` — the instance carries the
   `SampleData__Enabled` flag; AND
2. the subject is a **sample account** — its `Profile.Email` is a member
   of the closed `SampleAccountEmails` set.

The admin-toggle gate (`IsSampleMfaLockedAsync`) is **not** a conjunct
(a locked instance still lets its admin sign in — that is the point of
the exemption), and the `GlobalAdmin` check is **not** a conjunct either
(the exemption the login path needs is *precisely for* the sample
`GlobalAdmin` — a non-admin sample account never reaches leg 1.5b).
Returns `false` for every real account and on every non-sample-data
instance (the ADR 0056 "unreachable by construction" shape).

### The admin option (the same page, a second toggle)

The `GlobalAdmin`-gated `/admin/sample` surface (ADR 0138) gains a second
toggle, the MFA lock (`SampleAdminViewModel.MfaLocked`, the
`IsSampleMfaLockedAsync` / `SetSampleMfaLockedAsync` seams, the audit row
action `sample.set-mfa-lock`). The two toggles are **independent** (one
can be on while the other is off) — each its own form on the page (the
`/admin/signup` / `/admin/timezone` / `/admin/quiet` single-select shape,
one submit button per toggle), so the two locks are toggled independently.
The `Save(bool passwordChangeLocked, bool mfaLocked = false)` signature
keeps the ADR 0138 single-arg form shape working (a pre-0194 form that
posts only `passwordChangeLocked` still works, leaving the MFA gate at
its `false` floor).

### The surface (the MFA enrollment page, the ADR 0138 notice shape)

The `SecuritySettingsController` MFA actions (the `Mfa` GET, the
`MfaEnable` / `MfaRotateRecovery` / `MfaDisable` POSTs, and the
`MfaRecovery` GET) are each guarded by `IsMfaLockedForAsync`: a locked,
non-admin sample account gets the static `MfaLocked` notice (the
`Views/SecuritySettings/MfaLocked.cshtml`, the
`ChangePasswordLocked.cshtml` precedent re-applied to the MFA surface)
instead of the enrollment surface, and the write legs are refused before
any MFA write (the `UnauthorizedAccessException` analog — a redirect back
to the `Mfa` view, the defense-in-depth over the GET notice). The
`Sessions` surface (the kill-all / sign-out-everywhere action) is **not**
MFA-specific — it is a session-management lane that works for any
account, locked or not — so it is left as-is (a locked sample account
can still sign out everywhere; that is a privacy action, not a
credential-breaking one).

### The login leg 1.5b (the sample-admin exemption)

The `AccountController.Login` leg 1.5b (`GlobalAdmin`-with-MFA-off is
redirected to `/settings/security/mfa` to enroll) gains a fifth conjunct:
the subject is **not** a sample account (the
`IsSampleDataAccountAsync` read). A real, non-sample `GlobalAdmin` is
still forced to enroll (the F3/U4 enforcement, unchanged); the sample
`GlobalAdmin` is exempt (so a locked instance still lets the demo admin
sign in with the shared password). A non-admin sample account is never
affected by this leg (leg 1.5b is `GlobalAdmin`-only).

## Consequences

- **Two locks, one admin page, two independent toggles**: the
  `/admin/sample` surface now carries the ADR 0138 password lock and the
  ADR 0194 MFA lock, each a single-select + a submit button (the ADR 0050
  singleton-toggle shape, re-applied to the MFA surface). The two toggles
  are independent (one can be on while the other is off) — a demo admin
  can lock the password while leaving MFA open (or vice versa), or lock
  both.
- **The sample admin is exempt from both locks** (the ADR 0138 shape,
  re-applied): the sample `GlobalAdmin` keeps its own password lane and
  its own MFA lane. But the sample admin is also **exempt from the login
  leg 1.5b forced-enrollment redirect** (the `IsSampleDataAccountAsync`
  seam) so a locked instance still lets the demo admin sign in with the
  shared password — without this exemption, the admin would be stuck in
  a redirect loop (leg 1.5b → `/settings/security/mfa` → locked notice →
  no way to complete enrollment).
- **Real (non-sample) accounts are never affected** (the ADR 0056
  "unreachable by construction" shape, re-applied): a real `GlobalAdmin`
  is still forced to enroll MFA at login (leg 1.5b, unchanged); a real
  non-admin can still enroll MFA from the `/settings/security/mfa`
  surface (unchanged). The MFA lock is a *demo-only* guard — a real
  deployment never carries the `SampleData__Enabled` flag, so the lock
  is unreachable there by construction.
- **The closed set is reused** (the ADR 0078 / ADR 0129 single source of
  truth, re-applied): the MFA lock and the password lock and the
  notification-suppression gate all compare against the same
  `SampleDataSeeder.SampleAccountEmails` set — one origin, three gates,
  no drift.
- **Zero migrations**: the `LocaleSettings.SampleMfaLocked` additive
  field is delta-detected (the ADR 0004 §B.1 shape, the ADR 0138
  `SamplePasswordChangeLocked` precedent — the same Marten delta
  detection, no new migration).
- **`Milestones.cs` / README Roadmap / `MilestonesTests.cs` untouched**:
  ADR 0194 is a *named lane* on the ADR 0138 sample-data surface (the
  ADR 0138 "named lane on the shipped sample-data surface, not a
  renumber or a milestone" posture, re-applied). The close flip is the
  ADR 0194 file + the ADR README index row + the WhatsNew 0.74.0 entry +
  the README "Locking sample-account …" section (already extended to
  mention MFA).
- **The `IIdentityService` frozen surface gains four additive methods**
  (`IsSampleMfaLockedAsync` / `SetSampleMfaLockedAsync` /
  `IsMfaLockedForAsync` / `IsSampleDataAccountAsync`) — all additive,
  so no existing caller's contract changes (the ADR 0138
  `IsSamplePasswordChangeLockedAsync` / `SetSamplePasswordChangeLockedAsync`
  / `IsChangePasswordLockedForAsync` shape, re-applied). The private
  `IsSampleAccountAsync` / `IsSampleNonAdminAsync` helpers are the shared
  conjuncts (1)+(3)+(4) that both the ADR 0138 password lock and the ADR
  0194 MFA lock read, so the two locks cannot drift.

## Tests

The ADR 0138 test pin (the `AccessAudit` shape + the live-on-next-read
invariant + the four-conjunct decision) is re-applied to the MFA surface
in `tests/Kumunita.Core.Tests/IdentityServiceSampleMfaLockTests.cs`
(15 tests: the `false` floor, the audit-row shape for the MFA lock, the
re-unlock, the two-locks-are-independent, the four-conjunct decision
for the MFA lock, the two-conjunct login-exemption decision, and the
login-exemption-is-independent-of-the-admin-toggle-gate). The ADR 0138
test pin for the admin surface (the `AdminSampleDataController` thin
seam) is re-applied to the MFA toggle in
`tests/Kumunita.Web.Tests/AdminSampleDataControllerTests.cs` (6 new tests:
the Index `MfaLocked` seed, the Index `MfaLocked` floor, the Save MFA-lock
call, the Save MFA-unlock call, the Save MFA-lock 404 on a real instance,
and the Save pre-0194 single-arg form shape). The ADR 0138 test pin for
the resident surface (the `ChangePasswordLocked` notice shape) is
re-applied to the MFA surface in
`tests/Kumunita.Web.Tests/SecuritySettingsMfaLockTests.cs` (5 tests: the
Mfa GET renders the `MfaLocked` notice when locked, the Mfa GET renders
the enrollment surface when unlocked, the MfaEnable / MfaRotateRecovery /
MfaDisable POSTs are refused before any MFA write). The ADR 0138 test pin
for the login leg (the `F3U5_MfaLoginTests` harness) is extended with the
`IsSampleDataAccountAsync` exemption (2 new tests: the
`SampleAdminLogin_SkipsForcedMfaEnrollment` exemption fires for a sample
account, the `NonSampleGlobalAdminLogin_StillForcedToMfaEnrollment`
control does not fire for a non-sample account).

## References

- ADR 0138 (the template this ADR re-applies) — the sample-data
  change-password lock: the four-conjunct decision, the
  `LocaleSettings.SamplePasswordChangeLocked` additive field, the
  "surface-replaced-by-notice" posture, the `GlobalAdmin` exemption.
- ADR 0056 (the `SampleData__Enabled` existence gate) — the
  "unreachable by construction" posture the MFA lock is gated on.
- ADR 0078 / ADR 0129 (the closed `SampleAccountEmails` set) — the
  single source of truth the MFA lock, the password lock, and the
  notification-suppression gate all compare against.
- ADR 0174 (the MFA enrollment surface + the pairing QR) — the
  `SecuritySettingsController` MFA actions the MFA lock guards.
- ADR 0004 §B.1 (the additive-field delta-detection) — the
  `LocaleSettings.SampleMfaLocked` field is delta-detected, no migration.
- ADR 0006 §A (the `IIdentityService` frozen surface) — the four
  additive methods this ADR gains.
- The F3/U4 login leg 1.5b (the `AccountController.Login` seam) — the
  `GlobalAdmin`-with-MFA-off enforcement the `IsSampleDataAccountAsync`
  exemption exempts the sample admin from.
