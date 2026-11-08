---
id: ops-owner-ops-ledger
title: "Owner-ops ledger"
tier: reference
kind: runbook
status: current
audiences: [platform-maintainer]
related:
  - docs/ops/hardening-checklist.md
  - docs/ops/configuration-reference.md
  - docs/ops/privacy-operations.md
updated: 2026-11-08
---

> **Abstract:** one record of every step the **owner/operator** must run
> against a live instance — a provider console, a prod volume, a CI runner, a
> break-glass rehearsal — each row naming **what proves the control exists**
> (a test, ADR, config key, or runbook section) and a **"last verified"**
> column that stays **blank until the owner runs it**. **Who is it for:** the
> host operator, before declaring a control in place. **When to read the
> rest:** when an owner-ops item resurfaces, or you are auditing whether a
> control is merely in code or actually running on the instance.

# Owner-ops ledger

This is a **record, not a control**: it flips no values, opens no decisions,
and asserts nothing is done. A row whose **last verified** cell is blank is
*open* — that is the point of the column. The three source groups are merged
and de-duped (one row per step; overlapping sources share one row, named in
**source**). The "what proves it" cell is the **witness that the control
exists in code/docs** — it is not the owner's attestation, which belongs in
the blank **last verified** cell.

| item | source | what proves it | last verified |
|---|---|---|---|
| Install pgBackRest/WAL-G + offsite repo, `archive_mode=on` + `archive_command` + stanza, wire the alert to the F12 channel, and run the PITR restore drill | F4 | `docs/OPS.md` §4/§5 (the "Last tested (PITR)" lines to stamp); the F12 alert channel | |
| Run a live end-to-end session-kill-all / password-change verification against the served app (a second browser session, then assert it is signed out on its next request) | F5 | `KillAllSessions_SignsOutAll`, `PasswordChange_InvalidatesOtherSessions` | |
| Set the initial `disposable-emails.txt` content and the owner's Turnstile site key (`KUMUNITA__SIGNUP__BOT_CHECK__SITE_KEY`) | F9 | `Signup_DisposableEmail_Rejected`, `Signup_BotChallengeEnforced`; `configuration-reference.md` `KUMUNITA__SIGNUP__BOT_CHECK*` | |
| Decide the `SENTRY_DSN` value for the instance (now safe after the PII floor + log gate landed) | F10 + IS05/IS08 | `Error_TrackerRegisteredWhenDsnSet`, `InformationLog_IsNotABreadcrumb`, `NoLogTemplate_CarriesAPiiPlaceholder`; `configuration-reference.md` `SENTRY_DSN` | |
| Point the dead-letter alert sink at the F12 human channel (until then the `NoopAlertSink` fires — a structured log, not a human) | F11 | `DeadLetter_FiresAlert`; `configuration-reference.md` `Community__SupportEmail` | |
| Confirm `Community__SupportEmail` is a real, monitored address (alerts **and** the `security.txt` `Contact` field both route to it) | F12/F13 | `Alert_RoutedToConfiguredChannel`, `SecurityTxt_ReturnsValidFields` | |
| Wire the Coolify deploy to require the green CI check on `release` (deploy precondition / branch protection), confirm Docker on the CI runner, and confirm branch protection enforces the required check | F14 | `.github/workflows/ci.yml` (the gate is the workflow file itself); the `Dockerfile` + `docker-compose.yml` runner step | |
| Run ClamAV on the prod volume (`VirusScan__Host` / `VirusScan__Port`) so the serve gate is active (until then it is fail-open) | F16 | `InfectedUpload_NotServed`; `configuration-reference.md` `VirusScan__*` | |
| Point the production mail provider's bounce/complaint webhook at `POST /api/mail/suppress` with the `X-Suppression-Token` header, and confirm a test bounce arrives | F17 | `SuppressedAddress_NotMailed`; `configuration-reference.md` `MailSuppression__IngestToken` | |
| Create the provider-side T1–T3 alarm rules (bounce rate, complaint rate, volume spike) pointing at a real monitored mailbox, and run the cross-check drill | F18 | the T4 volume alarm in `MailAlarmsService` / `MailAlarmsHandler`; the F17 precondition (`SuppressedAddress` list) | |
| Run the E2E job in CI against a **live** compose stack (the runner starts `app` + `mailpit`), and open the ADR 0050 signup gate on the live instance | F19 | `e2e/specs/signup_verify_login.e2e.ts`, `e2e/specs/post_write.e2e.ts`; ADR [0050](../adr/0050-signup-gate.md) | |
| Run the one-step rollback drill once on a throwaway prod-style instance (the A → B1 → B2 → C → verify sequence) and stamp the "last drilled" date | F20 | `docs/OPS.md` §5 (the one-step rollback runbook + the "last drilled" line); F4 PITR precondition | |
| Enable Postgres TLS on the server **before** flipping `DbTls__SslMode` to `Verify CA`, then provision + distribute the CA bundle (`DbTls__CaPath`) and re-provision on CA rotation | F21 | `configuration-reference.md` `DbTls__SslMode` / `DbTls__CaPath`; the F21 U3 pristine-boot evidence | |
| Provision `kumunita_migrate` as DB + `mt` / `identity` owner **before** demoting the app role, then demote the app role to DML (keeping the `mt` schema-level `CREATE` exception) and set `ConnectionStrings:KumunitaMigrate` | F22 | `AppRole_CannotDropSchema`; ADR [0171](../adr/0171-least-privilege-db-role.md); `configuration-reference.md` `ConnectionStrings__KumunitaMigrate` | |
| Decide the account-enumeration question (uniform response timing / a decision to accept the risk) — **parked, no owner decision yet** | L1/U28 | the SEC U28 parking note (`sec-handoff-notes.md` `## Summary`); the login / verify seams | |
| Decide the log-forging question (user-supplied strings reaching `ILogger`) — **parked per the SEC deferred list** | L8c | the SEC L8c deferred note (`sec-handoff-notes.md` `## Summary`); the log-PII gate (the PII half is closed; the forging half is not) | |
| Pin the base images by digest — `node:22` (Dockerfile stage 1), `postgres:18` + `axllent/mailpit:latest` (`docker-compose.yml`) — resolved on a machine with Docker Hub pull access | to-pin (SEC U25) | the SEC U25 "to pin" note (`sec-handoff-notes.md`); the `Dockerfile` + `docker-compose.yml` | |
| Review the `Retention__*` day-counts (sessions, tokens, outbox body + row, dead letters, notifications read/unread, error reports) for this instance | IS33–IS35 | `configuration-reference.md` `Retention__*` (the D6 keys, ≈ lines 92–99) | |
| Rehearse the full-instance-export runbook end-to-end (break-glass elevation → full-scope export → resident transparency entry) | IS30 | [privacy-operations.md](privacy-operations.md) "Full instance export" section; ADR [0299](../adr/0299-instance-export-operator-gated.md); `FullExport_NotifiesEveryResident` | |

## Related

- [Hardening checklist](hardening-checklist.md) — the "what must be true" slice this ledger records against.
- [Configuration reference](configuration-reference.md) — the exact env keys the rows name.
- [Data & access operations](privacy-operations.md) — the break-glass + full-export runbook the last row points at.
