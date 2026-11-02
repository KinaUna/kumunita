---
id: adr-0039
title: "Pages: a hierarchical, audience-restricted, translatable knowledge tree (absorbs the static-page lane)"
tier: reference
kind: adr
status: accepted
audiences: [developer, agent, platform-maintainer]
related: [docs/design/m8-search-design.md]
supersedes: [adr-0005]
superseded_by: [adr-0040, adr-0057, adr-0165]
updated: 2026-11-02
---

# ADR 0039 — Pages: a hierarchical, audience-restricted, translatable knowledge tree (absorbs the static-page lane)

Status: Accepted
Date: 2026-09-17
Supersedes: **0005 A** (the static-page lane — *absorbed* into this tree,
retired last in U07).

> **Abstract:** the platform's one resident-facing long-form prose surface —
> the flat, admin-only, **public-by-construction** static-page lane (ADR 0005
> A: `LocalizedPage`, slugs `terms` / `help` / `about`) — is **absorbed into
> a single unified knowledge tree**: a new **`Kumunita.Core.Pages`** bounded
> context (`Page` + `PageTranslation`, the ADR 0022/0026/0029 shape) carrying
> **hierarchy** (`ParentId` forest + a derived, not stored, path), **audience
> restriction** (the post `Audience` reused verbatim — `null` = public, the
> frozen `Decide()` branch 5), **mount points** (a nullable `MountPoint` tag —
> what makes "the about page is in `footer/community`" expressible without a
> special case), **translatable UGC rows**, the **`PageToAuditableResource`**
> adapter (the only new authorization surface), and a **standing matrix**
> (GlobalAdmin ∪ community-Moderator, with the author ∪ Translator branches
> on the translation / draft lanes). The fork this settles: **absorb** the
> existing lane into one tree (user sign-off 2026-09-17), **not** run a
> parallel `Page` context. **No new `AudienceMode`, no new `AccessVia`, no
> new `AccessAction`** — the frozen `Decide()` already covers every standing
> a page needs. **For** a developer touching `Kumunita.Core.Pages`, the
> `PageService` seam, the `PageController` surface, or the `tests/*` pages
> families. The field sets, the hierarchy mechanics, the standing-matrix
> table, the Web surface, the 4-step absorb migration, and the `PG` lane pin
> are in the [handoff
> note](../design/adr-handoffs.md#adr-0039--pages--a-hierarchical--audience-restricted--translatable-knowledge-tree-moved-detail).

## Context

The platform ships **one** resident-facing surface for long-form, admin-
authored prose: the **static-page** lane (ADR 0005 A) — `LocalizedPage`
(flat slugs), served by `StaticPagesController` over
`ITranslationProvider.GetPageAsync`, written by the admin/translator-only
`UpsertPageAsync` seam, **public by construction** (no audience, no
authorization, no `AccessAudit`), editable in the WYSIWYG editor (ADR 0031 /
0033). It is **flat** (no hierarchy), **admin-only** (a community Moderator
has no standing to add a "how the platform works" page), and **not audience-
restricted**.

The requirement is a **knowledge tree**: (1) **seeded default pages** that
ship with translations and are editable in the WYSIWYG editor; (2) **admin-
and community-Moderator-authored pages**; (3) **audiences like community
posts** (private / community-visible / public); (4) **a hierarchy** (pages
nest like folders/files, listed as a tree; **special root nodes** name *where
a page appears in the UI* — `footer/community`, `help/account`); (5)
**referenced from UGC** (a page's URL is a Markdown link in a post/reply/
announcement body — **free**: `MarkdownRenderer` already renders `[label](url)`
links and `IsSafeUrl` already accepts relative paths).

**The fork that determined this ADR:** absorb the existing lane into one
unified tree, or run a *parallel* `Page` context alongside `LocalizedPage`.
**Absorb was chosen** (user sign-off 2026-09-17): the requirement is *one*
tree, and a parallel lane would give two editors, two renderers, two
translation lanes, and a `/about` that is special-cased forever. Absorbing
makes `/about` an *ordinary page* with a mount point. The absorb is
**additive, not destructive** — the old `LocalizedPage` surface is
**retired last** (U07), after the new surface is proven (the repo's "no
destructive step until green" discipline).

## Decision

- **A new bounded context `Kumunita.Core.Pages`** — `Page` (the body/
  structure of a page: `ParentId?` hierarchy, `Slug` (unique per parent),
  `Title`, `Body`, **`Audience?`** (the **existing** post `Audience` — *not*
  a new scope; `null` = public), `AuthorId`, `ComponentId?` (moderator
  scoping), `LanguageCode` (ADR 0018), `MountPoint?`, `Created` / `Modified?`,
  `IsDraft` (ADR 0037), `ImageIds` (ADR 0025), `AttachmentIds` (ADR 0034)) +
  `PageTranslation` (a user-added translation row, the ADR 0022/0026/0029
  shape, `(PageId, LanguageCode)` unique index). See the handoff note for the
  full field sets.
- **The audience is the post `Audience`, reused verbatim** (ADR 0001-B / 0036):
  `null` = public (`Decide()` branch 5, *unchanged*); non-null = `Mode` +
  `Grants` + the `Community` flag. **No new `Audience` field, no new
  `AudienceMode`, no new `AccessVia` value.** The **default** for a new page
  is **non-public, community-visible** (`Audience.Community = true` + a
  `ComponentId`, empty grants) — **consistent with posts** (ADR 0036) —
  ~~originally "pages default public"~~, **reversed 2026-09-17** (a *per-
  surface composer choice*, not a change to the `Audience` doc).
- **The decision path is the frozen `IAuthorizationService`.** A new
  **`PageToAuditableResource`** adapter (mirrors `PostToAuditableResource`
  verbatim: `Id` / `Name` / `OwnerId = AuthorId` / `Audience` (**null
  allowed**) / `ComponentId` / `TargetKind = "page"`) presents a `Page` to the
  **frozen** `CanAsync(…Read…)` / `CanSeeAsync(…Read…)` seams. **No new
  `AccessAction`**, **no new authorization branch** (`PG` adds an *adapter*,
  not a *branch*), **no new `AccessVia`** — the adapter is the **only** new
  authorization surface in this lane.
- **The hierarchy is a forest of `Page` docs, nested by `ParentId`, with a
  derived (not stored) path.** A page's canonical path is the chain of
  ancestor `Slug`s; the tree is a **forest** (any number of roots). The
  `(ParentId, Slug)` unique index enforces one page per slug per parent; a
  **cycle-guard + depth-cap** (e.g. 8) is validated in the write lane (the
  `PageService`), not a schema constraint. **Renaming a page** rewrites the
  *derived* paths of its subtree (a `Slug` change is a single-column write);
  **references from posts break by path, not by id**. See the handoff note
  for the full mechanics.
- **Mount points are pages, not a second mechanism.** `footer/community` and
  `help/account` are ordinary pages in the tree **whose `MountPoint` field is
  set**; the UI slot resolves "the page whose `MountPoint == 'footer/
  community'`" and renders / `<a href>`s it. `MountPoint` is a **display**
  concern (where to surface a link), **not** an access boundary — access is
  always the `Audience` + `CanAsync(Read)`.
- **Translatable UGC row shape, reused verbatim.** A `PageTranslation` row
  carries a user-added translation; **standing** = GlobalAdmin ∪ Translator
  (ADR 0021/0022) **plus the community-Moderator lane for a community-scoped
  page** (the ADR 0029 announcement lane, carried over); **add-only** (ADR
  0022); **display** = the ADR 0027 chip-swap, reused verbatim. No new
  display rule.
- **The standing matrix** (enforced server-side in the `PageService`, the
  `AnnouncementService.CreateAsync` C3 pattern; the Web `[Authorize(Roles=…)]`
  is a convenience pre-gate, not the source of truth): **Create** — GlobalAdmin
  ∪ (community Moderator); **Edit** body / audience / hierarchy — the `AuthorId`
  ∪ GlobalAdmin ∪ (a Moderator of its `ComponentId`); **Add a translation** —
  GlobalAdmin ∪ Translator ∪ (community Moderator); **Publish** a draft — the
  `AuthorId` only (ADR 0037); **Move / rename / delete** — GlobalAdmin ∪ (a
  Moderator) — *not* a plain author (a page is platform content, not a personal
  note). **Every write lane** stores its `AccessAudit` row (C3),
  `TargetKind = "page"`, `Action` `page.create` / `page.update` /
  `page.translation.add` / `page.publish` / `page.delete` / `page.move`. See
  the handoff note for the full table.
- **Delete is a soft-delete** (`IsDeleted = true`, the ADR 0024 shape),
  filtered in the read lanes — an accidental delete of a seeded page is
  recoverable and a page's children are not orphaned mid-tree. A hard delete
  is a future lane if needed.
- **Reference-from-UGC is free.** A page is referenced from a post/reply/
  announcement body as a plain Markdown link `[About](/pages/about)`;
  `MarkdownRenderer` + `IsSafeUrl` already handle it, so **the read path needs
  zero changes**. The lane *adds* the serving route `GET /pages/{path}`
  (audience-gated by `CanAsync(Read)`).
- **The Web surface** — `PageController`: `GET /pages` (the tree browse,
  `CanSeeAsync(Read)`-filtered), `GET /pages/{**path**}` (the post view,
  `CanAsync(Read)`-gated, 404 on absent / 403 on denied), `GET /pages/new` +
  `POST` (compose), the edit lane, and the standing-gated `publish` / `delete`
  / `move` actions. **`StaticPagesController` becomes a thin reader over the
  tree** — `/terms`, `/help`, `/about` resolve to the seeded page at that path
  (or the `/about` product-story fallback); the three hard-coded routes are
  **kept** but **read from the tree** (one store, not two). The editor reuses
  `Views/Languages/PreviewPage.cshtml`'s `bindRichEditor` + RC + ATT wiring.
  See the handoff note for the full surface.
- **The data migration (absorb)** — (1) create the `Page` + `PageTranslation`
  tables (ADR 0004 §B.1); (2) **seed the three default pages** (`about`,
  `terms`, `help`) as `Page` docs carrying the *current* `LocalizedPage` `en`
  bodies, `Audience = null`, `MountPoint` set, **idempotent**; (3) **keep
  `LocalizedPage` read-only + non-destructive for one release** (the old seam
  still works, so the old fallback is *available* if the new route misbehaves);
  (4) **delete the `LocalizedPage` surface** in U07, once the new route is
  green. See the handoff note for the full sequence.
- **The lane is `PG`** (a named lane, the `ML` / `GP` / `RC` / `RE` convention
  — a short ID, *not* a renumber). M4 / M5 / M6 stay Events / Projects /
  Portability; no roadmap letter moves. See the handoff note for the `PG`
  lane pin.

## Consequences

- **One page engine, one editor, one translation lane, one decision path** —
  the lane *unifies* instead of *duplicating* (the whole point of absorbing
  `LocalizedPage`); the "single page engine" (ADR 0005 A) now covers a tree,
  not three slugs.
- **The audience machinery is reused end-to-end** — a page gets
  private/community/public visibility, the correct `AccessVia` audit tag, and
  delegation for free (the `Decide()` algorithm is unchanged; only the
  `PageToAuditableResource` adapter is new).
- **`/about`, `/terms`, `/help` keep working** (now backed by the tree) and
  become *editable, translatable, hierarchy-aware, audience-capable* — a
  strict superset of today.
- **The "mount point" concept is new and small** (one nullable string column +
  one resolver), and it makes the "about is in `footer/community`" requirement
  expressible without a special case.
- **The default-audience now matches posts (amended 2026-09-17)**; the standing
  matrix is the existing role matrix re-checked server-side (no new role, no
  new `AccessVia`, no new `AccessAction`); reference-from-UGC is free; and the
  **`LocalizedPage` retirement is the last unit and the only destructive
  step** (a fresh instance's routes are byte-identical to today — the seeded
  pages carry the same `en` bodies). See the handoff note for the full
  consequence list.
