#!/bin/sh
# Kumunita container entrypoint — runs as root only long enough to re-claim the
# data-volume directories, then drops to the non-root `app` user (UID 1654) and
# exec's the .NET app. The running `dotnet` process is therefore non-root (the
# SEC U25 invariant), but the volume ownership is always correct at boot,
# regardless of which user owned the files when the volume was first populated.
#
# Why this exists (SEC U25 follow-up, 2026-10-31):
#   U25 dropped the app from root to `app` (Dockerfile `USER app` + `RUN chown`).
#   `RUN chown` fixes the *image layer* ownership, but a mounted named volume
#   that was first populated *before* U25 (when the app ran as root) keeps
#   root:root ownership on its files. The .NET data-protection keyring writes
#   its key XML as 0600 owned by the writing user — a root-owned 0600 key file
#   is unreadable to `app`, so antiforgery `Protect()` throws
#   `UnauthorizedAccessException` and every page that renders the antiforgery
#   token (i.e. every page) 500s. The same latent issue hits the media volume
#   (any new avatar upload would fail the same way).
#
#   The MCR aspnet base image ships `setpriv` (util-linux) but not `gosu` or
#   `su-exec`. `setpriv --reuid app --regid app --clear-groups` is the
#   setuid-free, PAM-free privilege drop that preserves the environment
#   (unlike `su`/`runuser`, which reset env and require a TTY or PAM stack).
#   `exec` replaces the shell process so `dotnet` becomes PID 1 (or the
#   container's init) and receives SIGTERM directly.
#
# This entrypoint is idempotent: on a fresh volume (first boot, or a volume
# created by a post-U25 image whose Docker VOLUME copy-in already carries
# app:app ownership) the chown is a no-op. On a stale pre-U25 volume it
# repairs the ownership. Either way the app starts correctly.

set -eu

# Re-claim the two data-volume directories (and any root-owned files inside
# them, e.g. a pre-U25 data-protection key or a pre-U25 avatar blob). `mkdir -p`
# is belt-and-suspenders: the image layer already has these dirs, but if the
# volume was created empty and the copy-in didn't fire, we still get a writable
# directory owned by `app`.
mkdir -p /data/media /data/dataprotection-keys
chown -R app:app /app /data

# Drop to the non-root `app` user and exec the .NET app. The shell's `exec`
# replaces this shell process with `setpriv`, and `setpriv` in turn replaces
# itself with `dotnet` — so `dotnet` becomes the container's PID 1 (or the
# container's init) and receives SIGTERM/SIGINT directly (clean shutdown).
# `setpriv` does NOT take an `exec` subcommand (it executes its first argument
# directly), so `dotnet` is the first argument after the flags. `--clear-groups`
# discards any supplementary groups (app has only its primary, but this is the
# documented setpriv idiom). Environment is preserved (ASPNETCORE_*, Community_*,
# ConnectionStrings_*, etc. all pass through — `setpriv` does NOT reset env the
# way `su`/`runuser` would).
exec setpriv --reuid app --regid app --clear-groups dotnet Kumunita.Web.dll
