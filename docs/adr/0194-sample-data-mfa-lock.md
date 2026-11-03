---
id: adr-0194
title: "Sample-data MFA (two-factor) lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of the MFA settings surface — except the sample `GlobalAdmin` — plus the login leg 1.5b forced-enrollment exemption that keeps the sample admin sign-in-able"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: []
supersedes: [adr-0138]
superseded_by: []
updated: 2026-11-02
---

# ADR [0194](0194-sample-data-mfa-lock.md) — Sample-data MFA (two-factor) lock: a `GlobalAdmin` option (shown only on a `SampleData__Enabled` instance) that locks the demo (sample) accounts out of the MFA settings surface — except the sample `GlobalAdmin` — plus the login leg 1.5b forced-enrollment exemption that keeps the sample admin sign-in-able

Status: Accepted | Date: 2026-10-27

> **Abstract:** a deployed demo site (`SampleData__Enabled`, ADR [0056](0056-sample-data-opt-in-and-deploy-posture.md)) has
> *shared* credentials — everyone who signs in as "Anna" signs in as the same
> account. ADR [0138](0138-sample-data-change-password-lock.md) locked the sample accounts out of the *password-change*
> surface; this ADR closes the *other* shared-credential seam on the same
> surface: locking them out of the **MFA** enrollment/rotation surface (minting
> a TOTP secret + recovery codes a *visitor* would keep, breaking the shared
> credential) — **except the sample `GlobalAdmin`** — plus the **login leg
> 1.5b** forced-enrollment exemption that keeps the sample admin sign-in-able.
> It re-applies the ADR [0138](0138-sample-data-change-password-lock.md) shape (four-conjunct read + write guard + the
> closed `SampleAccountEmails` set + the sample-admin exemption) to the MFA
> surface, so the two locks are a coherent pair: one admin page
> (`/admin/sample`), two independent toggles, the same "surface-replaced-by-
> notice" posture. **For** a developer touching `IIdentityService` MFA seams,
> the `/admin/sample` surface, or the `SecuritySettingsController` MFA guards.
> **Read the rest** before changing the conjuncts, the exemption, or the login
> leg. Amends + tests + references in the [handoff
> note](../design/adr-handoffs.md#adr-0194--sample-data-mfa-lock-moved-detail).

## Context

A deployed demo site (`SampleData__Enabled=true`, ADR [0056](0056-sample-data-opt-in-and-deploy-posture.md)) is a **public**
instance whose sample accounts are *shared* credentials: everyone who signs in
as "Anna" signs in as the *same* account. ADR [0138](0138-sample-data-change-password-lock.md) closed one seam on that
surface: a visitor testing the demo could change a sample account's password
and break the shared credential for everyone else. This ADR closes the *other*
shared-credential seam on the same surface: a visitor could enable MFA on a
sample account (minting a TOTP secret + recovery codes that the *visitor*
keeps) and break the shared credential for everyone else — the credential no
longer works with just the password, and the TOTP secret lives on the visitor's
device, not on the instance. ADR [0194](0194-sample-data-mfa-lock.md) re-applies the ADR [0138](0138-sample-data-change-password-lock.md) shape to the MFA
surface (the `SecuritySettingsController` MFA enrollment / recovery-rotation /
disable legs) so the two locks are a coherent pair: one admin page
(`/admin/sample`), two independent toggles (password + MFA), the same
four-conjunct decision for both (the `false` floor on the admin gate, the
closed `SampleAccountEmails` set, the sample-`GlobalAdmin` exemption), and the
same "surface-replaced-by-notice" posture.

The MFA surface (`SecuritySettingsController`, ADR [0174](0174-mfa-qr-code-and-security-tab.md) / ADR [0174](0174-mfa-qr-code-and-security-tab.md) D1 errata)
was already shipped (the per-resident enrollment + the `GlobalAdmin`
login-enforcement leg 1.5b) when the user reported the MFA-on-shared-credential
seam. The two seams are structurally identical (a shared credential the demo
shouldn't let a visitor break), so the *natural* shape is to re-apply the ADR
0138 decision to the MFA surface rather than invent a new one — one admin page,
two toggles, the same four-conjunct decision, the same notice shape, the same
audit-row shape. The *only* new seam the MFA surface needs (that the password
surface did not) is the login leg 1.5b exemption — because a locked instance
would otherwise redirect the sample admin to a page that is itself locked.

## Decision

- **The four-conjunct decision (the single place the MFA-lock rule lives).**
  `IIdentityService.IsMfaLockedForAsync(subjectId)` returns `true` (locked)
  **iff** all four hold: (1) `IsSampleDataEnabledAsync()` — the instance
  carries the `SampleData__Enabled` flag (a pure config read; a real deployment
  never carries it, so the lock is *unreachable by construction*, ADR [0056](0056-sample-data-opt-in-and-deploy-posture.md));
  (2) `IsSampleMfaLockedAsync()` — the admin opted in (the
  `LocaleSettings.SampleMfaLocked` gate, the `false` floor); (3) the subject is
  a **sample account** — its `Profile.Email` is a member of the closed
  `SampleAccountEmails` set (the ADR [0078](0078-sample-account-notification-suppression-in-production.md) / ADR [0129](0129-sample-data-embedded-json-document.md) single source of truth);
  and (4) the subject is **not** a `GlobalAdmin` (the sample admin is exempt).
  Conjuncts 3 and 4 short-circuit, so **every real (non-sample) account and
  the sample admin are never locked** — only a non-admin sample account on an
  opted-in demo instance is. This is a *deliberate inverse* of the codebase
  `true`-floor convention: the default is `false` (locked-out is **off**), so
  a fresh or real instance never blocks MFA. The conjuncts (1)+(3)+(4) are
  shared with the ADR [0138](0138-sample-data-change-password-lock.md) password lock via the private
  `IsSampleAccountAsync` / `IsSampleNonAdminAsync` helpers (the two locks
  cannot drift); only the admin-toggle gate (conjunct 2) is lane-specific.
- **The login-exemption decision (the two-conjunct read).**
  `IIdentityService.IsSampleDataAccountAsync(subjectId)` returns `true` **iff**
  both hold: (1) `IsSampleDataEnabledAsync()`; AND (2) the subject is a
  **sample account** (`Profile.Email` in the closed `SampleAccountEmails`
  set). The admin-toggle gate is **not** a conjunct (a locked instance still
  lets its admin sign in — that is the point of the exemption), and the
  `GlobalAdmin` check is **not** a conjunct either (the exemption the login
  path needs is *precisely for* the sample `GlobalAdmin` — a non-admin sample
  account never reaches leg 1.5b). Returns `false` for every real account and
  on every non-sample-data instance (the ADR [0056](0056-sample-data-opt-in-and-deploy-posture.md) "unreachable by
  construction" shape).
- **The admin option (the same page, a second toggle).** The
  `GlobalAdmin`-gated `/admin/sample` surface (ADR [0138](0138-sample-data-change-password-lock.md)) gains a second
  toggle, the MFA lock (`SampleAdminViewModel.MfaLocked`, the
  `IsSampleMfaLockedAsync` / `SetSampleMfaLockedAsync` seams, the audit-row
  action `sample.set-mfa-lock`). The two toggles are **independent** (one can
  be on while the other is off) — each its own form on the page (one submit
  button per toggle). The `Save(bool passwordChangeLocked, bool mfaLocked =
  false)` signature keeps the ADR 0138 single-arg form shape working (a
  pre-0194 form that posts only `passwordChangeLocked` still works, leaving
  the MFA gate at its `false` floor).
- **The surface (the MFA enrollment page, the ADR [0138](0138-sample-data-change-password-lock.md) notice shape).** The
  `SecuritySettingsController` MFA actions (the `Mfa` GET, the `MfaEnable` /
  `MfaRotateRecovery` / `MfaDisable` POSTs, and the `MfaRecovery` GET) are
  each guarded by `IsMfaLockedForAsync`: a locked, non-admin sample account
  gets the static `MfaLocked` notice (the
  `Views/SecuritySettings/MfaLocked.cshtml`, the `ChangePasswordLocked.cshtml`
  precedent re-applied to the MFA surface) instead of the enrollment surface,
  and the write legs are refused before any MFA write (a redirect back to the
  `Mfa` view — the defense-in-depth over the GET notice). The `Sessions`
  surface (kill-all / sign-out-everywhere) is **not** MFA-specific — it is a
  session-management lane that works for any account, locked or not — so it is
  left as-is (a locked sample account can still sign out everywhere; that is a
  privacy action, not a credential-breaking one).
- **The login leg 1.5b (the sample-admin exemption).** The
  `AccountController.Login` leg 1.5b (`GlobalAdmin`-with-MFA-off is redirected
  to `/settings/security/mfa` to enroll) gains a fifth conjunct: the subject
  is **not** a sample account (the `IsSampleDataAccountAsync` read). A real,
  non-sample `GlobalAdmin` is still forced to enroll (the F3/U4 enforcement,
  unchanged); the sample `GlobalAdmin` is exempt (so a locked instance still
  lets the demo admin sign in with the shared password). A non-admin sample
  account is never affected by this leg (leg 1.5b is `GlobalAdmin`-only).

## Consequences

- **Two locks, one admin page, two independent toggles**: the `/admin/sample`
  surface now carries the ADR [0138](0138-sample-data-change-password-lock.md) password lock and the ADR [0194](0194-sample-data-mfa-lock.md) MFA lock,
  each a single-select + a submit button (the ADR [0050](0050-admin-managed-signup-gate-open-or-invitation-only.md) singleton-toggle
  shape, re-applied to the MFA surface). The two toggles are independent (one
  can be on while the other is off) — a demo admin can lock the password while
  leaving MFA open (or vice versa), or lock both.
- **The sample admin is exempt from both locks** (the ADR [0138](0138-sample-data-change-password-lock.md) shape,
  re-applied): the sample `GlobalAdmin` keeps its own password lane and its
  own MFA lane. But the sample admin is also **exempt from the login leg 1.5b
  forced-enrollment redirect** (the `IsSampleDataAccountAsync` seam) so a
  locked instance still lets the demo admin sign in with the shared password —
  without this exemption, the admin would be stuck in a redirect loop (leg
  1.5b → `/settings/security/mfa` → locked notice → no way to complete
  enrollment).
- **Real (non-sample) accounts are never affected** (the ADR [0056](0056-sample-data-opt-in-and-deploy-posture.md) "unreachable
  by construction" shape, re-applied): a real `GlobalAdmin` is still forced to
  enroll MFA at login (leg 1.5b, unchanged); a real non-admin can still enroll
  MFA from the `/settings/security/mfa` surface (unchanged). The MFA lock is a
  *demo-only* guard — a real deployment never carries the `SampleData__Enabled`
  flag, so the lock is unreachable there by construction.
- **The closed set is reused** (the ADR [0078](0078-sample-account-notification-suppression-in-production.md) / ADR [0129](0129-sample-data-embedded-json-document.md) single source of
  truth, re-applied): the MFA lock and the password lock and the
  notification-suppression gate all compare against the same
  `SampleDataSeeder.SampleAccountEmails` set — one origin, three gates, no
  drift.
- **Zero migrations**: the `LocaleSettings.SampleMfaLocked` additive field is
  delta-detected (the ADR [0004](0004-data-persistence-and-schema-evolution.md) §B.1 shape, the ADR [0138](0138-sample-data-change-password-lock.md)
  `SamplePasswordChangeLocked` precedent — the same Marten delta detection, no
  new migration).
- **`Milestones.cs` / README Roadmap / `MilestonesTests.cs` untouched**: ADR
  0194 is a *named lane* on the ADR [0138](0138-sample-data-change-password-lock.md) sample-data surface (the ADR [0138](0138-sample-data-change-password-lock.md)
  "named lane on the shipped sample-data surface, not a renumber or a
  milestone" posture, re-applied). The close flip is the ADR [0194](0194-sample-data-mfa-lock.md) file + the
  ADR README index row + the WhatsNew 0.74.0 entry + the README "Locking
  sample-account …" section (already extended to mention MFA).
- **The `IIdentityService` frozen surface gains four additive methods**
  (`IsSampleMfaLockedAsync` / `SetSampleMfaLockedAsync` /
  `IsMfaLockedForAsync` / `IsSampleDataAccountAsync`) — all additive, so no
  existing caller's contract changes (the ADR [0138](0138-sample-data-change-password-lock.md)
  `IsSamplePasswordChangeLockedAsync` / `SetSamplePasswordChangeLockedAsync` /
  `IsChangePasswordLockedForAsync` shape, re-applied). The private
  `IsSampleAccountAsync` / `IsSampleNonAdminAsync` helpers are the shared
  conjuncts (1)+(3)+(4) that both the ADR [0138](0138-sample-data-change-password-lock.md) password lock and the ADR [0194](0194-sample-data-mfa-lock.md)
  MFA lock read, so the two locks cannot drift.
