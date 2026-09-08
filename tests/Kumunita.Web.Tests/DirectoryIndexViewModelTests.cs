using System.Reflection;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// M2, plan U7 — the <b>hidden-row privacy pin at the view-model layer</b>.
/// <para>
/// The M2 design doc's "Profile enumeration vs privacy" risk line: the directory's
/// <see cref="DirectoryViewModel"/> renders only a projected shape per row
/// (<see cref="VisibleProfile"/>: SubjectId + DisplayName + Verified) and the hidden
/// candidates' count — never a hidden <see cref="Profile"/>'s own email/phone/contact
/// fields. These tests pin that the *model shape itself* cannot grow those fields
/// silently (a "we just added the email to the directory row" regression is caught
/// here, not in prod).
/// </para>
/// </summary>
public sealed class DirectoryIndexViewModelTests
{
    [Fact]
    public void VisibleProfile_Has_Exactly_Four_Projected_Fields()
    {
        var fields = typeof(VisibleProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // The pin: SubjectId + DisplayName + Verified + Address — and *nothing else*.
        // Address is the one privacy-aware field on the list, deliberately added for the
        // neighbor-surface "who lives where" shape. No Email, No Phone, No ContactVisibility,
        // No HouseholdId.
        Assert.Equal(new[] { "Address", "DisplayName", "SubjectId", "Verified" }, fields.ToArray());
    }

    [Fact]
    public void DirectoryViewModel_Only_Exposes_Profiles()
    {
        var fields = typeof(DirectoryViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // The list model exposes exactly the projected rows — and nothing more. There
        // is no "hidden count" (the directory lists every non-blocked resident to
        // every signed-in viewer: the platform is invitation-only and limited to
        // residents) and no channel to a Profile's own contact/audience fields (those
        // only surface on the detail row, behind the ContactVisibility opt-in).
        Assert.Equal(new[] { "Profiles" }, fields.ToArray());
    }

    [Fact]
    public void Profile_Projection_Excludes_Contact_Fields()
    {
        // A hidden row's Privacy-critical fields must have *no* corresponding member
        // on the visible row type — even if someone later sets them on the Profile,
        // they have nowhere to land in the view model. Address is deliberately *not* in
        // this exclusion set: it is the neighbor-surface field, surfaced on the list when
        // the author has opted in (see VisibleProfile_Has_Exactly_Four_Projected_Fields
        // above).
        var profileContactFields = new[] { "Email", "Phone", "ContactVisibility", "Visibility" };
        var visibleProfileProps = typeof(VisibleProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var contact in profileContactFields)
            Assert.DoesNotContain(contact, visibleProfileProps);
    }
}
