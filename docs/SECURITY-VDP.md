# Kumunita — Vulnerability Disclosure Policy (VDP)

Kumunita is a **self-hosted, single-community platform**: each deployment
serves one neighborhood, running its own container + Postgres behind the
instance's own TLS edge. This policy applies to **that instance** — the
resident-facing and operator-facing web surface it serves, the identity /
authentication / authorization paths, the content / media / message stores,
and the mail-out path.

We want to find and fix security problems in our own platform before
someone else does. If you believe you have found a security issue, please
report it to us privately first, in good faith, and we will work with you to
understand, confirm, and fix it.

## How to report

- **Email the security contact:** see the `Contact` field in the
  machine-readable file served at `GET /.well-known/security.txt` on the
  instance — that field is the **authoritative, always-current address**
  (it is the instance's configured support mailbox). This document states
  the process; `security.txt` carries the live address as a field.
- **Subject prefix (recommended):** `Kumunita security report — <short title>`.
- **Include, if you have it:** the affected URL / endpoint, a minimal
  reproduction (steps, request, or PoC), and the observed vs. expected
  behavior. Do **not** include live credentials, other residents' personal
  data, or bulk data — a minimal, synthetic reproduction is enough and safer.
- You do **not** need an account, and you do **not** need to be a resident of
  the instance, to report.

## What we ask (responsible disclosure)

Please give us a reasonable chance to fix a vulnerability before you
publicly disclose it. If you report in good faith, we will **not** take
legal action or seek to have your report suppressed — see the safe-harbor
below. We ask only for:

1. A private, direct report (the contact above).
2. A reasonable window for us to confirm and prepare a fix before public
   disclosure — see the timeline below.
3. No testing that is disruptive, destructive, or that accesses other
   residents' personal data beyond what a minimal reproduction requires.

## Scope

**In scope** (the Kumunita platform, as served by the instance):

- The resident-facing and operator/admin-facing web surface (identity,
  login / signup / verification, MFA, session handling, authorization).
- Content, post/reply, group, event, project, announcement, and page
  stores, and their read/write paths.
- Media / file upload and serving (byte store, catalog, lifecycle).
- The message / conversation / notification and mail-out paths.
- The instance's configuration, health, and well-known endpoints.

**Out of scope:**

- Upstream project code (ASP.NET Core, .NET runtime, Postgres, the edge
  proxy) — please report those to their respective maintainers / CVE
  processes.
- Issues that require physical access to the host, or that depend on a
  misconfiguration we do not control (the deployment is self-hosted; the
  operator is responsible for their own host, DNS, and mail-provider
  settings).
- Social engineering, phishing, or other non-technical abuse.

Note: Kumunita is **self-hosted** — each deployment is a distinct instance
with its own contact address (the `Contact` field in `security.txt`). This
VDP describes the disclosure **process** for an instance; the instance
operator (the owner of the neighborhood) is the party you report to, via
that address.

## Our commitment (response timeline)

- **Acknowledgement:** we aim to acknowledge your report within **72 hours**.
- **Triage:** we will confirm the issue and its severity within **7 days**
  where possible.
- **Fix:** we will work to prepare a fix or mitigation on a best-efforts,
  time-bound basis, and keep you informed of progress.
- **Public disclosure:** we will coordinate the timing of any public
  disclosure with you once a fix or mitigation is available.

## Safe harbor / no-reprisal

If you (a) act in good faith, (b) provide a private, direct report, and
(c) do not cause, or attempt to cause, harm to the platform or to other
residents' personal data beyond a minimal reproduction, then we **will not**
bring any legal claim against you in connection with the technical acts
performed during your testing, and we will not seek to have your report
suppressed. This safe harbor does not cover acts that are not
responsible-disclosure testing (e.g., data exfiltration, account takeover
for non-testing purposes, or other abuse).

## Out-of-band / private handling

Reports are handled privately, with access limited to the people who need
it to confirm and fix the issue. We will not share your report or your
identity with third parties without your consent, except where required by
law.

---

*This policy is part of the Kumunita platform's security posture
(`docs/SECURITY.md` describes the full threat model and access-control
rules). The machine-readable companion is served at
`GET /.well-known/security.txt` (RFC 9116).*
