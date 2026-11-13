---
id: r15-prompt
title: "Prompt: IMPROVE-UX R15 — the ADR 0302 kiosk-link doc + service (the shared kiosk's core seam)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0285-kiosk-playlist-any-item.md
  - docs/adr/0164-kiosk-playlist.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-10
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> R15, the ADR + Core `KioskLink` doc + create/revoke/validate service that
> starts the shared-kiosk-link lane (R15–R19). **Who is it for:** the next
> coding agent (fresh context). **When to read the rest:** when starting R15
> (the Core seam that R16's kiosk-link authentication, R17's community-visible
> filter, R18's `/settings/kiosk` surface, and R19's SEC review all build on).

# Fresh-context prompt: IMPROVE-UX R15

You are continuing the Kumunita repository on `development`. The lane's
running log is `docs/plans-milestones/in-progress/improve-ux-handoff-notes.md`
(R14, the SEC review of the ADR 0301 grace period, is the last unit that
landed and is committed). R15 is the **first kiosk unit** — a new lane, not a
continuation of the deletion work.

**Implement IMPROVE-UX unit R15 only** — the ADR + the `KioskLink` doc + the
create/revoke/validate service. The plan card (in `plan-improve-ux.md`, the
R15 row):

> R15 — **ADR** + Core `KioskLink` doc (owner, playlist, token *hash*,
> created, lastUsed, revoked) + create/revoke/validate service; tests.
> Read first: `Kumunita.Core/Kiosk/*`, ADR 0285. Done when: **tests:
> revoked/unknown token refused, hash only at rest.**

Decision D4 (in the plan's Decisions table) is the shape you are building:
*shared kiosk link* — per-playlist, revocable, long random token, read-only,
reaches only `/kiosk` + its slides, shows **community-visible items only**
(others get the existing denied slide), last-used shown, every read audited
under the owner as via-kiosk-link. **A SEC-lane review step is required**
(R19 does it). R15 is the **Core seam + ADR**; R16 (auth on `/kiosk` +
`/kiosk/slide`), R17 (the community-visible filter), R18 (`/settings/kiosk`
surface) and R19 (SEC review) are separate later units — **do not start them**.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** — the
  doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the comment
  rule, the don't-pause-mid-task rule).
- **`docs/adr/0285-kiosk-playlist-any-item.md`** and
  **`docs/adr/0164-kiosk-playlist.md`** — the existing kiosk lane this builds
  on (the per-resident `KioskPlaylist`, the `IKioskService` 2-method seam, the
  `AccessVia` / `AccessAudit` conventions, the "zero audit on the read" floor).
- **`src/Kumunita.Core/Kiosk/`** — `KioskPlaylist.cs`, `IKioskService.cs`,
  `KioskService.cs`, `KioskEntry.cs`, `KioskDocTypes.cs`,
  `DependencyInjection.cs` — the exact doc/registration/audit patterns to
  mirror. Note the `KioskPlaylist` audit shape (one `kiosk.save` row,
  `Via: Owner`, `TargetKind = "kiosk"`).
- **`src/Kumunita.Web/Controllers/KioskController.cs`** and
  **`src/Kumunita.Web/Services/IKioskItemCatalog.cs`** — the existing
  `/kiosk` surface (the `AccessVia` / denied-slide shape R17 will extend; you
  only need it for context, not to change it).
- **The ADR 0301 deletion seam** (`src/Kumunita.Core/Identity/DeletionScheduler.cs`,
  `IDeletionScheduler.cs`) as the *template* for the shape: a `const` + a
  service + a `// TODO ❓` for an open question the SEC review settles — the
  kiosk-link token length / expiry is the parallel.
- **`docs/dev/testing-and-ci.md`** — the test-runner quirk and the
  in-process `dotnet exec` invocation.
- **`git status`** + the branch (R14's commit must be intact; you are on
  `development`).

The **Core work** (all in `src/Kumunita.Core/Kiosk/` unless noted):

1. **ADR 0302** (check `docs/adr` for the next free number — the lane has
   been consuming numbers in parallel; do not reuse 0301 or 0300): the
   shared-kiosk-link decision. State the D4 shape, the token-*hash*-at-rest
   rule, the revocable / read-only / community-visible / audited-as-via-kiosk-link
   invariants, and — like ADR 0301 did for the grace period — flag the open
   question(s) (token length, expiry, max links per playlist) as
   `// TODO ❓` the R19 SEC review settles.
2. **`KioskLink` doc** (one row per link): `PlaylistId`, `OwnerSubjectId`,
   `TokenHash` (the SHA-256 of the long random token — **never the raw
   token**), `CreatedAt`, `LastUsedAt?`, `RevokedAt?`. Register it on the
   **existing `CoreDocTypes`** surface (the ADR 0004 §B.1 additive
   precedent — delta-detected by Marten, no DDL, no new `*DocTypes`).
   Carries `[DataClass]` (the ADR 0298 SecGate rule).
3. **`IKioskLinkService` + `KioskLinkService`** — `CreateAsync(playlistId,
   ownerSubjectId)` (generates the long random token, stores only the hash,
   returns the raw token **once** for the caller to show), `RevokeAsync(linkId,
   ownerSubjectId)`, `ValidateAsync(tokenHash, ownerSubjectId)` (returns the
   row if valid + not revoked, else `null` — the caller renders the denied
   shape). Audit rows: one `kiosk.link.create` + one `kiosk.link.revoke`
   (`Via: Owner`, `TargetKind = "kiosk"`, the `KioskPlaylist` precedent); the
   validate read is unaudited (the M35·5 "zero audit on the read" floor — the
   read is a config load, not an access decision). The `ValidateAsync` is
   **pure Core** (no Web coupling) so R16's middleware can call it.
4. **DI registration** in `Kiosk/DependencyInjection.cs` (the
   `IKioskService` precedent).
5. **Tests** in `tests/Kumunita.Core.Tests/` — a `KioskLinkServiceTests`
   class: (a) a revoked/unknown token is refused (`ValidateAsync` returns
   `null`), (b) the stored value is a hash, never the raw token (the
   "hash only at rest" pin), (c) `CreateAsync` returns the raw token exactly
   once (the caller cannot recover it from the store), (d) an owner's
   `ValidateAsync` for another owner's link is refused.

**Scope rules:**

- **Do not touch** `KioskController.cs` / `IKioskItemCatalog.cs` / the
  `/settings/kiosk` views — R16/R17/R18 own those. R15 is the Core seam +
  ADR only.
- **Do not start R16–R19.** The unit card's "Done when" is the only
  acceptance bar.
- **One commit per change** (the house rule): the ADR + the
  `KioskLink` doc + the service + the tests + the DI registration land in
  **one** `feat(ux):` commit (`feat(ux): ADR 0302 + KioskLink doc +
  create/revoke/validate service (IMPROVE-UX R15)`); the ADR is a
  `docs(ux):` commit if you split it (the R10 precedent: `docs(ux): ADR
  0301 — … (IMPROVE-UX R10)`).
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged for
  the parked native review) — but R15 is Core + ADR, so the only new strings
  are the audit-row action names (which are not localized).
- **The translation parity test** must stay green (it will — you add no
  localized keys).

**Before committing:** run the narrowest tests via the in-process runner
(see `testing-and-ci.md`): `KioskLinkServiceTests` (your new class) +
`KioskServiceTests` (the existing `KioskPlaylist` seam intact) + the
translation parity test, and `dotnet build`. If any test fails, read the
error, adjust, continue — a failed test is a recoverable step, not a
stopping point.

Then report: the ADR number you used, the `KioskLink` doc shape, the service
method signatures, the test class name + the number of tests, the commit
hash(es), and the `// TODO ❓` open questions you flagged for R19's SEC
review. Do not commit unrelated work.
