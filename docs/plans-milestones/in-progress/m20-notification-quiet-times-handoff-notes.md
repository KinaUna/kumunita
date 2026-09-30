# M20 — Notification quiet times — rolling handoff notes

> The cross-unit memory (register §drift-guard, tier 3). Every unit appends a
> short `## U##` section before it moves its own plan to `done/`. A unit reads
> the `## U##` sections before it; it does not re-derive what an earlier unit
> already settled (a D# amendment, the key set, a seam shape, the deferred
> idempotency-key form).

## U00

- **Delivered (docs-only, no build/test exit):** authored
  `docs/design/m20-notification-quiet-times-design.md` (the 14-section design
  doc, §9 "Seams & contracts (Part 2)" carries the exact C# for U02–U08),
  `docs/adr/0121-notification-quiet-times.md` (Status: **Accepted**, the
  Context/Decision/Consequences/Amendments/Supersedes shape), and one ADR index
  row appended after the 0120 row in `docs/adr/README.md`.
- **The [PROPOSED] set locked as-is — no amendment** (D1–D9, C-M20·1…C-M20·7,
  FACES F1–F5, the named trade, the six GATE acceptance tests GATE-1…GATE-6, the
  closed 15-key `kw-l` set, the §drift-guard). Recorded in the design doc's
  §1.a ("None") — unlike M19's D2 array→`[Flags]`, no representation amendment
  was needed; the D2 `NotificationQuietSchedule` doc and the D6
  `LocaleSettings.QuietCheckMinutes` field are authored verbatim as the
  register's prose blocks.
- **The three cross-unit facts the later units read from here:**
  1. **The §gate (GATE-1…GATE-6)** — the milestone's acceptance criterion; each
     is a pinned test in the named unit's deliverables (design doc §11).
  2. **The closed `kw-l` key set (15 keys × en/de/fr/da = 60 strings)** —
     U06 authors the **complete** set (resident `settings.quiet_*` + admin
     `admin.quiet_*`); U06/U07 consume; U07 adds none (design doc §10).
  3. **The deferred idempotency-key form**
     `notification:{kind}:{source-id}:deferred` — the **distinct** deferred key
     (different from the emit-time `notification:{kind}:{source-id}`) so the
     outbox dedup (F10) does not collide; the flush job (U04/U05) stages a
     cleared row with this key so a cleared email is delivered **exactly once**
     (C-M20·4). U03 records it; U04/U05 consume it (design doc §6 + §7).
- **Open questions:** none. The register's [PROPOSED] set was internally
  consistent and locked verbatim.
- **Next unit entry point:** **U01** (`in-progress/m20-u01.md`) — open M20
  (`Milestones.cs` flip to `StatusNext`, `MilestonesTests` re-pin,
  README/STATUS parity). U01 is the **only** unit that opens the milestone; it
  exits on `Kumunita.Web.Tests` (the `MilestonesTests` pin). It reads the design
  doc's §0 + the register's Unit-map row for U01.
