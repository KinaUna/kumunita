# IP — Integration Polish: rolling handoff notes

> Every IP unit appends a `## U##` section here before it moves its own
> plan to `done/`. The file is created by U00 and moved to `done/` at U07.

## U00 — Docs parity + decision lock

**Delivered:**
- Corrected `ARCHITECTURE.md` ADR range — dropped the hardcoded `0001–0053`
  in favour of "0001 onward (the running decision ledger — append-only, highest
  number = newest)" so the line never needs a manual update.
- Extended the value-chain table in `ARCHITECTURE.md` to M18–M23 (5 new rows:
  M18 recurring events / M19 guest accounts / M20 notification quiet times /
  M21 document management / M23 extended profiles). M22 is in-progress and does
  not get a row.
- Added a one-line rationale comment to `Milestones.cs` before the M23 entry
  explaining why M23 ships before M22 (M23 was pulled forward and completed
  while M22 remained `StatusPlanned` / deferred).
- Locked [PROPOSED] D1–D6 verbatim. No amendments — the six decisions
  (D1 `AccessAuditFactory` pure static helper; D2 `StandingMatrix` pure
  static helper; D3 rate-limiting on message send + post/reply create;
  D4 log-and-degrade in `catch {}` blocks; D5 unit tests for
  `BlockedAccountMiddleware` + `PrivilegedStampMiddleware`;
  D6 localized `?error=` codes in the Login view) are all confirmed as
  consistent with the codebase's current state.

**Open questions:** none.

**Next unit:** U01 — `AccessAuditFactory` + the three `StoreAuditRow` call
sites. Entry: `src/Kumunita.Core/Authorization/AccessAudit.cs`,
`src/Kumunita.Core/Events/EventService.cs` (StoreAuditRow, ~line 1697),
`src/Kumunita.Core/Inventory/InventoryService.cs` (StoreAuditRow, line 507),
`src/Kumunita.Core/Projects/ProjectService.cs` (StoreAuditRow, ~line 3498).

## U01 — `AccessAuditFactory` + the three `StoreAuditRow` deletions

**Delivered:**
- New `src/Kumunita.Core/Authorization/AccessAuditFactory.cs` — a pure
  static class with `SingleTarget(actorId, action, targetKind, targetId, via,
  outcome = Allow)`: sets all 9 `AccessAudit` fields, `EffectivePrincipalId =
  actorId`, `VisibleCount`/`HiddenCount` = null (single-target shape).
- Deleted the private `StoreAuditRow` from `EventService`, `InventoryService`,
  and `ProjectService`; all **57** call sites (8 + 9 + 40) now call
  `session.Store(AccessAuditFactory.SingleTarget(...))` — the "caller's
  session" idiom preserved. `InventoryService.AuditVia` (role-based) left in
  place; the `*AuditViaFor` mappers untouched (U02).
- New `tests/Kumunita.Core.Tests/AccessAuditFactoryTests.cs` (2 pin tests).
- **Exit:** `dotnet build` clean; `Kumunita.Core.Tests` 1088/1088 green;
  `private static void StoreAuditRow` grep empty across `Kumunita.Core`.

**Open questions:** none. **Note:** actual call-site count is 57 (the
register estimated ~62, projecting ~45 Project) — codebase wins for mechanics.

**Next unit:** U02 — `StandingMatrix` + the four `*AuditViaFor` mappers.

## U02 — `StandingMatrix` + the five author-based mapper deletions

**Delivered:**
- New `src/Kumunita.Core/Authorization/StandingMatrix.cs` — a pure static
  class with `AuditVia(actorId, ownerId)`:
  `string.Equals(ownerId, actorId, Ordinal) ? Owner : Admin`. Zero new
  authorization surface (C-IP·2): no new `AccessVia` value, no DI registration.
- Deleted the five author-based mappers: `AuditViaFor` (EventService) +
  `TodoAuditViaFor` / `BoardAuditViaFor` / `GoalAuditViaFor` /
  `ProjectAuditViaFor` (ProjectService). All five bodies were the same
  `string.Equals(resource.AuthorId, actorId, Ordinal) ? Owner : Admin` — the
  register's "owner property name differs per resource" line was a stale
  estimate; the codebase wins (all `.AuthorId`).
- Replaced all **26** call sites (2 EventService + 24 ProjectService) with
  `StandingMatrix.AuditVia(actorId, <resource>.AuthorId)`; updated 8 stale
  `<see cref="…"/>` references (7 ProjectService + 1 EventService) to point at
  `StandingMatrix.AuditVia`.
- `InventoryService.AuditVia(IReadOnlySet<string>)` (role-based, a different
  pattern) left in place, as the register requires.
- New `tests/Kumunita.Core.Tests/StandingMatrixTests.cs` (3 pin tests: owner →
  Owner, non-owner → Admin, ordinal case-sensitive).
- **Exit:** `dotnet build Kumunita.slnx -c Debug` clean (0 warnings);
  `Kumunita.Core.Tests` 1091/1091 green (U01's 1088 + 3 new); `AuditViaFor`
  grep empty in `EventService.cs` + `ProjectService.cs`.

**Open questions:** none. **Note:** actual call-site count is 26 (the register
estimated ~22) — codebase wins for mechanics.

**Next unit:** U03 — the two rate-limit policies (`message` + `write`) + the
`[EnableRateLimiting]` attributes in `Kumunita.Web`.

## U03 — Rate-limit policies for message send + post/reply create

**Delivered:** `Program.cs` `AddRateLimiter` block gains two fixed-window
policies (the existing five unchanged): `message` 20/15min, `write` 30/15min.
`[EnableRateLimiting("message")]` on `MessagesController.Send`;
`[EnableRateLimiting("write")]` on `PostsController.New` + `PostsController.Replies`
(create surfaces only — `Edit`/`EditReply`/translation actions untouched, per D3).
Added the missing `using Microsoft.AspNetCore.RateLimiting;` to
`MessagesController.cs` (the trap the register flagged; `PostsController.cs`
already had it). No new DI registration, no new authorization surface, no Core
touch (C-IP·2). Exit: `dotnet build Kumunita.slnx -c Debug` clean;
`Kumunita.Web.Tests` 721/721 green; grep `EnableRateLimiting("message")`=1,
`EnableRateLimiting("write")`=2. **Open questions:** none. **Next unit:**
U04 — the two `catch {}` log-and-degrade fixes (`HomeController`,
`MessagesController`); inject `ILogger<T>` into each.

## U04 — Log + narrow the two silent `catch {}` blocks (D4, the degraded-but-signed-in seam)

**Delivered:** injected `ILogger<T>` into both controllers (`HomeController`
classic ctor — added as the required first param + `_logger` field;
`MessagesController` primary ctor — added as the first param +
`private readonly` field). Replaced the two bare `catch {}` D4-named blocks —
`HomeController` profile read (`profile = null`) and `MessagesController`
`Thread` actor display-name read (`actorDisplayName = null`) — with
`catch (Exception ex) when (ex is not UnauthorizedAccessException)` that calls
`_logger.LogWarning(ex, …)` before the null degrade. Trap blocks left untouched
(`HomeController` `effLang` catch + outer whole-feed catch). No rethrow, no
`IErrorHandlingService`, no `catch` removal, no Core touch (D4/C-IP·3). Test
ctor call sites in `HomeControllerTests.cs` + `MessagesControllerTests.cs`
updated to pass `NullLogger<T>.Instance`. Exit: `dotnet build Kumunita.slnx
-c Debug` clean; `Kumunita.Web.Tests` 721/721 green; grep `LogWarning`=1 in
each controller, `when (ex is not UnauthorizedAccessException)`=1 each.
**Open questions:** none. **Next unit:** U05 — the two middleware unit-test
files (`BlockedAccountMiddlewareTests` + `PrivilegedStampMiddlewareTests`) in
`tests/Kumunita.Web.Tests/`.

## U05 — Unit tests for `BlockedAccountMiddleware` + `PrivilegedStampMiddleware` (D5, the two per-request security gates)

**Delivered:** two new test files in `tests/Kumunita.Web.Tests/` —
`BlockedAccountMiddlewareTests.cs` (4 tests: unauthenticated pass-through,
profile-null pass-through, not-blocked pass-through, blocked → sign-out +
redirect to `/Account/Login?error=blocked`) and
`PrivilegedStampMiddlewareTests.cs` (5 tests: unauthenticated pass-through,
no-elevated-role fast-path pass-through, role-set match pass-through,
user-deleted → sign-out + redirect to `?error=account-removed`,
role-set mismatch → sign-out + redirect to `?error=role-changed`). Both
exercise `InvokeAsync` directly with a stubbed `DefaultHttpContext` +
`RequestServices` provider — no Postgres. `IUserInfoService` and
`IUserRoleStore<User>` are NSubstituted; `UserManager<User>` is a real instance
backed by a substituted `IUserRoleStore<User>` (the repo's `GuestClaimMintTests`
idiom); `SignInManager<User>` is proxied via
`Substitute.For<SignInManager<User>>(ctorArgs…)` (the plan's illustrative Moq
code was adapted to the codebase's NSubstitute-only convention). Production
middleware untouched. Exit: `dotnet build Kumunita.slnx -c Debug` clean
(13 pre-existing warnings in untouched files, 0 in the new files);
`Kumunita.Web.Tests` **730/730 green** (U04's 721 + 9 new).
**Open questions:** none. **Next unit:** U06 — the Login view error-code
mapping (D6) + the three `account.login.error.*` `kw-l` keys (en/de/fr/da).

## U06 — Login view `?error=` code→localized message (D6, the cognitive-integration seam)

**Delivered:** the controller passes the `?error=` code through verbatim
(`Error = error`) — the `const string blockedMessage` + the
`errorText` switch are **gone** (grep `blockedMessage`=0, `Error = error`=1).
The Login view gains the D6 "3-row table in the view": an `@switch (Model.Error)`
with one case per known code (`blocked` → `account.login.error.blocked`,
`account-removed` → `account.login.error.removed`, `role-changed` →
`account.login.error.role_changed`) each rendering its `kw-l`-localized
message, and a `default` fallback to `@Model.Error` (forward-compatible). The
three `kw-l` keys are registered in **all four** languages (en/de/fr/da) in
`KnownTranslationKeys.cs` (grep `account.login.error.*`=4 each). New
`tests/Kumunita.Web.Tests/LoginErrorCodeMappingTests.cs` (6 controller-seam
tests: the three codes pass through verbatim + are distinct + no code collapses
to null/empty + an unknown code still surfaces). No new `IErrorMessageService`,
no new `AccessAction`/`TargetKind`, no middleware/redirect change (D6 frozen).
Exit (the **named exception** — BOTH assemblies): `dotnet build Kumunita.slnx -c
Debug` clean (0 warnings in touched files); `Kumunita.Core.Tests` **1091/1091
green** (`KnownTranslationKeys_ParityTests` pin the 3 keys × 4 languages);
`Kumunita.Web.Tests` **736/736 green** (`KwLRegistryConsistencyTests` pin the 3
keys registered + `LoginErrorCodeMappingTests`). **Open questions:** none.
**Next unit:** U07 — the close (move all IP artifacts flat to `done/`).
