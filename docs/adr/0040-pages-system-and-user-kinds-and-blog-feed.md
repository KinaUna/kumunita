# ADR 0040 — Pages: system vs user (blog) kinds, and the per-resident blog feed

Status: Accepted
Date: 2026-09-17
Amends: **0039** (the Pages lane standing matrix, §3.7 — the
"community-Moderator-authored pages" and "Moderator-scoped write lanes" are
**retired**; the standing matrix is re-shaped into a **two-kind** matrix),
**0006** (the frozen `CanAsync` / `CanSeeAsync` signatures are untouched —
the `PageToAuditableResource` adapter from ADR 0039 is unchanged; what
changes is *which standing* the `PageService` write lanes enforce before
that adapter is reached). Additive on **0004** (the `Kind` field is a new
additive document column — delta-detected, idempotent, **zero
migrations** — the ADR 0004 §B pattern) and on **0037** (the draft idiom is
carried onto blog pages unchanged; a blog page's draft is still its
author's).

This ADR is a **follow-on refinement within the open `PG` lane** (ADR 0039),
not a new lane and not the "close the lane" step. It settles the three
requirements raised after the PG surface shipped: (1) the **composer's
parent picker must differentiate** between platform (system) pages and a
resident's personal (blog) pages; (2) a **GlobalAdmin may add new
sub-levels under a `system/` root** (e.g. `system/about`, `system/help`);
and (3) a **per-resident blog feed page** that lists a resident's own pages,
so a page can be the thing a post links to.

## Context

ADR 0039 shipped one unified tree and one standing matrix, but it was
shaped for a **single kind** of page — a platform/authored knowledge page —
and its standing matrix (A0039 §3.7) gave a **community Moderator** a lane
on every page (create, edit, move, delete, translate) *and* gave a plain
**author** a move/delete lane (a page was "platform content, not a personal
note"). That model cannot express the three new requirements:

1. **Two disjoint page kinds are needed.** A platform page
   (`system/about`, `system/terms`) is not the same *thing* as a resident's
   personal page (a "blog" entry). They differ in **who may author**,
   **who may edit**, and **who they belong to**. ADR 0039's single matrix
   (Moderator ∪ author ∪ GlobalAdmin) was a superset that let a Moderator
   author a "how the platform works" page *and* let any resident's personal
   page be managed by the community — neither is wanted (see the Decision).
2. **The parent picker is the UX seam.** A resident creating a blog page
   must be offered *their own* blog's pages as the nestable set (and only
   those); a GlobalAdmin adding a system sub-page must be offered the
   *system* namespace (and only that). ADR 0039's picker offered every page
   in the tree to every actor — which is exactly the leakage the two kinds
   must prevent.
3. **A blog needs a front door.** A resident's pages are personal content;
   there is no platform page to list "all blog posts" (a blog is a
   resident's, not the platform's). A `/blog/{userId}` feed — the resident's
   own `Kind = User` pages, newest-first, each linking to its
   `/pages/{path}` — is the resident's blog home and the anchor a post
   links its pages to.

**The fork that determined this ADR:** one page kind with a richer standing
matrix (ADR 0039's shape extended) versus **two explicit kinds** with a
kinds-aware standing matrix. **Two kinds was chosen** (user sign-off
2026-09-17): the two kinds differ in *standing*, not just in *content*, so
an explicit discriminator (a `Kind` field) makes the standing a pure
function of `(kind, author, actor)` and makes the parent picker a pure
function of `kind` — no per-call ad-hoc reasoning. It also makes the
"no Moderator lane on either kind" decision (the ADR 0039 matrix's single
biggest change, below) *expressible* — a Moderator's standing is scoped to
a community, and neither a platform page (no community) nor a personal page
(personal content) is community content.

**The standing matrix the user confirmed** (2026-09-17, verbatim):
> "1. You're right, only the user themselves should be able to create a
> page for their blog root and own existing pages. 2. Only admins,
> moderators should not be allowed to change system pages."

Read together with the ADR 0039 §3.7 context (a Moderator's standing is
*community-scoped*), the intent is: **a Moderator has no page standing at
all** — a system page is platform content (a Moderator moderates a
community, not the platform), and a blog page is personal content (a
Moderator moderates a community, not a resident's private notes). The
Moderator lane is **retired from pages entirely**; it is not re-granted for
the new `User` kind.

## Decision

- **A new additive field `Page.Kind` (a `PageKind` enum — `System` /
  `User`)** on the existing `Page` doc, **defaulting to `System`**
  (the ADR 0004 §B pattern — a new column, delta-detected, idempotent,
  zero migrations). It is the **standing discriminator**: every page
  standing decision in this ADR is a pure function of `(Kind,
  AuthorId, ComponentId, actorId, actorRoles)`. A `System` page is
  platform content; a `User` page is a resident's personal content.

- **The standing matrix, re-shaped to two kinds** (enforced
  server-side in `PageService`, the C3 single-source pattern; the Web
  `[Authorize]` / pre-gate is a convenience, not the source of truth). A
  community **Moderator has no standing on either kind**:

  | Action | `System` page | `User` (blog) page |
  | --- | --- | --- |
  | **Create** | **GlobalAdmin only** | **any signed-in actor** (becomes the author; the ownership guard in the Decision keeps it under their own blog root) |
  | **Edit** (body / audience / hierarchy) | **GlobalAdmin only** | **author ∪ GlobalAdmin** |
  | **Move / rename / delete** | **GlobalAdmin only** | **author ∪ GlobalAdmin** (the ADR 0037 author-only publish pin is unchanged and does not extend to move/delete of a system page) |
  | **Add a translation** (ADR 0029 carried over) | **GlobalAdmin ∪ Translator** | **GlobalAdmin ∪ Translator** |
  | **Publish** a draft | author only (ADR 0037) | author only (ADR 0037) |

  The **Moderator lane from ADR 0039 §3.7 is retired** on both kinds —
  the matrix above has no `AccessVia.Moderator` row. The `AccessVia`
  tags the audit rows carry are `Owner` (a blog author, the narrowest
  standing) or `Admin` (a GlobalAdmin); `Moderator` is never returned for a
  page write.

- **Two namespace guards, both enforced server-side (C3) on create and
  move:**
  1. **The namespace guard** — a page may nest **only under a parent of the
     same `Kind`**. A `System` page under a `User` page, or the reverse, is
     a structural error: the two namespaces are **disjoint by design**. The
     guard walks up from the parent to the root and checks the root's `Kind`
     matches the page's `Kind`.
  2. **The ownership guard** — a `User` (blog) page may nest **only under
     the actor's own blog root**. A resident cannot nest a page under
     *another* resident's blog (the user's "only the user themselves"
     constraint, sign-off item 1). The guard walks up to the parent's root
     and requires `root.AuthorId == actorId`. A root-level blog page is the
     actor's own blog root by construction (the write lane sets
     `AuthorId = actor`), so the guard is a no-op for it.

- **The root-slug collision guard is scoped by `(Kind, AuthorId)`** (the
  ADR 0039 guard, narrowed): Postgres treats `NULL` `ParentId`s as distinct,
  so the unique index does not cover two roots sharing a slug — this lane
  does. Scoping by `(Kind, AuthorId)` lets **two residents both have a root
  blog page slugged, e.g. `"recipes"`** (their `blog/{uid}` namespaces never
  overlap), while a single resident is still capped at one such root (a
  second top-level page of theirs nests under the first). A `System` root
  collides only with another `System` root — the `system/` namespace is
  singular.

- **Admin sub-levels under the `system/` root.** A GlobalAdmin may create a
  `System` page that is a **root** (a new top-level system page) *or* a
  **child of the `system/` root** (a sub-level, e.g. `system/about`). The
  seeder (the Decision below) creates the `system` root (a `Kind = System`
  container page) and seeds `terms` + `help` **under** it; a fresh
  instance's `/about` is still the product-story view (the ADR 0039 U05
  drift pin — `about` is admin-created at runtime, not seeded). Admin
  sub-levels are then a *create* (`ParentId = the system root`) and a
  standing check (`Kind = System` → GlobalAdmin), not a new route.

- **The seeder creates the `system` namespace root and re-parents the
  pre-ADR-0040 orphan roots** (upgrade path): on first boot the seeder
  ensures a `system` root (`Kind = System`, `ParentId = null`) exists,
  seeds `terms` + `help` under it, and re-parents any pre-ADR-0040 root
  page (a `ParentId = null` page slugged `terms` / `help`) under it,
  normalizing its `Kind` to `System`. Idempotent (the `ON CONFLICT` /
  upsert pattern, ADR 0004 §B); a warm re-run is a no-op.

- **The blog feed: `GET /blog` + `GET /blog/{userId}`** (a new
  `BlogController`, over the frozen `IPageService` seams — CQRS-lite, ADR
  0039). `GET /blog` is a signed-in resident's **own** feed (a
  self-route; a signed-out visitor redirects to `/directory` — the
  resident catalog, the one place "who is here" is always listed).
  `GET /blog/{userId}` is a specific resident's feed: their `Kind = User`
  pages, **newest-first**, each linking to its derived `/pages/{path}`
  (the ADR 0039 §3.3 derived-path walk, the `PagePaths.Href` seam). A
  **draft** page (ADR 0037) is **absent** from another resident's feed
  (a draft is its author's — the same gate `PageController.Show` runs), but
  appears — **badged** — on the author's own feed. An **empty feed** (no
  blog pages yet) is a **valid shape, not a 404** — the feed is a listing,
  and "no blog posts yet" is a real state a resident is in before writing
  their first page. The feed is **read-only**: the write lanes live on the
  `PageController`; the feed's only affordance is a "New blog page" button
  on the author's own feed, routing to `/pages/new` (the composer opens on
  a `User` page for a resident — the Decision's parent-picker pin).

- **The composer parent picker is kind- and ownership-differentiated**
  (the UX seam, server-backed): for a `Kind = System` page the parent
  picker offers the **system namespace** (the tree's `System` pages); for
  a `Kind = User` (blog) page it offers **only the actor's own blog** (the
  tree's `User` pages whose `AuthorId` is the actor). The picker is
  **server-side** (the `PageController` seeds it over the loaded tree), so
  a forged form field cannot widen it (C3 — the `Kind` field on the form is
  a hidden input, set by the server, not a user choice; the write lane
  re-checks `Kind` and the two guards). The top-level option's label and
  hint are kind-aware (a blog root is "your blog"; a system root is "the
  system namespace").

- **The `StaticPagesController` (`/terms` / `/help` / `/about`) resolves
  `system/{slug}` first, falling back to `{slug}`** (the ADR 0039
  U07 "tree-only" absorb, now under the `system/` root): the canonical
  pages live at `system/terms`, `system/help`, `system/about`; a
  pre-ADR-0040 root page (bare `terms`) still resolves (the re-parent
  makes the canonical path the one that hits, and the fallback keeps a
  legacy seam). `/about` still degrades to the product-story view when
  absent (the ADR 0039 U05 drift pin).

## Consequences

- **No schema migration** — `Page.Kind` is an additive column (ADR 0004
  §B), delta-detected and idempotent; a re-seed normalizes pre-ADR-0040
  roots. Re-deploying an older image over a forward-migrated database is
  safe (the column defaults to `System`, the pre-ADR-0040 behavior).
- **The ADR 0039 §3.7 standing matrix is superseded** — the Moderator lane
  on pages is retired, and the author's move/delete lane now applies only
  to a `User` (blog) page. The ADR 0029 translation standing (GlobalAdmin ∪
  Translator) is **unchanged in shape** — it is now applied per-kind, and a
  Moderator never qualified for a *page* translation (they qualified for a
  *community post* translation, ADR 0022/0026 — that lane is untouched).
- **The `AccessVia.Moderator` tag is never written for a page** — the
  `PageService` write lanes resolve `Owner` (a blog author) or `Admin`
  (a GlobalAdmin); the audit row's `TargetKind` is still `"page"` and the
  `Action` names (`page.create` / `page.update` / `page.move` /
  `page.delete` / `page.translation.add`) are unchanged from ADR 0039.
- **A new Web surface, read-only** — the `BlogController` + `/blog` +
  `/blog/{userId}` + the `BlogViewModel` / `BlogPostRow` records. It
  composes the frozen `IPageService` read lanes (`GetBlogPagesAsync`,
  `GetTreeAsync`) and the `IUserInfoService.GetProfileAsync` seam
  (best-effort display name — a missing profile is a display gap, not an
  error). No new bounded context, no new `AccessAction`, no new
  `AccessVia`, no new authorization branch (ADR 0006 / ADR 0039 hold).
- **The `kw-l` registry gains four `blog.*` keys** (`blog.new_page`,
  `blog.empty_own`, `blog.empty_other`, `blog.draft`) — the ADR 0015 D1 /
  M·3 view↔registry invariant (the `KwLRegistryConsistencyTests` pin)
  holds; the `pages.none` copy is corrected to the ADR 0040 standing (a
  resident may start their own blog; a Moderator may not create a system
  page).
- **The `Milestones.cs` + README Roadmap are unchanged** — ADR 0040 is a
  follow-on refinement within the **open** `PG` lane (ADR 0039), not the
  "close the lane" step and not a new milestone. `PG` stays
  `StatusPlanned`, `M4` stays the single in-progress milestone (the
  `MilestonesTests.cs` pins — the ordered `Ids` list, the
  single-in-progress pin, the `Shipped_Milestones_Are_Marked_Done` list —
  are all unchanged by this ADR).

## Tests

- **Core (`Kumunita.Core.Tests`, DB-backed)** — `PageServiceTests`:
  the ADR 0040 two-kind standing matrix (create / edit / translate /
  move / delete standing for each kind, with a community Moderator
  denied on every lane), the two namespace guards (a `System` page under a
  `User` parent is refused, and vice-versa), the ownership guard (a
  resident may not nest under another resident's blog), the root-slug
  collision scoped by `(Kind, AuthorId)` (two residents may both have a
  root blog page of the same slug; one resident may not have two), the
  seeder's `system`-root creation + re-parent + `Kind` normalization, and
  the blog read lanes (`GetBlogRootAsync` / `GetBlogPagesAsync` /
  `IsUnderBlogAsync`).
- **Web (`Kumunita.Web.Tests`)** — `BlogControllerTests` (NSubstitute
  `IPageService` + `IUserInfoService`, no live Postgres — the
  `StaticPagesControllerPgTests` shape): `GET /blog` signed-in → redirect
  to the actor's own feed; `GET /blog` signed-out → redirect to
  `Directory`; `GET /blog/{userId}` → the resident's `User` pages, newest-
  first, each linking to its derived `/pages/{path}`; the draft gate (a
  draft absent from another resident's feed, badged on the author's own);
  `IsOwner` true only when `userId == actorId`; the empty-feed shape
  (not a 404). `StaticPagesControllerPgTests` (the `system/{slug}`
  primary path + the `{slug}` fallback) and the `MLUI_FacesTests` /
  `PublicLocaleAndAboutTests` present/absent cases (the `about`
  product-story degrade) are updated to plant at `system/{slug}`.
- **`KwLRegistryConsistencyTests`** — the new `blog.*` keys are registered
  (the view↔registry invariant holds); the `pages.none` value is
  registered and matches the view's inner text.
- **`MilestonesTests.cs`** — unchanged (this ADR does not add a lane or
  flip a status; `PG` stays `Planned`, `M4` stays the single in-progress).

## Non-decisions

- **No machine translation** of a blog page (ADR 0005 C stands — a
  translation is a user-added row, the ADR 0029 shape; a `User` page's
  translation standing is GlobalAdmin ∪ Translator, never the author).
- **No blog-level moderation or community-scoping** — a blog is personal
  content; the `Audience` doc is still reusable on a `User` page (a
  resident may restrict a page to a community) but the *standing* is
  author ∪ GlobalAdmin, not community-scoped.
- **No "close the PG lane" step** — this ADR refines the open `PG` lane;
  closing it (the `PG` → `StatusDone` + `M4` → the active work flip, and
  the design-doc / plan-set move) is a separate, deliberate step when the
  lane's remaining work is green, per the repo's "no destructive step
  until green" discipline (the `GA` / `RE` / `DM` precedent).
