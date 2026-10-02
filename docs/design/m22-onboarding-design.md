# M22 — Onboarding (design doc)

> **Milestone M22 — Onboarding.** The README M22 line, verbatim:
> "**Onboarding** — a guided walk-through that walks a new user through account
> setup." M22 ships a **guided, non-blocking, localized walk-through** that takes
> a **new resident** through the account-setup surface they'd otherwise have to
> discover themselves — display name, avatar, UI language, time zone, date
> format, email/notification language, and contact details + visibility — and
> records that they've been through it.
>
> **A milestone over the frozen owner-scope lanes.** M22 rides **frozen** seams,
> extending none of them: the frozen `IUserInfoService` owner-scope lanes
> (`UpsertProfileAsync`, `SetProfileAvatarAsync`, `SetProfileTimezoneAsync`,
> `SetProfileDateFormatAsync`, `SetProfileEmailLanguageAsync`, D3), the
> `LocaleController` settings-tabs shape (the linkable-sections + `FlashAsync`
> + language-**cookie** lanes, D4), and the ADR 0004 §B.1 additive-surface
> discipline (D1). **Zero new `IAuthorizationService` surface, zero new
> `AccessAction`, zero new `AccessVia`, zero new `Decide()` branch, zero new
> claim type, zero new `*DocTypes` surface, zero new `Program.cs` boot line,
> zero new storage lane** (C-M22·2, D6). The **only** additions are **one
> additive `Profile` field** (`OnboardingCompletedAt: DateTimeOffset?`, D1) +
> **two owner-scope seams on `IUserInfoService`** (`CompleteOnboardingAsync`
> write + `GetOnboardingCompletedAsync` read, D2) + **one
> `OnboardingController`** (D4) + **one view + banner** (D5) + **one closed
> `onboarding.*` kw-l set** (15 keys × en/de/fr/da, D7). The six GATE acceptance
> tests (GATE-1…GATE-6, §9) + the drift-guard (§10) are locked in **ADR 0132
> (Accepted, 2026-10-02)**.
>
> **Status.** **LOCKED.** The decisions D1–D8, the invariants
> C-M22·1…C-M22·6, the FACES F1–F5 + the named trade, the closed `onboarding.*`
> kw-l key set (§8), and the §gate test names (§9) are locked in **ADR 0132
> (Accepted, 2026-10-02)**. The `[PROPOSED]` set in the register
> `docs/plans-milestones/in-progress/plan-m22-onboarding.md` is the locked set
> this doc restates **verbatim**, with **two named amendment surfaces**
> resolved in §1.a (D1's `Profile` member name/type confirmed; D4's
> skip-vs-finish shape confirmed at the register's single-`finish`+optional-
> `skip` shape).
>
> **The one thing every unit must respect:** M22 **rides the frozen
> owner-scope lanes** and **adds zero new authorization surface** (D6,
> C-M22·2). The completion state is **one additive nullable field on the
> existing `Profile` doc** (`null` = not completed, D1, C-M22·1); the completion
> stamp is a **single owner-scope write lane, no audit row, never
> load-or-creates** (D2, C-M22·3); the walk-through **rides the frozen lanes**
> and **adds zero new field-write lanes** (D3, C-M22·4); the banner is **gated
> on the completion read** and is **non-blocking** (D5, C-M22·5); every string
> is a **closed `onboarding.*` key in four languages** (D7, C-M22·6). It does
> **not** add a branch to `Decide()`, a new `IAuthorizationService` signature,
> a new `AccessAction`, a new `AccessVia`, a claim-encoded "is new" flag, a new
> `OnboardingState` doc, a new DocTypes surface, a new storage lane, a
> per-step persisted cursor, or a blocking wall (the §drift-guard, §10, is the
> exact set of traps that would break it).

## 0 — Scope & non-scope

**What M22 does (register's "Understanding", verbatim):** M22 = **onboarding.**
The roadmap line is the scope:
> "Onboarding — a guided walk-through that walks a new user through account
> setup."

M22 ships a **guided walk-through** that takes a **new resident** through the
account-setup surface they'd otherwise have to discover themselves — display
name, avatar, UI language, time zone, date format, email/notification language,
and contact details + visibility — and records that they've been through it.
Three things follow from the roadmap sentence:

1. **The walk-through is a *guided shell*, not a new data surface.** Every
   actual field it touches is **already writable** through a **frozen**
   owner-scope lane: `UpsertProfileAsync` (display name, contact details,
   visibility), `SetProfileAvatarAsync`, `SetProfileTimezoneAsync`,
   `SetProfileDateFormatAsync`, `SetProfileEmailLanguageAsync`
   (all on `IUserInfoService`), and the `LocaleController` language-cookie lane.
   M22 **rides those lanes verbatim** (D3) — it adds **no new field-write
   lane**. The walk-through is the *sequence + the copy + the completion
   record*, not a second write path.
2. **The only NEW Core surface is one additive completion marker + one
   owner-scope lane** (D1, D2): an additive
   `Profile.OnboardingCompletedAt: DateTimeOffset?` field (ADR 0004 §B.1 —
   delta-detected, idempotent, **no re-seed, no EF migration, no new `*DocTypes`
   surface, no new bounded context, no new adapter**) and one owner-scope write
   seam `CompleteOnboardingAsync(subjectId, actorBy)` (the
   `SetProfileTimezoneAsync` shape verbatim — owner-scope, one
   `SaveChangesAsync`, **no `AccessAudit` row** because it's a profile-field
   write, not an access decision). The "am I done?" read rides the existing
   `GetProfileAsync`.
3. **The banner is the *affordance*, and it is non-blocking** (D5): a
   dismissible home/nav banner is shown **only while
   `OnboardingCompletedAt` is `null`** (the floor = a fresh resident sees it),
   and cleared by the explicit finish/skip action. **Sign-in is never blocked**;
   the "is a new user" state is a *profile field read*, never an identity claim
   (the thin-token rule, ADR 0001-B).

Everything else about M22 is *what it is not* — see below and §11.

**What M22 is NOT (register's "What M22 is NOT", verbatim):**

- **Not a new data model.** M22 adds **one additive field** to the **existing**
  `Profile` doc (`OnboardingCompletedAt`) and **one owner-scope write lane**.
  There is **no new `OnboardingState` document, no new `*DocTypes` surface, no
  new bounded context, no new `IAuditableResource` adapter, no new
  `Program.cs` `AddMarten` line, no relational table** (D1, D6). The walk-
  through's state is *the completion flag on the profile the resident already
  owns*.
- **Not a multi-document wizard with per-step session state.** The walk-through
  is a **single stateless `/onboarding` page**: a fixed sequence of step cards,
  each either a lightweight inline control wired to the frozen lane or a link
  into the existing surface that already owns that field
  (`/profile/edit`, `/settings/*`). The **only** persisted state is
  `OnboardingCompletedAt`. **No per-step server session, no persisted step
  cursor, no per-step URL** (D4).
- **Not a second write path.** Every field write the walk-through surfaces
  delegates to the **frozen** lane that already owns that field (D3). The
  walk-through never re-implements `UpsertProfileAsync`, never writes
  `Profile.TimeZone` itself, and never re-derives an audience. It *links into*
  the existing editor/settings and *stamps completion*.
- **Not blocking, not modal, not a claim.** Onboarding is a **dismissible
  banner + a page**, never a wall that blocks sign-in or a stateless claim
  (D5, D6). A resident who closes the banner keeps the `null` completion state
  (they can return via the nav entry); the nav entry is always present for a
  `null`-completion resident.
- **Not a new authorization surface.** M22 adds **no** `AccessAction`, **no**
  `Decide()` branch, **no** `AccessVia`, **no** `IAuthorizationService` method;
  the claim set (`ClaimTypes.All`) is **unchanged** (D6). The walk-through is
  **owner-scope only** (the signed-in resident reads/writes *their own*
  profile); the completion read is owner-scope.
- **Not a new notification lane.** Finishing onboarding stages **no** email
  (M6/ADR 0076 is untouched); M22 is a guided shell, not a notify lane.
- **Not a new storage lane.** The avatar step rides the **frozen ADR 0011
  `IMediaStore`** through the existing `SetProfileAvatarAsync` lane; M22 adds
  no storage mechanism (D3).

## 1 — Decisions (D1–D8) — **LOCKED** (verbatim from the register's [PROPOSED] set; §1.a records the two named amendment surfaces)

**D1 — The completion state is **one additive field on the existing `Profile`
doc** (ADR 0004 §B.1).** `Kumunita.Core.UserInfo.Profile` gains exactly **one**
additive member (locked by U00's design doc):

```csharp
/// M22 (ADR 0132, D1) — the onboarding completion stamp. <c>null</c> = the
/// resident has not finished the guided walk-through (the floor: the banner
/// shows, the nav entry is present). A non-null value = finished/skipped; the
/// banner clears. Written only by the owner-scope
/// <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane (D2); read
/// through the existing <see cref="IUserInfoService.GetProfileAsync"/> read
/// (never a claim, D6). Additive per ADR 0004 §B.1: delta-detected,
/// idempotent, no re-seed, no EF migration.
public DateTimeOffset? OnboardingCompletedAt { get; set; }
```

Shape pin: **`DateTimeOffset?`**, nullable, **`null` = not completed**. This
is a **new member on an *existing* document** — Marten picks it up on the
existing delta-detected boot (the M23 `Bio`/`TagIds` additive precedent, ADR
0004 §B.1), so there is **no new `*DocTypes` surface, no new `AddMarten` line,
no new bounded context, no `SchemaBootstrap.cs` change**.

*Forbids:* a new `OnboardingState` document, a new `*DocTypes` surface, a new
`Program.cs` boot line, a new `IAuditableResource` adapter, a **relational**
table, an **EF Core** migration, a **per-step** persisted cursor, or encoding
"finished onboarding" in an **identity claim** (D6).

**D2 — Two owner-scope seams on `IUserInfoService` (the ADR 0006-E
compatible-addition idiom, the `SetProfileTimezoneAsync` shape verbatim).**
`IUserInfoService` gains exactly **two** members (the
`SetProfileTimezoneAsync` / `SetProfileDateFormatAsync` /
`SetProfileEmailLanguageAsync` shape verbatim — owner-scope, "not an access
decision"):

```csharp
Task CompleteOnboardingAsync(string subjectId, string actorBy);
Task<DateTimeOffset?> GetOnboardingCompletedAsync(string subjectId);
```

*Forbids:* an **audited** write row for a profile-field write (a completion
stamp is not an access decision), a Core seam that **re-derives standing/roles**
(the actor is the signed-in owner, owner-scope), **load-or-create** (the
`SetProfileTimezoneAsync` "never load-or-creates" pin — throw
`KeyNotFoundException`), or a write that touches **any field other than**
`OnboardingCompletedAt` (the single-write-lane pin — display name / avatar /
timezone / format / email-language stay their **own** frozen lanes, D3).

**D3 — The walk-through **rides the frozen lanes** for every field it
surfaces — **zero new field-write lanes**.** The walk-through's step cards each
delegate to the **existing** lane that owns that field (D3; the frozen seams
U00 locks verbatim in the design doc's "Seams & contracts" section — §6):

| Step | Surface it rides | Frozen lane (already shipped) |
| --- | --- | --- |
| Display name + contact details + visibility | `/profile/edit` | `IUserInfoService.UpsertProfileAsync` (the F13 single-write-surface) |
| Avatar | `/profile` (avatar upload) | `IUserInfoService.SetProfileAvatarAsync` |
| UI language | `/settings/language` | the `LocaleController` language-**cookie** lane (`LocaleCookie`, a per-request string, **not** a `Profile` field) |
| Time zone | `/settings/timezone` | `IUserInfoService.SetProfileTimezoneAsync` |
| Date & time format | `/settings/dateformat` | `IUserInfoService.SetProfileDateFormatAsync` |
| Email & notification language | `/settings/language` (email section) | `IUserInfoService.SetProfileEmailLanguageAsync` |

The walk-through **adds no field-write lane**: its **only** new write is
`CompleteOnboardingAsync` (D2). It *links into* the existing editor/settings
(and, where trivial, inlines the control) and *stamps completion*.

*Forbids:* re-implementing any of the frozen lanes, a walk-through POST that
writes `Profile.TimeZone`/`Profile.DateFormat`/`Profile.AvatarId` directly, or a
walk-through that re-derives an audience (D3/D6).

**D4 — The walk-through is a **single stateless `/onboarding` page** (the
`LocaleController` settings-tabs shape).** One `OnboardingController` (D4; the
`LocaleController` linkable-sections shape):

- `GET /onboarding` — renders the guided step sequence (the closed `onboarding.*`
  keys, D7) + the current completion state + the "finish / skip" action. The
  model is built from the **owner-scope read** (`GetProfileAsync` for
  `OnboardingCompletedAt` + the frozen read seams each step needs to show a
  "set / not set" hint, e.g. has-avatar, has-timezone-override).
- `POST /onboarding/finish` — the **one** write action: calls
  `CompleteOnboardingAsync` (D2) for the signed-in subject and redirects home
  with a `onboarding.flash_done` flash (the `LocaleController.FlashAsync` idiom).

**No per-step POST** (each step links into its owning surface), **no persisted
step cursor**, **no server session state** (D4). The only POST is
`/onboarding/finish` (and an optional `/onboarding/skip` that routes to the
same `CompleteOnboardingAsync` call — see §1.a C2).

*Forbids:* a per-step URL with a persisted step counter, a multi-document
wizard, server-session step state, or a POST that writes any field other than
the completion flag (D2/D3).

**D5 — The banner is the **affordance**, shown **only while
`OnboardingCompletedAt` is `null`**, and **non-blocking**.** A dismissible
**home/nav banner** (D5) is rendered in the shared layout / home page when, and
only when, the signed-in resident's `OnboardingCompletedAt` is `null` (the
floor = a fresh resident sees it). The banner links to `/onboarding`. It is
**non-blocking**: **sign-in is never gated**, the banner is always dismissible,
and the nav entry is present for any `null`-completion resident. The "is a new
user?" check is the **owner-scope read** of the profile field (D2), **never** a
claim (the thin-token rule, ADR 0001-B) and **never** a per-request flag
recomputed from scratch.

*Forbids:* blocking sign-in, a modal that can't be dismissed, a **claim-encoded**
"is new" flag, or a banner computed from anything other than the
`OnboardingCompletedAt` read (D5/D6).

**D6 — **Zero new authorization surface** (the C-M22·2 pin).** M22 adds
**no** `AccessAction`, **no** `Decide()` branch, **no** `AccessVia`, **no**
`IAuthorizationService` method; the claim set (`ClaimTypes.All`) is
**unchanged**. It adds exactly **one additive `Profile` field** (D1) +
**two owner-scope seams** (D2) + **one controller** (D4) + **one view + banner**
(D5) + **one closed `kw-l` set** (D7). The walk-through is **owner-scope
only**: the signed-in resident reads/writes *their own* profile; there is no
cross-resident surface.

*Forbids:* any extension of the frozen authorization seams, a claim-encoded
onboarding state, a new standing/`AccessVia`, or a cross-resident onboarding
read (the whole point of M22 is that it *rides* the owner-scope lanes, it does
**not** extend them).

**D7 — A **closed `onboarding.*` kw-l key set** × en/de/fr/da, authored by
U03.** Every new user-visible string M22 introduces is a `KnownTranslationKeys`
entry present, **non-empty, in all four** languages (en/de/fr/da), pinned by
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The
**locked closed set** is in §8 (the register's table; U03 authors the
`KnownTranslationKeys` entries; the *discipline* — closed, four-language,
parity-pinned — is locked).

*Forbids:* a user-visible string rendered from `M22` that is **not** in
`KnownTranslationKeys` for all four languages (inline it and the parity pin
fails — that is the point).

**D8 — **Deferred lanes** (each a future ADR, not part of M22).**
§11 lists the five deferred lanes verbatim (per-step progress; "you haven't
set X" detection; elevated/guest onboarding; an admin completion-rate metric;
I18n beyond the four-language floor).

---

### §1.a — Amendments (two named surfaces, resolved in U00)

The register flags two amendment surfaces (D1's `Profile` member name/type;
D4's skip-vs-finish shape). Both are resolved against the live codebase:

- **C1 — D1's `Profile` member name/type: confirmed as
  `OnboardingCompletedAt: DateTimeOffset?`.** Verified against
  `src/Kumunita.Core/UserInfo/Profile.cs`: the existing doc already carries
  **ten** additive fields in the ADR 0004 §B.1 shape (`AvatarId`, `TimeZone`,
  `DateFormat`, `EmailLanguage`, and the M23 `Bio`/`TagIds` precedent), all
  registered in `M1DocTypes` (no new DocTypes surface, no new boot line, no
  `SchemaBootstrap.cs` change). The **single nullable
  `DateTimeOffset?`** (not a `bool + DateTime` pair, and not a plain `bool`)
  is locked because (a) it matches the `SetProfileTimezoneAsync` /
  `SetProfileDateFormatAsync` / `SetProfileEmailLanguageAsync` owner-scope
  shape verbatim (owner-scope, one `SaveChangesAsync`, no audit row, never
  load-or-creates), (b) `null` = "not completed" is the floor the banner gates
  on (D5, C-M22·5), and (c) a `bool` + `DateTime` pair would be two members on
  the doc for the same fact — one nullable member carries both the state
  (null/non-null) and the stamp (the value) without a second field to keep
  in sync. The **shape** (one additive nullable member on the existing
  `Profile` doc, read through `GetProfileAsync`, written by one owner-scope
  lane) is locked; the member name `OnboardingCompletedAt` is the locked
  spelling.
- **C2 — D4's skip-vs-finish shape: confirmed at the register's single-
  `finish` + optional-`skip` shape.** The walk-through has **one required
  POST** (`/onboarding/finish` → `CompleteOnboardingAsync`) and **one
  optional POST** (`/onboarding/skip` → the **same** `CompleteOnboardingAsync`
  call). Neither action writes any field other than
  `OnboardingCompletedAt`; neither gates sign-in; neither persists a step
  cursor. Both surface the `onboarding.skip` kw-l key (the "Skip for now"
  button label, in the locked §8 set) and both clear the banner identically
  (the stamp is the same `OnboardingCompletedAt` value). The finish/skip
  distinction the resident sees after the redirect is carried by the
  **already-locked `onboarding.flash_done`** flash (the single flash key in
  the §8 set) — a distinct "skipped" flash is **not** a 16th key; if a unit
  wants one, it is a U03 §8-set amendment (a new `onboarding.flash_skip` key,
  four-language, parity-pinned), not a U00 lock. Collapsing to a single
  `/onboarding/finish` is also valid (the register permits U00 to collapse
  skip→finish); this doc **locks the two-action shape** (the register's
  default) because the `onboarding.skip` kw-l key is already in the locked §8
  set. If U03 later collapses to one action, the §8 set drops
  `onboarding.skip` (and the parity pin follows) — the *shape* (one required +
  optional action, same `CompleteOnboardingAsync` target) is the locked
  surface; only the action count is the amendment surface.

## 2 — Invariants (C-M22·1 … C-M22·6)

- **C-M22·1 — The completion state is one additive field on the existing
  `Profile` doc, `null` = not completed (D1).** `Profile.OnboardingCompletedAt`
  is a nullable `DateTimeOffset?`; `null` = the floor (banner shows). A test
  asserts the member exists on `Profile`, is nullable, and the default/new
  profile reads `null` (not completed).
- **C-M22·2 — Zero new authorization surface (D6).** No new `AccessAction` /
  `Decide()` branch / `AccessVia` / `IAuthorizationService` method;
  `ClaimTypes.All` unchanged. M22 adds **one additive `Profile` field** +
  **two owner-scope seams** + **one controller** + **one view/banner** +
  **one closed `kw-l` set**, and **no adapter, no new DocTypes surface, no new
  boot line**. A pin test asserts the seam/claim surface is unchanged.
- **C-M22·3 — The completion stamp is a single owner-scope write lane, no audit
  row (D2).** `CompleteOnboardingAsync` stamps **only**
  `OnboardingCompletedAt` = now, one `SaveChangesAsync`, **zero `AccessAudit`
  rows** (a profile-field write, not an access decision), **never
  load-or-creates** (`KeyNotFoundException` when no profile, the
  `SetProfileTimezoneAsync` pin). A test pins the single field-write + the no-
  audit-row + the no-load-or-create.
- **C-M22·4 — The walk-through rides the frozen lanes, zero new field-write
  lanes (D3).** The `/onboarding` page's **only** write is
  `CompleteOnboardingAsync`; every field it surfaces delegates to the frozen
  lane that owns it (the table in D3). A test pins that the walk-through
  introduces no new `IUserInfoService` write member other than
  `CompleteOnboardingAsync` (+ the read seam).
- **C-M22·5 — The banner is gated on the `OnboardingCompletedAt` read and is
  non-blocking (D5).** The banner renders **iff** the owner-scope read is
  `null`; it never blocks sign-in; the nav entry is present for a
  `null`-completion resident. A test pins the banner-eligibility read (the
  `OnboardingViewModel` exposes the `null`/non-null completion state) + the
  non-blocking shape (no auth gate on the banner read, no claim read).
- **C-M22·6 — The closed `onboarding.*` set is parity-pinned in four languages
  (D7).** Every `onboarding.*` key is present, non-empty, in en/de/fr/da; the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` closure
  pins it. A test pins the key set closure + the four-language parity.

## 3 — FACES (F1–F5) + the named trade

- **F1 — A fresh resident is guided through account setup** (C-M22·1, C-M22·5,
  D4, D5): the `/onboarding` page walks display name → avatar → language →
  timezone → date format → email language → contact, each step riding the
  frozen lane that owns it; the banner invites them.
- **F2 — A resident finishes (or skips) and the banner clears** (C-M22·3, D2):
  the finish/skip action stamps `OnboardingCompletedAt` = now, one owner-scope
  write, no audit row; the banner + nav entry clear on the next read.
- **F3 — Every step lands in the surface that already owns the field**
  (C-M22·4, D3): the walk-through never re-implements a write lane — display
  name/contact go to `/profile/edit`, avatar to the avatar upload,
  language/timezone/format/email-language to `/settings/*` — so M22 adds zero
  new field-write surface.
- **F4 — Onboarding is non-blocking and claim-free** (C-M22·2, C-M22·5, D5,
  D6): sign-in is never gated; the "is new" state is a profile-field read, not
  a claim; the banner is always dismissible.
- **F5 — The walk-through is localized in the resident's language** (C-M22·6,
  D7): every string is a closed `onboarding.*` key resolved through the
  `kw-l` provider floor (the en source text is the floor, ADR 0015 D1), in
  en/de/fr/da.

**The named trade.** M22 buys *a guided, non-blocking, localized walk-through
for a new resident's account setup + a completion record + a dismissible
banner* with **zero new authorization surface + one additive `Profile` field +
one owner-scope write lane + one controller + one view/banner + a closed
`kw-l` set**, in exchange for **no per-step progress tracking, no
"you're missing X" detection, no elevated/guest onboarding, no admin
completion-rate metric, and no new languages** (D8): M22 is *the guided shell +
the completion flag + the banner*, **not** an onboarding *system*. The deferred
lanes (D8·1…D8·5) are deliberately *not* in M22: they are larger designs that
need their own ADRs, and M22's value (a new resident who sees a short guided
tour of the setup that already exists, finishes it in one place, and never
sees the banner again) is delivered by the additive field + the one owner-
scope lane + the frozen lanes it links into.

## 4 — The additive `Profile.OnboardingCompletedAt` field (exact C#, D1, U01)

The **exact** additive member on `Kumunita.Core.UserInfo.Profile` (D1,
§1.a-C1 locked). The doc already carries ten additive fields in the ADR 0004
§B.1 shape (`AvatarId`, `TimeZone`, `DateFormat`, `EmailLanguage`, `Bio`,
`TagIds` — all delta-detected, all registered in `M1DocTypes`). Adding
`OnboardingCompletedAt` follows the same shape:

```csharp
// in src/Kumunita.Core/UserInfo/Profile.cs — one additive field (D1)
/// M22 (ADR 0132, D1) — the onboarding completion stamp. <c>null</c> = the
/// resident has not finished the guided walk-through (the floor: the banner
/// shows, the nav entry is present). A non-null value = finished/skipped; the
/// banner clears. Written only by the owner-scope
/// <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane (D2); read
/// through the existing <see cref="IUserInfoService.GetProfileAsync"/> read
/// (never a claim, D6). Additive per ADR 0004 §B.1: delta-detected,
/// idempotent, no re-seed, no EF migration.
public DateTimeOffset? OnboardingCompletedAt { get; set; }
```

**Additive on an existing doc** (the M23 `Bio`/`TagIds` ADR 0004 §B.1
precedent verbatim): delta-detected, idempotent, **no re-seed, no EF
migration, no new `*DocTypes` surface, no new `Program.cs` `AddMarten` line,
no `SchemaBootstrap.cs` change, no new bounded context, no new
`IAuditableResource` adapter, no new storage lane**. The `Profile` doc is
already registered in `M1DocTypes`; adding a field is the §B.1 additive lane
(the M19 `IsGuest` / M23 `Bio` / M23 `TagIds` history on this same doc).
A **new** profile (a `new Profile()` at bootstrap) reads `null` for
`OnboardingCompletedAt` — the floor (banner shows, nav entry present).
The existing `ProfileUpdate` patch record is **not** extended: the
completion stamp is written by its own dedicated lane (D2), not folded
into the F13 single-write-surface patch (which is for the display-name /
contact / visibility / bio / tags fields the resident author-sets — the
completion flag is a platform-set stamp, not an author-set field).

## 5 — The two owner-scope seams on `IUserInfoService` (D2, exact C#, U01)

The **exact** two members added to `IUserInfoService` (D2, §1.a-C1/C2
locked). The `SetProfileTimezoneAsync` shape verbatim (owner-scope, "not an
access decision", never load-or-creates):

```csharp
// in src/Kumunita.Core/UserInfo/IUserInfoService.cs — two additive seams (D2)

/// <summary>
/// M22 (ADR 0132, D2) — stamp the onboarding-completed state. Owner-scope
/// single write lane (the <see cref="SetProfileTimezoneAsync"/> shape
/// verbatim): stamps <see cref="Profile.OnboardingCompletedAt"/> = now, one
/// <c>SaveChangesAsync</c>, **no <c>AccessAudit</c> row** (a profile-field
/// write, not an access decision), **never load-or-creates** (throws
/// <see cref="KeyNotFoundException"/> when no <see cref="Profile"/> exists,
/// the <see cref="SetProfileTimezoneAsync"/> pin).
/// </summary>
Task CompleteOnboardingAsync(string subjectId, string actorBy);

/// <summary>
/// M22 (ADR 0132, D2) — owner-scope read of the completion flag (the
/// <see cref="Profile.OnboardingCompletedAt"/> value; <c>null</c> = not
/// completed). May be the <see cref="GetProfileAsync"/> read re-projected
/// rather than a distinct method (U01 picks; the *shape* is owner-scope,
/// no audit row).
/// </summary>
Task<DateTimeOffset?> GetOnboardingCompletedAsync(string subjectId);
```

**The `CompleteOnboardingAsync` shape** (the `SetProfileTimezoneAsync` pin
verbatim — the ADR 0006-E compatible-addition idiom this file already uses):

- **Owner-scope** — the self-scope check happens at the Web boundary (the
  owner is the actor); the lane does **not** re-gate.
- **One session, one `SaveChangesAsync`** — stamps
  `Profile.OnboardingCompletedAt = DateTimeOffset.UtcNow` (or the session
  clock, U01's choice — the value is the "when did the resident finish"
  stamp, the exact value is not the invariant — the invariant is "non-null =
  done, null = not done").
- **No `AccessAudit` row** — a profile-field write, not an access decision
  (the `UpsertProfileAsync` / `SetProfileTimezoneAsync` /
  `SetProfileDateFormatAsync` / `SetProfileEmailLanguageAsync` convention —
  "not an access decision" is the doc-comment language on every one of those
  four lanes in the live `IUserInfoService`).
- **Never load-or-creates** — throws
  `System.Collections.Generic.KeyNotFoundException` when no `Profile` with
  that `subjectId` exists (the exact `<exception>` doc-comment shape the live
  `SetProfileTimezoneAsync` / `SetProfileDateFormatAsync` /
  `SetProfileEmailLanguageAsync` lanes already carry).
- **Strong consistency (invariant C4)** — the new value is live on the very
  next `GetProfileAsync` / `GetOnboardingCompletedAsync` call.
- **Writes `Profile.OnboardingCompletedAt` only** — no other field (D3,
  C-M22·4 — the single-write-lane pin: display name / avatar / timezone /
  format / email-language stay their own frozen lanes).

**The `GetOnboardingCompletedAsync` shape** (D2 read seam):

- **Owner-scope read** — the signed-in resident reads *their own* completion
  flag; no audience, no decision, no `AccessAudit` row (the
  `GetProfileAsync` read re-projected, or a distinct method — U01's choice;
  the shape is owner-scope, no audit row).
- **Returns `null`** when the resident has not finished (the floor); returns
  the `DateTimeOffset` stamp when they have.
- **Strong consistency (invariant C4)** — the value is live on the very next
  call after `CompleteOnboardingAsync` commits.

## 6 — Seams & contracts (Part 2): the frozen lanes the walk-through links into (D3, exact signatures, U02/U03)

The **exact** frozen-lane signatures + routes the walk-through surfaces, read
from the live `IUserInfoService.cs` + `LocaleController.cs` +
`ProfileController.cs`. The walk-through **adds no field-write lane**; its
**only** new write is `CompleteOnboardingAsync` (D2, §5). Each step card
links into (and, where trivial, inlines a control over) the frozen lane that
already owns that field:

| Step | Route (link target) | Frozen lane (exact signature) | Notes |
| --- | --- | --- | --- |
| Display name + contact details + visibility | `GET /profile/edit` + `POST /profile/edit` | `Task UpsertProfileAsync(Profile profile, ProfileUpdate patch, string? actorBy = null)` | the F13 single-write-surface pin (the `ProfileController.Edit` POST is the sole Web caller; the `ProfileUpdate` patch record is the frozen shape M23 extended with `Bio` / `TagIds`). The walk-through step card links into this surface; it does **not** re-implement the patch. |
| Avatar | `GET /profile` (avatar upload) | `Task SetProfileAvatarAsync(string subjectId, string? avatarId, string actorBy)` | the C-MED·8 single write-lane shape (owner-scope, no audit row, never load-or-creates); the avatar upload itself rides the frozen ADR 0011 `IMediaStore` + `IOptions<MediaOptions>` C-MED·5 size/type guard (the `ProfileController.AvatarUpload` Web-only `IFormFile`-to-bytes boundary). The walk-through step card links into the avatar upload; it does **not** re-implement the media boundary. |
| UI language | `GET /settings/language` (the `LocaleController.Index` action; the `LocaleCookie` language section) | the `LocaleController` **cookie** lane (`LocaleCookie.Read` / `LocaleCookie.Write` / `LocaleCookie.Clear` — a per-request `string`, **not** a `Profile` field; the thin-token rule, ADR 0001-B — the cookie is passed to `ITranslationProvider` as a plain BCP-47 string on every subsequent request, M·8 — Core stays HTTP-free) | the walk-through step card links into the Language settings tab; the UI language is a **cookie**, not a `Profile` field, so the walk-through does **not** call any `IUserInfoService` lane for it. |
| Time zone | `GET /settings/timezone` (the `LocaleController.SettingsTimezone` action) | `Task SetProfileTimezoneAsync(string subjectId, string? timezone, string actorBy)` | the C-MED·8 single write-lane shape verbatim (owner-scope, one `SaveChangesAsync`, no `AccessAudit` row, never load-or-creates — `KeyNotFoundException` when no profile). The walk-through step card links into the Timezone settings tab. |
| Date & time format | `GET /settings/dateformat` (the `LocaleController.SettingsDateFormat` action) | `Task SetProfileDateFormatAsync(string subjectId, string? formatString, string actorBy)` | the C-MED·8 single write-lane shape verbatim (owner-scope, one `SaveChangesAsync`, no `AccessAudit` row, never load-or-creates — `KeyNotFoundException` when no profile). The walk-through step card links into the DateFormat settings tab. |
| Email & notification language | `GET /settings/language` (the email section folded into the Language tab, ADR 0061 / ADR 0080) | `Task SetProfileEmailLanguageAsync(string subjectId, string? emailLanguage, string actorBy)` | the C-MED·8 single write-lane shape verbatim (owner-scope, one `SaveChangesAsync`, no `AccessAudit` row, never load-or-creates — `KeyNotFoundException` when no profile). The walk-through step card links into the email section of the Language settings tab. |

**The `FlashAsync` idiom** (the `LocaleController.FlashAsync` /
`ProfileController.FlashAsync` shape the `OnboardingController` mirrors):

- `FlashAsync(string key, params object?[] args)` — resolves a `onboarding.*`
  kw-l key to the resident's effective language (the house
  `EffectiveLanguageCode.ResolveAsync` + `ITranslationProvider.GetAsync` seam —
  the same chain the view's `<kw-l>` TagHelper uses, so the flash string
  renders in the resident's language); falls back to the
  `KnownTranslationKeys.EnValues` source text when the translation seam is
  absent (the test-construction floor, ADR 0015 D1 — code is the floor, so a
  resident never sees a raw key). The `OnboardingController.Finish` /
  `Skip` actions both call `FlashAsync("onboarding.flash_done")` (the single
  flash key in the locked §8 set) and redirect home — the finish/skip
  distinction is the button the resident clicked (`onboarding.finish` /
  `onboarding.skip`), not a distinct flash key.
- The `LocaleSettingsViewModel` read-seam shapes (the `BuildModel` /
  `BuildTimezoneSection` / `BuildDateFormatSection` shape) are the **template**
  the `OnboardingViewModel` mirrors — a `BuildModel` that seeds the step
  "set / not set" hints off the frozen read seams (the `Profile.AvatarId`
  non-null check for the avatar step; the `Profile.TimeZone` non-null check for
  the timezone step; the `Profile.DateFormat` non-null check for the date-format
  step; the `Profile.EmailLanguage` non-null check for the email-language step;
  the `LocaleCookie.Read` for the UI-language step).

## 7 — The `OnboardingController` + `OnboardingViewModel` (D4/D5, exact route/action shape, U02/U03)

The **exact** actions (D4, §1.a-C2 locked) + the `OnboardingViewModel`'s
completion-state read + step hints (D5). The `LocaleController` settings-tabs
shape (the `[Authorize]` + `Controller` base + the ctor-seam idiom + the
`FlashAsync` idiom + the linkable-sections pattern) is the template.

**The `OnboardingController` actions:**

| Action | Route | Shape |
| --- | --- | --- |
| `Index` | `GET /onboarding` | `public async Task<IActionResult> Index()` — builds the `OnboardingViewModel` from the owner-scope read (`GetProfileAsync` for the signed-in subject; `null`-safe: an unauthenticated request has no subject and no profile read — the `LocaleController.BuildModel` null-safe idiom) and renders the step sequence (the closed `onboarding.*` keys, §8) + the current completion state + the finish/skip actions. The `[Authorize]` + `Controller` base is the `LocaleController` / `ProfileController` shape (the owner-scope page — the signed-in resident reads/writes *their own* profile; there is no cross-resident surface, D6). |
| `Finish` | `POST /onboarding/finish` | `public async Task<IActionResult> Finish()` — the **one required** write action: calls `IUserInfoService.CompleteOnboardingAsync(subjectId, actorBy)` (D2, §5) for the signed-in subject (the `KumunitaPrincipal.SubjectId(User)` idiom the `LocaleController` / `ProfileController` both use), stamps `Profile.OnboardingCompletedAt = now` (one owner-scope write, no audit row, never load-or-creates — the `KeyNotFoundException` catch redirects back to `/onboarding` with a model-state error, the `LocaleController.SaveTimezone` fail-closed idiom), and redirects home with a `onboarding.flash_done` flash (the `FlashAsync` idiom, §6). `[ValidateAntiForgeryToken]` is the `ProfileController.Edit` POST shape. |
| `Skip` | `POST /onboarding/skip` | `public async Task<IActionResult> Skip()` — the **one optional** write action (§1.a-C2): calls the **same** `IUserInfoService.CompleteOnboardingAsync(subjectId, actorBy)` (D2, §5) for the signed-in subject (the same stamp — the resident who skips is still "done" for banner-clearing purposes; the only user-visible difference is the `onboarding.skip` button label they clicked, not a distinct flash key), and redirects home with a `onboarding.flash_done` flash (the **same** flash key as `Finish` — the single flash key in the locked §8 set; a distinct "skipped" flash would be a 16th key, a U03 §8-set amendment, not a U00 lock). `[ValidateAntiForgeryToken]` is the `ProfileController.Edit` POST shape. |

**No per-step POST** (each step links into its owning surface — the §6 table),
**no persisted step cursor**, **no server session state** (D4). The only POSTs
are `/onboarding/finish` (required) and `/onboarding/skip` (optional, §1.a-C2).
Neither POST writes any field other than `Profile.OnboardingCompletedAt` (D2,
C-M22·3, C-M22·4).

**The `OnboardingViewModel`** (D5, C-M22·5):

```csharp
// in src/Kumunita.Web/Models/OnboardingViewModel.cs (U02's deliverable)

/// M22 (ADR 0132, D5) — the `/onboarding` page model. Owner-scope read;
/// no audience, no decision, no `AccessAudit` row (the `GetProfileAsync`
/// read re-projected, D2). The completion-state read is the
/// `OnboardingCompletedAt` null/non-null value; the step "set / not set"
/// hints are off the frozen read seams (the §6 table).
public sealed class OnboardingViewModel
{
    /// <summary>The completion stamp (`null` = not completed; the floor: the
    /// banner shows, the nav entry is present). D5, C-M22·5.</summary>
    public DateTimeOffset? OnboardingCompletedAt { get; init; }

    /// <summary>The banner-eligibility read (D5): <c>true</c> = the banner
    /// renders (the signed-in resident's `OnboardingCompletedAt` is `null`);
    /// <c>false</c> = the banner clears (non-null). Computed from the
    /// owner-scope read; never a claim (D6).</summary>
    public bool BannerEligible => OnboardingCompletedAt is null;

    // Step "set / not set" hints (D4, C-M22·5) — each is the non-null check
    // on the frozen read seam the step links into (the §6 table):
    public bool HasDisplayName { get; init; }        // Profile.DisplayName non-empty
    public bool HasAvatar { get; init; }              // Profile.AvatarId non-null (SetProfileAvatarAsync)
    public bool HasTimezone { get; init; }            // Profile.TimeZone non-null (SetProfileTimezoneAsync)
    public bool HasDateFormat { get; init; }          // Profile.DateFormat non-null (SetProfileDateFormatAsync)
    public bool HasEmailLanguage { get; init; }       // Profile.EmailLanguage non-null (SetProfileEmailLanguageAsync)
    public string? UiLanguageCode { get; init; }      // LocaleCookie.Read (the UI-language step; a cookie, not a Profile field)
}
```

**The banner-eligibility read (D5, C-M22·5):** the banner renders **iff**
`OnboardingViewModel.BannerEligible` is `true` (the owner-scope read of
`Profile.OnboardingCompletedAt` is `null`). The banner is rendered in the
shared layout / home page (the `Layout.cshtml` or the home `Index.cshtml` —
U03's choice; the `OnboardingViewModel` is the model, the banner is the view
markup). The banner links to `/onboarding` (the `onboarding.banner.text` +
`onboarding.banner.action` keys, §8). **Non-blocking** (D5, C-M22·5): sign-in
is never gated; the banner is always dismissible (a `data-dismiss`
attribute or a JS `dismiss` handler — the dismiss is a client-side state,
**not** a write to `OnboardingCompletedAt` — the only way to clear the banner
is the explicit finish/skip POST, D2/D4); the nav entry is present for any
`null`-completion resident (the nav entry is the `onboarding.banner.action`
key, §8, rendered in the `Layout.cshtml` nav when `BannerEligible` is `true`).

**The non-blocking posture (D5/D6, C-M22·5):** no auth gate on the banner
read (the banner is rendered for a signed-in resident whose
`OnboardingCompletedAt` is `null`; an unauthenticated request has no subject
and no banner — the `LocaleController.BuildModel` null-safe idiom, §6); no
claim read (the "is new" state is a profile-field read, never an identity
claim — the thin-token rule, ADR 0001-B, D6); no blocking wall (the banner is
a dismissible affordance, not a gate that blocks sign-in — D5).

## 8 — The `kw-l` key list (the closed `onboarding.*` set × en/de/fr/da)

The exact closed key set from the register (D7, §1.a-C1/C2 locked). The
**unit that renders the key** authors its `KnownTranslationKeys` entry in
**all four** languages — en/de/fr/da; the `KwLRegistryConsistencyTests` +
`KnownTranslationKeys_ParityTests` pin the closure. **U03 authors the full
M22 key set (the locked set below); U02 consumes its keys and adds none**
(the controller + view model reference the `onboarding.*` keys U03 authors —
the views are U03's):

| Key | Purpose (surface) |
| --- | --- |
| `onboarding.title` | Page title (the `/onboarding` heading) |
| `onboarding.intro` | Short intro paragraph under the title |
| `onboarding.step_displayname` | Step card: display name (+ label) |
| `onboarding.step_avatar` | Step card: avatar |
| `onboarding.step_language` | Step card: UI language |
| `onboarding.step_timezone` | Step card: time zone |
| `onboarding.step_dateformat` | Step card: date & time format |
| `onboarding.step_email` | Step card: email & notification language |
| `onboarding.step_contact` | Step card: contact details + visibility |
| `onboarding.visit` | The "Go to this setting" link label (each step card) |
| `onboarding.finish` | The "I'm all set — finish setup" button |
| `onboarding.skip` | The "Skip for now" button |
| `onboarding.flash_done` | "Setup complete" flash (the `/onboarding/finish` redirect) |
| `onboarding.banner.text` | The dismissible home/nav banner copy |
| `onboarding.banner.action` | The banner CTA link label ("Start setup") |

> **Reuse, don't invent:** the audience-editor labels, the "Save" submit, the
> profile-link targets (`/profile/edit`, `/profile`, `/settings/language`,
> `/settings/timezone`, `/settings/dateformat`), the flash-shape, and any
> settings-tab labels the ADR 0080 lane already ships are **not** re-keyed —
> M22 only adds the 15 keys above (the page title + intro + the seven step
> cards + the visit link + the finish/skip buttons + the flash + the banner
> text + the banner action). **U03 authors exactly the keys its surface
> renders, so its own parity test is green at its own exit; no unit consumes a
> key it has not authored.**
>
> **§1.a-C2 note:** if U03 later collapses the `Skip` action into `Finish`
> (§1.a-C2), the `onboarding.skip` key drops from this set (and the parity pin
> follows) — the *shape* (one required + optional action, same
> `CompleteOnboardingAsync` target) is the locked surface; only the action
> count is the amendment surface.

## 9 — §gate (the six acceptance tests)

The six acceptance tests from the register (GATE-1…GATE-6, verbatim), each
with the pin-test name U01–U03 will implement:

- **GATE-1 — One additive field, `null` = not completed.**
  `Profile.OnboardingCompletedAt` exists, is a nullable `DateTimeOffset?`,
  defaults to `null`, and is picked up by the existing delta-detected Marten
  boot (no new DocTypes surface / boot line / EF migration). *(C-M22·1, D1.)*
  — pin: `Profile_OnboardingCompletedAt_Is_Nullable_DateTimeOffset_Defaults_To_Null`.
- **GATE-2 — The completion stamp is one owner-scope write, no audit row, no
  load-or-create.** `CompleteOnboardingAsync` stamps **only**
  `OnboardingCompletedAt` = now, one `SaveChangesAsync`, **zero `AccessAudit`
  rows**, and throws `KeyNotFoundException` when no `Profile` exists.
  *(C-M22·3, D2.)* — pin:
  `CompleteOnboarding_Stamps_Only_CompletedAt_No_Audit_Row_Throws_KeyNotFound_When_No_Profile`.
- **GATE-3 — Zero new field-write lanes; the walk-through rides the frozen
  lanes.** The `/onboarding` page's only write is `CompleteOnboardingAsync`;
  the walk-through adds no new `IUserInfoService` write member beyond it (+
  the read seam); display name/contact/avatar/timezone/format/email-language
  all ride the frozen lanes (D3 table). *(C-M22·4, D3.)* — pin:
  `Onboarding_Adds_No_New_Field_Write_Lanes_Only_CompleteOnboardingAsync`.
- **GATE-4 — Zero new authorization surface.** The seam/claim pin passes: no
  new `AccessAction` / `Decide()` branch / `AccessVia` / `IAuthorizationService`
  method; `ClaimTypes.All` unchanged; no new `IAuditableResource` adapter, no
  new `*DocTypes` surface. *(C-M22·2, D6.)* — pin:
  `Onboarding_Adds_No_New_Authorization_Surface`.
- **GATE-5 — The banner is gated on the completion read and non-blocking.**
  The `OnboardingViewModel` exposes the completion state (the
  `OnboardingCompletedAt` null/non-null read); the banner renders iff `null`;
  sign-in is never gated; the nav entry is present for a `null`-completion
  resident. *(C-M22·5, D5.)* — pin:
  `Onboarding_Banner_Gated_On_Completion_Read_Non_Blocking_Nav_Entry_Present_For_Null`.
- **GATE-6 — The closed `onboarding.*` set is parity-pinned in four
  languages.** Every `onboarding.*` key is present, non-empty, in en/de/fr/da;
  the closure + parity pins hold. *(C-M22·6, D7.)* — pin:
  `Onboarding_KwL_Set_Is_Parity_Pinned_In_Four_Languages`.

## 10 — §drift-guard

**A unit stops (does not improvise) when it hits any of** (verbatim from the
register):

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A unit is about to **introduce a new `OnboardingState` document, a new
  `*DocTypes` surface, a new `Program.cs` `AddMarten` line, a new
  `IAuditableResource` adapter, a relational table, or an EF Core migration**
  — that violates C-M22·1 / D1; stop and report (the completion state is
  **one additive field on the existing `Profile` doc**, picked up by the
  existing delta-detected boot).
- A unit is about to **add a new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a claim-encoded
  "is new" flag** — that violates C-M22·2 / D6; stop and report (the whole
  point of M22 is that it *rides* the owner-scope lanes, it does **not**
  extend the seams).
- A unit is about to **write an `AccessAudit` row on the completion stamp**,
  **load-or-create** a `Profile`, or **write any field other than
  `OnboardingCompletedAt`** in the completion lane — that violates C-M22·3 /
  D2; stop and report (one owner-scope write, no audit row, no load-or-create,
  the `SetProfileTimezoneAsync` pin).
- A unit is about to **re-implement a field write lane** (display name / avatar
  / timezone / date format / email language / UI language) inside the
  walk-through, or add a **second `IUserInfoService` write member** — that
  violates C-M22·4 / D3; stop and report (the walk-through *links into* the
  frozen lanes; its only write is `CompleteOnboardingAsync`).
- A unit is about to **block sign-in, add a non-dismissible modal, or persist a
  per-step wizard cursor / server-session step state** — that violates D4 / D5 /
  C-M22·5; stop and report (one stateless page, one completion flag, a
  dismissible banner).
- A user-visible string that is **not** already in `KnownTranslationKeys` for
  all four languages is about to be rendered — add it to U03's closed set
  (§8) first; do not inline a string (the parity pin will fail, and that is
  the point).
- A unit other than **U04** is about to touch `Milestones.cs` or
  `MilestonesTests.cs` — stop; only the close-flip owns the roadmap (M22 has
  **one** flip — the close).
- A unit is about to **stage an email / notification** on finishing onboarding
  — that is a D8 future lane, not M22; stop and report.
- A unit is about to **close M22 by promoting M23** — M23 is **already done**;
  the close promotes **M24** (`StatusPlanned` → `StatusNext`); stop and report
  (M22's close flips M22 → done, M24 → next, the order unchanged).

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not
re-derive what an earlier unit already settled (a D# amendment, the key set,
the `OnboardingCompletedAt` shape, the owner-scope lane signature, the
banner-eligibility read).

**The register is the map, not the code.** If a unit is tempted to "just add
a new OnboardingState doc" or "gate sign-in behind onboarding" or "encode 'is
new' in a claim" or "re-implement the avatar write inside the walk-through,"
that is the §drift-guard firing — M22's value is precisely that it does *not*:
it adds one additive `Profile` field + one owner-scope completion lane, rides
the frozen field-write lanes it links into, gates a dismissible banner on the
completion read, and localizes everything through the closed `kw-l` set — with
**zero new authorization surface**.

## 11 — §deferred (the D8 lanes)

The five deferred lanes (each a future ADR, listed so a unit does not reach
for them — D8, verbatim):

1. **Per-step progress / resume-across-sessions** — M22's page is stateless
   (D4); a persisted per-step cursor + "pick up where you left off" is a
   future lane. *Why deferred:* the stateless single-page shape is the locked
   D4 surface; a persisted step cursor is a new data surface (a future ADR).
2. **"You haven't set X" detection** — M22 links into the existing surfaces
   and shows generic "set / not set" hints off the frozen read seams; an
   automatic "you're missing an avatar / timezone" recommendation engine is a
   future lane. *Why deferred:* the "set / not set" hints are the
   `OnboardingViewModel`'s non-null checks (D5); an automatic recommendation
   engine is a larger design (a future ADR).
3. **Onboarding for elevated/guest accounts** — the Guardian (M28) / Guest
   (M19) flows are out of scope; M22 onboards a *resident's* account setup.
   *Why deferred:* the Guardian / Guest lanes are their own ADRs (M28 / M19);
   M22 is the resident's account setup, not a cross-standing onboarding flow.
4. **An admin "onboarding completion rate" metric** — an operator analytics
   surface (the M13 logging lane); a future lane. M22 ships **no** admin view.
   *Why deferred:* the M13 logging lane is its own surface; a completion-rate
   metric is an operator analytics design (a future ADR).
5. **I18n beyond the four-language floor** — the en/de/fr/da parity pin is the
   M22 floor; additional catalog languages are the M9/M15 lanes (M22 does not
   seed new languages, only the four). *Why deferred:* the M9/M15 lanes own
   the catalog-language surface; M22 is the four-language floor, not a new
   language seed.
