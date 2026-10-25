// F19.U2 — the SHARED write-leg (pin §2.1 step 5 + pin §4 "the §2.1 step 5
// write-body" is the shared helper U3 re-uses).
//
// The write leg is the flow's terminal assertion (pin §2.1 step 5):
// navigate to `/posts/new`, fill `Title` + `Body` (run-unique strings — the
// re-runnable + unambiguous evidence invariant, pin §5 #4), submit, and
// assert the response is a redirect (not a validation re-render). The
// rendered post (the run-unique Title on the community feed or the redirect
// target) is the terminal evidence — the rendered-HTML-is-the-evidence
// doctrine (pin §5 #7). The suite never opens Postgres.
//
// U3 (Flow B) calls this SAME function — one body, two callers (pin §4's
// DRY point).
//
// The real view field names (grep-verified in `Views/Posts/New.cshtml`,
// pin §2.1 step 5): `#new-post-form` → `action="/posts/new"`, with required
// `select name="ComponentId"` (seeded — the sample-data neighborhood has
// components), `input name="Title"` (optional), `textarea name="Body"`
// (required — the controller adds a ModelState error if empty),
// `select name="LanguageCode"`, and the `Audience.*` checkboxes (the model
// defaults `Mode="Any"`, `CommunityVisible=true` — the defaults are
// sufficient; the spec does NOT touch the audience fields).

import type { Page } from '@playwright/test';

export interface WritePostInput {
  title: string; // run-unique (pin §5 #4)
  body: string; // run-unique (pin §5 #4)
}

/**
 * Perform the §2.1 step 5 write leg: navigate to `/posts/new`, fill
 * `Title` + `Body`, submit, and assert the response is a redirect (not a
 * validation re-render). Returns the redirect target URL (the caller then
 * asserts the rendered post — the terminal evidence).
 *
 * Preconditions: the page is on an authenticated session (the caller signs
 * in first — Flow A steps 3/4, or Flow B's sample-resident sign-in). The
 * form's defaults (`Mode="Any"`, `CommunityVisible=true`) are sufficient —
 * this helper does NOT touch the audience fields (pin §2.1 step 5).
 *
 * One write per spec run (the login lockout doctrine, pin §5 #6 — a write
 * is not a login, but the same "one action, no retry loop" spirit applies:
 * a 422/validation re-render is a real failure, not retried).
 */
export async function writePostViaBrowser(
  page: Page,
  input: WritePostInput,
): Promise<string> {
  // Navigate to the compose form (pin §2.1 step 5: `GET /posts/new`).
  await page.goto('/posts/new', { waitUntil: 'domcontentloaded' });

  // The form must render (not a redirect to `/account/login` — the write
  // surface is auth-gated, asserted by the spec's negative leg).
  const form = page.locator('#new-post-form');
  await form.waitFor({ state: 'visible', timeout: 15_000 });

  // Fill the required `Title` (optional in the model but the spec fills it
  // for the rendered-evidence assertion) and the required `Body`.
  await page.fill('input[name="Title"]', input.title);
  await page.fill('textarea[name="Body"]', input.body);

  // The `ComponentId` select is `required` (the view). The seeded
  // neighborhood has components — the first option is a valid choice (the
  // spec does NOT assert a specific community — the audience defaults are
  // sufficient, pin §2.1 step 5).
  await page.selectOption('select[name="ComponentId"]', { index: 0 });

  // Submit and capture the response. `page.click()` resolves to `void`,
  // NOT the navigation Response — so the response is captured with an armed
  // `waitForResponse` (the correct Playwright idiom), not the click's return
  // value (always `undefined`). The success path is a redirect (3xx);
  // a validation re-render is a 200 with the form still present — a real
  // failure (the `Body` was empty, or a community the actor cannot post to),
  // not retried (pin §5 #6).
  const responsePromise = page.waitForResponse(
    (r) => r.request().method() === 'POST' && r.url().includes('/posts/new'),
    { timeout: 15_000 },
  );
  await page.click('#new-post-form button[type="submit"]');
  const response = await responsePromise;
  const status = response.status();
  if (status < 300 || status >= 400) {
    throw new Error(
      `writePostViaBrowser: the write POST returned ${status}, not a ` +
        `redirect. A 200 is a validation re-render (an empty Body, or a ` +
        `community the actor cannot post to) — a real failure, NOT retried ` +
        `(pin §5 invariant #6).`,
    );
  }
  return response.url();
}
