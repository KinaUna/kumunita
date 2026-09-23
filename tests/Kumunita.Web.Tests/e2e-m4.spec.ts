// M4, plan U11 — e2e Playwright specs (browser-level, three specs).
//
// STATUS (honest — per U11's own entry read, mirroring U13's M2 / U10's
// M3b precedent):
//   Authored against the *shipped* M1–M4 UI (selectors + route pins are
//   grounded against the actual .cshtml — the header block below lists
//   them). NOT yet runnable: the *M2 D2* documented-throw is re-confirmed
//   here (the `kumunita` fixture is a documented throw in e2e-m2.spec.ts,
//   e2e-m3.spec.ts, AND this file, and no M4 unit — U0 through U11 —
//   implements the runtime). The plan register § U11 Exit explicitly
//   permits this: "If the e2e runtime (Postgres-boot + token-channel) is
//   not yet present, the spec is authored (mirroring M2's U13) and *not
//   run* — the gap is recorded in the design doc and the next unit who
//   lands the runtime records the pass count." U11 takes the *author*
//   path, exactly following the M2 U13 / M3 U10 / M3b U10 precedent.
//
//   The gap is recorded in `docs/design/m4-events-design.md` § "M4 —
//   Closed (recorded)" (the run-result section) and in
//   `m4-handoff-notes.md` § "## U11 — gate recorded". The *bounded*
//   runtime gap is: (1) the `kumunita` fixture's `signup / login /
//   lastCreatedEventId / grantEventToUser` implementation (the M2 D2
//   token-channel + M4's two helpers), (2) a Postgres boot wired to the
//   same DB the `dotnet run` server reads, and (3) no new production-code
//   changes are required to make this spec green (the M4 Core + Web
//   seams are frozen and already exercised by the 23 Core seam tests +
//   the 19 controller tests).
//
// Gate anchors frozen by this file (see `docs/design/m4-events-design.md`
// §3.8, the three-test acceptance gate):
//   (a) closed-loop — an author creates a published event → it appears in
//       the `/events` feed (the `div.airy-ann-row` card); the author
//       RSVPs `Going` → their RSVP is visible in the owner-only
//       "Responses" list (`ul.list-unstyled li` with a `.badge`).
//   (b) handoff — a user added to the event's `Audience.Grants`
//       *after* creation sees the event on the **next** request —
//       strong consistency, no cache. The `Delegation` branch is the
//       handoff-onto-a-delegate case.
//   (c) part-vs-whole — the 23 names in §3.7 are the **whole**; tests
//       (a)/(b) are the **parts**; all must pass *together* in the same
//       `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/PG anchors
//       (no per-name isolation). This is Core-level evidence — the
//       browser-level pin is the *negative*: the feed/detail surface
//       renders consistently across the (a)/(b) flows.
//
// Route / selector pins (from the shipped views — read the .cshtml first
// before "improving" them):
//   GET    /events                    (feed — `div.airy-ann-list` with one
//                                      `div.airy-ann-row` per event; each
//                                      row is an `h3.airy-ann-title > a`
//                                      linking to `/events/{id}`)
//   GET    /events/{id}               (detail — `h1` title, `.rc-body`
//                                      rendered body, the RSVP `form`
//                                      with `input[name="status"]` radios
//                                      `#rsvp-Going` / `#rsvp-Maybe` /
//                                      `#rsvp-No` + a `button[type="submit"]`
//                                      "Update"; the owner-only "Responses"
//                                      list is an `h3.h6` "Responses"
//                                      heading + `ul.list-unstyled li`
//                                      rows, each with a `.badge` for the
//                                      `Status` literal Going/Maybe/No)
//   GET    /events/new                (composer — `form#new-event-form`
//                                      with `input[name="Title"]`,
//                                      `input[name="Start"]`,
//                                      `input[name="End"]`,
//                                      `textarea[name="Body"]`,
//                                      `input[name="Audience.CommunityVisible"]`,
//                                      `input[name="Audience.Mode"]`
//                                      (radio, `#aud-mode-Any` / `#aud-mode-All`),
//                                      `input[name="ReminderEnabled"]`,
//                                      `input[name="SaveAsDraft"]`; a
//                                      `button[type="submit"]` "Create event")
//   POST   /events/new                (composer submit → redirects to
//                                      `/events/{id}`)
//   GET    /events/{id}/edit          (edit lane — the composer shape,
//                                      `form` with the same field names
//                                      as the composer)
//   POST   /events/{id}/edit          (edit lane submit → redirects)
//   POST   /events/{id}/publish       (publish lane — the Draft badge's
//                                      `button[type="submit"]` "Publish";
//                                      only present when `Model.IsDraft`
//                                      AND `Model.CanPublish`)
//   POST   /events/{id}/delete        (delete lane — the `button[type="submit"]`
//                                      "Delete"; only present when
//                                      `Model.CanDelete`)
//   POST   /events/{id}/rsvp          (RSVP lane — the RSVP form's
//                                      `input[name="status"]` radio +
//                                      `button[type="submit"]` "Update";
//                                      redirects back to `/events/{id}`)
//
//   (The `Status` literals **Going / Maybe / No** are frozen once written —
//    no new literal may be rendered by this spec or by any view it asserts
//    against.)

import { test as baseTest, expect, type Page } from '@playwright/test';

// ── Fixture shapes (documented; the implementation is the "Playwright
//    runtime" unit's work — U11 follows U13's M2 precedent and records
//    the gap rather than landing it mid-milestone) ──
//
// A single `kumunita` fixture the tests below consume, with the
// methods/fields the spec needs. This is the *entire* contract; it is
// deliberately small and bounded (same discipline as M2 U13 / M3 U10).
//
//   signup(displayName, email, password) ⇒ Promise<SignupHandle>
//     · POST /account/signup (the M1 four-field form)
//     · flip the account to verified via the M1 token channel (the
//       `IMailerStage` / `OutboxEmailHandler` durable outbox →
//       `IdentityToken` row read, server-side; the pure browser has no
//       channel to that table — see the "M1 token channel" note below)
//     · returns an opaque subjectId — a string, NOT a Guid (the M1 seam
//       freeze)
//
//   login(page, email, password) ⇒ Promise<void>
//     · clears the current context's cookies → fresh user in the same page
//     · POST /account/login; lands signed in
//
//   lastCreatedEventId() ⇒ Promise<string>
//     · the most-recently-created event's id — from the
//       `EventController.CreatePost` redirect target (`/events/{id}`).
//       Without this, tests (a)/(b) fall back to parsing the redirect
//       URL (a smaller, more brittle contract; prefer the fixture helper).
//
//   grantEventToUser(eventId, userId) ⇒ Promise<void>
//     · the test-side shortcut for adding `userId` to the event's
//       `Audience.Grants` list (the M4 handoff gate's precondition). The
//       browser surface does not expose a form to add a grant *after*
//       creation (the `_GrantPickers` partial is on the composer/edit
//       form only — a full round-trip would require the edit form's grant
//       picker, which is a *separate* write lane the gate does not pin).
//       The fixture is the test-side shortcut (the M3b U10
//       `assignModeratorToComponent` precedent — a Core-level setup step
//       the fixture owns).
//
// The e2e's *browser* steps assert the *observable* surface: the feed
// row's presence, the RSVP "Responses" list's badge, the draft badge's
// publish form, the detail page's 200 render — **never** the Core
// internals (the `AccessAudit` rows, the `EventRsvp` doc shape, the
// `EventToAuditableResource` projection — those are Core-level evidence,
// the 23 Core seam tests).

interface SignupHandle {
  subjectId: string; // opaque string, NOT a Guid (M1 seam freeze)
}

interface Kumunita {
  signup(
    displayName: string,
    email: string,
    password: string,
  ): Promise<SignupHandle>;
  login(page: Page, email: string, password: string): Promise<void>;
  lastCreatedEventId(): Promise<string>;
  grantEventToUser(eventId: string, userId: string): Promise<void>;
}

const extended = baseTest.extend<{ kumunita: Kumunita }>({
  kumunita: async ({}, use) => {
    throw new Error(
      'kumunita fixture not implemented (M4 U11 re-confirmed). ' +
      'U11 authored the spec (the selector + route pins are frozen in ' +
      'the header of e2e-m4.spec.ts); the Playwright runtime unit must ' +
      'land the implementation of `signup / login / lastCreatedEventId / ' +
      'grantEventToUser` per the fixture contract above. Reuse the M2 U13 ' +
      'fixture contract (e2e-m2.spec.ts) for the signup/login shape; the ' +
      'two new helpers (lastCreatedEventId, grantEventToUser) are M4 ADDs. ' +
      'See docs/plans-milestones/done/m4/m4-handoff-notes.md § U11.',
    );
    // `use` is required by the Playwright fixture API. The throw above
    // fires first (before `use` is ever called), so there is nothing
    // left to return. No explicit `return;` here — that would be
    // unreachable (TS7027) and adds no semantics. Same precedent as
    // e2e-m2.spec.ts / e2e-m3.spec.ts.
  },
});
const test = extended;

// A tiny helper (spec-local, not fixture-local): submit the *named* form
// on the current page (the M4 Razor views are Bootstrap forms with a
// single submit button each). Same helper as e2e-m2.spec.ts /
// e2e-m3.spec.ts.
async function submitForm(page: Page, scope?: string): Promise<void> {
  const scopeSel = scope ? `form:has(${scope})` : 'form';
  await page.locator(scopeSel).locator('button[type="submit"]')
    .first().click();
}

// Wait for the flash toast (_FlashToast.cshtml → #flash-toast-container
// .toast-body) to appear. The per-view `.alert` blocks are gone; the
// controller's TempData["info"]/["error"] is now rendered centrally as a
// Bootstrap 5.3 toast in the layout. Same as M3b U10's `expectAlert`
// helper, but on the toast surface.
async function expectAlert(page: Page, text: RegExp): Promise<void> {
  await expect(page.locator('#flash-toast-container .toast-body', { hasText: text })).toBeVisible();
}

test.describe('M4 e2e', () => {
  // ── (a) Closed-loop (gate test 1) — create → feed → RSVP → list ──
  // An author creates a published event (not a draft) through the
  // composer; the event appears in the `/events` feed (the
  // `div.airy-ann-row` card); the author RSVPs `Going`; the author's
  // own RSVP is visible in the owner-only "Responses" list (the
  // `ul.list-unstyled li` with a `.badge` for the `Status` literal).
  test('a. closed-loop — author creates → feed row → RSVP Going → owner-only list', async ({ page, kumunita }) => {
    const author = await kumunita.signup('Author A.', 'author-a@example.com', 'Passw0rd!');
    await kumunita.login(page, 'author-a@example.com', 'Passw0rd!');

    // 1) Author creates a *published* event (not a draft) through the
    //    composer. The `SaveAsDraft` checkbox is unchecked by default —
    //    the event is immediately visible to its audience.
    await page.goto('/events/new');
    await page.locator('input[name="Title"]').fill('M4 closed-loop pin');
    await page.locator('textarea[name="Body"]').fill('The e2e pin body.');
    // Start/End — the composer's `datetime-local` inputs. The value
    // must be a valid `yyyy-MM-ddTHH:mm` local string; the fixture's
    // runtime (a future unit) supplies a sensible default via the form's
    // pre-seeded values (the Create.cshtml's `@Model.Start` /
    // `@Model.End` are pre-seeded by the controller). We leave them as
    // pre-seeded — the e2e does not override the datetime fields.
    // `Audience.CommunityVisible` is checked by default (the ADR 0036
    // community-visible default) — the event is visible to everyone by
    // default. We do not touch the audience section in this test.
    // `ReminderEnabled` is the reminder opt-in — the closed-loop gate
    // does not pin the reminder (that is the U07/U08 Core-level seam,
    // already exercised by the 5 `EventReminderServiceTests`). We leave
    // it at the form's default.
    // `SaveAsDraft` — unchecked (the default). The event is published.
    await submitForm(page, 'form#new-event-form');

    const eventId = await kumunita.lastCreatedEventId();
    expect(typeof eventId).toBe('string');
    expect(eventId.length).toBeGreaterThan(0);

    // 2) The event appears in the `/events` feed (the closed-loop
    //    "it appears in the feed" pin). The feed row is a
    //    `div.airy-ann-row` with an `h3.airy-ann-title > a` linking to
    //    `/events/{eventId}`.
    await page.goto('/events');
    const feedRow = page.locator('div.airy-ann-row', {
      has: page.locator('h3.airy-ann-title a', { hasText: 'M4 closed-loop pin' }),
    });
    await expect(feedRow).toBeVisible();
    // The link points at the new event's detail page.
    await expect(feedRow.locator('h3.airy-ann-title a'))
      .toHaveAttribute('href', new RegExp('/events/' + eventId + '$'));

    // 3) The author RSVPs `Going` from the detail page. The RSVP form
    //    has three `input[name="status"]` radios (`#rsvp-Going` /
    //    `#rsvp-Maybe` / `#rsvp-No`) + a `button[type="submit"]`
    //    "Update". Selecting the radio and clicking submit POSTs to
    //    `/events/{id}/rsvp`.
    await page.goto('/events/' + encodeURIComponent(eventId));
    await page.locator('#rsvp-Going').check();
    await submitForm(page, 'form:has(input[name="status"])');
    // The RSVP lane redirects back to `/events/{id}` (the
    // `EventController.Rsvp`'s `Redirect($"/events/{id}")`). Assert the
    // "You're going to: Going" confirmation (the Detail.cshtml's
    // `Model.MyRsvp is not null` branch).
    await expect(page.locator('span.text-muted.small', {
      hasText: /You're going to:/i,
    })).toBeVisible();

    // 4) The author's own RSVP is visible in the owner-only "Responses"
    //    list (the closed-loop "their RSVP is visible in the owner-only
    //    list" pin). The list is an `h3.h6` "Responses" heading +
    //    `ul.list-unstyled li` rows, each with a `.badge` for the
    //    `Status` literal. The author (the event's owner) sees their
    //    own RSVP — the `EventService.GetRsvpsAsync` owner-only read.
    await expect(page.locator('h3.h6', { hasText: /Responses/i })).toBeVisible();
    await expect(page.locator('ul.list-unstyled li .badge', {
      hasText: 'Going',
    })).toHaveCount(1);
  });

  // ── (b) Handoff (gate test 2) — the grant handoff ─────────────────
  // A user added to the event's `Audience.Grants` *after* creation sees
  // the event on the **next** request — strong consistency, no cache.
  // The `Delegation` branch is the handoff-onto-a-delegate case (the
  // delegate sees the owner's grant-scoped event).
  //
  // The gate's shape: (1) the author creates an event with a restricted
  // audience (the `Audience.CommunityVisible` switch OFF, the
  // `Audience.Mode` = "Any" with a grant picker the author leaves empty
  // — the event is visible to *no one* but the author); (2) the
  // fixture's `grantEventToUser` adds `grantee` to the grant; (3) the
  // grantee loads the `/events` feed on the *next* request — the event
  // is now visible (the strong-consistency pin).
  //
  // The browser-level pin is the *observable*: the grantee's feed row
  // appears on the next request. The Core-level evidence (the
  // `Audience.Grants` row, the `AccessAudit` decision row, the
  // `EventToAuditableResource` projection) is already exercised by the
  // 23 Core seam tests (T04 `M4_GrantsAudienceOnlyGranteeSees` is the
  // direct pin).
  test('b. handoff — grant added after creation; grantee sees the event on the next request', async ({ page, kumunita }) => {
    const author  = await kumunita.signup('Handoff Author', 'handoff-author@example.com', 'Passw0rd!');
    const grantee = await kumunita.signup('Handoff Grantee', 'handoff-grantee@example.com', 'Passw0rd!');

    // 1) Author creates an event with a *restricted* audience (the
    //    `Audience.CommunityVisible` switch OFF). The event is visible
    //    to no one but the author (the `Audience.IsEmpty` branch — the
    //    `Audience` doc's "empty list denies everyone" rule).
    await kumunita.login(page, 'handoff-author@example.com', 'Passw0rd!');
    await page.goto('/events/new');
    await page.locator('input[name="Title"]').fill('M4 handoff pin');
    await page.locator('textarea[name="Body"]').fill('The G2 body.');
    // Turn OFF the community-visible default (the ADR 0036
    // community-visible default is the *checked* state; unchecking it
    // opens the `#audience-restricted` section with the `Audience.Mode`
    // radio + the `_GrantPickers` partial). The e2e's pin is the
    // *negative* — the grantee does NOT see the event before the grant.
    await page.locator('input[name="Audience.CommunityVisible"]').uncheck();
    // The `#audience-restricted` section is now visible (the
    // `d-none` class is removed by the Create.cshtml's
    // `@(Model.Audience is { IsEmpty: false } ? null : "d-none")`
    // ternary — the e2e does not assert this intermediate state; the
    // *observable* is the grantee's feed row appearing/disappearing).
    // `Audience.Mode` = "Any" is the default (the radio is pre-seeded
    // checked). We leave it as-is — the grant picker is left empty by
    // the author (the `_GrantPickers` partial's default). The event is
    // visible to no one but the author.
    await submitForm(page, 'form#new-event-form');
    const eventId = await kumunita.lastCreatedEventId();
    expect(typeof eventId).toBe('string');

    // 2) The grantee (a *non-author*, *non-grantee*-before) loads the
    //    feed — the event is NOT visible (the restricted audience
    //    denies them). This is the *pre-grant* negative.
    await kumunita.login(page, 'handoff-grantee@example.com', 'Passw0rd!');
    await page.goto('/events');
    await expect(page.locator('div.airy-ann-row', {
      has: page.locator('h3.airy-ann-title a', { hasText: 'M4 handoff pin' }),
    })).toHaveCount(0);

    // 3) The fixture plants the grant (the test-side shortcut — the
    //    M3b U10 `assignModeratorToComponent` precedent). The browser
    //    surface does not expose a form to add a grant *after*
    //    creation; the fixture is the test-side shortcut.
    await kumunita.grantEventToUser(eventId, grantee.subjectId);

    // 4) The grantee loads the feed again — the event is now visible
    //    (the strong-consistency pin — the "next request" gate).
    await page.goto('/events');
    await expect(page.locator('div.airy-ann-row', {
      has: page.locator('h3.airy-ann-title a', { hasText: 'M4 handoff pin' }),
    })).toBeVisible();
    // The link points at the event's detail page.
    await expect(page.locator('h3.airy-ann-title a', { hasText: 'M4 handoff pin' }))
      .toHaveAttribute('href', new RegExp('/events/' + eventId + '$'));
  });

  // ── (c) Part-vs-whole (gate test 3) — the 23 names are the whole ──
  // The 23 names in §3.7 are the **whole**; tests (a)/(b) are the
  // **parts**; all must pass *together* in the same
  // `Kumunita.Core.Tests` run as the inherited M1/M2/M3/M3b/PG anchors
  // (no per-name isolation).
  //
  // This is **Core-level** evidence — the browser-level pin is the
  // *negative*: the feed/detail surface renders consistently across
  // the (a)/(b) flows (the same `div.airy-ann-row` shape, the same
  // `ul.list-unstyled` RSVP list shape). The 23 names themselves are
  // the 18 `EventServiceTests` (T01–T18) + the 5
  // `EventReminderServiceTests` (T19–T23) — the U09 handoff note's
  // pinned list. The U11 gate run (the `dotnet exec Kumunita.Core.Tests.dll`
  // in the handoff note) records the "23/23 pass" line; this spec's
  // test (c) is the *browser-level* consistency pin that the 23 names
  // are the *whole* (the feed + detail + RSVP surface renders
  // coherently across the (a)/(b) flows).
  test('c. part-vs-whole — the feed + detail + RSVP surface renders coherently across the (a)/(b) flows', async ({ page, kumunita }) => {
    const author = await kumunita.signup('WhR.', 'wh-r@example.com', 'Passw0rd!');
    await kumunita.login(page, 'wh-r@example.com', 'Passw0rd!');

    // 1) Author creates a published event (the (a) closed-loop's
    //    precondition — the event is in the feed).
    await page.goto('/events/new');
    await page.locator('input[name="Title"]').fill('M4 part-vs-whole pin');
    await page.locator('textarea[name="Body"]').fill('The whole body.');
    await submitForm(page, 'form#new-event-form');
    const eventId = await kumunita.lastCreatedEventId();
    expect(typeof eventId).toBe('string');

    // 2) The feed row renders (the (a) closed-loop's step 2 pin — the
    //    feed surface is the *part* of the whole).
    await page.goto('/events');
    await expect(page.locator('div.airy-ann-row', {
      has: page.locator('h3.airy-ann-title a', { hasText: 'M4 part-vs-whole pin' }),
    })).toBeVisible();

    // 3) The detail page renders (the (a) closed-loop's step 3
    //    precondition — the detail surface is the *part* of the whole).
    //    The `h1` title + the `.rc-body` rendered body + the RSVP form
    //    are all present.
    await page.goto('/events/' + encodeURIComponent(eventId));
    await expect(page.locator('h1', { hasText: 'M4 part-vs-whole pin' })).toBeVisible();
    await expect(page.locator('.rc-body')).toContainText('The whole body.');
    await expect(page.locator('form:has(input[name="status"])')).toBeVisible();

    // 4) The RSVP form's three radios are present (the
    //    `#rsvp-Going` / `#rsvp-Maybe` / `#rsvp-No` pin — the
    //    `EventRsvp.Status` enum's three literals). The
    //    `button[type="submit"]` "Update" is the RSVP lane's trigger.
    await expect(page.locator('#rsvp-Going')).toBeVisible();
    await expect(page.locator('#rsvp-Maybe')).toBeVisible();
    await expect(page.locator('#rsvp-No')).toBeVisible();
    await expect(page.locator('form:has(input[name="status"]) button[type="submit"]', {
      hasText: /Update/i,
    })).toBeVisible();

    // 5) The RSVP "Responses" list is *absent* before any RSVP (the
    //    `Model.Rsvps.Count > 0` branch is the only render path for the
    //    list — the (a) closed-loop's step 4 precondition). The
    //    `h3.h6` "Responses" heading is the *observable* negative.
    await expect(page.locator('h3.h6', { hasText: /Responses/i })).toHaveCount(0);
  });
});
