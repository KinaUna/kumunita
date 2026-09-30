using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Events;
using Kumunita.Core.Localization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Localization;
using Kumunita.Web.Models;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// U05 (M18, ADR 0119, D2/D7/D3) — the composer recurrence picker's
/// Web-side contract. Mirrors the <see cref="EventControllerTests"/>
/// harness shape (NSubstitute + <see cref="DefaultHttpContext"/>, no
/// TestServer):
/// <list type="number">
/// <item><b>Create mapping</b> — a weekly picker POST maps onto the
///       <see cref="CreateEventRequest.Recurrence"/> field the U02
///       create lane reads: <see cref="Recurrence.Weekly"/> /
///       <c>Interval: 1</c> / <c>Count: 4</c> / <c>Ends: null</c>.
///       The zero-change branch ("none") posts <c>null</c> (GATE-2's
///       <c>Create_With_No_Rule_Behaves_Exactly_As_Today</c> pin).</item>
/// <item><b>Edit hide</b> — the edit form's picker is wrapped in
///       <c>@if (!Model.IsNonHeadOccurrence)</c> so a non-head
///       occurrence (D3) renders **no** picker markup. The house
///       "string pin, no TestServer" idiom (the
///       <see cref="BookmarkButtonTests"/> precedent): read the
///       <c>Edit.cshtml</c> file, verify the guard structure.</item>
/// </list>
/// </summary>
public class EventComposerRecurrenceTests
{
    // ── 1 — Composer_Submits_Weekly_Picker_Maps_Onto_CreateEventRequest ──────

    /// <summary>
    /// <c>POST /events/new</c> with the recurrence picker set to
    /// <c>weekly</c> / <c>Interval: 1</c> / <c>EndsAfterCount: 4</c>:
    /// the controller maps the picker fields onto the
    /// <see cref="CreateEventRequest.Recurrence"/> field (the U02
    /// create lane's field) — <see cref="Recurrence.Weekly"/> /
    /// <c>Interval: 1</c> / <c>Count: 4</c> / <c>Ends: null</c>.
    /// A mapping drift (a dropped <c>Count</c>, a missing
    /// <c>Interval</c>, a swapped <c>Recurrence</c> enum) is a
    /// single-source break: the composer is the *only* recurrence
    /// write surface (D2 / D7).
    /// </summary>
    [Fact]
    public async Task Composer_Submits_Weekly_Picker_Maps_Onto_CreateEventRequest()
    {
        var created = new Event
        {
            Id = "ev-weekly-001",
            Title = "Weekly cleanup",
            Body = "Bring gloves.",
            AuthorId = "subj-resident-001",
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            Created = DateTimeOffset.UtcNow,
        };

        CreateEventRequest? captured = null;
        var events = Substitute.For<IEventService>();
        events.CreateAsync(Arg.Any<string>(), Arg.Any<CreateEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { captured = call.ArgAt<CreateEventRequest>(1); return created; });

        var model = new EventEditorModel
        {
            Title = "Weekly cleanup",
            Body = "Bring gloves. Meet at the common shed.",
            Start = new DateTime(2026, 10, 1, 9, 0, 0),
            End = new DateTime(2026, 10, 1, 13, 0, 0),
            SaveAsDraft = true,
            ReminderEnabled = true,
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                CommunityVisible = true,
            },
            // M18 (ADR 0119, D2/D7) — the recurrence picker:
            // weekly, interval 1, ends after 4 occurrences.
            Recurrence = "weekly",
            RecurrenceInterval = 1,
            EndsAfterCount = 4,
        };

        var controller = BuildController(events, subjectId: "subj-resident-001");

        var result = await controller.CreatePost(model);

        Assert.IsType<RedirectResult>(result);
        await events.Received(1).CreateAsync(
            "subj-resident-001", Arg.Any<CreateEventRequest>(), Arg.Any<CancellationToken>());
        Assert.NotNull(captured);

        // The weekly rule maps onto the U02 create lane's Recurrence field.
        Assert.NotNull(captured!.Recurrence);
        Assert.Equal(Recurrence.Weekly, captured.Recurrence!.Recurrence);
        Assert.Equal(1, captured.Recurrence.Interval);
        Assert.Equal(4, captured.Recurrence.Count);
        Assert.Null(captured.Recurrence.Ends);
    }

    // ── 2 — Composer_Edit_Hides_Picker_On_A_NonHead_Row ───────────────────────

    /// <summary>
    /// <c>GET /events/{id}/edit</c> for a **non-head** occurrence (D3 —
    /// a non-head row's <see cref="Event.RecurrenceRule"/> is
    /// <c>null</c>; only the head carries the rule): the
    /// <see cref="EventEditorModel.IsNonHeadOccurrence"/> flag is
    /// <c>true</c>, and the <c>Edit.cshtml</c> picker is wrapped in
    /// <c>@if (!Model.IsNonHeadOccurrence)</c> so a non-head row
    /// renders **no** picker markup (the §drift-guard's "no
    /// per-occurrence override" pin, enforced at the Web layer).
    /// <para>
    /// Two pins:
    /// (1) the **behavioral** pin — a non-head row's model has
    ///     <c>IsNonHeadOccurrence = true</c>;
    /// (2) the **structural** pin — the <c>Edit.cshtml</c> file
    ///     wraps the picker in the <c>@if</c> guard (the house
    ///     "string pin, no TestServer" idiom — the
    ///     <see cref="BookmarkButtonTests"/> precedent).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Composer_Edit_Hides_Picker_On_A_NonHead_Row()
    {
        // (1) Behavioral — the controller sets IsNonHeadOccurrence
        //     = true on a non-head row (RecurrenceHeadId is non-null).
        const string id = "ev-non-head-001";
        var nonHead = new Event
        {
            Id = id,
            Title = "Weekly cleanup (week 3)",
            Body = "Bring gloves.",
            AuthorId = "subj-author-001",
            Start = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.Zero),
            Created = DateTimeOffset.UtcNow,
            RecurrenceHeadId = "ev-head-001", // non-null → non-head row (D3).
        };

        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(nonHead);

        var controller = BuildController(events,
            roles: new[] { Kumunita.Core.Identity.Roles.GlobalAdmin },
            subjectId: "subj-author-001");

        var result = await controller.EditGet(id);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EventEditorModel>(view.ViewData.Model);

        // The controller sets IsNonHeadOccurrence from the row's
        // RecurrenceHeadId (D3 — a non-head row's rule is null; the
        // head carries the rule).
        Assert.True(model.IsNonHeadOccurrence,
            "A non-head row (RecurrenceHeadId non-null) must set IsNonHeadOccurrence = true (D3).");

        // (2) Structural — the Edit.cshtml wraps the picker in
        //     @if (!Model.IsNonHeadOccurrence) (the house "string pin,
        //     no TestServer" idiom — the BookmarkButtonTests
        //     precedent). On a non-head row the guard renders nothing.
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Event", "Edit.cshtml");
        Assert.True(File.Exists(path), $"Views/Event/Edit.cshtml not found at {path}.");

        // Strip Razor comments — they are author documentation, not
        // rendered markup.
        var html = System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        // The @if guard is present (the D3 / F1 pin — the §drift-guard's
        // "no per-occurrence override" pin).
        Assert.Contains("if (!Model.IsNonHeadOccurrence)", html);

        // The picker's form fields are inside the guard (the D7
        // composer shape — the Recurrence select, the Interval
        // number, the EndsAfterCount / EndsOnDate inputs).
        Assert.Contains("name=\"Recurrence\"", html);
        Assert.Contains("name=\"RecurrenceInterval\"", html);
        Assert.Contains("name=\"EndsAfterCount\"", html);
        Assert.Contains("name=\"EndsOnDate\"", html);

        // The D9 kw-l key names are present (the key *values* land
        // in U07 — this unit references the key names only).
        Assert.Contains("events.recurrence.none", html);
        Assert.Contains("events.recurrence.weekly", html);
        Assert.Contains("events.recurrence.interval", html);
        Assert.Contains("events.recurrence.ends_after", html);
        Assert.Contains("events.recurrence.ends_on", html);
    }

    // ── 3 (bonus) — zero-change branch ────────────────────────────────────────

    /// <summary>
    /// <c>POST /events/new</c> with <c>Recurrence = "none"</c> (the
    /// default): the controller maps the picker to
    /// <see cref="CreateEventRequest.Recurrence"/> as <c>null</c> —
    /// the zero-change branch (GATE-2's
    /// <c>Create_With_No_Rule_Behaves_Exactly_As_Today</c> pin: a
    /// <c>Recurrence.None</c> / null rule creates a single row,
    /// <c>RecurrenceHeadId: null</c>, <c>RecurrenceRule: null</c>).
    /// </summary>
    [Fact]
    public async Task Composer_Submits_None_Picker_Maps_To_NullRecurrence()
    {
        var created = new Event
        {
            Id = "ev-none-001",
            Title = "One-off cleanup",
            Body = "Bring gloves.",
            AuthorId = "subj-resident-001",
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            Created = DateTimeOffset.UtcNow,
        };

        CreateEventRequest? captured = null;
        var events = Substitute.For<IEventService>();
        events.CreateAsync(Arg.Any<string>(), Arg.Any<CreateEventRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { captured = call.ArgAt<CreateEventRequest>(1); return created; });

        var model = new EventEditorModel
        {
            Title = "One-off cleanup",
            Body = "Bring gloves.",
            Start = new DateTime(2026, 10, 1, 9, 0, 0),
            End = new DateTime(2026, 10, 1, 13, 0, 0),
            SaveAsDraft = true,
            ReminderEnabled = true,
            Audience = new AudienceEditorModel
            {
                Mode = "Any",
                Grants = "[]",
                CommunityVisible = true,
            },
            // M18 (ADR 0119, D2) — the "none" default: a single,
            // non-recurring event (the zero-change branch).
            Recurrence = "none",
            RecurrenceInterval = 1,
        };

        var controller = BuildController(events, subjectId: "subj-resident-001");

        var result = await controller.CreatePost(model);

        Assert.IsType<RedirectResult>(result);
        Assert.NotNull(captured);
        // "none" maps to a null Recurrence field (the zero-change
        // branch — the U02 create lane treats null as "no
        // recurrence", creating a single row).
        Assert.Null(captured!.Recurrence);
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds an <see cref="EventController"/> over NSubstitute seams
    /// (the <see cref="EventControllerTests.Build"/> shape): the
    /// <see cref="IDocumentStore"/> is a plain substitute (the M4
    /// controller never opens a session — the service owns its own
    /// sessions, C3), and a no-op <see
    /// cref="ITempDataProvider"/> closes the <c>TempData</c> bag so
    /// the write lanes' success branches don't NRE.
    /// </summary>
    private static EventController BuildController(
        IEventService events,
        string[]? roles = null,
        string? subjectId = null)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetComponentsAsync(true).Returns(new List<Component>());
        userInfo.GetProfilesAsync(true).Returns(new List<Profile>());
        userInfo.GetPublicGroupsAsync().Returns(new List<Group>());

        var localization = Substitute.For<ILocalizationService>();
        localization.ListLanguagesAsync().Returns(new List<LanguageCatalog>());
        localization.GetDefaultLanguageCodeAsync().Returns("en");
        localization.GetDefaultTimezoneAsync().Returns("UTC");

        var timezone = new EffectiveTimezoneResolver(userInfo, localization, new HttpContextAccessor());

        var controller = new EventController(events, userInfo, localization, Substitute.For<IDocumentStore>(), timezone);
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var claims = new List<Claim>();
        if (subjectId is not null)
            claims.Add(new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, subjectId));
        if (roles is { Length: > 0 })
            claims.AddRange(roles.Select(r => new Claim(Kumunita.Core.Identity.ClaimTypes.Role, r)));

        if (claims.Count > 0)
            controller.ControllerContext.HttpContext.User =
                new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));

        controller.TempData = new TempDataDictionary(new DefaultHttpContext(), new NoOpTempDataProvider());
        return controller;
    }

    /// <summary>
    /// Walks up from the test assembly's output dir to the dir
    /// holding <c>Kumunita.slnx</c> (the repo root).
    /// </summary>
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

    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values) { /* no-op */ }
    }
}
