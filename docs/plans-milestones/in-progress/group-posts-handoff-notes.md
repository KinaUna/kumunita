# Group posts — rolling handoff notes (appended, never rewritten)

> One section per unit, **appended** (never rewritten). Each unit writes exactly
> one short section before it exits; the next unit reads only *that* section +
> its own entry-read list. Created by **U1**.

## U1 — design doc Part 1

- Authored `docs/design/group-posts-design.md` **Part 1**: **Context**,
  **Scope** (in / named deferral list / privacy-first "not available"),
  **Invariants G·1–G·8** (8), **FACES G1–G13** (13), and the drift-guard (Part
  1). No code, no build. The Part 2 seams / 19 test names / gate are **U2**.
- Invariants pinned (each verbatim from the master register, each with a
  pin-note): **G·1** membership-only = the single access decision (audience never
  evaluated) · **G·2** lane exclusivity (`GroupId ≠ ""` ⇒ `ComponentId` empty,
  excluded from `ListFeedAsync`/`ListAllFeedAsync`) · **G·3** members-only
  authoring (create gate **is** the group-lane decision) · **G·4** nobody peeks
  (no moderator, no break-glass) · **G·5** audit per lane (feed = aggregate row,
  detail = decision row, in-transaction) · **G·6** delegation action-scoped ·
  **G·7** replies inherit the parent's single group-lane decision · **G·8**
  group-post `Audience` written non-null and **empty**.
- **Break-glass is not a deferral** — it is an explicit "not available" rule
  (G·4). **U3 (ADR 0013) / U5 (the lane) do NOT re-litigate it**: the answer is
  *no by design* (how-it-works.md trust pitch, ADR 0003 default-OFF, invariant
  C5). Do not put break-glass in any deferral list.
- Handing forward to **U2**: **FACES count = 13** (G1–G13). U2 pins the **19**
  seam-test names (Part 2 §2.5) and the three-test gate (§2.4) against these
  invariants; the invariant/FACES numbers are frozen for the rest of the
  milestone.
- `Post.GroupId` is the **single additive** on the `Post` POCO (ADR 0004 §B.1,
  the M3b `Status` ADD precedent) — **no `M3DocTypes` change**, **no** second
  doc surface; `PostReply` is unchanged. `AccessVia.Group` (8th value) + the two
  `CanSeeGroupAsync` overloads are the group lane's ADDs on
  `IAuthorizationService` (frozen signatures untouched — ADR 0006-E). The
  "## Group posts — Closed (recorded)" section is still an empty **placeholder**.
