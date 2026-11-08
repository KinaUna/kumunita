---
id: security-data-classes
title: "Data classes"
tier: reference
kind: security
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/SECURITY.md
  - docs/security/trust-boundaries.md
  - docs/adr/0011-media-and-file-storage.md
  - docs/adr/0028-guardian-controls-account-scope-supervision.md
  - docs/adr/0034-file-attachments-posts-replies-announcements.md
updated: 2026-11-02
---

> **Abstract:** the five data classes (PII, private content, audit & moderation,
> secrets, media/attachment bytes) — how each is stored and what its loss means —
> plus the rule that the audit log is itself a disclosure surface. **Who is it
> for:** anyone deciding what may be stored, served, or logged. **When to read
> the rest:** before adding any new stored data class — add a row here, in the
> [control map](control-map.md), and re-decide the open item in
> [decisions-open-items](decisions-open-items.md) before it ships.

## 3. Data classes

How each class is stored and what its loss means.

| Class | Contents | Stored where | Sensitivity if leaked |
|-------|----------|--------------|------------------------|
| **(a) PII** | names, emails, phones (opt-in), profiles, households | `mt` (Profile…), `identity` | High — identifies real neighbors; phone/email is contact-targetable |
| **(b) Private content** | audience-restricted posts, replies, RSVPs, project members | `mt` | High — private conversations; disclosure breaks community trust permanently |
| **(c) Audit & moderation** | `AccessAudit`, `Report`, `AdminOverride` | `mt` | **High, asymmetric** — reveals *who accessed what*, incl. *denied* items (see §3.1) |
| **(d) Secrets** | DB/SMTP credentials, seed token, backup keys | env / secrets manager / object store | Critical — full instance compromise |
| **(e) Media & uploaded bytes** | raster avatars (jpeg/png/webp/gif) — identifying images of residents — and the `MediaObject` catalog (hash id, original filename, size, first-storer) | bytes: dedicated volume ([`backups`](../ops/backups.md)/[`restore`](../ops/restore.md), second restore surface); catalog: `mt` ([ADR 0011](../adr/0011-media-and-file-storage.md)) | High — a face is identity material; served **only** through an auth + audit-gated app endpoint (never a static path), raster-only (SVG excluded as a script vector) |
| **(e) Attachment files (ADR [0034](../adr/0034-file-attachments-posts-replies-announcements.md), lane `ATT`)** | user-attached files (PDF / Office docs / text / csv / zip / raster) on posts, replies, announcements — arbitrary resident content — cataloged by the **same** `MediaObject` doc on the **same** volume as (e) above | same volume + same `mt` catalog as (e); the owning doc carries `AttachmentIds` (additive, zero migrations, [ADR 0004 §B.1](../adr/0004-data-persistence-and-schema-evolution.md)) | High — same as (e): served **only** through the auth + audit-gated `GET /attachment/{id}` endpoint (never a static path), a **separate** file allowlist (SVG excluded, `Media__AttachmentAllowedContentTypes`), `Content-Disposition: attachment` + `nosniff` (a download, not an inline render — C-ATT·2/8), every miss a 404 (orphan-safe — C-ATT·7) |

**The per-type policy lives in code, not just here.** The five classes above
are the *vocabulary*; the authoritative per-type row — class, erasure, export
scope, and retention key — is carried on the registered type itself by the
[`[DataClass]` attribute](../adr/0298-data-classes-in-code.md) (ADR [0298](../adr/0298-data-classes-in-code.md))
and tabulated in the 81-row table at
[design §2.5](../design/improve-sec-design.md). Types added after this prose
was first written — **sessions** (`SessionDoc`), the **email outbox**
(`OutboxEmail`), and **group messaging** (`GroupConversation` / `GroupMessage`)
— are governed the same way: their class and erasure are set by their
`[DataClass]` row and listed in design §2.5.

### 3.1 The audit log is a disclosure surface (accepted by design)

`AccessAudit` records **denials with `targetId`**, and list views record `hiddenCount`.
A GlobalAdmin reading the log can therefore enumerate *which* restricted items exist and
*who was kept out of them*. That is a deliberate property — it is what makes the audit
log an accountability mechanism ("prove what happened", [`privacy-operations`](../ops/privacy-operations.md)) — but it means:

- The **audit log itself must be treated as class (c)**: GlobalAdmin-only, retained per
  the tiered policy ([`ARCHITECTURE.md`](../ARCHITECTURE.md) §5), pseudonymized on account deletion.
- Do not "fix" it by dropping `targetId` or denials — that removes the accountability
  the community is promised.

**"Minor" is not a stored data class.** A child's status as a supervised account is
carried by the `GuardianLink` row ([ADR 0028](../adr/0028-guardian-controls-account-scope-supervision.md)), not by a stored age or birthdate — so the
guardian lane adds a *control* without adding a sensitive PII field. This keeps rule 5
(minimize: "what we don't store, we can't leak") intact even as the feature lands.
