# M22 — Onboarding (the register)

> **This file is the register** — the milestone map, the locked [PROPOSED]
> decision set, the invariants, the FACES, the unit map, the workflow, the
> atomicity contract, and the §drift-guard. It is read first by **every** unit
> agent (U00–U04) before anything else. It is **not** a per-unit plan: each
> unit has its own self-contained file (`m22-uNN.md`) that a ~32K-context agent
> can execute without re-deriving this file.
>
> **M22 is the current in-progress milestone.** **M22 is *already* `StatusNext`
> in `Milestones.All`** — M23's close-flip (its U06) promoted it — and the live
> pin is `MilestonesTests.M22_Is_The_Single_InProgress_Milestone`. So M22 has
> **no open-flip unit**; its **single** flip is **U04 (the close)**:
> `StatusNext` → `StatusDone` (M22), **M24 promoted** `StatusPlanned` →
> `StatusNext`, the order **unchanged** (`…"M20","M21","M23","M22","M24"`,
> the ADR 013/089/093/109 "named lane, not a renumber" precedent), the pin
> re-pinned to `M24_Is_The_Single_InProgress_Milestone`. **No unit before U04
> touches `Milestones.cs` or `MilestonesTests`** (the roadmap already reads
> "M22 in progress" — it stays that way until M22 ships).

## Tiering (three documents, like M18–M21)

| Tier | File | Who owns it | Lifetime |
| --- | --- | --- | --- |
| 1 — Register (this file) | `in-progress/plan-m22-onboarding.md` | U00 (author) + all units (read) | moves to `done/m22/` at U04 |
| 2 — Per-unit plans | `in-progress/m22-uNN.md` | the `U##` agent | moves to `done/m22/` when the unit is done |
| 3 — Rolling handoff notes | `in-progress/m22-onboarding-handoff-notes.md` | every agent appends its `## U##` section | moves to `done/m22/` at U04 |

The handoff-notes file is created by **U00** (at runtime, not by this authoring
pass). Every unit appends a short `## U##` section before it moves its own plan
to `done/`.

> **`done/` layout note (resolve at U04, do not guess):** the register/m21
> text says "flat under `done/`", but the **actual** tree uses a **per-milestone
> subfolder** — `done/m21/`, `done/m23/`, `done/m3/` each hold their own
> `plan-…`, `m22-uNN.md`, and `-handoff-notes.md`. **Follow the real tree:
> close moves M22's artifacts into `done/m22/`** (consistent with `done/m23/`,
> which M23's own close just produced). If a future lane diverges, the subfolder
> is the dominant real convention.

## Atomicity contract (sized for ~32K context)

Each unit is one self-contained step a fresh agent can complete and hand off:

- **Entry reads:** 4–8 files, named in the unit plan (with a one-line "why"
  each).
- **Deliverables:** ≤ 7 small files, named exactly (paths +, where locked, the
  exact C# / keys the design doc pins).
- **Exit gate:** **one** `dotnet build Kumunita.slnx -c Debug` **plus one**
  `dotnet exec` test assembly — either
  `tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll` **or**
  `tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`. Both
  must be green.
- **A unit that needs *both* test assemblies is too big — split it.**
  (Core units exit on Core.Tests; Web units exit on Web.Tests. The close-flip
  unit — U04 — exits on Web.Tests, because it flips `Milestones.cs`, which is
  pinned by `MilestonesTests` in `Kumunita.Web.Tests`.)
- **No build/test exit for U00** — it is docs-only (design doc + ADR + index
  row). Its exit is "the files exist and the [PROPOSED] set is locked or
  amended in-unit."

> **Test-runner quirk (do not retry the broken paths):** `dotnet test` and VS
> Test Explorer discovery go wrong on this machine (xunit.v3 bridge). The
> reliable exit is the `dotnet build` + `dotnet exec … .dll` pair above.
> `Kumunita.Core.Tests` starts `postgres:18` via Testcontainers (~20 s) and
> leaves Docker containers behind if killed — `docker container prune` to clean
> up.

## Unit map

| Unit | Title | Track | Exit test assembly |
| --- | --- | --- | --- |
| U00 | Design doc + ADR 0132 (the sign-off gate) | docs-only | none (no build/test) |
| U01 | Additive `Profile.OnboardingCompletedAt` field + the owner-scope `CompleteOnboardingAsync` lane + the read seam + `UserInfoService` impl + DI | Core | `Kumunita.Core.Tests` |
| U02 | `OnboardingController` (`GET /onboarding` checklist + `POST /onboarding/finish` stamping completion) + the `OnboardingViewModel` (banner-eligibility read) | Web | `Kumunita.Web.Tests` |
| U03 | `Views/Onboarding/*` + the home/nav **banner** (shown only while not completed) + **the closed `onboarding.*` kw-l key set** × en/de/fr/da (author) | Web | `Kumunita.Web.Tests` |
| U04 | **Close M22** — flip M22 `StatusDone`, promote M24 to `StatusNext`, re-pin `MilestonesTests`, README/STATUS/ARCHITECTURE parity, ADR 0132 → `Accepted`, `done/m22/` move | Web + docs | `Kumunita.Web.Tests` |

## Understanding (what M22 is)

**M22 = onboarding.** The roadmap line is the scope:
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

Everything else about M22 is *what it is not* — see "What M22 is NOT" below and
the §deferred lanes (D8).

## What M22 is NOT

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

## Decisions (D1–D8) — [PROPOSED], locked or amended by U00

> Each D# carries a `*Forbids:*` tail — the anti-pattern it rules out. U00
> locks these verbatim in the design doc, or amends them **in U00** (recorded
> in the design doc's §1.a + the handoff notes). A later unit may not amend a
> D#; if a D# is wrong once the code starts, that is a §drift-guard stop.

### D1 — The completion state is **one additive field on the existing `Profile` doc** (ADR 0004 §B.1)

`Kumunita.Core.UserInfo.Profile` gains exactly **one** additive member
(locked by U00's design doc):

```csharp
/// <summary>
/// M22 (ADR 0132, D1) — the onboarding completion stamp. <c>null</c> = the
/// resident has not finished the guided walk-through (the floor: the banner
/// shows, the nav entry is present). A non-null value = finished/skipped; the
/// banner clears. Written only by the owner-scope
/// <see cref="IUserInfoService.CompleteOnboardingAsync"/> lane (D2); read
/// through the existing <see cref="IUserInfoService.GetProfileAsync"/> read
/// (never a claim, D6). Additive per ADR 0004 §B.1: delta-detected,
/// idempotent, no re-seed, no EF migration.
/// </summary>
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

> **U00 may amend D1's exact member** in U00 (e.g. `OnboardingCompletedAt`
> vs `OnboardingCompleted` bool + `OnboardingCompletedAt`). The *shape* —
> **one additive nullable member on the existing `Profile` doc, `null` = not
> completed, read through `GetProfileAsync`, written by one owner-scope
> lane** — is locked; only the member *name/type* is the amendment surface.

### D2 — Two owner-scope seams on `IUserInfoService` (the ADR 0006-E compatible-addition idiom, the `SetProfileTimezoneAsync` shape verbatim)

`IUserInfoService` gains exactly **two** members (the `SetProfileTimezoneAsync`
/ `SetProfileDateFormatAsync` / `SetProfileEmailLanguageAsync` shape verbatim —
owner-scope, "not an access decision"):

```csharp
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

*Forbids:* an **audited** write row for a profile-field write (a completion
stamp is not an access decision), a Core seam that **re-derives standing/roles**
(the actor is the signed-in owner, owner-scope), **load-or-create** (the
`SetProfileTimezoneAsync` "never load-or-creates" pin — throw
`KeyNotFoundException`), or a write that touches **any field other than**
`OnboardingCompletedAt` (the single-write-lane pin — display name / avatar /
timezone / format / email-language stay their **own** frozen lanes, D3).

### D3 — The walk-through **rides the frozen lanes** for every field it surfaces — **zero new field-write lanes**

The walk-through's step cards each delegate to the **existing** lane that owns
that field (D3; the frozen seams U00 locks verbatim in the design doc's "Seams
& contracts" section):

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

### D4 — The walk-through is a **single stateless `/onboarding` page** (the `LocaleController` settings-tabs shape)

One `OnboardingController` (D4; the `LocaleController` linkable-sections shape):

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
same `CompleteOnboardingAsync` call — U00 may collapse skip→finish in U00).

*Forbids:* a per-step URL with a persisted step counter, a multi-document
wizard, server-session step state, or a POST that writes any field other than
the completion flag (D2/D3).

### D5 — The banner is the **affordance**, shown **only while `OnboardingCompletedAt` is `null`**, and **non-blocking**

A dismissible **home/nav banner** (D5) is rendered in the shared layout /
home page when, and only when, the signed-in resident's
`OnboardingCompletedAt` is `null` (the floor = a fresh resident sees it). The
banner links to `/onboarding`. It is **non-blocking**: **sign-in is never
gated**, the banner is always dismissible, and the nav entry is present for any
`null`-completion resident. The "is a new user?" check is the **owner-scope
read** of the profile field (D2), **never** a claim (the thin-token rule,
ADR 0001-B) and **never** a per-request flag recomputed from scratch.

*Forbids:* blocking sign-in, a modal that can't be dismissed, a **claim-encoded**
"is new" flag, or a banner computed from anything other than the
`OnboardingCompletedAt` read (D5/D6).

### D6 — **Zero new authorization surface** (the C-M22·2 pin)

M22 adds **no** `AccessAction`, **no** `Decide()` branch, **no** `AccessVia`,
**no** `IAuthorizationService` method; the claim set (`ClaimTypes.All`) is
**unchanged**. It adds exactly **one additive `Profile` field** (D1) +
**two owner-scope seams** (D2) + **one controller** (D4) + **one view + banner**
(D5) + **one closed `kw-l` set** (D7). The walk-through is **owner-scope
only**: the signed-in resident reads/writes *their own* profile; there is no
cross-resident surface.

*Forbids:* any extension of the frozen authorization seams, a claim-encoded
onboarding state, a new standing/`AccessVia`, or a cross-resident onboarding
read (the whole point of M22 is that it *rides* the owner-scope lanes, it does
**not** extend them).

### D7 — A **closed `onboarding.*` kw-l key set** × en/de/fr/da, authored by U03

Every new user-visible string M22 introduces is a `KnownTranslationKeys` entry
present, **non-empty, in all four** languages (en/de/fr/da), pinned by
`KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`. The
**proposed closed set** (U00 may amend the exact set/wording in U00; the
*discipline* — closed, four-language, parity-pinned — is locked) is the
"closed `kw-l` key set" table below.

*Forbids:* a user-visible string rendered from `M22` that is **not** in
`KnownTranslationKeys` for all four languages (inline it and the parity pin
fails — that is the point).

### D8 — **Deferred lanes** (each a future ADR, not part of M22)

1. **Per-step progress / resume-across-sessions** — M22's page is stateless (D4);
   a persisted per-step cursor + "pick up where you left off" is a future lane.
2. **"You haven't set X" detection** — M22 links into the existing surfaces and
   shows generic "set / not set" hints off the frozen read seams; an automatic
   "you're missing an avatar / timezone" recommendation engine is a future lane.
3. **Onboarding for elevated/guest accounts** — the Guardian (M28) / Guest (M19)
   flows are out of scope; M22 onboards a *resident's* account setup.
4. **An admin "onboarding completion rate" metric** — an operator analytics
   surface (the M13 logging lane); a future lane. M22 ships **no** admin view.
5. **I18n beyond the four-language floor** — the en/de/fr/da parity pin is the
   M22 floor; additional catalog languages are the M9/M15 lanes (M22 does not
   seed new languages, only the four).

## Invariants (C-M22·1 … C-M22·6)

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

## FACES (F1–F5) + the named trade

- **F1 — A fresh resident is guided through account setup** (C-M22·1, C-M22·5,
  D4, D5): the `/onboarding` page walks display name → avatar → language →
  timezone → date format → email language → contact, each step riding the
  frozen lane that owns it; the banner invites them.
- **F2 — A resident finishes (or skips) and the banner clears** (C-M22·3, D2):
  the finish/skip action stamps `OnboardingCompletedAt` = now, one owner-scope
  write, no audit row; the banner + nav entry clear on the next read.
- **F3 — Every step lands in the surface that already owns the field** (C-M22·4,
  D3): the walk-through never re-implements a write lane — display name/contact
  go to `/profile/edit`, avatar to the avatar upload, language/timezone/format/
  email-language to `/settings/*` — so M22 adds zero new field-write surface.
- **F4 — Onboarding is non-blocking and claim-free** (C-M22·2, C-M22·5, D5,
  D6): sign-in is never gated; the "is new" state is a profile-field read, not a
  claim; the banner is always dismissible.
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

## §gate (acceptance tests, named — U00 locks them in the design doc)

- **GATE-1 — One additive field, `null` = not completed.**
  `Profile.OnboardingCompletedAt` exists, is a nullable `DateTimeOffset?`,
  defaults to `null`, and is picked up by the existing delta-detected Marten
  boot (no new DocTypes surface / boot line / EF migration). *(C-M22·1, D1.)*
- **GATE-2 — The completion stamp is one owner-scope write, no audit row, no
  load-or-create.** `CompleteOnboardingAsync` stamps **only**
  `OnboardingCompletedAt` = now, one `SaveChangesAsync`, **zero `AccessAudit`
  rows**, and throws `KeyNotFoundException` when no `Profile` exists.
  *(C-M22·3, D2.)*
- **GATE-3 — Zero new field-write lanes; the walk-through rides the frozen
  lanes.** The `/onboarding` page's only write is `CompleteOnboardingAsync`;
  the walk-through adds no new `IUserInfoService` write member beyond it (+
  the read seam); display name/contact/avatar/timezone/format/email-language
  all ride the frozen lanes (D3 table). *(C-M22·4, D3.)*
- **GATE-4 — Zero new authorization surface.** The seam/claim pin passes: no
  new `AccessAction` / `Decide()` branch / `AccessVia` / `IAuthorizationService`
  method; `ClaimTypes.All` unchanged; no new `IAuditableResource` adapter, no
  new `*DocTypes` surface. *(C-M22·2, D6.)*
- **GATE-5 — The banner is gated on the completion read and non-blocking.**
  The `OnboardingViewModel` exposes the completion state (the
  `OnboardingCompletedAt` null/non-null read); the banner renders iff `null`;
  sign-in is never gated; the nav entry is present for a `null`-completion
  resident. *(C-M22·5, D5.)*
- **GATE-6 — The closed `onboarding.*` set is parity-pinned in four
  languages.** Every `onboarding.*` key is present, non-empty, in en/de/fr/da;
  the closure + parity pins hold. *(C-M22·6, D7.)*

## Workflow (every unit, 7 steps)

1. **Read this register** (the Understanding, the [PROPOSED] D# set, the
   invariants, the FACES, the §gate, the §drift-guard, the closed `kw-l` key
   set).
2. **Read the unit plan** (`m22-uNN.md`) — the Goal, the Entry reads, the
   Deliverables, the Exit.
3. **Read the Entry reads** named in the unit plan (4–8 files, the design-doc
   sections the unit implements, the ADR, the code seams it touches).
4. **Execute** the Deliverables (≤ 7 files; implement the locked C# / keys
   exactly; do not amend a D#).
5. **Run the Exit gate** — one `dotnet build Kumunita.slnx -c Debug` + one
   `dotnet exec` test assembly, both green (U00: no build/test).
6. **Append a `## U##` section** to the handoff notes (5 lines: what was
   delivered, any open question, the next unit's entry point, no new drift).
7. **Move the unit plan** `in-progress/m22-uNN.md` → `done/m22/m22-uNN.md`
   (the **subfolder** convention, matching the real `done/m21/`, `done/m23/`
   tree — see the §"done/ layout note").

## Unit-series rules

- **Order is U00 → U04.** U00 (docs + ADR) must land before any code unit,
  because the design doc's "Seams & contracts (Part 2)" section pins the exact
  C# U01–U03 implement. M22 is **already** `StatusNext` (M23's close opened it),
  so **there is no open-flip unit** — U01 goes straight to the first code unit.
  A later unit may read an earlier unit's code, but never re-derive a D#.
- **One write lane per unit.** U01 owns the additive `Profile` field + the two
  owner-scope seams + the `UserInfoService` impl + DI. U02 owns the
  `OnboardingController` + the `OnboardingViewModel`. U03 owns the
  `Views/Onboarding/*` + the banner + the **full** `kw-l` set (author). No unit
  both *reads* and *writes* a seam another unit owns (U02 *calls* U01's
  `CompleteOnboardingAsync`; it never writes `Profile` itself).
- **One test-assembly exit per unit.** A Core unit (U01) exits on Core.Tests; a
  Web unit (U02, U03) exits on Web.Tests. The close-flip unit (U04) exits on
  Web.Tests (it flips `Milestones.cs`, pinned by `MilestonesTests`). If a unit
  needs both, it is too big — split it (see the Atomicity contract).
- **The close flip is bracketed by U04 ONLY, and ONLY it touches
  `Milestones.cs` / `MilestonesTests`.** U04: M22 `StatusNext` → `StatusDone` +
  **promote M24** `StatusPlanned` → `StatusNext` (the **order unchanged** —
  `…"M20","M21","M23","M22","M24"`) + append `"M22"` to the
  `Shipped_Milestones_Are_Marked_Done` done-list + **replace**
  `M22_Is_The_Single_InProgress_Milestone` with
  `M24_Is_The_Single_InProgress_Milestone` + README/STATUS/ARCHITECTURE parity +
  tag the ADR 0132 index row `**Done** (M22)` + flip ADR 0132 → `Accepted` +
  move all M22 artifacts to `done/m22/`. **No other unit touches the roadmap.**
- **`kw-l` parity is authored in U03 and consumed by U02 + U03.** Every new
  user-visible string M22 introduces is a `KnownTranslationKeys` entry present,
  non-empty, in **all four** languages (en/de/fr/da); the
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests` pin the
  closure. **U03 authors the COMPLETE M22 key set (the locked set below)**; U02
  *consumes* its keys and **adds none** (U02 ships the controller + view model
  referencing the `onboarding.*` keys U03 authors — the views are U03's).

### The closed `kw-l` key set (D7) — [PROPOSED] locked by U00, authored by U03

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

> **U00 may amend the exact key set / wording in U00** (e.g. collapse the two
> banner keys, add a per-step "set / not set" hint key). The *discipline* —
> closed, four-language (en/de/fr/da), parity-pinned — is locked (D7).

## §drift-guard

**A unit stops (does not improvise) when it hits any of:**

- A D# it needs is not in the [PROPOSED] set, or two D#s contradict — **stop,
  report to the user**; do not pick one silently. (A D# is locked by U00; a
  post-U00 D# change is a design-doc §1.a amendment + a handoff-note line, and
  only U00 makes it.)
- A unit needs *both* test assemblies green to exit — **it is too big**; split
  it per the Atomicity contract rather than running both.
- A unit is about to **introduce a new `OnboardingState` document, a new
  `*DocTypes` surface, a new `Program.cs` `AddMarten` line, a new
  `IAuditableResource` adapter, a relational table, or an EF Core migration** —
  that violates C-M22·1 / D1; stop and report (the completion state is **one
  additive field on the existing `Profile` doc**, picked up by the existing
  delta-detected boot).
- A unit is about to **add a new `IAuthorizationService` signature, a new
  `Decide()` branch, a new `AccessVia`/`AccessAction`, or a claim-encoded
  "is new" flag** — that violates C-M22·2 / D6; stop and report (the whole point
  of M22 is that it *rides* the owner-scope lanes, it does **not** extend the
  seams).
- A unit is about to **write an `AccessAudit` row on the completion stamp**,
  **load-or-create** a `Profile`, or **write any field other than
  `OnboardingCompletedAt`** in the completion lane — that violates C-M22·3 / D2;
  stop and report (one owner-scope write, no audit row, no load-or-create, the
  `SetProfileTimezoneAsync` pin).
- A unit is about to **re-implement a field write lane** (display name / avatar
  / timezone / date format / email language / UI language) inside the
  walk-through, or add a **second `IUserInfoService` write member** — that
  violates C-M22·4 / D3; stop and report (the walk-through *links into* the
  frozen lanes; its only write is `CompleteOnboardingAsync`).
- A unit is about to **block sign-in, add a non-dismissible modal, or persist a
  per-step wizard cursor / server-session step state** — that violates D4 / D5 /
  C-M22·5; stop and report (one stateless page, one completion flag, a
  dismissible banner).
- A user-visible string that is **not** already in `KnownTranslationKeys` for all
  four languages is about to be rendered — add it to U03's closed set first; do
  not inline a string (the parity pin will fail, and that is the point).
- A unit other than **U04** is about to touch `Milestones.cs` or
  `MilestonesTests.cs` — stop; only the close-flip owns the roadmap (M22 has
  **one** flip — the close).
- A unit is about to **stage an email / notification** on finishing onboarding —
  that is a D8 future lane, not M22; stop and report.
- A unit is about to **close M22 by promoting M23** — M23 is **already done**;
  the close promotes **M24** (`StatusPlanned` → `StatusNext`); stop and report
  (M22's close flips M22 → done, M24 → next, the order unchanged).

**The handoff-notes file is the cross-unit memory.** Every unit reads the
`## U##` sections before it and appends its own after; a unit does not re-derive
what an earlier unit already settled (a D# amendment, the key set, the
`OnboardingCompletedAt` shape, the owner-scope lane signature, the banner-
eligibility read).

**The register is the map, not the code.** If a unit is tempted to "just add a
new OnboardingState doc" or "gate sign-in behind onboarding" or "encode 'is
new' in a claim" or "re-implement the avatar write inside the walk-through,"
that is the §drift-guard firing — M22's value is precisely that it does *not*:
it adds one additive `Profile` field + one owner-scope completion lane, rides
the frozen field-write lanes it links into, gates a dismissible banner on the
completion read, and localizes everything through the closed `kw-l` set — with
**zero new authorization surface**.
