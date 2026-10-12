# ADR 0163 — Home upcoming events & to-dos — a resident can opt in to seeing the next events + to-dos on the home page, above the "What's new" feed

Status: Accepted
Date: 2026-10-12

The request: "Add a new setting to settings/home, let user choose to show
upcoming events and to-dos on the home page. If enabled, the next 5 upcoming
events and 5 to-dos, should show above the 'what's new …' section. If a user
has more than 5 upcoming events, or 5 to-dos, a show more button should be
shown to load more."

ADR 0163 settles this as a **named lane** (not a renumber of the M-letter
milestones — the AGENTS.md / ADR 0013 / ADR 0111 precedent: "When a new
capability lands out of the M-letter order, it gets a *named lane* with a
short ID — not a renumber"). The lane's shape follows the **ADR 0149
precedent** (the `Profile.HideHomeIntro` per-resident home-page display
preference) exactly: a **new additive `Profile` field** (the ADR 0004 §B.1
additive idiom — delta-detected, idempotent, no re-seed, no EF migration),
a **single write lane** (the C-MED·8 shape — the self-scope check happens at
the Web boundary), and a **best-effort read** on the home page (a display
read only — it never gates anything, the ADR 0149 "a read, not a decision"
pin).

**The one thing that separates ADR 0163 from a new bounded context — the
lane is *display*, not *data* (ADR 0163·1).** ADR 0163 adds no new doc, no
new bounded context, no new write seam on any content surface (the
`Events` / `Projects` services are **frozen**), no new dependency, no new
EF migration, and no new authorization. It **reuses** the existing
`/events` + `/projects/todos` read seams (so a row is visible on the home
page only when the viewer could already see it in that surface's own feed —
the ADR 0006 "thin token, fat authorization" posture) + the ADR 0149
single-write-lane shape (the `SetProfileHideHomeIntroAsync` precedent) + the
`mine-more` / `roadmap-more` disclosure idiom (the ADR 0031 plain-TS
convention — a JS-less visitor still sees the first five of each column, and
the whole list stays in the markup for screen-reader users) + the ADR 0157
translation-overlay shape (the `events` / `todos` rows render in the viewer's
language when a translation exists — the ADR 0022 / 0051 floor).

The one thing the whole lane respects: ADR 0163 is **additive on the
existing `Profile` doc + the existing `UserInfo` context** (ADR 0163·1) — it
**reuses** the `Kumunita.Core.UserInfo` context + the `IUserInfoService`
seam (the **frozen** surface) + the `Profile` doc (the ADR 0004 §B.1
additive surface — the 14th additive `Profile` field after
`AvatarId` / `TimeZone` / `DateFormat` / `EmailLanguage` / `Bio` / `TagIds`
/ `OnboardingCompletedAt` / `MessagingOptIn` / `MessagingRestricted` /
`BlockedCommunityIds` / `EventRsvpMode` / `PageSize` / `HideHomeIntro`) and
**adds** two new additive `Profile` fields
(`HomeShowUpcoming` — a `bool`, default `false` — and
`HomeUpcomingMineOnly` — a `bool`, default `false`, the D1a "only mine"
scope), two new single-write-lane seams on `IUserInfoService`
(`SetProfileHomeShowUpcomingAsync` + `SetProfileHomeUpcomingMineOnlyAsync`,
both the `SetProfileHideHomeIntroAsync` ADD precedent), the `LocaleController
BuildModel` / `SaveHome` settings pair, the `HomeViewModel` `Upcoming`
shape (the `HomeFeed` / `HomeFeedRow` projection precedent), the `Home
Index` "Upcoming" section (the `mine-more` / `roadmap-more` disclosure
idiom), the new `home-upcoming-more.ts` client module (the `mine-more.ts` /
`roadmap-more.ts` self-guard idiom), and the closed `settings.home.*`
(`upcoming_label` / `upcoming_note` / `flash_saved`) + `home.upcoming.*`
(`title` / `events` / `todos` / `show_more`) `kw-l` key set × en/de/fr/da.
It adds **no** new bounded context, **no** new doc, **no** re-shape of any
existing `Profile` field, **no** re-shape of any `IUserInfoService` method,
**no** re-shape of any `IEventService` / `IProjectService` method, **no**
`AccessAudit` row on the read (a display read, the ADR 0149 posture), **no**
JS library, **no** EF migration, and **no** re-seed.

## Decision

- **D1 — The new preference is a *per-resident* display flag, off by
  default.** `Profile.HomeShowUpcoming` (a `bool`, default `false`) — the
  ADR 0149 `HideHomeIntro` precedent (the ADR 0004 §B.1 additive idiom).
  A fresh account that never touches the checkbox sees exactly the
  pre-feature home page (the section is not in the DOM at all — the
  `@if (Model.Upcoming is { } upcoming)` wrap is the DOM-level pin, the
  ADR 0150 SITE·9 idiom).
- **D1a — An optional "only mine" scope, off by default (a companion to
  D1, shipped alongside).** `Profile.HomeUpcomingMineOnly` (a `bool`,
  default `false`) — the ADR 0149 `HideHomeIntro` precedent, a 15th additive
  `Profile` field. When on *and* the section is shown, the home page lists
  the resident's **own** events (RSVP'd or authored — the EV-MINE read,
  ADR 0065 `IEventService.ListMineAsync`) and **their own** to-dos (assigned
  to them — the `assigneeId = subject` "assigned to me" idiom, the
  `ProjectsController.TodosIndex` filter) instead of the community-wide
  lists (the D3 floor, the shape the section shipped in). Same display-read
  posture — it changes *what is listed*, never *whether a row is
  authorized* (both scopes use the same authorization-aware read seams; the
  mine-scoped reads apply their own `CanSeeAsync`, as the community-wide
  ones do). It is a checkbox on the settings page (indented under the
  "show upcoming" toggle it scopes; the controller reads it alongside the
  others, the ADR 0149 "un-checked is the floor" idiom) and a single write
  lane (`SetProfileHomeUpcomingMineOnlyAsync`, the D2 shape verbatim). The
  closed `kw-l` key set gains `settings.home_upcoming_mine_label` /
  `settings.home_upcoming_mine_note` × en/de/fr/da (the ADR 0015 / ADR 0119
  D9 "no English-only key" rule).
- **D2 — The write lane is the C-MED·8 single-write-lane shape.** One new
  method on `IUserInfoService`
  (`SetProfileHomeShowUpcomingAsync(subjectId, showUpcoming, actorBy)`) —
  the `SetProfileHideHomeIntroAsync` ADD precedent verbatim: the self-scope
  check happens at the Web boundary (the owner is the actor); the lane
  writes `Profile.HomeShowUpcoming` only; one session, one
  `SaveChangesAsync` (C3); no `AccessAudit` row (a `Profile` field write —
  the `UpsertProfileAsync` shape, "not an access decision"); strong
  consistency (C4) — the new value is live on the very next
  `GetProfileAsync` call; fail closed on a missing profile
  (`KeyNotFoundException` — the `SetProfileTimezoneAsync` pin, never
  load-or-create).
- **D3 — The home "Upcoming" section is a *best-effort* display read.**
  `HomeController.Index` reads the preference (the one profile read serves
  both the `HideHomeIntro` + `HomeShowUpcoming` flags) and, when the
  resident has opted in and is signed-in, builds the two columns — each
  column is the same authorization-aware read its surface's own feed uses
  (`IEventService.ListUpcomingAsync` / `IProjectService.ListTodosAsync`),
  so a row is visible here only when the viewer could already see it in
  that surface (the ADR 0006 "thin token, fat authorization" posture — no
  new `CanSeeAsync` call, no new `AccessAudit` row). A missing seam or a
  read failure degrades that column to empty, never the page (the
  `HomeFeed` best-effort posture). An anonymous visitor (no subject)
  never sees the section — the `BuildFeedAsync` posture.
- **D4 — Each column shows the first 5; overflow is a "show more"
  disclosure.** The view renders the first `HomeUpcoming.Columns` (5) rows
  of each column; overflow rows carry the native `hidden` attribute (a
  JS-less visitor still sees the first five + the whole list stays in the
  markup for screen-reader users — the `mine-more` / `roadmap-more`
  disclosure idiom). A new `home-upcoming-more.ts` client module (the
  `mine-more.ts` self-guard idiom — the ADR 0031 plain-TS convention)
  reveals the rest in batches of 5 on click and removes the button's
  wrapper when nothing remains. A column with ≤5 rows has no button (a
  clean no-op).
- **D5 — The settings page is the ADR 0149 shape extended.** The existing
  `Settings → Home page` surface (the ADR 0149 `HideHomeIntro` checkbox)
  gains a second checkbox (the `homeShowUpcoming` form field) — the same
  "un-checked does not post a value, so `"true"` is the only affirmative
  state" idiom (the `SavePageSize` "un-checked is the floor" shape). The
  `SaveHome` action saves both flags through their own single-write-lane
  seams; a single `settings.home_flash_saved` confirmation covers every
  toggle combination (the form is one save button).
- **D6 — The closed `kw-l` key set × en/de/fr/da.** 7 new keys (the
  `settings.home_upcoming_label` / `settings.home_upcoming_note` /
  `settings.home_flash_saved` / `home.upcoming_title` /
  `home.upcoming_events` / `home.upcoming_todos` /
  `home.upcoming_show_more`) — the `home.feed_*` / `settings.home_*`
  precedent, all four languages present (the ADR 0015 / ADR 0119 D9
  "no English-only key" rule + the `KnownTranslationKeys_ParityTests` pin).
  The `home.upcoming_show_more` label is a `"{n} …"` template — the
  `kw-l` TagHelper cannot substitute `{n}`, so the count is filled in
  server-side (the `home.roadmap.show_more_*` idiom) + refreshed by the
  JS (the `mine-more.ts` `data-hu-label` idiom).

## Status

Accepted. The lane ships (the `0.56.0` `WhatsNew` entry, 2026-10-12 — the
required sixth close-flip member per AGENTS.md).

## Consequences

- **The `Profile` doc gains two additive fields** (the ADR 0004 §B.1
  shape — delta-detected, idempotent, no re-seed, no EF migration; a
  fresh `Profile` has `HomeShowUpcoming = false` **and**
  `HomeUpcomingMineOnly = false` — the ADR 0149 / ADR 0019 "store,
  resolve on read" posture).
- **The `IUserInfoService` seam gains one method** (the
  `SetProfileHomeShowUpcomingAsync` ADD — the `SetProfileHideHomeIntroAsync`
  ADD precedent; the existing methods are untouched).
- **The home page is unchanged for a resident who has not opted in** (the
  section is not in the DOM at all — the `@if (Model.Upcoming is { })`
  wrap; the ADR 0150 SITE·9 idiom). The "Upcoming" section renders **above**
  the "What's new" feed (the request's "above the 'what's new …' section"
  requirement).
- **No re-seed** (the `FirstBootSeeder` / `SampleDataSeeder` are
  untouched — the new field's default is the in-code `false` floor, the
  ADR 0149 / ADR 0019 idiom).
- **No new `AccessAudit` row on the read** (a display read — the
  ADR 0149 "a read, not a decision" pin; the existing `Events` / `Projects`
  read seams' own audit rows are untouched).
- **The `WhatsNew` registry gains a `0.56.0` entry** (the required sixth
  close-flip member per AGENTS.md — the "M27 shipped 2026-10-07 with no
  entry until caught in review" lesson held).

## Tests

- `ProfileHomeShowUpcomingLaneTests` (8 tests, `Kumunita.Core.Tests`) —
  the new write lanes' invariants (default-off, set-true-live,
  false-clears-and-persists, missing-profile-fails-closed — for both
  `HomeShowUpcoming` and `HomeUpcomingMineOnly`), the
  `ProfileTimezoneLaneTests` / `ProfileAvatarLaneTests` idiom.
- `HomeControllerUpcomingTests` (4 tests, `Kumunita.Web.Tests`) — the
  home "Upcoming" section's view-model shape (a fresh profile ⇒ `Upcoming`
  is `null`, `HomeShowUpcoming = true` ⇒ `Upcoming` present with both
  columns, `HomeUpcomingMineOnly = true` ⇒ the mine-scoped reads —
  `ListMineAsync` for events + `assigneeId = subject` for to-dos, the
  community feed read not used, anonymous ⇒ `Upcoming` is `null`), the
  `HomeControllerSiteContentTests` shape.
- `WhatsNewTests` (7 tests, `Kumunita.Web.Tests`) — the version-registry
  head (the required sixth close-flip member, ADR 0110 / AGENTS.md) pins
  the `0.56.0` entry as the head (the `0.55.0` entry slides one row back).

## References

- ADR 0149 — the `Profile.HideHomeIntro` per-resident home-page display
  preference (the shape this ADR mirrors).
- ADR 0004 — persistence & schema evolution (the §B.1 additive idiom).
- ADR 0006 — module boundary contracts (the "thin token, fat
  authorization" posture + the single-write-lane shape).
- ADR 0110 — the `WhatsNew` registry (the required sixth close-flip
  member).
- ADR 0031 — the WYSIWYG editor + toolbar (the plain-TS self-guard
  idiom).
- ADR 0150 — the site-content customization lane (the `@if` DOM-level pin
  idiom + the SITE·9 "hidden section is not in the DOM at all" pin).
- ADR 0119 D9 — the "no English-only key" rule (the closed `kw-l` key set ×
  en/de/fr/da).
