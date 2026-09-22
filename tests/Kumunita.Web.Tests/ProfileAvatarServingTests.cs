using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// FACES M1, M2, M4, M5, M6 seam tests for <see cref="ProfileController.Avatar"/> (the profile-avatar
/// serving lane — design doc §2.3). ADR 0057: avatars are visible to every signed-in resident
/// by construction, so no <see cref="IAuthorizationService.CanAsync"/> decision is taken and no
/// <see cref="AccessAudit"/> row is written — the pins this harness owns are the structural ones
/// (unsigned → Challenge; unknown → 404; blocked → 404; any signed-in viewer → 200, no decision).
/// <list type="number">
/// <item><b>M1 — owner self-serve:</b> the owner loads their own avatar → a
///       <see cref="FileStreamResult"/> (the 200) carrying the <em>stored</em>
///       <see cref="MediaObject.ContentType"/> (C-MED·5), the whole payload, and
///       <c>X-Content-Type-Options: nosniff</c>. No <c>CanAsync</c> call (ADR 0057
///       removes the per-viewer decision entirely).</item>
/// <item><b>M2 — any signed-in other:</b> same serving surface, 200 via the
///       "all signed-in residents" branch (ADR 0041 AllResidents flag; ADR 0057
///       makes the avatar unconditionally visible to any resident). Same 200/
///       nosniff/Content-Type pins; no <c>CanAsync</c> call.</item>
/// <item><b>M4 — blocked profile:</b> <c>404</c> <em>before</em> the avatar read — a
///       blocked profile with an avatar set still 404s with <em>no</em>
///       <c>IMediaStore</c> call (blocked supersedes, the <see cref="DirectoryService"/>
///       early-return idiom).</item>
/// <item><b>M5 — unknown profile:</b> <c>404</c>; nothing downstream of
///       <c>GetProfileAsync</c> runs at all.</item>
/// <item><b>M6 — unsigned:</b> no principal → <see cref="ChallengeResult"/> (never a
///       200); the in-action defensive guard precedes even the profile read.</item>
/// </list>
/// <para>
/// Harness pattern mirrors <see cref="AnnouncementControllerTests"/> (NSubstitute +
/// <c>DefaultHttpContext</c>): the interface seams are substituted;
/// <see cref="DirectoryService"/> (ctor param #2) is <em>real</em> — it is <c>sealed</c>
/// (unproxyable) and neither action under test references it. The principal carries the
/// single <c>Kumunita.Sub</c> claim <see cref="KumunitaPrincipal"/> mints. Class-level
/// <c>[Authorize]</c> does not run under direct invocation — M6 verifies the action's
/// own defensive guard, per the design doc FACES row.
/// </para>
/// </summary>
public class ProfileAvatarServingTests
{
    private const string Owner = "subj-owner-001";
    private const string Viewer = "subj-viewer-001";
    private const string Other = "subj-other-001";
    private const string Unknown = "subj-unknown-001";

    /// <summary>A stand-in avatar id — 64 lowercase hex chars (the C-MED·4 SHA-256 content-address shape).</summary>
    private const string MediaId =
        "9f2c4d6e8a1b3c5d7e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d";

    /// <summary>A stand-in 12-byte PNG payload (the real PNG magic header + filler).</summary>
    private static readonly byte[] Png =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x01, 0x02, 0x03,
    };

    /// <summary>M1 — owner self-serve: 200 + stored Content-Type + nosniff, no CanAsync (ADR 0057).</summary>
    [Fact]
    public async Task Serving_SignedAuthorizedOwner_Returns200_CorrectContentType()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, principalSubjectId: Owner);

        userInfo.GetProfileAsync(Owner).Returns(OwnerProfile());
        media.GetAsync(MediaId, Arg.Any<CancellationToken>()).Returns(ServedMediaObject());
        media.OpenReadAsync(MediaId, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Png));

        var result = await controller.Avatar(Owner);

        var file = Assert.IsAssignableFrom<FileStreamResult>(result); // 200 — the serve (a File result)
        Assert.Equal("image/png", file.ContentType);                 // the stored Content-Type (C-MED·5)
        using var drained = new MemoryStream();
        await file.FileStream.CopyToAsync(drained, CancellationToken.None); // payload integrity
        Assert.Equal(Png, drained.ToArray());
        Assert.Equal("nosniff", controller.HttpContext.Response.Headers["X-Content-Type-Options"]);
        // ADR 0057 — the avatar lane no longer takes an authorization decision.
        await authz.DidNotReceiveWithAnyArgs().CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
        await media.Received(1).GetAsync(MediaId, Arg.Any<CancellationToken>());
        await media.Received(1).OpenReadAsync(MediaId, Arg.Any<CancellationToken>());
    }

    /// <summary>M2 — any signed-in other: 200 + stored Content-Type + nosniff, no CanAsync (ADR 0057).</summary>
    [Fact]
    public async Task Serving_SignedAuthorizedOther_Returns200()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, principalSubjectId: Viewer);

        userInfo.GetProfileAsync(Other).Returns(OtherProfile());
        media.GetAsync(MediaId, Arg.Any<CancellationToken>()).Returns(ServedMediaObject());
        media.OpenReadAsync(MediaId, Arg.Any<CancellationToken>()).Returns(new MemoryStream(Png));

        var result = await controller.Avatar(Other);

        var file = Assert.IsAssignableFrom<FileStreamResult>(result); // 200 — the serve (a File result)
        Assert.Equal("image/png", file.ContentType);
        using var drained = new MemoryStream();
        await file.FileStream.CopyToAsync(drained, CancellationToken.None);
        Assert.Equal(Png, drained.ToArray());
        Assert.Equal("nosniff", controller.HttpContext.Response.Headers["X-Content-Type-Options"]);
        // ADR 0057 — avatars are visible to every signed-in resident (no per-viewer decision).
        await authz.DidNotReceiveWithAnyArgs().CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
        await media.Received(1).OpenReadAsync(MediaId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// M4 — blocked profile: 404 <em>before</em> the avatar read. The profile carries an
    /// <c>AvatarId</c> on purpose — blocked supersedes the avatar (the §2.3 ordering
    /// pin), and no <c>IMediaStore</c> call.
    /// </summary>
    [Fact]
    public async Task Serving_BlockedProfile_Returns404()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, principalSubjectId: Viewer);

        userInfo.GetProfileAsync(Other)
            .Returns(new Profile
            {
                SubjectId = Other,
                DisplayName = "Bo",
                Blocked = true,                    // blocked supersedes…
                AvatarId = MediaId,                // …even though an avatar is set
            });

        var result = await controller.Avatar(Other);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.Received(1).GetProfileAsync(Other);
        await media.DidNotReceiveWithAnyArgs().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>M5 — unknown profile: <c>GetProfileAsync</c> → null → 404; nothing downstream runs.</summary>
    [Fact]
    public async Task Serving_UnknownProfile_Returns404()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, principalSubjectId: Viewer);

        userInfo.GetProfileAsync(Unknown).Returns((Profile?)null);

        var result = await controller.Avatar(Unknown);

        Assert.IsType<NotFoundResult>(result);
        await userInfo.Received(1).GetProfileAsync(Unknown);
        await media.DidNotReceiveWithAnyArgs().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// M6 — unsigned: the action's defensive guard (<c>viewer is null → Challenge()</c>)
    /// fires <em>before</em> the profile read — never a 200, no seam traffic at all.
    /// </summary>
    [Fact]
    public async Task Serving_Unsigned_Challenges_No200()
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var media = Substitute.For<IMediaStore>();
        var controller = Build(userInfo, authz, media, principalSubjectId: null);

        var result = await controller.Avatar(Owner);

        Assert.IsType<ChallengeResult>(result);
        Assert.IsNotAssignableFrom<FileStreamResult>(result); // never a 200
        await userInfo.DidNotReceiveWithAnyArgs().GetProfileAsync(Arg.Any<string>());
        await media.DidNotReceiveWithAnyArgs().GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ── fixtures ────────────────────────────────────────────────────────────

    private static Profile OwnerProfile() => new()
    {
        SubjectId = Owner,
        DisplayName = "Ada",
        Blocked = false,
        AvatarId = MediaId,
    };

    private static Profile OtherProfile() => new()
    {
        SubjectId = Other,
        DisplayName = "Bo",
        Blocked = false,
        AvatarId = MediaId,
    };

    private static MediaObject ServedMediaObject() => new()
    {
        Id = MediaId,
        ContentType = "image/png",
        SizeBytes = Png.Length,
        CreatedById = Owner,
    };

    /// <summary>
    /// Builds the <see cref="ProfileController"/> the repo's controller-test idiom does
    /// (<see cref="AnnouncementControllerTests"/>): NSubstitute for the three interface
    /// seams, a <em>real</em> <see cref="DirectoryService"/> (sealed — unproxyable — and
    /// referenced by no action under test), <c>Options.Create</c> for
    /// <see cref="MediaOptions"/>, and a principal (when <paramref name="principalSubjectId"/>
    /// is set) carrying the single <c>Kumunita.Sub</c> claim
    /// <see cref="KumunitaPrincipal"/> mints. Unauthenticated (M6) is the
    /// <see cref="DefaultHttpContext"/> default — an empty, unauthenticated principal.
    /// </summary>
    private static ProfileController Build(
        IUserInfoService userInfo,
        Kumunita.Core.Authorization.IAuthorizationService authz,
        IMediaStore media,
        string? principalSubjectId)
    {
        var controller = new ProfileController(
            userInfo,
            new DirectoryService(userInfo, authz),
            media,
            Options.Create(new MediaOptions()));

        var httpContext = new DefaultHttpContext();
        if (principalSubjectId is not null)
        {
            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, principalSubjectId) },
                    authenticationType: "test"));
        }

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }
}
