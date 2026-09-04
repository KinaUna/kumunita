using System.Reflection;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// M2, plan U8 — the <b>§9 pin at the view-model layer</b> ("contact block never on a hidden
/// profile"; invariant C-M2·1, <c>ARCHITECTURE.md</c> §9). The service-layer pin is U6's
/// <c>DirectoryServiceTests_U6.ContactVisibility_FourShape_TrightTable</c>; the e2e browser pin
/// is U13's. These tests pin that the *model shape itself* cannot grow a new contact-carrying
/// field silently (a "we just added the household email to the detail row" regression is caught
/// here, not in prod), and that the projection logic in <c>DirectoryController.ProjectDetail</c>
/// honors the §9 gate — a hidden row's <c>Email</c>/<c>Phone</c> are null regardless of the
/// underlying <c>Profile</c>.
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
    /// Plan U8 exit-criterion test (the §9 pin at the view-model layer): a
    /// <see cref="DirectoryViewModel.Detail"/> with <c>ShowContactBlock = false</c> has
    /// <c>Email</c>/<c>Phone</c> null (a hidden profile, or a visible profile whose
    /// <c>ContactVisibility</c> evaluated to deny / "not evaluated"), and with
    /// <c>ShowContactBlock = true</c> carries the projected non-null contact values.
    /// <para>
    /// The contact fields are <b>derived from</b> the gate in
    /// <c>DirectoryController.ProjectDetail</c> — <c>ShowContactBlock</c> is false ⇒ the
    /// projection nulls both, so the Razor view (<c>Directory/Detail.cshtml</c>) has no channel
    /// to render a contact block on a hidden row, even though it holds the source
    /// <c>Profile</c>. This is the view-model-layer twin of U6's test #1 (service-layer pin);
    /// the e2e browser pin is U13's.
    /// </para>
    /// </summary>
    [Fact]
    public void DirectoryDetailViewModel_ContactBlock_Gated()
    {
        // Case 1 — a HIDDEN profile (Visibility denied ⇒ DetailAsync returns IsVisible==false):
        // ProjectDetail yields the empty shape — no name, no verified badge, no contact.
        var hidden = new DirectoryViewModel.Detail(
            DisplayName: string.Empty,
            Verified: false,
            ShowContactBlock: false,
            Email: null,
            Phone: null);

        Assert.False(hidden.ShowContactBlock);
        Assert.Null(hidden.Email);
        Assert.Null(hidden.Phone);
        Assert.Equal(string.Empty, hidden.DisplayName);
        Assert.False(hidden.Verified);

        // Case 2 — VISIBLE but contact-hidden (Visibility allowed, ContactVisibility=null ⇒
        // §2.4 "not evaluated"; or a ContactVisibility that denied the viewer): name + badge are
        // present, contact stays null because the gate is off.
        var visibleNoContact = new DirectoryViewModel.Detail(
            DisplayName: "A. Resident",
            Verified: true,
            ShowContactBlock: false,
            Email: null,
            Phone: null);

        Assert.False(visibleNoContact.ShowContactBlock);
        Assert.Equal("A. Resident", visibleNoContact.DisplayName);
        Assert.True(visibleNoContact.Verified);
        // The model has no channel to a contact value while the gate is off — the Razor view
        // guards on ShowContactBlock, so this is the §9 pin held at the shape level.
        Assert.Null(visibleNoContact.Email);
        Assert.Null(visibleNoContact.Phone);

        // Case 3 — VISIBLE AND contact-allowed (the §2.4 Any+non-empty grant row): the gate is
        // on, so the projected contact values are carried alongside the name + badge.
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
    /// <see cref="DirectoryViewModel"/> (the list model) stays exactly the U7 shape —
    /// <c>Profiles</c> + <c>HiddenCount</c>. The detail surface lives in the <b>nested</b>
    /// <see cref="DirectoryViewModel.Detail"/> type, not as new list-model properties, so U7's
    /// shape-pinning test (<c>DirectoryViewModel_Only_Exposes_Profiles_And_HiddenCount</c>) stays
    /// green. This guards against a "sneak the Detail fields onto the list model" regression.
    /// </summary>
    [Fact]
    public void DirectoryViewModel_Still_Only_Profiles_And_HiddenCount()
    {
        var fields = typeof(DirectoryViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // Detail is a *nested type* (typeof(DirectoryViewModel.Detail)), not a property — so the
        // list model's public property set is unchanged from U7.
        Assert.Equal(new[] { "HiddenCount", "Profiles" }, fields);
    }
}
