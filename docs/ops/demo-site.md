---
id: ops-demo-site
title: "Deploy a demo site (sample data)"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/provision.md
  - docs/ops/first-boot.md
  - docs/adr/0002-deployment-topology.md
  - docs/adr/0055-development-sample-data-lane.md
updated: 2026-11-02
---

> **Abstract:** how to bring up a *deployed demo site* — a throwaway
> `Production` instance seeded with the mock neighborhood, disposable by
> design — plus the teardown/reset steps. **Who is it for:** the host
> operator showing a prospective neighborhood the platform. **When to read
> the rest:** before the demo, and at teardown.

# Deploy a demo site (sample data)

A **deployed demo site** is a throwaway `Production` instance a prospective
neighborhood can click through, pre-populated with the mock neighborhood (ADR
0055 / 0056). It is a **separate app + dedicated Postgres** (the one-instance-per
neighborhood topology, ADR 0002) — never a real neighborhood's database. It is
**disposable by design**: treat its data as scratch, and never let it become a real
deployment (wipe + re-seed, or provision fresh, when it outlives its purpose).

What makes it a *demo* instance (vs. a real one) is the env set, not the image:
`SampleData__Enabled=true` (first-boot only) plus the `examplium.com` naming. In the
**deploy posture** the seed admin keeps its `SeedAdmin__` setup-token lane, the other
demo accounts get random high-entropy passwords, and a single credentials summary is
emailed to the seed admin's address through the durable outbox — no weak credential is
stored on the public instance (the [Running-locally](../dev/running-locally.md) table is the **Development**
credential set, not this one).

1. Provision a fresh app + dedicated Postgres exactly as [Procedure: provision](provision.md), but with the
   demo identity: `Community__Name` (e.g. "Kumunita Demo Residents"),
   `Community__SupportEmail` (e.g. `demo@examplium.com`), a strong DB password, a real
   SMTP relay, `Verification__BaseUrl` = the demo domain, `Media__RootPath` +
   `DataProtection__KeysDirectory` on a volume ([configuration reference](configuration-reference.md)),
   and `SampleData__Enabled=true`.
2. Set `SeedAdmin__Email` to the **operator's real admin address** (the credentials
   e-mail and the setup e-mail both land there) and `SeedAdmin__Token` to a freshly
   generated one-time token ([first boot](first-boot.md)). Store the token in the
   secrets manager.
3. Deploy against a **fresh** Postgres (the pristine-DB gate is what makes the seeder
   run). Confirm `/health` is OK.
4. Open the site's first-boot setup link, **complete the admin setup** (set the admin
   password; the token is invalidated on first use), then **remove `SeedAdmin__*` from
   env** ([first boot](first-boot.md), step 4).
5. **Capture the credentials e-mail** in the admin inbox — it lists each demo account's
   e-mail → random password. Store the ones you need (secrets manager) and **delete the
   e-mail** (it is sensitive; the body says so).
6. Exercise the demo (feed, groups, posts, events, RSVPs, the language switcher, the
   resident blog). Verify the pinned "Test Platform" announcement is visible to a
   signed-out visitor (it is the most visible thing on a demo instance and tells them
   this is not real and to keep private data off it).
7. Record it in the **Instance inventory** ([configuration reference](configuration-reference.md)) with a `Notes`
   marker such as `DEMO — disposable, sample data (ADR 0056)` so it is never mistaken
   for a real neighborhood.

Teardown / reset: `docker compose down -v` (dev) or delete the Coolify app + its Postgres
addon and re-run steps 1–5 for a fresh seed — the content seeder is first-boot only and
idempotent, so a warm DB will not re-seed or duplicate (the pristine gate keeps it from
re-running). The one warm-boot write is the sample-event translation backfill
(create-if-missing, idempotent, ADR 0060) — it never duplicates and never clobbers a
Translator's edit. Do **not** point a real neighborhood's `Community__Name` / Postgres at
this instance; if a demo site is ever going to carry real residents, provision a
**real** instance ([provision](provision.md), without `SampleData__Enabled`) and migrate
the residents in — a demo DB is not a real DB.

## Related

- [Provision a new neighborhood](provision.md) — the procedure this one wraps with a demo identity.
- [First boot & admin setup](first-boot.md) — the sample-data posture and the `SeedAdmin__*` discipline.
- [Configuration reference](configuration-reference.md) — the env fields, the inventory table.
