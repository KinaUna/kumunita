# U4 — Report filing (resident-facing write lane) — execution plan

- **Unit register line:** `docs/plans-milestones/plan-m3b-moderation.md` → U4.
- **Deliverables:** **1 file, new** — `src/Kumunita.Core/Moderation/ModerationService.cs` (per the U4 register line, verbatim).
- **Exit:** `run_build` green on `Kumunita.Core`; handoff note appended to
  `docs/plans-milestones/m3b-handoff-notes.md` as `## U4 — Report filing` **before** any follow-up action.

## Understanding

U4 lands the **first** method on the new `Kumunita.Core.Moderation.ModerationService`
(§2.2.3's one new bounded context): `FileReportAsync` — the resident-facing
intake write lane for a post-relevant report (F1 / C-M3b·1). Filing is a
**write** action, not an access decision — per C-M3b·1 and §2.2.3's own
doc-comment pin it makes **no** `IAuthorizationService.CanAsync` call (no
`AccessAction.Read`, no `AccessAction.Moderate`); it composes only the
frozen seams for data access (`IDocumentSession` for the caller's
transaction) and writes two rows (the `Report` domain row, the
`AccessAudit` admin-action row) into the **one** caller-owned transaction
(C3 / ADR 0006-C — one `SaveChangesAsync`, no partial write; §2.3 item 4
pin). The audit row carries the pinned tag `AccessVia.Admin` (NOT
`AccessVia.Report` — reserved for the read branch C-M3b·2, §2.3 item 1 pin;
NOT `AccessVia.Owner`, the C1 owner-branch negative), and the `Report`
row's `Status` field is set to the **exact literal `"filed"`** (the first
of the four Status-literal pins, §2.3 item 2 pin).

## Assumptions

- **Design-doc `§2.2.3` is authoritative** (the §2.0 drift-guard: this
  file wins over any stale handoff-note line): the frozen signature is
  `public async Task<int> FileReportAsync(string postId, string actorId,
  string? reason, IDocumentSession session)`. The earlier U1 handoff line
  "Task FileReportAsync(string postId, string reporterId, ...)" is
  superseded — U1's line was a sketch for U2 to freeze; §2.2.3 froze it.
  (If the frozen pin turns out to be a *different* signature than the one
  U1 sketched, §2.0 resolves it: the **current** design doc wins.)
- **One write lane, one transaction (C3):** the `Report` row + the
  `AccessAudit` row + the caller's `SaveChangesAsync()` are the **single
  commit step**. A caller aborts the session → both rows roll back
  atomically. No partial write is possible (this is what §2.3 item 4
  pins, and it's what the M1 `SetComponentModeratorAccessAsync` / M3
  `CreatePostAsync` / U3 `HidePostAsync` all already do).
- **No `IAuthorizationService.CanAsync` call in `FileReportAsync`**
  (C-M3b·1 / §2.2.3 FileReportAsync doc-comment). The `Moderate` action
  is exercised in the **hide/remove** lanes (U3's `HidePostAsync` /
  `RemovePostAsync` — already in `PostService`), not here; the `Report`
  literal is exercised in the **read branch** (U5's
  `CanReadWithReportAsync`), not here; the `Owner` literal is the C1
  owner-branch, not here. Filing is an intake action — same shape as
  M1's `IUserInfoService.UpsertProfileAsync` (the design doc §2.2.3
  names this precedent explicitly).
- **The audit row's `Action` string is NOT pinned by §2.3** — §2.3's four
  numbered pins cover the four `Via` tags, the four `Status` literals,
  and the "no partial write" discipline; the action-string vocabulary is
  a Core-convention-level detail (M1's `IUserInfoService` uses
  `"group.add-member"` / `"group.remove-member"` / `"delegation.grant"` /
  `"delegation.revoke"` / `"moderator-access"`, none of which is a M3b
  pin). U4 picks one that fits the M1/M2 style; U5 picks the three other
  write-lane action strings against the same vocabulary. The `Via` tag
  (`AccessVia.Admin`) IS the pinned value (the §2.3 item 1 negative pair:
  not `Report`, not `Owner`).
- **The `Report` POCO is unchanged** (rule 5: never reshapes). `Status`
  is the existing nullable `string` on the M3-registered `Report` — U4
  writes the literal `"filed"` to it, that's the entire C-M3b·1
  "filing" surface on the domain row.
- **The `Report` row's `ComponentId`:** the `Report` POCO has a nullable
  `ComponentId` field (see
  `src/Kumunita.Core/Posts/Report.cs`). U4 sets it from the post's
  `Post.ComponentId` (which is also nullable on the M3 post POCO per the
  C-M3·2 "feed organizer / moderation scope, never an access boundary"
  convention) so the U5/U6/U7 surfaces can scope the queue / resolve-UI /
  flag-flip to the component. If the post's `ComponentId` is null, the
  report's `ComponentId` is also null (no fabricated default — no new
  rule invented by U4).
- **`Return int`:** §2.2.3 pins `Task<int>` (not `Task`). Since there is
  exactly one domain row written, U4 returns **the count of `Report` rows
  written (1)** on success — consistent with the "how many rows were
  written by this call" reading and matching the M2-style "int is the
  affected-row count" convention. (If the design doc's intent is
  otherwise, §2.0 drift-guard applies; U1's sketch was `Task` with no
  body, so §2.2.3's `int` is the frozen pin and its meaning is
  implementation-level — U4's choice, documented in the
  XML doc-comment per convention.)

## Approach

Create `src/Kumunita.Core/Moderation/ModerationService.cs` as a new
`sealed` class in the new bounded context `Kumunita.Core.Moderation`
(per §2.2.3's file/namespace pin). The class:

- Has a **ctor** (`IUserInfoService userInfo, IAuthorizationService authz,
  IDocumentStore store`) with the same null-throw shape as M2's
  `DirectoryService` and M3's `PostService` (defensive `?? throw new
  ArgumentNullException(...)` pattern). All three fields are `readonly`
  (C-M3b·2 / ADR 0006-D composes the frozen seams; the `IDocumentStore`
  is the caller's session provider — not a new seam opened by M3b).
- Has **one** implemented method in U4: `FileReportAsync` (the U4
  deliverable per the register line and per §2.2.3's "U4 implements"
  note). The **other four methods** (`AssignReportAsync`, `UnlockAsync`,
  `ResolveReportAsync`, `CanReadWithReportAsync`) are **NOT
  implemented in U4** — the U4 register line is explicit
  ("implement `FileReportAsync` on the new `ModerationService`"), and
  the U5 register line is explicit ("complete
  `ModerationService` (`AssignReportAsync`, `UnlockAsync`,
  `ResolveReportAsync`) and land the `Via = Report` read-lane addition").
  So U4's class has **only** the ctor + `FileReportAsync`. U5 adds the
  other four methods as modifications to the same file (matching the U5
  register line "modify — add the three methods", and the §2.4 pin for
  the read lane).

  This split keeps U4's deliverable **≤ 1 file, ≤ ~60 LOC of method
  body** (matching the unit register's "≤ ~300 lines each" guidance —
  U4's class is well under that). It also means U5 can add the three
  GlobalAdmin-gated lanes + the read branch independently without
  touching U4's FileReportAsync body. Two units, one file, two
  method-groups: U4's intake lane (F1, C-M3b·1) and U5's
  GlobalAdmin/SoD + read lanes (F5/F6/F2, C-M3b·2 / C-M3b·4).

- `FileReportAsync` body (C3, ADR 0006-C — no silent unaudited access):
  1. Argument guard (`ArgumentException` on empty `postId` / `actorId`,
     `ArgumentNullException` on null `session`) — same shape M3's
     `CreatePostAsync` / `CreateReplyAsync` use.
  2. **Load the post from the caller's session**
     (`session.LoadAsync<Post>(postId)`) — the report is filed *against*
     a post, so the post must exist in the caller's transaction scope.
     Missing post → `KeyNotFoundException` (no report row, no audit row —
     a failed call, not a partial write).
  3. **Build the `Report` domain row** with:
     - `Id = Guid.NewGuid().ToString("N")` (M1/M2/M3 convention —
       `SetComponentModeratorAccessAsync`, `CreatePostAsync`, etc. all
       use the `"N"` form).
     - `PostId = postId` (the post the report is filed against).
     - `ReporterId = actorId` (the acting resident — the reporter IS
       the actor in this lane; no delegation / no break-glass / no
       GlobalAdmin path here — C-M3b·1's resident-facing pin).
     - `ComponentId = post.ComponentId` (carry the post's component
       scope so U5's queue / resolve-UI / flag-flip can scope by
       component; null if the post has no component).
     - `Reason = reason` (the caller-supplied free-text, nullable).
     - **`Status = "filed"`** — the **exact literal** per §2.3 item 2
       (the first of the four Status-literal pins; U5's three other
       lanes write `"assigned"` / `"unlocked"` / `"resolved"` in their
       own commits, each a different literal on a different call).
     - `At = DateTimeOffset.UtcNow` (the row's creation timestamp).
  4. `session.Store(report)` — stage the domain row in the caller's
     transaction (C3: nothing commits until `SaveChangesAsync`).
  5. **Build the `AccessAudit` row** with:
     - `Id = Guid.NewGuid().ToString("N")` (same convention).
     - `At = now` (the same instant as the report row — "these two
       rows are one logical write"; using one shared timestamp makes
       the "same commit pair" relation auditable).
     - `ActorId = actorId` — the acting resident (the reporter is the
       actor in this lane, per C-M3b·1's resident-facing pin).
     - `EffectivePrincipalId = actorId` — in the filing lane the actor
       IS the principal (no delegation / break-glass / GlobalAdmin
       path; the filing is not an access decision, so there's no
       "acting under" to distinguish from the actor).
     - `Action = "report-file"` — the action name for the audit row.
       NOT a §2.3 pin (§2.3's four items are the Via tags, the Status
       literals, and the no-partial-write discipline — the action
       string is implementation-level, chosen to match the M1/M2
       kebab-case / verb pattern). Distinct from M1's
       `"moderator-access"` (the flag-flip action, distinct
       meaning: that one flips the `ModeratorAccess` field on a
       Component; this one files a resident's report — different
       verb, different noun, different resource).
     - `TargetKind = "post"` — the audit row's focus is the post the
       report is filed against (the `PostToAuditableResource`'s
       `TargetKind` pin, §2.2.1 in M3). Not `"report"` (the domain row
       that IS the report is the audit row's subject of an action;
       the `TargetKind` is the *referenced* post — the "what is this
       audit row auditing?" question).
     - `TargetId = postId` — the post the report is filed against.
     - **`Via = AccessVia.Admin`** — the **exact literal** per §2.3
       item 1 (the filing-Via pin — NOT `AccessVia.Report` (reserved
       for the read branch, C-M3b·2), NOT `AccessVia.Owner` (the C1
       owner-branch negative)). The rationale is §2.3 item 1's own
       text: "M1's frozen AccessVia vocabulary has no 'Intake' literal;
       Admin is the least-distortion slot (… a resident-filing action
       is equally 'a standing that is not Owner / Audience /
       Delegation / Moderator / BreakGlass / Report') — the two
       negatives (not Report, not Owner) are authoritative."
     - `Outcome = AccessOutcome.Allow` — no `Deny` is possible in this
       lane (no `IAuthorizationService` call; the "no partial write"
       pin (§2.3 item 4) is implemented as "no `Status` write if the
       row failed to save" — the single `SaveChangesAsync` either
       commits both rows or rolls both back). The `Allow`/`Deny`
       discriminant exists to record the outcome of a decision
       ("no decision was run" and "the decision was Allow" are both
       represented as `Outcome = Allow` on the write-lane audit row,
       matching M1's `SetComponentModeratorAccessAsync` which is also
       a write-lane-with-no-decision call).
  6. `session.Store(new AccessAudit { ... })` — stage the audit row in
     the caller's transaction.
  7. **`await session.SaveChangesAsync()`** — the single commit step
     (C3 — one `SaveChangesAsync` makes the two rows commit atomically;
     a caller aborts the session → both rows roll back; no partial
     write is possible — §2.3 item 4 pin).
  8. `return 1` — the count of `Report` rows written (1 on success;
     the only path to reach this line has written 1 row, so the return
     value is effectively a "one row created" success signal).

**Invariants held by U4's code:**

- **C-M3b·1** (resident-facing intake, no authz call, pinned filing `Via`
  tag) — **lives in U4's code** (the `FileReportAsync` body is the
  implementation; the doc-comment names the pin).
- **C-M3b·2** (`Via = Report` read branch) — **NOT in U4's code** (U5
  implements, per the U5 register line); U4's audit row does NOT
  carry an `AccessVia.Report` literal (the two negatives in §2.3 item 1 —
  **not `Report`, not `Owner`** — are honored: the literal is
  `AccessVia.Admin`).
- **C-M3b·3** (hide/remove lane) — **NOT in U4's code** (U3's
  `HidePostAsync` / `RemovePostAsync` in `PostService` — already landed
  per U3's handoff).
- **C-M3b·4** (assign / SoD, GlobalAdmin-gated) — **NOT in U4's code**
  (U5's `AssignReportAsync` / `UnlockAsync` / `ResolveReportAsync` —
  not yet landed).
- **C3** (same-transaction) — **lives in U4's code** (one
  `SaveChangesAsync` at the end of `FileReportAsync`).
- **ADR 0006-C** (audit always on — Allow AND Deny) — **lives in the
  Caller's Session** via U4's staged `AccessAudit` row (the audit row
  is written on the single success path; failure = no row, but that's a
  *failed call*, not a silent unaudited access).
- **ADR 0006-D** (single decision path, no second read of
  `GroupMembership`/`DelegationGrant`) — **lives in U4's code**
  (U4 does not read membership / delegation; it composes only the
  `IUserInfoService` / `IDocumentStore` seams).
- **ADR 0004 §B.1** (additive on `Post`, no migration) — **lives in
  U3's code** (U3 landed `Post.Status` — U4 only reads it from the
  post). U4 does not reshape `Post` / `PostReply` / `Report` (rule 5).

**Unit-series rules honored:**
- Rule 1 (file in `Deliverables`): U4 only writes
  `src/Kumunita.Core/Moderation/ModerationService.cs` (the one file in
  its deliverable list).
- Rule 3 (no test whose name isn't in §2.5): U4 does **not** introduce
  a test whose name isn't in §2.5 — the §2.5 tests anchored to
  C-M3b·1 (F1) are tests 1 and 2 (`FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner`
  and `FileReportAsync_Filing_WritesReportStatusFiled`), which are
  **U9's deliverable** (not U4's — U4's register line has no test
  deliverable; U4's Exit is `run_build` green).
- Rule 4 (no new seam on `IUserInfoService` / `IAuthorizationService` /
  `IIdentityService`): U4 constructs the class over the **frozen**
  seams (`IUserInfoService`, `IAuthorizationService`, `IDocumentStore`)
  per §2.2.3's ctor pin — no new method names on either interface are
  used by U4.
- Rule 5 (never reshape `Post` / `PostReply` / `Report` beyond the
  additive `Status` field): U4 does **not** reshape `Report` (the
  POCO already has `Status` as a nullable string, per the M3-registered
  `Report.cs` — U4 writes the literal `"filed"` to it, which is a
  *value* write, not a *shape* change).
- Rule 6 (never flip `Component.ModeratorAccess` outside the existing
  `SetComponentModeratorAccessAsync` seam): U4 does **not** invoke
  `SetComponentModeratorAccessAsync` (that is U5's F6 flag-flip, in
  `UnlockAsync` / `ResolveReportAsync` per the U5 register line).

## Key files

- `D:\repos\Kumunita\docs\design\m3b-moderation.md` (the **authoritative
  pin** — §2.2.3's C# block is the exact signature, §2.3's four numbered
  pins are the exact `Via` / `Status` / no-partial-write values, §2.5's
  seam-test list is U9's deliverable (not U4's), §2.0's drift-guard
  resolves U1's stale `Task` sketch vs. §2.2.3's `Task<int>` in favor of
  §2.2.3).
- `D:\repos\Kumunita\src\Kumunita.Core\Posts\Report.cs` (the M3-registered
  `Report` POCO — U4 writes to it, does not reshape it).
- `D:\repos\Kumunita\src\Kumunita.Core\Authorization\AccessAudit.cs`
  (the `AccessAudit` POCO — U4 writes to it, does not reshape it; the
  existing `AccessVia` enum lives alongside it, in the same file — U4
  references `AccessVia.Admin` per §2.3 item 1).
- `D:\repos\Kumunita\src\Kumunita.Core\Posts\PostService.cs` (U3's
  `HidePostAsync` / `RemovePostAsync` — the **same transaction / C3**
  shape U4's `FileReportAsync` mirrors; the "one
  `SaveChangesAsync`" discipline is the same convention).
- `D:\repos\Kumunita\src\Kumunita.Core\UserInfo\UserInfoService.cs`
  (M1's `SetComponentModeratorAccessAsync` — the **closest analog** for
  a frozen-interface-composing class that writes a domain row + an audit
  row in one commit; its `Action = "moderator-access"` / `TargetKind =
  "component"` / `Via = Admin` / `Outcome = Allow` shape is the
  convention U4's `AccessAudit` row follows).
- **New file to create:**
  `D:\repos\Kumunita\src\Kumunita.Core\Moderation\ModerationService.cs`.

## Risks & Open Questions

- **The `Action` string on the audit row.** §2.3 does **not** freeze
  the `Action` value — only the `Via` tag (item 1), the `Status`
  literal (item 2), the hide/remove `Via` tag (item 3), and the
  no-partial-write discipline (item 4). So U4 picks the action string.
  U4's choice (`"report-file"`) is **not** a drift from the design
  doc — it's an implementation-level decision that U9's seam test
  (test-1, `FileReportAsync_Filing_ViaTagIsAdmin_NotReport_NotOwner`)
  is expected to *assert* (per §2.5 pin #1: "C-M3b·1 (F1) — the two
  negatives + the pinned `Admin` literal (item 1)"). If U9's test
  hard-codes a different `Action` value, the drift-guard (§2.0) resolves
  it: the design doc wins, but §2.3 does not pin the `Action`, so the
  test's value is authoritative for U4's choice — U9's test will tell
  U4 the exact `Action` string to use, and U4 will match it. (This is
  not a *U4*-side risk because U4's Exit is `run_build` green; U9's
  Exit is the test-suite green, and U9's tests will anchor the exact
  string.)
- **`Return int` semantics.** The §2.2.3 pin is `Task<int>`; U4
  returns 1 (one row written). If U9's test expects a different int
  (e.g., the report's `Id` as an int, or 0 on success), the drift-guard
  applies. U4's choice (1 = one row) is a natural "affacted-row-count"
  convention (matching the "M2-style 'int is the affected-row count'"
  reading), but U9 will anchor the exact value. U4's Exit criterion is
  `run_build` green, which is met regardless of the return value; U9's
  Exit is the test-suite green, which is where the exact int matters.
  This tension is resolved naturally by the unit register's sequencing
  (U4 before U9): U4 lands the signature, U9 lands the test, and U9's
  test drives the exact int.

## Steps

1. **Create** `src/Kumunita.Core/Moderation/ModerationService.cs` — one
   new file, one new bounded-context namespace (`Kumunita.Core.
   Moderation`, per the §2.2.3 file/namespace pin). The file contains:
   - a file-level doc-comment naming U4's deliverable (the
     `FileReportAsync` lane — F1 / C-M3b·1) and the unit register line
     it implements, with the same "doc-comment on a class/method"
     convention M1/M2/M3's Core services follow
     (`IUserInfoService` / `DirectoryService` / `PostService`);
   - the `sealed class ModerationService` with a 3-parameter ctor
     (`IUserInfoService userInfo, IAuthorizationService authz,
     IDocumentStore store`) and `readonly` fields — the **same ctor
     shape** as M3's `PostService` / M2's `DirectoryService` (the
     `?? throw new ArgumentNullException(...)` pattern);
   - **one** implemented public method in U4: `public async Task<int>
     FileReportAsync(string postId, string actorId, string? reason,
     IDocumentSession session)` (the exact signature per §2.2.3's
     pin; the method body per the Approach above);
   - the **other four methods** are **not** implemented in U4 — U5
     adds them per the U5 register line.

2. **Verify** `Kumunita.Core` compiles — `run_build` on
   `src\Kumunita.Core\Kumunita.Core.csproj` must be green (0 warnings,
   0 errors) — the Exit criterion for U4 (per the register line).

3. **Append** the handoff note section `## U4 — Report filing (U4)
   (implement `FileReportAsync` on the new `ModerationService`)` to
   `docs/plans-milestones/m3b-handoff-notes.md`, mirroring the M2/M3
   handoff-note convention (one section per unit, appended, never
   rewritten). The section's content mirrors U3's section shape
   (U3's section in the **same** file — the deliverables, the entry
   reads, the invariants held, the build status, the seam-test
   reconciliation):
   - **Deliverable:** `src/Kumunita.Core/Moderation/ModerationService.cs`
     (1 file, new — the **U4** one-file deliverable per the register
     line).
   - **Entry-reads confirmed:** the U4 register line's 3 entry reads
     (`docs/design/m3b-moderation.md` §2.2/§2.3,
     `src/Kumunita.Core/Posts/Report.cs`,
     `src/Kumunita.Core/Posts/PostToAuditableResource.cs`,
     `src/Kumunita.Core/M3DocTypes.cs`) — each confirmed against the
     actual file contents.
   - **What U4 implemented (verbatim):** the full method signature +
     a short summary of the body's shape (one `KeyNotFoundException`
     guard on missing post, one `Report` row staged, one `AccessAudit`
     row staged, one `SaveChangesAsync`, `return 1`); the doc-comment
     (the class + method doc-comments naming the invariant / FACES /
     via / status pins); the `return 1` convention (documented in the
     U4 plan above).
   - **Invariants held:** C-M3b·1 (lives in U4's code), C3 (lives in
     U4's code), ADR 0006-C (lives in U4's staged `AccessAudit` row),
     ADR 0006-D (lives in U4's code — no `GroupMembership` /
     `DelegationGrant` reads), ADR 0004 §B.1 (U4 does not reshape
     `Report` / `Post`), C-M3b·4 (NOT in U4's code — U5's F6 flag-flip,
     per the U5 register line).
   - **Build status:** `run_build` on `Kumunita.Core` — 0 warnings / 0
     errors / **Build succeeded.** (The same Exit criterion the U3
     register line pinned.)
   - **No existing M3 seam-test name broke** (U4 does not add any new
     test; the 16 pinned M3b seam-test names are U9's deliverable).

4. **Hit the Exit** — the U4 unit register line's Exit is "run_build
   green" (not a test-suite green, not a "U4 closed" gate — the test/
   gate work is U9's and U11's deliverables respectively). The append
   of the U4 handoff note is the Exit's last step.
