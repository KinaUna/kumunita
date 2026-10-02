# M11 U00 — Lock the design: `m11-portability-design.md` + ADR 0108

**Unit plan (secondary tier, self-contained).** Read **this file + your
entry reads** and you can execute. The register is
`docs/plans-milestones/done/m11/plan-m11-portability.md`; the scratch handoff
note is `docs/plans-milestones/done/m11/m11-portability-handoff-notes.md`; the
atomicity contract and unit-series rule are in the register header.

## Goal

Author the **primary tier** and the decision record. This is the
**sign-off gate** for the whole milestone: lock D1–D10, the C-M11
invariants, F1–F5, the **closed document inventory + import order +
reference map** (D7 — the exact `Type` / file / order / reference-fields
per entry, **read from the `*DocTypes` surfaces**, not invented), the
**manifest format** (D2 — the exact `manifest.json` field set + the
`format` version string), the **no-secret principals shape** (D3 — the
exact `identity/principals.json` field set + the explicit **excluded**
credential fields), the **config shape** (the exact `config.json` field
set), the **fail-closed validate/apply contract** (D4 — the exact
validate checks + the apply order + the restore path), the **pinned test
names** (U07), the **kw-l key list** (U04/U06), and the **deferred-lane
list** (D10). ADR 0108 records: decisions + alternatives considered
(secrets travel; partial import; per-group now; a CLI surface now) + the
Consequences hand-off (the deferred lanes: per-slice / cross-instance,
import-merge, backup-automation, the iCal / other world-seam exports).

## Context (locked [PROPOSED] set — lock or refine, don't re-derive)

- **D1 · Scope = whole-instance export + import (restore).** M11 exports
  and imports the **entire instance**: every registered domain document
  type, the media bytes, the identity graph (principals + the
  delegation/guardian/membership/moderator domain docs), and the instance
  identity + localizations. The **import semantic is restore** (apply into
  a fresh instance; the current instance's state is the operator's
  pre-import backup, the rollback path). **Out of scope → deferred lanes
  (own ADRs):** per-slice export (one group's posts), cross-instance
  portability (A → B), import-merge (onto a diverged instance), and the
  backup-automation / cron surface. (Rejected: shipping per-group
  migration now — the repo is explicitly single-neighborhood with no
  multi-tenant model, and cross-neighborhood federation is a named
  deferral; M11 owns the *whole* instance the value-chain row names, not
  the fragments.)
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
- **D3 · The identity travels as the graph, never the secrets.** The
  archive carries the **principals** (`subjectId`, `username`, `email`,
  `normalizedEmail`, `displayName`, `roles`, and the lockout/state flags —
  the data needed to re-create a signable principal) **and** the
  delegation / guardian / membership / moderator **domain documents**
  (those live in the M1 domain context, not the Identity schema, so they
  travel with the content). It **never** carries credential material: no
  password hash, no security stamp, no access-token / refresh-token /
  recovery-code, no Identity sign-in record. On import the principals are
  **re-created** (keyed by the exported `subjectId`) and their secrets are
  **reset** (a fresh, non-portable password; the resident re-authenticates).
  This is the privacy pin (C-M11·2) and the ADR 0028 GU precedent ("no
  standing to carry a secret") applied to the operator's own archive.
  **The no-secret boundary is the single most load-bearing M11 invariant.**
- **D4 · Import is validate-then-apply, fail-closed, atomic.** The
  **validate phase** runs to completion **before any write**: (a) the
  `manifest.json` `format` is one the build understands (else reject — the
  C-M11·1 pin); (b) each `docs/{Type}.json` deserializes into its declared
  POCO set; (c) **referential integrity** — every id referenced by any
  document (an author's `subjectId`, a post's `ComponentId`, a reply's
  `PostId`, an RSVP's `EventId`, a message's `ConversationId`, a media
  `Id`, …) resolves to a row **within the archive** (the closed reference
  map is pinned in the design doc §import-order); (d) the media manifest's
  bytes are all present and their content-hash matches their path. **Any
  failure ⇒ zero writes.** The **apply phase** is one commit: re-create
  the Identity principals (so `subjectId` sign-in works) → store the domain
  documents in the pinned dependency order → copy the media bytes → apply
  the config. On a mid-apply failure the instance's state is the
  **documented restore path** (the operator's pre-import backup / a fresh
  instance + re-import; OPS.md) — M11 never silently accepts a half-import.
- **D5 · The surface is the admin web plane (GlobalAdmin), audited,
  quiet.** `GET /admin/portability` (the index: the export button + the
  import upload form + a status area) + `GET /admin/portability/export`
  (streams the `*.kumunita` archive) + `POST /admin/portability/import`
  (applies the uploaded archive). All three are
  `[Authorize(Roles = GlobalAdmin)]`; **export** and **import** each emit
  exactly **one** `AccessAudit` row (`TargetKind "portability"`, `Via =
  Admin`, verb `"export"` / `"import"`). A plain resident — including a
  moderator — never sees the surface (the ADR 0101 / 0105 admin-surface
  shape, no break-glass read). (Rejected: a CLI / backup-automation surface
  now — that is an OPS.md follow-on lane (D1's deferred list); M11 keeps
  the "operator in the app" house pattern.)
- **D6 · The media travels content-addressed, with the catalog doc.** The
  bytes are copied **verbatim** into the archive at the **same**
  `{Id[0..2]}/{Id}` layout the local volume uses (ADR 0011 C-MED·3/4/7), so
  import is a byte-copy and the content-hash dedup is preserved (re-importing
  the same file is a no-op at the byte layer). The `MediaObject` catalog doc
  travels as a `docs/MediaObject.json` row (the surviving catalog reference
  a `pg_dump` carries, per its own doc-comment). A `MediaObject` listed in
  the manifest whose bytes are missing / mismatched is a **validate-phase
  failure** (fail-closed, C-M11·3).
- **D7 · The closed document inventory + import order is pinned in the
  design doc from source.** U00 reads **every** `*DocTypes` surface
  (`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` / `M6DocTypes` /
  `M9DocTypes` / `MediaDocTypes` / `TagDocTypes` / `PageDocTypes`) and locks
  the **exact** closed inventory — each entry: the document `Type`, its
  archive file (`docs/{Type}.json`), its **import order** (parents before
  children), and its **reference fields** (which of its fields point at
  another entry's id) — that U01's registry implements as **data**. The
  inventory is data the U02/U05 loops iterate generically, **not**
  per-type code — that is what keeps the export/import units atomic despite
  ~40 document types.
- **D8 · One new `Portability` context in Core — an operator lane, not a
  content-decision lane.** `Kumunita.Core/Portability/` holds the manifest
  model, the document-inventory registry, the archive (de)serializer, and
  the `PortabilityService` (export / validate / apply). The service
  composes `IDocumentStore` (Marten, for the domain docs), the Identity
  `UserManager` / `RoleManager` (for the principals re-creation + roles),
  and `IMediaStore` / `IMediaFileStore` (for the bytes). It is registered
  in `DependencyInjection.cs` like its siblings. **No** new
  `AccessAction`, **no** new `AccessVia`, **no** `IAuthorizationService`
  branch, **no** `Audience` — portability is an operator action over the
  instance, decided by the GlobalAdmin role gate (C-M11·7).
- **D9 · Tests: a Core Postgres round-trip + the no-secret pin + the
  fail-closed pin + the Web surface pins.** The **three acceptance tests**
  (the closed-loop / handoff / part-vs-whole shape, per the design-doc
  template): (a) **round-trip** — plant a representative instance (a group,
  a post + reply + a translation, an event + RSVP, a board + lane + todo, a
  conversation + message, a page + translation, a media object + its bytes,
  a delegation grant, a guardian link, a couple of principals with roles) →
  **export** → import into a **fresh** instance → the content graph, the
  media, and the role assignments are preserved and referentially intact
  (the "handoff" — the data moves between two instances); (b) **no-secret**
  — the exported archive contains **no** password hash / security stamp /
  Identity token (the principals POCO has no such field + a byte-scan
  witness over the archive) (the "closed-loop" on the trust boundary);
  (c) **fail-closed** — a wrong-`format` manifest / a truncated media file /
  a dangling reference is rejected **before any write** — the fresh
  instance has **zero** rows after the failed import (the "part-vs-whole" —
  the whole import is all-or-nothing, not the parts). Plus Web-surface pins
  (the GlobalAdmin gate, the one-audit-row per action, the kw-l keys
  resolve).
- **D10 · kw-l keys + the deferred lanes.** The operator-facing strings
  (`portability.index.title` / `portability.export` / `portability.import` /
  `portability.confirm.import` / `portability.status.*`) × 4 languages
  (the closed-key registry + `KnownTranslationKeys_ParityTests` enforces).
  **Deferred lanes** (named in ADR 0108 Consequences, own ADRs): per-slice /
  cross-instance portability (ARCHITECTURE.md line 784 + the
  cross-neighborhood federation deferral), import-merge, the
  backup-automation / cron surface (OPS.md), and the other world-seam
  exports (M12 stays iCal).

**Invariants to lock (C-M11·1–8)** + **FACES (F1–F5):** as written in the
register — the design doc §Invariants / §FACES restate them and the test
pins below witness them.

## Entry reads (6)

1. `docs/philosophy/templates/design-doc.md` — the required section set
   (the house shape to follow).
2. `docs/design/m10-pwa-responsive-design.md` — the house style; a recent
   milestone's doc (its §Invariants / §FACES / §drift-guard shape).
3. `docs/adr/0107-pwa-and-responsive-design.md` + `docs/adr/0105-messaging.md`
   — the ADR shape to mirror (the operator-plane shape, the "a milestone"
   close, the deferred-lane language, and the ADR 0105 no-break-glass /
   floor precedent that D3/D4 build on).
4. `src/Kumunita.Core/M1DocTypes.cs` + `M3DocTypes.cs` + `M4DocTypes.cs` +
   `M5DocTypes.cs` — the D7 inventory source (the registered document types
   + their business-key / feed indexes reveal the referential
   relationships).
5. `src/Kumunita.Core/Media/IMediaFileStore.cs` + `IMediaStore.cs` +
   `MediaObject.cs` — the D6 media-travel shape (the content-addressed
   layout + the catalog doc).
6. `src/Kumunita.Core/Localization/LanguageCatalog.cs` — the D2 `config.json`
   shape (the `LocaleSettings` + language-catalog state that travels as
   instance identity).

## Deliverables (3)

- `docs/design/m11-portability-design.md` (new) — the primary tier.
  Sections: Context, D1–D10 (locked verbatim), §manifest (the D2 field set),
  §inventory (the D7 closed `Type` / file / order / reference-fields list),
  §principals (the D3 no-secret field set + the **excluded** credential
  fields), §config (the D2 config field set), §validate (the D4 validate
  checks + the apply order + the restore path), §surface (the D5 route +
  action set + the one-audit-row shape), Invariants C-M11·1–8, FACES F1–F5,
  §pinned tests (the D9 names), §kw-l (the D10 key list), §drift-guard (the
  frozen pins + the drift log), the three acceptance tests.
- `docs/adr/0108-portability-import-export.md` (new) — Accepted, the D1–D10
  decisions + the alternatives considered (secrets travel; partial import;
  per-group now; a CLI surface now) + the Consequences hand-off (the
  deferred lanes).
- `docs/adr/README.md` (one index row, after the 0107 row).

## Exit

`dotnet build Kumunita.slnx -c Debug` still green (docs only). Handoff
entry: decisions locked/vetoed, any D-item text changed, the **exact closed
document inventory + import order + reference map** as written (U01's
registry copies verbatim), the **exact manifest format** as written (U02's
manifest builder copies verbatim), the **exact no-secret principals shape**
as written (U02's extractor + U06's re-creator copy verbatim), the **exact
config shape** as written, the **exact validate checks + apply order** as
written (U05 copies verbatim), the **ADR number confirmed free** (the index
ran 0001–0107; `0108` is next — verified against `docs/adr/README.md`).
