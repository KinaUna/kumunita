# Kumunita — multi-stage build (README "Deployment": compile TS with tsc, publish, runtime).
# Build context: repository root. The front-end is plain TypeScript compiled with tsc
# (no bundler, no dev server).

# --- 1. Front-end: compile the plain-TS client with tsc ---
FROM node:22 AS ts
WORKDIR /src
COPY src/Kumunita.Web/package.json src/Kumunita.Web/package-lock.json src/Kumunita.Web/tsconfig.json ./
COPY src/Kumunita.Web/client/ ./client/
RUN npm ci && npm run build

# --- 2. .NET: restore + publish the web app (with the compiled front-end in wwwroot) ---
# SEC L5 (U25): the tag is SHA-pinned to the multi-arch index digest resolved
# from the MCR registry on 2026-10-31 (re-resolve and update when the .NET 10
# SDK rolls a new image; a pinned digest stops a base-image move changing the
# build silently). `node:22` (stage 1) is left unpinned — Docker Hub blocks
# anonymous digest resolution — and is on the "to pin" list in the handoff note.
FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:de4fe90a98845d75910b9769662465c84de1a934665c431279d1382b2bddc02b AS build
WORKDIR /src
COPY global.json ./
COPY src/Kumunita.Core/ ./src/Kumunita.Core/
COPY src/Kumunita.Web/ ./src/Kumunita.Web/
COPY --from=ts /src/wwwroot/js ./src/Kumunita.Web/wwwroot/js/
RUN dotnet publish src/Kumunita.Web -c Release -o /app/publish

# --- 3. Runtime: slim ASP.NET Core image ---
# SEC L5 (U25): SHA-pinned to the multi-arch index digest (resolved 2026-10-31).
FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:de0ee358cdd672f7017a003bed21e9ac9ed48a27c922a59b18175b6e82fa7fee AS runtime
WORKDIR /app
# curl is the healthcheck client: Coolify pings the /health endpoint with it
# (the slim image ships neither curl nor wget). wget is installed too so
# Coolify's default `wget`-based healthcheck works out of the box without
# any operator configuring a custom probe.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl wget \
    && rm -rf /var/lib/apt/lists
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish ./
EXPOSE 8080
# Persistence safety net (see docs/COOLIFY.md §5.2 for the operator-facing
# step). Declares /data/dataprotection-keys — the ASP.NET Core data-protection
# keyring directory when DataProtection__KeysDirectory points there — as a
# volume, so that if the container starts without an operator-attached named
# volume, Docker still promotes it to an anonymous named volume and the
# keyring survives a bare `docker restart`. This is strictly less robust than
# a Coolify-managed named volume (anonymous volumes can be pruned; Coolify
# redeploy replaces the container and may drop them), so the VOLUME line is
# a floor, not a replacement for the operator attaching a named volume to the
# Application in the Coolify UI.
VOLUME /data/dataprotection-keys
# Second persistence surface (ADR 0011, OPS.md §4/§5 "second restore surface"):
# the media byte store — the content-addressed avatar bytes live on this
# volume, not in Postgres (the dump carries only the MediaObject catalog).
# Media__RootPath is configured at /data/media in docker-compose.yml and MUST
# be set to an operator-provided mounted volume in production (COOLIFY.md
# §5.2A); the in-code default ({BaseDirectory}/media = /app/media) sits inside
# the image layer, so without the mount uploads are lost on redeploy — exact
# same floor-not-replacement rationale as the keyring volume above.
VOLUME /data/media
# SEC L5 (U25): run the app as a non-root user (was root). The MCR aspnet base
# already ships a non-root `app` user (UID 1654, /home/app) — we use it as the
# *runtime* user for the .NET process, not as the container's default USER.
# curl/wget are KEPT (not dropped): the health probe is external (Coolify's
# default `wget` probe / operator `curl`) and the .NET runtime has no built-in
# one-line HTTP health client, so there is no `HEALTHCHECK` built-in to
# substitute (see the `## U25` handoff note).
#
# U25 follow-up (2026-10-31): the U25-era `RUN chown -R app:app /app /data`
# fixed the *image layer* ownership, but a mounted named volume first populated
# by a pre-U25 (root-running) image keeps root:root ownership on its files —
# e.g. a 0600 data-protection key that the `app` process then cannot read
# (antiforgery `Protect()` throws, every page 500s), or a media-volume file
# that a new upload cannot write. The `docker/entrypoint.sh` script runs as
# root just long enough to re-claim the volume dirs at *every* boot, then drops
# to `app` via `setpriv` and `exec`s the .NET app — the running dotnet process
# is still non-root (the U25 invariant), but volume ownership is correct
# regardless of how the volume was first populated. The MCR base image ships
# `setpriv` (util-linux) but not `gosu` or `su-exec`; `setpriv` is the
# setuid-free, PAM-free, env-preserving drop (unlike `su`/`runuser`).
#
# Note: we deliberately do NOT set `USER app` in the image. The container's
# default USER stays the MCR base default (root) so the entrypoint can run its
# chown; the entrypoint then drops to `app` before `exec`-ing the app, so the
# running process is non-root. This matches the standard pattern used by
# PostgreSQL and other "run-as-root-for-init-then-drop" containers.
COPY docker/entrypoint.sh /docker/entrypoint.sh
RUN chmod +x /docker/entrypoint.sh
ENTRYPOINT ["/docker/entrypoint.sh"]
