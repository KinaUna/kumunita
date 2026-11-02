---
id: ops-first-boot
title: "First boot & admin setup"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/provision.md
  - docs/ops/demo-site.md
  - docs/adr/0005-multilingual-support.md
  - docs/adr/0055-development-sample-data-lane.md
updated: 2026-11-02
---

> **Abstract:** what happens automatically on first run (schema, seeded
> GlobalAdmin, default components, language catalog), plus the operator's six
> steps to complete admin setup — and the one-time-token discipline behind
> it. **Who is it for:** the host operator bringing a fresh instance up.
> **When to read the rest:** after [provision](provision.md), and whenever
> the `SeedAdmin__*` fields are in play.

# First boot & admin setup

What happens automatically on first run: Marten applies the schema → Identity schema is
created → the GlobalAdmin is created for `SeedAdmin__Email` with `SeedAdmin__Token` as
a **one-time setup credential** (invalidated on first use) → a verification email is
sent → default components (Safety, Maintenance, Social, Governance) are seeded → the
language catalog is seeded (source language `en` enabled and set as default).
Re-running the seeder is a no-op once the admin exists.

> **Sample data — opt-in (ADR 0055 / 0056 / 0060):** when `SampleData__Enabled=true` the
> first-boot lane seeds a mock neighborhood (residents, groups, posts/replies,
> events/RSVPs, tags, a blog, de/fr/da event translations) so a fresh instance is
> immediately exercisable. It is gated on `SampleData__Enabled` **and** first-boot, so a
> real deployment that never sets the flag never runs it. On a **warm** boot the same
> flag additionally backfills any *missing* sample-event `de`/`fr`/`da` translations
> — create-if-missing, idempotent, and never overwriting a Translator's in-app edit
> (ADR 0060), so an instance whose first boot predates the event-translation lane
> picks them up on the next redeploy. Two postures: in `Development`
> it uses the documented weak demo credentials (the [Running-locally](../dev/running-locally.md) table); on a deployed
> demo site (`Production` + the flag) the seed admin keeps its `SeedAdmin__`
> setup-token lane, the other demo accounts get random high-entropy passwords, and a
> single credentials summary is emailed to the seed admin's address through the
> durable outbox. The demo logins are `@examplium.com` (kept distinct from the real
> `kumunita.com`). See the [Running-locally](../dev/running-locally.md) "Sample data & demo accounts" table, and
> [deploy a demo site](demo-site.md) for the step-by-step.

**Languages are in-app data, not config.** Supported languages, the default language,
and all translations live in the DB (`mt` schema; admin-managed under
`/admin/languages`, ADR 0005) — not env, not image config. They ride the existing
DB backup; nothing extra to provision.

Operator steps:
1. Generate a strong one-time token (pre-M1: `openssl rand -hex 24`; M1+: the seeded
   admin arrives pre-initialized — confirm it was created and use the token exactly
   once); store it in your secrets manager, then set `SeedAdmin__Email` +
   `SeedAdmin__Token` in Coolify.
2. Deploy; open `https://<domain>`; confirm it renders with the correct
   `Community__Name`.
3. Log in as the seeded admin **using the token** — first login is "set your password";
   the token is invalidated by the app on this first use.
4. **Remove `SeedAdmin__*` from env** in Coolify (nothing reusable remains to leak).
   First M1 deploy, on a live instance: confirm the log shows schema-init ran once
   (subsequent boots are a no-op), a **new** Postgres addon: schema-init happens on the
   first M1 deploy (it is the first boot that creates it).
5. Verify: `/health` returns OK, the seeded admin exists, a test email arrives
   (check the provider's inbox, not just "sent"), and the default components (incl.
   the language catalog) appear in-app.
6. Update the inventory (image version, admin contact) — see the [configuration reference](configuration-reference.md).

Why a token, not a password: env is a config store — visible to ops tooling, shell
history, and container env dumps. A one-time token in env is acceptable; a long-lived
admin password in env is not. The app invalidating it on first use removes the
change-then-remove race entirely.

## Related

- [Provision a new neighborhood](provision.md) — the procedure this one follows.
- [Deploy a demo site](demo-site.md) — the sample-data variant of this procedure.
- [Configuration reference](configuration-reference.md) — the env fields named here.
