# U3 — Write lanes (create / edit / publish / move / delete + translation)

- **Lane:** Pages (`PG`)
- **Unit:** U3 (of U0–U07)
- **Kind:** write + audit (the standing-gated mutation surface — still Core,
  no Web yet)

## Goal

Implement the **write surface** on `IPageService` / `PageService` —
`CreateAsync`, `UpdateAsync`, `PublishAsync` (author-only, the ADR 0037 pin),
`MoveAsync` (reparent — cycle-guard + depth-cap + `Slug`-rewrite of the
derived path), `DeleteAsync` (**soft-delete**: `IsDeleted = true`, the ADR
0024 shape), and `AddTranslationAsync` (the ADR 0029 standing, the unique-index
add-only rule). Each stores its `AccessAudit` row **in the caller's
`IDocumentSession`** (C3, ADR 0006) with `TargetKind = "page"` and an `Action`
of `page.create` / `page.update` / `page.publish` / `page.move` / `page.delete`
/ `page.translation.add`. Server-side parse of the body for `ImageIds` /
`AttachmentIds` (the client never sends them).

## Entry reads (≤ 5 files, each < ~300 lines)

1. `docs/design/pages-design.md` §3.7 — the **standing matrix** (create /
   edit / publish / move / delete / translate) + the `AccessVia` column + the
   C3 audit-action names.
2. `src/Kumunita.Core/Announcements/AnnouncementService.cs` — the **write-lane
   template**: the C3 caller-session `AccessAudit` row, the
   `UnauthorizedAccessException`/`KeyNotFoundException` split, the
   server-side standing re-check, the `AccessVia` tag.
3. `docs/adr/0024-author-soft-delete-lane.md` — the **soft-delete** idiom
   (`IsDeleted` flag + the read-lane filter) carried onto `Page.DeleteAsync`.
4. `docs/adr/0037-draft-mode-author-only.md` — the **publish author-only**
   pin (`PublishAsync` is `AuthorId`-only, not GlobalAdmin).
5. `src/Kumunita.Web/Security/ContentImageIds.cs` (or the closest RC/ATT
   `ImageIds`/`AttachmentIds` server-side parse) — the **body parse** the
   write lane must reuse (the client never sends `ImageIds`/`AttachmentIds`).

## Deliverables (closed set)

1. **`CreateAsync(actor, …)`** — validate `CheckCreateStanding` (U02's
   helper), persist the `Page`, store the `page.create` `AccessAudit` row in
   the caller's session (C3), return the new `Page`.
2. **`UpdateAsync(actor, pageId, …)`** — validate `CheckEditStanding`,
   mutate body/audience/hierarchy, store `page.update`.
3. **`PublishAsync(actor, pageId)`** — **author-only** (the ADR 0037 pin):
   `IsDraft` → `false`, `Modified` set; `UnauthorizedAccessException` if the
   actor is not `AuthorId`; store `page.publish`.
4. **`MoveAsync(actor, pageId, newParentId, newSlug)`** — validate
   `CheckEditStanding` (move is admin/moderator-scoped, **not** a plain
   author per §3.7), apply the **cycle-guard** (the new parent is not a
   descendant of the page) + the **depth-cap** (chain ≤ 8), store
   `page.move`. A `Slug` change rewrites the *derived* path of the subtree
   (paths are not stored — a single-column write).
5. **`DeleteAsync(actor, pageId)`** — **soft-delete**: set `IsDeleted = true`
   (do **not** remove the row or orphan children), validate standing per §3.7,
   store `page.delete`. The U02 read-lane filter hides it from
   `GetTreeAsync` / `GetByPathAsync`.
6. **`AddTranslationAsync(actor, pageId, translation)`** — validate
   `CheckTranslateStanding`, rely on the **`(PageId, LanguageCode)` unique
   index** (U01) for the add-only duplicate rejection, store
   `page.translation.add`.
7. **Server-side body parse** — `ImageIds` / `AttachmentIds` derived from the
   `Body` in the write lane (the RC U04/U05 + ATT U5 idiom); the Web composer
   never posts them.
8. **`PageServiceTests` additions** (`Kumunita.Core.Tests`): the create/edit
   standing matrix, the publish author-only pin (author ✓, GlobalAdmin ✗ on
   publish, moderator ✗), the move cycle-guard + depth-cap, the soft-delete
   filter (a deleted page is absent from `GetTreeAsync` / `GetByPathAsync`),
   the translation add-only + unique-index duplicate rejection, and the
   audit-row `Action`/`TargetKind` shape asserted on every lane.

## Exit

- `dotnet build Kumunita.slnx -c Debug` green.
- `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
  green — **every** write lane re-checks standing server-side (the C3
  single-source pin: a Web `[Authorize]` is **not** the source of truth), and
  the audit-row `Action`/`TargetKind` shape is asserted in tests.
- **No Web code yet** — `PageController` is U04. `LocalizedPage` still
  untouched.
- Append a `## U3 — write lanes + C3 audit` section to
  `pages-handoff-notes.md`.

## Notes / deviations

- **`PublishAsync` is author-only** (ADR 0037). A GlobalAdmin cannot publish
  someone else's draft — that's the pin. If a test is tempted to assert
  "GlobalAdmin can publish," cut it (or record a `## U3 — Drift pause`
  naming it as a **new ADR**).
- **`MoveAsync` / `DeleteAsync` are admin/moderator-scoped, not author** (§3.7
  — a page is platform content, not a personal note). The `AccessVia` tag is
  `Admin` / `Moderator`, never `Owner`, for these two.
- **Soft-delete, not hard-delete.** `DeleteAsync` sets the flag and nothing
  else. A hard-delete lane (like announcements, ADR 0017) is a **future lane**,
  not this unit.
- The C3 audit row is written **in the caller's `IDocumentSession`** — not a
  separate session, not a Wolverine side effect (email is the only Wolverine
  lane; C3 audit is synchronous in-transaction).
