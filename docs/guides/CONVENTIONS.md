# User guides — conventions

> **The lane:** `UG` (ADR 0057). The guides are resident-facing `Page` docs
> nested under the canonical `help` page (ADR 0043 D1, `system/help`), in the
> existing page tree (ADR 0039), edited by the **one** WYSIWYG editor
> (ADR 0031/0033), translated by the **one** `PageTranslation` lane
> (ADR 0022/0047/0049/0051), read by the **one** `IAuthorizationService`
> (ADR 0006), and discovered by the **one** `/pages` tree browse (ADR 0039
> §3.8). This is the platform's own spine applied to documentation — the
> parts (the guides) are cheap; the integration (one engine, one editor, one
> translator, one reader, one tree) is the value.

## What a guide is (and is not)

**A guide is a resident-facing how-to.** It answers "how do I do a thing on
this platform?" in the resident's own words, in plain language, with the
resident's own steps. It is the *resident-facing view* of a feature the
platform has shipped — the ADR (the team's doc) is the *source*, the guide is
the *view*.

**A guide is *not* the team's doc.** SECURITY.md, ARCHITECTURE.md, OPS.md,
the ADRs — those are for the *team* (the operator, the moderator, the
developer). A resident who reads "audience = deny-by-default" in SECURITY.md
does not learn "how do I post to only my group?" The guide answers that
question, in the resident's words, with the resident's own steps.

**A guide is *not* a second doc set.** It is in the *existing* page tree
(ADR 0039), not a separate module, not a separate route, not a separate
editor. A guide that lives *outside* the tree is a bag of parallel lanes —
the ADR 0039 absorb-decision smell, applied to documentation. The tree is
the *closed loop*: the resident reads the guide, follows the link, does the
thing, and the *same* engine that rendered the guide rendered the thing they
did.

## The registry (the single source)

The guide set is a **closed registry**, in the seeder's `GuidePages()` array
(the `en` floor) **plus the matching `DeGuidePages()` / `FrGuidePages()` /
`DaGuidePages()` arrays** (the `de`/`fr`/`da` baselines, ADR 0057 D2 amended
2026-09-21) — the ADR 0042 D1 "the registry is the single source both the
seeder and these tests read" shape, applied to the guides. A new guide is a
new row in **all four** arrays (one per language), not a silent addition —
the ADR 0040 `PageKind` closed-set rule applied to the guide set. The twelve
seeded guides (the current shipped surface, M0–M4 + the `ML`/`LS`/`SP`/`RC`/
`RE`/`TG`/`PG`/`GU`/`GA`/`TR`/`TZ`/`DF` lanes):

| Slug (under `help/`) | Covers | The ADR it documents |
|---|---|---|
| `getting-started` | the first ten minutes: sign in, the feed, the directory, the `/pages` tree | ADR 0039 / 0043 / 0001 |
| `posts` | how a post works: the audience picker, replies, edit + soft-delete, drafts, file attachments | ADR 0014 / 0024 / 0037 / 0034 |
| `groups` | public groups (the audience reuse unit), private groups (the membership unit), group posts | ADR 0010 / 0013 / 0012 |
| `drafts` | save-as-draft, the author-only visibility, publishing, `/my/drafts` | ADR 0037 |
| `audience` | choosing who sees a post (the audience picker, in plain language; what "community-visible" / "individual" / "group" each mean) | ADR 0001-B / 0036 / 0041 |
| `language` | the language picker, the `Accept-Language` fallback, the per-resident override | ADR 0005 / 0015 / 0046 / 0019 / 0020 |
| `events` | how an event works: the feed, the detail view, RSVP, the day-before reminder | ADR 0054 |
| `translator` | what the `Translator` role may and may not do, in plain language | ADR 0021 |
| `child-accounts` | the guardian's child accounts: what a guardian may and may not do (suspend, memberships, invitation approval, assign a second guardian, hand-over), and the privacy boundary (no reading the child's content) | ADR 0028 / 0038 |
| `being-a-child` | the child's own view of a child account: what stays theirs (posts/replies/profile the guardian can't read), what the guardian handles (memberships, suspend), group invitations (decline always open, accept needs guardian approval), and hand-over restoring the child's own controls | ADR 0028 / 0038 |
| `admins` | what a global admin does (accounts, communities, the platform pages incl. reset-to-seeded, the sign-up gate, the platform defaults) and what keeps the role in check (audit trail; no reading of residents' content) | ADR 0062 / 0050 / 0019 / 0020 / 0040 / 0058 |
| `moderators` | what a standing moderator may do (see their scoped report queue) and may not (act on a report — assign/unlock/resolve stay with an admin; see outside their part; read anyone's content) | ADR 0003 / 0030 |
| `notifications` | where the bell and inbox live, the mark-all-read action, what an email is (inbox always records), the per-kind email on/off dial (preferences), and the per-place opt-in (subscriptions) | ADR 0076 / 0077 / 0083 / 0084 |

A guide that documents a feature **not yet shipped** (M5 Projects, M6
Portability / iCal / search, the deferred items in SECURITY.md
§6) is **not** seeded — it is a row in the registry in its own lane (the
ADR 0039 "no roadmap letter moves" discipline applied to the guide set).

## The writing rules (the `en` floor)

The `en` body of a guide is **code-owned** (the ADR 0042 D1 shape — the
seeder's `GuidePages()` array is the single source). On **first boot** the
seeder writes it; on an **upgrade (warm) boot** it is written
**create-if-missing only** — a guide already present (a community edit, or a
resident page using the slug) is never clobbered (ADR 0057 D2.1, the ADR
0047 D2 / 0052 backfill shape). The writing rules, at the ADR 0042 D2 bar
(idiomatic, register held, sentence case, no word-for-word calques):

1. **Plain language, the resident's words.** No code jargon (no "audience",
   no "PostStatus", no "AccessVia", no "component" — the resident's words
   are "who can see this", "draft", "my groups", "the neighborhood").
2. **The resident's steps, in order.** A guide is a *how-to*, not a
   *description*: "click **New post** at the top of the feed, write your
   post, pick **Who can see it** from the dropdown, and click **Post**" —
   not "the platform provides a post-composer with an audience picker".
3. **One guide, one thing.** A guide answers *one* question ("how do I
   post?"), not three ("how do I post, and also reply, and also delete?").
   The `getting-started` index is the *only* guide that links to the others;
   the rest are self-contained.
4. **The `en` floor is always present; the non-`en` body is community-owned.**
   A guide ships a curated `de`/`fr`/`da` **baseline** on a pristine DB
   (ADR 0057 D2, amended 2026-09-21 — the seeder's `DeGuidePages()` /
   `FrGuidePages()` / `DaGuidePages()` arrays are the single source, the
   same shape as the four-surface set's baselines), seeded
   **create-if-missing only**. A **human Translator** (the ADR 0021 lane) or
   a GlobalAdmin may then refine a `de`/`fr`/`da` body in the in-app editor,
   and that edit is never clobbered by a later deploy (the ADR 0042 D1
   invariant). A machine translation is **never** the writer of a non-`en`
   guide body (ADR 0005 C — the "never machine-translated" clause, applied to
   guides as to UGC); the baselines are hand-curated, not machine output.
   **Cross-links inside the `de`/`fr`/`da` baselines use the absolute form**
   (`[posts](/pages/system/help/posts)`) because a guide's canonical URL
   carries the `system` root and the relative links the `en` floor uses
   (e.g. `[posts](posts)`) do not resolve on the tree-browse page; the
   `en` floor is untouched.

## The consistency loop (the "keep it honest" lane)

The consistency loop is **three layers**, each answering a *different*
question (the `in-code.md` "each layer must answer a different question, or
it's ceremony" rule):

### 1. *Where* — the guides are in the tree

The single source of truth for "what the platform does, in the resident's
language" is the `Page` tree, not a second doc set. The guides are `Page`
docs (the existing doc), edited by the **one** `bindRichEditor`, translated
by the **one** `PageTranslation` lane, read by the **one**
`IAuthorizationService`, and discovered by the **one** `/pages` tree browse.
A new guide is a new row in the registry, not a new module.

### 2. *When* — the event-based check (the lane's Definition of Done)

A resident-facing change ships **with** its guide update, in the **same
lane**. A lane that ships a feature that changes how a resident does a thing
(a new audience option, a new RSVP flow, a new attachment type) **must**:

- **Update** the affected guide's `en` body (the code-owned floor) in the
  same commit — the ADR 0042 D1 "code wins for `en`" shape applied to the
  guide's `en` body.
- **Add a row** to the guide registry (the seeder's `GuidePages()` array,
  **plus the matching `DeGuidePages()` / `FrGuidePages()` / `DaGuidePages()`
  baselines**) if it is a *new* feature — the ADR 0040 `PageKind` closed-set
  rule applied to the guide set.
- **Update** the conventions doc (this file) in the same commit — the
  registry table gains the new row, the ADR it documents is named, and the
  drift pin (below) picks up the new slug.

A lane that ships a feature without its guide update is **not done** (the
ADR 0039 §3.7 C3 invariant shape, applied to docs). The *response* is the
test suite: the drift pin (below) is the single source the test reads, and a
feature-without-guide or a guide-without-feature is a red test.

**And across upgrades:** a deployment whose first boot predates the guide is
covered by the **warm-boot backfill** (`BackfillUserGuidesAsync`, ADR 0057
D2.1) — the absent guides are created on the upgrade boot, create-if-missing,
so existing instances get their guides without a reseed; a community-edited
or retired guide is never clobbered or resurrected.

### 3. *How often* — the periodic check (the OPS.md §13 procedure)

A **GlobalAdmin** (the owner) runs a **quarterly** (or per-release, whichever
is sooner) review procedure in **OPS.md §13**:

- **For each row in the registry**, open the guide's *current* `en` body in
  the in-app editor, follow it *as a resident would* (sign in as a demo
  resident, do the steps), and mark the row **current** / **needs update** /
  **feature retired**.
- **The *response* to a "needs update"** is a lane (the ADR 0057 §D3.2 shape
  — a lane ships the guide update, the ADR 0042 D1 "code wins for `en`"
  shape applied to the guide's `en` body).
- **The *response* to a "feature retired"** is a soft-delete of the guide
  (the ADR 0024 author-lane shape, applied to a guide by a GlobalAdmin) and a
  removal of the row from the registry (the ADR 0044 D7 / 0052 shape — the
  warm-boot backfill's "reset" clause, applied to the guide registry).
- **The *cadence*** is recorded in OPS.md §13 with a **Last tested: <date>**
  stamp (the OPS.md §12 convention), and the *owner* is the GlobalAdmin (the
  same owner as the other OPS procedures).

The periodic check is the *safety net* for the event-based check: a lane that
*should* have updated a guide but didn't (a missed Definition-of-Done) is
caught by the next quarterly review, not by a resident hitting a wall.

## The drift pin (the test that keeps the registry honest)

The **test** (`UG_U05_GuideRegistryTests`, in `Kumunita.Core.Tests`) pins:

- **The exact seeded set** — the guides are seeded as direct children of the
  canonical `help` page, **exactly one per slug** in `GuidePages()` (no
  duplicates, no missing), **`Kind = System`**, **`Audience = null`**,
  **`LanguageCode = "en"`**, **`AuthorId` empty**, **not a draft**, **not
  deleted**.
- **The `help` page is the only root under `system`** — the ADR 0043 D1
  exact-set pin (the four-surface set) is **unchanged**: `system`'s direct
  children are still `{terms, help, privacy, conduct}`, and `system` is still
  the **only** root (the guides are *grandchildren* of `system`, not new
  roots — the ADR 0039 §3.3 forest shape, unchanged).
- **No `PageTranslation` row is attached to a guide** — the guides ship
  `en`-only (ADR 0057 D2); a non-`en` guide body, once added, is a
  `PageTranslation` row on the guide's *own* `Id` (the ADR 0042 D6 parentage
  distinction, unchanged), and the *first-boot* state has none.
- **The `en` body parity** — the guide's seeded `Page.Body` equals
  `GuidePages()[slug].Body` exactly (the ADR 0042 D1 "code wins for `en`"
  shape, pinned by the test).
- **The feature↔guide drift pin** — the guide registry's slugs are the
  **single source** the test reads (the ADR 0042 D1 "the registry is the
  single source both the seeder and these tests read, so mirroring is exact"
  shape, applied to the guides). A lane that adds a feature without a guide
  row, or a guide row without a feature, is a red test.

## The standing (the ADR 0040 system-page matrix, unchanged)

A guide is a `PageKind.System` page (the ADR 0040 model, unchanged):

- **Edit / move / delete** — GlobalAdmin only.
- **Add a translation** — GlobalAdmin ∪ Translator (the ADR 0021 / 0022 /
  0047 / 0048 lanes, unchanged).
- A community Moderator has **no** standing on a guide (the ADR 0040
  amendment, unchanged). A guide is *not* a `PageKind.User` page (the
  resident's blog lane) — the ADR 0040 namespace guard (a `User` page is
  never nested under a `System` page, and vice versa) applies: a guide is a
  `System` page under the `System` `help` page, and a resident's blog page
  is never nested under `help`.

## Adding a guide (the lane's Definition of Done)

A new guide ships in **one lane**, in **one commit**, with **all** of:

1. **A row in the registry** — the seeder's `GuidePages()` array gains the
   new `(Slug, Title, Body)` tuple (the `en` body, the code-owned floor),
   **and the matching `DeGuidePages()` / `FrGuidePages()` / `DaGuidePages()`
   arrays gain the translated baselines** (the `de`/`fr`/`da` floor, the
   ADR 0057 D2 amended 2026-09-21 shape).
2. **A row in the conventions doc** — the registry table (above) gains the
   new row, the ADR it documents is named, and the drift pin picks up the
   new slug.
3. **The guide's `en` body** — in the seeder's array (the code-owned floor),
   at the ADR 0042 D2 bar (plain language, the resident's steps, no code
   jargon, sentence case).
4. **The drift pin** — the test (`UG_U05_GuideRegistryTests`) picks up the
   new slug automatically (the registry is the single source the test reads);
   a new slug in the registry that is not in the conventions doc, or a new
   row in the conventions doc that is not in the registry, is a red test.
5. **The ADR** — if the guide documents a *new* feature, the feature's lane
   ships the ADR (the team's doc) **and** the guide row (the resident's
   view) in the same commit; the ADR 0042 D1 "code wins for `en`" shape
   applied to the guide's `en` body.

A lane that ships a guide row without the ADR (or the ADR without the guide
row) is **not done** — the two are the same set, not two separate sources
(the ADR 0042 D1 "the registry is the single source both the seeder and
these tests read" shape, applied to the team's docs and the resident's
guides).

## The lane's Definition of Done (this commit)

- [x] **The ADR** (ADR 0057) — the decision record, the D1–D5 shape, the
  three-layer consistency loop, the drift pin, the standing, the follow-on
  lanes.
- [x] **The conventions doc** (this file) — the taxonomy, the writing rules,
  the consistency loop, the drift pin, the standing, the adding-a-guide
  procedure.
- [x] **The seeder** — `SeedUserGuidesAsync` (first-boot) +
  `BackfillUserGuidesAsync` (warm-boot, create-if-missing, wired in the
  `SchemaBootstrap` warm branch) + `GuidePages()` in `FirstBootSeeder.cs`
  (the guides under `help/`, `en`-only, idempotent, the ADR 0042 D1 shape).
- [x] **The drift pin** — `UG_GuideRegistryTests` in
  `Kumunita.Core.Tests` (the exact seeded set, the only-root pin, the
  en-only pin, the body parity, the feature↔guide drift, and the backfill:
  creates the absent guides, idempotent, never clobbers an existing one).
- [x] **The OPS.md §13** — the periodic review procedure (the owner, the
  cadence, the checklist, the response).
- [x] **The `Milestones.cs` + the README Roadmap** — the `UG` row (a
  `StatusDone` entry), the `MilestonesTests` pin updated in the same commit.
- [x] **The `en` body of the `help` page** — the single new bullet (a
  pointer to the guides), the `de`/`fr`/`da` baseline arrays gain the
  matching new bullet (the ADR 0042 D2 "structure preserved" parity).
