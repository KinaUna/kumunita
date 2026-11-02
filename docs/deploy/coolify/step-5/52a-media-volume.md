---
id: deploy-coolify-52a-media
title: "Coolify — 5.2A: Media volume"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/deploy/coolify/step-5/00-overview.md
  - docs/adr/0011-media-and-file-storage.md
  - docs/ops/backups.md
updated: 2026-11-02
---

> **Abstract:** why the avatar *bytes* must live on a persistent media volume (or uploads vanish on every redeploy), and the same two-step fix + verification as the keyring. **Who is it for:** the host operator. **When to read the rest:** before first deploy; when avatars 404 after a redeploy.

# 5.2A — Media volume persistence (required in production)
The avatar **bytes** are not in Postgres (ADR 0011): the dump carries the
catalog (`MediaObject` rows), while the content-addressed files live under
`Media__RootPath`. The in-code default is `{BaseDirectory}/media` — inside
the image layer — so a Coolify deployment **as documented above but without
a media volume loses its uploads on every redeploy**. Same two steps as
[§5.2](52-data-protection.md), different path:

1. **A persistent volume on the app container** — the same
   **Volumes / Persistent Data** section as [§5.2](52-data-protection.md) step 1 (Coolify *can*
   express a named volume on the app container; a host-path bind works too
   if you prefer it, but the named volume is the standard path, matching
   the keyring). Add a row:

   | Field | Value |
   |---|---|
   | **Target / Container path** | `/data/media` |
   | **Source / Type** | Named volume (Coolify-managed), e.g. `coolify_kumunita_media` |
   | **Mode** | `rw` |

   (The image itself declares `VOLUME /data/media` in the `Dockerfile` as
   a floor — see its comment — but a bare anonymous volume that Coolify
   drops on redeploy is exactly the failure mode this section exists to
   prevent.)

2. **`Media__RootPath` pointing at that path** (env var), set at the same
   scope as [§5.2](52-data-protection.md) step 2:

   | Variable | Value |
   |---|---|
   | `Media__RootPath` | `/data/media` (must match the container path from step 1) |

   `LocalVolumeFileStore` runs `Directory.CreateDirectory(RootPath)`
   (idempotent) on construction, so a freshly created volume just works —
   no pre-population step.

**Verify the two steps worked:** upload an avatar, then **redeploy** the
app — the avatar must still be there (the real pass/fail signal, exactly
as in [§5.2](52-data-protection.md)). From the VPS: `docker inspect <app-container>
| grep -A 4 Mounts` should show the media mount with `Source` naming the
Coolify volume and `Destination` = `/data/media`. If avatars 404 for
everyone after a redeploy (the page shows the initial-circle monograms),
the volume is not attached and/or `Media__RootPath` drifted between
deploys — check the mount, then the env var, in that order.

**Backups (align with the [backups runbook](../../../ops/backups.md) + [restore runbook](../../../ops/restore.md)):** the media volume is the *second
restore surface* — the `media-<date>.tgz` snapshot of `Media__RootPath`
belongs to the **same backup set as the `pg_dump`** (taken just before it,
offsite, same retention — the [backups runbook](../../../ops/backups.md)) and is restored **into `Media__RootPath`
before the app starts** (the [restore runbook](../../../ops/restore.md)). A `MediaObject` with no file degrades to
an avatar 404 and is re-hydratable (the owner re-uploads; the content hash
is the integrity key); an orphan file is inert.
