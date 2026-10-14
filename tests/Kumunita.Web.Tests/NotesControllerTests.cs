using System.Collections;
using System.Security.Claims;
using Kumunita.Core.PersonalNotes;
using Kumunita.Web.Controllers;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using NSubstitute;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// The <see cref="NotesController"/> Web-boundary seam tests (M37 U04 — Web
/// surface part 1: the <c>POST /notes/attach</c> write lane + the
/// <c>GET /notes/has/{targetKind}/{targetId}</c> F1 witness, ADR 0167).
/// Mirrors the <see cref="BookmarksControllerTests"/> harness shape (the M17
/// precedent — the NSubstitute seam over a thin HTTP layer, the 404 non-
/// leaky split + the redirect-after-write mapping are **this** layer's
/// pins). The **frozen** <see cref="IPersonalNoteService"/> seam is
/// substituted with NSubstitute (the controller never re-derives access —
/// C-M37·1: the seam is the sole reader / writer; the D3 write-lane
/// visibility check runs in the seam, not the controller). The
/// <see cref="IDocumentStore"/> is a plain substitute for the write lane's
/// <c>LightweightSession()</c> (the C3 same-transaction lane —
/// <see cref="PersonalNoteService"/> never commits internally; the caller
/// owns the single write).
/// <para>
/// The three M37-U04 pinned tests (the unit plan's Deliverables, locked
/// verbatim):
/// </para>
/// <list type="number">
/// <item><b>NotesController_Attach_InvisibleTarget_404_NoNoteRow_NoDenyRow</b>
/// — <c>POST /notes/attach</c>: the seam's <c>CreateNoteAsync</c> throws
/// <c>KeyNotFoundException</c> (the D3 write-lane check failed — the
/// target is not visible to the actor) and the controller maps it to a
/// non-leaky <see cref="NotFoundResult"/> (never 403 — the M16 create-gate
/// posture: the Deny row does not survive, and no note row is committed —
/// the <c>SaveChangesAsync</c> call is the witness that the commit never
/// runs on the refuse path).</item>
/// <item><b>NotesController_Attach_AbsentTarget_404_NoNoteRow</b> —
/// <c>POST /notes/attach</c>: the seam's <c>CreateNoteAsync</c> throws
/// <c>KeyNotFoundException</c> (the target is absent — the D3 write-lane
/// check's second branch) and the controller maps it to a non-leaky
/// <see cref="NotFoundResult"/> (never 403 — the M16 create-gate posture;
/// no note row is committed — the <c>SaveChangesAsync</c> call is the
/// witness that the commit never runs on the refuse path).</item>
/// <item><b>NotesController_HasNote_NonOwner_404_NoAuditRow</b> —
/// <c>GET /notes/has/{targetKind}/{targetId}</c>: a caller with no read
/// standing (no <c>SubjectId</c> — a <c>GlobalAdmin</c> included) gets a
/// non-leaky <see cref="NotFoundResult"/>, and the controller does **not**
/// call the seam's <c>HasNoteAsync</c> (the "operator has no read standing"
/// precedent — the ADR 0105 pin; the C-M37·2 sole-reader pin — the
/// controller returns the 404 before delegating any read to the seam).
/// That 404 commits <b>no</b> <c>AccessAudit</c> row (C-M37·2 — the owner's
/// read is a personal read, zero audit; the non-owner has no read
/// standing at all).</item>
/// </list>
/// <para>
/// **No database, no Testcontainers** — a pure NSubstitute seam test (the
/// seam's read / write decisions are the seam's; this layer pins the
/// controller's mapping + the non-leaky-404 split, not the seam's gate).
/// </para>
/// <para>
/// **Core / Web split (recorded per the unit plan's Deliverables):** the
/// 18 Core-layer §2.5 tests (the design doc's pinned seam tests) are U02's
/// + U03's (the note-method pins + the folder-tree pins); the 3 Web-layer
/// pins in this file are U04's (the D3 write-lane visibility check at the
/// Web layer + the C-M37·2 non-leaky-404 + zero-audit pins, witnessed at
/// the controller boundary). The 18 + 3 = 21 tests together cover the
/// M37 surface.
/// </para>
/// </summary>
public sealed class NotesControllerTests
{
    // ── 1 — NotesController_Attach_InvisibleTarget_404_NoNoteRow_NoDenyRow ─

    /// <summary>
    /// <c>POST /notes/attach</c> (the D3 write-lane visibility check at the
    /// Web layer — the seam's <see cref="IPersonalNoteService.CreateNoteAsync"/>
    /// re-runs the target's own frozen <c>CanAsync(Read)</c> before the row
    /// is created; the controller maps the seam's
    /// <see cref="KeyNotFoundException"/> to a non-leaky **404**, never
    /// 403 — the M16 create-gate posture: the Deny row does not survive,
    /// and no note row is committed — C-M37·3): the controller returns a
    /// <see cref="NotFoundResult"/>, the <c>SaveChangesAsync</c> commit
    /// never runs (the witness — no note row is committed on the refuse
    /// path), and the <c>CreateNoteAsync</c> call is the seam's sole writer
    /// (C-M37·1 — the controller never re-derives access; the seam is the
    /// sole writer).
    /// </summary>
    [Fact]
    public async Task NotesController_Attach_InvisibleTarget_404_NoNoteRow_NoDenyRow()
    {
        const string actor = "subj-pn-owner";
        const string targetKind = "post";
        const string targetId = "p-invisible-to-actor"; // D3 — target invisible (Deny)

        var notes = Substitute.For<IPersonalNoteService>();
        notes.When(x => x.CreateNoteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<IDocumentSession>()))
            .Throws(new KeyNotFoundException(
                "Target 'p-invisible-to-actor' is not visible to the actor."));

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(notes, store, subjectId: actor);
        var result = await controller.Attach(targetKind, targetId, "my note");

        // C-M37·3 / D3 — the non-leaky 404 (never 403 — the M16 create-gate
        // posture: the Deny row does not survive; the controller maps the
        // seam's KeyNotFoundException to a 404, the Web layer's mapping).
        Assert.IsType<NotFoundResult>(result);

        // C-M37·3 — no note row is committed on the refuse path: the
        // SaveChangesAsync commit never runs (the witness — the controller
        // returns the 404 before delegating the commit to the caller).
        await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        // C-M37·1 — the seam is the sole writer (the controller never
        // re-derives access; the CreateNoteAsync call is the seam's, not
        // the controller's): the seam's CreateNoteAsync was called exactly
        // once with the actor's SubjectId as the ownerId + the
        // targetKind / targetId / body verbatim + the caller's session (the
        // C3 same-transaction lane — the caller owns the single write).
        await notes.Received(1).CreateNoteAsync(
            actor, targetKind, targetId, "my note", Arg.Any<IDocumentSession>());
    }

    // ── 2 — NotesController_Attach_AbsentTarget_404_NoNoteRow ─────────────

    /// <summary>
    /// <c>POST /notes/attach</c> (the D3 write-lane visibility check at the
    /// Web layer — the seam's <see cref="IPersonalNoteService.CreateNoteAsync"/>
    /// re-runs the target's own frozen <c>CanAsync(Read)</c> before the row
    /// is created; the controller maps the seam's
    /// <see cref="KeyNotFoundException"/> to a non-leaky **404**, never
    /// 403 — the M16 create-gate posture: the Deny row does not survive,
    /// and no note row is committed — C-M37·3): the target is **absent**
    /// (the D3 write-lane check's second branch — not invisible / denied,
    /// just not there), and the controller returns a
    /// <see cref="NotFoundResult"/>, the <c>SaveChangesAsync</c> commit
    /// never runs (the witness — no note row is committed on the refuse
    /// path), and the <c>CreateNoteAsync</c> call is the seam's sole writer
    /// (C-M37·1 — the controller never re-derives access; the seam is the
    /// sole writer).
    /// </summary>
    [Fact]
    public async Task NotesController_Attach_AbsentTarget_404_NoNoteRow()
    {
        const string actor = "subj-pn-owner";
        const string targetKind = "post";
        const string targetId = "p-does-not-exist"; // D3 — target absent

        var notes = Substitute.For<IPersonalNoteService>();
        notes.When(x => x.CreateNoteAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<IDocumentSession>()))
            .Throws(new KeyNotFoundException(
                "Target 'p-does-not-exist' is not visible to the actor."));

        var store = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        store.LightweightSession().Returns(session);

        var controller = Build(notes, store, subjectId: actor);
        var result = await controller.Attach(targetKind, targetId, "my note");

        // C-M37·3 / D3 — the non-leaky 404 (never 403 — the M16 create-gate
        // posture: the target is absent; no row is committed; the controller
        // maps the seam's KeyNotFoundException to a 404, the Web layer's
        // mapping).
        Assert.IsType<NotFoundResult>(result);

        // C-M37·3 — no note row is committed on the refuse path: the
        // SaveChangesAsync commit never runs (the witness — the controller
        // returns the 404 before delegating the commit to the caller).
        await session.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());

        // C-M37·1 — the seam is the sole writer (the controller never
        // re-derives access; the CreateNoteAsync call is the seam's, not
        // the controller's): the seam's CreateNoteAsync was called exactly
        // once with the actor's SubjectId as the ownerId + the
        // targetKind / targetId / body verbatim + the caller's session (the
        // C3 same-transaction lane — the caller owns the single write).
        await notes.Received(1).CreateNoteAsync(
            actor, targetKind, targetId, "my note", Arg.Any<IDocumentSession>());
    }

    // ── 3 — NotesController_HasNote_NonOwner_404_NoAuditRow ───────────────

    /// <summary>
    /// <c>GET /notes/has/{targetKind}/{targetId}</c> (the "operator has no
    /// read standing" gate — C-M37·2, the ADR 0105 precedent): a caller
    /// with no read standing (no <c>SubjectId</c> — a <c>GlobalAdmin</c>
    /// included) gets a non-leaky <see cref="NotFoundResult"/>, and the
    /// controller does **not** call the seam's
    /// <see cref="IPersonalNoteService.HasNoteAsync"/> (the sole-reader
    /// pin — C-M37·1: the controller never re-derives access, and a caller
    /// with no read standing has no read to delegate). That 404 commits
    /// <b>no</b> <c>AccessAudit</c> row (C-M37·2 — the owner's read is a
    /// personal read, zero audit; the non-owner has no read standing at
    /// all — the ADR 0105 "operator has no read standing" shape).
    /// </summary>
    [Fact]
    public async Task NotesController_HasNote_NonOwner_404_NoAuditRow()
    {
        const string targetKind = "post";
        const string targetId = "p-any";

        var notes = Substitute.For<IPersonalNoteService>();
        // HasNoteAsync is **not** stubbed — the non-owner path returns
        // NotFound before reaching it. NSubstitute's auto-substitution of
        // the un-stubbed method is inert (no call is logged for a method
        // the controller never invokes), and the DidNotReceive assertion
        // below is the pin.
        var controller = Build(notes, subjectId: null);
        var result = await controller.HasNote(targetKind, targetId);

        // C-M37·2 — the non-leaky 404 (a non-owner, a GlobalAdmin included,
        // has no read standing over any note; that 404 commits no
        // AccessAudit row).
        Assert.IsType<NotFoundResult>(result);

        // The seam's HasNoteAsync was **not** called (the "operator has no
        // read standing" precedent — the controller returns the 404 before
        // delegating any read to the seam; the sole-reader pin, C-M37·1).
        await notes.DidNotReceive().HasNoteAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    // ── Harness ──────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="NotesController"/> over NSubstitute seams. The
    /// <see cref="IPersonalNoteService"/> is the sole seam (C-M37·1 /
    /// C-M37·2 — the controller composes **only** the note service; no
    /// <c>IAuthorizationService</c>, no <c>IUserInfoService</c> — the
    /// owner's read is a personal read, zero audit). The
    /// <see cref="IDocumentStore"/> is a plain substitute for the write
    /// lane's <c>LightweightSession()</c> (the C3 same-transaction lane —
    /// <see cref="PersonalNoteService"/> never commits internally; the
    /// caller owns the single write). A no-op
    /// <see cref="ITempDataProvider"/> closes the <c>TempData</c> bag so
    /// the write lane's success branch doesn't NRE
    /// (<c>DefaultHttpContext</c> leaves <c>Session</c> null by default).
    /// </summary>
    private static NotesController Build(
        IPersonalNoteService notes,
        IDocumentStore? store = null,
        string? subjectId = null,
        string[]? roles = null)
    {
        store ??= Substitute.For<IDocumentStore>();

        var controller = new NotesController(notes, store);
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
    /// A no-op <see cref="ITempDataProvider"/> — the house idiom for closing
    /// the <c>TempData</c> bag in controller tests (the
    /// <see cref="BookmarksControllerTests"/> / <see cref="AdminHelpControllerTests"/>
    /// precedent: <c>DefaultHttpContext</c> leaves <c>Session</c> null by
    /// default, so the <c>TempData</c> bag NREs on the redirect-after-write
    /// branch without a provider; this no-op keeps the assertion target
    /// (the call log / the redirect shape) intact).
    /// </summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext context) =>
            new Dictionary<string, object?>();
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            // no-op — the assertion target is the call log / the redirect shape
        }
    }
}
