# Kumunita — Our Philosophy

A neighborhood is a system of **differentiated parts whose integrated
functioning produces a property no part has on its own**: many different
people, households, and concerns, whose *linkage* — who knows whom, who can
rely on whom, how a problem actually gets solved across people — produces a
community. No single resident is a neighborhood.

Kumunita is the software that builds and holds that linkage. So we develop it
by the same rule we ask of it: **quality is the quality of the linkage, not
the sum of the parts.** Because that rule repeats at every scale — a module,
a feature, a community, the people who build it — we hold it through the
whole project, and call the docs in this folder Kumunita's **development
guidelines**.

👉 **Start here:** [`START-HERE.md`](START-HERE.md) — one argument, one hour.

This is a self-hosted, single-neighborhood platform built by a small team for
a real community. Nothing here is a growth playbook: we optimize for trust,
privacy, and a shared place that holds — not for scale, churn, or a metric.
Where a generic "product" doc talks about the market, ours talks about the
neighborhood and the lives inside it.

## How we think

1. **Quality is the quality of the linkage.** A platform is not good because
   each screen works; it is good because the connections hold — a post reaches
   the right people, a report reaches a moderator *with an audit trail*, a
   resident's private detail stays private while the community still functions.
   Most failures are seam failures, not component failures.

2. **Differentiation is deliberate, integration is the work.** We split on
   purpose: into functional **components** (Safety, Maintenance, Social,
   Governance…), into **modules** (Identity, UserInfo, Authorization), into
   **groups**. Every boundary we create is a place where access, trust, and
   value must now be designed, tested, and maintained. Parts without linkage
   are just a bag of features.

3. **Bugs and value both live at the seams.** The authorization model,
   delegation, group membership, the report→moderator flow — these are where
   privacy can leak *and* where trust is created. We test, observe, and invest
   there first.

4. **Feedback loops are a feature.** Audit is always on. A report closes a
   loop. Moderation is a loop. A scheduled reminder is a loop. A signal with no
   owner and no response decays into noise. We build the shortest honest path
   from a resident's action to a signal we can act on.

5. **Emergent properties can't be delegated.** Trust, belonging, safety, a
   sense of a shared place — none of these is assignable to a single feature,
   module, or admin. They exist only in the integrated whole. We judge the
   community the platform serves, not the part.

6. **Integration decays.** Group definitions go stale, access rules drift,
   "how moderation works here" becomes tribal knowledge, tests go quiet.
   ADRs, review, audit, and retrospectives are how we close that decay.

## Where each doc fits

**The core**

- **Onboarding:** read [`START-HERE.md`](START-HERE.md) top to bottom — that
  *is* the onboarding.
- **How the platform works, for residents:**
  [`how-it-works.md`](how-it-works.md) — what the platform does, how privacy
  and moderation actually behave, and how to give feedback that helps. No
  technical background needed; the entry point for anyone in the
  neighborhood who isn't a contributor.
- **In plain language:** [`everyday-life.md`](everyday-life.md) — the same
  idea told with everyday examples, for readers who don't live in the
  codebase.
- **How it fails:** [`anti-patterns.md`](anti-patterns.md) — the catalog of
  integration failures, and what to do instead.
- **The human system:** [`the-human-system.md`](the-human-system.md) is the
  outermost scale. It applies to how we build, and to the residents whose
  lives the platform sits inside.

**Applying it to Kumunita**

- **Why the platform exists:** [`the-platform-as-integrator.md`](the-platform-as-integrator.md)
  — the reason Kumunita is worth building, in our terms.
- **How we build code:** [`in-code.md`](in-code.md) — the principles turned
  into practice on this stack.
- **How we run the platform & community:** [`in-product.md`](in-product.md).

**A lens for diagnosing failures**

- [`domains-of-integration.md`](domains-of-integration.md) maps the kinds of
  linkage a working system needs. When a design feels wrong but nothing is
  "broken," name the domain it is failing at.

**Using it in the work**

- **Every review and retro:** the three tests — closed-loop? handoff?
  part-vs-whole? — plus a FACES score, the five faces of a healthy system
  (details in [`START-HERE.md`](START-HERE.md)).
- **Designing a change:** use [`templates/design-doc.md`](templates/design-doc.md).
  The "Seams & contracts" section is mandatory.
- **When something breaks:** use [`templates/postmortem.md`](templates/postmortem.md).
- **End of a cycle:** use [`templates/retrospective.md`](templates/retrospective.md).

This doc is itself a living system. We review it as the project grows: what
does our practice show to be true, false, or missing?

## Relationship to the other docs

The philosophy says *how to think*; it does not replace the concrete
decisions. Where a specific technical choice was made and why, the
[`../adr/`](../adr/) records are authoritative. Where the security and
privacy commitments are spelled out, [`../SECURITY.md`](../SECURITY.md) is
authoritative. This folder supplies the lens through which those decisions
are judged.
