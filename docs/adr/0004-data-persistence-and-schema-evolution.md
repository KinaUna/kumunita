# ADR 0004 — Data persistence & schema evolution

Status: Accepted
Date: 2026-08-26
Amends: 0001 (Decision A — "Marten in place of EF Core"; Consequences — "no EF migration
tooling")

## Context

ADR 0001 chose Marten over EF Core, but ASP.NET Core Identity's default persistence *is*
EF Core (`IdentityDbContext`). Two data-layer decisions therefore need an explicit home:

1. **Schema evolution.** The original plan leaned on Marten auto-upgrade (schema derived
   from document shapes). That is fragile in production: it cannot express destructive
   changes safely, gives no review gate, and makes "DB rollback" a hand-wave.
2. **Identity store.** Either (a) accept EF Core for the Identity tables, or (b) implement
   the Identity store interfaces over Marten by hand.

## Decision

### A. Two schemas, one Postgres

One Postgres per instance (unchanged), split into schemas with a hard ownership boundary:

| Schema     | Owner               | Contents                        | Schema evolution                          |
|------------|---------------------|---------------------------------|-------------------------------------|
| `mt`       | Marten (default)    | all domain documents, projections | versioned storage features (`FeatureSchemaBase`, e.g. `KumunitaFeature`) — delta-detected, idempotent, no `mt.migrations` ledger; DDL reviewable via `WriteMigrationFileAsync()` (§B) |
| `identity` | EF Core (Identity)  | `AspNet*` user/role/token tables | EF Core migrations (`identity.__EFMigrationsHistory`) |

Set via `modelBuilder.HasDefaultSchema("identity")` on the Identity `DbContext`.
Neither ORM touches the other schema. `Core` references EF only for the Identity stores;
**no domain model ever uses EF**.

### B. Schema evolution is versioned, never auto-upgrade

- **Marten:** every domain schema change is a Weasel-backed storage feature — a
  `FeatureSchemaBase` subclass (`KumunitaFeature` is the first) registered via
  `StoreOptions.Storage.Add<T>()`. Marten 9 removed the pre-9.x `IMigration` /
  `StoreOptions.Migrations` step model; the modern equivalent is a feature that yields
  `ISchemaObject`s. Applied idempotently by `ApplyAllConfiguredChangesToDatabaseAsync()`
  (each object delta-detected against the live Postgres catalog, so a re-run is a no-op);
  the DDL is exportable for review via `WriteMigrationFileAsync()`.
  Note on the table name: this feature pattern does not write to an operator-visible
  `mt.migrations` ledger (that model was IMigration-era) — the applied-state contract is
  "delta-detection against the live catalog => idempotent," not "already recorded."
- **Identity:** standard EF Core migrations, applied at startup.
- **Applied at boot in all environments (incl. production):** the versioned steps only —
  `mt` feature changes (delta-detected, so a pristine database gets its initial state
  with no operator step — this *is* the first-boot initialization) and EF
  `Database.MigrateAsync`. Re-running an already-applied step is a no-op (OPS §2, first
  deploy = first seeder run on a live instance).
- **Document-shape auto-creation/updating (dev loop)** — the current `mt` schema is
  derived from code and applied on startup *in Development only*
  (`ApplyAllDatabaseChangesOnStartup`); never against production.
- Both are forward-only: rolling a schema back means restoring a backup (OPS.md §5).

### C. Identity keeps the stock EF Core store (rejected: custom Marten store)

Use the default `IdentityUser` + `UserStore<>` over `IdentityDbContext` unchanged. Lockout,
email confirmation, security stamps, and recovery codes all come framework-supported.

**Rejected:** hand-rolling the Identity store interfaces (`IUserStore`,
`IUserPasswordStore`, `IUserSecurityStampStore`, `IUserEmailConfirmationStore`,
`IUserLockoutStore`, `IUserAuthenticationTokenStore`, ...) over Marten. The surface is
~10 interfaces and 60+ methods, it churns across framework versions, and it would force us
to test credential plumbing we did not design. The benefit — one less ORM in the tree —
does not justify that standing cost for a small team.

## Consequences

Positive
- Zero custom Identity plumbing; the credential surface stays framework-supported.
- Single Postgres, single `pg_dump` backup story (both schemas in one dump).
- Clean boundary: each ORM owns exactly one schema; each migration system tracks its own.
- Reviewable, forward-only schema changes with a real review gate (the migration code).

Negative / accepted risks
- Two ORMs (Marten + EF Core) in the dependency tree — **scoped**: EF is Identity-only.
- Two migration systems — **scoped**: each confined to its own schema and tracking table.
- ADR 0001's "Marten in place of EF Core" is amended to: Marten for the domain; EF
  strictly for the Identity tables.

## Revisit when

- Federation replaces local Identity with an OpenIddict IdP (the `identity` schema may
  move to the IdP and be dropped here).
- An Identity framework version changes store contracts enough to re-cost the Marten
  store option.
