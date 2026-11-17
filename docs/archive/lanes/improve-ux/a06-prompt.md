---
id: a06-prompt
title: "Prompt: IMPROVE-UX A06 — the moderation nav badge + the queue's friendly empty state"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0303-report-queued-routing.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A06** — the sixth and final unit of the **admin/moderator** block, and
> the visible *loop closer* for the E3 finding ("new reports reach no
> moderator"). A05 shipped the **routing** half (a filed report now produces
> one `report.queued` inbox row per standing moderator + GlobalAdmin,
> deduped, no content in the body, `linkPath = /moderation/{id}`); A06 ships
> the **badge** half (a count on the Moderation menu item in the account
> nav — the "open" count the actor may see, scoped by the same query the
> queue uses, cached ≤60 s) **and** the queue's **friendly empty state**
> (the "Moderation/Index is A06's" pin that R33 left as the last plain
> empty state). **Who is it for:** the next coding agent (fresh context).
> **When to read the rest:** when starting A06 (A01–A05 are shipped; the
> read lane honours `PostStatus`, the resolve lane executes the outcome for
> post + grouppost targets, `UnhidePostAsync` exists, `report.queued` routes
> on intake — no blockers).

# Fresh-context prompt: IMPROVE-UX A06

You are continuing the Kumunita repository on `development` (HEAD
`45f1443c`, A01 + A02 + A03 + A04 + A05 all landed — A01 makes the read
paths honour `PostStatus`; A02 makes the resolve lane execute the outcome
for post targets; A03 adds `UnhidePostAsync` + the "Unhide" button; A04
extends outcome-execution to grouppost targets; A05 routes filed reports
to the component's standing moderators + GlobalAdmins as one
`report.queued` inbox row per recipient). **A06** is the next unit. The
plan card (in `plan-improve-ux.md`, the A06 row):

> A06 — Nav badge: open-report count the actor may see (same scoped query
> as the queue, cached ≤60 s) on the Moderation menu item;
> `Moderation/Index` friendly empty state.
> Read first: `_AccountNav.cshtml:210-230`, `ModerationService.GetQueueAsync`.
> **Done when: badge shows; 0 hides.**

## The state of the code (measured 2026-11-11, `development`)

- **The scoped read seam** —
  `ModerationService.GetQueueAsync(actorId, isGlobalAdmin, isStanding)`
  (`src/Kumunita.Core/Moderation/ModerationService.cs:478`) returns a
  `ModerationQueueReadModel` with `Rows` (the scoped rows, `At` desc /
  `Status` desc) **and** `ByStatus` — a
  `IReadOnlyDictionary<string, int>` seeded to
  `{ "filed": 0, "assigned": 0, "unlocked": 0, "resolved": 0 }` and
  incremented over the scoped rows
  (`src/Kumunita.Core/Moderation/ModerationReadModels.cs:72-77`). The
  "open" count is
  **`Rows.Where(r => r.Status is "filed" or "assigned" or "unlocked")`** —
  i.e. the queue's `Rows.Count - ByStatus["resolved"]` (a
  `resolved` report is done; a `filed` / `assigned` / `unlocked` report
  is still in the actor's work queue). The scoping is already
  correct (the C-M3b·4 SoD shape: GlobalAdmin → every report; standing
  moderator → reports where they hold a `ModeratorAssignment` row on the
  report's `ComponentId` **and** the component's `Component.ModeratorAccess`
  flag is ON; a plain resident → zero reports — the
  `ModerationService.cs:495-517` block). **A06's badge count re-uses this
  exact scoped query — it does not invent a new one.**

- **The count seam (the A06 ADD).** The `ModerationQueueReadModel`
  already carries `Rows` + `ByStatus`, so a
  `ModerationService.CountOpenReportsAsync(actorId, isGlobalAdmin,
  isStanding, ct)` read seam (a **read lane**, not a decision lane — no
  `CanAsync` call, no `AccessAudit` row, the
  `GetQueueAsync` / `GetDetailAsync` shape) that returns
  `Rows.Count - ByStatus["resolved"]` is the narrowest shape. It can
  **either** re-derive the scoped query (a second, lightweight pass over
  the same `ModeratorAssignment` + `Component.ModeratorAccess` reads — the
  nav partial runs on every authenticated request, so a lean count query
  beats a full `GetQueueAsync` (which also resolves every row's display
  name, the reporter, the reply-author, the target's title + author —
  the A06 nav cost the badge does not need)) **or** be a pure function
  over the `GetQueueAsync` result (reuse the full call, drop the display
  work). **The prompt leaves the choice to the implementer; the pin is
  "same scoped query as the queue" — whichever shape ships, the badge's
  count is provably equal to the queue's open count for the same actor
  in the same window** (a test pins this).

- **The cache (the A06 "cached ≤60 s" pin).** The badge must not run a
  Marten query on **every** authenticated request. The shape to use is
  the instance-level `IMemoryCache` keyed by
  `(actorId, isGlobalAdmin, isStanding)` with a 60 s sliding window.
  **`IMemoryCache` is not yet registered in `Kumunita.Web`** (the
  `WebApplicationBuilder` does **not** auto-add it — the ADR 0006-D
  "Core is HTTP-free" boundary means the cache lives in the **Web**
  layer, so the implementer adds one line to `src/Kumunita.Web/Program.cs`:
  `builder.Services.AddMemoryCache();` — the partial reads it, the
  `IModerationService` seam stays pure Core). If a cleaner shape exists
  in the repo (a `Cache` partial, an `IModerationBadgeCache` seam, a
  `Kumunita.Web/Security` helper), use it; the pin is "≤60 s, per
  (actor, standing)" — the A06 "the badge does not double the queue's
  DB load" invariant.

- **The badge surface** —
  `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml`. The two Moderation
  menu items are at `:225` (the GlobalAdmin branch, alongside the
  `/admin/error-reports` row) and `:238` (the standing-moderator
  branch). Both are plain
  `<a class="dropdown-item text-dark" href="/moderation"><svg
  class="kmb-nav-icon kmb-icon" width="18" height="18"
  aria-hidden="true"><use href="#kmb-i-moderation"/></svg><kw-l
  key="nav.moderation">Moderation</kw-l></a>`. A06 adds a
  `<span class="kmb-nav-badge">` (a new
  `.kmb-nav-badge` class in the site CSS, Bootstrap
  `badge bg-secondary rounded-pill`-shaped — the
  `kmb-nav-icon` / `kmb-empty` naming shape, the
  `site-css-media-block-pin` doctrine) between the `</svg>` and the
  `<kw-l>`, with the count as the text, only when the count is `> 0`
  (a 0-badge is a noise path — the A06 "0 hides" pin), and **only** on
  the two Moderation rows (a plain resident sees neither row — the
  `else if (KumunitaPrincipal.IsModerator(User))` branch means the
  plain-resident nav never has a Moderation link at all, so the badge
  is unreachably scoped by construction — no extra guard needed).

- **The queue's friendly empty state (the A06 DoD's second half).**
  `src/Kumunita.Web/Views/Moderation/Index.cshtml:16-21` — the
  `@if (Model.TotalCount == 0)` branch currently renders a bare
  `<div class="alert alert-secondary"><kw-l
  key="moderation.empty">No reports yet. The queue is empty.</kw-l></div>`
  (one of the `empty_plain` `ux-measure.py` leads — the R29–R33 batch
  moved the others to `.kmb-empty`; Moderation/Index is the last one,
  R33's "Moderation/Index is A06's" pin). The reference shape is
  `Views/Groups/Index.cshtml:143-152` (the ADR 0177 friendly-empty-state
  shape, the R29 DoD "their leads → 0" pin): a
  `<div class="kmb-empty">` with a `kmb-empty-title` + a
  `kmb-empty-body` (the `groups.empty` + `groups.index_empty_body`
  pair). A06's empty state keeps the existing
  `moderation.empty` title key ("No reports yet. The queue is empty.")
  and adds **one** new key, `moderation.empty_body` — a plain sentence
  that reports reach this queue from residents (the A05 close — the
  `report.queued` lane the E3 finding closes): "When a resident files
  a report on content in a community you moderate, it will appear
  here." (no link — the queue is a passive inbox, not a resident
  surface; the "one next action" shape as R29, but the next action is
  *passive* — the resident files, the moderator acts).

- **The translation surface.** The badge needs **no new key** (a
  count, not a sentence — the `kw-l` label
  `nav.moderation` is already localized × 4). The empty state needs
  **one** new key in all four languages (en/de/fr/da × one JSON —
  `moderation.empty_body`, the `moderation.json` surface file; the
  existing `moderation.empty` title key stays as-is — only the
  `kmb-empty-body` text is new). The parity test
  (`tests/Kumunita.Core.Tests/KnownTranslationKeys_ParityTests.cs`)
  pins the 4-language key-set match — that one key is the **only**
  translation-surface change.

- **The test harness.** The badge count seam's test file is
  `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (the same
  class as A01–A05 — the `PostgresFixture` / `BootStoreAsync` /
  `Services` trio; the A02/A04 test block is the closest precedent for a
  "scoped read returns N rows" assertion; the A05 `ServicesWithRouting`
  helper is the closest precedent for a "wire a seam, assert the
  scoped result" shape). The nav-partial badge render is pinned by a
  Web-layer test in
  `tests/Kumunita.Web.Tests/` (the `_AccountNav` partial — find an
  existing nav-partial test or a controller test that renders the nav
  to confirm the badge span is present with the right count + the
  0-hides guard).

## The work

1. **The count seam (the A06 ADD).** One new read seam on
   `IModerationService` + `ModerationService`:
   `Task<int> CountOpenReportsAsync(string actorId, bool isGlobalAdmin,
   bool isStanding, CancellationToken ct = default)`. The body:
   re-derive the **exact same scoped query** `GetQueueAsync` uses
   (`ModerationService.cs:495-517` — the standing-moderator branch's
   `myComponents` ∩ `Component.ModeratorAccess == true` filter, the
   plain-resident branch's empty set, the GlobalAdmin branch's
   unscoped set) but return only the **count** of rows whose
   `Status is "filed" or "assigned" or "unlocked"` (a
   `resolved` report is done — not in the badge). **Do not** re-derive
   a different scoped query (the A06 pin "same scoped query as the
   queue"); the test pins this (see A06-1). No `CanAsync` call, no
   `AccessAudit` row (a read lane — the `GetQueueAsync` /
   `GetDetailAsync` shape, C-M3b·4 SoD display-level projection).

2. **The Web-layer cache (the A06 "cached ≤60 s" pin).** In the
   `_AccountNav.cshtml` partial (or a small `Kumunita.Web` helper
   partial, the ADR 0006-D boundary — the cache lives in Web, the
   seam stays pure Core): resolve
   `IModerationService.CountOpenReportsAsync` through
   `IMemoryCache` keyed by `(actorId, isGlobalAdmin, isStanding)`
   with a 60 s sliding expiration. **`IMemoryCache` is not yet
   registered in `Kumunita.Web`** — add one line to
   `src/Kumunita.Web/Program.cs`: `builder.Services.AddMemoryCache();`
   (the `WebApplicationBuilder` does not auto-add it). The first call per (actor, standing) in a 60 s window
   hits the DB; subsequent calls in the window read the cache. The
   badge render never re-queries for the same actor within 60 s
   (the A06 "the badge does not double the queue's DB load" pin).
   Cache the count, not the rows (the nav does not render the
   queue's rows).

3. **The badge on the two Moderation menu items.**
   `_AccountNav.cshtml:225` (GlobalAdmin branch) + `:238`
   (standing-moderator branch) — both currently
   `<a class="dropdown-item text-dark" href="/moderation"><svg
   class="kmb-nav-icon kmb-icon" width="18" height="18"
   aria-hidden="true"><use href="#kmb-i-moderation"/></svg><kw-l
   key="nav.moderation">Moderation</kw-l></a>`. Between the `</svg>`
   and the `<kw-l>`, add
   `<span class="kmb-nav-badge badge bg-secondary rounded-pill ms-2"
   data-nav-badge="moderation">@openCount</span>`
   **only when** `openCount > 0` (a 0-badge is a noise path — the A06
   "0 hides" pin). A plain resident sees neither Moderation link (the
   `else if (KumunitaPrincipal.IsModerator(User))` branch — the
   plain-resident nav has no Moderation row at all), so no extra
   guard is needed on the badge's visibility — the scope is by
   construction. The `@openCount` comes from the cached seam call
   (the partial's model or an injected `IModerationService` — the
   `Kumunita.Web` controller / partial-injection shape, the
   ADR 0006-D "Core is HTTP-free" boundary: the seam is a Core
   interface, the cache is Web).

4. **The queue's friendly empty state.**
   `Views/Moderation/Index.cshtml:16-21` — the
   `@if (Model.TotalCount == 0)` branch currently renders a bare
   `<div class="alert alert-secondary"><kw-l key="moderation.empty">
   No reports yet. The queue is empty.</kw-l></div>`. Replace it with
   the `.kmb-empty` shape (the reference is
   `Views/Groups/Index.cshtml:143-152` — the ADR 0177 shape, a
   `<div class="kmb-empty">` with a `kmb-empty-title` + a
   `kmb-empty-body`). Keep the existing `moderation.empty` title key;
   add **one** new key, `moderation.empty_body`:
   ```html
   <div class="kmb-empty">
       <p class="kmb-empty-title"><kw-l key="moderation.empty">No reports yet. The queue is empty.</kw-l></p>
       <p class="kmb-empty-body"><kw-l key="moderation.empty_body">When a resident files a report on content in a community you moderate, it will appear here.</kw-l></p>
   </div>
   ```
   One new key (`moderation.empty_body`), all four languages
   (en/de/fr/da × the `moderation.json` surface file; the existing
   `moderation.empty` title key stays as-is — the `kmb-empty-title`
   wraps the same text; the `KnownTranslationKeys_ParityTests` parity
   pin applies). The `ux-measure.py --check` `empty_plain` count
   **drops by 1** (Moderation/Index was the last plain empty state R33
   left for A06 — the `alert alert-secondary` → `kmb-empty` restyle).

5. **The A06 tests** — in `tests/Kumunita.Core.Tests/ModerationServiceTests.cs`
   (mirroring the A05 `ServicesWithRouting` shape) + a
   Web-layer test for the badge render:
   - **A06-1 — `CountOpenReportsAsync_GlobalAdmin_SameScopedQueryAsQueue`**
     — plant: a component with `ModeratorAccess = true` + a standing
     moderator `a06-1-mod` on it + one `filed` report on the component
     + one `resolved` report on the component + one `filed` report on
     a *different* component the moderator is not on. Assert
     `CountOpenReportsAsync("a06-1-mod", isGlobalAdmin: false,
     isStanding: true)` returns `1` (the filed report the moderator
     is on; the resolved one is not open; the other-component report
     is out of scope). Assert the **same** call with
     `isGlobalAdmin: true` returns `2` (the two `filed` reports, the
     resolved one still excluded) — the **same scoped query as the
     queue** pin (the A06 "the badge count is provably equal to the
     queue's open count" invariant).
   - **A06-2 — `CountOpenReportsAsync_PlainResident_Zero`** — a plain
     resident (no `ModeratorAssignment` row, not a GlobalAdmin) sees
     zero reports, the C-M3b·4 SoD display-level projection. Plant a
     `filed` report on a component the resident has no standing on;
     assert the count is `0`.
   - **A06-3 — `CountOpenReportsAsync_ResolvedReport_NotOpen`** — a
     `resolved` report is not "open" (the A06 "a `resolved` report is
     done" pin). Plant: a component with a standing moderator + one
     `filed` + one `resolved` report on the same component. Assert
     the count is `1` (the filed one; the resolved one excluded).
   - **A06-4 — the Web badge render (Web test)** — a
     `_AccountNav` partial (or a controller test that renders the
     nav) shows the badge span with the count when the count is
     `> 0` and **does not show it** when the count is `0` (the A06
     "0 hides" pin — a 0-badge is a noise path). The plain-resident
     nav (no Moderation link at all) is a 404 / no-link test, not a
     badge test (the badge is unreachably scoped by construction).
   - **A06-5 — the empty state renders (Web test)** —
     `Moderation/Index` with an empty queue renders a
     `<div class="kmb-empty">` (the `kmb-empty-title` wrapping the
     existing `moderation.empty` key + the `kmb-empty-body` wrapping
     the new `moderation.empty_body` key), not the bare
     `alert alert-secondary` it currently renders. The
     `ux-measure.py --check` `empty_plain` count drops by 1.
     (The `alert` class is a plain-empty-state lead; the `kmb-empty`
     class is the friendly shape — the R29–R33 batch moved 48
     views to it, Moderation/Index is the last one left for A06.)

6. **Verify the gates.** `dotnet build Kumunita.slnx`, run
   `ModerationServiceTests` (all A06 tests + the A05 tests to confirm
   the routing still works + the existing `GetQueueAsync` tests to
   confirm the scoped query is unchanged), and
   `python3 tools/ux/ux-measure.py --check`
   (A06 adds **no** new Web action, **no** new view — the badge is a
   partial change + the empty state is a view change; `bare_status`
   stays at 710, `a11y` / `jargon` / `notify_without_link` unchanged;
   **`empty_plain` drops from 37 to 36** — the Moderation/Index
   empty state moves from the bare `alert alert-secondary` to
   `.kmb-empty`). The translation-parity test
   (`*KnownTranslationKeys*`) must stay green (the one new
   `moderation.empty_body` key × en/de/fr/da).

## Scope rules

- **The count seam + the `AddMemoryCache()` registration + the Web
  cache + the badge on the two Moderation menu items + the queue's
  friendly empty state + the one `moderation.empty_body` key × 4
  languages + the tests.** Do **not** change
  the A05 routing (the `report.queued` emission is shipped; A06 is
  the badge, not the routing). Do **not** add a new Web action (the
  badge is a partial; the count seam is a Core read lane, the cache
  is Web — the ADR 0006-D boundary holds). Do **not** re-derive a
  different scoped query (the A06 pin "same scoped query as the
  queue"); the test A06-1 pins this. Do **not** render a 0-badge
  (the A06 "0 hides" pin). Do **not** widen `IUserInfoService`
  (the scoped query is the
  `IProfileSettingsService.GetAssignmentsAsync` shape — the
  standing-moderator branch already reads it, the A06 count seam
  re-uses the same read, not a new one). Do **not** cache the queue's
  rows in the nav (the nav renders a count, not a list — the
  "the badge does not double the queue's DB load" pin).
- **One commit** (the house rule): `feat(ux): moderation nav badge (open-report count, scoped, cached ≤60 s) + the queue's friendly empty state (IMPROVE-UX A06)`.

## Files that should change (revised)

- `src/Kumunita.Web/Program.cs` (one line: `builder.Services.AddMemoryCache();` —
  not yet registered in the Web host)
- `src/Kumunita.Core/Moderation/IModerationService.cs` + the
  `ModerationService` impl (the `CountOpenReportsAsync` read seam)
- `src/Kumunita.Web/Views/Shared/_AccountNav.cshtml` (the badge on
  the two Moderation menu items at `:225` / `:238` + the `IMemoryCache`
  per-actor read)
- `src/Kumunita.Web/Views/Moderation/Index.cshtml` (the
  `.kmb-empty` empty state — replacing the bare `alert alert-secondary`)
- `src/Kumunita.Web/wwwroot/site.css` (the `.kmb-nav-badge` class —
  the `kmb-nav-icon` / `kmb-empty` naming shape)
- `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/moderation.json`
  (the `moderation.empty_body` key × 4 languages — the existing
  `moderation.empty` title key is reused, not new)
- `tests/Kumunita.Core.Tests/ModerationServiceTests.cs` (the A06-1 /
  A06-2 / A06-3 tests)
- `tests/Kumunita.Web.Tests/` (a nav-partial test for the badge
  render — A06-4 — + a `Moderation/Index` empty-state test — A06-5)

(≤ ~7 Core + Web + test files — the unit-size rule; the 4×1 JSON key
file counts as the translation-parity surface, not a separate unit.)

## Before committing

```bash
dotnet build Kumunita.slnx -c Debug
python3 tools/ux/ux-measure.py --check   # empty_plain 37 -> 36, everything else unchanged
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*A06*"
# + the A05 tests (the routing is unchanged — A06 is the badge, not the routing):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*A05*"
# + the GetQueueAsync tests (the scoped query is unchanged):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.ModerationServiceTests" -method "*Queue*"
# + the translation-parity test (the two new moderation.* keys × en/de/fr/da):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -method "*KnownTranslationKeys*"
```

If a test fails, read the error, adjust, continue. Then report:
the count-seam shape (file:line in `ModerationService.cs`, the
scoped-query reuse pin), the Web cache shape (the `IMemoryCache`
key + the 60 s expiration), the badge render (file:line in
`_AccountNav.cshtml`, each of the two Moderation menu items), the
empty state (file:line in `Views/Moderation/Index.cshtml`), the
test names + pass counts, the `ux-measure.py --check` line (note the
`empty_plain` drop), and the commit hash. Do not commit unrelated
work.
