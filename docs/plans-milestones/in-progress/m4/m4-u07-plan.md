# U07 — `EventReminderService` (Wolverine-free business logic)

- **Lane:** Events (`M4`)
- **Unit:** U07 (of U00–U12)
- **Kind:** behavior (the §6.4 job business logic — no handler, no
  tests)

## Goal

Create the `EventReminderService` static class (namespace
`Kumunita.Core.Events`) — the **Wolverine-free** business logic for the
`EventReminders` §6.4 scheduled job. Mirror the `AuditPurgeService`
shape verbatim (the static class; the `SendRemindersAsync` method; the
24-hour window; the `Going` RSVP filter; the author-inclusion rule; the
idempotency key `remind:{eventId}:{userId}`). **No handler, no tick
record, no tests** (U08's handler; U09's tests).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Core/Moderation/AuditPurgeService.cs` — the
   Wolverine-free business logic shape to mirror (the static class; the
   `PurgeAsync` method; the `IDocumentStore` parameter; the
   `CancellationToken` parameter).
2. `src/Kumunita.Core/Identity/IMailerStage.cs` — the `IMailerStage`
   interface (the `StageAsync` method; the idempotency key; the
   recipient; the subject; the body).
3. `src/Kumunita.Core/Identity/OutboxEmailStager.cs` — the
   `OutboxEmailStager` implementation (the `StageAsync` method; the
   `OutboxEmail` doc; the `Wolverine.IMessageContext.PublishAsync`
   call).
4. `src/Kumunita.Core/Events/Event.cs` — the POCO (U01's output).
5. `src/Kumunita.Core/Events/EventRsvp.cs` — the RSVP POCO (U01's
   output).

## Deliverables (≤ 2 files)

1. **`src/Kumunita.Core/Events/EventReminderService.cs`** — the
   Wolverine-free business logic (the `AuditPurgeService` shape to
   mirror):
   - `public static async Task<int> SendRemindersAsync(
     IDocumentStore store, IMailerStage mailer, IAuthorizationService
     auth, ILocalizationService loc, DateTimeOptions tz,
     DateTimeOffset now, CancellationToken ct)` — the main method
     (the `AuditPurgeService.PurgeAsync` shape to mirror — the
     `IDocumentStore` + the `IMailerStage` + the
     `IAuthorizationService` + the `ILocalizationService` + the
     `DateTimeOptions` + the `now` + the `ct` parameters).
   - The 24-hour window: `now.AddHours(-24) < e.Start <= now` (the
     "remind the day before" semantics — the event is within the next
     24 hours).
   - The `Going` RSVP filter: `rsvp.Status == EventRsvpStatus.Going`
     (the `EventRsvp.Status = Going` rows are the recipients).
   - The author-inclusion rule: the author is reminded even if they
     did not RSVP (the `Event.AuthorId` is always included in the
     recipients).
   - The idempotency key: `remind:{eventId}:{userId}` (the §6.2
     per-email key scheme — the `IMailerStage.StageAsync` idempotency
     key).
   - The `IMailerStage.StageAsync` call: `mailer.StageAsync(session,
     "remind:{eventId}:{userId}", recipient, subject, body, ct)`
     (the `OutboxEmailStager.StageAsync` shape — the `OutboxEmail` doc
     + the `Wolverine.IMessageContext.PublishAsync` call in the same
     transaction — the C3 guarantee).
   - The subject + body: the `ILocalizationService` + the
     `EffectiveTimezoneResolver` + the `EffectiveDateFormatResolver`
     (the ADR 0019 / 0020 timezone / format resolvers) — the
     "You're reminded: {Event.Title} at {Event.Start} at
     {Event.Location}" shape.
2. **`src/Kumunita.Core/Events/EventReminderService.cs`** — the
   helper methods (the `AuditPurgeService` helper shape to mirror —
   the `GetEventRecipientsAsync` method; the `FormatReminderBodyAsync`
   method).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- The `EventReminderService` static class compiles.
- **No new test** (U09's seam tests are the first M4 tests).
- Handoff note: 6 lines starting `## U07 — EventReminderService` —
  (a) the `SendRemindersAsync` method signature (the 7 parameters:
  `IDocumentStore` + `IMailerStage` + `IAuthorizationService` +
  `ILocalizationService` + `DateTimeOptions` + `DateTimeOffset now` +
  `CancellationToken ct`), (b) the 24-hour window (the
  `now.AddHours(-24) < e.Start <= now` semantics), (c) the `Going`
  RSVP filter (the `EventRsvp.Status = Going` rows), (d) the
  author-inclusion rule (the `Event.AuthorId` always included), (e) the
  idempotency key (`remind:{eventId}:{userId}`), (f) the
  `IMailerStage.StageAsync` call (the `OutboxEmailStager.StageAsync`
  shape).

## Notes / deviations

- The `EventReminderService` is the **`AuditPurgeService` shape to
  mirror** (the static class; the `PurgeAsync` method; the
  `IDocumentStore` parameter; the `CancellationToken` parameter). **No
  new business logic mechanism.**
- The 24-hour window is the **"remind the day before" semantics**
  (the `now.AddHours(-24) < e.Start <= now` shape). **No new window
  mechanism.**
- The `Going` RSVP filter is the **`EventRsvp.Status = Going` rows**
  (the `EventRsvp.Status` enum — U01's output). **No new filter
  mechanism.**
- The author-inclusion rule is the **`Event.AuthorId` always included**
  (the `Event.AuthorId` field — U01's output). **No new inclusion
  mechanism.**
- The idempotency key is the **§6.2 per-email key scheme** (the
  `verify:{userId}:{attempt}` / `setup:{userId}` /
  `invite:{componentId}:{email}` / `remind:{eventId}:{userId}` key
  scheme). **No new key mechanism.**
- The `IMailerStage.StageAsync` call is the **M1 step 7 seam** (the
  `IMailerStage` interface + the `OutboxEmail` doc + the
  `OutboxEmailHandler` durable handler). **No new email mechanism.**
