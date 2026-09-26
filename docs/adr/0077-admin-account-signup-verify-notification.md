# ADR 0077 — Admin account notifications: notify every GlobalAdmin when a resident signs up or verifies (a new notify gate + two admin-lane kinds on the frozen M6 emitter)

Status: Accepted
Date: 2026-09-24
Amends: **0076** (the M6 notification surface — D2's *nine-kind* closed set
becomes an **eleven-kind** closed set: the two admin-lane kinds
`account.signup` / `account.verified` join `NotificationKinds`; the
`NotificationService.EmitAsync` seam, the `M6DocTypes` surface, the durable
email trio, and the per-recipient preference lane are **reused verbatim** —
no signature change), **0005 / 0015** (the `LocaleSettings` singleton + the
closed `KnownTranslationKeys` registry — one additive gate field on the
singleton; twelve new keys × four languages), **0004** (§B.1 additive — the
`NotifyAdminsOnSignup` field is a new field on an existing document, delta-
detected, no migration), **0019 / 0020** (the `LocaleSettings` read-modify-
write + single-target audit shape the new gate reuses verbatim), and
**0001** (the M1 `RegisterAsync` / `VerifyWithTokenAsync` handoff — the two
emission points hook in *before* the single `SaveChangesAsync`, same session).

## Context

The M1–M6 surface reaches *residents about residents*: a reply, an RSVP, a
group post, a report, a to-do. But the **admin lane** — the moments that
matter to the people *running* the instance — has no signal at all. When a
new resident signs up (`RegisterAsync`) or verifies their account
(`VerifyWithTokenAsync`), nothing is written anywhere an admin can see it;
the only trace is a single `AccessAudit` row on the verify path
(`verify`, via: Owner) that records the *resident's* self-action, not "a new
account exists." A self-hosted operator has no way to learn that someone
joined their community, short of tailing the logs or the `identity` schema.

M6's frozen `NotificationService.EmitAsync` is the exactly-right primitive to
close that gap: it already stores the durable inbox row, resolves the
recipient's language, resolves the localized subject/body templates, and
conditionally stages the durable email — all on the caller's session with the
C3 single-commit guarantee. What M6 does *not* have is a **kind** for "an
account event" and a **gate** for "the admin wants to be told about account
events." This ADR adds exactly those two things and nothing more.

**What the request is:** "add an admin option: send email/notification when a
user signs up / verifies an account." Read literally: a **GlobalAdmin
toggle** (the instance gate, the ADR 0019/0020/0050 `LocaleSettings` shape)
that, when on, fans out a **notification** (the M6 inbox row + best-effort
email) to **every GlobalAdmin** at **two moments** — signup and verification.

## Decision

**D1 — One additive instance gate, `LocaleSettings.NotifyAdminsOnSignup`.**
A new `bool` field on the `LocaleSettings` singleton (the same document the
timezone / date-format / editor / signup-gate lanes read and write),
**defaulting to `true`** — the M6 "null / empty = all enabled" lean-default
and the ADR 0050 `IsSignupOpen` floor shape: a fresh instance ships with the
admin notification *on* (the operator who self-hosts a community wants to know
when someone joins, until they say otherwise). The read seam
(`IIdentityService.IsNotifyAdminsOnSignupAsync`) carries the **`true` floor**
(a missing singleton or an unset value both yield `true`; only an explicit
`false` silences it), exactly like `IsSignupOpenAsync`. The write seam
(`IIdentityService.SetNotifyAdminsOnSignupAsync`) is a read-modify-write of
the singleton + **exactly one** `AccessAudit` row (via: Admin, action
`"signup.set-notify"`, target `"signup"`), the ADR 0019/0020 single-target
shape — a read, no audit row. *Forbids:* a per-admin toggle (this is an
*instance* setting — the recipient set is "every GlobalAdmin," not a chosen
subset), a new document, a migration, or a `false` floor.

**D2 — The two admin-lane kinds join the closed set (amends 0076 D2).**
`NotificationKinds` gains two `public const string` members appended *last*
in `Known`: `AccountSignup` = `"account.signup"` and `AccountVerified` =
`"account.verified"`. The closed set is now **eleven** kinds (was nine). The
two admin-lane kinds are **not reserved** (unlike `PostMention`) — they ship
wired (D3). Like every other kind they render as a normal enabled toggle in
`/notifications/preferences` (a GlobalAdmin can disable the *email* nudge for
a kind they consider noise and keep the inbox row — the D7 per-kind preference
lane is unchanged). *Forbids:* a resident-authored kind string, an enum-backed
kind, or a third admin-lane kind in this ADR (e.g. "blocked" / "unblocked"
stays a follow-on lane).

**D3 — Two emission points, best-effort, same session, recipient = every
GlobalAdmin.** `RegisterAsync` emits the `account.signup` kind immediately
before its `SaveChangesAsync` (snippet `"{displayName} <{email}>"`);
`VerifyWithTokenAsync` emits the `account.verified` kind immediately before
its `SaveChangesAsync` (snippet `"{profile.DisplayName} <{user.Email}>"`).
Both call the frozen `NotificationService.EmitAsync` on the **caller's open
session** (C3 — the domain write + the inbox row + the email commit
atomically). The private emitter is **best-effort**: it is wrapped in a
`try/catch` that swallows + logs (`LogWarning`) any failure — the account
operation (register / verify) is the *primary* domain op and must succeed even
if the notification lane is unavailable (the M6 "email is best-effort, the
inbox is the durable record" posture, D5 of 0076). The recipient set is
resolved via `userManager.GetUsersInRoleAsync(Roles.GlobalAdmin)` and the
emission is **skipped entirely when that set is empty** (a fresh instance's
single seed-admin account is *not* notified about itself — a no-op, not a
noise path). The idempotency key is **per-recipient**:
`notification:{kind}:{accountId}:{adminId}` — the M6 dedup is by key *alone*
(a shared key would collapse the fan-out to the first admin), and the
recipient id is part of the key because `EmitAsync`'s recipient is a
parameter, not part of the key (the §6.3 shape + the per-admin fan-out).
*Forbids:* a failure of the notification lane failing the signup / verify, a
shared key across admins, and a `ManuallyVerifyAsync` hook point (the admin
*already knows* they just verified someone — emitting a notification to
themselves is noise; the two automatic moments are the ones that matter).

**D4 — The emitter reaches in through an optional seam; DI auto-injects it.**
`IdentityService` gains one **optional** constructor parameter —
`NotificationService? notifications = null`, appended *last* (after the
existing optional `ITranslationProvider?`). The default (`null`) means
"not wired — the account op proceeds without notifying," which is what the
direct-construction Core test harnesses (the `GuardianAssignmentTests` boot)
already do today, so they keep compiling unchanged. The Web host's DI
container auto-injects the registered `NotificationService` instance into the
optional parameter (the same mechanism by which the optional
`ITranslationProvider?` is already populated in production), so **no DI
factory / decoration is needed** — the registration stays
`AddTransient<IIdentityService, IdentityService>()`. *Forbids:* a new
`INotificationService` interface, a mandatory constructor parameter, or a
factory that decorates the service.

**D5 — The admin surface: a second form on `/admin/signup`.**
The `AdminSignupController` (the existing signup-gate page) gains a second
POST action `SaveNotify(bool notifyAdmins)` + a GET seed
(`vm.NotifyAdmins`) + a second form in `AdminSignup/Index.cshtml` mirroring
the gate form (its own `[ValidateAntiForgeryToken]` POST, one `<select>` on/off,
its own submit button reusing the existing `admin.signup_save` label). One
toggle = one audited write lane (the ADR 0019/0020 shape) — the existing
`Save(bool isOpen)` action and its tests are **untouched**. *Forbids:* folding
the notify toggle into the gate `Save` action (one action should carry one
audited decision), or a new admin page.

**D6 — The localized strings join the closed registry (all four languages).**
Twelve new keys × four languages (en/de/fr/da) on `KnownTranslationKeys` —
eight for the notification surface (the two per-kind labels, the two
preference labels, the two subject templates, the two body templates — the
body templates end `": "` so the UGC snippet appends cleanly, the M6 D6
shape) and four for the admin UI (the section title, the lede, the on/off
option text). The parity test (`KnownTranslationKeys_ParityTests`) enforces
exact key-set equality across the four languages, so a missing translation is
a build failure. *Forbids:* a key in fewer than four languages.

## Consequences

- **The delta is small and additive:** one bool field on `LocaleSettings`
  (ADR 0004 §B.1, no migration), two constants on `NotificationKinds` (now
  eleven), two seams on `IIdentityService` + `IdentityService`
  (read + write gate, the emitter), one optional constructor parameter, one
  new admin POST action + one form, twelve keys × four languages, and one new
  Core test file (the two emitters + the gate + the fan-out + the no-admin
  no-op, run against a live scratch Postgres). That is the whole delta.
- **The no-ADD pin:** no new `AccessAction`, no new `AccessVia`, no new
  `IAuditableResource` adapter, no new email mechanism, no new UI dependency,
  no new bounded context, no new document surface — the
  `NotificationService`'s frozen seams carry it all (D4's optional parameter
  is the *only* new wiring, and it is a no-op in a test harness).
- **The reuse pin:** the M1 durable-email trio delivers the admin emails (the
  same `OutboxEmail` row, the same durable handler, the same dead-letter +
  `/health` degraded gate); ADR 0061's per-recipient language resolution
  governs them verbatim; the closed `KnownTranslationKeys` registry holds the
  subject/body templates (all four languages, the parity test enforces
  equality).
- **The behavior:** a self-hosted operator, on a fresh instance, gets an
  inbox row (and, if their profile has an email, an email) the moment a
  resident signs up *and* again when that resident verifies — for every
  GlobalAdmin, each with their own dedup key and their own per-kind preference
  (disable the email, keep the inbox — the M6 D7 lane). Turning the gate off
  silences both moments with a single audited write. A single-admin seed
  account is never notified about itself (empty recipient set is a no-op).
- **The no-failure pin:** a signup or verification **never** fails because
  the notification lane is down (D3's best-effort `try/catch`); the account
  is the record, the notification is the nudge.
- **Deferred, named:** notifications on *block / unblock*,
  *role-change*, *report-filed-to-admins* are follow-on lanes (each a named
  kind + emitter of the same shape); a per-admin (rather than instance-wide)
  opt-out is a future lane if the recipient set ever needs to be narrower
  than "every GlobalAdmin."
