---
id: adr-0293
title: "IMPROVE-CODE-3 C3-03 — the god-service seam extractions: the honest boundary-edge count raises (the new named-seam file lands on an already-existing module edge)"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/adr/0291-improve-code-2-c2-03-carry-the-9-concrete-service-crossings.md
  - docs/adr/0292-improve-code-3-c3-02-sampledataseeder-decomposition-raise.md
  - docs/archive/lanes/improve-code/plan-improve-code-3.md
  - docs/philosophy/in-code.md
supersedes: []
superseded_by: []
updated: 2026-11-06
---

# ADR 0293 — IMPROVE-CODE-3 C3-03: the god-service seam boundary-edge raises

Status: **Proposed** (this ADR is the `adr:` field named on the
`tools/code/code-baseline.json` **`boundary_violations`** entries that C3-03
**raised** — each is an *already-existing* module edge whose file-count grew by
exactly one when the extracted named-seam file (e.g. `Posts/GroupPostService.cs`)
landed in the same module and references the same cross-module type the
god-service it came out of already referenced. The gate's *"a `count` raised
above the base branch without an `adr:` fails"* rule (`CheckOriginMain`, which
honours `entry["adr"]` on boundary entries) is satisfied by this named entry.)

> **Abstract:** the named, defensible record for the boundary-edge count
> raises that the C3-03 god-service seam extractions force. Every extracted
> seam is a **real part-vs-whole surface** living in the **same module** as the
> god-service it came out of (the part-vs-whole rule), and references the same
> cross-module types its god-service already referenced (the C3-01 *"no new
> boundary edge"* doctrine — no **new** edge is minted). But a *new file* on an
> **already-existing** edge raises that edge's **file-count** by one. The gate
> (honest since C2-00) requires a `count` raised above the base branch to carry
> an `adr:` field; this ADR is that field. The intended-coupling record for each
> edge is **unchanged** — the count rises because the split minted a new file on
> the same legitimate edge (the C3-01 `Pages→Identity:Roles` 2→3 precedent, the
> C08 `Posts→Identity:Roles` split-raise precedent). **Who is it for:** the
> reviewer of C3-03's close, and any successor unit that re-examines the
> `boundary_violations` baseline. **When to read the rest:** before raising or
> retiring a C3-03 seam's boundary edge, or when a gate failure on one is the
> starting point.

## Context

C3-03 (the `c3-03-prompt.md` unit) splits the 5 god-services
(`BoardService` · `TodoService` · `PostService` · `IdentityService` ·
`UserPortabilityService`) each into a **real part-vs-whole seam with its own
unit test**. The plan's D1 + the lane's Part 2 say: *"No new boundary edge. The
extracted seam lives in the same module as the god-service it comes out of … It
references the same cross-module types the god-service already referenced.
Verify against the gate — the C3-01 boundary-grow trap (minting a file in a
module that crosses a new edge) applies here."*

The C3-01 precedent named this exact situation honestly: the `PageStandingPolicy.cs`
extraction (in the `Pages` module) referenced the `Roles` constants, adding a
**third file** to an *already-existing* `Pages→Identity:Roles` edge (was
`PageService` + `PageTranslationPolicy`). The C3-01 close resolved it by raising
the `count` 2 → 3 and **keeping its existing `adr: 0291`** (the intended-coupling
record). The C08 split-raise precedent (`Posts→Identity:Roles`,
`Projects→Identity:Roles`, `UserInfo→Identity:Roles` all carry an `adr:` for
split-induced grows) is the same move.

C3-03 hits the same class for the god-service seams. The `PostService` group-post
seam (`Posts/GroupPostService.cs`, this ADR's first landed seam) references the
`Authorization:Audience`, `Localization:LocaleSettings`, `Mentions:MentionTokens`,
`Notifications:UgcSnippets`, `Pages:Page`, `Query:PageSizer`, and
`Web:AttachmentIds` types — every one an **already-existing** `Posts→*` edge the
`PostService.cs` god-file already crossed. The new file does not mint a **new**
edge (the *from* and *to* modules + the *symbol* are unchanged); it adds one
**file** to each of the 7 existing edges. The gate's base-branch comparison
requires a raised `count` to carry an `adr:`; these 7 edges had none, so this
ADR supplies it.

## Decision

For each C3-03 god-service seam, when the extracted seam file lands in the
god-service's own module and references an already-existing `moduleA→moduleB:symbol`
edge, the `boundary_violations` entry for that edge is **raised** (the file-count
grows by one) and **named with `adr: 0293`**. The intended-coupling record
(which module legitimately talks to which, over which symbol) is **unchanged** —
the edge was already a recorded, sanctioned crossing; the count rises only
because the part-vs-whole split minted a second file on it. No **new** boundary
edge is introduced, and no new cross-module *dependency* is added (the seam
references the same types its god-service already did).

### The C3-03 boundary-edge raises (as landed; the list grows as the remaining
### seams land)

| Edge (`from → to : symbol`) | Count | Note |
|---|---|---|
| `Posts → Authorization : Audience` | 4 → **5** | the new `Posts/GroupPostService.cs` (the group-post seam) references `Audience`, as `PostService.cs` already did |
| `Posts → Localization : LocaleSettings` | 1 → **2** | the seam's `ResolveLanguageCodeAsync` reads the `LocaleSettings` singleton |
| `Posts → Mentions : MentionTokens` | 1 → **2** | the seam's create lane extracts `@`-tokens (the M36 mention shape) |
| `Posts → Notifications : UgcSnippets` | 1 → **2** | the seam's create lane truncates the UGC snippet for the notification |
| `Posts → Pages : Page` | 2 → **3** | the seam's feed reads the `Page`/`PageSizer`-adjacent feed shape |
| `Posts → Query : PageSizer` | 1 → **2** | the seam's feed uses `PageSizer` |
| `Posts → Web : AttachmentIds` | 5 → **6** | the seam's draft carries `AttachmentIds` |
| `Projects → Authorization : AccessAuditFactory` | 3 → **5** | the `BoardService` lane seam (`BoardLaneService.cs`) + the `TodoService` comment seam (`TodoCommentService.cs`) each write an `AccessAudit` row via `AccessAuditFactory`, as `BoardService.cs` / `TodoService.cs` already did |
| `Projects → Authorization : StandingMatrix` | 3 → **4** | the `BoardService` lane seam reads `StandingMatrix` (the board move lane), as `BoardService.cs` already did |
| `Portability → Identity : User` | 5 → **6** | the `UserPortabilityService` ownership-guard seam (`ImportOwnershipGuardService.cs`) reads `User` (the ownership prepass), as `UserPortabilityService.cs` already did |
| `Portability → Messaging : Conversation` | 2 → **3** | the ownership-guard seam reads `Conversation` (the actor-conversation prepass), as `UserPortabilityService.cs` already did |
| `Identity → Authorization : AccessAudit` | 1 → **2** | the `IdentityService` platform-gate seam (`IdentityPlatformGateService.cs`) writes an `AccessAudit` row (the sign-up / sample-data gate audit), as `IdentityService.cs` already did |

Each entry gains `adr: 0293` in `tools/code/code-baseline.json` (a textual edit
— the `\u003C` escapes survive; the baseline is never `json.load`/`dump`-ed).

The **collateral controller growth** the group-post re-point forces (the
`GroupsController` +1 ctor param `IGroupPostService? groupPosts` + the null-guard
on the `Detail` action) is also named with `adr: 0293` on the `files` and
`methods` entries (the C3-00 `EventController.cs` 1244 → 1249 precedent — the
seam injection's collateral, not a new god-surface).

## Consequences

- The total over-ceiling surface is **strictly smaller** (the extracted seam is
  under every ceiling; the god-service's `files` excess shrinks; 0 silent
  raises).
- The `boundary_violations` **count** for the named edges rises by one each — an
  honest *file-count* reflection of a real new file on an already-sanctioned
  edge, not a new crossing. The **set** of edges is unchanged (no new edge).
- A successor unit that further splits one of these modules and lands a third
  file on an edge already named by this ADR may raise the count again under this
  same `adr: 0293` (the intended-coupling is unchanged) or name a new ADR if the
  *symbol* changes.
