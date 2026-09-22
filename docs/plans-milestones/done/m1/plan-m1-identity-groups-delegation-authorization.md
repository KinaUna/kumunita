# M1 — Implement Identity, Groups, Delegation, Authorization

## Understanding
M1 builds the access model the design doc front-loads: the ADR 0006 interface set materialized in `Kumunita.Core` (Identity/UserInfo/Authorization), the seam tests anchored to ADR 0006 invariant numbers, versioned `mt` schema steps, Wolverine email + audit-purge side effects, and the web surfaces (signup/verify, `/admin`, `/health` degraded). The user wants the whole milestone planned first, then approved and executed in sequence.

## Assumptions
- `Kumunita.Core` references no ASP.NET HTTP types (ADR 0006-D); cookie/claim shaping lives in `Kumunita.Web`.
- The frozen ADR signature is semantic — C# identifiers may deviate for conflict avoidance (e.g. `AccessAction` per the ADR's own note).
- Audit rows commit in the same transaction as the domain write (§C3): authorization decisions in M1 are write-path calls (pre-serve checks ride the same transaction).
- `mt` steps follow the M0 `KumunitaFeature` pattern (Marten 9 `FeatureSchemaBase`, idempotent delta-detection, forward-only).
- `PostgresFixture` provides fresh-scratch-DB tests with no fixture change needed (per design doc "Parts affected").
- Test command follows the repo's `.vscode/tasks.json` conventions; `run_tests` tool used for Test Explorer runs.

## Approach
Execute bottom-up through the dependency chain: vocabulary types and interfaces first (the frozen contract, written to the ADR letter), then `mt` schema for the M1 documents, then modules bottom-up (UserInfo storage → authorization `MatchGroups` + audit + break-glass → identity lifecycle), Wolverine side effects, web surfaces, and the seam tests anchored to each ADR invariant number at each phase. Each phase ends with build green; seam tests run and recorded (the design doc's "three tests" + per-invariant list) at the end.

## Key Files
- docs/adr/0006-module-boundary-contracts.md — the frozen contract (source of truth for every C# identifier added in step 2)
- docs/design/m1-identity-access.md — scope, invariant list, acceptance criteria
- src/Kumunita.Core/Identity/AppDbContext.cs — `identity` schema context; gains `ExternalId` reserved column
- src/Kumunita.Core/Bootstrap/FirstBootSeeder.cs — currently a comment-stub; gains the five seeder steps idempotently
- src/Kumunita.Core/KumunitaFeature.cs — M0 `FeatureSchemaBase` pattern to extend with M1 `mt` schema
- src/Kumunita.Web/SideEffects/ — step 7: `OutboxEmailHandler` (durable send + `Fault<OutboxEmail>` dead-letter hook) and `AuditPurgeHandler` + `AuditPurgeTick` (self-rescheduling `TimeoutMessage`); host wiring in `src/Kumunita.Web/Program.cs` (`UseWolverine`, `RetryWithCooldown`, `PublishFaultEvents`)
- src/Kumunita.Core/Identity/EmailDeadLetterWriter.cs + src/Kumunita.Core/Authorization/AuditPurgeService.cs — step-7 business logic (Wolverine-free, testable)
- tests/Kumunita.Core.Tests/PostgresFixture.cs — fresh-scratch-DB shape for all seam tests
- tests/Kumunita.Core.Tests/SideEffectHarnessTests.cs — step 7: failure/retry/dead-letter harness + `AuditPurge` tiering tests

## Risks
- **Audience-evaluation bug = product's top risk** (design doc §Risks): mitigated by single `MatchGroups` pass, explicit empty guard, seam tests as acceptance criteria.
- **Same-transaction audit**: `CanAsync`/`CanSeeAsync` must run on a live Marten session (caller supplies/owns the session) so the audit row commits with the domain write; design the session ownership explicitly in step 3.
- **Audience mode `All` over empty grants** vacuously-true pitfall — the explicit guard (invariant 1) must be unit-tested at `MatchGroups` truth-table level (any-mode/all-mode × grant kinds × `moderatorAccess` × delegation scope).
- **Break-glass inline check on every decision**: index on `(actorId, consumedAt non-null)` per design doc; rare document on hot path is accepted.
- **`Kumunita.Core` no-HTTP-types rule** (ADR 0006-D): the identity module must use Identity's data-access layer without leaking `HttpContext` into Core; claims read via a service abstraction registered in Web.
- **Wolverine is a Web-side package** (repo convention, per `IMailerStage` doc comment): the `WolverineFx` + `WolverineFx.Marten` packages go into `Kumunita.Web.csproj`; the durable handlers live in `Kumunita.Web/SideEffects/`; the Wolverine-free business logic (`EmailDeadLetterWriter`, `AuditPurgeService`, `SmtpSender`) stays in `Kumunita.Core`. Handler tests follow the design-doc "email-handler failure/retry/dead-letter harness" against the business-logic seam (no live message host needed).

## Steps
1. Study remaining ADRs + repo conventions — read ADR 0003 (roles/moderator scoping), ARCHITECTURE.md §4.2/§5, Web `Program.cs` (DI/cookie shape), `Kumunita.Core.Tests` test patterns (`PostgresFixture`, DDL tests), `.vscode/tasks.json` test command
2. Materialize ADR 0006 contracts — create `Kumunita.Core/Identity/`, `UserInfo/`, `Authorization/` with `IIdentityService`, `IUserInfoService`, `IAuthorizationService` and `ThinPrincipal`, `Decision`, `VisibleSet`, `AccessAction`, `IAuditableResource`, `Group`, `Profile`, `DelegationGrant`, `ModeratorAssignment`, `AccessAudit`; build green
3. Versioned `mt` schema for M1 documents — new `FeatureSchemaBase` step(s) for `profile`, `group`, `group_membership`, `delegation_grant`, `moderator_assignment`, `access_audit`, `email_dead_letter`, `admin_override` (index on `(actorId, consumedAt non-null)`); DDL test in the `KumunitaFeatureDdlTests` style; `AppDbContext` gains `ExternalId` reserved column + EF migration
4. UserInfoModule implementation — `Profile` document (including `visibility: Audience` storage), `Group`/`GroupMembership` storage with strong-consistency `GetGroupIdsAsync`, delegation grants (grant/revoke); one handler test each
5. AuthorizationModule implementation — `MatchGroups` single-pass with empty-audience guard (invariant 1), `CanAsync`/`CanSeeAsync` sharing it (invariant 6), delegation scope enforcement + `Via = Delegation` recording (invariant 2), `moderatorAccess` off by default (invariant 5), inline break-glass `AdminOverride` check, `AccessAudit` in the same transaction (invariant 3); `MatchGroups` truth-table + part tests
6. Identity lifecycle — signup; verified-by-email via `OutboxEmail` + admin manual-verify valve; setup-token seed-admin bootstrap; `FirstBootSeeder` five steps implemented idempotently (community row, seed admin, default components, language-catalog note, first-boot email); role promote/demote + `ModeratorAssignment` component-scope assignment; `GetCurrentAsync` thin-principal claim wiring in Web; no-relational-data claim-set test (invariant set B)
7. Wolverine side effects — add `WolverineFx`/`WolverineFx.Marten` to `Kumunita.Web.csproj` (repo convention: Wolverine is a Web-side package); durable `OutboxEmail` handler in `Kumunita.Web/SideEffects/` (Postgres invocation state, retry cooldowns summing to ~24 h, `EmailDeadLetter` on final failure via the `Fault<OutboxEmail>` hook, transactional — a failed send never rolls back the domain write); `AuditPurge` self-rescheduling job (tiered expiry per §6.4, business logic in Core's `AuditPurgeService`); failure/retry/dead-letter harness tests against the Wolverine-free business-logic seam (`tests/Kumunita.Core.Tests/SideEffectHarnessTests.cs`)
8. Web surfaces — signup/verify/login pages; profile bootstrap (name, email, `visibility` defaulting per ADR 0001-B); `/admin` shell (roles + component scope, admin only), `/admin/audit` (GlobalAdmin), `/admin/break-glass` (consume `AdminOverride` once); `/health` degraded when `EmailDeadLetter` count > 0
9. Run seam tests + record the three tests — run the full invariant-anchored seam list (C1, C2, C4, C5, C6, C3, claim-set, break-glass, group-change-next-request) against `PostgresFixture`; record the design doc's three tests as the doc's acceptance gate; verify `/health` degraded path end-to-end
