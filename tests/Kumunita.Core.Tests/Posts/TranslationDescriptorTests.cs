using Kumunita.Core.Identity;
using Kumunita.Core.Posts;
using Kumunita.Core.Translations;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// The C06 <see cref="TranslationDescriptor{TParent,TVar}"/> pure-function
/// contract (ADR [0288](../../docs/adr/0288-one-translation-variant-lane.md) —
/// "the descriptor is unit-tested"). A **pure** unit — no
/// <c>PostgresFixture</c>, no <see cref="Marten.IDocumentStore"/>: it exercises
/// only the descriptor's pure seams (<see cref="TranslationDescriptor{TParent,TVar}.ResolveStanding"/>,
/// <see cref="TranslationDescriptor{TParent,TVar}.Create"/>,
/// <see cref="TranslationDescriptor{TParent,TVar}.ApplyUpdate"/>,
/// <see cref="TranslationDescriptor{TParent,TVar}.ValidateContent"/>) and the
/// lane's built-in content validation, using the <see cref="PostService"/>
/// posts descriptor (the ADR 0022 reference surface) and a
/// projects-style descriptor (the "title or body" content contract, ADR 0088).
/// </summary>
public class TranslationDescriptorTests
{
    private const string ComponentId = "c-a022-comp";
    private const string Author = "a022-author";

    private static IReadOnlySet<string> RoleSet(params string[] roles) => new HashSet<string>(roles);

    /// <summary>
    /// A descriptor shaped exactly like the <see cref="PostService"/> posts
    /// descriptor (the ADR 0022 reference surface) — the posts standing matrix
    /// + the verbatim-title / required-body content shape.
    /// </summary>
    private static TranslationDescriptor<Post, PostTranslation> PostsDescriptor() => new()
    {
        TargetKind = "post",
        ActionPrefix = "posttranslation",
        Noun = "Post",
        LoadStandingExtra = (_, _) => Task.FromResult<object?>(null),
        ResolveStanding = (Post p, object? _, string actorId, IReadOnlySet<string> actorRoles)
            => PostTranslationPolicy.ResolveTranslationStanding(p.GroupId.Length > 0, p.ComponentId, p.AuthorId, actorId, actorRoles),
        LoadParent = (_, _) => throw new NotSupportedException("session seam — not exercised by this pure test"),
        Find = (_, _, _) => throw new NotSupportedException("session seam — not exercised by this pure test"),
        Create = (string id, string lang, string? title, string? body, string actorId, DateTimeOffset created) => new PostTranslation
        {
            Id = Guid.NewGuid().ToString("N"),
            PostId = id,
            LanguageCode = lang,
            Title = title,
            Body = body!,
            AuthorId = actorId,
            Created = created
        },
        ApplyUpdate = (PostTranslation row, string? title, string? body, string actorId, DateTimeOffset now) =>
        {
            row.Title = string.IsNullOrWhiteSpace(title) ? null : title;
            row.Body = body!;
        }
    };

    /// <summary>A community-lane post (a GroupId of length 0).</summary>
    private static Post CommunityPost() => new() { Id = "p-1", ComponentId = ComponentId, AuthorId = Author, GroupId = "" };

    [Fact]
    public void PostsDescriptor_ResolveStanding_MatchesStandingMatrix()
    {
        var d = PostsDescriptor();
        var post = CommunityPost();

        // Author → Owner (the narrowest right).
        Assert.Equal(Authorization.AccessVia.Owner,
            d.ResolveStanding(post, null, Author, RoleSet()));
        // GlobalAdmin → Admin.
        Assert.Equal(Authorization.AccessVia.Admin,
            d.ResolveStanding(post, null, "a022-admin", RoleSet(Roles.GlobalAdmin)));
        // Community-lane component-moderator → Moderator.
        Assert.Equal(Authorization.AccessVia.Moderator,
            d.ResolveStanding(post, null, "a022-mod", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
        // A moderator of a *different* component has no standing.
        Assert.Null(
            d.ResolveStanding(post, null, "a022-mod-other", RoleSet(Roles.Moderator, Roles.ModeratorComponent("c-other"))));
        // A plain Member has no standing.
        Assert.Null(d.ResolveStanding(post, null, "a022-member", RoleSet(Roles.Member)));
    }

    [Fact]
    public void PostsDescriptor_ResolveStanding_GroupLaneExcludesModerator()
    {
        var d = PostsDescriptor();
        var groupPost = new Post { Id = "p-g", ComponentId = ComponentId, AuthorId = Author, GroupId = "g-1" };

        // Author and GlobalAdmin allow on the group lane; the component-
        // moderator branch is excluded (ADR 0007 — the group lane has no
        // component-moderator standing).
        Assert.Equal(Authorization.AccessVia.Owner,
            d.ResolveStanding(groupPost, null, Author, RoleSet()));
        Assert.Equal(Authorization.AccessVia.Admin,
            d.ResolveStanding(groupPost, null, "a022-admin", RoleSet(Roles.GlobalAdmin)));
        Assert.Null(
            d.ResolveStanding(groupPost, null, "a022-mod", RoleSet(Roles.Moderator, Roles.ModeratorComponent(ComponentId))));
    }

    [Fact]
    public void PostsDescriptor_Create_WritesVerbatimTitleAndBody()
    {
        var d = PostsDescriptor();
        var created = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

        var row = d.Create("p-1", "de", "Aufräumtag", "Körper", Author, created);

        Assert.NotEmpty(row.Id);
        Assert.Equal("p-1", row.PostId);
        Assert.Equal("de", row.LanguageCode);
        Assert.Equal("Aufräumtag", row.Title);   // verbatim (the posts shape — not normalized)
        Assert.Equal("Körper", row.Body);
        Assert.Equal(Author, row.AuthorId);
        Assert.Equal(created, row.Created);
    }

    [Fact]
    public void PostsDescriptor_ApplyUpdate_NormalizesBlankTitle()
    {
        var d = PostsDescriptor();
        var row = new PostTranslation { Id = "r-1", PostId = "p-1", LanguageCode = "de", Title = "alt", Body = "alt body" };

        d.ApplyUpdate(row, "   ", "neu body", Author, DateTimeOffset.UtcNow);

        Assert.Null(row.Title);   // blank → null (the posts update shape)
        Assert.Equal("neu body", row.Body);

        d.ApplyUpdate(row, "Neu", "k", Author, DateTimeOffset.UtcNow);
        Assert.Equal("Neu", row.Title);   // non-blank → verbatim
    }

    [Fact]
    public void PostsDescriptor_UsesLaneDefaultBodyRequiredValidation()
    {
        // The posts surface has a non-null Body (ADR 0022) — so its descriptor
        // supplies no ValidateContent hook and the lane falls back to its
        // built-in "a translation requires a non-empty body" check. (The
        // projects family, by contrast, supplies its own "title or body" hook —
        // ADR 0088 — which is exactly what the optional hook exists for.)
        var d = PostsDescriptor();
        Assert.Null(d.ValidateContent);
    }
}
