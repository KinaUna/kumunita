// M2, plan U13 — e2e Playwright specs (browser-level, three specs).
//
// STATUS (honest — from U13's own entry read):
//   Authored against the *shipped* M1/M2 UI (selectors are taken
//   from the actual .cshtml — see the per-test comments). NOT yet
//   runnable: the M2/U13 entry read expected an *existing* M1
//   Playwright fixture, but `tests/Kumunita.Web.Tests/` holds only
//   xUnit .cs files, and `src/Kumunita.Web/package.json` is tsc-only
//   (no Playwright dep anywhere in the repo). Plan U13 line 177:
//   "if it does not, this is a Deviations finding about the M1
//   fixture and the unit pauses rather than extending the fixture
//   mid-U13."
//   U13 recorded that finding in m2-handoff-notes.md and did NOT
//   invent a fixture. Whoever lands the Playwright runtime next (the
//   M3 "Playwright e2e arrives later" unit — docs/ARCHITECTURE.md
//   line ~101) picks this file up; the test bodies below are the
//   frozen selector pins, do not re-write them to fit a different
//   fixture.
//
// Invariant anchors frozen by this file:
//   (a) directory round-trip — the §9 / C-M2·1 "contact block on
//       (opt-in) vs off (opted out)" contrast at the Web layer.
//   (b) C4 group-audience end-to-end — *the* handoff test M2's
//       design doc names (member sees, non-member does not).
//   (c) C4 group-membership end-to-end — "the profile that
//       Visibility-pointed at this group now appears in the member's
//       directory, not the non-member's" (strong-consistency, live
//       documents, no projection — M2 design doc line 102).
//
// Route / selector pins (from the shipped views — read the .cshtml
// first before "improving" them):
//   GET/POST /account/signup          (form: DisplayName, Email,
//                                      Password, ConfirmPassword)
//   GET    /account/verify/{id}       (token = M1 handoff; signed in
//                                      on success)
//   POST   /account/login             (form: Email, Password)
//   GET    /directory                 (li.list-group-item rows;
//                                      p.mt-3.small = HiddenCount
//                                      footer, only when count > 0)
//   GET    /directory/{subjectId}     (dl Email/Phone rows ONLY when
//                                      ShowContactBlock; opted-out
//                                      branch renders the "hasn't
//                                      shared a contact method"
//                                      p.text-muted paragraph)
//   GET/POST /profile/edit            (input#optin-contact;
//                                      textarea[name=Visibility.
//                                      Grants] — the JSON grants
//                                      field, U11's F13 pin)
//   GET    /groups                    POST /groups/create (form:
//                                      Name, Description)
//   GET    /groups/{id}
//   POST   /groups/{id}/add-member    (form field: subjectId)
//   POST   /groups/{id}/remove-member (form field: subjectId)

import { test, expect, Page } from '@playwright/test';

// ── Fixture shapes (documented; implementations live in the M1/M3
//    Playwright runtime — U13 does not extend the fixture by plan) ──
//
// A single `kumunita` fixture the three tests below consume, with the
// methods/fields the spec needs. This is the *entire* contract; it
// is deliberately small and bounded.
//
//   signup(displayName, email, password) ⇒ Promise<SignupHandle>
//     · POST /account/signup (the four form fields)
//     · flip the account to verified via the M1 token channel
//       (see "M1 token channel" note below the describe block)
//     · returns an opaque subjectId — a string, NOT a Guid (the M1
//       seam freeze; U7/U9/U10's deviation)
//
//   login(page, email, password) ⇒ Promise<void>
//     · clears the current context's cookies → fresh user in the
//       same page
//     · POST /account/login; lands signed in
//
//   lastCreatedGroupId() ⇒ Promise<string>
//     · the most-recently-created group's id — from U9's
//       `CreateGroupAsync` return value (`Group.Id`, a string, U9's
//       deviation). Without this, tests (b)/(c) fall back to
//       `window.kumunitaGroupId` (set by the page via
//       addInitScript). Prefer the fixture helper.
//
// ─────────────────────────────────────────────────────────────────────
// M1 token channel (the only cross-browser/DB helper the pure browser
// context cannot do): M1's `IMailerStage`/Wolverine
// `OutboxEmailHandler` writes the verify email into a durable outbox
// table. A pure browser context cannot read that table. The fixture
// MUST be able to read the `IdentityToken` row for the account under
// test (`Kind` = `KindVerify`) and either (i) drive
// `GET /account/verify/{id}` in the browser (the M1 handoff end), or
// (ii) flip `Profile.Verified` server-side + issue a sign-in cookie
// via `KumunitaClaimsPrincipalFactory` + `HttpContext.SignInAsync`.
// The e2e never sees `token.Token` directly — the fixture consumes
// it. This is the "signup" step above.
// ─────────────────────────────────────────────────────────────────────

interface SignupHandle {
  subjectId: string; // opaque string, NOT a Guid
}

interface Kumunita {
  signup(displayName: string, email: string, password: string):
    Promise<SignupHandle>;
  login(page: Page, email: string, password: string): Promise<void>;
  lastCreatedGroupId(): Promise<string>;
}

// Declare the `kumunita` fixture on `test` so the three test bodies
// can destructure `{ page, kumunita }` without any type error.
// The *shape* is the contract above (U13 pins it); the *value* is
// deliberately a runtime thrower — a stub that fails loudly so
// "this test is not yet runnable" is the correct state, not a
// silent no-op.
declare module '@playwright/test' {
  interface Fixtures {
    kumunita: Kumunita;
  }
}

test.use({
  kumunita: async ({}, use) => {
    throw new Error(
      'kumunita fixture not implemented. ' +
      'U13 authored the spec (the selector + route pins are frozen); ' +
      'the M1/M3 Playwright runtime unit must land the implementation ' +
      '`signup / login / lastCreatedGroupId` per the fixture contract ' +
      'in the header of e2e-m2.spec.ts. See m2-handoff-notes.md § U13.',
    );
    // `use` is required by the Playwright fixture API but is never
    // reached (the throw above fires first). TS exhaustiveness is
    // satisfied by the `return` below the throw (unreachable, but
    // the compiler requires it).
    return;
    // eslint-disable-next-line no-unreachable
    await use(undefined);
  },
});

// A tiny helper (spec-local, not fixture-local): submit the *single*
// form on the current page (the M1/M2 Razor views use Bootstrap forms
// with a single submit button each). Keeping it here — and not in the
// fixture — makes the spec's test bodies read top-to-bottom and keeps
// the fixture contract as small as possible.
async function submitForm(page: Page, scope?: string): Promise<void> {
  const scopeSel = scope ? `form:has(${scope})` : 'form';
  await page.locator(scopeSel).locator('button[type="submit"]')
    .first().click();
}

test.describe('M2 e2e', () => {
  // ── (a) Directory round-trip — contact block ON vs OFF ──────────
  // The author's own `Visibility` is the bootstrap self-only shape
  // (ADR 0001-B — the Owner branch is always Allow), so the author
  // always sees their own profile. Flipping the contact opt-in is
  // the *only* thing that changes whether the Email/Phone dl rows
  // render — the §2.4 "null ⇒ short-circuit" shape (C-M2·1) at the
  // Web layer.
  test('a. directory round-trip — contact block on (opt-in) and off (opted out)', async ({ page, kumunita }) => {
    // 1) Sign up + verify (the M1 handoff) + sign in.
    const alice = await kumunita.signup('Alice R.', 'alice@example.com', 'Passw0rd!');
    await kumunita.login(page, 'alice@example.com', 'Passw0rd!');

    // 2) Directory list — exactly one row (self-only bootstrap),
    //    with NO HiddenCount footer (zero hidden rows).
    await page.goto('/directory');
    const rows = page.locator('ul.list-group > li.list-group-item');
    await expect(rows).toHaveCount(1);
    await expect(rows.first()).toContainText('Alice R.');
    await expect(page.locator('p.mt-3.small')).toHaveCount(0);

    // 3) Contact block ON (opt-in): flip the switch (#optin-contact
    //    — U11's `OptInContactVisibility`) + save.
    await page.goto('/profile/edit');
    await page.locator('#optin-contact').check();
    await submitForm(page);

    // 4) Detail must now show the Email row (U8's §9 pin — the
    //    contact dl renders only when ShowContactBlock, i.e.
    //    the two-gate evaluation allowed both gates).
    await page.goto('/directory/' + encodeURIComponent(alice.subjectId));
    await expect(page.locator('dt', { hasText: 'Email' })).toBeVisible();

    // 5) Contact block OFF (opted out): uncheck + save; the §2.4
    //    "null ⇒ short-circuit" shape returns → the Email/Phone dl
    //    rows must NOT render, and the "hasn't shared a contact
    //    method" muted paragraph MUST (U8's Detail.cshtml else-branch).
    await page.goto('/profile/edit');
    await page.locator('#optin-contact').uncheck();
    await submitForm(page);

    await page.goto('/directory/' + encodeURIComponent(alice.subjectId));
    await expect(page.locator('dt', { hasText: 'Email' })).toHaveCount(0);
    await expect(page.locator('dt', { hasText: 'Phone' })).toHaveCount(0);
    await expect(page.locator('p.text-muted',
      { hasText: /hasn't shared a contact method/i })).toBeVisible();
  });

  // ── (b) C4 group-audience end-to-end — the handoff test ─────────
  // Author sets `Visibility` to `Any + {Kind:Group, Id:<group-id>}`
  // through U11's `_AudienceEditor` (the grants textarea JSON shape
  // — U11's F13 single-source pin). A *member* added to the group
  // sees the profile on the next directory load; a *non-member*
  // does not. This is the C4 invariant at the Web surface.
  test('b. C4 — group visibility: member sees, non-member does not', async ({ page, kumunita }) => {
    const author   = await kumunita.signup('Author',   'author@example.com',   'Passw0rd!');
    const member   = await kumunita.signup('Member',   'member@example.com',   'Passw0rd!');
    const outsider = await kumunita.signup('Outsider', 'outsider@example.com', 'Passw0rd!');

    await kumunita.login(page, 'author@example.com', 'Passw0rd!');

    // 1) Author creates a group (U9).
    await page.goto('/groups/create');
    await page.locator('input[name="Name"]').fill('Building 4');
    await submitForm(page);

    // The newly-created group id — fixture helper preferred, with a
    // `window`-scoped fallback (the "lastCreatedGroupId" contract
    // above — either one is fine, the spec supports both).
    let groupId: string;
    try { groupId = await kumunita.lastCreatedGroupId(); }
    catch { groupId = await page.evaluate(() => (window as any).kumunitaGroupId!); }
    expect(typeof groupId).toBe('string');
    expect(groupId.length).toBeGreaterThan(0);

    // 2) Author adds the *member* (U10). The `#add-subjectId` input
    //    is the [FromForm] subjectId (U10's detail pin; the owner id
    //    the seam takes is the actor's own subject, minted from the
    //    signed-in principal — never a form field).
    await page.goto('/groups/' + encodeURIComponent(groupId));
    await page.locator('#add-subjectId').fill(member.subjectId);
    await submitForm(page, 'form:has(#add-subjectId)');

    // 3) Author sets `Visibility` to Any + {Kind:Group, Id:<group>}.
    //    (The `Visibility.Mode` radio's default is Any, so we only
    //    need to write the grants textarea — the F13 pin.)
    await page.goto('/profile/edit');
    await page.locator('textarea[name="Visibility.Grants"]')
      .fill(JSON.stringify([{ Kind: 'Group', Id: groupId }]));
    await submitForm(page, 'form:has(textarea[name="Visibility.Grants"])');

    // 4) Member's next directory load: author is visible.
    await kumunita.login(page, 'member@example.com', 'Passw0rd!');
    await page.goto('/directory');
    await expect(page.locator('ul.list-group > li.list-group-item'))
      .toContainText('Author');

    // 5) Non-member's next directory load: author is NOT visible.
    await kumunita.login(page, 'outsider@example.com', 'Passw0rd!');
    await page.goto('/directory');
    await expect(page.locator('ul.list-group > li.list-group-item'))
      .not.toContainText('Author');
  });

  // ── (c) C4 group-membership end-to-end — the "next request" ─────
  // Contrast with (b): the author's `Visibility` already points at
  // the group (a STABLE audience). The *membership add* is the
  // trigger that flips the member's directory. The directory MUST
  // reflect that change on the very next request — the C4
  // strong-consistency pin (M2 design doc line 102).
  test('c. C4 — add group member; directory reflects on the next request', async ({ page, kumunita }) => {
    const owner    = await kumunita.signup('Owner',    'owner@example.com',    'Passw0rd!');
    const resident = await kumunita.signup('Resident', 'resident@example.com', 'Passw0rd!');
    const outsider = await kumunita.signup('Outs',     'out2@example.com',     'Passw0rd!');

    await kumunita.login(page, 'owner@example.com', 'Passw0rd!');

    // 1) Owner creates a group (U9).
    await page.goto('/groups/create');
    await page.locator('input[name="Name"]').fill('Bike owners');
    await submitForm(page);
    let groupId: string;
    try { groupId = await kumunita.lastCreatedGroupId(); }
    catch { groupId = await page.evaluate(() => (window as any).kumunitaGroupId!); }
    expect(groupId.length).toBeGreaterThan(0);

    // 2) Owner adds the resident to the group (U10) FIRST — this is
    //    the "membership change" the (4)/ (5) assertions observe.
    await page.goto('/groups/' + encodeURIComponent(groupId));
    await page.locator('#add-subjectId').fill(resident.subjectId);
    await submitForm(page, 'form:has(#add-subjectId)');

    // 3) Owner's `Visibility` already points at the group (a stable
    //    audience — no mid-test edit; the contrast with (b)).
    await page.goto('/profile/edit');
    await page.locator('textarea[name="Visibility.Grants"]')
      .fill(JSON.stringify([{ Kind: 'Group', Id: groupId }]));
    await submitForm(page, 'form:has(textarea[name="Visibility.Grants"])');

    // 4) Member's directory: owner's profile is visible.
    await kumunita.login(page, 'resident@example.com', 'Passw0rd!');
    await page.goto('/directory');
    await expect(page.locator('ul.list-group > li.list-group-item'))
      .toContainText('Owner');

    // 5) Non-member's directory: owner's profile is NOT visible.
    await kumunita.login(page, 'out2@example.com', 'Passw0rd!');
    await page.goto('/directory');
    await expect(page.locator('ul.list-group > li.list-group-item'))
      .not.toContainText('Owner');
  });
});
