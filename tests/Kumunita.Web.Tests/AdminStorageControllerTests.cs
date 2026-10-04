using Kumunita.Core.Media;
using Kumunita.Core.Usage;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Claim = System.Security.Claims.Claim;
using ClaimsIdentity = System.Security.Claims.ClaimsIdentity;
using ClaimsPrincipal = System.Security.Claims.ClaimsPrincipal;

namespace Kumunita.Web.Tests;

/// <summary>
/// The 2 pinned admin Web tests for <see cref="AdminStorageController"/>
/// (M25, the storage-**settings** set-lane — U6) — the design doc §2.4 list,
/// items 20–21, verbatim:
/// <see cref="AdminStorage_SetLimits_Persists"/> (C-UP·1) and
/// <see cref="AdminStorage_NonGlobalAdmin_Forbidden"/> (C-UP·1).
/// <para>
/// Harness: the project's standing <b>direct-construction</b> idiom
/// (NSubstitute seams + <see cref="DefaultHttpContext"/> — the shape
/// <see cref="AdminStorageMetricsControllerTests"/> and
/// <see cref="AdminAnalyticsControllerTests"/> use), <b>not</b> a full host
/// boot. <see cref="IStorageSettingsService"/> (the <c>SetAsync</c> seam) is
/// NSubstituted (an interface — it proxies cleanly), and
/// <see cref="IDocumentStore"/> is NSubstituted with a
/// <see cref="IDocumentSession"/> stand-in for the controller's
/// <c>store.LightweightSession()</c> (C3 — the controller owns the session and
/// passes it to the service, the "one in-caller-session write" pin, C-UP·1).
/// <b>No Postgres</b> (the <c>SetAsync</c> seam's Postgres behaviour — the
/// "stored doc reflects them" side — is pinned by U4's
/// <c>SetAsync_PersistsInCallerSession</c> Core test; these 2 pin the Web
/// surface only: the values handed to the seam + the role gate).
/// </para>
/// <para>
/// <b>Route / value-semantics (C-UP·5):</b> the set-lane is
/// <c>POST /admin/storage/settings</c> (the option-A route recorded in the U5
/// handoff — one controller / one route / one view with the U5 <c>GET</c>;
/// <b>not</b> the <c>POST /admin/storage/limits</c> the unit plan originally
/// named). A <b>blank</b> per-file limit ⇒ <c>null</c> (the env
/// <see cref="MediaOptions.MaxBytes"/> fallback is in force) — blank is
/// <b>not</b> coerced to <c>0</c>; a <c>0</c> per-user quota ⇒ <b>unlimited</b>
/// (the sentinel). The subject is minted server-side from the signed-in
/// principal (never a path param).
/// </para>
/// </summary>
public class AdminStorageControllerTests
{
    private const string Admin = "m25-admin-001";

    // ── The 2 C-UP·1 pins (design §2.4, items 20–21) ────────────────────────

    [Fact]
    public async Task AdminStorage_SetLimits_Persists()
    {
        var (controller, settings, store, session) =
            Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);

        // The C3 "one in-caller-session write" (C-UP·1): the controller opens
        // its own session and hands it to the seam. The stand-in session is
        // what store.LightweightSession() returns, so Received(…session) also
        // pins that the controller used the caller's session (not a fresh one
        // inside the service).
        store.LightweightSession().Returns(session);

        // A GlobalAdmin POST sets both values: per-file override 5 MiB +
        // per-user quota 2 MiB (concrete, non-sentinel figures so the
        // "stored doc reflects them" side is unambiguous). The seam is called
        // with the byte equivalents, the actor minted from the principal, and
        // the caller's session.
        var result = await controller.Save("5", "2");

        // Redirect back to the GET view (the AdminMessagingController.Save /
        // AdminTimezoneController.Save redirect-after-save shape).
        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(AdminStorageController.Index), redirect.ActionName);

        // The single write lane (C-UP·1) is the seam — called exactly once with
        // the admin values (5 MiB → 5242880 bytes, 2 MiB → 2097152 bytes),
        // the server-minted actor, and the caller's session.
        await settings.Received(1)
            .SetAsync(5 * 1024 * 1024L, 2 * 1024 * 1024L, Admin, session);

        // The controller opened its session exactly once (one in-caller-session
        // write; no second session, no re-read in this lane).
        store.Received(1).LightweightSession();

        // The read seam (U5's GET) is untouched by the set-lane.
        await settings.DidNotReceiveWithAnyArgs().GetOrCreateAsync(default);
    }

    [Fact]
    public async Task AdminStorage_SetLimits_BlankPerFile_IsNull()
    {
        // C-UP·5 (blank per-file ⇒ null, env fallback) — the guard the rules
        // single out: a blank per-file field must NOT be coerced to 0 (0 means
        // "unlimited file size"). Blank per-file + quota 0 (unlimited) is the
        // sentinel pair, so this also pins the "0 quota ⇒ unlimited" arm on
        // the same seam call. (A supporting pin for item 20 — the same seam,
        // the sentinel branch.)
        var (controller, settings, store, session) =
            Build(role: Kumunita.Core.Identity.Roles.GlobalAdmin);
        store.LightweightSession().Returns(session);

        var result = await controller.Save(string.Empty, "0");
        Assert.IsType<RedirectToActionResult>(result);

        // Blank per-file ⇒ null (not 0); quota "0" ⇒ 0 (unlimited sentinel).
        await settings.Received(1)
            .SetAsync((long?)null, 0L, Admin, session);
    }

    [Fact]
    public void AdminStorage_NonGlobalAdmin_Forbidden()
    {
        // The C-UP·1 gate is the [Authorize(Roles = GlobalAdmin)] attribute on
        // the controller type — the M24 AdminStorageMetricsControllerTests
        // idiom (asserting the role set is GlobalAdmin exactly): a non-
        // GlobalAdmin never reaches the action, so the 403 is the attribute's
        // effect, not an action-level re-check. (No host boot in this harness —
        // the gate is the role.)
        var attr = typeof(AdminStorageController)
            .GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(Kumunita.Core.Identity.Roles.GlobalAdmin, attr!.Roles);
        Assert.DoesNotContain(Kumunita.Core.Identity.Roles.Member,
            (attr!.Roles ?? string.Empty).Split(',', StringSplitOptions.TrimEntries));

        // Concretely: a signed-in Member's principal does not carry the
        // GlobalAdmin role (the role check the authorization policy runs), so
        // their POST/GET is rejected before the action body runs.
        var member = BuildPrincipal(Kumunita.Core.Identity.Roles.Member);
        Assert.False(Kumunita.Web.Security.KumunitaPrincipal.IsGlobalAdmin(member));
    }

    // ── Harness ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Build the controller with NSubstituted <see cref="IStorageSettingsService"/>
    /// + <see cref="IStorageMetricsService"/> + <see cref="IDocumentStore"/> over a
    /// <see cref="DefaultHttpContext"/> whose <see cref="HttpContext.User"/> carries
    /// an authenticated principal in <paramref name="role"/> (the
    /// <c>ClaimTypes.Subject</c> claim <see cref="Admin"/>). <see
    /// cref="Kumunita.Web.Security.KumunitaPrincipal.SubjectId"/> reads the
    /// <c>"Kumunita.Sub"</c> claim (<see cref="Kumunita.Core.Identity.ClaimTypes.Subject"/>),
    /// so the actor is <see cref="Admin"/>. The U6 controller takes the settings
    /// seam + the metrics seam + <see cref="IOptions{MediaOptions}"/> +
    /// <see cref="IDocumentStore"/> as constructor deps (C-UP·1/3; C3 session
    /// ownership) — the <c>LightweightSession()</c> stand-in is supplied per-test.
    /// </summary>
    private static (AdminStorageController controller, IStorageSettingsService settings,
        IDocumentStore store, IDocumentSession session) Build(string role)
    {
        var settings = Substitute.For<IStorageSettingsService>();
        var metrics  = Substitute.For<IStorageMetricsService>();
        var store    = Substitute.For<IDocumentStore>();
        var session  = Substitute.For<IDocumentSession>();

        // The env fallback cap (MediaOptions.MaxBytes default 10 MiB) — the
        // controller reads it only on the GET path; the set-lane ignores it.
        var mediaOpts = Options.Create(new MediaOptions());

        var controller = new AdminStorageController(settings, metrics, mediaOpts, store);

        var httpContext = new DefaultHttpContext();
        httpContext.User = BuildPrincipal(role);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // A directly-constructed controller has no TempData; the Save lane sets
        // TempData["info"] on success (the redirect-after-save shape). Supply a
        // dictionary with a no-op provider (the AdminDateFormatControllerTests
        // house pattern) so that write does not NRE in the test harness.
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());

        return (controller, settings, store, session);
    }

    private static ClaimsPrincipal BuildPrincipal(string role) =>
        new ClaimsPrincipal(
            new ClaimsIdentity(
                new[]
                {
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, Admin),
                    new Claim(Kumunita.Core.Identity.ClaimTypes.Role, role),
                },
                authenticationType: "test"));

    /// <summary>
    /// No-op <see cref="ITempDataProvider"/> (the <see
    /// cref="AdminDateFormatControllerTests"/> house pattern): the assertion
    /// target is the redirect / the <c>SetAsync</c> call log, not the bag, so
    /// the provider loads an empty dictionary and drops saved values.
    /// </summary>
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
