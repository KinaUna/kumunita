---
id: deploy-coolify-52-dataprotection
title: "Coolify — 5.2: Data-protection keys"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/deploy/coolify/step-5/00-overview.md
  - docs/deploy/coolify/step-5/52a-media-volume.md
updated: 2026-11-02
---

> **Abstract:** why the data-protection keyring must live on a persistent volume in production (or every redeploy logs everyone out), and the two-step fix + verification. **Who is it for:** the host operator. **When to read the rest:** before the first production deploy; when you see “key not found in the key ring”.

# 5.2 — Data-protection key persistence (required in production)
ASP.NET's default data-protection keyring is **in-memory**. On a Coolify
instance, every redeploy (Coolify replaces the container on each push)
**regenerates** the key, so every previously-set cookie — the session, the
antiforgery token, the `.AspNetCore.Identity` cookie — can no longer be
decrypted. Consequence: a signed-in user is bounced to the login screen on
the next redeploy, and the log shows

```
fail: Microsoft.AspNetCore.Antiforgery.DefaultAntiforgery[7]
      An exception was thrown while deserializing the token.
      Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException: The antiforgery token could not be decrypted.
       ---> System.Security.Cryptography.CryptographicException:
      The key {<guid>} was not found in the key ring.
```

along with a persistent warning. The `warn` line is **informational but not
diagnostic** — ASP.NET Core emits it from the `FileSystemXmlRepository`
constructor every time that repository is used (i.e. whenever the keyring is
backed by the file system), regardless of whether the target path is a real
named volume or just a writable layer. It will appear on every deployment
that has `DataProtection__KeysDirectory` set, whether or not the volume
is attached; do not use its absence as a signal that the fix worked.

```
warn: Microsoft.AspNetCore.DataProtection.Repositories.FileSystemXmlRepository[60]
      Storing keys in a directory '/data/dataprotection-keys' that may not be
      persisted outside of the container. Protected data will be unavailable
      when container is destroyed.
```

**Fix — two steps, both required:**

1. **A persistent volume on the app container**
   Coolify does not attach a volume to an Application by default — the
   "`data` volume" people talk about on the **Postgres addon** is a
   different surface. Open the App's page (Project → Environment →
   your app), scroll to the **Volumes / Persistent Data** section (UI label
   varies by Coolify version: "Volumes", "Storage", or under
   "Advanced → Advanced Options"), and add a row:

   | Field | Value |
   |---|---|
   | **Target / Container path** | `/data/dataprotection-keys` |
   | **Source / Type** | Named volume (Coolify-managed), e.g. `coolify_kumunita_keys` |
   | **Mode** | `rw` |

   Saving this makes Coolify create a `docker volume` on the VPS and
   mount it inside the app container at `/data/dataprotection-keys`. The
   keyring written there survives container replacement (redeploy, restart,
   Coolify's image swap, VPS-level `docker system prune -a` — as long as the
   Coolify volume itself isn't explicitly removed).

2. **`DataProtection__KeysDirectory` pointing at that path** (env var), set
   at the **environment** level so the app sees it:

   | Variable | Value |
   |---|---|
   | `DataProtection__KeysDirectory` | `/data/dataprotection-keys` (must match the container path from step 1) |

   `Program.cs` (`src/Kumunita.Web/Program.cs`, lines 135–141) reads this,
   runs `Directory.CreateDirectory` on it (throws on `EACCES` → fail-fast,
   good), and registers `PersistKeysToFileSystem`.

   Omit it (in-memory) only where the instance is truly single-container
   with no redeploy and you accept that *every* container restart invalidates
   existing sessions — dev, unit tests, and the compose stack in
   `docker-compose.yml` all leave it unset. **Production should not omit it.**

**Verify the two steps worked:**

1. Log in as a user, then **redeploy** the app. The user should **still be
   logged in** — the session cookie references the key in the persistent
   ring, so it's still decryptable — and then submit a form: no antiforgery
   error. This is the real pass/fail signal.
2. From the VPS: `docker inspect <container> | grep -A 4 Mounts` should
   show the data-protection keys mount with `Source` naming the Coolify
   volume and `Destination` = `/data/dataprotection-keys`, `RW: true`.
   If it's not there, step 1 above isn't taking effect — the volume isn't
   attached.
3. **Ignore** the `warn: … FileSystemXmlRepository[60] … may not be
   persisted` log line in either direction: ASP.NET Core emits it from
   the `FileSystemXmlRepository` constructor every time that repository is
   used, regardless of whether the target is a real Docker volume, so its
   presence or absence in the log is **not** a signal that the keyring is
   (or isn't) on a persistent volume. Step 1 and step 2 above are the
   actual verification.

**If you still see `key not found in the key ring`,** the keys directory
the app used on boot #1 and boot #2 are different. Check, in order:

- The mount is attached to the app container (`docker inspect <container>
  | grep -A 4 Mounts`) at the *exact* path
  `/data/dataprotection-keys`, and not at a different path like `/data`
  (which would put the keys under the container's writable layer, not the
  named volume).
- `DataProtection__KeysDirectory` has the *same value* on every deploy.
  If it drifted between deploys (or was unset on one of them), the app
  would be writing/reading from two different keyrings.
- If you recently redeployed and the volume was newly created, the first
  boot after attach will write a fresh keyring; that's expected. All
  pre-attach sessions are invalidated — log in again after the first
  post-attach boot, and they should survive subsequent deploys.
