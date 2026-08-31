# ADR 0005 — Multilingual support (UI & static pages)

Status: Accepted
Date: 2026-08-27

## Context

Kumunita serves a single neighborhood whose residents may not all share the
platform's source language. Requirements:

- The **user interface** (labels, buttons, navigation) and the platform's own
  **static texts** — terms and conditions, about page, help/support — must be
  translatable. These are *platform* texts, not resident content.
- **Translation of user-generated content** (posts, replies, event
  descriptions) is **optional**: content is authored and rendered as written.
  Any translation assistance must be opt-in, per-item, and clearly labeled —
  never silent, never automatic.
- **Administrators manage languages in-app**: add and remove supported
  languages, and choose the instance's default language. Languages are an
  *instance* concern (one neighborhood, its languages), not a global or
  build-time concern.
- Privacy first (SECURITY.md §1): the default behavior must not send resident
  content to any third party.

## Decision

### A. What is translatable

| Text kind | Owner | Stored | Translated by |
|-----------|-------|--------|---------------|
| UI strings (labels, buttons, toasts, flash) | platform (code keys) | `mt` — `TranslationResource` | admin / community, in-app |
| Static pages (terms, about, help) | platform + admin | `mt` — `LocalizedPage` per slug+language | admin / community, in-app |
| User-generated content | residents | `mt` — domain documents | **never by default**; opt-in MT is deferred (C) |

### B. Languages and translations are data, not config

One Postgres per instance (ADR 0004), so languages live in the `mt` schema as
documents. Admins edit them in-app; changes take effect on the next request.
No env var, no rebuild, no new service.

- `LanguageCatalog` — one row per supported language:
  `{ code (BCP-47, e.g. "en", "pl"), nativeName, enabled, sortOrder }`.
- `LocaleSettings` — singleton: `{ defaultLanguageCode }`.
- `TranslationResource` — `{ key, languageCode, text }`; UI strings.
- `LocalizedPage` — `{ slug, languageCode, title, body (Markdown), updated }`;
  whole static pages rendered by a single page engine.

The **source language (`en`) ships with the code**: its catalog row and UI
strings are embedded in the image and materialized into `mt` by the first-run
seeder (ARCHITECTURE.md §8). Every other language is community-provided —
typed in or imported by the admin.

**Resolution order per request:** user's saved preference (cookie + settings
page) → instance default (`LocaleSettings`) → source language (`en`).
Fallback is per-string / per-page, so a partially translated UI degrades
gracefully: a resident never sees a blank label. The preference is a cookie —
it is *not* a claim, per the thin-token rule (ADR 0001-B).

### C. User-generated content: optional, opt-in, deferred

- UGC is always rendered **as authored**. Nothing is translated silently.
- Machine translation is a **deferred feature** (README → Deferred). If it is
  ever added: per-item, user-initiated ("show in my language"), clearly
  labeled ("machine translation"), and the provider becomes a **third-party
  boundary** in SECURITY.md (like B4) — audience-restricted content is
  **never** sent to it. Default: off.

### D. Admin management (in-app, audited)

GlobalAdmin manages languages under `/admin/languages`:

- add a language (BCP-47 code + native name), enable/disable, reorder;
- set the default language;
- edit and preview static-page translations per language, with a
  per-language completeness view (which UI keys / pages are missing);
- remove a language — **blocked while it is the default** (set a different
  default first). A user preference pointing at a removed language silently
  falls back to the default; `LocalizedPage` rows for it are retained, so
  re-adding the language restores the work.

Adding/removing languages and changing the default are admin actions and are
**audited** like every other admin action (ARCHITECTURE.md §5).

## Consequences

Positive
- A non-English-speaking neighborhood can run its entire platform in its own
  language without a code change or a deploy.
- Platform texts are data: reviewable, diffable in the DB, and covered by the
  existing single-`pg_dump` backup story (ADR 0004, OPS.md §4).
- UGC stays exactly what residents wrote — community trust is preserved.
- One page engine + one tag-helper path for *all* platform text: no second
  mechanism for a future feature (federation, templates).

Negative / accepted risks
- An extra indirection in the view path: a `TranslationResource`-backed string
  provider instead of static satellite assemblies. Accepted — the alternative
  (redeploy to add a language) is worse for the "admin manages languages
  in-app" requirement.
- Translations are only as good as the person who wrote them. Mitigated by
  the per-language completeness view and per-string fallback (B, D).
- Two people editing translations can collide — the standard optimistic-
  concurrency story applies (HTTP 409, ARCHITECTURE.md §5).

## Revisit when

- A community actually needs MT of UGC — then decide C as a **new trust
  boundary**, not a feature flag, and update SECURITY.md §5/§6.
- Federation arrives (ADR 0001-B): the *platform* language catalog may move
  with the IdP, but per-instance languages and translations stay local.
