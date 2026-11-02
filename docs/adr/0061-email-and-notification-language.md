---
id: adr-0061
title: "Email & notification language: per-resident outbound-channel language"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m22-onboarding-design.md]
supersedes: []
superseded_by: []
updated: 2026-11-02
---

# ADR [0061](0061-email-and-notification-language.md) — Email & notification language: per-resident outbound-channel language

Status: Accepted
Date: 2026-09-14

## Context

The platform's **UI** is already multilingual (ADR [0005](0005-multilingual-support.md) / 0015): the language a
resident is browsing in is resolved per request and the server-rendered markup is
localized through the `<kw-l>` TagHelper and the `ITranslationProvider` floor.
But the **outbound channels** — the emails the platform sends — were hardcoded
English:

- the **verification email** staged at signup / resend (`IdentityService`), and
- the **event-reminder email** staged by the recurring `EventReminders` job
  (`EventReminderService`).

A resident who has the whole site in German (or French, or Danish) would still
receive their account-verification and event-reminder emails in English, and had
no way to change that. The natural question — "are emails translated?" — the
answer was **no**, and there was no setting to ask for them to be.

This is the natural fourth member of the "instance default + user override" trio
(ADR [0015](0015-ui-view-localization-mechanics.md) language / ADR [0019](0019-timezone-platform-default-and-resident-override.md) timezone / ADR [0020](0020-date-format-platform-default-and-resident-override.md) date format), applied to the
**outbound channel language** rather than the browsing language:

- A **per-resident override** lets an individual choose which language their
  emails (and, later, notifications) arrive in — independent of what language the
  UI is currently rendered in. A resident may keep the UI in English but want
  reminders in German, or vice versa.
- A **platform default** (the same `LocaleSettings.DefaultLanguageCode` ADR [0005](0005-multilingual-support.md)
  already uses) is the fallback for residents who do not pick a value.
- An **`en` floor** guarantees the system never sends a blank or unresolvable
  message — the exact floor ADR [0005](0005-multilingual-support.md) uses for language.

This is deliberately **not** a re-use of the browsing language (ADR [0015](0015-ui-view-localization-mechanics.md)). The
browsing language is request-scoped ("what language is this page showing in
right now"); the outbound-channel language is a **persistent profile fact**
("what language do I want things *sent* to me in"). Conflating the two would
mean a resident's emails silently change language every time they browse in a
different language — surprising, and wrong for a background job that has no
request principal at all (the event reminder is a scheduled tick, exactly the
case ADR [0019](0019-timezone-platform-default-and-resident-override.md) / 0020 already special-case to "the recipient's `Profile` is the
actor here").

## Decision

- **Store the language code on the resident's profile — a `string?`, not a
  preset id.** `Kumunita.Core.UserInfo.Profile.EmailLanguage` (`string?`) — a
  BCP-47 code, the same shape as the language ids the catalog already carries.
  `null` means "no override; use the platform default." **Additive** (ADR [0004](0004-data-persistence-and-schema-evolution.md)
  §B.1) — Marten applies the added `string` column automatically; no migration
  file, exactly the shape of the `Profile.TimeZone` / `Profile.DateFormat`
  additions.

- **The resolution order is the ADR [0005](0005-multilingual-support.md) floor, scoped to the resident:**
  `Profile.EmailLanguage` → `LocaleSettings.DefaultLanguageCode` → the
  `en` registry floor. This is resolved *inside* `ITranslationProvider.GetAsync`
  (its existing `preferredLanguageCode` → `DefaultLanguageCode` → `en` chain),
  so the callers pass only the resident's code (or `null`) and the provider does
  the rest. No new resolution path is invented.

- **It governs the outbound channels that exist today** — the verification
  email and the event-reminder email. **Notifications do not exist yet** (they
  are a later milestone); when they land they read the *same*
  `Profile.EmailLanguage` field, so this setting is future-proof by construction.
  The ADR therefore calls it "email & notification language" rather than
  "email language," and the settings section is labeled accordingly, without
  inventing a notification mechanism that does not ship.

- **Two write lanes, mirroring the timezone/date-format ADRs exactly:**
  - **Resident override:** `IUserInfoService.SetProfileEmailLanguageAsync(
    subjectId, emailLanguage, actorBy)` — the **single write lane** on the
    UserInfo side, the exact shape of `SetProfileDateFormatAsync` /
    `SetProfileTimezoneAsync`: owner-scoped (the Web boundary's job), **no audit
    row** (a profile field write), `KeyNotFoundException` fail-closed (never
    load-or-create). `null` clears (fall back to the platform default).
  - **The UI seam:** the `/settings/language` page gains an
    **"Email & notification language"** section alongside the existing
    language/timezone/date-format sections. The picker is driven by the *same*
    enabled-language catalog rows the rest of the page uses (so a language not
    enabled in the catalog can't be chosen), the save lane POSTs to
    `/settings/email-language` (a `code` value, or `clear=1` to reset to the
    platform default), and the instance-default row is marked so the resident
    can see which choice is "the neighborhood default."

    *(Surface placement: ADR [0080](0080-settings-surface-split.md) originally split this section out onto its
    own `/settings/email-language` page, then the 2026-09-30 amendment
    re-homed it back onto the **Language** tab — that is the current shape.
    The `GET /settings/email-language` route is now a redirect to the
    Language tab; the save lane's route is unchanged.)*

- **The two email lanes resolve through `ITranslationProvider`, with an English
  fallback when no provider is wired in:**
  - **Verification** (`IdentityService`): the subject (`email.verify_subject`)
    and body template (`email.verify_body`, `{0}`=display name,
    `{1}`=verify link) are resolved in the recipient's
    `Profile.EmailLanguage` (signup has no profile yet → `null` → the platform
    default). The `ITranslationProvider` is an **optional** constructor
    dependency (`ITranslationProvider? translationProvider = null`), and when it
    is `null` the service returns the **exact** frozen English subject/body it
    used before — so the direct-construction test sites keep compiling and keep
    asserting the same strings.
  - **Reminder** (`EventReminderService`): the subject (`email.reminder_subject`,
    `{0}`=title) and the fixed "coming up" prefix (`email.reminder_body`,
    `{0}`=title, `{1}`=when, `{2}`=where) are resolved in each **recipient's**
    `Profile.EmailLanguage` (the scheduled tick has no request principal — the
    recipient's `Profile` is the actor, the ADR [0019](0019-timezone-platform-default-and-resident-override.md) / 0020 posture). The event's
    own UGC body (authored in its own language, ADR [0018](0018-ugc-authored-in-language-tag.md)) is appended after the
    localized prefix, exactly as the English fallback already did. The provider
    is an **optional trailing parameter**
    (`ITranslationProvider? translationProvider = null`) so the seven
    direct-call sites in `EventReminderServiceTests` keep compiling unchanged,
    and `null` yields the identical frozen English subject/body. The
    `EventReminderHandler` (the thin Wolverine adapter) passes the DI-registered
    `ITranslationProvider` in, so the production path is localized.

- **The registry keys** (`email.verify_subject`, `email.verify_body`,
  `email.reminder_subject`, `email.reminder_body`) live in the **closed**
  `KnownTranslationKeys` registry (ADR [0015](0015-ui-view-localization-mechanics.md)) with all four language values
  (en/de/fr/da); the parity test enforces exact key-set equality, so adding them
  to one dict without the others is a compile-time test failure, not a silent
  gap.

## Consequences

- A resident can now choose the language their emails arrive in, independent of
  the browsing language, and see the platform default marked in the picker —
  the same shape as the three sibling settings.
- The two shipped outbound channels (verification, event-reminder) honor the
  choice per recipient; the resolution order is the existing ADR [0005](0005-multilingual-support.md) floor, so
  there is no new code path and no new way to get a blank message.
- The **optional-provider + English-fallback** pattern keeps every existing test
  site compiling and asserting the same strings without touching them, and gives
  any future harness a trivially correct (English) path by passing nothing.
- When **notifications** ship, they read `Profile.EmailLanguage` directly — no
  migration, no settings change — so this ADR is the single source of truth for
  "which language does this resident want things *sent* to me in."
- The `confirm()` dialogs and HTML attributes remain deliberately un-translated
  (ADR [0015](0015-ui-view-localization-mechanics.md)'s exclusion); that exclusion is untouched here — this ADR only
  governs the *sent* channels.