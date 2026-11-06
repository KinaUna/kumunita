using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Marten;

namespace Kumunita.Core.Bootstrap;

/// <summary>
/// The **seeded platform-content surface** — split out of <see cref="FirstBootSeeder"/>
/// (IMPROVE-CODE-2 / C2-05, DoD #3/#4 — the still-large files off the <c>files</c> baseline)
/// as a genuine part-vs-whole seam. This type owns the two concerns <see
/// cref="FirstBootSeeder"/> does not:
/// <list type="bullet">
/// <item><b>The seeded-content <c>data</c> registry (C02, ADR 0289)</b> — the
/// embedded <c>Bootstrap/GuidePages/{lang}/*.md</c> + <c>order.json</c> /
/// <c>parent-map.json</c> and <c>Bootstrap/DefaultPages/{lang}/*.md</c> +
/// <c>index.json</c> resources, read by <see cref="GuidePages"/> /
/// <see cref="EnDefaultPages"/> / <see cref="DeDefaultPages"/> /
/// <see cref="FrDefaultPages"/> / <see cref="DaDefaultPages"/> /
/// <see cref="GuideParentMap"/> (and the <c>de</c> / <c>fr</c> / <c>da</c>
/// guide baselines). Pure data — no behaviour, no session.</item>
/// <item><b>The ongoing seeded-content <c>lifecycle</c></b> — the warm-boot
/// backfills (<see cref="BackfillUserGuidesAsync"/> /
/// <see cref="BackfillGuideTranslationsAsync"/> /
/// <see cref="BackfillPageTranslationsAsync"/> /
/// <see cref="BackfillUiStringBaselinesAsync"/>), the operator-requested
/// reset-to-seeded lane (<see cref="ResetSeededTextAsync"/>), and the display
/// probes (<see cref="HasSeededText"/> / <see cref="AllSeededSlugs"/>).
/// These run on <b>warm</b> boots (the pristine gate the first-boot seeder
/// runs under never fires) or on an admin's explicit request — a distinct
/// caller surface from <see cref="FirstBootSeeder"/>.</item>
/// </list>
/// <para>
/// The dependency is one-directional: <see cref="FirstBootSeeder"/> (the
/// first-boot materializer) reads the registries here; nothing here calls back
/// into the seeder. Both remain in the <c>Kumunita.Core.Bootstrap</c> module,
/// so the split is a code-shape change only (same module, same documents, same
/// create-if-missing invariants — ADR 0042 D1 — not a logic change).
/// </para>
/// </summary>
public static class SeededContentCatalog
{
    private const string GuideRoot = "Kumunita.Core.Bootstrap.GuidePages.";
    private const string DefaultRoot = "Kumunita.Core.Bootstrap.DefaultPages.";
    private static readonly System.Reflection.Assembly AssemblyRef = typeof(SeededContentCatalog).Assembly;
    /// <summary>One default-page index entry (slug → display title); the body lives in the per-slug .md.</summary>
    private sealed record DefaultPageIndexEntry(string Slug, string Title);
    /// <summary>Read one language's guide set in seed order (the body per slug; the title is the body's first `## ` heading).</summary>
    private static (string Slug, string Title, string Body)[] LoadGuideSet(string lang)
    {
        var prefix = GuideRoot + lang + ".";
        var order = ReadJson<List<string>>(prefix + "order.json");
        var result = new (string, string, string)[order.Count];
        for (var i = 0; i < order.Count; i++)
        {
            var slug = order[i];
            var body = ReadText(prefix + slug + ".md");
            result[i] = (slug, FirstHeading(body), body);
        }
        return result;
    }
    /// <summary>Read one language's four-surface default-page set in registry order (the title is the index's display title, not the heading).</summary>
    private static (string Slug, string Title, string Body)[] LoadDefaultSet(string lang)
    {
        var prefix = DefaultRoot + lang + ".";
        var index = ReadJson<List<DefaultPageIndexEntry>>(prefix + "index.json");
        var result = new (string, string, string)[index.Count];
        for (var i = 0; i < index.Count; i++)
            result[i] = (index[i].Slug, index[i].Title, ReadText(prefix + index[i].Slug + ".md"));
        return result;
    }
    /// <summary>Read the nested-guide parent map (ADR 0160): child slug → parent slug.</summary>
    private static IReadOnlyDictionary<string, string> LoadParentMap()
    {
        var map = ReadJson<Dictionary<string, string>>(GuideRoot + "parent-map.json");
        return map;
    }
    private static string ReadText(string name)
    {
        using var stream = AssemblyRef.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Seeded-content resource '{name}' is missing from the Kumunita.Core assembly — check the Bootstrap/GuidePages and Bootstrap/DefaultPages embedded resources.");
        using var reader = new System.IO.StreamReader(stream);
        return reader.ReadToEnd();
    }
    private static T ReadJson<T>(string name)
    {
        var text = ReadText(name);
        var value = System.Text.Json.JsonSerializer.Deserialize<T>(text);
        return value ?? throw new InvalidOperationException($"Seeded-content resource '{name}' failed to deserialize.");
    }
    private static string FirstHeading(string body)
    {
        foreach (var raw in body.Split('\n'))
        {
            var t = raw.TrimStart();
            if (t.StartsWith("## ")) return t[3..].Trim();
        }
        return string.Empty;
    }
    /// <summary>
    /// UG (ADR 0057) — **warm-boot backfill** of the guide <see cref="Page"/>
    /// docs for a deployment whose first boot predates the guides: it has the
    /// canonical <c>help</c> page but not its guide children. Mirrors the
    /// <see cref="BackfillPageTranslationsAsync"/> /
    /// <see cref="BackfillUiStringBaselinesAsync"/> shape (ADR 0047 D2 /
    /// ADR 0052) — **create-if-missing only** (the ADR 0042 D1 invariant): a
    /// guide that already exists under <c>help</c> (a community-edited guide,
    /// or a resident-created page that happens to use the same slug) is
    /// skipped, never refreshed — a code-wins refresh would clobber that edit.
    /// Only the absent guides are created, from the same
    /// <see cref="GuidePages"/>() registry the first-boot seeder writes, so a
    /// fresh and a backfilled instance carry the same <c>en</c> floor.
    /// <para>
    /// Idempotent: a second run finds every guide it created on the first run
    /// and skips (the create-if-missing path). No tombstones, no deletes.
    /// </para>
    /// </summary>
    public static async Task BackfillUserGuidesAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve the canonical `help` page (the guides hang from it, ADR
        // 0057 D1). On a warm boot `help` is committed, so a query finds it —
        // unlike the first-boot path, where it is still in-flight. Primary
        // resolution is `system/help` (ADR 0040); the bare-slug fallback
        // covers a pre-ADR-0040 deployment whose `help` is still an orphan
        // root (the same fallback shape as BackfillPageTranslationsAsync).
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        Page? help = null;
        if (systemRoot is not null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        if (help is null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == null && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        if (help is null)
        {
            return;   // no canonical `help` page to hang the guides from — nothing to backfill.
        }

        // ADR 0160 — nested guide parents: the same closed parent map the
        // first-boot path reads (SeedUserGuidesAsync), so a warm boot that
        // predates the nested guide set backfills them under the correct
        // top-level guide (not directly under `help`). The slug → page-Id map
        // tracks each guide as it is created / found, so a nested guide's
        // parent (a top-level guide) is already in the map by the time the
        // nested guide is reached (top-level entries precede their children).
        var parentMap = GuideParentMap();
        var pageIdBySlug = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["help"] = help.Id,
        };

        foreach (var (slug, title, body) in GuidePages())
        {
            string parentId = help.Id;
            if (parentMap.TryGetValue(slug, out var parentSlug)
                && pageIdBySlug.TryGetValue(parentSlug, out var parentGuideId))
            {
                parentId = parentGuideId;
            }

            var existing = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && p.ParentId == parentId && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (existing is null)
            {
                var newGuide = new Page
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    Slug = slug,
                    ParentId = parentId,   // under `help` or a top-level guide (ADR 0057 D1 / ADR 0160)
                    Kind = PageKind.System,   // a platform guide (ADR 0040 standing matrix)
                    Title = title,
                    Body = body,
                    LanguageCode = FirstBootSeeder.SourceLanguage,   // authored-in `en` (the floor)
                    Audience = null,            // public — world-readable (the how-to is not gated)
                    AuthorId = string.Empty,    // platform content — no resident author
                    Created = now,
                    Modified = now,
                };
                session.Store(newGuide);
                pageIdBySlug[slug] = newGuide.Id;
            }
            else
            {
                pageIdBySlug[slug] = existing.Id;
            }
            // else: skip the store — create-if-missing (never overwrite a community edit; ADR 0042 D1).
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — **warm-boot backfill** of the
    /// <c>de</c> / <c>fr</c> / <c>da</c> <see cref="PageTranslation"/> rows
    /// for the resident guides: the gap a deployment whose first boot predates
    /// this baseline lane has — the guides' <c>en</c> body (the
    /// <see cref="GuidePages"/>() floor, created by
    /// <see cref="BackfillUserGuidesAsync"/> or the original first-boot seed)
    /// but no translation rows, so a German / French / Danish-speaking
    /// resident reads the <c>en</c> body for every guide.
    /// <para>
    /// Mirrors <see cref="BackfillPageTranslationsAsync"/> (the four-surface
    /// set's baseline backfill) — **create-if-missing only** (the ADR 0042 D1
    /// invariant): a row for a (guide, language) pair that already exists — a
    /// human Translator's edit in the in-app editor — is skipped, never
    /// refreshed. Only the absent rows are created, from the same
    /// <see cref="DeGuidePages"/> / <see cref="FrGuidePages"/> /
    /// <see cref="DaGuidePages"/> baselines the first-boot seeder uses, so a
    /// fresh and a backfilled instance carry the same non-<c>en</c> baseline.
    /// </para>
    /// <para>
    /// Idempotent: a second run finds every row it created on the first run
    /// and skips (the create-if-missing path). No tombstones, no deletes.
    /// </para>
    /// </summary>
    public static async Task BackfillGuideTranslationsAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // Resolve the canonical `help` page (the guides hang from it, ADR
        // 0057 D1) — the same `system/help` primary + bare-`help` fallback as
        // BackfillUserGuidesAsync, so the two backfills agree on the parent.
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        Page? help = null;
        if (systemRoot is not null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == systemRoot.Id && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }
        if (help is null)
        {
            help = await session
                .Query<Page>()
                .Where(p => p.Slug == "help" && p.ParentId == null && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
        }

        if (help is null)
        {
            return;   // no canonical `help` page — nothing to backfill translations for.
        }

        // ADR 0160 — the backfill must reach BOTH top-level guides (direct
        // children of `help`) AND the nested guides (children of their top-level
        // parent). A guide's slug is unique across the whole tree (the
        // `(ParentId, Slug)` unique index guarantees at most one page per slug
        // under any single parent, and a guide slug never appears under two
        // different parents — the ADR 0160 D1 closed-set rule), so a slug-only
        // query is unambiguous. A defensive id set keeps the query scoped to
        // the guide subtree in case a non-guide page ever shares a slug.
        var topLevelGuideIds = (await session
            .Query<Page>()
            .Where(p => p.ParentId == help.Id && p.IsDeleted == false)
            .Select(p => p.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToList();
        var allGuideIds = (await session
            .Query<Page>()
            .Where(p => (p.ParentId == help.Id || topLevelGuideIds.Contains(p.ParentId!))
                        && p.IsDeleted == false)
            .Select(p => p.Id)
            .ToListAsync(ct)
            .ConfigureAwait(false)).ToList();

        foreach (var (slug, _, _) in GuidePages())
        {
            var guide = await session
                .Query<Page>()
                .Where(p => p.Slug == slug && allGuideIds.Contains(p.Id) && p.IsDeleted == false)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (guide is null)
                continue;   // the guide itself is absent (top-level or nested); BackfillUserGuidesAsync owns that gap.

            // The de/fr/da baselines for this guide, create-if-missing (ADR 0042 D1).
            foreach (var (code, baselines) in new[]
            {
                ("de", DeGuidePages()),
                ("fr", FrGuidePages()),
                ("da", DaGuidePages()),
            })
            {
                var baseline = baselines.FirstOrDefault(b => b.Slug == slug);
                if (baseline == default)
                    continue;   // defensive — the slug is not in the baseline set; skip rather than throw.

                var existing = await session
                    .Query<PageTranslation>()
                    .Where(t => t.PageId == guide.Id && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new PageTranslation
                    {
                        Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                        PageId = guide.Id,
                        LanguageCode = code,
                        Title = baseline.Title,
                        Body = baseline.Body,
                        AuthorId = string.Empty,   // platform content — no resident author
                        Created = now,
                    });
                }
                // else: skip — create-if-missing (never overwrite a community edit; ADR 0042 D1).
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
    /// <summary>
    /// UG (ADR 0057) — the canonical <c>en</c> guide bodies: the single source
    /// of the resident-facing guide text (the seeder writes exactly this, and
    /// the <c>UG</c> drift-pin test reads exactly this — the ADR 0042 D1
    /// "the registry is the single source both the seeder and these tests
    /// read, so mirroring is exact" shape, applied to the guides). The closed
    /// set (the ADR 0040 <c>PageKind</c> closed-set rule applied to the guide
    /// set) — a new guide is a new lane, not a silent addition. Each entry is
    /// a <see cref="Page"/> nested under the canonical <c>help</c> page
    /// (ADR 0057 D1), written at the ADR 0042 D2 bar (plain language, the
    /// resident's steps, no code jargon, sentence case).
    /// </summary>
    public static (string Slug, string Title, string Body)[] GuidePages()
    {
            return LoadGuideSet("en");
    }
    /// <summary>
    /// ADR 0160 — the **nested guide parent map**: for each guide slug that
    /// is a *child* of another guide (rather than a direct child of
    /// <c>help</c>), the map gives the slug of its parent guide.
    /// <para>
    /// A slug absent from this map hangs directly from the canonical
    /// <c>help</c> page (the ADR 0057 D1 shape, unchanged for all
    /// top-level guides). A slug present in this map hangs from the page
    /// seeded under the parent slug's <c>help</c>-relative path, so a
    /// resident reads e.g. <c>/pages/system/help/settings/timezone</c>
    /// (the <c>PagePaths.Derive</c> chain: <c>system → help → settings →
    /// timezone</c>).
    /// <para>
    /// The parent map is a **closed set** (the ADR 0057 "closed set" rule
    /// applied to nesting): a new nested guide is a new lane, not a silent
    /// addition. The map is the single source both
    /// <see cref="SeedUserGuidesAsync"/> and
    /// <see cref="BackfillUserGuidesAsync"/> read, so the seeder and the
    /// warm-boot backfill can never drift on the nesting shape.
    /// </para>
    /// </summary>
    public static IReadOnlyDictionary<string, string> GuideParentMap()
    {
            return LoadParentMap();
    }
    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — the curated <c>de</c> baseline for
    /// the resident guides: the single source of the seeded German guide
    /// bodies (the same shape as <see cref="GuidePages"/>() — a public static
    /// array of <c>(Slug, Title, Body)</c> tuples). A full translation of the
    /// <see cref="GuidePages"/>() bodies — the same Markdown structure
    /// (heading, the step blocks, the cross-links), idiomatic German UI copy at
    /// the ADR 0042 D2 bar (the <c>du</c> register held, sentence case, no
    /// word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine DB
    /// (ADR 0042 D1 — seeded once, then community-owned; the in-app editor
    /// supersedes them and no later deploy reverts a human edit).
    /// <para>
    /// <b>Cross-links are absolute.</b> A guide's canonical URL is
    /// <c>/pages/system/help/{slug}</c> (the <c>system</c> root is part of the
    /// derived path — the guides are grandchildren of <c>system</c>), so a
    /// sibling link like the <c>en</c> floor's <c>[posts](posts)</c> does not
    /// resolve on the tree-browse page. These translations therefore link with
    /// the absolute form <c>[posts](/pages/system/help/posts)</c>, which is a
    /// relative <c>/</c>-rooted path the <see cref="Kumunita.Web.Security.MarkdownRenderer"/>
    /// <c>IsSafeUrl</c> allowlist accepts. The <c>en</c> floor
    /// (<see cref="GuidePages"/>) is untouched — only the seeded baselines use
    /// the absolute form, and both render identically for the resident.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DeGuidePages()
    {
            return LoadGuideSet("de");
    }
    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — the curated <c>fr</c> baseline for
    /// the resident guides: the single source of the seeded French guide
    /// bodies (the same shape as <see cref="GuidePages"/>() — a public static
    /// array of <c>(Slug, Title, Body)</c> tuples). A full translation of the
    /// <see cref="GuidePages"/>() bodies — the same Markdown structure
    /// (heading, the step blocks, the cross-links), idiomatic French UI copy at
    /// the ADR 0042 D2 bar (the familiar <c>tu</c> register held, sentence
    /// case, no word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine DB
    /// (ADR 0042 D1 — seeded once, then community-owned; the in-app editor
    /// supersedes them and no later deploy reverts a human edit).
    /// <para>
    /// <b>Cross-links are absolute</b> — see <see cref="DeGuidePages"/>() for
    /// the rationale (a guide's canonical URL is
    /// <c>/pages/system/help/{slug}</c>; the <c>en</c> floor's relative links
    /// do not resolve on the tree-browse page, so the baselines link with the
    /// absolute <c>/pages/system/help/…</c> form, which the
    /// <see cref="Kumunita.Web.Security.MarkdownRenderer"/> allowlist accepts).
    /// The <c>en</c> floor is untouched.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] FrGuidePages()
    {
            return LoadGuideSet("fr");
    }
    /// <summary>
    /// UG (ADR 0057, amended 2026-09-21) — the curated <c>da</c> baseline for
    /// the resident guides: the single source of the seeded Danish guide
    /// bodies (the same shape as <see cref="GuidePages"/>() — a public static
    /// array of <c>(Slug, Title, Body)</c> tuples). A full translation of the
    /// <see cref="GuidePages"/>() bodies — the same Markdown structure
    /// (heading, the step blocks, the cross-links), idiomatic Danish UI copy
    /// (the familiar <c>dig</c> register held, sentence case, no word-for-word
    /// calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine DB
    /// (ADR 0042 D1 — seeded once, then community-owned; the in-app editor
    /// supersedes them and no later deploy reverts a human edit).
    /// <para>
    /// <b>Cross-links are absolute</b> — see <see cref="DeGuidePages"/>() for
    /// the rationale. The <c>en</c> floor is untouched.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DaGuidePages()
    {
            return LoadGuideSet("da");
    }
    /// <summary>
    /// The canonical <c>en</c> default-page bodies (terms + help + privacy +
    /// conduct — the four <c>Page</c>-backed surfaces of the ADR 0043 D1
    /// five-surface set). The single source of the seed text: the
    /// <see cref="Kumunita.Core.Pages.Page"/> docs (PG U05, ADR 0039 §3.9; the
    /// two added in SP U01 per ADR 0043 D2/D5) carry <em>exactly</em> this
    /// text, so a fresh instance is byte-identical for <c>/terms</c> /
    /// <c>/help</c> / <c>/privacy</c> / <c>/conduct</c> read from the tree.
    /// <para>
    /// <b><c>about</c> is deliberately absent</b> (the U05 drift pin): a fresh
    /// instance's <c>/about</c> is the <em>full-bleed product-story view</em>
    /// (<c>Views/StaticPages/About</c>, driven by <c>HomeViewModel</c>), not a
    /// Markdown body. Seeding an <c>about</c> <see cref="Kumunita.Core
    /// .Pages.Page"/> would change what <c>/about</c> renders and break the
    /// "byte-identical to today" exit gate — so the seeder never writes it and
    /// the <c>StaticPagesController</c> keeps its product-story fallback. This
    /// deviates from the plan's "seed all three" wording in favor of the
    /// byte-identical gate (recorded in the U05 handoff notes).
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] EnDefaultPages()
    {
            return LoadDefaultSet("en");
    }
    /// <summary>
    /// LS U04 (ADR 0042 D2); SP U02 (ADR 0043 D1/D3) — the curated <c>de</c>
    /// baseline for the seeded default pages (terms + help + privacy +
    /// conduct — the full four-page set of the ADR 0043 D1 five-surface
    /// set). A full translation of the
    /// <see cref="EnDefaultPages"/>() bodies — the same Markdown structure
    /// (heading, intro, the bullets, the closing line), idiomatic
    /// German UI copy at the ADR 0042 D2 bar (the <c>du</c> register held,
    /// sentence case, no word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine
    /// DB (ADR 0042 D1 — seeded once, then community-owned; the in-app
    /// editor supersedes them and no later deploy reverts an admin edit).
    /// <para>
    /// <b><c>about</c> is deliberately absent</b>, exactly as in
    /// <see cref="EnDefaultPages"/>() — it is the registry-key surface
    /// (the <c>about.*</c> keys in <see cref="KnownTranslationKeys"/>),
    /// not a Markdown body.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DeDefaultPages()
    {
            return LoadDefaultSet("de");
    }
    /// <summary>
    /// LS U04 (ADR 0042 D2); SP U02 (ADR 0043 D1/D3) — the curated <c>fr</c>
    /// baseline for the seeded default pages (terms + help + privacy +
    /// conduct — the full four-page set of the ADR 0043 D1 five-surface
    /// set). A full translation of the
    /// <see cref="EnDefaultPages"/>() bodies — the same Markdown structure
    /// (heading, intro, the bullets, the closing line), idiomatic
    /// French UI copy at the ADR 0042 D2 bar (the <c>tu</c> register held,
    /// accents and typography per French convention, no word-for-word
    /// calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine
    /// DB (ADR 0042 D1 — seeded once, then community-owned; the in-app
    /// editor supersedes them and no later deploy reverts an admin edit).
    /// <para>
    /// <b><c>about</c> is deliberately absent</b>, exactly as in
    /// <see cref="EnDefaultPages"/>() — it is the registry-key surface
    /// (the <c>about.*</c> keys in <see cref="KnownTranslationKeys"/>),
    /// not a Markdown body.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] FrDefaultPages()
    {
            return LoadDefaultSet("fr");
    }
    /// <summary>
    /// The Danish (<c>da</c>) baseline — the curated <c>da</c> translation of
    /// the seeded default pages (terms + help + privacy + conduct). A full
    /// translation of the <see cref="EnDefaultPages"/>() bodies — the same
    /// Markdown structure (heading, intro, the bullets, the closing line),
    /// idiomatic Danish UI copy (the familiar <c>dig</c> register held,
    /// sentence case, no word-for-word calques). These ship as
    /// <see cref="Kumunita.Core.Pages.PageTranslation"/> rows on a pristine
    /// DB (seeded once, then community-owned; the in-app editor supersedes
    /// them and no later deploy reverts an admin edit). The <c>da</c> catalog
    /// row is seeded disabled (the pre-seeded lane), but its page baseline is
    /// present so the moment an admin enables Danish, the pages are ready.
    /// <para>
    /// <b><c>about</c> is deliberately absent</b>, exactly as in
    /// <see cref="EnDefaultPages"/>() — it is the registry-key surface
    /// (the <c>about.*</c> keys in <see cref="KnownTranslationKeys"/>),
    /// not a Markdown body.
    /// </para>
    /// </summary>
    public static (string Slug, string Title, string Body)[] DaDefaultPages()
    {
            return LoadDefaultSet("da");
    }
    /// <summary>
    /// ADR 0058 — the **display probe**: does the <see cref="Page"/> at
    /// (<paramref name="slug"/>, <paramref name="parentId"/>) have a
    /// <b>seeded</b> <c>en</c> baseline and at least one non-<c>en</c>
    /// (de / fr / da) baseline to reset to? The Web edit view calls this to
    /// decide whether to render the "Reset to seeded" affordance (a
    /// <b>display</b> pin, not a gate — the real deny is the
    /// <see cref="Kumunita.Core.Pages.PageService.ResetToSeededAsync"/>
    /// standing re-check). <c>false</c> for a page the seed never wrote (a
    /// resident-authored system page, a blog page, a page with a custom slug)
    /// — there is no seeded text to reset to, so there is nothing to offer.
    /// <para>
    /// The parent resolution mirrors the seeder / backfill: the four-surface
    /// set resolves under the <c>system</c> root (ADR 0040), the guides
    /// resolve under the canonical <c>help</c> page (ADR 0057 D1), with the
    /// bare-root fallback for a pre-ADR-0040 deployment.
    /// </para>
    /// </summary>
    public static bool HasSeededText(string slug)
    {
        // The probe is purely over the code-owned seed registries (no DB): a
        // slug has seeded text to reset to exactly when it has a seeded `en`
        // body (the four-surface set or a UG guide). The non-`en` baselines
        // are a superset check — if the `en` body is seeded, at least the `en`
        // reset is meaningful, so the `en` presence is the single gate.
        if (string.IsNullOrEmpty(slug)) return false;
        return FindSeededPageText(slug) is not null;
    }
    /// <summary>
    /// ADR 0128 — the **full set of seeded page slugs** in display order:
    /// the four-surface set (terms / help / privacy / conduct) followed by the
    /// UG guides (the <see cref="GuidePages"/> registry, in the order the
    /// seeder writes them on first boot). The <c>/admin/help</c> surface
    /// (ADR 0128) iterates this to list every page the operator can reset, and
    /// to detect which have newer shipped text. Pure registry read — no DB, no
    /// session. The set is a **closed superset** of <see cref="HasSeededText"/>:
    /// every slug here passes <c>HasSeededText == true</c>, but the reverse is
    /// not required (a slug is in <c>AllSeededSlugs</c> iff it has a seeded
    /// <c>en</c> baseline in <see cref="EnDefaultPages"/> or
    /// <see cref="GuidePages"/>).
    /// </summary>
    public static IReadOnlyList<string> AllSeededSlugs()
    {
        var slugs = new List<string>();
        foreach (var p in EnDefaultPages()) slugs.Add(p.Slug);
        foreach (var g in GuidePages()) slugs.Add(g.Slug);
        return slugs.AsReadOnly();
    }
    /// <summary>
    /// ADR 0058 — the **reset applier**: overwrites the <see cref="Page"/>
    /// <c>en</c> <see cref="Kumunita.Core.Pages.Page.Title"/> /
    /// <see cref="Kumunita.Core.Pages.Page.Body"/> and the <c>de</c> /
    /// <c>fr</c> / <c>da</c> <see cref="Kumunita.Core.Pages
    /// .PageTranslation"/> rows of <paramref name="page"/> with the seeded
    /// baseline text (matched on <c>page.Slug</c>), in the **caller's**
    /// in-flight <see cref="IDocumentSession"/> (the C3 invariant — the
    /// <see cref="Kumunita.Core.Pages.PageService
    /// .ResetToSeededAsync"/> lane commits it in its single
    /// <c>SaveChangesAsync</c> and owns the standing re-check + the
    /// <c>page.reset</c> <see cref="Kumunita.Core.Authorization.AccessAudit"/>
    /// row). <para>
    /// Mirrors the <see cref="SeedDefaultPagesAsync"/> /
    /// <see cref="SeedPageTranslationsAsync"/> content exactly, but is
    /// **destructive by design** (a code-wins overwrite of the
    /// <c>(PageId, LanguageCode)</c> rows rather than the seeder's
    /// create-if-missing): the reset is the operator's explicit choice to
    /// accept the seeded text, so a customized body is replaced (the lane's
    /// confirmation prompt is the guard). A page with no seeded
    /// <c>en</c> baseline (<see cref="FindSeededPageText"/> returns
    /// <c>null</c>) is a <see cref="InvalidOperationException"/> — the Web
    /// layer maps that to a re-rendered form error (there is nothing to
    /// reset to).
    /// </para>
    /// </summary>
    public static async Task ResetSeededTextAsync(
        IDocumentSession session, Page page, DateTimeOffset now, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(page);
        var slug = page.Slug;

        // The `en` body + title (the page itself).
        var seeded = FindSeededPageText(slug);
        if (seeded is null)
            throw new InvalidOperationException(
                $"The page '{slug}' has no seeded text to reset to " +
                "(it is not one of the seeded platform pages or guides).");

        page.Title = seeded.Value.Title;
        page.Body = seeded.Value.Body;
        page.LanguageCode = FirstBootSeeder.SourceLanguage;
        page.Modified = now;
        session.Store(page);

        // The non-`en` translations (de / fr / da) — a code-wins overwrite of
        // each (PageId, LanguageCode) row that has a seeded baseline. A
        // language the seeded set does not carry (e.g. a community-added
        // language the baseline does not translate into) is left untouched —
        // the reset only replaces what the seed knows.
        foreach (var (code, baselines) in new[]
        {
            ("de", DeDefaultPages()),
            ("fr", FrDefaultPages()),
            ("da", DaDefaultPages()),
            ("de", DeGuidePages()),
            ("fr", FrGuidePages()),
            ("da", DaGuidePages()),
        })
        {
            var baseline = baselines.FirstOrDefault(b => b.Slug == slug);
            if (baseline == default)
                continue;   // this language carries no baseline for this slug.

            var row = await session
                .Query<PageTranslation>()
                .Where(t => t.PageId == page.Id && t.LanguageCode == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (row is null)
            {
                session.Store(new PageTranslation
                {
                    Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                    PageId = page.Id,
                    LanguageCode = code,
                    Title = baseline.Title,
                    Body = baseline.Body,
                    AuthorId = string.Empty,   // platform content — no resident author
                    Created = now,
                });
            }
            else
            {
                // Destructive by design (ADR 0058): the operator asked to
                // reset, so the customized row is replaced with the seeded
                // baseline. The original text is preserved by the
                // page.translation.* / page.reset audit trail + the page's
                // own Modified stamp (the seeder's create-if-missing never
                // reaches here — this lane is the explicit override).
                row.Title = baseline.Title;
                row.Body = baseline.Body;
                session.Store(row);
            }
        }
    }
    /// <summary>
    /// ADR 0058 — the seeded <c>en</c> (title, body) registry for a single
    /// page slug: from <see cref="EnDefaultPages"/> (the four-surface set) or
    /// <see cref="GuidePages"/> (the UG guides). <c>null</c> when the slug is
    /// not one of the seeded pages (the reset lane maps that to a form error
    /// — there is nothing to reset to).
    /// </summary>
    private static (string Title, string Body)? FindSeededPageText(string slug)
    {
        var en = EnDefaultPages().FirstOrDefault(p => p.Slug == slug);
        if (en != default)
            return (en.Title, en.Body);

        var guide = GuidePages().FirstOrDefault(p => p.Slug == slug);
        if (guide != default)
            return (guide.Title, guide.Body);

        return null;
    }
    /// <summary>
    /// Warm-boot backfill of the <c>de</c> / <c>fr</c> / <c>da</c>
    /// <see cref="PageTranslation"/> rows for the four canonical seeded pages
    /// (terms / help / privacy / conduct) — the lane that closes the ADR 0043
    /// D7 / ADR 0044 D5 gap: a deployment seeded before the <c>de</c> /
    /// <c>fr</c> / <c>da</c> baselines shipped (LS U04, SP U02) has the four
    /// pages but no translation rows, so <c>/terms</c> / <c>/help</c> /
    /// <c>/privacy</c> / <c>/conduct</c> render the authored-in <c>en</c>
    /// body even for a German / French / Danish-speaking resident.
    /// <para>
    /// **Create-if-missing only** (the ADR 0042 D1 invariant, the same rule
    /// as <see cref="SeedPageTranslationsAsync"/>): an existing row for a
    /// (page, language) pair is skipped, never refreshed — an admin's in-app
    /// edit of a baseline (the GlobalAdmin ∪ Translator lane, ADR 0021) is
    /// never clobbered by a later deploy. The <c>en</c> page bodies are
    /// **never** touched here: unlike the first-boot path, this runs on a warm
    /// DB where an admin may have edited the canonical <c>en</c> body, so a
    /// code-wins refresh would clobber that edit. A deployment that has the
    /// pages but no translation rows is exactly the one this lane serves —
    /// the rows are created from the same <see cref="DeDefaultPages"/> /
    /// <see cref="FrDefaultPages"/> / <see cref="DaDefaultPages"/> baselines
    /// the first-boot seeder uses, so a fresh and a backfilled instance are
    /// byte-identical for these surfaces.
    /// </para>
    /// <para>
    /// Idempotent: a second run finds every row it created on the first run
    /// and skips (the ADR 0042 D1 skip path). The <c>system/{slug}</c>
    /// primary resolution mirrors the <c>StaticPagesController</c> read path
    /// (ADR 0040 — the seeder nests the canonical pages under the
    /// <c>system</c> root); the bare-<c>{slug}</c> fallback covers a
    /// pre-ADR-0040 deployment whose pages are still orphan roots (the
    /// seeder's ADR 0040 lane re-parents them on the next first-boot
    /// re-seed, but a warm boot does not re-run that lane — the backfill
    /// finds them where they are).
    /// </para>
    /// </summary>
    public static async Task BackfillPageTranslationsAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        // The slugs that have a de/fr/da baseline in the seed data — the
        // same set EnDefaultPages() covers (terms / help / privacy /
        // conduct; `about` is deliberately absent — it is the
        // registry-key surface, not a Markdown body).
        var slugs = EnDefaultPages().Select(p => p.Slug).ToList();

        // The system root (ADR 0040) — null when the instance predates ADR
        // 0040 and the pages are still orphan roots (the fallback below).
        var systemRoot = await session
            .Query<Page>()
            .Where(p => p.Slug == "system" && p.ParentId == null && p.IsDeleted == false)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        foreach (var slug in slugs)
        {
            // Primary resolution: system/{slug} (ADR 0040 — the seeder
            // nests the canonical pages under the system root). The fallback
            // below is the pre-ADR-0040 orphan: a deployment whose pages are
            // still root-level, which the ADR 0040 re-parent lane has not
            // touched on this warm boot.
            Page? page = null;
            if (systemRoot is not null)
            {
                page = await session
                    .Query<Page>()
                    .Where(p => p.Slug == slug
                                && p.ParentId == systemRoot.Id
                                && p.IsDeleted == false)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
            }
            if (page is null)
            {
                page = await session
                    .Query<Page>()
                    .Where(p => p.Slug == slug && p.ParentId == null && p.IsDeleted == false)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);
            }

            if (page is null)
                continue;   // the page itself is absent; the first-boot path (or the ADR 0040 re-parent lane) owns that gap.

            // The de/fr/da baselines for this page, create-if-missing (ADR 0042 D1).
            foreach (var (code, baselines) in new[]
            {
                ("de", DeDefaultPages()),
                ("fr", FrDefaultPages()),
                ("da", DaDefaultPages()),
            })
            {
                var baseline = baselines.FirstOrDefault(b => b.Slug == slug);
                if (baseline == default)
                    continue;   // defensive — the slug is not in the baseline set (about, or a future slug).

                var existing = await session
                    .Query<PageTranslation>()
                    .Where(t => t.PageId == page.Id && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new PageTranslation
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        PageId = page.Id,
                        LanguageCode = code,
                        Title = baseline.Title,
                        Body = baseline.Body,
                        AuthorId = string.Empty,   // platform content — no resident author
                        Created = now,
                    });
                }
                // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
    /// <summary>
    /// Warm-boot backfill of the <c>de</c> / <c>fr</c> / <c>da</c>
    /// <see cref="TranslationResource"/> baseline rows for the closed
    /// <see cref="Localization.KnownTranslationKeys"/> registry — the lane that
    /// closes the ADR 0042 D1 / ADR 0047 D2 "new-key asymmetry" for the
    /// UI-string surface (ADR 0052). A deployment whose first boot predates a
    /// baseline added to the registry in a later code release has the
    /// <c>en</c> row (and, for new keys, the provider floor renders its
    /// English) but no <c>de</c> / <c>fr</c> / <c>da</c> row, so a German /
    /// French / Danish-speaking resident sees the English floor for that string
    /// on every boot — the same visible seam ADR 0047 D2 closed for the four
    /// canonical pages' <see cref="Pages.PageTranslation"/> rows.
    /// <para>
    /// **Create-if-missing only** (the ADR 0042 D1 invariant, the same rule as
    /// <see cref="BackfillPageTranslationsAsync"/>): an existing row for a
    /// (key, language) pair is skipped, never refreshed — an admin's in-app
    /// edit of a baseline (the GlobalAdmin ∪ Translator lane, ADR 0021; the
    /// localization editor updates the row in place, so its <c>Id</c> persists
    /// and the skip path finds it) is never clobbered by a later deploy.
    /// </para>
    /// <para>
    /// **The <c>en</c> row is never read or written here.** It is the floor,
    /// owned by code (the ADR 0042 D1 code-wins upsert lives on the
    /// pristine-boot path only), and a new key's English already renders on a
    /// warm instance via the provider floor (ADR 0015) — the only gap this
    /// lane serves is the missing non-<c>en</c> baseline rows.
    /// </para>
    /// <para>
    /// Idempotent: a second run finds every row it created on the first run
    /// and skips (the ADR 0042 D1 skip path). No tombstones, no deletes —
    /// create-if-missing is what makes the re-run a no-op.
    /// </para>
    /// </summary>
    public static async Task BackfillUiStringBaselinesAsync(
        IDocumentSession session,
        CancellationToken ct)
    {
        foreach (var (code, baseline) in new[]
        {
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        })
        {
            foreach (var (key, text) in baseline)
            {
                var existing = await session
                    .Query<TranslationResource>()
                    .Where(t => t.Key == key && t.LanguageCode == code)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (existing is null)
                {
                    session.Store(new TranslationResource
                    {
                        Id = Guid.NewGuid().ToString("N"),   // surrogate (the pair idiom)
                        Key = key,
                        LanguageCode = code,
                        Text = text
                    });
                }
                // else: skip — create-if-missing (never overwrite; ADR 0042 D1).
            }
        }

        await session.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
