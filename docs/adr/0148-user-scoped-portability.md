# ADR 0148 — User-scoped portability (resident self-export + per-entity conflict-resolution import)

Status: Accepted
Date: 2026-10-06

Builds on the frozen M11 (ADR 0108) `Portability/` context — the BCL-only
`KumunitaArchive` ZIP (de)serializer, the `PortabilityManifest`
self-describing head, the frozen `PortabilityDocTypes` 44-doc inventory, the
no-secret `PortabilityPrincipal` shape — and the resident self-lane house
pattern (`[Authorize]` verified-resident gate, `AccessVia.Owner`, the `kw-l`
closed-key registry). M27 is a **lane on the M11 surface**, not a new bounded
context (D7).

## Context

M11 (ADR 0108) closed the operator's world-seam: a GlobalAdmin moves the
**whole instance** in and out as one versioned `*.kumunita` archive,
fail-closed, no-secrets. It deliberately deferred two follow-on lanes (named
in ADR 0108 Consequences, C-M11·5): **per-slice portability** (one user's data
out) and **import-merge** (onto a diverged instance).

M27 owns both halves of that deferred lane, at the **resident's** scale. The
loop that M11 closes for the operator is now closed for the resident: **their
own data** can leave the platform as a portable archive and come back — but,
unlike M11's all-or-nothing restore, it comes back **where it fits**: on
import into an instance where their groups, components, pages, or tags do not
exist or they are not a member, the resident **manually resolves each conflict
entity** — adding it to a target they are a member of or discarding it. No
auto-merge, no silent re-assignment (ADR 0001-B: the author's choice is
absolute).

M27 is the **resident-scoped** half of the portability world-seam that M11
deliberately deferred as a follow-on lane. M11 shipped the **operator-scoped,
whole-instance** archive: a GlobalAdmin moves the *entire* instance in and
out, fail-closed, no-secrets. M27 reuses that machinery but **scopes it to one
resident** and **adds the one mechanic M11 did not have**: on import, the
resident **manually resolves conflicts per entity**, choosing to **add it
elsewhere** or **discard it**, because a resident's data may reference groups
/ components / pages / tags that do not fit the target community's structure
and authorization.

**The M11 vs M27 distinction (the load-bearing framing):**

| Axis | M11 (ADR 0108) — shipped | M27 (this lane) — to ship |
|---|---|---|
| Actor | GlobalAdmin (operator) | the resident themself (`[Authorize]`, verified) |
| Scope | the **whole** instance (44 doc types) | **one resident's authored data** (a closed subset, pinned) |
| Import semantic | **restore** into a fresh instance (fail-closed, all-or-nothing) | **merge with per-entity conflict resolution** (add elsewhere / discard, user-driven) |
| Reference conflicts | rejected (referential integrity, fail-closed) | **surfaced to the resident** as a resolve step (the lane's headline) |
| Secrets | identity graph travels (secrets reset on import) | the resident's own account is the anchor; **no credential material travels** (reuses the M11 no-secret boundary) |
| Audit `Via` | `Admin` | `Owner` (the resident self-lane) |

## Decisions (D1–D9)

- **D1 · Scope = the resident's authored data, resident-scoped.** M27 exports
  and imports **one resident's** authored content — the closed subset of the
  M11 44-doc inventory the resident **owns** (the `AuthorId`/`OwnerId`/
  `RecipientId`/participant fields, read from the M11 `PortabilityDocTypes`
  reference map where the doc is in the 44-entry set, or from the doc's own
  definition where it is not — the four non-M11 docs: `InventoryItem`,
  `Document`, `DocumentFolder`, `Bookmark`, and `Tag` whose ownership field is
  `CreatedBy`). Concretely the in-scope doc families (pinned in the design doc
  §2.2): the resident's `Post` + `PostReply` (+ their `PostTranslation` /
  `ReplyTranslation`), `AnnouncementComment`, `Event` (+`EventRsvp` the
  resident's own) + `EventTranslation`, `ProjectGoal` / `Project` / `TodoItem`
  / `KanbanBoard` (+translations/comments/placements they authored),
  `InventoryItem` they own, `Document` + `DocumentFolder` they own,
  `Conversation` + `Message` they participate in, `Page` they authored, `Tag`
  they created, `Bookmark`, `Profile` bio/tags (their own), and the `Group` +
  `GroupMembership` + `GroupInvitation` + `GroupJoinRequest` rows **for the
  groups the resident created or is a member of** (needed to make their
  content resolvable on import). **Out of scope:** the *operator*
  instance-identity (community name, language catalog, `LocaleSettings` — that
  is M11's `config.json`, the resident does not own it), the other residents'
  data, credential material (the C-M11·2 boundary, unchanged), `AccessAudit` /
  `OutboxEmail` / `EmailDeadLetter` / `IdentityToken` / `AuditPurgeSummary`
  (the M11 "Excluded" set, unchanged). **Alternative considered and rejected:**
  scoping to "everything the resident can *see*" — that is a *read* scope (the
  audience decision), not an *ownership* scope, and would let a resident
  import content they only had delegated/audience access to; M27 scopes to
  **what the resident authored/owns**, the privacy-correct reading of "their
  own data."

- **D2 · The archive reuses M11's format, at the resident scope.** Same
  BCL-only `*.kumunita` ZIP layout + the `PortabilityManifest` head (the
  `format` **authority** stays `kumunita/portability/1` — the archive is still
  a valid M11 archive; M27 adds **no** new format version). M27 adds one
  additive, **optional** manifest field the M11 importer ignores (the resident
  scope marker — `Scope? = "resident"` + `ResidentSubjectId?`; a
  `kumunita/portability/1` archive without them is a plain whole-instance
  archive — the ADR 0098/0106 additive-frozen-surface precedent: existing
  readers/callers keep compiling, a plain M11 archive still imports whole).
  **No new package** (the lean-stack + tsc-only discipline holds).

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
  **`clean`** (no conflict, ready to apply), **`duplicate`** (an equivalent
  entity already exists, matched by a per-kind **business key** pinned in the
  design doc §2.3), **`conflict`** (a reference target the entity depends on
  is absent in the target — the group/component/page/tag the resident's data
  pointed at does not exist or the resident is not a member of). The resident
  then **resolves each non-clean entity**: **add it elsewhere** (re-point the
  missing reference to a target they pick) **or discard it**. Clean entities
  are applied in one batch. A rejected / aborted resolve writes **nothing**
  (fail-closed on the write path, C-M27·4). **Alternative considered and
  rejected:** auto-merge (a deterministic best-effort re-point) — it silently
  re-assigns the resident's content to a community/authorization scope they
  did not choose, violating ADR 0001-B (the author's choice is absolute).

- **D5 · The surface is the resident's own, thin + audited + quiet.**
  `UserPortabilityController` at `/account/portability` (the resident's
  account surface — the `/settings/...` house pattern, `[Authorize]`, verified
  resident; the **resident's own** data only, so there is no GlobalAdmin
  break-glass and no audience decision — the ADR 0105/0118 personal-by-id
  posture): `GET /account/portability` (the index), `GET
  /account/portability/export` (streams the `*.kumunita`), `POST
  /account/portability/import` (validate + classify), `POST
  /account/portability/import/resolve` (apply the resident's per-entity
  decisions). Each write emits **one** `AccessAudit` row (`Via = Owner`,
  `TargetKind "portability"`, verb `export` / `import` / `import.resolve`);
  reads (index, the resolve-review) emit **none** (the ADR 0105 "reads never
  audit" / M6-inbox shape). **Zero new authorization surface** (C-M27·7).

- **D6 · Zero new authorization surface.** M27 reuses the resident self-lane
  (`[Authorize]` + the resident's own `subjectId`) + the `AccessVia.Owner`
  audit value (already exists — the "self acts as themself" slot). The
  per-entity conflict decision is a **business decision made by the resident
  in the UI**, not an `AccessAction` — it never touches the frozen
  `IAuthorizationService` decision engine. Pinned by the design doc §2.1 + the
  U12 handoff (the `IAuthorizationService` surface count unchanged).

- **D7 · Core home = a new resident-scoped service over the M11 context.**
  `Kumunita.Core/Portability/UserPortabilityService` (a new file in the
  **existing** `Kumunita.Core/Portability/` context — M27 is a lane **on** the
  M11 operator surface, the ADR 0109 "named lane on the shipped surface"
  precedent, not a new bounded context) with the `IUserPortabilityService`
  seam: `ExportAsync(residentSubjectId, ct)` → `Stream`;
  `ClassifyAsync(residentSubjectId, Stream, ct)` →
  `UserPortabilityImportPlan`; `ResolveAsync(residentSubjectId,
  UserPortabilityImportPlan, IReadOnlyList<UserPortabilityEntityResolution>,
  ct)` → `UserPortabilityImportResult`. No new document, no new `*DocTypes`
  surface, **no migration** (ADR 0004 §B.1).

- **D8 · Tests: a Core round-trip + the per-entity resolve pins + the Web
  surface pins.** The **three acceptance tests** (the closed-loop / handoff /
  part-vs-whole shape): (a) **round-trip** — plant a resident's authored
  footprint → export at the resident scope → import into a fresh instance
  where their groups/pages *do* exist → the clean entities are restored
  referentially intact; (b) **no-secret + self-scoped** — the exported archive
  contains **no** credential material and **only** the resident's
  authored/owned rows; (c) **per-entity conflict resolution** — import into a
  target where the resident's group/page *do not* exist → the `conflict`
  entities are **not** auto-applied, the resident resolves each, a discarded
  entity writes **nothing**. Plus the Web-surface pins (the resident gate, the
  one-audit-row-per-write, the kw-l keys resolve, reads emit no row).

- **D9 · kw-l keys + the deferred lanes.** The resident-facing strings use a
  **distinct `myportability.*` namespace** (the closed set:
  `myportability.index.*` / `myportability.export.*` / `myportability.import.*`
  / `myportability.resolve.*` / `myportability.status`) × 4 languages in
  `KnownTranslationKeys`. This is **deliberately distinct from M11's admin
  `portability.*` display keys** so the resident surface never collides with
  the operator surface. **Deferred lanes** (named in this ADR's Consequences,
  own ADRs): cross-neighborhood federation (the `README §Deferred` OpenIddict
  lane), the backup-automation / cron surface (OPS.md), a resident
  *delete-all-their-data* (a future ADR — M27 is export / backup + import, not
  deletion), and import-merge **beyond** the per-entity resident choice (a
  community-level reconciliation, own ADR).

## Alternatives considered

1. **Auto-merge (a deterministic best-effort re-point on import).** Rejected:
   it silently re-assigns the resident's content to a community/authorization
   scope they did not choose, violating ADR 0001-B (the author's choice is
   absolute). The README is explicit that the *user* resolves conflicts
   "choosing per entity whether to add it elsewhere or discard it."

2. **Scope-by-read (everything the resident can see).** Rejected: that is a
   *read* scope (the audience decision), not an *ownership* scope. It would
   let a resident import content they only had delegated/audience access to —
   a privacy violation. M27 scopes to **what the resident authored/owns**
   (C-M27·3), the privacy-correct reading of "their own data."

3. **A new bounded context (`Kumunita.Core/Portability/` →
   `Kumunita.Core/UserPortability/`).** Rejected: M27 is a lane **on** the M11
   operator surface (the ADR 0109 "named lane on the shipped surface"
   precedent), not a new bounded context. A new context would duplicate the
   M11 archive reader/writer + manifest model + doc-type inventory, all of
   which M27 reuses verbatim. The `UserPortabilityService` is a new file in
   the **existing** `Kumunita.Core/Portability/` context (D7).

4. **A new format version (`kumunita/portability/2`).** Rejected: the M27
   archive is the **same** M11 format (C-M27·1). The resident scope is an
   **additive optional** manifest field (`Scope?` + `ResidentSubjectId?`),
   not a new format version. A new format version would break the
   "a plain M11 archive still imports whole" invariant (C-M27·1) and would
   require the M11 importer to be version-aware (the ADR 0098/0106
   additive-frozen-surface precedent is the correct mechanism).

5. **A CLI surface now (a `dotnet kumunita export` / `import` command).**
   Rejected: the M11 surface is the resident's own Web surface (D5), not an
   operator CLI. A CLI would be a separate surface with its own auth story,
   its own audit lane, and its own kw-l keys — a follow-on lane if the
   operator wants one (the M11 `pg_dump` alternative is already covered by
   the operator's own tooling; M27 is the resident's surface).

## Consequences

- **The resident's data is portable in and out, as data, at the resident's
  scale.** The loop that M11 closed for the operator (the whole neighborhood's
  data can leave and return) is now closed for the resident: their own data
  can leave the platform as a portable archive and come back where it fits.
  The F1–F5 faces in the design doc are the user-facing consequences.

- **The no-secret boundary is carried verbatim from M11 (C-M27·2).** The M27
  archive contains the resident's authored data + media + the minimal
  no-secret identity reference set. It contains **no** credential material.
  The `PortabilityPrincipal` POCO shape is reused verbatim. A stolen or
  mishandled `*.kumunita` file is the resident's own content without secrets.

- **The scope is ownership, not read (C-M27·3).** Another resident's content
  is **absent** from the archive. A resident cannot import content they only
  had delegated/audience access to. This is the privacy-correct reading of
  "their own data."

- **The resident's choice is absolute on import (C-M27·4).** There is no
  auto-merge. A `conflict` entity the resident did not resolve is **not**
  applied. A discarded entity writes **nothing**. This is the ADR 0001-B
  "the author's choice is absolute" principle applied to the import path.

- **Zero new authorization surface (C-M27·7).** The `IAuthorizationService`
  surface count is unchanged. The `AccessVia.Owner` value (already exists) is
  reused. The per-entity resolve is a business decision in the resident's UI,
  not an authorization decision.

- **The M11 frozen set is reused, not reshaped (C-M27·1).** The
  `KumunitaArchive` / `PortabilityService` / `PortabilityDocTypes` /
  `PortabilityManifest` frozen set is unchanged except for the **one**
  additive optional `PortabilityManifest` field (the D2 resident-scope marker).
  A plain M11 whole-instance archive still validates/imports unchanged.

- **Deferred lanes (own ADRs):**
  - **Cross-neighborhood federation** (the `README §Deferred` OpenIddict
    lane) — M27 is one resident's data in and out of **one** instance, not
    a federation of neighborhoods.
  - **Backup-automation / cron surface** (OPS.md) — M27 is the resident's
    manual export/import, not an automated backup rotation.
  - **Resident delete-all-their-data** — M27 is export / backup + import,
    not deletion. A future ADR.
  - **Import-merge beyond the per-entity resident choice** (a community-level
    reconciliation) — M27's import is the resident's per-entity choice; a
    community-level reconciliation is a separate lane, own ADR.
