---
id: adr-0297
title: "IMPROVE-CODE-3 C3-07 — the 3 flagged-accidental boundary edges: 2 contract extractions + the 4 name-collision retires (boundary_violations 465 → 459)"
tier: reference
kind: adr
status: proposed
audiences: [developer, agent, platform-maintainer]
related:
  - docs/adr/0296-improve-code-3-c3-06-marten-allowlist-remainder-honest-finding.md
  - docs/adr/0290-improve-code-2-baseline-raises-and-seams.md
  - docs/archive/lanes/improve-code/plan-improve-code-3.md
  - docs/archive/lanes/improve-code/improve-code-3-handoff-notes.md
  - docs/philosophy/in-code.md
supersedes: []
superseded_by: []
updated: 2026-11-06
---

# ADR 0297 — IMPROVE-CODE-3 C3-07: the 3 flagged-accidental boundary edges

Status: **Proposed** (this ADR is the named record for C3-07 — D5 of the
`IMPROVE-CODE-3` plan, the 3 flagged-accidental boundary edges named in
ADR 0290 for C2-03 and never landed because C2 closed held. Each edge is a
concrete cross-module type where a contract extraction (an interface) would
retire it. **Two edges are real concrete-type crossings** and are resolved by
extracting an interface in the source module (the gate's allowed-target rule:
an interface is a named contract, not a concrete type crossing). **The third
edge is a name-collision false positive** — the gate's syntax-only name
heuristic conflates the ubiquitous Core `TagSlugs` *property* with the
Web-layer `TagSlugs` *parse-helper type* — and is resolved by giving the Web
parser a distinct name. All three retire cleanly (the `boundary_violations`
line moves **down** 465 → 459; **no baseline entry is raised** — the new
contracts are under every ceiling and the two are *interfaces*, so they add no
ceiling line and mint no edge). The `adr: 0290` field on each retired edge is
dropped with it (the gate's stale-entry check passes: the edge is gone).)

## Context

ADR 0290 (C2-00) recorded 30 baseline raises, of which C2-03 named the 9
concrete-`*Service` crossings that were later retired (ADR 0291) and left the
3 **flagged-accidental** boundary edges for a successor to extract as
contracts. The plan's D5 names the resolution for each: *"Each of the 3 is a
concrete cross-module type where an interface / `*Policy` / `*Keys` would do.
The contract extraction retires the edge (the gate count drops); the
`adr: 0290` field is dropped with it."*

The gate mechanic (the arbiter, `tools/code/CodeMetrics`): a **syntax-only
name heuristic**. A file's module is its first directory under
`src/Kumunita.Core/` (or `Web`); an edge `A→B:Symbol` exists when a file in
module A contains the *simple name* of a type *defined* in module B (B ≠ A).
A target is **allowed** (no edge) when it is an interface, a record, an enum,
or a static class named `*Policy` / `*Keys` / `*Kinds`. The baseline
`count` = the number of *files* in the edge; a baseline entry must exist
(else "stale") and `current > max` → "grew"; any current edge not in the
baseline → "NEW". Tightening (lowering / removing) is always allowed.
Comments and doc `cref`s are trivia (not counted).

The three edges (measured at the C3-06 close `2bae8552`, each with `adr: 0290`):

| # | Edge | The accidental concrete type |
|---|---|---|
| 1 | `Notifications → Identity : VerificationOptions` (count 2) | `NotificationService` + its DI bound `IOptions<VerificationOptions>` and read `.Value.BaseUrl` directly |
| 2 | `Portability → Identity : AppDbContext` (count 3) | `PortabilityService` + `PrincipalsExport` + its DI used the EF Core `AppDbContext` (the Identity tables, ADR 0004 §B) directly |
| 3 | `Projects → Web : TagSlugs` (count 4) | a **false positive** — see below |

## Decision

**Edge 1 — a contract extraction: `IInstanceBaseUrlReader` (Identity).**
The Notifications module's only cross-module need is a single value — the
instance's public base URL (`VerificationOptions.BaseUrl`), used to turn the
same-origin relative `LinkPath` into the absolute link the notification email
carries. The concrete `VerificationOptions` policy type is the accidental
crossing. Extract the read contract in the Identity module (the source
module): `IInstanceBaseUrlReader { string? BaseUrl { get; } }` + a small
`InstanceBaseUrlReader` adapter that reads the value from the host-bound
`IOptions<VerificationOptions>`. `NotificationService`'s ctor seam changes
from `IOptions<VerificationOptions>?` to `IInstanceBaseUrlReader?` (the
CS1736 trailing-param idiom is preserved, so the test harnesses that omit the
seam keep compiling); the module's DI resolves `IInstanceBaseUrlReader`; the
Identity module registers the adapter (the source module owns both the source
options and the contract). The 2 test sites pass
`new InstanceBaseUrlReader(Options.Create(new VerificationOptions { BaseUrl = … }))`
instead of the raw `IOptions<VerificationOptions>`. The edge retires
(interface = an allowed target); the `Bootstrap→Identity` and `Web→Identity`
`VerificationOptions` edges are **legitimate** (Bootstrap owns the seeder;
the Web host binds the options) and stay.

**Edge 2 — a contract extraction: `IIdentityPrincipalStore` (Identity).**
The Portability module's cross-module needs over the EF Core identity-schema
context are exactly two: read the principal list (the no-secret
`principals.json` export source) and wipe the principals (the clean-slate
import's dependent-order delete of the four user-dependent Identity tables +
`AspNetUsers`, with the change-tracker clear). The concrete `AppDbContext`
context type is the accidental crossing — ADR 0004 §B intended the EF Core
context to be a *private detail of the Identity module*. Extract the contract
in the Identity module: `IIdentityPrincipalStore { GetUsersAsync(ct);
WipeIdentityPrincipalsAsync(ct) }` + an `IdentityPrincipalStore` adapter that
wraps `AppDbContext` (the dependent-order wipe — the ADR 0193 no-cascade
discipline — moves verbatim into the adapter). `PrincipalsExport.ExportAsync`
now takes the pre-fetched `IReadOnlyList<User>` (the caller reads it through
the contract) instead of the `AppDbContext`; `PortabilityService`'s ctor
seam changes `Identity.AppDbContext` → `IIdentityPrincipalStore` and the
`WipeForCleanSlateImportAsync` identity block (the dependent-order delete +
tracker clear) becomes a single `WipeIdentityPrincipalsAsync(ct)` call. The
Identity module registers the adapter. The edge retires; the existing
`Portability→Identity:User` edge (ADR 0293, count 6) already covers returning
the `User` list, so the extraction mints **no new edge**.

**Edge 3 — break the name collision: rename the Web parse-helper type.**
The plan's framing ("a Core module referencing a Web-layer concrete type") is
**inaccurate for this edge** — verified against the tree: the 4 Projects
"references" are all the `TagSlugs` **DTO property** (`IReadOnlyList<string>?
TagSlugs` on `BoardRequest` / `TodoRequest` etc.), **not** a reference to the
Web `TagSlugs` *parse-helper type*. The gate is a syntax-only *name* heuristic:
the ubiquitous Core `TagSlugs` property name (a natural DTO field name — the
`PostDraft.TagSlugs` precedent) name-collides with the Web-layer
`Kumunita.Web.Security.TagSlugs` static parse-helper *class*, producing a
false-positive `*→Web:TagSlugs` edge for **four** modules (`Projects`, plus
the non-flagged `Bootstrap`, `Documents`, `Posts` — all the same collision).
No `using Kumunita.Web` exists anywhere in Core (Core is HTTP-free, ADR 0006-D)
and no Core file calls `TagSlugs.Parse`; the type is a Web-internal
form-parsing helper (the composer's `TagIds` JSON-array field → clean slugs).
The honest fix — which retires **all four** false-positive edges at once and
leaves the (correct) Core DTO property name untouched — is to give the Web
parser a distinct name that cannot collide with the Core DTO word:
`TagSlugs` → **`TagSlugParser`** (the class definition, its 8 Web controller /
model call sites `TagSlugs.Parse` → `TagSlugParser.Parse`, the `TagSlugs.cs`
→ `TagSlugParser.cs` file rename, and the doc `cref` / comment / TS / JS /
CSHTML prose references). This is a pure rename of a single Web-internal type
— no behaviour change, no new edge, no ceiling movement.

## Consequences

- **`boundary_violations` 465 → 459.** Six edge entries retire: the 3
  flagged edges (`Notifications→Identity:VerificationOptions`,
  `Portability→Identity:AppDbContext`, `Projects→Web:TagSlugs`) + the 3
  bonus name-collision false positives the rename clears
  (`Bootstrap→Web:TagSlugs`, `Documents→Web:TagSlugs`, `Posts→Web:TagSlugs`).
  The `adr: 0290` field on each retired edge is dropped with it (the
  stale-entry check passes — the edge is gone, not "tightened").
- **No baseline line is raised.** The two new contracts are *interfaces*
  (an allowed target — they add no `classes` / `methods` line and mint no
  edge) and are each well under the interface ceiling (2 methods); the
  `IdentityPrincipalStore` adapter (a `class`, 2 public methods) and the
  `InstanceBaseUrlReader` adapter (1 member) are under the `cs_file` /
  `classes` ceilings, so no `files` / `classes` entry moves. `files` 8 ·
  `classes` 3 · `interfaces` 8 · `methods` 89 · `allowlist` 17 are all
  **unchanged**.
- **The `*Translation*` translation-lane line (DoD #5, 39 ≤ 40), the
  concrete-edges line (DoD #6, 9 named, ADR 0291), the two-untested-services
  line (DoD #7), and the E11 SEC-pointer line (DoD #8) are all untouched** —
  this unit changes none of them.
- **The `User` principal seam (ADR 0293) is unchanged** — Edge 2's contract
  returns the same `User` type the module already crosses; the edge count does
  not grow.
- **The six-member flip is still not landed** — C3-08 the close decides it on
  the true number (DoD #6's `≤ 5` allowlist target is not met: 17, ADR 0296).
  C3-07 moves the boundary line **down** and retires the last named
  flagged-accidental edges; it is a pure reduction unit (no raise, no flip).
