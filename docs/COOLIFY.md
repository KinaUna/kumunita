---
id: coolify
title: "Kumunita — Coolify Setup Guide"
tier: living
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/OPS.md
  - docs/adr/0002-deployment-topology.md
  - docs/deploy/coolify/60-verify.md
updated: 2026-11-02
---

> **Abstract:** the hub for the Coolify deployment guide — practical,
> imperative steps for what a host operator does on a VPS to bring one
> Kumunita neighborhood online. Every step lives in its own file under
> [`deploy/coolify/`](deploy/coolify/) (split in `D05`); this page is the
> ordered checklist (0 → 6) with one-line "what each is for" reasons.
> **Who is it for:** the host operator. **When to read the rest:** pick the
> step you need from the checklist and read that file.

# Kumunita — Coolify Setup Guide

Practical instructions for what a host operator does on a VPS: installing
Coolify, a project + environment per neighborhood, a neighborhood's
Postgres, and the Kumunita app. A `how-to` reference only — this guide has
*no* per-instance values. Where this guide and the runbook overlap, the
runbook wins — the operational procedures (provision, first-boot, backups,
restore, TLS, SMTP, hardening) all live in the [OPS runbook](OPS.md).
Why one instance per neighborhood: [ADR 0002](adr/0002-deployment-topology.md).

**Convention:** every step is numbered and imperative. Stamp the step files
with **Last tested: <date>** when you run them on a live box.

## The checklist (0 → 6)

| Step | What it does | File |
|---|---|---|
| 0 — Plan | the step map; how often each runs | [`00-plan.md`](deploy/coolify/00-plan.md) |
| 1 — Install Coolify | once per VPS: installer, admin token, firewall | [`10-install-coolify.md`](deploy/coolify/10-install-coolify.md) |
| 2 — DNS prerequisite | once per VPS/domain: A/CNAME at the VPS before cert issuance | [`20-dns.md`](deploy/coolify/20-dns.md) |
| 3 — Project & environment | once per neighborhood: project + `production` env (app + Postgres share it) | [`30-project-environment.md`](deploy/coolify/30-project-environment.md) |
| 4 — Postgres 18 | once per neighborhood: dedicated DB, two-role split, one-shot init | [`40-postgres.md`](deploy/coolify/40-postgres.md) |
| 5 — The app | once per neighborhood: attach the app + the complete env table | [`step-5/00-overview.md`](deploy/coolify/step-5/00-overview.md) |
| 5.1 — SMTP | the mail env values (Mailpit vs real relay, 465/SMTPS, seed-admin token) | [`step-5/51-smtp.md`](deploy/coolify/step-5/51-smtp.md) |
| 5.2 — Data-protection keys | persistent keyring volume (or every redeploy logs everyone out) | [`step-5/52-data-protection.md`](deploy/coolify/step-5/52-data-protection.md) |
| 5.2A — Media volume | persistent media volume (or uploads vanish on every redeploy) | [`step-5/52a-media-volume.md`](deploy/coolify/step-5/52a-media-volume.md) |
| 5.3 — Domains | attach the domain + keep `Verification__BaseUrl` in sync | [`step-5/53-domains.md`](deploy/coolify/step-5/53-domains.md) |
| 5.4 — Health check | `GET /health` must return `200` | [`step-5/54-health-check.md`](deploy/coolify/step-5/54-health-check.md) |
| 5.5 — Deploy | build + push from the `release` branch only | [`step-5/55-deploy.md`](deploy/coolify/step-5/55-deploy.md) |
| 6 — Verify | the post-deploy checklist, then hand over to the runbook | [`60-verify.md`](deploy/coolify/60-verify.md) |

Something broken? Start at [`Troubleshooting (quick)`](deploy/coolify/troubleshooting.md).
