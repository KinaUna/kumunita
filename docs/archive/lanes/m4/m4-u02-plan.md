# U02 — `EventToAuditableResource` adapter

- **Lane:** Events (`M4`)
- **Unit:** U02 (of U00–U12)
- **Kind:** structure (the authorization adapter — no behavior)

## Goal

Create the `EventToAuditableResource` adapter (namespace
`Kumunita.Core.Events`) — the `PostToAuditableResource` shape verbatim
(ADR 0006: the frozen `IAuthorizationService` surface; the adapter is the
only bridge between the new `Event` doc and the frozen 6-member
`IAuditableResource`). **No `IAuthorizationService` change, no
`AccessVia` change, no new branch** — the adapter is the only new code.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — the adapter
   shape to mirror (the 6-member `IAuditableResource`: `Id`, `Name`,
   `OwnerId`, `Audience`, `ComponentId`, `TargetKind`).
2. `src/Kumunita.Core/Authorization/IAuthorizationService.cs` — the
   frozen surface (the `CanAccessAsync` / `IsInAudienceAsync` /
   `EvaluateAsync` methods).
3. `src/Kumunita.Core/Events/Event.cs` — the POCO (U01's output).
4. `docs/design/m4-events-design.md` §3.3 — the exact adapter field
   mapping (the primary source for this unit).
5. `src/Kumunita.Core/Announcements/Announcement.cs` — the `Audience`
   field shape (the `Kumunita.Core.Authorization.Audience` embedded doc).

## Deliverables (≤ 2 files)

1. **`src/Kumunita.Core/Events/EventToAuditableResource.cs`** — the
   adapter (a `sealed record` or `sealed class` implementing
   `IAuditableResource`):
   - `Id => Event.Id`
   - `Name => Event.Title ?? Event.Body[..Math.Min(60, Event.Body.Length)]`
     (the post adapter's 60-char fallback — the `PostToAuditableResource`
     shape verbatim)
   - `OwnerId => Event.AuthorId`
   - `Audience => Event.Audience` (the **exact** post `Audience` shape —
     ADR 0001-B / 0036)
   - `ComponentId => Event.ComponentId`
   - `TargetKind => "event"` (the new kind string — the `Post` adapter
     uses `"post"`, the `Announcement` adapter uses `"announcement"`, the
     `Page` adapter uses `"page"`; the `Event` adapter uses `"event"`)
2. **`src/Kumunita.Core/Events/EventToAuditableResource.cs`** — the
   `FromEvent(Event e)` static factory (the `PostToAuditableResource`
   factory to mirror).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- The adapter compiles against the frozen `IAuthorizationService` (the
  `CanAccessAsync` / `IsInAudienceAsync` / `EvaluateAsync` methods — no
  change to the frozen surface).
- **No new test** (U09's seam tests are the first M4 tests).
- Handoff note: 4 lines starting `## U02 — EventToAuditableResource
  adapter` — (a) the 6-member field mapping (verbatim), (b) the
  `TargetKind = "event"` pin, (c) the `Name` 60-char fallback (the post
  adapter's shape), (d) the `IAuthorizationService` frozen surface
  (unchanged — ADR 0006).

## Notes / deviations

- The adapter is the **`PostToAuditableResource` shape verbatim** (ADR
  0006: the frozen `IAuthorizationService` surface; the adapter is the
  only bridge between the new `Event` doc and the frozen 6-member
  `IAuditableResource`). **No `IAuthorizationService` change, no
  `AccessVia` change, no new branch** — the adapter is the only new
  code.
- The `Name` field's 60-char fallback is the **post adapter's shape**
  (the `PostToAuditableResource`'s `Name => Post.Title ??
  Post.Body[..Math.Min(60, Post.Body.Length)]` shape verbatim). The
  `Event.Title` is the primary name; the `Event.Body` is the fallback
  (the `Event` has a `Body` field, unlike the `Post` which has a `Body`
  field — the shape is the same).
- The `TargetKind = "event"` is the **new kind string** (the `Post`
  adapter uses `"post"`, the `Announcement` adapter uses
  `"announcement"`, the `Page` adapter uses `"page"`; the `Event`
  adapter uses `"event"`). **No change to the existing `TargetKind`
  strings.**
- The adapter's `Audience` field is the **exact** post `Audience` shape
  (ADR 0001-B / 0036) — the `Kumunita.Core.Authorization.Audience`
  embedded doc. **No new audience mechanism.**
