---
id: plan-improve-docs
title: "Plan: IMPROVE-DOCS — differentiate and link the documentation"
tier: living
kind: plan
status: in-progress
audiences: [developer, agent, platform-maintainer]
lane: improve-docs
related:
  - docs/archive/lanes/improve/plan-improve.md
  - docs/philosophy/START-HERE.md
  - docs/philosophy/anti-patterns.md
updated: 2026-11-02
---

# Plan: IMPROVE-DOCS — differentiate and link the documentation

> **Abstract:** The second IMPROVE pass, docs first. The corpus (1 016 `.md`
> files, ~249 k lines) is well *differentiated* but barely *linked*: 96 real
> Markdown links in total, 978 files nobody links to, and the IMPROVE-lane
> abstract/TL;DR gates grandfathered almost the whole baseline. This lane
> (1) moves shipped-lane history to `docs/archive/`, (2) gives every doc YAML
> front-matter + a `> **Abstract:**`, (3) splits the living docs by topic
> behind per-audience entry pages, (4) caps ADRs at ~250 lines with rationale
> moved to linked design docs (or the shared `adr-handoffs.md` for ADRs with no
> dedicated design doc), and (5) turns the gates from "don't grow" into
> "shrink to zero". Units `D00`–`D14`; each is one small, self-documenting
> commit (or a short series for the mechanical batches).
>
> **Read this if:** you are about to touch `docs/`, `README.md` or `AGENTS.md`,
> or you are picking up the next `D`-unit. **Skip to:** [Units](#units).

## Decisions already taken (2026-11-02)

| # | Question | Decision |
|---|----------|----------|
| 1 | How deep may this pass go? | **Full restructure** — living docs split, ADRs trimmed, design docs consolidated where it pays, history archived. |
| 2 | Where does shipped-lane history live? | **`docs/archive/`** in this repo, references rewritten by script, a redirect map left behind. |
| 3 | What is an ADR? | **A decision record** (Status / Context / Decision / Consequences + front-matter), **~250-line cap**; longer rationale moves to the linked design doc, or the shared `docs/design/adr-handoffs.md` when there is no dedicated design doc. Patch-sized UI decisions stop getting new ADRs (they go in the lane's handoff notes). **ADR numbers are permanent** — the code cites them ~9 600 times. |
| 4 | Header format | **YAML front-matter** (machine-readable) **+ a `> **Abstract:**` blockquote** (human-readable, the IMPROVE convention). |
| 5 | How are living docs split? | **By topic, one fact in one place**, with **per-audience entry pages** that link into the topic docs. |

Open decisions are asked one at a time, at the unit that needs them (marked
**❓** below).

## Why (evidence, collected 2026-11-02 at `61a3c7e`)

| Measure | Value | Philosophy name |
|---|---|---|
| `.md` files / lines (excl. `wwwroot/lib`) | 1 016 / ~249 k | — |
| …of which shipped-lane history (`archive/lanes/`) | 615 files | part sprawl (living + history mixed) |
| Real Markdown links between docs | **96** | parts work, seams don't |
| Files with **zero inbound** links | **978** | accidental integration (findable only by grep) |
| Files with zero outbound links | 992 | — |
| Broken relative links | 3 | silent coupling |
| `docs/design/*.md` > 400 lines **without** an Abstract | **48 of 62** | signals without loops (gate `g` grandfathered them) |
| Handoff notes > 600 lines without a TL;DR | **20** | same (gate `d` grandfathered them) |
| `plan-*.md` > 600 lines without Abstract/TL;DR | 31 of 73 | — |
| Living top-level docs with an abstract | **1 of 10** (`ARCHITECTURE.md`) | — |
| ADRs by length: ≤250 / 251–400 / >400 (pre-trim) | 251 / 33 / 1 | ADRs drifting into design docs |
| ADR index rows vs files | 282 / 282 ✅ | (IMPROVE U01 held) |
| Doc-path mentions of `archive/lanes/…` | 556 | — |
| Code files mentioning `docs/…` paths | 46 (incl. `RepositoryInfo.cs` in-app links, `WhatsNew.cs`, tests reading the ADR index) | — |
| `improve-check.ps1` wired into CI | **no** (manual) | signals without loops |

The diagnosis in one line: **the parts are cheap, the linkage is rare** — the
doc layer has every part it needs, and almost none of the seams are explicit.

## Target shape

```
README.md                      front page: what, status, quickstart, "where to go next" (≤120 lines)
AGENTS.md                      agent entry: the must-knows + links (≤120 lines; it is loaded every session)
SECURITY.md                    GitHub-recognized vulnerability policy (the VDP), links to docs/security/
docs/
  README.md                    the docs map: tiers, conventions, entry pages
  DOCS-CONVENTIONS.md          front-matter schema, tiers, abstract rule, ADR cap, linking rule
  start/                       per-audience entry pages (links only, no facts)
    resident.md  community-admin.md  platform-maintainer.md  developer.md  agent.md
  architecture/                ARCHITECTURE.md split by section (hub stays at docs/ARCHITECTURE.md)
  security/                    threat model, control map, open items, audit checklists C/D/P/R
  ops/                         one runbook per procedure (provision, upgrade, backup, restore, …)
  deploy/coolify/              the Coolify guide, one step per file + troubleshooting
  dev/                         running locally, tests, CI cheatsheet, Razor doctrine, terminal/browser quirks
  product/                     features, roadmap, deferred-by-design
  philosophy/                  unchanged shape; front-matter + entry-page links
  guides/                      user-guide conventions (in-app guide registry)
  adr/                         short decision records, index generated from front-matter
  design/                      living design references (current truth per subsystem)
  archive/
    README.md                  lane index: lane → design doc → ADRs → handoff
    REDIRECTS.md               old path → new path (generated)
    lanes/<lane>/…             former archive/lanes/<lane>/…
tools/docs/                    docs-report, docs-check, link rewriter, front-matter stamper
```

### Front-matter schema (finalized in `D00`)

```yaml
---
id: ops-backups                 # unique, stable; ADRs use adr-0242
title: "Backups"
tier: living | reference | archive
kind: entry | guide | runbook | architecture | security | adr | design | plan | handoff | philosophy
status: current | proposed | accepted | superseded | archived
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related: [docs/adr/0004-data-persistence-and-schema-evolution.md]
supersedes: []                  # ADRs / design docs
superseded_by: []
lane: m3b                       # where it came from, if anywhere
updated: 2026-11-02
---
```

The abstract rule: every doc over **60 lines** carries a `> **Abstract:**`
in its first 15 lines (after the front-matter) answering *what is this*,
*who is it for*, *when should I read the rest*. The TL;DR convention for
handoff notes becomes the same rule (the name "Abstract" wins; existing
`TL;DR` blocks are accepted as aliases).

## Units

Order rationale: **harness → move → stamp → split → trim → link → close.**
The archive move goes early because it is mechanical and every later unit
links to post-move paths; gates tighten last so no unit is blocked by
a half-applied rule.

### Unit progress

- `D00` — ✅ (commit `264acf36`, 2026-11-02)
- `D01` — ✅ (2026-11-02)
- `D02` — ✅ (2026-11-02)
- `D03` — ✅ (commit `d89163f7`, 2026-11-02)
- `D04` — ✅ (commit `782eca22`, 2026-11-02)
- `D05` — ✅ (commit `b55513e5`, 2026-11-02)
- `D06` — ✅ (commit `40f390e6`, 2026-11-02)
- `D07` — ✅ (commit `46e46783`, 2026-11-02)
- `D08` — ✅ (commit `ef416bb5`, 2026-11-02)
- `D09` — ✅ (commit `518cc135`, 2026-11-02)
- `D10` — ✅ (commit `1a1bb409`, 2026-11-02)
- `D11`–`D14` — not started

### `D00` — Baseline, conventions, harness
- Write `docs/DOCS-CONVENTIONS.md` (schema above, tiers, abstract rule,
  ADR cap, "link, don't backtick" rule, "one fact, one place" rule).
- Move the live tooling out of a `done/` folder: `improve-check.ps1` /
  `improve-report.ps1` → `tools/docs/` (they would otherwise end up in the
  archive in `D02`). Add `docs-report.ps1`: abstract and front-matter coverage,
  inbound/outbound link counts, orphans, broken links, ADR length histogram.
- Commit the baseline report next to this plan (the numbers above, script-reproduced).
- **Resolved (2026-11-02):** wire `docs-check` into CI at `D14` (not now) —
  the report-only phase ends with the close gate.

### `D01` — Docs map + per-audience entry pages ✅
- `docs/README.md` (the map) + `docs/start/{resident,community-admin,platform-maintainer,developer,agent}.md`.
- Entry pages contain **links and one-line reasons only** — no facts — so
  they cannot drift. They link to *current* paths and are updated by each
  later unit as docs move (the entry pages are the seam this lane keeps green).
- Resident / community-admin pages link to the in-app guide registry
  (`docs/guides/CONVENTIONS.md`) and `philosophy/everyday-life.md` rather than
  restating them.
- **Shipped 2026-11-02:** all six files landed with front-matter + abstract;
  the D00 open decision (CI wiring) is resolved as `D14`. Entry pages link to
  the *intended* paths for the not-yet-created folders (`docs/dev/`,
  `docs/product/`, `docs/security/`) so the seams stay green as `D04`–`D08`
  run.

### `D02` — Archive move (scripted) ✅
- `git mv docs/plans-milestones/done/* docs/archive/lanes/` (keeps history/blame).
- `tools/docs/rewrite-paths.ps1` rewrites the `plans-milestones/done/…` doc and
  code references to `archive/lanes/…`; `tools/docs/fix-stale-top-level.ps1`
  resolves the handful of older references that pointed at a file as if it
  lived directly under `done/` (it actually lived in a lane folder) to their
  real `archive/lanes/<lane>/…` home; both generate / re-emit
  `docs/archive/REDIRECTS.md`.
- **Code seams checked by hand:** `RepositoryInfo.cs` (no `plans-milestones/done`
  references — nothing to fix), `WhatsNew.cs` (the one prod-hardening reference
  rewritten), `Milestones.cs` (no references), `U1PinnedApiProbe.cs` /
  `IMailerStage.cs` / `Kumunita.Web.csproj` (the `m1` references rewritten), and
  the two e2e spec comments (`e2e-m3.spec.ts`, `e2e-m4.spec.ts`). `NavMoreFoldTests`
  reads the ADR index, not the archive — untouched.
- `docs/plans-milestones/README.md` shrinks to the in-progress-lane convention;
  the done-lane conventions (three-tier contract, TL;DR/Abstract rule, gate
  note) move to `docs/archive/README.md`, which also carries the full lane
  index as real links.
- **Shipped 2026-11-02:** 614 lane files moved by `git mv` (renames, so
  `git log --follow` still resolves the original history); ~300 doc + code
  references rewritten; `docs/archive/README.md` + `docs/archive/REDIRECTS.md`
  landed; the `improve-check.ps1` / `improve-report.ps1` gates re-pointed at
  `docs/archive/lanes/`.
- **Resolved (2026-11-02):** **rely on `REDIRECTS.md` alone** — no one-line
  stub files at the old top-level lane folders. Rationale: the move is a
  `git mv` (history intact), `REDIRECTS.md` is the authoritative path map, and
  the two human-facing entry points (`docs/plans-milestones/README.md` and
  `docs/archive/README.md`) both link straight into the archive. Stub files
  would be a third, redundant place to keep in sync — the "one fact, one place"
  rule says the redirect map is the single home for old-path resolution.

### `D03` — Archive tier: front-matter, abstracts, lane index
- Stamp generated front-matter on all archived files (`tier: archive`,
  `lane`, `kind` from filename pattern).
- Abstracts for the 20 un-TL;DR'd handoff notes and 31 large unit plans
  (batched, ~10 per commit; largest first: `m3b`, `pages`, `m11`, `esc`, `m10`, …).
- `docs/archive/README.md`: one row per lane — milestone, one-line outcome,
  design doc, ADRs, handoff note — **as real links**.

### `D04` — Split `docs/OPS.md` → `docs/ops/` ✅
- One file per procedure (the 13 `###` sections map 1:1: provision, first
  boot, upgrade, backups, restore, TLS, SMTP, monitoring, privacy operations,
  hardening checklist, incident response, demo site, guide review) +
  `environments.md` + `configuration-reference.md`.
- `OPS.md` becomes the ops hub: abstract + table of procedures (when to use each).
- Watch the "Upgrade" (150 lines) and "Hardening checklist" (90 lines) sections —
  candidates to link to `docs/security/` instead of restating it.
- **Shipped 2026-11-02:** the 13 procedures + `environments.md` +
  `configuration-reference.md` landed under `docs/ops/`, each with
  front-matter (`tier: reference` / `kind: runbook`) + Abstract; `OPS.md` is
  now the 62-line hub (abstract + "when to use each" table, all real links).
  Cross-references inside the split became real relative links (e.g. the
  one-step rollback's R3 leg now *calls* `docs/ops/backups.md` /
  `docs/ops/restore.md` instead of "§4/§5").
- **Resolved (2026-11-02):** the two big sections link to the **current**
  security home (`docs/SECURITY.md` + `docs/SECURITY-AUDIT.md`), not a
  guessed `docs/security/` path — `D08` will build that folder and re-point
  these links (the "link, don't backtick" rule means the seam is green today,
  not a TODO).
- **Note on `git mv`:** the split is a *restructure* (13 sections + 2 new
  files out of one monolith, with a new hub + inter-file links + front-matter
  added), so git sees it as modify `OPS.md` + add `docs/ops/*` rather than a
  pure rename — `git log --follow` on the procedure text still resolves the
  original content in `OPS.md` history. `docs-report.ps1` exits 0; broken
  links unchanged (9); front-matter 625 → 641, abstracts 606 → 616
  (the 15 new files + the rewritten hub).

### `D05` — Split `docs/COOLIFY.md` → `docs/deploy/coolify/` ✅
- One file per numbered step (0 plan … 6 verify) + troubleshooting; step 5
  "the app" (~310 lines) splits further by concern (env vars, volumes,
  health checks, …).
- Hub: an ordered checklist linking each step; the "switch to the runbook"
  handoff becomes an explicit link into `docs/ops/`.
- **Shipped 2026-11-02:** 7 top-level step files (`00-plan` … `60-verify` +
  `troubleshooting`) + 7 `step-5/` files (`00-overview`, `51-smtp`,
  `52-data-protection`, `52a-media-volume`, `53-domains`, `54-health-check`,
  `55-deploy`) landed under `docs/deploy/coolify/`, each with front-matter
  (`tier: reference` / `kind: runbook`) + Abstract; `COOLIFY.md` is now the
  54-line hub (abstract + the ordered 0 → 6 checklist, all real links).
  Cross-references inside the split became real relative links — the
  `OPS §N` / `OPS Procedure N` mentions now *call* the matching runbook in
  `docs/ops/`, and the `§5.1` / `§5.2` / `§5.2A` / `§4` / `§2` / `§6` mentions
  became links to the sibling step files. The stale `COOLIFY.md §N` pointers in
  the living docs that *did* name a section (`adr/0131`, `adr/README`,
  `ops/configuration-reference`, `SECURITY-AUDIT`) were re-pointed at the step
  file that now owns that section (the "one fact, one place" rule); the
  `ops/environments.md` + `ops/tls-domain.md` refs that just point at the hub
  stay as-is.

### `D06` — Slim `AGENTS.md` ✅
- `AGENTS.md` is loaded into every agent session, so **its length is a
  per-session cost**. Keep: start here, shape of the code, the one thing to
  internalize, conventions, don't-pause rule, doc-sync rule — as short
  paragraphs + links.
- Move to `docs/dev/`: Razor verification doctrine, test-runner quirk, CI
  known-failure cheatsheet, CI gates and pins, Postgres diagnostics, git
  gotcha, browser quirk, terminal-command safety.
- Re-check the `.github/copilot-instructions.md` de-duplication (IMPROVE U03 /
  gate `c`) — the copilot file should *link* to the same `docs/dev/` pages.
- **Shipped 2026-11-02:** `AGENTS.md` went from 468 to 119 lines — it keeps the
  six must-know sections (start here, shape of the code, the one thing to
  internalize, conventions, don't-pause, doc-sync) and now *links* out to
  `docs/dev/` instead of carrying the doctrine. `docs/dev/` landed with a
  [`README.md`](../../dev/README.md) hub + four topic files, each with
  front-matter + Abstract: [`razor-verification.md`](../../dev/razor-verification.md)
  (the rendered-HTML rule, the known Razor traps, live-server startup),
  [`testing-and-ci.md`](../../dev/testing-and-ci.md) (the xunit.v3 test-runner
  quirk, the CI known-failure cheatsheet, the by-design gates / anti-regression
  pins), [`live-server-debugging.md`](../../dev/live-server-debugging.md)
  (the `mt_doc_` / `kumunita`-DB / `pgcrypto` conventions, the `/admin/setup`
  404), and [`agent-environment-quirks.md`](../../dev/agent-environment-quirks.md)
  (the git-state gotcha, the browser trusted-folder quirk + `.tmp/` harness,
  the WSL2/Copilot terminal-command safety rules). The doctrine moved *verbatim*
  (the traps, commands and gotchas are the evidence — they were not rewritten);
  the only re-home was "Postgres diagnostics + `/admin/setup`" (from the live-
  server section) and the four sandbox sections consolidated into one
  `agent-environment-quirks.md` (the git, browser, and terminal rules are the
  same WSL2/Copilot sandbox surface). `.github/copilot-instructions.md` now
  *links* to those `docs/dev/` pages (the U03 / gate-`c` de-dup) instead of
  restating them. Housekeeping: `docs/README.md`'s folder table points `dev/`
  at the real hub, and `start/developer.md` + `start/agent.md` gained a
  `docs/dev/` row. Verification: `docs-report` broken links 9 → 9 (unchanged),
  front-matter 656 → 661, abstracts 621 → 624; `dotnet build` 0 errors;
  Web.Tests 1249/0 failed, Core.Tests 1682/0 failed (2 expected skips);
  `AGENTS.md` 119 lines (≤120).

### `D07` — Slim `README.md` ✅
- Keep: what Kumunita is, status (one paragraph + link), principles (3 lines +
  link to philosophy), quickstart, "where to go next" (→ `docs/start/`).
- Move: Features (120 lines) → `docs/product/features.md`; Roadmap + Deferred
  → `docs/product/roadmap.md` (link to `Milestones.cs`/`STATUS.md` as the
  source of truth instead of restating it); Running → `docs/dev/running-locally.md`.
- **Shipped 2026-11-02:** `README.md` went from 441 to **95 lines** (the front
  page keeps what Kumunita is, a one-paragraph status, the 5 principles, a
  one-line "what it does", the tech stack, a short architecture + deployment
  pointer, a quickstart, and a "where to go next" block that links into
  `docs/start/`). `docs/product/` landed with
  [`features.md`](../../product/features.md) (the Features bullet list, the
  M4/M5 "see the Roadmap" notes re-pointed to the sibling) and
  [`roadmap.md`](../../product/roadmap.md) (a *pointer* — it names
  `src/Kumunita.Web/Milestones.cs` as the **source of truth** and
  `docs/STATUS.md` as the narrative, and does **not** restate the 76-line
  milestone list — the "one fact, one place" rule; it also carries the
  `## Deferred (future, by design)` list, which `STATUS.md` does not own);
  `docs/dev/` gained
  [`running-locally.md`](../../dev/running-locally.md) (the Running section,
  its `OPS.md` refs re-pointed to the `docs/ops/` runbooks). Each new file has
  front-matter (`tier: living`) + Abstract. The doc-sync contract re-pointed:
  `AGENTS.md`'s sync bullet now names `Milestones.cs` as the source of truth
  (rendered home page) and `docs/product/roadmap.md` + `docs/STATUS.md` as the
  pointing docs (no longer "README Roadmap"); `MilestonesTests.cs` (the pin) is
  untouched, and the milestone order is unchanged (the user's constraint). The
  inbound `README §Running` refs (the coolify `step-5` overview + the three
  `docs/ops/` runbooks) now link into `docs/dev/running-locally.md`. Entry pages
  updated: `start/developer.md` (a `docs/product/` row + the dev row notes
  running-locally), `start/resident.md` (the roadmap row is no longer the
  "pending D07" home); `community-admin.md` / `agent.md` already point at the
  front page + map, so unchanged. Verification: `docs-report` broken links
  9 → 8 (one `ops/` ref now resolves; the rest are pre-existing `SECURITY.md`
  / philosophy links, `D08`/`D13` scope), front-matter 661 → 664, abstracts
  624 → 627; `dotnet build` 0 errors; Web.Tests 1249/0 failed, Core.Tests
  1682/0 failed (2 pre-existing skips); `README.md` 95 lines (≤120).

### `D08` — Security docs → `docs/security/` ✅
- `SECURITY.md` split: why-first, trust boundaries, data classes, adversaries,
  control map, decisions & open items — one file each.
- `SECURITY-AUDIT.md` split: rules + cadence (hub), checklists C / D / P / R,
  annual & trigger audits, audit log.
- `SECURITY-VDP.md` → repo-root `SECURITY.md` (GitHub's recognized location
  for the vulnerability policy); keep `/.well-known/security.txt` pointing at it.
- Every control-map row links to the code seam and the ADR that decided it
  (this is the prep for the later security/privacy integration pass).
- **Shipped 2026-11-02:** the model split into six concern files under
  `docs/security/` (`why-first`, `trust-boundaries`, `data-classes`,
  `adversaries`, `control-map`, `decisions-open-items`); `docs/SECURITY.md` is
  now the model hub (abstract + a concern table, all real links). The audit
  program split into `docs/security/audit-checklists` (C/D/P/R),
  `audit-annual-trigger` (A-1…A-3 + the trigger table) and `audit-log` (the
  append-only record + the SEC remediation per-finding rows);
  `docs/SECURITY-AUDIT.md` is now the audit hub (rules + cadence, all real
  links). `docs/SECURITY-VDP.md` moved to **repo-root `SECURITY.md`** via
  `git mv` (rename history intact), gaining front-matter + an Abstract; the
  `WellKnownController` comments re-pointed at the new home, and
  `docs-report.ps1`'s top-level list dropped the two moved monoliths.
  **Name-collision watch:** the currently-broken `docs/start/*` inbound links
  (`../../SECURITY*`) re-pointed at the model/audit hubs and the root VDP —
  not left at the new root `SECURITY.md` (that's the VDP, the wrong doc);
  the `SECURITY.md §N` prose in `docs/ops/` that meant a model section re-pointed
  to the concern file that now owns it. The [control map](../../security/control-map.md)
  gained a **Decided by** column (code seam + ADR per row) — the prep for the
  security/privacy integration pass. `docs/README.md`'s folder table points
  `security/` at the real folder; `start/platform-maintainer.md`'s D08 rows link
  the real files. Verification: `docs-report` broken links 8 → 3 (the five
  `docs/start/*` → `../../SECURITY*` links resolved; the 3 remaining are the
  pre-existing `improve`-lane + template links, `D13` scope), front-matter
  664 → 676, abstracts 627 → 633; `dotnet build` 0 errors; Web.Tests 1249/0
  failed, Core.Tests 1682/0 failed (2 pre-existing skips).

### `D09` — `ARCHITECTURE.md` → `docs/architecture/`
- Keep the hub (it already has an abstract): philosophy link, stack, and a
  map of sections.
- Split the long sections: solution layout (~170 lines), identity & access
  (~140), data model (~220), CQRS/Wolverine, localization, extension seams.
- Fix the duplicate `## 9.` numbering (Localization / Testing).
- **Shipped 2026-11-02:** the six long sections split into
  `docs/architecture/` — [`solution-layout`](../../architecture/solution-layout.md)
  (the file-and-folder tree, the §2 content),
  [`identity-access`](../../architecture/identity-access.md) (thin-token /
  fat-authorization, the `IUserInfoService` + `IAuthorizationService` seam,
  the access model, the decision algorithm, the roles — the §4 content),
  [`data-model`](../../architecture/data-model.md) (the two-schema split,
  the per-context doc inventory, the conventions, the optimistic-concurrency
  rule, the audit retention — the §5 content),
  [`cqrs-lite-wolverine`](../../architecture/cqrs-lite-wolverine.md) (the
  command-handler discipline, the durable email handler, the projections,
  the scheduled jobs — the §6 content),
  [`localization`](../../architecture/localization.md) (the multilingual
  operating shape — the §9a content), and
  [`extension-seams`](../../architecture/extension-seams.md) (the deferred
  seams + the M4→M5 deferrals — the §10 content). Each carries
  front-matter (`tier: reference` / `kind: architecture`) + an Abstract.
  `docs/ARCHITECTURE.md` is now the hub — front-matter + Abstract + the
  philosophy mapping + the technology stack + the "## 2. The sections" map
  (one row per split section, all real links) + the four short sections
  that stay inline (the modular monolith, the front-end, deployment &
  configuration, and testing). The duplicate `## 9.` (Localization / Testing)
  is resolved — Localization moved to its own doc, Testing renumbered to
  §6. The `ARCHITECTURE.md §N` cross-references in the living docs
  (`docs/design/*`, `docs/adr/*`) re-pointed at the section file that now
  owns that section; the internal cross-file §-refs in the split files
  re-pointed at the sibling that owns the target. The frozen
  `archive/lanes/*` references left alone. Verified on 2026-11-02:
  `docs-report.ps1` — broken links **3** (the ≤ 3 baseline; the three are
  pre-existing `archive/` + `philosophy/` targets, none from D09), front-matter
  **683** (baseline 676), abstract **639** (baseline 634); `dotnet build` 0
  errors / 0 warnings; `Kumunita.Web.Tests` 1249 passed / 0 failed;
  `Kumunita.Core.Tests` 1682 passed / 0 failed / 2 pre-existing skips.

### `D10` — Design docs: living vs archived, abstracts, consolidation
- **Resolved (2026-11-02):** a design doc is the **current truth for its
  subsystem** — kept up to date in place, `tier: reference` / `kind: design`
  (not a lane snapshot: the ADR's `related:` and the code's citations point at
  it as the *live* seam, and "one fact, one place" wants a single maintained
  home, exactly as `D09` settled the `architecture/` split at
  `tier: reference`). The consolidation output is therefore a **per-subsystem
  hub** (the "one living reference") that links the existing per-lane detail
  docs — which stay as their own current-truth reference docs — not a content
  merge (the detail docs are capability *chains* built on a frozen base; a
  literal merge would produce >10 k-line docs, violating the DoD's "no living
  doc > 300 lines without being a hub") and not a move to `archive/` (the
  frozen lane history already lives in `docs/archive/lanes/<lane>/`).
- Abstracts + front-matter for the large docs without one (the `>60`-line gate
  target; the `48` figure was the `D00` baseline snapshot — the close gate
  `D14` counts *all* `>60`-line docs).
- Consolidation candidates (merge into one living reference, old files
  become stubs or archive):
  - `prod-hardening-f*.md` (18 files) → one hardening reference
  - `m24` + `m33` storage metrics (+ history); `m13` + `m34` analytics (+ history)
  - `events-calendar` + `events-calendar-dwm` + `m18` recurring events + `m4` events
  - `rich-content` + `rich-editor` + `wysiwyg-editor` + `inline-editor`
  - `m6` notifications + `m20` quiet times
- Each design doc links forward to its ADRs and back to its archived lane.
- **Shipped 2026-11-02:** all **83** `docs/design/*.md` files now carry
  front-matter (`tier: reference` / `kind: design` / the decision ADR in
  `related:`) and an Abstract (the 69 that lacked one, each a grounded
  3-line what / who / when drawn from the doc's own opening; the 14 that
  already had one kept it). The six consolidation candidates each became a
  **per-subsystem hub** (the "one living reference" the `Resolved` line above
  names) that links the existing per-lane detail docs — which **stay** as
  their own current-truth reference docs — plus their ADRs and archived
  lanes: [`events-subsystem`](../../design/events-subsystem.md) (M4 base +
  M18 recurring + EV-CAL + EV-DWM),
  [`rich-content-subsystem`](../../design/rich-content-subsystem.md) (RC base +
  RE + WY + IE), [`notifications-subsystem`](../../design/notifications-subsystem.md)
  (M6 base + M20 quiet times),
  [`storage-metrics-subsystem`](../../design/storage-metrics-subsystem.md)
  (M24 base + M33 history), [`analytics-subsystem`](../../design/analytics-subsystem.md)
  (M13 base + M34 history), and
  [`prod-hardening`](../../design/prod-hardening.md) (the 18 `F#` findings,
  each to its design pin + audit row). **No detail doc was deleted, merged,
  or moved** — the hubs are *links and one-line reasons* (the entry-page
  convention, extended to subsystems), so the "one fact, one place" rule
  holds: the detail doc owns its facts, the hub owns the map. Housekeeping:
  `docs/README.md`'s `design/` row now names the six hubs as the doors in,
  `start/developer.md` + `start/agent.md`'s `design/` row points at the hub
  set instead of the single sample, and this D10 Shipped note + the
  `D10 — ✅ (2026-11-02)` progress line (the hash lands in the next commit,
  matching `D00`–`D09`). Verified on 2026-11-02: `docs-report.ps1` —
  broken links **3** (the ≤ 3 baseline; the three are pre-existing
  `archive/` + `philosophy/` targets, none from D10), front-matter **683 →
  772** (+89: the 83 design docs + the 6 hubs), abstract **639 → 709** (+70:
  the 69 design docs + the 1 `>60`-line hub that needed one); `dotnet build`
  0 errors; `Kumunita.Web.Tests` 1249 passed / 0 failed;
  `Kumunita.Core.Tests` 1682 passed / 0 failed / 2 pre-existing skips.

### `D11` — ADRs: front-matter, cap, rationale out
- Stamp front-matter on all 282 (`status`, `supersedes`, `superseded_by`,
  `related`, `design`). The ADR index becomes **generated** from front-matter
  (the IMPROVE U01 gate stays, now trivially true).
- Cap relaxed to **~250 lines** (from 80): only the **34 ADRs over 250 lines**
  need trimming, largest first (1 over 400). What stays: **Status, Context (the
  forces), Decision, Consequences.** What moves out: options analysis, test
  lists, implementation notes — to the linked design doc, or to the new shared
  `docs/design/adr-handoffs.md` when the ADR has no dedicated design doc (or
  the archived handoff note if the design doc is in `archive/lanes/…`).
- New `docs/adr/TEMPLATE.md` and an "ADR or handoff note?" rule of thumb
  (an ADR decides something a future change could get wrong; a copy/CSS
  decision is a handoff-note line).

### `D12` — Linking pass
- Script: convert backtick mentions of existing doc paths and `ADR NNNN`
  references *inside docs* into real relative links (code comments keep
  `ADR NNNN` as is — the number is the stable key).
- Fix the 3 broken links.
- No generated "referenced by" sections (they churn every commit); the
  generated indexes (ADR, archive lanes) carry the reverse direction.

### `D13` — Philosophy folder fit-check
- Light: front-matter, link the entry pages, verify `templates/` and
  `examples/` are reachable, fix `templates/design-doc.md`'s broken link.
- Check `adopting-fig.md` / `in-code.md` reflect the new doc conventions
  (the docs *are* an integration seam the philosophy talks about).

### `D14` — Gates and close
- `tools/docs/docs-check.ps1` gates, with **baselines retired, not grandfathered**:
  front-matter present on every doc; abstract on every doc over 60 lines;
  ADR ≤ 250 lines; zero broken links; every living doc reachable from an
  entry page (no living orphans); nothing new under `docs/archive/` except
  at a lane close.
- Wire into CI.
- Close report: before/after table of the evidence metrics.
- **Cold-read verification by a separate agent** that has not seen the work:
  given five tasks (restore a backup, add an ADR, find why moderators can't
  see private groups, onboard a new neighborhood, fix a Razor view), it must
  reach the right doc from `docs/start/` in ≤3 hops, reading only abstracts
  until the last hop.

## Definition of done

- Every metric in the evidence table moved in the right direction, reproduced by `docs-report`.
- `README.md` and `AGENTS.md` ≤ 120 lines each; no living doc > 300 lines without being a hub.
- Every doc has front-matter; every doc > 60 lines has an abstract.
- No broken links; no living orphans; the gates run in CI.
- The cold-read verification passes.

## After this lane (not in scope)

The same lens, applied in later lanes, one at a time: **code** (long
functions and files → smaller units; the IMPROVE gates `a`, `e`, `f`),
**security & privacy integration**, **UX per audience** (resident,
community admin, platform maintainer), **integration for communities and
society**, and **developer / agent experience** in the code itself.