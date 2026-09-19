// TG, plan U9 — e2e Playwright specs (browser-level, three pin groups).
//
// STATUS (honest — per U9's own entry read, mirroring M3b U10 + M2 U13):
//   Authored against the *shipped* U1–U8 TG UI (selectors pinned from the
//   actual .cshtml + client/lib/tag-suggest.ts — see the per-test comments).
//   NOT yet runnable: the same *M2 D2* documented-throw is re-confirmed here
//   (the `kumunita` fixture is a documented throw in BOTH `e2e-m2.spec.ts`
//   and `e2e-m3.spec.ts`, and no TG unit — U1 through U8 — implements the
//   Playwright runtime). The U9 register § U9 Exit permits this:
//   "a cookie-less `GET /tags` + `GET /tags/sanitation` render the scoped
//   sets (or are pinned by the e2e spec)" — the spec IS the pin. The
//   Playwright runtime unit (a future lane) lands the fixture
//   implementation and records the pass count in a later
//   `### Run result (TG e2e — <date>)` section.
//
// Invariant anchors frozen by this file (see `docs/design/tags-design.md`
// §2.4 / §2.5, FACES F1–F12):
//   (a) F9 — the display name is resolved in the viewer's language (C-TG·4,
//       D3/D5): a `de`-preferring actor typing `hy` gets the `de` name
//       `hygiène` in the autocomplete even if the `Slug` is `sanitation`.
//       The `starts_with(displayName, prefix) OR starts_with(slug, prefix)`
//       filter is server-side (C-TG·2 / C-TG·4); the client only renders
//       the dropdown + commits.
//   (b) F10 — autocomplete is **capped** at ≤ 10 (C-TG·2): the server
//       applies `.Take(10)`; the client renders whatever the server returns.
//   (c) Composer-input (C-TG·6, D6): the tag field renders on the post
//       composer (`/posts/new` + `/posts/{id}/edit`) and the blog-page
//       composer (`/pages/new`, `PageKind.User`), but **not** on the
//       System-page composer (`/pages/new`, `PageKind.System` — the
//       `@if (Model.Kind == "User")` gate in `_PageForm.cshtml`).
//   (d) Browse-page (C-TG·1 / C-TG·2 / C-TG·3, the privacy-pin): the tag
//       list (`GET /tags`) and by-tag view (`GET /tags/{slug}`) render
//       **only** what the access-scoped read seam returns. A tag used only
//       on content the viewer cannot read is **absent** from both — no
//       name, no "hidden" placeholder, no 403 (a 404, per the U7 404-floor
//       pin). The mixed-audience fixture makes the privacy-pin visible in
//       the HTML.
//
// Route / selector pins (from the shipped views + client/lib/tag-suggest.ts —
// read the .cshtml / .ts first before "improving" them):
//   GET    /tags                        (U7; the tag list — `.list-group-item
//                                       a` rows, one per readable tag; the
//                                       empty state is `div.alert.alert-info
//                                       [kw-l key="tags.list.empty"]`)
//   GET    /tags/{slug}                 (U7; the by-tag view — posts section
//                                       `.list-group` + pages section
//                                       `.list-group`; 404 when both lists
//                                       are empty, the U7 404-floor pin)
//   GET    /posts/new                   (M3 U7; the post composer — the Tags
//                                       card is `div.card:has(input#tags)`
//                                       with `input[data-tag-suggest]`)
//   GET    /posts/{id}/edit             (M3 U7; the post edit — same Tags
//                                       card shape as `/posts/new`)
//   GET    /pages/new                   (ADR 0040; the page composer —
//                                       `PageKind.User` (blog) renders the
//                                       Tags card; `PageKind.System` does
//                                       **not** (the `@if (Model.Kind ==
//                                       "User")` gate, C-TG·6))
//   POST   /api/tags/suggest            (U7; the autocomplete seam — the
//                                       `tag-suggest.ts` module POSTs via
//                                       `apiFetch`; the response is
//                                       `{ suggestions: [{ tag: { slug },
//                                       useCount, displayedName }] }`)
//
//   ── tag-suggest.ts selectors (U8; the client/lib module) ──────────
//   input[data-tag-suggest]            (the free-text tag input; the
//                                       self-wire selector)
//   .kw-tag-suggest                    (the dropdown — `role="listbox"`,
//                                       `display:none` until suggestions
//                                       arrive)
//   .kw-tag-suggest-item               (a dropdown row — `role="option"`,
//                                       `textContent` = the display name)
//   .kw-tag-chips                      (the committed-tag chip list)
//   .kw-tag-chip                       (a committed tag chip —
//                                       `textContent` = the label)
//   input[name="TagIds"]               (the hidden submission field —
//                                       `value` = JSON array of labels)
//
// (The `data-tag-suggest` attribute + the `.kw-tag-*` class names are
//  frozen once written — no new class or attribute may be rendered by
//  this spec or by any view it asserts against.)

import { test as baseTest, expect, type Page } from '@playwright/test';

// ── Fixture shapes (documented; the implementation is the Playwright
//    runtime unit's work — U9 follows the M2 U13 / M3b U10 precedent
//    and records the gap rather than landing it mid-lane) ──
//
// A single `kumunita` fixture the tests below consume, with the
// methods/fields the spec needs. This is the *entire* contract; it
// is deliberately small and bounded (same discipline as M2 U13).
//
//   signup(displayName, email, password) ⇒ Promise<SignupHandle>
//     · POST /account/signup (the four form fields)
//     · flip the account to verified via the M1 token channel
//     · returns an opaque subjectId — a string, NOT a Guid (the M1
//       seam freeze)
//
//   signupGlobalAdmin(displayName, email, password)
//   ⇒ Promise<SignupHandle>
//     · a GlobalAdmin-flipped account (per ADR 0003 §SoD pin)
//
//   login(page, email, password) ⇒ Promise<void>
//     · clears the current context's cookies → fresh user in the
//       same page
//     · POST /account/login; lands signed in
//
//   plantTag(slug, name, langCode, createdBy, translations?)
//   ⇒ Promise<string>  (returns the tag id)
//     · Core-level setup: creates a `Tag` doc (the `TagService`
//       write-lane idiom — `CreatedBy` = the actor, the typed string
//       the base `Name`, C-TG·4)
//     · `translations` is an optional array of `{ langCode, name }`
//       rows (the `TagTranslation` shape — the F9 viewer-language
//       display name is one of these rows)
//
//   attachTagToPost(postId, tagIds, authorId, groupId?)
//   ⇒ Promise<void>
//     · Core-level setup: sets `Post.TagIds` on the post (the
//       `TagService.AttachToPostAsync` seam). `groupId` is the
//       optional ADR 0013 group-lane marker (a group post's
//       `TagIds` never surface in a non-member's by-tag results —
//       the C-TG·3 pin).
//
//   createBlogPage(userId, title, tagIds, isSystem?)
//   ⇒ Promise<string>  (returns the page id)
//     · Core-level setup: creates a `Page` doc (the
//       `PageService` create seam). `isSystem` = `true` for a
//       `PageKind.System` page (C-TG·6 — the tag field is **not**
//       rendered on the System-page composer).
//
//   getSuggestResults(prefix, actorId)
//   ⇒ Promise<Array<{ slug: string; displayName: string }>>
//     · Core-level read: the `SuggestAsync` seam's result (the
//       `starts_with(displayName, prefix) OR starts_with(slug, prefix)`
//       filter + the ≤ 10 cap, C-TG·2 / C-TG·4)
//
//   getTagListForActor(actorId)
//   ⇒ Promise<Array<{ slug: string; displayName: string; useCount: number }>>
//     · Core-level read: the `ListForActorAsync` seam's result
//       (the C-TG·2 base query)
//
//   getPostsByTag(slug, actorId)
//   ⇒ Promise<Array<{ id: string; title: string; groupId: string }>>
//     · Core-level read: the `ListPostsByTagAsync` seam's result
//       (C-TG·3: the post's own `Read` decision applied before return)
//
//   getPagesByTag(slug, actorId)
//   ⇒ Promise<Array<{ id: string; title: string }>>
//     · Core-level read: the `ListPagesByTagAsync` seam's result
//
// The e2e's *browser* steps assert the *observable* surface: the
// dropdown items (F9 / F10), the composer tag field's presence /
// absence (C-TG·6), the tag-list + by-tag rows (the privacy-pin,
// C-TG·1 / C-TG·2 / C-TG·3) — **never** the Core internals.

interface SignupHandle {
  subjectId: string; // opaque string, NOT a Guid (M1 seam freeze)
}

interface TagTranslationSeed {
  langCode: string;
  name: string;
}

interface SuggestionRow {
  slug: string;
  displayName: string;
}

interface TagListRow {
  slug: string;
  displayName: string;
  useCount: number;
}

interface PostRow {
  id: string;
  title: string;
  groupId: string;
}

interface PageRow {
  id: string;
  title: string;
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
  plantTag(
    slug: string,
    name: string,
    langCode: string,
    createdBy: string,
    translations?: TagTranslationSeed[],
  ): Promise<string>;
  attachTagToPost(
    postId: string,
    tagIds: string[],
    authorId: string,
    groupId?: string,
  ): Promise<void>;
  createBlogPage(
    userId: string,
    title: string,
    tagIds: string[],
    isSystem?: boolean,
  ): Promise<string>;
  getSuggestResults(prefix: string, actorId: string):
    Promise<SuggestionRow[]>;
  getTagListForActor(actorId: string): Promise<TagListRow[]>;
  getPostsByTag(slug: string, actorId: string): Promise<PostRow[]>;
  getPagesByTag(slug: string, actorId: string): Promise<PageRow[]>;
}

// The `baseTest.extend<Kumunita>` call below returns a NEW typed
// `test` that knows about `kumunita`. Re-binding that back into the
// name `test` — so every `test('...')` call in this file sees the
// extended fixture type — is Playwright's canonical pattern for a
// custom fixture. Same precedent as `e2e-m2.spec.ts` /
// `e2e-m3.spec.ts`.
const extended = baseTest.extend<{ kumunita: Kumunita }>({
  kumunita: async ({}, use) => {
    throw new Error(
      'kumunita fixture not implemented (TG U9 re-confirmed). ' +
      'U9 authored the spec (the selector + route pins are frozen ' +
      'in the header of e2e-tags.spec.ts); the Playwright runtime ' +
      'unit must land the implementation of `signup / ' +
      'signupGlobalAdmin / login / plantTag / attachTagToPost / ' +
      'createBlogPage / getSuggestResults / getTagListForActor / ' +
      'getPostsByTag / getPagesByTag` per the fixture contract above. ' +
      'Reuse the M2 U13 fixture contract (e2e-m2.spec.ts) for the ' +
      'signup/login shape; the six new helpers (plantTag, ' +
      'attachTagToPost, createBlogPage, getSuggestResults, ' +
      'getTagListForActor, getPostsByTag, getPagesByTag) are TG ' +
      'ADDs. See docs/plans-milestones/in-progress/tags/' +
      'tags-handoff-notes.md § U9.',
    );
    // `use` is required by the Playwright fixture API. The throw
    // above fires first (before `use` is ever called), so there is
    // nothing left to return. No explicit `return;` here — that would
    // be unreachable (TS7027) and adds no semantics. Same precedent
    // as e2e-m2.spec.ts / e2e-m3.spec.ts.
  },
});
const test = extended;

// ── Helper: wait for the tag-suggest dropdown to appear ──────────
// The `tag-suggest.ts` module renders the dropdown (`.kw-tag-suggest`)
// with `display:none` until the `apiFetch` POST resolves and
// `render(suggestions)` sets `display:block`. The debounce is 120 ms
// (the `DEBOUNCE_MS` constant in `tag-suggest.ts`); the `page.goto`
// + `page.locator.fill` sequence gives the debounce enough time, but
// an explicit wait on the dropdown's visibility is the robust pin.
async function waitForDropdown(page: Page): Promise<void> {
  await expect(page.locator('.kw-tag-suggest')).toBeVisible();
}

// ── Helper: count the dropdown items ─────────────────────────────
// Each `.kw-tag-suggest-item` is a `role="option"` row; the count is
// the number of suggestions the server returned (capped at ≤ 10 by
// the `SuggestAsync` seam, C-TG·2 / F10).
async function countDropdownItems(page: Page): Promise<number> {
  return page.locator('.kw-tag-suggest-item').count();
}

test.describe('TG e2e — autocomplete, composer-input, browse-page', () => {
  // ── (a) F9 / F10 — the autocomplete pin ──────────────────────────
  // F9: a `de`-preferring actor typing `hy` gets the `de` name
  // `hygiène` in the autocomplete even if the `Slug` is `sanitation`
  // (the display-name branch of the `starts_with(displayName) OR
  // starts_with(slug)` filter, C-TG·2 / C-TG·4, D3/D5).
  //
  // F10: autocomplete is **capped** at ≤ 10 (C-TG·2): the server
  // applies `.Take(10)`; the client renders whatever the server
  // returns.
  //
  // The mixed-audience fixture: one tag (`sanitation` / `hygiène`)
  // used on a community post the actor can read + a `de`
  // `TagTranslation` row (the F9 viewer-language display name).
  // Thirteen additional tags on the same post (the F10 cap — the
  // server returns 10, the list shows 14).
  test('a. autocomplete — F9 viewer-language + F10 cap ≤ 10', async ({ page, kumunita }) => {
    const resident = await kumunita.signup(
      'Autocomplete R.', 'ac-r@example.com', 'Passw0rd!',
    );

    // 1) Plant the F9 tag: Slug `sanitation`, base name `Sanitation`
    //    (en), `de` translation `hygiène` (the F9 viewer-language
    //    display name — the `starts_with("hygiène", "hy")` branch).
    const sanitationTagId = await kumunita.plantTag(
      'sanitation', 'Sanitation', 'en', resident.subjectId,
      [{ langCode: 'de', name: 'hygiène' }],
    );

    // 2) Plant 13 more tags (the F10 cap fixture — 14 total).
    const extraTagIds: string[] = [];
    for (let i = 1; i <= 13; i++) {
      const id = await kumunita.plantTag(
        'tag' + i, 'Tag ' + i, 'en', resident.subjectId,
      );
      extraTagIds.push(id);
    }

    // 3) Attach all 14 tags to a community post the actor can read.
    //    (The Core-level setup — the `attachTagToPost` fixture
    //    method plants the `Post.TagIds` + the `Tag` docs.)
    //    For the e2e, the post is created via the browser composer
    //    (the M3 U7 composer — the `input[name="Title"]` +
    //    `textarea[name="Body"]` shape). The `TagIds` are attached
    //    server-side (the `attachTagToPost` fixture method).
    await kumunita.login(page, 'ac-r@example.com', 'Passw0rd!');
    await page.goto('/posts/new');
    await page.locator('input[name="Title"]').fill('TG e2e autocomplete pin');
    await page.locator('textarea[name="Body"]').fill('The F9/F10 body.');
    // Submit the post (the M3 U7 composer — the `form` with
    // `input[name="Title"]` + `textarea[name="Body"]`).
    await page.locator('form button[type="submit"]').first().click();
    // Wait for the post to land (the M3 U7 composer redirects to
    // the post detail on success).
    await page.waitForURL(/\/posts\/.+/);

    // The `lastCreatedPostId()` fixture method returns the id
    // (same as M3b U10 — the fixture is the test-side shortcut
    // for the Core-level read).
    // (In the fixture implementation, this is the most-recently
    //  created post's id.)
    // For the spec, we assert the observable surface:
    //  (i) the tag field is present (the composer-input pin — see
    //      test (b) below for the full pin);
    //  (ii) the tag-suggest dropdown renders the F9 / F10 pins.

    // 4) F9 — type `hy` in the tag input; the dropdown should show
    //    `hygiène` (the `de` display name), **not** `sanitation`
    //    (the slug — the display-name branch of the filter).
    //    The tag-suggest self-wire (the `tag-suggest.ts` module)
    //    creates the chips / dropdown / hidden-field around the
    //    `input[data-tag-suggest]`. The `apiFetch` POST to
    //    `/api/tags/suggest` returns the `SuggestAsync` result.
    await page.locator('input[data-tag-suggest]').fill('hy');
    await waitForDropdown(page);
    const f9Items = page.locator('.kw-tag-suggest-item');
    const f9Count = await f9Items.count();
    expect(f9Count).toBeGreaterThan(0);
    // The F9 pin: at least one item shows `hygiène` (the `de`
    // display name — the viewer-language resolution, C-TG·4, D5).
    const f9Texts = await f9Items.allTextContents();
    expect(f9Texts.some((t) => t.includes('hygiène'))).toBe(true);

    // 5) F10 — the dropdown is capped at ≤ 10 (C-TG·2). With 14
    //    readable tags, the server returns 10.
    await page.locator('input[data-tag-suggest]').fill('');
    await page.waitForTimeout(300); // let the debounce fire + the dropdown close
    await page.locator('input[data-tag-suggest]').fill('tag');
    await waitForDropdown(page);
    const f10Count = await countDropdownItems(page);
    expect(f10Count).toBeLessThanOrEqual(10);
  });

  // ── (b) Composer-input — the tag field renders on post + blog,
  //     NOT on the System-page composer (C-TG·6, D6) ──────────────
  // The `_PageForm.cshtml` gate: `@if (Model.Kind == "User")` wraps
  // the Tags card. A `PageKind.System` page (the GlobalAdmin's
  // composer) renders **no** tag input — the `tag-suggest.js`
  // self-wire finds nothing (a no-op, never a crash).
  test('b. composer-input — post + blog render the tag field, System does not', async ({ page, kumunita }) => {
    const resident = await kumunita.signup(
      'Composer R.', 'composer-r@example.com', 'Passw0rd!',
    );
    const globalAdm = await kumunita.signupGlobalAdmin(
      'Composer G.', 'composer-g@example.com', 'Passw0rd!',
    );

    // 1) Post composer — the tag field is present (U8's
    //    `Views/Posts/New.cshtml` Tags card, L133–150).
    await kumunita.login(page, 'composer-r@example.com', 'Passw0rd!');
    await page.goto('/posts/new');
    await expect(
      page.locator('input[data-tag-suggest]'),
    ).toBeVisible();
    // The `tag.input.hint` kw-l is rendered (U7's registry key).
    await expect(
      page.locator('div.text-muted.small.mt-1', {
        hasText: /Type to search existing tags/i,
      }),
    ).toBeVisible();

    // 2) Blog-page composer (the `PageKind.User` branch — the
    //    resident's own namespace, ADR 0040). The tag field is
    //    present (U8's `Views/Page/_PageForm.cshtml` Tags card,
    //    gated by `@if (Model.Kind == "User")`).
    //    (The `GET /pages/new` route — the `PageController.New`
    //    action sets `Kind = "User"` for a non-admin.)
    await page.goto('/pages/new');
    await expect(
      page.locator('input[data-tag-suggest]'),
    ).toBeVisible();

    // 3) System-page composer (the `PageKind.System` branch — the
    //    GlobalAdmin's composer, ADR 0040). The tag field is
    //    **not** present (the `@if (Model.Kind == "User")` gate —
    //    a `PageKind.System` page renders **no** tag input,
    //    C-TG·6). The `tag-suggest.js` self-wire finds nothing
    //    (a no-op, never a crash).
    await kumunita.login(page, 'composer-g@example.com', 'Passw0rd!');
    await page.goto('/pages/new');
    // The System-page composer — the `Model.Kind` is `"System"`
    // (the `PageController.New` action sets `Kind = "System"` for
    // a GlobalAdmin). The Tags card is **not** rendered.
    const tagInputCount = await page.locator('input[data-tag-suggest]').count();
    expect(tagInputCount).toBe(0);
    // The `kw-tag-field` wrapper (the `tag-suggest.ts` module's
    // `.kw-tag-field` div) is also absent (the self-wire never ran).
    const kwTagFieldCount = await page.locator('.kw-tag-field').count();
    expect(kwTagFieldCount).toBe(0);
  });

  // ── (c) Browse-page — the privacy-pin (C-TG·1 / C-TG·2 / C-TG·3) ──
  // The tag list (`GET /tags`) and by-tag view (`GET /tags/{slug}`)
  // render **only** what the access-scoped read seam returns. A tag
  // used only on content the viewer cannot read is **absent** from
  // both — no name, no "hidden" placeholder, no 403 (a 404, per the
  // U7 404-floor pin).
  //
  // The mixed-audience fixture:
  //   · Tag `shared` — used on a community post BOTH actors can read
  //     (the author's audience includes both). Visible to both.
  //   · Tag `secret` — used on a community post ONLY `other` can read
  //     (the author = `other`, audience = `other` only). Invisible
  //     to `viewer` (the C-TG·1 / C-TG·2 privacy-pin).
  //   · Tag `grouppost` — used on a group post (ADR 0013) that
  //     `viewer` is **not** a member of. Invisible to `viewer`
  //     (the C-TG·3 group-lane exclusion).
  test('c. browse-page — privacy-pin: visible to the reader, absent to the non-reader', async ({ page, kumunita }) => {
    const author = await kumunita.signup(
      'Browse Author', 'browse-author@example.com', 'Passw0rd!',
    );
    const viewer = await kumunita.signup(
      'Browse Viewer', 'browse-viewer@example.com', 'Passw0rd!',
    );
    const other = await kumunita.signup(
      'Browse Other', 'browse-other@example.com', 'Passw0rd!',
    );

    // 1) Plant `shared` — a tag both actors can see (used on a post
    //    the author can read; the audience includes `viewer`).
    const sharedId = await kumunita.plantTag(
      'shared', 'Shared', 'en', author.subjectId,
    );
    // Attach to a post `author` created (the audience includes
    // `viewer` — the M3 U7 composer's default audience is
    // `CommunityVisible`, ADR 0036).
    await kumunita.login(page, 'browse-author@example.com', 'Passw0rd!');
    await page.goto('/posts/new');
    await page.locator('input[name="Title"]').fill('Shared post');
    await page.locator('textarea[name="Body"]').fill('Both can read this.');
    await page.locator('form button[type="submit"]').first().click();
    await page.waitForURL(/\/posts\/.+/);
    // The `attachTagToPost` fixture method plants the `Post.TagIds`
    // (the Core-level setup — the `TagService.AttachToPostAsync` seam).
    // (In the fixture implementation, this is the `attachTagToPost`
    //  call with the post id + the `shared` tag id.)
    // For the spec, we assert the observable surface:
    //  (i) `shared` appears in `viewer`'s tag list;
    //  (ii) `shared` appears in `viewer`'s by-tag results.

    // 2) Plant `secret` — a tag ONLY `other` can see (used on a post
    //    `other` created; the audience = `other` only).
    const secretId = await kumunita.plantTag(
      'secret', 'Secret', 'en', other.subjectId,
    );
    // Attach to a post `other` created (the audience = `other` only
    // — `viewer` is **not** in the audience).
    await kumunita.login(page, 'browse-other@example.com', 'Passw0rd!');
    await page.goto('/posts/new');
    await page.locator('input[name="Title"]').fill('Secret post');
    await page.locator('textarea[name="Body"]').fill('Only other can read this.');
    await page.locator('form button[type="submit"]').first().click();
    await page.waitForURL(/\/posts\/.+/);
    // (The `attachTagToPost` fixture method — same as above.)

    // 3) `viewer` loads the tag list (`GET /tags`). The `shared` tag
    //    is **visible**; the `secret` tag is **absent** (the
    //    C-TG·1 / C-TG·2 privacy-pin — a tag behind unread content
    //    is as good as absent to that viewer).
    await kumunita.login(page, 'browse-viewer@example.com', 'Passw0rd!');
    await page.goto('/tags');
    const tagRows = page.locator('.list-group-item a');
    const tagTexts = await tagRows.allTextContents();
    // `shared` is visible (the positive control).
    expect(tagTexts.some((t) => t.includes('Shared'))).toBe(true);
    // `secret` is **absent** (the privacy-pin — C-TG·1 / C-TG·2).
    expect(tagTexts.some((t) => t.includes('Secret'))).toBe(false);

    // 4) `viewer` loads the by-tag view (`GET /tags/shared`). The
    //    post is **visible** (the C-TG·3 pin — the post's own `Read`
    //    decision is applied before the post is returned).
    await page.goto('/tags/shared');
    const byTagPosts = page.locator('h2:has-text("Posts") + .list-group .list-group-item');
    await expect(byTagPosts.first()).toBeVisible();
    // The post title `Shared post` is in the by-tag results.
    await expect(
      page.locator('.list-group-item a', { hasText: /Shared post/ }),
    ).toBeVisible();

    // 5) `viewer` loads the by-tag view (`GET /tags/secret`). The
    //    404-floor pin (U7): both `Posts` and `Pages` are empty
    //    for this viewer → **404** (not a blank page, not a 403 —
    //    a 403 would confirm the tag exists, C-TG·1).
    const secretResponse = await page.request.get('/tags/secret');
    expect(secretResponse.status()).toBe(404);
  });
});
