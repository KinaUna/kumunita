#!/usr/bin/env python3
"""IMPROVE-DOCS D13 — stamp front-matter (+ abstracts) on docs/philosophy/**.

One-shot, idempotent (refuses to re-stamp a doc that already has front-matter).
Writes each file's front-matter + abstract block above the existing H1, then
verifies: every file now starts with `---`, every >60-line doc has an
`> **Abstract:**` in its first 20 lines, and the relative .md link targets
inside docs/philosophy all still resolve.
"""
import pathlib
import re
import sys

ROOT = pathlib.Path(__file__).resolve().parent.parent.parent
PHI = ROOT / "docs" / "philosophy"

DATE = "2026-11-02"

# rel path under docs/philosophy -> (fm, abstract-or-None)
# abstract line-1 must start with `> **Abstract:**` (the docs-report gate pattern).
DOCS = {
    "README.md": (
        """id: philosophy-hub
title: "Kumunita — Our Philosophy"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/philosophy/START-HERE.md
  - docs/philosophy/domains-of-integration.md
  - docs/philosophy/anti-patterns.md
  - docs/philosophy/everyday-life.md
  - docs/philosophy/in-code.md
  - docs/philosophy/in-product.md
  - docs/philosophy/SOURCE.md
  - docs/philosophy/adopting-fig.md""",
        """> **Abstract:** the hub of Kumunita's development guidelines — the six principles
> (quality is the quality of the linkage, differentiation is deliberate,
> bugs and value live at the seams, feedback loops are a feature, emergent
> properties can't be delegated, integration decays) plus the map of every
> doc in the folder and where each fits. **Who is it for:** everyone, at the
> level of "how we think." **When to read the rest:** before deciding which
> of the other philosophy docs to open.
>""",
    ),
    "SOURCE.md": (
        """id: philosophy-source
title: "Provenance — FIG source & adoption"
tier: living
kind: philosophy
status: current
audiences: [developer, agent]
related:
  - docs/philosophy/adopting-fig.md
  - docs/philosophy/README.md
  - https://github.com/KinaUna/fractal_integration""",
        """> **Abstract:** the provenance record for this folder — the upstream
> Fractal Integration Guidelines (FIG) repo, the exact commit and date
> synced from, the re-sync cadence, and the deliberate local deviations.
> **Who is it for:** the people who maintain the philosophy docs (and any
> agent asked to re-sync them). **When to read the rest:** before re-syncing
> from the source, or when asked why a doc here differs from upstream.
>""",
    ),
    "START-HERE.md": (
        """id: philosophy-start-here
title: "START HERE — the philosophy in one hour"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/philosophy/README.md
  - docs/philosophy/everyday-life.md
  - docs/philosophy/domains-of-integration.md
  - docs/philosophy/anti-patterns.md
  - docs/philosophy/examples/README.md
  - docs/philosophy/adopting-fig.md""",
        """> **Abstract:** the whole philosophy in one hour — the one argument
> (parts are cheap, the linkage is rare), the six principles, the three
> tests (closed-loop, handoff, part-vs-whole), the FACES check, and a
> deliberate 60-minute reading order through every doc in the folder.
> **Who is it for:** anyone who has not read the philosophy yet — it is the
> onboarding. **When to read the rest:** as the first philosophy read, or
> when a doc in the folder stops making sense.
>""",
    ),
    "adopting-fig.md": (
        """id: philosophy-adopting-fig
title: "Adopting FIG in Your Own Project"
tier: living
kind: philosophy
status: current
audiences: [resident, developer]
related:
  - docs/philosophy/SOURCE.md
  - docs/philosophy/anti-patterns.md
  - https://github.com/KinaUna/fractal_integration""",
        """> **Abstract:** how to bring these guidelines into your own project —
> repo, wiki, team handbook, or an AI-agent prompt — without losing the link
> back to the FIG source: keep the core, differentiate around it, record the
> sync, note deviations, re-sync on a cadence. **Who is it for:** someone
> adopting the guidelines elsewhere (or an agent doing it for them).
> **When to read the rest:** when the guidelines stop being something you
> read and start being something you copy.
>""",
    ),
    "anti-patterns.md": (
        """id: philosophy-anti-patterns
title: "Anti-patterns"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer, agent]
related:
  - docs/philosophy/README.md
  - docs/philosophy/in-code.md
  - docs/philosophy/in-product.md""",
        """> **Abstract:** the eight domain-general integration failures (part
> sprawl, the god part, distributed fragmentation, parts-work-seams-don't,
> signals-without-loops, accidental integration, silent coupling,
> reflection-without-actuation), each with its Kumunita symptom and fix —
> name the failure by the general name so the diagnosis stays portable.
> **Who is it for:** anyone diagnosing why something is failing. **When to
> read the rest:** the next time something goes wrong, and in every review
> and retro.
>""",
    ),
    "domains-of-integration.md": (
        """id: philosophy-domains
title: "Domains of Integration — the Lens"
tier: living
kind: philosophy
status: current
audiences: [developer, platform-maintainer, community-admin]
related:
  - docs/philosophy/START-HERE.md
  - docs/philosophy/further-reading.md
  - docs/philosophy/anti-patterns.md""",
        """> **Abstract:** the diagnostic lens — the nine domains the mind
> already integrates (re-mapped onto the systems we build), the six native to
> software, and the six that belong to the product reaching into a life.
> **Who is it for:** the people designing or reviewing changes here.
> **When to read the rest:** when a design feels wrong but nothing is
> "broken" — name the domain it is failing at.
>""",
    ),
    "everyday-life.md": (
        """id: philosophy-everyday-life
title: "Everyday Life — Integration in Plain Language"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin]
related:
  - docs/philosophy/START-HERE.md
  - docs/philosophy/examples/README.md""",
        """> **Abstract:** the same philosophy as the rest of the folder, told
> with things you can touch — cooking, a relay race, a shared event — no
> jargon, no diagrams, read start to finish in ten minutes. **Who is it
> for:** readers who don't live in the codebase. **When to read the rest:**
> as the first philosophy read if jargon isn't your language; if none of
> its examples fit, [`examples/`](examples/README.md) has more.
>""",
    ),
    "further-reading.md": (
        """id: philosophy-further-reading
title: "Further Reading — The Traditions Behind Fractal Integration"
tier: living
kind: philosophy
status: current
audiences: [developer]
related:
  - docs/philosophy/START-HERE.md
  - docs/philosophy/domains-of-integration.md""",
        """> **Abstract:** optional entry points into the four traditions these
> guidelines draw from — systems theory, cybernetics, modularity,
> interpersonal neurobiology — each with a short note on where it shows up
> in this folder. **Who is it for:** the curious, or the person handing a
> skeptical colleague something with more weight behind it. **When to read
> the rest:** when "why this matters" makes you want to check the source.
>""",
    ),
    "how-it-works.md": (
        """id: philosophy-how-it-works
title: "How Kumunita Works — for the Neighborhood"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin]
related:
  - docs/philosophy/everyday-life.md
  - docs/SECURITY.md
  - docs/product/features.md""",
        """> **Abstract:** what Kumunita does, how privacy and moderation
> actually behave, and how to give feedback that helps — no technical
> background needed; the entry point for anyone in the neighborhood who
> isn't a contributor. **Who is it for:** residents and the people who
> moderate. **When to read the rest:** before asking for a change, or when
> explaining the platform to someone.
>""",
    ),
    "in-code.md": (
        """id: philosophy-in-code
title: "Integration in Code"
tier: living
kind: philosophy
status: current
audiences: [developer, agent]
related:
  - docs/philosophy/anti-patterns.md
  - docs/philosophy/START-HERE.md
  - docs/adr/0001-core-stack-and-identity-model.md
  - docs/adr/0003-roles-and-moderator-scoping.md
  - docs/dev/testing-and-ci.md
  - docs/dev/razor-verification.md""",
        """> **Abstract:** the six principles turned into practice on this
> stack (a modular monolith: ASP.NET Core 10, Marten, Wolverine) —
> boundaries as contracts, seams as where we test, short feedback loops,
> coupling discipline, judging the whole, and the integration clauses of
> the Definition of Done. **Who is it for:** anyone who writes code here.
> **When to read the rest:** before a design or review, and before calling
> a change done.
>""",
    ),
    "in-product.md": (
        """id: philosophy-in-product
title: "Integration in the Community & the Platform"
tier: living
kind: philosophy
status: current
audiences: [platform-maintainer, community-admin]
related:
  - docs/philosophy/the-platform-as-integrator.md
  - docs/philosophy/the-human-system.md
  - docs/philosophy/anti-patterns.md
  - docs/ops/backups.md
  - docs/ops/restore.md
  - docs/adr/0003-roles-and-moderator-scoping.md""",
        """> **Abstract:** how we run the platform *and* the community as one
> system — the loops and their owners (fast, medium, slow), moderation as
> the most important loop, the local-optimization watch, cross-role
> integration, and releases as integration events. **Who is it for:** the
> people who run this deployment. **When to read the rest:** when deciding
> how to operate the platform, or when a loop isn't closing.
>""",
    ),
    "the-human-system.md": (
        """id: philosophy-human-system
title: "The Human as the Outermost System"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer]
related:
  - docs/philosophy/START-HERE.md
  - docs/philosophy/in-product.md""",
        """> **Abstract:** the outermost scale — the person is the system that
> contains all the others: well-being as integration, burnout as an
> integration failure, boundaries as designed seams, and what that means for
> how we build and how the platform treats its residents. **Who is it
> for:** everyone, but especially the people who decide trade-offs.
> **When to read the rest:** before any trade-off that costs a part
> (throughput, hours, engagement) at the expense of a whole (a person, a
> team, the neighborhood).
>""",
    ),
    "the-platform-as-integrator.md": (
        """id: philosophy-platform-as-integrator
title: "The Platform as an Integration Engine"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer]
related:
  - docs/philosophy/README.md
  - docs/philosophy/in-product.md
  - docs/product/features.md""",
        """> **Abstract:** why Kumunita exists, in our terms — the value chain
> (signals → shared awareness → understanding → decisions → coordination →
> outcomes), each arrow an integration step, and the two consequences for
> how we build. **Who is it for:** everyone asking "why is this worth
> building." **When to read the rest:** before scoping a feature, or when
> arguing for the whole over a part.
>""",
    ),
    "examples/README.md": (
        """id: philosophy-examples
title: "Examples — One Door Per Reader"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer]
related:
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/a-garden.md": (
        """id: philosophy-example-garden
title: "A Garden"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/a-road-trip.md": (
        """id: philosophy-example-road-trip
title: "A Road Trip"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/a-sports-team.md": (
        """id: philosophy-example-sports-team
title: "A Sports Team"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/attending-a-meeting.md": (
        """id: philosophy-example-meeting
title: "Attending a Meeting"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/calendar.md": (
        """id: philosophy-example-calendar
title: "A Calendar"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/digesting-food.md": (
        """id: philosophy-example-digestion
title: "Digesting Food"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/learning-a-language.md": (
        """id: philosophy-example-language
title: "Learning a Language"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "examples/running-a-household.md": (
        """id: philosophy-example-household
title: "Running a Household"
tier: living
kind: philosophy
status: current
audiences: [resident]
related:
  - docs/philosophy/examples/README.md
  - docs/philosophy/everyday-life.md""",
        None,
    ),
    "templates/design-doc.md": (
        """id: philosophy-template-design-doc
title: "Design Doc — template"
tier: living
kind: philosophy
status: current
audiences: [developer, agent]
related:
  - docs/philosophy/anti-patterns.md
  - docs/ops/restore.md""",
        None,
    ),
    "templates/postmortem.md": (
        """id: philosophy-template-postmortem
title: "Postmortem — template"
tier: living
kind: philosophy
status: current
audiences: [platform-maintainer, developer, community-admin]
related:
  - docs/philosophy/anti-patterns.md
  - docs/ops/incident-response.md""",
        None,
    ),
    "templates/retrospective.md": (
        """id: philosophy-template-retro
title: "Retro — template"
tier: living
kind: philosophy
status: current
audiences: [resident, community-admin, platform-maintainer, developer]
related:
  - docs/philosophy/anti-patterns.md
  - docs/philosophy/START-HERE.md""",
        None,
    ),
}


def block(fm: str, abstract: str | None) -> str:
    body = f"---\n{fm}\nupdated: {DATE}\n---\n\n"
    if abstract:
        body += abstract + "\n\n"
    return body


def main() -> int:
    stamped = 0
    for rel, (fm, abstract) in DOCS.items():
        p = PHI / rel
        first = p.read_text().splitlines()[0]
        if first.startswith("---"):
            print(f"SKIP (already stamped): {p.relative_to(ROOT)}")
            continue
        if not first.startswith("# "):
            print(f"ERROR: {rel} does not start with an H1: {first!r}")
            return 1
        p.write_text(block(fm, abstract) + p.read_text())
        stamped += 1
    print(f"stamped {stamped} files")

    # --- verification ------------------------------------------------------
    errors = []
    for rel in DOCS:
        p = PHI / rel
        text = p.read_text()
        lines = text.splitlines()
        if not text.startswith("---\n"):
            errors.append(f"{rel}: no front-matter")
            continue
        if lines[-1] != "---":
            errors.append(f"{rel}: unterminated front-matter")
        # id uniqueness handled after
        if (len(lines) - 2) > 60:
            first20 = lines[:20]
            if not any(re.match(r"^\s*>\s*\*\*(?:Abstract|TL;DR)", ln) for ln in first20):
                errors.append(f"{rel}: >60 lines, no Abstract in first 20 lines")
    ids = []
    for rel in DOCS:
        text = (PHI / rel).read_text()
        m = re.search(r"^id: (\S+)$", text, re.M)
        ids.append((m.group(1) if m else f"MISSING:{rel}", rel))
    seen = {}
    for i, r in ids:
        if i in seen:
            errors.append(f"duplicate id {i}: {r} and {seen[i]}")
        seen[i] = r
    for rel in DOCS:
        p = PHI / rel
        for t in re.findall(r"\]\(([^)]+\.md)[^)]*\)", p.read_text()):
            t = t.split("#")[0]
            if not t or t.startswith(("http", "//")):
                continue
            if not (p.parent / t).resolve().exists():
                errors.append(f"{rel}: broken link {t}")
    if errors:
        print("VERIFICATION FAILED:")
        for e in errors:
            print("  " + e)
        return 1
    print("verification OK: front-matter, abstracts, link targets, id uniqueness")
    return 0


if __name__ == "__main__":
    sys.exit(main())
