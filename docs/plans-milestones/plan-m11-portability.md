# Plan: M11 — Portability (import/export)

> **In progress.** Unit register (secondary tier). Living handoff note:
> `docs/plans-milestones/m11-portability-handoff-notes.md` (scratch tier — one
> `## U#` section per unit, appended, never rewritten). The authoritative
> design (primary tier) — `docs/design/m11-portability-design.md` — is
> authored by **U00** and locked before any code unit runs; the decision
> record is **ADR 0108**.
>
> **Unit plans (this convention):** each unit ships its own self-contained
> plan file in `docs/plans-milestones/in-progress/`
> (`portability-u00.md` … `portability-u06.md`). When a unit is done,
> its plan file moves to `docs/plans-milestones/done/`. A unit agent reads
> **its own plan file + its entry reads** — it does not need to re-derive
> this register, which is why each unit plan restates the context it needs.
>
> **U00 is the sign-off gate** for the design decisions and the invariant
> contract; the decisions below are the [PROPOSED] set U00 locks (or the
> user vetoes before U00 runs — this register is the last cheap place to
> change them).
>
> **Atomicity contract.** This register is sized for **~32K-context
> agents**: every unit is one coherent step — ≤ 5 files, ≤ ~400 LOC of
> change, a short entry-reads list (3–6 files), and an Exit check that fits
> in one build + test run. A unit's full context (its unit-plan file + its
> entry reads + its deliverables) fits in one 32K window with headroom.
> **Unit-series rule: never touch files outside your own Deliverables;
> never rewrite the design doc outside the drift-guard note; no tests beyond
> the pinned list; no new authorization surface (`AccessAction`/`AccessVia`)
> or content-decision seam.**
>
> M11 is **greenfield and operator-scoped**: there is no `Portability`
> context, no export/import code, no `manifest.json` shape, no archive
> reader/writer anywhere in the tree (grep-confirmed). What M11 builds on:
> the **Marten domain documents** across the registered `*DocTypes` surfaces
> (`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` / `M6DocTypes` /
> `M9DocTypes` / `MediaDocTypes` / `TagDocTypes` / `PageDocTypes`), the
> **ASP.NET Identity** tables in the `identity` schema (the principals —
> *not* the domain docs), the **content-addressed media volume** (ADR 0011,
> `MediaObject` catalog doc + the bytes at `{root}/{Id[0..2]}/{Id}`), the
> **GlobalAdmin admin-surface house pattern** (ADR 0101/0105/0106 —
> `[Authorize(Roles = GlobalAdmin)]` + one `AccessAudit` row `Via = Admin`),
> and **BCL `System.IO.Compression`** (a ZIP reader/writer needs no new
> package — the lean-stack + tsc-only discipline holds). M11 is a
> **surface-milestone in the ADR 0101 / 0105 operator-plane sense**: an
> operator (GlobalAdmin) moves the *whole instance* in and out, as data —
> it is **not** a resident content-decision lane (it adds no `Audience`, no
> `AccessAction`, no `IAuthorizationService` branch).

---

## Understanding

M1–M10 have grown a full resident surface — identity, groups, delegation,
directory, posts, events + calendar, projects + boards, pages, tags, search,
notifications, messaging, PWA. But a self-hosted single-neighborhood platform
lives or dies on one world-seam the roadmap row names (ARCHITECTURE.md value
chain: "the loop closes *into* the residents' lives"): **the neighborhood's
data must be able to leave the platform and come back.** Today it is not:
the only "export" is the `pg_dump` an operator runs by hand (OPS.md), which
carries the Postgres schema *and* the credential hashes and *and* the media
volume as three separate artifacts that do not travel together and do not
verify against each other. And there is **no import path at all** — a fresh
instance is a blank instance.

M11 closes that seam in the two directions the row names (import / export):

1. **Export** — a GlobalAdmin produces **one** portable, **versioned**
   archive (`*.kumunita`) holding the whole instance's *content* (every
   registered domain document), the *media bytes* (content-addressed, dedup
   preserved), the *identity graph* (who-is-who + roles + delegation +
   guardian + membership — as data, **without** the credential material),
   and the *instance identity + localizations* (community name, the language
   catalog, the locale settings). It is a single self-describing file an
   operator can store as a backup or carry to a new host.
2. **Import (restore)** — a GlobalAdmin on a **fresh** (or current) instance
   ingests that archive: it **validates the whole archive first** (format
   version, per-document-type schema sanity, **referential integrity** —
   every referenced id resolves within the archive — and the media bytes),
   and only then **applies it in one commit** (documents in a pinned
   dependency order, the media bytes, the re-created principals with their
   secrets **reset**, the config). A rejected archive writes **nothing**.

What M11 is **not** (the deferred lanes, own ADRs): **per-slice** portability
(one group's posts out, the ARCHITECTURE.md "cross-neighborhood migration"
fragment), **cross-instance** portability (neighborhood A → B — the
cross-neighborhood federation deferral), **import-merge** (applying an
archive on top of a *diverged* existing instance — the reconciliation
problem), and the **backup-automation / cron** surface (OPS.md). M11 ships the
**whole-instance export + the fail-closed restore**, and nothing else — that
is the loop closing into the residents' lives, in the ADR 0101 / 0105
operator-plane sense.

## Assumptions / decisions — [PROPOSED, lockable by ADR 0108 in U00]

> **Open veto.** These are the decisions the user can still change cheaply —
> **before U00 runs**. After U00 they are locked by
> `docs/design/m11-portability-design.md` + ADR 0108 and changeable only
> via the drift guard.

- **D1 · Scope = whole-instance export + import (restore).** M11 exports and
  imports the **entire instance**: every registered domain document type, the
  media bytes, the identity graph (principals + the delegation/guardian/
  membership/moderator domain docs), and the instance identity + localizations.
  The **import semantic is restore** (apply into a fresh instance; the current
  instance's state is the operator's pre-import backup, the rollback path).
  **Out of scope → deferred lanes (own ADRs):** per-slice export (one group's
  posts), cross-instance portability (A → B), import-merge (onto a diverged
  instance), and the backup-automation / cron surface. Alternative considered
  and rejected: shipping per-group migration now — the repo is explicitly
  single-neighborhood with no multi-tenant model, and cross-neighborhood
  federation is a named deferral (README §Deferred); M11 owns the *whole*
  instance the value-chain row names, not the fragments.
- **D2 · The archive is a versioned ZIP, BCL-only (no new dependency).**
  `System.IO.Compression.ZipArchive` (in the .NET 10 framework — the
  lean-stack + tsc-only discipline holds; **no** new package). Layout:
  `manifest.json` (self-describing, the C-M11·1 shape) + `docs/{DocType}.json`
  (one JSON-array file per registered domain document type — the per-type
  isolation that keeps the round-trip testable) + `media/{Id[0..2]}/{Id}`
  (the content-addressed bytes, the **same** layout as the local volume, so
  import is a byte-copy and dedup-by-content-hash is preserved) +
  `identity/principals.json` (the C-M11·2 no-secret graph) + `config.json`
  (the instance identity + localizations). `*.kumunita` is the file
  extension (a content marker, not a format claim — the `format` field in
  `manifest.json` is the authority).
- **D3 · The identity travels as the graph, never the secrets.** The archive
  carries the **principals** (`subjectId`, `username`, `email`, `normalizedEmail`,
  `displayName`, `roles`, and the lockout/state flags — the data needed to
  re-create a signable principal) **and** the delegation / guardian /
  membership / moderator **domain documents** (those live in the M1 domain
  context, not the Identity schema, so they travel with the content). It
  **never** carries credential material: no password hash, no security stamp,
  no access-token / refresh-token / recovery-code, no Identity sign-in record.
  On import the principals are **re-created** (keyed by the exported
  `subjectId`) and their secrets are **reset** (a fresh, non-portable
  password; the resident re-authenticates). This is the privacy pin (C-M11·2)
  and the ADR 0028 GU precedent ("no standing to carry a secret") applied to
  the operator's own archive. **The no-secret boundary is the single most
  load-bearing M11 invariant** — it is pinned by a field-shape test (the
  principals POCO has no hash/stamp/token field) *and* a byte-scan witness
  (the archive contains none of the Identity credential columns).
- **D4 · Import is validate-then-apply, fail-closed, atomic.** The **validate
  phase** runs to completion **before any write**: (a) the `manifest.json`
  `format` is one the build understands (else reject — the C-M11·1 pin);
  (b) each `docs/{Type}.json` deserializes into its declared POCO set;
  (c) **referential integrity** — every id referenced by any document
  (an author's `subjectId`, a post's `ComponentId`, a reply's `PostId`, an
  RSVP's `EventId`, a message's `ConversationId`, a media `Id`, …) resolves
  to a row **within the archive** (the closed reference map is pinned in the
  design doc §import-order); (d) the media manifest's bytes are all present
  and their content-hash matches their path. **Any failure ⇒ zero writes.**
  The **apply phase** is one commit: re-create the Identity principals (so
  `subjectId` sign-in works) → store the domain documents in the pinned
  dependency order → copy the media bytes → apply the config. On a mid-apply
  failure the instance's state is the **documented restore path** (the
  operator's pre-import backup / a fresh instance + re-import; OPS.md) — M11
  never silently accepts a half-import. Alternative considered and rejected:
  a "best-effort partial import" — it is untestable and violates the
  privacy/consistency posture (a half-imported instance is exactly the
  "parts-work-seams-don't" failure the philosophy names).
- **D5 · The surface is the admin web plane (GlobalAdmin), audited, quiet.**
  `GET /admin/portability` (the index: the export button + the import
  upload form + a status area) + `GET /admin/portability/export` (streams the
  `*.kumunita` archive) + `POST /admin/portability/import` (applies the
  uploaded archive). All three are `[Authorize(Roles = GlobalAdmin)]`;
  **export** and **import** each emit exactly **one** `AccessAudit` row
  (`TargetKind` `"portability"`, `Via = Admin`, verb `"export"` / `"import"`).
  A plain resident — including a moderator — never sees the surface (the
  ADR 0101 / 0105 admin-surface shape, no break-glass read). Alternative
  considered and rejected: a CLI / backup-automation surface now — that is an
  OPS.md follow-on lane (D1's deferred list); M11 keeps the "operator in the
  app" house pattern so the operator is a GlobalAdmin resident, consistent
  with every other operator surface in the repo.
- **D6 · The media travels content-addressed, with the catalog doc.** The
  bytes are copied **verbatim** into the archive at the **same**
  `{Id[0..2]}/{Id}` layout the local volume uses (ADR 0011 C-MED·3/4/7), so
  import is a byte-copy and the content-hash dedup is preserved (re-importing
  the same file is a no-op at the byte layer). The `MediaObject` catalog doc
  travels as a `docs/MediaObject.json` row (the surviving catalog reference a
  `pg_dump` carries, per its own doc-comment). A `MediaObject` listed in the
  manifest whose bytes are missing / mismatched is a **validate-phase failure**
  (fail-closed, C-M11·3) — the archive never imports a doc pointing at a
  phantom byte.
- **D7 · The closed document inventory + import order is pinned in the design
  doc from source.** U00 reads **every** `*DocTypes` surface (`M1DocTypes` /
  `M3DocTypes` / `M4DocTypes` / `M5DocTypes` / `M6DocTypes` / `M9DocTypes` /
  `MediaDocTypes` / `TagDocTypes` / `PageDocTypes`) and locks the **exact**
  closed inventory — each entry: the document `Type`, its archive file
  (`docs/{Type}.json`), its **import order** (parents before children), and
  its **reference fields** (which of its fields point at another entry's id) —
  that U01's registry implements as **data**. The inventory is data the
  U02/U05 loops iterate generically, **not** per-type code — that is what
  keeps the export/import units atomic despite ~40 document types.
- **D8 · One new `Portability` context in Core — an operator lane, not a
  content-decision lane.** `Kumunita.Core/Portability/` holds the manifest
  model, the document-inventory registry, the archive (de)serializer, and the
  `PortabilityService` (export / validate / apply). The service composes
  `IDocumentStore` (Marten, for the domain docs), the Identity `UserManager`
  / `RoleManager` (for the principals re-creation + roles), and
  `IMediaStore` / `IMediaFileStore` (for the bytes). It is registered in
  `DependencyInjection.cs` like its siblings. **No** new `AccessAction`,
  **no** new `AccessVia`, **no** `IAuthorizationService` branch, **no**
  `Audience` — portability is an operator action over the instance, decided
  by the GlobalAdmin role gate (C-M11·7).
- **D9 · Tests: a Core Postgres round-trip + the no-secret pin + the
  fail-closed pin + the Web surface pins.** The **three acceptance tests**
  (the closed-loop / handoff / part-vs-whole shape, per the design-doc
  template): (a) **round-trip** — plant a representative instance (a group,
  a post + reply + a translation, an event + RSVP, a board + lane + todo, a
  conversation + message, a page + translation, a media object + its bytes,
  a delegation grant, a guardian link, a couple of principals with roles) →
  **export** → import into a **fresh** instance → the content graph, the
  media, and the role assignments are preserved and referentially intact
  (the "handoff" — the data moves between two instances); (b) **no-secret** —
  the exported archive contains **no** password hash / security stamp /
  Identity token (the principals POCO has no such field + a byte-scan
  witness over the archive) (the "closed-loop" on the trust boundary);
  (c) **fail-closed** — a wrong-`format` manifest / a truncated media file / a
  dangling reference is rejected **before any write** — the fresh instance
  has **zero** rows after the failed import (the "part-vs-whole" — the whole
  import is all-or-nothing, not the parts). Plus Web-surface pins (the
  GlobalAdmin gate, the one-audit-row per action, the kw-l keys resolve).
- **D10 · kw-l keys + the deferred lanes.** The operator-facing strings
  (`portability.index.title` / `portability.export` / `portability.import` /
  `portability.confirm.import` / `portability.status.*`) × 4 languages
  (the closed-key registry + `KnownTranslationKeys_ParityTests` enforces).
  **Deferred lanes** (named in ADR 0108 Consequences, own ADRs): per-slice /
  cross-instance portability (ARCHITECTURE.md line 784 + the
  cross-neighborhood federation deferral), import-merge, the backup-automation
  / cron surface (OPS.md), and the other world-seam exports (M12 stays iCal).

## Invariants — [PROPOSED, U00 locks into the design doc]

- **C-M11·1 · The archive is versioned + self-describing.** `manifest.json`
  carries a `format` version (`kumunita/portability/1` for M11), the
  `generated_at`, the community name, the per-document-type counts, and the
  media manifest (the file list + sizes + content types). Import **rejects**
  any archive whose `format` it does not understand — it fails closed, no
  partial apply. Pinned by the fail-closed test (D9c).
- **C-M11·2 · The archive travels the data, never the secrets.** The archive
  contains the identity **graph** (principals + the delegation/guardian/
  membership/moderator domain docs) + the content + the media + the config.
  It contains **no** credential material (no password hash, no security
  stamp, no access/refresh/recovery token, no Identity sign-in record). On
  import the principals are re-created and their secrets **reset**. Pinned
  by the no-secret test (D9b) — the single most load-bearing M11 invariant.
- **C-M11·3 · The media travels content-addressed + whole.** The bytes are
  copied verbatim (dedup-by-content-hash preserved); the `MediaObject`
  catalog doc + its bytes are either both present or the archive is rejected.
  Pinned by the round-trip test (D9a, the media leg) + the fail-closed test
  (D9c, the truncated-bytes leg).
- **C-M11·4 · Import is fail-closed + atomic.** The validate phase runs to
  completion **before** any write (format + per-type sanity + referential
  integrity + media verification); any failure ⇒ **zero writes**. The apply
  phase is one commit in the pinned dependency order; a mid-apply failure is
  the documented restore path, never a silently-accepted half-import. Pinned
  by the fail-closed test (D9c).
- **C-M11·5 · Whole-instance, not per-slice.** M11 exports/imports the
  **entire** registered instance. Per-slice / cross-instance / import-merge
  are **follow-on lanes** (own ADRs), not M11 scope. Pinned by the
  D1 deferral close in the design doc + the ADR 0108 Consequences.
- **C-M11·6 · The surface is GlobalAdmin + audited + quiet.** Export + import
  are `[Authorize(Roles = GlobalAdmin)]`, each emits **one** `AccessAudit`
  row (`TargetKind "portability"`, `Via = Admin`, verb `export` / `import`),
  and both are hidden from a non-GlobalAdmin. Pinned by the Web-surface pins
  (D9, the gate + the one-audit-row assertions).
- **C-M11·7 · Zero new authorization surface.** No new `AccessAction`, no new
  `AccessVia`, no `IAuthorizationService.Decide()` branch, no `Audience`.
  Portability reuses the GlobalAdmin role gate (the ADR 0101/0105 shape);
  the content's audiences/grants/delegations travel **as data**, the decision
  engine is untouched. Pinned by the design doc §Seams + the U06 handoff
  (the `IAuthorizationService` surface count is unchanged).
- **C-M11·8 · Docs parity holds at the flip.** `Milestones.cs` / `README.md`
  Roadmap / `docs/STATUS.md` / `docs/ARCHITECTURE.md` all move M11 to
  `StatusDone` + M12 to `StatusNext` in the **same** unit (U07), and
  `tests/Kumunita.Web.Tests/MilestonesTests.cs` keeps its single-in-progress
  pin passing (the AGENTS.md parity contract).

## FACES — [PROPOSED, U00 locks]

- **F1 · The neighborhood's life is portable out.** A GlobalAdmin downloads
  one `*.kumunita` archive holding the whole instance's content + media +
  identity graph + config — the loop closes out of the platform
  (C-M11·1/3/5).
- **F2 · The archive comes back intact.** Importing that archive into a fresh
  instance restores the content graph (posts, events, projects, conversations,
  pages, groups, roles, delegations) + the media, referentially intact
  (C-M11·1/4).
- **F3 · The secrets never travel.** The archive contains no credential
  material; after import the residents re-authenticate (fresh passwords) —
  the trust boundary is the archive, not the file's bytes (C-M11·2).
- **F4 · A corrupt archive is refused wholesale.** A wrong-version /
  truncated / referentially-inconsistent archive is rejected before any write;
  the instance is never left half-imported (C-M11·4).
- **F5 · The operator surface is quiet + audited.** Export + import are
  GlobalAdmin-only, each leaves exactly one audit row, and are invisible to a
  plain resident (C-M11·6/7).

## Approach

- **Track A — Docs (U00):** the design doc + ADR 0108. U00 is the sign-off
  gate; it locks D1–D10, the C-M11 invariants, F1–F5, the **closed document
  inventory + import order + reference map** (D7 — read from the `*DocTypes`
  surfaces), the **manifest format** (D2), the **no-secret principals shape**
  (D3), the **fail-closed validate/apply contract** (D4), the pinned test
  names (U07), the kw-l key list (U04/U06), and the deferred-lane list (D10).
- **Track B — Framework (U01):** the `Kumunita.Core/Portability/` context —
  the manifest model, the document-inventory registry (D7's data), and the
  ZIP archive (de)serializer (D2) — the uniform machinery U02–U06 loop.
- **Track C — Export (U02–U04):** the document + identity + config export
  (U02), the media bytes + manifest finalization (U03), the web surface +
  the `portability.export` audit + the kw-l keys (U04).
- **Track D — Import (U05–U06):** the validate-then-apply of documents +
  media, fail-closed (U05), the identity re-creation (secrets reset) +
  config apply + the web surface + the `portability.import` audit + the
  kw-l keys (U06).
- **Close (U07):** the Core round-trip / no-secret / fail-closed tests + the
  Web-surface pins; the `Milestones.cs` / `README.md` / `docs/STATUS.md` /
  `docs/ARCHITECTURE.md` flip; the `MilestonesTests.cs` re-pin; the unit-plan
  files → `done/`; the handoff `## Summary`.

## Workflow (three-tier, per-unit)

Same contract as the M10 register: primary tier =
`docs/design/m11-portability-design.md` (authored by U00; the only
authority after it lands); secondary = this register; scratch = the
handoff note (one `## U#` section per unit: entry state / what ran /
drift / open items). Per unit: Goal → Entry reads (3–6 files) →
Deliverables (≤ 5 files) → Exit (build green + handoff entry).
**Unit-series rule: never touch files outside your own Deliverables;
never rewrite the design doc outside the drift-guard note; no tests
beyond the pinned list; no new authorization surface or content-decision
seam.**

Tests run per AGENTS.md: `dotnet build Kumunita.slnx -c Debug`, then
`dotnet exec tests\Kumunita.Web.Tests\bin\Debug\net10.0\Kumunita.Web.Tests.dll`
and `dotnet exec tests\Kumunita.Core.Tests\bin\Debug\net10.0\Kumunita.Core.Tests.dll`
(the round-trip / no-secret / fail-closed tests are **Core** tests — they
need the Testcontainers Postgres + a media temp-dir, the `PostgresFixture`
harness shape; the Web-surface pins are **Web** tests, the NSubstitute
no-Postgres shape). `Kumunita.Core.Tests` takes ~20 s (it starts
`postgres:18` via Testcontainers and leaves Docker containers behind if the
process is killed — clean up with `docker container prune`).

---

## U00 — Lock the design: `m11-portability-design.md` + ADR 0108

**Goal.** Author the primary tier and the decision record. This is the
**sign-off gate** for the whole milestone: lock D1–D10, the C-M11
invariants, F1–F5, the **closed document inventory + import order +
reference map** (D7 — the exact `Type` / file / order / reference-fields per
entry, **read from the `*DocTypes` surfaces**, not invented), the
**manifest format** (D2 — the exact `manifest.json` field set + the
`format` version string), the **no-secret principals shape** (D3 — the exact
`identity/principals.json` field set + the explicit **excluded** credential
fields), the **config shape** (the exact `config.json` field set), the
**fail-closed validate/apply contract** (D4 — the exact validate checks +
the apply order + the restore path), the **pinned test names** (U07), the
**kw-l key list** (U04/U06), and the **deferred-lane list** (D10). ADR 0108
records: decisions + alternatives considered (secrets travel; partial
import; per-group now; a CLI surface now) + the Consequences hand-off (the
deferred lanes: per-slice / cross-instance, import-merge, backup-automation,
the iCal / other world-seam exports).

**Entry reads (6).** `docs/philosophy/templates/design-doc.md` (the required
section set); `docs/design/m10-pwa-responsive-design.md` (the house style — a
recent milestone's doc, its §Invariants / §FACES / §drift-guard shape);
`docs/adr/0107-pwa-and-responsive-design.md` + `docs/adr/0105-messaging.md`
(the ADR shape to mirror — the operator-plane shape, the "a milestone" close,
the deferred-lane language, and the ADR 0105 no-break-glass / floor
precedent that D3/D4 build on); `src/Kumunita.Core/M1DocTypes.cs` +
`M3DocTypes.cs` + `M4DocTypes.cs` + `M5DocTypes.cs` (the D7 inventory source —
the registered document types + their business-key / feed indexes reveal the
referential relationships); `src/Kumunita.Core/Media/IMediaFileStore.cs` +
`IMediaStore.cs` + `MediaObject.cs` (the D6 media-travel shape — the
content-addressed layout + the catalog doc); `src/Kumunita.Core/Localization/LanguageCatalog.cs`
(the D2 `config.json` shape — the `LocaleSettings` + language-catalog state
that travels as instance identity).

**Deliverables (3).** `docs/design/m11-portability-design.md`;
`docs/adr/0108-portability-import-export.md`; `docs/adr/README.md`
(one index row, after the 0107 row).

**Exit.** `dotnet build Kumunita.slnx -c Debug` still green (docs only).
Handoff entry: decisions locked/vetoed, any D-item text changed, the
**exact closed document inventory + import order + reference map** as
written (U01's registry copies verbatim), the **exact manifest format** as
written (U02's manifest builder copies verbatim), the **exact
no-secret principals shape** as written (U02's extractor + U06's re-creator
copy verbatim), the **exact config shape** as written, the **exact
validate checks + apply order** as written (U05 copies verbatim), the
**ADR number confirmed free** (the index ran 0001–0107; `0108` is next —
verified against `docs/adr/README.md`).

---

## U01 — Framework: the `Portability` context, manifest model, inventory registry, archive (de)serializer

**Goal.** D2/D7 rendered as code: the `Kumunita.Core/Portability/` context —
the **manifest model** (the `manifest.json` POCO set, the D2 field set), the
**document-inventory registry** (D7's closed `Type` / file / order /
reference-fields list, as **data** the U02–U06 loops iterate), and the
**archive (de)serializer** (the `System.IO.Compression.ZipArchive` read +
write over the D2 layout — `manifest.json`, `docs/{Type}.json`,
`media/{Id[0..2]}/{Id}`, `identity/principals.json`, `config.json`). No
export/import *service* yet (U02–U06 own that); this unit is the uniform
machinery they compose.

**Entry reads (5).** `docs/design/m11-portability-design.md` (§manifest — the
locked field set; §inventory — the locked `Type` / file / order /
reference-fields list; §layout — the D2 archive layout);
`src/Kumunita.Core/M1DocTypes.cs` + `M3DocTypes.cs` (the document types the
registry enumerates — U00 pinned the list, U01 turns it into the registry
data; the `M*DocTypes` surfaces confirm each type's namespace + the
business-key fields the reference map needs); `src/Kumunita.Core/Media/IMediaFileStore.cs`
(the content-addressed path derivation the media (de)serializer reuses —
`{root}/{Id[0..2]}/{Id}`); `src/Kumunita.Core/DependencyInjection.cs`
(the registration shape — the `PortabilityService` + the registry register
next to its siblings); `src/Kumunita.Core/Kumunita.Core.csproj` (confirm
`System.IO.Compression` needs no package — it is in the BCL; record if a
`System.IO.Packaging` / `ZipArchive` using is needed).

**Deliverables (≤ 5 files).**
`src/Kumunita.Core/Portability/PortabilityManifest.cs` (new — the
`manifest.json` POCO set: `format`, `generated_at`, `community_name`,
`docCounts` (a `Dictionary<string,int>`), `mediaManifest` (the file list +
sizes + content types), + the `format` version constant `kumunita/portability/1`);
`src/Kumunita.Core/Portability/PortabilityDocTypes.cs` (new — the D7 closed
inventory registry: the ordered list of `{ Type, FileName, Order, ReferenceFields }`,
**data** read from U00's pinned list, with a `ByType` lookup + the ordered
iteration U02/U05 consume); `src/Kumunita.Core/Portability/KumunitaArchive.cs`
(new — the archive (de)serializer: `WriteAsync(Stream, …)` +
`ReadAsync(Stream)` over the D2 layout using `ZipArchive`; the
`docs/{Type}.json` round-trip via `System.Text.Json`; the media-path
derivation reused from `IMediaFileStore`'s convention);
`src/Kumunita.Core/DependencyInjection.cs` (register the registry + the
serializer next to its siblings); `src/Kumunita.Core/Portability/PortabilityService.cs`
(new — the service **shell**: the ctor taking `IDocumentStore` + the Identity
`UserManager` + `IMediaStore`/`IMediaFileStore` + the registry, with the
export / validate / apply method **signatures** only, `NotImplemented` or
empty bodies U02–U06 fill in — the seam the Web layer + the tests target).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green. The
`Portability` context compiles; the registry enumerates the D7 list; the
archive round-trips a trivial `manifest.json` + one `docs/*.json` (a
smoke, not a pinned test — the real tests are U07's). Handoff entry: the
registry's entry count (the D7 list length, so U02/U05 know the loop size),
the `PortabilityService` ctor + method signatures as written (U02–U06 copy
verbatim), the `KumunitaArchive` public surface (the write/read entry
points), the `format` version string, any compile warnings.

---

## U02 — Export: documents + the no-secret principals + the config

**Goal.** D3/D7 rendered on the export side: the `PortabilityService` export
path — loop the registry (D7's data) and write each `docs/{Type}.json`
(the generic JSON round-trip over the POCOs, **not** per-type code); extract
the **no-secret principals** into `identity/principals.json` (the D3 field
set, the credential columns **excluded** — the C-M11·2 boundary enforced at
the source, the `UserManager` read that *selects* the allowed fields and
drops the hash/stamp/token columns); snapshot the **config** into
`config.json` (the D2 instance-identity + localizations set). The media
bytes + the manifest finalize are U03's.

**Entry reads (5).** `docs/design/m11-portability-design.md` (§principals —
the locked no-secret field set + the **excluded** credential fields;
§config — the locked config field set; §inventory — the D7 registry U02
loops); `src/Kumunita.Core/Portability/PortabilityService.cs` +
`PortabilityDocTypes.cs` + `KumunitaArchive.cs` (U01's shell + registry +
serializer — the seam U02 fills); `src/Kumunita.Core/Identity/UserManager.cs`
(or the Identity `UserManager` setup in `DependencyInjection.cs`) (the
principal read — the `UserManager` / `IdentityUser` shape U02 extracts from,
and the **excluded** credential fields to confirm are dropped);
`src/Kumunita.Core/Localization/LanguageCatalog.cs` (the `LocaleSettings` +
language-catalog state the `config.json` snapshot reads);
`src/Kumunita.Core/DependencyInjection.cs` (the `PortabilityService` ctor
wiring — confirm the `UserManager` / `LocaleSettings` deps resolve).

**Deliverables (≤ 5 files).**
`src/Kumunita.Core/Portability/ExportDocumentsAsync.cs` (new or a method on
`PortabilityService` — the registry loop: for each D7 entry, load all rows
via `IDocumentStore` + write `docs/{Type}.json` through the `KumunitaArchive`
writer; the `docCounts` the manifest carries);
`src/Kumunita.Core/Portability/PrincipalsExport.cs` (new — the no-secret
principals extractor: the `UserManager` read that projects **only** the D3
allowed fields (`subjectId`, `username`, `email`, `normalizedEmail`,
`displayName`, `roles`, lockout/state) and **drops** the credential columns —
the C-M11·2 boundary at the source);
`src/Kumunita.Core/Portability/ConfigExport.cs` (new — the `config.json`
snapshot: the community name + the `LocaleSettings` + the language-catalog
state, per the D2 config field set);
`src/Kumunita.Core/Portability/PortabilityService.cs` (the export method
bodies filled in, composing the three above + the `KumunitaArchive.WriteAsync`).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green. The export
loop writes one `docs/{Type}.json` per registry entry; the principals
extraction **drops** the credential columns (the C-M11·2 source boundary —
the U07 no-secret test witnesses it end-to-end). Handoff entry: the export
loop's entry count (matches the registry), the **exact** principals field set
as written (U06's re-creator copies verbatim), the **exact** config field set
as written, the credential columns **confirmed dropped** (the C-M11·2 pin),
the `docCounts` shape, any compile warnings.

---

## U03 — Export: the media bytes + the manifest finalization

**Goal.** D6 rendered: the export path completes — copy the **media bytes**
into the archive at the `{Id[0..2]}/{Id}` layout (the same as the local
volume, the C-M11·3 pin), build the **media manifest** (the file list +
sizes + content types, read from the `MediaObject` catalog), and **finalize
the manifest** (`manifest.json` — the `format` version, the `generated_at`,
the community name, the `docCounts` from U02, the media manifest). After
this unit the export produces a **complete, valid** `*.kumunita` archive
(the U04 web surface just streams it).

**Entry reads (4).** `docs/design/m11-portability-design.md` (§media — the
D6 layout + the media-manifest field set; §manifest — the finalize field
set); `src/Kumunita.Core/Portability/PortabilityService.cs` +
`KumunitaArchive.cs` (U02's export path + the serializer's media write seam);
`src/Kumunita.Core/Media/IMediaFileStore.cs` + `IMediaStore.cs` (the
`OpenReadAsync` + the `MediaObject` catalog — the source of the bytes + the
media manifest the U03 copies); `src/Kumunita.Core/Media/MediaObject.cs`
(the catalog shape — the `Id` (the content hash) + the `SizeBytes` /
`ContentType` the media manifest carries).

**Deliverables (≤ 3 files).**
`src/Kumunita.Core/Portability/MediaExport.cs` (new — the media byte copy:
for each `MediaObject` in the catalog, `OpenReadAsync` the bytes + write them
into the archive at the `{Id[0..2]}/{Id}` layout (the `KumunitaArchive`
writer's media seam) + accumulate the media-manifest entry);
`src/Kumunita.Core/Portability/ManifestFinalize.cs` (new or a method — the
`manifest.json` finalize: the `format` version + the `generated_at` + the
community name + the `docCounts` (from U02) + the media manifest (from
U03));
`src/Kumunita.Core/Portability/PortabilityService.cs` (the export method
completes — the U02 doc/principal/config write + the U03 media + the
manifest finalize, one `ExportAsync` producing a complete archive `Stream`).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green. A
`PortabilityService.ExportAsync` over a seeded store produces a complete
`*.kumunita` archive (the U07 round-trip test witnesses it end-to-end).
Handoff entry: the media-copy entry count (matches the catalog), the
**exact** media-manifest field set as written (U05's validator copies
verbatim), the **exact** finalized manifest field set as written, the
`ExportAsync` public signature (the U04 web surface + the U07 test target it
verbatim), any compile warnings.

---

## U04 — Export: the web surface + the `portability.export` audit + the kw-l keys

**Goal.** D5 rendered on the export side: the GlobalAdmin web surface —
`GET /admin/portability` (the index: the export button + the import upload
form + a status area) + `GET /admin/portability/export` (streams the
`*.kumunita` archive, the `Content-Disposition: attachment` the ADR 0034
attachment lane's shape). The **one** `portability.export` `AccessAudit` row
(`TargetKind "portability"`, `Via = Admin`, verb `export`) — emitted by the
service, the controller adds none (the ADR 0105 `messaging.toggle` shape).
The **kw-l keys** for the operator-facing strings (the D10 list, × 4
languages, the closed-key registry + the parity test).

**Entry reads (5).** `docs/design/m11-portability-design.md` (§surface — the
D5 route + action set + the one-audit-row shape; §kw-l — the locked key list);
`src/Kumunita.Web/Controllers/AdminMessagingController.cs` (the ADR 0105
admin-surface shape to mirror — the `[Authorize(Roles = GlobalAdmin)]`, the
one-audit-row, the `KumunitaPrincipal.SubjectId` actor extraction, the
`TempData["info"]` + redirect); `src/Kumunita.Web/Views/Admin/Messaging.cshtml`
(or the matching ADR 0105 view) (the admin-view shape to mirror — the card +
the form + the status area); `src/Kumunita.Web/Controllers/AttachmentController.cs`
(the `Content-Disposition: attachment` + the `File(...)` streaming shape the
`export` action reuses); `src/Kumunita.Core/Localization/KnownTranslationKeys.cs`
(the closed-key registry — the `portability.*` insertion point + the
4-language parity shape).

**Deliverables (≤ 5 files).**
`src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (new — the
`[Authorize(Roles = GlobalAdmin)]` controller: `GET Index` (the index view),
`GET Export` (the `PortabilityService.ExportAsync` → `File(stream, "application/octet-stream",
"kumunita.kumunita")`, the one `portability.export` audit row emitted by the
service — the controller adds none);
`src/Kumunita.Web/Views/Admin/Portability.cshtml` (new — the index view: the
export button (a `<a href="~/admin/portability/export">`), the import upload
form (U06's POST target — the form + the file input + the confirm, rendered
now so the surface is whole), the status area (`TempData`));
`src/Kumunita.Core/Portability/PortabilityService.cs` (the `portability.export`
audit row emission in the export path — the one `AccessAudit` row,
`Via = Admin`, verb `export`, the ADR 0105 shape);
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the `portability.*`
kw-l keys × 4 languages — the D10 list, the closed-key registry + the parity);
`src/Kumunita.Web/Views/Shared/_AdminNav.cshtml` (or the matching admin-nav
partial — the one nav link to `/admin/portability`, the ADR 0105 nav-entry
precedent).

**Exit.** `run_build` on `Kumunita.Web` green; the `portability.*` parity
test passes (the `KnownTranslationKeys_ParityTests` enforces the 4-language
set). A GlobalAdmin `GET /admin/portability/export` streams a complete
`*.kumunita` archive + emits the one `portability.export` audit row (the
U07 Web-surface pin witnesses it). Handoff entry: the exact route + action
names as written (U06's import + the U07 pins target verbatim), the
one-audit-row shape (`TargetKind` / `Via` / verb) as written, the **exact**
`portability.*` kw-l keys + their 4-language strings as written (U06 reuses
the shared ones), the nav-link insertion, any compile warnings.

---

## U05 — Import: the validate-then-apply of documents + media, fail-closed

**Goal.** D4 rendered: the `PortabilityService` import path — the **validate
phase** (runs to completion **before any write**, the C-M11·4 pin): (a) the
`manifest.json` `format` is one the build understands (else reject —
C-M11·1); (b) each `docs/{Type}.json` deserializes into its declared POCO
set (the registry's `Type`); (c) **referential integrity** — every id
referenced by the registry's `ReferenceFields` resolves to a row within the
archive (the D7 reference map, data-driven, the uniform loop — **not**
per-type code); (d) the media manifest's bytes are all present + their
content-hash matches the path (the C-M11·3 pin). **Any failure ⇒ zero
writes.** The **apply phase** (one commit): store the domain documents in the
registry's **import order** (parents before children, the D7 order) + copy
the media bytes into the volume (the C-M11·3 pin). The identity re-creation
+ the config apply are U06's.

**Entry reads (5).** `docs/design/m11-portability-design.md` (§validate — the
locked validate checks + the C-M11·1/3/4 pins; §apply-order — the D7 registry
order; §inventory — the D7 reference map the integrity loop drives);
`src/Kumunita.Core/Portability/PortabilityService.cs` +
`KumunitaArchive.cs` + `PortabilityDocTypes.cs` (U01's shell + serializer +
registry — the seam U05 fills; the registry's `Order` + `ReferenceFields` the
apply + the integrity loop consume); `src/Kumunita.Core/Media/IMediaFileStore.cs`
(the `PutAsync` + the content-addressed layout the media apply writes to);
`src/Kumunita.Core/M1DocTypes.cs` (the `Profile.SubjectId` identity + the
`GroupMembership` business key — the referential relationships the integrity
loop checks the *against*, confirming the D7 reference map matches the
source); `src/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness the
U07 round-trip / fail-closed tests use — U05's apply path is what they
exercise).

**Deliverables (≤ 4 files).**
`src/Kumunita.Core/Portability/ValidateAsync.cs` (new or a method — the
validate phase: the `format` check (C-M11·1) + the per-type deserialize
sanity + the data-driven referential-integrity loop over the registry's
`ReferenceFields` + the media-manifest byte verification (C-M11·3) — returns
a `ValidationResult` (ok / the closed failure set); **zero writes**);
`src/Kumunita.Core/Portability/ApplyDocumentsAsync.cs` (new or a method — the
apply phase: store the domain documents in the registry's **import order**
(the D7 order, the one commit) — the uniform loop, **not** per-type code);
`src/Kumunita.Core/Portability/ApplyMediaAsync.cs` (new or a method — the
media byte copy into the volume at the `{Id[0..2]}/{Id}` layout (the
C-M11·3 pin), the `MediaObject` catalog doc stored);
`src/Kumunita.Core/Portability/PortabilityService.cs` (the `ValidateAsync` +
the apply methods composed — the `ImportAsync` shape: `ValidateAsync` first
(a failure ⇒ return the closed failure set, **no apply**), else the apply
documents + apply media — the identity re-creation + the config apply are
U06's, the seam is reserved).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green. A
`ValidateAsync` over a corrupted archive (wrong `format` / a dangling
reference / a truncated media byte) returns the closed failure set + writes
**zero** rows (the U07 fail-closed test witnesses it); a `ValidateAsync` over
a valid archive + the apply stores the documents in order + the media bytes
(the U07 round-trip test witnesses it). Handoff entry: the exact
validate-check set as written (the U07 fail-closed test asserts exactly
those), the **exact** closed failure-set shape (the U06 web surface + the
U07 pin render it), the apply-order entry count (matches the registry), the
`ImportAsync` public signature (the U06 + the U07 target verbatim), the
"zero writes on validate failure" pin confirmed (the C-M11·4 source boundary),
any compile warnings.

---

## U06 — Import: the identity re-creation (secrets reset) + the config apply + the web surface + the `portability.import` audit + the kw-l keys

**Goal.** D3/D5 rendered on the import side: the import path completes — the
**identity re-creation** (from `identity/principals.json`: re-create each
principal keyed by the exported `subjectId`, **reset** the secrets — a fresh
non-portable password + a fresh security stamp — and re-apply the roles, the
C-M11·2 + D3 boundary on the import side); the **config apply** (from
`config.json`: the community name + the `LocaleSettings` + the
language-catalog state, per the D2 config field set); the **web surface**
completes — `POST /admin/portability/import` (applies the uploaded archive,
the `portability.import` audit row, the D5 surface) + the **kw-l keys** for
the import-side strings (the D10 list, × 4 languages). After this unit the
import produces a **complete, coherent** instance (the U07 round-trip test
witnesses it end-to-end).

**Entry reads (5).** `docs/design/m11-portability-design.md` (§principals —
the locked no-secret field set the re-creator consumes; §config — the locked
config field set; §surface — the D5 `import` route + action + the
one-audit-row shape; §kw-l — the locked key list);
`src/Kumunita.Core/Portability/PortabilityService.cs` + `PrincipalsExport.cs`
+ `ConfigExport.cs` (U02's principals/config extractors — the **exact** field
set U06's re-creator + applier mirror, verbatim); `src/Kumunita.Core/Identity/UserManager.cs`
(or the Identity `UserManager` setup) (the `UserManager.CreateAsync` + the
`AddToRoleAsync` + the **reset** shape — the `PasswordHasher` / the fresh
password + the security-stamp reset the C-M11·2 import boundary enforces);
`src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (U04's export
surface — the `import` action + the view form U06 completes, the D5 shape);
`src/Kumunita.Web/Views/Admin/Portability.cshtml` (U04's index view — the
import upload form U04 rendered, U06 wires the `POST` + the status render).

**Deliverables (≤ 5 files).**
`src/Kumunita.Core/Portability/ApplyIdentityAsync.cs` (new or a method — the
identity re-creation: for each `identity/principals.json` entry, the
`UserManager.CreateAsync` (keyed by the exported `subjectId`, the **reset**
secrets — a fresh non-portable password + a fresh security stamp — the
C-M11·2 import boundary) + the `AddToRoleAsync` for the exported roles; the
"principal already exists" case is the D1 restore semantic (a fresh instance
— a collision is a validate-phase failure, the C-M11·4 pin));
`src/Kumunita.Core/Portability/ApplyConfigAsync.cs` (new or a method — the
config apply: the community name + the `LocaleSettings` + the language-catalog
state, per the D2 config field set — the U02 `ConfigExport` mirror, verbatim);
`src/Kumunita.Core/Portability/PortabilityService.cs` (the `ImportAsync`
completes — the U05 `ValidateAsync` + the apply documents + apply media + the
U06 `ApplyIdentityAsync` + `ApplyConfigAsync`, one `ImportAsync` that
validate-fails-closed else applies the whole instance; the
`portability.import` audit row emission — the one `AccessAudit` row,
`Via = Admin`, verb `import`, the ADR 0105 shape);
`src/Kumunita.Web/Controllers/AdminPortabilityController.cs` (the `POST
Import` action completes — the uploaded file → `PortabilityService.ImportAsync`
→ the success / the closed-failure-set render, the one `portability.import`
audit row emitted by the service);
`src/Kumunita.Core/Localization/KnownTranslationKeys.cs` (the import-side
`portability.*` kw-l keys × 4 languages — the D10 list, the closed-key
registry + the parity; the U04 export-side keys are reused, not duplicated).

**Exit.** `run_build` on `Kumunita.Core` + `Kumunita.Web` green; the
`portability.*` parity test passes (the `KnownTranslationKeys_ParityTests`
enforces the 4-language set). A GlobalAdmin `POST /admin/portability/import`
over a valid archive produces a complete, coherent instance (the principals
re-created + the secrets reset + the roles re-applied + the config applied —
the U07 round-trip test witnesses it end-to-end); a failed import renders the
closed failure set + the instance is unchanged (the U07 fail-closed test
witnesses it). Handoff entry: the **exact** identity re-creation shape (the
`subjectId` keying + the secret reset + the role re-apply) as written, the
**exact** config-apply field set as written (matches U02's `ConfigExport`,
verbatim), the one-audit-row shape (`TargetKind` / `Via` / verb) as written,
the `POST Import` + the closed-failure-set render as written (the U07 pins
target verbatim), the **exact** import-side `portability.*` kw-l keys + their
4-language strings as written, any compile warnings.

---

## U07 — Close: the Core round-trip / no-secret / fail-closed tests + the Web-surface pins + the milestone flip

**Goal.** The acceptance gate (D9) + the milestone close (C-M11·8). The
**Core** tests (the `PostgresFixture` harness + a media temp-dir): (a)
**round-trip** — plant a representative instance (a group, a post + reply +
a translation, an event + RSVP, a board + lane + todo, a conversation +
message, a page + translation, a media object + its bytes, a delegation
grant, a guardian link, two principals with roles) → export → import into a
**fresh** instance → the content graph + the media + the role assignments
are preserved + referentially intact; (b) **no-secret** — the exported
archive contains **no** password hash / security stamp / Identity token (the
principals POCO field-shape + a byte-scan witness over the archive); (c)
**fail-closed** — a wrong-`format` manifest / a truncated media file / a
dangling reference is rejected **before any write** — the fresh instance has
**zero** rows after the failed import. The **Web** pins (NSubstitute, no
Postgres): the GlobalAdmin gate (a non-GlobalAdmin `GET /admin/portability`
403s), the one-audit-row per action (`portability.export` /
`portability.import`, `Via = Admin`), the `portability.*` kw-l keys resolve
(the parity). Then the **milestone flip** (C-M11·8): `Milestones.cs` /
`README.md` Roadmap / `docs/STATUS.md` / `docs/ARCHITECTURE.md` all move M11
→ `StatusDone` + M12 → `StatusNext`; the `MilestonesTests.cs` re-pin (the
single-in-progress moves M11 → M12); the unit-plan files
(`portability-u00.md` … `portability-u06.md`) → `done/`; the handoff
`## Summary`.

**Entry reads (6).** `docs/design/m11-portability-design.md` (§pinned tests —
the exact test names + the closed failure set; §Invariants — the C-M11 pins
the tests witness; §FACES — the F1–F5 the tests close);
`src/Kumunita.Core/Portability/PortabilityService.cs` (the `ExportAsync` +
`ImportAsync` + the validate/apply seams the tests target);
`src/Kumunita.Core.Tests/PostgresFixture.cs` (the test harness shape — the
`postgres:18` Testcontainers + the media temp-dir the round-trip / no-secret /
fail-closed tests need); `tests/Kumunita.Core.Tests/` (the Core test project
— the new test file's home, the `PostgresFixture` usage + the media
`IMediaFileStore` temp-dir wiring to mirror); `src/Kumunita.Web/Milestones.cs`
+ `README.md` (the Roadmap section) + `docs/STATUS.md` + `docs/ARCHITECTURE.md`
(the value-chain table — the four parity surfaces the flip touches);
`tests/Kumunita.Web.Tests/MilestonesTests.cs` (the single-in-progress pin the
re-pin moves M11 → M12).

**Deliverables (≤ 6 files).**
`tests/Kumunita.Core.Tests/PortabilityRoundTripTests.cs` (new — the three
acceptance tests: the round-trip, the no-secret, the fail-closed — the D9
names, the `PostgresFixture` + the media temp-dir);
`tests/Kumunita.Web.Tests/AdminPortabilityControllerTests.cs` (new — the
Web-surface pins: the GlobalAdmin gate, the one-audit-row per action, the
kw-l keys resolve — the NSubstitute no-Postgres shape);
`src/Kumunita.Web/Milestones.cs` (M11 → `StatusDone`, M12 → `StatusNext` —
the C-M11·8 flip); `README.md` (the Roadmap section: M11 → **Done** (ADR
0108), M12 the single in-progress — the parity); `docs/STATUS.md` +
`docs/ARCHITECTURE.md` (the M11 row → done + the value-chain table — the
parity); `tests/Kumunita.Web.Tests/MilestonesTests.cs` (the re-pin — the
single-in-progress moves M11 → M12).

**Exit.** `dotnet build Kumunita.slnx -c Debug` green; the Core tests
(round-trip + no-secret + fail-closed) pass (the `dotnet exec
tests\Kumunita.Core.Tests\…dll` run — the ~20 s Testcontainers path, the
`docker container prune` cleanup if the process is killed); the Web pins
pass (the `dotnet exec tests\Kumunita.Web.Tests\…dll` run); the
`MilestonesTests` re-pin passes (the single-in-progress is M12); the four
parity surfaces agree (M11 done, M12 the single in-progress); the
`portability-u00.md` … `portability-u06.md` files are in `done/`; the handoff
`## Summary` is appended. **The milestone is closed.** M12 (iCal) is the
single in-progress milestone.

---

## Deferred lanes (named, own ADRs — the D10 / C-M11·5 hand-off)

- **Per-slice / cross-instance portability** — one group's posts out, or
  neighborhood A → B (ARCHITECTURE.md line 784 "cross-neighborhood migration:
  versioned JSON export/import service (not built yet)" + the README
  §Deferred "Cross-neighborhood federation — a standalone OpenIddict IdP;
  global identity, local authorization"). M11 owns the *whole* instance; the
  fragments are the follow-on.
- **Import-merge** — applying an archive on top of a *diverged* existing
  instance (the reconciliation problem: duplicate posts, conflicting roles,
  diverged translations). M11's semantic is **restore** (a fresh instance);
  the merge is the harder lane, own ADR.
- **The backup-automation / cron surface** — the operator-run script / the
  scheduled export (OPS.md). M11 keeps the "operator in the app" house shape;
  the automation is the OPS.md lane.
- **The other world-seam exports** — iCal (M12 stays iCal), the other
  calendar / contacts exports. M11 is the *data* portability; the *format*
  world-seams are their own milestones.
