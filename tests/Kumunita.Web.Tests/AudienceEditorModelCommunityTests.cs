using Kumunita.Core.Authorization;
using Kumunita.Web.Models;
using Xunit;

namespace Kumunita.Web.Tests;

/// <summary>
/// ADR 0036 — the "Everyone in this community" flag on the reusable
/// <see cref="AudienceEditorModel"/>: the form-bound <see
/// cref="AudienceEditorModel.CommunityVisible"/> checkbox state must
/// round-trip <b>verbatim</b> onto the <see cref="Audience.Community"/>
/// document field and back, in both directions, without disturbing the
/// Mode / Grants shape (the U11 / F13 single-source pin: the editor is
/// the same shape as the document through one binder, no parallel
/// audience object).
/// </summary>
public class AudienceEditorModelCommunityTests
{
    /// <summary>
    /// BuildAudience carries the checkbox state verbatim onto the
    /// document field (checked → <c>Community == true</c>, unchecked →
    /// <c>false</c>), with the Mode + grant set untouched (a checked
    /// flag with empty grants is the "all community members" shape the
    /// composer seeds).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BuildAudience_CommunityVisible_RoundTripsVerbatim(bool flag)
    {
        var editor = new AudienceEditorModel
        {
            Mode = "Any",
            Grants = "[]",
            CommunityVisible = flag,
        };

        var audience = editor.BuildAudience();

        Assert.Equal(flag, audience.Community);
        Assert.Equal(AudienceMode.Any, audience.Mode);
        Assert.Empty(audience.Grants);
    }

    /// <summary>
    /// BuildAudience carries the flag alongside non-empty grants (the
    /// "community + additional picks" shape — the flag and the grant
    /// list are independent, the flag is not a mode of the list).
    /// </summary>
    [Fact]
    public void BuildAudience_CommunityVisible_WithGrants_BothPreserved()
    {
        var editor = new AudienceEditorModel
        {
            Mode = "All",
            Grants = "[{\"Kind\":\"User\",\"Id\":\"u-granted\"}]",
            CommunityVisible = true,
        };

        var audience = editor.BuildAudience();

        Assert.True(audience.Community);
        Assert.Equal(AudienceMode.All, audience.Mode);
        Assert.Single(audience.Grants);
        Assert.Equal(GrantKind.User, audience.Grants[0].Kind);
        Assert.Equal("u-granted", audience.Grants[0].Id);
    }

    /// <summary>
    /// FromAudience seeds the checkbox state from the document field in
    /// the read direction (the edit-lane pre-fill), so an edit POST
    /// round-trips a community-visible post back to community-visible
    /// (a regression that dropped the flag here would silently flip a
    /// post to owner-only on the next edit save).
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FromAudience_Community_RoundTripsVerbatim(bool flag)
    {
        var audience = new Audience(AudienceMode.Any,
            [new AudienceGrant(GrantKind.User, "u-granted")])
        {
            Community = flag,
        };

        var editor = AudienceEditorModel.FromAudience(audience);

        Assert.Equal(flag, editor.CommunityVisible);
        Assert.Equal("Any", editor.Mode);
        Assert.NotNull(editor.Grants);

        // And the full round-trip: build → from → build is stable.
        var rebuilt = editor.BuildAudience();
        Assert.Equal(flag, rebuilt.Community);
        Assert.Equal(AudienceMode.Any, rebuilt.Mode);
        Assert.Single(rebuilt.Grants);
    }
}
