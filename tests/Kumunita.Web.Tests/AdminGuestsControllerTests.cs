using System.Reflection;
using System.Security.Claims;
using Kumunita.Core.Identity;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="AdminGuestsController"/> Web-boundary seam tests (M19 U04 —
/// the D6 admin surface: <c>/admin/guests</c>, the GlobalAdmin's control plane
/// over the guest standing). Mirrors <see cref="AdminSignupControllerTests"/>
/// (ADR 0050) exactly: the **frozen** <see cref="IIdentityService"/> seam is
/// substituted with NSubstitute (the controller delegates the write to the
/// **single** audited write lane <see cref="IIdentityService
/// .SetGuestAccessAsync"/> — the <c>AccessAudit</c> row is the service's,
/// <c>Via = Admin</c>, action <c>guest.set-standing</c> — C-M19·5), and the
/// controller is tested as the thin seam over that lane. The audit-row
/// assertion lives in the Core tests (the service owns it); the controller's
/// pins are: the read seam seeds the form (C-M19·4 empty floor), the write
/// seam is delegated with the composed <see cref="GuestSurface"/> set, the
/// GlobalAdmin gate is carried as <c>[Authorize(Roles = GlobalAdmin)]</c>
/// (asserted at the boundary — the <see cref="AdminAnalyticsControllerTests"/>
/// gate-attribute idiom), and the <c>/admin</c> index guest link is
/// hidden-not-disabled (a "string pin, no TestServer" — the
/// <see cref="BookmarksControllerTests"/> / <see cref="InventoryControllerTests"/>
/// precedent: no <c>WebApplicationFactory</c> in this suite).
/// <para>
/// The three GATE-3 / D6 Web-layer pins the design doc §7 + §9.7 names:
/// </para>
/// <list type="number">
/// <item><b>Save_Delegates_To_The_Single_Audited_Write_Lane</b> —
/// <c>POST /admin/guests</c> calls <see
/// cref="IIdentityService.SetGuestAccessAsync"/> exactly once with the composed
/// <see cref="GuestSurface"/> set (the checkboxes → the <c>[Flags]</c> value)
/// and the actor's <c>SubjectId</c> as <c>SetByAdmin</c>, then redirects to
/// <c>Index</c> with the subject id (C-M19·5, D6).</item>
/// <item><b>Controller_Carries_GlobalAdmin_Role_Authorize</b> — the gate is
/// enforced at the boundary (D6, C-M19·5): the controller carries
/// <c>[Authorize(Roles = GlobalAdmin)]</c>, so a non-GlobalAdmin actor is
/// challenged before reaching the action (the 403 / sign-in split is the
/// platform's authz-failure shape — the <see
/// cref="AdminAnalyticsControllerTests"/> idiom).</item>
/// <item><b>Admin_Index_Guest_Link_Hidden_For_NonGlobalAdmin</b> — the
/// <c>/admin</c> index guest affordance is hidden-not-disabled (the ADR 0050
/// "affordances hidden, not just gated" rule): the <c>admin.guests_title</c>
/// link is wrapped in <c>@if (KumunitaPrincipal.IsGlobalAdmin(User))</c>, so a
/// non-GlobalAdmin actor's <c>/admin</c> index renders no guest link.</item>
/// </list>
/// </summary>
public class AdminGuestsControllerTests
{
    private const string Admin = "admin-guests-001";
    private const string GuestSubject = "guest-subj-001";

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the redirect / the call log, not the bag
        }
    }

    private static (AdminGuestsController controller, IIdentityService identity) Build(
        GuestAccess? existing = null)
    {
        var identity = Substitute.For<IIdentityService>();
        identity.GetGuestAccessAsync(GuestSubject).Returns(existing);

        var controller = new AdminGuestsController(identity);

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, Roles.GlobalAdmin),
                },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, identity);
    }

    // ── Index (the C-M19·4 empty floor + the seed) ──────────────────────

    /// <summary>
    /// <c>GET /admin/guests?subjectId=…</c> with an existing standing: the form
    /// model is seeded from the read seam (<see cref="IIdentityService
    /// .GetGuestAccessAsync"/>) — the window + the three surface checkboxes
    /// decomposed from the <see cref="GuestAccess.AllowedSurfaces"/>
    /// <c>[Flags]</c> value (D4). The read seam is the sole reader (the
    /// controller does not <c>IDocumentSession</c> — D6 / C-M19·5).
    /// </summary>
    [Fact]
    public async Task Index_Seeds_From_Existing_Standing()
    {
        var existing = new GuestAccess
        {
            SubjectId = GuestSubject,
            ValidFrom = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            ValidUntil = new DateTimeOffset(2026, 10, 1, 0, 0, 0, TimeSpan.Zero),
            AllowedSurfaces = GuestSurface.Announcements | GuestSurface.Events,
            SetByAdmin = Admin,
        };
        var (controller, identity) = Build(existing);

        var action = await controller.Index(GuestSubject);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<GuestAdminViewModel>(view.ViewData.Model);

        Assert.Equal(GuestSubject, vm.SubjectId);
        Assert.True(vm.HasStanding);
        Assert.Equal(existing.ValidFrom, vm.ValidFrom);
        Assert.Equal(existing.ValidUntil, vm.ValidUntil);
        // The [Flags] value decomposes to the three checkboxes (D4).
        Assert.True(vm.Announcements);
        Assert.True(vm.Events);
        Assert.False(vm.Directory);
        Assert.True(vm.HasSurfaces);

        // The read seam was the sole reader (D6 / C-M19·5 — the controller does
        // not IDocumentSession; it reads through the frozen seam).
        await identity.Received(1).GetGuestAccessAsync(GuestSubject);
    }

    /// <summary>
    /// <c>GET /admin/guests?subjectId=…</c> with **no** settled standing: the
    /// form model is the C-M19·4 empty floor — an empty set, not an error
    /// (the view renders the <c>admin.guests_empty</c> state). The read seam
    /// returns null; the controller seeds the floor, not a 500.
    /// </summary>
    [Fact]
    public async Task Index_NoStanding_ViewsTheEmptyFloor()
    {
        var (controller, _) = Build(existing: null);

        var action = await controller.Index(GuestSubject);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<GuestAdminViewModel>(view.ViewData.Model);

        Assert.Equal(GuestSubject, vm.SubjectId);
        Assert.False(vm.HasStanding, "A guest with no settled standing must render the C-M19·4 empty floor, not an error.");
        Assert.False(vm.Announcements);
        Assert.False(vm.Events);
        Assert.False(vm.Directory);
        Assert.False(vm.HasSurfaces, "The empty floor (C-M19·4) carries no surface checkboxes.");
    }

    /// <summary>
    /// <c>GET /admin/guests</c> (no subject id — the create shape): the form
    /// model is empty, the read seam is **not** called (no subject to seed
    /// from), and the view renders the create shape (the
    /// <c>admin.guests_empty</c> state + the <c>admin.guests_create</c>
    /// button). The controller delegates the read to the seam only when a
    /// subject id is present (D6 / C-M19·5).
    /// </summary>
    [Fact]
    public async Task Index_NoSubjectId_ReturnsEmptyModel_DoesNotRead()
    {
        var (controller, identity) = Build();

        var action = await controller.Index(subjectId: null);
        var view = Assert.IsType<ViewResult>(action);
        var vm = Assert.IsType<GuestAdminViewModel>(view.ViewData.Model);

        Assert.Equal(string.Empty, vm.SubjectId);
        Assert.False(vm.HasStanding);
        Assert.False(vm.HasSurfaces);

        // No subject id → no read (the create shape does not seed).
        await identity.DidNotReceive().GetGuestAccessAsync(Arg.Any<string>());
    }

    // ── Save (GATE-3 — the single audited write lane) ────────────────────

    /// <summary>
    /// GATE-3 / D6 / C-M19·5 — <c>POST /admin/guests</c> delegates to the
    /// <b>single</b> audited write lane (<see cref="IIdentityService
    /// .SetGuestAccessAsync"/>), calling it exactly once with the composed
    /// <see cref="GuestSurface"/> set (the three checkboxes → the <c>[Flags]</c>
    /// value — D4) + the actor's <c>SubjectId</c> as <c>SetByAdmin</c>, then
    /// redirects to <c>Index</c> with the subject id (the "redirect after
    /// write" precedent — the ADR 0050 shape). The controller does not
    /// <c>IDocumentSession</c> (the <c>AccessAudit</c> row — <c>Via = Admin</c>,
    /// action <c>guest.set-standing</c> — is the service's, asserted in the
    /// Core tests).
    /// </summary>
    [Fact]
    public async Task Save_Delegates_To_The_Single_Audited_Write_Lane()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var (controller, identity) = Build();

        var action = await controller.Save(
            GuestSubject, from, until,
            announcements: true, events: true, directory: false);

        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminGuestsController.Index), redirect.ActionName);
        Assert.Equal(GuestSubject, redirect.RouteValues["subjectId"]);

        // The single audited write lane, called exactly once (C-M19·5). The
        // second argument is the actor's SubjectId (the admin who settled the
        // standing — the service sets SetByAdmin from it, U01's lane shape;
        // the object in flight carries SetByAdmin unset).
        await identity.Received(1).SetGuestAccessAsync(
            Arg.Is<GuestAccess>(g =>
                g.SubjectId == GuestSubject
                && g.ValidFrom == from
                && g.ValidUntil == until
                // The composed [Flags] set (D4) — Announcements | Events, no Directory.
                && g.AllowedSurfaces == (GuestSurface.Announcements | GuestSurface.Events)),
            Admin);
    }

    /// <summary>
    /// C-M19·4 — the closed surface set floors to nothing: a save with **no**
    /// surface checkbox checked composes <see cref="GuestSurface.None"/>
    /// (the empty floor — a guest with no surfaces is a signed-in shell, not
    /// an error). The write lane is still called exactly once (the floor is a
    /// valid, least-privileged state, not a rejection).
    /// </summary>
    [Fact]
    public async Task Save_NoSurfaces_Composes_None_Floor()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var until = new DateTimeOffset(2026, 9, 2, 0, 0, 0, TimeSpan.Zero);
        var (controller, identity) = Build();

        var action = await controller.Save(
            GuestSubject, from, until,
            announcements: false, events: false, directory: false);

        var redirect = Assert.IsType<RedirectToActionResult>(action);
        Assert.Equal(nameof(AdminGuestsController.Index), redirect.ActionName);

        await identity.Received(1).SetGuestAccessAsync(
            Arg.Is<GuestAccess>(g =>
                g.SubjectId == GuestSubject
                && g.AllowedSurfaces == GuestSurface.None),
            Admin);
    }

    /// <summary>
    /// C-M19·3 — the "when" is a bounded, ordered window: an inverted or empty
    /// window (<c>ValidUntil &lt;= ValidFrom</c>) is rejected at the boundary
    /// (<c>BadRequest</c>, the design doc §7 pin), and the write lane is
    /// **not** called (no standing is settled — the floor is not bypassed by
    /// an unbounded window). The C-M19·3 *Forbids* an unbounded guest
    /// standing; the boundary enforces it.
    /// </summary>
    [Fact]
    public async Task Save_InvertedWindow_Rejects_At_Boundary()
    {
        var (controller, identity) = Build();
        var bad = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        // validUntil == validFrom (empty window) → rejected.
        var emptyResult = await controller.Save(
            GuestSubject, bad, bad,
            announcements: true, events: false, directory: false);
        Assert.IsType<BadRequestResult>(emptyResult);

        // validUntil < validFrom (inverted window) → rejected.
        var invertedResult = await controller.Save(
            GuestSubject,
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero),
            bad,
            announcements: true, events: false, directory: false);
        Assert.IsType<BadRequestResult>(invertedResult);

        // No standing was settled (the write lane was never reached).
        await identity.DidNotReceive().SetGuestAccessAsync(Arg.Any<GuestAccess>(), Arg.Any<string>());
    }

    /// <summary>
    /// D6 / C-M19·5 — the gate is enforced at the boundary (the Web owns the
    /// standing check; the Core seam does not re-check <c>User</c> — the
    /// ADR 0050 split): the controller carries
    /// <c>[Authorize(Roles = GlobalAdmin)]</c>, so a non-GlobalAdmin actor is
    /// challenged (the 403 / sign-in split — the platform's authz-failure
    /// shape) before reaching the action. The <see
    /// cref="AdminAnalyticsControllerTests"/> gate-attribute idiom (no
    /// <c>WebApplicationFactory</c> in this suite — the pin is the
    /// attribute's presence, the house convention).
    /// </summary>
    [Fact]
    public void Controller_Carries_GlobalAdmin_Role_Authorize()
    {
        var attr = typeof(AdminGuestsController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        if (attr is null) return;
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr.Roles);
    }

    /// <summary>
    /// D6 / ADR 0050 "affordances hidden, not just gated" — the <c>/admin</c>
    /// index guest affordance is hidden-not-disabled: the
    /// <c>admin.guests_title</c> link (the <c>/admin/guests</c> card) is
    /// wrapped in <c>@if (KumunitaPrincipal.IsGlobalAdmin(User))</c>, so a
    /// non-GlobalAdmin actor's <c>/admin</c> index renders **no** guest link
    /// (the ADR 0050 rule). The house "string pin, no TestServer" idiom (the
    /// <see cref="BookmarksControllerTests"/> / <see
    /// cref="InventoryControllerTests"/> precedent — a pure markup pin, no
    /// <c>WebApplicationFactory</c> in this suite).
    /// </summary>
    [Fact]
    public void Admin_Index_Guest_Link_Hidden_For_NonGlobalAdmin()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Admin", "Index.cshtml");
        Assert.True(File.Exists(path), $"Views/Admin/Index.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not rendered
        // markup: the pin asserts on the shape the browser sees.
        var html = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // (1) The guest affordance is present — the /admin/guests card + the
        // admin.guests_title kw-l key (the U03 closed set).
        Assert.Contains("/admin/guests", html, StringComparison.Ordinal);
        Assert.Contains("key=\"admin.guests_title\"", html, StringComparison.Ordinal);

        // (2) The hidden-not-disabled gate (the ADR 0050 rule): the guest card
        // is wrapped in @if (KumunitaPrincipal.IsGlobalAdmin(User)) — a
        // non-GlobalAdmin actor's /admin index renders no guest link.
        Assert.Contains(
            "KumunitaPrincipal.IsGlobalAdmin(User)", html, StringComparison.Ordinal);

        // (3) The conditional wraps the guest card: the @if ... block that
        // precedes the /admin/guests link carries the IsGlobalAdmin guard.
        int ifIdx = html.IndexOf("KumunitaPrincipal.IsGlobalAdmin(User)", StringComparison.Ordinal);
        int linkIdx = html.IndexOf("/admin/guests", StringComparison.Ordinal);
        Assert.True(ifIdx >= 0 && linkIdx > ifIdx,
            "The guest link must appear inside the IsGlobalAdmin conditional (hidden-not-disabled).");
    }

    // ── The U03 kw-l closed set is present + non-empty (the view consumes
    //    it — U04 does not add a key, but the pin asserts the 8 admin
    //    surface keys the view binds are in the registry, all four
    //    languages — the parity closure the view depends on). ──────────────

    /// <summary>
    /// The §kw-l pin (U03's closed set, the 8 <c>admin.guests_*</c> keys the
    /// <c>/admin/guests</c> view consumes + the <c>/admin</c> index
    /// affordance): the <see cref="Kumunita.Core.Localization
    /// .KnownTranslationKeys.EnValues"/> registry contains every key the view
    /// binds, present + non-empty, in **all four** languages (en / de / fr /
    /// da). U04 *consumes* the keys (it does not add one); this pin closes
    /// the loop that the view's bindings resolve to a real registry entry
    /// (a missing / empty key would be a drift event the U03 parity pin
    /// enforces independently).
    /// </summary>
    [Fact]
    public void AdminGuests_Keys_ArePresent_And_NonEmpty_In_All_Four_Languages()
    {
        var adminGuestKeys = new[]
        {
            "admin.guests_title",
            "admin.guests_empty",
            "admin.guests_create",
            "admin.guests_window_label",
            "admin.guests_surfaces_label",
            "admin.guests_surface_announcements",
            "admin.guests_surface_events",
            "admin.guests_surface_directory",
            "admin.guests_saved",
        };

        var registries = new (string Name, IReadOnlyDictionary<string, string> Dict)[]
        {
            ("en", Kumunita.Core.Localization.KnownTranslationKeys.EnValues),
            ("de", Kumunita.Core.Localization.KnownTranslationKeys.DeValues),
            ("fr", Kumunita.Core.Localization.KnownTranslationKeys.FrValues),
            ("da", Kumunita.Core.Localization.KnownTranslationKeys.DaValues),
        };

        foreach (var (name, dict) in registries)
        {
            foreach (var key in adminGuestKeys)
            {
                Assert.True(dict.ContainsKey(key), $"'{name}' registry missing '{key}'.");
                Assert.False(string.IsNullOrWhiteSpace(dict[key]), $"The '{key}' value in '{name}' must be non-empty.");
            }
        }
    }

    // ── Harness ──────────────────────────────────────────────────────────

    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kumunita.slnx")))
                dir = dir.Parent;
            Assert.True(dir is not null, "Could not locate the repo root (Kumunita.slnx).");
            return dir!.FullName;
        }
    }
}
