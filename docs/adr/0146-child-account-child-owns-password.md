# ADR 0146 — Child accounts: the child owns the password (the guardian never sets it; the child sets it at the confirmation link)

Status: Accepted
Date: 2026-10-06
Builds on: 0028 (the GU standing — the `GuardianLink` document, the
`AccessVia.Guardian` value, and — critically — the design doc's own stated
intent that "the parent does not need the child's credentials to supervise;
supervision rides the link, not the password" and "a parent does not re-key a
child's password"); 0001 (the M1 identity handoff — `RegisterAsync` +
`VerifyWithTokenAsync`, the single designed email seam this lane extends);
0006 (the §A frozen `IIdentityService` surface gains two *additive* methods;
the §E compatible-ADD precedent); 0138 (the `ChangePasswordAsync` credential
write + the `IsChangePasswordLockedForAsync` guard this lane's credential
write reuses the shape of)

## Context

ADR 0028 froze the GU surface and its design doc stated the intended
credential posture explicitly: the guardian supervises through the
`GuardianLink`, *not* through the child's password, and the design's
"world seams" section records "no 're-key the password into a phone' step"
as a deliberate non-seam. **But the implementation drifted from that intent:**
the GU formation lane (`GuardianController.AddChild` →
`IIdentityService.RegisterAsync`) took a **`Password` field on the
`AddChildForm`** and set the child's credential on the guardian's behalf. The
guardian's consent block even said "you maintain full control over their
account" — and, in practice, the guardian *did* hold the key.

This left two problems the documented intent did not want:

1. **The guardian held a credential the product said it should not.** A
   parent setting a child's password is exactly the "re-key the password
   into a phone" step the design doc listed as the seam it was *avoiding*.
   A child whose parent chose their password has no independent credential —
   the account's "ownership" of that password is the parent's, which inverts
   the very "eventually-independent" posture the GU lane is built toward
   (ADR 0028 §D·5, the come-of-age transition).

2. **The child never got to choose.** The account was created, the
   verification email sent, the child clicked the link, and signed in to an
   account whose password *someone else picked*. The M1 handoff ("you
   clicked the link, we sign you in") never asked the child for the one
   thing that is genuinely theirs.

This ADR corrects the implementation to match the documented intent:
**the guardian creates the child's account without a password, and the
child sets their own password when they click the confirmation link.** The
guardian still supervises everything else (suspend, memberships,
invitations, the event gate, deletion) — that standing is unchanged and
still rides the `GuardianLink`. Only the credential ownership moves: from
the guardian to the child.

## Decision

### A. The formation seam: `IIdentityService.RegisterChildAccountAsync`

One **additive** method on the frozen `IIdentityService` surface (ADR 0006
§A/§E, the `DeleteChildAccountAsync` / `RegisterAsync` ADD shape):

```csharp
Task<ThinPrincipal> RegisterChildAccountAsync(string displayName, string email);
```

- **No password parameter.** The implementation is a thin delegate to the
  same body as `RegisterAsync` (refactored into a private
  `RegisterCoreAsync(displayName, email, password, requiresPassword)`);
  `password: null` means the account is created **without a credential**
  (the `UserManager.AddPasswordAsync` call is skipped). The account, the
  `Profile` (unverified, self-only visibility), the single-use
  `KindVerify` token, and the ADR 0077 `account.signup` GlobalAdmin
  notification are all staged exactly as the self-serve lane stages them —
  **nothing about the formation is different except the absence of the
  credential.** The two lanes are one code path with one optional argument
  (no duplication, no drift).
- **The verification email names the missing step.** The child lane's
  email body is `email.verify_child_body` (a new registry key, four
  languages): "…confirm the account **and set your password**…". The
  self-serve lane keeps `email.verify_body` unchanged ("…confirm the
  account (it also signs you in)…"). The builder
  (`BuildVerificationEmailAsync`) threads a `requiresPassword` flag to
  select the body — a missing `email.verify_child_body` key in a provider
  falls back to the standard body rather than the raw key, so the child
  lane degrades gracefully on a provider that lacks the key.

### B. The activation seam: `IIdentityService.VerifyAndSetPasswordAsync`

A second **additive** method:

```csharp
Task<Profile> VerifyAndSetPasswordAsync(string tokenValue, string password);
```

- **The child sets their own credential at the handoff.** Same single-use
  `KindVerify` token the child lane staged (the token is lane-agnostic —
  it carries no "was a password set" flag; the lane is decided by whether
  the account *has* a password, not by the token). The implementation
  sets the credential (`UserManager.AddPasswordAsync`), flips
  `Profile.Verified`, consumes the token, and appends the `via: Owner`
  `verify` audit row — **one commit** (the
  `CompleteSeedAdminSetupAsync` shape: a credential write that also flips
  `Verified`). The credential write result is checked and thrown on
  failure (a password that fails the `UserManager` policy is a user
  error the Web surfaces, not a 500). The ADR 0077 `account.verified`
  notification is emitted as the other verify lanes emit it.
- **The credential is the child's.** `via: Owner` (the resident
  activating their own account — the `VerifyWithTokenAsync` /
  `ChangePasswordAsync` self-lane shape), not `via: Guardian`. The
  guardian did not act; the child did.

### C. The Web handoff: the confirm link becomes two-shaped

`AccountController.Verify` is split into a **GET** (decide the shape) and a
**POST** (activate). The decision is a single read — does the account have
a password? (`UserManager.HasPasswordAsync`, read off the token's
`UserId`):

- **Yes (self-serve lane):** the credential exists — confirm and sign in
  exactly as M1 did. The `VerifyWithTokenAsync` seam, the same
  sign-in-mint, the same redirect. **No behavior change** for the
  resident who signed up themselves.
- **No (child lane):** render a **set-password form** on the confirmation
  page (`account.verify_set_password_lede` /
  `account.verify_set_password_submit`), the child types their own
  password (the same `MinLength(8)` + `Compare` validation the signup form
  carries), and the POST calls `VerifyAndSetPasswordAsync`. The form's
  hidden `Id` round-trips the token row id back to the POST (the form
  posts to the same link the email sent).

The `VerifyViewModel` gains `RequiresPassword` / `Password` /
`ConfirmPassword` (the `ChangePasswordViewModel` one-model-serves-two-shapes
precedent — a single model, a branch flag, and the credential fields that
are only bound in the child lane). The self-serve lane's GET path renders
no form at all — it confirms and redirects, as before.

The lane is decided by **the account's state, not a flag**: a child who
re-requests the verification link (the `ResendVerificationEmailAsync`
lane) still has no password and still gets the set-password surface; a
resident who exhausted their attempts and was manually verified has a
password and gets the confirm-only surface. The
`ResendVerificationEmailAsync` body selection reads the same
`PasswordHash` state, so the two agree.

### D. The GU form loses its `Password` field

`AddChildForm` is now **two** form-bound fields (`DisplayName`, `Email`)
+ the `GuardianConsent` checkbox — the `Password` field and its
`[MinLength(8)]` / `[DataType(Password)]` attributes are removed.
`GuardianController.AddChild` calls
`RegisterChildAccountAsync(displayName, email)` (not
`RegisterAsync(..., form.Password!)`). Both the inline-on-`Index` form and
the `AddChild` failure-landing form drop the password input, and the
`guardian.child_email_hint` copy now says the child sets their own
password at the confirmation link — "you don't need (or set) their
password."

The **guardian consent block is unchanged** (the guardian still confirms
they are the legal guardian and accepts the child-account terms — that is
about *supervision*, which is unchanged; only the credential ownership
moved).

### E. What is deliberately NOT changed

- **The self-serve signup lane is untouched** — `AccountController.Signup`
  still collects a password, still calls `RegisterAsync`, and the
  confirm-only verify link still works exactly as M1 shipped. A resident
  who signed up themselves does not suddenly have to re-set a password.
- **The `ChangePasswordAsync` lane and its ADR 0138 lock are untouched** —
  the child, once activated, changes their own password through the same
  self-serve lane as anyone else (and is subject to the same sample-data
  lock). This ADR adds a way to *set* the password at creation; it does
  not re-route the *change* lane.
- **The GU standing and every other supervisory action are untouched** —
  suspend/unsuspend, community + group curation, invitation approval, the
  event gate (ADR 0144), deletion (ADR 0143) all still ride the
  `GuardianLink`. The lane's whole honesty (G·1 — the guardian never reads
  the child's content) is unchanged; this ADR only moves the *credential*,
  which the design doc always intended to be the child's.
- **No new `AccessVia` value, no new `GuardianLink` state, no schema
  change.** The two new seams are additive on `IIdentityService`; the
  token, the profile, the audit rows are the existing documents.

## Invariants (pinned)

- **C-0146·1 — The child account is created password-less.**
  `RegisterChildAccountAsync` leaves the account's `PasswordHash` empty;
  a `CheckPasswordAsync` against any candidate fails until the child sets
  it. (Pinned by
  `ChildAccountPasswordTests.RegisterChildAccount_CreatesAnUnverifiedAccountWithNoPassword`.)
- **C-0146·2 — The child's credential is set by the child, at the link.**
  `VerifyAndSetPasswordAsync` sets the password the child supplied, flips
  `Verified`, and consumes the token in one commit; the child's password
  then signs them in (a re-fetched `CheckPasswordAsync` succeeds) and the
  token is single-use (a second presentation is refused). (Pinned by
  `ChildAccountPasswordTests.VerifyAndSetPassword_SetsTheChildsOwnCredential_AndSignsIn`.)
- **C-0146·3 — The self-serve lane is unchanged.** A resident who signed
  up with a password gets the confirm-only verify link; the
  `AddChildForm` pin is now three fields (no `Password`). (Pinned by
  `GuardianViewModelsTests.AddChildForm_Is_Form_Model_With_Three_Fields`
  and the unchanged `AccountController.Signup` behavior.)

## Consequences

- A child account is **genuinely the child's** from the first sign-in —
  the credential they chose, which is the precondition for the come-of-age
  transition the GU lane is built toward (a child whose parent picked the
  password was never fully their own to begin with).
- The guardian's job is unchanged and arguably clearer: they supervise
  *through the platform* (the link), and they can still suspend, curate,
  or delete — but they no longer have a *key* to the child's account,
  which is the boundary a privacy-first product should hold.
- One extra click for a parent (the child must act on the link themselves)
  in exchange for a credential that is actually the child's — a trade the
  design doc already argued for; this ADR just makes the code agree.
- A child who loses the link (or whose attempts are exhausted) reaches the
  admin manual-verify valve (OPS §7) like any resident. **Known edge case:**
  that valve flips `Verified` but does not set a credential, so a
  password-less child account that is manually verified is *verified but
  cannot sign in* (and `ChangePasswordAsync`, which requires being signed
  in, is unreachable). For that one case the admin must establish a
  credential directly (the single place an adult sets a child's password —
  a recovery act, not the normal formation path this ADR removes). This is
  deliberately *not* automated: it is rare (a lost link + exhausted
  attempts), and the normal path — the child clicking the link and setting
  their own password — covers the overwhelming case.
