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
