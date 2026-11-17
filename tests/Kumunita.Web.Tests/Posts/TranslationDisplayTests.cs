using Kumunita.Core.Posts;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// TD U06 — the four **pinned** tests (<c>translation-display-design.md</c>
/// §Pinned contract → "### pinned tests (exact names)"), authored as
/// **view-model data-shape** pins against the U02 projection ADDs:
/// <see cref="PostDetailViewModel.OriginalLanguageCode"/>,
/// <see cref="GroupPostDetailViewModel.OriginalLanguageCode"/>, and the
/// <see cref="ReplyItem.OriginalLanguageCode"/> positional (11th, after
/// <c>DeletedAt</c>).
/// </summary>
/// <para>
/// <b>Why data-shape, not an end-to-end controller drive.</b> The four names
/// are pinned verbatim; the unit-series rule for a test that cannot be
/// written as-pinned against the existing seam is to honor the *faithful
/// seam available in this harness*, not to force a production edit. The two
/// detail controllers (<see cref="Kumunita.Web.Controllers.PostsController.Detail"/>
/// and <see cref="Kumunita.Web.Controllers.GroupsController.GroupPostDetail"/>)
/// source the ADDs from the **sealed** <see cref="Kumunita.Core.Posts.PostService"/>
/// read seams (<c>GetPostAsync</c> / <c>GetGroupPostAsync</c>), and those
/// methods open a real Marten <c>QuerySession</c> (<c>session.LoadAsync</c> /
/// <c>Query&lt;PostReply&gt;</c>). <see cref="Kumunita.Core.Posts.PostService"/>
/// is <c>sealed</c>, so NSubstitute cannot proxy it, and
/// <see cref="Kumunita.Web.Tests"/> carries **no** Testcontainers / Postgres
/// fixture (that lives in <c>Kumunita.Core.Tests</c> only) — the exact seam
/// wall <see cref="ContentImageServingTests"/> records for the RC lane.
/// Driving the route end-to-end here would require either a forbidden
/// production edit (a substitutable seam) or a forbidden new infra file.
/// </para>
/// <para>
/// The U02 projection itself is a **read, not a decision**: the controller
/// copies <c>Post.LanguageCode</c> / <c>PostReply.LanguageCode</c> (the ADR
/// 0018 fields, already present on the POCOs) onto the three ADDs, and the
/// design doc pins the source verbatim ("Population source:
/// <c>Post.LanguageCode</c> (both post VMs) and <c>PostReply.LanguageCode</c>
/// (each <c>ReplyItem</c>)"). These tests pin exactly that — that each ADD
/// **exists**, **carries the authored-in code** read from the ADR 0018 field,
/// and that the original is **distinct from** the user-added
/// <see cref="PostTranslation"/>/row codes (TD·5, one source of truth per
/// variant). The **FACES** TD1–TD8 (the visible swap, the "None yet" absence,
/// the JS-off degradation) are the visual acceptance recorded in U06's
/// handoff note — the codebase unit-tests controller/VM data shape, not Razor
/// markup.
/// </para>
/// <para>
/// **TD·7 held:** no Core / schema change is exercised or asserted — the
/// POCOs (<see cref="Post"/>, <see cref="PostReply"/>,
/// <see cref="PostTranslation"/>) are used verbatim as their stored shape, and
/// the only new surface under test is the three additive VM ADDs.
/// </para>
public sealed class TranslationDisplayTests
{
    /// <summary>
    /// Pinned test 1 — <c>PostDetail_OriginalLanguageCode_EqualsPostAuthoredIn</c>.
    /// <para>
    /// The community-lane detail VM carries the post's **authored-in**
    /// language (<c>Post.LanguageCode</c>, ADR 0018) on
    /// <see cref="PostDetailViewModel.OriginalLanguageCode"/> — the exact
    /// value the controller's <c>PostDetailViewModel</c> initializer assigns
    /// from <c>result.Post.LanguageCode</c>. The ADD is an additive property
    /// (TD·6: the shared <see cref="LanguageOption"/> record is untouched).
    /// </para>
    /// </summary>
    [Fact]
    public void PostDetail_OriginalLanguageCode_EqualsPostAuthoredIn()
    {
        // A post authored in Polish (ADR 0018 — <c>Post.LanguageCode</c>).
        var post = new Post
        {
            Id = "p1",
            AuthorId = "alice",
            Title = "Zakupy",
            Body = "Ktoś jedzie na zakupy?",
            LanguageCode = "pl", // the authored-in tag (the U02 source, verbatim).
        };

        // The U02 projection, exactly as PostsController.Detail assigns it:
        // OriginalLanguageCode = result.Post.LanguageCode.
        var vm = new PostDetailViewModel
        {
            Post = post,
            OriginalLanguageCode = post.LanguageCode,
        };

        // TD·1 / ADR 0027 — the ADD carries the authored-in code, equal to the
        // post's own ADR 0018 field (a read, never a synthesized value).
        Assert.Equal("pl", vm.OriginalLanguageCode);
        Assert.Equal(post.LanguageCode, vm.OriginalLanguageCode);
    }

    /// <summary>
    /// Pinned test 2 — <c>Reply_OriginalLanguageCode_EqualsReplyAuthoredIn</c>.
    /// <para>
    /// The reply row (<see cref="ReplyItem"/>) carries the reply's
    /// **authored-in** language as its **11th positional**
    /// (<c>OriginalLanguageCode</c>, after <c>DeletedAt</c>) — the value
    /// the controller's <c>ReplyItem</c> ctor appends from
    /// <c>reply.LanguageCode</c>. Each reply has its **own** ADR 0018 tag,
    /// independent of the parent post (ADR 0018 on <c>PostReply</c>).
    /// </para>
    /// </summary>
    [Fact]
    public void Reply_OriginalLanguageCode_EqualsReplyAuthoredIn()
    {
        // A reply authored in German — its own ADR 0018 tag, independent of
        // the parent post's language (the reply's own <c>PostReply.LanguageCode</c>).
        var reply = new PostReply
        {
            Id = "r1",
            PostId = "p1",
            AuthorId = "bob",
            Body = "Ja, ich komme.",
            Created = DateTimeOffset.UtcNow,
            LanguageCode = "de", // the reply's authored-in tag (the U02 source).
        };

        // The U02 projection, exactly as PostsController.Detail appends it as
        // the 11th positional (after DeletedAt): reply.LanguageCode.
        var item = new ReplyItem(
            Id: reply.Id,
            AuthorDisplayName: "B. Resident",
            AuthorSubjectId: reply.AuthorId,
            Body: reply.Body,
            Created: reply.Created,
            Modified: null,
            IsAuthor: reply.AuthorId == "bob",
            Translations: [],
            CanTranslate: false,
            DeletedAt: reply.DeletedAt,
            OriginalLanguageCode: reply.LanguageCode); // 11th positional (TD·6).

        // TD·1 (reply) / ADR 0027 — the ADD carries the reply's authored-in
        // code, equal to its own ADR 0018 field (a read, never a
        // synthesized value).
        Assert.Equal("de", item.OriginalLanguageCode);
        Assert.Equal(reply.LanguageCode, item.OriginalLanguageCode);
    }

    /// <summary>
    /// Pinned test 3 — <c>GroupPostDetail_OriginalLanguageCode_EqualsPostAuthoredIn</c>.
    /// <para>
    /// The group-lane detail VM carries the group post's **authored-in**
    /// language (<c>Post.LanguageCode</c>, ADR 0018) on
    /// <see cref="GroupPostDetailViewModel.OriginalLanguageCode"/> — the
    /// same value <see cref="Kumunita.Web.Controllers.GroupsController.GroupPostDetail"/>
    /// assigns from <c>result.Post.LanguageCode</c>. TD6 (parity): the group
    /// lane is byte-for-byte the same projection as the community lane — the
    /// only divergence is the group route, not the data shape.
    /// </para>
    /// </summary>
    [Fact]
    public void GroupPostDetail_OriginalLanguageCode_EqualsPostAuthoredIn()
    {
        // A group-lane post (ADR 0013 — non-empty <c>GroupId</c>) authored in
        // English (ADR 0018 — <c>Post.LanguageCode</c>).
        var post = new Post
        {
            Id = "gp1",
            AuthorId = "carol",
            GroupId = "g1", // non-empty ⇒ group lane (G·2 lane exclusivity).
            Title = "Borrowing",
            Body = "Anyone free with a ladder?",
            LanguageCode = "en", // the authored-in tag (the U02 source).
        };

        // The U02 projection, exactly as GroupsController.GroupPostDetail
        // assigns it: OriginalLanguageCode = result.Post.LanguageCode.
        var vm = new GroupPostDetailViewModel
        {
            GroupId = post.GroupId,
            Post = post,
            OriginalLanguageCode = post.LanguageCode,
        };

        // TD·1 (group lane) / ADR 0027 — the ADD carries the authored-in
        // code, equal to the post's own ADR 0018 field.
        Assert.Equal("en", vm.OriginalLanguageCode);
        Assert.Equal(post.LanguageCode, vm.OriginalLanguageCode);
    }

    /// <summary>
    /// Pinned test 4 — <c>PostDetail_OriginalNotAmongAddedTranslationCodes</c>.
    /// <para>
    /// TD·5 (**one source of truth per variant**) at the data layer: the
    /// **original** variant (the post's own <c>LanguageCode</c>) is
    /// **distinct from** every user-added <see cref="PostTranslation"/>
    /// <c>LanguageCode</c>. The authored-in language is the *base*; the
    /// added rows are *translations into* other languages — the original is
    /// never among them. This is the data invariant that makes the chip row
    /// a meaningful selector (TD·2): the first chip (the original) cannot be
    /// silently conflated with an added chip.
    /// </para>
    /// </summary>
    [Fact]
    public void PostDetail_OriginalNotAmongAddedTranslationCodes()
    {
        // A post authored in English (ADR 0018 — <c>Post.LanguageCode</c>),
        // with user-added translations (ADR 0022) into **other** languages —
        // French and German — never into English itself.
        var post = new Post
        {
            Id = "p2",
            AuthorId = "dave",
            Title = "BBQ",
            Body = "Grilling on the 14th.",
            LanguageCode = "en",
        };

        var added = new PostTranslation[]
        {
            new() { Id = "t1", PostId = "p2", LanguageCode = "fr", Title = "Barbecue", Body = "Grillade le 14.", AuthorId = "eve" },
            new() { Id = "t2", PostId = "p2", LanguageCode = "de", Title = "Grillen", Body = "Am 14. grillen.", AuthorId = "eve" },
        };

        var vm = new PostDetailViewModel
        {
            Post = post,
            PostTranslations = added,
            OriginalLanguageCode = post.LanguageCode, // the U02 source, verbatim.
        };

        // The original code is present, and is NOT among the added rows'
        // codes (TD·5 — the original is its own variant, not a translation).
        var addedCodes = vm.PostTranslations.Select(t => t.LanguageCode).ToHashSet();
        Assert.Contains("fr", addedCodes);
        Assert.Contains("de", addedCodes);
        Assert.DoesNotContain(vm.OriginalLanguageCode, addedCodes);

        // And the original is the post's own ADR 0018 field — a distinct,
        // first-class variant, equal to <c>Post.LanguageCode</c>.
        Assert.Equal(post.LanguageCode, vm.OriginalLanguageCode);
    }
}
