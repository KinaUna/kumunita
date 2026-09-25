using System.Security.Claims;
using System.Text.Json;
using Kumunita.Core;
using Kumunita.Core.Authorization;
using Kumunita.Core.Identity;
using Kumunita.Core.Localization;
using Kumunita.Core.Notifications;
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

    // ── 4 — GET /notifications/preferences → 200 + the eleven Known toggles ───

    /// <summary>
    /// <c>GET /notifications/preferences</c> (route 4, C-M6·2 / C-M6·9): the
    /// preference read returns <c>200</c> + a <see cref="ViewResult"/> whose
    /// model carries the **closed, code-owned** <see cref="NotificationKinds
    /// .Known"/> eleven-entry toggle set (C-M6·2 — a resident cannot mint a kind
    /// string the emitters don't use; ADR 0077 adds the two admin-lane kinds) and
    /// the actor's <c>KindsEnabled</c>
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
        // The eleven-entry closed kind set (C-M6·2 / C-M6·9; ADR 0077
        // adds the two admin-lane kinds — account.signup / account.verified).
        Assert.Equal(NotificationKinds.Known, vm.AllKinds);
        Assert.Equal(11, vm.AllKinds.Count);
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
    /// touch them). The actor is the <c>subj-notif-actor</c> claim principal
    /// (the <see cref="KumunitaPrincipal"/> subject-claim shape); a no-op
    /// <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag so the
    /// <c>RedirectToActionResult</c> branches don't NRE.
    /// </summary>
    private static NotificationsController Build(IDocumentStore store)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var translator = Substitute.For<ITranslationProvider>();
        var mailer = Substitute.For<IMailerStage>();

        var service = new NotificationService(store, userInfo, translator, mailer);
        var controller = new NotificationsController(service);

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
