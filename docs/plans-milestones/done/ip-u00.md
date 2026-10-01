# IP · U00 — Docs parity + decision lock (the sign-off gate)

**You are the U00 agent.** This is a **docs-only** unit. It corrects the stale
ADR range in `ARCHITECTURE.md:84`, extends the value-chain table to cover
M18–M23, adds a one-line rationale comment to the `Milestones.cs` M23/M22
order, and locks the register's [PROPOSED] D1–D6 set verbatim (or amends it
**in U00**, recorded in the handoff notes). It does **not** write any C# into
the codebase, does **not** touch `Milestones.cs`'s milestone list, and does
**not** add a new ADR. **Exit: the files exist + the changes are in place.**
No build, no test assembly.

**Read the register first** — the Understanding, the [PROPOSED] D# set, the
invariants C-IP·1…C-IP·5, the FACES F1–F4, the §gate GATE-1…GATE-4, the
§drift-guard, and the unit-series rules. If the codebase's actual state
differs from anything in the register (an ADR number has changed, a
`StoreAuditRow` has already been refactored, a rate-limit policy already
exists), **the codebase wins for mechanics** and the register wins for the
**locked surface** (the six improvements, the zero-new-authorization-surface
pin, the docs-parity scope).

## Goal

Fix the **three** doc↔code drift items that IP's later units cite:

1. The stale ADR range in `ARCHITECTURE.md:84` (currently `0001–0053`;
   the actual range is `0001–0123`).
2. The truncated value-chain table in `ARCHITECTURE.md` (stops at M17;
   M18–M23 are shipped but have no row).
3. The unexplained `Milestones.cs` M23-before-M22 order (the test pins the
   order, but no comment in the source explains *why*).

And **lock** the register's [PROPOSED] D1–D6 set verbatim (or amend it in-
unit, recorded in the handoff notes). A post-U00 D# change is **not
allowed** (C-IP·1…C-IP·5 + the §drift-guard).

## Entry reads (6)

1. `docs/plans-milestones/plan-ip-integration-polish.md` — **the register**:
   the Understanding, the [PROPOSED] D1–D6 set (each with its `*Forbids:*`
   tail), the invariants C-IP·1…C-IP·5, the FACES F1–F4 + the named trade,
   the §gate GATE-1…GATE-4, the §drift-guard, and the unit map U00–U07.
2. `docs/ARCHITECTURE.md` — the **stale** ADR range at line 84 (the
   `0001–0053` that must become `0001–0123` or be dropped), and the
   value-chain table (which must be extended to M18–M23).
3. `docs/adr/` (the directory listing) — the **actual** ADR range
   (highest file is `0123-extended-profiles.md`).
4. `src/Kumunita.Web/Milestones.cs` — the **real** milestone list (M0–M21,
   M23, M22 — the M23-before-M22 order that needs a one-line comment),
   and the `Milestones.cs` doc-comment (which names the README as the
   source of truth).
5. `docs/plans-milestones/done/plan-m23-extended-profiles.md` §2.1–§2.2 —
   **the unit-plan format to mirror** (Goal, Entry reads, Deliverables,
   Exit — the same shape every IP unit plan follows).
6. `README.md` (the **Roadmap** section) — the **source of truth** for the
   milestone order (the `Milestones.cs` doc-comment says this is the
   contract; the value-chain table in `ARCHITECTURE.md` must match it).

## Deliverables (3)

### 1. `docs/ARCHITECTURE.md` — the ADR range correction (C-IP·4)

Find the line that reads (approximately):

```
ADR 0001–0053 — decision records…
```

(or the equivalent that hardcodes `0001–0053`). **Replace the hardcoded
range with a description that does not require updating when a new ADR is
added.** Two acceptable forms (pick the one that matches the surrounding
prose):

- **Option A (drop the range):** "ADR 0001 onward — decision records for
  every design decision, each numbered after the current highest."
- **Option B (update the range):** "ADR 0001–0123 — decision records…"
  *(This form requires a manual update every time a new ADR lands — the
  Option A form is preferred for long-term maintainability.)*

**Do not** change any other ADR reference in `ARCHITECTURE.md` (the ADR
range is the only stale one; the rest of the file is current).

### 2. `docs/ARCHITECTURE.md` — the value-chain table extension (C-IP·4)

Find the value-chain table (the one that maps `M##` → the integration
value each milestone delivers). The table currently stops at `M17`.
**Append** the following rows (one per shipped milestone M18–M23), each
matching the existing row format (a `M##` column, a **short name** column,
and a **value-chain description** column):

| M# | Short name | Value-chain (one sentence) |
| --- | --- | --- |
| M18 | Recurring events | Events that repeat on a schedule (weekly, monthly, custom) — the integration value is the *calendar seam*: a recurring event is not five separate events but one event with a recurrence rule, and the RSVP + audit + notification seams all operate on the *series*, not the instance. |
| M19 | Guest accounts | A resident can invite a non-member to participate in a specific event or post — the integration value is the *identity seam*: the platform's identity model extends from "a member of this community" to "a member, or a guest invited by a member," and the authorization model resolves both through the same `IAuthorizationService` seam. |
| M20 | Notification quiet times | A resident can set quiet hours during which notifications are deferred (not suppressed) — the integration value is the *attention seam*: the platform's notification system respects the resident's attention rhythm, and the "deferred" state is a first-class concept in the notification lifecycle. |
| M21 | Document management | The platform's internal documents (design docs, ADRs, plans) are managed in the repo with a clear lifecycle (draft → in-progress → done) — the integration value is the *process seam*: the platform's own development process is visible in the repo, and the doc lifecycle mirrors the platform's own "parts work, seams hold" philosophy. |
| M23 | Extended profiles | A resident can add a biography (rich content) and author-set tags to their profile — the integration value is the *discovery seam*: the directory is not just a list of names but a discoverable index of people by skill, interest, and expertise, and the tag/bio fields are the integration points between the profile and the post/reply/tag surfaces. |

> **Note:** M22 (the "M22" milestone — the one that is `StatusNext`)
> **does not get a row** (it is in-progress, not shipped). The value-chain
> table covers **shipped** milestones only.

### 3. `src/Kumunita.Web/Milestones.cs` — the one-line rationale comment (C-IP·4)

Add a **one-line comment** immediately before the `M23` entry in the
milestone list (the list is `M0, M1, M2, …, M21, M23, M22`). The comment
explains *why* M23 comes before M22:

```csharp
// M23 (Extended Profiles) is listed before M22 (the current StatusNext)
// because M23 was pulled forward — the platform's extended-profile surface
// (bio + tags) is a prerequisite for the M22 feature's integration value.
// The test (MilestonesTests.cs) pins this order; do not reorder.
```

*(Adjust the comment text to match the **actual** reason M23 was pulled
forward — read the M23 register / design doc / ADR 0123 to confirm the
rationale, and write the comment to match. The point is not the exact
wording; it is that a future reader of `Milestones.cs` does not have to
guess why M23 is before M22.)*

### 4. The handoff notes (U00 section) — **create** the file

**Create** `docs/plans-milestones/in-progress/ip-integration-polish-handoff-
notes.md` with the following content:

```markdown
# IP — Integration Polish: rolling handoff notes

> Every IP unit appends a `## U##` section here before it moves its own
> plan to `done/`. The file is created by U00 and moved to `done/` at U07.

## U00 — Docs parity + decision lock

**Delivered:**
- Corrected `ARCHITECTURE.md` ADR range (`0001–0053` → [dropped / updated]).
- Extended the value-chain table to M18–M23 (5 new rows).
- Added a one-line rationale comment to `Milestones.cs` M23/M22 order.
- Locked [PROPOSED] D1–D6 verbatim (or amended: [list any amendments]).

**Open questions:** [none / list any]

**Next unit:** U01 — `AccessAuditFactory` + the three `StoreAuditRow` call
sites. Entry: `src/Kumunita.Core/Authorization/AccessAudit.cs`,
`src/Kumunita.Core/Events/EventService.cs` (StoreAuditRow, ~line 1697),
`src/Kumunita.Core/Inventory/InventoryService.cs` (StoreAuditRow, line 507),
`src/Kumunita.Core/Projects/ProjectService.cs` (StoreAuditRow, ~line 3498).
```

## Exit (no build, no test assembly)

- The three doc changes are in place (verify by reading the files).
- The handoff-notes file exists with the U00 section.
- The [PROPOSED] D1–D6 set is locked (or amended) — the handoff notes
  record the final state.
- **No C# has been written.** The `Kumunita.Core` and `Kumunita.Web`
  projects are unchanged.
