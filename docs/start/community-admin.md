---
id: start-community-admin
title: "Community admin entry"
tier: living
kind: entry
status: current
audiences: [community-admin]
related:
  - docs/README.md
  - docs/guides/CONVENTIONS.md
  - docs/SECURITY.md
  - docs/philosophy/everyday-life.md
  - docs/adr/
updated: 2026-11-02
---

> **Abstract:** the links a community admin (a moderator or global admin)
> is most likely to want, each with a one-line reason. Links only, no
> facts — this page cannot drift. **Who is it for:** the people who run
> this community on the platform (not the platform's own operators).
> **When to read the rest:** when you need the "why" behind a rule, the
> plain-language version of a feature, or the ADR that decided it.

# Community admin entry

You run this community — you moderate reports, manage accounts, and keep
the platform behaving. The docs below are the ones that matter for *you*.

| Link | Why |
|---|---|
| [`README.md`](../../README.md) | What Kumunita is and what it does — the front page. |
| [`docs/guides/CONVENTIONS.md`](../guides/CONVENTIONS.md) | The in-app guide registry — the canonical home for the how-to guides you'll share with residents (the `admins`, `moderators`, `audience` rows are the ones you'll cite most). |
| [`SECURITY.md`](../SECURITY.md) | The privacy model, the threat model, and the access-control rules — the authoritative constraint on what you may and may not see (the model hub; the [control map](../security/control-map.md) and [adversaries](../security/adversaries.md) are the pages to read when you enforce a rule). |
| [`docs/philosophy/everyday-life.md`](../philosophy/everyday-life.md) | The philosophy in everyday examples — the "why" behind the design choices you're enforcing. |
| [`docs/adr/`](../adr/README.md) | The ADR index — when you need the *deciding* document for a rule (e.g. ADR 0003 for moderator scoping, ADR 0010 for private groups). |
| [`docs/philosophy/START-HERE.md`](../philosophy/START-HERE.md) | The full philosophy argument, one hour. |

The platform's own operators (the people who host this deployment) read the
[platform-maintainer entry](platform-maintainer.md).
