---
id: adr-0291
title: "IMPROVE-CODE-2 C2-03 — the 9 remaining Web→concrete-*Service crossings: the named, defensible exception record (7 static-utility services → C2-06 seam-test floor; NotificationService → the Wolverine concrete binding; PostService → C2-05 god-surface)"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/archive/lanes/improve-code/plan-improve-code-2.md
  - docs/philosophy/in-code.md
  - docs/adr/0286-iuserservice-access-core-split.md
  - docs/adr/0287-projects-module-split.md
supersedes: []
superseded_by: []
updated: 2026-11-05
---

# ADR 0291 — IMPROVE-CODE-2 C2-03: the 9 remaining concrete-*Service crossings

Status: **Proposed** (this ADR is the `adr:` named in the 9
`tools/code/code-baseline.json` entries whose `symbol` is a **concrete**
`*Service` — not an `I*Service`, not a `*Policy`/`*Keys`/`*Kinds` static —
that the C2-03 extraction retired **13 of the original 22**. The gate's *"a
`count` raised above the base without an `adr:` fails"* rule is satisfied by
these named entries; the *retirement* of the remaining 13 is the C2-03 work
this ADR records. The 9 entries are **carried** — not retired — because
each one is a **genuinely warranted** concrete dependency, and minting an
`I*Service` to hide it would be a ceremony interface in the sense the plan's
D5 forbids. The *resolving* work is named per edge.)

> **Abstract:** the named, defensible exception record for the 9
> Web→concrete-`*Service` crossings that survive C2-03's extraction of the
> other 12. The 9 split into three groups, each with a *named* resolving unit
> (not "someday"): (1) the **7 static-utility services** — `AuditPurgeService`
> / `EventReminderService` / `MailAlarmsService` / `MediaLifecycleService` /
> `NotificationFlushService` / `StorageMetricsCaptureService` / `UsagePurgeService` —
> pure `public static class` single-method surfaces called from a single
> Wolverine `*Handler`; (2) `NotificationService` — a 12-method surface the
> Web calls **and** the one service the `Program.cs`
> `AlwaysUseServiceLocationFor<NotificationService>()` Wolverine binding
> (the outbox delivery seam) is written against as a **concrete**; and (3)
> `PostService` — the ~30-method god-surface the C2-05 (the
> still-large-file / 4-shrink scope) targets. Each edge's `adr:` field in the
> baseline is `"0291"`. The C2-03 DoD's *"count < 22"* is met: **21 → 9**
> (12 retired, 9 carried with a named `adr:` — 0 silent).

## Context

C2-03's DoD (the plan's D4): *"The concrete-`*Service` crossings reduced: the
22 `boundary_violations` entries whose `symbol` is a concrete `*Service` (not
`I*Service`) are either **retired** (the crossing now codes against an
interface / named `*Policy` / `*Keys`) or **carried with a named `adr:`**
(genuinely warranted, named per edge). The count is **< 22**; record before →
after."*

The **21** original Web→concrete-`*Service` entries (the count is 21, not 22 —
the plan's D4 said *"22"* but the baseline's actual count was 21; the DoD's
*"count < 22"* is met either way):

| # | Module | Symbol | Count | C2-03 decision |
|---|--------|--------|-------|----------------|
| 1 | Announcements | AnnouncementService | 1 | **Retired** — mint `AnnouncementTranslationPolicy` (the static display pin) |
| 2 | Authorization | AuditPurgeService | 1 | **Carried** (this ADR) → C2-06 |
| 3 | Documents | DocumentFolderService | 1 | **Retired** — mint `IDocumentFolderService` |
| 4 | Documents | DocumentService | 1 | **Retired** — mint `IDocumentService` |
| 5 | Events | EventReminderService | 1 | **Carried** (this ADR) → C2-06 |
| 6 | Media | MediaLifecycleService | 1 | **Carried** (this ADR) → C2-06 |
| 7 | Moderation | ModerationService | 2 | **Retired** — re-point to `IModerationService` |
| 8 | Notifications | MailAlarmsService | 1 | **Carried** (this ADR) → C2-06 |
| 9 | Notifications | NotificationFlushService | 1 | **Carried** (this ADR) → C2-06 |
| 10 | Notifications | NotificationService | 4 | **Carried** (this ADR) → C2-05 / the Wolverine binding |
| 11 | Pages | PageService | 1 | **Retired** — mint `PageTranslationPolicy` |
| 12 | Posts | PostService | 8 | **Carried** (this ADR) → C2-05 |
| 13 | Projects | BoardService | 1 | **Retired** — mint `BoardTranslationPolicy` |
| 14 | Projects | ProjectService | 1 | **Retired** — mint `ProjectTranslationPolicy` |
| 15 | Projects | TodoService | 1 | **Retired** — mint `TodoTranslationPolicy` |
| 16 | Search | SearchService | 2 | **Retired** — extract to `SearchKeys` |
| 17 | Sessions | SessionService | 3 | **Retired** — mint `ISessionService` |
| 18 | SiteContent | SiteContentService | 1 | **Retired** — property name collision (the type was already the interface) |
| 19 | Usage | StorageMetricsCaptureService | 1 | **Carried** (this ADR) → C2-06 |
| 20 | Usage | UsagePurgeService | 1 | **Carried** (this ADR) → C2-06 |
| 21 | UserInfo | DirectoryService | 3 | **Retired** — mint `IDirectoryService` |

12 retired (the 5 Can*Translation statics + 2 Document services + 1 Directory
+ 1 Moderation + 1 Search + 1 Session + 1 SiteContent) and 9 carried (the 7
static-utility services + `NotificationService` + `PostService`). The 9
carried entries are the *named* exception record.

## Decision

The 9 carried entries are **not** retired in C2-03. Each one is a
**genuinely warranted** concrete dependency — minting an `I*Service` for
each would be a ceremony interface in the sense the plan's D5 forbids:

> "Anything that would be a split-for-a-number is **named** instead
> (`adr:`)."

### Group 1 — the 7 static-utility services (→ C2-06)

`AuditPurgeService.PurgeAsync` / `EventReminderService.SendRemindersAsync` /
`MailAlarmsService.EvaluateAsync` / `MediaLifecycleService.SweepAsync` /
`NotificationFlushService.FlushDeferredAsync` /
`StorageMetricsCaptureService.CaptureAndPurgeAsync` / `UsagePurgeService.PurgeAsync`.

Each is a **`public static class` with a single `public static` method**
called from a single Wolverine `*Handler` (the outbox / scheduled-job seam).
The "crossing" is a **one-token static call** — there is no *behavioural*
surface to name an interface against (a `I*Service` with one static method is
pure ceremony — the part-vs-whole test fails: the part *is* the whole).
Minting a `I*PurgeService` / `I*Service` for each would be the exact
*"split-for-a-number"* anti-pattern D5 forbids.

The **named resolving unit is C2-06** (the plan's DoD #7 seam-test floor
target: `MailAlarmsService` + `UsagePurgeService` are named on the seam-test
RED list — the 2 of the 7 that have no unit test). C2-06's DoD will either
(a) extract a *named policy* (e.g. `MailAlarmPolicy` / `UsagePurgePolicy`)
for each of the 7, or (b) name an `adr:` for each if a policy is also
ceremony. C2-03 leaves the 7 **as-is** (the static call is the honest shape).

### Group 2 — `NotificationService` (4 files; → C2-05 / the Wolverine binding)

The 4 crossing files: `LocaleController.cs` / `NotificationsController.cs` /
`PageController.cs` / `Program.cs`.

The 3 controller crossings are DI-injected `NotificationService` fields
(a 12-method surface: `SendAsync` / `SendBatchAsync` / `MarkReadAsync` /
… / `FlushDeferredAsync`). The **4th crossing is `Program.cs:1010`**:

```csharp
opts.CodeGeneration.AlwaysUseServiceLocationFor<Kumunita.Core.Notifications.NotificationService>();
```

That line is the **Wolverine outbox-delivery seam** — the concrete type is
the *target* of the `AlwaysUseServiceLocationFor<T>` attribute, which
Wolverine's code generator uses to wire the **durable** `INotificationEmitter`
implementation to the handler. Re-pointing it to an `INotificationService`
would **change the runtime wiring** (Wolverine resolves the concrete by
type name, not by interface). The 3 controller crossings *could* re-point to
a new `INotificationService` (mint it with the 3 controller methods), but
the `Program.cs` binding **cannot** — and that binding is the *reason*
`NotificationService` is a **concrete** (the ADR 0001 durable-email-handler
decision).

The **named resolving unit is C2-05** (the still-large-file / 4-shrink
scope): C2-05 will either (a) mint an `INotificationService` for the 3
controller crossings (retiring 3 of the 4) and carry the `Program.cs`
binding with its own `adr:` (the Wolverine concrete is the *named* exception),
or (b) carry all 4 with a named `adr:` (the 12-method surface + the Wolverine
binding are both genuinely-warranted concrete dependencies). C2-03 leaves
the 4 **as-is**.

### Group 3 — `PostService` (8 files; → C2-05)

The 8 crossing files: `AttachmentController.cs` / `ContentImageController.cs`
/ `GroupsController.cs` / `HomeController.cs` (2 fields) /
`MyDraftsController.cs` / `PostsController.cs` /
`ReportTargetsController.cs` / `KioskItemCatalog.cs` (2 fields).

`PostService` is the **~30-method god-surface** (the ADR 0006 §4
"CQRS-lite" write-lane service for the `Posts` bounded context). Minting an
`IPostService` with all 30 methods is the *exact* ceremony-interface
anti-pattern D5 forbids — the part-vs-whole test says the *part* (the 30
methods the Web calls) is **not** meaningfully distinct from the *whole*
(the 30 methods + the internal helpers the service composes). The *honest*
resolution is to **shrink the god-surface** (C2-05's scope: extract the
read-path into a `PostQueryService`, the write-path into the existing
`*TranslationPolicy` statics, the media-path into the `Media` context's
service). C2-03 leaves the 8 **as-is**.

## Consequences

- The 9 baseline entries stay (the count is `9 < 22` — the DoD's
  *"count < 22"* is met).- The 9 entries carry `"adr": "0291"` (the gate's *"count raised above the
  base without an `adr:`"* rule is satisfied — the `count` is not *raised*,
  it is *carried* — but the naming is the honest record).
- The **resolving units are named**: C2-05 (the god-surface shrink + the
  Wolverine binding) and C2-06 (the 7 static-utility services' seam-test
  floor). C2-03 does **not** pre-empt either unit's scope.
- The **3 ratchet count bumps** (the new policy files add a crossing file
  for `Roles` / `User` in `Announcements` / `Pages`) are also named
  `"0291"` — the *policy* files are the C2-03 work, and their crossing
  growth is the honest consequence of the extraction.
- The **`controllers_marten_allowlist`** entries for `DocumentController` /
  `DocumentFolderController` stay (the C2-04 allowlist scope) — but their
  *reason text* now says "needs an IDocumentService committed surface",
  which C2-03 lands (the interfaces now exist). C2-04 revisits the reason
  text.

## Anti-patterns this ADR **rejects** (the D5 "ceremony interface" test)

- Minting `IAuditPurgeService` / `IEventReminderService` / … /
  `IUsagePurgeService` for the 7 static-utility services — a one-method
  interface with one static call is pure ceremony.
- Minting `INotificationService` for the `Program.cs:1010`
  `AlwaysUseServiceLocationFor<NotificationService>()` binding — that line
  is the Wolverine outbox-delivery seam; re-pointing it to an interface
  changes the runtime wiring.
- Minting `IPostService` with all 30 methods — the part-vs-whole test fails
  (the part is the whole).

The **named** resolution is C2-05 / C2-06, not C2-03. C2-03 does the 12
retirements (the honest ones) and names the 9 carries (the honest
exceptions).
