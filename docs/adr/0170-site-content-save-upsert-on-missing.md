# ADR 0170 — Site content singleton save: upsert-on-missing

Status: Accepted
Date: 2026-10-22
Amends **ADR 0150** (D3 — the `SiteContent` write lane) and, by extension,
the **M35·2** divergence note in **ADR 0164** (the Kiosk
`SavePlaylistAsync` "upsert vs. no-op" register). This ADR retires the
`SiteContentService.SaveAsync` no-op-on-missing-row template in favor of an
upsert, so a save is **never a silent no-op** — the singleton row is created
on the first save if it does not yet exist. The Kiosk service and the
`SiteContent` service are now **on parity** on the upsert shape (the M35·2
divergence is closed); the two other Kiosk divergences (caller's session,
nullable read) remain.

## Context

`SiteContentService.SaveAsync` (ADR 0150 D3) shipped as a **no-op on a
missing row**:

```csharp
var stored = await session.LoadAsync<SiteContent>(SiteContent.SingletonId, ct);
if (stored is null)
{
    return; // a missing row is a no-op — the lane never load-or-creates (SITE·6)
}
```

The rationale (in the original code comment and in ADR 0150 D3) was: the
`FirstBootSeeder` runs on the very first boot (the `IsPristineAsync` gate,
ADR 0005 / ADR 0015) and creates the singleton row with the shipped defaults
before any admin could ever save, so a save "on a missing row" could not
actually happen in production.

The Kiosk lane (ADR 0164, M35·2) had the **same structural template** and
**deliberately chose the opposite shape** — an upsert. The M35 design doc
`docs/design/m35-kiosk-mode-design.md` §2.2 pinned the three
`SiteContentService` → `KioskService` divergences (upsert vs. no-op;
caller's session vs. own session; nullable read vs. never-null read), and
the "upsert vs. no-op" item was the one that named the `SiteContentService`
no-op as the template.

This ADR closes the "upsert vs. no-op" divergence by **moving the
`SiteContentService` template to the upsert shape** (the M35·2 shape), not
by moving Kiosk to the no-op shape. The reasoning:

1. **A no-op save is a latent bug, not a design choice.** The no-op was
   correct only because the seeder runs before any save. A deployment that
   runs on a non-pristine boot, or a test harness that constructs the
   service without running the seeder (the existing
   `SiteContentServiceTests.BootStoreAsync` harness does exactly this and
   must manually `SeedRowAsync` "so the save is not a no-op"), would see
   a silent, audit-free, un-persisted write. The save would return
   `Task.CompletedTask` with no error, no `AccessAudit` row, and no
   visible change.
2. **The upsert is what the write actually needs to do.** The write
   contract (ADR 0150 D3, invariant C3/C4) is: "one session, doc + exactly
   one `AccessAudit` row commit together; strong consistency — the new
   value is live on the very next `GetAsync`." A no-op on a missing row
   is incompatible with that contract (the value is **not** live on the
   next `GetAsync` — it is still the in-code fallback, not a saved value,
   and no audit row was written).
3. **The upsert is what the sibling service already does.** The M35·2
   upsert is the correct shape; this ADR aligns the template to it. The
   register's M35·2 assumption was already the register's source of truth
   (`SavePlaylistAsync` upserts). The template was the outlier.
4. **The seeder's create-if-missing is unchanged and still idempotent.**
   `FirstBootSeeder.SeedSiteContentAsync` (SITE·3) is already create-if-
   missing, never-overwrites (the `SeedLanguageCatalogAsync` /
   `SeedDefaultPagesAsync` shape). A first-save on a missing row creates
   the row with the caller's field set (the `SiteContent` POCO defaults
   for any field the caller did not set, the ADR 0150 D4 shape — the
   byte-identical `kw-l` defaults); the seeder's later no-op on a warm
   boot is unchanged (it checks `LoadAsync` and skips if the row exists).
   There is no conflict between the two writers: they are idempotent in
   the same direction (create-if-missing, never-overwrites on a warm
   boot / second save).

## Decision

### D1 — the write lane: upsert-on-missing

`SiteContentService.SaveAsync` is amended to create the singleton row on
a save when it does not yet exist:

```csharp
var stored = await session
    .LoadAsync<SiteContent>(SiteContent.SingletonId, ct)
    .ConfigureAwait(false)
    ?? new SiteContent { Id = SiteContent.SingletonId };
```

The rest of the lane (apply the field set, `session.Store(stored)`, the
single `AccessAudit` row, `SaveChangesAsync`) is **unchanged**. The
`SiteContent` POCO field defaults (the ADR 0150 D4 "byte-identical to the
shipped `kw-l` text" shape) are the fallback for any field the caller
does not set — the same fallback the `GetAsync` in-code fallback returns
(ADR 0150 D2, SITE·1).

**Strong consistency is now actually honored on the missing-row path**
(invariant C4): the very next `GetAsync` returns the saved value (not
the in-code fallback), and exactly one `AccessAudit` row is written
(action `site.save`, `TargetKind` `"site"`, `Via = Admin`, `TargetId`
`"singleton"` — the ADR 0150 D3 shape).

**No new route, no new seam, no new bounded context, no new `AccessAction`
/ `AccessVia` / `Decide()` branch, no EF migration, no `kw-l` key
change.** The seam signature is byte-identical
(`Task SaveAsync(SiteContent content, string actorBy, CancellationToken
ct = default)`); the audit-row shape is byte-identical (one row, the
existing action / target-kind / via / target-id). The delta is a single
line in the service (the `?? new SiteContent { Id = … }` coalesce) + the
doc comment updates (this ADR + the `ISiteContentService.SaveAsync`
doc comment + the `KioskService` / `IKioskService` divergence notes).

### D2 — the M35·2 divergence is closed

The `KioskService` doc-comment "Three intentional departures from the
`SiteContentService` template" becomes "**Two** intentional departures"
(caller's session vs. own session; nullable read vs. never-null read).
The "Upsert vs. no-op" item is retired and replaced with an explicit
**"Upsert parity (ADR 0170)"** note: both `SavePlaylistAsync` and
`SiteContentService.SaveAsync` upsert — a missing row is created. A save
is never a silent no-op in either service.

The `IKioskService.SavePlaylistAsync` doc comment's "**Upsert**: a
missing row is created (the register's M35·2 pin — the one intentional
departure from the `SiteContentService.SaveAsync` no-op-on-missing
template; …)" is amended to: "**Upsert**: a missing row is created (the
register's M35·2 pin — a resident can configure their playlist on a fresh
instance without a seeder; ADR 0170 moved the
`SiteContentService.SaveAsync` template to the same upsert shape, so the
two services are on parity — a save is never a silent no-op)."

The M35 design doc `docs/design/m35-kiosk-mode-design.md` §2.2 "Upsert
vs. no-op" divergence note is amended with the same
"ADR 0170 closed this divergence" note. (The design doc is a historical
record; the amendment is appended, not rewritten.)

### D3 — the ADR 0150 D3 "never load-or-creates" sentence is retired

ADR 0150 D3's "The lane **upserts** the singleton — it never creates a
second row (D6)" is preserved (still true — the upsert is still an
upsert, not a second row). The **`ISiteContentService.SaveAsync` doc
comment**'s "(a missing row is a **no-op** — the lane does not load-or-
create; …)" is amended to "(a missing row is **created** — the ADR 0170
upsert-on-missing amendment, the ADR 0164 `KioskService` 'upsert vs.
no-op' shape: a save is live and idempotent even before the seeder ran,
never a silent no-op)".

ADR 0150's own file gains an `## Amendments` section (the ADR 0005 /
0015 / 0050 convention) with a 2026-10-22 entry pointing at ADR 0170.

### D4 — the Core test pins the new behavior

`tests/Kumunita.Core.Tests/SiteContentServiceTests.cs` gains
`SaveAsync_MissingRow_CreatesSingleton` (U08·1b): a save on a store with
**no** row (the `BootStoreAsync` harness without `SeedRowAsync`) creates
the singleton row with the caller's field set, writes exactly one
`AccessAudit` row (`site.save`, `site`, `Admin`, `TargetId`
`"singleton"`), the very next `GetAsync` reflects the change (strong
consistency), and a second save is still an upsert (exactly one row —
SITE·6). The existing `SaveAsync_WritesOneAccessAuditRow` /
`SaveAsync_StrongConsistency_LiveOnNextGetAsync` /
`SaveAsync_UpsertsSingleton_NoDuplicateRow` tests (which seed a row first)
are **unchanged** and still pass (the upsert on an existing row is the
same code path). The `SeedRowAsync` helper's doc comment is amended to
explain it now exists to exercise the existing-row path (the missing-row
path is pinned by the new test).

### D5 — the sibling `SurfaceLabelsService` (M29·6) is **not** amended in
this pass

`SurfaceLabelsService.SaveAsync` (ADR 0152, M29) has the **same**
no-op-on-missing-row template (the "M29·6 never load-or-creates" pin,
`SurfaceLabelsService.cs:164`). The same reasoning applies to it. This
ADR deliberately does **not** amend it — the user's request was scoped to
the `/admin/site` surface (ADR 0150), and the M29 lane is a separate
bounded context with its own ADR, its own test suite, and its own
`SeedRowAsync`-style harness. If the same latent-bug class is confirmed
on the M29 surface (a save on a missing row would silently no-op), the
M29 lane should get its own ADR 01xx amending ADR 0152 in the same shape
as this one — a one-line `?? new SurfaceLabels { Id = "singleton" }`
coalesce + the same doc / test updates. The M29 design doc
`docs/design/m29-admin-surface-labels-design.md` §2.1 already names the
"ADR 0150 … no-op on missing row — the SITE·6 'never load-or-creates'
pin" as the shape it mirrors, so the M29 amendment would need to update
that reference too.

## Consequences

- **A save on a missing row is now live and audited.** The invariant C3
  (one session, doc + audit row commit together) and C4 (strong
  consistency) are now actually honored on the missing-row path. A
  deployment that somehow skips the seeder (a non-pristine boot, a test
  harness) can still save site content and see the result.
- **The seeder is unchanged.** `FirstBootSeeder.SeedSiteContentAsync`
  (SITE·3) is still create-if-missing, idempotent, never-overwrites. A
  first-boot seed creates the row with the shipped defaults; a later
  save overwrites the field set the caller provides; a warm-boot seed is
  a no-op (the row already exists). No conflict.
- **The Kiosk service is on parity with the template.** The M35·2
  "upsert vs. no-op" divergence is closed. The M35 design doc's §2.2
  divergence notes are amended (appended, not rewritten) to reflect the
  closure. The Kiosk `SavePlaylistAsync` code is **unchanged** (it was
  already the upsert shape).
- **The `ISiteContentService.SaveAsync` seam signature is unchanged.**
  No caller in `Kumunita.Web` needs to change (the
  `AdminSiteController.SaveHome` / `SaveAbout` / `AddTranslation` /
  `UpdateTranslation` / `RemoveTranslation` POST actions are all
  byte-identical; they call `SaveAsync` and the behavior is now
  "always live" rather than "live iff the row exists").
- **One new Core test** (`SaveAsync_MissingRow_CreatesSingleton`) pins the
  new behavior. **Zero Web tests change** (the
  `AdminSiteControllerTests` suite constructs the controller with a mock
  `ISiteContentService` and pins the controller's delegation, not the
  service's behavior — the service's behavior is pinned in Core).
- **The sibling `SurfaceLabelsService` (M29·6) is unchanged** (D5). If
  the same latent-bug class is confirmed there, a separate ADR amending
  ADR 0152 in the same shape is the follow-up.
- **`Milestones.cs` / README Roadmap / `MilestonesTests.cs` are
  byte-identical** (a named-lane amendment, not a milestone flip — the
  ADR 0013 `GP` / ADR 0011 `M4-adjacent` precedent). **No `WhatsNew.cs`
  entry** (an internal hardening of an existing lane, not a new
  capability the platform ships — the ADR 0110 "milestone **or a named
  lane ships**" rule does not apply to a bug-fix amendment of an
  already-shipped lane). **No ADR index row added** (the amendment is
  recorded on ADR 0150's `## Amendments` section, the ADR 0005 / 0015 /
  0050 convention — not a new ADR with its own index row).
