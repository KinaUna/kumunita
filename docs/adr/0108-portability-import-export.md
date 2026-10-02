# ADR 0108 — M11: Portability (import/export) — whole-instance export + a fail-closed restore of a versioned `*.kumunita` archive (a new `Kumunita.Core/Portability/` operator context — the closed content inventory read from the `*DocTypes` surfaces, the no-secret identity graph (never the credential material), the content-addressed media, the instance identity + localizations via `config.json`; validate-then-apply fail-closed atomic import; the GlobalAdmin admin plane with one `AccessAudit` row per action; zero new authorization surface)

Status: Accepted
Date: 2026-09-27
Amends: **0004** (§B.1 — M11 is a **reader/writer over the frozen
documents**: it adds **no** document, **no** index, **no** migration; the
`Portability` POCOs are not Marten documents and create no `mt` tables),
**0006** (the frozen `IAuthorizationService` / `IAuditableResource`
surface — **no new adapter**, no new `AccessAction`, no new `AccessVia`,
no `Decide()` branch, no `Audience` — portability is an **operator action
over the instance** decided by the GlobalAdmin role gate, the ADR 0105
operator-plane shape applied to an operator lane),
**0011** (the content-addressed media — the archive reuses the
`{root}/{Id[0..2]}/{Id}` layout verbatim, so import is a byte-copy and the
dedup-by-content-hash is preserved; the `MediaObject` catalog doc + its
bytes travel as a pair),
**0005** (the `LocaleSettings` + `LanguageCatalog` state — the
`config.json` snapshot source; the `LanguageCode` on every `*Translation`
row resolves against the `config.json` catalog),
**0028** (the GU "no standing to carry a secret" precedent — the C-M11·2
no-secret boundary applied to the operator's own archive: the archive
travels the identity **graph** never the credential material, and on
import the principals are re-created with their secrets **reset**),
**0034** (the `Content-Disposition: attachment` streaming shape the
`export` action reuses),
**0015** (the closed `KnownTranslationKeys` registry — the `portability.*`
keys join it, all four languages, the
`KnownTranslationKeys_ParityTests` invariant holds),
**0101 / 0105** (the GlobalAdmin admin-surface house pattern —
`[Authorize(Roles = GlobalAdmin)]` + one `AccessAudit` row `Via = Admin`,
the `AdminMessagingController` shape to mirror; the
`messaging.toggle` one-audit-row shape the `export` / `import` rows reuse).

## Context

M1–M10 and the named lanes grew a full resident surface — identity,
directory, posts, events + calendar, projects + boards, pages, search,
notifications, messaging, PWA. But a self-hosted single-neighborhood
platform lives or dies on one world-seam the roadmap row names
(ARCHITECTURE.md value chain: "the loop closes **into** the residents'
lives"): **the neighborhood's data must be able to leave the platform and
come back.**

Today it cannot. There is **no import path at all** — a fresh instance is
a blank instance. And the only "export" is the `pg_dump` an operator runs
by hand (OPS.md), which carries the Postgres schema *and* the credential
hashes *and* the media volume as **three separate artifacts** that do not
travel together and do not verify against each other. `pg_dump` is a
*schema + state* snapshot, not a portable *content* artifact: it carries
credential material the platform's privacy posture forbids from being in
an operator's hands, and it is not self-describing (no format version, no
manifest, no media verification).

M11 closes that seam in the two directions the row names (import /
export):

1. **Export** — a GlobalAdmin produces **one** portable, **versioned**
   archive (`*.kumunita`) holding the whole instance's *content* (every
   registered content domain document), the *media bytes* (content-
   addressed, dedup preserved), the *identity graph* (who-is-who +
   delegation + guardian + membership + moderator — **as data**, without
   the credential material), and the *instance identity + localizations*
   (the community name, the language catalog, the locale settings).
2. **Import (restore)** — a GlobalAdmin on a **fresh** (or current)
   instance ingests that archive: it **validates the whole archive first**
   (format version, per-document-type schema sanity, **referential
   integrity**, the media bytes), and only then **applies it in one
   commit**. A rejected archive writes **nothing**.

M11 is **greenfield and operator-scoped** (grep-confirmed: there is no
`Portability` context, no export/import code, no `manifest.json` shape,
no archive reader/writer anywhere in the tree). It builds entirely on
frozen, verified seams: the Marten domain documents across the registered
`*DocTypes` surfaces (the **exact** closed content inventory + import
order + reference map is locked from source in the design doc
§inventory), the ASP.NET Identity tables (the **principals** — *not* the
domain docs; the credential material lives here and **never travels**),
the content-addressed media volume (ADR 0011), the GlobalAdmin
admin-surface house pattern (ADR 0101/0105/0106), the BCL
`System.IO.Compression.ZipArchive` (in the .NET 10 framework — **no new
package**), and the `CommunityOptions` + `LocaleSettings` +
`LanguageCatalog` state (the `config.json` snapshot source).

This is a **milestone** (a roadmap letter, not a named lane): the close
unit (U07) flips `Milestones.cs` / the README Roadmap / `docs/STATUS.md` /
`docs/ARCHITECTURE.md` + `MilestonesTests.cs` (the AGENTS.md doc↔code
parity contract, C-M11·8). The design doc
`docs/design/m11-portability-design.md` (authored U00, **LOCKED**) is the
primary tier; the register
`docs/plans-milestones/done/m11/plan-m11-portability.md` is the secondary tier.

## Decision

**D1 — Scope = whole-instance export + import (restore).** M11 exports and
imports the **entire instance**: every registered content domain document
type, the media bytes, the identity graph (principals + the
delegation/guardian/membership/moderator domain docs), and the instance
identity + localizations. The **import semantic is restore** (apply into a
fresh instance; the current instance's state is the operator's pre-import
backup, the rollback path). **Out of scope → deferred lanes (own ADRs):**
per-slice export (one group's posts), cross-instance portability (A → B),
import-merge (onto a diverged instance), and the backup-automation / cron
surface. *Forbids:* shipping per-group migration now — the repo is
explicitly single-neighborhood with no multi-tenant model, and
cross-neighborhood federation is a named deferral (README §Deferred); M11
owns the *whole* instance the value-chain row names, not the fragments.

**D2 — The archive is a versioned ZIP, BCL-only (no new dependency).**
`System.IO.Compression.ZipArchive` (in the .NET 10 framework — the
lean-stack + tsc-only discipline holds; **no** new package). Layout:
`manifest.json` (self-describing, the C-M11·1 shape) + `docs/{DocType}.json`
(one JSON-array file per registered content domain document type — the
per-type isolation that keeps the round-trip testable) +
`media/{Id[0..2]}/{Id}` (the content-addressed bytes, the **same** layout
as the local volume, so import is a byte-copy and dedup-by-content-hash is
preserved) + `identity/principals.json` (the C-M11·2 no-secret graph) +
`config.json` (the instance identity + localizations). `*.kumunita` is the
file extension (a content marker, not a format claim — the `format` field
in `manifest.json` is the authority). *Forbids:* a new package, a format
that the `manifest.json` `format` version does not own, or a media layout
different from the local volume.

**D3 — The identity travels as the graph, never the secrets.** The archive
carries the **principals** (`subjectId`, `username`, `email`,
`normalizedEmail`, `displayName`, `roles`, and the lockout/state flags —
the data needed to re-create a signable principal) **and** the
delegation / guardian / membership / moderator **domain documents** (those
live in the M1 domain context, not the Identity schema, so they travel
with the content). It **never** carries credential material: no password
hash, no security stamp, no access-token / refresh-token / recovery-code,
no Identity sign-in record. On import the principals are **re-created**
(keyed by the exported `subjectId`) and their secrets are **reset** (a
fresh, non-portable password; the resident re-authenticates). This is the
privacy pin (C-M11·2) and the ADR 0028 GU precedent ("no standing to carry
a secret") applied to the operator's own archive. **The no-secret boundary
is the single most load-bearing M11 invariant.** *Forbids:* a principals
projection that reads a credential column into the archive, a principals
POCO that has a hash/stamp/token field, or an import that preserves a
secret rather than resetting it.

**D4 — Import is validate-then-apply, fail-closed, atomic.** The
**validate phase** runs to completion **before any write**: (a) the
`manifest.json` `format` is one the build understands (else reject — the
C-M11·1 pin); (b) each `docs/{Type}.json` deserializes into its declared
POCO set; (c) **referential integrity** — every id referenced by the
inventory's reference map resolves to a row **within the archive** (the
closed reference map is pinned in the design doc §inventory); (d) the
media manifest's bytes are all present and their content matches their
path. **Any failure ⇒ zero writes.** The **apply phase** is one commit:
re-create the Identity principals (so `subjectId` sign-in works) → store
the domain documents in the pinned dependency order → copy the media bytes
→ apply the config. On a mid-apply failure the instance's state is the
**documented restore path** (the operator's pre-import backup / a fresh
instance + re-import; OPS.md) — M11 never silently accepts a half-import.
*Forbids:* a "best-effort partial import" — it is untestable and violates
the privacy/consistency posture (a half-imported instance is exactly the
"parts-work-seams-don't" failure the philosophy names).

**D5 — The surface is the admin web plane (GlobalAdmin), audited, quiet.**
`GET /admin/portability` (the index: the export button + the import upload
form + a status area) + `GET /admin/portability/export` (streams the
`*.kumunita` archive) + `POST /admin/portability/import` (applies the
uploaded archive). All three are `[Authorize(Roles = GlobalAdmin)]`;
**export** and **import** each emit exactly **one** `AccessAudit` row
(`TargetKind "portability"`, `Via = Admin`, verb `"export"` / `"import"`),
emitted by the service, the controller adds none (the ADR 0105
`messaging.toggle` shape). A plain resident — including a moderator —
never sees the surface (the ADR 0101 / 0105 admin-surface shape, no
break-glass read). *Forbids:* a CLI / backup-automation surface now (that
is an OPS.md follow-on lane, D1's deferred list); a second audit row per
action; an audit row on the index read; a non-GlobalAdmin reaching the
surface.

**D6 — The media travels content-addressed, with the catalog doc.** The
bytes are copied **verbatim** into the archive at the **same**
`{Id[0..2]}/{Id}` layout the local volume uses (ADR 0011 C-MED·3/4/7), so
import is a byte-copy and the content-hash dedup is preserved (re-importing
the same file is a no-op at the byte layer). The `MediaObject` catalog doc
travels as a `docs/MediaObject.json` row (the surviving catalog reference a
`pg_dump` carries, per its own doc-comment). A `MediaObject` listed in the
manifest whose bytes are missing / mismatched is a **validate-phase
failure** (fail-closed, C-M11·3) — the archive never imports a doc pointing
at a phantom byte. *Forbids:* a media layout different from the local
volume, a doc whose bytes are absent (the phantom byte), or a byte copy
that re-encodes rather than reuses the content-addressed path.

**D7 — The closed document inventory + import order is pinned in the design
doc from source.** U00 reads **every** `*DocTypes` surface
(`M1DocTypes` / `M3DocTypes` / `M4DocTypes` / `M5DocTypes` / `M6DocTypes` /
`M9DocTypes` / `MediaDocTypes` / `TagDocTypes` / `PageDocTypes`) and locks
the **exact** closed inventory — each entry: the document `Type`, its
archive file (`docs/{Type}.json`), its **import order** (parents before
children), and its **reference fields** (which of its fields point at
another entry's id) — that U01's registry implements as **data**. The
inventory is data the U02/U05 loops iterate generically, **not** per-type
code — that is what keeps the export/import units atomic despite ~40
document types. **Refined in favor of the source** (the design doc
drift-guard entries 1–4): the closed inventory is the **content +
identity-graph + localization** set — **44 content docs** — and it
**excludes** the five credential/operational documents (`IdentityToken` —
a high-entropy secret; `OutboxEmail` / `EmailDeadLetter` / `AccessAudit` /
`AuditPurgeSummary` — operational state) by name, and carries the
`LocaleSettings` + `LanguageCatalog` + `CommunityOptions` via
`config.json` (D2/D7 resolved) rather than as `docs/` rows. *Forbids:* a
per-type export/import code path (the loop must iterate the registry data),
an inventory that omits a `*DocTypes` content doc, or an inventory that
includes a credential/operational doc.

**D8 — One new `Portability` context in Core — an operator lane, not a
content-decision lane.** `Kumunita.Core/Portability/` holds the manifest
model, the document-inventory registry, the archive (de)serializer, and
the `PortabilityService` (export / validate / apply). The service composes
`IDocumentStore` (Marten, for the domain docs), the Identity `UserManager`
/ `RoleManager` (for the principals re-creation + roles), and
`IMediaStore` / `IMediaFileStore` (for the bytes). It is registered in
`DependencyInjection.cs` like its siblings. **No** new `AccessAction`,
**no** new `AccessVia`, **no** `IAuthorizationService` branch, **no**
`Audience` — portability is an operator action over the instance, decided
by the GlobalAdmin role gate (C-M11·7). *Forbids:* a new `AccessAction` /
`AccessVia` / `Decide()` branch / `Audience` (C-M11·7), a `Portability`
context that touches the resident decision engine, or a service seam that
is not the GlobalAdmin role gate.

**D9 — Tests: a Core Postgres round-trip + the no-secret pin + the
fail-closed pin + the Web surface pins.** The **three acceptance tests**
(the closed-loop / handoff / part-vs-whole shape, per the design-doc
template): (a) **round-trip** — plant a representative instance (a group,
a post + reply + a translation, an event + RSVP, a board + lane + todo, a
conversation + message, a page + a translation, a media object + its bytes,
a delegation grant, a guardian link, a couple of principals with roles) →
**export** → import into a **fresh** instance → the content graph, the
media, and the role assignments are preserved and referentially intact
(the "handoff" — the data moves between two instances); (b) **no-secret** —
the exported archive contains **no** password hash / security stamp /
Identity token (the principals POCO has no such field + a byte-scan witness
over the archive) (the "closed-loop" on the trust boundary); (c)
**fail-closed** — a wrong-`format` manifest / a truncated media file / a
dangling reference is rejected **before any write** — the fresh instance
has **zero** rows after the failed import (the "part-vs-whole" — the whole
import is all-or-nothing, not the parts). Plus Web-surface pins (the
GlobalAdmin gate, the one-audit-row per action, the kw-l keys resolve).
*Forbids:* a round-trip that only checks a single doc type, a no-secret
test that is field-shape only (the byte-scan witness is the closed loop),
or a fail-closed test that asserts the failure *after* a partial write.

**D10 — kw-l keys + the deferred lanes.** The operator-facing strings
(`portability.index.title` / `portability.export` / `portability.import` /
`portability.confirm.import` / `portability.status.*`) × 4 languages (the
closed-key registry + `KnownTranslationKeys_ParityTests` enforces).
**Deferred lanes** (named in ADR 0108 Consequences, own ADRs): per-slice /
cross-instance portability (ARCHITECTURE.md line 784 + the
cross-neighborhood federation deferral), import-merge, the
backup-automation / cron surface (OPS.md), and the other world-seam exports
(M12 stays iCal). *Forbids:* a kw-l key outside the closed registry, a
deferred lane shipped inside M11, or a language parity gap (the
`KnownTranslationKeys_ParityTests` invariant holds).

**The invariants to lock (C-M11·1–8):** the archive is versioned +
self-describing (C-M11·1); the archive travels the data, never the secrets
(C-M11·2 — the single most load-bearing M11 invariant); the media travels
content-addressed + whole (C-M11·3); import is fail-closed + atomic
(C-M11·4); whole-instance, not per-slice (C-M11·5); the surface is
GlobalAdmin + audited + quiet (C-M11·6); zero new authorization surface
(C-M11·7); docs parity holds at the flip (C-M11·8). The **FACES (F1–F5)**
are locked in the design doc §FACES: the neighborhood's life is portable
out (F1); the archive comes back intact (F2); the secrets never travel
(F3); a corrupt archive is refused wholesale (F4); the operator surface is
quiet + audited (F5).

## Consequences

**Positive:** the neighborhood's data can leave and come back — the
world-seam the roadmap row names, in the strongest form (the *whole*
instance, not a fragment). The archive is **structurally** secret-free
(C-M11·2), so a stolen or mishandled file is data without the credentials
— strictly stronger than `pg_dump` (which carries the hashes). The
fail-closed restore (C-M11·4) means a corrupt or inconsistent archive is
refused wholesale — the "parts-work-seams-don't" failure is
structurally impossible on the import path. The surface is quiet (one
audit row per action) and invisible to a plain resident (C-M11·6). The
BCL-only archive (D2) holds the lean-stack + tsc-only discipline (no new
package).

**Negative / cost:** M11 deliberately adds **no** resident facing, **no**
engagement surface, **no** feed — it is a quiet operator lane, and that is
the point (C-M11·6/7). The cost is the operator's only, and it is one
click. The **operator is a GlobalAdmin resident** (the house pattern) —
there is no non-resident operator model, and the backup-automation / cron
surface is a follow-on lane (D10), so an operator who wants a scheduled
export must run the app surface today.

**The deferred lanes (each a named follow-on, own ADR):**

- **Per-slice / cross-instance portability** — one group's posts out, or
  neighborhood A → B (ARCHITECTURE.md line 784 "cross-neighborhood
  migration: versioned JSON export/import service (not built yet)" + the
  README §Deferred "Cross-neighborhood federation — a standalone
  OpenIddict IdP; global identity, local authorization"). M11 owns the
  *whole* instance; the fragments are the follow-on.
- **Import-merge** — applying an archive on top of a *diverged* existing
  instance (the reconciliation problem: duplicate posts, conflicting
  roles, diverged translations). M11's semantic is **restore** (a fresh
  instance); the merge is the harder lane, own ADR.
- **The backup-automation / cron surface** — the operator-run script / the
  scheduled export (OPS.md). M11 keeps the "operator in the app" house
  shape; the automation is the OPS.md lane.
- **The other world-seam exports** — iCal (M12 stays iCal), the other
  calendar / contacts exports. M11 is the *data* portability; the *format*
  world-seams are their own milestones.

**Supersedes:** the ARCHITECTURE.md value-chain row
("outcome + world seams — the loop closes into the residents' lives") +
line 784 deferral ("Cross-neighborhood migration: versioned JSON
export/import service (not built yet)") + the README §Deferred
"Cross-neighborhood federation" deferral — these are the *source* of M11's
scope (whole-instance now, per-slice / cross-instance deferred). ADR 0108
**supersedes** the deferral by owning the whole instance and re-naming the
follow-on lanes (per-slice / cross-instance, import-merge,
backup-automation) in this ADR's Consequences (the kickoff handoff note's
"Not touched" record — those three are *source*, not *target*, of this
decision).
