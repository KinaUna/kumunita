# ADR 0161 — Page-translation standing: the editor of a page can also translate it

Status: Accepted
Date: 2026-10-11
Amends: **0040** (the two-kind standing matrix, §3.7 translation row — the
`PageKind.User` (blog) page row gains the **author** branch, mirroring the
edit standing), **0029** (the "add a translation" lane carried to pages — the
display pin `PageService.CanTranslatePage` now returns true for the author of
a blog page, and the write-lane gate `CheckTranslateStanding` accepts it),
**0048** (the edit + remove translation lanes — the same standing matrix, so
the author's edit + remove rights on their blog page follow automatically).

## Context

ADR 0040 (2026-09-17) shaped the page standing matrix into two kinds. The
translation row — "GlobalAdmin ∪ Translator on either kind" — was chosen in
part to match ADR 0029 (the announcement lane), where the community-Moderator
branch was retired from pages. The matrix is *correct for a system page* (a
GlobalAdmin authored it; the author is an audit identity, not a standing
branch), but it left a gap on **blog pages**: a resident who is the **author**
of their own page (and therefore may **edit** it — the ADR 0040 edit standing
is "author ∪ GlobalAdmin" on a `PageKind.User` page) could not add, edit, or
remove a translation of that same page. The Web layer's
`PageShowViewModel.CanTranslate` (the display pin — `PageService
.CanTranslatePage`) is `false` for such an actor, so the ⋮ dropdown on the
page detail page (`Views/Page/Show.cshtml`) rendered **Edit + Delete only**,
exactly the gap reported (the "a user who can edit a page should also be able
to add/edit translations" request).

The same gap existed on **/admin/platform**: the platform pages table
(ADR 0043 D4 — the five shipped surfaces, each with Preview / Edit) has no
translation management at all. A GlobalAdmin who can edit a platform page (the
ADR 0040 system-page standing) has no UI to add a translation of it; they must
navigate to the page detail page, which for a system page is under `system/…`
(not a top-level surface the /admin/platform table links to).

ADR 0022 (posts), 0026 (groups), 0029 (announcements), 0059 (events) all
settled the same question the same way: the **author** of the item is in
scope for the translation standing (in addition to GlobalAdmin / Translator),
mirroring the edit standing. Pages were the one surface that diverged, because
ADR 0040's translation row inherited ADR 0029's (announcement) standing verbatim
without adding the author branch. This ADR closes that gap, on blog pages
(the author branch is meaningful there) — and adds the missing UI surface on
/admin/platform so a GlobalAdmin can manage translations of the four
Page-backed platform pages without leaving the admin.

## Decision

- **The page-translation standing on a `PageKind.User` (blog) page gains the
  author branch.** The edit standing on a blog page is the author ∪
  GlobalAdmin (ADR 0040); the translation standing is now the same, **plus**
  Translator (who has translation standing on any page, the ADR 0021 / 0029
  shape). The concrete decision body in `PageService.CanTranslatePageCore`
  gains the branch (mirroring `CheckEditStanding`):
  `page.Kind == PageKind.User && page.AuthorId == actorId` → `true`. A
  `PageKind.System` page's author is an audit identity, not a standing
  branch (a system page is GlobalAdmin-only, ADR 0040), so the branch is
  **scoped to the `User` kind** — a resident who happens to be the stored
  author of a system page still cannot translate it. A community Moderator
  has **no** translation standing on either kind (the ADR 0040 amendment is
  unchanged).
- **The audit row records the narrowest right that applied.** The
  `AccessVia` tag the page-translation write lanes' audit rows carry
  (`ResolveTranslationStandingVia`, called by the add / update / remove
  lanes) now returns `<see cref="AccessVia.Owner"/>` when the actor is the
  author of a `PageKind.User` page, else `AccessVia.Admin` (a GlobalAdmin or
  Translator). The audit trail distinguishes "the author translated their own
  blog page" from "an admin / translator translated a platform page" — the
  same precedence shape the edit standing uses (`AccessVia.Owner` for the
  author, `AccessVia.Admin` for a non-author GlobalAdmin, the
  `ResolveWriteStandingVia` derivation).
- **The write-lane gate (`CheckTranslateStanding`) accepts the author
  branch.** The display pin (`CanTranslatePage`) and the gate share the same
  `CanTranslatePageCore` body (the C3 single-source pin — the display and the
  gate can never drift), so a blog author's "add / edit / remove translation"
  affordance on the page detail page now renders, and the
  `AddTranslationAsync` / `UpdateTranslationAsync` / `RemoveTranslationAsync`
  write lanes accept them (the `PageController` write lanes are unchanged —
  they already delegate to the service, C3).
- **The `/admin/platform` platform pages table gains a per-page translations
  ⋮ dropdown (ADR 0161), the posts/announcements pattern.** Each of the four
  Page-backed platform surfaces (terms / help / privacy / conduct — `about`
  is a view, not a `Page` doc, ADR 0043 D1, and stays preview-only) now has a
  ⋮ "Translations" button beside Preview / Edit, opening the same
  add / edit / remove menu the page detail page has. The modals are the
  shared `Views/Shared/_PageTranslationModals` partial (extracted from
  `Views/Page/Show.cshtml`, parameterized by an id-prefix so multiple rows on
  one document never collide — the page detail uses `pg`, the admin table
  uses the page's slug, e.g. `terms`, `help`, `privacy`, `conduct`). Standing
  is the GlobalAdmin gate on `/admin` (the controller's
  `[Authorize(Roles = GlobalAdmin)]`); the `PageController` write lanes
  re-check standing server-side (C3) — a GlobalAdmin qualifies for the
  translation standing on any page (the ADR 0040 / ADR 0161 rule).
- **The `PageController.Show` is unchanged.** It already renders the ⋮
  dropdown (the ADR 0082 shape, carried verbatim from posts / announcements)
  and gates the translation items on `Model.CanTranslate` (the
  `PageService.CanTranslatePage` display pin). The ADR 0161 standing change
  is the only functional change on the page detail surface: the author of a
  blog page now sees the translation items (they did not before). The
  modals move to the shared partial (the markup is byte-identical, the ids
  are identical — the `pg` id-prefix the page detail already used — so no
  client-side behaviour changes).
- **`WhatsNew` + `STATUS.md`** — the close-flip adds a
  <c>0.54.0</c> (2026-10-11) entry: "Page translations for editors +
  platform-pages translation surface — the editor of a page can now add /
  edit / remove translations of their own page (the ADR 0040 standing matrix
  gains the author branch on blog pages, mirroring the edit standing), and
  the /admin/platform platform pages table gains a per-page Translations ⋮
  dropdown (the posts/announcements pattern, the shared
  _PageTranslationModals partial extracted from the page detail surface)
  (ADR 0161)."

## Consequences

- A blog page's author can now add / edit / remove translations of their own
  page (the ⋮ dropdown on the page detail page renders the translation items
  for them, and the write lanes accept them). A system page's author is
  still an audit identity, not a standing branch (a system page is
  GlobalAdmin-only, ADR 0040) — a resident who happens to be the stored
  author of a system page still cannot translate it. A community Moderator
  has no translation standing on either kind (the ADR 0040 amendment is
  unchanged).
- The audit trail for page translations now distinguishes the author's
  standing (`AccessVia.Owner`) from the admin / translator's standing
  (`AccessVia.Admin`) — the same shape the edit standing uses.
- A GlobalAdmin can manage translations of the four Page-backed platform
  pages from /admin/platform (the ⋮ dropdown per row) without navigating to
  the page detail page. The page detail surface (Views/Page/Show.cshtml) is
  unchanged (the same dropdown + modals, now the shared partial).
- The `PageService` standing tests gain an author-branch family
  (`PG6_CanTranslatePage_AuthorOfBlogPage_Allows`,
  `PG6_CanTranslatePage_AuthorOfSystemPage_Denies`,
  `PG6_CanTranslatePage_NonAuthorOfBlogPage_Denies`,
  `PG6_CheckTranslateStanding_AuthorOfBlogPage_Allows`) pinning the new
  standing. The `/admin/platform` composition seam gains a test family
  (`ADR0161_SeededPages_FourTranslationRows_AboutSkipped`,
  `ADR0161_StoredTranslations_On_Row_CandidateList_ExcludesTranslating`,
  `ADR0161_About_View_Skipped`) pinning the per-page translation data.
