---
id: deploy-coolify-54-health
title: "Coolify — 5.4: Health check"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/deploy/coolify/step-5/00-overview.md
updated: 2026-11-02
---

> **Abstract:** the Coolify health-check configuration for the app — `GET /health` must return `200`. **Who is it for:** the host operator. **When to read the rest:** when configuring the app's health check.

# 5.4 — Health check
Path `GET /health`, expected `200`.
