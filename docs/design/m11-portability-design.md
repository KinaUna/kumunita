# M11 — Portability (import/export) — design

> **Milestone M11.** A GlobalAdmin moves the **whole instance** in and out,
> as data: one portable, **versioned** `*.kumunita` archive (the whole
> registered content + the media bytes + the identity *graph* + the
> instance identity/localizations), and a **fail-closed, atomic**
> **restore** of that archive into a fresh instance. **Operator plane,
> not a content-decision lane** (C-M11·7): zero new `AccessAction`,
> `AccessVia`, `IAuthorizationService.Decide()` branch, or `Audience` —
> the content's audiences/grants/delegations travel **as data**, the
> decision engine is untouched.
>
> **Status.** **LOCKED.** The decisions D1–D10 are locked in **ADR 0108
> (Accepted, 2026-09-27)**. The `[PROPOSED]` set in the register
> `plan-m11-portability.md` was **user-approved before U00 ran** (the
> open-veto window closed with no veto recorded; the U00 handoff entry
> records the lock) and is refined **in favor of the source** where the
> source contradicted the prose (drift guard, entries 1–4 below): the
> `*DocTypes` surfaces + `MediaObject` + `LocaleSettings` / `LanguageCatalog`
> / `CommunityOptions` are the ground truth this doc is pinned to.
>
> **The one thing every unit must respect:** the **no-secret boundary**
> (C-M11·2) is the single most load-bearing M11 invariant — the archive
> travels the identity **graph** (principals + delegation / guardian /
> membership / moderator docs) **never** the credential material, and on
> import the principals are **re-created with their secrets reset**.
> Every register unit (U02's extractor, U06's re-creator, U07's
> byte-scan witness) enforces this at a different seam; the closed
> content inventory in §inventory is the **exact** set that travels,
> read from the `*DocTypes` surfaces, and it **excludes** the
> secret/operational documents by name (§inventory "Excluded" + drift
> guard entry 1).

## Context

M1–M10 and the named lanes grew a full resident surface — identity,
directory, posts, events + calendar, projects + boards, pages, search,
notifications, messaging, PWA. But a self-hosted single-neighborhood
platform lives or dies on one world-seam the roadmap row names
(ARCHITECTURE.md value chain: "the loop closes **into** the residents'
lives"): **the neighborhood's data must be able to leave the platform and
come back.**

Today it cannot:

- **No import path at all.** A fresh instance is a blank instance.
- **The only "export" is a hand-run `pg_dump`** (OPS.md), which carries
  the Postgres schema *and* the credential hashes *and* the media volume
  as **three separate artifacts** that do not travel together and do not
  verify against each other. `pg_dump` is a *schema + state* snapshot,
  not a portable *content* artifact: it carries credential material the
  platform's privacy posture forbids from being in an operator's hands,
  and it is not self-describing (no format version, no manifest, no media
  verification).

M11 closes that seam in the two directions the row names (import /
export):

1. **Export** — a GlobalAdmin produces **one** portable, **versioned**
   archive (`*.kumunita`) holding the whole instance's *content* (every
   registered content domain document), the *media bytes* (content-
   addressed, dedup preserved), the *identity graph* (who-is-who +
   delegation + guardian + membership + moderator — **as data**, without
   the credential material), and the *instance identity + localizations*
   (the community name, the language catalog, the locale settings). It is
   a single self-describing file an operator can store as a backup or
   carry to a new host.
2. **Import (restore)** — a GlobalAdmin on a **fresh** (or current)
   instance ingests that archive: it **validates the whole archive first**
   (format version, per-document-type schema sanity, **referential
   integrity** — every referenced id resolves within the archive — and the
   media bytes), and only then **applies it in one commit** (the identity
   principals re-created with secrets **reset**, the domain documents in
   the pinned dependency order, the media bytes, the config). A rejected
   archive writes **nothing**.

M11 builds entirely on **frozen, verified seams** (greenfield and
operator-scoped — grep-confirmed: there is no `Portability` context, no
export/import code, no `manifest.json` shape, no archive reader/writer
anywhere in the tree):

1. **The Marten domain documents** across the registered `*DocTypes`
   surfaces (`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` /
   `M6DocTypes` / `M9DocTypes` / `MediaDocTypes` / `TagDocTypes` /
   `PageDocTypes`) — the **exact** closed content inventory + import order
   + reference map is locked from source in §inventory.
2. **The ASP.NET Identity tables** in the `identity` schema (the
   **principals** — *not* the domain docs; the credential material lives
   here and **never travels**, C-M11·2).
3. **The content-addressed media volume** (ADR 0011, C-MED·3/4/7): the
   `MediaObject` catalog doc + the bytes at `{root}/{Id[0..2]}/{Id}` —
   the same layout the archive reuses, so import is a byte-copy.
4. **The GlobalAdmin admin-surface house pattern** (ADR 0101/0105/0106):
   `[Authorize(Roles = GlobalAdmin)]` + one `AccessAudit` row `Via =
   Admin`, the `AdminMessagingController` shape to mirror; `AttachmentController`'s
   `Content-Disposition: attachment` streaming shape the `export` action
   reuses.
5. **The BCL `System.IO.Compression.ZipArchive`** (in the .NET 10
   framework — the lean-stack + tsc-only discipline holds; **no new
   package**).
6. **`CommunityOptions` + `LocaleSettings` + `LanguageCatalog`** (the
   instance identity + localizations the `config.json` snapshot carries,
   §config).

This is a **milestone** (a roadmap letter, not a named lane): the close
unit (U07) flips `Milestones.cs` / the README Roadmap / `docs/STATUS.md` /
`docs/ARCHITECTURE.md` + `MilestonesTests.cs` (the AGENTS.md doc↔code
parity contract — C-M11·8). This design doc (authored U00, **LOCKED**) is
the **primary tier**; the register
`docs/plans-milestones/done/plan-m11-portability.md` is the secondary tier;
the scratch handoff note is
`docs/plans-milestones/done/m11-portability-handoff-notes.md`.

## Goals / Non-goals

**In (shipped by M11):** the `Kumunita.Core/Portability/` context (D8) —
the manifest model (§manifest), the document-inventory registry (D7, §inventory
as data), the ZIP archive (de)serializer (D2), and the `PortabilityService`
(export / validate / apply). The **whole-instance export** (the documents +
the no-secret principals + the config, U02; the media bytes + the manifest
finalization, U03; the web surface + the `portability.export` audit + the
`portability.*` kw-l keys, U04) and the **fail-closed restore** (the
validate-then-apply of the documents + the media, U05; the identity
re-creation with secrets reset + the config apply + the web surface + the
`portability.import` audit, U06). The **three Core acceptance tests**
(round-trip / no-secret / fail-closed, D9) + the **Web-surface pins** (U07).
The **U07 milestone flip** (M11 → `StatusDone`, M12 → `StatusNext`).

**Out (each a named follow-on lane, ADR 0108 Consequences — own ADRs):**
per-slice / cross-instance portability (one group's posts out; neighborhood
A → B), import-merge (onto a diverged instance), the backup-automation /
cron surface (OPS.md), and the other world-seam exports (M12 stays iCal).

## Human cost

This is an **operator** surface, not a resident one. It gives the
**neighborhood's** data the ability to leave and return — the loop closing
*out of* the platform into the operator's backup rotation and the
resident's right to "move us, our whole neighborhood, to a new host." It
takes **nothing** from the resident's time or attention (a plain resident,
including a moderator, never sees the surface — C-M11·6), and it **protects**
the trust boundary rather than testing it: the archive is *structurally*
incapable of carrying credential material (C-M11·2), so a stolen or
mishandled `*.kumunita` file is data without the secrets, exactly the
privacy posture the platform already holds for its other world-seams
(ADR 0028 GU "no standing to carry a secret"; ADR 0034 the attachment
lane). The cost is the operator's — the surface is quiet (one audit row
per action) and the only thing it asks of the operator is a deliberate
click.

## Parts affected

- **`Kumunita.Core/Portability/`** (new) — `PortabilityManifest.cs`
  (the `manifest.json` POCO set), `PortabilityDocTypes.cs` (the D7 closed
  inventory registry — data), `KumunitaArchive.cs` (the ZIP (de)serializer),
  `PortabilityService.cs` (export / validate / apply), the export/apply
  partials U02/U03/U05/U06 fill in (`PrincipalsExport` / `ConfigExport` /
  `MediaExport` / `ManifestFinalize` / `ValidateAsync` / `ApplyDocumentsAsync`
  / `ApplyMediaAsync` / `ApplyIdentityAsync` / `ApplyConfigAsync`).
- **`Kumunita.Core/DependencyInjection.cs`** — the registry + the
  serializer + the service registered next to their siblings.
- **`Kumunita.Web/Controllers/AdminPortabilityController.cs`** (new) +
  **`Views/Admin/Portability.cshtml`** (new) + one nav link in
  `Views/Shared/_AdminNav.cshtml`.
- **`Kumunita.Core/Localization/KnownTranslationKeys.cs`** — the
  `portability.*` closed-key registry entries × 4 languages.
- **`tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs`** (new) +
  **`tests/Kumunita.Web.Tests/AdminPortabilityControllerTests.cs`** (new).
- **The U07 parity surfaces:** `Milestones.cs`, the `README.md` Roadmap,
  `docs/STATUS.md`, `docs/ARCHITECTURE.md` (the value-chain table),
  `MilestonesTests.cs`.

**Unchanged (the frozen surface):** `IAuthorizationService` /
`IAuditableResource` (zero new adapters, C-M11·7), the `AccessAction` /
`AccessVia` sets, the `Audience` doc, every `*DocTypes` surface (M11 adds
**no** document and **no** index — it is a **reader/writer over the frozen
documents**), and `Program.cs` (the CSP is untouched; the surface is
server-rendered MVC).

## Seams & contracts (mandatory)

**Created** (all new, all in `Kumunita.Core/Portability/`):

- **`PortabilityService`** — the operator seam the web layer + the tests
  target. The locked public surface (U01's shell; U02–U06 fill the bodies):

  ```csharp
  public interface IPortabilityService
  {
      // U02–U03 (export) — one complete, valid *.kumunita archive.
      // Emits exactly one `portability.export` AccessAudit row (Via = Admin).
      Task<Stream> ExportAsync(string actorId, CancellationToken ct = default);

      // U05–U06 (import) — validate-then-apply, fail-closed.
      // A validate failure returns the closed failure set and writes ZERO rows.
      // On success it emits exactly one `portability.import` AccessAudit row (Via = Admin).
      Task<PortabilityImportResult> ImportAsync(string actorId, Stream archive, CancellationToken ct = default);
  }
  ```

  where `PortabilityImportResult` is `ok` (the apply succeeded) or a
  **closed** failure set (§validate "closed failure set") — the U06 web
  surface + the U07 pin render / assert exactly that set.

- **`PortabilityDocTypes`** (D7) — the closed inventory registry, **data**:
  an ordered list of `{ Type, FileName, Order, ReferenceFields }`, a
  `ByType` lookup, and the ordered iteration U02's export loop + U05's
  apply / integrity loop consume. §inventory is the **verbatim** text U01
  copies.

- **`KumunitaArchive`** (D2) — `WriteAsync(Stream, …)` + `ReadAsync(Stream)`
  over the §layout using `ZipArchive`; the `docs/{Type}.json` round-trip via
  `System.Text.Json`; the media-path derivation reusing `IMediaFileStore`'s
  `{root}/{Id[0..2]}/{Id}` convention.

**Depended on** (frozen, unchanged):

- **`IDocumentStore`** (Marten) — load all rows of each inventory `Type`
  (export) + store the re-applied rows (apply). M11 **reads and writes the
  existing documents**; it adds no schema.
- **The Identity `UserManager` / `RoleManager`** — read the principals
  (U02's no-secret projection) + re-create them with secrets reset
  (U06). The credential columns are **never read into the archive** and
  **never written back** (C-M11·2).
- **`IMediaStore` / `IMediaFileStore`** — `OpenReadAsync` the bytes (export)
  + `PutAsync` the bytes (apply), both over the content-addressed id
  (C-M11·3).
- **`CommunityOptions` + `LocaleSettings` + `LanguageCatalog`** — the
  `config.json` snapshot source (§config).

**Access model:** **unchanged.** M11 touches **no** audience, **no** group,
**no** delegation, **no** moderator scope — it is an **operator action over
the instance** (C-M11·7), gated by the GlobalAdmin role, audited (C-M11·6),
and quiet (the surface is hidden from a non-GlobalAdmin). The content's
audiences/grants/delegations travel **as data** (they are Marten documents
on the frozen surfaces), so the decision engine is untouched — the same
ADR 0006 frozen-surface discipline ADR 0105 (messaging) applied to an
operator lane.

**Migration path:** none — M11 adds no document, no index, no migration
(the `*DocTypes` surfaces are read/write-only). The only additive surface is
the `Portability` context's **POCOs** (the manifest model, the registry,
the archive (de)serializer, the service), which are **not** Marten documents
and create **no** `mt` tables.

**Audited:** **yes** — the D5 surface. `export` and `import` each emit
**exactly one** `AccessAudit` row (`TargetKind "portability"`, `Via = Admin`,
verb `export` / `import`), emitted by the **service**, the controller adds
none (the ADR 0105 `messaging.toggle` shape).

## Feedback loops

**How we know it works** (the three acceptance tests + the surface pins,
U07):

- **round-trip** (D9a, the "handoff") — plant a representative instance
  (a group, a post + reply + a translation, an event + RSVP, a board +
  lane + todo, a conversation + message, a page + a translation, a media
  object + its bytes, a delegation grant, a guardian link, a couple of
  principals with roles) → **export** → **import** into a **fresh**
  instance → the content graph, the media, and the role assignments are
  preserved and referentially intact.
- **no-secret** (D9b, the "closed-loop" on the trust boundary) — the
  exported archive contains **no** password hash / security stamp /
  Identity token: the principals POCO has **no** such field **and** a
  byte-scan witness over the archive finds none.
- **fail-closed** (D9c, the "part-vs-whole") — a wrong-`format` manifest /
  a truncated media file / a dangling reference is rejected **before any
  write** — the fresh instance has **zero** rows after the failed import.
- **Web-surface pins** — the GlobalAdmin gate (a non-GlobalAdmin
  `GET /admin/portability` 403s), the one-audit-row per action
  (`portability.export` / `portability.import`, `Via = Admin`), the
  `portability.*` kw-l keys resolve (the parity).

**Which signals, which thresholds, who watches:** the test suite is the
watcher (the `dotnet exec …dll` runner per AGENTS.md; `Kumunita.Core.Tests`
~20 s, the Testcontainers `postgres:18` path). The closed failure set
(§validate) is the **contract** a failing import returns — a failure that
produces anything other than that set is a regression (the C-M11·4 pin).

## Emergent impact

**Privacy:** the archive is the trust boundary — it is **structurally**
incapable of carrying credential material (C-M11·2), so a stolen or
mishandled file is data without the secrets. This is the ADR 0028 GU
precedent ("no standing to carry a secret") applied to the operator's own
archive, and it is **stronger** than `pg_dump` (which carries the hashes).
**Trust:** the fail-closed restore (C-M11·4) means a corrupt or
referentially-inconsistent archive is refused **wholesale** — the
neighborhood's data is never left half-imported, the "parts-work-seams-
don't" failure the philosophy names is structurally impossible on the
import path. **Reliability:** the content-addressed media (C-M11·3) makes
re-import idempotent at the byte layer (dedup by hash is preserved).
**Legibility:** the archive is self-describing (C-M11·1) — the
`manifest.json` `format` version is the authority, so an old instance can
refuse a newer format it does not understand instead of misapplying it.
**Cost:** the operator's only, and it is one click.

## Local-optimization check

This is the **whole** (the neighborhood's data), not a part. It optimizes
**portability + trust** (the data can leave and come back without the
secrets) at the cost of **none of the resident's time or attention** (the
surface is operator-only, C-M11·6). The part it does **not** optimize is
the **fragment** — per-group / cross-neighborhood portability is a
follow-on lane (D1/C-M11·5), and that is the correct boundary: the repo is
explicitly single-neighborhood with no multi-tenant model, and M11 owns the
*whole* instance the value-chain row names, not the fragments.

## FACES check

**F1 · The neighborhood's life is portable out.** A GlobalAdmin downloads
one `*.kumunita` archive holding the whole instance's content + media +
identity graph + config — the loop closes **out of** the platform
(C-M11·1/3/5).

**F2 · The archive comes back intact.** Importing that archive into a
fresh instance restores the content graph (posts, events, projects,
conversations, pages, groups, roles, delegations) + the media,
referentially intact (C-M11·1/4).

**F3 · The secrets never travel.** The archive contains no credential
material; after import the residents re-authenticate (fresh passwords) —
the trust boundary is the archive, not the file's bytes (C-M11·2).

**F4 · A corrupt archive is refused wholesale.** A wrong-version /
truncated / referentially-inconsistent archive is rejected before any
write; the instance is never left half-imported (C-M11·4).

**F5 · The operator surface is quiet + audited.** Export + import are
GlobalAdmin-only, each leaves exactly one audit row, and are invisible to a
plain resident (C-M11·6/7).

**FACES-strength:** **f**lexible (one portable artifact; the operator
chooses when), **c**oherent (the closed inventory + the fail-closed
restore mean the whole travels as a whole), **s**table (the self-
describing `format` version + the content-addressed media are idempotent).
**FACES-consumed:** **e**nergizing — M11 deliberately adds **no** resident
facing, **no** engagement surface, **no** feed; it is a quiet operator
lane, and that is the point (C-M11·6). The trade is named: M11 consumes
the *resident's* energy-budget to buy the *neighborhood's* data-portability
+ trust, which is the correct exchange for a self-hosted platform's
world-seam.

## Rollout & rollback

**Deployment:** a surface-milestone — a new `Portability` context + the
admin controller/view + the two test files. **No migration** (no new
document / index; the `*DocTypes` surfaces are read/write-only). The
`Portability` POCOs are not Marten documents and create no `mt` tables.

**Restore path (the documented rollback):** the import semantic is
**restore** (D1) — apply into a **fresh** instance. The current
instance's state before an import **is the operator's pre-import backup**;
that is the rollback path. A mid-apply failure is **never** a
silently-accepted half-import (C-M11·4): the operator re-imports from the
pre-import backup on a fresh instance (OPS.md). This is the "clean
rollback / restore" the template asks for — it is the **whole** instance,
by construction, because M11 is whole-instance by scope (C-M11·5).

## Risks

- **The no-secret boundary is the whole ballgame.** A `docs/` row that
  carries a credential column (the `IdentityToken.Token`) or a principals
  projection that leaks a hash/stamp/token would **break C-M11·2** — the
  single most load-bearing invariant. It is pinned three ways: the
  §inventory "Excluded" set (the `IdentityToken` / `OutboxEmail` /
  `EmailDeadLetter` / `AccessAudit` / `AuditPurgeSummary` docs are **not**
  exported), U02's `PrincipalsExport` (the **exact** no-secret field set
  + the **excluded** credential fields, §principals), and U07's no-secret
  test (the POCO field-shape **and** the byte-scan witness).
- **Referential integrity is the part-vs-whole seam.** A dangling id
  (a post's `ComponentId` pointing at a community not in the archive; a
  reply's `PostId` pointing at a post not in the archive) would import a
  **half** graph — the C-M11·4 failure. It is pinned by the §validate
  referential-integrity loop (the D7 reference map, data-driven) + the
  U07 fail-closed test (a dangling reference ⇒ zero writes).
- **A mid-apply failure is the documented restore path, not a silent
  half-import.** The apply phase is one commit in the pinned dependency
  order (C-M11·4); a failure before the commit ⇒ the pre-import backup is
  the rollback (OPS.md). The risk is the operator *assuming* a partial
  import succeeded — the closed failure set (§validate) is the
  authoritative "it did not" signal the U06 surface renders.
- **The closed inventory is a snapshot of the frozen surfaces.** If a
  future lane adds a `*DocTypes` document, the §inventory list + the
  U07 round-trip plant must both move (the drift guard is the record).
  Until then the inventory is **closed** and **exact** — U01 copies it
  verbatim, U02/U05 iterate it as data.

## Integration step served

**outcome** — the value-chain arrow the M11 roadmap row names (the loop
closing **into** the residents' lives): the neighborhood's data can
**leave** the platform (a portable, versioned, secret-free artifact) and
**come back** (a fail-closed, atomic restore). This is not a signal /
awareness / understanding step — it is the **world-seam** handoff the
template asks about, and it is the strongest form of it: the *whole*
instance, not a fragment.

## World seams

- **Out** — the operator's backup rotation / a new host: one `*.kumunita`
  file, self-describing, verifiable, secret-free. This is the handoff the
  row names.
- **In** — the fresh instance: the archive is ingested as data, the
  principals re-created with secrets reset, the content referentially
  intact. The resident re-authenticates (F3).
- **Not** — per-slice / cross-neighborhood (A → B) portability is a
  **follow-on lane** (own ADR, D1/C-M11·5); M11 owns the *whole* instance,
  not the fragments. The iCal / other world-seam **format** exports are
  their own milestones (M12 stays iCal).

## §manifest — the `manifest.json` field set (D2, C-M11·1)

The archive's root `manifest.json` is the **self-describing** head. The
**`format`** field is the **authority** (the `*.kumunita` extension is a
content marker, not a format claim). The **exact** locked field set
(U02's `ManifestFinalize` builder copies verbatim):

```jsonc
{
  "format": "kumunita/portability/1",   // the format version — the authority (C-M11·1)
  "generated_at": "<ISO-8601 UTC>",      // the export timestamp
  "community_name": "Maplewood Residents",   // from CommunityOptions.Name (§config)
  "doc_counts": { "Post": 42, "PostReply": 17, "MediaObject": 5, /* … */ },
  // one entry per §inventory content doc type — the U02 doc loop's count
  "media_manifest": [
    { "id": "<sha256-hex>", "size_bytes": 12345, "content_type": "image/png" }
    // one entry per MediaObject in the catalog — the U03 media loop
  ]
}
```

- **`format`** — the closed version string for M11 is **`kumunita/portability/1`**.
  Import **rejects** any archive whose `format` is not one the build
  understands (the C-M11·1 pin — fail-closed, no partial apply).
- **`generated_at`** — the export timestamp (a witness, not a decision).
- **`community_name`** — the instance's display name
  (`CommunityOptions.Name`, §config) — the archive's self-description.
- **`doc_counts`** — a `Dictionary<string, int>` mapping each §inventory
  content doc type to its row count (the U02 doc loop's output — the
  per-type isolation that keeps the round-trip testable).
- **`media_manifest`** — the closed list of media entries, each
  `{ id, size_bytes, content_type }` (the U03 media loop's output, read
  from the `MediaObject` catalog — the **exact** field set U05's validator
  copies verbatim, §validate (d)).

## §layout — the archive layout (D2)

The `*.kumunita` is a ZIP (BCL `System.IO.Compression.ZipArchive`,
**no new package** — the lean-stack + tsc-only discipline holds) with the
**exact** locked layout:

```
{
  manifest.json                       # §manifest — the self-describing head
  docs/{DocType}.json                 # one JSON-array file per §inventory content doc type
  media/{Id[0..2]}/{Id}               # the content-addressed bytes — the SAME layout as the local volume (C-M11·3)
  identity/principals.json            # §principals — the no-secret graph
  config.json                         # §config — the instance identity + localizations
}
```

- **`docs/{DocType}.json`** — one **JSON array** per §inventory content
  document type (the per-type isolation that keeps the round-trip
  testable; the U02 export loop writes them, the U05 apply loop reads
  them in the §inventory order).
- **`media/{Id[0..2]}/{Id}`** — the media bytes at the **same**
  content-addressed layout the local volume uses (ADR 0011
  C-MED·3/4/7; `Id` = the lowercase-hex SHA-256 of the payload). Import
  is a **byte-copy** and the dedup-by-content-hash is preserved
  (C-M11·3).
- **`identity/principals.json`** — the **no-secret** identity graph
  (§principals).
- **`config.json`** — the instance identity + localizations (§config).

## §inventory — the closed content inventory + import order + reference map (D7)

> **LOCKED FROM SOURCE.** This is the **exact** closed set of content
> documents M11 exports/imports, read from the `*DocTypes` surfaces
> (`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` / `M6DocTypes`
> / `M9DocTypes` / `MediaDocTypes` / `TagDocTypes` / `PageDocTypes`), **not
> invented**. U01's `PortabilityDocTypes` registry copies this table
> **verbatim** (the `Type` / `Order` / `ReferenceFields` are data the
> U02 export loop + the U05 apply / integrity loop iterate **generically**
> — **not** per-type code).
>
> **Reference fields** = the fields whose value must resolve to a row
> **within the archive** (C-M11·4 / §validate (c)). `→ principal` means the
> value is a `subjectId` (the identity graph, §principals). `→ MediaObject`
> means the value is a content hash (`MediaObject.Id`, the media
> manifest). `→ LanguageCatalog` means the value is a `LanguageCatalog.Id`
> (the `config.json` catalog). The `LanguageCode` on every `*Translation`
> row resolves against the `config.json` language catalog (not a `docs/`
> row — the catalog travels via `config.json`, §config).

**Order 1 — the base units (parents; no doc-level parent):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 1 | `Group` | `docs/Group.json` | `OwnerId` → principal |
| 2 | `Component` | `docs/Component.json` | — |
| 3 | `MediaObject` | `docs/MediaObject.json` | `CreatedById` → principal (the catalog doc; the bytes are in `media/`) |
| 4 | `Tag` | `docs/Tag.json` | — |

**Order 2 — the identity graph (principals + the M1 delegation/guardian/membership/moderator docs):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 5 | `Profile` | `docs/Profile.json` | `AvatarId` → MediaObject (nullable; the identity is `SubjectId`) |
| 6 | `DelegationGrant` | `docs/DelegationGrant.json` | `OwnerId`, `DelegateId`, `RevokedBy` (nullable) → principal |
| 7 | `GuardianLink` | `docs/GuardianLink.json` | `GuardianId`, `ChildId` → principal |
| 8 | `GroupMembership` | `docs/GroupMembership.json` | `GroupId` → Group; `UserId`, `AddedBy` → principal |
| 9 | `GroupInvitation` | `docs/GroupInvitation.json` | `GroupId` → Group; `UserId`, `InvitedBy` → principal |
| 10 | `GroupJoinRequest` | `docs/GroupJoinRequest.json` | `GroupId` → Group; `UserId` → principal |
| 11 | `ModeratorAssignment` | `docs/ModeratorAssignment.json` | `ComponentId` → Component; `UserId`, `GrantedBy` (nullable) → principal |
| 12 | `ComponentMembership` | `docs/ComponentMembership.json` | `ComponentId` → Component; `UserId` → principal |

**Order 3 — the M1 translations (one row per (parent, language)):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 13 | `TranslationResource` | `docs/TranslationResource.json` | `LanguageCode` → LanguageCatalog (the UI-string row; `Key` is the registry key) |
| 14 | `GroupTranslation` | `docs/GroupTranslation.json` | `GroupId` → Group; `LanguageCode` → LanguageCatalog |
| 15 | `CommunityTranslation` | `docs/CommunityTranslation.json` | `ComponentId` → Component; `LanguageCode` → LanguageCatalog |
| 16 | `TagTranslation` | `docs/TagTranslation.json` | `TagId` → Tag; `LanguageCode` → LanguageCatalog |

**Order 4 — the M3 content (posts / announcements + their translations/comments):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 17 | `Post` | `docs/Post.json` | `ComponentId` (nullable) → Component; `GroupId` (nullable) → Group; `AuthorId` → principal; `ImageIds[]` → MediaObject; `TagIds[]` → Tag |
| 18 | `PostReply` | `docs/PostReply.json` | `PostId` → Post; `AuthorId` → principal; `ImageIds[]` → MediaObject |
| 19 | `PostTranslation` | `docs/PostTranslation.json` | `PostId` → Post; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |
| 20 | `ReplyTranslation` | `docs/ReplyTranslation.json` | `ReplyId` → PostReply; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |
| 21 | `Report` | `docs/Report.json` | `PostId` → Post; `ReporterId` → principal; `ComponentId` (nullable) → Component |
| 22 | `Announcement` | `docs/Announcement.json` | `AuthorId` → principal; `CommunityId` (nullable) → Component; `ImageIds[]` (nullable) → MediaObject |
| 23 | `AnnouncementTranslation` | `docs/AnnouncementTranslation.json` | `AnnouncementId` → Announcement; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |
| 24 | `AnnouncementComment` | `docs/AnnouncementComment.json` | `AnnouncementId` → Announcement; `AuthorId` → principal |

**Order 5 — the M4 content (events + RSVPs + translations):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 25 | `Event` | `docs/Event.json` | `ComponentId` (nullable) → Component; `GroupId` (nullable) → Group; `AuthorId` → principal |
| 26 | `EventRsvp` | `docs/EventRsvp.json` | `EventId` → Event; `UserId` → principal |
| 27 | `EventTranslation` | `docs/EventTranslation.json` | `EventId` → Event; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |

**Order 6 — the M5/PL content (projects + boards + to-dos + their translations/comments):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 28 | `ProjectGoal` | `docs/ProjectGoal.json` | `ComponentId` (nullable) → Component; `AuthorId` → principal |
| 29 | `Project` | `docs/Project.json` | `GoalId` (nullable) → ProjectGoal; `ComponentId` (nullable) → Component; `AuthorId` → principal |
| 30 | `TodoItem` | `docs/TodoItem.json` | `ComponentId` (nullable) → Component; `ProjectId` (nullable) → Project; `AuthorId` → principal; `AssigneeId` (nullable) → principal; `ParentId` (nullable) → TodoItem; `BlockedByTodoId` (nullable) → TodoItem; `TagIds[]` → Tag; `ImageIds[]` (nullable) → MediaObject |
| 31 | `KanbanBoard` | `docs/KanbanBoard.json` | `ComponentId` (nullable) → Component; `ProjectId` (nullable) → Project; `AuthorId` → principal |
| 32 | `KanbanLane` | `docs/KanbanLane.json` | `BoardId` → KanbanBoard |
| 33 | `BoardItemPlacement` | `docs/BoardItemPlacement.json` | `TodoItemId` → TodoItem; `BoardId` → KanbanBoard; `LaneId` → KanbanLane |
| 34 | `TodoTranslation` | `docs/TodoTranslation.json` | `TodoItemId` → TodoItem; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |
| 35 | `BoardTranslation` | `docs/BoardTranslation.json` | `BoardId` → KanbanBoard; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |
| 36 | `ProjectTranslation` | `docs/ProjectTranslation.json` | `ProjectId` → Project; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |
| 37 | `TodoComment` | `docs/TodoComment.json` | `TodoId` → TodoItem; `ParentId` (nullable) → TodoComment; `AuthorId` → principal |

**Order 7 — the M6 content (notifications + the per-resident read state):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 38 | `Notification` | `docs/Notification.json` | `RecipientId` → principal; `TargetId` (nullable) → Component / Group / Page / Announcement (kind-dependent) |
| 39 | `NotificationPreference` | `docs/NotificationPreference.json` | `RecipientId` (the identity) → principal |
| 40 | `NotificationSubscription` | `docs/NotificationSubscription.json` | `RecipientId` → principal; `TargetId` → Component / Group / Page (kind-dependent) |

**Order 8 — the M9 content (messaging):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 41 | `Conversation` | `docs/Conversation.json` | `ParticipantA`, `ParticipantB` → principal (the pair is the whole access story) |
| 42 | `Message` | `docs/Message.json` | `ConversationId` → Conversation; `SenderId` → principal; `LanguageCode` → LanguageCatalog |

**Order 9 — the PG content (pages + their translations):**

| Order | Type | `docs/{Type}.json` | Reference fields (→ target) |
|---|---|---|---|
| 43 | `Page` | `docs/Page.json` | `ParentId` (nullable) → Page; `AuthorId` → principal; `ComponentId` (nullable) → Component; `ImageIds[]` (nullable) → MediaObject; `TagIds[]` (nullable) → Tag |
| 44 | `PageTranslation` | `docs/PageTranslation.json` | `PageId` → Page; `LanguageCode` → LanguageCatalog; `AuthorId` (nullable) → principal |

**`MediaObject` note (C-M11·3):** the `MediaObject` catalog doc travels as
`docs/MediaObject.json` (the surviving catalog reference a `pg_dump`
carries, per its own doc-comment) **and** the bytes travel as
`media/{Id[0..2]}/{Id}`. The two are a pair: a `MediaObject` whose bytes
are missing / mismatched is a **validate-phase failure** (fail-closed,
C-M11·3 / §validate (d)) — the archive never imports a doc pointing at a
phantom byte.

**The closed failure-set note (the principal-id target set):** a
`→ principal` reference resolves against the `identity/principals.json`
`subjectId` set (the U05 integrity loop drives it — the D7 reference map,
data). A `→ LanguageCatalog` reference resolves against the
`config.json` language catalog (the U05 integrity loop reads
`config.json` **before** the docs integrity loop — the `LanguageCode`
target set is the catalog's `Id`s).

### Excluded — the docs that do **not** travel (the C-M11·2 boundary at the source)

The `*DocTypes` surfaces register **five** documents that the
**closed content inventory above deliberately excludes** — they are
either **credential material** (C-M11·2 forbids) or **operational state**
(a fresh instance's restore re-derives / never needs):

| Excluded type | Surface | Why it is excluded (drift guard entry 1) |
|---|---|---|
| `IdentityToken` | `M1DocTypes` | Holds a high-entropy **secret `Token`** (the verification-link token + the seed-admin setup credential) — **credential material**; C-M11·2 forbids it from traveling. A re-created principal re-verifies (the D3 "secrets reset" semantic). |
| `OutboxEmail` | `M1DocTypes` | The staged email row the durable handler writes — **operational** (an outbox, not resident content). A fresh instance's restore re-derives; an un-sent outbox row in a backup is a leak, not a fact. |
| `EmailDeadLetter` | `M1DocTypes` | The domain dead-letter doc the durable handler writes on final failure — **operational** (a failure record, not resident content). |
| `AccessAudit` | `M1DocTypes` | The audit trail — **operational** (a log, not resident content). A fresh instance's audit history is re-derivable from its own writes; the operator's pre-import backup is the rollback path. |
| `AuditPurgeSummary` | `M1DocTypes` | The purge summary — **operational** (a maintenance record, not resident content). |

> **This is the D3/C-M11·2 no-secret boundary enforced at the source.**
> U02's export loop iterates the §inventory list **only** (the 44 content
> docs), never these five. U07's no-secret test witnesses it end-to-end
> (the byte-scan finds none of the Identity credential columns). The
> `LanguageCatalog` + `LocaleSettings` docs (also on `M1DocTypes`) are
> **not** exported as `docs/` rows — they travel via `config.json`
> (§config, D2/D7 resolved — drift guard entry 4).

## §principals — the no-secret identity graph (D3, C-M11·2)

The `identity/principals.json` is a **JSON array** of principal objects.
The **exact** locked field set (U02's `PrincipalsExport` copies verbatim;
U06's `ApplyIdentityAsync` re-creator mirrors it **verbatim**):

```jsonc
[
  {
    "subjectId": "<the exported subjectId — the key U06 re-creates by>",
    "username": "<the Identity username>",
    "email": "<the Identity email>",
    "normalizedEmail": "<the Identity normalized email>",
    "displayName": "<the Profile.DisplayName — the display identity>",
    "verified": true,          // the Profile.Verified flag (the sign-in gate)
    "blocked": false,          // the Profile.Blocked flag (the suspension)
    "roles": ["Member", "Moderator"]   // the Identity role names — the standing
  }
]
```

**The exact locked field set (the allowed columns):**

| Field | Source | Note |
|---|---|---|
| `subjectId` | `Profile.SubjectId` (the identity) | **the key** U06 re-creates by — the stable id the rest of the graph references. |
| `username` | the Identity `UserManager` username | needed to re-create a signable principal. |
| `email` | the Identity `UserManager` email | the sign-in credential (the resident re-authenticates). |
| `normalizedEmail` | the Identity `UserManager` normalized email | the sign-in normalization. |
| `displayName` | `Profile.DisplayName` | the display identity (the directory / the composer). |
| `verified` | `Profile.Verified` | the sign-in gate (an unverified account cannot sign in). |
| `blocked` | `Profile.Blocked` | the suspension flag (a blocked account loses all standing). |
| `roles` | the Identity `RoleManager` role names | the standing (`Member` / `Moderator` / `GlobalAdmin` / `Translator`) — composable (ADR 0030). |

**The EXCLUDED credential fields (the C-M11·2 boundary — the single most
load-bearing M11 invariant):** the principals projection **never** reads
into the archive — and the U07 no-secret byte-scan witness confirms absent:

- **`PasswordHash`** (the Identity credential hash — the `pg_dump` leak).
- **`SecurityStamp`** (the Identity security stamp).
- **`AccessToken` / `RefreshToken`** (any Identity access / refresh token).
- **`RecoveryCode`** (any Identity recovery code).
- **The Identity sign-in record** (the sign-in history / the lockout
  counters — the lockout **state** travels via `verified` / `blocked`
  flags, never the lockout **count** the credential layer holds).

> **The U07 no-secret test** (D9b) asserts both: the principals POCO has
> **no** such field (the field-shape pin) **and** a byte-scan over the
> archive finds none of the Identity credential columns (the witness).

## §config — the `config.json` field set (D2)

The `config.json` is the instance identity + localizations. The **exact**
locked field set (U02's `ConfigExport` copies verbatim; U06's
`ApplyConfigAsync` mirrors it **verbatim**):

```jsonc
{
  "community": {
    "name": "Maplewood Residents",        // CommunityOptions.Name
    "support_email": "maplewood@example.com"   // CommunityOptions.SupportEmail (nullable)
  },
  "locale": {
    "default_language_code": "en",        // LocaleSettings.DefaultLanguageCode
    "default_timezone": "UTC",            // LocaleSettings.DefaultTimezone
    "default_date_format": "yyyy-MM-dd HH:mm",  // LocaleSettings.DefaultDateFormat
    "is_signup_open": true,               // LocaleSettings.IsSignupOpen
    "notify_admins_on_signup": true,      // LocaleSettings.NotifyAdminsOnSignup
    "announcement_comments_enabled": true,// LocaleSettings.AnnouncementCommentsEnabled
    "messaging_enabled": false            // LocaleSettings.MessagingEnabled (the inverted floor)
  },
  "languages": [
    { "id": "en", "native_name": "English", "enabled": true, "sort_order": 0 },
    { "id": "de", "native_name": "Deutsch", "enabled": true, "sort_order": 1 },
    /* … */   // every LanguageCatalog row (the enabled set + the disabled)
  ]
}
```

| Field | Source | Note |
|---|---|---|
| `community.name` | `CommunityOptions.Name` | the instance's display name (the `config.json` self-description + the `manifest.json` `community_name`). |
| `community.support_email` | `CommunityOptions.SupportEmail` | the contact (nullable). |
| `locale.*` | the `LocaleSettings` singleton (`LocaleSettings.SingletonId = "singleton"`) | the seven instance-level locale settings (the `DefaultLanguageCode` / `DefaultTimezone` / `DefaultDateFormat` / `IsSignupOpen` / `NotifyAdminsOnSignup` / `AnnouncementCommentsEnabled` / `MessagingEnabled` fields, verbatim from `Localization/LanguageCatalog.cs`). |
| `languages[]` | every `LanguageCatalog` row | the `Id` / `NativeName` / `Enabled` / `SortOrder` fields (the per-instance language catalog — the `LanguageCode` target set the §validate integrity loop resolves `*Translation` rows against). |

> **The D2/D7 double-travel is resolved:** the `LocaleSettings` +
> `LanguageCatalog` docs are **not** in the §inventory (they are **not**
> `docs/` rows); they travel via `config.json` (D2's explicit "config.json
> = the instance identity + localizations"). The §validate integrity loop
> reads `config.json` for the `LanguageCode` target set **before** the
> docs integrity loop. The §apply order (D4) is **unchanged** —
> principals → docs → media → config (the config apply is the **last**
> step, so the `LanguageCatalog` / `LocaleSettings` re-materialize after
> the `*Translation` rows that reference them are stored).

## §validate — the fail-closed validate/apply contract (D4, C-M11·1/3/4)

### The validate phase (runs to completion **before any write** — C-M11·4)

The **exact** locked validate checks (U05's `ValidateAsync` copies
verbatim; the U07 fail-closed test asserts **exactly** those):

1. **(a) `format`** (C-M11·1) — the `manifest.json` `format` is one the
   build understands (the closed set: `kumunita/portability/1`). A
   different / missing / malformed `format` ⇒ **reject** (fail-closed, no
   partial apply).
2. **(b) per-type sanity** (C-M11·4) — each `docs/{Type}.json` in the
   §inventory deserializes into its declared POCO set (the registry's
   `Type`). A malformed / missing / unexpected `docs/` file ⇒ **reject**.
3. **(c) referential integrity** (C-M11·4) — **every id referenced by
   the §inventory reference map** resolves to a row **within the
   archive**:
   - a `→ principal` id resolves against the `identity/principals.json`
     `subjectId` set;
   - a `→ {DocType}` id resolves against that doc type's `docs/{Type}.json`
     id set (the D7 reference map, data-driven — the uniform loop);
   - a `→ MediaObject` id resolves against the `manifest.json`
     `media_manifest` id set;
   - a `→ LanguageCatalog` code resolves against the `config.json`
     `languages[]` id set (read **before** the docs integrity loop).
   A dangling reference ⇒ **reject**.
4. **(d) media verification** (C-M11·3) — **every** `MediaObject` listed
   in the `manifest.json` `media_manifest` has its bytes present in
   `media/{Id[0..2]}/{Id}` **and** the byte content matches (the size +
   the content hash implied by the path). A missing / mismatched byte
   ⇒ **reject**.

**Any failure ⇒ zero writes** (C-M11·4). The validate phase is the
**closed failure set** contract:

```csharp
public sealed record PortabilityImportResult(
    bool Ok,
    IReadOnlyList<string> Failures = []   // the closed failure set — the U06 surface renders / the U07 pin asserts exactly this
);
```

The **closed** failure set (the U06 web surface + the U07 pin render /
assert **exactly** these, no more, no less):

| Failure (closed) | Triggered by |
|---|---|
| `format.unsupported` | check (a) |
| `docs.malformed` / `docs.missing` | check (b) |
| `ref.dangling:{Type}.{field}` | check (c) |
| `media.missing:{id}` / `media.mismatch:{id}` | check (d) |

### The apply phase (one commit, the pinned dependency order — C-M11·4)

On a **clean** validate, the apply phase is **one commit** in the locked
order (U05/U06 fill the bodies; the U07 round-trip test witnesses it):

1. **Re-create the Identity principals** (so `subjectId` sign-in works) —
   from `identity/principals.json`, keyed by the exported `subjectId`,
   **secrets reset** (a fresh, non-portable password + a fresh security
   stamp — the C-M11·2 import boundary), roles re-applied
   (U06's `ApplyIdentityAsync`).
2. **Store the domain documents** in the §inventory **import order**
   (the 44 content docs, parents before children — the D7 order; the
   uniform loop, **not** per-type code) (U05's `ApplyDocumentsAsync`).
3. **Copy the media bytes** into the volume at the `{Id[0..2]}/{Id}`
   layout (the C-M11·3 pin; the `MediaObject` catalog doc is in step 2)
   (U05's `ApplyMediaAsync`).
4. **Apply the config** (the `CommunityOptions` + the `LocaleSettings` +
   the `LanguageCatalog` — the §config field set; the U02 `ConfigExport`
   mirror, verbatim) (U06's `ApplyConfigAsync`).

**The restore path (the documented rollback):** on a **mid-apply**
failure, the instance's state is the **documented restore path** — the
operator's pre-import backup / a fresh instance + re-import (OPS.md).
M11 **never** silently accepts a half-import (C-M11·4).

## §surface — the admin web plane (D5, C-M11·6)

The **exact** locked surface (U04's export + U06's import; the U07
Web-surface pins target verbatim):

| Route | Action | Auth | Audit row (the service emits, the controller adds none) |
|---|---|---|---|
| `GET /admin/portability` | the index (the export button + the import upload form + the status area) | `[Authorize(Roles = GlobalAdmin)]` | — (a read, no audit) |
| `GET /admin/portability/export` | streams the `*.kumunita` archive (`Content-Disposition: attachment`, the ADR 0034 shape) | `[Authorize(Roles = GlobalAdmin)]` | **one** `portability.export` — `TargetKind "portability"`, `Via = Admin`, verb `export` |
| `POST /admin/portability/import` | applies the uploaded archive (the success / the closed-failure-set render) | `[Authorize(Roles = GlobalAdmin)]` | **one** `portability.import` — `TargetKind "portability"`, `Via = Admin`, verb `import` |

- **All three** are `[Authorize(Roles = GlobalAdmin)]` (the ADR 0101/0105
  admin-surface shape — a plain resident, **including a moderator**, never
  sees the surface; **no** break-glass read).
- **Each** of `export` + `import` emits **exactly one** `AccessAudit` row
  (the ADR 0105 `messaging.toggle` shape — `Via = Admin`), emitted by the
  **service** (the controller adds none).
- **Zero** new `AccessAction` / `AccessVia` / `IAuthorizationService.Decide()`
  branch / `Audience` (C-M11·7).

## Invariants (C-M11·1–8)

- **C-M11·1 · The archive is versioned + self-describing.**
  `manifest.json` carries a `format` version (`kumunita/portability/1` for
  M11), the `generated_at`, the community name, the per-document-type
  counts, and the media manifest (the file list + sizes + content types).
  Import **rejects** any archive whose `format` it does not understand —
  it fails closed, no partial apply. Pinned by the fail-closed test (D9c).

- **C-M11·2 · The archive travels the data, never the secrets.** The
  archive contains the identity **graph** (principals + the
  delegation/guardian/membership/moderator domain docs) + the content +
  the media + the config. It contains **no** credential material (no
  password hash, no security stamp, no access/refresh/recovery token, no
  Identity sign-in record). On import the principals are re-created and
  their secrets **reset**. Pinned by the no-secret test (D9b) — the
  **single most load-bearing M11 invariant**.

- **C-M11·3 · The media travels content-addressed + whole.** The bytes are
  copied verbatim (dedup-by-content-hash preserved); the `MediaObject`
  catalog doc + its bytes are either both present or the archive is
  rejected. Pinned by the round-trip test (D9a, the media leg) + the
  fail-closed test (D9c, the truncated-bytes leg).

- **C-M11·4 · Import is fail-closed + atomic.** The validate phase runs to
  completion **before** any write (format + per-type sanity + referential
  integrity + media verification); any failure ⇒ **zero writes**. The
  apply phase is one commit in the pinned dependency order; a mid-apply
  failure is the documented restore path, never a silently-accepted
  half-import. Pinned by the fail-closed test (D9c).

- **C-M11·5 · Whole-instance, not per-slice.** M11 exports/imports the
  **entire** registered instance. Per-slice / cross-instance /
  import-merge are **follow-on lanes** (own ADRs), not M11 scope. Pinned
  by the D1 deferral close in the design doc + the ADR 0108 Consequences.

- **C-M11·6 · The surface is GlobalAdmin + audited + quiet.** Export +
  import are `[Authorize(Roles = GlobalAdmin)]`, each emits **one**
  `AccessAudit` row (`TargetKind "portability"`, `Via = Admin`, verb
  `export` / `import`), and both are hidden from a non-GlobalAdmin.
  Pinned by the Web-surface pins (D9, the gate + the one-audit-row
  assertions).

- **C-M11·7 · Zero new authorization surface.** No new `AccessAction`, no
  new `AccessVia`, no `IAuthorizationService.Decide()` branch, no
  `Audience`. Portability reuses the GlobalAdmin role gate (the ADR
  0101/0105 shape); the content's audiences/grants/delegations travel
  **as data**, the decision engine is untouched. Pinned by the design
  doc §Seams + the U06 handoff (the `IAuthorizationService` surface count
  is unchanged).

- **C-M11·8 · Docs parity holds at the flip.** `Milestones.cs` /
  `README.md` Roadmap / `docs/STATUS.md` / `docs/ARCHITECTURE.md` all move
  M11 to `StatusDone` + M12 to `StatusNext` in the **same** unit (U07),
  and `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its
  single-in-progress pin passing (the AGENTS.md parity contract).

## §pinned tests — the D9 test names (U07)

> **The three acceptance tests** (the closed-loop / handoff /
> part-vs-whole shape, per the design-doc template) + the Web-surface
> pins. The Core tests are `PostgresFixture` (the `postgres:18`
> Testcontainers) + a media temp-dir; the Web pins are NSubstitute (no
> Postgres).

- **`PortabilityRoundTrip_ExportThenImportPreservesContentGraphAndMediaAndRoles`**
  (D9a, the "handoff") — plant a representative instance → **export** →
  **import** into a **fresh** instance → the content graph, the media,
  and the role assignments are preserved and referentially intact.
- **`PortabilityNoSecret_ArchiveContainsNoCredentialMaterial`** (D9b,
  the "closed-loop") — the exported archive contains **no** password hash
  / security stamp / Identity token (the principals POCO field-shape +
  the byte-scan witness over the archive).
- **`PortabilityFailClosed_RejectedArchiveWritesZeroRows`** (D9c, the
  "part-vs-whole") — a wrong-`format` manifest / a truncated media file /
  a dangling reference is rejected **before any write** — the fresh
  instance has **zero** rows after the failed import.
- **`AdminPortabilityControllerTests`** (the Web-surface pins) — the
  GlobalAdmin gate (a non-GlobalAdmin `GET /admin/portability` 403s), the
  one-audit-row per action (`portability.export` / `portability.import`,
  `Via = Admin`), the `portability.*` kw-l keys resolve (the parity).

## §kw-l — the `portability.*` key list (D10)

The **exact** locked `portability.*` keys (U04's export + U06's import;
the closed-key registry + the `KnownTranslationKeys_ParityTests`
enforces the 4-language set):

| Key | Use |
|---|---|
| `portability.index.title` | the `/admin/portability` index page title |
| `portability.export` | the export button label |
| `portability.import` | the import upload form label |
| `portability.confirm.import` | the import confirmation (the destructive-action guard) |
| `portability.status.ok` | the success status (a clean export / a clean import) |
| `portability.status.failure` | the closed-failure-set render (the validate-rejected import) |

Each × **4 languages** (`en` / `de` / `fr` / `da`), the closed-key
registry shape (the ADR 0015 `KnownTranslationKeys` pin).

## §drift-guard — the frozen pins + the drift log

**The frozen pins** (the U01–U06 units copy verbatim from this doc):
the §manifest field set (the `format` version string
`kumunita/portability/1` + the five field names); the §layout (the
`manifest.json` / `docs/{Type}.json` / `media/{Id[0..2]}/{Id}` /
`identity/principals.json` / `config.json` paths); the §inventory
(44 content docs + the `Type` / `Order` / `ReferenceFields` per entry +
the five **Excluded** docs); the §principals (the eight allowed fields +
the excluded credential fields); the §config (the `community` / `locale`
/ `languages` field sets); the §validate (the four checks + the closed
failure set + the apply order); the §surface (the three routes + the
one-audit-row shape); the §pinned tests (the four test names); the §kw-l
(six keys).

**The drift log** (the source-driven refinements U00 locked **in favor of
the source** where the register's prose was imprecise):

1. **`IdentityToken` is excluded from the content inventory** (C-M11·2
   / D3). The register's D7 "every registered domain document type"
   contradicted the source: `IdentityToken` (on `M1DocTypes`) holds a
   high-entropy **secret `Token`** (the verification-link token + the
   seed-admin setup credential) — credential material the C-M11·2
   "no-secret" pin (the "single most load-bearing M11 invariant")
   forbids from traveling. The closed inventory is therefore the
   **content + identity-graph + localization** set, and `IdentityToken`
   is named in the §inventory "Excluded" set (the D3 boundary enforced
   at the source).
2. **The four operational-state docs are excluded from the content
   inventory** (D1 / C-M11·5). `OutboxEmail` / `EmailDeadLetter` /
   `AccessAudit` / `AuditPurgeSummary` (all on `M1DocTypes`) are
   **operational** (an outbox / a dead-letter / an audit log / a purge
   summary) — not resident content. The D1 **restore** semantic (apply
   into a fresh instance; the operator's pre-import backup is the
   rollback path) means a fresh instance re-derives these; they are
   named in the §inventory "Excluded" set.
3. **The `LocaleSettings` + `LanguageCatalog` docs travel via
   `config.json`, not as `docs/` rows** (D2 / D7 resolved). D2's
   "config.json = the instance identity + localizations" already
   claims them; the §inventory therefore does **not** list them as
   content docs (the §config field set carries the `LocaleSettings`
   singleton + the `LanguageCatalog` rows). The §validate integrity
   loop reads `config.json` for the `LanguageCode` target set **before**
   the docs integrity loop. The §apply order (D4) is **unchanged**
   (principals → docs → media → config — the config apply is the last
   step).
4. **The `community_name` field is `CommunityOptions.Name`** (the D2
   `config.json` shape, grounded in source). The register's D2 "the
   community name" is the `CommunityOptions.Name` field (a
   `Kumunita.Core` config object, **not** a domain doc) — the
   `config.json` `community.name` field + the `manifest.json`
   `community_name` field both read from it. The `CommunityOptions` is
   a config object (like `LocaleSettings`), so it travels via
   `config.json` (the §config `community` block), **not** as a `docs/`
   row.

**The drift-guard rule** (the unit-series rule): a unit that finds the
source has moved beyond a frozen pin records the drift **here** (an
append, not a rewrite) and resolves in favor of the source; it never
silently re-derives a pin from a stale prose. The closed inventory
(§inventory) is the **exact** set — a future lane that adds a
`*DocTypes` document moves the §inventory list + the U07 round-trip
plant **together**, and records the drift here.
