---
id: adr-0005
title: "Multilingual support (UI & static pages)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/group-events-design.md]
supersedes: []
superseded_by: [adr-0039, adr-0042, adr-0043, adr-0046, adr-0049, adr-0165]
updated: 2026-11-02
---

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
| UI strings (labels, buttons, toasts, flash) | platform (code keys) | `mt` — `TranslationResource` | GlobalAdmin / **Translator** (ADR [0021](0021-translator-role-delegated-translation-editing.md)), in-app |
| Static pages (terms, about, help) | platform + admin | `mt` — `LocalizedPage` per slug+language | GlobalAdmin / **Translator** (ADR [0021](0021-translator-role-delegated-translation-editing.md)), in-app |
| User-generated content | residents | `mt` — domain documents | **never by default**; opt-in MT is deferred (C) |

### B. Languages and translations are data, not config

One Postgres per instance (ADR [0004](0004-data-persistence-and-schema-evolution.md)), so languages live in the `mt` schema as
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
seeder (see [deployment & configuration](../ARCHITECTURE.md)). Every other language is community-provided —
typed in or imported by the admin — **with one bundled exception** (amended
2026-09-18, ADR [0042](0042-bundled-initial-language-pack.md)): `de` and `fr` additionally ship as **bundled initial
values** (catalog rows + full UI-string baselines + `terms` / `help` page
bodies, seeded first-boot-only), editable in-app by a GlobalAdmin / Translator
(ADR [0021](0021-translator-role-delegated-translation-editing.md)); **`en` remains the only code-owned language.** See ADR [0042](0042-bundled-initial-language-pack.md) for
the locked ownership semantics.

**Resolution order per request:** user's saved preference (cookie + settings
page) → **browser `Accept-Language` match** (amended 2026-09-19, ADR [0046](0046-default-language-browser-accept-language-match.md) —
a *suggestion*, matched against the enabled catalog, never persisted, only
when no cookie preference is saved) → instance default (`LocaleSettings`) →
source language (`en`). Fallback is per-string / per-page, so a partially
translated UI degrades gracefully: a resident never sees a blank label. The
preference is a cookie — it is *not* a claim, per the thin-token rule
(ADR [0001](0001-core-stack-and-identity-model.md)-B); the browser match is a per-request read signal, not a claim or
a write path (ADR [0046](0046-default-language-browser-accept-language-match.md)).

### C. User-generated content: optional, opt-in, deferred

- UGC is always rendered **as authored**. Nothing is translated silently.
- Machine translation is a **deferred feature** (README → Deferred). If it is
  ever added: per-item, user-initiated ("show in my language"), clearly
  labeled ("machine translation"), and the provider becomes a **third-party
  boundary** in SECURITY.md (like B4) — audience-restricted content is
  **never** sent to it. Default: off.

### D. Management (in-app, audited)

The `/admin/languages` surface is split by standing (ADR [0021](0021-translator-role-delegated-translation-editing.md)):

**GlobalAdmin** manages the catalog under `/admin/languages`:

- add a language (BCP-47 code + native name), enable/disable, reorder;
- set the default language;
- remove a language — **blocked while it is the default** (set a different
  default first). A user preference pointing at a removed language silently
  falls back to the default; `LocalizedPage` rows for it are retained, so
  re-adding the language restores the work.

**GlobalAdmin or Translator** edits the translatable text (ADR [0021](0021-translator-role-delegated-translation-editing.md)):

- edit and preview static-page translations per language, and edit the UI
  strings; both with the per-language completeness view (which UI keys /
  pages are missing).

Adding/removing languages, reordering, and changing the default are
GlobalAdmin actions and are **audited** like every other admin action
(see [data model — audit retention & scope](../architecture/data-model.md)). Translation saves are audited the same way, with the
actor's account recorded on the row (ADR [0021](0021-translator-role-delegated-translation-editing.md) — `Via: Admin`, `ActorId` =
the GlobalAdmin *or* Translator).

## Consequences

Positive
- A non-English-speaking neighborhood can run its entire platform in its own
  language without a code change or a deploy.
- Platform texts are data: reviewable, diffable in the DB, and covered by the
  existing single-`pg_dump` backup story (ADR [0004](0004-data-persistence-and-schema-evolution.md), OPS.md §4).
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
  concurrency story applies (HTTP 409, see [data model — optimistic concurrency](../architecture/data-model.md)).
