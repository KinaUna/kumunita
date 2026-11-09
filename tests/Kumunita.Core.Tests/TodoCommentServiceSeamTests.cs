using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Projects;
using Kumunita.Core.UserInfo;
using Marten;
using Npgsql;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// C3-03 (ADR 0293) — the <see cref="TodoCommentService"/> seam's own tests.
/// The **to-do comment** lane (ADR 0100 / C-M3·1) was extracted out of
/// <see cref="TodoService"/> as a real part-vs-whole surface (the
/// <see cref="TodoCommentService"/> class + <see cref="ITodoCommentService"/>
/// interface — the C3-00 <c>EventRsvpGuardianService</c> pattern; the
/// <see cref="ProjectsStore"/> base supplies the shared <c>_store</c> /
/// <c>_authorization</c> + the <c>ResolveLanguageCodeAsync</c> ADR 0018
/// helper). These tests construct the seam **directly** (not through the
/// <see cref="TodoService"/> forwarders the existing <c>TodoServiceTests</c>
/// exercises) to prove the seam is a standalone surface that works on its own:
/// the same <see cref="PostgresFixture"/> (fresh scratch Postgres), the same
/// <c>UserInfoService</c> + <c>AuthorizationService</c> trio, but the comment
/// calls go through <see cref="TodoCommentService"/> (the named seam) rather
/// than the god-service it came out of.
/// <para>
/// This is the DoD's "each extracted seam has its own unit test" line for the
/// todo-comment seam, and it satisfies the gate's seam-test Rule 2
/// (<see cref="ITodoCommentService"/> named by a test file).
/// </para>
/// </summary>
public class TodoCommentServiceSeamTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private static readonly IReadOnlySet<string> NoRoles = new HashSet<string>(StringComparer.Ordinal);

    // ── 1 — create: the to-do's author comments (the Read decision allows) ─
    //
    // The author comments on their own to-do through the
    // <see cref="TodoCommentService"/> seam directly: the comment is stored
    // (top-level, ParentId null) and the audit row (TargetKind "todo", action
    // "todo.comment.create", Via Owner, Allow) commits — the seam's own
    // standing read ran, not the god-service's.

    [Fact]
    public async Task Seam_CreateComment_AuthorComments()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-tc-seam-create-author";

        await PlantTodo(store, "tc-create-todo", author);

        var comment = await svc.CreateTodoCommentAsync(
            "tc-create-todo", author, NoRoles, "a comment", null, null, TestContext.Current.CancellationToken);
        Assert.Equal("tc-create-todo", comment.TodoId);
        Assert.Null(comment.ParentId);
        Assert.Equal(author, comment.AuthorId);
        Assert.Equal("a comment", comment.Body);

        var row = Assert.Single(await CommentAudits(store, action: "todo.comment.create", actor: author));
        Assert.Equal("todo", row.TargetKind);
        Assert.Equal("tc-create-todo", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 2 — the create gate: a stranger on a private to-do is denied ──────
    //
    // A stranger comments on a to-do they are **not** readable (the audience
    // grant is to a third party, so the to-do's Read decision denies the
    // stranger): the standing read
    // (<c>CanAsync(Read)</c>, the C-M3·1 access boundary) refuses, and a Deny
    // audit row commits before the throw.

    [Fact]
    public async Task Seam_CreateComment_NonReadableDenied()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-tc-seam-deny-author";
        const string stranger = "u-tc-seam-deny-stranger";

        // A to-do whose audience is a **third party** (the author and the
        // stranger are both outside it — only the grantee is readable).
        await Plant(store, new TodoItem
        {
            Id = "tc-deny-todo", AuthorId = author, Title = "locked",
            Created = DateTimeOffset.UtcNow,
            Audience = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "u-tc-seam-deny-grantee")]),
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.CreateTodoCommentAsync("tc-deny-todo", stranger, NoRoles, "nope", null, null, TestContext.Current.CancellationToken));
    }

    // ── 3 — delete: the author soft-deletes their comment ──────────────────
    //
    // The comment author deletes their own comment through the seam: the
    // comment's <c>DeletedAt</c> stamp is set (the ADR 0024 soft-delete) and
    // the audit row (action "todo.comment.delete", Via Owner, Allow) commits.

    [Fact]
    public async Task Seam_DeleteComment_AuthorDeletes()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-tc-seam-delete-author";

        await PlantTodo(store, "tc-delete-todo", author);
        await Plant(store, new TodoComment
        {
            Id = "tc-delete-comment", TodoId = "tc-delete-todo", AuthorId = author,
            Body = "to delete", Created = DateTimeOffset.UtcNow,
        });

        var comment = await svc.DeleteTodoCommentAsync(
            "tc-delete-todo", "tc-delete-comment", author, NoRoles, TestContext.Current.CancellationToken);
        Assert.NotNull(comment.DeletedAt);

        var row = Assert.Single(await CommentAudits(store, action: "todo.comment.delete", actor: author));
        Assert.Equal("todo", row.TargetKind);
        Assert.Equal("tc-delete-todo", row.TargetId);
        Assert.Equal(AccessVia.Owner, row.Via);
        Assert.Equal(AccessOutcome.Allow, row.Outcome);
    }

    // ── 4 — the delete gate: a non-author is refused (ADR 0016) ────────────
    //
    // A non-author deletes a comment they do not own through the seam: the
    // author-only gate (ADR 0016 / ADR 0024) refuses before any write — no
    // <c>DeletedAt</c> stamp, no Deny-vs-Allow row for this action.

    [Fact]
    public async Task Seam_DeleteComment_NonAuthorDenied()
    {
        await using var store = await BootStoreAsync();
        var (_, authz, svc) = Services(store);
        const string author = "u-tc-seam-del-deny-author";
        const string stranger = "u-tc-seam-del-deny-stranger";

        await PlantTodo(store, "tc-deldeny-todo", author);
        await Plant(store, new TodoComment
        {
            Id = "tc-deldeny-comment", TodoId = "tc-deldeny-todo", AuthorId = author,
            Body = "author's", Created = DateTimeOffset.UtcNow,
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => svc.DeleteTodoCommentAsync("tc-deldeny-todo", "tc-deldeny-comment", stranger, NoRoles, TestContext.Current.CancellationToken));
    }

    // ── the harness ────────────────────────────────────────────────────────

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            CoreDocTypes.Configure(opts);
            PostDocTypes.Configure(opts);
            EventDocTypes.Configure(opts);
            ProjectDocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    /// <summary>The service trio over the scratch store, with the comment lane
    /// resolved on the <see cref="TodoCommentService"/> seam directly (not
    /// through the <see cref="TodoService"/> forwarders) — the C3-03
    /// named-seam construction the DI registration supplies in the app.</summary>
    private static (UserInfoService User, AuthorizationService Authz, TodoCommentService Comments)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var comments = new TodoCommentService(store, authz, userInfo); // notifications + mentions null (the optional mention-emitter seams; not exercised here)
        return (userInfo, authz, comments);
    }

    /// <summary>Plant a public to-do owned by the author.</summary>
    private static Task PlantTodo(IDocumentStore store, string todoId, string author)
        => Plant(store, new TodoItem
        {
            Id = todoId, AuthorId = author, Title = "todo " + todoId,
            Created = DateTimeOffset.UtcNow, Audience = null,
        });

    /// <summary>Plant a document row directly (fixture seeding, not a service
    /// write seam — the <c>TodoServiceTests.Plant</c> precedent).</summary>
    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }

    /// <summary>The comment lane's audit rows (filter by action).</summary>
    private static async Task<IReadOnlyList<AccessAudit>> CommentAudits(
        IDocumentStore store, string action, string? actor = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var s = store.QuerySession();
        var q = s.Query<AccessAudit>().Where(a => a.Action == action);
        if (actor is not null) q = q.Where(a => a.ActorId == actor);
        return await q.ToListAsync(ct);
    }
}
