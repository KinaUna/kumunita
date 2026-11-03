---
id: audit-log
title: "Audit log"
tier: reference
kind: security
status: current
audiences: [developer, platform-maintainer, community-admin, agent]
related:
  - docs/SECURITY-AUDIT.md
  - docs/security/audit-checklists.md
  - docs/security/audit-annual-trigger.md
  - docs/security/decisions-open-items.md
updated: 2026-11-02
---

> **Abstract:** the append-only record of every completed audit (or failed
> check that became a lane) — no PII, checklist IDs and a one-line summary
> only — plus the SEC remediation lane's per-finding rows. **Who is it for:**
> the auditor appending a row, and the reviewer checking "did we do the
> thing?". **When to read the rest:** before running an audit (append the
> row after); when tracing a finding back to its lane/ADR.

## Audit log

Append-only, one row per completed audit (or per failed check that became a
lane). No PII — checklist IDs and a one-line summary only.

| Date       | Scope (section + cadence)          | Who        | Result (pass / findings)        | Follow-up (lane/ADR) |
|------------|------------------------------------|------------|---------------------------------|----------------------|
|            | e.g. `C+D — release 2026-09-XX`    |            | pass / `C-8: Deny row missing`  |                      |
| 2026-10-31 | `C — SEC remediation (audit of `main` @ `bdcaff7`)` | dev | 14 fixed · 1 n/a · 2 parked (see lane rows below) | SEC lane; `6dafb783` |

### SEC remediation lane — per-finding rows (2026-10-31)

Closed in the SEC lane ([`archive/lanes/sec/`](../archive/lanes/sec/plan-sec-remediation.md)), all
remediation commits through `6dafb783` (the U29 acceptance gate, **PASS**).
No PII — test class names and lane IDs only.

| Finding | Result | Fixed in (unit) | Regression test(s) |
|---------|--------|-----------------|--------------------|
| H1 — import trusts archive rows | fixed | U4–U6 (guard → classify → resolve) | `ImportOwnershipGuardTests` (10), `UserPortabilityHostileClassifyTests` (4), `UserPortabilityHostileResolveTests` (5) |
| H2 — GlobalAdmin 2FA is a redirect | fixed | U7–U8 (`AdminTwoFactorMiddleware`, registered post-auth) | `AdminTwoFactorMiddlewareTests` (7) |
| H3 — forwarded headers no-op | fixed | U9–U10 (`ForwardedHeadersSetup` + `Secure` cookie policy) | `ForwardedHeadersSetupTests` (4); the cookie-policy wiring test is waived per design §2.3 ([`60-verify`](../deploy/coolify/60-verify.md) checklist is the evidence) |
| M1 — seven POSTs unvalidated | fixed | U11 (no-op, verified) + U12 (global filter + 2-token allow-list) | `AntiforgeryCoverageTests` (2) |
| M2 — zip bomb + un-gated import media | fixed | U13–U14 (`ArchiveLimits`, import media gate, `upload` limiter) | `KumunitaArchiveLimitsTests` (4), `UserPortabilityMediaGateTests` (3) |
| M3 — anonymous writes unthrottled | fixed | U15 (limiter + size cap + `[MaxLength(4000)]`) | `AnonymousWriteLimitsTests` (3) |
| M4 — `/health` public-by-default | fixed | U16 (minimal-by-default + constant-time compare + SMTP probe cache) | `HealthControllerTests` (9, incl. 4 new) |
| M5 — revocation fails open | fixed | U17–U18 (stamped sign-ins + fail-closed after cut-off) | `VerifySignInStampTests` (1), `RevocationStampFailClosedTests` (4) |
| L1 — account enumeration | **parked** | U28 — no `## Decision — L1` (2026-10-31) | — (parked; unit text stands ready) |
| L2 — upload type client-trusted | fixed | U20–U22 (`UploadSignatureSniffer`, tri-state `Mismatch`-only wire, owner decision 2026-10-31) | `UploadSignatureSnifferTests` (3), `UploadSniffAttachmentImageTests` (2), `Avatar_MismatchedBytes_Rejected` + `Document_MismatchedBytes_Rejected` |
| L3 — verify token / GET signs in | fixed (U23 **n/a**) | U23 n/a (dual-compare load-bearing in verify-by-Id lanes, owner decision 2026-10-31) + U24 (GET confirm page, POST signs in) | `VerifyConfirmStepTests` (3); `IdentityServiceTokenHashTests` untouched |
| L4 — markdown link filter | fixed | U19 (backslash/control-char reject, `mailto:` allowed) | `MarkdownRendererTests` (11, incl. 3 new) |
| L5 — container/CI hardening | fixed | U25 (non-root, SHA-pinned actions + MCR images, `permissions:`) | no tests — `docker build` EXIT 0 + workflow YAML parse (build/container surface) |
| L6 — compose publishes Postgres | fixed | U26 (ports bound to `127.0.0.1`, dev-only banner) | no new tests (dev-only surface; existing suites green) |
| L7 — HSTS scope / hosts / headers | fixed | U26 (1-year HSTS, `Permissions-Policy`, `WellKnown:BaseUrl`) | `F13U2_SecurityTxtTests` (existing, harness updated) |
| L8 — limiter exemption / smalls | fixed (L8c **parked**) | U27 (IP-only partition, exemption removed; L8b break-glass is n/a — parameterised SQL, no C# compare) | `LoginLimiterPartitionTests` (2) |

**Follow-ups owed (not fixed in the lane):** L1/U28 parked; L8c log-forging
parked (see [open items](decisions-open-items.md#open-items)); `node:22` / `postgres:18` /
`axllent/mailpit:latest` still "to pin" (Docker Hub blocks anonymous digest
resolution — U25); operator live-config checks in [checklist D](audit-checklists.md#checklist-d-deployment);
dedup-by-hash keeps the first uploader's declared type (U22 flag — a
follow-up decision, not a finding); the M11 operator-lane
`PortabilityService.ImportAsync` has no `InvalidDataException` handler
(U14 `Noticed`, outside the resident-lane scope).
