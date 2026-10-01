using Kumunita.Core;
using Kumunita.Core.Identity;
using Kumunita.Core.Notifications;
using Kumunita.Core.UserInfo;
using Kumunita.Core.Localization;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using System.Security.Claims;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// M20 (ADR 0121, U06) — the 5th <c>/settings/quiet</c> resident section on
/// <see cref="LocaleController"/> (D7, the ADR 0019 time-zone lane verbatim:
/// owner-scope, no audit row, <c>clear=1</c> → <c>null</c>). The save lane is
/// driven through the <b>real</b> <see cref="NotificationService"/> (concrete
/// sealed — the <see cref="NotificationsControllerTests"/> real-service
/// precedent) over a live scratch-Postgres <see cref="IDocumentStore"/>
/// (<see cref="PostgresFixture"/>): the seam's <c>Store</c> / <c>Delete</c>
/// are Marten <b>extension</b> methods (static, not intercepted by
/// NSubstitute), so the observable effect is asserted by <b>reading back</b>
/// through the seam's own read lane (<c>GetQuietScheduleAsync</c>) — the
/// <see cref="NotificationsControllerTests"/> "assert against the live store"
/// idiom. Pins the unit plan's ≥3 pins:
/// <list type="number">
/// <item><b>Save/clear round-trip (C-M20·7, owner-scope)</b> — a signed-in
/// POST <c>/settings/quiet</c> with a schedule stores a
/// <see cref="NotificationQuietSchedule"/> for the <b>signed-in subject</b>
/// (the <c>RecipientId</c> pinned to the subject, the mode mapped to the
/// enum, the hours/days passed through); a POST with <c>clear=1</c> deletes
/// it (the read seam returns <c>null</c> = the never-quiet floor, C-M20·3).
/// The <c>TempData["info"]</c> flash is the registered
/// <c>settings.quiet.flash_saved</c> / <c>settings.quiet.flash_cleared</c>
/// value (the kw-l floor, ADR 0015 D1 — the provider is null in the harness,
/// so <see cref="LocaleController"/> resolves to the <c>EnValues</c> source
/// text). The recipient is always the signed-in subject, never a caller-
/// supplied id (C-M20·7).</item>
/// <item><b>No caller-supplied recipient (C-M20·7)</b> — the <c>SaveQuiet</c>
/// action signature has <b>no</b> recipient-id parameter; the recipient is
/// always <c>SubjectId(User)</c> (the <see cref="LocaleController
/// .SaveTimezone"/> shape verbatim).</item>
/// <item><b>kw-l closure witness (the M20 closed 15-key set, U00's design-
/// doc §10)</b> — the 11 <c>settings.quiet.*</c> resident + 4
/// <c>admin.quiet.*</c> admin keys are each present <b>non-empty</b> in all
/// four language dictionaries (en/de/fr/da). An explicit pin naming the 15
/// keys (the M19
/// <c>AdminGuests_Keys_ArePresent_And_NonEmpty_In_All_Four_Languages</c>
/// precedent) so the M20 closure is a first-class assertion, not an implicit
/// one (the <see cref="KwLRegistryConsistencyTests"/> /
/// <c>KnownTranslationKeys_ParityTests</c> pins auto-extend over
/// <c>AllKeys</c> and pin the same closure).</item>
/// </list>
/// </summary>
public class LocaleControllerQuietSectionTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private const string Subject = "subj-quiet-001";

    /// <summary>An in-memory <see cref="ITempDataProvider"/> — closes the
    /// <c>TempData</c> bag for the save-lane flash writes so the assertion can
    /// read <c>TempData["info"]</c> after the action returns (the
    /// <see cref="NotificationsControllerTests"/> /
    /// <see cref="SettingsSectionSplitTests"/> idiom).</summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { }
    }

    /// <summary>Boots a real Marten <see cref="IDocumentStore"/> over a fresh
    /// scratch Postgres database (the <see cref="NotificationsControllerTests"
    /// .BootStoreAsync"/> shape) with the M6 surface so the
    /// <see cref="NotificationQuietSchedule"/> table exists (its identity is
    /// pinned to <c>RecipientId</c> in <see cref="M6DocTypes"/>).</summary>
    private async Task<IDocumentStore> BootStoreAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var conn = await fixture.NewDatabaseAsync(ct);
        var store = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = "mt";
            opts.Storage.Add<KumunitaFeature>();
            M6DocTypes.Configure(opts);
        });
        await store.Storage.Database.ApplyAllConfiguredChangesToDatabaseAsync(null, null, ct);
        return store;
    }

    /// <summary>A read-side <see cref="NotificationService"/> over the same
    /// real store — the seam's own read lane (<c>GetQuietScheduleAsync</c>)
    /// is the assertion target (NSubstitute can't intercept the seam's
    /// <c>Store</c> / <c>Delete</c> extension methods, so the observable
    /// effect is read back through the store).</summary>
    private static NotificationService BuildReader(IDocumentStore store) =>
        new NotificationService(
            store,
            Substitute.For<IUserInfoService>(),
            Substitute.For<ITranslationProvider>(),
            Substitute.For<IMailerStage>());

    /// <summary>Builds a <see cref="LocaleController"/> wired to a
    /// <b>real</b> <see cref="NotificationService"/> over the live store (the
    /// save lane calls the U02 seam end-to-end). The other seams are plain
    /// NSubstitute stands-ins (the save lane never touches
    /// localization/userInfo/store-reads); the <see cref="LocaleController"
    /// .FlashAsync"/> provider is <c>null</c> so the flash resolves to the
    /// <c>EnValues</c> source text (the kw-l floor, ADR 0015 D1).</summary>
    private static LocaleController Build(IDocumentStore store, bool signedIn)
    {
        var localization = Substitute.For<ILocalizationService>();
        var userInfo = Substitute.For<IUserInfoService>();

        var notifications = new NotificationService(
            store,
            userInfo,
            Substitute.For<ITranslationProvider>(),
            Substitute.For<IMailerStage>());

        var controller = new LocaleController(
            localization, userInfo, store,
            translationProvider: null,
            notifications: notifications);

        var httpContext = new DefaultHttpContext();
        if (signedIn)
        {
            // The claim type is "Kumunita.Sub" (the repo's identity claim,
            // Kumunita.Core.Identity.ClaimTypes.Subject — NOT the BCL
            // System.Security.Claims.ClaimTypes.Subject; KumunitaPrincipal.
            // SubjectId reads c.Type == "Kumunita.Sub"). The
            // SettingsSectionSplitTests / UsageCaptureMiddlewareTests
            // precedent uses the literal.
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim("Kumunita.Sub", Subject) }, "test"));
        }
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return controller;
    }

    // ── Pin 1 — save/clear round-trip (owner-scope, C-M20·7) ──────────────

    /// <summary>
    /// A signed-in POST <c>/settings/quiet</c> with a schedule stores a
    /// <see cref="NotificationQuietSchedule"/> for the <b>signed-in subject</b>
    /// (never a caller-supplied id, C-M20·7). The real seam (over the live
    /// store) stores it with the <c>RecipientId</c> pinned to the subject, the
    /// mode mapped to <see cref="QuietScheduleMode.Blocked"/> (the "blocked"
    /// string), and the hours/days passed through — read back through the
    /// seam's own read lane. The <c>TempData["info"]</c> flash is the
    /// registered <c>settings.quiet.flash_saved</c> value.
    /// </summary>
    [Fact]
    public async Task SaveQuiet_WithSchedule_Stores_For_SignedIn_Subject_Flashes_Saved()
    {
        var store = await BootStoreAsync();
        var reader = BuildReader(store);
        var controller = Build(store, signedIn: true);

        // Floor: no schedule for this subject yet (never quiet, C-M20·3).
        Assert.Null(await reader.GetQuietScheduleAsync(Subject));

        var result = await controller.SaveQuiet(
            enabled: true,
            mode: "blocked",
            hours: new[] { 22, 23 },
            daysOfWeek: new[] { 0, 6 },
            clear: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.SettingsQuiet), redirect.ActionName);

        // The seam stored a schedule for the signed-in subject: RecipientId
        // pinned to the subject, mode Blocked, hours/days passed through.
        var loaded = await reader.GetQuietScheduleAsync(Subject);
        Assert.NotNull(loaded);
        Assert.Equal(Subject, loaded!.RecipientId);
        Assert.True(loaded.Enabled);
        Assert.Equal(QuietScheduleMode.Blocked, loaded.Mode);
        Assert.Equal(new[] { 22, 23 }, loaded.Hours);
        Assert.Equal(new[] { 0, 6 }, loaded.DaysOfWeek);

        // The flash is the registered settings.quiet.flash_saved en value.
        Assert.Equal(KnownTranslationKeys.EnValues["settings.quiet.flash_saved"],
            controller.TempData["info"]);
    }

    /// <summary>
    /// A signed-in POST <c>/settings/quiet</c> with <c>clear=1</c> deletes the
    /// subject's schedule (the "never quiet" floor, C-M20·3). A schedule is
    /// planted first so the delete is observable (a delete of an absent row is
    /// a no-op); after the call the seam's read lane returns <c>null</c>. The
    /// <c>TempData["info"]</c> flash is the registered
    /// <c>settings.quiet.flash_cleared</c> value.
    /// </summary>
    [Fact]
    public async Task SaveQuiet_WithClear_Deletes_For_SignedIn_Subject_Flashes_Cleared()
    {
        var store = await BootStoreAsync();
        var reader = BuildReader(store);
        var controller = Build(store, signedIn: true);

        // Plant a schedule so the clear is observable (an existing row is
        // what the seam deletes).
        await reader.SetQuietScheduleAsync(Subject, new NotificationQuietSchedule
        {
            RecipientId = Subject,
            Enabled = true,
            Mode = QuietScheduleMode.Blocked,
            Hours = new[] { 22, 23 },
            DaysOfWeek = new[] { 0, 6 },
        });
        Assert.NotNull(await reader.GetQuietScheduleAsync(Subject));

        var result = await controller.SaveQuiet(
            enabled: true,
            mode: "blocked",
            hours: new[] { 22, 23 },
            daysOfWeek: new[] { 0, 6 },
            clear: "1");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.SettingsQuiet), redirect.ActionName);

        // The seam deleted the subject's schedule — the read lane returns null
        // (the floor, C-M20·3).
        Assert.Null(await reader.GetQuietScheduleAsync(Subject));

        Assert.Equal(KnownTranslationKeys.EnValues["settings.quiet.flash_cleared"],
            controller.TempData["info"]);
    }

    /// <summary>
    /// A signed-out POST <c>/settings/quiet</c> fails closed (the
    /// <see cref="LocaleController.SaveTimezone"/> shape, C-M20·7): the subject
    /// is <c>null</c>, so the action returns a redirect before any seam call —
    /// the subject's schedule is still absent (nothing written).
    /// </summary>
    [Fact]
    public async Task SaveQuiet_SignedOut_Redirects_Writing_Nothing()
    {
        var store = await BootStoreAsync();
        var reader = BuildReader(store);
        var controller = Build(store, signedIn: false);

        // Floor: no schedule for this subject.
        Assert.Null(await reader.GetQuietScheduleAsync(Subject));

        var result = await controller.SaveQuiet(
            enabled: true,
            mode: "blocked",
            hours: new[] { 22, 23 },
            daysOfWeek: new[] { 0, 6 },
            clear: null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(LocaleController.SettingsQuiet), redirect.ActionName);

        // Signed out → no subject → the seam was never invoked (nothing
        // written; the subject's schedule is still the floor).
        Assert.Null(await reader.GetQuietScheduleAsync(Subject));
    }

    // ── Pin 2 — no caller-supplied recipient (C-M20·7) ────────────────────

    /// <summary>
    /// The <c>SaveQuiet</c> action signature has <b>no</b> recipient-id
    /// parameter (C-M20·7): the recipient is always
    /// <c>SubjectId(User)</c>, never a caller-supplied id (the
    /// <see cref="LocaleController.SaveTimezone"/> shape verbatim — a resident
    /// cannot target another resident's schedule). Assert the action's
    /// parameter set is exactly the closed form fields
    /// (<c>enabled</c>, <c>mode</c>, <c>hours</c>, <c>daysOfWeek</c>,
    /// <c>clear</c>) and that none of the recipient-id spellings appear.
    /// (xunit.v3 routes a 3rd arg to a comparer, not a message, so these
    /// assertions carry no message.)
    /// </summary>
    [Fact]
    public void SaveQuiet_Action_Has_No_CallerSupplied_Recipient_Parameter()
    {
        var action = typeof(LocaleController).GetMethod(nameof(LocaleController.SaveQuiet))!;
        var paramNames = action.GetParameters().Select(p => p.Name!.ToLowerInvariant()).ToList();

        // The closed parameter set — the form fields only (no recipient id).
        Assert.Equal(
            new[] { "enabled", "mode", "hours", "daysofweek", "clear" },
            paramNames);

        // Explicit negative pin: none of the recipient-id spellings.
        foreach (var forbidden in new[] { "recipientid", "recipient", "id", "userid", "subjectid", "subject" })
        {
            Assert.DoesNotContain(forbidden, paramNames);
        }
    }

    // ── Pin 3 — kw-l closure witness (the M20 closed 15-key set) ──────────

    /// <summary>
    /// The M20 closed 15-key <c>kw-l</c> set (U00's design-doc §10 table —
    /// 11 <c>settings.quiet.*</c> resident + 4 <c>admin.quiet.*</c> admin
    /// keys) is each present <b>non-empty</b> in all four language
    /// dictionaries (en / de / fr / da). The <see
    /// cref="KwLRegistryConsistencyTests"/> + <c>KnownTranslationKeys_Parity
    /// Tests</c> pins auto-extend over <c>AllKeys</c> and pin the same
    /// closure; this pin names the 15 keys directly (the M19
    /// <c>AdminGuests_Keys_ArePresent_And_NonEmpty_In_All_Four_Languages</c>
    /// precedent) so the M20 closure is a first-class assertion, not an
    /// implicit one.
    /// </summary>
    [Fact]
    public void M20_QuietKeys_ArePresent_And_NonEmpty_In_All_Four_Languages()
    {
        var m20QuietKeys = new[]
        {
            // Resident (11) — settings.quiet.*
            "settings.quiet.title",
            "settings.quiet.description",
            "settings.quiet.enabled",
            "settings.quiet.mode_label",
            "settings.quiet.mode_blocked",
            "settings.quiet.mode_allowed",
            "settings.quiet.hours_label",
            "settings.quiet.days_label",
            "settings.quiet.save",
            "settings.quiet.flash_saved",
            "settings.quiet.flash_cleared",
            // Admin (4) — admin.quiet.*
            "admin.quiet.title",
            "admin.quiet.cadence_label",
            "admin.quiet.save",
            "admin.quiet.flash_saved",
        };
        // The M20 closed kw-l set is 15 keys (11 resident + 4 admin).
        Assert.Equal(15, m20QuietKeys.Length);

        var registries = new (string Name, IReadOnlyDictionary<string, string> Dict)[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };

        foreach (var (name, dict) in registries)
        {
            foreach (var key in m20QuietKeys)
            {
                Assert.True(dict.ContainsKey(key),
                    $"'{name}' registry missing '{key}' (the M20 closed 15-key set must be present in all four languages).");
                Assert.False(string.IsNullOrWhiteSpace(dict[key]),
                    $"The '{key}' value in '{name}' must be non-empty (the M20 closed 15-key set).");
            }
        }
    }
}
