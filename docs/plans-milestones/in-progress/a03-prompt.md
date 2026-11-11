---
id: a03-prompt
title: "Prompt: IMPROVE-UX A03 — the reversible lever"
tier: living
kind: plan
status: current
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0024-author-soft-delete-lane.md
  - docs/adr/0006-module-boundary-contracts.md
  - docs/adr/0004-data-persistence-and-schema-evolution.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing IMPROVE-UX
> **A03** — the third unit of the **admin/moderator** block and the
> reversible lever that completes the moderation loop (A01–A06). A02 made
> the resolve lane execute the outcome (`Hidden` → `HidePostAsync`,
> `Removed` → `RemovePostAsync`); **A03 adds the `UnhidePostAsync` write
> lane** (Moderate-gated, audited, the inverse of `HidePostAsync`) and the
> **"Unhide" button on the resolved report page** so a moderator who hid a
> post can reverse the decision without a fresh report. Before A03, the
> only way to unhide a post was to edit the `Post` document directly in
> Postgres (the operator's SQL seam, ADR 0142 D5) — there was no in-app
> surface. **Who is it for:** the next coding agent (fresh context). **When
> to read the rest:** when starting A03 (A02 is shipped, the write lane
> exists, the reads honour `PostStatus`, no blockers).

# Fresh-context prompt: IMPROVE-UX A03

You are continuing the Kumunita repository on `development` (HEAD
`eac34a57`, A02 landed — the resolve lane executes the outcome:
`Hidden` → `HidePostAsync`, `Removed` → `RemovePostAsync`, in the
caller's session, post-targets only). The **entire resident block
(R01–R21) is shipped**; A01 + A02 are landed. **A03** is the next
unit. The plan card (in `plan-improve-ux.md`, the A03 row):

> A03 — `UnhidePostAsync` (Moderate-gated, audited) + "Unhide" on the
> resolved report page; tests.
> Read first: `PostService.cs:1209-1250`.
> **Done when: hidden → active round trip.**

## The state of the code (measured 2026-11-11, `development`)

- **`HidePostAsync`** (`PostService.cs:1252`) — the M3b `Moderate`-gated
  write lane (F3). Takes `(postId, actorId, IDocumentSession session)`,
  runs `CanAsync(Moderate)`, and (if allowed) writes
  `Post.Status = PostStatus.Hidden` + `Post.Modified = UtcNow`. One
  `SaveChangesAsync` (C3 same-transaction). Unit-tested at
  `PostServiceTests.cs` M3b·9/M3b·10. **`UnhidePostAsync` is its
  mirror** — same signature, same gate, but writes
  `Post.Status = PostStatus.Active` (the inverse).
- **`RemovePostAsync`** (`PostService.cs:1290`) — the M3b `Moderate`-gated
  write lane (F4, the hard lane). `UnhidePostAsync` does **not** undo a
  `Removed` post (that is irreversible by design — the moderator's
  `Other` outcome + note is the record; the only way back is a fresh
  post or a DB restore).
- **The resolve page** (`Views/Moderation/Resolve.cshtml`) — the R04
  form (outcome radio + note textarea) is shown when
  `Model.IsResolvable` (Status is `filed`/`assigned`/`unlocked`). When
  `Status == "resolved"` **no action form is shown** (the view's last
  block). A03 adds a **fourth** form block: shown when the report is
  `resolved` **and** the outcome is `Hidden`, offering the "Unhide"
  action.
- **`ModerationResolveViewModel`** — the view model for the resolve page.
  It currently has `Status` (string) but **no `Outcome` field**. A03 adds
  `ReportOutcome? Outcome` so the view can conditionally render the
  Unhide form.
- **`ModerationDetailReadModel`** (`ModerationReadModels.cs:101`) — the
  Core read seam that the controller maps to the view model. It also
  currently has **no `Outcome` field**. A03 adds `ReportOutcome? Outcome`.
- **`ModerationService.GetDetailAsync`** (`ModerationService.cs`) — the
  read seam that populates `ModerationDetailReadModel`. It reads the
  `Report` doc (which has `Outcome`) but currently does not copy it to
  the read model. A03 adds the copy.
- **`IModerationService`** (`IModerationService.cs`) + the
  `ModerationService` partial — A03 adds a thin
  `UnhidePostAsync(string reportId, string actorId)` surface (the
  controller's single facade) that resolves the report's `PostId` and
  delegates to `_posts.UnhidePostAsync` (the R05 optional seam —
  production DI always wires the `PostService` via
  `GetRequiredService`, so it is non-null; guard it like the A02 hook
  does).
- **`ModerationController`** (`ModerationController.cs:113`) — the
  Web surface. It injects **only** `IModerationService` (the C09
  single-facade seam — no `PostService` / `groupPosts` here). It already
  has `Index` (GET queue), `Resolve` (GET detail + three forms), `Assign`
  (POST), `Unlock` (POST), `ResolvePost` (POST resolve). A03 adds
  **`Unhide`** (POST `/moderation/{id}/unhide`) — GlobalAdmin-gated the
  same way `ResolvePost` is (the **action-level** `[Authorize(Roles =
  Roles.GlobalAdmin)]`; the class carries only the bare `[Authorize]`),
  which **goes through `IModerationService`** (a thin
  `UnhidePostAsync(reportId, actorId)` method there that loads the
  report's `PostId` and delegates to `_posts.UnhidePostAsync`) —
  matching the controller's single-facade shape, not injecting
  `PostService` directly.
- **`PostService`** — `UnhidePostAsync` is a **new method** on this
  class (the write lane lives in the Posts context, the same as
  `HidePostAsync` / `RemovePostAsync` — not on `ModerationService`).
  The `ModerationService` already holds `_posts` (`Posts.PostService?`,
  the R05 optional seam); the Web `Unhide` action reaches it through
  the `IModerationService.UnhidePostAsync` seam (above) so the
  controller stays single-facade — **do not** inject `PostService`
  directly into `ModerationController` (unlike the group-post surface,
  which uses its own controller).

## The work

1. **`UnhidePostAsync` in `PostService.cs`.** Mirror `HidePostAsync`
   (`:1252`) exactly, except:
   - writes `post.Status = PostStatus.Active` (not `Hidden`);
   - the doc comment says "Unhide a post (A03; the inverse of
     `HidePostAsync` — restores a hidden post to the feed; does
     **not** undo a `Removed` post, which is the hard lane's
     irreversible shape)";
   - the `KeyNotFoundException` message says "nothing to unhide";
   - everything else (the `CanAsync(Moderate)` gate, the audit row
     shape, the C3 same-transaction `SaveChangesAsync`) is the same.

2. **The `Outcome` field on the read model + view model.** Add
   `ReportOutcome? Outcome` to:
   - `ModerationDetailReadModel` (`ModerationReadModels.cs:101`) —
     `public ReportOutcome? Outcome { get; init; }`
   - `ModerationResolveViewModel` (`Models/ModerationResolveViewModel.cs`)
     — `public ReportOutcome? Outcome { get; set; }`
   - `ModerationService.GetDetailAsync` — copy `report.Outcome` to the
     read model (the `Report` doc already has the field; the read
     seam just needs to read it).
   - `ModerationController.Resolve` — copy `detail.Outcome` to the
     view model.

3. **The `IModerationService.UnhidePostAsync` seam + the Web action.**
   Add a thin `UnhidePostAsync(string reportId, string actorId)` method
   to `IModerationService` (`IModerationService.cs`) + implement it in
   `ModerationService.cs` (the committed-overload shape the
   `ResolveReportAsync` committed overload uses — open its own
   `_store.LightweightSession()`, load the `Report` for the `PostId`,
   delegate to `_posts.UnhidePostAsync(report.PostId, actorId, session)`,
   one `SaveChangesAsync`). Then add **`Unhide`** to
   `ModerationController` (mirroring `ResolvePost`'s shape —
   `[HttpPost("{id}/unhide")]`, `[ValidateAntiForgeryToken]`,
   GlobalAdmin-gated). Call `moderation.UnhidePostAsync(id, admin)`,
   flash "Unhidden." and redirect to `/moderation/{id}`. Catch
   `KeyNotFoundException` → `NotFound()`.

4. **The "Unhide" form block in `Views/Moderation/Resolve.cshtml`.**
   After the existing `@if (Model.IsResolvable)` block (which is
   hidden when `Status == "resolved"`), add a **fourth** block:

   ```csharp
   @if (Model.Status == "resolved" && Model.Outcome == ReportOutcome.Hidden)
   {
       // "Unhide the post" form — the A03 reversible lever.
       // Shown only for a resolved report whose outcome was Hidden
       // (a Removed post is irreversible by design; a NoAction / Other
       // outcome has nothing to reverse).
   }
   ```

   The form is a simple `POST /moderation/@Model.ReportId/unhide` with
   a single button. The labels use new translation keys (en/de/fr/da,
   machine-written for de/fr/da, flagged for the parked native review).

5. **Tests** (in `PostServiceTests.cs`, mirroring M3b·9/M3b·10):
   - `UnhidePostAsync_Moderator_WritesStatusActive_ViaTagIsAdmin` — a
     hidden post (planted with `Status = PostStatus.Hidden`) flips to
     `PostStatus.Active`; the `moderate` audit row is committed;
   - `UnhidePostAsync_NonModeratorCaller_Denied_NoStatusWritten_NoPartialState`
     — a caller without a standing moderator is denied; the post's
     `Status` stays `Hidden`; the `Deny` audit row is committed;
   - (optional, the round-trip pin) `UnhidePostAsync_AfterHide_HiddenToActive`
     — `HidePostAsync` then `UnhidePostAsync` in sequence: the post
     ends at `PostStatus.Active` (the A03 DoD "hidden → active round
     trip" pin).

6. **Verify the gates.** `dotnet build Kumunita.slnx`, run
   `PostServiceTests` (all M3b tests + the new A03 tests), and
   `python3 tools/ux/ux-measure.py --check` (the Unhide form adds no
   tracked count; `bare_status` stays at its current value).

## Scope rules

- **`UnhidePostAsync` + the Outcome read-model plumbing + the Web action
  + the view form + the tests.** Do not touch `HidePostAsync` /
  `RemovePostAsync` / the `ReportOutcome` enum / the `Report` doc /
  the resolve form (R04) / the A02 hook. Do not add `UnhidePostAsync`
  to `ModerationService.ReportIntake.cs` (that is the resolve lane;
  the Unhide action is a **separate** Web surface — a new form on the
  resolved report page, not a new form on the resolve form).
- **One commit** (the house rule): `feat(ux): UnhidePostAsync + the
  Unhide button on the resolved report page (IMPROVE-UX A03)`.

## Files that should change

- `src/Kumunita.Core/Posts/PostService.cs` (the `UnhidePostAsync` lane)
- `src/Kumunita.Core/Moderation/ModerationReadModels.cs` (the `Outcome` field)
- `src/Kumunita.Core/Moderation/ModerationService.cs` (the `GetDetailAsync` copy + the `UnhidePostAsync` seam impl)
- `src/Kumunita.Core/Moderation/IModerationService.cs` (the `UnhidePostAsync` seam declaration)
- `src/Kumunita.Web/Models/ModerationResolveViewModel.cs` (the `Outcome` field)
- `src/Kumunita.Web/Controllers/ModerationController.cs` (the `Unhide` action)
- `src/Kumunita.Web/Views/Moderation/Resolve.cshtml` (the Unhide form block)
- `src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/moderation.json` (the new translation keys)
- `tests/Kumunita.Core.Tests/PostServiceTests.cs` (the A03 tests)

(≤ ~5 Core + Web files + translation JSON — the unit-size rule.)

## Before committing

```bash
dotnet build Kumunita.slnx -c Debug
python3 tools/ux/ux-measure.py --check   # must stay green
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.PostServiceTests" -method "*Unhide*"
# + the existing M3b write-lane tests (confirm no regression):
dotnet exec tests/Kumunita.Core.Tests/bin/Debug/net10.0/Kumunita.Core.Tests.dll \
  -class "Kumunita.Core.Tests.PostServiceTests" -method "*HidePostAsync*"
```

If a test fails, read the error, adjust, continue. Then report:
the `UnhidePostAsync` method location (file:line in `PostService.cs`),
the Web action route + file:line, the translation keys added, the
test names + pass counts, the `ux-measure.py --check` line, and the
commit hash. Do not commit unrelated work.
