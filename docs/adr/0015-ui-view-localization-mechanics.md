---
id: adr-0015
title: "UI view-localization mechanics"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/esc-escalation-authorization-design.md]
supersedes: []
superseded_by: [adr-0072, adr-0165]
updated: 2026-11-02
---

## Context

The `ML` lane (ADR 0005) shipped the **seams** of multilingual support — the two
content documents (`TranslationResource` / `LocalizedPage`), the
`ITranslationProvider` read path (preference → default → `en`, per-string /
per-page fallback), the `ILocalizationService` admin surface, the `LocaleCookie`
preference, and the `/admin/languages` + settings + `/terms`/`/help` Web
surface — and closed green. But its acceptance gate exercised the **part**
(save a `TranslationResource` → the provider resolves it), not the **whole**
(a resident actually seeing the platform in another language). Four gaps
remained (re-verified against the code, 2026-09-12; full detail in
`archive/lanes/multilingual-ui/plan-multilingual-ui.md`):

1. **No view resolved through the provider** — every in-scope view (nav,
   footer, Home, Posts, Groups, Directory, Profile, Account, Admin) carried
   hardcoded English; no `TagHelper` and no `HtmlHelper` extension referenced
   `ITranslationProvider` at all.
2. **The `en` floor was not seeded** — the first-boot seeder stored the
   `en` catalog row + the `LocaleSettings` singleton but **zero**
   `TranslationResource` rows, so the M·12 completeness view and the M·9
   `en`-floor claim were both vacuous for a fresh instance.
3. **The admin editor was free-form** — it asked an admin to hand-type a key
   that no view defined; there was no list of the platform's actual strings.
4. **Discoverability** — the language picker was `[Authorize]`d, so a
   signed-out visitor had no way to choose a language.

ADR 0005 is the **seam this ADR wires**: it defines *what* is translatable and
*how a string resolves*, and deliberately deferred the question of *how a
Razor view emits a string* and *where the canonical `en` key set lives*. The
`ML-UI` lane (ADR 0013's named-lane precedent — a capability with its own
short ID rather than a renumber of M4/M5/M6) settles those two remaining
mechanics.

## Decision

- **D1 — a `<kw-l>` TagHelper resolves a string per request.** A custom
  `TagHelper` (`<kw-l key="nav.home">`, `LocalizeTagHelper` in
  `Kumunita.Web/TagHelpers`) reads the request's preference via
  `LocaleCookie` (M·5 — the cookie is never a claim; M·8 — the read is
  HTTP-free) and resolves it against `ITranslationProvider.GetAsync`, whose
  last-resort floor is the key's `en` source text from the
  `KnownTranslationKeys` registry (the **provider floor** — code is the
  floor, M·1 — never a blank; an unregistered key falls back to the raw key).
  Chosen over
  an `HtmlHelper` extension because it is the idiomatic Razor fit: it reads
  the cookie from the `HttpContext` cleanly, composes directly in the view,
  and degrades safely to the key if the provider is ever absent.
- **D2 — a curated bounded registry defines and seeds the `en` key set.**
  `KnownTranslationKeys` (in `Kumunita.Core/Localization`) is the closed,
  single source of truth: a **key → `en` source text** dictionary. The
  first-boot seeder materializes it `en`-only (upsert, **code-wins** for
  `en`, never touches non-`en` rows), so the M·12 completeness universe is
  real from first boot. The **same** registry is read by the seeder, the
  key-managed admin editor (a closed list, no hand-typed key), the
  completeness view, and — as of the full-sweep amendment below — the
  `TranslationProvider` itself, which is the `en` floor for every registered
  key (D1).

## Consequences

- A resident **actually sees** the platform in their language: the in-scope
  views emit their strings through the provider, a `pl`-preferring resident
  (with `pl` rows) sees `pl`, and a missing key degrades **per string** to
  `en` while the rest of the page stays `pl` (M·2, M·10).
- The admin edits a **closed** key list — the registry — not a hand-typed key;
  the M·12 completeness view is meaningful from first boot (100% present for
  `en`, real missing/present for other languages).
- A **signed-out** visitor can pick a language: the public `/language` picker
  writes the `LocaleCookie` (M·11) — a harmless cookie write, not an
  authorization decision — so a choice is available whether or not the
  resident is signed in.
- **Cost — the registry is curated, not exhaustive.** A new view string is a
  registry entry plus a `<kw-l>` wrap — a **code change only**. The seeder is
  first-boot-only (pristine-DB gate, `DbBootstrap.IsPristineAsync`) and has no
  warm-reseed path; the upgrade safety instead comes from the **provider
  floor** (D1) resolving every registered key to the registry's `en` source
  text from code, so a newly wrapped string renders its English on *every*
  instance immediately — no database reseed. The registry remains the
  curated platform surface, not "every string in every view".
- **Recorded deviation (D1, U2):** the TagHelper emits the resolved text via
  `SetContent` (auto-escaping), not the plan's `SetHtmlContent` sketch. The
  resolved value is platform copy, and escaping it is the safer path against
  any injection from an admin-entered translation string — conservative, not a
  behavioral change to a resident.
- **Recorded limitation (U3/U4/U5):** `ViewData["Title"]` tab titles are
  **not** localized — only the in-view `<kw-l>` strings are; the `<title>` is
  left as the page's English name.
- **Follow-on — UGC is untouched (M·3).** The view-localization path resolves
  **platform text only**; post / reply / group / announcement bodies are
  authored and rendered **as written**. Machine translation of UGC remains a
  **deferred trust boundary** (ADR 0005 C) and, if it ever ships, a separate
  opt-in lane with a third-party-boundary review.
- **Cross-references:** ADR 0005 (the seam this ADR completes — the seam now
  reaches the primary UI surface), and ADR 0013 (the named-lane-with-a-short-
  ID precedent that `ML-UI` follows rather than renumbering M4/M5/M6).
