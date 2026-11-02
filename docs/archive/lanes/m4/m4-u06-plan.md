# U06 — Nav entry + `/my/drafts` lane + `kw-dt` timestamps

- **Lane:** Events (`M4`)
- **Unit:** U06 (of U00–U12)
- **Kind:** surface (nav + drafts + timestamps — no new service, no
  tests)

## Goal

Add the **nav entry** for Events (the `_Layout` nav bar), the
**`/my/drafts` lane** (the drafts list page — the ADR 0037
`IsDraft` flag), and verify the **`kw-dt` timestamps** are used in all
three views (feed / detail / composer). The drafts lane reuses the
existing `IEventService` (the `ListDraftsAsync` method — added to the
interface in this unit). **No new service, no new controller, no new
TS, no tests** (U10's controller tests cover the drafts lane).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/Views/Shared/_Layout.cshtml` — the nav bar (the
   existing nav entries to mirror — the Posts / Announcements / Pages
   entries).
2. `src/Kumunita.Web/Controllers/MyController.cs` — the `/my/`
   controller (the `Index` / `Settings` / `Drafts` actions — the
   `Drafts` action to mirror).
3. `src/Kumunita.Web/Views/My/Drafts.cshtml` — the drafts view (the
   existing drafts list to mirror — the `IsDraft = true` filter).
4. `src/Kumunita.Web/TagHelpers/DateTimeTagHelper.cs` — the `kw-dt`
   TagHelper (the ADR 0019 / 0020 timezone / format resolvers).
5. `docs/design/m4-events-design.md` §3.1 — the `Event` field set
   (the `Start` / `End` fields that need `kw-dt` timestamps).

## Deliverables (≤ 4 files)

1. **`src/Kumunita.Web/Views/Shared/_Layout.cshtml`** — the nav entry
   (add the "Events" link to the nav bar, next to the existing
   "Posts" / "Announcements" / "Pages" entries).
2. **`src/Kumunita.Core/Events/IEventService.cs`** — add the
   `ListDraftsAsync` method to the interface (the `IAnnouncementService.
   ListDraftsAsync` shape to mirror — the `IsDraft = true` +
   `IsDeleted = false` filter; the `AuthorId = actorId` filter).
3. **`src/Kumunita.Core/Events/EventService.cs`** — implement the
   `ListDraftsAsync` method (the `AnnouncementService.ListDraftsAsync`
   shape to mirror).
4. **`src/Kumunita.Web/Controllers/MyController.cs`** — add the
   `Drafts` action (the `IEventService.ListDraftsAsync` call; the
   `ViewBag` setup).
5. **`src/Kumunita.Web/Views/My/Drafts.cshtml`** — add the Events
   section to the drafts view (the existing Posts / Announcements
   sections to mirror — the `kw-dt` timestamp; the "Edit" / "Publish" /
   "Delete" buttons).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- The nav entry + the `/my/drafts` lane + the `kw-dt` timestamps are
  in place.
- **No new test** (U10's controller tests cover the drafts lane).
- Handoff note: 5 lines starting `## U06 — nav + drafts + kw-dt` —
  (a) the nav entry (the "Events" link in the nav bar), (b) the
  `ListDraftsAsync` method (the `IAnnouncementService.ListDraftsAsync`
  shape to mirror), (c) the `MyController.Drafts` action (the
  `IEventService.ListDraftsAsync` call), (d) the `kw-dt` TagHelper
  usage in the three views (feed / detail / composer), (e) the
  `IsDraft = true` + `IsDeleted = false` filter (the ADR 0037 / ADR
  0024 flags).

## Notes / deviations

- The drafts lane reuses the **existing `IEventService`** (the
  `ListDraftsAsync` method — the `IAnnouncementService.ListDraftsAsync`
  shape to mirror). **No new service, no new controller mechanism.**
- The `kw-dt` TagHelper is the **ADR 0019 / 0020 timezone / format
  resolvers** (the `EffectiveTimezoneResolver` + the
  `EffectiveDateFormatResolver`). **No new timezone or format
  mechanism.**
- The nav entry is the **existing nav bar** (the `_Layout.cshtml` nav
  bar — the Posts / Announcements / Pages entries). **No new nav
  mechanism.**
