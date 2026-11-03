# C03 — A contract for Notifications

## What you are doing

You are the **fourth** agent on the **IMPROVE-CODE** lane (plan: `docs/plans-milestones/plan-improve-code.md`). Your unit is **C03**, the fourth of 13. It is a **contract-extraction** unit: `NotificationService` is a public concrete class with no interface, and the ~10 Core modules plus the Web side-effects all reach into the concrete type. You extract the *emission* seam into a named interface so cross-module callers depend on a contract, and you retire the boundary edges the gate lists against the concrete class.

**You are NOT** doing C00 (shipped — the gate and baseline exist), C01 (shipped — ADRs 0286–0289), or C02 (shipped — the data move). You are **NOT** doing C04–C13. You write **no new feature, no route or rendered-HTML change, no document-schema change.** You do not touch the `IUserInfoService` / `IProjectService` splits (that is C07/C08 against ADRs 0286/0287).

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, clean tree at `196a3419` (C02 just landed).
- Commit style: `feat(improve-code): C03 — <short description>` (conventional commits, em dash).
- Gates you must keep green: `tools/code/code-check.ps1` (the ratchet) and `tools/docs/improve-check.ps1` (docs gates). Both are green now.
- The C00 gate already enforces the D6 module-boundary rule: `boundary_violations` in `tools/code/code-baseline.json` lists **11 concrete edges to `Notifications.NotificationService`** (Announcements, Events, GroupMessaging, Identity, Messaging, Moderation, Pages, Posts, Projects, UserInfo → Notifications, plus Web → Notifications). Clearing them **retires** those baseline entries.

## Entry reads

- The plan's **C03** section and **D6** (module-boundary enforcement), and ADR **0006** (§D, the access/contract seam).
- `src/Kumunita.Core/Notifications/NotificationService.cs` (813 lines). Public surface: `EmitAsync` (two overloads), `ListInboxAsync`, `CountUnreadAsync`, `MarkAllReadAsync`, `MarkReadAsync`, `MarkUnreadAsync`, `GetPreferencesAsync`, `SetPreferencesAsync`, `GetQuietScheduleAsync`, `SetQuietScheduleAsync`, `GetSubscriptionsAsync`, `SetSubscriptionAsync`, `IsSubscriptionEnabledForAsync`, the `InboxCap` const, and the static `DeferredKey(kind, sourceId)`.
- The ~10 Core caller modules' ctor + DI lines; `src/Kumunita.Core/DependencyInjection.cs` (the `sp.GetRequiredService<Notifications.NotificationService>()` registrations); the Web consumers: `Controllers/NotificationsController.cs`, `LocaleController.cs`, `PageController.cs`, `SideEffects/EventReminderHandler.cs`, `SideEffects/NotificationFlushHandler.cs`.
- `NotificationFlushService` / `NotificationFlushHandler` + their tests — the **`DeferredKey` consumers** you must also update.

## Do

- Introduce **`INotificationEmitter`** holding the *emission* contract only — `EmitAsync` (both overloads) and whatever the cross-module callers actually resolve. The inbox / preferences / quiet-schedule / subscription read-write surface is the **Notifications module's own** API (consumed by the owner `NotificationsController`) — **not** a cross-module seam. Leave it on the concrete class; do **not** copy all 15 methods onto the interface.
- Move the static `DeferredKey` into a named **`NotificationKeys`** static (the D6 allowlist explicitly permits `*Keys` statics); update its consumers (`NotificationFlushService`, `NotificationFlushHandler`, and their tests).
- Have the concrete class implement the interface; register `INotificationEmitter` in DI. Switch the ~10 Core modules + the Web side-effects that *emit* to the interface. `NotificationService` keeps serving the owner controller's full surface.
- A unit test with a fake emitter proves one caller (e.g. Posts) emits the expected kind without Postgres.

## Do NOT do

- Do NOT rename or remove any existing public member the owner surface relies on — add the interface, don't rip the class apart.
- Do NOT split `NotificationService` into sub-services (that would be a C04/C10-shaped move, not C03). Keep the class; only introduce the seam.
- Do NOT change behaviour: the audit rows, the `Via` values, the quiet-schedule / suppression logic all stay byte-identical. No new ADR is required for C03 — it builds against the plan's C03 section and the D6 rule the gate already enforces.
- Do NOT commit a baseline `max` / `count` higher than today — the ratchet is shrink-only.

## Exit criteria (all must pass before you commit)

1. **Zero** boundary edges to concrete `Notifications.NotificationService` in the `code-check` boundary report (the 11 entries are **retired, not lowered**).
2. Every cross-module *emit* call site resolves `INotificationEmitter`, not the concrete class. `grep` the ~10 Core modules + the Web side-effects for `NotificationService`; the remaining hits must be the owner controller, `Program.cs` composition, and DI — not cross-module call sites.
3. `DeferredKey` lives on `NotificationKeys`; its consumer files (`NotificationFlushService`, `NotificationFlushHandler`, and their tests) compile and pass unmodified.
4. `pwsh tools/code/code-check.ps1` → exit 0 (stale-edge rule satisfied by the retire).
5. `pwsh tools/docs/improve-check.ps1` → exit 0.
6. `dotnet build` clean; the full `Kumunita.Core.Tests` + `Kumunita.Web.Tests` notification suites pass unmodified (`NotificationServiceTests`, `NotificationFlushServiceTests`, `NotificationFlushHandlerTests`, `F12U3_AlertRoutedToChannelTests`, `F17U3_SuppressionGateTests`, …).

## Practical notes

- **The retirement rule is a hard stop.** The moment the last cross-module edge is gone, delete its `boundary_violations` entry in the same commit — or the ratchet's stale-edge check (C00 rule 3) fails. Edit `code-baseline.json` **textually** (the file was generated with `\u003C`/`\u003E` escapes) — a `json.load`/`dump` round-trip churns every escape site and muddies the diff.
- `NotificationService` is in Core; the Web controllers are a **separate assembly**, so it cannot become `internal`. "Module-internal in intent" = cross-module *Core* callers go through the interface; the owner controller and the Web keep the concrete public surface.
- The plan says "9 modules" (measured pre-C01); today it's **~10 Core + Web**. Trust the current boundary report over the plan's count.
- Commit as one coherent unit (the interface + the `DeferredKey` move + the DI + the baseline retirements share a test file and the baseline). Commit with: `feat(improve-code): C03 — a contract for Notifications: INotificationEmitter + NotificationKeys`.
