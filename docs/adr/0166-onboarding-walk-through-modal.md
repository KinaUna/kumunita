# ADR 0166 — Onboarding walk-through: each onboarding step opens its setting in a modal (a same-origin iframe of the surface's own route) with Previous / Next navigation + a "Step X of Y" counter, so the resident / admin onboarding pages are a clean linear process

Status: Accepted
Date: 2026-10-13

The resident onboarding page (`/onboarding`, ADR 0132, M22) and the admin
onboarding page (`/admin/onboarding`, ADR 0153, M30) are **links-only
walk-throughs**: a numbered list of the most important initial settings, each
row a link out to that surface's own page. The walk-through itself is a
singleton completion flag — it stores **no** settings and writes **no** lane.
Both shipped fine as a "here are the things to set up" checklist, but they
are **poor to actually use**: the user clicks a row, leaves the onboarding
page, loses the context of where they are in the list, and it is not obvious
how to proceed to the next step or how to get back. ADR 0166 settles the
**onboarding-navigation** question neither ADR 0132 nor ADR 0153 answered:
the walk-through should be a **clean linear process** — open the setting in
place (a modal), and move Previous / Next between the steps, so it is always
clear where you are and how to continue.

## The decision

Each onboarding step's "go to this setting" control now **opens the setting's
page inside a modal** (a Bootstrap 5 modal on the existing `bootstrap.bundle`
— no new dependency) whose body is a **same-origin `<iframe>` of that
surface's own route**. The modal carries a **Previous** button, a **Next**
button, and a **"Step X of Y"** counter; the two buttons walk the ordered
step list by index (Previous is disabled on the first step, Next on the last —
a **bounded linear walk**, not a loop). The step buttons on the main page
open the modal at the step the user chose.

The modal **frames the surface's own route** — it does not re-author the
form. That is the load-bearing choice, and it is the same "ride frozen lanes"
doctrine ADR 0132 D3 and ADR 0153·7 already pin for these two surfaces (the
walk-through is a links-only affordance; it must never re-implement a write).
Framing the real page is what lets that pin hold: the page inside the modal
is the **real** settings page — its form, its anti-forgery token, and its
save lane (the `RedirectToAction(nameof(Index))` each controller already
issues) all work unchanged. Saving reloads the frame in place and the modal
stays open; the walk-through's singleton completion flag and every write lane
are **untouched**.

## Why a same-origin iframe is legal (and the M35 / ADR 0104 idiom)

CSP permits this: `frame-ancestors 'self'` (the app may be iframed by
**itself**) and `script-src 'self'` (the modal's behaviour is a
`tsc`-built `client/lib/onboarding-modal.ts` module — **no** inline `<script>`,
the ADR 0031 / M35·7 pin). The ADR 0164 kiosk display already uses exactly
this shape (frame the surface's own route, hide the chrome); ADR 0166 is the
same idiom, applied to the onboarding walks instead of the shared wall
display.

## The chrome-hide

A settings page loaded inside the modal hides its page chrome (navbar,
footer, the onboarding banner itself, the pinned announcement, the What's-new
toast, and the settings sub-nav's negative margin) so it reads as a focused
panel rather than a nested full page. The signal is the **`Sec-Fetch-Dest:
iframe`** request header — `_Layout.cshtml` sets a `body.kmb-embed` class when
the page is being fetched as an iframe, and `site.css` carries the
`body.kmb-embed` chrome-hide rules (the ADR 0104 / M35 chrome-hide idiom).
A direct navigation (`Sec-Fetch-Dest: document`) never sets the class, so the
page is **byte-identical** when visited normally — a resident or admin who
opens the link directly sees exactly the surface they see today.

## What it is, and what it is not

- **It is a navigation / presentation rework of two existing views** — no new
  bounded context, no new document, no new service, no new doc-type surface,
  no EF migration, no re-shape of any existing write lane, no new `AccessAction`
  / `AccessVia` / `Decide()` branch, no new `AccessAudit` row.
- **It adds** the shared `OnboardingStep` record (the resident
  `OnboardingViewModel` and the admin `AdminOnboardingViewModel` both expose
  their ordered step list as `IReadOnlyList<OnboardingStep>`), the shared
  `Views/Shared/_OnboardingModal.cshtml` partial (the one modal both walks
  render through, differing only in the step list), the two views' step-list
  buttons (`.kmb-onb-open`, carrying `data-ob-index`), the `client/lib/onboarding-modal.ts`
  state machine (tsc-only, self-guards on a page without the modal — the
  `kiosk.ts` / `expand-page.ts` idiom), the `site.css` `body.kmb-embed` +
  `.kmb-onboarding-iframe` rules, the `_Layout.cshtml` `Sec-Fetch-Dest`
  detection + `common.step_of` / `common.title` in the P0-6
  `#kumunita-strings` bundle, and the closed `common.previous` / `common.next`
  / `common.done` / `common.step_of` `kw-l` key set × en/de/fr/da (absorbed
  into the `Common` per-surface view — the parity tests stay green).
- **It is a named lane**, not a milestone — `Milestones.cs`, the README
  Roadmap, `MilestonesTests.cs`, and `STATUS.md` are **untouched** (the M30
  admin-onboarding and M22 resident-onboarding rows are already "done"; this
  reworks *how* they are navigated, not *what* they walk through). The version
  is added to `WhatsNew.cs` (the `VN` lane, ADR 0110) as the head entry.

Additive on 0132 [the resident onboarding the lane reworks] + 0153 [the admin
onboarding the lane reworks] + 0164 [the kiosk "frame the surface's own
route" + chrome-hide idiom this lane mirrors] + 0104 [the `body`-class
chrome-hide idiom] + 0031 [the plain-TS client-module pin] + 0110 [the
`WhatsNew` close-flip member] + 0015 [the `kw-l` registry].
