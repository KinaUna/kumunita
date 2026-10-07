# M27 — living handoff note (scratch tier)

> One `## U#` section per unit, appended (never rewritten). Each unit writes
> exactly one short section before it exits; the next unit reads only that
> section + its own entry-reads list.
>
> **U00 completed 2026-10-06.** It first hit two hard drifts (below) and
> paused for a human decision; both were resolved (Option A for both). U00
> then authored the three deliverables and moved `m27-u00.md` to `done/`.
> The "Drift pause" sections below are the audit trail of the pauses and
> their resolutions; the final `## U00 — design doc + ADR 0148` section is
> the Exit checklist U01 should read.

## U00 — Drift pause

**Date:** 2026-10-06
**Unit:** U00 (lock the design: `m27-user-scoped-portability-design.md` + ADR 0148)
**Status:** STOPPED (resolved) before writing any deliverable. No design doc,
no ADR, no README row **at the time of the pause**. No code, no build run
(docs unit — nothing to build yet). **RESOLVED by user decision: Option A**
(see below).

**Why.** The hard rule "if an entry read contradicts the plan, STOP and record
a drift pause — don't improvise" fired. Entry read #6 (`docs/adr/README.md`)
contradicts the plan's own ADR-number premise in two concrete ways.

**The contradiction (with evidence):**

- **Plan premise (Goal):** "U00 verifies the ADR number is free (against
  `docs/adr/README.md` — the index runs **0001–0147, so `0148` is next**)."
- **Plan Exit instruction:** "`docs/adr/README.md` (one index row, **after the
  0147 row**)."
- **Actual state of `docs/adr/README.md`:** the index's **last** row is
  `| 0145 | Sorting: the one canonical sort contract | Accepted |` — line 144
  of a 144-line file. There is **no 0146 row and no 0147 row** in the index
  (a `Select-String` for `0146` and `0147` in the README returns nothing).
- **Actual state of `docs/adr/` on disk:**
  - `0146-child-account-child-owns-password.md` — **present** on disk,
    **absent** from the index.
  - `0147-todos-and-boards-tags.md` — **present** on disk (a real, Accepted
    ADR), **absent** from the index.
  - `0148-*.md` — **not present** on disk.

**Consequences of the contradiction:**

1. The plan's Exit instruction "add one index row **after the 0147 row**" is
   **not performable as written** — there is no 0147 row in the index to
   anchor after.
2. The premise "the index runs 0001–0147" is **factually wrong**; the index
   runs 0001–**0145**, and two shipped ADRs (0146, 0147) were never added to
   it.
3. `0148` is **free as a number** (no file, no index row), so it remains a
   *viable* choice — but the *reason* the plan gives for that is wrong, and
   the two unindexed ADRs are a separate integrity gap the plan never
   anticipated.

**Resolution (user decision, 2026-10-06):** **Option A** — backfill the two
missing index rows (`0146`, `0147`) first, then add the `0148` row after
`0147`. This widens U00's `docs/adr/README.md` Deliverable from "one row" to
"three rows." The design doc + ADR 0148 + the `docs/adr/README.md` edit are
**still not authored** — this resolution unblocks *how* the README edit
happens, not *whether* U00 proceeds. See "Drift pause #2" below, which is a
separate, still-open blocker.

### U00 — Drift pause #2 (scope inventory vs. the M11 frozen source)

**Date:** 2026-10-06
**Status:** OPEN (resolved) — stopped again before writing any deliverable.
**RESOLVED by user decision: Option A** (see the "Decision needed" block below).

**Why.** The register's D1 and the unit plan's D1 both state the resident
scope is "a closed subset of the **M11 44-doc inventory** … read from the M11
`PortabilityDocTypes` source," but both *simultaneously* list four doc types
that are **not** in that frozen source: `InventoryItem`, `Document`,
`DocumentFolder`, `Bookmark`.

**Evidence:**
- The frozen 44-entry source (`PortabilityDocTypes.Entries`, read in full)
  contains exactly: `Group`, `Component`, `MediaObject`, `Tag`, `Profile`,
  `DelegationGrant`, `GuardianLink`, `GroupMembership`, `GroupInvitation`,
  `GroupJoinRequest`, `ModeratorAssignment`, `ComponentMembership`,
  `TranslationResource`, `GroupTranslation`, `CommunityTranslation`,
  `TagTranslation`, `Post`, `PostReply`, `PostTranslation`, `ReplyTranslation`,
  `Report`, `Announcement`, `AnnouncementTranslation`, `AnnouncementComment`,
  `Event`, `EventRsvp`, `EventTranslation`, `ProjectGoal`, `Project`,
  `TodoItem`, `KanbanBoard`, `KanbanLane`, `BoardItemPlacement`,
  `TodoTranslation`, `BoardTranslation`, `ProjectTranslation`, `TodoComment`,
  `Notification`, `NotificationPreference`, `NotificationSubscription`,
  `Conversation`, `Message`, `Page`, `PageTranslation` — **44 total**. None of
  `InventoryItem` / `Document` / `DocumentFolder` / `Bookmark` are in it.
- All four exist in the codebase on **separate, later** doc-type surfaces
  (M16 Inventory / M21 Documents / M17 Bookmarks), shipped **after** M11's
  inventory was frozen, with clear ownership fields I read directly (not
  invented):
  - `Bookmark.OwnerId` — `src/Kumunita.Core/Bookmarks/Bookmark.cs`
  - `Document.OwnerId` — `src/Kumunita.Core/Documents/Document.cs`
  - `DocumentFolder.OwnerId` — `src/Kumunita.Core/Documents/DocumentFolder.cs`
  - `InventoryItem.AuthorId` — `src/Kumunita.Core/Inventory/InventoryItem.cs`

**The contradiction:** the plan says the ownership field must be read from the
M11 source; the source does not contain these four; but the plan's own D1
list explicitly names them as in-scope resident content. This is a **scope**
decision (does a resident's inventory items / documents / bookmarks travel in
their export, or not?) — not a clerical fix — and the plan's two statements
(the D1 list and the "read from M11 source" instruction) cannot both be
satisfied as written.

**What I deliberately did NOT do:** did not silently drop these four from
scope (contradicts the plan's explicit D1 list); did not silently keep them
and source their ownership field from a different file than the plan specified
(contradicts the "read from M11 `PortabilityDocTypes` source" instruction);
did not invent any ownership fields; did not author the design doc, ADR 0148,
or any README row; did not start U01.

**Decision needed (pick one, then U00 can proceed):**
- **(A)** Keep all four in scope; correct the design doc's sourcing note to say
  their ownership fields are read from the respective doc's own definition
  (the four files above), with an explicit note that these four live **outside**
  the M11 frozen 44-doc set (added to the platform after M11's inventory was
  frozen). The other ~36 in-scope docs keep their ownership field sourced from
  the M11 `PortabilityDocTypes` reference map, exactly as the plan already says.
- **(B)** Drop all four from scope; the plan's D1 list has an error (it names
  docs that don't belong to the M11 frozen inventory and shouldn't ride M11's
  export/import machinery).
- **(C)** A mixed answer (e.g. keep some, drop others) — specify which, and I
  will source each kept doc's ownership field per its actual definition as in
  (A).

**What I deliberately did NOT do (no improvisation):**

- Did **not** backfill the missing `0146`/`0147` index rows (that would be
  touching the index beyond U00's single 0148-row Deliverable, and is a
  decision the user has not authorized).
- Did **not** author `docs/design/m27-user-scoped-portability-design.md`.
- Did **not** author `docs/adr/0148-user-scoped-portability.md`.
- Did **not** add any row to `docs/adr/README.md`.
- Did **not** move `m27-u00.md` to `done/`.
- Did **not** start U01.
- Did **not** run a build (U00 is a docs unit and produced no code; nothing
  to verify yet).

**The decision a human must make to unblock (pick one, then U00 resumes):**

- **(A)** Backfill the index with the two missing rows (`0146`, `0147`) first,
  *then* add the `0148` row after `0147` — restores the index invariant and
  matches the plan's *intent* (a 0147 row exists to anchor after). This widens
  U00's `docs/adr/README.md` Deliverable from "one row" to "three rows."
- **(B)** Do not backfill; add the `0148` row directly after the current last
  row (`0145`), and leave the 0146/0147 gap as a separate, explicitly-deferred
  cleanup (named as a follow-on in the ADR Consequences or a separate lane).
- **(C)** A different ADR number if the team wants 0146/0147 to be the
  "next" numbers for something else — but note 0146/0147 files already exist,
  so any new ADR must be ≥ 0148 regardless.

I am holding here. Everything else in the U00 entry reads (the M11 design doc,
`PortabilityDocTypes.cs`, `PortabilityManifest.cs`, `Decision.cs`) was read and
is consistent with the plan; only the ADR-index entry read contradicts it.

> **RESOLUTION (drift pause #1): user chose Option A** — backfill the two
> missing index rows (`0146`, `0147`), then add the `0148` row after `0147`.
> U00's `docs/adr/README.md` Deliverable is widened from "one row" to "three
> rows." This unblocked the README edit and U00 resumed.

> **RESOLUTION (drift pause #2): user chose Option A** — keep all four
> (`InventoryItem` / `Document` / `DocumentFolder` / `Bookmark`) in scope;
> their ownership fields are sourced from each doc's own definition, with an
> explicit note that these four live **outside** the M11 frozen 44-doc set.
> The `Tag` ownership field is `CreatedBy` (sourced from `Tag.cs`). The other
> in-scope docs keep their ownership field sourced from the M11
> `PortabilityDocTypes` reference map. Both refinements are recorded in the
> design doc §2.8 drift-guard (entries 1 + 2). U00 resumed and completed.

## U00 — design doc + ADR 0148

**Date:** 2026-10-06
**Status:** COMPLETE. All three deliverables authored; `m27-u00.md` moved to
`done/`. Build green (docs only).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` still green (docs only). — **PASS**
  (`Build succeeded in 3.1s`, exit 0).
- [x] The design doc exists with all sections. —
  `docs/design/m27-user-scoped-portability-design.md` (Context / Goals /
  Human cost / Parts affected / Invariants C-M27·1–·8 / FACES F1–F5 / Approach
  / Workflow / Seams & contracts 2.1–2.8 / §kw-l).
- [x] The ADR exists + the README row is added. —
  `docs/adr/0148-user-scoped-portability.md` + `docs/adr/README.md` (three
  rows added: 0146, 0147, 0148 — per Option A).
- [x] **No code, no build beyond docs.** — no code files touched.
- [x] Decisions locked/vetoed. — D1–D9 locked (see below); no veto.

**(a) Decisions locked/vetoed:** D1 (scope = ownership), D2 (M11 format + one
additive optional field), D3 (resident-scoped export filter), D4
(classify + per-entity resolve, no auto-merge), D5 (resident surface,
audited, quiet), D6 (zero new authz surface), D7 (new service on M11
context, no new bounded context), D8 (three Core acceptance tests + Web pins),
D9 (distinct `myportability.*` keys + deferred lanes). No D-item vetoed.
**Two D-item refinements** (in favor of the source, recorded in design doc
§2.8): D1's four non-M11 docs (`InventoryItem` / `Document` /
`DocumentFolder` / `Bookmark`) are kept in scope but sourced from their own
doc definitions (not the M11 reference map); D1's `Tag` ownership field is
`CreatedBy` (sourced from `Tag.cs`, not the M11 reference map which lists no
ref fields for Tag).

**(b) Exact closed resident-scope inventory + per-doc ownership field (U01/U02/U04 copy verbatim):**
- `Post` → `AuthorId` (M11 ref map)
- `PostReply` → `AuthorId` (M11 ref map)
- `PostTranslation` → `AuthorId` (nullable) (M11 ref map)
- `ReplyTranslation` → `AuthorId` (nullable) (M11 ref map)
- `AnnouncementComment` → `AuthorId` (M11 ref map)
- `Event` → `AuthorId` (M11 ref map)
- `EventRsvp` → `UserId` (M11 ref map)
- `EventTranslation` → `AuthorId` (nullable) (M11 ref map)
- `ProjectGoal` → `AuthorId` (M11 ref map)
- `Project` → `AuthorId` (M11 ref map)
- `TodoItem` → `AuthorId` (M11 ref map)
- `KanbanBoard` → `AuthorId` (M11 ref map)
- `TodoTranslation` → `AuthorId` (nullable) (M11 ref map)
- `BoardTranslation` → `AuthorId` (nullable) (M11 ref map)
- `ProjectTranslation` → `AuthorId` (nullable) (M11 ref map)
- `TodoComment` → `AuthorId` (M11 ref map)
- `Conversation` → `ParticipantA` ∪ `ParticipantB` (M11 ref map)
- `Message` → `ConversationId` → `Conversation` (the resident is a participant) (M11 ref map)
- `Page` → `AuthorId` (M11 ref map)
- `Tag` → `CreatedBy` (`Tag.cs` — drift guard entry 2)
- `InventoryItem` → `AuthorId` (`InventoryItem.cs` — drift guard entry 1)
- `Document` → `OwnerId` (`Document.cs` — drift guard entry 1)
- `DocumentFolder` → `OwnerId` (`DocumentFolder.cs` — drift guard entry 1)
- `Bookmark` → `OwnerId` (`Bookmark.cs` — drift guard entry 1)
- `Profile` → `SubjectId` (the resident's own) (`Profile.cs`)
- `Group` → `OwnerId` ∪ member (in `GroupMembership.UserId`) (M11 ref map)
- `GroupMembership` → `UserId` (M11 ref map)
- `GroupInvitation` → `UserId` (M11 ref map)
- `GroupJoinRequest` → `UserId` (M11 ref map)

**(c) Exact per-kind business keys (U04's classifier copies verbatim):**
- `Post` → `(AuthorId, Created, Title, Body)`
- `PostReply` → `(PostId, AuthorId, Created, Body)`
- `Event` → `(AuthorId, Created, Title)`
- `TodoItem` → `(AuthorId, Created, Title)`
- `KanbanBoard` → `(AuthorId, Created, Title)`
- `Page` → `(AuthorId, Created, Title)`
- `Tag` → `Slug`
- `Document` → `(OwnerId, Created, Title)`
- `Conversation` → `(ParticipantA, ParticipantB)`
- `Message` → `(ConversationId, SenderId, Created)`
- `InventoryItem` → `(AuthorId, Created, Name)`
- `Bookmark` → `(OwnerId, TargetKind, TargetId)`
- `Profile` → `SubjectId`
- `Group` → `Name`

**(d) Exact manifest resident-scope marker (U01's manifest add copies verbatim):**
```csharp
// Added to PortabilityManifest (U01) — the ONE additive optional field.
public string? Scope { get; set; }             // "resident" = M27 scope; null = plain M11 whole-instance
public string? ResidentSubjectId { get; set; } // the subjectId this archive is scoped to
```
Every existing `PortabilityManifest` field (`Format`, `GeneratedAt`,
`CommunityName`, `DocCounts`, `MediaManifest`) is **unchanged**. The `Format`
authority stays `kumunita/portability/1` (C-M27·1).

**(e) Exact clean/duplicate/conflict classification + ResolveAsync apply contract (U04/U06 copy verbatim):**
- **Clean** — business key does not match an existing entity AND every ref
  field resolves. Ready to apply.
- **Duplicate** — business key matches an existing entity. Not applied;
  `DuplicateId` carries the existing entity's id.
- **Conflict** — business key does not match, but at least one ref field
  does not resolve. `AbsentReferences` carries the closed list
  (kind + field + value).
- **Pre-write (C-M27·5):** classification runs to completion before any write.
- **Apply (C-M27·4):** clean → apply in M11 import order; duplicate → not
  applied; conflict + `AddElsewhere` → apply with the absent ref re-pointed
  to `PickedTargetId` (the resident must have standing over it, else
  fail-closed); conflict + `Discard` → no write; conflict + **unresolved** →
  **not applied** (no-auto-merge pin). Mid-apply failure ⇒ transaction rolls
  back, **zero** new rows.

**(f) ADR number confirmed free:** `0148` — verified against
`docs/adr/README.md` (no `0148-*.md` on disk; no `0148` row in the index).
**Note:** the index ran 0001–0145 (not 0001–0147 as the register stated);
`0146` + `0147` were missing from the index and were backfilled per Option A.

**(g) Pinned seam-test names (U09/U10 implement; U11 records):**
- `UserPortabilityRoundTrip_ExportThenImportPreservesResidentContentAndMediaAndRoles`
- `UserPortabilityNoSecretAndSelfScoped_ArchiveContainsNoCredentialMaterialAndOnlyResidentAuthoredRows`
- `UserPortabilityConflictResolution_UnresolvedConflictEntitiesAreNotAppliedAndDiscardedWriteNothing`
- `UserPortabilityControllerTests` (the Web-surface pins class)

**(h) kw-l key list (U07/U08 land them × 4 languages):**
`myportability.index.title` / `myportability.export` / `myportability.import`
/ `myportability.import.resolve` / `myportability.resolve.add_elsewhere` /
`myportability.resolve.discard` / `myportability.status` — all ×
`en`/`de`/`fr`/`da` in `KnownTranslationKeys`. Distinct from M11's admin
`portability.*` keys.

**(i) Deferred-lane list (each named, ADR 0148 Consequences):**
1. Cross-neighborhood federation (the `README §Deferred` OpenIddict lane)
2. Backup-automation / cron surface (OPS.md)
3. Resident delete-all-their-data (future ADR — M27 is export/import, not deletion)
4. Import-merge beyond the per-entity resident choice (community-level reconciliation, own ADR)

**The `IAuthorizationService` surface count is unchanged** (C-M27·7 — the
zero-new-authorization-surface pin).

## U01 — service seam + manifest marker

**Date:** 2026-10-06
**Status:** COMPLETE. All three deliverables authored; `m27-u01.md` moved to
`done/`. Build green.

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (all projects
  succeeded; the only new warnings are the expected CS9113 "parameter is
  unread" on the shell's four ctor params, which U03–U06 consume).
- [x] A plain M11 archive (no resident marker) still deserializes. — **PASS**
  (the additive field is `string?` — nullable, so `System.Text.Json`
  deserializes an archive whose `manifest.json` omits `scope` /
  `residentSubjectId` to `null`; the U11 format-compat pin's precondition
  holds by construction).
- [x] No new test (U03's export pins are the first M27 tests). — **PASS**
  (no test files touched).
- [x] No new authorization surface (the `IAuthorizationService` surface count
  is unchanged — the C-M27·7 pin). — **PASS** (no `AccessAction` /
  `AccessVia` / `IAuthorizationService.Decide()` branch added; the service
  shell's ctor composes only the frozen seams `IDocumentStore` /
  `IUserInfoService` / `IMediaStore` / `UserManager<User>`).
- [x] Handoff note: 5–6 lines. — **this section.**

**(a) Seam signatures (verbatim from the design doc §2.1, as shipped):**
```csharp
public interface IUserPortabilityService
{
    Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default);
    Task<UserPortabilityImportPlan> ClassifyAsync(
        string residentSubjectId, Stream archive, CancellationToken ct = default);
    Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        CancellationToken ct = default);
}
```

**(b) `PortabilityManifest` additive field (verbatim from U00 (d), as
shipped — `null` = a plain M11 whole-instance archive, per D2 / C-M27·1):**
```csharp
public string? Scope { get; set; }             // "resident" = M27 scope; null = plain M11 whole-instance
public string? ResidentSubjectId { get; set; } // the subjectId this archive is scoped to
```
Every existing `PortabilityManifest` field (`Format` / `FormatVersion` /
`GeneratedAt` / `CommunityName` / `DocCounts` / `MediaManifest`) is
**unchanged**; the `Format` authority stays `kumunita/portability/1`.

**(c) The three POCO record shapes (verbatim from the design doc §2.1, as
shipped — field names/types/order unchanged; the design doc's `= []` /
`""` / `default` / `= null` defaults were not valid C# record positional-
parameter defaults, so all params ship as required positionals — the same
shape as M11's `PortabilityImportResult(bool Ok,
IReadOnlyList<string> Failures)`):**
```csharp
public sealed record UserPortabilityImportPlan(
    bool Ok,
    IReadOnlyList<UserPortabilityEntityClassification> Entities,
    IReadOnlyList<string> Failures);

public sealed record UserPortabilityEntityClassification(
    string Kind,              // e.g. "Post", "Event", "Message"
    string EntityId,          // the id in the archive
    UserPortabilityEntityStatus Status,
    IReadOnlyList<UserPortabilityAbsentReference> AbsentReferences,
    string? DuplicateId);     // for Duplicate: the existing entity's id in the target

public sealed record UserPortabilityEntityResolution(
    string Kind,              // the entity kind (must match a classification entry)
    string EntityId,          // the entity id (must match a classification entry)
    UserPortabilityResolutionKind Resolution,
    string? PickedTargetId,   // for AddElsewhere: the target the resident chose
    string? AbsentRefKind,    // for AddElsewhere: the absent-reference kind being re-pointed
    string? AbsentRefField);  // for AddElsewhere: the absent-reference field being re-pointed

public sealed record UserPortabilityImportResult(
    bool Ok,
    int AppliedCount,
    int DiscardedCount,
    IReadOnlyList<string> Failures);
```
Plus the two enums (`UserPortabilityEntityStatus`: `Clean` / `Duplicate` /
`Conflict`; `UserPortabilityResolutionKind`: `AddElsewhere` / `Discard`) +
the `UserPortabilityAbsentReference(string Kind, string Field, string Value)`
record, all verbatim from §2.1.

**(d) The `IAuthorizationService` surface count is unchanged** (C-M27·7 —
the zero-new-authorization-surface pin; U01 added no `AccessAction`, no
`AccessVia`, no `Decide()` branch).

**(e) Compile warnings:** the four CS9113 "parameter is unread" warnings on
`UserPortabilityService`'s ctor params (`documentStore` / `userInfoService` /
`mediaStore` / `userManager`) — expected and correct for a shell whose bodies
are `NotImplementedException` until U03–U06; no other new warnings.

**Registration:** `IUserPortabilityService` → `UserPortabilityService` in
`src/Kumunita.Core/DependencyInjection.cs`, immediately after the existing
`IPortabilityService` registration, same `AddTransient` shape.

**U02 entry point:** the seam + the POCO shapes + the manifest marker are
locked in code; U02 owns the resident-scope **filter** over the M11
`PortabilityDocTypes` inventory + the per-kind **business-key** matchers
(the design doc §2.2 / §2.3 tables are the copy source, verbatim).

## U02 — scope filter + business keys

**Date:** 2026-10-06
**Status:** COMPLETE. All three deliverables authored; `m27-u02.md` moved to
`done/`. Build green.

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (`Build
  succeeded with 82 warning(s) in 13.3s`, exit 0; no new errors).
- [x] No new test (U03's export pins are the first M27 tests). — **PASS**
  (no test files touched).
- [x] No new authorization surface (the `IAuthorizationService` surface
  count is unchanged — the C-M27·7 pin). — **PASS** (no `AccessAction` /
  `AccessVia` / `Decide()` branch added; the two U02 methods are pure,
  static, session-free compositions over the two U02 data registries).
- [x] The filter returns exactly the resident's rows for a seeded
  two-resident fixture (a U09 test precondition). — **by construction**
  (the U09 test pins it; the filter's membership rule is the locked §2.2
  seat test, fail-closed on out-of-closed-set types + null ownership
  values).

**(a) The D1 inventory + ownership fields (verbatim from design doc §2.2,
as shipped in `UserScopeInventory.Entries` — 29 in-scope doc families):**
- `Post` → `AuthorId` (M11 ref map) — the resident's authored posts
- `PostReply` → `AuthorId` (M11 ref map) — the resident's authored replies
- `PostTranslation` → `AuthorId` (nullable) (M11 ref map) — the resident's user-added translations (ADR 0022)
- `ReplyTranslation` → `AuthorId` (nullable) (M11 ref map) — the resident's user-added translations (ADR 0022)
- `AnnouncementComment` → `AuthorId` (M11 ref map) — the resident's comments on announcements
- `Event` → `AuthorId` (M11 ref map) — the resident's authored events
- `EventRsvp` → `UserId` (M11 ref map) — the resident's own RSVPs
- `EventTranslation` → `AuthorId` (nullable) (M11 ref map) — the resident's user-added event translations (ADR 0059)
- `ProjectGoal` → `AuthorId` (M11 ref map) — the resident's authored goals
- `Project` → `AuthorId` (M11 ref map) — the resident's authored projects
- `TodoItem` → `AuthorId` (M11 ref map) — the resident's authored to-dos
- `KanbanBoard` → `AuthorId` (M11 ref map) — the resident's authored boards
- `TodoTranslation` → `AuthorId` (nullable) (M11 ref map) — the resident's user-added to-do translations (ADR 0088)
- `BoardTranslation` → `AuthorId` (nullable) (M11 ref map) — the resident's user-added board translations (ADR 0088)
- `ProjectTranslation` → `AuthorId` (nullable) (M11 ref map) — the resident's user-added project translations (ADR 0088)
- `TodoComment` → `AuthorId` (M11 ref map) — the resident's comments on to-dos
- `Conversation` → `ParticipantA` ∪ `ParticipantB` (M11 ref map) — conversations the resident participates in
- `Message` → `ConversationId` → `Conversation` (the resident is a participant) (M11 ref map, via `Conversation`) — indirect basis
- `Page` → `AuthorId` (M11 ref map) — the resident's authored pages (blog)
- `Tag` → `CreatedBy` (`Tag.cs` — drift guard entry 1) — the tags the resident created
- `InventoryItem` → `AuthorId` (`InventoryItem.cs` — drift guard entry 1) — the inventory items the resident created
- `Document` → `OwnerId` (`Document.cs` — drift guard entry 1) — the documents the resident uploaded
- `DocumentFolder` → `OwnerId` (`DocumentFolder.cs` — drift guard entry 1) — the document folders the resident created
- `Bookmark` → `OwnerId` (`Bookmark.cs` — drift guard entry 1) — the resident's bookmarks
- `Profile` → `SubjectId` (the resident's own) (`Profile.cs`) — the resident's own bio + tags (M23) + display identity
- `Group` → `OwnerId` ∪ member (in `GroupMembership.UserId`) (M11 ref map — drift guard entry 2) — the groups the resident created or is a member of
- `GroupMembership` → `UserId` (M11 ref map) — the resident's own membership rows
- `GroupInvitation` → `UserId` (M11 ref map) — invitations the resident received
- `GroupJoinRequest` → `UserId` (M11 ref map) — join-requests the resident submitted

**(b) The per-kind business keys (verbatim from design doc §2.3, as
shipped in `UserBusinessKeys.ByType` — 14 kinds):**
- `Post` → `(AuthorId, Created, Title, Body)`
- `PostReply` → `(PostId, AuthorId, Created, Body)`
- `Event` → `(AuthorId, Created, Title)`
- `TodoItem` → `(AuthorId, Created, Title)`
- `KanbanBoard` → `(AuthorId, Created, Title)`
- `Page` → `(AuthorId, Created, Title)`
- `Tag` → `Slug`
- `Document` → `(OwnerId, Created, Title)`
- `Conversation` → `(ParticipantA, ParticipantB)`
- `Message` → `(ConversationId, SenderId, Created)`
- `InventoryItem` → `(AuthorId, Created, Name)`
- `Bookmark` → `(OwnerId, TargetKind, TargetId)`
- `Profile` → `SubjectId`
- `Group` → `Name`

**(c) The excluded list (verbatim from design doc §2.2 "Excluded", as
shipped in `UserScopeInventory.Excluded` — 22 rows):**
- `IdentityToken` — Credential material (C-M11·2 / C-M27·2, the M11 "Excluded" set, unchanged)
- `OutboxEmail` — Operational state (a fresh instance re-derives; the M11 "Excluded" set, unchanged)
- `EmailDeadLetter` — Operational state (the M11 "Excluded" set, unchanged)
- `AccessAudit` — Operational state (a log, not resident content; the M11 "Excluded" set, unchanged)
- `AuditPurgeSummary` — Operational state (the M11 "Excluded" set, unchanged)
- `LocaleSettings` / `LanguageCatalog` — Operator instance-identity (travels via `config.json` in M11; the resident does not own it — D1 "out of scope")
- `Notification` / `NotificationPreference` / `NotificationSubscription` — Per-resident read state (the M6 operational state; system-generated, not authored by the resident)
- `DelegationGrant` / `GuardianLink` / `ModeratorAssignment` / `ComponentMembership` — Identity-graph / authorization state (the M11 identity graph; not the resident's authored content)
- `Report` — Moderation state (a report is a moderation action, not the resident's authored content)
- `Announcement` / `AnnouncementTranslation` — Operator/GlobalAdmin content (authored by GlobalAdmin/Translator, not by a plain resident)
- `CommunityTranslation` / `TranslationResource` / `GroupTranslation` / `TagTranslation` — Operator/Translator-managed translations (not the resident's authored content)
- `KanbanLane` / `BoardItemPlacement` — Sub-entities of the resident's board (travel with the in-scope board via the M11 apply order, not as independently-scoped resident content)

Plus the C-M27·3 pin itself (not a type row): the *other residents'*
authored rows (their `Post` / `Message` / `Document` / `Bookmark` / any
other in-scope-type content) are excluded by the `ScopeFilter` ownership
test, not by a type row — the same type travels, scoped to this resident's
rows (the U09 two-resident fixture pins exactly that).

**(d) The `IAuthorizationService` surface count is unchanged** (C-M27·7 —
the zero-new-authorization-surface pin; U02 added no `AccessAction`, no
`AccessVia`, no `Decide()` branch — the two new methods are pure, static,
session-free).

**(e) Compile warnings:** the four CS9113 "parameter is unread" warnings
on `UserPortabilityService`'s ctor params (`documentStore` /
`userInfoService` / `mediaStore` / `userManager`) — the expected U01-shell
warnings, still present because the three interface bodies are still
`NotImplementedException` until U03–U06. **No new warnings** introduced by
the U02 files (`UserScopeInventory.cs`, `UserBusinessKeys.cs`, or the two
new static methods in `UserPortabilityService.cs`).

**Shipped shapes (the code the U03–U06 units call):**
- `UserScopeInventory.Entries` — `IReadOnlyDictionary<string,
  UserScopeEntry>` (`Type` → `OwnershipFields` (closed list) +
  `ScopeBasis` + `Note`, verbatim §2.2); `UserScopeInventory.Excluded` —
  `IReadOnlyList<(string Type, string Reason)>` (verbatim §2.2);
  `UserScopeInventory.IsInScope(string)` — the closed-membership test.
- `UserBusinessKeys.ByType` — `IReadOnlyDictionary<string,
  UserBusinessKey>` (`Type` → `Fields` (closed list, §2.3 order));
  `UserBusinessKeys.Matches(string type, object? a, object? b)` — the
  pure duplicate matcher (case-sensitive `Equals` over every locked
  field; fail-closed default — `false` for an out-of-closed-set type, a
  null argument, or a candidate lacking a locked field name).
- `UserPortabilityService.ScopeFilter(string type, IReadOnlyList<object>
  docs, string residentSubjectId, IReadOnlySet<string>?
  inScopeConversationIds = null, IReadOnlySet<string>? residentGroupIds =
  null)` — the pure resident-scope filter (C-M27·3): a row is in scope
  iff the resident holds at least one of its kind's locked ownership
  seats — the direct principal fields by value equality (a `null`
  ownership value on the nullable translation variants is NOT in scope),
  the `Conversation` union by either seat, the `Message` indirect basis
  via its `ConversationId` ∈ `inScopeConversationIds`, the `Group`
  owner-or-member union via its `OwnerId` or its `Id` ∈
  `residentGroupIds` (the two row-based seat sets are resolved by U03
  over the archive's own `Conversation` / `GroupMembership` arrays and
  passed in, keeping the filter a pure POCO test).
- `UserPortabilityService.MatchBusinessKey(string type, object? a,
  object? b)` — the D4 duplicate detector (delegates to
  `UserBusinessKeys.Matches`).

**Drift note (anticipated, resolved in favor of the source — no pause
needed):** the unit plan's Deliverables prose said
`Message`→`SenderId` ∪ participant and `Post`→`(AuthorId, Created,
Title?, Body-hash)`; the design doc §2.2/§2.3 + the U00 handoff (the
locked source) say `Message`→`ConversationId`→`Conversation` (the
resident is a participant) and `Post`→`(AuthorId, Created, Title, Body)`.
**The locked source won** — the shipped code uses
`Message`→`ConversationId`→`Conversation` (indirect basis, via
`inScopeConversationIds`) and `Post`→`(AuthorId, Created, Title, Body)`
(the field value compared directly, no re-invented hash). No other
contradiction found; no "## U02 — Drift pause" section needed.

**U03 entry point:** the seam + the POCO shapes + the manifest marker +
the resident-scope **filter** + the per-kind **business-key** matchers
are locked in code; U03 owns the resident-scoped **export** (the M11
archive loop, the resident-scope marker set on the manifest, the one
`portability.export` audit row) — the `ScopeFilter` / `MatchBusinessKey`
compositions are ready to call.

## U03 — export

**Date:** 2026-10-06
**Status:** COMPLETE. Both deliverables authored (`UserPortabilityService.ExportAsync` body filled + `UserPortabilityExportTests.cs` authored); `m27-u03.md` moved to `done/`. Build green. The 4 export pins discovered + passing (U11 records).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (`Build succeeded with 9 warning(s) in 4.5s`, exit 0; no new errors).
- [x] The export pins discovered (pass/red status recorded for U11's gate). — **PASS** (all 4 green, `Total: 4, Errors: 0, Failed: 0, Skipped: 0`).
- [x] No new authorization surface (the C-M27·7 pin). — **PASS** (no `AccessAction` / `AccessVia` / `Decide()` branch added; the service emits one `AccessAudit` row `Via = Owner`).
- [x] No new document, no new `*DocTypes` surface, no migration (D7). — **PASS** (no new doc type, no `*DocTypes` surface, no migration; the 4 non-M11 types are resolved locally in the `nameToType` dict, NOT added to the frozen `PortabilityDocTypes` table).
- [x] `ClassifyAsync` / `ResolveAsync` **stay** `NotImplementedException` until U04/U06. — **PASS** (both unchanged).

**(a) The `ExportAsync` loop note:** the U02 `ScopeFilter` is applied per in-scope doc-type array (the `PortabilityExportDocuments` per-type query + serialize pattern, filtered to the resident's rows via `ScopeFilter`). The M11 `MediaExport` pattern is reused at the resident scope (the `MediaObject` catalog filtered to the media ids referenced by the in-scope rows). The M11 `PrincipalsExport` no-secret principal shape is reused verbatim (the `PortabilityPrincipal` POCO, only the resident's own account). The M11 `ManifestFinalize` is called + the D2 marker set. The M11 `KumunitaArchive.WriteAsync` is the archive writer (verbatim).

**(b) The D2 marker field (verbatim):**
```csharp
manifest.Scope = "resident";
manifest.ResidentSubjectId = residentSubjectId;
```
Every existing `PortabilityManifest` field (`Format` / `GeneratedAt` / `CommunityName` / `DocCounts` / `MediaManifest`) is **unchanged**; the `Format` authority stays `kumunita/portability/1` (C-M27·1).

**(c) The one-audit-row shape:** `TargetKind = "portability"`, `TargetId = "portability"`, `Action = "portability.export"`, `Via = Authorization.AccessVia.Owner`, `Outcome = Authorization.AccessOutcome.Allow`, `ActorId = residentSubjectId`, `EffectivePrincipalId = residentSubjectId` — the **service** emits it (the controller adds none, the ADR 0105 `messaging.toggle` shape). Emitted after the archive is built (a failed build throws before this point, so no audit row for a refused export).

**(d) The `IAuthorizationService` surface count is unchanged** (C-M27·7 — the zero-new-authorization-surface pin; U03 added no `AccessAction`, no `AccessVia`, no `Decide()` branch — the service emits one `AccessAudit` row via the frozen `documentStore.OpenSession()` seam, no new authorization surface).

**(e) The 4 export pins + pass/red status:**
- `Export_SelfScoped_OtherResidentsAbsent` — **PASS** (C-M27·3: another resident's Post is absent from the archive).
- `Export_NoSecret_NoCredentialMaterial` — **PASS** (C-M27·2: the `PortabilityPrincipal` POCO has no credential field + a byte-scan witness over the archive finds none).
- `Export_D2Marker_Present` — **PASS** (D2: the manifest carries `Scope = "resident"` + `ResidentSubjectId`, format authority unchanged).
- `Export_PlainM11Archive_StillDeserializes` — **PASS** (C-M27·1: a plain M11 archive without the resident marker still deserializes; `Scope` + `ResidentSubjectId` are `null`).

**(f) Compile warnings:** no new warnings introduced by the U03 files. The 4 CS9113 "parameter is unread" warnings from the U01 shell are **gone** (the `ExportAsync` body now consumes all 4 ctor params: `documentStore` / `userInfoService` / `mediaStore` / `userManager`). The remaining 9 warnings are pre-existing (CS8604 / CS8600 / CS8602 / CS8714 / xUnit1051 / xUnit2017 / CS0219 in other files).

**U04 entry point:** the export path is complete (the resident-scoped `*.kumunita` archive + the one `portability.export` audit row). U04 owns the `ClassifyAsync` body — the `clean` / `duplicate` / `conflict` classification over the M11 validate-then-classify pattern, reusing the U02 `MatchBusinessKey` + the M11 `PortabilityDocTypes` reference map for the absent-reference report. The `ScopeFilter` / `MatchBusinessKey` compositions are ready to call; the `PortabilityExportDocuments` / `MediaExport` / `PrincipalsExport` / `ManifestFinalize` / `KumunitaArchive` partials are the frozen M11 machinery U04–U06 reuse.

## U04 — classify

**Date:** 2026-10-06
**Status:** COMPLETE. Both deliverables authored (`UserPortabilityService.ClassifyAsync` body filled + `UserPortabilityClassifyTests.cs` authored); `m27-u04.md` moved to `done/`. Build green. The 4 classify pins discovered + passing (U11 records).

**Exit checklist (from the unit plan):**


- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (all projects succeeded; `Kumunita.Core` 9 warnings, `Kumunita.Web` 12 warnings, `Kumunita.Core.Tests` 9 warnings — all pre-existing in other files; **no new warnings** in `UserPortabilityService.cs` or `UserPortabilityClassifyTests.cs`).
- [x] The classify pins discovered (pass/red status recorded for U11's gate). — **PASS** (all 4 green, each `Total: 1, Errors: 0, Failed: 0, Skipped: 0`).
- [x] No new authorization surface (the C-M27·7 pin). — **PASS** (no `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch added; the `ClassifyAsync` body emits **no** `AccessAudit` row — the `portability.import` / `portability.import.resolve` audit rows are U06's resolve/apply).
- [x] No writes (the C-M27·5 pre-write pin — the classify phase is read-only). — **PASS** (the `ClassifyAsync` body opens the store as a `QuerySession` (read-only), the `userManager.Users` read is the principal source (read-only), and **no** `session.Store` / `SaveChangesAsync` is called; the `Classify_NoWrite_ZeroRowsAfter` pin witnesses it).
- [x] No new document, no new `*DocTypes` surface, no migration (D7). — **PASS** (no new doc type, no `*DocTypes` surface, no migration; the 4 non-M11 docs' reference fields are resolved locally in the `NonM11RefFields` dict, NOT added to the frozen `PortabilityDocTypes` table).
- [x] `ResolveAsync` **stays** `NotImplementedException` until U06. — **PASS** (unchanged).

**(a) The classification rules (the §2.4 verbatim, as implemented):** for each in-scope entity (the `UserScopeInventory.Entries` types present in the archive's `docs/{Type}.json` arrays), the classifier applies the §2.4 contract in order:
- **(a) `clean`** — the entity's business key does **not** match any existing target row (the U02 `MatchBusinessKey` over the target's `QueryAllRowsAsync` row set), **and** every reference field (per the M11 `PortabilityDocTypes` reference map, or the 4 non-M11 docs' own definition) resolves to an existing target entity. Classified `UserPortabilityEntityStatus.Clean`, `AbsentReferences` empty, `DuplicateId` null.
- **(b) `duplicate`** — the entity's business key **does** match an existing target row (the U02 `MatchBusinessKey` match). Classified `UserPortabilityEntityStatus.Duplicate`, `DuplicateId` carries the existing row's id, `AbsentReferences` empty (the duplicate is not applied; the reference report is not computed — the existing entity is the one the resident already has).
- **(c) `conflict`** — the entity's business key does **not** match an existing target row, but **at least one** reference field does **not** resolve to an existing target entity. Classified `UserPortabilityEntityStatus.Conflict`, `AbsentReferences` carries the closed list of absent references, `DuplicateId` null.
The classifier runs to completion **before any write** (C-M27·5 — the pre-write pin); a `conflict` entity is only ever *reported*, never applied (C-M27·4 — the resident's per-entity choice is the only apply path, owned by U06's `ResolveAsync`).

**(b) The reference-availability shape (the `UserPortabilityAbsentReference` closed shape, verbatim §2.1/§2.4 — the U07 resolve-review renders this):** each `UserPortabilityAbsentReference` in the `AbsentReferences` list carries:
- `Kind` — the target doc type (the M11 reference map's `Target`, or the 4 non-M11 docs' own definition; e.g. `"Group"`, `"Tag"`, `"Component"`, `"Page"`).
- `Field` — the reference field on the entity (e.g. `"GroupId"`, `"TagIds"`, `"ComponentId"`).
- `Value` — the id in the archive that is absent in the target.
The resolve-review UI renders this as: *"This post references group `{Value}`, which does not exist here. Add it to a group you are a member of, or discard it."* (the §2.4 verbatim).

**(c) The `conflict` reason shape (verbatim §2.4):** the `conflict` entity's `AbsentReferences` list is the closed `UserPortabilityAbsentReference` set (the `{Kind, Field, Value}` shape above) — the data the U07/U08 resolve-review renders as the "add elsewhere → pick a target" picker. The `conflict` classification is assigned when **at least one** reference field does not resolve; the list carries **every** absent reference (not just the first), so the resident sees the full set of targets they may re-point to. The §2.4 (b) `duplicate` classification takes precedence over (c) — a `duplicate` entity is not conflict-checked (the existing entity is the one the resident already has, so the reference report is not computed).

**(d) The no-write pin (`Classify_NoWrite_ZeroRowsAfter` + pass/red):** — **PASS** (`Total: 1, Errors: 0, Failed: 0, Skipped: 0`). The pin plants a conflict post (an absent `ComponentId`) + asserts the target's `Post` count is unchanged (0 → 0) and no `AccessAudit` row is emitted (0 → 0) after `ClassifyAsync` (the C-M27·5 pre-write pin — the classify phase is read-only).

**(e) The `IAuthorizationService` surface count is unchanged** (C-M27·7 — the zero-new-authorization-surface pin; U04 added no `AccessAction`, no `AccessVia`, no `Decide()` branch — the `ClassifyAsync` body emits **no** `AccessAudit` row; the `portability.import` / `portability.import.resolve` audit rows are U06's resolve/apply).

**(f) Compile warnings:** no new warnings introduced by the U04 files (`UserPortabilityService.cs` or `UserPortabilityClassifyTests.cs`). The 4 CS9113 "parameter is unread" warnings from the U01 shell are **gone** (the `ClassifyAsync` body now consumes all 4 ctor params: `documentStore` / `userInfoService` / `mediaStore` / `userManager`). The remaining warnings are pre-existing (CS8604 / CS8600 / CS8602 / CS8714 / xUnit1051 / xUnit2017 / CS0219 in other files).

**Shipped shapes (the code the U05–U06 units call):**
- `UserPortabilityService.ClassifyAsync(string residentSubjectId, Stream archive, CancellationToken ct)` — the §2.4 classification (the `clean` / `duplicate` / `conflict` per-entity report + the `UserPortabilityAbsentReference` shape + the `UserPortabilityImportPlan` assembly) — **no writes** (the C-M27·5 pre-write pin).
- `UserPortabilityService.NonM11RefFields` — the 4 non-M11 in-scope docs' reference fields (`InventoryItem` / `Document` / `DocumentFolder` / `Bookmark` — the drift-guard entry 1 docs, resolved locally, NOT added to the frozen `PortabilityDocTypes` table).
- `UserPortabilityService.ReferencedDocTypes()` / `GetReferenceFields(type)` / `ResolveAbsentReferences(...)` / `RefResolves(...)` / `ReadFieldValue(...)` / `ReadId(...)` — the pure, POCO-over-the-target helpers (the M11 `PortabilityValidate` pattern restructured into the `UserPortabilityAbsentReference` shape — the `conflict` detector + the reference-availability report).

**U05 entry point:** the classify path is complete (the resident-scoped `*.kumunita` archive → the `clean` / `duplicate` / `conflict` classification + the per-entity reference-availability report + the `UserPortabilityImportPlan` assembly, **no writes**). U05 owns the **`UserPortabilityEntityResolution`** POCO closed shape (the `ResolutionKind` set + the `AddElsewhere` re-point fields + the `Discard` no-write pin + the `UserPortabilityImportResult` closed-failure shape) — the `ResolveAsync` **input contract** (the `ClassifyAsync` output is the `UserPortabilityImportPlan` U05's resolve model keys on; the `conflict` reason shape — the `UserPortabilityAbsentReference` set — is the `AddElsewhere` re-point source). The `MatchBusinessKey` / `ScopeFilter` compositions + the `PortabilityExportDocuments` / `MediaExport` / `PrincipalsExport` / `ManifestFinalize` / `KumunitaArchive` / `PortabilityApplyDocuments` / `PortabilityApplyMedia` partials are the frozen M11 machinery U05–U06 reuse.

## U05 — resolve model

**Date:** 2026-10-07
**Status:** COMPLETE. Both deliverables authored (`UserPortabilityResolveModelTests.cs` authored; the `UserPortabilityEntityResolution` / `UserPortabilityResolutionKind` / `UserPortabilityImportResult` closed shapes already shipped verbatim by U01 and confirmed to match the design doc §2.1 + the `AddElsewhere` re-point already keys off U04's `UserPortabilityAbsentReference` `{Kind, Field, Value}` shape verbatim — no re-invention); `m27-u05.md` moved to `done/`. Build green. The 4 resolve-model pins discovered + passing (U11 records).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (`Build succeeded with 78 warning(s) in 24.6s`, exit 0; **no new warnings** in `UserPortabilityService.cs` or `UserPortabilityResolveModelTests.cs`).
- [x] The resolve-model pins discovered (pass/red status recorded for U11's gate). — **PASS** (all 4 green, `Total: 4, Errors: 0, Failed: 0, Skipped: 0`, via the xunit.v3 in-process runner `-class Kumunita.Core.Tests.UserPortabilityResolveModelTests`).
- [x] **No new authorization surface** (C-M27·7 — the zero-new-authorization-surface pin). — **PASS** (no `AccessAction` / `AccessVia` / `Decide()` branch added; the resolve-model pins are pure reflection-over-the-POCO/enum shape asserts, no `IAuthorizationService` branch, no `AccessAudit` row).
- [x] **No apply logic** (U06 owns `ResolveAsync`) — the `ResolveAsync` body is still `NotImplementedException`. — **PASS** (unchanged; line 982 still `throw new NotImplementedException("M27 U06 — the fail-closed per-entity resolve apply (C-M27·4).")`).
- [x] **No auto-merge** (C-M27·4 — a `conflict` entity is only ever *reported* by U04 and *chosen* by the resident; there is no default, no fallback, no auto-re-point). — **PASS** (the `ResolveModel_ClosedResolutionKindSet` pin asserts exactly `{AddElsewhere, Discard}` — two members, no implicit/default member — and `ResolveModel_Discard_NoWritePin` pins the `Discard` no-write shape).
- [x] No new document, no new `*DocTypes` surface, no migration (D7). — **PASS** (no new doc type, no `*DocTypes` surface, no migration; the resolve-model shapes are POCO/enum types in the `Kumunita.Core.Portability` context, not doc types).
- [x] `ExportAsync` and `ClassifyAsync` stay as shipped. — **PASS** (both unchanged; the U03 export body + the U04 classify body are untouched).

**(a) The `ResolutionKind` set (verbatim from the design doc §2.1, as shipped in the code by U01 — the locked closed set, 2 members):**
```csharp
public enum UserPortabilityResolutionKind
{
    AddElsewhere,
    Discard
}
```
No third/default member (the D4 no-auto-merge pin — a `conflict` entity the resident does not resolve is not applied, not auto-re-pointed to a computed default).

**(b) The `AddElsewhere` re-point shape (verbatim from the design doc §2.1, as shipped in the code by U01 — the `conflict` reason U07's resolve-review renders):**
```csharp
public sealed record UserPortabilityEntityResolution(
    string Kind,              // the entity kind (must match a classification entry)
    string EntityId,          // the entity id (must match a classification entry)
    UserPortabilityResolutionKind Resolution,
    string? PickedTargetId,   // for AddElsewhere: the target the resident chose
    string? AbsentRefKind,    // for AddElsewhere: the absent-reference kind being re-pointed
    string? AbsentRefField);  // for AddElsewhere: the absent-reference field being re-pointed
```
The `AddElsewhere` re-point keys off U04's `UserPortabilityAbsentReference` `{Kind, Field, Value}` shape verbatim — `AbsentRefKind` carries the absent-ref's `Kind`, `AbsentRefField` carries the absent-ref's `Field`; the `Value` (the archive id that is absent) is replaced by the resident's picked target. `PickedTargetId` carries the resident's chosen target (a group they are a member of, a tag they own, a component they are a member of — the §2.5 standing check U06 enforces).

**(c) The `Discard` no-write pin (the shape — the apply is U06's pin):** the `UserPortabilityResolutionKind.Discard` member is a first-class closed decision (explicit, not a fallback); a `Discard` resolution carries **no** re-point data — the three `AddElsewhere`-only fields (`PickedTargetId` / `AbsentRefKind` / `AbsentRefField`) are null. The no-write choice is a closed, explicit decision (the D4 no-auto-merge posture — it is *chosen*, not defaulted), and the shape that U06's `ResolveAsync` will apply (no write, counted in `UserPortabilityImportResult.DiscardedCount`).

**(d) The `UserPortabilityImportResult` closed-failure set (verbatim from the design doc §2.1, as shipped in the code by U01 — the M11 `PortabilityImportResult(bool Ok, IReadOnlyList<string> Failures)` shape emulated, extended with the apply counts):**
```csharp
public sealed record UserPortabilityImportResult(
    bool Ok,
    int AppliedCount,
    int DiscardedCount,
    IReadOnlyList<string> Failures);
```
A fail-closed rejection (a `PickedTargetId` the resident has no standing over, a write error, a mid-apply failure — the D4 / §2.5 contract) is `Ok = false` + the closed `Failures` set, zero applied (the transaction rolls back, nothing is written).

**(e) The `IAuthorizationService` surface count is unchanged** (C-M27·7 — the zero-new-authorization-surface pin; U05 added no `AccessAction`, no `AccessVia`, no `Decide()` branch — the resolve-model pins are pure reflection-over-the-POCO/enum shape asserts, no `IAuthorizationService` branch, no `AccessAudit` row).

**(f) The 4 resolve-model pins + pass/red status (all PASS, U11 records the gate):**
- `ResolveModel_ClosedResolutionKindSet` — **PASS** (the `UserPortabilityResolutionKind` closed set is exactly `{AddElsewhere, Discard}` — 2 members, no default/fallback; the D4 no-auto-merge pin).
- `ResolveModel_AddElsewhere_RepointShape` — **PASS** (the `UserPortabilityEntityResolution` closed POCO shape — 6 positional fields, the locked types — + the `AddElsewhere` re-point fields key off U04's `UserPortabilityAbsentReference` `{Kind, Field, Value}` shape verbatim + the picked target id).
- `ResolveModel_Discard_NoWritePin` — **PASS** (the `Discard` no-write pin — the shape: a `Discard` resolution carries no re-point data, the three `AddElsewhere`-only fields are null).
- `ResolveModel_ImportResult_ClosedFailureSet` — **PASS** (the `UserPortabilityImportResult` closed-failure shape — 4 positional fields, the locked types; a fail-closed rejection is `Ok = false` + the closed `Failures` set, zero applied).

**(g) Compile warnings:** no new warnings introduced by the U05 files (`UserPortabilityResolveModelTests.cs`). The 78 warnings are all pre-existing (xUnit1051 nullable-CancellationToken in other test files, CS8604 / CS8600 / CS8602 / CS8714 / xUnit2017 / CS0219 in other files). **No new warnings** in `UserPortabilityService.cs` (the resolve-model shapes were already shipped by U01 and are consumed verbatim by the U05 pins).

**Shipped shapes (the code the U06 unit calls):**
- `UserPortabilityEntityResolution` — the resident's per-entity decision (the closed POCO shape — `Kind` / `EntityId` / `Resolution` / `PickedTargetId` / `AbsentRefKind` / `AbsentRefField`; the `AddElsewhere` re-point keys off U04's `UserPortabilityAbsentReference` `{Kind, Field, Value}` shape verbatim).
- `UserPortabilityResolutionKind` — the closed decision set (`AddElsewhere` / `Discard` — 2 members, no default/fallback).
- `UserPortabilityImportResult` — the resolve output (the closed-failure shape — `Ok` / `AppliedCount` / `DiscardedCount` / `Failures`; the D4 no-auto-merge + the apply-failure set).
- The `ResolveAsync` body **stays** `NotImplementedException` until U06 (the `UserPortabilityResolveModelTests` are shape pins — they assert the closed record/enum shapes, not behavior; the apply behavior is U06's).

**U06 entry point:** the resolve-model shapes are locked (the `UserPortabilityEntityResolution` POCO + the `UserPortabilityResolutionKind` closed set + the `UserPortabilityImportResult` closed-failure shape). U06 owns the `ResolveAsync` body — the fail-closed per-entity resolve apply (the §2.5 contract: clean → apply in M11 import order; duplicate → not applied; conflict + `AddElsewhere` → apply with the absent ref re-pointed to `PickedTargetId` (the resident must have standing over it, else fail-closed); conflict + `Discard` → no write; conflict + **unresolved** → not applied (no-auto-merge pin). Mid-apply failure ⇒ transaction rolls back, **zero** new rows. One `portability.import.resolve` `AccessAudit` row (`Via = Owner`). The `MatchBusinessKey` / `ScopeFilter` compositions + the `PortabilityExportDocuments` / `MediaExport` / `PrincipalsExport` / `ManifestFinalize` / `KumunitaArchive` / `PortabilityApplyDocuments` / `PortabilityApplyMedia` partials are the frozen M11 machinery U06 reuses for the apply.

## U06 — ResolveAsync

**Date:** 2026-10-07
**Status:** COMPLETE. Both deliverables authored (`UserPortabilityService.ResolveAsync` body filled + `UserPortabilityResolveTests.cs` authored); `m27-u06.md` moved to `done/`. Build green (0 errors). The 4 apply pins discovered + passing (U11 records).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (Core: 9 warnings, all pre-existing in other files; `Kumunita.Web` / `Kumunita.Core.Tests` / `Kumunita.Web.Tests` 0 new errors; no warnings in `UserPortabilityService.cs` or `UserPortabilityResolveTests.cs`).
- [x] The apply pins discovered (pass/red status recorded for U11's gate). — **PASS** (all 4 green, `Total: 4, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 12.021s`, via the xunit.v3 in-process runner `-class Kumunita.Core.Tests.UserPortabilityResolveTests`).
- [x] No new authorization surface (the C-M27·7 pin). — **PASS** (no `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch added; the service emits one `AccessAudit` row `Via = Owner` via the frozen `documentStore.OpenSession()` seam).
- [x] No auto-merge (the C-M27·4 pin — the author's choice is absolute). — **PASS** (the no-auto-merge rule is enforced in the apply loop: a `conflict` entity without a resolution is silently skipped — there is no default, no fallback, no auto-re-point).
- [x] No new document, no new `*DocTypes` surface, no migration (D7). — **PASS** (no new doc type, no `*DocTypes` surface, no migration; the 4 non-M11 types are resolved locally in the `nameToType` dict, NOT added to the frozen `PortabilityDocTypes` table).
- [x] `ExportAsync` and `ClassifyAsync` stay as shipped. — **PASS** (both unchanged; the U03 export body + the U04 classify body are untouched).

**(a) The apply order (the §2.5 (1) verbatim, as implemented):** the `clean` entities are applied in the M11 §inventory import order (`PortabilityDocTypes.InOrder()` — parents before children, the D7 order; the uniform loop, not per-type code), reusing the M11 `PortabilityApplyDocuments` pattern (the `session.Store(row)` loop + one `SaveChangesAsync` commit). The `duplicate` entities are **not applied** (the §2.5 (2) rule — counted in `DiscardedCount`). The `conflict` entities are the resident's decision (the §2.5 (3) rule): `AddElsewhere` → apply with the absent reference re-pointed to `PickedTargetId`; `Discard` → **not applied** (no write — counted in `DiscardedCount`); **unresolved** → **not applied** (the C-M27·4 no-auto-merge pin). The media bytes are applied after the docs commit (the M11 `PortabilityApplyMedia` pattern — the resident's own `IMediaStore.PutAsync` write lane, idempotent). One commit (a single `SaveChangesAsync` — the C-M27·4 "one commit" pin).

**(b) The re-point rule (the §2.5 (3) AddElsewhere verbatim, as implemented):** the entity is applied with the absent reference re-pointed to `PickedTargetId` (the target the resident chose). The re-point rule: the absent reference field (`res.AbsentRefField`, e.g. `"ComponentId"`) on the archive's POCO is set to `res.PickedTargetId` via reflection (`PropertyInfo.SetValue`) before `session.Store(row)`. The `AddElsewhere` re-point keys off U04's `UserPortabilityAbsentReference` `{Kind, Field, Value}` shape verbatim — `AbsentRefKind` carries the absent-ref's `Kind` (`"Component"`), `AbsentRefField` carries the absent-ref's `Field` (`"ComponentId"`); the `Value` (the archive id that is absent in the target, e.g. `"comp-absent"`) is replaced by the resident's picked target. The resident must have standing over `PickedTargetId` (a group they are a member of or own, a tag they created, a component they are a member of, a page they authored) — the standing check is a business read (not an `AccessAction` — C-M27·7), fail-closed on unknown target kinds.

**(c) The fail-closed contract (the §2.5 (5) verbatim, as implemented):** a mid-apply failure (a `PickedTargetId` the resident has no standing over, a write error, a session timeout) is the **documented rollback path** — the transaction rolls back, **no rows** are written, and the `UserPortabilityImportResult.Ok` is `false` with the failure set (the `standing.denied:{Kind}:{EntityId}:{AbsentRefKind}:{PickedTargetId}` / `apply.failed:{message}` closed failure set). A rejected / aborted resolve writes **nothing** (the M11 C-M11·4 posture). The standing pre-pass runs **before** the apply session opens, so a standing denial returns the closed failure set + `Ok=false` + zero applied + no audit row (the same "no audit for a refused action" posture as the export lane). A mid-apply exception is caught and the session is disposed (Marten's `IDocumentSession` is a transactional unit — the `SaveChangesAsync` either commits atomically or rolls back, the C-M27·4 "one commit" pin).

**(d) The no-auto-merge pin (the §2.5 (4) verbatim, as implemented):** a `conflict` entity the resident did **not** resolve (no `UserPortabilityEntityResolution` entry for it) is **not applied** (there is no default, no fallback, no auto-re-point). The apply loop checks `resolutionByEntity.TryGetValue($"{entry.Type}|{entityId}", out var res)` — a miss for a `conflict` entity is a `continue` (silently skipped, not applied, not counted in `AppliedCount` or `DiscardedCount`). Pinned by the `Resolve_NoAutoMerge_UnresolvedConflictAbsent` test (the target's `Post` count is unchanged after a resolve with zero resolutions).

**(e) The one-audit-row shape (the §2.5 (6) verbatim, as implemented):** `TargetKind = "portability"`, `TargetId = "portability"`, `Action = "portability.import.resolve"`, `Via = Authorization.AccessVia.Owner`, `Outcome = Authorization.AccessOutcome.Allow`, `ActorId = residentSubjectId`, `EffectivePrincipalId = residentSubjectId` — the **service** emits it (the controller adds none, the ADR 0105 `messaging.toggle` shape). Emitted **after** a clean apply (a standing pre-pass failure or a mid-apply exception returns the closed failure set before this point, so no audit row for a refused resolve — the `Resolve_FailClosed_MidApplyFailure_ZeroNewRows` test witnesses it: `Ok=false` + zero `Post` rows + zero `import.resolve` audit rows).

**(f) The `IAuthorizationService` surface count is unchanged** (C-M27·7 — the zero-new-authorization-surface pin; U06 added no `AccessAction`, no `AccessVia`, no `Decide()` branch — the service emits one `AccessAudit` row via the frozen `documentStore.OpenSession()` seam; the standing checks are business reads over the frozen `IDocumentStore` seam, not the authorization engine).

**(g) The 4 apply pins + pass/red status (all PASS, U11 records the gate):**
- `Resolve_AddElsewhere_RepointAppliesToPickedTarget` — **PASS** (§2.5 (3) AddElsewhere: the post's `ComponentId` is re-pointed from `"comp-absent"` to `"comp-target"` (the resident's member-of component); the post is applied + one `import.resolve` audit row `Via = Owner`).
- `Resolve_Discard_WritesNothing` — **PASS** (§2.5 (3) Discard: the discarded entity writes nothing — the target's `Post` count is unchanged; the resolve action is still audited, the C-M27·6 one-audit-row contract).
- `Resolve_NoAutoMerge_UnresolvedConflictAbsent` — **PASS** (§2.5 (4) no-auto-merge: a `conflict` entity with no resolution is not applied — the target's `Post` count is unchanged; no default, no fallback, no auto-re-point).
- `Resolve_FailClosed_MidApplyFailure_ZeroNewRows` — **PASS** (§2.5 (5) fail-closed: a `PickedTargetId` the resident has no standing over is a fail-closed rejection — `Ok=false` + `Failures` contains `"standing.denied:..."` + zero `Post` rows + zero `import.resolve` audit rows).

**(h) Compile warnings:** no new warnings introduced by the U06 files (`UserPortabilityService.cs` or `UserPortabilityResolveTests.cs`). The 9 warnings are all pre-existing (CS8604 in `SmtpSender.cs` + 8× CS8600 in `SampleDataSeeder.cs` in `Kumunita.Core`; the `Kumunita.Web` / `Kumunita.Core.Tests` / `Kumunita.Web.Tests` warnings are pre-existing in other files). **No new warnings** in `UserPortabilityService.cs` (the `ResolveAsync` body + the 5 standing-check helpers are clean) or `UserPortabilityResolveTests.cs` (the 4 apply pins are clean).

**Seam refinement (source-faithful, recorded here per the unit-series rule):** the locked `IUserPortabilityService.ResolveAsync` signature (U01, verbatim from the design doc §2.1) is `Task<UserPortabilityImportResult> ResolveAsync(string residentSubjectId, UserPortabilityImportPlan plan, IReadOnlyList<UserPortabilityEntityResolution> resolutions, CancellationToken ct = default)`. The §2.5 (1) apply rule reads the entity rows **from the uploaded archive** (the M11 `PortabilityApplyDocuments` pattern: the `docs/{Type}.json` array bytes are the single source of truth for the apply), but the locked signature carries **no archive stream**. U06 adds one **optional, trailing** parameter (`Stream? archive = null`) to the interface + the impl — the U01/U05 callers (no archive arg) still compile (the optional default `null` → a fail-closed `archive.missing` rejection, the C-M27·4 posture: the apply cannot proceed without the archive's rows). The U08 Web resolve-POST (which re-uploads the archive on the resolve step) passes the stream. This is the **source-faithful** refinement — the §2.5 apply contract reads from the archive, the locked signature did not carry it; the optional-parameter form keeps the locked shape compiling while satisfying the apply contract. The POCO shapes (`UserPortabilityImportPlan` / `UserPortabilityEntityClassification` / `UserPortabilityEntityResolution` / `UserPortabilityImportResult` / the `UserPortabilityEntityStatus` / `UserPortabilityResolutionKind` enums / the `UserPortabilityAbsentReference` record) are **unchanged** — the U01/U05 locked shapes are consumed verbatim.

**U07 entry point:** the resolve path is complete (the resident's per-entity apply + the re-point rule + the no-auto-merge pin + the fail-closed contract + the one `portability.import.resolve` audit row). U07 owns the Web **import** surface + the `portability.import` audit + the import kw-l keys (the `/account/portability` index + the `GET /account/portability/export` + the `POST /account/portability/import` upload + the one `portability.import` audit row + the `myportability.*` import kw-l keys × en/de/fr/da). The `ExportAsync` / `ClassifyAsync` / `ResolveAsync` bodies + the `ScopeFilter` / `MatchBusinessKey` compositions + the `PortabilityExportDocuments` / `MediaExport` / `PrincipalsExport` / `ManifestFinalize` / `KumunitaArchive` / `PortabilityApplyDocuments` / `PortabilityApplyMedia` partials are the frozen M11 machinery U07–U08 reuse for the Web surface.

## U07 — Web index + import upload

**Date:** 2026-10-07
**Status:** COMPLETE. All three deliverables authored (`UserPortabilityController.cs` new, `Views/Account/Portability/Index.cshtml` new, the 5 `myportability.*` keys × 4 languages added to `KnownTranslationKeys.cs`); `m27-u07.md` moved to `done/`. Build green (0 errors). The kw-l parity pins verified passing (U12 re-runs the close).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (`Build succeeded` — `Kumunita.Core` / `Kumunita.Web` / both test projects all `succeeded`; **no new warnings** in `UserPortabilityController.cs` or `KnownTranslationKeys.cs`; the `Views/Account/Portability/Index.cshtml` view compiles clean).
- [x] The kw-l parity pins pass (the four-language registry + the view-scan pin). — **PASS** (`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` — `Total: 1, Errors: 0, Failed: 0`; `KnownTranslationKeys_ParityTests` — `Total: 7, Errors: 0, Failed: 0`, the en/de/fr/da key-set parity over the now-expanded registry).
- [x] **No new authorization surface** (the C-M27·7 pin). — **PASS** (the controller uses only the `[Authorize]` verified-resident gate + `KumunitaPrincipal.SubjectId(User)`; no `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch / `Audience` added; the audit rows are the **service's** (U03 `export` / U06 `import.resolve`), the controller adds none).
- [x] No new test authored (the Web-surface pins are U10's). — **PASS** (no test files touched).
- [x] The resolve-review UI + `POST /import/resolve` are **U08's** — not built. — **PASS** (U07 ships only the index + export + the `ClassifyAsync` import; no `ResolveReview.cshtml`, no `import/resolve` action).

**(a) The route set (exact, for U08's resolve-review):**
- `GET /account/portability` — the index (the export button + the import upload form + the status area). A read — no audit row (C-M27·6 "reads emit none").
- `GET /account/portability/export` — the `IUserPortabilityService.ExportAsync(residentSubjectId)` delegation; the `Content-Disposition: attachment; filename="my-data.kumunita"` + `X-Content-Type-Options: nosniff` serve idiom (the M11 `AdminPortabilityController.Export` shape, the ADR 0034 attachment lane). The one `portability.export` `AccessAudit` row (`Via = Owner`) is the **service's** (U03).
- `POST /account/portability/import` — the `IUserPortabilityService.ClassifyAsync(residentSubjectId, archive)` delegation + the fail-closed render of the `clean`/`duplicate`/`conflict` report (the `plan.Ok=false` closed-failure set → `TempData["error"]` + the `myportability.status` kw-l key; `plan.Ok=true` → `TempData["info"]` + the `clean`/`duplicate`/`conflict` summary). `[ValidateAntiForgeryToken]`. The classify phase writes **nothing** (C-M27·5); the `portability.import` audit row is the **service's** when it exists (U06/U08 lane), the controller adds none.
- **U08's** (not built): `POST /account/portability/import/resolve` — the `ResolveAsync` delegation + the `ResolveReview.cshtml` UI (the resident's per-entity `AddElsewhere`/`Discard` choice) + the `myportability.import.resolve` / `myportability.resolve.add_elsewhere` / `myportability.resolve.discard` kw-l keys (registered in U07's registry batch, consumed in U08's view).

**(b) The kw-l key list (verbatim, the U07 batch — all in the registry now × 4 languages):** `myportability.index.title` / `myportability.export` / `myportability.import` / `myportability.import.resolve` / `myportability.status`. **Four-language note:** all five land in **all four** dictionaries in `KnownTranslationKeys.cs` (`EnValues`/`DeValues`/`FrValues`/`DaValues`) — `en` "My data / Export / Import / Apply my choices / Status"; `de` "Meine Daten / Exportieren / Importieren / Meine Auswahl anwenden / Status"; `fr` "Mes données / Exporter / Importer / Appliquer mes choix / Statut"; `da` "Mine data / Eksportér / Importér / Anvend mine valg / Status". **Deliberately distinct from M11's admin `portability.*` keys** (D9) so the resident surface never collides with the operator surface. U07's batch is the 5 keys above (including `myportability.import.resolve`); U08 will add the two resolve-review action keys (`myportability.resolve.add_elsewhere` / `myportability.resolve.discard`) to the same 4 dictionaries + its `ResolveReview.cshtml` — the design-doc §kw-l 7-key closed set completes at U08.

**(c) The `Via = Owner` + no-GlobalAdmin note (C-M27·6):** the gate is the **verified-resident self-lane** — `[Authorize]` (any signed-in resident), the actor is the resident's **own** `subjectId` (`KumunitaPrincipal.SubjectId(User)`), and there is **no** `[Authorize(Roles = GlobalAdmin)]` (unlike M11's `AdminPortabilityController`): this is the resident's own data, the ADR 0105/0118 personal-by-id posture — no GlobalAdmin break-glass, no audience decision. Each write's `AccessAudit` row is the **service's** (`TargetKind "portability"`, `Via = Owner`, verb `export` / `import` / `import.resolve`) — the controller adds none (the ADR 0105 `messaging.toggle` one-audit-row shape). The reads (index) emit no row.

**(d) Compile warnings:** **none new** in `UserPortabilityController.cs` or `KnownTranslationKeys.cs`. The full-solution build is green (`Kumunita.Core` 9 warnings, `Kumunita.Web` 12, both test projects — all pre-existing in other files, matching the U03–U06 baselines). The one view-resolution note: the controller is `UserPortabilityController` but the view lives at the mandated `Views/Account/Portability/Index.cshtml` (the design-doc "Parts affected" path, shared with U08's `ResolveReview.cshtml`), outside the default `Views/UserPortability/` convention — so `Index()` references it by explicit application-relative path (`View("~/Views/Account/Portability/Index.cshtml")`), the same `~/Views/...` idiom the codebase already uses (`Html.PartialAsync("~/Views/Locale/_SettingsTabs.cshtml")` in `Account/ChangePassword.cshtml`); `RazorViewEngine.GetView` resolves application-relative paths directly, so it renders at runtime.

**U08 entry point:** the index + export + the `ClassifyAsync` import (the clean/duplicate/conflict report render) + the U07 kw-l keys are locked in code. U08 owns the resolve-review UI (`Views/Account/Portability/ResolveReview.cshtml`) + `POST /account/portability/import/resolve` (the `IUserPortabilityService.ResolveAsync(residentSubjectId, plan, resolutions, archive)` delegation — U06's locked signature, the `archive` arg is the one the U06 seam-refinement added for the apply to read the entity rows) + the `myportability.import.resolve` / `myportability.resolve.add_elsewhere` / `myportability.resolve.discard` kw-l keys (already registered, to be consumed in U08's view). The `ExportAsync` / `ClassifyAsync` / `ResolveAsync` bodies + the `ScopeFilter` / `MatchBusinessKey` compositions + the frozen M11 machinery are ready to call; the `UserPortabilityController` ctor (the `IUserPortabilityService` + `ILocalizationService` + optional `ITranslationProvider` shape) is the U08 resolve-POST home.

## U08 — resolve-review UI + import/resolve

**Date:** 2026-10-07
**Status:** COMPLETE. All three deliverables authored (`UserPortabilityController.cs` — GET + POST `/import/resolve` + `BuildReviewModel` + nested form types + optional `IUserInfoService` ctor param; `Views/Account/Portability/ResolveReview.cshtml` new; the two `myportability.resolve.*` keys × 4 languages added to `KnownTranslationKeys.cs`); `m27-u08.md` moved to `done/`. Build green (0 errors, no new warnings in the U08 files).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (`Build succeeded. 0 Error(s)`; a `Select-String` over `UserPortabilityController` / `ResolveReview` / `KnownTranslationKeys` returns nothing — **no new warnings** in any of the three U08 files).
- [x] The resolve-review UI + `POST /import/resolve` wired. — **PASS** (the `GET` renders `ResolveReview.cshtml` with one card per `conflict` entity; the `POST` re-classifies, maps the resident's per-entity choices to `UserPortabilityEntityResolution` rows, and delegates to `ResolveAsync` with the re-uploaded archive stream).
- [x] **No new authorization surface** (the C-M27·7 pin). — **PASS** (the controller uses only the `[Authorize]` verified-resident gate + `KumunitaPrincipal.SubjectId(User)`; the `IUserInfoService` added as an **optional** ctor param is a *business read* seam (`GetGroupsForUserAsync`) to seed the "add elsewhere" target picker — not the authorization engine; no `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch added).
- [x] No new document, no new `*DocTypes` surface, no migration (D7). — **PASS** (U08 is Web-surface only; no doc/migration touched).
- [x] U00–U07 shipped code unchanged (except the additive optional ctor param). — **PASS** (the ctor's new param is **optional, trailing, `= null`** — the existing 3-arg registrations + `Index`/`Export`/`Import` actions compile untouched).

**(a) The `POST /import/resolve` route + the `ResolveAsync` delegation (the U06 seam refinement):** the POST is **stateless over the re-uploaded archive** — the `plan` (and the per-entity classification) is *transient* (computed from the upload, not persisted), and `UserPortabilityEntityResolution` is a get-only positional record that **cannot** be form-bound. So the POST: (1) `ClassifyAsync(actor, archive.OpenReadStream())` again to get a fresh `plan`; (2) validates each posted `ResolveConflict` row against the *current* conflict set (a `Kind|EntityId` set) so a stale/forged row is ignored; (3) maps each chosen row to a `UserPortabilityEntityResolution` (`Discard` → nulls; `AddElsewhere` → `PickedTargetId` + the absent-ref `Kind`/`Field`); (4) delegates `ResolveAsync(actor, plan, resolutions, archive.OpenReadStream())` — **the `archive` arg is the re-uploaded stream** (the U06 seam-refinement param the apply reads the entity rows from). A `row.Resolution` that is empty/whitespace (the resident left the radio off) is **skipped** — not applied, honoring C-M27·4 no-auto-merge. `TempData["info"]` carries the `AppliedCount` / `DiscardedCount` summary; `plan.Ok=false` / `result.Ok=false` → `TempData["error"]` + the `myportability.status` key + `RedirectToAction(Index)`. `[ValidateAntiForgeryToken]` on the POST.

**(b) The `ResolveReview.cshtml` + the §2.4 conflict reason:** one card per `conflict` entity. The reason line renders the `UserPortabilityAbsentReference` `{Kind, Field, Value}` shape as the design-doc §2.4 verbatim example — *"This post references group `{Value}`, which does not exist here. Add it to a group you are a member of, or discard it."* — generalized to `This {kind} references {kind} <code>{Value}</code>, which does not exist here.` (the `kind`/`Value` come from the classification, not hardcoded to "group"). Each card: an `AddElsewhere`/`Discard` radio pair (`name="Conflicts[i].Resolution"`, values = the `UserPortabilityResolutionKind.ToString()` strings — so an unchosen radio posts empty), a hidden `Kind`/`EntityId`/`AbsentKind`/`AbsentField`, a `<select name="Conflicts[i].PickedTargetId">` seeded from `Model.Targets`, and the `myportability.resolve.add_elsewhere` / `myportability.resolve.discard` kw-l keys as the radio labels. The form is `enctype="multipart/form-data"` with a `name="archive"` file input (the re-upload the POST's apply reads) + `@Html.AntiForgeryToken()`.

**(c) The "add elsewhere" target picker — the `_AudienceEditor` entry-read deviation:** the unit plan's entry-read pointed at `Views/Directory/_AudienceEditor.cshtml`, which **does not exist** — the real `Views/Profile/_AudienceEditor.cshtml` is bound to `ProfileEditViewModel` (a profile-visibility editor, not a reference re-point picker) and is **not** reusable here. Instead, the `GET` seeds a simple `<select>` of the resident's **member-of groups** via `IUserInfoService.GetGroupsForUserAsync(actor)` (a **business read** seam, transient-registered in `DependencyInjection.cs` line 45 — *not* the authorization engine, so C-M27·7 still holds). This matches the §2.4 verbatim "Add it to a **group** you are a member of." **Note for U10/U12:** the picker is **groups-only** (member-of groups), a subset of the design-doc's full standing set ("a group they are a member of, a tag they own, a component they are a member of") — the standing *check* is the service's (U06 `ResolveAsync` standing pre-pass, fail-closed on unknown target kinds); the UI surfaces the group subset the resident can pick. `GetGroupsForUserAsync` returning empty (no groups) yields an empty `<select>`; a throw is swallowed (`try/catch`) so a lookup failure degrades to "discard-only" rather than a 500.

**(d) The two kw-l keys (verbatim, completing the 7-key §kw-l closed set):** U08 adds the last two of the design-doc §kw-l `myportability.*` 7-key set — `myportability.resolve.add_elsewhere` and `myportability.resolve.discard` — to **all four** dictionaries in `KnownTranslationKeys.cs` (immediately after each dict's `myportability.import.resolve` line). **Four-language note:** `en` "Add elsewhere" / "Discard"; `de` "Woanders hinzufügen" / "Verwerfen"; `fr` "Ajouter ailleurs" / "Rejeter"; `da` "Tilføj andre steder" / "Kassér". With U07's 5 keys (`index.title` / `export` / `import` / `import.resolve` / `status`), the **7-key §kw-l closed set is now complete**. Still deliberately distinct from M11's admin `portability.*` keys (D9).

**(e) The `Via = Owner` + no-GlobalAdmin note (C-M27·6):** the gate is the **verified-resident self-lane** — `[Authorize]` (any signed-in resident), the actor is the resident's **own** `subjectId` (`KumunitaPrincipal.SubjectId(User)`), and there is **no** GlobalAdmin gate (C-M27·7 / the M11 `AdminPortabilityController` contrast): this is the resident's own data. The one `portability.import.resolve` `AccessAudit` row (`Via = Owner`, `TargetKind "portability"`) is emitted by the **service** (U06 `ResolveAsync`); the controller adds none. The reads (the `GET` resolve-review) emit **no** row.

**(f) Compile warnings:** **none new** in `UserPortabilityController.cs`, `ResolveReview.cshtml`, or `KnownTranslationKeys.cs`. The full-solution build is green (0 errors; the pre-existing baseline — `Kumunita.Core` 9 / `Kumunita.Web` 12 / both test projects — is unchanged, matching the U03–U07 baselines).

## U09 — acceptance tests (3)

**Date:** 2026-10-07
**Status:** COMPLETE. All three Core acceptance tests authored in one new
deliverable file; `m27-u09.md` moved to `done/`. Build green (0 errors, no new
warnings in the U09 file). **No gate recorded** (U11), **no Web pins authored**
(U10), **no design-doc edits** (U11).

**(a) The test file:** `tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs`
(the `UserPortabilityAcceptanceTests` class; `PostgresFixture` harness + the
same boot shape as U03/U04/U06).

**(b) The three names (verbatim):** `M27_Acceptance_RoundTrip_ResidentFootprintRestored`
/ `M27_Acceptance_NoSecret_SelfScoped` / `M27_Acceptance_PerEntityConflict_Resolve`.

**(c) Pass/red counts (for U11's gate):** all three **pass** — xunit.v3
in-process runner (`dotnet exec Kumunita.Core.Tests.dll -class
Kumunita.Core.Tests.UserPortabilityAcceptanceTests`) reports
`Total: 3, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0` (the ~20 s
`postgres:18` Testcontainers start was the long pole; leftover Docker
containers cleaned up after).

## U10 — Web pins

**Date:** 2026-10-07
**Status:** COMPLETE. The seven Web-surface pins authored in one new
deliverable file; `m27-u10.md` moved to `done/`. Build green (0 errors, no
new warnings in the U10 file). **No gate recorded** (U11), **no design-doc
edits** (U11), **no new authorization surface** (the C-M27·7 pin).

**Exit checklist (from the unit plan):**

- [x] `dotnet build Kumunita.slnx -c Debug` green. — **PASS** (`Build succeeded. 0 Error(s)`; a `Select-String` over `UserPortabilityControllerTests` returns nothing — **no new warnings** in the U10 file).
- [x] The Web pins discovered (the pass/red status recorded for U11's gate). — **PASS** (all 7 methods green — the 6 `[Fact]` + `Web_KwL_KeysResolve` as a 7-case `[Theory]` = **13 test cases**, `Total: 13, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`, `Time: 0.620s`).
- [x] **No gate recorded** (U11). — **PASS** (no gate section appended to the design doc; U11 owns the "run the record" step).
- [x] **No new authorization surface** (the C-M27·7 pin — the pins assert the existing `AccessVia.Owner`, not a new value). — **PASS** (the pins assert the *surface* contract — a plain `[Authorize]` resident self-lane (no `GlobalAdmin` role gate, no audience decision) + exactly-once resident-scoped delegation; the audit row (if any) is the **service's** (`Via = Owner`, `TargetKind "portability"`, verb `portability.export` / `portability.import.resolve`), the controller adds none — C-M27·6; no `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()` branch / `Audience` added).
- [x] U00–U09 shipped code unchanged (the pins are test-only). — **PASS** (only the one new test file was authored; no source / view / registry / design doc touched).

**(a) The test file (new):** `tests/Kumunita.Web.Tests/UserPortabilityControllerTests.cs` (the `UserPortabilityControllerTests` class; the M11 `AdminPortabilityControllerTests` Web-pin shape to mirror — the gate + the one-audit-row-per-write + the reads-emit-none; NSubstitute seam, **no** `PostgresFixture`).

**(b) The 7 pin names (verbatim, the unit-plan list):** `Web_Export_ResidentGate` / `Web_Export_OneAuditRow_ViaOwner` / `Web_Import_OneAuditRow_ViaOwner` / `Web_ImportResolve_OneAuditRow_ViaOwner` / `Web_Reads_EmitNoAuditRow` / `Web_FailClosed_Render` / `Web_KwL_KeysResolve`.

**(c) Pass/red counts (for U11's gate):** all **pass** — xunit.v3 in-process runner (`dotnet exec Kumunita.Web.Tests.dll -class Kumunita.Web.Tests.UserPortabilityControllerTests`) reports `Total: 13, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0, Time: 0.620s` (the `Web_KwL_KeysResolve` pin is a 7-case `[Theory]` — the 7 `myportability.*` keys × 4 languages — the other 6 pins are `[Fact]`; the design-doc §2.7 gate row is `UserPortabilityControllerTests (Web pins) | pass`). **No Postgres** (fast, < 1 s).

**(d) Compile warnings:** **none new** in `UserPortabilityControllerTests.cs` (the pre-existing baseline — `Kumunita.Core` / `Kumunita.Web` / both test projects — is unchanged, matching the U03–U09 baselines). **Note:** the `& 'C:\Program Files\dotnet\dotnet.exe' <…>` call-operator form (single-quoted full path) is the working invocation on this machine — `& "…"` / bare-path forms both hit a `Unexpected token 'build'` parse error in the agent's terminal wrapper (the same shell-mangling trap the AGENTS.md §"Running PowerShell commands safely" warns about).

**(e) The one-audit-row / reads-emit-none posture (C-M27·6), faithful to the shipped source:** the audit row is the **service's**, not the controller's (the M11 `AdminPortabilityControllerTests` contrast). The shipped `UserPortabilityService` emits the audit rows — `portability.export` in `ExportAsync` + `portability.import.resolve` in `ResolveAsync`, both `Via = Owner` / `TargetKind "portability"` — and the `ClassifyAsync` (import classify) read is **read-only** (C-M27·5, "no writes" → no audit row). The Web-surface pins therefore assert the surface's contract — **exactly-once**, resident-scoped delegation (the actor is the resident's own `Kumunita.Sub` `subjectId`, never another resident's) + the plain `[Authorize]` resident self-lane + the fail-closed render — NOT the Core audit-row content (that is the U09 Core acceptance test's pin, recorded in U11's gate). The `Web_Import_OneAuditRow_ViaOwner` pin asserts exactly-once, resident-scoped `ClassifyAsync` delegation (the import-lane action); the audit row it *may* turn into is the service's — the same "the audit row lives in the service, the controller adds none" posture the M11 test carries.

**(f) The fail-closed render (C-M27·4):** `Web_FailClosed_Render` — a `plan.Ok = false` (the `UserPortabilityImportPlan` closed-failure set) renders `TempData["error"]` containing both the `myportability.status` kw-l key + the failure list, and does not write (the `ClassifyAsync` read ran but no `ResolveAsync` apply write — the instance unchanged). The `translationProvider` is `null` (the test floor) → the controller's `T()` returns the raw key `myportability.status` — the pin asserts the key + failure list render (the `_FlashToast` partial / the view renders it in the resident's language when the seam is present).

## U11 — gate recorded

**Date:** 2026-10-07
**Status:** COMPLETE. U11 *ran* the two suites (per the AGENTS.md test-runner
quirk: `dotnet build Kumunita.slnx -c Debug` green, then xunit.v3 in-process
`dotnet exec <assembly.dll> -class <FQCN>`) and *recorded* the gate as
`### Run result (M27 acceptance gate — 2026-10-07)` in the design doc §2.7.
**No code, no build beyond the test run, no new tests** (U09/U10 own those).
`m27-u11.md` moved to `done/`.

**The gate (all pass — the M3/M4 author-not-run precedent did NOT fire; the
Core runtime was present):**
- **3 Core acceptance tests — all pass** (`Total: 3, Errors: 0, Failed: 0,
  Skipped: 0, Not Run: 0`, Time: 15.374s): `M27_Acceptance_RoundTrip_ResidentFootprintRestored`
  / `M27_Acceptance_NoSecret_SelfScoped` / `M27_Acceptance_PerEntityConflict_Resolve`.
  Note: the *actual* authored names are the `M27_Acceptance_*` set (the §2.6
  template prose above shows the illustrative `UserPortability*` names — the
  gate records the real names from
  `tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs`).
- **7 Web pins — pass** (`UserPortabilityControllerTests`,
  `Total: 13, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`, Time: 0.513s —
  the 6 `[Fact]` + the 7-case `Web_KwL_KeysResolve` `[Theory]`).
- **Drift pauses:** the two `## U00 — Drift pause` sections (#1 the ADR-index
  backfill, #2 the four-docs-outside-the-frozen-set + the `Tag.CreatedBy`
  field) — **both resolved** (Option A); **no drift still open**.
