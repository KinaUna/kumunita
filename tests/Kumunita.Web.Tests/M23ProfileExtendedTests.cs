using System.Reflection;
using System.Security.Claims;
using Kumunita.Core.Authorization;
using Kumunita.Core.Localization;
using Kumunita.Core.Media;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Controllers;
using Kumunita.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Kumunita.Web.Tests;

/// <summary>
/// M23 · U04 — the Web-surface pin (the U04 exit gate's Web.Tests-only half).
/// Four faces, matching the plan's <c>## Tests (Web)</c> section:
/// <list type="number">
/// <item><b>The editor binds the two new fields (D5, C-M23·6).</b> A valid
///       <see cref="ProfileEditViewModel"/> with a bio + a tags string maps to a
///       <see cref="ProfileUpdate"/> whose <c>Bio</c> is the trimmed authored
///       string (verbatim) and <c>TagIds</c> the parsed slugs (lowercased,
///       trimmed, de-duplicated). A blank bio → <c>Bio = null</c> (the "no bio
///       block" shape); a blank tags string → <c>TagIds = null</c> (the
///       "null ⇒ don't touch" patch rule).</item>
/// <item><b>The edit action passes the viewer's subject as <c>actorBy</c>
///       (D3, C-TG·9).</b> A POST to /profile/edit writes through
///       <see cref="IUserInfoService.UpsertProfileAsync"/> with the signed-in
///       subject as the third (actorBy) argument — the C-TG·9 tag-<c>createdBy</c>
///       provenance the U02 write lane uses for each new tag. The only write is
///       that single <c>UpsertProfileAsync</c> call — no separate
///       profile-write <c>AccessAudit</c> row (C-M23·3).</item>
/// <item><b>The directory-detail gate (F6 / §2.5, D2/D6) + self-view (F3) +
///       two-gate independence (F2).</b> A profile's <c>Bio</c> is projected on
///       the <c>DirectoryViewModel.Detail</c> only when the profile's
///       <c>Visibility</c> audience admits the viewer (the frozen
///       <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
///       <c>CanAsync</c> on <see cref="ProfileToAuditableResource"/>); a denied
///       viewer gets the name + verified badge but no bio; the owner always sees
///       their own (the F3 self-view short-circuit — no <c>CanAsync</c>, no
///       audit row). The contact block (address/email/phone) rides the
///       <b>independent</b> <c>ContactVisibility</c> audience: admitting the
///       contact block does not admit the bio, and vice versa (F2 "two
///       independent gates, zero new audiences").</item>
/// <item><b>Zero new authorization surface (GATE-4, C-M23·2, D7) + the seven
///       U04 kw-l keys non-empty in en/de/fr/da.</b> A reflection pin that
///       <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>'s public
///       method set is <b>exactly</b> the eight pre-existing signatures (no new
///       method for the bio/tags gate) and <see cref="Kumunita.Core.Identity.ClaimTypes.All"/>
///       is unchanged (four claims); plus a parity pin that each of the seven
///       U04 <c>kw-l</c> keys carries a non-empty value in all four languages.</item>
/// </list>
/// <para>
/// <b>Test-construction floor (transparency of the pin):</b> the two controllers
/// under test are driven by direct action invocation with NSubstitute for the
/// Core seams (the <see cref="ProfileAvatarUploadTests"/> /
/// <see cref="AnnouncementControllerTests"/> idiom), a <em>real</em> sealed
/// <see cref="DirectoryService"/>, and a principal carrying the single
/// <c>Kumunita.Sub</c> claim <see cref="KumunitaPrincipal"/> mints. The
/// <c>MarkdownRenderer</c> render path and the tag display-name resolution (which
/// needs a live Marten <c>IQuerySession</c> — the <c>MartenLinqQueryable</c>
/// <c>ToListAsync()</c> wall that cannot be NSubstituted) are the Core.Tests /
/// e2e half of the U04 gate; the tag <em>gate</em> itself (the
/// <c>showBioTags</c> projection decision) is what these tests pin at the Web
/// boundary, via the <c>Bio</c> field (store-independent).
/// </para>
/// </summary>
public sealed class M23ProfileExtendedTests
{
    private const string Owner = "subj-m23-owner";
    private const string ViewerA = "subj-m23-viewer-a";
    private const string ViewerB = "subj-m23-viewer-b";

    // ── Face 1 — the editor binds the two new fields (D5, C-M23·6) ──────

    /// <summary>
    /// D5/C-M23·6 — a bio + a tags string on the editor maps to the U02
    /// <see cref="ProfileUpdate"/> extension: <c>Bio</c> is the authored string
    /// (trimmed, verbatim — D3), and <c>TagIds</c> is the ADR 0044 tag-input
    /// string parsed to slugs (lowercased + trimmed, de-duplicated).
    /// </summary>
    [Fact]
    public void Editor_Binds_BioAndTags_ToPatch()
    {
        var model = Base();
        model.Bio = "  Hi — I garden a lot.  ";
        model.Tags = "Gardening,  Baking, gardening";

        var (_, patch) = model.ToProfileUpdate(Owner);

        // Bio: written verbatim (D3), trimmed of surrounding whitespace.
        Assert.Equal("Hi — I garden a lot.", patch.Bio);
        // TagIds: the ADR 0044 slug shape — lowercased, trimmed, de-duplicated,
        // order-preserving (the author's set order; the first occurrence wins).
        Assert.Equal(new[] { "gardening", "baking" }, patch.TagIds!.ToArray());
    }

    /// <summary>
    /// C-M23·6 / D5 — a blank bio → <c>Bio = null</c> (the "no bio block"
    /// shape), and a blank tags string → <c>TagIds = null</c> (the
    /// "null ⇒ don't touch" patch rule — a blank edit leaves the current tags,
    /// it does not wipe them to an empty list). "Blank" is the
    /// <see cref="string.IsNullOrWhiteSpace"/> contract: a genuinely
    /// whitespace-only field (a separators-only string like "  ,  " is NOT
    /// blank — it parses to an empty slug list, a separate, deliberate shape).
    /// </summary>
    [Fact]
    public void Editor_BlankBioAndTags_MapToNull()
    {
        var model = Base();
        model.Bio = "   ";
        model.Tags = "   ";   // whitespace-only ⇒ IsNullOrWhiteSpace ⇒ null (don't touch)

        var (_, patch) = model.ToProfileUpdate(Owner);

        Assert.Null(patch.Bio);
        Assert.Null(patch.TagIds);
    }

    /// <summary>
    /// The deliberate <c>null</c> vs <c>empty-list</c> distinction on
    /// <see cref="ProfileUpdate.TagIds"/> (the "null ⇒ don't touch" patch rule):
    /// a <b>separators-only</b> tags string ("  ,  ") is <b>not</b> blank, so it
    /// parses to an <b>empty</b> slug list (<c>[]</c>, "clear all tags") rather
    /// than <c>null</c> ("leave the current tags untouched"). A reader must not
    /// "simplify" the editor so a non-blank-but-separator-only input collapses to
    /// <c>null</c> — that would silently wipe nothing where it should clear.
    /// </summary>
    [Fact]
    public void Editor_SeparatorOnlyTags_MapToEmptyList_NotNull()
    {
        var model = Base();
        model.Tags = "  ,  ";

        var (_, patch) = model.ToProfileUpdate(Owner);

        Assert.NotNull(patch.TagIds);   // not the "don't touch" (null) shape
        Assert.Empty(patch.TagIds!);    // parsed to the empty slug list (clear all)
    }

    /// <summary>
    /// C-TG·4 slug-shape floor at the editor boundary: a token longer than 64
    /// chars is dropped (the <see cref="TagService"/> <c>DeriveSlug</c> shape),
    /// a blank token is dropped, and valid tokens survive — so a malformed
    /// free-form string can never push a >64-char slug into the write lane.
    /// </summary>
    [Fact]
    public void Editor_Drops_OversizeAndBlank_Tags()
    {
        var model = Base();
        // 65 'a' chars (>64 → dropped by the C-TG·4 slug-shape floor), a valid
        // tag, a blank token (dropped), and exactly 64 'b' chars (at the bound → kept).
        model.Tags = $"{new string('a', 65)}, real-tag,  , {new string('b', 64)}";

        var (_, patch) = model.ToProfileUpdate(Owner);

        Assert.Equal(new[] { "real-tag", new string('b', 64) }, patch.TagIds!.ToArray());
    }

    // ── Face 2 — the edit action passes the viewer's subject as actorBy (D3, C-TG·9) ──

    /// <summary>
    /// D3/C-TG·9 — a valid POST to /profile/edit writes through the single
    /// frozen write lane <see cref="IUserInfoService.UpsertProfileAsync"/> with
    /// the signed-in subject as the third (<c>actorBy</c>) argument — the
    /// C-TG·9 provenance the U02 write lane stamps onto each new tag's
    /// <c>CreatedBy</c>. The bio + resolved slugs ride the patch. Exactly one
    /// write; no separate profile-write audit row (C-M23·3).
    /// </summary>
    [Fact]
    public async Task EditAction_Passes_ViewerSubject_AsActorBy()
    {
        var (controller, userInfo) = BuildProfileController(Owner);

        var model = Base();
        model.Bio = "I bake.";
        model.Tags = "baking, gardening";

        var result = await controller.Edit(model);

        Assert.IsType<RedirectToActionResult>(result);

        // The write is the single UpsertProfileAsync lane; the third argument
        // (actorBy) is the signed-in subject — never a form value.
        await userInfo.Received(1).UpsertProfileAsync(
            Arg.Is<Profile>(p => p.SubjectId == Owner),
            Arg.Is<ProfileUpdate>(x =>
                x.Bio == "I bake." &&
                x.TagIds!.ToArray().SequenceEqual(new[] { "baking", "gardening" })),
            Owner);

        // The saved flash resolves to the en floor (the test-construction
        // seam — no live ITranslationProvider) — the seven U04 keys' en text.
        Assert.Equal(KnownTranslationKeys.EnValues["profile.flash.saved"], controller.TempData["info"]);
    }

    // ── Face 3 — the directory-detail gate (F6/§2.5), self-view (F3), two gates (F2) ──

    /// <summary>
    /// F6/§2.5 (D2/D6, C-M23·4) — a profile whose <c>Visibility</c> admits
    /// viewer A: A's /directory/[subjectId] projects the bio (and, when a
    /// document store is present, the tags). A denied viewer B gets the name
    /// + verified badge but <b>no</b> bio. The gate is the frozen
    /// <see cref="ProfileToAuditableResource"/> + <c>CanAsync</c> — the
    /// M2 contact-block gate shape (zero new authorization surface).
    /// </summary>
    [Fact]
    public async Task DirectoryDetail_BioGate_AllowsAdmitted_DeniesOther()
    {
        var target = TargetProfile();   // Bio + TagIds set; Visibility/ContactVisibility distinct non-null audiences.

        // Viewer A — Visibility admits:
        var (controllerA, authzA) = BuildDirectoryController(ViewerA, target);
        authzA.CanAsync(ViewerA, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.Visibility))
            .Returns(new Decision(true, AccessVia.Audience, ViewerA));
        // (the contact audience is not asserted for A here; a default Deny)
        authzA.CanAsync(ViewerA, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.ContactVisibility))
            .Returns(new Decision(false, AccessVia.Audience, ViewerA));

        var aModel = DetailModel(await controllerA.Detail(target.SubjectId));
        Assert.Equal(target.Bio, aModel.Bio);          // bio projected (Visibility admitted)
        Assert.Equal(target.DisplayName, aModel.DisplayName);
        Assert.True(aModel.Verified);
        Assert.Null(aModel.Email);                     // contact denied → contact fields null

        // Viewer B — Visibility denies:
        var (controllerB, authzB) = BuildDirectoryController(ViewerB, target);
        authzB.CanAsync(ViewerB, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.Visibility))
            .Returns(new Decision(false, AccessVia.Audience, ViewerB));
        authzB.CanAsync(ViewerB, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.ContactVisibility))
            .Returns(new Decision(true, AccessVia.Audience, ViewerB));

        var bModel = DetailModel(await controllerB.Detail(target.SubjectId));
        Assert.Equal(target.DisplayName, bModel.DisplayName);   // basic info still renders
        Assert.True(bModel.Verified);
        Assert.Null(bModel.Bio);                                  // bio NOT projected (Visibility denied)
        Assert.Equal(target.Email, bModel.Email);                 // contact admitted → contact fields present
    }

    /// <summary>
    /// F3 — the self-view short-circuit: the owner's /directory/[ownSubjectId]
    /// projects their own bio even when their <c>Visibility</c> audience excludes
    /// everyone else. No <c>CanAsync</c> decision runs on the bio gate (the
    /// controller short-circuits on <c>viewer == owner</c>), and the
    /// <see cref="DirectoryService"/> short-circuits the contact gate — so
    /// <b>zero</b> <c>CanAsync</c> calls, zero audit rows.
    /// </summary>
    [Fact]
    public async Task DirectoryDetail_SelfView_SeesOwnBio_Without_CanAsync()
    {
        var target = TargetProfile();   // owner = Owner.

        var (controller, authz) = BuildDirectoryController(Owner, target);

        var model = DetailModel(await controller.Detail(Owner));

        Assert.Equal(target.Bio, model.Bio);              // owner sees their own bio
        Assert.Equal(target.Email, model.Email);          // owner sees their own contact block (DirectoryService self-view)
        Assert.True(model.ShowContactBlock);

        // The pin that matters (F3): the bio gate ran NO CanAsync decision on
        // the profile's Visibility — and the contact gate did either
        // (self-view short-circuit). So the authz seam was never consulted.
        await authz.DidNotReceiveWithAnyArgs()
            .CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>());
    }

    /// <summary>
    /// F2 — "two independent gates, zero new audiences": the contact block
    /// (address/email/phone, on <c>ContactVisibility</c>) and the bio/tags
    /// (on <c>Visibility</c>) are gated <b>independently</b>. Admitting the
    /// contact block does not admit the bio, and admitting the bio does not
    /// admit the contact block.
    /// </summary>
    [Fact]
    public async Task DirectoryDetail_ContactAndBio_AreIndependent_Gates()
    {
        // Case 1 — contact ADMITS A, bio DENIES A: contact fields present, bio absent.
        {
            var target = TargetProfile();
            var (controller, authz) = BuildDirectoryController(ViewerA, target);
            authz.CanAsync(ViewerA, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.ContactVisibility))
                .Returns(new Decision(true, AccessVia.Audience, ViewerA));
            authz.CanAsync(ViewerA, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.Visibility))
                .Returns(new Decision(false, AccessVia.Audience, ViewerA));

            var model = DetailModel(await controller.Detail(target.SubjectId));
            Assert.True(model.ShowContactBlock);
            Assert.Equal(target.Email, model.Email);   // contact block present
            Assert.Equal(target.Address, model.Address);
            Assert.Null(model.Bio);                    // bio absent — the gates are independent
        }

        // Case 2 — contact DENIES A, bio ADMITS A: bio present, contact fields null.
        {
            var target = TargetProfile();
            var (controller, authz) = BuildDirectoryController(ViewerA, target);
            authz.CanAsync(ViewerA, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.Visibility))
                .Returns(new Decision(true, AccessVia.Audience, ViewerA));
            authz.CanAsync(ViewerA, AccessAction.Read, Arg.Is<IAuditableResource>(t => t.Audience == target.ContactVisibility))
                .Returns(new Decision(false, AccessVia.Audience, ViewerA));

            var model = DetailModel(await controller.Detail(target.SubjectId));
            Assert.Null(model.Email);                  // contact block absent
            Assert.False(model.ShowContactBlock);
            Assert.Equal(target.Bio, model.Bio);       // bio present — the gates are independent
        }
    }

    /// <summary>
    /// Fail-closed floor (D2) — when the authorization seam is absent at the
    /// Web boundary (a test-construction site), a non-self viewer's bio is
    /// <b>not</b> projected (a missing <c>IAuthorizationService</c> is treated
    /// as a deny, so a resident's bio can never leak through an absent gate).
    /// The owner still sees their own (the F3 short-circuit precedes the gate).
    /// </summary>
    [Fact]
    public async Task DirectoryDetail_MissingAuthzSeam_IsFailClosed_NonSelf()
    {
        var target = TargetProfile();
        var (controller, _) = BuildDirectoryControllerNoAuthz(ViewerA, target);

        var model = DetailModel(await controller.Detail(target.SubjectId));
        Assert.Equal(target.DisplayName, model.DisplayName);
        Assert.Null(model.Bio);   // fail-closed: no seam ⇒ a non-self viewer gets no bio

        // Owner is still fine (self-view short-circuits before the gate).
        var (ownerController, _) = BuildDirectoryControllerNoAuthz(Owner, target);
        var ownerModel = DetailModel(await ownerController.Detail(Owner));
        Assert.Equal(target.Bio, ownerModel.Bio);
    }

    // ── Face 4 — zero new authorization surface (GATE-4) + seven kw-l keys ──

    /// <summary>
    /// GATE-4 / C-M23·2 / D7 — the bio/tags gate introduces <b>no</b> new
    /// authorization surface: <see cref="Kumunita.Core.Authorization.IAuthorizationService"/>
    /// exposes exactly the eight pre-existing method signatures (the two
    /// <c>CanAsync</c> / two <c>CanSeeAsync</c> / two
    /// <c>CanSeeGroupAsync</c> / two <c>CanSeeGroupFeedAsync</c> — the
    /// ADR 0013 group-lane additions being the only post-frozen additions, and
    /// they predate M23), and <see cref="Kumunita.Core.Identity.ClaimTypes.All"/>
    /// is unchanged (the four admissible claims). A "U04 added a
    /// <c>CanSeeProfileBioAsync</c>" or a new claim pin would fail here.
    /// </summary>
    [Fact]
    public void AuthorizationSurface_Is_Unchanged_ZeroNewSurface()
    {
        var signatures = typeof(Kumunita.Core.Authorization.IAuthorizationService)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => !m.IsSpecialName && !m.IsGenericMethod)
            .Select(m => (m.Name, m.GetParameters().Length))
            .OrderBy(t => t.Name, StringComparer.Ordinal)
            .ThenBy(t => t.Item2)
            .ToList();

        Assert.Equal(
            new (string, int)[]
            {
                ("CanAsync", 3), ("CanAsync", 4),
                ("CanSeeAsync", 3), ("CanSeeAsync", 4),
                ("CanSeeGroupAsync", 3), ("CanSeeGroupAsync", 4),
                ("CanSeeGroupFeedAsync", 3), ("CanSeeGroupFeedAsync", 4),
            },
            signatures);

        var claims = Kumunita.Core.Identity.ClaimTypes.All
            .OrderBy(c => c, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(
            new[] { "Kumunita.ExternalId", "Kumunita.Role", "Kumunita.Sub", "Kumunita.Verified" },
            claims);
    }

    /// <summary>
    /// The seven U04 <c>kw-l</c> keys (the plan's §key table) each carry a
    /// non-empty value in all four languages (the
    /// <see cref="KnownTranslationKeys"/> en/de/fr/da registry — the
    /// <c>KwLRegistryConsistencyTests</c> + <c>KnownTranslationKeys_ParityTests</c>
    /// gate, asserted from the Web assembly so the U04 Web exit pins its own
    /// surface). A blank value in any language would make the provider floor
    /// resolve to nothing for that key under that preference.
    /// </summary>
    [Fact]
    public void U04_KwLKeys_Are_NonEmpty_InAllFourLanguages()
    {
        var keys = new[]
        {
            "profile.edit.bio",
            "profile.edit.tags",
            "profile.edit.tags.placeholder",
            "profile.detail.bio",
            "profile.detail.tags",
            "profile.detail.tags.empty",
            "profile.flash.saved",
        };
        Assert.Equal(7, keys.Length);

        var registry = new (string Name, IReadOnlyDictionary<string, string> Values)[]
        {
            ("en", KnownTranslationKeys.EnValues),
            ("de", KnownTranslationKeys.DeValues),
            ("fr", KnownTranslationKeys.FrValues),
            ("da", KnownTranslationKeys.DaValues),
        };

        foreach (var (lang, values) in registry)
        {
            foreach (var key in keys)
            {
                Assert.True(values.ContainsKey(key),
                    $"U04 kw-l key '{key}' is missing from the {lang} registry (KnownTranslationKeys.{lang}Values)");
                Assert.False(string.IsNullOrWhiteSpace(values[key]),
                    $"U04 kw-l key '{key}' has an empty {lang} value — the provider floor would resolve it to nothing");
            }
        }
    }

    // ── fixtures ────────────────────────────────────────────────────────────

    private static AudienceEditorModel WellFormed(string mode = "Any")
        => new() { Mode = mode, Grants = null };

    private static ProfileEditViewModel Base() => new()
    {
        DisplayName = "A. Resident",
        Email = "a@example.kumunita",
        Visibility = WellFormed("Any"),
        OptInContactVisibility = false,
        ContactVisibility = null,
    };

    /// <summary>
    /// The target profile the directory-detail gate tests project: an owner with
    /// a <c>Bio</c> + <c>TagIds</c>, a <c>Verified</c> badge, and — critically —
    /// <b>two distinct non-null</b> <see cref="Audience"/> instances for
    /// <c>Visibility</c> (the bio gate) and <c>ContactVisibility</c> (the contact
    /// gate) so the NSubstitute routing on <c>target.Audience</c> (identity
    /// comparison) can distinguish the two decisions.
    /// </summary>
    private static Profile TargetProfile() => new()
    {
        SubjectId = Owner,
        DisplayName = "A. Resident",
        Verified = true,
        Blocked = false,
        Bio = "I tend the community allotment and bake bread on Saturdays.",
        TagIds = new[] { "gardening", "baking" },
        Visibility = new Audience(AudienceMode.All, new[] { new AudienceGrant(GrantKind.User, ViewerA) }),
        ContactVisibility = new Audience(AudienceMode.All, new[] { new AudienceGrant(GrantKind.User, ViewerA) }),
        Email = "a@example.kumunita",
        Phone = "+1 555 0100",
        Address = "12 Maple Lane",
    };

    /// <summary>An in-memory <see cref="ITempDataProvider"/> — closes the
    /// <c>TempData</c> bag for the save-lane flash write so the assertion can
    /// read <c>TempData["info"]</c> after the action returns (the
    /// <see cref="AdminQuietControllerTests"/> idiom).</summary>
    private sealed class NoOpTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object?> _bag = new();
        public IDictionary<string, object?> LoadTempData(HttpContext context) => _bag;
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            _bag.Clear();
            foreach (var (k, v) in values) _bag[k] = v;
        }
    }

    private static (ProfileController controller, IUserInfoService userInfo) BuildProfileController(
        string principalSubjectId)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        var media = Substitute.For<IMediaStore>();
        var controller = new ProfileController(
            userInfo,
            new DirectoryService(userInfo, Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>()),
            media,
            Options.Create(new MediaOptions()));

        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, principalSubjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new NoOpTempDataProvider());
        return (controller, userInfo);
    }

    private static (DirectoryController controller, Kumunita.Core.Authorization.IAuthorizationService authz)
        BuildDirectoryController(string viewerSubjectId, Profile targetProfile)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(targetProfile.SubjectId).Returns(targetProfile);
        var authz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        var directory = new DirectoryService(userInfo, authz);
        // The bio/tags gate's document-store + translation seams are null (the
        // test-construction floor): tag display-name resolution is a no-op
        // (TagNames = null), but the bio projection + the contact gate are
        // fully exercised (the store is only needed for the tag *names*, which
        // the e2e / Core.Tests half pins against a live Marten session).
        var controller = new DirectoryController(directory, authz, store: null, localization: null, translationProvider: null);
        SetViewer(controller, viewerSubjectId);
        return (controller, authz);
    }

    private static (DirectoryController controller, Kumunita.Core.Authorization.IAuthorizationService? authz)
        BuildDirectoryControllerNoAuthz(string viewerSubjectId, Profile targetProfile)
    {
        var userInfo = Substitute.For<IUserInfoService>();
        userInfo.GetProfileAsync(targetProfile.SubjectId).Returns(targetProfile);
        // authz = null: the DirectoryService still needs a non-null authz to
        // construct (it null-checks), so give it a stub — but the controller's
        // OWN bio-gate seam (the second ctor arg) is null, which is what the
        // fail-closed floor asserts. The stub's contact-gate decision resolves to
        // Deny (Decision is a class — NSubstitute's default return is null, which
        // would NRE on `.Allowed` in DirectoryService.EvaluateContactGateAsync for
        // a non-self viewer); the test's assertion is on the *controller's* bio
        // gate being fail-closed, so the contact block's value is irrelevant here.
        var stubAuthz = Substitute.For<Kumunita.Core.Authorization.IAuthorizationService>();
        stubAuthz.CanAsync(Arg.Any<string>(), Arg.Any<AccessAction>(), Arg.Any<IAuditableResource>())
            .Returns(new Decision(false, AccessVia.Audience, ViewerA));
        var directory = new DirectoryService(userInfo, stubAuthz);
        var controller = new DirectoryController(directory, authz: null, store: null, localization: null, translationProvider: null);
        SetViewer(controller, viewerSubjectId);
        return (controller, null);
    }

    private static void SetViewer(ControllerBase controller, string viewerSubjectId)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                new[] { new Claim(Kumunita.Core.Identity.ClaimTypes.Subject, viewerSubjectId) },
                authenticationType: "test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    private static DirectoryViewModel.Detail DetailModel(IActionResult result)
    {
        var view = Assert.IsType<ViewResult>(result);
        return Assert.IsType<DirectoryViewModel.Detail>(view.Model);
    }
}
