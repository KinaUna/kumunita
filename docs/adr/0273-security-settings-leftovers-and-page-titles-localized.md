# ADR 0273 — Security settings views, the last leftovers and page titles are localized

Status: Accepted
Date: 2026-10-29

## Context
After ADR 0272 the remaining hard-coded English in views was the two security-settings pages (SecuritySettings/Mfa ~23 strings, Sessions ~9), a handful of leftovers, and 118 distinct
`ViewData["Title"]` strings that only feed the browser-tab `<title>` in `_Layout`.

## Decision
- **Mfa / Sessions**: `kw-l` for text, server-side variables for `data-confirm`, `aria-label` and `alt`. The recovery-code count used an inline plural (`code@(n == 1 ? "" : "s")`); it is now
  two keys `settings.mfa.codes_remaining_one/_other` (`{0}` placeholder) chosen in the view and shown after the localized status sentence. 33 keys: `settings.mfa.*`, `settings.sessions.*`.
- **Leftovers**: Portability index/resolve pages (`myportability.*`; the "N rows fit…" sentence became count sentences with `{0}`/`{1}` and the conflict reason a `{0} {1} {2}` template whose
  kind words stay English enum text), Moderation/Resolve, MyDrafts headings, Posts/New, Posts/Index, StaticPages/About contact band (`about.cta.*`), Blog draft tooltip, Kiosk exit label,
  inventory note placeholder, Pages sibling nav, `_Sort` aria-label. Existing keys reused where the English matched (`events.series.repeats`, `directory.detail_verified`, `common.remove`,
  `groups.member_leave`, `pages.form.scope_*`, `pages.children`, `sort.modified`, `notifications.bell`, `common.language`, `nav.announcements`, `nav.events`, `search.section.posts`,
  `settings.dateformat_firstday_label`).
- **Profile/Preview** used "As {name}, you" around two localized tails, which de/fr/da could not fit (the English words "As"/"you" stayed in the middle of the sentence). Replaced by whole
  sentences `profile.preview_visible_sentence` / `profile.preview_hidden_sentence` with a `{0}` for the bold name. `profile.preview_*_tail` are now unused.
- **Page titles (per Per's choice: central map)**: `Security/PageTitleKeys` maps each English title to a `title.*` key (118 keys, slug of the English text); `_Layout` looks the view's
  `ViewData["Title"]` up, resolves it with the effective language and falls back to the English the view set. No view was edited for titles. Titles built from data (a person's name, `$"Admin — {name}"`,
  a post title) are unmapped and pass through. The generic fallbacks ("Event", "Post"…) are mapped, so an item titled exactly that would show translated in the tab only — accepted.
  `PageTitleKeysTests` pins that each mapped key is registered with the mapped text as its English value.
- Unchanged on purpose: the brand "Kumunita", the "Polski" example placeholder, the `otpauth URI` label, and the dynamic composite titles.

## Not covered
Not compiled or run (no .NET SDK here); de/fr/da machine-written, review parked. Composite titles with data (`Admin — {name}`, `{community} — manage`, `Moderation: report …`) stay English.
Core service exception text stays English (ADR 0270). The hard-coded-English sweep of views and controllers is otherwise complete.
The `improve-check.ps1` gate (a) pin on `KnownTranslationKeys.cs` was re-based **13975 -> 14687** by this ADR (the 178 new keys above × en/de/fr/da), the same shipped-additions mechanism as ADR 0265–0272; both the pin and this note are in the same commit, and the gate was not loosened.
