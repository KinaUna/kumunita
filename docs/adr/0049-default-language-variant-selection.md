# ADR 0049 — Default-visible variant: the viewer's current language (when a translation exists)

Status: Accepted
Date: 2026-09-19
Amends: **0005 §C** (the "never automatic" clause for UGC display) and
**0027** (the "always default-visible" clause for the authored-in
variant). Additive to **0046** (the `Accept-Language` candidate chain,
which this reuses) — it does not change how the current language is
*resolved*, only how a translation variant is *selected* once resolved.

## Context

ADR 0005 §C locked a strong display rule: **UGC is always rendered as
authored, and its display is never auto-switched by the
`kumunita.locale` cookie**. ADR 0027 built the post/reply translation
detail surface on top of that: the **authored-in language is the first,
always-present, default-visible variant**; every user-added translation
is a hidden variant reached only by an **explicit click** on its chip
(never auto), with a guaranteed path back to the original. ADR 0029 /
0032 extended the same shape to announcements and the pinned banner.

The result is correct but *inert* for the common case: a resident whose
browser speaks `pl` opens a post that **already has** a Polish
translation (added by a human, ADR 0022/0026/0029 lanes — no machine
translation is involved, and none is ever), and still reads the
original. The translation exists on the page, fully rendered, one click
away — but the one-click detour is the norm, and the Polish text is the
exception. In a single-neighborhood community where the platform's whole
point is that *this* neighborhood reads *its* language, the "never
auto" rule — written in 2026-08-27, before any UGC translation lane
existed — now costs more than it protects: the machine-translation
concern that motivated it (ADR 0005 §C) is still fully enforced,
because **nothing is machine-translated anywhere**; the "never
automatic" clause is only *preventing a human's own translation from
being the first thing a reader sees*.

The instance already resolves a per-request "current language" for
platform text (ADR 0005 §B resolution order, amended by ADR 0046:
cookie → `Accept-Language` match → instance default → `en`). UGC
variants already exist in the DOM, rendered server-side, labeled, and
swappable. What is missing is only the *default selection*.

## Decision

- **The default-visible variant on a translation-bearing surface is the
  viewer's current language when a translation of the item into that
  language exists; otherwise it is the authored-in variant — exactly as
  before (the ADR 0027 floor).** "Translation-bearing surface" = the
  post, reply, and announcement **detail** views (community + group
  lanes) and the **pinned-announcement banner**. The current language is
  the *same* per-request resolution the platform text already uses —
  the `kw-l` chain (ADR 0005 §B + ADR 0046): saved cookie preference →
  browser `Accept-Language` matched against the enabled catalog →
  instance default → `en`. One Web helper
  (`Kumunita.Web.Security.EffectiveLanguageCode`) implements that chain
  against `ITranslationProvider.ResolveEffectiveLanguageAsync`, so the
  UGC default and the platform-text default can never disagree for the
  same request.

- **This is a display-only default selection. It is not, and does not
  become, a translation.** No content is generated, rewritten, or
  fetched. The variant shown is the **exact** human-authored
  translation row already stored (ADR 0018/0022/0026/0029) and already
  in the DOM. ADR 0005 §C's machine-translation deferral is untouched:
  there is still no MT, no provider boundary, no third-party send.

- **The ADR 0027 machinery is untouched.** All variants remain in the
  DOM; the chip row is unchanged; click-to-swap and the guaranteed path
  back to the original (and to any other variant) work exactly as
  before — they now just start from a different initial variant. With
  JS disabled the page degrades to whichever variant is
  default-visible; a viewer with no matching translation sees the
  original, exactly as before.

- **The pinned-announcement banner shows the translation** (title when
  present, otherwise the original title; body preview) of the pinned
  announcement in the viewer's current language, falling back to the
  authored-in title/body when no such translation row exists. It reuses
  the existing `GetAnnouncementTranslationsAsync` read seam — no new
  seam, no new read.

- **Zero Core / schema / view-model / controller change.** The
  resolution lives in the Web layer, per request, from data the views
  already have (or already fetch, for the banner). No migration, no new
  document, no new read seam, no new standing rule.

## Consequences

- A resident whose current language has a translation of a post/reply/
  announcement now **reads it first**, with one click to see the
  original — the detour is no longer the norm.
- The "never auto" clause is **re-scoped, not removed**: it still
  means "never *machine*-translate, never show *generated* text, never
  hide the original". What it no longer means is "a human's
  translation of this item may never be the first thing a matching
  viewer sees." The original remains one click — or one JS-off render
  — away on every surface.
- ADR 0005 §C and ADR 0027 are amended as recorded above; the
  ADR 0046 resolution chain gains a second consumer (UGC variant
  selection) without its own semantics changing.
- Surfaces that do *not* carry the variant/chip machinery (post and
  reply **list** previews, group and community **index** cards) were
  **out of scope** here and rendered as authored. ADR **0051** has now
  extended this default to the three list surfaces — the `/announcements`
  list, the `/community/{id}` feed, and the `/community` all-sections feed
  (including the community name each shows) — so they no longer render as
  authored when a translation of the viewer's language exists. The group
  **index** cards and the community **pill** directory remain out of scope
  (separate lanes, ADR 0051's scope boundary).
