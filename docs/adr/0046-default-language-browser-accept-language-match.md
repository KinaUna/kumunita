# ADR 0046 — Default-language fallback: browser `Accept-Language` match

Status: Accepted
Date: 2026-09-19
Amends: **0005 §C** (the resolution chain — a step is inserted between the
preferred-language cookie and the instance default; the cookie keeps first
place, the instance default + `en` floor keep last). Builds on **0015**
(`KnownTranslationKeys`, the `<kw-l>` TagHelper, the provider seam),
**0005 §B** (the per-string fallback discipline, M·2), and the M·8 / ADR
0001-B split (Core is HTTP-free; the preference cookie is never a claim). No
amendment to 0042/0043/0045 (the bundled baseline, the seeded system pages, the
Danish pre-seed) — this ADR is about *how the default resolves*, not about what
is in the catalog.

## Context

ADR 0005 §C pins the resolution chain: **preferred-language cookie → instance
default → `en` floor**, with the fallback discipline per-string (M·2). Today a
resident who has never chosen a language sees the instance default on every
page, whatever their browser speaks. The platform is being exercised in
neighborhoods that are not English-default, and a newcomer whose browser is
entirely Danish (or French, or German) gets an English-default UI until they
manually open the language picker and save — a discoverability gap the
picker's own purpose (find your language) is meant to close.

The request: *if a user has not selected a language, the page should try to
match the browser's language settings; if not possible, use the platform
default language.*

## Decision

### D1 — The resolution chain gains a step: cookie → browser match → instance default → `en`

- **The preferred-language cookie (M·5) keeps first place.** An explicit
  resident choice always wins over anything the browser says — the browser
  match is a *suggestion*, never a *choice*. The cookie is also, and stays,
  the **only write path**: a browser match is never persisted, never set,
  never refreshed.
- **When the cookie is absent** (no preference saved, or a blank/invalid
  cookie — the M·5 "no preference" shape), the per-request resolution tries a
  **match of the `Accept-Language` header against the enabled catalog**.
  "Enabled" is read exactly as ADR 0015 / 0042/0045 read it: the
  `LanguageCatalog.Enabled` rows, ordered. A tag matching a **disabled**
  language (ADR 0045's `da`-at-first-boot shape) is *not* a match — the
  suggestion does not outrun the catalog.
- **If no tag matches** (header absent, no enabled match), the chain continues
  exactly as before: instance default → `en` floor, per-string (M·2). The new
  step is *insertive*, not a replacement — a misconfigured browser degrades to
  the frozen ADR 0005 chain.

### D2 — Matching rules (the `RequestLanguage` helper, Web layer)

- **Exact tag first, then primary subtag.** `Accept-Language: fr-CA` matches
  the `fr` catalog row; `en-US` matches `en`. Case-insensitive.
- **Priority honored.** RFC 9110 quality factors (`q=`) order the tags
  (highest first, then document order); the first tag with an enabled-catalog
  match wins.
- **Permissive parse, strict output.** Invalid tags are dropped (not a fault);
  `q=0` tags are dropped (an explicit "not acceptable"); duplicates keep the
  highest quality. A garbage header degrades to "no browser signal" — it can
  never throw, never 500.
- **Single reader of the header.** `Kumunita.Web.Security.RequestLanguage`
  (a small static helper) is the one place the `Accept-Language` header is
  parsed — the TagHelper, the two locale controllers, and the inline-editor
  toggle all ask *it* for the effective per-request signal, so the parse
  cannot drift between surfaces.

### D3 — The Core seam: candidates, not headers (M·8 / ADR 0001-B hold)

- `ITranslationProvider` gains `ResolveEffectiveLanguageAsync(
  IReadOnlyCollection<string>? candidates)` — an **ordered candidate list**,
  deduplicated, first *enabled* match wins. The header itself never crosses
  the seam; the Web layer translates the browser signal into the candidate
  list and passes it in. Core stays HTTP-free (M·8); the language remains
  never-a-claim (ADR 0001-B) — the candidates are a resolution input, not
  identity.
- The existing `ResolveEffectiveLanguageAsync(string? preferred)` is
  unchanged: `candidates` omitted ⇒ the frozen ADR 0005 chain. The old call
  shape (cookie → default → `en`) is a special case of the new one, so no
  pinned behavior in 0005/0015 moves — the parity and fallback tests keep
  passing as written.

### D4 — UX: the suggestion is *visible*, not hidden

A browser match must not look like a saved choice — a resident who sees
"Danish" pre-selected but has no way to know *why* would treat it as their
setting. So:

- **The picker pre-selects the browser match** when no cookie is saved, with
  an explicit "(matched from your browser)" marker and an info line explaining
  the match is a suggestion — *saving* makes it the preferred language
  (the cookie write path, unchanged).
- **The account nav** marks the active language with a `(matched)` suffix in
  the dropdown only while the match (not a saved preference) is in effect.
- The marker/notes are themselves localized — new `locale.browser_matched`,
  `locale.browser_note`, `locale.browser_note_tail` keys in the ADR 0015
  registry, shipped in `en`/`de`/`fr`/`da` (the parity pin holds).

## Consequences

- The per-request resolution now reads (in Web): the cookie, then the header;
  the Core read seam reads only the candidate list. M·8, ADR 0001-B, and the
  thin-token rule all hold — nothing here enters a claim, a cookie write, or
  an authorization decision.
- Audit/SECURITY: a browser match grants no extra access. It only picks which
  *already-enabled* language the UI renders in; audience restriction and
  visibility are unchanged (the language choice never was an access control —
  ADR 0005 §B).
- ADR 0045's disabled-`da` shape is respected end-to-end: a Danish browser on
  a first-boot instance sees the `en`-default UI, *not* a Danish one, because
  `da` is not an enabled row — the suggestion cannot leak a disabled language
  into the UI.
- No new persistence, no new Marten document, no migration. The whole feature
  is a read-path refinement: one new Web helper, one provider overload, three
  registry keys, and the picker/nav markers.

## Tests (pinned)

- **Core** (`LocalizationServiceTests`, live Postgres): an enabled
  candidate resolves; a disabled candidate falls through to the default; a
  non-catalog tag falls through; candidate order is honored first-enabled-
  wins; null/empty candidates reproduce the frozen ADR 0005 chain.
- **Web** (`ADR_0046_RequestLanguageTests`): `Accept-Language` parse and
  match (exact, primary-subtag, priority, case, disabled-exclusion, `q=0`,
  malformed-degrades, duplicate-keeps-highest); `Preference` (cookie beats
  browser; no-cookie falls back; both absent ⇒ null ⇒ default chain; blank
  cookie ⇒ browser).
- **Registry parity** (`KnownTranslationKeys_ParityTests`): the three new
  keys are present in `en`/`de`/`fr`/`da` — the key-set pin holds.
