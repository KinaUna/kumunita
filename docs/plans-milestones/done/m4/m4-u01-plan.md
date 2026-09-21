# U01 — `Event` + `EventRsvp` docs + `M4DocTypes` registration + boot wiring

- **Lane:** Events (`M4`)
- **Unit:** U01 (of U00–U12)
- **Kind:** structure (documents + registration + DI — no behavior)

## Goal

Create the `Event` + `EventRsvp` POCOs (namespace `Kumunita.Core.Events`)
and a new `M4DocTypes.Configure(StoreOptions)` registration surface; wire
it into both boot paths (the dev loop in `Program.cs` and the all-env
`SchemaBootstrap`). Mirror the M3 `M3DocTypes` pattern exactly. **Zero
behavioral change** — the milestone's first *structure* unit.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Core/M3DocTypes.cs` — the registration shape to mirror
   (the `M3DocTypes.Configure` method; the `Schema.For<T>()` calls).
2. `src/Kumunita.Core/Announcements/Announcement.cs` — the POCO shape to
   mirror (the field set; the ADR 0037 `IsDraft` flag; the ADR 0024
   `IsDeleted` flag; the ADR 0018 `LanguageCode`; the ADR 0044 `TagIds`;
   the ADR 0025 `ImageIds`; the ADR 0034 `AttachmentIds`).
3. `docs/design/m4-events-design.md` §3.1 — the exact `Event` field set
   (the primary source for this unit).
4. `src/Kumunita.Core/DependencyInjection.cs` — where to add the
   `IEventService` → `EventService` registration (the
   `IAnnouncementService` → `AnnouncementService` registration to
   mirror).
5. `src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs` — where to add the
   `M4DocTypes.Configure(opts)` call (the `M3DocTypes.Configure(opts)`
   call to mirror).

## Deliverables (6 files)

1. **`src/Kumunita.Core/Events/Event.cs`** — POCO with the design doc
   §3.1 field set: `Id` (string), `Title` (string), `Body` (string,
   Markdown), `ComponentId?` (string), `AuthorId` (string), `Start`
   (DateTimeOffset), `End` (DateTimeOffset), `Location?` (string),
   `Capacity?` (int), `Audience` (the **exact** post `Audience` shape —
   the `Kumunita.Core.Authorization.Audience` embedded doc),
   `ReminderEnabled` (bool, default `true`), `IsDraft` (bool, default
   `true` — ADR 0037), `IsDeleted` (bool, default `false` — ADR 0024),
   `LanguageCode` (string — ADR 0018), `TagIds` (IReadOnlyList<string> —
   ADR 0044), `ImageIds` (IReadOnlyList<string> — ADR 0025),
   `AttachmentIds` (IReadOnlyList<string> — ADR 0034), `Created`
   (DateTimeOffset), `Modified?` (DateTimeOffset).
2. **`src/Kumunita.Core/Events/EventRsvp.cs`** — POCO with the design
   doc §3.2 shape: `Id` (string), `EventId` (string), `UserId` (string),
   `Status` (enum `EventRsvpStatus`: `Going` | `Maybe` | `No`), `At`
   (DateTimeOffset). **Last-write-wins** concurrency exception (the
   `docs/ARCHITECTURE.md` §5 exception — keyed per `(EventId, UserId)`;
   a conflicting RSVP is a no-op, the resident's latest status is the
   truth).
3. **`src/Kumunita.Core/Events/EventRsvpStatus.cs`** — the enum:
   `Going`, `Maybe`, `No`.
4. **`src/Kumunita.Core/M4DocTypes.cs`** — `public static class
   M4DocTypes { public static void Configure(StoreOptions opts) {
   opts.Schema.For<Event>(); opts.Schema.For<EventRsvp>()
   .UniqueIndex(r => r.EventId + r.UserId); } }`. The `(EventId,
   UserId)` unique index enforces the last-write-wins concurrency
   exception (a conflicting RSVP is a no-op — the resident's latest
   status is the truth).
5. **`src/Kumunita.Core/Bootstrap/SchemaBootstrap.cs`** — add the
   `using` + the `M3DocTypes.Configure(opts);` → `M4DocTypes.Configure(opts);`
   line *next to* the existing M3 call (one line added).
6. **`src/Kumunita.Web/Program.cs`** — add the `M4DocTypes.Configure(...)`
   call in the dev-loop path, next to the existing `M3DocTypes.Configure(...)`
   line (one line added).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `M4DocTypes.cs` exists; the two POCOs + the enum compile.
- **No new test** (U03's seam tests are the first M4 tests).
- Handoff note: 5 lines starting `## U01 — documents + M4DocTypes +
  boot` — (a) the `M4DocTypes` line count (2 `.Schema.For` calls), (b)
  the two boot-path lines added (file + line numbers), (c) the
  `EventRsvp` `(EventId, UserId)` unique index (the last-write-wins
  pin), (d) any compile warnings on the new POCOs.

## Notes / deviations

- The `Event.Audience` field is the **exact** post `Audience` shape
  (ADR 0001-B / 0036) — the `Kumunita.Core.Authorization.Audience`
  embedded doc. **No new audience mechanism.**
- The `EventRsvp` `(EventId, UserId)` unique index is the
  **last-write-wins** concurrency exception (the
  `docs/ARCHITECTURE.md` §5 exception) — a conflicting RSVP write is a
  no-op, the resident's latest status is the truth. **No `AccessAudit`
  row** on an RSVP (a routine resident action, not an access decision).
- The `M4DocTypes` surface is the **M3 pattern** verbatim (the
  `M3DocTypes.Configure` method; the `Schema.For<T>()` calls). **No new
  registration mechanism.**
- The `IEventService` → `EventService` registration is added to
  `DependencyInjection.cs` in **this** unit (the `IAnnouncementService`
  → `AnnouncementService` registration to mirror). The `EventService`
  itself is **stub** in this unit (the `ListUpcomingAsync` / `GetAsync` /
  `CreateAsync` / `UpdateAsync` / `RsvpAsync` methods throw
  `NotImplementedException` — U03 / U04 implement them).
