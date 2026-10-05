using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Projects;
using Kumunita.Core.Query;
using Kumunita.Core.UserInfo;
using Marten;
using Xunit;

namespace Kumunita.Core.Tests.Projects;

/// <summary>
/// M26 U6 — the pinned <c>Todo*_*</c> / <c>Board*_*</c> / <c>Project*_*</c>
/// / <c>Goal*_*</c> tests (design doc
/// <c>m26-sorting-design.md</c> §2.5; <c>m26-u06.md</c> deliverable 3).
/// Same <see cref="PostgresFixture"/> / <c>BootStoreAsync</c> / service-trio
/// shape as <c>ProjectServiceTests</c> (the M5 suite). Drives the 4 project
/// seams (<see cref="IProjectService.ListTodosAsync"/> /
/// <see cref="IProjectService.ListBoardsAsync"/> /
/// <see cref="IProjectService.ListGoalsAsync"/> /
/// <see cref="IProjectService.ListProjectsAsync"/>); the Boards / Projects /
/// Goals seams share the identical <c>OrderByProjectSort</c> helper.
/// <para>
/// <b>U6 note (brief vs frozen doc, user-confirmed):</b> the unit brief
/// pins a <c>Todo_SortPriority</c> test + a <c>priority</c> key, but Part 2
/// §2.8 correction C-3 <b>removed</b> that key — <c>TodoItem</c> has no
/// <c>Priority</c> property (confirmed in <c>Projects/TodoItem.cs</c>), and
/// the locked todos allowlist is exactly
/// <c>created</c>/<c>modified</c>/<c>title</c>/<c>due</c>/<c>status</c>.
/// Per the user's call the <c>priority</c> key is <b>not</b> implemented
/// and no <c>Todo_SortPriority</c> test exists; the frozen allowlist wins.
/// </para>
/// </summary>
public class ProjectSortTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    // ── Todo_SortSpecNull_CurrentOrder (C-SORT·2 pin) ───────────────────────
    //
    // A null SortSpec must produce the **exact** current order —
    // OrderByDescending(Created) — byte-for-byte (C-SORT·2). Three to-dos
    // with distinct Created timestamps come back newest-first.

    [Fact]
    public async Task Todo_SortSpecNull_CurrentOrder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-todo-null-member";
        const string author = "u-u6-todo-null-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Todo(author, "s1-a", member, t1, "a"));
        await Plant(store, Todo(author, "s1-b", member, t3, "b"));
        await Plant(store, Todo(author, "s1-c", member, t2, "c"));

        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: null);

        // Created-desc: t3 (s1-b) > t2 (s1-c) > t1 (s1-a)
        Assert.Equal(new[] { "s1-b", "s1-c", "s1-a" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Todo_SortDue_NullsLast (asc — Postgres null-placement pin) ──────────
    //
    // sort = (due, asc): dated to-dos come back oldest-due-first.
    //
    // MARTEN DRIFT (U6, carried from U4): the frozen §2.2 pin is "nulls last
    // in *both* directions", prescribed via a <c>ThenBy(DueAt is null)</c>
    // boolean flag — but Marten 9.31.2 rejects that expression (non-member
    // OrderBy → BadLinqExpressionException). The ordering is on the raw
    // nullable column and Postgres supplies nulls-**last** in asc. This test
    // pins the *actual* (documented) behavior: dated first (oldest→newest),
    // then undated last.

    [Fact]
    public async Task Todo_SortDue_NullsLast()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-due-asc-member";
        const string author = "u-u6-due-asc-author";

        var now = DateTimeOffset.UtcNow;
        var dueSoon = now.AddDays(1);
        var dueLater = now.AddDays(7);

        await Plant(store, Todo(author, "s2-a", member, now, "a", due: null));
        await Plant(store, Todo(author, "s2-b", member, now, "b", due: dueLater));
        await Plant(store, Todo(author, "s2-c", member, now, "c", due: dueSoon));

        var spec = new SortSpec("due", false); // asc
        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: spec);

        // Postgres asc: dated first (dueSoon s2-c < dueLater s2-b), null last (s2-a)
        Assert.Equal(new[] { "s2-c", "s2-b", "s2-a" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Todo_SortDue_NullsLast_Desc (desc — Postgres null-placement pin) ────
    //
    // sort = (due, desc): dated to-dos come back most-recent-first.
    //
    // MARTEN DRIFT (U6, carried from U4): the frozen §2.2 pin "nulls last in
    // both dirs" is not expressible in Marten 9.31.2 (non-member OrderBy is
    // rejected); the ordering is on the raw column and Postgres supplies
    // nulls-**first** in desc (the opposite of the pinned rule). This test
    // pins the *actual* (documented) behavior: null first (s3-a), then
    // most-recent→oldest (s3-b dueLater > s3-c dueSoon).

    [Fact]
    public async Task Todo_SortDue_NullsLast_Desc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-due-desc-member";
        const string author = "u-u6-due-desc-author";

        var now = DateTimeOffset.UtcNow;
        var dueSoon = now.AddDays(1);
        var dueLater = now.AddDays(7);

        await Plant(store, Todo(author, "s3-a", member, now, "a", due: null));
        await Plant(store, Todo(author, "s3-b", member, now, "b", due: dueLater));
        await Plant(store, Todo(author, "s3-c", member, now, "c", due: dueSoon));

        var spec = new SortSpec("due", true); // desc
        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: spec);

        // Postgres desc: null first (s3-a), then most-recent→oldest (s3-b > s3-c)
        Assert.Equal(new[] { "s3-a", "s3-b", "s3-c" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Todo_SortStatus (the §2.2 nullable-string key pin) ──────────────────
    //
    // sort = (status, asc): StringComparer.OrdinalIgnoreCase on the
    // non-null comparison. Two to-dos have status labels "blocked" and
    // "Open"; one has Status = null.
    //
    // MARTEN DRIFT (U4 carry-forward): the frozen §2.2 `?? ""` sentinel
    // (nulls first in asc) is rejected by Marten 9.31.2 (raw-column form
    // used instead) — Postgres supplies nulls-**last** in asc. This test
    // pins the *actual* (documented) behavior: the two labeled to-dos in
    // OrdinalIgnoreCase order ("blocked" < "open"), then null last.

    [Fact]
    public async Task Todo_SortStatus()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-status-member";
        const string author = "u-u6-status-author";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Todo(author, "s4-a", member, now, "a", status: null));
        await Plant(store, Todo(author, "s4-b", member, now, "b", status: "blocked"));
        await Plant(store, Todo(author, "s4-c", member, now, "c", status: "Open"));

        var spec = new SortSpec("status", false); // asc, OrdinalIgnoreCase
        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: spec);

        // OrdinalIgnoreCase asc: "blocked" (s4-b) < "open" (s4-c); null last (s4-a)
        Assert.Equal(new[] { "s4-b", "s4-c", "s4-a" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Todo_SortTitle_Ordinal ──────────────────────────────────────────────
    //
    // sort = (title, asc): StringComparer.OrdinalIgnoreCase (case-insensitive
    // ordinal). Two to-dos have titles "apple" and "Banana" — the
    // non-null-title path (TodoItem.Title is non-null in the model, so no
    // null-ordering question).

    [Fact]
    public async Task Todo_SortTitle_Ordinal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-ttitle-member";
        const string author = "u-u6-ttitle-author";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Todo(author, "s5-a", member, now, "apple"));
        await Plant(store, Todo(author, "s5-b", member, now, "Banana"));
        await Plant(store, Todo(author, "s5-c", member, now, "avocado"));

        var spec = new SortSpec("title", false); // asc, OrdinalIgnoreCase
        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: spec);

        // OrdinalIgnoreCase asc: "apple" (s5-a) < "avocado" (s5-c) < "banana" (s5-b)
        Assert.Equal(new[] { "s5-a", "s5-c", "s5-b" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Board_SortCreatedAsc ────────────────────────────────────────────────
    //
    // sort = (created, asc) on the boards feed (the shared
    // <c>OrderByProjectSort</c> helper, U2 §2.2 row 8): the three boards
    // come back oldest-first.

    [Fact]
    public async Task Board_SortCreatedAsc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-board-asc-member";
        const string author = "u-u6-board-asc-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Board(author, "s6-a", member, t1, "a"));
        await Plant(store, Board(author, "s6-b", member, t3, "b"));
        await Plant(store, Board(author, "s6-c", member, t2, "c"));

        var spec = new SortSpec("created", false); // asc
        var feed = await svc.ListBoardsAsync(null, member, 1, sort: spec);

        // Created-asc: t1 (s6-a) < t2 (s6-c) < t3 (s6-b)
        Assert.Equal(new[] { "s6-a", "s6-c", "s6-b" }, feed.Items.Select(b => b.Id).ToList());
    }

    // ── Board_SortTitle_Ordinal ─────────────────────────────────────────────
    //
    // sort = (title, asc) on the boards feed: the
    // <c>StringComparer.OrdinalIgnoreCase</c> comparator (case-insensitive
    // ordinal) — two boards have titles "apple" and "Banana".

    [Fact]
    public async Task Board_SortTitle_Ordinal()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-btitle-member";
        const string author = "u-u6-btitle-author";

        var now = DateTimeOffset.UtcNow;
        await Plant(store, Board(author, "s7-a", member, now, "apple"));
        await Plant(store, Board(author, "s7-b", member, now, "Banana"));
        await Plant(store, Board(author, "s7-c", member, now, "avocado"));

        var spec = new SortSpec("title", false); // asc, OrdinalIgnoreCase
        var feed = await svc.ListBoardsAsync(null, member, 1, sort: spec);

        // OrdinalIgnoreCase asc: "apple" (s7-a) < "avocado" (s7-c) < "banana" (s7-b)
        Assert.Equal(new[] { "s7-a", "s7-c", "s7-b" }, feed.Items.Select(b => b.Id).ToList());
    }

    // ── Project_SortCreatedDesc ─────────────────────────────────────────────
    //
    // sort = (created, desc) on the projects feed (the shared
    // <c>OrderByProjectSort</c> helper, U2 §2.2 row 9): the three projects
    // come back newest-first (their GoalId is null — the standalone feed).

    [Fact]
    public async Task Project_SortCreatedDesc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-proj-desc-member";
        const string author = "u-u6-proj-desc-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Project(author, "s8-a", member, t1, "a"));
        await Plant(store, Project(author, "s8-b", member, t3, "b"));
        await Plant(store, Project(author, "s8-c", member, t2, "c"));

        var spec = new SortSpec("created", true); // desc
        var feed = await svc.ListProjectsAsync(null, null, member, 1, sort: spec);

        // Created-desc: t3 (s8-b) > t2 (s8-c) > t1 (s8-a)
        Assert.Equal(new[] { "s8-b", "s8-c", "s8-a" }, feed.Items.Select(p => p.Id).ToList());
    }

    // ── Project_SortModifiedAsc ─────────────────────────────────────────────
    //
    // sort = (modified, asc) on the projects feed: projects with distinct
    // Modified timestamps come back oldest-Modified-first.
    //
    // MARTEN DRIFT (U4 carry-forward): the frozen §2.2 `?? MinValue`
    // sentinel (nulls first in asc) is rejected by Marten 9.31.2 — the raw
    // nullable column is used and Postgres supplies nulls-**last** in asc
    // (the opposite of the pinned sentinel). This test pins the *actual*
    // (documented) behavior with all-rows-non-null Modified, plus a null
    // row to pin the Postgres null-placement: oldest→newest, then null last.

    [Fact]
    public async Task Project_SortModifiedAsc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-pmod-member";
        const string author = "u-u6-pmod-author";

        var now = DateTimeOffset.UtcNow;
        var mOld = now.AddDays(-3);
        var mNew = now.AddDays(-1);

        await Plant(store, Project(author, "s9-a", member, now, "a", modified: null));
        await Plant(store, Project(author, "s9-b", member, now, "b", modified: mOld));
        await Plant(store, Project(author, "s9-c", member, now, "c", modified: mNew));

        var spec = new SortSpec("modified", false); // asc
        var feed = await svc.ListProjectsAsync(null, null, member, 1, sort: spec);

        // Postgres asc: oldest→newest (s9-b mOld < s9-c mNew), null last (s9-a)
        Assert.Equal(new[] { "s9-b", "s9-c", "s9-a" }, feed.Items.Select(p => p.Id).ToList());
    }

    // ── Goal_SortCreatedAsc ─────────────────────────────────────────────────
    //
    // sort = (created, asc) on the goals feed (the shared
    // <c>OrderByProjectSort</c> helper, U2 §2.2 row 10): the three goals
    // come back oldest-first.

    [Fact]
    public async Task Goal_SortCreatedAsc()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-goal-asc-member";
        const string author = "u-u6-goal-asc-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-3);
        var t2 = DateTimeOffset.UtcNow.AddDays(-2);
        var t3 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Goal(author, "s10-a", member, t1, "a"));
        await Plant(store, Goal(author, "s10-b", member, t3, "b"));
        await Plant(store, Goal(author, "s10-c", member, t2, "c"));

        var spec = new SortSpec("created", false); // asc
        var feed = await svc.ListGoalsAsync(null, member, 1, sort: spec);

        // Created-asc: t1 (s10-a) < t2 (s10-c) < t3 (s10-b)
        Assert.Equal(new[] { "s10-a", "s10-c", "s10-b" }, feed.Items.Select(g => g.Id).ToList());
    }

    // ── Todo_InvalidKey_DefaultOrder (C-SORT·1 defensive pin) ───────────────
    //
    // A SortSpec with a key not in the allowlist (simulating a
    // mis-constructed spec — in production Parse already fell back, so
    // this is the _-branch defensive pin) must produce the default
    // order: Created-desc + Id tie-breaker (C-SORT·1).

    [Fact]
    public async Task Todo_InvalidKey_DefaultOrder()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-inv-member";
        const string author = "u-u6-inv-author";

        var t1 = DateTimeOffset.UtcNow.AddDays(-2);
        var t2 = DateTimeOffset.UtcNow.AddDays(-1);

        await Plant(store, Todo(author, "s11-a", member, t1, "a"));
        await Plant(store, Todo(author, "s11-b", member, t2, "b"));

        var spec = new SortSpec("bogus", true); // not in allowlist → _ branch
        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: spec);

        // Default = Created-desc: t2 (s11-b) > t1 (s11-a)
        Assert.Equal(new[] { "s11-b", "s11-a" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Todo_StableTieBreakBy_Id (C-SORT·5) ─────────────────────────────────
    //
    // Two to-dos share the same Created timestamp (a tie on the primary
    // key) but have distinct Ids. The .ThenBy(Id) tie-breaker (C-SORT·5)
    // must order them by Id ascending regardless of the primary key's
    // direction.

    [Fact]
    public async Task Todo_StableTieBreakBy_Id()
    {
        var store = await BootStoreAsync();
        var (_, _, svc) = Services(store);
        const string member = "u-u6-tie-member";
        const string author = "u-u6-tie-author";

        var sharedTime = DateTimeOffset.UtcNow;
        // Deliberately planted in reverse Id order so the test fails if
        // the tie-breaker is absent.
        await Plant(store, Todo(author, "tie-b", member, sharedTime, "b"));
        await Plant(store, Todo(author, "tie-a", member, sharedTime, "a"));

        var spec = new SortSpec("created", false); // created asc — the tie is what matters
        var feed = await svc.ListTodosAsync(null, null, member, 1, sort: spec);

        // Created ties → ThenBy(Id) asc: "tie-a" < "tie-b"
        Assert.Equal(new[] { "tie-a", "tie-b" }, feed.Items.Select(t => t.Id).ToList());
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static TodoItem Todo(string author, string id, string memberId,
        DateTimeOffset created, string title, string? status = null, DateTimeOffset? due = null)
        => new()
        {
            Id = id,
            Title = title,
            AuthorId = author,
            Created = created,
            Status = status,
            DueAt = due,
            Audience = Aud(memberId),
        };

    private static KanbanBoard Board(string author, string id, string memberId, DateTimeOffset created, string title)
        => new()
        {
            Id = id,
            Title = title,
            AuthorId = author,
            Created = created,
            Audience = Aud(memberId),
        };

    private static Project Project(string author, string id, string memberId,
        DateTimeOffset created, string title, DateTimeOffset? modified = null)
        => new()
        {
            Id = id,
            Title = title,
            AuthorId = author,
            Created = created,
            Modified = modified,
            Audience = Aud(memberId),
        };

    private static ProjectGoal Goal(string author, string id, string memberId, DateTimeOffset created, string title)
        => new()
        {
            Id = id,
            Title = title,
            AuthorId = author,
            Created = created,
            Audience = Aud(memberId),
        };

    private static Audience Aud(string memberId)
        => new(AudienceMode.Any, [new AudienceGrant(GrantKind.User, memberId)]);

    private async Task<IDocumentStore> BootStoreAsync()
    {
        var conn = await fixture.NewDatabaseAsync(TestContext.Current.CancellationToken);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M4DocTypes.Configure(opts);
            M5DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(
            null, null, TestContext.Current.CancellationToken);
        return store;
    }

    private static (UserInfoService User, AuthorizationService Authz, ProjectService Projects)
        Services(IDocumentStore store)
    {
        var userInfo = new UserInfoService(store);
        var authz = new AuthorizationService(store, userInfo);
        var projects = new ProjectService(store, authz, userInfo);
        return (userInfo, authz, projects);
    }

    private static async Task Plant(IDocumentStore store, object document)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(document);
        await w.SaveChangesAsync(ct);
    }
}
