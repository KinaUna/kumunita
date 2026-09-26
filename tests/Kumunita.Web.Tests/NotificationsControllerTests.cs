using System.Security.Claims;
using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
using Kumunita.Core.Pages;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="NotificationsController"/> Web-boundary seam tests (M6 — the
/// **shared awareness** arrow, ADR 0076; the five route pins from design doc
/// §6.4). Mirrors the <see cref="ProjectsControllerTests"/> /
/// <see cref="AdminTimezoneControllerTests"/> harness shape: the controller
/// takes the **concrete sealed** <see cref="NotificationService"/> (no
/// interface — the M5 <c>ProjectService</c> precedent), so it is driven with
/// a **real** <see cref="NotificationService"/> over a live scratch-Postgres
/// <see cref="IDocumentStore"/> (<see cref="PostgresFixture"/>, the
/// <c>ProjectsControllerTests</c> real-store precedent — Marten 9's
/// <c>ToListAsync()</c> / <c>LoadAsync()</c> run a live query and cannot be
/// NSubstituted) with plain NSubstitute stands-ins for the
/// <c>IUserInfoService</c> / <c>ITranslationProvider</c> /
/// <c>IMailerStage</c> seams (the read + state lanes exercised here never
/// touch them).
/// <para>
/// **No <c>IAuthorizationService</c> anywhere** (C-M6·3 — a notification is a
/// *personal read*, not an <c>AccessAction</c> decision; the actor's
/// <c>SubjectId</c> is the whole access story). The <c>[Authorize]</c> gate is
/// an attribute (not exercised here — the action body runs unguarded, the
/// thin-HTTP-layer precedent); the actor id is minted from the signed-in
/// cookie via <see cref="KumunitaPrincipal"/>.
/// </para>
/// </summary>
public class NotificationsControllerTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Actor = "subj-notif-actor";

    // ── 1 — GET /notifications → 200 + the 50-row cap (the InboxCap constant) ─

    /// <summary>
    /// <c>GET /notifications</c> (route 1, C-M6·8): the inbox returns the
    /// actor's **most recent <see cref="NotificationService.InboxCap"/> (50)**
    /// rows, newest-first. A personal read — <c>200</c> + a
    /// <see cref="ViewResult"/> whose model carries the capped row set (the
    /// 50-row cap is the service's <c>Take(InboxCap)</c>; the view model's
    /// <c>Count</c> mirrors it). Planting more than 50 rows pins the cap.
    /// </summary>
    [Fact]
    public async Task GET_Notifications_Returns_200_And_50RowCap()
    {
        var store = await BootStoreAsync();
        // Plant 60 rows — more than the InboxCap (50) — so the cap is exercised.
        for (var i = 0; i < 60; i++)
            await PlantNotification(store, Actor, NotificationKinds.PostReply,
                key: $"notification:post.reply:cap-{i}", readAt: null,
                created: DateTimeOffset.UtcNow.AddMinutes(i));

        var controller = Build(store);
        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);                    // 200 (a view render)
        var vm = Assert.IsType<NotificationsInboxViewModel>(view.ViewData.Model);
        // The 50-row cap — the service's Take(InboxCap) trims the 60 planted.
        Assert.Equal(NotificationService.InboxCap, vm.Notifications.Count);
        Assert.Equal(NotificationService.InboxCap, vm.Count);
        // Newest-first (Created descending) — the most-recent planted row leads.
        Assert.True(vm.Notifications[0].Created >= vm.Notifications[^1].Created);
    }

    // ── 2 — GET /notifications/unread-count → 200 + JSON { "count": N } ─────

    /// <summary>
    /// <c>GET /notifications/unread-count</c> (route 2, C-M6·10): the layout
    /// bell's 30-second poller target returns <c>200</c> + a
    /// <see cref="JsonResult"/> whose payload is exactly
    /// <c>{ "count": N }</c> (the N unread rows — the rows with
    /// <c>ReadAt == null</c>). A personal read — no audit row.
    /// </summary>
    [Fact]
    public async Task GET_Notifications_UnreadCount_Returns_Json_Count()
    {
        var store = await BootStoreAsync();
        // 3 unread + 2 read → the unread count is exactly 3.
        for (var i = 0; i < 3; i++)
            await PlantNotification(store, Actor, NotificationKinds.GroupPost,
                key: $"notification:group.post:un-{i}", readAt: null);
        for (var i = 0; i < 2; i++)
            await PlantNotification(store, Actor, NotificationKinds.GroupPost,
                key: $"notification:group.post:rd-{i}", readAt: DateTimeOffset.UtcNow);

        var controller = Build(store);
        var result = await controller.UnreadCount();

        var json = Assert.IsType<JsonResult>(result);                    // 200 + application/json
        var payload = JsonSerializer.SerializeToElement(json.Value);
        Assert.True(payload.TryGetProperty("count", out var countProp));
        Assert.Equal(3, countProp.GetInt32());
    }

    // ── 3 — POST /notifications/mark-all-read → 302 + ReadAt set on all unread ─

    /// <summary>
    /// <c>POST /notifications/mark-all-read</c> (route 3, C-M6·8): the state
    /// lane sets <c>ReadAt = now</c> on **all** the actor's unread rows (one
    /// commit) and redirects back to the inbox (<c>302</c> /
    /// <see cref="RedirectToActionResult"/>). A state lane, not a read — no
    /// audit row. Asserted against the live store: after the call, every row
    /// the actor had carries a non-null <c>ReadAt</c>.
    /// </summary>
    [Fact]
    public async Task POST_Notifications_MarkAllRead_Returns_302_And_Sets_ReadAt()
    {
        var store = await BootStoreAsync();
        // 4 unread + 1 already read.
        for (var i = 0; i < 4; i++)
            await PlantNotification(store, Actor, NotificationKinds.EventRsvp,
                key: $"notification:event.rsvp:mr-{i}", readAt: null);
        await PlantNotification(store, Actor, NotificationKinds.EventRsvp,
            key: "notification:event.rsvp:mr-done", readAt: DateTimeOffset.UtcNow);

        var controller = Build(store);
        var result = await controller.MarkAllRead();

        // 302 — the redirect back to the inbox (Index).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(NotificationsController.Index), redirect.ActionName);
        // ...and the live store shows every row now read (the unread set is empty).
        Assert.Equal(0, await CountUnread(store, Actor));
        // The previously-read row is untouched (still read).
        Assert.Equal(5, await CountAll(store, Actor));
    }

    // ── 3a — ADR 0096 — POST /notifications/{id}/mark-read → 302 + that one row read ─

    /// <summary>
    /// <c>POST /notifications/{id}/mark-read</c> (ADR 0096, the single-row
    /// read lane): sets <c>ReadAt = now</c> on the **one** named notification
    /// the actor owns (the frozen
    /// <see cref="NotificationService.MarkReadAsync"/>), and **leaves the
    /// actor's other unread rows unread** (unlike mark-all-read) — the
    /// redirect is back to the inbox (<c>302</c> /
    /// <see cref="RedirectToActionResult"/>). A state lane — no audit row.
    /// Asserted against the live store: the named row is now read, the two
    /// sibling unread rows are still unread.
    /// </summary>
    [Fact]
    public async Task POST_Notifications_MarkRead_Sets_Only_That_Row_Read()
    {
        var store = await BootStoreAsync();
        // One target row (unread) + two sibling unread rows.
        var targetKey = "notification:post.reply:mr1-target";
        await PlantNotification(store, Actor, NotificationKinds.PostReply,
            key: targetKey, readAt: null);
        for (var i = 0; i < 2; i++)
            await PlantNotification(store, Actor, NotificationKinds.PostReply,
                key: $"notification:post.reply:mr1-sib-{i}", readAt: null);
        var targetId = (await FindRowId(store, targetKey))!;

        var controller = Build(store);
        var result = await controller.MarkRead(targetId);

        // 302 — the redirect back to the inbox (Index).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(NotificationsController.Index), redirect.ActionName);
        // The named row is now read; the two siblings are **still** unread
        // (the single-row lane touches exactly one row).
        Assert.Equal(2, await CountUnread(store, Actor));
        var row = await LoadRow(store, targetId);
        Assert.NotNull(row?.ReadAt);
    }

    // ── 3b — ADR 0096 — POST /notifications/{id}/mark-unread → 302 + that one row unread ─

    /// <summary>
    /// <c>POST /notifications/{id}/mark-unread</c> (ADR 0096, the
    /// single-row unread lane): clears <c>ReadAt</c> on the **one** named
    /// notification the actor owns (the frozen
    /// <see cref="NotificationService.MarkUnreadAsync"/>), and **leaves the
    /// actor's other read rows read** — the redirect is back to the inbox
    /// (<c>302</c> / <see cref="RedirectToActionResult"/>). A state lane —
    /// no audit row. Asserted against the live store: the named row is now
    /// unread, the sibling read row is still read.
    /// </summary>
    [Fact]
    public async Task POST_Notifications_MarkUnread_Sets_Only_That_Row_Unread()
    {
        var store = await BootStoreAsync();
        // One target row (read) + one sibling read row + one sibling unread row.
        var targetKey = "notification:group.post:mu1-target";
        await PlantNotification(store, Actor, NotificationKinds.GroupPost,
            key: targetKey, readAt: DateTimeOffset.UtcNow);
        await PlantNotification(store, Actor, NotificationKinds.GroupPost,
            key: "notification:group.post:mu1-sib-read", readAt: DateTimeOffset.UtcNow);
        await PlantNotification(store, Actor, NotificationKinds.GroupPost,
            key: "notification:group.post:mu1-sib-unread", readAt: null);
        var targetId = (await FindRowId(store, targetKey))!;

        var controller = Build(store);
        var result = await controller.MarkUnread(targetId);

        // 302 — the redirect back to the inbox (Index).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(NotificationsController.Index), redirect.ActionName);
        // The named row is now unread; the sibling unread row is untouched,
        // so the unread count goes 1 → 2, and the sibling read row is still read.
        Assert.Equal(2, await CountUnread(store, Actor));
        var row = await LoadRow(store, targetId);
        Assert.Null(row?.ReadAt);
    }

    // ── 4 — GET /notifications/preferences → 200 + the thirteen Known toggles ─

    /// <summary>
    /// <c>GET /notifications/preferences</c> (route 4, C-M6·2 / C-M6·9): the
    /// preference read returns <c>200</c> + a <see cref="ViewResult"/> whose
    /// model carries the **closed, code-owned** <see cref="NotificationKinds
    /// .Known"/> thirteen-entry toggle set (C-M6·2 — a resident cannot mint a kind
    /// string the emitters don't use; ADR 0077 adds the two admin-lane kinds;
    /// ADR 0083 adds the two group-membership kinds — group.added / group.invite)
    /// and the actor's <c>KindsEnabled</c>
    /// (the lean-default <c>null</c> when no preference row yet — C-M6·9).
    /// </summary>
    [Fact]
    public async Task GET_Notifications_Preferences_Returns_200_And_Toggles()
    {
        var store = await BootStoreAsync();
        // No preference row planted → the service synthesizes the lean-default
        // (KindsEnabled = null = all enabled).

        var controller = Build(store);
        var result = await controller.Preferences();

        var view = Assert.IsType<ViewResult>(result);                    // 200
        var vm = Assert.IsType<NotificationPreferencesViewModel>(view.ViewData.Model);
        // The sixteen-entry closed kind set (C-M6·2 / C-M6·9; ADR 0077
        // adds the two admin-lane kinds — account.signup / account.verified;
        // ADR 0083 adds the two group-membership kinds — group.added /
        // group.invite; ADR 0084 adds the three per-target subscription
        // kinds — announcement / community.post / page.child).
        Assert.Equal(NotificationKinds.Known, vm.AllKinds);
        Assert.Equal(16, vm.AllKinds.Count);
        // Lean-default: no stored preference yet → KindsEnabled is null.
        Assert.Null(vm.KindsEnabled);
    }

    // ── 5 — POST /notifications/preferences → 302 + KindsEnabled update ──────

    /// <summary>
    /// <c>POST /notifications/preferences</c> (route 5, C-M6·9): the
    /// preference write upserts the actor's <see cref="NotificationPreference"/>
    /// with the bound toggle set (the resident's own choice is the authority
    /// at emit time, C-M6·7) and redirects back to the inbox (<c>302</c> /
    /// <see cref="RedirectToActionResult"/>). A state lane — no audit row.
    /// Asserted against the live store: after the call, the actor's
    /// preference row carries the posted <c>KindsEnabled</c> set.
    /// </summary>
    [Fact]
    public async Task POST_Notifications_Preferences_Returns_302_And_Updates_KindsEnabled()
    {
        var store = await BootStoreAsync();

        var controller = Build(store);
        var posted = new[]
        {
            NotificationKinds.PostReply,
            NotificationKinds.TodoAssign,
        };
        var result = await controller.SavePreferences(
            new NotificationPreferencesViewModel(posted, NotificationKinds.Known));

        // 302 — the redirect back to the inbox (Index).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(NotificationsController.Index), redirect.ActionName);
        // ...and the live store shows the actor's preference row now carries
        // the posted (Known-intersected) enabled set (C-M6·9).
        await using var q = store.QuerySession();
        var stored = await q.LoadAsync<NotificationPreference>(Actor, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(posted, stored!.KindsEnabled);
    }

    // ── ADR 0084 — the per-target subscriptions settings surface ────────────

    // S1 — GET /notifications/subscriptions: one row per (kind, target) the
    //      resident can subscribe to, with the effective state (the stored
    //      row's Enabled when present, else the kind's default from
    //      NotificationKinds.OptInKinds — opt-IN kinds default off,
    //      opt-OUT kinds default on).
    //
    // S2 — POST /notifications/subscriptions (valid kind + target): upserts
    //      the row and redirects to Subscriptions.
    //
    // S3 — POST /notifications/subscriptions (kind outside the four
    //      per-target kinds, or empty target): 400.

    [Fact]
    public async Task S1_GET_Subscriptions_Returns_200_And_Effective_Rows()
    {
        var store = await BootStoreAsync();
        const string community = "safety";
        const string group = "grp-s1";
        const string parentPage = "parent-s1";

        // Plant an explicit opt-IN row for (announcement, community) saying
        // "enabled" — the toggle the resident sees should reflect the stored
        // row's value (true), not the opt-IN default (false).
        var svc = new NotificationService(store,
            Substitute.For<IUserInfoService>(), Substitute.For<ITranslationProvider>(),
            Substitute.For<IMailerStage>());
        await svc.SetSubscriptionAsync(Actor, NotificationKinds.Announcement,
            community, enabled: true, TestContext.Current.CancellationToken);
        // And an explicit opt-OUT row for (group.post, group) saying
        // "disabled" — the toggle the resident sees should reflect false,
        // not the opt-OUT default (true).
        await svc.SetSubscriptionAsync(Actor, NotificationKinds.GroupPost,
            group, enabled: false, TestContext.Current.CancellationToken);
        // And an opt-IN row for (page.child, parentPage) saying "enabled".
        await svc.SetSubscriptionAsync(Actor, NotificationKinds.PageChild,
            parentPage, enabled: true, TestContext.Current.CancellationToken);

        // The display-name seams: one member community, one owned group, one
        // parent page the resident has a page.child row for.
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetCommunityIdsAsync(Actor)
            .Returns(Task.FromResult<IReadOnlyCollection<string>>(new[] { community }));
        userInfo.GetComponentsAsync(true)
            .Returns(Task.FromResult<IReadOnlyList<Component>>(new List<Component>
            {
                new Component { Id = community, Name = "Safety" },
            }));
        userInfo.GetGroupsForUserAsync(Actor)
            .Returns(Task.FromResult<IReadOnlyList<Group>>(new List<Group>
            {
                new Group { Id = group, Name = "My Group" },
            }));
        var pages = Substitute.For<IPageService>();
        pages.GetTreeAsync()
            .Returns(Task.FromResult<IReadOnlyList<Page>>(new List<Page>
            {
                new Page { Id = parentPage, Title = "Parent S1" },
            }));

        var controller = Build(store, userInfo, pages);
        var result = await controller.Subscriptions();

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<NotificationSubscriptionsViewModel>(view.ViewData.Model);
        var rows = vm.Rows;

        // The expected set: announcement/community (stored true),
        // announcement/announcements (no row → opt-IN default false),
        // community.post/community (no row → opt-OUT default true),
        // group.post/grp-s1 (stored false),
        // page.child/parent-s1 (stored true).
        Assert.Equal(5, rows.Count);

        var byKey = rows.ToDictionary(r => (r.Kind, r.TargetId));
        // Stored rows reflect the resident's explicit choice.
        Assert.True(byKey[(NotificationKinds.Announcement, community)].Enabled);
        Assert.False(byKey[(NotificationKinds.GroupPost, group)].Enabled);
        Assert.True(byKey[(NotificationKinds.PageChild, parentPage)].Enabled);
        // The flat-announcement sentinel: opt-IN default (false) — no row.
        Assert.False(byKey[(NotificationKinds.Announcement, "announcements")].Enabled);
        // community.post: opt-OUT default (true) — no row.
        Assert.True(byKey[(NotificationKinds.CommunityPost, community)].Enabled);

        // The display names resolve through the seams (a nameless row is a
        // display gap, not an error — the controller degrades to the id).
        Assert.Equal("Safety", byKey[(NotificationKinds.Announcement, community)].Name);
        Assert.Equal("My Group", byKey[(NotificationKinds.GroupPost, group)].Name);
        Assert.Equal("Parent S1", byKey[(NotificationKinds.PageChild, parentPage)].Name);
    }

    [Fact]
    public async Task S2_POST_Subscriptions_ValidKindAndTarget_Updates_Row_And_Redirects()
    {
        var store = await BootStoreAsync();
        const string community = "safety";

        var controller = Build(store);
        var result = await controller.SaveSubscription(
            kind: NotificationKinds.CommunityPost,
            targetId: community,
            enabled: false);

        // 302 — the redirect back to the Subscriptions list (the toggle
        // page is the same page — the switch's new state is reflected on
        // the next GET).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(NotificationsController.Subscriptions), redirect.ActionName);

        // The live store shows the actor's row now carries Enabled=false
        // (the SetSubscriptionAsync upsert — the row is the record of the
        // resident's last explicit choice, never deleted).
        var svc = new NotificationService(store,
            Substitute.For<IUserInfoService>(), Substitute.For<ITranslationProvider>(),
            Substitute.For<IMailerStage>());
        var rows = await svc.GetSubscriptionsAsync(Actor, TestContext.Current.CancellationToken);
        var mine = Assert.Single(rows, r => r.Kind == NotificationKinds.CommunityPost && r.TargetId == community);
        Assert.False(mine.Enabled);
    }

    [Fact]
    public async Task S3_POST_Subscriptions_UnknownKind_Returns_400()
    {
        var store = await BootStoreAsync();
        var controller = Build(store);

        // A kind outside the four per-target lanes (e.g. the legacy
        // post.reply kind, which has no per-target scope) is rejected — a
        // client cannot mint a subscription the emitters don't consult.
        var result = await controller.SaveSubscription(
            kind: NotificationKinds.PostReply,
            targetId: "some-target",
            enabled: true);
        Assert.IsType<BadRequestResult>(result);

        // And an empty target is rejected (the (kind, target) business key
        // requires both parts).
        result = await controller.SaveSubscription(
            kind: NotificationKinds.CommunityPost,
            targetId: "",
            enabled: true);
        Assert.IsType<BadRequestResult>(result);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Boots a real Marten <see cref="IDocumentStore"/> over a fresh scratch
    /// Postgres database (the <see cref="ProjectsControllerTests"/>
    /// real-store precedent) with the M1 + M3 + M6 surfaces so the
    /// <see cref="Notification"/> / <see cref="NotificationPreference"/> /
    /// <see cref="Profile"/> / <see cref="AccessAudit"/> tables exist.
    /// </summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            opts.Storage.Add<AuthorizationFeature>();
            M1DocTypes.Configure(opts);
            M3DocTypes.Configure(opts);
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>
    /// Builds a <see cref="NotificationsController"/> over a **real**
    /// <see cref="NotificationService"/> (the concrete sealed service — the
    /// M5 precedent) on the scratch store, with plain NSubstitute stands-ins
    /// for the three frozen seams (the read + state lanes exercised here never
    /// touch them). The ADR 0084 <c>userInfo</c> / <c>pages</c> display-name
    /// seams are optional — pass <c>null</c> (the default) when the test
    /// doesn't exercise the subscriptions surface, or substitute them with
    /// the S1 test's display-name stubs. The actor is the
    /// <c>subj-notif-actor</c> claim principal (the <see cref="KumunitaPrincipal"/>
    /// subject-claim shape); a no-op <see cref="ITempDataProvider"/> closes
    /// the <c>TempData</c> bag so the <c>RedirectToActionResult</c> branches
    /// don't NRE.
    /// </summary>
    private static NotificationsController Build(IDocumentStore store,
        IUserInfoService? userInfo = null, IPageService? pages = null)
    {
        var translator = Substitute.For<ITranslationProvider>();
        var mailer = Substitute.For<IMailerStage>();

        var service = new NotificationService(store, userInfo ?? Substitute.For<IUserInfoService>(), translator, mailer);
        var controller = new NotificationsController(service, userInfo: userInfo, pages: pages);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(
                        new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Actor) },
                        authenticationType: "test")),
            },
        };
        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    private static async Task PlantNotification(IDocumentStore store, string recipientId, string kind,
        string key, DateTimeOffset? readAt, DateTimeOffset? created = null)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var w = store.OpenSession(new Marten.Services.SessionOptions());
        w.Store(new Notification
        {
            Id = Guid.NewGuid().ToString("N"),
            RecipientId = recipientId,
            Kind = kind,
            IdempotencyKey = key,
            SourceId = key.Split(':')[^1],
            Subject = "S",
            Body = "B",
            Created = created ?? DateTimeOffset.UtcNow,
            ReadAt = readAt,
        });
        await w.SaveChangesAsync(ct);
    }

    private static async Task<int> CountUnread(IDocumentStore store, string recipientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Notification>()
            .Where(n => n.RecipientId == recipientId && n.ReadAt == null)
            .CountAsync(ct);
    }

    private static async Task<int> CountAll(IDocumentStore store, string recipientId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.Query<Notification>()
            .Where(n => n.RecipientId == recipientId)
            .CountAsync(ct);
    }

    private static async Task<string?> FindRowId(IDocumentStore store, string idempotencyKey)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return (await q.Query<Notification>()
            .Where(n => n.IdempotencyKey == idempotencyKey)
            .FirstOrDefaultAsync(ct))?.Id;
    }

    private static async Task<Notification?> LoadRow(IDocumentStore store, string notificationId)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var q = store.QuerySession();
        return await q.LoadAsync<Notification>(notificationId, ct);
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
