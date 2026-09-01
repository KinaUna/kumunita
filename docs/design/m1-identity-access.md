# Design Doc — M1: Identity, groups, delegation, authorization

> The "Seams & contracts" section is mandatory. Three tests run and
> recorded at the end.

## Context

M0 delivered the deployable substrate: solution layout, Docker + Coolify
deploys, versioned `mt` boot block, `/health`, per-instance `Community`
config. M1 builds the **access model** — the linkage every later milestone
hangs its arrows off of (ARCHITECTURE.md §2). For M1 the three access
modules go from architecture prose to living code behind the interfaces
frozen in **ADR 0006**.

## Goals / Non-goals

**In scope**

- **Identity lifecycle** — signup; resident verification email +
  admin manual-verify safety valve; one-time setup-token admin bootstrap
  (`SeedAdmin__Email` / `SeedAdmin__Token`, consumed on first login —
  OPS.md §2); role promote/demote + component-scope assignment
  (`ModeratorAssignment`, ADR 0003).
- **UserInfoModule** — `Profile` document (including `visibility: Audience`
  storage — M2 consumes it, M1 stores it), `Group` / `GroupMembership`,
  `DelegationGrant` (grant/revoke/documents).
- **AuthorizationModule** — audience evaluation `Any`/`All` with
  empty-audience-denies guard; effective principal through delegation;
  break-glass inline check (consumed `AdminOverride`, `expiresAt` checked
  at decision time); `moderatorAccess` off by default; `AccessAudit`
  always-on (Allow and Deny, single rows + bulk aggregate rows);
  `CanAsync` / `CanSeeAsync` sharing one `MatchGroups` pass.
- **Wolverine side effects** — the durable `OutboxEmail` handler (Postgres
  invocation state, 6 attempts / ~24 h, `EmailDeadLetter` on final
  failure; transactional outbox so a failed send never rolls back a domain
  write — §6.2) and the `AuditPurge` scheduled job (tiered expiry, §6.4).
- **Schema** — versioned forward-only `mt` steps for the M1 documents
  (M0's `KumunitaFeature` pattern); `identity` EF migrations for stock
  Identity tables (+ `ExternalId` reserved). `FirstBootSeeder` extended:
  components seed (Safety, Maintenance, Social, Governance) + admin
  bootstrap, idempotent.
- **Seam tests** — the invariant list under "Feedback loops" below, each
  anchored to a frozen ADR 0006 invariant number.
- **`/health` degraded state** — reports `degraded` when
  `EmailDeadLetter` count > 0 (§6.2).

**Out of scope**

- Profile editing UI and directory visibility rules (M2).
- Posts, components UI, reports and report-driven moderator unlock (M3 —
  the `moderatorAccess` mechanism, not its triggers, lands in M1).
- Events/projects (M4/M5); export/iCal/notification surfaces (M6);
  language-catalog admin surface + `LocalizedPage` (M6 — catalog *seeding*
  is M6, so M1's seeder is admin + components only).
- MCP/API, federation.

**Surfaces**

- Signup / verify / login; profile bootstrap (name, email, `visibility`
  defaulting per ADR 0001-B — author's choice of audience, absolute by
  default).
- `/admin` shell — roles + component scope (admin only), `/admin/audit`
  (GlobalAdmin), `/admin/break-glass` (consume `AdminOverride` once).

## Human cost

No resident-facing behavior yet; the first resident touch is one
verification email (a designed seam, not a leak). No notification channels
created beyond the one that opens an account. Team cost: M1 is invariant-
heavy — the seam tests are deliberately front-loaded, so the pace must be
sustainable under a test-heavy milestone (`in-code.md`: at this scale the
risk is a *wrong* access decision, not load).

## Parts affected

- `Kumunita.Core`: new `Identity/` module (issues principal), `UserInfo/`,
  `Authorization/`; `Bootstrap/FirstBootSeeder` extended; Wolverine email
  handler + `AuditPurge` job; `mt` versioned steps.
- `Kumunita.Web`: signup/login/profile pages; `/admin` area; `/health`
  degraded wiring.
- `tests/Kumunita.Core.Tests`: seam tests (against the `PostgresFixture`'s
  fresh-scratch-DB shape — no fixture change needed).

## Seams & contracts (mandatory)

- **Created:** the ADR 0006 interface set — `IIdentityService`,
  `IUserInfoService`, `IAuthorizationService` — plus shared vocabulary
  (`ThinPrincipal`, `Decision`, `VisibleSet`, `AccessAction`,
  `IAuditableResource`). Change management per ADR 0006-E: these changes
  are *breaking* and ADR-gated.
- **Access model touched:** everything M1 builds *is* the access model.
  New contracts: `Audience { mode: Any|All, grants: [User|Group] }`;
  group resolution (strong consistency, no projection in the access
  path); delegation resolution (effective principal + `Via` mapping);
  audit contract (`AccessAudit` shape, always-on, same-transaction).
  Migration path: forward-only `mt` versioned steps, pristine-boot-
  compatible (fresh DB and existing instance both handled by M0's boot
  block). Audited: access decisions self-audit; admin actions (role
  change, scope change, break-glass consumption) append `Via`-tagged
  audit rows.
- **Seams created, each with an owner:**
  - Authorization ↔ UserInfo — group/delegation resolution (authorization
	may call `IUserInfoService`, never feature modules; read path is one
	group-load per request).
  - Identity ↔ cookie — the claim shape is the whole principal; the
	no-relational-data test pins it.
  - Authorization ↔ `mt.AdminOverride` — break-glass inline check (rare
	document, indexed; no job, no separate store).
  - `OutboxEmail` outbox → durable handler → SMTP — the side-effect seam
	(`in-code.md`: a side effect that fails silently is a broken loop →
	dead-letter table + degraded `/health` + OPS.md §7 re-queue).

## Feedback loops

- **Seam tests** (each cites ADR 0006 invariant):
  - empty-audience-denies under `All` (§C1) and the empty-audience guard
	itself;
  - delegation action-out-of-scope → Deny with `Via = Delegation` and the
	acting identity recorded (§C2);
  - group membership change takes effect on the next request (membership
	scoping of posts granted *before* the change — "a group whose
	membership changed after a post was granted", in-code.md) (§C4);
  - moderate-by-default OFF; `moderatorAccess = true` is the only
	standing-moderator path until M3 (§C5);
  - `CanSeeAsync` bulk equals per-`CanAsync` aggregate over the same
	candidates (§C6 — the no-drift property of `MatchGroups`);
  - restricted content is *always* audited: Allow and Deny, single row
	and bulk aggregate row, same transaction as the domain write (§C3);
  - thin principal carries no relational data (claim-set assertion, §B);
  - break-glass: consumed `AdminOverride` elevates until `expiresAt`,
	checked inline, no job.
- **Part tests:** `MatchGroups` truth table (Any/All × grant kinds ×
  `moderatorAccess` flag × delegation scope); effective-principal
  resolution; one per command handler; email-handler failure/retry/
  dead-letter harness.
- **Production signals:**
  - `EmailDeadLetter` count → `/health` **degraded** (owner: operator;
	action: OPS.md §7 re-queue or discard).
  - Unverified-signup pile-up → admin manual-verify valve (owner:
	GlobalAdmin; the account remains usable once verified manually).
  - Audit log readable at `/admin/audit`; `in-product.md` medium cadence:
	an admin who reads it. `AuditPurge` tiered expiry keeps the table
	bounded; purge decision (what expires when) is set in the job's
	config, not improvised.

## Emergent impact

- The **Authority** domain is born: identity ↔ permission ↔ audit is a
  chain that can be traced forward (decision → audit row) and backward
  (audit row → grant → identity) from day one (domains-of-integration.md).
- Privacy default for profiles exists as *storage* from day one (M2 never
  patches visibility onto a legacy document).
- Cost: the authorization path (group load + `MatchGroups` per list view)
  is on every read — the "allow to grow freely" complexity of the fat
  service, built deliberately early so no feature ever invents its own
  rule.

## Local-optimization check

Optimizes the whole (the trust/privacy substrate), not a part. No
resident-facing metric exists in M1, so there is no number to over-fit.
The explicit cost is team time on seam tests — paid because at this scale
the risk is a *wrong* access decision (in-code.md: "the risk is a wrong
access decision, a leaked private detail").

## FACES check

- **Stable — strengthened:** thin principal + always-on audit +
  forward-only versioned schema + two-admin standing practice = a place
  that survives handoff and restore.
- **Coherent — strengthened:** one authorization primitive; a moderator
  can trace any access decision to a grant (the Authority-domain
  heuristic) and a new resident can narrate who-can-see-what from the
  profile's `visibility`.
- **Flexible — consumed (the named trade):** `Any`/`All` semantics are
  frozen before M3's real workloads prove `All` earns its edge
  (ADR 0001-B revisit). Interface changes are ADR-gated: slower than
  ad-hoc, by design.
- **Adaptive — strengthened:** `EmailDeadLetter` → degraded health →
  operator re-queue is a closed response path; audit rows are the
  response path for access anomalies.
- **Energizing — protected:** the only resident contact in M1 is one
  verification email; nothing yet asks for their attention.

## Rollout & rollback

- Versioned forward-only `mt` steps through M0's boot block (pristine →
  seed; existing → no-op or forward migration); `identity` via EF Core
  migrations. `FirstBootSeeder` is idempotent (admin exists → no-op) —
  the "M0-deployed instances: M1's first deploy" path in README.
- `/health` gates the Coolify rollout (DB liveness, M0) + degraded on
  dead-letter count (M1).
- Rollback: M1's schema is **additive** (new documents/tables; no
  mutation of `mt.community`), so re-deploying the M0 image over a
  forward-migrated database is safe. Restore: `pg_dump` of the single
  Postgres captures both schemas (OPS.md).

## Risks

- **Audience-evaluation bug = privacy leak — the product's top
  risk.** Mitigated by the single `MatchGroups` pass, the explicit empty
  guard, and the seam-test list as acceptance criteria, not follow-up.
- **Audit write fails ⇒ domain write fails.** Accepted: an unevaluated
  access without audit is the loop that must not be honest, and the
  same-transaction commit makes the two failure together or not at all.
- **First-boot seeder on an already-migrated DB (drift case)** — M0's
  `IsPristine` + seeder idempotency handles it; covered by the existing
  `DbBootstrapIsPristineTests` pattern.
- **Break-glass row read on every decision** — a rare document on a hot
  path. Index on `(actorId, consumedAt non-null)`; at this scale cost is
  negligible and the cost of *not* checking inline (a job's lag window)
  is worse.
- **Verification email dead while SMTP is down** → unverified accounts
  accumulate. Mitigated by the admin manual-verify valve; `/health`
  flags the dead-letter.

## Integration step served

No resident-facing value arrow yet — that is M2–M5's job. What M1 moves
is **the linkage itself**: the access model that will turn M3's *signals*
into *shared awareness* for the right audience. Value added: an auditable,
traceable "who may see what" that every later arrow requires — without
it, later content is a bag of parts nobody can route trust through.

## World seams

- **Verification email** — designed outbound handoff into the
  resident's mailbox; one send, click to return on-platform; the only
  data crossing is the link.
- **`/health`** — operator seam (Coolify probe).
- Nothing else leaves the platform. (Export / iCal / notification
  surfaces are deliberately M6 — per the seam principle, design them
  then, not by accident now.)

## The three tests (recorded)

1. **Closed-loop:** sign-up → verification → verified sign-in (and
   first-boot admin → verified sign-in) completes entirely on-platform;
   the only exit is the verification link, which returns the resident to
   the platform. ✔
2. **Handoff:** exactly one designed handoff (the verification email,
   single send); no re-explaining, no re-keying anywhere in M1. ✔
3. **Part-vs-whole:** no resident-facing metric to over-fit; the cost is
   team time spent on seam tests — the whole's insurance policy, not a
   part's output. ✔

FACES name-carry (per definition-of-done, in-code.md): strengths =
**Stable + Coherent**, spend = **Flexible** (`Any`/`All` frozen early;
ADR-gated interface change).
