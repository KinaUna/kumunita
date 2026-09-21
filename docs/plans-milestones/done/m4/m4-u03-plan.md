# U03 — `EventService` read lanes + standing matrix

- **Lane:** Events (`M4`)
- **Unit:** U03 (of U00–U12)
- **Kind:** behavior (read + standing — no write, no controller)

## Goal

Implement the `EventService` **read lanes** (the `ListUpcomingAsync` /
`GetAsync` methods) + the **standing matrix** (the `CheckCreateStanding` /
`CheckEditStanding` gates) in `Kumunita.Core`. Mirror the
`AnnouncementService` read lanes + the `AnnouncementService.CreateAsync`
C3 pattern (the `CheckCreateStanding` gate). **No write lanes, no
controller, no tests** (U04's write lanes; U05's controller; U09's
tests).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Core/Announcements/AnnouncementService.cs` (the read
   lanes: `ListVisibleAsync` / `GetAsync` / `PinnedAsync` — the
   `CheckCreateStanding` gate in `CreateAsync`) — the shape to mirror.
2. `src/Kumunita.Core/Posts/PostService.cs` (the `ListAsync` /
   `GetAsync` / `CheckCreateStanding` / `CheckEditStanding` methods) —
   the post shape to mirror.
3. `src/Kumunita.Core/Events/Event.cs` — the POCO (U01's output).
4. `src/Kumunita.Core/Events/EventToAuditableResource.cs` — the adapter
   (U02's output).
5. `docs/design/m4-events-design.md` §3.4 — the standing matrix (the
   primary source for this unit).

## Deliverables (≤ 3 files)

1. **`src/Kumunita.Core/Events/IEventService.cs`** — the service
   interface (the `IAnnouncementService` shape to mirror):
   - `Task<IReadOnlyList<Event>> ListUpcomingAsync(string? componentId,
     string? tagId, CancellationToken ct)` — the feed (the upcoming events
     in the component, ordered by `Start` ascending; the `IsDraft = false`
     + `IsDeleted = false` filter; the audience check via the frozen
     `IAuthorizationService.CanAccessAsync`).
   - `Task<Event?> GetAsync(string id, CancellationToken ct)` — the detail
     (the single event by id; the audience check via the frozen
     `IAuthorizationService.CanAccessAsync`; the `IsDraft` + `IsDeleted`
     filter).
   - `Task<bool> CheckCreateStanding(string actorId, string? componentId,
     CancellationToken ct)` — the create gate (the
     `AnnouncementService.CreateAsync` C3 pattern; the
     `IAuthorizationService.CanAccessAsync` check against the component's
     audience).
   - `Task<bool> CheckEditStanding(string actorId, string eventId,
     CancellationToken ct)` — the edit gate (the ADR 0014 / 0016 / 0017
     author-only precedent; the `GlobalAdmin` override).
2. **`src/Kumunita.Core/Events/EventService.cs`** — the implementation
   (the `AnnouncementService` read lanes to mirror):
   - `ListUpcomingAsync` — the feed (the upcoming events in the component,
     ordered by `Start` ascending; the `IsDraft = false` + `IsDeleted =
     false` filter; the audience check via the frozen
     `IAuthorizationService.CanAccessAsync`).
   - `GetAsync` — the detail (the single event by id; the audience check
     via the frozen `IAuthorizationService.CanAccessAsync`; the
     `IsDraft` + `IsDeleted` filter).
   - `CheckCreateStanding` — the create gate (the
     `AnnouncementService.CreateAsync` C3 pattern; the
     `IAuthorizationService.CanAccessAsync` check against the component's
     audience).
   - `CheckEditStanding` — the edit gate (the ADR 0014 / 0016 / 0017
     author-only precedent; the `GlobalAdmin` override; the
     `EventToAuditableResource` adapter + the frozen
     `IAuthorizationService.CanAccessAsync` check).
3. **`src/Kumunita.Core/DependencyInjection.cs`** — the
   `IEventService` → `EventService` registration (the
   `IAnnouncementService` → `AnnouncementService` registration to
   mirror; the `EventService` constructor takes `IMailerStage` +
   `IAuthorizationService` + `ILocalizationService` + `ITagService` +
   `IMediaService` + `IAccountAuditService` — the
   `AnnouncementService` constructor shape to mirror).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- The `IEventService` interface + the `EventService` read lanes + the
  standing matrix compile.
- **No new test** (U09's seam tests are the first M4 tests).
- Handoff note: 5 lines starting `## U03 — EventService read lanes +
  standing matrix` — (a) the `IEventService` method count (4:
  `ListUpcomingAsync` / `GetAsync` / `CheckCreateStanding` /
  `CheckEditStanding`), (b) the standing matrix shape (the ADR 0014 /
  0016 / 0017 author-only precedent; the `GlobalAdmin` override), (c) the
  `EventService` constructor dependencies (the
  `AnnouncementService` constructor shape to mirror), (d) the
  `DependencyInjection.cs` registration line (the
  `IAnnouncementService` → `AnnouncementService` registration to
  mirror).

## Notes / deviations

- The standing matrix is the **ADR 0014 / 0016 / 0017 author-only
  precedent** (the post / reply / announcement edit gate) — the author
  can edit their own event; the `GlobalAdmin` can edit any event. **No
  new authorization branch, no new `AccessAction` enum value, no new
  `AccessVia` enum value** — the frozen `IAuthorizationService` surface
  (ADR 0006) is reused as-is.
- The `EventService` constructor is the **`AnnouncementService`
  constructor shape to mirror** (the `IMailerStage` +
  `IAuthorizationService` + `ILocalizationService` + `ITagService` +
  `IMediaService` + `IAccountAuditService` dependencies). **No new
  dependency injection mechanism.**
- The `ListUpcomingAsync` + `GetAsync` read lanes are the **`AnnouncementService`
  read lanes to mirror** (the `ListVisibleAsync` / `GetAsync` shape).
  **No new query mechanism.**
- The `CheckCreateStanding` + `CheckEditStanding` gates are the **ADR
  0014 / 0016 / 0017 author-only precedent** (the post / reply /
  announcement edit gate). **No new standing mechanism.**
