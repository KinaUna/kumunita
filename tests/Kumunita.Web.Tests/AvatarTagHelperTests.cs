using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Security;
using Kumunita.Web.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;
using NSubstitute;
using System.Text.Encodings.Web;

namespace Kumunita.Web.Tests;

/// <summary>
/// Pins the <see cref="AvatarTagHelper"/> (<c>kw-avatar</c>) rendering
/// contract — the branch the <see cref="IAvatarPresenceChecker"/>
/// presence decision selects.
/// <para>
/// <b>Why this harness.</b> Every avatar slot in the app (the ~29 rows in the
/// feed, post/reply, announcement, event, group, project, directory and
/// message surfaces) renders through <c>&lt;kw-avatar subject="..." .../&gt;</c>.
/// The whole point of the TagHelper is that when the resident has <b>no
/// avatar</b> (or their stored file is missing), the TagHelper emits the
/// monogram circle — or nothing — <b>without ever writing a
/// <c>&lt;img src="/profile/avatar/…"&gt;</c></b>, so the browser never makes
/// a request that the serving lane (<see cref="Kumunita.Web.Controllers.ProfileController.Avatar"/>,
/// ADR 0011) answers with a 404. The console was full of those doomed fetches
/// for every account without an avatar.
/// </para>
/// <para>
/// <b>The regression this owns.</b> A future rewording of the TagHelper (or a
/// re-introduction of the raw <c>&lt;img&gt;</c> markup in a view) would
/// silently re-introduce the 404 fetches. This test pins the branch directly:
/// present → <c>&lt;img&gt;</c> is emitted; absent → <b>no
/// <c>&lt;img&gt;</c>, no <c>/profile/avatar/</c> URL</b>, just the monogram
/// (or nothing when no monogram channel exists). The presence decision is
/// mocked (NSubstitute) so the branch under test is the rendering, not the
/// read.
/// </para>
/// </summary>
public class AvatarTagHelperTests
{
    private static readonly string Subject = "sub-123";
    private static readonly string Mono = "A";

    /// <summary>
    /// Build an <see cref="AvatarTagHelper"/> over the real
    /// <see cref="AvatarPresenceChecker"/> (the thin read seam over
    /// <c>IUserInfoService</c> + <c>IMediaStore</c>), with the two Core seams
    /// stubbed to deterministically control the present/absent answer. Run it
    /// against a <see cref="TagHelperOutput"/> and return the rendered inner
    /// content as a string.
    /// </summary>
    private static async Task<string> Run(bool present, string subject, string? mono, string? size)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var media = Substitute.For<IMediaStore>();
        if (present)
        {
            // Profile with a set avatar id + the media doc resolves — the
            // exact shape the serving lane would serve bytes for.
            userInfo.GetProfileAsync(subject).Returns(
                new Profile { SubjectId = subject, AvatarId = "media-1", Blocked = false });
            media.GetAsync("media-1").Returns(
                new MediaObject { Id = "media-1", ContentType = "image/png" });
        }
        else
        {
            // Absent: a profile with no avatar id (the common "no avatar set"
            // case). The checker folds this to false without reaching the
            // media doc — so media is never called in the absent branch.
            userInfo.GetProfileAsync(subject).Returns(
                new Profile { SubjectId = subject, AvatarId = null, Blocked = false });
        }

        var helper = new AvatarTagHelper(new AvatarPresenceChecker(userInfo, media))
        {
            Subject = subject,
            Mono = mono,
            Size = size ?? string.Empty,
        };

        var output = new TagHelperOutput(
            "kw-avatar",
            new TagHelperAttributeList(),
            (useCachedResult, encoder) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        var context = new TagHelperContext(
            "kw-avatar",
            new TagHelperAttributeList(),
            new Dictionary<object, object>(),
            uniqueId: "test");

        await helper.ProcessAsync(context, output).ConfigureAwait(false);

        // GetContent(HtmlEncoder) returns the fully-rendered inner HTML as a
        // string — the encoder is a no-op for the plain output in the tests
        // below (the one encoded-subject case is handled by its own assertion).
        return output.Content.GetContent(HtmlEncoder.Default);
    }

    [Fact(DisplayName = "Avatar present → emits the <img> at the audited serving lane (+ the hidden monogram fallback)")]
    public async Task Present_EmitsImg()
    {
        var rendered = await Run(present: true, subject: Subject, mono: Mono, size: null);

        // The standard set-avatar shape: the <img> at the serving lane, with
        // the hidden monogram sibling for the avatar.ts race fallback.
        Assert.Contains("<img ", rendered);
        Assert.Contains($"/profile/avatar/{Subject}", rendered);
        Assert.Contains("data-avatar-fallback", rendered);
        Assert.Contains("avatar-mono", rendered);
    }

    [Fact(DisplayName = "Avatar absent + monogram provided → renders the monogram circle, no <img>, no /profile/avatar/ fetch")]
    public async Task Absent_WithMono_RendersMonogram_NoFetch()
    {
        var rendered = await Run(present: false, subject: Subject, mono: Mono, size: null);

        // The monogram stands in directly. No broken <img>, no doomed URL —
        // this is the regression the user reported (404 console noise).
        Assert.Contains("avatar-mono", rendered);
        Assert.Contains(Mono, rendered);
        Assert.DoesNotContain("<img ", rendered);
        Assert.DoesNotContain("/profile/avatar/", rendered);
    }

    [Fact(DisplayName = "Avatar absent + no monogram channel → renders nothing (the comment-avatar shape)")]
    public async Task Absent_NoMono_RendersNothing()
    {
        var rendered = await Run(present: false, subject: Subject, mono: null, size: null);

        // No <img>, no monogram — the slot is empty. The previous markup
        // shipped a blank 404 slot here (an <img> the browser fetched and
        // failed); now there is nothing to fetch.
        Assert.DoesNotContain("<img ", rendered);
        Assert.DoesNotContain("avatar-mono", rendered);
        Assert.DoesNotContain("/profile/avatar/", rendered);
    }

    [Fact(DisplayName = "Avatar absent + size=lg → the monogram carries the avatar-lg size class (visually identical to the pre-kw-avatar markup)")]
    public async Task Absent_SizeLg_MonoCarriesSizeClass()
    {
        var rendered = await Run(present: false, subject: Subject, mono: Mono, size: "lg");

        // The size variant maps to the app's existing avatar-lg class so the
        // monogram is visually identical to the <img class="avatar-lg"> the
        // pre-kw-avatar markup emitted for a set avatar.
        Assert.Contains("avatar-mono avatar-lg", rendered);
        Assert.DoesNotContain("<img ", rendered);
    }

    [Fact(DisplayName = "Empty subject → suppresses output (no subject to resolve, no doomed fetch)")]
    public async Task EmptySubject_SuppressesOutput()
    {
        var rendered = await Run(present: true, subject: string.Empty, mono: Mono, size: null);

        // No subject → no render at all. The previous markup shipped a
        // <img src="/profile/avatar/"> that 404s; now there is no URL and no
        // element to fetch.
        Assert.DoesNotContain("<img ", rendered);
        Assert.DoesNotContain("/profile/avatar/", rendered);
        Assert.DoesNotContain("avatar-mono", rendered);
    }

    [Fact(DisplayName = "Subject id with a special character → HTML-encoded in the URL (no markup break-out)")]
    public async Task Subject_WithSpecialChar_IsEncodedInUrl()
    {
        // A subject id that contains a double-quote (pathological, but the
        // checker's contract is to HTML-encode it in the URL) must not break
        // out of the src="..." attribute. present=true so the <img> branch
        // renders (the only branch that writes the URL).
        var rendered = await Run(present: true, subject: "a\"b", mono: Mono, size: null);

        // The raw quote must not appear in the rendered output — the URL is
        // encoded so the attribute stays well-formed.
        Assert.DoesNotContain("\"a\"b", rendered);
        Assert.Contains("/profile/avatar/", rendered);
    }
}
