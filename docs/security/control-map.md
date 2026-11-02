---
id: security-control-map
title: "Control map"
tier: reference
kind: security
status: current
audiences: [community-admin, platform-maintainer, developer, agent]
related:
  - docs/security/adversaries.md
  - docs/security/decisions-open-items.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/adr/0011-media-and-file-storage.md
  - docs/adr/0034-file-attachments-posts-replies-announcements.md
  - docs/adr/0172-password-hash-posture-pbkdf2-identity-default.md
updated: 2026-11-02
---

> **Abstract:** every control on the platform, each linked to the adversary it
> answers, the code seam that enforces it, and the ADR that decided it — the
> "does it cover this threat?" self-check. **Who is it for:** the developer
> adding a feature and the auditor verifying a control holds. **When to read
> the rest:** before adding a data class or privilege surface — add a row here
> and in the [adversaries](adversaries.md) table before merge.

## 5. Control map — every threat must be covered

Quick self-check when adding a feature: does it introduce a new data class or a new
privilege surface? If so, add a row here and in the relevant checklist.

The **Decided by** column names the code seam (the file / class that enforces the
control) and the ADR that decided it — the stable keys a later change must not
silently break. Where a control is an owner-ops step (a flag, a daemon, a key to
set) rather than code, the seam is the runbook that documents it.

| Control | Answers | Decided by (code seam + ADR) |
|---------|---------|------------------------------|
| Deny-by-default audiences + empty-audience-denies | A1, A3 | [`IAuthorizationService.CanAsync(Read)`](../../src/Kumunita.Core/Authorization/AuthorizationService.cs) (`Kumunita.Core/Authorization`); the audience invariant in [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.3–4.4 |
| Thin token, fat authorization (no privilege in cookie) | A3 | [ADR 0001](../adr/0001-core-stack-and-identity-model.md)-B; [`AuthorizationService.cs`](../../src/Kumunita.Core/Authorization/AuthorizationService.cs) resolves "may this actor see that?" per request |
| Security-stamp rotation on privilege change | A3 (revoked grants) | `SecurityStampValidatorOptions` in [`Program.cs`](../../src/Kumunita.Web/Program.cs); operated per [`hardening-checklist`](../ops/hardening-checklist.md) |
| Optimistic concurrency + atomic audit commit | A3 (lost-update abuse) | the audit commit in the `Posts` / `Moderation` bounded contexts; [`ARCHITECTURE.md`](../ARCHITECTURE.md) §5 |
| Rate limiting (the eight fixed-window policies: `login` / `signup` / `resend` / `report` / `setup` / `message` / `write` / `upload`), per-IP | A2 | `AddRateLimiter` + `RateLimitingSetup` in [`Program.cs`](../../src/Kumunita.Web/Program.cs); mechanism in [`decisions`](decisions-open-items.md#decisions); endpoints + XFF in [`hardening-checklist`](../ops/hardening-checklist.md); the `upload` leg is F8 (the 2026-10-25 fix pass) |
| Identity account lockout, per-account (defaults) | A2 | `IdentityOptions` lockout in [`Program.cs`](../../src/Kumunita.Web/Program.cs); pairs with rate limiting in [`hardening-checklist`](../ops/hardening-checklist.md) |
| **2FA (TOTP): required for GlobalAdmin**, recommended for Moderator | A3, A4 | [`AdminTwoFactorMiddleware`](../../src/Kumunita.Web/Security/AdminTwoFactorMiddleware.cs) (registered in [`Program.cs`](../../src/Kumunita.Web/Program.cs) after `UseAuthentication`); decided in [`decisions`](decisions-open-items.md#decisions); break-glass step [`incident-response`](../ops/incident-response.md) |
| Content-Security-Policy `script-src 'self'` (strict, no inline script) + origin pinning (shipped) | A3 (XSS) | `app.Use(…)` CSP block in [`Program.cs`](../../src/Kumunita.Web/Program.cs); decided in [`decisions`](decisions-open-items.md#decisions); shipped in [`hardening-checklist`](../ops/hardening-checklist.md) |
| PBKDF2 password hashing (Identity default; iteration count documented) | A2, A3 | ASP.NET Identity default (no custom `PasswordHasher`); posture recorded in [ADR 0172](../adr/0172-password-hash-posture-pbkdf2-identity-default.md) |
| CAPTCHA (deferred, documented decision) | A2 | the `KUMUNITA__SIGNUP__BOT_CHECK` flag in [`bot-challenge.ts`](../../src/Kumunita.Web/client/lib/bot-challenge.ts) (off by default); owner deferral in [`hardening-checklist`](../ops/hardening-checklist.md) |
| Report-gated, audited moderator access | A4 (and protects A3's privacy from mods) | [ADR 0003](../adr/0003-roles-and-moderator-scoping.md); the `Moderation/` bounded context |
| Two-admin rule + operator-only break-glass | A4 | [ADR 0003](../adr/0003-roles-and-moderator-scoping.md); [`ARCHITECTURE.md`](../ARCHITECTURE.md) §4.5; operated per [`incident-response`](../ops/incident-response.md) |
| One-time seed token, removed from env after first boot | A5 | the first-boot procedure in [`first-boot`](../ops/first-boot.md) |
| Non-superuser DB, internal-only | A1, A5 | [ADR 0171](../adr/0171-least-privilege-db-role.md); the dev-db-init scripts; [`hardening-checklist`](../ops/hardening-checklist.md) |
| Encrypted offsite backups + verified restores | A5, disaster | [`backups`](../ops/backups.md) + [`restore`](../ops/restore.md) (the restore drill is the evidence) |
| Pinned image + packages | A6 | the SHA-pinned `Dockerfile` + `package-lock.json`; supply-chain decision in [`decisions`](decisions-open-items.md#decisions) |
| Guardian standing is creation-based, account-scope (no content read), and community-reversible | A7 | [ADR 0028](../adr/0028-guardian-controls-account-scope-supervision.md) G·4/G·1/G·5; the content path has no `Via: Guardian` branch; GlobalAdmin dissolve/un-suspend audited `Via: Admin` |
| SPF / DKIM / DMARC, stable From: | phishing-as-us (A3 impersonation) | the SMTP runbook in [`email-smtp`](../ops/email-smtp.md) |
| Audit log (always-on, tiered retention) | accountability for A3, A4 | the `AccessAudit` doc (Marten, [`Kumunita.Core`](../../src/Kumunita.Core/)); tiered retention in [`ARCHITECTURE.md`](../ARCHITECTURE.md) §5 |
| Media served only through an auth + audit endpoint (never static; one frozen `CanAsync(Read)` call commits the Allow **and** Deny rows) | A1 (scraping), A3 | [ADR 0011](../adr/0011-media-and-file-storage.md) (C-MED·1/2/3); the media serve endpoint in `Kumunita.Web`; design doc [`media-file-storage-design.md`](../design/media-file-storage-design.md) §2.3 |
| Upload type/size allowlist at the edge — raster only (SVG = excluded script vector), 10 MiB cap, self-only write lane | A3 (payload-as-XSS, self-impersonation of others) | [ADR 0011](../adr/0011-media-and-file-storage.md) (C-MED·5/8); `Media__*` keys in [`configuration-reference`](../ops/configuration-reference.md) |
| **Attachment lane (ADR [0034](../adr/0034-file-attachments-posts-replies-announcements.md)):** served only through `GET /attachment/{id}` (never static); the owning post / reply (parent post) / announcement's **single** `Read` decision; **every miss a 404** (store-miss, orphan, Deny) — existence never leaks; **one `Deny` row** on a UGC (post/reply) Deny, **zero rows** on every other 404 (announcement ⇒ zero rows); `Content-Disposition: attachment` + `nosniff` + stored `Content-Type` (a download, not an inline render); a **separate** file allowlist (SVG excluded); the `AttachmentIds` id is derived **server-side** from the body (never client-sent); a removed link ⇒ inert bytes (orphan-safe, never hard-deleted) | A1 (scraping), A3 (payload-as-XSS, self-impersonation) | [ADR 0034](../adr/0034-file-attachments-posts-replies-announcements.md) (C-ATT·1–10); design doc [`file-attachments-design.md`](../design/file-attachments-design.md); `Media__AttachmentAllowedContentTypes` in [`configuration-reference`](../ops/configuration-reference.md) |
| Upload virus scan (F16): the `IVirusScanner` seam + a quarantine flag on the `MediaObject` doc + a serve-lane gate, **fail-open by default** (`VirusScan__Host` / `VirusScan__Port` unset ⇒ the seam is absent), `InfectedUpload_NotServed` | A3 (malicious payload) | F16; the ClamAV daemon is an owner-ops step in [`hardening-checklist`](../ops/hardening-checklist.md); design doc [`prod-hardening-f16-scan.md`](../design/prod-hardening-f16-scan.md) |
| Global security-header floor (F7): every response carries `X-Content-Type-Options: nosniff` + `Referrer-Policy`, set in the same `app.Use(…)` block as the CSP (the 2026-10-25 fix pass added them; pinned by `F7F8_HardeningGateTests`) | A3 (MIME sniffing, referrer leakage) | F7; the `app.Use(…)` block in [`Program.cs`](../../src/Kumunita.Web/Program.cs); [`hardening-checklist`](../ops/hardening-checklist.md) |
