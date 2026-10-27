# ADR 0193 — Portability import modes: a "keep existing" choice

Status: Accepted
Date: 2026-10-27
Extends the **M11 portability surface** (ADR 0108, the D1 "restore onto a
fresh instance" semantic + the C-M11·2 no-secret import boundary + the
C-M11·4 fail-closed validate gate + the §validate/§apply phase order, the
§principals eight-field closed set, the §inventory 44-content-doc closed
registry + D7 import order, the C-M11·3 content-addressed media, and the
one-audit-row `portability.import` shape) and the **M27 user-scoped
portability precedent** (ADR 0148, the D4 business-key duplicate detector +
the classify→resolve import shape — the "keep vs. add" decision the
resident surface already makes per-entity, re-applied here at the operator
surface as a single whole-instance mode). This ADR gives the
**GlobalAdmin-gated `/admin/portability` import** a two-way mode choice:
**unchecked (the default) = clean-slate replace** (the D1 restore semantic —
the instance's version of the archive's surface is wiped, then the archive is
applied), and **checked = merge** (the instance's existing accounts + content
are kept intact; only new, non-existent accounts + content are added). The
default is unchanged from M11 (a restore); the new capability is the
**opt-in merge** that resolves the failure the M11 D1 semantic produced when
an import was attempted over an instance that already held the archive's
principals (the `Username/Email already taken` 500 that shipped the bug report
this lane closes).

## Context

M11 (ADR 0108) locked the import semantic as **D1 restore onto a fresh
instance**: the `PortabilityApplyIdentity.ApplyAsync` re-creator runs
`UserManager.CreateAsync` keyed by the exported `subjectId`, and a
"principal already exists" collision was a **validate-phase failure** (C-M11·4)
— a fresh instance has no rows, so `CreateAsync` ran against a clean identity
store. The assumption is explicit and load-bearing: **the import target is a
fresh instance**.

That assumption broke when a real operator imported a `*.kumunita` archive
into an instance that **already had accounts and content** (a freshly
deployed demo site with `SampleData__Enabled=true`, or an instance a previous
import had already populated): `PortabilityApplyIdentity.ApplyAsync` hit
`CreateAsync` on a principal whose `Email` / `UserName` were already taken,
and the re-creator threw an `InvalidOperationException`
(`Failed to re-create principal …: Username … is already taken, Email … is
already taken`) — an **unhandled 500** that left the instance in an unknown
state and gave the operator no path to proceed.

The operator's need was explicit: **"Can we change it so the admin can select
to keep existing content, so existing accounts are not changed and
accounts/content is only added if it doesn't exist already? If not selected a
clean slate import should be done."**

The constraint that keeps both modes safe is the one M11 already enforces
(C-M11·4, the fail-closed pin): **the validate phase runs to completion
before any write, and a mid-apply failure is the documented restore path,
never a silently-accepted half-import.** The ADR 0193 lane re-applies that
discipline to the two new modes. M27 (ADR 0148) already solved the *per-entity*
version of this exact decision at the resident surface (the D4 business-key
duplicate detector classifies each entity as `duplicate` vs. `new` and the
operator resolves them); this lane generalizes that same keep-vs-add decision
to the **whole instance** at the operator surface, as a single mode choice
rather than per-entity.

The Web-layer surface already exists (the `AdminPortabilityController` import
action + the `Views/AdminPortability/Index.cshtml` upload form, the ADR 0108
D5 surface); the ADR 0193 lane adds **one `bool keepExisting` form field** +
the mode dispatch, and **two new `portability.import.keep_existing*`
translation keys** (the ADR 0015 / ADR 0052 × en/de/fr/da parity pins,
re-applied). No new `AccessAction`, no new `AccessVia`, no new `Decide()`
branch, no new adapter, no new bounded context, no new document — the
`portability.import` audit row (the ADR 0108 one-audit-row shape) is the same
row in both modes.

## Decision

**D1 — A `PortabilityImportMode` enum + a mode-carrying `ImportAsync` overload.**
A new `Kumunita.Core.Portability.PortabilityImportMode` enum with two values:
`CleanSlate` (the default — the D1 restore semantic, replace) and
`KeepExisting` (the merge semantic, keep + add). The `IPortabilityService`
gains one overload,
`ImportAsync(string actorId, Stream archive, PortabilityImportMode mode,
CancellationToken ct = default)`; the original
`ImportAsync(string actorId, Stream archive, CancellationToken ct = default)`
is **unchanged** and delegates to the new overload with `CleanSlate` (source
+ test compatibility — the existing `PortabilityRoundTripTests` D9a/D9c call
sites keep their signature verbatim). The Web `AdminPortabilityController`
import action gains a `bool keepExisting = false` parameter and maps
`keepExisting ? KeepExisting : CleanSlate` onto the mode. The audit row is
emitted by the **service** (the controller adds none — the ADR 0108 one-audit-row
shape, re-applied); both modes emit exactly one `portability.import` row
(`TargetKind "portability"`, `Via = Admin`).

**D2 — CleanSlate (the default): wipe-then-apply.**
When `mode == CleanSlate`, the service runs a **`WipeForCleanSlateImportAsync`**
step **before** the apply phase (after the validate phase, so a rejected
archive still writes zero rows — the C-M11·4 pin holds). The wipe removes the
instance's version of the archive's surface, in the pinned order:

1. **The Identity principals** (the EF Core `identity` schema): the
   user-dependent tables (`AspNetUserRoles`, `AspNetUserClaims`,
   `AspNetUserLogins`, `AspNetUserTokens`) are deleted for the affected
   principal ids **first, in the dependent-order**, then `AspNetUsers` itself.
   This is deliberate and **not left to EF cascade** —
   `ExecuteDeleteAsync` does not reliably cascade the dependent rows, and a
   surviving `AspNetUserRoles` row for a re-created `subjectId` would collide
   with the apply's `AddToRoleAsync` (a duplicate `(userId, roleId)` — the
   exact class of 500 this lane exists to remove). The `AspNetRoles` /
   `AspNetRoleClaims` standing definitions are left in place (the
   re-creator's `roleManager.FindByNameAsync` / `CreateAsync` ensures
   existence; the archive's roles re-attach onto the fresh principals).
2. **The media bytes** on the volume: for every `MediaObject` in the
   archive's `media_manifest`, `IMediaFileStore.DeleteIfExistsAsync`
   (idempotent — a missing file is a no-op).
3. **The Marten content docs**: every document type in the §inventory's 44
   closed set (the same `PortabilityDocTypes` registry the apply loop drives
   off, via a generic `PortabilityApplyDocuments.DeleteAllAsync<T>` helper —
   the data-driven "not per-type code" pin, re-applied). This wipes the
   `MediaObject` catalog rows, the `Profile` rows (keyed by the now-deleted
   principal's `subjectId`), the config rows (`LocaleSettings` /
   `LanguageCatalog`), and the full content graph.

The **operational docs** (`AccessAudit` / `OutboxEmail` / `EmailDeadLetter` /
`IdentityToken`) are deliberately **not** wiped — they are the instance's own
operational state (the §inventory "Excluded" set), not resident content the
archive carries. The audit trail of the import itself (the
`portability.import` row) is emitted **after** the apply, so it survives. The
operator's pre-import backup remains the rollback path (the ADR 0108 D1
semantic, unchanged).

**D3 — KeepExisting (checked): skip-existing, add-new.**
When `mode == KeepExisting`, **no wipe runs**. The apply phase becomes
additive:

- **Principals** (`PortabilityApplyIdentity.ApplyAsync`): an existing
  principal (matched by `NormalizedEmail` → `UserId`, else `subjectId`) is
  **skipped entirely** — no password reset, no security-stamp rotation. The
  archive's role standing is applied **additively only** (a role the account
  does not already have is added; an existing role is untouched; a role the
  account already holds is never removed — the "only added if it doesn't exist"
  semantic). A non-existent principal is created (the M11 re-creator path,
  unchanged — fresh non-portable password + fresh security stamp, the C-M11·2
  boundary).
- **Content docs** (`PortabilityApplyDocuments.ApplyAsync`): a row whose id
  already exists in the target store is **skipped** (the existing content is
  kept intact, never overwritten); only new rows are stored. The existing-id
  set is read per doc type via the generic `PortabilityApplyDocuments.
  QueryAllAsync<T>` helper (the same data-driven id-property convention the
  §validate (b) integrity loop uses: `Id`, except `Profile`'s `SubjectId`).
- **Media** (`PortabilityApplyMedia.ApplyAsync`): unchanged — the C-MED·4
  dedup makes it idempotent in both modes (identical bytes → same id → no
  second volume file).
- **Config** (`PortabilityApplyConfig.ApplyAsync`): the `LocaleSettings`
  singleton + the `LanguageCatalog` rows are **store-if-absent** — an existing
  row is kept intact; only a new language code is added. (In CleanSlate the
  same store-if-absent shape re-materializes the rows from the archive, since
  the wipe removed them — the single code path serves both modes.)

**D4 — The Web surface: one checkbox, two new keys.**
The `Views/AdminPortability/Index.cshtml` import form gains a **checkbox**
(`name="keepExisting"`, `value="true"`) + a label + a hint. Unchecked (the
form default) posts nothing for the field → the controller's `bool
keepExisting = false` default → `CleanSlate` (replace). Checked posts
`keepExisting=true` → `KeepExisting` (merge). The two new kw-l keys
(`portability.import.keep_existing` +
`portability.import.keep_existing_hint`, × en/de/fr/da) go through the closed
`KnownTranslationKeys` registry (the ADR 0015 / ADR 0052 parity pins,
re-applied) + the `AdminPortabilityControllerTests` kw-l parity pin. The
form's existing `data-confirm` destructive-action guard (the `confirm.js`
idiom) is unchanged — the operator still confirms before a replace.

**D5 — The audit row is mode-agnostic.**
Both modes emit exactly one `portability.import` `AccessAudit` row
(`Action "portability.import"`, `TargetKind "portability"`,
`TargetId "portability"`, `Via = AccessVia.Admin`, `Outcome Allow`) — the
ADR 0108 one-audit-row shape. The mode is **not** recorded in the audit row
(the row's `Action` is the verb, not the mode; distinguishing the modes in
the audit trail is a future-lane decision, not a requirement of this lane —
the rollback path is the operator's pre-import backup in both modes).

## Consequences

- A GlobalAdmin importing a `*.kumunita` archive over an instance that
  already has accounts/content has a **working path**: unchecked (the
  default) = clean-slate replace (the D1 restore semantic, now succeeding
  where it 500'd — the wipe removes the collision first); checked = merge
  (the existing accounts + content are kept intact, only new ones added).
- The **default is unchanged** from M11 (a restore / clean-slate import) —
  an existing deployment that imports into a fresh instance behaves
  identically (the wipe is a no-op on a fresh store; the D9a round-trip test
  witnesses this — it still passes with the `CleanSlate` default).
- The **C-M11·4 fail-closed pin holds in both modes**: the validate phase
  runs to completion before any write, a rejected archive writes zero rows,
  and a mid-apply failure is the documented restore path, never a
  silently-accepted half-import.
- The **C-M11·2 no-secret boundary holds in both modes**: the archive never
  carries credential material; a CleanSlate import resets the re-created
  principals' secrets (fresh non-portable password + fresh security stamp); a
  KeepExisting import never touches an existing principal's credentials.
- **Zero new `AccessAction` / `AccessVia` / `Decide()` branch / adapter**
  (the ADR 0108 C-M11·7 "no new authorization surface" pin, re-applied) and
  **zero migrations** (the two `portability.import.keep_existing*` keys are
  rows in the `TranslationResource` catalog — the ADR 0004 §B.1
  delta-detected convention).
- The **operator's pre-import backup** remains the single rollback path in
  both modes (the ADR 0108 D1 semantic) — the CleanSlate wipe is the
  documented destructive step, the merge is the documented additive step,
  and neither silently accepts a half-import.
- The M27 (ADR 0148) resident surface is **unchanged** — it already has its
  own per-entity classify→resolve shape (the D4 business-key detector); the
  ADR 0193 mode is the operator-surface generalization of the same
  keep-vs-add decision, not a change to the resident lane.
