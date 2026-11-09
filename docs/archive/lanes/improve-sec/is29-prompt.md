---
id: is29-prompt
title: "IS29 prompt — Resident transparency entry (D10)"
tier: living
kind: prompt
status: current
audiences: [developer, agent]
lane: improve-sec
---

# IS29 — Resident transparency entry (D10)

## What you are doing

You are the **twenty-ninth working agent** on the **IMPROVE-SEC** lane
(plan: `docs/plans-milestones/plan-improve-sec.md`). IS00–IS28 are committed.

ADR 0299 §4 (D10, owner decision, 2026-11-06) is the spec: a **full**
instance export (the `Full` scope, IS28's gated path) must leave a
**transparency entry that residents can see**. The chosen mechanism (D10
default, confirmed by the owner): an `INotificationEmitter` notice to
**every non-guest resident**, kind `instance.full-export`, stating the date
and that the operator authorised it, **no actor name**. The existing
resident inbox is the surface; no new UI.

## The one thing to get right

**Only `Full` notifies; `ContentFree` does not.** The notification is
emitted **inside** `PortabilityService.ExportAsync(actorId, scope, ct)`,
after the audit row and before returning the stream — **only when
`scope == PortabilityExportScope.Full`**. A content-free export emits
**zero** notifications. The notification body must not carry the actor's
name or id (D10: "stating the date and that the operator authorised it,
with no actor name").

**Guests are excluded.** "Non-guest resident" = a row whose
`Profile.IsGuest` is `false` (or a row that has no `Profile` doc at all,
which defaults to non-guest per the M3 `PrincipalsExport` pattern —
treat absent as `false`). The service already reads all `Profile` docs
inside `PortabilityExportDocuments` / `PrincipalsExport`; the notification
loop can reuse the same `identityPrincipals.GetUsersAsync` + `Profile`
query the export already performs (one read, not two).

## Constraint that keeps existing tests green

`tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs` (3 tests, IS28)
constructs the controller with `new AdminPortabilityController(
portability, localization, null, store)` — the controller is unaffected
by IS29 (the notification lives in the service, not the controller).

`tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs` (13 tests, IS27b)
constructs `PortabilityService` directly. IS29 adds
`INotificationEmitter` to the **last** position of the `PortabilityService`
ctor with a **`= null`** default (the same pattern IS28 used for
`IDocumentStore?` on the controller), so the 7-arg construction in
`BootFullInstanceAsync` keeps compiling. The round-trip tests use `Full`
scope but their `INotificationEmitter` will be `null` (the test floor);
IS29 must **guard** the notification loop with
`if (notificationEmitter is not null && scope == Full)`, so the `null`
floor simply skips the loop and the round-trip tests stay green.

`tests/Kumunita.Core.Tests/PortabilityExportScopeTests.cs` (4 tests, IS27)
same story — 7-arg construction, `Full` scope, `null` emitter → loop
skipped.

## Deliverables (≤ 4 files)

1. **`src/Kumunita.Core/Notifications/NotificationKinds.cs`** — append one
   new constant: `public const string InstanceFullExport = "instance.full-export";`
   (the same `public const string` pattern as the ~18 existing kinds; a
   1–2 line XML doc comment referencing ADR 0299 §4 / D10 is appropriate).

2. **`src/Kumunita.Core/Portability/PortabilityService.cs`** — in the
   `PortabilityService` class, add `INotificationEmitter? notificationEmitter
   = null` as the **last** ctor parameter (DI resolves it in production;
   `null` in the existing test floor). In the `ExportAsync(actorId, scope,
   ct)` body, **after** the audit row is saved and **before**
   `KumunitaArchive.WriteAsync`, add the notification loop:

   ```csharp
   // IS29 (ADR 0299 §4, D10) — the transparency entry: every non-guest
   // resident receives a notification (kind instance.full-export, no actor
   // name, the date + operator authorisation). Content-free emits none.
   // The `EmitAsync` body is intentionally null — the kind label
   // (notifications.kind.instance.full-export in en/de/fr/da) carries the
   // message; D10 forbids an actor name / id in the body.
   if (scope == PortabilityExportScope.Full && notificationEmitter is not null)
   {
       await using var notifySession = documentStore.QuerySession();
       var profiles = (await notifySession.Query<Profile>().ToListAsync(ct))
           .ToDictionary(p => p.SubjectId, StringComparer.Ordinal);
       var allUsers = await identityPrincipals.GetUsersAsync(ct);
       var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
       foreach (var user in allUsers)
       {
           profiles.TryGetValue(user.Id, out var profile);
           if (profile?.IsGuest == true)
               continue; // D10 — non-guest residents only
           await notificationEmitter.EmitAsync(
               notifySession,
               user.Id,
               NotificationKinds.InstanceFullExport,
               $"instance.full-export-{stamp}",   // idempotency key: one export, one stamp
               body: null,
               ct: ct);
       }
   }
   ```

   Add `using Kumunita.Core.Notifications;` and `using Kumunita.Core.UserInfo;`
   if not already present. **Do not widen `IAuthorizationService`** (ADR 0006).

   **The code sample is a starting shape, not a pinned contract.** The
   IS29 agent may adjust the idempotency-key format, the session shape, or
   the user-list source as long as (a) the notification is emitted for
   exactly the non-guest residents, (b) the kind is
   `NotificationKinds.InstanceFullExport`, (c) the body carries no actor
   name / id, and (d) `ContentFree` emits zero notifications. The D10
   invariants are what the tests pin — not the implementation shape.

3. **`src/Kumunita.Core/Portability/DependencyInjection.cs`** — add
   `sp.GetRequiredService<INotificationEmitter>()` as the **last** argument
   to the `new PortabilityService(...)` factory call (one line).

4. **`tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs`** (append) —
   the two new tests:
   - `FullExport_NotifiesEveryResident` — boot the real service (reuse the
     existing `BootRealServiceAsync` helper) with a real
     `INotificationEmitter` (an NSubstitute or a test double that records
     the calls); seed 2 non-guest users + 1 guest user in the store; call
     `ExportAsync(actor, Full)`; assert the emitter received exactly 2 calls
     (the 2 non-guests) with kind `instance.full-export` and that the guest
     was NOT in the call set.
   - `ContentFreeExport_NotifiesNoOne` — same boot, but call
     `ExportAsync(actor, ContentFree)`; assert the emitter received **zero**
     calls (`DidNotReceive()`).

   **PII rule:** the two users and the guest use the repo's standard
   fixture style (`user-a`, `a@example.test`); no real addresses.

## Translation key (the 4th language gate)

The notification kind needs a `notifications.kind.instance.full-export`
key in all four language files (`en/de/fr/da` `notifications.json`) — the
`KnownTranslationKeys_ParityTests` gate forces it. A short label per
language:
- en: "Full instance export authorised"
- de: "Vollständiger Instanz-Export freigegeben"
- fr: "Export complet de l'instance autorisé"
- da: "Fuld eksport af instansen godkendt"

This is a **5th file** — if the ≤ 4-file deliverable cap is reached, split
the four JSON edits into **IS29a** (the code: files 1–4 above) and
**IS29b** (the translation keys only, tests-only follow-up), mirroring the
IS27/IS27b pattern. The plan allows letter suffixes.

## Entry reads (only these — no whole-file reads)

- `src/Kumunita.Core/Notifications/INotificationEmitter.cs` (whole file,
  under 60 lines — the `EmitAsync` signatures).
- `sed -n '10,160p' src/Kumunita.Core/Notifications/NotificationKinds.cs`
  (the existing `public const string` pattern + the 18 kinds; append
  after the last one).
- `sed -n '118,220p' src/Kumunita.Core/Portability/PortabilityService.cs`
  (the ctor + the `ExportAsync` body, where the loop goes in).
- `sed -n '25,45p' src/Kumunita.Core/Portability/DependencyInjection.cs`
  (the `AddTransient<IPortabilityService>` factory — the one line to add).
- `tests/Kumunita.Web.Tests/AdminPortabilityScopeTests.cs` (the
  `BootRealServiceAsync` helper + test 3 — the pattern to extend for
  `FullExport_NotifiesEveryResident`).
- `grep -n "IsGuest" src/Kumunita.Core/UserInfo/Profile.cs | head -5`
  (confirm the property name and type).
- `sed -n '1,30p' src/Kumunita.Core/Localization/Keys/en/notifications.json`
  (the `notifications.kind.*` key format to match).
- The SEC protocol: `docs/plans-milestones/plan-improve-sec.md` per-unit
  contract (D8).
- Test-runner form: `docs/archive/lanes/sec/sec-handoff-notes.md` (SEC U3 —
  xunit.v3 in-process `-class`, not `dotnet test --filter`).

## Tests (2 new, in `AdminPortabilityScopeTests`)

- `FullExport_NotifiesEveryResident` — 2 non-guest + 1 guest → exactly 2
  emission calls, both with `NotificationKinds.InstanceFullExport`.
- `ContentFreeExport_NotifiesNoOne` — zero emission calls.

## Verification

- [ ] Build green (`dotnet build`).
- [ ] `AdminPortabilityScopeTests` green (5/5: 3 from IS28 + 2 new) via the
      xunit.v3 runner form.
- [ ] `PortabilityRoundTripTests` **still** green (13/13) — the `null`
      emitter floor must not break the export path.
- [ ] `PortabilityExportScopeTests` **still** green (4/4) — same reason.
- [ ] `KnownTranslationKeys_ParityTests` **still** green (the new
      `notifications.kind.instance.full-export` key present in all 4
      languages) — if the key registration is IS29b, skip this check here
      and run it in IS29b.
- [ ] No PII in code, tests or notes.
- [ ] One `## IS29` section appended to
      `docs/plans-milestones/in-progress/improve-sec-handoff-notes.md`.
- [ ] One commit (or IS29a + IS29b, two commits if split).

## Definition of done (IS29)

- [ ] `ExportAsync(actor, Full)` with a non-null emitter → 1
      `Notification` per non-guest resident, kind
      `instance.full-export`, body contains no actor name or id.
- [ ] `ExportAsync(actor, ContentFree)` → zero `Notification` rows emitted.
- [ ] `NotificationKinds.InstanceFullExport` exists.
- [ ] The `notifications.kind.instance.full-export` translation key exists
      in en/de/fr/da (or is deferred to IS29b with a note in the handoff).
- [ ] `PortabilityService` ctor: `INotificationEmitter? notificationEmitter
      = null` is the **last** parameter (existing 7-arg tests keep
      compiling).
- [ ] `PortabilityRoundTripTests` 13/13; `PortabilityExportScopeTests`
      4/4; build green; no PII.
- [ ] One commit (or IS29a + IS29b), one handoff section.
- [ ] **Next unit: IS30** (Export docs) — an OPS break-glass runbook section
      ("full instance export"); a note in the M11 design doc linking ADR
      0299. The control-map row is **IS39's** (it lands in the new format
      there).
