# ADR 0051 — List surfaces: the default-visible variant is the viewer's current language

Status: Accepted
Date: 2026-09-20
Amends: **0049** (its "out of scope" clause — post and reply **list** previews and
the group / community **index** cards — is now extended to the
`/announcements` list, the `/community/{id}` and `/community` feeds, and the
community name shown on them). Additive to **0029** (announcement
translations), **0022** (post translations), and **0026** (community
name/description translations) — the human-authored rows it selects already
exist; this ADR only chooses which one is default-visible in the lists.

## Context

ADR 0049 made the **default-visible variant** of a translation-bearing item
the viewer's current language (the `kw-l` chain: `kumunita.locale` cookie →
`Accept-Language` match → instance default → `en`) *when a translation of the
item into that language exists*, falling back to the authored-in variant. It
shipped for the **detail** views (post, reply, announcement) and the
**pinned-announcement banner**, and it deliberately *excluded* the list
surfaces:

> Surfaces that do not carry the variant/chip machinery (post and reply
> **list** previews, group and community **index** cards) are **out of scope**
> and still render as authored — extending the default there is a separate
> lane if ever wanted.

In practice the lists are the *first* thing a resident sees, and the
inconsistency is exactly the one that reads as a bug: a resident whose
preference is `da` opens `/announcements` and the pinned banner already
shows the Danish body (ADR 0049), yet the list rows below it show the
English originals — while the very same announcement's *detail* page shows
Danish. The translation is a first-class, human-authored row (the ADR
0029/0022/0026 lanes); nothing is machine-translated (ADR 0005 §C still
holds). The one-click detour to the original that ADR 0049 removed from the
detail views is still the norm on the lists.

The rows the lists need are already stored and already read (the detail and
banner paths call the same read seams: `GetAnnouncementTranslationsAsync`,
`GetPostTranslationsAsync`, `GetCommunityTranslationsAsync`). Only the
*selection* is missing.

## Decision

- **The `/announcements` list, the `/community/{componentId}` feed, and the
  `/community` all-sections feed show each item in the viewer's current
  language when a translation of it into that language exists; otherwise the
  authored-in text, exactly as before (the ADR 0029/0022/0026 floor).** The
  "current language" is the *same* per-request resolution the platform text
  already uses — `Kumunita.Web.Security.EffectiveLanguageCode.ResolveAsync`
  (the one ADR 0049 added and the banner already calls) — so the list's
  default, the detail's default, the banner's default, and the `kw-l` UI-text
  default can never disagree for the same request.

- **The selection is per item and per field, with the same fallback the
  detail surfaces use:**
  - *Announcement* (the `/announcements` list row): the matching translation's
    **body** replaces the row's preview; its **title** replaces the row's
    title *unless it is blank*, in which case the authored title is kept (a
    translation title is optional, ADR 0029).
  - *Post* (both feed rows): the matching translation's **body** feeds the
    preview; its **title** replaces the row's title *unless blank* (ADR 0022).
  - *Community name* (the `/community/{id}` feed header and the per-row
    section badge on `/community`): the matching translation's **name**
    replaces the authored name *unless blank* (ADR 0026). The community
    pills' display names are the *reachable* set and are out of scope for
    this ADR (the ADR 0026 directory is a separate surface).

- **Display-only, read-only.** No content is generated, rewritten, or fetched.
  The item shown is the exact human-authored translation row already stored.
  The list rows' read is "a read, not a decision": the visibility decision
  (`ListVisibleAsync` / `ListFeedAsync` / `ListAllFeedAsync`) already ran in
  the service; the translation read inherits it. No `AccessAudit` row is
  added by the selection.

- **Zero Core / schema change.** The change is entirely in the two Web
  controllers (`AnnouncementController.Index`, `PostsController.Index` +
  `AllSections`) plus two private helpers on `PostsController`. They resolve
  the effective language once per request and, per row, pick among the rows
  the existing read seams already return. No migration, no new document, no
  new read seam, no new standing rule.

- **The list selection is *not* the ADR 0027 chip machinery.** The lists do
  not render variant chips or a swap affordance; they render a single
  default-visible variant. The original is still one navigation away (the
  detail view, which carries the full ADR 0027 chip row and path back to the
  original), so a resident never loses the authored text — matching ADR 0049's
  "the original remains one click away" guarantee.

## Consequences

- A resident whose current language has a translation of an announcement or
  post now reads it **first** on the lists too, consistent with the banner
  and the detail page — the banner/list/detail trio no longer disagrees.
- ADR 0005 §C's machine-translation deferral is untouched: there is still no
  MT, no provider boundary, no third-party send. The "never automatic"
  re-scoping ADR 0049 made now simply extends to the list surfaces as well.
- The lists' data source is unchanged: the same service read, the same row
  set, the same visibility gate. Only which pre-rendered row is shown is
  different, and only when a matching translation exists.
- **Scope boundary (intentional):** this ADR covers the three list surfaces
  named above and the community names they display. It does **not** extend
  ADR 0049 to the group **index** cards (groups do not carry the ADR 0029/0022
  item-translation rows in their index cards today), to the community **pill**
  directory, or to any write lane. Those remain separate lanes.
- `AnnouncementController` and `PostsController` gain an optional
  `ITranslationProvider` dependency (DI always supplies it). It is optional so
  the existing `AnnouncementController` test-construction sites — which assert
  the as-authored fallback, their original intent — keep compiling untouched.
