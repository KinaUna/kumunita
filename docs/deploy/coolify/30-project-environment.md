---
id: deploy-coolify-30-project-env
title: "Coolify — 3: Project & environment"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/adr/0002-deployment-topology.md
updated: 2026-11-02
---

> **Abstract:** how to create the Coolify project + environment for one neighborhood, and why the app and its Postgres must share one environment. **Who is it for:** the host operator. **When to read the rest:** before adding the Postgres addon or the app.

# 3 — Project & environment (once per neighborhood)
In Coolify, **resources** (apps, Postgres addons, services) live under a
**project**, and each project holds one or more **environments** (production,
staging, A/B tests, …). Every environment is a complete, independently
deployable set of resources with its own env variables and secrets. The
neighborhood's Postgres and app must be attached to the *same environment* —
that is what makes them network-adjacent and shares one scoped env-var set.

1. **Projects → Add new** (from the Coolify home). Name it after the
   neighborhood (`maplewood`). One project per neighborhood (ADR [0002](../../adr/0002-deployment-topology.md)).
2. Open the project → **Add a new environment** → name it `production`.
   A project must have at least one environment.
3. `production` is the only environment for now. A future staging or A/B
   instance of this neighborhood is a *second environment in the same
   project* — never a second project (one neighborhood = one project).
