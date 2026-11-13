---
id: a13-prompt
title: "Prompt: IMPROVE-UX A13 — the guest form's account picker (no free-text subject id)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/dev/testing-and-ci.md
lane: improve-ux
updated: 2026-11-11
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **A13** — a single admin affordance: the **guest-standing
> form** (`/admin/guests`) currently asks the admin to *type* the guest
> account's subject id into a free-text box; A13 replaces that with an
> **account picker** (a `<select>` over the existing account list) so an
> admin picks the account instead of re-keying an id. It is the **next
> unit in the Admin/moderator sequence** — the invitation lane
> (A07–A12) just closed (`A12` was its final unit), and A13 is
> independent (no dependencies). **Who is it for:** the next coding
> agent (fresh context). **When to read the rest:** when starting A13
> (A12 has shipped; nothing about guests has changed yet).

# Fresh-context prompt: IMPROVE-UX A13

You are continuing the Kumunita repository on `development` (HEAD
`7a682151`, **the invitation lane just closed** — A12, its final unit,
landed the resident "Invite a neighbour" surface + the admin approval
queue; before it A11 the resident-invite setting, A10 the acceptance
notify, A09 the `/admin/invitations` surface, A08 the `?invite=` gate,
A07 the `Invitation` doc + `IInvitationService`). A13 is a
**self-contained admin affordance change** with **no dependencies** —
it touches only the guest-standing form, not the guest write seam. The
plan card (in `plan-improve-ux.md`, the A13 row):

> A13 — Guest form: account picker instead of free-text subject id.
> Read first: `Views/AdminGuests/Index.cshtml`,
> `Views/Admin/Accounts.cshtml:41` (picker). **Done when: no free-text
> id.**

The problem it fixes: the guest form's `subjectId` field is a free-text
`<input>` ("the SubjectId of the Profile / User row for the account the
guest signs in with"). A resident-facing id is opaque; the admin has to
find it elsewhere and paste it in — a handoff, not a closed loop. A13
replaces the text box with a `<select>` of the accounts the admin can
already see on `/admin/accounts`, so they *pick* the account. The guest
write seam (`IIdentityService.SetGuestAccessAsync`) is **unchanged** —
it still receives a `subjectId` string; A13 only changes *how the admin
gets that id onto the form*.

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) and **`.github/copilot-instructions.md`** —
  the doctrine (CQRS-lite + Wolverine, ADRs before code, the ratchet, the
  comment rule, the don't-pause-mid-task rule).
- **`plan-improve-ux.md`** (this lane) — the **A13 card** + the
  "What this lane is" + DoD + the resident/admin/operator journeys (A13 is
  the "Guest — Arrive, learn the window, reach an admin" journey's admin
  half; the resident half is R20, separate).
- **`src/Kumunita.Web/Views/AdminGuests/Index.cshtml`** — the form to
  change. The target is the `subjectId` field (≈lines 25–29): a
  `<label for="subjectId">` + `<input type="text" name="subjectId"
  value="@Model.SubjectId">` + the `admin.guests.subject_hint` text. The
  **rest of the form is unchanged** — the access-window fieldset
  (`validFrom` / `validUntil`) and the allowed-surfaces switches
  (`announcements` / `events` / `directory`) all stay as they are.
- **`src/Kumunita.Web/Views/Admin/Accounts.cshtml`** (≈lines 41–46) — the
  **picker pattern to copy**: a `<select class="form-select"
  name="subjectId">` iterating `Model.Accounts` and rendering
  `<option value="@a.SubjectId">@a.Email ?? a.DisplayName ??
  a.SubjectId (unverified)</option>`. This is the "pick from the account
  list" shape A13 reuses.
- **`src/Kumunita.Web/Models/GuestAdminViewModel.cs`** — the form model
  (`SubjectId`, `ValidFrom`, `ValidUntil`, the surface switches,
  `HasStanding`). A13 needs the picker's option list here (a new
  `Accounts` / `AccountOptions` collection to seed the `<select>`);
  `SubjectId` stays (the selected value + the seed for re-render).
- **`src/Kumunita.Web/Controllers/AdminGuestsController.cs`** — the
  controller to touch. `Index(string? subjectId)` builds the model
  (A13 seeds the picker options here + keeps the `subjectId` seed);
  `Save(...)` is the **write seam call** (`identity.SetGuestAccessAsync`)
  and stays unchanged — it already accepts a `subjectId` string, which
  the picker now guarantees is a real account id. The `guests.view`
  `AccessAudit` row on `Index` (the IS36 disclosure row) stays unchanged.
- **`src/Kumunita.Web/Controllers/AdminController.cs`** (`BuildAccountDataAsync`,
  ≈line 90–130) + **`src/Kumunita.Web/Models/AdminIndexViewModel.cs`**
  (`AccountRow`: `SubjectId` / `Email` / `DisplayName` / `Verified` /
  `Blocked`) — the **account list** the picker is seeded from. This is
  the same read `/admin/accounts` (the `Accounts()` action) already
  renders, so A13 reuses it — the guest form gains a picker over the
  *same* accounts an admin can already see, not a new listing.
- **`src/Kumunita.Core/Localization/Keys/{en,de,fr,da}/admin.json`** —
  the `admin.guests.subject_label` + `admin.guests.subject_hint` keys
  (en value first, de/fr/da machine-written + flagged for the parked
  native review). A13 updates these two values (the label no longer says
  "subject id"; the hint no longer asks for a typed id).
- **`docs/dev/testing-and-ci.md`** — the xunit.v3 test-runner quirk and
  the in-process `dotnet exec` invocation.
- **`git status`** + the branch (A12's `28ba6086` + the ADR-index fix
  `7a682151` must be intact; you are on `development`).

**The one thing to get right.** A13 is **view + model + controller-seed
only** — it changes *how the admin reaches the `subjectId`*, never *what
the seam does with it*. The write seam
(`IIdentityService.SetGuestAccessAsync`), the `Save` validation
(`validUntil > validFrom`), the closed surface set (`GuestSurface.None`
floor), and the `guests.view` audit row are all **unchanged**. The picker
posts a `subjectId` that is, by construction, a real account id — the
`Save` contract is byte-for-byte the same; only the input that reaches
it is now a picker value instead of free text.

The **work**:

1. **The picker (view).** In `Views/AdminGuests/Index.cshtml`, replace
   the free-text `subjectId` `<input>` (≈lines 25–29) with a
   `<select class="form-select" name="subjectId" id="subjectId">` that
   iterates the account options the model now carries and renders
   `<option value="@row.SubjectId">@(row.Email ?? row.DisplayName ??
   row.SubjectId)</option>` (the `Accounts.cshtml:41` shape). Keep the
   `required` attribute. When re-rendering with a selected value, mark the
   matching option `selected` (`@if (row.SubjectId == Model.SubjectId)`).
   **Do not** touch the window fieldset or the surface switches.
2. **The model + seed (controller).** `GuestAdminViewModel` gains the
   account-option list (e.g. `IReadOnlyList<...> AccountOptions` — the
   `AdminIndexViewModel.AccountRow` or a small `(SubjectId, Email,
   DisplayName)` projection; prefer reusing the existing row type over
   inventing a parallel one). `AdminGuestsController.Index` seeds it from
   the **same account-listing read** `/admin/accounts` uses
   (`AdminController.BuildAccountDataAsync` or the underlying seam it
   calls — reuse the existing read, do not add a second listing seam).
   `Save` is **unchanged**.
3. **Keys (4 languages).** Update `admin.guests.subject_label`
   ("Guest account") + `admin.guests.subject_hint` (the "pick the
   account the guest signs in with; one allowance document per guest
   account" wording) in **en** first, then machine-write **de/fr/da**
   (flag for the parked native review). C02 four-language parity must
   stay green.

**Tests:**
- **Web** (a view-pin + a controller-seed pin, the house "string pin, no
  TestServer" idiom of `BookmarksControllerTests` / `InventoryControllerTests`):
  (a) the guest form **no longer renders a free-text `subjectId`
  `<input>`** and **does render a `<select name="subjectId">`** (read the
  `.cshtml` and assert the input is gone / the select is present — the
  "Done when: no free-text id" bar); (b) `AdminGuestsController.Index`
  seeds the picker with the account options (the seam read is consulted;
  the model's option list is non-empty for a non-empty account list);
  (c) `Save` is **unchanged** — the existing guest-stand tests still pass
  (the seam still receives the same `subjectId` / window / surfaces).
- If you change any `admin.guests.*` key, keep the
  `AdminGuestsControllerTests` key-parity pin (if present) green.

**Scope rules:**

- **Do not change** the guest write seam
  (`IIdentityService.SetGuestAccessAsync` / `GetGuestAccessAsync`), the
  `Save` validation, the closed surface set, or the `guests.view`
  `AccessAudit` row — A13 is an *affordance*, not a new capability.
- **Do not add** a new account-listing seam — reuse the read
  `/admin/accounts` already uses (the picker is a view over the same
  accounts). A large account list in a plain `<select>` can get
  unwieldy; if you see a genuinely large corpus, record it as a
  `// TODO ❓` (a searchable/filterable picker is a *follow-on*, not
  this unit — the card's "Done when" is *no free-text id*).
- **Do not start** A14 ("Request more space") or A15 (quota override) —
  those are separate Admin units.
- **No ADR.** A13 makes no new decision (it changes a UI affordance; the
  guest-standing model — `GuestAccess`, the window, the surfaces — is
  unchanged), so it writes no ADR and amends none. Do not fabricate one.
- **One commit** (the house rule): `feat(ux): guest form — account
  picker instead of a free-text subject id (IMPROVE-UX A13)`.
- **New strings go to en/de/fr/da** (de/fr/da machine-written, flagged
  for the parked native review). **C02 four-language parity**
  (`C02_DataAsEmbeddedResourcesTests`) must stay green.
- The **ux gate** (`tools/ux/ux-measure.py --check`) must not regress —
  A13 replaces a text box with a picker (not a tracked anti-pattern);
  confirm it before committing.
- ⚠️ **Known pre-existing reds (not yours):** the full Web suite has a
  small set of pre-existing failures that pre-date this lane (the two
  `SecGate.DataClassCoverageTests` + `ImproveHarnessTests.
  CodeCheck_Gate_Passes` — the IMPROVE-CODE Roslyn ratchet). Do **not**
  attribute these to A13 or try to fix them here; your A13 suite must be
  green.

**Before committing:** `dotnet build`, then run the narrowest tests via
the in-process runner (see `testing-and-ci.md`): your new A13 guest-form
tests + the existing `AdminGuestsControllerTests` (the guest seam still
intact) + `C02_DataAsEmbeddedResourcesTests` (four-language parity). If a
test fails, read the error, adjust, continue — a failed test is a
recoverable step, not a stopping point.

Then report: where the picker lives (the `<select>` in the guest form +
the model's account-option list) + the account-listing read you reused
(the `/admin/accounts` seam, not a new one), the keys you updated
(en/de/fr/da) and that C02 parity stayed green, the new test(s) + count,
the commit hash, and the guest seam / `Save` validation / `guests.view`
audit row you confirmed unchanged. Do not commit unrelated work.
