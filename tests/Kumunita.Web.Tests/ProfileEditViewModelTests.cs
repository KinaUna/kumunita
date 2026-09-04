using System.Reflection;
using Kumunita.Core.Authorization;
using Kumunita.Core.UserInfo;
using Kumunita.Web.Models;

namespace Kumunita.Web.Tests;

/// <summary>
/// M2, plan U11 — the profile editor's view-model pin (mirrors U8's
/// <c>DirectoryDetailViewModelTests</c>, U9's <c>GroupsViewModelTests</c>,
/// U10's <c>GroupsDetailViewModelTests</c>): the shape of the editor's form
/// model, and the §9 gate ("no contact opt-in without a profile gate").
/// </summary>
public sealed class ProfileEditViewModelTests
{
    [Fact]
    public void ProfileEditViewModel_Has_Exactly_Five_FormFields()
    {
        var fields = typeof(ProfileEditViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[]
        {
            "ContactVisibility",
            "DisplayName",
            "Email",
            "OptInContactVisibility",
            "Visibility",
        }, fields);
    }

    [Fact]
    public void ProfilePreviewViewModel_Has_Exactly_Five_Fields()
    {
        var fields = typeof(ProfilePreviewViewModel)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[]
        {
            "AsDisplayName",
            "Email",
            "IsVisible",
            "Phone",
            "ShowContactBlock",
        }, fields);
    }

    [Fact]
    public void ViewAsOption_Has_Exactly_Two_Projected_Fields()
    {
        var fields = typeof(ViewAsOption)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(new[] { "DisplayName", "SubjectId" }, fields);
    }

    private static AudienceEditorModel WellFormed(string mode = "Any") =>
        new() { Mode = mode, Grants = null };

    private static ProfileEditViewModel Base(string visMode = "Any") =>
        new()
        {
            DisplayName = "A. Resident",
            Email = "a@example.kumunita",
            Visibility = WellFormed(visMode),
            OptInContactVisibility = false,
            ContactVisibility = null,
        };

    // ── §9 pin — the plan's U11 exit test ─────────────────────────────────

    /// <summary>
    /// Plan U11 exit test (literal name): the contact opt-in is settable to a
    /// non-null, well-formed <see cref="AudienceEditorModel"/> only when — and
    /// only when — the <c>Visibility</c> editor is itself well-formed AND the
    /// author has opted in. Four sub-cases cover the §2.4 truth table read at
    /// the Web-model layer (C-M2·1 at the view-model, not the Core layer — the
    /// <c>DirectoryService</c> pins the same rule at evaluation time, U6).
    /// </summary>
    [Fact]
    public void ProfileEditViewModel_ContactVisibility_Gated()
    {
        // Case 1 — opt-IN with a well-formed Visibility + well-formed contact
        // editor: VALID. This is the "Any+non-empty" allowed row read at the
        // form level — the post will build a non-null contact gate.
        var optInBothOk = Base();
        optInBothOk.OptInContactVisibility = true;
        optInBothOk.ContactVisibility = WellFormed("Any");
        Assert.True(optInBothOk.IsValid);

        // Case 2 — opt-IN but the Visibility editor is MALFORMED (mode
        // missing): INVALID. A "contact opt-in next to a broken profile gate"
        // is a silent-drop bug the <c>IsValid</c> gate must catch — the
        // malformed Visibility never reaches Core, so this cannot be left to
        // the service's early-return.
        var brokenVisibility = Base();
        brokenVisibility.Visibility = new AudienceEditorModel { Mode = null, Grants = null };
        brokenVisibility.OptInContactVisibility = true;
        brokenVisibility.ContactVisibility = WellFormed("Any");
        Assert.False(brokenVisibility.IsValid);

        // Case 3 — opt-IN with a well-formed Visibility but a MALFORMED
        // contact editor (mode missing): INVALID. The §9 gate — opting in
        // must be accompanied by a well-formed contact shape; a half-typed
        // gate is a validation failure, not a silent drop to null.
        var brokenContact = Base();
        brokenContact.OptInContactVisibility = true;
        brokenContact.ContactVisibility = new AudienceEditorModel { Mode = null, Grants = null };
        Assert.False(brokenContact.IsValid);

        // Case 4 — opt-OUT: the contact editor's shape is irrelevant (it may
        // be null) — VALID as long as the (always-required) Visibility is
        // well-formed. This is the "null ⇒ short-circuit" reading: the patch
        // will emit a null gate, and the post is not rejected.
        var optOut = Base();
        optOut.OptInContactVisibility = false;
        optOut.ContactVisibility = null;
        Assert.True(optOut.IsValid);
    }

    // ── §2.4 "null ⇒ short-circuit" patch shape ────────────────────────────

    /// <summary>
    /// The §2.4 "null ⇒ short-circuit" pin at the patch-builder level: when
    /// the author opts OUT, <see cref="ProfileEditViewModel.ToProfileUpdate"/>
    /// emits a <b>null</b> <c>ContactVisibility</c> patch field — a distinct
    /// shape from an <b>empty</b> <see cref="Audience"/> (which still denies
    /// everyone per C1, but is a *non-null* gate that would be *evaluated*).
    /// Conflating the two ("always build the audience, default empty") would
    /// turn a "contact hidden because not evaluated" profile into a "contact
    /// hidden because an empty gate denied" profile — a different audit-lane
    /// shape (C3).
    /// </summary>
    [Fact]
    public void ProfileEditViewModel_Patch_Off_Emits_NullGate_On_Emits_Audience()
    {
        // Off → the contact gate patch field is NULL (the §2.4 short-circuit).
        var off = Base();
        off.OptInContactVisibility = false;
        off.ContactVisibility = null;
        var (profileOff, patchOff) = off.ToProfileUpdate("subj-a");

        Assert.Equal("subj-a", profileOff.SubjectId);
        Assert.NotNull(patchOff.Visibility);           // the profile gate is always built
        Assert.Null(patchOff.ContactVisibility);       // the opt-out shape: null, not empty

        // On → the contact gate patch field is the built Audience (non-null).
        var on = Base();
        on.OptInContactVisibility = true;
        on.ContactVisibility = WellFormed("All");
        var (_, patchOn) = on.ToProfileUpdate("subj-a");

        Assert.NotNull(patchOn.Visibility);
        Assert.NotNull(patchOn.ContactVisibility);
    }

    /// <summary>
    /// F13 single-write pin: the patch's <c>Visibility</c> /
    /// <c>ContactVisibility</c> are built from exactly the editor's two
    /// <see cref="AudienceEditorModel"/> fields through the shared
    /// <c>BuildAudience</c> deserializer — the Mode the editor carried is the
    /// Mode <c>Audience</c> the patch received (no re-derivation, no default,
    /// no merged compound audience). Drift-guard: a "U11's editor writes a
    /// combined audience" regression would change the Mode or the grant set
    /// relative to what the form posted.
    /// </summary>
    [Fact]
    public void ProfileEditViewModel_Patch_Audiences_MatchTheEditors()
    {
        var grants = "[{\"Kind\":\"User\",\"Id\":\"subj-b\"}]";
        var on = new ProfileEditViewModel
        {
            DisplayName = "A. Resident",
            Email = "a@example.kumunita",
            Visibility = new AudienceEditorModel { Mode = "All", Grants = grants },
            OptInContactVisibility = true,
            ContactVisibility = new AudienceEditorModel { Mode = "Any", Grants = null },
        };

        Assert.True(on.IsValid);

        var (_, patch) = on.ToProfileUpdate("subj-a");

        // Visibility: the exact Mode + grant set the editor posted.
        Assert.Equal(AudienceMode.All, patch.Visibility!.Mode);
        Assert.Single(patch.Visibility.Grants);
        Assert.Equal(GrantKind.User, patch.Visibility.Grants[0].Kind);
        Assert.Equal("subj-b", patch.Visibility.Grants[0].Id);

        // ContactVisibility: the exact Mode the editor posted, empty grant
        // list (the editor's Grants was null → the "any matches" empty
        // shape — a valid, non-null gate, distinct from the opt-out null).
        Assert.Equal(AudienceMode.Any, patch.ContactVisibility!.Mode);
        Assert.True(patch.ContactVisibility.IsEmpty);
    }

    /// <summary>
    /// Shape drift-guard: the <see cref="AudienceEditorMode"/> (the
    /// <c>Mode</c> string field) on the editor is <b>required</b> at the
    /// attribute level — a future refactor that drops the <c>[Required]</c>
    /// (or adds a "silently default to Any" fallback) would violate the ADR
    /// 0001-B "the author's choice is absolute" pin at the shape level.
    /// </summary>
    [Fact]
    public void AudienceEditorModel_Mode_Is_Required()
    {
        var mode = typeof(AudienceEditorModel).GetProperty("Mode")
            ?? throw new InvalidOperationException("AudienceEditorModel must expose a 'Mode' property.");

        var isRequired = mode.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.RequiredAttribute), true)
            .OfType<System.ComponentModel.DataAnnotations.RequiredAttribute>()
            .Any();

        Assert.True(isRequired);
    }
}
