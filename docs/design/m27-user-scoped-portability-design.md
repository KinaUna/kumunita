# M27 — User-scoped portability (resident self-export + per-entity conflict-resolution import) — design

> **Milestone M27.** A **resident** moves **their own authored data** in and
> out, as data: one portable `*.kumunita` archive (the resident's content +
> their media + the minimal identity reference set, **no credential material**),
> and a **fail-closed, per-entity conflict-resolution import** of that archive
> into another instance. **Resident plane, not an operator one** (C-M27·6):
> zero new `AccessAction`, `AccessVia`, `IAuthorizationService.Decide()`
> branch, or `Audience` — the resident self-lane (`[Authorize]` + the
> resident's own `subjectId`) is the only gate, and the per-entity conflict
> decision is a **business decision in the resident's UI**, not an
> authorization decision (C-M27·7).
>
> **Status.** **LOCKED.** The decisions D1–D9 are locked in **ADR 0148
> (Accepted, 2026-10-06)**. The `[PROPOSED]` set in the register
> `plan-m27-user-scoped-portability.md` was **user-approved before U00 ran**
> (the open-veto window closed with no veto recorded; the U00 handoff entry
> records the lock) and is refined **in favor of the source** where the source
> contradicted the prose (drift guard, entries 1–2 below): the M11
> `PortabilityDocTypes` reference map + the four non-M11 doc definitions are
> the ground truth this doc is pinned to.
>
> **The one thing every unit must respect:** the **C-M27·2 no-secret
> boundary** (carried verbatim from C-M11·2) is the single most load-bearing
> M27 invariant — the archive travels the resident's authored data + media,
> **never** the credential material. The **C-M27·3 scope invariant**
> (ownership, not read) is the second most load-bearing — the archive contains
> **only** the resident's authored/owned rows, never other residents' content.

## Context

M11 (ADR 0108) closed the operator's world-seam: a GlobalAdmin moves the
**whole instance** in and out as one versioned `*.kumunita` archive,
fail-closed, no-secrets. It deliberately deferred two follow-on lanes (named
in ADR 0108 Consequences, C-M11·5): **per-slice portability** (one user's data
out) and **import-merge** (onto a diverged instance).

M27 owns both halves of that deferred lane, at the **resident's** scale.
The loop that M11 closes for the operator (the whole neighborhood's data can
leave and return) is now closed for the resident: **their own data** can leave
the platform as a portable archive and come back — but, unlike M11's
all-or-nothing restore, it comes back **where it fits**: on import into an
instance where their groups, components, pages, or tags do not exist or they
are not a member, the resident **manually resolves each conflict entity** —
adding it to a target they are a member of or discarding it. No auto-merge, no
silent re-assignment (ADR 0001-B: the author's choice is absolute).

M27 builds entirely on **frozen, verified seams** (the M11 `Portability/`
context — the BCL-only `KumunitaArchive` ZIP (de)serializer, the
`PortabilityManifest` self-describing head, the frozen `PortabilityDocTypes`
44-doc inventory, the no-secret `PortabilityPrincipal` shape, the M11
export/import partials) + the **resident self-lane house pattern** (the
`/settings/...` sections, `AccessVia.Owner`, `[Authorize]` verified-resident
gate, the `kw-l` closed-key registry).

## Goals / Non-goals

**In (shipped by M27):** the `UserPortabilityService` (D7) in the existing
`Kumunita.Core/Portability/` context — the resident-scoped **export** (U03:
the M11 `KumunitaArchive` writer over the resident's authored/owned rows,
reusing the M11 no-secret principal shape), the **classify** (U04: the
`clean`/`duplicate`/`conflict` classification + the reference-availability
report, pre-write), the **resolve model** (U05: the
`UserPortabilityEntityResolution` POCO set), the **apply** (U06: the
`ResolveAsync` fail-closed apply, the resident's per-entity choice), the
`UserPortabilityController` at `/account/portability` (U07/U08: the index, the
export stream, the import upload, the resolve-review UI), the
`myportability.*` kw-l keys × 4 languages (U07/U08), the **three Core
acceptance tests** (U09: round-trip / no-secret + self-scoped / per-entity
conflict), the **Web-surface pins** (U10), the **acceptance gate** recorded
(U11), the **kw-l parity close** (U12), and the **U13 milestone flip**
(M27 → `StatusDone`, M28 → `StatusNext`).

**Out (each a named follow-on lane, ADR 0148 Consequences — own ADRs):**
cross-neighborhood federation (the `README §Deferred` OpenIddict lane), the
backup-automation / cron surface (OPS.md), a resident **delete-all-their-data**
lane (M27 is export / backup + import, not deletion), and import-merge
**beyond** the per-entity resident choice (a community-level reconciliation,
own ADR).

## Human cost

This is a **resident** surface, not an operator one. It gives the resident
**their own data** the ability to leave and return — the loop closing *out of*
the platform at the resident's scale. It takes **nothing** from the resident's
time or attention beyond a deliberate export click and a conflict-resolution
review on import (the per-entity decision is the point — F4). It **protects**
the trust boundary: the archive is *structurally* incapable of carrying
credential material (C-M27·2), and it contains **only** the resident's
authored/owned rows (C-M27·3) — a stolen or mishandled `*.kumunita` file is
the resident's own content without secrets, and it cannot leak another
resident's data. The surface is quiet (one audit row per write, reads emit
none, C-M27·6) and the only thing it asks of the resident is a deliberate
click.

## Parts affected

- **`Kumunita.Core/Portability/`** (existing context, **extended** — M27 is a
  lane **on** the M11 surface, not a new bounded context, D7) —
  `UserPortabilityService.cs` (new: the `IUserPortabilityService` seam + the
  `UserPortabilityService` shell + the `UserPortabilityImportPlan` /
  `UserPortabilityImportResult` / `UserPortabilityEntityResolution` POCO
  records), `UserScopeInventory.cs` (new: the D1 closed resident-scope
  inventory as data — the `Type` → ownership field map + the excluded list),
  `UserBusinessKeys.cs` (new: the D4 per-kind business-key matchers — the
  `duplicate` matcher + the `conflict` reference-availability report),
  `PortabilityManifest.cs` (modified: the **one** additive optional field —
  the D2 resident-scope marker; every existing field **unchanged**).
- **`Kumunita.Core/DependencyInjection.cs`** — the `IUserPortabilityService`
  registration (next to the `IPortabilityService` registration, the same shape).
- **`Kumunita.Web/Controllers/UserPortabilityController.cs`** (new) +
  **`Views/Account/Portability/Index.cshtml`** (new) +
  **`Views/Account/Portability/ResolveReview.cshtml`** (new).
- **`Kumunita.Core/Localization/KnownTranslationKeys.cs`** — the
  `myportability.*` closed-key registry entries × 4 languages.
- **`tests/Kumunita.Core.Tests/UserPortabilityExportTests.cs`** (new) +
  **`tests/Kumunita.Core.Tests/UserPortabilityClassifyTests.cs`** (new) +
  **`tests/Kumunita.Core.Tests/UserPortabilityResolveModelTests.cs`** (new) +
  **`tests/Kumunita.Core.Tests/UserPortabilityResolveTests.cs`** (new) +
  **`tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs`** (new) +
  **`tests/Kumunita.Web.Tests/UserPortabilityControllerTests.cs`** (new).
- **The U13 parity surfaces:** `Milestones.cs`, the `README.md` Roadmap,
  `docs/STATUS.md`, `docs/ARCHITECTURE.md`, `MilestonesTests.cs`.

**Unchanged (the frozen surface):** `IAuthorizationService` /
`IAuditableResource` (zero new adapters, C-M27·7), the `AccessAction` /
`AccessVia` sets (the `AccessVia.Owner` value already exists and is reused
verbatim), the `Audience` doc, every `*DocTypes` surface (M27 adds **no**
document and **no** index), the M11 `KumunitaArchive` / `PortabilityService` /
`PortabilityDocTypes` frozen set (M27 reads and reuses them, adds **one**
additive optional `PortabilityManifest` field), and `Program.cs` (the CSP is
untouched; the surface is server-rendered MVC).

## Invariants (C-M27·1–·8)

- **C-M27·1 · The archive reuses the M11 format at the resident scope.**
  The M27 `*.kumunita` archive is the **same** M11 format (the `format`
  authority `kumunita/portability/1` unchanged) + one additive optional
  resident-scope marker in the manifest (§2.1 manifest-marker). A plain M11
  whole-instance archive still validates/imports unchanged (the ADR 0098
  additive-frozen-surface precedent). Pinned by the U07 format-compat test +
  the §2.1 manifest-marker pin.

- **C-M27·2 · The archive travels the data, never the secrets.** The M27
  archive contains the resident's authored data + the resident's media + the
  minimal no-secret identity reference set. It contains **no** credential
  material (no password hash / security stamp / token / recovery code) — the
  `PortabilityPrincipal` POCO shape is reused verbatim (C-M11·2 carried).
  Pinned by the no-secret + self-scoped test (D8b) — the single most
  load-bearing M27 invariant, unchanged from M11.

- **C-M27·3 · Scope is ownership, not read.** The export/import scope is the
  resident's **authored/owned** rows (§2.2 scope-inventory), never "everything
  they can see." Another resident's content — a delegated, audience-visible,
  or membership-visible row the actor is *not* the author/owner of — is
  **absent** from the archive. Pinned by the self-scoped test (D8b) + the
  §2.2 closed set.

- **C-M27·4 · Import is fail-closed on the write path + the resident's
  choice is absolute.** A rejected / aborted resolve writes **nothing** (the
  M11 C-M11·4 posture). On apply, **only** the entities the resident resolved
  are written, re-pointed exactly as the resident chose (add-elsewhere → the
  target they picked; discard → no write). There is **no** silent auto-merge
  of a `conflict` entity to a scope the resident did not choose (ADR 0001-B).
  Pinned by the per-entity conflict test (D8c) + the `ResolveAsync`
  fail-closed pin.

- **C-M27·5 · Per-entity conflict classification is pre-write.** The
  `clean`/`duplicate`/`conflict` classification + the reference-availability
  report run to completion **before any write** (§2.4); a
  `conflict` entity never auto-applies — it is surfaced for the resident's
  decision. Pinned by the `ClassifyAsync` pre-write pin + the §2.4
  classification pin.

- **C-M27·6 · The surface is the resident's own, audited, and quiet.**
  Export + import + resolve are `[Authorize]` verified-resident
  (`/account/portability`, the resident's own data only — no GlobalAdmin
  break-glass, no audience decision); each **write** emits **exactly one**
  `AccessAudit` row (`TargetKind "portability"`, `Via = Owner`, verb
  `export` / `import` / `import.resolve`); the **reads** (index, the
  resolve-review) emit **none**. Pinned by the Web-surface pins (D8).

- **C-M27·7 · Zero new authorization surface.** No new `AccessAction`, no new
  `AccessVia`, no `IAuthorizationService.Decide()` branch, no `Audience`.
  The per-entity resolve is a **business decision in the resident's UI**,
  not an authorization decision — the frozen `IAuthorizationService` surface
  count is unchanged. Pinned by the design doc §Seams + the U12 handoff.

- **C-M27·8 · Docs parity holds at the flip (the close unit, U13).**
  `Milestones.cs` (M27 → `StatusDone`, M28 → `StatusNext`), the README
  Roadmap (M27 → **Done**, M28 → **In progress**), `docs/STATUS.md`,
  `docs/ARCHITECTURE.md` all move together **in the same unit** (U13), and
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its three pins passing
  (the AGENTS.md doc↔code parity contract).

## FACES (F1–F5)

**F1 · The resident's data is portable out.** A resident downloads one
`*.kumunita` archive holding their authored posts/events/projects/documents/
pages/messages + their media + the minimal identity reference set — the loop
closes out of the platform at the resident's scale (C-M27·1/2/3).

**F2 · Their data comes back intact where it fits.** Importing that archive
into an instance where their groups/pages *do* exist restores the clean
entities referentially intact (C-M27·1/4).

**F3 · Their data never carries a secret.** The archive has no credential
material; the resident re-authenticates as themself (C-M27·2).

**F4 · Where it doesn't fit, the resident decides — per entity.** Entities
whose references don't exist in the target are surfaced as `conflict`; the
resident adds each elsewhere (re-pointing to a target they are a member of)
or discards it; nothing is auto-merged and nothing is half-applied
(C-M27·4/5).

**F5 · The surface is quiet + audited + the resident's own.** Export / import
/ resolve are the resident's own (no GlobalAdmin break-glass), each write
leaves exactly one audit row, the reads leave none, and a non-resident (or a
resident reaching for someone else's data) gets no surface (C-M27·6/7).

**FACES-strength:** **f**lexible (the resident chooses when to export and how
to resolve each conflict), **c**oherent (the closed scope inventory + the
fail-closed apply mean the resident's data travels as a coherent whole),
**s**table (the self-describing `format` version + the content-addressed
media are idempotent). **FACES-consumed:** none — M27 is the resident's own
surface, not an operator's; it consumes the resident's deliberate attention
on import (the per-entity conflict review) to buy their data's portability,
which is the correct exchange for a self-hosted platform's world-seam.

## Approach (three tracks + close)

- **Track A — Docs (U00):** the design doc + ADR 0148. U00 is the sign-off
  gate.
- **Track B — Framework (U01–U02):** `IUserPortabilityService` +
  `UserPortabilityService` shell (U01); the resident-scope **filter** + the
  per-kind **business-key** matchers (U02).
- **Track C — Export (U03):** the resident-scoped document + media export.
- **Track D — Import (U04–U08):** `ClassifyAsync` (U04); the resolve model
  (U05); `ResolveAsync` (U06); the Web import surface + resolve-review UI
  (U07–U08).
- **Track E — Tests + close (U09–U13):** the Core acceptance tests (U09);
  the Web-surface pins (U10); the acceptance gate recorded (U11); the kw-l
  parity close (U12); the milestone flip (U13).

## Workflow (three-tier, per-unit)

Same contract as the M11/M12/M26 registers: **primary tier** = this design doc
(the only authority after it lands); **secondary** = the register
`docs/plans-milestones/plan-m27-user-scoped-portability.md`; **scratch** = the
handoff note (`docs/plans-milestones/in-progress/m27-handoff-notes.md`, one
`## U#` section per unit: entry state / what ran / drift / open items). Per
unit: Goal → Entry reads (3–6 files) → Deliverables (≤ 5 files) → Exit (build
green + handoff entry).

**Unit-series rules (the atomicity contract):** (1) a unit never modifies a
file not in its own `Deliverables`; (2) never rewrites this design doc outside
the §2.8 drift-guard note; (3) never introduces a test whose exact name is not
in the §2.6 pinned-tests list; (4) **never opens a new seam on
`IAuthorizationService` / `IUserInfoService` / `IIdentityService`** beyond
what this doc pinned — M27 adds **no** authorization surface (C-M27·7); (5)
**never re-shapes the M11 `PortabilityManifest` / `KumunitaArchive` /
`PortabilityDocTypes` frozen set** outside the §2.1 manifest-marker additive
optional field (U00 pinned); (6) **never auto-applies a `conflict` entity** —
the resident's per-entity choice is the only apply path (C-M27·4); (7) if
entry reads reveal this design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

**kw-l key rule (every Web unit that adds a key):** the key must land in
**all four** dictionaries in `src/Kumunita.Core/Localization/
KnownTranslationKeys.cs` (`EnValues` / `DeValues` / `FrValues` / `DaValues`)
or `KnownTranslationKeys_ParityTests` +
`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` fail.

**Milestone-flip rule (U13 only, in one unit):** `Milestones.cs` M27 →
`StatusDone` + M28 → `StatusNext`; the README Roadmap M27 line → **Done** + M28
→ **In progress**; `docs/STATUS.md` + `docs/ARCHITECTURE.md` move M27 → done;
**and** `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the order pin is
**unchanged**, the done-list pin gains `M27`, and the single-in-progress pin is
**renamed + re-pointed** to M28 (the AGENTS.md doc↔code parity contract,
C-M27·8).

## Seams & contracts (Part 2)

### 2.1 Frozen seam list (exact C#)

**`IUserPortabilityService`** — the resident seam the web layer + the tests
target (D7, U01's shell; U03–U06 fill the bodies):

```csharp
public interface IUserPortabilityService
{
    // U03 (export) — one complete, valid *.kumunita archive at the resident scope.
    // Emits exactly one `portability.export` AccessAudit row (Via = Owner).
    Task<Stream> ExportAsync(string residentSubjectId, CancellationToken ct = default);

    // U04 (classify) — validate-then-classify, no writes.
    // Returns the closed clean/duplicate/conflict classification (C-M27·5).
    Task<UserPortabilityImportPlan> ClassifyAsync(
        string residentSubjectId, Stream archive, CancellationToken ct = default);

    // U06 (resolve + apply) — apply only the resident-resolved entities, fail-closed.
    // A rejected / aborted resolve writes NOTHING (C-M27·4).
    // Emits exactly one `portability.import.resolve` AccessAudit row (Via = Owner).
    Task<UserPortabilityImportResult> ResolveAsync(
        string residentSubjectId,
        UserPortabilityImportPlan plan,
        IReadOnlyList<UserPortabilityEntityResolution> resolutions,
        CancellationToken ct = default);
}
```

**`UserPortabilityImportPlan`** — the classify output (U04; the closed shapes,
U00's §Seams verbatim):

```csharp
public sealed record UserPortabilityImportPlan(
    bool Ok,
    IReadOnlyList<UserPortabilityEntityClassification> Entities = [],
    IReadOnlyList<string> Failures = []);

public sealed record UserPortabilityEntityClassification(
    string Kind,              // e.g. "Post", "Event", "Message"
    string EntityId,          // the id in the archive
    UserPortabilityEntityStatus Status,
    IReadOnlyList<UserPortabilityAbsentReference> AbsentReferences = [],
    string? DuplicateId);     // for Duplicate: the existing entity's id in the target

public enum UserPortabilityEntityStatus
{
    Clean,
    Duplicate,
    Conflict
}

public sealed record UserPortabilityAbsentReference(
    string Kind,              // e.g. "Group", "Tag", "Component", "Page"
    string Field,             // e.g. "GroupId", "TagIds", "ComponentId"
    string Value);            // the id in the archive that is absent in the target
```

**`UserPortabilityEntityResolution`** — the resident's per-entity decision
(U05; the closed shapes, U00's §Seams verbatim):

```csharp
public sealed record UserPortabilityEntityResolution(
    string Kind,              // the entity kind (must match a classification entry)
    string EntityId,          // the entity id (must match a classification entry)
    UserPortabilityResolutionKind Resolution,
    string? PickedTargetId,   // for AddElsewhere: the target the resident chose
    string? AbsentRefKind,    // for AddElsewhere: the absent-reference kind being re-pointed
    string? AbsentRefField);  // for AddElsewhere: the absent-reference field being re-pointed

public enum UserPortabilityResolutionKind
{
    AddElsewhere,
    Discard
}
```

**`UserPortabilityImportResult`** — the resolve output (U06; the closed
shapes, U00's §Seams verbatim):

```csharp
public sealed record UserPortabilityImportResult(
    bool Ok,
    int AppliedCount,
    int DiscardedCount,
    IReadOnlyList<string> Failures = []);
```

**`PortabilityManifest`** additive field (D2, U01 — the **one** additive
optional field; every existing field **unchanged**):

```csharp
// Added to PortabilityManifest (U01):
/// <summary>
/// The D2 resident-scope marker. `"resident"` = a M27 resident-scoped archive;
/// `null` = a plain M11 whole-instance archive (the ADR 0098 additive-frozen-
/// surface precedent: existing readers keep compiling, a plain M11 archive
/// still imports whole).
/// </summary>
public string? Scope { get; set; }

/// <summary>The subjectId of the resident this archive is scoped to (D2, D3).</summary>
public string? ResidentSubjectId { get; set; }
```

### 2.2 Closed resident-scope inventory (D1)

> **LOCKED FROM SOURCE.** This is the **exact** closed set of content
> documents M27 exports/imports at the resident scope, with the per-doc
> ownership field read from the M11 `PortabilityDocTypes` reference map where
> the doc is in the 44-entry set, or from the doc's own definition where it
> is not (drift guard entry 1 — the four non-M11 docs). U02's
> `UserScopeInventory` registry copies this table **verbatim**.
>
> **Ownership field** = the field whose value must equal the resident's
> `subjectId` (or, for `Conversation` / `Message`, the field that links the
> entity to the resident as a participant) for the entity to be in scope.
> `→ principal` means the field is a `subjectId`. `→ doc` means the field is
> a reference to another doc's id (the scope is determined by the parent doc's
> ownership).

**In-scope doc families (the resident's authored/owned content):**

| Type | Ownership field (scope) | Source | Note |
|---|---|---|---|
| `Post` | `AuthorId` → principal | M11 ref map | the resident's authored posts |
| `PostReply` | `AuthorId` → principal | M11 ref map | the resident's authored replies |
| `PostTranslation` | `AuthorId` (nullable) → principal | M11 ref map | the resident's user-added translations (ADR 0022) |
| `ReplyTranslation` | `AuthorId` (nullable) → principal | M11 ref map | the resident's user-added translations (ADR 0022) |
| `AnnouncementComment` | `AuthorId` → principal | M11 ref map | the resident's comments on announcements |
| `Event` | `AuthorId` → principal | M11 ref map | the resident's authored events |
| `EventRsvp` | `UserId` → principal | M11 ref map | the resident's own RSVPs |
| `EventTranslation` | `AuthorId` (nullable) → principal | M11 ref map | the resident's user-added event translations (ADR 0059) |
| `ProjectGoal` | `AuthorId` → principal | M11 ref map | the resident's authored goals |
| `Project` | `AuthorId` → principal | M11 ref map | the resident's authored projects |
| `TodoItem` | `AuthorId` → principal | M11 ref map | the resident's authored to-dos |
| `KanbanBoard` | `AuthorId` → principal | M11 ref map | the resident's authored boards |
| `TodoTranslation` | `AuthorId` (nullable) → principal | M11 ref map | the resident's user-added to-do translations (ADR 0088) |
| `BoardTranslation` | `AuthorId` (nullable) → principal | M11 ref map | the resident's user-added board translations (ADR 0088) |
| `ProjectTranslation` | `AuthorId` (nullable) → principal | M11 ref map | the resident's user-added project translations (ADR 0088) |
| `TodoComment` | `AuthorId` → principal | M11 ref map | the resident's comments on to-dos |
| `Conversation` | `ParticipantA` ∪ `ParticipantB` → principal | M11 ref map | conversations the resident participates in |
| `Message` | `ConversationId` → `Conversation` (the resident is a participant) | M11 ref map (via `Conversation`) | messages in the resident's conversations |
| `Page` | `AuthorId` → principal | M11 ref map | the resident's authored pages (blog) |
| `Tag` | `CreatedBy` → principal | `Tag.cs` (drift guard entry 1) | the tags the resident created |
| `InventoryItem` | `AuthorId` → principal | `InventoryItem.cs` (drift guard entry 1) | the inventory items the resident created |
| `Document` | `OwnerId` → principal | `Document.cs` (drift guard entry 1) | the documents the resident uploaded |
| `DocumentFolder` | `OwnerId` → principal | `DocumentFolder.cs` (drift guard entry 1) | the document folders the resident created |
| `Bookmark` | `OwnerId` → principal | `Bookmark.cs` (drift guard entry 1) | the resident's bookmarks |
| `Profile` | `SubjectId` (the resident's own) | `Profile.cs` | the resident's own bio + tags (M23) + display identity |
| `Group` | `OwnerId` → principal **∪** member (the resident is in `GroupMembership.UserId`) | M11 ref map (drift guard entry 2) | the groups the resident created or is a member of — needed to make their content resolvable on import |
| `GroupMembership` | `UserId` → principal | M11 ref map | the resident's own membership rows |
| `GroupInvitation` | `UserId` → principal | M11 ref map | invitations the resident received |
| `GroupJoinRequest` | `UserId` → principal | M11 ref map | join-requests the resident submitted |

**The minimal identity reference set (not in-scope content, but carried so
the archive is self-contained on import):** the groups the resident created
or is a member of (above), the tags the resident created (above), and the
`MediaObject` rows for the media the resident's content references (the
M11 media manifest, filtered to the resident's `ImageIds` / `AttachmentIds`).

**Excluded — the docs that do NOT travel (C-M27·2 + C-M27·3 at the source):**

| Excluded type | Reason |
|---|---|
| `IdentityToken` | **Credential material** (C-M11·2 / C-M27·2 — the M11 "Excluded" set, unchanged) |
| `OutboxEmail` | **Operational state** (a fresh instance re-derives; the M11 "Excluded" set, unchanged) |
| `EmailDeadLetter` | **Operational state** (the M11 "Excluded" set, unchanged) |
| `AccessAudit` | **Operational state** (a log, not resident content; the M11 "Excluded" set, unchanged) |
| `AuditPurgeSummary` | **Operational state** (the M11 "Excluded" set, unchanged) |
| `LocaleSettings` / `LanguageCatalog` | **Operator instance-identity** (travels via `config.json` in M11; the resident does not own it — D1 "out of scope") |
| Other residents' `Post` / `Message` / `Document` / `Bookmark` / any other content | **C-M27·3** — scope is ownership, not read; another resident's authored rows are absent |
| `Notification` / `NotificationPreference` / `NotificationSubscription` | **Per-resident read state** (the M6 operational state; not the resident's authored content — the M11 inventory includes them for whole-instance restore, but the resident-scoped filter excludes them as they are system-generated, not authored by the resident) |
| `DelegationGrant` / `GuardianLink` / `ModeratorAssignment` / `ComponentMembership` | **Identity-graph / authorization state** (the M11 identity graph; not the resident's authored content — the M27 scope is the resident's *content*, not their authorization relationships) |
| `Report` | **Moderation state** (a report is a moderation action, not the resident's authored content) |
| `Announcement` / `AnnouncementTranslation` | **Operator/GlobalAdmin content** (announcements are authored by GlobalAdmin/Translator, not by a plain resident — the resident's `AnnouncementComment` is in scope, the `Announcement` itself is not) |
| `CommunityTranslation` / `TranslationResource` / `GroupTranslation` / `TagTranslation` | **Operator/Translator-managed translations** (the UI-string + operator-managed translation rows; not the resident's authored content) |
| `KanbanLane` / `BoardItemPlacement` | **Sub-entities of the resident's board** (the `KanbanBoard` is in scope; its lanes + placements are structural sub-rows that travel with the board via the M11 apply order, not as independently-scoped resident content) |

### 2.3 Per-kind business keys (D4)

> **The `duplicate` matcher keys.** A `duplicate` entity is one whose
> business key matches an existing entity in the target. The keys are the
> natural identity fields (the fields that make a specific entity
> distinguishable from all others by the same author). U02's
> `UserBusinessKeys` matchers implement these as pure
> `bool Matches(kind, candidate, existing)` functions — no session, no write.

| Type | Business key (the `duplicate` matcher) | Note |
|---|---|---|
| `Post` | `(AuthorId, Created, Title, Body)` | the same author, same time, same content = the same post |
| `PostReply` | `(PostId, AuthorId, Created, Body)` | the same reply on the same post |
| `Event` | `(AuthorId, Created, Title)` | the same event by the same author |
| `TodoItem` | `(AuthorId, Created, Title)` | the same to-do by the same author |
| `KanbanBoard` | `(AuthorId, Created, Title)` | the same board by the same author |
| `Page` | `(AuthorId, Created, Title)` | the same page by the same author |
| `Tag` | `Slug` | tags are globally unique by slug (the ADR 0044 business key) |
| `Document` | `(OwnerId, Created, Title)` | the same document by the same owner |
| `Conversation` | `(ParticipantA, ParticipantB)` | a conversation pair is unique (the M9 `convo_uidx_pair` shape) |
| `Message` | `(ConversationId, SenderId, Created)` | the same message in the same conversation |
| `InventoryItem` | `(AuthorId, Created, Name)` | the same inventory item by the same author |
| `Bookmark` | `(OwnerId, TargetKind, TargetId)` | a bookmark is unique per (owner, target) (the M17 `bm_uidx_owner_target` shape) |
| `Profile` | `SubjectId` | the profile is identified by the resident's subject id |
| `Group` | `Name` | groups are identified by name (the M1 `Group.Name` natural key) |

**The `conflict` reference-availability report shape:** for each in-scope
entity, the `ClassifyAsync` method checks every reference field in the M11
`PortabilityDocTypes` reference map (or the doc's own definition for the four
non-M11 docs) against the target instance. A reference is **absent** if the
target kind/id does not exist in the target. The `UserPortabilityAbsentReference`
record carries the **kind** (e.g. `"Group"`), the **field** (e.g.
`"GroupId"`), and the **value** (the id in the archive that is absent in the
target) — this is the data the U07/U08 resolve-review UI renders as the
"add elsewhere → pick a target" picker.

### 2.4 Clean/duplicate/conflict classification contract (D4)

> **The exact classification rules (U04's `ClassifyAsync` copies verbatim;
> the U11 acceptance test asserts exactly these).**

1. **(a) `clean`** — the entity's business key does not match any existing
   entity in the target, **and** every reference field (per the M11
   `PortabilityDocTypes` reference map or the doc's own definition) resolves
   to an existing entity in the target. The entity is ready to apply.

2. **(b) `duplicate`** — the entity's business key **does** match an existing
   entity in the target (matched by the §2.3 business keys). The entity is not
   applied; the resident is informed it already exists. The
   `UserPortabilityEntityClassification.DuplicateId` carries the existing
   entity's id.

3. **(c) `conflict`** — the entity's business key does not match an existing
   entity, but **at least one** reference field (per the M11 reference map)
   does not resolve to an existing entity in the target. The
   `UserPortabilityEntityClassification.AbsentReferences` carries the
   closed list of absent references (each: kind + field + value).

**Pre-write pin (C-M27·5):** the classification runs to completion **before
any write**. `ClassifyAsync` returns the `UserPortabilityImportPlan` without
writing any rows. A `conflict` entity never auto-applies.

**The `conflict` reason shape (the U07/U08 resolve-review renders this):**
each `UserPortabilityAbsentReference` in the `AbsentReferences` list carries:
- `Kind` — the target doc type (e.g. `"Group"`, `"Tag"`, `"Component"`, `"Page"`)
- `Field` — the reference field on the entity (e.g. `"GroupId"`, `"TagIds"`, `"ComponentId"`)
- `Value` — the id in the archive that is absent in the target

The resolve-review UI renders this as: *"This post references group
`{Value}`, which does not exist here. Add it to a group you are a member of,
or discard it."*

### 2.5 ResolveAsync fail-closed apply contract (D4)

> **The exact apply rules (U06's `ResolveAsync` copies verbatim; the U11
> acceptance test asserts exactly these).**

1. **`clean` entities** are applied in the M11 §inventory import order
   (parents before children — the D7 order; the uniform loop, not per-type
   code), reusing the M11 `PortabilityApplyDocuments` / `PortabilityApplyMedia`
   partials. No resident decision is needed for clean entities.

2. **`duplicate` entities** are **not applied** (the existing entity in the
   target is the one the resident already has). The resident's decision is
   informational (acknowledged, not applied). The
   `UserPortabilityImportResult.DiscardedCount` includes the duplicate count.

3. **`conflict` entities** — the resident's decision:
   - **`AddElsewhere`**: the entity is applied with the absent reference
     re-pointed to `PickedTargetId` (the target the resident chose). The
     apply writes the entity through the resident's own write lane for that
     doc family (the post/event/project/document/page create seams — **not**
     a new bulk importer), with the re-pointed reference. The resident must
     have standing over `PickedTargetId` (a group they are a member of, a
     tag they own, a component they are a member of) — a `PickedTargetId`
     the resident has no standing over is a **fail-closed rejection**
     (the entity is not applied; the failure is recorded in
     `UserPortabilityImportResult.Failures`).
   - **`Discard`**: the entity is **not applied** (no write). The
     `UserPortabilityImportResult.DiscardedCount` includes the discarded
     count.

4. **No-auto-merge pin (C-M27·4):** a `conflict` entity the resident did
   **not** resolve (no `UserPortabilityEntityResolution` entry for it) is
   **not applied**. There is no default, no fallback, no auto-re-point.

5. **Fail-closed contract (C-M27·4):** a mid-apply failure (a
   `PickedTargetId` the resident has no standing over, a write error, a
   session timeout) is the **documented rollback path** — the transaction
   rolls back, **no rows** are written, and the
   `UserPortabilityImportResult.Ok` is `false` with the failure set. A
   rejected / aborted resolve writes **nothing**.

6. **One `AccessAudit` row** (`Via = Owner`, `TargetKind "portability"`,
   verb `import.resolve`) emitted by the **service** (the controller adds
   none — the ADR 0105 `messaging.toggle` shape).

### 2.6 Pinned seam tests (exact names)

> **The three acceptance tests** (the closed-loop / handoff / part-vs-whole
> shape, per the design-doc template) + the Web-surface pins. The Core tests
> are `PostgresFixture` (the `postgres:18` Testcontainers) + a media
> temp-dir; the Web pins are NSubstitute (no Postgres). U09/U10 implement
> these exact names; U11 records the gate.

**Core acceptance tests (U09, `tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs`):**

- **`UserPortabilityRoundTrip_ExportThenImportPreservesResidentContentAndMediaAndRoles`**
  (D8a, the "handoff") — plant a resident's authored footprint (a `Post` +
  reply + translation in a group the resident is a member of, an `Event` they
  authored, a `TodoItem`, a `Conversation` + `Message`, a `Document`, a `Page`
  they authored, their `Profile` bio/tags, their media) → **export** at the
  resident scope → import into a **fresh** instance where those groups/pages
  *do* exist → the clean entities are restored referentially intact.

- **`UserPortabilityNoSecretAndSelfScoped_ArchiveContainsNoCredentialMaterialAndOnlyResidentAuthoredRows`**
  (D8b, the "closed-loop" on the trust boundary + scope) — the exported
  archive contains **no** credential material (the `PortabilityPrincipal`
  POCO has **no** such field **and** a byte-scan witness over the archive
  finds none) **and** contains **only** the resident's authored/owned rows
  (another resident's `Post` / `Message` is **absent** — the D1 scope pin).

- **`UserPortabilityConflictResolution_UnresolvedConflictEntitiesAreNotAppliedAndDiscardedWriteNothing`**
  (D8c, the "part-vs-whole") — import into a target where the resident's
  group/page *do not* exist → the `conflict` entities are **not**
  auto-applied, the resident resolves each (add-elsewhere re-points to a
  member-of target and applies; discard drops it) and a discarded entity
  writes **nothing** (the whole import is the resident's choice per entity,
  not a silent auto-merge).

**Web-surface pins (U10, `tests/Kumunita.Web.Tests/UserPortabilityControllerTests.cs`):**

- **`UserPortabilityControllerTests`** (the class) — the resident gate
  (a non-resident / a resident reaching for someone else's data gets no
  surface), the one-audit-row-per-write (`Via = Owner`,
  `TargetKind "portability"`, the `export` / `import` / `import.resolve`
  verbs), the reads-emit-none (index + resolve-review emit **no**
  `AccessAudit` row), the fail-closed render (a failed import shows the
  closed-failure-set), the kw-l keys resolve (the parity).

### 2.7 Acceptance gate (U11 records)

> **The gate section is appended to this design doc by U11** (the "run the
> record" step, split from U09/U10 so the test-authoring units stay atomic).
> If a Core test's runtime (Postgres-boot + a media temp-dir) is not yet
> present, the spec is authored (mirroring the M3/M4 author-not-run
> precedent) and *not* run — the gap is recorded in the design doc + the
> handoff note + the next unit who lands the runtime records the pass count.

The three acceptance tests from §2.6 pinned-tests + the Web-surface pins are
the closed-loop / handoff / part-vs-whole evidence. U11 appends:

```
### Run result (M27 acceptance gate — <date>)

| Test | Status |
|---|---|
| `UserPortabilityRoundTrip_ExportThenImportPreservesResidentContentAndMediaAndRoles` | pass / red |
| `UserPortabilityNoSecretAndSelfScoped_ArchiveContainsNoCredentialMaterialAndOnlyResidentAuthoredRows` | pass / red |
| `UserPortabilityConflictResolution_UnresolvedConflictEntitiesAreNotAppliedAndDiscardedWriteNothing` | pass / red |
| `UserPortabilityControllerTests` (Web pins) | pass / red |

One line per any `## U<m> — Drift pause` section in the handoff note
(each resolved or still open).
```

### Run result (M27 acceptance gate — 2026-10-07)

> **Recorded by U11** (the "run the record" step, split from U09/U10 so the
> test-authoring units stay atomic). **Runtime present** — both suites ran
> green; no author-not-run gap (the M3/M4 precedent did not fire). Build:
> `dotnet build Kumunita.slnx -c Debug` green (0 errors). Runner: xunit.v3
> in-process (`dotnet exec <assembly.dll> -class <FQCN>`), per the AGENTS.md
> test-runner quirk (VS Test Explorer / `dotnet test` discovery broken here).
>
> **Name note (source-of-truth):** the three Core acceptance tests authored
> by U09 (and run green here) carry the `M27_Acceptance_*` names recorded in
> the U09/U11 handoff entries, not the `UserPortability*` illustrative names
> the §2.6 template prose above shows. The gate below records the **actual
> run** — the exact names in
> `tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs`.

| Test | Status |
|---|---|
| `M27_Acceptance_RoundTrip_ResidentFootprintRestored` (D8a, the "handoff") | **pass** (the xunit.v3 in-process run reports `Total: 3, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`) |
| `M27_Acceptance_NoSecret_SelfScoped` (D8b, the "closed-loop") | **pass** (same `Total: 3, … Failed: 0` run) |
| `M27_Acceptance_PerEntityConflict_Resolve` (D8c, the "part-vs-whole") | **pass** (same `Total: 3, … Failed: 0` run) |
| `UserPortabilityControllerTests` (Web pins, 7) | **pass** (the 6 `[Fact]` pins + the 7-case `Web_KwL_KeysResolve` `[Theory]` = `Total: 13, Errors: 0, Failed: 0, Skipped: 0, Not Run: 0`) |

**Drift pauses (one line each, the `## U<m> — Drift pause` sections in the
handoff note — all **resolved**, none still open):**

- **U00 — Drift pause #1** (the ADR index ran to `0145`, not `0147`; the
  `0146`/`0147` ADR files were on disk but unindexed) — **resolved** (user
  chose Option A: backfill the two index rows, then add `0148` after `0147`).
- **U00 — Drift pause #2** (four in-scope docs — `InventoryItem` /
  `Document` / `DocumentFolder` / `Bookmark` — are outside the M11 frozen
  44-doc set, and `Tag`'s ownership field is `CreatedBy`) — **resolved**
  (user chose Option A: keep all four in scope, sourced from each doc's own
  definition + a note that they sit outside the frozen set; the `Tag` field
  is `CreatedBy`. Both refinements are recorded in §2.8 drift-guard entries
  1 + 2.)

### 2.8 Drift-guard (frozen once written)

**The frozen pins** (the U01–U12 units copy verbatim from this doc):
the §2.1 manifest-marker (the `Scope?` + `ResidentSubjectId?` additive fields,
the `null`-means-whole-instance note); the §2.2 scope-inventory (the in-scope
doc types + the per-doc ownership field + the excluded list); the §2.3
business-keys (the per-kind duplicate matcher keys + the reference-
availability report shape); the §2.4 classification (the three classification
rules + the `conflict` reason shape); the §2.5 apply (the six apply rules +
the no-auto-merge pin + the fail-closed contract); the §2.6 pinned-tests (the
four test names); the §kw-l (the 7 key names).

**The drift log** (the source-driven refinements U00 locked **in favor of
the source** where the register's prose was imprecise):

1. **Four in-scope docs are not in the M11 44-entry frozen inventory.**
   `InventoryItem` (M16, `InventoryItem.AuthorId`), `Document` (M21,
   `Document.OwnerId`), `DocumentFolder` (M21, `DocumentFolder.OwnerId`),
   and `Bookmark` (M17, `Bookmark.OwnerId`) are named in the plan's D1
   scope list but are **not** entries in the M11 `PortabilityDocTypes`
   44-doc set (they shipped in later milestones, after M11's inventory was
   frozen). Their ownership fields are sourced from their respective doc
   definitions (not the M11 reference map). The register's D1 "read from the
   M11 `PortabilityDocTypes` source" instruction is refined for these four:
   the ownership field is read from the doc's own definition, and the doc is
   explicitly noted as **outside** the M11 frozen set. The remaining ~25
   in-scope docs keep their ownership field sourced from the M11
   `PortabilityDocTypes` reference map, exactly as the plan says.

2. **The `Tag` ownership field is `CreatedBy`, not an M11 reference-map
   field.** The M11 `PortabilityDocTypes` reference map lists **no**
   reference fields for `Tag` (it is in the inventory as a base unit with
   `Array.Empty<PortabilityReferenceField>()`). The plan's D1 names "Tag
   they created" as in-scope. The ownership field is `Tag.CreatedBy`
   (read from `src/Kumunita.Core/Tags/Tag.cs`), not an M11 reference-map
   field. This is the same class of drift as entry 1: the plan names the
   doc as in-scope, the ownership field is sourced from the doc's own
   definition.

**The drift-guard rule** (the unit-series rule): a unit that finds the
source has moved beyond a frozen pin records the drift **here** (an append,
not a rewrite) and resolves in favor of the source; it never silently
re-derives a pin from a stale prose. The closed scope inventory
(§2.2) is the **exact** set — a future lane that adds a
`*DocTypes` document the resident can author moves the §2.2
list + the U09 round-trip plant **together**, and records the drift here.

## §kw-l — the `myportability.*` key list (D9)

The **exact** locked `myportability.*` keys (U07's index / export / import;
U08's resolve-review; the closed-key registry + the
`KnownTranslationKeys_ParityTests` enforces the 4-language set). This is
**deliberately distinct from M11's admin `portability.*` display keys**
(`portability.export` / `portability.import` / `portability.index.title` on
`/admin/portability`) so the resident surface never collides with the operator
surface. M27 may *reuse* M11's `portability.export` / `portability.import`
button labels **if** the copy is identical, but the resident-specific strings
below are all `myportability.*`. The audit `TargetKind` `"portability"` + the
`export` / `import` / `import.resolve` verbs are **audit values** (not display
keys) and are unchanged.

| Key | Use |
|---|---|
| `myportability.index.title` | the `/account/portability` index page title |
| `myportability.export` | the export button label (the resident's own data) |
| `myportability.import` | the import upload form label |
| `myportability.import.resolve` | the resolve-review submit label ("Apply my choices") |
| `myportability.resolve.add_elsewhere` | the per-entity "add elsewhere" action label |
| `myportability.resolve.discard` | the per-entity "discard" action label |
| `myportability.status` | the status area (the success / conflict-summary line) |

Each × **4 languages** (`en` / `de` / `fr` / `da`), the closed-key registry
shape (the ADR 0015 `KnownTranslationKeys` pin).

## M27 — Closed (recorded)

**Recorded:** 2026-10-07 (U13 close unit). M27 is **shipped**; the close
satisfies the C-M27·7 zero-new-authorization-surface pin (U12 confirmed the
`IAuthorizationService` surface count is unchanged — 8 methods) and unblocks
**M28 (guardian time limits)** (M27 `StatusDone` / M28 `StatusNext`).

- **The three acceptance tests (U09, run green at U11's gate — the source-of-truth
  names, not the §2.6 illustrative `UserPortability*` prose):**
  - `M27_Acceptance_RoundTrip_ResidentFootprintRestored` (D8a, the "handoff") — **pass**.
  - `M27_Acceptance_NoSecret_SelfScoped` (D8b, the "closed-loop") — **pass**.
  - `M27_Acceptance_PerEntityConflict_Resolve` (D8c, the "part-vs-whole") — **pass**.
  - Web-surface pins: `UserPortabilityControllerTests` (7, the 6 `[Fact]` pins +
    the 7-case `Web_KwL_KeysResolve` `[Theory]`) — **pass**.

- **The total M27 test count (honest "Closed" basis, U11 gate + U13 exit run).**
  Core `Kumunita.Core.Tests`: the 3 `M27_Acceptance_*` tests — **Total: 3, Errors: 0,
  Failed: 0, Skipped: 0, Not Run: 0** (`postgres:18` Testcontainers the long pole).
  Web `Kumunita.Web.Tests`: `UserPortabilityControllerTests` — **Total: 13, Errors: 0,
  Failed: 0, Skipped: 0, Not Run: 0** (the 7 `myportability.*` keys × 4 languages is
  the `Web_KwL_KeysResolve` `[Theory]`). **Both full suites green** at close (U13
  exit run, xunit.v3 in-process).

- **The ADR pointer.** `docs/adr/0148-user-scoped-portability.md` — the decision
  record for this lane (Status: **Accepted**); the ADR index (`docs/adr/README.md`)
  carries the 0148 row (appended after the backfilled 0146 / 0147 rows, per the U00
  drift resolution). The 8 invariants (C-M27·1–·8), the 5 FACES (F1–F5), the D1
  closed resident-scope inventory (§2.2), the D4 classify + resolve model (§2.4/§2.5),
  and the D9 `myportability.*` 7-key closed set are the ADR's normative core; the
  design doc is the reference tier.

- **The D9 deferred-lane list (each named; a future ADR per lane, none resolved by an
  M27 unit):**
  1. **Cross-neighborhood federation** — the `README §Deferred` OpenIddict lane (M27
     is a single-neighborhood resident's own data, not cross-community identity).
  2. **Backup-automation / cron surface** — the `OPS.md` operator lane (M27 is the
     resident's on-demand export/import, not a scheduled backup).
  3. **Resident delete-all-their-data** — a future ADR (M27 is export/import, not
     deletion; the author soft-delete precedent, ADR 0024, is the model to follow).
  4. **Import-merge beyond the per-entity resident choice** — community-level
     reconciliation, own ADR (M27's `ResolveAsync` is the resident's per-entity
     add-elsewhere / discard, never an automatic merge).

- **The `Milestones.cs` flip (U13).** M27 `StatusNext` → `StatusDone`; M28
  `StatusPlanned` → `StatusNext`; the order is **unchanged** (`…"M24", "M25", "M26",
  "M27", "M28"` — the "named lane, not a renumber" precedent). `MilestonesTests.cs`
  is re-pinned to match: `Shipped_Milestones_Are_Marked_Done` gains `M27`, and the
  single-in-progress test is renamed
  `M27_Is_The_Single_InProgress_Milestone` → `M28_Is_The_Single_InProgress_Milestone`
  + re-pointed to assert `M28` (with `M27` now in the done list). The order pin
  (`Roadmap_Covers_M0_Through_M28_Plus_Named_Lanes_In_Order`) is **untouched**.

- **The M28 handoff (C-M27·7) — a closed-loop artifact.** M28 (guardian time limits)
  starts **only after** M27 `StatusDone` **and** M28 `StatusNext`. M27 is a
  *resident-scoped export/import* surface over the existing content lanes; M28 is a
  *child-scoped schedule* on the GU guardian-controls lane (ADR 0028) — the two do
  not collide on the seams named above. The GU `GuardianLink` + the GU supervision
  seams stay frozen for any M28 schedule surface; M27's `IUserPortabilityService`
  seam (the M11 context, not a new bounded context — D7) is untouched by M28. M28's
  own register + unit plans follow the same sealed-unit shape as this one.
