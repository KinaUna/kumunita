# U2 — `PageService` read lanes + `PageToAuditableResource` + the standing matrix

- **Lane:** Pages (`PG`)
- **Unit:** U2 (of U0–U07)
- **Kind:** read + authorization (the lane's first code-bearing unit — no Web
  yet)

## Goal

Implement the **read surface** on `IPageService` (`GetByPathAsync`,
`GetBySlugUnderParentAsync`, `GetTreeAsync`, `GetTranslationsAsync`,
`GetByMountPointAsync`), the **`PageToAuditableResource`** adapter (the
`PostToAuditableResource` verbatim), and the **standing-matrix** gate helpers
(`CheckCreateStanding` / `CheckEditStanding` / `CheckTranslateStanding`) —
each throwing `UnauthorizedAccessException` (403) / `KeyNotFoundException`
(404) exactly like `AnnouncementService`. Then the **`PG_*` DB-backed
authorization family** in `Kumunita.Core.Tests`.

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.3 + §3.4 + §3.7 — the hierarchy model
   (derived path, cycle-guard, depth-cap 8), the `null`-audience-public
   default, and the standing matrix.
2. `src/Kumunita.Core/Posts/PostToAuditableResource.cs` — the **adapter** to
   mirror verbatim (`Id`/`Name`/`OwnerId`/`Audience`/**`null` allowed**/
   `ComponentId`/`TargetKind`). The `Page` adapter's only difference is
   `TargetKind = "page"`.
3. `src/Kumunita.Core/Announcements/AnnouncementService.cs` — the **standing
   gate** pattern (the C3 caller-session `AccessAudit` row, the
   `UnauthorizedAccessException`/`KeyNotFoundException` split, the
   server-side re-check) — the read lanes here mirror its gate shape.
4. `src/Kumunita.Core/Authorization/AuthorizationService.cs` — `Decide()`
   branch 5 (`target.Audience is null` ⇒ public) + the `Community` branch
   (ADR 0036). **Read-only** — confirm the `PageToAuditableResource` plugs in
   with **no** signature change (ADR 0006 §A).
5. `tests/Kumunita.Core.Tests/…AnnouncementServiceTests.cs` (or the closest
   `A0036_*` family) — the **`PG_*`** test family shape to mirror (the
   `null`-audience-public, `Community`, grants, cycle-guard, depth-cap,
   `GetByPath`, translation-standing cases).

## Deliverables (closed set)

1. **`PageToAuditableResource`** (`src/Kumunita.Core/Pages/`) — the adapter:
   `Id` = `Page.Id`, `Name` = `Page.Title`, `OwnerId` = `Page.AuthorId`,
   `Audience` = `Page.Audience` (**`null` allowed**), `ComponentId` =
   `Page.ComponentId`, `TargetKind` = `"page"`.
2. **Read lanes** on `IPageService` / `PageService`:
   - `GetByPathAsync(string path)` — resolve the derived path (`a/b/c`) → the
     `Page`; `KeyNotFoundException` on absent.
   - `GetBySlugUnderParentAsync(Guid? parentId, string slug)` — one level of
     the tree (the `(ParentId, Slug)` unique key).
   - `GetTreeAsync()` — the forest (roots + children), **filtering
     `IsDeleted`** (the ADR 0024 soft-delete filter; U03 sets the flag).
   - `GetTranslationsAsync(Guid pageId)` — ordered by `LanguageCode` (the ADR
     0022 read).
   - `GetByMountPointAsync(string slot)` — the mount-point resolver
     (`footer/community`, `help/account`).
3. **Standing-matrix gate helpers** (the §3.7 matrix, server-side):
   - `CheckCreateStanding(actor, page)` — GlobalAdmin ∪ (community
     Moderator, `ComponentId` = a community they moderate).
   - `CheckEditStanding(actor, page)` — `AuthorId` ∪ GlobalAdmin ∪ (a
     Moderator of `page.ComponentId`).
   - `CheckTranslateStanding(actor, page)` — GlobalAdmin ∪ Translator ∪
     (community Moderator, community-scoped — the ADR 0029 lane).
   - Each throws `UnauthorizedAccessException` / `KeyNotFoundException` like
     `AnnouncementService`.
4. **`PG_*` DB-backed tests** (`Kumunita.Core.Tests`, Testcontainers):
   the `null`-audience-public branch, the `Community` branch (the `A0036_*`
   family shape), the grants branch, the cycle-guard + depth-cap (a cycle and
   a >8 chain are rejected), the `GetByPath` resolution, and the translation
   standing (the ADR 0029 matrix — allowed on a community page, denied on a
   flat/public one).

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green with the **`PG_*`** authorization family + the hierarchy/translation
  standing tests.
- The adapter compiles against the **frozen** `IAuthorizationService`
  (**no** signature change — ADR 0006 §A; **no** new `AccessAction`, **no**
  new `AccessVia`, **no** new authorization branch).
- **No Web code yet** — `PageController` is U04.
- Append a `## U2 — read lanes + adapter + standing` section to
  `pages-handoff-notes.md`.

## Notes / deviations

- The adapter is **additive** (a new `IAuditableResource` impl), not a change
  to `Decide()`. If a standing cell in §3.7 can't be expressed with the
  *existing* role claims, that's **drift-pause (a)** — a new ADR, not a silent
  addition. Record a `## U2 — Drift pause` naming the cell.
- The cycle-guard + depth-cap live in the **write lane** (U03's
  `MoveAsync`/`CreateAsync`), not the schema (Marten has no FK). U02's tests
  exercise the *helpers* the write lanes will call, so the guard is testable
  now.
- `GetTreeAsync` **must** filter `IsDeleted` here (not in U03) — the read
  lanes are the single place a deleted page could leak into the browse view,
  so the filter belongs with the read, not the write.
