---
id: deploy-coolify-10-install
title: "Coolify — 1: Install Coolify"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/ops/hardening-checklist.md
updated: 2026-11-02
---

> **Abstract:** how to install Coolify itself on a fresh Ubuntu/Debian VPS — the one-click installer, the admin token, and the 80/443/22 firewall. **Who is it for:** the host operator, once per VPS. **When to read the rest:** the first time you set up a VPS for this platform.

# 1 — Installing Coolify (once per VPS)
Fresh Ubuntu/Debian VPS, as a user with `sudo`, key-based SSH already
configured (the [hardening checklist](../../ops/hardening-checklist.md)):

1. Run the official one-click installer:
`curl -fsSL https://cdn.coollabs.io/coolify/install.sh | bash`
This provisions Coolify and its components behind Caddy (reverse proxy;
issues and renews Let's Encrypt automatically). For more information and
up to date instructions, see [Coolify Installation docs](https://coolify.io/docs/get-started/installation).

2. Open the wizard in the browser at `http://<vps-ip>`, choose the
installation type (Cloud — default; no external services needed for this
repo's usage), and generate the **admin login token**. Store the token in
the secrets manager; log in and create a human Coolify account.

3. Firewall: open **80/443** (Caddy; needed for domain routing and cert
issuance) and **22** (SSH — key only, source-restricted if your provider
supports it). Everything else stays closed (the [hardening checklist](../../ops/hardening-checklist.md)).

