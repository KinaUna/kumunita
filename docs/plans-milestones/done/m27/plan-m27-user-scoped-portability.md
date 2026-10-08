# Plan: M27 — User-scoped portability (resident self-export + per-entity conflict-resolution import)

> **In progress.** Unit register (secondary tier). This file is the sealed unit
> register: one row per unit, each with Goal / Entry reads / Deliverables / Exit,
> sized for **~32K-context agents** (≤ ~5 files, ≤ ~400 LOC, 3–6 entry reads,
> one build + test run). The authoritative **primary tier** —
> `docs/design/m27-user-scoped-portability-design.md` — is authored by **U00** and
> locked before any code unit runs; the decision record is **ADR 0148** (U00
> verifies the number is free against `docs/adr/README.md`).
>
> **Unit plans (this convention):** each unit ships its own self-contained plan
> file in `docs/plans-milestones/in-progress/` (`m27-u00.md` … `m27-u13.md`).
> A unit agent reads **its own plan file + its entry reads** — it does not
> re-derive this register, which is why each unit plan restates the context it
> needs. **When a unit is done, its plan file is moved from
> `docs/plans-milestones/in-progress/` to `docs/plans-milestones/done/`.**
>
> **Living handoff note (scratch tier):**
> `docs/plans-milestones/in-progress/m27-handoff-notes.md` — one `## U#` section
> per unit, appended (never rewritten). Each unit writes exactly one short
> section before it exits; the next unit reads only that section + its own
> entry-reads list.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract. The `[PROPOSED]` decision set below is the last cheap place to
> change it — after U00 runs it is locked by the design doc + ADR 0148 and
> changeable only via the drift guard.

---

## What M27 is

The **resident's** data becomes portable in and out, *as data*, from the
resident's own perspective — the loop closing into the resident's life (the
ARCHITECTURE.md value chain: "the loop closes into the residents' lives") at
the scale of **one person**, not the whole instance.

M27 is the **resident-scoped** half of the portability world-seam that M11
(ADR 0108) deliberately deferred as a follow-on lane ("per-slice / cross-
instance portability … own ADRs"). M11 shipped the **operator-scoped,
whole-instance** `*.kumunita` archive: a GlobalAdmin moves the *entire*
instance in and out, fail-closed, no-secrets. **M27 reuses that machinery** —
the `Kumunita.Core/Portability/` context (the BCL-only `KumunitaArchive` ZIP
(de)serializer, the `PortabilityManifest` self-describing head, the frozen
`PortabilityDocTypes` 44-doc inventory, the no-secret posture) — but **scopes
it to one resident** and **adds the one mechanic M11 did not have**: on
import, the resident **manually resolves conflicts per entity**, choosing to
**add it elsewhere** or **discard it**, because a resident's data may reference
groups / components / pages / tags that do not fit the target community's
structure and authorization.

**The M11 vs M27 distinction (the load-bearing framing of every unit):**

| Axis | M11 (ADR 0108) — shipped | M27 (this lane) — to ship |
|---|---|---|
| Actor | GlobalAdmin (operator) | the resident themself (`[Authorize]`, verified) |
| Scope | the **whole** instance (44 doc types) | **one resident's authored data** (a closed subset, pinned) |
| Import semantic | **restore** into a fresh instance (fail-closed, all-or-nothing) | **merge with per-entity conflict resolution** (add elsewhere / discard, user-driven) |
| Reference conflicts | rejected (referential integrity, fail-closed) | **surfaced to the resident** as a resolve step (the lane's headline) |
| Secrets | identity graph travels (secrets reset on import) | the resident's own account is the anchor; **no credential material travels** (reuses the M11 no-secret boundary) |
| Audit `Via` | `Admin` | `Owner` (the resident self-lane) |

M27 **owns the M11 deferred lane** the ADR named "per-slice portability (one
user's data out)" + the "import-merge (onto a diverged instance)" fragment —
the two halves M11's C-M11·5 deferred as "follow-on lanes, own ADRs." M27 is
**not** cross-neighborhood federation (the `README.md §Deferred` OpenIddict
lane) and **not** the backup-automation/cron surface (OPS.md) — both stay
deferred.

## What M27 builds on (frozen, verified seams)

- **`Kumunita.Core/Portability/`** (M11, ADR 0108) — `KumunitaArchive`
  (BCL `System.IO.Compression.ZipArchive` read + write over the `manifest.json`
  + `docs/{Type}.json` + `media/{Id[0..2]}/{Id}` layout), `PortabilityManifest`
  (the `format` `kumunita/portability/1` authority + the `DocCounts` +
  `MediaManifest` head), `PortabilityDocTypes` (the frozen 44-entry closed
  inventory as data, the `PortabilityDocEntry`/`PortabilityReferenceField`
  records), `PortabilityPrincipal` (the no-secret identity graph POCO).
- **The `*DocTypes` surfaces** (the per-resident scope is a *subset* of the M11
  closed inventory, filtered by the resident's own `AuthorId`/`OwnerId`/
  `RecipientId`/participant fields — the field map in the M11
  `PortabilityDocTypes` is the source of truth for which field scopes each doc).
- **The content-addressed media store** (ADR 0011, `IMediaStore`/`IMediaFileStore`,
  `MediaObject`) — the resident's images travel the same content-addressed way.
- **The resident self-lane house pattern** — the `/settings/...` sections
  (`LocaleController`, `MessagingSettingsController`), the `AccessVia.Owner`
  audit value, the `[Authorize]` verified-resident gate, the `kw-l` closed-key
  registry (`KnownTranslationKeys` × en/de/fr/da).
- **The milestone-flip contract** — `Milestones.cs` + the README Roadmap +
  `docs/STATUS.md` + `docs/ARCHITECTURE.md` + the `MilestonesTests.cs`
  three-test pin, moved **in the same close unit** (U13).

## Assumptions / decisions — [PROPOSED, lockable by ADR 0148 in U00]

> **Open veto.** These are the decisions the user can still change cheaply —
> **before U00 runs**. After U00 they are locked by
> `docs/design/m27-user-scoped-portability-design.md` + ADR 0148 and changeable
> only via the drift guard.

- **D1 · Scope = the resident's authored data, resident-scoped.** M27 exports
  and imports **one resident's** authored content — the closed subset of the M11
  44-doc inventory the resident **owns** (the `AuthorId`/`OwnerId`/
  `RecipientId`/participant fields, read from the M11 `PortabilityDocTypes`
  reference map). Concretely the in-scope doc families (pinned in the design
  doc §scope-inventory): the resident's `Post` + `PostReply` (+ their
  `PostTranslation`/`ReplyTranslation`), `AnnouncementComment`, `Event`
  (+`EventRsvp` the resident's own) + `EventTranslation`, `ProjectGoal`/
  `Project`/`TodoItem`/`KanbanBoard` (+translations/comments/placements they
  authored), `InventoryItem` they own, `Document` + `DocumentFolder` they own,
  `Conversation` + `Message` they participate in, `Page` they authored, `Tag`
  they created, `Bookmark`, `Profile` bio/tags (their own), and the `Group` +
  `GroupMembership` + `GroupInvitation` + `GroupJoinRequest` rows **for the
  groups the resident created or is a member of** (needed to make their content
  resolvable on import). **Out of scope:** the *operator* instance-identity
  (community name, language catalog, `LocaleSettings` — that is M11's
  `config.json`, the resident does not own it), the other residents' data,
  credential material (the C-M11·2 boundary, unchanged), `AccessAudit` /
  `OutboxEmail` / `EmailDeadLetter` / `IdentityToken` / `AuditPurgeSummary`
  (the M11 "Excluded" set, unchanged). **Alternative considered and rejected:**
  scoping to "everything the resident can *see*" — that is a *read* scope (the
  audience decision), not an *ownership* scope, and would let a resident
  import content they only had delegated/audience access to; M27 scopes to
  **what the resident authored/owns**, the privacy-correct reading of "their
  own data."
- **D2 · The archive reuses M11's format, at the resident scope.** Same BCL-only
  `*.kumunita` ZIP layout + the `PortabilityManifest` head (the `format`
  **authority** stays `kumunita/portability/1` — the archive is still a valid
  M11 archive; M27 adds **no** new format version). M27 adds one additive,
  **optional** manifest field the M11 importer ignores (the resident scope
  marker — `scope: "resident"` + `residentSubjectId`, a `kumunita/portability/1`
  archive without them is a plain whole-instance archive — the ADR 0098/0106
  additive-frozen-surface precedent: existing readers/callers keep compiling,
  a plain M11 archive still imports whole). **No new package** (the lean-stack +
  tsc-only discipline holds).
- **D3 · Export = a resident-scoped filter over M11's closed inventory.**
  `UserPortabilityService.ExportAsync(residentSubjectId)` produces the
  **same** `*.kumunita` archive shape but with the `docs/{Type}.json` arrays
  **filtered to the resident's authored/owned rows** (the D1 subset) + the
  resident's media + the minimal identity reference set (the groups/tags/pages
  their content references, so the archive is self-contained). **No-secrets**
  holds (C-M27·2): the resident's own account row travels as the
  `PortabilityPrincipal` no-secret shape (the credential material is
  structurally absent, the M11 D3 boundary). One `AccessAudit` row
  (`TargetKind "portability"`, `Via = Owner`, verb `export`).
- **D4 · Import = validate-then-merge with per-entity conflict resolution.**
  The M27 **headline mechanic** and the part M11 did not have. On import,
  **before any write**, the service classifies each in-scope entity:
  - **`clean`** — no conflict, ready to apply;
  - **`duplicate`** — an equivalent entity already exists in the target (matched
    by a per-kind **business key** pinned in the design doc §conflict-keys, e.g.
    a `Post` by `(AuthorId, Created, Title?, Body-hash)`, a `Tag` by `Slug`);
  - **`conflict`** — a reference target the entity depends on is absent in the
    target (the group/component/page/tag the resident's data pointed at does not
    exist or the resident is not a member of) — the "may not fit the community's
    structure and authorization" case the README names.
  The resident then **resolves each non-clean entity**: **add it elsewhere**
  (re-point the missing reference to a target they pick — e.g. re-home a post to
  a component/group they *are* a member of; re-attach a tag they own or drop the
  tag) **or discard it**. **Clean** entities can be applied in one batch. A
  rejected / aborted resolve writes **nothing** (fail-closed on the write path,
  C-M27·4 — the M11 C-M11·4 posture carried to the merge). **Alternative
  considered and rejected:** auto-merge (a deterministic best-effort re-point) —
  it silently re-assigns the resident's content to a community/authorization
  scope they did not choose, violating ADR 0001-B (the author's choice is
  absolute); the README is explicit that the *user* resolves conflicts
  "choosing per entity whether to add it elsewhere or discard it."
- **D5 · The surface is the resident's own, thin + audited + quiet.**
  `UserPortabilityController` at `/account/portability` (the resident's
  account surface — the `/settings/...` house pattern, `[Authorize]`, verified
  resident; the **resident's own** data only, so there is no GlobalAdmin
  break-glass and no audience decision — the ADR 0105/0118 personal-by-id
  posture): `GET /account/portability` (the index: the export button + the
  import upload + the resolve-review area), `GET /account/portability/export`
  (streams the `*.kumunita`), `POST /account/portability/import` (validate +
  classify), `POST /account/portability/import/resolve` (apply the resident's
  per-entity decisions). Each write emits **one** `AccessAudit` row
  (`Via = Owner`, `TargetKind "portability"`, verb `export`/`import`/
  `import.resolve`); reads (index, the resolve-review) emit **none** (the ADR
  0105 "reads never audit" / M6-inbox shape). **Zero new authorization
  surface** (C-M27·7): no new `AccessAction`, no new `AccessVia`, no
  `IAuthorizationService` branch — the resident self-lane gate is the only
  decision.
- **D6 · Zero new authorization surface.** M27 reuses the resident self-lane
  (`[Authorize]` + the resident's own `subjectId`) + the `AccessVia.Owner`
  audit value (already exists — the "self acts as themself" slot). The
  per-entity conflict decision is a **business decision made by the resident
  in the UI**, not an `AccessAction` — it never touches the frozen
  `IAuthorizationService` decision engine. Pinned by the design doc §Seams +
  the U12 handoff (the `IAuthorizationService` surface count unchanged).
- **D7 · Core home = a new resident-scoped service over the M11 context.**
  `Kumunita.Core/Portability/UserPortabilityService` (a new file in the
  **existing** `Kumunita.Core/Portability/` context — M27 is a lane **on** the
  M11 operator surface, the ADR 0109 "named lane on the shipped surface"
  precedent, not a new bounded context) with the `IUserPortabilityService`
  seam: `ExportAsync(residentSubjectId, ct)` → `Stream`;
  `ClassifyAsync(residentSubjectId, Stream, ct)` →
  `UserPortabilityImportPlan` (the closed `clean`/`duplicate`/`conflict`
  classification + the per-entity reference-availability report);
  `ResolveAsync(residentSubjectId, UserPortabilityImportPlan,
  IReadOnlyList<UserPortabilityEntityResolution>, ct)` →
  `UserPortabilityImportResult` (apply only the resident-resolved set,
  fail-closed). No new document, no new `*DocTypes` surface, **no migration**
  (ADR 0004 §B.1 — M27 adds no domain document; the resolve is a *write* over
  existing docs through the resident's own write lanes, re-pointing references
  to targets the resident already has standing over).
- **D8 · Tests: a Core round-trip + the per-entity resolve pins + the Web
  surface pins.** The **three acceptance tests** (the closed-loop / handoff /
  part-vs-whole shape): (a) **round-trip** — plant a resident's authored
  footprint (a `Post` + reply + translation in a group the resident is a member
  of, an `Event` they authored, a `TodoItem`, a `Conversation` + `Message`, a
  `Document`, a `Page` they authored, their `Profile` bio/tags, their media) →
  **export** at the resident scope → import into a **fresh** instance where
  those groups/pages *do* exist → the clean entities are restored referentially
  intact (the "handoff" — the resident's data moves); (b) **no-secret +
  self-scoped** — the exported archive contains **no** credential material and
  **only** the resident's authored/owned rows (another resident's `Post`/
  `Message` is **absent** — the D1 scope pin) (the "closed-loop" on the trust
  boundary + scope); (c) **per-entity conflict resolution** — import into a
  target where the resident's group/page *do not* exist → the `conflict`
  entities are **not** auto-applied, the resident resolves each (add-elsewhere
  re-points to a member-of target and applies; discard drops it) and a
  discarded entity writes **nothing** (the "part-vs-whole" — the whole import
  is the resident's choice per entity, not a silent auto-merge). Plus the Web-
  surface pins (the resident gate, the one-audit-row-per-write, the kw-l keys
  resolve, reads emit no row).
- **D9 · kw-l keys.** The resident-facing strings use a **distinct
  `myportability.*` namespace** (the closed set: `myportability.index.*` /
  `myportability.export.*` / `myportability.import.*` /
  `myportability.resolve.*`) × 4 languages in `KnownTranslationKeys` (the
  parity pins move together, the `KwLRegistryConsistencyTests` closed-set).
  This is **deliberately distinct from M11's admin `portability.*` display
  keys** (`portability.export` / `portability.import` / `portability.index.title`
  on `/admin/portability`) so the resident surface never collides with the
  operator surface; M27 may *reuse* M11's `portability.export`/`portability.import`
  button labels if the copy is identical, but the resident-specific strings
  (the index title, the resolve-review labels, the "add elsewhere" / "discard"
  actions, the status) are all `myportability.*`. The audit `TargetKind`
  `"portability"` + the `export`/`import`/`import.resolve` verbs are **audit
  values** (not display keys) and are unchanged. **Deferred lanes** (named in
  ADR 0148 Consequences, own ADRs): cross-neighborhood federation (the
  `README §Deferred` OpenIddict lane), the backup-automation / cron surface
  (OPS.md), a resident *delete-all-their-data* (a future ADR — M27 is export /
  backup + import, not deletion), and import-merge **beyond** the per-entity
  resident choice (a community-level reconciliation, own ADR).

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M27·1 · The archive reuses the M11 format at the resident scope.** The
  M27 `*.kumunita` archive is the **same** M11 format (the `format`
  authority `kumunita/portability/1` unchanged) + one additive optional
  resident-scope marker in the manifest (D2). A plain M11 whole-instance
  archive still validates/imports unchanged (the ADR 0098 additive-frozen-
  surface precedent). Pinned by the U07 format-compat test + the D2 pin.
- **C-M27·2 · The archive travels the data, never the secrets (carries the
  M11 C-M11·2 boundary).** The M27 archive contains the resident's authored
  data + the resident's media + the minimal no-secret identity reference set.
  It contains **no** credential material (no password hash / security stamp /
  token / recovery code) — the `PortabilityPrincipal` POCO shape is reused
  verbatim. Pinned by the no-secret test (D8b) — the single most load-bearing
  boundary, unchanged from M11.
- **C-M27·3 · Scope is ownership, not read.** The export/import scope is the
  resident's **authored/owned** rows (D1), never "everything they can see."
  Another resident's content — a delegated, audience-visible, or membership-
  visible row the actor is *not* the author/owner of — is **absent** from the
  archive. Pinned by the self-scoped test (D8b) + the D1 closed scope inventory.
- **C-M27·4 · Import is fail-closed on the write path + the resident's
  choice is absolute.** A rejected / aborted resolve writes **nothing**
  (the M11 C-M11·4 posture). On apply, **only** the entities the resident
  resolved are written, re-pointed exactly as the resident chose (add-
  elsewhere → the target they picked; discard → no write). There is **no**
  silent auto-merge of a `conflict` entity to a scope the resident did not
  choose (ADR 0001-B — the author's choice is absolute). Pinned by the
  per-entity conflict test (D8c) + the `ResolveAsync` fail-closed pin.
- **C-M27·5 · Per-entity conflict classification is pre-write.** The
  `clean`/`duplicate`/`conflict` classification + the reference-availability
  report run to completion **before any write** (D4); a `conflict` entity
  never auto-applies — it is surfaced for the resident's decision. Pinned by
  the `ClassifyAsync` pre-write pin + the D4 pin.
- **C-M27·6 · The surface is the resident's own, audited, and quiet.** Export
  + import + resolve are `[Authorize]` verified-resident (`/account/portability`,
  the resident's own data only — no GlobalAdmin break-glass, no audience
  decision, the ADR 0105/0118 personal-by-id posture); each **write** emits
  **exactly one** `AccessAudit` row (`TargetKind "portability"`, `Via = Owner`,
  verb `export` / `import` / `import.resolve`); the **reads** (index, the
  resolve-review) emit **none**. Pinned by the Web-surface pins (D8).
- **C-M27·7 · Zero new authorization surface.** No new `AccessAction`, no new
  `AccessVia`, no `IAuthorizationService.Decide()` branch, no `Audience`. The
  per-entity resolve is a **business decision in the resident's UI**, not an
  authorization decision — the frozen `IAuthorizationService` surface count is
  unchanged. Pinned by the design doc §Seams + the U12 handoff.
- **C-M27·8 · Docs parity holds at the flip (the close unit, U13).**
  `Milestones.cs` (M27 → `StatusDone`, M28 → `StatusNext`), the README
  Roadmap (`M27` line → **Done**, `M28` → **In progress**), `docs/STATUS.md`,
  `docs/ARCHITECTURE.md` all move together **in the same unit**, and
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its three pins passing
  (the order pin, the done-list pin, the single-in-progress pin re-pointed
  M27 → **M28**) — the AGENTS.md doc↔code parity contract.

## FACES — [PROPOSED, U00 locks]

- **F1 · The resident's data is portable out.** A resident downloads one
  `*.kumunita` archive holding *their* authored posts/events/projects/documents/
  pages/messages + their media + the minimal identity reference set — the loop
  closes out of the platform at the resident's scale (C-M27·1/2/3).
- **F2 · Their data comes back intact where it fits.** Importing that archive
  into an instance where their groups/pages *do* exist restores the clean
  entities referentially intact (C-M27·1/4).
- **F3 · Their data never carries a secret.** The archive has no credential
  material; the resident re-authenticates as themself (C-M27·2).
- **F4 · Where it doesn't fit, the resident decides — per entity.** Entities
  whose references don't exist in the target are surfaced as `conflict`; the
  resident adds each elsewhere (re-pointing to a target they are a member of)
  or discards it; nothing is auto-merged and nothing is half-applied
  (C-M27·4/5).
- **F5 · The surface is quiet + audited + the resident's own.** Export /
  import / resolve are the resident's own (no GlobalAdmin break-glass), each
  write leaves exactly one audit row, the reads leave none, and a non-resident
  (or a resident reaching for *someone else's* data) gets no surface
  (C-M27·6/7).

## Approach (three tracks + close)

- **Track A — Docs (U00):** the design doc + ADR 0148. U00 is the sign-off
  gate: lock D1–D9, the C-M27 invariants, F1–F5, the **closed resident-scope
  inventory + the per-kind business keys** (D1 + the §conflict-keys), the
  **manifest resident-scope marker** (D2), the **`clean`/`duplicate`/
  `conflict` classification contract** (D4), the **`ResolveAsync` fail-closed
  apply contract** (D4), the **pinned test names** (U11), the **kw-l key list**
  (U05/U07/U08), and the **deferred-lane list** (D9).
- **Track B — Framework (U01–U02):** `IUserPortabilityService` +
  `UserPortabilityService` shell in the existing `Kumunita.Core/Portability/`
  context (U01); the resident-scope **filter** over the M11 inventory + the
  per-kind **business-key** matchers (U02) — the uniform machinery U03–U08
  loop.
- **Track C — Export (U03):** the resident-scoped document + media export
  (U03), reusing the M11 `KumunitaArchive` writer + the M11 no-secret principal
  shape; the Web index + the `portability.export` audit + the export kw-l keys.
- **Track D — Import (U04–U08):** the **`ClassifyAsync`** clean/duplicate/
  conflict classification + the reference-availability report (U04); the
  **per-entity resolve model** (the `UserPortabilityEntityResolution` POCO set,
  the add-elsewhere / discard shape) (U05); the **`ResolveAsync`** fail-closed
  apply (U06); the Web **import** surface + the resolve-review UI + the
  `portability.import`/`portability.import.resolve` audits + the import kw-l
  keys (U07–U08).
- **Track E — Tests + close (U09–U13):** the Core round-trip / no-secret +
  self-scoped / per-entity-conflict tests (U09); the Web-surface pins (U10);
  the three-test acceptance gate recorded (U11); the kw-l parity + the
  `KnownTranslationKeys` four-language close (U12); **U13 close** — the
  `Milestones.cs` / README / `docs/STATUS.md` / `docs/ARCHITECTURE.md` flip +
  the `MilestonesTests.cs` re-pin (M27 → done, M28 → single in-progress) + the
  unit-plan files → `done/` + the handoff `## Summary`.

## Workflow (three-tier, per-unit)

Same contract as the M11/M12/M26 registers: **primary tier** =
`docs/design/m27-user-scoped-portability-design.md` (authored by U00; the only
authority after it lands); **secondary** = this register; **scratch** = the
handoff note (one `## U#` section per unit: entry state / what ran / drift /
open items). Per unit: Goal → Entry reads (3–6 files) → Deliverables (≤ 5
files) → Exit (build green + handoff entry).

**Unit-series rules (the atomicity contract):** (1) a unit never modifies a
file not in its own `Deliverables`; (2) never rewrites the design doc outside
the §drift-guard note; (3) never introduces a test whose exact name is not in
the design-doc §seam-test pin (U11's list); (4) **never opens a new seam on
`IAuthorizationService` / `IUserInfoService` / `IIdentityService`** beyond
what U00 pinned — M27 adds **no** authorization surface (C-M27·7); (5) **never
re-shapes the M11 `PortabilityManifest` / `KumunitaArchive` /
`PortabilityDocTypes` frozen set** outside the D2 additive optional field (U00
pinned); (6) **never auto-applies a `conflict` entity** — the resident's
per-entity choice is the only apply path (C-M27·4); (7) if entry reads reveal
the design doc is out of date, the unit pauses and records
`## U<m> — Drift pause` in the handoff note.

**Tests run per AGENTS.md:** `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
and `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
(the round-trip / no-secret / per-entity tests are **Core** tests — they need
the Testcontainers Postgres + a media temp-dir, the `PostgresFixture` harness
shape; the Web-surface pins are **Web** tests, the NSubstitute no-Postgres
shape). `Kumunita.Core.Tests` takes ~20 s (it starts `postgres:18` via
Testcontainers and leaves Docker containers behind if the process is killed —
clean up with `docker container prune`).

**kw-l key rule (every Web unit that adds a key):** the key must land in **all
four** dictionaries in `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(`EnValues`/`DeValues`/`FrValues`/`DaValues`) or `KnownTranslationKeys_ParityTests`
+ `KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` fail.

**Milestone-flip rule (U13 only, in one unit):** `Milestones.cs` M27 →
`StatusDone` + M28 → `StatusNext`; the README Roadmap M27 line → **Done** + M28
→ **In progress**; `docs/STATUS.md` + `docs/ARCHITECTURE.md` move M27 → done;
**and** `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the order pin is
**unchanged**, the done-list pin gains `M27`, and
`M27_Is_The_Single_InProgress_Milestone` is **renamed + re-pointed** to
`M28_Is_The_Single_InProgress_Milestone` (asserting `M28`). The order of
`Milestones.All` is **unchanged** (`…, M25, M26, M27, M28`).

---

## Units (14 total — U00 … U13)

> **Naming:** unit plan files are `m27-u00.md` … `m27-u13.md` in
> `docs/plans-milestones/in-progress/`, moved to `docs/plans-milestones/done/`
> when the unit is done. The handoff note is
> `docs/plans-milestones/in-progress/m27-handoff-notes.md`.

### U00 — Lock the design: `m27-user-scoped-portability-design.md` + ADR 0148

**Goal.** Author the primary tier + the decision record. The **sign-off gate**
for the whole milestone: lock D1–D9, the C-M27 invariants, F1–F5, the
**closed resident-scope inventory** (D1 — the exact doc-family subset + the
per-doc **ownership field** that scopes it, **read from the M11
`PortabilityDocTypes` reference map**, not invented), the **per-kind business
keys** (the §conflict-keys the `ClassifyAsync` duplicate matcher + the
reference-availability report drive), the **manifest resident-scope marker**
(D2 — the additive optional field, the frozen `format` unchanged), the
**`clean`/`duplicate`/`conflict` classification contract** (D4 — the exact
classification rules + the per-entity reference-availability shape), the
**`ResolveAsync` fail-closed apply contract** (D4 — the exact apply rules +
the resident's per-entity choice shape + the no-auto-merge pin), the **pinned
test names** (U11), the **kw-l key list** (U05/U07/U08), and the **deferred-
lane list** (D9). ADR 0148 records: decisions + alternatives considered
(auto-merge; scope-by-read; a new bounded context; a new format version; a CLI
surface now) + the Consequences hand-off (the deferred lanes: cross-
neighborhood federation, backup-automation/cron, resident delete-all,
community-level import-merge). **U00 verifies the ADR number is free**
(against `docs/adr/README.md` — the index runs 0001–0147, so `0148` is next).

**Entry reads (6).** `docs/design/m11-portability-design.md` (the house style +
the D1/D2/D7/D8/D9 decision shape + the C-M11 invariants + the FACES + the
deferred-lane language — the M27 doc emulates it at the resident scope);
`src/Kumunita.Core/Portability/PortabilityDocTypes.cs` (the **D1 scope source**
— the 44-entry closed inventory + the per-doc reference fields U00 reads the
ownership field from); `src/Kumunita.Core/Portability/PortabilityManifest.cs` +
`KumunitaArchive.cs` (the D2 frozen manifest/archive shape U00 keeps + the
additive-marker precedent); `src/Kumunita.Core/Authorization/Decision.cs`
(the `AccessVia.Owner` value D6 reuses — confirm it is the resident self-lane
slot); `src/Kumunita.Web/Controllers/LocaleController.cs` +
`MessagingSettingsController.cs` (the D5 resident `/settings/...` self-lane
house pattern U00 emulates for `/account/portability`); `docs/adr/README.md`
(the ADR index — confirm `0148` is free + the row shape to add).

**Deliverables (3).** `docs/design/m27-user-scoped-portability-design.md`;
`docs/adr/0148-user-scoped-portability.md`; `docs/adr/README.md` (one index
row, after the 0147 row).

**Exit.** `dotnet build Kumunita.slnx -c Debug` still green (docs only).
Handoff entry: decisions locked/vetoed, any D-item text changed, the
**exact closed resident-scope inventory + the per-doc ownership field** as
written (U01/U02/U04 copy verbatim), the **exact per-kind business keys** as
written (U04's classifier copies verbatim), the **exact manifest resident-
scope marker** as written (U01's manifest add copies verbatim), the **exact
`clean`/`duplicate`/`conflict` classification + `ResolveAsync` apply
contract** as written (U04/U06 copy verbatim), the **ADR number confirmed
free** (`0148` — verified against `docs/adr/README.md`).

---

### U01 — Framework: `IUserPortabilityService` + the resident-scope marker + the shell

**Goal.** D2/D7 rendered as code: the **`IUserPortabilityService`** seam + the
**`UserPortabilityService`** shell (the ctor over the frozen seams + the three
method signatures, the bodies `NotImplementedException` until U02–U06 fill
them) in the **existing** `Kumunita.Core/Portability/` context, plus the
**D2 additive optional manifest field** the resident-scope marker lands on
(the `format` authority + every M11 field **unchanged**). No export/import
*logic* yet (U02–U06 own that); this unit is the uniform machinery they
compose.

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§manifest-marker — the locked D2 additive field; §Seams — the locked
`IUserPortabilityService` shape; §scope-inventory — the locked D1 inventory);
`src/Kumunita.Core/Portability/PortabilityManifest.cs` (the frozen manifest U01
adds the one optional field to — the `PortabilityMediaEntry` + `FormatVersion`
precedent); `src/Kumunita.Core/Portability/PortabilityService.cs` (the
`IPortabilityService` seam + the `PortabilityImportResult` closed-failure-set
shape U01 emulates for `IUserPortabilityService` + `UserPortabilityImportResult`);
`src/Kumunita.Core/Portability/PortabilityDocTypes.cs` (the D1 scope source —
the reference fields U01's service reads the ownership field from);
`src/Kumunita.Core/DependencyInjection.cs` (where to register — next to the
`PortabilityService` registration).

**Deliverables (≤ 4 files).**
- `src/Kumunita.Core/Portability/UserPortabilityService.cs` — the
  `IUserPortabilityService` interface (the `ExportAsync(residentSubjectId, ct)`
  → `Stream` / `ClassifyAsync(residentSubjectId, Stream, ct)` →
  `UserPortabilityImportPlan` / `ResolveAsync(residentSubjectId,
  UserPortabilityImportPlan, IReadOnlyList<UserPortabilityEntityResolution>,
  ct)` → `UserPortabilityImportResult` signatures, the D7 lock) + the
  `UserPortabilityImportPlan` + `UserPortabilityImportResult` +
  `UserPortabilityEntityResolution` POCO records (the closed shapes, U00's §Seams
  verbatim) + the `UserPortabilityService` shell (the ctor + the three methods
  as `NotImplementedException` until U02–U06).
- `src/Kumunita.Core/Portability/PortabilityManifest.cs` — the **one**
  additive optional field (the D2 resident-scope marker: `Scope?` +
  `ResidentSubjectId?` — `null` = a plain M11 whole-instance archive, the
  ADR 0098 additive-frozen-surface precedent; the `FormatVersion` + every
  existing field **unchanged**).
- `src/Kumunita.Core/DependencyInjection.cs` — the `IUserPortabilityService`
  registration (next to the `IPortabilityService` registration, the same shape).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. A new M11 archive (no
resident marker) still deserializes (the additive field is `null`) — the
U11 format-compat pin's precondition. Handoff entry: the seam signatures
(verbatim), the `PortabilityManifest` additive field (verbatim + the
`null`-means-whole-instance note), the three POCO record shapes, the
`IAuthorizationService` surface count (**unchanged** — the C-M27·7 pin),
any compile warnings.

---

### U02 — Framework: the resident-scope filter + the per-kind business keys

**Goal.** D1 rendered as the **resident-scope filter** (which doc families +
which ownership field scopes each) + the **per-kind business-key** matchers
(D4's `duplicate` matcher + the `conflict` reference-availability report both
drive off these). Pure, over the frozen M11 `PortabilityDocTypes` inventory —
no service logic, no writes, no new document. This is the "what belongs to the
resident" + "what is the same entity" map U03 (export filter) and U04
(classification) loop.

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§scope-inventory — the locked D1 inventory + the per-doc ownership field;
§conflict-keys — the locked per-kind business keys);
`src/Kumunita.Core/Portability/PortabilityDocTypes.cs` (the 44-entry closed
inventory + the reference fields U02 reads the ownership field from — the
**source of truth**, not re-invented); `src/Kumunita.Core/Portability/
KumunitaArchive.cs` (the archive read shape U02's filter iterates — confirm the
generic `docs/{Type}.json` array read the filter plugs into);
`src/Kumunita.Core/Portability/PortabilityService.cs` (the M11 export loop the
U02 filter mirrors at the resident scope — the per-type isolation U02 reuses);
`src/Kumunita.Core/Portability/UserPortabilityService.cs` (U01's shell — the
seam U02's filter + matchers are composed by).

**Deliverables (≤ 4 files).**
- `src/Kumunita.Core/Portability/UserScopeInventory.cs` — the D1 closed
  resident-scope inventory as **data** (the `Type` → **ownership field** map
  (`Post`→`AuthorId`, `PostReply`→`AuthorId`, `Event`→`AuthorId`,
  `TodoItem`→`AuthorId`, `InventoryItem`→`AuthorId`, `Document`→`OwnerId`,
  `Bookmark`→`OwnerId`, `Message`→`SenderId`∪`Conversation`-participant,
  `Profile`→the resident's own `SubjectId`, `Group`→`OwnerId`∪member, + the
  minimal identity reference set) + the **excluded** list (the C-M27·3
  ownership-not-read pin — the other residents' rows, the operator
  `config.json` state, the M11 "Excluded" set) — the U03/U04 loops iterate this.
- `src/Kumunita.Core/Portability/UserBusinessKeys.cs` — the D4 per-kind
  business-key matchers (the `duplicate` matcher: `Post`→`(AuthorId, Created,
  Title?, Body-hash)`, `Tag`→`Slug`, `Document`→`(OwnerId, Created,
  Title?, hash)`, + the closed set U00 pinned — a pure
  `bool Matches(a, b)` over the POCO set, no session, no write).
- `src/Kumunita.Core/Portability/UserPortabilityService.cs` — the
  **filter** + **matcher** composition methods (the `ScopeFilter(docs,
  residentId)` + the `MatchBusinessKey(kind, a, b)` seams U03/U04 call — the
  bodies filled, the three interface methods still `NotImplementedException`
  until U03–U06).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green. The filter returns
**exactly** the resident's rows for a seeded two-resident fixture (a U09 test
precondition). Handoff entry: the D1 inventory + ownership fields (verbatim),
the per-kind business keys (verbatim), the excluded list (verbatim), the
`IAuthorizationService` surface count (unchanged), any compile warnings.

---

### U03 — Export: the resident-scoped document + media export

**Goal.** D3 rendered as code: `UserPortabilityService.ExportAsync` — produce
the **same** `*.kumunita` archive shape as M11 but with the `docs/{Type}.json`
arrays **filtered to the resident's authored/owned rows** (U02's filter) + the
resident's media + the minimal no-secret identity reference set, the
`manifest.json` carrying the D2 resident-scope marker. Reuses the M11
`KumunitaArchive` writer + the M11 no-secret `PortabilityPrincipal` shape
verbatim. One `AccessAudit` row (`TargetKind "portability"`, `Via = Owner`,
verb `export`).

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§scope-inventory + §manifest-marker + §export — the locked D1/D2/D3 contract);
`src/Kumunita.Core/Portability/PortabilityExportDocuments.cs` +
`PrincipalsExport.cs` (the M11 export loops U03 reuses — the per-type `docs/
{Type}.json` array write + the no-secret principal extractor);
`src/Kumunita.Core/Portability/MediaExport.cs` (the M11 media loop U03 reuses
at the resident scope); `src/Kumunita.Core/Portability/ManifestFinalize.cs`
(the manifest finalize U03 calls + sets the D2 marker);
`src/Kumunita.Core/Portability/UserScopeInventory.cs` + `UserBusinessKeys.cs`
(U02's filter + matchers U03 composes).

**Deliverables (≤ 4 files).**
- `src/Kumunita.Core/Portability/UserPortabilityService.cs` — the
  `ExportAsync` body (the U02 scope-filter over the M11 per-type export loop +
  the resident media + the no-secret principal + the D2 marker in the manifest
  + the one `AccessAudit` row `Via = Owner` verb `export` — reusing the M11
  export partials, **not** re-inventing the writer).
- `tests/Kumunita.Core.Tests/UserPortabilityExportTests.cs` — the export
  pins (the self-scoped pin: another resident's row is **absent**; the
  no-secret pin: the `PortabilityPrincipal` POCO has no credential field + a
  byte-scan witness; the D2 marker present; the plain-M11-archive still
  deserializes) — the names pinned in the design doc §seam-test list (U11).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the export pins
discovered. Handoff entry: the `ExportAsync` loop note ("reuses the M11
`PortabilityExportDocuments`/`MediaExport`/`PrincipalsExport` partials + the
U02 scope filter"), the D2 marker field (verbatim), the one-audit-row shape
(`Via = Owner`, `TargetKind "portability"`, verb `export`), the
`IAuthorizationService` surface count (unchanged), any compile warnings.

---

### U04 — Import: the `ClassifyAsync` clean/duplicate/conflict classification

**Goal.** D4's **classification** half rendered as code:
`UserPortabilityService.ClassifyAsync` — read the uploaded resident archive,
and for each in-scope entity classify it `clean` / `duplicate` / `conflict`
(the U02 business-key matcher for `duplicate`; the reference-availability
report for `conflict` — the group/component/page/tag the entity points at that
is absent in the target or the resident is not a member of). **Before any
write** (C-M27·5). No apply yet (U06); this unit is the "what fits, what
doesn't, what is already here" report the resident's resolve-review (U07)
renders.

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§classification — the locked `clean`/`duplicate`/`conflict` rules + the
reference-availability shape; §conflict-keys — the locked per-kind business
keys); `src/Kumunita.Core/Portability/PortabilityValidate.cs` (the M11
validate-then-apply + the referential-integrity loop U04 reuses as the
`conflict` detector); `src/Kumunita.Core/Portability/UserBusinessKeys.cs` +
`UserScopeInventory.cs` (U02's matchers + the ownership field U04's
reference-availability report reads);
`src/Kumunita.Core/Portability/KumunitaArchive.cs` (the archive read U04
classifies over); `src/Kumunita.Core/Portability/UserPortabilityService.cs`
(U01's seam + U02's filter/matchers — the `ClassifyAsync` body slot).

**Deliverables (≤ 4 files).**
- `src/Kumunita.Core/Portability/UserPortabilityService.cs` — the
  `ClassifyAsync` body (the per-entity `clean`/`duplicate`/`conflict`
  classification + the reference-availability report + the
  `UserPortabilityImportPlan` assembly — **no writes**, the C-M27·5 pre-write
  pin; the `conflict` reason carries the **absent reference target kind + id**
  so U07's resolve-review can offer "add elsewhere → pick a target").
- `tests/Kumunita.Core.Tests/UserPortabilityClassifyTests.cs` — the
  classification pins (the `clean` happy path; the `duplicate` business-key
  match; the `conflict` absent-reference report; the no-write pin — zero rows
  after `ClassifyAsync`) — the names pinned in the design doc §seam-test list
  (U11).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the classify pins
discovered. Handoff entry: the classification rules (verbatim), the
reference-availability shape (verbatim — the absent-reference kind + id the
resolve-review renders), the `conflict` reason shape (verbatim), the no-write
pin, the `IAuthorizationService` surface count (unchanged), any compile
warnings.

---

### U05 — Import: the per-entity resolve model (add-elsewhere / discard)

**Goal.** D4's **resolve model** rendered as code: the
`UserPortabilityEntityResolution` POCO set (U01's shell declared it; U05 pins
the closed shape) — the resident's per-entity decision: **`AddElsewhere`**
(the re-point: the absent reference target kind + the **target id the resident
picked** — a group/component/page/tag they *are* a member of / own) or
**`Discard`** (no write). The closed `ResolutionKind` set + the
`UserPortabilityImportResult` closed-failure shape. Pure model + the
`ResolveAsync` **input contract** (U06 fills the apply). No apply logic yet.

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§resolve — the locked `ResolutionKind` set + the `AddElsewhere` re-point
shape + the `Discard` no-write pin + the `UserPortabilityImportResult` closed-
failure set); `src/Kumunita.Core/Portability/PortabilityService.cs` (the
`PortabilityImportResult` closed-failure-set shape U05 emulates for
`UserPortabilityImportResult`); `src/Kumunita.Core/Portability/
UserPortabilityService.cs` (U01's seam + the `UserPortabilityEntityResolution`
shell U05 pins); `src/Kumunita.Core/Portability/UserPortabilityClassifyTests.cs`
(U04's classify pins — the `conflict` reason shape U05's resolve model must
accept verbatim); `src/Kumunita.Web/Controllers/LocaleController.cs` (the
resident self-lane POST shape U05's resolve contract emulates — the
`[Authorize]` + the form-bound decision, no anti-forgery token needed for the
resolve POST — confirm the house pattern).

**Deliverables (≤ 4 files).**
- `src/Kumunita.Core/Portability/UserPortabilityService.cs` — the
  `UserPortabilityEntityResolution` POCO closed shape (the `ResolutionKind`
  enum `AddElsewhere`/`Discard` + the `AddElsewhere` re-point fields (the
  absent-reference kind + the **picked target id**) + the `EntityKey` the
  decision is keyed on) + the `UserPortabilityImportResult` closed-failure
  shape (the D4 no-auto-merge + the apply-failure set) — the `ResolveAsync`
  body still `NotImplementedException` until U06.
- `tests/Kumunita.Core.Tests/UserPortabilityResolveModelTests.cs` — the
  resolve-model shape pins (the closed `ResolutionKind` set; the `AddElsewhere`
  re-point shape; the `Discard` no-write pin; the `UserPortabilityImportResult`
  closed-failure set) — the names pinned in the design doc §seam-test list
  (U11).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the resolve-model pins
discovered. Handoff entry: the `ResolutionKind` set (verbatim), the
`AddElsewhere` re-point shape (verbatim), the `Discard` no-write pin, the
`UserPortabilityImportResult` closed-failure set (verbatim), the
`IAuthorizationService` surface count (unchanged), any compile warnings.

---

### U06 — Import: the `ResolveAsync` fail-closed apply (the resident's choice)

**Goal.** D4's **apply** half rendered as code:
`UserPortabilityService.ResolveAsync` — apply **only** the entities the
resident resolved, in the M11 dependency order, re-pointing each `AddElsewhere`
entity's absent reference to the **target the resident picked** (reusing the
resident's **own existing write lane** for each doc family — the post/event/
project/document/page create seams, **not** a new bulk importer), and writing
**nothing** for a `Discard`ed entity. Fail-closed: a mid-apply failure is the
documented rollback path, never a silently-accepted half-apply (C-M27·4). One
`AccessAudit` row (`Via = Owner`, `TargetKind "portability"`, verb
`import.resolve`). **No auto-merge** — a `conflict` entity the resident did
**not** resolve is **not** applied (C-M27·4/5).

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§apply — the locked apply order + the re-point rule + the fail-closed
contract + the no-auto-merge pin); `src/Kumunita.Core/Portability/
PortabilityApplyDocuments.cs` + `PortabilityApplyMedia.cs` (the M11 apply order
+ the media apply U06 reuses); `src/Kumunita.Core/Portability/
PortabilityService.cs` (the M11 `ImportAsync` fail-closed apply shape U06
emulates at the resident scope); `src/Kumunita.Core/Portability/
UserPortabilityService.cs` (U01's seam + U05's resolve model — the
`ResolveAsync` body slot); `src/Kumunita.Core/DependencyInjection.cs` (the
resident write-lane seams U06 composes — confirm they are registered).

**Deliverables (≤ 4 files).**
- `src/Kumunita.Core/Portability/UserPortabilityService.cs` — the
  `ResolveAsync` body (the per-resolved-entity apply, the re-point rule, the
  fail-closed contract, the no-auto-merge pin, the one `AccessAudit` row
  `Via = Owner` verb `import.resolve`).
- `tests/Kumunita.Core.Tests/UserPortabilityResolveTests.cs` — the apply pins
  (the `AddElsewhere` re-point applies to the picked target; the `Discard`
  writes nothing; the no-auto-merge pin — an unresolved `conflict` entity is
  absent after apply; the fail-closed pin — a mid-apply failure leaves zero
  new rows) — the names pinned in the design doc §seam-test list (U11).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the resolve pins
discovered. Handoff entry: the apply order (verbatim), the re-point rule
(verbatim), the fail-closed contract (verbatim), the no-auto-merge pin, the
one-audit-row shape (`Via = Owner`, verb `import.resolve`), the
`IAuthorizationService` surface count (unchanged), any compile warnings.

---

### U07 — Web: the `/account/portability` index + the import upload + the export kw-l keys

**Goal.** D5's Web surface, part 1: the `UserPortabilityController`
(`/account/portability` — the resident's own data, `[Authorize]`, verified)
with the index (the export button + the import upload form + the status area)
+ the `GET /account/portability/export` stream action (reusing the M11
`Content-Disposition: attachment` shape) + the `POST
/account/portability/import` (the `ClassifyAsync` delegation) + the export /
import kw-l keys. Mirrors `AdminPortabilityController` in shape but at the
resident scope + `Via = Owner`.

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§surface — the locked D5 route set + the kw-l key list);
`src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (the M11 surface
U07 emulates — the index/export/import shape, the `Content-Disposition` serve
idiom, the one-audit-row-per-write); `src/Kumunita.Web/Controllers/
LocaleController.cs` (the resident `/settings/...` self-lane house pattern U07
emulates for `/account/portability`);
`src/Kumunita.Web/Views/Admin/Portability/Portability.cshtml` (the M11 index
view U07 emulates at the resident scope); `src/Kumunita.Core/Localization/
KnownTranslationKeys.cs` (the closed-key registry + the four-language shape
U07's kw-l keys land in).

**Deliverables (≤ 5 files).**
- `src/Kumunita.Web/Controllers/UserPortabilityController.cs` — the
  `/account/portability` index + `GET /account/portability/export` +
  `POST /account/portability/import` (the `ClassifyAsync` delegation, the
  fail-closed render of the `clean`/`duplicate`/`conflict` report) + the
  export kw-l keys. `[Authorize]` (verified resident), the resident's own
  `subjectId` as the actor, **no** GlobalAdmin gate (C-M27·6/7).
- `src/Kumunita.Web/Views/Account/Portability/Index.cshtml` — the index (the
  export button + the import upload form + the status area — the M11 index
  shape at the resident scope, the resident's own data only).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the resident-
  facing `myportability.*` keys × en/de/fr/da (the `myportability.index.*` +
  `myportability.export.*` + `myportability.import.*` closed set — **distinct
  from M11's admin `portability.*` keys**, the parity pins move together).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the kw-l parity pins
pass (the four-language registry + the view-scan pin). Handoff entry: the
route set (exact, for U08's resolve-review), the kw-l key list (verbatim +
the four-language note), the `Via = Owner` + no-GlobalAdmin note (C-M27·6),
any compile warnings.

---

### U08 — Web: the per-entity resolve-review UI + the `ResolveAsync` POST + the resolve kw-l keys

**Goal.** D5/D4's Web surface, part 2: the **resolve-review** view (the
`conflict` + `duplicate` entities the resident must decide on — each with the
"add elsewhere" picker (re-point the absent reference to a group/component/
page/tag the resident **is** a member of / own) or the "discard" action) +
the `POST /account/portability/import/resolve` (the `ResolveAsync` delegation)
+ the resolve kw-l keys. The resident's **per-entity** choice — the lane's
headline mechanic (C-M27·4).

**Entry reads (5).** `docs/design/m27-user-scoped-portability-design.md`
(§resolve-review — the locked per-entity decision shape + the "add elsewhere"
picker target set + the resolve kw-l key list);
`src/Kumunita.Web/Controllers/UserPortabilityController.cs` (U07's index +
import U08 extends with the resolve POST); `src/Kumunita.Core/Portability/
UserPortabilityService.cs` (U06's `ResolveAsync` seam + U05's
`UserPortabilityEntityResolution` model U08's form binds);
`src/Kumunita.Web/Views/Directory/_AudienceEditor.cshtml` (the group/component
picker U08 reuses for the "add elsewhere" re-point target — the existing
audience/group editor shape, **reused** not re-invented); `src/Kumunita.Core/
Localization/KnownTranslationKeys.cs` (the resolve kw-l keys × en/de/fr/da).

**Deliverables (≤ 5 files).**
- `src/Kumunita.Web/Controllers/UserPortabilityController.cs` — the
  `POST /account/portability/import/resolve` (the `ResolveAsync` delegation,
  the one-audit-row `Via = Owner` verb `import.resolve`, the fail-closed
  render of the `UserPortabilityImportResult`) + the resolve kw-l keys.
- `src/Kumunita.Web/Views/Account/Portability/ResolveReview.cshtml` — the
  per-entity resolve review (each `conflict`/`duplicate` entity with the
  "add elsewhere" picker — the `_AudienceEditor` group/component picker
  **reused** — or the "discard" action; the one form-bound
  `UserPortabilityEntityResolution` set the resolve POST submits).
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the resolve kw-l
  keys × en/de/fr/da (the `myportability.resolve.*` closed set — **distinct
  from M11's admin `portability.*` keys**, the parity pins move together).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the kw-l parity pins
pass. Handoff entry: the resolve-review route (exact), the "add elsewhere"
picker reuse (the `_AudienceEditor` group/component picker, **not** re-invented),
the resolve kw-l key list (verbatim + the four-language note), the
`Via = Owner` note (C-M27·6), any compile warnings.

---

### U09 — Core acceptance tests: the round-trip + no-secret/self-scoped + per-entity conflict

**Goal.** Implement the **three acceptance tests** (the closed-loop / handoff /
part-vs-whole shape) from the design doc §gate (D8) in
`tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs` (the M11
`PortabilityService` round-trip test is the shape to mirror — the
`PostgresFixture` harness + the media temp-dir + the seed-and-assert pattern).
**This unit does NOT author the Web pins** (U10) **and does NOT record the
gate** (U11) — each is a distinct unit for atomic handoff.

**Entry reads (4).** `docs/design/m27-user-scoped-portability-design.md`
(§gate — the locked three-test names + definitions; §seam-test — the full
pinned test-name list U09 implements); `tests/Kumunita.Core.Tests/
PortabilityServiceTests.cs` (the M11 round-trip / no-secret / fail-closed test
shape to mirror — the `PostgresFixture` + the media temp-dir + the seed-and-
assert); `tests/Kumunita.Core.Tests/PostgresFixture.cs` (the harness);
`src/Kumunita.Core/Portability/UserPortabilityService.cs` (U03–U06's
`ExportAsync`/`ClassifyAsync`/`ResolveAsync` U09 drives).

**Deliverables (1 file, new).**
- `tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs` — the **three**
  acceptance tests (the exact pinned names from the design doc §seam-test):
  (a) **round-trip** — plant a resident's authored footprint → export at the
  resident scope → import into a fresh instance where their groups/pages
  *exist* → the clean entities restored referentially intact; (b) **no-secret +
  self-scoped** — the archive has **no** credential material + **only** the
  resident's rows (another resident's `Post`/`Message` absent); (c) **per-entity
  conflict resolution** — import into a target where their group/page *don't*
  exist → the `conflict` entities are **not** auto-applied, the resident
  resolves each (add-elsewhere re-points + applies; discard drops it), a
  discarded entity writes **nothing**.

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the three tests
discovered (the pass/red status recorded for U11's gate). **No gate recorded**
(U11), **no Web pins authored** (U10), **no design-doc edits** (U11). Handoff
entry: the test file path, the three names (verbatim), the three pass/red
counts (for U11 to consume).

---

### U10 — Web-surface pins (the resident gate + the one-audit-row + the reads-emit-none)

**Goal.** Implement the **Web-surface pins** from the design doc §Web-pins
(D8) in `tests/Kumunita.Web.Tests/UserPortabilityControllerTests.cs` (the
NSubstitute no-Postgres shape, the M11 `AdminPortabilityController` test shape
to mirror — the gate + the one-audit-row-per-write + the reads-emit-none). The
resident gate (a non-resident / a resident reaching for someone else's data
gets no surface), the one `AccessAudit` row per write (`Via = Owner`), the
reads (index, resolve-review) emit **none** (C-M27·6), the kw-l keys resolve
(C-M27·6/7). **This unit does NOT record the gate** (U11) — each is a distinct
unit.

**Entry reads (4).** `docs/design/m27-user-scoped-portability-design.md`
(§Web-pins — the locked pin list); `tests/Kumunita.Web.Tests/
AdminPortabilityControllerTests.cs` (the M11 Web-pin shape to mirror — the
gate + the one-audit-row + the fail-closed render); `src/Kumunita.Web/
Controllers/UserPortabilityController.cs` (U07/U08's surface U10 pins);
`src/Kumunita.Core/Authorization/Decision.cs` (the `AccessVia.Owner` value U10
asserts the audit row carries).

**Deliverables (1 file, new).**
- `tests/Kumunita.Web.Tests/UserPortabilityControllerTests.cs` — the Web-surface
  pins (the resident gate; the one-audit-row-per-write `Via = Owner` + the
  `TargetKind "portability"` + the `export`/`import`/`import.resolve` verbs;
  the reads-emit-none; the fail-closed render; the kw-l keys resolve) — the
  names pinned in the design doc §seam-test list (U11).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green + the Web pins
discovered (the pass/red status recorded for U11's gate). **No gate recorded**
(U11). Handoff entry: the test file path, the pin list (verbatim), the pass/
red counts (for U11 to consume), any compile warnings.

---

### U11 — run + record the M27 acceptance gate

**Goal.** Execute and **record** the three-test acceptance gate (U09's Core
round-trip / no-secret / per-entity + U10's Web pins) from the design doc
§gate, *using* U09/U10's tests as the part-vs-whole evidence. The **gate
section** is appended to the design doc (the M2 U10 / M11 U07 analog — the
"run the record" step, split from U09/U10 so the test-authoring units stay
atomic). **If a Core test's runtime (Postgres-boot + a media temp-dir) is not
yet present, the spec is authored (mirroring the M3/M4 author-not-run
precedent) and *not* run — the gap is recorded in the design doc + the handoff
note + the next unit who lands the runtime records the pass count.**

**Entry reads (4).** `docs/design/m27-user-scoped-portability-design.md` §gate
(the three-test names + definitions) + §seam-test (the full pinned list);
`docs/plans-milestones/in-progress/m27-handoff-notes.md` (U09's + U10's
sections — the test results the gate *references*);
`tests/Kumunita.Core.Tests/UserPortabilityAcceptanceTests.cs` (U09's three
tests); `tests/Kumunita.Web.Tests/UserPortabilityControllerTests.cs` (U10's
Web pins).

**Deliverables (1 file, modify).** `docs/design/m27-user-scoped-portability-
design.md` — append `### Run result (M27 acceptance gate — <date>)`: the three
Core test names + their pass/red status, the Web-pin count, one line per any
`## U<m> — Drift pause` section in the handoff note (each resolved or still
open). **No code, no build.**

**Exit.** the gate section is present + consistent with U09/U10's results.
Handoff entry: the three Core test names + pass counts + the Web-pin count +
the date + any still-open drift.

---

### U12 — kw-l parity close + the `KnownTranslationKeys` four-language audit

**Goal.** One final pass over the **kw-l closed set** M27 added (U07's export /
import keys + U08's resolve keys) — confirm every key is in **all four**
`KnownTranslationKeys` dictionaries (`EnValues`/`DeValues`/`FrValues`/`DaValues`),
the `KnownTranslationKeys_ParityTests` + the
`KwLRegistryConsistencyTests.Every_KwL_Key_In_A_View_Is_Registered` pass, and
the resident-facing strings are honest (no `@(...)` attribute-interpolation
trap, no un-awaited async helper — the AGENTS.md Razor-verification doctrine).
**No code, no build beyond the kw-l pins.** The `IAuthorizationService`
surface count is **confirmed unchanged** (the C-M27·7 close pin, the M11 U07
analog).

**Entry reads (4).** `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(the four-language registry U12 audits); `src/Kumunita.Web/Views/Account/
Portability/Index.cshtml` + `ResolveReview.cshtml` (the U07/U08 views U12
scans for the `kw-l` keys + the Razor-verification traps);
`tests/Kumunita.Web.Tests/KwLRegistryConsistencyTests.cs` (the closed-set
pin U12 must keep passing); `docs/design/m27-user-scoped-portability-design.md`
§surface + §resolve-review (the locked kw-l key list U12 audits against).

**Deliverables (≤ 2 files, modify).**
- `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — **only** if a key
  is missing from a language (add the missing en/de/fr/da entry — the parity
  pin is the guard).
- `src/Kumunita.Web/Views/Account/Portability/Index.cshtml` +
  `ResolveReview.cshtml` — **only** if a Razor-verification trap is found (the
  un-awaited async helper / the `@(...)` attribute-interpolation — the
  AGENTS.md doctrine).
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — **untouched** (U13 owns the
  flip; U12 confirms the kw-l set only).

**Exit.** the kw-l parity + view-scan pins pass. Handoff entry: the kw-l key
count (en/de/fr/da each), the `IAuthorizationService` surface count (unchanged
— the C-M27·7 close pin), any Razor-verification trap fixed, any missing key
added.

---

### U13 — close: the `Milestones.cs` / README / STATUS / ARCHITECTURE flip + the `MilestonesTests` re-pin + the handoff `## Summary`

**Goal.** The **single close unit** (C-M27·8): flip the milestone state in the
four doc surfaces **in one unit** + re-pin the `MilestonesTests` three-test
contract (the order pin **unchanged**, the done-list pin gains `M27`, the
single-in-progress pin re-pointed `M27` → `M28`) + move the M27 unit-plan files
`docs/plans-milestones/in-progress/` → `docs/plans-milestones/done/` + append
the handoff `## Summary`. The loop-closing step (the M11 U07 / M26 U18 analog).

**Entry reads (5).** `src/Kumunita.Web/Milestones.cs` (the `M27` row U13 flips
`StatusNext` → `StatusDone` + the `M28` row U13 promotes `StatusPlanned` →
`StatusNext` — the **order of `Milestones.All` is unchanged**, the `M26` / `M28`
rows are the anchors); `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the three
pins U13 re-points — the order pin, the done-list pin, the
`M27_Is_The_Single_InProgress_Milestone` → `M28_Is_The_Single_InProgress_Milestone`
rename + re-point); `README.md` §Roadmap (the `M27` line U13 flips → **Done**
+ the `M28` line U13 flips → **In progress**); `docs/STATUS.md` (the M27 line
U13 flips → **Done**); `docs/ARCHITECTURE.md` (the M27 line U13 flips →
**shipped**, the M28 line U13 flips → **next**);
`docs/plans-milestones/in-progress/` (the M27 unit-plan files U13 moves to
`done/`).

**Deliverables (≤ 6 files).**
- `src/Kumunita.Web/Milestones.cs` — `M27` → `StatusDone`, `M28` →
  `StatusNext` (the **order of `Milestones.All` is unchanged** — `…, M25, M26,
  M27, M28`).
- `tests/Kumunita.Web.Tests/MilestonesTests.cs` — the order pin **unchanged**,
  the done-list pin gains `"M27"`, the `M27_Is_The_Single_InProgress_Milestone`
  test is **renamed** `M28_Is_The_Single_InProgress_Milestone` + **re-pointed**
  (asserting `M28` as the single `StatusNext` + the done-list now includes
  `M27`).
- `README.md` — the §Roadmap `M27` line → **Done** (the resident-scoped
  portability shipped, the per-entity conflict-resolution import the headline),
  the `M28` line → **In progress** (the `M26` line already **Done**, the
  `M27`/`M28` rows are the anchors).
- `docs/STATUS.md` — the M27 line → **Done** + the M28 line → **next** (the
  M26 line already **Done**, the M27 line the anchor).
- `docs/ARCHITECTURE.md` — the M27 line → **shipped** + the M28 line →
  **next** (the M26 line already **shipped**, the M27 line the anchor).
- `docs/design/m27-user-scoped-portability-design.md` — append the
  **`## M27 — Closed (recorded)`** section **last** (mirroring the M26 close,
  `m26-u18`): the three acceptance tests from U11's record, the total M27
  test count (Core + Web), the ADR 0148 pointer, the D9 deferred-lane list
  (each item named), and the M28-handoff note.
- `docs/plans-milestones/in-progress/m27-handoff-notes.md` — append `##
  Summary` (a table of the shipped units U00–U12, with their one-liner goal +
  test count + any deviations + the D9 deferred-lane list (each item named,
  each with a one-line follow-on ADR candidate or "resolved by U<m>")).
- The M27 unit-plan files (`m27-u00.md` … `m27-u13.md`) — **moved** from
  `docs/plans-milestones/in-progress/` to `docs/plans-milestones/done/`
  (**the user's explicit storage instruction for this lane is the flat
  `done/` folder**). Note: the repo's older precedent (the `done/m3/` /
  `done/m26/` example the user referenced) uses a `done/<lane>/` subfolder;
  if the team prefers that shape, move them to `docs/plans-milestones/done/
  m27/` instead — either way, `in-progress/` must no longer hold the M27
  unit plans. (The master register
  `docs/plans-milestones/plan-m27-user-scoped-portability.md` stays at the
  `docs/plans-milestones/` top level — it is the sealed register, not a unit
  plan.)

**Rules.** The **order** of `Milestones.All` is **unchanged**; only the two
status values flip (C-M27·8). Do not reorder. `MilestonesTests.cs` is the
**pin** — it must pass after the flip (the single-in-progress test re-pointed
to M28, the done-list gains M27, the order test untouched). The four doc
surfaces must **agree** in the same unit: **M27 done, M28 next** (C-M27·8).
The moves are **the** "done" step. Do **not** re-shape the design doc outside
the appended "Closed" section. **Zero new authorization surface** is the final
close pin (C-M27·7).

**Exit (the reliable test path — both suites + the milestone pin).**
```powershell
dotnet build Kumunita.slnx -c Debug
dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll
dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll
```
Build green; the Web suite passes **including the re-pointed `MilestonesTests`**
(the order + the done-list + the `M28_Is_The_Single_InProgress_Milestone`
re-point) + the kw-l parity pins (U12's close); the Core suite passes (the
three acceptance tests from U09); the four doc surfaces agree (M27 done, M28
next); the design doc's `## M27 — Closed (recorded)` section is appended; the
plan + handoff note + unit plans are in `done/` (or `done/m27/`); `in-progress/`
no longer holds the M27 files. Handoff entry: the four doc-surface flips
(file + line numbers), the `MilestonesTests` re-pin (the renamed test + the
re-pointed assertion), the D9 deferred-lane list (each item), the unit-plan
files moved (the `done/` paths). **The last handoff note U13 writes is the
M27→M28 handoff artifact** (one table + one line: "M27 is closed; the three
acceptance tests are recorded in the design doc §gate; the D9 deferred lanes
are above; M28 (guardian time limits) is now the single in-progress
milestone").
