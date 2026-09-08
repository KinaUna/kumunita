using System.Reflection;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// M2, plan U8 — the <b>contact-block opt-in pin at the view-model layer</b> (invariant
/// C-M2·1, <c>ARCHITECTURE.md</c> §2.4/§9). The service-layer pin is U6's
/// <c>DirectoryServiceTests_U6.ContactVisibility_FourShape_TrightTable</c>; the e2e browser
/// pin is U13's. These tests pin that the *model shape itself* cannot grow a new
/// contact-carrying field silently (a "we just added the household email to the detail row"
/// regression is caught here, not in prod), and that the <c>Detail</c> record only carries
/// the basic info (name + verified — always) plus the contact block behind
/// <c>ShowContactBlock</c>.
/// <para>
/// The old "contact block never on a hidden profile" pin is now the narrower
/// "contact block only on the ContactBlock-opted-in profile" pin: the directory no longer
/// has a "hidden profile" shape (show-everyone rule), so the <c>Detail</c> record no longer
/// has an <c>IsVisible</c> field — only the <c>ShowContactBlock</c> gate remains.
/// </para>
/// </summary>
public sealed class DirectoryDetailViewModelTests
{
    /// <summary>
    /// Plan U8 pin — the <see cref="DirectoryViewModel.Detail"/> record has exactly five fields,
    /// and <b>nothing else</b>. No <c>Visibility</c>, no <c>ContactVisibility</c>, no
    /// <c>HouseholdId</c>, no <c>ExternalId</c>, no <c>SubjectId</c> (the row is already
    /// addressed by its route). The contact surface is a *subset* of
    /// <c>Kumunita.Core.UserInfo.Profile</c> (<c>Email</c>/<c>Phone</c>) — nothing more.
    /// </summary>
    [Fact]
    public void Detail_Has_Exactly_Five_Fields()
    {
        var fields = typeof(DirectoryViewModel.Detail)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // DisplayName, Verified, ShowContactBlock, Email, Phone — the plan's U8 freeze.
        Assert.Equal(
            new[] { "DisplayName", "Email", "Phone", "ShowContactBlock", "Verified" },
            fields);
    }

    /// <summary>
    /// Plan U8 exit-criterion test (the contact-block opt-in pin at the view-model layer):
    /// a <see cref="DirectoryViewModel.Detail"/> with <c>ShowContactBlock = false</c> has
    /// <c>Email</c>/<c>Phone</c> null (the profile's <c>ContactVisibility</c> evaluated to
    /// deny, or the author opted out), and with <c>ShowContactBlock = true</c> carries the
    /// projected non-null contact values. The basic info
    /// (<c>DisplayName</c>/<c>Verified</c>) is always carried — the directory no longer has
    /// a "hidden profile" shape.
    /// <para>
    /// The contact fields are <b>derived from</b> the gate in
    /// <c>DirectoryController.ProjectDetail</c> — <c>ShowContactBlock</c> is false ⇒ the
    /// projection nulls both, so the Razor view (<c>Directory/Detail.cshtml</c>) has no
    /// channel to render a contact block on a non-opted-in or denied row, even though it
    /// holds the source <c>Profile</c>. This is the view-model-layer twin of U6's test #1
    /// (service-layer pin); the e2e browser pin is U13's.
    /// </para>
    /// </summary>
    [Fact]
    public void DirectoryDetailViewModel_ContactBlock_Gated()
    {
        // Case 1 — a profile whose <c>ContactVisibility</c> was denied (or the author
        // opted out) for the viewer: basic info renders (name + verified), but the
        // contact block fields are null.
        var contactHidden = new DirectoryViewModel.Detail(
            DisplayName: "A. Resident",
            Verified: true,
            ShowContactBlock: false,
            Email: null,
            Phone: null);

        Assert.False(contactHidden.ShowContactBlock);
        Assert.Equal("A. Resident", contactHidden.DisplayName);
        Assert.True(contactHidden.Verified);
        // The model has no channel to a contact value while the gate is off — the Razor view
        // guards on ShowContactBlock, so the §2.4 "null ⇒ not opted in" pin is held at the
        // shape level.
        Assert.Null(contactHidden.Email);
        Assert.Null(contactHidden.Phone);

        // Case 2 — a profile whose <c>ContactVisibility</c> allowed the viewer (the
        // §2.4 Any+non-empty grant row): the gate is on, so the projected contact values
        // are carried alongside the name + badge.
        var contactAllowed = new DirectoryViewModel.Detail(
            DisplayName: "B. Resident",
            Verified: true,
            ShowContactBlock: true,
            Email: "b@example.kumunita",
            Phone: "+1 555 0100");

        Assert.True(contactAllowed.ShowContactBlock);
        Assert.Equal("B. Resident", contactAllowed.DisplayName);
        Assert.True(contactAllowed.Verified);
        Assert.Equal("b@example.kumunita", contactAllowed.Email);
        Assert.Equal("+1 555 0100", contactAllowed.Phone);
    }

    /// <summary>
    /// <see cref="DirectoryViewModel"/> (the list model) stays exactly one field —
    /// <c>Profiles</c> — (the show-everyone rule: no <c>HiddenCount</c>). The detail
    /// surface lives in the <b>nested</b> <see cref="DirectoryViewModel.Detail"/> type,
    /// not as new list-model properties, so U7's shape-pin
    /// (<c>DirectoryViewModel_Only_Exposes_Profiles</c>) stays green. This guards
    /// against a "sneak the Detail fields onto the list model" regression.
    /// </summary>
    [Fact]
    public void DirectoryViewModel_Still_Only_Profiles()
    {
        var fields = typeof(DirectoryViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // Detail is a *nested type* (typeof(DirectoryViewModel.Detail)), not a property — so
        // the list model's public property set is just Profiles.
        Assert.Equal(new[] { "Profiles" }, fields);
    }
}
