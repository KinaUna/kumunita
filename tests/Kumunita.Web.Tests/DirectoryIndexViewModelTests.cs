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
    public void VisibleProfile_Has_Exactly_Three_Projected_Fields()
    {
        var fields = typeof(VisibleProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // The privacy pin: SubjectId + DisplayName + Verified — and *nothing else*.
        // No Email, No Phone, No ContactVisibility, No HouseholdId.
        Assert.Equal(new[] { "DisplayName", "SubjectId", "Verified" }, fields.ToArray());
    }

    [Fact]
    public void DirectoryViewModel_Only_Exposes_Profiles_And_HiddenCount()
    {
        var fields = typeof(DirectoryViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        // The list model exposes the projected rows + the hidden count, and nothing
        // more — the view has no channel to a Profile's own contact fields.
        Assert.Equal(new[] { "HiddenCount", "Profiles" }, fields.ToArray());
    }

    [Fact]
    public void Profile_Projection_Excludes_Contact_Fields()
    {
        // A hidden row's Privacy-critical fields must have *no* corresponding member
        // on the visible row type — even if someone later sets them on the Profile,
        // they have nowhere to land in the view model.
        var profileContactFields = new[] { "Email", "Phone", "ContactVisibility", "Visibility" };
        var visibleProfileProps = typeof(VisibleProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToHashSet();

        foreach (var contact in profileContactFields)
            Assert.DoesNotContain(contact, visibleProfileProps);
    }
}
