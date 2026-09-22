# ADR 0027 — Post & reply translation display and swap (authored-in as first-class variant)

Status: Accepted
Date: 2026-09-14
Amends: 0018

## Context

ADR 0018 added the **authored-in language tag** (`Post.LanguageCode` /
`PostReply.LanguageCode`) — "the language the item was written in." ADR 0022
added the **user-added-translation** chip row — a human translating an existing
post/reply into *another* supported language, shown on the detail surface as
chips with expandable `<details>` panels.

But the detail surface's chip row is computed **only from the added rows**
(`Languages.Where(l => !l.HasTranslation)` for the add-lane, and the
`PostTranslation`/`ReplyTranslation` set for the chip list). The authored-in
language has **no** `PostTranslation`/`ReplyTranslation` row, so it is
invisible to that row. Three defects result, all reported on the post/reply
detail surfaces (community + group lane):

1. **"None yet" is a lie.** An item plainly *in* a language renders
   `Translations: None yet` because it has no *added* row for its own language.
2. **"Add a <that language>" is offered.** The item's own language is offered
   as a translation to add.
3. **No in-place reading.** A translation's title+body only live in an
   expandable panel below the original; there is no way to read it in place or
   switch back.

ADR 0005 §C is the constraint that shapes the fix: **UGC is never
machine-translated and its display is never auto-switched by the
`kumunita.locale` cookie.** Whatever this lane adds is therefore an
**explicit click**, not a preference.

## Decision

Two decisions, both display-only, both zero Core / schema change.

**(a) The authored-in language is a first-class variant on the post/reply
detail surface.** The item's own `LanguageCode` renders as the **first,
always-present, default-visible** variant chip; the main title+body (post) /
body (reply) is swapped to the selected variant on **explicit click** (never
auto — ADR 0005 §C), with a guaranteed path back to the original. Every
variant's title+body is rendered **server-side** (the same escape-safe
`MarkdownRenderer` the current `<details>` panels use) into a hidden
container; a small `tsc`-only ES module toggles `display` — no `innerHTML`, no
re-fetch, no navigation. With JS disabled the original is visible and the
added variants remain present in the DOM. The "Add a …" candidate list
**excludes** the item's own language, so the authored-in language is never
offered to add.

**(b) The shared `LanguageOption` record is left untouched** (ADR 0026 still
consumes its 3-positional-arg shape). The authored-in code is carried
**additively** on the two post-detail view models and the reply item —
`PostDetailViewModel.OriginalLanguageCode`,
`GroupPostDetailViewModel.OriginalLanguageCode`, and a trailing positional
`OriginalLanguageCode` on `ReplyItem` (11th, after `DeletedAt?`) — populated
from the ADR 0018 fields the detail result already returns.

## Consequences

- The ADR 0022 add-lane candidate list now **excludes** the authored-in
  language (TD·4); the "Add a <authored-in>" offer disappears.
- The ADR 0018 tag gains a **visible home** on the detail surface — it is the
  first, always-present, default-visible variant, not just a stored tag.
- The post/reply detail views render **one variant container per language**
  (the `<details>` panels are replaced by the chip + variant model on this
  surface); the swap is progressive enhancement (JS-off still shows the
  original).
- Soft-deleted rows (ADR 0024) show their placeholder and **no** chip row /
  swap.
- **Zero** migration, **zero** new document, **zero** new read seam — the
  field already exists (ADR 0018); the shared `LanguageOption` record and the
  ADR 0022 write lane (standing + route actions) are unchanged.
- **Amends 0018**: the authored-in tag's meaning is extended from "a stored
  tag" to "the first, always-present, default-visible variant on the detail
  surface." ADR 0005 §C, ADR 0022, and the M4/M5/M6 roadmap letters are
  untouched.

## Amendments

### 2026-09-19 — default-visible variant is the viewer's current language (ADR 0049)

Decision (a)'s "default-visible" clause is amended: the **default-visible**
variant on the post/reply detail surfaces (and, mirroring, the announcement
detail surface and the pinned-announcement banner) is now the **viewer's
current language** — the ADR 0005 §B resolution order as amended by ADR 0046
(cookie → `Accept-Language` match → instance default → `en`) — **when a
user-added translation of the item exists in it**; otherwise the authored-in
variant stays default-visible exactly as locked above. The rest of (a) is
unchanged: every variant remains in the DOM, the chip row is the same, and
the click-to-swap + guaranteed path back (now starting from a different
initial variant) is untouched. Display-only; zero Core / schema / view-model
change; the JS-off degradation path is preserved. See **ADR 0049** for the
full locked text.
