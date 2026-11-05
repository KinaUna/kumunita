# C2-03 — Module surface: concrete `*Service` crossings become contracts

## What you are doing

You are the **fourth** agent on the **IMPROVE-CODE-2** lane (plan:
`docs/plans-milestones/plan-improve-code-2.md`). Your unit is **C2-03**, the
first of the two **DoD #6** units (the module-surface lines). C2-00 landed the
honest ratchet gate; C2-01 + C2-02 finished the translation-lane collapse
(**54 → 39**, the `≤ 40` line met — `701c7a7f`). Your unit is **DoD #6, half
one: the concrete-`*Service` boundary crossings.**

The codebase is a modular monolith with explicit module boundaries (ADR 0006,
`docs/adr/0006-module-boundary-contracts.md`). The ratchet gate
(`tools/code/code-check.ps1`) baselines every cross-module edge that is not
already a "known" contract. A **concrete `*Service`** crossing a module
boundary is a smell: it means module A codes against module B's *implementation
class*, so any change in B breaks A. The C03 reference seam is
`INotificationEmitter` (a narrow interface a module codes against, not the
concrete `NotificationService`). **D4** says it in one sentence: *"A concrete
`*Service` crossing a module boundary becomes the interface (the C03
`INotificationEmitter` pattern) or a named `*Policy`/`*Keys`."* Your unit is
that, for the concrete-service edges — **the count drops per edge.**

**Your unit is exactly that.** For each boundary edge whose target is a
concrete `*Service`, make the crossing module code against a **contract**
(the existing `I*Service` interface where one already exists, or a *narrow*
new interface / named `*Policy` / `*Keys` where it doesn't), so the
`boundary_violations` entry for that concrete symbol **retires or drops**.
The crossing's behaviour is unchanged — only the *type it references* changes.

**You are NOT** doing C2-04 (the **Marten allowlist** — the 21
`controllers_marten_allowlist` entries are a separate unit), C2-05 (the
still-large files), C2-06 (the two untested services), C2-07 (the SEC pointer),
or C2-08 (the close). You are **not** re-opening C2-01/C2-02 (the translation
lane is done — don't touch `*Translation*`), **not** the 4-shrink god-types
(A0290's named seams — C2-05 / the successor's scope), and **not** re-opening
the C05/C07/C08 part-vs-whole splits or the doctrine rules (C00). You are **not**
touching routes, rendered HTML, document schemas, or adding a feature, and you
are **not** doing a project split (the predecessor explicitly rejected one).

**Do not re-fix the gate.** The ratchet is honest (C2-00). A `boundary_violations`
entry is **shrink-only**: you *retire* or *reduce* an entry as the edge drops;
you never raise one, and a *new* edge you must keep needs a **named `adr:`**
in the same commit (the gate fails a raise without one). If a concrete crossing
is **genuinely warranted** (a real seam that should stay concrete), **stop and
name it** — an `adr:` entry — rather than minting a ceremony interface to hide it.

## Starting state

- Repo: `/home/agent/projects/kumunita`, branch `development`, HEAD
  `701c7a7f` (C2-02 close).
- Commit style: `feat(improve-code-2): C2-03 — <short description>` (conventional
  commits, em dash, no ADR in the subject). **One edge (or one coherent group of
  edges) per commit** — the gate green after each, the entry retires as it moves.
- Read `AGENTS.md` and `.github/copilot-instructions.md` first — the doctrine.
  Read the plan: `docs/plans-milestones/plan-improve-code-2.md` — the **D4**
  decision and the **C2-03** row are your spec, and **DoD #6** (the target).
  Read **D2** (part-vs-whole — the interface must be a *real* surface, not a
  ceremony type) and `docs/philosophy/in-code.md` § "The ratchet, the split,
  the comment."
- Read the contracts: ADR `docs/adr/0006-module-boundary-contracts.md` and the
  C03 reference seam `src/Kumunita.Core/Notifications/INotificationEmitter.cs`
  (a narrow interface a *different* module codes against).
- Read the C2-02 handoff note
  (`docs/plans-milestones/improve-code-2-handoff-notes.md`) so your entry
  continues the running log in the same shape.

### The concrete starting numbers (measured at `701c7a7f` — trust these + the
### code, re-derive from `tools/code/code-baseline.json` before touching anything)

- **22** `boundary_violations` entries cross a **concrete `*Service`** (the
  target; the `symbol` field ends in `Service` but is **not** an `I*Service`).
  The other ~456 boundary entries cross **documents / value types**
  (`AccessAudit`, `Roles`, `User`, `Tag`, …) that C2-00 already resolved with a
  named `adr:` (intended coupling) — **leave those alone.**
- **Two cases among the 22** (verify each against the code — some interfaces
  exist, some don't):
  - **The `I*Service` interface already exists** (e.g. `IBoardService`,
    `ITodoService`, `IProjectService`, `IModerationService`, `IPageService`,
    `ISearchService`, `ISiteContentService`, `IAnnouncementService`,
    `IEventService`) — the crossing module just codes against the concrete
    class. The fix is a **pure re-point** to the existing interface (delete the
    concrete reference, code against the `I*Service`). No new type.
  - **No `I*Service` interface exists yet** (e.g. `PostService`,
    `NotificationService`, `DirectoryService`, `SessionService`, `DocumentService`,
    `DocumentFolderService`, `EventReminderService`, `MediaLifecycleService`,
    `NotificationFlushService`, `MailAlarmsService`, `StorageMetricsCaptureService`,
    `UsagePurgeService`, `AuditPurgeService`) — mint a **narrow** interface
    (only the surface the crossing module actually calls, the C03 pattern), or a
    named `*Policy`/`*Keys` if the coupling is a rule/constant, **or** name an
    `adr:` if the concrete crossing is genuinely warranted.
- **21** `controllers_marten_allowlist` entries — that is **C2-04**, not this
  unit. Do not touch them here.

## Reduce the concrete-`*Service` crossings (one edge / group per commit)

- **Re-derive the list** from `tools/code/code-baseline.json`
  (`boundary_violations` → `symbol` ending in `Service`, not `I…`), grouped by
  the concrete service. For each:
  - **Interface already exists → re-point.** Find the crossing module's
    reference to the concrete `*Service` (a field, a constructor param, a
    `new XService(...)`, a `static` call) and code against the `I*Service`
    instead. Where the crossing was `new XService(...)`, the DI registration
    (`Kumunita.Core/…/DependencyInjection.cs`) is where the concrete is
    registered — the crossing should receive the interface and let DI resolve
    it. The `boundary_violations` entry for that concrete symbol **drops**;
    retire or reduce it (shrink-only).
  - **No interface → mint a narrow one (or a `*Policy`/`*Keys`).** Declare the
    smallest interface the crossing needs (the C03 `INotificationEmitter`
    shape — only the members actually called, no ceremony), put it in the
    **owned** module, code the crossing against it, register the existing
    concrete against the interface. **Part-vs-whole:** the interface must be the
    true cross-module surface; if carving a narrow interface would split a
    service into a ceremony shell, **say so and name it** (an `adr:`) rather
    than forcing the split.
  - **Update** the DI registration, any test that constructs the concrete
    directly for this seam, and the `boundary_violations` baseline entry
    (retire it if the edge is gone; reduce `count` if it drops). A new entry
    you must keep gets a **named `adr:`** in the same commit.
- **One edge per commit; the entry retires as it moves.** After each, re-run
  the count of concrete-`*Service` `boundary_violations` entries — it must be
  **lower** than 22. Record before → after in the handoff note. Do not add a new
  concrete crossing to compensate.

## Definition of done (C2-03)

- [ ] **The concrete-`*Service` crossings reduced:** the 22 `boundary_violations`
      entries whose `symbol` is a concrete `*Service` (not `I*Service`) are
      either **retired** (the crossing now codes against an interface / named
      `*Policy` / `*Keys`) or **carried with a named `adr:`** (genuinely
      warranted, named per edge). The count is **< 22**; record before → after.
- [ ] **No ceremony types:** every new interface is the *true* cross-module
      surface (the part-vs-whole rule a C13 cold read would pass); anything that
      would be a split-for-a-number is **named** instead (`adr:`).
- [ ] **Behaviour unchanged:** each re-point is a type-reference change, not a
      logic change — the same method is called, the same instance is resolved
      by DI. No route, rendered-HTML, document-schema, or feature change.
- [ ] `dotnet build Kumunita.slnx -c Debug` — 0 errors. The touched seams have
      passing tests (**run** the affected suites, don't just build).
- [ ] `tools/code/code-baseline.json` — the retired / reduced
      `boundary_violations` entries updated (shrink-only; no entry raised; any
      entry you must keep carries a **named `adr:`** in the same commit).
      **Edit it textually** — a `json.load`/`dump` round-trip churns the
      `\u003C` escapes (C2-01 + C2-02 both hit this).
- [ ] `tools/code/code-check.ps1` — green on the current tree (it is the
      authoritative check — a `--report` list truncates to the top 10 per
      category, so **verify any entry decision against `code-check.ps1`**).
- [ ] `tools/docs/improve-check.ps1` — ALL GATES PASS.
- [ ] The handoff note gets a **C2-03** entry (continue the running log): which
      edges, the re-point vs. mint-vs-`adr:` decision per edge, the before →
      after concrete-`*Service` count, the baseline entries retired/reduced, and
      the exact remaining list (so C2-04 — the allowlist — and the reviewer see
      DoD #6's first half is closed).
