# U08 — `EventReminderHandler` + `EventReminderTick` (§6.4 job wiring)

- **Lane:** Events (`M4`)
- **Unit:** U08 (of U00–U12)
- **Kind:** structure (the §6.4 job handler + tick — no tests)

## Goal

Create the `EventReminderHandler` static class + the `EventReminderTick`
record (namespace `Kumunita.Web.SideEffects`) — the **§6.4 scheduled
job** wiring. Mirror the `AuditPurgeHandler` + `AuditPurgeTick` shape
verbatim (the `TimeoutMessage(TimeSpan.FromDays(1))` base; the
self-rescheduling `Handle` method; the thin adapter over the
Wolverine-free `EventReminderService`). **No tests** (U09's seam tests
cover the handler).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `src/Kumunita.Web/SideEffects/AuditPurgeHandler.cs` — the handler
   shape to mirror (the `AuditPurgeTick` record; the
   `TimeoutMessage(TimeSpan.FromDays(1))` base; the `Handle` method;
   the self-rescheduling `return new[] { new AuditPurgeTick() };`).
2. `src/Kumunita.Web/SideEffects/OutboxEmailHandler.cs` — the durable
   handler shape (the `Handle` method; the `IMailerStage` dependency;
   the `IDocumentSession` parameter).
3. `src/Kumunita.Web/Program.cs` — the `UseWolverine` +
   `IntegrateWithMarten` wiring (the `AuditPurgeHandler` registration
   to mirror).
4. `src/Kumunita.Core/Events/EventReminderService.cs` — the Wolverine-
   free business logic (U07's output).
5. `docs/design/m4-events-design.md` §3.6 — the `EventReminders` §6.4
   job shape (the primary source for this unit).

## Deliverables (≤ 3 files)

1. **`src/Kumunita.Web/SideEffects/EventReminderTick.cs`** — the tick
   record (the `AuditPurgeTick` shape to mirror):
   `public sealed record EventReminderTick() :
   Wolverine.TimeoutMessage(TimeSpan.FromDays(1));`
2. **`src/Kumunita.Web/SideEffects/EventReminderHandler.cs`** — the
   handler (the `AuditPurgeHandler` shape to mirror):
   - `public static async Task<IEnumerable<object>> Handle(
     EventReminderTick tick, IDocumentStore store, IMailerStage mailer,
     IAuthorizationService auth, ILocalizationService loc,
     IOptions<TimezoneOptions> tzOptions, IOptions<DateFormatOptions>
     dfOptions, CancellationToken ct)` — the main method (the
     `AuditPurgeHandler.Handle` shape to mirror — the `tick` + the
     `store` + the `mailer` + the `auth` + the `loc` + the `tzOptions`
     + the `dfOptions` + the `ct` parameters).
   - The `EventReminderService.SendRemindersAsync` call:
     `var count = await EventReminderService.SendRemindersAsync(store,
     mailer, auth, loc, tzOptions.Value, dfOptions.Value,
     DateTimeOffset.UtcNow, ct);` (the U07 business logic call).
   - The self-rescheduling: `return new[] { new EventReminderTick() };`
     (the `AuditPurgeHandler` self-rescheduling shape verbatim).
   - The `Wolverine.Logging` call: `Wolverine.Logging.Info(
     $"Sent {count} event reminders");` (the `AuditPurgeHandler`
     logging shape to mirror).
3. **`src/Kumunita.Web/Program.cs`** — the `UseWolverine` wiring (the
   `AuditPurgeHandler` registration to mirror — the
   `options.UseMessage<Tick, Handler>()` call for the
   `EventReminderTick` + `EventReminderHandler`).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- The `EventReminderTick` record + the `EventReminderHandler` static
  class + the `Program.cs` wiring compile.
- **No new test** (U09's seam tests cover the handler).
- Handoff note: 5 lines starting `## U08 — EventReminderHandler +
  Tick` — (a) the `EventReminderTick` record (the
  `TimeoutMessage(TimeSpan.FromDays(1))` base), (b) the
  `EventReminderHandler.Handle` method signature (the 8 parameters),
  (c) the `EventReminderService.SendRemindersAsync` call (the U07
  business logic), (d) the self-rescheduling shape (the
  `return new[] { new EventReminderTick() };` line), (e) the
  `Program.cs` `UseWolverine` wiring (the `AuditPurgeHandler`
  registration to mirror).

## Notes / deviations

- The `EventReminderTick` record is the **`AuditPurgeTick` shape to
  mirror** (the `TimeoutMessage(TimeSpan.FromDays(1))` base). **No new
  tick mechanism.**
- The `EventReminderHandler.Handle` method is the **`AuditPurgeHandler.
  Handle` shape to mirror** (the `tick` + the `store` + the `mailer` +
  the `auth` + the `loc` + the `tzOptions` + the `dfOptions` + the `ct`
  parameters; the self-rescheduling `return new[] { new
  EventReminderTick() };` line). **No new handler mechanism.**
- The `EventReminderService.SendRemindersAsync` call is the **U07
  business logic** (the Wolverine-free static class). **No new business
  logic mechanism.**
- The `Program.cs` `UseWolverine` wiring is the **`AuditPurgeHandler`
  registration to mirror** (the `options.UseMessage<Tick, Handler>()`
  call). **No new wiring mechanism.**
