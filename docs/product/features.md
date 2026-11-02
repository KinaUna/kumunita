---
id: product-features
title: "Features"
tier: living
kind: guide
status: current
audiences: [resident, community-admin, developer]
related:
  - README.md
  - docs/product/roadmap.md
  - docs/philosophy/START-HERE.md
lane: improve-docs
updated: 2026-11-02
---

> **Abstract:** the current feature surface of Kumunita, one bullet per
> capability — what the platform does today. This was the `## Features`
> section of the front-page [`README.md`](../../README.md); it now lives here
> so the front page can stay a ≤120-line door. **Who is it for:** anyone who
> wants the plain-language list of what the platform can do (residents,
> admins, developers). **When to read the rest:** when you want the full
> feature list rather than the front page's one-paragraph summary.

# Features

- Resident directory (profiles, opt-in contact details)
- Announcements & discussions, organized by **functional components** (Safety, Maintenance, Social, Governance, …)
- Events with RSVP and reminders *(M4 — done, ADR 0054; see the [Roadmap](roadmap.md))*
- Collaborative projects (goals, tasks, contributors) *(M5 — done, ADR 0067; see the [Roadmap](roadmap.md))*
- **Groups** — public groups power reusable access lists; **private groups**
  (ADR 0010) are a membership/organizing unit for a family or circle, and stay
  out of the audience pickers
- **Delegation** — owners grant family/caretakers scoped access
- **Guardian controls** — a parent adds an account for a child and supervises it
  at the account level: suspend/lock, curate the child's community & group
  memberships, and approve a group invitation sent to the child — with **no
  standing to read the child's private content** (ADR 0028; the child's account
  is handed over to independence when the child comes of age)
- **Avatars & media** — profile avatar upload + serving on a content-addressed
  local-volume byte store behind an HTTP-free seam (ADR 0011). Group logos and
  badge icons are **follow-on lanes reusing the same seam** — each with its own
  design doc, not this one. (Post / reply / announcement attachments have
  shipped — the **File attachments** bullet below.)
- **File attachments** — attach a file (PDF / Office docs / text / csv / zip +
  the raster image types) to a **post, group post, reply, or
  announcement**: a body link `[label](/attachment/{id})` that **downloads**
  (`Content-Disposition:
  attachment`, not an inline render) on the **same** content-addressed byte
  store as images, under a **separate** file allowlist (SVG excluded) and the
  owning resource's `Read` decision (a reply resolves its parent post's); an
  "Attach file" toolbar button on every composer (ADR 0034). Follow-on lanes
  (own design doc + ADR): attachments on static/about pages; video/audio;
  in-browser preview.
- **Group posts** — the post channel *inside* a group (`/groups/{id}/posts`):
  a membership-scoped feed, detail, composer and replies (ADR 0013). Members
  only — a non-member (moderator or admin alike) neither sees nor posts; the
  audience lane is never evaluated, and replies inherit the parent's single
  membership decision.
- **Group events** — the event channel *inside* a group
  (`/groups/{id}/events`): a membership-scoped feed, detail, composer and
  RSVPs, exactly the ADR 0013 membership lane applied to the M4 event surface
  (ADR 0089, the `GE` lane). A group event is always a draft until its author
  publishes it; members only (a non-member — moderator or admin alike —
  neither sees nor creates one); edit and publish are author-only (no
  GlobalAdmin override, no break-glass); RSVPs and reminders inherit the M4
  lane as-is; and group events never reach the community feed or calendar.
- **Tags** — free author-set **subject labels** on posts (community + group)
  and blog pages: a lightweight, multi-valued, non-access label that closes
  the "what is a post about" gap (a component is *where* a post lives, a tag
  is *what it's about*). Display names are per-language and creator-owned —
  the creator (or a `GlobalAdmin`) may reword a tag, a later attacher may
  not. One **access-scoped** read seam serves the tag list, the by-tag
  browse, and composer autocomplete; a tag is computed over content a viewer
  may already read, so a tag used only on unread content never surfaces
  (ADR 0044).
- **Drafts** — a post, group post, or announcement can be **saved but not
  made public yet**: a "Save as draft" option on every composer. A draft is
  the author's private scratchpad — excluded from every feed and the pinned
  list, invisible to everyone **including a `GlobalAdmin`** who is not the
  author (no audit row, not subject to the audience decision), and visible
  only to its author, who can edit it, find it at `/my/drafts`, and publish
  it with one action (publishing is author-only too). Editing a draft never
  publishes it (ADR 0037).
- Moderation with component-scoped moderators and full audit
- **Multilingual** — UI and platform texts (terms, help) are translatable,
  resolved per request (user preference → instance default → `en`, with
  per-string / per-page fallback). The admin manages the language catalog +
  instance default in `/admin/languages` (audited), and residents pick their
  language on the settings page (a cookie, never a claim). A non-English-speaking
  neighborhood can run its platform in its own language with no code change or
  deploy (ADR 0005). The `ML-UI` lane (ADR 0015) then makes it **real in the
  UI**: a resident **sees** the platform in their language (every in-scope view
  resolves per request, with a seeded `en` floor that is always present), the
  admin edits the **closed** key list per language (no hand-typed key), a
  **signed-out** visitor can pick a language at `/language`, and `/about` renders
  an admin-authored page or the product story. The `TR` lane (ADR 0021) then
  delegates the editing: a GlobalAdmin can grant a **`Translator`** role to a
  resident, who may update and add the UI strings and static pages but holds
  none of the GlobalAdmin's other standing (the catalog — add / enable /
  reorder / set-default / remove — stays GlobalAdmin-only). The `LS` lane
  (ADR 0042) then ships a **bundled initial pack**: a first-boot instance has
  German and French enabled with complete UI-string and about/terms/help
  baselines (English stays the default and the only code-owned language);
  per-string fallback still lands on the en floor. The `SP` lane (ADR 0043)
  then ships the **platform-page set as five surfaces** — About / Terms / Help
  / Privacy / Code of conduct — complete in en/de/fr and reachable from the
  footer + the admin shell.
- **Timezone** — the platform carries a **default time zone** the admin sets
  once (`/admin/timezone`, audited); each resident can override it in their
  own settings page (the time-zone section of `/settings/language`). Every
  timestamp renders in the
  effective zone (resident override → platform default → `UTC` floor) via the
  `kw-dt` TagHelper — the same per-request resolution shape as multilingual,
  data-driven, no rebuild (ADR 0019).
- **Date & time format** — the platform carries a **default date-time format**
  the admin sets once (`/admin/dateformat`, audited) and each resident can
  override in their own settings page (the date-format section of
  `/settings/language`). A short preset list (Long / Short / ISO / Day-first)
  plus a free-text **custom** format; the unambiguous Long format is the floor.
  Every timestamp renders in the effective format (resident override → platform
  default → Long floor) via the `kw-dt` TagHelper — the same per-request
  resolution shape as timezone and multilingual, data-driven, no rebuild
  (ADR 0020).
- **Search** — one nav search box (anonymous + signed-in) over the **ten**
  resident content surfaces: posts (community **and** group), events
  (community + group), pages, and announcements (the original four, ADR 0091)
  **plus** projects, boards, todos, inventory, documents, and people (the six
  extended by ADR 0124). `/search` renders `all`
  (top 5 hits per surface — a search-box answer, no pager) or a single-surface
  paged list on the M7 `HasMore` / `_Pager` discipline (pager links carry
  `q` + `surface` + `scope`); the `groups` scope is signed-in-only and rides
  the **frozen** ADR 0013 group seams (a group you can't see contributes no
  hit and no count; anonymous silently degrades to community — no 403).
  The six extended surfaces are signed-in-only (their canonical feeds are
  gated; anonymous sees the original four). Drafts, soft-deletes, and hidden
  content are never hits; every decided visit audits (one aggregate row per
  (query, surface, scope), `TargetKind = "search:<surface>"`); the render
  surface is hits + `HasMore` only — never a count. Case-insensitive substring
  match over authored-in `Title` + `Body`, **and** the names of a document's
  own tags (a tag is a label, never a gate — C-TG·1); the people surface
  matches `DisplayName` + `Bio` + profile tags. Zero schema change;
  language-scoped search and `tsvector` full-text are the named deferred
  lanes (ADR 0018's own consequence; ADR 0091 + ADR 0124).
