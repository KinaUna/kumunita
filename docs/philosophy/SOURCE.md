# Provenance — FIG source & adoption

This folder is Kumunita's local adoption of the **Fractal Integration Guidelines (FIG)**.

## Source

- **Repo:** <https://github.com/KinaUna/fractal_integration>
- **Synced from commit:** `cb2a2fd29bb76371b46f57f88dfaafa1ea922328`
- **Date synced:** 2026-09-08
- **Synced by:** the small Kumunita team

## Re-sync cadence

Quarterly, matching the source repo's own review cadence. To re-sync:

1. Clone or pull `fractal_integration` `generic/` to a scratch location.
2. `diff -ru` each guideline core file against its Kumunita counterpart and
   review each hunk. Kumunita-specific wording (neighborhood, moderators,
   residents, access model, the community as product) is an **intentional
   deviation** and should be preserved unless a guideline change explicitly
   supersedes it.
3. Update this file's "committed synced from" hash and date, and append an
   entry to the Deviations log below.

## Deliberate local deviations (Kumunita is not a general-purpose product)

These are intentional, not drift. Do not revert to the guideline wording on
this pass without a matching guideline change that supersedes:

| Local file | What differs from guideline `generic/` | Why |
|---|---|---|
| `README.md` | Opens with "A neighborhood is a system…"; frames the product as "a shared place that holds." | Kumunita is *a* platform for *one* neighborhood, not a market-seeking product. "Product" in the guidelines corresponds to "community platform" here. |
| `START-HERE.md` | "the-platform-as-integrator.md" row (not `product-as-integrator.md`); every example refers to residents, moderators, access model, groups, reports, audit log. | Local domain guide is `the-platform-as-integrator.md`. |
| `domains-of-integration.md` | 6 product-native set is rephrased around community (Value, Fit, Trust, Lifecycle, Agency, Access) rather than market. | Kumunita has no paid tier, no growth funnel, no churn. The same integration questions apply to the community platform but with different parts. |
| `in-code.md` | Adds "the access model is the most load-bearing contract in the codebase" section; names the three modules (Identity, UserInfo, Authorization); audit log as the production feedback loop. | Kumunita's authorization model (thin token, fat authorization service, always-on audit) is its single biggest seam and deserves its own paragraph. |
| `in-product.md` | Frames "product operations" as running the platform *and* tending the community; names the moderation loop as the most important loop. | The two jobs (platform ops + community ops) are one system here. |
| `the-human-system.md` | Adds a "The platform lives inside the residents' lives" section and a "Privacy as room to be private" heuristic. | The community framing is Kumunita's distinguishing property. |
| `the-platform-as-integrator.md` | Kumunita-specific local domain guide (value chain Signals → Shared awareness → Understanding → Decisions → Coordination → Outcomes tuned to a neighborhood). | The guideline's `product-as-integrator.md` is the template Kumunita adapted. |
| `how-it-works.md` | Kumunita-specific resident-facing explainer. No direct counterpart in the guideline `generic/`. | Local addition. |
| `templates/*.md` | Adds Kumunita-specific rows (access model, report→moderator flow, restore-from-backup, moderator scoping) alongside the guideline template sections. | Templates need local context to be actionable. |
| `everyday-life.md` | "shared event" / "community project" / "group" rows in the neighborhood table instead of "product" / "team" / "work" rows. | Neighborhood is the community-scale row for Kumunita. |
| `examples/` | Copied from guideline `generic/examples/` (commit `cb2a2fd`); only the extension of `a-sports-team` was corrected to `a-sports-team.md`. | Domain-neutral; the guideline author explicitly expects adopters to treat `examples/` as a shared shelf. |
| `further-reading.md` | Copied from guideline `generic/further-reading.md` (commit `cb2a2fd`); "this repo" references were updated to "these guidelines." | Domain-neutral. |
| `SOURCE.md`, this file | No counterpart in the guideline — added by Kumunita per the guideline's own `adopting-fig.md`. | Required by the source repo's own adoption doc. |

## Adopter notes (from `adopting-fig.md`)

The three things this folder does to keep the link honest:

1. **Source commit and date recorded** — above.
2. **Deliberate local deviations noted** — the table above; also see inline
   `<!-- local: ... -->` markers in the files where they apply.
3. **Re-sync cadence set** — quarterly.

The core docs in this folder are kept close to verbatim to `generic/` on
every re-sync (except the Kumunita-specific lines called out above). Kumunita
wrote two local additions that the source repo has no counterpart for
(`how-it-works.md`, `the-platform-as-integrator.md` as a *community* platform
framing of what the guideline templates as `product-as-integrator.md`). New
domain guides in the same shape are welcome.
