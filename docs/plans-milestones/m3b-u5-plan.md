# U5 — Assign / unlock / resolve + `Via = Report` read branch — execution plan

- **Unit register line:** `docs/plans-milestones/plan-m3b-moderation.md` → U5 (line 208).
- **Deliverables (≤ 2 files, modify 1):** `src/Kumunita.Core/Moderation/ModerationService.cs` — add four methods to U4's class (the three GlobalAdmin/SoD-gated write lanes + the `Via = Report` read branch). **No new file** (U4's register line created the file; U5's register line says "modify — add the three methods"; since `CanReadWithReportAsync`'s §2.4 item 1 pin places it in the **same** file — a new method on `ModerationService` — U5's deliverable stays the one U4-owned file).
- **Exit:** `run_build` green on `Kumunita.Core` (verified); handoff note appended to `docs/plans-milestones/m3b-handoff-notes.md` as `## U5 — Assign / unlock / resolve + Via=Report read branch` **before** any follow-up action.

## Understanding

U5 completes `ModerationService`. U4 landed the ctor + `FileReportAsync` (F1, C-M3b·1 — the resident-facing intake lane). U5 adds **four** methods (the U5 register line names three — `AssignReportAsync`, `UnlockAsync`, `ResolveReportAsync` — but its Goal also names "land the `Via = Report` read-lane addition U1/U2 pinned", which §2.2.3 pins as `CanReadWithReportAsync` in the same file):

| Method | FACES | Invariant | Gate |
|---|---|---|---|
| `AssignReportAsync` | F5 | C-M3b·4 | GlobalAdmin (SoD) |
| `UnlockAsync` | F6 | C-M3b·4 | GlobalAdmin (SoD); calls M1 `SetComponentModeratorAccessAsync` |
| `ResolveReportAsync` | F6 | C-M3b·4 | GlobalAdmin (SoD); calls M1 `SetComponentModeratorAccessAsync` |
| `CanReadWithReportAsync` | F2 | C-M3b·2 | standalone read lane (ADR 0006-E "compatible lane") |

The §2.4 item 1 pin is **explicit**: the read lane is a **new method on
`ModerationService`** — **not** a branch inside `AuthorizationService.Decide`
(a branch would couple the M1-frozen §A decision algorithm to
`Report` reads, a unit-series rule 4 violation — the M1 frozen seams are
untouched).

## Assumptions

- **§2.2.3 and §2.4 are authoritative** (§2.0 drift-guard: the design
  doc wins; if a later unit finds a mismatch, *this* doc is updated in
  the same commit — the drift note belongs in
  `docs/plans-milestones/m3b-handoff-notes.md`).
- **SoD gate in Core (all three write lanes):** the Core
  `AccessAction.Moderate`-gated `CanAsync` call is the SoD
  discriminator (a standing-moderator vs. the GlobalAdmin caller); the
  **actual** GlobalAdmin-only restriction is a **Web-layer** concern
  (`[Authorize(Roles = GlobalAdmin)]` on U7's
  `ModerationController`, mirroring M1's `AdminController` / `DirectoryController`
  split). The M1 `SetComponentModeratorAccessAsync` precedent holds
  the same split (Core trusts the caller; Web verifies the standing —
  ADR 0003 SoD is a Web-layer assertion *plus* the Core SoD lane's
  "no partial write on a `Decision.Allowed = false` result" property,
  which **is** a U5 Core-side pin).
- **Core SoD shape (identical across all three write lanes):**
  1. `session = await _session...` — the caller's `IDocumentSession`
     parameter (ADR 0006-E compatible lane — same-transaction
     guarantee, C3).
  2. Load `Report`; load `Post` from `report.PostId`.
  3. `decision = await _authz.CanAsync(actorId, AccessAction.Moderate,
     new PostToAuditableResource(post), session)` — the
     `IDocumentSession` overload: **the audit row lands in the
     caller's transaction** (C3 — one `SaveChangesAsync` at the end).
  4. **Denied** (decision.Allowed = false) → still commits the audit row
     (C3 — "Allow **and** Deny" — ADR 0006-C: no silent unaudited
     access), but **no `Report.Status` write, no `ModeratorAssignment`
     write, no `SetComponentModeratorAccessAsync` call, no partial
     state** (the §2.3 item 4 / C-M3b·3 "no partial write" discipline).
  5. **Allowed** → write the domain change (per the specific lane) +
     the `AccessAudit` Allow row + the optional `ModeratorAssignment`
     / `SetComponentModeratorAccessAsync` call in the **caller's**
     session, then **one** `SaveChangesAsync()` at the end of the
     method.
  6. **`SetComponentModeratorAccessAsync` (in `UnlockAsync` /
     `ResolveReportAsync`)** — M1's frozen shape opens **its own**
     session (the M1 seam's own `SaveChangesAsync`, a *separate* commit
     from the caller's); this is the "flag-flip commits separately"
     tension the §2.0 drift-guard applies — the M1 seam is frozen
     (unit-series rule 4 forbids reshaping it). The **report-domain
     and report-audit rows** still commit atomically in the
     caller's transaction (the C3 pin **is** honored).
- **`report.ComponentId` may be null** (U4 sets it from
  `post.ComponentId`, which is nullable on the M3 `Post` POCO). If
  null, `SetComponentModeratorAccessAsync` **cannot** be called (the M1
  seam requires a non-null `componentId`); U5's guard: **skip the
  flag-flip** when `ComponentId` is null (no fabricated default — no
  new rule invented by U5).
- **The `Report` POCO is unchanged** (rule 5 — never reshapes). Each
  of the three write lanes writes the **exact** `Report.Status`
  literal (per §2.3 item 2): `"assigned"` / `"unlocked"` /
  `"resolved"`.
- **The `AccessAudit` `Action` string is NOT pinned by §2.3** —
  §2.3's four numbered items cover the four `Via` tags, the four
  `Status` literals, and the no-partial-write discipline (the
  Action-string vocabulary is Core-convention-level, matching M1's
  `"group.add-member"` / `"delegation.grant"` / `"moderator-access"`
  — U5 picks the three action strings against the same vocabulary,
  matching U4's `"report.file"`).
- **`Action` string pins per lane (U5's choice):**
  - `AssignReportAsync` → `Action = "report.assign"` (the
    resident-facing "assignment" analog of U4's `"report.file"`,
    different verb + different resource — U9's test-6 will anchor).
  - `UnlockAsync` → `Action = "report.unlock"` (the C5 activation
    event; U9's test-7 will anchor).
  - `ResolveReportAsync` → `Action = "report.resolve"` (the resolve
    counterpart; U9's test-8 will anchor).
  - `CanReadWithReportAsync` (the *early deny* case) →
    `Action = "read"` (matching `AuthorizationService.Decide`'s own
    audit row, which uses `action.Id` = `"read"` for
    `AccessAction.Read` — U9's test-4 will anchor).
- **The `TargetKind` on the write-lane audit rows is `"report"`** —
  the audit row's subject-of-action is the `Report` domain row (the
  "what is this audit row auditing?" question). Distinct from U4's
  `"post"` (there the audit row audits the *post* the report is filed
  against); U9's tests will anchor the exact string.
- **The `TargetId` on the write-lane audit rows is the
  `reportId`** (the domain row the action is *writing to* — the
  subject of the audit). U9's tests will anchor.
- **`Via` tag on each write-lane audit row** is taken from the
  `Decision` (whatever standing the M1 frozen §A algorithm resolved
  to) — this keeps the write-lane audit row honest to the *actual*
  decision path (owner / delegation / break-glass / moderation /
  audience / admin). The §2.3 pin for the **filing** `Via`
  tag (`Admin`) does **not** extend to these three lanes — those lanes
  are the *decision* lanes (they call `CanAsync`), so the row
  records the decision's actual `Via`.
- **`EffectivePrincipalId` on each write-lane audit row** is taken
  from the `Decision` (delegated actor → owner; break-glass →
  actor; moderation branch → moderator; otherwise actor). Matches
  the M1 `SetComponentModeratorAccessAsync` precedent
  (`EffectivePrincipalId = actorId` on a plain write lane with no
  "acting under").
- **`CanReadWithReportAsync` shape — the §2.2.3 / §2.4 item 1 pin
  verbatim:**
  - **Standalone method** (no `IDocumentSession` parameter — §2.4
    item 1: "a plain read with no in-flight caller transaction (the
    M3 `PostService.GetPostAsync` precedent)").
  - Opens **its own** session (`await using var session = _store.
    OpenSession(new SessionOptions())` — the M1/M2/M3 convention;
    the §2.0 drift-guard notes that the M1 seam convention is to
    use a *writable* session for the audit-row write — an
    `IQuerySession` cannot `Store` in this Marten version).
  - **C5 unactivated gate** — the §2.4 item 4 "filed report is the
    gate" pin. Queries `Report` rows where `PostId == postId`; if
    there are **none** → the C5 branch is unactivated → U5 writes
    `AccessAudit { ActorId = actorId, EffectivePrincipalId = actorId,
    Action = "read", TargetKind = "post", TargetId = postId,
    Via = AccessVia.Report, Outcome = Deny }` **and commits** in
    its own session (the §2.4 item 3 `AccessVia.Report` pin), and
    returns `new Decision(false, AccessVia.Report, actorId)`.
  - **If a filed report exists** — the C5 branch is activated →
    U5 delegates to the **standalone**
    `IAuthorizationService.CanAsync(string, AccessAction,
    IAuditableResource)` overload (the **own-commit** variant — §2.4
    item 1's "audit row in own commit" shape, the M3
    `PostService.GetPostAsync` precedent). The M1 seam's audit row
    commits in its own transaction; the `Decision` returned is
    the result to the Web layer.
- **`Decision` returned by `CanReadWithReportAsync`** is a **real**
  allow/deny — the §2.2.3 pin: "the Web layer renders 403 on
  `Allowed = false`". When the C5 early-reject gate hits (no filed
  report), the `Decision` is synthesized with `Via = Report`
  (§2.4 item 3 pin) and `Allowed = false`. When the M1 seam's
  `CanAsync` runs, its `Decision` is returned as-is (the `Via` tag
  will be whatever the M1 §A algorithm resolved — the M1 frozen
  seam's own pin).

## Approach

### Files

All four methods are additions to the existing
`src/Kumunita.Core/Moderation/ModerationService.cs`
(U4 already created the file, the ctor, the `using` directives, and
`FileReportAsync`). U5 does **not** reshape the `FileReportAsync`
body (unit-series rule 1: U5's `Deliverables` is "modify — add the
three methods" — plus the Goal's "land the `Via = Report` read-lane
addition" per its Goal line).

- `using` directives: already sufficient (U4 imported
  `Kumunita.Core.Authorization`, `Kumunita.Core.Posts`,
  `Kumunita.Core.UserInfo`, `Marten`). `Marten.Services.SessionOptions` is
  qualified at the call site (same pattern as
  `AuditPurgeService.cs`). No new `using` needed.

### `AssignReportAsync` (F5, C-M3b·4, SoD)

1. Argument guards (`ArgumentException` on empty `reportId` /
   `assignedToModeratorId` / `globalAdminId`;
   `ArgumentNullException.ThrowIfNull` on null `session`) — same shape
   as U4's `FileReportAsync`.
2. `report = await session.LoadAsync<Report>(reportId)` → null →
   `KeyNotFoundException` (no partial write).
3. `post = await session.LoadAsync<Post>(report.PostId)` → null →
   `KeyNotFoundException`.
4. SoD gate: `decision = await _authz.CanAsync(
   globalAdminId, AccessAction.Moderate, new
   PostToAuditableResource(post), session)` (ADR 0006-E compatible
   lane — the audit row lands in the caller's transaction).
5. **If denied**: only the decision's audit row is written (the
   M1 seam wrote it); no `Status` update, no `ModeratorAssignment`
   write; `await session.SaveChangesAsync()` (C3 — the audit row
   commits, no partial state).
6. **If allowed**:
   - `report.Status = "assigned"` (the §2.3 item 2 literal for the
     assign lane);
   - `session.Store(report)`;
   - **If `report.ComponentId` is not null**: upsert the
     `ModeratorAssignment` row for `(assignedToModeratorId,
     report.ComponentId)` — set `GrantedBy = globalAdminId`,
     `At = now` (the SoD audit trail);
     `session.Store(assignment)`;
   - Write `AccessAudit`:
     `{ Id = new guid (N), At = now, ActorId = globalAdminId,
     EffectivePrincipalId = decision.EffectivePrincipalId,
     Action = "report.assign", TargetKind = "report",
     TargetId = reportId, Via = decision.Via,
     Outcome = Allow }`;
     `session.Store(audit)`;
   - `await session.SaveChangesAsync()` (C3 — one commit, atomic).

### `UnlockAsync` (F6, C-M3b·4, C5 activation)

Same shape as `AssignReportAsync`. Differences:
- `report.Status = "unlocked"` (the §2.3 item 2 literal for the
  unlock lane).
- `Action = "report.unlock"`.
- **No `ModeratorAssignment` upsert** (this lane's job is the
  *flag-flip*, not the *assignment*-write — the C-M3b·4
  "separate seam" pin from the §2.2.3 doc-comment: "the flag-flip
  is **not** this lane's job (C-M3b·4 'separate seam' pin) — that
  is the `ResolveReportAsync` lane's (F6)."; the *assign* lane
  writes the `ModeratorAssignment`, the *unlock* lane flips the
  flag).
- `SetComponentModeratorAccessAsync` call (if `ComponentId` is
  non-null): `await _userInfo.SetComponentModeratorAccessAsync(
  report.ComponentId, true, globalAdminId)` — M1's frozen seam
  (own session — the "flag-flip commits separately" note). **This
  call happens BEFORE the `SaveChangesAsync` at the end** — the
  flag lands in its own commit; the `Report.Status` + audit row
  commit atomically in the caller's transaction (the C3 pin is
  satisfied for the *report-domain + report-audit* pair; the
  flag-flip is a separate, pre-existing M1 commit).

### `ResolveReportAsync` (F6, C-M3b·4)

Same shape as `UnlockAsync`. Differences:
- `report.Status = "resolved"` (the §2.3 item 2 literal for the
  resolve lane).
- `Action = "report.resolve"`.
- The flag-flip call (`SetComponentModeratorAccessAsync`) is the
  **same** (per §2.2.3 `ResolveReportAsync`'s doc-comment: "The
  flag-flip via
  `IUserInfoService.SetComponentModeratorAccessAsync`
  is this lane's job — see `UnlockAsync`.").

### `CanReadWithReportAsync` (F2, C-M3b·2)

1. Argument guards (`postId` non-empty, `actorId` non-empty).
2. `await using var session = _store.OpenSession(new
   SessionOptions())` (writable — the standalone lane's
   "audit-row in own commit" shape requires it).
3. `post = await session.LoadAsync<Post>(postId)` → null → return
   synthetic deny `new Decision(false, AccessVia.Report, actorId)`
   (no audit row — the post doesn't exist, so there's no "what are
   we auditing?" subject — the Web layer's 404 handles it).
4. `filedReports = await session.Query<Report>().Where(r => r.PostId
   == postId).ToListAsync()` (Marten's `.Where` on `IQueryable`
   returns `Task<IReadOnlyList<T>>` per the codebase's existing
   `Marten` convention).
5. **If `filedReports.Count == 0`** (§2.4 item 4 — C5 unactivated):
   - Write `AccessAudit { actorId, actorId, "read", "post", postId,
     AccessVia.Report, Deny }` (the §2.4 item 3 literal pin on the
     read-branch audit row);
   - `await session.SaveChangesAsync()` (the standalone lane's own
     commit);
   - Return `new Decision(false, AccessVia.Report, actorId)`.
6. **If a filed report exists** — delegate:
   ```csharp
   return await _authz.CanAsync(
       actorId,
       AccessAction.Read,
       new PostToAuditableResource(post)).ConfigureAwait(false);
   ```
   The M1 seam's own §A decision algorithm runs in its own session;
   its audit row commits in its own (separate) transaction. The
   Web layer renders 403 on `Allowed = false`.

## Invariants held by U5's code

- **C-M3b·2** (`Via = Report` read branch) — **lives in U5's code**
  (the `CanReadWithReportAsync` body is the C5-activation gate + the
  §2.4 item 3 `AccessVia.Report` audit-row pin on the early-reject
  path).
- **C-M3b·4** (assign / unlock / resolve — GlobalAdmin-gated, SoD)
  — **lives in U5's code** (the three write-lane bodies are the
  C-M3b·4 implementation).
- **C3** (same-transaction) — **lives in U5's code** (one
  `SaveChangesAsync` per write lane; the
  `SetComponentModeratorAccessAsync` call is a separate, pre-existing
  M1 commit per its frozen shape).
- **ADR 0006-C** (audit always on — Allow AND Deny) — **lives in
  U5's code** (the write lanes' audit rows are written on both the
  Allow and Deny paths — U4's `FileReportAsync` is a write lane
  (no decision call, so only the Allow path is reachable); U5's
  three write lanes and the `CanReadWithReportAsync` early-reject
  path all carry that pin).
- **ADR 0006-D** (single decision path) — **lives in U5's code**
  (the three write lanes' `CanAsync` calls are the single-decision-
  path pin; the `CanReadWithReportAsync` delegated case is the
  standalone decision path).
- **ADR 0003** (SoD) — **lives in U5's code + U7's Web layer**
  (the three write lanes' `AccessAction.Moderate` call is the
  SoD discriminator; U7's `ModerationController`
  `[Authorize(Roles = GlobalAdmin)]` is the standing assertion).

## Unit-series rules honored

- Rule 1 (file in `Deliverables`): U5 only writes
  `src/Kumunita.Core/Moderation/ModerationService.cs` (the one file in
  its deliverable list — `ModerationService` is the target).
- Rule 3 (no test whose name isn't in §2.5): U5 does **not**
  introduce a test (U5's Exit is `run_build` green; the §2.5 tests
  5–8 and 3–4 — `AssignReportAsync_*` / `ResolveReportAsync_*` /
  `CanReadWithReportAsync_*` — are U9's deliverables).
- Rule 4 (no new seam on `IUserInfoService` / `IAuthorizationService` /
  `IIdentityService`): U5 does **not** add a new seam to any frozen
  interface. It **uses**:
  - `IAuthorizationService.CanAsync(string, AccessAction,
    IAuditableResource, IDocumentSession)` (the ADR 0006-E
    compatible lane — already in the M1 frozen surface) and
  - `IAuthorizationService.CanAsync(string, AccessAction,
    IAuditableResource)` (the standalone overload — the M1 frozen
    surface) and
  - `IUserInfoService.SetComponentModeratorAccessAsync` (the M1
    frozen seam, unchanged).
  No new `using` is needed. No new `AccessAction` id is introduced
  (the two existing — `Read` / `Moderate` — cover U5's lanes
  per §2.0 preambles / M1's "named here" list).
- Rule 5 (never reshape `Post` / `PostReply` / `Report`): U5 does
  **not** reshape any of these. The three write lanes write exact
  literals (`"assigned"` / `"unlocked"` / `"resolved"`) to the
  existing nullable `Report.Status` field (a *value* write, not a
  *shape* change).
- Rule 6 (never flip `Component.ModeratorAccess` outside
  `SetComponentModeratorAccessAsync`): U5's `UnlockAsync` /
  `ResolveReportAsync` **do** call `SetComponentModeratorAccessAsync`
  (that **is** the "the existing seam" — the rule is honored by
  using the existing seam, not by avoiding it).

## Key files

- `D:\repos\Kumunita\docs\design\m3b-moderation.md` — §2.2.3's
  C# block (frozen signatures) + §2.3 (the four `Via` /
  `Status` / no-partial-write pins) + §2.4 (the read-branch
  shape + the flag-flip call + the `Via = Report` audit-row
  literal + the C5 unactivated pin) + §2.5 (U9's test anchor).
- `D:\repos\Kumunita\src\Kumunita.Core\Moderation\ModerationService.cs`
  — **the file U5 modifies** (U4 already created it with the ctor +
  `FileReportAsync`); U5's `Deliverables` list is the one file this.
- `D:\repos\Kumunita\src\Kumunita.Core\Authorization\AccessAudit.cs`
  — the `AccessAudit` POCO (the U5 write lanes write to it).
- `D:\repos\Kumunita\src\Kumunita.Core\Authorization\AccessAction.cs`
  — the frozen `AccessAction.Moderate` / `AccessAction.Read` id
  (U5's lanes reference the existing ids; no new id introduced).
- `D:\repos\Kumunita\src\Kumunita.Core\UserInfo\Component.cs` —
  the `ModeratorAssignment` POCO (U5's `AssignReportAsync`
  upserts it).
- `D:\repos\Kumunita\src\Kumunita.Core\UserInfo\UserInfoService.cs` —
  M1's `SetComponentModeratorAccessAsync` (U5's F6 flag-flip call).
- `D:\repos\Kumunita\src\Kumunita.Core\Posts\PostToAuditableResource.cs`
  — the M3 adapter (U5's write lanes pass it into
  `CanAsync`; U4's `FileReportAsync` does **not** use it — the
  adapter is only for a `CanAsync` decision target).
- `D:\repos\Kumunita\src\Kumunita.Core\Authorization\AuthorizationService.cs`
  — the M1 frozen §A decision algorithm (U5's write lanes call it
  through the `IAuthorizationService` seam; U5 does **not**
  modify it).
- `D:\repos\Kumunita\docs\plans-milestones\m3b-handoff-notes.md`
  — U5's Exit deliverable (the `## U5` section).

## Risks & Open Questions

- **The "same `IDocumentSession` transaction" pin for the
  flag-flip** (§2.4 item 2). U5 **cannot** satisfy it literally:
  M1's `SetComponentModeratorAccessAsync` opens **its own** session
  (a frozen shape — unit-series rule 4 forbids reshaping it). The
  M1 seam's own `SaveChangesAsync` is its own commit. U5's
  mitigation: call the M1 seam *before* the final
  `SaveChangesAsync` on the caller's session, so the
  caller's-transaction `Report.Status` + `AccessAudit` pair commits
  atomically (the C3 pin **is** honored for the report-domain +
  report-audit pair); the flag-flip is a *pre-existing,
  separate-commit* M1 action (documented in the XML
  doc-comment). A **drift note** belongs in the handoff
  (`docs/plans-milestones/m3b-handoff-notes.md`, `## U5`).
- **`Action = "report.assign"` / `"report.unlock"` /
  `"report.resolve"`** — U5's vocabulary-level choice against
  M1's existing `"report-file"` (U4), `"moderator-access"` /
  `"delegation.grant"` / `"delegation.revoke"` / `"profile.upsert"`
  — **not** a drift pin, but U9's tests will anchor these
  literals (test-6, test-7, test-8 — the §2.5 pin name each test
  as "…_Writes…Via/…Status…", so U5's action string is an
  implementation-level detail that U9's tests can assert
  independently).
- **`ModeratorAssignment` upsert shape** — U5's `AssignReportAsync`
  reuses an existing `(UserId, ComponentId)` row if present
  (the M1 POCO's doc-comment: "One row per (user, component) pair").
  U9's test-6 (`AssignReportAsync_GlobalAdmin_WritesStatusAssigned_ModAssignmentRow`)
  will anchor the exact write.
- **`report.ComponentId` null handling** — U5's guard: *skip
  the flag-flip call* and *skip the `ModeratorAssignment` upsert*
  when `ComponentId` is null. This is implementation-level (the M3
  post POCO's `ComponentId` is nullable per C-M3·2's "feed
  organizer / moderation scope" pin — the post can exist without
  a component); not a drift, but U9's tests should cover the
  null-`ComponentId` edge if time permits.
- **M1 §A `Decision` on the delegated path** — when U5's write
  lanes call `CanAsync`, the `Decision.EffectivePrincipalId`
  (and `Via`) is resolved by the M1 §A algorithm in
  `AuthorizationService.ResolveActorAsync` (delegated /
  break-glass / owner / moderation / audience — whatever matched
  first per the §A fixed-order branch). The write lane's
  `AccessAudit` row honors the *decision's own* `Via` tag (not
  the lane's own pin — the write-lane pins are on `Via` only
  for *filing/hiding/removing* where there's a single
  deterministic lane-`Via` literal; the write lanes here are
  *decision* lanes, so the row reflects the decision's `Via`).

## Steps

1. **Implement the four methods** in
   `src/Kumunita.Core/Moderation/ModerationService.cs` (append to
   the class, after `FileReportAsync`).
   - `AssignReportAsync(string reportId, string assignedToModeratorId,
     string globalAdminId, IDocumentSession session)` (F5).
   - `UnlockAsync(string reportId, string globalAdminId,
     IDocumentSession session)` (F6, C5 activation).
   - `ResolveReportAsync(string reportId, string globalAdminId,
     IDocumentSession session)` (F6).
   - `CanReadWithReportAsync(string postId, string actorId)` (F2,
     the standalone read branch — **no** `IDocumentSession`
     parameter per the §2.4 item 1 pin).
   - Each body follows the shape described in **Approach** above.
   - Each has an XML doc-comment that names the invariant / FACES
     row / §2.x pin it pins (per the repo's M1/M2/M3 convention:
     the doc-comment is part of the deliverable).
2. **`run_build`** on `Kumunita.Core` must be green.
3. **Run the Core test suite** (`run_tests` on
   `Kumunita.Core.Tests`) — U5 does **not** add new tests (rule 3:
   the §2.5 U5-anchored tests are U9's deliverable), but U5 should
   verify no existing test breaks (U4's
   `FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner` /
   `FileReportAsync_Filing_WritesReportStatusFiled` — if those
   are landed by the time U5 runs — and U3's
   `HidePostAsync_*` / `RemovePostAsync_*` must stay green).
4. **Append the handoff note** to
   `docs/plans-milestones/m3b-handoff-notes.md` — the
   `## U5 — Assign / unlock / resolve + Via=Report read branch`
   section. Content per the U5 register line's Exit requirement:
   - The **three** write-lane signatures (verbatim, as landed).
   - The **read branch** signature (verbatim, as landed).
   - The `Action` strings chosen (`"report.assign"` /
     `"report.unlock"` / `"report.resolve"` / `"read"`).
   - The `Via` tags on each lane (the write lanes' audit rows
     carry the **decision's** `Via`; the read branch's early
     deny carries `AccessVia.Report` per the §2.4 item 3 pin;
     no **new** `AccessAction` id introduced — the two existing
     `Read` / `Moderate` ids cover all four lanes).
   - Confirmation **`report.ComponentId` null handling**: the
     three write lanes *skip the flag-flip / skip the
     `ModeratorAssignment` upsert* when `ComponentId` is null
     (implementation-level choice, not a §2.3 pin).
   - Confirmation **`run_build` green** + **existing Core tests
     unchanged** (U5's Exit is build green, not tests — the
     handoff note records that the **existing** tests are the
     regression check, not a U5-added set).
   - **Drift note** (if needed): the §2.4 item 2 "same
     `IDocumentSession` transaction" pin is **not** literally
     satisfied by the M1 `SetComponentModeratorAccessAsync`
     seam (own session, own commit — frozen shape). The
     `Report.Status` + `AccessAudit` pair are in the
     caller's transaction (C3 honored for that pair); the
     flag-flip is a separate M1 commit (unit-series rule 4:
     the M1 seam is not reshaped by U5). The §2.0 drift-guard
     applies — **the design doc wins** — so U9's test-7
     (`ResolveReportAsync_GlobalAdmin_WritesStatusResolved_FlipsFlagSameTxn`)
     should be structured to check the *domain + audit row*
     pair's same-transaction commit, and the *flag-flip's
     own-commit* behavior, rather than a single-txn
     assertion.
