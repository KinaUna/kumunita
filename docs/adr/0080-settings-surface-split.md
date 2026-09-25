# ADR 0080 — The /settings surface split: four linkable section pages

Status: Accepted
Date: 2026-09-25

## Context

The resident's settings surface (ADR 0005 B; the ADR 0019 time zone and
ADR 0020 date & time format sections folded in, and ADR 0061's email &
notification language appended) was a single long scroll at
`/settings/language`: the Language picker, the Time zone picker, the Date &
time format picker, the Email & notification language picker, and the
help/account mount-slot link — all on one `Index` page, in that order. The
same two problems ADR 0062 named for `/admin` had built up here:

- **No linkable, shareable, per-section address.** A resident (or a
  moderator pointing one at a setting) who wanted to "send someone straight
  to the time zone setting" could not — `/settings/language` was the only
  route, and the time zone was a mid-scroll section.
- **The scroll's length made it hard to scan.** Every section the platform
  gained (time zone, date format, email language) had been appended to the
  same page, so a resident doing one small thing (set the time zone) landed
  on a page that also contained the language picker, the date-format picker,
  and the email-language picker.

This is the same settled design question ADR 0062 recorded for the admin
surface ("the shell is too much, split it"), so it follows the ADR 0062
pattern: **split the one page into linkable sections, each with its own
route, and render a shared sub-nav.** This is a *surface change, not a
capability change* — no write lane, seam, or model changes.

The constraint from the test harnesses: `PublicLocaleAndAboutTests`
builds `LocaleController(localization, userInfo, store)` and pins the
`Index()` GET, so the section actions live **on the existing
`LocaleController`** with explicit `[Route]` attributes rather than on new
controllers.

## Decision

- **Split `/settings/language` into four linkable GET routes, all on
  `LocaleController`, sharing one `LocaleSettingsViewModel`** (built by a
  single private `BuildModel()` that all four actions call — the model is
  still the full model; each view renders only its own section):

  | Route | View | Section |
  |---|---|---|
  | `/settings/language` | `Index` | **Language** (the ADR 0005 B picker + the ADR 0046 browser-match hint) |
  | `/settings/timezone` | `Timezone` | **Time zone** (ADR 0019 — the `Profile.TimeZone` override) |
  | `/settings/dateformat` | `DateFormat` | **Date & time format** (ADR 0020 — the `Profile.DateFormat` override) |
  | `/settings/email-language` | `EmailLanguage` | **Email & notification language** (ADR 0061 — the `Profile.EmailLanguage` override) |

  Each section view keeps the `Model.<Section> is not null` guard so a
  signed-out render (the harness's null-subject path) still degrades cleanly.

- **A shared sub-nav** (`Views/Locale/_SettingsTabs.cshtml`) renders a
  `nav-tabs` strip on all four pages so a resident can jump between sections
  without scrolling. The active tab is derived from the current action name
  (`Index` → Language, `SettingsTimezone` → Time zone, `SettingsDateFormat` →
  Date & time format, `SettingsEmailLanguage` → Email & notification
  language) — the exact `_EventsTabs.cshtml` / `_AdminNav.cshtml` idiom. The
  tab **labels** reuse the four already-registered section-title keys
  (`locale.language_heading`, `settings.timezone_title`,
  `settings.dateformat_title`, `settings.email_title`) — **no new
  translation key** is introduced, so the ML parity pins and
  `KwLRegistryConsistencyTests` are untouched.

- **The write lanes are unchanged; only their redirect targets are re-pointed
  to the section that owns them.** The four POST lanes (`Save` at
  `/settings/language`, `SaveTimezone` at `/settings/timezone`,
  `SaveDateFormat` at `/settings/dateformat`, `SaveEmailLanguage` at
  `/settings/email-language`) keep their exact signatures, routes, CSRF
  attributes, and fail-closed `KeyNotFoundException` handling. The
  time-zone / date-format / email-language saves now
  `RedirectToAction` the section that owns them (instead of the old `Index`),
  so a resident lands on the page the form was on. The language `Save`
  keeps redirecting to `Index` (it is the landing tab).

- **The help/account mount-slot link (ADR 0039 §3.8) moves to the last tab.**
  The "Help with your account" slot block — the `help/account` page link
  resolved via `PageMountResolver` — renders at the bottom of
  `EmailLanguage.cshtml` (the last settings section), out of the way of the
  pickers. It is the only view that `@inject`s `IPageService`; the other
  three section views and the language `Index` do not.

## Consequences

- **Deep-linkable settings sections.** `/settings/timezone`,
  `/settings/dateformat`, and `/settings/email-language` are now shareable
  addresses — the ADR 0062 "send a colleague straight to the accounts page"
  property, applied to the resident surface.
- **Surface-only change.** No Core seam, doc type, migration, or translation
  key changed. `Kumunita.Core` stays HTTP-free; the four write lanes and
  their audited Core lanes are byte-for-byte unchanged; the pinned
  `LS_U06_SettingsSurface_ListsFirstBootCatalog_InSortOrder` test (which
  calls `controller.Index()`) stays valid.
- **`PublicLocaleController` / `/language` are untouched.** The public
  language picker (`Views/PublicLocale/Index.cshtml`) deliberately does not
  depend on the settings surface and renders no tabs — it keeps its own
  surface (the "must not depend on the settings view changes" invariant).
- **The tab labels are the section titles.** Because the tab labels reuse the
  section-title keys, a resident sees each section's title twice on its own
  page (tab + heading). That is accepted — it matches the ADR 0062 admin
  sub-nav, where the tab and the section heading also share the same label,
  and it costs zero new translation keys.
- **New test pins the split.** `SettingsSectionSplitTests` asserts each
  section GET renders its own view name with the shared model, and each save
  lane (via the subject-null branch, which returns before any `TempData`
  write the harness can't observe) redirects back to the section that owns
  it — the language `Save` still targeting `Index`.
