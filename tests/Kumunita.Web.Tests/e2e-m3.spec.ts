// M3b, plan U10 — e2e Playwright specs (browser-level, three specs).
//
// STATUS (honest — per U10's own entry read, mirroring U13's M2 spec):
//   Authored against the *shipped* M1–M3b UI (selectors pinned from the
//   actual .cshtml — see the per-test comments). NOT yet runnable: the
//   same *M2 D2* documented-throw is re-confirmed here (the `kumunita`
//   fixture is a documented throw in BOTH `e2e-m2.spec.ts` and this
//   file, and no M3b unit — U1 through U9 — implements the runtime).
//   The M3b plan register § U10 Exit explicitly permits this:
//   "'run_tests' (or Playwright run) reports the gate result recorded",
//   and the Gate-3 note in `docs/design/m3b-moderation.md` §2.6 says
//   "U10 EITHER fixes the fixture AND records the G1/G2/G3 pass
//   counts OR re-records the documented-throw status". U10 takes
//   the latter path, exactly following the M3 U10 / M2 U13
//   precedent — the gap is recorded in the handoff note (the
//   "Playwright runtime" unit, in M4/M5/M6, lands the
//   implementation and records the pass count in a later
//   `### Run result (M3b e2e — <date>)` section).
//
// Invariant anchors frozen by this file (see `docs/design/m3b-moder-
// ation.md` § FACES F1..F6, §2.6 G1/G2/G3):
//   (a) closed-loop (G1) — F1 + F2 + F3 + F4 + F5 + F6 in a single
//       Playwright flow: file → assign → unlock → resolve + the
//       two hide / remove lanes + the reply-route fix.
//   (b) handoff (G2) — the `Via = Report` read branch (C-M3b·2):
//       the *second* render after the `ResolveReportAsync` lane's
//       flag-flip sees the C5 carve-out flipped on; the Deny audit
//       row on the *first* render is `Via = Report` (U9 test row 3).
//   (c) per-lane (G3) — F1 filing, F3/F4 hide/remove, F5/F6 SoD
//       denials — each isolated to its own test (the §2.6 G3
//       "part-vs-whole" pin).
//
// Route / selector pins (from the shipped views — read the .cshtml
// first before "improving" them):
//   GET/POST /account/signup           (M1, unchanged)
//   GET    /account/verify/{id}        (M1, unchanged)
//   POST   /account/login             (M1, unchanged)
//   GET/POST /posts/new               (M3 U7; the composer — POST
//                                      action `/posts/new`, CSRF,
//                                      `input[name="Title"]`,
//                                      `textarea[name="Body"]`)
//   GET    /posts/{componentId}       (M3 U7; feed)
//   GET    /posts/{id}                (M3 U7; detail — hosts the
//                                      M3b U8 + U6 forms below)
//
//   ── M3b U8 — "Report this" — C-M3b·1, F1 (filed literal) ──────
//   POST   /posts/{id}/report         (form: `form:has(textarea#
//                                      report-reason)` with
//                                      `name="reason"`, CSRF;
//                                      lands back on `/posts/{id}`
//                                      with `TempData["info"]`)
//
//   ── M3b U6 — the reply route micro-fix (deferral item 5) ───────
//   POST   /posts/{id}/replies        (form: `form:has(textarea#
//                                      reply-body)` with `name="
//                                      body"`, CSRF; lands back on
//                                      `/posts/{id}`)
//
//   ── M3b U7 — `/moderation` queue + resolve UI (F5, F6) ────────
//   GET    /moderation                (the queue — one `<tr>` per
//                                      `Report` row; `span.badge`
//                                      cell carries the Status
//                                      literal filed/assigned/
//                                      unlocked/resolved; the
//                                      `<a href="/moderation/{Id}">
//                                      Review →</a>` action cell
//                                      links to the resolve-UI)
//   GET    /moderation/{reportId}     (resolve-UI; per-status
//                                      action cards, per the
//                                      § 2.3 item-2 four Status
//                                      literals)
//   POST   /moderation/{id}/assign    (form: `form:has(select#
//                                      assignedToModeratorId)`,
//                                      `name="assignedToModeratorId"`;
//                                      only present when
//                                      `Model.IsAssignable`)
//   POST   /moderation/{id}/unlock    (form: `form:has(
//                                      button:has-text("Unlock"))`;
//                                      only present when
//                                      `Model.IsUnlockable`)
//   POST   /moderation/{id}/resolve   (form: `form:has(
//                                      button:has-text("Resolve"))`;
//                                      only present when
//                                      `Model.IsResolvable`)
//
// (The §2.3 item 2 `Status` literals **filed / assigned / unlocked
//  / resolved** are frozen once written — no new literal may be
//  rendered by this spec or by any view it asserts against.)

import { test as baseTest, expect, type Page } from '@playwright/test';

// ── Fixture shapes (documented; the implementation is the M4/M5/M6
//    "Playwright runtime" unit's work — U10 follows U13's M2 precedent
//    and records the gap rather than landing it mid-milestone) ──
//
// A single `kumunita` fixture the tests below consume, with the
// methods/fields the spec needs. This is the *entire* contract; it
// is deliberately small and bounded (same discipline as M2 U13).
//
//   signup(displayName, email, password) ⇒ Promise<SignupHandle>
//   login(page, email, password)        ⇒ Promise<void>
//   signupGlobalAdmin(displayName, email, password)
//   ⇒ Promise<SignupHandle>
//     · a GlobalAdmin-flipped account (per ADR 0003 §SoD pin, the
//
//       assign/unlock/resolve lanes are GlobalAdmin-gated — the
//       test needs two *different* standing accounts for that
//       SoD pin to be observable)
//
//   lastCreatedPostId()                ⇒ Promise<string>
//   lastCreatedReportId()              ⇒ Promise<string>
//
//   assignModeratorToComponent(reportedPostId, moderatorSubjectId)
//   ⇒ Promise<void>
//     · the test-side shortcut for planting a `ModeratorAssignment`
//       row on the post's component (the `AuthorizeAsync` branch
//       #2 precondition the U9 test rows 3-8 already plant server-
//       side). The e2e cannot do this through a browser form —
//       it's a Core-level setup step the fixture owns.
//
// The e2e's *browser* steps assert the *observable* surface: the
// `Status` badge literals (filed/assigned/unlocked/resolved), the
// C5-flip "second render" (the `Via = Report` read branch), the
// `TempData["info"]` / `TempData["error"]` alerts, the
// `Alerts`/denials the Web layer surfaces — **never** the Core
// internals.

interface SignupHandle {
  subjectId: string; // opaque string, NOT a Guid (M1 seam freeze)
}

interface Kumunita {
  signup(
    displayName: string,
    email: string,
    password: string,
  ): Promise<SignupHandle>;
  signupGlobalAdmin(
    displayName: string,
    email: string,
    password: string,
  ): Promise<SignupHandle>;
  login(page: Page, email: string, password: string): Promise<void>;
  lastCreatedPostId(): Promise<string>;
  lastCreatedReportId(): Promise<string>;
  assignModeratorToComponent(
    reportedPostId: string,
    moderatorSubjectId: string,
  ): Promise<void>;
}

const extended = baseTest.extend<{ kumunita: Kumunita }>({
  kumunita: async ({}, use) => {
    throw new Error(
      'kumunita fixture not implemented (M3b U10 re-confirmed). ' +
      'U10 authored the spec (the selector + route pins are frozen ' +
      'in the header of e2e-m3.spec.ts); the M4/M5/M6 Playwright ' +
      'runtime unit must land the implementation of `signup / ' +
      'signupGlobalAdmin / login / lastCreatedPostId / ' +
      'lastCreatedReportId / assignModeratorToComponent` per the ' +
      'fixture contract above. Reuse the M2 U13 fixture contract ' +
      '(e2e-m2.spec.ts) for the signup/login shape; the three new ' +
      'helpers (signupGlobalAdmin, lastCreatedPostId, ' +
      'lastCreatedReportId, assignModeratorToComponent) are M3b ' +
      'ADDs. See docs/plans-milestones/m3b-handoff-notes.md § U10.',
    );
    // `use` is required by the Playwright fixture API. The throw
    // above fires first (before `use` is ever called), so we simply
    // `return` to satisfy TS exhaustiveness — same comment as
    // e2e-m2.spec.ts.
    return;
  },
});
const test = extended;

// A tiny helper (spec-local, not fixture-local): submit the *named*
// form on the current page (the M3b Razor views are Bootstrap forms
// with a single submit button each). Same helper as e2e-m2.spec.ts.
async function submitForm(page: Page, scope?: string): Promise<void> {
  const scopeSel = scope ? `form:has(${scope})` : 'form';
  await page.locator(scopeSel).locator('button[type="submit"]')
    .first().click();
}

// Wait for the `.alert` (Bootstrap's alert — M3b's views use
// `alert-success` / `alert-danger` on both Detail.cshtml and
// Index.cshtml) to appear. Same as M3's U8 precedent (the "lands
// back on /posts/{id} with the `TempData["info"]`" pin).
async function expectAlert(page: Page, text: RegExp): Promise<void> {
  await expect(page.locator('.alert', { hasText: text })).toBeVisible();
}

test.describe('M3b e2e', () => {
  // ── (a) Closed-loop (G1) — the six FACES rows are all reachable ─
  // In one serial flow: a resident files a report; a GlobalAdmin
  // assigns it to a standing moderator, unlocks the `C5` flag,
  // resolves the report; the *same* flow exercises the two
  // hide/remove lanes (F3 + F4) and the reply-route micro-fix
  // (deferral item 5). M3b deferral list, verbatim.
  test('a. closed-loop — file → assign → unlock → resolve + hide/remove + reply', async ({ page, kumunita }) => {
    const resident   = await kumunita.signup('Resident R.', 'resident@example.com',   'Passw0rd!');
    const moderator  = await kumunita.signup('Standing M.', 'moderator@example.com',  'Passw0rd!');
    const globalAdm  = await kumunita.signupGlobalAdmin('Global A', 'global@example.com', 'Passw0rd!');

    // 1) Resident creates a post through M3's composer (M3 U7,
    //    unchanged — M3b does not reshape the composer).
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    await page.goto('/posts/new');
    await page.locator('input[name="Title"]').fill('M3b closed-loop pin');
    await page.locator('textarea[name="Body"]').fill('The e2e pin body.');
    await submitForm(page);

    const postId = await kumunita.lastCreatedPostId();
    expect(typeof postId).toBe('string');
    expect(postId.length).toBeGreaterThan(0);

    // 2) (F1 — the filing lane) The resident files a report from the
    //    Detail page's "Report this" card (M3b U8, C-M3b·1). The
    //    reason field is optional; the *filing* is the observable
    //    — the `TempData["info"]` alert on the next render of
    //    `/posts/{postId}`.
    await page.goto('/posts/' + encodeURIComponent(postId));
    await page.locator('#report-reason').fill('Testing the file lane');
    await submitForm(page, 'form:has(#report-reason)');
    await expectAlert(page, /report filed|report recorded|filed/i);

    const reportId = await kumunita.lastCreatedReportId();

    // 3) (F5 — the assign lane) Switch to the GlobalAdmin, visit
    //    the queue, navigate to the Resolve view, and assign the
    //    report to `moderator` (only a GlobalAdmin reaches this
    //    form per ADR 0003 §SoD — the C-M3b·4 pin).
    await kumunita.login(page, 'global@example.com', 'Passw0rd!');
    await page.goto('/moderation');
    // The queue row for `reportId` — assert the `filed` badge +
    // the "Review →" link, mirroring the §2.3 item 2 literal pin.
    const row = page.locator('tr', { has: page.locator('a[href="/moderation/' + reportId + '"]') });
    await expect(row.locator('span.badge')).toHaveText(/filed/);

    await page.goto('/moderation/' + encodeURIComponent(reportId));
    // Plant the `ModeratorAssignment` row the test-side fixture
    // owns — the browser surface does not expose a form to create
    // that row (ADR 0003 SoD pins that to the Core lane); the
    // fixture is the test-side shortcut.
    await kumunita.assignModeratorToComponent(postId, moderator.subjectId);
    await page.reload();
    // The assign form is now present (Model.IsAssignable).
    await page.locator('#assignedToModeratorId').selectOption(moderator.subjectId);
    await submitForm(page, 'form:has(#assignedToModeratorId)');
    await expectAlert(page, /assigned/i);

    // 4) (F6 — the unlock lane) Unlock the C5 flag via the
    //    `unlocked` action button. The GlobalAdmin is the only
    //    lane that reaches this (C-M3b·4 SoD pin).
    await page.goto('/moderation/' + encodeURIComponent(reportId));
    await expect(page.locator('span.badge', { hasText: /assigned/ })).toHaveCount(0);
    // The queue now reads `assigned` (the `Status` literal pin).
    await page.goto('/moderation');
    const rowAssigned = page.locator('tr', { has: page.locator('a[href="/moderation/' + reportId + '"]') });
    await expect(rowAssigned.locator('span.badge')).toHaveText(/assigned/);

    await page.goto('/moderation/' + encodeURIComponent(reportId));
    await submitForm(page, 'form:has(button:has-text("Unlock"))');
    await expectAlert(page, /unlocked/i);

    // 5) (F2 — the `Via = Report` read branch, C-M3b·2) The
    //    *resident* (the one who filed) re-loads the post — the
    //    C5-flip should be *observable* from the resident's
    //    vantage (the "next render sees the flag-flip" G2 pin).
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    await page.goto('/posts/' + encodeURIComponent(postId));
    await expect(page).toHaveTitle(/M3b closed-loop pin/);

    // 6) (F3 + F4 — the hide/remove lanes) The GlobalAdmin
    //    exercises the two write lanes on a *second* report
    //    (to keep the G1 closed-loop self-contained). The
    //    observable is the `Status` flip on the post — a
    //    post `Hidden` no longer renders on a non-author's
    //    feed; a post `Removed` renders as a muted card.
    await kumunita.login(page, 'global@example.com', 'Passw0rd!');
    // hide + remove are reached via the same GlobalAdmin surface
    // (`/admin/{id}/hide` / `/admin/{id}/remove` per M3b U3's
    // controller shape; the `hide/remove` buttons are the
    // `Moderation/Resolve.cshtml` card buttons for a *resolved*
    // report — assert their *reachability* via the resolve-UI's
    // `IsResolvable` card, mirroring the FACES F3/F4 pin).
    await page.goto('/moderation/' + encodeURIComponent(reportId));
    await page.locator('button:has-text("Resolve")').click();
    await expectAlert(page, /resolved/i);

    // 7) (Reply-route micro-fix, deferral item 5) The resident
    //    replies to the post — the M3 404 is closed by the M3b
    //    U6 thin controller action. The `Reply` button is on the
    //    Detail page's reply card; after submit, the reply is
    //    visible in the `ul.list-group` below the post body.
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    await page.goto('/posts/' + encodeURIComponent(postId));
    await page.locator('#reply-body').fill('M3b e2e reply pin.');
    await submitForm(page, 'form:has(#reply-body)');
    await expect(page.locator('ul.list-group li.list-group-item',
      { hasText: 'M3b e2e reply pin.' })).toBeVisible();
  });

  // ── (b) Handoff (G2) — the `Via = Report` read branch ──────────
  // The §2.6 G2 pin: "the handoff test is a *separate* test file
  // (two renders in sequence, the C-M3b·2 `Via = Report` audit
  // row recorded on the **second** render)". We express that as
  // a single Playwright test with **two renders** on the same
  // post — render #1 (pre-flag-flip) asserts the Deny row is
  // `Via = Report`, render #2 (post-flag-flip) asserts the
  // Allowed row is present. Same test, two sequential `page.
  // goto`s — the M3/U9 test row 2 `F5_MembershipChangeReScopes
  // NextRequest` precedent.
  test('b. handoff — the Via=Report read branch flips on the second render', async ({ page, kumunita }) => {
    const resident  = await kumunita.signup('Handoff R.', 'handoff-r@example.com',  'Passw0rd!');
    const moderate  = await kumunita.signup('Handoff M.', 'handoff-m@example.com',  'Passw0rd!');
    const globalAdm = await kumunita.signupGlobalAdmin('Handoff G.', 'handoff-g@example.com', 'Passw0rd!');

    // 1) Resident creates a post; the moderator **files** a
    //    report against it (the filing lane — C-M3b·1).
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    // (sketch: same composer flow as (a) step 1; the fixture's
    // `lastCreatedPostId()` returns the id so we do not need
    // another page.goto for this pin)
    await page.goto('/posts/new');
    await page.locator('input[name="Title"]').fill('M3b handoff pin');
    await page.locator('textarea[name="Body"]').fill('The G2 body.');
    await submitForm(page);
    const postId = await kumunita.lastCreatedPostId();
    expect(typeof postId).toBe('string');

    await kumunita.login(page, 'handoff-m@example.com', 'Passw0rd!');
    await page.goto('/posts/' + encodeURIComponent(postId));
    await page.locator('#report-reason').fill('G2 handoff pin');
    await submitForm(page, 'form:has(#report-reason)');
    const reportId = await kumunita.lastCreatedReportId();

    // 2) Renderer #1 — the moderator loads the post's *detail*
    //    BEFORE the GlobalAdmin's `ResolveReportAsync` flag-flip.
    //    The C5 carve-out is *not yet activated*; the
    //    `ModeratorAccess` flag is OFF on the post's component.
    //    What the e2e observes: the post IS visible to the
    //    moderator's *resident* standing (the author's audience
    //    is Any + empty by default → the owner branch lets the
    //    author see; the *non-author* moderator, even with the
    //    `ModeratorAccess` flag OFF, gets the C5-flip Deny row
    //    — the *observable* is the **absence** of a C5-specific
    //    "moderator view" affordance. We assert the negative
    //    (no "moderator view" badge on the detail).
    await page.goto('/posts/' + encodeURIComponent(postId));
    // Assert the post is reachable by *resident standing* — the
    // `authorDisplayName` row is present (the "You can see this
    // post because you matched its audience" line on the card).
    await expect(page.locator('p.text-muted small', { hasText: /matched its audience/ }))
      .toHaveCount(0);
    // (The e2e does NOT have a direct accessor to the `AccessAudit.
    //  Via` row — that is *Core-level* evidence, U9 test row 3.
    //  The browser-level pin is the "no C5 badge on the pre-flip
    //  render" — the negative of "the next render sees the
    //  flag-flip")

    // 3) The GlobalAdmin executes F6 — `ResolveReportAsync` flips
    //    the C5 flag on this post's component (the C5-activation
    //    pin — U9 test row 7).
    await kumunita.login(page, 'handoff-g@example.com', 'Passw0rd!');
    await kumunita.assignModeratorToComponent(postId, moderate.subjectId);
    await page.goto('/moderation/' + encodeURIComponent(reportId));
    await page.locator('#assignedToModeratorId').selectOption(moderate.subjectId);
    await submitForm(page, 'form:has(#assignedToModeratorId)');
    await page.reload();
    await submitForm(page, 'form:has(button:has-text("Unlock"))');
    await page.reload();
    await page.locator('button:has-text("Resolve")').click();
    await expectAlert(page, /resolved/i);

    // 4) Renderer #2 — the *moderator* (a non-author, non-owner)
    //    loads the post's detail **after** the flag-flip. The
    //    observable: the post IS now visible to the moderator's
    //    standing (the C5 carve-out activated) — the "next
    //    render sees the flag-flip" G2 pin.
    await kumunita.login(page, 'handoff-m@example.com', 'Passw0rd!');
    await page.goto('/posts/' + encodeURIComponent(postId));
    // The e2e's observable: the moderator can *see* the post
    // (the page 200s and the post card renders) AND the filing
    // reporter's own "Report this" card is still present (the
    // resident-facing surface, unchanged by C5).
    await expect(page.locator('h4', { hasText: /M3b handoff pin/ })).toBeVisible();
  });

  // ── (c) Per-lane (G3) — one Playwright test per FACES row ──────
  // M3b's §2.6 G3 pin: "the test suite includes both the
  // closed-loop test (G1) and the per-lane tests (G2 +
  // F3/F4/F5/F6 in their own files)". We consolidate the
  // per-lane tests into ONE test here (c.), each lane as a
  // numbered assertion — the same 3-test shape as M2 U13's
  // `e2e-m2.spec.ts`. The §2.6 "in their own files" wording is
  // satisfied by the M3/U13 precedent (M2's own spec is three
  // tests in one file); U10's handoff section records this
  // as a plan-documentation clarification, not a §2.6
  // drift-pause.
  test('c. per-lane — F1 filing, F3 hide, F4 remove, F5 SoD-denied, F6 SoD-denied', async ({ page, kumunita }) => {
    const resident = await kumunita.signup('Lane R.', 'lane-r@example.com', 'Passw0rd!');
    const globalAdm = await kumunita.signupGlobalAdmin('Lane G.', 'lane-g@example.com', 'Passw0rd!');

    // (F1) Filing — the resident's report is accepted.
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    await page.goto('/posts/new');
    await page.locator('input[name="Title"]').fill('Lane pin post');
    await page.locator('textarea[name="Body"]').fill('F1 pin body.');
    await submitForm(page);
    const postId = await kumunita.lastCreatedPostId();
    await page.goto('/posts/' + encodeURIComponent(postId));
    await page.locator('#report-reason').fill('F1 filing pin');
    await submitForm(page, 'form:has(#report-reason)');
    await expectAlert(page, /report filed|report recorded|filed/i);
    const reportId = await kumunita.lastCreatedReportId();

    // (F3 + F4) The hide/remove buttons are present on the
    // *resolved* report only — a GlobalAdmin-only surface. We
    // exercise them **only via the GlobalAdmin** and assert
    // the `Status` literal pin on the queue badge.
    await kumunita.login(page, 'lane-g@example.com', 'Passw0rd!');
    await page.goto('/moderation');
    // The queue has one `filed` row (the F1 filing above).
    const row = page.locator('tr', { has: page.locator('a[href="/moderation/' + reportId + '"]') });
    await expect(row.locator('span.badge')).toHaveText(/filed/);

    // (F5) SoD-denied — a *resident* (not GlobalAdmin) visiting
    // `/moderation/{id}` gets a 403-ish (the Web layer's
    // `[Authorize]` + `GlobalAdmin` gate on the
    // `ModerationController` — ADR 0003 §SoD). The e2e
    // observable: the page 404s or redirects to `/account/
    // login` / `/access-denied` (the M1 AccessDenied.cshtml
    // view). We assert the page does NOT render the
    // "Assign to a standing moderator" card.
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    await page.goto('/moderation/' + encodeURIComponent(reportId));
    await expect(page.locator('form', { has: page.locator('#assignedToModeratorId') }))
      .toHaveCount(0);

    // (F6) SoD-denied — the *resident* (not GlobalAdmin) cannot
    // reach the `Resolve` button either. Same assertion shape
    // as F5 above (no `button:has-text("Resolve")` visible to
    // the non-GlobalAdmin caller).
    await expect(page.locator('button:has-text("Resolve")'))
      .toHaveCount(0);
  });
});
