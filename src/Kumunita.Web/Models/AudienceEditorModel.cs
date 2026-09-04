using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kumunita.Core.Authorization;

namespace Kumunita.Web.Models;

/// <summary>
/// The <b>reusable</b> form-bound <see cref="Audience"/> editor (M2, plan U11). One
/// <see cref="ProfileEditViewModel"/> carries two of these — <see
/// cref="ProfileEditViewModel.Visibility"/> (the <see
/// cref="Kumunita.Core.UserInfo.Profile.Visibility"/> gate, always present) and
/// <see cref="ProfileEditViewModel.ContactVisibility"/> (the
/// <see cref="Kumunita.Core.UserInfo.Profile.ContactVisibility"/> opt-in gate, nullable) —
/// both bound through the <b>same</b> <c>Views/Profile/_AudienceEditor.cshtml</c> partial
/// (U11's "only one <c>_AudienceEditor.cshtml</c> exists" pin; the partial is a single
/// Razor template rendered for each nested model, not two parallel editor files).
/// <para>
/// <b>Why a JSON <see cref="Grants"/> string instead of a grant-list complex-binding
/// loop:</b> <see cref="AudienceGrant"/> is a <see cref="Record{T1, T2}"/>-shaped sealed
/// record (a <see cref="(GrantKind, string)"/> — no parameterless ctor, and
/// <see cref="GrantKind"/> is an <c>enum</c> the simple model binder would read as a
/// string and fail on). A hand-rolled grant-list <c>foreach</c> loop with per-element
/// sub-prefixes would be a parallel, hand-maintained audience shape next to
/// <see cref="Audience"/> — exactly the drift the design doc's line-615 pin forbids
/// ("if U11's editor writes a combined profile + contact audience in one shot, the
/// contact-block tests fail" — the *editor* must not introduce a *second* audience
/// object; the single-source pin is the editor and the <see cref="Audience"/> type are
/// the *same* shape through one binder). The JSON <see cref="Grants"/> field is a
/// *transport* for grants, not a second audience object (it carries <see
/// cref="AudienceGrant"/> elements verbatim, one JSON array, one binder call); the
/// editor round-trips through <see cref="BuildAudience"/> — <see cref="Audience"/> is
/// the only <b>audience</b> the <c>UpsertProfileAsync</c> patch ever receives (F13:
/// single write surface; no combined / merged audience).
/// </para>
/// <para>
/// <b>Binding:</b> <see cref="Mode"/> is a <see cref="string"/> carrying the
/// <c>AudienceMode</c> value name ("Any" / "All"), validated in <see cref="IsValid"/>
/// (not by an enum attribute — <c>AudienceMode</c> is a plain <c>enum</c> the radio
/// buttons post as plain "Any"/"All" strings). <see cref="Mode"/> empty on round-trip
/// (a form that didn't post a radio) fails <see cref="IsValid"/> → the action
/// re-renders with a validation summary (a form-bound audience that loses its mode is
/// the same class of bug as a form-bound owner id — the audience is part of the
/// editor's *shape*, never a silent default).
/// </para>
/// </summary>
public sealed class AudienceEditorModel
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>The <see cref="AudienceMode"/> as a form-bound string ("Any" / "All").
    /// Empty/unset fails validation (a post that loses the mode is malformed — not a
    /// silent default back to <c>Any</c>, which would <b>change the audience's
    /// meaning</b> in the All-mode-deny-on-empty-grants invariant (C1)); it must be the
    /// mode the user actually chose.</summary>
    [Required]
    public string? Mode { get; set; }

    /// <summary>The grants as a JSON array of <see cref="AudienceGrant"/> elements
    /// (<c>[{"Kind":"User","Id":"..."},{"Kind":"Group","Id":"..."}]</c> or
    /// <c>[]</c>). The <b>single</b> source of grant truth on the form — the partial's
    /// JS (a <c>data-</c> binding, not an inline edit) mutates this field's string, and
    /// <see cref="BuildAudience"/> (the one deserialization site) converts to the
    /// <see cref="Audience"/> the patch receives. Whitespace/null/empty → an empty
    /// <see cref="Audience"/> (the <c>Visibility</c> gate's bootstrap default per ADR
    /// 0001-B's self-only shape; the <c>ContactVisibility</c> gate's <c>null</c>
    /// shape is handled at the parent's <see cref="ProfileEditViewModel"/>, not here).</summary>
    public string? Grants { get; set; }

    /// <summary>true when the editor's grant list is empty (an empty audience — the
    /// C1 deny shape). Used by the partial's inline hint (an "empty audience denies
    /// everyone, including you — see the M1 bootstrap default" note). A *parse
    /// failure* (not "empty") reports <c>false</c> — the malformed shape is the
    /// <see cref="IsValid"/> false channel, not the "empty" hint.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Grants) ||
        (TryParseGrants(Grants, out var parsed) && parsed.Length == 0);

    /// <summary>
    /// Whether this editor's shape is well-formed for a round-trip. <see cref="Mode"/>
    /// must be present (a missing mode is a malformed post, not a default), and
    /// <see cref="Grants"/> if present must parse as a JSON array of
    /// <see cref="AudienceGrant"/> (a half-typed grant row is a <see cref="IsValid"/>
    /// false, not a silent drop).
    /// </summary>
    public bool IsValid
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Mode) ||
                !(Mode == "Any" || Mode == "All"))  // enum values exactly (C: no other audience mode exists)
                return false;

            if (string.IsNullOrWhiteSpace(Grants))
                return true;

            return TryParseGrants(Grants, out _);
        }
    }

    /// <summary>Parses <see cref="Grants"/> as an <see cref="AudienceGrant"/> array.
    /// The <b>single</b> deserialization site in the editor (the single-source pin) —
    /// a malformed string returns <c>false</c> (the editor's <see cref="IsValid"/> is
    /// the fail-fast channel); <paramref name="parsed"/> is non-null only on success.</summary>
    private static bool TryParseGrants(string? grants, out AudienceGrant[] parsed)
    {
        parsed = Array.Empty<AudienceGrant>();
        if (string.IsNullOrWhiteSpace(grants))
            return true;

        try
        {
            var array = JsonSerializer.Deserialize<AudienceGrant[]>(grants, JsonOptions);
            if (array is null || array.Any(g => g is null || string.IsNullOrWhiteSpace(g.Id)))
                return false;

            parsed = array;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Converts this editor's shape to the <see cref="Audience"/> document
    /// field. One deserialization site (the single-source pin); a malformed <see
    /// cref="Grants"/> string (which <see cref="IsValid"/> guards against) throws
    /// <see cref="InvalidOperationException"/> rather than silently returning an
    /// empty <see cref="Audience"/> (a "your grants are lost" failure is worse than a
    /// 500 the action surfaces as a validation failure — the action's <see
    /// cref="IsValid"/> guard runs <b>before</b> this is called, so in practice a
    /// malformed <see cref="Grants"/> never reaches this path; the throw is the
    /// fail-loud pin for a future caller that bypasses the guard).</summary>
    public Audience BuildAudience()
    {
        var mode = Mode is "All" ? AudienceMode.All : AudienceMode.Any;

        if (string.IsNullOrWhiteSpace(Grants))
            return new Audience(mode, Array.Empty<AudienceGrant>());

        var grants = JsonSerializer.Deserialize<AudienceGrant[]>(Grants, JsonOptions)
            ?? throw new InvalidOperationException(
                $"{nameof(AudienceEditorModel)}.{nameof(Grants)} failed to parse as an audience grant list.");

        return new Audience(mode, grants);
    }
}
