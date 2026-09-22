# ADR 0062 — The /admin surface split: overview dashboard + linkable section pages

Status: Accepted
Date: 2026-09-22

## Context

The `/admin` surface (M1 step 8) was a single long scroll: the platform
list-group, the platform-pages table, the full accounts table (with the
verify-queue form and the per-row role / scope / posting checkboxes), and the
community list — all on one `Index` page, in that order. Two problems had built
up by the time the multilingual lane pulled the platform forward:

- **No linkable, shareable, per-section address.** An admin deep in the
  community table who wanted to "send a colleague straight to the accounts
  page" could not — `/admin` was the only route, and the accounts were a
  mid-scroll section. (Steps 1 and 2 of the clutter pass had already carved
  out the per-account detail at `/admin/accounts/{subjectId}` and replaced the
  checkbox pile with a `list-group` + action hierarchy, which proved the
  "the shell is too much, split it" direction was right.)
- **The scroll's length made it hard to scan.** Every surface the platform
  gained since M1 (languages, time zone, date format, sign-up, the platform
  pages, the community management) had been appended to the same page, so an
  admin doing one small thing (set the time zone) landed on a page that also
  contained the full accounts table and the community editor.

The natural fix is the one this repo has already applied to the per-account
detail (step 1): **split the one page into linkable sections, each with its own
route**, and leave a thin dashboard at the old address. This is a settled
design question about the admin *surface layout* — the kind of decision the
repo records as an ADR (like ADR 0053, which gave the community-translation
surface its own page for the same "the shell is too much" reason).

The constraint from the test harnesses (documented in
`AdminTimezoneController` / `AdminSignupController`): the
`AdminController` constructor is pinned by the Web-layer test harnesses
(`AdminControllerBlockTests` / `AdminControllerMandatoryTests` /
`AdminControllerSetRoleTests`), so a new dependency in that constructor would
break them. The section actions therefore live **on the existing
`AdminController`** with explicit `[Route]` attributes (the exact precedent the
`Manage` action and the sibling `AdminTimezoneController` / `LanguagesController`
already establish), rather than on new controllers that would each need the
constructor's dependencies.

## Decision

- **Split `/admin` into five linkable routes, all on `AdminController`:**

  | Route | Contents |
  |---|---|
  | `/admin` | **Overview dashboard** — the account / unverified / blocked / community counts at a glance, the unverified-queue shortcut (the safety valve, still one click away), and section cards linking to the four detail pages. |
  | `/admin/accounts` | **Accounts** — the account list (roles / scope / posting, read-only), the verify-queue form, and the row-level Block / Unblock quick actions. Per-account writes still live on `/admin/accounts/{subjectId}` (the `Manage` detail page). |
  | `/admin/communities` | **Communities** — the add form and the community list with Edit, Make optional / mandatory, and Disable / Re-enable. |
  | `/admin/platform` | **Platform** — the platform list-group (languages, time zone, date & time format, sign-up, audit, break-glass) and the five shipped platform pages (preview / edit). |
  | `/admin/security` | **Security** — a landing hub linking to the two live security surfaces, `/admin/audit` and `/admin/break-glass`, which keep their own routes and pages. |

- **A shared sub-nav** (`_AdminNav.cshtml`) renders a `nav-tabs` strip on all
  five pages so an admin can jump between sections without returning to the
  dashboard. The active tab is derived from the current action name; the
  `Audit` and `BreakGlass` pages (which are their own routes under the security
  section) highlight the **Security** tab.

- **The write lanes are unchanged; only their redirect targets are re-pointed
  to the correct section page.** The audited Core lanes
  (`IIdentityService` / `IUserInfoService`) and their controller wrappers
  (`Block`, `Unblock`, `ManuallyVerify`, `AddCommunity`, `UpdateCommunity`,
  `ToggleCommunityEnabled`, `ToggleCommunityMandatory`, `SetRole`,
  `SetCommunityMembership`) keep their exact signatures and behavior. After a
  save they now `RedirectToAction` to the section that owns them —
  `Accounts` for the account lanes, `Communities` for the community lanes
  (instead of the old `Index`) — so an admin lands on the page the form was
  on, not the dashboard. The `SetRole` / `SetCommunityMembership` lanes already
  redirect to the `Manage` detail page; that is untouched.

- **The `Manage` detail page is unchanged.** `/admin/accounts/{subjectId}`
  keeps its `[Route]`, its `AdminAccountManageViewModel`, and its three
  fieldset forms (SetRole / SetCommunityMembership / the standing surface).
  The `Manage` link on the accounts section still points at it.

- **The routing convention is the `[Route]` attribute on the existing action**,
  exactly the `Manage` precedent: explicit `[Route("admin/accounts")]`,
  `[Route("admin/communities")]`, `[Route("admin/platform")]`,
  `[Route("admin/security")]` on the new GET actions. This keeps the actions
  on the test-pinned `AdminController` (no new constructor, no new controller),
  and the URL generator resolves them for the `RedirectToAction` targets — the
  same mechanism `Manage` already relies on.

## Consequences

- An admin can now deep-link, share, and bookmark each section:
  `/admin/accounts`, `/admin/communities`, `/admin/platform`, `/admin/security`
  — and `/admin` is the "where am I" landing surface, not a 400-line scroll.
- The dashboard is thin by design: it answers "how many, and what needs my
  attention right now" (the counts + the verify queue) and links out. It does
  **not** re-render the accounts table or the community editor — those have
  their own pages.
- The **write-lane behavior is unchanged** (the audited Core lanes, the CSRF
  gates, the self-block guard, the fail-closed standing checks) — the split is
  a *surface* change, not a *capability* change. The tests that pin those lanes
  (`AdminControllerBlockTests` / `AdminControllerMandatoryTests` /
  `AdminControllerSetRoleTests`) keep passing untouched, because they assert on
  the NSubstitute call log (the Core invocation) and swallow the
  `RedirectToAction` URL-resolution NRE — so re-pointing the redirect target is
  safe by construction.
- The **security surfaces keep their own routes and pages**
  (`/admin/audit`, `/admin/break-glass`); `/admin/security` is a hub, not a
  merge. This matches the existing convention (the audit / break-glass pages
  already link to each other by `asp-action`), and keeps the OPS.md §9
  break-glass procedure (`go to /admin/break-glass`) unchanged.
- The `<kw-l>` registry is untouched: the new section pages reuse the existing
  `admin.*` keys (`admin.title`, `admin.verify`) and plain-English section copy
  (this admin surface's local convention, the `KwLRegistryConsistencyTests`
  pin), so no new registry key is required and the four-language parity test is
  unaffected.
- A future admin surface (a new platform setting, a new security surface) adds
  to the **right section page** rather than to a growing `/admin` scroll, and
  the sub-nav is the single place to register a new section — the same
  "add to the section, not the shell" discipline step 1 established for the
  per-account detail.
