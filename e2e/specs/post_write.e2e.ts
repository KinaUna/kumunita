// F19.U3 — Flow B (pin §2.2, verbatim): a STANDALONE write-leg spec.
//
// The pinned spec name (pin §3): `post_write.e2e.ts`.
// The pinned test title (pin §3): "a signed-in resident publishes one post
// and it appears".
//
// Flow B is SELF-CONTAINED (pin §2.2): it signs in with a KNOWN sample
// resident (the compose `SampleData__Enabled: "true"` seeder's credentials)
// and then performs pin §2.1 step 5 verbatim — the write leg — re-using the
// SAME shared helper Flow A calls (`writePostViaBrowser` in
// `helpers/post-write.ts`). One body, two callers (pin §4's DRY point).
//
// The sample-resident choice (pin §2.2: "U must read SampleDataSeeder.cs to
// pick one resident (non-admin) account's email+password"): the seeder's
// residents all share `ResidentPassword = "Resident123!"` and have no
// `role` field (unlike the GlobalAdmin / Moderator / Translator accounts).
// We pick `anna@examplium.com` — a plain verified resident. (Recorded in the
// handoff note, per the pin §2.2 requirement.)
//
// Why a standalone spec (pin §2.2, the pinned rationale): Flow A couples the
// write to the signup/verify/login chain — if the signup gate is closed or
// the mail leg breaks, the write leg never runs and the failure is
// mis-attributed. Flow B ISOLATES the write lane: a green B + red A localizes
// to the identity/mail chain; a red B + green A localizes to the write lane.
//
// This spec does NOT call the signup-gate precondition (pin §2.2 — the gate
// is a Flow A concern only), and does NOT call Mailpit (no verify link
// needed — the resident already exists).
//
// One login POST per run (pin §5 invariant #6): a 503 (ASP.NET Identity
// lockout) is a precondition/environment failure, recorded with the
// doctrine's "STOP retrying" note, never retried in a loop.
//
// The rendered HTML is the evidence (pin §5 invariant #7): the terminal
// assertion is on the rendered `/community` feed, not on a `.cshtml` source
// read or a DB query. The suite never opens Postgres.

import { test, expect } from '@playwright/test';
import { writePostViaBrowser } from '../helpers/post-write';

// The sample resident (non-admin) — from SampleDataSeeder.cs (pin §2.2).
// All residents share ResidentPassword = "Resident123!".
const RESIDENT_EMAIL = 'anna@examplium.com';
const RESIDENT_PASSWORD = 'Resident123!';

// The run-unique post Title/Body (pin §5 invariant #4 — re-runnable +
// unambiguous evidence on a re-used compose volume).
const RUN_ID = Date.now().toString(36);
const RUN_TITLE = `E2E flow-B post ${RUN_ID}`;
const RUN_BODY = `E2E flow-B body ${RUN_ID} — this post is the terminal evidence of Flow B (pin §2.2).`;

test('a signed-in resident publishes one post and it appears', async ({ page }) => {
  // ── Sign in (Flow B, standalone) — the seeded sample resident ──
  // Navigate to /account/login, fill Email + Password, submit. Assert the
  // controller's Redirect("/profile/edit") (verified in Flow A's step 4 —
  // the same authenticated surface). ONE login POST (pin §5 invariant #6).
  await page.goto('/account/login', { waitUntil: 'domcontentloaded' });
  await page.fill('input[name="Email"]', RESIDENT_EMAIL);
  await page.fill('input[name="Password"]', RESIDENT_PASSWORD);

  const loginResponse = await Promise.resolve(
    // Scope to the login form: the nav language-picker carries its own
    // button[type="submit"] dropdown items, and an unscoped selector grabs
    // the first (invisible) one and times out.
    //
    // The view is written as <form asp-action="Login"> (Views/Account/Login.cshtml),
    // but the Razor tag helper CONSUMES asp-action and emits the routed
    // action="/Account/Login" — asp-action is NOT present in the served HTML,
    // so a form[asp-action=...] selector matches nothing in a real browser.
    // Scope by the rendered action value instead (exact match also keeps
    // this off the sibling LoginMfa form, which posts to /Account/LoginMfa).
    page.click('form[action="/Account/Login"] button[type="submit"]'),
  );
  // A 503 (ASP.NET Identity lockout) is a PRECONDITION/ENVIRONMENT failure —
  // record it, do NOT retry (pin §5 invariant #6 + the repo memory
  // env-docker-ts7-and-portbind "rapid login POSTs trip the lockout").
  if (loginResponse && loginResponse.status() === 503) {
    throw new Error(
      'login POST returned 503 (ASP.NET Identity lockout). This is a ' +
      'PRECONDITION/ENVIRONMENT failure (a prior run left the resident ' +
      'account in lockout), NOT a test failure — STOP retrying (pin §5 ' +
      'invariant #6). Reset the lockout on the compose volume and re-run.',
    );
  }
  expect(
    loginResponse,
    'the login submit should produce a navigation response (a redirect to /profile/edit; a missing response is a real failure)',
  ).toBeTruthy();
  expect(
    page.url(),
    'after login, the browser should be on /profile/edit (the controller\'s Redirect("/profile/edit"))',
  ).toContain('/profile/edit');

  // ── One write (pin §2.1 step 5, verbatim — the shared write leg) ──
  // The SAME helper Flow A calls (helpers/post-write.ts): navigate to
  // /posts/new, fill Title + Body (run-unique strings), submit, assert the
  // response is a redirect (not a validation re-render). One body, two
  // callers (pin §4's DRY point).
  await writePostViaBrowser(page, { title: RUN_TITLE, body: RUN_BODY });

  // ── The terminal evidence (pin §2.2: "assert the rendered post") ──
  // The community feed at /community is the PostsController paged-feed route
  // (pin §2.1 step 5). The post's audience defaults (Mode="Any",
  // CommunityVisible=true) make it visible in the community feed. The
  // rendered-HTML-is-the-evidence doctrine (pin §5 invariant #7) — the
  // suite never opens Postgres.
  await page.goto('/community', { waitUntil: 'domcontentloaded' });
  const communityBody = await page.locator('body').textContent();
  expect(
    communityBody,
    `the community feed should render the run-unique post Title "${RUN_TITLE}" (the terminal evidence — pin §2.2, the rendered-HTML-is-the-evidence doctrine)`,
  ).toContain(RUN_TITLE);
});
