using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Xunit;

namespace Kumunita.Core.Tests;

/// <summary>
/// M2 U4 — <see cref="ProfileToAuditableResource"/>: the single adapter that presents a
/// <see cref="Profile"/> to the frozen <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>.
/// Pure in-memory projection: no store, no audit lane, no DB — the two tests pin the six
/// <see cref="IAuditableResource"/> member-to-field mappings (M2 design §2.2, Shape B) and the
/// <c>TargetKind = "directory"</c> constant (ADR 0006-C3's named value for this line of
/// audit rows). <see cref="Profile.ContactVisibility"/> is intentionally NOT projected onto
/// <see cref="IAuditableResource.Audience"/> — it is the *caller's* separate second decision
/// (M2 design §2.4) and never a member of this adapter's surface.
/// </summary>
public class ProfileToAuditableResourceTests
{
    /// <summary>
    /// All six <see cref="IAuditableResource"/> members map to the expected <see cref="Profile"/>
    /// fields — M2 design §2.2 Shape B:
    /// Id = SubjectId, Name = DisplayName, OwnerId = SubjectId, Audience = Visibility,
    /// ComponentId = null, TargetKind = "directory".
    /// </summary>
    [Fact]
    public void Maps_All_Six_Fields()
    {
        var visibility = new Audience(AudienceMode.All,
            [new AudienceGrant(GrantKind.Group, "g-homeroom")]);

        var profile = new Profile
        {
            SubjectId = "u-adaptee",
            DisplayName = "The Adaptee",
            Verified = true,
            Visibility = visibility,
            // A *distinct* ContactVisibility on purpose: the adapter must expose exactly
            // `Visibility` (not ContactVisibility, not a merged audience) as its Audience.
            ContactVisibility = new Audience(AudienceMode.Any,
                [new AudienceGrant(GrantKind.User, "u-contact-only")]),
        };

        var adapter = new ProfileToAuditableResource(profile);

        Assert.Equal("u-adaptee", adapter.Id);
        Assert.Equal("The Adaptee", adapter.Name);
        Assert.Equal("u-adaptee", adapter.OwnerId);
        Assert.Same(visibility, adapter.Audience);   // reference identity: exactly `Visibility`
        Assert.Null(adapter.ComponentId);
        Assert.Equal("directory", adapter.TargetKind);
    }

    /// <summary>
    /// <see cref="ProfileToAuditableResource.TargetKind"/> is a *constant* `"directory"` (ADR
    /// 0006-C3's named value for the directory's audit aggregate rows) — the same string for
    /// every profile, every viewer; nothing in the profile's own state can change it.
    /// </summary>
    [Fact]
    public void TargetKind_Is_Directory()
    {
        // Three different profiles; for each, TargetKind must be the constant "directory"
        // regardless of what other fields the profile carries (visibility shape, subject, …).
        foreach (var profile in new[]
        {
            new Profile { SubjectId = "u-a", DisplayName = "A", Verified = true,
                          Visibility = new Audience() },
            new Profile { SubjectId = "u-b", DisplayName = "B", Verified = false,
                          Visibility = new Audience(AudienceMode.All,
                              [new AudienceGrant(GrantKind.User, "u-x")]),
                          ContactVisibility = new Audience() },
            new Profile { SubjectId = "u-c", DisplayName = "C", Verified = true,
                          Visibility = new Audience(AudienceMode.Any,
                              [new AudienceGrant(GrantKind.Group, "g-1")]),
                          ContactVisibility = new Audience(AudienceMode.Any, [new AudienceGrant(GrantKind.User, "u-y")]) },
        })
        {
            var adapter = new ProfileToAuditableResource(profile);
            Assert.Equal("directory", adapter.TargetKind);
        }
    }
}
