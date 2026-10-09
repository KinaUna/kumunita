# ADR 0153 — Admin onboarding (a guided walk-through for a new GlobalAdmin through the seven most important initial settings)

Status: Accepted
Date: 2026-10-08

## Context

A new **GlobalAdmin** who takes over the instance for the first time faces the
*exact same* problem M22 (ADR 0132) solved for a new **resident**, one level
up: they must discover, in a sprawling `/admin` surface (the `/admin/platform`
list + the `/admin` dashboard hub), the **seven most important initial
settings** — **community name, languages, moderation, notifications, storage
limits, site content, issue escalation** — and there is **no guided path**
that walks them through those seven *in order*, and **no completion record**
so a second GlobalAdmin (or a returning first one) doesn't see the same
walk-through again.

M22 (ADR 0132) gave a **new resident** a short guided tour of the account-
setup surface that already exists, a single completion stamp
(`Profile.OnboardingCompletedAt`), and a dismissible banner. SITE (ADR 0150)
gave a **GlobalAdmin** a single-surface `/admin/site` edit page. But the **seven
most important initial settings** are scattered across **six shipped admin
surfaces** (the seventh — issue escalation — is a **placeholder**, see below),
and there is **no guided path** that walks a new GlobalAdmin through those
seven *in order*, and **no completion record** so a second GlobalAdmin doesn't
see the same walk-through again.

## Decision

- **The walk-through is a *guided shell*, not a new data surface** (D1).
  Every setting it links into is **already writable** through an existing
  `GlobalAdmin`-gated lane (`/admin/languages` / `/admin/announcements/comments`
  / `/admin/quiet` / `/admin/storage/settings` / `/admin/site` /
  `/admin/announcements`). M30 **rides those surfaces verbatim** — it adds
  **no new write lane** for any of the seven settings; the walk-through's
  *only* write is the completion stamp. The walk-through is **links-only**: it
  does **not** inline a control for any of the seven, does **not** add a
  per-step POST, does **not** persist a step cursor, and does **not** gate the
  admin surface on completion.
- **The completion state is one singleton doc, `null` = not-yet-guided**
  (D2). The `AdminOnboarding` doc is a **singleton** (one row per instance,
  `Id = "singleton"` sentinel, the exact `SiteContent` / `LocaleSettings`
  shape, ADR 0005 B / ADR 0150) with exactly **one** additive member —
  `CompletedAt: DateTimeOffset?`, `null` = the floor (banner shows), non-null
  = completed (banner clears). It is registered in a new
  `AdminOnboardingDocTypes` surface (ADR 0004 §B.1); the delta is applied
  idempotently at boot; **no EF migration**. The `SiteContent` +
  `LocaleSettings` docs are **untouched** (M30 adds a *new* doc in a *new*
  context, ADR 0006).
- **The read is a public admin surface** (D3). The completion-state read is
  `GlobalAdmin`-gated by the `[Authorize(Roles = GlobalAdmin)]` on the
  controller (the standard admin-scope gate, **not** a new authorization
  surface — the thin-token rule, ADR 0001-B), and is **never audited** (a
  read, not an access decision). A missing `AdminOnboarding` row (a fresh boot
  before the seeder ran, or a test construction with no store) degrades to
  `CompletedAt = null` (not-yet-guided, the banner shows) — never a blank
  walk-through, never an error.
- **The write is the ADR 0150 single-write-lane shape** (D4). One
  `IAdminOnboardingService.CompleteAsync(actorBy)` lane that loads the
  singleton, stamps `CompletedAt = now`, and saves in one session (invariant
  C3); exactly **one** `AccessAudit` row per stamp (`Via = Admin`, action
  `admin_onboarding.complete`, `TargetKind` "admin-onboarding" — the
  `site.save` / `signup.set-open` / `timezone.set-default` singleton-toggle
  shape). The lane **upserts** the singleton (the `SiteContent` "one row per
  instance" shape, the ADR 0150 D6 pin); **strong consistency** — the new
  value is live on the very next `GetAsync` / banner read.
- **The banner is the *affordance*, and it is non-blocking** (D5). A
  dismissible admin banner (the M22 `_OnboardingBanner` partial's admin
  sibling, the `_AdminNav` partial's scope) is rendered on the admin surfaces
  **only while `CompletedAt` is `null`** AND the signed-in actor is a
  `GlobalAdmin`. It links to `/admin/onboarding`. It is **non-blocking**: the
  admin sign-in is never gated, the banner is always dismissible (a
  `sessionStorage` flag — never a write to `CompletedAt`), and the
  `/admin/onboarding` route is always reachable for a `GlobalAdmin` regardless
  of completion state.
- **The closed `adminonboarding.*` `kw-l` key set is parity-pinned in four
  languages** (D6). Every new user-visible string M30 introduces is a
  `KnownTranslationKeys` entry present, **non-empty, in all four** languages
  (en/de/fr/da), pinned by `KwLRegistryConsistencyTests` +
  `KnownTranslationKeys_ParityTests`. The closed set (the title + the intro +
  the seven step cards + the "visit" link + the "mark as complete" button +
  the flash + the banner copy + the banner CTA) is authored in all four
  languages.
- **The walk-through links into the seven roadmap settings** (D7). The seven
  step cards are the **closed** set named in the README / `Milestones.cs`
  line — **community name, languages, moderation, notifications, storage
  limits, site content, issue escalation** — each linking into the existing
  admin surface that already owns it (by route).
- **The `AdminOnboarding` doc is a singleton in a new context** (D8). The
  `AdminOnboarding` doc is a **new** bounded context
  (`Kumunita.Core.AdminOnboarding`) with a **new** `AdminOnboardingDocTypes`
  registration surface; the `SiteContent` + `LocaleSettings` contexts are
  **untouched** (the ADR 0006 module-boundary contract).

## Consequences

- The **seven admin surfaces are walk-able in order** — a new GlobalAdmin can
  now walk community name → languages → moderation → notifications → storage
  limits → site content → issue escalation, each step linking into the
  existing admin surface that already owns it.
- The **`/admin/onboarding` surface is the GlobalAdmin's walk-through page** —
  a dedicated `AdminOnboardingController` in the ADR 0150 / ADR 0050 shape
  (the `GET /admin/onboarding` walk-through + the
  `POST /admin/onboarding/complete` completion stamp).
- The **admin banner is the affordance** — a dismissible banner on the admin
  surfaces while `CompletedAt` is `null` AND the actor is a `GlobalAdmin`,
  linking to `/admin/onboarding`; it clears on completion.
- The **`SiteContent` + `LocaleSettings` docs are untouched** — M30 adds a
  *new* doc in a *new* context (`Kumunita.Core.AdminOnboarding`), not a new
  field on an existing one (the ADR 0006 module-boundary contract keeps the
  three contexts independent).
- The **M22 `Profile.OnboardingCompletedAt` field is untouched** — M22 is
  resident-scope, M30 is admin-scope; the two completion flags are
  **independent**.
- The **`Milestones.cs` / README / `MilestonesTests` trio is untouched until
  the milestone *ships*** (the close unit — U07 — owns the flip). The
  `WhatsNew.cs` registry gains one new entry (newest-first, the `0.46.0`
  row) naming M30 + this ADR — the M27 "shipped with no entry until caught in
  review" lesson (AGENTS.md) is held.
- **Named deferrals (for future lanes, if one comes):** per-step progress /
  resume-across-sessions (the M22 D8·1 deferral, verbatim), a "you haven't
  set X" detection engine (the M22 D8·2 deferral, verbatim), and an admin
  "onboarding completion rate" metric (the M22 D8·4 deferral, verbatim).
- **No EF migration** (a new Marten doc type is additive per ADR 0004 §B.1;
  the delta is applied idempotently at boot). **One new route**:
  `/admin/onboarding` (the GlobalAdmin's walk-through surface + the
  completion stamp). **No new `AccessAction` / `AccessVia` / `Decide()`
  branch / `IAuthorizationService` surface** (the read is a public admin
  surface; the write is the ADR 0150 `GlobalAdmin`-gated single-write-lane
  shape, one `AccessAudit` row per stamp).
- **No roadmap letter moves** — M30 stays the milestone it is; M31–M34 are
  untouched.
