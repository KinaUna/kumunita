---
id: adr-0132
title: "Onboarding (a guided, non-blocking walk-through of a new resident's account setup, with a completion record + a dismissible banner)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m22-onboarding-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR 0132 — M22 Onboarding — a guided, non-blocking, localized walk-through + a completion record + a dismissible banner, with zero new authorization surface

Status: Accepted | Date: 2026-09-22

> **Abstract:** M22 ships a **guided, non-blocking, localized walk-through**
> + a **completion record** + a **dismissible banner**, with **zero new
> authorization surface** (the C-M22·2 pin). It rides the frozen seams:
> the ADR 0001-B owner-scope read/write, the ADR 0015 closed
> `KnownTranslationKeys` set, the ADR 0061 per-recipient language
> resolution — **no** new `AccessAction`, **no** `Decide()` branch,
> **no** `AccessVia`, **no** `IAuthorizationService` method. The
> completion state is **one additive `Profile` field**
> (`OnboardingCompletedAt`, nullable `DateTimeOffset?`, `null` = the
> floor = banner shows). The walk-through is a **single stateless
> `/onboarding` page** (the `LocaleController` settings-tabs shape) with
> **one required** `POST /onboarding/finish` action (calls
> `CompleteOnboardingAsync`) + an **optional** `POST /onboarding/skip`
> (routes to the **same** call). The banner is **non-blocking**: sign-in
> is **never** gated. **For** a developer touching
> `Kumunita.Core/UserInfo/`, `Kumunita.Web/Controllers/Onboarding*`,
> `Kumunita.Web/Views/Onboarding/`, or the `tests/*` onboarding families.
> The closed key set, the deferred lanes, and the acceptance tests are in
> the [handoff note](../design/adr-handoffs.md#adr-0132--onboarding-moved-detail).

## Context

The README Roadmap's M22 row names the M22 surface. M22 is the
resident's **account setup** walk-through — **not** an onboarding
*system* (no per-step progress, no "you're missing X" detection, no
elevated/guest onboarding, no admin completion-rate metric, no new
languages — each a follow-on lane, D8). The *natural* shape is to ride
the frozen seams (the ADR 0001-B owner-scope read/write, the ADR 0015
closed `KnownTranslationKeys` set) and add **one additive `Profile`
field** + **one owner-scope write seam** + **one controller** + **one
view/banner** + **one closed `kw-l` set** — **not** to invent a new
authorization surface, a new bounded context, or a new document.

## Decision

- **D1 — The completion state is one additive `Profile` field.**
  `Profile.OnboardingCompletedAt` is a nullable `DateTimeOffset?`;
  `null` = the floor (banner shows). A test asserts the member exists on
  `Profile`, is nullable, and the default/new profile reads `null`.
- **D2 — The completion stamp is a single owner-scope write lane.**
  `CompleteOnboardingAsync` stamps **only** `OnboardingCompletedAt` =
  now, one `SaveChangesAsync`, **zero `AccessAudit` rows**, **never
  load-or-creates** (`KeyNotFoundException` when no profile, the
  `SetProfileTimezoneAsync` pin). The walk-through's **only** new write
  is `CompleteOnboardingAsync`.
- **D3 — The walk-through rides the frozen lanes.** Every field the
  walk-through surfaces (display name, avatar, UI language, time zone,
  date format, email language) delegates to the **frozen lane that owns
  it** — the walk-through **re-implements none of them**. *Forbids:*
  re-implementing any of the frozen lanes, a walk-through POST that
  writes `Profile.TimeZone` / `Profile.DateFormat` / `Profile.AvatarId`
  directly, or a walk-through that re-derives an audience.
- **D4 — The walk-through is a single stateless `/onboarding` page.**
  One `OnboardingController`: `GET /onboarding` (renders the step
  sequence + the current completion state + the finish/skip action) and
  `POST /onboarding/finish` (the **one required** write action). An
  **optional** `POST /onboarding/skip` routes to the **same**
  `CompleteOnboardingAsync` call. **No per-step POST**, **no persisted
  step cursor**, **no server session state**.
- **D5 — The banner is the affordance, shown only while
  `OnboardingCompletedAt` is `null`, and non-blocking.** A dismissible
  **home/nav banner** is rendered when, and only when, the signed-in
  resident's `OnboardingCompletedAt` is `null`. It links to
  `/onboarding`. **Sign-in is never gated**, the banner is always
  dismissible, and the nav entry is present for any `null`-completion
  resident. *Forbids:* blocking sign-in, a modal that can't be dismissed,
  a **claim-encoded** "is new" flag, or a banner computed from anything
  other than the `OnboardingCompletedAt` read.
- **D6 — Zero new authorization surface (the C-M22·2 pin).** M22 adds
  **no** `AccessAction`, **no** `Decide()` branch, **no** `AccessVia`,
  **no** `IAuthorizationService` method; the claim set
  (`ClaimTypes.All`) is **unchanged**. It adds exactly **one additive
  `Profile` field** (D1) + **two owner-scope seams** (D2) + **one
  controller** (D4) + **one view + banner** (D5) + **one closed `kw-l`
  set** (D7). The walk-through is **owner-scope only**.
- **D7 — A closed `onboarding.*` kw-l key set × en/de/fr/da.** Every new
  user-visible string M22 introduces is a `KnownTranslationKeys` entry
  present, non-empty, in all four languages (en/de/fr/da), pinned by
  `KwLRegistryConsistencyTests` + `KnownTranslationKeys_ParityTests`.
  See the handoff note for the 15-key locked set. *Forbids:* a
  user-visible string rendered from M22 that is **not** in
  `KnownTranslationKeys` for all four languages.
- **D8 — Deferred lanes (each a future ADR, not part of M22).** (1)
  Per-step progress / resume-across-sessions; (2) "you haven't set X"
  detection; (3) onboarding for elevated/guest accounts; (4) an admin
  "onboarding completion rate" metric; (5) I18n beyond the four-language
  floor. See the handoff note for the full deferred-lane catalog.

## Consequences

- **C-M22·1** — The completion state is one additive field on the
  existing `Profile` doc, `null` = not completed (D1).
- **C-M22·2** — Zero new authorization surface (D6). **No** new
  `AccessAction` / `Decide()` branch / `AccessVia` /
  `IAuthorizationService` method; `ClaimTypes.All` unchanged. M22 adds
  **one additive `Profile` field** + **two owner-scope seams** + **one
  controller** + **one view/banner** + **one closed `kw-l` set**, and
  **no adapter, no new DocTypes surface, no new boot line**.
- **C-M22·3** — The completion stamp is a single owner-scope write lane,
  no audit row (D2). `CompleteOnboardingAsync` stamps **only**
  `OnboardingCompletedAt` = now, one `SaveChangesAsync`, **zero
  `AccessAudit` rows**, **never load-or-creates**.
- **C-M22·4** — The walk-through rides the frozen lanes, zero new
  field-write lanes (D3). The `/onboarding` page's **only** write is
  `CompleteOnboardingAsync`; every field it surfaces delegates to the
  frozen lane that owns it.
- **C-M22·5** — The banner is gated on the `OnboardingCompletedAt` read
  and is non-blocking (D5). The banner renders **iff** the owner-scope
  read is `null`; it never blocks sign-in.
- **C-M22·6** — The closed `onboarding.*` set is parity-pinned in four
  languages (D7). Every `onboarding.*` key is present, non-empty, in
  en/de/fr/da.
- **FACES (F1–F5) + the named trade.** F1 — a fresh resident is guided
  through account setup. F2 — a resident finishes (or skips) and the
  banner clears. F3 — every step lands in the surface that already owns
  the field. F4 — onboarding is non-blocking and claim-free. F5 — the
  walk-through is localized in the resident's language. **The named
  trade:** M22 buys *a guided, non-blocking, localized walk-through + a
  completion record + a dismissible banner* with **zero new
  authorization surface + one additive `Profile` field + one owner-scope
  write lane + one controller + one view/banner + a closed `kw-l` set**,
  in exchange for **no per-step progress tracking, no "you're missing X"
  detection, no elevated/guest onboarding, no admin completion-rate
  metric, and no new languages** (D8) — M22 is *the guided shell + the
  completion flag + the banner*, **not** an onboarding *system*.
- **The acceptance tests (GATE-1…GATE-6).** See the handoff note for the
  full GATE-1…GATE-6 list.
- **The deferred lanes (D8).** See the handoff note for the full
  deferred-lane catalog.
