# Integration in the Community & the Platform

> The conceptual foundation for everything in this doc is
> [`the-platform-as-integrator.md`](the-platform-as-integrator.md). Read it
> first — it answers *why the platform exists*. This doc is *how we run it*.

This is a self-hosted platform for **one** neighborhood, run by a small team
for its residents. So "product operations" here means two intertwined things:
running the *platform* (deployments, backups, moderation) and tending the
*community* (keeping it a shared place that holds). We treat them as one
system.

## The platform is a system

Parts: components, posts, events, projects, the directory, the access model,
moderation, the team, the neighborhood itself. The platform's behavior —
trust, belonging, safety — is emergent. It can't be assigned to a feature, so
we protect it by watching the whole.

## Seams are where residents meet the whole

Residents don't experience features. They experience the seams between them:
signing up and being verified, posting to the right audience, a report
actually reaching a moderator, an event turning into real attendance, "can my
family member see my posts?" We invest in seams at least as heavily as in
features.

## Every loop needs an actuator

- **Fast (minutes–hours):** deploy signals, the audit log, failed handlers
  (email, reminders), error rates.
- **Medium (days–weeks):** moderation queue, reports filed, who's active,
  what's going stale.
- **Slow (months):** is the neighborhood actually using this? Are people
  relying on it? Are residents staying private by choice, or by confusion?

A loop without a scheduled response action is decoration. Every loop has an
owner, a cadence, and a defined response. A loop that closes but nothing
changes is [`reflection without actuation`](anti-patterns.md).

Moderation is the most important loop in the system: report → moderator with
audited access → resolution → record. If that loop is slow, missing, or
unaudited, the community loses trust faster than any feature can rebuild it.

## Watch local optimization

Optimizing one number (post count, active users, engagement) while the whole
(privacy, trust, a sense of safety) decays is the classic systems failure —
and the one a *community* platform can least afford. Before committing to a
goal, ask: *which part are we optimizing, and what does the neighborhood pay
for it?* A feed that "works" while residents feel surveilled or drained has
failed at the seam with their lives. See [`the-human-system.md`](the-human-system.md).

## Cross-role integration

The team, the moderators, and the administrators must share one end-to-end
model of the platform — especially of the access model. Where their mental
models diverge, seams break silently: a moderator assumes a group "obviously"
includes the board, a resident assumes their post is private to neighbors.
Design reviews, a shared written model of the access rules, and clear
moderator scoping (ADR 0003) are our integration harnesses. A feature that
works technically but confuses the residents failed at a seam we didn't see.

## Releases are integration events

The deploy pipeline (Coolify, one instance per neighborhood), migrations
(Marten's versioned migrations), and the upgrade path are integration harnesses
that let us change the platform with controlled risk. The rollout and rollback
plan — and a clean restore from backup — are part of the design, not an
afterthought (see [`../OPS.md`](../OPS.md)).

## Tending the community is part of the loop

The platform is only as healthy as the neighborhood using it. Some of this is
operational (keep it up, keep it backed up, keep it private); some is
relational (help moderators, answer "how does this work" plainly, say "no"
when a feature would optimize a metric at the community's expense). The
retrospective and postmortem templates in [`templates/`](templates/) cover
both.
