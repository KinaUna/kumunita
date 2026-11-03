---
id: deploy-coolify-55-deploy
title: "Coolify — 5.5: Deploy"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/deploy/coolify/step-5/00-overview.md
  - docs/ops/environments.md
updated: 2026-11-02
---

> **Abstract:** the deploy action for the app in Coolify — production deploys from the `release` branch only. **Who is it for:** the host operator. **When to read the rest:** when shipping a new version.

# 5.5 — Deploy

With the app attached in [step 5's overview](00-overview.md) (source, buildpack,
ports, env vars all set), **Deploy** builds the image from the repo root
`Dockerfile` and pushes it to the environment.

- Production deploys from the **`release` branch only** — `main` never deploys
  (see the [environments runbook](../../../ops/environments.md)).
- After a deploy, the container is replaced on the next build — which is exactly
  why the two persistent volumes ([data-protection keys](52-data-protection.md)
  and [media](52a-media-volume.md)) must be attached, or the keyring and uploads
  are lost on every deploy.
- Then move to [step 6 — Verify](../60-verify.md).
