using System.Security.Claims;
using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Pages;
using Kumunita.Core.Posts;
using Kumunita.Core.Tags;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Kumunita.Web.Security;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Roles = Kumunita.Core.Identity.Roles;
using KumunitaClaimTypes = Kumunita.Core.Identity.ClaimTypes;

namespace Kumunita.Web.Tests;

/// <summary>
/// Unit tests for <see cref="TagController"/> — the <c>TG</c> lane's Web
/// surface (ADR 0044 D5; the U8c reword lane <c>POST /tags/{slug}/translate</c>).
/// The pins this harness owns:
/// <list type="number">
/// <item><b>C-TG·5 standing split (404 vs 403)</b>: <see
///       cref="TagController.Translate"/> on a tag the actor can read but
///       does not own (a non-creator attacher, not a GlobalAdmin) is a
///       <see cref="ForbidResult"/> (403) — the tag exists and is readable,
///       the standing decision ran and denied; a missing or unreadable tag
///       is a <see cref="NotFoundResult"/> (404) — the C-TG·1 floor (a 403
///       would confirm the tag exists). The two are distinct HTTP semantics
///       (the M3 403-vs-404 split pin, the
///       <see cref="PostsController"/> precedent).</item>
/// <item><b>C-TG·9 one audit row per write</b>: a successful
///       <see cref="TagController.Translate"/> calls
///       <see cref="ITagService.AddTagTranslationAsync"/> exactly once, in
///       the caller's <c>IDocumentSession</c> (the C3 single-transaction
///       idiom — the controller owns the session; the service's
///       <c>SaveChangesAsync</c> is the single write). The
///       <c>tagtranslation.add</c> audit row is the service's (not the
///       controller's); this harness pins the call count, the argument
///       shape, and the redirect.</item>
/// <item><b>404-floor precondition</b>: <see cref="TagController
///       .Translate"/> re-runs the read seam
///       (<see cref="ITagService.ListForActorAsync"/>) before the write — a
///       tag absent from the actor's readable set (unknown slug, or used
///       only on unread content) is a 404 regardless of standing (C-TG·1:
///       the tag grants nothing; its visibility is derived from the content
///       the actor may already read).</item>
/// <item><b>Blank-field shape</b>: <see cref="TagController
///       .Translate"/> on a blank <c>languageCode</c> or <c>name</c> is a
///       redirect back to the by-tag view with a <c>TempData["error"]</c>
///       message — not a 404, not a 403, not a 500 (the form-validation
///       shape, the <see cref="PostsController.AddTranslation"/> precedent).
///       The service's <c>AddTagTranslationAsync</c> is never called (the
///       blank-field check is a Web-layer pin, not a service
///       responsibility).</item>
/// <item><b>U8c reword surface display (C-TG·5)</b>: <see
///       cref="TagController.ByTag"/> seeds
///       <see cref="TagByTagViewModel.Translation"/> with
///       <see cref="TagTranslationForm.CanTranslate"/> = <c>true</c> for
///       the tag's <c>CreatedBy</c> (creator) and for a GlobalAdmin, and
///       <c>false</c> for a non-creator attacher (the form renders disabled
///       — the display affordance pin; the real deny is the POST lane's
///       standing re-check, C3).</item>
/// </list>
/// <para>
/// The harness mirrors <see cref="PageControllerTests"/>: a NSubstitute
/// <see cref="ITagService"/> + <see cref="IPageService"/> +
/// <see cref="ILocalizationService"/>. A <see cref="KumunitaPrincipal"/>-shaped
/// principal is built with the
/// <see cref="KumunitaClaimTypes.Subject"/> + <see cref="KumunitaClaimTypes.Role"/>
/// claims (the repo's role-claim convention).
/// <para>
/// <b>ByTag read path (the 3 form-rendering tests):</b> a real
/// <see cref="IDocumentStore"/> over a fresh scratch Postgres
/// (<see cref="PostgresFixture"/>), per the <see cref="GuardianAssignmentTests"/>
/// precedent — Marten 9 is async-only for data access, so the controller's
/// <c>Query&lt;TagTranslation&gt;().Where(…).ToListAsync()</c> cast to the
/// internal <c>MartenLinqQueryable</c> and execute a live query (not
/// NSubstitutable). The 5 <c>Translate</c> tests still use a NSubstitute
/// <see cref="IDocumentStore"/> (they only call <c>LightweightSession()</c> —
/// no LINQ read).
/// </para>
/// </summary>
public sealed class TagControllerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── U8c reword lane — C-TG·5 standing split (404 vs 403) ─────────────

    /// <summary>
    /// <see cref="TagController.Translate"/> by the tag's creator
    /// (non-GlobalAdmin): the service's <c>AddTagTranslationAsync</c> is
    /// called once with the exact (tagId, languageCode, name, actorId,
    /// roles, session) shape; the response is a <see cref="RedirectResult"/>
    /// back to <c>/tags/{slug}</c> (the C-TG·9 one-audit-row write — the
    /// audit row itself is the service's, this pins the call count + the
    /// redirect shape).
    /// </summary>
    [Fact]
    public async Task Translate_When_Creator_PostsDeName_CallsServiceOnceAndRedirects()
    {
        const string slug = "sanitation";
        const string tagId = "tag-001";
        const string actorId = "creator-001";
        var roles = RolesSet(Roles.Member);

        var tags = Substitute.For<ITagService>();
        var tag = new Tag
        {
            Id = tagId,
            Slug = slug,
            Name = "Sanitation",
            LanguageCode = "en",
            CreatedBy = actorId,
            Created = DateTimeOffset.UtcNow,
        };
        tags.ListForActorAsync(actorId)
            .Returns(new List<TagItem> { new(tag, 3, "Sanitation") });
        tags.AddTagTranslationAsync(tagId, "de", "Reinigung", actorId, roles, Arg.Any<IDocumentSession>())
            .Returns(new TagTranslation
            {
                Id = "tr-001",
                TagId = tagId,
                LanguageCode = "de",
                Name = "Reinigung",
                AuthorId = actorId,
                Created = DateTimeOffset.UtcNow,
            });

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(tags, store: store, actorId: actorId, roles: roles);

        var result = await controller.Translate(slug, "de", "Reinigung");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/tags/{slug}", redirect.Url);
        // The write lane ran once, in the caller's session (the C3 single-
        // transaction idiom), with the (tag, de, name, actor) the controller
        // received. roles is matched loosely (the pin is the call shape + the
        // session identity, not the set's object identity).
        await tags.Received(1).AddTagTranslationAsync(
            tagId, "de", "Reinigung", actorId, Arg.Any<IReadOnlySet<string>>(), session);
    }

    /// <summary>
    /// <see cref="TagController.Translate"/> by a **non-creator attacher**
    /// (Member, not the tag's <c>CreatedBy</c>, not a GlobalAdmin): the
    /// service's <c>AddTagTranslationAsync</c> throws
    /// <see cref="UnauthorizedAccessException"/> (the C-TG·5 standing
    /// re-check) → a <see cref="ForbidResult"/> (403). The 403-vs-404 split:
    /// the tag exists and is readable (the 404-floor precondition passed —
    /// the tag is in the actor's readable set); the standing decision ran
    /// and denied. A 404 here would be wrong (the tag is known to this
    /// actor — it's readable; 403 is the correct semantic).
    /// </summary>
    [Fact]
    public async Task Translate_When_NonCreator_Posts_ReturnsForbid()
    {
        const string slug = "sanitation";
        const string actorId = "attacher-002";
        var roles = RolesSet(Roles.Member);

        var tags = Substitute.For<ITagService>();
        var tag = new Tag
        {
            Id = "tag-001",
            Slug = slug,
            Name = "Sanitation",
            LanguageCode = "en",
            CreatedBy = "creator-001",
            Created = DateTimeOffset.UtcNow,
        };
        tags.ListForActorAsync(actorId)
            .Returns(new List<TagItem> { new(tag, 3, "Sanitation") });
        tags.AddTagTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>())
            .Returns(Task.FromException<TagTranslation>(new UnauthorizedAccessException()));

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(tags, actorId: actorId, roles: roles);

        var result = await controller.Translate(slug, "de", "Müllabfuhr");

        Assert.IsType<ForbidResult>(result);
    }

    /// <summary>
    /// <see cref="TagController.Translate"/> by a **GlobalAdmin** (who is
    /// not the tag's <c>CreatedBy</c>): the service's
    /// <c>AddTagTranslationAsync</c> succeeds (the GlobalAdmin standing,
    /// C-TG·5) → a <see cref="RedirectResult"/> back to
    /// <c>/tags/{slug}</c>. The pin: the GlobalAdmin's break-glass standing
    /// is honored through the service (the controller does not re-derive
    /// standing itself — the C-TG·5 re-check is the service's).
    /// </summary>
    [Fact]
    public async Task Translate_When_GlobalAdmin_Posts_ReturnsRedirect()
    {
        const string slug = "sanitation";
        const string tagId = "tag-001";
        const string actorId = "admin-001";
        var roles = RolesSet(Roles.GlobalAdmin);

        var tags = Substitute.For<ITagService>();
        var tag = new Tag
        {
            Id = tagId,
            Slug = slug,
            Name = "Sanitation",
            LanguageCode = "en",
            CreatedBy = "creator-001",
            Created = DateTimeOffset.UtcNow,
        };
        tags.ListForActorAsync(actorId)
            .Returns(new List<TagItem> { new(tag, 3, "Sanitation") });
        tags.AddTagTranslationAsync(tagId, "fr", "Assainissement", actorId, roles, Arg.Any<IDocumentSession>())
            .Returns(new TagTranslation
            {
                Id = "tr-002",
                TagId = tagId,
                LanguageCode = "fr",
                Name = "Assainissement",
                AuthorId = actorId,
                Created = DateTimeOffset.UtcNow,
            });

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(tags, actorId: actorId, roles: roles);

        var result = await controller.Translate(slug, "fr", "Assainissement");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/tags/{slug}", redirect.Url);
        // roles matched loosely (the pin is the call shape, not set identity).
        await tags.Received(1).AddTagTranslationAsync(
            tagId, "fr", "Assainissement", actorId, Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── U8c reword lane — C-TG·1 404-floor precondition ────────────────────

    /// <summary>
    /// <see cref="TagController.Translate"/> on a slug that is **absent
    /// from the actor's readable set** (unknown slug, or used only on unread
    /// content — the two are indistinguishable, C-TG·1 / C-TG·2): the
    /// 404-floor precondition (the read seam check) returns a
    /// <see cref="NotFoundResult"/> BEFORE the service's write lane is
    /// called. The pin: the controller re-runs
    /// <see cref="ITagService.ListForActorAsync"/> (the same read seam the
    /// <c>ByTag</c> action uses) as a precondition for the write lane — a
    /// missing or unreadable tag is a 404 regardless of standing (the tag
    /// grants nothing; its visibility is derived from the content the actor
    /// may already read, C-TG·1).
    /// </summary>
    [Fact]
    public async Task Translate_When_TagUnreadable_ReturnsNotFound()
    {
        const string slug = "secret-tag";
        const string actorId = "viewer-001";
        var roles = RolesSet(Roles.GlobalAdmin);

        var tags = Substitute.For<ITagService>();
        // The tag is NOT in the actor's readable set (used only on unread
        // content, or unknown — the two are indistinguishable, C-TG·1).
        tags.ListForActorAsync(actorId).Returns(new List<TagItem>());

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(tags, actorId: actorId, roles: roles);

        var result = await controller.Translate(slug, "de", "Geheimer Tag");

        Assert.IsType<NotFoundResult>(result);
        // The write lane was NEVER called (the 404-floor precondition
        // short-circuits before the write).
        await tags.DidNotReceive().AddTagTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── U8c reword lane — blank-field shape ─────────────────────────────────

    /// <summary>
    /// <see cref="TagController.Translate"/> with a blank
    /// <paramref name="languageCode"/> is a redirect back to the by-tag
    /// view with a <c>TempData["error"]</c> message — not a 404, not a 403,
    /// not a 500. The pin: the blank-field check is a Web-layer
    /// validation (the <see cref="PostsController.AddTranslation"/>
    /// precedent); the service's <c>AddTagTranslationAsync</c> is never
    /// called (the blank-field check is a Web-layer pin, not a service
    /// responsibility).
    /// </summary>
    [Fact]
    public async Task Translate_When_LanguageCodeBlank_ReturnsRedirectWithError()
    {
        const string slug = "sanitation";
        const string actorId = "creator-001";
        var roles = RolesSet(Roles.Member);

        var tags = Substitute.For<ITagService>();
        tags.ListForActorAsync(actorId).Returns(new List<TagItem>());

        var store = Substitute.For<IDocumentStore>();
        store.LightweightSession().Returns(Substitute.For<IDocumentSession>());

        var controller = Build(tags, actorId: actorId, roles: roles);

        var result = await controller.Translate(slug, "", "Reinigung");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal($"/tags/{slug}", redirect.Url);
        // The service's write lane was NEVER called (the blank-field check
        // short-circuits before the read seam check).
        await tags.DidNotReceive().AddTagTranslationAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>(), Arg.Any<IDocumentSession>());
    }

    // ── U8c reword surface — C-TG·5 display affordance on ByTag ─────────────

    /// <summary>
    /// <see cref="TagController.ByTag"/> seeds
    /// <see cref="TagByTagViewModel.Translation"/> with
    /// <see cref="TagTranslationForm.CanTranslate"/> = <c>true</c> when the
    /// actor is the tag's <c>CreatedBy</c> (creator) — the C-TG·5 standing
    /// probe is wired through to the form (the display affordance pin).
    /// The pin: the form's <c>CanTranslate</c> reflects the
    /// <see cref="ITagService.CanTranslateTag"/> probe result verbatim
    /// (the controller does not re-derive standing itself).
    /// </summary>
    [Fact]
    public async Task ByTag_When_Creator_SeesTranslationFormEnabled()
    {
        const string slug = "sanitation";
        const string actorId = "creator-001";
        var roles = RolesSet(Roles.Member);

        var tags = BuildTags(slug, actorId, createdById: actorId, canTranslate: true);
        var pages = BuildPages();
        // Real scratch-Postgres store (Marten 9 async-only — see
        // GuardianAssignmentTests precedent). No translation rows seeded:
        // the form is empty (all rows fall back to the base name).
        var store = await BuildRealStoreAsync(fixture, new List<TagTranslation>());

        var controller = Build(tags, pages, store, actorId: actorId, roles: roles);

        var result = await controller.ByTag(slug);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TagByTagViewModel>(viewResult.Model);
        Assert.NotNull(model.Translation);
        var form = model.Translation;
        Assert.True(form.CanTranslate);
        Assert.Equal(slug, form.Slug);
        Assert.Equal("Sanitation", form.BaseName);
    }

    /// <summary>
    /// <see cref="TagController.ByTag"/> seeds
    /// <see cref="TagByTagViewModel.Translation"/> with
    /// <see cref="TagTranslationForm.CanTranslate"/> = <c>false</c> when
    /// the actor is a **non-creator attacher** (Member, not the tag's
    /// <c>CreatedBy</c>, not a GlobalAdmin) — the C-TG·5 standing probe is
    /// <c>false</c>, the form renders **disabled** (the display affordance
    /// pin; the real deny is the POST lane's standing re-check, C3). The
    /// pin: the form's <c>CanTranslate</c> reflects the
    /// <see cref="ITagService.CanTranslateTag"/> probe result verbatim.
    /// </summary>
    [Fact]
    public async Task ByTag_When_NonCreator_SeesTranslationFormDisabled()
    {
        const string slug = "sanitation";
        const string actorId = "attacher-002";
        var roles = RolesSet(Roles.Member);

        var tags = BuildTags(slug, actorId, createdById: "creator-001", canTranslate: false);
        var pages = BuildPages();
        // Real scratch-Postgres store (no rows — CanTranslate is the pin).
        var store = await BuildRealStoreAsync(fixture, new List<TagTranslation>());

        var controller = Build(tags, pages, store, actorId: actorId, roles: roles);

        var result = await controller.ByTag(slug);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TagByTagViewModel>(viewResult.Model);
        Assert.NotNull(model.Translation);
        var form = model.Translation;
        Assert.False(form.CanTranslate);
    }

    /// <summary>
    /// <see cref="TagController.ByTag"/> seeds
    /// <see cref="TagTranslationForm"/> with one row per enabled
    /// <see cref="LanguageCatalog"/> (the ADR 0005 B shape — the instance
    /// catalog), each row's <see cref="TagTranslationRow.CurrentName"/>
    /// pre-filled with the current <see cref="TagTranslation.Name"/> for
    /// that language when a translation row exists, otherwise the tag's
    /// base <see cref="Tag.Name"/> (the ADR 0005 preference-order
    /// fallback). The pin: the form's rows reflect the actual
    /// <see cref="TagTranslation"/> rows in the store (the read seam's
    /// projection, not a re-derivation).
    /// </summary>
    [Fact]
    public async Task ByTag_When_TranslationExists_SeesPreFilledRow()
    {
        const string slug = "sanitation";
        const string actorId = "creator-001";
        var roles = RolesSet(Roles.Member);

        var tags = BuildTags(slug, actorId, createdById: actorId, canTranslate: true);

        // The store has an existing de translation row.
        var existingDe = new TagTranslation
        {
            Id = "tr-001",
            TagId = "tag-001",
            LanguageCode = "de",
            Name = "Reinigung",
            AuthorId = actorId,
            Created = DateTimeOffset.UtcNow,
        };
        // Real scratch-Postgres store seeded with the existing de row.
        var store = await BuildRealStoreAsync(fixture, new List<TagTranslation> { existingDe });
        var pages = BuildPages();

        var controller = Build(tags, pages, store, actorId: actorId, roles: roles);

        var result = await controller.ByTag(slug);

        var viewResult = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<TagByTagViewModel>(viewResult.Model);
        Assert.NotNull(model.Translation);
        var form = model.Translation;
        Assert.True(form.CanTranslate);
        // The de row is pre-filled with the existing translation.
        var deRow = form.Rows.Single(r => r.LanguageCode == "de");
        Assert.Equal("Reinigung", deRow.CurrentName);
        // The en row (no translation) falls back to the base name.
        var enRow = form.Rows.Single(r => r.LanguageCode == "en");
        Assert.Equal("Sanitation", enRow.CurrentName);
    }

    // ── harness ──────────────────────────────────────────────────────────

    private static IReadOnlySet<string> RolesSet(params string[] roles) =>
        new HashSet<string>(roles, StringComparer.Ordinal);

    /// <summary>
    /// Builds an <see cref="ITagService"/> substitute wired for a readable
    /// tag (the by-tag read lane returns one post; the list lane returns
    /// the tag; the standing probe returns <paramref name="canTranslate"/>).
    /// </summary>
    private static ITagService BuildTags(
        string slug, string actorId, string createdById, bool canTranslate)
    {
        var tags = Substitute.For<ITagService>();
        var tag = new Tag
        {
            Id = "tag-001",
            Slug = slug,
            Name = "Sanitation",
            LanguageCode = "en",
            CreatedBy = createdById,
            Created = DateTimeOffset.UtcNow,
        };
        var post = new Post
        {
            Id = "post-001",
            Title = "About sanitation",
            Body = "The sanitation schedule for March.",
            AuthorId = createdById,
            Created = DateTimeOffset.UtcNow,
        };
        tags.ListForActorAsync(actorId)
            .Returns(new List<TagItem> { new(tag, 1, "Sanitation") });
        // M7 (ADR 0090 D6) — the controller reads the paged seams (the
        // non-paged ListPostsByTagAsync / ListPagesByTagAsync were the
        // pre-M7 shape; the app calls the paged pair). One post, one page,
        // HasMore false (the by-tag single-page pin).
        tags.ListPostsByTagPagedAsync(slug, actorId, 1, Arg.Any<CancellationToken>())
            .Returns(new TagPostPage(Items: new List<Post> { post }, HasMore: false));
        tags.ListPagesByTagPagedAsync(slug, actorId, 1, Arg.Any<CancellationToken>())
            .Returns(new TagPagePage(Items: new List<Page>(), HasMore: false));
        tags.CanTranslateTag(Arg.Any<Tag>(), Arg.Any<string>(), Arg.Any<IReadOnlySet<string>>())
            .Returns(canTranslate);
        return tags;
    }

    private static IPageService BuildPages()
    {
        var pages = Substitute.For<IPageService>();
        pages.GetTreeAsync().Returns(new List<Page>());
        return pages;
    }

    private static ILocalizationService DefaultLocalization()
    {
        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>
        {
            new() { Id = "en", NativeName = "English", Enabled = true, SortOrder = 1 },
            new() { Id = "de", NativeName = "Deutsch", Enabled = true, SortOrder = 2 },
            new() { Id = "fr", NativeName = "Français", Enabled = true, SortOrder = 3 },
        });
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        return localization;
    }

    /// <summary>
    /// Builds a real Marten <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres database (the <see cref="GuardianAssignmentTests"/> precedent:
    /// Marten 9's <c>ToListAsync()</c> casts to the internal
    /// <c>MartenLinqQueryable</c> and executes a live query, so it cannot be
    /// NSubstituted — a real store is the only faithful backing for the
    /// <see cref="TagController.ByTag"/> read path's
    /// <c>Query&lt;TagTranslation&gt;().Where(…).ToListAsync()</c>).
    /// Seeds the given <see cref="TagTranslation"/> rows so the controller's
    /// LINQ read returns exactly those.
    /// </summary>
    private static async Task<IDocumentStore> BuildRealStoreAsync(
        PostgresFixture fixture, List<TagTranslation> translations)
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);

        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            TagDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);

        if (translations.Count > 0)
        {
            await using var session = store.OpenSession(new Marten.Services.SessionOptions());
            foreach (var t in translations)
                session.Store(t);
            await session.SaveChangesAsync(ct);
        }

        return store;
    }

    private static TagController Build(
        ITagService tags,
        IPageService? pages = null,
        IDocumentStore? store = null,
        IReadOnlySet<string>? roles = null,
        bool IsAuthenticated = false,
        string? actorId = null)
    {
        var pagesImpl = pages ?? Substitute.For<IPageService>();
        if (pages is null)
        {
            pagesImpl.GetTreeAsync().Returns(new List<Page>());
        }

        // The 5 Translate tests use this branch (they pass store: null).
        // Translate only calls store.LightweightSession() (the C3 write-
        // session) — it never calls QuerySession() (no LINQ read), so the
        // stub is sufficient without any IMartenQueryable wiring. The 3 ByTag
        // tests pass a real scratch-Postgres store (GuardianAssignmentTests
        // precedent: Marten 9's ToListAsync() cannot be NSubstituted).
        var storeImpl = store ?? Substitute.For<IDocumentStore>();
        if (store is null)
        {
            storeImpl.LightweightSession().Returns(Substitute.For<IDocumentSession>());
        }

        var localization = DefaultLocalization();

        var controller = new TagController(tags, pagesImpl, localization, storeImpl);
        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        if (IsAuthenticated || (roles is { Count: > 0 }))
        {
            var claims = new List<Claim>();
            if (actorId is not null)
                claims.Add(new Claim(KumunitaClaimTypes.Subject, actorId));
            if (roles is { Count: > 0 })
                claims.AddRange(roles.Select(r => new Claim(KumunitaClaimTypes.Role, r)));

            controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(claims, authenticationType: "test"));
        }

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }
}
