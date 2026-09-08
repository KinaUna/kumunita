# Adopting FIG in Your Own Project

> This doc is for when these guidelines stop being something you read and
> start being something you copy — into a repo, a wiki, a team handbook, or
> a prompt you hand an AI agent. It answers: how do you make them yours
> without losing the link back to the source?

For Kumunita, that link is recorded in [`SOURCE.md`](SOURCE.md): the
upstream source repo, the commit synced from, the date, and the list of
deliberate local deviations. That's the "designed seam" made concrete.

## Adopting this is itself an integration problem

The moment you copy any of this into your own project, you've done exactly
what principle 2 describes: you differentiated a part on purpose (your
local copy) out of a whole (the source repo). That copy now needs to be
integrated — with your project's language, and, over time, with whatever
the source becomes next. Skip that second half and you've made exactly the
anti-pattern this source repo warns about: a copy that drifts from its
source with nobody tracking why, which is
[`accidental integration`](anti-patterns.md) — or, worse,
[`silent coupling`](anti-patterns.md) — your team relying on a version of
FIG that no longer matches the one anyone else is reading.

There are two easy ways to get this wrong, and one steady way to get it
right.

**Wrong: copy-paste and forget.** You paste `START-HERE.md` into your wiki
once. It's useful for a month, then the source repo moves on — a principle
gets sharpened, a new domain guide ships, an anti-pattern gets renamed —
and your copy quietly becomes a fork nobody meant to create. Six months
later someone asks "wait, is this still current?" and no one can answer.
This is [`integration decays`](README.md) (principle 6), applied to the
guidelines about integration decaying.

**Also wrong: blind overwrite.** You script a pull that replaces your local
docs with source verbatim on every sync. This destroys the thing you
actually wanted — the domain-specific guide you wrote for *your* field,
which the source repo explicitly expects you to add (see "Applying it to
your domain" in `START-HERE.md`) and which the source has no way to know
about.

**Right: a designed seam.** Treat your local copy the way `in-code.md`
tells you to treat any boundary — a contract, not an accident:

1. **Keep the core, differentiate around it.** Pull the domain-neutral core
   (`README.md`, `START-HERE.md`, `anti-patterns.md`,
   `domains-of-integration.md`, `the-human-system.md`, `everyday-life.md`,
   `examples/`, `templates/`) close to verbatim. Write your own domain
   guide — Kumunita's is `the-platform-as-integrator.md` (a community
   platform, not a market-facing product); another team's might be
   `in-accounting.md`, `in-clinic-ops.md`, or the codebase's own
   conventions. That split is the seam: the core is what you sync, the
   domain guide is what you own.
2. **Record what you pulled and when.** At the top of your local copy — a
   comment, a short `SOURCE.md`, or a line in your README — note the
   source repo, the commit or tag you're synced to, and the date. This is
   the "memory" domain applied to your own adoption: an explicit
   artifact that says *why* your docs look the way they do, instead of
   leaving that only in someone's head. Kumunita's is `SOURCE.md`.
3. **Note your deliberate deviations.** If you trim a section or reword
   something for your team, say so in one line next to it (`<!-- local:
   trimmed the templates section, we use Linear's own retro doc -->`).
   That turns an overwrite risk into a diff you can read at a glance.
4. **Re-sync on a cadence, not never.** Do a quarterly pass, following the
   source repo's own review cadence: diff your local core docs against the
   source, pull in what changed, and re-check your deviations still make
   sense. A sync loop with no cadence is
   [`reflection without actuation`](anti-patterns.md) waiting to happen.

## Doing this with an AI agent

This is designed to be adopted by an agent, not just a person — that's
part of why it lives in a plain, public, well-structured repo instead of a
slide deck. A reasonable prompt for Kumunita:

> Read `README.md` and `START-HERE.md` from
> `https://github.com/KinaUna/fractal_integration`. Write a domain guide
> for the community platform (a shared place for one neighborhood) in the
> same shape as `in-code.md`: the six principles turned into our
> practices, with access-model and privacy as the load-bearing contracts.
> Copy the core docs in as-is, note the commit you synced from in
> `SOURCE.md`, and leave our project-specific conventions out of the core
> files — they belong in the new domain guide.

For a codebase, the mechanical version of "keep the core, differentiate
around it" is whatever tooling you already have for tracking an external
source at a point in time — a git submodule or subtree pointed at the
source, or simply a pinned commit hash recorded in the file (Kumunita's is
`SOURCE.md`). None of that is required; a dated note is enough to start.
The point isn't the tooling, it's that the link back to source is a
designed, visible thing — not an assumption.

## Adoption checklist

- [ ] Core docs copied in, close to verbatim.
- [ ] A domain guide written for your specific field or project.
- [ ] Source commit/tag and date recorded somewhere visible.
- [ ] Any deliberate local deviations noted inline (in the file, in
      `SOURCE.md`, or in the domain guide's own header).
- [ ] A re-sync cadence set (quarterly is a reasonable default).

That's the whole pattern: differentiate deliberately, link back explicitly,
and revisit on a schedule. It's principles 1, 2, and 6 of `README.md`,
pointed at the guidelines themselves.
