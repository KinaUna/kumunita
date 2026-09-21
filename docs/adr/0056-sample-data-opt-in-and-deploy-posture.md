# ADR 0056 — Sample-data opt-in: a flag-gated seeder with a deploy posture

Status: Accepted
Date: 2026-09-21
Amends: **0055** (the development sample-data seeder — its gate is changed from
the `Development` environment wall to an explicit `SampleData__Enabled` opt-in,
and the seeder gains a second "deploy" posture). The seeder's data model,
content, idempotency, and the `examplium.com` domain convention are all
**unchanged** — this ADR only changes *when* it runs and *how* it hands over
credentials in the non-Development case.

## Context

ADR 0055 shipped a first-boot, **Development-only** mock neighborhood (a
password-bearing admin, a scoped moderator, a translator, four residents, groups,
announcements, posts/replies, events/RSVPs, tags, a resident blog, and de/fr
translations). Its two hard constraints were:

- **It must never ship** — a real deployment must be *incapable* of seeding demo
  residents, demo credentials, or a `test@…` support address.
- **It must not corrupt** — a warm re-run must not duplicate every group /
  announcement / post.

Both are met by construction: a `Development` ∧ first-boot gate, plus a
pristine-DB outer gate and create-if-missing accounts. The `Development` wall
is what enforces "it must never ship."

A new requirement arrived: the sample neighborhood is exactly the right content
for a **deployed demo site** (a Coolify `Production` instance a prospective
neighborhood can click through). The `Development` wall is now in the way — the
demo site must run in `Production` (the production error page, no dev-only
schema auto-apply, the real HSTS/forwarded-header pipeline), so it cannot reach
the seeder. And a `Production` instance is a **public site**: seeding it with the
weak, documented, shared passwords (`Admin123!`, `Resident123!`, …) that ADR 0055
deliberately used for local dev would mean publishing a live credential to
anyone — exactly the "it must never ship" failure the `Development` wall exists
to prevent, reintroduced through the environment door.

So the gate must change from "the environment" to "an explicit per-instance
decision," and the non-Development case must hand out credentials in a way that
stores **no weak credential on a public instance**.

## Decision

- **The gate is `SampleData__Enabled` ∧ first-boot.** A new config POCO,
  `Kumunita.Core.Identity.SampleDataOptions` (section `SampleData`, `bool
  Enabled`, **default `false`** — the same "absence is the safe default" shape as
  `SeedAdminOptions`), is bound in `Program.cs` and read at the seeder call site.
  The `IsDevelopment()` wall is removed from the gate. Absence (null/blank/omitted)
  is `false`, so a real deployment that does not carry the flag is **unreachable
  by construction** — the ADR 0055 "it must never ship" property is now enforced
  by the *absence of an opt-in* rather than by the environment, which is the
  stronger guarantee: a `Production` instance with no flag seeds nothing, and a
  misconfigured `Production` instance *with* the flag is visibly different from
  a real one in its env diff.

- **Two postures, one seeder.** The seeder keeps the ADR 0055 content and data
  model verbatim; it now takes `IMailerStage? mailer` + `string? adminEmail` and
  branches on their presence:
  - **Development posture** (`mailer == null`, i.e. the `Development` environment
    where `Program.cs` passes no mailer): the documented weak demo credentials
    (the README table), printed to the log; the seed admin keeps its
    `SeedAdmin__` setup-token lane *plus* a weak demo password. This is ADR 0055
    unchanged.
  - **Deploy posture** (`mailer != null`, i.e. a non-Development instance with the
    flag set): the **seed-admin account keeps only its `SeedAdmin__` setup-token
    lane** (no weak password is ever set on it — its credential is the one-time
    token from the first-boot setup e-mail, exactly the OPS §2 posture a real
    deployment already uses); the **other demo accounts get random 128-bit
    (32-char, lowercase-hex) high-entropy passwords** from a
    `System.Security.Cryptography.RandomNumberGenerator` (CSPRNG — distinct per
    account, never derived from or printed alongside the admin's own credential);
    and a **single credentials summary e-mail** (one `OutboxEmail`,
    idempotency-keyed `sampledata:{adminId}`) is staged to the seed admin's
    address through the durable outbox, listing each demo e-mail → its random
    password. No weak credential is stored on the public instance; the only
    plaintext credential a demo operator sees is the random one in their own
    inbox, and the e-mail tells them to delete it.

- **The seed-admin pairing is preserved.** In the deploy posture the seeder keys
  off `SeedAdmin__Email` (the same address `FirstBootSeeder` created the admin
  under), not the ADR 0055 `AdminEmail` constant — so the first-boot admin is
  *found*, its token lane is untouched, and the credentials e-mail is addressed
  to the operator's real admin mailbox. In Development it keeps keying off the
  constant (which `docker-compose.yml` keeps matched to `SeedAdmin__Email`, the
  ADR 0055 paired invariant).

- **The random-password policy is satisfied.** The app's password policy
  (`Program.cs`: `RequiredLength = 8`, `RequireNonAlphanumeric = false`) is
  trivially met by 32 lowercase-hex characters, so `AddPasswordAsync` succeeds
  without a policy override.

## Consequences

### Positive

- **A deployed demo site is now a first-class, safe configuration** — a Coolify
  `Production` app + dedicated Postgres + `SampleData__Enabled=true` + the
  `examplium.com` `Community__*`/`SeedAdmin__` names, fresh DB → first boot seeds
  the full neighborhood, and the operator's admin inbox receives the demo
  credentials. The demo site keeps the production error page, HSTS,
  forwarded-headers, and no dev-only schema auto-apply.
- **No weak credential is ever stored on a public instance.** The deploy posture
  sets random high-entropy passwords and hands them over out-of-band (the
  operator's own mail); the seed admin keeps its token lane. A compromise of the
  demo *database* does not leak a known shared password.
- **The "it must never ship" guarantee is now opt-in-out, not opt-in-in.** A real
  deployment seeds sample data only if it explicitly sets the flag *and* is
  non-Development — the default for every real instance is "nothing," and the
  flag shows up in the env diff, so a demo instance is visibly distinct from a
  real one.
- **Zero data-model change.** Same documents, same lanes, same ADR 0012
  visibility mechanism, same idempotency; the seeder's body is untouched except
  for the credential branching and the one staged e-mail.

### Negative / accepted risks

- **The deploy-posture credentials travel through the outbox e-mail, which is a
  third-party boundary in the operator's mailbox (SECURITY.md §6).** This is
  accepted: the e-mail is addressed only to the operator's own admin, carries
  only demo credentials for a site that is *by design* a public test instance,
  and the body explicitly tells the operator to delete it. It is the same
  channel OPS §2 already uses for the seed-admin setup token.
- **The seed-admin e-mail address is an operator choice in the deploy posture.**
  If an operator sets `SampleData__Enabled=true` on a *real* neighborhood by
  mistake, the demo accounts are seeded with random passwords and the admin gets
  the e-mail — recoverable (delete the demo accounts / re-seed) but a
  configuration error. The `examplium.com` naming convention and the ADR 0055
  "test platform" pinned announcement both make a mis-seeded real instance
  obvious on first visit.
- **A `Production` instance *without* the flag seeds nothing — unchanged from a
  real deployment.** The only behavioral difference from ADR 0055 for real
  instances is that they no longer need to be in `Development` to be safe; the
  safe default is now simply "flag unset."
- **The `test@examplium.com` support address and the `examplium.com` demo
  identities still appear on a deployed demo site** (via `Community__*` and the
  seeded profiles) — this is intentional and is the point of the demo; it is
  still a deliberately-fictional domain, kept distinct from the real
  `kumunita.com` (the ADR 0055 convention, unchanged).
