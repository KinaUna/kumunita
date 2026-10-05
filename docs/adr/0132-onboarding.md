# ADR 0132 — Onboarding (a guided, non-blocking walk-through of a new resident's account setup, with a completion record + a dismissible banner)

Status: Accepted
Date: 2026-10-02

The README roadmap names M22 exactly: "**Onboarding** — a guided walk-through
that walks a new user through account setup." "a guided walk-through" is the
**single stateless `/onboarding` page** (D4) — a fixed sequence of step cards,
each linking into (or inlining a control over) the frozen lane that already
owns that field; "walks a new user through account setup" is the **display
name → avatar → UI language → time zone → date format → email/notification
language → contact + visibility** sequence (the D3 frozen-lane table); "a
new user" is the **`Profile.OnboardingCompletedAt` is `null`** state (the
floor, D1/D5).

M22 rides **frozen** seams, extending none of them:

- **The frozen `IUserInfoService` owner-scope lanes (ADR 0006-E compatible-
  addition idiom)** — `UpsertProfileAsync` (F13 single-write-surface, the
  display-name / contact / visibility fields), `SetProfileAvatarAsync` (the
  C-MED·8 single write-lane shape), `SetProfileTimezoneAsync` /
  `SetProfileDateFormatAsync` / `SetProfileEmailLanguageAsync` (each the
  `SetProfileTimezoneAsync` shape verbatim — owner-scope, one
  `SaveChangesAsync`, **no `AccessAudit` row**, never load-or-creates). M22
  adds **no** new field-write lane — the walk-through *links into* these lanes;
  its **only** new write is `CompleteOnboardingAsync` (D2/D3, C-M22·4).
- **The `LocaleController` settings-tabs shape (ADR 0080)** — the
  linkable-sections pattern, the `FlashAsync` idiom, and the language-**cookie**
  lane (`LocaleCookie`, a per-request string, **not** a `Profile` field — the
  thin-token rule, ADR 0001-B; the UI-language step rides this cookie lane, so
  it calls no `IUserInfoService` write). The `OnboardingController` mirrors
  the `LocaleController` / `ProfileController` shape (`[Authorize]` +
  `Controller` base + the ctor-seam idiom + `FlashAsync`).
- **The `LocaleSettingsViewModel` read-seam shapes** — the `BuildModel` /
  `BuildTimezoneSection` / `BuildDateFormatSection` null-safe idiom (the
  `OnboardingViewModel`'s step "set / not set" hints are the same non-null
  checks over the frozen read seams, D5, C-M22·5).
- **The ADR 0004 §B.1 additive-surface discipline** — the one new Core field
  (`Profile.OnboardingCompletedAt`) is an **additive nullable POCO field on the
  existing `Profile` doc** (delta-detected, idempotent, **no re-seed**, **no
  EF migration**, **no new `*DocTypes` surface**, **no new `Program.cs`
  `AddMarten` line**, **no new bounded context**, **no new
  `IAuditableResource` adapter** — `Profile` already registers in
  `M1DocTypes`; the M19 `IsGuest` / M23 `Bio` / M23 `TagIds` precedent on this
  same doc, D1, C-M22·1).
- **The thin-token rule (ADR 0001-B)** — the "is a new user" state is a
  **profile-field read** (`OnboardingCompletedAt` is `null`), **never** an
  identity claim (D5/D6, C-M22·2/C-M22·5); sign-in is never gated.

## Context

The platform already ships the **full account-setup surface** a new resident
would need to configure — the profile editor (`/profile/edit`, the
`ProfileController` write lane over `IUserInfoService.UpsertProfileAsync` for
display name / contact / visibility), the avatar upload (`SetProfileAvatarAsync`
over the frozen ADR 0011 `IMediaStore`), and the three settings tabs
(`/settings/language`, `/settings/timezone`, `/settings/dateformat` — the
`LocaleController`, over the language-**cookie** lane for UI language +
`SetProfileTimezoneAsync` / `SetProfileDateFormatAsync` /
`SetProfileEmailLanguageAsync` for the persisted overrides). What the platform
does *not* have is a **guided walk-through** that takes a **new resident**
through that surface in one place, records that they've been through it, and
clears itself once they're done — today a fresh resident has to *discover*
each of those surfaces on their own, and there is no record that they have
been through setup at all.

The constraints the choice must honor (all pre-existing, not new):

- **Audit-by-default** (SECURITY.md §3, ADR 0006-C): audience-restricted reads
  are resolved per request and logged. M22's completion read and stamp are
  **owner-scope profile-field reads/writes** (not access decisions) — so the
  completion stamp emits **no** `AccessAudit` row (the
  `UpsertProfileAsync` / `SetProfileTimezoneAsync` / `SetProfileDateFormatAsync`
  / `SetProfileEmailLanguageAsync` convention: "a profile-field write, not an
  access decision", D2, C-M22·3).
- **The thin-token rule** (ADR 0001-B): "is a new user" is a **profile-field
  read**, never an identity claim (the claim set `ClaimTypes.All` is unchanged,
  D6, C-M22·2).
- **`Core` stays HTTP-free** (ADR 0006-D): the new seams live on
  `IUserInfoService` (the owning module); the `OnboardingController` is a thin
  Web wrapper that mints the subject from the signed-in principal
  (`KumunitaPrincipal.SubjectId`) and delegates — it never re-derives standing
  or roles (owner-scope only, D2/D6).
- **Marten owns the domain documents** (ADR 0004 §B): the one new field is an
  **additive field on the existing `Profile` doc** (D1) — **no** new document,
  **no** new DocTypes surface, **no** migration.
- **Lean + Boring, one database** (README principles, ADR 0002): no new doc, no
  new context, no new `AccessAction`, no new storage lane, no per-step session
  state (D1/D4/D6).

The shape that keeps M22 safe (C-M22·2): **add one additive `Profile` field +
one owner-scope completion lane, ride the frozen field-write lanes the
walk-through links into, gate a dismissible banner on the completion read** —
and **add zero new authorization surface**. The completion write is a **single
owner-scope lane** with **no audit row** and **never load-or-creates** (a
profile-field write, not an access decision — the
`SetProfileTimezoneAsync` pin, D2, C-M22·3).

## Decision

**D1 — The completion state is **one additive field on the existing
`Profile` doc**; no new document, no new DocTypes surface (ADR 0004 §B.1).**
`Kumunita.Core.UserInfo.Profile` gains exactly **one** additive member,
`public DateTimeOffset? OnboardingCompletedAt { get; set; }` (nullable,
`null` = not completed = the floor; the banner shows, the nav entry is
present). This is a **new member on an *existing* document** — Marten picks it
up on the existing delta-detected boot (the M23 `Bio`/`TagIds` / M19 `IsGuest`
additive precedent on this same doc), so there is **no new `*DocTypes`
surface, no new `AddMarten` line, no new bounded context, no
`SchemaBootstrap.cs` change, no EF migration**. A **new** profile reads
`null` (not completed). *Forbids:* a new `OnboardingState` document, a new
`*DocTypes` surface, a new `Program.cs` boot line, a new `IAuditableResource`
adapter, a **relational** table, an **EF Core** migration, a **per-step**
persisted cursor, or encoding "finished onboarding" in an **identity claim**
(D6).

**D2 — Two owner-scope seams on `IUserInfoService` (the ADR 0006-E
compatible-addition idiom, the `SetProfileTimezoneAsync` shape verbatim).**
`IUserInfoService` gains exactly **two** members:
`Task CompleteOnboardingAsync(string subjectId, string actorBy)` (stamp
`Profile.OnboardingCompletedAt` = now, one `SaveChangesAsync`, **no
`AccessAudit` row**, **never load-or-creates** → `KeyNotFoundException`) and
`Task<DateTimeOffset?> GetOnboardingCompletedAsync(string subjectId)` (the
owner-scope read; `null` = not completed; may be the `GetProfileAsync` read
re-projected). The self-scope check happens at the Web boundary (the owner is
the actor); the lane does **not** re-gate; it writes
`OnboardingCompletedAt` **only**. *Forbids:* an **audited** write row for a
profile-field write, a Core seam that **re-derives standing/roles**,
**load-or-create**, or a write that touches **any field other than**
`OnboardingCompletedAt` (display name / avatar / timezone / format /
email-language stay their **own** frozen lanes, D3).

**D3 — The walk-through **rides the frozen lanes** for every field it
surfaces — **zero new field-write lanes**.** The `/onboarding` step cards each
delegate to the **existing** lane that owns that field: display name + contact
+ visibility → `UpsertProfileAsync` via `/profile/edit`; avatar →
`SetProfileAvatarAsync`; UI language → the `LocaleController` language-
**cookie** lane via `/settings/language`; time zone → `SetProfileTimezoneAsync`
via `/settings/timezone`; date format → `SetProfileDateFormatAsync` via
`/settings/dateformat`; email language → `SetProfileEmailLanguageAsync` via
`/settings/language`. The walk-through's **only** new write is
`CompleteOnboardingAsync` (D2). *Forbids:* re-implementing any of the frozen
lanes, a walk-through POST that writes `Profile.TimeZone` / `Profile.DateFormat`
/ `Profile.AvatarId` directly, or a walk-through that re-derives an audience
(D3/D6).

**D4 — The walk-through is a **single stateless `/onboarding` page** (the
`LocaleController` settings-tabs shape).** One `OnboardingController`:
`GET /onboarding` (renders the step sequence + the current completion state +
the finish/skip action, from the **owner-scope read**) and
`POST /onboarding/finish` (the **one required** write action: calls
`CompleteOnboardingAsync`, redirects home with a `onboarding.flash_done`
flash). **No per-step POST** (each step links into its owning surface),
**no persisted step cursor**, **no server session state**. *Forbids:* a
per-step URL with a persisted step counter, a multi-document wizard,
server-session step state, or a POST that writes any field other than the
completion flag (D2/D3).
*Amendment (design doc §1.a-C2):* an **optional** `POST /onboarding/skip`
that routes to the **same** `CompleteOnboardingAsync` call is permitted — the
walk-through must be completable *or* explicitly skipped; **neither gates
sign-in**. Both actions clear the banner identically (the same
`OnboardingCompletedAt` stamp) and both surface the `onboarding.skip` button
label (in the locked §8 set); the finish/skip distinction the resident sees
after the redirect is the button they clicked, carried by the single
`onboarding.flash_done` flash key (a distinct "skipped" flash would be a 16th
`onboarding.*` key, a U03 §8-set amendment, not a U00 lock). The locked
surface is "one required `finish` action + (optional) a `skip` action, both
targeting `CompleteOnboardingAsync`".

**D5 — The banner is the **affordance**, shown **only while
`OnboardingCompletedAt` is `null`**, and **non-blocking**.** A dismissible
**home/nav banner** is rendered when, and only when, the signed-in resident's
`OnboardingCompletedAt` is `null` (the floor = a fresh resident sees it). The
banner links to `/onboarding`. It is **non-blocking**: **sign-in is never
gated**, the banner is always dismissible, and the nav entry is present for
any `null`-completion resident. The "is a new user?" check is the **owner-scope
read** of the profile field (D2), **never** a claim (the thin-token rule,
ADR 0001-B). *Forbids:* blocking sign-in, a modal that can't be dismissed, a
**claim-encoded** "is new" flag, or a banner computed from anything other than
the `OnboardingCompletedAt` read (D5/D6).

**D6 — **Zero new authorization surface** (the C-M22·2 pin).** M22 adds
**no** `AccessAction`, **no** `Decide()` branch, **no** `AccessVia`, **no**
`IAuthorizationService` method; the claim set (`ClaimTypes.All`) is
**unchanged**. It adds exactly **one additive `Profile` field** (D1) +
**two owner-scope seams** (D2) + **one controller** (D4) + **one view +
banner** (D5) + **one closed `kw-l` set** (D7). The walk-through is
**owner-scope only** (the signed-in resident reads/writes *their own*
profile); there is no cross-resident surface. *Forbids:* any extension of the
frozen authorization seams, a claim-encoded onboarding state, a new
standing/`AccessVia`, or a cross-resident onboarding read.

**D7 — A **closed `onboarding.*` kw-l key set** × en/de/fr/da.** Every new
user-visible string M22 introduces is a `KnownTranslationKeys` entry present,
**non-empty, in all four** languages (en/de/fr/da), pinned by
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The locked
closed set (15 keys) is: `onboarding.title`, `onboarding.intro`,
`onboarding.step_displayname`, `onboarding.step_avatar`,
`onboarding.step_language`, `onboarding.step_timezone`,
`onboarding.step_dateformat`, `onboarding.step_email`,
`onboarding.step_contact`, `onboarding.visit`, `onboarding.finish`,
`onboarding.skip`, `onboarding.flash_done`, `onboarding.banner.text`,
`onboarding.banner.action`. *Forbids:* a user-visible string rendered from
M22 that is **not** in `KnownTranslationKeys` for all four languages (inline
it and the parity pin fails — that is the point).

**D8 — **Deferred lanes** (each a future ADR, not part of M22).** (1) Per-step
progress / resume-across-sessions; (2) "you haven't set X" detection; (3)
onboarding for elevated/guest accounts; (4) an admin "onboarding completion
rate" metric; (5) I18n beyond the four-language floor.

## Consequences

**C-M22·1 — The completion state is one additive field on the existing
`Profile` doc, `null` = not completed (D1).** `Profile.OnboardingCompletedAt`
is a nullable `DateTimeOffset?`; `null` = the floor (banner shows). A test
asserts the member exists on `Profile`, is nullable, and the default/new
profile reads `null`.

**C-M22·2 — Zero new authorization surface (D6).** No new `AccessAction` /
`Decide()` branch / `AccessVia` / `IAuthorizationService` method;
`ClaimTypes.All` unchanged. M22 adds **one additive `Profile` field** +
**two owner-scope seams** + **one controller** + **one view/banner** +
**one closed `kw-l` set**, and **no adapter, no new DocTypes surface, no new
boot line**.

**C-M22·3 — The completion stamp is a single owner-scope write lane, no audit
row (D2).** `CompleteOnboardingAsync` stamps **only**
`OnboardingCompletedAt` = now, one `SaveChangesAsync`, **zero `AccessAudit`
rows**, **never load-or-creates** (`KeyNotFoundException` when no profile, the
`SetProfileTimezoneAsync` pin).

**C-M22·4 — The walk-through rides the frozen lanes, zero new field-write
lanes (D3).** The `/onboarding` page's **only** write is
`CompleteOnboardingAsync`; every field it surfaces delegates to the frozen
lane that owns it (the D3 table).

**C-M22·5 — The banner is gated on the `OnboardingCompletedAt` read and is
non-blocking (D5).** The banner renders **iff** the owner-scope read is
`null`; it never blocks sign-in; the nav entry is present for a
`null`-completion resident.

**C-M22·6 — The closed `onboarding.*` set is parity-pinned in four languages
(D7).** Every `onboarding.*` key is present, non-empty, in en/de/fr/da; the
closure + parity pins hold.

**FACES (F1–F5) + the named trade.** F1 — a fresh resident is guided through
account setup (C-M22·1, C-M22·5, D4, D5). F2 — a resident finishes (or skips)
and the banner clears (C-M22·3, D2). F3 — every step lands in the surface that
already owns the field (C-M22·4, D3). F4 — onboarding is non-blocking and
claim-free (C-M22·2, C-M22·5, D5, D6). F5 — the walk-through is localized in
the resident's language (C-M22·6, D7). **The named trade:** M22 buys *a guided,
non-blocking, localized walk-through + a completion record + a dismissible
banner* with **zero new authorization surface + one additive `Profile` field +
one owner-scope write lane + one controller + one view/banner + a closed
`kw-l` set**, in exchange for **no per-step progress tracking, no "you're
missing X" detection, no elevated/guest onboarding, no admin completion-rate
metric, and no new languages** (D8) — M22 is *the guided shell + the completion
flag + the banner*, **not** an onboarding *system*.

**The pins.** The **zero-new-authorization-surface** pin is C-M22·2 (D6). The
**one additive field + one owner-scope lane** pin is C-M22·1 + C-M22·3
(D1/D2). The **rides-frozen-lanes, zero new field-write lanes** pin is C-M22·4
(D3). The **dismissible-banner-gated-on-the-completion-read, non-blocking**
pin is C-M22·5 (D5). The **closed four-language `kw-l` set** pin is C-M22·6
(D7).

**The acceptance tests (GATE-1…GATE-6).** GATE-1 — one additive field,
`null` = not completed (C-M22·1, D1). GATE-2 — the completion stamp is one
owner-scope write, no audit row, no load-or-create (C-M22·3, D2). GATE-3 — zero
new field-write lanes; the walk-through rides the frozen lanes (C-M22·4, D3).
GATE-4 — zero new authorization surface (C-M22·2, D6). GATE-5 — the banner is
gated on the completion read and non-blocking (C-M22·5, D5). GATE-6 — the
closed `onboarding.*` set is parity-pinned in four languages (C-M22·6, D7).

**The deferred lanes (D8).** (1) Per-step progress / resume-across-sessions
— the stateless single-page shape is the locked D4 surface; a persisted step
cursor is a new data surface. (2) "You haven't set X" detection — the "set /
not set" hints are the `OnboardingViewModel`'s non-null checks; an automatic
recommendation engine is a larger design. (3) Onboarding for elevated/guest
accounts — the Guardian (M28) / Guest (M19) lanes are their own ADRs; M22 is
the resident's account setup. (4) An admin "onboarding completion rate" metric
— the M13 logging lane is its own surface; M22 ships **no** admin view. (5)
I18n beyond the four-language floor — the en/de/fr/da parity pin is the M22
floor; additional catalog languages are the M9/M15 lanes.
