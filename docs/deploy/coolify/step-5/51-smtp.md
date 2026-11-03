---
id: deploy-coolify-51-smtp
title: "Coolify — 5.1: SMTP"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/deploy/coolify/step-5/00-overview.md
  - docs/adr/0131-smtp-transport-mailkit-swap.md
  - docs/ops/email-smtp.md
updated: 2026-11-02
---

> **Abstract:** the SMTP env values for Kumunita's mail — the MailKit transport's three TLS shapes, the local (Mailpit) vs real-relay shapes, 465/SMTPS support, and the one-time seed-admin token. **Who is it for:** the host operator. **When to read the rest:** when configuring mail for verification + the setup-admin lane.

# 5.1 — SMTP (concrete env values)
The `SmtpSender` reads six options from the `SMTP` section: `Host`, `Port`,
`User`, `Pass`, `Secure`, and `From` (`SmtpOptions` in
`src/Kumunita.Core/Identity/SmtpSender.cs`); all are bound through
`Configure<SmtpOptions>` in `Program.cs`. The transport is **MailKit's
`SmtpClient`** (ADR [0131](../../../adr/0131-smtp-transport-mailkit-swap.md), 2026-10-02) — it supports all three TLS shapes the
`Secure` option offers: `Tls` (STARTTLS, the conventional 587 shape; the
default), `Ssl` (implicit TLS / SMTPS, the "TLS up front" 465 shape), and
`None` (no enforced encryption — take TLS if the relay offers it, plain
otherwise; the local-only Mailpit shape). The original BCL
`System.Net.Mail.SmtpClient` was replaced because the .NET 10 BCL transport
reached `MAIL FROM` without a live AUTH session on a real relay (Proton Mail
`smtp.protonmail.ch:587`) even though the identical credentials authenticated
cleanly from the same container and from the VPS host — a live, reproducible
BCL regression (see ADR [0131](../../../adr/0131-smtp-transport-mailkit-swap.md) for the full evidence trail).

**A. Local dev loop (compose / Mailpit):** run via `docker-compose.yml` at the
repo root, which brings up `Mailpit` (SMTP on port 1025, web UI on 8025) plus
Postgres 18 in the same network. Only reachable from inside that network — this
is the `ASPNETCORE_ENVIRONMENT=Development` path, not a Coolify deploy:

| Variable | Value |
|---|---|
| `SMTP__Host` | `mailpit` (the compose service name) |
| `SMTP__Port` | `1025` (Mailpit's SMTP port; no AUTH, no TLS needed — Mailpit is a local relay) |
| `SMTP__Secure` | `None` (plain SMTP; the honest shape for a loopback-only relay) |
| `SMTP__User` / `SMTP__Pass` | leave unset (Mailpit doesn't require auth) |
| `SMTP__From` | optional; the dev loop has no strict relay to reject a missing `From` |

**B. A real relay (test server or production):** most relays require `User` +
`Pass` **and** a STARTTLS port. The shape to use:

| Variable | Value |
|---|---|
| `SMTP__Host` | e.g. `smtp.mailgun.org`, `smtp.resend.com`, `relay.mailprovider.com` — your provider's host |
| `SMTP__Port` | the relay's **STARTTLS** port — conventionally **587**; use the port your provider documents for STARTTLS (some are 2525 or 5870 — check the provider's SMTP-settings page, not a "webmail login" port) |
| `SMTP__Secure` | `Tls` (the default; `None` would talk plain SMTP, which leaks credentials in transit and is not a production shape) |
| `SMTP__User` | the relay's username (often the sender's email or an API-issued account) — **secret** |
| `SMTP__Pass` | the relay's password / API key — **secret**; store in the secrets manager, inject via env, never in the runbook |
| `SMTP__From` | the resident-facing address shown in verification emails (often the same as `Community__SupportEmail`) |

> **465 / implicit TLS (SMTPS) is supported.** The MailKit transport
> (ADR [0131](../../../adr/0131-smtp-transport-mailkit-swap.md)) drives `SecureSocketOptions.SslOnConnect` for the `Ssl` value, so
> a 465-only relay is a first-class shape: set `SMTP__Port=465` and
> `SMTP__Secure=Ssl`. (The prior BCL transport could not do this; that
> limitation is what drove the swap.)
>
> **`SMTP__User` and `SMTP__Pass` are a pair.** Setting only one (or an empty
> string for one and a real value for the other) is a configuration error:
> `SmtpSender.SendAsync` throws an `InvalidOperationException` before opening
> the client rather than failing opaquely at the `AUTH` handshake once on the
> wire. See the `SmtpOptions.User` doc comment in `SmtpSender.cs` for the
> invariant.

Whichever shape you pick, **one strong `SeedAdmin__Email` / one strong token**
is required for the first-boot admin lane (the [first-boot runbook](../../../ops/first-boot.md), step 1). Generate it
with `openssl rand -hex 24`, store it in your secrets manager, and set it before
the first boot. The token is **one-time** — the app invalidates it on the admin's
first login (the [first-boot runbook](../../../ops/first-boot.md), step 4: then remove `SeedAdmin__*` from env so
nothing reusable remains).

> **Why this matters:** verification + admin handoff both depend on a *delivered*
> email. An unconfigured SMTP host **throws** on first send (SmtpSender is
deliberate about not "sending to nowhere") — the durable handler then
retries/dead-letters per the [email (SMTP) runbook](../../../ops/email-smtp.md) and `/health` flips to `degraded` so it is visible.
Verify the mail landed on the real inbox (the [first-boot runbook](../../../ops/first-boot.md), step 5) before
considering the test "working".
