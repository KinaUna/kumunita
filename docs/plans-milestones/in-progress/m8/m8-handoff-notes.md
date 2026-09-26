# M8 handoff notes

One `## U#` section per unit, **appended, never rewritten** (the shared
scratch tier of the three-tier contract — see the plan header). Each entry:
files written/touched, decisions locked or refined (with the design-doc /
ADR section it maps to), anything that drifted from the plan's text, and the
next unit's entry reads.

## U00 — design doc + ADR 0091

- **Files written:** `docs/design/m8-search-design.md` (all sections:
  Context / §2 verified-reuse list / D1–D8 locked / C-M8·1–7 / F1–F6 /
  Parts affected / Rollout / Risks / the three acceptance tests + Part 2:
  the exact C# of `SearchModels.cs` / the per-surface candidate predicates /
  the audit-row shape / the 24 pinned test names / the 11 `kw-l` keys /
  drift-guard with 3 entries) + `docs/adr/0091-search.md` (Accepted,
  2026-09-26) + one index row in `docs/adr/README.md`.
- **ADR number:** **0091 confirmed free** (index ran 0001–0090; `0091`
  appeared only in the M8 plan text).
- **Veto window closed:** D1–D5 **confirmed by the user 2026-09-26** (the
  register's "Open veto" block is retired; D6–D8 were never open —
  they follow from D1–D5 + the frozen-surface discipline).
- **Drift log (3 entries, design doc §2.6):** (1) the plan's D5 ADR-0018
  quotation corrected to the exact text (decision unchanged); (2) the
  audit row locked to the aggregate shape with `TargetKind =
  "search:<surface>"`, `TargetId = null` — the plan's D7 sketch had also
  named `TargetId` as the surface carrier, which would have broken the two
  stored `AccessAudit` shapes (verified on the doc at
  `Authorization/AccessAudit.cs`); (3) `Via` locked to the dominant
  standing enum value (`Audience` / `Group`) — the plan's D7 sketch
  wrote `Via: "service"`, which is not an `AccessVia` value (the frozen
  enum verified at `Authorization/Decision.cs`).
- **Verified against the actual files (U01's copy-from list):** the
  `PostService.ListFeedAsync` candidate predicate + early-return +
  `CanSeeAsync` shape (`Posts/PostService.cs:87`); the announcement
  predicate (`Announcements/AnnouncementService.cs:87`); the
  `AccessAction` record shape (`Authorization/AccessAction.cs` —
  `Action = AccessAction.Read.Id` in the audit row); the `AccessVia` /
  `AccessOutcome` frozen enums (`Authorization/Decision.cs`); the ADR 0018
  exact text (Context + Decision + Consequences).
- **No code touched:** nothing under `src/` or `tests/` was modified; no
  build was run. **U01 entry reads:** the design doc §2 (the verified
  reuse list) + §3 D6/D7 + **Part 2 §2.1–2.4 (the exact C# — the pinned
  records, the six candidate predicates, the audit-row shape, the 14
  pinned test names)** + §4 (C-M8·1/2/3/7) + this section.

