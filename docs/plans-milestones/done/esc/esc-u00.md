# ESC · U00 — Kickoff verification + handoff-note skeleton

**You are the U00 agent.** This is a **docs-only** unit. It verifies the M31 /
M32 `ErrorReports` surface ESC reuses (the `ErrorReport` doc, the
`IErrorReportService` 4-method surface, the M32 `EscalationForwarder` +
`IEscalationForwarder`, the `AccessVia` enum, the `/admin/error-reports`
list / detail / resolve / escalate, the M31/M32 `errorreport.*` / `issue.*`
`kw-l` key sets) + confirms **ADR 0159** is free, and authors the
handoff-note skeleton (the "Lane open" section). **No code, no build, no
test.**

**Read the register first** —
`docs/plans-milestones/plan-esc-escalation-authorization.md` (the
Understanding, the "one thing" section ESC·1–ESC·13, the Assumptions, the
Approach, the Workflow, the unit-series rules). It locks the **19-member ESC
ceiling** (the M32 15 unchanged + the ESC 4 additive; ESC·2), the
`EscalationToken` + `EscalationOutboundConfig` doc shapes (ESC·3 / ESC·4),
the `IEscalationTokenService` seam (ESC·3 / ESC·9), the `AcceptInboundAsync`
seam (ESC·5 / ESC·7), the `EscalationForwarder` `Bearer`-token + DB-config
change (ESC·6 / ESC·4), the `AccessVia.Escalation` append (ESC·10), the closed
`escalation.*` `kw-l` key set (ESC·11), and the ADR 0159 number.

## Goal

Verify the surface + author the handoff-note skeleton (the "Lane open"
section). **No code, no build, no test.**

## Entry reads (verified — findings recorded, nothing changed)

1. `docs/plans-milestones/plan-esc-escalation-authorization.md` — the
   register (the Understanding, the "one thing" ESC·1–ESC·13, the
   Assumptions, the Approach, the Workflow, the unit-series rules).
2. `src/Kumunita.Core/ErrorReports/ErrorReport.cs` — the M32 doc (the
   **15-member M32 ceiling** ESC extends additively to the **19-member ESC
   ceiling**; confirmed `Origin` is a `string` with the closed set
   `{"error-page","general"}` + the `ResolvedAt`/`ResolvedBy`/`ResolutionNote`
   shape ESC mirrors for the four additive fields; `TriageStatus` stays a
   `string`).
3. `src/Kumunita.Core/ErrorReports/IErrorReportService.cs` — the M31 3 + the
   M32 `MarkResolvedAsync` **4-method surface** ESC adds the 5th
   (`AcceptInboundAsync`) to.
4. `src/Kumunita.Core/ErrorReports/ErrorReportDraft.cs` — the M31/M32 draft
   record (7 members incl. `Origin`); confirms ESC's `InboundReport` is a
   **separate** record, not a re-shape.
5. `src/Kumunita.Core/ErrorReports/ErrorReportService.cs` — the M32 impl — the
   `MarkResolvedAsync` idempotent-write-lane shape ESC mirrors for
   `AcceptInboundAsync`.
6. `src/Kumunita.Core/ErrorReports/ErrorReportDocTypes.cs` — the M31
   registration (one `.Schema.For<ErrorReport>()` + the (TriageStatus,
   Created) indexes); confirms the ESC `EscalationDocTypes` surface is a
   **new** parallel surface (the Usage `UsageDocTypes` precedent), not an
   edit of this one.
7. `src/Kumunita.Web/Services/EscalationForwarder.cs` +
   `IEscalationForwarder.cs` — the M32 forwarder (the `ForwardAsync(reportId)`
   **1-method seam**, the 8-field payload `{ id, subjectId, description,
   contactEmail, requestId, exceptionType, origin, created }`, the
   `EscalationResult(Configured, Success, StatusCode, Error)` record) — the
   impl ESC modifies for the `Bearer` token + the DB-config read + the
   env-var fallback.
8. `src/Kumunita.Core/Authorization/Decision.cs` — the `AccessVia` enum (the
   **12 values** ESC appends `Escalation` to; confirmed 12: Owner, Audience,
   Delegation, Moderator, Report, BreakGlass, Admin, Group, Guardian,
   Community, Resident, Anonymous).
9. `src/Kumunita.Web/Controllers/ErrorReportAdminController.cs` — the M31/M32
   admin surface (the `[Authorize(Roles = GlobalAdmin)]` gate + the optional
   ctor-param precedent + the **5 actions** ESC's received filter + detail
   fields mirror: `Index` + `MarkTriaged` + `Detail` + `Resolve` +
   `Escalate`).
10. `src/Kumunita.Core/Localization/KnownTranslationKeys.cs` — the M31/M32
    `errorreport.*` / `issue.*` keys (confirmed the **`escalation.*`
    namespace is FREE** — the M32 `errorreport.escalate.*` set is a *different*
    namespace; a repo-wide grep for `"escalation.` finds no `kw-l` key).
11. `docs/adr/README.md` — the ADR index (confirmed **0159** is free after the
    0158 row; 0158 = surface-label-translation `Accepted — **Done** (LBL-2)`;
    no 0159 / 016x row exists).

**Confirmed counts (the register's U00 exit pins):**

- The `ErrorReport` field count — **15** (the M32 ceiling ESC extends to 19).
- The `IErrorReportService` method count — **4** (ESC adds the 5th).
- The `AccessVia` value count — **12** (ESC appends the 13th).
- The `EscalationForwarder` payload field count — **8**; the forwarder seam is
  **1 method** (`ForwardAsync`).
- The `ErrorReportAdminController` action count — **5** (`Index` +
  `MarkTriaged` + `Detail` + `Resolve` + `Escalate`).
- The `escalation.*` `kw-l` namespace — **FREE**.
- **ADR 0159** — **FREE** (0158 is the current highest).

## Deliverables (2 files, new)

- `docs/plans-milestones/in-progress/esc-u00.md` — this unit plan.
- `docs/plans-milestones/in-progress/esc-handoff-notes.md` — the **skeleton
  only** (the header + the "Lane open" section + the
  `<!-- U00 appends its section below this line. One ## section per unit, in
  order (U00, U01, … U09). Never rewrite a prior section. -->` marker). The
  skeleton mirrors the `m32-handoff-notes.md` shape (the "Lane open" section
  names the register, the design doc, the ADR, the scope, the out-of-scope
  deferrals, and the frozen base — the M32 `ErrorReport` doc is **reused**
  (ESC·1), the M32 `EscalationForwarder` is **extended** (ESC·6), the
  `KUMUNITA_ESCALATION_ENDPOINT` env var is the **fallback** (ESC·4), the
  `AccessVia` enum is the ESC·10 append target, and the `escalation.*` `kw-l`
  namespace is the ESC·11 new surface) + the `## U00 — Kickoff verified`
  section.

## Exit

- The handoff-note skeleton is present. The `## Lane open` section names (a)
  the M32 `ErrorReport` doc (the 15-member ceiling ESC extends additively to
  19 — the ESC·1 / ESC·2 pins), (b) the M31/M32 `IErrorReportService` 4-method
  surface (the ESC·1 pin — ESC adds `AcceptInboundAsync`), (c) the M32
  `EscalationForwarder` (the ESC·6 `Bearer`-token extension target), (d) the
  `KUMUNITA_ESCALATION_ENDPOINT` env var (the ESC·4 fallback), (e) the
  `AccessVia` 12-value set (the ESC·10 append target), (f) the **ADR 0159**
  (the next free number after 0158 — the ADR index confirms 0158 is the
  current highest).
- Handoff note: a `## U00 — Kickoff verified` section with the current M32
  surface shape (the `ErrorReport` field count [15], the
  `IErrorReportService` method count [4], the `AccessVia` value count [12],
  the `EscalationForwarder` payload field count [8], the
  `ErrorReportAdminController` action count [5: `Index` + `MarkTriaged` +
  `Detail` + `Resolve` + `Escalate`]), the `escalation.*` `kw-l` namespace
  **free** flag, the ADR number (0159) + the precedent ADR list (0004 §B.1
  [additive fields + parallel doc surfaces], 0001-B [thin-token], 0006-C3
  [single-write-lane], 0006-D [Core HTTP-free], 0154 [the M31 surface], 0155
  [the M32 surface ESC reuses + evolves]).
- `git status` clean except the two new files.
