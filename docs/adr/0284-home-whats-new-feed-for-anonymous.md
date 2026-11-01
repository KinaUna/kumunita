# ADR 0284 — Home "What's new" feed for anonymous visitors

Status: Accepted
Date: 2026-11-01

## Context

The home page's "What's new around the street" feed (the `HomeFeed`, the
three columns: posts / announcements / pages) was **signed-in only**:
`HomeController.BuildFeedAsync` returned `null` for an anonymous visitor,
and the view omitted the whole section. A non-logged-in visitor saw only
the hero, the feature cards, and the roadmap.

The ask: *"For non-logged-in users it should still show the What's new
around the street listing public announcements and public pages."*

The underlying read seams already know how to answer for an anonymous
caller — the feed just never asked them:

- **Announcements** — `IAnnouncementService.ListVisibleAsync(string? actorId, …)`
  already takes a *nullable* actor: with a null actor it returns
  `Scope == Public` announcements only (the `authed` flag is `false`, so
  community-scope rows are filtered out). The `/announcements` list itself
  is public.
- **Pages** — `IPageService.GetTreeAsync()` + one aggregate
  `IAuthorizationService.CanSeeAsync(…)` (the `/pages` tree-browse shape,
  which is also a public route — `PageController.Index` passes
  `KumunitaPrincipal.SubjectId(User) ?? string.Empty` for an anonymous
  visitor). The frozen `Decide()` branch 5 (`Audience is null` → public)
  is the only branch an empty actor id matches, so a public page is
  visible, a community / resident / group-restricted one is not.
- **Posts** — `PostService.ListAllFeedAsync` requires a non-empty
  `actorId` by contract, and a `Post.Audience` is **non-null** (invariant
  C1 — posts never have a null-audience "public" shape), so the frozen
  `Decide()` has **no** branch that admits an anonymous actor to a post.
  The posts column is therefore structurally empty for an anonymous
  visitor — there is nothing to list, not a read to degrade.

## Decision

`HomeController.BuildFeedAsync` no longer short-circuits on an anonymous
visitor; it builds the feed for every visitor and the three column
builders each apply their own anonymous floor:

- **Posts column** — `BuildPostRowsAsync` takes a `string?` subject and
  returns an empty column up front when the subject is null (no service
  call — the `Posts` / `UserInfo` seams are not consulted at all). This is
  not a degradation: a post's `Audience` is non-null (invariant C1), so no
  post is ever world-readable, and an empty column is the *correct*
  answer, not a fallback.
- **Announcements column** — `BuildAnnouncementRowsAsync` passes the
  nullable subject straight into `ListVisibleAsync` (the seam's existing
  public-only-for-anonymous behavior — the same shape the public
  `/announcements` list already has).
- **Pages column** — `BuildPageRowsAsync` passes
  `subjectId ?? string.Empty` into the one aggregate `CanSeeAsync`
  (exactly what the public `/pages` tree browse does,
  `PageController.Index`), so a public page (`Audience is null`) is
  visible and every audience-restricted one is not. The author-only draft
  gate already excludes every draft for an anonymous visitor
  (`p.AuthorId == null` never matches).
- The `roles` set is computed null-safely (`User?.Claims ?? empty`) — a
  controller-without-context test construction (the `HomeControllerTests`
  harness) must not NRE, and an empty role set is the anonymous floor
  `ListVisibleAsync` expects.
- `HomeViewModel.Feed` stays `HomeFeed?` — `null` now means "no feed seams
  at all" (a test construction), never "the visitor is anonymous". The
  view renders the section whenever `Feed` is non-null and shows the
  "nothing yet" note for the `IsEmpty` shape — an anonymous visitor on a
  fresh instance (no public announcements / pages yet) lands on that note
  in place of an absent section, which is the desired "be the first"
  affordance rather than a missing area.

No view markup change (the section was already conditionally rendered on
`Model.Feed is { } feed`), no routing, no schema, no audit-lane change
(the one aggregate `AccessAudit` row `CanSeeAsync` writes is the same
shape the `/pages` browse writes for an anonymous visitor — an `Allow`
row over the public candidates), no new translation key, no new
`AccessAction` / `AccessVia` / `Decide()` branch. The
`HomeUpcoming` section (the resident's `Profile.HomeShowUpcoming`
preference, ADR 0163) stays **signed-in only** — it is a *personal*
surface ("your" upcoming events / to-dos) and an anonymous visitor has no
subject to scope it against (the ADR 0163 pin is unchanged and is a
different section from the "What's new" feed).

## Verification

`HomeControllerTests.Index_FeedPresentButEmpty_ForAnonymous_Viewer_Without_Seams`
pins the new floor: with no feed seams wired and no controller context
(an anonymous shape), `vm.Feed` is **non-null** and `IsEmpty` (every
column degraded to empty) — replacing the old pin that `vm.Feed` was
`null` for an anonymous visitor. The full `Kumunita.Web.Tests` suite runs
green (1212 tests, 0 failed). The signed-in feed shape is unchanged: a
signed-in visitor still gets posts + community announcements +
community/public pages, exactly as before (the three `Build*RowsAsync`
helpers are untouched for a non-null subject).
