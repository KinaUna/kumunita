using System.Security.Claims;
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
/// U06 (M18, ADR 0119, D7 / F2 / F3 / C-M18·4) — the event detail page's
/// <b>series chip</b> + the <b>author's skip / restore buttons</b> Web-boundary
/// seam tests. Mirrors the <see cref="EventComposerRecurrenceTests"/> /
/// <see cref="BookmarkButtonTests"/> harness shape (NSubstitute +
/// <see cref="DefaultHttpContext"/>, **no** TestServer):
/// <list type="number">
/// <item><b>Head row</b> — <c>GET /events/{id}</c> for a series <b>head</b>
///       (the row whose <see cref="Event.RecurrenceRule"/> is non-null, D3):
///       the model carries the series-shape flags (<c>IsPartOfSeries</c> /
///       <c>IsHead</c> = true, <c>IsNonHead</c> = false, <c>Rule</c> = the
///       head's own rule), and the <c>Detail.cshtml</c> renders the
///       <c>events.series.repeats</c> chip on the head but gates the
///       skip / restore buttons behind <c>@if (Model.IsNonHead &amp;&amp;
///       Model.CanSkipOrUndelete)</c> — so a head row shows the chip but
///       <b>neither</b> button (F3 — the head is edited via the existing edit
///       lane, D4; the §drift-guard's D5 "no delete-entire-series button" pin).</item>
/// <item><b>Non-head row (author)</b> — <c>GET /events/{id}</c> for a non-head
///       <b>sibling</b> (the row whose <see cref="Event.RecurrenceHeadId"/> is
///       non-null): the model carries <c>IsNonHead</c> = true + the chip's
///       <c>Rule</c> resolved from the <b>head</b> row (a single
///       <see cref="IEventService.GetAsync"/> on <c>RecurrenceHeadId</c>, F2 /
///       C-M18·1 — the read seams stay concrete-only), <c>CanSkipOrUndelete</c>
///       = true for the author (C-M18·4 — the existing
///       <c>CheckEditStanding</c> standing), and the <c>Detail.cshtml</c>
///       carries the <c>/events/{id}/skip</c> form + the
///       <c>events.series.part_of</c> chip (F2 / F3).</item>
/// </list>
/// <para>
/// The two pins are the house "string pin, <b>no</b> TestServer" idiom (the
/// <see cref="BookmarkButtonTests"/> precedent): (1) a <b>behavioral</b> pin —
/// drive the detail lane's <c>GET</c> through the controller and assert the
/// <see cref="EventDetailViewModel"/> carries the correct series-shape flags;
/// (2) a <b>structural</b> pin — read the shipped <c>Detail.cshtml</c> and
/// assert the chip / button markup is present and the button is guarded by
/// <c>Model.IsNonHead</c> (a head row renders the chip but no button).
/// </para>
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test (the
/// seam's read / write decisions are the seam's; this layer pins the
/// controller's flag-setting + the view's structural shape, not the seam's
/// gate). The <b>skip / restore write lanes</b> (POST) ride the frozen U03
/// seams (<see cref="IEventService.SkipOccurrenceAsync"/> /
/// <see cref="IEventService.UndeleteOccurrenceAsync"/>); their standing +
/// head-row guard + no-leak 404 split are the <i>seam's</i> pins, not this
/// layer's (C-M18·4 / C-M18·5 / D5).
/// </para>
/// </summary>
public sealed class EventDetailRecurrenceTests
{
    // ── 1 — Detail_Head_Row_Shows_Series_Chip_But_No_Skip_Button ─────────────

    /// <summary>
    /// <c>GET /events/{id}</c> for a series <b>head</b> row (D3 — the row whose
    /// <see cref="Event.RecurrenceRule"/> is non-null), author viewing:
    /// <para>
    /// <b>Behavioral pin</b> — the <see cref="EventDetailViewModel"/> carries
    /// <c>IsPartOfSeries = true</c>, <c>IsHead = true</c>,
    /// <c>IsNonHead = false</c>, and <c>Rule</c> = the head's own rule (F2 —
    /// the head reads its own <c>RecurrenceRule</c>, no
    /// <see cref="IEventService.GetAsync"/> head-lookup needed).
    /// </para>
    /// <para>
    /// <b>Structural pin</b> — the shipped <c>Detail.cshtml</c> renders the
    /// <c>events.series.repeats</c> chip (the F2 series chip) but gates the
    /// skip / restore buttons behind <c>@if (Model.IsNonHead &amp;&amp;
    /// Model.CanSkipOrUndelete)</c>: on a head row <c>IsNonHead</c> is
    /// <c>false</c>, so the head shows the chip but <b>neither</b> button
    /// (F3 — the head is edited via the existing edit lane, D4; the
    /// §drift-guard's D5 "no delete-entire-series button" pin).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Detail_Head_Row_Shows_Series_Chip_But_No_Skip_Button()
    {
        const string id = "ev-head-001";
        const string author = "subj-author-001";
        var head = new Event
        {
            Id = id,
            Title = "Weekly cleanup (head)",
            Body = "Bring gloves.",
            AuthorId = author,
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            Created = DateTimeOffset.UtcNow,
            // D3 — the head carries the rule; RecurrenceHeadId is null.
            RecurrenceRule = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 4,
            },
        };

        var events = Substitute.For<IEventService>();
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(head);

        var controller = BuildController(events, subjectId: author);

        var result = await controller.Detail(id);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EventDetailViewModel>(view.ViewData.Model);

        // (1) Behavioral — the controller sets the series-shape flags from
        //     the head row (D3 / F2). The head reads its OWN rule; no
        //     GetAsync head-lookup is needed for the chip on a head row.
        Assert.True(model.IsPartOfSeries,
            "A head row (RecurrenceRule non-null) must be part of a series (D3 / F2).");
        Assert.True(model.IsHead,
            "A head row (RecurrenceRule non-null) must set IsHead = true (D3).");
        Assert.False(model.IsNonHead,
            "A head row (RecurrenceHeadId null) must NOT set IsNonHead (D3).");
        Assert.NotNull(model.Rule);
        Assert.Equal(Recurrence.Weekly, model.Rule!.Recurrence);

        // (2) Structural — the Detail.cshtml renders the events.series.repeats
        //     chip (the F2 series chip) …
        var html = ReadDetailCshtml();
        Assert.True(html.Contains("events.series.repeats"),
            "The F2 series chip (events.series.repeats) must be present in Detail.cshtml.");
        Assert.True(html.Contains("events.recurrence.count"),
            "The chip's Count suffix (events.recurrence.count) must be present (D9).");

        // … but the skip / restore buttons are guarded behind Model.IsNonHead,
        //     so a head row (IsNonHead == false) renders the chip but NEITHER
        //     button (F3 — the head is edited via the edit lane, D4; the
        //     §drift-guard's "no delete-entire-series button" pin).
        Assert.True(html.Contains("if (Model.IsNonHead && Model.CanSkipOrUndelete)"),
            "The skip / restore buttons must be guarded behind Model.IsNonHead " +
            "(a head row shows the chip but neither button — F3).");
        Assert.True(html.Contains("events.series.skip"),
            "The skip button label (events.series.skip) must be present (F3).");
        Assert.True(html.Contains("events.series.restore"),
            "The restore button label (events.series.restore) must be present (F3).");
    }

    // ── 2 — Detail_NonHead_Row_For_Author_Shows_Skip_Button ───────────────────

    /// <summary>
    /// <c>GET /events/{id}</c> for a non-head <b>sibling</b> (D3 — the row
    /// whose <see cref="Event.RecurrenceHeadId"/> is non-null), author viewing:
    /// <para>
    /// <b>Behavioral pin</b> — the <see cref="EventDetailViewModel"/> carries
    /// <c>IsNonHead = true</c>, <c>IsHead = false</c>, <c>CanSkipOrUndelete =
    /// true</c> (C-M18·4 — the author ∪ GlobalAdmin standing, the existing
    /// <c>CheckEditStanding</c>), and <c>Rule</c> = the <b>head</b> row's rule
    /// (resolved by a single <see cref="IEventService.GetAsync"/> on
    /// <c>RecurrenceHeadId</c> — F2 / C-M18·1: the read seams stay
    /// concrete-only, the detail page is the one reader of the head's rule
    /// for the chip).
    /// </para>
    /// <para>
    /// <b>Structural pin</b> — the shipped <c>Detail.cshtml</c> carries the
    /// <c>/events/{id}/skip</c> form (the F3 skip button, gated behind
    /// <c>Model.IsNonHead &amp;&amp; Model.CanSkipOrUndelete</c>) + the
    /// <c>events.series.part_of</c> chip (F2 — the non-head row's "Part of a
    /// series" affordance).
    /// </para>
    /// </summary>
    [Fact]
    public async Task Detail_NonHead_Row_For_Author_Shows_Skip_Button()
    {
        const string id = "ev-sib-001";      // the non-head sibling (the row we GET).
        const string headId = "ev-head-001"; // the head (RecurrenceHeadId target).
        const string author = "subj-author-001";

        var nonHead = new Event
        {
            Id = id,
            Title = "Weekly cleanup (week 3)",
            Body = "Bring gloves.",
            AuthorId = author,
            Start = new DateTimeOffset(2026, 10, 15, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.Zero),
            Created = DateTimeOffset.UtcNow,
            // D3 — a non-head sibling: RecurrenceHeadId non-null, rule null.
            RecurrenceHeadId = headId,
        };

        var head = new Event
        {
            Id = headId,
            Title = "Weekly cleanup (head)",
            Body = "Bring gloves.",
            AuthorId = author,
            Start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 10, 1, 13, 0, 0, TimeSpan.Zero),
            Created = DateTimeOffset.UtcNow,
            RecurrenceRule = new EventRecurrenceRule
            {
                Recurrence = Recurrence.Weekly,
                Interval = 1,
                Count = 4,
            },
        };

        var events = Substitute.For<IEventService>();
        // The row we GET (the non-head sibling).
        events.GetAsync(id, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(nonHead);
        // F2 / C-M18·1 — the chip reads the head's rule via a single
        // GetAsync(RecurrenceHeadId).
        events.GetAsync(headId, Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(head);

        var controller = BuildController(events, subjectId: author);

        var result = await controller.Detail(id);
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<EventDetailViewModel>(view.ViewData.Model);

        // (1) Behavioral — the controller sets IsNonHead from the row's
        //     RecurrenceHeadId (D3) + CanSkipOrUndelete from the author
        //     standing (C-M18·4).
        Assert.True(model.IsPartOfSeries,
            "A non-head row (RecurrenceHeadId non-null) must be part of a series (D3 / F2).");
        Assert.False(model.IsHead,
            "A non-head row (RecurrenceRule null) must NOT set IsHead (D3).");
        Assert.True(model.IsNonHead,
            "A non-head row (RecurrenceHeadId non-null) must set IsNonHead = true (D3).");
        Assert.True(model.CanSkipOrUndelete,
            "The author (author ∪ GlobalAdmin, C-M18·4) may skip / restore a non-head row (F3).");

        // F2 / C-M18·1 — the chip's Rule is resolved from the HEAD row (the
        // single GetAsync(RecurrenceHeadId) the controller ran for the chip;
        // the read seams stay concrete-only, the detail page is the one
        // reader of the head's rule).
        Assert.NotNull(model.Rule);
        Assert.Equal(Recurrence.Weekly, model.Rule!.Recurrence);
        await events.Received(1).GetAsync(
            headId, Arg.Any<string>(), Arg.Any<CancellationToken>());

        // (2) Structural — the Detail.cshtml carries the skip form (the F3
        //     skip button, gated behind Model.IsNonHead && CanSkipOrUndelete)
        //     + the events.series.part_of chip (F2 — the non-head row's
        //     "Part of a series" affordance).
        var html = ReadDetailCshtml();
        Assert.True(html.Contains("/events/@Model.Event.Id/skip"),
            "The skip form (POST /events/{id}/skip) must be present (F3).");
        Assert.True(html.Contains("events.series.skip"),
            "The skip button label (events.series.skip) must be present (F3).");
        Assert.True(html.Contains("events.series.part_of"),
            "A non-head row shows the 'Part of a series' chip (F2 / D9).");
    }

    // ── Harness ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the shipped <c>Views/Event/Detail.cshtml</c> (the house "string
    /// pin, no TestServer" idiom — the
    /// <see cref="EventComposerRecurrenceTests"/> /
    /// <see cref="BookmarkButtonTests"/> precedent) and strips the
    /// <c>@* … *@</c> Razor comment blocks (author documentation, not rendered
    /// markup) so the structural pins check the real markup.
    /// </summary>
    private static string ReadDetailCshtml()
    {
        var path = Path.Combine(RepoRoot, "src", "Kumunita.Web", "Views", "Event", "Detail.cshtml");
        Assert.True(File.Exists(path), $"Views/Event/Detail.cshtml not found at {path}.");
        return System.Text.RegularExpressions.Regex.Replace(
            File.ReadAllText(path),
            @"\@\*.*?\*\@",
            "",
            System.Text.RegularExpressions.RegexOptions.Singleline);
    }

    /// <summary>
    /// Builds an <see cref="EventController"/> over NSubstitute seams (the
    /// <see cref="EventComposerRecurrenceTests.BuildController"/> shape): the
    /// <see cref="IDocumentStore"/> is a plain substitute (the M4 controller
    /// never opens a session for the read lanes — the service owns its own
    /// sessions, C3), and a no-op <see cref="ITempDataProvider"/> closes the
    /// <c>TempData</c> bag.
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
    /// Walks up from the test assembly's output dir to the dir holding
    /// <c>Kumunita.slnx</c> (the repo root).
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
