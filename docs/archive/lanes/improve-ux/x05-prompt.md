---
id: x05-prompt
title: "Prompt: IMPROVE-UX X05 — close F2 + F3 (the two uncertain journeys, a seeded live re-walk)"
tier: archive
kind: plan
status: done
audiences: [developer, agent]
related:
  - docs/plans-milestones/in-progress/plan-improve-ux.md
  - docs/plans-milestones/in-progress/improve-ux-handoff-notes.md
  - docs/adr/0120-guest-accounts.md
  - docs/DOCS-CONVENTIONS.md
lane: improve-ux
updated: 2026-11-13
---

> **Abstract:** copy-ready prompt for a fresh agent implementing
> IMPROVE-UX **X05** — the successor to X04 (`04b95a52`, the close of F7).
> X02's honest held close filed 7 findings (F1–F7); X03 closed F1 + F4 +
> F5, X04 closed F7. This unit closes the two remaining **uncertain
> journeys** — **F2** (J7 the guest end-date, R20) and **F3** (J10
> request-more-space, A14) — **both of which already shipped** (R20 =
> `8e13085a`, A14 = `b879dc9c`) but that X02's cold read could not see
> render, because the seed data had neither a live guest window nor a set
> quota. This is a **live re-walk with seeded data**, not a code change —
> plus the honest `## X05` handoff append. **F6** (the 6 docs-check tier
> failures in the `improve-sec` archive) is **not in this unit's scope**
> (another lane's docs). **Who is it for:** the next coding agent (fresh
> context). **When to read the rest:** when starting X05 (X04 is committed
> at `04b95a52`).

# Fresh-context prompt: IMPROVE-UX X05 (close F2 + F3 — the two uncertain journeys)

You are on `development` (HEAD **`04b95a52`** — X04's close of F7,
committed 2026-11-13). Behind it: **every unit in the lane** has shipped,
and X02's cold read filed 7 findings (F1–F7). X03 closed F1 + F4 + F5;
X04 closed F7 (the two missing ADRs, 0305 + 0306 — DoD line 6 is now 6 of
6). This unit closes the **two remaining "uncertain" journey rows** —
**F2 (J7)** and **F3 (J10)** — by walking them **live with the right seed
data**, which is the only thing X02's walk lacked.

**The one thing to get right.** R20 (the guest notice) and A14 (the
"Request more space" button) **are already in the code and green** — this
is **not** a fix and **not** a new feature. X02 marked J7 + J10 "uncertain"
because its walk rendered the guest home **anonymous** (the notice only
renders for a **signed-in guest with a live window**) and `/account/storage`
with an **unlimited** quota (the button renders **only in the quota-set
branch**). Close F2 + F3 by **seeding the data the condition needs,
re-walking the live page, and recording the before → after** in the
handoff. If either surface genuinely does *not* render under the seeded
condition, that is a **real bug** — file it honestly (do not paper over it),
and stop. **F6 is not in this unit** (the 6 docs-check tier failures are
`improve-sec` archive docs — another lane; X02/X03/X04 all deferred it).

Before writing, read (in this order):

- **`AGENTS.md`** (repo root) — the doctrine, including "Don't pause
  mid-task to check in" + "a failed tool call is a recoverable step, not a
  stopping point".
- **`improve-ux-handoff-notes.md` § X02 (J7 + J10 + the F2/F3 findings) +
  § X04** — the register to match. The F2/F3 findings are explicit:
  "either R20/A14 did not land, **or** the surface only renders under
  specific conditions and the seed data has no such resident." X05 settles
  that the **units did land** (R20 = `8e13085a`, A14 = `b879dc9c`) and the
  gap is the **seed condition**.
- **`docs/dev/live-server-debugging.md`** — the Postgres conventions
  (`mt_doc_` naming, the `postgres` vs `kumunita` database, `pgcrypto`,
  the `/admin/setup` 404-is-success rule) and how to get a live server.
  The two `dotnet run` exits 134 in the terminal log are the known crash
  pattern — use the `ConnectionStrings__Kumunita` env form the log shows.
- **`git log --oneline -8`** + `git status` — confirm `04b95a52` is intact;
  you are on `development`; the untracked prompt files under `in-progress/`
  are not yours to touch.

## The work (re-walk F2 + F3 live, then one `## X05` handoff append)

### F2 — J7 the guest end-date (R20, already shipped at `8e13085a`)

**Why X02 missed it:** the notice strip in
[`src/Kumunita.Web/Views/Shared/_LayoutNotices.cshtml`](../../../../src/Kumunita.Web/Views/Shared/_LayoutNotices.cshtml)
(the R20 block) renders **only when** `User.Identity.IsAuthenticated`
**and** the `Guest` role is present — and the `Guest` role is minted only
when `Profile.IsGuest && ValidFrom ≤ now < ValidUntil` (the `GuestAccess`
document, ADR 0120 D3). X02 walked the guest home **anonymous**, so the
strip never rendered.

**The re-walk (seed → sign in → confirm):**
1. Seed a **guest resident with a live window**: sign in as a GlobalAdmin,
   go to `GET /admin/guests` (the A13 picker now — no free-text id), pick a
   resident, set `ValidFrom` in the past + `ValidUntil` in the future, and
   settle it (`SetGuestAccessAsync` — the ADR 0120 D6 write). Or set the
   `GuestAccess` doc directly via the Postgres conventions in
   `docs/dev/live-server-debugging.md`.
2. Sign in **as that guest** (or present that session's cookies to `curl`).
3. `GET /` (or any resident page) → confirm the **guest notice strip**
   renders: the `account.guest_welcome` text **plus the window end date**
   (rendered in the guest's own zone via `<kw-dt>`, ADR 0019/0020) **plus
   the "message the admins" affordance** (`GET /issues/new`).
4. (Optional, the lapsed state) set `ValidUntil` in the past and re-render
   → the friendly "your guest access has ended" notice + the same admins
   affordance.

**Passes when:** the signed-in guest sees the window **end date** + a
working "reach the admins" link, with no "ask the admin" handoff. Record the
before (X02's "not visible, anonymous walk") → after (the rendered strip) in
the `## X05` section.

### F3 — J10 request-more-space (A14, already shipped at `b879dc9c`)

**Why X02 missed it:** the "Request more space" button in
[`src/Kumunita.Web/Views/Account/Storage.cshtml`](../../../../src/Kumunita.Web/Views/Account/Storage.cshtml)
renders **only in the `else` branch** — i.e. **only when the quota is set**
(`Model.QuotaUnlimited` is `false`). X02's walk had an **unlimited** quota
(the C-UP·5 quota-0 sentinel), so it saw the "unlimited" note and **no
button**.

**The re-walk (seed → sign in → confirm):**
1. Seed a **set quota** on a resident: sign in as a GlobalAdmin and use the
   A15 per-resident quota-override surface (`AdminStorageController`,
   `GET/POST /admin/storage`-shaped) to set a non-zero quota on a resident,
   **or** set the `CommunityStorageSettings` / the resident override via the
   Postgres conventions. (A15 = `89f51248`, already shipped.)
2. Sign in **as that resident** and `GET /account/storage`.
3. Confirm the **quota-set branch** renders: the `account.storage_remaining_note`
   text **plus the "Request more space" button** (a `POST
   /account/storage/request`).
4. (Optional) submit the button → confirm a **`StorageRequest`
   notification** is emitted to the GlobalAdmins (the `AccountController`
   `RequestMoreSpace` lane, the `StorageRequest` kind in
   `NotificationKinds.cs`) with a link to the resident's account.

**Passes when:** a quota-set resident sees a working "request more space"
button that notifies the admins in-app, with no "ask an administrator"
handoff. Record the before (X02's "not visible, unlimited seed") → after (the
rendered button) in the `## X05` section.

### The handoff append (one `## X05` section)

Append a short `## X05 — close F2 + F3 (the two uncertain journeys) —
**SHIPPED**` section to the end of `improve-ux-handoff-notes.md` (match the
register of the X02/X03/X04 sections): the two journeys (J7 + J10), that
**both units already shipped** (R20 = `8e13085a`, A14 = `b879dc9c`) and the
gap was the **seed condition** (live guest window / set quota), the re-walk
result (the rendered strip / button, before → after), and the honest note
that **the lane's journey table is now 10 of 12 walked + 2 infra-verified
(J12), with F6 the only remaining open item** (another lane's `improve-sec`
archive docs).

## Front-matter + commit

- **One commit** (the house rule): `docs(ux): X05 — re-walk J7 + J10 with
  seeded data, close F2 + F3 (IMPROVE-UX X05)`.
- Commit **only** the `## X05` handoff append in
  `improve-ux-handoff-notes.md`. If the re-walk surfaced a **real bug** in
  R20 or A14, that fix is a separate `fix(ux):` commit — but do **not**
  bundle an unrelated code fix into this doc commit. Do **not** commit the
  untracked prompt files under `in-progress/` (not yours to touch).

**Scope rules:**

- **F6 is NOT in this unit.** The 6 docs-check tier failures are
  `docs/archive/lanes/improve-sec/*` docs — another lane's archive. X02/X03/
  X04 all deferred it; do the same.
- **This is a live re-walk, not a code change.** R20 + A14 are already
  shipped and green. You are *verifying the render under the right seed
  data* and *recording the honest before → after*.
- **If the surface does not render under the seeded condition**, that is a
  real bug — file it honestly in the `## X05` section and stop. Do not
  force a "pass".
- **One commit** (the handoff append). The re-walk is the evidence; the
  note is the record.

**Before committing:** re-confirm the two journeys pass on the running
instance (the rendered strip + button, with the seeded data); then
`dotnet build` (green — confirm you did not touch code) + `pwsh
tools/docs/docs-check.ps1` (the **6 F6 failures are pre-existing and not
yours to fix** — note them, don't fix; confirm your handoff append adds no
new failure) + `python3 tools/ux/ux-measure.py --check` (green — counts
unchanged). Read your `## X05` section to confirm the before → after is
honest and the F6 deferral is stated.

Then report: the two journeys' re-walk result (J7 + J10, before → after),
the two "both units already shipped" commit refs (R20 `8e13085a`, A14
`b879dc9c`), the docs-check + build + gate result, the commit hash, and a
one-line summary (e.g. "IMPROVE-UX X05 closes F2 + F3 — J7 the guest
end-date (R20) + J10 request-more-space (A14) both verified live under the
right seed data; the lane's journey table is now 10 of 12 walked + 2
infra-verified, with F6 the only remaining open item (another lane's
improve-sec archive docs)").
