---
id: arch-localization
title: "Localization (multilingual)"
tier: reference
kind: architecture
status: current
audiences: [developer, agent, platform-maintainer, community-admin]
related:
  - docs/ARCHITECTURE.md
  - docs/adr/0005-multilingual-support.md
  - docs/adr/0015-ui-view-localization-mechanics.md
updated: 2026-11-02
---

# Localization (multilingual)

> **Abstract:** the operating shape of the multilingual platform — what is translatable (UI strings + the `PageKind.System` static pages), how it is stored (data in `mt`, not env), the resolution order (cookie → instance default → `en`), the admin surface (`/admin/languages`), and the shipped-lane state (ML, ML-UI, SP). **Who is it for:** a developer adding a UI string or a static page; a community admin managing the language catalog. **When to read the rest:** before adding a new `kw-l` key, a new static page, or a new language.


Design and rationale in ADR [0005](../adr/0005-multilingual-support.md); this is the operating shape. **Current state: the
`ML` lane is shipped** (2026-09-12 — see [`design/multilingual-design.md`](../design/multilingual-design.md)
§ Multilingual — Closed) — the two content documents (`TranslationResource` /
`LocalizedPage`), the `ITranslationProvider` read seam, the `ILocalizationService`
admin seam, the `LocaleCookie` preference, and the `/admin/languages` + settings +
`/terms`/`/help` Web surface are all live. **`ML-UI` (ADR [0015](../adr/0015-ui-view-localization-mechanics.md), 2026-09-12) then
wired that seam into the views** — the full platform UI surface (251 keys,
the registry's completeness universe equals exactly what the views emit)
resolves per request through a `<kw-l>` TagHelper against the provider, the
`en` floor is the `KnownTranslationKeys` registry's own source text (code is
the floor — the first-boot seeder's `en` rows are a stored copy, no reseed is
ever needed), the admin editor is key-managed (a closed list, no hand-typed
key), and the picker is public (signed-out residents can choose a language) —
closing the `/about` follow-on the `ML` record had left open. The `SP` lane
(ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md)) widened the static-page routes from three to the **five-surface
set**: `/privacy` + `/conduct` join the hard-coded routes (404 floor, ADR
0043 D2) alongside `/terms` / `/help`; the four `Page`-backed surfaces (terms
/ help / privacy / conduct) are seeded under the `system/` root, `/about`
stays the product-story **view** (not a seeded page, ADR [0043](../adr/0043-system-pages-shipped-and-discoverable.md) D1), and the
footer now carries an unconditional "Platform" column linking all five (ADR
0043 D4).

- **What is translatable:** UI strings and platform static pages (terms, help,
  privacy, conduct — the `PageKind.System` pages) — see [data model](data-model.md) documents. UGC is always rendered **as authored**; machine
  translation is deferred and, if it ever ships, per-item opt-in with a
  third-party-boundary review (ADR [0005](../adr/0005-multilingual-support.md) C, SECURITY.md §6). Separately, each UGC
  document carries an **authored-in language tag** (`LanguageCode` on `Post` /
  `PostReply` / `Announcement`, ADR [0018](../adr/0018-ugc-authored-in-language-tag.md)) — a BCP-47 metadata field for future
  search and reader-added language versions, resolved to a concrete code at
  write time (instance default → `en`); it translates nothing. The **TD lane**
  (ADR [0027](../adr/0027-post-reply-translation-display-and-swap.md)) builds on that tag at the **display** layer only: on the
  post/reply detail surface the authored-in code is surfaced **additively**
  (as `OriginalLanguageCode` on `PostDetailViewModel` /
  `GroupPostDetailViewModel` / `ReplyItem`, read from the ADR [0018](../adr/0018-ugc-authored-in-language-tag.md) field) and
  rendered as the first, default-visible variant chip, toggled by a small
  server-rendered / client-`display`-toggle swap. It adds **no** document and
  **no** migration (ADR [0004](../adr/0004-data-persistence-and-schema-evolution.md) §B.1 is not engaged — the field already exists)
  and leaves the ADR [0022](../adr/0022-user-added-post-reply-translations.md) add-translation write lane untouched; the swap is a
  presentation concern, not a data, schema, or authorization one (TD·7).
- **Storage:** languages, the default, and all translations are data in `mt` —
  not env, not image config. Admins add/remove languages and set the default
  in-app; the change is effective on the next request, no redeploy.
- **Resolution order:** user preference (cookie + settings page) → instance
  default (`LocaleSettings`) → source language (`en`). Fallback is per-string /
  per-page, so a partially translated UI degrades gracefully. The preference is
  a cookie, never a claim (thin-token rule, see [identity & access](identity-access.md)).
- **Admin surface** (`/admin/languages`, GlobalAdmin): add (BCP-47 code +
  native name), enable/disable, reorder; set the default; edit/preview
  static-page translations per language with a per-language completeness view.
  Removing the default language is blocked (set a different default first);
  removed languages keep their `LocalizedPage` rows so re-adding restores the
  work. All changes are audited admin actions (see [data model](data-model.md)).
- **Source language:** `en` ships in the image and is seeded on first run (see [deployment & configuration](../ARCHITECTURE.md));
  every other language is community-provided.

