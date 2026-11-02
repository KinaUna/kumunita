---
id: deploy-coolify-troubleshooting
title: "Coolify — Troubleshooting (quick)"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/COOLIFY.md
  - docs/deploy/coolify/step-5/52a-media-volume.md
  - docs/ops/backups.md
updated: 2026-11-02
---

> **Abstract:** a quick symptom→first-look table for Coolify deployments — media-volume loss, logouts after a redeploy, boot loops, the `/admin/setup` 404, TLS, and port issues. **Who is it for:** the host operator. **When to read the rest:** when something is broken and you need the first thing to check.

# Troubleshooting (quick)
| Symptom | First look |
|---|---|
| Avatars 404 for everyone after a redeploy (initial-circle monograms shown), or an upload vanished | the media volume is being lost between boots. Two things must both be true (see [§5.2A](step-5/52a-media-volume.md)): a **Coolify-managed named volume** attached to the **app container** at `/data/media` (the Postgres addon's `/data` volume is a different surface, same trap as [§5.2](step-5/52-data-protection.md)) **and** the `Media__RootPath` env var pointing at that same path, every deploy. Verify with `docker inspect <app-container>` (`Mounts`) and the Coolify env settings. Note the catalog row may still exist (Postgres) while the *bytes* are gone (volume) — restore the media tarball from the same backup set (the [backups runbook](../../ops/backups.md) + [restore runbook](../../ops/restore.md)) |
| Logged out / bounced to login after a redeploy,
| Health check `database` failed on boot | `Host` in the connection string (should be the internal addon name, not `localhost`) |
| Boot loops, "permission denied: schema" | the `kumunita_migrate` role isn't the **owner** of the `kumunita` database (or the `mt` schema isn't owned by it / the app role lacks `CREATE` on `mt`) — re-run [§4](40-postgres.md) step 5 (two-role split, ADR [0171](../../adr/0171-least-privilege-db-role.md)) |
| `/admin/setup` 404s on submit (or re-submit) | **This is usually success, not failure.** The setup lane is one-time: a successful POST consumes the token, sets the password, and *permanently closes* the lane — every later GET/POST to `/admin/setup` then returns 404 by design (the controller's uniform `NotFound()`, so it can't reveal "used" vs "wrong token" vs "wrong email"). Confirm the account has a password (`SELECT "Email" FROM identity."AspNetUsers"` after `\c kumunita`) and sign in at `/Account/Login` with the password you set. If that works, setup is done. Only if the account has *no* password is this a real failure — see [§6](60-verify.md) step 4 for the token probes |
| TLS not issued | DNS not pointing at the VPS yet ([§2](20-dns.md)), or port 80/443 closed |
