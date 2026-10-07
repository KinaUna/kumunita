# ADR 0149 — Home intro sections: a per-resident "hide and show me the feed" preference

Status: Accepted
Date: 2026-10-07

## Context

The home page (`/`) is the first surface a resident lands on and it opens with
two presentational sections before the "What's new" feed:

1. **"Intro: what Kumunita is and does"** — the hero band (the community name,
   the plain-language pitch, the "What it is & how it works" link).
2. **"What Kumunita does"** — the three-surfaces band (one feed / groups /
   pinned notes).

Both are written in `Views/Home/Index.cshtml` as full-bleed sections and
rendered for **every** visitor — anonymous and signed-in alike. A returning
resident who has used the platform for a while finds them noise: they know
what Kumunita is and what it does, and they want to see what's new on their
street right away. There was no way to get past the two intro sections other
than scrolling.

## Decision

- A new **per-resident display preference**, `Profile.HideHomeIntro`
  (a `bool`, default `false`), is the single switch that governs both
  sections together (they hide and show as a pair — the resident's intent is
  "get me to the feed," not "hide the hero but keep the surfaces," so there
  is no finer-grained control). `false` (the default) means show both, exactly
  as before; `true` means hide both so the page opens on the "What's new" feed
  (for a signed-in resident) or, when there is no feed, on the roadmap.
- It is a **pure display preference** — it changes what this resident sees on
  `/` and nothing else. It is never an access decision, never a claim
  (thin-token rule, ADR 0001-B), and it is read fresh per request (the next
  visit reflects the change, with no rebuild / restart). Anonymous visitors
  always see both sections (there is no subject to resolve a preference
  against) — the hero remains the public landing surface.
- The read lives in `HomeController.Index`: when the request has a signed-in
  subject and the `IUserInfoService` seam is present, it reads
  `Profile.HideHomeIntro` (best-effort — a missing seam or a read failure
  degrades to `false`, and the page always renders) and passes it through a
  new `HomeViewModel.HideIntro` field (default `false`). The view wraps the
  two sections in `@if (!Model.HideIntro) { … }`; the "What's new" feed and the
  roadmap are untouched, so a hidden-intro page simply starts lower.
- The write is the ADR 0019 / ADR 0020 owner-scope single-write-lane shape,
  mirrored from `SetProfilePageSizeAsync`: a new
  `IUserInfoService.SetProfileHideHomeIntroAsync(subjectId, hideHomeIntro,
  actorBy)` lane that loads the profile, sets the one flag, and saves in one
  session (invariant C3); no `AccessAudit` row (a profile field write — the
  `UpsertProfileAsync` shape, "not an access decision"); fails closed on a
  missing profile (`KeyNotFoundException`, the `SetProfileTimezoneAsync` pin —
  the lane never load-or-creates). Strong consistency (invariant C4): the new
  value is live on the very next `GetProfileAsync` call.
- The resident-facing surface is a new **Home page** section in the ADR 0080
  tabbed settings: `GET /settings/home` + `POST /settings/home`
  (`LocaleController.SettingsHome` / `SaveHome`) rendering one on/off checkbox
  ("Hide the intro sections and show me the feed right away"). Unlike the
  other settings sections there is no picker and no reset button — un-checking
  is the reset (unchecked is the `false` floor). The section joins the
  `_SettingsTabs` sub-nav as a "Home page" tab, active on
  `LocaleController.SettingsHome`.
- The `Profile.HideHomeIntro` field is an **additive** field per ADR 0004
  §B.1 (delta-detected, idempotent, no re-seed, no EF migration, no new
  `*DocTypes` surface) — the 13th additive `Profile` field, like
  `PageSize`. The `false` default means every existing account is
  unchanged on a fresh boot.
- New UI strings (`settings.home_title` / `_lede` / `_label` / `_note` /
  `_save` / `_flash_hide` / `_flash_show`) land in the
  `KnownTranslationKeys` closed set for all four shipped languages (en, de,
  fr, da) — the ADR 0015 D1 provider-floor discipline; the en source text is
  the fallback.

## Consequences

- A returning resident can land straight on the "What's new" feed with one
  checkbox; a new resident (or an anonymous visitor) is unaffected and still
  sees the full intro — the platform keeps its onboarding story for the
  people who need it and removes it for the people who don't.
- The preference is per-resident and stored on their own account (not a
  cookie, not a claim, not a global setting) — it travels with the resident,
  is visible to no one else, and cannot gate any access.
- `IUserInfoService` gains one member (a compatible addition to the frozen
  surface, ADR 0006-A — the `SetProfilePageSizeAsync` precedent); the single
  concrete `UserInfoService` is the only implementer.
- The home page's first two sections are now conditional: the markup is
  unchanged except that they sit inside one `@if`, and the section that
  follows ("What's new") moves up to the top of the page when the preference
  is on. The roadmap section (4) is never affected.
- No migration: `Profile` is a Marten document and the new field defaults to
  `false`, so existing documents read back with the field absent → `false`,
  which is the exact prior behavior (intro shown).
