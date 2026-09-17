# Pages (hierarchy + audiences + translations) — design

> **Lane.** `PG` — a **named lane** (the `ML` / `GP` / `RC` / `RE` convention: a
> short ID, *not* a renumber). M4 / M5 / M6 stay Events / Projects /
> Portability; **no roadmap letter moves**. **ADR 0039** (the next number after
> 0038) is this lane's decision record. This file is the **primary reference
> tier** (the exact seams every unit codes against); the sealed-unit register
> is `docs/plans-milestones/pages/plan-pages.md`.
>
> **Status.** Accepted (ADR 0039, 2026-09-17). All decisions marked
> **[DECIDED]** are locked in ADR 0039. The former **[PROPOSED]** markers have
> been resolved to **[DECIDED — ADR 0039]**; the ADR is the authoritative
> record, this doc is the primary reference tier for implementation.

## 1. What this lane is

A **Pages** bounded context (`Kumunita.Core.Pages`) that generalizes the existing
**static-page** lane into a **hierarchical, audience-restricted, translatable
knowledge tree**:

- **Seeded default pages** — home, about, terms & conditions — present on a
  fresh instance, each with at least an `en` floor, **editable in the WYSIWYG
  editor** (the RE `bindRichEditor` + `MarkdownRenderer` surface).
- **Admin- and community-moderator-authored pages** — a blogging surface for
  "how the platform works", "what's coming", longer resident prose.
- **Audiences like community posts** — a page is **private** (a subset of
  users/groups), **community-visible** (all members of a component), or
  **public** (everyone) — reusing the exact `Audience` mechanism posts use
  (ADR 0001-B / ADR 0006 / ADR 0036), *not* a second flat scope.
- **A hierarchy** — pages nest like folders/files (`parent_id`), listed as a
  tree and rendered like blog posts. **Special root nodes** (the "mount points")
  name **where a page appears in the UI**: `footer/community` (the about page),
  `help/account` (a change-password help page), etc. A page can be *both* a
  leaf in the tree and a mount target for a UI slot.
- **Referenced from UGC** — a page's URL is a Markdown link
  `[label](/pages/{...})` that posts / replies / announcements can already
  render (the content-image / attachment idiom — **zero read-path change**).

**The one thing every unit must respect:** this lane is **additive on top of the
existing lane and reuses every settled mechanism** — the `Audience` doc (no
second scope enum), the `IAuthorizationService.CanAsync(…Read…)` decision path
(through a `PageToAuditableResource` adapter, mirroring `PostToAuditableResource`),
the user-added-translation row shape (ADR 0022/0026/0029), the `MarkdownRenderer`
+ `bindRichEditor` authoring surface (ADR 0025 / ADR 0031), the content-image /
attachment idiom (ADR 0025 / ADR 0034), and the ADR 0004 §B.1 **additive-field**
rule (delta-detected, idempotent, no seed reset, zero migrations for new fields).
**No new `AccessAction`** (the existing `Read` / `Moderate` are enough), **no new
`AccessVia` value** (the audience / community / moderator branches already cover
the standing), **no new bounded-context authorization path** (it goes through the
frozen `IAuthorizationService`).

## 2. The existing surface this lane builds on (verified, 2026-09-17)

Read directly, not assumed:

1. **`LocalizedPage`** (`Kumunita.Core.Localization`) — a flat page doc: `Id`,
   `Slug` (`terms`|`about`|`help`), `LanguageCode`, `Title`, `Body` (Markdown),
   `Updated`, `ImageIds`. **No parent, no audience, no author.** Registered in
   `M1DocTypes` with a `(Slug, LanguageCode)` unique index.
2. **`StaticPagesController`** — three hard-coded `HttpGet` routes (`/terms`,
   `/help`, `/about`) over `ITranslationProvider.GetPageAsync(slug, preferred)`.
   Public by construction (no authorization, no `AccessAudit`). `/about` falls
   back to the product-story `HomeViewModel` when truly absent.
3. **`UpsertPageAsync`** (`ILocalizationService`) — the admin/translator write
   lane (pair idiom by slug), writes an `AccessAudit` row
   (`Action = "page.save"`, `Via = Admin`). The **only** writer today.
4. **Authoring surface already exists** — `Views/Languages/PreviewPage.cshtml`
   hosts the RE editor (`bindRichEditor`) over a page `Body`; the RC
   content-image lane is **complete for static pages** (the `SavePage` lane,
   per the `PreviewPage.cshtml` note). So **editing a page in the WYSIWYG
   editor is already working** — this lane widens *who* can edit it, *what* it
   is (audience/hierarchy), and *where* it appears.
5. **Audiences are reusable** — `Audience` (`Mode`/`Grants`/`Community`), the
   `AudienceEditorModel` + `BuildAudience()` single-source form surface
   (M2, reused by posts/profiles), and — the load-bearing fact — **`Decide()`
   branch 5: `Audience == null` ⇒ public** (allow all). So "public when
   appropriate" is *not* a new flag; it is the *absence* of an audience.
6. **Translatable UGC row shape is settled** — `PostTranslation` /
   `ReplyTranslation` / `AnnouncementTranslation` / `GroupTranslation` /
   `CommunityTranslation`, all `(ParentId, LanguageCode)` unique-indexed,
   standing = GlobalAdmin ∪ Translator (∪ community Moderator for community-
   scoped targets), display via the ADR 0027 chip-swap.
7. **Reference-from-UGC is free** — `MarkdownRenderer` renders
   `[label](url)` under the `IsSafeUrl` whitelist (relative paths accepted), so
   a `[About](/pages/about)` link in a post body already works end-to-end with
   **no renderer change**.

## 3. The design decisions

### 3.1 The big fork — absorb vs. parallel  **[DECIDED: absorb]**

The existing `LocalizedPage`/`StaticPagesController`/`UpsertPageAsync` lane is
**absorbed**, not run in parallel. `about`/`terms`/`help` become **three seeded
pages** in the new hierarchy; the three hard-coded routes become **redirects /
re-renders** to the same tree. Rationale:

- Your framing is *one* tree ("about is in `footer/community`", "help/account/
  is a path") — that is a single document graph, not two coexisting page
  systems. Two systems would give two editors, two renderers, two translation
  lanes, and a `/about` that is special-cased forever. Absorbing makes
  `/about` an *ordinary page* with a mount point.
- It keeps **one** `MarkdownRenderer` (ADR 0005 A's "single page engine"),
  **one** editor (`bindRichEditor`), **one** translation-row shape, **one**
  decision path — the "lean" / "boring" principle.
- **Additive, not destructive:** the seeded pages keep the *same* `en` floor
  text a fresh instance has today (no visible regression), and the
  `terms`/`help`/`about` URLs keep resolving (via the tree), so nothing that
  works today stops working. The absorption is a **migration of data model, not
  of behavior**.

> **Fallback if sign-off rejects absorb:** run **parallel** — new `Page` tree
> for the blogging/hierarchy surface; `LocalizedPage` stays for
> `about`/`terms`/`help`. Cost: two renderers stay, two translation lanes stay,
> and the "about is in `footer/community`" mount point cannot be expressed on a
> `LocalizedPage` (it has no hierarchy), so mount points only work on `Page`.
> This lane is *not* coherent in the parallel shape — absorb is the intended
> model; parallel is only a retreat if the migration is judged too risky for
> the deployment.

### 3.2 Document shape  **[DECIDED — ADR 0039]**

A **`Page`** POCO (`Kumunita.Core.Pages`) — the **body/structure** of a page
(authored-in language, hierarchy, audience, author). Body text is stored
**once per authored-in language**, and **translations are separate rows**
(`PageTranslation`), exactly mirroring `Post` + `PostTranslation`. This matches
how the platform already stores every other translatable UGC surface (ADR
0018 authored-in tag + ADR 0022 separate translation rows), and it is what lets
the ADR 0027 chip-swap display work unchanged.

```csharp
// Kumunita.Core.Pages
public sealed class Page
{
    public string Id { get; set; } = string.Empty;        // surrogate (Marten default)

    // Hierarchy (the "folders and files" shape).
    public string? ParentId { get; set; }                 // null = a root node
    public string Slug { get; set; } = string.Empty;      // path-segment, unique per parent
    public string Title { get; set; } = string.Empty;     // authored-in title (display label)
    public string Body { get; set; } = string.Empty;      // authored-in Markdown (the single page engine)

    // Audience (ADR 0001-B / 0036 — REUSED verbatim, not a new scope).
    public Authorization.Audience? Audience { get; set; } // null = public (Decide() branch 5)

    // Authorship + moderation scoping.
    public string AuthorId { get; set; } = string.Empty;
    public string? ComponentId { get; set; }              // the community a moderator is scoped to
    public string LanguageCode { get; set; } = string.Empty; // ADR 0018 authored-in tag

    // Mount point (the "special root nodes" — where a page appears in the UI).
    public string? MountPoint { get; set; }               // e.g. "footer/community", "help/account"

    public DateTimeOffset Created { get; set; }
    public DateTimeOffset? Modified { get; set; }
    public bool IsDraft { get; set; } = false;            // ADR 0037 draft idiom, reused

    // RC content-image + ATT attachment idiom (ADR 0025 / 0034), reused.
    public IReadOnlyList<string> ImageIds { get; set; } = [];
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];
}
```

**Field-by-field provenance** (each reuses an existing mechanism, none invents one):

| Field | Source idiom | Note |
|---|---|---|
| `ParentId` / `Slug` | *new, this lane* | the hierarchy. `Slug` is **unique per parent** (a `(ParentId, Slug)` unique index, the `GroupMembership` business-key convention). The full path is *derived* (`/pages/a/b/c`) — not stored — so a subtree moves in one column write. |
| `Title` / `Body` / `LanguageCode` | `Post` / `Announcement` (ADR 0018) | authored-in tag + Markdown body; rendered by the single `MarkdownRenderer`. |
| `Audience` | `Post.Audience` (ADR 0001-B / 0036) | **`null` = public** (Decide branch 5); non-null = grant list / `Community` flag. Reuses the `AudienceEditorModel` form surface verbatim. |
| `AuthorId` / `ComponentId` | `Post` / `Announcement` | owner branch + moderator scoping, both already in `Decide()`. |
| `MountPoint` | *new, this lane* | a **nullable string tag** (e.g. `"footer/community"`). A UI slot reads "the page mounted at X". It is a *display* concern (where to surface a link), **not** an access boundary — access is always the `Audience` + `CanAsync(Read)`. |
| `IsDraft` | `Announcement.IsDraft` (ADR 0037) | the author-only draft pin, reused for "admin drafting a blog post before publishing". |
| `ImageIds` / `AttachmentIds` | `Announcement` (RC U03 / ATT U3) | the content-image + attachment idiom, byte-store + reverse-lookup serving reused. |

### 3.3 Hierarchy model  **[DECIDED — ADR 0039]**

- **Nesting is by `ParentId`; the path is derived.** A page's canonical
  path is the chain of ancestor `Slug`s: `Page("/a/b/c")` ⇒ `a` (root) → `b`
  → `c`. The tree is **a forest** (any number of roots — `about`, `help`,
  `blog`, `footer/community`, …). There is no single mandatory root.
- **A page is either a *folder* (has children, `Body` may be empty) or a
  *leaf* (has a `Body`), or both** (a "folder-with-index" — like a blog
  category that also carries an intro). Both are the same `Page` doc; "folder"
  is not a type, it is "has ≥1 child".
- **Mount points are pages too.** `footer/community` and `help/account` are
  ordinary pages in the tree *whose `MountPoint` field is set*. The UI slot
  (`_Layout.cshtml` footer, the account help panel) resolves *"the page whose
  `MountPoint == 'footer/community'`"* and renders/links it. This is what makes
  "the about page is in `footer/community`" true: the about page *is* the page
  mounted at that slot.
- **Listed like folders/files, displayed like blog posts.** Two read shapes,
  one tree: (a) a **tree/list** view (the `/pages` browse — the folder/file
  metaphor, nested) and (b) a **post** view (`/pages/{path}` — one page's
  title + rendered body + its translations, the blog-post shape). The same
  `Page` doc feeds both; nothing is duplicated.
- **Cycle / depth guard.** `ParentId` writes validate that the new parent does
  not descend from the node being reparented (no self-cycles) and cap depth at a
  sane bound (e.g. 8) — a data-integrity guard in the write lane, not a schema
  constraint (Marten has no FK; the guard is the `PageService`'s).
- **Renaming a page rewrites its children's derived paths** (paths are not
  stored, so a `Slug` change is a single-column write; the derived paths of the
  subtree update automatically). **References from posts break by path, not by
  id** — the same way a content-image link would if a slug changed; acceptable
  for a knowledge tree (the RC lane has the same property). *[PROPOSED — note
  as a known consequence.]*

### 3.4 Audiences  **[DECIDED — reuses ADR 0001-B / 0036; no new mechanism]**

A page's `Audience` is **exactly** a post's: `null` = public; non-null =
`Mode` + `Grants` (users/groups) + `Community` flag. This means:

- **Private page** — a non-null `Audience` with an explicit `Grants` list
  (a set of users/groups), `Community` `false`. Empty-audience-denies (C1)
  still applies to the *grant list*; the owner branch still lets the author see
  their own empty-audience page (the draft/author pin).
- **Community page** — `Audience.Community = true`, `ComponentId` set, `Grants`
  empty ⇒ visible to that component's members (ADR 0036's branch, *unchanged*).
- **Public page** — `Audience = null` ⇒ everyone, including unauthenticated
  (Decide branch 5, *unchanged*).
- **The decision path is the frozen `IAuthorizationService.CanAsync(…Read…)`**,
  through a **`PageToAuditableResource`** adapter (mirrors
  `PostToAuditableResource` verbatim: `Id`/`Name`/`OwnerId = AuthorId` /
  `Audience` (null allowed) / `ComponentId` / `TargetKind = "page"`). **No new
  `AccessAction`** (`Read` exists) and **no new `AccessVia`** (the
  `Owner`/`Moderator`/`Community`/`Audience` branches already cover every
  standing a page needs). The adapter is the *only* new authorization surface.
- **List/tree views** filter via `CanSeeAsync(Read, candidates)` (the same
  C6 no-drift, aggregate-audit bulk decision posts use) so a resident only
  *sees* pages they can read — the tree does not leak the *existence* of a
  private page they cannot open.
- **The default is the author's choice, verbatim** (ADR 0001-B) — the composer
  seeds the `AudienceEditorModel` exactly as the post composer does (ADR 0036's
  "community-visible by default" is a *per-surface* choice; for **pages** the
  default is **[PROPOSED]: public** (`Audience = null`), because a default page
  like `about`/`terms` is meant for everyone and a blog post is meant to be
  read — the *author* narrows when they want private. This is the one place
  pages differ from posts (posts default community-visible; pages default
  public). **[DECIDED — ADR 0039]**

### 3.5 Translations  **[DECIDED — reuses ADR 0018 / 0022 / 0026 / 0027 / 0029]**

- A **`PageTranslation`** row (`Kumunita.Core.Pages`): `Id`, `PageId`,
  `LanguageCode`, `Title?`, `Body`, `AuthorId`, `Created` — **registered
  alongside `Page`** (a new `PageDocTypes` surface, or added to `M3DocTypes`
  — the register's U01 picks the surface; the shape is fixed). `(PageId,
  LanguageCode)` unique index, the settled business-key convention.
- **Authored-in** is `Page.LanguageCode` (ADR 0018) — the base body is
  authored in one language; the `PageTranslation` rows are **user-added**
  (never machine-translated — ADR 0005 C stands).
- **Standing = GlobalAdmin ∪ Translator** (ADR 0021/0022) **plus the
  community-Moderator lane for a community-scoped page** (`ComponentId` set and
  equal to a community the actor moderates — the ADR 0029 announcement lane,
  carried over). Add-only (ADR 0022); a wrong translation is corrected by the
  same standing adding the right one, the unique index preventing a duplicate.
- **Display = the ADR 0027 chip-swap**, reused verbatim: the authored-in
  variant is the default visible one, a chip per `PageTranslation`, click to
  swap. The page view (`/pages/{path}`) renders exactly like the
  post/announcement/reply detail translation surface. **No new display rule.**
- **Seeded default pages ship with an `en` floor** (the existing
  `about`/`terms`/`help` bodies become the `en` authored-in body of the seeded
  pages) so a fresh instance is byte-identical to today for those three pages,
  and a Translator can add `fr`/`de`/… rows the ADR 0027 surface already renders.

### 3.6 Reference-from-UGC  **[DECIDED — free, the RC/ATT idiom]**

A page is referenced from a post/reply/announcement body as a plain Markdown
link `[About](/pages/about)` (or a deep path `/pages/help/account`). Because
`MarkdownRenderer` already renders `[label](url)` links and `IsSafeUrl`
already accepts relative paths, **the read path needs zero changes**. What the
lane *adds* is the **serving route** `GET /pages/{path}` (the post view), which
is itself audience-gated by `CanAsync(Read)` — so a link to a private page
renders as a link in the (already-authorized) post, and *opening* it is a
separate `Read` decision that can 403/404. No `ImageIds`-style population is
needed (a page reference is a URL, not an in-body media id — the RC reverse
lookup is for media bytes, not for page links).

### 3.7 Write lanes + standing  **[DECIDED — ADR 0039]**

| Action | Standing | `AccessVia` |
|---|---|---|
| **Create** a page | GlobalAdmin ∪ (community Moderator, `ComponentId` = a community they moderate) | `Admin` / `Moderator` |
| **Edit** a page's body / audience / hierarchy | its `AuthorId` ∪ GlobalAdmin ∪ (a Moderator of its `ComponentId`) | `Owner` / `Admin` / `Moderator` |
| **Add a translation** | GlobalAdmin ∪ Translator ∪ (community Moderator, community-scoped) | `Admin` / `Moderator` |
| **Publish** a draft | its `AuthorId` only (ADR 0037 pin) | `Owner` |
| **Move / rename / delete** | GlobalAdmin ∪ (a Moderator of its `ComponentId`) — *not* a plain author (a page is platform content, not a personal note) | `Admin` / `Moderator` |

- The standing is **enforced server-side** in the `PageService` (the
  `AnnouncementService.CreateAsync` scope-vs-role re-check pattern, C3), not
  only by a Web `[Authorize(Roles=…)]` — the Web attribute is a convenience
  pre-gate; the service is the single source of truth (the M3b convention).
- **Every write lane** stores its `AccessAudit` row in the caller's session
  (invariant C3, ADR 0006), `TargetKind = "page"`, `Action`
  `page.create`/`page.update`/`page.translation.add`/`page.publish`/
  `page.delete`/`page.move`.
- **Delete** is a **hard delete** (like announcements, ADR 0017) or an
  **author soft-delete** (ADR 0024) — **[PROPOSED: soft-delete via an
  `IsDeleted` flag + `CanSeeAsync` filter — **[DECIDED — ADR 0039]**], so an
  accidental delete of a seeded page is recoverable and a page's children are
  not orphaned mid-tree.

### 3.8 The Web surface  **[DECIDED — ADR 0039]**

- **`PageController`** (`Kumunita.Web.Controllers`):
  - `GET /pages` — the **tree browse** (folders/files), `CanSeeAsync(Read)`-
    filtered.
  - `GET /pages/{**path**}` — the **post view** (title + rendered body +
    ADR 0027 translations), `CanAsync(Read)`-gated; 404 on absent, 403 on
    denied (the announcement 404-vs-403 split).
  - `GET /pages/new` + `POST /pages/new` — compose (title, body, parent,
    audience editor, language) — WYSIWYG (`bindRichEditor`).
  - `GET /pages/{id}/edit` + `POST /pages/{id}/edit` — the edit lane.
  - `POST /pages/{id}/publish`, `POST /pages/{id}/delete`,
    `POST /pages/{id}/move` — the standing-gated actions.
- **`StaticPagesController` becomes a thin redirect** — `/terms`, `/help`,
  `/about` resolve to the seeded page at that path (or 404/fallback as today
  for `/about`). The three hard-coded routes are *kept* (backward-compatible)
  but **read from the tree**, so there is one store, not two.
- **Mount points** — the layout partials (footer, account-help) resolve
  "the page mounted at `X`" and render/`<a href>` it. The `about` slot is
  `footer/community`; the account-change-password slot is `help/account`.
- **The editor** reuses `Views/Languages/PreviewPage.cshtml`'s existing
  `bindRichEditor` + RC content-image + ATT attachment wiring — the composer
  is the *same* editor posts/announcements use, pointed at a `Page` body.

### 3.9 Data migration (absorb)  **[DECIDED — ADR 0039 — the one real migration in the lane]**

1. Create the `Page` + `PageTranslation` tables (new doc types, ADR 0004 §B.1).
2. **Seed the three default pages** — `about`, `terms`, `help` — as `Page`
   docs carrying the **current** `LocalizedPage` `en` bodies/titles, `Audience
   = null` (public), `MountPoint` set (`about` → `footer/community`),
   `LanguageCode = "en"`. **Idempotent** (a `(null, Slug)` unique index on the
   root set, or an explicit seed guard — the seeder's existing first-boot
   idiom).
3. **Keep `LocalizedPage` read-only + non-destructive for one release** (the
   old `GetPageAsync`/`UpsertPageAsync` seam still works against it), so the
   old `StaticPagesController` fallback is *available* if the new route
   misbehaves; then **delete the `LocalizedPage` surface** in a follow-up
   unit once the new route is green (the register's U07). This is the
   "absorb, don't yank" discipline — nothing is removed until the replacement
   is proven.
4. **No seed reset, no schema file change** for the new *fields* — they are
   ADR 0004 §B.1 additive on the *new* `Page` doc (new table, so a clean
   create), and the migration above is the only data move.

## 4. What this lane is deliberately *not* (non-decisions)

- **Not a CMS with its own permissions engine.** Standing is the *existing*
  role matrix (GlobalAdmin / Moderator / Translator) re-checks; there is no
  page-specific role.
- **Not a comment / discussion surface.** A page is *about* content; discussion
  belongs in posts/replies that *reference* the page (the RC link idiom).
- **Not search.** A page-listing *filter* box may ship; full-text search over
  the tree is a **future lane** (the M6 portability/search milestone owns
  search).
- **Not version history / revision diffing.** `Modified` + the audit log is the
  "who changed what, when" record (the ADR 0017/0024 convention); a full
  revision store is out of scope.
- **Not multi-tree.** There is one forest, not per-community trees; a page's
  `ComponentId` + `Audience` is what scopes it, not a separate tree per
  community (consistent with `Post.ComponentId` being "a feed organizer, never
  an access boundary" — C-M3·2).

## 5. Consequences

- **One page engine, one editor, one translation lane, one decision path** —
  the lane *unifies* instead of *duplicating*, which is the whole point of
  absorbing `LocalizedPage`.
- **The audience machinery is reused end-to-end**, so a page gets
  private/community/public visibility, the correct `AccessVia` audit tag, and
  delegation for free (the `Decide()` algorithm is unchanged; only the
  `PageToAuditableResource` adapter is new).
- **`/about`, `/terms`, `/help` keep working** (now backed by the tree) and
  become *editable, translatable, hierarchy-aware, audience-capable* — a strict
  superset of today.
- **The "mount point" concept is new and small** (one nullable string column +
  one resolver), and it is the mechanism that makes the
  "about is in `footer/community`" requirement expressible without a special
  case.
- **The only irreversible step is the absorb migration**, and it is sequenced
  *last* (U07), after the new surface is proven, with the old surface kept
  readable until then — the lowest-risk ordering the repo's
  "no destructive step until green" convention allows.

## 6. Test surface (the seam tests, mirroring the repo's shape)

- **Core (`Kumunita.Core.Tests`, DB-backed)** — `PageServiceTests`:
  create/edit standing matrix (GlobalAdmin / Moderator / author / plain
  member), the `null`-audience-public vs. community vs. grants branches through
  the real `Decide()` (the `A0036_*` family shape, a `PG_*` family), the
  hierarchy cycle-guard, the soft-delete filter, and the `PageTranslation`
  standing (the ADR 0029 matrix).
- **Web (`Kumunita.Web.Tests`)** — `PageControllerTests` (NSubstitute
  `IPageService`, no live Postgres): the route map (`/pages`, `/pages/{path}`,
  `/about`→tree), the 404-vs-403 split, the mount-point resolution, and the
  composer/edit round-trip through the `AudienceEditorModel`.
- **Renderer/editor parity** — a page body with the same Markdown markers a
  post body uses renders identically (it does, by construction — one
  `MarkdownRenderer`) and the `bindRichEditor` binding is byte-identical to
  the post/announcement composer (it is — same `textarea[data-rich-editor]`).
- **`MilestonesTests.cs`** — if the lane adds a `PG` entry to `Milestones.All`,
  the order + single-in-progress pin holds (the test already pins the shape;
  add the row in the same commit).
